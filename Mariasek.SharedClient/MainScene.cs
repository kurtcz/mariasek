//#define STARTING_PLAYER_1
//#define STARTING_PLAYER_2
//#define STARTING_PLAYER_3
//#define DEBUG_PROGRESS
using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;
using Mariasek.SharedClient.GameComponents;
using Mariasek.Engine.New;

namespace Mariasek.SharedClient
{
    public enum GameState
    {
        NotPlaying,
        ChooseTrump,
        ChooseTalon,
        ChooseGameFlavour,
        ChooseGameType,
        Bid,
        Play,
        RoundFinished,
        GameFinished
    }

    public class MainScene : Scene
    {
        #region Child components
        #pragma warning disable 414

        //TODO:Add child components here
        private GameComponents.Hand _hand;
        private GameComponents.Hand _winningHand;
        private Deck _deck;
        private Sprite[] _cardsPlayed;
        private CardButton[][] _hlasy;
        private CardButton[] _stychy;
        private Sprite[] _stareStychy;
        private ClickableArea _overlay;
        private Button _menuBtn;
        private Button _sendBtn;
        private Button _okBtn;
        private Button[] gtButtons, gfButtons, bidButtons;
        private Button gtHraButton;
        private Button gt7Button;
        private Button gt100Button;
        private Button gt107Button;
        private Button gtBetlButton;
        private Button gtDurchButton;
        private Button gfDobraButton;
        private Button gfSpatnaButton;
        private ToggleButton flekBtn;
        private ToggleButton sedmaBtn;
        private ToggleButton kiloBtn;
        private Button _hintBtn;
        private Label _trumpLabel1, _trumpLabel2, _trumpLabel3;
        private Label[] _trumpLabels;
        private Label _msgLabel;
        private Label _msgLabelLeft;
        private Label _msgLabelRight;
        private ProgressIndicator _progress1, _progress2, _progress3;
        private ProgressIndicator[] _progressBars;
        private TextBox _bubble1,  _bubble2,  _bubble3;
        private ManualResetEvent _bubbleEvent1, _bubbleEvent2, _bubbleEvent3;
        private ManualResetEvent[] _bubbleEvents;

        #pragma warning restore 414
        #endregion

        public Mariasek.Engine.New.Game g;
        private const int _bubbleTime = 1000;
        private Task _gameTask;
        private SynchronizationContext _synchronizationContext;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly AutoResetEvent _evt = new AutoResetEvent(false);
        private bool _canSort;
        private bool _canShowTrumpHint;
        private int _aiMessageIndex;
        public int CurrentStartingPlayerIndex = -1;
        private Mariasek.Engine.New.Configuration.ParameterConfigurationElementCollection _aiConfig;

