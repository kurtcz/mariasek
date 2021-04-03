using System;
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
using Mariasek.Engine;
using System.Diagnostics;
using System.Threading.Tasks;
using CsvHelper;
using System.Globalization;
//using Android.Views;
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
        //Android 1-9: /Mariasek
        //Android 10+: /Android/data/com.tnemec.mariasek.android/files
        public static string RootPath => (int)Android.OS.Build.VERSION.SdkInt >= 29
                                            ? Android.App.Application.Context.GetExternalFilesDir(null).Path
                                            : Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "Mariasek");
        //public static string RootPath = Android.App.Application.Context.GetExternalFilesDir(null).Path;
#else   //#elif __IOS__
        public static string RootPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#endif
        public static string _settingsFilePath = Path.Combine(RootPath, "Mariasek.settings");

        private int _loadProgress;
        private int _maxProgress;
        private const int _progressBarOffset = 50;
        private const int _progressBarHeight = 5;
        private Color _progressBarColor = Color.White;
        private Texture2D _progressBar;
        private int _progressRenderWidth;
        private int _maxRenderWidth;
        private string _fileBeingMigrated;
        private FontRenderer _textRenderer;
        private Vector2 _textPosition;
        private Random _random = new Random();

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
        private EditorScene _editorScene;
        public EditorScene EditorScene
        {
            get
            {
                if (_editorScene == null)
                {
                    _editorScene = new EditorScene(this);
                    _editorScene.Initialize();
                }
                return _editorScene;
            }
            private set { _editorScene = value; }
        }
        public GameSettings Settings { get; private set; }
        public bool SettingsLoaded { get; private set; }
        public NumberFormatInfo CurrencyFormat
        {
            get
            {
                var numberFormatInfo = CultureInfo.CreateSpecificCulture(Settings.Locale).NumberFormat;
#if __IOS__     //Comply with Apple's Simulating Gambling policies
                numberFormatInfo.CurrencySymbol = string.Empty;
#endif
                return numberFormatInfo;
            }
        }

        public Rectangle BackSideRect { get; set; }
        public Texture2D CardTextures { get; set; }
        public Texture2D CardTextures1 => Assets.GetTexture("marias");
        public Texture2D CardTextures2 => Assets.GetTexture("marias2");
        public Texture2D CardTextures3 => Assets.GetTexture("marias3");
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
        public Vector2 CardScaleFactor { get; set; }

        public List<Mariasek.Engine.MoneyCalculatorBase> Money = new List<Mariasek.Engine.MoneyCalculatorBase>();

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
            LoadGameSettings(true);
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
            Window.ClientSizeChanged += Window_ClientSizeChanged;
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
                                             "marias3",
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

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            SetupScaleMatrices();
        }

        public void SetupScaleMatrices()
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
                translation = new Vector3(ScreenManager.Padding.Left, (height - VirtualScreenHeight * scaleX) / 2f - ScreenManager.Padding.Bottom, 0);
                scale = new Vector3(scaleX, scaleX, 1.0f);
                RealScreenGeometry = ScreenGeometry.Narrow;
            }
            else if ((float)width / (float)height > (float)VirtualScreenWidth / (float)VirtualScreenHeight)
            {
                //skutecna obrazovka ma pomery sran vice sirokouhle nez virtualni
                //horizontalni pomer upravime podle vertikalniho, obraz horizontalne posuneme na stred (vzniknou okraje vlevo a vpravo)
                translation = new Vector3((width - VirtualScreenWidth * scaleY) / 2f - ScreenManager.Padding.Right, ScreenManager.Padding.Top, 0);
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
                LoadGameSettings(false);
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
        }

		public void LoadGameSettings(bool forceLoad)
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
				SettingsLoaded = true;
                //docasny kod pro iOS: ignoruj stare nastaveni ShowRatingOffer
#if _IOS_
                var creationTime = new FileInfo(_settingsFilePath).CreationTime;
                var thresholdTime = new DateTime(2021,4,4);

                if (creationTime < thresholdTime)
                {
                    Settings.ShowRatingOffer = null;
                    SaveGameSettings();
                }
