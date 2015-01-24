using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
//using log4net;
using Mariasek.Engine.New.Logger;
using Mariasek.Engine.New.Configuration;

namespace Mariasek.Engine.New
{
    public class AiPlayer : AbstractPlayer, IStatsPlayer
    {
#if !PORTABLE
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
#else
        private static readonly ILog _log = new DummyLogWrapper();
#endif   

        private List<Card> _talon;
        private Hand[] _hands = new Hand[Game.NumPlayers];
        private new List<GameComputationResult> _scores;
 
        public Probability Probabilities { get; set; }
        public AiPlayerSettings Settings { get; set; }

        public AiPlayer(Game g) : base(g)
        {
            Settings = new AiPlayerSettings
            {
                Cheat = false,
                RoundsToCompute = 1,
                CardSelectionStrategy = CardSelectionStrategy.MaxCount,
                SimulationsPerRound = 50,
                RuleThreshold = 80,
                GameThreshold = 80
            };
            _log.InfoFormat("AiPlayerSettings:\n{0}", Settings);

            DebugInfo = new PlayerDebugInfo();
            g.GameTypeChosen += GameTypeChosen;
            g.CardPlayed += CardPlayed;
            //g.RoundFinished += RoundFinished;
        }

        public AiPlayer(Game g, ParameterConfigurationElementCollection parameters) : this(g)
        {
            Settings.Cheat = bool.Parse(parameters["AiCheating"].Value);
            Settings.RoundsToCompute = int.Parse(parameters["RoundsToCompute"].Value);
            Settings.CardSelectionStrategy = (CardSelectionStrategy)Enum.Parse(typeof(CardSelectionStrategy), parameters["CardSelectionStrategy"].Value);
            Settings.SimulationsPerRound = int.Parse(parameters["SimulationsPerRound"].Value);
            Settings.RuleThreshold = int.Parse(parameters["RuleThreshold"].Value) / 100f;
            Settings.GameThreshold = int.Parse(parameters["GameThreshold"].Value) / 100f;
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

        public override Card ChooseTrump()
        {
            var scores = Enum.GetValues(typeof(Barva)).Cast<Barva>().Select(barva => new
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
            return card;
        }

        public override List<Card> ChooseTalon()
        {
            //TODO: promyslet poradne jak na to (v kombinaci s hlasy apod.)
            var talon = new List<Card>();
            //nejdriv zkus vzit karty v barve kde krom esa nemam nic jineho (neber krale ani svrska)
            var b = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        .Where(barva => barva != _g.trump &&
                                        Hand.Count(i => i.Suit == barva &&
                                                        i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka) <= 2 &&
                                        !Hand.HasX(barva) &&
                                        !(Hand.HasK(barva) && Hand.HasQ(barva)));

            talon.AddRange(Hand.Where(i => b.Contains(i.Suit) &&
                                      i.Value != Hodnota.Eso &&
                                      i.Value != Hodnota.Desitka)
                               .Take(2)
                               .ToList());
            if (talon.Count < 2)
            {
                b = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        .Where(barva => barva != _g.trump &&
                                        (talon.Count == 0 || barva != talon.First().Suit) &&
                                        Hand.Count(i => i.Suit == barva &&
                                                        i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka) <= 2 &&
                                        !Hand.HasX(barva));

                talon.AddRange(Hand.Where(i => b.Contains(i.Suit) &&
                                          i.Value != Hodnota.Eso &&
                                          i.Value != Hodnota.Desitka)
                                   .Take(2 - talon.Count)
                                   .ToList());

                if (talon.Count < 2)
                {
                    b = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(barva => barva != _g.trump &&
                                            (talon.Count == 0 || barva != talon.First().Suit));
                    talon.AddRange(Hand.Where(i => b.Contains(i.Suit) &&
                                              i.Value != Hodnota.Eso &&
                                              i.Value != Hodnota.Desitka)
                                       .OrderBy(i => i.Value)
                                       .Take(2 - talon.Count)
                                       .ToList());
                }
            }

            _talon = new List<Card>(talon);
            _log.DebugFormat("Talon chosen: {0} {1}", talon[0], talon[1]);
            return talon;
        }

        public override GameFlavour ChooseGameFlavour()
        {
            //TODO: rozhodnout kdy hrat betl a durch
            return GameFlavour.Good;
        }

        public override Hra ChooseGameType(Hra minimalBid = Hra.Hra)
        {
            //tohle je docasne dokud neumime betl a durch
            if (minimalBid >= Hra.Betl)
                return minimalBid;

            //nasimuluj hry pro kazdeho vaznejsiho kandidata na trumfy (skore >= n)
            var cardScores = new Dictionary<Card, List<GameComputationResult>>();

            //rozhodnout se kde delat inicializaci (asi driv pred vyberem typu hry)
            //            if (Probabilities == null)
            //{
                Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, _talon);
            //}

            for (int i = 0; i < Settings.SimulationsPerRound; i++)
            {
                _hands = Probabilities.GenerateHands(1, PlayerIndex);
                var gameComputationResult = ComputeGame(null, null, _g.trump, Hra.Sedma, 10, 1);

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
            var opponent = TeamMateIndex == (PlayerIndex + 1) % Game.NumPlayers
                                                                ? (PlayerIndex + 2) % Game.NumPlayers : (PlayerIndex + 1) % Game.NumPlayers;
            _scores = cardScores.SelectMany(i => i.Value).ToList();
            //sgrupuj simulace podle vysledku skore
            var myScores = _scores.GroupBy(i => i.Score[PlayerIndex])
                                .Select(g => new
                                {
                                    Score = g.Key,
                                    Count = g.Count()
                                });
            var finalSevens = cardScores.SelectMany(i => i.Value)
                                        .Where(i => i.Final7Won.HasValue)
                                        .GroupBy(i => i.Final7Won)
                                        .ToDictionary(k => k.Key.Value, v => v.Count());
            if (!finalSevens.ContainsKey(true))
            {
                finalSevens.Add(true, 0);
            }
            if (!finalSevens.ContainsKey(false))
            {
                finalSevens.Add(false, 0);
            }
            foreach (var score in myScores)
            {
                _log.DebugFormat("simulated score: {0} pts {1} times ({2}%)", score.Score, score.Count, score.Count * 100 / myScores.Sum(i => i.Count));
            }

            //najdi nejmensi skore
            //var minScore = scores.OrderBy(i => i.Score).First();
            //var gameType = minScore.Score < 100 ? Hra.Hra : Hra.Kilo;

            //hledej nejvyssi pocet bodu s prahem vyssim nez je parametr
            var count = 0;
            var minScore = 0;
            foreach (var score in myScores.OrderByDescending(i => i.Score))
            {
                minScore = score.Score;
                count += score.Count;
                if (count >= Settings.GameThreshold / Settings.SimulationsPerRound)
                {
                    break;
                }
            }
            var gameType = minScore < 100 ? Hra.Hra : Hra.Kilo;

            if (finalSevens[true] > finalSevens[false])
            {
                gameType |= Hra.Sedma;
            };

            _log.DebugFormat("Selected game type: {0}", gameType);

            return gameType;
        }

        private int _numberOfDoubles = 0;

        public override Hra GetBidsAndDoubles(Bidding bidding)
        {
            //1x flekujeme hru, jinak mlcime
            if (_numberOfDoubles++ == 0)
            {
                return bidding.Bids & Hra.Hra;
            }
            return 0;

            var money = _scores.Select(i =>
            {
                var calc = new AddingMoneyCalculator(_g, bidding, i);

                calc.CalculateMoney();

                return calc.MoneyWon[PlayerIndex];
            }).ToArray();

            var percentWon = (float) money.Count(i => i > 0) / money.Count();

            if (percentWon > (100 - Settings.GameThreshold)/money.Count()*_numberOfDoubles)
            {
                return Hra.Hra;
            }
            return 0;
        }

        public override void Init()
        {
            Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, _talon);
        }

        private void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            Probabilities.UpdateProbabilitiesAfterGameTypeChosen(e);
        }

