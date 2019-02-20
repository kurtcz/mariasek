﻿using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Media;
using Mariasek.SharedClient.GameComponents;
using System.Xml.Serialization;
using Mariasek.Engine.New;
using System.Diagnostics;
#if __IOS__
using Foundation;
#endif

namespace Mariasek.SharedClient
{
    public enum AnchorType
    {
        Main,
        Left,
        Top,
        Right,
        Bottom,
        Background
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

        private int _loadProgress;
        private int _maxProgress;
        private const int _progressBarOffset = 50;
        private const int _progressBarHeight = 5;
        private Color _progressBarColor = Color.White;
        private Texture2D _progressBar;
        private int _progressRenderWidth;
        private int _maxRenderWidth;

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

        private ReviewScene _reviewScene;
        public ReviewScene ReviewScene
        {
            get
            {
                if (_reviewScene == null)
                {
                    _reviewScene = new ReviewScene(this);
                    _reviewScene.Initialize();
                }
                return _reviewScene;
            }
            private set { _reviewScene = value; }
        }
        //public GeneratorScene GenerateScene { get; private set; }

        public GameSettings Settings { get; private set; }

		public Rectangle BackSideRect { get; set; }
        public Texture2D CardTextures { get; set; }
        public Texture2D CardTextures1 => Assets.GetTexture("marias");
        public Texture2D CardTextures2 => Assets.GetTexture("marias2");
        public Texture2D ReverseTexture => Assets.GetTexture("revers");
        public Texture2D LogoTexture => Assets.GetTexture("logo_hracikarty");
        public Texture2D RatingTexture => Assets.GetTexture("mariasek_rate");
        public Texture2D DefaultBackground => Assets.GetTexture("wood2");
        public Texture2D CanvasBackground { get; private set; }
        public Texture2D DarkBackground { get; private set; }
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

        public SoundEffect ClickSound => Assets.GetSoundEffect("watch-tick");
        //public SoundEffect TickSound { get; private set; }
        public SoundEffect OnSound => Assets.GetSoundEffect("on");
        public SoundEffect OffSound => Assets.GetSoundEffect("off");
        public SoundEffect ClapSound => Assets.GetSoundEffect("clap");
        public SoundEffect CoughSound => Assets.GetSoundEffect("cough");
        public SoundEffect BooSound => Assets.GetSoundEffect("boo");
        public SoundEffect LaughSound => Assets.GetSoundEffect("laugh");
        public SoundEffectInstance AmbientSound { get; private set; }
        public Song NaPankraciSong => Assets.GetSong("na pankraci");

        public IEmailSender EmailSender { get; private set; }
        public IWebNavigate Navigator { get; private set; }
        public IScreenManager ScreenManager { get; private set; }
		public IStorageAccessor StorageAccessor { get; private set; }
        public Vector2 CardScaleFactor { get; private set; }

        public List<Mariasek.Engine.New.MoneyCalculatorBase> Money = new List<Mariasek.Engine.New.MoneyCalculatorBase>();

        private Stopwatch sw = new Stopwatch();
        public MariasekMonoGame()
            : this(null, null, null, null)
        {
        }

		public MariasekMonoGame(IEmailSender emailSender, IWebNavigate navigator, IScreenManager screenManager, IStorageAccessor storageAccessor)
        {
            System.Diagnostics.Debug.WriteLine("MariasekMonoGame()");
            sw.Start();
            Graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsFixedTimeStep = false;
			StorageAccessor = storageAccessor;
            LoadGameSettings();
            Graphics.IsFullScreen = !Settings.ShowStatusBar;
            //make sure SupportedOrientations is set accordingly to ActivityAttribute.ScreenOrientation
            Graphics.SupportedOrientations = DisplayOrientation.LandscapeRight |
                                             DisplayOrientation.LandscapeLeft;
            Graphics.ApplyChanges();
            CardScaleFactor = new Vector2(0.6f, 0.6f); //will be overwritten in LoadGameSettings()
            EmailSender = emailSender;
            Navigator = navigator;
            ScreenManager = screenManager;

            Resumed += GameResumed;
            Paused += GamePaused;
        }

        public static string Platform
        {
            get
            {
#if __ANDROID__
                return "Android";
#elif __IOS__
                return "iOS";
#else
                return "unknown";
#endif
            }
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
#elif __IOS__
                var v = assembly.GetName().Version;
                var buildNumber = int.Parse(NSBundle.MainBundle.InfoDictionary["CFBundleVersion"].ToString());
                return new Version(v.Major, v.Minor, buildNumber);
#else
                return assembly.GetName().Version;
#endif
#endif
			}
        }

