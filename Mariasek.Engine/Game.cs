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
using System.Xml;
using System.Xml.Serialization;
using Mariasek.Engine.Logger;
//#if !PORTABLE
using Mariasek.Engine.Schema;
using System.Runtime.CompilerServices;
using System.Globalization;
//#endif

namespace Mariasek.Engine
{
    public interface IGameTypeValues
    {
        int GameValue { get; set; }
        int SevenValue { get; set; }
        int QuietSevenValue { get; set; }
        int HundredValue { get; set; }
        int QuietHundredValue { get; set; }
        int BetlValue { get; set; }
        int DurchValue { get; set; }
    }

    public class Game : IGameTypeValues
    {
#if !PORTABLE
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
#else
        private static readonly ILog _log = new DummyLogWrapper();
#endif
        private static readonly Random rand = new Random();
        public CancellationToken CancellationToken;
        private AbstractPlayer _roundStartingPlayer;

        #region Public fields and properties

        public const int NumPlayers = 3;
        public const int NumRounds = 10;
        public const int NumSuits = 4;
        public const int TalonIndex = 3;

        private int CurrentGameNumber;
        private static int GameCounter;

        public Func<IStringLogger> GetStringLogger { get; set; }
        public IStringLogger BiddingDebugInfo { get; private set; }
        public AbstractPlayer[] players { get; private set; }

        public AbstractPlayer GameStartingPlayer { get { return players[GameStartingPlayerIndex]; } }
        public int GameStartingPlayerIndex { get; private set; }
        public int OriginalGameStartingPlayerIndex { get; private set; }

        public NumberFormatInfo CurrencyFormat { get; set; }
        public MoneyCalculatorBase Results { get; private set; }

        public int GameValue { get; set; }
        public int SevenValue { get; set; }
        public int QuietSevenValue { get; set; }
        public int HundredValue { get; set; }
        public int QuietHundredValue { get; set; }
        public int BetlValue { get; set; }
        public int DurchValue { get; set; }
        public SortMode SortMode { get; set; }
        public bool AllowFakeSeven { get; set; }
        public bool AllowFake107 { get; set; }
        public bool AllowAXTalon { get; set; }
        public bool AllowTrumpTalon { get; set; }
        public bool AllowAIAutoFinish { get; set; }
        public bool AllowPlayerAutoFinish { get; set; }
        public bool OptimisticAutoFinish { get; set; }
        public bool SkipBidding { get; set; }
        public float BaseBet { get; set; }
        public string Locale { get; set; }
        public int MaxWin { get; set; }
        public int MinimalBidsForGame { get; set; }
        public int MinimalBidsForSeven { get; set; }
        public bool PlayZeroSumGames { get; set; }
        public bool MandatoryDouble { get; set; }
        public bool Top107 { get; set; }
        public bool Calculate107Separately { get; set; }
        public int FirstMinMaxRound { get; set; }
        public HlasConsidered HlasConsidered { get; set; }
        public CalculationStyle CalculationStyle { get; set; }
        public bool CountHlasAgainst { get; set; }
        public bool IsRunning { get; private set; }
        public Hra GameType { get; private set; }
        public bool GivenUp { get; private set; }
        public float GameTypeConfidence { get; private set; }
        public Barva? trump { get; private set; }
        public Card TrumpCard { get; private set; }
        public List<Card> talon { get; private set; }
        public Round[] rounds { get; private set; }
        public Round CurrentRound { get { return rounds != null && RoundNumber > 0  && RoundNumber <= rounds.Length ? rounds[RoundNumber - 1] : null; } }
        public int RoundNumber { get; private set; }
        public int LastRoundNumber { get; private set; }
        public Bidding Bidding { get; private set; }
        public string Author { get; set; }
        public bool LogProbDebugInfo { get; set; }
        public bool SaveSimulations { get; set; }
        public int SimulatedGameId { get; set; }
        //public bool DoSort { get; set; }
        public bool AutoDisable100Against { get; set; }
#if !PORTABLE
        public static Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
#else
        public Version Version { get { return GetVersion != null ? GetVersion() : typeof(Game).GetTypeInfo().Assembly.GetName().Version; } }
        public Func<string, Stream> GetFileStream { get; set; }
        public Func<Version> GetVersion { get; set; }
#endif
        public string Comment { get; set; }
        public IStringLogger DebugString { get; private set; }

        public Action PreGameHook = () => { };

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
                try
                {
                    GameException(this, e);
                }
                catch
                {                    
                }
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

        public Game(Func<IStringLogger> stringLoggerFactory = null, int? gameStartingPlayerIndex = null)
        {
            CurrentGameNumber = ++GameCounter;
            BaseBet = 1f;
            Locale = "cs-CZ";
            MinimalBidsForGame = 1;
            MinimalBidsForSeven = 0;
            Top107 = false;
            GameTypeConfidence = -1f;
            AllowAXTalon = false;
            AllowTrumpTalon = true;
            AllowAIAutoFinish = true;
            AllowPlayerAutoFinish = true;
            Calculate107Separately = true;
            FirstMinMaxRound = 8;
            LogProbDebugInfo = false;
            HlasConsidered = HlasConsidered.Highest;
            GetStringLogger = stringLoggerFactory;
            if (GetStringLogger == null)
            {
                GetStringLogger = () => new StringLogger();
            }
            BiddingDebugInfo = GetStringLogger();
            DebugString = GetStringLogger();
            if (gameStartingPlayerIndex.HasValue)
            {
                GameStartingPlayerIndex = gameStartingPlayerIndex.Value;
            }
#if PORTABLE
            GetFileStream = _ => new MemoryStream(); //dummy stream factory
#endif
        }

        ~Game()
        {
            System.Diagnostics.Debug.WriteLine("<<< end of game {0}", CurrentGameNumber);
        }

        public void RegisterPlayers(AbstractPlayer[] players)
        {
            RegisterPlayers(players[0], players[1], players[2]);
        }

        public void RegisterPlayers(AbstractPlayer player1, AbstractPlayer player2, AbstractPlayer player3)
        {
            players = new[] {player1, player2, player3};
            player1.PlayerIndex = 0;
            player2.PlayerIndex = 1;
            player3.PlayerIndex = 2;
        }

#if !PORTABLE
        public void RegisterPlayers(IPlayerSettingsReader settingsReader)
        {
            var playersSettings = settingsReader.ReadSettings();

            var player1 = CreatePlayerInstance(playersSettings.Player1);
            var player2 = CreatePlayerInstance(playersSettings.Player2);
            var player3 = CreatePlayerInstance(playersSettings.Player3);

            player1.PlayerIndex = 0;
            player2.PlayerIndex = 1;
            player3.PlayerIndex = 2;

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
            if (CancellationToken.IsCancellationRequested)
            {
                _log.Debug("Task cancellation requested");
                CancellationToken.ThrowIfCancellationRequested();
            }
        }

        public void NewGame(int gameStartingPlayerIndex, Deck deck = null, bool cutDeck = true)
        {
            IsRunning = true;
            if (deck == null || deck.IsEmpty())
            {
                cutDeck = true;
                deck = new Deck();
                deck.Shuffle();
            }
            if (cutDeck)
            {
                deck.Cut();
            }
            GameStartingPlayerIndex = gameStartingPlayerIndex;
            OriginalGameStartingPlayerIndex = GameStartingPlayerIndex;

            _log.Init();
            _log.Info("********");
            DebugString.AppendLine("*NewGame*");

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
            for (int i = 0; i < 7; i++)
            {
                GameStartingPlayer.Hand.Add(deck.TakeOne());
            }
            for (int i = 0; i < 5; i++)
            {
                players[(GameStartingPlayer.PlayerIndex + 1) % NumPlayers].Hand.Add(deck.TakeOne());
            }
            for (int i = 0; i < 5; i++)
            {
                players[(GameStartingPlayer.PlayerIndex + 2) % NumPlayers].Hand.Add(deck.TakeOne());
            }
            for (int i = 0; i < 5; i++)
            {
                GameStartingPlayer.Hand.Add(deck.TakeOne());
            }
            for (int i = 0; i < 5; i++)
            {
                players[(GameStartingPlayer.PlayerIndex + 1) % NumPlayers].Hand.Add(deck.TakeOne());
            }
            for (int i = 0; i < 5; i++)
            {
                players[(GameStartingPlayer.PlayerIndex + 2) % NumPlayers].Hand.Add(deck.TakeOne());
            }

            talon = new List<Card>();
            InitPlayers();
#if !PORTABLE
            var programFolder = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            SaveGame(System.IO.Path.Combine(programFolder, "_def.hra"));
#else
            using (var fs = GetFileStream("_def.hra"))
            {
                SaveGame(fs);
            }
#endif        
        }

#if !PORTABLE
        public void LoadGame(string filename)
        {
            using (var fileStream = new FileStream(filename, FileMode.Open))
            {
                LoadGame(fileStream);
            }
        }
#endif

