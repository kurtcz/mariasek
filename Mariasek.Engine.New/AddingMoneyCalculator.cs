using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
{
    public class AddingMoneyCalculator : MoneyCalculatorBase
    {
        //Default constructor for XmlSerialize purposes
        [Preserve]
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

        public AddingMoneyCalculator(Hra gameType, Barva? trump, int gameStartingPlayerIndex, Bidding bidding, IGameTypeValues values,
            bool calculate107Separately, HlasConsidered hlasConsidered, GameComputationResult res)
            : base(gameType, trump, gameStartingPlayerIndex, bidding, values, calculate107Separately, hlasConsidered, res)
        {
        }

        public override void CalculateMoney()
        {
            var money = 0;

            if (GoodGame)
            {
                if ((_gameType & Hra.Hra) != 0)
                {
                    if ((_gameType & Hra.KiloProti) != 0)
                    {
                        if (HundredAgainstWon)
                        {
                            HundredAgainstMoneyWon = -HundredValue * _bidding.HundredAgainstMultiplier * (PointsLost - 90) / 10;
                        }
                        else
                        {
                            var bodyProti = BasicPointsLost + MaxHlasLost;
                            var mojeHlasy = PointsWon - BasicPointsWon;

                            HundredAgainstMoneyWon = HundredValue * _bidding.HundredAgainstMultiplier * (100 + mojeHlasy - bodyProti) / 10;
                            if (HundredAgainstMoneyWon <= 0 &&
                                !Calculate107Separately)
                            {
                                HundredAgainstMoneyWon = -HundredValue * _bidding.HundredAgainstMultiplier;
                            }
                        }
                        money += HundredAgainstMoneyWon;
                    }
                    else
                    {
                        if (GameWon)
                        {
                            GameMoneyWon = GameValue * _bidding.GameMultiplier;
                            if (QuietHundredWon)
                            {
                                GameMoneyWon = 2 * GameMoneyWon * (PointsWon - 90) / 10;
                            }
                        }
                        else
                        {
                            GameMoneyWon = -GameValue * _bidding.GameMultiplier;
                            if (QuietHundredAgainstWon)
                            {
                                GameMoneyWon = 2 * GameMoneyWon * (PointsLost - 90) / 10;
                            }
                        }
                        money += GameMoneyWon;
                    }
                }

                if ((_gameType & Hra.Sedma) != 0)
                {
                    if (SevenWon)
                    {
                        SevenMoneyWon = SevenValue * _bidding.SevenMultiplier;
                    }
                    else
                    {
                        SevenMoneyWon = -SevenValue * _bidding.SevenMultiplier;
                        //if (KilledSeven)
                        //{
                        //    KilledSevenMoneyWon = -KilledSevenValue * _bidding.SevenMultiplier;
                        //}
                    }
                    money += SevenMoneyWon;// + KilledSevenMoneyWon;
                }
                else if (SevenWon)
                {
                    QuietSevenMoneyWon = QuietSevenValue;
                    money += QuietSevenMoneyWon;
                }
                else if (KilledSeven)
                {
                    //KilledSevenMoneyWon = -KilledSevenValue * _bidding.SevenMultiplier;
                    //money += KilledSevenMoneyWon;
                    QuietSevenMoneyWon = -QuietSevenValue;
                    money += QuietSevenMoneyWon;
                }

                if ((_gameType & Hra.SedmaProti) != 0)
                {
                    if (SevenAgainstWon)
                    {
                        SevenAgainstMoneyWon = -SevenValue * _bidding.SevenAgainstMultiplier;
                    }
                    else
                    {
                        SevenAgainstMoneyWon = SevenValue * _bidding.SevenAgainstMultiplier;
                        //if (KilledSevenAgainst)
                        //{
                        //    KilledSevenAgainstMoneyWon = KilledSevenValue * _bidding.SevenAgainstMultiplier;
                        //}
                    }
                    money += SevenAgainstMoneyWon;// + KilledSevenAgainstMoneyWon;
                }
                else if (SevenAgainstWon)
                {
                    QuietSevenAgainstMoneyWon = -QuietSevenValue;
                    money += QuietSevenAgainstMoneyWon;
                }
                else if (KilledSevenAgainst)
                {
                    //KilledSevenAgainstMoneyWon = KilledSevenValue;
                    //money += KilledSevenAgainstMoneyWon;
                    QuietSevenAgainstMoneyWon = QuietSevenValue;
                    money += QuietSevenAgainstMoneyWon;
                }

                //if ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 && KilledSeven)
                //{
                //    if (FinalCardWon)
                //    {
                //        KilledSevenMoneyWon = KilledSevenValue;
                //    }
                //    else
                //    {
                //        KilledSevenMoneyWon = -KilledSevenValue;
                //    }
                //    money += KilledSevenMoneyWon;
                //}

                if ((_gameType & Hra.Kilo) != 0)
                {
                    if (HundredWon)
                    {
                        HundredMoneyWon = HundredValue * _bidding.GameMultiplier * (PointsWon - 90) / 10;
                    }
                    else
                    {
                        var mojeBody = BasicPointsWon + MaxHlasWon;
                        var hlasyProti = PointsLost - BasicPointsLost;

                        HundredMoneyWon = -HundredValue * _bidding.GameMultiplier * (100 + hlasyProti - mojeBody) / 10;
                        if (HundredMoneyWon <= 0 &&
                            !Calculate107Separately)
                        {
                            HundredMoneyWon = -HundredValue * _bidding.GameMultiplier;
                        }
                    }
                    money += HundredMoneyWon;
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
                    BetlMoneyWon = BetlValue * _bidding.BetlDurchMultiplier * (BetlWon ? 1 : -1);
                    money += BetlMoneyWon;
                }
                else
                {
                    DurchMoneyWon = DurchValue * _bidding.BetlDurchMultiplier * (DurchWon ? 1 : -1);
                    money += DurchMoneyWon;
                }
            }

			if (MaxWin > 0)
            {
                if (money < -MaxWin)
                {
                    money = -MaxWin;
                }
                if (money > MaxWin)
                {
                    money = MaxWin;
                }
            }
            for (var i = 0; i < Game.NumPlayers; i++)
            {
                if (i == _gameStartingPlayerIndex)
                {
                    MoneyWon[i] = 2 * money;
                }
                else
                {
                    MoneyWon[i] = -money;
                }
            }
        }
    }
}
