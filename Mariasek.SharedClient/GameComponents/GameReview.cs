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
                _background.BorderColors = new List<Color> { value };
                _background.BackgroundColors = new List<Color> { value };
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
                _background.Position = value;
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

        public GameReview(GameComponent parent)
            : base(parent)
        {
            const int headLength = Mariasek.Engine.New.Game.NumPlayers + 1;
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
            _initialHands = new List<Mariasek.Engine.New.Card>[Mariasek.Engine.New.Game.NumPlayers + 1];
            Hands = new Sprite[Mariasek.Engine.New.Game.NumPlayers + 1][];
            Names = new Label[Mariasek.Engine.New.Game.NumPlayers + 1];
            RoundNum = new Label[Mariasek.Engine.New.Game.NumRounds];
            Rounds = new Sprite[Mariasek.Engine.New.Game.NumRounds][];
            Labels = new Label[Mariasek.Engine.New.Game.NumRounds][];

            for (var i = 0; i < Mariasek.Engine.New.Game.NumPlayers + 1; i++)
            {
                //_initialHands[i] = new List<Mariasek.Engine.New.Card>();
                //if (i < Mariasek.Engine.New.Game.NumPlayers)
                //{
                //    _initialHands[i].AddRange(game.players[i].Hand);    //u betla a durcha mohly zustat karty v ruce
                //}
                Names[i] = new Label(this)
                {
                    Position = new Vector2(200 - 0.5f * Hand.CardWidth * reviewCardScaleFactor.X, 100 + (i - 0.5f) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 20),
                    Width = 200,
                    Height = 40,
                    TextColor = Game.Settings.HighlightedTextColor,
                    ZIndex = 200,
                    UseCommonScissorRect = true
                };
            }
            for (var i = 0; i < Rounds.Length; i++)
            {
                //if (game.rounds[i] == null || game.rounds[i].c3 == null)
                //{
                //    continue;
                //}
                //if (i >= roundsLength)
                //{
                //    roundsLength = i + 1;
                //}
                RoundNum[i] = new Label(this)
                {
                    Position = new Vector2(200 - 0.5f * Hand.CardWidth * reviewCardScaleFactor.X, 100 + (i + 4 - 0.5f) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 20),
                    Width = 200,
                    Height = 40,
                    Text = string.Format("{0}. kolo:", i + 1),
                    TextColor = Game.Settings.HighlightedTextColor,
                    ZIndex = 200,
                    UseCommonScissorRect = true
                };
                //var r = game.rounds[i];
                Rounds[i] = new Sprite[Mariasek.Engine.New.Game.NumPlayers];
                Labels[i] = new Label[Mariasek.Engine.New.Game.NumPlayers];
                //if (r.c1 != null)
                //{
                //    _initialHands[r.player1.PlayerIndex].Add(r.c1);
                //}
                //if (r.c2 != null)
                //{
                //    _initialHands[r.player2.PlayerIndex].Add(r.c2);
                //}
                //if (r.c3 != null)
                //{
                //    _initialHands[r.player3.PlayerIndex].Add(r.c3);
                //}

                //var rect = r.c1.ToTextureRect();
                //var debugNote1 = GetMainDebugNote(r.debugNote1);
                //var debugNote2 = GetMainDebugNote(r.debugNote2);
                //var debugNote3 = GetMainDebugNote(r.debugNote3);

                Rounds[i][0] = new Sprite(this, Game.CardTextures)//, rect)
                {
                    Position = new Vector2(200, 100 + (i + headLength) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 30),
					ZIndex = (Mariasek.Engine.New.Game.NumPlayers * Mariasek.Engine.New.Game.NumRounds + 2) + i * 3 + 1,
					Scale = reviewCardScaleFactor
                };
                //if ((game.GameType & (Hra.Betl | Hra.Durch)) == 0 && (r.c1.Value == Hodnota.Eso || r.c1.Value == Hodnota.Desitka))
                //{
                //    if (r.roundWinner.PlayerIndex == game.players[0].PlayerIndex || r.roundWinner.PlayerIndex == game.players[0].TeamMateIndex)
                //    {
                //        Rounds[i][0].Tint = Color.LightGreen;
                //    }
                //    else
                //    {
                //        Rounds[i][0].Tint = Color.LightPink;
                //    }
                //}
                Labels[i][0] = new Label(this)
                {
                    Position = new Vector2(200 + 3 * Hand.CardWidth * reviewCardScaleFactor.X - 20, 100 + (i + headLength - 0.5f) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 20),
                    Width = 600,
                    Height = 40,
                    //Text = string.Format("{0}: {1}", game.rounds[i].player1.Name, game.rounds[i].player1.PlayerIndex == 0 ? "-" : debugNote1 != null ? debugNote1.Split('\n')[0] : "-"),
                    //TextColor = game.rounds[i].roundWinner.PlayerIndex == game.rounds[i].player1.PlayerIndex ? Color.Yellow : Color.White,
                    UseCommonScissorRect = true,
                    ZIndex = 200,
                    FontScaleFactor = 0.9f
                };
                //rect = game.rounds[i].c2.ToTextureRect();
                Rounds[i][1] = new Sprite(this, Game.CardTextures)//, rect)
                {
                    Position = new Vector2(200 + Hand.CardWidth * reviewCardScaleFactor.X - 10, 100 + (i + headLength) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 40),
					ZIndex = (Mariasek.Engine.New.Game.NumPlayers * Mariasek.Engine.New.Game.NumRounds + 2) + i * 3 + 2,
					Scale = reviewCardScaleFactor
                };
                //if ((game.GameType & (Hra.Betl | Hra.Durch)) == 0 && (r.c2.Value == Hodnota.Eso || r.c2.Value == Hodnota.Desitka))
                //{
                //    if (r.roundWinner.PlayerIndex == game.players[0].PlayerIndex || r.roundWinner.PlayerIndex == game.players[0].TeamMateIndex)
                //    {
                //        Rounds[i][1].Tint = Color.LightGreen;
                //    }
                //    else
                //    {
                //        Rounds[i][1].Tint = Color.LightPink;
                //    }
                //}
                Labels[i][1] = new Label(this)
                {
                    Position = new Vector2(200 + 3 * Hand.CardWidth * reviewCardScaleFactor.X - 20, 100 + (i + headLength - 0.5f) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 50),
                    Width = 600,
                    Height = 40,
                    //Text = string.Format("{0}: {1}", game.rounds[i].player2.Name, game.rounds[i].player2.PlayerIndex == 0 ? "-" : debugNote2 != null ? debugNote2.Split('\n')[0] : "-"),
                    //TextColor = game.rounds[i].roundWinner.PlayerIndex == game.rounds[i].player2.PlayerIndex ? Color.Yellow : Color.White,
                    UseCommonScissorRect = true,
                    ZIndex = 200,
                    FontScaleFactor = 0.9f
                };
                //rect = game.rounds[i].c3.ToTextureRect();
                Rounds[i][2] = new Sprite(this, Game.CardTextures)//, rect)
                {
                    Position = new Vector2(200 + 2 * (Hand.CardWidth * reviewCardScaleFactor.X - 10), 100 + (i + headLength) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 50),
					ZIndex = (Mariasek.Engine.New.Game.NumPlayers * Mariasek.Engine.New.Game.NumRounds + 2) + i * 3 + 3,
					Scale = reviewCardScaleFactor
                };
                //if ((game.GameType & (Hra.Betl | Hra.Durch)) == 0 && (r.c3.Value == Hodnota.Eso || r.c3.Value == Hodnota.Desitka))
                //{
                //    if (r.roundWinner.PlayerIndex == game.players[0].PlayerIndex || r.roundWinner.PlayerIndex == game.players[0].TeamMateIndex)
                //    {
                //        Rounds[i][2].Tint = Color.LightGreen;
                //    }
                //    else
                //    {
                //        Rounds[i][2].Tint = Color.LightPink;
                //    }
                //}
                Labels[i][2] = new Label(this)
                {
                    Position = new Vector2(200 + 3 * Hand.CardWidth * reviewCardScaleFactor.X - 20, 100 + (i + headLength - 0.5f) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 80),
                    Width = 600,
                    Height = 40,
                    //Text = string.Format("{0}: {1}", game.rounds[i].player3.Name, game.rounds[i].player3.PlayerIndex == 0 ? "-" : debugNote3 != null ? debugNote3.Split('\n')[0] : "-"),
                    //TextColor = game.rounds[i].roundWinner.PlayerIndex == game.rounds[i].player3.PlayerIndex ? Color.Yellow : Color.White,
                    UseCommonScissorRect = true,
                    ZIndex = 200,
                    FontScaleFactor = 0.9f
                };
            }
            //_initialHands[3] = game.talon;
            BoundsRect = new Rectangle(0, 0, (int)Game.VirtualScreenWidth - (int)Position.X, 
                                       30 + (roundsLength + headLength) * (int)(Hand.CardHeight * reviewCardScaleFactor.Y + 50) - 
                                       (int)Position.Y);
            ScrollBarColor = Color.Transparent;

			//var maxHlasKMarked = false;
   //         var maxHlasQMarked = false;
			//var maxHlasKAgainstMarked = false;
            //var maxHlasQAgainstMarked = false;
			for (var i = 0; i < Mariasek.Engine.New.Game.NumPlayers; i++)
            {
                //var ii = (game.GameStartingPlayerIndex + i) % Mariasek.Engine.New.Game.NumPlayers;
                //var hand = new List<Card>(_initialHands[ii].Sort(false, game.trump.HasValue ? false : true, game.trump));

                Hands[i] = new Sprite[12];//hand.Count];
                //Names[i].Text = i == 0 ? string.Format("{0}: {1}", 
                                       //                game.players[ii].Name, 
                                       //                game.GameType.ToDescription().Trim() +
                                       //                (string.IsNullOrEmpty(game.players[ii].BidMade)
                                       //                     ? string.Empty
                                       //                     : string.Format(" {0}", game.players[ii].BidMade.TrimEnd()))) +
                                       //  (Game.Settings.HintEnabled && game.GameTypeConfidence >= 0
                                       //     ? string.Format(" ({0:0}%)", game.GameTypeConfidence * 100)
                                       //     : string.Empty)
                                       //: string.IsNullOrEmpty(game.players[ii].BidMade)
                                            //? game.players[ii].Name
                                            //: string.Format("{0}: {1}", game.players[ii].Name,
                                              //                          game.players[ii].BidMade.Trim()) +
                                              //(Game.Settings.HintEnabled && game.players[ii].BidConfidence >= 0
                                                //? string.Format(" ({0:0}%)", game.players[ii].BidConfidence * 100)
                                                //: string.Empty);
                //for (var j = 0; j < hand.Count; j++)
                for (var j = 0; j < 12; j++)
                {
                    //var rect = hand[j].ToTextureRect();
                    //var hlas = (game.GameType & (Hra.Betl | Hra.Durch)) == 0 &&
                        //       game.rounds[0] != null &&
                        //       (game.Results == null ||
                        //        game.Results.BasicPointsWon + game.Results.BasicPointsLost == 90) &&
                        //((hand[j].Value == Hodnota.Kral && hand.Any(k => k.Value == Hodnota.Svrsek && k.Suit == hand[j].Suit)) ||
                         //(hand[j].Value == Hodnota.Svrsek && hand.Any(k => k.Value == Hodnota.Kral && k.Suit == hand[j].Suit)));

     //               if (hlas && 
     //                   game.Results != null &&
     //                   i == 0 && 
     //                   (game.GameType & Hra.Kilo) != 0 &&
     //                   !game.Results.HundredWon)
     //               {
     //                   if (maxHlasKMarked && maxHlasQMarked)
     //                   {
					//		hlas = false;
					//	}
     //                   else if ((game.Results.MaxHlasWon == 40 &&
     //                             hand[j].Suit == game.trump) ||
					//			 (game.Results.MaxHlasWon == 20 &&
     //                             hand[j].Suit != game.trump))
     //                   {
     //                       if (hand[j].Value == Hodnota.Kral)
     //                       {
     //                           maxHlasKMarked = true;
     //                       }
     //                       else
     //                       {
     //                           maxHlasQMarked = true;
     //                       }
     //                   }
     //               }
					//else if (hlas &&
					//	     game.Results != null &&
					//	     i != 0 &&
					//	     (game.GameType & Hra.KiloProti) != 0 &&
					//	     !game.Results.HundredAgainstWon)
					//{
					//	if (maxHlasKAgainstMarked && maxHlasQAgainstMarked)
					//	{
					//		hlas = false;
					//	}
					//	else if ((game.Results.MaxHlasLost == 40 &&
					//			  hand[j].Suit == game.trump) ||
					//			 (game.Results.MaxHlasLost == 20 &&
					//			  hand[j].Suit != game.trump))
					//	{
     //                       if (hand[j].Value == Hodnota.Kral)
     //                       {
     //                           maxHlasKAgainstMarked = true;
     //                       }
     //                       else
     //                       {
     //                           maxHlasQAgainstMarked = true;
     //                       }
					//	}
					//}
                    Hands[i][j] = new Sprite(this, Game.CardTextures)//, rect)
                    {
                        Position = new Vector2(200 + j * (Hand.CardWidth * reviewCardScaleFactor.X - 10), 100 + i * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 30),
						ZIndex = i * Mariasek.Engine.New.Game.NumRounds + j + 1,
						Scale = reviewCardScaleFactor
                    };
     //               if ((game.GameType & (Hra.Betl | Hra.Durch)) == 0 &&
     //                   (hand[j].Value == Hodnota.Eso ||
     //                    hand[j].Value == Hodnota.Desitka) &&
     //                   game.rounds.Any(k => k != null &&
     //                                                    (k.c1 == hand[j] ||
     //                                                     k.c2 == hand[j] ||
     //                                                     k.c3 == hand[j])))
     //               {
					//	var roundWinner = game.rounds.First(k => k != null &&
     //                                                                        (k.c1 == hand[j] ||
     //                                                                         k.c2 == hand[j] ||
     //                                                                         k.c3 == hand[j])).roundWinner;
     //                   if (roundWinner.PlayerIndex == 0 || roundWinner.TeamMateIndex == 0)
     //                   {
     //                       Hands[i][j].Tint = Color.LightGreen;
     //                   }
     //                   else
     //                   {
     //                       Hands[i][j].Tint = Color.LightPink;
     //                   }
					//}
                    //if (hlas)
                    //{
                    //    if (game.players[ii].PlayerIndex == 0 || game.players[ii].TeamMateIndex == 0)
                    //    {
                    //        Hands[i][j].Tint = Color.LightGreen;
                    //    }
                    //    else
                    //    {
                    //        Hands[i][j].Tint = Color.LightPink;
                    //    }
                    //}
                }
            }
            Hands[3] = new Sprite[2];//_initialHands[3].Count];
            Names[3].Text = "Talon";
            for (var j = 0; j < 2; j++)//_initialHands[3].Count; j++)
            {
                //var rect = _initialHands[3][j].ToTextureRect();
                Hands[3][j] = new Sprite(this, Game.CardTextures)//, rect)
                {
                    Position = new Vector2(200 + j * (Hand.CardWidth * reviewCardScaleFactor.X - 10), 100 + 3 * (Hand.CardHeight * reviewCardScaleFactor.Y + 50) + 30),
                    ZIndex = Mariasek.Engine.New.Game.NumPlayers * Mariasek.Engine.New.Game.NumRounds + j + 1,
					Scale = reviewCardScaleFactor,
                    SpriteRectangle = Rectangle.Empty
                };
            }
        }

        public void UpdateReview(Mariasek.Engine.New.Game game)
        {
            const int headLength = Mariasek.Engine.New.Game.NumPlayers + 1;
            var roundsLength = game.CurrentRound != null ? game.CurrentRound.number : 0;
            var reviewCardScaleFactor = Game.CardScaleFactor * 0.7f;

            //if (game.IsRunning)
            //{
            //    BackgroundColor = Color.Black;
            //}
            //else
            //{
            //    BackgroundColor = Color.Transparent;
            //}
            for (var i = 0; i < Mariasek.Engine.New.Game.NumPlayers; i++)
            {
                _initialHands[i] = new List<Mariasek.Engine.New.Card>(game.players[i].Hand);
            }
            if (game.trump.HasValue)
            {
                var talon = game.talon;

                for (var i = 0; i < talon.Count(); i++)
                {
                    if (talon[i].Value == Hodnota.Eso ||
                        talon[i].Value == Hodnota.Desitka)
                    {
                        if (game.GameStartingPlayer.PlayerIndex == 0)
                        {
                            Hands[3][i].Tint = Game.Settings.ReviewPtsLostColor;
                        }
                        else
                        {
                            Hands[3][i].Tint = Game.Settings.ReviewPtsWonColor;
                        }
                    }
                    else
                    {
                        Hands[3][i].Tint = Color.White;
                    }
                }
            }
            for (var i = 0; i < Rounds.Length; i++)
            {
                if (game.rounds[i] == null || game.rounds[i].c3 == null)
                {
                    for (var j = 0; j < Mariasek.Engine.New.Game.NumPlayers; j++)
                    {
                        Labels[i][j].Hide();
                        Rounds[i][j].Hide();
                    }
                    continue;
                }
                if (i >= roundsLength)
                {
                    roundsLength = i + 1;
                }
                var r = game.rounds[i];
                _initialHands[r.player1.PlayerIndex].Add(r.c1);
                _initialHands[r.player2.PlayerIndex].Add(r.c2);
                _initialHands[r.player3.PlayerIndex].Add(r.c3);

                var debugNote1 = GetMainDebugNote(r.debugNote1);
                var debugNote2 = GetMainDebugNote(r.debugNote2);
                var debugNote3 = GetMainDebugNote(r.debugNote3);

                Rounds[i][0].SpriteRectangle = r.c1.ToTextureRect();
                if ((game.GameType & (Hra.Betl | Hra.Durch)) == 0 && (r.c1.Value == Hodnota.Eso || r.c1.Value == Hodnota.Desitka))
                {
                    if (r.roundWinner.PlayerIndex == game.players[0].PlayerIndex || r.roundWinner.PlayerIndex == game.players[0].TeamMateIndex)
                    {
                        Rounds[i][0].Tint = Game.Settings.ReviewPtsWonColor;//Color.LightGreen;
                    }
                    else
                    {
                        Rounds[i][0].Tint = Game.Settings.ReviewPtsLostColor;//Pink;
                    }
                }
                else
                {
                    Rounds[i][0].Tint = Color.White;
                }
                Labels[i][0].Text = string.Format("{0}: {1}", game.rounds[i].player1.Name, game.rounds[i].player1.PlayerIndex == 0 ? "-" : debugNote1 != null ? debugNote1.Split('\n')[0] : "-");
                Labels[i][0].TextColor = game.rounds[i].roundWinner.PlayerIndex == game.rounds[i].player1.PlayerIndex 
                                            ? Game.Settings.HighlightedTextColor
                                            : Game.Settings.DefaultTextColor;

                Rounds[i][1].SpriteRectangle = game.rounds[i].c2.ToTextureRect();
                if ((game.GameType & (Hra.Betl | Hra.Durch)) == 0 && (r.c2.Value == Hodnota.Eso || r.c2.Value == Hodnota.Desitka))
                {
                    if (r.roundWinner.PlayerIndex == game.players[0].PlayerIndex || r.roundWinner.PlayerIndex == game.players[0].TeamMateIndex)
                    {
                        Rounds[i][1].Tint = Game.Settings.ReviewPtsWonColor;
                    }
                    else
                    {
                        Rounds[i][1].Tint = Game.Settings.ReviewPtsLostColor;
                    }
                }
                else
                {
                    Rounds[i][1].Tint = Color.White;
                }
                Labels[i][1].Text = string.Format("{0}: {1}", game.rounds[i].player2.Name, game.rounds[i].player2.PlayerIndex == 0 ? "-" : debugNote2 != null ? debugNote2.Split('\n')[0] : "-");
                Labels[i][1].TextColor = game.rounds[i].roundWinner.PlayerIndex == game.rounds[i].player2.PlayerIndex 
                                            ? Game.Settings.HighlightedTextColor
                                            : Game.Settings.DefaultTextColor;

                Rounds[i][2].SpriteRectangle = game.rounds[i].c3.ToTextureRect();
                if ((game.GameType & (Hra.Betl | Hra.Durch)) == 0 && (r.c3.Value == Hodnota.Eso || r.c3.Value == Hodnota.Desitka))
                {
                    if (r.roundWinner.PlayerIndex == game.players[0].PlayerIndex || r.roundWinner.PlayerIndex == game.players[0].TeamMateIndex)
                    {
                        Rounds[i][2].Tint = Game.Settings.ReviewPtsWonColor;
                    }
                    else
                    {
                        Rounds[i][2].Tint = Game.Settings.ReviewPtsLostColor;
                    }
                }
                else
                {
                    Rounds[i][2].Tint = Color.White;
                }
                Labels[i][2].Text = string.Format("{0}: {1}", game.rounds[i].player3.Name, game.rounds[i].player3.PlayerIndex == 0 ? "-" : debugNote3 != null ? debugNote3.Split('\n')[0] : "-");
                Labels[i][2].TextColor = game.rounds[i].roundWinner.PlayerIndex == game.rounds[i].player3.PlayerIndex 
                                            ? Game.Settings.HighlightedTextColor
                                            : Game.Settings.DefaultTextColor;
                for (var j = 0; j < Mariasek.Engine.New.Game.NumPlayers; j++)
                {
                    Labels[i][j].Show();
                    Rounds[i][j].Show();
                }
            }
            _initialHands[3] = game.talon;
            BoundsRect = new Rectangle(0, 0, (int)Game.VirtualScreenWidth - (int)Position.X,
                                       130 + 
                                       (int)((roundsLength + headLength - 0.5f) * (Hand.CardHeight * reviewCardScaleFactor.Y + 50)) -
                                       (int)Position.Y);
            ScrollBarColor = Color.Transparent;

            var maxHlasKMarked = false;
            var maxHlasQMarked = false;
            var maxHlasKAgainstMarked = false;
            var maxHlasQAgainstMarked = false;
            for (var i = 0; i < Mariasek.Engine.New.Game.NumPlayers; i++)
            {
                var ii = (game.GameStartingPlayerIndex + i) % Mariasek.Engine.New.Game.NumPlayers;
                var hand = new List<Card>(_initialHands[ii].Sort(false, game.trump.HasValue ? false : true, game.trump));

                Names[i].Text = i == 0 ? string.Format("{0}: {1}",
                                                       game.players[ii].Name,
                                                       game.GameType.ToDescription().Trim() +
                                                       (string.IsNullOrEmpty(game.players[ii].BidMade)
                                                            ? string.Empty
                                                            : string.Format(" {0}", game.players[ii].BidMade.TrimEnd()))) +
                                         (game.GameTypeConfidence >= 0
                                            ? string.Format(" ({0:0}%)", game.GameTypeConfidence * 100)
                                            : string.Empty)
                                       : string.IsNullOrEmpty(game.players[ii].BidMade)
                                            ? game.players[ii].Name
                                            : string.Format("{0}: {1}", game.players[ii].Name,
                                                                        game.players[ii].BidMade.Trim()) +
                                              (game.players[ii].BidConfidence >= 0
                                                ? string.Format(" ({0:0}%)", game.players[ii].BidConfidence * 100)
                                                : string.Empty);
                for (var j = 0; j < hand.Count; j++)
                {
                    var rect = hand[j].ToTextureRect();
                    var hlas = (game.GameType & (Hra.Betl | Hra.Durch)) == 0 &&
                               game.rounds[0] != null &&
                               (game.Results == null ||
                                game.Results.BasicPointsWon + game.Results.BasicPointsLost == 90) &&
                        ((hand[j].Value == Hodnota.Kral && hand.Any(k => k.Value == Hodnota.Svrsek && k.Suit == hand[j].Suit)) ||
                         (hand[j].Value == Hodnota.Svrsek && hand.Any(k => k.Value == Hodnota.Kral && k.Suit == hand[j].Suit)));

                    if (hlas &&
                        game.Results != null &&
                        i == 0 &&
                        (game.GameType & Hra.Kilo) != 0 &&
                        !game.Results.HundredWon)
                    {
                        if (maxHlasKMarked && maxHlasQMarked)
                        {
                            hlas = false;
                        }
                        else if ((game.Results.MaxHlasWon == 40 &&
                                  hand[j].Suit == game.trump) ||
                                 (game.Results.MaxHlasWon == 20 &&
                                  hand[j].Suit != game.trump))
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
                             game.Results != null &&
                             i != 0 &&
                             (game.GameType & Hra.KiloProti) != 0 &&
                             !game.Results.HundredAgainstWon)
                    {
                        if (maxHlasKAgainstMarked && maxHlasQAgainstMarked)
                        {
                            hlas = false;
                        }
                        else if ((game.Results.MaxHlasLost == 40 &&
                                  hand[j].Suit == game.trump) ||
                                 (game.Results.MaxHlasLost == 20 &&
                                  hand[j].Suit != game.trump))
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
                    Hands[i][j].SpriteRectangle = rect;
                    Hands[i][j].Show();
                    if ((game.GameType & (Hra.Betl | Hra.Durch)) == 0 &&
                        (hand[j].Value == Hodnota.Eso ||
                         hand[j].Value == Hodnota.Desitka) &&
                        game.rounds.Any(k => k != null &&
                                                         (k.c1 == hand[j] ||
                                                          k.c2 == hand[j] ||
                                                          k.c3 == hand[j])))
                    {
                        var roundWinner = game.rounds.First(k => k != null &&
                                                                             (k.c1 == hand[j] ||
                                                                              k.c2 == hand[j] ||
                                                                              k.c3 == hand[j])).roundWinner;
                        if (roundWinner.PlayerIndex == 0 || roundWinner.TeamMateIndex == 0)
                        {
                            Hands[i][j].Tint = Game.Settings.ReviewPtsWonColor;
                        }
                        else
                        {
                            Hands[i][j].Tint = Game.Settings.ReviewPtsLostColor;
                        }
                    }
                    else
                    {
                        Hands[i][j].Tint = Color.White;
                    }
                    if (hlas)
                    {
                        if (game.players[ii].PlayerIndex == 0 || game.players[ii].TeamMateIndex == 0)
                        {
                            Hands[i][j].Tint = Game.Settings.ReviewPtsWonColor;
                        }
                        else
                        {
                            Hands[i][j].Tint = Game.Settings.ReviewPtsLostColor;
                        }
                    }
                }
                for (var j = hand.Count; j < 12; j++)
                {
                    Hands[i][j].Hide();
                }
            }
            Names[3].Text = "Talon";
            for (var j = 0; j < _initialHands[3].Count; j++)
            {
                var rect = _initialHands[3][j].ToTextureRect();
                Hands[3][j].SpriteRectangle = rect;
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
