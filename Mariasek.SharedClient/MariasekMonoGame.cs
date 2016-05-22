﻿#region Using Statements
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Mariasek.SharedClient.GameComponents;
#endregion

namespace Mariasek.SharedClient
{
	/// <summary>
	/// This is the main type for your game
	/// </summary>
	public class MariasekMonoGame : Game
	{
		//Texture2D logoTexture;

        public static string ConfigPath
        {
            get
            {
                #if __IOS__
                var personalPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

                return Path.Combine(personalPath, "..", "Library");
                #else
                return Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                #endif
            }
        }
        public GraphicsDeviceManager Graphics { get; private set; }
        public TouchCollection TouchCollection { get; private set; }
        public SpriteBatch SpriteBatch { get; private set; }
        public Scene CurrentScene { get; set; }
        public TestScene TestScene { get; private set; }
        public MainScene MainScene { get; private set; }
        public MenuScene MenuScene { get; private set; }
        public SettingsScene SettingsScene { get; private set; }
        public HistoryScene HistoryScene { get; private set; }

        public Texture2D CardTextures { get; private set; }
        public Texture2D ReverseTexture { get; private set; }
        public Dictionary<string, FontRenderer> FontRenderers { get; private set; }

		public readonly float VirtualScreenWidth = 800;
        public readonly float VirtualScreenHeight = 480;
		public Matrix ScaleMatrix;

        public SoundEffect ClickSound { get; private set; }
        public SoundEffect OnSound { get; private set; }
        public SoundEffect OffSound { get; private set; }

        public readonly Vector2 CardScaleFactor;

        public List<Mariasek.Engine.New.MoneyCalculatorBase> Money = new List<Mariasek.Engine.New.MoneyCalculatorBase>();
		public MariasekMonoGame ()
		{
            System.Diagnostics.Debug.WriteLine("MariasekMonoGame()");
			Graphics = new GraphicsDeviceManager (this);
			Content.RootDirectory = "Content";	            
			Graphics.IsFullScreen = true;
			//make sure SupportedOrientations is set accordingly to ActivityAttribute.ScreenOrientation
			Graphics.SupportedOrientations = //DisplayOrientation.Portrait |
											 //DisplayOrientation.PortraitDown |
											 DisplayOrientation.LandscapeRight |
											 DisplayOrientation.LandscapeLeft;	
            Graphics.ApplyChanges();
//            var pt = new Vector2(4, 2);
//            var c = new Vector2(3, 3);
//            var rt = pt.Rotate(c, (float)Math.PI / 2);
//            System.Diagnostics.Debug.WriteLine(rt);
            CardScaleFactor = new Vector2(1.2f, 1.2f);
		}

        public static string Version
        {
            get
            {
                var assembly = typeof(MariasekMonoGame).Assembly;

                return assembly.GetName().Version.ToString();
            }
        }

