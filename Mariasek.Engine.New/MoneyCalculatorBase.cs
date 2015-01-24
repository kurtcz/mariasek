using System;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
{
    [XmlInclude(typeof(AddingMoneyCalculator))]
    [XmlRoot(ElementName="Money")]
    public abstract class MoneyCalculatorBase
    {
        protected Hra _gameType;
        protected Barva? _trump;
        protected Bidding _bidding;
        protected int _gameStartingPlayerIndex;

        public bool GoodGame
        {
            get { return (_gameType & Hra.Betl) == 0 && (_gameType & Hra.Durch) == 0; }
        }

        public int PointsWon { get; private set; }
        public int PointsLost { get; private set; }
        public int BasicPointsWon { get; private set; }
        public int BasicPointsLost { get; private set; }
        public int MaxHlasWon { get; private set; }
        public int MaxHlasLost { get; private set; }

        public bool FinalCardWon { get; private set; }

        public bool GameWon { get; private set; }
        public bool SevenWon { get; private set; }
        public bool SevenAgainstWon { get; private set; }
        public bool KilledSeven { get; private set; }
        public bool HundredWon { get; private set; }
        public bool QuietHundredWon { get; private set; }
        public bool HundredAgainstWon { get; private set; }
        public bool QuietHundredAgainstWon { get; private set; }
        public bool BetlWon { get; private set; }
        public bool DurchWon { get; private set; }

        [XmlArray]
        public int[] MoneyWon { get; protected set; }

        //Default constructor for XmlSerialize purposes
        public MoneyCalculatorBase()
        {
        }

        protected MoneyCalculatorBase(Game g)
        {
            _gameType = g.GameType;
            _trump = g.trump;
            _bidding = g.Bidding;
            _gameStartingPlayerIndex = g.GameStartingPlayerIndex;
            
            if (GoodGame)
            {
                var score = new int[Game.NumPlayers];
                var basicScore = new int[Game.NumPlayers];
                var maxHlasScore = new int[Game.NumPlayers];

                foreach (var r in g.rounds)
                {
                    score[r.player1.PlayerIndex] += r.points1;
                    score[r.player2.PlayerIndex] += r.points2;
                    score[r.player3.PlayerIndex] += r.points3;

                    basicScore[r.player1.PlayerIndex] += r.basicPoints1;
                    basicScore[r.player2.PlayerIndex] += r.basicPoints1;
                    basicScore[r.player3.PlayerIndex] += r.basicPoints1;

                    if(score[r.player1.PlayerIndex] - basicScore[r.player1.PlayerIndex] > maxHlasScore[r.player1.PlayerIndex])
                    {
                        maxHlasScore[r.player1.PlayerIndex] = score[r.player1.PlayerIndex] - basicScore[r.player1.PlayerIndex];
                    }
                    if (score[r.player2.PlayerIndex] - basicScore[r.player2.PlayerIndex] > maxHlasScore[r.player2.PlayerIndex])
                    {
                        maxHlasScore[r.player2.PlayerIndex] = score[r.player2.PlayerIndex] - basicScore[r.player2.PlayerIndex];
                    }
                    if (score[r.player3.PlayerIndex] - basicScore[r.player3.PlayerIndex] > maxHlasScore[r.player3.PlayerIndex])
                    {
                        maxHlasScore[r.player3.PlayerIndex] = score[r.player3.PlayerIndex] - basicScore[r.player3.PlayerIndex];
                    }
                }

                PointsWon = score[_gameStartingPlayerIndex];
                PointsLost = score[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] + score[(_gameStartingPlayerIndex + 2) % Game.NumPlayers];

                BasicPointsWon = basicScore[_gameStartingPlayerIndex];
                BasicPointsLost = basicScore[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] + basicScore[(_gameStartingPlayerIndex + 2) % Game.NumPlayers];

                MaxHlasWon = maxHlasScore[_gameStartingPlayerIndex];
                MaxHlasLost = maxHlasScore[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] + maxHlasScore[(_gameStartingPlayerIndex + 2) % Game.NumPlayers];

                GameWon = PointsWon > PointsLost;

                var finalRound = g.rounds[Game.NumRounds - 1];
                var lastWinningCard = Round.WinningCard(finalRound.c1, finalRound.c2, finalRound.c3, g.trump);

                FinalCardWon = finalRound.roundWinner == g.GameStartingPlayer;

                SevenWon = FinalCardWon &&
                           lastWinningCard.Suit == g.trump &&
                           lastWinningCard.Value == Hodnota.Sedma;

                SevenAgainstWon = !FinalCardWon &&
                                  lastWinningCard.Suit == g.trump &&
                                  lastWinningCard.Value == Hodnota.Sedma;

                var gameStarterLastCard = GetGameStarterLastCard(finalRound, g.GameStartingPlayer);

                KilledSeven = finalRound.roundWinner != g.GameStartingPlayer &&
                              gameStarterLastCard.Suit == g.trump &&
                              gameStarterLastCard.Value == Hodnota.Sedma;

                QuietHundredWon = PointsWon >= 100;
                QuietHundredAgainstWon = PointsLost >= 100;

                HundredWon = BasicPointsWon + MaxHlasWon >= 100;
                HundredAgainstWon = BasicPointsLost + MaxHlasLost >= 100;
            }
            else //bad game
            {
                throw new NotImplementedException();
            }
            MoneyWon = new int[Game.NumPlayers];
        }

        protected MoneyCalculatorBase(Game g, Bidding bidding, GameComputationResult res)
        {
            _gameType = g.GameType;
            _trump = g.trump;
            _bidding = bidding;

            if (GoodGame)
            {
                PointsWon = res.Score[_gameStartingPlayerIndex];
                PointsLost = res.Score[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] + res.Score[(_gameStartingPlayerIndex + 2) % Game.NumPlayers];

                SevenWon = res.Final7Won.HasValue && res.Final7Won.Value;

                //Not implemented:
                //SevenAgainstWon
                //KilledSeven

                QuietHundredWon = PointsWon >= 100;
                QuietHundredAgainstWon = PointsLost >= 100;

                HundredWon = BasicPointsWon + MaxHlasWon >= 100;
                HundredAgainstWon = BasicPointsLost + MaxHlasLost >= 100;
            }
            else
            {
                throw new NotImplementedException();
            }
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
