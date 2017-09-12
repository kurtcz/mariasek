using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;

namespace Mariasek.SharedClient.GameComponents
{
    public class CardButton : SpriteButton
    {
        private Sprite _reverseSprite;
        private bool _doneFlipping = true;
		private Vector2 _origPosition;
		private Vector2 _origTouchLocation;
		private const int MinimalDragDistance = 100;

        private Rectangle _reverseSpriteRectangle;
        public Rectangle ReverseSpriteRectangle
        {
            get { return _reverseSpriteRectangle; }
            set
            {
                _reverseSpriteRectangle = value;
                _reverseSprite.SpriteRectangle = _reverseSpriteRectangle;
            }
        }

		private bool _isSelected;
		public bool IsSelected
        { 
            get { return _isSelected; } 
            set
            {
                _isSelected = value;
                Sprite.Tint = _isSelected ? Color.Gray : Color.White;
            }
        }

        public bool IsFaceUp { get { return Sprite.IsVisible; } }

        public CardButton(GameComponent parent)
            : base(parent)
        {
            Init();
        }

        public CardButton(GameComponent parent, Sprite sprite)
            : base(parent, sprite)
        {
            Init();
        }

        private void Init()
        {
            _reverseSprite = new Sprite(this, Game.ReverseTexture, _reverseSpriteRectangle) { Name = "Backsprite", Position = Position, Scale = Game.CardScaleFactor };
            _reverseSprite.Hide();
        }

        public override int ZIndex
        {
            get { return base.ZIndex; }
            set
            {
                base.ZIndex = value;
                _reverseSprite.ZIndex = value;
            }
        }

        public override Vector2 Position
        {
            get { return base.Position; }
            set
            {
                base.Position = value;
                _reverseSprite.Position = value;
            }
        }

		public override AnchorType Anchor
		{
			get { return base.Anchor; }
			set
			{
				base.Anchor = value;
				_reverseSprite.Anchor = value;
			}
		}

		public override void Show()
        {
            ShowFrontSide();
        }
                   
        public override void Hide()
        {
            _reverseSprite.Hide();
            Sprite.Hide();
            base.Hide();
        }

        public void ShowFrontSide()
        {
            base.Show();
            Sprite.Show();
            _reverseSprite.Hide();
        }

        public void ShowBackSide()
        {
            base.Show();
            _reverseSprite.Show();
            Sprite.Hide();
        }

        public CardButton FlipToFront(float speed = 4f)
        {
            var slim = new Vector2
            {
                X = 0,
                Y = Game.CardScaleFactor.Y
            };
            this.Invoke(() =>
                {
                    Sprite
                    .WaitUntil(() => _doneFlipping)
                    .Invoke(() =>
                            {
                                _doneFlipping = false;
                                Sprite.Hide();
                                _reverseSprite.Show();
                                _reverseSprite
                                .ScaleTo(slim, speed)
                                .Invoke(() => _doneFlipping = true);
                            })
                    .WaitUntil(() => _doneFlipping)
                    .Invoke(() =>
                            {
                                _doneFlipping = false;
                                _reverseSprite.Hide();
                                Sprite.Scale = slim;
                                Sprite.Show();
                            })
                    .ScaleTo(Game.CardScaleFactor.X, speed)
                    .Invoke(() =>
                            {
                                _reverseSprite.Scale = Game.CardScaleFactor;//Vector2.One;
                                _doneFlipping = true;
                            });
                })
            .WaitUntil(() => _doneFlipping);
            return this;
        }

        public CardButton FlipToBack(float speed = 4f)
        {
            var slim = new Vector2
                {
                    X = 0,
                    Y = Game.CardScaleFactor.Y
                };
            this.Invoke(() =>
                {
                    Sprite
                    .WaitUntil(() => _doneFlipping)
                    .Invoke(() =>
                        {
                            _doneFlipping = false;
                            _reverseSprite.Hide();
                            Sprite.Show();
                        })
                    .ScaleTo(slim, speed)
                    .Invoke(() =>
                        {
                            Sprite.Hide();
                            _reverseSprite.Scale = slim;
                            _reverseSprite.Show();
                            _reverseSprite
                                .ScaleTo(Game.CardScaleFactor.X, speed)
                                .Invoke(() =>
                                {
                                    Sprite.Scale = Game.CardScaleFactor;//Vector2.One;
                                    _doneFlipping = true;
                                });
                        });
                })
            .WaitUntil(() => _doneFlipping);
            return this;
        }

