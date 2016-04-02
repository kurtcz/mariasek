using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
//using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;


namespace Mariasek.SharedClient.GameComponents
{
    /// <summary>
    /// This is a game component that implements IUpdateable.
    /// </summary>
    public class Scene : GameComponent
    {
        protected SpriteBatch _spriteBatch;

        public new MariasekMonoGame Game { get; private set; }
        //public Dialog ModalDialog { get; set; }
        public GameComponent ExclusiveControl { get; set; }
        //public Texture2D LightShader { get; set; }
        public Texture2D Background { get; set; }

        public Scene(MariasekMonoGame game)
            : base(game)
        {
            // TODO: Construct any child components here
            Game = game;
            Game.Restarted += GameRestarted;
            _spriteBatch = new SpriteBatch(Game.GraphicsDevice);
            Hide();
        }

        public void SetActive()
        {
            if (Game.CurrentScene != null)
            {
                Game.CurrentScene.Hide();
            }
            Game.CurrentScene = this;
            Show();
            //TODO: Add your scene activation related code here
        }

        /// <summary>
        /// Allows the game component to perform any initialization it needs to before starting
        /// to run.  This is where it can query for any required services and load content.
        /// </summary>
        public override void Initialize()
        {
            // TODO: Add your initialization code here

            base.Initialize();
        }

        private void GameRestarted()
        {
            if (Background != null && Background.IsDisposed)
            {
                Background = Game.Content.Load<Texture2D>(Background.Name);
            }
        }

        /// <summary>
        /// Allows the game component to update itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public override void Update(GameTime gameTime)
        {
            // TODO: Add your update code here

            base.Update(gameTime);
        }

        public override void Draw (GameTime gameTime)
        {
            if (Background != null)
            {
                Game.SpriteBatch.Draw(Background, new Rectangle(0, 0, (int)Game.VirtualScreenWidth, (int)Game.VirtualScreenHeight), Color.White);
            }
            base.Draw(gameTime);
        }
    }
}

