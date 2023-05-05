using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Mariasek.Engine;
using Microsoft.Xna.Framework.Graphics;

namespace Mariasek.SharedClient.GameComponents
{
    public class ProbabilityBox : ScrollBox
    {
        private Mariasek.Engine.Game _g;
        private RectangleShape _background;
        private LeftRightSelector _playerBtn;
        private List<Mariasek.Engine.Card> _certainCards;
        private List<Mariasek.Engine.Card> _potentialCards;
        public Sprite[] CertainCards;
        public Sprite[] PotentialCards;
        public Label CertainCardsLabel;
        public Label PotentialCardsLabel;
        private Color _backgroundColor;
        private bool _positionSet;
        public Color BackgroundColor
        {
            get { return _backgroundColor; }
            set
            {
                _backgroundColor = value;
                _background.BorderColors = new List<Color> { value };
                _background.BackgroundColors = new List<Color> { value };
            }
        }

        public override Vector2 Position
        {
            get
            {
                return base.Position;
            }
            set
            {
                var positionDiff = value - base.Position;

                base.Position = value;
                _background.Position = value;
                if (_positionSet)
                {
                    if (CertainCards != null)
                    {
                        for (var i = 0; i < CertainCards.Length; i++)
                        {
                            if (CertainCards[i] == null)
                            {
                                continue;
                            }
                            CertainCards[i].Position += positionDiff;
                        }
                    }
                    if (PotentialCards != null)
                    {
                        for (var i = 0; i < PotentialCards.Length; i++)
                        {
                            if (PotentialCards[i] == null)
                            {
                                continue;
                            }
                            PotentialCards[i].Position += positionDiff;
                        }
                    }
                    CertainCardsLabel.Position += positionDiff;
                    PotentialCardsLabel.Position += positionDiff;
                    _playerBtn.Position += positionDiff;
                }
                _positionSet = true;
            }
        }

        protected override Rectangle BoundsRect
        {
            get
            {
                return base.BoundsRect;
            }
            set
            {
                base.BoundsRect = value;
                _background.Width = value.Width;
                _background.Height = value.Height;
            }
        }

        //public override void Show()
        //{
        //    base.Show();
        //    for (var i = 0; i < Children.Count; i++)
        //    {
        //        Children[i].Show();
        //    }
        //}

        //public override void Hide()
        //{
        //    base.Hide();
        //    for (var i = 0; i < Children.Count; i++)
        //    {
        //        Children[i].Hide();
        //    }
        //}

        public ProbabilityBox(GameComponent parent)
            : base(parent)
        {
            const int headLength = Mariasek.Engine.Game.NumPlayers + 1;
            const int maxCards = 22;
            var roundsLength = Game.MainScene?.g?.CurrentRound?.number ?? 0;
            var reviewCardScaleFactor = new Vector2(0.42f, 0.42f);//Game.CardScaleFactor * 0.7f; //default card scale factor = 0.6f

            _background = new RectangleShape(this)
            {
                BackgroundColors = new List<Color> { BackgroundColor },
                BorderColors = new List<Color> { BackgroundColor },
                Opacity = 0.8f,
                Width = BoundsRect.Width,
                Height = BoundsRect.Height
            };
            CertainCards = new Sprite[maxCards];
            PotentialCards = new Sprite[maxCards];
            CertainCardsLabel = new Label(this)
                                {
                                    Width = 200,
                                    Height = 40,
                                    TextColor = Game.Settings.HighlightedTextColor,
                                    ZIndex = 100,
                                    UseCommonScissorRect = true
                                };
            PotentialCardsLabel = new Label(this)
            {
                Width = 200,
                Height = 40,
                TextColor = Game.Settings.HighlightedTextColor,
                ZIndex = 100,
                UseCommonScissorRect = true
            };
            for (var i = 0; i < CertainCards.Length; i++)
            {
                CertainCards[i] = new Sprite(this, Game.CardTextures)//, rect)
                {
                    ZIndex = 90 + i,
                    Scale = reviewCardScaleFactor
                };
            }
            for (var i = 0; i < PotentialCards.Length; i++)
            {
                PotentialCards[i] = new Sprite(this, Game.CardTextures)//, rect)
                {
                    ZIndex = 90 + i,
                    Scale = reviewCardScaleFactor
                };
            }
            _playerBtn = new LeftRightSelector(this)
            {
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { Game.Settings.PlayerNames[1], 1 }, { Game.Settings.PlayerNames[2], 2 }, { "Talon", 3 } },
                UseCommonScissorRect = true,
                ZIndex = 100
            };
            _playerBtn.SelectionChanged += PlayerBtnChanged;
            if (_playerBtn.SelectedIndex < 0)
            {
                _playerBtn.SelectedIndex = 0;
            }
        }

