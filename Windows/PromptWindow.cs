using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FaeLightCards
{
    public partial class PromptWindow : Window
    {
        private static readonly Vector2 PromptSize = new(300f, 120f);
        private readonly Plugin plugin;
        private bool shouldResetPosition = false;
        private Vector2? dragTargetPosition = null;
        private bool isFirstDraw = true;
        private int pushedStyleVarCount = 0;
        private Vector2? lastNormalPosition = null;
        private string roomInput = string.Empty;
        private float copiedTimer = 0f;
        private bool isDraggingPrompt = false;

        public PromptWindow(Plugin plugin) : base("Prompt###FaeLightCardsPromptWindow")
        {
            this.plugin = plugin;
            this.IsOpen = true;
            this.RespectCloseHotkey = false;
            this.Size = PromptSize;
            this.SizeCondition = ImGuiCond.Always;

            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = PromptSize,
                MaximumSize = PromptSize
            };

            this.Position = new Vector2(300, 300);
            this.PositionCondition = ImGuiCond.FirstUseEver;
        }

        public void ResetPosition()
        {
            shouldResetPosition = true;
            plugin.UiState.IsPromptPositionCustom = false;
            lastNormalPosition = null;
            plugin.UiState.PromptCenterPos = Vector2.Zero;
        }

        public Vector2 GetExpectedSize()
        {
            Vector2 expectedSize = PromptSize;
            if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Connected)
            {
                expectedSize = new Vector2(300f, 150f);
            }
            else if (plugin.AppState.ActiveConnectionMode == ConnectionMode.Networked && plugin.GameState.ActiveMode == GameMode.Undecided)
            {
                int playerCount = plugin.GameState.Players.Count;
                expectedSize = new Vector2(300f, 160f + Math.Max(0, playerCount) * 24f);
            }
            else if (plugin.AppState.ActiveConnectionMode == ConnectionMode.ConnectionFailed
                     && !string.IsNullOrWhiteSpace(plugin.AppState.ConnectionFailureMessage))
            {
                expectedSize = new Vector2(340f, 210f);
            }
            return expectedSize;
        }

        public Vector2 GetDefaultPosition()
        {
            var viewport = ImGui.GetMainViewport();
            float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
            Vector2 expectedSize = GetExpectedSize();
            float scaledWidth = expectedSize.X * globalScale;

            float x, y;
            if (plugin.GameState.ActivePhase == GamePhase.Pyramid && plugin.UiState.IsPyramidVisible)
            {
                var pyramidPos = plugin.UiState.PyramidPosition != Vector2.Zero ? plugin.UiState.PyramidPosition : new Vector2(viewport.Pos.X + 50f, viewport.Pos.Y + 150f);
                var pyramidSize = plugin.UiState.PyramidSize;

                x = pyramidPos.X + (pyramidSize.X - scaledWidth) / 2f;
                float gap = 20f * globalScale;
                y = pyramidPos.Y + pyramidSize.Y + gap;
            }
            else if (plugin.GameState.ActivePhase == GamePhase.Accumulation && plugin.UiState.IsHandVisible)
            {
                var handPos = plugin.HandWindow.LastSetWindowPosition != Vector2.Zero ? plugin.HandWindow.LastSetWindowPosition : new Vector2(viewport.Pos.X + 50f, viewport.Pos.Y + viewport.Size.Y - 200f);
                var handSize = plugin.HandWindow.LastSetWindowSize != Vector2.Zero ? plugin.HandWindow.LastSetWindowSize : new Vector2(300f, 150f);

                x = handPos.X + (handSize.X - scaledWidth) / 2f;
                float gap = 20f * globalScale;
                y = handPos.Y - (expectedSize.Y * globalScale) - gap;
            }
            else
            {
                x = viewport.Pos.X + (viewport.Size.X - scaledWidth) / 2f;

                // Position Y above the hand window (if registered) to guarantee a gap
                float handTop = plugin.HandWindow.Position?.Y ?? (viewport.Pos.Y + (viewport.Size.Y - plugin.HandWindow.LastSetWindowSize.Y) / 2f);
                float gap = 24f * globalScale;
                y = handTop - (expectedSize.Y * globalScale) - gap - 50f * globalScale;
            }

            // Ensure it does not go off-screen top
            y = Math.Max(viewport.Pos.Y + 10f * globalScale, y);

            return new Vector2(x, y);
        }

        public Vector2 CurrentCenter
        {
            get
            {
                float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
                if (plugin.UiState.IsPromptPositionCustom && plugin.UiState.PromptCenterPos != Vector2.Zero)
                {
                    return plugin.UiState.PromptCenterPos;
                }

                return GetDefaultPosition() + GetExpectedSize() * globalScale * 0.5f;
            }
        }

        public void ResetPromptState()
        {
            plugin.UiState.PromptState = UIState.PromptAnimState.Normal;
            plugin.UiState.PromptScale = 1.0f;
            plugin.UiState.ClickedButtonIndex = -1;
            plugin.UiState.PromptAnimTimer = 0f;
        }

        public void UpdatePromptTransition(float dt)
        {
            if (plugin.UiState.PromptState == UIState.PromptAnimState.Normal)
            {
                plugin.UiState.PromptScale = 1.0f;
                return;
            }

            plugin.UiState.PromptAnimTimer += dt;

            if (plugin.UiState.PromptState == UIState.PromptAnimState.ButtonClick)
            {
                if (plugin.UiState.PromptAnimTimer >= UIConstants.ButtonClickDuration)
                {
                    plugin.UiState.PromptState = UIState.PromptAnimState.Shrinking;
                    plugin.UiState.PromptAnimTimer = 0f;
                }
            }
            else if (plugin.UiState.PromptState == UIState.PromptAnimState.Shrinking)
            {
                float t = Math.Clamp(plugin.UiState.PromptAnimTimer / UIConstants.PromptShrinkDuration, 0f, 1f);
                plugin.UiState.PromptScale = 1.0f - t;

                if (plugin.UiState.PromptAnimTimer >= UIConstants.PromptShrinkDuration)
                {
                    plugin.UiState.PromptState = UIState.PromptAnimState.Hidden;
                    plugin.UiState.PromptScale = 0f;
                    plugin.UiState.PromptAnimTimer = 0f;
                }
            }
            else if (plugin.UiState.PromptState == UIState.PromptAnimState.Hidden)
            {
                plugin.UiState.PromptScale = 0f;
            }
            else if (plugin.UiState.PromptState == UIState.PromptAnimState.Growing)
            {
                float t = Math.Clamp(plugin.UiState.PromptAnimTimer / UIConstants.PromptGrowDuration, 0f, 1f);
                plugin.UiState.PromptScale = t;

                if (plugin.UiState.PromptAnimTimer >= UIConstants.PromptGrowDuration)
                {
                    plugin.UiState.PromptState = UIState.PromptAnimState.Normal;
                    plugin.UiState.PromptScale = 1.0f;
                    plugin.UiState.PromptAnimTimer = 0f;
                    plugin.UiState.ClickedButtonIndex = -1;
                }
            }
        }

        public override void PreDraw()
        {
            Vector2 expectedSize = GetExpectedSize();

            if (plugin.UiState.PromptState != UIState.PromptAnimState.Normal)
            {
                float scale = Math.Max(0.001f, plugin.UiState.PromptScale);
                Vector2 currentSize = expectedSize * scale;
                this.Size = currentSize;
                this.SizeCondition = ImGuiCond.Always;
                this.SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = currentSize,
                    MaximumSize = currentSize
                };
                float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;

                // Center around the last normal position (or CurrentCenter if not set)
                Vector2 center = lastNormalPosition.HasValue
                    ? (lastNormalPosition.Value + expectedSize * globalScale * 0.5f)
                    : CurrentCenter;

                this.Position = center - (expectedSize * scale * globalScale) * 0.5f;
                this.PositionCondition = ImGuiCond.Always;
            }
            else
            {
                this.Size = expectedSize;
                this.SizeCondition = ImGuiCond.Always;
                this.SizeConstraints = new WindowSizeConstraints
                {
                    MinimumSize = expectedSize,
                    MaximumSize = expectedSize
                };

                if (dragTargetPosition.HasValue)
                {
                    this.Position = dragTargetPosition.Value;
                    this.PositionCondition = ImGuiCond.Always;
                    lastNormalPosition = dragTargetPosition.Value;
                    dragTargetPosition = null;
                }
                else if (lastNormalPosition.HasValue)
                {
                    // Restore the pre-animation position
                    this.Position = lastNormalPosition.Value;
                    this.PositionCondition = ImGuiCond.Always;
                    lastNormalPosition = null;
                }
                else if (isFirstDraw || shouldResetPosition)
                {
                    this.Position = GetDefaultPosition();
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
            }

            ImGuiWindowFlags flags = ImGuiWindowFlags.NoScrollbar
                                   | ImGuiWindowFlags.NoScrollWithMouse
                                   | ImGuiWindowFlags.NoTitleBar
                                   | ImGuiWindowFlags.NoBackground
                                   | ImGuiWindowFlags.NoResize;

            if (plugin.Configuration.IsLocked)
            {
                flags |= ImGuiWindowFlags.NoMove;
            }

            this.Flags = flags;

            pushedStyleVarCount = 0;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
            pushedStyleVarCount++;
        }

        public override void Draw()
        {
            if (plugin.UiState.PromptScale <= 0.01f)
            {
                return;
            }

            TickCopiedTimer();

            Vector2 expectedSize = GetExpectedSize();
            float globalScale = Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
            float scale = plugin.UiState.PromptScale * globalScale;
            Vector2 expectedWindowSize = expectedSize * scale;

            SaveCurrentPromptPosition(expectedWindowSize);

            var renderModel = BuildRenderModel();
            if (renderModel.ScreenKind == PromptScreenKind.Hidden)
            {
                return;
            }

            using (plugin.MediumFont.Push())
            {
                ImGui.SetWindowFontScale(plugin.UiState.PromptScale);

                if (plugin.Configuration.ShowDebugBounds)
                {
                    var wp = ImGui.GetWindowPos();
                    UiDebugDraw.DrawBounds(wp, wp + expectedWindowSize);
                }

                RenderPromptScreen(renderModel, scale, expectedWindowSize);
            }

            UpdatePromptDragState();
        }

        public override void PostDraw()
        {
            if (pushedStyleVarCount > 0)
            {
                ImGui.PopStyleVar(pushedStyleVarCount);
                pushedStyleVarCount = 0;
            }
        }

        private void TickCopiedTimer()
        {
            if (copiedTimer > 0f)
            {
                copiedTimer -= ImGui.GetIO().DeltaTime;
            }
        }

        private void SaveCurrentPromptPosition(Vector2 expectedWindowSize)
        {
            if (plugin.UiState.PromptState != UIState.PromptAnimState.Normal)
            {
                return;
            }

            plugin.UiState.PromptCenterPos = ImGui.GetWindowPos() + expectedWindowSize * 0.5f;
            lastNormalPosition = ImGui.GetWindowPos();
        }
    }
}
