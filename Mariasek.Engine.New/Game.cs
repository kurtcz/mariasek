﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
//using log4net;
using Mariasek.Engine.New.Logger;
using Mariasek.Engine.New.Configuration;
#if !PORTABLE
using Mariasek.Engine.New.Schema;
#endif

namespace Mariasek.Engine.New
{
    public class Game //: MarshalByRefObject
    {
#if !PORTABLE
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
#else
        private static readonly ILog _log = new DummyLogWrapper();
#endif
        private static readonly Random rand = new Random();
        private CancellationToken _cancellationToken;
        private AbstractPlayer _roundStartingPlayer;
        
        #region Public fields and properties

        public const int NumPlayers = 3;
        public const int NumRounds = 10;
        public const int NumSuits = 4;

        public AbstractPlayer[] players { get; private set; }

        public AbstractPlayer GameStartingPlayer { get { return players[GameStartingPlayerIndex]; } }
        public int GameStartingPlayerIndex { get; private set; }
        public int OriginalGameStartingPlayerIndex { get; private set; }

        public MoneyCalculatorBase Results { get; private set; }

        public bool SkipBidding { get; set; }
        public float BaseBet { get; set; }
        public CalculationStyle CalculationStyle { get; set; }
        public bool IsRunning { get; private set; }
        public Hra GameType { get; private set; }
        public Barva? trump { get; private set; }
        public Card TrumpCard { get; private set; }
        public List<Card> talon { get; private set; }
        public Round[] rounds { get; private set; }
        public Round CurrentRound { get { return RoundNumber > 0  && RoundNumber <= 10 ? rounds[RoundNumber - 1] : null; } }
        public int RoundNumber { get; private set; }
        public Bidding Bidding { get; private set; }
        public string Author { get; set; }
#if !PORTABLE
        public static Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
#endif
        public string Comment { get; set; }

        #endregion

        #region Events and delegates

        public delegate void GameLoadedEventHandler(object sender);
        public event GameLoadedEventHandler GameLoaded;
        protected virtual void OnGameLoaded()
        {
            if (GameLoaded != null)
            {
                GameLoaded(this);
            }
        }

        public delegate void GameFlavourChosenEventHandler(object sender, GameFlavourChosenEventArgs e);
        public event GameFlavourChosenEventHandler GameFlavourChosen;
        protected virtual void OnGameFlavourChosen(GameFlavourChosenEventArgs e)
        {
            if (GameFlavourChosen != null)
            {
                GameFlavourChosen(this, e);
            }
        }

        public delegate void GameTypeChosenEventHandler(object sender, GameTypeChosenEventArgs e);
        public event GameTypeChosenEventHandler GameTypeChosen;
        protected virtual void OnGameTypeChosen(GameTypeChosenEventArgs e)
        {
            if (GameTypeChosen != null)
            {
                GameTypeChosen(this, e);
            }
        }

        public delegate void GameExceptionEventHandler(object sender, GameExceptionEventArgs e);
        public event GameExceptionEventHandler GameException;
        protected virtual void OnGameException(GameExceptionEventArgs e)
        {
            if (GameException != null)
            {
                GameException(this, e);
            }
        }

        public delegate void BidMadeEventHandler(object sender, BidEventArgs e);
        public event BidMadeEventHandler BidMade;
        public virtual void OnBidMade(BidEventArgs e)
        {
            if (BidMade != null)
            {
                BidMade(this, e);
            }
        }

        public delegate void CardPlayedEventHandler(object sender, Round r);
        public event CardPlayedEventHandler CardPlayed;
        public virtual void OnCardPlayed(Round r)
        {
            if (CardPlayed != null)
            {
                CardPlayed(this, r);
            }
        }

        public delegate void RoundEventHandler(object sender, Round r);
        public event RoundEventHandler RoundStarted; 
        public event RoundEventHandler RoundFinished;
        protected virtual void OnRoundStarted(Round r)
        {
            if (RoundStarted != null)
            {
                RoundStarted(this, r);
            }
        }
        protected virtual void OnRoundFinished(Round r)
        {
            if (RoundFinished != null)
            {
                RoundFinished(this, r);
            }
        }

