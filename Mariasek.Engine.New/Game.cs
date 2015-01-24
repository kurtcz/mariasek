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

        public AbstractPlayer[] players { get; private set; }

        public AbstractPlayer GameStartingPlayer { get { return players[GameStartingPlayerIndex]; } }
        public int GameStartingPlayerIndex { get; private set; }
        public int OriginalGameStartingPlayerIndex { get; private set; }

        public MoneyCalculatorBase Results { get; private set; }

        public bool SkipBidding { get; set; }
        public Hra GameType { get; private set; }
        public Barva trump { get; private set; }
        public List<Card> talon { get; private set; }
        public Round[] rounds { get; private set; }
        public Round CurrentRound { get { return RoundNumber > 0  && RoundNumber <= 10 ? rounds[RoundNumber - 1] : null; } }
        public int RoundNumber { get; private set; }
        public Bidding Bidding { get; private set; }

        #endregion

        #region Events and delegates

        public delegate void GameTypeChosenEventHandler(object sender, GameTypeChosenEventArgs e);
        public event GameTypeChosenEventHandler GameTypeChosen;
        protected virtual void OnGameTypeChosen(GameTypeChosenEventArgs e)
        {
            if (GameTypeChosen != null)
            {
                GameTypeChosen(this, e);
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
        #endregion

        #region Public methods

        public Game()
        {
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
            players[(GameStartingPlayerIndex + 1) % NumPlayers].Hand.Sort();
            players[(GameStartingPlayerIndex + 2) % NumPlayers].Hand.Sort();

            talon = new List<Card>();

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
            _log.InfoFormat("Assembly version: {0} ({1})", version, buildConfiguration);
            _log.InfoFormat("**Loading game**\n");

            using (var fileStream = new FileStream(filename, FileMode.Open))
            {
                var serializer = new XmlSerializer(typeof(GameDto));
                var gameData = (GameDto)serializer.Deserialize(fileStream);

                RoundNumber = 0;
                if (gameData.Trumf.HasValue)
                {
                    trump = gameData.Trumf.Value;
                }
                if (gameData.Typ.HasValue)
                {
                    GameType = gameData.Typ.Value;
                }
                GameStartingPlayerIndex = (int) gameData.Voli;
                OriginalGameStartingPlayerIndex = GameStartingPlayerIndex;
                _roundStartingPlayer = players[(int)gameData.Zacina];

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

                    OnRoundFinished(r);
                }
                RoundNumber = gameData.Kolo;
                if(RoundNumber > 0)
                {
                    players[GameStartingPlayerIndex].Hand.Sort();   //voliciho hrace utridime pokud uz zvolil trumf
                }
                players[(GameStartingPlayerIndex + 1) % NumPlayers].Hand.Sort();
                players[(GameStartingPlayerIndex + 2) % NumPlayers].Hand.Sort();
            }
        }

        public void SaveGame(string filename)
        {
            var gameDto = new GameDto
            {
                Kolo = CurrentRound != null ? RoundNumber + 1 : 0,
                Voli = (Hrac) GameStartingPlayerIndex,
                Trumf = RoundNumber > 0 ? (Barva?) trump : null,
                Typ = RoundNumber > 0 ? (Hra?) GameType : null,
                Zacina = (Hrac) (CurrentRound != null ? CurrentRound.roundWinner.PlayerIndex : GameStartingPlayerIndex),
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
                    var playerIndices = new[] { r.player1.PlayerIndex, r.player2.PlayerIndex, r.player3.PlayerIndex };
                    int index = Array.IndexOf(playerIndices, 0);
                    stych.Hrac1 = new Karta
                    {
                        Barva = cards[index].Suit,
                        Hodnota = cards[index].Value
                    };
                    index = Array.IndexOf(playerIndices, 1);
                    stych.Hrac2 = new Karta
                    {
                        Barva = cards[index].Suit,
                        Hodnota = cards[index].Value
                    };
                    index = Array.IndexOf(playerIndices, 2);
                    stych.Hrac3 = new Karta
                    {
                        Barva = cards[index].Suit,
                        Hodnota = cards[index].Value
                    };
                }
            }
            catch (Exception ex)
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

            gameDto.SaveGame(filename);
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
                    ChooseGame();
                    Bidding = new Bidding(this);
                    if (!SkipBidding)
                    {
                        GameType = Bidding.StartBidding();
                    }
                    RoundNumber++;
                }
                else
                {
                    Bidding = new Bidding(this); 
                }
                //vlastni hra
                var roundWinner = _roundStartingPlayer;

                for (; RoundNumber <= NumRounds; RoundNumber++)
                {
                    var r = new Round(this, roundWinner);
                    OnRoundStarted(r);

                    rounds[RoundNumber - 1] = r;
                    roundWinner = r.PlayRound();

                    OnRoundFinished(r);
                }

                //zakonceni hry
                Results = new AddingMoneyCalculator(this);
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
#if !PORTABLE
                SaveGame(string.Format("_error_{0}.hra", DateTime.Now.ToString("yyyyMMddHHmmss")));
