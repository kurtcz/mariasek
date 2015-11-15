using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mariasek.Engine.New;

namespace Mariasek.Engine.Tests
{
    [TestClass]
    public class ProbabilitiesTests
    {
        //[TestMethod]
        //public void X()
        //{
        //    var hand = new Card[] {
        //        new Card(Barva.Cerveny, Hodnota.Desitka),
        //        new Card(Barva.Cerveny, Hodnota.Kral),
        //        new Card(Barva.Cerveny, Hodnota.Spodek),
        //        new Card(Barva.Cerveny, Hodnota.Osma),
        //        new Card(Barva.Cerveny, Hodnota.Sedma),
        //        new Card(Barva.Zeleny, Hodnota.Desitka),
        //        new Card(Barva.Zeleny, Hodnota.Kral),
        //        new Card(Barva.Zeleny, Hodnota.Spodek),
        //        new Card(Barva.Zeleny, Hodnota.Sedma),
        //        new Card(Barva.Zaludy, Hodnota.Desitka),
        //        new Card(Barva.Zaludy, Hodnota.Spodek),
        //        new Card(Barva.Kule, Hodnota.Devitka)
        //    }.ToList();

        //    var g = new Game();
        //    var ai = new AiPlayer(g);
        //    var talon = ai.ChooseBetlTalon(hand);
        //    //talon = ai.ChooseDurchTalon(hand);
        //}

        private void CardProbabilityTest(int myIndex, int gameStarterIndex, List<Card> hand, Barva? trump, List<Card> talon)
        {
            var probabilities = new Probability(myIndex, gameStarterIndex, new Hand(hand), trump, talon);

            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                {
                    var c = new Card(b, h);
                    var total = 0f;

                    for(int i = 0; i < Game.NumPlayers + 1; i++)
                    {
                        total += probabilities.CardProbability(i, c);
                    }
                    Assert.AreEqual(1f, total, "Card probabilities do not add up for {0}:\nPlayer 1:{1}\nPlayer 2:{2}\nPlayer 3:{3}\nTalon:{4}",
                        c,
                        probabilities.CardProbability(0, c),
                        probabilities.CardProbability(1, c),
                        probabilities.CardProbability(2, c),
                        probabilities.CardProbability(3, c));
                }
            }

            var roundNumber = 1;
            var numTests = 100;
            for (int k = 0; k < numTests; k++)
            {
                var hands = probabilities.GenerateHands(roundNumber, gameStarterIndex);

                for (int i = 0; i < Game.NumPlayers + 1; i++)
                {
                    if (i == gameStarterIndex)
                    {
                        Assert.AreEqual(hand.Count(), hands[i].Count(c => true), string.Format("Wrong number of cards generated for player {0}", i + 1));
                    }
                    else if (i == 3)
                    {
                        var expectedTalonCardsGenerated = talon != null ? talon.Count() : 2;

                        Assert.IsTrue(expectedTalonCardsGenerated == 0 || expectedTalonCardsGenerated == 2);
                        Assert.AreEqual(expectedTalonCardsGenerated, hands[i].Count(c => true), "Wrong number of cards generated for talon");
                    }
                    else
                    {
                        Assert.AreEqual(10 - roundNumber + 1, hands[i].Count(c => true), string.Format("Wrong number of cards generated for player {0}", i + 1));
                    }
                }
            }
        }

        [TestCategory("Probability tests")]
        [TestMethod]
        public void Hand10Talon0()
        {
            var hand = new Card[] {
                new Card(Barva.Cerveny, Hodnota.Desitka),
                new Card(Barva.Cerveny, Hodnota.Kral),
                new Card(Barva.Cerveny, Hodnota.Spodek),
                new Card(Barva.Cerveny, Hodnota.Osma),
                new Card(Barva.Cerveny, Hodnota.Sedma),
                new Card(Barva.Zeleny, Hodnota.Desitka),
                new Card(Barva.Zeleny, Hodnota.Kral),
                new Card(Barva.Zeleny, Hodnota.Spodek),
                new Card(Barva.Zeleny, Hodnota.Sedma),
                new Card(Barva.Zaludy, Hodnota.Desitka)
            }.ToList();
            var talon = new Card[] {
            }.ToList();
            var trump = Barva.Cerveny;

            CardProbabilityTest(1, 0, hand, trump, talon);
        }

        [TestCategory("Probability tests")]
        [TestMethod]
        public void Hand10Talon2()
        {
            var hand = new Card[] {
                new Card(Barva.Cerveny, Hodnota.Desitka),
                new Card(Barva.Cerveny, Hodnota.Kral),
                new Card(Barva.Cerveny, Hodnota.Spodek),
                new Card(Barva.Cerveny, Hodnota.Osma),
                new Card(Barva.Cerveny, Hodnota.Sedma),
                new Card(Barva.Zeleny, Hodnota.Desitka),
                new Card(Barva.Zeleny, Hodnota.Kral),
                new Card(Barva.Zeleny, Hodnota.Spodek),
                new Card(Barva.Zeleny, Hodnota.Sedma),
                new Card(Barva.Zaludy, Hodnota.Desitka)
            }.ToList();
            var talon = new Card[] {
                new Card(Barva.Kule, Hodnota.Devitka),
                new Card(Barva.Zaludy, Hodnota.Spodek)
            }.ToList();
            var trump = Barva.Cerveny;

            CardProbabilityTest(0, 0, hand, trump, talon);
        }

        [TestCategory("Probability tests")]
        [TestMethod]
        public void Hand12Talon0()
        {
            var hand = new Card[] {
                new Card(Barva.Cerveny, Hodnota.Desitka),
                new Card(Barva.Cerveny, Hodnota.Kral),
                new Card(Barva.Cerveny, Hodnota.Spodek),
                new Card(Barva.Cerveny, Hodnota.Osma),
                new Card(Barva.Cerveny, Hodnota.Sedma),
                new Card(Barva.Zeleny, Hodnota.Desitka),
                new Card(Barva.Zeleny, Hodnota.Kral),
                new Card(Barva.Zeleny, Hodnota.Spodek),
                new Card(Barva.Zeleny, Hodnota.Sedma),
                new Card(Barva.Zaludy, Hodnota.Desitka),
                new Card(Barva.Zaludy, Hodnota.Spodek),
                new Card(Barva.Kule, Hodnota.Devitka)
            }.ToList();
            var talon = new Card[] {
            }.ToList();
            var trump = Barva.Cerveny;

            CardProbabilityTest(0, 0, hand, trump, talon);
        }

        [TestCategory("Probability tests")]
        [TestMethod]
        public void Hand10NullTalon()
        {
            var hand = new Card[] {
                new Card(Barva.Cerveny, Hodnota.Desitka),
                new Card(Barva.Cerveny, Hodnota.Kral),
                new Card(Barva.Cerveny, Hodnota.Spodek),
                new Card(Barva.Cerveny, Hodnota.Osma),
                new Card(Barva.Cerveny, Hodnota.Sedma),
                new Card(Barva.Zeleny, Hodnota.Desitka),
                new Card(Barva.Zeleny, Hodnota.Kral),
                new Card(Barva.Zeleny, Hodnota.Spodek),
                new Card(Barva.Zeleny, Hodnota.Sedma),
                new Card(Barva.Zaludy, Hodnota.Desitka)
            }.ToList();
            var trump = Barva.Cerveny;

            CardProbabilityTest(1, 0, hand, trump, null);
        }
    }
}