        public delegate void GameFinishedEventHandler(object sender, MoneyCalculatorBase results);
        public event GameFinishedEventHandler GameFinished;
        protected virtual void OnGameFinished(MoneyCalculatorBase results)
        {
            if (GameFinished != null)
            {
                GameFinished(this, results);
            }
        }

        public delegate void GameWonPrematurelyEventHandler(object sender, GameWonPrematurelyEventArgs e);
        public event GameWonPrematurelyEventHandler GameWonPrematurely;
        protected virtual void OnGameWonPrematurely(object sender, GameWonPrematurelyEventArgs e)
        {
            if (GameWonPrematurely != null)
            {
                GameWonPrematurely(this, e);
            }
        }
        #endregion

        #region Public methods

        public Game()
        {
            BaseBet = 1f;
        }

        public void RegisterPlayers(AbstractPlayer player1, AbstractPlayer player2, AbstractPlayer player3)
        {
            players = new[] {player1, player2, player3};
        }

#if !PORTABLE
        public void RegisterPlayers(IPlayerSettingsReader settingsReader)
        //public void RegisterPlayers()
        {
            //var playersSettings = ConfigurationManager.GetSection("players") as PlayersConfigurationSection;
            var playersSettings = settingsReader.ReadSettings();

            var player1 = CreatePlayerInstance(playersSettings.Player1);
            var player2 = CreatePlayerInstance(playersSettings.Player2);
            var player3 = CreatePlayerInstance(playersSettings.Player3);
            
            players = new[] { player1, player2, player3 };
        }

        public AbstractPlayer CreatePlayerInstance(PlayerConfigurationElement playerConfiguration)
        {
            var assembly = Assembly.LoadFrom(playerConfiguration.Assembly);
            var type = assembly.GetType(playerConfiguration.Type);
            AbstractPlayer player;

            if (playerConfiguration.Parameters.Count == 0)
            {
                //no settings provided
                var ctorArgs = new object[] { this };
                var ctorArgTypes = new[] { typeof(Game) };
                var ctor = type.GetConstructor(ctorArgTypes);
                player = (AbstractPlayer)ctor.Invoke(ctorArgs);
            }
            else
            {
                //settings have been provided
                var ctorArgs = new object[] { this, playerConfiguration.Parameters };
                var ctorArgTypes = new[] { typeof(Game), typeof(ParameterConfigurationElementCollection) };
                var ctor = type.GetConstructor(ctorArgTypes);
                player = (AbstractPlayer)ctor.Invoke(ctorArgs);
            }

            player.Name = playerConfiguration.Name;

            return player;
        }
#endif      
        /// <summary>
        /// This method needs to be called after the players get their cards but before the 1st round is played. Players get a chance to initialize their ai models here.
        /// </summary>
        private void InitPlayers()
        {
            foreach (var player in players)
            {
                player.Init();
            }
        }