        public void LoadGame(Stream fileStream, bool calculateMoney = false, int impersonationPlayerIndex = 0, bool forceLoadToLastRound = false, int initialRound = -1)
        {
            _log.Init();
            _log.Info("********");
            DebugString.AppendLine("*LoadGame*");
#if !PORTABLE
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
#if DEBUG
            const string buildConfiguration = "DEBUG";
#else
            const string buildConfiguration = "RELEASE";
#endif
            _log.InfoFormat("Assembly version: {0} ({1})", version, buildConfiguration);
#endif
            //_log.InfoFormat("**Loading game {0}**\n", Path.GetFileName(filename));
            IsRunning = true;

            var serializer = new XmlSerializer(typeof(GameDto));
            var gameData = (GameDto)serializer.Deserialize(fileStream);
            fileStream.Position = 0;
            var xmlrdr = XmlReader.Create(fileStream);
            var parentNodes = new Stack<string>();
            var roundComments = new List<Queue<string>>();
            string[] tmpcomments = null;
            var comment = 0;

            if (initialRound == 0 ||
                ((initialRound == 1 ||
                  initialRound == Game.NumRounds + 1) &&
                 gameData.Typ != null))
            {
                gameData.Kolo = initialRound;
                gameData.Zacina = gameData.Voli;
            }
            else if (initialRound > 1 &&
                     initialRound <= Game.NumRounds &&
                     gameData.Stychy != null &&
                     gameData.Stychy.Count(i => i.Hrac1 != null &&
                                                i.Hrac2 != null &&
                                                i.Hrac3 != null) >= initialRound)
            {
                gameData.Kolo = initialRound;
                gameData.Zacina = gameData.Stychy[initialRound - 1].Zacina;
            }
            while (xmlrdr.Read())
            {
                switch (xmlrdr.NodeType)
                {
                    case XmlNodeType.Element:
                        if (!xmlrdr.IsEmptyElement)
                        {
                            parentNodes.Push(xmlrdr.Name);
                            if (xmlrdr.Name == "Stych")
                            {
                                tmpcomments = new[] { "-", "-", "-" };
                                comment = 0;
                            }
                        }
                        break;
                    case XmlNodeType.Comment:
                        if (parentNodes.Any() &&
                            parentNodes.Peek() == "Hra")
                        {
                            BiddingDebugInfo.Clear();
                            BiddingDebugInfo.Append(xmlrdr.Value.Trim());
                        }
                        else if (parentNodes.Any() &&
                            parentNodes.Peek() == "Stych" &&
                            tmpcomments != null)
                        {
                            if (comment < tmpcomments.Length)
                            {
                                tmpcomments[comment++] = xmlrdr.Value.Trim();
                            }
                        }
                        break;
                    case XmlNodeType.EndElement:
                        if (xmlrdr.Name == "Stych")
                        {
                            roundComments.Add(new Queue<string>(tmpcomments));
                            tmpcomments = null;
                        }
                        parentNodes.Pop();
                        break;
                }
            }

            RoundNumber = 0;
            if (gameData.Typ.HasValue)
            {
                GameType = gameData.Typ.Value;
            }
            if (GameType != 0 && (GameType & (Hra.Betl | Hra.Durch)) == 0)
            {
                trump = gameData.Trumf;
            }

            //pouze novou hru lze sehrat za jineho hrace
            if (gameData.Kolo > 0 || impersonationPlayerIndex < 0 || impersonationPlayerIndex >= Game.NumPlayers)
            {
                impersonationPlayerIndex = 0;
            }
            var shift = Game.NumPlayers - impersonationPlayerIndex;
            var hraci = new[] { Hrac.Hrac1, Hrac.Hrac2, Hrac.Hrac3 };
            var voli = Enum.IsDefined(typeof(Hrac), gameData.Voli) ? Array.IndexOf(hraci, gameData.Voli) : (int)gameData.Voli;
            var zacina = Enum.IsDefined(typeof(Hrac), gameData.Voli) ? Array.IndexOf(hraci, gameData.Zacina) : (int)gameData.Zacina;

            GameStartingPlayerIndex = (voli + shift) % Game.NumPlayers;
            OriginalGameStartingPlayerIndex = GameStartingPlayerIndex;
            _roundStartingPlayer = players[(zacina + shift) % Game.NumPlayers];

            Author = gameData.Autor;
            Comment = gameData.Komentar;
            Bidding = new Bidding(this);
            foreach (var flek in gameData.Fleky)
            {
                switch (flek.Hra)
                {
                    case Hra.Hra:
                    case Hra.Kilo:
                        Bidding._gameFlek = flek.Pocet + 1; break;
                    case Hra.Sedma:
                        Bidding._sevenFlek = flek.Pocet + 1; break;
                    case Hra.SedmaProti:
                        Bidding._sevenAgainstFlek = flek.Pocet + 1; break;
                    case Hra.KiloProti:
                        Bidding._hundredAgainstFlek = flek.Pocet + 1; break;
                    case Hra.Betl:
                    case Hra.Durch:
                        Bidding._betlDurchFlek = flek.Pocet + 1; break;
                }
                if ((flek.Hraci & Hrac.Hrac1) != 0)
                {
                    Bidding.AllPlayerBids[0] |= flek.Hra;
                }
                if ((flek.Hraci & Hrac.Hrac2) != 0)
                {
                    Bidding.AllPlayerBids[1] |= flek.Hra;
                }
                if ((flek.Hraci & Hrac.Hrac3) != 0)
                {
                    Bidding.AllPlayerBids[2] |= flek.Hra;
                }
            }
            Bidding.AllPlayerBids[GameStartingPlayerIndex] |= gameData.TypValue & ~(Hra.SedmaProti | Hra.KiloProti);

            if (gameData.Talon == null)
            {
                gameData.Talon = new Karta[0];
            }
            talon = new List<Card>(gameData.Talon.Select(i => new Card(i.Barva, i.Hodnota)));

            if (gameData.Stychy == null)
            {
                gameData.Stychy = new Stych[0];
            }

            switch (impersonationPlayerIndex)
            {
                case 1:
                    players[0].Hand.AddRange(gameData.Hrac2.Select(i => new Card(i.Barva, i.Hodnota)));
                    players[1].Hand.AddRange(gameData.Hrac3.Select(i => new Card(i.Barva, i.Hodnota)));
                    players[2].Hand.AddRange(gameData.Hrac1.Select(i => new Card(i.Barva, i.Hodnota)));

                    players[0].Hand.AddRange(gameData.Stychy.Where(i => i.Hrac1 != null &&
                                                                        i.Hrac2 != null &&
                                                                        i.Hrac3 != null).Select(i => new Card(i.Hrac2.Barva, i.Hrac2.Hodnota)));
                    players[1].Hand.AddRange(gameData.Stychy.Where(i => i.Hrac1 != null &&
                                                                        i.Hrac2 != null &&
                                                                        i.Hrac3 != null).Select(i => new Card(i.Hrac3.Barva, i.Hrac3.Hodnota)));
                    players[2].Hand.AddRange(gameData.Stychy.Where(i => i.Hrac1 != null &&
                                                                        i.Hrac2 != null &&
                                                                        i.Hrac3 != null).Select(i => new Card(i.Hrac1.Barva, i.Hrac1.Hodnota)));
                    break;
                case 2:
                    players[0].Hand.AddRange(gameData.Hrac3.Select(i => new Card(i.Barva, i.Hodnota)));
                    players[1].Hand.AddRange(gameData.Hrac1.Select(i => new Card(i.Barva, i.Hodnota)));
                    players[2].Hand.AddRange(gameData.Hrac2.Select(i => new Card(i.Barva, i.Hodnota)));

                    players[0].Hand.AddRange(gameData.Stychy.Where(i => i.Hrac1 != null &&
                                                                        i.Hrac2 != null &&
                                                                        i.Hrac3 != null).Select(i => new Card(i.Hrac3.Barva, i.Hrac3.Hodnota)));
                    players[1].Hand.AddRange(gameData.Stychy.Where(i => i.Hrac1 != null &&
                                                                        i.Hrac2 != null &&
                                                                        i.Hrac3 != null).Select(i => new Card(i.Hrac1.Barva, i.Hrac1.Hodnota)));
                    players[2].Hand.AddRange(gameData.Stychy.Where(i => i.Hrac1 != null &&
                                                                        i.Hrac2 != null &&
                                                                        i.Hrac3 != null).Select(i => new Card(i.Hrac2.Barva, i.Hrac2.Hodnota)));
                    break;
                case 0:
                default:
                    players[0].Hand.AddRange(gameData.Hrac1.Select(i => new Card(i.Barva, i.Hodnota)));
                    players[1].Hand.AddRange(gameData.Hrac2.Select(i => new Card(i.Barva, i.Hodnota)));
                    players[2].Hand.AddRange(gameData.Hrac3.Select(i => new Card(i.Barva, i.Hodnota)));

                    players[0].Hand.AddRange(gameData.Stychy.Where(i => i.Hrac1 != null &&
                                                                        i.Hrac2 != null &&
                                                                        i.Hrac3 != null).Select(i => new Card(i.Hrac1.Barva, i.Hrac1.Hodnota)));
                    players[1].Hand.AddRange(gameData.Stychy.Where(i => i.Hrac1 != null &&
                                                                        i.Hrac2 != null &&
                                                                        i.Hrac3 != null).Select(i => new Card(i.Hrac2.Barva, i.Hrac2.Hodnota)));
                    players[2].Hand.AddRange(gameData.Stychy.Where(i => i.Hrac1 != null &&
                                                                        i.Hrac2 != null &&
                                                                        i.Hrac3 != null).Select(i => new Card(i.Hrac3.Barva, i.Hrac3.Hodnota)));
                    break;
            }
            players[0].Hand = players[0].Hand.Distinct().ToList();
            players[1].Hand = players[1].Hand.Distinct().ToList(); 
            players[2].Hand = players[2].Hand.Distinct().ToList();
            var temp = new List<Card>();
            for (int i = 0; i < 3; i++)
            {
                foreach (var c in players[i].Hand)
                {
                    if (temp.Contains(c))
                    {
                        throw new InvalidDataException($"LoadGame error: {c} spotted more than once");
                    }
                    temp.Add(c);
                }
            }
            foreach (var c in talon)
            {
                if (temp.Contains(c))
                {
                    throw new InvalidDataException($"LoadGame error: {c} spotted more than once");
                }
                temp.Add(c);
            }
            InitPlayers();
            OnGameLoaded();

            rounds = new Round[NumRounds];
            TrumpCard = trump.HasValue
                        ? players[GameStartingPlayerIndex].Hand
                                                          .Where(i => i.Suit == trump)
                                                          .OrderBy(i => i.Value)
                                                          .First()
                        : null;
            foreach (var stych in gameData.Stychy.Where(i => i.Kolo < gameData.Kolo && i.Hrac1 != null && i.Hrac2 != null && i.Hrac3 != null).OrderBy(i => i.Kolo))
            {
                if (RoundNumber == 0)
                {
                    OnGameTypeChosen(new GameTypeChosenEventArgs    //dovolime hracum aby zjistili jaky jsou trumfy
                    {
                        GameType = GameType,
                        TrumpCard = TrumpCard,
                        axTalon = talon.Where(i => i.Value == Hodnota.Eso || i.Value == Hodnota.Desitka).ToList(),
                        GameStartingPlayerIndex = GameStartingPlayerIndex
                    });
                }
                RoundNumber++;

                var comments = roundComments != null &&
                               RoundNumber >= 1 && 
                               RoundNumber <= roundComments.Count()
                               ? roundComments[RoundNumber - 1]
                               : null;
                var debugNotes = comments != null && comments.Count() >= NumPlayers 
                                     ? new [] 
                                       {
                                           comments.Dequeue().StringBeforeToken("\n").Trim(), 
                                           comments.Dequeue().StringBeforeToken("\n").Trim(),
                                           comments.Dequeue().StringBeforeToken("\n").Trim()
                                       }
                                     : new [] { string.Empty, string.Empty, string.Empty };
                var cards = new[] {stych.Hrac1, stych.Hrac2, stych.Hrac3};
                var player1 = Array.IndexOf(hraci, stych.Zacina);
                var player2 = (player1 + 1) % NumPlayers;
                var player3 = (player1 + 2) % NumPlayers;
                
                var c1 = new Card(cards[player1].Barva, cards[player1].Hodnota);
                var c2 = new Card(cards[player2].Barva, cards[player2].Hodnota);
                var c3 = new Card(cards[player3].Barva, cards[player3].Hodnota);
                //inside this constructor we replay the round and call all event handlers to ensure that all players can update their ai model
                var r = new Round(this, players[player1], c1, c2, c3, RoundNumber, debugNotes[player1], debugNotes[player2], debugNotes[player3]);

                rounds[stych.Kolo - 1] = r;

                players[player1].Hand.Remove(c1);
                players[player2].Hand.Remove(c2);
                players[player3].Hand.Remove(c3);

                OnRoundFinished(r);
            }
            RoundNumber = gameData.Kolo;
            DebugString.AppendLine($"Loaded round {RoundNumber}");
            if (!calculateMoney &&
                RoundNumber > 0 && 
                ((talon == null || !talon.Any()) ||
                 GameType == 0 ||
                 ((GameType & (Hra.Betl | Hra.Durch)) == 0 &&
                  !trump.HasValue) ||
                 players[0].Hand.Count != 10 - RoundNumber + 1 ||
                 players[1].Hand.Count != 10 - RoundNumber + 1 ||
                 players[2].Hand.Count != 10 - RoundNumber + 1))
            {
                throw new InvalidDataException("Game check failed");
            }
            //if (DoSort)
            //{
            //    if (RoundNumber > 0)
            //    {
            //        players[GameStartingPlayerIndex].Hand.Sort();   //voliciho hrace utridime pokud uz zvolil trumf
            //    }
            //    players[(GameStartingPlayerIndex + 1) % NumPlayers].Hand.Sort();
            //    players[(GameStartingPlayerIndex + 2) % NumPlayers].Hand.Sort();
            //}
            RoundSanityCheck();
            if(RoundNumber == 0)
            {
                Rewind();
            }
            if(calculateMoney)
            {
                Results = GetMoneyCalculator();
                Results.CalculateMoney();
            }
            RoundSanityCheck();
        }

#if !PORTABLE
        public void SaveGame(string filename, bool saveDebugInfo = false)
        {
            using (var fileStream = new FileStream(filename, FileMode.Create))
            {
                SaveGame(fileStream, saveDebugInfo);
            }
        }
#endif

