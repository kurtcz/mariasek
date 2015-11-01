﻿using System;
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
        private Barva? _trump;
        private List<Card> _talon;
        private Hand[] _hands = new Hand[Game.NumPlayers];
        private List<AddingMoneyCalculator> _moneyCalculations;
        private int _gamesBalance;
        private int _hundredsBalance;
        private int _hundredsAgainstBalance;
        private int _sevensBalance;
        private int _sevensAgainstBalance;
        private int _betlBalance;
        private int _durchBalance;
 
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
                RuleThreshold = 0.8f,
                GameThresholds = new [] { 0.7f, 0.8f, 0.9f }
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

            var gameThresholds = parameters["GameThreshold"].Value.Split('|');
            Settings.GameThresholds = gameThresholds.Select(i => int.Parse(i) / 100f).ToArray();
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

            _trump = card.Suit;
            _log.DebugFormat("Trump chosen: {0}", card);
            return card;
        }

        private List<Card> ChooseBetlTalon(List<Card> hand)
        {
            var holesByCard = hand.Select(i => {
                //pro kazdou kartu spocitej diry (mensi karty v barve ktere nemam)
                var holes = 0;

                foreach(var h in Enum.GetValues(typeof(Hodnota))
                                     .Cast<Hodnota>()
                                     .Where(h => i.IsHigherThan(new Card(i.Suit, h), null)))
                {
                    if(!hand.Any(j => j.Suit == i.Suit && j.Value == h))
                    {
                        holes++;
                    }
                }

                return new Tuple<Barva, Card, int>(i.Suit, i, holes);
            }).Where(i => i.Item3 > 0)
              .GroupBy(i => i.Item1);
            //radime podle poctu karet v barve vzestupne
            //a potom podle poctu der sestupne
            var talon = holesByCard.OrderBy(i => i.Count())
                                   .ThenByDescending(i => i.Max(j => j.Item3))
                                   .SelectMany(i => i.Select(j => j.Item2))
                                   .Take(2)
                                   .ToList();
            var count = talon.Count();
            
            //pokud je potreba, doplnime o nejake nizke karty (abych zhorsil talon na durcha)
            if(count < 2)
            {
                talon.AddRange(hand.OrderBy(i => i.Value).Take(2 - count));
            }

            return talon;
        }

        private List<Card> ChooseDurchTalon(List<Card> hand)
        {
            var holesByCard = hand.Select(i =>
            {
                //pro kazdou kartu spocitej diry (vetsi karty v barve ktere nemam)
                var holes = 0;

                foreach (var h in Enum.GetValues(typeof(Hodnota))
                                     .Cast<Hodnota>()
                                     .Where(h => i.IsLowerThan(new Card(i.Suit, h), null)))
                {
                    if (!hand.Any(j => j.Suit == i.Suit && j.Value == h))
                    {
                        holes++;
                    }
                }

                return new Tuple<Card, int>(i, holes);
            }).Where(i => i.Item2 > 0);

            var talon = holesByCard.OrderByDescending(i => i.Item2)
                                   .ThenBy(i => i.Item1.Value)
                                   .Select(i => i.Item1)
                                   .Take(2)
                                   .ToList();
            var count = talon.Count();

            //pokud je potreba, doplnime o nejake nizke karty
            if (count < 2)
            {
                talon.AddRange(hand.OrderBy(i => i.Value).Take(2 - count));
            }

            //mozna by stacilo negrupovat podle barev ale jen sestupne podle hodnot a vzit prvni dve?
            return talon;
        }

        private List<Card> ChooseNormalTalon(List<Card> hand)
        {
            //_trump = trumf ktery zacinajici hrac vybral pred vyberem typu hry
            //TODO: promyslet poradne jak na to (v kombinaci s hlasy apod.)
            var talon = new List<Card>();

            //nejdriv zkus vzit karty v barve kde krom esa nemam nic jineho (neber krale ani svrska)
            var b = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        .Where(barva => barva != _trump.Value &&
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
                        .Where(barva => barva != _trump.Value &&
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
                            .Where(barva => barva != _trump.Value &&
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

        public override List<Card> ChooseTalon()
        {
            //Sjedeme simulaci hry, betlu, durcha i normalni hry a vratit talon pro to nejlepsi. 
            //Zapamatujeme si vysledek a pouzijeme ho i v ChooseGameFlavour() a ChooseGameType()
            var bidding = new Bidding(_g);

            _talon = new List<Card>();
            RunGameSimulations(bidding, true, true);
            if (_durchBalance >= Settings.GameThresholds[0] * Settings.SimulationsPerRound)
            {
                _talon = ChooseDurchTalon(Hand);
            }
            else if (_betlBalance >= Settings.GameThresholds[0] * Settings.SimulationsPerRound)
            {
                _talon = ChooseBetlTalon(Hand);
            }
            else
            {
                _talon = ChooseNormalTalon(Hand);
            }

            _log.DebugFormat("Talon chosen: {0} {1}", _talon[0], _talon[1]);
            Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, _talon);
            Probabilities.UpdateProbabilitiesAfterTalon(_talon);

            return _talon;
        }

        public override GameFlavour ChooseGameFlavour()
        {
            if(_g.GameStartingPlayerIndex == PlayerIndex)
            {
                //simulace uz probehly v ChooseTalon()
            }
            else
            {
                var bidding = new Bidding(_g);

                RunGameSimulations(bidding, false, true);
            }

            if ((_durchBalance >= Settings.GameThresholds[0] * Settings.SimulationsPerRound) ||
                (_betlBalance >= Settings.GameThresholds[0] * Settings.SimulationsPerRound))
            {
                return GameFlavour.Bad;
            }

            return GameFlavour.Good;
        }

        private void UpdateGeneratedHandsByChoosingTalon(Func<List<Card>, List<Card>> chooseTalonFunc)
        {
            const int talonIndex = 3;

            //volicimu hraci dame i to co je v talonu, aby mohl vybrat skutecny talon
            _hands[_g.GameStartingPlayerIndex].AddRange(_hands[3]);

            var talon = chooseTalonFunc(_hands[_g.GameStartingPlayerIndex]);

            _hands[_g.GameStartingPlayerIndex].Remove(talon[0]);
            _hands[_g.GameStartingPlayerIndex].Remove(talon[1]);
            _hands[talonIndex] = new Hand(talon);
        }

        //vola se jak pro voliciho hrace tak pro oponenty 
        private void RunGameSimulations(Bidding bidding, bool simulateGoodGames, bool simulateBadGames)
        {
            var gameComputationResults = new List<GameComputationResult>();
            var durchComputationResults = new List<GameComputationResult>();
            var betlComputationResults = new List<GameComputationResult>();

            //pokud volim hru tak se ted rozhoduju jaky typ hry hrat (hra, betl, durch)
            //pokud nevolim hru, tak bud simuluju betl a durch nebo konkretni typ hry
            //tak ci tak nevim co je/bude v talonu

            _log.DebugFormat("Running game simulations for {0} ...", Name);
            if (simulateGoodGames)
            {
                //pokud volim hru tak uz znam trumfy, pokud nevolim hru tak uz je ted znam taky
                var probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _trump, _talon);

                //nasimuluj hry v barve
                for (int i = 0; i < Settings.SimulationsPerRound; i++)
                {
                    _hands = probabilities.GenerateHands(1, PlayerIndex);
                    UpdateGeneratedHandsByChoosingTalon(ChooseNormalTalon);

                    var gameComputationResult = ComputeGame(null, null, _trump ?? _g.trump, Hra.Sedma, 10, 1);

                    gameComputationResults.Add(gameComputationResult);                    
                }
            }
            if(simulateBadGames)
            {
                var probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), null, _talon);

                //nasimuluj durchy
                for (int i = 0; i < Settings.SimulationsPerRound; i++)
                {
                    _hands = probabilities.GenerateHands(1, PlayerIndex);
                    UpdateGeneratedHandsByChoosingTalon(ChooseDurchTalon);

                    var durchComputationResult = ComputeGame(null, null, null, Hra.Durch, 10, 1);

                    durchComputationResults.Add(durchComputationResult);
                }
                //nasimuluj betly
                probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), null, _talon);
                for (int i = 0; i < Settings.SimulationsPerRound; i++)
                {
                    _hands = probabilities.GenerateHands(1, PlayerIndex);
                    UpdateGeneratedHandsByChoosingTalon(ChooseBetlTalon);

                    var betlComputationResult = ComputeGame(null, null, null, Hra.Betl, 10, 1);

                    betlComputationResults.Add(betlComputationResult);
                }
            }

            //vyber vhodnou hru podle vysledku simulace
            var opponent = TeamMateIndex == (PlayerIndex + 1) % Game.NumPlayers
                ? (PlayerIndex + 2) % Game.NumPlayers : (PlayerIndex + 1) % Game.NumPlayers;
            _moneyCalculations = gameComputationResults.Select(i =>
            {
                var calc = new AddingMoneyCalculator(Hra.Sedma, _g.trump, _g.GameStartingPlayerIndex, bidding, i);

                calc.CalculateMoney();

                return calc;
            }).Union(durchComputationResults.Select(i =>
            {
                var calc = new AddingMoneyCalculator(Hra.Durch, null, _g.GameStartingPlayerIndex, bidding, i);

                calc.CalculateMoney();

                return calc;
            })).Union(betlComputationResults.Select(i =>
            {
                var calc = new AddingMoneyCalculator(Hra.Betl, null, _g.GameStartingPlayerIndex, bidding, i);

                calc.CalculateMoney();

                return calc;
            })).ToList();
            _gamesBalance = PlayerIndex == _g.GameStartingPlayerIndex
                            ? _moneyCalculations.Count(i => i.GameWon)
                            : _moneyCalculations.Count(i => !i.GameWon);
            _hundredsBalance = PlayerIndex == _g.GameStartingPlayerIndex
                                ? _moneyCalculations.Count(i => i.HundredWon)
                                : _moneyCalculations.Count(i => !i.HundredWon);
            _hundredsAgainstBalance = PlayerIndex == _g.GameStartingPlayerIndex
                                        ? _moneyCalculations.Count(i => i.QuietHundredAgainstWon)
                                        : _moneyCalculations.Count(i => !i.QuietHundredAgainstWon);
            _sevensBalance = PlayerIndex == _g.GameStartingPlayerIndex
                                ? _moneyCalculations.Count(i => i.SevenWon)
                                : _moneyCalculations.Count(i => !i.SevenWon);
            _sevensAgainstBalance = PlayerIndex == _g.GameStartingPlayerIndex
                                        ? _moneyCalculations.Count(i => i.SevenAgainstWon)
                                        : _moneyCalculations.Count(i => !i.SevenAgainstWon);
            _durchBalance = PlayerIndex == _g.GameStartingPlayerIndex
                                ? _moneyCalculations.Count(i => i.DurchWon)
                                : _moneyCalculations.Count(i => !i.DurchWon);
            _betlBalance = PlayerIndex == _g.GameStartingPlayerIndex
                                ? _moneyCalculations.Count(i => i.BetlWon)
                                : _moneyCalculations.Count(i => !i.BetlWon);
            _log.DebugFormat("** Game {0} by {1} {2} times ({3}%)", PlayerIndex == _g.GameStartingPlayerIndex ? "won" : "lost", _g.GameStartingPlayer.Name,
                _gamesBalance, 100 * _gamesBalance / Settings.SimulationsPerRound);
            _log.DebugFormat("** Hundred {0} by {1} {2} times ({3}%)", PlayerIndex == _g.GameStartingPlayerIndex ? "won" : "lost", _g.GameStartingPlayer.Name,
                _hundredsBalance, 100 * _hundredsBalance / Settings.SimulationsPerRound);            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Hundred against won {0} times ({1}%)",
                _hundredsAgainstBalance, 100f * _hundredsAgainstBalance / Settings.SimulationsPerRound);            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Seven {0} by {1} {2} times ({3}%)", PlayerIndex == _g.GameStartingPlayerIndex ? "won" : "lost", _g.GameStartingPlayer.Name,
                _sevensBalance, 100 * _sevensBalance / Settings.SimulationsPerRound);            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Seven against won {0} times ({1}%)",
                _sevensAgainstBalance, 100 * _sevensAgainstBalance / Settings.SimulationsPerRound);            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Durch won {0} times ({1}%)",
                _durchBalance, 100 * _durchBalance / Settings.SimulationsPerRound);            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Betl won {0} times ({1}%)",
                _betlBalance, 100 * _betlBalance / Settings.SimulationsPerRound);            //sgrupuj simulace podle vysledku skore
            var scores = _moneyCalculations.GroupBy(i => i.PointsWon)
                .Select(g => new
                {
                    Score = g.Key,
                    Items = g.ToList()
                });
            foreach (var score in scores)
            {
                _log.DebugFormat("simulated score: {0} pts {1} times ({2}%)", score.Score, score.Items.Count(), score.Items.Count() * 100 / scores.Sum(i => i.Items.Count()));
            }
        }

        //vola se z enginu
        public override Hra ChooseGameType(Hra minimalBid = Hra.Hra)
        {
            //TODO: urcit typ hry podle zisku ne podle pradepodobnosti
            Hra gameType;

            if (_durchBalance >= Settings.GameThresholds[0] * Settings.SimulationsPerRound)
            {
                gameType = Hra.Durch;
            }
            else if (_betlBalance >= Settings.GameThresholds[0] * Settings.SimulationsPerRound)
            {
                gameType = Hra.Betl;
            }
            else
            {
                gameType = _hundredsBalance >= Settings.GameThresholds[0] * Settings.SimulationsPerRound
                             ? Hra.Kilo : Hra.Hra;
                if (_sevensBalance >= Settings.GameThresholds[0] * Settings.SimulationsPerRound)
                {
                    gameType |= Hra.Sedma;
                };
            }
            _log.DebugFormat("Selected game type: {0}", gameType);

            return gameType;
        }

        private int _numberOfDoubles = 0;

        //V tehle funkci muzeme dat flek nebo hlasit protihru
        public override Hra GetBidsAndDoubles(Bidding bidding)
        {
            var gameThreshold = Settings.GameThresholds[Math.Min(Settings.GameThresholds.Length - 1, _numberOfDoubles++)] / 100f;

            if (_moneyCalculations == null)
            {
                if (bidding.BetlDurchMultiplier == 0)
                {
                    //mame flekovat hru
                    RunGameSimulations(bidding, true, false);
                }
                else
                {
                    //mame flekovat betl nebo durch
                    RunGameSimulations(bidding, false, true);
                }
            }            
            if (_gamesBalance / Settings.SimulationsPerRound >= gameThreshold)
            {
                return bidding.Bids & Hra.Hra;
            }
            if (_sevensBalance / Settings.SimulationsPerRound >= gameThreshold)
            {
                return bidding.Bids & Hra.Sedma;
            }
            if (_hundredsBalance / Settings.SimulationsPerRound >= gameThreshold)
            {
                return bidding.Bids & Hra.Kilo;
            }
            if (_sevensAgainstBalance / Settings.SimulationsPerRound >= gameThreshold)
            {
                if (_numberOfDoubles == 1 && PlayerIndex != _g.GameStartingPlayerIndex)
                {
                    //v prvnim kole muze souper zahlasit sedmu proti
                    return bidding.Bids | Hra.SedmaProti;
                }
                return bidding.Bids & Hra.SedmaProti;
            }
            if (_hundredsAgainstBalance / Settings.SimulationsPerRound >= gameThreshold)
            {
                if (_numberOfDoubles == 1 && PlayerIndex != _g.GameStartingPlayerIndex)
                {
                    //v prvnim kole muze souper zahlasit kilo proti
                    return bidding.Bids | Hra.KiloProti;
                }
                return bidding.Bids & Hra.KiloProti;
            }
            return 0;
        }

        public override void Init()
        {
            _trump = null;
            _talon = null;
            Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, _talon);
        }

        private void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            if (PlayerIndex != _g.GameStartingPlayerIndex)
            {
                Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, _talon);
            }
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
                Probabilities.UpdateProbabilities(r.number, r.player1.PlayerIndex, r.c1, r.hlas1);
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
                DebugInfo.Rule = computationResult.Rule.Description;
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

        private GameComputationResult InitGameComputationResult()
        {
            var result = new GameComputationResult
            {
                Hands = new Hand[Game.NumPlayers],
                Rounds = new List<RoundDebugContext>(),
                Score = new int[Game.NumPlayers],
                BasicScore = new int[Game.NumPlayers],
                MaxHlasScore = new int[Game.NumPlayers],
                Final7Won = null
            };

            for (var i = 0; i < Game.NumPlayers; i++ )
            {
                var h = new List<Card>();

                h.AddRange((List<Card>)_hands[i]);
                result.Hands[i] = new Hand(h);
            }

            for (var i = 0; i < Game.NumRounds && _g.rounds[i] != null; i++)
            {
                result.Score[_g.rounds[i].player1.PlayerIndex] += _g.rounds[i].points1;
                result.Score[_g.rounds[i].player2.PlayerIndex] += _g.rounds[i].points2;
                result.Score[_g.rounds[i].player3.PlayerIndex] += _g.rounds[i].points3;
                result.BasicScore[_g.rounds[i].player1.PlayerIndex] += _g.rounds[i].basicPoints1;
                result.BasicScore[_g.rounds[i].player2.PlayerIndex] += _g.rounds[i].basicPoints1;
                result.BasicScore[_g.rounds[i].player3.PlayerIndex] += _g.rounds[i].basicPoints1;
                var hlasScore1 = _g.rounds[i].hlas1
                                    ? (_g.rounds[i].c1.Suit == _g.trump.Value ? 40 : 20)
                                    : 0;
                var hlasScore2 = _g.rounds[i].hlas2
                                    ? (_g.rounds[i].c2.Suit == _g.trump.Value ? 40 : 20)
                                    : 0;
                var hlasScore3 = _g.rounds[i].hlas3
                                    ? (_g.rounds[i].c3.Suit == _g.trump.Value ? 40 : 20)
                                    : 0;
                result.MaxHlasScore[_g.rounds[i].player1.PlayerIndex] = Math.Max(hlasScore1, result.MaxHlasScore[_g.rounds[i].player1.PlayerIndex]);
                result.MaxHlasScore[_g.rounds[i].player2.PlayerIndex] = Math.Max(hlasScore2, result.MaxHlasScore[_g.rounds[i].player2.PlayerIndex]);
                result.MaxHlasScore[_g.rounds[i].player3.PlayerIndex] = Math.Max(hlasScore3, result.MaxHlasScore[_g.rounds[i].player3.PlayerIndex]);
            }

            return result;
        }

        private void AmendGameComputationResult(GameComputationResult result, int roundStarterIndex, int roundWinnerIndex, int roundScore, Card c1, Card c2, Card c3)
        {
            result.Score[roundWinnerIndex] += roundScore;
            result.BasicScore[roundWinnerIndex] += roundScore;
            if (c1.Value == Hodnota.Svrsek && _hands[roundStarterIndex].HasK(c1.Suit))
            {
                var hlas = _g.trump.HasValue && c1.Suit == _g.trump.Value ? 40 : 20;
                result.Score[roundStarterIndex] += hlas;
                result.MaxHlasScore[roundStarterIndex] = Math.Max(hlas, result.MaxHlasScore[roundStarterIndex]);
            }
            if (c2.Value == Hodnota.Svrsek && _hands[(roundStarterIndex + 1) % Game.NumPlayers].HasK(c2.Suit))
            {
                var hlas = _g.trump.HasValue && c2.Suit == _g.trump.Value ? 40 : 20;
                result.Score[(roundStarterIndex + 1) % Game.NumPlayers] += hlas;
                result.MaxHlasScore[(roundStarterIndex + 1) % Game.NumPlayers] = Math.Max(hlas, result.MaxHlasScore[(roundStarterIndex + 1) % Game.NumPlayers]);
            }
            if (c3.Value == Hodnota.Svrsek && _hands[(roundStarterIndex + 2) % Game.NumPlayers].HasK(c3.Suit))
            {
                var hlas = _g.trump.HasValue && c3.Suit == _g.trump.Value ? 40 : 20;
                result.Score[(roundStarterIndex + 2) % Game.NumPlayers] += hlas;
                result.MaxHlasScore[(roundStarterIndex + 2) % Game.NumPlayers] = Math.Max(hlas, result.MaxHlasScore[(roundStarterIndex + 2) % Game.NumPlayers]);
            }
            if (c1.Value == Hodnota.Kral && _hands[roundStarterIndex].HasQ(c1.Suit))
            {
                var hlas = _g.trump.HasValue && c1.Suit == _g.trump.Value ? 40 : 20;
                result.Score[roundStarterIndex] += hlas;
                result.MaxHlasScore[roundStarterIndex] = Math.Max(hlas, result.MaxHlasScore[roundStarterIndex]);
            }
            if (c2.Value == Hodnota.Kral && _hands[(roundStarterIndex + 1) % Game.NumPlayers].HasQ(c2.Suit))
            {
                var hlas = _g.trump.HasValue && c2.Suit == _g.trump.Value ? 40 : 20;
                result.Score[(roundStarterIndex + 1) % Game.NumPlayers] += hlas;
                result.MaxHlasScore[(roundStarterIndex + 1) % Game.NumPlayers] = Math.Max(hlas, result.MaxHlasScore[(roundStarterIndex + 1) % Game.NumPlayers]);
            }
            if (c3.Value == Hodnota.Kral && _hands[(roundStarterIndex + 2) % Game.NumPlayers].HasQ(c3.Suit))
            {
                var hlas = _g.trump.HasValue && c3.Suit == _g.trump.Value ? 40 : 20;
                result.Score[(roundStarterIndex + 2) % Game.NumPlayers] += hlas;
                result.MaxHlasScore[(roundStarterIndex + 2) % Game.NumPlayers] = Math.Max(hlas, result.MaxHlasScore[(roundStarterIndex + 2) % Game.NumPlayers]);
            }
        }

        private GameComputationResult ComputeGame(Card c1, Card c2, Barva? trump = null, Hra? gameType = null, int? roundsToCompute = null, int? initialRoundNumber = null)
        {
            var result = InitGameComputationResult();
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

            if(!gameType.HasValue || !initialRoundNumber.HasValue || !roundsToCompute.HasValue)
            {
                gameType = _g.GameType;
                initialRoundNumber = _g.RoundNumber;
                roundsToCompute = Settings.RoundsToCompute;
            }
            if(!trump.HasValue && (gameType & (Hra.Betl | Hra.Durch)) != 0)
            {
                trump = _g.trump;
            }
            //var aiStrategy = new AiStrategy(trump.HasValue ? trump.Value : _g.trump, gameType.HasValue ? gameType.Value : _g.GameType, _hands)
            //{
            //    MyIndex = PlayerIndex,
            //    MyName = Name,
            //    TeamMateIndex = TeamMateIndex,
            //    RoundNumber = initialRoundNumber.HasValue ? initialRoundNumber.Value : _g.RoundNumber
            //};
            var aiStrategy = AiStrategyFactory.GetAiStrategy(_g, gameType, trump, _hands, Name, PlayerIndex, TeamMateIndex, initialRoundNumber);
            
            _log.DebugFormat("Round {0}. Starting simulation for {1}", _g.RoundNumber, _g.players[PlayerIndex].Name);
            if (c1 != null) _log.DebugFormat("First card: {0}", c1);
            if (c2 != null) _log.DebugFormat("Second card: {0}", c2);
            _log.TraceFormat("{0}: {1} cerveny, {2} zeleny, {3} kule, {4} zaludy", _g.players[player2].Name, _hands[player2].Count(i => i.Suit == Barva.Cerveny), _hands[player2].Count(i => i.Suit == Barva.Zeleny), _hands[player2].Count(i => i.Suit == Barva.Kule), _hands[player2].Count(i => i.Suit == Barva.Zaludy));
            _log.TraceFormat("{0}: {1} cerveny, {2} zeleny, {3} kule, {4} zaludy", _g.players[player3].Name, _hands[player3].Count(i => i.Suit == Barva.Cerveny), _hands[player3].Count(i => i.Suit == Barva.Zeleny), _hands[player3].Count(i => i.Suit == Barva.Kule), _hands[player3].Count(i => i.Suit == Barva.Zaludy));
            for (initialRoundNumber = aiStrategy.RoundNumber;
                 aiStrategy.RoundNumber < initialRoundNumber + roundsToCompute;
                 aiStrategy.RoundNumber++)
            {
                if (aiStrategy.RoundNumber > 10) break;

                var roundStarterIndex = player1;
                AiRule r1 = null, r2 = null, r3 = null;

                if (!firstTime || c1 == null)
                {
                    c1 = aiStrategy.PlayCard1(out r1);

                    aiStrategy.MyIndex = player2;
                    //aiStrategy.TeamMateIndex = TeamMateIndex == player2 ? PlayerIndex : (TeamMateIndex == -1 ? player3 : -1);
                    aiStrategy.TeamMateIndex = aiStrategy.TeamMateIndex == player2 ? player1 : (aiStrategy.TeamMateIndex == -1 ? player3 : -1);
                    if (firstTime)
                    {
                        result.CardToPlay = c1;
                        result.Rule = r1;
                        firstTime = false;
                    }
                }
                if (!firstTime || c2 == null)
                {
                    c2 = aiStrategy.PlayCard2(c1, out r2);
                    aiStrategy.MyIndex = player3;
                    //aiStrategy.TeamMateIndex = TeamMateIndex == player3 ? PlayerIndex : (TeamMateIndex == -1 ? player2 : -1);
                    aiStrategy.TeamMateIndex = aiStrategy.TeamMateIndex == player3 ? player2 : (aiStrategy.TeamMateIndex == -1 ? player1 : -1);
                    if (firstTime)
                    {
                        result.CardToPlay = c2;
                        result.Rule = r2;
                        firstTime = false;
                    }
                }
                var c3 = aiStrategy.PlayCard3(c1, c2, out r3);

                if (c1 == null || c2 == null || c3 == null)
                    c3 = c3; //investigate
                var roundWinnerCard = Round.WinningCard(c1, c2, c3, trump);
                var roundWinnerIndex = roundWinnerCard == c1 ? roundStarterIndex : (roundWinnerCard == c2 ? (roundStarterIndex + 1) % Game.NumPlayers : (roundStarterIndex + 2) % Game.NumPlayers);
                var roundScore = Round.ComputePointsWon(c1, c2, c3, aiStrategy.RoundNumber);

                result.Rounds.Add(new RoundDebugContext
                {
                    RoundStarterIndex = roundStarterIndex,
                    c1 = c1,
                    c2 = c2,
                    c3 = c3,
                    r1 = r1 != null ? r1.Description : null,
                    r2 = r2 != null ? r2.Description : null,
                    r3 = r3 != null ? r3.Description : null,
                    RoundWinnerIndex = roundWinnerIndex
                });
                _log.TraceFormat("Simulation round {2} won by {0}. Points won: {1}", _g.players[roundWinnerIndex].Name, roundScore, aiStrategy.RoundNumber);
                if (firstTime)
                {
                    result.CardToPlay = c3;
                    result.Rule = r3;
                    firstTime = false;
                }
                if (aiStrategy.RoundNumber == 10 && trump.HasValue)
                {
                    result.Final7Won = roundWinnerCard.Suit == trump.Value && roundWinnerCard.Value == Hodnota.Sedma && roundWinnerIndex == _g.GameStartingPlayerIndex;
                }
                aiStrategy.MyIndex = roundWinnerIndex;
                aiStrategy.TeamMateIndex = _g.players[roundWinnerIndex].TeamMateIndex;
                player1 = aiStrategy.MyIndex;
                player2 = (aiStrategy.MyIndex + 1) % Game.NumPlayers;
                player3 = (aiStrategy.MyIndex + 2) % Game.NumPlayers;
                AmendGameComputationResult(result, roundStarterIndex, roundWinnerIndex, roundScore, c1, c2, c3);
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

            if(!_g.trump.HasValue)
            {
                return false;
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

            if(!_g.trump.HasValue)
            {
                return false;
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
                first.IsLowerThan(validCards.First(), _g.trump) &&
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
                        (Probabilities.SuitProbability(player3, _g.trump.Value, _g.RoundNumber) == 0f &&
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
                         Probabilities.SuitProbability(opponent, _g.trump.Value, _g.RoundNumber) == 0f &&
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

            if(!_g.trump.HasValue)
            {
                return false;
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
