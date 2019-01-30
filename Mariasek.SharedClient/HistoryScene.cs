using System.Globalization;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using Mariasek.SharedClient.GameComponents;
using System.IO;
using System;
using Mariasek.Engine.New;
using System.Threading.Tasks;

namespace Mariasek.SharedClient
{
    public class HistoryScene : Scene
    {
#if __ANDROID__
        private static string _path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "Mariasek");
#else   //#elif __IOS__
        private static string _path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#endif
        private string _archivePath = Path.Combine(_path, "Archive");

        private Button _menuButton;
        private Button _viewGameButton;
        private ToggleButton _tableButton;
        private ToggleButton _chartButton;
        private Button _statsButton;
        private Button _resetHistoryButton;
        private Label _stat;
        private Label _header;
        private Label _player1;
        private Label _player2;
        private Label _player3;
        private Label _footer;
        private TextBox _historyBox;
        private LineChart _historyChart;
        private bool _useMockData;// = true;
        private Vector2 _origPosition;
        private Vector2 _hiddenPosition;
		private TouchLocation _touchDownLocation;
		private TouchLocation _touchHeldLocation;
        private bool _moneyDataFixupStarted;

        public HistoryScene(MariasekMonoGame game)
            : base(game)
        {
        }

        /// <summary>
        /// Allows the game component to perform any initialization it needs to before starting
        /// to run.  This is where it can query for any required services and load content.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            _origPosition = new Vector2(220, 60);
            _hiddenPosition = new Vector2(Game.VirtualScreenWidth, 60);
            _historyChart = new LineChart(this)
            {
                Position = _origPosition,
                Width = (int)Game.VirtualScreenWidth - 230,
                Height = (int)Game.VirtualScreenHeight - 120,
                Colors = new [] { Game.Settings.Player1Color, Game.Settings.Player2Color, Game.Settings.Player3Color },
                LineThickness = 3f,
                DataMarkerSize = 9f,
                TickMarkLength = 5f,
                GridInterval = new Vector2(1f, 10f),
                ShowHorizontalGridLines = false,
                ShowVerticalGridLines = false,
                SizeChartToFit = true
            };
            _historyChart.TouchDown += (sender, tl) => {
                _touchDownLocation = tl;
                _touchHeldLocation = tl;
            };
            _historyChart.TouchHeld += (sender, touchHeldTimeMs, tl) => {
                _touchHeldLocation = tl;
                return false;
            };
			_historyChart.Click += (sender) => {
				if (Vector2.Distance(_touchHeldLocation.Position, _touchDownLocation.Position) < 10)
				{
                    _historyChart.ToggleSizeChartToFit();
				}
			};
            _menuButton = new Button(this)
            {
                Position = new Vector2(10, (int)Game.VirtualScreenHeight - 60),
                Width = 200,
                Height = 50,
                Text = "Menu",
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _menuButton.Click += MenuClicked;
            _chartButton = new ToggleButton(this)
            {
                Position = new Vector2(10, (int)Game.VirtualScreenHeight - 180),
                Width = 95,
                Height = 50,
                Text = "Graf",
                IsSelected = true,
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _chartButton.Click += ChartButtonClicked;
            _tableButton = new ToggleButton(this)
            {
                Position = new Vector2(115, (int)Game.VirtualScreenHeight - 180),
                Width = 95,
                Height = 50,
                Text = "Tabulka",
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _tableButton.Click += TableButtonClicked;
            _statsButton = new Button(this)
            {
                Position = new Vector2(10, (int)Game.VirtualScreenHeight - 120),
                Width = 200,
                Height = 50,
                Text = "Statistiky",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _statsButton.Click += StatsButtonClicked;
            _resetHistoryButton = new Button(this)
            {
                Position = new Vector2(10, 10),
                Width = 200,
                Height = 50,
                Text = "Smazat historii",
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _resetHistoryButton.Click += ResetHistoryClicked;
			_stat = new Label(this)
			{
				Position = new Vector2(10, 70),
				Width = 200,
				Height = (int)Game.VirtualScreenHeight - 140,
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
			};
            _header = new Label(this)
            {
                Position = new Vector2(220, 20),
                Width = (int)Game.VirtualScreenWidth - 180,
                Height = 50,
                Text = "Výsledky:"
            };
            _player1 = new Label(this)
            {
                Position = new Vector2(350, 20),
                Width = 120,
                Height = 50,
                Text = Game.Settings.PlayerNames[0],
                TextColor = _historyChart.Colors[0]
            };
            _player2 = new Label(this)
            {
                Position = new Vector2(510, 20),
                Width = 120,
                Height = 50,
                Text = Game.Settings.PlayerNames[1],
                TextColor = _historyChart.Colors[1]
            };
            _player3 = new Label(this)
            {
                Position = new Vector2(670, 20),
                Width = 120,
                Height = 50,
                Text = Game.Settings.PlayerNames[2],
                TextColor = _historyChart.Colors[2]
            };
            _historyBox = new TextBox(this)
            {
                Position = _hiddenPosition,
                Width = (int)Game.VirtualScreenWidth - 230,
                Height = (int)Game.VirtualScreenHeight - 120,
                HorizontalAlign = HorizontalAlignment.Left,
				VerticalAlign = VerticalAlignment.Bottom,
                Tabs = new[]
                {
                    new Tab
                    {
                        TabPosition = 400,
                        TabAlignment = HorizontalAlignment.Right
                    },
                    new Tab
                    {
                        TabPosition = 560,
                        TabAlignment = HorizontalAlignment.Right
                    },
                    new Tab
                    {
                        TabPosition = 720,
                        TabAlignment = HorizontalAlignment.Right
                    }
                },
                FontScaleFactor = 0.9f,
                HighlightColor = Game.Settings.HighlightedTextColor,
                TapToHighlight = true
            };
            _historyBox.Hide();
            _viewGameButton = new Button(this)
            {
                Position = new Vector2(115, (int)Game.VirtualScreenHeight - 240),
                Text = "Náhled",
                Width = 95,//(int)Math.Round(_historyBox.TextRenderer.LineHeightAndSpacing * _historyBox.FontScaleFactor),//150,
                Height = 50,//(int)Math.Round(_historyBox.TextRenderer.LineHeightAndSpacing * _historyBox.FontScaleFactor)
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _viewGameButton.Click += ViewGameButtonClicked;
            _viewGameButton.Hide();
			_footer = new Label(this)
			{
				Position = new Vector2(220, Game.VirtualScreenHeight - 50),
				Width = (int)Game.VirtualScreenWidth - 230,
				Height = 50,
				TextColor = Game.Settings.HighlightedTextColor,
                Tabs = new[]
                {
                    new Tab
                    {
                        TabPosition = 400,
                        TabAlignment = HorizontalAlignment.Right
                    },
                    new Tab
                    {
                        TabPosition = 560,
                        TabAlignment = HorizontalAlignment.Right
                    },
                    new Tab
                    {
                        TabPosition = 720,
                        TabAlignment = HorizontalAlignment.Right
                    }
                }
			};
            Background = Game.Assets.GetTexture("wood2");
            BackgroundTint = Color.DimGray;

            PopulateControls();
        }

        public void PopulateControls()
        {
            var sb = new StringBuilder();
            int wins = 0, total = 0;
            var series = new Vector2[Mariasek.Engine.New.Game.NumPlayers][];
            var numFormat = (NumberFormatInfo)CultureInfo.GetCultureInfo("cs-CZ").NumberFormat.Clone();

            numFormat.CurrencyGroupSeparator = "";
            if (_useMockData)
            {
                for (var i = 0; i < 20; i++)
                {
                    sb.AppendFormat("\t\t{0}\t{1}\t{2}\n", 
                        ((i + 1) * 1f).ToString("C", numFormat), 
                        ((i + 1) * -0.5f).ToString("C", numFormat), 
                        ((i + 1) * -0.5f).ToString("C", numFormat));
                }
            }
            var maxWon = 0f;
            var maxLost = 0f;
            var sums = new float[series.Length];

            for (var i = 0; i < series.Length; i++)
            {
                series[i] = new Vector2[Game.Money.Count + 1];
                series[i][0] = Vector2.Zero;

                for (var j = 0; j < Game.Money.Count; j++)
                {
                    sums[i] += Game.Money[j].MoneyWon[i] * Game.Settings.BaseBet;
                    series[i][j + 1] = new Vector2(j + 1, sums[i]);
                    if (maxWon < sums[i])
                    {
                        maxWon = sums[i];
                    }
                    if (maxLost > sums[i])
                    {
                        maxLost = sums[i];
                    }
                }
            }
            _historyChart.MaxValue = new Vector2(Game.Money.Count, maxWon);
            _historyChart.MinValue = new Vector2(0, maxLost);
            if (Game.Settings != null)
            {
                _historyChart.GridInterval = new Vector2(1f, 10 * Game.Settings.BaseBet);
            }
            _historyChart.Data = series;
            //_historyChart.Click += (sender) => 
            //{
            //    _historyChart.MaxValue = new Vector2(_historyChart.Data[0].Length, maxWon);
            //};
            foreach (var historyItem in Game.Money)
            {
                sb.AppendFormat(" {0,-7}\t{1}\t{2}\t{3}\n",
                                string.IsNullOrWhiteSpace(historyItem.GameTypeString) ? "?": historyItem.GameTypeString,
                                (historyItem.MoneyWon[0] * Game.Settings.BaseBet).ToString("C", numFormat), 
                                (historyItem.MoneyWon[1] * Game.Settings.BaseBet).ToString("C", numFormat), 
                                (historyItem.MoneyWon[2] * Game.Settings.BaseBet).ToString("C", numFormat));
                if (historyItem.MoneyWon[0] > 0)
                {
                    wins++;
                }
                total++;
            }
            var ratio = total != 0 ? (wins * 100f / total) : 0f;

            _historyBox.Text = sb.ToString();
            _historyChart.ScrollToEnd();
			_historyBox.ScrollToBottom();
            _stat.Text = string.Format("Odehráno her:\n{0}\nZ toho výher:\n{1}\nPoměr: {2:N0}%\nPříště začíná:\n{3}", 
                total, wins, ratio, 
                Game.Settings.PlayerNames[(Game.MainScene.CurrentStartingPlayerIndex + 1) % Mariasek.Engine.New.Game.NumPlayers]);

            var sum1 = Game.Money.Sum(i => i.MoneyWon[0] * Game.Settings.BaseBet).ToString("C", numFormat);
            var sum2 = Game.Money.Sum(i => i.MoneyWon[1] * Game.Settings.BaseBet).ToString("C", numFormat);
            var sum3 = Game.Money.Sum(i => i.MoneyWon[2] * Game.Settings.BaseBet).ToString("C", numFormat);

            _footer.Text = string.Format("Součet:\t{0}\t{1}\t{2}", sum1, sum2, sum3);

            //Elements of _archiveGames hold GameId or 0 if the game is not archived
            var noidGames = Game.Money.Count(i => i.GameId == 0);

            if (noidGames > Game.Money.Count() / 2 && !_moneyDataFixupStarted)
            {
                _moneyDataFixupStarted = true;
                Task.Run(() =>
                {
                    try
                    {

                        var k = 0;
                        for (var i = 0; i < Game.Money.Count(); i++)
                        {
                            if (Game.Money[i].IsArchived)
                            {
                                var n = Game.Money[i].GameId == 0 ? k + 1 : Game.Money[i].GameId;
                                var files = Directory.GetFiles(_archivePath, string.Format("{0:0000}-{1}*.hra", n, Game.Money[i].GameTypeString.Trim().ToLower()));

                                if (files.Length == 2)
                                {
                                    if (Game.Money[i].GameId == 0)
                                    {
                                        Game.Money[i].GameId = n;
                                        k = n;
                                    }
                                }
                                else
                                {
                                    Game.Money[i].GameId = 0;
                                }
                            }
                        }
                        //chci ziskat ostre rostouci posloupnost (nuly ignoruju)
                        //vyhodime duplikaty, nechavam si vzdy posledni vyskyt, ostatni mazu
                        //zbavime se zaroven i pripadnych nesetridenych prvku
                        var minimum = int.MaxValue;
                        for (var i = Game.Money.Count() - 1; i >= 0; i--)
                        {
                            if (Game.Money[i].GameId > 0)
                            {
                                if (Game.Money[i].GameId < minimum)
                                {
                                    minimum = Game.Money[i].GameId;
                                }
                                else
                                {
                                    Game.Money[i].GameId = 0;
                                }
                            }
                        }
                        Game.MainScene.SaveHistory();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("Cannot fixup history\n{0}", ex.Message));
                    }
                });
            }
        }

        private void ChartButtonClicked(object sender)
        {
            if (_historyChart.IsVisible)
            {
                _historyChart.MoveTo(_hiddenPosition, 5000)
                             .Invoke(() =>
                             {
                                 _historyChart.Hide();
                                 _historyBox.Show();
                                 _historyBox.MoveTo(_origPosition, 5000);
                                 _tableButton.IsSelected = true;
                                 _chartButton.IsSelected = false;
                             });
            }
            else
            {
                _historyBox.MoveTo(_hiddenPosition, 5000)
                            .Invoke(() =>
                            {
                                _historyBox.Hide();
                                _historyChart.Show();
                                _historyChart.MoveTo(_origPosition, 5000);
                                _chartButton.IsSelected = true;
                                _tableButton.IsSelected = false;
                            });
            }
        }

        private void TableButtonClicked(object sender)
        {
            ChartButtonClicked(sender);
            //if (_historyBox.IsVisible)
            //{
            //    _historyBox.MoveTo(_hiddenPosition, 5000)
            //               .Invoke(() => { _historyBox.Hide(); });
            //}
            //else
            //{
            //    _historyBox.Invoke(() => { _historyBox.Show(); })
            //        .MoveTo(_origPosition, 5000);
            //}
        }

        private void StatsButtonClicked(object sender)
        {
            Game.StatScene.PopulateControls();
            Game.StatScene.SetActive();
        }

        private void ResetHistoryClicked(object sender)
        {
            _useMockData = false;
            Game.Money.Clear();
            Game.MainScene.DeleteArchiveFolder();
            PopulateControls();
            Game.MainScene.SaveHistory();
        }

        private void ViewGameButtonClicked(object sender)
        {
            if (_historyBox.HighlightedLine >= 0 &&
                _historyBox.HighlightedLine < Game.Money.Count())
            {
                var historicGame = Game.Money[_historyBox.HighlightedLine];
                if (historicGame.GameId <= 0)
                {
                    return;
                }
                var pattern = string.Format("{0:0000}-*.hra", historicGame.GameId);
                var files = Directory.GetFiles(_archivePath, pattern).OrderBy(i => i).ToArray();

                if (historicGame.GameId > 0 &&
                    files.Length == 2)
                {
                    var newGameFilePath = files[0];
                    var endGameFilePath = files[1];

                    if (File.Exists(newGameFilePath) &&
                        File.Exists(endGameFilePath))
                    {
                        Game.ReviewScene.ShowGame(historicGame, newGameFilePath, endGameFilePath);
                        Game.ReviewScene.SetActive();
                    }
                }
            }
        }

        private void MenuClicked(object sender)
        {
            Game.MenuScene.SetActive();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            _historyChart.IsEnabled = !_historyBox.IsVisible;

            if (_historyBox.IsVisible &&
                _historyBox.HighlightedLine >= 0 &&
                _historyBox.HighlightedLine < Game.Money.Count() &&
                Game.Money[_historyBox.HighlightedLine].GameId > 0)
            {
                //_viewGameButton.Position = new Vector2(753, _historyBox.HighlightedLineBoundsRect.Top);
                //_viewGameButton.Height = _historyBox.HighlightedLineBoundsRect.Height;
                _viewGameButton.Show();
            }
            else
            {
                _viewGameButton.Hide();
            }
        }
    }
}
