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

        public void Shuffle()
        {
            _cards = _cards.Shuffle().ToList();
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
    }
}
