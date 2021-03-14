using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace Mariasek.Engine.New
{
    public class Deck
    {
        private const int MaxSize = 32;
        private List<Card> _cards;
        private readonly Random _rand;

        public Deck(int? rndSeed = null)
        {
            _rand = rndSeed.HasValue ? new Random(rndSeed.Value) : new Random();
            Init();
        }

        public Deck(List<Card> cards, int? rndSeed = null)
        {
            _rand = rndSeed.HasValue ? new Random(rndSeed.Value) : new Random();
            _cards = cards;
			CheckDeck();
        }

        public void Init()
        {
            _cards = new List<Card>();

            for (int i = 0; i < MaxSize; i++)
            {
                var c = new Card((Barva)Enum.ToObject(typeof(Barva), i / 8),
                                 (Hodnota)Enum.ToObject(typeof(Hodnota), i % 8));
                _cards.Add(c);
            }
			CheckDeck();
        }

        private Barva RandomSuit()
        {
            return (Barva)Enum.ToObject(typeof(Barva), _rand.Next() % Game.NumSuits);
        }

        public void Shuffle(Hra? gameType = null)
        {
            if (gameType == null)
            {
                _cards = _cards.Shuffle().ToList();
            }
            else
            {
                var trump = RandomSuit();
                var hand1 = new List<Card>();
                var hand2 = new List<Card>();
                var hand3 = new List<Card>();

                if ((gameType & Hra.Kilo) != 0)
                {
                    var kqSuit = RandomSuit();
                    hand1.Add(new Card(kqSuit, Hodnota.Kral));
                    hand1.Add(new Card(kqSuit, Hodnota.Svrsek));
                }
                if ((gameType & Hra.Sedma) != 0)
                {
                    hand1.Add(new Card(trump, Hodnota.Sedma));
                }

                //generujeme rozlozeni na konkretni typ hry
                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().OrderByDescending(h => h))
                {
                    foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>().OrderByDescending(b => b == trump ? 10 : (int)b))
                    {
                        float[] probs;

                        if (hand1.Contains(new Card(b, h)))
                        {
                            continue;
                        }
                        if (h >= Hodnota.Desitka && ((gameType & Hra.Kilo) != 0))
                        {
                            probs = new[] { 0.5f, 0.25f, 0.25f };
                        }
                        else if (h >= Hodnota.Svrsek && ((gameType & Hra.Kilo) != 0))
                        {
                            probs = new[] { 0.4f, 0.3f, 0.3f };
                        }
                        else if (b == trump)
                        {
                            if ((gameType & Hra.Sedma) != 0)
                            {
                                probs = new[] { 0.7f, 0.15f, 0.15f };
                            }
                            else if ((gameType & Hra.Kilo) != 0)
                            {
                                probs = new[] { 0.5f, 0.25f, 0.25f };
                            }
                            else
                            {
                                probs = new[] { 1 / 3f, 1 / 3f, 1 / 3f };
                            }
                        }
                        else
                        {
                            probs = new[] { 1 / 3f, 1 / 3f, 1 / 3f };
                        }
                        probs[1] = probs[0] + probs[1];
                        probs[2] = 1;

                        var n = _rand.NextDouble();
                        if (n <= probs[0] && hand1.Count() < 12)
                        {
                            hand1.Add(new Card(b, h));
                        }
                        else if (n <= probs[1] && hand2.Count() < 10)
                        {
                            hand2.Add(new Card(b, h));
                        }
                        else if (hand3.Count() < 10)
                        {
                            hand3.Add(new Card(b, h));
                        }
                        else if (hand2.Count() < 10)
                        {
                            hand2.Add(new Card(b, h));
                        }
                        else
                        {
                            hand1.Add(new Card(b, h));
                        }
                    }
                }
                _cards = hand3.Take(5).ToList();
                _cards.AddRange(hand2.Take(5));
                _cards.AddRange(hand1.Take(5));
                _cards.AddRange(hand3.Skip(5).Take(5));
                _cards.AddRange(hand2.Skip(5).Take(5));
                _cards.AddRange(hand1.Skip(5).Take(7));
            }
			CheckDeck();
        }

        public void Cut()
        {
            const int minimalCut = 3;
            var temp = new List<Card>();
            int r = _rand.Next() % (_cards.Count - 2 * minimalCut) + minimalCut;

            temp = _cards.GetRange(0, r);
            _cards.RemoveRange(0, r);
            _cards.AddRange(temp);
        }

        public Card TakeOne()
        {
            if (_cards.Count == 0)
                return null;

            Card c = _cards.Last();
            _cards.RemoveAt(_cards.Count - 1);

            return c;
        }

        public List<Card> Take(int n)
        {
            if (_cards.Count == 0)
                return null;

            var result = _cards.GetRange(_cards.Count - n, n);
            
            _cards.RemoveRange(_cards.Count - n, n);

            return result;
        }

        public Card TakeCard(Card c)
        {
            var card = _cards.Find(i => i.Suit == c.Suit && i.Value == c.Value);
            return c;
        }

        public bool IsEmpty()
        {
            return _cards == null || _cards.Count == 0;
        }

        public void LoadDeck(Stream stream)
        {
            var xml = new XmlSerializer(typeof(List<Card>));
            _cards = (List<Card>)xml.Deserialize(stream);

			CheckDeck();
        }

        public void SaveDeck(Stream stream)
        {
            var xml = new XmlSerializer(typeof(List<Card>));
            xml.Serialize(stream, _cards);
        }

		private void CheckDeck()
		{
			if (IsEmpty())
			{
				return;
			}
			for (int i = 0; i < MaxSize; i++)
			{
				var c = new Card((Barva)Enum.ToObject(typeof(Barva), i / 8),
					(Hodnota)Enum.ToObject(typeof(Hodnota), i % 8));
				var n = _cards.Count(j => j == c);

				if (n != 1)
				{
					throw new InvalidDataException(string.Format("CheckDeck failed: {0} is present {1} times", c, n));
				};
			}
		}

        public override string ToString()
        {
            if (_cards == null || !_cards.Any())
            {
                return "Empty deck";
            }
            var sb = new StringBuilder();
            for(var i = _cards.Count - 1; i >= 0; i--)
            {
                sb.AppendFormat("{0:##}. {1}\n", _cards.Count - i, _cards[i]);
            }
            return sb.ToString();
        }
    }
}
