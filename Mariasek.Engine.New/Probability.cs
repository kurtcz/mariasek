using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
//using log4net;
using Mariasek.Engine.New.Logger;
//using log4net.Appender;
using MersenneTwister;

namespace Mariasek.Engine.New
{
    public class Probability
    {
#if !PORTABLE
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
#else
        private static readonly ILog _log = new DummyLogWrapper();
#endif
        
        private int _myIndex;
        private int _gameStarterIndex;
        private Barva _trump;
        private readonly Dictionary<Barva, Dictionary<Hodnota, float>>[] _cardProbabilityForPlayer;
        //private static Random r = new Random();
        private static RandomMT mt = new RandomMT((ulong)(DateTime.Now - DateTime.MinValue).TotalMilliseconds);

        public Probability(int myIndex, int gameStarterIndex, Hand myHand, Barva trump, List<Card> talon = null)
        {
            _myIndex = myIndex;
            _gameStarterIndex = gameStarterIndex;
            _trump = trump;
            _cardProbabilityForPlayer = new Dictionary<Barva, Dictionary<Hodnota, float>>[Game.NumPlayers + 1];
            if (talon == null)
            {
                talon = new List<Card>();
            }
            for (var i = 0; i < Game.NumPlayers + 1; i++)
            {
                _cardProbabilityForPlayer[i] = new Dictionary<Barva, Dictionary<Hodnota, float>>();
                foreach (var b in Enum.GetValues(typeof (Barva)).Cast<Barva>())
                {
                    _cardProbabilityForPlayer[i][b] = new Dictionary<Hodnota, float>();
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                    {
                        if (i == myIndex)
                        {
                            _cardProbabilityForPlayer[i][b].Add(h, myHand.Any(k => k.Suit == b && k.Value == h) ? 1f : 0f);
                        }
                        else if (i == 3 && talon.Any())
                        {
                            //ten kdo voli vi co je v talonu
                            _cardProbabilityForPlayer[i][b].Add(h, talon.Any(k => k.Suit == b && k.Value == h) ? 1f : 0f);
                        }
                        else
                        {
                            if (i == 3 && (h == Hodnota.Eso || h == Hodnota.Desitka || b == trump))
                            {
                                //karty co nemohou byt v talonu
                                _cardProbabilityForPlayer[i][b].Add(h, 0f);
                            }
                            else
                            {
                                _cardProbabilityForPlayer[i][b].Add(h, myHand.Any(k => k.Suit == b && k.Value == h) ||
                                                                   talon.Any(k => k.Suit == b && k.Value == h) ? 0f : 0.5f);
                            }
                        }
                    }
                }
            }
            UpdateUncertainCardsProbability(0, trump);
        }

