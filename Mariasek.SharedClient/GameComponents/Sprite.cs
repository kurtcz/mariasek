using System;
using System.Collections.Concurrent;
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
        enum TransformationType
        {
            Move = 1,
            Rotate = 2,
            Scale = 4
        }
        class Transformation
        {
            public TransformationType TransformationType;
            public Vector2 Position { get; set; }
            public float Angle { get; set; }
            public Vector2 Scale { get; set; }
            public float Speed { get; set; }
            public float RotationSpeed { get; set; }
            public float ScalingSpeed { get; set; }
        }

        public override Vector2 Position
        {
            get { return _position; }
            set
            { 
                Transformation top; 

                _position = value;
                if (_transformations.TryPeek(out top) && (top.TransformationType & TransformationType.Move) != 0)
                {
                    top.Position = value;
                }
            }
        }
        public Vector2 Scale
        {
            get { return _scale; }
            set
            {
                Transformation top; 

                _scale = value;
                if (_transformations.TryPeek(out top) && (top.TransformationType & TransformationType.Scale) != 0)
                {
                    top.Scale = value;
                }
            }
        }
        public float RotationAngle
        {
            get { return _rotationAngle; }
            set
            {
                Transformation top; 

                _rotationAngle = value;
                if (_transformations.TryPeek(out top) && (top.TransformationType &= TransformationType.Rotate) != 0)
                {
                    top.Angle = value;
                }
            }
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
        private Vector2 _scale;
        private float _rotationAngle;
        private Rectangle _spriteRectangle;

        private ConcurrentQueue<Transformation> _transformations = new ConcurrentQueue<Transformation>();

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
            _scale = Vector2.One;//1f;
            _rotationAngle = 0f;
            _position = new Vector2();
            //_targetPosition = _position;
            //_targetAngle = _rotationAngle;
            //_targetScale = _scale;
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
            //_targetPosition = targetPosition;
            //_speed = speed;
            _transformations.Enqueue(new Transformation
                {
                    TransformationType = TransformationType.Move,
                    Position = targetPosition,
                    Speed = speed
                });
        }

        public Sprite RotateTo(float targetAngle, float rotationSpeed = 1f)
        {
            //_targetAngle = targetAngle;
            //_rotationSpeed = rotationSpeed;
            _transformations.Enqueue(new Transformation
                {
                    TransformationType = TransformationType.Rotate,
                    Angle = targetAngle,
                    RotationSpeed = rotationSpeed
                });

            return this;
        }

        public Sprite ScaleTo(float targetScale, float scalingSpeed = 1f)
        {
            return ScaleTo(new Vector2(targetScale), scalingSpeed);
        }

        public Sprite ScaleTo(Vector2 targetScale, float scalingSpeed = 1f)
        {
            //_targetScale = targetScale;
            //_scalingSpeed = scalingSpeed;
            _transformations.Enqueue(new Transformation
                {
                    TransformationType = TransformationType.Scale,
                    Scale = targetScale,
                    ScalingSpeed = scalingSpeed
                });

            return this;
        }

        public Sprite Slerp(Vector2 targetPosition, float targetAngle, float targetScale, float speed = 100f, float rotationSpeed = 1f, float scalingSpeed = 1f)
        {
            //MoveTo(targetPosition, speed);
            //RotateTo(targetAngle, rotationSpeed);
            //ScaleTo(targetScale, scalingSpeed);
            _transformations.Enqueue(new Transformation
                {
                    TransformationType = TransformationType.Move | TransformationType.Rotate | TransformationType.Scale,
                    Position = targetPosition,
                    Speed = speed,
                    Angle = targetAngle,
                    RotationSpeed = rotationSpeed,
                    Scale = new Vector2(targetScale),
                    ScalingSpeed = scalingSpeed
                });

            return this;
        }

        public void StopMoving()
        {
            Transformation dummy;

            while(_transformations.Count > 0)
            {
                _transformations.TryDequeue(out dummy);
            }
        }

        /// <summary>
        /// Updates sprite's properties during movement, rotation or scaling. This method is always called from the UI thread
        /// </summary>
        public override void Update(GameTime gameTime)
        {
            Transformation transformation;

            if (!_transformations.TryPeek(out transformation))
            //if(transformation == null)
            {
                return;
            }

            var wasMoving = IsMoving;
            var deltaTime = (float) gameTime.ElapsedGameTime.TotalSeconds;

            var moveVector = Vector2.Subtract(transformation.Position, _position);
            var normalizedDirection = moveVector.Length() == 0 ? moveVector : Vector2.Normalize(moveVector);
            var positionDiff = transformation.Speed * deltaTime * normalizedDirection;

            var rotationDirection = Math.Sign(transformation.Angle - RotationAngle);
            var angleDiff = transformation.RotationSpeed * deltaTime * rotationDirection;

            var scalingXDirection = Math.Sign(transformation.Scale.X - Scale.X);
            var scalingYDirection = Math.Sign(transformation.Scale.Y - Scale.Y);
            var scaleXDiff = transformation.ScalingSpeed * deltaTime * scalingXDirection;
            var scaleYDiff = transformation.ScalingSpeed * deltaTime * scalingYDirection;

            if ((transformation.TransformationType & TransformationType.Move) != 0 && positionDiff != Vector2.Zero)
            {
                if (positionDiff.Length() > moveVector.Length())
                {
                    _position = transformation.Position;
                }
                else
                {
                    _position += positionDiff;
                }
            }
            if ((transformation.TransformationType & TransformationType.Rotate) != 0 && angleDiff != 0)
            {
                if (angleDiff > Math.Abs(transformation.Angle - _rotationAngle))
                {
                    _rotationAngle = transformation.Angle;
                }
                else
                {
                    _rotationAngle += angleDiff;
                }
            }
            if ((transformation.TransformationType & TransformationType.Scale) != 0)
            {
                if (scaleXDiff != 0)
                {
                    if (scaleXDiff > Math.Abs(transformation.Scale.X - _scale.X))
                    {
                        _scale.X = transformation.Scale.X;
                    }
                    else
                    {
                        _scale.X += scaleXDiff;
                    }
                }
                if (scaleYDiff != 0)
                {
                    if (scaleYDiff > Math.Abs(transformation.Scale.Y - _scale.Y))
                    {
                        _scale.Y = transformation.Scale.Y;
                    }
                    else
                    {
                        _scale.Y += scaleYDiff;
                    }
                }
            }
                
            var moveFinished = positionDiff == Vector2.Zero && angleDiff == 0 && scaleXDiff == 0 && scaleYDiff == 0;
            if (moveFinished)
            {
                _transformations.TryDequeue(out transformation);
            }
            IsMoving = _transformations.Count > 0;
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
                    0   //depth
                );
            }

            base.Draw(gameTime);
        }
    }
}

