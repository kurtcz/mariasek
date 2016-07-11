using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mariasek.Engine.New;
using Mariasek.Engine.New.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace Mariasek.Engine.Tests
{
    [TestClass]
    public class AiPlayerTests
    {
        private ParameterConfigurationElementCollection _aiConfig;

        [TestInitialize]
        public void Initialize()
        {
            _aiConfig = new Mariasek.Engine.New.Configuration.ParameterConfigurationElementCollection();

            _aiConfig.Add("AiCheating", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "AiCheating",
                Value = "false"
            });
            _aiConfig.Add("RoundsToCompute", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "RoundsToCompute",
                Value = "1"
            });
            _aiConfig.Add("CardSelectionStrategy", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "CardSelectionStrategy",
                Value = "MaxCount"
            }); 
            _aiConfig.Add("SimulationsPerGameType", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "SimulationsPerGameType",
                Value = "25"
            });
            _aiConfig.Add("SimulationsPerGameTypePerSecond", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "SimulationsPerGameTypePerSecond",
                Value = "0"
            });
            _aiConfig.Add("SimulationsPerRound", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "SimulationsPerRound",
                Value = "100"
            });
            _aiConfig.Add("SimulationsPerRoundPerSecond", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "SimulationsPerRoundPerSecond",
                Value = "0"
            });
            _aiConfig.Add("MaxSimulationTimeMs", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "MaxSimulationTimeMs",
                Value = "3000"
            });
            _aiConfig.Add("RuleThreshold", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "RuleThreshold",
                Value = "80"
            });
            _aiConfig.Add("GameThreshold", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "GameThreshold",
                Value = "51|52"
            });
            _aiConfig.Add("MaxDoubleCount", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "MaxDoubleCount",
                Value = "5"
            });
            _aiConfig.Add("SigmaMultiplier", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "SigmaMultiplier",
                Value = "0"
            });
        }

        private Hra ChooseGameType(string filename, out Dictionary<string, object> props, bool cheat = false)
        {
            var g = new Game()
            {
                SkipBidding = false
            };

            if(cheat)
            {
                _aiConfig["Cheat"] = new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                {
                    Name = "AiCheating",
                    Value = "true"
                };
            }

            var player1 = new DummyPlayer(g);
            var aiPlayer = new AiPlayer(g, _aiConfig);
            var player3 = new DummyPlayer(g);

            g.RegisterPlayers(
                    player1,
                    aiPlayer,
                    player3);
            //zacina aiPlayer (player2)
            g.LoadGame(filename);
            g.InvokeMethod("ChooseGame");
            props = aiPlayer.ToPropertyDictionary();

            return g.GameType;
        }

        private void ChooseTrumpAndTalon(string filename, out Dictionary<string, object> props)
        {
            var g = new Game()
            {
                SkipBidding = false
            };

            //if (cheat)
            //{
            //    _aiConfig["Cheat"] = new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            //    {
            //        Name = "AiCheating",
            //        Value = "true"
            //    };
            //}

            var player1 = new DummyPlayer(g);
            var aiPlayer = new AiPlayer(g, _aiConfig);
            var player3 = new DummyPlayer(g);

            g.RegisterPlayers(
                    player1,
                    aiPlayer,
                    player3);
            //zacina aiPlayer (player2)
            g.LoadGame(filename);
            //g.InvokeMethod("ChooseGame");
            var trumpCard = aiPlayer.ChooseTrump();
            var talon = aiPlayer.ChooseTalon();
            props = aiPlayer.ToPropertyDictionary();

            return;
        }

        private Hra GetOpponentsBidsAndDoubles(string filename, out Dictionary<string, object> props)
        {
            var g = new Game()
            {
                SkipBidding = false
            };
            var player1 = new DummyPlayer(g);
            var aiPlayer = new AiPlayer(g, _aiConfig);
            var player3 = new DummyPlayer(g);

            g.RegisterPlayers(
                    player1,
                    aiPlayer,
                    player3);

            g.LoadGame(filename);
            
            var bidding = new Bidding(g);

            g.InvokeMethod("OnGameTypeChosen", new GameTypeChosenEventArgs
            {
                GameStartingPlayerIndex = g.GameStartingPlayerIndex, 
                GameType = g.GameType, 
                TrumpCard = g.TrumpCard
            });
            bidding.InvokeMethod("AdjustValidBidsForPlayer", 1, 0);

            var hra = aiPlayer.GetBidsAndDoubles(bidding);

            props = aiPlayer.ToPropertyDictionary();

            return hra;
        }

        #region Open Hands Game choice tests
        [TestCategory("Open Hands Game choice tests")]
        [TestMethod]
        public void OpenHandsChoose107()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\__107uhratelna.hra", out props, true);

            Assert.IsTrue((hra & Hra.Kilo) != 0, string.Format("Ai mel zavolit kilo ale zvolil {0}", hra));
            Assert.IsTrue((hra & Hra.Sedma) != 0, string.Format("Ai mel zavolit sedmu ale zvolil {0}", hra));
        }

        [TestCategory("Open Hands Game choice tests")]
        [TestMethod]
        public void OpenHandsDoNotChoose107()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\__107neuhratelna.hra", out props, true);

            Assert.IsTrue((hra & Hra.Kilo) == 0, string.Format("Ai nemel zavolit kilo ale zvolil {0}", hra));
            Assert.IsTrue((hra & Hra.Sedma) != 0, string.Format("Ai mel zavolit sedmu ale zvolil {0}", hra));
        }

        [TestCategory("Open Hands Game choice tests")]
        [TestMethod]
        public void OpenHandsChoose7()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\__sedma.hra", out props, true);

            Assert.IsTrue((hra & Hra.Sedma) != 0, string.Format("Ai mel zavolit sedmu ale zvolil {0}", hra));
        }

        [TestCategory("Open Hands Game choice tests")]
        [TestMethod]
        public void OpenHandsChooseBetl()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\Betl.hra", out props, true);

            Assert.IsTrue((hra & Hra.Betl) != 0, string.Format("Ai mel zavolit betla ale zvolil {0}", hra));
        }

        [TestCategory("Open Hands Game choice tests")]
        [TestMethod]
        public void OpenHandsChooseDurch()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\Durch.hra", out props, true);

            Assert.IsTrue((hra & Hra.Durch) != 0, string.Format("Ai mel zavolit durcha, ale zvolil {0}", hra));
        }
        #endregion

        #region Game choice tests
        [TestCategory("Game choice tests")]
        [TestMethod]
        public void Choose107()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\__107uhratelna.hra", out props);

            Assert.IsTrue((hra & Hra.Kilo) != 0, string.Format("Ai mel zavolit kilo ale zvolil {0}", hra));
            Assert.IsTrue((hra & Hra.Sedma) != 0, string.Format("Ai mel zavolit sedmu ale zvolil {0}", hra));
        }

        [TestCategory("Game choice tests")]
        [TestMethod]
        public void DoNotChoose107()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\__107neuhratelna.hra", out props);

            Assert.IsTrue((hra & Hra.Kilo) == 0, string.Format("Ai nemel zavolit kilo ale zvolil {0}", hra));
            Assert.IsTrue((hra & Hra.Sedma) != 0, string.Format("Ai mel zavolit sedmu ale zvolil {0}", hra));
        }

        [TestCategory("Game choice tests")]
        [TestMethod]
        public void Choose7()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\__sedma.hra", out props);

            Assert.IsTrue((hra & Hra.Sedma) != 0, string.Format("Ai mel zavolit sedmu ale zvolil {0}", hra));
        }

        [TestCategory("Game choice tests")]
        [TestMethod]
        public void ChooseBetl()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\Betl.hra", out props);

            Assert.IsTrue((hra & Hra.Betl) != 0, string.Format("Ai mel zavolit betla ale zvolil {0}", hra));
        }

        [TestCategory("Game choice tests")]
        [TestMethod]
        public void ChooseDurch()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\Durch.hra", out props);

            Assert.IsTrue((hra & Hra.Durch) != 0, string.Format("Ai mel zavolit durcha, ale zvolil {0}", hra));
        }

        [TestMethod]
        public void ChooseTrumpAndTalon1()
        {
            var g = new Game();
            var aiPlayer = new AiPlayer(g, _aiConfig);
            var player2 = new DummyPlayer(g);
            var player3 = new DummyPlayer(g);
            var hand = new Hand(
                new[] {
                    new Card(Barva.Zeleny, Hodnota.Eso),
                    new Card(Barva.Zeleny, Hodnota.Desitka),
                    new Card(Barva.Zeleny, Hodnota.Kral),
                    new Card(Barva.Zeleny, Hodnota.Svrsek),
                    new Card(Barva.Zeleny, Hodnota.Sedma),
                    new Card(Barva.Kule, Hodnota.Eso),
                    new Card(Barva.Kule, Hodnota.Osma),
                    new Card(Barva.Zaludy, Hodnota.Osma),
                    new Card(Barva.Cerveny, Hodnota.Desitka),
                    new Card(Barva.Cerveny, Hodnota.Kral),
                    new Card(Barva.Cerveny, Hodnota.Spodek),
                    new Card(Barva.Cerveny, Hodnota.Devitka)
                });
            aiPlayer.Hand = hand;
            var trumpCard = aiPlayer.ChooseTrump();
            var talon = aiPlayer.InvokeMethod<IEnumerable<Card>>("ChooseNormalTalon", aiPlayer.Hand, trumpCard);
            var props = aiPlayer.ToPropertyDictionary();

            Assert.AreEqual(2, talon.Count(), "Bad number of cards in talon");
            Assert.IsFalse(talon.Any(i => i.Value == Hodnota.Eso || i.Value == Hodnota.Desitka), "talon nesmi obsahovat eso nebo desitku");
            Assert.IsFalse(talon.Contains(trumpCard), "talon nesmi obsahovat kartu co ukazuju jako trumf");
        }

        [TestMethod]
        public void ChooseTrumpAndTalon2()
        {
            var g = new Game();
            var aiPlayer = new AiPlayer(g, _aiConfig);
            var player2 = new DummyPlayer(g);
            var player3 = new DummyPlayer(g);
            var hand = new Hand(
                new[] {
                    new Card(Barva.Zeleny, Hodnota.Eso),
                    new Card(Barva.Zeleny, Hodnota.Desitka),
                    new Card(Barva.Zeleny, Hodnota.Kral),
                    new Card(Barva.Zeleny, Hodnota.Svrsek),
                    new Card(Barva.Zeleny, Hodnota.Spodek),
                    new Card(Barva.Zeleny, Hodnota.Osma),
                    new Card(Barva.Zeleny, Hodnota.Sedma),
                    new Card(Barva.Kule, Hodnota.Eso),
                    new Card(Barva.Cerveny, Hodnota.Desitka),
                    new Card(Barva.Cerveny, Hodnota.Kral),
                    new Card(Barva.Zaludy, Hodnota.Eso),
                    new Card(Barva.Zaludy, Hodnota.Desitka)
                });
            aiPlayer.Hand = hand;
            var trumpCard = aiPlayer.ChooseTrump();
            var talon = aiPlayer.InvokeMethod<IEnumerable<Card>>("ChooseNormalTalon", aiPlayer.Hand, trumpCard);
            var props = aiPlayer.ToPropertyDictionary();

            Assert.IsFalse(talon.Any(i => i.Value == Hodnota.Eso || i.Value == Hodnota.Desitka), "talon nesmi obsahovat eso nebo desitku");
            Assert.IsFalse(talon.Contains(trumpCard), "talon nesmi obsahovat kartu co ukazuju jako trumf");
        }

        [TestMethod]
        public void ChooseTrumpAndTalon3()
        {
            var g = new Game();
            var aiPlayer = new AiPlayer(g, _aiConfig);
            var player2 = new DummyPlayer(g);
            var player3 = new DummyPlayer(g);
            var hand = new Hand(
                new[] {
                    new Card(Barva.Zeleny, Hodnota.Sedma),
                    new Card(Barva.Cerveny, Hodnota.Osma),
                    new Card(Barva.Cerveny, Hodnota.Devitka),
                    new Card(Barva.Cerveny, Hodnota.Spodek),
                    new Card(Barva.Cerveny, Hodnota.Kral),
                    new Card(Barva.Cerveny, Hodnota.Svrsek),
                    new Card(Barva.Cerveny, Hodnota.Desitka),
                    new Card(Barva.Zeleny, Hodnota.Osma),
                    new Card(Barva.Zeleny, Hodnota.Devitka),
                    new Card(Barva.Cerveny, Hodnota.Eso),
                    new Card(Barva.Zeleny, Hodnota.Svrsek),
                    new Card(Barva.Kule, Hodnota.Spodek)
                });
            aiPlayer.Hand = hand;
            var trumpCard = aiPlayer.ChooseTrump();
            var talon = aiPlayer.InvokeMethod<IEnumerable<Card>>("ChooseNormalTalon", aiPlayer.Hand, trumpCard);
            var props = aiPlayer.ToPropertyDictionary();

            Assert.IsFalse(talon.Any(i => i.Value == Hodnota.Eso || i.Value == Hodnota.Desitka), "talon nesmi obsahovat eso nebo desitku");
            Assert.IsFalse(talon.Contains(trumpCard), "talon nesmi obsahovat kartu co ukazuju jako trumf");
        }
        #endregion

        #region Bidding tests
        [TestCategory("Bidding tests")]
        [TestMethod]
        public void Call107Against()
        {
            Dictionary<string, object> props;
            var hra = GetOpponentsBidsAndDoubles(@"Scenarios\Bidding\__107proti.hra", out props);

            Assert.IsTrue((hra & Hra.KiloProti) != 0, "Ai mel hlasit kilo proti");
            Assert.IsTrue((hra & Hra.SedmaProti) != 0, "Ai mel hlasit sedmu proti");
        }

        [TestCategory("Bidding tests")]
        [TestMethod]
        public void Flek107()
        {
            Dictionary<string, object> props;
            var hra = GetOpponentsBidsAndDoubles(@"Scenarios\Bidding\__107flek.hra", out props);

            Assert.IsTrue((hra & Hra.Kilo) != 0, "Ai mel flekovat kilo (ale skore bylo: {0})", props["_hundredsBalance"]);
            Assert.IsTrue((hra & Hra.Sedma) != 0, string.Format("Ai mel flekovat sedmu (ale skore bylo: {0})", props["_sevensBalance"]));
        }

        [TestCategory("Bidding tests")]
        [TestMethod]
        [Ignore] //Betla neflekujeme
        public void FlekBetl()
        {
            Dictionary<string, object> props;
            var hra = GetOpponentsBidsAndDoubles(@"Scenarios\Bidding\__betlflek.hra", out props);

            Assert.IsTrue((hra & Hra.Betl) != 0, string.Format("Ai mel flekovat betla (ale skore bylo: {0})", props["_betlBalance"]));
        }

        [TestCategory("Bidding tests")]
        [TestMethod]
        public void FlekDurch()
        {
            Dictionary<string, object> props;
            var hra = GetOpponentsBidsAndDoubles(@"Scenarios\Bidding\__durchflek.hra", out props);

            Assert.IsTrue((hra & Hra.Durch) != 0, string.Format("Ai mel flekovat durch (ale skore bylo: {0})", props["_durchBalance"]));
        }
        #endregion
    }
}