#endif
            }
            catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine(string.Format("Cannot load settings\n{0}", e.Message));
                Settings = new GameSettings();
                Settings.CurrentStartingPlayerIndex = currentStartingPlayerIndex;
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
            if (Settings.BgMusicMinDelayMs <= 0)
            {
                Settings.BgMusicMinDelayMs = 60000;
            }
            if (Settings.BgMusicMaxDelayMs <= Settings.BgMusicMinDelayMs)
            {
                Settings.BgMusicMaxDelayMs = Settings.BgMusicMinDelayMs;
            }
            if (Settings.FirstMinMaxRound == 0)
            {
                Settings.FirstMinMaxRound = 8;
            }
            if (Settings.SafetyBetlThreshold > 0 &&
                Settings.SafetyBetlThreshold < 24)
            {
                Settings.SafetyBetlThreshold = 24;
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
			LoadGameSettings(false);
		}

        bool _contentLoaded = false;
        private bool _loadingFinished;

        private void ContentLoaded()
        {
            FontRenderers = new Dictionary<string, FontRenderer>
                        {
                            { "BMFont", FontRenderer.GetFontRenderer(this, 5, 8, "BMFont.fnt", "BMFont_0", "BMFont_1") },
                            { "BM2Font", FontRenderer.GetFontRenderer(this, "BM2Font.fnt", "BM2Font_0", "BM2Font_1") },
                            { "SegoeUI40Outl", FontRenderer.GetFontRenderer(this, "SegoeUI40Outl.fnt", "SegoeUI40Outl_0", "SegoeUI40Outl_1", "SegoeUI40Outl_2") }
                        };
            _textRenderer = FontRenderers["BM2Font"];
            _textPosition = new Vector2(VirtualScreenWidth / 2f, VirtualScreenHeight / 2f);

            AmbientSound = Assets.GetSoundEffect("tavern-ambience-looping").CreateInstance();
            if (AmbientSound != null && !AmbientSound.IsDisposed)
            {
                AmbientSound.IsLooped = true;
                AmbientSound.Volume = 0;
                AmbientSound.PlaySafely();
            }
            Task.Run(async () =>
            {
                await Task.Delay(_random.Next(Settings.BgMusicMinDelayMs) / 2);
                PlayBackgroundMusic();
            });
            System.Diagnostics.Debug.WriteLine("update sw {0}", sw.ElapsedMilliseconds);

            MigrateFilesIfNeeded();
            if (!SettingsLoaded)
            {
                LoadGameSettings(true);
            }
        }

        private void MigrateFilesIfNeeded()
        {
#if __ANDROID__
            if ((int)Android.OS.Build.VERSION.SdkInt == 29)
            {
                try
                {
                    var legacyFolder = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath + "/Mariasek";

                    StorageAccessor.GetStorageAccess(true);
                    if (Directory.Exists(legacyFolder) &&
                        !Directory.Exists(Path.Combine(RootPath, "Archive")) &&
                        !File.Exists(Path.Combine(RootPath, "Mariasek.history")) &&
                        !File.Exists(Path.Combine(RootPath, "Mariasek.deck")) &&
                        !File.Exists(Path.Combine(RootPath, "Mariasek.settings")))
                    {
                        MigrateFiles();
                    }
                    else
                    {
                        _loadingFinished = true;
                    }
                }
                catch
                {
                    _loadingFinished = true;
                }
            }
            else
            {
                _loadingFinished = true;
            }
#else
            _loadingFinished = true;
#endif
        }

#if __ANDROID__
        private void MigrateFiles()
        {
            Task.Run(() =>
            {
                try
                {
                    var legacyFolder = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath + "/Mariasek";
                    var destinationFolder = Android.App.Application.Context.GetExternalFilesDir(null).Path;
                    var legacyArchive = Path.Combine(legacyFolder, "Archive");
                    var destinationArchive = Path.Combine(destinationFolder, "Archive");
                    var legacyEditor = Path.Combine(legacyFolder, "Editor");
                    var destinationEditor = Path.Combine(destinationFolder, "Editor");

                    foreach (var entry in Directory.EnumerateFiles(legacyFolder))
                    {
                        _fileBeingMigrated = Path.GetFileName(entry);
                        var destination = Path.Combine(destinationFolder, _fileBeingMigrated);
                        MoveWithOverwrite(entry, destination);
                    }
                    if (!Directory.Exists(destinationArchive))
                    {
                        Directory.CreateDirectory(destinationArchive);
                    }
                    foreach (var entry in Directory.EnumerateFiles(legacyArchive))
                    {
                        _fileBeingMigrated = Path.GetFileName(entry);
                        var destination = Path.Combine(destinationArchive, _fileBeingMigrated);
                        MoveWithOverwrite(entry, destination);
                    }
                    if (Directory.Exists(legacyEditor))
                    {
                        if (!Directory.Exists(destinationEditor))
                        {
                            Directory.CreateDirectory(destinationEditor);
                        }
                        foreach (var entry in Directory.EnumerateFiles(legacyEditor))
                        {
                            _fileBeingMigrated = Path.GetFileName(entry);
                            var destination = Path.Combine(destinationEditor, _fileBeingMigrated);
                            MoveWithOverwrite(entry, destination);
                        }
                    }
                    Directory.Delete(legacyFolder, true);
                }
                catch
                { }
                finally
                {
                    _loadingFinished = true;
                }
            });
        }
#endif

        private void MoveWithOverwrite(string source, string destination)
        {
            try
            {
                if (File.Exists(destination))
                {
                    File.Delete(destination);
                }
                File.Move(source, destination);
            }
            catch
            { }
        }

        public async void PlayBackgroundMusic()
        {
            if (NaPankraciSong != null && !NaPankraciSong.IsDisposed)
            {
                await Task.Run(() =>
                {
                    try
                    {
                        //Microsoft.Xna.Framework.Media.MediaPlayer.IsRepeating = true;
                        if (Microsoft.Xna.Framework.Media.MediaPlayer.State == MediaState.Stopped)
                        {
                            Microsoft.Xna.Framework.Media.MediaPlayer.Volume = Settings.BgSoundEnabled ? 0.1f : 0f;
                            Microsoft.Xna.Framework.Media.MediaPlayer.Play(NaPankraciSong);
                        }
                    }
                    catch(Exception ex)
                    {
                    }
                });
                await Task.Delay(_random.Next(Settings.BgMusicMinDelayMs, Settings.BgMusicMaxDelayMs));
                PlayBackgroundMusic();
            }
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
                if (_loadingFinished && CurrentScene == null)
                {
                    MenuScene.SetActive();
                    MainScene.ResumeGame();
                }
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
                else if (CurrentScene != null)
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
                if (!string.IsNullOrEmpty(_fileBeingMigrated))
                {
                    SpriteBatch.Begin(SpriteSortMode.Immediate, null, null, null, null, null, MainScaleMatrix);
                    SpriteBatch.Draw(DefaultBackground, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height), Color.White);
                    _textRenderer.DrawText(
                        SpriteBatch,
                        $"Importuji soubor {_fileBeingMigrated} ...",
                        _textPosition,
                        1f,
                        Color.White,
                        Alignment.MiddleCenter);
                    SpriteBatch.End();
                }

                if (CurrentScene == null)
                {
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

