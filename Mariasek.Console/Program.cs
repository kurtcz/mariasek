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
        public static void NewGame([DefaultValue(false)] bool skipGame)
        {
            playerSettingsReader = new Mariasek.WinSettings.PlayerSettingsReader();
            g = new Game();
            g.RegisterPlayers(playerSettingsReader);

            g.NewGame(gameStartingPlayerIndex: 0); //saves the new game as _temp.hra
            if (!skipGame)
            { 
                PlayGame(); 
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

        private static void PlayGame()
        {
            g.GameTypeChosen += GameTypeChosen;
            g.CardPlayed += CardPlayed;
            g.RoundFinished += RoundFinished;
            g.PlayGame();

            if (!g.trump.HasValue)
            {
                System.Console.WriteLine("{0} has {1} the {2}.",
                    g.GameStartingPlayer.Name,
                    (((g.GameType & Hra.Betl) != 0 && g.Results.BetlWon) || g.Results.DurchWon) ? "won" : "lost",
                    g.GameType);
            }
            else
            {
                System.Console.WriteLine("{0} has {1} the game ({2} {3}).",
                    g.GameStartingPlayer.Name,
                    g.Results.GameWon ? "won" : "lost",
                    g.GameType, g.trump.Value);
                System.Console.WriteLine("Final score: {0}:{1}", g.Results.PointsWon, g.Results.PointsLost);
            }
            if ((g.GameType & Hra.Sedma) != 0)
            {
                System.Console.WriteLine("{0} has {1} the seven.", g.GameStartingPlayer.Name, g.Results.SevenWon ? "won" : "lost");
            }
            for (var i = 0; i < Game.NumPlayers; i++)
            {
                System.Console.WriteLine("{0}: {1}", g.players[i].Name,
                                                     g.Results.MoneyWon[i].ToString("C", CultureInfo.CreateSpecificCulture("cs-CZ")));
            }            

            if (!string.IsNullOrEmpty(resultFilename))
            {
                g.SaveGame(resultFilename);
            }
            else
            {
                g.SaveGame(System.IO.Path.Combine(programFolder, "_konec.hra"));            
            }
        }

        static void Main(string[] args)
        {
            Parser.Run<Program>(args);
        }

        #region Event handlers

        private static void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            for (var i = 0; i < Game.NumPlayers; i++)
            {
                var player = g.players[(g.GameStartingPlayerIndex + i) % Game.NumPlayers];
                System.Console.WriteLine("{0}: {1}", player.Name, new Hand(player.Hand));
            }
            System.Console.WriteLine("{0}: {1} {2}\nTalon: {3} {4}\n", g.GameStartingPlayer.Name, e.GameType, e.TrumpCard, g.talon[0], g.talon[1]);
        }

        private static void CardPlayed(object sender, Round r)
        {
            if (r.c3 != null)
            {
                var debugInfo = string.Format(": {0} {1}x", g.players[r.player3.PlayerIndex].DebugInfo.Rule,
                                                g.players[r.player3.PlayerIndex].DebugInfo.RuleCount);

                System.Console.WriteLine("{0}: {1}{2}{3} {4}", r.player3.Name, 
                                                           r.c3, 
                                                           r.c3.Suit == g.trump ? "*" : string.Empty, 
                                                           r.hlas3 ? " (hlas)" : string.Empty,
                                                           debugInfo ?? string.Empty);
                foreach (var player in g.players)
                {
                    System.Console.WriteLine("{0}: {1}", player.Name, new Hand(player.Hand));
                }
                System.Console.WriteLine();
            }
            else if (r.c2 != null)
            {
                var debugInfo = string.Format(": {0} {1}x", g.players[r.player2.PlayerIndex].DebugInfo.Rule,
                                                g.players[r.player2.PlayerIndex].DebugInfo.RuleCount);

                System.Console.WriteLine("{0}: {1}{2}{3} {4}", r.player2.Name,
                                                           r.c2,
                                                           r.c2.Suit == g.trump ? "*" : string.Empty,
                                                           r.hlas2 ? " (hlas)" : string.Empty,
                                                           debugInfo ?? string.Empty);
            }
            else
            {
                var debugInfo = string.Format(": {0} {1}x", g.players[r.player1.PlayerIndex].DebugInfo.Rule,
                                                g.players[r.player1.PlayerIndex].DebugInfo.RuleCount);

                System.Console.WriteLine("Round {0}", r.number);
                System.Console.WriteLine("{0}: {1}{2}{3} {4}", r.player1.Name,
                                                           r.c1,
                                                           r.c1.Suit == g.trump ? "*" : string.Empty,
                                                           r.hlas1 ? " (hlas)" : string.Empty,
                                                           debugInfo ?? string.Empty);
            }
        }

        private static void RoundFinished(object sender, Round r)
        {
            System.Console.WriteLine("Round winner: {0} ({1} points won)\n", r.roundWinner.Name, r.PointsWon);
        }

        #endregion
    }
}
