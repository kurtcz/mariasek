using System;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.GamerServices;

using Mariasek.SharedClient.GameComponents;
using Mariasek.Engine.New;
using System.Linq;

namespace Mariasek.SharedClient
{
    public class SettingsScene : Scene
    {
		//public static string _settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mariasek.settings");
#if __ANDROID__
		private static string _path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "Mariasek");
#else   //#elif __IOS__
        private static string _path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#endif
        public static string _settingsFilePath = Path.Combine(_path, "Mariasek.settings");
		private Label _hint;
        private Label _sounds;
        private Label _bgsounds;
        private Label _handSorting;
        private Label _baseBet;
        private Label _kiloCounting;
        private Label _top107;
        private Label _thinkingTime;
		private Label _cardFace;
        private Label _cardBack;
        private Label _roundFinishedWaitTime;
        private Label _autoFinishRounds;
        private Label _autoFinishLastRound;
        private Label _player1;
        private Label _player2;
        private Label _player3;
        private Label _minBidsForGame;
        private Label _minBidsForSeven;
        private Label _maxHistoryLength;
        private Label _bubbleTime;
        //private Label _showStatusBar;
        private Label _bgImage;
		private ToggleButton _hintBtn;
        private ToggleButton _soundBtn;
        private ToggleButton _bgsoundBtn;
		private LeftRightSelector _handSortingSelector;
		private LeftRightSelector _baseBetSelector;
		private LeftRightSelector _kiloCountingSelector;
        private LeftRightSelector _top107Selector;
		private LeftRightSelector _thinkingTimeSelector;
		private LeftRightSelector _cardFaceSelector;
        private LeftRightSelector _cardBackSelector;
        private LeftRightSelector _roundFinishedWaitTimeSelector;
        private LeftRightSelector _autoFinishRoundsSelector;
        private LeftRightSelector _autoFinishLastRoundSelector;
        private LeftRightSelector _bubbleTimeSelector;
        //private LeftRightSelector _showStatusBarSelector;
        private LeftRightSelector _bgImageSelector;
        private Button _player1Name;
        private Button _player2Name;
        private Button _player3Name;
        private LeftRightSelector _minBidsForGameSelector;
        private LeftRightSelector _minBidsForSevenSelector;
        private LeftRightSelector _maxHistoryLengthSelector;
        private RectangleShape _hline;
        private Label _performance;
        private Button _menuBtn;
        private Button _aiBtn;
        private LeftRightSelector _pageSelector;

        private int currentPage;
        private const int pageOffset = 700; //dost aby na tabletech nebyla videt nasledujici stranka

        public SettingsScene(MariasekMonoGame game)
            : base(game)
        {
            Game.SettingsChanged += SettingsChanged;
            SceneActivated += Activated;
        }

        private void SettingsChanged(object sender, SettingsChangedEventArgs e)
        {
			SoundEffect.MasterVolume = Game.Settings.SoundEnabled ? 1f : 0f;
			Game.AmbientSound.Volume = Game.Settings.BgSoundEnabled ? 0.2f : 0f;
			Microsoft.Xna.Framework.Media.MediaPlayer.Volume = Game.Settings.BgSoundEnabled ? 0.1f : 0f;
		}

        private void Activated(object sender)
        {
			_performance.Text = string.Format("Výkon simulace: {0} her/s",
	            Game.Settings.GameTypeSimulationsPerSecond > 0 ? Game.Settings.GameTypeSimulationsPerSecond.ToString() : "?");
		}

