using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Mariasek.Engine.New;

namespace Mariasek.SharedClient.GameComponents
{
    public class GameReview : ScrollBox
    {
        private RectangleShape _background;
        private List<Mariasek.Engine.New.Card>[] _initialHands;
        public Sprite[][] Hands;
        public Sprite[][] Rounds;
        public Label[] Names;
        public Label[] RoundNum;
        public Label[][] Labels;
        public Color BackgroundColor;

        public override Vector2 Position
        {
            get
            {
                return base.Position;
            }
            set
            {
                base.Position = value;
                if (_background != null)
                {
                    _background.Position = value;
                }
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
                if (_background != null)
                {
                    _background.Width = value.Width;
                    _background.Height = value.Height;
                }
            }
        }

        public override void Show()
        {
            base.Show();
            for (var i = 0; i < Children.Count; i++)
            {
                Children[i].Show();
            }
        }

        public override void Hide()
        {
            base.Hide();
            for (var i = 0; i < Children.Count; i++)
            {
                Children[i].Hide();
            }
        }

        public GameReview(GameComponent parent)
            : base (parent)
        {
            const int headLength = Mariasek.Engine.New.Game.NumPlayers + 1;
            var roundsLength = Game.MainScene.g.CurrentRound != null ? Game.MainScene.g.CurrentRound.number : 0;

            _background = new RectangleShape(this)
            {
                BackgroundColors = new List<Color> { BackgroundColor },
                BorderColors = new List<Color> { BackgroundColor },
                Opacity = 0.5f
            };
            _initialHands = new List<Mariasek.Engine.New.Card>[Mariasek.Engine.New.Game.NumPlayers + 1];
            Hands = new Sprite[Mariasek.Engine.New.Game.NumPlayers+1][];
            Names = new Label[Mariasek.Engine.New.Game.NumPlayers + 1];
            RoundNum = new Label[Mariasek.Engine.New.Game.NumRounds];
            Rounds = new Sprite[Mariasek.Engine.New.Game.NumRounds][];
            Labels = new Label[Mariasek.Engine.New.Game.NumRounds][];

            for (var i = 0; i < Mariasek.Engine.New.Game.NumPlayers+1; i++)
            {
                _initialHands[i] = new List<Mariasek.Engine.New.Card>();
                if (i < Mariasek.Engine.New.Game.NumPlayers)
                {
                    _initialHands[i].AddRange(Game.MainScene.g.players[i].Hand);    //u betla a durcha mohly zustat karty v ruce
                }
                Names[i] = new Label(this)
                {
                    Position = new Vector2(200 - 0.5f * Hand.CardWidth, 100 + (i - 0.5f) * (Hand.CardHeight + 50) + 20),
                    Width = 200,
                    Height = 40,
                    TextColor = Color.Yellow,
                    ZIndex = 200,
                    UseCommonScissorRect = true
                };
            }
            for (var i = 0; i < Rounds.Length; i++)
            {
                if (Game.MainScene.g.rounds[i] == null || Game.MainScene.g.rounds[i].c3 == null)
                {
                    continue;
                }
                if (i > roundsLength)
                {
                    roundsLength = i;
                }
                RoundNum[i] = new Label(this)
                {
                    Position = new Vector2(200 - 0.5f * Hand.CardWidth, 100 + (i + 4 - 0.5f) * (Hand.CardHeight + 50) + 20),
                    Width = 200,
                    Height = 40,
                    Text = string.Format("{0}. kolo:", i + 1),
                    TextColor = Color.Yellow,
                    ZIndex = 200,
                    UseCommonScissorRect = true
                };
                Rounds[i] = new Sprite[Mariasek.Engine.New.Game.NumPlayers];
                Labels[i] = new Label[Mariasek.Engine.New.Game.NumPlayers];
                _initialHands[Game.MainScene.g.rounds[i].player1.PlayerIndex].Add(Game.MainScene.g.rounds[i].c1);
                _initialHands[Game.MainScene.g.rounds[i].player2.PlayerIndex].Add(Game.MainScene.g.rounds[i].c2);
                _initialHands[Game.MainScene.g.rounds[i].player3.PlayerIndex].Add(Game.MainScene.g.rounds[i].c3);

                var rect = Game.MainScene.g.rounds[i].c1.ToTextureRect();
                var debugNote1 = Game.MainScene.g.rounds[i].debugNote1;
                var debugNote2 = Game.MainScene.g.rounds[i].debugNote2;
                var debugNote3 = Game.MainScene.g.rounds[i].debugNote3;

                Rounds[i][0] = new Sprite(this, Game.CardTextures, rect)
                {
                    Position = new Vector2(200, 100 + (i + headLength) * (Hand.CardHeight + 50) + 30),
                    ZIndex = (Mariasek.Engine.New.Game.NumPlayers * Mariasek.Engine.New.Game.NumRounds + 2) + i * 3
                };
                Labels[i][0] = new Label(this)
                {
                    Position = new Vector2(200 + 3 * Hand.CardWidth - 20, 100 + (i + headLength - 0.5f) * (Hand.CardHeight + 50) + 20),
                    Width = 600,
                    Height = 40,
                    Text = string.Format("{0}: {1}", Game.MainScene.g.rounds[i].player1.Name, Game.MainScene.g.rounds[i].player1.PlayerIndex == 0 ? "-" : debugNote1 != null ? debugNote1.Split('\n')[0] : "-"),
                    TextColor = Game.MainScene.g.rounds[i].roundWinner.PlayerIndex == Game.MainScene.g.rounds[i].player1.PlayerIndex ? Color.Yellow : Color.White,
                    UseCommonScissorRect = true
                };
                rect = Game.MainScene.g.rounds[i].c2.ToTextureRect();
                Rounds[i][1] = new Sprite(this, Game.CardTextures, rect)
                {
                    Position = new Vector2(200 + Hand.CardWidth - 10, 100 + (i + Mariasek.Engine.New.Game.NumPlayers + 1) * (Hand.CardHeight + 50) + 40),
                    ZIndex = (Mariasek.Engine.New.Game.NumPlayers * Mariasek.Engine.New.Game.NumRounds + 2) + i * 3 + 1
                };
                Labels[i][1] = new Label(this)
                {
                    Position = new Vector2(200 + 3 * Hand.CardWidth - 20, 100 + (i + headLength - 0.5f) * (Hand.CardHeight + 50) + 50),
                    Width = 600,
                    Height = 40,
                    Text = string.Format("{0}: {1}", Game.MainScene.g.rounds[i].player2.Name, Game.MainScene.g.rounds[i].player2.PlayerIndex == 0 ? "-" : debugNote2 != null ? debugNote2.Split('\n')[0] : "-"),
                    TextColor = Game.MainScene.g.rounds[i].roundWinner.PlayerIndex == Game.MainScene.g.rounds[i].player2.PlayerIndex ? Color.Yellow : Color.White,
                    UseCommonScissorRect = true
                };
                rect = Game.MainScene.g.rounds[i].c3.ToTextureRect();
                Rounds[i][2] = new Sprite(this, Game.CardTextures, rect)
                {
                    Position = new Vector2(200 + 2 * (Hand.CardWidth - 10), 100 + (i + headLength) * (Hand.CardHeight + 50) + 50),
                    ZIndex = (Mariasek.Engine.New.Game.NumPlayers * Mariasek.Engine.New.Game.NumRounds + 2) + i * 3 + 2
                };
                Labels[i][2] = new Label(this)
                {
                    Position = new Vector2(200 + 3 * Hand.CardWidth - 20, 100 + (i + headLength - 0.5f) * (Hand.CardHeight + 50) + 80),
                    Width = 600,
                    Height = 40,
                    Text = string.Format("{0}: {1}", Game.MainScene.g.rounds[i].player3.Name, Game.MainScene.g.rounds[i].player3.PlayerIndex == 0 ? "-" : debugNote3 != null ? debugNote3.Split('\n')[0] : "-"),
                    TextColor = Game.MainScene.g.rounds[i].roundWinner.PlayerIndex == Game.MainScene.g.rounds[i].player3.PlayerIndex ? Color.Yellow : Color.White,
                    UseCommonScissorRect = true
                };
            }
            _initialHands[3] = Game.MainScene.g.talon;
            BoundsRect = new Rectangle(0, 0, (int)Game.VirtualScreenWidth - (int)Position.X, (roundsLength + headLength) * (Hand.CardHeight + 50) - (int)Position.Y);
            ScrollBarColor = Color.Transparent;

            for (var i = 0; i < Mariasek.Engine.New.Game.NumPlayers; i++)
            {
                var ii = (Game.MainScene.g.GameStartingPlayerIndex + i) % Mariasek.Engine.New.Game.NumPlayers;
                var hand = new List<Card>(_initialHands[ii].Sort(false, Game.MainScene.g.trump.HasValue ? false : true));

                Hands[i] = new Sprite[hand.Count];
                Names[i].Text = Game.MainScene.g.players[ii].Name;
                for (var j = 0; j < hand.Count; j++)
                {
                    var rect = hand[j].ToTextureRect();
                    Hands[i][j] = new Sprite(this, Game.CardTextures, rect)
                    {
                        Position = new Vector2(200 + j * (Hand.CardWidth - 10), 100 + i * (Hand.CardHeight + 50) + 30),
                        ZIndex = i * Mariasek.Engine.New.Game.NumRounds + j
                    };
                }
            }
            Hands[3] = new Sprite[_initialHands[3].Count];
            Names[3].Text = "Talon";
            for (var j = 0; j < _initialHands[3].Count; j++)
            {
                var rect = _initialHands[3][j].ToTextureRect();
                Hands[3][j] = new Sprite(this, Game.CardTextures, rect)
                {
                    Position = new Vector2(200 + j * (Hand.CardWidth - 10), 100 + 3 * (Hand.CardHeight + 50) + 30),
                    ZIndex = Mariasek.Engine.New.Game.NumPlayers * Mariasek.Engine.New.Game.NumRounds + j
                };
            }
        }
    }
}
