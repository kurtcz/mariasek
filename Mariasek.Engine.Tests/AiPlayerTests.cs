using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mariasek.Engine.New;
using Mariasek.Engine.New.Configuration;
using System.Collections.Generic;

namespace Mariasek.Engine.Tests
{
    [TestClass]
    //[Ignore]
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
            _aiConfig.Add("SimulationsPerRound", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "SimulationsPerRound",
                Value = "100"
            });
            _aiConfig.Add("RuleThreshold", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "RuleThreshold",
                Value = "80"
            });
            _aiConfig.Add("GameThreshold", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "GameThreshold",
                Value = "51"
            });
        }

        private Hra ChooseGameType(string filename, out Dictionary<string, object> props)
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
            
            //typeof(Game).GetProperty("trump").SetValue(g, trump);
            g.LoadGame(filename);

            var trump = aiPlayer.ChooseTrump();
            var talon = aiPlayer.ChooseTalon();
            var flavour = aiPlayer.ChooseGameFlavour();
            var hra = aiPlayer.ChooseGameType();
            props = aiPlayer.ToPropertyDictionary();

            return hra;
        }

        [TestMethod]
        public void Choose107()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\__107uhratelna.hra", out props);

            Assert.IsTrue((hra & Hra.Kilo) != 0, "Ai mel zavolit kilo");
            Assert.IsTrue((hra & Hra.Sedma) != 0, "Ai mel zavolit sedmu");
        }

        [TestMethod]
        public void DoNotChoose107()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\__107neuhratelna.hra", out props);

            Assert.IsTrue((hra & Hra.Kilo) == 0, "Ai nemel zavolit kilo");
            Assert.IsTrue((hra & Hra.Sedma) != 0, "Ai mel zavolit sedmu");
        }

        [TestMethod]
        public void Choose7()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\__sedma.hra", out props);

            Assert.IsTrue((hra & Hra.Sedma) != 0, "Ai mel zavolit sedmu");
        }

        [TestMethod]
        public void ChooseBetl()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\Betl.hra", out props);

            Assert.IsTrue((hra & Hra.Betl) != 0, "Ai mel zavolit betla");
        }

        [TestMethod]
        public void ChooseDurch()
        {
            Dictionary<string, object> props;
            var hra = ChooseGameType(@"Scenarios\ChooseGame\Durch.hra", out props);

            Assert.IsTrue((hra & Hra.Durch) != 0, "Ai mel zavolit durcha");
        }
    }
}
