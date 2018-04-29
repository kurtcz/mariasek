using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Mariasek.Engine.New.Logger;
using MersenneTwister;
//using Newtonsoft.Json;

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
        private Barva? _trump;
        private Dictionary<Barva, Dictionary<Hodnota, float>>[] _cardProbabilityForPlayer;
        private List<Card>[] _cardsPlayedByPlayer;
        private static RandomMT mt = new RandomMT((ulong)(DateTime.Now - DateTime.MinValue).TotalMilliseconds);
        private List<int> _playerWeights;
        private List<Hand[]> generatedHands;
        private bool _allowAXTalon;
        private bool _allowTrumpTalon;
        public const int talonIndex = Game.NumPlayers;
        public bool UseDebugString { get; set; }

        //for debug purposes onlys
        private Hand _myHand;
        private List<Card> _talon;
        private List<Card> _myTalon;
        public IStringLogger _debugString;
        public IStringLogger _verboseString;
        public IStringLogger ExternalDebugString;
        private List<int> _gameBidders;
        private int _gameIndex;
        private int _sevenIndex;
        private int _sevenAgainstIndex;
        private int _hundredIndex;
        private int _hundredAgainstIndex;
        private float[] _initialExpectedTrumps;
        //private Dictionary<Hra, float> _initialExpectedTrumpsPerGameType = new Dictionary<Hra, float>() { { Hra.Hra, 3f }, { Hra.Sedma, 5f }, { Hra.Kilo, 7f}};
        private float _gameStarterCurrentExpectedTrumps = 3f;

        public Probability(int myIndex, int gameStarterIndex, Hand myHand, Barva? trump, bool allowAXTalon, bool allowTrumpTalon, Func<IStringLogger> stringLoggerFactory, List<Card> talon = null)
        {
            _myIndex = myIndex;
			_gameIndex = -1;
            _sevenIndex = -1;
            _sevenAgainstIndex = -1;
			_hundredIndex = -1;
            _hundredAgainstIndex = -1;
            _allowAXTalon = allowAXTalon;
            _allowTrumpTalon = allowTrumpTalon;
            _gameStarterIndex = gameStarterIndex;
            _trump = trump;
            _gameBidders = new List<int>();
			_playerWeights = new List<int>();
            _myHand = myHand;
            _talon = talon != null ? new List<Card>(talon) : null;
            _myTalon = talon != null ? new List<Card>(talon) : null;
            _debugString = stringLoggerFactory();
            _verboseString = stringLoggerFactory();
            ExternalDebugString = stringLoggerFactory();
			_debugString.AppendFormat("ctor\nhand:\n{0}\ntalon:\n{1}", 
				_myHand, _talon != null ? _talon.Any() ? string.Format("{0} {1}", _talon[0], _talon[1]) : "empty" : "null");
            var GenerateTalonProbabilities = talon == null;

            _gameBidders.Add(gameStarterIndex);
            _cardProbabilityForPlayer = new Dictionary<Barva, Dictionary<Hodnota, float>>[Game.NumPlayers + 1];
            _cardsPlayedByPlayer = new List<Card>[Game.NumPlayers];
            _initialExpectedTrumps = new float[Game.NumPlayers + 1];
            for (var i = 0; i < Game.NumPlayers + 1; i++)
            {
                _initialExpectedTrumps[i] = 0f;
                if (i < Game.NumPlayers)
                {
                    _cardsPlayedByPlayer[i] = new List<Card>();
                }
                _cardProbabilityForPlayer[i] = new Dictionary<Barva, Dictionary<Hodnota, float>>();
                foreach (var b in Enum.GetValues(typeof (Barva)).Cast<Barva>())
                {
                    _cardProbabilityForPlayer[i][b] = new Dictionary<Hodnota, float>();
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                    {
                        if (i == myIndex)
                        {
                            _cardProbabilityForPlayer[i][b].Add(h, _myHand.Any(k => k.Suit == b && k.Value == h) ? 1f : 0f);
                        }
                        else if (i == talonIndex)
                        {
                            if(!Game.IsValidTalonCard(h, b, trump, _allowAXTalon, _allowTrumpTalon))
                            {
                                //karty co nemohou byt v talonu
                                _cardProbabilityForPlayer[i][b].Add(h, 0f);
                            }
                            else if(talon != null)
                            {
                                //ten kdo voli vi co je v talonu (pokud tam uz neco je)
                                _cardProbabilityForPlayer[i][b].Add(h, _talon.Any(k => k.Suit == b && k.Value == h) ? 1f : 0f);
                            }
                            else
                            {
                                _cardProbabilityForPlayer[i][b].Add(h, _myHand.Any(k => k.Suit == b && k.Value == h) ? 0f : 0.5f);
                            }
                        }
                        else
                        {
                            _cardProbabilityForPlayer[i][b].Add(h, _myHand.Any(k => k.Suit == b && k.Value == h) ||
                                                                   (_talon != null &&
                                                                    _talon.Any(k => k.Suit == b && k.Value == h)) ? 0f : 0.5f);
                        }
                    }
                }
            }
            UpdateUncertainCardsProbability();
            _debugString.Append(FriendlyString(0));
            _debugString.Append("-----\n");
            Check();
        }

        public void Set(Hand[] hands)
        {
            for (var i = 0; i < _cardProbabilityForPlayer.Count(); i++)
            {
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                {
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                    {
                        _cardProbabilityForPlayer[i][b][h] = hands[i].Count(j => j.Suit == b && j.Value == h);
                    }
                }
            }            
        }

        public Probability Clone()
        {
            var clone = (Probability)MemberwiseClone();

            clone._cardsPlayedByPlayer = new List<Card>[_cardsPlayedByPlayer.Length];
            clone._cardProbabilityForPlayer = new Dictionary<Barva, Dictionary<Hodnota, float>>[Game.NumPlayers + 1];

            for (var i = 0; i < _cardsPlayedByPlayer.Length; i++)
            {
                clone._cardsPlayedByPlayer[i] = new List<Card>();
                for (var j = 0; j < _cardsPlayedByPlayer[i].Count; j++)
                {
                    clone._cardsPlayedByPlayer[i].Add(_cardsPlayedByPlayer[i][j]);
                }
            }
            for (var i = 0; i < _cardProbabilityForPlayer.Count(); i++)
            {
                clone._cardProbabilityForPlayer[i] = new Dictionary<Barva, Dictionary<Hodnota, float>>();
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                {
                    clone._cardProbabilityForPlayer[i][b] = new Dictionary<Hodnota, float>();
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                    {
                        clone._cardProbabilityForPlayer[i][b][h] = _cardProbabilityForPlayer[i][b][h];
                    }
                }
            }
            if (_myTalon != null)
            {
                clone._myTalon = new List<Card>();
                foreach (var c in _myTalon)
                {
                    clone._myTalon.Add(c);
                }
            }
            clone.generatedHands = generatedHands;
            clone._playerWeights = _playerWeights;
            clone._gameBidders = _gameBidders;

            return clone; 
        }

        /// <summary>
        /// Changes the probability of uncertain cards from 0.5 to the actual probability
        /// </summary>
        private void UpdateUncertainCardsProbability()
        {
            if (_initialExpectedTrumps.All(i => i == 0) && _trump.HasValue)
            {
                _initialExpectedTrumps[_myIndex] = _myHand.CardCount(_trump.Value);
                for (var i = 0; i < Game.NumPlayers + 1; i++)
                {
                    if (i != _myIndex && i < Game.NumPlayers)
                    {
                        _initialExpectedTrumps[i] = (8 - _initialExpectedTrumps[_myIndex]) / 2f;
                    }
                }
            }
            for (var i = 0; i < Game.NumPlayers + 1; i++)
            {
                const float epsilon = 0.01f;
                const float small = 0.1f;
                var ii = (_gameStarterIndex + i) % (Game.NumPlayers + 1);
                if (ii == _myIndex)
                {
                    continue;
                }
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                {
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().Where(h => _cardProbabilityForPlayer[ii][b][h] > epsilon &&
                                                                                                 _cardProbabilityForPlayer[ii][b][h] < 1 - epsilon))
                    {
                        //totalUncertainCards is computed only over those players who can have such card (with probability 0.5)
                        var totalUncertainCards = _cardProbabilityForPlayer.Count(j => j[b][h] > 0f && j[b][h] < 1f);
                        var ratio = 1f / totalUncertainCards;

                        //pokud nekdo jiny hlasil hru, sedmu nebo kilo, zvednu mu pravdepodobnosti trumfovych karet
                        //ostatnim naopak pravdepodobnosti snizim
                        if (_trump.HasValue && b == _trump.Value)
                        {
                            var playedTrumps = ii != talonIndex ? _cardsPlayedByPlayer[ii].Count(j => j.Suit == b) : 0;
                            var certainTrumps = _cardProbabilityForPlayer[ii][b].Count(j => j.Value == 1);//pocet jistych trumfu pro daneho hrace
                            var totalUncertainTrumps = _cardProbabilityForPlayer.SelectMany(j => j[b].Where(k => k.Value > 0f && k.Value < 1f)
                                                                                                     .Select(k => k.Key))
                                                                                .Distinct().Count();//celkovy pocet nejistych trumfu pro vsechny
                            var currentExpectedTrumps = 0f;

                            if (ii == _gameStarterIndex)
                            {
                                currentExpectedTrumps = _initialExpectedTrumps[ii] - certainTrumps - playedTrumps;
                                _gameStarterCurrentExpectedTrumps = currentExpectedTrumps;
                            }
                            else if (ii != talonIndex)
                            {
                                if (_myIndex == _gameStarterIndex)
                                {
                                    currentExpectedTrumps = _initialExpectedTrumps[ii] - certainTrumps - playedTrumps;
                                }
                                else
                                {
                                    currentExpectedTrumps = totalUncertainTrumps - _gameStarterCurrentExpectedTrumps;
                                }
                            }
                            else
                            {
                                currentExpectedTrumps = 0;
                            }
                            ratio = (float)currentExpectedTrumps / totalUncertainTrumps;
                            //nemuzeme pouzit epsilon, protoze sance, ze volici hrac uz nema dalsi trumfy
                            //v zavislosti na typu hry a tom kolik trumfu uz odehral musi byt vetsi
                            //pravidla pocitajici s epsilonem by jinak mohla vest ke spatnym rozhodnutim
                            if (ratio > 1 - small)
                            {
                                ratio = 1 - small;
                            }
                            else if (ratio < small)
                            {
                                ratio = small;
                            }
                        }
                        _cardProbabilityForPlayer[ii][b][h] = ratio;
                    }
                }
            }
        }

        public float CardProbability(int playerIndex, Card c)
        {
            return _cardProbabilityForPlayer[playerIndex][c.Suit][c.Value];
        }

        public int MaxCardCount(int playerIndex, Barva b)
        {
            return _cardProbabilityForPlayer[playerIndex][b].Count(p => p.Value > 0.01f);
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
            if (_cardProbabilityForPlayer[playerIndex][b].Any(h => h.Value >= 0.9f))
            {
                return 0f;
            }

            //let n be the total amount of uncertain cards not in the given suit
            var n = _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != b).Sum(i =>i.Value.Count(h => h.Value > 0f && h.Value < 0.9f));
            var uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.9f));
            var certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value >= 0.9f));

            var totalCards = 10 - roundNumber + 1;

            if (n == 0)
            {
                //no uncertain cardsin other suits, however
                //the result is based on assumed cards, not on actual game play
                //let's use actual card probabilities in this situation
                //so that we can distinguish between actual and assumed suit probabilities

                n = _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != b).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f));
                uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f));
                certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value >= 0.99f));
            }
            return (float)CNK(n, totalCards - certainCards) / (float)CNK(uncertainCards, totalCards - certainCards);
        }

        //public float CardHigherThanCardProbability(int playerIndex, Card c, int roundNumber)
        //{
        //    if (_cardProbabilityForPlayer[playerIndex].Any(i => i.Value.Any(j => j.Value == 1f &&
        //                                                                         c.IsLowerThan(new Card(i.Key, j.Key), _trump))))
        //    {
        //        return 1f;
        //    }

        //    var uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 1f));
        //    var certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value == 1f));
        //    var totalCards = 10 - roundNumber + 1;
        //    var hiCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value > 0f &&
        //                                                                                     c.IsLowerThan(new Card(i.Key, j.Key), _trump)));
        //    var hiCardsInSuit = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value > 0f &&
        //                                                                                      c.Suit == i.Key &&
        //                                                                                      c.Value < j.Key));
        //    var trumps = _cardProbabilityForPlayer[playerIndex][_trump.Value].Count(i => i.Value > 0f);

        //    return (float)(CNK(uncertainCards, totalCards - certainCards) - CNK(uncertainCards - hiCards, totalCards - certainCards)) /
        //           (float)CNK(uncertainCards, totalCards - certainCards);
        //}

        public float SuitHigherThanCardProbability(int playerIndex, Card c, int roundNumber, bool goodGame = true)
        {
            //if (_cardProbabilityForPlayer[playerIndex][c.Suit].Where(h => h.Key > c.Value).Any(h => h.Value == 1f))
            //{
            //    return 1f;
            //}
            //if (_cardProbabilityForPlayer[playerIndex][c.Suit].Where(h => h.Key > c.Value).All(h => h.Value == 0f))
            //{
            //    return 0f;
            //}

            //var uncertainHighCardsInSuit = _cardProbabilityForPlayer[playerIndex][c.Suit].Count(h => h.Key > c.Value && h.Value > 0f && h.Value < 1f);
            //var x = (float)((1 << uncertainHighCardsInSuit) - 1) / (float)(1 << uncertainHighCardsInSuit);
            var y = 1 - NoSuitHigherThanCardProbability(playerIndex, c, roundNumber, goodGame);

            return y;
        }

        public float NoSuitHigherThanCardProbability(int playerIndex, Card c, int roundNumber, bool goodGame = true)
        {
            //if I know at least one card for certain the case is trivial
            if (playerIndex == _myIndex)
            {
                if (goodGame)
                {
                    return _cardProbabilityForPlayer[playerIndex][c.Suit].Any(h => h.Key > c.Value && h.Value == 1f) ? 0f : 1f;
                }
                else
                {
                    return _cardProbabilityForPlayer[playerIndex][c.Suit].Any(h => Card.GetBadValue(h.Key) > c.BadValue && h.Value == 1f) ? 0f : 1f;
                }
            }
            if ((goodGame && _cardProbabilityForPlayer[playerIndex][c.Suit].Any(h => h.Key > c.Value && h.Value >= 0.9f)) ||
                (!goodGame && _cardProbabilityForPlayer[playerIndex][c.Suit].Any(h => Card.GetBadValue(h.Key) > c.BadValue && h.Value >= 0.9f)))
            {
                return 0f;
            }

            //let n be the total amount of uncertain cards not in c.Suit plus cards in suit smaller than c.Value
            var n = goodGame
                    ? _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.9f)) +
                      _cardProbabilityForPlayer[playerIndex][c.Suit].Where(i => i.Key < c.Value).Count(h => h.Value > 0f && h.Value < 0.9f)
                    : _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.9f)) +
                      _cardProbabilityForPlayer[playerIndex][c.Suit].Where(i => Card.GetBadValue(i.Key) < c.BadValue).Count(h => h.Value > 0f && h.Value < 0.9f);
            var uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.9f));
            var certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value >= 0.9f));
            var totalCards = 10 - roundNumber + 1;

            if (n == 0)
            {
                n = goodGame
                    ? _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f)) +
                      _cardProbabilityForPlayer[playerIndex][c.Suit].Where(i => i.Key < c.Value).Count(h => h.Value > 0f && h.Value < 0.99f)
                    : _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f)) +
                      _cardProbabilityForPlayer[playerIndex][c.Suit].Where(i => Card.GetBadValue(i.Key) < c.BadValue).Count(h => h.Value > 0f && h.Value < 0.99f);
                uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f));
                certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value >= 0.99f));
            }
            return (float)CNK(n, totalCards - certainCards) / (float)CNK(uncertainCards, totalCards - certainCards);
        }

        public float HlasProbability(int playerIndex)
        {
            var p = new float[Game.NumSuits];

            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                p[(int)b] = _cardProbabilityForPlayer[playerIndex][b][Hodnota.Kral] *
                            _cardProbabilityForPlayer[playerIndex][b][Hodnota.Svrsek];
            }

            return + p[0] + p[1] + p[2] + p[3]
                   - p[0] * p[1] - p[0] * p[2] - p[0] * p[3] - p[1] * p[2] - p[1] * p[3] - p[2] * p[3]
                   + p[0] * p[1] * p[2] + p[0] * p[1] * p[3] + p[0] * p[2] * p[3] + p[1] * p[2] * p[3]
                   - p[0] * p[1] * p[2] * p[3];
        }

        public bool CertainlyHasAOrXAndNothingElseInSuit(int playerIndex, Barva b)
        {
            bool AXpossible = false;
            bool othersPossible = false;

            foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
            {
                if (_cardProbabilityForPlayer[playerIndex][b][h] > 0)
                {
                    if ((h == Hodnota.Eso || h == Hodnota.Desitka))
                    {
                        AXpossible = true;
                    }
                    else
                    {
                        othersPossible = true;
                    }
                }
            }

            return AXpossible && !othersPossible;
        }

        public float HasSolitaryX(int playerIndex, Barva suit, int roundNumber)
        {
            return AnyOfTheseCardsButNothingElseInSuitProbability(playerIndex, suit, roundNumber, Hodnota.Desitka);
        }

        public float HasAOrXAndNothingElse(int playerIndex, Barva suit, int roundNumber)
        {
            var y = AnyOfTheseCardsButNothingElseInSuitProbability(playerIndex, suit, roundNumber, Hodnota.Eso, Hodnota.Desitka);

            return y;
        }

        private float AnyOfTheseCardsButNothingElseInSuitProbability(int playerIndex, Barva suit, int roundNumber, params Hodnota[] values)
        {
            //pokud ma hrac jiste kartu ktera neni na seznamu, tak vrat nulu
            if (_cardProbabilityForPlayer[playerIndex][suit].Any(i => !values.Contains(i.Key) && i.Value == 1f))
            {
                return 0f;
            }
            //pokud hrac jiste nema ani jednu z karet na seznamu, tak vrat nulu
            if (_cardProbabilityForPlayer[playerIndex][suit].All(i => !values.Contains(i.Key) || i.Value == 0f))
            {
                return 0f;
            }

            var listedCertainCardsInSuit = values.Count(h =>_cardProbabilityForPlayer[playerIndex][suit][h] == 1f);
            var uncertainCardsInSuit = _cardProbabilityForPlayer[playerIndex][suit].Count(i => i.Value > 0f && i.Value < 1f);

            //pokud hrac nema zadne nejiste karty v barve tak vrat podle toho jestli ma v barve nejake karty jiste nebo ne
            if (uncertainCardsInSuit == 0)
            {
                return listedCertainCardsInSuit > 0 ? 1f : 0f;
            }

            //hrac ma nejake nejiste karty v barve
            var uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(b => b.Value.Count(h => h.Value > 0f && h.Value < 1f));
            var uncertainCardsNotInSuit = uncertainCards - uncertainCardsInSuit;
            var certainCards = _cardProbabilityForPlayer[playerIndex].Sum(b => b.Value.Count(h => h.Value == 1f));
            var totalCards = 10 - roundNumber + 1;
            var unknownCards = totalCards - certainCards;
            var numerator = 0f;
            var n = values.Length - listedCertainCardsInSuit;

            //pokud mam jiste nejakou z karet na seznamu, potom je pravdepodobnost rovna pravdepodobnosti, ze nemam zadnou z karet co neni na seznamu
            if (listedCertainCardsInSuit > 0)
            {
                return CNK(uncertainCardsNotInSuit, unknownCards) / (float)CNK(uncertainCards, unknownCards);
            }

            //do citatele dame vsechny k-tice ktere hrac v dane barve muze mit a do jmenovatele vsechny kombinace
            for (var k = 1; k <= n && k <= unknownCards; k++)
            {
                numerator += CNK(n, k) * CNK(uncertainCardsNotInSuit, unknownCards - k);
            }

            return numerator / (float)CNK(uncertainCards, unknownCards);
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
            var totalCards = 10 - roundNumber + 1;

            return CNK(uncertainCards, totalCards - certainCards);
        }

        /// <summary>
        /// pokud ma nejaky hrac stejny pocet nejistych karet jako pocet nepridelenych karet, tak mu dam vse, co pro nej zbylo
        /// </summary>
        private void ReduceUcertainCardSet()
        {
            //pocet mych jistych karet je pocet karet ve hre pro kazdeho hrace
            var tc = _cardProbabilityForPlayer[_myIndex].SelectMany(i => i.Value).Count(i => i.Value == 1f);
            var totalCards = new int[] { tc, tc, tc, 2 };
			_verboseString.Append("ReduceUcertainCardSet -> Enter\n");
			
            while (true)
            {
                bool reduced = false;

                for (int j = 0; j < Game.NumPlayers + 1; j++)
                {
                    var certainCards = _cardProbabilityForPlayer[j].SelectMany(i => i.Value.Where(k => k.Value == 1f).Select(k => new Card(i.Key, k.Key))).ToList();
                    var uncertainCards = _cardProbabilityForPlayer[j].SelectMany(i => i.Value.Where(k => k.Value > 0f && k.Value < 1f).Select(k => new Card(i.Key, k.Key))).ToList();

                    if (uncertainCards.Any() && (totalCards[j] - certainCards.Count() == uncertainCards.Count()))
                    {
                        //tento hrac ma stejne karet jako nejistych karet. Zmenime u nich pravdepodobnost na 1 a u ostatnich na 0
                        foreach (var uncertainCard in uncertainCards)
                        {
                            for (int k = 0; k < Game.NumPlayers + 1; k++)
                            {
                                _cardProbabilityForPlayer[k][uncertainCard.Suit][uncertainCard.Value] = j == k ? 1f : 0f;
								_verboseString.AppendFormat("Player{0}[{1}][{2}] = {3}\n",
									k + 1, uncertainCard.Suit, uncertainCard.Value, _cardProbabilityForPlayer[k][uncertainCard.Suit][uncertainCard.Value]);
                            }
                        }
                        reduced = true;
                    }
                    if(certainCards.Count() == totalCards[j])
                    {
                        foreach (var uncertainCard in uncertainCards)
                        {
                            if (_cardProbabilityForPlayer[j][uncertainCard.Suit][uncertainCard.Value] > 0f &&
                                _cardProbabilityForPlayer[j][uncertainCard.Suit][uncertainCard.Value] < 1f)
                            {
                                _cardProbabilityForPlayer[j][uncertainCard.Suit][uncertainCard.Value] = 0f;
                                reduced = true;
								_verboseString.AppendFormat("Player{0}[{1}][{2}] = {3}\n",
									j + 1, uncertainCard.Suit, uncertainCard.Value, _cardProbabilityForPlayer[j][uncertainCard.Suit][uncertainCard.Value]);
                            }
                        }
                    }
                }
                //opakuju tak dlouho dokud odebirame
                if (!reduced)
                {
                    break;
                }
            }
			_verboseString.Append("ReduceUcertainCardSet <- Exit\n");
        }

        /// <summary>
        /// pokud ma nejaky hrac stejny pocet nejistych karet jako pocet nepridelenych karet, tak mu dam vse, co pro nej zbylo
        /// </summary>
        private void ReduceUncertainCardSet(List<Card>[] hands, int[] totalCards, List<Card>[] uncertainCards)
        {
			_verboseString.Append("ReduceUncertainCardSet -> Enter\n");
			//Pozor!!!: u vsech techto vstupnich promennych plati, ze index 0 == ja ale u _cardProbabilityForPlayer je _myIndex == ja
			while (true)
            {
                bool reduced = false;

                for (int j = 0; j < Game.NumPlayers + 1; j++)
                {
                    var certainCards = j == talonIndex
                    ? _cardProbabilityForPlayer[talonIndex].Sum(i => i.Value.Count(k => k.Value == 1f))
                    : _cardProbabilityForPlayer[(_myIndex + j) % Game.NumPlayers].Sum(i => i.Value.Count(k => k.Value == 1f));
                    if (uncertainCards[j].Any() && totalCards[j] - hands[j].Count() == uncertainCards[j].Count())
                    {
						_verboseString.AppendFormat("Hand{0}: {1} total, {2} certain+generated so far, {3} uncertain remaining\n",
						                            (_myIndex + j) % Game.NumPlayers + 1, totalCards[j], hands[j].Count(), uncertainCards[j].Count());
                        //danemu hraci musim uz dat vse co zbylo
						for (int k = uncertainCards[j].Count() - 1; k >= 0 ; k--)
                        {
                            var c = uncertainCards[j][k];
                            hands[j].Add(c);
							_verboseString.AppendFormat("Hand{0} += {1} (rest)\n",  (_myIndex + j) % Game.NumPlayers + 1, c);
                            //a u ostatnich tim padem tuhle kartu uz nesmim brat v potaz
                            for (int l = 0; l < Game.NumPlayers + 1; l++)
                            {
                                uncertainCards[l].Remove(c);
								if (l < Game.NumPlayers)
								{
									_verboseString.AppendFormat("Uncertain: Player{0}[{1}][{2}] = 0\n",
										(_myIndex + l) % Game.NumPlayers + 1, c.Suit, c.Value);
								}
								else
								{
									_verboseString.AppendFormat("Uncertain: Talon[{0}][{1}] = 0\n",
										c.Suit, c.Value);
								}
                            }
                        }
                        uncertainCards[j].Clear();
                        reduced = true;
                    }
                    if (certainCards == totalCards[j] && uncertainCards[j].Any())
                    {
                        uncertainCards[j].Clear();
                        reduced = true;
                    }
                }
                //opakuju tak dlouho dokud odebirame
                if (!reduced)
                {
                    break;
                }
            }
			_verboseString.Append("ReduceUncertainCardSet <- Exit\n");
        }

        public float Deviation
        {
            get
            {
                if(generatedHands == null || !generatedHands.Any())
                {
                    return float.NaN;
                }

                var differences = new List<double>();

                //spocitej odchylku pro kazdou kartu
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                {
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                    {
                        for(var i = 0; i < Game.NumPlayers + 1; i++)
                        {
                            var cardCount = generatedHands.Count(j => j[i].Any(k => k.Suit == b && k.Value == h));
                            var cardDiffSq = (_cardProbabilityForPlayer[i][b][h] - cardCount / generatedHands.Count()) *
                                             (_cardProbabilityForPlayer[i][b][h] - cardCount / generatedHands.Count());

                            differences.Add(cardDiffSq);
                        }
                    }
                }

                //vrat prumernou odchulku pro vsechny karty
                return (float)Math.Sqrt(differences.Sum(i => i) / generatedHands.Count());
            }
        }

        public IEnumerable<Hand[]> GenerateHands(int roundNumber, int roundStarterIndex, int maxGenerations)
        {
            generatedHands = new List<Hand[]>();
            for(var i = 0; i < maxGenerations; i++)
            {
                var hands = GenerateHands(roundNumber, roundStarterIndex);

                generatedHands.Add(hands);

                yield return hands;
            }
        }

        public Hand[] GenerateHands(int roundNumber, int roundStarterIndex)
        {
            //Pozor!!!: u vsech techto lokalnich promennych plati, ze index 0 == ja ale u _cardProbabilityForPlayer je _myIndex == ja
            var hands = new List<Card>[Game.NumPlayers + 1];
            var certainCards = new List<Card>[Game.NumPlayers + 1];
            var uncertainCards = new List<Card>[Game.NumPlayers + 1];
            var totalCards = new int[Game.NumPlayers + 1];
            var thresholds = new [] { 0f, 0f, 0f, 0f };// new float[Game.NumPlayers + 1];
            var badThreshold = string.Empty;

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

            _verboseString.Clear();
            _verboseString.AppendFormat("GenerateHands(round: {0}, starting: Player{1} -> Enter\n", roundNumber, roundStarterIndex + 1);
			//zacneme tim, ze rozdelime jiste karty
			for (int i = 0; i < Game.NumPlayers; i++)
            {
                hands[i].AddRange(certainCards[i]);
				foreach(var c in certainCards[i])
                {
					_verboseString.AppendFormat("Hand{0} += {1} (certain)\n", (_myIndex + i) % Game.NumPlayers + 1, c);
                }
				_verboseString.AppendFormat("Player {0}: {1} total, {2} certain, {3} uncertain cards\n",
					(_myIndex + i) % Game.NumPlayers + 1, totalCards[i], certainCards[i].Count(), uncertainCards[i].Count());
			}
			hands[talonIndex].AddRange(certainCards[talonIndex]);
			foreach (var c in certainCards[talonIndex])
			{
				_verboseString.AppendFormat("Talon += {0} (certain)\n", c);
			}
			_verboseString.AppendFormat("Talon: {0} total, {1} certain, {2} uncertain cards\n",
                totalCards[talonIndex], certainCards[talonIndex].Count(), uncertainCards[talonIndex].Count());
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
						_verboseString.AppendFormat("{0} skipped\n", c);
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
                        if (hands[i].Count() < totalCards[i] && i > 0)
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
							if (thresholds[i] == sum)
							{
								thresholds[i] = 1f;
							}
							else
							{
								thresholds[i] /= sum;
							}
                        }
                        //nyni by melo platit, ze thresholds[talonIndex] == 1f
                    }

                    //var n = r.NextDouble();
                    var n = mt.RandomDouble();

                    //podle toho do ktereho intervalu urceneho prahy se vejdeme pridelime kartu konkretnimu hraci
                    for (int i = Game.NumPlayers; i >= 0; i--)
                    {
                        if (i == 0)
                        {
                            badThreshold += string.Format("Bad thresholds for card {0}:\n{1:0.000} {2:0.000} {3:0.000} {4:0.000}\n" +
                                                          "card probabilities:\n{5:0.000} {6:0.000} {7:0.000} {8:0.000}\n" +
                                                          "uncertain cards:\n[{9}]\n[{10}]\n[{11}]\n[{12}]\n",
                                                          c, thresholds[0], thresholds[1], thresholds[2], thresholds[3],
                                                          _cardProbabilityForPlayer[0][c.Suit][c.Value],
                                                          _cardProbabilityForPlayer[1][c.Suit][c.Value],
                                                          _cardProbabilityForPlayer[2][c.Suit][c.Value],
                                                          _cardProbabilityForPlayer[3][c.Suit][c.Value],
                                                          string.Join(" ", uncertainCards[0].Select(j => j.ToString())),
                                                          string.Join(" ", uncertainCards[1].Select(j => j.ToString())),
                                                          string.Join(" ", uncertainCards[2].Select(j => j.ToString())),
                                                          string.Join(" ", uncertainCards[3].Select(j => j.ToString())));
                        }
                        if ((i == 0) || (n >= thresholds[i - 1] && n < thresholds[i]))
                        {
                            //i == 0 by nemelo nikdy nastat (thresholds[0] je prah pro me a ma byt == 0 (moje karty nejsou nezname)
                            hands[i].Add(c);
							if (i == Game.NumPlayers)
							{
                                _verboseString.AppendFormat("Talon += {0} (random) n = {1:0.000}; {2:0.000}:{3:0.000}:{4:0.000}:{5:0.000}\n",
                                                            c, n, thresholds[0], thresholds[1], thresholds[2], thresholds[3]);
							}
							else
							{
                                _verboseString.AppendFormat("Hand{0} += {1} (random) n = {2:0.000}; {3:0.000}:{4:0.000}:{5:0.000}:{6:0.000}\n", 
                                                            (_myIndex + i) % Game.NumPlayers + 1, c, n,
                                                            thresholds[0], thresholds[1], thresholds[2], thresholds[3]);
							}
                            for (int j = 0; j < Game.NumPlayers + 1; j ++)
                            {
                                uncertainCards[j].Remove(c);
								if (i == j)
								{
									continue;
								}
								if (j == Game.NumPlayers)
								{
									_verboseString.AppendFormat("Uncertain:Talon[{0}][{1}] = 0\n", c.Suit, c.Value);
								}
								else
								{
									_verboseString.AppendFormat("Uncertain:Player{0}[{1}][{2}] = 0\n", (_myIndex + j) % Game.NumPlayers + 1, c.Suit, c.Value);
								}
                            }
                            ReduceUncertainCardSet(hands, totalCards, uncertainCards);
                            break;
                        }
                    }
                }
            }
            //vsechny karty jsou nahodne rozdelene mezi hrace a talon
            var result = new Hand[Game.NumPlayers + 1];

            for (int i = 0; i < Game.NumPlayers; i++)
            {
                result[(_myIndex + i) % Game.NumPlayers] = new Hand(hands[i]);
            }
            result[talonIndex] = new Hand(hands[talonIndex]);

            var sb = new StringBuilder();
            for (var i = 0; i < Game.NumPlayers; i++)
            {
                sb.AppendFormat("\nPlayer {0}: ", i + 1);
                sb.Append(result[i].ToString());
            }
            sb.AppendFormat("\nTalon: {0}", result[talonIndex].ToString());
            //zkontroluj vysledek
            var check = true;
            for (int i = 0; i < Game.NumPlayers; i++)
            {
                if (roundNumber <= 1 &&                     
                    hands[i].Count != 12 &&
                    hands[i].Count != 10)   //pri uvodnim generovani musi mit nekdo vic karet, talon vybirame az posleze
                {
                    check = false;
                    break;
                }
                else if (roundNumber > 1 && hands[i].Count != 10 - roundNumber + 1)
                {
                    check = false;
                    break;
                }
            }
            if(hands[talonIndex].Count != 0 && hands[talonIndex].Count != 2)
            {
                check = false;
            }
            if (!check)
            {
                var friendlyString = new StringBuilder();

                friendlyString.AppendFormat("My hand:\n{0}\n", _myHand);
                friendlyString.AppendFormat("Talon:\n{0}\n", _talon == null ? "null" : _talon.Count() == 0 ? "empty" : string.Format("{0} {1}", _talon[0], _talon[1]));
                for (var i = 0; i < Game.NumPlayers + 1; i++)
                {
                    friendlyString.AppendFormat("Player{0}'s probabilities for {1}:\n{2}\n",
                        _myIndex+1, i < Game.NumPlayers ? string.Format("Player{0}", i+1) : "talon", FriendlyString(i, roundNumber));
                }

                throw new InvalidOperationException(string.Format("Badly generated hands for player {0}, round {1}:{2}\n{3}{4}\nGenerovani:\n{5}\nHistorie:\n{6}\nExterni:\n{7}\n", 
                              _myIndex + 1, roundNumber, sb.ToString(), badThreshold, friendlyString.ToString(), _verboseString.ToString(), _debugString.ToString(), ExternalDebugString.ToString()));
            }
            _verboseString.Append("-=-=-\n");
            _debugString.Append("-=-=-\n");
            for (var i = 0; i < Game.NumPlayers + 1; i++)
            {
                _verboseString.AppendFormat("{0}\n", result[i]);
                _debugString.AppendFormat("{0}\n", result[i]);
            }
            _log.DebugFormat("Finished generating hands for player{0}\n{1}", _myIndex + 1, sb.ToString());
			_verboseString.Append("GenerateHands <- Exit\n");

            return result;
        }

        private void Check()
        {
            //var certainCards = new List<List<Card>>();
            //
            //for (var j = 0; j < Game.NumPlayers; j++)
            //{
            //    certainCards.Add(_cardProbabilityForPlayer[j].SelectMany(i => i.Value.Where(k => k.Value == 1f).Select(k => new Card(i.Key, k.Key))).ToList());
            //}
            //if (certainCards[0].Count() != certainCards[1].Count || certainCards[0].Count() != certainCards[2].Count())
            //{
            //    if (certainCards[_gameStarterIndex].Count != 12)
            if (_cardProbabilityForPlayer[_myIndex].Sum(i => i.Value.Count(j => j.Value == 1f)) == 11)
            {
                var probs = new StringBuilder();
                for (var i = 0; i < Game.NumPlayers; i++)
                {
                    probs.AppendFormat("probabilities for player{0}:\n", i + 1, FriendlyString(i, 1));
                }

                var msg = string.Format("Bad certain card probabilities\n{3}Generovani:{0}\nHistorie:{1}\nExterni:{2}\n",
                                            _verboseString.ToString(), _debugString.ToString(), ExternalDebugString.ToString(),
                                        probs);
                throw new InvalidOperationException(msg);
            }
            //}
        }

        public bool IsUpdateProbabilitiesAfterTalonNeeded()
        {
            return _myIndex == _gameStarterIndex && (_myTalon == null || _myTalon.Count() == 0);
        }

        public void UpdateProbabilitiesAfterTalon(List<Card> hand, List<Card> talon)
        {
            if (UseDebugString)
            {
                _debugString.AppendFormat("After talon: {0} {1}\n", talon[0], talon[1]);
            }
            _myTalon = new List<Card>(talon);
            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                {
                    _cardProbabilityForPlayer[talonIndex][b][h] = 0f;
                }
            }
            foreach (var card in hand)
            {
                for(var i = 0; i < Game.NumPlayers + 1; i++)
                {
                    _cardProbabilityForPlayer[i][card.Suit][card.Value] = 0f;
                }
                _cardProbabilityForPlayer[_myIndex][card.Suit][card.Value] = 1f;
            }
            foreach (var card in talon)
            {
                for (var i = 0; i < Game.NumPlayers + 1; i++)
                {
                    _cardProbabilityForPlayer[i][card.Suit][card.Value] = 0f;
                }
                _cardProbabilityForPlayer[talonIndex][card.Suit][card.Value] = 1f;
            }
            if (UseDebugString)
            {
                _debugString.Append(FriendlyString(0));
                _debugString.Append("-----\n");
            }
            Check();
        }

        public void UpdateProbabilitiesAfterGameFlavourChosen(GameFlavourChosenEventArgs e)
        {
			_debugString.AppendFormat("GameFlavourchosen: {0} {1}\n", e.Player.Name, e.Flavour);
			if (e.Flavour == GameFlavour.Bad)
			{
				_trump = null;
				if(e.Player.PlayerIndex != _myIndex && _myTalon != null)
				{
					_gameBidders.Add(e.Player.PlayerIndex);
					foreach (var card in _myTalon)
					{
						foreach (var gameBidder in _gameBidders)
						{
							_cardProbabilityForPlayer[gameBidder][card.Suit][card.Value] = gameBidder == _myIndex ? 0f : 0.5f;
						}
						_cardProbabilityForPlayer[talonIndex][card.Suit][card.Value] = 0.5f;
					}
                    foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                    {
                        foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                        {
                            _cardProbabilityForPlayer[talonIndex][b][h] = _cardProbabilityForPlayer[_myIndex][b][h] == 1 ||
                                                                          !Game.IsValidTalonCard(h, b, _trump, _allowAXTalon, _allowTrumpTalon) 
                                                                          ? 0 : 0.5f;
                        }
                    }
					UpdateUncertainCardsProbability();
				}
			}
            if (UseDebugString)
            {
                _debugString.Append(FriendlyString(0));
                _debugString.Append("-----\n");
            }
            Check();
        }

        public void UpdateProbabilitiesAfterGameTypeChosen(GameTypeChosenEventArgs e)
        {
            Check();
            if (UseDebugString)
            {
                _debugString.AppendFormat("GameTypeChosen {0} {1}\n", e.GameType, e.TrumpCard);
                _debugString.Append(FriendlyString(0));
                _debugString.Append("===");
            }
            if (e.TrumpCard == null)
            {
                return;
            }

            _trump = e.TrumpCard.Suit;
            for (var i = 0; i < e.axTalon.Count(); i++)
            {
                for (var j = 0; j < Game.NumPlayers + 1; j++)
                {
                    _cardProbabilityForPlayer[j][e.axTalon[i].Suit][e.axTalon[i].Value] = j == talonIndex ? 1f : 0f;
                }
            }
            if ((e.GameType & (Hra.Betl | Hra.Durch)) == 0 && e.GameStartingPlayerIndex != _myIndex)
            {
                //v talonu nesmi byt ostre karty vyjma tech hlasenych
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                {
                    var eso = new Card(b, Hodnota.Eso);
                    var desitka = new Card(b, Hodnota.Desitka);

                    for (var i = 0; i < Game.NumPlayers + 1; i++)
                    {
                        if (i == talonIndex)
                        {
                            _cardProbabilityForPlayer[i][b][Hodnota.Eso] = e.axTalon.Contains(eso) ? 1f : 0f;
                            _cardProbabilityForPlayer[i][b][Hodnota.Desitka] = e.axTalon.Contains(desitka) ? 1f : 0f;
                        }
                        else if (i != _myIndex)
                        {
                            _cardProbabilityForPlayer[i][b][Hodnota.Eso] = e.axTalon.Contains(eso) || _myHand.HasA(b)
                                                                            ? 0f
                                                                            : 0.5f;
                            _cardProbabilityForPlayer[i][b][Hodnota.Desitka] = e.axTalon.Contains(eso) || _myHand.HasX(b)
                                                                                ? 0f
                                                                                : 0.5f;
                        }
                    }
                }
            }
            for (var i = 0; i < Game.NumPlayers + 1; i++)
            {
                _cardProbabilityForPlayer[i][e.TrumpCard.Suit][e.TrumpCard.Value] = i == e.GameStartingPlayerIndex ? 1f : 0f;
                if ((e.GameType & Hra.Sedma) != 0)
                {
                    _cardProbabilityForPlayer[i][e.TrumpCard.Suit][Hodnota.Sedma] = i == e.GameStartingPlayerIndex ? 1f : 0f;
                }
            }
            if ((e.GameType & Hra.Sedma) != 0)
            {
                _sevenIndex = e.GameStartingPlayerIndex;
            }
            if ((e.GameType & Hra.Kilo) != 0)
            {
                _hundredIndex = e.GameStartingPlayerIndex;
            }
            else if((e.GameType & Hra.Hra) != 0)
            {
                _gameIndex = e.GameStartingPlayerIndex;
            }
            if (e.GameStartingPlayerIndex != _myIndex && _trump.HasValue)
            {
                if (_hundredIndex >= 0)
                {
                    _initialExpectedTrumps[e.GameStartingPlayerIndex] = GetGameStarterInitialExpectedTrumps(Hra.Kilo);
                }
                else if (_sevenIndex >= 0)
                {
                    _initialExpectedTrumps[e.GameStartingPlayerIndex] = GetGameStarterInitialExpectedTrumps(Hra.Sedma);
                }
                else
                {
                    _initialExpectedTrumps[e.GameStartingPlayerIndex] = GetGameStarterInitialExpectedTrumps(Hra.Hra);
                }
                for (var i = 0; i < Game.NumPlayers; i++)
                {
                    if (i != _myIndex && i != e.GameStartingPlayerIndex)
                    {
                        _initialExpectedTrumps[i] = 8 - _initialExpectedTrumps[_myIndex] - _initialExpectedTrumps[e.GameStartingPlayerIndex];
                    }
                }
            }
            UpdateUncertainCardsProbability();
            if (UseDebugString)
            {
                _debugString.Append(FriendlyString(0));
                _debugString.Append("-----\n");
            }
            Check();
        }

        private float GetGameStarterInitialExpectedTrumps(Hra gameType)
        {
            var myInitialTrumpCount = _myHand.CardCount(_trump.Value);

            switch (gameType)
            {
                case Hra.Kilo:
                    return Math.Min(6, 8 - myInitialTrumpCount);
                case Hra.Sedma:
                    return Math.Min(5, 8 - myInitialTrumpCount);
                default:
                    return Math.Min(4, 8 - myInitialTrumpCount);
            }
        }

        public void UpdateProbabilitiesAfterBidMade(BidEventArgs e, Bidding bidding)
        {
            if ((e.BidMade & Hra.SedmaProti) != 0 && bidding.SevenAgainstMultiplier == 1)
            {
                _sevenAgainstIndex = e.Player.PlayerIndex;
                _initialExpectedTrumps[_sevenAgainstIndex] = GetNonStarterInitialExpectedTrumps(Hra.SedmaProti);
                for (var i = 0; i < Game.NumPlayers + 1; i++)
                {
                    _cardProbabilityForPlayer[i][_trump.Value][Hodnota.Sedma] = i == _sevenAgainstIndex ? 1 : 0;
                    if (i != _myIndex && i != _sevenAgainstIndex && i != _gameStarterIndex && i != talonIndex)
                    {
                        _initialExpectedTrumps[i] = 8 - _initialExpectedTrumps[_gameStarterIndex] - _initialExpectedTrumps[_sevenAgainstIndex];
                    }
                }
            }
            if ((e.BidMade & Hra.KiloProti) != 0 && bidding.HundredAgainstMultiplier == 1)
            {
                _hundredAgainstIndex = e.Player.PlayerIndex;
                _initialExpectedTrumps[_hundredAgainstIndex] = GetNonStarterInitialExpectedTrumps(Hra.KiloProti);
                for (var i = 0; i < Game.NumPlayers; i++)
                {
                    if (i != _myIndex && i != _hundredAgainstIndex && i != _gameStarterIndex)
                    {
                        _initialExpectedTrumps[i] = 8 - _initialExpectedTrumps[_gameStarterIndex] - _initialExpectedTrumps[_hundredAgainstIndex];
                    }
                }
            }
            UpdateUncertainCardsProbability();
            if (UseDebugString)
            {
                _debugString.Append(FriendlyString(0));
                _debugString.Append("-----\n");
            }
        }

        private float GetNonStarterInitialExpectedTrumps(Hra gameType)
        {
            if (_myIndex == _sevenAgainstIndex || _myIndex == _hundredAgainstIndex)
            {
                return _myHand.CardCount(_trump.Value);
            }
            if (_myIndex != _gameStarterIndex)
            {
                return 8 - _initialExpectedTrumps[_myIndex] - _initialExpectedTrumps[_gameStarterIndex];
            }
            //_myIndex == _gameStarterIndex
            switch (gameType)
            {
                case Hra.KiloProti:
                    return Math.Min(6, 8 - _initialExpectedTrumps[_myIndex]);
                case Hra.SedmaProti:
                    return Math.Min(4, 8 - _initialExpectedTrumps[_myIndex]);
                default:
                    return Math.Min(2, 8 - _initialExpectedTrumps[_myIndex]);
            }
        }

        public void UpdateProbabilities(int roundNumber, int roundStarterIndex, Card c1, bool hlas1)
        {
            if (UseDebugString)
            {
                _debugString.AppendFormat("Round {0}: starting {1}, card1: {2}, hlas: {3}\n", roundNumber, roundStarterIndex + 1, c1, hlas1);
            }
            for (var i = 0; i < Game.NumPlayers + 1; i++)
            {
                _cardProbabilityForPlayer[i][c1.Suit][c1.Value] = i == roundStarterIndex ? 1f : 0f;
            }
            if (hlas1)
            {
                _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Kral] = 1f;
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Kral] = 0f;
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Kral] = 0f;
                _cardProbabilityForPlayer[talonIndex][c1.Suit][Hodnota.Kral] = 0f;
            }
            else if (_trump.HasValue && c1.Value == Hodnota.Svrsek && roundStarterIndex != _myIndex)
            {
                _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Kral] = 0;
                if (!_allowTrumpTalon && c1.Suit == _trump)
                {
                    //pokud trumfovy hlas a tedy i krale nema tento hrac a nesmi byt v talonu
                    //a dalsi hrac ho mit muze (protoze ja ho nemam), tak ho ma jiste
                    var otherPlayer = Enumerable.Range(0, Game.NumPlayers).First(i => i != _myIndex && i != roundStarterIndex);

                    if (_cardProbabilityForPlayer[otherPlayer][c1.Suit][Hodnota.Kral] > 0)
                    {
                        _cardProbabilityForPlayer[otherPlayer][c1.Suit][Hodnota.Kral] = 1f;
                    }
                }
            }
            _cardsPlayedByPlayer[roundStarterIndex].Add(c1);
            ReduceUcertainCardSet();
            UpdateUncertainCardsProbability();
            if (UseDebugString)
            {
                _debugString.Append(FriendlyString(roundNumber));
                _debugString.Append("-----\n");
            }
        }

        public void UpdateProbabilities(int roundNumber, int roundStarterIndex, Card c1, Card c2, bool hlas2)
        {
            if (UseDebugString)
            {
                _debugString.AppendFormat("Round {0}: starting {1}, card1: {2}, card2: {3}, hlas: {4}\n", roundNumber, roundStarterIndex + 1, c1, c2, hlas2);
            }
            for (var i = 0; i < Game.NumPlayers + 1; i++)
            {
                _cardProbabilityForPlayer[i][c2.Suit][c2.Value] = i == (roundStarterIndex + 1) % Game.NumPlayers ? 1f : 0f;
            }
            if (hlas2)
            {
                _cardProbabilityForPlayer[roundStarterIndex][c2.Suit][Hodnota.Kral] = 0f;
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c2.Suit][Hodnota.Kral] = 1f;
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c2.Suit][Hodnota.Kral] = 0f;
                _cardProbabilityForPlayer[talonIndex][c2.Suit][Hodnota.Kral] = 0f;
            }
            else if (_trump.HasValue && c2.Value == Hodnota.Svrsek && (roundStarterIndex + 1) % Game.NumPlayers != _myIndex)
            {
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c2.Suit][Hodnota.Kral] = 0;
                if (!_allowTrumpTalon && c2.Suit == _trump)
                {
                    //pokud trumfovy hlas a tedy i krale nema tento hrac a nesmi byt v talonu
                    //a dalsi hrac ho mit muze (protoze ja ho nemam), tak ho ma jiste
                    var otherPlayer = Enumerable.Range(0, Game.NumPlayers).First(i => i != _myIndex && i != (roundStarterIndex + 1) % Game.NumPlayers);

                    if (_cardProbabilityForPlayer[otherPlayer][c2.Suit][Hodnota.Kral] > 0)
                    {
                        _cardProbabilityForPlayer[otherPlayer][c2.Suit][Hodnota.Kral] = 1f;
                    }
                }
            }
			const float epsilon = 0.01f;

			if (_trump.HasValue &&
                c1.Suit != _trump &&
                c1.Suit == c2.Suit &&
                c2.Value > c1.Value &&
                c2.Value != Hodnota.Desitka &&
                c2.Value != Hodnota.Eso &&
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] > 0 &&
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] < 1 &&
                roundStarterIndex == _gameStarterIndex)
            {
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] = epsilon;
                if (_myIndex == roundStarterIndex)
                {
					_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] = 1 - epsilon;
				}
                else
                {
					_cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Desitka] = 1 - epsilon;
				}
            }
			if (_trump.HasValue &&
				c1.Suit != _trump &&
				c1.Suit == c2.Suit &&
				c2.Value > c1.Value &&
				c2.Value != Hodnota.Eso &&
                _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Desitka] == 0 &&
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] == 0 &&
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] == 0 &&
				_cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Eso] > 0 &&
				_cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Eso] < 1 &&
				roundStarterIndex == _gameStarterIndex)
			{
				_cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Eso] = epsilon;
                if (_myIndex == roundStarterIndex)
                {
					_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Eso] = 1 - epsilon;
				}
                else
                {
					_cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Eso] = 1 - epsilon;
				}
			}
			_cardsPlayedByPlayer[(roundStarterIndex + 1) % Game.NumPlayers].Add(c2);
            ReduceUcertainCardSet();
            if (c2.Suit != c1.Suit || c2.IsLowerThan(c1, _trump))
            {
                //druhy hrac nepriznal barvu nebo nesel vejs
                if (c2.Suit == c1.Suit)
                {
                    //druhy hrac nesel vejs
                    SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 1) % Game.NumPlayers, c1);
                }
                else
                {
                    //druhy hrac nepriznal barvu
					SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 1) % Game.NumPlayers, new Card(c1.Suit, Hodnota.Sedma));
                    if (_trump.HasValue && c2.Suit != _trump.Value)
                    {
						SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 1) % Game.NumPlayers, new Card(_trump.Value, Hodnota.Sedma));
                    }
                }
            }
            UpdateUncertainCardsProbability();
            if (UseDebugString)
            {
                _debugString.Append(FriendlyString(roundNumber));
                _debugString.Append("-----\n");
            }
        }

        public void UpdateProbabilities(int roundNumber, int roundStarterIndex, Card c1, Card c2, Card c3, bool hlas3)
        {
            if (UseDebugString)
            {
                _debugString.AppendFormat("Round {0}: starting {1}, card1: {2}, card2: {3}, card3: {4}, hlas: {5}\n", roundNumber, roundStarterIndex + 1, c1, c2, c3, hlas3);
            }
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
                _cardProbabilityForPlayer[talonIndex][c3.Suit][Hodnota.Kral] = 0f;
            }
            else if (_trump.HasValue && c3.Value == Hodnota.Svrsek && (roundStarterIndex + 2) % Game.NumPlayers != _myIndex)
            {
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Kral] = 0;
                if (!_allowTrumpTalon && c3.Suit == _trump)
                {
                    //pokud trumfovy hlas a tedy i krale nema tento hrac a nesmi byt v talonu
                    //a dalsi hrac ho mit muze (protoze ja ho nemam), tak ho ma jiste
                    var otherPlayer = Enumerable.Range(0, Game.NumPlayers).First(i => i != _myIndex && i != (roundStarterIndex + 2) % Game.NumPlayers);

                    if (_cardProbabilityForPlayer[otherPlayer][c3.Suit][Hodnota.Kral] > 0)
                    {
                        _cardProbabilityForPlayer[otherPlayer][c3.Suit][Hodnota.Kral] = 1f;
                    }
                }
            }
			const float epsilon = 0.01f;

			if (_trump.HasValue &&
                c1.Suit != _trump &&
                c1.Suit == c3.Suit &&
                c3.Value > c1.Value &&
                c3.Value != Hodnota.Desitka &&
                c3.Value != Hodnota.Eso &&
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] > 0 &&
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] < 1)
            {
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] = epsilon;
                if (_myIndex == roundStarterIndex)
                {
                    _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] = 1 - epsilon;
				}
                else
                {
					_cardProbabilityForPlayer[roundStarterIndex][c3.Suit][Hodnota.Desitka] = 1 - epsilon;
				}
            }
			if (_trump.HasValue &&
				c1.Suit != _trump &&
				c1.Suit == c3.Suit &&
				c3.Value > c1.Value &&
				c3.Value != Hodnota.Eso &&
                _cardProbabilityForPlayer[roundStarterIndex][c3.Suit][Hodnota.Desitka] == 0 &&
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] == 0 &&
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] == 0 &&
				_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Eso] > 0 &&
				_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Eso] < 1)
			{
				_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Eso] = epsilon;
                if (_myIndex == roundStarterIndex)
                {
					_cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][Hodnota.Eso] = 1 - epsilon;
				}
                else
                {
					_cardProbabilityForPlayer[roundStarterIndex][c3.Suit][Hodnota.Eso] = 1 - epsilon;
				}
			}
			_cardsPlayedByPlayer[(roundStarterIndex + 2) % Game.NumPlayers].Add(c3);
            ReduceUcertainCardSet();
            if (c2.Suit != c1.Suit || c2.IsLowerThan(c1, _trump))
            {
                //druhy hrac nepriznal barvu nebo nesel vejs
                if (c3.Suit != c1.Suit || c3.IsLowerThan(c1, _trump))
                {
                    //treti hrac nepriznal barvu nebo nesel vejs
                    if (c3.Suit == c1.Suit)
                    {
                        //treti hrac nesel vejs
                        if (!_trump.HasValue || c1.Suit == _trump.Value || c2.Suit != _trump.Value)
                        {
                            //druhy hrac nepriznal barvu ani nehral trumf a treti hrac nesel vejs
                            SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 2) % Game.NumPlayers, c1);
                        }
                    }
                    else
                    {
                        //treti hrac nepriznal barvu
                        SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 2) % Game.NumPlayers, new Card(c1.Suit, Hodnota.Sedma));
                        if (_trump.HasValue && c3.Suit != _trump.Value)
                        {
                            //a navic nehral trumfem
                            SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 2) % Game.NumPlayers, new Card(_trump.Value, Hodnota.Sedma));
                        }
                        else if (_trump.HasValue && c2.Suit == _trump.Value && c3.IsLowerThan(c2, _trump))
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
                if (c3.Suit != c2.Suit || c3.IsLowerThan(c2, _trump))
                {
                    //treti hrac nepriznal barvu nebo nesel vejs
                    if (c3.Suit == c2.Suit)
                    {
                        //treti hrac nesel vejs
                        SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 2) % Game.NumPlayers, c2);
                    }
                    else
                    {
                        //treti hrac nepriznal barvu
                        SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 2) % Game.NumPlayers, new Card(c1.Suit, Hodnota.Sedma));
                        if (_trump.HasValue && c3.Suit != _trump.Value)
                        {
                            SetCardProbabilitiesHigherThanCardToZero((roundStarterIndex + 2) % Game.NumPlayers, new Card(_trump.Value, Hodnota.Sedma));
                        }
                    }
                }
            }
            UpdateUncertainCardsProbability();
            if (UseDebugString)
            {
                _debugString.Append(FriendlyString(roundNumber));
                _debugString.Append("-----\n");
            }
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
                var c2 = new Card(c.Suit, h);

                if (!c.IsHigherThan(c2, _trump))
                {
                    _cardProbabilityForPlayer[playerIndex][c2.Suit][h] = 0f;
                    //pokud uz nikdo jiny kartu mit nemuze, tak upravim pravdepodobnosti
                    if (_cardProbabilityForPlayer[otherPlayerIndex][c2.Suit][h] > 0f &&
                        _cardProbabilityForPlayer[otherPlayerIndex][c2.Suit][h] < 1f &&
                        _cardProbabilityForPlayer[_myIndex][c2.Suit][h] == 0f &&
                        _cardProbabilityForPlayer[talonIndex][c2.Suit][h] == 0f)
                    {
                        _cardProbabilityForPlayer[otherPlayerIndex][c2.Suit][h] = 1f;
                    }
                    else if (_cardProbabilityForPlayer[otherPlayerIndex][c2.Suit][h] == 0f &&
                        _cardProbabilityForPlayer[_myIndex][c2.Suit][h] == 0f &&
                        _cardProbabilityForPlayer[talonIndex][c2.Suit][h] > 0f &&
                        _cardProbabilityForPlayer[talonIndex][c2.Suit][h] < 1f)
                    {
                        _cardProbabilityForPlayer[talonIndex][c2.Suit][h] = 1f;
                    }
                }
            }
        }

		public string FriendlyString(int roundNumber)
		{
			var friendlyString = new StringBuilder();

			friendlyString.AppendFormat("My hand:\n{0}\n", _myHand);
			friendlyString.AppendFormat("Talon:\n{0}\n", _talon == null ? "null" : _talon.Count() == 0 ? "empty" : string.Format("{0} {1}", _talon[0], _talon[1]));
			for (var i = 0; i < Game.NumPlayers + 1; i++)
			{
				friendlyString.AppendFormat("Player{0}'s probabilities for {1}:\n{2}\n",
					_myIndex+1, i < Game.NumPlayers ? string.Format("Player{0}", i+1) : "talon", FriendlyString(i, roundNumber));
			}

			return friendlyString.ToString();
		}

        public string FriendlyString(int playerIndex, int roundNumber)
        {
            var sb = new StringBuilder();

			foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {                
				foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().OrderByDescending(i => (int)i))
                {
                    sb.AppendFormat("{0} {1}:\t{2:0.000}\t", h, b, CardProbability(playerIndex, new Card(b, h)));
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
