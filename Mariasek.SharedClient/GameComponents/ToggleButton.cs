
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;

namespace Mariasek.SharedClient.GameComponents
{
    public class ToggleButton : Button
    {
        private bool _isSelected;
        public bool IsSelected
        { 
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                TextColor = _isSelected ? Color.Yellow : Color.White;
            }
        }

        public ToggleButton(GameComponent parent)
            : base(parent)
        {
        }
            
        protected override void OnTouchDown(TouchLocation tl)
        {
            IsSelected = !IsSelected;
            if (IsSelected)
            {
                ClickSound = Game.OnSound;
            }
            else
            {
                ClickSound = Game.OffSound;
            }

            base.OnTouchDown(tl);
        }
    }
}

