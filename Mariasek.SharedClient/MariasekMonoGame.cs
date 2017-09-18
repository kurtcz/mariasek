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
using Microsoft.Xna.Framework.Media;
using Mariasek.SharedClient.GameComponents;
using System.Xml.Serialization;
using Mariasek.Engine.New;

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
    public class MariasekMonoGame : Microsoft.Xna.Framework.Game
    {
#if __ANDROID__
		private static string _path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "Mariasek");
#else   //#elif __IOS__
        private static string _path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#endif
		public static string _settingsFilePath = Path.Combine(_path, "Mariasek.settings");
		
        //Texture2D logoTexture;
		public GraphicsDeviceManager Graphics { get; private set; }
        public TouchCollection TouchCollection { get; private set; }
        public SpriteBatch SpriteBatch { get; private set; }
        public Scene CurrentScene { get; set; }
        //public TestScene TestScene { get; private set; }

        private MainScene _mainScene;
        public MainScene MainScene
        {
            get
            {
                if (_mainScene == null)
                {
                    _mainScene = new MainScene(this);
                    _mainScene.Initialize();
                }
                return _mainScene;
            }
            private set { _mainScene = value; }
        }
		private MenuScene _menuScene;
		public MenuScene MenuScene
		{
			get
			{
				if (_menuScene == null)
				{
					_menuScene = new MenuScene(this);
					_menuScene.Initialize();
				}
				return _menuScene;
			}
			private set { _menuScene = value; }
		}
		private SettingsScene _settingsScene;
		public SettingsScene SettingsScene
		{
			get
			{
				if (_settingsScene == null)
				{
					_settingsScene = new SettingsScene(this);
					_settingsScene.Initialize();
				}
				return _settingsScene;
			}
			private set { _settingsScene = value; }
		}
        private AiSettingsScene _aiSettingsScene;
        public AiSettingsScene AiSettingsScene
        {
            get
            {
                if (_aiSettingsScene == null)
                {
                    _aiSettingsScene = new AiSettingsScene(this);
                    _aiSettingsScene.Initialize();
                }
                return _aiSettingsScene;
            }
            private set { _aiSettingsScene = value; }
        }
        private HistoryScene _historyScene;
        public HistoryScene HistoryScene
		{
			get
			{
				if (_historyScene == null)
				{
					_historyScene = new HistoryScene(this);
					_historyScene.Initialize();
				}
				return _historyScene;
			}
			private set { _historyScene = value; }
		}
		private StatScene _statScene;
		public StatScene StatScene
		{
			get
			{
				if (_statScene == null)
				{
					_statScene = new StatScene(this);
					_statScene.Initialize();
				}
				return _statScene;
			}
			private set { _statScene = value; }
		}
		//public GeneratorScene GenerateScene { get; private set; }

		public GameSettings Settings { get; private set; }

		public Rectangle BackSideRect { get; set; }
        public Texture2D CardTextures { get; set; }
        public Texture2D CardTextures1 { get; private set; }
        public Texture2D CardTextures2 { get; private set; }
        public Texture2D ReverseTexture { get; private set; }
        public Texture2D LogoTexture { get; private set; }
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
        public AnchorType CurrentRenderingGroup { get; set; }

        public SoundEffect ClickSound { get; private set; }
        public SoundEffect TickSound { get; private set; }
        public SoundEffect OnSound { get; private set; }
        public SoundEffect OffSound { get; private set; }
        public SoundEffect ClapSound { get; private set; }
        public SoundEffect CoughSound { get; private set; }
        public SoundEffect BooSound { get; private set; }
        public SoundEffect LaughSound { get; private set; }
        public SoundEffectInstance AmbientSound { get; private set; }
        public Song NaPankraciSong { get; private set; }
        public IEmailSender EmailSender { get; private set; }
        public IWebNavigate Navigator { get; private set; }
        public readonly Vector2 CardScaleFactor;

        public List<Mariasek.Engine.New.MoneyCalculatorBase> Money = new List<Mariasek.Engine.New.MoneyCalculatorBase>();

        public MariasekMonoGame()
            : this(null, null)
        {
        }

        public MariasekMonoGame(IEmailSender emailSender, IWebNavigate navigator)
        {
            System.Diagnostics.Debug.WriteLine("MariasekMonoGame()");
            Graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsFixedTimeStep = false;
            Graphics.IsFullScreen = true;
            //make sure SupportedOrientations is set accordingly to ActivityAttribute.ScreenOrientation
            Graphics.SupportedOrientations = DisplayOrientation.LandscapeRight |
                                             DisplayOrientation.LandscapeLeft;
            Graphics.ApplyChanges();
            CardScaleFactor = new Vector2(0.6f, 0.6f);
            EmailSender = emailSender;
            Navigator = navigator;
            Resumed += GameResumed;
            Paused += GamePaused;
        }

        public static Version Version
        {
            get
            {
                var assembly = typeof(MariasekMonoGame).Assembly;

#if DEBUG
                return assembly.GetName().Version;
#else //RELEASE
#if __ANDROID__
                var v = assembly.GetName().Version;
                var context = Android.App.Application.Context;
                var versionCode = context.PackageManager.GetPackageInfo(context.PackageName, 0).VersionCode;

                return new Version(v.Major, v.Minor, versionCode);
#else
                return assembly.GetName().Version;
#endif
#endif
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
            LogoTexture = Content.Load<Texture2D>("logo_hracikarty");
            FontRenderers = new Dictionary<string, FontRenderer>
            {
                { "BMFont", FontRenderer.GetFontRenderer(this, 5, 8, "BMFont.fnt", "BMFont_0", "BMFont_1") },
                { "BM2Font", FontRenderer.GetFontRenderer(this, "BM2Font.fnt", "BM2Font_0", "BM2Font_1") },
                { "SegoeUI40Outl", FontRenderer.GetFontRenderer(this, "SegoeUI40Outl.fnt", "SegoeUI40Outl_0", "SegoeUI40Outl_1", "SegoeUI40Outl_2") },
                { "LuckiestGuy32Outl", FontRenderer.GetFontRenderer(this, "LuckiestGuy32Outl.fnt", "LuckiestGuy32Outl_0", "LuckiestGuy32Outl_1", "LuckiestGuy32Outl_2") }
            };

            try
            {
                ClickSound = Content.Load<SoundEffect>("watch-tick");
                //TickSound = Content.Load<SoundEffect>("watch-tick");
                OnSound = Content.Load<SoundEffect>("on");
                OffSound = Content.Load<SoundEffect>("off");
                ClapSound = Content.Load<SoundEffect>("clap");
                CoughSound = Content.Load<SoundEffect>("cough");
                BooSound = Content.Load<SoundEffect>("boo");
                LaughSound = Content.Load<SoundEffect>("laugh");

                AmbientSound = Content.Load<SoundEffect>("tavern-ambience-looping").CreateInstance();
                AmbientSound.IsLooped = true;
                AmbientSound.Volume = 0;
                AmbientSound?.PlaySafely();

                NaPankraciSong = Content.Load<Song>("na pankraci");
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + "\n" + ex.StackTrace);
            }

			LoadGameSettings();

			MenuScene.SetActive();
            MainScene.ResumeGame();
        }

        //protected override void Dispose(bool disposing)
        //{
        //    if (MainScene != null)
        //    {
        //        MainScene.CancelRunningTask();
        //    }
        //    base.Dispose(disposing);
        //}

        public delegate void ResumedEventHandler();
        public event ResumedEventHandler Resumed;

        public void OnResume()
        {
            if (Resumed != null)
            {
                Resumed();
            }
        }

        public delegate void PausedEventHandler();
        public event PausedEventHandler Paused;

        public void OnPaused()
        {
            if (Paused != null)
            {
                Paused();
            }
        }

        public delegate void StartedEventHandler();
        public event StartedEventHandler Started;

        public void OnStart()
        {
            if (Started != null)
            {
                Started();
            }
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

        public delegate void StoppedEventHandler();
        public event StoppedEventHandler Stopped;

        public void OnStop()
        {
            if (Stopped != null)
            {
                Stopped();
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

        private void GameResumed()
        {
            if (AmbientSound != null && NaPankraciSong != null)
            {
                AmbientSound.PlaySafely();
                MediaPlayer.Play(NaPankraciSong);
                MediaPlayer.IsRepeating = true;
            }
        }

        private void GamePaused()
        {
            if (AmbientSound != null)
            {
                AmbientSound.Stop();
            }
            MediaPlayer.Stop();
        }

		public void LoadGameSettings(bool forceLoad = true)
		{
			if (!forceLoad && Settings != null)
			{
				return;
			}

			var xml = new XmlSerializer(typeof(GameSettings));
			try
			{
				using (var fs = File.Open(_settingsFilePath, FileMode.Open))
				{
					Settings = (GameSettings)xml.Deserialize(fs);
					if (!Settings.Default.HasValue ||
						Settings.Default.Value ||
						Settings.Thresholds == null ||
						!Settings.Thresholds.Any() ||
						Settings.Thresholds.Count() != Enum.GetValues(typeof(Hra)).Cast<Hra>().Count())
					{
						Settings.ResetThresholds();
					}
					Settings.ThinkingTimeMs = 2000;
				}
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine(string.Format("Cannot load settings\n{0}", e.Message));
				Settings = new GameSettings();
			}
//			_performance.Text = string.Format("Výkon simulace: {0} her/s",
//				Settings.GameTypeSimulationsPerSecond > 0 ? Settings.GameTypeSimulationsPerSecond.ToString() : "?");
		}

		public delegate void SettingsChangedEventHandler(object sender, SettingsChangedEventArgs e);
		public event SettingsChangedEventHandler SettingsChanged;
		public virtual void OnSettingsChanged()
		{
			if (SettingsChanged != null)
			{
				SettingsChanged(this, new SettingsChangedEventArgs { Settings = Settings });
			}
		}

		public void UpdateSettings()
		{
			SaveGameSettings();
			OnSettingsChanged();
		}

		public void SaveGameSettings()
		{
			var xml = new XmlSerializer(typeof(GameSettings));
			try
			{
				MainScene.CreateDirectoryForFilePath(_settingsFilePath);
				using (var fs = File.Open(_settingsFilePath, FileMode.Create))
				{
					xml.Serialize(fs, Settings);
				}
				//using (var tw = new StringWriter())
				//{
				//    xml.Serialize(tw, _settings);
				//    var str = tw.ToString();
				//}
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine(string.Format("Cannot save settings\n{0}", e.Message));
			}
			LoadGameSettings();
		}

		/// <summary>
		/// Allows the game to run logic such as updating the world,
		/// checking for collisions, gathering input, and playing audio.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		protected override void Update (GameTime gameTime)
		{
#if !__IOS__ && !__TVOS__
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

