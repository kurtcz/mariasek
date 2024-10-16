﻿using System.Globalization;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Mariasek.SharedClient.GameComponents;
using System.IO;
using System;
using System.Collections.Generic;

namespace Mariasek.SharedClient
{
    public class HistoryScene : Scene
    {
        private string _archivePath = Path.Combine(MariasekMonoGame.RootPath, "Archive");
        private string _simulationsPath = Path.Combine(MariasekMonoGame.RootPath, "Simulations");

        private Button _menuButton;
        private ToggleButton _simButton;
        private Button _viewGameButton;
        private ToggleButton _tableButton;
        private ToggleButton _chartButton;
        private Button _statsButton;
        private Button _resetHistoryButton;
        private LeftRightSelector _daysToShowSelector;
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
        private Func<HistoryItem, bool> _filter;
        private HistoryItem[] _filteredItems;

        private int _lastHistorySize;

        public HistoryScene(MariasekMonoGame game)
            : base(game)
        {
            Game.SettingsChanged += SettingsChanged;
        }

        /// <summary>
        /// Allows the game component to perform any initialization it needs to before starting
        /// to run.  This is where it can query for any required services and load content.
        /// </summary>
        public override void Initialize()
        {
            const float originalDataMarkerSize = 9f;
            base.Initialize();

            _origPosition = new Vector2(220, 60);
            _hiddenPosition = new Vector2(Game.VirtualScreenWidth, 60);
            _historyChart = new LineChart(this)
            {
                Position = _origPosition,
                Width = (int)Game.VirtualScreenWidth - 230,
                Height = (int)Game.VirtualScreenHeight - 120,
                Colors = new[] { Game.Settings.Player1Color, Game.Settings.Player2Color, Game.Settings.Player3Color },
                LineThickness = 3f,
                DataMarkerSize = originalDataMarkerSize,
                TickMarkLength = 5f,
                GridInterval = new Vector2(1f, 10f),
                ShowHorizontalGridLines = false,
                ShowVerticalGridLines = false,
                SizeChartToFit = true,
                UseSplineCorrection = true
            };
            _historyChart.TouchDown += (sender, tl) => {
                _touchDownLocation = tl;
                _touchHeldLocation = tl;
            };
            _historyChart.TouchHeld += (sender, touchHeldTimeMs, tl) => {
                _touchHeldLocation = tl;
                return false;
            };
			//_historyChart.Click += (sender) => {
			//	if (Vector2.Distance(_touchHeldLocation.Position, _touchDownLocation.Position) < 10)
			//	{
            //        _historyChart.ToggleSizeChartToFit();
			//	}
			//};
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
            _daysToShowSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(0, 70),
                Width = 220,
                Height = 50,
                Items = new SelectorItems() { { "Celá historie", 0 }, { "Dnešek", 1 }, { "7 dní", 7 }, { "14 dní", 14 }, { "30 dní", 30 }, { "60 dní", 60 }, { "90 dní", 90 } },
                UseCommonScissorRect = true,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _daysToShowSelector.SelectedIndex = _daysToShowSelector.Items.FindIndex(Game.Settings.HistoryDaysToShow);
            _daysToShowSelector.SelectionChanged += DaysToShowChanged;
            _stat = new Label(this)
			{
				Position = new Vector2(10, 120),
				Width = 210,
				Height = (int)Game.VirtualScreenHeight - 335,
                Tabs = new Tab[] { new Tab{ TabAlignment = HorizontalAlignment.Right, TabPosition = 210 } },
                FontScaleFactor = 0.9f,
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
                Position = new Vector2(390, 20),
                Width = 120,
                Height = 50,
                Text = Game.Settings.PlayerNames[0],
                TextColor = _historyChart.Colors[0]
            };
            _player2 = new Label(this)
            {
                Position = new Vector2(530, 20),
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
                        TabPosition = 280,
                        TabAlignment = HorizontalAlignment.Left
                    },
                    new Tab
                    {
                        TabPosition = 440,
                        TabAlignment = HorizontalAlignment.Right
                    },
                    new Tab
                    {
                        TabPosition = 580,
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
                TapToHighlight = true,
                UseCommonScissorRect = false
            };
            _historyBox.Hide();
            _simButton = new ToggleButton(this)
            {
                Position = new Vector2(10, (int)Game.VirtualScreenHeight - 240),
                Width = 95,
                Height = 50,
                Text = " Simulace ",
                IsSelected = false,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _simButton.Click += SimButtonClicked;
#if !DEBUG
            _simButton.Hide();
#endif
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
                        TabPosition = 440,
                        TabAlignment = HorizontalAlignment.Right
                    },
                    new Tab
                    {
                        TabPosition = 580,
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
            var ticksToShow = Game.Settings.HistoryDaysToShow * 864000000000L;

            _filter = (HistoryItem i) => Game.Settings.HistoryDaysToShow <= 0 ||
                                         DateTime.Today.Ticks - i.DateTime.Date.Ticks < ticksToShow;

            _simButton.IsEnabled = Game.Simulations.Any();
            if (!_simButton.IsEnabled && _simButton.IsSelected)
            {
                _simButton.IsSelected = false;
            }
            if (_simButton.IsSelected)
            {
                _filteredItems = Game.Simulations.ToArray();
            }
            else
            {
                _filteredItems = Game.Money.Where(_filter).ToArray();

                if (_filteredItems.Length > 0 &&
                    _filteredItems.Length == _lastHistorySize)
                {
                    return;
                }
            }

            var sb = new StringBuilder();
            int played = 0, wins = 0, total = 0;
            var gameCount = _filteredItems.Length;
            var seriesData = new int[Mariasek.Engine.Game.NumPlayers][];
            var numFormat = (NumberFormatInfo)Game.CurrencyFormat.Clone();

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
            var maxWon = 0;
            var maxLost = 0;
            var sums = new int[seriesData.Length];
            var lastGameId = 0;

            for (var i = 0; i < seriesData.Length; i++)
            {
                seriesData[i] = new int[gameCount + 1];
                seriesData[i][0] = 0;

                for (var j = 0; j < gameCount; j++)
                {
                    sums[i] += _filteredItems[j].MoneyWon[i];
                    seriesData[i][j + 1] = sums[i];
                    lastGameId = Math.Max(lastGameId, _filteredItems[j].GameId);
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

            var series = AdjustChartData(seriesData, _historyChart.Width);

            _historyChart.MaxValue = new Vector2(series[0].Length, maxWon);
            _historyChart.MinValue = new Vector2(0, maxLost);

            if (Game.Settings != null)
            {
                //_historyChart.GridInterval = new Vector2(1f, 10 * Game.Settings.BaseBet);
                var gridInterval = (int)Math.Pow(10, Math.Round(Math.Log10(_historyChart.MaxValue.Y - _historyChart.MinValue.Y)) - 1);

                if (gridInterval == 0)
                {
                    _historyChart.GridInterval = new Vector2(1, 0.1f);
                }
                else
                {
                    if ((_historyChart.MaxValue.Y - _historyChart.MinValue.Y) / gridInterval <= 2)
                    {
                        gridInterval /= 10;
                    }
                    else if ((_historyChart.MaxValue.Y - _historyChart.MinValue.Y) / gridInterval <= 3)
                    {
                        gridInterval /= 4;
                    }
                    _historyChart.GridInterval = new Vector2(1, gridInterval);
                }
            }

            if (seriesData[0].Length > _historyChart.Width)
            {
                _historyChart.LineThickness = 2;
                _historyChart.DataMarkerSize = 5;
                _historyChart.TickMarkLength = 10;
            }
            _historyChart.Data = series;
            //_historyChart.Click += (sender) => 
            //{
            //    _historyChart.MaxValue = new Vector2(_historyChart.Data[0].Length, maxWon);
            //};
            var files = _simButton.IsSelected ? Directory.GetFiles(_simulationsPath, "*.hra").OrderBy(i => i).ToArray() : null;
            
            foreach (var historyItem in _filteredItems)
            {
                if (Game.Settings.MaxHistoryTableLength <= 0 ||
                    _filteredItems.Length - total < Game.Settings.MaxHistoryTableLength)
                {
                    //tabulka zobrazuje jen poslednich MaxHistoryTableLength zaznamu
                    var format = lastGameId < 10000 ? " {0:D4}\t{1}\t{2}\t{3}\t{4}\n" : " {0:D5}\t{1}\t{2}\t{3}\t{4}\n";
                    sb.AppendFormat(format,
                                    (historyItem.GameIdSpecified ? (historyItem.GameId % 100000).ToString(lastGameId < 10000 ? "D4" : "D5") : "-----"),
                                    _simButton.IsSelected
                                    ? $"{files[historyItem.GameId - 1].Split("-")[1]}.{historyItem.GameTypeString}"
                                    : (string.IsNullOrWhiteSpace(historyItem.GameTypeString) ? "?" : historyItem.GameTypeString),
                                    (historyItem.MoneyWon[0] * Game.Settings.BaseBet).ToString("C", numFormat),
                                    (historyItem.MoneyWon[1] * Game.Settings.BaseBet).ToString("C", numFormat),
                                    (historyItem.MoneyWon[2] * Game.Settings.BaseBet).ToString("C", numFormat));
                }
                if (historyItem.GameIdSpecified)
                {
                    if (historyItem.MoneyWon[0] > 0)
                    {
                        wins++;
                    }
                    played++;
                }
                total++;
            }
            var ratio = played != 0 ? (wins * 100f / played) : 0f;

            _historyBox.FontScaleFactor = lastGameId < 10000 ? 0.9f : 0.8f;
            _historyBox.Text = sb.ToString().TrimEnd();
            _historyChart.ScrollToEnd();
            _historyBox.ScrollToBottom();
            _stat.Text = string.Format("Odehráno her:\t{0}\nZ toho výher:\t{1}\nPoměr výher:\t{2:N0}%\nPříště začíná:\n{3}",
                played, wins, ratio,
                Game.Settings.PlayerNames[(Game.MainScene.CurrentStartingPlayerIndex + 1) % Mariasek.Engine.Game.NumPlayers]);

            lock (Game.Money.SyncRoot)
            {
                var sum1 = _filteredItems.Sum(i => i.MoneyWon[0] * Game.Settings.BaseBet).ToString("C", numFormat);
                var sum2 = _filteredItems.Sum(i => i.MoneyWon[1] * Game.Settings.BaseBet).ToString("C", numFormat);
                var sum3 = _filteredItems.Sum(i => i.MoneyWon[2] * Game.Settings.BaseBet).ToString("C", numFormat);

                _footer.Text = string.Format("Součet:\t{0}\t{1}\t{2}", sum1, sum2, sum3);

                _lastHistorySize = _filteredItems.Length;
            }
        }

        private Vector2[][] AdjustChartData(int[][] data, int buckets)
        {
            var result = new Vector2[data.Length][];

            for(var i = 0; i < data.Length; i++)
            {
                var bucketSize = data[i].Length / (double)buckets;

                if (data[i].Length > buckets)
                {
                    result[i] = new Vector2[4 * buckets];

                    for (var j = 0; j < buckets; j++)
                    {
                        var startIndex = (int)(j * bucketSize);
                        var endIndex = Math.Min((int)((j + 1) * bucketSize), data[i].Length - 1);

                        var firstValue = data[i][startIndex];
                        var lastValue = data[i][endIndex];
                        var minValue = float.MaxValue;
                        var maxValue = float.MinValue;

                        for (var k = startIndex; k <= endIndex; k++)
                        {
                            minValue = Math.Min(minValue, data[i][k]);
                            maxValue = Math.Max(maxValue, data[i][k]);
                        }

                        result[i][4 * j] = new Vector2(4 * j, firstValue);
                        result[i][4 * j + 1] = new Vector2(4 * j + 1, minValue);
                        result[i][4 * j + 2] = new Vector2(4 * j + 2, maxValue);
                        result[i][4 * j + 3] = new Vector2(4 * j + 3, lastValue);
                    }
                }
                else
                {
                    result[i] = new Vector2[data[i].Length];

                    for (var j = 0; j < data[i].Length; j++)
                    {
                        result[i][j] = new Vector2(j, data[i][j]);
                    }
                }
            }

            return result;
        }

        private void SettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            Game.MainScene.UpdateToggleButtons(this);
        }

        private void DaysToShowChanged(object sender)
        {
            Game.Settings.HistoryDaysToShow = (int)(sender as LeftRightSelector).SelectedValue;
            Game.SaveGameSettings();
            PopulateControls();
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
                                 _viewGameButton.Show();
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
                                _viewGameButton.Hide();
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

        private async void ResetHistoryClicked(object sender)
        {
            if (MessageBox.IsVisible)
            {
                return;
            }
            var buttonIndex = await MessageBox.Show("Varování", $"Opravdu si přejete smazat historii?", new string[] { "Zpět", "Smazat" });
            
            if (buttonIndex.HasValue && buttonIndex.Value == 1)
            {
                _useMockData = false;
                Game.Money.Clear();
                try
                {
                    Game.MainScene.DeleteArchiveFolder();
                }
                catch(Exception ex)
                {
                    if (!MessageBox.IsVisible)
                    {
                        await MessageBox.Show("Chyba", ex.Message, new string[] { "OK" });
                    }
                }
                RunOnUiThread(() =>
                {
                    PopulateControls();
                });
                Game.MainScene.SaveHistory();
            }
        }

        private void SimButtonClicked(object sender)
        {
            _lastHistorySize = -1;  //force invalidate
            PopulateControls();
        }

        private void ViewGameButtonClicked(object sender)
        {
            try
            {
                if (_historyBox.HighlightedLine >= 0 &&
                    _historyBox.HighlightedLine < _filteredItems.Length)
                {
                    var historicGame = _filteredItems.ToArray()[_historyBox.HighlightedLine];
                    if (historicGame.GameId <= 0)
                    {
                        return;
                    }
                    Game.StorageAccessor.GetStorageAccess();
                    if (_simButton.IsSelected)
                    {
                        var files = Directory.GetFiles(_simulationsPath, "*.hra").OrderBy(i => i).ToArray();
                        if (historicGame.GameId > 0)
                        {
                            var endGameFilePath = files[_historyBox.HighlightedLine];

                            if (File.Exists(endGameFilePath))
                            {
                                Game.ReviewScene.ShowGame(historicGame, endGameFilePath, endGameFilePath, true);
                                Game.ReviewScene.SetActive();
                            }
                        }
                    }
                    else
                    {
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
            }
            catch(Exception ex)
            {
                if (!MessageBox.IsVisible)
                {
                    _ = MessageBox.Show("Chyba", ex.Message, new string[] { "OK" });
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
                _historyBox.HighlightedLine < _filteredItems.Count() &&
                _filteredItems.ToArray()[_historyBox.HighlightedLine].GameId > 0)
            {
                //_viewGameButton.Position = new Vector2(753, _historyBox.HighlightedLineBoundsRect.Top);
                //_viewGameButton.Height = _historyBox.HighlightedLineBoundsRect.Height;
                //_viewGameButton.Show();
                _viewGameButton.IsEnabled = true;
            }
            else
            {
                //_viewGameButton.Hide();
                _viewGameButton.IsEnabled = false;
            }
        }
    }
}
