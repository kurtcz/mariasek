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
                        GameMoneyWon = GameValue * _bidding.GameMultiplier;
                    }
                    else
                    {
                        GameMoneyWon = - GameValue * _bidding.GameMultiplier;
                    }
                    money += GameMoneyWon;
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
                        if (KilledSeven)
                        {
                            KilledSevenMoneyWon = -KilledSevenValue * _bidding.SevenMultiplier;
                        }
                    }
                    money += SevenMoneyWon + KilledSevenMoneyWon;
                }
                else if (SevenWon)
                {
                    QuietSevenMoneyWon = QuietSevenValue;
                    money += QuietSevenMoneyWon;
                }
                else if (KilledSeven)
                {
                    KilledSevenMoneyWon = -KilledSevenValue * _bidding.SevenMultiplier;
                    money += KilledSevenMoneyWon;
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
                        if (KilledSevenAgainst)
                        {
                            KilledSevenAgainstMoneyWon = KilledSevenValue * _bidding.SevenAgainstMultiplier;
                        }
                    }
                    money += SevenAgainstMoneyWon + KilledSevenAgainstMoneyWon;
                }
                else if (SevenAgainstWon)
                {
                    QuietSevenAgainstMoneyWon = -QuietSevenValue;
                    money += QuietSevenAgainstMoneyWon;
                }
                else if (KilledSevenAgainst)
                {
                    KilledSevenAgainstMoneyWon = -KilledSevenValue;
                    money += KilledSevenAgainstMoneyWon;
                }

                if ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 && KilledSeven)
                {
                    if (FinalCardWon)
                    {
                        KilledSevenMoneyWon = KilledSevenValue;
                    }
                    else
                    {
                        KilledSevenMoneyWon = - KilledSevenValue;
                    }
                    money += KilledSevenMoneyWon;
                }

                if ((_gameType & Hra.Kilo) != 0)
                {
                    if (HundredWon)
                    {
                        HundredMoneyWon = HundredValue * _bidding.GameMultiplier * (PointsWon - 90) / 10;
                    }
                    else
                    {
                        HundredMoneyWon = - HundredValue * _bidding.GameMultiplier * (100 - BasicPointsWon - MaxHlasWon) / 10;
                    }
                    money += HundredMoneyWon;
                }
                else
                {
                    if (QuietHundredWon)
                    {
                        QuietHundredMoneyWon = QuietHundredValue * _bidding.GameMultiplier * (PointsWon - 90) / 10;
                        money += QuietHundredMoneyWon;
                    }
                }

                if ((_gameType & Hra.KiloProti) != 0)
                {
                    if (HundredAgainstWon)
                    {
                        HundredAgainstMoneyWon = - HundredValue * _bidding.GameMultiplier * (BasicPointsLost - 90) / 10;
                    }
                    else
                    {
                        HundredAgainstMoneyWon = HundredValue * _bidding.GameMultiplier * (100 - BasicPointsLost - MaxHlasLost) / 10;
                    }
                    money += HundredAgainstMoneyWon;
                }
                else
                {
                    if (QuietHundredAgainstWon)
                    {
                        QuietHundredAgainstMoneyWon = - QuietHundredValue * _bidding.GameMultiplier * (PointsLost - 90) / 10;
                        money += QuietHundredAgainstMoneyWon;
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
                    BetlMoneyWon = BetlValue * _bidding.BetlDurchMultiplier * (BetlWon ? 1 : -1);
                    money += BetlMoneyWon;
                }
                else
                {
                    DurchMoneyWon = DurchValue * _bidding.BetlDurchMultiplier * (DurchWon ? 1 : -1);
                    money += DurchMoneyWon;
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

        enum Genre
        {
            Masculine,
            Feminine,
            Neutral
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var gt in Enum.GetValues(typeof(Hra)).Cast<Hra>().Where(i => (_gameType & i) != 0))
            {
                var won = false;
                var multiplier = 0;
                var genre = Genre.Masculine;
                var status = string.Empty;
                var other = string.Empty;
                var score = string.Empty;
                var money = 0;

                switch (gt)
                {
                    case Hra.Hra:
                        won = GameWon;
                        genre = Genre.Feminine;
                        multiplier = _bidding.GameMultiplier;
                        money = GameMoneyWon;
                        score = string.Format("Skóre: {0}:{1}\n", PointsWon, PointsLost);
                        break;
                    case Hra.Sedma:
                        won = SevenWon;
                        genre = Genre.Feminine;
                        multiplier = _bidding.SevenMultiplier;
                        money = SevenMoneyWon + KilledSevenMoneyWon;
                        other = KilledSeven ? " zabitá" : string.Empty;
                        break;
                    case Hra.Kilo:
                        won = HundredWon;
                        genre = Genre.Neutral;
                        multiplier = _bidding.GameMultiplier;
                        money = HundredMoneyWon;
                        score = string.Format("Skóre: {0}:{1}{2}\n", PointsWon, PointsLost, won ? string.Empty : string.Format("\nDo kila schází: {0} bodů", 100 - (BasicPointsWon + MaxHlasWon)));
                        break;
                    case Hra.SedmaProti:
                        won = SevenAgainstWon;
                        genre = Genre.Feminine;
                        multiplier = _bidding.SevenAgainstMultiplier;
                        money = SevenAgainstMoneyWon;
                        other = KilledSeven ? " zabitá" : string.Empty;
                        break;
                    case Hra.KiloProti:
                        won = HundredAgainstWon;
                        genre = Genre.Neutral;
                        multiplier = _bidding.HundredAgainstMultiplier;
                        money = HundredAgainstMoneyWon;
                        break;
                    case Hra.Betl:
                        won = BetlWon;
                        genre = Genre.Masculine;
                        multiplier = _bidding.BetlDurchMultiplier;
                        money = BetlMoneyWon;
                        break;
                    case Hra.Durch:
                        won = DurchWon;
                        genre = Genre.Masculine;
                        multiplier = _bidding.BetlDurchMultiplier;
                        money = DurchMoneyWon;
                        break;
                }
                switch(genre)
                {
                    case Genre.Masculine:
                        status = won ? "Vyhranej" : "Prohranej";
                        break;
                    case Genre.Feminine:
                        status = won ? "Vyhraná" : "Prohraná";
                        break;
                    case Genre.Neutral:
                        status = won ? "Vyhraný" : "Prohraný";
                        break;
                }

                sb.AppendFormat("{0} {1}{2}{3}\t{4}\n{5}", 
                    status, 
                    gt.ToString().ToLower(), 
                    multiplier > 1 ? string.Format(" ({0}x flek)", MultiplierToDoubleCount(multiplier)) : string.Empty, 
                    other, 
                    (money * BaseBet).ToString("C", _ci),
                    score);
            }
            if (QuietHundredWon)
            {
                sb.AppendFormat("Tichý kilo\t{0}\n", (QuietHundredMoneyWon * BaseBet).ToString("C", _ci));
            }
            if (QuietHundredAgainstWon)
            {
                sb.AppendFormat("Tichý kilo proti\t{0}\n", (QuietHundredAgainstMoneyWon * BaseBet).ToString("C", _ci));
            }
            if (QuietSevenWon)
            {
                sb.AppendFormat("Tichá sedma\t{0}\n", (QuietSevenMoneyWon * BaseBet).ToString("C", _ci));
            }
            if (KilledSeven && (_gameType & Hra.Sedma) == 0)
            {
                sb.AppendFormat("Tichá sedma zabitá\t{0}\n", (KilledSevenMoneyWon * BaseBet).ToString("C", _ci));
            }
            if (QuietSevenAgainstWon)
            {
                sb.AppendFormat("Tichá sedma proti\t{0}\n", (QuietSevenAgainstMoneyWon * BaseBet).ToString("C", _ci));
            }
            if (KilledSevenAgainst && (_gameType & Hra.SedmaProti) == 0)
            {
                sb.AppendFormat("Tichá sedma proti zabitá\t{0}\n", (KilledSevenAgainstMoneyWon * BaseBet).ToString("C", _ci));
            }
            if (_trump.HasValue && _trump.Value == Barva.Cerveny)
            {
                sb.AppendFormat("V červenejch:\t{0}\n", (MoneyWon[_gameStartingPlayerIndex] / 2 * BaseBet).ToString("C", _ci));
            }
            for (var i = 0; i < Game.NumPlayers; i++)
            {
                sb.AppendFormat("\n{0}:\t{1}",
                    PlayerNames[(_gameStartingPlayerIndex + i) % Game.NumPlayers], (MoneyWon[(_gameStartingPlayerIndex + i) % Game.NumPlayers] * BaseBet).ToString("C", _ci));
            }

            return sb.ToString();
        }

        private int MultiplierToDoubleCount(int multiplier)
        {
            for(var i = 0; multiplier != 0; i++, multiplier >>= 1)
            {
                if((multiplier & 1) != 0)
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