        private void CardPlayed(object sender, Round r)
        {
            if (r.c3 != null)
            {
                Probabilities.UpdateProbabilities(r.number, r.player1.PlayerIndex, r.c1, r.c2, r.c3, r.hlas3);
            }
            else if (r.c2 != null)
            {
                Probabilities.UpdateProbabilities(r.number, r.player1.PlayerIndex, r.c1, r.c2, r.hlas2);
            }
            else
            {
                Probabilities.UpdateProbabilities(r.number, r.player1.PlayerIndex, r.c1, r.hlas1, _g.trump);
            }
        }

        public override Card PlayCard(Round r)
        {
            var roundStarterIndex = r.player1.PlayerIndex;
            Card cardToPlay = null;
            var cardScores = new Dictionary<Card, List<GameComputationResult>>();

            if (Settings.Cheat)
            {
                _hands = _g.players.Select(i => new Hand(i.Hand)).ToArray();
                var computationResult = ComputeGame(r.c1, r.c2);

                cardToPlay = computationResult.CardToPlay;
            }
            else
            {
                var canSkipSimulations = CanSkipSimulations(r.c1, r.c2);

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
                    var computationResult = ComputeGame(r.c1, r.c2);

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
                cardToPlay = ChooseCardToPlay(cardScores);
            }

            _log.InfoFormat("{0} plays card: {1}", Name, cardToPlay);
            return cardToPlay;                
        }

