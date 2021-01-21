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

        public string debugNote1 { get; private set; }
        public string debugNote2 { get; private set; }
        public string debugNote3 { get; private set; }

        public int hlasPoints1 { get; private set; }
        public int hlasPoints2 { get; private set; }
        public int hlasPoints3 { get; private set; }

        public bool hlas1 { get { return hlasPoints1 != 0; } }
        public bool hlas2 { get { return hlasPoints2 != 0; } }
        public bool hlas3 { get { return hlasPoints3 != 0; } }

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

        public Round(Game g, AbstractPlayer roundStarter, Card c1, Card c2, Card c3, int roundNumber) : this(g, roundStarter, c1, c2, c3, roundNumber, string.Empty, string.Empty, string.Empty)
        {            
        }

        public Round(AbstractPlayer[] players, Barva? trump, int roundStarterIndex, Card c1, Card c2, Card c3, int roundNumber)
        {
            this.player1 = players[roundStarterIndex];
            this.player2 = players[(roundStarterIndex + 1) % Game.NumPlayers];
            this.player3 = players[(roundStarterIndex + 2) % Game.NumPlayers];

            this.c1 = c1;
            this.c2 = c2;
            this.c3 = c3;
            number = roundNumber;

            if (trump.HasValue && hlas1)
            {
                hlasPoints1 += c1.Suit == trump ? 40 : 20;
            }
            if (trump.HasValue && hlas2)
            {
                hlasPoints2 += c2.Suit == trump ? 40 : 20;
            }
            if (trump.HasValue && hlas3)
            {
                hlasPoints3 += c3.Suit == trump ? 40 : 20;
            }

            CalculateRoundScore(trump);
        }

        public Round(Game g, AbstractPlayer roundStarter, Card c1, Card c2, Card c3, int roundNumber, bool hlas1, bool hlas2, bool hlas3) : this(g, roundStarter)
        {
            this.c1 = c1;
            this.c2 = c2;
            this.c3 = c3;
            number = roundNumber;

            if (_g.trump.HasValue && hlas1)
            {
                hlasPoints1 += c1.Suit == _g.trump ? 40 : 20;
            }
            if (_g.trump.HasValue && hlas2)
            {
                hlasPoints2 += c2.Suit == _g.trump ? 40 : 20;
            }
            if (_g.trump.HasValue && hlas3)
            {
                hlasPoints3 += c3.Suit == _g.trump ? 40 : 20;
            }

            CalculateRoundScore();
        }

        public Round(Game g, AbstractPlayer roundStarter, Card c1, Card c2, Card c3, int roundNumber, string debugNote1, string debugNote2, string debugNote3) : this(g, roundStarter)
        {
            number = roundNumber;
            player1 = roundStarter;
            player2 = _g.players[(roundStarter.PlayerIndex + 1) % Game.NumPlayers];
            player3 = _g.players[(roundStarter.PlayerIndex + 2) % Game.NumPlayers];

            this.c1 = c1;
            this.debugNote1 = debugNote1;
            if(_g.trump.HasValue && c1.Value == Hodnota.Svrsek && player1.Hand.HasK(c1.Suit))
            {
                hlasPoints1 = c1.Suit == _g.trump.Value ? 40 : 20;
            }
            if (hlas1) player1.Hlasy++;
            player1.Hand.Remove(c1);
            _g.OnCardPlayed(this);
            
            this.c2 = c2;
            this.debugNote2 = debugNote2;
            if (_g.trump.HasValue && c2.Value == Hodnota.Svrsek && player2.Hand.HasK(c2.Suit))
            {
                hlasPoints2 = c2.Suit == _g.trump.Value ? 40 : 20;
            }
            if (hlas2) player2.Hlasy++;
            player2.Hand.Remove(c2);
            _g.OnCardPlayed(this);

            this.c3 = c3;
            this.debugNote3 = debugNote3;
            if (_g.trump.HasValue && c3.Value == Hodnota.Svrsek && player3.Hand.HasK(c3.Suit))
            {
                hlasPoints3 = c3.Suit == _g.trump.Value ? 40 : 20;
            }
            if (hlas3) player3.Hlasy++;
            player3.Hand.Remove(c3);
            _g.OnCardPlayed(this);

            CalculateRoundScore();
        }

        public AbstractPlayer PlayRound()
        {
            var allRules = new StringBuilder();

            //musim nejak overit, ze karty jsou validni a pokud ne tak to hraci oznamit a akci opakovat
            c1 = player1.PlayCard(this);
            if (c1 == null)
            {
                throw new InvalidOperationException("Card c1 cannot be null");
            }
            player1.Hand.Remove(c1);
            _g.DebugString.AppendFormat("Player{0}: {1}\n", player1.PlayerIndex + 1, c1);
            if (_g.rounds.Where(i => i != null && i.number < number).SelectMany(i => new[] { i.c1, i.c2, i.c3 }).Contains(c1))
            {
                throw new InvalidOperationException($"Card {c1} has already been played");
            }
            if (player1.DebugInfo.AllChoices.Count() > 1)
            {
                foreach (var choice in player1.DebugInfo.AllChoices)
                {
                    allRules.AppendFormat("\n{0} ({1}/{2})", choice.Card, choice.RuleCount, player1.DebugInfo.TotalRuleCount);
                }
                debugNote1 = string.Format("{0}: {1}\nVšechny simulace:{2}", player1.DebugInfo.Card, player1.DebugInfo.Rule, allRules.ToString());
            }
            else
            {
                debugNote1 = $"{player1.DebugInfo.Card}: {player1.DebugInfo.Rule}";
            }

            if (_g.trump.HasValue && c1.Value == Hodnota.Svrsek && player1.Hand.HasK(c1.Suit))
            {
                hlasPoints1 = c1.Suit == _g.trump.Value ? 40 : 20;
            }
            if (hlas1) player1.Hlasy++;
            _g.ThrowIfCancellationRequested();
            _g.OnCardPlayed(this);
            
            c2 = player2.PlayCard(this);
            if (c2 == null)
            {
                throw new InvalidOperationException("Card c2 cannot be null");
            }
            player2.Hand.Remove(c2);
            _g.DebugString.AppendFormat("Player{0}: {1}\n", player2.PlayerIndex + 1, c2);
            if (_g.rounds.Where(i => i != null && i.number < number).SelectMany(i => new[] { i.c1, i.c2, i.c3 }).Contains(c2))
			{
				throw new InvalidOperationException($"Card {c2} has already been played");
			}
            if (player2.DebugInfo.AllChoices.Count() > 1)
            {
                allRules.Clear();
                foreach (var choice in player2.DebugInfo.AllChoices)
                {
                    allRules.AppendFormat("\n{0} ({1}/{2})", choice.Card, choice.RuleCount, player2.DebugInfo.TotalRuleCount);
                }
                debugNote2 = string.Format("{0}: {1}\nVšechny simulace:{2}", player2.DebugInfo.Card, player2.DebugInfo.Rule, allRules.ToString());
            }
            else
            {
                debugNote2 = $"{player2.DebugInfo.Card}: {player2.DebugInfo.Rule}";
            }

            if (_g.trump.HasValue && c2.Value == Hodnota.Svrsek && player2.Hand.HasK(c2.Suit))
            {
                hlasPoints2 = c2.Suit == _g.trump.Value ? 40 : 20;
            }
            if (hlas2) player2.Hlasy++;
            _g.ThrowIfCancellationRequested(); 
            _g.OnCardPlayed(this);
            
            c3 = player3.PlayCard(this);
            if (c3 == null)
            {
                throw new InvalidOperationException("Card c3 cannot be null");
            }
            player3.Hand.Remove(c3);
            _g.DebugString.AppendFormat("Player{0}: {1}\n", player3.PlayerIndex + 1, c3);
			if (_g.rounds.Where(i => i != null && i.number < number).SelectMany(i => new[] { i.c1, i.c2, i.c3 }).Contains(c3))
			{
				throw new InvalidOperationException($"Card {c3} has already been played");
			}
            if (player3.DebugInfo.AllChoices.Count() > 1)
            {
                allRules.Clear();
                foreach (var choice in player3.DebugInfo.AllChoices)
                {
                    allRules.AppendFormat("\n{0} ({1}/{2})", choice.Card, choice.RuleCount, player3.DebugInfo.TotalRuleCount);
                }
                debugNote3 = string.Format("{0}: {1}\nVšechny simulace:{2}", player3.DebugInfo.Card, player3.DebugInfo.Rule, allRules.ToString());
            }
            else
            {
                debugNote3 = $"{player3.DebugInfo.Card}: {player3.DebugInfo.Rule}";
            }

            if (_g.trump.HasValue && c3.Value == Hodnota.Svrsek && player3.Hand.HasK(c3.Suit))
            {
                hlasPoints3 = c3.Suit == _g.trump.Value ? 40 : 20;
            }
            if (hlas3) player3.Hlasy++;
            _g.ThrowIfCancellationRequested(); 
            _g.OnCardPlayed(this);

            CalculateRoundScore();

            return roundWinner;
        }

        private void CalculateRoundScore(Barva? trump = null)
        {
            trump = trump ?? _g?.trump;

            var winningCard = Round.WinningCard(c1, c2, c3, trump);

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

            if (trump.HasValue)
            {
                points1 += hlasPoints1;
                points2 += hlasPoints2;
                points3 += hlasPoints3;
            }
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
