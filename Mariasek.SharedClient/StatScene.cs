﻿using System;
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
    [Flags]
    public enum StatMode
    {
        Efectivity = 1,
        Gain = 2,
        Leader = 4,
        Defence = 8
    };

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
        private LeftRightSelector _effectivityMoneySelector;
        private LeftRightSelector _leaderDefenceSelector;
        private RadarChart _chart;
        private Label[] _chartLabels;
        private TextBox _summary;
        private TextBox _details;
        private string _effectivityText = string.Empty;
        private string _effectivitySummary = string.Empty;
        private string _defenceEffectivityText = string.Empty;
        private string _defenceEffectivitySummary = string.Empty;
        private string _moneyText = string.Empty;
        private string _moneySummary = string.Empty;
        private string _defenceMoneyText = string.Empty;
        private string _defenceMoneySummary = string.Empty;
        private float[][] _points = new float[Mariasek.Engine.New.Game.NumPlayers][];
        private float[][] _defencePoints = new float[Mariasek.Engine.New.Game.NumPlayers][];

        public StatScene(MariasekMonoGame game)
            : base(game)
        {
        }

        public override void Initialize()
        {
            base.Initialize();

            _effectivityMoneySelector = new LeftRightSelector(this)
            {
                Position = new Vector2(0, (int)Game.VirtualScreenHeight - 180),
                Width = 220,
                Items = new SelectorItems() { { "Efektivita", StatMode.Efectivity }, { "Výhra", StatMode.Gain } },
                SelectedIndex = 0,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _effectivityMoneySelector.SelectionChanged += StatModeSelectorClicked;
            _leaderDefenceSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(0, (int)Game.VirtualScreenHeight - 120),
                Width = 220,
                Items = new SelectorItems() { { "Při volbě", StatMode.Leader }, { "V obraně", StatMode.Defence } },
                SelectedIndex = 0,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _leaderDefenceSelector.SelectionChanged += StatModeSelectorClicked;
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
            _summary = new TextBox(this)
            {
                Position = new Vector2(210, 10),
                Width = (int)Game.VirtualScreenWidth - 210,
                Height = 200,
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Top,
                TextRenderer = Game.FontRenderers["BMFont"]
            };
            _details = new TextBox(this)
            {
                Position = new Vector2(210, 210),
                Width = (int)Game.VirtualScreenWidth - 210,
                Height = (int)Game.VirtualScreenHeight - 220,
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
            var sbGamesSummary = new StringBuilder();
            var sbMoneySummary = new StringBuilder();
            var sbDefenceGames = new StringBuilder();
            var sbDefenceMoney = new StringBuilder();
            var sbDefenceGamesSummary = new StringBuilder();
            var sbDefenceMoneySummary = new StringBuilder();
            var totalGroup = new MyGrouping<string, MoneyCalculatorBase>();

            totalGroup.Key = "Souhrn";
            totalGroup.AddRange(Game.Money);
            AppendStatsForGameType(totalGroup, sbGamesSummary, sbMoneySummary, sbDefenceGamesSummary, sbDefenceMoneySummary);
            foreach (var stat in stats.OrderBy(g => g.Key))
            {
                AppendStatsForGameType(stat, sbGames, sbMoney, sbDefenceGames, sbDefenceMoney);
            }

            PopulateRadarChart();
            _effectivityText = sbGames.ToString();
            _effectivitySummary = sbGamesSummary.ToString();
            _defenceEffectivityText = sbDefenceGames.ToString();
            _defenceEffectivitySummary = sbDefenceGamesSummary.ToString();
            _moneyText = sbMoney.ToString();
            _moneySummary = sbMoneySummary.ToString();
            _defenceMoneyText = sbDefenceMoney.ToString();
            _defenceMoneySummary = sbDefenceMoneySummary.ToString();
            StatModeSelectorClicked(this);
        }

        private void PopulateRadarChart()
        {
            var stats = Game.Money.GroupBy(g => g.GameTypeString.TrimEnd()).Where(i => !string.IsNullOrWhiteSpace(i.Key)).ToList();
            var maxValue = 0f;
            var maxDefenceValue = 0f;
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
                _points[i] = new float[stats.Count()];
                _defencePoints[i] = new float[stats.Count()];
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
                var defencesPlayed1 = gamesPlayed2 + gamesPlayed3;
                var defencesPlayed2 = gamesPlayed1 + gamesPlayed3;
                var defencesPlayed3 = gamesPlayed1 + gamesPlayed2;
                var defencesWon1 = gamesLost2 + gamesLost3;
                var defencesWon2 = gamesLost1 + gamesLost3;
                var defencesWon3 = gamesLost1 + gamesLost2;
                var defencesLost1 = gamesWon2 + gamesWon3;
                var defencesLost2 = gamesWon1 + gamesWon3;
                var defencesLost3 = gamesWon1 + gamesWon2;
                var defencesRatio1 = defencesPlayed1 > 0 ? defencesWon1 / (float)defencesPlayed1 : 0f;
                var defencesRatio2 = defencesPlayed2 > 0 ? defencesWon2 / (float)defencesPlayed2 : 0f;
                var defencesRatio3 = defencesPlayed3 > 0 ? defencesWon3 / (float)defencesPlayed3 : 0f;


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
                if (maxDefenceValue < defencesRatio1)
                {
                    maxDefenceValue = defencesRatio1;
                }
                if (maxDefenceValue < defencesRatio2)
                {
                    maxDefenceValue = defencesRatio2;
                }
                if (maxDefenceValue < defencesRatio3)
                {
                    maxDefenceValue = defencesRatio3;
                }
                _points[0][i] = gamesRatio1;
                _points[1][i] = gamesRatio2;
                _points[2][i] = gamesRatio3;
                _defencePoints[0][i] = defencesRatio1;
                _defencePoints[1][i] = defencesRatio2;
                _defencePoints[2][i] = defencesRatio3;
            }
            if (((StatMode)_leaderDefenceSelector.SelectedValue & StatMode.Leader) != 0)
            {
                _chart.Data = _points;
            }
            else
            {
                _chart.Data = _defencePoints;
            }
        }

        private void AppendStatsForGameType(IGrouping<string, MoneyCalculatorBase> stat, StringBuilder sbGames, StringBuilder sbMoney, StringBuilder sbDefenceGames, StringBuilder sbDefenceMoney)
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
            var defencesPlayed1 = gamesPlayed2 + gamesPlayed3;
            var defencesPlayed2 = gamesPlayed1 + gamesPlayed3;
            var defencesPlayed3 = gamesPlayed1 + gamesPlayed2;
            var defencesWon1 = gamesLost2 + gamesLost3;
            var defencesWon2 = gamesLost1 + gamesLost3;
            var defencesWon3 = gamesLost1 + gamesLost2;
            var defencesLost1 = gamesWon2 + gamesWon3;
            var defencesLost2 = gamesWon1 + gamesWon3;
            var defencesLost3 = gamesWon1 + gamesWon2;
            var gamesRatio1 = gamesPlayed1 > 0 ? gamesWon1 / (float)gamesPlayed1 : 0f;
            var gamesRatio2 = gamesPlayed2 > 0 ? gamesWon2 / (float)gamesPlayed2 : 0f;
            var gamesRatio3 = gamesPlayed3 > 0 ? gamesWon3 / (float)gamesPlayed3 : 0f;
            var defencesRatio1 = defencesPlayed1 > 0 ? defencesWon1 / (float)defencesPlayed1 : 0f;
            var defencesRatio2 = defencesPlayed2 > 0 ? defencesWon2 / (float)defencesPlayed2 : 0f;
            var defencesRatio3 = defencesPlayed3 > 0 ? defencesWon3 / (float)defencesPlayed3 : 0f;
            var moneyBalance1 = games1.Sum(i => i.MoneyWon[0] * _settings.BaseBet);
            var moneyBalance2 = games2.Sum(i => i.MoneyWon[1] * _settings.BaseBet);
            var moneyBalance3 = games3.Sum(i => i.MoneyWon[2] * _settings.BaseBet);
            var defenceMoneyBalance1 = (games2.Sum(i => -i.MoneyWon[1] * _settings.BaseBet) + games3.Sum(i => -i.MoneyWon[2] * _settings.BaseBet))/2;
            var defenceMoneyBalance2 = (games1.Sum(i => -i.MoneyWon[0] * _settings.BaseBet) + games3.Sum(i => -i.MoneyWon[2] * _settings.BaseBet))/2;
            var defenceMoneyBalance3 = (games1.Sum(i => -i.MoneyWon[0] * _settings.BaseBet) + games2.Sum(i => -i.MoneyWon[1] * _settings.BaseBet))/2;
            var moneyMin1 = gamesPlayed1 > 0 ? games1.Min(i => i.MoneyWon[0] * _settings.BaseBet) : 0f;
            var moneyMin2 = gamesPlayed2 > 0 ? games2.Min(i => i.MoneyWon[1] * _settings.BaseBet) : 0f;
            var moneyMin3 = gamesPlayed3 > 0 ? games3.Min(i => i.MoneyWon[2] * _settings.BaseBet) : 0f;
            var defenceMoneyMin1 = gamesPlayed2 > 0 ? games2.Min(i => -i.MoneyWon[1] * _settings.BaseBet) : 0f + 
                                   gamesPlayed3 > 0 ? games3.Min(i => -i.MoneyWon[2] * _settings.BaseBet) : 0f;
            var defenceMoneyMin2 = gamesPlayed1 > 0 ? games1.Min(i => -i.MoneyWon[0] * _settings.BaseBet) : 0f +
                                   gamesPlayed3 > 0 ? games3.Min(i => -i.MoneyWon[2] * _settings.BaseBet) : 0f;
            var defenceMoneyMin3 = gamesPlayed1 > 0 ? games1.Min(i => -i.MoneyWon[0] * _settings.BaseBet) : 0f +
                                   gamesPlayed2 > 0 ? games2.Min(i => -i.MoneyWon[1] * _settings.BaseBet) : 0f;
            var moneyAvg1 = gamesPlayed1 > 0 ? games1.Average(i => i.MoneyWon[0] * _settings.BaseBet) : 0f;
            var moneyAvg2 = gamesPlayed2 > 0 ? games2.Average(i => i.MoneyWon[1] * _settings.BaseBet) : 0f;
            var moneyAvg3 = gamesPlayed3 > 0 ? games3.Average(i => i.MoneyWon[2] * _settings.BaseBet) : 0f;
            var defenceMoneyAvg1 = gamesPlayed2 > 0 ? games2.Average(i => -i.MoneyWon[1] * _settings.BaseBet) : 0f + 
                                   gamesPlayed3 > 0 ? games3.Average(i => -i.MoneyWon[2] * _settings.BaseBet) : 0f;
            var defenceMoneyAvg2 = gamesPlayed1 > 0 ? games1.Average(i => -i.MoneyWon[0] * _settings.BaseBet) : 0f + 
                                   gamesPlayed3 > 0 ? games3.Average(i => -i.MoneyWon[2] * _settings.BaseBet) : 0f;
            var defenceMoneyAvg3 = gamesPlayed1 > 0 ? games1.Average(i => -i.MoneyWon[0] * _settings.BaseBet) : 0f +
                                   gamesPlayed2 > 0 ? games2.Average(i => -i.MoneyWon[1] * _settings.BaseBet) : 0f;
            var moneyMax1 = gamesPlayed1 > 0 ? games1.Max(i => i.MoneyWon[0] * _settings.BaseBet) : 0f;
            var moneyMax2 = gamesPlayed2 > 0 ? games2.Max(i => i.MoneyWon[1] * _settings.BaseBet) : 0f;
            var moneyMax3 = gamesPlayed3 > 0 ? games3.Max(i => i.MoneyWon[2] * _settings.BaseBet) : 0f;
            var defenceMoneyMax1 = gamesPlayed2 > 0 ? games2.Max(i => -i.MoneyWon[1] * _settings.BaseBet) : 0f + 
                                   gamesPlayed3 > 0 ? games3.Max(i => -i.MoneyWon[2] * _settings.BaseBet) : 0f;
            var defenceMoneyMax2 = gamesPlayed1 > 0 ? games1.Max(i => -i.MoneyWon[0] * _settings.BaseBet) : 0f +
                                   gamesPlayed3 > 0 ? games3.Max(i => -i.MoneyWon[2] * _settings.BaseBet) : 0f;
            var defenceMoneyMax3 = gamesPlayed1 > 0 ? games1.Max(i => -i.MoneyWon[0] * _settings.BaseBet) : 0f +
                                   gamesPlayed2 > 0 ? games2.Max(i => -i.MoneyWon[1] * _settings.BaseBet) : 0f;
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

            sbDefenceGames.AppendFormat("{0,-7}\tHer\tVýher\tProher\tPoměr\n", gameTypeString);
            sbDefenceGames.AppendFormat("{0,-7}\t{1}\t{2}\t{3}\t{4:F0}%\n", Game.MainScene.PlayerNames[0], defencesPlayed1, defencesWon1, defencesLost1, defencesRatio1 * 100);
            sbDefenceGames.AppendFormat("{0,-7}\t{1}\t{2}\t{3}\t{4:F0}%\n", Game.MainScene.PlayerNames[1], defencesPlayed2, defencesWon2, defencesLost2, defencesRatio2 * 100);
            sbDefenceGames.AppendFormat("{0,-7}\t{1}\t{2}\t{3}\t{4:F0}%\n", Game.MainScene.PlayerNames[2], defencesPlayed3, defencesWon3, defencesLost3, defencesRatio3 * 100);
            sbDefenceGames.Append("__________________________________________________________\n");

            sbDefenceMoney.AppendFormat("{0,-7}\tVýhra\tMin\tPrůměr\tMax\n", gameTypeString);
            sbDefenceMoney.AppendFormat("{0,-7}\t{1}\t{2}\t{3}\t{4}\n", Game.MainScene.PlayerNames[0], defenceMoneyBalance1.ToString("F2", culture), defenceMoneyMin1.ToString("F2", culture), defenceMoneyAvg1.ToString("F2", culture), defenceMoneyMax1.ToString("F2", culture));
            sbDefenceMoney.AppendFormat("{0,-7}\t{1}\t{2}\t{3}\t{4}\n", Game.MainScene.PlayerNames[1], defenceMoneyBalance2.ToString("F2", culture), defenceMoneyMin2.ToString("F2", culture), defenceMoneyAvg2.ToString("F2", culture), defenceMoneyMax2.ToString("F2", culture));
            sbDefenceMoney.AppendFormat("{0,-7}\t{1}\t{2}\t{3}\t{4}\n", Game.MainScene.PlayerNames[2], defenceMoneyBalance3.ToString("F2", culture), defenceMoneyMin3.ToString("F2", culture), defenceMoneyAvg3.ToString("F2", culture), defenceMoneyMax3.ToString("F2", culture));
            sbDefenceMoney.Append("__________________________________________________________\n");
            //sbMoney.Append("«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»«»\n");
        }

        public void SettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            _settings = e.Settings;
        }

        private void StatModeSelectorClicked(object sender)
        {
            if (((StatMode)_effectivityMoneySelector.SelectedValue & StatMode.Efectivity) != 0)
            {
                if (((StatMode)_leaderDefenceSelector.SelectedValue & StatMode.Leader) != 0)
                {
                    _summary.Text = _effectivitySummary;
                    _details.Text = _effectivityText;
                    _chart.Data = _points;
                }
                else
                {
                    _summary.Text = _defenceEffectivitySummary;
                    _details.Text = _defenceEffectivityText;
                    _chart.Data = _defencePoints;
                }
            }
            else
            {
                if (((StatMode)_leaderDefenceSelector.SelectedValue & StatMode.Leader) != 0)
                {
                    _summary.Text = _moneySummary;
                    _details.Text = _moneyText;
                    _chart.Data = _points;
                }
                else
                {
                    _summary.Text = _defenceMoneySummary;
                    _details.Text = _defenceMoneyText;
                    _chart.Data = _defencePoints;
                }
            }
        }

        private void BackButtonClicked(object sender)
        {
            Game.HistoryScene.SetActive();
        }
    }
}