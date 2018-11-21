using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mariasek.Engine.New;
using Mariasek.SharedClient.GameComponents;
using Microsoft.Xna.Framework;

namespace Mariasek.SharedClient
{
	public class ReviewScene : Scene
    {
#if __ANDROID__
        private static string _path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "Mariasek");
#else   //#elif __IOS__
        private static string _path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#endif
        private string _savedGameFilePath = Path.Combine(_path, "_temp.hra");
        private string _screenPath = Path.Combine(_path, "screen.png");

        Button _backButton;
        Button _replayButton;
        ToggleButton _rawButton;
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

            _origPosition = new Vector2(160, 45);
            _hiddenPosition = new Vector2(Game.VirtualScreenWidth, 45);

            _description = new TextBox(this)
            {
                Position = new Vector2(10, 10),
                Width = 200,
                Height = (int)Game.VirtualScreenHeight - 200,
                FontScaleFactor = 0.85f,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                HighlightColor = Color.Yellow,
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Top
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
                Width = 200,
                Height = 50,
                Text = "<XML/>",
                IsSelected = false,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _rawButton.Click += RawButtonClicked;
            _replayButton = new Button(this)
            {
                Position = new Vector2(10, (int)Game.VirtualScreenHeight - 180),
                Width = 200,
                Height = 50,
                Text = "Hrát znovu",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _replayButton.Click += ReplayButtonClicked;
            _review = new GameReview(this)
            {
                Position = _origPosition,
                Width = (int)Game.VirtualScreenWidth - 160,
                Height = (int)Game.VirtualScreenHeight - 65,
                BackgroundColor = Color.Transparent,
                ZIndex = 100
            };
            _rawData = new TextBox(this)
            {
                Position = _hiddenPosition,
                Width = (int)Game.VirtualScreenWidth - 160,
                Height = (int)Game.VirtualScreenHeight - 65,
                ZIndex = 100,
                BackgroundColor = Color.TransparentBlack,
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Top,
                FontScaleFactor = 0.9f
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

        public void ShowGame(MoneyCalculatorBase results, string newGamePath, string endGamePath)
        {
            _review.Hide();
            _rawData.Hide();
            _description.Text = "Načítám hru ...";
            _rawButton.IsSelected = false;
            _rawData.Text = "";
            _rawData.Position = _hiddenPosition;
            _review.Position = _origPosition;
            Task.Run(() =>
            {
                var g = new Mariasek.Engine.New.Game()
                {
                    BaseBet = Game.Settings.BaseBet,
                    MaxWin = Game.Settings.MaxWin,
                    SkipBidding = false,
                    MinimalBidsForGame = Game.Settings.MinimalBidsForGame,
                    MinimalBidsForSeven = Game.Settings.MinimalBidsForSeven,
                    CalculationStyle = Game.Settings.CalculationStyle,
                    Top107 = Game.Settings.Top107,
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
                    AllowPlayerAutoFinish = Game.Settings.AllowPlayerAutoFinish
                };
                g.RegisterPlayers(new Engine.New.AbstractPlayer[]
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
                        g.LoadGame(fs, true);
                    }
                    _rawData.Text = string.Format("{0}\n\n", File.ReadAllText(endGamePath));
                }
                catch (Exception ex)
                {
                    //ShowMsgLabel(string.Format("Error loading game:\n{0}", ex.Message), false);
                    return;
                }
                //Vysledky beru z g.Results ale penize beru z historie
                var resultStr = g.Results.ToString().Split('\n');
                var numFormat = (NumberFormatInfo)CultureInfo.GetCultureInfo("cs-CZ").NumberFormat.Clone();
                var gameDate = new FileInfo(endGamePath).CreationTime;
                _description.Tabs = new[] { new Tab() { TabAlignment = HorizontalAlignment.Right, TabPosition = 180 } };
                _description.Text = string.Format("{0}\n{1} {2}\n{3}\n{4}\t{5}\n{6}\t{7}\n{8}\t{9}",
                                                  gameDate.ToString("dd.MM.yyyy HH:mm"),
                                                  Path.GetFileName(endGamePath).Split('-')[0],
                                                  g.GameType.ToDescription(g.trump),
                                                  string.Join("\n", resultStr.Take(resultStr.Length - 3)
                                                                             .Select(i => i.Split('\t')[0]
                                                                                           .Replace(" (", "\n(")
                                                                                           .Replace(", ", "\n"))
                                                                             .ToArray()),
                                                  Game.Settings.PlayerNames[g.GameStartingPlayerIndex],
                                                  (results.MoneyWon[g.GameStartingPlayerIndex] * Game.Settings.BaseBet).ToString("C", numFormat),
                                                  Game.Settings.PlayerNames[(g.GameStartingPlayerIndex + 1) % Mariasek.Engine.New.Game.NumPlayers],
                                                  (results.MoneyWon[(g.GameStartingPlayerIndex + 1) % Mariasek.Engine.New.Game.NumPlayers] * Game.Settings.BaseBet).ToString("C", numFormat),
                                                  Game.Settings.PlayerNames[(g.GameStartingPlayerIndex + 2) % Mariasek.Engine.New.Game.NumPlayers],
                                                  (results.MoneyWon[(g.GameStartingPlayerIndex + 2) % Mariasek.Engine.New.Game.NumPlayers] * Game.Settings.BaseBet).ToString("C", numFormat));
                _description.HighlightedLine = 2;
                _review.UpdateReview(g);
                _review.Show();
                _newGamePath = newGamePath;
                _endGamePath = endGamePath;
            });
        }

        void BackButtonClicked(object sender)
        {
            Game.HistoryScene.SetActive();
        }

        void ReplayButtonClicked(object sender)
        {
            Game.MainScene.ReplayGame(_newGamePath);
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
#if !__IOS__
                using (var fs = Game.MainScene.GetFileStream(Path.GetFileName(_screenPath)))
                {
                    var target = _review.SaveTexture();
                    target.SaveAsPng(fs, target.Width, target.Height);
                }
#endif

                var subject = $"Mariášek: komentář v{MariasekMonoGame.Version} ({MariasekMonoGame.Platform})";
                Game.EmailSender.SendEmail(new[] { "mariasek.app@gmail.com" }, subject, "Sdělte mi prosím své dojmy nebo komentář ke konkrétní hře\n:",
                                           new[] { _screenPath, _newGamePath, _endGamePath, SettingsScene._settingsFilePath });
            }
        }
    }
}
