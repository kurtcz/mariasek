#define LOADGAME
#region Using Statements
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
	public enum AnchorType
	{
		Main,
		Left,
		Top,
		Right,
		Bottom
	}

	public enum ScreenGeometry
	{
		Narrow,
		Wide
	}

	/// <summary>
	/// This is the main type for your game
	/// </summary>
	public class MariasekMonoGame : Game
	{
		//Texture2D logoTexture;
        public GraphicsDeviceManager Graphics { get; private set; }
        public TouchCollection TouchCollection { get; private set; }
        public SpriteBatch SpriteBatch { get; private set; }
        public Scene CurrentScene { get; set; }
        public TestScene TestScene { get; private set; }
        public MainScene MainScene { get; private set; }
        public MenuScene MenuScene { get; private set; }
        public SettingsScene SettingsScene { get; private set; }
        public HistoryScene HistoryScene { get; private set; }
		public GeneratorScene GenerateScene { get; private set; }

		public Texture2D CardTextures { get; set; }
        public Texture2D CardTextures1 { get; private set; }
		public Texture2D CardTextures2 { get; private set; }
        public Texture2D ReverseTexture { get; private set; }
        public Dictionary<string, FontRenderer> FontRenderers { get; private set; }

		//Let's create a virtual screen with acpect ratio that is neither widescreen nor narrowscreen
		// iPhone5+, Samsung A3:	1.78 : 1
		// iPad:					1.33 : 1
		// old iPhone:				1.50 : 1
		// old Androids 800x480:	1.67 : 1
		// Virtual 800x512:			1.56 : 1
		public readonly float VirtualScreenWidth = 800;
        public readonly float VirtualScreenHeight = 512;
		public Matrix MainScaleMatrix;
		public Matrix LeftScaleMatrix;
		public Matrix RightScaleMatrix;
		public Matrix TopScaleMatrix;
		public Matrix BottomScaleMatrix;
		public ScreenGeometry RealScreenGeometry;

		/// <summary>
		/// Gets the current rendering group. Game components should render themselves only if they belong to the current group.
		/// </summary>
		public AnchorType CurrentRenderingGroup { get; private set; } 

        public SoundEffect ClickSound { get; private set; }
		public SoundEffect TickSound { get; private set; }
        public SoundEffect OnSound { get; private set; }
        public SoundEffect OffSound { get; private set; }
        public SoundEffect ClapSound { get; private set; }
        public SoundEffect CoughSound { get; private set; }
        public SoundEffect BooSound { get; private set; }
        public IEmailSender EmailSender { get; private set; }
        public readonly Vector2 CardScaleFactor;

        public List<Mariasek.Engine.New.MoneyCalculatorBase> Money = new List<Mariasek.Engine.New.MoneyCalculatorBase>();

        public MariasekMonoGame()
            : this(null)
        {
        }

        public MariasekMonoGame (IEmailSender emailSender)
		{
            System.Diagnostics.Debug.WriteLine("MariasekMonoGame()");
			Graphics = new GraphicsDeviceManager (this);
			Content.RootDirectory = "Content";	            
			Graphics.IsFullScreen = true;
			//make sure SupportedOrientations is set accordingly to ActivityAttribute.ScreenOrientation
			Graphics.SupportedOrientations = DisplayOrientation.LandscapeRight |
											 DisplayOrientation.LandscapeLeft;	
            Graphics.ApplyChanges();
            CardScaleFactor = new Vector2(1.2f, 1.2f);
            EmailSender = emailSender;
		}

        public static Version Version
        {
            get
            {
                var assembly = typeof(MariasekMonoGame).Assembly;

                return assembly.GetName().Version;
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

            var width = Math.Max(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            var height = Math.Min(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            var scaleX = (float)width / (float)VirtualScreenWidth;
			var scaleY = (float)height / (float)VirtualScreenHeight;
            
			var translation = new Vector3(width - VirtualScreenWidth * scaleY, 0, 0);
			var scale = new Vector3(scaleY, scaleY, 1.0f);

			LeftScaleMatrix = Matrix.CreateScale(scale);
			RightScaleMatrix = Matrix.CreateScale(scale) * Matrix.CreateTranslation(translation);

			translation = new Vector3(0, height - VirtualScreenHeight * scaleX, 0);
			scale = new Vector3(scaleX, scaleX, 1.0f);

			TopScaleMatrix = Matrix.CreateScale(scale);
			BottomScaleMatrix = Matrix.CreateScale(scale) * Matrix.CreateTranslation(translation);

            if ((float)width / (float)height < (float)VirtualScreenWidth / (float)VirtualScreenHeight)
            {
                //skutecna obrazovka ma pomery sran mene sirokouhle nez virtualni
                //vertikalni pomer upravime podle horizontalniho, obraz vertikalne posuneme na stred (vzniknou okraje nahore a dole)
                translation = new Vector3(0, (height - VirtualScreenHeight * scaleX) / 2f, 0);
				scale = new Vector3(scaleX, scaleX, 1.0f);
				RealScreenGeometry = ScreenGeometry.Narrow;
            }
            else if ((float)width / (float)height > (float)VirtualScreenWidth / (float)VirtualScreenHeight)
            {
                //skutecna obrazovka ma pomery sran vice sirokouhle nez virtualni
                //horizontalni pomer upravime podle vertikalniho, obraz horizontalne posuneme na stred (vzniknou okraje vlevo a vpravo)
                translation = new Vector3((width - VirtualScreenWidth * scaleY) / 2f, 0, 0);
				scale = new Vector3(scaleY, scaleY, 1.0f);
				RealScreenGeometry = ScreenGeometry.Wide;
            }
			MainScaleMatrix = Matrix.CreateScale(scale) * Matrix.CreateTranslation(translation);

			base.Initialize();
		}

		/// <summary>
		/// LoadContent will be called once per game and is the place to load
		/// all of your content.
		/// </summary>
		protected override void LoadContent ()
        {
            System.Diagnostics.Debug.WriteLine("LoadContent()");
            // Create a new SpriteBatch, which can be used to draw textures.
            SpriteBatch = new SpriteBatch(GraphicsDevice);
            CardTextures1 = Content.Load<Texture2D>("marias");
			CardTextures2 = Content.Load<Texture2D>("marias2");
            ReverseTexture = Content.Load<Texture2D>("revers");
            FontRenderers = new Dictionary<string, FontRenderer>
            {
                { "BMFont", FontRenderer.GetFontRenderer(this, "BMFont.fnt", "BMFont_0", "BMFont_1") },
                { "BM2Font", FontRenderer.GetFontRenderer(this, "BM2Font.fnt", "BM2Font_0", "BM2Font_1") },
                { "SegoeUI40Outl", FontRenderer.GetFontRenderer(this, "SegoeUI40Outl.fnt", "SegoeUI40Outl_0", "SegoeUI40Outl_1", "SegoeUI40Outl_2") },
                { "LuckiestGuy32Outl", FontRenderer.GetFontRenderer(this, "LuckiestGuy32Outl.fnt", "LuckiestGuy32Outl_0", "LuckiestGuy32Outl_1", "LuckiestGuy32Outl_2") }
            };

            ClickSound = Content.Load<SoundEffect>("click");
			//TickSound = Content.Load<SoundEffect>("watch-tick");
            OnSound = Content.Load<SoundEffect>("on");
            OffSound = Content.Load<SoundEffect>("off");
            ClapSound = Content.Load<SoundEffect>("clap");
            CoughSound = Content.Load<SoundEffect>("cough");
            BooSound = Content.Load<SoundEffect>("boo");
            //TestScene = new TestScene(this);
            //TestScene.Initialize();
            //TestScene.SetActive();

            MenuScene = new MenuScene(this);           
            SettingsScene = new SettingsScene(this);
            HistoryScene = new HistoryScene(this);
            MainScene = new MainScene(this);
			//GenerateScene = new GeneratorScene(this);

            MenuScene.Initialize();
            HistoryScene.Initialize();
            SettingsScene.Initialize();
            MainScene.Initialize();
			//GenerateScene.Initialize();

            MenuScene.SetActive();
            //#if LOADGAME
            if (MainScene.CanLoadGame())
            {
                MainScene.LoadGame();
            }
//#endif
        }

        protected override void Dispose(bool disposing)
        {
            MainScene.CancelRunningTask();
            base.Dispose(disposing);
        }

        public delegate void RestartedEventHandler();
        public event RestartedEventHandler Restarted;

        public void OnRestart()
        {
            if (Restarted != null)
            {
                Restarted();
            }
        }
        public delegate void SaveInstanceStateEventHandler();
        public event SaveInstanceStateEventHandler SaveInstanceState;

        public void OnSaveInstanceState()
        {
            if (SaveInstanceState != null)
            {
                SaveInstanceState();
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
			TouchCollection = TouchPanel.GetState();
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
            Graphics.GraphicsDevice.Clear (Color.ForestGreen);
		
			//TODO: Add your drawing code here
			CurrentRenderingGroup = AnchorType.Main;
			SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, MainScaleMatrix);
			CurrentScene.Draw(gameTime);
			SpriteBatch.End();

			CurrentRenderingGroup = AnchorType.Left;
			SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, LeftScaleMatrix);
			CurrentScene.Draw(gameTime);
			SpriteBatch.End();

			CurrentRenderingGroup = AnchorType.Top;
			SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, TopScaleMatrix);
			CurrentScene.Draw(gameTime);
			SpriteBatch.End();

			CurrentRenderingGroup = AnchorType.Right;
			SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, RightScaleMatrix);
			CurrentScene.Draw(gameTime);
			SpriteBatch.End();

			CurrentRenderingGroup = AnchorType.Bottom;
			SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, BottomScaleMatrix);
			CurrentScene.Draw(gameTime);
			SpriteBatch.End();
		}
	}
}

