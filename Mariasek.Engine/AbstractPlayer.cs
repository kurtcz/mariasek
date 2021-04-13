using System;
using System.Collections.Generic;
using System.Linq;
//using System.Security.Policy;
using System.Text;

namespace Mariasek.Engine
{
    public interface IPlayer
    {
        Card ChooseTrump();
        List<Card> ChooseTalon();
        Hra ChooseGameType(Hra minimalBid = Hra.Hra);
        void Init();
        Card PlayCard(Round r);
    }

    //public abstract class AbstractPlayer : /*MarshalByRefObject,*/ IPlayer
    public abstract class AbstractPlayer : IPlayer
    {
        protected Game _g;

        public string Name { get; set; }
        public int PlayerIndex { get; set; }

		private int _teamMateIndex;
        public int TeamMateIndex
        {
            get
            {
                if (_g == null) //after Die() has been called
                {
                    return _teamMateIndex;
                }
				if (PlayerIndex == _g.GameStartingPlayerIndex)
				{
                    _teamMateIndex = -1;
				}
				else
				{
					_teamMateIndex = _g.players.First(i => i.PlayerIndex != PlayerIndex && i.PlayerIndex != _g.GameStartingPlayerIndex).PlayerIndex;
				}
				return _teamMateIndex;
            }
        }

        public List<Card> Hand { get; set; }
        public int Hlasy { get; set; }
        /// <summary>
        /// For debuggin only: show info about the rule that the player used to make a certain move
        /// </summary>
        public PlayerDebugInfo DebugInfo { get; set; }
        public string BidMade { get; set; }
        public float BidConfidence { get; protected set; }
        public Hra? TestGameType { get; set; }          //used when we want to generate certain game type for testing

        public abstract Card ChooseTrump();
        public abstract List<Card> ChooseTalon();
        public abstract GameFlavour ChooseGameFlavour();
        public abstract Hra ChooseGameType(Hra validGameTypes);
        public abstract Hra GetBidsAndDoubles(Bidding bidding);

        public delegate void GameComputationProgressEventHandler(object sender, GameComputationProgressEventArgs e);
        public event GameComputationProgressEventHandler GameComputationProgress;
        protected virtual void OnGameComputationProgress(GameComputationProgressEventArgs e)
        {
            if (GameComputationProgress != null)
            {
                GameComputationProgress(this, e);
            }
        }

        /// <summary>
        /// This method will be called before the 1st round has been played. Override this method to initialize the player's ai model.
        /// </summary>
        public abstract void Init();

        public virtual void Die()
        {
            if (GameComputationProgress != null)
            {
                foreach(Delegate d in GameComputationProgress.GetInvocationList())
                {
                    GameComputationProgress -= (GameComputationProgressEventHandler)d;
                }
            }
			_g = null;
        }

        public abstract Card PlayCard(Round r); //r obsahuje kontext (ktere karty uz nekdo hral prede mnou a jestli byly zahrany nejake hlasy)

        protected AbstractPlayer(Game g)
        {
            _g = g;
            TestGameType = null;
            BidMade = string.Empty;
            Hand = new List<Card>();
            DebugInfo = new PlayerDebugInfo();
        }

        ~AbstractPlayer()
        {
            System.Diagnostics.Debug.WriteLine("<<< End of player {0}", PlayerIndex);
        }

        private static Renonc IsCardValid(List<Card> hand, Barva? trump, Hra gameType, int teamMateIndex, Card c, bool isFirstPlayer)
        {
            //Hrali jsme krale kdyz mame v ruce hlasku?
            if (c.Value == Hodnota.Kral && hand.HasQ(c.Suit) && (gameType & (Hra.Betl | Hra.Durch)) == 0)
            {
                return Renonc.HrajSvrska;
            }
            else if (trump.HasValue &&
                     (((gameType & Hra.Sedma) != 0 &&
                       teamMateIndex == -1) ||
                      ((gameType & Hra.SedmaProti) != 0 &&
                       teamMateIndex != -1)) &&
                     c.Value == Hodnota.Sedma &&
                     c.Suit == trump.Value &&
                     ((isFirstPlayer && hand.Count() > 1) ||    //pokud zacinam kolo musim hrat trumfovou sedmu jako posledni kartu v ruce
                      (!isFirstPlayer && hand.CardCount(trump.Value) > 1))) //jinak musim hrat trumfovou sedmu jako posledniho trumfa v ruce
            {
                return Renonc.NehrajSedmu;
            }
            else
            {
                return Renonc.Ok;
            }
        }

