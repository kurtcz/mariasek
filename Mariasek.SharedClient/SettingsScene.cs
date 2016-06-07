using System;
using System.Xml.Serialization;
using System.IO;

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
    public class SettingsScene : Scene
    {
        private string _settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mariasek.settings");

        private Label _hint;
        private Label _sounds;
        private Label _handSorting;
        private Label _baseBet;
        private Label _kiloCounting;
        private Label _thinkingTime;
        private ToggleButton _hintBtn;
        private ToggleButton _soundBtn;
        private ToggleButton _ascSortBtn;
        private ToggleButton _descSortBtn;
        private ToggleButton _noSortBtn;
        private ToggleButton _bet001Btn;
        private ToggleButton _bet005Btn;
        private ToggleButton _bet010Btn;
        private ToggleButton _bet020Btn;
        private ToggleButton _bet050Btn;
        private ToggleButton _bet100Btn;
        private ToggleButton _addingBtn;
        private ToggleButton _multiplyingBtn;
        private ToggleButton _thinkingTime1;
        private ToggleButton _thinkingTime2;
        private ToggleButton _thinkingTime3;
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
                    HorizontalAlign = HorizontalAlignment.Right
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
                Position = new Vector2(Game.VirtualScreenWidth - 260, 10),
                Width = 120,
                Height = 50,
                Text = _settings.SoundEnabled ? "Zapnutý" : "Vypnutý",
                IsSelected = _settings.SoundEnabled
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
                    Position = new Vector2(Game.VirtualScreenWidth - 260, 70),
                    Width = 120,
                    Height = 50,
                    Text = _settings.HintEnabled ? "Zapnutá" : "Vypnutá",
                    IsSelected = _settings.HintEnabled
                };
            _hintBtn.Click += HintBtnClick;
            _handSorting = new Label(this)
            {
                Position = new Vector2(10, 130),
                Width = (int)Game.VirtualScreenWidth / 2 - 20,
                Height = 50,
                Text = "Řadit karty",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _ascSortBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 390, 130),
                Width = 120,
                Height = 50,
                Text = "Vzestupně",
                IsSelected = _settings.SortMode == SortMode.Ascending
            };
            _ascSortBtn.Click += SortBtnClick;
            _descSortBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 260, 130),
                Width = 120,
                Height = 50,
                Text = "Sestupně",
                IsSelected = _settings.SortMode == SortMode.Descending
            };
            _descSortBtn.Click += SortBtnClick;
            _noSortBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 130, 130),
                Width = 120,
                Height = 50,
                Text = "Vůbec",
                IsSelected = _settings.SortMode == SortMode.None
            };
            _baseBet = new Label(this)
            {
                Position = new Vector2(10, 190),
                Width = (int)Game.VirtualScreenWidth / 2 - 20,
                Height = 50,
                Text = "Hodnota hry",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle
            };
            _bet001Btn = new ToggleButton(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth - 390, 190),
                    Width = 120,
                    Height = 50,
                    Text = "0,10 Kč",
                    IsSelected = _settings.BaseBet == 0.1f
                };
            _bet001Btn.Click += BetBtnClick;
            _bet005Btn = new ToggleButton(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth - 260, 190),
                    Width = 120,
                    Height = 50,
                    Text = "0,50 Kč",
                    IsSelected = _settings.BaseBet == 0.5f
                };
            _bet005Btn.Click += BetBtnClick;
            _bet010Btn = new ToggleButton(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth - 130, 190),
                    Width = 120,
                    Height = 50,
                    Text = "1 Kč",
                    IsSelected = _settings.BaseBet == 1f
                };
            _bet010Btn.Click += BetBtnClick;
            _bet020Btn = new ToggleButton(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth - 390, 250),
                    Width = 120,
                    Height = 50,
                    Text = "2 Kč",
                    IsSelected = _settings.BaseBet == 2f
                };
            _bet020Btn.Click += BetBtnClick;
            _bet050Btn = new ToggleButton(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth - 260, 250),
                    Width = 120,
                    Height = 50,
                    Text = "5 Kč",
                    IsSelected = _settings.BaseBet == 5f
                };
            _bet050Btn.Click += BetBtnClick;
            _bet100Btn = new ToggleButton(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth - 130, 250),
                    Width = 120,
                    Height = 50,
                    Text = "10 Kč",
                    IsSelected = _settings.BaseBet == 10f
                };
            _bet100Btn.Click += BetBtnClick;
            _kiloCounting = new Label(this)
                {
                    Position = new Vector2(10, 310),
                    Width = (int)Game.VirtualScreenWidth / 2 - 20,
                    Height = 50,
                    Text = "Počítání peněz u kila",
                    HorizontalAlign = HorizontalAlignment.Center,
                    VerticalAlign = VerticalAlignment.Middle
                };
            _addingBtn = new ToggleButton(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth - 325, 310),
                    Width = 120,
                    Height = 50,
                    Text = "Sčítat",
                    IsSelected = _settings.CalculationStyle == Mariasek.Engine.New.CalculationStyle.Adding
                };
            _addingBtn.Click += CalculationBtnClick;
            _multiplyingBtn = new ToggleButton(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth - 195, 310),
                    Width = 120,
                    Height = 50,
                    Text = "Násobit",
                    IsSelected = _settings.CalculationStyle == Mariasek.Engine.New.CalculationStyle.Multiplying
                };
            _multiplyingBtn.Click += CalculationBtnClick;
            _thinkingTime = new Label(this)
                {
                    Position = new Vector2(10, 370),
                    Width = (int)Game.VirtualScreenWidth / 2 - 20,
                    Height = 50,
                    Text = "Jak dlouho AI přemýšlí",
                    HorizontalAlign = HorizontalAlignment.Center,
                    VerticalAlign = VerticalAlignment.Middle
                };
            _thinkingTime1 = new ToggleButton(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth - 390, 370),
                    Width = 120,
                    Height = 50,
                    Text = "Krátce",
                    IsSelected = _settings.ThinkingTimeMs == 3000
                };
            _thinkingTime1.Click += ThinkingTimeBtnClick;
            _thinkingTime2 = new ToggleButton(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth - 260, 370),
                    Width = 120,
                    Height = 50,
                    Text = "Středně",
                    IsSelected = _settings.ThinkingTimeMs == 4000
                };
            _thinkingTime2.Click += ThinkingTimeBtnClick;
            _thinkingTime3 = new ToggleButton(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth - 130, 370),
                    Width = 120,
                    Height = 50,
                    Text = "Dlouho",
                    IsSelected = _settings.ThinkingTimeMs == 5000
                };
            _thinkingTime3.Click += ThinkingTimeBtnClick;

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

        void BetBtnClick (object sender)
        {
            var betbtn = sender as ToggleButton;

            _bet001Btn.IsSelected = false;
            _bet005Btn.IsSelected = false;
            _bet010Btn.IsSelected = false;
            _bet020Btn.IsSelected = false;
            _bet050Btn.IsSelected = false;
            _bet100Btn.IsSelected = false;
            betbtn.IsSelected = true;

            if (betbtn == _bet001Btn)
            {
                _settings.BaseBet = 0.1f;
            }
            else if (betbtn == _bet005Btn)
            {
                _settings.BaseBet = 0.5f;
            }
            else if (betbtn == _bet010Btn)
            {
                _settings.BaseBet = 1f;
            }
            else if (betbtn == _bet020Btn)
            {
                _settings.BaseBet = 2f;
            }
            else if (betbtn == _bet050Btn)
            {
                _settings.BaseBet = 5f;
            }
            else if (betbtn == _bet100Btn)
            {
                _settings.BaseBet = 10f;
            }

            SaveGameSettings();
            OnSettingsChanged();
        }

        void ThinkingTimeBtnClick (object sender)
        {
            var btn = sender as ToggleButton;

            _thinkingTime1.IsSelected = false;
            _thinkingTime2.IsSelected = false;
            _thinkingTime3.IsSelected = false;
            btn.IsSelected = true;

            if (btn == _thinkingTime1)
            {
                _settings.ThinkingTimeMs = 3000;
            }
            else if (btn == _thinkingTime2)
            {
                _settings.ThinkingTimeMs = 4000;
            }
            else if (btn == _thinkingTime3)
            {
                _settings.ThinkingTimeMs = 5000;
            }

            SaveGameSettings();
            OnSettingsChanged();
        }

        void CalculationBtnClick (object sender)
        {
            var btn = sender as ToggleButton;

            _addingBtn.IsSelected = false;
            _multiplyingBtn.IsSelected = false;
            btn.IsSelected = true;

            if (btn == _addingBtn)
            {
                _settings.CalculationStyle = Mariasek.Engine.New.CalculationStyle.Adding;
            }
            else if (btn == _multiplyingBtn)
            {
                _settings.CalculationStyle = Mariasek.Engine.New.CalculationStyle.Multiplying;
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