        public void ThrowIfCancellationRequested()
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                _log.Debug("Task cancellation requested");
                _cancellationToken.ThrowIfCancellationRequested();
            }
        }

        public void NewGame(int gameStartingPlayerIndex, Deck deck = null)
        {
            IsRunning = true;
            if (deck == null || deck.IsEmpty())
            {
                deck = new Deck();
                deck.Shuffle();
            }
            
            GameStartingPlayerIndex = gameStartingPlayerIndex;
            OriginalGameStartingPlayerIndex = GameStartingPlayerIndex;

            //var logName = string.Format(@"Logs\LogFile.{0}.txt", DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
            //LoggerSetup.TraceFileSetup(logName, LoggerSetup.FileMode.CreateNewOrTruncate);

            _log.Init();
            _log.Info("********");

            RoundNumber = 0;
            rounds = new Round[NumRounds];
#if !PORTABLE
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
#if DEBUG
            const string buildConfiguration = "DEBUG";
#else
            const string buildConfiguration = "RELEASE";
#endif
            _log.InfoFormat("Assembly version: {0} ({1})", version, buildConfiguration);
#endif
            _log.InfoFormat("**Starting game**\n");

            Comment = null;
            for (int i = 0; i < 12; i++)
            {
                GameStartingPlayer.Hand.Add(deck.TakeOne());
            }
            for (int i = 0; i < 10; i++)
            {
                players[(GameStartingPlayer.PlayerIndex + 1) % NumPlayers].Hand.Add(deck.TakeOne());
            }
            for (int i = 0; i < 10; i++)
            {
                players[(GameStartingPlayer.PlayerIndex + 2) % NumPlayers].Hand.Add(deck.TakeOne());
            }
            //players[GameStartingPlayerIndex].Hand.Sort();   //voliciho hrace utridime pokud uz zvolil trumf
            //players[(GameStartingPlayerIndex + 1) % NumPlayers].Hand.Sort();
            //players[(GameStartingPlayerIndex + 2) % NumPlayers].Hand.Sort();

            talon = new List<Card>();
            InitPlayers();
#if !PORTABLE
            var programFolder = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            SaveGame(System.IO.Path.Combine(programFolder, "_temp.hra"));
#endif        
        }

