using System;
using System.Collections.Generic;
using System.Configuration;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Mariasek.Engine.New;
using Mariasek.Engine.New.Configuration;
using CLAP;
using System.IO;
using Accord.Neuro;
using Accord.Neuro.Learning;

namespace Mariasek.Console
{
    class Program
    {
        private static string nnFilename = "Mariasek.nn";
        private static IPlayerSettingsReader playerSettingsReader;
        private static string programFolder = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
        private static string resultFilename;
        private static Game g;

        #region CLAP verbs
        
        [Help(Aliases="help,h,?")]
        public static void ShowHelp(string help)
        {
            System.Console.WriteLine("{0} {1}\nUsage:\n{2}",
                Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName),
                System.Diagnostics.Process.GetCurrentProcess().MainModule.FileVersionInfo.ProductVersion,
                help);
        }

        [Global(Aliases="config", Description="Use a custom config file")]
        public static void SetConfigFile(string filename)
        {
            if(!File.Exists(filename))
            {
                System.Console.WriteLine("Error loading custom config file {0}", filename);
                System.Environment.Exit(-1);
            }
            AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", filename);
            System.Console.WriteLine("Using config file {0}", filename);
        }

        [Global(Aliases="result", Description="Save finished game to file")]
        public static void SetResultFilename(string filename)
        {
            resultFilename = filename;
        }

        [Verb(Aliases="new", Description="Starts a new random game", IsDefault=true)]
        public static void NewGame([DefaultValue(false)] bool skipGame, [DefaultValue(0)] Hra gameType)
        {
            bool finished = false;
            int iterations;
            Deck deck = null;
            bool skipBidding = false;

            bool.TryParse(ConfigurationManager.AppSettings["SkipBidding"], out skipBidding);

            for (iterations = 0; !finished; iterations++)
            {
                playerSettingsReader = new Mariasek.WinSettings.PlayerSettingsReader();
                g = new Game()
                {
                    SkipBidding = skipBidding
                };
                g.RegisterPlayers(playerSettingsReader);

                g.NewGame(gameStartingPlayerIndex: 0, deck: deck); //saves the new game as _temp.hra
                if (!skipGame)
                {
                    finished = PlayGame(gameType == 0 ? (Hra?)null : gameType);
                    deck = g.GetDeckFromLastGame();
                }
                else
                {
                    finished = true;
                }
            }
            if (gameType != 0)
            {
                System.Console.WriteLine("Done generating {0}. {1} games generated in total", gameType, iterations);
            }
        }

        [Verb(Aliases="load", Description="Loads a game from a file")]
        public static void LoadGame(string filename, [DefaultValue(false)] bool classify, string outputDir = null)
        {
            if (!classify)
            {
                playerSettingsReader = new Mariasek.WinSettings.PlayerSettingsReader();
                g = new Game();
                g.RegisterPlayers(playerSettingsReader);

                g.LoadGame(filename);
                PlayGame();
            }
            else
            {
                var gameTypes = new Dictionary<Hra, Tuple<bool, float>>();
                foreach (var gt in Enum.GetValues(typeof(Hra)).Cast<Hra>().Where(gt => gt != Hra.SedmaProti && gt != Hra.KiloProti))
                {
                    playerSettingsReader = new Mariasek.WinSettings.PlayerSettingsReader();
                    g = new Game();
                    g.RegisterPlayers(playerSettingsReader);

                    g.LoadGame(filename);
                    var gameWon = PlayGame(gt, strict: true);

                    gameTypes.Add(gt, new Tuple<bool, float>(gameWon, g.Results.MoneyWon[0]));
                }
                System.Console.WriteLine("-=-=-=-=-");
                foreach (var kv in gameTypes)
                {
                    System.Console.WriteLine("{0}:\t{1}\t{2}", kv.Key, kv.Value.Item1 ? "won" : "lost", kv.Value.Item2.ToString("C", CultureInfo.CreateSpecificCulture("cs-CZ")));
                }
                var gamesWon = 0;
                foreach (var kv in gameTypes.Where(gt => gt.Value.Item1))
                {
                    gamesWon |= (int)kv.Key;
                }

                var classifiedName = Path.Combine(outputDir ?? Path.GetDirectoryName(filename), 
                                                  string.Format("{0}.0x{1:X02}.hra", Path.GetFileNameWithoutExtension(filename), gamesWon));
                File.Copy(filename, classifiedName, true);
            }
        }

