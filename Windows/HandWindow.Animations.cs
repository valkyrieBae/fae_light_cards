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
        public void ClearAnimationsAndParticles()
        {
            plugin.AnimationManager.ActiveAnimations.Clear();
            plugin.AnimationManager.ActiveDiscardAnimations.Clear();
            plugin.AnimationManager.ActiveExitAnimations.Clear();
            plugin.AnimationManager.Particles.Clear();
            plugin.AnimationManager.PendingDiscardsQueue.Clear();
            plugin.AnimationManager.DiscardQueueTimer = 0f;
        }
        public void TriggerBusRideSlideDown(Card card)
        {
            Vector2 startPos = slotPositions.TryGetValue(0, out var pos) ? pos : LastSetWindowPosition;
            plugin.AnimationManager.ActiveExitAnimations.Add(new ExitCardAnimation(card, startPos, new Vector2(0f, 400f), true));
        }
        public void TriggerBusRideSlideRight(Card card)
        {
            Vector2 startPos = slotPositions.TryGetValue(0, out var pos) ? pos : LastSetWindowPosition;
            plugin.AnimationManager.ActiveExitAnimations.Add(new ExitCardAnimation(card, startPos, new Vector2(600f, 0f), true));
        }
        public void TriggerBusRideDeal(Card card)
        {
            TriggerDealAnimation(card, 0);
        }
        public void TriggerDiscardAnimation(Card card, int handIndex, int pyramidIndex, Vector2 endPos, float endScale, float endCardW, float endCardH, string playerName, string targetPlayerName)
        {
            Vector2 startPos = slotPositions.TryGetValue(handIndex, out var pos) ? pos : (this.Position ?? Vector2.Zero);
            float startScale = plugin.Configuration.HandScale;
            float rotation = (random.NextSingle() - 0.5f) * 0.14f; // Cap max rotation down to half (from 0.28 to 0.14)
            plugin.AnimationManager.ActiveDiscardAnimations.Add(new DiscardAnimation(card, pyramidIndex, startPos, endPos, startScale, endScale, endCardW, endCardH, playerName, targetPlayerName, rotation));
        }
        public void TriggerScionDiscardAnimation(Card card, Vector2 startPos, int pyramidIndex, Vector2 endPos, float endScale, float endCardW, float endCardH, string playerName, string targetPlayerName)
        {
            float startScale = 0.05f * endScale;
            float rotation = (random.NextSingle() - 0.5f) * 0.14f; // Cap max rotation down to half (from 0.28 to 0.14)
            plugin.AnimationManager.ActiveDiscardAnimations.Add(new DiscardAnimation(card, pyramidIndex, startPos, endPos, startScale, endScale, endCardW, endCardH, playerName, targetPlayerName, rotation));
        }
        public void QueueDiscardAnimation(Card card, int handIndex, int pyramidIndex, Vector2 endPos, float endScale, float endCardW, float endCardH, string playerName, string targetPlayerName)
        {
            plugin.AnimationManager.PendingDiscardsQueue.Enqueue(new PendingDiscardInfo
            {
                Card = card,
                HandIndex = handIndex,
                PyramidIndex = pyramidIndex,
                EndPos = endPos,
                EndScale = endScale,
                EndCardW = endCardW,
                EndCardH = endCardH,
                PlayerName = playerName,
                TargetPlayerName = targetPlayerName
            });
        }
        public void QueueScionDiscardAnimation(Card card, Vector2 startPos, int pyramidIndex, Vector2 endPos, float endScale, float endCardW, float endCardH, string playerName, string targetPlayerName)
        {
            plugin.AnimationManager.PendingDiscardsQueue.Enqueue(new PendingDiscardInfo
            {
                Card = card,
                HandIndex = -1,
                PyramidIndex = pyramidIndex,
                StartPos = startPos,
                EndPos = endPos,
                EndScale = endScale,
                EndCardW = endCardW,
                EndCardH = endCardH,
                PlayerName = playerName,
                TargetPlayerName = targetPlayerName
            });
        }
        public int GetPendingDiscardCount(int pyramidIndex)
        {
            int count = 0;
            foreach (var info in plugin.AnimationManager.PendingDiscardsQueue)
            {
                if (info.PyramidIndex == pyramidIndex)
                {
                    count++;
                }
            }
            foreach (var anim in plugin.AnimationManager.ActiveDiscardAnimations)
            {
                if (anim.TargetIndex == pyramidIndex)
                {
                    count++;
                }
            }
            return count;
        }
        private void UpdateAndDrawActiveAnimations(float scale)
        {
            float deltaTime = ImGui.GetIO().DeltaTime;

            // Calculate base dimensions
            var baseCardSize = GetHandBaseCardSize();
            float baseCardW = baseCardSize.X;
            float baseCardH = baseCardSize.Y;
            float cardWidth = baseCardW * scale;
            float cardHeight = baseCardH * scale;

            float dealStartScale = plugin.Configuration.DeckScale / UIConstants.CardTextureScaleMultiplier;
            Vector2 pStart = GetDeckAnimationStartPosition(baseCardW * dealStartScale);

            // Update Deal animations
            for (int i = plugin.AnimationManager.ActiveAnimations.Count - 1; i >= 0; i--)
            {
                var anim = plugin.AnimationManager.ActiveAnimations[i];
                anim.ElapsedTime += deltaTime;

                if (anim.ElapsedTime >= anim.Duration)
                {
                    // Trigger landing splash!
                    Vector2 landPos = slotPositions.TryGetValue(anim.SlotIndex, out var lPos) ? lPos : pStart;
                    SpawnSplashParticles(anim.Card, landPos, cardWidth, cardHeight, scale);

                    plugin.AnimationManager.ActiveAnimations.RemoveAt(i);
                    continue;
                }

                // Interpolation progress (0 to 1)
                float t = Math.Clamp(anim.ElapsedTime / anim.Duration, 0f, 1f);
                Vector2 pEnd = slotPositions.TryGetValue(anim.SlotIndex, out var pos) ? pos : pStart;

                Vector2 currentPos = pStart;

                float endScale = scale; // HandScale
                float currentScale = dealStartScale;

                var type = anim.Type;

                if (type == CardAnimationType.ThreeDSpin || type == CardAnimationType.LinearSlide)
                {
                    // ease-out position translation and scale interpolation
                    float tEase = 1f - (1f - t) * (1f - t);
                    currentPos = Vector2.Lerp(pStart, pEnd, tEase);
                    currentScale = dealStartScale + (endScale - dealStartScale) * tEase;
                }
                else if (type == CardAnimationType.SwoopAndBounce)
                {
                    // Swoop & Bounce: Bezier curve path
                    Vector2 pControl = new Vector2((pStart.X + pEnd.X) / 2f, Math.Min(pStart.Y, pEnd.Y) - 200f);
                    currentPos = (1f - t) * (1f - t) * pStart + 2f * (1f - t) * t * pControl + t * t * pEnd;

                    // Spring bounce settling
                    if (t > 0.8f)
                    {
                        float springT = (t - 0.8f) / 0.2f;
                        float bounceOffset = (float)Math.Sin(springT * Math.PI * 3.5f) * 15f * (1f - springT);
                        currentPos.Y += bounceOffset;
                    }

                    // Zoom in/out scale
                    float baseScale = dealStartScale + (endScale - dealStartScale) * t;
                    currentScale = baseScale * (1f + (float)Math.Sin(t * Math.PI) * 0.3f);
                }

                // Spawn trail plugin.AnimationManager.Particles around the current card position
                SpawnTrailParticles(anim.Card, currentPos, baseCardW * currentScale, baseCardH * currentScale, currentScale);

                DrawAnimatingCard(anim.Card, currentPos, currentScale, type, anim.ElapsedTime, anim.Duration, baseCardW * currentScale, baseCardH * currentScale);
            }

            // Process queued discard animations with a delay between them
            if (plugin.AnimationManager.PendingDiscardsQueue.Count > 0)
            {
                plugin.AnimationManager.DiscardQueueTimer -= deltaTime;
                if (plugin.AnimationManager.DiscardQueueTimer <= 0f)
                {
                    var info = plugin.AnimationManager.PendingDiscardsQueue.Dequeue();
                    float rotation = (random.NextSingle() - 0.5f) * 0.14f;

                    if (info.HandIndex >= 0)
                    {
                        // Local discard
                        Vector2 startPos = slotPositions.TryGetValue(info.HandIndex, out var pos) ? pos : (this.Position ?? Vector2.Zero);
                        float startScale = plugin.Configuration.HandScale;
                        plugin.AnimationManager.ActiveDiscardAnimations.Add(new DiscardAnimation(
                            info.Card, info.PyramidIndex, startPos, info.EndPos,
                            startScale, info.EndScale, info.EndCardW, info.EndCardH,
                            info.PlayerName, info.TargetPlayerName, rotation
                        ));
                    }
                    else
                    {
                        // Scion discard
                        float startScale = 0.05f * info.EndScale;
                        plugin.AnimationManager.ActiveDiscardAnimations.Add(new DiscardAnimation(
                            info.Card, info.PyramidIndex, info.StartPos, info.EndPos,
                            startScale, info.EndScale, info.EndCardW, info.EndCardH,
                            info.PlayerName, info.TargetPlayerName, rotation
                        ));
                    }

                    plugin.EventBus.PublishPlaySound(plugin.Configuration.DrawSound);

                    // 0.4 seconds delay between consecutive matches
                    plugin.AnimationManager.DiscardQueueTimer = 0.4f;
                }
            }

            // Update Discard animations
            for (int i = plugin.AnimationManager.ActiveDiscardAnimations.Count - 1; i >= 0; i--)
            {
                var anim = plugin.AnimationManager.ActiveDiscardAnimations[i];
                anim.ElapsedTime += deltaTime;

                if (anim.ElapsedTime >= anim.Duration)
                {
                    plugin.GameState.PyramidMatchedCards[anim.TargetIndex] = anim.Card;
                    plugin.GameState.PyramidMatchedCardsLists[anim.TargetIndex].Add(anim.Card);
                    plugin.GameState.PyramidMatchedPlayerNamesLists[anim.TargetIndex].Add(anim.PlayerName);

                    // Assign the pre-calculated glide rotation to the pyramid stacked card rotations list
                    plugin.GameState.PyramidMatchedCardsRotationsLists[anim.TargetIndex].Add(anim.Rotation);

                    // Trigger plugin.AnimationManager.Particles splash at landing position
                    SpawnSplashParticles(anim.Card, anim.EndPos, anim.EndCardW, anim.EndCardH, anim.EndScale);

                    // Trigger match popup overlay
                    int row = RulesEngine.GetRowIndex(anim.TargetIndex);
                    int multiplier = RulesEngine.GetRowMultiplier(row);

                    if (anim.PlayerName == GameConstants.LocalPlayerName)
                    {
                        plugin.EventBus.PublishPlaySound(plugin.Configuration.WinSound);
                    }
                    else
                    {
                        if (anim.TargetPlayerName == GameConstants.LocalPlayerName)
                        {
                            plugin.GameCoordinator.QueueConveyorMessage($"{anim.PlayerName} gave you {multiplier} {(multiplier == 1 ? "drink" : "drinks")}!", true);
                        }
                        else
                        {
                            plugin.GameCoordinator.QueueConveyorMessage($"{anim.PlayerName} matched! {anim.TargetPlayerName} takes {multiplier} {(multiplier == 1 ? "drink" : "drinks")}!");
                        }
                    }

                    plugin.AnimationManager.ActiveDiscardAnimations.RemoveAt(i);
                    continue;
                }

                float t = Math.Clamp(anim.ElapsedTime / anim.Duration, 0f, 1f);
                float tEase = 1f - (1f - t) * (1f - t);
                Vector2 currentPos = Vector2.Lerp(anim.StartPos, anim.EndPos, tEase);
                float currentScale = anim.StartScale + (anim.EndScale - anim.StartScale) * tEase;

                // Spawn trail plugin.AnimationManager.Particles
                SpawnTrailParticles(anim.Card, currentPos, baseCardW * currentScale, baseCardH * currentScale, currentScale);

                // Draw the animating card using ForegroundDrawList, passing anim.Rotation
                DrawAnimatingCard(anim.Card, currentPos, currentScale, CardAnimationType.LinearSlide, anim.ElapsedTime, anim.Duration, baseCardW * currentScale, baseCardH * currentScale, anim.Rotation);
            }

            // Update Exit animations
            for (int i = plugin.AnimationManager.ActiveExitAnimations.Count - 1; i >= 0; i--)
            {
                var anim = plugin.AnimationManager.ActiveExitAnimations[i];
                anim.ElapsedTime += deltaTime;

                if (anim.ElapsedTime >= anim.Duration)
                {
                    plugin.AnimationManager.ActiveExitAnimations.RemoveAt(i);
                    continue;
                }

                float t = Math.Clamp(anim.ElapsedTime / anim.Duration, 0f, 1f);
                float tEase = t * t; // ease-in for exiting
                Vector2 currentPos = anim.StartPos + anim.TargetOffset * tEase;

                float opacity = anim.FadeOut ? (1f - t) : 1f;

                DrawExitingCard(anim.Card, currentPos, scale, opacity);
            }
        }
        private Vector2 GetDeckAnimationStartPosition(float cardWidth)
        {
            if (plugin.DeckWindow.ActualPosition != Vector2.Zero)
            {
                return plugin.DeckWindow.ActualPosition;
            }

            var viewport = ImGui.GetMainViewport();
            return new Vector2(
                viewport.Pos.X + (viewport.Size.X - cardWidth) * 0.5f,
                viewport.Pos.Y + viewport.Size.Y * 0.10f
            );
        }
        private void DrawAnimatingCard(Card card, Vector2 pos, float scale, CardAnimationType type, float elapsedTime, float duration, float defaultW, float defaultH, float rotation = 0f)
        {
            float t = Math.Clamp(elapsedTime / duration, 0f, 1f);
            IDalamudTextureWrap? wrap;
            var drawList = ImGui.GetForegroundDrawList();

            if (type == CardAnimationType.ThreeDSpin)
            {
                // Quadratic ease-out flip
                float tEase = 1f - (1f - t) * (1f - t);
                float widthMultiplier = (float)Math.Cos(tEase * Math.PI * 4f); // 2 full spins

                if (widthMultiplier < 0f)
                {
                    wrap = GetCardBackTexture(light: true).GetWrapOrEmpty();
                }
                else
                {
                    wrap = GetCardTexture(card).GetWrapOrEmpty();
                }

                if (wrap == null)
                {
                    // Placeholder spin
                    float animW = defaultW * Math.Abs(widthMultiplier);
                    Vector2 drawPos = pos + new Vector2((defaultW - animW) / 2f, 0f);
                    drawList.AddRectFilled(drawPos, drawPos + new Vector2(animW, defaultH), ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.1f, 1f)), 4f);
                    return;
                }

                float cardW = defaultW;
                float cardH = defaultH;
                UiCardRenderer.DrawCardWith3DEffects(drawList, wrap.Handle, pos, new Vector2(cardW, cardH), scale, widthMultiplier);
            }
            else
            {
                // Linear slide or swoop
                wrap = GetCardTexture(card).GetWrapOrEmpty();
                if (wrap == null)
                {
                    drawList.AddRectFilled(pos, pos + new Vector2(defaultW, defaultH), ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.1f, 1f)), 4f);
                    return;
                }

                float cardW = defaultW;
                float cardH = defaultH;

                if (rotation != 0f)
                {
                    // Draw centered rotated card!
                    Vector2 centerPos = pos + new Vector2(cardW * 0.5f, cardH * 0.5f);
                    UiCardRenderer.DrawRotatedCard(drawList, wrap.Handle, centerPos, new Vector2(cardW, cardH), scale, rotation);
                }
                else
                {
                    UiCardRenderer.DrawCardWith3DEffects(drawList, wrap.Handle, pos, new Vector2(cardW, cardH), scale);
                }
            }
        }
        private void DrawExitingCard(Card card, Vector2 pos, float scale, float opacity)
        {
            var drawList = ImGui.GetForegroundDrawList();
            IDalamudTextureWrap? wrap = GetCardTexture(card).GetWrapOrEmpty();
            if (wrap == null) return;

            var cardSize = GetHandCardSize(scale);
            float cardW = cardSize.X;
            float cardH = cardSize.Y;

            var tintColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, opacity));

            // Draw shadow with opacity
            float shadowOffsetDist = Math.Max(2.5f, UIConstants.CardTextureScaleMultiplier * scale);
            Vector2 shadowOffset = new Vector2(shadowOffsetDist * 0.7f, shadowOffsetDist);
            float rounding = cardW * 0.068f;
            drawList.AddRectFilled(
                pos + shadowOffset,
                pos + new Vector2(cardW, cardH) + shadowOffset,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.33f * opacity)),
                rounding
            );

            // Draw image with opacity
            drawList.AddImage(wrap.Handle, pos, pos + new Vector2(cardW, cardH), Vector2.Zero, Vector2.One, tintColor);

            // Draw gold frame with opacity
            float frameThickness = MathF.Max(3.0f, 4.0f * scale);
            float frameInset = frameThickness * 0.5f;
            Vector2 frameMin = pos + new Vector2(frameInset, frameInset);
            Vector2 frameMax = pos + new Vector2(cardW - frameInset, cardH - frameInset);
            float frameRounding = MathF.Max(0f, rounding - frameInset);
            var goldColor = ImGui.ColorConvertFloat4ToU32(new Vector4(197f / 255f, 160f / 255f, 89f / 255f, opacity));
            drawList.AddRect(frameMin, frameMax, goldColor, frameRounding, ImDrawFlags.None, frameThickness);

            // Outlines with opacity
            float outerInset = 0.5f;
            var outerColor = ImGui.ColorConvertFloat4ToU32(new Vector4(17f / 255f, 17f / 255f, 17f / 255f, 0.8f * opacity));
            drawList.AddRect(pos + new Vector2(outerInset, outerInset), pos + new Vector2(cardW - outerInset, cardH - outerInset), outerColor, rounding - outerInset, ImDrawFlags.None, 1.0f);
        }
    }
}
