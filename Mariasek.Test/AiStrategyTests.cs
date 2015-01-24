using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using Mariasek.Engine;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mariasek.Test
{
    [TestClass]
    [DeploymentItem(@"Logger\LoggerSetup.xml")]
    [DeploymentItem(@"TestFiles\Player1", @"TestFiles\Player1")]
    [DeploymentItem(@"TestFiles\Player2", @"TestFiles\Player2")]
    [DeploymentItem(@"TestFiles\Player3", @"TestFiles\Player3")]
    public class AiStrategyTests
    {
        private AiStrategy PrepareAiStrategyTestFromFile(string filename)
        {
            var playerSettings = new AiPlayerSettings
            {
                Cheat = true,
                RoundsToCompute = 10
            };
            var g = Game.LoadGame(filename, playerSettings);
            var aiPlayer = g.players[0];
            var hands = g.players.Select(i => new Hand(i.Hand)).ToArray();
            var aiStrategy = new AiStrategy(g.trump, hands)
            {
                MyIndex = Array.IndexOf(g.players, aiPlayer),
                MyName = aiPlayer.Name,
                TeamMateIndex = aiPlayer.TeamMateIndex,
                RoundNumber = g.RoundNumber
            };

            return aiStrategy;
        }

        private void RunAiStrategyTestForPlayer1(PrivateObject aiStrategy, int ruleNo)
        {
            foreach (var rule in (IEnumerable<AiRule>)aiStrategy.Invoke("GetRules1", aiStrategy.GetField("_hands")))
            {
                var cardToPlay = rule.ChooseCard1();

                if (cardToPlay != null)
                {
                    Assert.AreEqual(ruleNo, rule.Order);
                    break;
                }
            }
        }

        #region Player1 Rule 0

        [TestMethod]
        public void TestPlayer1Rule0Alone()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule0Alone.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 0);
        }

        [TestMethod]
        public void TestPlayer1Rule0WithPlayer2()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule0WithPlayer2.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 0);
        }

        [TestMethod]
        public void TestPlayer1Rule0WithPlayer3()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule0WithPlayer3.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 0);
        }

        #endregion
        
        #region Player1 Rule 1

        [TestMethod]
        public void TestPlayer1Rule1Alone()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule1Alone.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 1);
        }

        [TestMethod]
        public void TestPlayer1Rule1WithPlayer2()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule1WithPlayer2.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 1);
        }

        [TestMethod]
        public void TestPlayer1Rule1WithPlayer3()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule1WithPlayer3.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 1);
        }

        #endregion

        #region Player1 Rule 2

        [TestMethod]
        public void TestPlayer1Rule2AloneV1()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule2Alone V1.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 2);
        }

        [TestMethod]
        public void TestPlayer1Rule2AloneV2()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule2Alone V2.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 2);
        }

        [TestMethod]
        public void TestPlayer1Rule2WithPlayer2V1()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule2WithPlayer2 V1.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 2);
        }

        [TestMethod]
        public void TestPlayer1Rule2WithPlayer2V2()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule2WithPlayer2 V2.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 2);
        }

        [TestMethod]
        public void TestPlayer1Rule2WithPlayer3V1()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule2WithPlayer3 V1.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 2);
        }

        [TestMethod]
        public void TestPlayer1Rule2WithPlayer3V2()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule2WithPlayer3 V2.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 2);
        }

        #endregion

        #region Player1 Rule 3

        [TestMethod]
        public void TestPlayer1Rule3AloneV1()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule3Alone V1.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 3);
        }

        [TestMethod]
        public void TestPlayer1Rule3AloneV2()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule3Alone V2.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 3);
        }

        [TestMethod]
        public void TestPlayer1Rule3WithPlayer2()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule3WithPlayer2.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 3);
        }

        [TestMethod]
        public void TestPlayer1Rule3WithPlayer3()
        {
            var aiStrategy = PrepareAiStrategyTestFromFile(@"TestFiles\Player1\Rule3WithPlayer3.hra");

            RunAiStrategyTestForPlayer1(new PrivateObject(aiStrategy), 3);
        }

        #endregion

    }
}