#if !PORTABLE
        public void LoadGame(string filename)
        {
            _log.Init();
            _log.Info("********");

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
#if DEBUG
            const string buildConfiguration = "DEBUG";
#else
            const string buildConfiguration = "RELEASE";
#endif
            IsRunning = true;
            _log.InfoFormat("Assembly version: {0} ({1})", version, buildConfiguration);
            _log.InfoFormat("**Loading game {0}**\n", Path.GetFileName(filename));

            using (var fileStream = new FileStream(filename, FileMode.Open))
            {
                var serializer = new XmlSerializer(typeof(GameDto));
                var gameData = (GameDto)serializer.Deserialize(fileStream);

                RoundNumber = 0;
                trump = gameData.Trumf;
                if (gameData.Typ.HasValue)
                {
                    GameType = gameData.Typ.Value;
                }
                GameStartingPlayerIndex = (int) gameData.Voli;
                OriginalGameStartingPlayerIndex = GameStartingPlayerIndex;
                _roundStartingPlayer = players[(int)gameData.Zacina];

                Author = gameData.Autor;
                Comment = gameData.Komentar;
                players[0].Hand.AddRange(gameData.Hrac1.Select(i => new Card(i.Barva, i.Hodnota)));
                players[1].Hand.AddRange(gameData.Hrac2.Select(i => new Card(i.Barva, i.Hodnota)));
                players[2].Hand.AddRange(gameData.Hrac3.Select(i => new Card(i.Barva, i.Hodnota)));

                if (gameData.Talon == null)
                {
                    gameData.Talon = new Karta[0];
                }
                talon = new List<Card>(gameData.Talon.Select(i => new Card(i.Barva, i.Hodnota)));

                if (gameData.Stychy == null)
                {
                    gameData.Stychy = new Stych[0];
                }

                players[0].Hand.AddRange(gameData.Stychy.Where(i => i.Hrac1 != null).Select(i => new Card(i.Hrac1.Barva, i.Hrac1.Hodnota)));
                players[1].Hand.AddRange(gameData.Stychy.Where(i => i.Hrac2 != null).Select(i => new Card(i.Hrac2.Barva, i.Hrac2.Hodnota)));
                players[2].Hand.AddRange(gameData.Stychy.Where(i => i.Hrac3 != null).Select(i => new Card(i.Hrac3.Barva, i.Hrac3.Hodnota)));

                InitPlayers();
                OnGameLoaded();

                rounds = new Round[NumRounds];
                foreach (var stych in gameData.Stychy.Where(i => i.Hrac1 != null && i.Hrac2 != null && i.Hrac3 != null).OrderBy(i => i.Kolo))
                {
                    RoundNumber++;
                    var cards = new[] {stych.Hrac1, stych.Hrac2, stych.Hrac3};
                    
                    var player1 = (int) stych.Zacina;
                    var player2 = ((int) stych.Zacina + 1) % NumPlayers;
                    var player3 = ((int) stych.Zacina + 2) % NumPlayers;
                    
                    var c1 = new Card(cards[player1].Barva, cards[player1].Hodnota);
                    var c2 = new Card(cards[player2].Barva, cards[player2].Hodnota);
                    var c3 = new Card(cards[player3].Barva, cards[player3].Hodnota);
                    var r = new Round(this, players[player1], c1, c2, c3);  //inside this constructor we replay the round and call all event handlers to ensure that all players can update their ai model

                    rounds[stych.Kolo - 1] = r;

                    players[player1].Hand.Remove(c1);
                    players[player2].Hand.Remove(c2);
                    players[player3].Hand.Remove(c3);

                    OnRoundFinished(r);
                }
                RoundNumber = gameData.Kolo;
                if(RoundNumber > 0)
                {
                    players[GameStartingPlayerIndex].Hand.Sort();   //voliciho hrace utridime pokud uz zvolil trumf
                }
                players[(GameStartingPlayerIndex + 1) % NumPlayers].Hand.Sort();
                players[(GameStartingPlayerIndex + 2) % NumPlayers].Hand.Sort();
                if(RoundNumber == 0)
                {
                    Rewind();
                }
            }
        }

        public void SaveGame(string filename, bool saveDebugInfo = false)
        {
            var startingPlayerIndex = GameStartingPlayerIndex;
            if(CurrentRound != null)
            {
                if (CurrentRound.roundWinner == null)
                {
                    //save after an exception (unfinished round)
                    startingPlayerIndex = CurrentRound.player1.PlayerIndex;
                    Comment = string.Format("Exception has been thrown at round {0}", CurrentRound.number);
                    RoundNumber--;
                }
                else
                {
                    startingPlayerIndex = CurrentRound.roundWinner.PlayerIndex;
                }
            }
            var gameDto = new GameDto
            {
                Kolo = CurrentRound != null ? RoundNumber + 1 : 0,
                Voli = (Hrac) GameStartingPlayerIndex,
                Trumf = RoundNumber > 0 ? trump : null,
                Typ = RoundNumber > 0 ? (Hra?) GameType : null,
                Zacina = (Hrac) startingPlayerIndex,
                Autor = Author,
                Verze = Version.ToString(),
                Komentar = Comment,
                Hrac1 = players[0].Hand
                    .Select(i => new Karta
                    {
                        Barva = i.Suit,
                        Hodnota = i.Value
                    }).ToArray(),
                Hrac2 = players[1].Hand
                    .Select(i => new Karta
                    {
                        Barva = i.Suit,
                        Hodnota = i.Value
                    }).ToArray(),
                Hrac3 = players[2].Hand
                    .Select(i => new Karta
                    {
                        Barva = i.Suit,
                        Hodnota = i.Value
                    }).ToArray(),
                Stychy = rounds.Where(r => r != null)
                    .Select(r => new Stych
                    {
                        Kolo = r.number,
                        Zacina = (Hrac) r.player1.PlayerIndex
                    }).ToArray(),
                Talon = talon
                    .Select(i => new Karta
                    {
                        Barva = i.Suit,
                        Hodnota = i.Value
                    }).ToArray()
            };
            try
            {
                foreach (var stych in gameDto.Stychy)
                {
                    var r = rounds[stych.Kolo - 1];
                    var cards = new[] { r.c1, r.c2, r.c3 };
                    var debugInfo = new[] { r.debugNote1, r.debugNote2, r.debugNote3 };
                    var playerIndices = new[] { r.player1.PlayerIndex, r.player2.PlayerIndex, r.player3.PlayerIndex };
                    int index = Array.IndexOf(playerIndices, 0);
                    stych.Hrac1 = new Karta
                    {
                        Barva = cards[index].Suit,
                        Hodnota = cards[index].Value,
                        Poznamka = debugInfo[index]
                    };
                    index = Array.IndexOf(playerIndices, 1);
                    stych.Hrac2 = new Karta
                    {
                        Barva = cards[index].Suit,
                        Hodnota = cards[index].Value,
                        Poznamka = debugInfo[index]
                    };
                    index = Array.IndexOf(playerIndices, 2);
                    stych.Hrac3 = new Karta
                    {
                        Barva = cards[index].Suit,
                        Hodnota = cards[index].Value,
                        Poznamka = debugInfo[index]
                    };
                }
            }
            catch (Exception)
            {
            }
            if (Results != null)
            {
                gameDto.Zuctovani = new Zuctovani
                {
                    Hrac1 = new Skore
                    {
                        Body = GameStartingPlayerIndex == 0 ? Results.PointsWon : Results.PointsLost,
                        Zisk = Results.MoneyWon[0]
                    },
                    Hrac2 = new Skore
                    {
                        Body = GameStartingPlayerIndex == 1 ? Results.PointsWon : Results.PointsLost,
                        Zisk = Results.MoneyWon[1]
                    },
                    Hrac3 = new Skore
                    {
                        Body = GameStartingPlayerIndex == 2 ? Results.PointsWon : Results.PointsLost,
                        Zisk = Results.MoneyWon[2]
                    }
                };
            }

            gameDto.SaveGame(filename, saveDebugInfo);
        }
