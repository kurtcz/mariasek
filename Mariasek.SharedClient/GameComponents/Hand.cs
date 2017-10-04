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
                foreach (var sprite in _cardButtons)
                {
                    if (sprite != null)
                    {
                        sprite.Position = _centre;
                    }
                }
            }
        }
        public Rectangle BoundsRect
        {
            get
            {
                var left = int.MaxValue;
                var top = int.MaxValue;
                var right = int.MinValue;
                var bottom = int.MinValue;

                foreach(var sprite in _cardButtons.Where(i => i != null && i.IsVisible))
                {
                    if (sprite.Sprite.Position.X - CardWidth * Game.CardScaleFactor.X / 2f < left)
                    {
                        left = (int)(sprite.Sprite.Position.X - CardWidth * Game.CardScaleFactor.X / 2f);
                    }
					if (sprite.Sprite.Position.Y - CardHeight * Game.CardScaleFactor.Y / 2f < top)
					{
                        top = (int)(sprite.Sprite.Position.Y - CardHeight * Game.CardScaleFactor.Y / 2f);
					}
                    if (sprite.Sprite.Position.X + CardWidth * Game.CardScaleFactor.X / 2f > right)
					{
                        right = (int)(sprite.Sprite.Position.X + CardWidth * Game.CardScaleFactor.X / 2f);
					}
					if (sprite.Sprite.Position.Y + CardHeight * Game.CardScaleFactor.Y / 2f > bottom)
					{
                        bottom = (int)(sprite.Sprite.Position.Y + CardHeight * Game.CardScaleFactor.Y / 2f);
					}
				}

				//rs = rs ?? new RectangleShape(this)
                //{
                //  Position = new Vector2(left, top),
                //  Width = right - left,
				//	Height = bottom - top,
				//	BackgroundColors = new List<Color> { Color.Transparent },
				//	BorderColors = new List<Color> { Color.Red },
				//	BorderThickness = 2,
				//	BorderRadius = 1,
				//	Opacity = 1,
				//	ZIndex = 1000
				//};

				return new Rectangle(left, top, right - left, bottom - top);
            }
        }

		/// <summary>
		/// When this event is set then there are no animations going on.
		/// </summary>
		public ManualResetEventSlim AnimationEvent { get; private set; }
        public Card Card { get; set; }
		public bool IsStraight { get; private set; }
        public override bool IsMoving { get { return _cardButtons.Any(i => i != null && i.IsVisible && i.IsMoving); }  }
        public bool SpritesBusy { get { return _cardButtons.Any(i => i != null && i.IsBusy); } }

        private Vector2 _centre;

        private CardButton[] _cardButtons = new CardButton[12];
        //private RectangleShape rs;
        private const int ZIndexBase = 50;
        public const int CardWidth = 164;
        public const int CardHeight = 272;
        public delegate void ClickEventHandler(object sender);
        public event ClickEventHandler Click;

        //public Hand(GameComponent parent, ContentManager content)
        //    : base(parent)
        //{
        //    for(var i = 0; i < _cardButtons.Length; i++)
        //    {
        //        var rect = new Rectangle(1 + (i % 8) * (CardWidth + 1), 2 + (i / 8) * (CardHeight + 1), CardWidth, CardHeight);

        //        _cardButtons[i] = new CardButton(this, new Sprite(this, Game.CardTextures, rect) { Name = string.Format("HandSprite{0}", i+1), Scale = Game.CardScaleFactor })
        //        { Name = string.Format("HandButton{0}", i + 1), ReverseSpriteRectangle = Game.BackSideRect, ZIndex = ZIndexBase + i };
        //        _cardButtons[i].Click += CardClicked;
        //        _cardButtons[i].DragEnd += CardDragged;
        //        _cardButtons[i].Name = string.Format("HandButton{0}", i + 1);
        //        _cardButtons[i].Position = Centre;
        //    }
        //    AnimationEvent = new ManualResetEventSlim(true);
        //}

        public Hand(GameComponent parent, Card[] hand)
            : base(parent)
        {
            AnimationEvent = new ManualResetEventSlim();
            Centre = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight - 85);
            UpdateHand(hand);
        }

        public bool HighlightCard(Card cardToPlay)
        {
            if (_cardButtons == null || _cardButtons.All(i => i == null))
            {
                return false;
            }

            var sprite = _cardButtons.FirstOrDefault(i => (Card)i.Tag == cardToPlay);

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
            for (var i = 0; i < _cardButtons.Length; i++)
            {
                if (i >= hand.Count())
                {
                    if (_cardButtons[i] != null)
                    {
                        _cardButtons[i].Hide();
                    }
                    continue;
                }

                var rect = hand[i].ToTextureRect();
                var refreshSprites = _cardButtons[i] == null;
                if (refreshSprites)
                {
                    _cardButtons[i] = new CardButton(this, new Sprite(this, Game.CardTextures) { Name = string.Format("HandSprite{0}", i + 1), Scale = Game.CardScaleFactor })
                    { Name = string.Format("HandButton{0}", i + 1), ReverseSpriteRectangle = Game.BackSideRect, ZIndex = 50 + i };
                    _cardButtons[i].Click += CardClicked;
					_cardButtons[i].DragEnd += CardDragged;
					_cardButtons[i].Position = Centre;
                }
                ZIndex = ZIndexBase + i;
                _cardButtons[i].Sprite.SpriteRectangle = rect;  //ReverseSpriteRect is updated for all CardButtons from MainScene.UpdateCardBackSides()
				_cardButtons[i].Tag = hand[i];
                _cardButtons[i].Sprite.Tag = hand[i];
                if((Card)_cardButtons[i].Tag == cardToHide)
                {
                    _cardButtons[i].Hide();
                }
                else
                {
                    if (!_cardButtons[i].IsVisible)
                    {
                        _cardButtons[i].Show();
                    }
                    if (i < hand.Count() - cardsNotRevealed)
                    {
                        if (!_cardButtons[i].IsFaceUp)
                        {
                            _cardButtons[i].FlipToFront();
                        }
                    }
                    else
                    {
                        _cardButtons[i].ShowBackSide();
                    }
                    _cardButtons[i].IsSelected = false;
                }
            }
        }

        public override void Show()
        {
            base.Show();
            foreach (var sprite in _cardButtons.Where(i => i != null))
            {
                sprite.Show();
            }
        }

        public override void Hide()
        {
            base.Hide();
            foreach (var sprite in _cardButtons.Where(i => i != null))
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
                foreach (var sprite in _cardButtons.Where(i => i != null))
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

        private void CardDragged(object sender, DragEndEventArgs e)
        {
            CardClicked(sender);

            //var cardButton = sender as CardButton;

            //if(cardButton != null)
            //{
            //    cardButton.Invoke(() => cardButton.Position = e.DragStartLocation);
            //}
        }

		public void SelectCard(Card card)
		{
			var s = _cardButtons.FirstOrDefault(i => ((Card)i.Tag == card));

			if (s != null)
			{
				s.IsSelected = true;
			}
		}


        public void DeselectAllCards()
        {
            foreach(var sprite in _cardButtons.Where(i => i != null))
            {
                sprite.IsSelected = false;
            }
        }

		public void AllowDragging()
		{
			foreach (var sprite in _cardButtons.Where(i => i != null))
			{
				sprite.CanDrag = true;
			}
		}

		public void ForbidDragging()
		{
			foreach (var sprite in _cardButtons.Where(i => i != null))
			{
				sprite.CanDrag = false;
			}
		}

		public void ShowArc(float arcAngle)
        {
            var hh = _cardButtons.Where(i => i != null && i.IsVisible).ToList();
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
            var hiddenCards = _cardButtons.Where(i => i != null && !i.IsVisible).ToList();
            for (var i = 0; i < hiddenCards.Count; i++)
            {
                hiddenCards[i].Position = targetPosition;
            }
            IsStraight = false;
            AnimationEvent.Reset();
        }

        public void ShowStraight(int width)
        {
            var hh = _cardButtons.Where(i => i != null && i.IsVisible).ToList();
            var padding = 0;//10;
            /* This code shows cards in one row */
            float rowWidth = CardWidth * Game.CardScaleFactor.X * hh.Count + padding * (hh.Count - 1);
            var targetPosition = Centre;

            if (rowWidth > width)
            {
                rowWidth = width;
            }
			var innerWidth = rowWidth - CardWidth * Game.CardScaleFactor.X;

			for (var i = 0; i < hh.Count; i++)
            {
                var x_offset = hh.Count > 1
                                 ? -innerWidth / 2f + i * innerWidth / (hh.Count - 1)
                                 : 0;
                var y_offset = 0;
                var targetAngle = 0f;
                targetPosition = new Vector2(Centre.X + x_offset, Centre.Y + y_offset);

                hh[i].Slerp(targetPosition, targetAngle, Game.CardScaleFactor.X, 400, 2f, 1f);
            }
            var hiddenCards = _cardButtons.Where(i => i != null && !i.IsVisible).ToList();
            //for (var i = 0; i < hiddenCards.Count; i++)
            //{
            //    hiddenCards[i].Position = targetPosition;
            //}
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
                    initialPosition = new Vector2(100, Game.VirtualScreenHeight - CardHeight * Game.CardScaleFactor.Y / 2f - 10);
                    delta = new Vector2(65, -65 / 4);
                    break;
                case 1:
                    initialPosition = new Vector2(100, 90);
                    delta = new Vector2(65, 65 / 4);
                    break;
                case 2:
                    initialPosition = new Vector2(Game.VirtualScreenWidth - 100, 90);
                    delta = new Vector2(-65, 65 / 4);
                    break;
            }

            var targetPosition = initialPosition;
            for (var i = 0; i < _cardButtons.Length && _cardButtons[i] != null; i++, targetPosition += delta)
            {
                _cardButtons[i].Position = initialPosition;
                _cardButtons[i].MoveTo(targetPosition, 200f);
            }
            AnimationEvent.Reset();
        }

        public void SortHand(List<Card> unsorted)
        {
            foreach (var sprite in _cardButtons.Where(i => i != null))
            {
                var n = unsorted.IndexOf(sprite.Tag as Card);

                if (n < 0)
                {
                    continue;
                }
                sprite.Position = _cardButtons[n].Position;
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

