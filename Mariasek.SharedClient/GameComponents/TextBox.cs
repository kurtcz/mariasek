using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace Mariasek.SharedClient.GameComponents
{
    public class TextBox : Label
    {
        private TouchLocation _touchDownLocation;
        private TouchLocation _touchHeldLocation;
        private TouchLocation _previoustouchHeldLocation;
        private RectangleShape _backgroundShape;
        private RectangleShape _highlightShape;
        private ClickableArea _clickableArea;

        private Texture2D _scrollBarTexture, _scrollBarBgTexture;
        private int _scrollBarWidth = 5;
        private int _scrollBarHeight;
        private Vector2 _scrollBarPosition;

        public int HighlightedLine { get; set; }

        public override Vector2 Position
        {
            get { return base.Position; }
            set 
            {
                base.Position = value;
                _clickableArea.Position = value;
                _backgroundShape.Position = value; 
            }
        }

        public override int Width 
        { 
            get { return base.Width; }
            set 
            {
                base.Width = value;
                _clickableArea.Width = value;
                _backgroundShape.Width = value; 
            } 
        }

        public override int Height
        { 
            get { return base.Height; }
            set 
            { 
                base.Height = value;
                _clickableArea.Height = value;
                _backgroundShape.Height = value;
                UpdateVerticalScrollbar();
            } 
        }

        public override AnchorType Anchor
        {
            get { return base.Anchor; }
            set
            {
                base.Anchor = value;
                _backgroundShape.Anchor = value;
                _highlightShape.Anchor = value;
            }
        }

        public override string Text
        {
            get { return base.Text; }
            set
            {
                base.Text = value;

                var lineSeparators = new [] { '\r', '\n' };
                var lines = value.Split(lineSeparators);

                BoundsRect = TextRenderer.GetBoundsRect(lines, FontScaleFactor);

                //shift the bounding rectangle to the right offset
                if (VerticalAlign == VerticalAlignment.Middle)
                {
                    BoundsRect.Offset((int)Position.X, (int)Position.Y - BoundsRect.Height / 2);
                }
                else if (VerticalAlign == VerticalAlignment.Bottom)
                {
                    BoundsRect.Offset((int)Position.X, (int)Position.Y - BoundsRect.Height);
                }
                UpdateVerticalScrollbar();
            }
        }

        public bool ShowVerticalScrollbar { get; set; }

        public Color BackgroundColor
        { 
            get { return _backgroundShape.BackgroundColors[0]; }
            set
            {
                _backgroundShape.BackgroundColors[0] = value;
                _backgroundShape.UpdateTexture();
            } 
        }

        public Color BorderColor
        { 
            get { return _backgroundShape.BorderColors[0]; }
            set 
            { 
                _backgroundShape.BorderColors[0] = value; 
                _backgroundShape.UpdateTexture();
            } 
        }

        public Color HighlightColor
        {
            get { return _highlightShape.BackgroundColors[0]; }
            set
            {
                _highlightShape.BackgroundColors[0] = value;
                _highlightShape.UpdateTexture();
            }
        }

        public bool TapToHighlight { get; set; }

        public override float Opacity
        {
            get { return base.Opacity; }
            set
            { 
                base.Opacity = value;
                if (_backgroundShape != null)
                {
                    _backgroundShape.Opacity = value;
                } 
            }
        }

        public override VerticalAlignment VerticalAlign
        { 
            get { return base.VerticalAlign; }
            set
            {
                base.VerticalAlign = value;

                UpdateVerticalScrollbar();
            }
        }

        public int VerticalScrollOffset
        { 
            get { return _verticalScrollOffset; }
            private set
            {
                if (_verticalScrollOffset != value)
                {
                    _verticalScrollOffset = value;
                    if (_verticalScrollOffset < _minVerticalScrollOffset)
                    {
                        _verticalScrollOffset = _minVerticalScrollOffset;
                    }
                    else if (_verticalScrollOffset > _maxVerticalScrollOffset)
                    {
                        _verticalScrollOffset = _maxVerticalScrollOffset;
                    }
                    UpdateVerticalScrollbarPosition();
                }
            }
        }

        private int _verticalScrollOffset;
        private int _minVerticalScrollOffset;
        private int _maxVerticalScrollOffset;
        private bool _textureUpdateNeeded;

        public TextBox(GameComponent parent)
            : base(parent)
        {
            _backgroundShape = new RectangleShape(this)
            {
                BackgroundColors = new List<Color> { Color.Navy },
                BorderColors = new List<Color> { Color.White },
                BorderRadius = 1,
                BorderThickness = 3,
                Opacity = 0
            };
            _highlightShape = new RectangleShape(this)
            {
                BackgroundColors = new List<Color> { Color.Transparent },
                BorderColors = new List<Color> { Color.Transparent },
                BorderRadius = 1,
                BorderThickness = 3,
                Opacity = 0
            };
            _clickableArea = new ClickableArea(this)
            {
                    Position = _backgroundShape.Position,
                    Width = _backgroundShape.Width,
                    Height = _backgroundShape.Height
            };
            _clickableArea.TouchDown += HandleTouchDown;
            _clickableArea.TouchUp += HandleTouchUp;
            _clickableArea.TouchHeld += HandleTouchHeld;
            VerticalAlign = VerticalAlignment.Middle;
            HorizontalAlign = HorizontalAlignment.Center;
            Width = _backgroundShape.Width;
            Height = _backgroundShape.Height;
            HighlightedLine = -1;
            ShowVerticalScrollbar = true;
            Game.Activated += (sender, e) => ScheduleTextureUpdate();
        }

        void ScheduleTextureUpdate()
        {
            _textureUpdateNeeded = true;
        }

        /// <summary>
        /// Called when Text, Width or Height changes
        /// </summary>
        void UpdateVerticalScrollbar()
        {
			if (Height >= BoundsRect.Height)
			{
				_minVerticalScrollOffset = 0;
				_maxVerticalScrollOffset = 0;
			}
			else
			{
				switch (VerticalAlign)
				{
					case VerticalAlignment.Top:
						_minVerticalScrollOffset = Height - BoundsRect.Height;
						_maxVerticalScrollOffset = 0;
						break;
					case VerticalAlignment.Middle:
						_minVerticalScrollOffset = (Height - BoundsRect.Height) / 2;
						_maxVerticalScrollOffset = (BoundsRect.Height - Height) / 2;
						break;
					case VerticalAlignment.Bottom:
						_minVerticalScrollOffset = 0;
						_maxVerticalScrollOffset = BoundsRect.Height - Height;
						break;
				}
			}
			VerticalScrollOffset = 0;
            UpdateVerticalScrollbarPosition();
        }

        /// <summary>
        /// Called from VerticalScrollOffset setter and UpdateScrollbar()
        /// </summary>
        void UpdateVerticalScrollbarPosition()
        {
            if (BoundsRect.Height > Height)
            {
                //create a scrollbar texture
                var scrollBarHeight = Height * Height / BoundsRect.Height;

                if (scrollBarHeight != _scrollBarHeight)
                {
                    _scrollBarHeight = scrollBarHeight;
                    _scrollBarBgTexture = new Texture2D(Game.GraphicsDevice, _scrollBarWidth, Height, false, SurfaceFormat.Color);
                    _scrollBarTexture = new Texture2D(Game.GraphicsDevice, _scrollBarWidth, _scrollBarHeight, false, SurfaceFormat.Color);
                    Color[] color = new Color[_scrollBarWidth * _scrollBarHeight];
                    Color[] bgcolor = new Color[_scrollBarWidth * Height];
                    for (var i = 0; i < _scrollBarWidth; i++)
                    {
                        for (var j = 0; j < _scrollBarHeight; j++)
                        {
                            color[j * _scrollBarWidth + i] = Color.White;
                        }
                        for (var j = 0; j < Height; j++)
                        {
                            bgcolor[j * _scrollBarWidth + i] = Color.White;
                        }
                    }
                    _scrollBarTexture.SetData<Color>(color);
                    _scrollBarBgTexture.SetData<Color>(bgcolor);
                }
                _scrollBarPosition.X = Position.X + Width - _scrollBarWidth;
                _scrollBarPosition.Y = Position.Y - VerticalScrollOffset * (Height - _scrollBarHeight) / (BoundsRect.Height - Height);
				switch (VerticalAlign)
				{
					case VerticalAlignment.Middle:
						_scrollBarPosition.Y += (Height - _scrollBarHeight) / 2;
						break;
					case VerticalAlignment.Bottom:
						_scrollBarPosition.Y += (Height - _scrollBarHeight);
						break;
				}
            }
            else
            {
                if (_scrollBarTexture != null)
                {
                    if (!_scrollBarTexture.IsDisposed)
                    {
                        _scrollBarTexture.Dispose();
                    }
                    _scrollBarTexture = null;
                }
                if (_scrollBarBgTexture != null)
                {
                    if (!_scrollBarBgTexture.IsDisposed)
                    {
                        _scrollBarBgTexture.Dispose();
                    }
                    _scrollBarBgTexture = null;
                }
            }
        }

		public void ScrollToBottom()
		{
			VerticalScrollOffset = _minVerticalScrollOffset;
		}

        void HandleTouchDown (object sender, TouchLocation tl)
        {
            _touchDownLocation = tl;
            _touchHeldLocation = tl;
            if (TapToHighlight)
            {
                var highlightedLine = PositionToLineNumber(tl.Position);
                if (HighlightedLine == highlightedLine)
                {
                    highlightedLine = -1;
                }
                HighlightedLine = highlightedLine;
            }
            //System.Diagnostics.Debug.WriteLine("Down: {0} BR: {1} VO: {2}", tl.Position, BoundsRect, VerticalScrollOffset);
        }

        void HandleTouchUp (object sender, TouchLocation tl)
        {
            _previoustouchHeldLocation = _touchHeldLocation;
			//System.Diagnostics.Debug.WriteLine("Up: {0} BR: {1} VO: {2}", tl.Position, BoundsRect, VerticalScrollOffset);
        }

        bool HandleTouchHeld (object sender, float touchHeldTimeMs, TouchLocation tl)
        {
            var handled = false;

            _previoustouchHeldLocation = _touchHeldLocation;
            _touchHeldLocation = tl;
			//System.Diagnostics.Debug.WriteLine("Held: {0} BR: {1} VO: {2}", tl.Position, BoundsRect, VerticalScrollOffset);

            return handled;
        }

        private int PositionToLineNumber(Vector2 position)
        {
            return (int)((BoundsRect.Height - Height - VerticalScrollOffset + position.Y - Position.Y) / (TextRenderer.LineHeightAndSpacing * FontScaleFactor));
        }

        public Rectangle HighlightedLineBoundsRect
        {
            get
            {
                if (HighlightedLine < 0)
                {
                    return default(Rectangle);
                }

                return new Rectangle((int)Position.X,
                                     (int)(Math.Round(TextRenderer.LineHeightAndSpacing * FontScaleFactor) * HighlightedLine - BoundsRect.Height + Height + VerticalScrollOffset + Position.Y),
                                     Width,
                                     (int)(TextRenderer.LineHeightAndSpacing * FontScaleFactor));
            }
        }

		private double _scrollingVelocity;
		private int _scrollingDirection;
		private const float decceleration = 0.01f;

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            var distance = _touchHeldLocation.Position.Y - _previoustouchHeldLocation.Position.Y;
			var dt = gameTime.ElapsedGameTime;

            //if (distance > 0)
            //{
            //System.Diagnostics.Debug.WriteLine("Distance: {0}", distance);
            //}
            if (_textureUpdateNeeded)
            {
                UpdateVerticalScrollbarPosition();
            }
			if ((int)distance != 0)
			{
				_scrollingVelocity = Math.Abs(distance / dt.TotalMilliseconds);
			}
			if (distance > 0)
			{
				_scrollingDirection = 1;
			}
			else if (distance < 0)
			{
				_scrollingDirection = -1;
			}
			//System.Diagnostics.Debug.WriteLine("Dist: {0} DT: {1}, SV: {2}", distance, dt.TotalMilliseconds, _scrollingVelocity);
			if ((int)distance == 0 && _scrollingVelocity > 0)
			{
				distance = (float)(_scrollingDirection * _scrollingVelocity * dt.TotalMilliseconds);
				_scrollingVelocity -= decceleration * dt.TotalMilliseconds;
				if (_scrollingVelocity < 0)
				{
					_scrollingVelocity = 0;
				}
			}
            VerticalScrollOffset += (int)distance;
			//if ((int)distance != 0)
			//{
			//	System.Diagnostics.Debug.WriteLine("Update: distance: {0} VO: {1}", distance, VerticalScrollOffset);
			//}
        }

        public override void Draw(GameTime gameTime)
        {
            if (Anchor == Game.CurrentRenderingGroup &&
			    IsVisible)
            {
				var textPosition = Position;

	            switch (HorizontalAlign)
	            {
	                case HorizontalAlignment.Center:
	                    textPosition.X += Width / 2f;
	                    break;
	                case HorizontalAlignment.Right:
	                    textPosition.X += Width;
	                    break;
	            }
	            switch (VerticalAlign)
	            {
	                case VerticalAlignment.Middle:
	                    textPosition.Y += Height / 2f;
	                    break;
	                case VerticalAlignment.Bottom:
	                    textPosition.Y += Height;
	                    break;
	            }
				textPosition.Y += VerticalScrollOffset;

                _backgroundShape.Draw(gameTime);
                DrawTextAtPosition(textPosition);

                if (ShowVerticalScrollbar &&
                    _scrollBarTexture != null &&
                    _scrollBarBgTexture != null)
                {
                    Game.SpriteBatch.Draw(_scrollBarBgTexture, new Vector2(_scrollBarPosition.X, Position.Y), TextColor * 0.5f);
                    Game.SpriteBatch.Draw(_scrollBarTexture, _scrollBarPosition, TextColor * 0.8f);
                }
            }
        }

        private void DrawTextAtPosition(Vector2 position)
        {
            var colors = new[] { TextColor * Opacity };

            if (HighlightColor != TextColor &&
                HighlightColor != Color.Transparent)
            {
                var linesOfText = Text.Split('\n').Length;

                colors = new Color[linesOfText];
                for (var i = 0; i < linesOfText; i++)
                {
                    colors[i] = i == HighlightedLine
                                ? HighlightColor
                                : TextColor;
                }
            }
            if (UseCommonScissorRect)
            {
                TextRenderer.DrawText(
                    Game.SpriteBatch,
                    Text,
                    position,
                    FontScaleFactor,
                    colors,
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
                    colors,
                    (Alignment)VerticalAlign | (Alignment)HorizontalAlign,
                    Tabs,
                    false);

                Game.SpriteBatch.End();

                Game.SpriteBatch.GraphicsDevice.ScissorRectangle = origClippingRectangle;
                Game.SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, ScaleMatrix);
            }
        }
    }
}

