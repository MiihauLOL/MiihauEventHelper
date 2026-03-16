using Microsoft.Xna.Framework;
using StardewValley;
using System;

namespace MiihauEventHelper
{

    internal class FireflyEffect
    {
        private readonly float baseRadius;
        private readonly float amplitude;
        private readonly float pulseSpeed;
        private readonly Color color;
        private readonly int durationMs;
        private readonly float movementSpeed;
        private readonly Random random = new();

        private LightSource light;
        private TemporaryAnimatedSprite sprite;
        private GameLocation location;
        private float x;
        private float y;
        private int startTimeMs;
        private readonly int fadeInDurationMs;

        private bool isFadingOut;
        private int fadeOutDurationMs;
        private int fadeOutStartTimeMs;

        private readonly string lightId;
        public string Id { get; }

        private float velocityX;
        private float velocityY;
        private float targetVelocityX;
        private float targetVelocityY;
        private float directionChangeTimer;
        

        public FireflyEffect(
            string modUniqueId,
            float xPix,
            float yPix,
            int durationMs,
            float baseRadius,
            float amplitude,
            float pulseSpeed,
            Color color,
            float movementSpeed,
            int fadeInDurationMs = 0,
            string? customId = null)
        {
            this.x = xPix;
            this.y = yPix;
            this.durationMs = durationMs;
            this.baseRadius = baseRadius;
            this.amplitude = amplitude;
            this.pulseSpeed = pulseSpeed;
            this.color = color;
            this.movementSpeed = movementSpeed;
            this.fadeInDurationMs = Math.Max(0, fadeInDurationMs);

            this.Id = string.IsNullOrWhiteSpace(customId)
                ? Guid.NewGuid().ToString("N")
                : customId;

            this.lightId = $"{modUniqueId}/firefly/{this.Id}";
        }

        public void Initialize(GameLocation loc)
        {
            this.location = loc;
            this.startTimeMs = Environment.TickCount;

            Vector2 pos = new Vector2(this.x, this.y);

            float initialFade = this.fadeInDurationMs > 0 ? 0f : 1f;

            this.light = new LightSource(this.lightId, 4, pos, this.baseRadius * initialFade);
            this.light.color.Value = this.color;
            Game1.currentLightSources[this.lightId] = this.light;

            this.sprite = new TemporaryAnimatedSprite(
                textureName: "LooseSprites\\Cursors",
                sourceRect: new Rectangle(427, 659, 3, 3),
                animationInterval: 800f,
                animationLength: 3,
                numberOfLoops: 999999,
                position: new Vector2(this.x, this.y),
                flicker: false,
                flipped: false
            )
            {
                scale = 1f,
                scaleChange = 0f,
                rotation = 0f,
                rotationChange = 0f,
                color = this.color,
                alphaFade = 0f,
                layerDepth = 1f,
                alpha = initialFade
            };

            this.location.TemporarySprites.Add(this.sprite);
        }

        public bool Update(GameTime time)
        {
            int nowMs = Environment.TickCount;
            int elapsedMs = nowMs - this.startTimeMs;

            if (!this.isFadingOut && this.durationMs > 0 && elapsedMs >= this.durationMs)
                return false;

            float fadeInFactor = 1f;
            if (this.fadeInDurationMs > 0)
                fadeInFactor = Math.Min(1f, elapsedMs / (float)this.fadeInDurationMs);

            float fadeOutFactor = 1f;
            if (this.isFadingOut)
            {
                int fadeOutElapsedMs = nowMs - this.fadeOutStartTimeMs;

                if (fadeOutElapsedMs >= this.fadeOutDurationMs)
                    return false;

                fadeOutFactor = 1f - (fadeOutElapsedMs / (float)this.fadeOutDurationMs);
            }

            float visibility = fadeInFactor * fadeOutFactor;

            float t = elapsedMs / 1000f;
            float pulseFactor = 1f + this.amplitude * (float)Math.Sin(2f * Math.PI * this.pulseSpeed * t);

            this.light.radius.Value = this.baseRadius * pulseFactor * visibility;
            this.sprite.alpha = visibility;

            float dt = (float)time.ElapsedGameTime.TotalSeconds;

            this.directionChangeTimer -= dt;
            if (this.directionChangeTimer <= 0f)
            {
                this.directionChangeTimer = 0.5f + (float)this.random.NextDouble() * 1.0f;
                this.targetVelocityX = ((float)this.random.NextDouble() - 0.5f) * this.movementSpeed;
                this.targetVelocityY = ((float)this.random.NextDouble() - 0.5f) * this.movementSpeed;
            }

            float smoothing = 2.5f;
            this.velocityX = MathHelper.Lerp(this.velocityX, this.targetVelocityX, smoothing * dt);
            this.velocityY = MathHelper.Lerp(this.velocityY, this.targetVelocityY, smoothing * dt);

            this.x += this.velocityX * dt;
            this.y += this.velocityY * dt;

            Vector2 pos = new Vector2(this.x, this.y);
            this.light.position.Value = pos;
            this.sprite.position = pos;

            return true;
        }
        public void Remove(int fadeOutDurationMs = 0)
        {
            if (this.location == null)
                return;

            if (fadeOutDurationMs <= 0)
            {
                this.RemoveNow();
                return;
            }

            if (this.isFadingOut)
                return;

            this.isFadingOut = true;
            this.fadeOutDurationMs = fadeOutDurationMs;
            this.fadeOutStartTimeMs = Environment.TickCount;
        }

        private void RemoveNow()
        {
            if (this.location != null)
            {
                this.location.TemporarySprites.Remove(this.sprite);
                Game1.currentLightSources.Remove(this.lightId);
            }
        }
    }
}