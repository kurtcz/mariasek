using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using log4net;
using Logger;

namespace Mariasek.Engine
{
    public enum CardSelectionStrategy
    {
        [Description("Nejčastější karta")]
        MaxCount = 0,
        [Description("Min. skóre soupeřů")]
        MinScore = 1,
        [Description("Průměrné skóre")]
        AverageScore = 2
    }

    public class AiPlayerSettings
    {
        public bool Cheat { get; set; }
        public CardSelectionStrategy CardSelectionStrategy { get; set; }
        public int SimulationsPerRound { get; set; }
        public int RoundsToCompute { get; set; }
        public float RuleThreshold { get; set; }

        public override string ToString()
        {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                      .Select(i => new
                                                    {
                                                        Name = i.Name,
                                                        Value = i.GetValue(this, null)
                                                    });

            var builder = new StringBuilder();

            foreach (var property in properties)
            {
                builder
                    .Append(property.Name)
                    .Append(": ")
                    .Append(property.Value)
                    .AppendLine();
            }

            return builder.ToString();
        }
    }

    public class AiPlayer : AbstractPlayer, IPlayerStats
    {
        public Probability Probabilities { get; set; }

        public AiPlayerSettings Settings { get; set; }
        public AiPlayer(string name, Game g) : base(name, g)
        {
        }

        public static List<Card> ChooseTalon(Hand hand, Barva trump)
        {
            //TODO: promyslet poradne jak na to (v kombinaci s hlasy apod.)
            var talon = new List<Card>();
            //nejdriv zkus vzit karty v barve kde krom esa nemam nic jineho (neber krale ani svrska)
            var b = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        .Where(barva => barva != trump &&
                                        hand.Count(i => i.Suit == barva &&
                                                        i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka) <= 2 &&
                                        !hand.HasX(barva) &&
                                        !(hand.HasK(barva) && hand.HasQ(barva)));

            talon.AddRange(hand.Where(i => b.Contains(i.Suit) &&
                                      i.Value != Hodnota.Eso &&
                                      i.Value != Hodnota.Desitka)
                               .Take(2)
                               .ToList());
            if (talon.Count < 2)
            {
                b = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        .Where(barva => barva != trump &&
                                        (talon.Count == 0 || barva != talon.First().Suit) &&
                                        hand.Count(i => i.Suit == barva &&
                                                        i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka) <= 2 &&
                                        !hand.HasX(barva));

                talon.AddRange(hand.Where(i => b.Contains(i.Suit) &&
                                          i.Value != Hodnota.Eso &&
                                          i.Value != Hodnota.Desitka)
                                   .Take(2 - talon.Count)
                                   .ToList());

                if (talon.Count < 2)
                {
                    b = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(barva => barva != trump &&
                                            (talon.Count == 0 || barva != talon.First().Suit));
                    talon.AddRange(hand.Where(i => b.Contains(i.Suit) &&
                                              i.Value != Hodnota.Eso &&
                                              i.Value != Hodnota.Desitka)
                                       .OrderBy(i => i.Value)
                                       .Take(2 - talon.Count)
                                       .ToList());
                }
            }

            return talon;
        }

        public override void ChooseTalon()
        {
            var talon = ChooseTalon(new Hand(Hand), _g.trump);

            _log.DebugFormat("Talon: {0}, {1}", talon[0], talon[1]);

            OnTalonChosen(new TalonEventArgs(talon));
        }

        public override void ChooseTrump()
        {
            var scores =  Enum.GetValues(typeof(Barva)).Cast<Barva>().Select(barva => new
                                                                                  {
                                                                                      Suit = barva,
                                                                                      Score = GetSuitScoreForTrumpChoice(barva)
                                                                                  });
                       
            //vezmi barvu s nejvetsim skore, pokud je skore shodne tak vezmi nejdelsi barvu
            var trump = scores.OrderByDescending(i => i.Score)
                              .Select(i => i.Suit)
                              .First();

            //vyber jednu z karet v barve (nejdriv zkus neukazovat zadne dulezite karty, pokud to nejde vezmi libovolnou kartu v barve)
            var card = Hand.FirstOrDefault(i => i.Suit == trump && i.Value > Hodnota.Sedma && i.Value < Hodnota.Svrsek) ??
                       Hand.OrderBy(i => i.Value).FirstOrDefault(i => i.Suit == trump);

            _log.DebugFormat("Trump chosen: {0}", card);

            OnTrumpChosen(new CardEventArgs(card));
        }

        private int GetSuitScoreForTrumpChoice(Barva b)
        {
            var score = 0;
            var count = Hand.Count(i => i.Suit == b);
            
            if (count > 1)
            {
                if (Hand.HasK(b))
                    score += 20;
                if (Hand.HasQ(b))
                    score += 20;
                if (Hand.HasA(b))
                    score += 10;
                if (Hand.HasX(b))
                    score += 10;

                score += count;
            }
            _log.DebugFormat("Trump score for {0}: {1}", b, score);

            return score;
        }

