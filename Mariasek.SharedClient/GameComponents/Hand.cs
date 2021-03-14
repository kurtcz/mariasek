using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework;

using Mariasek.Engine;

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
                foreach (var cardButton in _cardButtons)
                {
                    if (cardButton != null)
                    {
                        cardButton.Position = _centre;
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

                foreach(var cardButton in _cardButtons.Where(i => i != null && i.IsVisible))
                {
                    if (cardButton.Sprite.Position.X - CardWidth * Game.CardScaleFactor.X / 2f < left)
                    {
                        left = (int)(cardButton.Sprite.Position.X - CardWidth * Game.CardScaleFactor.X / 2f);
                    }
					if (cardButton.Sprite.Position.Y - CardHeight * Game.CardScaleFactor.Y / 2f < top)
					{
                        top = (int)(cardButton.Sprite.Position.Y - CardHeight * Game.CardScaleFactor.Y / 2f);
					}
                    if (cardButton.Sprite.Position.X + CardWidth * Game.CardScaleFactor.X / 2f > right)
					{
                        right = (int)(cardButton.Sprite.Position.X + CardWidth * Game.CardScaleFactor.X / 2f);
					}
					if (cardButton.Sprite.Position.Y + CardHeight * Game.CardScaleFactor.Y / 2f > bottom)
					{
                        bottom = (int)(cardButton.Sprite.Position.Y + CardHeight * Game.CardScaleFactor.Y / 2f);
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

        public Vector2 Scale
        {
            get
            {
                return _cardButtons.Where(i => i != null)
                                   .Select(i => i.Scale)
                                   .FirstOrDefault();
            }
            set
            {
                foreach(var card in _cardButtons.Where(i => i != null))
                {
                    card.Scale = value;
                }
            }
        }

		/// <summary>
		/// When this event is set then there are no animations going on.
		/// </summary>
		public ManualResetEventSlim AnimationEvent { get; private set; }
        public Card Card { get; set; }
        public bool IsStraight { get; private set; }
        public override bool IsMoving { get { return _cardButtons.Where(i => i != null).Any(i => i.IsVisible && i.IsMoving); }  }
        public bool SpritesBusy { get { return _cardButtons.Where(i => i != null).Any(i => i.IsBusy || i.IsMoving); } }
        public bool AllCardsFaceUp { get { return _cardButtons.Where(i => i != null && i.IsVisible).All(i => i.IsFaceUp); } }

        private Vector2 _centre;
        public Card CardToHide { get; private set; }
        public int CardsVisible { get { return _cardButtons.Count(i => i != null && i.IsVisible); } }
        public int CardsNotRevealed { get; private set; }
        private bool _handChanged;
        private CardButton[] _cardButtons = new CardButton[12];
        public CardButton CardButtonForCard(Card card)
        {
            return _cardButtons.FirstOrDefault(i => (Card)i.Tag == card);
        }
        //private RectangleShape rs;
        private const int ZIndexBase = 80;
        public const int CardWidth = 164;
        public const int CardHeight = 272;
        public delegate void ClickEventHandler(object sender);
        public event ClickEventHandler Click;

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

        public void UpdateHand(IList<Card> hand, int cardsNotRevealed = 0, Card cardToHide = null, bool flipCardsIfNeeded = true)
        {
            if (hand == null || !hand.Any())
            {
                base.Hide();
                for (var i = 0; i < _cardButtons.Length; i++)
                {
                    if (_cardButtons[i] != null)
                    {
                        _cardButtons[i].Position = Centre;
                    }
                }
                CardsNotRevealed = 0;
                CardToHide = null;
                return;
            }
            base.Show();
            var cardStrings = new StringBuilder();

            //AnimationEvent.Reset();
            foreach (var c in hand)
            {
                cardStrings.AppendFormat("{0} ", c);
            }
            System.Diagnostics.Debug.WriteLine(string.Format("UpdateHand() {0}", cardStrings.ToString()));
            if (cardToHide != CardToHide || cardsNotRevealed != CardsNotRevealed)
            {
                _handChanged = true;
                CardsNotRevealed = cardsNotRevealed;
                CardToHide = cardToHide;
            }
            for (var i = 0; i < _cardButtons.Length; i++)
            {
                if (i >= hand.Count())
                {
                    if (_cardButtons[i] != null)
                    {
                        if (!_cardButtons[i].IsVisible)
                        {
                            _handChanged = true;
                        }
                        _cardButtons[i].Hide();
                    }
                    continue;
                }

                var rect = hand[i].ToTextureRect();
                var refreshSprites = _cardButtons[i] == null;
                if (refreshSprites)
                {
                    _handChanged = true;
                    _cardButtons[i] = new CardButton(this, new Sprite(this, Game.CardTextures, rect) { Name = string.Format("HandSprite{0}", i + 1), Scale = Game.CardScaleFactor })
                    { Name = string.Format("HandButton{0}", i + 1), ReverseSpriteRectangle = Game.BackSideRect, ZIndex = ZIndexBase + i };
                    _cardButtons[i].Click += CardClicked;
                    _cardButtons[i].DragEnd += CardDragged;
                    _cardButtons[i].Position = Centre;
                    _cardButtons[i].Show();
                    _cardButtons[i].ShowBackSide();
                }
                if ((Card)_cardButtons[i].Tag != hand[i])
                {
                    _handChanged = true;
                }
                _cardButtons[i].ZIndex = ZIndexBase + i;
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
                    if (flipCardsIfNeeded)
                    {
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
                    }
                    _cardButtons[i].IsSelected = false;
                }
            }
        }

        public override void Show()
        {
            base.Show();
            foreach (var cardButton in _cardButtons.Where(i => i != null))
            {
                cardButton.Show();
            }
        }

        public override void Hide()
        {
            base.Hide();
            foreach (var cardButton in _cardButtons.Where(i => i != null))
            {
                cardButton.Hide();
            }
            CardsNotRevealed = 0;
            CardToHide = null;
        }

        public override bool IsEnabled
        {
            get { return base.IsEnabled; }
            set
            {
                base.IsEnabled = value;
                foreach (var cardButton in _cardButtons.Where(i => i != null))
                {
                    cardButton.IsEnabled = value;
                }
            }
        }

		private void CardClicked(object sender)
        {
            Card = (Card)(sender as CardButton).Tag;
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
			var cardButton = _cardButtons.FirstOrDefault(i => ((Card)i.Tag == card));

			if (cardButton != null)
			{
				cardButton.IsSelected = true;
			}
		}


        public void DeselectAllCards()
        {
            foreach(var cardButton in _cardButtons.Where(i => i != null))
            {
                cardButton.IsSelected = false;
            }
        }

		public void AllowDragging()
		{
			foreach (var cardButton in _cardButtons.Where(i => i != null))
			{
				cardButton.CanDrag = true;
			}
		}

		public void ForbidDragging()
		{
			foreach (var cardButton in _cardButtons.Where(i => i != null))
			{
				cardButton.CanDrag = false;
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

                hh[i].Slerp(targetPosition, targetAngle, Game.CardScaleFactor.X, 300, 2f, 1f);
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

            if (Game.Settings.DirectionOfPlay == DirectionOfPlay.Counterclockwise)
            {
                if (playerIndex == 1)
                {
                    playerIndex = 2;
                }
                else if (playerIndex == 2)
                {
                    playerIndex = 1;
                }
            }
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
                _cardButtons[i].ClearOperations();
                _cardButtons[i].Position = initialPosition;
                _cardButtons[i].MoveTo(targetPosition, 200f);
            }
            AnimationEvent.Reset();
        }

        public void SortHand(List<Card> unsorted)
        {
            var positions = _cardButtons.Where(i => i != null)
                                        .Select(i => i.Position)
                                        .ToList();

            //dat karty na puvodni misto. Indexy jsou setridene, ale pozice ne
            foreach (var cardButton in _cardButtons.Where(i => i != null))
            {
                var n = unsorted.IndexOf(cardButton.Tag as Card);

                if (n < 0)
                {
                    continue;
                }
                cardButton.Position = positions[n];
            }
            //zde se v animaci karty setridi na sva mista podle svych indexu
            ShowStraight((int)Game.VirtualScreenWidth - 20);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
            if (!IsMoving && !SpritesBusy && !AnimationEvent.IsSet)
            {
                AnimationEvent.Set();
            }
        }
    }
}

