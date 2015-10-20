using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using log4net;
using Mariasek.Engine.New;
using Mariasek.Engine.New.Configuration;
using Mariasek.Engine.New.Schema;
using Microsoft.Win32;

namespace Mariasek.TesterGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window//, INotifyPropertyChanged
    {
        #region Private fields

        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IPlayerSettingsReader _playerSettingsReader;
        private readonly SynchronizationContext _synchronizationContext;
        private readonly AutoResetEvent _evt = new AutoResetEvent(false);
        private Task _starterTask;
        private Task _gameTask;
        private Task _bubble1Task;
        private Task _bubble2Task;
        private Task _bubble3Task;
        private CancellationTokenSource _cancellationTokenSource;
        private Game g;
        private Card _cardClicked;
        private List<Card> _talon;
        private Hra _gameTypeChosen;
        private Hra _bid;
        private GameState _state = GameState.NotPlaying;
        private int _aiMessageIndex;
        private int _bubbleTime;
        private int _currentStartingPlayerIndex;
        private Deck _deck;

        public bool ShowAllCards
        {
            get { return (bool)GetValue(ShowAllCardsProperty); }
            set { SetValue(ShowAllCardsProperty, value); }
        }
        public static readonly DependencyProperty ShowAllCardsProperty = DependencyProperty.Register("ShowAllCards", typeof(bool), typeof(MainWindow), new UIPropertyMetadata(false));
        
        private bool CanSaveGame;
        private bool CanRewind;
        private bool CanEdit;
        private bool CanShowProbabilities;

        private readonly HandWindow[] hw;
        private readonly ProbabilityWindow pw;
        private readonly SettingsWindow settingsWindow;
        private LoggerWindow loggerWindow;
        private readonly Button[] gtButtons;

        #endregion

        #region Public methods

        public MainWindow()
        {
            _playerSettingsReader = new Mariasek.WinSettings.PlayerSettingsReader();
            hw = new[]
                 {
                     new HandWindow(),
                     new HandWindow()
                 };
            pw = new ProbabilityWindow(this);
            settingsWindow = new SettingsWindow();
            //loggerWindow = new LoggerWindow();

            _synchronizationContext = SynchronizationContext.Current;
            InitializeSettingsWindow();
            InitializeComponent();
            _currentStartingPlayerIndex = AppSettings.GetInt("StartingPlayerIndex", 0);
            var scaleFactor = AppSettings.GetFloat("ScaleFactor", 0.8f);
            _bubbleTime = AppSettings.GetInt("BubbleTime", 1000);
            Width *=  scaleFactor;
            Height *= scaleFactor;
            ShowAllCards = AppSettings.GetBool("Cheat", true);
            gtButtons = new[] { gtHraButton, gt7Button, gt100Button, gt107Button, gtBetlButton, gtDurchButton };
            gtHraButton.Tag = Hra.Hra;
            gt7Button.Tag = Hra.Hra | Hra.Sedma;
            gt100Button.Tag = Hra.Kilo;
            gt107Button.Tag = Hra.Kilo | Hra.Sedma;
            gtBetlButton.Tag = Hra.Betl;
            gtDurchButton.Tag = Hra.Durch;
        }

        public void UpdateProbabilityWindow()
        {
            var sb = new StringBuilder();
            IStatsPlayer statsPlayer = null;

            if (pw == null) return;

            switch (pw.viewAsPlayer)
            {
                case 0:
                    statsPlayer = g.players[0] as IStatsPlayer;
                    break;
                case 1:
                    statsPlayer = g.players[1] as IStatsPlayer;
                    break;
                case 2:
                    statsPlayer = g.players[2] as IStatsPlayer;
                    break;
            }
            switch (pw.showPlayer)
            {
                case 0:
                    pw.Button1.Focus();
                    break;
                case 1:
                    pw.Button2.Focus();
                    break;
                case 2:
                    pw.Button3.Focus();
                    break;
            }

            var probabilities = statsPlayer != null ? statsPlayer.Probabilities : null;

            if (probabilities == null)
            {
                pw.TextBox.Text = String.Empty;
                return;
            }

            foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().OrderByDescending(i => (int)i))
            {
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                {
                    sb.AppendFormat("{0} {1}:\t{2:0.00}\t", h, b,
                        probabilities.CardProbability(pw.showPlayer, new Card(b, h)));
                }
                sb.Append("\n");
            }
            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                sb.AppendFormat("\n{0}:\t{1}", b, probabilities.SuitProbability(pw.showPlayer, b, g.RoundNumber - 1));
            }

            sb.AppendFormat("\n\nPossible combinations:\t{0}",
                probabilities.PossibleCombinations(pw.showPlayer, g.RoundNumber - 1));
            pw.TextBox.Text = sb.ToString();
            //pw.Show();
        }

        #endregion

        #region Private methods

        private void InitializeSettingsWindow()
        {
            foreach (var description in GetDescriptions(typeof(CardSelectionStrategy)))
            {
                settingsWindow.CardSelectionComboBox.Items.Add(description);
            }
            settingsWindow.StartingPlayerComboBox.Items.Add("Human");
            settingsWindow.StartingPlayerComboBox.Items.Add("Player2");
            settingsWindow.StartingPlayerComboBox.Items.Add("Player3");

            var playersSection = ConfigurationManager.GetSection("players") as Mariasek.WinSettings.PlayersConfigurationSection;
            var playersSettings = new[] { playersSection.Player1, playersSection.Player2, playersSection.Player3 };
            var aiSettings = playersSettings.FirstOrDefault(i => i.Type.EndsWith(".AiPlayer"));

            if (aiSettings == null)
                return;

            settingsWindow.AiCheating.IsChecked = bool.Parse(aiSettings.Parameters["AiCheating"].Value);
            settingsWindow.SkipBidding.IsChecked = AppSettings.GetBool("SkipBidding", false);
            settingsWindow.RoundsToComputeTextBox.Text = aiSettings.Parameters["RoundsToCompute"].Value;
            settingsWindow.CardSelectionComboBox.SelectedIndex = (int)Enum.Parse(typeof(CardSelectionStrategy), aiSettings.Parameters["CardSelectionStrategy"].Value);
            settingsWindow.SimulationsPerRoundTextBox.Text = aiSettings.Parameters["SimulationsPerRound"].Value;
            settingsWindow.RuleThreshold.Text = aiSettings.Parameters["RuleThreshold"].Value;
            settingsWindow.GameThreshold.Text = aiSettings.Parameters["GameThreshold"].Value;
            settingsWindow.RotateStartingPlayer.IsChecked = AppSettings.GetBool("RotateStartingPlayer", false);
            settingsWindow.StartingPlayerComboBox.SelectedIndex = AppSettings.GetInt("StartingPlayerIndex", 0);
            settingsWindow.ShuffleCards.IsChecked = AppSettings.GetBool("ShuffleCards", true);
        }

        private static IEnumerable<string> GetDescriptions(Type type)
        {
            var descs = new List<string>();
            var names = Enum.GetNames(type);
            foreach (var name in names)
            {
                var field = type.GetField(name);
                var fds = field.GetCustomAttributes(typeof (DescriptionAttribute), true);
                foreach (DescriptionAttribute fd in fds)
                {
                    descs.Add(fd.Description);
                }
            }
            return descs;
        }

        private void ShowMsgLabel(string msg, bool showButton)
        {
            this.Dispatch((o, p1) =>
                          {
                              msgLabel.Content = p1;
                              msgLabel.Visibility = Visibility.Visible;
                              okButton.Visibility = showButton ? Visibility.Visible : Visibility.Hidden;
                          }, msg);
        }

        private void HideMsgLabel()
        {
            this.Dispatch(o =>
                          {
                              msgLabel.Visibility = Visibility.Hidden;
                              okButton.Visibility = Visibility.Hidden;
                          });
        }

        private void ClearHand()
        {
            imgHand1.ClearValue(Image.SourceProperty);
            imgHand2.ClearValue(Image.SourceProperty);
            imgHand3.ClearValue(Image.SourceProperty);
            imgHand4.ClearValue(Image.SourceProperty);
            imgHand5.ClearValue(Image.SourceProperty);
            imgHand6.ClearValue(Image.SourceProperty);
            imgHand7.ClearValue(Image.SourceProperty);
            imgHand8.ClearValue(Image.SourceProperty);
            imgHand9.ClearValue(Image.SourceProperty);
            imgHand10.ClearValue(Image.SourceProperty);
            imgHand11.ClearValue(Image.SourceProperty);
            imgHand12.ClearValue(Image.SourceProperty);

            imgHand1.ClearValue(FrameworkElement.TagProperty);
            imgHand2.ClearValue(FrameworkElement.TagProperty);
            imgHand3.ClearValue(FrameworkElement.TagProperty);
            imgHand4.ClearValue(FrameworkElement.TagProperty);
            imgHand5.ClearValue(FrameworkElement.TagProperty);
            imgHand6.ClearValue(FrameworkElement.TagProperty);
            imgHand7.ClearValue(FrameworkElement.TagProperty);
            imgHand8.ClearValue(FrameworkElement.TagProperty);
            imgHand9.ClearValue(FrameworkElement.TagProperty);
            imgHand10.ClearValue(FrameworkElement.TagProperty);
            imgHand11.ClearValue(FrameworkElement.TagProperty);
            imgHand12.ClearValue(FrameworkElement.TagProperty);
        }

        private void ClearTable(bool hlasy = false)
        {
            imgCardPlayed1.ClearValue(Image.SourceProperty);
            imgCardPlayed2.ClearValue(Image.SourceProperty);
            imgCardPlayed3.ClearValue(Image.SourceProperty);

            imgCardPlayed1.ClearValue(FrameworkElement.TagProperty);
            imgCardPlayed2.ClearValue(FrameworkElement.TagProperty);
            imgCardPlayed3.ClearValue(FrameworkElement.TagProperty);

            if (hlasy)
            {
                imgPlayer1Hlas1.ClearValue(Image.SourceProperty);
                imgPlayer1Hlas2.ClearValue(Image.SourceProperty);
                imgPlayer1Hlas3.ClearValue(Image.SourceProperty);
                imgPlayer1Hlas4.ClearValue(Image.SourceProperty);
                imgPlayer2Hlas1.ClearValue(Image.SourceProperty);
                imgPlayer2Hlas2.ClearValue(Image.SourceProperty);
                imgPlayer2Hlas3.ClearValue(Image.SourceProperty);
                imgPlayer2Hlas4.ClearValue(Image.SourceProperty);
                imgPlayer3Hlas1.ClearValue(Image.SourceProperty);
                imgPlayer3Hlas2.ClearValue(Image.SourceProperty);
                imgPlayer3Hlas3.ClearValue(Image.SourceProperty);
                imgPlayer3Hlas4.ClearValue(Image.SourceProperty);

                imgPlayer1Hlas1.ClearValue(FrameworkElement.TagProperty);
                imgPlayer1Hlas2.ClearValue(FrameworkElement.TagProperty);
                imgPlayer1Hlas3.ClearValue(FrameworkElement.TagProperty);
                imgPlayer1Hlas4.ClearValue(FrameworkElement.TagProperty);
                imgPlayer2Hlas1.ClearValue(FrameworkElement.TagProperty);
                imgPlayer2Hlas2.ClearValue(FrameworkElement.TagProperty);
                imgPlayer2Hlas3.ClearValue(FrameworkElement.TagProperty);
                imgPlayer2Hlas4.ClearValue(FrameworkElement.TagProperty);
                imgPlayer3Hlas1.ClearValue(FrameworkElement.TagProperty);
                imgPlayer3Hlas2.ClearValue(FrameworkElement.TagProperty);
                imgPlayer3Hlas3.ClearValue(FrameworkElement.TagProperty);
                imgPlayer3Hlas4.ClearValue(FrameworkElement.TagProperty);
            }
            lblRule2.ClearValue(ContentProperty);
            lblRule3.ClearValue(ContentProperty);
        }

        private void UpdateHands()
        {
            if (g == null)
                return;

            this.Dispatch(o =>
                          {
                              Image[] imgs =
                              {
                                  imgHand1, imgHand2, imgHand3, imgHand4, imgHand5, imgHand6, imgHand7,
                                  imgHand8, imgHand9, imgHand10, imgHand11, imgHand12
                              };

                              ClearHand();
                              int skip = (12 - g.players[0].Hand.Count)/2;
                              var imgSrcConverter = new ImageSourceConverter();

                              for (int i = 0; i < g.players[0].Hand.Count; i++)
                              {
                                  string source =
                                      !showHandsMenuItem.IsChecked && _state == GameState.ChooseTrump && i >= 7
                                        ? "pack://application:,,,/Mariasek.TesterGUI;component/Images/karty/revers.png"
                                        : string.Format("pack://application:,,,/Mariasek.TesterGUI;component/Images/karty/{0}",
                                            CardResources.Images[g.players[0].Hand[i].Num]);

                                  imgs[skip + i].SetValue(UIElement.OpacityProperty, 1.0);
                                  imgs[skip + i].SetValue(FrameworkElement.TagProperty, g.players[0].Hand[i]);
                                  imgs[skip + i].SetValue(Image.SourceProperty,
                                      imgSrcConverter.ConvertFromString(source) as ImageSource);
                              }

                              for (int i = 0; i < 2; i++)
                              {
                                  Image[] hwimgs =
                                  {
                                      hw[i].imgHand1, hw[i].imgHand2, hw[i].imgHand3, hw[i].imgHand4,
                                      hw[i].imgHand5, hw[i].imgHand6, hw[i].imgHand7, hw[i].imgHand8,
                                      hw[i].imgHand9, hw[i].imgHand10, hw[i].imgHand11, hw[i].imgHand12
                                  };

                                  skip = (12 - g.players[i + 1].Hand.Count)/2;
                                  for (int j = 0; j < 12; j++)
                                  {
                                      hwimgs[j].SetValue(UIElement.OpacityProperty, 0.0);
                                  }
                                  for (int j = 0; j < g.players[i + 1].Hand.Count; j++)
                                  {
                                      string source =
                                          string.Format(
                                              "pack://application:,,,/Mariasek.TesterGUI;component/Images/karty/{0}",
                                              CardResources.Images[g.players[i + 1].Hand[j].Num]);

                                      hwimgs[skip + j].SetValue(UIElement.OpacityProperty, 1.0);
                                      hwimgs[skip + j].SetValue(FrameworkElement.TagProperty, g.players[i + 1].Hand[j]);
                                      hwimgs[skip + j].SetValue(Image.SourceProperty,
                                          imgSrcConverter.ConvertFromString(source) as ImageSource);
                                  }
                                  hw[i].Title = g.players[i + 1].Name;
                              }
                              OnShowHands(this, null);  //null indicates a call from within UpdateHands() method
                              CanShowProbabilities = true;
                          });
        }

        private void UpdateTable(Round r)
        {
            Card lastCard;
            AbstractPlayer lastPlayer;
            bool lastHlas;

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

            Image[] imgs = { imgCardPlayed1, imgCardPlayed2, imgCardPlayed3 };
            Label[] rules = { null, lblRule2, lblRule3 };
            Image[,] hlasy =
            {
                {imgPlayer1Hlas1, imgPlayer1Hlas2, imgPlayer1Hlas3, imgPlayer1Hlas4},
                {imgPlayer2Hlas1, imgPlayer2Hlas2, imgPlayer2Hlas3, imgPlayer2Hlas4},
                {imgPlayer3Hlas1, imgPlayer3Hlas2, imgPlayer3Hlas3, imgPlayer3Hlas4}
            };
            string source = string.Format("pack://application:,,,/Mariasek.TesterGUI;component/Images/karty/{0}",
                CardResources.Images[lastCard.Num]);
            var imgSrcConverter = new ImageSourceConverter();

            if (lastHlas)
            {
                //hlasy[lastPlayer.PlayerIndex, e.Player.Hlasy - 1].SetValue(Image.SourceProperty, new ImageSourceConverter().ConvertFromString(source) as ImageSource);
                hlasy[lastPlayer.PlayerIndex, lastPlayer.Hlasy - 1].Dispatch((o, p1, p2) => o.SetValue(p1, p2), Image.SourceProperty,
                    imgSrcConverter.ConvertFromString(source) as ImageSource);
            }
            else
            {
                //imgs[lastPlayer.PlayerIndex].SetValue(Image.SourceProperty, new ImageSourceConverter().ConvertFromString(source) as ImageSource);
                imgs[lastPlayer.PlayerIndex].Dispatch((o, p1, p2) => o.SetValue(p1, p2), Image.SourceProperty,
                    imgSrcConverter.ConvertFromString(source) as ImageSource);
            }
            if (rules[lastPlayer.PlayerIndex] != null)
            {
                var debugInfo = string.Format("{0}\n{1}x", g.players[lastPlayer.PlayerIndex].DebugInfo.Rule,
                                                                g.players[lastPlayer.PlayerIndex].DebugInfo.RuleCount);
                rules[lastPlayer.PlayerIndex].Dispatch((o, p1, p2) => o.SetValue(p1, p2), ContentProperty, debugInfo);
            }
        }

        private bool ToggleCardOpacity(Image img)
        {
            if (img.Opacity == 1.0)
            {
                img.Opacity = 0.5;
                return true; //selected
            }
            else //if (img.Opacity == 0.5)
            {
                img.Opacity = 1.0;
                return false; //unselected
            }
        }

        private void RunBubbleTask(TextBlock tb, Border b, string message, bool autoHide, out Task t)
        {
            t = Task.Run(() =>
            {
                try
                {
                    g.ThrowIfCancellationRequested();
                    _log.DebugFormat("BubbleTask: Showing message '{0}'", message);
                    _synchronizationContext.Send(_ =>
                    {
                        tb.Text = message;
                        b.Visibility = Visibility.Visible;
                    }, null);
                    if (!autoHide)
                    {
                        return;
                    }
                    _log.DebugFormat("BubbleTask: Waiting");
                    for (var i = 0; i < _bubbleTime; i += 100)
                    {
                        g.ThrowIfCancellationRequested();
                        Thread.Sleep(100);
                    }
                    _log.DebugFormat("BubbleTask: Hiding message '{0}'", message);
                    _synchronizationContext.Send(_ =>
                    {
                        b.Visibility = Visibility.Hidden;
                    }, null);
                }
                catch (TaskCanceledException)
                {
                    _log.Debug("BubbleTask: TaskCanceledException caught");
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
                    RunBubbleTask(bubble1Text, bubble1, message, autoHide, out _bubble1Task);
                    break;
                case 1:
                    if (_bubble1Task != null)
                    {
                        Task.WaitAll(new[] { _bubble1Task });
                    }
                    RunBubbleTask(bubble2Text, bubble2, message, autoHide, out _bubble2Task);
                    break;
                case 2:
                    if (_bubble2Task != null)
                    {
                        Task.WaitAll(new[] { _bubble2Task });
                    }
                    RunBubbleTask(bubble3Text, bubble3, message, autoHide, out _bubble3Task);
                    break;
            }
        }

        private void ShowThinkingMessage()
        {
            //TODO: Do not call ShowMsgLabel if HumanPlayer is the next one up
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

        private void GameFinished(object sender, MoneyCalculatorBase results)
        {
            _state = GameState.GameFinished;
            var sb = new StringBuilder();
            var baseBet = AppSettings.GetFloat("BaseBet", 1f);

            sb.AppendFormat("{0} {1} hru ({2} {3}). Skóre {4}:{5}",
                g.GameStartingPlayer.Name,
                g.Results.GameWon ? "vyhrál" : "prohrál",
                g.GameType, g.trump.HasValue ? g.trump.Value.Description() : "", g.Results.PointsWon, g.Results.PointsLost);
            if ((g.GameType & Hra.Sedma) != 0)
            {
                sb.AppendFormat("\n{0} {1} sedmu.", g.GameStartingPlayer.Name,
                    g.Results.SevenWon ? "vyhrál" : "prohrál");
            }
            sb.AppendFormat("\nVyúčtování:");
            for (var i = 0; i < Game.NumPlayers; i++)
            {
                sb.AppendFormat("\n{0}: {1}", g.players[i].Name,
                    (g.Results.MoneyWon[i] * baseBet).ToString("C", CultureInfo.CreateSpecificCulture("cs-CZ")));
            }
            ShowMsgLabel(sb.ToString(), false);
            CanRewind = true;
            CanSaveGame = true;
            var programFolder = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            g.SaveGame(System.IO.Path.Combine(programFolder, "_konec.hra"));
            _deck = g.GetDeckFromLastGame();
        }

        /// <summary>
        /// This methods waits for the running task to cancel. It shall be executed from the UI thread.
        /// </summary>
        private void CancelRunningTask()
        {
            if (_gameTask != null && _gameTask.Status == TaskStatus.Running)
            {
                _log.Debug("Cancelling task");
                _synchronizationContext.Send(_ => ClearTable(true), null);
                _cancellationTokenSource.Cancel();
                _evt.Set();
                _log.Debug("Waiting for task to cancel");
                Task.WaitAll(new[] { _gameTask, _bubble1Task, _bubble2Task, _bubble3Task }.Where(i => i != null).ToArray());
                _log.DebugFormat("Finished waiting for task. Game task status: {0}", _gameTask.Status);
            }
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// This method suspends the execution of the current thread until it is resumed by the UI thread via the _evt event being set.
        /// This method may be invoked only from the background task. Invoking it from a UI thread will cause an application to hang.
        /// </summary>
        private void WaitForUIThread()
        {
            _log.Debug("Waiting for UI thread");
            _evt.Reset();
            _evt.WaitOne();
            g.ThrowIfCancellationRequested();
        }

        #endregion

        #region HumanPlayer methods

        public Card ChooseTrump()
        {
            g.ThrowIfCancellationRequested();
            _synchronizationContext.Send(_ =>
            {
                ShowMsgLabel("Vyber trumfovou kartu", false);
                _state = GameState.ChooseTrump;
                UpdateHands();
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
                okButton.Visibility = Visibility.Visible;
                _state = GameState.ChooseTalon;
                UpdateHands();
            }, null);
            WaitForUIThread();
            return _talon;
        }

        public GameFlavour ChooseGameFlavour()
        {
            //TODO: betl a durch
            return GameFlavour.Good;
        }

        public Hra ChooseGameType(Hra minimalBid)
        {
            g.ThrowIfCancellationRequested();
            _synchronizationContext.Send(_ =>
            {
                UpdateHands(); //abych nevidel karty co jsem hodil do talonu
                foreach (var gtButton in gtButtons)
                {
                    gtButton.IsEnabled = (Hra)gtButton.Tag >= minimalBid && (Hra)gtButton.Tag < Hra.Betl;
                    gtButton.Visibility = Visibility.Visible;
                }
                ShowMsgLabel("Co budeš hrát?", false);
                _state = GameState.ChooseGameType;
                CanRewind = false;
            }, null);
            WaitForUIThread();
            return _gameTypeChosen;
        }

        private int _numberOfDoubles = 0;

        public Hra GetBidsAndDoubles(Bidding bidding)
        {
            g.ThrowIfCancellationRequested();
            Task.WaitAll(new[] { _bubble1Task, _bubble2Task, _bubble3Task }.Where(i => i != null).ToArray());
            _synchronizationContext.Send(_ =>
            {
                UpdateHands();
                var biddingWnd = new BiddingWindow(bidding);
                biddingWnd.ShowDialog();
                _bid = biddingWnd.Bid;
                //_evt.Set();
            }, null);
            //WaitForUIThread();
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
                CanSaveGame = false;
                CanRewind = true;
            }, null);
            WaitForUIThread(); 
            return _cardClicked;
        }

        #endregion

        #region Game event handlers

        private void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            g.ThrowIfCancellationRequested();
            _synchronizationContext.Send(_ =>
            {
                Image[] imgs = { imgPlayer1Hlas1, imgPlayer2Hlas1, imgPlayer3Hlas1 };
                var source = e.TrumpCard != null
                                ? string.Format("pack://application:,,,/Mariasek.TesterGUI;component/Images/karty/{0}",
                                    CardResources.Images[e.TrumpCard.Num])
                                : null;

                if (source != null)
                {
                    var imgSrcConverter = new ImageSourceConverter();
                    imgs[e.GameStartingPlayerIndex].SetValue(Image.SourceProperty, imgSrcConverter.ConvertFromString(source) as ImageSource);
                }
                lblTrump.Content = string.Format("{0}: {1} {2}", g.GameStartingPlayer.Name, g.GameType, g.trump.HasValue? g.trump.Value.Description() : "");
            }, null);
        }

        private void BidMade(object sender, BidEventArgs e)
        {
            _log.DebugFormat("Bidding task: bid made: {0} {1}", e.Player.Name, e.Description);
            ShowBubble(e.Player.PlayerIndex, e.Description);
        }

        private void CardPlayed(object sender, Round r)
        {
            ShowThinkingMessage();
            g.ThrowIfCancellationRequested();
            _synchronizationContext.Send(_ =>
            {
                CanSaveGame = false;
                CanRewind = false;
                UpdateTable(r);
                UpdateHands();
                UpdateProbabilityWindow();
            }, null);
        }

        private void RoundStarted(object sender, Round r)
        {
            if (r.number == 1)
            {
                _synchronizationContext.Send(_ => ClearTable(true), null);
            }
            if (r.player1.PlayerIndex != 0)
            {
                ShowThinkingMessage();
            }
            //if (r.number == 1)
            //{
            //    Task.WaitAll(new[] { _bubble1Task, _bubble2Task, _bubble3Task }.Where(i => i != null).ToArray());
            //    ShowBubble(g.GameStartingPlayerIndex, string.Format("{0} {1}", g.GameType, g.trump.Description()), false);
            //}
        }

        private void RoundFinished(object sender, Round r)
        {
            g.ThrowIfCancellationRequested();
            _synchronizationContext.Send(_ =>
            {
                if (r.number < 10)
                {
                    ShowMsgLabel("Klikni na libovolnou kartu", false);
                }
                _state = GameState.RoundFinished;
                CanSaveGame = true;
                CanRewind = true;
            }, null);
            WaitForUIThread();
        }

        #endregion

        #region MainWindow event handlers

        private void OnNewGame(object sender, ExecutedRoutedEventArgs e)
        {
            if (_starterTask != null && _starterTask.Status == TaskStatus.Running)
            {
                _log.Debug("OnNewGame: Starter task already running");
                return;
            }
            _starterTask = Task.Run(() =>
            {
                CancelRunningTask();
                _currentStartingPlayerIndex = AppSettings.GetBool("RotateStartingPlayer") 
                    ? (_currentStartingPlayerIndex + 1) % Game.NumPlayers 
                    : AppSettings.GetInt("StartingPlayerIndex");
                g = new Game() { SkipBidding = AppSettings.GetBool("SkipBidding", false) };
                //g.RegisterPlayers(new HumanPlayer(g, this) { Name = "Human" },
                //                  new AiPlayer(g) { Name = "Hrac2" },
                //                  new AiPlayer(g) { Name = "Hrac3" });
                _synchronizationContext.Send(_ =>
                {
                    g.RegisterPlayers(_playerSettingsReader);
                    g.NewGame(_currentStartingPlayerIndex, AppSettings.GetBool("ShuffleCards", true) ? null : _deck);
                    g.GameTypeChosen += GameTypeChosen;
                    g.BidMade += BidMade;
                    g.CardPlayed += CardPlayed;
                    g.RoundStarted += RoundStarted;
                    g.RoundFinished += RoundFinished;
                    g.GameFinished += GameFinished;
                    CanEdit = true;
                    //UpdateHands();
                    CanEdit = true;
                    CanSaveGame = true;
                    var logVisible = loggerWindow != null && loggerWindow.IsVisible;
                    if (logVisible)
                    {
                        loggerWindow.Close();
                    }
                    loggerWindow = new LoggerWindow();
                    if (logVisible)
                    {
                        loggerWindow.Show();
                    }
                    //var binding = new Binding {Source = NotifyAppender.Instance.Notification};
                    //var binding = new Binding("Notification");
                    //loggerWindow.LogWindow.textBox.DataContext = NotifyAppender.Instance;
                    //loggerWindow.LogWindow.textBox.SetBinding(TextBlock.TextProperty, binding);   //re-bind the data context
                }, null);
                _gameTask = Task.Run(() => g.PlayGame(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            });
        }

        private void OnLoadGame(object sender, ExecutedRoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();

            dlg.DefaultExt = ".hra";
            dlg.Filter = "Mariasek files (*.hra)|*.hra";

            Nullable<bool> res = dlg.ShowDialog();
            if (res == true)
            {
                var filename = dlg.FileName;

                if (_starterTask != null && _starterTask.Status == TaskStatus.Running)
                {
                    _log.Debug("OnLoadGame: Starter task already running");
                    return;
                }
                _starterTask = Task.Run(() =>
                {
                    CancelRunningTask();
                    g = new Game() {SkipBidding = AppSettings.GetBool("SkipBidding", false)};
                    //g.RegisterPlayers(new HumanPlayer(g, this) { Name = "Human" },
                    //                  new AiPlayer(g) { Name = "Hrac2" },
                    //                  new AiPlayer(g) { Name = "Hrac3" });
                    _synchronizationContext.Send(_ =>
                    {
                        g.RegisterPlayers(_playerSettingsReader);
                        g.LoadGame(filename);
                        g.GameTypeChosen += GameTypeChosen;
                        g.BidMade += BidMade;
                        g.CardPlayed += CardPlayed;
                        g.RoundStarted += RoundStarted;
                        g.RoundFinished += RoundFinished;
                        g.GameFinished += GameFinished;
                        lblTrump.Content = string.Format("{0} {1}", g.GameType, g.trump.HasValue ? g.trump.Value.Description() : "");
                        if (g.RoundNumber > 0)
                        {
                            UpdateHands();
                        }
                        CanEdit = true;
                        CanSaveGame = true;
                        if (loggerWindow != null)
                        {
                            loggerWindow.Close();
                        }
                        loggerWindow = new LoggerWindow();
                    }, null);
                    _gameTask = Task.Run(() => g.PlayGame(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                });
            }
        }

        private void OnCanSaveGame(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanSaveGame;
        }

        private void OnSaveGame(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();

            dlg.DefaultExt = ".hra";
            dlg.Filter = "Mariasek files (*.hra)|*.hra";

            Nullable<bool> res = dlg.ShowDialog();
            if (res == true)
            {
                var gameDto = e.Parameter as GameDto;

                if (gameDto != null)
                {
                    gameDto.SaveGame(dlg.FileName);
                }
                else
                {
                    g.SaveGame(dlg.FileName);
                }
            }
        }

        private void OnEndGame(object sender, ExecutedRoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void OnSettings(object sender, ExecutedRoutedEventArgs e)
        {
            settingsWindow.ShowDialog();

            _currentStartingPlayerIndex = settingsWindow.StartingPlayerComboBox.SelectedIndex;
            AppSettings.Open();
            AppSettings.Set("RotateStartingPlayer", settingsWindow.RotateStartingPlayer.IsChecked);
            AppSettings.SetInt("StartingPlayerIndex", settingsWindow.StartingPlayerComboBox.SelectedIndex);
            AppSettings.Set("ShuffleCards", settingsWindow.ShuffleCards.IsChecked);
            AppSettings.Save();

            var config = ConfigurationManager.OpenExeConfiguration(Assembly.GetEntryAssembly().Location);
            var playersSection = config.GetSection("players") as Mariasek.WinSettings.PlayersConfigurationSection;
            var playersSettings = new[] { playersSection.Player1, playersSection.Player2, playersSection.Player3 };
            var aiSettings = playersSettings.Where(i => i.Type.EndsWith(".AiPlayer")).ToList();

            foreach (var aiSetting in aiSettings)
            {
                aiSetting.Parameters["AiCheating"] = new Mariasek.WinSettings.ParameterConfigurationElement
                {
                    Name = "AiCheating",
                    Value = settingsWindow.AiCheating.IsChecked.Value.ToString()
                };
                aiSetting.Parameters["CardSelectionStrategy"] = new Mariasek.WinSettings.ParameterConfigurationElement
                {
                    Name = "CardSelectionStrategy",
                    Value = ((CardSelectionStrategy)settingsWindow.CardSelectionComboBox.SelectedIndex).ToString()
                };
                aiSetting.Parameters["SimulationsPerRound"] = new Mariasek.WinSettings.ParameterConfigurationElement
                {
                    Name = "SimulationsPerRound",
                    Value = settingsWindow.SimulationsPerRoundTextBox.Text
                };
                aiSetting.Parameters["RuleThreshold"] = new Mariasek.WinSettings.ParameterConfigurationElement
                {
                    Name = "RuleThreshold",
                    Value = settingsWindow.RuleThreshold.Text
                };
                aiSetting.Parameters["GameThreshold"] = new Mariasek.WinSettings.ParameterConfigurationElement
                {
                    Name = "GameThreshold",
                    Value = settingsWindow.SimulationsPerRoundTextBox.Text
                };
                aiSetting.Parameters["RoundsToCompute"] = new Mariasek.WinSettings.ParameterConfigurationElement
                {
                    Name = "RoundsToCompute",
                    Value = settingsWindow.RoundsToComputeTextBox.Text
                };
            }
            config.Save();
            AppSettings.Open();
            AppSettings.Set("SkipBidding", settingsWindow.SkipBidding.IsChecked.Value);
            AppSettings.Save();
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            HideMsgLabel();
            okButton.Visibility = Visibility.Hidden;
            _state = GameState.NotPlaying;
            _evt.Set();
        }

        private void Card_Click(object sender, RoutedEventArgs e)
        {
            var img = DependencyObjectExtensions.findElementOfType<Image>(sender as DependencyObject);

            _cardClicked = (Card)img.Tag;
            switch (_state)
            {
                case GameState.ChooseTalon:
                    //ignore clicks on card if two cards have been selected
                    if (_talon.Count == 2 && img.Opacity == 1.0)
                        return;
                    if (ToggleCardOpacity(img))
                    {
                        //selected
                        _talon.Add(_cardClicked);
                    }
                    else
                    {
                        //unselected
                        _talon.Remove(_cardClicked);
                    }
                    okButton.IsEnabled = _talon.Count == 2;
                    break;
                case GameState.ChooseTrump:
                    if (_cardClicked == null)
                        return;
                    _state = GameState.NotPlaying;
                    HideMsgLabel();
                    _evt.Set();
                    break;
                case GameState.Play:
                    if (_cardClicked == null)
                        return;
                    _state = GameState.NotPlaying;
                    HideMsgLabel();
                    _evt.Set();
                    break;
                case GameState.RoundFinished:
                    _state = GameState.NotPlaying;
                    ClearTable();
                    HideMsgLabel();
                    UpdateProbabilityWindow();
                    _evt.Set();
                    break;
                case GameState.GameFinished:
                    _state = GameState.NotPlaying;
                    ClearTable(true);
                    HideMsgLabel();
                    OnNewGame(this, null);
                    return;
                default:
                    return;
            }
        }

        private void gtButton_Click(object sender, RoutedEventArgs e)
        {
            _gameTypeChosen = (Hra)(sender as Button).Tag;
            HideMsgLabel();
            foreach (var gtButton in gtButtons)
            {
                gtButton.Visibility = Visibility.Hidden;
            }
            okButton.Visibility = Visibility.Hidden;
            _state = GameState.NotPlaying;
            _evt.Set();
        }

        private void OnCanRewind(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanRewind;
        }

        private void OnRewind(object sender, ExecutedRoutedEventArgs e)
        {
            if (_starterTask != null && _starterTask.Status == TaskStatus.Running)
            {
                _log.Debug("OnRewind: Starter task already running");
                return;
            }
            _starterTask = Task.Run(() =>
            {
                CancelRunningTask();
                _synchronizationContext.Send(_ =>
                {
                    g.Rewind();
                    CanEdit = true;
                    //UpdateHands();
                    CanEdit = true;
                    CanSaveGame = true;
                    var logVisible = loggerWindow != null && loggerWindow.IsVisible;
                    if (logVisible)
                    {
                        loggerWindow.Close();
                    }
                    loggerWindow = new LoggerWindow();
                    if (logVisible)
                    {
                        loggerWindow.Show();
                    }
                }, null);
                _gameTask = Task.Run(() => g.PlayGame(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            });
        }

        private void OnCanShowHands(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void OnShowHands(object sender, ExecutedRoutedEventArgs e)
        {
            if (e != null)
            {
                //only change the value if not called from within UpdateHands()
                ShowAllCards = !ShowAllCards;
                AppSettings.Open();
                AppSettings.Set("Cheat", ShowAllCards);
                AppSettings.Save();
            }
            if (ShowAllCards)
            {
                for (var i = 0; i < 2; i++)
                {
                    if (!hw[i].IsVisible)
                    {
                        if (i == 0)
                        {
                            hw[i].Left = (Left + Width/2) - hw[i].Width;
                        }
                        else
                        {
                            hw[i].Left = (Left + Width/2);
                        }
                        hw[i].Top = Top - hw[i].Height;
                    }
                    hw[i].Show();
                }
                Activate();
            }
            else
            {
                hw[0].Hide();
                hw[1].Hide();
            }
            if (e == null)  //null indicates a call from within UpdateHands() method: bail out to prevent infinite recursion
            {
                return;
            }
            UpdateHands();
        }

        private void OnCanEdit(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanEdit;
        }

        private void OnEdit(object sender, ExecutedRoutedEventArgs e)
        {
            var editorWindow = new EditorWindow(g);
            var dialogResult = editorWindow.ShowDialog();
            if (dialogResult.HasValue && dialogResult.Value)
            {
                var gameDto = new GameDto
                {
                    Kolo = editorWindow.RoundNumber,
                    Trumf = editorWindow.TrumpComboBox.SelectedIndex >= 0 ? (Barva?)editorWindow.TrumpComboBox.SelectedIndex : null,
                    Voli = (Hrac)editorWindow.GameStarterComboBox.SelectedIndex,
                    Zacina = (Hrac)editorWindow.RoundStarterComboBox.SelectedIndex,
                    Hrac1 = editorWindow.Hands[0]
                             .Select(i => new Karta
                             {
                                 Barva = i.Suit,
                                 Hodnota = i.Value
                             }).ToArray(),
                    Hrac2 = editorWindow.Hands[1]
                             .Select(i => new Karta
                             {
                                 Barva = i.Suit,
                                 Hodnota = i.Value
                             }).ToArray(),
                    Hrac3 = editorWindow.Hands[2]
                             .Select(i => new Karta
                             {
                                 Barva = i.Suit,
                                 Hodnota = i.Value
                             }).ToArray(),
                    Talon = editorWindow.Hands[3]
                             .Select(i => new Karta
                             {
                                 Barva = i.Suit,
                                 Hodnota = i.Value
                             }).ToArray(),
                    Stychy = new Stych[0]
                };
                Commands.SaveGame.Execute(gameDto, null);
            }
        }

        private void OnCanLog(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = loggerWindow != null;
        }

        private void OnLog(object sender, ExecutedRoutedEventArgs e)
        {
            if (loggerWindow.IsVisible)
            {
                loggerWindow.Hide();
            }
            else
            {
                loggerWindow.Show();
            }
            logMenuItem.IsChecked = loggerWindow.IsVisible;
        }

        private void OnCanShowProbabilities(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanShowProbabilities;
        }

        private void OnShowProbabilities(object sender, ExecutedRoutedEventArgs e)
        {
            if (pw.IsVisible)
            {
                pw.Hide();
            }
            else
            {
                pw.Show();
            }
            probabilitiesMenuItem.IsChecked = pw.IsVisible;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //Originally the main window was supposed to appear at the centre of the screen
            //Let's move it down
            Top = (Top + Height) * AppSettings.GetDouble("ShiftFactor", 0.33);
        }

        private void MainGrid_SizeChanged(object sender, EventArgs e)
        {
            CalculateScale();
        }

        private void CalculateScale()
        {
            double yScale = ActualHeight / 600.0d;
            double xScale = ActualWidth / 800.0d;
            double value = Math.Min(xScale, yScale);
            ScaleValue = (double)OnCoerceScaleValue(MainGrid, value);
        }

        #endregion

        #region ScaleValue Dependency Property

        public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue", typeof(double), typeof(MainWindow), new UIPropertyMetadata(1.0, new PropertyChangedCallback(OnScaleValueChanged), new CoerceValueCallback(OnCoerceScaleValue)));

        private static object OnCoerceScaleValue(DependencyObject o, object value)
        {
            MainWindow mainWindow = o as MainWindow;
            if (mainWindow != null)
                return mainWindow.OnCoerceScaleValue((double)value);
            else
                return value;
        }

        private static void OnScaleValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            MainWindow mainWindow = o as MainWindow;
            if (mainWindow != null)
                mainWindow.OnScaleValueChanged((double)e.OldValue, (double)e.NewValue);
        }

        protected virtual double OnCoerceScaleValue(double value)
        {
            if (double.IsNaN(value))
                return 1.0d;

            value = Math.Max(0.1, value);
            return value;
        }

        protected virtual void OnScaleValueChanged(double oldValue, double newValue)
        {

        }

        public double ScaleValue
        {
            get
            {
                return (double)GetValue(ScaleValueProperty);
            }
            set
            {
                SetValue(ScaleValueProperty, value);
            }
        }

        #endregion
    }
}
