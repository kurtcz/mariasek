using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static Android.Renderscripts.Sampler;

namespace Mariasek.SharedClient.GameComponents
{
    public enum AutosizeMode
    {
        Horizontal = 1,
        Vertical = 2,
        Both = Horizontal | Vertical
    }

    public class Label : GameComponent
    {
        private float? _origFontScaleFactor;
        private string _text;

        public virtual int Width { get; set; }
        public virtual int Height { get; set; }
        public virtual Color TextColor { get; set; }
        public virtual Color ScrollBarBackgroundColor { get; set; }
        public virtual float FontScaleFactor { get; set; }
        public virtual VerticalAlignment VerticalAlign { get; set; }
        public virtual HorizontalAlignment HorizontalAlign { get; set; }
        public virtual FontRenderer TextRenderer { get; set; }
        public bool UseCommonScissorRect { get; set; }
        public Tab[] Tabs { get; set; }
        public bool AutosizeText { get; set; }
        public AutosizeMode AutosizeMode { get; set; }
        public int AutosizeHorizontalMargin { get; set; }
        public int AutosizeVerticalMargin { get; set; }
        public virtual string Text
        {
            get { return _text; }
            set
            {
                _text = value;

                var lineSeparators = new[] { '\r', '\n' };
                var lines = value.Split(lineSeparators);

                BoundsRect = TextRenderer.GetBoundsRect(lines, FontScaleFactor);

                if (AutosizeText)
                {
                    var newScaleFactor = FontScaleFactor;
                    var normalBoundsRect = TextRenderer.GetBoundsRect(lines, 1);

                    if ((AutosizeMode & AutosizeMode.Horizontal) != 0 &&
                        BoundsRect.Width > 0 && BoundsRect.Width > Width - 2 * AutosizeHorizontalMargin)
                    {
                        newScaleFactor = (Width - 2 * AutosizeHorizontalMargin) / (float)normalBoundsRect.Width;
                    }
                    if ((AutosizeMode & AutosizeMode.Vertical) != 0 &&
                        BoundsRect.Height > 0 && BoundsRect.Height > Height - 2 * AutosizeVerticalMargin)
                    {
                        newScaleFactor = Math.Min(newScaleFactor, (Height - 2 * AutosizeVerticalMargin) / (float)normalBoundsRect.Height);
                    }
                    FontScaleFactor = newScaleFactor;
                    BoundsRect = TextRenderer.GetBoundsRect(lines, FontScaleFactor);
                }
            }
        }

        protected Rectangle BoundsRect;

        public Label(GameComponent parent)
            : base(parent)
        {
            VerticalAlign = VerticalAlignment.Top;
            HorizontalAlign = HorizontalAlignment.Left;
            TextColor = Color.White;
            ScrollBarBackgroundColor = Color.Transparent;
            Opacity = 1f;
            TextRenderer = Game.FontRenderers["BM2Font"];
            Text = Name;
            Tabs = null;
            FontScaleFactor = 1f;
            AutosizeText = false;
            AutosizeMode = AutosizeMode.Horizontal;
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

        private void DrawTextAtPosition(Vector2 position)
        {
            if (UseCommonScissorRect)
            {
                TextRenderer.DrawText(
                    Game.SpriteBatch,
                    Text,
                    position,
                    FontScaleFactor,
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
                    FontScaleFactor,
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

