using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine.New
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

        public static IEnumerable<TSource> Randomize<TSource>(this IEnumerable<TSource> source)
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

        public static IEnumerable<Card> Sort(this List<Card>hand, bool ascending, bool badGameSorting = false, Barva? firstSuit = null, Barva? lastSuit = null)
        {
            hand.Sort((c1, c2) =>
                {
                    var sign = ascending ? -1 : 1;
                    int s1 = Convert.ToInt16(c1.Suit);
                    int s2 = Convert.ToInt16(c2.Suit);

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

        public static bool HasSolitaryX(this List<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Desitka && i.Suit == suit) &&
                   hand.Count(i => i.Suit == suit) == 1;
        }

        public static bool HasSuit(this List<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Suit == suit);
        }

        public static bool HasAtLeastNCardsOfSuit(this List<Card> hand, Barva suit, int n)
        {
            return hand.Count(i => i.Suit == suit) >= n;
        }

        public static bool HasAtMostNCardsOfSuit(this List<Card> hand, Barva suit, int n)
        {
            return hand.Count(i => i.Suit == suit) <= n;
        }

        public static bool HasX(this List<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Desitka && i.Suit == suit);
        }

        public static bool HasA(this List<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Eso && i.Suit == suit);
        }

        public static bool HasK(this List<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Kral && i.Suit == suit);
        }

        public static bool HasQ(this List<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Svrsek && i.Suit == suit);
        }

        public static bool Has7(this List<Card> hand, Barva suit)
        {
            return hand.Any(i => i.Value == Hodnota.Sedma && i.Suit == suit);
        }

        public static int CardCount(this List<Card> hand, Barva suit)
        {
            return hand.Count(i => i.Suit == suit);
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
        private readonly Dictionary<Barva, bool> _has7 = new Dictionary<Barva, bool>();

        public Hand(IEnumerable<Card> hand)
        {
            _hand = hand != null ? new List<Card>(hand.Where(i => i != null)) : new List<Card>();
            Sort();
            Update();
        }

        public void AddRange(List<Card> list)
        {
            _hand.AddRange(list.Where(i => i != null));
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
            return _hand.RemoveAll(predicate);
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

        public bool Has7(Barva suit)
        {
            return _has7[suit];
        }

        public int CardCount(Barva suit)
        {
            return _hand.Where(i => i != null).Count(i => i.Suit == suit);
        }

        public void Sort(bool ascending = false, bool badGameSorting = false)
        {
            _hand.Sort(ascending, badGameSorting);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                sb.AppendFormat(" {0}: ", b);
                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().OrderByDescending(h => h))
                {
                    var card = _hand.FirstOrDefault(c => c != null && c.Suit == b && c.Value == h);
                    sb.Append(card != null ? card.CharCode : '-');
                }
            }

            return sb.ToString();
        }
    }
}
