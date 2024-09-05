using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public static class HandExtensions
    {
        private static readonly Random rand = new Random();

        public static TSource RandomOne<TSource>(this ICollection<TSource> source)
        {
            return source.ElementAt(rand.Next(source.Count()));
        }

        public static TSource RandomOneOrDefault<TSource>(this ICollection<TSource> source)
        {
            if (source.Any())
            {
                return RandomOne(source);
            }

            return default(TSource);
        }

        //Shuffles collection members using https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle
        public static IEnumerable<TSource> Shuffle<TSource>(this IEnumerable<TSource> source)
        {
            var list = source.ToList();
    		var n = list.Count;

			while (n > 1)
			{
				n--;
				int k = rand.Next(n + 1);
				TSource value = list[k];
				list[k] = list[n];
				list[n] = value;
			}

			return list;
		}

        public static IEnumerable<Card> Sort(this List<Card> hand, SortMode sortMode, bool badGameSorting = false, Barva? firstSuit = null, Barva? lastSuit = null, bool shuffleSuits = false)
        {
            if (sortMode == SortMode.None)
            {
                return hand;
            }

            var shuffledSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>().Shuffle().ToArray();
            var currentSuitOrder = hand.Select(i => i.Suit).Distinct().ToArray();

            hand.Sort((c1, c2) =>
            {
                var sign = sortMode == SortMode.Ascending ? -1 : sortMode == SortMode.Descending ? 1 : 0;
                int s1 = shuffleSuits ? Array.IndexOf(shuffledSuits, c1.Suit) : Array.IndexOf(currentSuitOrder, c1.Suit);
                int s2 = shuffleSuits ? Array.IndexOf(shuffledSuits, c2.Suit) : Array.IndexOf(currentSuitOrder, c2.Suit);
                
                //if (!shuffleSuits)
                {
                    if (lastSuit.HasValue)
                    {
                        if (c1.Suit == lastSuit.Value)
                        {
                            s1 += 10;
                        }
                        if (c2.Suit == lastSuit.Value)
                        {
                            s2 += 10;
                        }
                    }
                    else if (firstSuit.HasValue)
                    {
                        if (c1.Suit == firstSuit.Value)
                        {
                            s1 -= 10;
                        }
                        if (c2.Suit == firstSuit.Value)
                        {
                            s2 -= 10;
                        }
                    }
                }
                if (s1 == s2)
                {
                    if (!badGameSorting)
                    {
                        //normalni trideni
                        return sign * (Convert.ToInt16(c2.Value) - Convert.ToInt16(c1.Value));
                    }
                    else
                    {
                        //betl a durch
                        return sign * (c2.BadValue - c1.BadValue);
                    }
                }

                //return sign*(s1 - s2);
                return s1 - s2; //sign ignorujeme v poradi barev
            });

            return hand;
        }

        public static bool HasSolitaryX(this IEnumerable<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Desitka && i.Suit == suit) &&
                   hand.Count(i => i.Suit == suit) == 1;
        }

        public static bool HasSuit(this List<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Suit == suit);
        }

        public static int SuitCount(this IEnumerable<Card> hand)
        {
            return hand.Select(i => i.Suit).Distinct().Count();
        }

        public static bool HasAtLeastNCardsOfSuit(this IEnumerable<Card> hand, Barva suit, int n)
        {
            return hand.Count(i => i.Suit == suit) >= n;
        }

        public static bool HasAtMostNCardsOfSuit(this IEnumerable<Card> hand, Barva suit, int n)
        {
            return hand.Count(i => i.Suit == suit) <= n;
        }

        public static bool HasX(this IEnumerable<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Desitka && i.Suit == suit);
        }

        public static bool HasA(this IEnumerable<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Eso && i.Suit == suit);
        }

        public static bool HasK(this IEnumerable<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Kral && i.Suit == suit);
        }

        public static bool HasQ(this IEnumerable<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Svrsek && i.Suit == suit);
        }

        public static bool HasJ(this IEnumerable<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Spodek && i.Suit == suit);
        }

        public static bool Has9(this IEnumerable<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Devitka && i.Suit == suit);
        }

        public static bool Has8(this IEnumerable<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Osma && i.Suit == suit);
        }

        public static bool Has7(this IEnumerable<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Sedma && i.Suit == suit);
        }

        public static bool HasSuit(this IEnumerable<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Suit == suit);
        }

        public static int CardCount(this IEnumerable<Card> hand, Barva suit)
        {
            return hand.Count(i => i.Suit == suit);
        }

        public static int CardCount(this IEnumerable<Card> hand, Hodnota value)
        {
            return hand.Count(i => i.Value == value);
        }

        public static string ToHandString(this IEnumerable<Card> hand)
        {
            var sb = new StringBuilder();

            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                sb.AppendFormat(" {0}: ", b);
                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().OrderByDescending(h => h))
                {
                    var card = hand?.FirstOrDefault(c => c != null && c.Suit == b && c.Value == h);
                    sb.Append(card != null ? card.CharCode : '-');
                }
            }

            return sb.ToString().Trim();
        }
    }

    public class Hand
    {
        private readonly List<Card> _hand;
        private readonly Dictionary<Barva, int> _count = new Dictionary<Barva, int>();
        private readonly Dictionary<Barva, bool> _hasSolitaryX = new Dictionary<Barva, bool>();
        private readonly Dictionary<Barva, bool> _hasA = new Dictionary<Barva, bool>();
        private readonly Dictionary<Barva, bool> _hasX = new Dictionary<Barva, bool>();
        private readonly Dictionary<Barva, bool> _hasK = new Dictionary<Barva, bool>();
        private readonly Dictionary<Barva, bool> _hasQ = new Dictionary<Barva, bool>();
        private readonly Dictionary<Barva, bool> _hasJ = new Dictionary<Barva, bool>();
        private readonly Dictionary<Barva, bool> _has9 = new Dictionary<Barva, bool>();
        private readonly Dictionary<Barva, bool> _has8 = new Dictionary<Barva, bool>();
        private readonly Dictionary<Barva, bool> _has7 = new Dictionary<Barva, bool>();

        public Hand(IEnumerable<Card> hand)
        {
            _hand = hand != null ? new List<Card>(hand) : new List<Card>();
            _hand = _hand.Where(i => i != null).ToList();   //call Where() on a copy to try to prevent a collection was modified error
            Sort();
            Update();
        }

        public Hand(Hand hand) : this(hand._hand)
        {
        }

        public void AddRange(List<Card> list)
        {
            _hand.AddRange(list);
            Update();
        }

        public void Remove(Card c)
        {
            //_hand.Remove(c);
            var index = _hand.FindIndex(i => i == c);
            if (index >= 0)
            {
                _hand[index] = null;
                Update();
            }
        }

        public int RemoveAll(Predicate<Card> predicate)
        {
            int result = _hand.RemoveAll(predicate);

            if (result > 0)
            {
                Update();
            }

            return result;
        }

        public IEnumerable<Card> Where(Func<Card, bool> predicate)
        {
            return _hand.Where(i => i != null).Where(predicate);
        }

        public IEnumerable<IGrouping<TKey, Card>> GroupBy<TKey>(Func<Card, TKey> predicate)
        {
            return _hand.Where(i => i != null).GroupBy<Card, TKey>(predicate);
        }

        public bool Any(Func<Card, bool> predicate)
        {
            return _hand.Where(i => i != null).Any(predicate);
        }

        public bool All(Func<Card, bool> predicate)
        {
            return _hand.Where(i => i != null).All(predicate);
        }

        public int Count()
        {
            return _hand.Count(i => i != null);
        }

        public int Count(Func<Card, bool> predicate)
        {
            return _hand.Where(i => i != null).Count(predicate);
        }

        public Hodnota Min(Barva suit, Barva? trump)
        {
            var cards = _hand.Where(i => i != null).Where(c => c.Suit == suit);

            if(cards.Any())
            {
                return cards.Aggregate((i, j) => i.IsHigherThan(j, trump) ? j : i).Value;
            }
            return (Hodnota)(int.MaxValue);
        }

        public Hodnota Max(Barva suit, Barva? trump)
        {
            var cards = _hand.Where(i => i != null).Where(c => c.Suit == suit);

            if (cards.Any())
            {
                return cards.Aggregate((i, j) => i.IsHigherThan(j, trump) ? i : j).Value;
            }
            return (Hodnota)(int.MinValue);
        }

        public Card First(Func<Card, bool> predicate)
        {
            return _hand.Where(i => i != null).First(predicate);
        }

        public Card FirstOrDefault(Func<Card, bool> predicate)
        {
            return _hand.Where(i => i != null).FirstOrDefault(predicate);
        }

        public Card FirstOrDefault()
        {
            return _hand.Where(i => i != null).FirstOrDefault();
        }

        public Card RandomOneOrDefault()
        {
            return _hand.Where(i => i != null).ToList().RandomOneOrDefault();
        }

        public static implicit operator List<Card>(Hand h)
        {
            return h._hand.Where(i => i != null).ToList();
        }

        private void Update()
        {
            foreach (var suit in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                _count[suit] = _hand.Where(i => i != null).Count(i => i.Suit == suit);
                _hasSolitaryX[suit] = _hand.Where(i => i != null).Any(i => i.Value == Hodnota.Desitka && i.Suit == suit) && _count[suit] == 1;
                _hasA[suit] = _hand.Where(i => i != null).Any(i => i.Value == Hodnota.Eso && i.Suit == suit);
                _hasX[suit] = _hand.Where(i => i != null).Any(i => i.Value == Hodnota.Desitka && i.Suit == suit);
                _hasK[suit] = _hand.Where(i => i != null).Any(i => i.Value == Hodnota.Kral && i.Suit == suit);
                _hasQ[suit] = _hand.Where(i => i != null).Any(i => i.Value == Hodnota.Svrsek && i.Suit == suit);
                _hasJ[suit] = _hand.Where(i => i != null).Any(i => i.Value == Hodnota.Spodek && i.Suit == suit);
                _has9[suit] = _hand.Where(i => i != null).Any(i => i.Value == Hodnota.Devitka && i.Suit == suit);
                _has8[suit] = _hand.Where(i => i != null).Any(i => i.Value == Hodnota.Osma && i.Suit == suit);
                _has7[suit] = _hand.Where(i => i != null).Any(i => i.Value == Hodnota.Sedma && i.Suit == suit);
            }
        }

        public bool HasSolitaryX(Barva suit)
        {
            return _hasSolitaryX[suit];
        }

        public bool HasSuit(Barva suit)
        {
            return _count[suit] > 0;
        }

        public bool HasAtLeastNCardsOfSuit(Barva suit, int n)
        {
            return _count[suit] >= n;
        }

        public bool HasAtMostNCardsOfSuit(Barva suit, int n)
        {
            return _count[suit] <= n;
        }

        public bool HasX(Barva suit)
        {
            return _hasX[suit];
        }

        public bool HasA(Barva suit)
        {
            return _hasA[suit];
        }

        public bool HasK(Barva suit)
        {
            return _hasK[suit];
        }

        public bool HasQ(Barva suit)
        {
            return _hasQ[suit];
        }

        public bool HasJ(Barva suit)
        {
            return _hasJ[suit];
        }

        public bool Has9(Barva suit)
        {
            return _has9[suit];
        }

        public bool Has8(Barva suit)
        {
            return _has8[suit];
        }

        public bool Has7(Barva suit)
        {
            return _has7[suit];
        }

        public int CardCount(Barva suit)
        {
            return _hand.Where(i => i != null).Count(i => i.Suit == suit);
        }

        public int CardCount(Hodnota value)
        {
            return _hand.Where(i => i != null).Count(i => i.Value == value);
        }

        public int SuitCount => _hand.Where(i => i != null).Select(i => i.Suit).Distinct().Count();
        
        public void Sort(SortMode sortMode = SortMode.Descending, bool badGameSorting = false)
        {
            _hand.Sort(sortMode, badGameSorting);
        }

        public override string ToString()
        {
            return _hand.ToHandString();
        }
    }
}
