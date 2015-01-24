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

namespace Mariasek.AndroidClient.Sprites
{
	public class Sprite
	{
		public Vector2 Position
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
        public float Opacity { get; set; }
        public Color Tint { get; set; }
		public Vector2 RotationOrigin { get; set; }
		public Rectangle? SpriteRectangle { get; set; }
		public string Name { get; set; }
		public bool IsVisible { get; private set; }
		public bool IsMoving { get; private set; }

		private Texture2D _spriteTexture;
		private Vector2 _position;
		private float _scale;
		private float _rotationAngle;
		private float _speed;
		private float _rotationSpeed;
		private float _scalingSpeed;
		private Vector2 _targetPosition;
		private float _targetAngle;
		private float _targetScale;

		public Sprite(Texture2D texture, Rectangle? spriteRectangle = null, Vector2? rotationOrigin = null)
		{
            Init(texture, spriteRectangle, rotationOrigin);
		}

		public Sprite(ContentManager content, string name)
		{
			_spriteTexture = content.Load<Texture2D>(name);
			Name = name;
            Init(_spriteTexture);
		}

        private void Init(Texture2D texture, Rectangle? spriteRectangle = null, Vector2? rotationOrigin = null)
        {
            _spriteTexture = texture;
            //set the sprite clipping rectangle (null signifies no clipping)
            SpriteRectangle = spriteRectangle;
            _scale = 1f;
            _rotationAngle = 0f;
            _position = new Vector2();
            _targetPosition = _position;
            _targetAngle = _rotationAngle;
            _targetScale = _scale;
            Opacity = 1f;
            Tint = Color.White;
            //by default set the rotation origin to the center point
            RotationOrigin = rotationOrigin
                             ??   (spriteRectangle.HasValue
                                    ? new Vector2(spriteRectangle.Value.Width / 2f, spriteRectangle.Value.Height / 2f)
                                    : new Vector2(_spriteTexture.Bounds.Width / 2f, _spriteTexture.Bounds.Height / 2f));
            Name = ToString();
        }

        /// <summary>
        /// Updates sprite's properties during movement, rotation or scaling. This method is always called from the UI thread
        /// </summary>
		public void Update(GameTime gameTime)
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

		public void Show()
		{
			IsVisible = true;
		}

		public void Hide()
		{
			IsVisible = false;
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

		public void Draw(SpriteBatch spriteBatch)
		{
			//we will be redrawing our sprite with each frame
		if(IsVisible)
			{
				spriteBatch.Draw(
					_spriteTexture,
					Position,
					SpriteRectangle,
					Tint * Opacity,
					RotationAngle,
					RotationOrigin,
					Scale,
					(SpriteEffects)0,
					0	//depth
				);
			}
		}
	}
}

