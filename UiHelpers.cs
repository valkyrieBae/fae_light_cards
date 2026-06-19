using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public static class UiLayout
    {
        public enum TextOutlineMode
        {
            Diagonal,
            EightWay
        }

        private static readonly Vector2[] DiagonalTextOutlineDirections =
        {
            new(-1f, -1f),
            new(1f, -1f),
            new(-1f, 1f),
            new(1f, 1f)
        };

        private static readonly Vector2[] EightWayTextOutlineDirections =
        {
            new(-1f, -1f),
            new(0f, -1f),
            new(1f, -1f),
            new(-1f, 0f),
            new(1f, 0f),
            new(-1f, 1f),
            new(0f, 1f),
            new(1f, 1f)
        };

        public static float GetCenteredX(float elementWidth)
        {
            var viewport = ImGui.GetMainViewport();
            return viewport.Pos.X + (viewport.Size.X - elementWidth) / 2f;
        }

        public static Vector2 GetCenteredPosition(Vector2 elementSize, float yRatioOrOffset, bool isYRatio = true)
        {
            var viewport = ImGui.GetMainViewport();
            float x = viewport.Pos.X + (viewport.Size.X - elementSize.X) / 2f;
            float y = isYRatio
                ? viewport.Pos.Y + viewport.Size.Y * yRatioOrOffset
                : viewport.Pos.Y + yRatioOrOffset;
            return new Vector2(x, y);
        }

        public static void DrawOutlinedText(
            ImDrawListPtr drawList,
            Vector2 pos,
            string text,
            Vector4 textColor,
            Vector4 outlineColor,
            float outlineThickness,
            TextOutlineMode outlineMode = TextOutlineMode.EightWay)
        {
            DrawOutlinedText(
                drawList,
                pos,
                text,
                ImGui.ColorConvertFloat4ToU32(textColor),
                ImGui.ColorConvertFloat4ToU32(outlineColor),
                outlineThickness,
                outlineMode);
        }

        public static void DrawOutlinedText(
            ImDrawListPtr drawList,
            Vector2 pos,
            string text,
            uint textColor,
            uint outlineColor,
            float outlineThickness,
            TextOutlineMode outlineMode = TextOutlineMode.EightWay)
        {
            if (outlineThickness > 0.1f)
            {
                var directions = outlineMode == TextOutlineMode.Diagonal
                    ? DiagonalTextOutlineDirections
                    : EightWayTextOutlineDirections;

                foreach (var direction in directions)
                {
                    drawList.AddText(pos + direction * outlineThickness, outlineColor, text);
                }
            }

            drawList.AddText(pos, textColor, text);
        }

        public static bool DrawCustomChoiceButton(
            string label,
            Vector2 size,
            ButtonTheme theme,
            float opacity,
            float scale,
            Action<Vector2, Vector2, float>? clickFeedback = null)
        {
            return DrawCustomChoiceButton(
                label,
                size,
                theme.GetFill(opacity),
                theme.GetOutline(opacity),
                theme.GetText(opacity),
                theme.GetTextOutline(opacity),
                scale,
                clickFeedback);
        }

        public static bool DrawCustomChoiceButton(string label, Vector2 size, Vector4 fillCol, Vector4 outlineCol, Vector4 textCol, Vector4 textOutlineCol, float scale)
        {
            return DrawCustomChoiceButton(label, size, fillCol, outlineCol, textCol, textOutlineCol, scale, null);
        }

        private static bool DrawCustomChoiceButton(
            string label,
            Vector2 size,
            Vector4 fillCol,
            Vector4 outlineCol,
            Vector4 textCol,
            Vector4 textOutlineCol,
            float scale,
            Action<Vector2, Vector2, float>? clickFeedback)
        {
            var startPos = ImGui.GetCursorScreenPos();

            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1f, 1f, 1f, 0.08f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1f, 1f, 1f, 0.15f));

            bool clicked = ImGui.Button($"##{label}", size);

            ImGui.PopStyleColor(3);

            if (clicked)
            {
                clickFeedback?.Invoke(startPos, size, scale);
            }

            bool pressed = ImGui.IsItemActive();
            var drawStartPos = pressed ? startPos + new Vector2(1.5f * scale, 1.5f * scale) : startPos;
            var drawList = ImGui.GetWindowDrawList();
            var drawEndPos = drawStartPos + size;

            // Fill background
            drawList.AddRectFilled(drawStartPos, drawEndPos, ImGui.ColorConvertFloat4ToU32(fillCol), 6f * scale);

            // Draw outline
            drawList.AddRect(drawStartPos, drawEndPos, ImGui.ColorConvertFloat4ToU32(outlineCol), 6f * scale, ImDrawFlags.None, 2.5f * scale);

            // Draw text centered and pixel-aligned
            var textSize = ImGui.CalcTextSize(label);
            var textPos = new Vector2(
                MathF.Round(drawStartPos.X + (size.X - textSize.X) * 0.5f),
                MathF.Round(drawStartPos.Y + (size.Y - textSize.Y) * 0.5f - 1.0f * scale)
            );

            // Draw text outline
            float thickness = MathF.Max(1.0f, MathF.Round(1.5f * scale));
            uint outlineU32 = ImGui.ColorConvertFloat4ToU32(textOutlineCol);
            uint textU32 = ImGui.ColorConvertFloat4ToU32(textCol);

            DrawOutlinedText(drawList, textPos, label, textU32, outlineU32, thickness);

            return clicked;
        }
    }

    public static class UiAnimation
    {
        public const float WinLoseAnimDuration = 2.4f;

        public static float CalculateWinLoseScale(float elapsed)
        {
            if (elapsed < 0f) return 0f;
            if (elapsed >= WinLoseAnimDuration) return 0f;

            // Phase 1: Grow (0.0s to 0.4s) -> scale from 0.0 to 1.0
            if (elapsed < 0.4f)
            {
                float progress = elapsed / 0.4f;
                return 1f - (1f - progress) * (1f - progress);
            }
            // Phase 2: Pulse (0.4s to 1.0s) -> pulse size
            if (elapsed < 1.0f)
            {
                float progress = (elapsed - 0.4f) / 0.6f;
                return 1.0f + 0.20f * MathF.Sin(progress * MathF.PI * 3f) * (1f - progress);
            }
            // Phase 3: Hold (1.0s to 2.0s) -> scale remains 1.0
            if (elapsed < 2.0f)
            {
                return 1.0f;
            }
            // Phase 4: Shrink (2.0s to 2.4s) -> scale from 1.0 to 0.0
            {
                float progress = (elapsed - 2.0f) / 0.4f;
                return (1f - progress) * (1f - progress);
            }
        }
    }

    public static class UiCardRenderer
    {
        public static void DrawCardWith3DEffects(
            ImDrawListPtr drawList,
            ImTextureID textureHandle,
            Vector2 pos,
            Vector2 size,
            float scale,
            float widthMultiplier = 1.0f,
            float opacity = 1.0f)
        {
            // Proportional rounding based on the card's actual corner-to-width ratio (14.5 / 242 = ~0.06)
            // We use 0.068f as the base ratio for a slightly rounder corner look as requested.
            float rounding = size.X * 0.068f;
            float absMult = Math.Abs(widthMultiplier);
            float currentRounding = rounding * absMult;

            uint alphaByte = (uint)Math.Clamp(opacity * 255f, 0f, 255f);
            uint imageCol = (alphaByte << 24) | 0x00FFFFFF;
            uint shadowCol = ((uint)(Math.Min(0.33f, opacity * 0.33f) * 255f) << 24) | 0x00000000;
            uint goldCol = (alphaByte << 24) | 0x0059A0C5; // warm metallic gold
            uint outerOutlineCol = ((uint)(opacity * 0.80f * 255f) << 24) | 0x00111111;
            uint innerOutlineCol = ((uint)(opacity * 0.60f * 255f) << 24) | 0x00111111;
            uint highlightCol = ((uint)(opacity * 0.40f * 255f) << 24) | 0x00FFFFFF;

            // 1. Drop Shadow (behind card)
            float shadowOffsetDist = Math.Max(4.0f, 6.0f * scale);
            Vector2 shadowOffset = new Vector2(shadowOffsetDist * 0.7f, shadowOffsetDist);
            float animW = size.X * absMult;
            float offsetX = (size.X - animW) / 2f;
            Vector2 animPos = new Vector2(pos.X + offsetX, pos.Y);

            drawList.AddRectFilled(
                animPos + shadowOffset,
                animPos + new Vector2(animW, size.Y) + shadowOffset,
                shadowCol,
                currentRounding
            );

            // 2. Card Image
            drawList.AddImage(textureHandle, animPos, animPos + new Vector2(animW, size.Y), new Vector2(0f, 0f), new Vector2(1f, 1f), imageCol);

            // 3. Triple Triad Style 3D Gold Frame (rendered on top of image)
            // We use physical screen pixels for thicknesses to avoid sub-pixel rendering issues.
            float frameThickness = MathF.Max(3.0f, 4.0f * scale);
            float frameInset = frameThickness * 0.5f; // Center the stroke so it aligns with the outer edge

            Vector2 frameMin = animPos + new Vector2(frameInset, frameInset);
            Vector2 frameMax = animPos + new Vector2(animW - frameInset, size.Y - frameInset);
            float frameRounding = MathF.Max(0f, rounding - frameInset) * absMult;

            // Warm metallic gold/bronze frame (FFXIV style: R=197, G=160, B=89 -> AABBGGRR = 0xFF59A0C5)
            drawList.AddRect(
                frameMin,
                frameMax,
                goldCol,
                frameRounding,
                ImDrawFlags.None,
                frameThickness
            );

            // Outer dark outline (thin 1px) to make the gold pop from the background
            float outerInset = 0.5f;
            Vector2 outerMin = animPos + new Vector2(outerInset, outerInset);
            Vector2 outerMax = animPos + new Vector2(animW - outerInset, size.Y - outerInset);
            float outerRounding = MathF.Max(0f, rounding - outerInset) * absMult;

            drawList.AddRect(
                outerMin,
                outerMax,
                outerOutlineCol,
                outerRounding,
                ImDrawFlags.None,
                1.0f
            );

            // Inner dark outline (thin 1px) on the inside edge of the gold frame
            float innerInset = frameThickness + 0.5f;
            Vector2 innerMin = animPos + new Vector2(innerInset, innerInset);
            Vector2 innerMax = animPos + new Vector2(innerInset, size.Y - innerInset);
            float innerRounding = MathF.Max(0f, rounding - innerInset) * absMult;

            drawList.AddRect(
                innerMin,
                innerMax,
                innerOutlineCol,
                innerRounding,
                ImDrawFlags.None,
                1.0f
            );

            // Reflective highlight (thin 1px) to give a beveled, reflective 3D inner edge
            float highlightInset = frameThickness - 0.5f;
            Vector2 highlightMin = animPos + new Vector2(highlightInset, highlightInset);
            Vector2 highlightMax = animPos + new Vector2(animW - highlightInset, size.Y - highlightInset);
            float highlightRounding = MathF.Max(0f, rounding - highlightInset) * absMult;

            drawList.AddRect(
                highlightMin,
                highlightMax,
                highlightCol,
                highlightRounding,
                ImDrawFlags.None,
                1.0f
            );
        }

        public static void DrawRotatedCard(
            ImDrawListPtr drawList,
            ImTextureID textureHandle,
            Vector2 center,
            Vector2 size,
            float scale,
            float rotation)
        {
            float w = size.X;
            float h = size.Y;

            float cos = MathF.Cos(rotation);
            float sin = MathF.Sin(rotation);

            // Half size vectors
            Vector2 dx = new Vector2(cos * (w * 0.5f), sin * (w * 0.5f));
            Vector2 dy = new Vector2(-sin * (h * 0.5f), cos * (h * 0.5f));

            // Corners (clockwise from top-left)
            Vector2 p1 = center - dx - dy;
            Vector2 p2 = center + dx - dy;
            Vector2 p3 = center + dx + dy;
            Vector2 p4 = center - dx + dy;

            // 1. Drop Shadow (offset in screen space)
            float shadowOffsetDist = Math.Max(4.0f, 6.0f * scale);
            Vector2 shadowOffset = new Vector2(shadowOffsetDist * 0.7f, shadowOffsetDist);
            drawList.AddQuadFilled(
                p1 + shadowOffset,
                p2 + shadowOffset,
                p3 + shadowOffset,
                p4 + shadowOffset,
                0x55000000
            );

            // 2. Card Image
            drawList.AddImageQuad(
                textureHandle,
                p1,
                p2,
                p3,
                p4,
                Vector2.Zero,
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                0xFFFFFFFF
            );

            // 3. Triple Triad Style 3D Gold Frame
            float frameThickness = MathF.Max(3.0f, 4.0f * scale);
            float frameWidth = w - frameThickness;
            float frameHeight = h - frameThickness;
            Vector2 fdx = new Vector2(cos * (frameWidth * 0.5f), sin * (frameWidth * 0.5f));
            Vector2 fdy = new Vector2(-sin * (frameHeight * 0.5f), cos * (frameHeight * 0.5f));

            Vector2 fp1 = center - fdx - fdy;
            Vector2 fp2 = center + fdx - fdy;
            Vector2 fp3 = center + fdx + fdy;
            Vector2 fp4 = center - fdx + fdy;

            drawList.AddQuad(fp1, fp2, fp3, fp4, 0xFF59A0C5, frameThickness);

            // 4. Outer thin dark outline
            drawList.AddQuad(p1, p2, p3, p4, 0xCC111111, 1.0f);
        }
    }
}
