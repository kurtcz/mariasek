using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public class Round
    {
        public AbstractPlayer player1;
        public AbstractPlayer player2;
        public AbstractPlayer player3;

        public Card c1;
        public Card c2;
        public Card c3;

        private AbstractPlayer winner;
        private int points;

        private bool player1ConfirmedEndOfRound;
        private bool player2ConfirmedEndOfRound;
        private bool player3ConfirmedEndOfRound;

        public int player1Hlas { get; set; }
        public int player2Hlas { get; set; }
        public int player3Hlas { get; set; }

        private Game _g;

        public Round(Game g)
        {
            _g = g;
        }

        public void UnregisterEventHandlers()
        {
            player1.CardPlayed -= Player1CardPlayed;
            player2.CardPlayed -= Player2CardPlayed;
            player3.CardPlayed -= Player3CardPlayed;

            player1.EndOfRoundConfirmed -= PlayerConfirmedEndOfRound;
            player2.EndOfRoundConfirmed -= PlayerConfirmedEndOfRound;
            player3.EndOfRoundConfirmed -= PlayerConfirmedEndOfRound;

            RoundFinished -= _g.RoundFinished;
        }

        public void PlayRound(AbstractPlayer startingPlayer)
        {
            int starter = Array.IndexOf(_g.players, startingPlayer);

            player1 = startingPlayer;
            player2 = _g.players[(starter + 1) % Game.NumPlayers];
            player3 = _g.players[(starter + 2) % Game.NumPlayers];

            player1.CardPlayed += Player1CardPlayed;
            player2.CardPlayed += Player2CardPlayed;
            player3.CardPlayed += Player3CardPlayed;

            player1.EndOfRoundConfirmed += PlayerConfirmedEndOfRound;
            player2.EndOfRoundConfirmed += PlayerConfirmedEndOfRound;
            player3.EndOfRoundConfirmed += PlayerConfirmedEndOfRound;

            player1ConfirmedEndOfRound = false;
            player2ConfirmedEndOfRound = false;
            player3ConfirmedEndOfRound = false;

            player1Hlas = 0;
            player2Hlas = 0;
            player3Hlas = 0;

            player1.PlayCard(AbstractPlayer.Renonc.Ok);
        }

        private void Player1CardPlayed(object sender, CardEventArgs e)
        {
            AbstractPlayer.Renonc err;
            c1 = e.card;
            if ((err = player1.IsCardValid(c1)) != AbstractPlayer.Renonc.Ok)
            {
                player1.PlayCard(err);
            }
            else
            {
                bool hlas = (c1.Value == Hodnota.Svrsek && player1.Hlas(c1.Suit));
                if(hlas)
                {
                    player1Hlas += c1.Suit == _g.trump ? 40 : 20;
                    player1.Hlasy++;
                }
                player1.CardPlayed -= Player1CardPlayed;
                player1.Hand.Remove(c1);
                foreach (var player in _g.players)
                {
                    player.RoundInfo(c1, player1, hlas);
                }

                player2.PlayCard2(c1, AbstractPlayer.Renonc.Ok);
            }
        }

        private void Player2CardPlayed(object sender, CardEventArgs e)
        {
            AbstractPlayer.Renonc err;

            c2 = e.card;
            if ((err = player2.IsCardValid(c2, c1)) != AbstractPlayer.Renonc.Ok)
            {
                player2.PlayCard2(c1, err);
            }
            else
            {
                bool hlas = (c2.Value == Hodnota.Svrsek && player2.Hlas(c2.Suit));
                if (hlas)
                {
                    player2Hlas += c2.Suit == _g.trump ? 40 : 20;
                    player2.Hlasy++;
                }
                player2.CardPlayed -= Player2CardPlayed;
                player2.Hand.Remove(c2);
                foreach (var player in _g.players)
                {
                    player.RoundInfo(c1, c2, player1, player2, hlas);
                }

                player3.PlayCard3(c1, c2, AbstractPlayer.Renonc.Ok);                
            }
        }

        private void Player3CardPlayed(object sender, CardEventArgs e)
        {
            AbstractPlayer.Renonc err;

            c3 = e.card;
            if ((err = player3.IsCardValid(c3, c1, c2)) != AbstractPlayer.Renonc.Ok)
            {
                player3.PlayCard3(c1, c2, err);
            }
            else
            {
                bool hlas = (c3.Value == Hodnota.Svrsek && player3.Hlas(c3.Suit));
                if (hlas)
                {
                    player3Hlas += c3.Suit == _g.trump ? 40 : 20;
                    player3.Hlasy++;
                }
                player3.CardPlayed -= Player3CardPlayed;
                player3.Hand.Remove(c3);

                winner = WinnerOfTheRound();
                points = PointsWon();

                foreach (var player in _g.players)
                {
                    player.RoundInfo(c1, c2, c3, player1, player2, player3, hlas);
                    player.RoundFinishedInfo(winner, points);
                }                
            }
        }

        private void PlayerConfirmedEndOfRound(object sender)
        {
            AbstractPlayer player = sender as AbstractPlayer;

            if(player == player1)
                player1ConfirmedEndOfRound = true;
            else if(player == player2)
                player2ConfirmedEndOfRound = true;
            else if(player == player3)
                player3ConfirmedEndOfRound = true;

            if (player1ConfirmedEndOfRound && player2ConfirmedEndOfRound && player3ConfirmedEndOfRound)
            {
                player1.EndOfRoundConfirmed -= PlayerConfirmedEndOfRound;
                player2.EndOfRoundConfirmed -= PlayerConfirmedEndOfRound;
                player3.EndOfRoundConfirmed -= PlayerConfirmedEndOfRound;

                OnRoundFinished(new RoundFinishedEventArgs(winner, points));
            }
        }

        public delegate void PlayRoundEventHandler(object sender, RoundFinishedEventArgs e);
        public event PlayRoundEventHandler RoundFinished;

        protected virtual void OnRoundFinished(RoundFinishedEventArgs e)
        {
            if (RoundFinished != null)
                RoundFinished(this, e);
        }

        public AbstractPlayer WinnerOfTheRound()
        {
            if (c1.IsHigherThan(c2, _g.trump))
            {
                if (c1.IsHigherThan(c3, _g.trump))
                {
                    return player1;
                }
                else
                {
                    return player3;
                }
            }
            else
            {
                if (c2.IsHigherThan(c3, _g.trump))
                {
                    return player2;
                }
                else
                {
                    return player3;
                }
            }
        }

        public static Card WinningCard(Card c1, Card c2, Card c3, Barva trump)
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

        public int PointsWon()
        {
            int points = 0;

            if (c1.Value == Hodnota.Desitka || c1.Value == Hodnota.Eso)
                points += 10;
            if (c2.Value == Hodnota.Desitka || c2.Value == Hodnota.Eso)
                points += 10;
            if (c3.Value == Hodnota.Desitka || c3.Value == Hodnota.Eso)
                points += 10;

            return points;
        }
    }
}
