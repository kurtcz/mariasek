using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using log4net;
using Logger;

namespace Mariasek.Engine
{
    /// <summary>
    /// Encapsulates one game
    /// </summary>
    public class Game
    {
        #region Fields

        public const int NumPlayers = 3;

        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        /// <summary>
        /// Gets the player instances (either AI or human)
        /// </summary>
        public AbstractPlayer[] players;
        /// <summary>
        /// Gets the current score
        /// </summary>
        public int[] score;
        /// <summary>
        /// Gets the talon cards as chosen by the <see cref="GameStartingPlayer"/>
        /// </summary>
        public List<Card> talon;
        /// <summary>
        /// Gets an array of rounds played in this game
        /// </summary>
        public Round[] rounds;
        /// <summary>
        /// Gets a trump suit
        /// </summary>
        public Barva trump;
        /// <summary>
        /// Gets a winner of the last round
        /// </summary>
        private AbstractPlayer _roundWinner;
        /// <summary>
        /// Gets the number of the current round starting from one.
        /// </summary>
        public int RoundNumber;
        /// <summary>
        /// Gets the player that started the game (the one who plays alone and chooses trumps)
        /// </summary>
        public AbstractPlayer GameStartingPlayer;

        #endregion

        #region Properties

        public Player HumanPlayer
        {
            get { return players[0] as Player; }
        }

        public Round CurrentRound
        {
            get { return RoundNumber > 0 ? rounds[RoundNumber - 1] : null; }
        }

        public AbstractPlayer RoundStartingPlayer { get; set; }

        #endregion

        #region Members

        public void Init(AbstractPlayer player1, AbstractPlayer player2, AbstractPlayer player3, int startingPlayerIndex)
        {
            var deck = new Deck();
            deck.Shuffle();
            Init(deck, player1, player2, player3, startingPlayerIndex);            
        }

