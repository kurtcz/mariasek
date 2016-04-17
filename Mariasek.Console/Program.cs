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

namespace Mariasek.Console
{
    class Program
    {
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

            for (iterations = 0; !finished; iterations++)
            {
                playerSettingsReader = new Mariasek.WinSettings.PlayerSettingsReader();
                g = new Game();
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
        public static void LoadGame(string filename)
        {
            playerSettingsReader = new Mariasek.WinSettings.PlayerSettingsReader();
            g = new Game();
            g.RegisterPlayers(playerSettingsReader);

            g.LoadGame(filename);
            PlayGame();
        }

        #endregion

        private static bool PlayGame(Hra? desiredGameType = null)
        {
            g.GameTypeChosen += GameTypeChosen;
            g.BidMade += BidMade;
            g.CardPlayed += CardPlayed;
            g.RoundFinished += RoundFinished;
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

            return !desiredGameType.HasValue || (g.GameType & desiredGameType.Value) != 0;
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

        #endregion
    }
}
