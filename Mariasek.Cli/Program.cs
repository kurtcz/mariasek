using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using CLAP;
using Mariasek.Engine.New;
using Mariasek.SharedClient;

namespace Mariasek.Cli
{
    class Program
    {
        private static string _path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string _settingsFilePath = Path.Combine(_path, "Mariasek.settings");

        private static Mariasek.Engine.New.Configuration.ParameterConfigurationElementCollection _aiConfig;
        private static string programFolder = _path;
        private static string resultFilename;
        private static Game g;

        public static GameSettings Settings { get; private set; }

        #region CLAP verbs

        [Help(Aliases = "help,h,?")]
        public static void ShowHelp(string help)
        {
            System.Console.WriteLine("{0} {1}\nUsage:\n{2}",
                Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName),
                System.Diagnostics.Process.GetCurrentProcess().MainModule.FileVersionInfo.ProductVersion,
                help);
        }

        [Global(Aliases = "config", Description = "Use a custom config file")]
        public static void SetConfigFile(string filename)
        {
            if (!File.Exists(filename))
            {
                System.Console.WriteLine("Error loading custom config file {0}", filename);
                System.Environment.Exit(-1);
            }
            AppDomain.CurrentDomain.SetData("APP_CONFIG_FILE", filename);
            System.Console.WriteLine("Using config file {0}", filename);
        }

        [Global(Aliases = "result", Description = "Save finished game to file")]
        public static void SetResultFilename(string filename)
        {
            resultFilename = filename;
        }