        public void SaveGame(Stream fileStream, bool saveDebugInfo = false, bool saveFromEditor = false, bool logAiModel = false, int simulatedGameStartingPlayer = -1, Hra? simulatedGameType = null, Hand[] simulatedHands = null, List<RoundDebugContext> simulatedRounds = null, MoneyCalculatorBase simulatedResult = null)
        {
            try
            {
                if (simulatedGameStartingPlayer < 0)
                {
                    simulatedGameStartingPlayer = GameStartingPlayerIndex;
                }
                var startingPlayerIndex = simulatedGameStartingPlayer;
                var roundNumber = RoundNumber;

                if (CurrentRound != null)
                {
                    if (CurrentRound.roundWinner == null)
                    {
                        //save after an exception (unfinished round)
                        startingPlayerIndex = CurrentRound.player1.PlayerIndex;
                        //Comment = string.Format("Exception has been thrown at round {0}", CurrentRound.number);
                        if (CurrentRound.c1 != null)
                        {
                            CurrentRound.player1.Hand.Add(CurrentRound.c1);
                        }
                        if (CurrentRound.c2 != null)
                        {
                            CurrentRound.player2.Hand.Add(CurrentRound.c2);
                        }
                    }
                    else
                    {
                        startingPlayerIndex = CurrentRound.roundWinner.PlayerIndex;
                        roundNumber++;
                    }
                }
                var fleky = Enum.GetValues(typeof(Hra))
                                .Cast<Hra>()
                                .Where(gt => (gt & GameType) != 0)
                                .Select(gt =>
                {
                    var flek = 0;
                    switch (gt)
                    {
                        case Hra.Hra:
                        case Hra.Kilo:
                            flek = Bidding._gameFlek; break;
                        case Hra.Sedma:
                            flek = Bidding._sevenFlek; break;
                        case Hra.SedmaProti:
                            flek = Bidding._sevenAgainstFlek; break;
                        case Hra.KiloProti:
                            flek = Bidding._hundredAgainstFlek; break;
                        case Hra.Betl:
                        case Hra.Durch:
                            flek = Bidding._betlDurchFlek; break;
                    }

                    Hrac flekoval = 0;

                    if ((Bidding.AllPlayerBids[0] & gt) != 0)
                    {
                        flekoval |= Hrac.Hrac1;
                    }
                    if ((Bidding.AllPlayerBids[1] & gt) != 0)
                    {
                        flekoval |= Hrac.Hrac2;
                    }
                    if ((Bidding.AllPlayerBids[2] & gt) != 0)
                    {
                        flekoval |= Hrac.Hrac3;
                    }

                    if (flek <= 2 &&
                        (gt & (Hra.SedmaProti)) == 0 &&
                        (gt & (Hra.KiloProti)) == 0)
                    {
                        if (simulatedGameStartingPlayer == 0)
                        {
                            flekoval &= ~Hrac.Hrac1;
                        }
                        if (simulatedGameStartingPlayer == 1)
                        {
                            flekoval &= ~Hrac.Hrac2;
                        }
                        if (simulatedGameStartingPlayer == 2)
                        {
                            flekoval &= ~Hrac.Hrac3;
                        }
                    }
                    return new Flek
                    {
                        Hra = gt,
                        Pocet = flek - 1,
                        Hraci = flekoval
                    };
                })
                .Where(i => i.Pocet > 0 ||
                            (i.Hra & (Hra.SedmaProti)) != 0 ||
                            (i.Hra & (Hra.KiloProti)) != 0)
                .ToArray();
                var hands = simulatedHands != null ? simulatedHands.Select(i => (List<Card>)i).ToArray() : new[]
                {
                    new List<Card>(players[0].Hand),
                    new List<Card>(players[1].Hand),
                    new List<Card>(players[2].Hand)
                };

                if (saveFromEditor)
                {
                    rounds = new Round[NumRounds];
                }

                var roundsToSave = simulatedRounds ?? rounds.Select(r => r == null ? null : new RoundDebugContext
                {
                    number = r.number,
                    c1 = r.c1,
                    c2 = r.c2,
                    c3 = r.c3,
                    hlas1 = r.hlas1,
                    hlas2 = r.hlas2,
                    hlas3 = r.hlas3,
                    r1 = r.debugNote1,
                    r2 = r.debugNote2,
                    r3 = r.debugNote3,
                    RoundStarterIndex = r.player1.PlayerIndex
                }).ToList();
                var result = simulatedResult ?? Results;

                if (!IsRunning &&
                    simulatedRounds == null &&
                    rounds[0] != null &&
                    ((GameType & (Hra.Betl | Hra.Durch)) == 0 ||
                     result.MoneyWon[simulatedGameStartingPlayer] > 0))
                {
                    hands[0].Clear();
                    hands[1].Clear();
                    hands[2].Clear();
                }
                if (RoundNumber > 0)
                {
                    hands[simulatedGameStartingPlayer].Sort(SortMode.SuitsOnly);
                }
                if (!saveFromEditor)
                {
                    hands[(simulatedGameStartingPlayer + 1) % NumPlayers].Sort(SortMode.SuitsOnly);
                    hands[(simulatedGameStartingPlayer + 2) % NumPlayers].Sort(SortMode.SuitsOnly);
                }
                var hraci = new[] { Hrac.Hrac1, Hrac.Hrac2, Hrac.Hrac3 };
                var gameType = simulatedGameType ?? GameType;
                var gameDto = new GameDto
                {
                    Kolo = roundNumber,
                    Voli = hraci[simulatedGameStartingPlayer],
                    Trumf = gameType != 0 ? trump : null,
                    Typ = gameType != 0 ? (Hra?)gameType : null,
                    Zacina = hraci[startingPlayerIndex],
                    Autor = Author,
                    Verze = Version.ToString(),
                    BiddingNotes = BiddingDebugInfo?.ToString(),
                    Komentar = Comment,
                    Hrac1 = hands[0]
                        ?.Select(i => new Karta
                        {
                            Barva = i.Suit,
                            Hodnota = i.Value
                        }).ToArray(),
                    Hrac2 = hands[1]
                        ?.Select(i => new Karta
                        {
                            Barva = i.Suit,
                            Hodnota = i.Value
                        }).ToArray(),
                    Hrac3 = hands[2]
                        ?.Select(i => new Karta
                        {
                            Barva = i.Suit,
                            Hodnota = i.Value
                        }).ToArray(),
                    Fleky = fleky,
                    Stychy = roundsToSave.Where(r => r != null)
                        .Select(r => new Stych
                        {
                            Kolo = r.number,
                            Zacina = hraci[r.RoundStarterIndex]
                        }).ToArray(),
                    Talon = (simulatedHands?[Game.TalonIndex] ?? talon)
                        ?.Select(i => new Karta
                        {
                            Barva = i.Suit,
                            Hodnota = i.Value
                        })?.ToArray()
                };
                foreach (var stych in gameDto.Stychy.Where(i => i != null))
                {
                    if (simulatedRounds != null)
                    {
                        var sr = simulatedRounds[stych.Kolo - 1];
                        var scards = new[] { sr.c1, sr.c2, sr.c3 };
                        var sdebugInfo = new[] { sr.r1, sr.r2, sr.r3 };
                        var splayerIndices = new[] { sr.RoundStarterIndex, (sr.RoundStarterIndex + 1) % Game.NumPlayers, (sr.RoundStarterIndex + 2 ) % Game.NumPlayers };

                        var sindex = Array.IndexOf(splayerIndices, 0);
                        stych.Hrac1 = new Karta
                        {
                            Barva = scards[sindex].Suit,
                            Hodnota = scards[sindex].Value,
                            Poznamka = sdebugInfo[sindex]
                        };
                        sindex = Array.IndexOf(splayerIndices, 1);
                        stych.Hrac2 = new Karta
                        {
                            Barva = scards[sindex].Suit,
                            Hodnota = scards[sindex].Value,
                            Poznamka = sdebugInfo[sindex]
                        };
                        sindex = Array.IndexOf(splayerIndices, 2);
                        stych.Hrac3 = new Karta
                        {
                            Barva = scards[sindex].Suit,
                            Hodnota = scards[sindex].Value,
                            Poznamka = sdebugInfo[sindex]
                        };
                        continue;
                    }
                    var r = rounds[stych.Kolo - 1];
                    if (r == null)
                    {
                        continue;
                    }
                    var cards = new[] { r.c1, r.c2, r.c3 };
                    var debugInfo = new[] { r.debugNote1, r.debugNote2, r.debugNote3 };
                    var aiDebugInfo = new[] { r.aiDebugNote1, r.aiDebugNote2, r.aiDebugNote3 };
                    var probDebugInfo = new[] { r.probDebugNote1, r.probDebugNote2, r.probDebugNote3 };
                    var playerIndices = new[] { r.player1.PlayerIndex, r.player2.PlayerIndex, r.player3.PlayerIndex };

                    var index = Array.IndexOf(playerIndices, 0);
                    if (cards[index] != null)
                    {
                        stych.Hrac1 = new Karta
                        {
                            Barva = cards[index].Suit,
                            Hodnota = cards[index].Value,
                            Poznamka = debugInfo[index],
                            AiDebugInfo = aiDebugInfo[index],
                            AiDebugInfo2 = probDebugInfo[index]
                        };
                    }
                    index = Array.IndexOf(playerIndices, 1);
                    if (cards[index] != null)
                    {
                        stych.Hrac2 = new Karta
                        {
                            Barva = cards[index].Suit,
                            Hodnota = cards[index].Value,
                            Poznamka = debugInfo[index],
                            AiDebugInfo = aiDebugInfo[index],
                            AiDebugInfo2 = probDebugInfo[index]
                        };
                    }
                    index = Array.IndexOf(playerIndices, 2);
                    if (cards[index] != null)
                    {
                        stych.Hrac3 = new Karta
                        {
                            Barva = cards[index].Suit,
                            Hodnota = cards[index].Value,
                            Poznamka = debugInfo[index],
                            AiDebugInfo = aiDebugInfo[index],
                            AiDebugInfo2 = probDebugInfo[index]
                        };
                    }
                }
                if (result != null)
                {
                    gameDto.Zuctovani = new Zuctovani
                    {
                        Hrac1 = new Skore
                        {
                            Body = simulatedGameStartingPlayer == 0 ? result.PointsWon : result.PointsLost,
                            Zisk = result.MoneyWon[0]
                        },
                        Hrac2 = new Skore
                        {
                            Body = simulatedGameStartingPlayer == 1 ? result.PointsWon : result.PointsLost,
                            Zisk = result.MoneyWon[1]
                        },
                        Hrac3 = new Skore
                        {
                            Body = simulatedGameStartingPlayer == 2 ? result.PointsWon : result.PointsLost,
                            Zisk = result.MoneyWon[2]
                        }
                    };
                }

                gameDto.SaveGame(fileStream, saveDebugInfo);
            }
            catch (Exception e)
            {
                using(var sw = new StreamWriter(fileStream))
                {
                    var c1 = CurrentRound?.c1;
                    var c2 = CurrentRound?.c2;
                    var c3 = CurrentRound?.c3;
                    var p = GameStartingPlayerIndex + 1;
                    sw.Write($"<!--\nException caught while saving game,\ntype {GameType} played by player{p} round {RoundNumber}, c1 {c1}, c2 {c2}, c3 {c3}.\n{e.Message}\n{e.StackTrace}\n-->");
                }
            }
        }
         