        public override void ChooseGameType()
        {
            //nasimuluj hry pro kazdeho vaznejsiho kandidata na trumfy (skore >= n)
            var cardScores = new Dictionary<Card, List<GameComputationResult>>();

//            if (Probabilities == null)
            {
                Probabilities = new Probability(MyIndex, GameStarterIndex, new Hand(Hand), _g.trump, _g.talon);
            }

            for (int i = 0; i < Settings.SimulationsPerRound; i++)
            {
                _hands = Probabilities.GenerateHands(1, MyIndex);
                var gameComputationResult = ComputeGame(null, null, _g.trump, 10, 1);

                if (cardScores.ContainsKey(gameComputationResult.CardToPlay))
                {
                    cardScores[gameComputationResult.CardToPlay].Add(gameComputationResult);
                }
                else
                {
                    cardScores.Add(gameComputationResult.CardToPlay, new List<GameComputationResult> { gameComputationResult });
                }
            }

            //vyber vhodnou hru podle vysledku simulace
            var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                                                                ? (MyIndex + 2) % Game.NumPlayers : (MyIndex + 1) % Game.NumPlayers;
            //sgrupuj simulace podle vysledku skore
            var scores = cardScores.SelectMany(i => i.Value)
                                   .GroupBy(i => i.Score.Points[MyIndex])
                                   .Select(g => new
                                   {
                                       Score = g.Key,
                                       Count = g.Count()
                                   });
            var finalSevens = cardScores.SelectMany(i => i.Value)
                                        .Where(i => i.Final7Won.HasValue)
                                        .GroupBy(i => i.Final7Won)
                                        .ToDictionary(k => k.Key.Value, v => v.Count());
            foreach (var score in scores)
            {
                _log.DebugFormat("simulated score: {0} pts {1} times ({2}%)", score.Score, score.Count, score.Count*100 / scores.Sum(i => i.Count));
            }

            //najdi nejmensi skore
            var minScore = scores.OrderBy(i => i.Score).First();
            Hra gameType;
            if (minScore.Score < 100)
            {
                gameType = finalSevens[true] > finalSevens[false] ? Hra.Sedma : Hra.Hra;
            }
            else
            {
                gameType = finalSevens[true] > finalSevens[false] ? Hra.Kilo : Hra.Stosedm;
            }

            _log.DebugFormat("Selected game type: {0}", gameType);

            OnGameTypeChosen(new GameTypeEventArgs(gameType));
            //TODO: poresit sedmu a 107

            //TODO: simulace muzeme vyuzit abychom pak v prvnim tahu uz nemuseli nic znovu pocitat a rovnou mohli hrat
            /*
            var maxScores = cardScores.ToDictionary(k => k.Key,
                v => v.Value.OrderByDescending(i => TeamMateIndex == -1
                    ? i.Score.Points[(MyIndex + 1) % Game.NumPlayers] +
                      i.Score.Points[(MyIndex + 2) % Game.NumPlayers]
                    : i.Score.Points[opponent]).First());

            var kvp =
                maxScores.OrderBy(
                    i => TeamMateIndex == -1
                        ? i.Value.Score.Points[(MyIndex + 1) % Game.NumPlayers] + i.Value.Score.Points[(MyIndex + 2) % Game.NumPlayers]
                        : i.Value.Score.Points[opponent]).First();

            var cardToPlay = kvp.Key;*/
        }

