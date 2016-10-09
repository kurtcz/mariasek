using System;
using System.Xml.Serialization;
using System.IO;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

using Mariasek.SharedClient.GameComponents;
using Mariasek.Engine.New;

namespace Mariasek.SharedClient
{
    public class SettingsScene : Scene
    {
        private string _settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mariasek.settings");

        private Label _hint;
        private Label _sounds;
        private Label _handSorting;
        private Label _baseBet;
        private Label _kiloCounting;
        private Label _thinkingTime;
		private Label _cardFace;
		private ToggleButton _hintBtn;
        private ToggleButton _soundBtn;
		private LeftRightSelector _handSortingSelector;
		private LeftRightSelector _baseBetSelector;
		private LeftRightSelector _kiloCountingSelector;
		private LeftRightSelector _thinkingTimeSelector;
		private LeftRightSelector _cardFaceSelector;
        private Label _performance;
        private Button _menuBtn;

        private GameSettings _settings;

        public delegate void SettingsChangedEventHandler(object sender, SettingsChangedEventArgs e);
        public event SettingsChangedEventHandler SettingsChanged;
        protected virtual void OnSettingsChanged()
        {
            if (SettingsChanged != null)
            {
                SettingsChanged(this, new SettingsChangedEventArgs{ Settings = this._settings });
            }
        }

        public SettingsScene(MariasekMonoGame game)
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