        private string _historyFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mariasek.history");
        private string _deckFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mariasek.deck");
        private string _savedGameFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SavedGame.hra");
		private string _loadedGameFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SavedGame.hra");
		private string _newGameFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "_temp.hra");
        private string _errorFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "_error.hra");
        private string _endGameFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "_end.hra");

        private Action HintBtnFunc;
        private GameState _state;
        private GameSettings _settings;
        private volatile Bidding _bidding;
        private volatile Hra _gameTypeChosen;
        private volatile GameFlavour _gameFlavourChosen;
        private GameFlavourChosenEventArgs _gameFlavourChosenEventArgs;
        private bool _firstTimeGameFlavourChosen;
        private volatile Hra _bid;
        private volatile Card _cardClicked;
        private volatile Card _trumpCardChosen;
        private volatile List<Card> _talon;

        public MainScene(MariasekMonoGame game)
            : base(game)
        {
            Game.SettingsScene.SettingsChanged += SettingsChanged;
            Game.SaveInstanceState += SaveGame;
        }

        /// <summary>
        /// Allows the game component to perform any initialization it needs to before starting
        /// to run.  This is where it can query for any required services and load content.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            _aiConfig = new Mariasek.Engine.New.Configuration.ParameterConfigurationElementCollection();

            _aiConfig.Add("AiCheating", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "AiCheating",
                    Value = "false"
                });
            _aiConfig.Add("RoundsToCompute", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "RoundsToCompute",
                    Value = "1"
                });
            _aiConfig.Add("CardSelectionStrategy", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "CardSelectionStrategy",
                    Value = "MaxCount"
                });
            _aiConfig.Add("SimulationsPerGameType", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "SimulationsPerGameType",
                    Value = "1000"
                });
            _aiConfig.Add("SimulationsPerGameTypePerSecond", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "SimulationsPerGameTypePerSecond",
                    Value = _settings.GameTypeSimulationsPerSecond.ToString()
                });
            _aiConfig.Add("MaxSimulationTimeMs", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "MaxSimulationTimeMs",
                    Value = _settings.ThinkingTimeMs.ToString()
                });
            _aiConfig.Add("SimulationsPerRound", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "SimulationsPerRound",
                    Value = "1000"
                });
            _aiConfig.Add("SimulationsPerRoundPerSecond", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "SimulationsPerRoundPerSecond",
                    Value = _settings.RoundSimulationsPerSecond.ToString()
                });
            _aiConfig.Add("RuleThreshold", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "RuleThreshold",
                    Value = "90"
                });
            _aiConfig.Add("RuleThreshold.Kilo", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "RuleThreshold.Kilo",
                    Value = "99"
                });
            _aiConfig.Add("GameThreshold", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "GameThreshold",
                    Value = "75|80|85|90|95"
                });
            _aiConfig.Add("GameThreshold.Hra", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "GameThreshold.Hra",
                    Value = "0|50|70|85|95"
                });
            _aiConfig.Add("GameThreshold.Kilo", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "GameThreshold.Kilo",
                    Value = "80|85|90|95|99"
                });
            _aiConfig.Add("GameThreshold.KiloProti", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "GameThreshold.KiloProti",
                    Value = "90|95|97|98|99"
                });
            _aiConfig.Add("GameThreshold.Betl", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "GameThreshold.Betl",
					Value = "65|75|85|90|95"
                });
            _aiConfig.Add("GameThreshold.Durch", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "GameThreshold.Durch",
                    Value = "80|85|90|95|99"
                });
            _aiConfig.Add("MaxDoubleCount", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "MaxDoubleCount",
                    Value = "5"
                });
            _aiConfig.Add("SigmaMultiplier", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "SigmaMultiplier",
                    Value = "0"
                });
            _aiConfig.Add("BaseBet", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "BaseBet",
                    Value = "1"
                });
            
            _synchronizationContext = SynchronizationContext.Current;
            _hlasy = new []
            {
                new []
                {
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 100, Game.VirtualScreenHeight / 2f + 20), IsEnabled = false, Name="Hlasy11", ZIndex = 1 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 150, Game.VirtualScreenHeight / 2f + 20), IsEnabled = false, Name="Hlasy12", ZIndex = 2 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 200, Game.VirtualScreenHeight / 2f + 20), IsEnabled = false, Name="Hlasy13", ZIndex = 3 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 250, Game.VirtualScreenHeight / 2f + 20), IsEnabled = false, Name="Hlasy14", ZIndex = 4 },
                },
                new []
                {
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(100, 130), IsEnabled = false, Name="Hlasy21", ZIndex = 1 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(150, 130), IsEnabled = false, Name="Hlasy22", ZIndex = 2 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(200, 130), IsEnabled = false, Name="Hlasy23", ZIndex = 3 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(250, 130), IsEnabled = false, Name="Hlasy24", ZIndex = 4 },
                },
                new []
                {
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 100, 130), IsEnabled = false, Name="Hlasy31", ZIndex = 1 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 150, 130), IsEnabled = false, Name="Hlasy32", ZIndex = 2 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 200, 130), IsEnabled = false, Name="Hlasy33", ZIndex = 3 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 250, 130), IsEnabled = false, Name="Hlasy34", ZIndex = 4 },
                }
            };
            _stareStychy = new []
            {
                new Sprite(this, Game.ReverseTexture) { Position = new Vector2(Game.VirtualScreenWidth - 50, Game.VirtualScreenHeight / 2f + 50), Scale = Game.CardScaleFactor, Name = "StareStychy1" },
                new Sprite(this, Game.ReverseTexture) { Position = new Vector2(50, 80), Scale = Game.CardScaleFactor, Name = "StareStychy2" },
                new Sprite(this, Game.ReverseTexture) { Position = new Vector2(Game.VirtualScreenWidth - 50, 80), Scale = Game.CardScaleFactor, Name = "StareStychy3" }
            };
            _stychy = new []
            {
                new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor, Name="Stychy1" }) { Position = new Vector2(Game.VirtualScreenWidth - 50, Game.VirtualScreenHeight / 2f + 50), IsEnabled = false, ZIndex = 10 },
                new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor, Name="Stychy2" }) { Position = new Vector2(50, 80), IsEnabled = false, ZIndex = 10 },
                new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor, Name="Stychy3" }) { Position = new Vector2(Game.VirtualScreenWidth - 50, 80), IsEnabled = false, ZIndex = 10 }
            };
            _cardsPlayed = new []
            {
                new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight / 2f - 100), Scale = Game.CardScaleFactor, Name="CardsPlayed1", ZIndex = 10 },
                new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth / 2f - 50, Game.VirtualScreenHeight / 2f - 140), Scale = Game.CardScaleFactor, Name="CardsPlayed2", ZIndex = 10 },
                new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth / 2f + 50, Game.VirtualScreenHeight / 2f - 150), Scale = Game.CardScaleFactor, Name="CardsPlayed3", ZIndex = 10 }
            };
            _overlay = new ClickableArea(this)
            {
                Width = Game.VirtualScreenWidth,
                Height = Game.VirtualScreenHeight,
                IsEnabled = false
            };
            _overlay.TouchUp += OverlayTouchUp;
            _menuBtn = new Button(this)
            {
                Text = "Menu",
                Position = new Vector2(10, Game.VirtualScreenHeight / 2f - 30),
                ZIndex = 100
            };
            _menuBtn.Click += MenuBtnClicked;
            _sendBtn = new Button(this)
            {
                Text = "Odeslat",
                Position = new Vector2(10, Game.VirtualScreenHeight / 2f + 30),
                ZIndex = 100
            };
            _sendBtn.Click += SendBtnClicked;
            _hintBtn = new Button(this)
            {
                Text = "?",
                Position = new Vector2(Game.VirtualScreenWidth - 60, Game.VirtualScreenHeight / 2f - 30),
                Width = 50,
                TextColor = Color.SaddleBrown,
                BackgroundColor = Color.White,
                BorderColor = Color.SaddleBrown,
                ZIndex = 100
            };
            _hintBtn.Click += HintBtnClicked;
            _okBtn = new Button(this)
            {
                Text = "OK",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 50, Game.VirtualScreenHeight / 2f - 100),
                //Position = new Vector2(10, 60),
                IsEnabled = false,
                ZIndex = 100
            };
            _okBtn.Click += OkBtnClicked;
            _okBtn.Hide();
            gtHraButton = new Button(this)
            {
                Text = "Hra",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 325, Game.VirtualScreenHeight / 2f - 100),
                Tag = Hra.Hra,
                ZIndex = 100
            };
            gtHraButton.Click += GtButtonClicked;
            gtHraButton.Hide();
            gt7Button = new Button(this)
            {
                Text = "Sedma",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 215, Game.VirtualScreenHeight / 2f - 100),
                Tag = Hra.Hra | Hra.Sedma,
                ZIndex = 100
            };
            gt7Button.Click += GtButtonClicked;
            gt7Button.Hide();
            gt100Button = new Button(this)
            {
                Text = "Kilo",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 105, Game.VirtualScreenHeight / 2f - 100),
                Tag = Hra.Kilo,
                ZIndex = 100
            };
            gt100Button.Click += GtButtonClicked;
            gt100Button.Hide();
            gt107Button = new Button(this)
            {
                Text = "Stosedm",
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 5, Game.VirtualScreenHeight / 2f - 100),
                Tag = Hra.Kilo | Hra.Sedma,
                ZIndex = 100
            };
            gt107Button.Click += GtButtonClicked;
            gt107Button.Hide();
            gtBetlButton = new Button(this)
            {
                Text = "Betl",
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 115, Game.VirtualScreenHeight / 2f - 100),
                Tag = Hra.Betl,
                ZIndex = 100
            };
            gtBetlButton.Click += GtButtonClicked;
            gtBetlButton.Hide();
            gtDurchButton = new Button(this)
            {
                Text = "Durch",
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 225, Game.VirtualScreenHeight / 2f - 100),
                Tag = Hra.Durch,
                ZIndex = 100
            };
            gtDurchButton.Click += GtButtonClicked;
            gtDurchButton.Hide();
            gtButtons = new[] { gtHraButton, gt7Button, gt100Button, gt107Button, gtBetlButton, gtDurchButton };
            gfDobraButton = new Button(this)
            {
                Text = "Dobrá",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 105, Game.VirtualScreenHeight / 2f - 100),
                Tag = GameFlavour.Good,
                ZIndex = 100
            };
            gfDobraButton.Click += GfButtonClicked;
            gfDobraButton.Hide();
            gfSpatnaButton = new Button(this)
            {
                Text = "Špatná",
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 5, Game.VirtualScreenHeight / 2f - 100),
                Tag = GameFlavour.Bad,
                ZIndex = 100
            };
            gfSpatnaButton.Click += GfButtonClicked;
            gfSpatnaButton.Hide();
            gfButtons = new[] { gfDobraButton, gfSpatnaButton };
            _trumpLabel1 = new Label(this)
            { 
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 125, 1),
                Width = 250,
                Height = 50,
                ZIndex = 100
            };
            _trumpLabel1.Hide();
            _trumpLabel2 = new Label(this)
            { 
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Middle,
                Position = new Vector2(125, 1),
                Width = 250,
                Height = 50,
                ZIndex = 100
            };
            _trumpLabel2.Hide();
            _trumpLabel3 = new Label(this)
            { 
                HorizontalAlign = HorizontalAlignment.Right,
                VerticalAlign = VerticalAlignment.Middle,
                Position = new Vector2(Game.VirtualScreenWidth - 260, 1),
                Width = 250,
                Height = 50,
                ZIndex = 100
            };
            _trumpLabel3.Hide();
            _trumpLabels = new[] { _trumpLabel1, _trumpLabel2, _trumpLabel3 };
            _msgLabel = new Label(this)
            { 
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                Position = new Vector2(10, 60),
                Width = (int)Game.VirtualScreenWidth - 20,
                Height = (int)Game.VirtualScreenHeight - 120,
                TextColor = Color.Yellow,
                TextRenderer = Game.FontRenderers["SegoeUI40Outl"],
                ZIndex = 100
            };
            _msgLabelLeft = new Label(this)
            { 
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Middle,
                Position = new Vector2(120, 20),
                Width = (int)Game.VirtualScreenWidth - 240,
                Height = (int)Game.VirtualScreenHeight - 40,
                TextColor = Color.Yellow,
                TextRenderer = Game.FontRenderers["SegoeUI40Outl"],
                ZIndex = 100
            };
            _msgLabelRight = new Label(this)
            { 
                HorizontalAlign = HorizontalAlignment.Right,
                VerticalAlign = VerticalAlignment.Middle,
                Position = new Vector2(120, 20),
                Width = (int)Game.VirtualScreenWidth - 240,
                Height = (int)Game.VirtualScreenHeight - 40,
                TextColor = Color.Yellow,
                TextRenderer = Game.FontRenderers["SegoeUI40Outl"],
                ZIndex = 100
            };
            _hand = new GameComponents.Hand(this, new Card[0]) { Centre = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight - 60), ZIndex = 50 };
            _hand.Click += CardClicked;
            _hand.ShowArc((float)Math.PI / 2);
            _bubble1 = new TextBox(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 100, Game.VirtualScreenHeight / 2 - 100),
                Width = 200,
                Height = 50,
                BackgroundColor = new Color(0x40, 0x40, 0x40),
                TextColor = Color.Yellow,
                BorderColor = Color.Red,
                Opacity = 0.8f,
                VerticalAlign = VerticalAlignment.Middle,
                HorizontalAlign = HorizontalAlignment.Center,
                ZIndex = 100
            };
            _bubble1.Hide();
            _bubble2 = new TextBox(this)
            {
                Position = new Vector2(50, 80),
                Width = 200,
                Height = 50,
                BackgroundColor = new Color(0x40, 0x40, 0x40),
                TextColor = Color.Yellow,
                BorderColor = Color.Green,
                Opacity = 0.8f,
                VerticalAlign = VerticalAlignment.Middle,
                HorizontalAlign = HorizontalAlignment.Center,
                ZIndex = 100
            };
            _bubble2.Hide();
            _bubble3 = new TextBox(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 250, 80),
                Width = 200,
                Height = 50,
                BackgroundColor = new Color(0x40, 0x40, 0x40),
                TextColor = Color.Yellow,
                BorderColor = Color.Blue,
                Opacity = 0.8f,
                VerticalAlign = VerticalAlignment.Middle,
                HorizontalAlign = HorizontalAlignment.Center,
                ZIndex = 100
            };
            _bubble3.Hide();
            _bubbleEvent1 = new ManualResetEvent(true);
            _bubbleEvent2 = new ManualResetEvent(true);
            _bubbleEvent3 = new ManualResetEvent(true);
            _bubbleEvents = new [] { _bubbleEvent1, _bubbleEvent2, _bubbleEvent3 };
            flekBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 230, Game.VirtualScreenHeight / 2f - 150),
                Width = 150,
                Height = 50,
                Tag = Hra.Hra | Hra.Kilo | Hra.Betl | Hra.Durch,
                ZIndex = 100
            };

            flekBtn.Click += BidButtonClicked;
            flekBtn.Hide();
            sedmaBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 75, Game.VirtualScreenHeight / 2f - 150),
                Width = 150,
                Height = 50,
                Tag = Hra.Sedma | Hra.SedmaProti,
                ZIndex = 100
            };
            sedmaBtn.Click += BidButtonClicked;
            sedmaBtn.Hide();
            kiloBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 80, Game.VirtualScreenHeight / 2f - 150),
                Width = 150,
                Height = 50,
                Tag = Hra.KiloProti,
                ZIndex = 100
            };
            kiloBtn.Click += BidButtonClicked;
            kiloBtn.Hide();
            bidButtons = new [] { flekBtn, sedmaBtn, kiloBtn };
            _progress1 = new ProgressIndicator(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 75, 0),
                Width = 150,
                Height = 8,
                Color = Color.Red,
                ZIndex = 100
            };
            if (!_settings.HintEnabled)
            {
                _progress1.Hide();
            }
            _progress2 = new ProgressIndicator(this)
            {
                Position = new Vector2(0, 0),
                Width = 150,
                Height = 8,
                Color = Color.Green,
                ZIndex = 100
            };
            _progress3 = new ProgressIndicator(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 150, 0),
                Width = 150,
                Height = 8,
                Color = Color.Blue,
                ZIndex = 100
            };
            _progressBars = new [] { _progress1, _progress2, _progress3 };

            LoadHistory();
            Game.SettingsScene.LoadGameSettings(false);

            Background = Game.Content.Load<Texture2D>("wood2");
            ClearTable(true);
        }

        private static Stream GetFileStream(string filename)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), filename);

            return new FileStream(path, FileMode.Create);
        }
			
        public void LoadHistory()
        {
            try
            {
				var xml = new XmlSerializer(typeof(List<MoneyCalculatorBase>));

				using (var fs = File.Open(_historyFilePath, FileMode.Open))
                {
                    Game.Money = (List<MoneyCalculatorBase>)xml.Deserialize(fs);
                }
            }
            catch(Exception e)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Cannot load history\n{0}", e.Message));
            }
        }

        public void SaveHistory()
        {
            try
            {
				var xml = new XmlSerializer(typeof(List<MoneyCalculatorBase>));

				using (var fs = File.Open(_historyFilePath, FileMode.Create))
                {
                    xml.Serialize(fs, Game.Money);
                }
            }
            catch(Exception e)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Cannot save history\n{0}", e.Message));
            }
        }

        public void LoadDeck()
        {
            _deck = new Deck();
            try
            {
                using (var fs = File.Open(_deckFilePath, FileMode.Open))
                {
                    _deck.LoadDeck(fs);
                }
            }
            catch(Exception e)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Cannot load deck\n{0}", e.Message));
                _deck.Shuffle();
            }
        }

        public void SaveDeck()
        {
            try
            {
                using (var fs = File.Open(_deckFilePath, FileMode.Create))
                {
                    _deck.SaveDeck(fs);
                }
            }
            catch(Exception e)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Cannot save deck\n{0}", e.Message));
            }
        }

        private void CancelRunningTask()
        {
            if (_gameTask != null && _gameTask.Status == TaskStatus.Running)
            {
                _synchronizationContext.Send(_ => ClearTable(true), null);
                (g.players[0] as HumanPlayer).CancelAiTask();
                try
                {
                    _cancellationTokenSource.Cancel();
                    _evt.Set();
                    _gameTask.Wait();
                }
                catch (Exception e)
                {
                    //exception caught during task cancellation
                }
            }
            _cancellationTokenSource = new CancellationTokenSource();
        }

        private void ShowBidButtons(Bidding bidding)
        {
            _bidding = bidding;

            var bids = _bidding.Bids;

            flekBtn.IsEnabled = (bids & (Hra.Hra | Hra.Kilo | Hra.Betl | Hra.Durch)) != 0;
            sedmaBtn.IsEnabled = (bids & (Hra.Sedma | Hra.SedmaProti)) != 0;
            kiloBtn.IsEnabled = (bids & Hra.KiloProti) != 0;

            flekBtn.Text = Bidding.MultiplierToString(bidding.GameMultiplier * 2);
            sedmaBtn.Text = (bids & Hra.SedmaProti) != 0 && (_bidding.SevenAgainstMultiplier == 0)? "Sedma proti" : "Na sedmu";
            kiloBtn.Text = (bids & Hra.KiloProti) != 0 && (_bidding.HundredAgainstMultiplier == 0) ? "Kilo proti" : "Na kilo";

            flekBtn.Show();
            sedmaBtn.Show();
            kiloBtn.Show();
        }

        private void HideBidButtons()
        {
            flekBtn.Hide();
            sedmaBtn.Hide();
            kiloBtn.Hide();
        }

        public void NewGameBtnClicked(object sender)
        {
            CancelRunningTask();
            _gameTask = Task.Run(() => {
                if (File.Exists(_errorFilePath))
                {
                    File.Delete(_errorFilePath);
                }
                g = new Mariasek.Engine.New.Game()
                {
                    SkipBidding = false,
                    BaseBet = _settings.BaseBet,
                    GetFileStream = GetFileStream,
					GetVersion = () => MariasekMonoGame.Version
                };
                g.RegisterPlayers(
                    new HumanPlayer(g, _aiConfig, this, _settings.HintEnabled) { Name = "Hráč 1" },
                    new AiPlayer(g, _aiConfig) { Name = "Hráč 2" },
                    new AiPlayer(g, _aiConfig) { Name = "Hráč 3" }
                );
                CurrentStartingPlayerIndex = _settings.CurrentStartingPlayerIndex; //TODO: zrusit CurrentStartingPlayerIndex a pouzivat jen _settings.CurrentStartingPlayerIndex
                CurrentStartingPlayerIndex = (CurrentStartingPlayerIndex + 1) % Mariasek.Engine.New.Game.NumPlayers;
#if STARTING_PLAYER_1
                CurrentStartingPlayerIndex = 0;
#elif STARTING_PLAYER_2
                CurrentStartingPlayerIndex = 1;
#elif STARTING_PLAYER_3
                CurrentStartingPlayerIndex = 2;
#endif
                _settings.CurrentStartingPlayerIndex = CurrentStartingPlayerIndex;
                Game.SettingsScene.SaveGameSettings();
                if (_deck == null)
                {
                    LoadDeck();
                }
                else if (_deck.IsEmpty())
                {
                    _deck = g.GetDeckFromLastGame();
                    if(_deck.IsEmpty())
                    {
                        LoadDeck();
                    }
                }
                if (Game.MenuScene.ShuffleBtn.IsSelected)
                {
                    _deck.Shuffle();
                    Game.MenuScene.ShuffleBtn.IsSelected = false;
                }
                _canSort = CurrentStartingPlayerIndex != 0;

                g.NewGame(CurrentStartingPlayerIndex, _deck);
                g.GameFlavourChosen += GameFlavourChosen;
                g.GameTypeChosen += GameTypeChosen;
                g.BidMade += BidMade;
                g.CardPlayed += CardPlayed;
                g.RoundStarted += RoundStarted;
                g.RoundFinished += RoundFinished;
                g.GameFinished += GameFinished;
                g.GameWonPrematurely += GameWonPrematurely;
                g.GameException += GameException;
                g.players[1].GameComputationProgress += GameComputationProgress;
                g.players[2].GameComputationProgress += GameComputationProgress;
                _firstTimeGameFlavourChosen = true;
                _trumpCardChosen = null;

                _state = GameState.NotPlaying;

                BackgroundTint = Color.White;
                ClearTable(true);
                HideMsgLabel();
                foreach (var btn in gtButtons)
                {
                    btn.Hide();
                }
                foreach(var btn in gfButtons)
                {
                    btn.Hide();
                }
                foreach(var btn in bidButtons)
                {
                    btn.Hide();
                }
                _hand.ClearOperations();
                _hintBtn.IsEnabled = false;
                if(_settings.HintEnabled)
                {
                    _hintBtn.Show();
                }
                else
                {
                    _hintBtn.Hide();
                }
                if (g.GameStartingPlayerIndex != 0)
                {
                    g.players[0].Hand.Sort(_settings.SortMode == SortMode.Ascending, false);
                    ShowThinkingMessage();
                    _hand.Show();
                    UpdateHand();
                }
                else
                {
                    _hand.Hide();
                    _canShowTrumpHint = false;
                }
                g.PlayGame(_cancellationTokenSource.Token);
            },  _cancellationTokenSource.Token);
        }
            
        private void GameWonPrematurely (object sender, GameWonPrematurelyEventArgs e)
        {
            g.ThrowIfCancellationRequested();
            _synchronizationContext.Send(_ =>
                {
                    if((g.GameType & Hra.Betl) != 0)
                    {
                        if(e.roundNumber > 1)
                        {
                            ShowMsgLabel("Už mě nechytíte", false);
                        }
                        else
                        {
                            ShowMsgLabel("Mám to ložený", false);
                        }
                    }
                    else
                    {
                        var finalTrumpSeven = g.trump.HasValue && e.winningHand.Any(i => i.Suit == g.trump.Value && i.Value == Hodnota.Sedma);
                        if(e.roundNumber > 1)
                        {
                            ShowMsgLabel(string.Format("Zbytek jde za mnou{0}", finalTrumpSeven ? ", sedma nakonec" : string.Empty), false);
                        }
                        else
                        {
                            ShowMsgLabel(string.Format("Mám to ložený{0}", finalTrumpSeven ? ", sedma nakonec" : string.Empty), false);
                        }
                    }
                    ShowInvisibleClickableOverlay();
                    _hand.Hide();
                    _winningHand = new GameComponents.Hand(this, e.winningHand.Sort(false, (g.GameType & (Hra.Betl | Hra.Durch)) != 0, g.trump).ToArray());
                    _winningHand.ShowWinningHand(e.winner.PlayerIndex);
                    _winningHand.Show();
                    _state = GameState.RoundFinished;
                }, null);
            WaitForUIThread();
            ClearTable(true);
        }

        public void GameException (object sender, GameExceptionEventArgs e)
        {
            var ex = e.e;
            var ae = ex as AggregateException;

            if (ae != null)
            {
                ae = ae.Flatten();
                if (ae.InnerExceptions.Count > 0)
                {
                    ex = ae.InnerExceptions[0];
                }
            }
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }
            if (ex is OperationCanceledException)
            {
                return;
            }                

			var msg1 = string.Format("Chyba:\n{0}\nOdesílám zprávu...", ex.Message.Split('\n').First());
			var msg2 = string.Format("{0}\n{1}", ex.Message, ex.StackTrace);

            ShowMsgLabel(msg1, false);
            if (Game.EmailSender != null)
            {
                Game.EmailSender.SendEmail(new[] { "tnemec78@gmail.com" }, "Mariasek crash report", msg2, new[] { _newGameFilePath, _errorFilePath });
            }
        }

        public void GameComputationProgress(object sender, GameComputationProgressEventArgs e)
        {
            var player = sender as AbstractPlayer;

            _progressBars[player.PlayerIndex].Progress = e.Current;
            _progressBars[player.PlayerIndex].Max = e.Max;
#if DEBUG_PROGRESS
            if (!string.IsNullOrEmpty(e.Message))
            {
                ShowMsgLabel(string.Format("{0}/{1} {2}", e.Current, e.Max > 0 ? e.Max.ToString() : "?", e.Message), false);
            }
#endif
        }

        public void MenuBtnClicked(object sender)
        {
            Game.MenuScene.SetActive();
        }

        public void SendBtnClicked(object sender)
        {
            if (Game.EmailSender != null && g != null)
            {
                if (g.IsRunning)
                {
                    using (var fs = GetFileStream(Path.GetFileName(_savedGameFilePath)))
                    {
                        g.SaveGame(fs, saveDebugInfo: true);
                    }
                    Game.EmailSender.SendEmail(new[] { "tnemec78@gmail.com" }, "Mariasek game feedback", "", new[] { _newGameFilePath, _savedGameFilePath });
                }
                else
                {
                    Game.EmailSender.SendEmail(new[] { "tnemec78@gmail.com" }, "Mariasek game feedback", "", new[] { _newGameFilePath, _endGameFilePath });
                }
            }
        }

        public void HintBtnClicked(object sender)
        {
            HintBtnFunc();
            _hintBtn.IsEnabled = false;
        }

        public void CardClicked(object sender)
        {
            var button = sender as CardButton;
            var origZIndex = button.ZIndex;
            Sprite targetSprite;

            _cardClicked = (Card)button.Tag;
            System.Diagnostics.Debug.WriteLine(string.Format("{0} clicked", _cardClicked));
            switch (_state)
            {
                case GameState.ChooseTalon:                    
                    if (button.IsFaceUp && _talon.Count == 2)
                    {
                        //do talonu nemuzu pridat kdyz je plnej
                        return;
                    }
                    if (button.IsFaceUp)
                    {
                        //selected
                        button.FlipToBack();
                        _talon.Add(_cardClicked);
                    }
                    else
                    {
                        //unselected
                        button.FlipToFront();
                        _talon.Remove(_cardClicked);
                    }
                    _okBtn.IsEnabled = _talon.Count == 2;
                    if (_talon.Any(i => !g.IsValidTalonCard(i)))
                    {
                        ShowMsgLabel("Vyber si talon\nS tímto talonem musíš hrát betl nebo durch", false);                        
                    }
                    else
                    {
                        ShowMsgLabel("Vyber si talon", false);                        
                    }
                    break;
                case GameState.ChooseTrump:
                    if (_cardClicked == null)
                        return;
                    _trumpCardChosen = _cardClicked;
                    _state = GameState.NotPlaying;
                    HideMsgLabel();
                    button.IsSelected = false; //aby karta nebyla pri animaci tmava
                    var origPosition = _hlasy[0][0].Position;
                    _hlasy[0][0].Position = button.Position;
                    if (!button.Sprite.IsVisible)
                    {
                        button
                            .FlipToFront()
                            .Wait(1000);
                    }
                    button
                        .FlipToBack()
                        .Invoke(() =>
                        {
                            _hlasy[0][0].ShowBackSide();
                            button.Hide();
                            _hlasy[0][0]
                                .MoveTo(origPosition, 1000)
                                .Invoke(() =>
                                {
                                    _evt.Set();
                                });
                        });
                    _hand.IsEnabled = false;
                    break;
                //case GameState.ChooseGameType:
                case GameState.Play:
                    if (_cardClicked == null)
                        return;
                    _state = GameState.NotPlaying;
                    HideMsgLabel();
                    if (_cardClicked.Value == Hodnota.Svrsek && g.players[0].Hand.HasK(_cardClicked.Suit))
                    {
                        targetSprite = _hlasy[0][g.players[0].Hlasy].Sprite;
                        targetSprite.ZIndex = _hlasy[0][g.players[0].Hlasy].ZIndex;
                    }
                    else
                    {
                        targetSprite = _cardsPlayed[0];
                        targetSprite.ZIndex = _cardsPlayed[0].ZIndex;
                    }
                    origPosition = targetSprite.Position;
                    targetSprite.Position = button.Position;
                    targetSprite.SpriteRectangle = _cardClicked.ToTextureRect();
                    targetSprite.Show();
                    button.Hide();
                    targetSprite
                        .MoveTo(origPosition, 1000)
                        .Invoke(() =>
                        {
                            _evt.Set();
                        });
                    _hand.IsEnabled = false;
                    break;
                case GameState.RoundFinished:
                    _state = GameState.NotPlaying;
                    ClearTable();
                    HideMsgLabel();
                    _hand.IsEnabled = false;
                    _evt.Set();
                    break;
                case GameState.GameFinished:
                    _state = GameState.NotPlaying;
                    ClearTable(true);
                    HideMsgLabel();
                    _hand.IsEnabled = false;
                    NewGameBtnClicked(this);
                    return;
                default:
                    return;
            }
        }

        public void OkBtnClicked(object sender)
        {
            HideMsgLabel();
            HideBidButtons();
            _okBtn.Hide();
            if (_state == GameState.Bid)
            {
                flekBtn.IsSelected = false;
                sedmaBtn.IsSelected = false;
                kiloBtn.IsSelected = false;
            }
            _state = GameState.NotPlaying;
            _evt.Set();
        }

        public void GtButtonClicked(object sender)
        {
            HideMsgLabel();
            foreach (var btn in gtButtons)
            {
                btn.Hide();
            }
            _okBtn.Hide();
            _state = GameState.NotPlaying;
            _gameTypeChosen = (Hra)(sender as Button).Tag;
            _evt.Set();
        }

        public void GfButtonClicked(object sender)
        {
            foreach (var btn in gfButtons)
            {
                btn.Hide();
            }
            _state = GameState.NotPlaying;
            _gameFlavourChosen = (GameFlavour)(sender as Button).Tag;
            _evt.Set();
        }

        public void BidButtonClicked(object sender)
        {
            if (sender == flekBtn && flekBtn.IsSelected)
            {
                kiloBtn.IsSelected = false;
            }
            else if (sender == kiloBtn && kiloBtn.IsSelected)
            {
                flekBtn.IsSelected = false;
            }
            _bid = _bidding.Bids &
                ((flekBtn.IsSelected
                    ? (Hra)flekBtn.Tag
                    : 0) |
                    (sedmaBtn.IsSelected
                        ? (Hra)sedmaBtn.Tag
                        : 0) |
                    (kiloBtn.IsSelected
                        ? (Hra)kiloBtn.Tag
                        : 0));
        }

        #region HumanPlayer methods

        public Card ChooseTrump()
        {
            g.ThrowIfCancellationRequested();
            _hand.IsEnabled = true;
            _hintBtn.IsEnabled = false;
            _synchronizationContext.Send(_ =>
                {
                    ShowMsgLabel("Vyber trumfovou kartu", false);
                    _state = GameState.ChooseTrump;
                    _hand.Show();
                    UpdateHand(flipCardsUp: true, cardsNotRevealed: 5);
                }, null);
            WaitForUIThread();
            _hintBtn.IsEnabled = false;
            return _cardClicked;
        }

        public List<Card> ChooseTalon()
        {
            g.ThrowIfCancellationRequested();
            _hand.IsEnabled = true;
            _hintBtn.IsEnabled = false;
            _synchronizationContext.Send(_ =>
                {
                    _talon = new List<Card>();
                    ShowMsgLabel("Vyber si talon", false);
                    _okBtn.Show();
                    _okBtn.IsEnabled = false;
                    _state = GameState.ChooseTalon;
                    UpdateHand(cardToHide: _trumpCardChosen);
                }, null);
            WaitForUIThread();
            _hintBtn.IsEnabled = false;
            return _talon;
        }

        public GameFlavour ChooseGameFlavour()
        {
            g.ThrowIfCancellationRequested();
            _hand.IsEnabled = false;
            _hintBtn.IsEnabled = false;
            _synchronizationContext.Send(_ =>
            {
                UpdateHand(); //abych nevidel karty co jsem hodil do talonu                
                foreach (var gfButton in gfButtons)
                {
                        gfButton.Show();
                }
                if (!_settings.HintEnabled || !_msgLabel.IsVisible) //abych neprepsal napovedu
                {
                    ShowMsgLabel("Co řekneš?", false);
                }
                _state = GameState.ChooseGameFlavour;
            }, null);
            WaitForUIThread();
            _hintBtn.IsEnabled = false;
            return _gameFlavourChosen;
        }

        private void ChooseGameTypeInternal(Hra validGameTypes)
        {
            g.ThrowIfCancellationRequested();
            UpdateHand(cardToHide: _trumpCardChosen); //abych nevidel karty co jsem hodil do talonu
            foreach (var gtButton in gtButtons)
            {
                gtButton.IsEnabled = ((Hra)gtButton.Tag & validGameTypes) == (Hra)gtButton.Tag;
                gtButton.Show();
            }
            if (!_settings.HintEnabled || !_msgLabel.IsVisible) //abych neprepsal napovedu
            {
                ShowMsgLabel("Co budeš hrát?", false);
            }
            _state = GameState.ChooseGameType;
        }

        public Hra ChooseGameType(Hra validGameTypes)
        {
            g.ThrowIfCancellationRequested();
            _hand.IsEnabled = false;
            _hintBtn.IsEnabled = false;
            _synchronizationContext.Send(_ =>
                {
                    ChooseGameTypeInternal(validGameTypes);
                }, null);
            WaitForUIThread();
            WaitHandle.WaitAll(_bubbleEvents);
            _synchronizationContext.Send(_ =>
                {
                    ShowThinkingMessage();
                }, null);
            _hintBtn.IsEnabled = false;
            g.ThrowIfCancellationRequested();
            return _gameTypeChosen;
        }

        public Hra GetBidsAndDoubles(Bidding bidding)
        {
            g.ThrowIfCancellationRequested();
            _hand.IsEnabled = false;
            WaitHandle.WaitAll(_bubbleEvents);
            _synchronizationContext.Send(_ =>
                {
                    UpdateHand();
                }, null);
            _hand.AnimationEvent.Wait();
            _synchronizationContext.Send(_ =>
                {
                    _state = GameState.Bid;
                    _bid = 0;
                    ShowBidButtons(bidding);
                    _okBtn.IsEnabled = true;
                    _okBtn.Show();
                }, null);
            WaitForUIThread();
            return _bid;
        }

        public Card PlayCard(Renonc validationState)
        {
            g.ThrowIfCancellationRequested();
            _hand.IsEnabled = true;
            _hintBtn.IsEnabled = false;
            _synchronizationContext.Send(_ =>
                {
                    _state = GameState.Play;
                    switch (validationState)
                    {
                        case Renonc.Ok:
                            ShowMsgLabel("Hraj", false);
                            break;
                        case Renonc.PriznejBarvu:
                            ShowMsgLabel("Musíš přiznat barvu", false);
                            break;
                        case Renonc.JdiVejs:
                            ShowMsgLabel("Musíš jít vejš", false);
                            break;
                        case Renonc.HrajTrumf:
                            ShowMsgLabel("Musíš hrát trumf", false);
                            break;
                        case Renonc.HrajSvrska:
                            ShowMsgLabel("Hraj svrška místo krále", false);
                            break;
                        case Renonc.NehrajSedmu:
                            ShowMsgLabel("Trumfovou sedmu musíš hrát nakonec", false);
                            break;
                    }
                    _cardsPlayed[0].Hide();
                    UpdateHand();
                }, null);
            WaitForUIThread();
            _hintBtn.IsEnabled = false;
            return _cardClicked;
        }

        #endregion

        #region Game event handlers

        public void GameFlavourChosen(object sender, GameFlavourChosenEventArgs e)
        {
            g.ThrowIfCancellationRequested();

            _gameFlavourChosenEventArgs = e;
            if(_firstTimeGameFlavourChosen)
            {                
                if (g.GameStartingPlayerIndex != 0)
                {
                    _progressBars[g.GameStartingPlayerIndex].Progress = _progressBars[g.GameStartingPlayerIndex].Max;
                    if (_gameFlavourChosenEventArgs.Flavour == GameFlavour.Good)
                    {
                        ShowBubble(_gameFlavourChosenEventArgs.Player.PlayerIndex, "Barva?");
                    }
                    else
                    {
                        //protihrac z ruky zavolil betl nebo durch
                        //var str = _gameFlavourChosenEventArgs.Flavour == GameFlavour.Good ? "Dobrá" : "Špatná";
                        //ShowBubble(_gameFlavourChosenEventArgs.Player.PlayerIndex, str);
                    }
                }
                else
                {
                    if (_gameFlavourChosenEventArgs.Flavour == GameFlavour.Good)
                    {
                        ShowBubble(_gameFlavourChosenEventArgs.Player.PlayerIndex, "Barva?");
                    }
                    else
                    {
                        _trumpCardChosen = null;
                    }
                }
            }
            else if (_gameFlavourChosenEventArgs.Flavour == GameFlavour.Bad || g.GameType == 0)
            {
                var str = _gameFlavourChosenEventArgs.Flavour == GameFlavour.Good ? "Dobrá" : "Špatná";
                ShowBubble(_gameFlavourChosenEventArgs.Player.PlayerIndex, str);
            }
            _synchronizationContext.Send(_ =>
                {
                    if(e.Player.PlayerIndex == 2)
                    {
                        HideMsgLabel();
                    }
                    else if(e.Player.PlayerIndex == 1)
                    {
                        ShowThinkingMessage();
                    }
                    UpdateHand(cardToHide: _trumpCardChosen);
                }, null);

            _firstTimeGameFlavourChosen = false;
        }

        public void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            g.ThrowIfCancellationRequested();
            _synchronizationContext.Send(_ =>
                {
                    var imgs = new []
                        {
                            _hlasy[0][0], _hlasy[1][0], _hlasy[2][0]
                        };
                    _trumpLabels[e.GameStartingPlayerIndex].Text = g.GameType.ToDescription(g.trump);
                    foreach(var trumpLabel in _trumpLabels)
                    {
                        trumpLabel.Hide();
                    }
                    _trumpLabels[e.GameStartingPlayerIndex].Show();
                    if(e.TrumpCard != null)
                    {
                        //imgs[e.GameStartingPlayerIndex].Texture = Game.CardTextures;
                        imgs[e.GameStartingPlayerIndex].Sprite.SpriteRectangle = e.TrumpCard.ToTextureRect();
                        imgs[e.GameStartingPlayerIndex].ShowBackSide();
                        imgs[e.GameStartingPlayerIndex].FlipToFront()
                                                       .Wait(2000)
                            .Invoke(() => {
                                imgs[e.GameStartingPlayerIndex].Hide();
                                UpdateHand();
                            });
                    }
                    else if(e.GameStartingPlayerIndex == 0)
                    {
                        imgs[e.GameStartingPlayerIndex].Hide();
                    }
                    if(e.GameStartingPlayerIndex != 0)
                    {
                        _progressBars[e.GameStartingPlayerIndex].Progress = _progressBars[e.GameStartingPlayerIndex].Max;                        
                    }
                    if(e.GameStartingPlayerIndex != 2)
                    {
                        ShowThinkingMessage();
                    }
                    else
                    {
                        HideMsgLabel();
                    }
                }, null);
        }

        public void BidMade(object sender, BidEventArgs e)
        {
            if(e.Player.PlayerIndex != 0)
            {
                _progressBars[e.Player.PlayerIndex].Progress = _progressBars[e.Player.PlayerIndex].Max;
            }
            ShowBubble(e.Player.PlayerIndex, e.Description);
            if(e.Player.PlayerIndex != 2)
            {
                ShowThinkingMessage();
            }
            else
            {
                HideMsgLabel();
            }
        }

        public void CardPlayed(object sender, Round r)
        {
            if (g.CurrentRound == null || !g.IsRunning) //pokud se vubec nehralo (lozena hra) nebo je lozeny zbytek hry
            {
                return;
            }
            _synchronizationContext.Send(_ =>
                {
                    Card lastCard;
                    AbstractPlayer lastPlayer;
                    bool lastHlas;
                    Rectangle rect = default(Rectangle);

                    if (g.CurrentRound.c3 != null)
                    {
                        lastCard = g.CurrentRound.c3;
                        lastPlayer = g.CurrentRound.player3;
                        lastHlas = g.CurrentRound.hlas3;
                    }
                    else if (g.CurrentRound.c2 != null)
                    {
                        lastCard = g.CurrentRound.c2;
                        lastPlayer = g.CurrentRound.player2;
                        lastHlas = g.CurrentRound.hlas2;
                    }
                    else
                    {
                        lastCard = g.CurrentRound.c1;
                        lastPlayer = g.CurrentRound.player1;
                        lastHlas = g.CurrentRound.hlas1;
                    }
                    _progressBars[lastPlayer.PlayerIndex].Progress = _progressBars[lastPlayer.PlayerIndex].Max;
                    rect = lastCard.ToTextureRect();
                    if (lastHlas)
                    {
                        _hlasy[lastPlayer.PlayerIndex][lastPlayer.Hlasy - 1].Sprite.SpriteRectangle = rect;
                        _hlasy[lastPlayer.PlayerIndex][lastPlayer.Hlasy - 1].Show();
                    }
                    else
                    {
                        _cardsPlayed[lastPlayer.PlayerIndex].SpriteRectangle = rect;
                        _cardsPlayed[lastPlayer.PlayerIndex].Show();
                    }

                    _hand.DeselectAllCards();
                    _hand.ShowArc((float)Math.PI / 2);
                }, null);
        }

        public void RoundStarted(object sender, Round r)
        {
            if (r.player1.PlayerIndex != 0)
            {
                ShowThinkingMessage();
            }
            // 3 * (r-1) + i
            _cardsPlayed[r.player1.PlayerIndex].ZIndex = (r.number - 1) * 3 + 1;
            _cardsPlayed[r.player2.PlayerIndex].ZIndex = (r.number - 1) * 3 + 2;
            _cardsPlayed[r.player3.PlayerIndex].ZIndex = (r.number - 1) * 3 + 3;
            // 3 * (r-1) + i
            _hlasy[r.player1.PlayerIndex][r.player1.Hlasy].ZIndex = (r.number - 1) * 3 + 1;
            _hlasy[r.player2.PlayerIndex][r.player2.Hlasy].ZIndex = (r.number - 1) * 3 + 2;
            _hlasy[r.player3.PlayerIndex][r.player3.Hlasy].ZIndex = (r.number - 1) * 3 + 3;
            // 3 * (r-1) + i
            _stychy[r.player1.PlayerIndex].ZIndex = (r.number - 1) * 3 + 1;
            _stychy[r.player2.PlayerIndex].ZIndex = (r.number - 1) * 3 + 2;
            _stychy[r.player3.PlayerIndex].ZIndex = (r.number - 1) * 3 + 3;
        }

        public void RoundFinished(object sender, Round r)
        {
            if (r.number <= 10)
            {
                g.ThrowIfCancellationRequested();
                _synchronizationContext.Send(_ =>
                    {
                        ShowMsgLabel("Klikni kamkoli", false);
                        ShowInvisibleClickableOverlay();
                        _state = GameState.RoundFinished;
                    }, null);
                WaitForUIThread();
            }
        }

        public void GameFinished(object sender, MoneyCalculatorBase results)
        {
            _state = GameState.GameFinished;

            Game.Money.Add(results);
            SaveHistory();

            BackgroundTint = Color.DimGray;
            ClearTable(true);
            _hand.UpdateHand(new Card[0]);
            _hintBtn.IsEnabled = false;
            //multi-line string needs to be split into two strings separated by a tab on each line
            var leftMessage = new StringBuilder();
            var rightMessage = new StringBuilder();

            foreach (var line in g.Results.ToString().Split('\n'))
            {
                var tokens = line.Split('\t');
                if (tokens.Length > 0)
                {
                    leftMessage.Append(tokens[0]);
                }
                if (tokens.Length > 1)
                {
                    rightMessage.Append(tokens[1]);
                }
                leftMessage.Append("\n");
                rightMessage.Append("\n");
            }
            HideInvisibleClickableOverlay();
            ShowMsgLabelLeftRight(leftMessage.ToString(), rightMessage.ToString());

            _deck = g.GetDeckFromLastGame();
            SaveDeck();

            var value = (int)g.players.Where(i => i is AiPlayer).Average(i => (i as AiPlayer).Settings.SimulationsPerGameTypePerSecond);
            if (value > 0)
            {
                _settings.GameTypeSimulationsPerSecond = value;
            }
            value = (int)g.players.Where(i => i is AiPlayer).Average(i => (i as AiPlayer).Settings.SimulationsPerRoundPerSecond);
            if (value > 0)
            {
                _settings.RoundSimulationsPerSecond = value;
            }
            //_aiConfig["SimulationsPerGameTypePerSecond"].Value = _settings.GameTypeSimulationsPerSecond.ToString();
            //_aiConfig["SimulationsPerRoundPerSecond"].Value = _settings.RoundSimulationsPerSecond.ToString();
            _settings.CurrentStartingPlayerIndex = CurrentStartingPlayerIndex;
            Game.SettingsScene.UpdateSettings(_settings);

            if(results.MoneyWon[0] >= 4 || (g.GameStartingPlayerIndex != 0 && results.MoneyWon[0] >= 2))
            {
                Game.ClapSound.Play();
            }
            else if (results.MoneyWon[0] <= -10 || (g.GameStartingPlayerIndex != 0 && results.MoneyWon[0] <= -5))
            {
                Game.BooSound.Play();
            }
            else
            {
                Game.CoughSound.Play();
            }
        }

        #endregion

        public void LoadGame()
        {
            if (File.Exists(_savedGameFilePath) && g == null)
            {
                SetActive();
                _cancellationTokenSource = new CancellationTokenSource();
                _gameTask = Task.Run(() => 
                {
                    g = new Mariasek.Engine.New.Game()
                        {
                            SkipBidding = false,
                            BaseBet = _settings.BaseBet
                        };
                    g.RegisterPlayers(
                        new HumanPlayer(g, _aiConfig, this, _settings.HintEnabled) { Name = "Hráč 1" },
                        new AiPlayer(g, _aiConfig) { Name = "Hráč 2" },
                        new AiPlayer(g, _aiConfig) { Name = "Hráč 3" }
                    );

                    using (var fs = File.Open(_savedGameFilePath, FileMode.Open))
                    {                            
                        g.LoadGame(fs);
                    }
                    g.GameFlavourChosen += GameFlavourChosen;
                    g.GameTypeChosen += GameTypeChosen;
                    g.BidMade += BidMade;
                    g.CardPlayed += CardPlayed;
                    g.RoundStarted += RoundStarted;
                    g.RoundFinished += RoundFinished;
                    g.GameFinished += GameFinished;
                    g.GameWonPrematurely += GameWonPrematurely;
                    g.GameException += GameException;
                    g.players[1].GameComputationProgress += GameComputationProgress;
                    g.players[2].GameComputationProgress += GameComputationProgress;
                    _firstTimeGameFlavourChosen = true;
                    _trumpCardChosen = null;

                    _state = GameState.NotPlaying;

                    CurrentStartingPlayerIndex = g.GameStartingPlayerIndex;
                    _settings.CurrentStartingPlayerIndex = CurrentStartingPlayerIndex;
                    Game.SettingsScene.SaveGameSettings();
                    _canSort = CurrentStartingPlayerIndex != 0;

                    ClearTable(true);
                    HideMsgLabel();
                    foreach (var btn in gtButtons)
                    {
                        btn.Hide();
                    }
                    foreach(var btn in gfButtons)
                    {
                        btn.Hide();
                    }
                    foreach(var btn in bidButtons)
                    {
                        btn.Hide();
                    }
                    if(g.CurrentRound != null)
                    {
                        _trumpLabels[g.GameStartingPlayerIndex].Text = g.GameType.ToDescription(g.trump);
                    }
                    if(g.GameStartingPlayerIndex != 0)
                    {
                        g.players[0].Hand.Sort(_settings.SortMode == SortMode.Ascending, false);
                        ShowThinkingMessage();
                        _hand.Show();
                        UpdateHand();
                    }
                    else
                    {
                        _hand.Hide();
                        _canShowTrumpHint = false;
                    }
                    File.Delete(_savedGameFilePath);
                    g.PlayGame(_cancellationTokenSource.Token);
                },  _cancellationTokenSource.Token);
            }
        }

        public void SaveGame()
        {
            if (g != null && g.IsRunning)
            {
                try
                {
                    using (var fs = File.Open(_savedGameFilePath, FileMode.Create))
                    {
                        g.SaveGame(fs);
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Cannot save game\n{0}", e.Message));
                }
            }
        }

        public void UpdateHand(bool flipCardsUp = false, int cardsNotRevealed = 0, Card cardToHide = null)
        {
            _hand.UpdateHand(g.players[0].Hand.ToArray(), flipCardsUp ? g.players[0].Hand.Count : 0, cardToHide);
            _hand.ShowStraight((int)Game.VirtualScreenWidth - 20);
            if(flipCardsUp)
            {
                 _hand.WaitUntil(() => !_hand.SpritesBusy)
                      .Invoke(() => 
                        {
                            _hand.UpdateHand(g.players[0].Hand.ToArray(), cardsNotRevealed, cardToHide);
                        });
            }
            if(_state == GameState.ChooseTalon)
            {
                _hand.WaitUntil(() => !_hand.SpritesBusy)
                     .Invoke(() =>
                        {
                            _canSort = true;
                            SortHand(cardToHide);
                        });
            }
        }

        public void SettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            _settings = e.Settings;
            if (_progress1 != null)
            {
                if (_settings.HintEnabled)
                {
                    _progress1.Show();
                }
                else
                {
                    _progress1.Hide();
                }
            }
            if (_hintBtn != null)
            {
                if (_settings.HintEnabled)
                {
                    _hintBtn.Show();
                }
                else
                {
                    _hintBtn.Hide();
                }
            }
            SortHand(null);
            SoundEffect.MasterVolume = _settings.SoundEnabled ? 1f : 0f;
        }

        public void SuggestTrump(Card trumpCard)
        {
            if (_settings.HintEnabled)
            {
                _hintBtn.IsEnabled = true;
				HintBtnFunc = () =>
				{
					_hand.HighlightCard(trumpCard);
					ShowMsgLabel(trumpCard.ToString(), false); //Docasne, zjistit kdy radi kartu kterou jsem nemohl videt
				}; 
			}
        }

        public void SuggestTalon(List<Card> talon)
        {
            if (_settings.HintEnabled)
            {
                _hintBtn.IsEnabled = true;
                HintBtnFunc = () =>
                {
                    _hand.HighlightCard(talon[0]);
                    _hand.HighlightCard(talon[1]);
                };
            }
        }

        public void SuggestGameFlavour(string flavour)
        {
            if (_settings.HintEnabled)
            {
                _hintBtn.IsEnabled = true;
                HintBtnFunc = () => ShowMsgLabel(string.Format("\n\nNápověda:\n{0}", flavour), false);
            }
        }

        public void SuggestGameType(string gameType)
        {
            _hintBtn.IsEnabled = true;
            HintBtnFunc = () => ShowMsgLabel(string.Format("\n\nNápověda:\n{0}", gameType), false);
        }

        public void SuggestBidsAndDoubles(string bid)
        {
            _hintBtn.IsEnabled = true;
            HintBtnFunc = () => ShowMsgLabel(string.Format("\n\nNápověda:\n{0}", bid), false);
        }

        public void SuggestCardToPlay(Card cardToPlay, string hint)
        {
            _hintBtn.IsEnabled = true;
            HintBtnFunc = () =>
            {
                ShowMsgLabel(hint, false);
                if (!_hand.HighlightCard(cardToPlay))
                {
                    var msg = string.Format("Chyba simulace: hráč nemá {0}", cardToPlay);
                    ShowMsgLabel(msg, false);
                }
            };
        }

        public void SortHand(Card cardToHide = null)
        {
            if (_canSort)
            {
                var unsorted = new List<Card>(g.players[0].Hand);

                if (_settings.SortMode != SortMode.None)
                {
                    g.players[0].Hand.Sort(_settings.SortMode == SortMode.Ascending, (g.GameType & (Hra.Betl | Hra.Durch)) != 0);
                }
                _hand.UpdateHand(g.players[0].Hand.ToArray(), 0, cardToHide);
                _hand.SortHand(unsorted);
                _hand.ShowStraight((int)Game.VirtualScreenWidth - 20);
            }
        }

        private void ShowBubble(int bubbleNo, string message, bool autoHide = true)
        {
            var bubbles = new [] { _bubble1, _bubble2, _bubble3 };

            WaitHandle.WaitAll(_bubbleEvents);
            _bubbleEvents[bubbleNo].Reset();
            bubbles[bubbleNo]
                .Invoke(() =>
                {                    
                    bubbles[bubbleNo].Text = message;
                    bubbles[bubbleNo].Show();
                });
            if (autoHide)
            {
                bubbles[bubbleNo]
                    .Wait(_bubbleTime)
                    .Invoke(() =>
                        {
                            bubbles[bubbleNo].Hide();
                        });
            }
            bubbles[bubbleNo]
                .Invoke(() => _bubbleEvents[bubbleNo].Set());
        }

        private void ShowThinkingMessage()
        {
            string[] msg =
                {
                    "Momentík ...",
                    "Chvilku strpení ...",
                    "Musím si to rozmyslet",
                    "Přemýšlím ..."
                };

            g.ThrowIfCancellationRequested();
            _synchronizationContext.Send(_ =>
                {
                    ShowMsgLabel(msg[(_aiMessageIndex++)%msg.Length], false);
                }, null);
        }

        private void ClearTable(bool hlasy = false)
        {
            _cardsPlayed[0].Hide();
            _cardsPlayed[1].Hide();
            _cardsPlayed[2].Hide();

            if (hlasy)
            {
                for(var i = 0; i < Mariasek.Engine.New.Game.NumPlayers; i++)
                {
                    _hlasy[i][0].Hide();
                    _hlasy[i][1].Hide();
                    _hlasy[i][2].Hide();
                    _hlasy[i][3].Hide();
                }
                _stychy[0].Hide();
                _stychy[1].Hide();
                _stychy[2].Hide();
                _stareStychy[0].Hide();
                _stareStychy[1].Hide();
                _stareStychy[2].Hide();

                _trumpLabel1.Hide();
                _trumpLabel2.Hide();
                _trumpLabel3.Hide();
            }
            _msgLabel.Hide();
            _msgLabelLeft.Hide();
            _msgLabelRight.Hide();

            if (_winningHand != null)
            {
                _winningHand.Hide();
            }
        }

        private void ShowMsgLabel(string message, bool showButton)
        {
            System.Diagnostics.Debug.WriteLine(message);

            _msgLabel.Text = message;
            _msgLabel.Show();

            if (showButton)
            {
                _okBtn.Show();
            }
        }

        private void ShowMsgLabelLeftRight(string leftMessage, string rightMessage)
        {
            _msgLabelLeft.Text = leftMessage;
            _msgLabelRight.Text = rightMessage;
            _msgLabelLeft.Show();
            _msgLabelRight.Show();
        }

        public void HideMsgLabel()
        {
            _msgLabel.Hide();
            _msgLabelLeft.Hide();
            _msgLabelRight.Hide();
            _okBtn.Hide();
        }

        private void ClearTableAfterRoundFinished()
        {
            if (g.CurrentRound == null || !g.IsRunning) //pokud se vubec nehralo (lozena hra) nebo je lozeny zbytek hry
            {
                _evt.Set();
                return;
            }

            var stych = _stychy[g.CurrentRound.roundWinner.PlayerIndex];
            var origPositions = _cardsPlayed.Select(i => i.Position).ToArray();

            foreach (var cardPlayed in _cardsPlayed)
            {
                cardPlayed.MoveTo(stych.Position, 1000);
            }
            stych.WaitUntil(() => _cardsPlayed.All(i => !i.IsBusy))
                .Invoke(() =>
                {
                    var i = 0;
                    foreach (var cardPlayed in _cardsPlayed)
                    {
                        cardPlayed.Hide();
                        cardPlayed.Position = origPositions[i++];
                    }
                    stych.Sprite.SpriteRectangle = _cardsPlayed[g.CurrentRound.roundWinner.PlayerIndex].SpriteRectangle;
                    stych.Show();
                })
                .FlipToBack()
                .Invoke(() =>
                {                        
                    _stareStychy[g.CurrentRound.roundWinner.PlayerIndex].ZIndex = (g.CurrentRound.number - 1) * 3;
                    _stareStychy[g.CurrentRound.roundWinner.PlayerIndex].Show();
                    stych.Hide();
                    _evt.Set();
                });
        }

        private void OverlayTouchUp(object sender, TouchLocation tl)
        {
            _state = GameState.NotPlaying;
            ClearTableAfterRoundFinished();
            HideMsgLabel();
            HideInvisibleClickableOverlay();
        }

        private void ShowInvisibleClickableOverlay()
        {
            _hand.IsEnabled = false;
            ExclusiveControl = _overlay;
            _overlay.IsEnabled = true;
            System.Diagnostics.Debug.WriteLine("ShowInvisibleClickableOverlay");
        }

        private void HideInvisibleClickableOverlay()
        {
            _hand.IsEnabled = true;
            ExclusiveControl = null;
            _overlay.IsEnabled = false;
            System.Diagnostics.Debug.WriteLine("HideInvisibleClickableOverlay");
        }

        private void WaitForUIThread()
        {
            _evt.Reset();
            _evt.WaitOne();
            g.ThrowIfCancellationRequested();
        }
    }
}

