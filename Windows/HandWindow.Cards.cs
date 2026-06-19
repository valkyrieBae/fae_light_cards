using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class HandWindow
    {
        private ISharedImmediateTexture GetCardTexture(Card card)
        {
            return plugin.CardDeckService.GetCardTexture(card);
        }
        private ISharedImmediateTexture GetCardBackTexture(bool light = false)
        {
            return plugin.CardDeckService.GetCardBackTexture(light);
        }
        private Vector2 GetHandBaseCardSize()
        {
            return plugin.CardDeckService.GetHandCardDisplaySize(1.0f);
        }
        private Vector2 GetHandCardSize(float scale)
        {
            return plugin.CardDeckService.GetHandCardDisplaySize(scale);
        }
        private void DrawCard(Card? card, Vector2 screenPos, float scale, bool isFaceDown, float opacity = 1.0f)
        {
            var drawList = ImGui.GetWindowDrawList();
            IDalamudTextureWrap? wrap = null;
            if (isFaceDown)
            {
                wrap = GetCardBackTexture().GetWrapOrEmpty();
            }
            else if (card != null)
            {
                wrap = GetCardTexture(card).GetWrapOrEmpty();
            }

            // Render custom placeholder if texture is still loading asynchronously
            var cardSize = GetHandCardSize(scale);
            float cardW = cardSize.X;
            float cardH = cardSize.Y;
            if (wrap == null)
            {
                drawList.AddRectFilled(screenPos, screenPos + cardSize, ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.1f, opacity)), 4f);
                drawList.AddRect(screenPos, screenPos + cardSize, ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.3f, 0.3f, opacity)), 4f);
                return;
            }

            // Hover checks
            var mousePos = ImGui.GetMousePos();
            bool isHovered = mousePos.X >= screenPos.X && mousePos.X <= screenPos.X + cardW &&
                             mousePos.Y >= screenPos.Y && mousePos.Y <= screenPos.Y + cardH &&
                             ImGui.IsWindowHovered();

            // Check if this card matches any flipped pyramid card that the local player hasn't matched yet AND is required to match
            bool canMatch = false;
            int matchTargetIdx = -1;
            if (plugin.GameState.ActivePhase == GamePhase.Pyramid && card != null)
            {
                var localPlayerName = plugin.GameState.Players.FirstOrDefault(p => p.IsLocal)?.Name ?? GameConstants.LocalPlayerName;
                for (int i = 0; i < 15; i++)
                {
                    if (plugin.GameState.PyramidFlipped[i])
                    {
                        int requiredCount = plugin.GameState.PyramidRequiredMatchers[i].Count(name =>
                            string.Equals(name, localPlayerName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name, GameConstants.LocalPlayerName, StringComparison.OrdinalIgnoreCase));

                        int matchedCount = plugin.GameState.PyramidMatchedPlayerNamesLists[i].Count(name =>
                            string.Equals(name, localPlayerName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name, GameConstants.LocalPlayerName, StringComparison.OrdinalIgnoreCase));

                        if (matchedCount < requiredCount && plugin.GameState.Pyramid[i].Rank == card.Rank)
                        {
                            if (RulesEngine.GetRowIndex(i) == plugin.GameState.ActiveRow)
                            {
                                matchTargetIdx = i;
                                break; // Perfect match found in active row
                            }
                            else if (matchTargetIdx == -1)
                            {
                                matchTargetIdx = i; // Fallback to match in another row
                            }
                        }
                    }
                }
                if (matchTargetIdx != -1)
                {
                    canMatch = true;
                }
            }

            // Slide card up slightly when hovered, or bounce/float if it is a match
            Vector2 offset = Vector2.Zero;
            float time = (float)ImGui.GetTime();
            if (canMatch)
            {
                offset.Y -= (15f + MathF.Sin(time * 8f) * 8f) * scale;
            }
            else if (isHovered && !plugin.Configuration.IsLocked)
            {
                offset.Y -= 10f * scale;
            }

            var finalPos = screenPos + offset;

            UiCardRenderer.DrawCardWith3DEffects(drawList, wrap.Handle, finalPos, new Vector2(cardW, cardH), scale, opacity: opacity);

            ImGui.SetCursorScreenPos(finalPos);
            ImGui.Dummy(new Vector2(cardW, cardH));

            if (canMatch)
            {
                float pulse = (0.7f + MathF.Sin(time * 6f) * 0.3f) * opacity;
                float outlineThickness = MathF.Max(3.0f, 4.0f * scale);
                float outlineRounding = cardW * 0.068f;
                drawList.AddRect(
                    finalPos,
                    finalPos + new Vector2(cardW, cardH),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.84f, 0.0f, pulse)),
                    outlineRounding,
                    ImDrawFlags.None,
                    outlineThickness
                );

                // Spawn some ambient sparkles along the perimeter of the matching hand card
                if (random.NextDouble() < 0.15)
                {
                    SpawnTrailParticles(card!, finalPos, cardW, cardH, scale);
                }

                // Matches are played via the player list buttons instead of clicking the card directly.
            }
        }
        private void DrawDashedRect(Vector2 min, Vector2 max, uint color, float thickness, float dashLength, float gapLength)
        {
            float inset = Math.Max(1f, thickness * 0.5f);
            min += new Vector2(inset, inset);
            max -= new Vector2(inset, inset);

            if (max.X <= min.X || max.Y <= min.Y) return;

            // Draw top edge
            DrawDashedLine(new Vector2(min.X, min.Y), new Vector2(max.X, min.Y), color, thickness, dashLength, gapLength);
            // Draw right edge
            DrawDashedLine(new Vector2(max.X, min.Y), new Vector2(max.X, max.Y), color, thickness, dashLength, gapLength);
            // Draw bottom edge
            DrawDashedLine(new Vector2(max.X, max.Y), new Vector2(min.X, max.Y), color, thickness, dashLength, gapLength);
            // Draw left edge
            DrawDashedLine(new Vector2(min.X, max.Y), new Vector2(min.X, min.Y), color, thickness, dashLength, gapLength);
        }
        private void DrawDashedLine(Vector2 p1, Vector2 p2, uint color, float thickness, float dashLength, float gapLength)
        {
            var drawList = ImGui.GetForegroundDrawList();
            var dir = p2 - p1;
            float len = dir.Length();
            if (len < 0.0001f) return;
            dir /= len;

            float step = dashLength + gapLength;
            float current = 0f;

            while (current < len)
            {
                float end = current + dashLength;
                if (end > len) end = len;
                drawList.AddLine(p1 + dir * current, p1 + dir * end, color, thickness);
                current += step;
            }
        }
        private int GetHandIndexForVisualSlot(int visualSlot, int handCount)
        {
            if (handCount == 3)
            {
                if (visualSlot == 0) return 0;
                if (visualSlot == 1) return 2;
                if (visualSlot == 2) return 1;
            }
            else if (handCount == 4)
            {
                if (visualSlot == 0) return 0;
                if (visualSlot == 1) return 2;
                if (visualSlot == 2) return 1;
                if (visualSlot == 3) return 3;
            }
            return visualSlot;
        }
    }
}
