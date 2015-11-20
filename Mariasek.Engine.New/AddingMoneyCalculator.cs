using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
{
    public class AddingMoneyCalculator : MoneyCalculatorBase
    {
        private const int GameValue = 1;
        private const int SevenValue = 2;
        private const int QuietSevenValue = 1;
        private const int KilledSevenValue = 1;
        private const int HundredValue = 4;
        private const int QuietHundredValue = 2;
        private const int BetlValue = 5;
        private const int DurchValue = 10;

        //Default constructor for XmlSerialize purposes
        public AddingMoneyCalculator()
        {
        }

        public AddingMoneyCalculator(Game g)
            : base(g)
        {
        }

        public AddingMoneyCalculator(Game g, Bidding bidding, GameComputationResult res)
            : base(g, bidding, res)
        {            
        }

        public AddingMoneyCalculator(Hra gameType, Barva? trump, int gameStartingPlayerIndex, Bidding bidding, GameComputationResult res)
            :base(gameType, trump, gameStartingPlayerIndex, bidding, res)
        {
        }

        public override void CalculateMoney()
        {
            var money = 0;

            if (GoodGame)
            {
                if ((_gameType & Hra.Hra) != 0)
                {
                    if (GameWon)
                    {
                        money += GameValue * _bidding.GameMultiplier;
                    }
                    else
                    {
                        money -= GameValue * _bidding.GameMultiplier;
                    }
                }

                if ((_gameType & Hra.Sedma) != 0)
                {
                    if (SevenWon)
                    {
                        money += SevenValue * _bidding.SevenMultiplier;
                    }
                    else
                    {
                        money -= SevenValue * _bidding.SevenMultiplier;
                        if (KilledSeven)
                        {
                            money -= KilledSevenValue * _bidding.SevenMultiplier;
                        }
                    }
                }
                else if (SevenWon)
                {
                    money += QuietSevenValue;
                }

                if ((_gameType & Hra.SedmaProti) != 0)
                {
                    if (SevenAgainstWon)
                    {
                        money -= SevenValue * _bidding.SevenAgainstMultiplier;
                    }
                    else
                    {
                        money += SevenValue * _bidding.SevenAgainstMultiplier;
                        if (KilledSeven)
                        {
                            money += KilledSevenValue * _bidding.SevenAgainstMultiplier;
                        }
                    }
                }
                else if (SevenAgainstWon)
                {
                    money -= QuietSevenValue;
                }

                if ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 && KilledSeven)
                {
                    if (FinalCardWon)
                    {
                        money += KilledSevenValue;
                    }
                    else
                    {
                        money -= KilledSevenValue;
                    }
                }

                if ((_gameType & Hra.Kilo) != 0)
                {
                    if (HundredWon)
                    {
                        money += HundredValue * _bidding.GameMultiplier * (PointsWon - 90) / 10;
                    }
                    else
                    {
                        money -= HundredValue * _bidding.GameMultiplier * (100 - BasicPointsWon - MaxHlasWon) / 10;
                    }
                }
                else
                {
                    if (QuietHundredWon)
                    {
                        money += QuietHundredValue * _bidding.GameMultiplier * (PointsWon - 90) / 10;
                    }
                }

                if ((_gameType & Hra.KiloProti) != 0)
                {
                    if (HundredAgainstWon)
                    {
                        money -= HundredValue * _bidding.GameMultiplier * (BasicPointsLost - 90) / 10;
                    }
                    else
                    {
                        money += HundredValue * _bidding.GameMultiplier * (100 - BasicPointsLost - MaxHlasLost) / 100;
                    }
                }
                else
                {
                    if (QuietHundredAgainstWon)
                    {
                        money -= QuietHundredValue * _bidding.GameMultiplier * (PointsLost - 90) / 100;
                    }
                }

                if (_trump == Barva.Cerveny)
                {
                    money *= 2;
                }
            }
            else
            {
                if ((_gameType & Hra.Betl) != 0)
                {
                    money = BetlValue * _bidding.BetlDurchMultiplier * (BetlWon ? 1 : -1);
                }
                else
                {
                    money = DurchValue * _bidding.BetlDurchMultiplier * (DurchWon ? 1 : -1);
                }
            }

            for (var i = 0; i < Game.NumPlayers; i++)
            {
                if (i == _gameStartingPlayerIndex)
                {
                    MoneyWon[i] = 2*money;
                }
                else
                {
                    MoneyWon[i] = -money;
                }
            }
        }
    }
}