        private Card ChooseCardToPlay(Dictionary<Card, List<GameComputationResult>> cardScores)
        {
            Card cardToPlay = null;

            _log.InfoFormat("Cards to choose from: {0}", cardScores.Count);
            var totalCount = cardScores.Sum(i => i.Value.Count);

            foreach (var cardScore in cardScores)
            {
                //opponent is only applicable if TeamMateIndex ! -1
                var opponent = TeamMateIndex == (PlayerIndex + 1)%Game.NumPlayers
                    ? (PlayerIndex + 2)%Game.NumPlayers
                    : (PlayerIndex + 1)%Game.NumPlayers;
                var maxScores = cardScore.Value.OrderByDescending(i => TeamMateIndex == -1
                    ? i.Score[(PlayerIndex + 1)%Game.NumPlayers] + i.Score[(PlayerIndex + 2)%Game.NumPlayers]
                    : i.Score[opponent]).First();

                _log.InfoFormat("{0}: {1} times ({2:0}%) max. score: {3}/{4}/{5} avg. score {6:0}/{7:0}/{8:0}",
                    cardScore.Key, cardScore.Value.Count, cardScore.Value.Count/(float) totalCount*100,
                    maxScores.Score[0], maxScores.Score[1], maxScores.Score[2],
                    cardScore.Value.Average(i => i.Score[0]), cardScore.Value.Average(i => i.Score[1]),
                    cardScore.Value.Average(i => i.Score[2]));
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
                    var opponent = TeamMateIndex == (PlayerIndex + 1)%Game.NumPlayers
                        ? (PlayerIndex + 2)%Game.NumPlayers
                        : (PlayerIndex + 1)%Game.NumPlayers;
                    var maxScores = cardScores.ToDictionary(k => k.Key,
                        v => v.Value.OrderByDescending(i => TeamMateIndex == -1
                            ? i.Score[(PlayerIndex + 1)%Game.NumPlayers] +
                              i.Score[(PlayerIndex + 2)%Game.NumPlayers]
                            : i.Score[opponent]).First());

                    kvp2 =
                        maxScores.OrderBy(
                            i => TeamMateIndex == -1
                                ? i.Value.Score[(PlayerIndex + 1)%Game.NumPlayers] +
                                  i.Value.Score[(PlayerIndex + 2)%Game.NumPlayers]
                                : i.Value.Score[opponent]).First();
                    cardToPlay = kvp2.Key;
                    DebugInfo.Rule = kvp2.Value.Rule.Description;
                    DebugInfo.RuleCount = cardScores.Count(i => i.Key == cardToPlay);
                    break;
                case CardSelectionStrategy.AverageScore:
                    kvp =
                        cardScores.OrderByDescending(
                            i => i.Value.Average(j => TeamMateIndex == -1
                                ? j.Score[PlayerIndex]
                                : j.Score[PlayerIndex] + j.Score[TeamMateIndex])).First();
                    cardToPlay = kvp.Key;
                    DebugInfo.Rule = kvp.Value.First().Rule.Description;
                    DebugInfo.RuleCount = kvp.Value.Count;
                    break;
                default:
                    throw new Exception("Unknown card selection strategy");
            }
            return cardToPlay;
        }