        private static Renonc IsCardValid(List<Card> hand, Barva? trump, Hra gameType, int teamMateIndex, Card c, Card first)
        {
            //priznana barva
            if (c.Suit == first.Suit)
            {
                //sli jsme vejs - ok
                if(first.IsLowerThan(c, trump))
                {
                    return IsCardValid(hand, trump, gameType, teamMateIndex, c, false);
                }
                //sli jsme niz: nemame v ruce vyssi v barve?
                if (hand.Exists(i => i.Suit == first.Suit && first.IsLowerThan(i, trump)))
                {
                    return Renonc.JdiVejs;
                }
                else
                {
                    return IsCardValid(hand, trump, gameType, teamMateIndex, c, false);
                }
            }
            else
            {
                //nepriznali jsme barvu: je to ok?
                if (hand.HasSuit(first.Suit))
                {
                    return Renonc.PriznejBarvu;
                }

                if (c.Suit == trump)
                {
                    return IsCardValid(hand, trump, gameType, teamMateIndex, c, false);
                }

                //nehrali jsme trumf. Nemame ho v ruce?
                if (trump.HasValue && hand.HasSuit(trump.Value))
                {
                    return Renonc.HrajTrumf;
                }
                else
                {
                    return IsCardValid(hand, trump, gameType, teamMateIndex, c, false);
                }
            }
        }

        private static Renonc IsCardValid(List<Card> hand, Barva? trump, Hra gameType, int teamMateIndex, Card c, Card first, Card second)
        {
            //porovnej kartu s tou nejvyssi hranou v tomhle kole
            if (first.IsHigherThan(second, trump))
            {
                //prvni karta je nejvyssi
                return IsCardValid(hand, trump, gameType, teamMateIndex, c, first);
            }
            else
            {
                //druha karta prebiji prvni
                if (first.Suit == second.Suit)
                {
                    return IsCardValid(hand, trump, gameType, teamMateIndex, c, second);
                }
                else
                {
                    //druha karta je trumf
                    if (c.Suit == trump)
                    {
                        //my jsme hrali taky trumf, je to ok?
                        if (hand.HasSuit(first.Suit))
                        {
                            return Renonc.PriznejBarvu;
                        }
                        else
                        {
                            return IsCardValid(hand, trump, gameType, teamMateIndex, c, second);
                        }
                    }
                    else if (c.Suit == first.Suit)
                    {
                        //my jsme nehrali trumf, ale ctili jsme barvu
                        return IsCardValid(hand, trump, gameType, teamMateIndex, c, false);
                    }
                    else
                    {
                        //nehrali jsme trumf a nectili jsme barvu, je to ok? (tzn. nemame ani barvu ani trumf)
                        if (hand.HasSuit(first.Suit))
                        {
                            return Renonc.PriznejBarvu;
                        }
                        else if (trump.HasValue && hand.HasSuit(trump.Value))
                        {
                            return Renonc.HrajTrumf;
                        }
                        else
                        {
                            return IsCardValid(hand, trump, gameType, teamMateIndex, c, false);
                        }
                    }
                }
            }
        }

        public Renonc IsCardValid(Card c)
        {
            var trump = (_g.GameType & (Hra.Betl | Hra.Durch)) == 0 ? _g.trump : (Barva?)null;
            return IsCardValid(Hand, trump, _g.GameType, TeamMateIndex, c, true);
        }

        public Renonc IsCardValid(Card c, Card first)
        {
            var trump = (_g.GameType & (Hra.Betl | Hra.Durch)) == 0 ? _g.trump : (Barva?)null;
            return IsCardValid(Hand, trump, _g.GameType, TeamMateIndex, c, first);
        }

        public Renonc IsCardValid(Card c, Card first, Card second)
        {
            var trump = (_g.GameType & (Hra.Betl | Hra.Durch)) == 0 ? _g.trump : (Barva?)null;
            return IsCardValid(Hand, trump, _g.GameType, TeamMateIndex, c, first, second);
        }

        public static List<Card> ValidCards(List<Card> hand, Barva? trump, Hra gameType, int teamMateIndex)
        {
            return hand.Where(c => IsCardValid(hand, trump, gameType, teamMateIndex, c, true) == Renonc.Ok).ToList();
        }

        public static List<Card> ValidCards(List<Card> hand, Barva? trump, Hra gameType, int teamMateIndex, Card first)
        {
            return hand.Where(c => IsCardValid(hand, trump, gameType, teamMateIndex, c, first) == Renonc.Ok).ToList();
        }

        public static List<Card> ValidCards(List<Card> hand, Barva? trump, Hra gameType, int teamMateIndex, Card first, Card second)
        {
            return hand.Where(c => IsCardValid(hand, trump, gameType, teamMateIndex, c, first, second) == Renonc.Ok).ToList();
        }

        protected List<Card> ValidCards()
        {
            return ValidCards(Hand, _g.trump, _g.GameType, TeamMateIndex);
        }

        protected List<Card> ValidCards(Card first)
        {
            return ValidCards(Hand, _g.trump, _g.GameType, TeamMateIndex, first);
        }

        protected List<Card> ValidCards(Card first, Card second)
        {
            return ValidCards(Hand, _g.trump, _g.GameType, TeamMateIndex, first, second);
        }
    }
}