#endif
                throw;
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
            foreach (var r in rounds)
            {
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
            }
            deck.InsertRange(0, talon);

            //sejmeme
            var n = rand.Next(deck.Count);
            var temp = deck.GetRange(0, n);
            deck.RemoveRange(0, n);
            deck.AddRange(temp);

            return new Deck(deck);
        }

        #endregion

        #region Private methods


        private void ChooseGame()
        {
            var trumpCard = GameStartingPlayer.ChooseTrump();
            GameStartingPlayer.Hand.Sort();

            if (trumpCard == null)
            {
                throw new NotImplementedException("Betl a durch nejsou implementovany");
            }
            trump = trumpCard.Suit;
            talon = GameStartingPlayer.ChooseTalon();
            GameStartingPlayer.Hand.Remove(talon[0]);
            GameStartingPlayer.Hand.Remove(talon[1]);
            InitPlayers();

            //volba hry
            var minimalBid = Hra.Betl;
            var gameFlavour = GameStartingPlayer.ChooseGameFlavour();
            if (gameFlavour == GameFlavour.Good)
            {
                var player2 = players[(GameStartingPlayerIndex + 1) % NumPlayers];
                var player3 = players[(GameStartingPlayerIndex + 2) % NumPlayers];

                //hrac1: barva?
                gameFlavour = player2.ChooseGameFlavour();
                if (gameFlavour == GameFlavour.Bad)
                {
                    //hrac: spatna
                    player2.Hand.AddRange(talon);
                    talon.Clear();
                    talon = player2.ChooseTalon();
                    player2.Hand.Remove(talon[0]);
                    player2.Hand.Remove(talon[1]);
                    GameStartingPlayerIndex = player2.PlayerIndex;
                    //hrac1 vybira hru
                    GameType = player2.ChooseGameType(minimalBid: minimalBid);
                    if (GameType == Hra.Betl)
                    {
                        minimalBid = Hra.Durch;
                    }
                    OnGameTypeChosen(new GameTypeChosenEventArgs
                    {
                        GameStartingPlayerIndex = GameStartingPlayerIndex,
                        GameType = GameType,
                        TrumpCard = null
                    });
                }
                if (GameType < Hra.Durch)
                {
                    //hrac1: barva? nebo hrac2: betl?/durch?
                    gameFlavour = player3.ChooseGameFlavour();
                    if (gameFlavour == GameFlavour.Bad)
                    {
                        //hrac3: spatny
                        player3.Hand.AddRange(talon);
                        talon.Clear();
                        talon = player3.ChooseTalon();
                        player3.Hand.Remove(talon[0]);
                        player3.Hand.Remove(talon[1]);
                        GameStartingPlayerIndex = player3.PlayerIndex;
                        //hrac3 vybira hru
                        GameType = player3.ChooseGameType(minimalBid: minimalBid);
                        OnGameTypeChosen(new GameTypeChosenEventArgs
                        {
                            GameStartingPlayerIndex = GameStartingPlayerIndex,
                            GameType = GameType,
                            TrumpCard = null
                        });
                    }
                }
                if (GameStartingPlayerIndex == OriginalGameStartingPlayerIndex)
                {
                    //hrac1 vybira hru
                    GameType = GameStartingPlayer.ChooseGameType();
                    OnGameTypeChosen(new GameTypeChosenEventArgs
                    {
                        GameStartingPlayerIndex = GameStartingPlayerIndex,
                        GameType = GameType,
                        TrumpCard = trumpCard
                    });
                }
            }
            else
            {
                //hrac1: spatna barva
                GameType = GameStartingPlayer.ChooseGameType(minimalBid: minimalBid);
                OnGameTypeChosen(new GameTypeChosenEventArgs
                {
                    GameStartingPlayerIndex = GameStartingPlayerIndex,
                    GameType = GameType,
                    TrumpCard = null
                });
            }
            _roundStartingPlayer = GameStartingPlayer;
        }

        #endregion
    }
}
