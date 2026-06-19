using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    // Saved Tavern Table logic for future iteration
    public class TavernTableWindow : Window
    {
        private readonly Plugin plugin;
        private float hoverTime = 0f;
        private bool shouldResetPosition = false;
        private Vector2? dragTargetPosition = null;
        private bool isFirstDraw = true;

        public TavernTableWindow(Plugin plugin) : base("Tavern Table Layout###FaeLightCardsTavernTableLayoutWindow")
        {
            this.plugin = plugin;
            this.IsOpen = false;
            this.RespectCloseHotkey = false;

            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(350, 480),
                MaximumSize = new Vector2(800, 1000)
            };

            this.Size = new Vector2(380, 600);
            this.SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void ResetPosition()
        {
            shouldResetPosition = true;
        }

        public override void PreDraw()
        {
            ImGuiWindowFlags flags = ImGuiWindowFlags.NoScrollbar
                                   | ImGuiWindowFlags.NoScrollWithMouse;

            if (plugin.Configuration.IsLocked)
            {
                flags |= ImGuiWindowFlags.NoMove;
            }

            this.Flags = flags;

            if (dragTargetPosition.HasValue)
            {
                this.Position = dragTargetPosition.Value;
                this.PositionCondition = ImGuiCond.Always;
                dragTargetPosition = null;
            }
            else if (isFirstDraw || shouldResetPosition)
            {
                var viewport = ImGui.GetMainViewport();
                float x = viewport.Pos.X + viewport.Size.X - 430f;
                float y = viewport.Pos.Y + 150f;
                this.Position = new Vector2(x, y);
                this.PositionCondition = ImGuiCond.Always;

                this.Size = new Vector2(380f, 600f);
                this.SizeCondition = ImGuiCond.Always;

                shouldResetPosition = false;
                isFirstDraw = false;
            }
            else
            {
                this.PositionCondition = ImGuiCond.None;
                this.SizeCondition = ImGuiCond.None;
            }
        }

        public override void Draw()
        {
            if (plugin.GameState.ActivePhase != GamePhase.Pyramid)
            {
                return;
            }

            var drawList = ImGui.GetWindowDrawList();
            var windowPos = ImGui.GetWindowPos();
            var windowSize = ImGui.GetWindowSize();

            if (plugin.Configuration.ShowDebugBounds)
            {
                UiDebugDraw.DrawBounds(windowPos, windowPos + windowSize);
            }

            // Drag handle when unlocked
            if (!plugin.Configuration.IsLocked)
            {
                if (ImGui.IsWindowHovered())
                {
                    hoverTime += ImGui.GetIO().DeltaTime;
                    if (hoverTime >= plugin.Configuration.TooltipDelay)
                    {
                        ImGui.SetTooltip("Drag to move the Tavern Table.\nLock position in settings (/faecards).");
                    }
                    if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                    {
                        var delta = ImGui.GetIO().MouseDelta;
                        dragTargetPosition = ImGui.GetWindowPos() + delta;
                    }
                }
                else
                {
                    hoverTime = 0f;
                }
            }

            // 1. Draw Tavern Title Header
            ImGui.SetCursorPos(new Vector2(15f, 12f));
            ImGui.TextColored(new Vector4(1.0f, 0.84f, 0.0f, 1.0f), "TAVERN LOBBY (CIRCLE TABLE)");
            ImGui.SetCursorPos(new Vector2(15f, 28f));
            ImGui.Separator();

            // 2. Draw Table Center Coordinates
            Vector2 tableCenter = windowPos + new Vector2(windowSize.X * 0.5f, 150f);
            float tableRadius = 55f;

            // 3. Draw Wooden Tavern Table
            drawList.AddCircleFilled(tableCenter, tableRadius + 6f, 0xFF141F29); // Dark border
            drawList.AddCircleFilled(tableCenter, tableRadius, 0xFF2D4B63);      // Wooden Table surface (slate blue/wood)
            drawList.AddCircle(tableCenter, tableRadius - 3f, 0x33FFFFFF, 0, 1.5f); // Rim inner highlight

            // Draw a subtle tankard outline in the center of the table
            drawList.AddCircle(tableCenter, 8f, 0x44FFFFFF, 0, 1.0f);

            var players = plugin.GameState.Players;
            int pendingMatchSlot = plugin.GameState.PendingLocalMatchSlotIndex;
            bool isWaitingForLocalPlay = pendingMatchSlot != -1;

            // Find max cards remaining (for the Bus Rider warning)
            var busEligiblePlayers = players.Where(p => !p.IsDealer && p.IsEligibleForCurrentBusRide).ToList();
            int maxCards = busEligiblePlayers.Count > 0 ? busEligiblePlayers.Max(p => p.Hand.Count) : 0;
            bool hasLoser = maxCards > 0;

            // 4. Arrange players in a circle around the table
            for (int k = 0; k < players.Count; k++)
            {
                var p = players[k];
                float angle = k * (2f * MathF.PI / 8f) - MathF.PI / 2f; // Offset by -90 deg to start top-center
                Vector2 playerPos = tableCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (tableRadius + 60f);

                float avatarRadius = 20f;
                bool isTarget = isWaitingForLocalPlay && !p.IsLocal && p.Hand.Count > 0;

                // Pulsing target halo if you need to choose someone to give drinks to
                if (isTarget)
                {
                    float pulse = 0.7f + MathF.Sin((float)ImGui.GetTime() * 8f) * 0.3f;
                    drawList.AddCircle(playerPos, avatarRadius + 4f, ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 1.0f, 0.5f, pulse)), 0, 2.5f);

                    // Add invisible button overlay to support clicking the avatar directly
                    var origCursor = ImGui.GetCursorPos();
                    ImGui.SetCursorScreenPos(playerPos - new Vector2(avatarRadius + 4f, avatarRadius + 4f));
                    if (ImGui.InvisibleButton($"##tbl_avatar_btn_{p.Name}", new Vector2((avatarRadius + 4f) * 2f, (avatarRadius + 4f) * 2f)))
                    {
                        plugin.GameController.PlayLocalMatch(p.Name);
                    }
                    ImGui.SetCursorPos(origCursor);
                }

                // Draw Avatar Outer Ring
                uint outerColor = p.IsLocal ? 0xFF59A0C5 : 0xFF555555; // Gold for local player, grey for Scions
                drawList.AddCircle(playerPos, avatarRadius, outerColor, 0, p.IsLocal ? 2.5f : 1.5f);

                // Draw Avatar Fill
                uint fillCol = p.IsLocal ? 0xFF35441E : 0xFF202020; // Soft green for local, dark charcoal for Scions
                drawList.AddCircleFilled(playerPos, avatarRadius - 1f, fillCol);

                // Draw initials inside avatar
                string initials = p.IsLocal ? "U" : (p.Name.Length > 2 ? p.Name.Substring(0, 2) : p.Name);
                var textSz = ImGui.CalcTextSize(initials);
                drawList.AddText(playerPos - textSz * 0.5f, p.IsLocal ? 0xFF59A0C5 : 0xFFDDDDDD, initials);

                // 5. Draw Player Name Text (centered horizontally below the avatar)
                var nameSz = ImGui.CalcTextSize(p.Name);
                Vector2 namePos = new Vector2(playerPos.X - nameSz.X * 0.5f, playerPos.Y + 22f);
                namePos.X = Math.Clamp(namePos.X, windowPos.X + 5f, windowPos.X + windowSize.X - nameSz.X - 5f);
                namePos.Y = Math.Clamp(namePos.Y, windowPos.Y + 45f, windowPos.Y + 310f);

                uint nameColor = p.IsLocal ? 0xFF59A0C5 : 0xFFFFFFFF;
                drawList.AddText(namePos, nameColor, p.Name);

                // 6. Draw cards remaining as tiny overlapping cards centered above the avatar
                Vector2 cardPivot = playerPos - new Vector2(0f, 32f);

                if (p.Hand.Count > 0)
                {
                    float cardW = 9f;
                    float cardH = 13f;
                    float stepX = 4f;
                    float totalW = cardW + (p.Hand.Count - 1) * stepX;
                    Vector2 cardStart = cardPivot - new Vector2(totalW * 0.5f, cardH * 0.5f);

                    for (int cIdx = 0; cIdx < p.Hand.Count; cIdx++)
                    {
                        Vector2 cPos = cardStart + new Vector2(cIdx * stepX, 0f);
                        drawList.AddRectFilled(cPos, cPos + new Vector2(cardW, cardH), 0xFFFFFFFF, 1f);
                        drawList.AddRect(cPos, cPos + new Vector2(cardW, cardH), 0xFF111111, 1f, ImDrawFlags.None, 0.8f);

                        // Draw red suit dot on tiny cards for detail
                        bool isRedSuit = p.Hand[cIdx].Suit == Suit.Hearts || p.Hand[cIdx].Suit == Suit.Diamonds;
                        drawList.AddCircleFilled(cPos + new Vector2(cardW * 0.5f, cardH * 0.5f), 1f, isRedSuit ? 0xFF3F3FD9 : 0xFF111111);
                    }
                }
                else
                {
                    // Draw a green "Safe" label
                    var safeSz = ImGui.CalcTextSize("Safe");
                    drawList.AddText(cardPivot - safeSz * 0.5f, 0xFF45D945, "Safe");
                }

                // 7. Draw Beer Mug (Drinks Taken/Given)
                bool isLeft = MathF.Cos(angle) < 0f;
                Vector2 mugPos = playerPos + new Vector2(isLeft ? -37f : 25f, -10f);

                // Mug geometry
                float mugW = 12f;
                float mugH = 16f;
                drawList.AddRect(mugPos, mugPos + new Vector2(mugW, mugH), 0xFFCCCCCC, 1.5f, ImDrawFlags.None, 1.2f); // Glass body

                // Handle
                drawList.AddRect(mugPos + new Vector2(isLeft ? -3f : mugW, 4f),
                                 mugPos + new Vector2(isLeft ? 0f : mugW + 3f, 12f),
                                 0xFFCCCCCC, 1.0f, ImDrawFlags.None, 1.0f);

                // Beer fill level
                if (p.DrinksTaken > 0)
                {
                    float fillRatio = Math.Min(1.0f, p.DrinksTaken / 15.0f);
                    float fillH = (mugH - 2f) * fillRatio;
                    Vector2 fillMin = mugPos + new Vector2(1.5f, mugH - 1.5f - fillH);
                    Vector2 fillMax = mugPos + new Vector2(mugW - 1.5f, mugH - 1.5f);

                    drawList.AddRectFilled(fillMin, fillMax, 0xFF33B3FF, 0.5f);
                    drawList.AddRectFilled(new Vector2(fillMin.X, fillMin.Y - 1f), new Vector2(fillMax.X, fillMin.Y + 1f), 0xFFFFFFFF, 0.5f);
                }

                // Text stats: Drinks Given / Taken underneath the mug (split into two compact lines)
                string statText = $"G:{p.DrinksGiven}\nT:{p.DrinksTaken}";
                var statSz = ImGui.CalcTextSize(statText);
                Vector2 statPos = mugPos + new Vector2(mugW * 0.5f - statSz.X * 0.5f, mugH + 2f);
                drawList.AddText(statPos, 0xAAFFFFFF, statText);

                // 8. Draw red warning crown if they currently hold the most cards (Bus Rider candidate)
                if (hasLoser && p.IsEligibleForCurrentBusRide && p.Hand.Count == maxCards && p.Hand.Count > 0)
                {
                    float crownPulse = 0.6f + MathF.Sin((float)ImGui.GetTime() * 10f) * 0.4f;
                    uint crownCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.9f, 0.15f, 0.15f, crownPulse));

                    Vector2 crownCenter = cardPivot - new Vector2(0f, 12f);
                    Vector2 pLeft = crownCenter + new Vector2(-6f, 2f);
                    Vector2 pRight = crownCenter + new Vector2(6f, 2f);
                    Vector2 pBottomCenter = crownCenter + new Vector2(0f, 2f);

                    Vector2 tLeft = crownCenter + new Vector2(-7f, -4f);
                    Vector2 tMid = crownCenter + new Vector2(0f, -6f);
                    Vector2 tRight = crownCenter + new Vector2(7f, -4f);
                    Vector2 tMidLeft = crownCenter + new Vector2(-3.5f, 0f);
                    Vector2 tMidRight = crownCenter + new Vector2(3.5f, 0f);

                    drawList.AddTriangleFilled(pLeft, tLeft, tMidLeft, crownCol);
                    drawList.AddTriangleFilled(tMidLeft, tMid, tMidRight, crownCol);
                    drawList.AddTriangleFilled(tMidRight, tRight, pRight, crownCol);
                    drawList.AddLine(pLeft, pRight, crownCol, 1.5f);
                }

                // 9. Draw the "Give {mult}" button if local player has a match
                if (isTarget)
                {
                    int row = RulesEngine.GetRowIndex(pendingMatchSlot);
                    int mult = RulesEngine.GetRowMultiplier(row);

                    var origCursor = ImGui.GetCursorPos();
                    ImGui.SetCursorScreenPos(playerPos + new Vector2(-32f, 38f));

                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.3f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.7f, 0.4f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.1f, 0.4f, 0.2f, 1.0f));

                    if (ImGui.Button($"Give {mult}##{p.Name}", new Vector2(64f, 18f)))
                    {
                        plugin.GameController.PlayLocalMatch(p.Name);
                    }

                    ImGui.PopStyleColor(3);
                    ImGui.SetCursorPos(origCursor);
                }
            }

            // 5. Draw Action Log at the bottom
            float logY = 330f;
            ImGui.SetCursorPos(new Vector2(15f, logY));
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.9f, 1.0f), "ACTION LOG");
            ImGui.SetCursorPos(new Vector2(15f, logY + 16f));
            ImGui.Separator();

            ImGui.SetCursorPos(new Vector2(15f, logY + 24f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(6f, 6f));

            float logHeight = windowSize.Y - (logY + 36f) - 12f;
            if (logHeight < 40f) logHeight = 40f;

            ImGui.BeginChild("##tavern_action_log", new Vector2(windowSize.X - 30f, logHeight), true);

            var logEntries = plugin.GameState.ActionLog;
            for (int l = 0; l < logEntries.Count; l++)
            {
                ImGui.TextWrapped(logEntries[l]);
            }

            ImGui.SetScrollHereY(1.0f);
            ImGui.EndChild();
            ImGui.PopStyleVar();
        }
    }
}
