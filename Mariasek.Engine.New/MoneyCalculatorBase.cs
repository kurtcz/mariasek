using System;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace Mariasek.Engine.New
{
    public enum CalculationStyle
    {
        Adding,
        Multiplying
    }

    [XmlInclude(typeof(AddingMoneyCalculator))]
    [XmlInclude(typeof(MultiplyingMoneyCalculator))]
    [XmlRoot(ElementName = "Money")]
    public abstract class MoneyCalculatorBase
    {
        protected Hra _gameType;
        protected Barva? _trump;
        protected Bidding _bidding;
        protected int _gameStartingPlayerIndex;

        protected string[] PlayerNames;

        public bool GoodGame
        {
            get { return (_gameType & Hra.Betl) == 0 && (_gameType & Hra.Durch) == 0; }
        }

        public bool GamePlayed { get; private set; }
        public float BaseBet { get; set; }
        public Hra GameType { get { return _gameType; } }
        public string GameTypeString { get; set; }
        public int PointsWon { get; private set; }
        public int PointsLost { get; private set; }
        public int BasicPointsWon { get; private set; }
        public int BasicPointsLost { get; private set; }
        public int MaxHlasWon { get; private set; }
        public int MaxHlasLost { get; private set; }

        public bool FinalCardWon { get; private set; }

        public bool GameWon { get; private set; }
        public bool SevenWon { get; private set; }
        public bool QuietSevenWon { get; private set; }
        public bool SevenAgainstWon { get; private set; }
        public bool QuietSevenAgainstWon { get; private set; }
        public bool KilledSeven { get; private set; }
        public bool KilledSevenAgainst { get; private set; }
        public bool HundredWon { get; private set; }
        public bool QuietHundredWon { get; private set; }
        public bool HundredAgainstWon { get; private set; }
        public bool QuietHundredAgainstWon { get; private set; }
        public bool BetlWon { get; private set; }
        public bool DurchWon { get; private set; }

        public int GameMoneyWon { get; protected set; }
        public int SevenMoneyWon { get; protected set; }
        public int QuietSevenMoneyWon { get; protected set; }
        public int SevenAgainstMoneyWon { get; protected set; }
        public int QuietSevenAgainstMoneyWon { get; protected set; }
        public int KilledSevenMoneyWon { get; protected set; }
        public int KilledSevenAgainstMoneyWon { get; protected set; }
        public int HundredMoneyWon { get; protected set; }
        public int QuietHundredMoneyWon { get; protected set; }
        public int HundredAgainstMoneyWon { get; protected set; }
        public int QuietHundredAgainstMoneyWon { get; protected set; }
        public int BetlMoneyWon { get; protected set; }
        public int DurchMoneyWon { get; protected set; }

        [XmlArray]
        public int[] MoneyWon { get; protected set; }
        [XmlElement]
        public float SimulatedSuccessRate { get; set; } //set from MainScene

        protected CultureInfo _ci;

        //Default constructor for XmlSerialize purposes
        public MoneyCalculatorBase()
        {
            PlayerNames = new[] { "Hráč1", "Hráč2", "Hráč3"};
            BaseBet = 1f;
            _ci = new CultureInfo("cs-CZ");
            SimulatedSuccessRate = -1;
        }

        //vola se na konci hry
        protected MoneyCalculatorBase(Game g, CultureInfo ci = null)
        {
            _gameType = g.GameType;
            _trump = g.trump;
            GameTypeString = _gameType.ToDescription(null);
            _bidding = g.Bidding;
            _gameStartingPlayerIndex = g.GameStartingPlayerIndex;
            PlayerNames = g.players.Select(i => i.Name).ToArray();
            BaseBet = g.BaseBet;
            GamePlayed = g.rounds[0] != null;
            SimulatedSuccessRate = -1;

            if(ci == null)
            {
                _ci = new CultureInfo("cs-CZ");
            }
            if (GoodGame)
            {
                var score = new int[Game.NumPlayers];
                var basicScore = new int[Game.NumPlayers];
                var maxHlasScore = new int[Game.NumPlayers];

                foreach (var r in g.rounds.Where(i => i != null))
                {
                    score[r.player1.PlayerIndex] += r.points1;
                    score[r.player2.PlayerIndex] += r.points2;
                    score[r.player3.PlayerIndex] += r.points3;

                    basicScore[r.player1.PlayerIndex] += r.basicPoints1;
                    basicScore[r.player2.PlayerIndex] += r.basicPoints2;
                    basicScore[r.player3.PlayerIndex] += r.basicPoints3;

                    if(r.hlas1)
                    {
                        if(r.hlasPoints1 > maxHlasScore[r.player1.PlayerIndex])
                        {
                            maxHlasScore[r.player1.PlayerIndex] = r.hlasPoints1;
                        }
                    }
                    if (r.hlas2)
                    {
                        if (r.hlasPoints2 > maxHlasScore[r.player2.PlayerIndex])
                        {
                            maxHlasScore[r.player2.PlayerIndex] = r.hlasPoints2;
                        }
                    }
                    if (r.hlas3)
                    {
                        if (r.hlasPoints3 > maxHlasScore[r.player3.PlayerIndex])
                        {
                            maxHlasScore[r.player3.PlayerIndex] = r.hlasPoints3;
                        }
                    }
                }

                PointsWon = score[_gameStartingPlayerIndex];
                PointsLost = score[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] + score[(_gameStartingPlayerIndex + 2) % Game.NumPlayers];

                BasicPointsWon = basicScore[_gameStartingPlayerIndex];
                BasicPointsLost = basicScore[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] + basicScore[(_gameStartingPlayerIndex + 2) % Game.NumPlayers];

                MaxHlasWon = maxHlasScore[_gameStartingPlayerIndex];
                MaxHlasLost = Math.Max(maxHlasScore[(_gameStartingPlayerIndex + 1) % Game.NumPlayers], maxHlasScore[(_gameStartingPlayerIndex + 2) % Game.NumPlayers]);

                var finalRound = g.rounds[Game.NumRounds - 1];
                if (finalRound != null) //hra se hrala
                {
                    var lastWinningCard = Round.WinningCard(finalRound.c1, finalRound.c2, finalRound.c3, g.trump);

                    GameWon = PointsWon > PointsLost;
                    FinalCardWon = finalRound.roundWinner == g.GameStartingPlayer;

                    SevenWon = FinalCardWon &&
                    lastWinningCard.Suit == g.trump.Value &&
                    lastWinningCard.Value == Hodnota.Sedma;
                    QuietSevenWon = SevenWon && (_gameType & Hra.Sedma) == 0;
                    
                    SevenAgainstWon = !FinalCardWon &&
                    lastWinningCard.Suit == g.trump.Value &&
                    lastWinningCard.Value == Hodnota.Sedma;
                    QuietSevenAgainstWon = SevenAgainstWon && (_gameType & Hra.SedmaProti) == 0;

                    var gameStarterLastCard = GetGameStarterLastCard(finalRound, g.GameStartingPlayer);
                    var killedSeven = GetKilledSeven(finalRound);

                    KilledSeven = killedSeven &&
                        gameStarterLastCard.Suit == g.trump.Value &&
                        gameStarterLastCard.Value == Hodnota.Sedma;

                    KilledSevenAgainst = killedSeven &&
                        (gameStarterLastCard.Suit != g.trump.Value ||
                            gameStarterLastCard.Value != Hodnota.Sedma);
                }
                else //hra se nehrala
                {
                    GameWon = (g.GameType & Hra.Sedma) == 0; //neflekovana hra se bere jako vyhrana, flekovana hra pri sedme se bere jako prohrana
                    SevenWon = (g.GameType & Hra.Sedma) != 0;
                    QuietSevenWon = false;
                    SevenAgainstWon = false;
                    QuietSevenAgainstWon = false;
                    KilledSeven = false;
                    KilledSevenAgainst = false;
                }
                    
                QuietHundredWon = PointsWon >= 100 && (_gameType & Hra.Kilo) == 0;
                QuietHundredAgainstWon = PointsLost >= 100 && (_gameType & Hra.KiloProti) == 0;

                HundredWon = BasicPointsWon + MaxHlasWon >= 100;
                HundredAgainstWon = BasicPointsLost + MaxHlasLost >= 100;
            }
            else //bad game
            {
                foreach(var r in g.rounds)
                {
                    if (r == null)
                    { 
                        break; 
                    }
                    if(g.GameType == Hra.Betl)
                    {
                        BetlWon = r.roundWinner != g.GameStartingPlayer;
                    }
                    else
                    {
                        DurchWon = r.roundWinner == g.GameStartingPlayer;
                    }
                }
            }
            MoneyWon = new int[Game.NumPlayers];
        }

        private bool GetKilledSeven(Round finalRound)
        {
            if (!_trump.HasValue)
            {
                return false;
            }

            var winningCard = Round.WinningCard(finalRound.c1, finalRound.c2, finalRound.c3, _trump);
            var finalCards = new [] { finalRound.c1, finalRound.c2, finalRound.c3 };

            return winningCard.Value != Hodnota.Sedma && finalCards.Any(i => i.Suit == _trump.Value && i.Value == Hodnota.Sedma);
        }

        protected MoneyCalculatorBase(Game g, Bidding bidding, GameComputationResult res)
            :this(g.GameType, g.trump, g.GameStartingPlayerIndex, bidding, res)
        {
        }

        //vola se na konci simulace
        protected MoneyCalculatorBase(Hra gameType, Barva? trump, int gameStartingPlayerIndex, Bidding bidding, GameComputationResult res)
        {
            _gameType = gameType;
            _trump = trump;
            GameTypeString = _gameType.ToDescription(null);
            _bidding = bidding;
            _gameStartingPlayerIndex = gameStartingPlayerIndex;
            SimulatedSuccessRate = -1;

            if (GoodGame)
            {
                PointsWon = res.Score[_gameStartingPlayerIndex];
                PointsLost = res.Score[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] + res.Score[(_gameStartingPlayerIndex + 2) % Game.NumPlayers];
                BasicPointsWon = res.BasicScore[_gameStartingPlayerIndex];
                BasicPointsLost = res.BasicScore[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] + res.BasicScore[(_gameStartingPlayerIndex + 2) % Game.NumPlayers];
                MaxHlasWon = res.MaxHlasScore[_gameStartingPlayerIndex];
                MaxHlasLost = res.MaxHlasScore[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] + res.MaxHlasScore[(_gameStartingPlayerIndex + 2) % Game.NumPlayers];
                GameWon = PointsWon > PointsLost;
                SevenWon = res.Final7Won.HasValue && res.Final7Won.Value;

                var finalRound = res.Rounds[Game.NumRounds - 1];
                var lastWinningCard = Round.WinningCard(finalRound.c1, finalRound.c2, finalRound.c3, trump);

                SevenAgainstWon = !FinalCardWon &&
                                  lastWinningCard.Suit == trump.Value &&
                                  lastWinningCard.Value == Hodnota.Sedma;
                //Not implemented:
                //KilledSeven

                QuietHundredWon = PointsWon >= 100 && (_gameType & Hra.Kilo) == 0;
                QuietHundredAgainstWon = PointsLost >= 100;

                HundredWon = BasicPointsWon + MaxHlasWon >= 100;
                HundredAgainstWon = BasicPointsLost + MaxHlasLost >= 100;
            }
            else
            {
                DurchWon = res.Rounds.All(i => i.RoundWinnerIndex == _gameStartingPlayerIndex);
                BetlWon = res.Rounds.All(i => i.RoundWinnerIndex != _gameStartingPlayerIndex);
            }
            MoneyWon = new int[Game.NumPlayers];
        }

        private Card GetGameStarterLastCard(Round finalRound, AbstractPlayer gameStartingPlayer)
        {
            Card gameStarterLastCard;

            if (finalRound.player1 == gameStartingPlayer)
            {
                gameStarterLastCard = finalRound.c1;
            }
            else if (finalRound.player2 == gameStartingPlayer)
            {
                gameStarterLastCard = finalRound.c2;
            }
            else
            {
                gameStarterLastCard = finalRound.c3;
            }

            return gameStarterLastCard;
        }

        public abstract void CalculateMoney();
    }
}
