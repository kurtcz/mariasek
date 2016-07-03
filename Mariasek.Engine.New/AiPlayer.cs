using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Mariasek.Engine.New.Logger;
using Mariasek.Engine.New.Configuration;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Mariasek.Engine.New
{
    public class AiPlayer : AbstractPlayer, IStatsPlayer
    {
#if !PORTABLE
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
#else
        private static readonly ILog _log = new DummyLogWrapper();
#endif   
        public Barva? _trump;
        private Hra? _gameType;
        public List<Card> _talon; //public so that HumanPlayer can set it
        private List<MultiplyingMoneyCalculator> _moneyCalculations;
        private int _gamesBalance;
        private int _hundredsBalance;
        private int _hundredsAgainstBalance;
        private int _sevensBalance;
        private int _sevensAgainstBalance;
        private int _betlBalance;
        private int _durchBalance;
        private bool _initialSimulation;
        private bool _teamMateDoubledGame;
        private bool _shouldMeasureThroughput;
 
        public Probability Probabilities { get; set; }
        public AiPlayerSettings Settings { get; set; }
        public bool UpdateProbabilitiesAfterTalon { get; set; }

        public Action ThrowIfCancellationRequested;

        public AiPlayer(Game g) : base(g)
        {
            Settings = new AiPlayerSettings
            {
                Cheat = false,
                RoundsToCompute = 1,
                CardSelectionStrategy = CardSelectionStrategy.MaxCount,
                SimulationsPerGameType = 100,
                MaxSimulationTimeMs = 3000,
                SimulationsPerRound = 250,
                RuleThreshold = 0.9f,
                RuleThresholdForGameType = new Dictionary<Hra, float> {{ Hra.Hra, 0.9f }, { Hra.Sedma, 0.9f }, { Hra.Kilo, 0.99f }, { Hra.Betl, 0.9f }, { Hra.Durch, 0.9f }},
                GameThresholds = new [] { 0.75f, 0.8f, 0.85f, 0.9f, 0.95f },
                GameThresholdsForGameType = new Dictionary<Hra, float[]>
                                            {
                                                { Hra.Hra,        new[] { 0.50f, 0.65f, 0.75f, 0.85f, 0.95f } },
                                                { Hra.Sedma,      new[] { 0.75f, 0.80f, 0.85f, 0.90f, 0.95f } },
                                                { Hra.SedmaProti, new[] { 0.75f, 0.80f, 0.85f, 0.90f, 0.95f } },
                                                { Hra.Kilo,       new[] { 0.80f, 0.85f, 0.90f, 0.95f, 0.99f } },
                                                { Hra.KiloProti,  new[] { 0.95f, 0.96f, 0.97f, 0.98f, 0.99f } },
                                                { Hra.Betl,       new[] { 0.75f, 0.80f, 0.85f, 0.90f, 0.95f } },
                                                { Hra.Durch,      new[] { 0.75f, 0.80f, 0.85f, 0.90f, 0.95f } }
                                            },
                MaxDoubleCount = 5,
                SigmaMultiplier = 0
            };
            UpdateProbabilitiesAfterTalon = true;
            _log.InfoFormat("AiPlayerSettings:\n{0}", Settings);

            DebugInfo = new PlayerDebugInfo();
            g.GameLoaded += GameLoaded;
            g.GameFlavourChosen += GameFlavourChosen;
            g.GameTypeChosen += GameTypeChosen;
            g.BidMade += BidMade;
            g.CardPlayed += CardPlayed;
            ThrowIfCancellationRequested = g.ThrowIfCancellationRequested;
        }

        public AiPlayer(Game g, ParameterConfigurationElementCollection parameters) : this(g)
        {
            Settings.Cheat = bool.Parse(parameters["AiCheating"].Value);
            Settings.RoundsToCompute = int.Parse(parameters["RoundsToCompute"].Value);
            Settings.CardSelectionStrategy = (CardSelectionStrategy)Enum.Parse(typeof(CardSelectionStrategy), parameters["CardSelectionStrategy"].Value);
            Settings.SimulationsPerGameType = int.Parse(parameters["SimulationsPerGameType"].Value);
            Settings.MaxSimulationTimeMs = int.Parse(parameters["MaxSimulationTimeMs"].Value);
            Settings.SimulationsPerGameTypePerSecond = int.Parse(parameters["SimulationsPerGameTypePerSecond"].Value);
            Settings.SimulationsPerRound = int.Parse(parameters["SimulationsPerRound"].Value);
            Settings.SimulationsPerRoundPerSecond = int.Parse(parameters["SimulationsPerRoundPerSecond"].Value);
            Settings.RuleThreshold = int.Parse(parameters["RuleThreshold"].Value) / 100f;
            Settings.RuleThresholdForGameType = new Dictionary<Hra, float>();
            Settings.RuleThresholdForGameType[Hra.Hra] = int.Parse(parameters["RuleThreshold.Hra"].Value ?? parameters["RuleThreshold"].Value) / 100f;
            Settings.RuleThresholdForGameType[Hra.Sedma] = int.Parse(parameters["RuleThreshold.Sedma"].Value ?? parameters["RuleThreshold"].Value) / 100f;
            Settings.RuleThresholdForGameType[Hra.Kilo] = int.Parse(parameters["RuleThreshold.Kilo"].Value ?? parameters["RuleThreshold"].Value) / 100f;
            Settings.RuleThresholdForGameType[Hra.Durch] = int.Parse(parameters["RuleThreshold.Durch"].Value ?? parameters["RuleThreshold"].Value) / 100f;
            Settings.RuleThresholdForGameType[Hra.Betl] = int.Parse(parameters["RuleThreshold.Betl"].Value ?? parameters["RuleThreshold"].Value) / 100f;
            var gameThresholds = parameters["GameThreshold"].Value.Split('|');
            Settings.GameThresholds = gameThresholds.Select(i => int.Parse(i) / 100f).ToArray();
            Settings.GameThresholdsForGameType = new Dictionary<Hra, float[]>();
            var gameThresholds2 = parameters["GameThreshold.Hra"].Value;
            Settings.GameThresholdsForGameType[Hra.Hra] = ((gameThresholds2 != null) ? gameThresholds2.Split('|') : gameThresholds).Select(i => int.Parse(i) / 100f).ToArray();
            gameThresholds2 = parameters["GameThreshold.Sedma"].Value;
            Settings.GameThresholdsForGameType[Hra.Sedma] = ((gameThresholds2 != null) ? gameThresholds2.Split('|') : gameThresholds).Select(i => int.Parse(i) / 100f).ToArray();
            gameThresholds2 = parameters["GameThreshold.SedmaProti"].Value;
            Settings.GameThresholdsForGameType[Hra.SedmaProti] = ((gameThresholds2 != null) ? gameThresholds2.Split('|') : gameThresholds).Select(i => int.Parse(i) / 100f).ToArray();
            gameThresholds2 = parameters["GameThreshold.Kilo"].Value;
            Settings.GameThresholdsForGameType[Hra.Kilo] = ((gameThresholds2 != null) ? gameThresholds2.Split('|') : gameThresholds).Select(i => int.Parse(i) / 100f).ToArray();
            gameThresholds2 = parameters["GameThreshold.KiloProti"].Value;
            Settings.GameThresholdsForGameType[Hra.KiloProti] = ((gameThresholds2 != null) ? gameThresholds2.Split('|') : gameThresholds).Select(i => int.Parse(i) / 100f).ToArray();
            gameThresholds2 = parameters["GameThreshold.Betl"].Value;
            Settings.GameThresholdsForGameType[Hra.Betl] = ((gameThresholds2 != null) ? gameThresholds2.Split('|') : gameThresholds).Select(i => int.Parse(i) / 100f).ToArray();
            gameThresholds2 = parameters["GameThreshold.Durch"].Value;
            Settings.GameThresholdsForGameType[Hra.Durch] = ((gameThresholds2 != null) ? gameThresholds2.Split('|') : gameThresholds).Select(i => int.Parse(i) / 100f).ToArray();
            Settings.MaxDoubleCount = int.Parse(parameters["MaxDoubleCount"].Value);
            Settings.SigmaMultiplier = int.Parse(parameters["SigmaMultiplier"].Value);

            //Settings.SimulationsPerGameType = Settings.SimulationsPerGameTypePerSecond * Settings.MaxSimulationTimeMs / 1000;
            //Settings.SimulationsPerRound = Settings.SimulationsPerRoundPerSecond * Settings.MaxSimulationTimeMs / 1000;
        }

        private int GetSuitScoreForTrumpChoice(List<Card> hand, Barva b)
        {
            var hand7 = hand.Take(7).ToList(); //nekoukat do karet co nejsou videt
            var score = 0;
            var count = hand7.Count(i => i.Suit == b);

            if (count > 1)
            {
                if (hand7.HasK(b))
                    score += 20;
                if (hand7.HasQ(b))
                    score += 20;
                if (hand7.HasA(b))
                    score += 10;
                if (hand7.HasX(b))
                    score += 10;

                score += count;
            }
            _log.DebugFormat("Trump score for {0}: {1}", b, score);

            return score;
        }

        public override Card ChooseTrump()
        {
            return ChooseTrump(Hand);
        }

        public Card ChooseTrump(List<Card> hand)
        {
            var scores = Enum.GetValues(typeof(Barva)).Cast<Barva>().Select(barva => new
            {
                Suit = barva,
                Score = GetSuitScoreForTrumpChoice(hand, barva)
            });

            //vezmi barvu s nejvetsim skore, pokud je skore shodne tak vezmi nejdelsi barvu
            var trump = scores.OrderByDescending(i => i.Score)
                              .Select(i => i.Suit)
                              .First();

            //vyber jednu z karet v barve (nejdriv zkus neukazovat zadne dulezite karty, pokud to nejde vezmi libovolnou kartu v barve)
            var card = hand.FirstOrDefault(i => i.Suit == trump && i.Value > Hodnota.Sedma && i.Value < Hodnota.Svrsek) ??
                       hand.OrderBy(i => i.Value).FirstOrDefault(i => i.Suit == trump);

            _trump = card.Suit;
            _log.DebugFormat("Trump chosen: {0}", card);
            return card;
        }

        private List<Card> ChooseBetlTalon(List<Card> hand, Barva? trump)
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
                                   .OrderByDescending(i => i)
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

        private List<Card> ChooseDurchTalon(List<Card> hand, Barva? trump)
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

        private List<Card> ChooseNormalTalon(List<Card> hand, Barva? trump)
        {
            var talon = new List<Card>();

            //nejdriv zkus vzit karty v barve kde krom esa nemam nic jineho (neber krale ani svrska)
            var b = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        .Where(barva => barva != trump.Value &&
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
                        .Where(barva => barva != trump.Value &&
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
                            .Where(barva => barva != trump.Value &&
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
            //zacinajici hrac nejprve vybira talon a az pak rozhoduje jakou hru bude hrat (my mame oboje implementovane uvnitr ChooseGameFlavour())
            if (PlayerIndex == _g.OriginalGameStartingPlayerIndex)
            {
                ChooseGameFlavour();
            }
            else
            {
                //protihrac nejdriv sjede simulaci nanecisto (bez talonu) a potom znovu s kartami talonu a vybere novy talon
                if (_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][0] * Settings.SimulationsPerGameType)
                {
                    _talon = ChooseDurchTalon(Hand, null);
                }
                else
                {
                    _talon = ChooseBetlTalon(Hand, null);
                }
            }
            _log.DebugFormat("Talon chosen: {0} {1}", _talon[0], _talon[1]);
            
            return _talon;
        }

        public override GameFlavour ChooseGameFlavour()
        {
            if (_initialSimulation)
            {
                var bidding = new Bidding(_g);

                //pokud volim hru tak mam 12 karet a nechci generovat talon,
                //jinak mam 10 karet a talon si necham nagenerovat a potom ho vymenim za talon zvoleny podle logiky
                if (_talon == null) //pokud je AiPlayer poradce cloveka, tak uz je _talon definovanej
                {
                    _talon = PlayerIndex == _g.GameStartingPlayerIndex ? new List<Card>() : null;
                }
                Probabilities = new Probability(PlayerIndex, PlayerIndex, new Hand(Hand), null, _talon);

                if (PlayerIndex == _g.OriginalGameStartingPlayerIndex)
                {
                    //Sjedeme simulaci hry, betlu, durcha i normalni hry a vratit talon pro to nejlepsi. 
                    //Zapamatujeme si vysledek a pouzijeme ho i v ChooseGameFlavour() a ChooseGameType()
                    RunGameSimulations(bidding, PlayerIndex, true, true);
                    if (_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][0] * Settings.SimulationsPerGameType)
                    {
                        _talon = ChooseDurchTalon(Hand, null);
						DebugInfo.RuleCount = _durchBalance;
                    }
                    else if (_betlBalance >= Settings.GameThresholdsForGameType[Hra.Betl][0] * Settings.SimulationsPerGameType)
                    {
                        _talon = ChooseBetlTalon(Hand, null);
						DebugInfo.RuleCount = _betlBalance;
                    }
                    else
                    {
                        _talon = ChooseNormalTalon(Hand, _trump);
						DebugInfo.RuleCount = Settings.SimulationsPerGameType - Math.Max(_durchBalance, _betlBalance);
                    }
                    if (UpdateProbabilitiesAfterTalon)
                    {
                        Probabilities.UpdateProbabilitiesAfterTalon(Hand, _talon);
                    }
                }
                else
                {
                    RunGameSimulations(bidding, PlayerIndex, false, true);
                }
                _initialSimulation = false;
            }
            _moneyCalculations = null; //abychom v GetBidsAndDoubles znovu sjeli simulaci normalni hry

			DebugInfo.TotalRuleCount = Settings.SimulationsPerGameType;
            //byla uz zavolena nejaka hra?
            if (_gameType == Hra.Durch)
            {
				DebugInfo.RuleCount = _durchBalance;
                return GameFlavour.Good;
            }
            else if (_gameType == Hra.Betl)
            {
                if (_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][0] * Settings.SimulationsPerGameType)
                {
					DebugInfo.RuleCount = _durchBalance;
                    return GameFlavour.Bad;
                }
				DebugInfo.RuleCount = _betlBalance;
                return GameFlavour.Good;
            }
            else
            {
                if ((_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][0] * Settings.SimulationsPerGameType) ||
                    (_betlBalance >= Settings.GameThresholdsForGameType[Hra.Betl][0] * Settings.SimulationsPerGameType))
                {
					DebugInfo.RuleCount = Math.Max(_durchBalance, _betlBalance);
                    return GameFlavour.Bad;
                }
				DebugInfo.RuleCount = Settings.SimulationsPerGameType - Math.Max(_durchBalance, _betlBalance);
                return GameFlavour.Good;
            }
        }

        private void UpdateGeneratedHandsByChoosingTalon(Hand[] hands, Func<List<Card>, Barva?, List<Card>> chooseTalonFunc, int GameStartingPlayerIndex, Barva? trump = null)
        {
            const int talonIndex = 3;

            //volicimu hraci dame i to co je v talonu, aby mohl vybrat skutecny talon
            hands[GameStartingPlayerIndex].AddRange(hands[3]);

            var talon = chooseTalonFunc(hands[GameStartingPlayerIndex], trump);

            hands[GameStartingPlayerIndex].RemoveAll(i => talon.Contains(i));
            hands[talonIndex] = new Hand(talon);
        }

        private void UpdateGeneratedHandsByChoosingTrumpAndTalon(Hand[] hands, Func<List<Card>, Barva?, List<Card>> chooseTalonFunc, int GameStartingPlayerIndex)
        {
            const int talonIndex = 3;

            //volicimu hraci dame i to co je v talonu, aby mohl vybrat skutecny talon
            hands[GameStartingPlayerIndex].AddRange(hands[3]);

            var trump = ChooseTrump(hands[GameStartingPlayerIndex]).Suit;
            var talon = chooseTalonFunc(hands[GameStartingPlayerIndex], trump);

            hands[GameStartingPlayerIndex].RemoveAll(i => talon.Contains(i));
            hands[talonIndex] = new Hand(talon);
        }

        private Hand[] GetPlayersHandsAndTalon()
        {

            var hands = new List<Hand>(_g.players.Select(i => new Hand(i.Hand)));

            hands.Add(new Hand(_g.talon));

            return hands.ToArray();
        }

        //vola se jak pro voliciho hrace tak pro oponenty 
        private void RunGameSimulations(Bidding bidding, int GameStartingPlayerIndex, bool simulateGoodGames, bool simulateBadGames)
        {
            var gameComputationResults = new ConcurrentQueue<GameComputationResult>();
            var durchComputationResults = new ConcurrentQueue<GameComputationResult>();
            var betlComputationResults = new ConcurrentQueue<GameComputationResult>();
            var totalGameSimulations = (simulateGoodGames ? Settings.SimulationsPerGameType : 0) +
                                       (simulateBadGames ? 2 * Settings.SimulationsPerGameType : 0);
            var progress = 0;
            var goodGamesPerSecond = 0;
            var badGamesPerSecond = 0;
            OnGameComputationProgress(new GameComputationProgressEventArgs { Current = progress, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0, Message = "Generuju karty"});

            //nasimuluj hry v barve
            var source = Settings.Cheat
                ? new [] { GetPlayersHandsAndTalon() }
                : null;
            var tempSource = new ConcurrentQueue<Hand[]>();

            //pokud volim hru tak se ted rozhoduju jaky typ hry hrat (hra, betl, durch)
            //pokud nevolim hru, tak bud simuluju betl a durch nebo konkretni typ hry
            //tak ci tak nevim co je/bude v talonu
            _log.DebugFormat("Running game simulations for {0} ...", Name);
            if (simulateGoodGames)
            {
                var start = DateTime.Now;
                var actualSimulations = 0;
                var prematureEnd = false;

                Parallel.ForEach(source ?? Probabilities.GenerateHands(1, PlayerIndex, Settings.SimulationsPerGameType), (hh, loopState) =>
                {                        
                    ThrowIfCancellationRequested();
                    if (source == null)
                    {
                        tempSource.Enqueue(hh);
                    }
                    if((DateTime.Now - start).TotalMilliseconds > Settings.MaxSimulationTimeMs)
                    {
                        prematureEnd = true;
                        loopState.Stop();
                    }
                    Interlocked.Increment(ref actualSimulations);
                    var hands = new Hand[Game.NumPlayers + 1];
                    for(var i = 0; i < hands.Length; i++)
                    {
                        hands[i] = new Hand(new List<Card>((List<Card>)hh[i]));   //naklonuj karty aby v pristich simulacich nebyl problem s talonem
                    }
                    if (PlayerIndex != GameStartingPlayerIndex)
                    {
                        UpdateGeneratedHandsByChoosingTalon(hands, ChooseNormalTalon, GameStartingPlayerIndex, _trump);
                    }

                    // to ?? vypada chybne
                    var gameComputationResult = ComputeGame(hands, null, null, _trump ?? _g.trump, _gameType != null ? (_gameType | Hra.SedmaProti) : Hra.Sedma, 10, 1); 
                    gameComputationResults.Enqueue(gameComputationResult);
                    
                    var val = Interlocked.Increment(ref progress);
                        OnGameComputationProgress(new GameComputationProgressEventArgs { Current = val, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0, Message = "Simuluju hru"});
                });
                var end = DateTime.Now;
                Settings.SimulationsPerGameType = actualSimulations;
                Settings.SimulationsPerGameTypePerSecond = (int)((float)actualSimulations / Settings.MaxSimulationTimeMs * 1000);
                //Settings.SimulationsPerGameTypePerSecond = (int)((float)actualSimulations / (end - start).TotalMilliseconds * 1000);
                totalGameSimulations = (simulateGoodGames ? Settings.SimulationsPerGameType : 0) +
                    (simulateBadGames ? 2 * Settings.SimulationsPerGameType : 0);
                if (source == null)
                {
                    source = tempSource.ToArray();
                }
            }
            if (simulateBadGames)
            {
                var initialProgress = progress;
                var start = DateTime.Now;
                var actualSimulations = 0;
                var prematureEnd = false;

                Parallel.ForEach(source ?? Probabilities.GenerateHands(1, PlayerIndex, Settings.SimulationsPerGameType), (hh, loopState) =>
                {
                    ThrowIfCancellationRequested();
                    if (source == null)
                    {
                        tempSource.Enqueue(hh);
                    }
                    if((DateTime.Now - start).TotalMilliseconds > Settings.MaxSimulationTimeMs)
                    {
                        prematureEnd = true;
                        loopState.Stop();
                    }
                    Interlocked.Increment(ref actualSimulations);

                    var hands = new Hand[Game.NumPlayers + 1];
                    for(var i = 0; i < hh.Length; i++)
                    {
                        hands[i] = new Hand(new List<Card>((List<Card>)hh[i]));   //naklonuj karty aby v pristich simulacich nebyl problem s talonem
                    }
                    UpdateGeneratedHandsByChoosingTrumpAndTalon(hands, ChooseNormalTalon, _g.GameStartingPlayerIndex);
                    UpdateGeneratedHandsByChoosingTalon(hands, ChooseBetlTalon, GameStartingPlayerIndex);

                    var betlComputationResult = ComputeGame(hands, null, null, null, Hra.Betl, 10, 1, true);
                    betlComputationResults.Enqueue(betlComputationResult);
                    
                    var val = Interlocked.Increment(ref progress);
                    OnGameComputationProgress(new GameComputationProgressEventArgs { Current = val, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0, Message = "Simuluju betl"});
                });
                var end = DateTime.Now;
                Settings.SimulationsPerGameType = actualSimulations;
                Settings.SimulationsPerGameTypePerSecond = (int)((float)actualSimulations / Settings.MaxSimulationTimeMs * 1000);
                //Settings.SimulationsPerGameTypePerSecond = (int)((float)actualSimulations / (end - start).TotalMilliseconds * 1000);
                totalGameSimulations = (simulateGoodGames ? Settings.SimulationsPerGameType : 0) +
                    (simulateBadGames ? 2 * Settings.SimulationsPerGameType : 0);
                if (source == null)
                {
                    source = tempSource.ToArray();
                }
                Parallel.ForEach(source ?? Probabilities.GenerateHands(1, PlayerIndex, Settings.SimulationsPerGameType), (hands, loopState) =>
                {
                    ThrowIfCancellationRequested();
                    if (source == null)
                    {
                        tempSource.Enqueue(hands);
                    }
                    //nasimuluj ze volici hrac vybral trumfy a/nebo talon
                    if (_g.GameType == Hra.Betl)
                    {
                        UpdateGeneratedHandsByChoosingTalon(hands, ChooseBetlTalon, _g.GameStartingPlayerIndex);
                    }
                    else
                    {
                        UpdateGeneratedHandsByChoosingTrumpAndTalon(hands, ChooseNormalTalon, _g.GameStartingPlayerIndex);
                    }
                    UpdateGeneratedHandsByChoosingTalon(hands, ChooseDurchTalon, GameStartingPlayerIndex);

                    var durchComputationResult = ComputeGame(hands, null, null, null, Hra.Durch, 10, 1, true);
                    durchComputationResults.Enqueue(durchComputationResult);

                    var val = Interlocked.Increment(ref progress);
                    OnGameComputationProgress(new GameComputationProgressEventArgs { Current = val, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0, Message = "Simuluju durch"});

                    if (NoChanceToWinDurch(PlayerIndex, hands))
                    {
                        OnGameComputationProgress(new GameComputationProgressEventArgs { Current = initialProgress + Settings.SimulationsPerGameType, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0, Message = "Neuhratelnej durch"});
                        loopState.Stop();
                    }
                });
            }

            //vyber vhodnou hru podle vysledku simulace
            var opponent = TeamMateIndex == (PlayerIndex + 1) % Game.NumPlayers
                ? (PlayerIndex + 2) % Game.NumPlayers : (PlayerIndex + 1) % Game.NumPlayers;
            _moneyCalculations = gameComputationResults.Select(i =>
            {
                var calc = new MultiplyingMoneyCalculator(Hra.Sedma, _trump ?? _g.trump, GameStartingPlayerIndex, bidding, i);

                calc.CalculateMoney();

                return calc;
            }).Union(durchComputationResults.Select(i =>
            {
                var calc = new MultiplyingMoneyCalculator(Hra.Durch, null, GameStartingPlayerIndex, bidding, i);

                calc.CalculateMoney();

                return calc;
            })).Union(betlComputationResults.Select(i =>
            {
                var calc = new MultiplyingMoneyCalculator(Hra.Betl, null, GameStartingPlayerIndex, bidding, i);

                calc.CalculateMoney();

                return calc;
            })).ToList();
            _gamesBalance = PlayerIndex == GameStartingPlayerIndex
                            ? _moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => i.GameWon)
                            : _moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => !i.GameWon);
            _hundredsBalance = PlayerIndex == GameStartingPlayerIndex
                                ? _moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => i.HundredWon)
                                : _moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => !i.HundredWon);
            _hundredsAgainstBalance = PlayerIndex == GameStartingPlayerIndex
                                        ? _moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => !i.QuietHundredAgainstWon)
                                        : _moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => i.QuietHundredAgainstWon);
            _sevensBalance = PlayerIndex == GameStartingPlayerIndex
                                ? _moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => i.SevenWon)
                                : _moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => !i.SevenWon);
            _sevensAgainstBalance = PlayerIndex == GameStartingPlayerIndex
                                        ? _moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => !i.SevenAgainstWon)
                                        : _moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => i.SevenAgainstWon);
            _durchBalance = PlayerIndex == GameStartingPlayerIndex
                                ? _moneyCalculations.Where(i => (i.GameType & Hra.Durch) != 0).Count(i => i.DurchWon)
                                : _moneyCalculations.Where(i => (i.GameType & Hra.Durch) != 0).Count(i => !i.DurchWon);
            _betlBalance = PlayerIndex == GameStartingPlayerIndex
                                ? _moneyCalculations.Where(i => (i.GameType & Hra.Betl) != 0).Count(i => i.BetlWon)
                                : _moneyCalculations.Where(i => (i.GameType & Hra.Betl) != 0).Count(i => !i.BetlWon);
            _log.DebugFormat("** Game {0} by {1} {2} times ({3}%)", PlayerIndex == GameStartingPlayerIndex ? "won" : "lost", _g.GameStartingPlayer.Name,
                _gamesBalance, 100 * _gamesBalance / Settings.SimulationsPerGameType);
            _log.DebugFormat("** Hundred {0} by {1} {2} times ({3}%)", PlayerIndex == GameStartingPlayerIndex ? "won" : "lost", _g.GameStartingPlayer.Name,
                _hundredsBalance, 100 * _hundredsBalance / Settings.SimulationsPerGameType);            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Hundred against won {0} times ({1}%)",
                _hundredsAgainstBalance, 100f * _hundredsAgainstBalance / Settings.SimulationsPerGameType);            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Seven {0} by {1} {2} times ({3}%)", PlayerIndex == GameStartingPlayerIndex ? "won" : "lost", _g.GameStartingPlayer.Name,
                _sevensBalance, 100 * _sevensBalance / Settings.SimulationsPerGameType);            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Seven against won {0} times ({1}%)",
                _sevensAgainstBalance, 100 * _sevensAgainstBalance / Settings.SimulationsPerGameType);            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Durch won {0} times ({1}%)",
                _durchBalance, 100 * _durchBalance / Settings.SimulationsPerGameType);            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Betl won {0} times ({1}%)",
                _betlBalance, 100 * _betlBalance / Settings.SimulationsPerGameType);            //sgrupuj simulace podle vysledku skore
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
        public override Hra ChooseGameType(Hra validGameTypes)
        {
            //TODO: urcit typ hry podle zisku ne podle pradepodobnosti
            Hra gameType;

            if ((validGameTypes & (Hra.Betl | Hra.Durch)) != 0)
            {
                if (_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][0] * Settings.SimulationsPerGameType)
                {
                    gameType = Hra.Durch;
                    DebugInfo.RuleCount = _durchBalance;
                }
                else //if (_betlBalance >= Settings.GameThresholds[0] * Settings.SimulationsPerGameType)
                {
                    gameType = Hra.Betl;
                    DebugInfo.RuleCount = _betlBalance;
                }
            }
            else
            {
                if (_hundredsBalance >= Settings.GameThresholdsForGameType[Hra.Kilo][0] * Settings.SimulationsPerGameType)
                {
                    gameType = Hra.Kilo;
                    DebugInfo.RuleCount = _hundredsBalance;
                }
                else
                {
                    gameType = Hra.Hra;
                    DebugInfo.RuleCount = _gamesBalance;
                }
                if (_sevensBalance >= Settings.GameThresholdsForGameType[Hra.Sedma][0] * Settings.SimulationsPerGameType)
                {
                    gameType |= Hra.Sedma;
                };
            }
            DebugInfo.TotalRuleCount = Settings.SimulationsPerGameType;
            DebugInfo.Rule = (gameType & (Hra.Betl | Hra.Durch)) == 0 ? string.Format("{0} {1}", gameType, _trump) : gameType.ToString();
            var allChoices = new List<RuleDebugInfo> ();
            allChoices.Add(new RuleDebugInfo
            {
                Rule = string.Format("{0} {1}", Hra.Hra, _trump),
                RuleCount = _gamesBalance,
                TotalRuleCount = Settings.SimulationsPerGameType
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = string.Format("{0} {1}", Hra.Hra | Hra.Sedma, _trump),
                RuleCount = _sevensBalance,
                TotalRuleCount = Settings.SimulationsPerGameType
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = string.Format("{0} {1}", Hra.Kilo, _trump),
                RuleCount = _hundredsBalance,
                TotalRuleCount = Settings.SimulationsPerGameType
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Betl.ToString(),
                RuleCount = _betlBalance,
                TotalRuleCount = Settings.SimulationsPerGameType
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Durch.ToString(),
                RuleCount = _durchBalance,
                TotalRuleCount = Settings.SimulationsPerGameType
            });
            DebugInfo.AllChoices = allChoices.OrderByDescending(i => i.RuleCount).ToArray();
            _log.DebugFormat("Selected game type: {0}", gameType);

            return gameType;
        }

        //V tehle funkci muzeme dat flek nebo hlasit protihru
        public override Hra GetBidsAndDoubles(Bidding bidding)
        {
            Hra bid = 0;
            var gameThreshold = bidding._gameFlek < Settings.GameThresholdsForGameType[Hra.Hra].Length ? Settings.GameThresholdsForGameType[Hra.Hra][bidding._gameFlek] : 1f;
            var sevenThreshold = bidding._sevenFlek < Settings.GameThresholdsForGameType[Hra.Sedma].Length ? Settings.GameThresholdsForGameType[Hra.Sedma][bidding._sevenFlek] : 1f;
            var hundredThreshold = bidding._gameFlek < Settings.GameThresholdsForGameType[Hra.Kilo].Length ? Settings.GameThresholdsForGameType[Hra.Kilo][bidding._gameFlek] : 1f;
            var sevenAgainstThreshold = bidding._sevenAgainstFlek < Settings.GameThresholdsForGameType[Hra.SedmaProti].Length ? Settings.GameThresholdsForGameType[Hra.SedmaProti][bidding._sevenAgainstFlek] : 1f;
            var hundredAgainstThreshold = bidding._hundredAgainstFlek < Settings.GameThresholdsForGameType[Hra.KiloProti].Length ? Settings.GameThresholdsForGameType[Hra.KiloProti][bidding._hundredAgainstFlek] : 1f;
            var betlThreshold = bidding._betlDurchFlek < Settings.GameThresholdsForGameType[Hra.Betl].Length ? Settings.GameThresholdsForGameType[Hra.Betl][bidding._betlDurchFlek] : 1f;
            var durchThreshold = bidding._betlDurchFlek < Settings.GameThresholdsForGameType[Hra.Durch].Length ? Settings.GameThresholdsForGameType[Hra.Durch][bidding._betlDurchFlek] : 1f;
			var minRuleCount = int.MaxValue;

            if (bidding.MaxDoubleCount > Settings.MaxDoubleCount)
            {
                //uz stacilo
                return bid;
            }
            if (_moneyCalculations == null)
            {
                if (bidding.BetlDurchMultiplier == 0)
                {
                    //mame flekovat hru
                    //kilo simulovat nema cenu, hrac ho asi ma, takze flekovat stejne nebudeme
                    if (_g.GameType != Hra.Kilo)
                    {
                        RunGameSimulations(bidding, _g.GameStartingPlayerIndex, true, false);
                    }
                }
                else
                {
                    //mame flekovat betl nebo durch
                    //nema smysl, nic o souperovych kartach nevime
                    //RunGameSimulations(bidding, _g.GameStartingPlayerIndex, false, true);
                }
            }
            //Flekovani se u hry posuzuje podle pravdepodobnosti (musi byt vyssi nez prah) pokud trham (flek) nebo kolega flekoval (tutti a vys),
            //ostatni flekujeme pouze pokud zvolenou hru volici hrac nemuze uhrat
            if (_gamesBalance / (float)Settings.SimulationsPerGameType >= gameThreshold && _g.trump.HasValue &&
                (Hand.HasK(_g.trump.Value) || Hand.HasQ(_g.trump.Value) || _teamMateDoubledGame))
            {
                bid |= bidding.Bids & Hra.Hra;
                minRuleCount = Math.Min(minRuleCount, _gamesBalance);
            }
            //sedmu flekuju jen pokud jsem volil sam sedmu a v simulacich jsem ji uhral dost casto
            //nebo pokud jsem nevolil a v simulacich ani jednou nevysla
            if ((PlayerIndex == _g.GameStartingPlayerIndex && _sevensBalance / (float)Settings.SimulationsPerGameType >= sevenThreshold) ||
                (PlayerIndex != _g.GameStartingPlayerIndex && _sevensBalance == Settings.SimulationsPerGameType))
            {
                bid |=bidding.Bids & Hra.Sedma;
                minRuleCount = Math.Min(minRuleCount, _sevensBalance);
            }
            //kilo flekuju jen pokud jsem volil sam kilo a v simulacich jsem ho uhral dost casto
            //nebo pokud jsem nevolil a je nemozne aby mel volici hrac kilo (nema hlas)
            //?! Pokud bych chtel simulovat sance na to, ze volici hrac hlasene kilo neuhraje, tak musim nejak generovat "karty na kilo" (aspon 1 hlas) a ne nahodne karty
            if ((PlayerIndex == _g.GameStartingPlayerIndex && _hundredsBalance / (float)Settings.SimulationsPerGameType >= gameThreshold) ||
                (PlayerIndex != _g.GameStartingPlayerIndex && Probabilities.HlasProbability(_g.GameStartingPlayerIndex) == 0))
            {
                bid |= bidding.Bids & Hra.Kilo;
                minRuleCount = Math.Min(minRuleCount, _hundredsBalance);
            }
            //sedmu proti flekuju jen pokud jsem hlasil sam sedmu proti a v simulacich jsem ji uhral dost casto
            //nebo pokud jsem volil trumf a v simulacich ani jednou nevysla
            //?! Pokud bych chtel simulovat sance na to, ze volici hrac hlasenou sedmu neuhraje, tak musim nejak generovat "karty na sedmu" (aspon 4-5 trumfu) a ne nahodne karty
            if ((PlayerIndex != _g.GameStartingPlayerIndex && _sevensAgainstBalance / (float)Settings.SimulationsPerGameType >= sevenAgainstThreshold) ||
                (PlayerIndex == _g.GameStartingPlayerIndex && _sevensAgainstBalance == Settings.SimulationsPerGameType))
            {
                //if (_numberOfDoubles == 1 && PlayerIndex != _g.GameStartingPlayerIndex)
                //{
                //    //v prvnim kole muze souper zahlasit sedmu proti
                //    bid |= bidding.Bids | Hra.SedmaProti;
                //}
                bid |= bidding.Bids & Hra.SedmaProti;
                minRuleCount = Math.Min(minRuleCount, _sevensAgainstBalance);
            }
            //kilo proti flekuju jen pokud jsem hlasil sam kilo proti a v simulacich jsem ho uhral dost casto
            //nebo pokud jsem volil trumf a je nemozne aby meli protihraci kilo (nemaji hlas)
            if ((PlayerIndex != _g.GameStartingPlayerIndex && _hundredsAgainstBalance / (float)Settings.SimulationsPerGameType >= hundredAgainstThreshold) ||
                (PlayerIndex == _g.GameStartingPlayerIndex && //_hundredsAgainstBalance == Settings.SimulationsPerGameType))); //never monte carlu, dej na pravdepodobnost
                                                              (Probabilities.HlasProbability((PlayerIndex + 1) % Game.NumPlayers) == 0) &&
                                                              (Probabilities.HlasProbability((PlayerIndex + 2) % Game.NumPlayers) == 0)))
            {
                //if (_numberOfDoubles == 1 && PlayerIndex != _g.GameStartingPlayerIndex)
                //{
                //    //v prvnim kole muze souper zahlasit kilo proti
                //    bid |= bidding.Bids | Hra.KiloProti;
                //}
                bid |= bidding.Bids & Hra.KiloProti;
                bid &= (Hra)~Hra.Hra; //u kila proti uz nehlasime flek na hru
                minRuleCount = Math.Min(minRuleCount, _hundredsAgainstBalance);
            }
            //durch flekuju jen pokud jsem volil sam durch a v simulacich jsem ho uhral dost casto
            //nebo pokud jsem nevolil a nejde teoreticky uhrat            
            if ((PlayerIndex == _g.GameStartingPlayerIndex && _durchBalance / (float)Settings.SimulationsPerGameType >= durchThreshold) ||
                (PlayerIndex != _g.GameStartingPlayerIndex && Hand.Count(i => i.Value == Hodnota.Eso) == 4))
            {
                bid |= bidding.Bids & Hra.Durch;
                minRuleCount = Math.Min(minRuleCount, _durchBalance);
            }
            //betla flekuju jen pokud jsem volil sam betla a v simulacich jsem ho uhral dost casto
            if ((PlayerIndex == _g.GameStartingPlayerIndex && _betlBalance / (float)Settings.SimulationsPerGameType >= betlThreshold))
            {
                bid |= bidding.Bids & Hra.Betl;
                minRuleCount = Math.Min(minRuleCount, _betlBalance);
            }
            DebugInfo.Rule = bid.ToString();
			//DebugInfo.RuleCount = minRuleCount; //tohle je spatne. je treba brat v potaz jen balance ktere jsou v bidding.Bids popr. ty co skoncily flekem
			DebugInfo.RuleCount = 0;
            DebugInfo.TotalRuleCount = Settings.SimulationsPerGameType;
            var allChoices = new List<RuleDebugInfo>();
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Hra.ToString(),
                RuleCount = _gamesBalance,
                TotalRuleCount = Settings.SimulationsPerGameType
            });
            if ((bid & Hra.Hra) != 0)
            {
                DebugInfo.RuleCount = _gamesBalance;
            }
            allChoices.Add(new RuleDebugInfo
            {
                Rule = (Hra.Hra | Hra.Sedma).ToString(),
                RuleCount = _sevensBalance,
                TotalRuleCount = Settings.SimulationsPerGameType
            });
            if ((bid & Hra.Sedma) != 0)
            {
                DebugInfo.RuleCount = _sevensBalance;
            }
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Kilo.ToString(),
                RuleCount = _hundredsBalance,
                TotalRuleCount = Settings.SimulationsPerGameType
            });
            if ((bid & Hra.Kilo) != 0)
            {
                DebugInfo.RuleCount = _hundredsBalance;
            }
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Betl.ToString(),
                RuleCount = _betlBalance,
                TotalRuleCount = Settings.SimulationsPerGameType
            });
            if ((bid & Hra.Betl) != 0)
            {
                DebugInfo.RuleCount = _betlBalance;
            }
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Durch.ToString(),
                RuleCount = _durchBalance,
                TotalRuleCount = Settings.SimulationsPerGameType
            });
            if ((bid & Hra.Durch) != 0)
            {
                DebugInfo.RuleCount = _durchBalance;
            }
            DebugInfo.AllChoices = allChoices.OrderByDescending(i => i.RuleCount).ToArray();

            return bid;
        }

        public override void Init()
        {
            _trump = null;
            _talon = null;
            _gameType = null;
            Probabilities = null;
            _initialSimulation = true;
            _teamMateDoubledGame = false;
            _shouldMeasureThroughput = true;
            Settings.SimulationsPerRoundPerSecond = 0;
        }

        public void GameLoaded(object sender)
        {
            if(PlayerIndex == _g.GameStartingPlayerIndex)
            {
                _talon = _g.talon;
            }
            Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, _talon);
        }

        private void GameFlavourChosen(object sender, GameFlavourChosenEventArgs e)
        {
            //pokud nekdo hlasi spatnou barvu vymaz svuj talon (uz neni relevantni)
            if (e.Player.PlayerIndex != PlayerIndex && e.Flavour == GameFlavour.Bad)
            {
                _talon = null;
            }
            if (Probabilities != null) //Probabilities == null kdyz jsem nezacinal, tudiz netusim co je v talonu a nemusim nic upravovat
            {
                Probabilities.UpdateProbabilitiesAfterGameFlavourChosen(e);
            }
        }

        private void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            _trump = _g.trump;
            _gameType = _g.GameType;
            if (PlayerIndex != _g.GameStartingPlayerIndex || Probabilities == null) //Probabilities == null by nemelo nastat, ale ...
            {
                Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, _talon);
            }
            Probabilities.UpdateProbabilitiesAfterGameTypeChosen(e);
        }


        private void BidMade (object sender, BidEventArgs e)
        {
            //spoluhrac flekoval hru nebo kilo
            if (e.Player.PlayerIndex == TeamMateIndex && (e.BidMade & (Hra.Hra | Hra.Kilo)) != 0)
            {
                _teamMateDoubledGame = true;
            }
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
            var cardScores = new ConcurrentDictionary<Card, ConcurrentQueue<GameComputationResult>>();

            if (Settings.Cheat)
            {
                var hands = _g.players.Select(i => new Hand(i.Hand)).ToArray();
                var computationResult = ComputeGame(hands, r.c1, r.c2);

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
                var simulations = (int)Math.Min(Settings.SimulationsPerRound,
                    Math.Max(Probabilities.PossibleCombinations((PlayerIndex + 1) % Game.NumPlayers, r.number),
                             Probabilities.PossibleCombinations((PlayerIndex + 2) % Game.NumPlayers, r.number)));
                OnGameComputationProgress(new GameComputationProgressEventArgs { Current = 0, Max = Settings.SimulationsPerRoundPerSecond > 0 ? simulations : 0, Message = "Generuju karty"});
                var source = Probabilities.GenerateHands(_g.RoundNumber, roundStarterIndex, simulations);
                var progress = 0;
                var start = DateTime.Now;
                var prematureEnd = false;

                Parallel.ForEach(source, (hands, loopState) =>
                {
                    ThrowIfCancellationRequested();
                    if ((DateTime.Now - start).TotalMilliseconds > Settings.MaxSimulationTimeMs)
                    {
                        prematureEnd = true;
                        loopState.Stop();
                    }
                    var computationResult = ComputeGame(hands, r.c1, r.c2);

                    if (!cardScores.TryAdd(computationResult.CardToPlay, new ConcurrentQueue<GameComputationResult>(new[] { computationResult })))
                    {
                        cardScores[computationResult.CardToPlay].Enqueue(computationResult);
                    }
                    
                    var val = Interlocked.Increment(ref progress);
                        OnGameComputationProgress(new GameComputationProgressEventArgs { Current = val, Max = Settings.SimulationsPerRoundPerSecond > 0 ? simulations : 0, Message = "Simuluju hru"});

                    if (computationResult.Rule == AiRule.PlayTheOnlyValidCard || canSkipSimulations)    //We have only one card to play, so there is really no need to compute anything
                    {
                        OnGameComputationProgress(new GameComputationProgressEventArgs { Current = simulations, Max = Settings.SimulationsPerRoundPerSecond > 0 ? simulations : 0});
                        loopState.Stop();
                    }
                });
                if (_shouldMeasureThroughput) // only do this 1st time when we calculate most to get a more realistic benchmark
                {
                    var end = DateTime.Now;
                    //Settings.SimulationsPerRound = progress;
                    Settings.SimulationsPerRoundPerSecond = (int)((float)progress / Settings.MaxSimulationTimeMs * 1000);
                    //Settings.SimulationsPerRoundPerSecond = (int)((float)progress / (end - start).TotalMilliseconds * 1000);
                    _shouldMeasureThroughput = false;
                }
                if (canSkipSimulations)
                {
                    _log.InfoFormat("Other simulations have been skipped");
                }
                cardToPlay = ChooseCardToPlay(cardScores.ToDictionary(k => k.Key, v => new List<GameComputationResult>(v.Value)));
            }

            _log.InfoFormat("{0} plays card: {1} - {2}", Name, cardToPlay, DebugInfo.Rule);
            return cardToPlay;                
        }

        private Card ChooseCardToPlay(Dictionary<Card, List<GameComputationResult>> cardScores)
        {
            Card cardToPlay = null;

            //!!! v cardRules jsou ruly mockrat
            //spravne mam mit pro kazdou kartu max. tolik rulu kolik je simulaci
            var cardRules = new Dictionary<Card, List<AiRule>>();
            var cards = cardScores.Values.SelectMany(i => i).SelectMany(i => i.ToplevelRuleDictionary.Values).Distinct();
            foreach (var card in cards)
            {
                var rules = cardScores.Values.SelectMany(i => i)                                    //pro vsechny karty vezmi v kazde simulaci jedno pravidlo pro tuto kartu
                                             .Select(i => i.ToplevelRuleDictionary
                                                               .Where(j => j.Value == card)
                                                               .OrderByDescending(j => j.Key.Order)
                                                               .Select(j => j.Key)
                                                               .FirstOrDefault())
                                             .Where(i => i != null)
                                             .ToList();
                cardRules.Add(card, rules);
            }
            cardScores.ToDictionary(k => k.Key, v => cardScores.Values.SelectMany(i => 
                                                            i.Select(j => 
                                                                j.ToplevelRuleDictionary.Where(k => k.Value == v.Key)
                                                                    .Select(k => k.Key).FirstOrDefault()))
                                                                    .Where(k => k != null).Distinct().ToList());
            _log.InfoFormat("Cards to choose from: {0}", cardRules.Count);
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

                _log.InfoFormat("max. score: {0}/{1}/{2} avg. score {3:0}/{4:0}/{5:0}",                    
                    maxScores.Score[0], maxScores.Score[1], maxScores.Score[2],
                    cardScore.Value.Average(i => i.Score[0]), cardScore.Value.Average(i => i.Score[1]),
                    cardScore.Value.Average(i => i.Score[2]));
            }
            foreach(var cardRule in cardRules)
            {
                var count = cardRule.Value.Count();
                _log.InfoFormat("{0}: {1} times ({2:0}%)", cardRule.Key, count, count / (float)totalCount * 100);
            }
            _log.InfoFormat("Deviation: {0}", Probabilities.Deviation * 100);

            KeyValuePair<Card, List<AiRule>> kv;
            KeyValuePair<Card, List<GameComputationResult>> kvp;
            KeyValuePair<Card, GameComputationResult> kvp2;
            bool ignoreThreshold = (_g.GameType & Hra.Kilo) != 0 && TeamMateIndex != -1;    //pokud hraju proti kilu, tak ryskuju cokoli abych snizil ztraty
            var sigma = Probabilities.Deviation; //1sigma ~ 67%, 2sigma ~ 95%, 3sigma ~ 99.7%

            switch (Settings.CardSelectionStrategy)
            {
                case CardSelectionStrategy.MaxCount:
                    var countOfBestRuleWithThreshold = cardRules.Select(i => i.Value.Count(j => j.UseThreshold)).OrderByDescending(i => i).FirstOrDefault();
                    var threshold = GetRuleThreshold();

                    _log.DebugFormat("Threshold value: {0}", Settings.RuleThreshold);
                    _log.DebugFormat("Count of best rule with a threshold: {0}", countOfBestRuleWithThreshold);
                    _log.DebugFormat("Ignoring threshold: {0}", ignoreThreshold);
                    if (ignoreThreshold || (countOfBestRuleWithThreshold / (float) totalCount) - Settings.SigmaMultiplier * sigma > threshold)
                    {
                        var bestRuleWithThreshold = cardRules.Where(i => i.Value.Any(j => j.UseThreshold))
                                                                .OrderByDescending(i => i.Value.Count).FirstOrDefault();

                        cardToPlay = bestRuleWithThreshold.Key;
                        if (cardToPlay != null)
                        {
                            DebugInfo.Card = bestRuleWithThreshold.Key;
                            DebugInfo.Rule = bestRuleWithThreshold.Value.First().Description;
                            DebugInfo.RuleCount = bestRuleWithThreshold.Value.Count;
                            DebugInfo.TotalRuleCount = cardScores.Sum(i => i.Value.Count); //tohle by se melo rovnat poctu simulaci
                            DebugInfo.AllChoices = cardRules.Where(i => i.Value.Any(j => j.UseThreshold))
                                                                .OrderByDescending(i => i.Value.Count).Select(i => new RuleDebugInfo
                                                                {
                                                                    Card = i.Key,
                                                                    Rule = string.Format("{0}: {1}", i.Key, i.Value.First().Description),
                                                                    RuleCount = i.Value.Count,
                                                                    TotalRuleCount = DebugInfo.TotalRuleCount
                                                                }).ToArray();
                        }
                    }
                    if (cardToPlay == null)
                    {
                        _log.DebugFormat("Threshold condition has not been met");
                        kv = cardRules.OrderByDescending(i => i.Value.Count(j => !j.UseThreshold)).FirstOrDefault();
                        cardToPlay = kv.Key;
                        if (cardToPlay != null)
                        {
                            DebugInfo.Card = kv.Key;
                            DebugInfo.Rule = kv.Value.First().Description;
                            DebugInfo.RuleCount = kv.Value.Count;
                            DebugInfo.TotalRuleCount = cardScores.Sum(i => i.Value.Count); //tohle by se melo rovnat poctu simulaci
                            DebugInfo.AllChoices = cardRules.OrderByDescending(i => i.Value.Count(j => !j.UseThreshold)).Select(i => new RuleDebugInfo
                            {
                                Card = i.Key,
                                Rule = string.Format("{0}: {1}", i.Key, i.Value.First().Description),
                                RuleCount = i.Value.Count,
                                TotalRuleCount = DebugInfo.TotalRuleCount
                            }).ToArray();
                        }
                    }
                    if (cardToPlay == null)
                    {
                        _log.DebugFormat("No rule without a threshold found");
                        kv = cardRules.OrderByDescending(i => i.Value.Count).First();
                        cardToPlay = kv.Key;
                        DebugInfo.Card = kv.Key;
                        DebugInfo.Rule = kv.Value.First().Description;
                        DebugInfo.RuleCount = kv.Value.Count;
                        DebugInfo.TotalRuleCount = cardScores.Sum(i => i.Value.Count); //tohle by se melo rovnat poctu simulaci
                        DebugInfo.AllChoices = cardRules.OrderByDescending(i => i.Value.Count).Select(i => new RuleDebugInfo
                        {
                            Card = i.Key,
                            Rule = string.Format("{0}: {1}", i.Key, i.Value.First().Description),
                            RuleCount = i.Value.Count,
                            TotalRuleCount = DebugInfo.TotalRuleCount
                        }).ToArray();
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
                    DebugInfo.Card = cardToPlay;
                    DebugInfo.Rule = kvp2.Value.Rule.Description;
                    DebugInfo.RuleCount = cardScores.Count(i => i.Key == cardToPlay);
                    DebugInfo.TotalRuleCount = cardScores.Sum(i => i.Value.Count);
                    DebugInfo.AllChoices = cardScores.OrderByDescending(i => i.Value.Count).Select(i => new RuleDebugInfo
                    {
                        Card = i.Key,
                        Rule = string.Format("{0}: {1}", i.Value.First().CardToPlay, i.Value.First().Rule.Description),
                        RuleCount = i.Value.Count,
                        TotalRuleCount = DebugInfo.TotalRuleCount
                    }).ToArray();
                    break;
                case CardSelectionStrategy.AverageScore:
                    kvp =
                        cardScores.OrderByDescending(
                            i => i.Value.Average(j => TeamMateIndex == -1
                                ? j.Score[PlayerIndex]
                                : j.Score[PlayerIndex] + j.Score[TeamMateIndex])).First();
                    cardToPlay = kvp.Key;
                    DebugInfo.Card = cardToPlay;
                    DebugInfo.Rule = kvp.Value.First().Rule.Description;
                    DebugInfo.RuleCount = kvp.Value.Count;
                    DebugInfo.TotalRuleCount = cardScores.Sum(i => i.Value.Count);
                    DebugInfo.AllChoices = cardScores.OrderByDescending(i => i.Value.Count).Select(i => new RuleDebugInfo
                    {
                        Card = i.Key,
                        Rule = string.Format("{0}: {1}", i.Value.First().CardToPlay, i.Value.First().Rule.Description),
                        RuleCount = i.Value.Count,
                        TotalRuleCount = DebugInfo.TotalRuleCount
                    }).ToArray();
                    break;
                default:
                    throw new Exception("Unknown card selection strategy");
            }
            return cardToPlay;
        }

        private float GetRuleThreshold()
        {
            foreach (var gameType in Settings.RuleThresholdForGameType.Keys)
            {
                if((gameType & _g.GameType) != 0)
                {
                    return Settings.RuleThresholdForGameType[gameType];
                }
            }

            return Settings.RuleThreshold;
        }

        private GameComputationResult InitGameComputationResult(Hand[] hands)
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

                h.AddRange((List<Card>)hands[i]);
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
                result.MaxHlasScore[_g.rounds[i].player1.PlayerIndex] = Math.Max(_g.rounds[i].hlasPoints1, result.MaxHlasScore[_g.rounds[i].player1.PlayerIndex]);
                result.MaxHlasScore[_g.rounds[i].player2.PlayerIndex] = Math.Max(_g.rounds[i].hlasPoints2, result.MaxHlasScore[_g.rounds[i].player2.PlayerIndex]);
                result.MaxHlasScore[_g.rounds[i].player3.PlayerIndex] = Math.Max(_g.rounds[i].hlasPoints3, result.MaxHlasScore[_g.rounds[i].player3.PlayerIndex]);
            }

            return result;
        }

        private void AmendGameComputationResult(GameComputationResult result, int roundStarterIndex, int roundWinnerIndex, int roundScore, Hand[] hands, Card c1, Card c2, Card c3)
        {
            result.Score[roundWinnerIndex] += roundScore;
            result.BasicScore[roundWinnerIndex] += roundScore;
            if (c1.Value == Hodnota.Svrsek && hands[roundStarterIndex].HasK(c1.Suit))
            {
                var hlas = _g.trump.HasValue && c1.Suit == _g.trump.Value ? 40 : 20;
                result.Score[roundStarterIndex] += hlas;
                result.MaxHlasScore[roundStarterIndex] = Math.Max(hlas, result.MaxHlasScore[roundStarterIndex]);
            }
            if (c2.Value == Hodnota.Svrsek && hands[(roundStarterIndex + 1) % Game.NumPlayers].HasK(c2.Suit))
            {
                var hlas = _g.trump.HasValue && c2.Suit == _g.trump.Value ? 40 : 20;
                result.Score[(roundStarterIndex + 1) % Game.NumPlayers] += hlas;
                result.MaxHlasScore[(roundStarterIndex + 1) % Game.NumPlayers] = Math.Max(hlas, result.MaxHlasScore[(roundStarterIndex + 1) % Game.NumPlayers]);
            }
            if (c3.Value == Hodnota.Svrsek && hands[(roundStarterIndex + 2) % Game.NumPlayers].HasK(c3.Suit))
            {
                var hlas = _g.trump.HasValue && c3.Suit == _g.trump.Value ? 40 : 20;
                result.Score[(roundStarterIndex + 2) % Game.NumPlayers] += hlas;
                result.MaxHlasScore[(roundStarterIndex + 2) % Game.NumPlayers] = Math.Max(hlas, result.MaxHlasScore[(roundStarterIndex + 2) % Game.NumPlayers]);
            }
            if (c1.Value == Hodnota.Kral && hands[roundStarterIndex].HasQ(c1.Suit))
            {
                var hlas = _g.trump.HasValue && c1.Suit == _g.trump.Value ? 40 : 20;
                result.Score[roundStarterIndex] += hlas;
                result.MaxHlasScore[roundStarterIndex] = Math.Max(hlas, result.MaxHlasScore[roundStarterIndex]);
            }
            if (c2.Value == Hodnota.Kral && hands[(roundStarterIndex + 1) % Game.NumPlayers].HasQ(c2.Suit))
            {
                var hlas = _g.trump.HasValue && c2.Suit == _g.trump.Value ? 40 : 20;
                result.Score[(roundStarterIndex + 1) % Game.NumPlayers] += hlas;
                result.MaxHlasScore[(roundStarterIndex + 1) % Game.NumPlayers] = Math.Max(hlas, result.MaxHlasScore[(roundStarterIndex + 1) % Game.NumPlayers]);
            }
            if (c3.Value == Hodnota.Kral && hands[(roundStarterIndex + 2) % Game.NumPlayers].HasQ(c3.Suit))
            {
                var hlas = _g.trump.HasValue && c3.Suit == _g.trump.Value ? 40 : 20;
                result.Score[(roundStarterIndex + 2) % Game.NumPlayers] += hlas;
                result.MaxHlasScore[(roundStarterIndex + 2) % Game.NumPlayers] = Math.Max(hlas, result.MaxHlasScore[(roundStarterIndex + 2) % Game.NumPlayers]);
            }
        }

        private bool NoChanceToWinDurch(int gameStarterIndex, Hand[] hands)
        {
            var player2 = (gameStarterIndex + 1) % Game.NumPlayers;
            var player3 = (gameStarterIndex + 2) % Game.NumPlayers;

            return hands[player2].Any(i => hands[gameStarterIndex].All(j => !j.IsHigherThan(i, null))) ||
                   hands[player3].Any(i => hands[gameStarterIndex].All(j => !j.IsHigherThan(i, null)));
        }

        private GameComputationResult ComputeGame(Hand[] hands, Card c1, Card c2, Barva? trump = null, Hra? gameType = null, int? roundsToCompute = null, int? initialRoundNumber = null, bool ImpersonateGameStartingPlayer = false)
        {
            var result = InitGameComputationResult(hands);
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
            if(!trump.HasValue && (gameType & (Hra.Betl | Hra.Durch)) == 0)
            {
                trump = _g.trump;
            }
            var aiStrategy = AiStrategyFactory.GetAiStrategy(_g, gameType, trump, hands, Name, PlayerIndex, ImpersonateGameStartingPlayer ? -1 : TeamMateIndex, initialRoundNumber);
            
            _log.DebugFormat("Round {0}. Starting simulation for {1}", _g.RoundNumber, _g.players[PlayerIndex].Name);
            if (c1 != null) _log.DebugFormat("First card: {0}", c1);
            if (c2 != null) _log.DebugFormat("Second card: {0}", c2);
            _log.TraceFormat("{0}: {1} cerveny, {2} zeleny, {3} kule, {4} zaludy", _g.players[player2].Name, hands[player2].Count(i => i.Suit == Barva.Cerveny), hands[player2].Count(i => i.Suit == Barva.Zeleny), hands[player2].Count(i => i.Suit == Barva.Kule), hands[player2].Count(i => i.Suit == Barva.Zaludy));
            _log.TraceFormat("{0}: {1} cerveny, {2} zeleny, {3} kule, {4} zaludy", _g.players[player3].Name, hands[player3].Count(i => i.Suit == Barva.Cerveny), hands[player3].Count(i => i.Suit == Barva.Zeleny), hands[player3].Count(i => i.Suit == Barva.Kule), hands[player3].Count(i => i.Suit == Barva.Zaludy));
            for (initialRoundNumber = aiStrategy.RoundNumber;
                 aiStrategy.RoundNumber < initialRoundNumber + roundsToCompute;
                 aiStrategy.RoundNumber++)
            {
                if (aiStrategy.RoundNumber > 10) break;

                var roundStarterIndex = player1;
                AiRule r1 = null, r2 = null, r3 = null;
                Dictionary<AiRule, Card> ruleDictionary;

                if (!firstTime || c1 == null)
                {
                    ruleDictionary = aiStrategy.GetApplicableRules();

                    r1 = ruleDictionary.Keys.OrderBy(i => i.Order).FirstOrDefault();
                    c1 = ruleDictionary.OrderBy(i => i.Key.Order).Select(i => i.Value).FirstOrDefault();

                    aiStrategy.MyIndex = player2;
                    aiStrategy.TeamMateIndex = aiStrategy.TeamMateIndex == player2 ? player1 : (aiStrategy.TeamMateIndex == -1 ? player3 : -1);
                    if (firstTime)
                    {
                        result.CardToPlay = c1;
                        result.Rule = r1;
                        result.ToplevelRuleDictionary = ruleDictionary;
                        firstTime = false;
                    }
                }
                if (!firstTime || c2 == null)
                {
                    ruleDictionary = aiStrategy.GetApplicableRules2(c1);

                    r2 = ruleDictionary.Keys.OrderBy(i => i.Order).FirstOrDefault();
                    c2 = ruleDictionary.OrderBy(i => i.Key.Order).Select(i => i.Value).FirstOrDefault();

                    aiStrategy.MyIndex = player3;
                    aiStrategy.TeamMateIndex = aiStrategy.TeamMateIndex == player3 ? player2 : (aiStrategy.TeamMateIndex == -1 ? player1 : -1);
                    if (firstTime)
                    {
                        result.CardToPlay = c2;
                        result.Rule = r2;
                        result.ToplevelRuleDictionary = ruleDictionary;
                        firstTime = false;
                    }
                }
                ruleDictionary = aiStrategy.GetApplicableRules3(c1, c2);

                r3 = ruleDictionary.Keys.OrderBy(i => i.Order).FirstOrDefault();
                var c3 = ruleDictionary.OrderBy(i => i.Key.Order).Select(i => i.Value).FirstOrDefault();

                //if (c1 == null || c2 == null || c3 == null)
                //    c3 = c3; //investigate
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
                    result.ToplevelRuleDictionary = ruleDictionary;
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
                AmendGameComputationResult(result, roundStarterIndex, roundWinnerIndex, roundScore, hands, c1, c2, c3);
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
