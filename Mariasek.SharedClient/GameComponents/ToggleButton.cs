
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;

namespace Mariasek.SharedClient.GameComponents
{
    public class ToggleButton : Button
    {
        private Color _origTextColor;
        private Color _origBorderColor;
        private bool _isSelected;
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
                        _origButtonColor = BackgroundColor;
                        _origTextColor = TextColor;
                        _origBorderColor = BorderColor;
                        if (_origButtonColor != Color.Transparent)
                        {
                            BackgroundColor = Game.Settings.SelectedButtonColor;//Color.DarkGreen;
                            TextColor = Color.White;
                        }
                        else
                        {
                            TextColor = Color.Yellow;
                        }
                        if (_origBorderColor != Color.Transparent)
                        {
                            BorderColor = Color.White;
                        }
                        ClickSound = Game.OnSound;
                    }
                    else
                    {
                        BackgroundColor = _origButtonColor;
                        TextColor = _origTextColor;
                        BorderColor = _origBorderColor;
                        ClickSound = Game.OffSound;
                    }
                }
            }
        }

        public ToggleButton(GameComponent parent)
            : base(parent)
        {
        }
            
        protected override void OnTouchDown(TouchLocation tl)
        {
            IsSelected = !IsSelected;

            base.OnTouchDown(tl);
        }
    }
}

