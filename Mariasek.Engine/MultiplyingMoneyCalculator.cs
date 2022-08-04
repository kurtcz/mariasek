using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine
{
    public class MultiplyingMoneyCalculator : MoneyCalculatorBase
    {
        //Default constructor for XmlSerialize purposes
        [Preserve]
        public MultiplyingMoneyCalculator()
        {
        }

        //public MultiplyingMoneyCalculator(NumberFormatInfo nfi)
        //{
        //    _nfi = nfi;
        //}

        public MultiplyingMoneyCalculator(Game g, NumberFormatInfo nfi)
            : base(g)
        {
            _nfi = nfi;
        }

        public MultiplyingMoneyCalculator(Game g, Bidding bidding, GameComputationResult res)
            : base(g, bidding, res)
        {            
        }

        public MultiplyingMoneyCalculator(Hra gameType, Barva? trump, int gameStartingPlayerIndex, Bidding bidding, IGameTypeValues values,
            bool calculate107Separately, HlasConsidered hlasConsidered, bool countHlasAgainst, GameComputationResult res)
            :base(gameType, trump, gameStartingPlayerIndex, bidding, values, calculate107Separately, hlasConsidered, countHlasAgainst, res)
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
                            HundredAgainstMoneyWon = -HundredValue * _bidding.HundredAgainstMultiplier * (1 << (PointsLost - 100) / 10);
                        }
                        else
                        {
                            var bodyProti = BasicPointsLost + MaxHlasLost;
                            var mojeHlasy = PointsWon - BasicPointsWon;

                            //pokud se pocita stosedm dohromady a neuhral jsem sedmu, ale uhral kilo, tak pocitej kilo jako tesne prohrane
                            if (bodyProti >= 100 &&
                                !SevenAgainstWon &&
                                !Calculate107Separately)
                            {
                                HundredAgainstMoneyWon = -HundredValue * _bidding.HundredAgainstMultiplier;
                            }
                            else
                            {
                                HundredAgainstMoneyWon = HundredValue * _bidding.HundredAgainstMultiplier * (1 << (90 + mojeHlasy - bodyProti) / 10);
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
                                GameMoneyWon = QuietHundredValue * _bidding.GameMultiplier * (1 <<  (PointsWon - 100) / 10);
                           }
                        }
                        else
                        {
                            GameMoneyWon = -GameValue * _bidding.GameMultiplier;
                            if (QuietHundredAgainstWon)
                            {
                                GameMoneyWon = - QuietHundredValue * _bidding.GameMultiplier * (1 << (PointsLost - 100) / 10);
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
                //        KilledSevenMoneyWon = - KilledSevenValue;
                //    }
                //    money += KilledSevenMoneyWon;
                //}

                if ((_gameType & Hra.Kilo) != 0)
                {
                    if (HundredWon)
                    {
                        HundredMoneyWon = HundredValue * _bidding.GameMultiplier * (1 << (PointsWon - 100) / 10);
                    }
                    else
                    {
                        var mojeBody = BasicPointsWon + MaxHlasWon;
                        var hlasyProti = CountHlasAgainst ? PointsLost - BasicPointsLost : 0;

                        //pokud se pocita stosedm dohromady a neuhral jsem sedmu, ale uhral kilo, tak pocitej kilo jako tesne prohrane
                        if (mojeBody >= 100 &&
                            !SevenWon &&
                            !Calculate107Separately)
                        {
                            HundredMoneyWon = -HundredValue * _bidding.GameMultiplier;
                        }
                        else
                        {
                            HundredMoneyWon = - HundredValue * _bidding.GameMultiplier * (1 << (90 + hlasyProti - mojeBody) / 10);
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
