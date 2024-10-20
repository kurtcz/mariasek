﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mariasek.Engine;
using Mariasek.SharedClient.GameComponents;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Mariasek.SharedClient
{
	public class ReviewScene : Scene
    {
        private const float OriginalDescriptionFontScaleFactor = 0.82f;
        private string _savedGameFilePath = Path.Combine(MariasekMonoGame.RootPath, "_temp.hra");
        private string _screenPath = Path.Combine(MariasekMonoGame.RootPath, "screen.jpg");

        Button _backButton;
        Button _replayButton;
        Button _replayOptionButton;
        Button _replayAsPlayer2Button;
        Button _replayAsPlayer3Button;
        Button _replayFromButton;
        ToggleButton _rawButton;
        Button _editButton;
        Button _sendButton;
        GameReview _review;
        TextBox _description;
        TextBox _rawData;

        Vector2 _origPosition;
        Vector2 _hiddenPosition;
        string _newGamePath;
        string _endGamePath;

        public ReviewScene(MariasekMonoGame game)
        : base(game)
        {
        }

        public override void Initialize()
        {
            base.Initialize();

            Background = Game.Assets.GetTexture("wood2");
            BackgroundTint = Color.DimGray;

            _origPosition = new Vector2(220, 45);
            _hiddenPosition = new Vector2(Game.VirtualScreenWidth, 45);

            _description = new TextBox(this)
            {
                Position = new Vector2(10, 10),
                Width = 250,
                AutosizeText = true,
                AutosizeMode = AutosizeMode.Vertical,
                Height = (int)Game.VirtualScreenHeight - 190,
                FontScaleFactor = OriginalDescriptionFontScaleFactor,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                HighlightColor = Game.Settings.HighlightedTextColor,
                HighlightedLine = 2,
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Top,
                Tabs = new[] { new Tab() { TabAlignment = HorizontalAlignment.Right, TabPosition = 180 } }
            };
            _backButton = new Button(this)
            {
                Position = new Vector2(10, (int)Game.VirtualScreenHeight - 60),
                Width = 200,
                Height = 50,
                Text = "Zpět",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _backButton.Click += BackButtonClicked;
            _rawButton = new ToggleButton(this)
            {
                Position = new Vector2(10, (int)Game.VirtualScreenHeight - 120),
                Width = 95,
                Height = 50,
                Text = "<XML/>",
                IsSelected = false,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _rawButton.Click += RawButtonClicked;
            _editButton = new Button(this)
            {
                Position = new Vector2(115, (int)Game.VirtualScreenHeight - 120),
                Width = 95,
                Height = 50,
                Text = "Upravit",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _editButton.Click += EditButtonClicked;
            _replayButton = new Button(this)
            {
                Position = new Vector2(10, (int)Game.VirtualScreenHeight - 180),
                Width = 200,
                Height = 50,
                Text = "Hrát znovu",
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _replayButton.Click += ReplayButtonClicked;
            _replayOptionButton = new Button(this)
            {
                Position = new Vector2(210, (int)Game.VirtualScreenHeight - 185),
                Width = 40,
                Height = 50,
                Text = "»",
                ZIndex = 90,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                BackgroundColor = Color.Transparent,
                BorderColor = Color.Transparent,
                TextRenderer = Game.FontRenderers["SegoeUI40Outl"]
            };
            _replayOptionButton.Click += ReplayOptionButtonClicked;
            _replayAsPlayer2Button = new Button(this)
            {
                Position = new Vector2(220, (int)Game.VirtualScreenHeight - 180),
                Width = 200,
                Height = 50,
                Text = "Jako 2",
                ZIndex = 95,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _replayAsPlayer2Button.Click += ReplayAsPlayer2ButtonClicked;
            _replayAsPlayer3Button = new Button(this)
            {
                Position = new Vector2(430, (int)Game.VirtualScreenHeight - 180),
                Width = 200,
                Height = 50,
                Text = "Jako 3",
                ZIndex = 95,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _replayAsPlayer3Button.Click += ReplayAsPlayer3ButtonClicked;
            _replayFromButton = new Button(this)
            {
                Position = new Vector2(640, (int)Game.VirtualScreenHeight - 180),
                Width = 200,
                Height = 50,
                Text = "Vyber štych",
                ZIndex = 95,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _replayFromButton.Click += ReplayFromButtonClicked;
            _review = new GameReview(this, (int)_origPosition.X + 40)
            {
                Position = _origPosition,
                Width = (int)Game.VirtualScreenWidth - 180,
                Height = (int)Game.VirtualScreenHeight - 65,
                BackgroundColor = Color.Transparent,
                ZIndex = 80
            };
            _rawData = new TextBox(this)
            {
                Position = _hiddenPosition,
                Width = (int)Game.VirtualScreenWidth - (int)_origPosition.X,
                Height = (int)Game.VirtualScreenHeight - 65,
                ZIndex = 80,
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Top,
                FontScaleFactor = 0.85f
            };
            _rawData.Hide();
            //tlacitka vpravo
            _sendButton = new Button(this)
            {
                Text = "@",
                Position = new Vector2(Game.VirtualScreenWidth - 60, Game.VirtualScreenHeight / 2f - 25),
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Right : AnchorType.Main,
                Width = 50
            };
            _sendButton.Click += SendBtnClicked;
        }

        private void EditButtonClicked(object sender)
        {
            Game.EditorScene.LoadGame(_newGamePath);
            Game.EditorScene.SetActive();
        }

        public void ShowGame(HistoryItem results, string newGamePath, string endGamePath, bool simulatedGame = false)
        {
            _review.Hide();
            _rawData.Hide();
            _description.Text = "Načítám hru ...";
            _rawButton.IsSelected = false;
            _rawData.Text = "";
            _rawData.Position = _hiddenPosition;
            _review.Position = _origPosition;
            _replayButton.IsEnabled = true; //Game.StorageAccessor.CheckStorageAccess();
            _replayOptionButton.IsEnabled = _replayButton.IsEnabled;
            _replayAsPlayer2Button.Hide();
            _replayAsPlayer3Button.Hide();
            _replayFromButton.Hide();
            Task.Run(() =>
            {
                try
                {
                    var g = new Mariasek.Engine.Game()
                    {
                        BaseBet = Game.Settings.BaseBet,
                        Locale = Game.Settings.Locale,
                        MaxWin = Game.Settings.MaxWin,
                        SkipBidding = false,
                        MinimalBidsForGame = Game.Settings.MinimalBidsForGame,
                        MinimalBidsForSeven = Game.Settings.MinimalBidsForSeven,
                        CalculationStyle = Game.Settings.CalculationStyle,
                        CountHlasAgainst = Game.Settings.CountHlasAgainst,
                        PlayZeroSumGames = Game.Settings.PlayZeroSumGames,
                        MandatoryDouble = Game.Settings.MandatoryDouble,
                        Top107 = Game.Settings.Top107,
                        Calculate107Separately = Game.Settings.Calculate107Separately,
                        HlasConsidered = Game.Settings.HlasConsidered,
                        AutoDisable100Against = Game.Settings.AutoDisable100Against,
                        //GetFileStream = GetFileStream,
                        //GetVersion = () => MariasekMonoGame.Version,
                        GameValue = Game.Settings.GameValue,
                        QuietSevenValue = Game.Settings.QuietSevenValue,
                        SevenValue = Game.Settings.SevenValue,
                        QuietHundredValue = Game.Settings.QuietHundredValue,
                        HundredValue = Game.Settings.HundredValue,
                        BetlValue = Game.Settings.BetlValue,
                        DurchValue = Game.Settings.DurchValue,
                        AllowAXTalon = Game.Settings.AllowAXTalon,
                        AllowTrumpTalon = Game.Settings.AllowTrumpTalon,
                        AllowAIAutoFinish = Game.Settings.AllowAIAutoFinish,
                        AllowPlayerAutoFinish = Game.Settings.AllowPlayerAutoFinish,
                        OptimisticAutoFinish = Game.Settings.OptimisticAutoFinish
                    };
                    g.RegisterPlayers(new Engine.AbstractPlayer[]
                                      {
                                          new DummyPlayer(g) { Name = Game.Settings.PlayerNames[0] },
                                          new DummyPlayer(g) { Name = Game.Settings.PlayerNames[1] },
                                          new DummyPlayer(g) { Name = Game.Settings.PlayerNames[2] }
                                      });
                    try
                    {
                        Game.StorageAccessor.GetStorageAccess();
                        using (var fs = File.Open(endGamePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            g.LoadGame(fs, calculateMoney: true, initialRound: 11);
                        }
                        _rawData.Text = string.Format("{0}\n\n", File.ReadAllText(endGamePath));
                    }
                    catch (Exception ex)
                    {
                        //ShowMsgLabel(string.Format("Error loading game:\n{0}", ex.Message), false);
                        return;
                    }
                    //Vysledky beru z g.Results ale penize a procenta u aktera beru z historie
                    var resultStr = g.Results.ToString().Split('\n');
                    var gameDate = new FileInfo(endGamePath).CreationTime;

                    var description = string.Format("{0}\n{1} {2}\n{3}\n{4}\t{5}\n{6}\t{7}\n{8}\t{9}",
                                                      gameDate.ToString("dd.MM.yyyy HH:mm"),
                                                      Path.GetFileName(endGamePath).Split('-')[0],
                                                      simulatedGame
                                                      ? $"{Path.GetFileName(endGamePath).Split('-')[1]}.{Game.MainScene.AmendSuitNameIfNeeded(g.GameType.ToDescription(g.trump))}"
                                                      : Game.MainScene.AmendSuitNameIfNeeded(g.GameType.ToDescription(g.trump)),
                                                      string.Join("\n", resultStr.Take(resultStr.Length - 3)
                                                                                 .Select(i => i.Split('\t')[0]
                                                                                               .Replace(" (", "\n(")
                                                                                               .Replace(", ", "\n"))
                                                                                 .ToArray()),
                                                      Game.Settings.PlayerNames[g.GameStartingPlayerIndex],
                                                      (results.MoneyWon[g.GameStartingPlayerIndex] * Game.Settings.BaseBet).ToString("C", Game.CurrencyFormat),
                                                      Game.Settings.PlayerNames[(g.GameStartingPlayerIndex + 1) % Mariasek.Engine.Game.NumPlayers],
                                                      (results.MoneyWon[(g.GameStartingPlayerIndex + 1) % Mariasek.Engine.Game.NumPlayers] * Game.Settings.BaseBet).ToString("C", Game.CurrencyFormat),
                                                      Game.Settings.PlayerNames[(g.GameStartingPlayerIndex + 2) % Mariasek.Engine.Game.NumPlayers],
                                                      (results.MoneyWon[(g.GameStartingPlayerIndex + 2) % Mariasek.Engine.Game.NumPlayers] * Game.Settings.BaseBet).ToString("C", Game.CurrencyFormat))
                                            .Split("\n");
                    if (description.Length > 12)
                    {
                        description = description.Where(i => !string.IsNullOrWhiteSpace(i)).ToArray();
                    }
                    description = description.Where(i => !i.StartsWith("Celkem:")).ToArray();
                    _description.FontScaleFactor = OriginalDescriptionFontScaleFactor;
                    _description.Text = string.Join("\n", description)
                                              .Replace("V červenejch:", "V červenejch");
                                              //.Replace("hraná sedma proti", "hraná sedma\nproti")
                                              //.Replace("hraný kilo proti", "hraný kilo\nproti");
                    _review.UpdateReview(g);

                    var biddingInfo = new string[Mariasek.Engine.Game.NumPlayers];
                    for (var i = 0; i < Mariasek.Engine.Game.NumPlayers; i++)
                    {
                        var playerName = string.Format("Player {0}", i + 1);

                        biddingInfo[i] = string.Join(" ", g.BiddingDebugInfo.ToString()
                                                           .Split('\n')
                                                           .Select(j => j.Split(':')
                                                                         .Select(k => k.Trim())
                                                                         .ToArray())
                                                           .Where(j => j.Length == 2 &&
                                                                       j[0] == playerName &&
                                                                       !j[1].StartsWith("Dobr") &&
                                                                       !j[1].StartsWith("Špatn"))
                                                           .Select(j => j[1])
                                                           .Distinct());
                    }
                    _review.Names[0].Text = string.Format("{0}: {1}{2}", Game.Settings.PlayerNames[g.GameStartingPlayerIndex],
                                                                         Game.MainScene.AmendSuitNameIfNeeded(biddingInfo[g.GameStartingPlayerIndex]),
                                                                         results.GameTypeConfidence < 0
                                                                            ? ""
                                                                            : string.Format(" ({0:0}%)", results.GameTypeConfidence * 100));
                    _review.Names[1].Text = string.Format("{0}: {1}", Game.Settings.PlayerNames[(g.GameStartingPlayerIndex + 1) % Mariasek.Engine.Game.NumPlayers],
                                                                      biddingInfo[(g.GameStartingPlayerIndex + 1) % Mariasek.Engine.Game.NumPlayers]);
                    _review.Names[2].Text = string.Format("{0}: {1}", Game.Settings.PlayerNames[(g.GameStartingPlayerIndex + 2) % Mariasek.Engine.Game.NumPlayers],
                                                                      biddingInfo[(g.GameStartingPlayerIndex + 2) % Mariasek.Engine.Game.NumPlayers]);
                    _review.Show();
                    _newGamePath = newGamePath;
                    _endGamePath = endGamePath;
                }
                catch(Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Unexpected error in ShowGame: {0}\n{1}", ex.Message, ex.StackTrace);
                    if (!MessageBox.IsVisible)
                    {
                        _ = MessageBox.Show("Chyba", ex.Message, new string[] { "OK" });
                    }
                }
            });
        }

        void BackButtonClicked(object sender)
        {
            Game.HistoryScene.SetActive();
        }

        void ReplayButtonClicked(object sender)
        {
            Game.MainScene.ImpersonationPlayerIndex = 0;
            Game.MainScene.ReplayGame(_newGamePath, 0);
        }

        void ReplayOptionButtonClicked(object sender)
        {
            var origPosition2 = _replayAsPlayer2Button.Position;
            var hiddenPosition2 = _replayButton.Position;
            var origPosition3 = _replayAsPlayer3Button.Position;
            var hiddenPosition3 = _replayButton.Position;
            var origPositionFrom = _replayFromButton.Position;
            var hiddenPositionFrom = _replayButton.Position;

            _replayAsPlayer2Button.Text = string.Format("Jako {0}", Game.Settings.PlayerNames[1]);
            _replayAsPlayer3Button.Text = string.Format("Jako {0}", Game.Settings.PlayerNames[2]);
            _replayOptionButton.Hide();
            _replayAsPlayer2Button.Position = hiddenPosition2;
            _replayAsPlayer3Button.Position = hiddenPosition3;
            _replayFromButton.Position = hiddenPositionFrom;
            _replayAsPlayer2Button.Show();
            _replayAsPlayer3Button.Show();
            _replayFromButton.Show();
            _replayAsPlayer2Button.MoveTo(origPosition2, 2000)
                                  .Wait(2000)
                                  .MoveTo(hiddenPosition2, 2000)
                                  .Invoke(() =>
                                  {
                                      _replayAsPlayer2Button.Hide();
                                      _replayAsPlayer2Button.Position = origPosition2;
                                  });
            _replayAsPlayer3Button.MoveTo(origPosition3, 2000)
                                  .Wait(2000)
                                  .MoveTo(hiddenPosition3, 2000)
                                  .Invoke(() =>
                                  {
                                      _replayAsPlayer3Button.Hide();
                                      _replayAsPlayer3Button.Position = origPosition3;
                                      _replayOptionButton.Show();
                                  });
            _replayFromButton.MoveTo(origPositionFrom, 2000)
                             .Wait(2000)
                             .MoveTo(hiddenPositionFrom, 2000)
                             .Invoke(() =>
                             {
                                 _replayFromButton.Hide();
                                 _replayFromButton.Position = origPositionFrom;
                                 _replayOptionButton.Show();
                             });
        }

        void ReplayAsPlayer2ButtonClicked(object sender)
        {
            Game.MainScene.ImpersonationPlayerIndex = 1;
            Game.MainScene.ReplayGame(_newGamePath, 0);
        }

        void ReplayAsPlayer3ButtonClicked(object sender)
        {
            Game.MainScene.ImpersonationPlayerIndex = 2;
            Game.MainScene.ReplayGame(_newGamePath, 0);
        }

        async void ReplayFromButtonClicked(object sender)
        {
            var text = await KeyboardInput.Show("Vyber počáteční štych", $"Od jakého kola chceš hru opakovat? (1-9)", "1");

            if (int.TryParse(text, out int initialRound) &&
                initialRound >= 1 &&
                initialRound < Mariasek.Engine.Game.NumRounds)
            {
                Game.MainScene.ImpersonationPlayerIndex = 0;
                Game.MainScene.ReplayGame(_endGamePath, initialRound);
            }
        }

        void RawButtonClicked(object sender)
        {
            if (_rawData.IsVisible)
            {
                _rawData.MoveTo(_hiddenPosition, 5000)
                        .Invoke(() =>
                        {
                            _rawData.Hide();
                            _review.Show();
                            _review.MoveTo(_origPosition, 5000);
                        });
            }
            else
            {
                _review.MoveTo(_hiddenPosition, 5000)
                       .Invoke(() =>
                       {
                           _review.Hide();
                           _rawData.Show();
                           _rawData.MoveTo(_origPosition, 5000);
                       });
            }
        }

        public void SendBtnClicked(object sender)
        {
            if (Game.EmailSender != null)
            {
//                using (var fs = Game.MainScene.GetFileStream(Path.GetFileName(_screenPath)))
//                {
//                    var target = _review.SaveTexture();
//#if !__IOS__
//                    target.SaveAsJpeg(fs, target.Width, target.Height);
//#endif
//                }

                var subject = $"Mariášek: komentář v{MariasekMonoGame.Version} ({MariasekMonoGame.Platform})";
                Game.EmailSender.SendEmail(new[] { "mariasek.app@gmail.com" }, subject, "Napište svůj komentář k této hře\n:",
                                           new[] { //_screenPath,
                                                   _newGamePath, _endGamePath, SettingsScene._settingsFilePath });
            }
        }
    }
}
