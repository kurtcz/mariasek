﻿using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
        private readonly char[] _lineSeparators = new[] { '\r', '\n' };
        private float _origFontScaleFactor;
        private float _currFontScaleFactor;
        private string _text;

        private int _width;
        public virtual int Width
        {
            get { return _width; }
            set
            {
                _width = value;
                AmendFontScaleFactor();
            }
        }
        private int _height;
        public virtual int Height
        {
            get { return _height; }
            set
            {
                _height = value;
                AmendFontScaleFactor();
            }
        }
        public virtual Color TextColor { get; set; }
        public virtual Color ScrollBarBackgroundColor { get; set; }
        public virtual float FontScaleFactor
        {
            get { return _currFontScaleFactor; }
            set
            {
                _origFontScaleFactor = value;
                _currFontScaleFactor = value;
            }
        }
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
                AmendFontScaleFactor();
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

        private void AmendFontScaleFactor()
        {
            var lines = _text.Split(_lineSeparators);

            if (AutosizeText)
            {
                var newScaleFactor = _origFontScaleFactor;
                var normalBoundsRect = TextRenderer.GetBoundsRect(lines, 1);

                if ((AutosizeMode & AutosizeMode.Horizontal) != 0 &&
                    normalBoundsRect.Width > 0 && normalBoundsRect.Width > Width - 2 * AutosizeHorizontalMargin)
                {
                    newScaleFactor = (Width - 2 * AutosizeHorizontalMargin) / (float)normalBoundsRect.Width;
                }
                if ((AutosizeMode & AutosizeMode.Vertical) != 0 &&
                    normalBoundsRect.Height > 0 && normalBoundsRect.Height > Height - 2 * AutosizeVerticalMargin)
                {
                    newScaleFactor = Math.Min(newScaleFactor, (Height - 2 * AutosizeVerticalMargin) / (float)normalBoundsRect.Height);
                }
                _currFontScaleFactor = newScaleFactor;
            }
            BoundsRect = TextRenderer.GetBoundsRect(lines, FontScaleFactor);
        }
    }
}

