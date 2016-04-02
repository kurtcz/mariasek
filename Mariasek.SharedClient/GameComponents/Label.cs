using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
//using Microsoft.Xna.Framework.GamerServices;
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
        public virtual float Opacity { get; set; }
        public virtual VerticalAlignment VerticalAlign { get; set; }
        public virtual HorizontalAlignment HorizontalAlign { get; set; }
        public virtual FontRenderer TextRenderer { get; set; }

        protected Rectangle BoundsRect;

        public Label(GameComponent parent)
            : base(parent)
        {
            VerticalAlign = VerticalAlignment.Top;
            HorizontalAlign = HorizontalAlignment.Left;
            TextColor = Color.White;
            Opacity = 1f;
            TextRenderer = Game.FontRenderers["BM2Font"];
            //TextRenderer = Game.FontRenderers["LuckiestGuy32Outl"];
            Text = Name;
        }

        public override void Draw(GameTime gameTime)
        {
            if (IsVisible)
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
            //System.Diagnostics.Debug.WriteLine(string.Format("{0} {1} {2} {3}", Text, position.Y, defaultVerticalScrollOffset, VerticalScrollOffset));
            Game.SpriteBatch.End();
            var origClippingRectangle = Game.GraphicsDevice.ScissorRectangle;
            //we need to create a new sprite batch instance that is going to use a clipping rectangle
            Game.GraphicsDevice.ScissorRectangle = new Rectangle((int)(Position.X*Game.ScaleMatrix.M11), (int)(Position.Y*Game.ScaleMatrix.M22), (int)(Width*Game.ScaleMatrix.M11), (int)(Height*Game.ScaleMatrix.M22));

            Game.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, new RasterizerState { ScissorTestEnable = true }, null, Game.ScaleMatrix);

            TextRenderer.DrawText(
                Game.SpriteBatch, 
                Text, 
                position,
                TextColor * Opacity, 
                (Alignment)VerticalAlign | (Alignment)HorizontalAlign);                    

            Game.SpriteBatch.End();

            Game.GraphicsDevice.ScissorRectangle = origClippingRectangle;
            Game.SpriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, null, Game.ScaleMatrix);
        }
    }
}