        private GameComputationResult ComputeGame(Card c1, Card c2, Barva? trump = null, int? roundsToCompute = null, int? initialRoundNumber = null)
        {
            var result = new GameComputationResult
                         {
                             Score = new Score(_g.score),
                             Final7Won = null
                         };
            var firstTime = true;
            int player1;
            int player2;
            int player3;
            
            //_hands[MyIndex] = new Hand(Hand);
            if (c1 == null && c2 == null)
            {
                player1 = MyIndex;
                player2 = (MyIndex + 1) % Game.NumPlayers;
                player3 = (MyIndex + 2) % Game.NumPlayers;
            }
            else if (c2 == null)
            {
                player1 = (MyIndex + 2) % Game.NumPlayers;
                player2 = MyIndex;
                player3 = (MyIndex + 1) % Game.NumPlayers;
            }
            else
            {
                player1 = (MyIndex + 1) % Game.NumPlayers;
                player2 = (MyIndex + 2) % Game.NumPlayers;
                player3 = MyIndex;
            }

            var aiStrategy = new AiStrategy(trump.HasValue ? trump.Value : _g.trump, _hands)
                             {
                                 MyIndex = MyIndex,
                                 MyName = Name,
                                 TeamMateIndex =  TeamMateIndex,
                                 RoundNumber = initialRoundNumber.HasValue ? initialRoundNumber.Value : _g.RoundNumber
                             };
            _log.DebugFormat("Round {0}. Starting simulation for {1}", _g.RoundNumber, _g.players[MyIndex].Name);
            if(c1 != null) _log.DebugFormat("First card: {0}", c1);
            if (c2 != null) _log.DebugFormat("Second card: {0}", c2);
            _log.TraceFormat("{0}: {1} cerveny, {2} zeleny, {3} kule, {4} zaludy", _g.players[player2].Name, _hands[player2].Count(i => i.Suit == Barva.Cerveny), _hands[player2].Count(i => i.Suit == Barva.Zeleny), _hands[player2].Count(i => i.Suit == Barva.Kule), _hands[player2].Count(i => i.Suit == Barva.Zaludy));
            _log.TraceFormat("{0}: {1} cerveny, {2} zeleny, {3} kule, {4} zaludy", _g.players[player3].Name, _hands[player3].Count(i => i.Suit == Barva.Cerveny), _hands[player3].Count(i => i.Suit == Barva.Zeleny), _hands[player3].Count(i => i.Suit == Barva.Kule), _hands[player3].Count(i => i.Suit == Barva.Zaludy));
            for (initialRoundNumber = aiStrategy.RoundNumber;
                 aiStrategy.RoundNumber < initialRoundNumber + (roundsToCompute.HasValue 
                                                                ? roundsToCompute.Value 
                                                                : Settings.RoundsToCompute);
                 aiStrategy.RoundNumber++)
            {
                if (aiStrategy.RoundNumber > 10) break;

                var t0 = DateTime.Now;
                var roundStarterIndex = player1;
                AiRule r;

                if (!firstTime || c1 == null)
                {
                    c1 = aiStrategy.PlayCard1(out r);

                    var t01 = DateTime.Now - t0;
                    aiStrategy.MyIndex = player2;
                    aiStrategy.TeamMateIndex = TeamMateIndex == player2 ? MyIndex : (TeamMateIndex == -1 ? player3 : -1);
                    if (firstTime)
                    {
                        result.CardToPlay = c1;
                        result.Rule = r;
                        firstTime = false;
                    }
                }
                var t1 = DateTime.Now - t0;
                if (!firstTime || c2 == null)
                {
                    c2 = aiStrategy.PlayCard2(c1, out r);
                    aiStrategy.MyIndex = player3;
                    aiStrategy.TeamMateIndex = TeamMateIndex == player3 ? MyIndex : (TeamMateIndex == -1 ? player2 : -1);
                    if (firstTime)
                    {
                        result.CardToPlay = c2;
                        result.Rule = r;
                        firstTime = false;
                    }
                }
                var t2 = DateTime.Now - t0;
                var c3 = aiStrategy.PlayCard3(c1, c2, out r);

                var roundWinnerCard = Round.WinningCard(c1, c2, c3, _g.trump);
                var roundWinnerIndex = roundWinnerCard == c1 ? roundStarterIndex : (roundWinnerCard == c2 ? (roundStarterIndex + 1) % Game.NumPlayers : (roundStarterIndex + 2) % Game.NumPlayers);
                var roundScore = ComputeRoundScore(c1, c2, c3, aiStrategy.RoundNumber, roundStarterIndex);
                var t3 = DateTime.Now - t0;
                _log.TraceFormat("Simulation round {2} won by {0}. Points won: {1}", _g.players[roundWinnerIndex].Name, roundScore, aiStrategy.RoundNumber);
                if (firstTime)
                {
                    result.CardToPlay = c3;
                    result.Rule = r;
                    firstTime = false;
                }
                if (aiStrategy.RoundNumber == 10)
                {
                    result.Final7Won = roundWinnerCard.Suit == _g.trump && roundWinnerCard.Value == Hodnota.Sedma;
                }
                aiStrategy.MyIndex = roundWinnerIndex;
                aiStrategy.TeamMateIndex = ComputeTeamMateIndex(roundWinnerIndex, MyIndex, TeamMateIndex);
                player1 = aiStrategy.MyIndex;
                player2 = (aiStrategy.MyIndex + 1) % Game.NumPlayers;
                player3 = (aiStrategy.MyIndex + 2) % Game.NumPlayers;
                result.Score.Points[roundWinnerIndex] += roundScore;
                if (c1.Value == Hodnota.Svrsek && _hands[roundStarterIndex].HasK(c1.Suit))
                    result.Score.Points[roundStarterIndex] += c1.Suit == _g.trump ? 40 : 20;
                if (c2.Value == Hodnota.Svrsek && _hands[(roundStarterIndex + 1) % Game.NumPlayers].HasK(c2.Suit))
                    result.Score.Points[(roundStarterIndex + 1) % Game.NumPlayers] += c2.Suit == _g.trump ? 40 : 20;
                if (c3.Value == Hodnota.Svrsek && _hands[(roundStarterIndex + 2) % Game.NumPlayers].HasK(c3.Suit))
                    result.Score.Points[(roundStarterIndex + 2) % Game.NumPlayers] += c3.Suit == _g.trump ? 40 : 20;

                if (c1.Value == Hodnota.Kral && _hands[roundStarterIndex].HasQ(c1.Suit))
                    result.Score.Points[roundStarterIndex] += c1.Suit == _g.trump ? 40 : 20;
                if (c2.Value == Hodnota.Kral && _hands[(roundStarterIndex + 1) % Game.NumPlayers].HasQ(c2.Suit))
                    result.Score.Points[(roundStarterIndex + 1) % Game.NumPlayers] += c2.Suit == _g.trump ? 40 : 20;
                if (c3.Value == Hodnota.Kral && _hands[(roundStarterIndex + 2) % Game.NumPlayers].HasQ(c3.Suit))
                    result.Score.Points[(roundStarterIndex + 2) % Game.NumPlayers] += c3.Suit == _g.trump ? 40 : 20;
                var t4 = DateTime.Now - t0;
                _log.TraceFormat("Score: {0}/{1}/{2}", result.Score.Points[0], result.Score.Points[1], result.Score.Points[2]);
            }

            _log.DebugFormat("Round {0}. Finished simulation for {1}. Card/rule to play: {2} - {3}, expected score in the end: {4}/{5}/{6}\n",
                _g.RoundNumber, _g.players[MyIndex].Name, result.CardToPlay, result.Rule.Description, result.Score.Points[0], result.Score.Points[1], result.Score.Points[2]);

            return result;
        }

        private int ComputeRoundScore(Card c1, Card c2, Card c3, int round, int roundStarterIndex)
        {
            var score = (round == 10 ? 10 : 0);

            if (c1.Value == Hodnota.Eso || c1.Value == Hodnota.Desitka)
                score += 10;
            if (c2.Value == Hodnota.Eso || c2.Value == Hodnota.Desitka)
                score += 10;
            if (c3.Value == Hodnota.Eso || c3.Value == Hodnota.Desitka)
                score += 10;

            return score;
        }

        private static int ComputeTeamMateIndex(int roundWinnerIndex, int myIndex, int teamMateIndex)
        {
            if (roundWinnerIndex == myIndex)
                return teamMateIndex;

            if (roundWinnerIndex == teamMateIndex)
                return myIndex;

            if (teamMateIndex != -1)
                return -1;

            for (var i = 0; i < Game.NumPlayers; i++)
            {
                if (i != myIndex && i != roundWinnerIndex)
                    return i;
            }

            return -1;
        }

