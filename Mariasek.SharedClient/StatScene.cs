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

using Mariasek.Engine.New;
using Mariasek.SharedClient.GameComponents;

namespace Mariasek.SharedClient
{
    public class MyGrouping<TKey, TValue> : List<TValue>, IGrouping<TKey, TValue>
    {
        public TKey Key { get; set; }
        public MyGrouping()
            : base()
        {
        }
        public MyGrouping(IEnumerable<TValue> collection)
            : base()
        {
            AddRange(collection);
        }
    }

    public class StatScene : Scene
    {
        private GameSettings _settings;
        private Button _backButton;
        private ToggleButton _effectivityButton;
        private ToggleButton _moneyButton;
        private RadarChart _chart;
        private Label[] _chartLabels;
        private TextBox _text;
        private string _effectivityText;
        private string _moneyText;

        public StatScene(MariasekMonoGame game)
            : base(game)
        {
        }

        public override void Initialize()
        {
            base.Initialize();

            _effectivityButton = new ToggleButton(this)
            {
                Position = new Vector2(10, (int)Game.VirtualScreenHeight - 180),
                Width = 200,
                Height = 50,
                Text = "Úspěšnost",
                IsSelected = true,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _effectivityButton.Click += EffectivityButtonClicked;
            _moneyButton = new ToggleButton(this)
            {
                Position = new Vector2(10, (int)Game.VirtualScreenHeight - 120),
                Width = 200,
                Height = 50,
                Text = "Výhra",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _moneyButton.Click += MoneyButtonClicked;
            _backButton = new Button(this)
            {
                Position = new Vector2(10, (int)Game.VirtualScreenHeight - 60),
                Width = 200,
                Height = 50,
                Text = "Zpět",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _backButton.Click += BackButtonClicked;
            _chart = new RadarChart(this)
            {
                Position = new Vector2(10, 70),
                Width = 200,
                Height = 200,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                MinValue = -0.1f,
                MaxValue = 1.1f
            };
            _text = new TextBox(this)
            {
                Position = new Vector2(210, 10),
                Width = (int)Game.VirtualScreenWidth - 210,
                Height = (int)Game.VirtualScreenHeight - 20,
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Top,
                TextRenderer = Game.FontRenderers["BMFont"]
            };
            Background = Game.Content.Load<Texture2D>("wood2");
            BackgroundTint = Color.DimGray;

            _chartLabels = new Label[6];
            _chartLabels[0] = new Label(this)
            {
                Position = _chart.Position + new Vector2(0, _chart.Height / 2),
                Width = 100,
                Height = 30,
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Top,
                Text = "Betl",
                TextRenderer = Game.FontRenderers["BMFont"],
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                ZIndex = 100
            };
            _chartLabels[1] = new Label(this)
            {
                Position = _chart.Position + new Vector2(0, -25),
                Width = 100,
                Height = 30,
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Top,
                Text = "Durch",
                TextRenderer = Game.FontRenderers["BMFont"],
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                ZIndex = 100
            };
            _chartLabels[2] = new Label(this)
            {
                Position = _chart.Position + new Vector2(_chart.Width - 100, -25),
                Width = 100,
                Height = 30,
                HorizontalAlign = HorizontalAlignment.Right,
                VerticalAlign = VerticalAlignment.Top,
                Text = "Hra",
                TextRenderer = Game.FontRenderers["BMFont"],
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                ZIndex = 100
            };
            _chartLabels[3] = new Label(this)
            {
                Position = _chart.Position + new Vector2(_chart.Width - 100, _chart.Height / 2),
                Width = 100,
                Height = 30,
                HorizontalAlign = HorizontalAlignment.Right,
                VerticalAlign = VerticalAlignment.Top,
                Text = "Kilo",
                TextRenderer = Game.FontRenderers["BMFont"],
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                ZIndex = 100
            };
            _chartLabels[4] = new Label(this)
            {
                Position =_chart.Position + new Vector2(_chart.Width - 100, _chart.Height - 5),
                Width = 100,
                Height = 30,
                HorizontalAlign = HorizontalAlignment.Right,
                VerticalAlign = VerticalAlignment.Top,
                Text = "Sedma",
                TextRenderer = Game.FontRenderers["BMFont"],
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                ZIndex = 100
            };
            _chartLabels[5] = new Label(this)
            {
                Position = _chart.Position + new Vector2(0, _chart.Height - 5),
                Width = 100,
                Height = 30,
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Top,
                Text = "Stosedm",
                TextRenderer = Game.FontRenderers["BMFont"],
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                ZIndex = 100
            };
            Game.SettingsScene.SettingsChanged += SettingsChanged;
            PopulateControls();
        }

        public void PopulateControls()
        {
            var stats = Game.Money.GroupBy(g => g.GameTypeString.TrimEnd());
            var sbGames = new StringBuilder();
            var sbMoney = new StringBuilder();
            var totalGroup = new MyGrouping<string, MoneyCalculatorBase>();

            totalGroup.Key = "Souhrn";
            totalGroup.AddRange(Game.Money);
            AppendStatsForGameType(totalGroup, sbGames, sbMoney);
            foreach (var stat in stats.OrderBy(g => g.Key))
            {
                AppendStatsForGameType(stat, sbGames, sbMoney);
            }

            PopulateRadarChart();
            _effectivityText = sbGames.ToString();
            _moneyText = sbMoney.ToString();
            EffectivityButtonClicked(this);
        }

        private void PopulateRadarChart()
        {
            var stats = Game.Money.GroupBy(g => g.GameTypeString.TrimEnd()).Where(i => !string.IsNullOrWhiteSpace(i.Key)).ToList();
            var maxValue = 0f;
            var points = new float[Mariasek.Engine.New.Game.NumPlayers][];
            var allStats = new[]
            {
                new MyGrouping<string, MoneyCalculatorBase>()
                {
                    Key = "Betl"
                },
                new MyGrouping<string, MoneyCalculatorBase>()
                {
                    Key = "Durch"
                },
                new MyGrouping<string, MoneyCalculatorBase>()
                {
                    Key = "Hra"
                },
                new MyGrouping<string, MoneyCalculatorBase>()
                {
                    Key = "Kilo"
                },
                new MyGrouping<string, MoneyCalculatorBase>()
                {
                    Key = "Sedma"
                },
                new MyGrouping<string, MoneyCalculatorBase>()
                {
                    Key = "Stosedm"
                }
            };
            stats.AddRange(allStats.Where(i => stats.All(j => i.Key != j.Key)));
            stats = stats.OrderBy(i => i.Key).ToList();
            for (var i = 0; i < Mariasek.Engine.New.Game.NumPlayers; i++)
            {
                points[i] = new float[stats.Count()];
            }

            for (var i = 0; i < stats.Count(); i++)
            {
                var stat = stats[i];
                var gameTypeString = stat.Key;
                var games1 = stat.Where(j => (j.MoneyWon[0] > 0 && j.MoneyWon[1] < 0 && j.MoneyWon[2] < 0) ||
                                             (j.MoneyWon[0] < 0 && j.MoneyWon[1] > 0 && j.MoneyWon[2] > 0));
                var games2 = stat.Where(j => (j.MoneyWon[0] < 0 && j.MoneyWon[1] > 0 && j.MoneyWon[2] < 0) ||
                                             (j.MoneyWon[0] > 0 && j.MoneyWon[1] < 0 && j.MoneyWon[2] > 0));
                var games3 = stat.Where(j => (j.MoneyWon[0] < 0 && j.MoneyWon[1] < 0 && j.MoneyWon[2] > 0) ||
                                             (j.MoneyWon[0] > 0 && j.MoneyWon[1] > 0 && j.MoneyWon[2] < 0));
                var gamesWon1 = games1.Count(j => j.MoneyWon[0] > 0);
                var gamesWon2 = games2.Count(j => j.MoneyWon[1] > 0);
                var gamesWon3 = games3.Count(j => j.MoneyWon[2] > 0);
                var gamesLost1 = games1.Count(j => j.MoneyWon[0] < 0);
                var gamesLost2 = games2.Count(j => j.MoneyWon[1] < 0);
                var gamesLost3 = games3.Count(j => j.MoneyWon[2] < 0);
                var gamesPlayed1 = gamesWon1 + gamesLost1;
                var gamesPlayed2 = gamesWon2 + gamesLost2;
                var gamesPlayed3 = gamesWon3 + gamesLost3;
                var gamesRatio1 = gamesPlayed1 > 0 ? gamesWon1 / (float)gamesPlayed1 : 0f;
                var gamesRatio2 = gamesPlayed2 > 0 ? gamesWon2 / (float)gamesPlayed2 : 0f;
                var gamesRatio3 = gamesPlayed3 > 0 ? gamesWon3 / (float)gamesPlayed3 : 0f;

                if (maxValue < gamesRatio1)
                {
                    maxValue = gamesRatio1;
                }
                if (maxValue < gamesRatio2)
                {
                    maxValue = gamesRatio2;
                }
                if (maxValue < gamesRatio3)
                {
                    maxValue = gamesRatio3;
                }
                points[0][i] = gamesRatio1;
                points[1][i] = gamesRatio2;
                points[2][i] = gamesRatio3;
            }
            _chart.Data = points;
        }

        private void AppendStatsForGameType(IGrouping<string, MoneyCalculatorBase> stat, StringBuilder sbGames, StringBuilder sbMoney)
        {
            var gameTypeString = stat.Key;
            var games1 = stat.Where(i => (i.MoneyWon[0] > 0 && i.MoneyWon[1] < 0 && i.MoneyWon[2] < 0) ||
                                         (i.MoneyWon[0] < 0 && i.MoneyWon[1] > 0 && i.MoneyWon[2] > 0));
            var games2 = stat.Where(i => (i.MoneyWon[0] < 0 && i.MoneyWon[1] > 0 && i.MoneyWon[2] < 0) ||
                                         (i.MoneyWon[0] > 0 && i.MoneyWon[1] < 0 && i.MoneyWon[2] > 0));
            var games3 = stat.Where(i => (i.MoneyWon[0] < 0 && i.MoneyWon[1] < 0 && i.MoneyWon[2] > 0) ||
                                         (i.MoneyWon[0] > 0 && i.MoneyWon[1] > 0 && i.MoneyWon[2] < 0));
            var gamesWon1 = games1.Count(i => i.MoneyWon[0] > 0);
            var gamesWon2 = games2.Count(i => i.MoneyWon[1] > 0);
            var gamesWon3 = games3.Count(i => i.MoneyWon[2] > 0);
            var gamesLost1 = games1.Count(i => i.MoneyWon[0] < 0);
            var gamesLost2 = games2.Count(i => i.MoneyWon[1] < 0);
            var gamesLost3 = games3.Count(i => i.MoneyWon[2] < 0);
            var gamesPlayed1 = gamesWon1 + gamesLost1;
            var gamesPlayed2 = gamesWon2 + gamesLost2;
            var gamesPlayed3 = gamesWon3 + gamesLost3;
            var gamesRatio1 = gamesPlayed1 > 0 ? gamesWon1 / (float)gamesPlayed1 : 0f;
            var gamesRatio2 = gamesPlayed2 > 0 ? gamesWon2 / (float)gamesPlayed2 : 0f;
            var gamesRatio3 = gamesPlayed3 > 0 ? gamesWon3 / (float)gamesPlayed3 : 0f;
            var moneyBalance1 = games1.Sum(i => i.MoneyWon[0] * _settings.BaseBet);
            var moneyBalance2 = games2.Sum(i => i.MoneyWon[1] * _settings.BaseBet);
            var moneyBalance3 = games3.Sum(i => i.MoneyWon[2] * _settings.BaseBet);
            var moneyMin1 = gamesPlayed1 > 0 ? games1.Min(i => i.MoneyWon[0] * _settings.BaseBet) : 0f;
            var moneyMin2 = gamesPlayed2 > 0 ? games2.Min(i => i.MoneyWon[1] * _settings.BaseBet) : 0f;
            var moneyMin3 = gamesPlayed3 > 0 ? games3.Min(i => i.MoneyWon[2] * _settings.BaseBet) : 0f;
            var moneyAvg1 = gamesPlayed1 > 0 ? games1.Average(i => i.MoneyWon[0] * _settings.BaseBet) : 0f;
            var moneyAvg2 = gamesPlayed2 > 0 ? games2.Average(i => i.MoneyWon[1] * _settings.BaseBet) : 0f;
            var moneyAvg3 = gamesPlayed3 > 0 ? games3.Average(i => i.MoneyWon[2] * _settings.BaseBet) : 0f;
            var moneyMax1 = gamesPlayed1 > 0 ? games1.Max(i => i.MoneyWon[0] * _settings.BaseBet) : 0f;
            var moneyMax2 = gamesPlayed2 > 0 ? games2.Max(i => i.MoneyWon[1] * _settings.BaseBet) : 0f;
            var moneyMax3 = gamesPlayed3 > 0 ? games3.Max(i => i.MoneyWon[2] * _settings.BaseBet) : 0f;
            var culture = CultureInfo.CreateSpecificCulture("cs-CZ");

            sbGames.AppendFormat("{0,-7}\tHer\tVýher\tProher\tPoměr\n", gameTypeString);
            sbGames.AppendFormat("{0,-7}\t{1}\t{2}\t{3}\t{4:F0}%\n", Game.MainScene.PlayerNames[0], gamesPlayed1, gamesWon1, gamesLost1, gamesRatio1 * 100);
            sbGames.AppendFormat("{0,-7}\t{1}\t{2}\t{3}\t{4:F0}%\n", Game.MainScene.PlayerNames[1], gamesPlayed2, gamesWon2, gamesLost2, gamesRatio2 * 100);
            sbGames.AppendFormat("{0,-7}\t{1}\t{2}\t{3}\t{4:F0}%\n", Game.MainScene.PlayerNames[2], gamesPlayed3, gamesWon3, gamesLost3, gamesRatio3 * 100);
            sbGames.Append("__________________________________________________________\n");

            sbMoney.AppendFormat("{0,-7}\tVýhra\tMin\tPrůměr\tMax\n", gameTypeString);
            sbMoney.AppendFormat("{0,-7}\t{1}\t{2}\t{3}\t{4}\n", Game.MainScene.PlayerNames[0], moneyBalance1.ToString("F2", culture), moneyMin1.ToString("F2", culture), moneyAvg1.ToString("F2", culture), moneyMax1.ToString("F2", culture));
            sbMoney.AppendFormat("{0,-7}\t{1}\t{2}\t{3}\t{4}\n", Game.MainScene.PlayerNames[1], moneyBalance2.ToString("F2", culture), moneyMin2.ToString("F2", culture), moneyAvg2.ToString("F2", culture), moneyMax2.ToString("F2", culture));
            sbMoney.AppendFormat("{0,-7}\t{1}\t{2}\t{3}\t{4}\n", Game.MainScene.PlayerNames[2], moneyBalance3.ToString("F2", culture), moneyMin3.ToString("F2", culture), moneyAvg3.ToString("F2", culture), moneyMax3.ToString("F2", culture));
            sbMoney.Append("__________________________________________________________\n");
            //sbMoney.Append("«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»\n");
        }

        public void SettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            _settings = e.Settings;
        }

        private void EffectivityButtonClicked(object sender)
        {
            _effectivityButton.IsSelected = true;
            _moneyButton.IsSelected = false;
            _text.Text = _effectivityText;
        }

        private void MoneyButtonClicked(object sender)
        {
            _effectivityButton.IsSelected = false;
            _moneyButton.IsSelected = true;
            _text.Text = _moneyText;
        }

        private void BackButtonClicked(object sender)
        {
            Game.HistoryScene.SetActive();
        }
    }
}
