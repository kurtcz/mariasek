﻿using System;
using System.Collections.Generic;
using System.Linq;
using Mariasek.Engine.New;
using Mariasek.SharedClient.GameComponents;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Mariasek.SharedClient
{
    public class AiSettingsScene : Scene
    {
#region Child Components
        private Button _backButton;
        private Button _resetButton;
        private Label _play;
        private Label _maxBidCount;
        private Label _threshold0;
        private Label _threshold1;
        private Label _threshold2;
        private Label _threshold3;
        private Label _riskFactor;
        private Label _solitaryXThreshold;
        private Label _solitaryXThresholdDefense;
        private Label _safetyBetlThreshold;
        private RectangleShape _hline;
        //private RectangleShape _shadow;
        private LeftRightSelector _gameTypeSelector;
        private LeftRightSelector _playSelector;
        private LeftRightSelector _maxBidCountSelector;
        private LeftRightSelector _threshold0Selector;
        private LeftRightSelector _threshold1Selector;
        private LeftRightSelector _threshold2Selector;
        private LeftRightSelector _threshold3Selector;
        private LeftRightSelector _riskFactorSelector;
        private LeftRightSelector _solitaryXSelector;
        private LeftRightSelector _solitaryXDefenseSelector;
        private LeftRightSelector _safetyBetlSelector;
        private LineChart _chart;
        private Label _0Percent;
        private Label _50Percent;
        private Label _100Percent;
        private Label _note;
        private Label _note2;
        private LeftRightSelector _pageSelector;
        private const string _defaultNote = "Prahy udávají jistotu, kterou AI potřebuje aby si dal flek.";
        private const string _defaultNote2 = "Risk faktor udává ochotu AI mazat a hrát ostrou kartu.";
        private static readonly Dictionary<Hra, string> _notes = new Dictionary<Hra, string>
        {
            { Hra.Betl, "AI používá práh pro Flek když nevolil a hlásí špatnou\nbarvu. AI hraje utíkáčka pokud hrozí zobrazená prohra." },
            { Hra.Durch, "AI používá práh pro Flek když nevolil a hlásí\nšpatnou barvu. AI durch flekuje jen když nejde uhrát." },
        };
#endregion

        private bool _settingsChanged;
        private int  _recursionLevel;
        private bool _updating;

        public AiSettingsScene(MariasekMonoGame game)
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
            base.Initialize();

            //PopulateAiConfig();

            ///// BIDDING CONTROLS /////
            _resetButton = new Button(this)
            {
                Text = "Výchozí",
                Position = new Vector2(10, 10),
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                Width = 200
            };
            _resetButton.Click += ResetButtonClicked;

            _backButton = new Button(this)
            {
                Text = "Zpět",
                Position = new Vector2(10, Game.VirtualScreenHeight - 60),
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                Width = 200
            };
            _backButton.Click += BackButtonClicked;

            _pageSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(210, 10),
                Items = new SelectorItems() { { "Nastavení pro volbu a fleky", 0 }, { "Nastavení pro hru", 1 } },
                Width = (int)Game.VirtualScreenWidth - 210,
                TextRenderer = Game.FontRenderers["SegoeUI40Outl"]
            };
            _pageSelector.SelectedIndex = 0;
            _pageSelector.SelectionChanged += PageChanged;
            if (_pageSelector.SelectedIndex < 0)
            {
                _pageSelector.SelectedIndex = 0;
            }

            _chart = new LineChart(this)
            {
                Position = new Vector2(60, Game.VirtualScreenHeight / 2 - 125),
                Width = (int)300,
                Height = (int)300,
                LineThickness = 3f,
                DataMarkerSize = 20f,
                DataMarkerShape = DataMarkerShape.Square,
                Colors = new[] { new Color(0x40, 0x40, 0x40, 0x80), Color.Red },
                TickMarkLength = 5f,
                GridInterval = new Vector2(1f, 10f),
                ShowVerticalGridLines = false
            };

            _100Percent = new Label(this)
            {
                Position = new Vector2(0, Game.VirtualScreenHeight / 2 - 125),
                Width = 60,
                Height = 50,
                Text = "100%",
                HorizontalAlign = HorizontalAlignment.Right,
                VerticalAlign = VerticalAlignment.Top,
            };
            _50Percent = new Label(this)
            {
                Position = new Vector2(0, Game.VirtualScreenHeight / 2),
                Width = 60,
                Height = 50,
                Text = "50%",
                HorizontalAlign = HorizontalAlignment.Right,
                VerticalAlign = VerticalAlignment.Middle
            };
            _0Percent = new Label(this)
            {
                Position = new Vector2(0, Game.VirtualScreenHeight / 2 + 125),
                Width = 60,
                Height = 50,
                Text = "0%",
                HorizontalAlign = HorizontalAlignment.Right,
                VerticalAlign = VerticalAlignment.Bottom
            };

            _hline = new RectangleShape(this)
            {
                Position = new Vector2(0, Game.VirtualScreenHeight / 2 - 180),
                Width = (int)Game.VirtualScreenWidth,
                Height = 3,
                BackgroundColors = { Color.White },
                BorderColors = { Color.Transparent },
                BorderRadius = 0,
                BorderThickness = 1,
                Opacity = 0.7f
            };
            //_shadow = new RectangleShape(this)
            //{
            //    Position = new Vector2(Game.VirtualScreenWidth / 2, Game.VirtualScreenHeight / 2 - 175),
            //    Width = (int)Game.VirtualScreenWidth / 2,
            //    Height = 50,
            //    BackgroundColors = { Color.Black },
            //    BorderColors = { Color.Transparent },
            //    BorderRadius = 0,
            //    BorderThickness = 1,
            //    Opacity = 0.7f,
            //    ZIndex = 1
            //};
            _gameTypeSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(60, Game.VirtualScreenHeight / 2 - 175),
                //Position = new Vector2(Game.VirtualScreenWidth / 2 - 150, Game.VirtualScreenHeight / 2 - 175),
                Width = 300,
                //Position = new Vector2(Game.VirtualScreenWidth / 2, Game.VirtualScreenHeight / 2 - 175),
                //Width = (int)Game.VirtualScreenWidth / 2,
                Items = new SelectorItems() { { "Hra", Hra.Hra }, { "Sedma", Hra.Sedma }, { "Kilo", Hra.Kilo },
                                              { "Sedma proti", Hra.SedmaProti }, { "Kilo proti", Hra.KiloProti },
                                              { "Betl", Hra.Betl }, { "Durch", Hra.Durch } },
                TextRenderer = Game.FontRenderers["SegoeUI40Outl"],
                //ZIndex = 3
            };
            _gameTypeSelector.SelectedIndex = 0;
            _gameTypeSelector.SelectionChanged += GameTypeChanged;

            _play = new Label(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 80, Game.VirtualScreenHeight / 2 - 175),
                Width = 300,
                Height = 50,
                Text = "Hrát",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _playSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 + 190, Game.VirtualScreenHeight / 2 - 175),
                Width = (int)Game.VirtualScreenWidth / 2 - 190,
                Items = new SelectorItems() { { "Ano", true }, { "Ne", false } }
            };
            _playSelector.SelectionChanged += MaxBidCountChanged;
            _play.Hide();
            _playSelector.Hide();

            _maxBidCount = new Label(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 80, Game.VirtualScreenHeight / 2 - 115),
                Width = 300,
                Height = 50,
                Text = "Flekovat max.",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _maxBidCountSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 + 190, Game.VirtualScreenHeight / 2 - 115),
                Width = (int)Game.VirtualScreenWidth / 2 - 190,
                IsCyclicSelector = false,
                Items = new SelectorItems() { { "0x", 0 }, { "1x", 1 }, { "2x", 2 }, { "3x", 3 } }
            };
            _maxBidCountSelector.SelectionChanged += MaxBidCountChanged;

            _threshold0 = new Label(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 80, Game.VirtualScreenHeight / 2 - 55),
                Width = 300,
                Height = 50,
                Text = "Volba",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _threshold0Selector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 + 190, Game.VirtualScreenHeight / 2 - 55),
                Width = (int)Game.VirtualScreenWidth / 2 - 190,
                IsCyclicSelector = false,
                Items = new SelectorItems() { { "0%", 0 }, { "5%", 5 }, { "10%", 10 }, { "15%", 15 },
                                              { "20%", 20 }, { "25%", 25 }, { "30%", 30 }, { "35%", 35 },
                                              { "40%", 40 }, { "45%", 45 }, { "50%", 50 }, { "55%", 55 },
                                              { "60%", 60 }, { "65%", 65 }, { "70%", 70 }, { "75%", 75 },
                                              { "80%", 80 }, { "85%", 85 }, { "90%", 90 }, { "95%", 95 },
                                              { "100%", 100 } }
            };
            _threshold0Selector.SelectionChanged += ThresholdChanged;

            _threshold1 = new Label(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 80, Game.VirtualScreenHeight / 2 + 5),
                Width = 300,
                Height = 50,
                Text = "Flek",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _threshold1Selector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 + 190, Game.VirtualScreenHeight / 2 + 5),
                Width = (int)Game.VirtualScreenWidth / 2 - 190,
                IsCyclicSelector = false,
                Items = new SelectorItems() { { "0%", 0 }, { "5%", 5 }, { "10%", 10 }, { "15%", 15 },
                                              { "20%", 20 }, { "25%", 25 }, { "30%", 30 }, { "35%", 35 },
                                              { "40%", 40 }, { "45%", 45 }, { "50%", 50 }, { "55%", 55 },
                                              { "60%", 60 }, { "65%", 65 }, { "70%", 70 }, { "75%", 75 },
                                              { "80%", 80 }, { "85%", 85 }, { "90%", 90 }, { "95%", 95 },
                                              { "100%", 100 } }
            };
            _threshold1Selector.SelectionChanged += ThresholdChanged;

            _threshold2 = new Label(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 80, Game.VirtualScreenHeight / 2 + 65),
                Width = 300,
                Height = 50,
                Text = "Re",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _threshold2Selector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 + 190, Game.VirtualScreenHeight / 2 + 65),
                Width = (int)Game.VirtualScreenWidth / 2 - 190,
                IsCyclicSelector = false,
                Items = new SelectorItems() { { "0%", 0 }, { "5%", 5 }, { "10%", 10 }, { "15%", 15 },
                                              { "20%", 20 }, { "25%", 25 }, { "30%", 30 }, { "35%", 35 },
                                              { "40%", 40 }, { "45%", 45 }, { "50%", 50 }, { "55%", 55 },
                                              { "60%", 60 }, { "65%", 65 }, { "70%", 70 }, { "75%", 75 },
                                              { "80%", 80 }, { "85%", 85 }, { "90%", 90 }, { "95%", 95 },
                                              { "100%", 100 } }
            };
            _threshold2Selector.SelectionChanged += ThresholdChanged;

            _threshold3 = new Label(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 80, Game.VirtualScreenHeight / 2 + 125),
                Width = 300,
                Height = 50,
                Text = "Tutti",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _threshold3Selector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 + 190, Game.VirtualScreenHeight / 2 + 125),
                Width = (int)Game.VirtualScreenWidth / 2 - 190,
                IsCyclicSelector = false,
                Items = new SelectorItems() { { "0%", 0 }, { "5%", 5 }, { "10%", 10 }, { "15%", 15 },
                                              { "20%", 20 }, { "25%", 25 }, { "30%", 30 }, { "35%", 35 },
                                              { "40%", 40 }, { "45%", 45 }, { "50%", 50 }, { "55%", 55 },
                                              { "60%", 60 }, { "65%", 65 }, { "70%", 70 }, { "75%", 75 },
                                              { "80%", 80 }, { "85%", 85 }, { "90%", 90 }, { "95%", 95 },
                                              { "100%", 100 } }
            };
            _threshold3Selector.SelectionChanged += ThresholdChanged;

			_note = new Label(this)
			{
				Position = new Vector2(200, Game.VirtualScreenHeight - 70),
                Width = (int)Game.VirtualScreenWidth - 200,
				Height = 60,
				//Text = "",
                Text = _defaultNote,
				HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Top,
                FontScaleFactor = 0.9f
			};

            _note2 = new Label(this)
            {
                Position = new Vector2(200, Game.VirtualScreenHeight - 70),
                Width = (int)Game.VirtualScreenWidth - 200,
                Height = 60,
                //Text = "",
                Text = _defaultNote2,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Top,
                FontScaleFactor = 0.9f
            };

            _safetyBetlThreshold = new Label(this)
			{
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 80, Game.VirtualScreenHeight / 2 - 175),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
				Text = "Práh pro utíkáček",
				HorizontalAlign = HorizontalAlignment.Center,
				VerticalAlign = VerticalAlignment.Middle
			};
			_safetyBetlSelector = new LeftRightSelector(this)
			{
                Position = new Vector2(Game.VirtualScreenWidth / 2 + 190, Game.VirtualScreenHeight / 2 - 175),
                Width = (int)Game.VirtualScreenWidth / 2 - 190,
                Items = new SelectorItems() { { "Žádný", 0 }, { "16x základ", 16 }, { "20x základ", 20 }, 
                                              { "24x základ", 24 }, { "28x základ", 28 }, { "32x základ", 32 } }
			};
            _safetyBetlSelector.SelectedIndex = 0;
            _safetyBetlSelector.SelectionChanged += SafetyBetlChanged;
            if (_safetyBetlSelector.SelectedIndex < 0)
            {
                _safetyBetlSelector.SelectedIndex = 0;
            }

            ///// Game Play Controls /////
            _riskFactor = new Label(this)
            {
                Position = new Vector2(200, Game.VirtualScreenHeight / 2 - 115),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Risk faktor",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _riskFactorSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, Game.VirtualScreenHeight / 2 - 115),
                Width = 270,
                Items = new SelectorItems(
                    Enumerable.Range(0, 51)
                              .Select(i => new KeyValuePair<string, object>(string.Format("{0}%", i), i / 100f))
                              .ToList()
                )
            };
            _riskFactorSelector.SelectedIndex = 0;
            _riskFactorSelector.SelectionChanged += RiskFactorChanged;
            if (_riskFactorSelector.SelectedIndex < 0)
            {
                _riskFactorSelector.SelectedIndex = 0;
            }

            _solitaryXThreshold = new Label(this)
            {
                Position = new Vector2(200, Game.VirtualScreenHeight / 2 - 30),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 60,
                Text = "Risk při chytání\nplonkové X pro aktéra",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _solitaryXSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, Game.VirtualScreenHeight / 2 - 25),
                Width = 270,
                Items = new SelectorItems(
                    Enumerable.Range(0, 51)
                              .Select(i => new KeyValuePair<string, object>(string.Format("{0}%", i), i / 100f))
                              .ToList()
                )
            };
            _solitaryXSelector.SelectedIndex = 0;
            _solitaryXSelector.SelectionChanged += SolitaryXChanged;
            if (_solitaryXSelector.SelectedIndex < 0)
            {
                _solitaryXSelector.SelectedIndex = 0;
            }

            _solitaryXThresholdDefense = new Label(this)
            {
                Position = new Vector2(200, Game.VirtualScreenHeight / 2 + 65),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 60,
                Text = "Risk při chytání\nplonkové X pro obranu",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _solitaryXDefenseSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, Game.VirtualScreenHeight / 2 + 65),
                Width = 270,
                Items = new SelectorItems(
                    Enumerable.Range(0, 51)
                              .Select(i => new KeyValuePair<string, object>(string.Format("{0}%", i), i / 100f))
                              .ToList()
                )
            };
            _solitaryXDefenseSelector.SelectedIndex = 0;
            _solitaryXDefenseSelector.SelectionChanged += SolitaryXDefenseChanged;
            if (_solitaryXDefenseSelector.SelectedIndex < 0)
            {
                _solitaryXDefenseSelector.SelectedIndex = 0;
            }

            Background = Game.Assets.GetTexture("wood2");
            BackgroundTint = Color.DimGray;

            ShowPage(0);

            _settingsChanged = false;
			GameTypeChanged(this);
		}

        private void PageChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            ShowPage((int)selector.SelectedValue);
        }

        private void ShowPage(int page)
        {
            switch(page)
            {
                case 1:
                    HideBiddingControls();
                    ShowGamePlayControls();
                    break;
                case 0:
                default:
                    HideGamePlayControls();
                    ShowBiddingControls();
                    if ((Hra)_gameTypeSelector.SelectedValue == Hra.Betl)
                    {
                        _safetyBetlSelector.Show();
                        _safetyBetlThreshold.Show();
                    }
                    else
                    {
                        _safetyBetlSelector.Hide();
                        _safetyBetlThreshold.Hide();
                    }
                    break;
            }
        }

        private void ShowGamePlayControls()
        {
            _solitaryXThreshold.Show();
            _solitaryXSelector.Show();
            _solitaryXThresholdDefense.Show();
            _solitaryXDefenseSelector.Show();
            _riskFactor.Show();
            _riskFactorSelector.Show();
            _note2.Show();
        }

        private void HideGamePlayControls()
        {
            _solitaryXThreshold.Hide();
            _solitaryXSelector.Hide();
            _solitaryXThresholdDefense.Hide();
            _solitaryXDefenseSelector.Hide();
            _riskFactor.Hide();
            _riskFactorSelector.Hide();
            _note2.Hide();
        }

        private void ShowBiddingControls()
        {
            //_play.Show();
            _maxBidCount.Show();
            _threshold0.Show();
            _threshold1.Show();
            _threshold2.Show();
            _threshold3.Show();
            _safetyBetlThreshold.Show();
            _safetyBetlSelector.Show();
            _gameTypeSelector.Show();
            //_playSelector.Show();
            _maxBidCountSelector.Show();
            _threshold0Selector.Show();
            _threshold1Selector.Show();
            _threshold2Selector.Show();
            _threshold3Selector.Show();
            _safetyBetlSelector.Show();
            _chart.Show();
            _0Percent.Show();
            _50Percent.Show();
            _100Percent.Show();
            _note.Show();
        }

        private void HideBiddingControls()
        {
            //_play.Hide();
            _maxBidCount.Hide();
            _threshold0.Hide();
            _threshold1.Hide();
            _threshold2.Hide();
            _threshold3.Hide();
            _safetyBetlThreshold.Hide();
            _safetyBetlSelector.Hide();
            _gameTypeSelector.Hide();
            //_playSelector.Hide();
            _maxBidCountSelector.Hide();
            _threshold0Selector.Hide();
            _threshold1Selector.Hide();
            _threshold2Selector.Hide();
            _threshold3Selector.Hide();
            _safetyBetlSelector.Hide();
            _chart.Hide();
            _0Percent.Hide();
            _50Percent.Hide();
            _100Percent.Hide();
            _note.Hide();
        }

        private void SettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            if (_gameTypeSelector != null)
            {
                UpdateControls();
            }
            _settingsChanged = false;
        }

        public void UpdateControls(bool chartOnly = false)
        {
            var thresholdsDictionary = Game.Settings.Thresholds.ToDictionary(k => k.GameType, v => v);
            var thresholds = thresholdsDictionary[(Hra)_gameTypeSelector.SelectedValue].Thresholds.Split('|')
                                                                                                  .Select(i => int.Parse(i))
                                                                                                  .ToArray();

            if (!chartOnly && !_updating)
            {
                _updating = true;
                _playSelector.SelectedIndex = _playSelector.Items.FindIndex(thresholdsDictionary[(Hra)_gameTypeSelector.SelectedValue].Use);
                _maxBidCountSelector.SelectedIndex = _maxBidCountSelector.Items.FindIndex(thresholdsDictionary[(Hra)_gameTypeSelector.SelectedValue].MaxBidCount);
                _threshold0Selector.SelectedIndex = _threshold0Selector.Items.FindIndex(thresholds[0]);
                _threshold1Selector.SelectedIndex = _threshold1Selector.Items.FindIndex(thresholds[1]);
                _threshold2Selector.SelectedIndex = _threshold2Selector.Items.FindIndex(thresholds[2]);
                _threshold3Selector.SelectedIndex = _threshold3Selector.Items.FindIndex(thresholds[3]);
                _safetyBetlSelector.SelectedIndex = _safetyBetlSelector.Items.FindIndex(Game.Settings.SafetyBetlThreshold);
                _riskFactorSelector.SelectedIndex = _riskFactorSelector.Items.FindIndex(Game.Settings.RiskFactor);
                _solitaryXSelector.SelectedIndex = _solitaryXSelector.Items.FindIndex(Game.Settings.SolitaryXThreshold);
                _solitaryXDefenseSelector.SelectedIndex = _solitaryXDefenseSelector.Items.FindIndex(Game.Settings.SolitaryXThresholdDefense);
                _updating = false;
            }

            var series = new Vector2[2][];
            var effectiveCount = _playSelector.SelectedIndex >= 0 &&
                                 _maxBidCountSelector.SelectedIndex >= 0 && 
                                 (bool)_playSelector.SelectedValue
                                      ? (int)_maxBidCountSelector.SelectedValue + 1 : 0;
            series[0] = thresholds.Select((threshold, index) => new Vector2(index, threshold)).ToArray();
            series[1] = thresholds.Take(effectiveCount).Select((threshold, index) => new Vector2(index, threshold)).ToArray();

			_chart.MaxValue = new Vector2(thresholds.Length - 1 + 0.1f, 105);
			_chart.MinValue = Vector2.Zero;
			_chart.GridInterval = new Vector2(1, 10);
			_chart.Data = series;

            var gt = (Hra)_gameTypeSelector.SelectedValue;
            _note.Text = _notes.ContainsKey(gt) ? _notes[gt] : _defaultNote;
            if ((Hra)_gameTypeSelector.SelectedValue == Hra.Betl)
            {
                _safetyBetlThreshold.Show();
                _safetyBetlSelector.Show();
            }
            else
            {
                _safetyBetlThreshold.Hide();
                _safetyBetlSelector.Hide();
            }
        }

        private void UpdateAiSettings()
        {
            if (_threshold0Selector.SelectedIndex == -1 ||
                _threshold1Selector.SelectedIndex == -1 ||
                _threshold2Selector.SelectedIndex == -1 ||
                _threshold3Selector.SelectedIndex == -1 ||
                _maxBidCountSelector.SelectedIndex == -1 ||
                _playSelector.SelectedIndex == -1 ||
                _riskFactorSelector.SelectedIndex == -1 ||
                _safetyBetlSelector.SelectedIndex == -1 ||
                _solitaryXSelector.SelectedIndex == -1 ||
                _solitaryXDefenseSelector.SelectedIndex == -1)
            {
                return;
            }
            var thresholdSettings = Game.Settings.Thresholds.First(i => i.GameType == (Hra)_gameTypeSelector.SelectedValue);

            thresholdSettings.Thresholds = string.Format("{0}|{1}|{2}|{3}", _threshold0Selector.SelectedValue,
                                                                            _threshold1Selector.SelectedValue,
                                                                            _threshold2Selector.SelectedValue,
                                                                            _threshold3Selector.SelectedValue);
            thresholdSettings.MaxBidCount = (int)_maxBidCountSelector.SelectedValue;
            thresholdSettings.Use = (bool)_playSelector.SelectedValue;
            Game.Settings.RiskFactor = (float)_riskFactorSelector.SelectedValue;
            Game.Settings.SolitaryXThreshold = (float)_solitaryXSelector.SelectedValue;
            Game.Settings.SolitaryXThresholdDefense = (float)_solitaryXDefenseSelector.SelectedValue;
            Game.Settings.SafetyBetlThreshold = (int)_safetyBetlSelector.SelectedValue;
        }

        public void BackButtonClicked(object sender)
        {
            if (_settingsChanged)
            {
                Game.Settings.Default = false;
                Game.UpdateSettings();
                _settingsChanged = false;
            }
            Game.SettingsScene.SetActive();
        }

        public void ResetButtonClicked(object sender)
        {            
            Game.Settings.ResetThresholds();
            Game.UpdateSettings();
            _settingsChanged = false;
            UpdateControls();
        }

        public void GameTypeChanged(object sender)
        {
            UpdateControls();
            _playSelector.IsEnabled = ((Hra)_gameTypeSelector.SelectedValue & Hra.Hra) == 0;
            //if (_settingsChanged)
            //{
            //    Game.SettingsScene.UpdateSettings(_settings);
            //    _settingsChanged = false;
            //}
        }

        public void MaxBidCountChanged(object sender)
        {
            if (!_updating)
            {
                _settingsChanged = true;
                UpdateAiSettings();
                UpdateControls(true);
            }
            _maxBidCountSelector.IsEnabled = _playSelector.SelectedIndex >= 0 && (bool)_playSelector.SelectedValue;
            _threshold0Selector.IsEnabled = _playSelector.SelectedIndex >= 0 && (bool)_playSelector.SelectedValue;// && (Hra)_gameTypeSelector.SelectedValue != Hra.Hra;
            _threshold1Selector.IsEnabled = _playSelector.SelectedIndex >= 0 && (bool)_playSelector.SelectedValue && _maxBidCountSelector.SelectedIndex > 0;
            _threshold2Selector.IsEnabled = _playSelector.SelectedIndex >= 0 && (bool)_playSelector.SelectedValue && _maxBidCountSelector.SelectedIndex > 1;
            _threshold3Selector.IsEnabled = _playSelector.SelectedIndex >= 0 && (bool)_playSelector.SelectedValue && _maxBidCountSelector.SelectedIndex > 2;
        }

        public void ThresholdChanged(object sender)
        {
            _recursionLevel++;
            if (sender == _threshold0Selector)
            {
                if (_threshold0Selector.SelectedIndex > _threshold1Selector.SelectedIndex)
                {
                    _threshold1Selector.SelectedIndex = _threshold0Selector.SelectedIndex;
                }
            }
            else if (sender == _threshold1Selector)
            {
                if (_threshold1Selector.SelectedIndex > _threshold2Selector.SelectedIndex)
                {
                    _threshold2Selector.SelectedIndex = _threshold1Selector.SelectedIndex;
                }
                else if (_threshold1Selector.SelectedIndex < _threshold0Selector.SelectedIndex)
                {
                    _threshold0Selector.SelectedIndex = _threshold1Selector.SelectedIndex;
                }
            }
            else if (sender == _threshold2Selector)
            {
                if (_threshold2Selector.SelectedIndex > _threshold3Selector.SelectedIndex)
                {
                    _threshold3Selector.SelectedIndex = _threshold2Selector.SelectedIndex;
                }
                else if (_threshold2Selector.SelectedIndex < _threshold1Selector.SelectedIndex)
                {
                    _threshold1Selector.SelectedIndex = _threshold2Selector.SelectedIndex;
                }
            }
            else if (sender == _threshold3Selector)
            {
                if (_threshold3Selector.SelectedIndex < _threshold2Selector.SelectedIndex)
                {
                    _threshold2Selector.SelectedIndex = _threshold3Selector.SelectedIndex;
                }
            }
            if (_recursionLevel == 1 && !_updating)
            {
                _settingsChanged = true;
                UpdateAiSettings();
                UpdateControls(true);
            }
            _recursionLevel--;
        }
		
        public void RiskFactorChanged(object sender)
        {
            if (!_updating)
            {
                _settingsChanged = true;
                UpdateAiSettings();
            }
		}

        public void SafetyBetlChanged(object sender)
        {
            if (!_updating)
            {
                _settingsChanged = true;
                UpdateAiSettings();
            }
        }

        public void SolitaryXChanged(object sender)
        {
            if (!_updating)
            {
                _settingsChanged = true;
                UpdateAiSettings();
            }
        }

        public void SolitaryXDefenseChanged(object sender)
        {
            if (!_updating)
            {
                _settingsChanged = true;
                UpdateAiSettings();
            }
        }
    }
}
