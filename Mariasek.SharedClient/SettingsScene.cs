using System;
using System.Xml.Serialization;
using System.IO;

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
    public class SettingsScene : Scene
    {
        private string _settingsFilePath = Path.Combine (
            Environment.GetFolderPath (Environment.SpecialFolder.Personal),
            "Mariasek.settings");

        private Label _sounds;
        private Label _handSorting;
        private ToggleButton _soundBtn;
        private ToggleButton _ascSortBtn;
        private ToggleButton _descSortBtn;
        private ToggleButton _noSortBtn;
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

            LoadGameSettings();
            _sounds = new Label(this)
            {
                Position = new Vector2(10, 60),
                Width = (int)Game.VirtualScreenWidth / 2 - 20,
                Height = 50,
                Text = "Zvuk",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _soundBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 260, 60),
                Width = 120,
                Height = 50,
                Text = _settings.SoundEnabled ? "Zapnutý" : "Vypnutý",
                IsSelected = _settings.SoundEnabled
            };
            _soundBtn.Click += SoundBtnClick;
            _handSorting = new Label(this)
            {
                Position = new Vector2(10, 120),
                Width = (int)Game.VirtualScreenWidth / 2 - 20,
                Height = 50,
                Text = "Řadit karty",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _ascSortBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 390, 120),
                Width = 120,
                Height = 50,
                Text = "Vzestupně",
                IsSelected = _settings.SortMode == SortMode.Ascending
            };
            _ascSortBtn.Click += SortBtnClick;
            _descSortBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 260, 120),
                Width = 120,
                Height = 50,
                Text = "Sestupně",
                IsSelected = _settings.SortMode == SortMode.Descending
            };
            _descSortBtn.Click += SortBtnClick;
            _noSortBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 130, 120),
                Width = 120,
                Height = 50,
                Text = "Vůbec",
                IsSelected = _settings.SortMode == SortMode.None
            };
            _noSortBtn.Click += SortBtnClick;
            _menuBtn = new Button(this)
            {
                Position = new Vector2(10, Game.VirtualScreenHeight - 60),
                Width = 100,
                Height = 50,
                Text = "Menu"
            };
            _menuBtn.Click += MenuBtnClick;

            Background = Game.Content.Load<Texture2D>("wood2");
            BackgroundTint = Color.DimGray;
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

        void SortBtnClick (object sender)
        {
            var sortBtn = sender as ToggleButton;

            _ascSortBtn.IsSelected = false;
            _descSortBtn.IsSelected = false;
            _noSortBtn.IsSelected = false;
            sortBtn.IsSelected = true;

            if (sortBtn == _ascSortBtn)
            {
                _settings.SortMode = SortMode.Ascending;
            }
            else if (sortBtn == _descSortBtn)
            {
                _settings.SortMode = SortMode.Descending;
            }
            else
            {
                _settings.SortMode = SortMode.None;
            }
                
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
    }
}