        public override void PlayCard(Renonc err)
        {
            var t = new Thread(() =>
                               {
                                   if (Probabilities == null)
                                   {
                                       Probabilities = _g.GameStartingPlayer == this ? new Probability(MyIndex, GameStarterIndex, new Hand(Hand), _g.trump, _g.talon)
                                                                                     : new Probability(MyIndex, GameStarterIndex, new Hand(Hand), _g.trump);
                                   }

                                   var t0 = DateTime.Now;
                                   var roundStarterIndex = Array.IndexOf(_g.players, _g.RoundStartingPlayer);
                                   Score score;
                                   Card cardToPlay = null;
                                   var cardScores = new Dictionary<Card, List<GameComputationResult>>();

                                   if (Settings.Cheat)
                                   {
                                       _hands = _g.players.Select(i => new Hand(i.Hand)).ToArray();
                                       var computationResult = ComputeGame(null, null);

                                       cardToPlay = computationResult.CardToPlay;
                                   }
                                   else
                                   {
                                       var canSkipSimulations = CanSkipSimulations();

                                       for (var i = 0; i < Game.NumPlayers; i++)
                                       {
                                            _log.DebugFormat("{0}'s probabilities for {1}:\n{2}", Name, _g.players[i].Name, Probabilities.FriendlyString(i, _g.RoundNumber));
                                       }
                                       //TODO: Consider using:
                                       //Parallel.For(0, Settings.MinSimulationsPerRound || (i < Settings.MaxSimulationsPerRound && !ruleWithoutTresholdFound), (i, loopState) => {
                                       //   if(breaking_condition) {
                                       //       loopState.Stop();
                                       //   }
                                       //});
                                       for (var i = 0; i < Settings.SimulationsPerRound; i++)
                                       {
                                           _hands = Probabilities.GenerateHands(_g.RoundNumber, roundStarterIndex);
                                           var computationResult = ComputeGame(null, null);

                                           if (cardScores.ContainsKey(computationResult.CardToPlay))
                                           {
                                               cardScores[computationResult.CardToPlay].Add(computationResult);
                                           }
                                           else
                                           {
                                               cardScores.Add(computationResult.CardToPlay, new List<GameComputationResult> { computationResult });
                                           }
                                           
                                           if (computationResult.Rule == AiRule.PlayTheOnlyValidCard || canSkipSimulations)    //We have only one card to play, so there is really no need to compute anything
                                           {
                                               break;
                                           }
                                       }
                                       if (canSkipSimulations)
                                       {
                                           _log.InfoFormat("Other simulations have been skipped");
                                       }
                                       _log.InfoFormat("Cards to choose from: {0}", cardScores.Count);
                                       var totalCount = cardScores.Sum(i => i.Value.Count);

                                       foreach (var cardScore in cardScores)
                                       {
                                           //opponent is only applicable if TeamMateIndex ! -1
                                           var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                                                            ? (MyIndex + 2) % Game.NumPlayers : (MyIndex + 1) % Game.NumPlayers;
                                           var maxScores = cardScore.Value.OrderByDescending(i => TeamMateIndex == -1
                                               ? i.Score.Points[(MyIndex + 1) % Game.NumPlayers] + i.Score.Points[(MyIndex + 2) % Game.NumPlayers]
                                               : i.Score.Points[opponent]).First();

                                           _log.InfoFormat("{0}: {1} times ({2:0}%) max. score: {3}/{4}/{5} avg. score {6:0}/{7:0}/{8:0}",
                                               cardScore.Key, cardScore.Value.Count, cardScore.Value.Count / (float)totalCount * 100,
                                               maxScores.Score.Points[0], maxScores.Score.Points[1], maxScores.Score.Points[2],
                                               cardScore.Value.Average(i => i.Score.Points[0]), cardScore.Value.Average(i => i.Score.Points[1]), cardScore.Value.Average(i => i.Score.Points[2]));
                                       }
                                       KeyValuePair<Card, List<GameComputationResult>> kvp;
                                       KeyValuePair<Card, GameComputationResult> kvp2;
                                       switch (Settings.CardSelectionStrategy)
                                       {
                                           case CardSelectionStrategy.MaxCount:
                                               var countOfRulesWithThreshold = cardScores.Sum(i => i.Value.Count(j => j.Rule.UseThreshold));

                                               _log.DebugFormat("Threshold value: {0}", Settings.RuleThreshold);
                                               _log.DebugFormat("Count of all rules with a threshold: {0}", countOfRulesWithThreshold);
                                               if (countOfRulesWithThreshold/(float) totalCount > Settings.RuleThreshold)
                                               {
                                                   kvp = cardScores.OrderByDescending(i => i.Value.Count)
                                                                 .FirstOrDefault(i => i.Value.Any(j => j.Rule.UseThreshold));
                                                   cardToPlay = kvp.Key;
                                                   DebugInfo.Rule = kvp.Value.First().Rule.Description;
                                                   DebugInfo.RuleCount = kvp.Value.Count;
                                               }
                                               if (cardToPlay == null)
                                               {
                                                   _log.DebugFormat("Threshold condition has not been met");
                                                   kvp = cardScores.OrderByDescending(i => i.Value.Count(j => !j.Rule.UseThreshold)).FirstOrDefault();
                                                   cardToPlay = kvp.Key;
                                                   DebugInfo.Rule = kvp.Value.First().Rule.Description;
                                                   DebugInfo.RuleCount = kvp.Value.Count;
                                               }
                                               if (cardToPlay == null)
                                               {
                                                   _log.DebugFormat("No rule without a threshold found");
                                                   kvp = cardScores.OrderByDescending(i => i.Value.Count).First();
                                                   cardToPlay = kvp.Key;
                                                   DebugInfo.Rule = kvp.Value.First().Rule.Description;
                                                   DebugInfo.RuleCount = kvp.Value.Count;
                                               }
                                               break;
                                           case CardSelectionStrategy.MinScore:
                                               //opponent is only applicable if TeamMateIndex ! -1
                                               var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                                                                ? (MyIndex + 2) % Game.NumPlayers : (MyIndex + 1) % Game.NumPlayers;
                                               var maxScores = cardScores.ToDictionary(k => k.Key,
                                                   v => v.Value.OrderByDescending(i => TeamMateIndex == -1
                                                       ? i.Score.Points[(MyIndex + 1)%Game.NumPlayers] +
                                                         i.Score.Points[(MyIndex + 2)%Game.NumPlayers]
                                                       : i.Score.Points[opponent]).First());

                                               kvp2 =
                                                   maxScores.OrderBy(
                                                       i =>  TeamMateIndex == -1
                                                           ? i.Value.Score.Points[(MyIndex + 1) % Game.NumPlayers] + i.Value.Score.Points[(MyIndex + 2) % Game.NumPlayers]
                                                           : i.Value.Score.Points[opponent]).First();
                                               cardToPlay = kvp2.Key;
                                               DebugInfo.Rule = kvp2.Value.Rule.Description;
                                               DebugInfo.RuleCount = cardScores.Count(i => i.Key == cardToPlay);
                                               break;
                                           case CardSelectionStrategy.AverageScore:
                                               kvp =
                                                   cardScores.OrderByDescending(
                                                       i => i.Value.Average(j => TeamMateIndex == -1
                                                           ? j.Score.Points[MyIndex]
                                                           : j.Score.Points[MyIndex] + j.Score.Points[TeamMateIndex])).First();
                                                cardToPlay = kvp.Key;
                                                DebugInfo.Rule = kvp.Value.First().Rule.Description;
                                                DebugInfo.RuleCount = kvp.Value.Count;
                                               break;
                                           default:
                                               throw new Exception("Unknown card selection strategy");
                                       }
                                   }
                                   var t1 = DateTime.Now - t0;

                                   _log.InfoFormat("{0} plays card: {1}", Name, cardToPlay);
                                   OnCardPlayed(new CardEventArgs(cardToPlay));
                               });

            OnPlayCardCommand(err);
            t.Start();
        }

