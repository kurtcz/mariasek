
using Microsoft.Xna.Framework;

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

