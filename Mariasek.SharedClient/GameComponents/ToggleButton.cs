using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
//using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

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
            base.OnTouchDown(tl);

            IsSelected = !IsSelected;
        }
    }
}