        private void LogHands()
        {
            for (var i = 0; i < NumPlayers; i++)
            {
                var handstr = players[i].Hand != null ? new Hand(players[i].Hand).ToString() : "(null)";
                DebugString.AppendLine($"Player{i + 1}: {handstr}");
            }
            var talonstr = talon != null ? new Hand(talon).ToString() : "(null)";
            DebugString.AppendLine($"Talon: {talonstr}");
        }

        public async Task PlayGame(CancellationToken cancellationToken = default(CancellationToken), Hra? desiredGameType = null)
        {
            try
            {
                CancellationToken = cancellationToken;
                LogHands();
                //zahajeni hry
                PreGameHook();

                if (RoundNumber == 0)
                {
                    GameType = Hra.Hra; //docasne nastavena nejaka minimalni hra
                    Bidding = new Bidding(this);

                    await ChooseGame();
                    RoundNumber++;
                }
                if (ShouldPlayGame() &&
                    (desiredGameType == null ||
                     (GameType & desiredGameType) != 0))
                {
                    //vlastni hra
                    var roundWinner = _roundStartingPlayer;

                    for (; RoundNumber <= NumRounds; RoundNumber++)
                    {
                        var catchCardsMayExist = false;

                        //predcasne vitezstvi ukazuju jen do sedmeho kola, pro posledni 2 karty to nema smysl
                        //(kontrola je po sedmem kole)
                        if (RoundNumber <= 8 && 
                            ((AllowPlayerAutoFinish && roundWinner.PlayerIndex == 0) || 
                             (AllowAIAutoFinish && roundWinner.PlayerIndex != 0)) &&
                            PlayerWinsGame(roundWinner, out catchCardsMayExist))
                        {
                            IsRunning = false;
                            if (GameType == Hra.Betl && !catchCardsMayExist)
                            {
                                roundWinner = GameStartingPlayer;
                            }
                            var winningHand = roundWinner.Hand.ToList();
                            CompleteUnfinishedRounds();
                            OnGameWonPrematurely(this, new GameWonPrematurelyEventArgs { winner = roundWinner, winningHand = winningHand, roundNumber = RoundNumber });
                            break;
                        }
                        var r = new Round(this, roundWinner);

                        LastRoundNumber = RoundNumber;
                        DebugString.AppendFormat("Starting round {0}\n", RoundNumber);
                        RoundSanityCheck();
                        OnRoundStarted(r);

                        rounds[RoundNumber - 1] = r;
                        roundWinner = await r.PlayRound();

                        DebugString.AppendFormat("Finished round {0}\n", RoundNumber);
                        OnRoundFinished(r);
                        if(IsGameOver(r))
                        {
                            break;
                        }
                    }
                }
                //zakonceni hry
                IsRunning = false;
                Results = GetMoneyCalculator();
                Results.CalculateMoney();
#if PORTABLE
                using (var fs = GetFileStream("_end.hra"))
                {
                    SaveGame(fs, saveDebugInfo: true);
                }
#endif
                Bidding.Die();
                OnGameFinished(Results);
            }
            catch (Exception ex)
            {
                if (ex.ContainsCancellationException())
                {
                    _log.Debug("OperationCanceledException caught");
                }
                else
                {
                    var ae = ex as AggregateException;
                    if (ae != null)
                    {
                        ex = ae.Flatten().InnerExceptions[0];
                    }
                    _log.Error("Exception in PlayGame()", ex);
#if !PORTABLE
                    SaveGame(string.Format("_error_{0}.hra", DateTime.Now.ToString("yyyyMMddHHmmss")), saveDebugInfo: true);
#else
                    //SaveGame(GetFileStream(string.Format("_error_{0}.hra", DateTime.Now.ToString("yyyyMMddHHmmss"))));
                    using (var fs = GetFileStream("_error.hra"))
                    {
                        SaveGame(fs, saveDebugInfo: true);
                    }
                    using (var fs = GetFileStream("_error.txt"))
                    {
                        using(var tw = new StreamWriter(fs))
                        {
                            tw.Write($"{ex.Message}\n{ex.StackTrace}\n{DebugString.ToString()}\n-\n{BiddingDebugInfo.ToString()}");
                        }
                    }
#endif
                    OnGameException(new GameExceptionEventArgs { e = ex });
                    throw;
                }
            }
            finally
            {
                IsRunning = false;
                //try
                //{
                //    Die();
                //}
                //catch(Exception ex)
                //{                    
                //}
            }
        }

        public void Die()
        {
            Bidding?.Die();
            Bidding = null;
            foreach (var player in players.Where(i => i != null))
            {
                player.Die();
            }
            if (GameLoaded != null)
            {
                foreach (Delegate d in GameLoaded.GetInvocationList())
                {
                    GameLoaded -= (GameLoadedEventHandler)d;
                }
            }
            if (GameFlavourChosen != null)
            {
                foreach (Delegate d in GameFlavourChosen.GetInvocationList())
                {
                    GameFlavourChosen -= (GameFlavourChosenEventHandler)d;
                }
            }
            if (GameTypeChosen != null)
            {
                foreach (Delegate d in GameTypeChosen.GetInvocationList())
                {
                    GameTypeChosen -= (GameTypeChosenEventHandler)d;
                }
            }
            if (BidMade != null)
            {
                foreach (Delegate d in BidMade.GetInvocationList())
                {
                    BidMade -= (BidMadeEventHandler)d;
                }
            }
            if (CardPlayed != null)
            {
                foreach (Delegate d in CardPlayed.GetInvocationList())
                {
                    CardPlayed -= (CardPlayedEventHandler)d;
                }
            }
            if (RoundStarted != null)
            {
                foreach (Delegate d in RoundStarted.GetInvocationList())
                {
                    RoundStarted -= (RoundEventHandler)d;
                }
            }
            if (RoundFinished != null)
            {
                foreach (Delegate d in RoundFinished.GetInvocationList())
                {
                    RoundFinished -= (RoundEventHandler)d;
                }
            }
            if (GameFinished != null)
            {
                foreach (Delegate d in GameFinished.GetInvocationList())
                {
                    GameFinished -= (GameFinishedEventHandler)d;
                }
            }
            if (GameWonPrematurely != null)
            {
                foreach (Delegate d in GameWonPrematurely.GetInvocationList())
                {
                    GameWonPrematurely -= (GameWonPrematurelyEventHandler)d;
                }
            }
            if (GameException != null)
            {
                foreach (Delegate d in GameException.GetInvocationList())
                {
                    GameException -= (GameExceptionEventHandler)d;
                }
            }
        }

