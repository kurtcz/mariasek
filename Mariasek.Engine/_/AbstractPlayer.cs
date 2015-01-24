using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public abstract class AbstractPlayer
    {
        public enum Renonc
        {
            Ok,
            PriznejBarvu,
            JdiVejs,
            HrajTrumf,
            HrajSvrska
        };

        protected Game _g;
        public string Name { get; set; }
        public List<Card> Hand { get; set; }
        public int Hlasy;

        protected AbstractPlayer(string name, Game g)
        {
            Hand = new List<Card>();
            _g = g;
            Name = name;
        }

        public void SortHand()
        {
            Hand.Sort(
                delegate(Card c1, Card c2)
                {
                    int s1 = Convert.ToInt16(c1.Suit);
                    int s2 = Convert.ToInt16(c2.Suit);

                    if (s1 == s2)
                        return Convert.ToInt16(c2.Value) - Convert.ToInt16(c1.Value);
                    else
                    {
                        return s1 - s2;
                    }
                });
        }
        
        public void ShowHand()
        {
            int i = 1;
            foreach (var card in Hand)
            {
                Console.WriteLine("{0}: {1}", i++, card);
            }
        }

        public bool Hlas(Barva suit)
        {
            return Hlas(Hand, suit);
        }

        protected static bool IsCardUnplayed(Card card)
        {
            return card.RoundPlayed == 0;
        }

        public static bool Hlas(List<Card> hand, Barva suit)
        {
            return hand.Exists(item => IsCardUnplayed(item) && item.Value == Hodnota.Svrsek && item.Suit == suit) &&
                   hand.Exists(item => IsCardUnplayed(item) && item.Value == Hodnota.Kral && item.Suit == suit);
        }

        public Renonc IsCardValid(Card c)
        {
            //Hrali jsme krale kdyz mame v ruce hlasku?
            if (c.Value == Hodnota.Kral && Hlas(c.Suit))
            {
                return Renonc.HrajSvrska;
            }
            else
            {
                return Renonc.Ok;
            }
        }

        public static Renonc IsCardValid(List<Card> hand, Card c)
        {
            //Hrali jsme krale kdyz mame v ruce hlasku?
            if (c.Value == Hodnota.Kral && Hlas(hand, c.Suit))
            {
                return Renonc.HrajSvrska;
            }
            else
            {
                return Renonc.Ok;
            }
        }

        public Renonc IsCardValid(Card c, Card first)
        {
            return IsCardValid(Hand, _g.trump, c, first);
        }

        public static Renonc IsCardValid(List<Card> hand, Barva trump, Card c, Card first)
        {
            if (IsCardValid(hand, c) == Renonc.HrajSvrska)
            {
                return Renonc.HrajSvrska;
            }

            //priznana barva
            if(c.Suit == first.Suit)
            {
                //sli jsme vejs - ok
                if (c.Value > first.Value)
                {
                    return Renonc.Ok;
                }
                //sli jsme niz: nemame v ruce vyssi v barve?
                if (hand.Exists(item => IsCardUnplayed(item) && item.Suit == first.Suit && item.Value > first.Value))
                {
                    return Renonc.JdiVejs;
                }
                else
                {
                    return Renonc.Ok;
                }
            }
            else
            {
                //nepriznali jsme barvu: je to ok?
                if (hand.Exists(item => IsCardUnplayed(item) && item.Suit == first.Suit))
                {
                    return Renonc.PriznejBarvu;
                }

                if (c.Suit == trump)
                {
                    return Renonc.Ok;
                }

                //nehrali jsme trumf. Nemame ho v ruce?
                if (hand.Exists(item => IsCardUnplayed(item) && item.Suit == trump))
                {
                    return Renonc.HrajTrumf;
                }
                else
                {
                    return Renonc.Ok;
                }
            }
        }

        public Renonc IsCardValid(Card c, Card first, Card second)
        {
            return IsCardValid(Hand, _g.trump, c, first, second);
        }
        public static Renonc IsCardValid(List<Card> hand, Barva trump, Card c, Card first, Card second)
        {
            if (IsCardValid(hand, c) == Renonc.HrajSvrska)
            {
                return Renonc.HrajSvrska;
            }

            //porovnej kartu s tou nejvyssi hranou v tomhle kole
            if(first.IsHigherThan(second, trump))
            {
                //prvni karta je nejvyssi
                return IsCardValid(hand, trump, c, first);
            }
            else
            {
                //druha karta prebiji prvni
                if(first.Suit == second.Suit)
                {
                    return IsCardValid(hand, trump, c, second);
                }
                else
                {
                    //druha karta je trumf
                    if(c.Suit == trump)
                    {
                        //my jsme hrali taky trumf, je to ok?
                        if (hand.Exists(item => IsCardUnplayed(item) && item.Suit == first.Suit))
                        {
                            return Renonc.PriznejBarvu;
                        }
                        else
                        {
                            return IsCardValid(hand, trump, c, second);                            
                        }
                    }
                    else if(c.Suit == first.Suit)
                    {
                        //my jsme nehrali trumf, ale ctili jsme barvu
                        return Renonc.Ok;
                    }
                    else
                    {
                        //nehrali jsme trumf a nectili jsme barvu, je to ok? (tzn. nemame ani barvu ani trumf)
                        if (hand.Exists(item => IsCardUnplayed(item) && item.Suit == first.Suit))
                        {
                            return Renonc.PriznejBarvu;
                        }
                        else if (hand.Exists(item => IsCardUnplayed(item) && item.Suit == trump))
                        {
                            return Renonc.HrajTrumf;
                        }
                        else
                        {
                            return Renonc.Ok;
                        }
                    }
                }
            }                
        }

        //these functions are called from Game class. Descendants of this class should implement their game logic here
        public abstract void ChooseTalon();
        //once the game logic decides which card should be played this callback should be called. It notifies the Round class of the result of Player's thinking
        public void  ChooseTalonCallback(List<Card> talon)
        {
            OnTalonChosen(new TalonEventArgs(talon));
        }

        public delegate void TalonChosenEventHandler(object sender, TalonEventArgs e);
        public event TalonChosenEventHandler TalonChosen;

        protected virtual void OnTalonChosen(TalonEventArgs e)
        {
            if (TalonChosen != null)
                TalonChosen(this, e);
        }

        //these functions are called from Game class. Descendants of this class should implement their game logic here
        public abstract void ChooseTrump();
        //once the game logic decides which card should be played this callback should be called. It notifies the Round class of the result of Player's thinking
        public void ChooseTrumpCallback(Card card)
        {
            OnTrumpChosen(new CardEventArgs(card));
        }

        public delegate void TrumpChosenEventHandler(object sender, CardEventArgs e);
        public event TrumpChosenEventHandler TrumpChosen;

        protected virtual void OnTrumpChosen(CardEventArgs e)
        {
            if (TrumpChosen != null)
                TrumpChosen(this, e);
        }

        //these functions are called from Round class. Descendants of this class should implement their game logic here
        public abstract void PlayCard(Renonc err);
        public abstract void PlayCard(Card first, Renonc err);
        public abstract void PlayCard(Card first, Card second, Renonc err);

        //once the game logic decides which card should be played this callback should be called. It notifies the Round class of the result of Player's thinking
        public void PlayCardCallback(Card c)
        {
            OnCardPlayed(new CardEventArgs(c));
        }

        public delegate void CardPlayedEventHandler(object sender, CardEventArgs e);
        public event CardPlayedEventHandler CardPlayed;

        protected virtual void OnCardPlayed(CardEventArgs e)
        {
            if (CardPlayed != null)
                CardPlayed(this, e);
        }

        //this function gets called whenever any player plays a card
        public abstract void RoundInfo(Card first, AbstractPlayer player1, bool hlas);
        public abstract void RoundInfo(Card first, Card second, AbstractPlayer player1, AbstractPlayer player2, bool hlas);
        public abstract void RoundInfo(Card first, Card second, Card third, AbstractPlayer player1, AbstractPlayer player2, AbstractPlayer player3, bool hlas);

        //this function gets called from Round class after the round has finished
        public abstract void RoundFinishedInfo(AbstractPlayer winner, int points);

        public delegate void RoundFinishedEventHandler(object sender, RoundFinishedEventArgs e);

        public event RoundFinishedEventHandler RoundFinished;
        protected virtual void OnRoundFinished(RoundFinishedEventArgs e)
        {
            if (RoundFinished != null)
                RoundFinished(this, e);
        }

        //This function is called from the descendants class to confirm that the current round can end and a new round can start
        public void RoundFinishedCallback()
        {
            OnEndOfRoundConfirmed();
        }

        public delegate void EndOfRoundConfirmedEventHandler(object sender);
        public event EndOfRoundConfirmedEventHandler EndOfRoundConfirmed;

        protected virtual void OnEndOfRoundConfirmed()
        {
            if (EndOfRoundConfirmed != null)
                EndOfRoundConfirmed(this);
        }

    }
}
