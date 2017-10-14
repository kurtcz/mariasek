using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Mariasek.Engine.New;
using Microsoft.Xna.Framework.Graphics;

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
        private Color _backgroundColor;
        private bool _positionSet;
        public Color BackgroundColor
        {
            get { return _backgroundColor; }
            set
            {
                _backgroundColor = value;
                if (_background != null)
                {
                    _background.BorderColors = new List<Color> { value };
                    _background.BackgroundColors = new List<Color> { value };
                }
            }
        }

        public override float Opacity
        {
            get { return base.Opacity; }
            set
            {
                base.Opacity = value;
                if (Hands != null)
                {
                    for (var i = 0; i < Hands.Length; i++)
                    {
                        if (Hands[i] == null)
                        {
                            continue;
                        }
                        for (var j = 0; j < Hands[i].Length; j++)
                        {
                            if (Hands[i][j] == null)
                            {
                                continue;
                            }
                            Hands[i][j].Opacity = value;
                        }
                    }
                }
                if (Rounds != null)
                {
                    for (var i = 0; i < Rounds.Length; i++)
                    {
                        if (Rounds[i] == null)
                        {
                            continue;
                        }
                        for (var j = 0; j < Rounds[i].Length; j++)
                        {
                            if (Rounds[i][j] == null)
                            {
                                continue;
                            }
                            Rounds[i][j].Opacity = value;
                        }
                    }
                }
                if (Names != null)
                {
                    for (var i = 0; i < Names.Length; i++)
                    {
                        if (Names[i] == null)
                        {
                            continue;
                        }
                        Names[i].Opacity = value;
                    }
                }
                if (RoundNum != null)
                {
                    for (var i = 0; i < RoundNum.Length; i++)
                    {
                        if (RoundNum[i] == null)
                        {
                            continue;
                        }
                        RoundNum[i].Opacity = value;
                    }
                }
                if (Labels != null)
                {
                    for (var i = 0; i < Labels.Length; i++)
                    {
                        if (Labels[i] == null)
                        {
                            continue;
                        }
                        for (var j = 0; j < Labels[i].Length; j++)
                        {
                            if (Labels[i][j] == null)
                            {
                                continue;
                            }
                            Labels[i][j].Opacity = value;
                        }
                    }
                }
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
                if (_background != null)
                {
                    _background.Position = value;
                }
                if (_positionSet)
                {
                    if (Hands != null)
                    {
                        for (var i = 0; i < Hands.Length; i++)
                        {
                            if (Hands[i] == null)
                            {
                                continue;
                            }
                            for (var j = 0; j < Hands[i].Length; j++)
                            {
                                if (Hands[i][j] == null)
                                {
                                    continue;
                                }
                                Hands[i][j].Position += positionDiff;
                            }
                        }
                    }
                    if (Rounds != null)
                    {
                        for (var i = 0; i < Rounds.Length; i++)
                        {
                            if (Rounds[i] == null)
                            {
                                continue;
                            }
                            for (var j = 0; j < Rounds[i].Length; j++)
                            {
                                if (Rounds[i][j] == null)
                                {
                                    continue;
                                }
                                Rounds[i][j].Position += positionDiff;
                            }
                        }
                    }
                    if (Names != null)
                    {
                        for (var i = 0; i < Names.Length; i++)
                        {
                            if (Names[i] == null)
                            {
                                continue;
                            }
                            Names[i].Position += positionDiff;
                        }
                    }
                    if (RoundNum != null)
                    {
                        for (var i = 0; i < RoundNum.Length; i++)
                        {
                            if (RoundNum[i] == null)
                            {
                                continue;
                            }
                            RoundNum[i].Position += positionDiff;
                        }
                    }
                    if (Labels != null)
                    {
                        for (var i = 0; i < Labels.Length; i++)
                        {
                            if (Labels[i] == null)
                            {
                                continue;
                            }
                            for (var j = 0; j < Labels[i].Length; j++)
                            {
                                if (Labels[i][j] == null)
                                {
                                    continue;
                                }
                                Labels[i][j].Position += positionDiff;
                            }
                        }
                    }
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
            : base(parent)
        {
            const int headLength = Mariasek.Engine.New.Game.NumPlayers + 1;
            var roundsLength = Game.MainScene.g.CurrentRound != null ? Game.MainScene.g.CurrentRound.number : 0;
			var reviewCardScaleFactor = Game.CardScaleFactor * 0.7f;

			_background = new RectangleShape(this)
            {
                BackgroundColors = new List<Color> { BackgroundColor },
                BorderColors = new List<Color> { BackgroundColor },
                Opacity = 0.8f,
                Width = BoundsRect.Width,
                Height = BoundsRect.Height
            };
            _initialHands = new List<Mariasek.Engine.New.Card>[Mariasek.Engine.New.Game.NumPlayers + 1];
            Hands = new Sprite[Mariasek.Engine.New.Game.NumPlayers + 1][];
            Names = new Label[Mariasek.Engine.New.Game.NumPlayers + 1];
            RoundNum = new Label[Mariasek.Engine.New.Game.NumRounds];
            Rounds = new Sprite[Mariasek.Engine.New.Game.NumRounds][];
            Labels = new Label[Mariasek.Engine.New.Game.NumRounds][];

            for (var i = 0; i < Mariasek.Engine.New.Game.NumPlayers + 1; i++)
            {
                _initialHands[i] = new List<Mariasek.Engine.New.Card>();
                if (i < Mariasek.Engine.New.Game.NumPlayers)
                {
                    _initialHands[i].AddRange(Game.MainScene.g.players[i].Hand);    //u betla a durcha mohly zustat karty v ruce
                }
                Names[i] = new Label(this)
                {
                    Position = new Vector2(200 - 0.5f * Hand.CardWidth * reviewCardScaleFactor.X, 100 + (i - 0.5f) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 20),
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
                if (i >= roundsLength)
                {
                    roundsLength = i + 1;
                }
                RoundNum[i] = new Label(this)
                {
                    Position = new Vector2(200 - 0.5f * Hand.CardWidth * reviewCardScaleFactor.X, 100 + (i + 4 - 0.5f) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 20),
                    Width = 200,
                    Height = 40,
                    Text = string.Format("{0}. kolo:", i + 1),
                    TextColor = Color.Yellow,
                    ZIndex = 200,
                    UseCommonScissorRect = true
                };
                var r = Game.MainScene.g.rounds[i];
                Rounds[i] = new Sprite[Mariasek.Engine.New.Game.NumPlayers];
                Labels[i] = new Label[Mariasek.Engine.New.Game.NumPlayers];
                _initialHands[r.player1.PlayerIndex].Add(r.c1);
                _initialHands[r.player2.PlayerIndex].Add(r.c2);
                _initialHands[r.player3.PlayerIndex].Add(r.c3);

                var rect = r.c1.ToTextureRect();
                var debugNote1 = GetMainDebugNote(r.debugNote1);
                var debugNote2 = GetMainDebugNote(r.debugNote2);
                var debugNote3 = GetMainDebugNote(r.debugNote3);

                Rounds[i][0] = new Sprite(this, Game.CardTextures, rect)
                {
                    Position = new Vector2(200, 100 + (i + headLength) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 30),
					ZIndex = (Mariasek.Engine.New.Game.NumPlayers * Mariasek.Engine.New.Game.NumRounds + 2) + i * 3 + 1,
					Scale = reviewCardScaleFactor
                };
                if ((Game.MainScene.g.GameType & (Hra.Betl | Hra.Durch)) == 0 && (r.c1.Value == Hodnota.Eso || r.c1.Value == Hodnota.Desitka))
                {
                    if (r.roundWinner.PlayerIndex == Game.MainScene.g.players[0].PlayerIndex || r.roundWinner.PlayerIndex == Game.MainScene.g.players[0].TeamMateIndex)
                    {
                        Rounds[i][0].Tint = Color.LightGreen;
                    }
                    else
                    {
                        Rounds[i][0].Tint = Color.LightPink;
                    }
                }
                Labels[i][0] = new Label(this)
                {
                    Position = new Vector2(200 + 3 * Hand.CardWidth * reviewCardScaleFactor.X - 20, 100 + (i + headLength - 0.5f) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 20),
                    Width = 600,
                    Height = 40,
                    Text = string.Format("{0}: {1}", Game.MainScene.g.rounds[i].player1.Name, Game.MainScene.g.rounds[i].player1.PlayerIndex == 0 ? "-" : debugNote1 != null ? debugNote1.Split('\n')[0] : "-"),
                    TextColor = Game.MainScene.g.rounds[i].roundWinner.PlayerIndex == Game.MainScene.g.rounds[i].player1.PlayerIndex ? Color.Yellow : Color.White,
                    UseCommonScissorRect = true,
                    ZIndex = 200
                };
                rect = Game.MainScene.g.rounds[i].c2.ToTextureRect();
                Rounds[i][1] = new Sprite(this, Game.CardTextures, rect)
                {
                    Position = new Vector2(200 + Hand.CardWidth * reviewCardScaleFactor.X - 10, 100 + (i + Mariasek.Engine.New.Game.NumPlayers + 1) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 40),
					ZIndex = (Mariasek.Engine.New.Game.NumPlayers * Mariasek.Engine.New.Game.NumRounds + 2) + i * 3 + 2,
					Scale = reviewCardScaleFactor
                };
                if ((Game.MainScene.g.GameType & (Hra.Betl | Hra.Durch)) == 0 && (r.c2.Value == Hodnota.Eso || r.c2.Value == Hodnota.Desitka))
                {
                    if (r.roundWinner.PlayerIndex == Game.MainScene.g.players[0].PlayerIndex || r.roundWinner.PlayerIndex == Game.MainScene.g.players[0].TeamMateIndex)
                    {
                        Rounds[i][1].Tint = Color.LightGreen;
                    }
                    else
                    {
                        Rounds[i][1].Tint = Color.LightPink;
                    }
                }
                Labels[i][1] = new Label(this)
                {
                    Position = new Vector2(200 + 3 * Hand.CardWidth * reviewCardScaleFactor.X - 20, 100 + (i + headLength - 0.5f) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 50),
                    Width = 600,
                    Height = 40,
                    Text = string.Format("{0}: {1}", Game.MainScene.g.rounds[i].player2.Name, Game.MainScene.g.rounds[i].player2.PlayerIndex == 0 ? "-" : debugNote2 != null ? debugNote2.Split('\n')[0] : "-"),
                    TextColor = Game.MainScene.g.rounds[i].roundWinner.PlayerIndex == Game.MainScene.g.rounds[i].player2.PlayerIndex ? Color.Yellow : Color.White,
                    UseCommonScissorRect = true,
                    ZIndex = 200
                };
                rect = Game.MainScene.g.rounds[i].c3.ToTextureRect();
                Rounds[i][2] = new Sprite(this, Game.CardTextures, rect)
                {
                    Position = new Vector2(200 + 2 * (Hand.CardWidth * reviewCardScaleFactor.X - 10), 100 + (i + headLength) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 50),
					ZIndex = (Mariasek.Engine.New.Game.NumPlayers * Mariasek.Engine.New.Game.NumRounds + 2) + i * 3 + 3,
					Scale = reviewCardScaleFactor
                };
                if ((Game.MainScene.g.GameType & (Hra.Betl | Hra.Durch)) == 0 && (r.c3.Value == Hodnota.Eso || r.c3.Value == Hodnota.Desitka))
                {
                    if (r.roundWinner.PlayerIndex == Game.MainScene.g.players[0].PlayerIndex || r.roundWinner.PlayerIndex == Game.MainScene.g.players[0].TeamMateIndex)
                    {
                        Rounds[i][2].Tint = Color.LightGreen;
                    }
                    else
                    {
                        Rounds[i][2].Tint = Color.LightPink;
                    }
                }
                Labels[i][2] = new Label(this)
                {
                    Position = new Vector2(200 + 3 * Hand.CardWidth * reviewCardScaleFactor.X - 20, 100 + (i + headLength - 0.5f) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 80),
                    Width = 600,
                    Height = 40,
                    Text = string.Format("{0}: {1}", Game.MainScene.g.rounds[i].player3.Name, Game.MainScene.g.rounds[i].player3.PlayerIndex == 0 ? "-" : debugNote3 != null ? debugNote3.Split('\n')[0] : "-"),
                    TextColor = Game.MainScene.g.rounds[i].roundWinner.PlayerIndex == Game.MainScene.g.rounds[i].player3.PlayerIndex ? Color.Yellow : Color.White,
                    UseCommonScissorRect = true,
                    ZIndex = 200
                };
            }
            _initialHands[3] = Game.MainScene.g.talon;
            BoundsRect = new Rectangle(0, 0, (int)Game.VirtualScreenWidth - (int)Position.X, (roundsLength + headLength) * (int)(Hand.CardHeight * reviewCardScaleFactor.Y + 50) - (int)Position.Y);
            ScrollBarColor = Color.Transparent;

			var maxHlasKMarked = false;
            var maxHlasQMarked = false;
			var maxHlasKAgainstMarked = false;
            var maxHlasQAgainstMarked = false;
			for (var i = 0; i < Mariasek.Engine.New.Game.NumPlayers; i++)
            {
                var ii = (Game.MainScene.g.GameStartingPlayerIndex + i) % Mariasek.Engine.New.Game.NumPlayers;
                var hand = new List<Card>(_initialHands[ii].Sort(false, Game.MainScene.g.trump.HasValue ? false : true, Game.MainScene.g.trump));

                Hands[i] = new Sprite[hand.Count];
                Names[i].Text = i == 0 ? string.Format("{0}: {1} ({2:0}%)", Game.MainScene.g.players[ii].Name, 
                                                                            Game.MainScene.g.GameType.ToDescription().Trim() +
                                                                            (string.IsNullOrEmpty(Game.MainScene.g.players[ii].BidMade)
                                                                             ? ""
                                                                             : string.Format(" {0}", 
                                                                                             Game.MainScene.g.players[ii].BidMade.TrimEnd())),
                                                                            Game.MainScene.g.GameTypeConfidence * 100) 
                                       : string.IsNullOrEmpty(Game.MainScene.g.players[ii].BidMade)
                                            ? Game.MainScene.g.players[ii].Name
                                            : string.Format("{0}: {1} ({2:0}%)", Game.MainScene.g.players[ii].Name,
                                                                                 Game.MainScene.g.players[ii].BidMade.Trim(),
                                                                                 Game.MainScene.g.players[ii].BidConfidence * 100);
                for (var j = 0; j < hand.Count; j++)
                {
                    var rect = hand[j].ToTextureRect();
                    var hlas = (Game.MainScene.g.GameType & (Hra.Betl | Hra.Durch)) == 0 &&
                               Game.MainScene.g.rounds[0] != null &&
                               (Game.MainScene.g.Results == null ||
                                Game.MainScene.g.Results.BasicPointsWon + Game.MainScene.g.Results.BasicPointsLost == 90) &&
                        ((hand[j].Value == Hodnota.Kral && hand.Any(k => k.Value == Hodnota.Svrsek && k.Suit == hand[j].Suit)) ||
                         (hand[j].Value == Hodnota.Svrsek && hand.Any(k => k.Value == Hodnota.Kral && k.Suit == hand[j].Suit)));

                    if (hlas && 
                        Game.MainScene.g.Results != null &&
                        i == 0 && 
                        (Game.MainScene.g.GameType & Hra.Kilo) != 0 &&
                        !Game.MainScene.g.Results.HundredWon)
                    {
                        if (maxHlasKMarked && maxHlasQMarked)
                        {
							hlas = false;
						}
                        else if ((Game.MainScene.g.Results.MaxHlasWon == 40 &&
                                  hand[j].Suit == Game.MainScene.g.trump) ||
								 (Game.MainScene.g.Results.MaxHlasWon == 20 &&
                                  hand[j].Suit != Game.MainScene.g.trump))
                        {
                            if (hand[j].Value == Hodnota.Kral)
                            {
                                maxHlasKMarked = true;
                            }
                            else
                            {
                                maxHlasQMarked = true;
                            }
                        }
                    }
					else if (hlas &&
						     Game.MainScene.g.Results != null &&
						     i != 0 &&
						     (Game.MainScene.g.GameType & Hra.KiloProti) != 0 &&
						     !Game.MainScene.g.Results.HundredAgainstWon)
					{
						if (maxHlasKAgainstMarked && maxHlasQAgainstMarked)
						{
							hlas = false;
						}
						else if ((Game.MainScene.g.Results.MaxHlasLost == 40 &&
								  hand[j].Suit == Game.MainScene.g.trump) ||
								 (Game.MainScene.g.Results.MaxHlasLost == 20 &&
								  hand[j].Suit != Game.MainScene.g.trump))
						{
                            if (hand[j].Value == Hodnota.Kral)
                            {
                                maxHlasKAgainstMarked = true;
                            }
                            else
                            {
                                maxHlasQAgainstMarked = true;
                            }
						}
					}
					Hands[i][j] = new Sprite(this, Game.CardTextures, rect)
                    {
                        Position = new Vector2(200 + j * (Hand.CardWidth * reviewCardScaleFactor.X - 10), 100 + i * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 30),
						ZIndex = i * Mariasek.Engine.New.Game.NumRounds + j + 1,
						Scale = reviewCardScaleFactor
                    };
                    if ((Game.MainScene.g.GameType & (Hra.Betl | Hra.Durch)) == 0 &&
                        Game.MainScene.g.rounds[0] != null &&
                        (hand[j].Value == Hodnota.Eso ||
                         hand[j].Value == Hodnota.Desitka))
                    {
						var roundWinner = Game.MainScene.g.rounds.First(k => k != null &&
                                                                             (k.c1 == hand[j] ||
                                                                              k.c2 == hand[j] ||
                                                                              k.c3 == hand[j])).roundWinner;
                        if (roundWinner.PlayerIndex == 0 || roundWinner.TeamMateIndex == 0)
                        {
                            Hands[i][j].Tint = Color.LightGreen;
                        }
                        else
                        {
                            Hands[i][j].Tint = Color.LightPink;
                        }
					}
                    if (hlas)
                    {
                        if (Game.MainScene.g.players[ii].PlayerIndex == 0 || Game.MainScene.g.players[ii].TeamMateIndex == 0)
                        {
                            Hands[i][j].Tint = Color.LightGreen;
                        }
                        else
                        {
                            Hands[i][j].Tint = Color.LightPink;
                        }
                    }
                }
            }
            Hands[3] = new Sprite[_initialHands[3].Count];
            Names[3].Text = "Talon";
            for (var j = 0; j < _initialHands[3].Count; j++)
            {
                var rect = _initialHands[3][j].ToTextureRect();
                Hands[3][j] = new Sprite(this, Game.CardTextures, rect)
                {
                    Position = new Vector2(200 + j * (Hand.CardWidth * reviewCardScaleFactor.X - 10), 100 + 3 * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 30),
                    ZIndex = Mariasek.Engine.New.Game.NumPlayers * Mariasek.Engine.New.Game.NumRounds + j + 1,
					Scale = reviewCardScaleFactor
                };
            }
        }

        private string GetMainDebugNote(string debugNote)
        {
            if (debugNote == null)
            {
                return null;
            }
            var pos = debugNote.IndexOf('(');

            if (pos < 0)
            {
                return debugNote;
            }
            return debugNote.Substring(0, pos);
        }
	}
}