        public override void PlayCard2(Card first, Renonc err)
        {
            var t = new Thread(() =>
                               {
                                   var t0 = DateTime.Now;
                                   var roundStarterIndex = Array.IndexOf(_g.players, _g.RoundStartingPlayer);
                                   Score score;
                                   Card cardToPlay = null;
                                   var cardScores = new Dictionary<Card, List<GameComputationResult>>();
                                   
                                   if (Settings.Cheat)
                                   {
                                       _hands = _g.players.Select(i => new Hand(i.Hand)).ToArray();
                                       var computationResult = ComputeGame(first, null);

                                       cardToPlay = computationResult.CardToPlay;
                                   }
                                   else
                                   {
                                       var canSkipSimulations = CanSkipSimulations(first);

                                       for (var i = 0; i < Settings.SimulationsPerRound; i++)
                                       {
                                           _hands = Probabilities.GenerateHands(_g.RoundNumber, roundStarterIndex);
                                           var computationResult = ComputeGame(first, null);

                                           if (cardScores.ContainsKey(computationResult.CardToPlay))
                                           {
                                               cardScores[computationResult.CardToPlay].Add(computationResult);
                                           }
                                           else
                                           {
                                               cardScores.Add(computationResult.CardToPlay, new List<GameComputationResult> { computationResult });
                                           }

                                           if (computationResult.Rule == AiRule.PlayTheOnlyValidCard || canSkipSimulations)    //We have only one card to play, so there is really no need to compute anything
                                           {
                                               break;
                                           }

                                           //TODO: do not stop computing rounds if we can win opponent's X
                                           //if (Settings.RoundsToCompute == 1 && 
                                           //    TeamMateIndex == (MyIndex + 1) % Game.NumPlayers && 
                                           //    computationResult.CardToPlay.IsHigherThan(first, _g.trump) &&
                                           //    !computationResult.Rule.UseThreshold)    //zajisti ze se nebudeme zbytecne zbavovat esa po prvni simulaci (ktera zavisi na nahodnem rozlozeni karet)
                                           //{
                                           //    //We do not want to compute more rounds, we play winning card and the 3rd player is my team mate, so there is no need for any simulations
                                           //    break;
                                           //}
                                       }

                                       if (canSkipSimulations)
                                       {
                                           _log.InfoFormat("Other simulations have been skipped");
                                       }
                                       _log.InfoFormat("Cards to choose from: {0}", cardScores.Count);
                                       var totalCount = cardScores.Sum(i => i.Value.Count);

                                       foreach (var cardScore in cardScores)
                                       {
                                           //opponent is only applicable if TeamMateIndex ! -1
                                           var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                                                            ? (MyIndex + 2) % Game.NumPlayers : (MyIndex + 1) % Game.NumPlayers;
                                           var maxScores = cardScore.Value.OrderByDescending(i => TeamMateIndex == -1
                                               ? i.Score.Points[(MyIndex + 1) % Game.NumPlayers] + i.Score.Points[(MyIndex + 2) % Game.NumPlayers]
                                               : i.Score.Points[opponent]).First();

                                           _log.InfoFormat("{0}: {1} times ({2:0}%) max. score: {3}/{4}/{5} avg. score {6:0}/{7:0}/{8:0}",
                                               cardScore.Key, cardScore.Value.Count, cardScore.Value.Count / (float)totalCount * 100,
                                               maxScores.Score.Points[0], maxScores.Score.Points[1], maxScores.Score.Points[2],
                                               cardScore.Value.Average(i => i.Score.Points[0]), cardScore.Value.Average(i => i.Score.Points[1]), cardScore.Value.Average(i => i.Score.Points[2]));
                                       }
                                       KeyValuePair<Card, List<GameComputationResult>> kvp;
                                       KeyValuePair<Card, GameComputationResult> kvp2;
                                       switch (Settings.CardSelectionStrategy)
                                       {
                                           case CardSelectionStrategy.MaxCount:
                                               var countOfRulesWithThreshold = cardScores.Sum(i => i.Value.Count(j => j.Rule.UseThreshold));

                                               _log.DebugFormat("Threshold value: {0}", Settings.RuleThreshold);
                                               _log.DebugFormat("Count of all rules with a threshold: {0}", countOfRulesWithThreshold);
                                               if (countOfRulesWithThreshold/(float) totalCount > Settings.RuleThreshold)
                                               {
                                                   kvp = cardScores.OrderByDescending(i => i.Value.Count)
                                                                 .FirstOrDefault(i => i.Value.Any(j => j.Rule.UseThreshold));
                                                   cardToPlay = kvp.Key;
                                                   DebugInfo.Rule = kvp.Value.First().Rule.Description;
                                                   DebugInfo.RuleCount = kvp.Value.Count;
                                               }
                                               if (cardToPlay == null)
                                               {
                                                   _log.DebugFormat("Threshold condition has not been met");
                                                   kvp = cardScores.OrderByDescending(i => i.Value.Count(j => !j.Rule.UseThreshold)).FirstOrDefault();
                                                   cardToPlay = kvp.Key;
                                                   DebugInfo.Rule = kvp.Value.First().Rule.Description;
                                                   DebugInfo.RuleCount = kvp.Value.Count;
                                               }
                                               if (cardToPlay == null)
                                               {
                                                   _log.DebugFormat("No rule without a threshold found");
                                                   kvp = cardScores.OrderByDescending(i => i.Value.Count).First();
                                                   cardToPlay = kvp.Key;
                                                   DebugInfo.Rule = kvp.Value.First().Rule.Description;
                                                   DebugInfo.RuleCount = kvp.Value.Count;
                                               }
                                               break;
                                           case CardSelectionStrategy.MinScore:
                                               //opponent is only applicable if TeamMateIndex ! -1
                                               var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                                                                ? (MyIndex + 2) % Game.NumPlayers : (MyIndex + 1) % Game.NumPlayers;
                                               var maxScores = cardScores.ToDictionary(k => k.Key,
                                                   v => v.Value.OrderByDescending(i => TeamMateIndex == -1
                                                       ? i.Score.Points[(MyIndex + 1)%Game.NumPlayers] +
                                                         i.Score.Points[(MyIndex + 2)%Game.NumPlayers]
                                                       : i.Score.Points[opponent]).First());

                                               kvp2 =
                                                   maxScores.OrderBy(
                                                       i =>  TeamMateIndex == -1
                                                           ? i.Value.Score.Points[(MyIndex + 1) % Game.NumPlayers] + i.Value.Score.Points[(MyIndex + 2) % Game.NumPlayers]
                                                           : i.Value.Score.Points[opponent]).First();
                                               cardToPlay = kvp2.Key;
                                               DebugInfo.Rule = kvp2.Value.Rule.Description;
                                               DebugInfo.RuleCount = cardScores.Count(i => i.Key == cardToPlay);
                                               break;
                                           case CardSelectionStrategy.AverageScore:
                                               kvp =
                                                   cardScores.OrderByDescending(
                                                       i => i.Value.Average(j => TeamMateIndex == -1
                                                           ? j.Score.Points[MyIndex]
                                                           : j.Score.Points[MyIndex] + j.Score.Points[TeamMateIndex])).First();
                                                cardToPlay = kvp.Key;
                                                DebugInfo.Rule = kvp.Value.First().Rule.Description;
                                                DebugInfo.RuleCount = kvp.Value.Count;
                                               break;
                                           default:
                                               throw new Exception("Unknown card selection strategy");
                                       }
                                   }
                                   var t1 = DateTime.Now - t0;

                                   _log.InfoFormat("{0} plays card: {1}", Name, cardToPlay);
                                   OnCardPlayed(new CardEventArgs(cardToPlay));
                               });

            OnPlayCardCommand(err);
            t.Start();
        }

