using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    internal static class UiDebugDraw
    {
        public const uint BoundsColor = 0xFFFF00FF;

        public static void DrawBounds(Vector2 min, Vector2 max, float thickness = 2f)
        {
            if (max.X <= min.X || max.Y <= min.Y)
            {
                return;
            }

            float inset = MathF.Ceiling(thickness * 0.5f);
            var insetVector = new Vector2(inset, inset);
            var drawMin = min + insetVector;
            var drawMax = max - insetVector;

            if (drawMax.X <= drawMin.X || drawMax.Y <= drawMin.Y)
            {
                return;
            }

            ImGui.GetForegroundDrawList().AddRect(
                drawMin,
                drawMax,
                BoundsColor,
                0f,
                ImDrawFlags.None,
                thickness);
        }
    }
}
