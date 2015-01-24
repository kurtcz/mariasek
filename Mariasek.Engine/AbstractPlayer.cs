using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
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

        /// <summary>
        /// Reference to the current <see cref="Game"/> instance
        /// </summary>
        protected Game _g;
        /// <summary>
        /// Gets the player's name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Gets the player's hand
        /// </summary>
        public List<Card> Hand { get; set; }
        public int Hlasy;

        protected AbstractPlayer(string name, Game g)
        {
            Hand = new List<Card>();
            _g = g;
            Name = name;
            DebugInfo = new PlayerDebugInfo();
        }

        /// <summary>
        /// For debuggin only: show info about the rule that the player used to make a certain move
        /// </summary>
        public PlayerDebugInfo DebugInfo { get; set; }

        /// <summary>
        /// MyIndex je muj index ve hre obecne
        /// </summary>
        protected int MyIndex
        {
            get
            {
                return Array.IndexOf(_g.players, this);
            }
        }

        /// <summary>
        /// GameStarterIndex je index toho, kdo volil trumfy (a hraje sam) ve hre obecne. 0 = human; 1,2 = ai
        /// </summary>

        protected int GameStarterIndex
        {
            get
            {
                return Array.IndexOf(_g.players, _g.GameStartingPlayer);
            }
        }

        /// <summary>
        /// TeamMateIndex je index spoluhrace. Pokud hraju sam pak nema hodnota smysl (vraci -1)
        /// </summary>
        public int TeamMateIndex
        {
            get
            {
                if (_g.GameStartingPlayer == this)
                    return -1;

                for (int i = 0; i < Game.NumPlayers; i++)
                {
                    if ((i != MyIndex) && (i != GameStarterIndex))
                        return i;
                }

                return -1;
            }
        }

        /// <summary>
        /// Sorts a player's hand
        /// </summary>
        public void SortHand()
        {
            //TODO: implement ascending / descending sorting (conditionally multiply result by -1 upon return)
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

        /// <summary>
        /// Returns a boolean value indicating whether the player has got a Hlas in a given suit
        /// </summary>
        public bool Hlas(Barva suit)
        {
            return Hlas(Hand, suit);
        }

        /// <summary>
        /// Returns a boolean value indicating whether the card has been played so far
        /// </summary>
        protected static bool IsCardUnplayed(Card card)
        {
            return card.RoundPlayed == 0;
        }

        private static bool Hlas(List<Card> hand, Barva suit)
        {
            return hand.Exists(item => IsCardUnplayed(item) && item.Value == Hodnota.Svrsek && item.Suit == suit) &&
                   hand.Exists(item => IsCardUnplayed(item) && item.Value == Hodnota.Kral && item.Suit == suit);
        }

        /// <summary>
        /// Checks if player 1 can play a given card
        /// </summary>
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

        /// <summary>
        /// Checks if player 1 can play a given card
        /// </summary>
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

        /// <summary>
        /// Checks if player 2 can play a given card
        /// </summary>
        public Renonc IsCardValid(Card c, Card first)
        {
            return IsCardValid(Hand, _g.trump, c, first);
        }

        /// <summary>
        /// Checks if player 2 can play a given card
        /// </summary>
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

        /// <summary>
        /// Checks if player 3 can play a given card
        /// </summary>
        public Renonc IsCardValid(Card c, Card first, Card second)
        {
            return IsCardValid(Hand, _g.trump, c, first, second);
        }

        /// <summary>
        /// Checks if player 3 can play a given card
        /// </summary>
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

        /// <summary>
        /// Gets a list of player's valid cards
        /// </summary>
        public static List<Card> ValidCards(List<Card> hand, Barva trump, Card first)
        {
            return hand.Where(c => IsCardValid(hand, trump, c, first) == Renonc.Ok).ToList();
        }

        /// <summary>
        /// Gets a list of player's valid cards
        /// </summary>
        public static List<Card> ValidCards(List<Card> hand, Barva trump, Card first, Card second)
        {
            return hand.Where(c => IsCardValid(hand, trump, c, first, second) == Renonc.Ok).ToList();
        }

        #region Commands    //Commands tell player that an action needs to be taken. AI implementations shall ensure that a notice that AI is thinking is displayed on screen and start a background worker thread
        public delegate void ChooseTalonCommandDelegate();
        public event ChooseTalonCommandDelegate ChooseTalonCommand;
        protected virtual void OnChooseTalonCommand()
        {
            if (ChooseTalonCommand != null)
                ChooseTalonCommand();
        }

        public delegate void ChooseTrumpCommandDelegate();
        public event ChooseTrumpCommandDelegate ChooseTrumpCommand;
        protected virtual void OnChooseTrumpCommand()
        {
            if (ChooseTrumpCommand != null)
                ChooseTrumpCommand();
        }

        public delegate void ChooseGameTypeCommandDelegate();
        public event ChooseGameTypeCommandDelegate ChooseGameTypeCommand;
        protected virtual void OnChooseGameTypeCommand()
        {
            if (ChooseGameTypeCommand != null)
                ChooseGameTypeCommand();
        }

        public delegate void PlayCardCommandDelegate(Renonc err);
        public event PlayCardCommandDelegate PlayCardCommand;
        protected virtual void OnPlayCardCommand(Renonc err)
        {
            if (PlayCardCommand != null)
                PlayCardCommand(err);
        }
        #endregion

        //these functions are called from the Game class. Descendants of this class should implement their game logic here
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

        public abstract void ChooseTrump();
        //once the game logic chooses the trump colour this callback should be called. It notifies the Game class of the result of Player's thinking
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

        public abstract void ChooseGameType();
        //once the game logic chooses the game type this callback should be called. It notifies the Game class of the result of Player's thinking
        public void ChooseGameTypeCallback(Hra gameType)
        {
            OnGameTypeChosen(new GameTypeEventArgs(gameType));
        }
        public delegate void GameTypeChosenEventHandler(object sender, GameTypeEventArgs e);
        public event GameTypeChosenEventHandler GameTypeChosen;
        protected virtual void OnGameTypeChosen(GameTypeEventArgs e)
        {
            if (GameTypeChosen != null)
                GameTypeChosen(this, e);
        }

        //these functions are called from the Game or Round class. Descendants of this class should implement their game logic here
        public abstract void PlayCard(Renonc err);
        public abstract void PlayCard2(Card first, Renonc err);
        public abstract void PlayCard3(Card first, Card second, Renonc err);
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

        //these callback functions get called whenever any player plays a card
        public abstract void RoundInfo(Card first, AbstractPlayer player1, bool hlas);
        public abstract void RoundInfo(Card first, Card second, AbstractPlayer player1, AbstractPlayer player2, bool hlas);
        public abstract void RoundInfo(Card first, Card second, Card third, AbstractPlayer player1, AbstractPlayer player2, AbstractPlayer player3, bool hlas);

        //this callback function gets called from Round class after the round has finished
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
