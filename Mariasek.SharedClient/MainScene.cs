﻿//#define STARTING_PLAYER_1
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
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using Mariasek.SharedClient.GameComponents;
using Mariasek.Engine;
using CsvHelper;
using Microsoft.Xna.Framework.Input;

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
        private Sprite[] _cardsPlayed;  //aktualni kolo
        private CardButton[][] _hlasy;  //vylozene hlasy
        private CardButton[] _stychy;   //animace otoceni stychu
        private Sprite[] _stareStychy;  //stychy z minulych kol
        private Sprite[][] _poslStych;  //zobrazeni posledniho stychu a talonu
        private Sprite[] _shuffledCards;  //aktualni kolo
        private ClickableArea _overlay;
        private Button _menuBtn;
        private Button _sendBtn;
        private Button _newGameBtn;
        private Button _repeatGameBtn;
        private Button _repeatGameOptionBtn;
        private Button _repeatGameAsPlayer2Btn;
        private Button _repeatGameAsPlayer3Btn;
        private Button _reviewGameBtn;
        private Button _editGameBtn;
        private ToggleButton _reviewGameToggleBtn;
        private Button _okBtn;
        private Button[] gtButtons, gfButtons, bidButtons;
        private Button gtHraButton;
        private Button gt7Button;
        private Button gt100Button;
        private Button gt107Button;
        private Button gtBetlButton;
        private Button gtDurchButton;
        private Button giveUpButton;
        private Button gfDobraButton;
        private Button gfSpatnaButton;
        private ToggleButton flekBtn;
        private ToggleButton sedmaBtn;
        private ToggleButton kiloBtn;
        private Button _hintBtn;
        private TextBox _trumpLabel1, _trumpLabel2, _trumpLabel3;
        private TextBox[] _trumpLabels;
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
        private bool _shouldShuffle;
        private bool _shuffleAnimationRunning;
        private AutoResetEvent _shuffleEvent = new AutoResetEvent(false);
        private GameReview _review;

#pragma warning restore 414
        #endregion

        public Mariasek.Engine.Game g;
        private SemaphoreSlim _gameSemaphore = new SemaphoreSlim(1);
        private Task _gameTask;
        private Task _shuffleTask;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly AutoResetEvent _evt = new AutoResetEvent(false);
        private readonly AutoResetEvent _preGameEvent = new AutoResetEvent(false);
        private bool _canSort;
        private bool _canSortTrump;
        private bool _firstTimeTalonCardClick;
        private int _aiMessageIndex;
        public int CurrentStartingPlayerIndex = -1;
        private AiPlayerSettings _aiSettings;
        private string _archivePath = Path.Combine(MariasekMonoGame.RootPath, "Archive");
        private string _historyFilePath = Path.Combine(MariasekMonoGame.RootPath, "Mariasek.history");
        private string _deckFilePath = Path.Combine(MariasekMonoGame.RootPath, "Mariasek.deck");
        private string _savedGameFilePath = Path.Combine(MariasekMonoGame.RootPath, "_temp.hra");
        private string _newGameFilePath = Path.Combine(MariasekMonoGame.RootPath, "_def.hra");
        private string _screenPath = Path.Combine(MariasekMonoGame.RootPath, "screen.png");
        private string _errorFilePath = Path.Combine(MariasekMonoGame.RootPath, "_error.hra");
        private string _errorMsgFilePath = Path.Combine(MariasekMonoGame.RootPath, "_error.txt");
        private string _endGameFilePath = Path.Combine(MariasekMonoGame.RootPath, "_end.hra");

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
        public bool TrumpCardTakenBack { get; private set; }
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
        private bool _lastGameWasLoaded;

        public MainScene(MariasekMonoGame game)
            : base(game)
        {
            Game.SettingsChanged += SettingsChanged;
            Game.Activated += GameActivated;
            Game.Deactivated += GameDeactivated;
            Game.Stopped += SaveGame;
            Game.Started += ResumeGame;
            SceneActivated += Activated;
        }

        private void PopulateAiConfig()
        {
            //TODO: Nastavit prahy podle uspesnosti v predchozich zapasech
            _aiSettings = new AiPlayerSettings()
            {
                Cheat = Game.Settings.AiCheating.HasValue && Game.Settings.AiCheating.Value,
                AiMayGiveUp = Game.Settings.AiMayGiveUp,
                RoundsToCompute = 1,
                CardSelectionStrategy = CardSelectionStrategy.MaxCount,
                SimulationsPerGameType = 1000,
                SimulationsPerGameTypePerSecond = Game.Settings.GameTypeSimulationsPerSecond,
                MaxSimulationTimeMs = Game.Settings.ThinkingTimeMs,
                SimulationsPerRound = 500,
                SimulationsPerRoundPerSecond = Game.Settings.RoundSimulationsPerSecond,
                RuleThreshold = 0.95f,
                RuleThresholdForGameType = new Dictionary<Hra, float>() { { Hra.Kilo, 0.99f } },
                GameThresholds = new float[] { 0.75f, 0.80f, 0.85f, 0.90f, 0.95f },
                GameThresholdsForGameType = Game.Settings.Thresholds.ToDictionary(k => k.GameType, v => v.Thresholds.Split('|').Select(i => int.Parse(i) / 100f).ToArray()),
                MaxDoubleCountForGameType = Game.Settings.Thresholds.ToDictionary(k => k.GameType, v => v.MaxBidCount),
                CanPlayGameType = Game.Settings.Thresholds.ToDictionary(k => k.GameType, v => v.Use),
                SigmaMultiplier = 0,
                GameFlavourSelectionStrategy = GameFlavourSelectionStrategy.Fast,
                RiskFactor = Game.Settings.RiskFactor,
                RiskFactorSevenDefense = Game.Settings.RiskFactorSevenDefense,
                SolitaryXThreshold = Game.Settings.SolitaryXThreshold,
                SolitaryXThresholdDefense = Game.Settings.SolitaryXThresholdDefense,
                SafetyGameThreshold = Game.Settings.SafetyGameThreshold,
                SafetyHundredThreshold = Game.Settings.SafetyHundredThreshold,
                SafetyBetlThreshold = Game.Settings.SafetyBetlThreshold
            };
        }

        /// <summary>
        /// Allows the game component to perform any initialization it needs to before starting
        /// to run.  This is where it can query for any required services and load content.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();
            Game.OnSettingsChanged();
            Game.BackSideRect = Game.Settings.CardDesign == CardFace.Pikety
                                ? CardBackSide.Pikety.ToTextureRect()
                                : Game.Settings.CardBackSide.ToTextureRect();
            Game.CardTextures = Game.Settings.CardDesign == CardFace.Single
                                    ? Game.CardTextures1
                                    : Game.Settings.CardDesign == CardFace.Double
                                        ? Game.CardTextures2
                                        : Game.CardTextures3;
            //PopulateAiConfig(); //volano uz v Game.OnSettingsChanged()
            _hlasy = new[]
            {
                new []
                {
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 100, Game.VirtualScreenHeight / 2f + 20), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, Name="Hlasy11", ZIndex = 1 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 150, Game.VirtualScreenHeight / 2f + 20), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, Name="Hlasy12", ZIndex = 2 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 200, Game.VirtualScreenHeight / 2f + 20), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, Name="Hlasy13", ZIndex = 3 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 250, Game.VirtualScreenHeight / 2f + 20), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, Name="Hlasy14", ZIndex = 4 },
                },
                new []
                {
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(100, 130), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, Name="Hlasy21", ZIndex = 1 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(150, 130), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, Name="Hlasy22", ZIndex = 2 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(200, 130), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, Name="Hlasy23", ZIndex = 3 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(250, 130), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, Name="Hlasy24", ZIndex = 4 },
                },
                new []
                {
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 100, 130), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, Name="Hlasy31", ZIndex = 1 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 150, 130), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, Name="Hlasy32", ZIndex = 2 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 200, 130), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, Name="Hlasy33", ZIndex = 3 },
                    new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor }) { Position = new Vector2(Game.VirtualScreenWidth - 250, 130), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, Name="Hlasy34", ZIndex = 4 },
                }
            };
            _hlasy[0][0].Click += TrumpCardClicked;
            _hlasy[0][0].DragEnd += TrumpCardDragged;
            _hlasy[0][0].CanDrag = true;
            _hlasy[0][0].ZIndex = 70;
            _stareStychy = new[]
            {
                new Sprite(this, Game.ReverseTexture, Game.BackSideRect) { Position = new Vector2(Game.VirtualScreenWidth - 60, Game.VirtualScreenHeight / 2f + 40), Scale = Game.CardScaleFactor, Name = "StareStychy1" },
                new Sprite(this, Game.ReverseTexture, Game.BackSideRect) { Position = new Vector2(60, 90), Scale = Game.CardScaleFactor, Name = "StareStychy2" },
                new Sprite(this, Game.ReverseTexture, Game.BackSideRect) { Position = new Vector2(Game.VirtualScreenWidth - 60, 90), Scale = Game.CardScaleFactor, Name = "StareStychy3" }
            };
            _poslStych = new[]
            {
                new[]
                {
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth - 200, Game.VirtualScreenHeight / 2f + 40), Scale = Game.CardScaleFactor, Name = "PoslStych11", SpriteRectangle = Rectangle.Empty, ZIndex = 11 },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth - 250, Game.VirtualScreenHeight / 2f + 40), Scale = Game.CardScaleFactor, Name = "PoslStych12", SpriteRectangle = Rectangle.Empty, ZIndex = 12 },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth - 300, Game.VirtualScreenHeight / 2f + 40), Scale = Game.CardScaleFactor, Name = "PoslStych13", SpriteRectangle = Rectangle.Empty, ZIndex = 13 }
                },
                new[]
                {
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(200, 90), Scale = Game.CardScaleFactor, Name = "PoslStych21", SpriteRectangle = Rectangle.Empty, ZIndex = 11 },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(250, 90), Scale = Game.CardScaleFactor, Name = "PoslStych22", SpriteRectangle = Rectangle.Empty, ZIndex = 12 },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(300, 90), Scale = Game.CardScaleFactor, Name = "PoslStych23", SpriteRectangle = Rectangle.Empty, ZIndex = 13 }
                },
                new[]
                {
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth - 200, 90), Scale = Game.CardScaleFactor, Name = "PoslStych31", SpriteRectangle = Rectangle.Empty, ZIndex = 11 },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth - 250, 90), Scale = Game.CardScaleFactor, Name = "PoslStych32", SpriteRectangle = Rectangle.Empty, ZIndex = 12 },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth - 300, 90), Scale = Game.CardScaleFactor, Name = "PoslStych33", SpriteRectangle = Rectangle.Empty, ZIndex = 13 }
                }
            };
            _stychy = new[]
            {
                new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor, Name="Stychy1", SpriteRectangle = Rectangle.Empty }) { Position = new Vector2(Game.VirtualScreenWidth - 60, Game.VirtualScreenHeight / 2f + 40), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, ZIndex = 10 },
                new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor, Name="Stychy2", SpriteRectangle = Rectangle.Empty }) { Position = new Vector2(60, 90), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, ZIndex = 10 },
                new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = Game.CardScaleFactor, Name="Stychy3", SpriteRectangle = Rectangle.Empty }) { Position = new Vector2(Game.VirtualScreenWidth - 60, 90), ReverseSpriteRectangle = Game.BackSideRect, IsEnabled = false, ZIndex = 10 }
            };
            _cardsPlayed = new[]
            {
                new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight / 2f - 65), Scale = Game.CardScaleFactor, Name="CardsPlayed1", ZIndex = 10 },
                new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth / 2f - 50, Game.VirtualScreenHeight / 2f - 95), Scale = Game.CardScaleFactor, Name="CardsPlayed2", ZIndex = 10 },
                new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth / 2f + 50, Game.VirtualScreenHeight / 2f - 105), Scale = Game.CardScaleFactor, Name="CardsPlayed3", ZIndex = 10 }
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
            _repeatGameOptionBtn = new Button(this)
            {
                Text = "»",
                Position = new Vector2(160, Game.VirtualScreenHeight / 2f - 95),
                ZIndex = 99,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                Width = 40,
                BackgroundColor = Color.Transparent,
                BorderColor = Color.Transparent,
                TextRenderer = Game.FontRenderers["SegoeUI40Outl"]
            };
            //_repeatGameOptionBtn.Click += RepeatGameOptionBtnClicked;
            _repeatGameOptionBtn.TouchUp += RepeatGameOptionBtnTouchUp;
            _repeatGameOptionBtn.Hide();
            _repeatGameAsPlayer2Btn = new Button(this)
            {
                Text = "Jako 2",
                Position = new Vector2(170, Game.VirtualScreenHeight / 2f - 90),
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                Width = 200
            };
            _repeatGameAsPlayer2Btn.Click += RepeatGameAsPlayer2BtnClicked;
            _repeatGameAsPlayer2Btn.Hide();
            _repeatGameAsPlayer3Btn = new Button(this)
            {
                Text = "Jako 3",
                Position = new Vector2(380, Game.VirtualScreenHeight / 2f - 90),
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                Width = 200
            };
            _repeatGameAsPlayer3Btn.Click += RepeatGameAsPlayer3BtnClicked;
            _repeatGameAsPlayer3Btn.Hide();
            _menuBtn = new Button(this)
            {
                Text = "Menu",
                Position = new Vector2(10, Game.VirtualScreenHeight / 2f - 30),
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                Width = 150
            };
            _menuBtn.Click += MenuBtnClicked;
            giveUpButton = new Button(this)
            {
                Text = "Vzdát",
                //Position = new Vector2(10, Game.VirtualScreenHeight / 2f + 30),
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 50, Game.VirtualScreenHeight / 2f - 90),
                Tag = 0,
                ZIndex = 100,
                //Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                //Width = 150
            };
            giveUpButton.Click += GtButtonClicked;
            giveUpButton.Hide();
            _editGameBtn = new Button(this)
            {
                Text = "Upravit hru",
                Position = new Vector2(10, Game.VirtualScreenHeight / 2f + 30),
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                Width = 150
            };
            _editGameBtn.Click += EditGameBtnClicked;
            _editGameBtn.Hide();
            _reviewGameBtn = new Button(this)
            {
                Text = "Průběh hry",
                Position = new Vector2(10, Game.VirtualScreenHeight / 2f + 90),
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main,
                Width = 150
            };
            _reviewGameBtn.Click += ReviewGameBtnClicked;
            _reviewGameBtn.Hide();
            //tlacitka na prave strane
            _sendBtn = new Button(this)
            {
                Text = "@",
                Position = new Vector2(Game.VirtualScreenWidth - 60, Game.VirtualScreenHeight / 2f - 150),
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Right : AnchorType.Main,
                Width = 50
            };
            _sendBtn.Click += SendBtnClicked;
            _reviewGameToggleBtn = new ToggleButton(this)
            {
                Text = "i",
                Position = new Vector2(Game.VirtualScreenWidth - 60, Game.VirtualScreenHeight / 2f - 90),
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Right : AnchorType.Main,
                Width = 50
            };
            //_reviewGameToggleBtn.Hide();
            _reviewGameToggleBtn.IsEnabled = false;
            _reviewGameToggleBtn.Click += ReviewGameBtnClicked;
            _hintBtn = new Button(this)
            {
                Text = "?",
                IsEnabled = false,
                Position = new Vector2(Game.VirtualScreenWidth - 60, Game.VirtualScreenHeight / 2f - 30),
                Width = 50,
                TextColor = Game.Settings.ButtonColor,
                BackgroundColor = Game.Settings.DefaultTextColor,
                BorderColor = Game.Settings.ButtonColor,
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Right : AnchorType.Main
            };
            _hintBtn.Click += HintBtnClicked;