        private GameComputationResult ComputeGame(Card c1, Card c2, Barva? trump = null, Hra? gameType = null, int? roundsToCompute = null, int? initialRoundNumber = null)
        {
            var result = new GameComputationResult
            {
                Score = new int[Game.NumPlayers],
                Final7Won = null
            };
            for (var i = 0; i < Game.NumRounds && _g.rounds[i] != null; i++)
            {
                result.Score[_g.rounds[i].player1.PlayerIndex] += _g.rounds[i].points1;
                result.Score[_g.rounds[i].player2.PlayerIndex] += _g.rounds[i].points2;
                result.Score[_g.rounds[i].player3.PlayerIndex] += _g.rounds[i].points3;
            }
            var firstTime = true;
            int player1;
            int player2;
            int player3;

            if (c1 == null && c2 == null)
            {
                player1 = PlayerIndex;
                player2 = (PlayerIndex + 1) % Game.NumPlayers;
                player3 = (PlayerIndex + 2) % Game.NumPlayers;
            }
            else if (c2 == null)
            {
                player1 = (PlayerIndex + 2) % Game.NumPlayers;
                player2 = PlayerIndex;
                player3 = (PlayerIndex + 1) % Game.NumPlayers;
            }
            else
            {
                player1 = (PlayerIndex + 1) % Game.NumPlayers;
                player2 = (PlayerIndex + 2) % Game.NumPlayers;
                player3 = PlayerIndex;
            }

            var aiStrategy = new AiStrategy(trump.HasValue ? trump.Value : _g.trump, gameType.HasValue ? gameType.Value : _g.GameType, _hands)
            {
                MyIndex = PlayerIndex,
                MyName = Name,
                TeamMateIndex = TeamMateIndex,
                RoundNumber = initialRoundNumber.HasValue ? initialRoundNumber.Value : _g.RoundNumber
            };
            _log.DebugFormat("Round {0}. Starting simulation for {1}", _g.RoundNumber, _g.players[PlayerIndex].Name);
            if (c1 != null) _log.DebugFormat("First card: {0}", c1);
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

                var roundStarterIndex = player1;
                AiRule r;

                if (!firstTime || c1 == null)
                {
                    c1 = aiStrategy.PlayCard1(out r);

                    aiStrategy.MyIndex = player2;
                    //aiStrategy.TeamMateIndex = TeamMateIndex == player2 ? PlayerIndex : (TeamMateIndex == -1 ? player3 : -1);
                    aiStrategy.TeamMateIndex = aiStrategy.TeamMateIndex == player2 ? player1 : (aiStrategy.TeamMateIndex == -1 ? player3 : -1);
                    if (firstTime)
                    {
                        result.CardToPlay = c1;
                        result.Rule = r;
                        firstTime = false;
                    }
                }
                if (!firstTime || c2 == null)
                {
                    c2 = aiStrategy.PlayCard2(c1, out r);
                    aiStrategy.MyIndex = player3;
                    //aiStrategy.TeamMateIndex = TeamMateIndex == player3 ? PlayerIndex : (TeamMateIndex == -1 ? player2 : -1);
                    aiStrategy.TeamMateIndex = aiStrategy.TeamMateIndex == player3 ? player2 : (aiStrategy.TeamMateIndex == -1 ? player1 : -1);
                    if (firstTime)
                    {
                        result.CardToPlay = c2;
                        result.Rule = r;
                        firstTime = false;
                    }
                }
                var c3 = aiStrategy.PlayCard3(c1, c2, out r);

                if (c1 == null || c2 == null || c3 == null)
                    c3 = c3; //investigate
                var roundWinnerCard = Round.WinningCard(c1, c2, c3, _g.trump);
                var roundWinnerIndex = roundWinnerCard == c1 ? roundStarterIndex : (roundWinnerCard == c2 ? (roundStarterIndex + 1) % Game.NumPlayers : (roundStarterIndex + 2) % Game.NumPlayers);
                var roundScore = Round.ComputePointsWon(c1, c2, c3, aiStrategy.RoundNumber);

                _log.TraceFormat("Simulation round {2} won by {0}. Points won: {1}", _g.players[roundWinnerIndex].Name, roundScore, aiStrategy.RoundNumber);
                if (firstTime)
                {
                    result.CardToPlay = c3;
                    result.Rule = r;
                    firstTime = false;
                }
                if (aiStrategy.RoundNumber == 10)
                {
                    result.Final7Won = roundWinnerCard.Suit == _g.trump && roundWinnerCard.Value == Hodnota.Sedma && roundWinnerIndex == _g.GameStartingPlayerIndex;
                }
                aiStrategy.MyIndex = roundWinnerIndex;
                aiStrategy.TeamMateIndex = _g.players[roundWinnerIndex].TeamMateIndex;
                player1 = aiStrategy.MyIndex;
                player2 = (aiStrategy.MyIndex + 1) % Game.NumPlayers;
                player3 = (aiStrategy.MyIndex + 2) % Game.NumPlayers;
                result.Score[roundWinnerIndex] += roundScore;
                if (c1.Value == Hodnota.Svrsek && _hands[roundStarterIndex].HasK(c1.Suit))
                    result.Score[roundStarterIndex] += c1.Suit == _g.trump ? 40 : 20;
                if (c2.Value == Hodnota.Svrsek && _hands[(roundStarterIndex + 1) % Game.NumPlayers].HasK(c2.Suit))
                    result.Score[(roundStarterIndex + 1) % Game.NumPlayers] += c2.Suit == _g.trump ? 40 : 20;
                if (c3.Value == Hodnota.Svrsek && _hands[(roundStarterIndex + 2) % Game.NumPlayers].HasK(c3.Suit))
                    result.Score[(roundStarterIndex + 2) % Game.NumPlayers] += c3.Suit == _g.trump ? 40 : 20;

                if (c1.Value == Hodnota.Kral && _hands[roundStarterIndex].HasQ(c1.Suit))
                    result.Score[roundStarterIndex] += c1.Suit == _g.trump ? 40 : 20;
                if (c2.Value == Hodnota.Kral && _hands[(roundStarterIndex + 1) % Game.NumPlayers].HasQ(c2.Suit))
                    result.Score[(roundStarterIndex + 1) % Game.NumPlayers] += c2.Suit == _g.trump ? 40 : 20;
                if (c3.Value == Hodnota.Kral && _hands[(roundStarterIndex + 2) % Game.NumPlayers].HasQ(c3.Suit))
                    result.Score[(roundStarterIndex + 2) % Game.NumPlayers] += c3.Suit == _g.trump ? 40 : 20;

                _log.TraceFormat("Score: {0}/{1}/{2}", result.Score[0], result.Score[1], result.Score[2]);
            }

