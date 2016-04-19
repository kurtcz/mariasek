using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
//using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

using Mariasek.Engine.New;

namespace Mariasek.SharedClient.GameComponents
{
    public class Hand : GameComponent
    {
        public Vector2 Centre
        {
            get { return _centre; }
            set
            {
                _centre = value;
                foreach (var sprite in _sprites)
                {
                    if (sprite != null)
                    {
                        sprite.Position = _centre;
                    }
                }
            }
        }
        /// <summary>
        /// When this event is set then there are no animations going on.
        /// </summary>
        public ManualResetEventSlim AnimationEvent { get; private set; }
        public Card Card { get; set; }
        public bool IsStraight { get; private set; }
        public override bool IsMoving { get { return _sprites.Any(i => i != null && i.IsVisible && i.IsMoving); }  }
        public bool SpritesBusy { get { return _sprites.Any(i => i != null && i.IsBusy); } }

        private Vector2 _centre;

        private CardButton[] _sprites = new CardButton[12];
        //private Sprite[] _sprites = new Sprite[12];

        const int CardWidth = 65;
        const int CardHeight = 112;

        public delegate void ClickEventHandler(object sender);
        public event ClickEventHandler Click;

        public Hand(GameComponent parent, ContentManager content)
            : base(parent)
        {
            for(var i = 0; i < _sprites.Length; i++)
            {
                var rect = new Rectangle(4 + (i % 8) * 74, 5 + (i / 8) * 120, CardWidth, CardHeight);

                _sprites[i] = new CardButton(this, new Sprite(this, Game.CardTextures, rect) { Name = string.Format("HandSprite{0}", i+1) });
                //_sprites[i] = new Sprite(this, Game.CardTextures, rect);
                _sprites[i].Click += CardClicked;
                _sprites[i].Name = string.Format("HandButton{0}", i + 1);
            }
            AnimationEvent = new ManualResetEventSlim(true);
        }

        public Hand(GameComponent parent, Card[] hand)
            : base(parent)
        {
            AnimationEvent = new ManualResetEventSlim();
            UpdateHand(hand);
        }

        public void UpdateHand(Card[] hand, int cardsNotRevealed = 0, Card cardToHide = null)
        {
            var cardStrings = new StringBuilder();

            foreach (var c in hand)
            {
                cardStrings.AppendFormat("{0} ", c);
            }
            System.Diagnostics.Debug.WriteLine(string.Format("UpdateHand() {0}", cardStrings.ToString()));
            /*for(var i = 0; i < _sprites.Length; i++)
            {
                if (_sprites[i] != null)
                {
                    _sprites[i].Hide();
                    _sprites[i].Parent = null;
                }
                if (i < hand.Length)
                {
                    var rect = hand[i].ToTextureRect();

                    _sprites[i] = new CardButton(this, new Sprite(this, Game.CardTextures, rect) { Tag = hand[i], Name = string.Format("HandSprite{0}", i+1) })
                        { Tag = hand[i], Name = string.Format("HandButton{0}", i+1) };
                    //_sprites[i] = new Sprite(this, Game.CardTextures, rect) { Tag = hand[i] };
                    _sprites[i].Position = Centre;
                    _sprites[i].Click += CardClicked;
                }
                else
                {
                    _sprites[i] = null;
                }
            }*/

            //Hide cards that were played
//            for(var i = 0; i < _sprites.Length; i++)
//            {
//                if (_sprites[i] != null)
//                {
//                    _sprites[i].IsSelected = false;
//                    if (!hand.Contains(_sprites[i].Tag) || _sprites[i].Tag == cardToHide)
//                    {
//                        _sprites[i].Hide();
//                    }
//                }
//            }

            for (var i = 0; i < _sprites.Length; i++)
            {
                if (i >= hand.Count())
                {
                    if (_sprites[i] != null)
                    {
                        _sprites[i].Hide();
                    }
                    continue;
                }

                var rect = hand[i].ToTextureRect();

                if (_sprites[i] == null)
                {
                    _sprites[i] = new CardButton(this, new Sprite(this, Game.CardTextures) { Name = string.Format("HandSprite{0}", i + 1) })
                    { Name = string.Format("HandButton{0}", i + 1) };
                    _sprites[i].Click += CardClicked;
                    _sprites[i].Position = Centre;
                }
                _sprites[i].Sprite.SpriteRectangle = rect;
                _sprites[i].Tag = hand[i];
                _sprites[i].Sprite.Tag = hand[i];
                if((Card)_sprites[i].Tag == cardToHide)
                {
                    _sprites[i].Hide();
                }
                else
                {
                    if (i < hand.Count() - cardsNotRevealed)
                    {
                        if (!_sprites[i].IsVisible)
                        {
                            _sprites[i].Show();
                        }
                        else if (!_sprites[i].IsFaceUp)
                        {
                            _sprites[i].FlipToFront();
                        }
                    }
                    else
                    {
                        _sprites[i].ShowBackSide();
                    }
                    _sprites[i].IsSelected = false;
                }
            }
        }