        private void Init(Deck deck, AbstractPlayer player1, AbstractPlayer player2, AbstractPlayer player3, int startingPlayerIndex)
        {
            var logName = string.Format(@"Logs\LogFile.{0}.txt", DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
            LoggerSetup.TraceFileSetup(logName, LoggerSetup.FileMode.CreateNewOrTruncate);

            rounds = new Round[10];
            players = new AbstractPlayer[NumPlayers];
            players[0] = player1;
            players[1] = player2;
            players[2] = player3;
            RoundStartingPlayer = players[startingPlayerIndex];
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            var aiPlayer = players.First(i => i is AiPlayer) as AiPlayer;
#if DEBUG
            const string buildConfiguration = "DEBUG";
#else
            const string buildConfiguration = "RELEASE";
#endif
            _log.InfoFormat("Assembly version: {0} ({1})", version, buildConfiguration);
            _log.InfoFormat("**Starting game**\n{0}", aiPlayer.Settings);
            GameStartingPlayer = players[startingPlayerIndex];

            score = new int[NumPlayers];
            for(int i = 0; i < NumPlayers; i++)
            {
                score[i] = 0;
            }

            for (int i = 0; i < 12; i++)
            {
                players[startingPlayerIndex].Hand.Add(deck.TakeOne());
            }
            for (int i = 0; i < 10; i++)
            {
                players[(startingPlayerIndex + 1) % Game.NumPlayers].Hand.Add(deck.TakeOne());
            }
            for (int i = 0; i < 10; i++)
            {
                players[(startingPlayerIndex + 2) % Game.NumPlayers].Hand.Add(deck.TakeOne());
            }
            players[0].SortHand();
            players[1].SortHand();
            players[2].SortHand();
            players[startingPlayerIndex].TalonChosen += TalonChosen;
            players[startingPlayerIndex].TrumpChosen += TrumpChosen;
            players[startingPlayerIndex].GameTypeChosen += GameTypeChosen;
        }

        private void Init(AbstractPlayer player1, AbstractPlayer player2, AbstractPlayer player3, Barva Trump, int round, int startingPlayerIndex, int roundStartingPlayerIndex, Round[] oldRounds = null)
        {
            var logName = string.Format(@"Logs\LogFile.{0}.txt", DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
            LoggerSetup.TraceFileSetup(logName, LoggerSetup.FileMode.CreateNewOrTruncate);
            rounds = new Round[10];
            players = new AbstractPlayer[NumPlayers];
            players[0] = player1;
            players[1] = player2;
            players[2] = player3;
            RoundStartingPlayer = players[roundStartingPlayerIndex];
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            var aiPlayer = players.First(i => i is AiPlayer) as AiPlayer;
#if DEBUG
            const string buildConfiguration = "DEBUG";
#else
            const string buildConfiguration = "RELEASE";
#endif
            _log.InfoFormat("Binary version: {0} ({1})", version, buildConfiguration);
            _log.InfoFormat("**Loading game**\n{0}", aiPlayer.Settings);

            score = new[] {0, 0, 0};
            //Let's (re)create the probability map
            var hands = new []
                        {
                            new List<Card>(),
                            new List<Card>(),
                            new List<Card>(),
                        };
            //Start with each player's hand
            hands[0].AddRange(players[0].Hand);
            hands[1].AddRange(players[1].Hand);
            hands[2].AddRange(players[2].Hand);
            //Optionally add cards already played
            if (oldRounds != null)
            {
                for (var i = 0; i < oldRounds.Length; i++)
                {
                    var player1Index = Array.IndexOf(players, oldRounds[i].player1);
                    var player2Index = Array.IndexOf(players, oldRounds[i].player2);
                    var player3Index = Array.IndexOf(players, oldRounds[i].player3);

                    hands[player1Index].Add(oldRounds[i].c1);
                    hands[player2Index].Add(oldRounds[i].c2);
                    hands[player3Index].Add(oldRounds[i].c3);
                }
            }
            //This will happen after the game starts
            //Now let's initialize the probability maps
            for (var i = 0; i < Game.NumPlayers; i++)
            {
                (players[i] as IPlayerStats).Probabilities = new Probability(i, startingPlayerIndex, new Hand(hands[i]), Trump, GameStartingPlayer == players[i] ? talon : null);
            }

            //Optionally if we have info about played rounds let's re-play the old rounds to reconstruct the probability maps to the latest state
            if (oldRounds != null)
            {
                for (var i = 0; i < oldRounds.Length; i++)
                {
                    rounds[i] = oldRounds[i];
                    var roundWinnerIndex = Array.IndexOf(players, rounds[i].WinnerOfTheRound());
                    var roundStarterIndex = Array.IndexOf(players, rounds[i].player1);
                    score[roundWinnerIndex] += rounds[i].PointsWon();
                    score[roundStarterIndex] += rounds[i].player1Hlas;
                    score[(roundStarterIndex + 1) % Game.NumPlayers] += rounds[i].player2Hlas;
                    score[(roundStarterIndex + 2) % Game.NumPlayers] += rounds[i].player3Hlas;
                    for (var j = 0; j < Game.NumPlayers; j++)
                    {
                        (players[j] as IPlayerStats).Probabilities.UpdateProbabilities(RoundNumber, roundStarterIndex, rounds[i].c1, rounds[i].player1Hlas != 0, trump);
                        (players[j] as IPlayerStats).Probabilities.UpdateProbabilities(RoundNumber, roundStarterIndex, rounds[i].c1, rounds[i].c2, rounds[i].player2Hlas != 0);
                        (players[j] as IPlayerStats).Probabilities.UpdateProbabilities(RoundNumber, roundStarterIndex, rounds[i].c1, rounds[i].c2, rounds[i].c3, rounds[i].player3Hlas != 0);
                    }
                }
            }
            GameStartingPlayer = players[startingPlayerIndex];
            //if (round != 0)
            //{
            //    GameStartingPlayer = players[startingPlayerIndex];
            //}
            //else
            //{
            //    GameStartingPlayer = players[0];
            //}

            trump = Trump;
            RoundNumber = round;

            players[0].SortHand();
            players[1].SortHand();
            players[2].SortHand();
        }
       
        public void TalonChosen(object sender, TalonEventArgs e)
        {
            AbstractPlayer player = sender as AbstractPlayer;

            player.TalonChosen -= TalonChosen;
            talon = e.Talon;
            player.Hand.Remove(talon[0]);
            player.Hand.Remove(talon[1]);
            player.ChooseGameType();
        }

        public void TrumpChosen(object sender, CardEventArgs e)
        {
            AbstractPlayer player = sender as AbstractPlayer;

            player.TrumpChosen -= TrumpChosen;
            trump = e.card.Suit;
            player.ChooseTalon();
        }

        public void GameTypeChosen(object sender, GameTypeEventArgs e)
        {
            AbstractPlayer player = sender as AbstractPlayer;

            player.GameTypeChosen -= GameTypeChosen;
            PlayGame(player);            
        }

        public void PlayGame()
        {
            PlayGame(players[0]);
        }

        public void PlayGame(AbstractPlayer startingPlayer)
        {
            RoundNumber = 1;
            rounds[RoundNumber - 1] = new Round(this);

            rounds[RoundNumber - 1].RoundFinished += RoundFinished;
            rounds[RoundNumber - 1].PlayRound(startingPlayer);
        }

        public void RoundFinished(object sender, RoundFinishedEventArgs e)
        {
            //old roundWinner is round.player1
            Round oldRound = sender as Round;

            if(oldRound.player1Hlas != 0)
            {
                int index = Array.IndexOf(players, oldRound.player1);
                score[index] += oldRound.player1Hlas;
            }
            if (oldRound.player2Hlas != 0)
            {
                int index = Array.IndexOf(players, oldRound.player2);
                score[index] += oldRound.player2Hlas;
            }
            if (oldRound.player3Hlas != 0)
            {
                int index = Array.IndexOf(players, oldRound.player3);
                score[index] += oldRound.player3Hlas;
            }

            _roundWinner = e.Winner;
            int winner = Array.IndexOf(players, _roundWinner);
            score[winner] += e.PointsWon;
            if (RoundNumber == 10)
            {
                score[winner] += 10;

                OnGameFinished(new GameFinishedEventArgs(score[0], score[1], score[2]));
            }
            else
            {
                RoundNumber++;
                RoundStartingPlayer = _roundWinner;
                rounds[RoundNumber - 1] = new Round(this);
                rounds[RoundNumber - 1].RoundFinished += RoundFinished;
                rounds[RoundNumber - 1].PlayRound(_roundWinner);
            }
        }

        public static Game LoadGame(string filename, AiPlayerSettings playerSettings)
        {
            var g = new Game();
            Barva trump = Barva.Cerveny;
            int round = 0;
            int[] score = { 0, 0, 0 };
            string strStarter = null;
            string strGameStarter = null;
            string strTrump = null;
            var players = new AbstractPlayer[]
                          {
                              new Player(null, g),
                              new AiPlayer(null, g) {Settings = playerSettings},
                              new AiPlayer(null, g) {Settings = playerSettings}
                          };
            var oldRounds = new List<Round>();

            try
            {
                XmlReader reader = XmlReader.Create(filename);
                int cardsForPlayer = 0;
                int cardsForRound = 0;
                bool processPlayers = true;
                bool processTalon = false;
                bool processPoints = false;

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "Hra")
                        {
                            string strRound = reader.GetAttribute("Kolo");
                            round = int.Parse(strRound);

                            if (round < 0 || round > 11)
                            {
                                throw new Exception("Číslo kola musí být 0-11");
                            }

                            strTrump = reader.GetAttribute("Trumf");
                            strStarter = reader.GetAttribute("Zacina");
                            strGameStarter = reader.GetAttribute("Voli");
                            g.GameStartingPlayer = players[StringToPlayerIndex(strGameStarter)];
                        }
                        else if (reader.Name == "Hrac1" && processPlayers)
                        {
                            string strName = reader.GetAttribute("Jmeno");
                            players[0].Name = strName ?? "Hrac1";
                            if (cardsForRound == 0)
                            {
                                cardsForPlayer = 1;
                            }
                            else
                            {
                                //parsujeme stych
                                if (oldRounds.Count < cardsForRound)
                                {
                                    oldRounds.Add(new Round(g));
                                    cardsForPlayer = 1;
                                }
                                var r = oldRounds[oldRounds.Count - 1];
                                string strSuit = reader.GetAttribute("Barva");
                                string strValue = reader.GetAttribute("Hodnota");
                                var hlas = reader.GetAttribute("Hlas");
                                Barva suit = (Barva)Enum.Parse(typeof(Barva), strSuit, true);
                                Hodnota value = (Hodnota)Enum.Parse(typeof(Hodnota), strValue, true);
                                Card card = new Card(suit, value);

                                switch (cardsForPlayer)
                                {
                                    case 1: r.c1 = card;
                                            r.player1 = players[0];
                                            r.player1Hlas = (hlas == "1" ? (suit == trump ? 40 : 20) : 0);
                                            break;
                                    case 2: r.c2 = card;
                                            r.player2 = players[0];
                                            r.player2Hlas = (hlas == "1" ? (suit == trump ? 40 : 20) : 0);
                                            break;
                                    case 3: r.c3 = card;
                                            r.player3 = players[0];
                                            r.player3Hlas = (hlas == "1" ? (suit == trump ? 40 : 20) : 0);
                                            break;
                                }
                                cardsForPlayer++;
                            }
                        }
                        else if (reader.Name == "Hrac2" && processPlayers)
                        {
                            string strName = reader.GetAttribute("Jmeno");
                            players[1].Name = strName ?? "Hrac2";
                            if (cardsForRound == 0)
                            {
                                cardsForPlayer = 2;
                            }
                            else
                            {
                                //parsujeme stych
                                if (oldRounds.Count < cardsForRound)
                                {
                                    oldRounds.Add(new Round(g));
                                    cardsForPlayer = 1;
                                }
                                var r = oldRounds[oldRounds.Count - 1];
                                string strSuit = reader.GetAttribute("Barva");
                                string strValue = reader.GetAttribute("Hodnota");
                                var hlas = reader.GetAttribute("Hlas");
                                Barva suit = (Barva)Enum.Parse(typeof(Barva), strSuit, true);
                                Hodnota value = (Hodnota)Enum.Parse(typeof(Hodnota), strValue, true);
                                Card card = new Card(suit, value);

                                switch (cardsForPlayer)
                                {
                                    case 1: r.c1 = card;
                                        r.player1 = players[1];
                                        r.player1Hlas = (hlas == "1" ? (suit == trump ? 40 : 20) : 0);
                                        break;
                                    case 2: r.c2 = card;
                                        r.player2 = players[1];
                                        r.player2Hlas = (hlas == "1" ? (suit == trump ? 40 : 20) : 0);
                                        break;
                                    case 3: r.c3 = card;
                                        r.player3 = players[1];
                                        r.player3Hlas = (hlas == "1" ? (suit == trump ? 40 : 20) : 0);
                                        break;
                                }
                                cardsForPlayer++;
                            }
                        }
                        else if (reader.Name == "Hrac3" && processPlayers)
                        {
                            string strName = reader.GetAttribute("Jmeno");
                            players[2].Name = strName ?? "Hrac3";
                            if (cardsForRound == 0)
                            {
                                cardsForPlayer = 3;
                            }
                            else
                            {
                                //parsujeme stych
                                if (oldRounds.Count < cardsForRound)
                                {
                                    oldRounds.Add(new Round(g));
                                    cardsForPlayer = 1;
                                }
                                var r = oldRounds[oldRounds.Count - 1];
                                string strSuit = reader.GetAttribute("Barva");
                                string strValue = reader.GetAttribute("Hodnota");
                                var hlas = reader.GetAttribute("Hlas");
                                Barva suit = (Barva)Enum.Parse(typeof(Barva), strSuit, true);
                                Hodnota value = (Hodnota)Enum.Parse(typeof(Hodnota), strValue, true);
                                Card card = new Card(suit, value);

                                switch (cardsForPlayer)
                                {
                                    case 1: r.c1 = card;
                                        r.player1 = players[2];
                                        r.player1Hlas = (hlas == "1" ? (suit == trump ? 40 : 20) : 0);
                                        break;
                                    case 2: r.c2 = card;
                                        r.player2 = players[2];
                                        r.player2Hlas = (hlas == "1" ? (suit == trump ? 40 : 20) : 0);
                                        break;
                                    case 3: r.c3 = card;
                                        r.player3 = players[2];
                                        r.player3Hlas = (hlas == "1" ? (suit == trump ? 40 : 20) : 0);
                                        break;
                                }
                                cardsForPlayer++;
                            }
                        }
                        else if (reader.Name == "Karta")
                        {
                            string strSuit = reader.GetAttribute("Barva");
                            string strValue = reader.GetAttribute("Hodnota");
                            Barva suit = (Barva)Enum.Parse(typeof(Barva), strSuit, true);
                            Hodnota value = (Hodnota)Enum.Parse(typeof(Hodnota), strValue, true);
                            Card card = new Card(suit, value);

                            if (processTalon)
                            {
                                if (g.talon == null)
                                {
                                    g.talon = new List<Card>();
                                }
                                g.talon.Add(card);
                            }
                            else if (cardsForPlayer > 0 && cardsForPlayer <= 3)
                            {
                                if (players[0].Hand.Exists(item => item.Suit == card.Suit && item.Value == card.Value) ||
                                    players[1].Hand.Exists(item => item.Suit == card.Suit && item.Value == card.Value) ||
                                    players[2].Hand.Exists(item => item.Suit == card.Suit && item.Value == card.Value))
                                {
                                    throw new Exception(string.Format("Duplicitní karta: {0}", card));
                                }
                                players[cardsForPlayer - 1].Hand.Add(card);
                            }
                            else
                            {
                                throw new Exception("Neočekávaný element: Karta");
                            }
                        }
                        else if (reader.Name == "Stych")
                        {
                            var strRoundNum = reader.GetAttribute("Kolo");

                            cardsForRound = Convert.ToInt16(strRoundNum);
                        }
                        else if (reader.Name == "Talon")
                        {
                            processTalon = true;
                            processPlayers = false;
                        }
                        else if (reader.Name == "Stychy")
                        {
                            processPlayers = true;
                            processTalon = false;
                        }
                        else if (reader.Name == "Zuctovani")
                        {
                            processPlayers = false;
                            processTalon = false;
                            processPoints = true;
                        }
                    }
                }

                if (strTrump != null)
                {
                    trump = (Barva)Enum.Parse(typeof(Barva), strTrump, true);
                }
                else if (round != 0)
                {
                    throw new Exception("Není zvolen trumf");
                }

                if (strStarter == null || (strStarter != "Hrac1" && strStarter != "Hrac2" && strStarter != "Hrac3"))
                {
                    throw new Exception("Kdo má začínat?");
                }
                if (round == 0)
                {
                    if (players[StringToPlayerIndex(strGameStarter)].Hand.Count != 12 || 
                        players[(StringToPlayerIndex(strGameStarter) + 1) % NumPlayers].Hand.Count != 10 ||
                        players[(StringToPlayerIndex(strGameStarter) + 2) % NumPlayers].Hand.Count != 10)
                    {
                        throw new Exception("Počet karet nesedí");
                    }
                }
                else
                {
                    if (strGameStarter == null || (strGameStarter != "Hrac1" && strGameStarter != "Hrac2" && strGameStarter != "Hrac3"))
                    {
                        throw new Exception("Kdo volil trumfy?");
                    }
                    if ((players[0].Hand.Count != players[1].Hand.Count) || (players[0].Hand.Count != players[2].Hand.Count))
                    {
                        throw new Exception("Počet karet nesedí");
                    }
                    else if (players[0].Hand.Count + round - 1 != 10)
                    {
                        throw new Exception("Počet karet nesedí s číslem kola");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Nepodařilo se načíst hru. {0}", ex.Message));
            }

            //g.Init(new Player(strName1, g), new DummyPlayer(strName2, g), new DummyPlayer(strName3, g), hand[0], hand[1], hand[2], score, trump, round);
            //g.Init(new Player(strName1, g), new BrutePlayer(strName2, g), new BrutePlayer(strName3, g), hand[0], hand[1], hand[2], score, trump, round, gameStarterIndex, starterIndex);
            g.Init(players[0],
                   players[1],
                   players[2],
                   trump, round, StringToPlayerIndex(strGameStarter), StringToPlayerIndex(strStarter), oldRounds.ToArray());
            
            return g;
        }

        private static int StringToPlayerIndex(string str)
        {
            if (str == "Hrac1")
            {
                return 0;
            }
            else if (str == "Hrac2")
            {
                return 1;
            }
            else if (str == "Hrac3")
            {
                return 2;
            }

            return -1;
        }

        #endregion

        #region Events and Delegates

        public delegate void GameFinishedEventHandler(object sender, GameFinishedEventArgs e);       
        public event GameFinishedEventHandler GameFinished;
        protected virtual void OnGameFinished(GameFinishedEventArgs e)
        {
            if (GameFinished != null)
                GameFinished(this, e);
        }

        #endregion
    }
}
