using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
//using log4net;
using Mariasek.Engine.New.Logger;

namespace Mariasek.Engine.New
{
    public class Round
    {
#if !PORTABLE
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
#else
        private static readonly ILog _log = new DummyLogWrapper();
#endif
        private Game _g;

        public int number { get; private set; }
        public AbstractPlayer player1 { get; private set; }
        public AbstractPlayer player2 { get; private set; }
        public AbstractPlayer player3 { get; private set; }
        public AbstractPlayer roundWinner { get; private set; }

        public Card c1 { get; private set; }
        public Card c2 { get; private set; }
        public Card c3 { get; private set; }

        public bool hlas1 { get; private set; }
        public bool hlas2 { get; private set; }
        public bool hlas3 { get; private set; }

        public int points1 { get; private set; }
        public int points2 { get; private set; }
        public int points3 { get; private set; }

        public int basicPoints1 { get; private set; }   //bez hlasu
        public int basicPoints2 { get; private set; }
        public int basicPoints3 { get; private set; }

        public Round(Game g, AbstractPlayer roundStarter)
        {
            _g = g;
            number = _g.RoundNumber;
            
            player1 = roundStarter;
            player2 = _g.players[(player1.PlayerIndex + 1) % Game.NumPlayers];
            player3 = _g.players[(player2.PlayerIndex + 1) % Game.NumPlayers];
        }

        public Round(Game g, AbstractPlayer roundStarter, Card c1, Card c2, Card c3) : this(g, roundStarter)
        {
            player1 = roundStarter;
            player2 = _g.players[(roundStarter.PlayerIndex + 1) % Game.NumPlayers];
            player3 = _g.players[(roundStarter.PlayerIndex + 2) % Game.NumPlayers];

            this.c1 = c1;
            hlas1 = c1.Value == Hodnota.Svrsek && player1.Hand.HasK(c1.Suit);
            if (hlas1) player1.Hlasy++;
            player1.Hand.Remove(c1);
            _g.OnCardPlayed(this);
            
            this.c2 = c2;
            hlas2 = c2.Value == Hodnota.Svrsek && player2.Hand.HasK(c2.Suit);
            if (hlas2) player1.Hlasy++;
            player2.Hand.Remove(c2);
            _g.OnCardPlayed(this);

            this.c3 = c3;
            hlas3 = c3.Value == Hodnota.Svrsek && player3.Hand.HasK(c3.Suit);
            if (hlas3) player1.Hlasy++;
            player3.Hand.Remove(c3);
            _g.OnCardPlayed(this);

            CalculateRoundScore();
        }

        public AbstractPlayer PlayRound()
        {
            //musim nejak overit, ze karty jsou validni a pokud ne tak to hraci oznamit a akci opakovat
            c1 = player1.PlayCard(this);
            hlas1 = c1.Value == Hodnota.Svrsek && player1.Hand.HasK(c1.Suit);
            if (hlas1) player1.Hlasy++;
            player1.Hand.Remove(c1);
            _g.ThrowIfCancellationRequested();
            _g.OnCardPlayed(this);
            
            c2 = player2.PlayCard(this);
            hlas2 = c2.Value == Hodnota.Svrsek && player2.Hand.HasK(c2.Suit);
            if (hlas2) player2.Hlasy++;
            player2.Hand.Remove(c2);
            _g.ThrowIfCancellationRequested(); 
            _g.OnCardPlayed(this);
            
            c3 = player3.PlayCard(this);
            hlas3 = c3.Value == Hodnota.Svrsek && player3.Hand.HasK(c3.Suit);
            if (hlas3) player3.Hlasy++;
            player3.Hand.Remove(c3);
            _g.ThrowIfCancellationRequested(); 
            _g.OnCardPlayed(this);

            CalculateRoundScore();

            return roundWinner;
        }

        private void CalculateRoundScore()
        {
            var winningCard = Round.WinningCard(c1, c2, c3, _g.trump);

            if (winningCard == c1)
            {
                points1 = PointsWon;
                roundWinner = player1;
            }
            else if (winningCard == c2)
            {
                points2 = PointsWon;
                roundWinner = player2;
            }
            else
            {
                points3 = PointsWon;
                roundWinner = player3;
            }

            basicPoints1 = points1;
            basicPoints2 = points2;
            basicPoints3 = points3;

            points1 += hlas1 ? (c1.Suit == _g.trump.Value ? 40 : 20) : 0;
            points2 += hlas2 ? (c2.Suit == _g.trump.Value ? 40 : 20) : 0;
            points3 += hlas3 ? (c3.Suit == _g.trump.Value ? 40 : 20) : 0;
        }

        public static Card WinningCard(Card c1, Card c2, Card c3, Barva? trump)
        {
            if (c1.IsHigherThan(c2, trump))
            {
                if (c1.IsHigherThan(c3, trump))
                {
                    return c1;
                }
                else
                {
                    return c3;
                }
            }
            else
            {
                if (c2.IsHigherThan(c3, trump))
                {
                    return c2;
                }
                else
                {
                    return c3;
                }
            }
        }

        public int PointsWon { get { return ComputePointsWon(c1, c2, c3, number); } }

        public static int ComputePointsWon(Card c1, Card c2, Card c3, int roundNumber)
        {
            var points = 0;

            if (c1 != null && c2 != null && c3 != null)
            {
                if (c1.Value == Hodnota.Desitka || c1.Value == Hodnota.Eso)
                    points += 10;
                if (c2.Value == Hodnota.Desitka || c2.Value == Hodnota.Eso)
                    points += 10;
                if (c3.Value == Hodnota.Desitka || c3.Value == Hodnota.Eso)
                    points += 10;
            }

            if (roundNumber == Game.NumRounds)
                points += 10;
                
            return points;
        }
    }
}
