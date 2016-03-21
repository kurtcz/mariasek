﻿using System;
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
    public class CardButton : SpriteButton
    {
        private Sprite _reverseSprite;
        private bool _doneFlipping = true;

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
            _reverseSprite = new Sprite(this, Game.ReverseTexture) { Name = "Backsprite", Position = Position };
            _reverseSprite.Hide();
        }

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
                Y = 1
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
                    .ScaleTo(Vector2.One, speed)
                    .Invoke(() =>
                            {
                                _reverseSprite.Scale = Vector2.One;
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
                    Y = 1
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
                                .ScaleTo(Vector2.One, speed)
                                .Invoke(() =>
                                {
                                    Sprite.Scale = Vector2.One;
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

        protected override void OnTouchDown(TouchLocation tl)
        {
            base.OnTouchDown(tl);
        }

        protected override void OnTouchUp(TouchLocation tl)
        {
            base.OnTouchUp(tl);
        }

        protected override void OnClick()
        {
            IsSelected = !IsSelected;
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

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
            _reverseSprite.Draw(gameTime);
        }
    }
}

