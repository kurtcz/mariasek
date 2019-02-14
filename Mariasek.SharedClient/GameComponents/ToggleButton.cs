
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;

namespace Mariasek.SharedClient.GameComponents
{
    public class ToggleButton : Button
    {
        private Color _origTextColor;
        private Color _origBorderColor;
        private bool _isSelected;

        public Color SelectedBackgroundColor { get; set; }

        public bool IsSelected
        { 
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    if (_isSelected)
                    {
                        _origTextColor = _buttonText.TextColor;
                        _origBorderColor = _buttonShape.BorderColors[0];
                        if (_origButtonColor != Color.Transparent)
                        {
                            _buttonShape.BackgroundColors[0] = SelectedBackgroundColor;
                            _buttonText.TextColor = Color.White;
                        }
                        else
                        {
                            _buttonText.TextColor = Color.Yellow;
                        }
                        if (_origBorderColor != Color.Transparent)
                        {
                            _buttonShape.BorderColors[0] = Color.White;
                        }
                        ClickSound = Game.OnSound;
                    }
                    else
                    {
                        _buttonShape.BackgroundColors[0] = _origButtonColor;
                        _buttonText.TextColor = _origTextColor;
                        _buttonShape.BorderColors[0] = _origBorderColor;
                        ClickSound = Game.OffSound;
                    }
                    _buttonShape.UpdateTexture();
                }
            }
        }

        public ToggleButton(GameComponent parent)
            : base(parent)
        {
            SelectedBackgroundColor = Game.Settings.SelectedButtonColor;
        }
            
        protected override void OnTouchDown(TouchLocation tl)
        {
            IsSelected = !IsSelected;

            base.OnTouchDown(tl);

            if (_origBorderColor != Color.Transparent)
            {
                _buttonShape.BackgroundColors[0] = IsSelected ? Game.Settings.SelectedButtonColor : _origButtonColor;
                _buttonShape.UpdateTexture();
            }
        }

        protected override void OnTouchUp(TouchLocation tl)
        {
            base.OnTouchUp(tl);

            if (_origBorderColor != Color.Transparent)
            {
                _buttonShape.BackgroundColors[0] = IsSelected ? Game.Settings.SelectedButtonColor : _origButtonColor;
                _buttonShape.UpdateTexture();
            }
        }
    }
}

