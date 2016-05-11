using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mariasek.Engine.New;
using Moq;

namespace Mariasek.Engine.Tests
{
    [TestClass]
    public class GameTests
    {
        private Game g;
        private Mock<DummyPlayer> player1;
        private Mock<DummyPlayer> player2;
        private Mock<DummyPlayer> player3;
        private Card trumpCard;
        private Func<List<Card>> talon;

        [TestInitialize]
        public void Initialize()
        {
            g = new Game();
            player1 = new Mock<DummyPlayer>(g);
            player2 = new Mock<DummyPlayer>(g);
            player3 = new Mock<DummyPlayer>(g);
            trumpCard = new Card(Barva.Cerveny, Hodnota.Eso);
            talon = () => new Card[]
            {
                new Card(Barva.Zeleny, Hodnota.Osma),
                new Card(Barva.Zeleny, Hodnota.Devitka)
            }.ToList();

            player1.Name = "Hrac1";
            player1.Object.Name = "Hrac1";
            player2.Name = "Hrac2";
            player2.Object.Name = "Hrac2";
            player3.Name = "Hrac3";
            player3.Object.Name = "Hrac3";

            g.RegisterPlayers(player1.Object, player2.Object, player3.Object);
            g.SetProperty<int>("GameStartingPlayerIndex", 0);
            g.SetProperty<Hra>("GameType", Hra.Hra);
            g.SetProperty<Bidding>("Bidding", new Bidding(g));
        }

        [TestCategory("Game Flavour and Type Choice Tests")]
        [TestMethod]
        public void Player1ChoosesDurch()
        {
            player1.Setup(m => m.ChooseTrump()).Returns(trumpCard);
            player1.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Bad);
            player1.Setup(m => m.ChooseTalon()).Returns(talon());
            player1.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Durch);
            player1.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(Hra.Durch);
            
            player2.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(0);
            player3.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(0);

            g.InvokeMethod("ChooseGame");

