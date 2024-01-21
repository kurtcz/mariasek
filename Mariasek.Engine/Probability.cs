using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using Combinatorics.Collections;
using Mariasek.Engine.Logger;
using MersenneTwister;
//using Newtonsoft.Json;

namespace Mariasek.Engine
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
        private bool _allowFakeSeven;
        private bool _allowAXTalon;
        private bool _allowTrumpTalon;
        public bool UseDebugString { get; set; }
        private CancellationToken _cancellationToken;

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
        private Hra _gameType;

        public Probability(int myIndex, int gameStarterIndex, Hand myHand, Barva? trump, bool allowFakeSeven, bool allowAXTalon, bool allowTrumpTalon, CancellationToken cancellationToken, Func<IStringLogger> stringLoggerFactory, List<Card> talon = null)
        {
            _myIndex = myIndex;
        	_gameIndex = -1;
            _sevenIndex = -1;
            _sevenAgainstIndex = -1;
			_hundredIndex = -1;
            _hundredAgainstIndex = -1;
            _allowFakeSeven = allowFakeSeven;
            _allowAXTalon = allowAXTalon;
            _allowTrumpTalon = allowTrumpTalon;
            _gameStarterIndex = gameStarterIndex;
            _trump = trump;
            _gameBidders = new List<int>();
			_playerWeights = new List<int>();
            _myHand = new Hand((List<Card>)myHand);
            _talon = talon != null ? new List<Card>(talon) : null;
            _myTalon = talon != null ? new List<Card>(talon) : null;
            _cancellationToken = cancellationToken;
            _debugString = stringLoggerFactory();
            _verboseString = stringLoggerFactory();
            ExternalDebugString = stringLoggerFactory();
			_debugString.AppendFormat("ctor\nhand:\n{0}\ntalon:\n{1}", 
				_myHand, _talon != null ? _talon.Any() ? string.Format("{0} {1}", _talon[0], _talon[1]) : "empty" : "null");
            var GenerateTalonProbabilities = _talon == null;

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
                        else if (i == Game.TalonIndex)
                        {
                            if(!Game.IsValidTalonCard(h, b, trump, _allowAXTalon, _allowTrumpTalon))
                            {
                                //karty co nemohou byt v talonu
                                _cardProbabilityForPlayer[i][b].Add(h, 0f);
                            }
                            else if(_talon != null)
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
            clone._myHand = new Hand((List<Card>)_myHand);
            //clone.generatedHands = generatedHands != null ? new List<Hand[]>(generatedHands) : null; //not a truly deep clone (that would be too slow)
            clone._playerWeights = new List<int>(_playerWeights);
            clone._gameBidders = new List<int>(_gameBidders);

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

                        //pokud nekdo jiny hlasil sedmu nebo kilo, zvednu mu pravdepodobnosti trumfovych karet
                        //ostatnim naopak pravdepodobnosti snizim
                        if (_trump.HasValue && b == _trump.Value && (_gameType & (Hra.Kilo | Hra.Sedma)) != 0)
                        {
                            var playedTrumps = ii != Game.TalonIndex ? _cardsPlayedByPlayer[ii].Count(j => j.Suit == b) : 0;
                            var certainTrumps = _cardProbabilityForPlayer[ii][b].Count(j => j.Value == 1);//pocet jistych trumfu pro daneho hrace
                            var totalUncertainTrumps = _cardProbabilityForPlayer.SelectMany(j => j[b].Where(k => k.Value > 0f && k.Value < 1f)
                                                                                                     .Select(k => k.Key))
                                                                                .Distinct().Count();//celkovy pocet nejistych trumfu pro vsechny
                            var currentExpectedTrumps = 0f;

                            if (ii != Game.TalonIndex &&
                                _cardsPlayedByPlayer[ii].Where(j => j.Suit == b)
                                                        .Any(j => _cardProbabilityForPlayer[ii][b][j.Value] == 1))
                            {
                                //pokud hrac vyjel trumfem v aktualnim kole tak je karta zapocitana i v playedTrumps i v certainTrumps, proto musime playedTrumps snizit
                                playedTrumps--;
                            }
                            if (ii == _gameStarterIndex)
                            {
                                currentExpectedTrumps = _initialExpectedTrumps[ii] - certainTrumps - playedTrumps;
                                _gameStarterCurrentExpectedTrumps = currentExpectedTrumps;
                            }
                            else if (ii != Game.TalonIndex)
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
                    if ((_gameType & Hra.Kilo) != 0 &&
                        _myIndex != _hundredIndex &&
                        ((b == _trump.Value &&
                          _cardProbabilityForPlayer[_hundredIndex][_trump.Value][Hodnota.Kral] > 0 &&
                          _cardProbabilityForPlayer[_hundredIndex][_trump.Value][Hodnota.Svrsek] > 0) || 
                         (b != _trump.Value &&
                          (_cardProbabilityForPlayer[_hundredIndex][_trump.Value][Hodnota.Kral] == 0 ||
                           _cardProbabilityForPlayer[_hundredIndex][_trump.Value][Hodnota.Svrsek] == 0))))
                    {
                        if (_cardProbabilityForPlayer[ii][b][Hodnota.Kral] > 0 &&
                            _cardProbabilityForPlayer[ii][b][Hodnota.Kral] < 1)
                        {
                            _cardProbabilityForPlayer[ii][b][Hodnota.Kral] = ii == _hundredIndex ? 0.9f : 0.05f;
                        }
                        if (_cardProbabilityForPlayer[ii][b][Hodnota.Svrsek] > 0 &&
                            _cardProbabilityForPlayer[ii][b][Hodnota.Svrsek] < 1)
                        {
                            _cardProbabilityForPlayer[ii][b][Hodnota.Svrsek] = ii == _hundredIndex ? 0.9f : 0.05f;
                        }
                    }
                    foreach (var h in new[] { Hodnota.Eso, Hodnota.Desitka })
                    {
                        if ((_gameType & Hra.Kilo) != 0 &&
                            _myIndex != _hundredIndex &&
                            _cardProbabilityForPlayer[ii][b][h] > epsilon &&
                            _cardProbabilityForPlayer[ii][b][h] < 1 - epsilon)
                        {
                            _cardProbabilityForPlayer[ii][b][h] = ii == _hundredIndex ? 0.9f : 0.05f;
                        }
                    }
                    if ((_gameType & Hra.KiloProti) != 0 &&
                        _myIndex != _hundredAgainstIndex &&
                        _hundredAgainstIndex != -1 &&
                        ((b == _trump.Value &&
                          _cardProbabilityForPlayer[_hundredAgainstIndex][_trump.Value][Hodnota.Kral] > 0 &&
                          _cardProbabilityForPlayer[_hundredAgainstIndex][_trump.Value][Hodnota.Svrsek] > 0) ||
                         (b != _trump.Value &&
                          (_cardProbabilityForPlayer[_hundredAgainstIndex][_trump.Value][Hodnota.Kral] == 0 ||
                           _cardProbabilityForPlayer[_hundredAgainstIndex][_trump.Value][Hodnota.Svrsek] == 0))))
                    {
                        if (_cardProbabilityForPlayer[ii][b][Hodnota.Kral] > 0 &&
                            _cardProbabilityForPlayer[ii][b][Hodnota.Kral] < 1)
                        {
                            _cardProbabilityForPlayer[ii][b][Hodnota.Kral] = ii == _hundredAgainstIndex ? 0.9f : 0.05f;
                        }
                        if (_cardProbabilityForPlayer[ii][b][Hodnota.Svrsek] > 0 &&
                            _cardProbabilityForPlayer[ii][b][Hodnota.Svrsek] < 1)
                        {
                            _cardProbabilityForPlayer[ii][b][Hodnota.Svrsek] = ii == _hundredAgainstIndex ? 0.9f : 0.05f;
                        }
                    }
                }
            }
        }

        public Card[] CertainCards(int playerIndex)
        {
            return CardsBetweenThresholds(playerIndex, 0.99f, 1f);
        }

        public Card[] LikelyCards(int playerIndex)
        {
            return CardsBetweenThresholds(playerIndex, 0.9f, 1f);
        }

        public Card[] PotentialCards(int playerIndex)
        {
            return CardsBetweenThresholds(playerIndex, 0.01f, 1f, false);
            //return CardsBetweenThresholds(playerIndex, 0.1f, 1f, false);
        }

        public Card[] UnlikelyCards(int playerIndex)
        {
            return CardsBetweenThresholds(playerIndex, 0f, 0.01f);
            //return CardsBetweenThresholds(playerIndex, 0f, 0.1f);
        }

        private Card[] CardsBetweenThresholds(int playerIndex, float min, float max, bool minInclusive = true, bool maxInclusive = true)
        {
            return _cardProbabilityForPlayer[playerIndex].SelectMany(i => i.Value
                                                                           .Where(j => (minInclusive ? j.Value >= min : j.Value > min) &&
                                                                                       (maxInclusive ? j.Value <= max : j.Value < max))
                                                                           .Select(j => new Card(i.Key, j.Key))
                                                                           .ToList()).ToArray();
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
            var y = 1 - NoSuitProbability(playerIndex, b, roundNumber);

            return y;
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

            if (n == 0 || totalCards < certainCards)
            {
                //no uncertain cardsin other suits, however
                //the result is based on assumed cards, not on actual game play
                //let's use actual card probabilities in this situation
                //so that we can distinguish between actual and assumed suit probabilities

                n = _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != b).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f));
                uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f));
                certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value >= 0.99f));
            }
            if (n < totalCards - certainCards)
            {
                return 0;
            }
            if (n > 0 && totalCards == certainCards)
            {
                n = _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != b).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f));
                uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f));
                certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value >= 0.99f));
            }
            return (float)CNK(n, totalCards - certainCards) / (float)CNK(uncertainCards, totalCards - certainCards);
        }

        public float SuitHigherThanCardProbability(int playerIndex, Card c, int roundNumber, bool goodGame = true)
        {
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
                      _cardProbabilityForPlayer[playerIndex][c.Suit].Where(i => i.Key <= c.Value).Count(h => h.Value > 0f && h.Value < 0.9f)
                    : _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.9f)) +
                      _cardProbabilityForPlayer[playerIndex][c.Suit].Where(i => Card.GetBadValue(i.Key) <= c.BadValue).Count(h => h.Value > 0f && h.Value < 0.9f);
            var uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.9f));
            var certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value >= 0.9f));
            var totalCards = 10 - roundNumber + 1;

            if (n == 0)
            {
                n = goodGame
                    ? _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f)) +
                      _cardProbabilityForPlayer[playerIndex][c.Suit].Where(i => i.Key <= c.Value).Count(h => h.Value > 0f && h.Value < 0.99f)
                    : _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f)) +
                      _cardProbabilityForPlayer[playerIndex][c.Suit].Where(i => Card.GetBadValue(i.Key) <= c.BadValue).Count(h => h.Value > 0f && h.Value < 0.99f);
                uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f));
                certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value >= 0.99f));
            }
            if (n < totalCards - certainCards)
            {
                return 0;
            }
            return (float)CNK(n, totalCards - certainCards) / (float)CNK(uncertainCards, totalCards - certainCards);
        }

        public float NoSuitOrSuitLowerThanXProbability(int playerIndex, Barva b, int roundNumber, bool goodGame = true)
        {
            var y = SuitLowerThanCardProbability(playerIndex, new Card(b, Hodnota.Desitka), roundNumber);
            var z = NoSuitProbability(playerIndex, b, roundNumber);

            return y > z ? y : z;
        }

        public float SuitLowerThanCardProbability(int playerIndex, Card c, int roundNumber, bool goodGame = true)
        {
            var y = 1 - NoSuitLowerThanCardProbability(playerIndex, c, roundNumber, goodGame);

            return y;
        }

        public float NoSuitLowerThanCardProbability(int playerIndex, Card c, int roundNumber, bool goodGame = true)
        {
            //if I know at least one card for certain the case is trivial
            if (playerIndex == _myIndex)
            {
                if (goodGame)
                {
                    return _cardProbabilityForPlayer[playerIndex][c.Suit].Any(h => h.Key < c.Value && h.Value == 1f) ? 0f : 1f;
                }
                else
                {
                    return _cardProbabilityForPlayer[playerIndex][c.Suit].Any(h => Card.GetBadValue(h.Key) < c.BadValue && h.Value == 1f) ? 0f : 1f;
                }
            }
            if ((goodGame && _cardProbabilityForPlayer[playerIndex][c.Suit].Any(h => h.Key < c.Value && h.Value >= 0.9f)) ||
                (!goodGame && _cardProbabilityForPlayer[playerIndex][c.Suit].Any(h => Card.GetBadValue(h.Key) < c.BadValue && h.Value >= 0.9f)))
            {
                return 0f;
            }

            //let n be the total amount of uncertain cards not in c.Suit plus cards in suit bigger than c.Value
            var n = goodGame
                    ? _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.9f)) +
                      _cardProbabilityForPlayer[playerIndex][c.Suit].Where(i => i.Key >= c.Value).Count(h => h.Value > 0f && h.Value < 0.9f)
                    : _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.9f)) +
                      _cardProbabilityForPlayer[playerIndex][c.Suit].Where(i => Card.GetBadValue(i.Key) >= c.BadValue).Count(h => h.Value > 0f && h.Value < 0.9f);
            var uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.9f));
            var certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value >= 0.9f));
            var totalCards = 10 - roundNumber + 1;

            if (n == 0)
            {
                n = goodGame
                    ? _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f)) +
                      _cardProbabilityForPlayer[playerIndex][c.Suit].Where(i => i.Key >= c.Value).Count(h => h.Value > 0f && h.Value < 0.99f)
                    : _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f)) +
                      _cardProbabilityForPlayer[playerIndex][c.Suit].Where(i => Card.GetBadValue(i.Key) >= c.BadValue).Count(h => h.Value > 0f && h.Value < 0.99f);
                uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f));
                certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value >= 0.99f));
            }
            if (n < totalCards - certainCards)
            {
                return 0;
            }
            return (float)CNK(n, totalCards - certainCards) / (float)CNK(uncertainCards, totalCards - certainCards);
        }

        public float SuitHigherThanCardExceptAXProbability(int playerIndex, Card c, int roundNumber)
        {
            var y = 1 - NoSuitBetweenCardsProbability(playerIndex, c, new Card(c.Suit, Hodnota.Desitka), roundNumber, true);

            return y;
        }

        public float NoSuitBetweenCardsProbability(int playerIndex, Card c1, Card c2, int roundNumber, bool goodGame = true)
        {
            //if I know at least one card for certain the case is trivial
            if (playerIndex == _myIndex)
            {
                if (goodGame)
                {
                    return _cardProbabilityForPlayer[playerIndex][c1.Suit].Any(h => h.Key > c1.Value && h.Key < c2.Value && h.Value == 1f) ? 0f : 1f;
                }
                else
                {
                    return _cardProbabilityForPlayer[playerIndex][c1.Suit].Any(h => Card.GetBadValue(h.Key) > c1.BadValue && Card.GetBadValue(h.Key) < c2.BadValue && h.Value == 1f) ? 0f : 1f;
                }
            }

            if ((goodGame && !_cardProbabilityForPlayer[playerIndex][c1.Suit].Any(h => h.Key > c1.Value && h.Key < c2.Value && h.Value > 0.01f)) ||
                (!goodGame && !_cardProbabilityForPlayer[playerIndex][c1.Suit].Any(h => Card.GetBadValue(h.Key) > c1.BadValue && Card.GetBadValue(h.Key) < c2.BadValue && h.Value > 0.01f)))
            {
                return 1f;
            }

            if ((goodGame && _cardProbabilityForPlayer[playerIndex][c1.Suit].Any(h => h.Key > c1.Value && h.Key < c2.Value && h.Value >= 0.9f)) ||
                (!goodGame && _cardProbabilityForPlayer[playerIndex][c1.Suit].Any(h => Card.GetBadValue(h.Key) > c1.BadValue && Card.GetBadValue(h.Key) < c2.BadValue && h.Value >= 0.9f)))
            {
                return 0f;
            }

            //let n be the total amount of uncertain cards not in c.Suit plus cards in suit outside c1.Value - c2.Value range
            var n = goodGame
                    ? _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c1.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.9f)) +
                      _cardProbabilityForPlayer[playerIndex][c1.Suit].Where(i => i.Key <= c1.Value || i.Key >= c2.Value).Count(h => h.Value > 0f && h.Value < 0.9f)
                    : _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c1.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.9f)) +
                      _cardProbabilityForPlayer[playerIndex][c1.Suit].Where(i => Card.GetBadValue(i.Key) <= c1.BadValue || Card.GetBadValue(i.Key) >= c2.BadValue).Count(h => h.Value > 0f && h.Value < 0.9f);
            var uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.9f));
            var certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value >= 0.9f));
            var totalCards = 10 - roundNumber + 1;

            if (n == 0)
            {
                n = goodGame
                    ? _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c1.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f)) +
                      _cardProbabilityForPlayer[playerIndex][c1.Suit].Where(i => i.Key <= c1.Value || i.Key >= c2.Value).Count(h => h.Value > 0f && h.Value < 0.99f)
                    : _cardProbabilityForPlayer[playerIndex].Where(i => i.Key != c1.Suit).Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f)) +
                      _cardProbabilityForPlayer[playerIndex][c1.Suit].Where(i => Card.GetBadValue(i.Key) <= c1.BadValue || Card.GetBadValue(i.Key) >= c2.BadValue).Count(h => h.Value > 0f && h.Value < 0.99f);
                uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(h => h.Value > 0f && h.Value < 0.99f));
                certainCards = _cardProbabilityForPlayer[playerIndex].Sum(i => i.Value.Count(j => j.Value >= 0.99f));
            }
            if (n < totalCards - certainCards)
            {
                return 0;
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
            const float epsilon = 0.01f;

            if ((_cardProbabilityForPlayer[playerIndex][suit][Hodnota.Desitka] == epsilon &&
                 _cardProbabilityForPlayer[playerIndex][suit].Count(i => i.Value == epsilon) > 1) ||
                (_cardProbabilityForPlayer[playerIndex][suit][Hodnota.Desitka] == 1 - epsilon &&
                 _cardProbabilityForPlayer[playerIndex][suit].Count(i => i.Value == 1 - epsilon) > 1))
            {
                return epsilon;
            }

            if (_cardProbabilityForPlayer[playerIndex][suit][Hodnota.Desitka] >= 1 - epsilon &&
                _cardProbabilityForPlayer[playerIndex][suit].All(i => i.Key == Hodnota.Desitka ||
                                                                      i.Value <= epsilon))
            {
                return _cardProbabilityForPlayer[playerIndex][suit][Hodnota.Desitka];
            }
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

        //public float NoneOfCardsInSuitProbability(int playerIndex, Barva suit, int roundNumber, Hodnota bottomValue, Hodnota topValue)
        //{
        //    var y = NoneOfCardsInSuitProbability(playerIndex, suit, roundNumber, Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
        //                                                                             .Where(h => h >= bottomValue && h <= topValue)
        //                                                                             .ToArray());

        //    return y;
        //}

        //private float NoneOfCardsInSuitProbability(int playerIndex, Barva suit, int roundNumber, Hodnota[] values)
        //{
        //    //pokud ma hrac jiste kartu ktera je na seznamu, tak vrat nulu
        //    if (_cardProbabilityForPlayer[playerIndex][suit].Any(i => values.Contains(i.Key) && i.Value == 1f))
        //    {
        //        return 0f;
        //    }
        //    //pokud nema hrac jiste zadnou kartu ktera je na seznamu, tak vrat jednicku
        //    if (_cardProbabilityForPlayer[playerIndex][suit].All(i => !values.Contains(i.Key) || i.Value == 0f))
        //    {
        //        return 1f;
        //    }

        //    //hrac ma nejake nejiste karty v barve
        //    var uncertainCards = _cardProbabilityForPlayer[playerIndex].Sum(b => b.Value.Count(h => h.Value > 0f && h.Value < 1f));
        //    var certainCards = _cardProbabilityForPlayer[playerIndex].Sum(b => b.Value.Count(h => h.Value == 1f));
        //    var cardsCertainlyNotInSuit = _cardProbabilityForPlayer[playerIndex][suit].Count(h => values.Contains(h.Key) &&
        //                                                                                          h.Value == 0f);
        //    var totalCards = 10 - roundNumber + 1;
        //    var unknownCards = totalCards - certainCards;
        //    var n = values.Length - cardsCertainlyNotInSuit;

        //    return CNK(uncertainCards - n, unknownCards) / (float)CNK(uncertainCards, unknownCards);
        //}

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

            if (uncertainCards == 0)
            {
                return 1;
            }

            return CNK(uncertainCards, totalCards - certainCards);
        }

        /// <summary>
        /// pokud ma nejaky hrac stejny pocet nejistych karet jako pocet nepridelenych karet, tak mu dam vse, co pro nej zbylo
        /// </summary>
        private void ReduceUcertainCardSet()
        {
            ReduceUcertainCardSet(0f);
            ReduceUcertainCardSet(0.01f);
        }

        private void ReduceUcertainCardSet(float epsilon)
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
                    var certainCards = _cardProbabilityForPlayer[j].SelectMany(i => i.Value.Where(k => k.Value >= 1 - epsilon).Select(k => new Card(i.Key, k.Key))).ToList();
                    var uncertainCards = _cardProbabilityForPlayer[j].SelectMany(i => i.Value.Where(k => k.Value > 0 && k.Value < 1 - epsilon).Select(k => new Card(i.Key, k.Key))).ToList();

                    if (uncertainCards.Any() && (totalCards[j] - certainCards.Count() == uncertainCards.Count()))
                    {
                        //tento hrac ma stejne karet jako nejistych karet. Zmenime u nich pravdepodobnost na 1 a u ostatnich na 0
                        foreach (var uncertainCard in uncertainCards)
                        {
                            for (int k = 0; k < Game.NumPlayers + 1; k++)
                            {
                                _cardProbabilityForPlayer[k][uncertainCard.Suit][uncertainCard.Value] = j == k ? 1 - epsilon : epsilon;
								_verboseString.AppendFormat("Player{0}[{1}][{2}] = {3}\n",
									k + 1, uncertainCard.Suit, uncertainCard.Value, _cardProbabilityForPlayer[k][uncertainCard.Suit][uncertainCard.Value]);
                            }
                        }
                        reduced = true;
                    }
                    //pokud ma akter pri falesne sedme povolene jiste vsechny karty az na trumfovou sedmu
                    //tak ma jiste i trumfovou sedmu a naopak nema ostatni nejiste karty
                    if (_trump.HasValue &&
                        _allowFakeSeven &&
                        certainCards.Count() == totalCards[j] - 1 &&
                        uncertainCards.Has7(_trump.Value) &&
                        ((j == _gameStarterIndex &&
                          (_gameType & Hra.Sedma) != 0) ||
                         (j != _gameStarterIndex &&
                          (_gameType & Hra.SedmaProti) != 0)) &&
                        uncertainCards.Any(k => _cardProbabilityForPlayer[j][k.Suit][k.Value] > epsilon &&
                                                _cardProbabilityForPlayer[j][k.Suit][k.Value] < 1 - epsilon))
                    {
                        foreach (var uncertainCard in uncertainCards)
                        {
                            for (int k = 0; k < Game.NumPlayers + 1; k++)
                            {
                                if (uncertainCard.Suit == _trump.Value &&
                                    uncertainCard.Value == Hodnota.Sedma)
                                {
                                    _cardProbabilityForPlayer[k][uncertainCard.Suit][uncertainCard.Value] = j == k
                                        ? 1 - epsilon
                                        : _cardProbabilityForPlayer[k][uncertainCard.Suit][uncertainCard.Value] > 0
                                            ? epsilon
                                            : 0f;
                                    reduced = true;
                                    _verboseString.AppendFormat("Player{0}[{1}][{2}] = {3}\n",
                                        k + 1, uncertainCard.Suit, uncertainCard.Value, _cardProbabilityForPlayer[k][uncertainCard.Suit][uncertainCard.Value]);
                                }
                                else if (_cardProbabilityForPlayer[Game.TalonIndex][uncertainCard.Suit][uncertainCard.Value] == 0)
                                {
                                    _cardProbabilityForPlayer[k][uncertainCard.Suit][uncertainCard.Value] = j == k
                                        ? 0.01f
                                        : _cardProbabilityForPlayer[k][uncertainCard.Suit][uncertainCard.Value] > 0
                                            ? k != Game.TalonIndex
                                                ? 1 - epsilon
                                                : epsilon
                                            : 0f;
                                    reduced = true;
                                    _verboseString.AppendFormat("Player{0}[{1}][{2}] = {3}\n",
                                        k + 1, uncertainCard.Suit, uncertainCard.Value, _cardProbabilityForPlayer[k][uncertainCard.Suit][uncertainCard.Value]);
                                }
                            }
                        }
                    }
                    if (certainCards.Count() == totalCards[j])
                    {
                        foreach (var uncertainCard in uncertainCards)
                        {
                            if (_cardProbabilityForPlayer[j][uncertainCard.Suit][uncertainCard.Value] > epsilon &&
                            _cardProbabilityForPlayer[j][uncertainCard.Suit][uncertainCard.Value] < 1 - epsilon)
                            {
                                _cardProbabilityForPlayer[j][uncertainCard.Suit][uncertainCard.Value] = epsilon;
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
                    var certainCards = j == Game.TalonIndex
                    ? _cardProbabilityForPlayer[Game.TalonIndex].Sum(i => i.Value.Count(k => k.Value == 1f))
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

        private bool _stopGenerating;
        public void StopGeneratingHands()
        {
            _stopGenerating = true;
        }

        public IEnumerable<Hand[]> GenerateHands(int roundNumber, int roundStarterIndex, int maxGenerations)
        {
            generatedHands = new List<Hand[]>();
            _stopGenerating = false;
            for (var i = 0; i < maxGenerations && !_cancellationToken.IsCancellationRequested && !_stopGenerating; i++)
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
            certainCards[Game.TalonIndex] = _cardProbabilityForPlayer[Game.TalonIndex].SelectMany(j => j.Value.Where(k => k.Value == 1f).Select(k => new Card(j.Key, k.Key))).ToList();
            uncertainCards[Game.TalonIndex] = _cardProbabilityForPlayer[Game.TalonIndex].SelectMany(j => j.Value.Where(k => k.Value > 0f && k.Value < 1f).Select(k => new Card(j.Key, k.Key))).ToList();
            totalCards[Game.TalonIndex] = 2;
            hands[Game.TalonIndex] = new List<Card>();

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
			hands[Game.TalonIndex].AddRange(certainCards[Game.TalonIndex]);
			foreach (var c in certainCards[Game.TalonIndex])
			{
				_verboseString.AppendFormat("Talon += {0} (certain)\n", c);
			}
			_verboseString.AppendFormat("Talon: {0} total, {1} certain, {2} uncertain cards\n",
                totalCards[Game.TalonIndex], certainCards[Game.TalonIndex].Count(), uncertainCards[Game.TalonIndex].Count());
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
                            if (i == Game.TalonIndex)
                            {
                                thresholds[i] += _cardProbabilityForPlayer[Game.TalonIndex][b][h];
                            }
                            else
                            {
                                thresholds[i] += _cardProbabilityForPlayer[(_myIndex + i) % Game.NumPlayers][b][h];
                            }                            
                        } 
                    }
                    if (thresholds[Game.TalonIndex] != 1f)
                    {
                        //musime znormalizovat vsechny prahy, protoze nekomu uz jsme nagenerovali maximum moznych karet
                        var sum = thresholds[Game.TalonIndex];

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
                        //nyni by melo platit, ze thresholds[Game.TalonIndex] == 1f
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
            result[Game.TalonIndex] = new Hand(hands[Game.TalonIndex]);

            var sb = new StringBuilder();
            for (var i = 0; i < Game.NumPlayers; i++)
            {
                sb.AppendFormat("\nPlayer {0}: ", i + 1);
                sb.Append(result[i].ToString());
            }
            sb.AppendFormat("\nTalon: {0}", result[Game.TalonIndex].ToString());
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
            if(hands[Game.TalonIndex].Count != 0 && hands[Game.TalonIndex].Count != 2)
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

        public long EstimateTotalCombinations(int roundNumber)
        {
            var p2 = (_myIndex + 1) % Game.NumPlayers;
            var p3 = (_myIndex + 2) % Game.NumPlayers;
            var certainCards = new List<Card>[Game.NumPlayers + 1];
            var uncertainCards = new List<Card>[Game.NumPlayers + 1];
            var cardsToGuess = new int[Game.NumPlayers + 1];

            certainCards = _cardProbabilityForPlayer.Select(j => j.SelectMany(i => i.Value.Where(k => k.Value >= 0.9f).Select(k => new Card(i.Key, k.Key))).ToList()).ToArray();
            uncertainCards = _cardProbabilityForPlayer.Select(j => j.SelectMany(i => i.Value.Where(k => k.Value > 0.1f && k.Value < 0.9f).Select(k => new Card(i.Key, k.Key))).ToList()).ToArray();

            cardsToGuess = certainCards.Select(i => 10 - roundNumber + 1 - i.Count()).ToArray();
            cardsToGuess[Game.TalonIndex] = 2 - certainCards[Game.TalonIndex].Count();

            //tohle je jen radovy odhad, pro presne cislo je dulezite na kolik pozic muzu dat kazdou nejistou kartu
            var combinations2 = CNK(uncertainCards[p2].Count(), cardsToGuess[p2]);
            var combinations3 = CNK(Math.Max(0, uncertainCards[p3].Count() - cardsToGuess[p2]), cardsToGuess[p3]);
            var combinations4 = CNK(Math.Max(0, uncertainCards[Game.TalonIndex].Count() - Math.Max(cardsToGuess[p2], cardsToGuess[p3])), cardsToGuess[Game.TalonIndex]);

            var result = combinations2 * combinations3 * combinations4;

            return result;
        }

        public IEnumerable<Hand[]> GenerateAllHandCombinations(int roundNumber)
        {
            var p2 = (_myIndex + 1) % Game.NumPlayers;
            var p3 = (_myIndex + 2) % Game.NumPlayers;
            var certainCards = new List<Card>[Game.NumPlayers + 1];
            var uncertainCards = new List<Card>[Game.NumPlayers + 1];
            var cardsToGuess = new int[Game.NumPlayers + 1];
            //var result = new List<Hand[]>();

            certainCards = _cardProbabilityForPlayer.Select(j => j.SelectMany(i => i.Value.Where(k => k.Value == 1).Select(k => new Card(i.Key, k.Key))).ToList()).ToArray();
            uncertainCards = _cardProbabilityForPlayer.Select(j => j.SelectMany(i => i.Value.Where(k => k.Value > 0 && k.Value < 1).Select(k => new Card(i.Key, k.Key))).ToList()).ToArray();

            cardsToGuess = certainCards.Select(i => 10 - roundNumber + 1 - i.Count()).ToArray();
            cardsToGuess[Game.TalonIndex] = 2 - certainCards[Game.TalonIndex].Count();

            var combinations2 = new Combinations<Card>(uncertainCards[p2], cardsToGuess[p2]);
            foreach (var guessedCards2 in combinations2)
            {
                var hands = new List<Card>[Game.NumPlayers + 1];

                hands[_myIndex] = certainCards[_myIndex];
                hands[p2] = certainCards[p2].Concat(guessedCards2).ToList();

                var combinations3 = new Combinations<Card>(uncertainCards[p3].Except(guessedCards2).ToList(), cardsToGuess[p3]);
                foreach(var guessedCards3 in combinations3)
                {
                    hands[p3] = certainCards[p3].Concat(guessedCards3).ToList();

                    var combinations4 = new Combinations<Card>(uncertainCards[Game.TalonIndex].Except(guessedCards2).Except(guessedCards3).ToList(), cardsToGuess[Game.TalonIndex]);
                    foreach(var guessedCards4 in combinations4)
                    {
                        hands[Game.TalonIndex] = certainCards[Game.TalonIndex].Concat(guessedCards4).ToList();

                        //vynech kombinace ve kterych nevyslo na vsechny spravny pocet karet
                        if (hands[0].Count() == 10 - roundNumber + 1 &&
                            hands[1].Count() == 10 - roundNumber + 1 &&
                            hands[2].Count() == 10 - roundNumber + 1 &&
                            hands[Game.TalonIndex].Count() == 2)
                        {
                            yield return new[]
                            {
                                new Hand(hands[0]),
                                new Hand(hands[1]),
                                new Hand(hands[2]),
                                new Hand(hands[3])
                            };
                            //result.Add(new[]
                            //{
                            //    new Hand(hands[0]),
                            //    new Hand(hands[1]),
                            //    new Hand(hands[2]),
                            //    new Hand(hands[3])
                            //});
                        }
                    }
                }
            }

            //return result;
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
                    probs.AppendFormat("probabilities for player{0}:\n{1}\n", i + 1, FriendlyString(i, 1));
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
                    _cardProbabilityForPlayer[Game.TalonIndex][b][h] = 0f;
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
            foreach (var card in _myTalon)
            {
                for (var i = 0; i < Game.NumPlayers + 1; i++)
                {
                    _cardProbabilityForPlayer[i][card.Suit][card.Value] = 0f;
                }
                _cardProbabilityForPlayer[Game.TalonIndex][card.Suit][card.Value] = 1f;
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
						_cardProbabilityForPlayer[Game.TalonIndex][card.Suit][card.Value] = 0.5f;
					}
                    foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                    {
                        foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                        {
                            _cardProbabilityForPlayer[Game.TalonIndex][b][h] = _cardProbabilityForPlayer[_myIndex][b][h] == 1 ||
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
            _gameType = e.GameType;
            if (e.TrumpCard == null)
            {
                return;
            }

            _trump = e.TrumpCard.Suit;
            if ((e.GameType & (Hra.Betl | Hra.Durch)) == 0 && e.GameStartingPlayerIndex != _myIndex)
            {
                //v talonu nesmi byt ostre karty vyjma tech hlasenych
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                {
                    var eso = new Card(b, Hodnota.Eso);
                    var desitka = new Card(b, Hodnota.Desitka);

                    for (var i = 0; i < Game.NumPlayers + 1; i++)
                    {
                        if (i == Game.TalonIndex)
                        {
                            _cardProbabilityForPlayer[i][b][Hodnota.Eso] = e.axTalon.Contains(eso) ? 1f : 0f;
                            _cardProbabilityForPlayer[i][b][Hodnota.Desitka] = e.axTalon.Contains(desitka) ? 1f : 0f;
                        }
                        else if (i != _myIndex)
                        {
                            _cardProbabilityForPlayer[i][b][Hodnota.Eso] = e.axTalon.Contains(eso) || _myHand.HasA(b)
                                                                            ? 0f
                                                                            : 0.5f;
                            _cardProbabilityForPlayer[i][b][Hodnota.Desitka] = e.axTalon.Contains(desitka) || _myHand.HasX(b)
                                                                                ? 0f
                                                                                : 0.5f;
                        }
                    }
                }
                //pokud dal akter do talonu ostrou, tak uz urcite zadnou dalsi kartu v barve na ruce nema
                for (var i = 0; i < e.axTalon.Count(); i++)
                {
                    SetCardProbabilitiesToEpsilon(e.GameStartingPlayerIndex, e.axTalon[i].Suit);
                }
            }
            for (var i = 0; i < Game.NumPlayers + 1; i++)
            {
                _cardProbabilityForPlayer[i][e.TrumpCard.Suit][e.TrumpCard.Value] = i == e.GameStartingPlayerIndex ? 1f : 0f;
                if ((e.GameType & Hra.Sedma) != 0)
                {
                    if (!_allowFakeSeven)
                    {
                        _cardProbabilityForPlayer[i][e.TrumpCard.Suit][Hodnota.Sedma] = i == e.GameStartingPlayerIndex ? 1f : 0f;
                    }
                    else if(e.GameStartingPlayerIndex != _myIndex &&
                            i != _myIndex &&
                            i != Game.TalonIndex &&
                            !(_myHand.Has7(e.TrumpCard.Suit)))
                    {
                        _cardProbabilityForPlayer[i][e.TrumpCard.Suit][Hodnota.Sedma] = i == e.GameStartingPlayerIndex ? 0.99f : 0.01f;
                    }
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
                    return Math.Min(6, 8 - myInitialTrumpCount);
                default:
                    return Math.Min(4, 8 - myInitialTrumpCount);
            }
        }

        public void UpdateProbabilitiesAfterBidMade(BidEventArgs e, Bidding bidding)
        {
            if (!_trump.HasValue)
            {
                return;
            }
            if ((e.BidMade & Hra.Hra) != 0 &&
                e.Player.PlayerIndex != _myIndex &&
                _gameStarterIndex == _myIndex)
            {
                for (var i = 0; i < Game.NumPlayers + 1; i++)
                {
                    if (i == _myIndex ||
                        i == Game.TalonIndex)
                    {
                        continue;
                    }
                    const float small = 0.1f;

                    if (_cardProbabilityForPlayer[i][_trump.Value][Hodnota.Svrsek] > 0 &&
                        _cardProbabilityForPlayer[i][_trump.Value][Hodnota.Svrsek] < 1)
                    {
                        _cardProbabilityForPlayer[i][_trump.Value][Hodnota.Svrsek] = i == e.Player.PlayerIndex ? 1 - small : small;
                    }
                    if (_cardProbabilityForPlayer[i][_trump.Value][Hodnota.Kral] > 0 &&
                        _cardProbabilityForPlayer[i][_trump.Value][Hodnota.Kral] < 1)
                    {
                        _cardProbabilityForPlayer[i][_trump.Value][Hodnota.Kral] = i == e.Player.PlayerIndex ? 1 - small : small;
                    }
                }
            }
            if (_allowFakeSeven &&
                (e.BidMade & Hra.Sedma) != 0 &&
                e.Player.PlayerIndex != _myIndex &&
                _gameStarterIndex == _myIndex &&
                !_myHand.Has7(_trump.Value))
            {
                //ten kdo flekoval mou falesnou sedmu ma nejspis trumfovou sedmu sam
                for (var i = 0; i < Game.NumPlayers + 1; i++)
                {
                    if (i == _myIndex)
                    {
                        continue;
                    }
                    _cardProbabilityForPlayer[i][_trump.Value][Hodnota.Sedma] = i == e.Player.PlayerIndex ? 0.99f : 0.01f;
                }
            }
            if (_allowFakeSeven &&
                (e.BidMade & Hra.SedmaProti) != 0 &&
                e.Player.PlayerIndex != _myIndex &&
                _sevenAgainstIndex == _myIndex &&
                !_myHand.Has7(_trump.Value))
            {
                //akter flekoval mou falesnou sedmu proti takze ma nejspis trumfovou sedmu sam
                for (var i = 0; i < Game.NumPlayers + 1; i++)
                {
                    if (i == _myIndex)
                    {
                        continue;
                    }
                    _cardProbabilityForPlayer[i][_trump.Value][Hodnota.Sedma] = i == e.Player.PlayerIndex ? 0.99f : 0.01f;
                }
            }
            if ((e.BidMade & Hra.Sedma) != 0 &&
                e.Player.PlayerIndex != _myIndex &&
                _gameStarterIndex == _myIndex)
            {
                for (var i = 0; i < Game.NumPlayers + 1; i++)
                {
                    if (i == _myIndex ||
                        i == Game.TalonIndex)
                    {
                        continue;
                    }
                    foreach(var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                    {
                        if (_cardProbabilityForPlayer[i][_trump.Value][h] > 0 &&
                            _cardProbabilityForPlayer[i][_trump.Value][h] < 1)
                        {
                            const float epsilon = 0.01f;

                            _cardProbabilityForPlayer[i][_trump.Value][h] = i == e.Player.PlayerIndex ? 1 - epsilon : epsilon;
                        }
                    }
                    _initialExpectedTrumps[i] = i == _sevenIndex ? 8 - _initialExpectedTrumps[_myIndex] : 0;
                }
            }
            if ((e.BidMade & Hra.SedmaProti) != 0 &&
                bidding.SevenAgainstMultiplier == 1)
            {
                _sevenAgainstIndex = e.Player.PlayerIndex;
                if (_sevenAgainstIndex != _myIndex &&
                    !_myHand.Has7(_trump.Value))
                {
                    _initialExpectedTrumps[_sevenAgainstIndex] = GetNonStarterInitialExpectedTrumps(Hra.SedmaProti);
                    for (var i = 0; i < Game.NumPlayers + 1; i++)
                    {
                        var likelyOrCertain = _allowFakeSeven ? 0.99f : 1f;

                        _cardProbabilityForPlayer[i][_trump.Value][Hodnota.Sedma] = i == _sevenAgainstIndex ? likelyOrCertain : 1 - likelyOrCertain;
                        if (i != _myIndex && i != _sevenAgainstIndex && i != _gameStarterIndex && i != Game.TalonIndex)
                        {
                            _initialExpectedTrumps[i] = 8 - _initialExpectedTrumps[_gameStarterIndex] - _initialExpectedTrumps[_sevenAgainstIndex];
                        }
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
                _cardProbabilityForPlayer[Game.TalonIndex][c1.Suit][Hodnota.Kral] = 0f;
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

        public void UpdateProbabilities(int roundNumber, int roundStarterIndex, Card c1, Card c2, bool hlas2, bool gameWinningRound)
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
                _cardProbabilityForPlayer[Game.TalonIndex][c2.Suit][Hodnota.Kral] = 0f;
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

            //pokud hrajeme v barve, zacinal akter a nejel trumfem a 
            //druhy hrac priznal barvu, sel vejs, ale nehral desitku ani eso, tak
            //pokud jsem zacinal ja, ma desitku a vsechny nizke karty pravdepodobne treti hrac
            //pokud jsem nezacinal ja, ma desitku a vsechny nizke karty pravdepodobne akter
            if (_trump.HasValue &&
                c1.Suit != _trump &&
                c1.Suit == c2.Suit &&
                c2.Value > c1.Value &&
                c2.Value != Hodnota.Desitka &&
                c2.Value != Hodnota.Eso &&
                !gameWinningRound &&
                //_cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] > 0 &&
                //_cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] < 1 &&
                roundStarterIndex == _gameStarterIndex)
            {
                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                      .Where(h => h < c2.Value &&
                                                  _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] > 0 &&
                                                  _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] < 1))
                {
                    _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] = epsilon;
                }
                if (_myIndex == roundStarterIndex)
                {
                    if (_cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] > 0 &&
                        _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] < 1 &&
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] > 0 &&
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] < 1 &&
                        !(_cardsPlayedByPlayer[_myIndex].HasQ(c1.Suit) &&
                          _myHand.HasK(c1.Suit)))
                    {
                        _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] = epsilon;
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] = 1 - epsilon;
                    }
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                          .Where(h => h > c1.Value &&
                                                      h < c2.Value &&
                                                      _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][h] > 0 &&
                                                      _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][h] < 1))
                    {
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][h] = 1 - epsilon;
                    }
				}
                else
                {
                    if (_cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Desitka] > 0 &&
                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Desitka] < 1 &&
                        _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] > 0 &&
                        _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] < 1 &&
                        !_cardProbabilityForPlayer[roundStarterIndex][c1.Suit].Any(i => i.Key != c1.Value &&
                                                                                        i.Key < Hodnota.Eso &&
                                                                                        i.Value == 1f))
                    {
                        _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] = epsilon;
                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Desitka] = 1 - epsilon;
                    }
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                          .Where(h => h > c1.Value &&
                                                      h < c2.Value &&
                                                      _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][h] > 0 &&
                                                      _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][h] < 1))
                    {
                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][h] = 0.5f;
                        _cardProbabilityForPlayer[Game.TalonIndex][c1.Suit][h] = 0.5f;
                    }
                }
            }

            //pokud hrajeme v barve, zacinal akter a nejel trumfem a 
            //druhy hrac priznal barvu, sel vejs, a hral esem a
            //desitku muze stale mit akter, tak
            //druhy hrac pravdepodobne nema v barve zadne karty vyssi nez c1
            if (_trump.HasValue &&
                roundStarterIndex == _gameStarterIndex &&
                !gameWinningRound &&
                c1.Suit != _trump &&
                c1.Suit == c2.Suit &&
                c2.Value > c1.Value &&
                c2.Value == Hodnota.Eso &&
                _cardProbabilityForPlayer[roundStarterIndex][c2.Suit][Hodnota.Desitka] > epsilon &&
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c2.Suit][Hodnota.Desitka] <= epsilon)
            {
                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().Where(h => h > c1.Value && h < Hodnota.Desitka))
                {
                    if (_cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] > 0 &&
                        _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] < 1)
                    {
                        _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] = epsilon;
                        if (_myIndex == roundStarterIndex)
                        {
                            if (_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][h] > 0 &&
                                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][h] < 1)
                            {
                                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][h] = 1 - epsilon;
                            }
                        }
                        else
                        {
                            if (_cardProbabilityForPlayer[roundStarterIndex][c1.Suit][h] > 0 &&
                                _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][h] < 1)
                            {
                                var x = (h == Hodnota.Kral || h == Hodnota.Svrsek) &&
                                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Kral] > epsilon &&
                                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Svrsek] > epsilon
                                        ? epsilon : 0.5f;

                                _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][h] = 1 - x;
                            }
                        }
                    }
                }
            }
            //pokud hrajeme v barve a prvni hrac nevolil
            //a zacina necim jinym nez trumfem
            //a druhy hrac sel vejs desitkou
            //a treti muze prebijet esem nebo trumfem
            //tak druhy hrac uz nema zadne dalsi vyssi karty v barve (a proto hral desitku nebo eso)
            //plati at je akter druhym nebo tretim hracem
            if (_trump.HasValue &&
                c1.Suit != _trump &&
                c1.Suit == c2.Suit &&
                c2.Value > c1.Value &&
                c2.Value == Hodnota.Desitka &&
                roundStarterIndex != _gameStarterIndex &&
                _myIndex != (roundStarterIndex + 1) % Game.NumPlayers &&
                (_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Eso] > epsilon ||
                 _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit].All(h => h.Value == 0) &&
                 _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][_trump.Value].Any(h => h.Value > epsilon)))
            {
                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                      .Where(h => h > c1.Value &&
                                                  h < c2.Value &&
                                                  _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] > 0 &&
                                                  _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] < 1))
                {
                    var otherPlayerIndex = _myIndex == roundStarterIndex ? (roundStarterIndex + 2) % Game.NumPlayers : roundStarterIndex;

                    _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] = epsilon;

                    if (_cardProbabilityForPlayer[otherPlayerIndex][c1.Suit][h] == 0 &&
                        _cardProbabilityForPlayer[Game.TalonIndex][c1.Suit][h] > 0)
                    {
                        _cardProbabilityForPlayer[Game.TalonIndex][c1.Suit][h] = 1 - epsilon;
                    }
                }
            }
            //pokud hrajeme v barve, zacinal akter a nejel trumfem a 
            //druhy hrac priznal barvu, sel vejs, ale nehral eso a
            //vim, ze nikdo nema desitku, tak
            //pokud jsem zacinal ja, ma eso a male karty pravdepodobne treti hrac
            //pokud jsem nezacinal ja, ma eso a male karty pravdepodobne akter
            if (_trump.HasValue &&
				c1.Suit != _trump &&
				c1.Suit == c2.Suit &&
				c2.Value > c1.Value &&
				c2.Value != Hodnota.Eso &&
                !gameWinningRound &&
                _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Desitka] == 0 &&
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] == 0 &&
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] == 0 &&
				//_cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Eso] > 0 &&
				//_cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Eso] < 1 &&
				roundStarterIndex == _gameStarterIndex)
			{
                if (_cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Eso] > 0 &&
                    _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Eso] < 1)
                {
                    _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Eso] = epsilon;
                }
                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                      .Where(h => h < c2.Value &&
                                                  _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] > 0 &&
                                                  _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] < 1))
                {
                    _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] = epsilon;
                }
                if (_myIndex == roundStarterIndex)
                {
                    if (_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Eso] > 0 &&
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Eso] < 1)
                    {
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Eso] = 1 - epsilon;
                    }
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                          .Where(h => h < c2.Value &&
                                                      _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][h] > 0 &&
                                                      _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][h] < 1))
                    {
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][h] = 1 - epsilon;
                    }
                }
                else
                {
                    if (_cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Eso] > 0 &&
                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Eso] < 1)
                    {
                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Eso] = 1 - epsilon;
                    }
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                          .Where(h => h < c2.Value &&
                                                      _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][h] > 0 &&
                                                      _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][h] < 1))
                    {
                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][h] = 0.5f;
                        _cardProbabilityForPlayer[Game.TalonIndex][c1.Suit][h] = 0.5f;
                    }
                }
            }
            //pokud hrajeme v barve, zacinal akter a
            //druhy hrac nesel vejs, ale hral desitkou nebo esem
            //tak pravdepodobne uz v dane barve nema zadne nizke karty
            //if (_trump.HasValue &&
            //    roundStarterIndex == _gameStarterIndex &&
            //    (c2.Value == Hodnota.Eso || c2.Value == Hodnota.Desitka) &&
            //    c1.IsHigherThan(c2, _trump))
            //{
            //    SetCardProbabilitiesLowerThanCardToEpsilon((roundStarterIndex + 1) % Game.NumPlayers, c2);
            //}

            //pokud hrajeme v barve, zacinal akter a
            //druhy hrac nesel vejs, ale namazal eso
            //tak pravdepodobne ma v dane barve i desitku (jinak by nemazal eso)
            if (_trump.HasValue &&
                roundStarterIndex == _gameStarterIndex &&
                c2.Value == Hodnota.Eso &&
                c1.IsHigherThan(c2, _trump) &&
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c2.Suit][Hodnota.Desitka] > 0 &&
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c2.Suit][Hodnota.Desitka] < 1)
            {
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c2.Suit][Hodnota.Desitka] = 1 - epsilon;
                if (_cardProbabilityForPlayer[roundStarterIndex][c2.Suit][Hodnota.Desitka] > 0 &&
                    _cardProbabilityForPlayer[roundStarterIndex][c2.Suit][Hodnota.Desitka] < 1)
                {
                    _cardProbabilityForPlayer[roundStarterIndex][c2.Suit][Hodnota.Desitka] = epsilon;
                }
            }

            //pokud hrajeme v barve a
            //druhy hrac neni akter a nepriznal barvu ale hral trumf a
            //akter muze mit hlasku v barve prvni karty
            //tak ji ma jiste (nebude prece davat hlasku do talonu)
            if (_trump.HasValue &&
                _gameStarterIndex == roundStarterIndex &&
                c1.Suit != _trump &&
                c2.Suit == _trump &&
                _cardProbabilityForPlayer[_gameStarterIndex][c1.Suit][Hodnota.Kral] > 0 &&
                _cardProbabilityForPlayer[_gameStarterIndex][c1.Suit][Hodnota.Kral] < 1 &&
                _cardProbabilityForPlayer[_gameStarterIndex][c1.Suit][Hodnota.Svrsek] > 0 &&
                _cardProbabilityForPlayer[_gameStarterIndex][c1.Suit][Hodnota.Svrsek] < 1)
            {
                _cardProbabilityForPlayer[_gameStarterIndex][c1.Suit][Hodnota.Kral] = 1 - epsilon;
                _cardProbabilityForPlayer[_gameStarterIndex][c1.Suit][Hodnota.Svrsek] = 1 - epsilon;
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

        public void UpdateProbabilities(int roundNumber, int roundStarterIndex, Card c1, Card c2, Card c3, bool hlas3, bool gameWinningRound)
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
                _cardProbabilityForPlayer[Game.TalonIndex][c3.Suit][Hodnota.Kral] = 0f;
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
            const int talonIndex = 3;

            //pokud hrajeme v barve, zacinal akter a nejel trumfem a 
            //treti hrac priznal barvu, sel vejs, ale nehral desitku ani eso, tak
            //pokud jsem zacinal, ma desitku pravdepodobne druhy hrac
            //pokud jsem hral druhy, ma desitku pravdepodobne akter
            //pokud jsem hral treti, tak nevim o desitce nic
            if (_trump.HasValue &&
                roundStarterIndex == _gameStarterIndex &&
                c1.Suit != _trump &&
                c1.Suit == c3.Suit &&
                (c3.Value > c1.Value ||
                 (c1.Suit == c2.Suit &&
                  c2.Value > c1.Value)) &&
                !gameWinningRound &&
                c3.Value != Hodnota.Desitka &&
                c3.Value != Hodnota.Eso) //&&
                //_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] > 0 &&
                //_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] < 1)
            {
                if (_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] > 0 &&
                    _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] < 1)
                {
                    _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] = epsilon;
                }
                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                        .Where(h => h < c3.Value &&
                                                    h > c1.Value &&
                                                    _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][h] > 0 &&
                                                    _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][h] < 1))
                {
                    _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][h] = epsilon;
                }
                if (_myIndex == roundStarterIndex)
                {
                    if (_cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] > 0 &&
                        _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] < 1 &&
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] > 0 &&
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] < 1 &&
                        !(_cardsPlayedByPlayer[_myIndex].HasQ(c1.Suit) &&
                          _myHand.HasK(c1.Suit)))
                    {
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] = epsilon;
                        _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] = 1 - epsilon;
                    }
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                        .Where(h => h > c2.Value &&
                                                    h < c3.Value &&
                                                    _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][h] > 0 &&
                                                    _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][h] < 1))
                    {
                        _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][h] = 1 - epsilon;
                    }
                }
                else
                {
                    if (_myIndex == (roundStarterIndex + 1) % Game.NumPlayers &&
                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Desitka] > 0 &&
                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Desitka] < 1 &&
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] > 0 &&
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] < 1 &&
                        !_cardProbabilityForPlayer[roundStarterIndex][c1.Suit].Any(i => i.Key != c1.Value &&
                                                                                        i.Key < Hodnota.Eso &&
                                                                                        i.Value == 1f))
                    {
                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Desitka] = 1 - epsilon;
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] = epsilon;
                    }
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                            .Where(h => h > c2.Value &&
                                                        h < c3.Value &&
                                                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][h] > 0 &&
                                                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][h] < 1))
                    {
                        _cardProbabilityForPlayer[roundStarterIndex][c3.Suit][h] = 0.5f;
                        _cardProbabilityForPlayer[Game.TalonIndex][c3.Suit][h] = 0.5f;
                    }
                }
            }

            //pokud hrajeme v barve, nevolil jsem a
            //akterovi zbyvaji jen max. 2 karty ktere lze hodit do talonu
            //tak jsou asi v talonu
            if (_trump.HasValue &&
                _myIndex != _gameStarterIndex)
            {
                var gameStarterPotentialTalonCards = PotentialCards(_gameStarterIndex).Where(i => i.Value < Hodnota.Desitka &&
                                                                                                  i.Suit != _trump &&
                                                                                                  i != c1 &&
                                                                                                  i != c2 &&
                                                                                                  i != c3)
                                                                                      .ToList();
                if (gameStarterPotentialTalonCards.Count > 0 &&
                    gameStarterPotentialTalonCards.Count == 2 - CertainCards(Game.TalonIndex).Length)
                {
                    foreach (var c in gameStarterPotentialTalonCards)
                    {
                        for (var i = 0; i < Game.NumPlayers + 1; i++)
                        {
                            if (_cardProbabilityForPlayer[i][c.Suit][c.Value] > 0 &&
                                _cardProbabilityForPlayer[i][c.Suit][c.Value] < 1)
                            {
                                if (i == Game.TalonIndex)
                                {
                                    _cardProbabilityForPlayer[i][c.Suit][c.Value] = 1 - epsilon;
                                }
                                else
                                {
                                    _cardProbabilityForPlayer[i][c.Suit][c.Value] = epsilon;
                                }
                            }
                        }
                    }

                    foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                    {
                        foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                        {
                            if (_cardProbabilityForPlayer[Game.TalonIndex][b][h] > epsilon &&
                                _cardProbabilityForPlayer[Game.TalonIndex][b][h] < 1 - epsilon)
                            {
                                _cardProbabilityForPlayer[Game.TalonIndex][b][h] = epsilon;
                            }
                        }
                    }
                }
            }

            //pokud hrajeme v barve, zacinal akter a nejel trumfem a 
            //treti hrac priznal barvu, sel vejs, a hral esem a
            //akter muze mit desitku, tak
            //treti hrac pravdepodobne nema v barve zadne karty vyssi nez c1
            if (_trump.HasValue &&
                roundStarterIndex == _gameStarterIndex &&
                !gameWinningRound &&
                c1.Suit != _trump &&
                c2.Suit != _trump &&
                c1.Suit == c3.Suit &&
                c3.Value == Hodnota.Eso &&
                _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Desitka] > epsilon &&
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][Hodnota.Desitka] <= epsilon)
            {
                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().Where(h => h > c1.Value && h < Hodnota.Desitka))
                {
                    if (_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][h] > 0 &&
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][h] < 1)
                    {
                        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c1.Suit][h] = epsilon;
                        if (_myIndex == roundStarterIndex)
                        {
                            if (_cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] > 0 &&
                                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] < 1)
                            {
                                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c1.Suit][h] = 1 - epsilon;
                            }
                        }
                        else
                        {
                            if (_cardProbabilityForPlayer[roundStarterIndex][c1.Suit][h] > 0 &&
                                _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][h] < 1)
                            {
                                var x = (h == Hodnota.Kral || h == Hodnota.Svrsek) &&
                                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Kral] > epsilon &&
                                        _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][Hodnota.Svrsek] > epsilon
                                        ? epsilon : 0.5f;

                                _cardProbabilityForPlayer[roundStarterIndex][c1.Suit][h] = 1 - x;
                            }
                        }
                    }
                }
            }

            //pokud hrajeme v barve, zacinal akter a nejel trumfem a 
            //treti hrac priznal barvu, sel vejs, ale nehral eso a
            //vim, ze nikdo nema desitku, tak
            //pokud jsem zacinal ja, ma eso pravdepodobne druhy hrac
            //pokud jsem nezacinal ja, ma eso pravdepodobne akter
            if (_trump.HasValue &&
                roundStarterIndex == _gameStarterIndex &&
                c1.Suit != _trump &&
				c1.Suit == c3.Suit &&
				c3.Value > c1.Value &&
				c3.Value != Hodnota.Eso &&
                c3.Value != Hodnota.Desitka &&
                !gameWinningRound &&
                _cardProbabilityForPlayer[roundStarterIndex][c3.Suit][Hodnota.Desitka] == 0 &&
                _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] == 0 &&
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] == 0 &&
                _cardProbabilityForPlayer[talonIndex][c3.Suit][Hodnota.Desitka] == 0) //&&
				//_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Eso] > 0 &&
				//_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Eso] < 1)
			{
                if (_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Eso] > 0 &&
                    _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Eso] < 1)
                {
                    _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Eso] = epsilon;
                }
                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                      .Where(h => h < c3.Value &&
                                                  _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][h] > 0 &&
                                                  _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][h] < 1))
                {
                    _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][h] = epsilon;
                }
                if (_myIndex == roundStarterIndex)
                {
                    if (_cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][Hodnota.Eso] > 0 &&
                        _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][Hodnota.Eso] < 1)
                    {
                        _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][Hodnota.Eso] = 1 - epsilon;
                    }
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                          .Where(h => h < c3.Value &&
                                                      _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][h] > 0 &&
                                                      _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][h] < 1))
                    {
                        _cardProbabilityForPlayer[(roundStarterIndex + 1) % Game.NumPlayers][c3.Suit][h] = 1 - epsilon;
                    }
                }
                else
                {
                    if (_cardProbabilityForPlayer[roundStarterIndex][c3.Suit][Hodnota.Eso] > 0 &&
                        _cardProbabilityForPlayer[roundStarterIndex][c3.Suit][Hodnota.Eso] < 1)
                    {
                        _cardProbabilityForPlayer[roundStarterIndex][c3.Suit][Hodnota.Eso] = 1 - epsilon;
                    }
                    foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                          .Where(h => h < c3.Value &&
                                                      _cardProbabilityForPlayer[roundStarterIndex][c3.Suit][h] > 0 &&
                                                      _cardProbabilityForPlayer[roundStarterIndex][c3.Suit][h] < 1))
                    {
                        _cardProbabilityForPlayer[roundStarterIndex][c3.Suit][h] = 0.5f;
                        _cardProbabilityForPlayer[Game.TalonIndex][c3.Suit][h] = 0.5f;
                    }
                }
            }
            //pokud hrajeme v barve, zacinal akter a
            //druhy hrac (ja) jsem hral trumf a kolega
            //priznal akterovu barvu ale nehral desitku ani eso, tak ma akter pravdepodobne desitku (o esu nic nevim)
            //if (_trump.HasValue &&
            //    roundStarterIndex == _gameStarterIndex &&
            //    _myIndex == (roundStarterIndex + 1) % Game.NumPlayers &&
            //    c1.Suit != _trump &&
            //    c2.Suit == _trump &&
            //    c3.Suit == c1.Suit &&
            //    c3.Value < Hodnota.Desitka &&
            //    _cardProbabilityForPlayer[roundStarterIndex][c3.Suit][Hodnota.Eso] > epsilon)
            //{
            //    if (_cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] > epsilon)
            //    {
            //        _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] = epsilon;
            //        _cardProbabilityForPlayer[roundStarterIndex][c3.Suit][Hodnota.Desitka] = 1 - epsilon;
            //    }
            //}
            //pokud hrajeme v barve, zacinal akter a
            //druhy hrac nesel vejs, ale hral desitkou nebo esem
            //tak pravdepodobne uz v dane barve nema zadne nizke karty
            //if (_trump.HasValue &&
            //    roundStarterIndex == _gameStarterIndex &&
            //    (c3.Value == Hodnota.Eso || c3.Value == Hodnota.Desitka) &&
            //    c1.IsHigherThan(c2, _trump) &&
            //    c1.IsHigherThan(c3, _trump))
            //{
            //    SetCardProbabilitiesLowerThanCardToEpsilon((roundStarterIndex + 2) % Game.NumPlayers, c3);
            //}

            //pokud hrajeme v barve, zacinal akter a
            //treti hrac nesel vejs, ale namazal eso
            //tak pravdepodobne ma v dane barve i desitku (jinak by nemazal eso)
            if (_trump.HasValue &&
                roundStarterIndex == _gameStarterIndex &&
                c3.Value == Hodnota.Eso &&
                c1.IsHigherThan(c3, _trump) &&
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] > 0 &&
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] < 1)
            {
                _cardProbabilityForPlayer[(roundStarterIndex + 2) % Game.NumPlayers][c3.Suit][Hodnota.Desitka] = 1 - epsilon;
                if (_cardProbabilityForPlayer[roundStarterIndex][c3.Suit][Hodnota.Desitka] > 0 &&
                    _cardProbabilityForPlayer[roundStarterIndex][c3.Suit][Hodnota.Desitka] < 1)
                {
                    _cardProbabilityForPlayer[roundStarterIndex][c3.Suit][Hodnota.Desitka] = epsilon;
                }
            }

            //pokud hrajeme v barve, akter hral na druhe pozici a hral vejs
            //treti hrac nesel vejs, ale hral desitkou nebo esem
            //tak pravdepodobne uz v dane barve nema zadne nizke karty
            if (_trump.HasValue &&
                (roundStarterIndex + 1) % Game.NumPlayers == _gameStarterIndex &&
                (c3.Value == Hodnota.Eso || c3.Value == Hodnota.Desitka) &&
                c1.IsLowerThan(c2, _trump) &&
                c2.IsHigherThan(c3, _trump))
            {
                SetCardProbabilitiesLowerThanCardToEpsilon((roundStarterIndex + 2) % Game.NumPlayers, c3);
            }

            //pokud hrajeme v barve a
            //treti hrac neni akter a nepriznal barvu ale hral trumf a
            //akter muze mit hlasku v barve prvni karty
            //tak ji ma jiste (nebude prece davat hlasku do talonu)
            if (_trump.HasValue &&
                _gameStarterIndex == roundStarterIndex &&
                c1.Suit != _trump &&
                c3.Suit == _trump &&
                _cardProbabilityForPlayer[_gameStarterIndex][c1.Suit][Hodnota.Kral] > 0 &&
                _cardProbabilityForPlayer[_gameStarterIndex][c1.Suit][Hodnota.Kral] < 1 &&
                _cardProbabilityForPlayer[_gameStarterIndex][c1.Suit][Hodnota.Svrsek] > 0 &&
                _cardProbabilityForPlayer[_gameStarterIndex][c1.Suit][Hodnota.Svrsek] < 1)
            {
                _cardProbabilityForPlayer[_gameStarterIndex][c1.Suit][Hodnota.Kral] = 1 - epsilon;
                _cardProbabilityForPlayer[_gameStarterIndex][c1.Suit][Hodnota.Svrsek] = 1 - epsilon;
            }
            if (_trump.HasValue &&
                _gameStarterIndex == (roundStarterIndex + 1) % Game.NumPlayers &&
                c1.Suit != _trump &&
                c2.Suit == c1.Suit &&
                c3.Suit == _trump &&
                _cardProbabilityForPlayer[_gameStarterIndex][c2.Suit][Hodnota.Kral] > 0 &&
                _cardProbabilityForPlayer[_gameStarterIndex][c2.Suit][Hodnota.Kral] < 1 &&
                _cardProbabilityForPlayer[_gameStarterIndex][c2.Suit][Hodnota.Svrsek] > 0 &&
                _cardProbabilityForPlayer[_gameStarterIndex][c2.Suit][Hodnota.Svrsek] < 1)
            {
                _cardProbabilityForPlayer[_gameStarterIndex][c2.Suit][Hodnota.Kral] = 1 - epsilon;
                _cardProbabilityForPlayer[_gameStarterIndex][c2.Suit][Hodnota.Svrsek] = 1 - epsilon;
            }

            //pokud hrajeme v barve a zacina akter a nehral trumf
            //a treti hrac priznal barvu
            //a stych nejde za akterem ani za tretim hracem
            //tak treti hrac asi uz nema v barve nic nizsiho
            if (_trump.HasValue &&
                _gameStarterIndex == roundStarterIndex &&
                c1.Suit != _trump &&
                //c2.Suit == c1.Suit &&
                c3.Suit == c1.Suit &&
                c1.IsLowerThan(c2, _trump) &&
                c2.IsHigherThan(c3, _trump) &&
                c3.Value < Hodnota.Desitka)
            {
                SetCardProbabilitiesLowerThanCardToEpsilon((roundStarterIndex + 2) % Game.NumPlayers, c3);
            }
            if (_trump.HasValue &&
                _gameStarterIndex == (roundStarterIndex + 1) % Game.NumPlayers &&
                c1.Suit != _trump &&
                //c2.Suit == c1.Suit &&
                c3.Suit == c1.Suit &&
                c1.IsHigherThan(c2, _trump) &&
                c1.IsHigherThan(c3, _trump) &&
                c3.Value < Hodnota.Desitka)
            {
                SetCardProbabilitiesLowerThanCardToEpsilon((roundStarterIndex + 2) % Game.NumPlayers, c3);
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

                if (c.IsLowerThan(c2, _trump))
                {
                    _cardProbabilityForPlayer[playerIndex][c2.Suit][h] = 0f;
                    //pokud uz nikdo jiny kartu mit nemuze, tak upravim pravdepodobnosti
                    if (_cardProbabilityForPlayer[otherPlayerIndex][c2.Suit][h] > 0f &&
                        _cardProbabilityForPlayer[otherPlayerIndex][c2.Suit][h] < 1f &&
                        _cardProbabilityForPlayer[_myIndex][c2.Suit][h] == 0f &&
                        _cardProbabilityForPlayer[Game.TalonIndex][c2.Suit][h] == 0f)
                    {
                        _cardProbabilityForPlayer[otherPlayerIndex][c2.Suit][h] = 1f;
                    }
                    else if (_cardProbabilityForPlayer[otherPlayerIndex][c2.Suit][h] == 0f &&
                        _cardProbabilityForPlayer[_myIndex][c2.Suit][h] == 0f &&
                        _cardProbabilityForPlayer[Game.TalonIndex][c2.Suit][h] > 0f &&
                        _cardProbabilityForPlayer[Game.TalonIndex][c2.Suit][h] < 1f)
                    {
                        _cardProbabilityForPlayer[Game.TalonIndex][c2.Suit][h] = 1f;
                    }
                }
            }
        }

        private void SetCardProbabilitiesToEpsilon(int playerIndex, Barva suit)
        {
            //vola se pote co akter ukaze ostrou v talonu (vsechny zbyle barvy ma urcite kolega)
            const float epsilon = 0.01f;
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
                var c = new Card(suit, h);

                if (_cardProbabilityForPlayer[playerIndex][c.Suit][h] > 0f &&
                    _cardProbabilityForPlayer[playerIndex][c.Suit][h] < 1f)
                {
                    _cardProbabilityForPlayer[playerIndex][c.Suit][h] = epsilon;
                }
                if (_cardProbabilityForPlayer[otherPlayerIndex][c.Suit][h] > 0f &&
                    _cardProbabilityForPlayer[otherPlayerIndex][c.Suit][h] < 1f)
                {
                    _cardProbabilityForPlayer[otherPlayerIndex][c.Suit][h] = 1 - epsilon;
                }
                if (_cardProbabilityForPlayer[Game.TalonIndex][c.Suit][h] > 0f &&
                    _cardProbabilityForPlayer[Game.TalonIndex][c.Suit][h] < 1f)
                {
                    _cardProbabilityForPlayer[Game.TalonIndex][c.Suit][h] = epsilon;
                }
            }
        }

        private void SetCardProbabilitiesLowerThanCardToEpsilon(int playerIndex, Card c)
        {
            const float epsilon = 0.01f;
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

                if (c.IsHigherThan(c2, _trump))
                {
                    if (_cardProbabilityForPlayer[playerIndex][c2.Suit][h] > 0f &&
                        _cardProbabilityForPlayer[playerIndex][c2.Suit][h] < 1f)
                    {
                        _cardProbabilityForPlayer[playerIndex][c2.Suit][h] = epsilon;
                        //pokud uz nikdo jiny kartu mit nemuze, tak upravim pravdepodobnosti
                        if (_cardProbabilityForPlayer[otherPlayerIndex][c2.Suit][h] > 0f &&
                            _cardProbabilityForPlayer[otherPlayerIndex][c2.Suit][h] < 1f &&
                            _cardProbabilityForPlayer[_myIndex][c2.Suit][h] == 0f &&
                            _cardProbabilityForPlayer[Game.TalonIndex][c2.Suit][h] <= epsilon)
                        {
                            _cardProbabilityForPlayer[otherPlayerIndex][c2.Suit][h] = 1 - epsilon;
                        }
                        else if (_cardProbabilityForPlayer[otherPlayerIndex][c2.Suit][h] <= epsilon &&
                            _cardProbabilityForPlayer[_myIndex][c2.Suit][h] == 0f &&
                            _cardProbabilityForPlayer[Game.TalonIndex][c2.Suit][h] > 0f &&
                            _cardProbabilityForPlayer[Game.TalonIndex][c2.Suit][h] < 1f)
                        {
                            _cardProbabilityForPlayer[Game.TalonIndex][c2.Suit][h] = 1 - epsilon;
                        }
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
