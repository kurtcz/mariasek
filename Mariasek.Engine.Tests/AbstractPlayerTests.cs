using Mariasek.Engine.New;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.Tests
{
    [TestFixture]
    public class AbstractPlayerTests
    {
        private Game g;
        private AbstractPlayer player1;
        private AbstractPlayer player2;
        private AbstractPlayer player3;

        [SetUp]
        public void Initialize()
        {
            g = new Game()
            {
                SkipBidding = false
            };
            player1 = new DummyPlayer(g);
            player2 = new DummyPlayer(g);
            player3 = new DummyPlayer(g);

            g.RegisterPlayers(
                    player1,
                    player2,
                    player3);
        }

        private Renonc CardValidityTest(Hra gameType, Card trumpCard, List<Card> hand, Card c, Card first = null, Card second = null)
        {
            g.SetProperty("GameType", gameType);
            g.SetProperty("trump", trumpCard != null ? trumpCard.Suit : (Barva?)null);
            g.InvokeMethod("OnGameTypeChosen", new GameTypeChosenEventArgs
                {
                    GameStartingPlayerIndex = 0,
                    GameType = gameType,
                    TrumpCard = trumpCard
                });
            if(second != null)
            {
                return player1.InvokeMethod<Renonc>("IsCardValid", c, first, second); 
            }
            else if (first != null)
            {
                return player1.InvokeMethod<Renonc>("IsCardValid", c, first);
            }
            else
            {
                return player1.InvokeMethod<Renonc>("IsCardValid", c); 
            }
        }

        [Category("AbstractPlayer card validity tests")]
        [Test]
        public void PrematureTrumpSevenTest()
        {
            player1.Hand = new [] {
                new Card(Barva.Cerveny, Hodnota.Sedma),
                new Card(Barva.Cerveny, Hodnota.Osma)
            }.ToList();

            var c = player1.Hand[0];
            var trumpCard = new Card(Barva.Cerveny, Hodnota.Devitka);
            var result = CardValidityTest(Hra.Hra, trumpCard, player1.Hand, c);

            Assert.AreEqual(Renonc.Ok, result, "Pri hre nemusi hrac hrat trumfovou sedmu az nakonec");

            result = CardValidityTest(Hra.Sedma, trumpCard, player1.Hand, c);
            Assert.AreEqual(Renonc.NehrajSedmu, result, "Pri sedme musi hrac hrat trumfovou sedmu az nakonec");
        }

        [Category("AbstractPlayer card validity tests")]
        [Test]
        public void PrematureQueenTest()
        {
            player1.Hand = new[] {
                new Card(Barva.Cerveny, Hodnota.Kral),
                new Card(Barva.Cerveny, Hodnota.Svrsek)
            }.ToList();

            var c = player1.Hand[0];
            var trumpCard = new Card(Barva.Cerveny, Hodnota.Devitka);
            var result = CardValidityTest(Hra.Hra, trumpCard, player1.Hand, c);

            Assert.AreEqual(Renonc.HrajSvrska, result, "Pri hlasu je treba hrat nejdriv svrska");

            result = CardValidityTest(Hra.Betl, null, player1.Hand, c);
            Assert.AreEqual(Renonc.Ok, result, "Pri spatne hre nemusi hrac hrat svrska driv nez krale");
        }

        [Category("AbstractPlayer card validity tests")]
        [Test]
        public void TrumpTest()
        {
            player1.Hand = new[] {
                new Card(Barva.Cerveny, Hodnota.Sedma),
                new Card(Barva.Kule, Hodnota.Osma)
            }.ToList();

            var first = new Card(Barva.Zeleny, Hodnota.Osma);
            var c = player1.Hand[1];
            var trumpCard = new Card(Barva.Cerveny, Hodnota.Devitka);
            var result = CardValidityTest(Hra.Hra, trumpCard, player1.Hand, c, first);

            Assert.AreEqual(Renonc.HrajTrumf, result, "Hrac musi hrat trumf");
        }

        [Category("AbstractPlayer card validity tests")]
        [Test]
        public void PlayHigherCardTest()
        {
            player1.Hand = new[] {
                new Card(Barva.Kule, Hodnota.Kral),
                new Card(Barva.Kule, Hodnota.Osma)
            }.ToList();

            var first = new Card(Barva.Kule, Hodnota.Devitka);
            var c = player1.Hand[1];
            var trumpCard = new Card(Barva.Cerveny, Hodnota.Devitka);
            var result = CardValidityTest(Hra.Hra, trumpCard, player1.Hand, c, first);

            Assert.AreEqual(Renonc.JdiVejs, result, "Hrac musi jit vejs");
        }

        [Category("AbstractPlayer card validity tests")]
        [Test]
        public void PlayLowerCardAfterTrumpTest()
        {
            player1.Hand = new[] {
                new Card(Barva.Kule, Hodnota.Kral),
                new Card(Barva.Kule, Hodnota.Osma)
            }.ToList();

            var first = new Card(Barva.Kule, Hodnota.Devitka);
            var second = new Card(Barva.Cerveny, Hodnota.Devitka);
            var c = player1.Hand[1];
            var trumpCard = new Card(Barva.Cerveny, Hodnota.Devitka);
            var result = CardValidityTest(Hra.Hra, trumpCard, player1.Hand, c, first, second);

            Assert.AreEqual(Renonc.Ok, result, "Hrac uz nemusi jit vejs po trumfove karte");
        }
    }
}
