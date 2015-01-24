using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Logger;
using log4net;
using Mariasek.Engine.Properties;

namespace Mariasek.Engine
{
    public class BrutePlayer : AbstractPlayer
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly List<Card> cardsPlayed = new List<Card>();
        private readonly List<Card> trumpsPlayed = new List<Card>();
        private List<Card>[] _handPools = null;
        private List<Card>[] HandPools
        {
            get
            {
                if(_handPools == null)
                {
                    InitHandPools();
                }

                return _handPools;
            }
        }

        private bool inited;

        private readonly List<Card>[] hands = new List<Card>[Game.NumPlayers];
        private readonly Hashtable _hashtable = new Hashtable();
        private ulong _counter;
        private readonly int DEPTH = Settings.Default.Depth;

        /// <summary>
        /// MyIndex je muj index ve hre obecne
        /// </summary>
        private int MyIndex
        {
            get
            {
                return Array.IndexOf(_g.players, this);
            }
        }

        /// <summary>
        /// GameStarterIndex je index toho, kdo volil trumfy (a hraje sam) ve hre obecne. 0 = human; 1,2 = ai
        /// </summary>

        private int GameStarterIndex
        {
            get
            {
                return Array.IndexOf(_g.players, _g.GameStartingPlayer);
            }
        }

        /// <summary>
        /// GameStarterRoundIndex je index toho, kdo volil trumfy (a hraje sam) v tomto kole (0 = ten, pro koho pocitame)
        /// </summary>
        private int GameStarterRoundIndex
        {
            get
            {
                int delta = Game.NumPlayers - MyIndex;
                return (GameStarterIndex + delta) % Game.NumPlayers;
            }
        }

        /// <summary>
        /// TeamMateIndex je index spoluhrace. Pokud hraju sam pak nema hodnota smysl (vraci -1)
        /// </summary>
        private int TeamMateIndex
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

        public BrutePlayer(string name, Game g)
            : base(name, g)
        {
            LoggerSetup.TraceFileSetup("LogFile.txt", LoggerSetup.FileMode.Append);
        }

        #region ValidCards

        protected List<Card> ValidCards()
        {
            return ValidCards(Hand);
        }

        protected List<Card> ValidCards(Card first)
        {
            return ValidCards(Hand, first);
        }

        protected List<Card> ValidCards(Card first, Card second)
        {
            return ValidCards(Hand, first, second);
        }

        protected List<Card> ValidCards(List<Card> hand)
        {
            return hand.FindAll(item => IsCardUnplayed(item) && (IsCardValid(hand, item) == Renonc.Ok));
        }

        protected List<Card> ValidCards(List<Card> hand, Card first)
        {
            return hand.FindAll(item => IsCardUnplayed(item) && (IsCardValid(hand, _g.trump, item, first) == Renonc.Ok));
        }

        protected List<Card> ValidCards(List<Card> hand, Card first, Card second)
        {
            return hand.FindAll(item => IsCardUnplayed(item) && (IsCardValid(hand, _g.trump, item, first, second) == Renonc.Ok));
        }

        #endregion

        #region RecommendedCards

        protected List<Card>  RecommendedCards()
        {
            return RecommendedCards(Hand);
        }

        protected List<Card>  RecommendedCards(List<Card> hand)
        {
            return GetRecommendedCards(ValidCards(hand));
        }

        protected List<Card>  RecommendedCards(List<Card> hand, Card first)
        {
            return GetRecommendedCards(ValidCards(hand, first));
        }

        protected List<Card>  RecommendedCards(List<Card> hand, Card first, Card second)
        {
            return GetRecommendedCards(ValidCards(hand, first, second));
        }

        #endregion

