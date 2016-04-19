﻿using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
    public class MenuScene : Scene
    {
        private Button _newGameButton;
        private Button _resumeButton;
        public ToggleButton ShuffleBtn;
        private Button _historyBtn;
        private Label _version;
        private Label _author;

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
                    Position = new Vector2(Game.VirtualScreenWidth / 2f - 100, Game.VirtualScreenHeight / 2f - 120),
                    Width = 200,
                    Height = 50,
                    Text = "Nová hra"
                };
            _newGameButton.Click += NewGameClicked;
            _resumeButton = new Button(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth / 2f - 100, Game.VirtualScreenHeight / 2f - 60),
                    Width = 200,
                    Height = 50,
                    Text = "Zpět do hry"
                };
            _resumeButton.Click += ResumeClicked;
            ShuffleBtn = new ToggleButton(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth / 2f - 100, Game.VirtualScreenHeight / 2f),
                    Width = 200,
                    Height = 50,
                    Text = "Zamíchat karty"
                };
            _historyBtn = new Button(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth / 2f - 100, Game.VirtualScreenHeight / 2f + 60),
                    Width = 200,
                    Height = 50,
                    Text = "Historie"
                };
            _historyBtn.Click += HistoryClicked;
            
            Background = Game.Content.Load<Texture2D>("wood2");
            BackgroundTint = Color.DimGray;

            _version = new Label(this)
            {
                    Position = new Vector2(5, Game.VirtualScreenHeight - 34),
                    Width = 200,
                    Height = 34,
                    Text = string.Format("v{0}", MariasekMonoGame.Version)
            };
            _author = new Label(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 405, Game.VirtualScreenHeight - 34),
                Width = 400,
                Height = 34,
                HorizontalAlign = HorizontalAlignment.Right,
                Text = "Copyright ©2016 Tomáš Němec"
            };
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

        private void HistoryClicked(object sender)
        {
            Game.HistoryScene.PopulateControls();
            Game.HistoryScene.SetActive();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            _resumeButton.IsEnabled = Game.MainScene.g != null && Game.MainScene.g.IsRunning;
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}