        /// <summary>
        /// Changes the probability of uncertain cards from 0.5 to the actual probability
        /// </summary>
        private void UpdateUncertainCardsProbability(int roundNumber, Barva? trump = null) //trumfy jsou kvuli talonu a staci pouze poprve
        {
            var uncertainCardNumbers = new int[Game.NumPlayers + 1];
            var certainCardNumbers = new int[Game.NumPlayers + 1];
            var totalCardNumbers = new int[Game.NumPlayers + 1];

            for (var i = 0; i < Game.NumPlayers + 1; i++)
            {
                totalCardNumbers[i] = i < Game.NumPlayers ? 10 - roundNumber : 2;
                certainCardNumbers[i] = _cardProbabilityForPlayer[i].SelectMany(b => b.Value.Where(c => c.Value == 1f)).Count();
                uncertainCardNumbers[i] = _cardProbabilityForPlayer[i].SelectMany(b => b.Value.Where(c => c.Value > 0f && c.Value < 1f)).Count();
            }

            //var totalUncertainCards = uncertainCardNumbers.Sum(n => n);
            //var totalUncertainAXCards = _cardProbabilityForPlayer.SelectMany(i => i.SelectMany(b => b.Value.Where(c => c.Value > 0f && c.Value < 1f))).Count();

            for (var i = 0; i < Game.NumPlayers + 1; i++)
            {
                if (i == _myIndex)
                {
                    continue;
                }
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                {
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().Where(h => _cardProbabilityForPlayer[i][b][h] > 0f &&
                                                                                                 _cardProbabilityForPlayer[i][b][h] < 1f))
                    {
                        //totalUncertainCards is computed only over those players who can have such card (with probability 0.5)
                        int totalUncertainCards = 0;
                        for (int j = 0; j < Game.NumPlayers + 1; j++)
                        {
                            if (_cardProbabilityForPlayer[j][b][h] > 0f &&
                                _cardProbabilityForPlayer[j][b][h] < 1f)
                            {
                                totalUncertainCards += uncertainCardNumbers[j];
                            }
                        }
                        _cardProbabilityForPlayer[i][b][h] = (float)uncertainCardNumbers[i] / totalUncertainCards;
                        /*if (h == Hodnota.Eso || h == Hodnota.Desitka || (trump.HasValue && b == trump.Value))
                        {
                            //v talonu nemuzou byt desitky a esa
                            _cardProbabilityForPlayer[i][b][h] = (float)uncertainCardNumbers[i] / (totalUncertainCards - uncertainCardNumbers[3]);
                        }
                        else
                        {
                            _cardProbabilityForPlayer[i][b][h] = (float)uncertainCardNumbers[i] / totalUncertainCards;
                        }*/
                    }
                }
            }
        }

        public float CardProbability(int playerIndex, Card c)
        {
            return _cardProbabilityForPlayer[playerIndex][c.Suit][c.Value];
        }

        public float SuitProbability(int playerIndex, Barva b, int roundNumber)
        {
            return 1 - NoSuitProbability(playerIndex, b, roundNumber);
        }