        protected List<Card> GetRecommendedCards(List<Card> validCards)
        {
            List<Card> recommendedCards = new List<Card>();

            var suits = Enum.GetValues(typeof(Barva)).Cast<Barva>();
            foreach (var suit in suits)
            {
                int count = validCards.Count(item => item.Suit == suit);
                bool mamDesitku = validCards.Exists(item => item.Suit == suit && item.Value == Hodnota.Desitka);
                bool byloHranyEso = cardsPlayed.Exists(item => item.Suit == suit && item.Value == Hodnota.Eso);
                bool bylaHranaDesitka = cardsPlayed.Exists(item => item.Suit == suit && item.Value == Hodnota.Desitka);

                if(count == 0)
                {
                    continue;
                }
                else if(count == 1)
                {
                    //nekdy (v predposlednim kole) nekdy muze byt vyhodne obetovat desitku a v pristim tahu vynest eso a vzit posledni stych
                    //if ((!mamDesitku) || byloHranyEso)
                    {
                        recommendedCards.Add(validCards.Find(item => item.Suit == suit));
                    }
                }
                else
                {
                    bool mamEso = validCards.Exists(item => item.Suit == suit && item.Value == Hodnota.Eso);

                    if(mamEso)
                    {
                        //pokud mam eso ale ne desitku, tak nebudu hrat eso dokud nebyla vynesena desitka
                        if (!mamDesitku && bylaHranaDesitka)
                        {
                            //mame sestupne setridene pole, takze prvni karta vyhovujici podmince bude ta nejvetsi
                            recommendedCards.Add(validCards.Find(item => item.Suit == suit));
                        }
                        else
                        {
                            recommendedCards.Add(validCards.Find(item => item.Suit == suit && item.Value < Hodnota.Eso));
                        }

                    }
                    else if (!mamEso && mamDesitku)
                    {
                        //mam-li desitku a aspon jednu dalsi kartu a pokud eso nebylo odehrany potom zkus vytlacit eso
                        if (!byloHranyEso)
                        {
                            //mame sestupne setridene pole, takze prvni karta vyhovujici podmince bude ta nejvetsi
                            recommendedCards.Add(
                                validCards.Find(item => item.Suit == suit && item.Value < Hodnota.Desitka));
                        }
                    }
                    else
                    {
                        //nemam-li eso ani desitku, zkus nejnizsi od barvy
                        Hodnota min = validCards.Where(item => item.Suit == suit).Min(item => item.Value);
                        recommendedCards.Add(validCards.Find(item => item.Suit == suit && item.Value == min));
                    }
                }
            }

            //pokud jsme pres predchozi pravidla nic nenasli, potom zkus vse co mam v ruce
            if(recommendedCards.Count == 0)
                return validCards;

            return recommendedCards;            
        }

        public override void ChooseTalon()
        {
            throw new NotImplementedException();
        }

        public override void ChooseTrump()
        {
            throw new NotImplementedException();
        }


        public override void ChooseGameType()
        {
            throw new NotImplementedException();
        }

