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
        private Button _newGameBtn;
        private Button _repeatGameBtn;
        private Button _reviewGameBtn;
        private ToggleButton _reviewGameToggleBtn;
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
        private Label _msgLabelSmall;
        private Label _msgLabelLeft;
        private Label _msgLabelRight;
        //private Label _gameResult;
        private TextBox _gameResult;
        private Label _totalBalance;
        private ProgressIndicator _progress1, _progress2, _progress3;
        private ProgressIndicator[] _progressBars;
        private TextBox _bubble1, _bubble2, _bubble3;
		private TextBox[] _bubbles;
        private int _bubbleSemaphore;
        private bool[] _bubbleAutoHide;
        private bool _skipBidBubble;
        private GameReview _review;

#pragma warning restore 414
        #endregion

        public Mariasek.Engine.New.Game g;
        private Task _gameTask;
        private SynchronizationContext _synchronizationContext;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly AutoResetEvent _evt = new AutoResetEvent(false);
        private bool _canSort;
        private int _aiMessageIndex;
        public int CurrentStartingPlayerIndex = -1;
        private Mariasek.Engine.New.Configuration.ParameterConfigurationElementCollection _aiConfig;
        public string[] PlayerNames = { "Já", "Karel", "Pepa" };
#if __ANDROID__
        private static string _path = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, "Mariasek");