//tlacitka ve hre
            _okBtn = new Button(this)
            {
                Text = "OK",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 50, Game.VirtualScreenHeight / 2f - 90),
                //Position = new Vector2(10, 60),
                IsEnabled = false,
                ZIndex = 100
            };
            _okBtn.Click += OkBtnClicked;
            _okBtn.Hide();
            gtHraButton = new Button(this)
            {
                Text = "Hra",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 325, Game.VirtualScreenHeight / 2f - 155),
                Tag = Hra.Hra,
                ZIndex = 100
            };
            gtHraButton.Click += GtButtonClicked;
            gtHraButton.Hide();
            gt7Button = new Button(this)
            {
                Text = "Sedma",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 215, Game.VirtualScreenHeight / 2f - 155),
                Tag = Hra.Hra | Hra.Sedma,
                ZIndex = 100
            };
            gt7Button.Click += GtButtonClicked;
            gt7Button.Hide();
            gt100Button = new Button(this)
            {
                Text = "Kilo",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 105, Game.VirtualScreenHeight / 2f - 155),
                Tag = Hra.Kilo,
                ZIndex = 100
            };
            gt100Button.Click += GtButtonClicked;
            gt100Button.Hide();
            gt107Button = new Button(this)
            {
                Text = "Stosedm",
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 5, Game.VirtualScreenHeight / 2f - 155),
                Tag = Hra.Kilo | Hra.Sedma,
                ZIndex = 100
            };
            gt107Button.Click += GtButtonClicked;
            gt107Button.Hide();
            gtBetlButton = new Button(this)
            {
                Text = "Betl",
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 115, Game.VirtualScreenHeight / 2f - 155),
                Tag = Hra.Betl,
                ZIndex = 100
            };
            gtBetlButton.Click += GtButtonClicked;
            gtBetlButton.Hide();
            gtDurchButton = new Button(this)
            {
                Text = "Durch",
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 225, Game.VirtualScreenHeight / 2f - 155),
                Tag = Hra.Durch,
                ZIndex = 100
            };
            gtDurchButton.Click += GtButtonClicked;
            gtDurchButton.Hide();
            gtButtons = new[] { gtHraButton, gt7Button, gt100Button, gt107Button, gtBetlButton, gtDurchButton };
            gfDobraButton = new Button(this)
            {
                Text = "Dobrá",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 155, Game.VirtualScreenHeight / 2f - 100),
                Tag = GameFlavour.Good,
                Width = 150,
                ZIndex = 100
            };
            gfDobraButton.Click += GfButtonClicked;
            gfDobraButton.Hide();
            gfSpatnaButton = new Button(this)
            {
                Text = "Špatná",
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 5, Game.VirtualScreenHeight / 2f - 100),
                Tag = GameFlavour.Bad,
                Width = 150,
                ZIndex = 100
            };
            gfSpatnaButton.Click += GfButtonClicked;
            gfSpatnaButton.Hide();
            gfButtons = new[] { gfDobraButton, gfSpatnaButton };
            _trumpLabel1 = new TextBox(this)
            {
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Top,
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 200, 5),
                Width = 400,
                Height = 60,
                ZIndex = 100,
                Anchor = AnchorType.Top,
                FontScaleFactor = 0.85f,
                HighlightedLine = 1,
                ShowVerticalScrollbar = false
            };
            _trumpLabel1.Hide();
            _trumpLabel2 = new TextBox(this)
            {
                HorizontalAlign = Game.Settings.DirectionOfPlay == DirectionOfPlay.Clockwise ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                VerticalAlign = VerticalAlignment.Top,
                Position = Game.Settings.DirectionOfPlay == DirectionOfPlay.Clockwise ? new Vector2(10, 5) : new Vector2(Game.VirtualScreenWidth - 410, 5),
                Width = 400,
                Height = 60,
                ZIndex = 100,
                Anchor = AnchorType.Top,
                FontScaleFactor = 0.85f,
                HighlightedLine = 1,
                ShowVerticalScrollbar = false
            };
            _trumpLabel2.Hide();
            _trumpLabel3 = new TextBox(this)
            {
                HorizontalAlign = Game.Settings.DirectionOfPlay == DirectionOfPlay.Clockwise ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Top,
                Position = Game.Settings.DirectionOfPlay == DirectionOfPlay.Clockwise ? new Vector2(Game.VirtualScreenWidth - 410, 5) : new Vector2(10, 5),
                Width = 400,
                Height = 60,
                ZIndex = 100,
                Anchor = AnchorType.Top,
                FontScaleFactor = 0.85f,
                HighlightedLine = 1,
                ShowVerticalScrollbar = false
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
                TextColor = Game.Settings.HighlightedTextColor,
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
                TextColor = Game.Settings.HighlightedTextColor,
                ZIndex = 100,
                FontScaleFactor = 0.9f
            };
            _msgLabelLeft = new Label(this)
            {
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Top,
                Position = new Vector2(200, 140),
                Width = (int)Game.VirtualScreenWidth - 245,
                Height = (int)Game.VirtualScreenHeight - 210,
                TextColor = Game.Settings.HighlightedTextColor,
                ZIndex = 98,
                FontScaleFactor = 0.9f
            };
            _msgLabelLeftOrigPosition = new Vector2(200, 140);
            _msgLabelLeftHiddenPosition = new Vector2(200, 140 - Game.VirtualScreenHeight);
            _msgLabelRight = new Label(this)
            {
                HorizontalAlign = HorizontalAlignment.Right,
                VerticalAlign = VerticalAlignment.Top,
                Position = new Vector2(165, 140),
                Width = (int)Game.VirtualScreenWidth - 280,
                Height = (int)Game.VirtualScreenHeight - 210,
                TextColor = Game.Settings.HighlightedTextColor,
                ZIndex = 100,
                FontScaleFactor = 0.9f
            };
            _msgLabelRightOrigPosition = new Vector2(160, 140);
            _msgLabelRightHiddenPosition = new Vector2(160, 140 - Game.VirtualScreenHeight);
            _gameResult = new TextBox(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 125, 80),
                Width = 250,
                Height = 50,
                BackgroundColor = new Color(0x40, 0x40, 0x40),
                TextColor = Game.Settings.HighlightedTextColor,
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
                TextColor = Game.Settings.HighlightedTextColor,
                BorderColor = Game.Settings.Player1Color,
                Opacity = 0.8f,
                VerticalAlign = VerticalAlignment.Middle,
                HorizontalAlign = HorizontalAlignment.Center,
                ZIndex = 100
            };
            _bubble1.Hide();
            _bubble2 = new TextBox(this)
            {
                Position = Game.Settings.DirectionOfPlay == DirectionOfPlay.Clockwise ? new Vector2(10, 80) : new Vector2(Game.VirtualScreenWidth - 260, 80),
                Width = 250,
                Height = 50,
                BackgroundColor = new Color(0x40, 0x40, 0x40),
                TextColor = Game.Settings.HighlightedTextColor,
                BorderColor = Game.Settings.Player2Color,
                Opacity = 0.8f,
                VerticalAlign = VerticalAlignment.Middle,
                HorizontalAlign = HorizontalAlignment.Center,
                ZIndex = 100
            };
            _bubble2.Hide();
            _bubble3 = new TextBox(this)
            {
                Position = Game.Settings.DirectionOfPlay == DirectionOfPlay.Clockwise ? new Vector2(Game.VirtualScreenWidth - 260, 80) : new Vector2(10, 80),
                Width = 250,
                Height = 50,
                BackgroundColor = new Color(0x40, 0x40, 0x40),
                TextColor = Game.Settings.HighlightedTextColor,
                BorderColor = Game.Settings.Player3Color,
                Opacity = 0.8f,
                VerticalAlign = VerticalAlignment.Middle,
                HorizontalAlign = HorizontalAlignment.Center,
                ZIndex = 100
            };
            _bubble3.Hide();
            _bubbles = new[] { _bubble1, _bubble2, _bubble3 };
            _bubbleAutoHide = new[] { false, false, false };
            flekBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 230, Game.VirtualScreenHeight / 2f - 155),
                Width = 150,
                Height = 50,
                Tag = Hra.Hra | Hra.Kilo | Hra.Betl | Hra.Durch,
                ZIndex = 100
            };

            //flekBtn.Click += BidButtonClicked;
            flekBtn.TouchDown += (sender, tl) => BidButtonClicked(sender);
            flekBtn.Hide();
            sedmaBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 75, Game.VirtualScreenHeight / 2f - 155),
                Width = 150,
                Height = 50,
                Tag = Hra.Sedma | Hra.SedmaProti,
                ZIndex = 100
            };
            //sedmaBtn.Click += BidButtonClicked;
            sedmaBtn.TouchDown += (sender, tl) => BidButtonClicked(sender);
            sedmaBtn.Hide();
            kiloBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 80, Game.VirtualScreenHeight / 2f - 155),
                Width = 150,
                Height = 50,
                Tag = Hra.KiloProti,
                ZIndex = 100
            };
            //kiloBtn.Click += BidButtonClicked;
            kiloBtn.TouchDown += (sender, tl) => BidButtonClicked(sender);
            kiloBtn.Hide();
            bidButtons = new[] { flekBtn, sedmaBtn, kiloBtn };
            _progress1 = new ProgressIndicator(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 75, 0),
                Width = 150,
                Height = 8,
                Color = Game.Settings.Player1Color,
                ZIndex = 100,
                Anchor = AnchorType.Top
            };
            if (!Game.Settings.HintEnabled)
            {
                _progress1.Hide();
            }
            _progress2 = new ProgressIndicator(this)
            {
                Position = Game.Settings.DirectionOfPlay == DirectionOfPlay.Clockwise ? new Vector2(5, 0) : new Vector2(Game.VirtualScreenWidth - 155, 0),
                Width = 150,
                Height = 8,
                Color = Game.Settings.Player2Color,
                ZIndex = 100,
                Anchor = AnchorType.Top
            };
            _progress3 = new ProgressIndicator(this)
            {
                Position = Game.Settings.DirectionOfPlay == DirectionOfPlay.Clockwise ? new Vector2(Game.VirtualScreenWidth - 155, 0) : new Vector2(5, 0),
                Width = 150,
                Height = 8,
                Color = Game.Settings.Player3Color,
                ZIndex = 100,
                Anchor = AnchorType.Top
            };
            _progressBars = new[] { _progress1, _progress2, _progress3 };

            _shuffledCards = new Sprite[3];
            for (var i = 0; i < _shuffledCards.Length; i++)
            {
                _shuffledCards[i] = new Sprite(this, Game.ReverseTexture, Game.BackSideRect)
                {
                    Scale = Game.CardScaleFactor,
                    Name = string.Format("card{0}", i)
                };
                _shuffledCards[i].Hide();
            }
            _review = new GameReview(this, 200)
            {
                Position = new Vector2(160, 45),
                Width = (int)Game.VirtualScreenWidth - 160,
                Height = (int)Game.VirtualScreenHeight - 55,
                BackgroundColor = g != null && g.IsRunning ? Color.Black : Color.Transparent,
                ZIndex = 100
            };
            _review.Hide();
            UpdateControlsPositions();

            Task.Run(() =>
            {
                LoadHistory();
                Game.LoadGameSettings(false);
                SettingsChanged(this, new SettingsChangedEventArgs() { Settings = Game.Settings });
                BackgroundTint = Color.DimGray;
                ClearTable(true);
            });
        }

        /// <summary>
        /// Gets the file stream. Callback function used from Mariasek.Engine.Game
        /// </summary>
        public Stream GetFileStream(string filename)
        {
            var path = Path.Combine(MariasekMonoGame.RootPath, filename);

            Game.StorageAccessor.GetStorageAccess();
            CreateDirectoryForFilePath(path);

            try
            {
                var fs = new FileStream(path, FileMode.Create);

                return fs;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Cannot create file stream\n{0}", ex.Message));
                return new MemoryStream();
            }
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
				Game.StorageAccessor.GetStorageAccess();
                using (var reader = new StreamReader(_historyFilePath))
                {
                    using(var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        //csv.Configuration.RegisterClassMap<MoneyCalculatorBaseMap>();
                        ////Game.Money = csv.GetRecords<MoneyCalculatorBase>().ToList();
                        //Game.Money = Game.Settings.CalculationStyle == CalculationStyle.Adding
                        //                ? csv.GetRecords<AddingMoneyCalculator>().Select(i => (MoneyCalculatorBase)i).ToList()
                        //                : csv.GetRecords<MultiplyingMoneyCalculator>().Select(i => (MoneyCalculatorBase)i).ToList();
                        csv.Read();
                        csv.ReadHeader();
                        while(csv.Read())
                        {
                            var money = new AddingMoneyCalculator();
                            money.GameId = csv.GetField<int>(0);
                            money.GameTypeString = csv.GetField<string>(1);
                            money.GameTypeConfidence = csv.GetField<float>(2);
                            money.MoneyWon = new[]
                            {
                                 csv.GetField<int>(3),
                                 csv.GetField<int>(4),
                                 csv.GetField<int>(5)
                            };

                            Game.Money.Add(money);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Cannot load CSV history\n{0}", e.Message);
                try
                {
                    var xml = new XmlSerializer(typeof(List<MoneyCalculatorBase>));

                    Game.StorageAccessor.GetStorageAccess();
                    using (var fs = File.Open(_historyFilePath, FileMode.Open))
                    {
                        Game.Money = (List<MoneyCalculatorBase>)xml.Deserialize(fs);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Cannot load XML history\n{0}", ex.Message);
                }
            }
        }

        public void SaveHistory()
        {
            try
            {
                Game.StorageAccessor.GetStorageAccess();
                using (var sw = new StreamWriter(_historyFilePath))
                {
                    using (var csv = new CsvWriter(sw, CultureInfo.InvariantCulture))
                    {
                        //csv.Configuration.RegisterClassMap<MoneyCalculatorBaseMap>();
                        //csv.WriteRecords<MoneyCalculatorBase>(Game.Money);
                        csv.WriteField("GameId");
                        csv.WriteField("GameTypeString");
                        csv.WriteField("GameTypeConfidence");
                        csv.WriteField("MoneyWon1");
                        csv.WriteField("MoneyWon2");
                        csv.WriteField("MoneyWon3");
                        csv.NextRecord();
                        foreach(var money in Game.Money)
                        {
                            csv.WriteField(money.GameId);
                            csv.WriteField(money.GameTypeString.TrimEnd());
                            csv.WriteField(money.GameTypeConfidence);
                            csv.WriteField(money.MoneyWon[0]);
                            csv.WriteField(money.MoneyWon[1]);
                            csv.WriteField(money.MoneyWon[2]);
                            csv.NextRecord();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Cannot save CSV history\n{0}", e.Message);
                try
                {
                    var xml = new XmlSerializer(typeof(List<MoneyCalculatorBase>));

                    Game.StorageAccessor.GetStorageAccess();
                    CreateDirectoryForFilePath(_historyFilePath);
                    using (var fs = File.Open(_historyFilePath, FileMode.Create))
                    {
                        xml.Serialize(fs, Game.Money);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Cannot save XML history\n{0}", ex.Message);
                }
            }
        }

        public void LoadDeck()
        {
            _deck = new Deck();
            try
            {
				Game.StorageAccessor.GetStorageAccess();
                using (var fs = File.Open(_deckFilePath, FileMode.Open))
                {
                    _deck.LoadDeck(fs);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Cannot load deck\n{0}", e.Message));
                _deck.Init();
                _deck.Shuffle();
            }
        }

        public void SaveDeck(Deck deck = null)
        {
            try
            {
                deck = deck ?? _deck;
				Game.StorageAccessor.GetStorageAccess();
                CreateDirectoryForFilePath(_deckFilePath);
                using (var fs = File.Open(_deckFilePath, FileMode.Create))
                {
                    deck.SaveDeck(fs);
                }
            }
            catch (Exception e)
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
        
        private string GetBaseFileName(string path, Hra gameType, out int count)
        {
            count = GetGameCount(path) + 1;
            var gt = string.Empty;

            if ((gameType & Hra.Sedma) != 0)
            {
                if ((gameType & Hra.Hra) != 0)
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
                if ((gameType & Hra.Hra) != 0)
                {
                    gt = "hra";
                }
                else if ((gameType & Hra.Kilo) != 0)
                {
                    gt = "kilo";
                }
                else if ((gameType & Hra.Betl) != 0)
                {
                    gt = "betl";
                }
                else if ((gameType & Hra.Durch) != 0)
                {
                    gt = "durch";
                }
            }
            return string.Format("{0:0000}-{1}.{2}", count, gt, DateTime.Now.ToString("yyyyMMdd"));
        }

        public int ArchiveGame()
        {
            try
            {
                int counter;
                var baseFileName = GetBaseFileName(_archivePath, g.GameType, out counter);
                var newGameArchivePath = Path.Combine(_archivePath, string.Format("{0}.def.hra", baseFileName));
                var endGameArchivePath = Path.Combine(_archivePath, string.Format("{0}.end.hra", baseFileName));

                CreateDirectoryForFilePath(newGameArchivePath);
                FileCopy(_newGameFilePath, newGameArchivePath);
                FileCopy(_endGameFilePath, endGameArchivePath);

                return counter;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Cannot archive game\n{0}", e.Message);
                return -1;
            }
        }

        public void CancelRunningTask(Task gameTask)
        {
            ClearActionQueue();
            if (gameTask != null && gameTask.Status == TaskStatus.Running)
            {
                ClearTable(true);
                try
                {
                    (g.players[0] as HumanPlayer).CancelAiTask();
                    _cancellationTokenSource.Cancel();
                    _preGameEvent.Set();
                    _evt.Set();
                    gameTask.Wait();
                }
                catch (Exception)
                {
                    //exception caught during task cancellation
                }
            }
            //_cancellationTokenSource = new CancellationTokenSource();
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
            if (!Game.Settings.Calculate107Separately &&
                ((g.GameType == (Hra.Kilo | Hra.Sedma) &&
                  (bids & Hra.Kilo) != 0 &&
                  (bids & Hra.Sedma) != 0) ||
                ((g.GameType & Hra.KiloProti) != 0 &&
                 (g.GameType & Hra.SedmaProti) != 0 &&
                 (bids & Hra.KiloProti) != 0 &&
                 (bids & Hra.SedmaProti) != 0)))
            {
                sedmaBtn.IsEnabled = false;
            }
            kiloBtn.IsEnabled = (bids & Hra.KiloProti) != 0 &&
                                (bidding.HundredAgainstLastBidder == null ||
                                 bidding.HundredAgainstLastBidder.PlayerIndex != g.players[0].TeamMateIndex);

            flekBtn.Text = Bidding.MultiplierToString((g.GameType & (Hra.Betl | Hra.Durch)) == 0 ? bidding.GameMultiplier * 2 : bidding.BetlDurchMultiplier * 2);
            sedmaBtn.Text = (g.GameType & Hra.Sedma) == 0 && (_bidding.SevenAgainstMultiplier == 0) ? "Sedma proti" : "Na sedmu";
			kiloBtn.Text = "Kilo proti";//(g.GameType & Hra.Kilo) == 0 && (_bidding.HundredAgainstMultiplier == 0) ? "Kilo proti" : "Na kilo";

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
            //if (_review != null)
            //{
            //    Children.Remove(_review);
            //    _review.Dispose();
            //}
            //_review = new GameReview(this)
            //{
            //    Position = new Vector2(160, 45),
            //    Width = (int)Game.VirtualScreenWidth - 160,
            //    Height = (int)Game.VirtualScreenHeight - 55,
            //    BackgroundColor = g.IsRunning ? Color.Black : Color.Transparent,
            //    ZIndex = 200
            //};
            //if (hidden)
            //{
            //    _review.Hide();
            //}
        }

        public void EditGameBtnClicked(object sender)
        {
            //pokud nehrajeme hru z editoru, tak nahraj aktualni hru do editoru
            if (!_testGame)
            {
                Game.EditorScene.LoadGame(_newGameFilePath);
            }
            Game.EditorScene.SetActive();
        }

        public void ReviewGameBtnClicked(object sender)
        {
            if (g.IsRunning &&
                (!Game.Settings.TestMode.HasValue || !Game.Settings.TestMode.Value))
            {
                if (_reviewGameToggleBtn.IsSelected)
                {
                    ShowLastTrick();
                }
                else
                {
                    _reviewGameToggleBtn.IsSelected = false;
                }
                return;
            }

            var origPosition = new Vector2(160, 45);
            var hiddenPosition = new Vector2(160, 45 + Game.VirtualScreenHeight);

            RunOnUiThread(() =>
            {
                if (!_review.IsVisible)
                {
                    //show
                    for (var i = 0; i < _trumpLabels.Count(); i++)
                    {
                        _trumpLabels[i].Height = 30; //aby neprekazel _review
                    }
                    if (g.IsRunning)
                    {
                        _review.Position = origPosition;
                        _review.BackgroundColor = Color.Black;
                        _review.UpdateReview(g);
                        _review.Opacity = 0f;
                        _review.Show();
                        _review.FadeIn(4f);
                    }
                    else
                    {
                        _reviewGameBtn.Text = "Vyúčtování";
                        HideGameScore();
                        _review.Position = origPosition;
                        _review.BackgroundColor = Color.Transparent;
                        _review.UpdateReview(g);
                        _review.Position = hiddenPosition;
                        _review.Opacity = 1f;
                        _review.Show();
                        _review.MoveTo(origPosition, 2000);
                    }
                    _hand.IsEnabled = false;
                }
                else
                {
                    //hide
                    for (var i = 0; i < _trumpLabels.Count(); i++)
                    {
                        _trumpLabels[i].Height = 60; //vratit na puvodni vysku
                    }
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
                        _review.MoveTo(hiddenPosition, 2000)
                               .Invoke(() =>
                        {
                            _review.Hide();
                        });
                        ShowGameScore();
                    }
                }
            });
        }

        private void CleanUpOldGame()
        {
            if (g != null)
            {
                try
                {
                    g.Die();
                }
                catch (Exception ex)
                {
                }
            }
            _bidding = null;
            _gameFlavourChosenEventArgs = null;
            g = null;
            GC.Collect();
            System.Diagnostics.Debug.WriteLine("<<< CleanUpOldGame()");
        }

        public void RepeatGameBtnClicked(object sender)
        {
            ReplayGame(_newGameFilePath);
        }

        //public void RepeatGameOptionBtnClicked(object sender)
        public void RepeatGameOptionBtnTouchUp(object sender, TouchLocation tl)
        {
            var origPosition2 = _repeatGameAsPlayer2Btn.Position;
            var hiddenPosition2 = _repeatGameBtn.Position;
            var origPosition3 = _repeatGameAsPlayer3Btn.Position;
            var hiddenPosition3 = _repeatGameBtn.Position;

            _repeatGameAsPlayer2Btn.Text = string.Format("Jako {0}", Game.Settings.PlayerNames[1]);
            _repeatGameAsPlayer3Btn.Text = string.Format("Jako {0}", Game.Settings.PlayerNames[2]);
            _repeatGameOptionBtn.Hide();
            _repeatGameAsPlayer2Btn.Position = hiddenPosition2;
            _repeatGameAsPlayer3Btn.Position = hiddenPosition3;
            _repeatGameAsPlayer2Btn.Show();
            _repeatGameAsPlayer3Btn.Show();
            _repeatGameAsPlayer2Btn.MoveTo(origPosition2, 2000)
                                  .Wait(2000)
                                  .MoveTo(hiddenPosition2, 2000)
                                  .Invoke(() =>
                                  {
                                      _repeatGameAsPlayer2Btn.Hide();
                                      _repeatGameAsPlayer2Btn.Position = origPosition2;
                                  });
            _repeatGameAsPlayer3Btn.MoveTo(origPosition3, 2000)
                                  .Wait(2000)
                                  .MoveTo(hiddenPosition3, 2000)
                                  .Invoke(() =>
                                  {
                                      _repeatGameAsPlayer3Btn.Hide();
                                      _repeatGameAsPlayer3Btn.Position = origPosition3;
                                      if (_repeatGameBtn.IsVisible)
                                      {
                                          _repeatGameOptionBtn.Show();
                                      }
                                  });
        }

        public void RepeatGameAsPlayer2BtnClicked(object sender)
        {
            ReplayGame(_newGameFilePath, 1);
        }

        public void RepeatGameAsPlayer3BtnClicked(object sender)
        {
            ReplayGame(_newGameFilePath, 2);
        }

        //vola se z menu (micha vzdy)
        public void ShuffleDeck()
        {
            //if (_deck == null)
            //{
            //    LoadDeck();
            //}
            //else if (_deck.IsEmpty())
            //{
            //    _deck = g.GetDeckFromLastGame();
            //    if (_deck.IsEmpty())
            //    {
            //        LoadDeck();
            //    }
            //}
            _deck = new Deck();
            _deck.Shuffle();
            Task.Run(() => SaveDeck());
        }

        public void NewGameBtnClicked(object sender)
        {
            if (_shuffleAnimationRunning)
            {
                if (!this.ScheduledOperations.IsEmpty)
                {
                    ClearOperations();
                }
                _shuffledCards[0].Hide();
                _shuffledCards[1].Hide();
                _shuffledCards[2].Hide();
                _cancellationTokenSource.Cancel();
            }
            _shuffleEvent.Set(); //odblokovat pripadnou predchozi hru cekajici na konec shuffle animace
            if (!_gameSemaphore.Wait(0))
            {
                return;
            }
            var gameTask = _gameTask;

            _trumpLabel1.Hide();
            _trumpLabel2.Hide();
            _trumpLabel3.Hide();
            _newGameBtn.Hide();
            _repeatGameBtn.ClearOperations();
            _repeatGameBtn.Hide();
            _repeatGameOptionBtn.Hide();
            _repeatGameAsPlayer2Btn.Hide();
            _repeatGameAsPlayer3Btn.Hide();
            _testGame = false;
            var cancellationTokenSource = new CancellationTokenSource();
            _gameTask = Task.Run(() => CancelRunningTask(gameTask))
                            .ContinueWith(cancellationTask =>
             {
                 _preGameEvent.Reset();
                 try
                 {
                     CleanUpOldGame();
                     _cancellationTokenSource = cancellationTokenSource;
                     try
                     {
                         if (File.Exists(_errorFilePath))
                         {
                             File.Delete(_errorFilePath);
                         }
                     }
                     catch (Exception ex)
                     {
                        ShowMsgLabel(ex.Message, false);
                     }
                     g = new Mariasek.Engine.Game()
                     {
                         BaseBet = Game.Settings.BaseBet,
                         Locale = Game.Settings.Locale,
                         MaxWin = Game.Settings.MaxWin,
                         SkipBidding = false,
                         MinimalBidsForGame = Game.Settings.MinimalBidsForGame,
                         MinimalBidsForSeven = Game.Settings.MinimalBidsForSeven,
                         CalculationStyle = Game.Settings.CalculationStyle,
                         Top107 = Game.Settings.Top107,
                         PlayZeroSumGames = Game.Settings.PlayZeroSumGames,
                         Calculate107Separately = Game.Settings.Calculate107Separately,
                         HlasConsidered = Game.Settings.HlasConsidered,
                         AutoDisable100Against = Game.Settings.AutoDisable100Against,
                         GetFileStream = GetFileStream,
                         GetVersion = () => MariasekMonoGame.Version,
                         GameValue = Game.Settings.GameValue,
                         QuietSevenValue = Game.Settings.QuietSevenValue,
                         SevenValue = Game.Settings.SevenValue,
                         QuietHundredValue = Game.Settings.QuietHundredValue,
                         HundredValue = Game.Settings.HundredValue,
                         BetlValue = Game.Settings.BetlValue,
                         DurchValue = Game.Settings.DurchValue,
                         FirstMinMaxRound = Game.Settings.FirstMinMaxRound,
                         AllowFakeSeven = Game.Settings.AllowFakeSeven,
                         AllowFake107 = Game.Settings.AllowFake107,
                         AllowAXTalon = Game.Settings.AllowAXTalon,
                         AllowTrumpTalon = Game.Settings.AllowTrumpTalon,
                         AllowAIAutoFinish = Game.Settings.AllowAIAutoFinish,
                         AllowPlayerAutoFinish = Game.Settings.AllowPlayerAutoFinish,
                         PreGameHook = () => _preGameEvent.WaitOne(),
                         CurrencyFormat = Game.CurrencyFormat
                     };
                     g.RegisterPlayers(
                         new HumanPlayer(g, _aiSettings, this, Game.Settings.HintEnabled) { Name = Game.Settings.PlayerNames[0] },
                         new AiPlayer(g, _aiSettings) { Name = Game.Settings.PlayerNames[1] },
                         new AiPlayer(g, _aiSettings) { Name = Game.Settings.PlayerNames[2] }
                     );
                     CurrentStartingPlayerIndex = Game.Settings.CurrentStartingPlayerIndex; //TODO: zrusit CurrentStartingPlayerIndex a pouzivat jen Game.Settings.CurrentStartingPlayerIndex
                     CurrentStartingPlayerIndex = (CurrentStartingPlayerIndex + 1) % Mariasek.Engine.Game.NumPlayers;
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
                         if (_deck.IsEmpty())
                         {
                             LoadDeck();
                         }
                     }
                     if (Game.Settings.WhenToShuffle == ShuffleTrigger.Always)
                     {
                         _shouldShuffle = true;
                     }
                     if (_shouldShuffle)
                     {
                         _deck.Shuffle();
                     }
                     _canSort = Game.Settings.AutoSort;
                     _canSortTrump = false;
                     _firstTimeTalonCardClick = true;
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
                     _cardClicked = null;
                     _gameTypeChosen = 0;
                     _gameFlavourChosen = 0;
                     _bid = 0;
                     TrumpCardTakenBack = false;
                     _state = GameState.NotPlaying;
                     _talon = new List<Card>();

                     ClearTable(true);
                     HideMsgLabel();
                     _reviewGameBtn.Hide();
                     _reviewGameToggleBtn.Show();
                     _reviewGameToggleBtn.IsSelected = false;
                     _reviewGameToggleBtn.IsEnabled = Game.Settings.TestMode.HasValue && Game.Settings.TestMode.Value;
                     _editGameBtn.Hide();

                     if (_review != null)
                     {
                         _review.Hide();
                     }
                     foreach (var btn in gtButtons)
                     {
                         btn.BorderColor = Game.Settings.DefaultTextColor;
                         btn.Hide();
                     }
                     giveUpButton.BorderColor = Game.Settings.DefaultTextColor;
                     giveUpButton.Hide();
                     foreach (var btn in gfButtons)
                     {
                         btn.BorderColor = Game.Settings.DefaultTextColor;
                         btn.Hide();
                     }
                     foreach (var btn in bidButtons)
                     {
                         btn.BorderColor = Game.Settings.DefaultTextColor;
                         btn.Hide();
                     }
                     foreach (var bubble in _bubbles)
                     {
                         bubble.Hide();
                     }
                     AmendCardScaleFactor();
                     RunOnUiThread(() =>
                     {
                         _hand.ClearOperations();
                         this.ClearOperations();
                         _shuffledCards[0].Hide();
                         _shuffledCards[1].Hide();
                         _shuffledCards[2].Hide();
                         if (_shouldShuffle)
                         {
                             _shuffleEvent.Reset();
                             ShowShuffleAnimation();
                         }
                         this.Invoke(() =>
                         {
                             if (g.GameStartingPlayerIndex != 0)
                             {
                                 ShowThinkingMessage(g.GameStartingPlayerIndex);
                                 SortHand();
                                 _hand.Hide();
                                 UpdateHand(true);
                             }
                             else
                             {
                                 SortHand(null, 7);
                                 _hand.Hide();
                             }
                             _shuffleEvent.Set();
                        });
                     });
                     _hintBtn.IsEnabled = false;
                     _hintBtn.Show();
                     //if (g.GameStartingPlayerIndex != 0)
                     //{
                     //    ShowThinkingMessage(g.GameStartingPlayerIndex);
                     //    SortHand();
                     //    _hand.Hide();
                     //    UpdateHand(true);
                     //}
                     //else
                     //{
                     //    SortHand(null, 7);
                     //    _hand.Hide();
                     //}
                     for (var i = 0; i < _trumpLabels.Count(); i++)
                     {
                         var sum = Game.Money.Sum(j => j.MoneyWon[i]) * Game.Settings.BaseBet;
                         //_trumpLabels[i].Text = g.players[i].Name;
                         _trumpLabels[i].Text = string.Format("{0}\n{1}",
                                                  GetTrumpLabelForPlayer(g.players[i].PlayerIndex),
                                                  Game.Settings.ShowScoreDuringGame
                                                  ? sum.ToString("C", Game.CurrencyFormat)
                                                  : string.Empty);
                         _trumpLabels[i].Height = 60;
                         if (Game.Settings.WhiteScore)
                         {
                             _trumpLabels[i].HighlightColor = Game.Settings.DefaultTextColor;
                         }
                         else
                         {
                             _trumpLabels[i].HighlightColor = sum > 0
                             ? Game.Settings.PositiveScoreColor
                             : sum < 0
                             ? Game.Settings.NegativeScoreColor
                             : Game.Settings.DefaultTextColor;
                         }
                         _trumpLabels[i].Show();
                     }
                     _hlasy[0][0].Position = new Vector2(Game.VirtualScreenWidth - 100, Game.VirtualScreenHeight / 2f + 20);
                     try
                     {
                         Game.StorageAccessor.GetStorageAccess();
                         if (File.Exists(_endGameFilePath))
                         {
                             File.Delete(_endGameFilePath);
                         }
                         if (File.Exists(_savedGameFilePath))
                         {
                             File.Delete(_savedGameFilePath);
                         }
                     }
                     catch (Exception e)
                     {
                         System.Diagnostics.Debug.WriteLine(string.Format("Cannot delete old end of game file\n{0}", e.Message));
                     }
                 }
                 catch(Exception e)
                 {
                     ShowMsgLabel(e.Message, false);
                 }
                 finally
                 {
                     if (_gameSemaphore.CurrentCount == 0)
                     {
                         _gameSemaphore.Release();
                     }
                 }
                 _shuffleEvent.WaitOne();
                 if (!_cancellationTokenSource.Token.IsCancellationRequested)
                 {
                     _lastGameWasLoaded = false;
                     try
                     {
                         g.PlayGame(_cancellationTokenSource.Token);
                     }
                     catch (Exception e)
                     {
                         ShowMsgLabel(e.Message, false);
                     }
                 }
             }, cancellationTokenSource.Token);
        }

        private void AmendCardScaleFactor()
        {
            foreach (var hlasy in _hlasy)
            {
                foreach (var card in hlasy)
                {
                    card.Scale = Game.CardScaleFactor;
                }
            }
            foreach (var card in _stychy)
            {
                card.Scale = Game.CardScaleFactor;
            }
            foreach (var card in _stareStychy)
            {
                card.Scale = Game.CardScaleFactor;
            }
            foreach (var card in _cardsPlayed)
            {
                card.Scale = Game.CardScaleFactor;
            }
            _hand.Scale = Game.CardScaleFactor;
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
                            if (e.winner.PlayerIndex != g.GameStartingPlayerIndex)
                            {
                                ShowMsgLabel("Zbytek jde za mnou", false);
                            }
                            else
                            {
                                ShowMsgLabel("Už mě nechytíte", false);
                            }
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
                        for (var i = e.roundNumber - 1; i < Mariasek.Engine.Game.NumRounds && g.rounds[i] != null; i++)
                        {
                            winningCards.Add(g.rounds[i].c1);
                        }
                    }
                    if ((g.GameType & (Hra.Betl | Hra.Durch)) != 0)
                    {
                        winningCards = winningCards.Sort(SortMode.Descending, true).ToList();
                    }
                    _winningHand = new GameComponents.Hand(this, winningCards.ToArray())
                    {
                        ZIndex = 50
                    };
                    _winningHand.ShowWinningHand(e.winner.PlayerIndex);
                    _winningHand.Show();
                    _state = GameState.RoundFinished;
                });
            WaitForUIThread();
            ClearTable(true);
        }

        public void GameException(object sender, GameExceptionEventArgs e)
        {
            try
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
                var subject = $"Mariášek crash report v{MariasekMonoGame.Version} ({MariasekMonoGame.Platform})";
                var msg1 = string.Format("Chyba:\n{0}\nOdesílám zprávu...", ex.Message.Split('\n').First()+'n'+ex.StackTrace.Split('\n').Take(5));
                var msg2 = string.Format("{0}\n{1}\n{2}\n{3}\n-\n{4}", 
                                         subject, ex.Message, ex.StackTrace, 
                                         g?.DebugString?.ToString() ?? string.Empty,
                                         g?.BiddingDebugInfo?.ToString() ?? string.Empty);

                if (_gameSemaphore != null && _gameSemaphore.CurrentCount == 0)
                {
                    _gameSemaphore.Release();
                }
                ShowMsgLabel(msg1, false);

                if (Game.EmailSender != null)
                {
                    Game.EmailSender.SendEmail(
                        new[] { "mariasek.app@gmail.com" },
                        subject, msg2,
                        new[] { _newGameFilePath, _errorFilePath, _errorMsgFilePath, SettingsScene._settingsFilePath });
                }
            }
            catch
            {                
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
                //RefreshReview(true);
                _review.UpdateReview(g);
#if !__IOS__
                using (var fs = GetFileStream(Path.GetFileName(_screenPath)))
                {
                    var target = _review.SaveTexture();
                    target.SaveAsPng(fs, target.Width, target.Height);
                }
#endif

                var subject = $"Mariášek: komentář v{MariasekMonoGame.Version} ({MariasekMonoGame.Platform})";
				if (g != null && g.IsRunning)
                {
                    using (var fs = GetFileStream(Path.GetFileName(_savedGameFilePath)))
                    {
                        g.SaveGame(fs, saveDebugInfo: true);
                    }
                    Game.EmailSender.SendEmail(new[] { "mariasek.app@gmail.com" }, subject, "Sdělte mi prosím své dojmy nebo komentář ke konkrétní hře\n:",
                                               new[] { _screenPath, _newGameFilePath, _savedGameFilePath, SettingsScene._settingsFilePath });
                }
                else
                {
                    Game.EmailSender.SendEmail(new[] { "mariasek.app@gmail.com" }, subject, "Sdělte mi prosím své dojmy nebo komentář ke konkrétní hře\n:",
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
            if (cardClicked == null)
            {
                throw new InvalidDataException($"CardClicked is null, state: {_state}");
            }
			switch (_state)
            {
                case GameState.ChooseTalon:
                    if (button.IsBusy || (button.IsFaceUp && _talon.Count == 2))
                    {
                        //do talonu nemuzu pridat kdyz je plnej
                        return;
                    }
                    if (_firstTimeTalonCardClick)
                    {
                        _firstTimeTalonCardClick = false;
                        _canSort = !Game.Settings.AutoSort;
                        _canSortTrump = true;
                        if (_canSort && Game.Settings.SortMode != SortMode.None)
                        {
                            SortHand(_trumpCardChosen);
                            return;
                        }
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
                    if (!TrumpCardTakenBack)
                    {
                        if (_talon.Any(i => !g.IsValidTalonCard(i)))
                        {
                            _msgLabelSmall.Text = "\n\nS tímto talonem musíš hrát betl nebo durch";
                            _msgLabelSmall.Show();
                        }
                        else
                        {
                            _msgLabelSmall.Hide();
                        }
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
                    if (g.GameStartingPlayerIndex != 0 && 
                        g.RoundNumber == 0)
                    {
                        _canSort = !Game.Settings.AutoSort;
                        SortHand();
                    }
                    return;
            }
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
                TrumpCardTakenBack = true;
                _talon.Clear();
                _okBtn.IsEnabled = false;
                button.Hide();
				button.Position = origPosition;
				UpdateHand();

                _msgLabelSmall.Text = "\n\nBez trumfů musíš hrát betl nebo durch";
                _msgLabelSmall.Show();
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
            giveUpButton.Hide();
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
            //_hand.IsEnabled = false;
			_cardClicked = null;
			_evt.Reset();
            RunOnUiThread(() =>
            {
                ShowMsgLabel("Vyber trumfovou kartu", false);
                _hand.Show();
                UpdateHand(flipCardsUp: true, cardsNotRevealed: 5);
                _hand.IsEnabled = true;
                _hand.AllowDragging();
            });
            WaitForUIThread();

            _state = GameState.NotPlaying;
			RunOnUiThread(() =>
            {
                _hintBtn.IsEnabled = false;
                //_hand.IsEnabled = false;
                _hand.ForbidDragging();
            });

            return _cardClicked;
        }

        public List<Card> ChooseTalon()
        {
			EnsureBubblesHidden();
			g.ThrowIfCancellationRequested();
			//_hand.IsEnabled = false;
			_cardClicked = null;
            _talon = new List<Card>();
			_evt.Reset();
            RunOnUiThread(() =>
            {
                ShowMsgLabel("Vyber si talon", false);
                if (_trumpCardChosen != null)// &&
                    //(Game.Money == null || Game.Money.Count() % 5 == 0))
                {
                    _msgLabelSmall.Text = "\n\n\nTrumfovou kartu můžeš přetáhnout zpět do ruky";
                    _msgLabelSmall.Show();
                    this.Wait(3000)
                        .Invoke(() =>
                    {
                        _msgLabelSmall.Hide();
                    });
                }
                _okBtn.Show();
                _okBtn.IsEnabled = false;
                _state = GameState.ChooseTalon;
                if (_trumpCardChosen != null)
                {
                    UpdateHand(true, cardToHide: _trumpCardChosen); //abych si otocil zbyvajicich 5 karet
                }
                else
                {
                    UpdateHand(); //abych videl jaky talon mi prisel po zahlaseni spatne barvy
                }
                _hand.IsEnabled = true;
            });
            WaitForUIThread();

			_state = GameState.NotPlaying;
			RunOnUiThread(() =>
            {
                _hintBtn.IsEnabled = false;
                //_hand.IsEnabled = false;
                _okBtn.Hide();
            });

            return _talon;
        }

        public GameFlavour ChooseGameFlavour()
        {
			EnsureBubblesHidden();
			g.ThrowIfCancellationRequested();
			//_hand.IsEnabled = false;
			_gameFlavourChosen = (GameFlavour)(-1);
			_evt.Reset();
			RunOnUiThread(() =>
    		{
	    		UpdateHand(); //abych nevidel karty co jsem hodil do talonu
                this.Invoke(() =>
                {
					_state = GameState.ChooseGameFlavour;
                    if (g.GameType == Hra.Betl)
                    {
                        gfDobraButton.Text = "Dobrý";
                        gfSpatnaButton.Text = "Špatný";
                    }
                    else
                    {
                        gfDobraButton.Text = "Dobrá";
                        gfSpatnaButton.Text = "Špatná";
                    }
                    foreach (var gfButton in gfButtons)
                    {
                        gfButton.Show();
                    }

                    if (!Game.Settings.HintEnabled || !_msgLabel.IsVisible) //abych neprepsal napovedu
                    {
                        ShowMsgLabel("Co řekneš?", false);
                    }
                });
            });
            WaitForUIThread();

            _state = GameState.NotPlaying;
			RunOnUiThread(() =>
            {
				_hintBtn.IsEnabled = false;
				this.ClearOperations();
				HideMsgLabel();
				foreach (var gfButton in gfButtons)
				{
					gfButton.Hide();
				}
			});

			return _gameFlavourChosen;
        }

        private void ChooseGameTypeInternal(Hra validGameTypes, bool showGiveUpButton)
        {
            g.ThrowIfCancellationRequested();
			_state = GameState.ChooseGameType;
			UpdateHand(cardToHide: _trumpCardChosen); //abych nevidel karty co jsem hodil do talonu
            this.Invoke(() =>
            {
                if (TrumpCardTakenBack)
                {
                    validGameTypes &= Hra.Betl | Hra.Durch;
                }
                foreach (var gtButton in gtButtons)
                {
                    if ((validGameTypes & (Hra)gtButton.Tag) == 0)
                    {
                        gtButton.BorderColor = Game.Settings.DefaultTextColor;
                    }
                    gtButton.IsEnabled = ((Hra)gtButton.Tag & validGameTypes) == (Hra)gtButton.Tag;
                    gtButton.Show();
                }
                if (showGiveUpButton)
                {
                    giveUpButton.Show();
                }
                if (!Game.Settings.HintEnabled || !_msgLabel.IsVisible) //abych neprepsal napovedu
                {
                    ShowMsgLabel("Co budeš hrát?", false);
                }
            });
        }

        public Hra ChooseGameType(Hra validGameTypes, bool showGiveUpButton = false)
        {
			EnsureBubblesHidden();
			g.ThrowIfCancellationRequested();
			//_hand.IsEnabled = false;
			_evt.Reset();
            _canSortTrump = true;
            RunOnUiThread(() =>
            {
                ChooseGameTypeInternal(validGameTypes, showGiveUpButton);
            });
            WaitForUIThread();

            _state = GameState.NotPlaying;
			RunOnUiThread(() =>
            {
                HideMsgLabel();
                this.ClearOperations();
                foreach (var btn in gtButtons)
                {
                    btn.Hide();
                }
                giveUpButton.Hide();
            });

            return _gameTypeChosen;
        }

        public Hra GetBidsAndDoubles(Bidding bidding)
        {
            //_evt.WaitOne();
			EnsureBubblesHidden();
			g.ThrowIfCancellationRequested();
			_evt.Reset();
			_state = GameState.Bid;
			//_hand.IsEnabled = false;
			_hand.AnimationEvent.Wait();
			_bid = 0;
            if (bidding.Bids != 0)
            {
                ShowBidButtons(bidding);
                _okBtn.IsEnabled = true;
                _okBtn.Show();
                //RunOnUiThread(() =>
                //{
                //    this.Invoke(() =>
                //    {
                //        ShowBidButtons(bidding);
                //        _okBtn.IsEnabled = true;
                //        _okBtn.Show();
                //    });
                //});
                WaitForUIThread();
                EnsureBubblesHidden();
                //RunOnUiThread(() =>
                //{
                //    this.ClearOperations();
                //    HideBidButtons();
                //});
                HideBidButtons();
            }
            _state = GameState.NotPlaying;
            g.ThrowIfCancellationRequested();

            return _bid;
        }

        public Card PlayCard(Renonc validationState)
        {
            EnsureBubblesHidden();
			g.ThrowIfCancellationRequested();
			_hand.IsEnabled = false;
			_cardClicked = null;
			_evt.Reset();
			RunOnUiThread(() =>
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
                        var validCards = g.CurrentRound != null
                                            ? g.CurrentRound.c2 != null
                                                ? g.players[0].Hand
                                                   .Where(i => g.players[0].IsCardValid(i, g.CurrentRound.c1, g.CurrentRound.c2) == Renonc.Ok)
                                                   .ToList()
                                                : g.CurrentRound.c1 != null
                                                    ? g.players[0].Hand
                                                       .Where(i => g.players[0].IsCardValid(i, g.CurrentRound.c1) == Renonc.Ok)
                                                       .ToList()
                                                    : g.players[0].Hand
                                                       .Where(i => g.players[0].IsCardValid(i) == Renonc.Ok)
                                                       .ToList()
                                            : new List<Card>(g.players[0].Hand);
                        if ((Game.Settings.AutoPlaySingletonCard &&
                             validCards.Count == 1) ||
                            (Game.Settings.AutoFinish &&
                             g.CurrentRound != null &&
                             g.CurrentRound.number == 10))
                        {
                            var button = _hand.CardButtonForCard(g.players[0].Hand.Count() == 1
                                                                 ? g.players[0].Hand.First()
                                                                 : g.players[0].Hand.First(i => i == validCards.First()));

                            _hand.AllowDragging();
                            button.Wait(200)
                                  .Invoke(() =>
                                  {
                                      button.TouchUp(new TouchLocation(-1, TouchLocationState.Released, button.Position - Vector2.UnitY * button.MinimalDragDistance));
                                  });
                        }
                        else
                        {
                            switch (validationState)
                            {
                                case Renonc.Ok:
                                    ShowMsgLabel("Hraj", false);
                                    //var n2 = (g.players[0] as HumanPlayer)._aiPlayer.Probabilities.PossibleCombinations(1, g.RoundNumber);
                                    //var n3 = (g.players[0] as HumanPlayer)._aiPlayer.Probabilities.PossibleCombinations(2, g.RoundNumber);
                                    //_msgLabelSmall.Text = $"\n\n{n2} {n3}";
                                    //_msgLabelSmall.Show();
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
                        }
                        _cardsPlayed[0].Hide();
                        if (validationState != Renonc.Ok)
                        {
                            _canSort = true;
                            _canSortTrump = g.trump != null;
                            SortHand();
                        }
                        if (Game.Settings.HintEnabled)
                        {
                            _hintBtn.IsEnabled = true;
                        }
                        _hand.IsEnabled = true;
                        _hand.AllowDragging();
                    });
            });
            WaitForUIThread();

            _state = GameState.NotPlaying;
			RunOnUiThread(() =>
            {
                this.ClearOperations();
                _hintBtn.IsEnabled = false;
                _hand.IsEnabled = false;
                _hand.ForbidDragging();
            });

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
                            var str = string.Format("Barva?{0}", _gameFlavourChosenEventArgs.AXTalon ? "\nOstrá v talonu" : string.Empty);

                            _bubbles[_gameFlavourChosenEventArgs.Player.PlayerIndex].Height = _gameFlavourChosenEventArgs.AXTalon ? 80 : 50;
                            ShowBubble(_gameFlavourChosenEventArgs.Player.PlayerIndex, str);
                            _trumpLabels[_gameFlavourChosenEventArgs.Player.PlayerIndex].Text = string.Format("{0}: Barva?{1}\n{2}",
                                                     g.players[_gameFlavourChosenEventArgs.Player.PlayerIndex].Name,
                                                     _gameFlavourChosenEventArgs.AXTalon ? " Ostrá v talonu" : string.Empty,
                                                     Game.Settings.ShowScoreDuringGame
                                                     ? (Game.Money.Sum(j => j.MoneyWon[_gameFlavourChosenEventArgs.Player.PlayerIndex]) * Game.Settings.BaseBet).ToString("C", Game.CurrencyFormat)
                                                     : string.Empty);
                        }
                        if (g.GameStartingPlayerIndex != 2)
                        {
                            ShowThinkingMessage((g.GameStartingPlayerIndex + 1) % Mariasek.Engine.Game.NumPlayers);
                        }
                    }
                }
                else
                {
                    if (_gameFlavourChosenEventArgs.Flavour == GameFlavour.Good)
                    {
                        if (g.GameType == 0)
                        {
                            var str = string.Format("Barva?{0}", _gameFlavourChosenEventArgs.AXTalon ? "\nOstrá v talonu" : string.Empty);

                            _bubbles[_gameFlavourChosenEventArgs.Player.PlayerIndex].Height = _gameFlavourChosenEventArgs.AXTalon ? 80 : 50;
                            ShowBubble(_gameFlavourChosenEventArgs.Player.PlayerIndex, str);
                            _trumpLabels[_gameFlavourChosenEventArgs.Player.PlayerIndex].Text = string.Format("{0}: Barva?{1}\n{2}",
                                                                                 g.players[_gameFlavourChosenEventArgs.Player.PlayerIndex].Name,
                                                                                 _gameFlavourChosenEventArgs.AXTalon ? " Ostrá v talonu" : string.Empty,
                                                                                 Game.Settings.ShowScoreDuringGame
                                                                                 ? (Game.Money.Sum(j => j.MoneyWon[_gameFlavourChosenEventArgs.Player.PlayerIndex]) * Game.Settings.BaseBet).ToString("C", Game.CurrencyFormat)
                                                                                 : string.Empty);
                        }
                        ShowThinkingMessage((_gameFlavourChosenEventArgs.Player.PlayerIndex + 1) % Mariasek.Engine.Game.NumPlayers);
                    }
                }
                //UpdateHand(cardToHide: _trumpCardChosen);
            }
            else if (_gameFlavourChosenEventArgs.Flavour == GameFlavour.Bad || g.GameType == 0)
            {
                var description = _gameFlavourChosenEventArgs.Flavour.Description();
                if (_gameFlavourChosenEventArgs.Flavour == GameFlavour.Bad && g.GameType != 0)
                {
                    //pokud odpovidame na betl, tak zmen koncovku
                    //dobra, spatna -> dobry, spatny
                    description = description.Replace("á", "ý");
                }
                ShowBubble(_gameFlavourChosenEventArgs.Player.PlayerIndex, description);
                if (e.Player.PlayerIndex != 2 && _gameFlavourChosenEventArgs.Flavour == GameFlavour.Good)
                {
                    ShowThinkingMessage((e.Player.PlayerIndex + 1) % Mariasek.Engine.Game.NumPlayers);
                }
                if (_gameFlavourChosenEventArgs.Flavour == GameFlavour.Bad && g.GameType == 0)
                {
                    _trumpCardChosen = null;
                    _canSort = Game.Settings.AutoSort;
                    _canSortTrump = false;
                    SortHand(); //preusporadame karty
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

        private string GetTrumpLabelForPlayer(int playerIndex)
        {
            var gameTypeString = AmendSuitNameIfNeeded(Game.MainScene.g.GameType.ToDescription(g.trump));
            var text = playerIndex == g.GameStartingPlayerIndex
                                   ? string.Format("{0}: {1}",
                                       Game.MainScene.g.players[playerIndex].Name,
                                           gameTypeString +
                                           (string.IsNullOrEmpty(Game.MainScene.g.players[playerIndex].BidMade)
                                            ? string.Empty
                                            : string.Format(" {0}", Game.MainScene.g.players[playerIndex].BidMade.TrimEnd())))
                                   : string.IsNullOrEmpty(Game.MainScene.g.players[playerIndex].BidMade)
                                       ? Game.MainScene.g.players[playerIndex].Name
                                       : string.Format("{0}: {1}", Game.MainScene.g.players[playerIndex].Name,
                                                                   Game.MainScene.g.players[playerIndex].BidMade.Trim());

            return text;
        }

        public string AmendSuitNameIfNeeded(string gameTypeString)
        {
            gameTypeString = gameTypeString.Trim();

            if (Game.Settings.CardDesign == CardFace.Pikety)
            {
                gameTypeString = gameTypeString.Replace("červený", "srdce")
                                               .Replace("zelený", "piky")
                                               //.Replace("kule", "káry")
                                               .Replace("žaludy", "kříže");
            }

            return gameTypeString;
        }

        public void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            //pokud se hrac vzdal, tak nic neukazujeme
            if (e.GameType == 0)
            {
                return;
            }
            g.ThrowIfCancellationRequested();
            _canSortTrump = e.TrumpCard != null;
            //trumfovou kartu otocime az zmizi vsechny bubliny
            RunOnUiThread(() =>
            {
                var imgs = new[]
                {
                    _hlasy[0][0], _hlasy[1][0], _hlasy[2][0]
                };
                this.Invoke(() =>
                {
                    for (var i = 0; i < _trumpLabels.Count(); i++)
                    {
                        _trumpLabels[i].Text = string.Format("{0}\n{1}", GetTrumpLabelForPlayer(g.players[i].PlayerIndex),
                                                                         Game.Settings.ShowScoreDuringGame
                                                                         ? (Game.Money.Sum(j => j.MoneyWon[i]) * Game.Settings.BaseBet).ToString("C", Game.CurrencyFormat)
                                                                         : string.Empty);
                    }
                });

                if (e.TrumpCard != null)
                {
                    ShowTalonIfNeeded();
                    this.Invoke(() =>
                    {
                        imgs[e.GameStartingPlayerIndex].Sprite.SpriteRectangle = e.TrumpCard.ToTextureRect();
                        imgs[e.GameStartingPlayerIndex].ShowBackSide();
                        imgs[e.GameStartingPlayerIndex].FlipToFront();
                        SortHand(null); //preusporadame karty
                        if (e.GameStartingPlayerIndex == 0)
                        {
                            UpdateHand(cardToHide: e.TrumpCard);
                        }
                        else
                        {
                            UpdateHand(); //trumfy dame doleva
                        }
                    })
                    .Wait(500)
                    .Invoke(() =>
                    {
                        _bubbleAutoHide[e.GameStartingPlayerIndex] = true;
                        _bubbles[e.GameStartingPlayerIndex].Text = AmendSuitNameIfNeeded(g.GameType.ToDescription(g.trump));
                        if (g.trump.HasValue && g.talon.Any(i => i.Value == Hodnota.Eso || i.Value == Hodnota.Desitka))
                        {
                            _bubbles[e.GameStartingPlayerIndex].Height = 80;
                            _bubbles[e.GameStartingPlayerIndex].Text += "\nostrá v talonu";
                        }
                        _bubbles[e.GameStartingPlayerIndex].Show();
                    })
                    //.Wait(1000)
                    //.Invoke(() =>
                    //{
                    //    _evt.Set(); //v GetBidsAndDoubles() je _evt.WaitOne()
                    //})
                    .Wait(1500)
                    .Invoke(() =>
                    {
                        _bubbles[e.GameStartingPlayerIndex].Hide();
                        _bubbles[e.GameStartingPlayerIndex].Height = 50;
                    })
                    .Wait(100)
                    .Invoke(() =>
                    {
                        imgs[e.GameStartingPlayerIndex].Hide();
                        UpdateHand();
                    });
                    _skipBidBubble = true;  //abychom nezobrazovali bublinu znovu v BidMade()
                }
                else 
                {
                    if (e.GameStartingPlayerIndex == 0)
                    {
                        this.Invoke(() =>
                        {
                            imgs[0].Hide();
                        });
                    }
                    //preusporadame karty
                    if ((e.GameType & (Hra.Betl | Hra.Durch)) != 0 && Game.Settings.AutoSort)
                    {
                        _canSort = true;
                        _canSortTrump = false;
                        this.Invoke(() =>
                        {
                            SortHand();
                        });
                    }
                }
                this.Invoke(() =>
                {
                    _progressBars[e.GameStartingPlayerIndex].Progress = _progressBars[e.GameStartingPlayerIndex].Max;
                });
            });
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
                ShowThinkingMessage((e.Player.PlayerIndex + 1) % Mariasek.Engine.Game.NumPlayers);
            }
            this.Invoke(() =>
            {
                for (var i = 0; i < _trumpLabels.Count(); i++)
                {
                    var sum = Game.Money.Sum(j => j.MoneyWon[i]) * Game.Settings.BaseBet;
                    _trumpLabels[i].Text = string.Format("{0}\n{1}",
                                             GetTrumpLabelForPlayer(g.players[i].PlayerIndex),
                                             Game.Settings.ShowScoreDuringGame
                                             ? sum.ToString("C", Game.CurrencyFormat)
                                             : string.Empty);
                    if (Game.Settings.WhiteScore)
                    {
                        _trumpLabels[i].HighlightColor = Game.Settings.DefaultTextColor;
                    }
                    else
                    {
                        _trumpLabels[i].HighlightColor = sum > 0
                                                           ? Game.Settings.PositiveScoreColor
                                                           : sum < 0
                                                               ? Game.Settings.NegativeScoreColor
                                                               : Game.Settings.DefaultTextColor;
                    }
                }
            });
        }

        public void CardPlayed(object sender, Round r)
        {
            if (r == null || g == null || r.c1 == null || !g.IsRunning) //pokud se vubec nehralo (lozena hra) nebo je lozeny zbytek hry
            {
                return;
            }
			EnsureBubblesHidden();
            g.ThrowIfCancellationRequested();
			RunOnUiThread(() =>
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
                    ShowThinkingMessage((lastPlayer.PlayerIndex + 1) % Mariasek.Engine.Game.NumPlayers);
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
            });
        }

        public void RoundStarted(object sender, Round r)
        {
            g.ThrowIfCancellationRequested();
            if (r.player1.PlayerIndex != 0)
            {
                ShowThinkingMessage(r.player1.PlayerIndex);
            }
            //pokud az do ted nebyly karty serazeny, tak je serad
            if (r.number == 1 &&
                !Game.Settings.AutoSort)
            {
                _canSort = true;
                SortHand();
            }
            // 3 * (r-1) + i
            _cardsPlayed[r.player1.PlayerIndex].ZIndex = (r.number - 1) * 3 + 1;
            _cardsPlayed[r.player2.PlayerIndex].ZIndex = (r.number - 1) * 3 + 2;
            _cardsPlayed[r.player3.PlayerIndex].ZIndex = (r.number - 1) * 3 + 3;
            // 3 * (r-1) + i
            if (r.player1.Hlasy >=0 && r.player1.Hlasy < 4)
            {
                _hlasy[r.player1.PlayerIndex][r.player1.Hlasy].ZIndex = (r.number - 1) * 3 + 1;
            }
            if (r.player2.Hlasy >= 0 && r.player2.Hlasy < 4)
            {
                _hlasy[r.player2.PlayerIndex][r.player2.Hlasy].ZIndex = (r.number - 1) * 3 + 2;
            }
            if (r.player3.Hlasy >= 0 && r.player3.Hlasy < 4)
            {
                _hlasy[r.player3.PlayerIndex][r.player3.Hlasy].ZIndex = (r.number - 1) * 3 + 3;
            }
            // 3 * (r-1) + i
            _stychy[r.player1.PlayerIndex].ZIndex = (r.number - 1) * 3 + 1;
            _stychy[r.player2.PlayerIndex].ZIndex = (r.number - 1) * 3 + 2;
            _stychy[r.player3.PlayerIndex].ZIndex = (r.number - 1) * 3 + 3;

            _reviewGameToggleBtn.IsEnabled = (Game.Settings.TestMode.HasValue && Game.Settings.TestMode.Value) ||
                                             r.number > 1;
        }

        public void RoundFinished(object sender, Round r)
        {
            if (r.number <= 10)
            {
                if (Game.Settings.AutoFinishRounds)
                {
                    g.ThrowIfCancellationRequested();
                    _evt.Reset();
                    RunOnUiThread(() =>
                    {
                        this.Wait(Game.Settings.RoundFinishedWaitTimeMs)
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
                                }
                                //pokud g.RoundNumber tak mezitim uzivatel zacal hrat novou hru
                                //a tohle je jeste event 
                                ClearTableAfterRoundFinished();
                            });
                    });
                    WaitForUIThread();
                }
                else
                {
                    ShowMsgLabel("\n\nKlikni pro pokračování ...", false);
                    //_msgLabelSmall.Text = "\n\nKlikni kamkoli ...";
                    //_msgLabelSmall.Show();
                    ShowInvisibleClickableOverlay();
                    WaitForUIThread();
                }
            }
        }

        private void EnsureBubblesHidden()
        {
            var evt = new AutoResetEvent(false);

            this.Invoke(() => evt.Set());
            //_evt bude nastaven pokud chci preusit hru (a zacit novou)
            WaitHandle.WaitAny(new[] { evt, _evt });
            //if (WaitHandle.WaitAny(new [] { evt, _evt }, Game.Settings.BubbleTimeMs * 2) == WaitHandle.WaitTimeout)
            //{
            //    HideThinkingMessage();
            //}
        }

        public void GameFinished(object sender, MoneyCalculatorBase results)
        {
            _state = GameState.GameFinished;

            //novou hru pujde spustit az pote, co se ulozi balicek
            //aby se nestalo, ze budu hrat novou hru s balickem z predchozi hry
            _newGameBtn.Enabled = false;
            EnsureBubblesHidden();
            g.ThrowIfCancellationRequested();

            results.SimulatedSuccessRate = SimulatedSuccessRate;
            if (!_testGame)
            {
                _deck = g.GetDeckFromLastGame();
                System.Diagnostics.Debug.WriteLine(_deck);
                var deck = _deck;

                Task.Run(() => SaveDeck(deck));

                if (Game.Settings.MaxHistoryLength > 0 &&
                    Game.Money.Count() >= Game.Settings.MaxHistoryLength)
                {
                    Game.Money.Clear();
                }
                Game.Money.Add(results);
                PopulateResults(results);
                Task.Run(async () =>
                {
                    try
                    {
                        if (Game.Settings.MaxHistoryLength > 0 &&
                            Game.Money.Count() >= Game.Settings.MaxHistoryLength)
                        {
                            DeleteArchiveFolder();
                        }
                        _newGameBtn.Enabled = true;
                        if (g.rounds[0] != null)
                        {
                            results.GameId = ArchiveGame();
                        }
                        try
                        {
                            Game.StorageAccessor.GetStorageAccess();
                            if (File.Exists(_savedGameFilePath))
                            {
                                File.Delete(_savedGameFilePath);
                            }
                        }
                        catch (Exception e)
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("Cannot delete old end of game file\n{0}", e.Message));
                        }
                        try
                        {
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
                        }
                        catch
                        {
                        }
                        try
                        {
                            SaveHistory();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("Cannot save history\n{0}", ex.Message));
                        }
                        if ((!Game.Settings.ShowRatingOffer.HasValue ||
                             Game.Settings.ShowRatingOffer.Value) &&
                            Game.Money.Count >= 500 &&
                            Game.Money.Count % 100 == 0)
                        {
#if __IOS__
                            var buttonIndex = await MessageBox.Show("Prosba autora:", "Rád bych Vás požádal o ohodnocení Mariášku v AppStore", new[] { "Ano", "Později", "Ne, děkuji" });
                            if (buttonIndex.HasValue)                                
                            {
                                switch(buttonIndex.Value)
                                {
                                    case 0:
                                        Game.Settings.ShowRatingOffer = false;
                                        Game.SaveGameSettings();
                                        this.Invoke(() =>
                                        {
                                            Game.MenuScene.RatingClicked(this);
                                        });
                                        break;
                                    case 1:
                                        Game.Settings.ShowRatingOffer = null;
                                        this.Invoke(() =>
                                        {
                                            Game.MenuScene.RatingClicked(this);
                                        });
                                        break;
                                    case 2:
                                        Game.Settings.ShowRatingOffer = false;
                                        this.Invoke(() =>
                                        {
                                            Game.MenuScene.RatingClicked(this);
                                        });
                                        break;
                                    default:
                                        break;
                                }
                            }
#else
                            var buttonIndex = await MessageBox.Show("Prosba autora:", "Rád bych Vás požádal o ohodnocení Mariášku\nna Google Play", new[] { "Ne, děkuji", "Ano", "Později" });
                            if (buttonIndex.HasValue)
                            {
                                switch (buttonIndex.Value)
                                {
                                    case 0:
                                        Game.Settings.ShowRatingOffer = false;
                                        Game.SaveGameSettings();
                                        break;
                                    case 1:
                                        Game.Settings.ShowRatingOffer = false;
                                        Game.SaveGameSettings();
                                        Game.MenuScene.RatingClicked(this);
                                        break;
                                    case 2:
                                        Game.Settings.ShowRatingOffer = null;
                                        Game.SaveGameSettings();
                                        break;
                                    default:
                                        break;
                                }
                            }
#endif

                        }
                    }
                    catch(Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("Unexpected error in GameFinished: {0}\n{1}", ex.Message, ex.StackTrace));
                    }
                });
            }
            else
            {
                PopulateResults(results);
            }
        }

