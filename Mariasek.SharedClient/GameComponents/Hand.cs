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

        private const int ZIndexBase = 50;
        public const int CardWidth = 65;
        public const int CardHeight = 112;

        public delegate void ClickEventHandler(object sender);
        public event ClickEventHandler Click;

        public Hand(GameComponent parent, ContentManager content)
            : base(parent)
        {
            for(var i = 0; i < _sprites.Length; i++)
            {
                var rect = new Rectangle(4 + (i % 8) * 74, 5 + (i / 8) * 120, CardWidth, CardHeight);

                _sprites[i] = new CardButton(this, new Sprite(this, Game.CardTextures, rect) { Name = string.Format("HandSprite{0}", i+1), Scale = Game.CardScaleFactor })
                { Name = string.Format("HandButton{0}", i + 1), ZIndex = ZIndexBase + i };
                _sprites[i].Click += CardClicked;
                _sprites[i].DragEnd += (sender, tl) => CardClicked(sender);
                _sprites[i].Name = string.Format("HandButton{0}", i + 1);
                _sprites[i].Position = Centre;
            }
            AnimationEvent = new ManualResetEventSlim(true);
        }

        public Hand(GameComponent parent, Card[] hand)
            : base(parent)
        {
            AnimationEvent = new ManualResetEventSlim();
            Centre = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight - 65);
            UpdateHand(hand);
        }

        public bool HighlightCard(Card cardToPlay)
        {
            if (_sprites == null || _sprites.All(i => i == null))
            {
                return false;
            }

            var sprite = _sprites.FirstOrDefault(i => (Card)i.Tag == cardToPlay);

            if (sprite != null)
            {
                //sprite.IsSelected = true;
                sprite.MoveTo(new Vector2(sprite.Sprite.Position.X, sprite.Sprite.Position.Y - 25), 50f);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void UpdateHand(IList<Card> hand, int cardsNotRevealed = 0, Card cardToHide = null)
        {
            var cardStrings = new StringBuilder();

            foreach (var c in hand)
            {
                cardStrings.AppendFormat("{0} ", c);
            }
            System.Diagnostics.Debug.WriteLine(string.Format("UpdateHand() {0}", cardStrings.ToString()));
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
                var refreshSprites = _sprites[i] == null;
                if (refreshSprites)
                {
                    _sprites[i] = new CardButton(this, new Sprite(this, Game.CardTextures) { Name = string.Format("HandSprite{0}", i + 1), Scale = Game.CardScaleFactor})
                    { Name = string.Format("HandButton{0}", i + 1), ZIndex = 50 + i };
                    _sprites[i].Click += CardClicked;
					_sprites[i].DragEnd += (sender, tl) => CardClicked(sender);
					_sprites[i].Position = Centre;
                }
                ZIndex = ZIndexBase + i;
                _sprites[i].Sprite.SpriteRectangle = rect;
                _sprites[i].Tag = hand[i];
                _sprites[i].Sprite.Tag = hand[i];
                if((Card)_sprites[i].Tag == cardToHide)
                {
                    _sprites[i].Hide();
                }
                else
                {
                    if (!_sprites[i].IsVisible)
                    {
                        _sprites[i].Show();
                    }
                    if (i < hand.Count() - cardsNotRevealed)
                    {
                        if (!_sprites[i].IsFaceUp)
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

		public void SelectCard(Card card)
		{
			var s = _sprites.FirstOrDefault(i => ((Card)i.Tag == card));

			if (s != null)
			{
				s.IsSelected = true;
			}
		}


        public void DeselectAllCards()
        {
            foreach(var sprite in _sprites.Where(i => i != null))
            {
                sprite.IsSelected = false;
            }
        }

		public void AllowDragging()
		{
			foreach (var sprite in _sprites.Where(i => i != null))
			{
				sprite.CanDrag = true;
			}
		}

		public void ForbidDragging()
		{
			foreach (var sprite in _sprites.Where(i => i != null))
			{
				sprite.CanDrag = false;
			}
		}

		public void ShowArc(float arcAngle)
        {
            var hh = _sprites.Where(i => i != null && i.IsVisible).ToList();
            float angle0 = (float)Math.PI / 2;
            var targetPosition = Centre;
            var r = 300f;

            var cardsToSkip = (12 - hh.Count) / 2;
            for(var i = 0; i < hh.Count; i++)
            {
                var angle = angle0 - arcAngle / 12 * (i /*+ cardsToSkip*/ - hh.Count()/2.0f/* + 0.5f*/);
                float rotationAngle = -angle + (float)Math.PI / 2;
                targetPosition = new Vector2(Centre.X + r * (float)Math.Cos(angle),
                                             Centre.Y + r * 0.75f - r * (float)Math.Sin(angle));

                hh[i].Slerp(targetPosition, rotationAngle, Game.CardScaleFactor.X, 400, 2f, 1f);
            }
            var hiddenCards = _sprites.Where(i => i != null && !i.IsVisible).ToList();
            for (var i = 0; i < hiddenCards.Count; i++)
            {
                hiddenCards[i].Position = targetPosition;
            }
            IsStraight = false;
            AnimationEvent.Reset();
        }

        public void ShowStraight(int width)
        {
            var hh = _sprites.Where(i => i != null && i.IsVisible).ToList();
            var padding = 10;
            /* This code shows cards in one row */
            float rowWidth = CardWidth * hh.Count + padding * (hh.Count - 1);
            var targetPosition = Centre;

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
                var targetAngle = 0f;
                targetPosition = new Vector2(Centre.X + x_offset, Centre.Y + y_offset);

                hh[i].Slerp(targetPosition, targetAngle, Game.CardScaleFactor.X, 400, 2f, 1f);
            }
            var hiddenCards = _sprites.Where(i => i != null && !i.IsVisible).ToList();
            for (var i = 0; i < hiddenCards.Count; i++)
            {
                hiddenCards[i].Position = targetPosition;
            }
            IsStraight = true;

            AnimationEvent.Reset();
        }

		public void ShowWinningHand(int playerIndex)
        {
            var initialPosition = Centre;
            var delta = Vector2.Zero;

            switch (playerIndex)
            {
                case 0:
                    initialPosition = new Vector2(100, Game.VirtualScreenHeight - CardHeight);
                    delta = new Vector2(CardWidth, -CardWidth / 4);
                    break;
                case 1:
                    initialPosition = new Vector2(100, 80);
                    delta = new Vector2(CardWidth, CardWidth / 4);
                    break;
                case 2:
                    initialPosition = new Vector2(Game.VirtualScreenWidth - 100, 80);
                    delta = new Vector2(-CardWidth, CardWidth / 4);
                    break;
            }

            var targetPosition = initialPosition;
            for (var i = 0; i < _sprites.Length && _sprites[i] != null; i++, targetPosition += delta)
            {
                _sprites[i].Position = initialPosition;
                _sprites[i].MoveTo(targetPosition, 200f);
            }
            AnimationEvent.Reset();
        }

        public void SortHand(List<Card> unsorted)
        {
            foreach (var sprite in _sprites.Where(i => i != null))
            {
                var n = unsorted.IndexOf(sprite.Tag as Card);

                if (n < 0)
                {
                    continue;
                }
                sprite.Position = _sprites[n].Position;
            }
            ShowStraight((int)Game.VirtualScreenWidth - 20);
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

