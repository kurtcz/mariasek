using Mariasek.Engine.New;
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
		public int MinimalDragDistance { get; set; } = 100;

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

        public Vector2 PreDragPosition { get { return _origPosition; } }
        public Vector2 PostDragPosition { get; private set; }

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

        public Vector2 Scale
        {
            get { return Sprite.Scale; }
            set
            {
                Sprite.Scale = value;
                _reverseSprite.Scale = value;
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
                                _reverseSprite.Scale = Game.CardScaleFactor;
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
                                    Sprite.Scale = Game.CardScaleFactor;
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
                polygon[i] = polygon[i].Scale(new Vector2(0.5f * Sprite.Scale.X, 1f * Sprite.Scale.Y));   //aby neslo klikat na okraje karty, kde jsou prekryvy
				polygon[i] = Vector2.Add(polygon[i], Position);
			}

            //var r = Sprite.SpriteRectangle;

            //System.Diagnostics.Debug.WriteLine($"{Name}: position at {position.X}:{position.Y} collides with {Name} BoundsRect {r.Left}:{r.Top}-{r.Right}:{r.Bottom}");

            return position.IsPointInPolygon(polygon);
		}

		public override bool IsBusy { get { return Sprite.IsBusy || _reverseSprite.IsBusy; } }
        public override bool IsMoving { get { return Sprite.IsMoving || _reverseSprite.IsMoving; } }

        private int _preTouchZIndex;

        protected override void OnTouchDown(TouchLocation tl)
        {
            base.OnTouchDown(tl);

            _origPosition = Position;
			_origTouchLocation = tl.Position;
            PostDragPosition = Position;
            _preTouchZIndex = ZIndex;
            ZIndex = 100;
            System.Diagnostics.Debug.WriteLine(string.Format("{0}: DOWN state: {1} id: {2} position: {3} z-index: {4}", Name, tl.State, tl.Id, tl.Position, ZIndex));
        }

        protected override bool OnTouchHeld(float touchHeldTimeMs, TouchLocation tl)
        {
            base.OnTouchHeld(touchHeldTimeMs, tl);

            System.Diagnostics.Debug.WriteLine(string.Format("{0}: HELD state: {1} id: {2} position: {3} z-index: {4}", Name, tl.State, tl.Id, tl.Position, ZIndex));
			if (CanDrag)
			{
				Position = new Vector2(_origPosition.X + tl.Position.X - _origTouchLocation.X, _origPosition.Y + tl.Position.Y - _origTouchLocation.Y);
			}
            
			return !CanDrag;
        }

		protected override void OnTouchUp(TouchLocation tl)
		{
            base.OnTouchUp(tl);

            ZIndex = _preTouchZIndex;
            System.Diagnostics.Debug.WriteLine(string.Format("{0}: UP state: {1} id: {2} position: {3} z-index: {4}", Name, tl.State, tl.Id, tl.Position, ZIndex));
            if (CanDrag)
			{
				if (Vector2.Distance(tl.Position, _origTouchLocation) >= MinimalDragDistance)
                {
                    PostDragPosition = tl.Position;
                    OnDragEnd(new DragEndEventArgs
                    {
                        DragStartLocation = _origTouchLocation,
                        DragEndLocation = tl.Position
                    });
				}
				else
				{
					Position = _origPosition;
                    PostDragPosition = _origPosition;
				}
			}
		}

        public new void TouchUp(TouchLocation tl)
        {
            OnTouchUp(tl);
        }

        public override string ToString()
        {
            if (Tag == null)
            {
                return base.ToString();
            }

            return string.Format("CardButton: {0} @ {1}:{2}", Tag, Position.X, Position.Y);
        }
    }
}