        protected string ComputeHash(int playerNum, Card card, Card c2 = null, Card c3 = null)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0:x2}", card.Num);
            if(c2 != null)
                sb.AppendFormat("{0:x2}", c2.Num);
            if (c3 != null)
                sb.AppendFormat("{0:x2}", c3.Num);

            int i = 1;
            for(int j = 0; j < Game.NumPlayers; j++)
            {
                foreach (var c in hands[(playerNum + j) % Game.NumPlayers])
                {
                    if (c.RoundPlayed == 0)
                    {
                        sb.AppendFormat("{0:x2}", c.Num);
                    }
                }
            }

            return sb.ToString();
        }

        protected int ComputeRoundScore(Card card, Card c3, Card c2, int winnerIndex)
        {
            int roundscore = 0;
            
            if (winnerIndex == 0 || (winnerIndex != GameStarterRoundIndex) && (0 != GameStarterRoundIndex))
            {
                if (card.Value == Hodnota.Desitka || card.Value == Hodnota.Eso)
                {
                    roundscore += 10;
                }
                if (c2.Value == Hodnota.Desitka || c2.Value == Hodnota.Eso)
                {
                    roundscore += 10;
                }
                if (c3.Value == Hodnota.Desitka || c3.Value == Hodnota.Eso)
                {
                    roundscore += 10;
                }
            }
            else
            {
                if (card.Value == Hodnota.Desitka || card.Value == Hodnota.Eso)
                {
                    roundscore -= 10;
                }
                if (c2.Value == Hodnota.Desitka || c2.Value == Hodnota.Eso)
                {
                    roundscore -= 10;
                }
                if (c3.Value == Hodnota.Desitka || c3.Value == Hodnota.Eso)
                {
                    roundscore -= 10;
                }
            }
            return roundscore;
        }

        private void InitHandPools()
        {
            _handPools = new List<Card>[Game.NumPlayers];

            //moje karty znam            
            _handPools[MyIndex] = new List<Card>();
            _handPools[MyIndex].AddRange(Hand);

            //ostatni hraci muzou mit cokoli z toho co neznam
            //pokud hraju sam tak taky nemuzou mit nic z talonu
            for (int i = 0; i < Game.NumPlayers; i++)
            {
                if (i == MyIndex)
                    continue;

                _handPools[i] = new List<Card>();
                Deck deck = new Deck();

                Card c;
                while ((c = deck.TakeOne()) != null)
                {
                    if (!Hand.Contains(c) || ((GameStarterIndex == MyIndex) && (!_g.talon.Contains(c))))
                        _handPools[i].Add(c);
                }
            }
            inited = true;
        }

        public List<List<Card>[]> CreateHandCandidates()
        {
            var handCandidates = new List<List<Card>[]>();

            //TODO: vybrat kandidaty z handPoolu
            var handCandidate = new List<Card>[Game.NumPlayers];

            for (int i = 0; i < Game.NumPlayers; i++)
                handCandidate[i] = new List<Card>();

            handCandidate[MyIndex].AddRange(HandPools[MyIndex]);

            if(_g.GameStartingPlayer == this)
            {
                //hraju sam, mam dva soupere
            }
            else
            {
                //jeden z dalsich hracu je souper a jeden hraje se mnou

                var tempList = new List<Card>();
                //1. dej souperi co nejvic volnych trumfu
                tempList.AddRange(HandPools[GameStarterIndex].FindAll(item => item.Suit == _g.trump));
                handCandidate[GameStarterIndex].AddRange(tempList);

                //2. dej souperi vsechny volny esa na moje desitky
                int cardsRemaining = 10 - tempList.Count;

                for(int i = 0, j = 0; i<Hand.Count; i++)
                {
                    if(Hand[i].Value == Hodnota.Desitka)
                    {
                        var card =
                            HandPools[GameStarterIndex].Find(
                                item => item.Suit == Hand[i].Suit && item.Value > Hand[i].Value);

                        if(card != null)
                        {
                            handCandidate[GameStarterIndex].Add(card);
                            if(handCandidate[GameStarterIndex].Count == 10)
                            {
                                //Dej kolegovi to co zbylo
                                //handCandidate[TeamMateIndex].AddRange( HandPools[TeamMateIndex].FindAll(item => !handCandidate[GameStarterIndex].Contains(item)));
                                //Odmaz mu posledni dve karty (ty budou talon)
                                //while (handCandidate[TeamMateIndex].Count > 10)
                                //    handCandidate[TeamMateIndex].RemoveAt(handCandidate[TeamMateIndex].Count - 1);
                                handCandidate[TeamMateIndex].AddRange(
                                            (from item in HandPools[TeamMateIndex]
                                            where !handCandidate[GameStarterIndex].Contains(item)
                                            select item).Take(10 - handCandidate[TeamMateIndex].Count).ToList());

                                handCandidates.Add(handCandidate);
                                handCandidate[GameStarterIndex].AddRange(tempList);
                            }
                        }
                    }
                }

                //3. dej souperi vsechny volny zbejvajici esa a desitky
                foreach (var card in HandPools[GameStarterIndex])
                {
                    if (card.Value == Hodnota.Eso)
                    {
                        handCandidate[GameStarterIndex].Add(card);
                        if (handCandidate[GameStarterIndex].Count == 10)
                            break;
                    }
                }

                foreach (var card in HandPools[GameStarterIndex])
                {
                    if (card.Value == Hodnota.Desitka)
                    {
                        handCandidate[GameStarterIndex].Add(card);
                        if (handCandidate[GameStarterIndex].Count == 10)
                            break;
                    }
                }

                //4. dopln
            }

            for (int i = 0; i < Game.NumPlayers; i++)
            {
                if(i == MyIndex)
                    continue;

                handCandidate[i] = new List<Card>();
            }
            handCandidates.Add(handCandidate);

            return handCandidates;
        }

        public override void PlayCard(AbstractPlayer.Renonc err)
        {
            _log.InfoFormat("Playcard started. Round {0}", _g.RoundNumber);
            Card cardToPlay = hands[0][0];

            foreach (var handCandidate in CreateHandCandidates())
            {

                _counter = 0;
                _hashtable.Clear();
                //Zkusime vymyslet optimalni strategii pro dane rozlozeni karet
                //Pro zacatek budeme koukat hracum do karet ... 
                //Tento algoritmus by se mel volat pro vsechny rozlozeni, ktere chceme proverit a vybrat nejlepsi variantu

                //Pozor: handCandidate ma index 0 pro toho kdo voli trumfy, zde mam index 0 vzdy ja -> nutny prepocet
                hands[0] = handCandidate[MyIndex];
                hands[1] = handCandidate[(MyIndex + 1) % Game.NumPlayers];
                hands[2] = handCandidate[(MyIndex + 2) % Game.NumPlayers];
                //hands[1] = _g.players[(MyIndex + 1)%Game.NumPlayers].Hand;
                //hands[2] = _g.players[(MyIndex + 2)%Game.NumPlayers].Hand;

                //List<Card> cards = ValidCards(hands[0]);
                List<Card> cards = RecommendedCards(hands[0]);
                cardToPlay = cards[0];
                int maxscore = int.MinValue;

                foreach (var card in cards)
                {
                    int score = EvalCard(0, card, _g.RoundNumber, DEPTH);
                    if ((score > maxscore) ||
                        //Nehraj eso nebo desitku kdyz existuje jina karta se stejnym skore (zamezi brzkemu odhozu desitek kdyz nemuzeme propocitavat dost do hloubky)
                        (score == maxscore && (cardToPlay.Value == Hodnota.Desitka || cardToPlay.Value == Hodnota.Eso)))
                    {
                        maxscore = score;
                        cardToPlay = card;
                    }
                }
            }
            _log.InfoFormat("Play {0}. {1} Calls to EvalCard made. Hashtable has {2} entries", cardToPlay, _counter, _hashtable.Count);
            OnCardPlayed(new CardEventArgs(cardToPlay));
        }

        private int EvalCard(int playerNum, Card card, int round, int depth)
        {
            _counter++;

            string hash = ComputeHash(playerNum, card);
            if(_hashtable.ContainsKey(hash))
            {
                return (int)_hashtable[hash];
            }

            int playerNum2 = (playerNum + 1) % Game.NumPlayers;
            int playerNum3 = (playerNum + 2) % Game.NumPlayers;
            int minmaxscore2 = InitMinMaxScore(playerNum2);
            int minmaxscore3 = InitMinMaxScore(playerNum3);
            Card cardToPlay2 = null;
            Card cardToPlay3 = null;

            card.RoundPlayed = round;

            //najdi optimalni kartu pro druheho hrace
            //List<Card> cards2 = ValidCards(hands[playerNum2], card);
            List<Card> cards2 = RecommendedCards(hands[playerNum2], card);

            foreach (var c2 in cards2)
            {
                int score2 = EvalCard2(playerNum2, c2, card, round, depth);

                if (UpdateMinMaxScore(playerNum2, score2, ref minmaxscore2))
                {
                    cardToPlay2 = c2;
                }
            }

            cardToPlay2.RoundPlayed = round;

            //najdi optimalni kartu pro tretiho hrace
            List<Card> cards3 = ValidCards(hands[playerNum3], card, cardToPlay2);

            foreach(var c3 in cards3)
            {

                int score3 = EvalCard3(playerNum3, c3, card, cardToPlay2, round, depth);

                if (UpdateMinMaxScore(playerNum3, score3, ref minmaxscore3))
                {
                    cardToPlay3 = c3;
                }

            }

            cardToPlay3.RoundPlayed = round;

            //winner: 0 = ten kdo vynasi v tomhle kole
            int winner = WinnerOfTheRound(card, cardToPlay2, cardToPlay3);
            //winnerIndex: 0 = ten pro koho pocitame skore celkove
            int winnerIndex = (playerNum + winner) % Game.NumPlayers;

            int minmaxscore = InitMinMaxScore(winnerIndex);

            int roundscore = ComputeRoundScore(card, cardToPlay3, cardToPlay2, winnerIndex);

            if (round == 10)
            {
                //pokud jsem vyhral ja nebo pokud vyhral muj spoluhrac (spoluhrac nemohl zacinat hru)
                if ((winnerIndex == 0) || ((winnerIndex != GameStarterIndex) && (0 != GameStarterIndex)))
                {
                    roundscore += 10;
                }
                else
                {
                    roundscore -= 10;
                }
            }

            if ((round == 10) || (depth == 0))
            {
                minmaxscore = roundscore;
            }
            else if (round < 10)
            {

                //List<Card> cards = ValidCards(hands[winnerIndex]);
                List<Card> cards = RecommendedCards(hands[winnerIndex]);
                foreach (var c1 in cards)
                {
                    int score = roundscore + EvalCard(winnerIndex, c1, round + 1, depth - 1);

                    UpdateMinMaxScore(winnerIndex, score, ref minmaxscore);
                }

            }

            cardToPlay3.RoundPlayed = 0;
            cardToPlay2.RoundPlayed = 0;
            card.RoundPlayed = 0;

            if (!_hashtable.ContainsKey(hash))
            {
                _hashtable.Add(hash, minmaxscore);
            }

            return minmaxscore;
        }

        public override void PlayCard2(Card first, AbstractPlayer.Renonc err)
        {
            _log.InfoFormat("Playcard2 started. Round {0}. {1} played", _g.RoundNumber, first);
            Card cardToPlay;

            //for (int zz = 0; zz < 100; zz++)
            {
                _counter = 0;
                _hashtable.Clear();
                //Zkusime vymyslet optimalni strategii pro dane rozlozeni karet
                //Pro zacatek budeme koukat hracum do karet ...
                //Tento algoritmus by se mel volat pro vsechny rozlozeni, ktere chceme proverit a vybrat nejlepsi variantu
                hands[0] = Hand;
                hands[1] = _g.players[(MyIndex + 1)%Game.NumPlayers].Hand;
                hands[2] = _g.players[(MyIndex + 2)%Game.NumPlayers].Hand;

                //List<Card> cards = ValidCards(hands[0], first);
                List<Card> cards = RecommendedCards(hands[0], first);
                cardToPlay = cards[0];
                int maxscore = int.MinValue;

                foreach (var card in cards)
                {
                    int score = EvalCard2(0, card, first, _g.RoundNumber, DEPTH);
                    if ((score > maxscore) ||
                        //Nehraj eso nebo desitku kdyz existuje jina karta se stejnym skore (zamezi brzkemu odhozu desitek kdyz nemuzeme propocitavat dost do hloubky)
                        (score == maxscore && (cardToPlay.Value == Hodnota.Desitka || cardToPlay.Value == Hodnota.Eso)))
                    {
                        maxscore = score;
                        cardToPlay = card;
                    }
                }
            }
            _log.InfoFormat("Play {0}. {1} Calls to EvalCard made. Hashtable has {2} entries", cardToPlay, _counter, _hashtable.Count);
            OnCardPlayed(new CardEventArgs(cardToPlay));
        }

        private int EvalCard2(int playerNum2, Card c2, Card card, int round, int depth)
        {
            _counter++;

            string hash = ComputeHash(playerNum2, card, c2);
            if (_hashtable.ContainsKey(hash))
            {
                return (int)_hashtable[hash];
            }

            int playerNum = (playerNum2 + 2) % Game.NumPlayers;
            int playerNum3 = (playerNum2 + 1) % Game.NumPlayers;
            int minmaxscore3 = InitMinMaxScore(playerNum3);

            Card cardToPlay3 = null;

            c2.RoundPlayed = round;

            //najdi optimalni kartu pro tretiho hrace
            //List<Card> cards3 = ValidCards(hands[playerNum3], card, c2);
            List<Card> cards3 = RecommendedCards(hands[playerNum3], card, c2);

            foreach (var c3 in cards3)
            {
                int score3 = EvalCard3(playerNum3, c3, card, c2, round, depth);

                if (UpdateMinMaxScore(playerNum3, score3, ref minmaxscore3))
                {
                    cardToPlay3 = c3;
                }
            }

            cardToPlay3.RoundPlayed = round;

            //winner: 0 = ten kdo vynasi v tomhle kole
            int winner = WinnerOfTheRound(card, c2, cardToPlay3);
            //winnerIndex: 0 = ten pro koho pocitame skore celkove.
            int winnerIndex = (playerNum + winner) % Game.NumPlayers;

            int minmaxscore2 = InitMinMaxScore(winnerIndex);

            int roundscore = ComputeRoundScore(card, cardToPlay3, c2, winnerIndex);

            if (round == 10)
            {
                //pokud jsem vyhral ja nebo pokud vyhral muj spoluhrac (spoluhrac nemohl zacinat hru)
                if ((winnerIndex == 0) || ((winnerIndex != GameStarterIndex) && (0 != GameStarterIndex)))
                {
                    roundscore += 10;
                }
                else
                {
                    roundscore -= 10;
                }
            }

            if ((round == 10) || (depth == 0))
            {
                minmaxscore2 = roundscore;
            }
            else if (round < 10)
            {
                //List<Card> cards = ValidCards(hands[winnerIndex]);
                List<Card> cards = RecommendedCards(hands[winnerIndex]);
                foreach (var c1 in cards)
                {
                    int score = roundscore + EvalCard(winnerIndex, c1, round + 1, depth - 1);

                    UpdateMinMaxScore(winnerIndex, score, ref minmaxscore2);
                }
            }

            cardToPlay3.RoundPlayed = 0;
            c2.RoundPlayed = 0;

            if (!_hashtable.ContainsKey(hash))
            {
                _hashtable.Add(hash, minmaxscore2);
            }

            return minmaxscore2;
        }

        public override void PlayCard3(Card first, Card second, AbstractPlayer.Renonc err)
        {
            _log.InfoFormat("Playcard3 started. Round {0}. {1}, {2} played", _g.RoundNumber, first, second);
            Card cardToPlay;

            _counter = 0;
            _hashtable.Clear();
            //Zkusime vymyslet optimalni strategii pro dane rozlozeni karet
            //Pro zacatek budeme koukat hracum do karet ...
            //Tento algoritmus by se mel volat pro vsechny rozlozeni, ktere chceme proverit a vybrat nejlepsi variantu
            hands[0] = Hand;
            hands[1] = _g.players[(MyIndex + 1) % Game.NumPlayers].Hand;
            hands[2] = _g.players[(MyIndex + 2) % Game.NumPlayers].Hand;

            //List<Card> cards = ValidCards(hands[0], first, second);
            List<Card> cards = RecommendedCards(hands[0], first, second);
            cardToPlay = cards[0];
            int maxscore = int.MinValue;

            foreach (var card in cards)
            {
                int score = EvalCard3(0, card, first, second, _g.RoundNumber, DEPTH);
                if ((score > maxscore) ||
                    //Nehraj eso nebo desitku kdyz existuje jina karta se stejnym skore (zamezi brzkemu odhozu desitek kdyz nemuzeme propocitavat dost do hloubky)
                    (score == maxscore && (cardToPlay.Value == Hodnota.Desitka || cardToPlay.Value == Hodnota.Eso)))
                {
                    maxscore = score;
                    cardToPlay = card;
                }
            }
            _log.InfoFormat("Play {0}. {1} Calls to EvalCard made. Hashtable has {2} entries", cardToPlay, _counter, _hashtable.Count);
            OnCardPlayed(new CardEventArgs(cardToPlay));
        }

        private int EvalCard3(int playerNum3, Card c3, Card card, Card c2, int round, int depth)
        {
            _counter++;

            string hash = ComputeHash(playerNum3, card, c2, c3);
            if (_hashtable.ContainsKey(hash))
            {
                return (int)_hashtable[hash];
            }

            int playerNum = (playerNum3 + 1) % Game.NumPlayers;
            int playerNum2 = (playerNum3 + 2) % Game.NumPlayers;

            c3.RoundPlayed = round;

            //winner: 0 = ten kdo vynasi v tomhle kole
            int winner = WinnerOfTheRound(card, c2, c3);
            //winnerIndex: 0 = ten pro koho pocitame skore celkove.
            int winnerIndex = (playerNum + winner) % Game.NumPlayers;

            int minmaxscore3 = InitMinMaxScore(winnerIndex);

            int roundscore = ComputeRoundScore(card, c3, c2, winnerIndex);

            if (round == 10)
            {
                //pokud jsem vyhral ja nebo pokud vyhral muj spoluhrac (spoluhrac nemohl zacinat hru)
                if ((winnerIndex == 0) || ((winnerIndex != GameStarterIndex) && (0 != GameStarterIndex)))
                {
                    roundscore += 10;
                }
                else
                {
                    roundscore -= 10;
                }
            }

            if ((round == 10) || (depth == 0))
            {
                minmaxscore3 = roundscore;
            }
            else if (round < 10)
            {
                //List<Card> cards = ValidCards(hands[winnerIndex]);
                List<Card> cards = RecommendedCards(hands[winnerIndex]);
                foreach (var c1 in cards)
                {
                    int score = roundscore + EvalCard(winnerIndex, c1, round + 1, depth - 1);
                    
                    UpdateMinMaxScore(winnerIndex, score, ref minmaxscore3);
                }
            }

            c3.RoundPlayed = 0;

            if (!_hashtable.ContainsKey(hash))
            {
                _hashtable.Add(hash, minmaxscore3);
            }

            return minmaxscore3;
        }

        protected int InitMinMaxScore(int playerNum)
        {
            if ((playerNum == 0) ||
                               ((playerNum != GameStarterRoundIndex) && (0 != GameStarterRoundIndex)))
                return int.MinValue;
            else
                return int.MaxValue;            
        }

        protected bool UpdateMinMaxScore(int playerNum, int score, ref int minmaxscore)
        {
            //ten pro koho pocitame skore usiluje o maximalni skore
            //spoluhrac toho pro koho pocitame skore usiluje o maximalni skore (spoluhrac nemohl zacinat hru)
            if ((playerNum == 0) ||
                ((playerNum != GameStarterRoundIndex) && (0 != GameStarterRoundIndex)))
            {
                if (minmaxscore < score)
                {
                    minmaxscore = score;
                    return true;
                }
            }
            //souper usiluje o minimalni skore
            else
            {
                if (minmaxscore > score)
                {
                    minmaxscore = score;
                    return true;
                }
            }

            return false;
        }

        protected int WinnerOfTheRound(Card c1, Card c2, Card c3)
        {
            if (c1.IsHigherThan(c2, _g.trump))
            {
                if (c1.IsHigherThan(c3, _g.trump))
                {
                    return 0;
                }
                else
                {
                    return 2;
                }
            }
            else
            {
                if (c2.IsHigherThan(c3, _g.trump))
                {
                    return 1;
                }
                else
                {
                    return 2;
                }
            }
        }

        public override void RoundInfo(Card first, AbstractPlayer player1, bool hlas)
        {
            cardsPlayed.Add(first);
            if(first.Suit == _g.trump)
            {
                trumpsPlayed.Add(first);
            }

            HandPools[0].Remove(first);
            HandPools[1].Remove(first);
            HandPools[2].Remove(first);
        }

        public override void RoundInfo(Card first, Card second, AbstractPlayer player1, AbstractPlayer player2, bool hlas)
        {
            cardsPlayed.Add(second);
            if (second.Suit == _g.trump)
            {
                trumpsPlayed.Add(second);
            }

            HandPools[0].Remove(second);
            HandPools[1].Remove(second);
            HandPools[2].Remove(second);

            //player2Index je index hrace ve hre
            int player2Index = Array.IndexOf(_g.players, player2);
            if((second.Suit == first.Suit) && (second.Value < first.Value))
            {
                //hrac nesel pres
                HandPools[player2Index].RemoveAll(item => item.Suit == first.Suit && item.Value > first.Value);
            }
            else if(second.Suit != first.Suit)
            {
                if(second.Suit == _g.trump)
                {
                    HandPools[player2Index].RemoveAll(item => item.Suit == first.Suit);
                }
                else
                {
                    HandPools[player2Index].RemoveAll(item => (item.Suit == first.Suit) || (item.Suit == _g.trump));
                }
            }
        }

        public override void RoundInfo(Card first, Card second, Card third, AbstractPlayer player1, AbstractPlayer player2, AbstractPlayer player3, bool hlas)
        {
            cardsPlayed.Add(third);
            if (third.Suit == _g.trump)
            {
                trumpsPlayed.Add(third);
            }

            HandPools[0].Remove(third);
            HandPools[1].Remove(third);
            HandPools[2].Remove(third);

            //player3Index je index hrace ve hre
            int player3Index = Array.IndexOf(_g.players, player3);
            if((second.Suit == first.Suit) && (second.Value > first.Value))
            {
                //druhy hrac sel pres
                if ((third.Suit == second.Suit) && (third.Value < second.Value))
                {
                    //treti hrac nesel pres
                    HandPools[player3Index].RemoveAll(item => item.Suit == second.Suit && item.Value > second.Value);
                }
                else if (third.Suit != second.Suit)
                {
                    if (third.Suit == _g.trump)
                    {
                        HandPools[player3Index].RemoveAll(item => item.Suit == second.Suit);
                    }
                    else
                    {
                        HandPools[player3Index].RemoveAll(item => (item.Suit == second.Suit) || (item.Suit == _g.trump));
                    }
                }                
            }
            else if((second.Suit != first.Suit) && (second.Suit == _g.trump))
            {
                //druhy hrac hral trumf
                if(third.Suit == _g.trump)
                {
                    //treti hrac hral tumf
                    if(third.Value < second.Value)
                    {
                        HandPools[player3Index].RemoveAll(item => (item.Suit == first.Suit) || (item.Suit == second.Suit && item.Value > second.Value));
                    }
                    else
                    {
                        HandPools[player3Index].RemoveAll(item => item.Suit == first.Suit);
                    }
                }
                else if(third.Suit != first.Suit)
                {
                    HandPools[player3Index].RemoveAll(item => (item.Suit == first.Suit) || (item.Suit == _g.trump));
                }
            }
            else
            {
                //druhy hrac nesel pres a nehral trumf
                if ((third.Suit == first.Suit) && (third.Value < first.Value))
                {
                    //treti hrac nesel pres
                    HandPools[player3Index].RemoveAll(item => item.Suit == first.Suit && item.Value > first.Value);
                }
                else if (third.Suit != first.Suit)
                {
                    if (third.Suit == _g.trump)
                    {
                        HandPools[player3Index].RemoveAll(item => item.Suit == first.Suit);
                    }
                    else
                    {
                        HandPools[player3Index].RemoveAll(item => (item.Suit == first.Suit) || (item.Suit == _g.trump));
                    }
                }
            }
        }

        public override void RoundFinishedInfo(AbstractPlayer winner, int points)
        {
            OnEndOfRoundConfirmed();
        }
    }
}
