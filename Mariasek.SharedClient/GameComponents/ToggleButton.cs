
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;

namespace Mariasek.SharedClient.GameComponents
{
    public class ToggleButton : Button
    {
        private Color _origTextColor;
        private Color _origBorderColor;
        private Color _origBgColor;
        private bool _isSelected;

        public override Color TextColor
        {
            get { return base.TextColor; }
            set { _origTextColor = value; base.TextColor = value; }
        }
        public override Color BackgroundColor
        {
            get { return base.BackgroundColor; }
            set { _origBgColor = value; base.BackgroundColor = value; }
        }
        public override Color BorderColor
        {
            get { return base.BorderColor; }
            set { _origBorderColor = value; base.BorderColor = value; }
        }
        public Color SelectedBackgroundColor { get; set; }
        public Color SelectedTextColor { get; set; }

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
                        if (_origBgColor != Color.Transparent)
                        {
                            _buttonShape.BackgroundColors[0] = Game.Settings.InvertedToggleButton ? SelectedTextColor : SelectedBackgroundColor;
                            _buttonText.TextColor = Game.Settings.InvertedToggleButton ? SelectedBackgroundColor : SelectedTextColor;
                        }
                        else
                        {
                            _buttonText.TextColor = Game.Settings.HighlightedTextColor;
                        }
                        if (_origBorderColor != Color.Transparent)
                        {
                            _buttonShape.BorderColors[0] = Game.Settings.InvertedToggleButton ? SelectedBackgroundColor : SelectedTextColor;
                        }
                        ClickSound = Game.OnSound;
                    }
                    else
                    {
                        _buttonShape.BackgroundColors[0] = _origBgColor;
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
            _origBgColor = base.BackgroundColor;
            SelectedBackgroundColor = Game.Settings.SelectedButtonColor;
            SelectedTextColor = Game.Settings.SelectedButtonTextColor;
        }

        protected override void OnTouchDown(TouchLocation tl)
        {
            IsSelected = !IsSelected;

            base.OnTouchDown(tl);

            if (_origBorderColor != Color.Transparent)
            {
                _buttonShape.BackgroundColors[0] = IsSelected
                                                    ? Game.Settings.InvertedToggleButton
                                                        ? Game.Settings.SelectedButtonTextColor
                                                        : Game.Settings.SelectedButtonColor
                                                    : _origButtonColor;
                _buttonShape.UpdateTexture();
            }
        }

        protected override void OnTouchUp(TouchLocation tl)
        {
            base.OnTouchUp(tl);

            if (_origBorderColor != Color.Transparent)
            {
                _buttonShape.BackgroundColors[0] = IsSelected
                                                    ? Game.Settings.InvertedToggleButton
                                                        ? Game.Settings.SelectedButtonTextColor
                                                        : Game.Settings.SelectedButtonColor
                                                    : _origButtonColor;
                _buttonShape.UpdateTexture();
            }
        }
    }
}