        public float NoSuitProbability(int playerIndex, Barva b, int roundNumber)
        {
            //if I know at least one card for certain the case is trivial
            if (playerIndex == _myIndex)
            {
                return _cardProbabilityForPlayer[playerIndex][b].Any(h => h.Value == 1f) ? 0f : 1f;
            }
            if (_cardProbabilityForPlayer[playerIndex][b].Any(h => h.Value == 1f))
            {
                return 0f;
            }

            //let n be the total amount of uncertain cards not in the given suit
            var n = _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != b).Sum(i =>i.Value.Count(h => h.Value > 0f && h.Value < 1f));
            var uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 1f));
            var certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value == 1f));
            var totalCards = 10 - roundNumber;

            return (float)CNK(n, totalCards - certainCards) / CNK(uncertainCards, totalCards - certainCards);
        }

        public float SuitHigherThanCardProbability(int playerIndex, Card c, int roundNumber)
        {
            return 1 - NoSuitHigherThanCardProbability(playerIndex, c, roundNumber);
        }

        public float NoSuitHigherThanCardProbability(int playerIndex, Card c, int roundNumber)
        {
            //if I know at least one card for certain the case is trivial
            if (playerIndex == _myIndex)
            {
                return _cardProbabilityForPlayer[playerIndex][c.Suit].Any(h => h.Key > c.Value && h.Value == 1f) ? 0f : 1f;
            }
            if (_cardProbabilityForPlayer[playerIndex][c.Suit].Any(h => h.Key > c.Value && h.Value == 1f))
            {
                return 0f;
            }

            //let n be the total amount of uncertain cards not in c.Suit plus cards in suit smaller than c.Value
            var n = _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 1f)) +
                    _cardProbabilityForPlayer[playerIndex][c.Suit].Where(i => i.Key < c.Value).Count(h => h.Value > 0f && h.Value < 1f);
            var uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 1f));
            var certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value == 1f));
            var totalCards = 10 - roundNumber;

            return (float)CNK(n, totalCards - certainCards) / CNK(uncertainCards, totalCards - certainCards);
        }

        private long CNK(int n, int k)
        {
            long numerator = 1;
            long denominator = 1;

            for (var i = 1; i <= k; i++)
            {
                numerator *= (n - i + 1);
                denominator *= i;
            }

            return numerator/denominator;
        }

        public long PossibleCombinations(int playerIndex, int roundNumber)
        {
            var certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value == 1f));
            var uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value > 0f && j.Value < 1f));
            var totalCards = 10 - roundNumber;

            return CNK(uncertainCards, totalCards - certainCards);
        }

        /// <summary>
        /// pokud ma nejaky hrac stejny pocet nejistych karet jako pocet nepridelenych karet, tak mu dam vse, co pro nej zbylo
        /// </summary>
        private void ReduceUcertainCardSet()
        {
            //pocet mych jistych karet je pocet karet ve hre pro kazdeho hrace
            var totalCards = _cardProbabilityForPlayer[_myIndex].SelectMany(i => i.Value).Count(i => i.Value == 1f);

            while (true)
            {
                bool reduced = false;

                for (int j = 0; j < Game.NumPlayers; j++)
                {
                    var certainCards = _cardProbabilityForPlayer[j].SelectMany(i => i.Value.Where(k => k.Value == 1f).Select(k => new Card(i.Key, k.Key))).ToList();
                    var uncertainCards = _cardProbabilityForPlayer[j].SelectMany(i => i.Value.Where(k => k.Value > 0f && k.Value < 1f).Select(k => new Card(i.Key, k.Key))).ToList();

                    if (uncertainCards.Any() && (totalCards - certainCards.Count() == uncertainCards.Count()))
                    {
                        //tento hrac ma stejne karet jako nejistych karet. Zmenime u nich pravdepodobnost na 1 a u ostatnich na 0
                        foreach (var uncertainCard in uncertainCards)
                        {
                            for (int k = 0; k < Game.NumPlayers + 1; k++)
                            {
                                _cardProbabilityForPlayer[k][uncertainCard.Suit][uncertainCard.Value] = j == k ? 1f : 0f;
                            }
                        }
                        reduced = true;
                    }
                }
                //opkuju tak dlouho dokud odebirame
                if (!reduced)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// pokud ma nejaky hrac stejny pocet nejistych karet jako pocet nepridelenych karet, tak mu dam vse, co pro nej zbylo
        /// </summary>
        private void ReduceUncertainCardSet(List<Card>[] hands, int[] totalCards, List<Card>[] uncertainCards)
        {
            while (true)
            {
                bool reduced = false;

                for (int j = 0; j < Game.NumPlayers; j++)
                {
                    if (uncertainCards[j].Any() && totalCards[j] - hands[j].Count() == uncertainCards[j].Count())
                    {
                        //danemu hraci musim uz dat vse co zbylo
                        for (int k = 0; k < uncertainCards[j].Count(); k++)
                        {
                            hands[j].Add(uncertainCards[j][k]);
                            //a u ostatnich tim padem tuhle kartu uz nesmim brat v potaz
                            for (int l = 0; l < Game.NumPlayers + 1; l++)
                            {
                                if (j != l)
                                {
                                    uncertainCards[l].Remove(uncertainCards[j][k]);
                                }
                            }
                        }
                        uncertainCards[j].Clear();
                        reduced = true;
                    }
                }
                //opkuju tak dlouho dokud odebirame
                if (!reduced)
                {
                    break;
                }
            }
        }

        public Hand[] GenerateHands(int roundNumber, int roundStarterIndex, Card c1 = null, Card c2 = null)
        {
            const int talonIndex = 3;
            //Pozor!!!: u vsech techto lokalnich promennych plati, ze index 0 == ja ale u _cardProbabilityForPlayer je _myIndex == ja
            var hands = new List<Card>[Game.NumPlayers + 1];
            var certainCards = new List<Card>[Game.NumPlayers + 1];
            var uncertainCards = new List<Card>[Game.NumPlayers + 1];
            var totalCards = new int[Game.NumPlayers + 1];
            var thresholds = new double[Game.NumPlayers + 1];

            //inicializujeme si promenne
            for (int i = 0; i < Game.NumPlayers; i++)
            {
                certainCards[i] = _cardProbabilityForPlayer[(_myIndex + i) % Game.NumPlayers].SelectMany(j => j.Value.Where(k => k.Value == 1f).Select(k => new Card(j.Key, k.Key))).ToList();
                uncertainCards[i] = _cardProbabilityForPlayer[(_myIndex + i) % Game.NumPlayers].SelectMany(j => j.Value.Where(k => k.Value > 0f && k.Value < 1f).Select(k => new Card(j.Key, k.Key))).ToList();
                totalCards[i] = 10 - roundNumber + 1;
                hands[i] = new List<Card>();
            }
            certainCards[talonIndex] = _cardProbabilityForPlayer[talonIndex].SelectMany(j => j.Value.Where(k => k.Value == 1f).Select(k => new Card(j.Key, k.Key))).ToList();
            uncertainCards[talonIndex] = _cardProbabilityForPlayer[talonIndex].SelectMany(j => j.Value.Where(k => k.Value > 0f && k.Value < 1f).Select(k => new Card(j.Key, k.Key))).ToList();
            totalCards[talonIndex] = 2;
            hands[talonIndex] = new List<Card>();

            //zacneme tim, ze rozdelime jiste karty
            for (int i = 0; i < Game.NumPlayers + 1; i++)
            {
                hands[i].AddRange(certainCards[i]);
            }
            ReduceUncertainCardSet(hands, totalCards, uncertainCards);
            //projdeme vsechny nejiste karty a zkusime je rozdelit podle pravdepodobnosti jednotlivym hracum (nebo do talonu)
            foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().OrderByDescending(h => h))
            {
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                {
                    var c = new Card(b, h);
                    if (!uncertainCards.Any(i => i.Contains(c)))
                    {
                        //toto neni karta na rozdeleni (je bud jista nebo odehrana)
                        continue;
                    }
                    //spocitame si pravdepodobnostni prahy
                    //pokud se hands[i].Count == totalCards[i] pak uz pro hrace nemame generovat karty => nastavime prah stejne jako predchozi, jinak ho zvysime o aktualni pravdepodobnost
                    for (int i = 0; i < Game.NumPlayers + 1; i++)
                    {
                        if (i > 0)
                        {
                            thresholds[i] = thresholds[i - 1];
                        }
                        else
                        {
                            thresholds[i] = 0;
                        }
                        if (hands[i].Count() < totalCards[i])
                        {
                            if (i == talonIndex)
                            {
                                thresholds[i] += _cardProbabilityForPlayer[talonIndex][b][h];
                            }
                            else
                            {
                                thresholds[i] += _cardProbabilityForPlayer[(_myIndex + i) % Game.NumPlayers][b][h];
                            }                            
                        } 
                    }
                    if (thresholds[talonIndex] != 1f)
                    {
                        //musime znormalizovat vsechny prahy, protoze nekomu uz jsme nagenerovali maximum moznych karet
                        var sum = thresholds[talonIndex];

                        for (int i = 0; i < Game.NumPlayers + 1; i++)
                        {
                            thresholds[i] /= sum;
                        }
                        //nyni by melo platit priblizne, ze thresholds[talonIndex] == 1f
                    }

                    //var n = r.NextDouble();
                    var n = mt.RandomDouble();

                    //podle toho do ktereho intervalu urceneho prahy se vejdeme pridelime kartu konkretnimu hraci
                    for (int i = Game.NumPlayers; i >= 0; i--)
                    {
                        if ((i == 0 || n >= thresholds[i - 1]) && n < thresholds[i])
                        {
                            hands[i].Add(c);
                            for (int j = 0; j < Game.NumPlayers + 1; j ++)
                            {
                                uncertainCards[j].Remove(c);
                            }
                            ReduceUncertainCardSet(hands, totalCards, uncertainCards);
                            break;
                        }
                    }
                }
            }
            var result = new Hand[Game.NumPlayers + 1];

            for (int i = 0; i < Game.NumPlayers; i++)
            {
                result[(_myIndex + i) % Game.NumPlayers] = new Hand(hands[i]);
            }
            result[talonIndex] = new Hand(hands[talonIndex]);

            var sb = new StringBuilder();
            for (var i = 0; i < Game.NumPlayers; i++)
            {
                sb.AppendFormat(" Player {0}: ", i + 1);
                sb.Append(result[i].ToString());
            }
            _log.DebugFormat("Finished generating hands for player{0}\n{1}", _myIndex + 1, sb.ToString());
            
            return result;
        }

        public void UpdateProbabilitiesAfterTalon(List<Card> talon)
        {
            const int talonIndex = 3;

            foreach (var card in talon)
            {
                _cardProbabilityForPlayer[_myIndex][card.Suit][card.Value] = 0f;
                _cardProbabilityForPlayer[talonIndex][card.Suit][card.Value] = 1f;
            }
        }

        public void UpdateProbabilitiesAfterGameTypeChosen(GameTypeChosenEventArgs e)
        {
            if (e.TrumpCard == null)
            {
                return;
            }

            for (var i = 0; i < Game.NumPlayers + 1; i++)
            {
                _cardProbabilityForPlayer[i][e.TrumpCard.Suit][e.TrumpCard.Value] = i == e.GameStartingPlayerIndex ? 1f : 0f;
                if ((e.GameType & Hra.Sedma) != 0)
                {
                    _cardProbabilityForPlayer[i][e.TrumpCard.Suit][Hodnota.Sedma] = i == e.GameStartingPlayerIndex ? 1f : 0f;
                }
            }
        }

        public void UpdateProbabilities(int roundNumber, int roundStarterIndex, Card c1, bool hlas1, Barva trump)
        {
            for (var i = 0; i < Game.NumPlayers + 1; i++)
            {
                _cardProbabilityForPlayer[i][c1.Suit][c1.Value] = i == roundStarterIndex ? 1f : 0f;
            }
            if (hlas1)
            {
                _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Kral] = 1f;
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Kral] = 0f;
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Kral] = 0f;
            }
            ReduceUcertainCardSet();
            UpdateUncertainCardsProbability(roundNumber, trump);
        }

        public void UpdateProbabilities(int roundNumber, int roundStarterIndex, Card c1, Card c2, bool hlas2)
        {
            for (var i = 0; i < Game.NumPlayers + 1; i++)
            {
                _cardProbabilityForPlayer[i][c2.Suit][c2.Value] = i == (roundStarterIndex + 1) % Game.NumPlayers ? 1f : 0f;
            }
            if (hlas2)
            {
                _cardProbabilityForPlayer[roundStarterIndex][c2.Suit][Hodnota.Kral] = 0f;
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c2.Suit][Hodnota.Kral] = 1f;
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c2.Suit][Hodnota.Kral] = 0f;
            }
            //ReduceUcertainCardSet();
            if (c2.Suit != c1.Suit || c2.Value < c1.Value)
            {
                if (c2.Suit == c1.Suit)
                {
                    SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 1) % Game.NumPlayers, c1);
                }
                else
                {
                    SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 1) % Game.NumPlayers, new Card(c1.Suit, Hodnota.Sedma));
                    if (c2.Suit != _trump)
                    {
                        SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 1) % Game.NumPlayers, new Card(_trump, Hodnota.Sedma));
                    }
                }
            }
            UpdateUncertainCardsProbability(roundNumber);
        }

        public void UpdateProbabilities(int roundNumber, int roundStarterIndex, Card c1, Card c2, Card c3, bool hlas3)
        {
            for (var i = 0;  i < Game.NumPlayers + 1; i++)
            {
                _cardProbabilityForPlayer[i][c1.Suit][c1.Value] = 0f;
                _cardProbabilityForPlayer[i][c2.Suit][c2.Value] = 0f;
                _cardProbabilityForPlayer[i][c3.Suit][c3.Value] = 0f;
            }
            if (hlas3)
            {
                _cardProbabilityForPlayer[roundStarterIndex][c3.Suit][Hodnota.Kral] = 0f;
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][Hodnota.Kral] = 0f;
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Kral] = 1f;
            }
            ReduceUcertainCardSet();
            if (c2.Suit != c1.Suit || c2.Value < c1.Value)
            {
                //druhy hrac nepriznal barvu nebo nesel vejs
                if (c3.Suit != c1.Suit || c3.Value < c1.Value)
                {
                    if (c3.Suit == c1.Suit)
                    {
                        if (c2.Suit != _trump)
                        {
                            //druhy hrac nehral trumf a treti hrac nesel vejs
                            SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 2) % Game.NumPlayers, c1);
                        }
                    }
                    else
                    {
                        //treti hrac nepriznal barvu
                        SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 2) % Game.NumPlayers, new Card(c1.Suit, Hodnota.Sedma));
                        if (c3.Suit != _trump)
                        {
                            //a navic nehral trumfem
                            SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 2) % Game.NumPlayers, new Card(_trump, Hodnota.Sedma));
                        }
                        else if (c2.Suit == _trump && c3.Value < c2.Value)
                        {
                            //druhy i treti hrac hrali trumfem ale treti hrac hral mensim trumfem nez druhy hrac
                            SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 2) % Game.NumPlayers, c2);
                        }
                    }
                }
            }
            else
            {
                //druhy hrac priznal barvu a sel vejs
                if (c3.Suit != c2.Suit || c3.Value < c2.Value)
                {
                    if (c3.Suit == c2.Suit)
                    {
                        SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 2) % Game.NumPlayers, c2);
                    }
                    else
                    {
                        SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 2) % Game.NumPlayers, new Card(c1.Suit, Hodnota.Sedma));
                        if (c3.Suit != _trump)
                        {
                            SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 2) % Game.NumPlayers, new Card(_trump, Hodnota.Sedma));
                        }
                    }
                }
            }
            UpdateUncertainCardsProbability(roundNumber);
        }

        private void SetCardProbabilitiesHigherThanCardToZero(int playerIndex, Card c)
        {
            var otherPlayerIndex = -1;

            if (playerIndex == _myIndex)
            {
                //no need to update my own probabilities (i know what cards i have)
                return;
            }

            for (var i = 0; i < Game.NumPlayers; i++)
            {
                if (i != _myIndex && i != playerIndex)
                {
                    otherPlayerIndex = i;
                    break;
                }
            }

            foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
            {
                if (h >= c.Value)
                {
                    _cardProbabilityForPlayer[playerIndex][c.Suit][h] = 0f;
                    //Pozor! Tenhle pristup ignoruje to, ze karta muze byt i v talonu ...
                    if (_cardProbabilityForPlayer[otherPlayerIndex][c.Suit][h] > 0f &&
                        _cardProbabilityForPlayer[otherPlayerIndex][c.Suit][h] < 1f &&
                        (h == Hodnota.Eso || h == Hodnota.Desitka))
                    {
                        _cardProbabilityForPlayer[otherPlayerIndex][c.Suit][h] = 1f;
                    }
                }
            }
        }

        public string FriendlyString(int playerIndex, int roundNumber)
        {
            var sb = new StringBuilder();

            foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().OrderByDescending(i => (int)i))
            {
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                {
                    sb.AppendFormat("{0} {1}:\t{2:0.00}\t", h, b, CardProbability(playerIndex, new Card(b, h)));
                }
                sb.Append("\n");
            }
            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                sb.AppendFormat("\n{0}:\t{1}", b, SuitProbability(playerIndex, b, roundNumber - 1));
            }

            sb.AppendFormat("\n\nPossible combinations:\t{0}", PossibleCombinations(playerIndex, roundNumber - 1));
            
            return sb.ToString();
        }
    }
}