        public AssetLoader Assets;

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize ()
		{
            System.Diagnostics.Debug.WriteLine("Initialize()");

            SetupScaleMatrices();
            _loadProgress = 1;
            Assets = new AssetLoader(Content,
                                         new[]
                                         {
                                             "wood2",
                                             "BMFont_0",
                                             "BMFont_1",
                                             "BM2Font_0",
                                             "BM2Font_1",
                                             "SegoeUI40Outl_0",
                                             "SegoeUI40Outl_1",
                                             "SegoeUI40Outl_2",
                                             "marias",
                                             "marias2",
                                             "revers",
                                             "logo_hracikarty",
                                             "mariasek_rate"
                                         },
                                         new[]
                                         {
                                            "watch-tick",
                                            "on",
                                            "off",
                                            "clap",
                                            "cough",
                                            "boo",
                                            "laugh",
                                            "tavern-ambience-looping"
                                         },
                                         new[]
                                         {
                                            "na pankraci"
                                         });

            _maxProgress = Assets.TotalCount;
            _progressBar = new Texture2D(GraphicsDevice, 1, 1);
            _progressBar.SetData<Color>(new[] { Color.White });
            _maxRenderWidth = GraphicsDevice.Viewport.Width - 2 * _progressBarOffset;
            base.Initialize();
		}

