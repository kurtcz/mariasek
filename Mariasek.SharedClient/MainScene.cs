﻿using System;
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
using Microsoft.Xna.Framework.GamerServices;
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
        private Sprite[] _cardsPlayed;
        private Sprite[][] _hlasy;
        private ClickableArea _overlay;
        private Button _newGameBtn;
        private Button _menuBtn;
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
        private Label _trumpLabel1, _trumpLabel2, _trumpLabel3;
        private Label[] _trumpLabels;
        private Label _msgLabel;
        private TextBox _bubble1,  _bubble2,  _bubble3;

        #pragma warning restore 414
        #endregion

        private Mariasek.Engine.New.Game g;
        private const int _bubbleTime = 1000;
        private Task _gameTask, _bubble1Task, _bubble2Task, _bubble3Task;
        private SynchronizationContext _synchronizationContext;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly AutoResetEvent _evt = new AutoResetEvent(false);
        private int _aiMessageIndex;
        private int _currentStartingPlayerIndex = -1;
        private Mariasek.Engine.New.Configuration.ParameterConfigurationElementCollection _aiConfig;
        private string _historyFilePath = Path.Combine (
            Environment.GetFolderPath (Environment.SpecialFolder.Personal),
            "Mariasek.history");

        private GameState _state;
        private volatile Bidding _bidding;
        private volatile Hra _gameTypeChosen;
        private volatile GameFlavour _gameFlavourChosen;
        private volatile Hra _bid;
        private volatile Card _cardClicked;
        private volatile Card _trumpCardChosen;
        private volatile List<Card> _talon;

        public MainScene(MariasekMonoGame game)
            : base(game)
        {
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
            _aiConfig.Add("SimulationsPerRound", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "SimulationsPerRound",
                    Value = "50"
                });
            _aiConfig.Add("RuleThreshold", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "RuleThreshold",
                    Value = "80"
                });
            _aiConfig.Add("GameThreshold", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "GameThreshold",
                    Value = "80"
                });
        }

        /// <summary>
        /// Allows the game component to perform any initialization it needs to before starting
        /// to run.  This is where it can query for any required services and load content.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            _synchronizationContext = SynchronizationContext.Current;
            _cardsPlayed = new []
            {
                new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight / 2f - 100) },
                new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth / 2f - 100, Game.VirtualScreenHeight / 2f - 150) },
                new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth / 2f + 100, Game.VirtualScreenHeight / 2f - 150) }
            };
            _hlasy = new []
            {
                new []
                {
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth - 100, Game.VirtualScreenHeight / 2f) },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth - 150, Game.VirtualScreenHeight / 2f) },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth - 200, Game.VirtualScreenHeight / 2f) },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth - 250, Game.VirtualScreenHeight / 2f) },
                },
                new []
                {
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(100, 130) },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(150, 130) },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(200, 130) },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(250, 130) },
                },
                new []
                {
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth - 100, 130) },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth - 150, 130) },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth - 200, 130) },
                    new Sprite(this, Game.CardTextures) { Position = new Vector2(Game.VirtualScreenWidth - 250, 130) },
                }
            };
            _overlay = new ClickableArea(this)
            {
                Width = Game.VirtualScreenWidth,
                Height = Game.VirtualScreenHeight,
                IsEnabled = false
            };
            _overlay.TouchUp += OverlayTouchUp;
            _newGameBtn = new Button(this)
                {
                    Text = "Nová hra",
                    Position = new Vector2(10, 10)                
                };
            _newGameBtn.Click += NewGameBtnClicked;
            _menuBtn = new Button(this)
                {
                    Text = "Menu",
                    Position = new Vector2(10, 70)
                };
            _menuBtn.Click += MenuBtnClicked;
            _okBtn = new Button(this)
            {
                Text = "OK",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 50, Game.VirtualScreenHeight / 2f - 100),
                //Position = new Vector2(10, 60),
                IsEnabled = false
            };
            _okBtn.Click += OkBtnClicked;
            _okBtn.Hide();
            gtHraButton = new Button(this)
            {
                Text = "Hra",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 325, Game.VirtualScreenHeight / 2f - 100),
                Tag = Hra.Hra
            };
            gtHraButton.Click += GtButtonClicked;
            gtHraButton.Hide();
            gt7Button = new Button(this)
            {
                Text = "Sedma",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 215, Game.VirtualScreenHeight / 2f - 100),
                Tag = Hra.Hra | Hra.Sedma
            };
            gt7Button.Click += GtButtonClicked;
            gt7Button.Hide();
            gt100Button = new Button(this)
            {
                Text = "Kilo",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 105, Game.VirtualScreenHeight / 2f - 100),
                Tag = Hra.Kilo
            };
            gt100Button.Click += GtButtonClicked;
            gt100Button.Hide();
            gt107Button = new Button(this)
            {
                Text = "Stosedm",
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 5, Game.VirtualScreenHeight / 2f - 100),
                Tag = Hra.Kilo | Hra.Sedma
            };
            gt107Button.Click += GtButtonClicked;
            gt107Button.Hide();
            gtBetlButton = new Button(this)
            {
                Text = "Betl",
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 115, Game.VirtualScreenHeight / 2f - 100),
                Tag = Hra.Betl
            };
            gtBetlButton.Click += GtButtonClicked;
            gtBetlButton.Hide();
            gtDurchButton = new Button(this)
            {
                Text = "Durch",
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 225, Game.VirtualScreenHeight / 2f - 100),
                Tag = Hra.Durch
            };
            gtDurchButton.Click += GtButtonClicked;
            gtDurchButton.Hide();
            gtButtons = new[] { gtHraButton, gt7Button, gt100Button, gt107Button, gtBetlButton, gtDurchButton };
            gfDobraButton = new Button(this)
            {
                Text = "Dobrá",
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 105, Game.VirtualScreenHeight / 2f - 100),
                Tag = GameFlavour.Good
            };
            gfDobraButton.Click += GfButtonClicked;
            gfDobraButton.Hide();
            gfSpatnaButton = new Button(this)
            {
                Text = "Špatná",
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 5, Game.VirtualScreenHeight / 2f - 100),
                Tag = GameFlavour.Bad,
                IsEnabled = false
            };
            gfSpatnaButton.Click += GfButtonClicked;
            gfSpatnaButton.Hide();
            gfButtons = new[] { gfDobraButton, gfSpatnaButton };
            _trumpLabel1 = new Label(this)
            { 
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 100, 5),
                Width = 200,
                Height = 50
            };
            _trumpLabel1.Hide();
            _trumpLabel2 = new Label(this)
            { 
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Middle,
                Position = new Vector2(120, 5),
                Width = 200,
                Height = 50
            };
            _trumpLabel2.Hide();
            _trumpLabel3 = new Label(this)
            { 
                HorizontalAlign = HorizontalAlignment.Right,
                VerticalAlign = VerticalAlignment.Middle,
                Position = new Vector2(Game.VirtualScreenWidth - 210, 5),
                Width = 200,
                Height = 50
            };
            _trumpLabel3.Hide();
            _trumpLabels = new[] { _trumpLabel1, _trumpLabel2, _trumpLabel3 };
            _msgLabel = new Label(this)
            { 
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                //Position = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight / 2f - 20),
                Position = new Vector2(10, 60),
                Width = (int)Game.VirtualScreenWidth - 20,
                Height = (int)Game.VirtualScreenHeight - 120,
                TextColor = Color.Yellow,
                TextRenderer = Game.FontRenderers["SegoeUI40Outl"]
            };
            _hand = new GameComponents.Hand(this, new Card[0]) { Centre = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight - 60) };
            _hand.Click += CardClicked;
            _hand.ShowArc((float)Math.PI / 2);
            _bubble1 = new TextBox(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2 - 75, Game.VirtualScreenHeight / 2 - 100),
                Width = 150,
                Height = 50,
                BackgroundColor = new Color(0x40, 0x40, 0x40),
                TextColor = Color.Yellow,
                BorderColor = Color.Yellow,
                Opacity = 0.8f,
                VerticalAlign = VerticalAlignment.Middle,
                HorizontalAlign = HorizontalAlignment.Center
            };
            _bubble1.Hide();
            _bubble2 = new TextBox(this)
            {
                Position = new Vector2(50, 80),
                Width = 150,
                Height = 50,
                BackgroundColor = new Color(0x40, 0x40, 0x40),
                TextColor = Color.Yellow,
                BorderColor = Color.Yellow,
                Opacity = 0.8f,
                VerticalAlign = VerticalAlignment.Middle,
                HorizontalAlign = HorizontalAlignment.Center
            };
            _bubble2.Hide();
            _bubble3 = new TextBox(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 200, 80),
                Width = 150,
                Height = 50,
                BackgroundColor = new Color(0x40, 0x40, 0x40),
                TextColor = Color.Yellow,
                BorderColor = Color.Yellow,
                Opacity = 0.8f,
                VerticalAlign = VerticalAlignment.Middle,
                HorizontalAlign = HorizontalAlignment.Center
            };
            _bubble3.Hide();
            flekBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 230, Game.VirtualScreenHeight / 2f - 150),
                Width = 150,
                Height = 50,
                Tag = Hra.Hra | Hra.Kilo | Hra.Betl | Hra.Durch
            };

            flekBtn.Click += BidButtonClicked;
            flekBtn.Hide();
            sedmaBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f - 75, Game.VirtualScreenHeight / 2f - 150),
                Width = 150,
                Height = 50,
                Tag = Hra.Sedma | Hra.SedmaProti
            };
            sedmaBtn.Click += BidButtonClicked;
            sedmaBtn.Hide();
            kiloBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth / 2f + 80, Game.VirtualScreenHeight / 2f - 150),
                Width = 150,
                Height = 50,
                Tag = Hra.KiloProti
            };
            kiloBtn.Click += BidButtonClicked;
            kiloBtn.Hide();

            LoadHistory();

            //LightShader = Game.Content.Load<Texture2D>("Spotlight");
            Background = Game.Content.Load<Texture2D>("wood2");
            ClearTable(true);

        }

        public void LoadHistory()
        {
            var xml = new XmlSerializer(typeof(List<MoneyCalculatorBase>));
            try
            {
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
            var xml = new XmlSerializer(typeof(List<MoneyCalculatorBase>));
            try
            {
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

        private void CancelRunningTask()
        {
            if (_gameTask != null && _gameTask.Status == TaskStatus.Running)
            {
                _synchronizationContext.Send(_ => ClearTable(true), null);
                _cancellationTokenSource.Cancel();
                _evt.Set();
                Task.WaitAll(new[] { _gameTask, _bubble1Task, _bubble2Task, _bubble3Task }.Where(i => i != null).ToArray());
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
            _newGameBtn.IsEnabled = false;
            CancelRunningTask();
            _gameTask = Task.Run(() => {
                g = new Mariasek.Engine.New.Game()
                    {
                        SkipBidding = false//true
                    };
                g.RegisterPlayers(
                    //new DummyPlayer(g) { Name = "Hráč 1" },
                    new HumanPlayer(g, this) { Name = "Hráč 1" },
                    //new DummyPlayer(g) { Name = "Hráč 2" },
                    //new DummyPlayer(g) { Name = "Hráč 3" }
                    new AiPlayer(g, _aiConfig) { Name = "Hráč 2" },
                    new AiPlayer(g, _aiConfig) { Name = "Hráč 3" }
                );
                _currentStartingPlayerIndex = (_currentStartingPlayerIndex + 1) % Mariasek.Engine.New.Game.NumPlayers;
                g.NewGame(_currentStartingPlayerIndex, null);
                g.GameTypeChosen += GameTypeChosen;
                g.BidMade += BidMade;
                g.CardPlayed += CardPlayed;
                g.RoundStarted += RoundStarted;
                g.RoundFinished += RoundFinished;
                g.GameFinished += GameFinished;

                _state = GameState.NotPlaying;
                ClearTable(true);
                g.PlayGame(_cancellationTokenSource.Token);
            },  _cancellationTokenSource.Token);
        }

        public void MenuBtnClicked(object sender)
        {
            Game.MenuScene.PopulateControls();
            Game.MenuScene.SetActive();
        }

        public void CardClicked(object sender)
        {
            var button = sender as CardButton;

            _cardClicked = (Card)button.Tag;
            System.Diagnostics.Debug.WriteLine(string.Format("{0} clicked", _cardClicked));
            switch (_state)
            {
                case GameState.ChooseTalon:
                    if (_talon.Count == 2 && button.IsSelected)
                    {
                        button.IsSelected = !button.IsSelected;
                        return;
                    }
                    if (button.IsSelected)
                    {
                        //selected
                        _talon.Add(_cardClicked);
                    }
                    else
                    {
                        //unselected
                        _talon.Remove(_cardClicked);
                    }
                    _okBtn.IsEnabled = _talon.Count == 2;
                    break;
                case GameState.ChooseTrump:
                    if (_cardClicked == null)
                        return;
                    _trumpCardChosen = _cardClicked;
                    _state = GameState.NotPlaying;
                    HideMsgLabel();
                    //TODO: animate revealing of the trump card to the opponents
                    var origPosition = _hlasy[0][0].Position;
                    _hlasy[0][0].Position = button.Position;
                    _hlasy[0][0].MoveTo(origPosition, 1000);
                    if (button.Sprite.IsVisible)
                    {
                        _hlasy[0][0].Texture = Game.CardTextures;
                        _hlasy[0][0].SpriteRectangle = _cardClicked.ToTextureRect();
                    }
                    else
                    {
                        _hlasy[0][0].Texture = Game.ReverseTexture;
                        _hlasy[0][0].SpriteRectangle = Game.ReverseTexture.Bounds;
                    }
                    _hlasy[0][0].Show();
                    button.Hide();
                    Task.Run(() => {
                        while(_hlasy[0][0].Position != origPosition)
                        {
                            Thread.Sleep(100);
                        }
                        _hlasy[0][0].Texture = Game.CardTextures;
                        _hlasy[0][0].SpriteRectangle = _cardClicked.ToTextureRect();
                        _evt.Set();
                    });
                    //_evt.Set();
                    break;
                //case GameState.ChooseGameType:
                case GameState.Play:
                    if (_cardClicked == null)
                        return;
                    _state = GameState.NotPlaying;
                    HideMsgLabel();
                    _evt.Set();
                    break;
//                case GameState.RoundFinished:
//                    button.IsSelected = false;
//                    _state = GameState.NotPlaying;
//                    ClearTable();
//                    HideMsgLabel();
//                    _evt.Set();
//                    break;
                case GameState.GameFinished:
                    _state = GameState.NotPlaying;
                    ClearTable(true);
                    HideMsgLabel();
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
            //HideMsgLabel();
            foreach (var btn in gfButtons)
            {
                btn.Hide();
            }
            //_okBtn.Hide();
            _state = GameState.NotPlaying;
            _gameFlavourChosen = (GameFlavour)(sender as Button).Tag;
            _evt.Set();
        }

        public void BidButtonClicked(object sender)
        {
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
            _synchronizationContext.Send(_ =>
                {
                    ShowMsgLabel("Vyber trumfovou kartu", false);
                    _state = GameState.ChooseTrump;
                    UpdateHand(cardsNotRevealed: 5);
                }, null);
            WaitForUIThread();
            return _cardClicked;
        }

        public List<Card> ChooseTalon()
        {
            g.ThrowIfCancellationRequested();
            _synchronizationContext.Send(_ =>
                {
                    _talon = new List<Card>();
                    ShowMsgLabel("Vyber si talon", false);
                    _okBtn.Show();
                    _state = GameState.ChooseTalon;
                    UpdateHand(cardToHide: _trumpCardChosen);
                }, null);
            WaitForUIThread();
            return _talon;
        }

        public GameFlavour ChooseGameFlavour()
        {
            g.ThrowIfCancellationRequested();
            if (g.GameStartingPlayerIndex == 0)
            {
                //Pokud zacinam, tak se logika zjednodusuje a dobrou/spatnou hru vybiram pomoci konkretni hry
                _synchronizationContext.Send(_ =>
                    {
                        ChooseGameTypeInternal(Hra.Hra);
                    }, null);
                WaitForUIThread();
                if (_gameTypeChosen != Hra.Betl && _gameTypeChosen != Hra.Durch)
                {
                    return GameFlavour.Good;
                }
                else
                {
                    return GameFlavour.Bad;
                }
            }
            else
            {
                //Pokud nezacinam, tak musim odpovedet
                _synchronizationContext.Send(_ =>
                    {
                        UpdateHand();
                        foreach (var gfButton in gfButtons)
                        {
                            gfButton.Show();
                        }
                        _state = GameState.ChooseGameFlavour;
                    }, null);
                WaitForUIThread();
                return _gameFlavourChosen;
            }
        }

        private void ChooseGameTypeInternal(Hra minimalBid)
        {
            UpdateHand(cardToHide: _trumpCardChosen); //abych nevidel karty co jsem hodil do talonu
            foreach (var gtButton in gtButtons)
            {
                gtButton.IsEnabled = (Hra)gtButton.Tag >= minimalBid && (Hra)gtButton.Tag < Hra.Betl;
                gtButton.Show();
            }
            ShowMsgLabel("Co budeš hrát?", false);
            _state = GameState.ChooseGameType;
        }

        public Hra ChooseGameType(Hra minimalBid)
        {
            g.ThrowIfCancellationRequested();
            //pokud zacinam, tak jsem typ hry uz vybral v ChooseGameFlavour() jinak zobrazit tlacitka a cekat na volbu
            if (g.GameStartingPlayerIndex != 0)
            {
                _synchronizationContext.Send(_ =>
                    {
                        ChooseGameTypeInternal(minimalBid);
                    }, null);
                WaitForUIThread();
            }
            Task.WaitAll(new[] { _bubble1Task, _bubble2Task, _bubble3Task }.Where(i => i != null).ToArray());
            return _gameTypeChosen;
        }

        private int _numberOfDoubles = 0;

        public Hra GetBidsAndDoubles(Bidding bidding)
        {
            g.ThrowIfCancellationRequested();
            Task.WaitAll(new[] { _bubble1Task, _bubble2Task, _bubble3Task }.Where(i => i != null).ToArray());
            _synchronizationContext.Send(_ =>
                {
                    UpdateHand(cardToHide: _trumpCardChosen);
                }, null);
            _hand.AnimationEvent.Wait();
            _synchronizationContext.Send(_ =>
                {
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
                    UpdateHand();
                }, null);
            WaitForUIThread(); 
            return _cardClicked;
        }

        #endregion

        #region Game event handlers

        public void GameFlavourChosen(object sender, GameFlavourChosenEventArgs e)
        {
            if (e.PlayerIndex == _currentStartingPlayerIndex)
            {
                if (e.Flavour == GameFlavour.Good)
                {
                    ShowBubble(e.PlayerIndex, "Barva");
                }
                else
                {
                    //zacinajici hrac vybral spatnou barvu - nezobrazujeme nic, pozswji zobrazime konkretni typ spatne hry
                }
            }
            else
            {
                ShowBubble(e.PlayerIndex, e.Flavour.ToDescription());
            }
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
                    _trumpLabels[_currentStartingPlayerIndex].Text = string.Format("{0} {1}", e.GameType, e.TrumpCard.Suit.ToDescription());
                    foreach(var trumpLabel in _trumpLabels)
                    {
                        trumpLabel.Hide();
                    }
                    _trumpLabels[_currentStartingPlayerIndex].Show();
                    imgs[e.GameStartingPlayerIndex].SpriteRectangle = e.TrumpCard.ToTextureRect();
                    imgs[e.GameStartingPlayerIndex].Show();
                }, null);
        }

        public void BidMade(object sender, BidEventArgs e)
        {
            ShowBubble(e.Player.PlayerIndex, e.Description);
        }

        public void CardPlayed(object sender, Round r)
        {
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
                    rect = lastCard.ToTextureRect();
                    if (lastHlas)
                    {
                        _hlasy[lastPlayer.PlayerIndex][lastPlayer.Hlasy - 1].SpriteRectangle = rect;
                        _hlasy[lastPlayer.PlayerIndex][lastPlayer.Hlasy - 1].Show();
                    }
                    else
                    {
                        _cardsPlayed[lastPlayer.PlayerIndex].SpriteRectangle = rect;
                        _cardsPlayed[lastPlayer.PlayerIndex].Show();
                    }

                    UpdateHand();
                    _hand.ShowArc((float)Math.PI / 2);
                }, null);
        }

        public void RoundStarted(object sender, Round r)
        {
            if (r.number == 1)
            {
                _synchronizationContext.Send(_ => ClearTable(true), null);
            }
            if (r.player1.PlayerIndex != 0)
            {
                ShowThinkingMessage();
            }
        }

        public void RoundFinished(object sender, Round r)
        {
            if (r.number < 10)
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
            var sb = new StringBuilder();
            var baseBet = 1f;

            Game.Money.Add(results);
            SaveHistory();

            sb.AppendFormat("{0} {1} hru ({2} {3}). Skóre {4}:{5}",
                g.GameStartingPlayer.Name,
                g.Results.GameWon ? "vyhrál" : "prohrál",
                g.GameType, g.trump.ToDescription(), g.Results.PointsWon, g.Results.PointsLost);
            if ((g.GameType & Hra.Sedma) != 0)
            {
                sb.AppendFormat("\n{0} {1} sedmu.", g.GameStartingPlayer.Name,
                    g.Results.SevenWon ? "vyhrál" : "prohrál");
            }
            sb.AppendFormat("\nVyúčtování:");
            for (var i = 0; i < Mariasek.Engine.New.Game.NumPlayers; i++)
            {
                sb.AppendFormat("\n{0}: {1}", g.players[i].Name,
                    (g.Results.MoneyWon[i] * baseBet).ToString("C", CultureInfo.CreateSpecificCulture("cs-CZ")));
            }
            _newGameBtn.IsEnabled = true;
            ClearTable(true);
            ShowMsgLabel(sb.ToString(), false);
        }

        #endregion

        private void UpdateHand(int cardsNotRevealed = 0, Card cardToHide = null)
        {
            _hand.UpdateHand(g.players[0].Hand.ToArray(), cardsNotRevealed, cardToHide);
            //_hand.ShowArc((float)Math.PI / 2);
            _hand.ShowStraight((int)Game.VirtualScreenWidth - 20);
        }

        private void RunBubbleTask(TextBox tb, string message, bool autoHide, out Task t)
        {
            t = Task.Run(() =>
                {
                    try
                    {
                        g.ThrowIfCancellationRequested();
                        System.Diagnostics.Debug.WriteLine(string.Format("BubbleTask: Showing message '{0}'", message));
                        _synchronizationContext.Send(_ =>
                            {
                                tb.Text = message;
                                tb.Show();
                            }, null);
                        if (!autoHide)
                        {
                            return;
                        }
                        System.Diagnostics.Debug.WriteLine(string.Format("BubbleTask: Waiting"));
                        for (var i = 0; i < _bubbleTime; i += 100)
                        {
                            g.ThrowIfCancellationRequested();
                            Thread.Sleep(100);
                        }
                            System.Diagnostics.Debug.WriteLine(string.Format("BubbleTask: Hiding message '{0}'", message));
                        _synchronizationContext.Send(_ =>
                            {
                                tb.Hide();
                            }, null);
                    }
                    catch (TaskCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine(string.Format("BubbleTask: TaskCanceledException caught"));
                    }
                }, _cancellationTokenSource.Token);
        }

        private void ShowBubble(int bubbleNo, string message, bool autoHide = true)
        {
            switch (bubbleNo)
            {
                case 0:
                    if (_bubble3Task != null)
                    {
                        Task.WaitAll(new [] { _bubble3Task });
                    }
                    RunBubbleTask(_bubble1, message, autoHide, out _bubble1Task);
                    break;
                case 1:
                    if (_bubble1Task != null)
                    {
                        Task.WaitAll(new[] { _bubble1Task });
                    }
                    RunBubbleTask(_bubble2, message, autoHide, out _bubble2Task);
                    break;
                case 2:
                    if (_bubble2Task != null)
                    {
                        Task.WaitAll(new[] { _bubble2Task });
                    }
                    RunBubbleTask(_bubble3, message, autoHide, out _bubble3Task);
                    break;
            }
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
            }

            _msgLabel.Hide();
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

        public void HideMsgLabel()
        {
            _msgLabel.Hide();
            _okBtn.Hide();
        }

        private void OverlayTouchUp(object sender, TouchLocation tl)
        {
            _state = GameState.NotPlaying;
            ClearTable();
            HideMsgLabel();
            HideInvisibleClickableOverlay();
            _evt.Set();
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

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            //TODO
        }
    }
}