        public override void PlayCard3(Card first, Card second, Renonc err)
        {
            var t = new Thread(() =>
                               {
                                   var t0 = DateTime.Now;
                                   var roundStarterIndex = Array.IndexOf(_g.players, _g.RoundStartingPlayer);
                                   Score score;
                                   Card cardToPlay = null;
                                   var cardScores = new Dictionary<Card, List<GameComputationResult>>();
                                   
                                   if (Settings.Cheat)
                                   {
                                       _hands = _g.players.Select(i => new Hand(i.Hand)).ToArray();
                                       var computationResult = ComputeGame(first, second);

                                       cardToPlay = computationResult.CardToPlay;
                                   }
                                   else
                                   {
                                       var canSkipSimulations = CanSkipSimulations(first, second);

                                       for (var i = 0; i < Settings.SimulationsPerRound; i++)
                                       {
                                           _hands = Probabilities.GenerateHands(_g.RoundNumber, roundStarterIndex);
                                           var computationResult = ComputeGame(first, second);

                                           if (cardScores.ContainsKey(computationResult.CardToPlay))
                                           {
                                               cardScores[computationResult.CardToPlay].Add(computationResult);
                                           }
                                           else
                                           {
                                               cardScores.Add(computationResult.CardToPlay, new List<GameComputationResult> { computationResult });
                                           }

                                           if (computationResult.Rule == AiRule.PlayTheOnlyValidCard || canSkipSimulations)    //We have only one card to play, so there is really no need to compute anything
                                           {
                                               break;
                                           }

                                           //if (Settings.RoundsToCompute == 1 &&         //We do not want to compute more rounds so there is no need for any simulations
                                           //    !computationResult.Rule.UseThreshold)    //zajisti ze se nebudeme zbytecne zbavovat esa po prvni simulaci (ktera zavisi na nahodnem rozlozeni karet)
                                           //{
                                           //    break;
                                           //}
                                       }
                                       if (canSkipSimulations)
                                       {
                                           _log.InfoFormat("Other simulations have been skipped");
                                       }
                                       _log.InfoFormat("Cards to choose from: {0}", cardScores.Count);
                                       var totalCount = cardScores.Sum(i => i.Value.Count);

                                       foreach (var cardScore in cardScores)
                                       {
                                           //opponent is only applicable if TeamMateIndex ! -1
                                           var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                                                            ? (MyIndex + 2) % Game.NumPlayers : (MyIndex + 1) % Game.NumPlayers;
                                           var maxScores = cardScore.Value.OrderByDescending(i => TeamMateIndex == -1
                                               ? i.Score.Points[(MyIndex + 1) % Game.NumPlayers] + i.Score.Points[(MyIndex + 2) % Game.NumPlayers]
                                               : i.Score.Points[opponent]).First();

                                           _log.InfoFormat("{0}: {1} times ({2:0}%) max. score: {3}/{4}/{5} avg. score {6:0}/{7:0}/{8:0}",
                                               cardScore.Key, cardScore.Value.Count, cardScore.Value.Count / (float)totalCount * 100,
                                               maxScores.Score.Points[0], maxScores.Score.Points[1], maxScores.Score.Points[2],
                                               cardScore.Value.Average(i => i.Score.Points[0]), cardScore.Value.Average(i => i.Score.Points[1]), cardScore.Value.Average(i => i.Score.Points[2]));
                                       }
                                       KeyValuePair<Card, List<GameComputationResult>> kvp;
                                       KeyValuePair<Card, GameComputationResult> kvp2;
                                       switch (Settings.CardSelectionStrategy)
                                       {
                                           case CardSelectionStrategy.MaxCount:
                                               var countOfRulesWithThreshold = cardScores.Sum(i => i.Value.Count(j => j.Rule.UseThreshold));

                                               _log.DebugFormat("Threshold value: {0}", Settings.RuleThreshold);
                                               _log.DebugFormat("Count of all rules with a threshold: {0}", countOfRulesWithThreshold);
                                               if (countOfRulesWithThreshold / (float)totalCount > Settings.RuleThreshold)
                                               {
                                                   kvp = cardScores.OrderByDescending(i => i.Value.Count)
                                                                 .FirstOrDefault(i => i.Value.Any(j => j.Rule.UseThreshold));
                                                   cardToPlay = kvp.Key;
                                                   DebugInfo.Rule = kvp.Value.First().Rule.Description;
                                                   DebugInfo.RuleCount = kvp.Value.Count;
                                               }
                                               if (cardToPlay == null)
                                               {
                                                   _log.DebugFormat("Threshold condition has not been met");
                                                   kvp = cardScores.OrderByDescending(i => i.Value.Count(j => !j.Rule.UseThreshold)).FirstOrDefault();
                                                   cardToPlay = kvp.Key;
                                                   DebugInfo.Rule = kvp.Value.First().Rule.Description;
                                                   DebugInfo.RuleCount = kvp.Value.Count;
                                               }
                                               if (cardToPlay == null)
                                               {
                                                   _log.DebugFormat("No rule without a threshold found");
                                                   kvp = cardScores.OrderByDescending(i => i.Value.Count).First();
                                                   cardToPlay = kvp.Key;
                                                   DebugInfo.Rule = kvp.Value.First().Rule.Description;
                                                   DebugInfo.RuleCount = kvp.Value.Count;
                                               }
                                               break;
                                           case CardSelectionStrategy.MinScore:
                                               //opponent is only applicable if TeamMateIndex ! -1
                                               var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                                                                ? (MyIndex + 2) % Game.NumPlayers : (MyIndex + 1) % Game.NumPlayers;
                                               var maxScores = cardScores.ToDictionary(k => k.Key,
                                                   v => v.Value.OrderByDescending(i => TeamMateIndex == -1
                                                       ? i.Score.Points[(MyIndex + 1) % Game.NumPlayers] +
                                                         i.Score.Points[(MyIndex + 2) % Game.NumPlayers]
                                                       : i.Score.Points[opponent]).First());

                                               kvp2 =
                                                   maxScores.OrderBy(
                                                       i => TeamMateIndex == -1
                                                           ? i.Value.Score.Points[(MyIndex + 1) % Game.NumPlayers] + i.Value.Score.Points[(MyIndex + 2) % Game.NumPlayers]
                                                           : i.Value.Score.Points[opponent]).First();
                                               cardToPlay = kvp2.Key;
                                               DebugInfo.Rule = kvp2.Value.Rule.Description;
                                               DebugInfo.RuleCount = cardScores.Count(i => i.Key == cardToPlay);
                                               break;
                                           case CardSelectionStrategy.AverageScore:
                                               kvp =
                                                   cardScores.OrderByDescending(
                                                       i => i.Value.Average(j => TeamMateIndex == -1
                                                           ? j.Score.Points[MyIndex]
                                                           : j.Score.Points[MyIndex] + j.Score.Points[TeamMateIndex])).First();
                                               cardToPlay = kvp.Key;
                                               DebugInfo.Rule = kvp.Value.First().Rule.Description;
                                               DebugInfo.RuleCount = kvp.Value.Count;
                                               break;
                                           default:
                                               throw new Exception("Unknown card selection strategy");
                                       }
                                   }
                                   var t1 = DateTime.Now - t0;

                                   _log.InfoFormat("{0} plays card: {1}", Name, cardToPlay);
                                   OnCardPlayed(new CardEventArgs(cardToPlay));
                               });

            OnPlayCardCommand(err);
            t.Start();
        }