#endif

        public void PlayGame(CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                _cancellationToken = cancellationToken;
                //zahajeni hry
                if (RoundNumber == 0)
                {
                    //InitPlayers();
                    GameType = Hra.Hra; //docasne nastavena nejaka minimalni hra
                    ChooseGame();
                    //Bidding = new Bidding(this);
                    //if (!SkipBidding)
                    //{
                    //    GameType = Bidding.StartBidding();
                    //}
                    RoundNumber++;
                }
                else
                {
                    Bidding = new Bidding(this); 
                }

                if(ShouldPlayGame())
                {
                    //vlastni hra
                    var roundWinner = _roundStartingPlayer;

                    if(PlayerWinsGame(GameStartingPlayer))
                    {
                        OnGameWonPrematurely(this, new GameWonPrematurelyEventArgs { winner = roundWinner, winningHand = roundWinner.Hand });
                        CompleteUnfinishedRounds();
                    }
                    else
                    {
                        for (; RoundNumber <= NumRounds; RoundNumber++)
                        {
                            var r = new Round(this, roundWinner);
                            OnRoundStarted(r);

                            rounds[RoundNumber - 1] = r;
                            roundWinner = r.PlayRound();

                            OnRoundFinished(r);
                            if(CanStopPlaying(r))
                            {
                                //predcasne vitezstvi ukazuju jen do sedmeho kola, pro posledni 2 karty to nema smysl
                                if(RoundNumber < 8 && PlayerWinsGame(roundWinner))
                                {
                                    if(GameType == Hra.Betl)
                                    {
                                        roundWinner = GameStartingPlayer;
                                    }
                                    OnGameWonPrematurely(this, new GameWonPrematurelyEventArgs { winner = roundWinner, winningHand = roundWinner.Hand, roundNumber = RoundNumber });
                                    CompleteUnfinishedRounds();
                                    break;
                                }
                            }
                        }
                    }
                }

                //zakonceni hry
                IsRunning = false;
                Results = GetMoneyCalculator();
                Results.CalculateMoney();
                OnGameFinished(Results);
            }
            catch (OperationCanceledException)
            {
                _log.Debug("OperationCanceledException caught");
            }
            catch (Exception ex)
            {
                _log.Error("Exception in PlayGame()", ex);
                OnGameException(new GameExceptionEventArgs { e = ex });
#if !PORTABLE
                SaveGame(string.Format("_error_{0}.hra", DateTime.Now.ToString("yyyyMMddHHmmss")));
#endif
                throw;
            }
            finally
            {
                IsRunning = false;
            }
        }

        public void Rewind()
        {
            for (var i = 0; i < Game.NumRounds; i++)
            {
                var r = rounds[i];
                rounds[i] = null;

                if (r == null || r.c1 == null) break;
                players[r.player1.PlayerIndex].Hand.Add(r.c1);
                if (r.c2 == null) break;
                players[r.player2.PlayerIndex].Hand.Add(r.c2);
                if (r.c3 == null) break;
                players[r.player3.PlayerIndex].Hand.Add(r.c3);
            }
            GameStartingPlayer.Hand.AddRange(talon);
            talon.Clear();
            RoundNumber = 0;
            InitPlayers();
        }

        public Deck GetDeckFromLastGame()
        {
            var deck = new List<Card>();
            if (rounds != null)
            {
                foreach (var r in rounds)
                {
                    if (r == null)
                    {
                        break;
                    }
                    if (!r.hlas1)
                    {
                        deck.Insert(0, r.c1);
                    }
                    if (!r.hlas2)
                    {
                        deck.Insert(0, r.c2);
                    }
                    if (!r.hlas3)
                    {
                        deck.Insert(0, r.c3);
                    }
                }
                foreach (var player in players)
                {
                    foreach (var r in rounds)
                    {
                        if (r == null)
                        {
                            break;
                        }
                        if (r.hlas1 && r.player1 == player)
                        {
                            deck.Insert(0, r.c1);
                        }
                        if (r.hlas2 && r.player2 == player)
                        {
                            deck.Insert(0, r.c2);
                        }
                        if (r.hlas3 && r.player3 == player)
                        {
                            deck.Insert(0, r.c3);
                        }
                    }
                    deck.InsertRange(0, player.Hand);
                }
                deck.InsertRange(0, talon);

                //sejmeme
                var n = rand.Next(deck.Count);
                var temp = deck.GetRange(0, n);
                deck.RemoveRange(0, n);
                deck.AddRange(temp);
            }
            return new Deck(deck);
        }

        public bool IsValidTalonCard(Card c)
        {
            return IsValidTalonCard(c.Value, c.Suit, trump);
        }

        public static bool IsValidTalonCard(Hodnota h, Barva b, Barva? trump)
        {
            if (!trump.HasValue)
            {
                return true;
            }

            return !(//(trump.HasValue && b == trump.Value) ||
                     h == Hodnota.Eso ||
                     h == Hodnota.Desitka);
        }

        #endregion

        #region Private methods

        private MoneyCalculatorBase GetMoneyCalculator()
        {
            switch (CalculationStyle)
            {
                case CalculationStyle.Adding:
                    return new AddingMoneyCalculator(this);
                case CalculationStyle.Multiplying:
                    return new MultiplyingMoneyCalculator(this);
                default:
                    throw new Exception(string.Format("Unsupported calculation style: {0}", CalculationStyle));
            }
        }

        private bool ShouldPlayGame()
        {
            if (SkipBidding)
            {
                return true; //pokud je vyple flekovani, tak hrajeme vzdycky
            }
            if (GameType == Hra.Hra && Bidding.GameMultiplier == 1)
            {
                return false; //neflekovana hra se nehraje
            }
            if (GameType == (Hra.Hra | Hra.Sedma) && Bidding.SevenMultiplier == 1 && Bidding.GameMultiplier == 2)
            {
                return false; //sedma a flek na hru se nehraje
            }
            return true;
        }

        private bool CanStopPlaying(Round r)
        {
            if ((GameType == Hra.Betl && r.roundWinner == GameStartingPlayer) ||
                (GameType == Hra.Durch && r.roundWinner != GameStartingPlayer))
            {
                return true;
            }

            if (PlayerWinsGame(r.roundWinner))
            {
                return true;
            }

            return false;
        }

        private bool PlayerWinsGame(AbstractPlayer player)
        {
            if(GameType == Hra.Betl)
            {
                var player2 = (GameStartingPlayerIndex + 1) % Game.NumPlayers;
                var player3 = (GameStartingPlayerIndex + 2) % Game.NumPlayers;

                player = GameStartingPlayer;

                return player.Hand.All(i => players[player2].Hand.All(j => players[player3].Hand.All(k => j.IsHigherThan(i, trump) && k.IsHigherThan(i, trump))));
            }
            else
            {
                var player2 = (player.PlayerIndex + 1) % Game.NumPlayers;
                var player3 = (player.PlayerIndex + 2) % Game.NumPlayers;

                return player.Hand.All(i => players[player2].Hand.All(j => players[player3].Hand.All(k => Round.WinningCard(i, j, k, trump) == i)));
            }
        }
        
        public Hra GetValidGameTypesForPlayer(AbstractPlayer player, GameFlavour gameFlavour, Hra minimalBid)
        {
            Hra validGameTypes;

            if(minimalBid == Hra.Hra)
            {
                validGameTypes = Hra.Hra | Hra.Kilo;
                if(player.Hand.Contains(new Card(trump.Value, Hodnota.Sedma)))
                {
                    validGameTypes |= Hra.Sedma;
                }
            }
            else //GameFlavour.Bad
            {
                validGameTypes = Hra.Durch;
                if(minimalBid == Hra.Betl)
                {
                    validGameTypes |= Hra.Betl;
                }
            }

            return validGameTypes;
        }

        private void ChooseGame()
        {
            GameFlavour gameFlavour;
            Hra validGameTypes = 0;
            var minimalBid = Hra.Hra;
            var gameTypeForPlayer = new Hra[] {0, 0, 0};
            var bidForPlayer = new Hra[] { 0, 0, 0 };
            var nextPlayer = GameStartingPlayer;
            var firstTime = true;
            var bidNumber = 0;
            var canChooseFlavour = true;

            gameTypeForPlayer[GameStartingPlayerIndex] = Hra.Hra;
            TrumpCard = GameStartingPlayer.ChooseTrump();
            trump = TrumpCard.Suit;
            //GameStartingPlayer.Hand.Sort();
            GameType = 0;
            talon = new List<Card>();
            while(GameType < Hra.Durch)
            {
                if(gameTypeForPlayer.All(i => i == 0) && bidForPlayer.All(i => i == 0))
                {
                    break;
                }
                if(!firstTime && nextPlayer == GameStartingPlayer)
                {
                    canChooseFlavour = false;
                }
                if (firstTime)
                {
                    talon = GameStartingPlayer.ChooseTalon();
                    GameStartingPlayer.Hand.RemoveAll(i => talon.Contains(i));
                    if (talon.Any(i => !IsValidTalonCard(i)))
                    {
                        canChooseFlavour = false;
                    }
                }
                if (canChooseFlavour)
                {
                    gameFlavour = nextPlayer.ChooseGameFlavour();
                    OnGameFlavourChosen(new GameFlavourChosenEventArgs
                    {
                        Player = nextPlayer,
                        Flavour = gameFlavour
                    });
                }
                else
                {
                    if (talon.Any(i => !IsValidTalonCard(i)))
                    {
                        gameFlavour = GameFlavour.Bad;
                    }
                    else
                    {
                        gameFlavour = GameFlavour.Good;
                    }
                }
                if(!firstTime && gameFlavour == GameFlavour.Good && GameType > Hra.Hra)
                {
                    //u betlu a durchu muzou hraci jeste navic flekovat
                    if(!SkipBidding)
                    {
                        Bidding.Round = (Bidding.Round + 1) % Game.NumPlayers;

                        //zapis novy flek
                        bidForPlayer[nextPlayer.PlayerIndex] = Bidding.GetBidsForPlayer(GameType, players[nextPlayer.PlayerIndex], bidNumber++);                        
                    }
                }
                else if(gameFlavour == GameFlavour.Bad)
                {
                    GameStartingPlayerIndex = nextPlayer.PlayerIndex;
                    trump = null;
                    TrumpCard = null;
                    if (!firstTime)
                    {
                        GameStartingPlayer.Hand.AddRange(talon);
                        talon = GameStartingPlayer.ChooseTalon();
                        GameStartingPlayer.Hand.RemoveAll(i => talon.Contains(i));
                    }
                    if(minimalBid == Hra.Hra)
                    {
                        minimalBid = Hra.Betl;
                    }
                    validGameTypes = GetValidGameTypesForPlayer(nextPlayer, gameFlavour, minimalBid);
                    GameType = GameStartingPlayer.ChooseGameType(validGameTypes); //TODO: zkontrolovat ze hrac nezvolil nelegalni variantu
                    minimalBid = Hra.Durch;
                    gameTypeForPlayer[GameStartingPlayerIndex] = GameType;
                    OnGameTypeChosen(new GameTypeChosenEventArgs
                    {
                        GameStartingPlayerIndex = GameStartingPlayerIndex,
                        GameType = GameType,
                        TrumpCard = null
                    });
                    if (!SkipBidding)
                    {
                        bidNumber = 0;
                        Bidding = new Bidding(this);
                        Bidding.StartBidding(GameType);
                    }
                }
                nextPlayer = players[(nextPlayer.PlayerIndex + 1) % Game.NumPlayers];
                gameTypeForPlayer[nextPlayer.PlayerIndex] = 0;
                if (players[(nextPlayer.PlayerIndex + 2) % Game.NumPlayers].PlayerIndex != nextPlayer.TeamMateIndex)
                {
                    bidForPlayer[nextPlayer.PlayerIndex] = 0;
                    if (nextPlayer.TeamMateIndex != -1)
                    {
                        bidForPlayer[nextPlayer.TeamMateIndex] = 0;
                    }
                }
                firstTime = false;
            }
            if (GameType == 0)
            {
                //hrac1 vybira normalni hru
                validGameTypes = GetValidGameTypesForPlayer(GameStartingPlayer, GameFlavour.Good, minimalBid);
                GameType = GameStartingPlayer.ChooseGameType(validGameTypes);
                OnGameTypeChosen(new GameTypeChosenEventArgs
                {
                    GameStartingPlayerIndex = GameStartingPlayerIndex,
                    GameType = GameType,
                    TrumpCard = TrumpCard
                });
                if(!SkipBidding)
                {
                    Bidding = new Bidding(this);
                    GameType = Bidding.CompleteBidding();
                }
            }
            _roundStartingPlayer = GameStartingPlayer;
        }

        private void CompleteUnfinishedRounds()
        {
            var lastRoundWinner = rounds[0] == null ? GameStartingPlayer : rounds[0].roundWinner;

            for (var i = 0; i < Game.NumRounds; i++)
            {                
                if (rounds[i] == null)
                {
                    var player1 = lastRoundWinner.PlayerIndex;
                    var player2 = (lastRoundWinner.PlayerIndex + 1) % Game.NumPlayers;
                    var player3 = (lastRoundWinner.PlayerIndex + 2) % Game.NumPlayers;

                    var c1 = AbstractPlayer.ValidCards(players[player1].Hand, trump, GameType, players[player1].TeamMateIndex).RandomOne();
                    var c2 = AbstractPlayer.ValidCards(players[player2].Hand, trump, GameType, players[player2].TeamMateIndex, c1).RandomOne();
                    var c3 = AbstractPlayer.ValidCards(players[player3].Hand, trump, GameType, players[player3].TeamMateIndex, c1, c2).RandomOne();

                    rounds[i] = new Round(this, lastRoundWinner, c1, c2, c3);
                }
                lastRoundWinner = rounds[i].roundWinner;
            }
        }
        #endregion
    }
}
