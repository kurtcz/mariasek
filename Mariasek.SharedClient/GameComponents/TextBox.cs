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
            switch (VerticalAlign)
            {
                case VerticalAlignment.Top:
                    if (Height < BoundsRect.Height)
                    {
                        _minVerticalScrollOffset = Height - BoundsRect.Height;
                        _maxVerticalScrollOffset = 0;
                    }
                    VerticalScrollOffset = 0;
                    break;
                case VerticalAlignment.Middle:
                    if (Height < BoundsRect.Height)
                    {
                        _minVerticalScrollOffset = Height - BoundsRect.Height / 2;
                        _maxVerticalScrollOffset = BoundsRect.Height / 2;
                    }
                    VerticalScrollOffset = Height / 2;
                    break;
                case VerticalAlignment.Bottom:
                    if (Height < BoundsRect.Height)
                    {
                        _minVerticalScrollOffset = Height;
                        _maxVerticalScrollOffset = BoundsRect.Height;
                    }
                    VerticalScrollOffset = Height;
                    break;
            }
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
            }
            else if(_scrollBarTexture != null)
            {
                _scrollBarTexture.Dispose();
                _scrollBarTexture = null;
            }
        }

        void HandleTouchDown (object sender, TouchLocation tl)
        {
            _touchDownLocation = tl;
            _touchHeldLocation = tl;
        }

        void HandleTouchUp (object sender, TouchLocation tl)
        {
            _previoustouchHeldLocation = _touchHeldLocation;
        }

        bool HandleTouchHeld (object sender, float touchHeldTimeMs, TouchLocation tl)
        {
            var handled = false;

            _previoustouchHeldLocation = _touchHeldLocation;
            _touchHeldLocation = tl;

            return handled;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            var distance = _touchHeldLocation.Position.Y - _previoustouchHeldLocation.Position.Y;
            VerticalScrollOffset += (int)distance;
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
                position.Y += VerticalScrollOffset;

                _backgroundShape.Draw(gameTime);
                DrawTextAtPosition(position);

                if (_scrollBarTexture != null)
                {
                    Game.SpriteBatch.Draw(_scrollBarBgTexture, new Vector2(_scrollBarPosition.X, Position.Y), TextColor * 0.5f);
                    Game.SpriteBatch.Draw(_scrollBarTexture, _scrollBarPosition, TextColor * 0.8f);
                }
            }
        }
    }
}