		/// <summary>
		/// Allows the game to perform any initialization it needs to before starting to run.
		/// This is where it can query for any required services and load any non-graphic
		/// related content.  Calling base.Initialize will enumerate through any components
		/// and initialize them as well.
		/// </summary>
		protected override void Initialize ()
		{
            System.Diagnostics.Debug.WriteLine("Initialize()");
			// TODO: Add your initialization logic here
			base.Initialize ();

            var width = Math.Max(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            var height = Math.Min(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            var scaleX = (float)width / (float)VirtualScreenWidth;
			var scaleY = (float)height / (float)VirtualScreenHeight;
            var translation = Vector3.Zero;

            if ((float)width / (float)height < (float)VirtualScreenWidth / (float)VirtualScreenHeight)
            {
                //skutecna obrazovka ma pomery sran mene sirokouhle nez virtualni
                //vertikalni pomer upravime podle horizontalniho, obraz vertikalne posuneme na stred (vzniknou okraje nahore a dole)
                translation = new Vector3(0, (height - VirtualScreenHeight * scaleX) / 2f, 0);
                scaleY = scaleX;
            }
            else if ((float)width / (float)height > (float)VirtualScreenWidth / (float)VirtualScreenHeight)
            {
                //skutecna obrazovka ma pomery sran vice sirokouhle nez virtualni
                //horizontalni pomer upravime podle vertikalniho, obraz horizontalne posuneme na stred (vzniknou okraje vlevo a vpravo)
                translation = new Vector3((width - VirtualScreenWidth * scaleY) / 2f, 0, 0);
                scaleX = scaleY;
            }
			var _screenScale = new Vector3(scaleX, scaleY, 1.0f);

            ScaleMatrix = Matrix.CreateScale(_screenScale) * Matrix.CreateTranslation(translation);
		}

//        public void ReloadContent()
//        {
//            // Create a new SpriteBatch, which can be used to draw textures.
//            SpriteBatch = new SpriteBatch (GraphicsDevice);
//
//            var cardTextures = Content.Load<Texture2D>("marias");
//            var reverseTexture = Content.Load<Texture2D>("revers");
//            FontRenderers = new Dictionary<string, FontRenderer>
//            {
//                { "BMFont", FontRenderer.GetFontRenderer(this, "BMFont.fnt", "BMFont_0.png", "BMFont_1.png") },
//                { "BM2Font", FontRenderer.GetFontRenderer(this, "BM2Font.fnt", "BM2Font_0.png", "BM2Font_1.png") },
//                { "SegoeUI40Outl", FontRenderer.GetFontRenderer(this, "SegoeUI40Outl.fnt", "SegoeUI40Outl_0.png", "SegoeUI40Outl_1.png", "SegoeUI40Outl_2.png") }
//            };
//
//            foreach (var kvp in FontRenderers)
//            {
//                Restarted += kvp.Value.GameRestarted;
//            }
//
//            Color[] data;
//            data = new Color[cardTextures.Width * cardTextures.Height];
//            cardTextures.GetData<Color>(data);
//            CardTextures.SetData<Color>(data);
//
//            data = new Color[reverseTexture.Width * reverseTexture.Height];
//            reverseTexture.GetData<Color>(data);
//            ReverseTexture.SetData<Color>(data);
//            //MainScene.Initialize();
//            //MainScene.SetActive();
//        }

		/// <summary>
		/// LoadContent will be called once per game and is the place to load
		/// all of your content.
		/// </summary>
		protected override void LoadContent ()
		{
            System.Diagnostics.Debug.WriteLine("LoadContent()");
            // Create a new SpriteBatch, which can be used to draw textures.
            SpriteBatch = new SpriteBatch (GraphicsDevice);

            CardTextures = Content.Load<Texture2D>("marias");
            ReverseTexture = Content.Load<Texture2D>("revers");
            FontRenderers = new Dictionary<string, FontRenderer>
            {
                { "BMFont", FontRenderer.GetFontRenderer(this, "BMFont.fnt", "BMFont_0.png", "BMFont_1.png") },
                { "BM2Font", FontRenderer.GetFontRenderer(this, "BM2Font.fnt", "BM2Font_0.png", "BM2Font_1.png") },
                { "SegoeUI40Outl", FontRenderer.GetFontRenderer(this, "SegoeUI40Outl.fnt", "SegoeUI40Outl_0.png", "SegoeUI40Outl_1.png", "SegoeUI40Outl_2.png") },
                { "LuckiestGuy32Outl", FontRenderer.GetFontRenderer(this, "LuckiestGuy32Outl.fnt", "LuckiestGuy32Outl_0.png", "LuckiestGuy32Outl_1.png", "LuckiestGuy32Outl_2.png") }
            };

            foreach (var kvp in FontRenderers)
            {
                Restarted += kvp.Value.GameRestarted;
            }
            ClickSound = Content.Load<SoundEffect>("click");
            OnSound = Content.Load<SoundEffect>("on");
            OffSound = Content.Load<SoundEffect>("off");

//            TestScene = new TestScene(this);
//            TestScene.Initialize();
//            TestScene.SetActive();

            MenuScene = new MenuScene(this);           
            SettingsScene = new SettingsScene(this);
            HistoryScene = new HistoryScene(this);
            MainScene = new MainScene(this);

            MenuScene.Initialize();
            HistoryScene.Initialize();
            SettingsScene.Initialize();
            MainScene.Initialize();

            MenuScene.SetActive();
        }

        public int HandlerCount { get; private set; }
        public delegate void RestartedEventHandler();
        public event RestartedEventHandler Restarted;

        public void OnRestart()
        {
            Content.Unload();
            if (Restarted != null)
            {
                Restarted();
            }
        }

		/// <summary>
		/// Allows the game to run logic such as updating the world,
		/// checking for collisions, gathering input, and playing audio.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		protected override void Update (GameTime gameTime)
		{
#if !__IOS__ &&  !__TVOS__
			// For Mobile devices, this logic will close the Game when the Back button is pressed
			if (GamePad.GetState (PlayerIndex.One).Buttons.Back == ButtonState.Pressed) {
				Exit ();
			}
#endif
            // Transform the touch collection to show position in virtual coordinates
            TouchCollection = new TouchCollection(
                TouchPanel.GetState()
                          .Select(i => new TouchLocation(
                                        i.Id, 
                                        i.State, 
                                        new Vector2(
                                            (i.Position.X - ScaleMatrix.M41) / ScaleMatrix.M11, 
                                            (i.Position.Y - ScaleMatrix.M42) / ScaleMatrix.M22)))
                          .ToArray());
			// TODO: Add your update logic here
            CurrentScene.Update(gameTime);
			base.Update (gameTime);
		}

		/// <summary>
		/// This is called when the game should draw itself.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		protected override void Draw (GameTime gameTime)
		{
			//Graphics.GraphicsDevice.Clear (Color.CornflowerBlue);
            Graphics.GraphicsDevice.Clear (Color.ForestGreen);
		
			//TODO: Add your drawing code here
			//spriteBatch.Begin ();
            SpriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, null, ScaleMatrix);
            CurrentScene.Draw(gameTime);
			SpriteBatch.End ();

            //SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, null, null, null, null, ScaleMatrix);
            //SpriteBatch.Draw(CurrentScene.LightShader);
            //SpriteBatch.End ();

			base.Draw (gameTime);
		}
	}
}