        private void RoundSanityCheck()
        {
            Exception e = null;
            try
            {
                LogHands();
                var cardsPlayed = rounds.Where(i => i != null && i.c3 != null).SelectMany(i => new[] { i.c1, i.c2, i.c3 }).ToList();

                for (var i = 0; i < NumPlayers; i++)
                {
                    var distinctHand = new List<Card>(players[i].Hand).Distinct().ToList();
                    if (distinctHand.Count != players[i].Hand.Count)
                    {
                        players[i].Hand = distinctHand;
                    }
                    foreach (var c in cardsPlayed)
                    {
                        players[i].Hand.Remove(c);
                    }

                    var aiPlayer = players[i] as AiPlayer;

                    if (aiPlayer != null &&
                        aiPlayer.Probabilities != null &&
                        aiPlayer.PlayerIndex == GameStartingPlayerIndex &&
                        (aiPlayer._talon == null ||
                         aiPlayer._talon.Count() == 0))
                    {
                        aiPlayer._talon = new List<Card>(talon);
                        if (aiPlayer.Probabilities.IsUpdateProbabilitiesAfterTalonNeeded())
                        {
                            aiPlayer.Probabilities.UpdateProbabilitiesAfterTalon(new List<Card>(aiPlayer.Hand), new List<Card>(aiPlayer._talon));
                        }
                    }
                }
                if (talon != null)
                {
                    var distinctHand = new List<Card>(talon).Distinct().ToList();
                    if (distinctHand.Count != talon.Count)
                    {
                        talon = distinctHand;
                    }
                    for (var i = 0; i < NumPlayers; i++)
                    {
                        foreach (var c in talon)
                        {
                            players[i].Hand.Remove(c);
                        }
                    }
                    if (RoundNumber > 0 && talon.Count() != 2)
                    {
                        if (players.Sum(i => i.Hand.Count()) + cardsPlayed.Count() == 30)
                        {
                            talon = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                        .SelectMany(b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                             .Select(h => new Card(b, h)))
                                        .Where(i => !cardsPlayed.Contains(i) &&
                                                    players.All(j => !j.Hand.Contains(i)))
                                        .ToList();
                        }
                        else
                        {
                            e = new InvalidOperationException($"Bad talon count: {talon.Count()} hands: {players[0].Hand.Count()} {players[1].Hand.Count()} {players[2].Hand.Count()}");
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{ex.GetType().Name} in RoundSanityCheck:\n {ex.Message}\n{ex.StackTrace}");
            }
            if (e != null)
            {
                throw e;
            }
        }

        public void Rewind()
        {
            DebugString.AppendLine("Rewind");
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
            if (talon != null && talon.Any())
            {
                GameStartingPlayer.Hand.AddRange(talon);
                talon.Clear();
            }
            RoundNumber = 0;
            GameType = 0;
            InitPlayers();
        }

        public Deck GetDeckFromLastGame()
        {
            var deck = new List<Card>();
            var sb = new StringBuilder();

            if (rounds != null)
            {
                //slozime karty v nahodnem poradi
                sb.AppendFormat("Adding hand of player 1\nAdding hand of player 2\nAdding hand of player 3\nAdding talon");
                var hand1 = new List<Card>(players[0].Hand).Shuffle().ToList();
                var hand2 = new List<Card>(players[1].Hand).Shuffle().ToList();
                var hand3 = new List<Card>(players[2].Hand).Shuffle().ToList();

                //setridime zbyle karty hracu v ruce, aby se barvy slozily k sobe
                hand1.Sort(SortMode.SuitsOnly, shuffleSuits: true);
                hand2.Sort(SortMode.SuitsOnly, shuffleSuits: true);
                hand3.Sort(SortMode.SuitsOnly, shuffleSuits: true);

                //do balicku budeme karty pridavat odzadu
                hand1.Reverse();
                hand2.Reverse();
                hand3.Reverse();

                var all = new List<IEnumerable<Card>>()
                {
                    hand1,
                    hand2,
                    hand3,
                    talon
                };

                foreach (var player in players)
                {
                    var playedCards = new List<Card>();
                    var playedHlasCards = new List<Card>();

                    //dodame karty ve stychu vyjma hlasu
                    foreach (var r in rounds.Where(r => r != null && r.number <= LastRoundNumber))
                    {
                        if (r.roundWinner.PlayerIndex == player.PlayerIndex)
                        {
                            if (r.hlas3)
                            {
                                playedHlasCards.Add(r.c3);
                                sb.AppendFormat("Round {0} card 3: Hlas {1}\n", r.number, r.c3);
                            }
                            else
                            {
                                playedCards.Add(r.c3);
                                sb.AppendFormat("Round {0} card 3: Adding {1}\n", r.number, r.c3);
                            }
                            if (r.hlas2)
                            {
                                playedHlasCards.Add(r.c2);
                                sb.AppendFormat("Round {0} card 2: Hlas {1}\n", r.number, r.c2);
                            }
                            else
                            {
                                playedCards.Add(r.c2);
                                sb.AppendFormat("Round {0} card 2: Adding {1}\n", r.number, r.c2);
                            }
                            if (r.hlas1)
                            {
                                playedHlasCards.Add(r.c1);
                                sb.AppendFormat("Round {0} card 1: Hlas {1}\n", r.number, r.c1);
                            }
                            else
                            {
                                playedCards.Add(r.c1);
                                sb.AppendFormat("Round {0} card 1: Adding {1}\n", r.number, r.c1);
                            }
                        }
                    }

                    if (playedCards.Any())
                    {
                        all.Add(playedCards);
                    }
                    if (playedHlasCards.Any())
                    {
                        all.Add(playedHlasCards);
                    }
                }
                sb.AppendFormat("Randomly collecting stacks\n");
                deck = all.Shuffle()
                          .SelectMany(i => i)
                          .Where(i => i != null)
                          .Distinct()
                          .ToList();
            }
            try
            {
                //obcas se nepodari balicek rekonstuovat - napr. protoze zmizel talon
                //neni jasne jak tato chyba vznika. Pokud karet nechybi moc,
                //tak se pokus rozbity balicek opravit dodanim chybejicich karet
                if (deck.Count < 32 && deck.Count > 24)
                {
                    sb.AppendFormat("Wrong deck count, adding missing cards\n");
                    foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                    {
                        foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                        {
                            if (!deck.Any(i => i.Suit == b && i.Value == h))
                            {
                                var c = new Card(b, h);

                                sb.AppendFormat("{0}\n", c);
                                deck.Add(c);
                            }
                        }
                    }
                }
                Deck newDeck;

                if (deck.Count != 32)
                {
                    newDeck = new Deck();
                    newDeck.Shuffle();
                }
                else
                {
                    newDeck = new Deck(deck);
                }
                //if (Results != null && !Results.GamePlayed)
                //{
                //    newDeck.Cut();
                //}

                return newDeck;
            }
            catch (InvalidDataException e)
            {
                //pokud se balicek nepodarilo dat dohromady, tak vyrob uplne novy
                //throw new InvalidDataException(sb.ToString(), e);
                var newDeck = new Deck();

                newDeck.Init();
                newDeck.Shuffle();

                return newDeck;
            }
        }

        public bool IsValidTalonCard(Card c)
        {
            //to druhe by nemelo teoreticky nastat, ale uz se to par hracum nejak povedlo
            return IsValidTalonCard(c.Value, c.Suit, trump, AllowAXTalon, AllowTrumpTalon) && c != TrumpCard;
        }

        public static bool IsValidTalonCard(Hodnota h, Barva b, Barva? trump, bool allowAX, bool allowTrump)
        {
            if (!trump.HasValue)
            {
                return true;
            }
            if (b == trump.Value && !allowTrump)
            {
                return false;
            }
            return (h != Hodnota.Eso && 
                    h != Hodnota.Desitka) || allowAX;
        }

#endregion

#region Private methods

        private MoneyCalculatorBase GetMoneyCalculator()
        {
            switch (CalculationStyle)
            {
                case CalculationStyle.Fixed:
                    return new FixedMoneyCalculator(this, CurrencyFormat);
                case CalculationStyle.Adding:
                    return new AddingMoneyCalculator(this, CurrencyFormat);
                case CalculationStyle.Multiplying:
                    return new MultiplyingMoneyCalculator(this, CurrencyFormat);
                default:
                    throw new Exception(string.Format("Unsupported calculation style: {0}", CalculationStyle));
            }
        }

        private bool ShouldPlayGame()
        {
            if (GivenUp)
            {
                return false;
            }
            if (SkipBidding)
            {
                return true; //pokud je vyple flekovani, tak hrajeme vzdycky
            }
            if (rounds != null &&
                rounds[0] != null)
            {
                return true; //pokud nahravame rozehranou hru, tak hrajeme vzdycky
            }
            //if (GameType == Hra.Hra && Bidding.GameMultiplier == 1)
            if (GameType == Hra.Hra && 
                Bidding.GameMultiplier < (1 << MinimalBidsForGame))
            {
                return false; //neflekovana hra se nehraje v zavislosti na nastaveni
            }
            if (GameType == (Hra.Hra | Hra.Sedma) && 
                Bidding.SevenMultiplier < (1 << MinimalBidsForSeven) && 
                Bidding.GameMultiplier < (1 << MinimalBidsForGame))
            {
                return false; //neflekovana sedma se nehraje v zavislosti na nastaveni
            }
            if (GameType == (Hra.Hra | Hra.Sedma) && 
                Bidding.SevenMultiplier == 1 &&
                Bidding.GameMultiplier == 2 &&
                //SevenValue == GameValue * 2 &&
                !PlayZeroSumGames)
            {
                return false; //sedma a flek na hru se nehraje v zavislosti na nastaveni
            }
            return true;
        }

        private bool IsGameOver(Round r)
        {
            if ((GameType == Hra.Betl && r.roundWinner == GameStartingPlayer) ||
                (GameType == Hra.Durch && r.roundWinner != GameStartingPlayer))
            {
                return true;
            }

            return false;
        }

        private bool PlayerWinsGame(AbstractPlayer player, out bool catchCardsMayExist)
        {
            var player2 = (player.PlayerIndex + 1) % Game.NumPlayers;
            var player3 = (player.PlayerIndex + 2) % Game.NumPlayers;
            var hand1 = new List<Card>(player.Hand);
            var hand2 = new List<Card>(players[player2].Hand);
            var hand3 = new List<Card>(players[player3].Hand);
            var hands = new[] { hand1, hand2, hand3 };

            catchCardsMayExist = false;
            if (GameType == Hra.Betl)
            {
                //hrac ktery vynasi ma vsechny nejvyssi karty a spoluhrac se nema jak dostat do stychu i kdyby aktera chytal
                if (hand1.All(i => hand2.All(j => hand3.All(k => i.IsHigherThan(j, trump) && i.IsHigherThan(k, trump)))))
                {
                    catchCardsMayExist = true;
                    return true;
                }
                var player1 = (Game.NumPlayers - player.PlayerIndex + GameStartingPlayerIndex) % Game.NumPlayers;
                player2 = (player1 + 1) % Game.NumPlayers;
                player3 = (player1 + 2) % Game.NumPlayers;

                return hands[player1].All(i => hands[player2].All(j => hands[player3].All(k => j.IsHigherThan(i, trump) && k.IsHigherThan(i, trump))));
            }
            else
            {
                //pokud nema hrac trumfy ale nekdo jiny je ma, tak nelze koncit
                if (trump.HasValue && 
                    hand1.All(i => i.Suit != trump.Value) &&
                    (hand2.Any(i => i.Suit == trump.Value) ||
                     hand3.Any(i => i.Suit == trump.Value)))
                {
                    return false;
                }
                //return player.Hand.All(i => players[player2].Hand.All(j => players[player3].Hand.All(k => Round.WinningCard(i, j, k, trump) == i)));
                var holesPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                       .Where(b => hand1.Any(i => i.Suit == b))
                                       .ToDictionary(b => b,
                                                     b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                              .Count(h => (hand2.Any(i => i.Suit == b && i.Value == h) ||
                                                                           hand3.Any(i => i.Suit == b && i.Value == h)) &&
                                                                          (((GameType & (Hra.Betl | Hra.Durch)) != 0 &&
                                                                            hand1.Any(i => i.Suit == b && i.BadValue < Card.GetBadValue(h))) ||
                                                                           ((GameType & (Hra.Betl | Hra.Durch)) == 0 &&
                                                                            hand1.Any(i => i.Suit == b && i.Value < h)))));

                var topCards = hand1.Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                    .Where(h => ((GameType & (Hra.Betl | Hra.Durch)) == 0 && h > i.Value) ||
                                                                 ((GameType & (Hra.Betl | Hra.Durch)) != 0 && Card.GetBadValue(h) > i.BadValue))
                                                    .All(h => hand2.All(j => j.Suit != i.Suit || j.Value != h) &&
                                                              hand3.All(j => j.Suit != i.Suit || j.Value != h)))
                                    .GroupBy(g => g.Suit)
                                    .ToList();
                var topTrumps = trump.HasValue 
                                     ? topCards.Where(i => i.Key == trump.Value)
                                               .SelectMany(i => i)
                                               .ToList() 
                                     : new List<Card>();

                if (!OptimisticAutoFinish)
                {
                    return topCards.All(g => holesPerSuit[g.Key] == 0) &&
                           hand1.All(i => topCards.Any(g => i.Suit == g.Key)) &&        //nesmi existovat barva kde nemam nejvyssi karty
                           (!trump.HasValue ||
                            topTrumps.Count() >= hand2.CardCount(trump.Value) +
                                                 hand3.CardCount(trump.Value));
                }

                return topCards.All(g => holesPerSuit[g.Key] == 0 ||                //pokud mam v barve diru, musim mit vic
                                         g.Count() >= hand2.CardCount(g.Key) +      //nejvyssich karet nez maji ostatni hraci
                                                      hand3.CardCount(g.Key)) &&    //dohromady karet v dane barve    
                       hand1.All(i => topCards.Any(g => i.Suit == g.Key)) &&        //a nesmi existovat barva kde nemam nejvyssi karty
                       (!trump.HasValue ||
                        topTrumps.Count() >= hand2.CardCount(trump.Value) +
                                             hand3.CardCount(trump.Value));
            }
        }
        
        public Hra GetValidGameTypesForPlayer(AbstractPlayer player, GameFlavour gameFlavour, Hra minimalBid)
        {
            Hra validGameTypes;

            if(minimalBid == Hra.Hra)
            {
                validGameTypes = Hra.Hra;
                if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        .Any(b => player.Hand.HasK(b) && 
                                  player.Hand.HasQ(b)))
                {
                    validGameTypes |= Hra.Kilo; //aby neslo omylem hlasit kilo bez hlasky
                }
                if(player.Hand.Has7(trump.Value) ||
                   AllowFakeSeven ||
                   AllowFake107)
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

        private async Task ChooseGame()
        {
            GameFlavour gameFlavour;
            Hra validGameTypes = 0;
            var minimalBid = Hra.Hra;
            var gameTypeForPlayer = new Hra[] {0, 0, 0};
            var bidForPlayer = new Hra[] { 0, 0, 0 };
            var nextPlayer = GameStartingPlayer;
            var firstTime = true;
            var bidNumber = 0;
            var noMoreGameFlavourChoices = false;

            gameTypeForPlayer[GameStartingPlayerIndex] = Hra.Hra;
            DebugString.AppendLine("ChooseGame()");
            DebugString.AppendFormat("Player {0} ChooseTrump()\n", GameStartingPlayer.PlayerIndex + 1);
            TrumpCard = await GameStartingPlayer.ChooseTrump();
            DebugString.AppendFormat("TrumpCard: {0}\n", TrumpCard);
            trump = TrumpCard.Suit;
            GameType = 0;
            talon = new List<Card>();
            if (GameStartingPlayerIndex == 0)
            {
                PreGameHook();
            }
            //ptame se na barvu
            while (true)
            {
                var canChooseFlavour = true;

                if(gameTypeForPlayer.All(i => i == 0) && bidForPlayer.All(i => i == 0))
                {
                    break;
                }
                if (GameType == Hra.Durch)
                {
                    canChooseFlavour = false;
                }
                if(!firstTime && nextPlayer == GameStartingPlayer)
                {
                    noMoreGameFlavourChoices = true;
                }
                if (noMoreGameFlavourChoices)
                {
                    canChooseFlavour = false;
                }
                if (firstTime)
                {
                    if (GameStartingPlayer.Hand.Count() != 12)
                    {
                        DebugString.AppendLine("Invalid card count during ChooseGame()");
                        LogHands();
                        throw new InvalidOperationException($"Invalid card count during ChooseGame(): {GameStartingPlayer.Hand.Count()}");
                    }
                    DebugString.AppendFormat("Player {0} ChooseTalon()\n", GameStartingPlayer.PlayerIndex + 1);
                    talon = new List<Card>(await GameStartingPlayer.ChooseTalon());
                    GameStartingPlayer.Hand.RemoveAll(i => talon.Contains(i));
                    if (talon == null || talon.Count() != 2)
                    {
                        DebugString.AppendLine("Invalid talon count during ChooseGame()");
                        LogHands();                            
                        throw new InvalidOperationException($"Invalid talon count from player{GameStartingPlayerIndex + 1} during ChooseGame(): {talon?.Count()} null: {talon == null}");
                    }
                    DebugString.AppendFormat("talon: {0} {1}\n", talon[0], talon[1]);
                    BiddingDebugInfo.AppendFormat("\nPlayer {0} talon: {1} {2}", GameStartingPlayer.PlayerIndex + 1, talon[0], talon[1]);
                    if (GameStartingPlayer.Hand.Count() != 10)
                    {
                        DebugString.AppendLine("Invalid card count during ChooseGame()");
                        LogHands();                            
                        throw new InvalidOperationException($"Invalid card count during ChooseGame(): {GameStartingPlayer.Hand.Count()}");
                    }
                    if (talon.Any(i => !IsValidTalonCard(i))) //pokud je v talonu eso nebo desitka, musime hrat betla nebo durch
                    {
                        canChooseFlavour = false;
                    }
                }
                if (canChooseFlavour)
                {
                    DebugString.AppendFormat("Player {0} ChooseGameFlavour()\n", nextPlayer.PlayerIndex + 1);
                    gameFlavour = await nextPlayer.ChooseGameFlavour();
                    if (gameFlavour == GameFlavour.Good107 && 
                        (!Top107 || !firstTime))
                    {
                        gameFlavour = GameFlavour.Good;
                    }
                    DebugString.AppendFormat("ChooseGameFlavour: {0}\n", gameFlavour);
                    BiddingDebugInfo.AppendFormat("\nPlayer {0}: {1} ({2}/{3})", nextPlayer.PlayerIndex + 1,
                                                                                 gameFlavour.Description(),
                                                                                 nextPlayer.DebugInfo.RuleCount,
                                                                                 nextPlayer.DebugInfo.TotalRuleCount);
                    var betl = nextPlayer.DebugInfo.AllChoices.FirstOrDefault(i => i.Rule == "Betl");
                    if (betl?.TotalRuleCount > 0)
                    {
                        BiddingDebugInfo.AppendFormat("\nBetl ({0}/{1})", betl.RuleCount,
                                                                          betl.TotalRuleCount);
                    }
                    var durch = nextPlayer.DebugInfo.AllChoices.FirstOrDefault(i => i.Rule == "Durch");
                    if (durch?.TotalRuleCount > 0)
                    {
                        BiddingDebugInfo.AppendFormat("\nDurch ({0}/{1})", durch.RuleCount,
                                                                           durch.TotalRuleCount);
                    }
                    if (gameFlavour == GameFlavour.Bad)
                    {
                        GameStartingPlayerIndex = nextPlayer.PlayerIndex;
                        GivenUp = false;
                        trump = null;
                        TrumpCard = null;
                        foreach (var player in players)
                        {
                            player.BidMade = string.Empty;
                        }
                    }
                    OnGameFlavourChosen(new GameFlavourChosenEventArgs
                    {
                        Player = nextPlayer,
                        Flavour = gameFlavour,
                        AXTalon = gameFlavour != GameFlavour.Bad && talon.Any(i => i.Value == Hodnota.Desitka || i.Value == Hodnota.Eso)
                    });
                }
                else
                {
                    if (talon.Any(i => !IsValidTalonCard(i))) //pokud je v talonu eso nebo desitka, musime hrat betla nebo durch
                    {
                        gameFlavour = GameFlavour.Bad;
                        trump = null;
                        TrumpCard = null;
                        OnGameFlavourChosen(new GameFlavourChosenEventArgs
                        {
                            Player = nextPlayer,
                            Flavour = gameFlavour
                        });                    
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
                        bidForPlayer[nextPlayer.PlayerIndex] = await Bidding.GetBidsForPlayer(GameType, players[nextPlayer.PlayerIndex], bidNumber++);
                    }
                }
                else if(gameFlavour == GameFlavour.Bad)
                {
                    if (!firstTime)
                    {
                        if (talon == null || talon.Count() != 2)
                        {
                            DebugString.AppendLine("Invalid talon count during ChooseGame()");
                            LogHands();
                            throw new InvalidOperationException($"Invalid talon count after ChooseGameFlavour(): {talon?.Count()} null: {talon == null}");
                        }
                        DebugString.AppendFormat("Old talon {0} {1} goes to Player {2}\n", talon[0], talon[1], GameStartingPlayer.PlayerIndex + 1);
                        GameStartingPlayer.Hand.AddRange(talon);
                        talon.Clear();
                        if (GameStartingPlayer.Hand.Count() != 12)
                        {
                            DebugString.AppendLine("Invalid card count during ChooseGame()");
                            LogHands();
                            throw new InvalidOperationException($"Invalid card count after taking old talon: {GameStartingPlayer.Hand.Count()}");
                        }
                        DebugString.AppendFormat("Player {0} ChooseTalon()\n", GameStartingPlayer.PlayerIndex + 1);
                        talon = new List<Card>(await GameStartingPlayer.ChooseTalon());
                        GameStartingPlayer.Hand.RemoveAll(i => talon.Contains(i));
                        if (talon == null || talon.Count() != 2)
                        {
                            DebugString.AppendLine("Invalid talon count during ChooseGame()");
                            LogHands();
                            throw new InvalidOperationException($"Invalid talon count from player{GameStartingPlayerIndex+1} during ChooseGame(): {talon?.Count()} null: {talon == null}");
                        }
                        DebugString.AppendFormat("talon: {0} {1}\n", talon[0], talon[1]);
                        BiddingDebugInfo.AppendFormat("\nPlayer {0} talon: {1} {2}", GameStartingPlayer.PlayerIndex + 1, talon[0], talon[1]);
                        if (GameStartingPlayer.Hand.Count() != 10)
                        {                            
                            DebugString.AppendLine("Invalid card count during ChooseGame()");
                            LogHands();
                            throw new InvalidOperationException($"Invalid card count during ChooseGame(): {GameStartingPlayer.Hand.Count()}");
                        }
                    }
                    if(minimalBid == Hra.Hra)
                    {
                        minimalBid = Hra.Betl;
                    }
                    validGameTypes = GetValidGameTypesForPlayer(nextPlayer, gameFlavour, minimalBid);
                    DebugString.AppendFormat("Player {0} ChooseGameType()\n", GameStartingPlayer.PlayerIndex + 1);
                    GameType = await GameStartingPlayer.ChooseGameType(validGameTypes);
                    if ((GameType & Hra.Kilo) != 0 &&
                        GameStartingPlayer.DebugInfo.EstimatedHundredWinProbability > 0)
                    {
                        GameTypeConfidence = GameStartingPlayer.DebugInfo.EstimatedHundredWinProbability / 100f;
                    }
                    else if (GameType == Hra.Durch &&
                             GameStartingPlayer.DebugInfo.EstimatedDurchWinProbability > 0)
                    {
                        GameTypeConfidence = GameStartingPlayer.DebugInfo.EstimatedDurchWinProbability / 100f;
                    }
                    else
                    {
                        GameTypeConfidence = GameStartingPlayer.DebugInfo.TotalRuleCount > 0 ? (float)GameStartingPlayer.DebugInfo.RuleCount / (float)GameStartingPlayer.DebugInfo.TotalRuleCount : -1f;
                    }
                    DebugString.AppendFormat("ChooseGameType: {0}\n", GameType);
                    if (GameType == 0)
                    {
                        GameType = minimalBid;
                        GivenUp = true;
                    }
                    minimalBid = Hra.Durch;
                    gameTypeForPlayer[GameStartingPlayerIndex] = GameType;
                    OnGameTypeChosen(new GameTypeChosenEventArgs
                    {
                        GameStartingPlayerIndex = GameStartingPlayerIndex,
                        GameType = GameType,
                        TrumpCard = null,
                        axTalon = new List<Card>()
                    });
                    if (!SkipBidding)
                    {
                        bidNumber = 0;
                        Bidding = new Bidding(this);
                        Bidding.StartBidding(GameType);
                    }
                }
                else if (gameFlavour == GameFlavour.Good107)
                {
                    GameType = Hra.Kilo | Hra.Sedma;
                    break;
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
            if (GameType == 0 || (Top107 && GameType == (Hra.Kilo | Hra.Sedma)))
            {
                //hrac1 vybira normalni hru
                validGameTypes = GetValidGameTypesForPlayer(GameStartingPlayer, GameFlavour.Good, minimalBid);
                DebugString.AppendFormat("Player {0} ChooseGameType()\n", GameStartingPlayer.PlayerIndex + 1);
                if (Top107 && GameType == (Hra.Kilo | Hra.Sedma))
                {
                    GameStartingPlayer.ChooseGameType(validGameTypes); //vysledek zahodime, uz vime, co se bude hrat
                }
                else
                {
                    GameType = await GameStartingPlayer.ChooseGameType(validGameTypes);
                    if (GameType == 0)
                    {
                        GameType = Hra.Hra;
                        GivenUp = true;
                    }
                }
                if ((GameType & Hra.Kilo) != 0 &&
                    GameStartingPlayer.DebugInfo.EstimatedHundredWinProbability > 0)
                {
                    GameTypeConfidence = GameStartingPlayer.DebugInfo.EstimatedHundredWinProbability / 100f;
                }
                else if (GameType == Hra.Durch &&
                         GameStartingPlayer.DebugInfo.EstimatedDurchWinProbability > 0)
                {
                    GameTypeConfidence = GameStartingPlayer.DebugInfo.EstimatedDurchWinProbability / 100f;
                }
                else
                {
                    GameTypeConfidence = GameStartingPlayer.DebugInfo.TotalRuleCount > 0 ? (float)GameStartingPlayer.DebugInfo.RuleCount / (float)GameStartingPlayer.DebugInfo.TotalRuleCount : -1f;
                }
                DebugString.AppendFormat("ChooseGameType: {0}\n", GameType);
                OnGameTypeChosen(new GameTypeChosenEventArgs
                {
                    GameStartingPlayerIndex = GameStartingPlayerIndex,
                    GameType = GameType,
                    TrumpCard = TrumpCard,
                    axTalon = talon.Where(i => i.Value == Hodnota.Eso || i.Value == Hodnota.Desitka).ToList()
                });
                if(!SkipBidding)
                {
                    Bidding = new Bidding(this);
                    if (GameType != 0)
                    {
                        GameType = await Bidding.CompleteBidding();
                    }
                }
                if (Bidding.SevenMultiplier * SevenValue > 0 &&
                    Bidding.SevenMultiplier * SevenValue < Bidding.GameMultiplier * GameValue &&
                    GameStartingPlayer.DebugInfo.TotalRuleCount > 0 &&
                    !((GameType & Hra.Kilo) != 0 &&
                      GameStartingPlayer.DebugInfo.EstimatedHundredWinProbability > 0))
                {
                    GameTypeConfidence = (float)GameStartingPlayer.DebugInfo.RuleCount / (float)GameStartingPlayer.DebugInfo.TotalRuleCount;
                }
                if (GivenUp &&
                    (Bidding.SevenAgainstMultiplier > 0 ||      //bude se hrat
                     Bidding.HundredAgainstMultiplier > 0 ||    //bude se hrat
                     Bidding.GameMultiplier == 1))              //stejne se hrat nebude
                {
                    GivenUp = false;
                }
            }
            _roundStartingPlayer = GameStartingPlayer;
            if (SkipBidding)
            {
                Bidding.SetLastBidder(GameStartingPlayer, GameType);
            }
        }

        private void CompleteUnfinishedRounds()
        {
            var lastRoundWinner = rounds[0] == null ? GameStartingPlayer : rounds[0].roundWinner;
            //ulozime si momentalni karty hracu, protoze se v konstruktoru Round() z ruky postupne odebiraji
            var temp1 = new List<Card>(players[0].Hand);
            var temp2 = new List<Card>(players[1].Hand);
            var temp3 = new List<Card>(players[2].Hand);

            DebugString.AppendLine("CompleteUnfinishedRounds()");
            RoundSanityCheck();
            for (var i = 0; i < Game.NumRounds; i++)
            {
                var hand1 = new List<Card>(players[0].Hand);
                var hand2 = new List<Card>(players[1].Hand);
                var hand3 = new List<Card>(players[2].Hand);
                var hands = new[] { hand1, hand2, hand3 };

                if (rounds[i] == null)
                {
                    var player1 = lastRoundWinner.PlayerIndex;
                    var player2 = (lastRoundWinner.PlayerIndex + 1) % Game.NumPlayers;
                    var player3 = (lastRoundWinner.PlayerIndex + 2) % Game.NumPlayers;
                    Barva? firstSuit;
                    Barva? lastSuit;

                    if (trump.HasValue &&
                        (hands[player2].HasSuit(trump.Value) ||
                         hands[player3].HasSuit(trump.Value) ||
                         HlasConsidered == HlasConsidered.First))
                    {
                        firstSuit = trump;
                        lastSuit = null;
                    }
                    else if (trump.HasValue)
                    {
                        firstSuit = null;
                        lastSuit = trump;
                    }
                    else if (GameType == Hra.Betl)
                    {
                        firstSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                        .FirstOrDefault(b => hands[player1].HasSuit(b) &&
                                                        (hands[player2].HasSuit(b) ||
                                                         hands[player3].HasSuit(b)));
                        lastSuit = null;
                    }
                    else
                    {
                        firstSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                        .OrderByDescending(b => hands[player1].CardCount(b))
                                        .FirstOrDefault();
                        lastSuit = null;
                    }
                    var gt = GameType;

                    if (trump.HasValue)
                    {
                        gt |= Hra.Sedma | Hra.SedmaProti;   //abych vzdy uhral tichou sedmu nakonec pokud to je mozne
                    }
                    var c1 = AbstractPlayer.ValidCards(hands[player1], trump, gt, players[player1].TeamMateIndex)
                                           .Sort(SortMode.Descending, (GameType & (Hra.Betl | Hra.Durch)) != 0, firstSuit, lastSuit)
                                           .First();
                    var c2 = AbstractPlayer.ValidCards(hands[player2], trump, GameType, players[player2].TeamMateIndex, c1)
                                           .OrderBy(j => j.Value)
                                           .First();
                    var c3 = AbstractPlayer.ValidCards(hands[player3], trump, GameType, players[player3].TeamMateIndex, c1, c2)
                                           .OrderBy(j => j.Value)
                                           .First();

                    rounds[i] = new Round(this, lastRoundWinner, c1, c2, c3, i+1);
                }
                lastRoundWinner = rounds[i].roundWinner;
            }

            //obnovime puvodni karty hracu, aby neodehrane karty zustaly u sebe
            //a mohly se tudiz spravne slozit do balicku
            players[0].Hand = temp1;
            players[1].Hand = temp2;
            players[2].Hand = temp3;
        }

        public void AddBiddingDebugInfo(int playerIndex)
        {
            if (players[playerIndex].DebugInfo == null)
            {
                BiddingDebugInfo.Append("DebugInfo == null");
                return;
            }

            var kqScore = trump.HasValue
                            ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => players[playerIndex].Hand.HasK(b) && players[playerIndex].Hand.HasQ(b))
                                   .Sum(b => b == trump.Value ? 40 : 20)
                            : 0;

            if (trump.HasValue)
            {
                BiddingDebugInfo.AppendFormat("Odhad skóre: {0}", players[playerIndex].DebugInfo.EstimatedFinalBasicScore);
                if (kqScore > 0)
                {
                    BiddingDebugInfo.AppendFormat("+{0}", kqScore);
                }
                //BiddingDebugInfo.AppendFormat("\nSkóre2: {0}", players[playerIndex].DebugInfo.EstimatedFinalBasicScore2);
                //BiddingDebugInfo.AppendFormat("\nTygrovo: {0}", players[playerIndex].DebugInfo.Tygrovo);
                //BiddingDebugInfo.AppendFormat("\nSilná: {0}", players[playerIndex].DebugInfo.Strong);
                BiddingDebugInfo.AppendFormat("\nPočet děr: {0}", players[playerIndex].DebugInfo.TotalHoles);
            }
            if (players[playerIndex].TeamMateIndex == -1 && GameType != Hra.Durch && OriginalGameStartingPlayerIndex == playerIndex)
            {
                BiddingDebugInfo.AppendFormat("\nMinimální odhadovaná bodová ztráta: {0}", players[playerIndex].DebugInfo.MinBasicPointsLost);
                BiddingDebugInfo.AppendFormat("\nMaximální odhadovaná bodová ztráta: {0}", players[playerIndex].DebugInfo.MaxEstimatedBasicPointsLost);
                if (players[playerIndex].DebugInfo.MaxEstimatedHlasPointsLost > 0)
                {
                    BiddingDebugInfo.AppendFormat("+{0}", players[playerIndex].DebugInfo.MaxEstimatedHlasPointsLost);
                }
                BiddingDebugInfo.AppendFormat("\nPrůměrné simulované skóre: {0}:{1}", players[playerIndex].DebugInfo.AvgSimulatedPointsWon, players[playerIndex].DebugInfo.AvgSimulatedPointsLost);
                if (players[playerIndex].DebugInfo.MaxEstimatedMoneyLost < 0)
                {
                    BiddingDebugInfo.AppendFormat("\nMaximální odhadovaná prohra: {0}", players[playerIndex].DebugInfo.MaxEstimatedMoneyLost);
                }
                if (players[playerIndex].DebugInfo.EstimatedHundredLoss < 0)
                {
                    BiddingDebugInfo.AppendFormat("\nOdhadovaná prohra při kilu: {0}", players[playerIndex].DebugInfo.EstimatedHundredLoss);
                }
                if (players[playerIndex].DebugInfo.MaxSimulatedLoss < 0)
                {
                    BiddingDebugInfo.AppendFormat("\nMaximální simulovaná prohra: {0}", players[playerIndex].DebugInfo.MaxSimulatedLoss);
                }
                if (players[playerIndex].DebugInfo.MaxSimulatedHundredLoss < 0)
                {
                    BiddingDebugInfo.AppendFormat("\nMaximální simulovaná prohra při kilu: {0}", players[playerIndex].DebugInfo.MaxSimulatedHundredLoss);
                }
                if (players[playerIndex].DebugInfo.AvgSimulatedGameLoss < 0)
                {
                    BiddingDebugInfo.AppendFormat("\nPrůměrná simulovaná prohra při hře: {0}", players[playerIndex].DebugInfo.AvgSimulatedGameLoss);
                }
                if (players[playerIndex].DebugInfo.AvgSimulatedHundredLoss <= 0 &&
                    players[playerIndex].DebugInfo.MaxSimulatedHundredLoss < 0)
                {
                    BiddingDebugInfo.AppendFormat("\nPrůměrná simulovaná prohra při kilu: {0}", players[playerIndex].DebugInfo.AvgSimulatedHundredLoss);
                }
                if (players[playerIndex].DebugInfo.SevenTooRisky)
                {
                    BiddingDebugInfo.AppendFormat("\nPříliš riskantní na sedmu");
                }
                if (players[playerIndex].DebugInfo.EstimatedGreaseProbabilityDictionary.Any())
                {
                    foreach (var kvp in players[playerIndex].DebugInfo.EstimatedGreaseProbabilityDictionary)
                    {
                        BiddingDebugInfo.AppendFormat("\nPravděpodobnost námazu na {0}: {1}%", kvp.Key.Description(), kvp.Value);
                    }
                }
                //if (players[playerIndex].DebugInfo.EstimatedGreaseProbabilityList.Any())
                //{
                //    for(var i = 0; i < players[playerIndex].DebugInfo.EstimatedGreaseProbabilityList.Count; i++)
                //    {
                //        BiddingDebugInfo.AppendFormat("\nPravděpodobnost námazu {0} bodů: {1}%", (i+1)*10, players[playerIndex].DebugInfo.EstimatedGreaseProbabilityList[i]);
                //    }
                //}
                if (players[playerIndex].DebugInfo.EstimatedHundredWinProbability > 0)
                {
                    BiddingDebugInfo.AppendFormat("\nPravděpodobnost výhry kila: {0}%", players[playerIndex].DebugInfo.EstimatedHundredWinProbability);
                }
                if (players[playerIndex].DebugInfo.EstimatedAverageHundredMoneyWon < 0)
                {
                    BiddingDebugInfo.AppendFormat("\nOdhadovaná průměrná prohra u kila: {0}", players[playerIndex].DebugInfo.EstimatedAverageHundredMoneyWon);
                }

                if (kqScore > 0 &&
                    players[playerIndex].DebugInfo.HundredTooRisky)
                {
                    BiddingDebugInfo.AppendFormat("\nPříliš riskantní na kilo");
                }
                if (players[playerIndex].DebugInfo.EstimatedDurchWinProbability > 0)
                {
                    BiddingDebugInfo.AppendFormat("\nPravděpodobnost výhry durcha: {0}%", players[playerIndex].DebugInfo.EstimatedDurchWinProbability);
                }
            }
            else if (players[playerIndex].TeamMateIndex == -1 && GameType == Hra.Durch)
            {
                if (players[playerIndex].DebugInfo.EstimatedDurchWinProbability > 0)
                {
                    BiddingDebugInfo.AppendFormat("\nPravděpodobnost výhry durcha: {0}%", players[playerIndex].DebugInfo.EstimatedDurchWinProbability);
                }
            }
            BiddingDebugInfo.Append("\nVšechny simulace:");
            if (players[playerIndex].DebugInfo.AllChoices == null)
            {
                BiddingDebugInfo.Append("DebugInfo.AllChoices == null");
                return;
            }
            foreach (var choice in players[playerIndex].DebugInfo.AllChoices.Where(i => i?.TotalRuleCount > 0))
            {
                BiddingDebugInfo.AppendFormat("\n{0} ({1}/{2})", choice.Rule, choice.RuleCount, choice.TotalRuleCount);
            }
#if DEBUG && SAVE_SIMULATIONS
            using (var fs = GetFileStream($"Simulations/summary.txt"))
            {
                var str = BiddingDebugInfo.ToString();
                var buffer = Encoding.UTF8.GetBytes(str);
                fs.Write(buffer, 0, buffer.Length);
            }
#endif
        }
        #endregion
    }
}
