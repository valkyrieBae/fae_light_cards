using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public class DeckWindow : Window
    {
        private readonly Plugin plugin;
        private bool shouldResetPosition = false;
        private Vector2? dragTargetPosition = null;
        private float hoverTime = 0f;
        private bool isFirstDraw = true;
        private bool isPositionCustom = false;
        private int pushedStyleVarCount = 0;
        private int pushedStyleColorCount = 0;

        public Vector2 ActualPosition { get; private set; }

        public DeckWindow(Plugin plugin) : base("Deck###FaeLightCardsDeckWindow")
        {
            this.plugin = plugin;
            this.IsOpen = false;

            this.RespectCloseHotkey = false;

            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(10, 10),
                MaximumSize = new Vector2(2000, 2000)
            };

            this.Position = new Vector2(200, 200);
            this.PositionCondition = ImGuiCond.FirstUseEver;
        }

        public void ResetPosition()
        {
            shouldResetPosition = true;
            isPositionCustom = false;
        }

        public override void PreDraw()
        {
            if (dragTargetPosition.HasValue)
            {
                this.Position = dragTargetPosition.Value;
                this.PositionCondition = ImGuiCond.Always;
                dragTargetPosition = null;
            }
            else if (isFirstDraw || shouldResetPosition || !isPositionCustom)
            {
                var viewport = ImGui.GetMainViewport();
                var deckSize = plugin.CardDeckService.GetDeckCardDisplaySize(plugin.Configuration.DeckScale);

                float defaultY = viewport.Pos.Y + viewport.Size.Y * 0.10f;
                float currentY = this.Position?.Y ?? defaultY;
                this.Position = new Vector2(
                    UiLayout.GetCenteredX(deckSize.X),
                    (isFirstDraw || shouldResetPosition) ? defaultY : currentY
                );
                this.PositionCondition = ImGuiCond.Always;

                if (shouldResetPosition)
                {
                    shouldResetPosition = false;
                }
                isFirstDraw = false;
            }
            else
            {
                this.PositionCondition = ImGuiCond.None;
            }

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoScrollbar
                                   | ImGuiWindowFlags.NoScrollWithMouse
                                   | ImGuiWindowFlags.NoTitleBar
                                   | ImGuiWindowFlags.NoResize;

            // Hide background and block movement when locked
            if (plugin.Configuration.IsLocked)
            {
                flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBackground;
            }

            this.Flags = flags;

            pushedStyleVarCount = 0;
            pushedStyleColorCount = 0;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            pushedStyleVarCount++;


        }

        public override void Draw()
        {
            var wrap = plugin.CardDeckService.GetCardBackTexture().GetWrapOrEmpty();
            var size = plugin.CardDeckService.GetDeckCardDisplaySize(plugin.Configuration.DeckScale);
            ActualPosition = ImGui.GetWindowPos();

            ImGui.SetWindowSize(size);

            if (plugin.Configuration.ShowDebugBounds)
            {
                var wp = ImGui.GetWindowPos();
                UiDebugDraw.DrawBounds(wp, wp + size);
            }

            // Render the deck texture
            if (wrap != null)
            {
                ImGui.Image(wrap.Handle, size);
            }
            else
            {
                ImGui.Dummy(size);
            }

            if (!plugin.Configuration.IsLocked)
            {
                if (ImGui.IsWindowHovered())
                {
                    hoverTime += ImGui.GetIO().DeltaTime;
                    if (hoverTime >= plugin.Configuration.TooltipDelay)
                    {
                        ImGui.SetTooltip("Drag to move the deck.\nLock position in settings (/faecards).");
                    }

                    if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                    {
                        var delta = ImGui.GetIO().MouseDelta;
                        dragTargetPosition = ImGui.GetWindowPos() + delta;
                        isPositionCustom = true;
                        plugin.UiState.LastMovedElementName = "Deck";
                        plugin.UiState.LastMovedElementCoords = dragTargetPosition.Value;
                    }
                }
                else
                {
                    hoverTime = 0f;
                }
            }
        }

        public override void PostDraw()
        {
            if (pushedStyleColorCount > 0)
            {
                ImGui.PopStyleColor(pushedStyleColorCount);
                pushedStyleColorCount = 0;
            }

            if (pushedStyleVarCount > 0)
            {
                ImGui.PopStyleVar(pushedStyleVarCount);
                pushedStyleVarCount = 0;
            }
        }
    }
}
