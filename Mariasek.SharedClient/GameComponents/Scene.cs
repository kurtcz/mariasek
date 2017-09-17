﻿//#define DEBUG_SPRITES
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
    public enum BackgroundAlignment
    {
        Stretch,
        Center
    }

    /// <summary>
    /// This is a game component that implements IUpdateable.
    /// </summary>
    public class Scene : GameComponent
    {
        public new MariasekMonoGame Game { get; private set; }
        public GameComponent ExclusiveControl { get; set; }
        public Texture2D Background { get; set; }
        public BackgroundAlignment BackgroundAlign { get; set; }
        public Color BackgroundTint { get; set; }

		protected bool _initialized;

#if DEBUG_SPRITES
        public int Counter;
#endif
		public Scene(MariasekMonoGame game)
            : base(game)
        {
            // TODO: Construct any child components here
            Game = game;
            BackgroundTint = Color.White;
            Hide();
        }

		public delegate void SceneActivatedEventHandler(object sender);
		public event SceneActivatedEventHandler SceneActivated;
		public virtual void OnSceneActivated()
		{
			if (SceneActivated != null)
			{
				SceneActivated(this);
			}
		}

		public delegate void SceneDeactivatedEventHandler(object sender);
		public event SceneDeactivatedEventHandler SceneDeactivated;
		public virtual void OnSceneDeactivated()
		{
			if (SceneDeactivated != null)
			{
				SceneDeactivated(this);
			}
		}

		public void SetActive()
        {
            if (Game.CurrentScene != null)
            {
                Game.CurrentScene.Hide();
                Game.CurrentScene.OnSceneDeactivated();
            }
            Game.CurrentScene = this;
            Game.CurrentScene.OnSceneActivated();
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
            _initialized = true;
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
#if DEBUG_SPRITES
            Counter = 0;
#endif
			if (Background != null && Game.CurrentRenderingGroup == AnchorType.Main)
            {
                switch(BackgroundAlign)
                {
                    case BackgroundAlignment.Stretch:
                        Game.SpriteBatch.Draw(Background, new Rectangle(
                                (int)(0 - Game.MainScaleMatrix.M41), 
                                (int)(0 - Game.MainScaleMatrix.M42), 
                                (int)(Game.VirtualScreenWidth + 2 * Game.MainScaleMatrix.M41), 
                                (int)(Game.VirtualScreenHeight + 2 * Game.MainScaleMatrix.M42)), BackgroundTint);
                        break;
                    case BackgroundAlignment.Center:
                    default:
                        Game.SpriteBatch.Draw(Background, new Rectangle(0, 0, (int)Game.VirtualScreenWidth, (int)Game.VirtualScreenHeight), BackgroundTint);
                        break;
                }
            }
			base.Draw(gameTime);
        }
	}
}

