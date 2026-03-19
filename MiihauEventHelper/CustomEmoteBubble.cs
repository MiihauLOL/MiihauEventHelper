using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using static StardewValley.Minigames.CraneGame;

namespace MiihauEventHelper
{
    internal sealed class CustomEmoteBubble
    {
        private const int BubbleFrameSize = 16;
        private const int BubbleScale = 4;
        private const int BuildRowY = 0;
        private const int EmoteRowY = 16;
        private const int FrameDurationMs = 50;
        private const int EmoteHoldFrames = 24; 
        private const int SequenceFrameCount = 8 + EmoteHoldFrames;
        private const int IconMaxWidth = 10;
        private const int IconMaxHeight = 9;
        private const float IconVerticalOffset = -8f;
        

        private readonly Character actor;
        private readonly Texture2D bubbleTexture;
        private readonly Texture2D iconTexture;
        private readonly Rectangle iconSourceRect;
        private readonly int YOffset;
        private int elapsedMs;

        public CustomEmoteBubble(Character actor, Texture2D bubbleTexture, Texture2D iconTexture, Rectangle iconSourceRect, int YOffset)
        {
            this.actor = actor;
            this.bubbleTexture = bubbleTexture;
            this.iconTexture = iconTexture;
            this.iconSourceRect = iconSourceRect;
            this.YOffset = YOffset;
        }

        public bool Update(GameTime gameTime)
        {
            if (this.actor == null)
                return false;

            this.elapsedMs += (int)gameTime.ElapsedGameTime.TotalMilliseconds;
            return this.elapsedMs < SequenceFrameCount * FrameDurationMs;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (this.actor == null)
                return;

            int sequenceFrame = System.Math.Min(SequenceFrameCount - 1, this.elapsedMs / FrameDurationMs);
            Rectangle bubbleSource = this.GetBubbleSource(sequenceFrame, out bool drawIcon);
            Vector2 screenPosition = this.GetBubbleScreenPosition();

            spriteBatch.Draw(
                texture: this.bubbleTexture,
                position: screenPosition,
                sourceRectangle: bubbleSource,
                color: Color.White,
                rotation: 0f,
                origin: Vector2.Zero,
                scale: BubbleScale,
                effects: SpriteEffects.None,
                layerDepth: 1f
            );

            if (!drawIcon || this.iconSourceRect.Width <= 0 || this.iconSourceRect.Height <= 0)
                return;

            float iconScale = System.Math.Min(
                (IconMaxWidth * BubbleScale) / (float)this.iconSourceRect.Width,
                (IconMaxHeight * BubbleScale) / (float)this.iconSourceRect.Height
            );

            Vector2 iconSize = new Vector2(
                this.iconSourceRect.Width * iconScale,
                this.iconSourceRect.Height * iconScale
            );

            Vector2 iconPosition = screenPosition + new Vector2(
                (BubbleFrameSize * BubbleScale - iconSize.X) / 2f,
                (BubbleFrameSize * BubbleScale - iconSize.Y) / 2f + IconVerticalOffset
            );

            spriteBatch.Draw(
                texture: this.iconTexture,
                position: iconPosition,
                sourceRectangle: this.iconSourceRect,
                color: Color.White,
                rotation: 0f,
                origin: Vector2.Zero,
                scale: iconScale,
                effects: SpriteEffects.None,
                layerDepth: 1f
            );
        }

        private Rectangle GetBubbleSource(int sequenceFrame, out bool drawIcon)
        {
            int frameX;
            int rowY;

            if (sequenceFrame <= 3)
            {
                frameX = sequenceFrame;
                rowY = BuildRowY;
                drawIcon = false;
            }
            else if (sequenceFrame < 4 + EmoteHoldFrames)
            {
                int emoteIndex = sequenceFrame - 4;
                frameX = System.Math.Min(3, emoteIndex * 4 / EmoteHoldFrames);
                rowY = EmoteRowY;
                drawIcon = true;
            }
            else
            {
                int closeFrame = sequenceFrame - (4 + EmoteHoldFrames);
                frameX = 3 - closeFrame;
                rowY = BuildRowY;
                drawIcon = false;
            }

            return new Rectangle(frameX * BubbleFrameSize, rowY, BubbleFrameSize, BubbleFrameSize);
        }

        private Vector2 GetBubbleScreenPosition()
        {
            Rectangle bounds = this.actor.GetBoundingBox();
            float x = bounds.Center.X - Game1.viewport.X - (BubbleFrameSize * BubbleScale / 2f);
            float y = bounds.Top - Game1.viewport.Y - (BubbleFrameSize * BubbleScale) - 96f + YOffset;
            return new Vector2(x, y);
        }
    }
}
