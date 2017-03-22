using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

using Mariasek.SharedClient.BmFont;

namespace Mariasek.SharedClient.GameComponents
{
    public class Label : GameComponent
    {
        public virtual int Width { get; set; }
        public virtual int Height { get; set; }
        public virtual string Text { get; set; }
        public virtual Color TextColor { get; set; }
        public virtual VerticalAlignment VerticalAlign { get; set; }
        public virtual HorizontalAlignment HorizontalAlign { get; set; }
        public virtual FontRenderer TextRenderer { get; set; }
        public bool UseCommonScissorRect { get; set; }
        public Tab[] Tabs { get; set; }

        protected Rectangle BoundsRect;

        public Label(GameComponent parent)
            : base(parent)
        {
            VerticalAlign = VerticalAlignment.Top;
            HorizontalAlign = HorizontalAlignment.Left;
            TextColor = Color.White;
            Opacity = 1f;
            TextRenderer = Game.FontRenderers["BM2Font"];
            Text = Name;
            Tabs = null;
        }

        public override void Draw(GameTime gameTime)
        {
            if (Anchor == Game.CurrentRenderingGroup &&
			    IsVisible)
            {
                var position = Position;

                switch (HorizontalAlign)
                {
                    case HorizontalAlignment.Center:
                        position.X += Width / 2f;
                        break;
                    case HorizontalAlignment.Right:
                        position.X += Width;
                        break;
                }
                switch (VerticalAlign)
                {
                    case VerticalAlignment.Middle:
                        position.Y += Height / 2f;
                        break;
                    case VerticalAlignment.Bottom:
                        position.Y += Height;
                        break;
                }

                DrawTextAtPosition(position);
            }

            base.Draw(gameTime);
        }

        protected void DrawTextAtPosition(Vector2 position)
        {
            if (UseCommonScissorRect)
            {
                TextRenderer.DrawText(
                    Game.SpriteBatch,
                    Text,
                    position,
                    TextColor * Opacity,
                    (Alignment)VerticalAlign | (Alignment)HorizontalAlign,
                    Tabs);
            }
            else
            {
                Game.SpriteBatch.End();
                var origClippingRectangle = Game.SpriteBatch.GraphicsDevice.ScissorRectangle;
                //we need to create a new sprite batch instance that is going to use a clipping rectangle
                Game.SpriteBatch.GraphicsDevice.ScissorRectangle = new Rectangle((int)(ScaleMatrix.M41 + Position.X * ScaleMatrix.M11),
                                                                     (int)(ScaleMatrix.M42 + Position.Y * ScaleMatrix.M22),
                                                                     (int)(Width * ScaleMatrix.M11),
                                                                     (int)(Height * ScaleMatrix.M22));

                Game.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, new RasterizerState { ScissorTestEnable = true }, null, ScaleMatrix);

                TextRenderer.DrawText(
                    Game.SpriteBatch,
                    Text,
                    position,
                    TextColor * Opacity,
                    (Alignment)VerticalAlign | (Alignment)HorizontalAlign,
                    Tabs);

                Game.SpriteBatch.End();

                Game.SpriteBatch.GraphicsDevice.ScissorRectangle = origClippingRectangle;
                Game.SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, ScaleMatrix);
            }
        }
    }
}

