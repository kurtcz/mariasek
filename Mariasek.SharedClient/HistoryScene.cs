using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

using Mariasek.SharedClient.GameComponents;

namespace Mariasek.SharedClient
{
    public class HistoryScene : Scene
    {
        private Button _menuButton;
        private ToggleButton _tableButton;
        private ToggleButton _chartButton;
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
        private GameSettings _settings;
        private Vector2 _origPosition;
        private Vector2 _hiddenPosition;

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
                LineThickness = 3f,
                DataMarkerSize = 9f,
                TickMarkLength = 5f,
                GridInterval = new Vector2(1f, 10f),
                ShowHorizontalGridLines = false,
                ShowVerticalGridLines = false
            };
            _menuButton = new Button(this)
            {
                Position = new Vector2(10, 10),
                Width = 200,
                Height = 50,
                Text = "Menu",
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _menuButton.Click += MenuClicked;
            _chartButton = new ToggleButton(this)
            {
                Position = new Vector2(10, (int)Game.VirtualScreenHeight - 120),
                Width = 95,
                Height = 50,
                Text = "Graf",
                IsSelected = true,
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _chartButton.Click += ChartButtonClicked;
            _tableButton = new ToggleButton(this)
            {
                Position = new Vector2(115, (int)Game.VirtualScreenHeight - 120),
                Width = 95,
                Height = 50,
                Text = "Tabulka",
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _tableButton.Click += TableButtonClicked;
            _resetHistoryButton = new Button(this)
            {
                Position = new Vector2(10, (int)Game.VirtualScreenHeight - 60),
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
                Text = "Hráč 1",
                TextColor = _historyChart.Colors[0]
            };
            _player2 = new Label(this)
            {
                Position = new Vector2(485, 20),
                Width = 120,
                Height = 50,
                Text = "Hráč 2 (AI)",
                TextColor = _historyChart.Colors[1]
            };
            _player3 = new Label(this)
            {
                Position = new Vector2(630, 20),
                Width = 120,
                Height = 50,
                Text = "Hráč 3 (AI)",
                TextColor = _historyChart.Colors[2]
            };
            _historyBox = new TextBox(this)
            {
                Position = _hiddenPosition,
                Width = (int)Game.VirtualScreenWidth - 230,
                Height = (int)Game.VirtualScreenHeight - 120,
                HorizontalAlign = HorizontalAlignment.Left,
				VerticalAlign = VerticalAlignment.Bottom
            };
            _historyBox.Hide();
			_footer = new Label(this)
			{
				Position = new Vector2(220, Game.VirtualScreenHeight - 50),
				Width = (int)Game.VirtualScreenWidth - 230,
				Height = 50,
				TextColor = Color.Yellow
			};
            Background = Game.Content.Load<Texture2D>("wood2");
            BackgroundTint = Color.DimGray;

            Game.SettingsScene.SettingsChanged += SettingsChanged;

            PopulateControls();
        }

        public void PopulateControls()
        {            
            var culture = CultureInfo.CreateSpecificCulture("cs-CZ");
            var sb = new StringBuilder();
            int wins = 0, total = 0;
            var series = new Vector2[Mariasek.Engine.New.Game.NumPlayers][];

            if (_useMockData)
            {
                for (var i = 0; i < 20; i++)
                {
                    sb.AppendFormat("\t\t{0}\t{1}\t{2}\n", 
                        ((i + 1) * 1f).ToString("C", culture), 
                        ((i + 1) * -0.5f).ToString("C", culture), 
                        ((i + 1) * -0.5f).ToString("C", culture));
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
                    sums[i] += Game.Money[j].MoneyWon[i];
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
            if (_settings != null)
            {
                _historyChart.GridInterval = new Vector2(1f, 10 * _settings.BaseBet);
            }
            _historyChart.Data = series;

            foreach (var historyItem in Game.Money)
            {                
                sb.AppendFormat("\t\t{0}\t{1}\t{2}\n", 
                    historyItem.MoneyWon[0].ToString("C", culture), 
                    historyItem.MoneyWon[1].ToString("C", culture), 
                    historyItem.MoneyWon[2].ToString("C", culture));
                if (historyItem.MoneyWon[0] > 0)
                {
                    wins++;
                }
                total++;
            }
            var ratio = total != 0 ? (wins * 100f / total) : 0f;

            _historyBox.Text = sb.ToString();
			//_historyBox.ScrollToBottom();
            _stat.Text = string.Format("Odehráno her:\n{0}\nZ toho výher:\n{1}\nPoměr: {2:N0}%\nPříště začíná:\n{3}", 
                total, wins, ratio, 
                (Game.MainScene.g != null && Game.MainScene.g.players != null)
                ? Game.MainScene.g.players[(Game.MainScene.CurrentStartingPlayerIndex + 1) % Mariasek.Engine.New.Game.NumPlayers].Name
                : string.Format("Hráč {0}", (Game.MainScene.CurrentStartingPlayerIndex + 1) % Mariasek.Engine.New.Game.NumPlayers + 1));

            var sum1 = Game.Money.Sum(i => i.MoneyWon[0] * _settings.BaseBet).ToString("C", culture);
            var sum2 = Game.Money.Sum(i => i.MoneyWon[1] * _settings.BaseBet).ToString("C", culture);
            var sum3 = Game.Money.Sum(i => i.MoneyWon[2] * _settings.BaseBet).ToString("C", culture);

            _footer.Text = string.Format("Součet:\t{0}\t{1}\t{2}", sum1, sum2, sum3);
        }

        private void ChartButtonClicked(object sender)
        {
            if (_historyChart.IsVisible)
            {
                _historyChart.MoveTo(_hiddenPosition, 5000)
                    .Invoke(() => { _historyChart.Hide(); });
            }
            else
            {
                _historyChart.Invoke(() => { _historyChart.Show(); })
                    .MoveTo(_origPosition, 5000);
            }
        }

        private void TableButtonClicked(object sender)
        {
            if (_historyBox.IsVisible)
            {
                _historyBox.MoveTo(_hiddenPosition, 5000)
                           .Invoke(() => { _historyBox.Hide(); });
            }
            else
            {
                _historyBox.Invoke(() => { _historyBox.Show(); })
                    .MoveTo(_origPosition, 5000);
            }
        }

        private void ResetHistoryClicked(object sender)
        {
            _useMockData = false;
            Game.Money.Clear();
            PopulateControls();
            Game.MainScene.SaveHistory();
        }

        private void MenuClicked(object sender)
        {
            Game.MenuScene.SetActive();
        }

        public void SettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            _settings = e.Settings;
        }
    }
}