        /// <summary>
        /// Allows the game component to perform any initialization it needs to before starting
        /// to run.  This is where it can query for any required services and load content.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            _performance = new Label(this)
            {
                Position = new Vector2(0, Game.VirtualScreenHeight - 155),
                Width = 220,
                Height = 34,
                HorizontalAlign = HorizontalAlignment.Center,
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Bottom,
                FontScaleFactor = 0.75f
            };
            Game.LoadGameSettings();
            _sounds = new Label(this)
            {
                Position = new Vector2(200, 10),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Zvuk",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _bgsounds = new Label(this)
            {
                Position = new Vector2(200, 70),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Zvuk pozadí",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _soundBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 10),
                Width = 270,
                Height = 50,
                Group = 1,
                Text = Game.Settings.SoundEnabled ? "Zapnutý" : "Vypnutý",
                IsSelected = Game.Settings.SoundEnabled,
				BorderColor = Color.Transparent,
				BackgroundColor = Color.Transparent
            };
            _soundBtn.Click += SoundBtnClick;
            _bgsoundBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 70),
                Width = 270,
                Height = 50,
                Group = 1,
                Text = Game.Settings.BgSoundEnabled ? "Zapnutý" : "Vypnutý",
                IsSelected = Game.Settings.BgSoundEnabled,
                BorderColor = Color.Transparent,
                BackgroundColor = Color.Transparent
            };
            _bgsoundBtn.Click += BgSoundBtnClick;
            _hint = new Label(this)
            {
                Position = new Vector2(200, 130),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Nápověda",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _hintBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 130),
                Width = 270,
                Height = 50,
                Group = 1,
                Text = Game.Settings.HintEnabled ? "Zapnutá" : "Vypnutá",
                IsSelected = Game.Settings.HintEnabled,
				BorderColor = Color.Transparent,
				BackgroundColor = Color.Transparent
            };
            _hintBtn.Click += HintBtnClick;
            _cardFace = new Label(this)
			{
				Position = new Vector2(200, 190),
				Width = (int)Game.VirtualScreenWidth / 2 - 150,
				Height = 50,
                Group = 1,
                Text = "Vzor karet",
				HorizontalAlign = HorizontalAlignment.Center,
				VerticalAlign = VerticalAlignment.Middle
			};
            _cardFaceSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 300, 190),
				Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Jednohlavé", CardFace.Single }, { "Dvouhlavé", CardFace.Double } }
			};
			_cardFaceSelector.SelectedIndex = _cardFaceSelector.Items.FindIndex(Game.Settings.CardDesign);
			_cardFaceSelector.SelectionChanged += CardFaceChanged;
			if (_cardFaceSelector.SelectedIndex < 0)
			{
				_cardFaceSelector.SelectedIndex = 0;
			}
            _cardBack = new Label(this)
			{
				Position = new Vector2(200, 250),
				Width = (int)Game.VirtualScreenWidth / 2 - 150,
				Height = 50,
                Group = 1,
                Text = "Rub karet",
				HorizontalAlign = HorizontalAlignment.Center,
				VerticalAlign = VerticalAlignment.Middle
			};
            _cardBackSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 300, 250),
				Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Káro", CardBackSide.Tartan }, { "Koník", CardBackSide.Horse }, { "Krajka", CardBackSide.Lace } }
			};
			_cardBackSelector.SelectedIndex = _cardBackSelector.Items.FindIndex(Game.Settings.CardBackSide);
			_cardBackSelector.SelectionChanged += CardBackChanged;
			if (_cardBackSelector.SelectedIndex < 0)
			{
				_cardBackSelector.SelectedIndex = 0;
			}
            _handSorting = new Label(this)
            {
                Position = new Vector2(200, 310),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Řadit karty",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _handSortingSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 300, 310),
				Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Vzestupně", SortMode.Ascending }, { "Sestupně", SortMode.Descending }, { "Vůbec", SortMode.None } }
			};
			_handSortingSelector.SelectedIndex = _handSortingSelector.Items.FindIndex(Game.Settings.SortMode);
			_handSortingSelector.SelectionChanged += SortModeChanged;
            _baseBet = new Label(this)
            {
                Position = new Vector2(200, 370),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Hodnota hry",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _baseBetSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 300, 370),
				Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "0,10 Kč", 0.1f}, { "0,20 Kč", 0.2f}, { "0,50 Kč", 0.5f },
											  { "1 Kč", 1f }, { "2 Kč", 2f}, { "5 Kč", 5f}, { "10 Kč", 10f} }
			};
			_baseBetSelector.SelectedIndex = _baseBetSelector.Items.FindIndex(Game.Settings.BaseBet);
			_baseBetSelector.SelectionChanged += BaseBetChanged;
