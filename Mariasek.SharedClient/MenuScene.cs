using System;
using System.IO;
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

using Mariasek.SharedClient.GameComponents;

namespace Mariasek.SharedClient
{
    public class MenuScene : Scene
    {
        private Button _newGameButton;
        private Button _resumeButton;
        private Button _settingsButton;
        public ToggleButton ShuffleBtn;
        private Button _historyBtn;
        private Label _version;
        private Label _author;
        private Sprite[] _cards;

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
            _cards = new Sprite[3];
            for (var i = 0; i < _cards.Length; i++)
            {
                _cards[i] = new Sprite(this, Game.ReverseTexture)
                { 
                    Scale = Game.CardScaleFactor,
                    Name = string.Format("card{0}", i)
                };
                _cards[i].Hide();
            }
            Background = Game.Content.Load<Texture2D>("wood2");
            BackgroundTint = Color.DimGray;

            _version = new Label(this)
            {
                Position = new Vector2(5, Game.VirtualScreenHeight - 34),
                Width = 200,
                Height = 34,
                Text = string.Format("v{0}", MariasekMonoGame.Version),
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Bottom
            };
            _author = new Label(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 405, Game.VirtualScreenHeight - 34),
                Width = 400,
                Height = 34,
                HorizontalAlign = HorizontalAlignment.Right,
                Text = "©2017 Tomáš Němec",
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Right : AnchorType.Bottom
            };
        }

        void ShuffleBtnClicked(object sender)
        {
            var position1 = new Vector2(Game.VirtualScreenWidth * 3 / 4f, Game.VirtualScreenHeight / 2f);
            var position2 = new Vector2(Game.VirtualScreenWidth * 3 / 4f + 150, Game.VirtualScreenHeight / 2f);
            var positionX = new Vector2(Game.VirtualScreenWidth * 3 / 4f, Game.VirtualScreenHeight / 2f - 100);

            if (this.ScheduledOperations.Count() > 0)
            {                
                return;
            }
            _cards[0].ZIndex = 50;
            _cards[1].ZIndex = 51;
            _cards[2].ZIndex = 52;
            _cards[0].Position = position1;
            _cards[1].Position = position1;
            _cards[2].Position = position2;
            _cards[0].Texture = Game.ReverseTexture;
            _cards[1].Texture = Game.ReverseTexture;
            _cards[2].Texture = Game.ReverseTexture;
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

		private void GenerateClicked(object sender)
		{
			//Game.GenerateScene.SetActive();
		}

		public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            _resumeButton.IsEnabled = Game.MainScene.g != null && Game.MainScene.g.IsRunning;
        }
    }
}

