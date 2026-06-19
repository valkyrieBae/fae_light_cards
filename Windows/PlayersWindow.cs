using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public class PlayersWindow : Window
    {
        private readonly Plugin plugin;
        private float hoverTime = 0f;
        private bool shouldResetPosition = false;
        private Vector2? dragTargetPosition = null;
        private bool isFirstDraw = true;
        private int lastLogCount = 0;

        public Vector2 ActualPosition { get; private set; } = new Vector2(1120f, 80f);
        public Vector2 ActualSize { get; private set; } = new Vector2(360f, 450f);

        public PlayersWindow(Plugin plugin) : base("Players###FaeLightCardsTavernWindow")
        {
            this.plugin = plugin;
            this.IsOpen = false; // Opened once a game mode is active.
            this.RespectCloseHotkey = false;

            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(340, 360),
                MaximumSize = new Vector2(500, 800)
            };

            this.Size = new Vector2(360, 450);
            this.SizeCondition = ImGuiCond.FirstUseEver;

            // Default position: right side of the screen
            this.Position = new Vector2(800, 200);
            this.PositionCondition = ImGuiCond.FirstUseEver;
        }

        public void ResetPosition()
        {
            shouldResetPosition = true;
        }

        public static Vector4 GetPlayerColor(string name)
        {
            string clean = name.ToLowerInvariant();
            if (clean.Contains("alphinaud") || clean.Contains("urianger"))
            {
                // Healer green
                return new Vector4(0.29f, 0.87f, 0.49f, 1.0f);
            }
            if (clean.Contains("estinien") || clean.Contains("alisaie") || clean.Contains("y'shtola"))
            {
                // DPS red
                return new Vector4(0.97f, 0.44f, 0.44f, 1.0f);
            }
            if (clean.Contains("thancred") || clean.Contains("g'raha"))
            {
                // Tank blue
                return new Vector4(0.38f, 0.65f, 0.98f, 1.0f);
            }
            // Local player (GameConstants.LocalPlayerName) or default: Gold/Amber
            return new Vector4(0.98f, 0.75f, 0.14f, 1.0f);
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

                // Position to the upper-right of the pyramid:
                // Pyramid is at X = viewport.Pos.X + 50, width ~580-700
                // So we position Players at X = viewport.Pos.X + 1120 to avoid any overlap (70px up-right shift)
                float x = viewport.Pos.X + 1120f;
                float y = viewport.Pos.Y + 80f;
                this.Position = new Vector2(x, y);
                this.PositionCondition = ImGuiCond.Always;

                this.Size = new Vector2(360f, 450f);
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
            if (plugin.GameState.ActiveMode == GameMode.Undecided ||
                (plugin.GameState.ActivePhase != GamePhase.Accumulation && plugin.GameState.ActivePhase != GamePhase.Pyramid && plugin.GameState.ActivePhase != GamePhase.TieChoice && plugin.GameState.ActivePhase != GamePhase.BusRide))
            {
                return;
            }

            var drawList = ImGui.GetWindowDrawList();
            var windowPos = ImGui.GetWindowPos();
            var windowSize = ImGui.GetWindowSize();

            ActualPosition = windowPos;
            ActualSize = windowSize;

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
                        ImGui.SetTooltip("Drag to move the player list.\nLock position in settings (/faecards).");
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

            var players = plugin.GameState.Players;
            int pendingMatchSlot = plugin.GameState.PendingLocalMatchSlotIndex;
            bool isWaitingForLocalPlay = pendingMatchSlot != -1;
            bool isWaitingForDrinkTarget = plugin.GameState.HasPendingDrinkTarget;
            string pendingDrinkGiver = plugin.GameState.PendingDrinkGiverName;
            var localPlayer = players.FirstOrDefault(p => p.IsLocal);
            bool isLocalPendingDrinkGiver = isWaitingForDrinkTarget
                                            && localPlayer != null
                                            && localPlayer.Name == pendingDrinkGiver;

            int maxCards = 0;
            var nonDealers = players.Where(p => !p.IsDealer && p.IsEligibleForCurrentBusRide).ToList();
            if (nonDealers.Count > 0)
            {
                maxCards = nonDealers.Max(p => p.Hand.Count);
            }
            bool isTieChoice = plugin.GameState.ActivePhase == GamePhase.TieChoice;
            bool isDealerMode = plugin.GameState.ActiveMode == GameMode.Dealer;
            int busRiderCandidateCount = isTieChoice
                ? nonDealers.Count(p => p.Hand.Count == maxCards)
                : 0;

            // Clear previous screen positions in PyramidWindow just in case, and populate them with the row positions
            plugin.PyramidWindow.PlayerRowScreenPositions.Clear();

            if (plugin.IsLocalDealer)
            {
                bool isAtPlayerLimit = players.Count >= GameConstants.MaxPlayers;
                bool hasUnusedNpc = GameConstants.ScionNames.Any(name => !players.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)));
                bool canAddNpc = !isAtPlayerLimit && hasUnusedNpc;

                if (!canAddNpc)
                {
                    ImGui.BeginDisabled();
                }

                if (ImGui.Button("Add NPC"))
                {
                    plugin.GameController.AddNpcPlayer();
                }

                if (!canAddNpc)
                {
                    ImGui.EndDisabled();
                }

                if (ImGui.IsItemHovered())
                {
                    if (isAtPlayerLimit)
                    {
                        ImGui.SetTooltip("Player limit reached.");
                    }
                    else if (!hasUnusedNpc)
                    {
                        ImGui.SetTooltip("No unused NPCs remain.");
                    }
                }

                ImGui.Spacing();
            }

            // Render the simple list of players inside an aligned Table with natural spacing
            if (ImGui.BeginTable("##players_simple_table", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerH, new Vector2(0f, 0f)))
            {
                ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthFixed, 115f);
                ImGui.TableSetupColumn("Cards", ImGuiTableColumnFlags.WidthFixed, 60f);
                ImGui.TableSetupColumn("Taken", ImGuiTableColumnFlags.WidthFixed, 60f);
                ImGui.TableSetupColumn("Given", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                for (int k = 0; k < players.Count; k++)
                {
                    var p = players[k];
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, 28f);

                    // Column 0: Player name
                    ImGui.TableSetColumnIndex(0);
                    Vector2 cellPos = ImGui.GetCursorScreenPos();

                    // Store screen position of the player's name cell for discard animations
                    plugin.PyramidWindow.PlayerRowScreenPositions[p.Name] = cellPos + new Vector2(50f, 10f);

                    bool isPyramidMatchTarget = isWaitingForLocalPlay && !p.IsLocal && p.Hand.Count > 0;
                    bool isDrinkTarget = isLocalPendingDrinkGiver && !p.IsDealer && p.Name != pendingDrinkGiver;
                    bool isTarget = isPyramidMatchTarget || isDrinkTarget;

                    bool isTied = isTieChoice
                                  && busRiderCandidateCount > 1
                                  && !p.IsDealer
                                  && p.IsEligibleForCurrentBusRide
                                  && p.Hand.Count == maxCards;

                    if (isTied)
                    {
                        uint highlightColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.85f, 0.65f, 0.15f, 0.25f));
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, highlightColor);
                    }

                    bool isClickable = isTarget || (isTied && isDealerMode);
                    if (isClickable)
                    {
                        var origCursor = ImGui.GetCursorPos();
                        ImGui.SetCursorPosY(origCursor.Y + 3f); // Align vertically

                        // Span selectable across all columns to highlight on hover and make row clickable
                        if (ImGui.Selectable($"##row_sel_{p.Name}", false, ImGuiSelectableFlags.SpanAllColumns, new Vector2(0f, 22f)))
                        {
                            if (isPyramidMatchTarget)
                            {
                                plugin.GameController.PlayLocalMatch(p.Name);
                            }
                            else if (isDrinkTarget)
                            {
                                plugin.GameController.GivePendingDrinkToPlayer(p.Name);
                            }
                            else if (isTied)
                            {
                                plugin.GameController.ChooseBusRider(p.Name);
                            }
                        }
                        ImGui.SetCursorPos(origCursor);
                    }

                    Vector4 pColor = GetPlayerColor(p.Name);
                    ImGui.TextColored(pColor, p.IsLocal ? GameConstants.LocalPlayerName : p.Name);

                    // Column 1: Cards remaining
                    ImGui.TableSetColumnIndex(1);
                    if (p.Hand.Count == 0)
                    {
                        ImGui.TextColored(new Vector4(0.29f, 0.87f, 0.49f, 1.0f), "Safe");
                    }
                    else
                    {
                        ImGui.Text($"{p.Hand.Count}");
                    }

                    // Column 2: Drinks Taken
                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text($"{p.DrinksTaken}");

                    // Column 3: Drinks Given
                    ImGui.TableSetColumnIndex(3);
                    ImGui.Text($"{p.DrinksGiven}");
                }
                ImGui.EndTable();
            }

            // Draw Action Log at the bottom of the window (dynamically occupies remaining height)
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.9f, 1.0f), "ACTION LOG");

            ImGui.Spacing();
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(6f, 6f));

            // Begin child window with height = 0 to naturally fill the remainder of the resizable parent window
            if (ImGui.BeginChild("##tavern_action_log", new Vector2(0f, 0f), true))
            {
                var logEntries = plugin.GameState.ActionLog;
                for (int l = 0; l < logEntries.Count; l++)
                {
                    ImGui.TextWrapped(logEntries[l]);
                }

                if (logEntries.Count != lastLogCount)
                {
                    if (logEntries.Count > lastLogCount)
                    {
                        ImGui.SetScrollHereY(1.0f);
                    }
                    lastLogCount = logEntries.Count;
                }

                ImGui.EndChild();
            }
            ImGui.PopStyleVar();
        }
    }
}
