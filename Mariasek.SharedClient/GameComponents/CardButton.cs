using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace Mariasek.SharedClient.GameComponents
{
    public class CardButton : SpriteButton
    {
        private Sprite _reverseSprite;
        private bool _doneFlipping = true;
		private Vector2 _origPosition;
		private Vector2 _origTouchLocation;
		private const int MinimalDragDistance = 100;

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
            _reverseSprite = new Sprite(this, Game.ReverseTexture) { Name = "Backsprite", Position = Position, Scale = Game.CardScaleFactor };
            _reverseSprite.Hide();
        }

		public bool CanDrag { get; set; }

        public override Vector2 Position
        {
            get { return base.Position; }
            set
            {
                base.Position = value;
                if (!Sprite.IsMoving)
                {
                    _reverseSprite.Position = value;
                }
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

        public CardButton FlipToFront(float speed = 2f)
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

        public CardButton FlipToBack(float speed = 2f)
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

        public CardButton MoveTo(Vector2 targetPosition, float speed = 100f)
        {
            base.MoveTo(targetPosition, speed);
            _reverseSprite.MoveTo(targetPosition, speed);

            return this;
        }

        public CardButton RotateTo(float targetAngle, float rotationSpeed = 1f)
        {
            base.RotateTo(targetAngle, rotationSpeed);
            _reverseSprite.RotateTo(targetAngle, rotationSpeed);

            return this;
        }

        public CardButton ScaleTo(float targetScale, float scalingSpeed = 1f)
        {
            base.ScaleTo(targetScale, scalingSpeed);
            _reverseSprite.ScaleTo(targetScale, scalingSpeed);

            return this;
        }

        public CardButton Slerp(Vector2 targetPosition, float targetAngle, float targetScale, float speed = 100f, float rotationSpeed = 1f, float scalingSpeed = 1f)
        {
            base.Slerp(targetPosition, targetAngle, targetScale, speed, rotationSpeed, scalingSpeed);
            _reverseSprite.Slerp(targetPosition, targetAngle, targetScale, speed, rotationSpeed, scalingSpeed);

            return this;
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
					OnClick();
				}
				else
				{
					Position = _origPosition;
				}
			}
		}

        protected override void OnClick()
        {
            base.OnClick();
        }

        /// <summary>
        /// Allows the game component to update itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public override void Update(GameTime gameTime)
        {
            // TODO: Add your update code here
            _reverseSprite.Update(gameTime);
            base.Update(gameTime);
        }
    }
}

