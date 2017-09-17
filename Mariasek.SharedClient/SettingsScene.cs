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
        //private Label _thinkingTime;
		private Label _cardFace;
        private Label _cardBack;
		private ToggleButton _hintBtn;
        private ToggleButton _soundBtn;
        private ToggleButton _bgsoundBtn;
		private LeftRightSelector _handSortingSelector;
		private LeftRightSelector _baseBetSelector;
		private LeftRightSelector _kiloCountingSelector;
		//private LeftRightSelector _thinkingTimeSelector;
		private LeftRightSelector _cardFaceSelector;
        private LeftRightSelector _cardBackSelector;
        private Label _performance;
        private Button _menuBtn;
        private Button _aiBtn;

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
			MediaPlayer.Volume = Game.Settings.BgSoundEnabled ? 0.1f : 0f;
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
                Position = new Vector2(Game.VirtualScreenWidth - 405, Game.VirtualScreenHeight - 34),
                Width = 400,
                Height = 34,
                HorizontalAlign = HorizontalAlignment.Right,
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Right : AnchorType.Bottom
            };
            Game.LoadGameSettings();
            _sounds = new Label(this)
            {
                Position = new Vector2(200, 10),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Zvuk",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _bgsounds = new Label(this)
            {
                Position = new Vector2(200, 70),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Zvuk pozadí",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _soundBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 10),
                Width = 270,
                Height = 50,
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
                Text = "Nápověda",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _hintBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 130),
                Width = 270,
                Height = 50,
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
				Text = "Vzor karet",
				HorizontalAlign = HorizontalAlignment.Center,
				VerticalAlign = VerticalAlignment.Middle
			};
			_cardFaceSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 300, 190),
				Width = 270,
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
				Text = "Rub karet",
				HorizontalAlign = HorizontalAlignment.Center,
				VerticalAlign = VerticalAlignment.Middle
			};
			_cardBackSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 300, 250),
				Width = 270,
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
                Text = "Řadit karty",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
			_handSortingSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 300, 310),
				Width = 270,
				Items = new SelectorItems() { { "Vzestupně", SortMode.Ascending }, { "Sestupně", SortMode.Descending }, { "Vůbec", SortMode.None } }
			};
			_handSortingSelector.SelectedIndex = _handSortingSelector.Items.FindIndex(Game.Settings.SortMode);
			_handSortingSelector.SelectionChanged += SortModeChanged;
            _baseBet = new Label(this)
            {
                Position = new Vector2(200, 370),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Hodnota hry",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
			_baseBetSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 300, 370),
				Width = 270,
				Items = new SelectorItems() { { "0,10 Kč", 0.1f}, { "0,20 Kč", 0.2f}, { "0,50 Kč", 0.5f },
											  { "1 Kč", 1f }, { "2 Kč", 2f}, { "5 Kč", 5f}, { "10 Kč", 10f} }
			};
			_baseBetSelector.SelectedIndex = _baseBetSelector.Items.FindIndex(Game.Settings.BaseBet);
			_baseBetSelector.SelectionChanged += BaseBetChanged;
			_kiloCounting = new Label(this)
			{
				Position = new Vector2(200, 430),
				Width = (int)Game.VirtualScreenWidth / 2 - 150,
				Height = 50,
				Text = "Počítání peněz u kila",
				HorizontalAlign = HorizontalAlignment.Center,
				VerticalAlign = VerticalAlignment.Middle
			};
			_kiloCountingSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 300, 430),
				Width = 270,
				Items = new SelectorItems() { { "Sčítat", CalculationStyle.Adding}, { "Násobit", CalculationStyle.Multiplying} }
			};
			_kiloCountingSelector.SelectedIndex = _kiloCountingSelector.Items.FindIndex(Game.Settings.CalculationStyle);
			_kiloCountingSelector.SelectionChanged += KiloCountingChanged;
            //_thinkingTime = new Label(this)
            //{
            //	Position = new Vector2(10, 370),
            //	Width = (int)Game.VirtualScreenWidth / 2 - 20,
            //	Height = 50,
            //	Text = "Jak dlouho AI přemýšlí",
            //	HorizontalAlign = HorizontalAlignment.Center,
            //	VerticalAlign = VerticalAlignment.Middle
            //};
            //_thinkingTimeSelector = new LeftRightSelector(this)
            //{
            //	Position = new Vector2(Game.VirtualScreenWidth - 390, 370),
            //	Width = 360,
            //	Items = new SelectorItems() { { "Krátce", 1000 }, { "Středně", 2000 }, { "Dlouho", 3000 } }
            //};
            //_thinkingTimeSelector.SelectedIndex = _thinkingTimeSelector.Items.FindIndex(Game.Settings.ThinkingTimeMs);
            //_thinkingTimeSelector.SelectionChanged += ThinkingTimeChanged;
            //if (_thinkingTimeSelector.SelectedIndex < 0)
            //{
            //	_thinkingTimeSelector.SelectedIndex = 0;
            //}
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

