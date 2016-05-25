using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
//using Microsoft.Xna.Framework.GamerServices;
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
        private Button _resetHistoryButton;
        private Label _stat;
        private Label _header;
        private Label _footer;
        private TextBox _historyBox;
        private LineChart _historyChart;
        private bool _useMockData;// = true;
        private GameSettings _settings;

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

            _historyChart = new LineChart(this)
                {
                    Position = new Vector2(10,10),
                    Width = (int)Game.VirtualScreenWidth - 20,
                    Height = (int)Game.VirtualScreenHeight - 120
                };
            _menuButton = new Button(this)
                {
                    Position = new Vector2(10, 10),
                    Width = 200,
                    Height = 50,
                    Text = "Menu"
                };
            _menuButton.Click += MenuClicked;
            _resetHistoryButton = new Button(this)
                {
                    Position = new Vector2(10, (int)Game.VirtualScreenHeight - 60),
                    Width = 200,
                    Height = 50,
                    Text = "Smazat historii"
                };
            _resetHistoryButton.Click += ResetHistoryClicked;
            _stat = new Label(this)
            {
                    Position = new Vector2(10, 70),
                    Width = 200,
                    Height = (int)Game.VirtualScreenHeight - 140
            };
            _header = new Label(this)
                {
                    Position = new Vector2(220, 10),
                    Width = (int)Game.VirtualScreenWidth - 180,
                    Height = 50
                };
            _historyBox = new TextBox(this)
                {
                    Position = new Vector2(220, 60),
                    Width = (int)Game.VirtualScreenWidth - 230,
                    Height = (int)Game.VirtualScreenHeight - 120,
                    HorizontalAlign = HorizontalAlignment.Left,
                    VerticalAlign = VerticalAlignment.Top
                };
            _footer = new Label(this)
                {
                    Position = new Vector2(220, Game.VirtualScreenHeight - 60),
                    Width = (int)Game.VirtualScreenWidth - 230,
                    Height = 50
                };
            Background = Game.Content.Load<Texture2D>("wood2");
            BackgroundTint = Color.DimGray;

            Game.SettingsScene.SettingsChanged += SettingsChanged;

            PopulateControls();
        }

        public void PopulateControls()
        {            
            _header.Text = string.Format("Hráč:\t\t{0}\t\t{1}\t{2}", "Hráč 1", "Hráč 2 (AI)", "Hráč 3 (AI)");

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
                    if (maxWon < Game.Money[j].MoneyWon[i])
                    {
                        maxWon = Game.Money[j].MoneyWon[i];
                    }
                    if (maxLost > Game.Money[j].MoneyWon[i])
                    {
                        maxLost = Game.Money[j].MoneyWon[i];
                    }
                }
            }
            _historyChart.MaxValue = new Vector2(Game.Money.Count, maxWon);
            _historyChart.MinValue = new Vector2(0, maxLost);
            _historyChart.Series = series;

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

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}

