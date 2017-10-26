using System;
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
        private RectangleShape _hline;
        private RectangleShape _shadow;
        private LeftRightSelector _gameTypeSelector;
        private LeftRightSelector _playSelector;
        private LeftRightSelector _maxBidCountSelector;
        private LeftRightSelector _threshold0Selector;
        private LeftRightSelector _threshold1Selector;
        private LeftRightSelector _threshold2Selector;
        private LeftRightSelector _threshold3Selector;
        private LeftRightSelector _riskFactorSelector;
        private LineChart _chart;
        private Label _0Percent;
        private Label _50Percent;
        private Label _100Percent;
        private Label _note;

        private static readonly Dictionary<Hra, string> _notes = new Dictionary<Hra, string>
        {
            { Hra.Hra, "AI flekuje jen když trhá\nnebo má aspoň 40 bodů v hlasech." },
            { Hra.Sedma, "AI flekuje jen když má aspoň 2 trumfy." },
            { Hra.Kilo, "AI nevolí kilo pokud netrhá\na nemá aspoň 40 bodů v hlasech." },
            { Hra.Betl, "AI používá práh pro Flek když nevolil\na hlásí špatnou barvu. Pokud AI nevolil, neflekuje." },
            { Hra.Durch, "AI používá práh pro Flek když nevolil\na hlásí špatnou barvu." }
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
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 100, Game.VirtualScreenHeight / 2 - 175),
                Width = 300,
                Height = 50,
                Text = "Hrát",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _playSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 + 200, Game.VirtualScreenHeight / 2 - 175),
                Width = (int)Game.VirtualScreenWidth / 2 - 200,
                Items = new SelectorItems() { { "Ano", true }, { "Ne", false } }
            };
            _playSelector.SelectionChanged += MaxBidCountChanged;
            _play.Hide();
            _playSelector.Hide();

            _maxBidCount = new Label(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 100, Game.VirtualScreenHeight / 2 - 115),
                Width = 300,
                Height = 50,
                Text = "Flekovat max.",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _maxBidCountSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 + 200, Game.VirtualScreenHeight / 2 - 115),
                Width = (int)Game.VirtualScreenWidth / 2 - 200,
                Items = new SelectorItems() { { "0x", 0 }, { "1x", 1 }, { "2x", 2 }, { "3x", 3 } }
            };
            _maxBidCountSelector.SelectionChanged += MaxBidCountChanged;

            _threshold0 = new Label(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 100, Game.VirtualScreenHeight / 2 - 55),
                Width = 300,
                Height = 50,
                Text = "Volba",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _threshold0Selector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 + 200, Game.VirtualScreenHeight / 2 - 55),
                Width = (int)Game.VirtualScreenWidth / 2 - 200,
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
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 100, Game.VirtualScreenHeight / 2 + 5),
                Width = 300,
                Height = 50,
                Text = "Flek",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _threshold1Selector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 + 200, Game.VirtualScreenHeight / 2 + 5),
                Width = (int)Game.VirtualScreenWidth / 2 - 200,
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
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 100, Game.VirtualScreenHeight / 2 + 65),
                Width = 300,
                Height = 50,
                Text = "Re",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _threshold2Selector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 + 200, Game.VirtualScreenHeight / 2 + 65),
                Width = (int)Game.VirtualScreenWidth / 2 - 200,
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
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 100, Game.VirtualScreenHeight / 2 + 125),
                Width = 300,
                Height = 50,
                Text = "Tutti",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _threshold3Selector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 + 200, Game.VirtualScreenHeight / 2 + 125),
                Width = (int)Game.VirtualScreenWidth / 2 - 200,
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
				Text = "",
				HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Top
			};

			_riskFactor = new Label(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth / 2 - 100, Game.VirtualScreenHeight / 2 - 235),
				Width = 300,
				Height = 50,
				Text = "Risk faktor",
				HorizontalAlign = HorizontalAlignment.Center,
				VerticalAlign = VerticalAlignment.Middle
			};
			_riskFactorSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth / 2 + 200, Game.VirtualScreenHeight / 2 - 235),
				Width = (int)Game.VirtualScreenWidth / 2 - 200,
                Items = new SelectorItems(
                    Enumerable.Range(0, 101)
                              .Select(i => new KeyValuePair<string, object>(string.Format("{0}%", i), i / 100f))
                              .ToList()
                )
			};
			_riskFactorSelector.SelectionChanged += RiskFactorChanged;

			Background = Game.Content.Load<Texture2D>("wood2");
            BackgroundTint = Color.DimGray;

            _settingsChanged = false;
			GameTypeChanged(this);
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
                _riskFactorSelector.SelectedIndex = _riskFactorSelector.Items.FindIndex(Game.Settings.RiskFactor);
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
            _note.Text = _notes.ContainsKey(gt) ? _notes[gt] : string.Empty;
        }

        private void UpdateAiSettings()
        {
            if (_threshold0Selector.SelectedIndex == -1 ||
                _threshold1Selector.SelectedIndex == -1 ||
                _threshold2Selector.SelectedIndex == -1 ||
                _threshold3Selector.SelectedIndex == -1 ||
                _maxBidCountSelector.SelectedIndex == -1 ||
                _playSelector.SelectedIndex == -1 ||
                _riskFactorSelector.SelectedIndex == -1)
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
            _threshold0Selector.IsEnabled = _playSelector.SelectedIndex >= 0 && (bool)_playSelector.SelectedValue && (Hra)_gameTypeSelector.SelectedValue != Hra.Hra;
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
			_settingsChanged = true;
			UpdateAiSettings();
		}
	}
}