            _log.DebugFormat("Round {0}. Finished simulation for {1}. Card/rule to play: {2} - {3}, expected score in the end: {4}/{5}/{6}\n",
                _g.RoundNumber, _g.players[PlayerIndex].Name, result.CardToPlay, result.Rule.Description, result.Score[0], result.Score[1], result.Score[2]);

            return result;
        }

        private bool CanSkipSimulations()
        {
            if (_g.RoundNumber == 10)
            {
                return true;
            }

            var player2 = (PlayerIndex + 1) % Game.NumPlayers;
            var player3 = (PlayerIndex + 2) % Game.NumPlayers;

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

            var player1 = (PlayerIndex + 2) % Game.NumPlayers;
            var player3 = (PlayerIndex + 1) % Game.NumPlayers;
            var validCards = ValidCards(Hand, _g.trump, _g.GameType, TeamMateIndex, first);

            if (validCards.All(c => c.Value == Hodnota.Desitka || c.Value == Hodnota.Eso) ||
                validCards.All(c => c.Value != Hodnota.Desitka && c.Value != Hodnota.Eso))
            {
                //musim hrat A nebo X
                //nebo
                //nemam ani A ani X
                return true;
            }

            if (TeamMateIndex == player3 &&
                !first.IsHigherThan(validCards.First(), _g.trump) &&
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
            if (first == null)
            {
                return CanSkipSimulations();
            }
            if (second == null)
            {
                return CanSkipSimulations(first);
            }

            if (_g.RoundNumber == 10)
            {
                return true;
            }

            var player1 = (PlayerIndex + 1) % Game.NumPlayers;
            var player2 = (PlayerIndex + 2) % Game.NumPlayers;
            var validCards = ValidCards(Hand, _g.trump, _g.GameType, TeamMateIndex, first, second);
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

        private void RoundFinished(object sender, Round r)
        {
            Probabilities.UpdateProbabilities(r.number, r.player1.PlayerIndex, r.c1, r.c2, r.c3, r.hlas3);
        }
    }
}
