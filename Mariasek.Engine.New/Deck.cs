﻿using System;
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

        public Deck(List<Card> cards)
        {
            _cards = cards;
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
        }

        public void Shuffle()
        {
            var temp = new List<Card>();

            for (int i = 0; i < MaxSize; i++ )
            {
                int r = _rand.Next() % _cards.Count;
                temp.Add(_cards[r]);
                _cards.RemoveAt(r);
            }
            _cards = temp;
        }

        public Card TakeOne()
        {
            if (_cards.Count == 0)
                return null;

            Card c = _cards[0];
            _cards.RemoveAt(0);

            return c;
        }

        public List<Card> Take(int n)
        {
            if (_cards.Count == 0)
                return null;

            var result = _cards.GetRange(0, n);
            
            _cards.RemoveRange(0, n);

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
        }

        public void SaveDeck(Stream stream)
        {
            var xml = new XmlSerializer(typeof(List<Card>));
            xml.Serialize(stream, _cards);
        }
    }
}
