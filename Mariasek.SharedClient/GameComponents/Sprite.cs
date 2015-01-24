using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace Mariasek.SharedClient.GameComponents
{
    public class Sprite : GameComponent
	{
		public override Vector2 Position
		{
			get { return _position; }
            set { _position = value; _targetPosition = value; }
		}
		public float Scale
		{
			get { return _scale; }
			set { _scale = value; _targetScale = value; }
		}
		public float RotationAngle
		{
			get { return _rotationAngle; }
			set { _rotationAngle = value; _targetAngle = value; }
		}
        public Texture2D Texture
        {
            get { return _spriteTexture; }
            set { _spriteTexture = value; }
        }
        public float Opacity { get; set; }
        public Color Tint { get; set; }
		public Vector2 RotationOrigin { get; set; }
		public Rectangle SpriteRectangle
        { 
            get { return _spriteRectangle; } 
            set
            { 
                _spriteRectangle = value; 
                RotationOrigin = new Vector2(
                    (_spriteRectangle.Left + _spriteRectangle.Right) / 2f, 
                    (_spriteRectangle.Top + _spriteRectangle.Bottom) / 2f);
            } 
        }
		public bool IsMoving { get; private set; }

		private Texture2D _spriteTexture;
		private Vector2 _position;
		private float _scale;
		private float _rotationAngle;
        private Rectangle _spriteRectangle;
		private float _speed;
		private float _rotationSpeed;
		private float _scalingSpeed;
		private Vector2 _targetPosition;
		private float _targetAngle;
		private float _targetScale;

        //TODO: Call parent constructor
        public Sprite(GameComponent parent, Texture2D texture, Rectangle? spriteRectangle = null, Vector2? rotationOrigin = null)
            : base(parent)
		{
            Init(texture, spriteRectangle, rotationOrigin);
		}

        public Sprite(GameComponent parent, ContentManager content, string name)
            : base(parent)
		{
			_spriteTexture = content.Load<Texture2D>(name);
			Name = name;
            Init(_spriteTexture);
		}

        public void GameRestarted()
        {
            if (_spriteTexture.IsDisposed)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0} {1} {2} update texture", Name, Position.X, Position.Y));
                _spriteTexture = Game.Content.Load<Texture2D>(_spriteTexture.Name);
            }
        }

        private void Init(Texture2D texture, Rectangle? spriteRectangle = null, Vector2? rotationOrigin = null)
        {
            Game.Restarted += GameRestarted;
            _spriteTexture = texture;
            //set the sprite clipping rectangle (null signifies no clipping)
            SpriteRectangle = spriteRectangle.HasValue 
                                ? spriteRectangle.Value 
                                : ( _spriteTexture != null
                                    ? _spriteTexture.Bounds
                                    : new Rectangle());
            _scale = 1f;
            _rotationAngle = 0f;
            _position = new Vector2();
            _targetPosition = _position;
            _targetAngle = _rotationAngle;
            _targetScale = _scale;
            Opacity = 1f;
            Tint = Color.White;
            //by default set the rotation origin to the center point
            if (rotationOrigin.HasValue)
            {
                RotationOrigin = rotationOrigin.Value;
            }
//            RotationOrigin = rotationOrigin
//                             ??   (spriteRectangle.HasValue
//                                    ? new Vector2(spriteRectangle.Value.Width / 2f, spriteRectangle.Value.Height / 2f)
//                                    : ( _spriteTexture != null
//                                        ? new Vector2(_spriteTexture.Bounds.Width / 2f, _spriteTexture.Bounds.Height / 2f)
//                                        : new Vector2()));
            Name = ToString();
        }

		public void MoveTo(Vector2 targetPosition, float speed = 100f)
		{
			_targetPosition = targetPosition;
			_speed = speed;
		}

		public void RotateTo(float targetAngle, float rotationSpeed = 1f)
		{
			_targetAngle = targetAngle;
			_rotationSpeed = rotationSpeed;
		}

		public void ScaleTo(float targetScale, float scalingSpeed = 1f)
		{
			_targetScale = targetScale;
			_scalingSpeed = scalingSpeed;
		}

		public void Slerp(Vector2 targetPosition, float targetAngle = 0f, float targetScale = 1f, float speed = 100f, float rotationSpeed = 1f, float scalingSpeed = 1f)
		{
			MoveTo(targetPosition, speed);
			RotateTo(targetAngle, rotationSpeed);
			ScaleTo(targetScale, scalingSpeed);
		}

        /// <summary>
        /// Updates sprite's properties during movement, rotation or scaling. This method is always called from the UI thread
        /// </summary>
        public override void Update(GameTime gameTime)
        {
            var deltaTime = (float) gameTime.ElapsedGameTime.TotalSeconds;
            var moveVector = Vector2.Subtract(_targetPosition, _position);
            var normalizedDirection = moveVector.Length() == 0 ? moveVector : Vector2.Normalize(moveVector);
            var rotationDirection = Math.Sign(_targetAngle - RotationAngle);
            var scalingDirection = Math.Sign(_targetScale - Scale);
            var positionDiff = _speed * deltaTime * normalizedDirection;
            var angleDiff = _rotationSpeed * deltaTime * rotationDirection;
            var scaleDiff = _scalingSpeed * deltaTime * scalingDirection;

            if (positionDiff.Length() > moveVector.Length())
            {
                _position = _targetPosition;
            }
            else
            {
                _position += positionDiff; 
            }

            if (angleDiff > Math.Abs(_targetAngle - _rotationAngle))
            {
                _rotationAngle = _targetAngle;
            }
            else
            {
                _rotationAngle += angleDiff;
            }

            if (scaleDiff > Math.Abs(_targetScale - _scale))
            {
                _scale = _targetScale;
            }
            else
            {
                _scale += scaleDiff;
            }

            IsMoving = positionDiff.Length() > 0 || angleDiff != 0 || scaleDiff != 0;
        }

        public override void Draw(GameTime gameTime)
		{
            //System.Diagnostics.Debug.WriteLine(string.Format("Draw {0}/{8} {1} [{2} {3} - {4} {5}] {6} {7}", 
            //    Name, Position, SpriteRectangle.Left, SpriteRectangle.Top, SpriteRectangle.Right, SpriteRectangle.Bottom, RotationOrigin, RotationAngle, Tag));
			//we will be redrawing our sprite with each frame
            if(IsVisible && _spriteTexture != null)
			{
                if (_spriteTexture.IsDisposed)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("{0} texture disposed", _spriteTexture.Name));
                }
				Game.SpriteBatch.Draw(
					_spriteTexture,
                    Position,
					SpriteRectangle,
					Tint * Opacity,
					RotationAngle,
                    Vector2.Subtract(RotationOrigin, new Vector2(SpriteRectangle.Left, SpriteRectangle.Top)),
					Scale,
					(SpriteEffects)0,
					0	//depth
				);
			}

            base.Draw(gameTime);
		}
	}
}

