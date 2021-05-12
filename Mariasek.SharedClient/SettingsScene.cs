using System;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

using Mariasek.SharedClient.GameComponents;
using Mariasek.Engine;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace Mariasek.SharedClient
{
    public class SettingsScene : Scene
    {
        public static string _settingsFilePath = Path.Combine(MariasekMonoGame.RootPath, "Mariasek.settings");

        private Button _sideNavigation1;
        private Button _sideNavigation2;
        private Button _sideNavigation3;
        private Button _sideNavigation4;
        private Button[] _sideNavigations;

        private RectangleShape _hline;
        private Label _performance;
        private Button _menuBtn;
        private Button _aiBtn;
        private LeftRightSelector _pageSelector;
        private SettingsBox _settingsBox;

        private int currentPage;

        public SettingsScene(MariasekMonoGame game)
            : base(game)
        {
            Game.SettingsChanged += SettingsChanged;
            SceneActivated += Activated;
        }

        private void SettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            try
            {                
                SoundEffect.MasterVolume = Game.Settings.SoundEnabled ? 1f : 0f;
                if (Game.AmbientSound != null && !Game.AmbientSound.IsDisposed)
                {
                    Game.AmbientSound.Volume = Game.Settings.BgSoundEnabled ? 0.2f : 0f;
                }
                Microsoft.Xna.Framework.Media.MediaPlayer.Volume = Game.Settings.BgSoundEnabled ? 0.1f : 0f;
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{ex.Message}\n{ex.StackTrace}");
            }
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
            Game.LoadGameSettings(false);

            _settingsBox = new SettingsBox(this)
            {
                Position = new Vector2(0, 10),
                Width = (int)Game.VirtualScreenWidth,
                Height = (int)Game.VirtualScreenHeight - 20
            };

            _sideNavigation1 = new Button(this)
            {
                Position = new Vector2(10, 10),
                Width = 200,
                Text = "Obecná nast.",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Top
            };
            _sideNavigation1.Click += SideNavigationClick;

            _sideNavigation2 = new Button(this)
            {
                Position = new Vector2(10, 70),
                Width = 200,
                Text = "Nast. pravidel",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Top
            };
            _sideNavigation2.Click += SideNavigationClick;

            _sideNavigation3 = new Button(this)
            {
                Position = new Vector2(10, 130),
                Width = 200,
                Text = "Vzhled a chování",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Top
            };
            _sideNavigation3.Click += SideNavigationClick;

            _sideNavigation4 = new Button(this)
            {
                Position = new Vector2(10, 190),
                Width = 200,
                Text = "Sazby her",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Top
            };
            _sideNavigation4.Click += SideNavigationClick;
            _sideNavigations = new[] { _sideNavigation1, _sideNavigation2, _sideNavigation3, _sideNavigation4 };

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

            Background = Game.Assets.GetTexture("wood2");
            BackgroundTint = Color.DimGray;
            Game.OnSettingsChanged();
        }

        private void SideNavigationClick(object sender)
        {
            var index = Array.IndexOf(_sideNavigations, sender);

            if (index >=0 && index < _sideNavigations.Length)
            {
                _settingsBox.ScrollTo(-_settingsBox.TopicOffsets[index]);
            }
        }

        void MenuBtnClick(object sender)
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