            _performance = new Label(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 405, Game.VirtualScreenHeight - 34),
                Width = 400,
                Height = 34,
                HorizontalAlign = HorizontalAlignment.Right,
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Right : AnchorType.Bottom
            };
            LoadGameSettings();
            _sounds = new Label(this)
            {
                Position = new Vector2(10, 10),
                Width = (int)Game.VirtualScreenWidth / 2 - 20,
                Height = 50,
                Text = "Zvuk",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _soundBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 390, 10),
                Width = 360,
                Height = 50,
                Text = _settings.SoundEnabled ? "Zapnutý" : "Vypnutý",
                IsSelected = _settings.SoundEnabled,
				BorderColor = Color.Transparent,
				BackgroundColor = Color.Transparent
            };
            _soundBtn.Click += SoundBtnClick;
            _hint = new Label(this)
            {
                Position = new Vector2(10, 70),
                Width = (int)Game.VirtualScreenWidth / 2 - 20,
                Height = 50,
                Text = "Nápověda",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _hintBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 390, 70),
                Width = 360,
                Height = 50,
                Text = _settings.HintEnabled ? "Zapnutá" : "Vypnutá",
                IsSelected = _settings.HintEnabled,
				BorderColor = Color.Transparent,
				BackgroundColor = Color.Transparent
            };
            _hintBtn.Click += HintBtnClick;
			_cardFace = new Label(this)
			{
				Position = new Vector2(10, 130),
				Width = (int)Game.VirtualScreenWidth / 2 - 20,
				Height = 50,
				Text = "Vzor karet",
				HorizontalAlign = HorizontalAlignment.Center,
				VerticalAlign = VerticalAlignment.Middle
			};
			_cardFaceSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 390, 130),
				Width = 360,
				Items = new SelectorItems() { { "Jednohlavé", CardFace.Single }, { "Dvouhlavé", CardFace.Double } }
			};
			_cardFaceSelector.SelectedIndex = _cardFaceSelector.Items.FindIndex(_settings.CardDesign);
			_cardFaceSelector.SelectionChanged += CardFaceChanged;
			if (_cardFaceSelector.SelectedIndex < 0)
			{
				_cardFaceSelector.SelectedIndex = 0;
			}
            _handSorting = new Label(this)
            {
                Position = new Vector2(10, 190),
                Width = (int)Game.VirtualScreenWidth / 2 - 20,
                Height = 50,
                Text = "Řadit karty",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
			_handSortingSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 390, 190),
				Width = 360,
				Items = new SelectorItems() { { "Vzestupně", SortMode.Ascending }, { "Sestupně", SortMode.Descending }, { "Vůbec", SortMode.None } }
			};
			_handSortingSelector.SelectedIndex = _handSortingSelector.Items.FindIndex(_settings.SortMode);
			_handSortingSelector.SelectionChanged += SortModeChanged;
            _baseBet = new Label(this)
            {
                Position = new Vector2(10, 250),
                Width = (int)Game.VirtualScreenWidth / 2 - 20,
                Height = 50,
                Text = "Hodnota hry",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
			_baseBetSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 390, 250),
				Width = 360,
				Items = new SelectorItems() { { "0,10 Kč", 0.1f}, { "0,20 Kč", 0.2f}, { "0,50 Kč", 0.5f },
											  { "1 Kč", 1f }, { "2 Kč", 2f}, { "5 Kč", 5f}, { "10 Kč", 10f} }
			};
			_baseBetSelector.SelectedIndex = _baseBetSelector.Items.FindIndex(_settings.BaseBet);
			_baseBetSelector.SelectionChanged += BaseBetChanged;
			_kiloCounting = new Label(this)
			{
				Position = new Vector2(10, 310),
				Width = (int)Game.VirtualScreenWidth / 2 - 20,
				Height = 50,
				Text = "Počítání peněz u kila",
				HorizontalAlign = HorizontalAlignment.Center,
				VerticalAlign = VerticalAlignment.Middle
			};
			_kiloCountingSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 390, 310),
				Width = 360,
				Items = new SelectorItems() { { "Sčítat", CalculationStyle.Adding}, { "Násobit", CalculationStyle.Multiplying} }
			};
			_kiloCountingSelector.SelectedIndex = _kiloCountingSelector.Items.FindIndex(_settings.CalculationStyle);
			_kiloCountingSelector.SelectionChanged += KiloCountingChanged;
			_thinkingTime = new Label(this)
			{
				Position = new Vector2(10, 370),
				Width = (int)Game.VirtualScreenWidth / 2 - 20,
				Height = 50,
				Text = "Jak dlouho AI přemýšlí",
				HorizontalAlign = HorizontalAlignment.Center,
				VerticalAlign = VerticalAlignment.Middle
			};
			_thinkingTimeSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 390, 370),
				Width = 360,
				Items = new SelectorItems() { { "Krátce", 2000 }, { "Středně", 4000 }, { "Dlouho", 6000 } }
			};
			_thinkingTimeSelector.SelectedIndex = _thinkingTimeSelector.Items.FindIndex(_settings.ThinkingTimeMs);
			_thinkingTimeSelector.SelectionChanged += ThinkingTimeChanged;
			if (_thinkingTimeSelector.SelectedIndex < 0)
			{
				_thinkingTimeSelector.SelectedIndex = 0;
			}

            _menuBtn = new Button(this)
            {
                Position = new Vector2(10, Game.VirtualScreenHeight - 60),
                Width = 100,
                Height = 50,
                Text = "Menu",
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Bottom
            };
            _menuBtn.Click += MenuBtnClick;

            Background = Game.Content.Load<Texture2D>("wood2");
            BackgroundTint = Color.DimGray;
            OnSettingsChanged();
        }

        void HintBtnClick (object sender)
        {
            var btn = sender as ToggleButton;

            _settings.HintEnabled = btn.IsSelected;
            _hintBtn.Text = _settings.HintEnabled ? "Zapnutá" : "Vypnutá";
            SaveGameSettings();
            OnSettingsChanged();
        }

        void SoundBtnClick (object sender)
        {
            var btn = sender as ToggleButton;

            _settings.SoundEnabled = btn.IsSelected;
            _soundBtn.Text = _settings.SoundEnabled ? "Zapnutý" : "Vypnutý";
            SaveGameSettings();
            OnSettingsChanged();
        }

        void SortModeChanged (object sender)
        {
			var selector = sender as LeftRightSelector;

			_settings.SortMode = (SortMode)selector.SelectedValue;
            SaveGameSettings();
            OnSettingsChanged();
        }

        void BaseBetChanged (object sender)
        {
			var selector = sender as LeftRightSelector;

			_settings.BaseBet = (float)selector.SelectedValue;
            SaveGameSettings();
            OnSettingsChanged();
        }

		void ThinkingTimeChanged(object sender)
		{
			var selector = sender as LeftRightSelector;

			_settings.ThinkingTimeMs = (int)selector.SelectedValue;
			SaveGameSettings();
			OnSettingsChanged();
		}

		void KiloCountingChanged (object sender)
        {
			var selector = sender as LeftRightSelector;

			_settings.CalculationStyle = (CalculationStyle)selector.SelectedValue;
            SaveGameSettings();
            OnSettingsChanged();
        }

		void CardFaceChanged(object sender)
		{
			var selector = sender as LeftRightSelector;
			var origValue = _settings.CardDesign;

			_settings.CardDesign = (CardFace)selector.SelectedValue;
			SaveGameSettings();
			OnSettingsChanged();
		}

        void MenuBtnClick (object sender)
        {
            Game.MenuScene.SetActive();
        }


        public void LoadGameSettings(bool forceLoad = true)
        {
            if (!forceLoad && _settings != null)
            {
                return;
            }

            var xml = new XmlSerializer(typeof(GameSettings));
            try
            {
                using (var fs = File.Open(_settingsFilePath, FileMode.Open))
                {
                    _settings = (GameSettings)xml.Deserialize(fs);
                }
            }
            catch(Exception e)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Cannot load settings\n{0}", e.Message));
                _settings = new GameSettings();
            }
            _performance.Text = string.Format("Výkon simulace: {0} her/s, {1} kol/s", 
                _settings.GameTypeSimulationsPerSecond > 0 ? _settings.GameTypeSimulationsPerSecond.ToString() : "?", 
                _settings.RoundSimulationsPerSecond > 0 ? _settings.RoundSimulationsPerSecond.ToString() : "?");
        }

        public void SaveGameSettings()
        {            
            var xml = new XmlSerializer(typeof(GameSettings));
            try
            {
                using (var fs = File.Open(_settingsFilePath, FileMode.Create))
                {
                    xml.Serialize(fs, _settings);
                }
            }
            catch(Exception e)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Cannot save settings\n{0}", e.Message));
            }
            LoadGameSettings();
        }

        public void UpdateSettings(GameSettings settings)
        {
            _settings = settings;
            SaveGameSettings();
        }
    }
}