// PAGE 2 //
            _minBidsForGame = new Label(this)
            {
                Position = new Vector2(200, pageOffset + 10),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Hru hrát",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _minBidsForGameSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, pageOffset + 10),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Vždy", 0 }, { "S Flekem", 1 }, { "S Re", 2 } }
            };
            _minBidsForGameSelector.SelectedIndex = _minBidsForGameSelector.Items.FindIndex(Game.Settings.MinimalBidsForGame);
            _minBidsForGameSelector.SelectionChanged += MinBidsForGameChanged;
            if (_minBidsForGameSelector.SelectedIndex < 0)
            {
                _minBidsForGameSelector.SelectedIndex = 0;
            }
            _minBidsForSeven = new Label(this)
            {
                Position = new Vector2(200, pageOffset + 70),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Sedmu hrát",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _minBidsForSevenSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, pageOffset + 70),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Vždy", 0 }, { "S Flekem", 1 }, { "S Re", 2 } }
            };
            _minBidsForSevenSelector.SelectedIndex = _minBidsForSevenSelector.Items.FindIndex(Game.Settings.MinimalBidsForSeven);
            _minBidsForSevenSelector.SelectionChanged += MinBidsForSevenChanged;
            if (_minBidsForSevenSelector.SelectedIndex < 0)
            {
                _minBidsForSevenSelector.SelectedIndex = 0;
            }
            _kiloCounting = new Label(this)
			{
                Position = new Vector2(200, pageOffset + 130),
				Width = (int)Game.VirtualScreenWidth / 2 - 150,
				Height = 50,
                Group = 1,
                Text = "Počítání peněz u kila",
				HorizontalAlign = HorizontalAlignment.Center,
				VerticalAlign = VerticalAlignment.Middle
			};
            _kiloCountingSelector = new LeftRightSelector(this)
			{
                Position = new Vector2(Game.VirtualScreenWidth - 300, pageOffset + 130),
				Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Sčítat", CalculationStyle.Adding}, { "Násobit", CalculationStyle.Multiplying} }
			};
			_kiloCountingSelector.SelectedIndex = _kiloCountingSelector.Items.FindIndex(Game.Settings.CalculationStyle);
			_kiloCountingSelector.SelectionChanged += KiloCountingChanged;
            if (_kiloCountingSelector.SelectedIndex < 0)
            {
                _kiloCountingSelector.SelectedIndex = 0;
            }
            _top107 = new Label(this)
            {
                Position = new Vector2(200, pageOffset + 190),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Hrát stosedm z ruky",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _top107Selector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, pageOffset + 190),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Ne", false }, { "Ano", true } }
            };
            _top107Selector.SelectedIndex = _top107Selector.Items.FindIndex(Game.Settings.Top107);
            _top107Selector.SelectionChanged += Top107Changed;
            if (_top107Selector.SelectedIndex < 0)
            {
                _top107Selector.SelectedIndex = 0;
            }
            _thinkingTime = new Label(this)
            {
                Position = new Vector2(200, pageOffset + 250),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
            	Height = 50,
                Group = 1,
                Text = "Jak dlouho AI přemýšlí",
            	HorizontalAlign = HorizontalAlignment.Center,
            	VerticalAlign = VerticalAlignment.Middle
            };
            _thinkingTimeSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, pageOffset + 250),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Krátce", 750 }, { "Středně", 1500 }, { "Dlouho", 2500 } }
            };
            _thinkingTimeSelector.SelectedIndex = _thinkingTimeSelector.Items.FindIndex(Game.Settings.ThinkingTimeMs);
            _thinkingTimeSelector.SelectionChanged += ThinkingTimeChanged;
            if (_thinkingTimeSelector.SelectedIndex < 0)
            {
            	_thinkingTimeSelector.SelectedIndex = 1;
            }
            _bubbleTime = new Label(this)
            {
                Position = new Vector2(200, pageOffset + 310),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Rychlost dialogů",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _bubbleTimeSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, pageOffset + 310),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Rychlá", 500 }, { "Střední", 1000 }, { "Pomalá", 2000 } }
            };
            _bubbleTimeSelector.SelectedIndex = _bubbleTimeSelector.Items.FindIndex(Game.Settings.BubbleTimeMs);
            _bubbleTimeSelector.SelectionChanged += BubbleTimeChanged;
            if (_bubbleTimeSelector.SelectedIndex < 0)
            {
                _bubbleTimeSelector.SelectedIndex = 1;
            }
            _bgImage = new Label(this)
            {
                Position = new Vector2(200, pageOffset + 370),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Pozadí při hře",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _bgImageSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, pageOffset + 370),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Výchozí", BackgroundImage.Default }, { "Plátno", BackgroundImage.Canvas }, { "Tmavé", BackgroundImage.Dark } }
            };
            _bgImageSelector.SelectedIndex = _bgImageSelector.Items.FindIndex(Game.Settings.BackgroundImage);
            _bgImageSelector.SelectionChanged += BackgroundImageChanged;
            if (_bgImageSelector.SelectedIndex < 0)
            {
                _bgImageSelector.SelectedIndex = 0;
            }            