        public override void Show()
        {
            base.Show();
            foreach (var sprite in _sprites.Where(i => i != null))
            {
                sprite.Show();
            }
        }

        public override void Hide()
        {
            base.Hide();
            foreach (var sprite in _sprites.Where(i => i != null))
            {
                sprite.Hide();
            }
        }

        public override bool IsEnabled
        {
            get { return base.IsEnabled; }
            set
            {
                base.IsEnabled = value;
                foreach (var sprite in _sprites.Where(i => i != null))
                {
                    sprite.IsEnabled = value;
                }
            }
        }

        private void CardClicked(object sender)
        {
            Card = (Card)(sender as SpriteButton).Tag;
            if (Click != null)
            {
                Click(sender);
            }
        }

        public void ShowArc(float arcAngle)
        {
            var hh = _sprites.Where(i => i != null && i.IsVisible).ToList();
            float angle0 = (float)Math.PI / 2;
            var r = 300f;

            var cardsToSkip = (12 - hh.Count) / 2;
            for(var i = 0; i < hh.Count; i++)
            {
                var angle = angle0 - arcAngle / 12 * (i /*+ cardsToSkip*/ - hh.Count()/2.0f/* + 0.5f*/);
                float rotationAngle = -angle + (float)Math.PI / 2;
                //if(rotationAngle < 0)
                //    rotationAngle += (float)Math.PI * 2;
                var targetPosition = new Vector2(Centre.X + r * (float)Math.Cos(angle),
                                                 Centre.Y + r * 0.75f - r * (float)Math.Sin(angle));

                //hh[i].MoveTo(targetPosition, 400);
                //hh[i].RotateTo(rotationAngle, 2);
                hh[i].Slerp(targetPosition, rotationAngle, 1f, 400, 2f, 1f);
            }
            IsStraight = false;
            AnimationEvent.Reset();
        }

        public void ShowStraight(int width)
        {
            var hh = _sprites.Where(i => i != null && i.IsVisible).ToList();
            var padding = 10;
            /* This code shows cards in up to two rows */
/*
            var cardsPerRow = 8;
            for(var i = 0; i < hh.Count; i++)
            {
                var rowItems = hh.Count <= cardsPerRow || i < cardsPerRow
                    ? cardsPerRow
                    : hh.Count - cardsPerRow;
                var x_offset = ((i % cardsPerRow) - rowItems/2.0f + 0.5f) * (CardWidth + padding);
                var y_offset = hh.Count > cardsPerRow
                    ? ((i / cardsPerRow) - 1) * (CardHeight + padding)
                    : 0;

                var targetPosition = new Vector2(Centre.X + x_offset, Centre.Y + y_offset);
                var targetAngle = 0f;

                hh[i].MoveTo(targetPosition, 400);
                hh[i].RotateTo(targetAngle, 2);
            }
*/
            /* This code shows cards in one row */
            float rowWidth = CardWidth * hh.Count + padding * (hh.Count - 1);

            if (rowWidth > width - CardWidth)
            {
                rowWidth = width - CardWidth;
            }
            for (var i = 0; i < hh.Count; i++)
            {
                var x_offset = hh.Count > 1 
                                ? -rowWidth / 2f + i * rowWidth / (hh.Count - 1)
                                : -rowWidth / 2f;
                var y_offset = 0;
                var targetPosition = new Vector2(Centre.X + x_offset, Centre.Y + y_offset);
                var targetAngle = 0f;

                //hh[i].MoveTo(targetPosition, 400);
                //hh[i].RotateTo(targetAngle, 2);
                hh[i].Slerp(targetPosition, targetAngle, 1f, 400, 2f, 1f);
            }
            IsStraight = true;

            AnimationEvent.Reset();
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsMoving)
            {
                AnimationEvent.Set();
            }
            base.Update(gameTime);
        }
    }
}

