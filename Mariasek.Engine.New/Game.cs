using System;
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
using Mariasek.Engine.New.Logger;
using Mariasek.Engine.New.Configuration;
//#if !PORTABLE
using Mariasek.Engine.New.Schema;
//#endif

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
        public CancellationToken CancellationToken;
        private AbstractPlayer _roundStartingPlayer;

        #region Public fields and properties

        public const int NumPlayers = 3;
        public const int NumRounds = 10;
        public const int NumSuits = 4;

        public Func<IStringLogger> GetStringLogger { get; set; }
        public IStringLogger BiddingDebugInfo { get; private set; }
        public AbstractPlayer[] players { get; private set; }

        public AbstractPlayer GameStartingPlayer { get { return players[GameStartingPlayerIndex]; } }
        public int GameStartingPlayerIndex { get; private set; }
        public int OriginalGameStartingPlayerIndex { get; private set; }

        public MoneyCalculatorBase Results { get; private set; }

        public bool SkipBidding { get; set; }
        public float BaseBet { get; set; }
        public int MinimalBidsForGame { get; set; }
        public int MinimalBidsForSeven { get; set; }
        public bool Top107 { get; set; }
        public CalculationStyle CalculationStyle { get; set; }
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
        public Bidding Bidding { get; private set; }
        public string Author { get; set; }
        public bool DoSort { get; set; }
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

        public Game(Func<IStringLogger> stringLoggerFactory = null)
        {
            BaseBet = 1f;
            MinimalBidsForGame = 1;
            MinimalBidsForSeven = 0;
            Top107 = false;
            GameTypeConfidence = -1f;
            GetStringLogger = stringLoggerFactory;
            if (GetStringLogger == null)
            {
                GetStringLogger = () => new StringLogger();
            }
            BiddingDebugInfo = GetStringLogger();
            DebugString = GetStringLogger();
#if PORTABLE
            GetFileStream = _ => new MemoryStream(); //dummy stream factory
#endif
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

        public void NewGame(int gameStartingPlayerIndex, Deck deck = null)
        {
            IsRunning = true;
            if (deck == null || deck.IsEmpty())
            {
                deck = new Deck();
                deck.Shuffle();
            }
            deck.Cut();

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

        public void LoadGame(Stream fileStream)
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
            }
            //Bidding.PlayerBids je ztracene, ale pro hrani ho nepotrebujeme
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
            foreach (var stych in gameData.Stychy.Where(i => i.Hrac1 != null && i.Hrac2 != null && i.Hrac3 != null).OrderBy(i => i.Kolo))
            {
                if (RoundNumber == 0)
                {
                    OnGameTypeChosen(new GameTypeChosenEventArgs    //dovolime hracum aby zjistili jaky jsou trumfy
                    {
                        GameType = GameType,
                        TrumpCard = trump.HasValue
                                         ? players[GameStartingPlayerIndex].Hand
                                                                           .Where(i => i.Suit == trump)
                                                                           .OrderBy(i => i.Value)
                                                                           .First()
                                         : null,
                        GameStartingPlayerIndex = GameStartingPlayerIndex
                    });
                }
                RoundNumber++;
                var cards = new[] {stych.Hrac1, stych.Hrac2, stych.Hrac3};
                
                var player1 = (int) stych.Zacina;
                var player2 = ((int) stych.Zacina + 1) % NumPlayers;
                var player3 = ((int) stych.Zacina + 2) % NumPlayers;
                
                var c1 = new Card(cards[player1].Barva, cards[player1].Hodnota);
                var c2 = new Card(cards[player2].Barva, cards[player2].Hodnota);
                var c3 = new Card(cards[player3].Barva, cards[player3].Hodnota);
                var r = new Round(this, players[player1], c1, c2, c3, RoundNumber);  //inside this constructor we replay the round and call all event handlers to ensure that all players can update their ai model

                rounds[stych.Kolo - 1] = r;

                players[player1].Hand.Remove(c1);
                players[player2].Hand.Remove(c2);
                players[player3].Hand.Remove(c3);

                OnRoundFinished(r);
            }
            RoundNumber = gameData.Kolo;
            DebugString.AppendLine($"Loaded round {RoundNumber}");
            if (RoundNumber > 0 && 
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
            if (DoSort)
            {
                if (RoundNumber > 0)
                {
                    players[GameStartingPlayerIndex].Hand.Sort();   //voliciho hrace utridime pokud uz zvolil trumf
                }
                players[(GameStartingPlayerIndex + 1) % NumPlayers].Hand.Sort();
                players[(GameStartingPlayerIndex + 2) % NumPlayers].Hand.Sort();
            }
			RoundSanityCheck();
			if(RoundNumber == 0)
            {
                Rewind();
            }
            RoundSanityCheck();
#if !PORTABLE
            var programFolder = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            SaveGame(System.IO.Path.Combine(programFolder, "_temp.hra"));
#else
            //using (var fs = GetFileStream("_temp.hra"))
            //{
            //    SaveGame(fs);
            //}
#endif        
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

        public void SaveGame(Stream fileStream, bool saveDebugInfo = false)
        {
            try
            {
                var startingPlayerIndex = GameStartingPlayerIndex;
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
                    RoundSanityCheck();
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

                    return new Flek
                    {
                        Hra = gt,
                        Pocet = flek - 1
                    };
                }).ToArray();
                if (fleky.All(i => i.Pocet <= 0))
                {
                    fleky = new Flek[0];
                }
                var gameDto = new GameDto
                {
                    Kolo = roundNumber,
                    Voli = (Hrac)GameStartingPlayerIndex,
                    Trumf = trump.HasValue ? trump : null,
                    Typ = GameType != 0 ? (Hra?)GameType : null,
                    Zacina = (Hrac)startingPlayerIndex,
                    Autor = Author,
                    Verze = Version.ToString(),
                    BiddingNotes = BiddingDebugInfo?.ToString(),
                    Komentar = Comment,
                    Hrac1 = players[0].Hand
                        ?.Select(i => new Karta
                        {
                            Barva = i.Suit,
                            Hodnota = i.Value
                        }).ToArray(),
                    Hrac2 = players[1].Hand
                        ?.Select(i => new Karta
                        {
                            Barva = i.Suit,
                            Hodnota = i.Value
                        }).ToArray(),
                    Hrac3 = players[2].Hand
                        ?.Select(i => new Karta
                        {
                            Barva = i.Suit,
                            Hodnota = i.Value
                        }).ToArray(),
                    Fleky = fleky,
                    Stychy = rounds.Where(r => r != null)
                        .Select(r => new Stych
                        {
                            Kolo = r.number,
                            Zacina = (Hrac)r.player1.PlayerIndex
                        }).ToArray(),
                    Talon = talon
                        ?.Select(i => new Karta
                        {
                            Barva = i.Suit,
                            Hodnota = i.Value
                        })?.ToArray()
                };
                foreach (var stych in gameDto.Stychy.Where(i => i != null))
                {
                    var r = rounds[stych.Kolo - 1];
                    if (r == null)
                    {
                        continue;
                    }
                    var cards = new[] { r.c1, r.c2, r.c3 };
                    var debugInfo = new[] { r.debugNote1, r.debugNote2, r.debugNote3 };
                    var playerIndices = new[] { r.player1.PlayerIndex, r.player2.PlayerIndex, r.player3.PlayerIndex };

                    var index = Array.IndexOf(playerIndices, 0);
                    if (cards[index] != null)
                    {
                        stych.Hrac1 = new Karta
                        {
                            Barva = cards[index].Suit,
                            Hodnota = cards[index].Value,
                            Poznamka = debugInfo[index]
                        };
                    }
                    index = Array.IndexOf(playerIndices, 1);
                    if (cards[index] != null)
                    {
                        stych.Hrac2 = new Karta
                        {
                            Barva = cards[index].Suit,
                            Hodnota = cards[index].Value,
                            Poznamka = debugInfo[index]
                        };
                    }
                    index = Array.IndexOf(playerIndices, 2);
                    if (cards[index] != null)
                    {
                        stych.Hrac3 = new Karta
                        {
                            Barva = cards[index].Suit,
                            Hodnota = cards[index].Value,
                            Poznamka = debugInfo[index]
                        };
                    }
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

        public void PlayGame(CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                CancellationToken = cancellationToken;
                LogHands();
                //zahajeni hry
                if (RoundNumber == 0)
                {
                    GameType = Hra.Hra; //docasne nastavena nejaka minimalni hra
                    Bidding = new Bidding(this);
                    ChooseGame();
                    RoundNumber++;
                }

                if(ShouldPlayGame())
                {
                    //vlastni hra
                    var roundWinner = _roundStartingPlayer;

                    for (; RoundNumber <= NumRounds; RoundNumber++)
                    {
                        //predcasne vitezstvi ukazuju jen do sedmeho kola, pro posledni 2 karty to nema smysl
                        //(kontrola je po sedmem kole)
						if (RoundNumber <= 8 && PlayerWinsGame(roundWinner))
						{
							IsRunning = false;
							if (GameType == Hra.Betl)
							{
								roundWinner = GameStartingPlayer;
							}
                            var winningHand = roundWinner.Hand.ToList();
							CompleteUnfinishedRounds();
                            OnGameWonPrematurely(this, new GameWonPrematurelyEventArgs { winner = roundWinner, winningHand = winningHand, roundNumber = RoundNumber });
							break;
						}
						var r = new Round(this, roundWinner);

						DebugString.AppendFormat("Starting round {0}\n", RoundNumber);
                        RoundSanityCheck();
						OnRoundStarted(r);

                        rounds[RoundNumber - 1] = r;
                        roundWinner = r.PlayRound();

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
            }
        }

        private void RoundSanityCheck()
        {
            LogHands();
            var cardsPlayed = rounds.Where(i => i != null && i.c3 != null).SelectMany(i => new[] { i.c1, i.c2, i.c3 }).ToList();

            for (var i = 0; i < NumPlayers; i++)
            {
                players[i].Hand = players[i].Hand.Distinct().ToList();
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
                    aiPlayer._talon = talon;
                    if (aiPlayer.Probabilities.IsUpdateProbabilitiesAfterTalonNeeded())
                    {
                        aiPlayer.Probabilities.UpdateProbabilitiesAfterTalon(aiPlayer.Hand, aiPlayer._talon);
                    }
                }
			}
			if (talon != null)
			{
                talon = talon.Distinct().ToList();
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
                        throw new InvalidOperationException($"Bad talon count: {talon.Count()} hands: {players[0].Hand.Count()} {players[1].Hand.Count()} {players[2].Hand.Count()}");
                    }
                }
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
            GameStartingPlayer.Hand.AddRange(talon);
            talon.Clear();
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
                //slozime stychy v nahodnem poradi
                var randomPlayers = players.Shuffle<AbstractPlayer>();

                foreach (var player in randomPlayers)
                {
					sb.AppendFormat("Adding hand of player {0}\n", player.PlayerIndex + 1);
                    //dodame karty ve stychu vyjma hlasu
                    foreach (var r in rounds)
                    {
                        if (r == null)
                        {
                            break;
                        }
						if (r.roundWinner == player && !r.hlas1)
                        {
                            deck.Add(r.c1);
							sb.AppendFormat("Round {0} card 1: Adding {1}\n", r.number, r.c1);
                        }
						if (r.roundWinner == player && !r.hlas2)
                        {
                            deck.Add(r.c2);
							sb.AppendFormat("Round {0} card 2: Adding {1}\n", r.number, r.c2);
                        }
						if (r.roundWinner == player && !r.hlas3)
                        {
                            deck.Add(r.c3);
							sb.AppendFormat("Round {0} card 3: Adding {1}\n", r.number, r.c3);
                        }
                    }
					sb.Append("Hlasy\n");
                    foreach (var r in rounds)
                    {
                        if (r == null)
                        {
                            break;
                        }
                        //dodame hlasy
                        if (r.hlas1 && r.player1 == player)
                        {
                            deck.Add(r.c1);
							sb.AppendFormat("Round {0} card 1: Hlas {1}\n", r.number, r.c1);
                        }
                        if (r.hlas2 && r.player2 == player)
                        {
                            deck.Add(r.c2);
							sb.AppendFormat("Round {0} card 2: Hlas {1}\n", r.number, r.c2);
                        }
                        if (r.hlas3 && r.player3 == player)
                        {
                            deck.Add(r.c3);
							sb.AppendFormat("Round {0} card 3: Hlas {1}\n", r.number, r.c3);
                        }
                    }
					sb.AppendFormat("Adding hand of player {0}\n", player.PlayerIndex + 1);
					foreach (var c in player.Hand)
					{
						sb.AppendFormat("{0}\n", c);
					}
                    deck.AddRange(player.Hand);
                }
				sb.Append("Adding talon\n");
				foreach (var c in talon)
				{
					sb.AppendFormat("{0}\n", c);
				}
				deck.AddRange(talon);
            }
			try
			{
				return new Deck(deck.Distinct().ToList());
			}
			catch (InvalidDataException e)
			{
				throw new InvalidDataException(sb.ToString(), e);
			}
        }

        public bool IsValidTalonCard(Card c)
        {
			//to druhe by nemelo teoreticky nastat, ale uz se to par hracum nejak povedlo
			return IsValidTalonCard(c.Value, c.Suit, trump) && c != TrumpCard;
        }

        public static bool IsValidTalonCard(Hodnota h, Barva b, Barva? trump)
        {
            if (!trump.HasValue)
            {
                return true;
            }

            return h != Hodnota.Eso && 
                   h != Hodnota.Desitka;
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
            if (GivenUp)
            {
                return false;
            }
            if (SkipBidding)
            {
                return true; //pokud je vyple flekovani, tak hrajeme vzdycky
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
                Bidding.GameMultiplier == 2)
            {
                return false; //sedma a flek na hru se nehraje nikdy
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

                //pokud nema hrac trumfy ale nekdo jiny je ma, tak nelze koncit
                if (trump.HasValue && 
                    player.Hand.All(i => i.Suit != trump.Value) &&
                    (players[player2].Hand.Any(i => i.Suit == trump.Value) ||
                     players[player3].Hand.Any(i => i.Suit == trump.Value)))
                {
                    return false;
                }
				//return player.Hand.All(i => players[player2].Hand.All(j => players[player3].Hand.All(k => Round.WinningCard(i, j, k, trump) == i)));
				var holesPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                       .Where(b => player.Hand.Any(i => i.Suit == b))
                                       .ToDictionary(b => b,
                                                     b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                              .Count(h => (players[player2].Hand.Any(i => i.Suit == b && i.Value == h) ||
                                                                           players[player3].Hand.Any(i => i.Suit == b && i.Value == h)) &&
                                                                          (((GameType & (Hra.Betl | Hra.Durch)) != 0 &&
                                                                            player.Hand.Any(i => i.Suit == b && i.BadValue < Card.GetBadValue(h))) ||
																		   ((GameType & (Hra.Betl | Hra.Durch)) == 0 &&
																		    player.Hand.Any(i => i.Suit == b && i.Value < h)))));
				var topCards = player.Hand.Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
				                                          .Where(h => ((GameType & (Hra.Betl | Hra.Durch)) == 0 && h > i.Value) ||
                                                                       ((GameType & (Hra.Betl | Hra.Durch)) != 0 && Card.GetBadValue(h) > i.BadValue))
                                                          .All(h => players[player2].Hand.All(j => j.Suit != i.Suit || j.Value != h) &&
                                                                    players[player3].Hand.All(j => j.Suit != i.Suit || j.Value != h)))
				                          .GroupBy(g => g.Suit);
                var topTrumps = trump.HasValue 
                                     ? topCards.Where(i => i.Key == trump.Value)
                                               .SelectMany(i => i)
                                               .ToList() 
                                     : new List<Card>();

                return topCards.All(g => holesPerSuit[g.Key] == 0 ||                            //pokud mam v barve diru, musim mit vic
                                         g.Count() >= players[player2].Hand.CardCount(g.Key) +  //nejvyssich karet nez maji ostatni hraci
                                                      players[player3].Hand.CardCount(g.Key)) &&//dohromady karet v dane barve    
                       player.Hand.All(i => topCards.Any(g => i.Suit == g.Key)) &&              //a nesmi existovat barva kde nemam nejvyssi karty
                       (!trump.HasValue ||
                        topTrumps.Count() >= players[player2].Hand.CardCount(trump.Value) +
                                             players[player3].Hand.CardCount(trump.Value));
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
                if(player.Hand.Has7(trump.Value))
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
            var noMoreGameFlavourChoices = false;

            gameTypeForPlayer[GameStartingPlayerIndex] = Hra.Hra;
            DebugString.AppendLine("ChooseGame()");
            DebugString.AppendFormat("Player {0} ChooseTrump()\n", GameStartingPlayer.PlayerIndex + 1);
            TrumpCard = GameStartingPlayer.ChooseTrump();
            DebugString.AppendFormat("TrumpCard: {0}\n", TrumpCard);
            trump = TrumpCard.Suit;
            GameType = 0;
            talon = new List<Card>();
			//ptame se na barvu
            while(true)
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
                    talon = GameStartingPlayer.ChooseTalon();
                    GameStartingPlayer.Hand.RemoveAll(i => talon.Contains(i));
                    if (talon == null || talon.Count() != 2)
                    {
                        DebugString.AppendLine("Invalid talon count during ChooseGame()");
                        LogHands();                            
                        throw new InvalidOperationException($"Invalid talon count from player{GameStartingPlayerIndex + 1} during ChooseGame(): {talon?.Count()}");
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
                    gameFlavour = nextPlayer.ChooseGameFlavour();
                    if (gameFlavour == GameFlavour.Good107 && 
                        (!Top107 || !firstTime))
                    {
                        gameFlavour = GameFlavour.Good;
                    }
                    DebugString.AppendFormat("ChooseGameFlavour: {0}\n", gameFlavour);
                    BiddingDebugInfo.AppendFormat("\nPlayer {0}: {1} ({2}/{3})", nextPlayer.PlayerIndex + 1, gameFlavour.Description(), nextPlayer.DebugInfo.RuleCount, nextPlayer.DebugInfo.TotalRuleCount);
                    BiddingDebugInfo.AppendFormat("\nBetl ({0}/{1})", nextPlayer.DebugInfo.AllChoices.FirstOrDefault(i => i.Rule == "Betl")?.RuleCount ?? -1, nextPlayer.DebugInfo.TotalRuleCount);
                    BiddingDebugInfo.AppendFormat("\nDurch ({0}/{1})", nextPlayer.DebugInfo.AllChoices.FirstOrDefault(i => i.Rule == "Durch")?.RuleCount ?? -1, nextPlayer.DebugInfo.TotalRuleCount);
                    if (gameFlavour == GameFlavour.Bad)
                    {
						GameStartingPlayerIndex = nextPlayer.PlayerIndex;
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
                        Flavour = gameFlavour
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
                        bidForPlayer[nextPlayer.PlayerIndex] = Bidding.GetBidsForPlayer(GameType, players[nextPlayer.PlayerIndex], bidNumber++);
                    }
                }
                else if(gameFlavour == GameFlavour.Bad)
                {
                    GivenUp = false;
                    if (!firstTime)
                    {
                        if (talon == null || talon.Count() != 2)
                        {
                            DebugString.AppendLine("Invalid talon count during ChooseGame()");
                            LogHands();
                            throw new InvalidOperationException($"Invalid talon count after ChooseGameFlavour(): {talon?.Count()}");
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
						talon = GameStartingPlayer.ChooseTalon();
                        GameStartingPlayer.Hand.RemoveAll(i => talon.Contains(i));
						if (talon == null || talon.Count() != 2)
                        {
                            DebugString.AppendLine("Invalid talon count during ChooseGame()");
                            LogHands();
                            throw new InvalidOperationException($"Invalid talon count from player{GameStartingPlayerIndex+1} during ChooseGame(): {talon?.Count()}");
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
                    GameType = GameStartingPlayer.ChooseGameType(validGameTypes); //TODO: zkontrolovat ze hrac nezvolil nelegalni variantu
                    GameTypeConfidence = GameStartingPlayer.DebugInfo.TotalRuleCount > 0 ? (float)GameStartingPlayer.DebugInfo.RuleCount / (float)GameStartingPlayer.DebugInfo.TotalRuleCount : -1f;
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
                        TrumpCard = null
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
                    GameType = GameStartingPlayer.ChooseGameType(validGameTypes);
                    if (GameType == 0)
                    {
                        GameType = Hra.Hra;
                        GivenUp = true;
                    }
                }
                GameTypeConfidence = GameStartingPlayer.DebugInfo.TotalRuleCount > 0 ? (float)GameStartingPlayer.DebugInfo.RuleCount / (float)GameStartingPlayer.DebugInfo.TotalRuleCount : -1f;
                DebugString.AppendFormat("ChooseGameType: {0}\n", GameType);
                OnGameTypeChosen(new GameTypeChosenEventArgs
                {
                    GameStartingPlayerIndex = GameStartingPlayerIndex,
                    GameType = GameType,
                    TrumpCard = TrumpCard
                });
                if(!SkipBidding)
                {
                    Bidding = new Bidding(this);
                    if (GameType != 0)
                    {
                        GameType = Bidding.CompleteBidding();
                    }
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

            DebugString.AppendLine("CompleteUnfinishedRounds()");
            RoundSanityCheck();
            for (var i = 0; i < Game.NumRounds; i++)
            {                
                if (rounds[i] == null)
                {
                    var player1 = lastRoundWinner.PlayerIndex;
                    var player2 = (lastRoundWinner.PlayerIndex + 1) % Game.NumPlayers;
                    var player3 = (lastRoundWinner.PlayerIndex + 2) % Game.NumPlayers;
                    Barva? firstSuit;
                    Barva? lastSuit;

                    if (trump.HasValue &&
                        (players[player2].Hand.HasSuit(trump.Value) ||
                         players[player3].Hand.HasSuit(trump.Value)))
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
                                        .FirstOrDefault(b => players[player1].Hand.HasSuit(b) &&
                                                        (players[player2].Hand.HasSuit(b) ||
                                                         players[player3].Hand.HasSuit(b)));
                        lastSuit = null;
                    }
                    else
                    {
                        firstSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                        .OrderByDescending(b => players[player1].Hand.CardCount(b))
                                        .FirstOrDefault();
                        lastSuit = null;
                    }
                    var c1 = AbstractPlayer.ValidCards(players[player1].Hand, trump, GameType, players[player1].TeamMateIndex)
                                           .Sort(false, (GameType & (Hra.Betl | Hra.Durch)) != 0, firstSuit, lastSuit)
                                           .First();
                    var c2 = AbstractPlayer.ValidCards(players[player2].Hand, trump, GameType, players[player2].TeamMateIndex, c1).RandomOne();
                    var c3 = AbstractPlayer.ValidCards(players[player3].Hand, trump, GameType, players[player3].TeamMateIndex, c1, c2).RandomOne();

                    rounds[i] = new Round(this, lastRoundWinner, c1, c2, c3, i+1);
                }
                lastRoundWinner = rounds[i].roundWinner;
            }
        }

        public void AddBiddingDebugInfo(int playerIndex)
        {
            BiddingDebugInfo.Append("Všechny simulace:");
			if (players[playerIndex].DebugInfo == null)
			{
				BiddingDebugInfo.Append("DebugInfo == null");
				return;
			}
			if (players[playerIndex].DebugInfo.AllChoices == null)
			{
				BiddingDebugInfo.Append("DebugInfo.AllChoices == null");
				return;
			}
			foreach (var choice in players[playerIndex].DebugInfo.AllChoices.Where(i => i != null))
            {
                BiddingDebugInfo.AppendFormat("\n{0} ({1}/{2})", choice.Rule, choice.RuleCount, choice.TotalRuleCount);
            }
        }
        #endregion
    }
}
