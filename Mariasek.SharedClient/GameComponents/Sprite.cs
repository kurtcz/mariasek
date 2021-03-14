//#define DEBUG_SPRITES
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mariasek.Engine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Mariasek.SharedClient.GameComponents
{
    public class Sprite : GameComponent
    {
        public bool UseCommonScissorRect { get; set; }
        protected class SpriteOperationType : GameComponentOperationType
        {
            public const int Rotate = 16;
            public const int Scale = 32;
        }
        class SpriteOperation : GameComponentOperation
        {
            public float Angle { get; set; }
            public Vector2 Scale { get; set; }
            public float RotationSpeed { get; set; }
            public float ScalingSpeed { get; set; }
        }

        public Vector2 Scale
        {
            get { return _scale; }
            set { _scale = value; }
        }
        public float RotationAngle
        {
            get { return _rotationAngle; }
            set { _rotationAngle = value; }
        }
        public Texture2D Texture
        {
            get { return _spriteTexture; }
            set { _spriteTexture = value; }
        }
        public Color Tint { get; set; }
        public SpriteEffects Flip { get; set; }
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
        private bool _isMoving;
        public override bool IsMoving 
        { 
            get { return _isMoving; } 
            protected set { _isMoving = value; } 
        }
        private Texture2D _spriteTexture;
		private Vector2 _scale;
        private float _rotationAngle;
        private Rectangle _spriteRectangle;

        //TODO: Call parent constructor
        public Sprite(GameComponent parent, Texture2D texture, Rectangle? spriteRectangle = null, Vector2? rotationOrigin = null)
            : base(parent)
        {
            Init(texture, spriteRectangle, rotationOrigin);
        }

        //public Sprite(GameComponent parent, ContentManager content, string name)
        //    : base(parent)
        //{
        //    _spriteTexture = content.Load<Texture2D>(name);
        //    Name = name;
        //    Init(_spriteTexture);
        //}

        private void Init(Texture2D texture, Rectangle? spriteRectangle = null, Vector2? rotationOrigin = null)
        {
            _spriteTexture = texture;
            //set the sprite clipping rectangle (null signifies no clipping)
            SpriteRectangle = spriteRectangle.HasValue 
                ? spriteRectangle.Value 
                : ( _spriteTexture != null
                    ? _spriteTexture.Bounds
                    : new Rectangle());
            _scale = Vector2.One;//1f;
            _rotationAngle = 0f;
            Position = new Vector2();
            Opacity = 1f;
            Tint = Color.White;
            //by default set the rotation origin to the center point
            if (rotationOrigin.HasValue)
            {
                RotationOrigin = rotationOrigin.Value;
            }
            Name = ToString();
        }

        public Sprite RotateToImpl(float targetAngle, float rotationSpeed = 1f)
        {
            ScheduledOperations.Enqueue(new SpriteOperation
                {
                    OperationType = SpriteOperationType.Rotate,
                    Angle = targetAngle,
                    RotationSpeed = rotationSpeed
                });

            return this;
        }
            
        public Sprite ScaleToImpl(float targetScale, float scalingSpeed = 1f)
        {
            return ScaleToImpl(new Vector2(targetScale), scalingSpeed);
        }

        public Sprite ScaleToImpl(Vector2 targetScale, float scalingSpeed = 1f)
        {
            ScheduledOperations.Enqueue(new SpriteOperation
                {
                    OperationType = SpriteOperationType.Scale,
                    Scale = targetScale,
                    ScalingSpeed = scalingSpeed
                });

            return this;
        }

        public Sprite SlerpImpl(Vector2 targetPosition, float targetAngle, float targetScale, float speed = 100f, float rotationSpeed = 1f, float scalingSpeed = 1f)
        {
            ScheduledOperations.Enqueue(new SpriteOperation
                {
                    OperationType = SpriteOperationType.Move | SpriteOperationType.Rotate | SpriteOperationType.Scale,
                    Position = targetPosition,
                    Speed = speed,
                    Angle = targetAngle,
                    RotationSpeed = rotationSpeed,
                    Scale = new Vector2(targetScale),
                    ScalingSpeed = scalingSpeed
                });

            return this;
        }

        /// <summary>
        /// Updates sprite's properties during movement, rotation or scaling. This method is always called from the UI thread
        /// </summary>
        public override void Update(GameTime gameTime)
        {
            GameComponentOperation operation;
            SpriteOperation transformation;

            if (!ScheduledOperations.TryPeek(out operation))
            {
                return;
            }
            transformation = operation as SpriteOperation;
            if (transformation != null)
            {
                var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

                var moveVector = Vector2.Subtract(operation.Position, Position);
                var normalizedDirection = moveVector.Length() == 0 ? moveVector : Vector2.Normalize(moveVector);
                var positionDiff = operation.Speed * deltaTime * normalizedDirection;

                var rotationDirection = Math.Sign(transformation.Angle - RotationAngle);
                var angleDiff = transformation.RotationSpeed * deltaTime * rotationDirection;

                var scalingXDirection = Math.Sign(transformation.Scale.X - Scale.X);
                var scalingYDirection = Math.Sign(transformation.Scale.Y - Scale.Y);
                var scaleXDiff = transformation.ScalingSpeed * deltaTime * scalingXDirection;
                var scaleYDiff = transformation.ScalingSpeed * deltaTime * scalingYDirection;

                if ((transformation.OperationType & SpriteOperationType.Move) != 0 && positionDiff != Vector2.Zero)
                {
                    if (positionDiff.Length() > moveVector.Length())
                    {
                        Position = operation.Position;
                    }
                    else
                    {
                        Position += positionDiff;
                    }
                }

                if ((transformation.OperationType & SpriteOperationType.Rotate) != 0 && angleDiff != 0)
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

                if ((transformation.OperationType & SpriteOperationType.Scale) != 0)
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
                
                var moveFinished = (transformation.OperationType & (SpriteOperationType.Move | SpriteOperationType.Rotate | SpriteOperationType.Scale)) != 0 &&
                    positionDiff == Vector2.Zero && angleDiff == 0 && scaleXDiff == 0 && scaleYDiff == 0;
                if (moveFinished)
                {
                    ScheduledOperations.TryDequeue(out operation);
                }
                _isMoving = (transformation.OperationType & (SpriteOperationType.Move | SpriteOperationType.Rotate | SpriteOperationType.Scale)) != 0 &&
                    (positionDiff != Vector2.Zero || angleDiff != 0 || scaleXDiff != 0 || scaleYDiff != 0);
            }

            base.Update(gameTime);
        }
                   
        public override void Draw(GameTime gameTime)
        {
            //System.Diagnostics.Debug.WriteLine(string.Format("Draw {0}/{8} {1} [{2} {3} - {4} {5}] {6} {7}", 
            //    Name, Position, SpriteRectangle.Left, SpriteRectangle.Top, SpriteRectangle.Right, SpriteRectangle.Bottom, RotationOrigin, RotationAngle, Tag));
            //we will be redrawing our sprite with each frame
            if(Anchor == Game.CurrentRenderingGroup &&
			   IsVisible && 
			   _spriteTexture != null)
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
                    Flip,
                    ZIndex/100f   //depth
                );
#if DEBUG_SPRITES
                Game.CurrentScene.Counter++;
                Game.FontRenderers["BM2Font"].DrawText(
                    Game.SpriteBatch, 
                    string.Format("{0}/{1}", Game.CurrentScene.Counter, Parent.ZIndex != 0 ? Parent.ZIndex : ZIndex),
                    Position,
                    Color.Orange, 
                    Alignment.MiddleCenter);   
#endif
            }

            base.Draw(gameTime);
        }
    }
}

