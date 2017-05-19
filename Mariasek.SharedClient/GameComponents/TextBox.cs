using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace Mariasek.SharedClient.GameComponents
{
    public class TextBox : Label
    {
        private TouchLocation _touchDownLocation;
        private TouchLocation _touchHeldLocation;
        private TouchLocation _previoustouchHeldLocation;
        private RectangleShape _backgroundShape;
        private ClickableArea _clickableArea;

        private Texture2D _scrollBarTexture, _scrollBarBgTexture;
        private int _scrollBarWidth = 5;
        private int _scrollBarHeight;
        private Vector2 _scrollBarPosition;

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

        public override string Text
        {
            get { return base.Text; }
            set
            {
                base.Text = value;

                var lineSeparators = new [] { '\r', '\n' };
                var lines = value.Split(lineSeparators, StringSplitOptions.RemoveEmptyEntries);

                BoundsRect = TextRenderer.GetBoundsRect(lines);

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

        public float Opacity
        {
            get { return _backgroundShape.Opacity; }
            set { _backgroundShape.Opacity = value; }
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
                BorderThickness = 3
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
            else if(_scrollBarTexture != null)
            {
                if (!_scrollBarTexture.IsDisposed)
                {
                    _scrollBarTexture.Dispose();
                }
                _scrollBarTexture = null;
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

		private double _scrollingVelocity;
		private int _scrollingDirection;
		private const float decceleration = 0.02f;

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            var distance = _touchHeldLocation.Position.Y - _previoustouchHeldLocation.Position.Y;
			var dt = gameTime.ElapsedGameTime;

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

                if (_scrollBarTexture != null)
                {
                    Game.SpriteBatch.Draw(_scrollBarBgTexture, new Vector2(_scrollBarPosition.X, Position.Y), TextColor * 0.5f);
                    Game.SpriteBatch.Draw(_scrollBarTexture, _scrollBarPosition, TextColor * 0.8f);
                }
            }
        }
    }
}