        private int _playerNumber = 1;
        public void UpdateControls(Mariasek.Engine.Game game)
        {
            const int talonIndex = 3;
            int playerNumber = _playerNumber;
            var reviewCardScaleFactor = new Vector2(0.42f, 0.42f);
            var nextPlayer = (_playerNumber + 1) % (Mariasek.Engine.Game.NumPlayers + 1);

            if (nextPlayer == 0)
            {
                nextPlayer++;
            }

            _g = game;
            if (_g == null)
            {
                for(var j = 0; j < CertainCards.Length; j++)
                {
                    CertainCards[j].Hide();
                }
                for (var j = 0; j < PotentialCards.Length; j++)
                {
                    PotentialCards[j].Hide();
                }
                return;
            }
            _certainCards = new List<Mariasek.Engine.Card>((game.players[0] as IStatsPlayer).Probabilities.CertainCards(playerNumber));
            _potentialCards = new List<Mariasek.Engine.Card>((game.players[0] as IStatsPlayer).Probabilities.PotentialCards(playerNumber).Where(i => !_certainCards.Contains(i)));

            var xDelta = Math.Min(Hand.CardWidth * reviewCardScaleFactor.X,
                                  (Game.VirtualScreenWidth - Position.X - Hand.CardWidth * reviewCardScaleFactor.X - 20) / Math.Max(_certainCards.Count - 1, 1));

            for (var j = 0; j < _certainCards.Count; j++)
            {
                var rect = _certainCards[j].ToTextureRect();
                CertainCards[j].Texture = Game.CardTextures;
                CertainCards[j].SpriteRectangle = rect;
                CertainCards[j].Tag = _certainCards[j];
                CertainCards[j].ZIndex = 70 + j;
                CertainCards[j].Show();
                CertainCards[j].Position = new Vector2(Position.X + 10 + 0.5f * Hand.CardWidth * reviewCardScaleFactor.X + j * xDelta,
                                                       Position.Y + 50 + 0.5f * Hand.CardHeight * reviewCardScaleFactor.Y);
            }
            for (var j = _certainCards.Count; j < CertainCards.Length; j++)
            {
                CertainCards[j].Hide();
            }

            xDelta = Math.Min(Hand.CardWidth * reviewCardScaleFactor.X,
                              (Game.VirtualScreenWidth - Position.X - Hand.CardWidth * reviewCardScaleFactor.X - 20) / Math.Max(_potentialCards.Count - 1, 1));

            for (var j = 0; j < _potentialCards.Count; j++)
            {
                var rect = _potentialCards[j].ToTextureRect();
                PotentialCards[j].Texture = Game.CardTextures;
                PotentialCards[j].SpriteRectangle = rect;
                PotentialCards[j].Tag = _potentialCards[j];
                PotentialCards[j].ZIndex = 70 + j;
                PotentialCards[j].Show();
                PotentialCards[j].Position = new Vector2(Position.X + 10 + 0.5f * Hand.CardWidth * reviewCardScaleFactor.X + j * xDelta,
                                                         Position.Y + 100 + 1.5f * Hand.CardHeight * reviewCardScaleFactor.Y);
            }
            for (var j = _potentialCards.Count; j < PotentialCards.Length; j++)
            {
                PotentialCards[j].Hide();
            }
            CertainCardsLabel.Text = playerNumber == talonIndex ? "V talonu je určitě" : $"{game.players[playerNumber].Name} má určitě";
            CertainCardsLabel.Position = new Vector2(Position.X + 10, Position.Y + 10);
            PotentialCardsLabel.Text = playerNumber == talonIndex ? "V talonu může být" : $"{game.players[playerNumber].Name} může mít";
            PotentialCardsLabel.Position = new Vector2(Position.X + 10, Position.Y + 60 + Hand.CardHeight * reviewCardScaleFactor.Y);

            BoundsRect = new Rectangle(0, 0, (int)Game.VirtualScreenWidth - (int)Position.X,
                                       (int)(110 + 2 * Hand.CardHeight * reviewCardScaleFactor.Y));
            ScrollBarColor = Color.White;

            _playerBtn.Position = new Vector2(Position.X + Width - _playerBtn.Width - 10, Position.Y);
        }

        private void PlayerBtnChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            _playerNumber = (int)selector.SelectedValue;
            UpdateControls(_g);
        }
    }
}
