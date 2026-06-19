using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace FaeLightCards
{
    public class DealAnimation
    {
        public Card Card { get; }
        public int SlotIndex { get; }
        public float ElapsedTime { get; set; }
        public float Duration { get; } = 0.8f;
        public CardAnimationType Type { get; }

        public DealAnimation(Card card, int slotIndex, CardAnimationType type)
        {
            Card = card;
            SlotIndex = slotIndex;
            ElapsedTime = 0f;
            Type = type;
        }
    }

    public class ExitCardAnimation
    {
        public Card Card { get; }
        public Vector2 StartPos { get; }
        public Vector2 TargetOffset { get; }
        public float ElapsedTime { get; set; }
        public float Duration { get; } = 0.6f;
        public bool FadeOut { get; }

        public ExitCardAnimation(Card card, Vector2 startPos, Vector2 targetOffset, bool fadeOut)
        {
            Card = card;
            StartPos = startPos;
            TargetOffset = targetOffset;
            ElapsedTime = 0f;
            FadeOut = fadeOut;
        }
    }

    public class DiscardAnimation
    {
        public Card Card { get; }
        public int TargetIndex { get; }
        public Vector2 StartPos { get; }
        public Vector2 EndPos { get; }
        public float ElapsedTime { get; set; }
        public float Duration { get; } = 0.8f;
        public float StartScale { get; }
        public float EndScale { get; }
        public float EndCardW { get; }
        public float EndCardH { get; set; }
        public string PlayerName { get; }
        public string TargetPlayerName { get; }
        public float Rotation { get; }

        public DiscardAnimation(Card card, int targetIndex, Vector2 startPos, Vector2 endPos, float startScale, float endScale, float endCardW, float endCardH, string playerName, string targetPlayerName, float rotation)
        {
            Card = card;
            TargetIndex = targetIndex;
            StartPos = startPos;
            EndPos = endPos;
            ElapsedTime = 0f;
            StartScale = startScale;
            EndScale = endScale;
            EndCardW = endCardW;
            EndCardH = endCardH;
            PlayerName = playerName;
            TargetPlayerName = targetPlayerName;
            Rotation = rotation;
        }
    }

    public class PendingDiscardInfo
    {
        public Card Card { get; set; } = null!;
        public int HandIndex { get; set; }
        public int PyramidIndex { get; set; }
        public Vector2 StartPos { get; set; }
        public Vector2 EndPos { get; set; }
        public float EndScale { get; set; }
        public float EndCardW { get; set; }
        public float EndCardH { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string TargetPlayerName { get; set; } = string.Empty;
    }

    public struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector4 Color;
        public float Size;
        public float MaxLife;
        public float Life;
        public float Rotation;
        public float RotationSpeed;
        public int ShapeType; // 0 = Circle, 1 = Sparkle, 2 = Square
        public bool IsFirework;
        public bool DrawOnTop;
    }

    public class AnimationManager
    {
        public readonly List<DealAnimation> ActiveAnimations = new();
        public readonly List<DiscardAnimation> ActiveDiscardAnimations = new();
        public readonly List<ExitCardAnimation> ActiveExitAnimations = new();
        public readonly List<Particle> Particles = new();
        public readonly Queue<PendingDiscardInfo> PendingDiscardsQueue = new();

        public float DiscardQueueTimer { get; set; } = 0f;
        private readonly Random random = new();
        private CardAnimationType lastResolvedAnimationType = (CardAnimationType)(-1);

        public bool HasActiveAnimations => ActiveAnimations.Count > 0 || ActiveDiscardAnimations.Count > 0 || ActiveExitAnimations.Count > 0 || PendingDiscardsQueue.Count > 0;
        public bool HasActiveDiscardAnimations => ActiveDiscardAnimations.Count > 0;

        public event Action<DealAnimation>? OnDealAnimationCompleted;
        public event Action<DiscardAnimation>? OnDiscardAnimationCompleted;
        public event Action<ExitCardAnimation>? OnExitAnimationCompleted;

        public void ClearAnimationsAndParticles()
        {
            ActiveAnimations.Clear();
            ActiveDiscardAnimations.Clear();
            ActiveExitAnimations.Clear();
            Particles.Clear();
            PendingDiscardsQueue.Clear();
            DiscardQueueTimer = 0f;
        }

        public void Update(float deltaTime)
        {
            // Deal animations
            for (int i = ActiveAnimations.Count - 1; i >= 0; i--)
            {
                var anim = ActiveAnimations[i];
                anim.ElapsedTime += deltaTime;
                if (anim.ElapsedTime >= anim.Duration)
                {
                    OnDealAnimationCompleted?.Invoke(anim);
                    ActiveAnimations.RemoveAt(i);
                }
            }

            // Discard animations
            for (int i = ActiveDiscardAnimations.Count - 1; i >= 0; i--)
            {
                var anim = ActiveDiscardAnimations[i];
                anim.ElapsedTime += deltaTime;
                if (anim.ElapsedTime >= anim.Duration)
                {
                    OnDiscardAnimationCompleted?.Invoke(anim);
                    ActiveDiscardAnimations.RemoveAt(i);
                }
            }

            // Exit animations
            for (int i = ActiveExitAnimations.Count - 1; i >= 0; i--)
            {
                var anim = ActiveExitAnimations[i];
                anim.ElapsedTime += deltaTime;
                if (anim.ElapsedTime >= anim.Duration)
                {
                    OnExitAnimationCompleted?.Invoke(anim);
                    ActiveExitAnimations.RemoveAt(i);
                }
            }

            // Particles
            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                var p = Particles[i];
                p.Life -= deltaTime;
                if (p.Life <= 0f)
                {
                    Particles.RemoveAt(i);
                }
                else
                {
                    p.Position += p.Velocity * deltaTime;
                    if (!p.IsFirework)
                    {
                        p.Velocity.Y += 500f * deltaTime; // Gravity
                    }
                    else
                    {
                        p.Velocity.Y += 150f * deltaTime; // Fireworks gravity
                        p.Color.W = p.Life / p.MaxLife; // Fade out
                    }
                    p.Rotation += p.RotationSpeed * deltaTime;
                    Particles[i] = p; // struct copy back
                }
            }
        }

        public void TriggerDealAnimation(Card card, int slotIndex, CardAnimationType configType)
        {
            var type = configType;
            if (type == CardAnimationType.Random)
            {
                var availableTypes = new List<CardAnimationType>
                {
                    CardAnimationType.ThreeDSpin,
                    CardAnimationType.LinearSlide,
                    CardAnimationType.SwoopAndBounce
                };

                if (lastResolvedAnimationType != (CardAnimationType)(-1))
                {
                    availableTypes.Remove(lastResolvedAnimationType);
                }

                type = availableTypes[random.Next(availableTypes.Count)];
                lastResolvedAnimationType = type;
            }
            else
            {
                lastResolvedAnimationType = (CardAnimationType)(-1);
            }
            ActiveAnimations.Add(new DealAnimation(card, slotIndex, type));
        }

        public void TriggerExitAnimation(Card card, Vector2 startPos, Vector2 targetOffset, bool fadeOut)
        {
            ActiveExitAnimations.Add(new ExitCardAnimation(card, startPos, targetOffset, fadeOut));
        }

        public void TriggerDiscardAnimation(Card card, int pyramidIndex, Vector2 startPos, Vector2 endPos, float startScale, float endScale, float endCardW, float endCardH, string playerName, string targetPlayerName)
        {
            float rotation = (random.NextSingle() - 0.5f) * 0.14f;
            ActiveDiscardAnimations.Add(new DiscardAnimation(card, pyramidIndex, startPos, endPos, startScale, endScale, endCardW, endCardH, playerName, targetPlayerName, rotation));
        }

        public void QueueDiscardAnimation(PendingDiscardInfo info)
        {
            PendingDiscardsQueue.Enqueue(info);
        }

        public int GetPendingDiscardCount(int pyramidIndex)
        {
            int count = 0;
            foreach (var info in PendingDiscardsQueue)
            {
                if (info.PyramidIndex == pyramidIndex) count++;
            }
            foreach (var anim in ActiveDiscardAnimations)
            {
                if (anim.TargetIndex == pyramidIndex) count++;
            }
            return count;
        }

        public void AddParticle(Particle p)
        {
            Particles.Add(p);
        }
    }
}
