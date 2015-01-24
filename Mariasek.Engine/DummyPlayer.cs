using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Mariasek.Engine
{
    public class DummyPlayer : AbstractPlayer
    {
        public DummyPlayer(string name, Game g) : base(name, g)
        {
        }

        public override void ChooseTalon()
        {
            List<Card> talon = new List<Card>();
            int c;

            c = Hand.FindIndex(
                    item => item.Suit != _g.trump && item.Value != Hodnota.Eso && item.Value != Hodnota.Desitka);
            talon.Add(Hand[c]);
            Hand.RemoveAt(c);

            c = Hand.FindIndex(
                    item => item.Suit != _g.trump && item.Value != Hodnota.Eso && item.Value != Hodnota.Desitka);
            talon.Add(Hand[c]);
            Hand.RemoveAt(c);

            OnTalonChosen(new TalonEventArgs(talon));
        }

        /// <summary>
        /// Chooses first card a a trump card
        /// </summary>
        public override void ChooseTrump()
        {
            OnTrumpChosen(new CardEventArgs(Hand[0]));
        }

        /// <summary>
        /// Plays the 1st card
        /// </summary>
        public override void PlayCard(Renonc err)
        {
            OnCardPlayed(new CardEventArgs(Hand[0]));
        }

        /// <summary>
        /// Plays the 1st valid card
        /// </summary>
        public override void PlayCard2(Card first, Renonc err)
        {
            Card c = Hand.Find(item => IsCardValid(item, first) == Renonc.Ok);

            OnCardPlayed(new CardEventArgs(c));
        }

        /// <summary>
        /// Plays the 1st valid card
        /// </summary>
        public override void PlayCard3(Card first, Card second, Renonc err)
        {
            Card c = Hand.Find(item => IsCardValid(item, first, second) == Renonc.Ok);

            OnCardPlayed(new CardEventArgs(c));
        }

        public override void RoundInfo(Card first, AbstractPlayer player1, bool hlas)
        {
        }

        public override void RoundInfo(Card first, Card second, AbstractPlayer player1, AbstractPlayer player2, bool hlas)
        {
        }

        public override void RoundInfo(Card first, Card second, Card third, AbstractPlayer player1, AbstractPlayer player2, AbstractPlayer player3, bool hlas)
        {
        }

        public override void RoundFinishedInfo(AbstractPlayer winner, int points)
        {
            OnEndOfRoundConfirmed();
        }

        public override void ChooseGameType()
        {
            OnGameTypeChosen(new GameTypeEventArgs(Hra.Hra));
        }
    }
}
