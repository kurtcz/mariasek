#region Using Statements
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Input;

using Mariasek.SharedClient.Sprites;
#endregion

namespace Mariasek.AndroidClient
{
	/// <summary>
	/// This is the main type for your game
	/// </summary>
	public class MariasekMonoGame : Game
	{
		GraphicsDeviceManager graphics;
		SpriteBatch spriteBatch;
		Texture2D logoTexture;
		List<Sprite> sprites = new List<Sprite>();

		float VirtualScreenWidth = 800;
		float VirtualScreenHeight = 480;
		Matrix ScaleMatrix;

		Hand hand;

		private Task t = null;
		private SynchronizationContext sync;

		public MariasekMonoGame ()
		{
			graphics = new GraphicsDeviceManager (this);
			Content.RootDirectory = "Content";	            
			graphics.IsFullScreen = true;
			//make sure SupportedOrientations is set accordingly to ActivityAttribute.ScreenOrientation
			graphics.SupportedOrientations = //DisplayOrientation.Portrait |
											 //DisplayOrientation.PortraitDown |
											 DisplayOrientation.LandscapeLeft;// |
											 //DisplayOrientation.LandscapeRight;	
		}

		/// <summary>
		/// Allows the game to perform any initialization it needs to before starting to run.
		/// This is where it can query for any required services and load any non-graphic
		/// related content.  Calling base.Initialize will enumerate through any components
		/// and initialize them as well.
		/// </summary>
		protected override void Initialize ()
		{
			// TODO: Add your initialization logic here
			base.Initialize ();
				
			var scaleX = (float)GraphicsDevice.Viewport.Width / (float)VirtualScreenWidth;
			var scaleY = (float)GraphicsDevice.Viewport.Height / (float)VirtualScreenHeight;
			var _screenScale = new Vector3(scaleX, scaleY, 1.0f);

			ScaleMatrix = Matrix.CreateScale(_screenScale);
			sync = SynchronizationContext.Current;
		}

		/// <summary>
		/// LoadContent will be called once per game and is the place to load
		/// all of your content.
		/// </summary>
		protected override void LoadContent ()
		{
			// Create a new SpriteBatch, which can be used to draw textures.
			spriteBatch = new SpriteBatch (GraphicsDevice);

			//TODO: use this.Content to load your game content here 
			//logoTexture = Content.Load<Texture2D> ("logo");
			sprites = new List<Sprite>
			{
//				new Sprite(Content, "logo"),
//				new Sprite(Content, "logo") { Position = new Vector2(VirtualScreenWidth / 2f, 100) },
//				new Sprite(Content, "logo") { Position = new Vector2(100, VirtualScreenHeight - 100) },
				new Sprite(Content, "revers") { Position = new Vector2(VirtualScreenWidth - 100, VirtualScreenHeight - 100) }
			};

			hand = new Hand(Content, sprites) { Centre = new Vector2(VirtualScreenWidth / 2f, VirtualScreenHeight - 60) };

			//hand.ShowArc((float)Math.PI / 2);
			foreach (var sprite in sprites)
			{
				sprite.Show();
			}
			//hand.ShowStraight((int)VirtualScreenWidth - 20);
			hand.ShowArc((float)Math.PI / 2);
		}

		/// <summary>
		/// Allows the game to run logic such as updating the world,
		/// checking for collisions, gathering input, and playing audio.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		protected override void Update (GameTime gameTime)
		{
			// For Mobile devices, this logic will close the Game when the Back button is pressed
			if (GamePad.GetState (PlayerIndex.One).Buttons.Back == ButtonState.Pressed) {
				Exit ();
			}
			// TODO: Add your update logic here			
			base.Update (gameTime);

			if (!hand.IsMoving)
			{
				if (t == null || t.Status == TaskStatus.RanToCompletion)
				{
					t = Task.Factory.StartNew(() =>
						{
							Thread.Sleep(1000);
							if (hand.IsStraight)
							{
								hand.ShowArc((float)Math.PI / 2);
							}
							else
							{
								hand.ShowStraight((int)VirtualScreenWidth - 20);
							}
						});
				}
			}
			foreach(var sprite in sprites)
			{
				sprite.Update(gameTime);
			}
		}

		/// <summary>
		/// This is called when the game should draw itself.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		protected override void Draw (GameTime gameTime)
		{
			graphics.GraphicsDevice.Clear (Color.CornflowerBlue);
		
			//TODO: Add your drawing code here
			//spriteBatch.Begin ();
			spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, ScaleMatrix);

			// draw the logo
			//spriteBatch.Draw (logoTexture, new Vector2 (40, 40), Color.White);
			//foreach (var sprite in sprites.Where(i => i.RedrawNeeded))
			foreach (var sprite in sprites.Where(i => i.IsVisible))
			{
				sprite.Draw(spriteBatch);
			}

			spriteBatch.End ();

			base.Draw (gameTime);
		}
	}
}

