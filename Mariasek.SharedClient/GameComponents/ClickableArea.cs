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
    public class ClickableArea : TouchControlBase
    {
        public float Width { get; set; }
        public float Height { get; set; }

        public ClickableArea(GameComponent parent)
            : base (parent)
        {
        }

        public override bool CollidesWithPosition(Vector2 position)
        {
            return position.X >= Position.X &&
                position.Y >= Position.Y &&
                position.X <= Position.X + Width &&
                position.Y <= Position.Y + Height;
        }
    }
}

