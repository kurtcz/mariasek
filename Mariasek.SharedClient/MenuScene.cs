using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using Mariasek.SharedClient.GameComponents;
using System;
using System.Collections.Generic;

namespace Mariasek.SharedClient
{
    public class MenuScene : Scene
    {
        private Button _newGameButton;
        private Button _resumeButton;
        private Button _settingsButton;
        public ToggleButton ShuffleBtn;
        private Button _historyBtn;
        private Button _editorBtn;
        private Button _leftBtn;
        private Button _rightBtn;
        private Label _version;
        private Label _author;
        private Sprite[] _cards;
        private SpriteButton _logo;
        private SpriteButton _rateApp;
        private Vector2 _originalLogoScale;
        private Vector2 _originalRatingScale;
        public TextBox _warning;
        private Button _permissionsButton;

        private int _currentButtonIndex;

        public MenuScene(MariasekMonoGame game)
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

            _currentButtonIndex = 0;
            _newGameButton = new Button(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 100, Game.VirtualScreenHeight / 2f - 140),
                Width = 200,
                Height = 50,
                Text = "Nová hra"
            };
            _newGameButton.Click += NewGameClicked;
            _resumeButton = new Button(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 100, Game.VirtualScreenHeight / 2f - 80),
                Width = 200,
                Height = 50,
                Text = "Zpět do hry"
            };
            _resumeButton.Click += ResumeClicked;
            _settingsButton = new Button(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 100, Game.VirtualScreenHeight / 2f - 20),
                Width = 200,
                Height = 50,
                Text = "Nastavení"
            };
            _settingsButton.Click += SettingsClicked;
            ShuffleBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 100, Game.VirtualScreenHeight / 2f + 40),
                Width = 200,
                Height = 50,
                Text = "Zamíchat karty"
            };
            ShuffleBtn.Click += ShuffleBtnClicked;
            _historyBtn = new Button(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 100, Game.VirtualScreenHeight / 2f + 100),
                Width = 200,
                Height = 50,
                Text = "Historie"
            };
            _historyBtn.Click += HistoryClicked;
            _editorBtn = new Button(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 100, Game.VirtualScreenHeight / 2f + 100),
                Width = 200,
                Height = 50,
                Text = "Editor her"
            };
            _editorBtn.Click += EditorClicked;
            _editorBtn.Hide();
            _leftBtn = new Button(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 140, Game.VirtualScreenHeight / 2f + 95),
                Width = 40,
                Height = 50,
                Text = "«",
                BackgroundColor = Color.Transparent,
                BorderColor = Color.Transparent,
                TextRenderer = Game.FontRenderers["SegoeUI40Outl"]
            };
            _leftBtn.Click += LeftRightBtnClick;
            _rightBtn = new Button(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 100, Game.VirtualScreenHeight / 2f + 95),
                Width = 40,
                Height = 50,
                Text = "»",
                BackgroundColor = Color.Transparent,
                BorderColor = Color.Transparent,
                TextRenderer = Game.FontRenderers["SegoeUI40Outl"]
            };
            _rightBtn.Click += LeftRightBtnClick;
            _warning = new TextBox(this)
            {
                Position = new Vector2(0, Game.VirtualScreenHeight / 2f + 160),
                Width = (int)Game.VirtualScreenWidth,
                Height = 80,
                Text = "!UPOZORNĚNÍ! Mariášek potřebuje přístup k souborům kvůli ukládání\nhistorie a nastavení. Bez něho nebude hra správně fungovat!",
                FontScaleFactor = 0.85f,
                BorderColor = Game.Settings.LossColor,
                BackgroundColor = new Color(0x20, 0x20, 0x20),
                TextColor = Game.Settings.HighlightedTextColor,
                Opacity = 0.8f,
                VerticalAlign = VerticalAlignment.Middle,
                HorizontalAlign = HorizontalAlignment.Center,
                ZIndex = 100
            };
            _warning.Hide();
            _permissionsButton = new Button(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 200, Game.VirtualScreenHeight / 2f + 100),
                Width = 200,
                Height = 50,
                Text = "Povolit přístup",
                BorderColor = Game.Settings.HintColor
            };
            _permissionsButton.Click += PermissionsButtonClicked;
            _permissionsButton.Hide();
            _cards = new Sprite[3];
            var backSideRect = Game.Settings.CardBackSide.ToTextureRect();
            for (var i = 0; i < _cards.Length; i++)
            {
                _cards[i] = new Sprite(this, Game.ReverseTexture, backSideRect)
                {
                    Scale = Game.CardScaleFactor,
                    Name = string.Format("card{0}", i)
                };
                _cards[i].Hide();
            }
            Background = Game.Assets.GetTexture("wood2");
            BackgroundTint = Color.DimGray;

            _logo = new SpriteButton(this, 
                                     new Sprite(this, Game.LogoTexture) 
                                     {
                                        Name = "LogoSprite", 
                                        Scale = new Vector2(0.7f, 0.7f), 
                                        Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Top 
                                     })
            {
                //Position = new Vector2(70, 75),
                Position = new Vector2(65, 58),
                Name = "LogoButton",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Top
            };
            _logo.Click += LogoClicked;
            _originalLogoScale = _logo.Sprite.Scale;
            AnimateLogo();
            _rateApp = new SpriteButton(this, 
                                        new Sprite(this, Game.RatingTexture) 
                                        {
                                            Name = "RateSprite", 
                                            Scale = new Vector2(0.7f, 0.7f), 
                                            Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Right : AnchorType.Top
                                        })
            {
                Position = new Vector2(Game.VirtualScreenWidth - 65, 58),
                Name = "RateApp",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Right : AnchorType.Top
            };
            _rateApp.Click += RatingClicked;
            _originalRatingScale = _rateApp.Sprite.Scale;
            AnimateRating();

            _version = new Label(this)
            {
                Position = new Vector2(15, Game.VirtualScreenHeight - 30),
                Width = 200,
                Height = 30,
                Text = string.Format("v{0}", MariasekMonoGame.Version),
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Bottom,
                FontScaleFactor = Game.RealScreenGeometry == ScreenGeometry.Wide ? 0.9f : 0.75f
            };
            _author = new Label(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 415, Game.VirtualScreenHeight - 30),
                Width = 400,
                Height = 30,
                HorizontalAlign = HorizontalAlignment.Right,
                Text = "©2020 Tomáš Němec",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Right : AnchorType.Bottom,
                FontScaleFactor = Game.RealScreenGeometry == ScreenGeometry.Wide ? 0.9f : 0.75f
            };
            ShowWarningIfNeeded();
            try
            {
                SoundEffect.MasterVolume = Game.Settings.SoundEnabled ? 1f : 0f;
                if (Game.AmbientSound != null && !Game.AmbientSound.IsDisposed)
                {
                    Game.AmbientSound.Volume = Game.Settings.BgSoundEnabled ? 0.2f : 0f;
                    //Microsoft.Xna.Framework.Media.MediaPlayer.Volume = Game.Settings.BgSoundEnabled ? 0.1f : 0f;
                    //Microsoft.Xna.Framework.Media.MediaPlayer.IsRepeating = true;
                    //Microsoft.Xna.Framework.Media.MediaPlayer.Play(Game.NaPankraciSong);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{ex.Message}\n{ex.StackTrace}");
            }
        }

        private void LeftRightBtnClick(object sender)
        {
            _currentButtonIndex = (_currentButtonIndex + 1) % 2;

            switch (_currentButtonIndex)
            {
                case 0:
                    _editorBtn.Hide();
                    _historyBtn.Show();
                    break;
                case 1:
                    _historyBtn.Hide();
                    _editorBtn.Show();
                    break;
            }
        }

        void ShowWarningIfNeeded()
        {
            if (Game.StorageAccessor.CheckStorageAccess())
            {
                _warning.Hide();
                _permissionsButton.Hide();
            }
            else
            {
                _warning.Show();
                _permissionsButton.Show();
            }
            _settingsButton.ClearOperations();
            _settingsButton.Wait(3000)
                           .Invoke(() => ShowWarningIfNeeded());
        }

        void PermissionsButtonClicked(object sender)
        {
            Game.StorageAccessor.GetStorageAccess();
        }

        void LogoClicked(object sender)
        {
            if (Game.Navigator != null)
            {
                var url = "http://www.hracikarty.cz/";
                try
                {
                    Game.Navigator.Navigate(url);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot navigate to url {url}\n{ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        void RatingClicked(object sender)
        {
            if (Game.Navigator != null)
            {
#if __IOS__
                //Game.Navigator.Navigate("itms-apps://itunes.apple.com/app/id1326617658?action=write-review");
                Game.Navigator.Navigate("itms-apps://itunes.apple.com/app/id1326617658");
#elif __ANDROID__
                try
                {
                    Game.Navigator.Navigate("market://details?id=com.tnemec.mariasek.android");
                }
                catch (Exception ex0)
                {
                    var url = "https://play.google.com/store/apps/details?id=com.tnemec.mariasek.android";
                    try
                    {
                        Game.Navigator.Navigate(url);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Cannot navigate to url {url}\n{ex.Message}\n{ex.StackTrace}");
                    }
                }
#endif
            }
        }

        void AnimateLogo()
        {
			var normal = _originalLogoScale;
            var slim = new Vector2(0, _originalLogoScale.Y);
            const float flipSpeed = 1f;

            _logo.Wait(3000)
                 .Invoke(() =>
                 {
                     _logo.Sprite.ScaleTo(slim, flipSpeed);
                 })
                 .WaitUntil(() => !_logo.Sprite.IsBusy)
                 .Invoke(() =>
                 {
                     _logo.Sprite.Flip = SpriteEffects.FlipHorizontally;
                     _logo.Sprite.ScaleTo(normal, flipSpeed);
                 })
                 .WaitUntil(() => !_logo.Sprite.IsBusy)
                 .Invoke(() =>
                 {
                     _logo.Sprite.ScaleTo(slim, flipSpeed);
                 })
                 .WaitUntil(() => !_logo.Sprite.IsBusy)
                 .Invoke(() =>
                 {
                     _logo.Sprite.Flip = SpriteEffects.None;
                     _logo.Sprite.ScaleTo(normal, flipSpeed);
                 })
                 .WaitUntil(() => !_logo.Sprite.IsBusy)
                 .Wait(7000)
                 .Invoke(() => AnimateLogo());
        }

        void AnimateRating()
        {
            var normal = _originalRatingScale;
            var slim = new Vector2(0, _originalRatingScale.Y);
            const float flipSpeed = 1f;

            _rateApp.Wait(2000)
                 .Invoke(() =>
                 {
                     _rateApp.Sprite.ScaleTo(slim, flipSpeed);
                 })
                 .WaitUntil(() => !_rateApp.Sprite.IsBusy)
                 .Invoke(() =>
                 {
                     _rateApp.Sprite.Flip = SpriteEffects.FlipHorizontally;
                     _rateApp.Sprite.ScaleTo(normal, flipSpeed);
                 })
                 .WaitUntil(() => !_rateApp.Sprite.IsBusy)
                 .Invoke(() =>
                 {
                     _rateApp.Sprite.ScaleTo(slim, flipSpeed);
                 })
                 .WaitUntil(() => !_rateApp.Sprite.IsBusy)
                 .Invoke(() =>
                 {
                     _rateApp.Sprite.Flip = SpriteEffects.None;
                     _rateApp.Sprite.ScaleTo(normal, flipSpeed);
                 })
                 .WaitUntil(() => !_rateApp.Sprite.IsBusy)
                 .Wait(7000)
                 .Invoke(() => AnimateRating());
        }

        void ShuffleBtnClicked(object sender)
        {
            var position1 = new Vector2(Game.VirtualScreenWidth * 3 / 4f, Game.VirtualScreenHeight / 2f);
            var position2 = new Vector2(Game.VirtualScreenWidth * 3 / 4f + 150, Game.VirtualScreenHeight / 2f);
            var positionX = new Vector2(Game.VirtualScreenWidth * 3 / 4f, Game.VirtualScreenHeight / 2f - 100);

            if (!this.ScheduledOperations.IsEmpty)
            {                
                return;
            }
            _cards[0].Scale = Game.CardScaleFactor;
            _cards[1].Scale = Game.CardScaleFactor;
            _cards[2].Scale = Game.CardScaleFactor;
            _cards[0].ZIndex = 50;
            _cards[1].ZIndex = 51;
            _cards[2].ZIndex = 52;
            _cards[0].Position = position1;
            _cards[1].Position = position1;
            _cards[2].Position = position2;
            _cards[0].Texture = Game.ReverseTexture;
            _cards[1].Texture = Game.ReverseTexture;
            _cards[2].Texture = Game.ReverseTexture;
            var backSideRect = Game.Settings.CardBackSide.ToTextureRect();
            _cards[0].SpriteRectangle = backSideRect;
            _cards[1].SpriteRectangle = backSideRect;
            _cards[2].SpriteRectangle = backSideRect;
            _cards[0].Show();
            _cards[1].Hide();
            _cards[2].Hide();
            _cards[0].FadeIn(2);
            this.WaitUntil(() => _cards[0].Opacity == 1)
                .Invoke(() => _cards[1].Show());
            for (var i = 0; i < 5; i++)
            {
                this.Invoke(() => _cards[0].MoveTo(position2, 800))
                    .WaitUntil(() => _cards[0].Position == position2)
                    .Invoke(() =>
                     {
                         _cards[0].Hide();
                         _cards[2].Position = position2;
                         _cards[2].Show();
                     })
                    .Invoke(() => _cards[2].MoveTo(position1, 800))
                    .WaitUntil(() => _cards[2].Position == position1)
                    .Invoke(() =>
                     {
                         _cards[0].Position = position1;
                         _cards[0].Show();
                         _cards[2].Hide();
                         _cards[2].Position = position2;
                     });
            }
            this.Invoke(() =>
                 {
                     _cards[0].FadeOut(2);
                     _cards[1].Hide();
                     ShuffleBtn.IsSelected = false;
                 })
                .WaitUntil(() => _cards[0].Opacity == 0)
                .Invoke(() => _cards[0].Hide());
            Game.MainScene.ShuffleDeck();
        }

        private void NewGameClicked(object sender)
        {            
			Game.MainScene.SetActive();
			Game.MainScene.NewGameBtnClicked(sender);
        }

        private void ResumeClicked(object sender)
        {
            Game.MainScene.SetActive();
        }
            
        private void SettingsClicked(object sender)
        {
            Game.SettingsScene.SetActive();
        }

        private void HistoryClicked(object sender)
        {
            Game.HistoryScene.PopulateControls();
            Game.HistoryScene.SetActive();
        }

        private void EditorClicked(object sender)
        {
            Game.EditorScene.SetActive();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            _resumeButton.IsEnabled = Game.MainScene.g != null;// && Game.MainScene.g.IsRunning;
        }
	}
}

