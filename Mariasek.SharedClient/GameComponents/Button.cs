﻿using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Content;

namespace Mariasek.SharedClient.GameComponents
{
    public class Button : TouchControlBase
    {
        private Color _highlightColor = new Color(0x99, 0x4d, 0x1a);
        private Color _origButtonColor = new Color(0x60, 0x30, 0x10);//Color.SaddleBrown;
        private Color _origTextColor = Color.White;
        private RectangleShape _buttonShape;
        protected Label _buttonText;

        public override Vector2 Position
        {
            get { return _buttonShape.Position; }
            set 
            { 
                _buttonShape.Position = value; 
                _buttonText.Position = value;
            }
        }
		public override AnchorType Anchor
		{
			get
			{
				return base.Anchor;
			}
			set
			{
				base.Anchor = value;
				_buttonShape.Anchor = value;
				_buttonText.Anchor = value;
			}
		}
        public int Width 
        { 
            get { return _buttonShape.Width; }
            set 
            { 
                _buttonShape.Width = value; 
                _buttonText.Width = value;
                _buttonText.Position = Position;
            } 
        }
        public int Height
        { 
            get { return _buttonShape.Height; }
            set 
            { 
                _buttonShape.Height = value; 
                _buttonText.Height = value;
                _buttonText.Position = Position;
            } 
        }
        public override bool IsEnabled
        {
            get { return base.IsEnabled; }
            set
            {
                base.IsEnabled = value;
                if(IsEnabled)
                {
                    if (_buttonShape != null && _buttonShape.BackgroundColors.Count > 0)
                    {
                        _buttonShape.BackgroundColors[0] = _origButtonColor;
                        _buttonShape.Opacity = 0.9f;
                        _buttonShape.UpdateTexture();
                    }
                    if (_buttonText != null)
                    {
                        _buttonText.TextColor = _origTextColor;
                        _buttonText.Opacity = 0.9f;
                    }
                }
                else
                {
                    if(_buttonShape != null && _buttonShape.BackgroundColors.Count > 0)
                    {
                        _buttonShape.BackgroundColors[0] = Color.Gray;
                        _buttonShape.Opacity = 0.5f;
                        _buttonShape.UpdateTexture();
                    }
                    if (_buttonText != null)
                    {
                        _buttonText.TextColor = Color.LightGray;
                        _buttonText.Opacity = 0.5f;
                    }
                }
            }
        }
        public Color BackgroundColor
        { 
            get { return _buttonShape.BackgroundColors[0]; }
            set
            {
                _buttonShape.BackgroundColors[0] = value;
                _origButtonColor = value;
                _buttonShape.UpdateTexture();
            } 
        }
        public Color BorderColor
        { 
            get { return _buttonShape.BorderColors[0]; }
            set 
            { 
                _buttonShape.BorderColors[0] = value; 
                _buttonShape.UpdateTexture();
            } 
        }
        public string Text 
        { 
            get { return _buttonText != null ? _buttonText.Text : "(null)"; } 
            set { _buttonText.Text = value; }
        }
        public Color TextColor
        { 
            get { return _buttonText.TextColor; } 
            set 
            { 
                _buttonText.TextColor = value; 
                _origTextColor = value;
            }
        }

        public Button(GameComponent parent)
            : base(parent)
        {
            _buttonShape = new RectangleShape(this)
            {
                BackgroundColors = new List<Color> { _origButtonColor },//Color.SaddleBrown },
                BorderColors = new List<Color> { Color.White },
                BorderRadius = 3,
                BorderThickness = 3,
                Opacity = 0.7f
            };
            _buttonText = new Label(this)
            {
                Text = Name,
                TextColor = Color.White,
                VerticalAlign = VerticalAlignment.Middle,
                HorizontalAlign = HorizontalAlignment.Center,
                Width = _buttonShape.Width,
                Height = _buttonShape.Height,
                TextRenderer = Game.FontRenderers["BMFont"]
            };
        }

        public override bool CollidesWithPosition(Vector2 position)
        {
            //TODO: use texture.GetData and poll for alpha channel in case we implement rounded corners
            return position.X >= Position.X &&
                position.Y >= Position.Y &&
                position.X <= Position.X + Width &&
                position.Y <= Position.Y + Height;
        }

        protected override void OnTouchDown(TouchLocation tl)
        {
            _buttonShape.BackgroundColors[0] = _highlightColor;
            _buttonShape.UpdateTexture();
            base.OnTouchDown(tl);
        }

        protected override void OnTouchUp(TouchLocation tl)
        {
            _buttonShape.BackgroundColors[0] = _origButtonColor;
            _buttonShape.UpdateTexture();
            base.OnTouchUp(tl);
        }

        protected override void OnClick()
        {
            ClickSound.Play();
            base.OnClick();
        }
    }
}

