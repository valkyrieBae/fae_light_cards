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
        private enum CardParticleSpawnKind
        {
            Trail,
            Splash
        }

        private void UpdateAndDrawParticles()
        {
            float dt = ImGui.GetIO().DeltaTime;
            var drawList = ImGui.GetForegroundDrawList();

            for (int i = plugin.AnimationManager.Particles.Count - 1; i >= 0; i--)
            {
                var p = plugin.AnimationManager.Particles[i];
                p.Life -= dt;

                if (p.Life <= 0f)
                {
                    plugin.AnimationManager.Particles.RemoveAt(i);
                    continue;
                }

                // Update position
                p.Position += p.Velocity * dt;

                // Apply particle behavior
                if (p.IsFirework)
                {
                    p.Velocity.Y += 120f * dt; // gravity
                    p.Velocity *= (1f - 0.4f * dt); // drag
                }
                else if (p.DrawOnTop)
                {
                    p.Velocity *= (1f - 4.5f * dt); // Button feedback sparks: high friction, no gravity
                }
                else
                {
                    var type = plugin.Configuration.ParticleType;
                    if (type == CardParticleType.GoldSparkles || type == CardParticleType.CardMatch)
                    {
                        p.Velocity.Y += 80f * dt; // gravity
                        p.Velocity *= (1f - 0.5f * dt); // drag
                    }
                    else if (type == CardParticleType.FireEmbers)
                    {
                        p.Velocity.Y -= 150f * dt; // float upward
                        p.Velocity.X += (float)Math.Sin(p.Life * 8f) * 40f * dt; // sine wave drift
                    }
                    else if (type == CardParticleType.NeonDigital)
                    {
                        p.Velocity *= (1f - 0.2f * dt);
                    }
                }

                p.Rotation += p.RotationSpeed * dt;

                if (!p.DrawOnTop)
                {
                    DrawParticleShape(drawList, p, GetParticleDrawColor(p));
                }

                plugin.AnimationManager.Particles[i] = p;
            }
        }
        public void DrawOnTopParticles()
        {
            var drawList = ImGui.GetForegroundDrawList();
            for (int i = 0; i < plugin.AnimationManager.Particles.Count; i++)
            {
                var p = plugin.AnimationManager.Particles[i];
                if (p.DrawOnTop && p.Life > 0f)
                {
                    DrawParticleShape(drawList, p, GetParticleDrawColor(p));
                }
            }
        }
        public static void DrawSparkle(ImDrawListPtr drawList, Vector2 center, float size, float rotation, uint color)
        {
            float cos = MathF.Cos(rotation);
            float sin = MathF.Sin(rotation);

            Vector2 top = center + new Vector2(-sin * size * 1.5f, -cos * size * 1.5f);
            Vector2 bottom = center + new Vector2(sin * size * 1.5f, cos * size * 1.5f);
            Vector2 left = center + new Vector2(-cos * size * 0.4f, sin * size * 0.4f);
            Vector2 right = center + new Vector2(cos * size * 0.4f, -sin * size * 0.4f);

            drawList.AddQuadFilled(top, right, bottom, left, color);

            Vector2 hTop = center + new Vector2(-sin * size * 0.4f, -cos * size * 0.4f);
            Vector2 hBottom = center + new Vector2(sin * size * 0.4f, cos * size * 0.4f);
            Vector2 hLeft = center + new Vector2(-cos * size * 1.5f, sin * size * 1.5f);
            Vector2 hRight = center + new Vector2(cos * size * 1.5f, -sin * size * 1.5f);

            drawList.AddQuadFilled(hTop, hRight, hBottom, hLeft, color);
        }
        private static void DrawRotatedSquare(ImDrawListPtr drawList, Vector2 center, float size, float rotation, uint color)
        {
            float cos = MathF.Cos(rotation);
            float sin = MathF.Sin(rotation);

            Vector2 dx = new Vector2(cos * size, sin * size);
            Vector2 dy = new Vector2(-sin * size, cos * size);

            Vector2 p1 = center - dx - dy;
            Vector2 p2 = center + dx - dy;
            Vector2 p3 = center + dx + dy;
            Vector2 p4 = center - dx + dy;

            drawList.AddQuadFilled(p1, p2, p3, p4, color);
        }

        private static uint GetParticleDrawColor(Particle particle)
        {
            float alpha = Math.Clamp(particle.Life / particle.MaxLife, 0f, 1f);
            return ImGui.ColorConvertFloat4ToU32(new Vector4(particle.Color.X, particle.Color.Y, particle.Color.Z, particle.Color.W * alpha));
        }

        private static void DrawParticleShape(ImDrawListPtr drawList, Particle particle, uint drawColor)
        {
            if (particle.ShapeType == 0) // Circle
            {
                drawList.AddCircleFilled(particle.Position, particle.Size, drawColor, 8);
            }
            else if (particle.ShapeType == 1) // Sparkle
            {
                DrawSparkle(drawList, particle.Position, particle.Size, particle.Rotation, drawColor);
            }
            else if (particle.ShapeType == 2) // Square
            {
                DrawRotatedSquare(drawList, particle.Position, particle.Size, particle.Rotation, drawColor);
            }
        }

        private static CardParticleType GetActiveParticleType(CardParticleType configuredType)
        {
            return configuredType == CardParticleType.CardMatch ? CardParticleType.GoldSparkles : configuredType;
        }

        private Vector2 GetRandomCardPerimeterPoint(Vector2 cardPos, float cardWidth, float cardHeight)
        {
            int edge = random.Next(4);
            float offset = random.NextSingle();

            return edge switch
            {
                0 => new Vector2(cardPos.X + offset * cardWidth, cardPos.Y),
                1 => new Vector2(cardPos.X + offset * cardWidth, cardPos.Y + cardHeight),
                2 => new Vector2(cardPos.X, cardPos.Y + offset * cardHeight),
                _ => new Vector2(cardPos.X + cardWidth, cardPos.Y + offset * cardHeight)
            };
        }

        private Vector2 GetDirectionFromCenter(Vector2 position, Vector2 center, bool useRandomFallback)
        {
            Vector2 direction = position - center;
            if (direction.LengthSquared() > 0.001f)
            {
                return Vector2.Normalize(direction);
            }

            if (!useRandomFallback)
            {
                return Vector2.Zero;
            }

            float angle = random.NextSingle() * MathF.PI * 2f;
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        private void AddParticle(Particle particle)
        {
            particle.Life = particle.MaxLife;
            plugin.AnimationManager.Particles.Add(particle);
        }

        private void ApplyCardParticleStyle(
            ref Particle particle,
            Card card,
            CardParticleType configuredType,
            CardParticleType activeType,
            CardParticleSpawnKind spawnKind,
            float scale,
            float speed = 0f)
        {
            if (configuredType == CardParticleType.CardMatch)
            {
                ApplyCardMatchParticleStyle(ref particle, card, spawnKind);
            }
            else if (activeType == CardParticleType.GoldSparkles)
            {
                ApplyGoldParticleStyle(ref particle, spawnKind);
            }
            else if (activeType == CardParticleType.FireEmbers)
            {
                ApplyFireParticleStyle(ref particle, spawnKind);
            }
            else if (activeType == CardParticleType.NeonDigital)
            {
                ApplyNeonParticleStyle(ref particle, spawnKind, scale, speed);
            }
        }

        private void ApplyCardMatchParticleStyle(ref Particle particle, Card card, CardParticleSpawnKind spawnKind)
        {
            bool isSplash = spawnKind == CardParticleSpawnKind.Splash;
            particle.ShapeType = random.Next(10) < (isSplash ? 6 : 7) ? 1 : 0;

            bool isRed = card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds;
            if (isRed)
            {
                float roll = random.NextSingle();
                if (roll < 0.5f)
                    particle.Color = new Vector4(1.0f, 0.15f, 0.15f, isSplash ? 1.0f : 0.8f + random.NextSingle() * 0.2f);
                else if (roll < 0.8f)
                    particle.Color = new Vector4(1.0f, 0.4f, 0.1f, isSplash ? 1.0f : 0.8f + random.NextSingle() * 0.2f);
                else
                    particle.Color = new Vector4(0.85f, 0.0f, 0.2f, isSplash ? 1.0f : 0.8f + random.NextSingle() * 0.2f);
            }
            else
            {
                float roll = random.NextSingle();
                if (roll < 0.35f)
                    particle.Color = new Vector4(0.2f, 0.2f, 0.2f, isSplash ? 1.0f : 0.8f + random.NextSingle() * 0.2f);
                else if (roll < 0.7f)
                    particle.Color = new Vector4(0.75f, 0.75f, 0.8f, isSplash ? 1.0f : 0.8f + random.NextSingle() * 0.2f);
                else
                    particle.Color = new Vector4(1.0f, 1.0f, 1.0f, isSplash ? 1.0f : 0.9f + random.NextSingle() * 0.1f);
            }
        }

        private void ApplyGoldParticleStyle(ref Particle particle, CardParticleSpawnKind spawnKind)
        {
            bool isSplash = spawnKind == CardParticleSpawnKind.Splash;
            particle.ShapeType = random.Next(10) < (isSplash ? 6 : 7) ? 1 : 0;
            particle.Color = new Vector4(1.0f, 0.84f, 0.0f, isSplash ? 1.0f : 0.8f + random.NextSingle() * 0.2f);
        }

        private void ApplyFireParticleStyle(ref Particle particle, CardParticleSpawnKind spawnKind)
        {
            bool isSplash = spawnKind == CardParticleSpawnKind.Splash;
            particle.ShapeType = 0;

            float colorRoll = random.NextSingle();
            if (isSplash)
            {
                if (colorRoll < 0.4f)
                    particle.Color = new Vector4(1.0f, 0.2f, 0.0f, 1.0f);
                else if (colorRoll < 0.7f)
                    particle.Color = new Vector4(1.0f, 0.5f, 0.0f, 1.0f);
                else
                    particle.Color = new Vector4(1.0f, 0.9f, 0.1f, 1.0f);
            }
            else
            {
                if (colorRoll < 0.5f)
                    particle.Color = new Vector4(1.0f, 0.27f, 0.0f, 0.9f);
                else if (colorRoll < 0.8f)
                    particle.Color = new Vector4(1.0f, 0.65f, 0.0f, 0.9f);
                else
                    particle.Color = new Vector4(1.0f, 1.0f, 0.0f, 0.9f);
            }
        }

        private void ApplyNeonParticleStyle(ref Particle particle, CardParticleSpawnKind spawnKind, float scale, float speed)
        {
            bool isSplash = spawnKind == CardParticleSpawnKind.Splash;
            particle.ShapeType = 2;

            if (isSplash && random.Next(10) < 4)
            {
                float gridAngle = random.Next(8) * MathF.PI / 4f;
                particle.Velocity = new Vector2(MathF.Cos(gridAngle), MathF.Sin(gridAngle)) * speed * scale;
            }

            if (isSplash)
            {
                particle.Color = random.Next(2) == 0
                    ? new Vector4(0.0f, 1.0f, 0.9f, 1.0f)
                    : new Vector4(0.0f, 1.0f, 0.2f, 1.0f);
                particle.Size = (11f + random.NextSingle() * 16f) * scale;
            }
            else
            {
                particle.Color = random.Next(2) == 0
                    ? new Vector4(0.0f, 1.0f, 0.8f, 0.9f)
                    : new Vector4(0.0f, 1.0f, 0.3f, 0.9f);
                particle.Size = (9f + random.NextSingle() * 12f) * scale;
            }
        }

        private void SpawnTrailParticles(Card card, Vector2 cardPos, float cardWidth, float cardHeight, float scale)
        {
            var type = plugin.Configuration.ParticleType;
            if (type == CardParticleType.None) return;

            var activeType = GetActiveParticleType(type);
            Vector2 cardCenter = cardPos + new Vector2(cardWidth / 2f, cardHeight / 2f);

            int count = random.Next(1, 4);
            for (int i = 0; i < count; i++)
            {
                Vector2 spawnPos = GetRandomCardPerimeterPoint(cardPos, cardWidth, cardHeight);

                // Add a small outward puff offset to make them look more natural and "around" the card
                Vector2 dirFromCenter = GetDirectionFromCenter(spawnPos, cardCenter, useRandomFallback: false);
                if (dirFromCenter.LengthSquared() > 0.001f)
                {
                    spawnPos += dirFromCenter * (random.NextSingle() * 12f - 4f) * scale;
                }

                var p = new Particle
                {
                    Position = spawnPos,
                    Velocity = new Vector2(random.NextSingle() * 40f - 20f, random.NextSingle() * 40f - 20f) * scale,
                    MaxLife = 0.5f + random.NextSingle() * 0.4f,
                    Size = (10f + random.NextSingle() * 14f) * scale,
                    Rotation = random.NextSingle() * MathF.PI * 2f,
                    RotationSpeed = random.NextSingle() * 4f - 2f
                };

                ApplyCardParticleStyle(ref p, card, type, activeType, CardParticleSpawnKind.Trail, scale);
                AddParticle(p);
            }
        }
        public void SpawnButtonFeedbackParticles(Vector2 center, Vector2 buttonSize, float scale)
        {
            int count = 25;
            for (int i = 0; i < count; i++)
            {
                float angle = random.NextSingle() * MathF.PI * 2f;
                float speed = (120f + random.NextSingle() * 180f) * scale;
                Vector2 velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;

                float colorChoice = random.NextSingle();
                Vector4 color = colorChoice < 0.6f
                    ? new Vector4(0f, 0.9f, 1f, 1f) // Neon Cyan
                    : new Vector4(0.8f, 0.2f, 1f, 1f); // Neon Purple

                var p = new Particle
                {
                    Position = center + new Vector2(
                        (random.NextSingle() - 0.5f) * buttonSize.X * 0.7f,
                        (random.NextSingle() - 0.5f) * buttonSize.Y * 0.7f
                    ),
                    Velocity = velocity,
                    Color = color,
                    Size = (4f + random.NextSingle() * 6f) * scale,
                    MaxLife = 0.35f + random.NextSingle() * 0.25f,
                    Rotation = random.NextSingle() * MathF.PI * 2f,
                    RotationSpeed = random.NextSingle() * 8f - 4f,
                    ShapeType = random.Next(2), // 0 = Circle, 1 = Sparkle
                    IsFirework = false,
                    DrawOnTop = true
                };
                AddParticle(p);
            }
        }
        private void SpawnSplashParticles(Card card, Vector2 cardPos, float cardWidth, float cardHeight, float scale)
        {
            var type = plugin.Configuration.ParticleType;
            if (type == CardParticleType.None) return;

            var activeType = GetActiveParticleType(type);

            int count = activeType == CardParticleType.NeonDigital ? 40 : 60;
            Vector2 cardCenter = cardPos + new Vector2(cardWidth / 2f, cardHeight / 2f);

            for (int i = 0; i < count; i++)
            {
                Vector2 spawnPos = GetRandomCardPerimeterPoint(cardPos, cardWidth, cardHeight);

                // Add a small outward/inward random offset around the perimeter
                Vector2 dirFromCenter = GetDirectionFromCenter(spawnPos, cardCenter, useRandomFallback: true);

                spawnPos += dirFromCenter * (random.NextSingle() * 15f - 5f) * scale;

                float speed = (activeType == CardParticleType.NeonDigital)
                    ? (120f + random.NextSingle() * 180f)
                    : (90f + random.NextSingle() * 240f);

                float angleOffset = random.NextSingle() * 0.5f - 0.25f;
                float cos = MathF.Cos(angleOffset);
                float sin = MathF.Sin(angleOffset);
                Vector2 velocityDir = new Vector2(dirFromCenter.X * cos - dirFromCenter.Y * sin, dirFromCenter.X * sin + dirFromCenter.Y * cos);

                Vector2 velocity = velocityDir * speed * scale;

                var p = new Particle
                {
                    Position = spawnPos,
                    Velocity = velocity,
                    MaxLife = 0.6f + random.NextSingle() * 0.6f,
                    Size = (14f + random.NextSingle() * 20f) * scale,
                    Rotation = random.NextSingle() * MathF.PI * 2f,
                    RotationSpeed = random.NextSingle() * 8f - 4f
                };

                ApplyCardParticleStyle(ref p, card, type, activeType, CardParticleSpawnKind.Splash, scale, speed);
                AddParticle(p);
            }
        }
        private Vector4 ConvertHueToRgba(float hue, float opacity)
        {
            float h = hue * 6.0f;
            int sector = (int)h;
            float f = h - sector;
            float q = 1.0f - f;
            float t = f;

            var (r, g, b) = sector switch
            {
                0 => (1.0f, t, 0.0f),
                1 => (q, 1.0f, 0.0f),
                2 => (0.0f, 1.0f, t),
                3 => (0.0f, q, 1.0f),
                4 => (t, 0.0f, 1.0f),
                _ => (1.0f, 0.0f, q)
            };

            return new Vector4(r, g, b, opacity);
        }
        public void SpawnFireworkParticle(Vector2 minBound, Vector2 maxBound, float scale)
        {
            Vector2 pos;
            Vector2 vel;
            float side = random.NextSingle();
            if (side < 0.25f) // Top edge
            {
                pos = new Vector2(minBound.X + random.NextSingle() * (maxBound.X - minBound.X), minBound.Y);
                vel = new Vector2((random.NextSingle() - 0.5f) * 100f, -(random.NextSingle() * 150f + 100f)) * scale;
            }
            else if (side < 0.50f) // Bottom edge
            {
                pos = new Vector2(minBound.X + random.NextSingle() * (maxBound.X - minBound.X), maxBound.Y);
                vel = new Vector2((random.NextSingle() - 0.5f) * 100f, (random.NextSingle() * 150f + 50f)) * scale;
            }
            else if (side < 0.75f) // Left edge
            {
                pos = new Vector2(minBound.X, minBound.Y + random.NextSingle() * (maxBound.Y - minBound.Y));
                vel = new Vector2(-(random.NextSingle() * 150f + 100f), (random.NextSingle() - 0.5f) * 100f) * scale;
            }
            else // Right edge
            {
                pos = new Vector2(maxBound.X, minBound.Y + random.NextSingle() * (maxBound.Y - minBound.Y));
                vel = new Vector2((random.NextSingle() * 150f + 100f), (random.NextSingle() - 0.5f) * 100f) * scale;
            }

            float life = 0.6f + random.NextSingle() * 0.6f;
            float hue = random.NextSingle();
            Vector4 col = ConvertHueToRgba(hue, 0.8f + 0.2f * random.NextSingle());

            plugin.AnimationManager.Particles.Add(new Particle
            {
                Position = pos,
                Velocity = vel,
                Color = col,
                Size = (4f + random.NextSingle() * 8f) * scale,
                MaxLife = life,
                Life = life,
                Rotation = random.NextSingle() * MathF.PI * 2f,
                RotationSpeed = (random.NextSingle() - 0.5f) * 8f,
                ShapeType = 1,
                IsFirework = true,
                DrawOnTop = random.Next(2) == 0
            });
        }
        public void SpawnFireworkBurst(Vector2 center, float scale)
        {
            for (int i = 0; i < 50; i++)
            {
                float angle = random.NextSingle() * MathF.PI * 2f;
                float speed = 80f + random.NextSingle() * 220f;
                Vector2 vel = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed * scale;

                float life = 0.8f + random.NextSingle() * 0.7f;
                float hue = random.NextSingle();
                Vector4 col = ConvertHueToRgba(hue, 0.9f);

                plugin.AnimationManager.Particles.Add(new Particle
                {
                    Position = center,
                    Velocity = vel,
                    Color = col,
                    Size = (6f + random.NextSingle() * 10f) * scale,
                    MaxLife = life,
                    Life = life,
                    Rotation = random.NextSingle() * MathF.PI * 2f,
                    RotationSpeed = (random.NextSingle() - 0.5f) * 6f,
                    ShapeType = 1,
                    IsFirework = true,
                    DrawOnTop = random.Next(2) == 0
                });
            }
        }
        public void SpawnMessageFireworks(Vector2 center, Vector2 textSize, float scale)
        {
            int particleCount = 150;
            float halfW = textSize.X * 0.5f;
            float halfH = textSize.Y * 0.5f;

            for (int i = 0; i < particleCount; i++)
            {
                float angle = random.NextSingle() * MathF.PI * 2f;
                float cos = MathF.Cos(angle);
                float sin = MathF.Sin(angle);

                // Calculate distance to boundary in this direction (ray-box intersection)
                float distToBoundary;
                if (MathF.Abs(cos) * halfH > MathF.Abs(sin) * halfW)
                {
                    distToBoundary = halfW / MathF.Abs(cos);
                }
                else
                {
                    distToBoundary = halfH / MathF.Abs(sin);
                }

                // Travel just beyond the boundary (with some organic variance - wider spread to ensure it covers breadth)
                float variance = 1.2f + random.NextSingle() * 0.80f; // 120% to 200%
                float targetDist = (distToBoundary * variance + 180f) * scale;

                float life = 0.6f + random.NextSingle() * 0.6f; // 0.6s to 1.2s
                // Compensate for particle drag (which reduces distance covered to ~85% of initial_velocity * life)
                float speed = (targetDist / life) * 1.3f;

                Vector2 dir = new Vector2(cos, sin);
                Vector2 vel = dir * speed;

                float hue = random.NextSingle();
                Vector4 col = ConvertHueToRgba(hue, 0.9f);

                plugin.AnimationManager.Particles.Add(new Particle
                {
                    Position = center,
                    Velocity = vel,
                    Color = col,
                    Size = (6f + random.NextSingle() * 10f) * scale,
                    MaxLife = life,
                    Life = life,
                    Rotation = random.NextSingle() * MathF.PI * 2f,
                    RotationSpeed = (random.NextSingle() - 0.5f) * 8f,
                    ShapeType = 1,
                    IsFirework = true,
                    DrawOnTop = random.Next(2) == 0
                });
            }
        }
    }
}