#else   //#elif __IOS__
        private static string _path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#endif
        private string _archivePath = Path.Combine(_path, "Archive");
        private string _historyFilePath = Path.Combine(_path, "Mariasek.history");
        private string _deckFilePath = Path.Combine(_path, "Mariasek.deck");
        private string _savedGameFilePath = Path.Combine(_path, "_temp.hra");
        private string _testGameFilePath = Path.Combine(_path, "test.hra");
		private string _newGameFilePath = Path.Combine(_path, "_def.hra");
        private string _screenPath = Path.Combine(_path, "screen.png");
        private string _errorFilePath = Path.Combine(_path, "_error.hra");
        private string _endGameFilePath = Path.Combine(_path, "_end.hra");

        private Action HintBtnFunc;
        private volatile GameState _state;
        private volatile Bidding _bidding;
        private volatile Hra _gameTypeChosen;
        private volatile GameFlavour _gameFlavourChosen;
        private GameFlavourChosenEventArgs _gameFlavourChosenEventArgs;
        private bool _firstTimeGameFlavourChosen;
        private volatile Hra _bid;
        private volatile Card _cardClicked;
        private volatile Card _trumpCardChosen;
        private volatile List<Card> _talon;
        private bool _trumpCardTakenBack;
        public float SimulatedSuccessRate;
        private Vector2 _msgLabelLeftOrigPosition;
        private Vector2 _msgLabelLeftHiddenPosition;
        private Vector2 _msgLabelRightOrigPosition;
        private Vector2 _msgLabelRightHiddenPosition;
        private Vector2 _gameResultOrigPosition;
        private Vector2 _gameResultHiddenPosition;
        private Vector2 _totalBalanceOrigPosition;
        private Vector2 _totalBalanceHiddenPosition;
        private bool _testGame;

        public MainScene(MariasekMonoGame game)
            : base(game)
        {
            Game.SettingsChanged += SettingsChanged;
            Game.Stopped += SaveGame;
            Game.Started += ResumeGame;
        }

        private void PopulateAiConfig()
        {
            //TODO: Nastavit prahy podle uspesnosti v predchozich zapasech
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
                Value = "500"
            });
            _aiConfig.Add("SimulationsPerGameTypePerSecond", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "SimulationsPerGameTypePerSecond",
                Value = Game.Settings.GameTypeSimulationsPerSecond.ToString()
            });
            _aiConfig.Add("MaxSimulationTimeMs", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "MaxSimulationTimeMs",
                Value = Game.Settings.ThinkingTimeMs.ToString()
            });
            _aiConfig.Add("SimulationsPerRound", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "SimulationsPerRound",
                Value = "500"
            });
            _aiConfig.Add("SimulationsPerRoundPerSecond", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "SimulationsPerRoundPerSecond",
                Value = Game.Settings.RoundSimulationsPerSecond.ToString()
            });
            _aiConfig.Add("RuleThreshold", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "RuleThreshold",
                Value = "95"
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
            _aiConfig.Add("MaxDoubleCount", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "MaxDoubleCount",
                Value = "3"
            });
            foreach (var thresholdSettings in Game.Settings.Thresholds)
            {
                _aiConfig.Add(string.Format("GameThreshold.{0}", thresholdSettings.GameType.ToString()),
                              new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                              {
                                    Name = string.Format("GameThreshold.{0}", thresholdSettings.GameType.ToString()),
                                    Value = thresholdSettings.Thresholds
                              });
                _aiConfig.Add(string.Format("MaxDoubleCount.{0}", thresholdSettings.GameType.ToString()),
                              new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                              {
                                  Name = string.Format("MaxDoubleCount.{0}", thresholdSettings.GameType.ToString()),
                                  Value = thresholdSettings.MaxBidCount.ToString()
                              });
                _aiConfig.Add(string.Format("CanPlay.{0}", thresholdSettings.GameType.ToString()),
                              new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                              {
                                  Name = string.Format("CanPlay.{0}", thresholdSettings.GameType.ToString()),
                                  Value = thresholdSettings.Use.ToString()
                              });
            }
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
            _aiConfig.Add("GameFlavourSelectionStrategy", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "GameFlavourSelectionStrategy",
                Value = "Fast"
            });
			_aiConfig.Add("RiskFactor", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
			{
				Name = "RiskFactor",
                Value = Game.Settings.RiskFactor.ToString(CultureInfo.InvariantCulture)
			});
		}

        /// <summary>
        /// Allows the game component to perform any initialization it needs to before starting
        /// to run.  This is where it can query for any required services and load content.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            Game.OnSettingsChanged();
            var backSideRect = Game.Settings.CardBackSide.ToTextureRect();
			Game.CardTextures = Game.Settings.CardDesign == CardFace.Single ? Game.CardTextures1 : Game.CardTextures2;
			//PopulateAiConfig(); //volano uz v Game.OnSettingsChanged()

			_synchronizationContext = SynchronizationContext.Current;
            _hlasy = new []
            {
                new []
                {
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 100, Game.VirtualScreenHeight / 2f + 20), ReverseSpriteRectangle = backSideRect, IsEnabled = false, Name="Hlasy11", ZIndex = 1 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 150, Game.VirtualScreenHeight / 2f + 20), ReverseSpriteRectangle = backSideRect, IsEnabled = false, Name="Hlasy12", ZIndex = 2 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 200, Game.VirtualScreenHeight / 2f + 20), ReverseSpriteRectangle = backSideRect, IsEnabled = false, Name="Hlasy13", ZIndex = 3 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 250, Game.VirtualScreenHeight / 2f + 20), ReverseSpriteRectangle = backSideRect, IsEnabled = false, Name="Hlasy14", ZIndex = 4 },
                },
                new []
                {
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(100, 130), ReverseSpriteRectangle = backSideRect, IsEnabled = false, Name="Hlasy21", ZIndex = 1 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(150, 130), ReverseSpriteRectangle = backSideRect, IsEnabled = false, Name="Hlasy22", ZIndex = 2 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(200, 130), ReverseSpriteRectangle = backSideRect, IsEnabled = false, Name="Hlasy23", ZIndex = 3 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(250, 130), ReverseSpriteRectangle = backSideRect, IsEnabled = false, Name="Hlasy24", ZIndex = 4 },
                },
                new []
                {
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 100, 130), ReverseSpriteRectangle = backSideRect, IsEnabled = false, Name="Hlasy31", ZIndex = 1 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 150, 130), ReverseSpriteRectangle = backSideRect, IsEnabled = false, Name="Hlasy32", ZIndex = 2 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 200, 130), ReverseSpriteRectangle = backSideRect, IsEnabled = false, Name="Hlasy33", ZIndex = 3 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 250, 130), ReverseSpriteRectangle = backSideRect, IsEnabled = false, Name="Hlasy34", ZIndex = 4 },
                }
            };
            _hlasy[0][0].Click += TrumpCardClicked;
            _hlasy[0][0].DragEnd += TrumpCardDragged;
			_hlasy[0][0].CanDrag = true;
            _hlasy[0][0].ZIndex = 70;
			_stareStychy = new []
            {
                new Sprite(this, Game.ReverseTexture, backSideRect) { Position = new Vector2(Game.VirtualScreenWidth - 60, Game.VirtualScreenHeight / 2f + 40), Scale = Game.CardScaleFactor, Name = "StareStychy1" },
                new Sprite(this, Game.ReverseTexture, backSideRect) { Position = new Vector2(60, 90), Scale = Game.CardScaleFactor, Name = "StareStychy2" },
                new Sprite(this, Game.ReverseTexture, backSideRect) { Position = new Vector2(Game.VirtualScreenWidth - 60, 90), Scale = Game.CardScaleFactor, Name = "StareStychy3" }
            };
            _stychy = new []
            {
                new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor, Name="Stychy1" }) { Position = new Vector2(Game.VirtualScreenWidth - 60, Game.VirtualScreenHeight / 2f + 40), ReverseSpriteRectangle = backSideRect, IsEnabled = false, ZIndex = 10 },
                new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor, Name="Stychy2" }) { Position = new Vector2(60, 90), ReverseSpriteRectangle = backSideRect, IsEnabled = false, ZIndex = 10 },
                new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor, Name="Stychy3" }) { Position = new Vector2(Game.VirtualScreenWidth - 60, 90), ReverseSpriteRectangle = backSideRect, IsEnabled = false, ZIndex = 10 }
            };
            _cardsPlayed = new []
            {
                new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight / 2f - 90), Scale = Game.CardScaleFactor, Name="CardsPlayed1", ZIndex = 10 },
                new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth / 2f - 50, Game.VirtualScreenHeight / 2f - 130), Scale = Game.CardScaleFactor, Name="CardsPlayed2", ZIndex = 10 },
                new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth / 2f + 50, Game.VirtualScreenHeight / 2f - 140), Scale = Game.CardScaleFactor, Name="CardsPlayed3", ZIndex = 10 }
            };
            _overlay = new ClickableArea(this)
            {
                Width = Game.VirtualScreenWidth,
                Height = Game.VirtualScreenHeight,
                IsEnabled = false
            };
            _overlay.TouchUp += OverlayTouchUp;
            //tlacitka na leve strane
            _newGameBtn = new Button(this)
            {
                Text = "Nová hra",
                Position = new Vector2(10, Game.VirtualScreenHeight / 2f - 150),
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                Width = 150
            };
            _newGameBtn.Click += NewGameBtnClicked;
            _newGameBtn.Hide();
            _repeatGameBtn = new Button(this)
            {
                Text = "Opakovat",
                Position = new Vector2(10, Game.VirtualScreenHeight / 2f - 90),
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                Width = 150
            };
            _repeatGameBtn.Click += RepeatGameBtnClicked;
            _repeatGameBtn.Hide();
            _menuBtn = new Button(this)
            {
                Text = "Menu",
                Position = new Vector2(10, Game.VirtualScreenHeight / 2f - 30),
                ZIndex = 100,
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                Width = 150
            };
            _menuBtn.Click += MenuBtnClicked;
            _reviewGameBtn = new Button(this)
            {
                Text = "Průběh hry",
                Position = new Vector2(10, Game.VirtualScreenHeight / 2f + 30),
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                Width = 150
            };
            _reviewGameBtn.Click += ReviewGameBtnClicked;
            _reviewGameBtn.Hide();
            //tlacitka na prave strane
            _reviewGameToggleBtn = new ToggleButton(this)
            {
                Text = "i",
                Position = new Vector2(Game.VirtualScreenWidth - 60, Game.VirtualScreenHeight / 2f - 150),
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Right : AnchorType.Main,
                Width = 50
            };
            _reviewGameToggleBtn.Click += ReviewGameBtnClicked;
            if (!Game.Settings.TestMode.HasValue || !Game.Settings.TestMode.Value)
            {
                _reviewGameToggleBtn.Hide();
            }
			_sendBtn = new Button(this)
			{
				Text = "@",
				Position = new Vector2(Game.VirtualScreenWidth - 60, Game.VirtualScreenHeight / 2f - 90),
				ZIndex = 100,
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Right : AnchorType.Main,
				Width = 50
			};
			_sendBtn.Click += SendBtnClicked;
			_hintBtn = new Button(this)
            {
                Text = "?",
                Position = new Vector2(Game.VirtualScreenWidth - 60, Game.VirtualScreenHeight / 2f - 30),
                Width = 50,
                TextColor = new Color(0x60, 0x30, 0x10),//Color.SaddleBrown,
                BackgroundColor = Color.White,
                BorderColor = new Color(0x60, 0x30, 0x10),//Color.SaddleBrown,
                ZIndex = 100,
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Right : AnchorType.Main
            };
            _hintBtn.Click += HintBtnClicked;
            //tlacitka ve hre
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
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 140, 1),
                Width = 280,
                Height = 50,
                ZIndex = 100,
				Anchor = AnchorType.Top
            };
			_trumpLabel1.Hide();
            _trumpLabel2 = new Label(this)
            { 
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Middle,
                Position = new Vector2(10, 1),
                Width = 280,
                Height = 50,
                ZIndex = 100,
				Anchor = AnchorType.Top
            };
			_trumpLabel2.Hide();
            _trumpLabel3 = new Label(this)
            { 
                HorizontalAlign = HorizontalAlignment.Right,
                VerticalAlign = VerticalAlignment.Middle,
                Position = new Vector2(Game.VirtualScreenWidth - 290, 1),
                Width = 280,
                Height = 50,
                ZIndex = 100,
				Anchor = AnchorType.Top
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
            _msgLabelSmall = new Label(this)
            {
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                Position = new Vector2(10, 60),
                Width = (int)Game.VirtualScreenWidth - 20,
                Height = (int)Game.VirtualScreenHeight - 120,
                TextColor = Color.Yellow,
                ZIndex = 100
            };
            _msgLabelLeft = new Label(this)
            { 
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Top,
                Position = new Vector2(165, 140),
                Width = (int)Game.VirtualScreenWidth - 280,
                Height = (int)Game.VirtualScreenHeight - 210,
                TextColor = Color.Yellow,
                //TextRenderer = Game.FontRenderers["SegoeUI40Outl"],
                ZIndex = 100
            };
            _msgLabelLeftOrigPosition = new Vector2(160, 140);
            _msgLabelLeftHiddenPosition = new Vector2(160, 140 - Game.VirtualScreenHeight);
            _msgLabelRight = new Label(this)
            { 
                HorizontalAlign = HorizontalAlignment.Right,
                VerticalAlign = VerticalAlignment.Top,
                Position = new Vector2(165, 140),
                Width = (int)Game.VirtualScreenWidth - 280,
                Height = (int)Game.VirtualScreenHeight - 210,
                TextColor = Color.Yellow,
                //TextRenderer = Game.FontRenderers["SegoeUI40Outl"],
                ZIndex = 100
            };
            _msgLabelRightOrigPosition = new Vector2(160, 140);
            _msgLabelRightHiddenPosition = new Vector2(160, 140 - Game.VirtualScreenHeight);
            //_gameResult = new Label(this)
            //{
            //    HorizontalAlign = HorizontalAlignment.Center,
            //    VerticalAlign = VerticalAlignment.Middle,
            //    Position = new Vector2(120, 80),
            //    Width = (int)Game.VirtualScreenWidth - 240,
            //    Height = 40,
            //    TextColor = Color.Yellow,
            //    TextRenderer = Game.FontRenderers["SegoeUI40Outl"],
            //    ZIndex = 100
            //};
            //_gameResultOrigPosition = new Vector2(120, 80);
            //_gameResultHiddenPosition = Vector2(120, 80 - Game.VirtualScreenHeight);
            _gameResult = new TextBox(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 125, 80),
                Width = 250,
                Height = 50,
                BackgroundColor = new Color(0x40, 0x40, 0x40),
                TextColor = Color.Yellow,
                Opacity = 0.8f,
                VerticalAlign = VerticalAlignment.Middle,
                HorizontalAlign = HorizontalAlignment.Center,
                ZIndex = 100
            };
            _gameResultOrigPosition = new Vector2(Game.VirtualScreenWidth / 2 - 125, 80);
            _gameResultHiddenPosition = new Vector2(Game.VirtualScreenWidth / 2 - 125, 80 - Game.VirtualScreenHeight);
            _totalBalance = new Label(this)
            {
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                Position = new Vector2(120, (int)Game.VirtualScreenHeight - 60),
                Width = (int)Game.VirtualScreenWidth - 240,
                Height = 40,
                TextRenderer = Game.FontRenderers["SegoeUI40Outl"],
                ZIndex = 100
            };
            _totalBalanceOrigPosition = new Vector2(120, (int)Game.VirtualScreenHeight - 60);
            _totalBalanceHiddenPosition = new Vector2(120, -60);
			_hand = new GameComponents.Hand(this, new Card[0])
			{
				ZIndex = 50,
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Main : AnchorType.Bottom
			};
            _hand.Click += CardClicked;
            //_hand.ShowArc((float)Math.PI / 2);
            _bubble1 = new TextBox(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 125, Game.VirtualScreenHeight / 2 - 100),
                Width = 250,
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
                Position = new Vector2(10, 80),
                Width = 250,
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
                Position = new Vector2(Game.VirtualScreenWidth - 260, 80),
                Width = 250,
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
			_bubbles = new[] { _bubble1, _bubble2, _bubble3 };
			_bubbleAutoHide = new [] {false, false, false};
			flekBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 230, Game.VirtualScreenHeight / 2f - 155),
                Width = 150,
                Height = 50,
                Tag = Hra.Hra | Hra.Kilo | Hra.Betl | Hra.Durch,
                ZIndex = 100
            };

            flekBtn.Click += BidButtonClicked;
            flekBtn.Hide();
            sedmaBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 75, Game.VirtualScreenHeight / 2f - 155),
                Width = 150,
                Height = 50,
                Tag = Hra.Sedma | Hra.SedmaProti,
                ZIndex = 100
            };
            sedmaBtn.Click += BidButtonClicked;
            sedmaBtn.Hide();
            kiloBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 80, Game.VirtualScreenHeight / 2f - 155),
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
                ZIndex = 100,
				Anchor = AnchorType.Top
            };
            if (!Game.Settings.HintEnabled)
            {
                _progress1.Hide();
            }
            _progress2 = new ProgressIndicator(this)
            {
                Position = new Vector2(0, 0),
                Width = 150,
                Height = 8,
                Color = Color.Green,
                ZIndex = 100,
				Anchor = AnchorType.Top
            };
            _progress3 = new ProgressIndicator(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 150, 0),
                Width = 150,
                Height = 8,
                Color = Color.Blue,
                ZIndex = 100,
				Anchor = AnchorType.Top
            };
            _progressBars = new [] { _progress1, _progress2, _progress3 };
            //Children.Sort((a, b) => a.ZIndex - b.ZIndex);

            LoadHistory();
            Game.LoadGameSettings(false);
            Background = Game.Content.Load<Texture2D>("wood2");
            BackgroundTint = Color.DimGray;
            ClearTable(true);
        }

        /// <summary>
        /// Gets the file stream. Callback function used from Mariasek.Engine.New.Game
        /// </summary>
        private static Stream GetFileStream(string filename)
        {
            var path = Path.Combine(_path, filename);

            CreateDirectoryForFilePath(path);

            return new FileStream(path, FileMode.Create);
        }

        public static void CreateDirectoryForFilePath(string path)
        {
            var dir = Path.GetDirectoryName(path);

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
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
                System.Diagnostics.Debug.WriteLine("Cannot load history\n{0}", e.Message);
            }
        }

        public void SaveHistory()
        {
            try
            {
				var xml = new XmlSerializer(typeof(List<MoneyCalculatorBase>));

                CreateDirectoryForFilePath(_historyFilePath);
				using (var fs = File.Open(_historyFilePath, FileMode.Create))
                {
                    xml.Serialize(fs, Game.Money);
                }
            }
            catch(Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Cannot save history\n{0}", e.Message);
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
				_deck.Init();
                _deck.Shuffle();
            }
        }

        public void SaveDeck()
        {
            try
            {
                CreateDirectoryForFilePath(_deckFilePath);
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

        private int GetGameCount(string path)
        {
            try
            {
                var lastFile1 = Path.GetFileName(Directory.GetFiles(path, "*.????????.def.hra").OrderBy(i => i).Last());
                var lastFile2 = Path.GetFileName(Directory.GetFiles(path, "*.????????.end.hra").OrderBy(i => i).Last());
                var countString1 = lastFile1.Substring(0, lastFile1.IndexOf('-'));
                var countString2 = lastFile2.Substring(0, lastFile2.IndexOf('-'));

                return Math.Max(int.Parse(countString1), int.Parse(countString2));
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Cannot determine file count in folder {0}\n{1}", path, e.Message);
            }

            return 0;
        }

        private string GetBaseFileName(string path)
        {
            var count = GetGameCount(path);
            var gt = string.Empty;

            if ((g.GameType & Hra.Sedma) != 0)
            {
                if ((g.GameType & Hra.Hra) != 0)
                {
                    gt = "sedma";
                }
                else
                {
                    gt = "stosedm";
                }
            }
            else
            {
                if ((g.GameType & Hra.Hra) != 0)
                {
                    gt = "hra";
                }
                else if ((g.GameType & Hra.Kilo) != 0)
                {
                    gt = "kilo";
                }
                else if ((g.GameType & Hra.Betl) != 0)
                {
                    gt = "betl";
                }
                else if ((g.GameType & Hra.Durch) != 0)
                {
                    gt = "durch";
                }
            }
            return string.Format("{0:0000}-{1}.{2}", count + 1, gt, DateTime.Now.ToString("yyyyMMdd"));
        }

        public void ArchiveGame()
        {
            try
            {
                var baseFileName = GetBaseFileName(_archivePath);
                var newGameArchivePath = Path.Combine(_archivePath, string.Format("{0}.def.hra", baseFileName));
                var endGameArchivePath = Path.Combine(_archivePath, string.Format("{0}.end.hra", baseFileName));

                CreateDirectoryForFilePath(newGameArchivePath);
                File.Copy(_newGameFilePath, newGameArchivePath);
                File.Copy(_endGameFilePath, endGameArchivePath);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Cannot archive game\n{0}", e.Message);
            }
        }

        public void CancelRunningTask()
        {
            if (_gameTask != null && _gameTask.Status == TaskStatus.Running)
            {
				ClearTable(true);
                _synchronizationContext.Send(_ =>
                {
                    this.ClearOperations();
                    _evt.Set();
				}, null);
                (g.players[0] as HumanPlayer).CancelAiTask();
                try
                {
                    _cancellationTokenSource.Cancel();
                    _evt.Set();
                    _gameTask.Wait();
                }
                catch (Exception)
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
            sedmaBtn.IsEnabled = ((bids & Hra.Sedma) != 0) ||
								 (((bids & Hra.SedmaProti) != 0) &&
                                  (bidding.SevenAgainstLastBidder == null ||
                                   bidding.SevenAgainstLastBidder.PlayerIndex != g.players[0].TeamMateIndex));
            kiloBtn.IsEnabled = (bids & Hra.KiloProti) != 0 &&
                                (bidding.HundredAgainstLastBidder == null ||
                                 bidding.HundredAgainstLastBidder.PlayerIndex != g.players[0].TeamMateIndex);

			flekBtn.Text = Bidding.MultiplierToString((g.GameType & (Hra.Betl | Hra.Durch)) == 0 ? bidding.GameMultiplier * 2 : bidding.BetlDurchMultiplier * 2);
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

        private void RefreshReview(bool hidden = false)
        {
            if (_review != null)
            {
                Children.Remove(_review);
                _review.Dispose();
            }
            _review = new GameReview(this)
            {
                Position = new Vector2(160, 45),
                Width = (int)Game.VirtualScreenWidth - 160,
                Height = (int)Game.VirtualScreenHeight - 55,
                BackgroundColor = g.IsRunning ? Color.Black : Color.Transparent,
                ZIndex = 200
            };
            if (hidden)
            {
                _review.Hide();
            }
        }

        public void ReviewGameBtnClicked(object sender)
        {
            var origPosition = new Vector2(160, 45);
            var hiddenPosition = new Vector2(160, 45 + Game.VirtualScreenHeight);

            if (_review == null || !_review.IsVisible)
            {
                if (g.IsRunning)
                {
                    Task.Run(() =>
                    {
                        RefreshReview();
                        _review.Opacity = 0f;
                        _review.Show();
                        _review.FadeIn(4f);
                    });
                }
                else
                {
                    _reviewGameBtn.Text = "Vyúčtování";
                    HideGameScore();
                    _totalBalance//.WaitUntil(() => !_totalBalance.IsVisible)
                                 .Invoke(() =>
                                 {
                                     if (_review == null)
                                     {
                                         RefreshReview();
                                     }
                                     //_review.Opacity = 0f;
                                     //_review.Show();
                                     //_review.FadeIn(4f);
                                     _review.Position = hiddenPosition;
                                     _review.Show();
                                     _review.MoveTo(origPosition, 2000);
                                 });
                }
                _hand.IsEnabled = false;
            }
            else
            {
                _hand.IsEnabled = true;
                if (g.IsRunning)
                {
                    _review.Opacity = 1f;
                    _review.Show();
                    _review.FadeOut(4f)
                           .Invoke(() => _review.Hide());
                }
                else
                {
                    _reviewGameBtn.Text = "Průběh hry";
                    //_review.Opacity = 1f;
                    //_review.Show();
                    //_review.FadeOut(4f)
                    //       .Invoke(() =>
                    //{
                    //    _review.Hide();
                    //    ShowGameScore();
                    //});
                    _review.MoveTo(hiddenPosition, 2000)
                           .Invoke(() =>
                    {
                        _review.Hide();
                    });
                    ShowGameScore();
                }
            }
        }

        public void RepeatGameBtnClicked(object sender)
        {
            g = null;
            _testGame = true;
			File.Copy(_newGameFilePath, _savedGameFilePath, true);
			LoadGame();
        }

        public void ShuffleDeck()
        {
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
            _deck.Shuffle();
            SaveDeck();
        }

        public void NewGameBtnClicked(object sender)
        {
            CancelRunningTask();
			_trumpLabel1.Hide();
			_trumpLabel2.Hide();
			_trumpLabel3.Hide();
            _newGameBtn.Hide();
            _repeatGameBtn.Hide();
            _testGame = false;
            _gameTask = Task.Run(() => {
                if (File.Exists(_errorFilePath))
                {
                    File.Delete(_errorFilePath);
                }
                g = new Mariasek.Engine.New.Game()
                {
                    SkipBidding = false,
                    BaseBet = Game.Settings.BaseBet,
                    GetFileStream = GetFileStream,
					GetVersion = () => MariasekMonoGame.Version
                };
                g.RegisterPlayers(
                    new HumanPlayer(g, _aiConfig, this, Game.Settings.HintEnabled) { Name = PlayerNames[0] },
                    new AiPlayer(g, _aiConfig) { Name = PlayerNames[1] },
                    new AiPlayer(g, _aiConfig) { Name = PlayerNames[2] }
                );
                CurrentStartingPlayerIndex = Game.Settings.CurrentStartingPlayerIndex; //TODO: zrusit CurrentStartingPlayerIndex a pouzivat jen Game.Settings.CurrentStartingPlayerIndex
                CurrentStartingPlayerIndex = (CurrentStartingPlayerIndex + 1) % Mariasek.Engine.New.Game.NumPlayers;
#if STARTING_PLAYER_1
                CurrentStartingPlayerIndex = 0;
#elif STARTING_PLAYER_2
                CurrentStartingPlayerIndex = 1;
#elif STARTING_PLAYER_3
                CurrentStartingPlayerIndex = 2;
#endif
                Game.Settings.CurrentStartingPlayerIndex = CurrentStartingPlayerIndex;
                Game.SaveGameSettings();
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
                _trumpCardTakenBack = false;
                _state = GameState.NotPlaying;

                ClearTable(true);
                HideMsgLabel();
                _reviewGameBtn.Hide();
                if (Game.Settings.TestMode.HasValue && Game.Settings.TestMode.Value)
                {
                    _reviewGameToggleBtn.Show();
                    _reviewGameToggleBtn.IsSelected = false;
                }
                if (_review != null)
                {
                    _review.Hide();
                }
                foreach (var btn in gtButtons)
                {
                    btn.BorderColor = Color.White;
                    btn.Hide();
                }
                foreach (var btn in gfButtons)
                {
                    btn.BorderColor = Color.White;
                    btn.Hide();
                }
                foreach (var btn in bidButtons)
                {
                    btn.BorderColor = Color.White;
                    btn.Hide();
                }
                foreach (var bubble in _bubbles)
                {
                    bubble.Hide();
                }
                _hand.ClearOperations();
                this.ClearOperations();
                _hintBtn.IsEnabled = false;
                if (Game.Settings.HintEnabled)
                {
                    _hintBtn.Show();
                }
                else
                {
                    _hintBtn.Hide();
                }
                if (g.GameStartingPlayerIndex != 0)
                {
                    g.players[0].Hand.Sort(Game.Settings.SortMode == SortMode.Ascending, false);
                    ShowThinkingMessage(g.GameStartingPlayerIndex);
                    _hand.Show();
                    UpdateHand();
                    _hand.AnimationEvent.Wait();
                }
                else
                {
                    _hand.Hide();
                }
                for (var i = 0; i < _trumpLabels.Count(); i++)
                {
                    _trumpLabels[i].Text = g.players[i].Name;
                    _trumpLabels[i].Show();
                }
                _hlasy[0][0].Position = new Vector2(Game.VirtualScreenWidth - 100, Game.VirtualScreenHeight / 2f + 20);
                if (File.Exists(_endGameFilePath))
                {
                    try
                    {
                        File.Delete(_endGameFilePath);
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("Cannot delete old end of game file\n{0}", e.Message));
                    }
                }
                g.PlayGame(_cancellationTokenSource.Token);
            }, _cancellationTokenSource.Token);
        }

        private void GameWonPrematurely(object sender, GameWonPrematurelyEventArgs e)
        {
            g.ThrowIfCancellationRequested();
			_evt.Reset();
			this.WaitUntil(() => _bubbles.All(i => !i.IsVisible))
                .Invoke(() =>
                {
                    if ((g.GameType & Hra.Betl) != 0)
                    {
                        if (e.roundNumber > 1)
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
                        if (e.roundNumber > 1)
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
                    var winningCards = new List<Card>();

                    if (g.GameType == Hra.Betl)
                    {
                        winningCards = e.winningHand;
                    }
                    else
                    {
                        for (var i = e.roundNumber - 1; i < Mariasek.Engine.New.Game.NumRounds; i++)
                        {
                            winningCards.Add(g.rounds[i].c1);
                        }
                    }
                    if ((g.GameType & (Hra.Betl | Hra.Durch)) != 0)
                    {                    
						winningCards = winningCards.Sort(false, true).ToList();
					}
                    _winningHand = new GameComponents.Hand(this, winningCards.ToArray());
                    _winningHand.ShowWinningHand(e.winner.PlayerIndex);
                    _winningHand.Show();
                    _state = GameState.RoundFinished;
                });
            WaitForUIThread();
            ClearTable(true);
        }

        public void GameException(object sender, GameExceptionEventArgs e)
        {
            var ex = e.e;
            if (ex.ContainsCancellationException())
			{
                return;
            }
            var ae = ex as AggregateException;
            if (ae != null)
            {
                ex = ae.Flatten().InnerExceptions[0];
            }
            var msg1 = string.Format("Chyba:\n{0}\nOdesílám zprávu...", ex.Message.Split('\n').First());
            var msg2 = string.Format("{0}\n{1}", ex.Message, ex.StackTrace);

            ShowMsgLabel(msg1, false);
            if (Game.EmailSender != null)
            {
                Game.EmailSender.SendEmail(
                    new[] { "mariasek.app@gmail.com" },
                    "Mariasek crash report", msg2,
                    new[] { _newGameFilePath, _errorFilePath, SettingsScene._settingsFilePath });
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
            if (Game.EmailSender != null)
            {
                RefreshReview(true);
				using (var fs = GetFileStream(Path.GetFileName(_screenPath)))
				{
                    var target = _review.SaveTexture();
					target.SaveAsPng(fs, target.Width, target.Height);
				}
				if (g != null && g.IsRunning)
                {
                    using (var fs = GetFileStream(Path.GetFileName(_savedGameFilePath)))
                    {
                        g.SaveGame(fs, saveDebugInfo: true);
                    }
                    Game.EmailSender.SendEmail(new[] { "mariasek.app@gmail.com" }, "Mariášek: komentář", "Sdělte mi prosím své dojmy nebo komentář ke konkrétní hře\n:",
                                               new[] { _screenPath, _newGameFilePath, _savedGameFilePath, SettingsScene._settingsFilePath });
                }
                else
                {
                    Game.EmailSender.SendEmail(new[] { "mariasek.app@gmail.com" }, "Mariášek: komentář", "Sdělte mi prosím své dojmy nebo komentář ke konkrétní hře\n:",
                                               new[] { _screenPath, _newGameFilePath, _endGameFilePath, SettingsScene._settingsFilePath });
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
			var cardClicked = (Card)button.Tag; //_cardClicked nelze pouzit kvuli race condition
			System.Diagnostics.Debug.WriteLine(string.Format("{0} clicked", cardClicked));
            //Task.Run(() =>
            //{
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
                            if (!_talon.Contains(cardClicked))
                            {
                                _talon.Add(cardClicked);
                            }
                        }
                        else
                        {
                            //unselected
                            button.FlipToFront();
                            _talon.Remove(cardClicked);
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
                        if (cardClicked == null)
                            return;
                        _trumpCardChosen = cardClicked;
                        _state = GameState.NotPlaying;
                        HideMsgLabel();
                        button.IsSelected = false; //aby karta nebyla pri animaci tmava
                        var origPosition = _hlasy[0][0].Position;
                        _hlasy[0][0].Sprite.SpriteRectangle = cardClicked.ToTextureRect();
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
                                        _hlasy[0][0].IsEnabled = true;
                                        _evt.Set();
                                    });
                            });
                        _hand.IsEnabled = false;
                        break;
                    case GameState.Play:
                        if (cardClicked == null)
                            return;
                        _state = GameState.NotPlaying;
                        HideMsgLabel();
                        if ((g.GameType & (Hra.Betl | Hra.Durch)) == 0 &&
                            _cardClicked.Value == Hodnota.Svrsek &&
                            g.players[0].Hand.HasK(_cardClicked.Suit))
                        {
                            _hlasy[0][g.players[0].Hlasy].IsEnabled = false;
                            targetSprite = _hlasy[0][g.players[0].Hlasy].Sprite;
                            targetSprite.ZIndex = _hlasy[0][g.players[0].Hlasy].ZIndex;
                        }
                        else
                        {
                            targetSprite = _cardsPlayed[0];
                            targetSprite.ZIndex = _cardsPlayed[0].ZIndex;
                        }
						origPosition = targetSprite.Position;
                        button.Position = button.PostDragPosition;
                        button.Sprite
                              .MoveTo(origPosition, 1000)
                              .Invoke(() =>
                              {
                                  targetSprite.SpriteRectangle = cardClicked.ToTextureRect();
                                  targetSprite.Show();
                                  button.Hide();
                                  button.Position = button.PreDragPosition;
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
            //});
		}

        public void TrumpCardClicked(object sender)
        {
			var button = sender as CardButton;

            if (!button.IsBusy)
            {
                button.FlipToFront()
                      .Wait(1000)
                      .FlipToBack();
            }
		}

        public void TrumpCardDragged(object sender, DragEndEventArgs e)
        {
            var button = sender as CardButton;
			var origPosition = button.Position + e.DragStartLocation - e.DragEndLocation;

            if (e.DragEndLocation.Y > _hand.BoundsRect.Top)
            {
                _trumpCardTakenBack = true;
                _talon.Clear();
                button.Hide();
				button.Position = origPosition;
				UpdateHand();
            }
            else
            {
                button.Sprite.MoveTo(origPosition, 200f);
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
            if (_gameFlavourChosen == GameFlavour.Bad)
            {
                //Task.Run(() =>
                //{
					SortHand(null); //preusporadame karty
					UpdateHand();
				//});
            }
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
			EnsureBubblesHidden();
			g.ThrowIfCancellationRequested();
			_state = GameState.ChooseTrump;
			_hand.IsEnabled = true;
			_hintBtn.IsEnabled = false;
			_cardClicked = null;
			_evt.Reset();
			ShowMsgLabel("Vyber trumfovou kartu", false);
            _hand.Show();
            UpdateHand(flipCardsUp: true, cardsNotRevealed: 5);
            _hand.IsEnabled = true;
            _hand.AllowDragging();
            WaitForUIThread();

            _state = GameState.NotPlaying;
			_hintBtn.IsEnabled = false;
            _hand.IsEnabled = false;
            _hand.ForbidDragging();

            return _cardClicked;
        }

        public List<Card> ChooseTalon()
        {
			EnsureBubblesHidden();
			g.ThrowIfCancellationRequested();
			_hintBtn.IsEnabled = false;
			_hand.IsEnabled = false;
			_cardClicked = null;
			_evt.Reset();
			_talon = new List<Card>();
            ShowMsgLabel("Vyber si talon", false);
            _okBtn.Show();
            _okBtn.IsEnabled = false;
			_state = GameState.ChooseTalon;
			UpdateHand(cardToHide: _trumpCardChosen); //abych si otocil zbyvajicich 5 karet
			_hand.IsEnabled = true;
            WaitForUIThread();

            _state = GameState.NotPlaying;
			_hintBtn.IsEnabled = false;
			_hand.IsEnabled = false;
			_okBtn.Hide();

            return _talon;
        }

        public GameFlavour ChooseGameFlavour()
        {
			EnsureBubblesHidden();
			g.ThrowIfCancellationRequested();
			_hand.IsEnabled = false;
			_hintBtn.IsEnabled = false;
			_gameFlavourChosen = (GameFlavour)(-1);
			_evt.Reset();
			_synchronizationContext.Send(_ =>
    		{
	    		UpdateHand(); //abych nevidel karty co jsem hodil do talonu
                this.Invoke(() =>
                {
					_state = GameState.ChooseGameFlavour;
					foreach (var gfButton in gfButtons)
                    {
                        gfButton.Show();
                    }
                    if (!Game.Settings.HintEnabled || !_msgLabel.IsVisible) //abych neprepsal napovedu
                    {
                        ShowMsgLabel("Co řekneš?", false);
                    }
                });
            }, null);
            WaitForUIThread();

            _state = GameState.NotPlaying;
			_hintBtn.IsEnabled = false;
			_synchronizationContext.Send(_ =>
            {
                this.ClearOperations();
				HideMsgLabel();
				foreach (var gfButton in gfButtons)
				{
					gfButton.Hide();
				}
			}, null);

			return _gameFlavourChosen;
        }

        private void ChooseGameTypeInternal(Hra validGameTypes)
        {
            g.ThrowIfCancellationRequested();
			_state = GameState.ChooseGameType;
			UpdateHand(cardToHide: _trumpCardChosen); //abych nevidel karty co jsem hodil do talonu
            this.Invoke(() =>
            {
                if (_trumpCardTakenBack)
                {
                    validGameTypes &= Hra.Betl | Hra.Durch;
                }
                foreach (var gtButton in gtButtons)
                {                    
                    gtButton.IsEnabled = ((Hra)gtButton.Tag & validGameTypes) == (Hra)gtButton.Tag;
                    gtButton.Show();
                }
                if (!Game.Settings.HintEnabled || !_msgLabel.IsVisible) //abych neprepsal napovedu
                {
                    ShowMsgLabel("Co budeš hrát?", false);
                }
            });
        }

        public Hra ChooseGameType(Hra validGameTypes)
        {
			EnsureBubblesHidden();
			g.ThrowIfCancellationRequested();
			_hand.IsEnabled = false;
			_hintBtn.IsEnabled = false;
			_evt.Reset();
			_synchronizationContext.Send(_ =>
            {
				ChooseGameTypeInternal(validGameTypes);
            }, null);
            WaitForUIThread();

            _state = GameState.NotPlaying;
            _synchronizationContext.Send(_ =>
            {
                HideMsgLabel();
                this.ClearOperations();
                foreach (var btn in gtButtons)
                {
                    btn.Hide();
                }
            }, null);
			g.ThrowIfCancellationRequested();

            return _gameTypeChosen;
        }

        public Hra GetBidsAndDoubles(Bidding bidding)
        {
			EnsureBubblesHidden();
			g.ThrowIfCancellationRequested();
			_evt.Reset();
			_state = GameState.Bid;
			_hand.IsEnabled = false;
			_hand.AnimationEvent.Wait();
			_bid = 0;
			_synchronizationContext.Send(_ =>
            {
				this.Invoke(() =>
                {
                    ShowBidButtons(bidding);
                    _okBtn.IsEnabled = true;
                    _okBtn.Show();
                });
            }, null);
            WaitForUIThread();

            _state = GameState.NotPlaying;
			_synchronizationContext.Send(_ =>
            {
                this.ClearOperations();
                HideBidButtons();
            }, null);

            return _bid;
        }

        public Card PlayCard(Renonc validationState)
        {
            EnsureBubblesHidden();
			g.ThrowIfCancellationRequested();
			_hand.IsEnabled = false;
			_cardClicked = null;
			_evt.Reset();
			_synchronizationContext.Send(_ =>
            {
                this.ClearOperations();
                this.WaitUntil(() => _bubbles.All(i => !i.IsVisible) && !_hand.IsBusy)
                    .Invoke(() =>
                    {
                        foreach (var bubble in _bubbles)
                        {
                            bubble.Hide();
                        }
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
                        if (validationState != Renonc.Ok)
                        {
                            UpdateHand();
                        }
                        _hand.IsEnabled = true;
                        _hand.AllowDragging();
                    });
            }, null);
            WaitForUIThread();

            _state = GameState.NotPlaying;
            _synchronizationContext.Send(_ =>
            {
                this.ClearOperations();
                _hintBtn.IsEnabled = false;
                _hand.IsEnabled = false;
                _hand.ForbidDragging();
            }, null);

			return _cardClicked;
        }

        #endregion

        #region Game event handlers

        public void GameFlavourChosen(object sender, GameFlavourChosenEventArgs e)
        {
            g.ThrowIfCancellationRequested();

            _gameFlavourChosenEventArgs = e;
            _gameFlavourChosen = _gameFlavourChosenEventArgs.Flavour;
            if (_firstTimeGameFlavourChosen)
            {
                if (g.GameStartingPlayerIndex != 0)
                {
                    _progressBars[g.GameStartingPlayerIndex].Progress = _progressBars[g.GameStartingPlayerIndex].Max;
                    if (_gameFlavourChosenEventArgs.Flavour == GameFlavour.Good)
                    {
                        if (g.GameType == 0)
                        {
                            ShowBubble(_gameFlavourChosenEventArgs.Player.PlayerIndex, "Barva?");
                        }
                        if (g.GameStartingPlayerIndex != 2)
                        {
                            ShowThinkingMessage((g.GameStartingPlayerIndex + 1) % Mariasek.Engine.New.Game.NumPlayers);
                        }
                    }
                    else
                    {
                        //protihrac z ruky zavolil betl nebo durch
                        //var str = _gameFlavourChosenEventArgs.Flavour == GameFlavour.Good ? "Dobrá" : "Špatná";
                        //ShowBubble(_gameFlavourChosenEventArgs.Player.PlayerIndex, str);
                        _trumpCardChosen = null;
                        SortHand(null); //preusporadame karty
                        UpdateHand();
                    }
                }
                else
                {
                    if (_gameFlavourChosenEventArgs.Flavour == GameFlavour.Good)
                    {
                        if (g.GameType == 0)
                        {
                            ShowBubble(_gameFlavourChosenEventArgs.Player.PlayerIndex, "Barva?");
                        }
                        ShowThinkingMessage((_gameFlavourChosenEventArgs.Player.PlayerIndex + 1) % Mariasek.Engine.New.Game.NumPlayers);
                    }
                    else
                    {
                        _trumpCardChosen = null;
                        SortHand(null); //preusporadame karty
                        UpdateHand();
                    }
                }
                //UpdateHand(cardToHide: _trumpCardChosen);
            }
            else if (_gameFlavourChosenEventArgs.Flavour == GameFlavour.Bad || g.GameType == 0)
            {
                var str = _gameFlavourChosenEventArgs.Flavour == GameFlavour.Good ? "Dobrá" : "Špatná";
                ShowBubble(_gameFlavourChosenEventArgs.Player.PlayerIndex, str);
                if (e.Player.PlayerIndex != 2 && _gameFlavourChosenEventArgs.Flavour == GameFlavour.Good)
                {
                    ShowThinkingMessage((e.Player.PlayerIndex + 1) % Mariasek.Engine.New.Game.NumPlayers);
                }
                if (_gameFlavourChosenEventArgs.Flavour == GameFlavour.Bad)
                {
                    SortHand(null); //preusporadame karty
                    if (e.Player.PlayerIndex != 0 && g.OriginalGameStartingPlayerIndex == 0)
                    {
                        _hlasy[0][0].IsEnabled = false;
						_hlasy[0][0].Hide();
                        UpdateHand(); //abych vratil trumfovou kartu zpet do ruky
                    }
                }
            }
            _firstTimeGameFlavourChosen = false;
        }

        public void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            g.ThrowIfCancellationRequested();
            //trumfovou kartu otocime az zmizi vsechny bubliny
            _synchronizationContext.Send(_ =>
            {
                var imgs = new[]
                {
                _hlasy[0][0], _hlasy[1][0], _hlasy[2][0]
            };
                this.Invoke(() =>
                {
                    for (var i = 0; i < _trumpLabels.Count(); i++)
                    {
                        _trumpLabels[i].Text = g.players[i].Name;
                    }
                    _trumpLabels[e.GameStartingPlayerIndex].Text = string.Format("{0}: {1}", g.players[e.GameStartingPlayerIndex].Name, g.GameType.ToDescription(g.trump));
                });

                if (e.TrumpCard != null)
                {
                    this.Invoke(() =>
                    {
                        imgs[e.GameStartingPlayerIndex].Sprite.SpriteRectangle = e.TrumpCard.ToTextureRect();
                        imgs[e.GameStartingPlayerIndex].ShowBackSide();
                        imgs[e.GameStartingPlayerIndex].FlipToFront();
                    })
                    .Wait(500)
                    .Invoke(() =>
                    {
                        _bubbleAutoHide[e.GameStartingPlayerIndex] = true;
                        _bubbles[e.GameStartingPlayerIndex].Text = g.GameType.ToDescription(g.trump);
                        _bubbles[e.GameStartingPlayerIndex].Show();
                    })
                    .Wait(2000)
                    .Invoke(() =>
                    {
                        _bubbles[e.GameStartingPlayerIndex].Hide();
                    })
                    .Wait(1000)
                    .Invoke(() =>
                    {
                        imgs[e.GameStartingPlayerIndex].Hide();
                        UpdateHand();
                    });
                    _skipBidBubble = true;  //abychom nezobrazovali bublinu znovu v BidMade()
                    SortHand(null); //preusporadame karty
                    UpdateHand(cardToHide: e.TrumpCard);
                }
                else if (e.GameStartingPlayerIndex == 0)
                {
                    this.Invoke(() =>
                    {
                        imgs[e.GameStartingPlayerIndex].Hide();
                    });
                }
                this.Invoke(() =>
                {
                    _progressBars[e.GameStartingPlayerIndex].Progress = _progressBars[e.GameStartingPlayerIndex].Max;
                });
            }, null);
        }

        public void BidMade(object sender, BidEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("!!BidMade: {0} ({1})", e.Player.PlayerIndex + 1, e.Description));
            _progressBars[e.Player.PlayerIndex].Progress = _progressBars[e.Player.PlayerIndex].Max;
            if (!_skipBidBubble)
            {
                ShowBubble(e.Player.PlayerIndex, e.Description);
            }
            _skipBidBubble = false;
            //premysleci bublinu nezobrazuj pokud bude premyslet clovek
            //nebo pokud se naposledy vyslovil volici hrac a uz dal neflekuje (budeme hrat)
            if (e.Player.PlayerIndex != 2 && e.Player.PlayerIndex != g.GameStartingPlayerIndex && e.BidMade != 0)
            {
                ShowThinkingMessage((e.Player.PlayerIndex + 1) % Mariasek.Engine.New.Game.NumPlayers);
            }
        }

        public void CardPlayed(object sender, Round r)
        {
            if (r == null || g == null || r.c1 == null || !g.IsRunning) //pokud se vubec nehralo (lozena hra) nebo je lozeny zbytek hry
            {
                return;
            }
			EnsureBubblesHidden();
            g.ThrowIfCancellationRequested();
			_synchronizationContext.Send(_ =>
            {
                if (g == null || !g.IsRunning) //pokud se vubec nehralo (lozena hra) nebo je lozeny zbytek hry
				{
					return;
				}
				Card lastCard;
                AbstractPlayer lastPlayer;
                bool lastHlas;
                Rectangle rect1 = r.c1 != null ? r.c1.ToTextureRect() : default(Rectangle);
                Rectangle rect2 = r.c2 != null ? r.c2.ToTextureRect() : default(Rectangle);
                Rectangle rect3 = r.c3 != null ? r.c3.ToTextureRect() : default(Rectangle);

                if (r.c3 != null)
                {
                    lastCard = r.c3;
                    lastPlayer = r.player3;
                    lastHlas = r.hlas3;
                }
                else if (r.c2 != null)
                {
                    lastCard = r.c2;
                    lastPlayer = r.player2;
                    lastHlas = r.hlas2;
                }
                else
                {
                    lastCard = r.c1;
                    lastPlayer = r.player1;
                    lastHlas = r.hlas1;
                }
                _progressBars[lastPlayer.PlayerIndex].Progress = _progressBars[lastPlayer.PlayerIndex].Max;
                HideThinkingMessage();
                if (lastPlayer.PlayerIndex != 2 && r.c3 == null)
                {
                    ShowThinkingMessage((lastPlayer.PlayerIndex + 1) % Mariasek.Engine.New.Game.NumPlayers);
                }
                if (lastPlayer.PlayerIndex == 2)
                {
                    this.Wait(200); //aby AI nehrali moc rychle po sobe
                }
                this.Invoke(() =>
                {
                    if (r.c1 != null)
                    {
                        if (r.hlas1)
                        {
                            _hlasy[r.player1.PlayerIndex][r.player1.Hlasy - 1].Sprite.SpriteRectangle = rect1;
                            _hlasy[r.player1.PlayerIndex][r.player1.Hlasy - 1].Show();
                        }
                        else
                        {
                            _cardsPlayed[r.player1.PlayerIndex].SpriteRectangle = rect1;
                            _cardsPlayed[r.player1.PlayerIndex].Show();
                        }
                    }
                    if (r.c2 != null)
                    {
                        if (r.hlas2)
                        {
                            _hlasy[r.player2.PlayerIndex][r.player2.Hlasy - 1].Sprite.SpriteRectangle = rect2;
                            _hlasy[r.player2.PlayerIndex][r.player2.Hlasy - 1].Show();
                        }
                        else
                        {
                            _cardsPlayed[r.player2.PlayerIndex].SpriteRectangle = rect2;
                            _cardsPlayed[r.player2.PlayerIndex].Show();
                        }
                    }
                    if (r.c3 != null)
                    {
                        if (r.hlas3)
                        {
                            _hlasy[r.player3.PlayerIndex][r.player3.Hlasy - 1].Sprite.SpriteRectangle = rect3;
                            _hlasy[r.player3.PlayerIndex][r.player3.Hlasy - 1].Show();
                        }
                        else
                        {
                            _cardsPlayed[r.player3.PlayerIndex].SpriteRectangle = rect3;
                            _cardsPlayed[r.player3.PlayerIndex].Show();
                        }
                    }

                    _hand.DeselectAllCards();
                        //_hand.ShowArc((float)Math.PI / 2);
                        _hand.ShowStraight((int)Game.VirtualScreenWidth - 20);
                });
            }, null);
        }

        public void RoundStarted(object sender, Round r)
        {
            g.ThrowIfCancellationRequested();
            if (r.player1.PlayerIndex != 0)
            {
                ShowThinkingMessage(r.player1.PlayerIndex);
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
                _evt.Reset();
                _synchronizationContext.Send(_ =>
                {
                    this.Wait(1000)
                        .Invoke(() =>
                        {
                            var roundWinnerIndex = r.roundWinner.PlayerIndex;

                            //pokud hrajeme hru v barve a sebereme nekomu desitku nebo eso, tak se zasmej
                            if ((g.GameType & (Hra.Betl | Hra.Durch)) == 0 &&
                                ((r.player1.PlayerIndex != roundWinnerIndex && r.player1.TeamMateIndex != roundWinnerIndex && (r.c1.Value == Hodnota.Eso || r.c1.Value == Hodnota.Desitka)) ||
                                 (r.player2.PlayerIndex != roundWinnerIndex && r.player2.TeamMateIndex != roundWinnerIndex && (r.c2.Value == Hodnota.Eso || r.c2.Value == Hodnota.Desitka)) ||
                                 (r.player3.PlayerIndex != roundWinnerIndex && r.player3.TeamMateIndex != roundWinnerIndex && (r.c3.Value == Hodnota.Eso || r.c3.Value == Hodnota.Desitka))))
                            {
                                Game.LaughSound?.PlaySafely();
                                //_synchronizationContext.Send(_ =>
                                //{ Game.LaughSound?.PlaySafely(); }, null);
                            }
                            ClearTableAfterRoundFinished();
                        });
                }, null);
                WaitForUIThread();
            }
        }

        private void EnsureBubblesHidden()
        {
            var evt = new AutoResetEvent(false);

            //aby se bubliny predcasne neschovaly
            //while (!this.ScheduledOperations.IsEmpty)
            {                
                this.Invoke(() => evt.Set());
                //evt.WaitOne();
                WaitHandle.WaitAny(new [] { evt, _evt });   //_evt bude nastaven pokud chci preusit hru (a zacit novou)
            }
        }

        public void GameFinished(object sender, MoneyCalculatorBase results)
        {
            _state = GameState.GameFinished;

            results.SimulatedSuccessRate = SimulatedSuccessRate;
            if (!_testGame)
            {
				Game.Money.Add(results);
				SaveHistory();
            }

            EnsureBubblesHidden();
			g.ThrowIfCancellationRequested();
            _synchronizationContext.Send(_ =>
            {
                ClearTable(true);
                _hand.UpdateHand(new Card[0]);
                _hintBtn.IsEnabled = false;
                //multi-line string needs to be split into two strings separated by a tab on each line
                var leftMessage = new StringBuilder();
                var rightMessage = new StringBuilder();
                var firstWord = string.Empty;

                foreach (var line in g.Results.ToString().Split('\n'))
                {
                    var tokens = line.Split('\t');
                    if (tokens.Length > 0)
                    {
                        if (firstWord == string.Empty)
                        {
                            firstWord = tokens[0];
                            continue;
                        }
                        leftMessage.Append(tokens[0]);
                    }
                    if (tokens.Length > 1)
                    {
                        rightMessage.Append(tokens[1]);
                    }

                    leftMessage.Append("\n");
                    rightMessage.Append("\n");
                }
                if (_review != null)
                {
                    _review.Dispose();
                    _review = null;
                }
                _reviewGameToggleBtn.Hide();
                _reviewGameBtn.Text = "Průběh hry";
                _reviewGameBtn.Show();
                HideInvisibleClickableOverlay();
                HideThinkingMessage();
                var totalWon = Game.Money.Sum(i => i.MoneyWon[0]) * Game.Settings.BaseBet;
                _totalBalance.Text = string.Format("Celkem jsem {0}: {1}", totalWon >= 0 ? "vyhrál" : "prohrál", totalWon.ToString("C", CultureInfo.CreateSpecificCulture("cs-CZ")));
                _newGameBtn.Show();
                _repeatGameBtn.Show();
                if (!results.GamePlayed || results.MoneyWon[0] == 0)
                {
                    _gameResult.BorderColor = Color.Blue;
                }
                else if (results.MoneyWon[0] > 0)
                {
                    _gameResult.BorderColor = Color.Green;
                }
                else //(results.MoneyWon[0] < 0)
                {
                    _gameResult.BorderColor = Color.Red;
                }
                _gameResult.Text = firstWord;
                _msgLabelLeft.Text = leftMessage.ToString();
                _msgLabelRight.Text = rightMessage.ToString();
                ShowGameScore();
            }, null);
            _deck = g.GetDeckFromLastGame();
            SaveDeck();
            if (g.rounds[0] != null)
            {
                ArchiveGame();
            }
			if (File.Exists(_savedGameFilePath))
			{
				try
				{
					File.Delete(_savedGameFilePath);
				}
				catch (Exception e)
				{
					System.Diagnostics.Debug.WriteLine(string.Format("Cannot delete old end of game file\n{0}", e.Message));
				}
			}
			var value = (int)g.players.Where(i => i is AiPlayer).Average(i => (i as AiPlayer).Settings.SimulationsPerGameTypePerSecond);
            if (value > 0)
            {
                Game.Settings.GameTypeSimulationsPerSecond = value;
            }
            value = (int)g.players.Where(i => i is AiPlayer).Average(i => (i as AiPlayer).Settings.SimulationsPerRoundPerSecond);
            if (value > 0)
            {
                Game.Settings.RoundSimulationsPerSecond = value;
            }
            //_aiConfig["SimulationsPerGameTypePerSecond"].Value = Game.Settings.GameTypeSimulationsPerSecond.ToString();
            //_aiConfig["SimulationsPerRoundPerSecond"].Value = Game.Settings.RoundSimulationsPerSecond.ToString();
            Game.Settings.CurrentStartingPlayerIndex = CurrentStartingPlayerIndex;
            //tohle zpusobi prekresleni nekterych ui prvku, je treba volat z UI threadu
            _synchronizationContext.Send(_ => Game.UpdateSettings(), null);

            if (g.rounds[0] != null && (results.MoneyWon[0] >= 4 || (g.GameStartingPlayerIndex != 0 && results.MoneyWon[0] >= 2)))
            {                
                Game.ClapSound?.PlaySafely();
            }
            else if (results.MoneyWon[0] <= -10 || (g.GameStartingPlayerIndex != 0 && results.MoneyWon[0] <= -5))
            {
                Game.BooSound?.PlaySafely();
            }
            else
            {
                Game.CoughSound?.PlaySafely();
            }
        }

        #endregion

        public bool CanLoadGame()
        {
            return File.Exists(_savedGameFilePath);
        }

        public bool CanLoadTestGame()
        {
            return File.Exists(_testGameFilePath);
        }

        public void LoadGame(bool testGame = false)
        {
            //var gameToLoadString = ResourceLoader.GetEmbeddedResourceString(this.GetType().Assembly, "GameToLoad");
            if (g == null)
            {
				CancelRunningTask();
				_trumpLabel1.Hide();
				_trumpLabel2.Hide();
				_trumpLabel3.Hide();
				_newGameBtn.Hide();
				_repeatGameBtn.Hide();
				SetActive();
                _cancellationTokenSource = new CancellationTokenSource();
                _gameTask = Task.Run(() =>
                {
                    g = new Mariasek.Engine.New.Game()
                    {
                        SkipBidding = false,
                        GetFileStream = GetFileStream,
                        BaseBet = Game.Settings.BaseBet,
                        GetVersion = () => MariasekMonoGame.Version
                    };
                    g.RegisterPlayers(
                        new HumanPlayer(g, _aiConfig, this, Game.Settings.HintEnabled) { Name = PlayerNames[0] },
                        new AiPlayer(g, _aiConfig) { Name = PlayerNames[1] },
                        new AiPlayer(g, _aiConfig) { Name = PlayerNames[2] }
                    );

                    try
                    {
                        using (var fs = File.Open(testGame ? _testGameFilePath : _savedGameFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            g.LoadGame(fs);
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowMsgLabel(string.Format("Error loading game:\n{0}", ex.Message), false);
                        if (!testGame)
                        {
                            File.Delete(_savedGameFilePath);
                        }
                        MenuBtnClicked(this);
                        return;
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
                    _trumpCardTakenBack = false;
                    _state = GameState.NotPlaying;

                    CurrentStartingPlayerIndex = g.GameStartingPlayerIndex;
                    Game.Settings.CurrentStartingPlayerIndex = CurrentStartingPlayerIndex;
                    Game.SaveGameSettings();
                    _canSort = CurrentStartingPlayerIndex != 0;

                    ClearTable(true);
                    HideMsgLabel();
                    _reviewGameBtn.Hide();
					if (Game.Settings.TestMode.HasValue && Game.Settings.TestMode.Value)
					{
						_reviewGameToggleBtn.Show();
						_reviewGameToggleBtn.IsSelected = false;
					}
                        
                    if (_review != null)
                    {
                        _review.Hide();
                    }
                    foreach (var btn in gtButtons)
                    {
                        btn.BorderColor = Color.White;
                        btn.Hide();
                    }
                    foreach (var btn in gfButtons)
                    {
                        btn.BorderColor = Color.White;
                        btn.Hide();
                    }
                    foreach (var btn in bidButtons)
                    {
                        btn.BorderColor = Color.White;
                        btn.Hide();
                    }
                    foreach (var bubble in _bubbles)
                    {
                        bubble.Hide();
                    }
                    _hand.ClearOperations();
                    this.ClearOperations();
                    _hintBtn.IsEnabled = false;
                    if (Game.Settings.HintEnabled)
                    {
                        _hintBtn.Show();
                    }
                    else
                    {
                        _hintBtn.Hide();
                    }
                    if (g.GameStartingPlayerIndex != 0 || g.GameType != 0)
                    {
                        g.players[0].Hand.Sort(Game.Settings.SortMode == SortMode.Ascending, false);
                        if (g.GameType == 0)
                        {
                            ShowThinkingMessage(g.GameStartingPlayerIndex);
                        }
                        _hand.Show();
                        UpdateHand();
                    }
                    else
                    {
                        _hand.Hide();
                    }
                    var hlasy1 = 0;
                    var hlasy2 = 0;
                    var hlasy3 = 0;
                    foreach(var r in g.rounds.Where(i => i != null))
                    {
                        if (r.hlas1)
                        {
                            var rect = r.c1.ToTextureRect();

                            _hlasy[r.player1.PlayerIndex][hlasy1].Sprite.SpriteRectangle = rect;
							_hlasy[r.player1.PlayerIndex][hlasy1].Show();
                            hlasy1++;
                        }
						if (r.hlas2)
						{
							var rect = r.c1.ToTextureRect();

							_hlasy[r.player1.PlayerIndex][hlasy2].Sprite.SpriteRectangle = rect;
							_hlasy[r.player1.PlayerIndex][hlasy2].Show();
							hlasy2++;
						}
						if (r.hlas3)
						{
							var rect = r.c1.ToTextureRect();

							_hlasy[r.player1.PlayerIndex][hlasy3].Sprite.SpriteRectangle = rect;
							_hlasy[r.player1.PlayerIndex][hlasy3].Show();
							hlasy3++;
						}
					}
					for (var i = 0; i < _trumpLabels.Count(); i++)
                    {
                        _trumpLabels[i].Text = g.players[i].Name;
                        _trumpLabels[i].Show();
                    }
                    if (g.GameType != 0)
                    {
                        _trumpLabels[g.GameStartingPlayerIndex].Text = string.Format("{0}: {1}", g.players[g.GameStartingPlayerIndex].Name, g.GameType.ToDescription(g.trump));
                    }
                    if (!_testGame)
                    {
                        File.Delete(_savedGameFilePath);
                    }
                    g.PlayGame(_cancellationTokenSource.Token);
                }, _cancellationTokenSource.Token);
            }
        }

       public void SaveGame()
       {
            if (g != null && g.IsRunning)
            {
                try
                {
                    CreateDirectoryForFilePath(_savedGameFilePath);
                    if (g.GameType != 0)
                    {
                        using (var fs = File.Open(_savedGameFilePath, FileMode.Create))
                        {
                            g.SaveGame(fs, saveDebugInfo: true);
                        }
                    }
                    else
                    {
                        File.Copy(_newGameFilePath, _savedGameFilePath, true);
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Cannot save game\n{0}", e.Message));
                }
            }
        }

        public void ResumeGame()
        {
            if (CanLoadGame())
            {
                _testGame = false;
                LoadGame();
            }
            else if (CanLoadTestGame())
            {
                _testGame = true;
                File.Copy(_testGameFilePath, _newGameFilePath, true);
                LoadGame(true);
            }
        }

        public void UpdateHand(bool flipCardsUp = false, int cardsNotRevealed = 0, Card cardToHide = null)
        {
            _synchronizationContext.Send(_ =>
            {
                _hand.UpdateHand(g.players[0].Hand.ToArray(), flipCardsUp ? g.players[0].Hand.Count : 0, cardToHide);
            }, null);
            _hand.ShowStraight((int)Game.VirtualScreenWidth - 20);
            if (flipCardsUp)
            {
                _hand.WaitUntil(() => !_hand.SpritesBusy)
                     .Invoke(() =>
                       {
                           _hand.UpdateHand(g.players[0].Hand.ToArray(), cardsNotRevealed, cardToHide);
                       });
            }
			if (_state == GameState.ChooseTrump)
			{
			    _hand.WaitUntil(() => !_hand.SpritesBusy)
			         .Invoke(() =>
			            {
			                _canSort = true;
			                SortHand(null, 7);
			            });
			}
			if (_state == GameState.ChooseTalon)
			{
			    _hand.WaitUntil(() => !_hand.SpritesBusy)
			         .Invoke(() =>
			            {
			                _canSort = true;
			                SortHand(cardToHide);
			            });
			}
		}

        private void UpdateCardTextures(GameComponent parent, Texture2D oldTexture, Texture2D newTexture)
        {
            var sprite = parent as Sprite;
            if (sprite != null && sprite.Texture == oldTexture)
            {
                sprite.Texture = newTexture;
            }
            foreach (var child in parent.ChildElements)
            {
                UpdateCardTextures(child, oldTexture, newTexture);
            }
        }

        public void UpdateCardBackSides(GameComponent parent)
        {
			var cardButton = parent as CardButton;

			if (cardButton != null)
			{
				cardButton.ReverseSpriteRectangle = Game.BackSideRect;
			}
			foreach (var child in parent.ChildElements)
			{
				UpdateCardBackSides(child);
			}
		}

        public void SettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            //Task.Run(() =>
            //{
                PopulateAiConfig();
                if (_progress1 != null)
                {
                    if (Game.Settings.HintEnabled)
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
                    if (Game.Settings.HintEnabled)
                    {
                        _hintBtn.Show();
                    }
                    else
                    {
                        _hintBtn.Hide();
                    }
                }
                var newBackSideRect = Game.Settings.CardBackSide.ToTextureRect();

                if (Game.BackSideRect != newBackSideRect)
                {
                    Game.BackSideRect = newBackSideRect;
                    UpdateCardBackSides(this);
                    if (_stareStychy != null)
                    {
                        foreach (var sprite in _stareStychy)
                        {
                            sprite.SpriteRectangle = newBackSideRect;
                        }
                    }
                }
                //call UpdateHand() instead
                if (_state == GameState.ChooseTrump)
                {
                    SortHand(null, 7);
                }
                else if (_state == GameState.ChooseTalon)
                {
                    SortHand(_trumpCardChosen);
                }
                else
                {
                    SortHand(null);
                }

                var oldTextures = Game.CardTextures;
                var newTextures = Game.Settings.CardDesign == CardFace.Single ? Game.CardTextures1 : Game.CardTextures2;

                if (oldTextures != newTextures)
                {
                    UpdateCardTextures(this, oldTextures, newTextures);
                    Game.CardTextures = newTextures;
                }
            //});
		}

        public void SuggestTrump(Card trumpCard, int? t = null)
        {
            _progress1.Progress = _progress1.Max;
            if (Game.Settings.HintEnabled)
            {
                _hintBtn.IsEnabled = true;
                HintBtnFunc = () =>
                {
                    var msg = string.Format("{0}", trumpCard);

                    _hand.HighlightCard(trumpCard);
                    //ShowMsgLabel(msg, false); //Docasne, zjistit kdy radi kartu kterou jsem nemohl videt
                };
            }
        }

        public void SuggestTalon(List<Card> talon, int? t = null)
        {
            _progress1.Progress = _progress1.Max;
            if (Game.Settings.HintEnabled)
            {
                _hintBtn.IsEnabled = true;
                HintBtnFunc = () =>
                {
                    _hand.HighlightCard(talon[0]);
                    _hand.HighlightCard(talon[1]);
                    //ShowMsgLabel(string.Format("{0}ms", t), false);
                };
            }
        }

        public void SuggestGameFlavour(string flavour, int? t = null)
        {
            _progress1.Progress = _progress1.Max;
            if (Game.Settings.HintEnabled)
            {
                _hintBtn.IsEnabled = true;
                HintBtnFunc = () => ShowMsgLabel(string.Format("\n\nNápověda:\n{0}", flavour), false);
            }
        }

        public void SuggestGameType(string gameType, string allChoices, int? t = null)
        {
            _progress1.Progress = _progress1.Max;
            _hintBtn.IsEnabled = true;
            HintBtnFunc = () =>
            {
                ShowMsgLabel(string.Format("Nápověda:\n{0}", gameType), false);
                _msgLabelSmall.Text = string.Format("\n\n\n\n{0}", allChoices);
                _msgLabelSmall.Show();
            };
        }

        public void SuggestGameTypeNew(Hra gameType)
        {
            foreach (var gtButton in gtButtons)
            {
                //if ((gameType & (Hra)gtButton.Tag) == (Hra)gtButton.Tag)
                if (gameType == (Hra)gtButton.Tag)
                {
                    gtButton.BorderColor = Color.Green;
                }
                else
                {
                    gtButton.BorderColor = Color.White;
                }
            }
        }

        public void SuggestGameFlavourNew(Hra gameType)
        {
            var flavour = (gameType & (Hra.Betl | Hra.Durch)) == 0 ? GameFlavour.Good : GameFlavour.Bad;

            SuggestGameFlavourNew(flavour);
        }

        public void SuggestGameFlavourNew(GameFlavour flavour)
        {
            gfDobraButton.BorderColor = flavour == GameFlavour.Good ? Color.Green : Color.White;
            gfSpatnaButton.BorderColor = flavour == GameFlavour.Bad ? Color.Green : Color.White;
        }

        public void SuggestBidsAndDoubles(string bid, int? t = null)
        {
            _progress1.Progress = _progress1.Max;
            _hintBtn.IsEnabled = true;
            HintBtnFunc = () => ShowMsgLabel(string.Format("\n\nNápověda:\n{0}", bid), false);
        }

		public void SuggestBidsAndDoublesNew(Hra bid)
		{
			foreach (var btn in bidButtons)
			{
				if ((bid & (Hra)btn.Tag) != 0)
				{
					btn.BorderColor = Color.Green;
				}
				else
				{
					btn.BorderColor = Color.White;
				}
			}
		}

		public void SuggestCardToPlay(Card cardToPlay, string hint, int? t = null)
        {
            _progress1.Progress = _progress1.Max;
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

        public void SortHand(Card cardToHide = null, int numberOfCardsToSort = 12)
        {
            if (_canSort)
            {
                var unsorted = new List<Card>(g.players[0].Hand);

                if (Game.Settings.SortMode != SortMode.None)
                {
                    var badGameSorting = _gameFlavourChosen == GameFlavour.Bad || (g.GameType & (Hra.Betl | Hra.Durch)) != 0;

                    if (numberOfCardsToSort == 12)
                    {
                        g.players[0].Hand.Sort(Game.Settings.SortMode == SortMode.Ascending, badGameSorting, g.trump);
                    }
                    else
                    {
                        var sortedList = g.players[0].Hand.Take(numberOfCardsToSort).ToList();

                        sortedList.Sort(Game.Settings.SortMode == SortMode.Ascending, badGameSorting, g.trump);
                        g.players[0].Hand = sortedList.Concat(g.players[0].Hand.Skip(numberOfCardsToSort).Take(12)).ToList();
                    }
                }
                _hand.UpdateHand(g.players[0].Hand.ToArray(), 12 - numberOfCardsToSort, cardToHide);
                _hand.SortHand(unsorted);
            }
        }

        public void DeleteArchiveFolder()
        {
            foreach (var game in Directory.GetFiles(_archivePath))
            {
                File.Delete(game);
            }
        }

        private void ShowBubble(int bubbleNo, string message, bool autoHide = true)
        {
            Console.WriteLine("!!ShowBubble ({0}, \"{1}\", {2}) ->", bubbleNo + 1, message, autoHide);
            this.Invoke(() =>
                {
                    if (_bubbles[bubbleNo].IsVisible && !_bubbleAutoHide[bubbleNo])
                    {
                        Console.WriteLine("!!Bubble {0} hide old {1}", bubbleNo + 1, _bubbleAutoHide[bubbleNo] ? "auto" : "manual");
                    }
                    _bubbleAutoHide[bubbleNo] = autoHide;
                    _bubbles[bubbleNo].Text = message;
                    _bubbles[bubbleNo].Show();
                    Console.WriteLine("!!Bubble {0} show [{1}]: {2}", bubbleNo + 1, message, _bubbleAutoHide[bubbleNo] ? "auto" : "manual");
                });
            if (autoHide)
            {
                this.Wait(Game.Settings.BubbleTimeMs)
                    .Invoke(() =>
                    {
                        _bubbles[bubbleNo].Hide();
                        Console.WriteLine("!!Bubble {0} hide [{1}]: {2}", bubbleNo + 1, message, _bubbleAutoHide[bubbleNo] ? "auto" : "manual");
                    });
            }
            Console.WriteLine("!!ShowBubble ({0}, \"{1}\", {2}) <-", bubbleNo + 1, message, autoHide);
        }

        private void ShowThinkingMessage(int playerIndex = -1)
        {
            string[] msg =
            {
                "Momentík ...",
                "Chvilku strpení ...",
                "Musím si to rozmyslet",
                "Přemýšlím ..."
            };

            System.Diagnostics.Debug.WriteLine(string.Format("!!ShowThinkingMessage({0})", playerIndex + 1));
            g.ThrowIfCancellationRequested();
            if (playerIndex == -1)
            {
                ShowMsgLabel(msg[(_aiMessageIndex++) % msg.Length], false);
            }
            else
            {
                ShowBubble(playerIndex, msg[(_aiMessageIndex++) % msg.Length], false);
            }
        }

        private void HideThinkingMessage()
        {
            HideMsgLabel();
            this.ClearOperations();         //MainScene holds bubble operations
            foreach (var bubble in _bubbles)
            {
                bubble.Hide();
            }
            _bubbleSemaphore = 0;
        }

        private void ClearTable(bool hlasy = false)
        {
            _cardsPlayed[0].Hide();
            _cardsPlayed[1].Hide();
            _cardsPlayed[2].Hide();

            if (hlasy)
            {
                for (var i = 0; i < Mariasek.Engine.New.Game.NumPlayers; i++)
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
            }
            _msgLabel.Hide();
            _msgLabelSmall.Hide();
            _msgLabelLeft.Hide();
            _msgLabelRight.Hide();
            _gameResult.Hide();
            _totalBalance.Hide();
            _hand.Hide();

            if (_winningHand != null)
            {
                _winningHand.Hide();
            }
        }

        private void ShowMsgLabel(string message, bool showButton)
        {
            System.Diagnostics.Debug.WriteLine(string.Format("!!ShowMsgLabel: {0}", message));

            _msgLabel.Text = message;
            _msgLabel.Show();

            if (showButton)
            {
                _okBtn.Show();
            }
        }

        private void ShowGameScore()
        {
            _msgLabelLeft.Position = _msgLabelLeftHiddenPosition;
            _msgLabelRight.Position = _msgLabelRightHiddenPosition;
            _gameResult.Position = _gameResultHiddenPosition;
            _totalBalance.Position = _totalBalanceHiddenPosition;

            _msgLabelLeft.Show();
            _msgLabelRight.Show();
            _gameResult.Show();
            _totalBalance.Show();

            _msgLabelLeft.MoveTo(_msgLabelLeftOrigPosition, 2000);
            _msgLabelRight.MoveTo(_msgLabelRightOrigPosition, 2000);
            _gameResult.MoveTo(_gameResultOrigPosition, 2000);
            _totalBalance.MoveTo(_totalBalanceOrigPosition, 2000);
        }

        private void HideGameScore()
        {
            _msgLabelLeft.MoveTo(_msgLabelLeftHiddenPosition, 2000).Invoke(() => _msgLabelLeft.Hide());
            _msgLabelRight.MoveTo(_msgLabelRightHiddenPosition, 2000).Invoke(() => _msgLabelRight.Hide());
            _gameResult.MoveTo(_gameResultHiddenPosition, 2000).Invoke(() => _gameResult.Hide());
            _totalBalance.MoveTo(_totalBalanceHiddenPosition, 2000).Invoke(() => _totalBalance.Hide());
        }

        public void HideMsgLabel()
        {
            _msgLabel.Hide();
            _msgLabelSmall.Hide();
            _msgLabelLeft.Hide();
            _msgLabelRight.Hide();
            _gameResult.Hide();
            _totalBalance.Hide();
            _okBtn.Hide();
        }

        private void ClearTableAfterRoundFinished()
        {
			//pokud se vubec nehralo (lozena hra) nebo je lozeny zbytek hry
			if (g.CurrentRound == null || !g.IsRunning || g.CurrentRound.roundWinner == null)
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
            //_state = GameState.NotPlaying;
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

        private bool WaitForUIThread(TimeSpan? ts = null)
        {
            bool result;

            if (ts.HasValue)
            {
                result = _evt.WaitOne(ts.Value);
			}
            else
            {
                result = _evt.WaitOne();
            }
            g.ThrowIfCancellationRequested();

            return result;
        }
    }
}

