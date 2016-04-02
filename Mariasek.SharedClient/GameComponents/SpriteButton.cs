using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
//using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;


namespace Mariasek.SharedClient.GameComponents
{
    /// <summary>
    /// This is a game component that implements IUpdateable.
    /// </summary>
    public class SpriteButton : TouchControlBase
    {
        private Vector2 _position;

        public Sprite Sprite { get; set; }
        public bool IsMoving { get { return Sprite.IsMoving; } }

        public SpriteButton(GameComponent parent)
            : base(parent)
        {
            // TODO: Construct any child components here
        }

        public SpriteButton(GameComponent parent, Sprite sprite)
            : base(parent)
        {
            // TODO: Construct any child components here
            Sprite = sprite;
            Sprite.Parent = this;
        }

        /// <summary>
        /// Allows the game component to perform any initialization it needs to before starting
        /// to run.  This is where it can query for any required services and load content.
        /// </summary>
        public override void Initialize()
        {
            // TODO: Add your initialization code here

            base.Initialize();
        }

        public override bool CollidesWithPosition(Vector2 position)
        {
            //Calculate the sprite's bounding polygon based on the rotation origin and angle
            var polygon = Sprite.SpriteRectangle.Rotate(Sprite.RotationOrigin, Sprite.RotationAngle);

            for (var i = 0; i < polygon.Length; i++)
            {
                polygon[i] = Vector2.Subtract(polygon[i], Sprite.RotationOrigin);
                polygon[i] = Vector2.Add(polygon[i], Position);
            }

            return position.IsPointInPolygon(polygon);
        }

        public override Vector2 Position
        {
            get { return _position; }
            set
            {
                _position = value;
                if (!Sprite.IsMoving)
                {
                    Sprite.Position = _position;
                }
                //if sprite is moving its position will be adjusted inside Update()
            }
        }
            
        public override void Show()
        {
            base.Show();
            Sprite.Show();
        }

        public override void Hide()
        {
            base.Hide();
            Sprite.Hide();
        }
/*
        protected override void OnTouchDown()
        {
            Sprite.Tint = Color.LightSalmon;
            base.OnTouchDown();
        }

        protected override void OnTouchUp()
        {
            Sprite.Tint = Color.White;
            base.OnTouchUp();
        }

        protected override void OnClick()
        {
            Sprite.Tint = Color.LightGreen;
            base.OnClick();
        }
*/
        public SpriteButton MoveTo(Vector2 targetPosition, float speed = 100f)
        {
            Sprite.MoveTo(targetPosition, speed);

            return this;
        }

        public SpriteButton RotateTo(float targetAngle, float rotationSpeed = 1f)
        {
            Sprite.RotateTo(targetAngle, rotationSpeed);

            return this;
        }

        public SpriteButton ScaleTo(float targetScale, float scalingSpeed = 1f)
        {
            Sprite.ScaleTo(targetScale, scalingSpeed);

            return this;
        }

        public SpriteButton Slerp(Vector2 targetPosition, float targetAngle, float targetScale, float speed = 100f, float rotationSpeed = 1f, float scalingSpeed = 1f)
        {
            Sprite.Slerp(targetPosition, targetAngle, targetScale, speed, rotationSpeed, scalingSpeed);

            return this;
        }
            
        /// <summary>
        /// Allows the game component to update itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public override void Update(GameTime gameTime)
        {
            // TODO: Add your update code here
            Sprite.Update(gameTime);
            if (Sprite.IsMoving)
            {
                Position = Sprite.Position;
            }
            base.Update(gameTime);
        }
   
        public override void Draw(GameTime gameTime)
        {
            Sprite.Draw(gameTime);
        }
    }
}