        public override SpriteButton MoveTo(Vector2 targetPosition, float speed = 100f)
        {
            base.MoveTo(targetPosition, speed);
            _reverseSprite.MoveTo(targetPosition, speed);

            return this;
        }

        public override SpriteButton RotateTo(float targetAngle, float rotationSpeed = 1f)
        {
            base.RotateTo(targetAngle, rotationSpeed);
            _reverseSprite.RotateTo(targetAngle, rotationSpeed);

            return this;
        }

        public override SpriteButton ScaleTo(float targetScale, float scalingSpeed = 1f)
        {
            base.ScaleTo(targetScale, scalingSpeed);
            _reverseSprite.ScaleTo(targetScale, scalingSpeed);

            return this;
        }

        public override SpriteButton Slerp(Vector2 targetPosition, float targetAngle, float targetScale, float speed = 100f, float rotationSpeed = 1f, float scalingSpeed = 1f)
        {
            base.Slerp(targetPosition, targetAngle, targetScale, speed, rotationSpeed, scalingSpeed);
            _reverseSprite.Slerp(targetPosition, targetAngle, targetScale, speed, rotationSpeed, scalingSpeed);

            return this;
        }

		public override bool CollidesWithPosition(Vector2 position)
		{
            var polygon = Sprite.SpriteRectangle.Rotate(Sprite.RotationOrigin, Sprite.RotationAngle);

			for (var i = 0; i < polygon.Length; i++)
			{
				polygon[i] = Vector2.Subtract(polygon[i], Sprite.RotationOrigin);
                polygon[i] = polygon[i].Scale(new Vector2(0.5f, 1f));   //aby neslo klikat na okraje karty, kde jsou prekryvy
				polygon[i] = Vector2.Add(polygon[i], Position);
			}

			return position.IsPointInPolygon(polygon);
		}

		public override bool IsBusy { get { return Sprite.IsBusy; } }

        protected override void OnTouchDown(TouchLocation tl)
        {
			_origPosition = Position;
			_origTouchLocation = tl.Position;
            System.Diagnostics.Debug.WriteLine(string.Format("{0}: DOWN state: {1} id: {2} position: {3}", Name, tl.State, tl.Id, tl.Position));
        }

        protected override bool OnTouchHeld(float touchHeldTimeMs, TouchLocation tl)
        {
			System.Diagnostics.Debug.WriteLine(string.Format("{0}: HELD state: {1} id: {2} position: {3}", Name, tl.State, tl.Id, tl.Position));
			if (CanDrag)
			{
				Position = new Vector2(_origPosition.X + tl.Position.X - _origTouchLocation.X, _origPosition.Y + tl.Position.Y - _origTouchLocation.Y);
			}
            
			return !CanDrag;
        }

		protected override void OnTouchUp(TouchLocation tl)
		{
			System.Diagnostics.Debug.WriteLine(string.Format("{0}: UP state: {1} id: {2} position: {3}", Name, tl.State, tl.Id, tl.Position));
			if (CanDrag)
			{
				if (Vector2.Distance(tl.Position, _origTouchLocation) >= MinimalDragDistance)
                {
                    OnDragEnd(new DragEndEventArgs
                    {
                        DragStartLocation = _origTouchLocation,
                        DragEndLocation = tl.Position
                    });
				}
				else
				{
					Position = _origPosition;
				}
			}
		}

        /// <summary>
        /// Allows the game component to update itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        //public override void Update(GameTime gameTime)
        //{
        //    // TODO: Add your update code here
        //    _reverseSprite.Update(gameTime);
        //    base.Update(gameTime);
        //}
    }
}