        private void SetupScaleMatrices()
        {   
            var width = Math.Max(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            var height = Math.Min(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            var scaleX = (float)(width - ScreenManager.Padding.Left - ScreenManager.Padding.Right) / (float)VirtualScreenWidth;
            var scaleY = (float)(height - ScreenManager.Padding.Top - ScreenManager.Padding.Bottom) / (float)VirtualScreenHeight;

            var translation = new Vector3(ScreenManager.Padding.Left, ScreenManager.Padding.Top, 0);
            var scale = new Vector3(scaleY, scaleY, 1.0f);

            LeftScaleMatrix = Matrix.CreateScale(scale) * Matrix.CreateTranslation(translation);

            translation = new Vector3(width - ScreenManager.Padding.Right - VirtualScreenWidth * scaleY, ScreenManager.Padding.Top, 0);
            RightScaleMatrix = Matrix.CreateScale(scale) * Matrix.CreateTranslation(translation);

            translation = new Vector3(ScreenManager.Padding.Left, ScreenManager.Padding.Top, 0);
            scale = new Vector3(scaleX, scaleX, 1.0f);

            TopScaleMatrix = Matrix.CreateScale(scale) * Matrix.CreateTranslation(translation);

            translation = new Vector3(ScreenManager.Padding.Left, height - ScreenManager.Padding.Bottom - VirtualScreenHeight * scaleX, 0);
            BottomScaleMatrix = Matrix.CreateScale(scale) * Matrix.CreateTranslation(translation);

            if ((float)width / (float)height < (float)VirtualScreenWidth / (float)VirtualScreenHeight)
            {
                //skutecna obrazovka ma pomery sran mene sirokouhle nez virtualni
                //vertikalni pomer upravime podle horizontalniho, obraz vertikalne posuneme na stred (vzniknou okraje nahore a dole)
                translation = new Vector3(ScreenManager.Padding.Left, (height - VirtualScreenHeight * scaleX) / 2f, 0);
                scale = new Vector3(scaleX, scaleX, 1.0f);
                RealScreenGeometry = ScreenGeometry.Narrow;
            }
            else if ((float)width / (float)height > (float)VirtualScreenWidth / (float)VirtualScreenHeight)
            {
                //skutecna obrazovka ma pomery sran vice sirokouhle nez virtualni
                //horizontalni pomer upravime podle vertikalniho, obraz horizontalne posuneme na stred (vzniknou okraje vlevo a vpravo)
                translation = new Vector3((width - VirtualScreenWidth * scaleY) / 2f, ScreenManager.Padding.Top, 0);
                scale = new Vector3(scaleY, scaleY, 1.0f);
                RealScreenGeometry = ScreenGeometry.Wide;
            }
            MainScaleMatrix = Matrix.CreateScale(scale) * Matrix.CreateTranslation(translation);
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent ()
        {            
            System.Diagnostics.Debug.WriteLine("LoadContent()");
            System.Diagnostics.Debug.WriteLine("sw {0}", sw.ElapsedMilliseconds);
            var canvas = new Texture2D(GraphicsDevice, 1, 1);
            canvas.SetData(new[] { Color.DarkGreen });
            var dark = new Texture2D(GraphicsDevice, 1, 1);
            dark.SetData(new[] { Color.Black });

            // Create a new SpriteBatch, which can be used to draw textures.
            SpriteBatch = new SpriteBatch(GraphicsDevice);
            CanvasBackground = canvas;
            DarkBackground = dark;

            //Assets are loaded via AssetLoader class in the Update() loop
            System.Diagnostics.Debug.WriteLine("LoadContent finished sw {0}", sw.ElapsedMilliseconds);
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
            try
            {
                if (Settings == null)
                {
                    LoadGameSettings(true);
                }
                //Assets are handled by the ContentManager
            }
            catch (Exception ex)
            {
                try
                {
                    MainScene?.GameException(this, new GameExceptionEventArgs() { e = ex });
                }
                catch
                {                    
                }
            }
		}

        private void GamePaused()
        {
            //if (AmbientSound != null && !AmbientSound.IsDisposed)
            //{
            //    AmbientSound.Stop();
            //}
            //Microsoft.Xna.Framework.Media.MediaPlayer.Stop();
        }

		public void LoadGameSettings(bool forceLoad = true)
		{
			if (!forceLoad && Settings != null)
			{
				return;
			}

			var xml = new XmlSerializer(typeof(GameSettings));
            var currentStartingPlayerIndex = Settings?.CurrentStartingPlayerIndex ?? 0;
			try
			{
				StorageAccessor.GetStorageAccess();
				using (var fs = File.Open(_settingsFilePath, FileMode.Open))
				{
					Settings = (GameSettings)xml.Deserialize(fs);
				}
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine(string.Format("Cannot load settings\n{0}", e.Message));
                if (Settings == null)
                {
                    Settings = new GameSettings();
                }
			}
            if (!Settings.Default.HasValue ||
                Settings.Default.Value ||
                Settings.Thresholds == null ||
                !Settings.Thresholds.Any() ||
                Settings.Thresholds.Count() != Enum.GetValues(typeof(Hra)).Cast<Hra>().Count())
            {
                Settings.ResetThresholds();
            }
            //obe nastaveni nemuzou byt false protoze by nemuselo zbyt dost karet ktere jdou dat do talonu
            if (!Settings.AllowAXTalon && !Settings.AllowTrumpTalon)
            {
                Settings.AllowTrumpTalon = true;
            }
            if (Settings.RiskFactor > 0.5f)
            {
                Settings.RiskFactor = 0.5f;
            }
            CardScaleFactor = new Vector2(Settings.CardScaleFactor, Settings.CardScaleFactor);
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

        //public delegate void OrientationChangedEventHandler(object sender, OrientationChangedEventArgs e);
        //public event OrientationChangedEventHandler OrientationChanged;
        public virtual void OnOrientationChanged()
        {
            if (ScreenManager.Padding.Left > 0 ||
                ScreenManager.Padding.Top > 0 ||
                ScreenManager.Padding.Right > 0 ||
                ScreenManager.Padding.Bottom > 0)
            {
                SetupScaleMatrices();
            }
            //if (OrientationChanged != null)
            //{
            //    OrientationChanged(this, new OrientationChangedEventArgs { Padding = ScreenManager.Padding });
            //}
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
				StorageAccessor.GetStorageAccess();
				MainScene.CreateDirectoryForFilePath(_settingsFilePath);
				using (var fs = File.Open(_settingsFilePath, FileMode.Create))
				{
					xml.Serialize(fs, Settings);
				}
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine(string.Format("Cannot save settings\n{0}", e.Message));
			}
			LoadGameSettings();
		}

        bool _contentLoaded = false;

        private void ContentLoaded()
        {
            FontRenderers = new Dictionary<string, FontRenderer>
                        {
                            { "BMFont", FontRenderer.GetFontRenderer(this, 5, 8, "BMFont.fnt", "BMFont_0", "BMFont_1") },
                            { "BM2Font", FontRenderer.GetFontRenderer(this, "BM2Font.fnt", "BM2Font_0", "BM2Font_1") },
                            { "SegoeUI40Outl", FontRenderer.GetFontRenderer(this, "SegoeUI40Outl.fnt", "SegoeUI40Outl_0", "SegoeUI40Outl_1", "SegoeUI40Outl_2") }
                        };

            AmbientSound = Assets.GetSoundEffect("tavern-ambience-looping").CreateInstance();
            if (AmbientSound != null && !AmbientSound.IsDisposed)
            {
                AmbientSound.IsLooped = true;
                AmbientSound.Volume = 0;
                AmbientSound.PlaySafely();
            }
            if (NaPankraciSong != null && !NaPankraciSong.IsDisposed)
            {
                Microsoft.Xna.Framework.Media.MediaPlayer.IsRepeating = true;
                Microsoft.Xna.Framework.Media.MediaPlayer.Volume = Settings.BgSoundEnabled ? 0.1f : 0f;
                Microsoft.Xna.Framework.Media.MediaPlayer.Play(NaPankraciSong);
            }
            System.Diagnostics.Debug.WriteLine("update sw {0}", sw.ElapsedMilliseconds);

            MenuScene.SetActive();
            MainScene.ResumeGame();
        }

		/// <summary>
		/// Allows the game to run logic such as updating the world,
		/// checking for collisions, gathering input, and playing audio.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		protected override void Update (GameTime gameTime)
		{
            try
            {
                if (!Assets.ContentLoaded)
                {
                    _progressRenderWidth = _maxRenderWidth * _loadProgress / _maxProgress;
                    if (Assets.LoadOneAsset())
                    {
                        _loadProgress++;
                    }
                    else
                    {
                        ContentLoaded();
                    }
                }
                else
                {
#if !__IOS__ && !__TVOS__
                    // For Mobile devices, this logic will close the Game when the Back button is pressed
                    if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                    {
                        //Exit ();                      //under MonoGame 3.6.0 there is a bug that prevents activity from being able to resume again
                        Activity.MoveTaskToBack(true);  //so we need to call this instead
                    }
#endif
                    if (gameTime.ElapsedGameTime.TotalMilliseconds > 500)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("!!!!! Update called after {0} ms", gameTime.ElapsedGameTime.TotalMilliseconds));
                    }
                    TouchCollection = TouchPanel.GetState();
                    // TODO: Add your update logic here
                    CurrentScene.Update(gameTime);
                }
            }
            catch(Exception ex)
            {
                try
                {
                    MainScene?.GameException(this, new GameExceptionEventArgs { e = ex });
                }
                catch
                {                    
                }
            }
            base.Update(gameTime);
		}

		/// <summary>
		/// This is called when the game should draw itself.
		/// </summary>
		/// <param name="gameTime">Provides a snapshot of timing values.</param>
		protected override void Draw (GameTime gameTime)
		{
            try
            {
                GraphicsDevice.Clear(Color.Black);
                if (!Assets.ContentLoaded)
                {
                    if (DefaultBackground != null)
                    {
                        SpriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, null, null);
                        SpriteBatch.Draw(DefaultBackground, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);
                        SpriteBatch.Draw(_progressBar, new Rectangle(_progressBarOffset, GraphicsDevice.Viewport.Height - _progressBarOffset - _progressBarHeight / 2, _progressRenderWidth, _progressBarHeight), _progressBarColor);
                        SpriteBatch.End();
                    }
                    return;
                }

                if (SpriteBatch == null || SpriteBatch.IsDisposed)
                {
                    SpriteBatch = new SpriteBatch(GraphicsDevice);
                }
                try
                {
                    CurrentRenderingGroup = AnchorType.Background;
                    SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, null);
                    CurrentScene.Draw(gameTime);
                }
                catch
                {
                }
                finally
                {
                    SpriteBatch.End();
                }
                try
                {
                    CurrentRenderingGroup = AnchorType.Main;
                    SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, MainScaleMatrix);
                    CurrentScene.Draw(gameTime);
                }
                catch
                {
                }
                finally
                {
                    SpriteBatch.End();
                }

                try
                {
                    CurrentRenderingGroup = AnchorType.Left;
                    SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, LeftScaleMatrix);
                    CurrentScene.Draw(gameTime);
                }
                catch
                {
                }
                finally
                {
                    SpriteBatch.End();
                }

                try
                {
                    CurrentRenderingGroup = AnchorType.Top;
                    SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, TopScaleMatrix);
                    CurrentScene.Draw(gameTime);
                }
                catch
                {
                }
                finally
                {
                    SpriteBatch.End();
                }

                try
                {
                    CurrentRenderingGroup = AnchorType.Right;
                    SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, RightScaleMatrix);
                    CurrentScene.Draw(gameTime);
                }
                catch
                {
                }
                finally
                {
                    SpriteBatch.End();
                }

                try
                {
                    CurrentRenderingGroup = AnchorType.Bottom;
                    SpriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, BottomScaleMatrix);
                    CurrentScene.Draw(gameTime);
                }
                catch
                {
                }
                finally
                {
                    SpriteBatch.End();
                }
            }
            catch
            {                
            }
		}
	}
}

