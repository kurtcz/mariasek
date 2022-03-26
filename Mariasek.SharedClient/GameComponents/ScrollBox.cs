using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace Mariasek.SharedClient.GameComponents
{
    public abstract class ScrollBox : GameComponent
    {
        private TouchLocation _touchDownLocation;
        private TouchLocation _touchHeldLocation;
        private TouchLocation _previoustouchHeldLocation;
        protected ClickableArea _clickableArea;
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
            }
        }

        private int _width;
        public virtual int Width
        {
            get { return _width; }
            set
            {
                _width = value;
                _clickableArea.Width = value;
                UpdateVerticalScrollbar();
            }
        }

        private int _height;
        public virtual int Height
        {
            get { return _height; }
            set
            {
                _height = value;
                _clickableArea.Height = value;
                UpdateVerticalScrollbar();
            }
        }

        public Color ScrollBarColor { get; set; }
        public Color ScrollBarBackgroundColor { get; set; }

        private Rectangle _boundsRect;
        protected virtual Rectangle BoundsRect { get { return _boundsRect; } set { _boundsRect = value; UpdateVerticalScrollbar(); } }

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

        public ScrollBox(GameComponent parent)
            : base(parent)
        {
            _clickableArea = new ClickableArea(this);
            Width = 100;
            Height = 100;
            ScrollBarColor = Color.White;
            ScrollBarBackgroundColor = Color.Transparent;
            _clickableArea.TouchDown += HandleTouchDown;
            _clickableArea.TouchUp += HandleTouchUp;
            _clickableArea.TouchHeld += HandleTouchHeld;
        }

        /// <summary>
        /// Called when Width or Height changes
        /// </summary>
        private void UpdateVerticalScrollbar()
        {
            if (Height >= BoundsRect.Height)
            {
                _minVerticalScrollOffset = 0;
                _maxVerticalScrollOffset = 0;
            }
            else
            {
                _minVerticalScrollOffset = Height - BoundsRect.Height;
                _maxVerticalScrollOffset = 0;
                //switch (VerticalAlign)
                //{
                //    case VerticalAlignment.Top:
                //        _minVerticalScrollOffset = Height - BoundsRect.Height;
                //        _maxVerticalScrollOffset = 0;
                //        break;
                //    case VerticalAlignment.Middle:
                //        _minVerticalScrollOffset = (Height - BoundsRect.Height) / 2;
                //        _maxVerticalScrollOffset = (BoundsRect.Height - Height) / 2;
                //        break;
                //    case VerticalAlignment.Bottom:
                //        _minVerticalScrollOffset = 0;
                //        _maxVerticalScrollOffset = BoundsRect.Height - Height;
                //        break;
                //}
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
                //switch (VerticalAlign)
                //{
                //    case VerticalAlignment.Middle:
                //        _scrollBarPosition.Y += (Height - _scrollBarHeight) / 2;
                //        break;
                //    case VerticalAlignment.Bottom:
                //        _scrollBarPosition.Y += (Height - _scrollBarHeight);
                //        break;
                //}
            }
            else if (_scrollBarTexture != null)
            {
                if (!_scrollBarTexture.IsDisposed)
                {
                    _scrollBarTexture.Dispose();
                }
                _scrollBarTexture = null;
            }
        }

        void HandleTouchDown(object sender, TouchLocation tl)
        {
            _touchDownLocation = tl;
            _touchHeldLocation = tl;
            _previoustouchHeldLocation = tl;
            _scrollingVelocity = 0;
            System.Diagnostics.Debug.WriteLine("Down: {0} BR: {1} VO: {2}", tl.Position, BoundsRect, VerticalScrollOffset);
        }

        void HandleTouchUp(object sender, TouchLocation tl)
        {
            _previoustouchHeldLocation = _touchHeldLocation;
            System.Diagnostics.Debug.WriteLine("Up: {0} BR: {1} VO: {2}", tl.Position, BoundsRect, VerticalScrollOffset);
        }

        bool HandleTouchHeld(object sender, float touchHeldTimeMs, TouchLocation tl)
        {
            var handled = false;

            _previoustouchHeldLocation = _touchHeldLocation;
            _touchHeldLocation = tl;
            //System.Diagnostics.Debug.WriteLine("Held: {0} BR: {1} VO: {2}", tl.Position, BoundsRect, VerticalScrollOffset);

            return handled;
        }

        public void ScrollTo(int verticalOffset)
        {
            VerticalScrollOffset = verticalOffset;
        }

        private double _scrollingVelocity;
        private int _scrollingDirection;
        private const float decceleration = 0.002f;

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            var dt = gameTime.ElapsedGameTime;
            var distance = _touchHeldLocation.Position.Y - _previoustouchHeldLocation.Position.Y;

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
            //  System.Diagnostics.Debug.WriteLine("Update: distance: {0} VO: {1}", distance, VerticalScrollOffset);
            //}
        }

        public override void Draw(GameTime gameTime)
        {
            if (Anchor == Game.CurrentRenderingGroup &&
                IsVisible)
            {
                Game.SpriteBatch.End();
                var origClippingRectangle = Game.SpriteBatch.GraphicsDevice.ScissorRectangle;
                //we need to create a new sprite batch instance that is going to use a clipping rectangle
                if (!_savingToTexture)
                {
                    Game.SpriteBatch.GraphicsDevice.ScissorRectangle = new Rectangle((int)(ScaleMatrix.M41 + Position.X * ScaleMatrix.M11),
                                                                         (int)(ScaleMatrix.M42 + Position.Y * ScaleMatrix.M22),
                                                                         (int)(Width * ScaleMatrix.M11),
                                                                         (int)(Height * ScaleMatrix.M22));
                }
                Game.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, new RasterizerState { ScissorTestEnable = true }, null, ScaleMatrix);

                for (var i = 0; i < Children.Count; i++)
                {
                    Children[i].Position = new Vector2(Children[i].Position.X, Children[i].Position.Y + VerticalScrollOffset);
                }
                base.Draw(gameTime);    //render the child components
                for (var i = 0; i < Children.Count; i++)
                {
                    Children[i].Position = new Vector2(Children[i].Position.X, Children[i].Position.Y - VerticalScrollOffset);
                }

                Game.SpriteBatch.End();

                Game.SpriteBatch.GraphicsDevice.ScissorRectangle = origClippingRectangle;
                Game.SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, ScaleMatrix);

                if (_scrollBarTexture != null)
                {
                    Game.SpriteBatch.Draw(_scrollBarBgTexture, new Vector2(_scrollBarPosition.X, Position.Y), ScrollBarBackgroundColor * 0.5f);
                    Game.SpriteBatch.Draw(_scrollBarTexture, _scrollBarPosition, ScrollBarColor * 0.8f);
                }
            }
        }

        private bool _savingToTexture;

		public RenderTarget2D SaveTexture()
		{
			var target = new RenderTarget2D(Game.SpriteBatch.GraphicsDevice,
                                            Game.Graphics.PreferredBackBufferWidth,
                                            //(int)(ScaleMatrix.M41 + (Position.X + BoundsRect.Width) * ScaleMatrix.M11),
                                            (int)(ScaleMatrix.M42 + (Position.Y + BoundsRect.Height) * ScaleMatrix.M22));
			Game.SpriteBatch.GraphicsDevice.SetRenderTarget(target);
            Game.SpriteBatch.GraphicsDevice.Clear(Color.DimGray);

			var visible = IsVisible;
			var origRasterizerState = Game.SpriteBatch.GraphicsDevice.RasterizerState;
			var renderGroup = Game.CurrentRenderingGroup;
            //we need to create a new sprite batch instance that is going to use a clipping rectangle
            Game.SpriteBatch.GraphicsDevice.RasterizerState = new RasterizerState { ScissorTestEnable = false };

			Show();
			Game.CurrentRenderingGroup = Anchor;

			Game.SpriteBatch.Begin();
            _savingToTexture = true;
			Draw(new GameTime());
            _savingToTexture = false;
			Game.SpriteBatch.End();

			Game.SpriteBatch.GraphicsDevice.SetRenderTarget(null);
			Game.SpriteBatch.GraphicsDevice.RasterizerState = origRasterizerState;

			if (!visible)
			{
				Hide();
			}
			Game.CurrentRenderingGroup = renderGroup;

			return target;
		}
    }
}