        [Verb(Aliases = "learn", Description = "Runs a neural network trainer on the test set")]
        public static void Learn(string inputDir, double learningRate = 0.1, double momentum = 0, double targetError = 30)
        {
            var files = Directory.GetFiles(inputDir);
            var data = new List<Tuple<double[], double[]>>();

            System.Console.WriteLine("Populating data ...");
            foreach (var file in files)
            {
                playerSettingsReader = new Mariasek.WinSettings.PlayerSettingsReader();
                g = new Game();
                g.RegisterPlayers(playerSettingsReader);

                g.LoadGame(file);
                var input = ConvertInput(g.GameStartingPlayer.Hand, g.trump);
                var filename = Path.GetFileNameWithoutExtension(file);
                var tokens = filename.Split('.');

                if (!tokens.Any())
                {
                    return;
                }

                var code = tokens[tokens.Length - 1];

                if (code.Length < 3)
                {
                    return;
                }
                int mask;

                if(!int.TryParse(code.Substring(3), out mask))
                {
                    return;
                }
                var output = ConvertOutput(mask);
                data.Add(new Tuple<double[], double[]>(input, output));
            }
            double[][] inputs = data.Select(i => i.Item1).ToArray();
            double[][] outputs = data.Select(i => i.Item2).ToArray();
            ActivationNetwork network = File.Exists(nnFilename)
                                        ? (ActivationNetwork)ActivationNetwork.Load(nnFilename)
                                        : new ActivationNetwork(
                                            new SigmoidFunction(2),
                                            32,
                                            19,
                                            5);
            // create teacher
            BackPropagationLearning teacher = new BackPropagationLearning(network)
            {
                LearningRate = learningRate,
                Momentum = momentum
            };

            System.Console.WriteLine("Training in progress ...");
            // loop
            bool needToStop = false;
            while (!needToStop)
            {
                // run epoch of learning procedure
                double error = teacher.RunEpoch(inputs, outputs);
                System.Console.Write("\r{0}", error);
                needToStop = error < targetError;
            }
            System.Console.WriteLine("\nDone training, saving neural network to {0}", nnFilename);
            network.Save(nnFilename);
        }
        #endregion

        [Verb(Aliases = "nn", Description = "Let the neural network choose game type")]
        public static void TestNn(string filename)
        {
            var data = new List<Tuple<double[], double[]>>();
            playerSettingsReader = new Mariasek.WinSettings.PlayerSettingsReader();
            g = new Game();
            g.RegisterPlayers(playerSettingsReader);

            g.LoadGame(filename);
            var input = ConvertInput(g.GameStartingPlayer.Hand, g.trump);            
            ActivationNetwork network = new ActivationNetwork(
                                            new SigmoidFunction(2),
                                            32,
                                            19,
                                            5);
            var output = network.Compute(input);

            System.Console.WriteLine(new Hand(g.GameStartingPlayer.Hand));
            var i = 0;
            System.Console.WriteLine("Recommended bids:");
            foreach (var gt in Enum.GetValues(typeof(Hra)).Cast<Hra>().Where(gt => gt != Hra.SedmaProti && gt != Hra.KiloProti))
            {
                if (output[i++] > 0.5)
                {
                    System.Console.WriteLine(gt);
                }
            }
        }

        private static double[] ConvertInput(List<Card> hand, Barva? trump)
        {
            var result = new double[32];

            for(var i = 0; i < result.Length; i++)
            {
                result[i] = -1;
            }
            foreach(var c in hand)
            {
                result[c.Num] = 1;
            }
            //trumfyradit na prvnim miste
            if (trump.HasValue &&
                trump.Value != 0)
            {
                foreach(var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                {
                    var temp = result[(int)h];
                    result[(int)h] = result[8 * (int)trump.Value + (int)h];
                    result[8 * (int)trump.Value + (int)h] = temp;
                }
            }
            return result.ToArray();
        }

        private static double[] ConvertOutput(int mask)
        {
            var result = new double[5];

            for(var i = 0; i < 5; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    result[i] = 1;
                }
            }

            return result;
        }

        private static bool PlayGame(Hra? desiredGameType = null, bool strict = false)
        {
            g.GameTypeChosen += GameTypeChosen;
            g.BidMade += BidMade;
            g.CardPlayed += CardPlayed;
            g.RoundFinished += RoundFinished;
            g.GameFinished += GameFinished;
            if (desiredGameType.HasValue)
            {
                if (strict)
                {
                    foreach (var player in g.players)
                    {
                        player.TestGameType = 0;
                    }
                }
                g.GameStartingPlayer.TestGameType = desiredGameType;
            }
            g.PlayGame();

            System.Console.WriteLine(g.Results.ToString());

            if (!string.IsNullOrEmpty(resultFilename))
            {
                g.SaveGame(resultFilename, true);
            }
            else
            {
                g.SaveGame(System.IO.Path.Combine(programFolder, "_konec.hra"), true);
            }
            return (!strict &&
                    (g.GameType & desiredGameType.Value) != 0 &&
                    g.Results.MoneyWon[g.GameStartingPlayerIndex] > 0 &&
                    g.Results.MoneyWon[(g.GameStartingPlayerIndex + 1) % Game.NumPlayers] < 0 &&
                    g.Results.MoneyWon[(g.GameStartingPlayerIndex + 2) % Game.NumPlayers] < 0) ||
                   (strict &&
                    g.Results.MoneyWon[0] > 0 &&
                    g.Results.MoneyWon[1] < 0 &&
                    g.Results.MoneyWon[2] < 0);
        }