        private bool CanSkipSimulations()
        {
            if (_g.RoundNumber == 10)
            {
                return true;
            }

            var player2 = (MyIndex + 1) % Game.NumPlayers;
            var player3 = (MyIndex + 2) % Game.NumPlayers;

            //zkusim jestli neexistuje barva ve ktere me muze nekdo chytit
            //TODO: tohle by se dalo zlepsit -> napriklad tak, ze budu ignorovat trivialni diry
            foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                //vem nejmensi kartu v barve
                var c = Hand.Where(i => i.Suit == barva).OrderBy(i => i.Value).FirstOrDefault();

                if (c != null)
                {
                    //jestli ma souper cokoli vyssiho, tak je sance, ze me muze chytit a tudiz nesmim preskocit simulace
                    if (Probabilities.NoSuitHigherThanCardProbability(player2, c, _g.RoundNumber) > 0 ||
                        Probabilities.NoSuitHigherThanCardProbability(player3, c, _g.RoundNumber) > 0)
                    {
                        //je sance ze me nekdo muze chytit
                        return false;
                    }
                }
            }

            return true;
        }

        private bool CanSkipSimulations(Card first)
        {
            if (_g.RoundNumber == 10)
            {
                return true;
            }

            var player1 = (MyIndex + 2) % Game.NumPlayers;
            var player3 = (MyIndex + 1) % Game.NumPlayers;
            var validCards = ValidCards(Hand, _g.trump, first);
            
            if (validCards.All(c => c.Value == Hodnota.Desitka || c.Value == Hodnota.Eso) ||
                validCards.All(c => c.Value != Hodnota.Desitka && c.Value != Hodnota.Eso))
            {
                //musim hrat A nebo X
                //nebo
                //nemam ani A ani X
                return true;
            }

            if (TeamMateIndex == player3 &&
                validCards.First().IsHigherThan(first, _g.trump) &&
                validCards.Any(c => c.Value == Hodnota.Desitka))
            {
                //: -co
                //: mam desitku ale ne eso a zaroven beru stych
                return true;
            }

            //souper(i) urcite nemaji ani A ani X a pripadny souper co hraje po me nema navic ani trumf nebo ma urcite jinou kartu v barve
            var validSuits = validCards.Select(i => i.Suit).Distinct().ToList();

            if (validSuits.Count() == 1)
            {
                var A = new Card(validSuits.First(), Hodnota.Eso);
                var X = new Card(validSuits.First(), Hodnota.Desitka);

                if (TeamMateIndex == -1)
                {
                    if (Probabilities.CardProbability(player1, A) == 0f &&
                        Probabilities.CardProbability(player1, X) == 0f &&
                        Probabilities.CardProbability(player3, A) == 0f &&
                        Probabilities.CardProbability(player3, X) == 0f &&
                        (Probabilities.SuitProbability(player3, _g.trump, _g.RoundNumber) == 0f &&
                         validCards.All(c => Probabilities.SuitHigherThanCardProbability(player3, c, _g.RoundNumber) == 0f)))
                    {
                        return true;
                    }
                }
                else
                {
                    var opponent = TeamMateIndex == player1 ? player3 : player1;

                    if (Probabilities.CardProbability(opponent, A) == 0f &&
                        Probabilities.CardProbability(opponent, X) == 0f &&
                        (TeamMateIndex == player3 ||
                         Probabilities.SuitProbability(opponent, _g.trump, _g.RoundNumber) == 0f &&
                         validCards.All(c => Probabilities.SuitHigherThanCardProbability(player3, c, _g.RoundNumber) == 0f)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool CanSkipSimulations(Card first, Card second)
        {
            if (_g.RoundNumber == 10)
            {
                return true;
            }

            var player1 = (MyIndex + 1) % Game.NumPlayers;
            var player2 = (MyIndex + 2) % Game.NumPlayers;
            var validCards = ValidCards(Hand, _g.trump, first, second);
            if (validCards.All(c => c.Value == Hodnota.Desitka || c.Value == Hodnota.Eso) ||
                validCards.All(c => c.Value != Hodnota.Desitka && c.Value != Hodnota.Eso))
            {
                return true;
            }


            if (validCards.First().IsHigherThan(first, _g.trump) &&
                validCards.First().IsHigherThan(second, _g.trump) &&
                validCards.Any(c => c.Value == Hodnota.Desitka))
            {
                //: -co
                //: mam desitku ale ne eso a zaroven beru stych
                return true;
            }

            var validSuits = validCards.Select(i => i.Suit).Distinct().ToList();

            if (validSuits.Count() == 1)
            {
                var A = new Card(validSuits.First(), Hodnota.Eso);
                var X = new Card(validSuits.First(), Hodnota.Desitka);

                if (TeamMateIndex == -1)
                {
                    if (Probabilities.CardProbability(player1, A) == 0f &&
                        Probabilities.CardProbability(player1, X) == 0f &&
                        Probabilities.CardProbability(player2, A) == 0f &&
                        Probabilities.CardProbability(player2, X) == 0f)
                    {
                        return true;
                    }
                }
                else
                {
                    var opponent = TeamMateIndex == player1 ? player2 : player1;

                    if (Probabilities.CardProbability(opponent, A) == 0f &&
                        Probabilities.CardProbability(opponent, X) == 0f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public override void RoundInfo(Card first, AbstractPlayer player1, bool hlas)
        {
            var roundStartingIndex = Array.IndexOf(_g.players, _g.RoundStartingPlayer);
            
            if (Probabilities == null)
            {
                Probabilities = new Probability(MyIndex, GameStarterIndex, new Hand(Hand), _g.trump);
            }
            Probabilities.UpdateProbabilities(_g.RoundNumber, roundStartingIndex, _g.CurrentRound.c1, _g.CurrentRound.player1Hlas != 0, _g.trump);
        }

        public override void RoundInfo(Card first, Card second, AbstractPlayer player1, AbstractPlayer player2, bool hlas)
        {
            var roundStartingIndex = Array.IndexOf(_g.players, _g.RoundStartingPlayer);

            Probabilities.UpdateProbabilities(_g.RoundNumber, roundStartingIndex, _g.CurrentRound.c1, _g.CurrentRound.c2, _g.CurrentRound.player2Hlas != 0);
        }

        public override void RoundInfo(Card first, Card second, Card third, AbstractPlayer player1, AbstractPlayer player2,
            AbstractPlayer player3, bool hlas)
        {
            var roundStartingIndex = Array.IndexOf(_g.players, _g.RoundStartingPlayer);

            Probabilities.UpdateProbabilities(_g.RoundNumber, roundStartingIndex, _g.CurrentRound.c1, _g.CurrentRound.c2, _g.CurrentRound.c3, _g.CurrentRound.player3Hlas != 0);
        }

        public override void RoundFinishedInfo(AbstractPlayer winner, int points)
        {
            //throw new NotImplementedException();
            OnEndOfRoundConfirmed();
        }

        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        //private readonly List<Card>[] _hands = new List<Card>[Game.NumPlayers];
        private Hand[] _hands = new Hand[Game.NumPlayers];

        /// <summary>
        /// GameStarterRoundIndex je index toho, kdo volil trumfy (a hraje sam) v tomto kole (0 = ten, pro koho pocitame)
        /// </summary>
        //private int GameStarterRoundIndex
        //{
        //    get
        //    {
        //        int delta = Game.NumPlayers - MyIndex;
        //        return (GameStarterIndex + delta) % Game.NumPlayers;
        //    }
        //}

        private readonly List<Card> cardsPlayed = new List<Card>();
    }
}