        [Verb(Aliases = "new", Description = "Starts a new random game", IsDefault = true)]
        public static void NewGame([DefaultValue(false)] bool skipGame, [DefaultValue(0)] Hra gameType)
        {
            bool finished = false;
            int iterations;
            Deck deck = null;

            PopulateAiConfig();

            for (iterations = 0; !finished; iterations++)
            {
                g = new Game()
                {
                    SkipBidding = false,
                    BaseBet = Settings.BaseBet,
                    GetFileStream = GetFileStream,
                    GetVersion = () => Assembly.GetExecutingAssembly().GetName().Version
                };
                g.RegisterPlayers(
                    new AiPlayer(g, _aiConfig) { Name = Settings.PlayerNames[0] },
                    new AiPlayer(g, _aiConfig) { Name = Settings.PlayerNames[1] },
                    new AiPlayer(g, _aiConfig) { Name = Settings.PlayerNames[2] }
                );

                g.NewGame(gameStartingPlayerIndex: 0, deck: deck); //saves the new game as _temp.hra
                for (var i = 0; i < Game.NumPlayers; i++)
                {
                    var player = g.players[(g.GameStartingPlayerIndex + i) % Game.NumPlayers];
                    System.Console.WriteLine("{0}: {1}", player.Name, new Hand(player.Hand));
                }
                if (!skipGame)
                {
                    finished = PlayGame(gameType == 0 ? (Hra?)null : gameType);
                    deck = g.GetDeckFromLastGame();
                    deck.Shuffle();
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

        [Verb(Aliases = "load", Description = "Loads a game from a file")]
        public static void LoadGame(string filename, [DefaultValue(false)] bool classify, string outputDir = null)
        {
            if (!classify)
            {
                PopulateAiConfig();

                g = new Game()
                {
                    SkipBidding = false,
                    BaseBet = Settings.BaseBet,
                    GetFileStream = GetFileStream,
                    GetVersion = () => Assembly.GetExecutingAssembly().GetName().Version
                };
                g.RegisterPlayers(
                    new AiPlayer(g, _aiConfig) { Name = Settings.PlayerNames[0] },
                    new AiPlayer(g, _aiConfig) { Name = Settings.PlayerNames[1] },
                    new AiPlayer(g, _aiConfig) { Name = Settings.PlayerNames[2] }
                );

                using (var fs = new FileStream(filename, FileMode.Open))
                {
                    g.LoadGame(fs);
                    for (var i = 0; i < Game.NumPlayers; i++)
                    {
                        var player = g.players[(g.GameStartingPlayerIndex + i) % Game.NumPlayers];
                        System.Console.WriteLine("{0}: {1}", player.Name, new Hand(player.Hand));
                    }
                    PlayGame();
                }
            }
            else
            {
                var gameTypes = new Dictionary<Hra, Tuple<bool, float>>();

                PopulateAiConfig();
                foreach (var gt in Enum.GetValues(typeof(Hra)).Cast<Hra>().Where(gt => gt != Hra.SedmaProti && gt != Hra.KiloProti))
                {
                    
                    g = new Game()
                    {
                        SkipBidding = false,
                        BaseBet = Settings.BaseBet,
                        GetFileStream = GetFileStream,
                        GetVersion = () => Assembly.GetExecutingAssembly().GetName().Version
                    };
                    g.RegisterPlayers(
                        new AiPlayer(g, _aiConfig) { Name = Settings.PlayerNames[0] },
                        new AiPlayer(g, _aiConfig) { Name = Settings.PlayerNames[1] },
                        new AiPlayer(g, _aiConfig) { Name = Settings.PlayerNames[2] }
                    );

                    using (var fs = new FileStream(filename, FileMode.Open))
                    {
                        g.LoadGame(fs);
                        for (var i = 0; i < Game.NumPlayers; i++)
                        {
                            var player = g.players[(g.GameStartingPlayerIndex + i) % Game.NumPlayers];
                            System.Console.WriteLine("{0}: {1}", player.Name, new Hand(player.Hand));
                        }
                        var gameWon = PlayGame(gt, strict: true);

                        gameTypes.Add(gt, new Tuple<bool, float>(gameWon, g.Results.MoneyWon[0]));
                    }
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

        //[Verb(Aliases = "learn", Description = "Runs a neural network trainer on the test set")]
        //public static void Learn(string inputDir, double learningRate = 0.1, double momentum = 0, double targetError = 30)
        //{
        //    var files = Directory.GetFiles(inputDir);
        //    var data = new List<Tuple<double[], double[]>>();

        //    System.Console.WriteLine("Populating data ...");
        //    foreach (var file in files)
        //    {
        //        playerSettingsReader = new Mariasek.WinSettings.PlayerSettingsReader();
        //        g = new Game();
        //        g.RegisterPlayers(playerSettingsReader);

        //        g.LoadGame(file);
        //        var input = ConvertInput(g.GameStartingPlayer.Hand, g.trump);
        //        var filename = Path.GetFileNameWithoutExtension(file);
        //        var tokens = filename.Split('.');

        //        if (!tokens.Any())
        //        {
        //            return;
        //        }

        //        var code = tokens[tokens.Length - 1];

        //        if (code.Length < 3)
        //        {
        //            return;
        //        }
        //        int mask;

        //        if (!int.TryParse(code.Substring(3), out mask))
        //        {
        //            return;
        //        }
        //        var output = ConvertOutput(mask);
        //        data.Add(new Tuple<double[], double[]>(input, output));
        //    }
        //    double[][] inputs = data.Select(i => i.Item1).ToArray();
        //    double[][] outputs = data.Select(i => i.Item2).ToArray();
        //    ActivationNetwork network = File.Exists(nnFilename)
        //                                ? (ActivationNetwork)ActivationNetwork.Load(nnFilename)
        //                                : new ActivationNetwork(
        //                                    new SigmoidFunction(2),
        //                                    32,
        //                                    19,
        //                                    5);
        //    // create teacher
        //    BackPropagationLearning teacher = new BackPropagationLearning(network)
        //    {
        //        LearningRate = learningRate,
        //        Momentum = momentum
        //    };

        //    System.Console.WriteLine("Training in progress ...");
        //    // loop
        //    bool needToStop = false;
        //    while (!needToStop)
        //    {
        //        // run epoch of learning procedure
        //        double error = teacher.RunEpoch(inputs, outputs);
        //        System.Console.Write("\r{0}", error);
        //        needToStop = error < targetError;
        //    }
        //    System.Console.WriteLine("\nDone training, saving neural network to {0}", nnFilename);
        //    network.Save(nnFilename);
        //}
        #endregion

        private static void LoadGameSettings()
        {
            var xml = new XmlSerializer(typeof(GameSettings));
            try
            {
                using (var fs = File.Open(_settingsFilePath, FileMode.Open))
                {
                    Settings = (GameSettings)xml.Deserialize(fs);
                    if (!Settings.Default.HasValue ||
                        Settings.Default.Value ||
                        Settings.Thresholds == null ||
                        !Settings.Thresholds.Any() ||
                        Settings.Thresholds.Count() != Enum.GetValues(typeof(Hra)).Cast<Hra>().Count())
                    {
                        Settings.ResetThresholds();
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Cannot load settings\n{0}", e.Message));
                Settings = new GameSettings();
            }
            //          _performance.Text = string.Format("Výkon simulace: {0} her/s",
            //              Settings.GameTypeSimulationsPerSecond > 0 ? Settings.GameTypeSimulationsPerSecond.ToString() : "?");
        }
        private static void PopulateAiConfig()
        {
            //TODO: Nastavit prahy podle uspesnosti v predchozich zapasech
            _aiConfig = new Mariasek.Engine.New.Configuration.ParameterConfigurationElementCollection();

            LoadGameSettings();
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
                Value = "500"
            });
            _aiConfig.Add("SimulationsPerGameTypePerSecond", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "SimulationsPerGameTypePerSecond",
                Value = Settings.GameTypeSimulationsPerSecond.ToString()
            });
            _aiConfig.Add("MaxSimulationTimeMs", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "MaxSimulationTimeMs",
                Value = Settings.ThinkingTimeMs.ToString()
            });
            _aiConfig.Add("SimulationsPerRound", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "SimulationsPerRound",
                Value = "500"
            });
            _aiConfig.Add("SimulationsPerRoundPerSecond", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "SimulationsPerRoundPerSecond",
                Value = Settings.RoundSimulationsPerSecond.ToString()
            });
            _aiConfig.Add("RuleThreshold", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "RuleThreshold",
                Value = "95"
            });
            _aiConfig.Add("RuleThreshold.Kilo", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "RuleThreshold.Kilo",
                Value = "99"
            });
            _aiConfig.Add("GameThreshold", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "GameThreshold",
                Value = "75|80|85|90|95"
            });
            _aiConfig.Add("MaxDoubleCount", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "MaxDoubleCount",
                Value = "3"
            });
            foreach (var thresholdSettings in Settings.Thresholds)
            {
                _aiConfig.Add(string.Format("GameThreshold.{0}", thresholdSettings.GameType.ToString()),
                              new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                              {
                                  Name = string.Format("GameThreshold.{0}", thresholdSettings.GameType.ToString()),
                                  Value = thresholdSettings.Thresholds
                              });
                _aiConfig.Add(string.Format("MaxDoubleCount.{0}", thresholdSettings.GameType.ToString()),
                              new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                              {
                                  Name = string.Format("MaxDoubleCount.{0}", thresholdSettings.GameType.ToString()),
                                  Value = thresholdSettings.MaxBidCount.ToString()
                              });
                _aiConfig.Add(string.Format("CanPlay.{0}", thresholdSettings.GameType.ToString()),
                              new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
                              {
                                  Name = string.Format("CanPlay.{0}", thresholdSettings.GameType.ToString()),
                                  Value = thresholdSettings.Use.ToString()
                              });
            }
            _aiConfig.Add("SigmaMultiplier", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "SigmaMultiplier",
                Value = "0"
            });
            _aiConfig.Add("AiMayGiveUp", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "AiMayGiveUp",
                Value = "true"
            });
            _aiConfig.Add("BaseBet", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "BaseBet",
                Value = "1"
            });
            _aiConfig.Add("GameFlavourSelectionStrategy", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "GameFlavourSelectionStrategy",
                Value = "Fast"
            });
            _aiConfig.Add("RiskFactor", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "RiskFactor",
                Value = Settings.RiskFactor.ToString(CultureInfo.InvariantCulture)
            });
            _aiConfig.Add("SolitaryXThreshold", new Mariasek.Engine.New.Configuration.ParameterConfigurationElement
            {
                Name = "SolitaryXThreshold",
                Value = Settings.SolitaryXThreshold.ToString(CultureInfo.InvariantCulture)
            });
        }

        private static Stream GetFileStream(string filename)
        {
            var path = Path.Combine(_path, filename);

            CreateDirectoryForFilePath(path);

            return new FileStream(path, FileMode.Create);
        }

        public static void CreateDirectoryForFilePath(string path)
        {
            var dir = Path.GetDirectoryName(path);

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private static bool PlayGame(Hra? desiredGameType = null, bool strict = false)
        {
            g.GameFlavourChosen += GameFlavourChosen;
            g.GameTypeChosen += GameTypeChosen;
            g.BidMade += BidMade;
            g.RoundFinished += RoundFinished;
            g.GameFinished += GameFinished;
            g.PlayGame();

            var origcolor = System.Console.ForegroundColor;
            System.Console.ForegroundColor = g.Results.GamePlayed
                                                ? g.Results.MoneyWon[0] >= 0 ? ConsoleColor.Green : ConsoleColor.Red
                                                : ConsoleColor.Yellow;
            System.Console.WriteLine(g.Results.ToString());
            System.Console.ForegroundColor = origcolor;

            if (!string.IsNullOrEmpty(resultFilename))
            {
                using (var fs = new FileStream(resultFilename, FileMode.Create))
                {
                    g.SaveGame(fs, true);
                }
            }
            else
            {
                using (var fs = new FileStream(Path.Combine(programFolder, "_konec.hra"), FileMode.Create))
                {
                    g.SaveGame(fs, true);
                }
            }
            return desiredGameType == null || (g.GameType & desiredGameType.Value) != 0;
        }

        private static string GetTrumpLabelForPlayer(int playerIndex)
        {
            var text = playerIndex == g.GameStartingPlayerIndex
                                   ? string.Format("{0}: {1}",
                                       g.players[playerIndex].Name,
                                       g.GameType.ToDescription().Trim() +
                                           (string.IsNullOrEmpty(g.players[playerIndex].BidMade)
                                            ? string.Empty
                                            : string.Format(" {0}", g.players[playerIndex].BidMade.TrimEnd())))
                                   : string.IsNullOrEmpty(g.players[playerIndex].BidMade)
                                       ? g.players[playerIndex].Name
                                       : string.Format("{0}: {1}", g.players[playerIndex].Name,
                                                                   g.players[playerIndex].BidMade.Trim());

            return text;
        }

        static void Main(string[] args)
        {
            Parser.RunConsole<Program>(args);
        }

        #region Event handlers

        private static void GameFlavourChosen(object sender, GameFlavourChosenEventArgs e)
        {
            var debugInfo = g.players[e.Player.PlayerIndex].DebugInfo != null
                             ? g.players[e.Player.PlayerIndex].DebugInfo.AllChoices
                                : new RuleDebugInfo[0];
            var origcolor = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.WriteLine("{0}: {1} {2}/{3} ({4:F0}%)",
                                     e.Player.Name,
                                     e.Flavour.Description(),
                                     g.players[e.Player.PlayerIndex].DebugInfo.RuleCount,
                                     g.players[e.Player.PlayerIndex].DebugInfo.TotalRuleCount,
                                     g.players[e.Player.PlayerIndex].DebugInfo.TotalRuleCount > 0 
                                        ? 100 * (float)g.players[e.Player.PlayerIndex].DebugInfo.RuleCount / (float)g.players[e.Player.PlayerIndex].DebugInfo.TotalRuleCount
                                        : 0);
            System.Console.ForegroundColor = origcolor;
            foreach (var choice in debugInfo)
            {
                System.Console.WriteLine("{0}: {1}/{2} ({3:F0}%)",
                                         choice.Rule, choice.RuleCount, choice.TotalRuleCount,
                                         choice.TotalRuleCount > 0 ? 100 * (float)choice.RuleCount / (float)choice.TotalRuleCount : 0);
            }
        }

        private static void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            var debugInfo = g.players[e.GameStartingPlayerIndex].DebugInfo != null
                                ? g.players[e.GameStartingPlayerIndex].DebugInfo.AllChoices
                                : new RuleDebugInfo[0];
            var origcolor = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.WriteLine("{0}: {1} {2}/{3} ({4:F0}%)", 
                                     g.players[e.GameStartingPlayerIndex].Name,
                                     GetTrumpLabelForPlayer(e.GameStartingPlayerIndex),
                                     g.players[e.GameStartingPlayerIndex].DebugInfo.RuleCount,
                                     g.players[e.GameStartingPlayerIndex].DebugInfo.TotalRuleCount,
                                     g.players[e.GameStartingPlayerIndex].DebugInfo.TotalRuleCount > 0
                                        ? 100 * (float) g.players[e.GameStartingPlayerIndex].DebugInfo.RuleCount / (float)g.players[e.GameStartingPlayerIndex].DebugInfo.TotalRuleCount
                                        : 0);
            System.Console.ForegroundColor = origcolor;
            foreach (var choice in debugInfo)
            {
                System.Console.WriteLine("{0}: {1}/{2} ({3:F0}%)", 
                                         choice.Rule, choice.RuleCount, choice.TotalRuleCount, 
                                         choice.TotalRuleCount > 0 ? 100 * (float)choice.RuleCount / (float)choice.TotalRuleCount : 0);
            }
            System.Console.WriteLine("{0}: {1} (trumf: {2})\nTalon: {3} {4}\n", g.GameStartingPlayer.Name, e.GameType, e.TrumpCard, g.talon[0], g.talon[1]);
        }

        private static void BidMade(object sender, BidEventArgs e)
        {
            var origcolor = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.WriteLine("{0}: {1}", e.Player.Name, e.Description);
            System.Console.ForegroundColor = origcolor;

            var debugInfo = g.players[e.Player.PlayerIndex].DebugInfo != null
                                ? g.players[e.Player.PlayerIndex].DebugInfo.AllChoices
                                : new RuleDebugInfo[0];

            foreach (var choice in debugInfo)
            {
                System.Console.WriteLine("{0}: {1}/{2} ({3:F0}%)",
                                         choice.Rule, choice.RuleCount, choice.TotalRuleCount,
                                         choice.TotalRuleCount > 0 ? 100 * (float)choice.RuleCount / (float)choice.TotalRuleCount : 0);
            }
        }

        private static void RoundFinished(object sender, Round r)
        {
            if (r.c3 != null)
            {
                var debugInfo = g.players[r.player1.PlayerIndex].DebugInfo != null
                                    ? string.Format(": {0} {1}x", g.players[r.player1.PlayerIndex].DebugInfo.Rule,
                                                g.players[r.player1.PlayerIndex].DebugInfo.RuleCount)
                                    : string.Empty;

                System.Console.WriteLine("Round {0}", r.number);
                var origcolor = System.Console.ForegroundColor;
                System.Console.ForegroundColor = r.roundWinner.PlayerIndex == r.player1.PlayerIndex ? ConsoleColor.Yellow : origcolor;
                System.Console.WriteLine("{0}: {1}{2}{3} {4}", r.player1.Name,
                                                           r.c1,
                                                           r.c1.Suit == g.trump ? "*" : string.Empty,
                                                           r.hlas1 ? " (hlas)" : string.Empty,
                                                           debugInfo);
                System.Console.ForegroundColor = origcolor;

                debugInfo = g.players[r.player2.PlayerIndex].DebugInfo != null
                                    ? string.Format(": {0} {1}x", g.players[r.player2.PlayerIndex].DebugInfo.Rule,
                                                g.players[r.player2.PlayerIndex].DebugInfo.RuleCount)
                                    : string.Empty;
                
                System.Console.ForegroundColor = r.roundWinner.PlayerIndex == r.player2.PlayerIndex ? ConsoleColor.Yellow : origcolor;
                System.Console.WriteLine("{0}: {1}{2}{3} {4}", r.player2.Name,
                                                           r.c2,
                                                           r.c2.Suit == g.trump ? "*" : string.Empty,
                                                           r.hlas2 ? " (hlas)" : string.Empty,
                                                           debugInfo);
                System.Console.ForegroundColor = origcolor;

                debugInfo = g.players[r.player3.PlayerIndex].DebugInfo != null
                                    ? string.Format(": {0} {1}x", g.players[r.player3.PlayerIndex].DebugInfo.Rule,
                                                g.players[r.player3.PlayerIndex].DebugInfo.RuleCount)
                                    : string.Empty;

                System.Console.ForegroundColor = r.roundWinner.PlayerIndex == r.player3.PlayerIndex ? ConsoleColor.Yellow : origcolor;
                System.Console.WriteLine("{0}: {1}{2}{3} {4}", r.player3.Name,
                                                           r.c3,
                                                           r.c3.Suit == g.trump ? "*" : string.Empty,
                                                           r.hlas3 ? " (hlas)" : string.Empty,
                                                           debugInfo);
                System.Console.ForegroundColor = origcolor;
                System.Console.WriteLine("Round winner: {0} ({1} points won)\n", r.roundWinner.Name, r.PointsWon);
                foreach (var player in g.players)
                {
                    System.Console.WriteLine("{0}: {1}", player.Name, new Hand(player.Hand));
                }
                System.Console.WriteLine();
            }
        }

        private static void GameFinished(object sender, MoneyCalculatorBase e)
        {
            System.Console.WriteLine("Výkon simulace: {0} her/s",
                (int)g.players.Where(i => i is AiPlayer).Average(i => (i as AiPlayer).Settings.SimulationsPerGameTypePerSecond));
            System.Console.WriteLine("Jistota volby: {0:F0}%", g.GameTypeConfidence * 100);
        }
        #endregion    
    }
}