        static void Main(string[] args)
        {
            Parser.RunConsole<Program>(args);
        }

        #region Event handlers

        private static void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            var debugInfo = g.players[e.GameStartingPlayerIndex].DebugInfo != null
                                ? g.players[e.GameStartingPlayerIndex].DebugInfo.AllChoices
                                : new RuleDebugInfo[0];
            foreach (var choice in debugInfo)
            {
                System.Console.WriteLine("{0}: {1}x", choice.Rule, choice.RuleCount);
            }
            System.Console.WriteLine("{0}: {1} {2}\nTalon: {3} {4}\n", g.GameStartingPlayer.Name, e.GameType, e.TrumpCard, g.talon[0], g.talon[1]);
            for (var i = 0; i < Game.NumPlayers; i++)
            {
                var player = g.players[(g.GameStartingPlayerIndex + i) % Game.NumPlayers];
                System.Console.WriteLine("{0}: {1}", player.Name, new Hand(player.Hand));
            }
        }

        private static void BidMade(object sender, BidEventArgs e)
        {
            System.Console.WriteLine("{0}: {1}", e.Player.Name, e.BidMade == 0 ? "dobry" : string.Format("vejs na {0}", e.BidMade));
        }

        private static void CardPlayed(object sender, Round r)
        {
            if (r.c3 != null)
            {
                var debugInfo = g.players[r.player3.PlayerIndex].DebugInfo != null 
                                    ? string.Format(": {0} {1}x", g.players[r.player3.PlayerIndex].DebugInfo.Rule,
                                                g.players[r.player3.PlayerIndex].DebugInfo.RuleCount)
                                    : string.Empty;

                System.Console.WriteLine("{0}: {1}{2}{3} {4}", r.player3.Name, 
                                                           r.c3, 
                                                           r.c3.Suit == g.trump ? "*" : string.Empty, 
                                                           r.hlas3 ? " (hlas)" : string.Empty,
                                                           debugInfo);
                foreach (var player in g.players)
                {
                    System.Console.WriteLine("{0}: {1}", player.Name, new Hand(player.Hand));
                }
                System.Console.WriteLine();
            }
            else if (r.c2 != null)
            {
                var debugInfo = g.players[r.player2.PlayerIndex].DebugInfo != null 
                                    ? string.Format(": {0} {1}x", g.players[r.player2.PlayerIndex].DebugInfo.Rule,
                                                g.players[r.player2.PlayerIndex].DebugInfo.RuleCount)
                                    : string.Empty;

                System.Console.WriteLine("{0}: {1}{2}{3} {4}", r.player2.Name,
                                                           r.c2,
                                                           r.c2.Suit == g.trump ? "*" : string.Empty,
                                                           r.hlas2 ? " (hlas)" : string.Empty,
                                                           debugInfo);
            }
            else
            {
                var debugInfo = g.players[r.player1.PlayerIndex].DebugInfo != null 
                                    ? string.Format(": {0} {1}x", g.players[r.player1.PlayerIndex].DebugInfo.Rule,
                                                g.players[r.player1.PlayerIndex].DebugInfo.RuleCount)
                                    : string.Empty;

                System.Console.WriteLine("Round {0}", r.number);
                System.Console.WriteLine("{0}: {1}{2}{3} {4}", r.player1.Name,
                                                           r.c1,
                                                           r.c1.Suit == g.trump ? "*" : string.Empty,
                                                           r.hlas1 ? " (hlas)" : string.Empty,
                                                           debugInfo);
            }
        }

        private static void RoundFinished(object sender, Round r)
        {
            System.Console.WriteLine("Round winner: {0} ({1} points won)\n", r.roundWinner.Name, r.PointsWon);
        }

        private static void GameFinished(object sender, MoneyCalculatorBase e)
        {
            System.Console.WriteLine("Výkon simulace: {0} her/s, {1} kol/s",
                (int)g.players.Where(i => i is AiPlayer).Average(i => (i as AiPlayer).Settings.SimulationsPerGameTypePerSecond),
                (int)g.players.Where(i => i is AiPlayer).Average(i => (i as AiPlayer).Settings.SimulationsPerRoundPerSecond));
        }
        #endregion
    }
}