// PAGE 3 //
            _player1 = new Label(this)
            {
                Position = new Vector2(200, 2 * pageOffset + 10),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Jméno hráče č.1",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _player1Name = new Button(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 2 * pageOffset + 10),
                Width = 270,
                Height = 50,
                Group = 1,
                Text = Game.Settings.PlayerNames[0],
                Tag = 1,
                BorderColor = Color.Transparent,
                BackgroundColor = Color.Transparent
            };
            _player1Name.Click += ChangePlayerName;
            _player2 = new Label(this)
            {
                Position = new Vector2(200, 2 * pageOffset + 70),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Jméno hráče č.2",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _player2Name = new Button(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 2 * pageOffset + 70),
                Width = 270,
                Height = 50,
                Group = 1,
                Text = Game.Settings.PlayerNames[1],
                Tag = 2,
                BorderColor = Color.Transparent,
                BackgroundColor = Color.Transparent
            };
            _player2Name.Click += ChangePlayerName;
            _player3 = new Label(this)
            {
                Position = new Vector2(200, 2 * pageOffset + 130),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Jméno hráče č.3",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _player3Name = new Button(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 2 * pageOffset + 130),
                Width = 270,
                Height = 50,
                Group = 1,
                Text = Game.Settings.PlayerNames[2],
                Tag = 3,
                BorderColor = Color.Transparent,
                BackgroundColor = Color.Transparent
            };
            _player3Name.Click += ChangePlayerName;
            _autoFinishRounds = new Label(this)
            {
                Position = new Vector2(200, 2 * pageOffset + 190),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Ukončení kola",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _autoFinishRoundsSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 2 * pageOffset + 190),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Automaticky", true }, { "Ručně", false } }
            };
            _autoFinishRoundsSelector.SelectedIndex = _autoFinishRoundsSelector.Items.FindIndex(Game.Settings.AutoFinishRounds);
            _autoFinishRoundsSelector.SelectionChanged += AutoFinishRoundsChanged;
            if (_autoFinishRoundsSelector.SelectedIndex < 0)
            {
                _autoFinishRoundsSelector.SelectedIndex = 0;
            }
            //_showStatusBar = new Label(this)
            //{
            //    Position = new Vector2(200, 2 * pageOffset + 250),
            //    Width = (int)Game.VirtualScreenWidth / 2 - 150,
            //    Height = 50,
            //    Text = "Stavový řádek",
            //    Group = 1,
            //    HorizontalAlign = HorizontalAlignment.Center,
            //    VerticalAlign = VerticalAlignment.Middle
            //};
            //_showStatusBarSelector = new LeftRightSelector(this)
            //{
            //    Position = new Vector2(Game.VirtualScreenWidth - 300, 2 * pageOffset + 250),
            //    Width = 270,
            //    Group = 1,
            //    Items = new SelectorItems() { { "Nezobrazovat", false }, { "Zobrazit", true } }
            //};
            //_showStatusBarSelector.SelectedIndex = _showStatusBarSelector.Items.FindIndex(Game.Settings.ShowStatusBar);
            //_showStatusBarSelector.SelectionChanged += ShowStatusBarChanged;
            //if (_showStatusBarSelector.SelectedIndex < 0)
            //{
            //    _showStatusBarSelector.SelectedIndex = 0;
            //}
            _roundFinishedWaitTime = new Label(this)
            {
                Position = new Vector2(200, 2 * pageOffset + 250),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Čekání na konci kola",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _roundFinishedWaitTimeSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 2 * pageOffset + 250),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Krátké", 1000 }, { "Dlouhé", 2000 } }
            };
            _roundFinishedWaitTimeSelector.SelectedIndex = _roundFinishedWaitTimeSelector.Items.FindIndex(Game.Settings.RoundFinishedWaitTimeMs);
            _roundFinishedWaitTimeSelector.SelectionChanged += RoundFinishedWaitTimeChanged ;
            if (_roundFinishedWaitTimeSelector.SelectedIndex < 0)
            {
                _roundFinishedWaitTimeSelector.SelectedIndex = 0;
            }
            _autoFinishLastRound = new Label(this)
            {
                Position = new Vector2(200, 2 * pageOffset + 310),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Poslední kolo sehrát",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _autoFinishLastRoundSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 2 * pageOffset + 310),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Automaticky", true }, { "Ručně", false } }
            };
            _autoFinishLastRoundSelector.SelectedIndex = _autoFinishLastRoundSelector.Items.FindIndex(Game.Settings.AutoFinish);
            _autoFinishLastRoundSelector.SelectionChanged += AutoFinishLastRoundChanged;
            if (_autoFinishLastRoundSelector.SelectedIndex < 0)
            {
                _autoFinishLastRoundSelector.SelectedIndex = 0;
            }
            _maxHistoryLength = new Label(this)
            {
                Position = new Vector2(200, 2 * pageOffset + 370),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Max. délka historie",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _maxHistoryLengthSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 2 * pageOffset + 370),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Neomezená", 0 }, { "1000 her", 1000 }, { "2000 her", 2000 }, { "5000 her", 5000 }, { "10000 her", 10000 } }
            };
            _maxHistoryLengthSelector.SelectedIndex = _maxHistoryLengthSelector.Items.FindIndex(Game.Settings.MaxHistoryLength);
            _maxHistoryLengthSelector.SelectionChanged += MaxHistoryLengthChanged;
            if (_maxHistoryLengthSelector.SelectedIndex < 0)
            {
                _maxHistoryLengthSelector.SelectedIndex = 0;
            }

            _menuBtn = new Button(this)
            {
                Position = new Vector2(10, Game.VirtualScreenHeight - 60),
                Width = 200,
                Height = 50,
                Text = "Menu",
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Bottom
            };
            _menuBtn.Click += MenuBtnClick;
            _aiBtn = new Button(this)
            {
                Position = new Vector2(10, Game.VirtualScreenHeight - 120),
                Width = 200,
                Height = 50,
                Text = "Nastavení AI",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Bottom
            };
            _aiBtn.Click += AiBtnClick;
            _pageSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(210, (int)Game.VirtualScreenHeight - 60),
                Width = (int)Game.VirtualScreenWidth - 210,
                Items = new SelectorItems() { { "1/3", 0 }, { "2/3", 1 }, { "3/3", 2 } },
                TextRenderer = Game.FontRenderers["SegoeUI40Outl"]
            };
            _pageSelector.SelectedIndex = 0;
            _pageSelector.SelectionChanged += PageChanged;
            if (_pageSelector.SelectedIndex < 0)
            {
                _pageSelector.SelectedIndex = 0;
            }
            _hline = new RectangleShape(this)
            {
                Position = new Vector2(210, (int)Game.VirtualScreenHeight - 60),
                Width = (int)Game.VirtualScreenWidth - 210,
                Height = 3,
                BackgroundColors = { Color.White },
                BorderColors = { Color.Transparent },
                BorderRadius = 0,
                BorderThickness = 1,
                Opacity = 0.7f
            };
			_performance.Text = string.Format("Výkon simulace: {0} her/s",
				Game.Settings.GameTypeSimulationsPerSecond > 0 ? Game.Settings.GameTypeSimulationsPerSecond.ToString() : "?");

			Background = Game.Content.Load<Texture2D>("wood2");
            BackgroundTint = Color.DimGray;
            Game.OnSettingsChanged();
        }

        void HintBtnClick (object sender)
        {
            var btn = sender as ToggleButton;

            Game.Settings.HintEnabled = btn.IsSelected;
            _hintBtn.Text = Game.Settings.HintEnabled ? "Zapnutá" : "Vypnutá";
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void SoundBtnClick (object sender)
        {
            var btn = sender as ToggleButton;

            Game.Settings.SoundEnabled = btn.IsSelected;
            _soundBtn.Text = Game.Settings.SoundEnabled ? "Zapnutý" : "Vypnutý";
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void BgSoundBtnClick(object sender)
        {
            var btn = sender as ToggleButton;

            Game.Settings.BgSoundEnabled = btn.IsSelected;
            _bgsoundBtn.Text = Game.Settings.BgSoundEnabled ? "Zapnutý" : "Vypnutý";
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void SortModeChanged (object sender)
        {
			var selector = sender as LeftRightSelector;

			Game.Settings.SortMode = (SortMode)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void BaseBetChanged (object sender)
        {
			var selector = sender as LeftRightSelector;

			Game.Settings.BaseBet = (float)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

		void ThinkingTimeChanged(object sender)
		{
			var selector = sender as LeftRightSelector;

			Game.Settings.ThinkingTimeMs = (int)selector.SelectedValue;
			Game.SaveGameSettings();
			Game.OnSettingsChanged();
		}

		void KiloCountingChanged (object sender)
        {
			var selector = sender as LeftRightSelector;

			Game.Settings.CalculationStyle = (CalculationStyle)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

		void CardFaceChanged(object sender)
		{
			var selector = sender as LeftRightSelector;
			var origValue = Game.Settings.CardDesign;

			Game.Settings.CardDesign = (CardFace)selector.SelectedValue;
			Game.SaveGameSettings();
			Game.OnSettingsChanged();
		}

		void CardBackChanged(object sender)
		{
			var selector = sender as LeftRightSelector;
			var origValue = Game.Settings.CardBackSide;

			Game.Settings.CardBackSide = (CardBackSide)selector.SelectedValue;
			Game.SaveGameSettings();
			Game.OnSettingsChanged();
		}

        void AutoFinishRoundsChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.AutoFinishRounds = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void AutoFinishLastRoundChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.AutoFinish = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void RoundFinishedWaitTimeChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.RoundFinishedWaitTimeMs = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void Top107Changed(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.Top107 = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void MaxHistoryLengthChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.MaxHistoryLength = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void BubbleTimeChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.BubbleTimeMs = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void ShowStatusBarChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.ShowStatusBar = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void BackgroundImageChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.BackgroundImage = (BackgroundImage)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void MinBidsForGameChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.MinimalBidsForGame = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void MinBidsForSevenChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.MinimalBidsForSeven = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private int editedPlayerIndex;

        void ChangePlayerName(object sender)
        {
            var button = sender as Button;
            var playerIndex = (int)button.Tag;

            editedPlayerIndex = playerIndex;
            Guide.BeginShowKeyboardInput(PlayerIndex.One,
                                         "Jméno hráče",
                                         $"Zadej jméno hráče č.{playerIndex}",
                                         Game.Settings.PlayerNames[playerIndex - 1],
                                         PlayerNameChangedCallback,
                                         //editedPlayerIndex);
                                         null);
        }

        void PlayerNameChangedCallback(IAsyncResult result)
        {
            const int MaxNameLength = 12;
            var text = Guide.EndShowKeyboardInput(result);

            if(text == null)
            {
                return;
            }
            text = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).First().Trim();
            if (text.Length > MaxNameLength)
            {
                text = text.Substring(0, MaxNameLength);
            }
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            //var playerIndex = (int)result.AsyncState;
            var playerIndex = editedPlayerIndex;
            var button = Children.Select(i => i as Button)
                                 .Where(i => i != null && (int)i.Tag == playerIndex)
                                 .FirstOrDefault();
            if (button != null)
            {
                button.Text = text;
            }
            Game.Settings.PlayerNames[playerIndex - 1] = text;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void PageChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            ShowPage((int)selector.SelectedValue);
        }

        void ShowPage(int pageIndex)
        {
            var delta = pageIndex - currentPage;
            var controls = Children.Where(i => i.Group == 1);

            if (delta != 0)
            {
                foreach (var control in controls)
                {
                    control.Position = new Vector2(control.Position.X, control.Position.Y - delta * pageOffset);
                }
            }
            currentPage = pageIndex;
        }

        void MenuBtnClick (object sender)
        {
            Game.MenuScene.SetActive();
        }

        void AiBtnClick(object sender)
        {
            Game.AiSettingsScene.UpdateControls();
            Game.AiSettingsScene.SetActive();
        }
    }
}