#endregion

        private void PopulateResults(MoneyCalculatorBase results)
        {
            RunOnUiThread(() =>
            {
                ClearTable(true);
                _hand.UpdateHand(new Card[0]);
                _hintBtn.IsEnabled = false;
                //multi-line string needs to be split into two strings separated by a tab on each line
                var leftMessage = new StringBuilder();
                var rightMessage = new StringBuilder();
                var firstWord = string.Empty;
                var numLines = 0;
                var summary = g.Results.ToString();

                summary = summary.Replace("\nzabit", " zabit");
                foreach (var line in summary.Split('\n'))
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
                    else if (tokens.Length == 1 && 
                             !string.IsNullOrWhiteSpace(tokens[0]))
                    {
                        rightMessage.Append(" ");
                    }

                    leftMessage.Append("\n");
                    rightMessage.Append("\n");
                    numLines++;
                }
                if (numLines > 10)
                {
                    leftMessage = leftMessage.Replace("\n\n", "\n");
                    rightMessage = rightMessage.Replace("\n\n", "\n");
                }
                giveUpButton.Hide();
                _reviewGameToggleBtn.IsEnabled = false;
                HideInvisibleClickableOverlay();
                HideThinkingMessage();
                var totalWon = Game.Money.Sum(i => i.MoneyWon[0]) * Game.Settings.BaseBet;
                _totalBalance.Text = string.Format("Celkem jsem {0}: {1}", totalWon >= 0 ? "vyhrál" : "prohrál", totalWon.ToString("C", Game.CurrencyFormat));
                for (var i = 0; i < _trumpLabels.Count(); i++)
                {
                    var sum = Game.Money.Sum(j => j.MoneyWon[i]) * Game.Settings.BaseBet;

                    _trumpLabels[i].Text = string.Format("{0}\n{1}",
                                             GetTrumpLabelForPlayer(g.players[i].PlayerIndex),
                                             sum.ToString("C", Game.CurrencyFormat));
                    if (Game.Settings.WhiteScore)
                    {
                        _trumpLabels[i].HighlightColor = Game.Settings.DefaultTextColor;
                    }
                    else
                    {
                        _trumpLabels[i].HighlightColor = sum > 0
                                                           ? Game.Settings.PositiveScoreColor
                                                           : sum < 0
                                                               ? Game.Settings.NegativeScoreColor
                                                               : Game.Settings.DefaultTextColor;
                    }
                }
                if (!results.GamePlayed || results.MoneyWon[0] == 0)
                {
                    _gameResult.BorderColor = Game.Settings.TieColor;
                }
                else if (results.MoneyWon[0] > 0)
                {
                    _gameResult.BorderColor = Game.Settings.WinColor;
                }
                else //(results.MoneyWon[0] < 0)
                {
                    _gameResult.BorderColor = Game.Settings.LossColor;
                }
                _gameResult.Text = firstWord;
                _msgLabelLeft.Text = leftMessage.ToString();
                _msgLabelRight.Text = rightMessage.ToString();
                ShowGameScore();

                _shouldShuffle = (!_testGame &&
                                  Game.Settings.WhenToShuffle == ShuffleTrigger.AfterAutomaticVictory && 
                                  results.MoneyWon[g.GameStartingPlayerIndex] > 0 && 
                                  results.GamePlayed && 
                                  (g.RoundNumber == 1 ||
                                   (g.Results.BetlWon &&
                                    g.RoundNumber == 2))) ||
                                 Game.Settings.WhenToShuffle == ShuffleTrigger.Always;
                if (!_lastGameWasLoaded)
                {
                    Game.Settings.CurrentStartingPlayerIndex = CurrentStartingPlayerIndex;
                }
                //tohle zpusobi prekresleni nekterych ui prvku, je treba volat z UI threadu
                Game.UpdateSettings();

                if (results.GamePlayed && results.MoneyWon[0] > 0)
                {
                    Game.ClapSound?.PlaySafely();
                }
                else if (results.GamePlayed && results.MoneyWon[0] < 0)
                {
                    Game.BooSound?.PlaySafely();
                }
                else
                {
                    Game.CoughSound?.PlaySafely();
                }
                _newGameBtn.Show();
                _repeatGameBtn.IsEnabled = Game.StorageAccessor.CheckStorageAccess();
                _repeatGameOptionBtn.IsEnabled = _repeatGameBtn.IsEnabled;
                _repeatGameBtn.Show();
                _repeatGameOptionBtn.Show();
                //_repeatGameAsPlayer2Btn.Show();
                //_repeatGameAsPlayer3Btn.Show();
                _reviewGameBtn.Text = "Průběh hry";
                _reviewGameBtn.Show();
                _editGameBtn.Show();
            });
        }

        void ShowShuffleAnimation()
        {
            var position1 = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight / 2f);
            var position2 = new Vector2(Game.VirtualScreenWidth / 2f + 150, Game.VirtualScreenHeight / 2f);

            _shuffleAnimationRunning = true;
            _shuffledCards[0].Scale = Game.CardScaleFactor;
            _shuffledCards[1].Scale = Game.CardScaleFactor;
            _shuffledCards[2].Scale = Game.CardScaleFactor;
            _shuffledCards[0].ZIndex = 50;
            _shuffledCards[1].ZIndex = 51;
            _shuffledCards[2].ZIndex = 52;
            _shuffledCards[0].Position = position1;
            _shuffledCards[1].Position = position1;
            _shuffledCards[2].Position = position2;
            _shuffledCards[0].Texture = Game.ReverseTexture;
            _shuffledCards[1].Texture = Game.ReverseTexture;
            _shuffledCards[2].Texture = Game.ReverseTexture;
            _shuffledCards[0].SpriteRectangle = Game.BackSideRect;
            _shuffledCards[1].SpriteRectangle = Game.BackSideRect;
            _shuffledCards[2].SpriteRectangle = Game.BackSideRect;
            _shuffledCards[0].Show();
            _shuffledCards[1].Hide();
            _shuffledCards[2].Hide();
            _shuffledCards[0].FadeIn(2);
            this.WaitUntil(() => _shuffledCards[0].Opacity == 1)
                .Invoke(() => _shuffledCards[1].Show());
            for (var i = 0; i < 5; i++)
            {
                this.Invoke(() => _shuffledCards[0].MoveTo(position2, 800))
                    .WaitUntil(() => _shuffledCards[0].Position == position2)
                    .Invoke(() =>
                    {
                        _shuffledCards[0].Hide();
                        _shuffledCards[2].Position = position2;
                        _shuffledCards[2].Show();
                    })
                    .Invoke(() => _shuffledCards[2].MoveTo(position1, 800))
                    .WaitUntil(() => _shuffledCards[2].Position == position1)
                    .Invoke(() =>
                    {
                        _shuffledCards[0].Position = position1;
                        _shuffledCards[0].Show();
                        _shuffledCards[2].Hide();
                        _shuffledCards[2].Position = position2;
                    });
            }
            this.Invoke(() =>
            {
                _shuffledCards[0].FadeOut(2);
                _shuffledCards[1].Hide();
            })
                .WaitUntil(() => _shuffledCards[0].Opacity == 0)
                .Invoke(() =>
                {
                    _shuffledCards[0].Hide();
                    _shuffleAnimationRunning = false;
                });
        }

        public bool CanLoadGame()
        {
            try
            {
                Game.StorageAccessor.GetStorageAccess();
                if (File.Exists(_savedGameFilePath))
                {
                    var fi = new FileInfo(_savedGameFilePath);

                    return fi.Length > 0;
                }                    
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            return false;
        }

        public void LoadGame(string path, bool testGame, int impersonationPlayerIndex = 0)
        {
            _testGame = testGame;
            //if (g == null)
            {
                if (!_gameSemaphore.Wait(0))
                {
                    return;
                }
                var gameTask = _gameTask;

                _trumpLabel1.Hide();
                _trumpLabel2.Hide();
                _trumpLabel3.Hide();
                _newGameBtn.Hide();
                _repeatGameBtn.Hide();
                _repeatGameOptionBtn.Hide();
                _repeatGameAsPlayer2Btn.Hide();
                _repeatGameAsPlayer3Btn.Hide();
                SetActive();
                var cancellationTokenSource = new CancellationTokenSource();
                _gameTask = Task.Run(() => CancelRunningTask(gameTask))
                                .ContinueWith(cancellationTask =>
                {
                    _preGameEvent.Reset();
                    try
                    {
                        _cancellationTokenSource = cancellationTokenSource;
                        g = new Mariasek.Engine.Game()
                        {
                            BaseBet = Game.Settings.BaseBet,
                            Locale = Game.Settings.Locale,
                            MaxWin = Game.Settings.MaxWin,
                            SkipBidding = false,
                            MinimalBidsForGame = Game.Settings.MinimalBidsForGame,
                            MinimalBidsForSeven = Game.Settings.MinimalBidsForSeven,
                            CalculationStyle = Game.Settings.CalculationStyle,
                            PlayZeroSumGames = Game.Settings.PlayZeroSumGames,
                            Top107 = Game.Settings.Top107,
                            Calculate107Separately = Game.Settings.Calculate107Separately,
                            HlasConsidered = Game.Settings.HlasConsidered,
                            AutoDisable100Against = Game.Settings.AutoDisable100Against,
                            GetFileStream = GetFileStream,
                            GetVersion = () => MariasekMonoGame.Version,
                            GameValue = Game.Settings.GameValue,
                            QuietSevenValue = Game.Settings.QuietSevenValue,
                            SevenValue = Game.Settings.SevenValue,
                            QuietHundredValue = Game.Settings.QuietHundredValue,
                            HundredValue = Game.Settings.HundredValue,
                            BetlValue = Game.Settings.BetlValue,
                            DurchValue = Game.Settings.DurchValue,
                            FirstMinMaxRound = Game.Settings.FirstMinMaxRound,
                            AllowFakeSeven = Game.Settings.AllowFakeSeven,
                            AllowFake107 = Game.Settings.AllowFake107,
                            AllowAXTalon = Game.Settings.AllowAXTalon,
                            AllowTrumpTalon = Game.Settings.AllowTrumpTalon,
                            AllowAIAutoFinish = Game.Settings.AllowAIAutoFinish,
                            AllowPlayerAutoFinish = Game.Settings.AllowPlayerAutoFinish,
                            CurrencyFormat = Game.CurrencyFormat
                        };
                        g.RegisterPlayers(
                            new HumanPlayer(g, _aiSettings, this, Game.Settings.HintEnabled) { Name = Game.Settings.PlayerNames[0] },
                            new AiPlayer(g, _aiSettings) { Name = Game.Settings.PlayerNames[1] },
                            new AiPlayer(g, _aiSettings) { Name = Game.Settings.PlayerNames[2] }
                        );

                        try
                        {
                            //g.DoSort = Game.Settings.SortMode != SortMode.None;
                            Game.StorageAccessor.GetStorageAccess();
                            using (var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                g.LoadGame(fs, impersonationPlayerIndex: impersonationPlayerIndex);
                            }
                        }
                        catch (Exception ex)
                        {
                            ShowMsgLabel(string.Format("Error loading game:\n{0}", ex.Message), false);
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

                        _canSort = Game.Settings.AutoSort || g.RoundNumber > 0;
                        _canSortTrump = g.RoundNumber > 0;
                        _firstTimeTalonCardClick = true;
                        if (g.GameStartingPlayerIndex == 0 && g.RoundNumber == 0)
                        {
                            SortHand(null, 7);
                        }
                        else
                        {
                            SortHand();
                        }

                        _firstTimeGameFlavourChosen = true;
                        _trumpCardChosen = null;
                        _cardClicked = null;
                        _bid = 0;
                        _gameTypeChosen = 0;
                        _gameFlavourChosen = 0;
                        TrumpCardTakenBack = false;
                        _state = GameState.NotPlaying;
                        _talon = g.GameStartingPlayerIndex == 0 && g.talon != null && g.talon.Any() ? new List<Card>(g.talon) : new List<Card>();

                        if (g.RoundNumber >= 1 && g.RoundNumber <= Mariasek.Engine.Game.NumRounds)
                        {
                            CurrentStartingPlayerIndex = g.GameStartingPlayerIndex;
                            Game.Settings.CurrentStartingPlayerIndex = CurrentStartingPlayerIndex;
                            Game.SaveGameSettings();
                        }
                        ClearTable(true);
                        HideMsgLabel();
                        _reviewGameBtn.Hide();
                        _editGameBtn.Hide();
                        _reviewGameToggleBtn.Show();
                        _reviewGameToggleBtn.IsSelected = false;
                        _reviewGameToggleBtn.IsEnabled = (Game.Settings.TestMode.HasValue && 
                                                          Game.Settings.TestMode.Value) || 
                                                         (g.CurrentRound != null && g.RoundNumber > 1);

                        if (_review != null)
                        {
                            _review.Hide();
                        }
                        foreach (var btn in gtButtons)
                        {
                            btn.BorderColor = Game.Settings.DefaultTextColor;
                            btn.Hide();
                        }
                        giveUpButton.BorderColor = Game.Settings.DefaultTextColor;
                        giveUpButton.Hide();
                        foreach (var btn in gfButtons)
                        {
                            btn.BorderColor = Game.Settings.DefaultTextColor;
                            btn.Hide();
                        }
                        foreach (var btn in bidButtons)
                        {
                            btn.BorderColor = Game.Settings.DefaultTextColor;
                            btn.Hide();
                        }
                        foreach (var bubble in _bubbles)
                        {
                            bubble.Hide();
                        }
                        AmendCardScaleFactor();
                        RunOnUiThread(() =>
                        {
                            _hand.ClearOperations();
                            this.ClearOperations();
                        });
                        _hintBtn.IsEnabled = false;
                        _hintBtn.Show();
                        if (g.GameStartingPlayerIndex != 0 || g.GameType != 0)
                        {
                            if (g.GameType == 0)
                            {
                                ShowThinkingMessage(g.GameStartingPlayerIndex);
                            }
                            if (g.GameStartingPlayerIndex == 0 && g.RoundNumber == 0)
                            {
                                UpdateHand(true, 7);
                            }
                            else
                            {
                                UpdateHand(true);
                            }
                        }
                        else
                        {
                            _hand.Hide();
                        }
                        var hlasy1 = 0;
                        var hlasy2 = 0;
                        var hlasy3 = 0;
                        foreach (var r in g.rounds.Where(i => i != null))
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
                            var sum = new List<MoneyCalculatorBase>(Game.Money).Sum(j => j.MoneyWon[i]) * Game.Settings.BaseBet;
                            //_trumpLabels[i].Text = g.players[i].Name;
                            _trumpLabels[i].Text = string.Format("{0}\n{1}",
                                                     GetTrumpLabelForPlayer(g.players[i].PlayerIndex),
                                                     Game.Settings.ShowScoreDuringGame
                                                     ? sum.ToString("C", Game.CurrencyFormat)
                                                     : string.Empty);
                            _trumpLabels[i].Height = 60;
                            if (Game.Settings.WhiteScore)
                            {
                                _trumpLabels[i].HighlightColor = Game.Settings.DefaultTextColor;
                            }
                            else
                            {
                                _trumpLabels[i].HighlightColor = sum > 0
                                                                   ? Game.Settings.PositiveScoreColor
                                                                   : sum < 0
                                                                       ? Game.Settings.NegativeScoreColor
                                                                       : Game.Settings.DefaultTextColor;
                            }
                            _trumpLabels[i].Show();
                        }
                        try
                        {
                            Game.StorageAccessor.GetStorageAccess();
                            if (File.Exists(_savedGameFilePath))
                            {
                                File.Delete(_savedGameFilePath);
                            }
                        }
                        catch (Exception e)
                        { }
                    }
                    finally
                    {
                        if (_gameSemaphore.CurrentCount == 0)
                        {
                            _gameSemaphore.Release();
                        }
                    }
                    _lastGameWasLoaded = true;
                    g.PlayGame(_cancellationTokenSource.Token);
                }, cancellationTokenSource.Token);
            }
        }

       public void SaveGame()
       {
            if (g != null && g.IsRunning)
            {
                if (g != null)
                {
                    g.DebugString.AppendLine("SaveGame");
                }
                try
                {
                    Game.StorageAccessor.GetStorageAccess();
                    if (_testGame)
                    {
                        if (File.Exists(_savedGameFilePath))
                        {
                            File.Delete(_savedGameFilePath);
                        }
                        return;
                    }
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
                        FileCopy(_newGameFilePath, _savedGameFilePath, true);
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(string.Format("Cannot save game\n{0}", e.Message));
                }
            }
        }

        public async void ResumeGame()
        {
            if (g != null)
            {
                g.DebugString.AppendLine("ResumeGame");
            }
            if (CanLoadGame() && !MessageBox.IsVisible)
            {
                var buttonIndex = await MessageBox.Show("", $"Přejete si pokračovat v rozehrané hře?", new string[] { "Ne", "Ano" });

                if (g != null)
                {
                    g.DebugString.AppendLine("ResumeGameCallback");
                }
                try
                {
                    if (buttonIndex.HasValue && buttonIndex.Value == 1)
                    {
                        LoadGame(_savedGameFilePath, false);
                    }
                    else
                    {
                        File.Delete(_savedGameFilePath);
                    }
                }
                catch (Exception ex)
                {
                    GameException(this, new GameExceptionEventArgs() { e = ex });
                }
            }
        }

        public void ReplayGame(string gamePath, int impersonationPlayerIndex = 0)
        {
            CancelRunningTask(_gameTask);
            CleanUpOldGame();
            _testGame = true;
            Task.Run(() =>
            {
                try
                {
                    LoadGame(gamePath, true, impersonationPlayerIndex: impersonationPlayerIndex);
                }
                catch (Exception ex)
                {
                    ShowMsgLabel(ex.Message, false);
                }
            });
        }

        public void UpdateHand(bool flipCardsUp = false, int cardsNotRevealed = 0, Card cardToHide = null)
        {
            if (_hand.IsVisible &&
                _hand.CardsVisible == g.players[0].Hand.Count &&
                cardsNotRevealed == _hand.CardsNotRevealed &&
                cardToHide == _hand.CardToHide)
            {
                return;
            }
            _hand.AnimationEvent.Reset();
            RunOnUiThread(() =>
            {
                if (flipCardsUp)
                {
                    if (_state == GameState.ChooseTalon)
                    {
                        _hand.UpdateHand(g.players[0].Hand.ToArray(), 5, cardToHide);
                    }
                    else
                    {
                        _hand.UpdateHand(g.players[0].Hand.ToArray(), g.players[0].Hand.Count, cardToHide);
                    }
                }
                else
                {
                    _hand.UpdateHand(g.players[0].Hand.ToArray(), 0, cardToHide);
                }
                //nyni by mely vsechny karty byt videt (predek nebo zadek)
                //nasledujici radek by mel karty posunout na sva mista
                _hand.ShowStraight((int)Game.VirtualScreenWidth - 20);
                if (flipCardsUp)
                {
                    _hand.WaitUntil(() =>
                         { 
                             if (!_hand.SpritesBusy && !_hand.IsMoving)
                             {

                             }
                             return !_hand.SpritesBusy && !_hand.IsMoving; 
                         })
                         .Invoke(() =>
                         {
                             _hand.UpdateHand(g.players[0].Hand.ToArray(), cardsNotRevealed, cardToHide);
                         });
                }
            });
		    if (_state == GameState.ChooseTalon && Game.Settings.AutoSort)
            {
                RunOnUiThread(() =>
                {
                    _hand.WaitUntil(() => _hand.AllCardsFaceUp)
                         .Invoke(() =>
                         {
                             _canSort = true;
                             _canSortTrump = true;
                             SortHand(cardToHide, flipCardsIfNeeded: false);
                         });
                });
            }
            RunOnUiThread(() =>
            {
                _hand.WaitUntil(() =>
                {
                    if (!_hand.SpritesBusy && !_hand.IsMoving)
                    {

                    }
                    return !_hand.SpritesBusy && !_hand.IsMoving;
                }).Invoke(() =>
                {
                    _preGameEvent.Set();
                });
            });
        }

        public void UpdateCardTextures(GameComponent parent, Texture2D oldTexture, Texture2D newTexture)
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

        private void UpdateBackground()
        {
            switch (Game.Settings.BackgroundImage)
            {
                case BackgroundImage.Canvas:
                    Background = Game.CanvasBackground;
                    break;
                case BackgroundImage.Dark:
                    Background = Game.DarkBackground;
                    break;
                case BackgroundImage.Default:
                default:
                    Background = Game.DefaultBackground;
                    break;
            }
        }

        public void SettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            PopulateAiConfig();
            Game.CardScaleFactor = new Vector2(Game.Settings.CardScaleFactor, Game.Settings.CardScaleFactor);
            if (g != null)
            {
                g.GameValue = Game.Settings.GameValue;
                g.QuietSevenValue = Game.Settings.QuietSevenValue;
                g.SevenValue = Game.Settings.SevenValue;
                g.QuietHundredValue = Game.Settings.QuietHundredValue;
                g.HundredValue = Game.Settings.HundredValue;
                g.BetlValue = Game.Settings.BetlValue;
                g.DurchValue = Game.Settings.DurchValue;
                g.Locale = Game.Settings.Locale;
                for (var i = 0; i < _trumpLabels.Count(); i++)
                {
                    var sum = Game.Money.Sum(j => j.MoneyWon[i]) * Game.Settings.BaseBet;

                    _trumpLabels[i].Text = string.Format("{0}\n{1}",
                                             GetTrumpLabelForPlayer(g.players[i].PlayerIndex),
                                             Game.Settings.ShowScoreDuringGame
                                             ? sum.ToString("C", Game.CurrencyFormat)
                                             : string.Empty);
                    if (Game.Settings.WhiteScore)
                    {
                        _trumpLabels[i].HighlightColor = Game.Settings.DefaultTextColor;
                    }
                    else
                    {
                        _trumpLabels[i].HighlightColor = sum > 0
                                                           ? Game.Settings.PositiveScoreColor
                                                           : sum < 0
                                                               ? Game.Settings.NegativeScoreColor
                                                               : Game.Settings.DefaultTextColor;
                    }
                }
            }
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
            RunOnUiThread(() =>
            {
                //Buggy on Android when called from outside of the game constructor
                //if (Game.Graphics.IsFullScreen != !Game.Settings.ShowStatusBar)
                //{
                //    Game.Graphics.ToggleFullScreen();
                //    Game.Graphics.ApplyChanges();
                //}
                Game.ScreenManager.SetKeepScreenOnFlag(Game.Settings.KeepScreenOn);
            });
            UpdateBackground();
            var newBackSideRect = Game.Settings.CardDesign == CardFace.Pikety
                                    ? CardBackSide.Pikety.ToTextureRect()
                                    : Game.Settings.CardBackSide.ToTextureRect();

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
                if (_shuffledCards != null)
                {
                    foreach (var sprite in _shuffledCards)
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
            else if (_state != GameState.GameFinished && g != null)
            {
                SortHand(null);
            }

            var oldTextures = Game.CardTextures;
            var newTextures = Game.Settings.CardDesign == CardFace.Single
                                ? Game.CardTextures1
                                : Game.Settings.CardDesign == CardFace.Double
                                    ? Game.CardTextures2
                                    : Game.CardTextures3;

            if (oldTextures != newTextures)
            {
                UpdateCardTextures(this, oldTextures, newTextures);
                Game.CardTextures = newTextures;
            }

            UpdateControlsPositions();
        }

        public void UpdateControlsPositions()
        {
            if (_trumpLabel2 == null)
            {
                //controls not initialized yet
                return;
            }
            if (Game.Settings.DirectionOfPlay == DirectionOfPlay.Clockwise)
            {
                _trumpLabel2.Position = new Vector2(10, 5);
                _trumpLabel2.HorizontalAlign = HorizontalAlignment.Left;
                _trumpLabel3.Position = new Vector2(Game.VirtualScreenWidth - 410, 5);
                _trumpLabel3.HorizontalAlign = HorizontalAlignment.Right;
                _progress2.Position = new Vector2(5, 0);
                _progress3.Position = new Vector2(Game.VirtualScreenWidth - 155, 0);
                _bubble2.Position = new Vector2(10, 80);
                _bubble3.Position = new Vector2(Game.VirtualScreenWidth - 260, 80);
                _cardsPlayed[1].Position = new Vector2(Game.VirtualScreenWidth / 2f - 50, Game.VirtualScreenHeight / 2f - 95);
                _cardsPlayed[2].Position = new Vector2(Game.VirtualScreenWidth / 2f + 50, Game.VirtualScreenHeight / 2f - 105);
                _hlasy[1][0].Position = new Vector2(100, 130);
                _hlasy[1][1].Position = new Vector2(150, 130);
                _hlasy[1][2].Position = new Vector2(200, 130);
                _hlasy[1][3].Position = new Vector2(250, 130);
                _hlasy[2][0].Position = new Vector2(Game.VirtualScreenWidth - 100, 130);
                _hlasy[2][1].Position = new Vector2(Game.VirtualScreenWidth - 150, 130);
                _hlasy[2][2].Position = new Vector2(Game.VirtualScreenWidth - 200, 130);
                _hlasy[2][3].Position = new Vector2(Game.VirtualScreenWidth - 250, 130);
                _stychy[1].Position = new Vector2(60, 90);
                _stychy[2].Position = new Vector2(Game.VirtualScreenWidth - 60, 90);
                _stareStychy[1].Position = new Vector2(60, 90);
                _stareStychy[2].Position = new Vector2(Game.VirtualScreenWidth - 60, 90);
                _poslStych[1][0].Position = new Vector2(200, 90);
                _poslStych[1][1].Position = new Vector2(250, 90);
                _poslStych[1][2].Position = new Vector2(300, 90);
                _poslStych[2][0].Position = new Vector2(Game.VirtualScreenWidth - 200, 90);
                _poslStych[2][1].Position = new Vector2(Game.VirtualScreenWidth - 250, 90);
                _poslStych[2][2].Position = new Vector2(Game.VirtualScreenWidth - 300, 90);
            }
            else
            {
                _trumpLabel3.Position = new Vector2(10, 5);
                _trumpLabel3.HorizontalAlign = HorizontalAlignment.Left;
                _trumpLabel2.Position = new Vector2(Game.VirtualScreenWidth - 410, 5);
                _trumpLabel2.HorizontalAlign = HorizontalAlignment.Right;
                _progress3.Position = new Vector2(5, 0);
                _progress2.Position = new Vector2(Game.VirtualScreenWidth - 155, 0);
                _bubble3.Position = new Vector2(10, 80);
                _bubble2.Position = new Vector2(Game.VirtualScreenWidth - 260, 80);
                _cardsPlayed[2].Position = new Vector2(Game.VirtualScreenWidth / 2f - 50, Game.VirtualScreenHeight / 2f - 95);
                _cardsPlayed[1].Position = new Vector2(Game.VirtualScreenWidth / 2f + 50, Game.VirtualScreenHeight / 2f - 105);
                _hlasy[2][0].Position = new Vector2(100, 130);
                _hlasy[2][1].Position = new Vector2(150, 130);
                _hlasy[2][2].Position = new Vector2(200, 130);
                _hlasy[2][3].Position = new Vector2(250, 130);
                _hlasy[1][0].Position = new Vector2(Game.VirtualScreenWidth - 100, 130);
                _hlasy[1][1].Position = new Vector2(Game.VirtualScreenWidth - 150, 130);
                _hlasy[1][2].Position = new Vector2(Game.VirtualScreenWidth - 200, 130);
                _hlasy[1][3].Position = new Vector2(Game.VirtualScreenWidth - 250, 130);
                _stychy[2].Position = new Vector2(60, 90);
                _stychy[1].Position = new Vector2(Game.VirtualScreenWidth - 60, 90);
                _stareStychy[2].Position = new Vector2(60, 90);
                _stareStychy[1].Position = new Vector2(Game.VirtualScreenWidth - 60, 90);
                _poslStych[2][0].Position = new Vector2(200, 90);
                _poslStych[2][1].Position = new Vector2(250, 90);
                _poslStych[2][2].Position = new Vector2(300, 90);
                _poslStych[1][0].Position = new Vector2(Game.VirtualScreenWidth - 200, 90);
                _poslStych[1][1].Position = new Vector2(Game.VirtualScreenWidth - 250, 90);
                _poslStych[1][2].Position = new Vector2(Game.VirtualScreenWidth - 300, 90);
            }
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
                HintBtnFunc = () => ShowMsgLabel(string.Format("Nápověda: {0}", flavour), false);
            }
        }

        public void SuggestGameType(string gameType, string allChoices, int? t = null)
        {
            _progress1.Progress = _progress1.Max;
            if (Game.Settings.HintEnabled)
            {
                _hintBtn.IsEnabled = true;
                HintBtnFunc = () =>
                {
                    ShowMsgLabel(string.Format("Nápověda: {0}", AmendSuitNameIfNeeded(gameType)), false);
                    _msgLabelSmall.Text = string.Format("\n\n\n\n{0}", allChoices.Trim());
                    _msgLabelSmall.Show();
                };
            }
        }

        public void SuggestGameTypeNew(Hra gameType)
        {            
            foreach (var gtButton in gtButtons)
            {
                if (gameType == (Hra)gtButton.Tag)
                {
                    gtButton.BorderColor = Game.Settings.HintColor;
                }
                else
                {
                    gtButton.BorderColor = Game.Settings.DefaultTextColor;
                }
            }
            giveUpButton.BorderColor = gameType == 0 ? Game.Settings.HintColor : Game.Settings.DefaultTextColor;
        }

        public void SuggestGameFlavourNew(Hra gameType)
        {
            var flavour = (gameType & (Hra.Betl | Hra.Durch)) == 0 ? GameFlavour.Good : GameFlavour.Bad;

            SuggestGameFlavourNew(flavour);
        }

        public void SuggestGameFlavourNew(GameFlavour flavour)
        {
            gfDobraButton.BorderColor = flavour == GameFlavour.Good ? Game.Settings.HintColor : Game.Settings.DefaultTextColor;
            gfSpatnaButton.BorderColor = flavour == GameFlavour.Bad ? Game.Settings.HintColor : Game.Settings.DefaultTextColor;
        }

        public void SuggestBidsAndDoubles(string bid, int? t = null)
        {
            _progress1.Progress = _progress1.Max;
            if (Game.Settings.HintEnabled)
            {
                _hintBtn.IsEnabled = true;
                HintBtnFunc = () => ShowMsgLabel(string.Format("Nápověda: {0}", bid), false);
            }
        }

		public void SuggestBidsAndDoublesNew(Hra bid)
		{
			foreach (var btn in bidButtons)
			{
				if ((bid & (Hra)btn.Tag) != 0)
				{
					btn.BorderColor = Game.Settings.HintColor;
				}
				else
				{
					btn.BorderColor = Game.Settings.DefaultTextColor;
				}
			}
		}

		public void SuggestCardToPlay(Card cardToPlay, string hint, string rule, int? t = null)
        {
            _progress1.Progress = _progress1.Max;
            if (Game.Settings.HintEnabled)
            {
                _hintBtn.IsEnabled = true;
                HintBtnFunc = () =>
                {
                    ShowMsgLabel(hint, false);
                    _msgLabelSmall.Text = $"\n\n{rule}";
                    _msgLabelSmall.Show();
                    if (!_hand.HighlightCard(cardToPlay))
                    {
                        var msg = string.Format("Chyba simulace: hráč nemá {0}", cardToPlay);
                        ShowMsgLabel(msg, false);
                    }
                };
            }
        }

        //Pozor: tato funkce nove karty zobrazi, pokud zobrazene zatim nebyly
        public void SortHand(Card cardToHide = null, int numberOfCardsToSort = 12, bool flipCardsIfNeeded = true)
        {
            if (_canSort)
            {
                var unsorted = new List<Card>(g.players[0].Hand);

                if (Game.Settings.SortMode != SortMode.None)
                {
                    var badGameSorting = (g.GameType == 0 && _gameFlavourChosen == GameFlavour.Bad) || ((g.GameType & (Hra.Betl | Hra.Durch)) != 0);

                    if (numberOfCardsToSort == 12)
                    {
                        g.players[0].Hand.Sort(Game.Settings.SortMode, badGameSorting, _canSortTrump ? g.trump : null);
                    }
                    else
                    {
                        var sortedList = g.players[0].Hand.Take(numberOfCardsToSort).ToList();

                        sortedList.Sort(Game.Settings.SortMode, badGameSorting, g.trump);
                        g.players[0].Hand = sortedList.Concat(g.players[0].Hand.Skip(numberOfCardsToSort).Take(12)).ToList();
                    }
                }
                //nyni mame setridene karty ve hre, aktualizujeme karty na displeji
                _hand.UpdateHand(g.players[0].Hand.ToArray(), 12 - numberOfCardsToSort, cardToHide, flipCardsIfNeeded);
                //a setridime je na spravne misto
                _hand.SortHand(unsorted);
                //_hand.SortHand(g.players[0].Hand);
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

        private void ShowLastTrick()
        {
            var prevRound = g.rounds.LastOrDefault(i => i != null && i.c3 != null);

            if (prevRound != null)
            {
                ShowTalonOrLastTrick(prevRound.roundWinner.PlayerIndex, new List<Card>() { prevRound.c1, prevRound.c2, prevRound.c3 });
                _poslStych[prevRound.roundWinner.PlayerIndex][0].Invoke(() =>
                {
                    _reviewGameToggleBtn.IsSelected = false;
                });
            }
            else
            {
                _reviewGameToggleBtn.IsSelected = false;
            }
        }

        private void ShowTalonIfNeeded()
        {
            var axTalon = g.talon.Where(i => i.Value == Hodnota.Eso || i.Value == Hodnota.Desitka).ToList();

            if (g.trump.HasValue && axTalon.Any())
            {
                _poslStych[g.GameStartingPlayerIndex][0].Wait(2000)
                                                        .Invoke(() => ShowTalonOrLastTrick(g.GameStartingPlayerIndex, axTalon));
            }
        }

        private void ShowTalonOrLastTrick(int playerIndex, List<Card> cards)
        {
            var initialPosition = _poslStych[playerIndex][0].Position;

            for (var i = 0; i < _poslStych.Count() && i < cards.Count(); i++)
            {
                var card = _poslStych[playerIndex][i];
                var origPosition = card.Position;

                card.Position = initialPosition;
                card.SpriteRectangle = cards[i].ToTextureRect();
                card.Show();
                card.FadeIn(4f)
                    .MoveTo(origPosition, 500)
                    .Wait(2000)
                    .MoveTo(initialPosition, 500)
                    .FadeOut(4f)
                    .Invoke(() => 
                {
                    card.Hide();
                    card.Position = origPosition;
                });
            }
        }

        private void ClearTable(bool hlasy = false)
        {
            RunOnUiThread(() =>
            {
                _cardsPlayed[0].Hide();
                _cardsPlayed[1].Hide();
                _cardsPlayed[2].Hide();

                _shuffledCards[0].Hide();
                _shuffledCards[1].Hide();
                _shuffledCards[2].Hide();

                for (var i = 0; i < Mariasek.Engine.Game.NumPlayers; i++)
                {
                    _poslStych[i][0].Hide();
                    _poslStych[i][1].Hide();
                    _poslStych[i][2].Hide();
                }
                if (hlasy)
                {
                    for (var i = 0; i < Mariasek.Engine.Game.NumPlayers; i++)
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
            });
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
            var roundWinnerPlayerIndex = g.CurrentRound.roundWinner.PlayerIndex;
            var stych = _stychy[roundWinnerPlayerIndex];
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
                    stych.Sprite.SpriteRectangle = _cardsPlayed[roundWinnerPlayerIndex].SpriteRectangle;
                    stych.Show();
                })
                .FlipToBack()
                .Invoke(() =>
                {
                    _stareStychy[roundWinnerPlayerIndex].ZIndex = (g.RoundNumber - 1) * 3;
                    _stareStychy[roundWinnerPlayerIndex].Show();
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

        private void GameActivated(object sender, EventArgs e)
        {
            if (g != null)
            {
                g.DebugString.AppendLine("Game activated");
            }
        }

        private void GameDeactivated(object sender, EventArgs e)
        {
            if (g != null)
            {
                g.DebugString.AppendLine("Game deactivated");
            }
        }

        private void Activated(object sender)
        {
            if (g != null)
            {
                AmendCardScaleFactor();
            }
        }

        //Addresses bug File.Copy throwing UnauthorizedAccessException in Xamarin.Android 9.4+
        //See https://forums.xamarin.com/discussion/163424/file-copy-throws-system-unauthorizedaccessexception-but-file-is-copied-successful
        //See https://github.com/xamarin/xamarin-android/issues/3426
        //See https://github.com/mono/mono/issues/16032
        private void FileCopy(string source, string destination, bool overwrite = true)
        {
            try
            {
                File.Copy(source, destination, overwrite);
            }
            catch (Exception ex)
            {
                File.WriteAllText(destination, File.ReadAllText(source));
            }
        }
    }
}
