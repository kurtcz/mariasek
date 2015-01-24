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

        public Hand(IEnumerable<Card> hand)
        {
            _hand = new List<Card>(hand);
            Sort();
            Update();
        }

        public void Remove(Card c)
        {
            _hand.Remove(c);
            Update();
        }

        public IEnumerable<Card> Where(Func<Card, bool> predicate)
        {
            return _hand.Where(predicate);
        }

        public bool Any(Func<Card, bool> predicate)
        {
            return _hand.Any(predicate);
        }

        public bool All(Func<Card, bool> predicate)
        {
            return _hand.All(predicate);
        }

        public int Count(Func<Card, bool> predicate)
        {
            return _hand.Count(predicate);
        }

        public Card First(Func<Card, bool> predicate)
        {
            return _hand.First(predicate);
        }

        public Card FirstOrDefault(Func<Card, bool> predicate)
        {
            return _hand.FirstOrDefault(predicate);
        }

        public Card FirstOrDefault()
        {
            return _hand.FirstOrDefault();
        }

        public Card RandomOneOrDefault()
        {
            return _hand.RandomOneOrDefault();
        }

        public static implicit operator List<Card>(Hand h)
        {
            return h._hand;
        }

        private void Update()
        {
            foreach (var suit in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                _count[suit] = _hand.Count(i => i.Suit == suit);
                _hasSolitaryX[suit] = _hand.Any(i => i.Value == Hodnota.Desitka && i.Suit == suit) && _count[suit] == 1;
                _hasA[suit] = _hand.Any(i => i.Value == Hodnota.Eso && i.Suit == suit);
                _hasX[suit] = _hand.Any(i => i.Value == Hodnota.Desitka && i.Suit == suit);
                _hasK[suit] = _hand.Any(i => i.Value == Hodnota.Kral && i.Suit == suit);
                _hasQ[suit] = _hand.Any(i => i.Value == Hodnota.Svrsek && i.Suit == suit);
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

        public int CardCount(Barva suit)
        {
            return _hand.Count(i => i.Suit == suit);
        }

        public void Sort(bool ascending = false)
        {
            _hand.Sort((c1, c2) =>
                       {
                           var sign = ascending ? -1 : 1;
                           int s1 = Convert.ToInt16(c1.Suit);
                           int s2 = Convert.ToInt16(c2.Suit);

                           if (s1 == s2)
                               return sign*(Convert.ToInt16(c2.Value) - Convert.ToInt16(c1.Value));
                            
                           return sign*(s1 - s2);
                       });
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                sb.AppendFormat(" {0}: ", b);
                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().OrderByDescending(h => h))
                {
                    var card = _hand.FirstOrDefault(c => c.Suit == b && c.Value == h);
                    sb.Append(card != null ? card.CharCode : '-');
                }
            }

            return sb.ToString();
        }
    }
}