            Assert.AreEqual(player1.Object, g.GameStartingPlayer);
            Assert.AreEqual(Hra.Durch, g.GameType);
        }

        [TestCategory("Game Flavour and Type Choice Tests")]
        [TestMethod]
        public void Player1ChoosesBetl()
        {
            player1.Setup(m => m.ChooseTrump()).Returns(trumpCard);
            player1.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Bad);
            player1.Setup(m => m.ChooseTalon()).Returns(talon());
            player1.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Betl);
            player1.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(Hra.Durch);

            player2.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Good);
            player2.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(0);
            player3.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Good);
            player3.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(0);

            g.InvokeMethod("ChooseGame");

            Assert.AreEqual(player1.Object, g.GameStartingPlayer);
            Assert.AreEqual(Hra.Betl, g.GameType);
        }

        [TestCategory("Game Flavour and Type Choice Tests")]
        [TestMethod]
        public void Player1ChoosesBetlPlayer3Durch()
        {
            var player1BidsAndDoubles = new Queue<Hra> (new Hra[] {Hra.Betl, 0});

            player1.Setup(m => m.ChooseTrump()).Returns(trumpCard);
            player1.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Bad);
            player1.Setup(m => m.ChooseTalon()).Returns(talon());
            player1.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Betl);
            player1.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(() => player1BidsAndDoubles.Dequeue());

            player2.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Good);
            player2.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(0);
            player3.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Bad);
            player3.Setup(m => m.ChooseTalon()).Returns(talon());
            player3.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Durch);
            player3.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(Hra.Durch);

            g.InvokeMethod("ChooseGame");

            Assert.AreEqual(player3.Object, g.GameStartingPlayer);
            Assert.AreEqual(Hra.Durch, g.GameType);
        }

        [TestCategory("Game Flavour and Type Choice Tests")]
        [TestMethod]
        public void Player3ChoosesDurch()
        {
            player1.Setup(m => m.ChooseTrump()).Returns(trumpCard);
            player1.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Good);
            player1.Setup(m => m.ChooseTalon()).Returns(talon());

            player2.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Good);

            player3.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Bad);
            player3.Setup(m => m.ChooseTalon()).Returns(talon());
            player3.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Durch);
            player3.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(Hra.Durch);

            player1.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(0);
            player2.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(0);

            g.InvokeMethod("ChooseGame");

            Assert.AreEqual(player3.Object, g.GameStartingPlayer);
            Assert.AreEqual(Hra.Durch, g.GameType);
        }

        [TestCategory("Game Flavour and Type Choice Tests")]
        [TestMethod]
        public void Player2ChoosesBetlPlayer1Durch()
        {
            var player1GameFlavours = new Queue<GameFlavour> (new []{GameFlavour.Good, GameFlavour.Bad});
            var player2BidsAndDoubles = new Queue<Hra>(new Hra[] { Hra.Betl, 0 });

            player1.Setup(m => m.ChooseTrump()).Returns(trumpCard);
            player1.Setup(m => m.ChooseGameFlavour()).Returns(() => player1GameFlavours.Dequeue());
            player1.Setup(m => m.ChooseTalon()).Returns(talon());
            player1.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Durch);

            player2.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Bad);
            player2.Setup(m => m.ChooseTalon()).Returns(talon());
            player2.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Betl);
            player2.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(() => player2BidsAndDoubles.Dequeue());

            player3.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Good);
            player3.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(0);

            player1.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(Hra.Durch);

            g.InvokeMethod("ChooseGame");

            Assert.AreEqual(player1.Object, g.GameStartingPlayer);
            Assert.AreEqual(Hra.Durch, g.GameType);
        }

        [TestCategory("Game Flavour and Type Choice Tests")]
        [TestMethod]
        public void Player3ChoosesBetlPlayer2Durch()
        {
            var player2GameFlavours = new Queue<GameFlavour>(new[] { GameFlavour.Good, GameFlavour.Bad });
            var player2BidsAndDoubles = new Queue<Hra>(new Hra[] { 0, Hra.Durch });
            var player3BidsAndDoubles = new Queue<Hra>(new Hra[] { Hra.Betl, 0 });

            player1.Setup(m => m.ChooseTrump()).Returns(trumpCard);
            player1.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Good);
            player1.Setup(m => m.ChooseTalon()).Returns(talon());
            player1.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(0);

            player2.Setup(m => m.ChooseGameFlavour()).Returns(() => player2GameFlavours.Dequeue());
            player2.Setup(m => m.ChooseTalon()).Returns(talon());
            player2.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Durch);
            player2.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(() => player2BidsAndDoubles.Dequeue());

            player3.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Bad);
            player3.Setup(m => m.ChooseTalon()).Returns(talon());
            player3.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Betl);
            player3.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(() => player3BidsAndDoubles.Dequeue());

            g.InvokeMethod("ChooseGame");

            Assert.AreEqual(player2.Object, g.GameStartingPlayer);
            Assert.AreEqual(Hra.Durch, g.GameType);
        }

        [TestCategory("Game Flavour and Type Choice Tests")]
        [TestMethod]
        public void Player3ChoosesBetlPlayer1Flek()
        {
            player1.Setup(m => m.ChooseTrump()).Returns(trumpCard);
            player1.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Good);
            player1.Setup(m => m.ChooseTalon()).Returns(talon());
            player1.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(Hra.Betl);

            player2.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Good);
            player2.Setup(m => m.ChooseTalon()).Returns(talon());
            player2.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Durch);
            player2.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(0);

            player3.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Bad);
            player3.Setup(m => m.ChooseTalon()).Returns(talon());
            player3.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Betl);
            player3.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(0);

            g.InvokeMethod("ChooseGame");

            Assert.AreEqual(player3.Object, g.GameStartingPlayer);
            Assert.AreEqual(Hra.Betl, g.GameType);
            player1.Verify(m => m.GetBidsAndDoubles(It.IsAny<Bidding>()), Times.Once);
            player2.Verify(m => m.GetBidsAndDoubles(It.IsAny<Bidding>()), Times.Once);
            player3.Verify(m => m.GetBidsAndDoubles(It.IsAny<Bidding>()), Times.Once);
        }

        [TestCategory("Game Flavour and Type Choice Tests")]
        [TestMethod]
        public void Player3ChoosesBetlPlayer2Flek()
        {
            player1.Setup(m => m.ChooseTrump()).Returns(trumpCard);
            player1.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Good);
            player1.Setup(m => m.ChooseTalon()).Returns(talon());
            player1.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(0);

            player2.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Good);
            player2.Setup(m => m.ChooseTalon()).Returns(talon());
            player2.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Durch);
            player2.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(Hra.Betl);

            player3.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Bad);
            player3.Setup(m => m.ChooseTalon()).Returns(talon());
            player3.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Betl);
            player3.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(0);

            g.InvokeMethod("ChooseGame");

            Assert.AreEqual(player3.Object, g.GameStartingPlayer);
            Assert.AreEqual(Hra.Betl, g.GameType);
            player1.Verify(m => m.GetBidsAndDoubles(It.IsAny<Bidding>()), Times.Once);
            player2.Verify(m => m.GetBidsAndDoubles(It.IsAny<Bidding>()), Times.Once);
            player3.Verify(m => m.GetBidsAndDoubles(It.IsAny<Bidding>()), Times.Once);
        }

        [TestCategory("Game Flavour and Type Choice Tests")]
        [TestMethod]
        public void Player3ChoosesBetlPlayer2FlekPlayer3Re()
        {
            var player2BidsAndDoubles = new Queue<Hra>(new Hra[] { Hra.Betl, 0 });

            player1.Setup(m => m.ChooseTrump()).Returns(trumpCard);
            player1.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Good);
            player1.Setup(m => m.ChooseTalon()).Returns(talon());
            player1.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(0);

            player2.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Good);
            player2.Setup(m => m.ChooseTalon()).Returns(talon());
            player2.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Durch);
            player2.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(() => player2BidsAndDoubles.Dequeue());

            player3.Setup(m => m.ChooseGameFlavour()).Returns(GameFlavour.Bad);
            player3.Setup(m => m.ChooseTalon()).Returns(talon());
            player3.Setup(m => m.ChooseGameType(It.IsAny<Hra>())).Returns(Hra.Betl);
            player3.Setup(m => m.GetBidsAndDoubles(It.IsAny<Bidding>())).Returns(Hra.Betl);

            g.InvokeMethod("ChooseGame");

            Assert.AreEqual(player3.Object, g.GameStartingPlayer);
            Assert.AreEqual(Hra.Betl, g.GameType);
            player1.Verify(m => m.GetBidsAndDoubles(It.IsAny<Bidding>()), Times.Exactly(2));
            player2.Verify(m => m.GetBidsAndDoubles(It.IsAny<Bidding>()), Times.Exactly(2));
            player3.Verify(m => m.GetBidsAndDoubles(It.IsAny<Bidding>()), Times.Once);
        }

        [TestCategory("Bid and double tests")]
        [TestMethod]
        public void DoublingTest()
        {
            g.SetProperty("GameStartingPlayerIndex", 0); //player1
            g.SetProperty("GameType", Hra.Hra);
            g.SetProperty("trump", (Barva?)Barva.Cerveny);

            var bidding = new Bidding(g);
            var player2BidsAndDoubles = new Queue<Hra>(new Hra[] { Hra.Hra, 0 });

            player2.Setup(m => m.GetBidsAndDoubles(bidding)).Returns(0);
            player3.Setup(m => m.GetBidsAndDoubles(bidding)).Returns(Hra.Hra); //flek na hru
            player1.Setup(m => m.GetBidsAndDoubles(bidding)).Returns(0);

            bidding.CompleteBidding();

            player1.Verify(m => m.GetBidsAndDoubles(It.IsAny<Bidding>()), Times.Once);
            player2.Verify(m => m.GetBidsAndDoubles(It.IsAny<Bidding>()), Times.Once);
            player3.Verify(m => m.GetBidsAndDoubles(It.IsAny<Bidding>()), Times.Once);
        }
    }
}
