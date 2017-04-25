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
        private List<AddingMoneyCalculator> _moneyCalculations;
        private int _gamesBalance;
        private int _hundredsBalance;
        private int _hundredsAgainstBalance;
        private int _sevensBalance;
        private int _sevensAgainstBalance;
        private int _betlBalance;
        private int _durchBalance;
        private int _goodSimulations;
        private int _betlSimulations;
        private int _durchSimulations;
        private bool _initialSimulation;
        private bool _teamMateDoubledGame;
        private bool _shouldMeasureThroughput;
        private List<Barva> _teamMatesSuits;

        private ParallelOptions options = new ParallelOptions
        {
            //MaxDegreeOfParallelism = 1  //Uncomment before debugging
            //MaxDegreeOfParallelism = Environment.ProcessorCount * 8
        };
		public bool AdvisorMode { get; set; }
		private Card _trumpCard;
		public Card TrumpCard
		{
			get { return _trumpCard; }
			set { _trumpCard = value; _trump = value != null ? (Barva?)value.Suit : null; }
		}
		public StringBuilder _debugString { get; set; }
        public Probability Probabilities { get; set; }
        public AiPlayerSettings Settings { get; set; }
        
        public Action ThrowIfCancellationRequested;

        public AiPlayer(Game g) : base(g)
        {
            Settings = new AiPlayerSettings
            {
                Cheat = false,
                RoundsToCompute = 1,
                CardSelectionStrategy = CardSelectionStrategy.MaxCount,
                SimulationsPerGameType = 100,
                MaxSimulationTimeMs = 2000,
                SimulationsPerRound = 250,
                RuleThreshold = 0.95f,
                RuleThresholdForGameType = new Dictionary<Hra, float> {{ Hra.Kilo, 0.99f }},
                GameThresholds = new [] { 0.75f, 0.8f, 0.85f, 0.9f, 0.95f },
                GameThresholdsForGameType = new Dictionary<Hra, float[]>
                                            {
                                                { Hra.Hra,        new[] { 0.00f, 0.50f, 0.65f, 0.80f, 0.95f } },
                                                { Hra.Sedma,      new[] { 0.75f, 0.80f, 0.85f, 0.90f, 0.95f } },
                                                { Hra.SedmaProti, new[] { 0.75f, 0.80f, 0.85f, 0.90f, 0.95f } },
                                                { Hra.Kilo,       new[] { 0.80f, 0.85f, 0.90f, 0.95f, 0.99f } },
                                                { Hra.KiloProti,  new[] { 0.95f, 0.96f, 0.97f, 0.98f, 0.99f } },
                                                { Hra.Betl,       new[] { 0.75f, 0.80f, 0.85f, 0.90f, 0.95f } },
                                                { Hra.Durch,      new[] { 0.80f, 0.85f, 0.90f, 0.95f, 0.99f } }
                                            },
                MaxDoubleCountForGameType = new Dictionary<Hra, int>
                                            {
                                                { Hra.Hra,        3 },
                                                { Hra.Sedma,      3 },
                                                { Hra.SedmaProti, 2 },
                                                { Hra.Kilo,       3 },
                                                { Hra.KiloProti,  0 },
                                                { Hra.Betl,       3 },
                                                { Hra.Durch,      3 }
                                            },
                CanPlayGameType = new Dictionary<Hra, bool>
                                            {
                                                { Hra.Hra,        true },
                                                { Hra.Sedma,      true },
                                                { Hra.SedmaProti, true },
                                                { Hra.Kilo,       true },
                                                { Hra.KiloProti,  false },
                                                { Hra.Betl,       true },
                                                { Hra.Durch,      true }
                                            },
                SigmaMultiplier = 0,
				GameFlavourSelectionStrategy = GameFlavourSelectionStrategy.Standard
            };
            _log.InfoFormat("AiPlayerSettings:\n{0}", Settings);

			_debugString = g.DebugString;
            _teamMatesSuits = new List<Barva>();
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
            //Settings.MaxDoubleCount = int.Parse(parameters["MaxDoubleCount"].Value);
            Settings.MaxDoubleCountForGameType = new Dictionary<Hra, int>();
            Settings.MaxDoubleCountForGameType[Hra.Hra] = int.Parse(parameters["MaxDoubleCount.Hra"].Value);
            Settings.MaxDoubleCountForGameType[Hra.Sedma] = int.Parse(parameters["MaxDoubleCount.Sedma"].Value);
            Settings.MaxDoubleCountForGameType[Hra.Kilo] = int.Parse(parameters["MaxDoubleCount.Kilo"].Value);
            Settings.MaxDoubleCountForGameType[Hra.SedmaProti] = int.Parse(parameters["MaxDoubleCount.SedmaProti"].Value);
            Settings.MaxDoubleCountForGameType[Hra.KiloProti] = int.Parse(parameters["MaxDoubleCount.KiloProti"].Value);
            Settings.MaxDoubleCountForGameType[Hra.Betl] = int.Parse(parameters["MaxDoubleCount.Betl"].Value);
            Settings.MaxDoubleCountForGameType[Hra.Durch] = int.Parse(parameters["MaxDoubleCount.Durch"].Value);
            Settings.CanPlayGameType = new Dictionary<Hra, bool>();
            Settings.CanPlayGameType[Hra.Hra] = bool.Parse(parameters["CanPlay.Hra"].Value);
            Settings.CanPlayGameType[Hra.Sedma] = bool.Parse(parameters["CanPlay.Sedma"].Value);
            Settings.CanPlayGameType[Hra.Kilo] = bool.Parse(parameters["CanPlay.Kilo"].Value);
            Settings.CanPlayGameType[Hra.SedmaProti] = bool.Parse(parameters["CanPlay.SedmaProti"].Value);
            Settings.CanPlayGameType[Hra.KiloProti] = bool.Parse(parameters["CanPlay.KiloProti"].Value);
            Settings.CanPlayGameType[Hra.Betl] = bool.Parse(parameters["CanPlay.Betl"].Value);
            Settings.CanPlayGameType[Hra.Durch] = bool.Parse(parameters["CanPlay.Durch"].Value);
            Settings.SigmaMultiplier = int.Parse(parameters["SigmaMultiplier"].Value);
			Settings.GameFlavourSelectionStrategy = (GameFlavourSelectionStrategy)Enum.Parse(typeof(GameFlavourSelectionStrategy), parameters["GameFlavourSelectionStrategy"].Value);
            _teamMatesSuits = new List<Barva>();
            //Settings.SimulationsPerGameType = Settings.SimulationsPerGameTypePerSecond * Settings.MaxSimulationTimeMs / 1000;
            //Settings.SimulationsPerRound = Settings.SimulationsPerRoundPerSecond * Settings.MaxSimulationTimeMs / 1000;
        }

        private int GetSuitScoreForTrumpChoice(List<Card> hand, Barva b)
        {
            var score = 0;
            var count = hand.Count(i => i.Suit == b);

            if (count > 1)
            {
                if (hand.HasK(b))
                    score += 20;
                if (hand.HasQ(b))
                    score += 20;
                if (hand.HasA(b))
                    score += 10;
                if (hand.HasX(b))
                    score += 10;
                if (hand.Has7(b) && count >= 4)
                    score += count * 10;
                score += count;
            }
            _log.DebugFormat("Trump score for {0}: {1}", b, score);

            return score;
        }

        public override Card ChooseTrump()
        {
			return ChooseTrump(Hand.Take(7).ToList()); //nekoukat do karet co nejsou videt
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
            TrumpCard = hand.FirstOrDefault(i => i.Suit == trump && i.Value > Hodnota.Sedma && i.Value < Hodnota.Svrsek) ??
                       hand.OrderBy(i => i.Value).FirstOrDefault(i => i.Suit == trump);

			_log.DebugFormat("Trump chosen: {0}", TrumpCard);
			return TrumpCard;
        }

        private List<Card> ChooseBetlTalon(List<Card> hand, Card trumpCard)
        {
			var holesByCard = hand.Select(i =>
			{
				//pro kazdou kartu spocitej diry (mensi karty v barve ktere nemam)
				var holes = 0;
				var holesDelta = 0;
                var higherCards = 0;

				foreach (var h in Enum.GetValues(typeof(Hodnota))
									 .Cast<Hodnota>()
									 .Where(h => i.IsHigherThan(new Card(i.Suit, h), null)))
				{
					if (!hand.Any(j => j.Suit == i.Suit && j.Value == h))
					{
						holes++;
					}
				}
                foreach (var h in Enum.GetValues(typeof(Hodnota))
                                     .Cast<Hodnota>()
                                     .Where(h => !i.IsHigherThan(new Card(i.Suit, h), null)))
                {
                    if (!hand.Any(j => j.Suit == i.Suit && j.Value == h))
                    {
                        higherCards++;
                    }
                }
                //mam v ruce mensi kartu v barve?
                var card2 = hand.Where(j => j.Suit == i.Suit && j.BadValue < i.BadValue).OrderByDescending(j => j.BadValue).FirstOrDefault();

				if (card2 != null)
				{
					//spocitej pocet der ktere zmizi pokud dam kartu do talonu
					holesDelta = Enum.GetValues(typeof(Hodnota))
									 .Cast<Hodnota>()
									 .Select(h => new Card(i.Suit, h))
									 .Count(j => i.IsHigherThan(j, null) && j.IsHigherThan(card2, null));
				}
				else
				{
					holesDelta = holes;
				}

				return new Tuple<Card, int, int, int, int>(i, hand.CardCount(i.Suit), holesDelta, holes, higherCards);
			}).Where(i => i.Item4 > 0);

            //nejprve vem karty ktere jsou ve hre nejvyssi a tudiz nejvice rizikove (A, K)
            var talon = holesByCard.Where(i => i.Item2 < 7 && i.Item5 == 0)// &&               
                                   .OrderByDescending(i => i.Item1.BadValue)
                                   .Take(2)
                                   .Select(i => i.Item1)                    //Card
                                   .ToList();

            if (talon.Count < 2)
            {
                //potom vezmi karty od nejkratsich barev a alespon stredni hodnoty (1 karta > devitka)
                //radime podle poctu poctu der ktere odstranime sestupne, poctu der celkem sestupne a hodnoty karty sestupne
                talon.AddRange(holesByCard.Where(i => i.Item2 == 1 && i.Item5 > 0 && !talon.Contains(i.Item1))// &&			    //CardCount
                                          //i.Item1.Value > Hodnota.Devitka)
                                          .OrderByDescending(i => i.Item3)          //holesDelta
                                          .ThenByDescending(i => i.Item4)           //holes
                                          .ThenByDescending(i => i.Item1.BadValue)
                                          .Take(2 - talon.Count)
                                          .Select(i => i.Item1)					//Card
                                          .ToList());
            }
            if (talon.Count < 2)
			{
				//dopln kartami od delsich barev
				talon.AddRange(holesByCard.Where(i => i.Item2 < 7 &&  !talon.Contains(i.Item1))		//Card
				               		   	  .OrderByDescending(i => i.Item3)			//holesDelta
				               			  .ThenByDescending(i => i.Item4)			//holes
									   	  .ThenByDescending(i => i.Item1.BadValue)
									   	  .Take(2 - talon.Count)
									   	  .Select(i => i.Item1)						//Card
									   	  .ToList());
			}
            
            //pokud je potreba, doplnime o nejake nizke karty (abych zhorsil talon na durcha)
            if(talon.Count < 2)
            {
				talon.AddRange(hand.Where(i => !talon.Contains(i)).OrderBy(i => i.BadValue).Take(2 - talon.Count));
            }

			if (talon == null || talon.Distinct().Count() != 2)
			{
				var msg = talon == null ? "(null)" : "Count: " + talon.Distinct().Count();
				throw new InvalidOperationException("Bad talon: " + msg);
			}
            return talon;
        }

        private List<Card> ChooseDurchTalon(List<Card> hand, Card trumpCard)
        {
            var talon = hand.Where(i => hand.CardCount(i.Suit) <= 2 && //nejdriv zkus odmazat kratkou barvu ve ktere nemam eso
                                        !hand.HasA(i.Suit))
                            .Take(2)
                            .ToList();
            var holesByCard = hand.Where(i => !talon.Contains(i))
                                  .Select(i =>
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

            talon.AddRange(holesByCard.OrderByDescending(i => i.Item2) //pak doplnime kartama v barvach s nejvice dirama
                                      .ThenBy(i => i.Item1.Value)
                                      .Select(i => i.Item1)
                                      .Take(2 - talon.Count()));
            var count = talon.Count();

            //pokud je potreba, doplnime o nejake nizke karty
            if (count < 2)
            {
                talon.AddRange(hand.Where(i => !talon.Contains(i)).OrderBy(i => i.Value).Take(2 - count));
            }

			if (talon == null || talon.Distinct().Count() != 2)
			{
				var msg = talon == null ? "(null)" : "Count: " + talon.Count;
				throw new InvalidOperationException("Bad talon: " + msg);
			}

			//mozna by stacilo negrupovat podle barev ale jen sestupne podle hodnot a vzit prvni dve?
			return talon;
        }

        private List<Card> ChooseNormalTalon(List<Card> hand, Card trumpCard)
        {
            var talon = new List<Card>();

            if (trumpCard == null)
            {
                trumpCard = hand.OrderByDescending(i => hand.CardCount(i.Suit))
                                .ThenByDescending(i => i.Value).First();
            }
            //nejdriv zkus vzit karty v barve kde krom esa mam max 2 plivy (a nemam hlasku)
            talon.AddRange(hand.Where(i => i.Suit != trumpCard.Suit &&             //nevybirej trumfy
                                           i.Value != Hodnota.Eso &&               //ani esa
                                           hand.Count(j => i.Suit == j.Suit &&     //max. 2 karty jine nez A
                                                           j.Value != Hodnota.Eso) <= 2 &&
                                           !hand.HasX(i.Suit) &&                   //v barve kde neznam X ani nemam hlas
                                           !(hand.HasK(i.Suit) && hand.HasQ(i.Suit)))
                               .OrderBy(i => hand.Count(j => j.Suit == i.Suit))    //vybirej od nejkratsich barev
                               .ThenBy(i => hand.Count(j => j.Suit == i.Suit &&    //v pripade stejne delky barev
                                            j.Value == Hodnota.Eso))               //dej prednost barve s esem
                .ToList());
            //potom zkus cokoli mimo trumfu,A,X,7, hlasu a samotne plivy ktera doplnuje X
            talon.AddRange(hand.Where(i => i.Suit != trumpCard.Suit &&             //nevybirej trumfy
                                           i.Value != Hodnota.Eso &&               //ani A,X
                                           i.Value != Hodnota.Desitka &&
                                           !((i.Value == Hodnota.Kral ||           //ani hlasy
                                               i.Value == Hodnota.Svrsek) && 
                                               hand.HasK(i.Suit) && hand.HasQ(i.Suit)) &&
                                           i.Value != Hodnota.Sedma &&             //ani sedmu
                                           !(hand.HasX(i.Suit) &&                  //pokud mam jen X+plivu, tak nech plivu byt
                                               hand.CardCount(i.Suit) <= 2))
                .OrderBy(i => i.Value)                              //vybirej od nejmensich karet
                .ToList());

            //potom zkus cokoli mimo trumfu,A,X,trumfove 7, hlasu a samotne plivy ktera doplnuje X
            talon.AddRange(hand.Where(i => !(i.Value == trumpCard.Value &&         //nevybirej trumfovou kartu
                                             i.Suit == trumpCard.Suit) &&
                                           i.Value != Hodnota.Eso &&             //ani A,X
                                           i.Value != Hodnota.Desitka &&
                                           !((i.Value == Hodnota.Kral ||         //ani hlasy
                                               i.Value == Hodnota.Svrsek) &&
                                           hand.HasK(i.Suit) && hand.HasQ(i.Suit)) &&
                                           !(i.Value == Hodnota.Sedma &&         //ani trumfovou sedmu
                                             i.Suit == trumpCard.Suit) &&
                                           !(hand.HasX(i.Suit) &&                //pokud mam jen X+plivu, tak nech plivu byt
                                           hand.CardCount(i.Suit) <= 2))
                .OrderBy(i => i.Value)                              //vybirej od nejmensich karet
                .ToList());

            //nakonec cokoli co je podle pravidel
            talon.AddRange(hand.Where(i => !(i.Value == trumpCard.Value &&         //nevybirej trumfovou kartu
                                             i.Suit == trumpCard.Suit) &&
                                           i.Value != Hodnota.Eso &&               //ani A,X
                                           i.Value != Hodnota.Desitka)
                .OrderByDescending(i => i.Suit == trumpCard.Suit 
                                ? -1 : (int)trumpCard.Suit)           //nejdriv zkus jine nez trumfy
                .ThenBy(i => i.Value)                               //vybirej od nejmensich karet
                .ToList());

            if(talon.Count < 2)
            {
                var talonstr = string.Join(",", talon);
                throw new InvalidOperationException(string.Format("Badly generated talon for player{0}\nTalon:\n{1}\nHand:\n{2}", PlayerIndex + 1, talonstr, new Hand(hand)));
            }
            talon = talon.Distinct().Take(2).ToList();

			if (talon == null || talon.Count != 2 || talon.Contains(trumpCard))
			{
				var msg = talon == null ? "(null)" : "Count: " + talon.Count;
				throw new InvalidOperationException("Bad talon: " + msg);
			}

			return talon;
        }

        public override List<Card> ChooseTalon()
        {
            //zacinajici hrac nejprve vybira talon a az pak rozhoduje jakou hru bude hrat (my mame oboje implementovane uvnitr ChooseGameFlavour())
            if (PlayerIndex == _g.OriginalGameStartingPlayerIndex)
            {
                ChooseGameFlavour();
				//pokud delam poradce pro cloveka, musim vybrat talon i kdyz bych normalne nehral betla nebo durcha
				//v tom pripade jestli uz byl vybranej betl, tak my musime jit na durch a podle toho vybirat talon
				//jiank vybirame betlovej talon
				if (AdvisorMode && (_talon == null || !_talon.Any()))
				{
                    if (_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][0] * _durchSimulations && _durchSimulations > 0 ||
					    _gameType == Hra.Betl)
					{
						_talon = ChooseDurchTalon(Hand, null);
					}
					else
					{
						_talon = ChooseBetlTalon(Hand, null);
					}
				}
            }
            else
            {
                //protihrac nejdriv sjede simulaci nanecisto (bez talonu) a potom znovu s kartami talonu a vybere novy talon
                if (_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][0] * _durchSimulations && _durchSimulations > 0)
                {
                    _talon = ChooseDurchTalon(Hand, null);
                }
                else
                {
                    _talon = ChooseBetlTalon(Hand, null);
                }
            }
			if (_talon == null || _talon.Count != 2)
			{
				var msg = _talon == null ? "(null)" : "Count: " + _talon.Count;
				throw new InvalidOperationException("Bad talon: " + msg);
			}

			if (!AdvisorMode)
			{
				Probabilities.UpdateProbabilitiesAfterTalon(Hand, _talon);
			}
			_log.DebugFormat("Talon chosen: {0} {1}", _talon[0], _talon[1]);

			return _talon;
        }

        public override GameFlavour ChooseGameFlavour()
        {
            if (TestGameType.HasValue)
            {
                GameFlavour flavour;

                if (TestGameType == Hra.Betl)
                {
                    if (_talon == null || !_talon.Any())
                    {
                        _talon = ChooseBetlTalon(Hand, null);
                    }
                    flavour = GameFlavour.Bad;
                }
                else if (TestGameType == Hra.Durch)
                {
                    if (_talon == null || !_talon.Any())
                    {
                        _talon = ChooseDurchTalon(Hand, null);
                    }
                    flavour = GameFlavour.Bad;
                }
                else
                {
                    if (PlayerIndex == _g.OriginalGameStartingPlayerIndex && (_talon == null || !_talon.Any()))
                    {
                        _talon = ChooseNormalTalon(Hand, TrumpCard);
                    }
                    flavour = GameFlavour.Good;
                }
                Probabilities = new Probability(PlayerIndex, PlayerIndex, new Hand(Hand), null, _talon)
                {
                    ExternalDebugString = _debugString,
                    UseDebugString = true
                };

                return flavour;
            }
            if (_initialSimulation || AdvisorMode)
            {
                var bidding = new Bidding(_g);

                //pokud volim hru tak mam 12 karet a nechci generovat talon,
                //jinak mam 10 karet a talon si necham nagenerovat a potom ho vymenim za talon zvoleny podle logiky
                if (_talon == null) //pokud je AiPlayer poradce cloveka, tak uz je _talon definovanej
                {
                    _talon = PlayerIndex == _g.GameStartingPlayerIndex ? new List<Card>() : null;
                }
				Probabilities = new Probability(PlayerIndex, PlayerIndex, new Hand(Hand), null, _talon)
				{
					ExternalDebugString = _debugString
				};

				if (PlayerIndex == _g.OriginalGameStartingPlayerIndex && bidding.BetlDurchMultiplier == 0)
                {
                    //Sjedeme simulaci hry, betlu, durcha i normalni hry a vratit talon pro to nejlepsi. 
                    //Zapamatujeme si vysledek a pouzijeme ho i v ChooseGameFlavour() a ChooseGameType()
                    RunGameSimulations(bidding, PlayerIndex, true, true);
                    if (_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][0] * _durchSimulations && _durchSimulations > 0)
                    {
                        _talon = ChooseDurchTalon(Hand, null);
                        DebugInfo.Rule = "Durch";
                        DebugInfo.RuleCount = _durchBalance;
                        DebugInfo.TotalRuleCount = _durchSimulations;
                    }
                    else if (_betlBalance >= Settings.GameThresholdsForGameType[Hra.Betl][0] * _betlSimulations && _betlSimulations > 0)
                    {
                        _talon = ChooseBetlTalon(Hand, null);
                        DebugInfo.Rule = "Betl";
                        DebugInfo.RuleCount = _betlBalance;
                        DebugInfo.TotalRuleCount = _betlSimulations;
                    }
                    else
                    {
                        _talon = ChooseNormalTalon(Hand, TrumpCard);
                        DebugInfo.Rule = "Klasika";
                        if ((float)_betlBalance / (float)_betlSimulations > (float)_durchBalance / (float)_durchSimulations)
                        {
                            DebugInfo.RuleCount = _betlSimulations - _betlBalance;
                            DebugInfo.TotalRuleCount = _betlSimulations;
                        }
                        else
                        {
                            DebugInfo.RuleCount = _durchSimulations - _durchBalance;
                            DebugInfo.TotalRuleCount = _durchSimulations;
                        }
                    }
                }
                else
                {
                    RunGameSimulations(bidding, PlayerIndex, false, true);
                }
                _initialSimulation = false;
            }
            _moneyCalculations = null; //abychom v GetBidsAndDoubles znovu sjeli simulaci normalni hry

			//byla uz zavolena nejaka hra?
            if (_gameType == Hra.Durch)
            {
				DebugInfo.RuleCount = _durchBalance;
                DebugInfo.TotalRuleCount = _durchSimulations;
                return GameFlavour.Good;
            }
            else if (_gameType == Hra.Betl)
            {
                //pouzivam vyssi prahy: pokud nam vysel durch (beru 95% prah), abych kompenzoval, ze simulace nejsou presne
                var thresholdIndex = Math.Min(Settings.GameThresholdsForGameType[Hra.Durch].Length - 1, 3);    //95%
                if (_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][thresholdIndex] * _durchSimulations && _durchSimulations > 0)
                {
					_talon = ChooseDurchTalon(Hand, null);
					DebugInfo.RuleCount = _durchBalance;
                    DebugInfo.TotalRuleCount = _durchSimulations;
                    return GameFlavour.Bad;
                }
				DebugInfo.RuleCount = _betlBalance;
                DebugInfo.TotalRuleCount = _betlSimulations;
                return GameFlavour.Good;
            }
            else
            {
                //pouzivam vyssi prahy: pokud nam vysel betl nebo durch (beru 85% resp. 95% prah), abych kompenzoval, ze simulace nejsou presne
                var betlThresholdIndex = PlayerIndex == _g.GameStartingPlayerIndex ? 0 : Math.Min(Settings.GameThresholdsForGameType[Hra.Betl].Length - 1, 2);     //85%
                var durchThresholdIndex = PlayerIndex == _g.GameStartingPlayerIndex ? 0 : Math.Min(Settings.GameThresholdsForGameType[Hra.Durch].Length - 1, 2);    //85%
                if ((_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][durchThresholdIndex] * _durchSimulations && _durchSimulations > 0) ||
                    (_betlBalance >= Settings.GameThresholdsForGameType[Hra.Betl][betlThresholdIndex] * _betlSimulations && _betlSimulations > 0))
                {
                    if (_betlSimulations > 0 && (_durchSimulations == 0 || (float)_betlBalance / (float)_betlSimulations > (float)_durchBalance / (float)_durchSimulations))
                    {
                        DebugInfo.RuleCount = _betlBalance;
                        DebugInfo.TotalRuleCount = _betlSimulations;
                    }
                    else
                    {
                        DebugInfo.RuleCount = _durchBalance;
                        DebugInfo.TotalRuleCount = _durchSimulations;
                    }
                    return GameFlavour.Bad;
                }
                if (_betlSimulations > 0 && (_durchSimulations == 0 || (float)_betlBalance / (float)_betlSimulations > (float)_durchBalance / (float)_durchSimulations))
                {
                    DebugInfo.RuleCount = _betlSimulations - _betlBalance;
                    DebugInfo.TotalRuleCount = _betlSimulations;
                }
                else
                {
                    DebugInfo.RuleCount = _durchSimulations - _durchBalance;
                    DebugInfo.TotalRuleCount = _durchSimulations;
                }
                return GameFlavour.Good;
            }
        }

        private void UpdateGeneratedHandsByChoosingTalon(Hand[] hands, Func<List<Card>, Card, List<Card>> chooseTalonFunc, int GameStartingPlayerIndex)
        {
            const int talonIndex = 3;

            //volicimu hraci dame i to co je v talonu, aby mohl vybrat skutecny talon
            hands[GameStartingPlayerIndex].AddRange(hands[3]);

            var talon = chooseTalonFunc(hands[GameStartingPlayerIndex], TrumpCard);

            hands[GameStartingPlayerIndex].RemoveAll(i => talon.Contains(i));
            hands[talonIndex] = new Hand(talon);
        }

        private void UpdateGeneratedHandsByChoosingTrumpAndTalon(Hand[] hands, Func<List<Card>, Card, List<Card>> chooseTalonFunc, int GameStartingPlayerIndex)
        {
            const int talonIndex = 3;

            //volicimu hraci dame i to co je v talonu, aby mohl vybrat skutecny talon
            hands[GameStartingPlayerIndex].AddRange(hands[3]);

            var trumpCard = ChooseTrump(hands[GameStartingPlayerIndex]);
            var talon = chooseTalonFunc(hands[GameStartingPlayerIndex], trumpCard);

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
            OnGameComputationProgress(new GameComputationProgressEventArgs { Current = progress, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 1, Message = "Generuju karty"});

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

				_debugString.Append("Simulating good games\n");
                Parallel.ForEach(source ?? Probabilities.GenerateHands(1, PlayerIndex, Settings.SimulationsPerGameType), options, (hh, loopState) =>
                {                        
                    ThrowIfCancellationRequested();
                    if (source == null)
                    {
                        tempSource.Enqueue(hh);
                    }
                    if ((DateTime.Now - start).TotalMilliseconds > Settings.MaxSimulationTimeMs)
                    {
                        prematureEnd = true;
                        loopState.Stop();
                    }
                    else
                    {
                        Interlocked.Increment(ref actualSimulations);
                        var hands = new Hand[Game.NumPlayers + 1];
                        for (var i = 0; i < hands.Length; i++)
                        {
                            hands[i] = new Hand(new List<Card>((List<Card>)hh[i]));   //naklonuj karty aby v pristich simulacich nebyl problem s talonem
                        }
                        UpdateGeneratedHandsByChoosingTalon(hands, ChooseNormalTalon, GameStartingPlayerIndex);

                        // to ?? vypada chybne
                        var gameComputationResult = ComputeGame(hands, null, null, _trump ?? _g.trump, _gameType != null ? (_gameType | Hra.SedmaProti) : (Hra.Sedma | Hra.SedmaProti), 10, 1);
                        gameComputationResults.Enqueue(gameComputationResult);
                    }                    
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
				var fastSelection = Settings.GameFlavourSelectionStrategy == GameFlavourSelectionStrategy.Fast;

				if (!fastSelection || ShouldChooseBetl())
				{
					_debugString.AppendFormat("Simulating betl. Fast guess: {0}\n", ShouldChooseBetl());
					Parallel.ForEach(source ?? Probabilities.GenerateHands(1, PlayerIndex, Settings.SimulationsPerGameType), options, (hh, loopState) =>
					{
						ThrowIfCancellationRequested();
						if (source == null)
						{
							tempSource.Enqueue(hh);
						}
						if ((DateTime.Now - start).TotalMilliseconds > Settings.MaxSimulationTimeMs)
						{
							prematureEnd = true;
							loopState.Stop();
						}
						Interlocked.Increment(ref actualSimulations);

						var hands = new Hand[Game.NumPlayers + 1];
						for (var i = 0; i < hh.Length; i++)
						{
							hands[i] = new Hand(new List<Card>((List<Card>)hh[i]));   //naklonuj karty aby v pristich simulacich nebyl problem s talonem
						}
						if (PlayerIndex != _g.GameStartingPlayerIndex) //pokud nevolim tak nasimuluju shoz do talonu
						{
							UpdateGeneratedHandsByChoosingTrumpAndTalon(hands, ChooseNormalTalon, _g.GameStartingPlayerIndex);
						}
						else //pokud volim, tak v UpdateGeneratedHandsByChoosingTalon() beru v potaz trumfovou kartu kterou jsem zvolil
						{
							UpdateGeneratedHandsByChoosingTalon(hands, ChooseNormalTalon, _g.GameStartingPlayerIndex);
						}
						UpdateGeneratedHandsByChoosingTalon(hands, ChooseBetlTalon, GameStartingPlayerIndex);

						var betlComputationResult = ComputeGame(hands, null, null, null, Hra.Betl, 10, 1, true);
						betlComputationResults.Enqueue(betlComputationResult);

						var val = Interlocked.Increment(ref progress);
						OnGameComputationProgress(new GameComputationProgressEventArgs { Current = val, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0, Message = "Simuluju betl" });
					});
					var end = DateTime.Now;
					Settings.SimulationsPerGameType = actualSimulations;
					Settings.SimulationsPerGameTypePerSecond = (int)((float)actualSimulations / Settings.MaxSimulationTimeMs * 1000);
					//Settings.SimulationsPerGameTypePerSecond = (int)((float)actualSimulations / (end - start).TotalMilliseconds * 1000);
				}
				else
				{
					Interlocked.Add(ref progress, Settings.SimulationsPerGameType);
				}
				totalGameSimulations = (simulateGoodGames ? Settings.SimulationsPerGameType : 0) +
                    (simulateBadGames ? 2 * Settings.SimulationsPerGameType : 0);
				OnGameComputationProgress(new GameComputationProgressEventArgs { Current = progress, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0 });
                if (source == null && tempSource.Any())
                {
                    source = tempSource.ToArray();
                }
                start = DateTime.Now;
				if (!fastSelection || ShouldChooseDurch())
				{
					_debugString.AppendFormat("Simulating durch. fast guess: {0}\n", ShouldChooseDurch());
					Parallel.ForEach(source ?? Probabilities.GenerateHands(1, PlayerIndex, Settings.SimulationsPerGameType), (hands, loopState) =>
					{
						ThrowIfCancellationRequested();
						if (source == null)
						{
							tempSource.Enqueue(hands);
						}
                        if ((DateTime.Now - start).TotalMilliseconds > Settings.MaxSimulationTimeMs)
                        {
                            prematureEnd = true;
                            loopState.Stop();
                        }
                        //nasimuluj ze volici hrac vybral trumfy a/nebo talon
                        if (_g.GameType == Hra.Betl || PlayerIndex == _g.GameStartingPlayerIndex)
						{   //pokud jsem volil ja tak v UpdateGeneratedHandsByChoosingTalon() pouziju skutecne zvoleny trumf
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
						OnGameComputationProgress(new GameComputationProgressEventArgs { Current = val, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0, Message = "Simuluju durch" });

						if (NoChanceToWinDurch(PlayerIndex, hands))
						{
							OnGameComputationProgress(new GameComputationProgressEventArgs { Current = initialProgress + Settings.SimulationsPerGameType, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0, Message = "Neuhratelnej durch" });
							loopState.Stop();
						}
					});
				}
				else
				{
					Interlocked.Add(ref progress, Settings.SimulationsPerGameType);
				}
			}
			OnGameComputationProgress(new GameComputationProgressEventArgs { Current = progress, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0 });

			//vyber vhodnou hru podle vysledku simulace
			var opponent = TeamMateIndex == (PlayerIndex + 1) % Game.NumPlayers
                ? (PlayerIndex + 2) % Game.NumPlayers : (PlayerIndex + 1) % Game.NumPlayers;
            _moneyCalculations = gameComputationResults.Select(i =>
            {
                var calc = new AddingMoneyCalculator(Hra.Sedma, _trump ?? _g.trump, GameStartingPlayerIndex, bidding, i);

                calc.CalculateMoney();

                return calc;
            }).Union(durchComputationResults.Select(i =>
            {
                var calc = new AddingMoneyCalculator(Hra.Durch, null, GameStartingPlayerIndex, bidding, i);

                calc.CalculateMoney();

                return calc;
            })).Union(betlComputationResults.Select(i =>
            {
                var calc = new AddingMoneyCalculator(Hra.Betl, null, GameStartingPlayerIndex, bidding, i);

                calc.CalculateMoney();

                return calc;
            })).ToList();
            _goodSimulations = _moneyCalculations.Count(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0);
            _betlSimulations = _moneyCalculations.Count(i => i.GameType == Hra.Betl);
            _durchSimulations = _moneyCalculations.Count(i => i.GameType == Hra.Durch);

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
            var allChoices = new List<RuleDebugInfo>();
            allChoices.Add(new RuleDebugInfo
            {
                Rule = "Hra",
                RuleCount = _gamesBalance
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = "Sedma",
                RuleCount = _sevensBalance
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = "Kilo",
                RuleCount = _hundredsBalance
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = "Sedma proti",
                RuleCount = _sevensAgainstBalance
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = "Kilo proti",
                RuleCount = _hundredsAgainstBalance
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = "Betl",
                RuleCount = _betlBalance
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = "Durch",
                RuleCount = _durchBalance
            });
            DebugInfo.AllChoices = allChoices.ToArray();
            DebugInfo.TotalRuleCount = Settings.SimulationsPerGameType;
        }

		public bool ShouldChooseDurch()
		{
			var holesPerSuit = new Dictionary<Barva, int>();
			var hiHolePerSuit = new Dictionary<Barva, Card>();
			foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
			{
				var holes = 0;
				var hiHole = new Card(Barva.Cerveny, Hodnota.Sedma);

				foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
				{
					var c = new Card(b, h);

					if (Hand.Any(i => i.Suit == b && i.BadValue < c.BadValue && !Hand.Contains(c)))
					{
						holes++;
						if (c.BadValue > hiHole.BadValue)
						{
							hiHole = c;
						}
					}
				}

				holesPerSuit.Add(b, holes);
				hiHolePerSuit.Add(b, hiHole);
			}
            if (PlayerIndex == _g.GameStartingPlayerIndex)
            {
                var minHole = new Card(Barva.Cerveny, Hodnota.Desitka);
                if (holesPerSuit.All(i => i.Value == 0 || hiHolePerSuit[i.Key].BadValue <= minHole.BadValue))
                {
                    return true;
                }
            }
            else //pokud se rozmyslim, zda vzit talon a hrat durcha, tak mam mensi pozadavky
            {
                var minHole = new Card(Barva.Cerveny, Hodnota.Spodek);
                var topCards = Hand.Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                   .Where(h => new Card(Barva.Cerveny, h).BadValue > i.BadValue)
                                                   .All(h => Probabilities.CardProbability(_g.GameStartingPlayerIndex, new Card(i.Suit, h)) == 0 &&
                                                             Probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, h)) == 0))
                                   .ToList();
                if (holesPerSuit.All(i => i.Value == 0 || 
                                          hiHolePerSuit[i.Key].BadValue <= minHole.BadValue ||
                                          (topCards.Count(j => j.Suit == i.Key) > 0 &&              //barva ve ktere mam o 2 mene nejvyssich karet nez der 
                                           topCards.Count(j => j.Suit == i.Key) + 2 >= i.Value) ||  //doufam v dobrou rozlozenost (simulace ukaze)
                                          Hand.CardCount(i.Key) <= 2))
                {
                    return true;
                }
            }
			return false;
		}

		public bool ShouldChooseBetl()
		{
            var talon = ChooseBetlTalon(Hand, null);                //nasimuluj talon
            var hh = PlayerIndex == _g.GameStartingPlayerIndex 
                                    ? Hand 
                                    : Hand.Where(i => !talon.Contains(i)).ToList();
			var holesPerSuit = new Dictionary<Barva, int>();
			var hiCardsPerSuit = new Dictionary<Barva, int>();
			foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
			{
				var holes = 0;		//pocet der v barve
				var hiCards = 0;	//pocet karet ktere maji pod sebou diru v barve

				foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
				{
					var c = new Card(b, h);
					var n = hh.Count(i => i.Suit == b && i.BadValue > c.BadValue && !hh.Contains(c));

					if (n > 0)
					{
						holes++;
						hiCards = Math.Max(hiCards, n);
					}
				}
				holesPerSuit.Add(b, holes);
				hiCardsPerSuit.Add(b, hiCards);
			}

			//max 4 vysokych karet celkove a max 3 v jedne barve => 2 pujdou do talonu, treti kartou zacnu hrat, dalsi diry risknu
            //pokud jsem puvodne nevolil, je sance, ze nekterou diru zalepi talon ...
			//nebo max jedna barva s hodne vysokymi kartami ale prave jednou dirou (musim mit sedmu v dane barve)
			if ((hiCardsPerSuit.Sum(i => i.Value) <= 4 && hiCardsPerSuit.All(i => i.Value <= 3)) ||
			    (hiCardsPerSuit.Count(i => i.Value > 2 &&
                 holesPerSuit[i.Key] == 1 &&
                 hh.Any(j => j.Value == Hodnota.Sedma && j.Suit == i.Key)) == 1))
			{
				return true;
			}
			return false;
		}

		public bool ShouldChooseHundred()
		{
			var axCount = Hand.Count(i => i.Value == Hodnota.Eso || 
			                         	  (i.Value == Hodnota.Desitka && 
			                          	   Hand.Any(j => j.Suit == i.Suit && 
			                                   			 (j.Value == Hodnota.Eso || j.Value == Hodnota.Kral))));
			var trumpCount = Hand.Count(i => i.Suit == _trump.Value && i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka); //bez A,X
			var axTrumpCount = Hand.Count(i => i.Suit == _trump.Value && (i.Value == Hodnota.Eso ||
										   (i.Value == Hodnota.Desitka &&
											Hand.Any(j => j.Suit == i.Suit &&
			                                              (j.Value == Hodnota.Eso || j.Value == Hodnota.Kral)))));
			var cardsPerSuit = new Dictionary<Barva, int>();
			var kqs = new List<Barva>();
			var n = axCount * 10;	//vezmi body za A,X

			foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
			{
				//mam v barve hlasku?
				if (Hand.Count(i => i.Suit == b && (i.Value == Hodnota.Kral || i.Value == Hodnota.Svrsek)) == 2)
				{
					kqs.Add(b);
				}
				cardsPerSuit.Add(b, Hand.Count(i => i.Suit == b));
			}
			if (!kqs.Any())
			{
				//nemam zadanou hlasku v ruce
				return false;
			}
			//pridej body za hlasky
			if (kqs.Any(i => i == _trump.Value))
			{
				n += 40;
			}
			else
			{
				n += 20;
			}

			var emptySuits = cardsPerSuit.Count(i => i.Value == 0);
			var aceOnlySuits = Hand.Count(i => i.Value == Hodnota.Eso && Hand.Count(j => j.Suit == i.Suit) == 1);

			//pridej 20 za kazdou prazdnou barvu a 10 za kazdou barvu kde znam jen eso
			n += Math.Min(2 * emptySuits + aceOnlySuits, trumpCount + axTrumpCount) * 10;

			if (trumpCount + axCount >= 5)
			{
				//posledni stych
				n += 10;
			}

			return n >= 100;
		}

		public bool ShouldChooseSeven()
		{
			var numSuits = Hand.Select(i => i.Suit).Distinct().Count();
			var numTrumps = Hand.Count(i => i.Suit == _trump.Value);
			var has7 = Hand.Any(i => i.Suit == _trump.Value && i.Value == Hodnota.Sedma);

			return has7 && ((numTrumps == 4 && numSuits == 4) ||
			                (numTrumps >= 5));
		}

		public Hra ChooseGameTypeNew(Hra validGameTypes)
		{
			var gameType = Hra.Hra;

			//pokud volim trumfy tak je znam a volim ze vsech her, jinak je neznam a muzu jen volit z betla nebo durcha
			if (_trump.HasValue && ShouldChooseHundred())
			{
				gameType = Hra.Kilo;
			}
			else if (ShouldChooseDurch())
			{
				gameType = Hra.Durch;
			}
			else if (ShouldChooseBetl())
			{
				gameType = Hra.Betl;
			}
			if ((gameType & (Hra.Betl | Hra.Durch)) == 0)
			{
				if (_trump.HasValue && ShouldChooseSeven())
				{
					gameType |= Hra.Sedma;
				}
			}

			return gameType;
		}

        //vola se z enginu
        public override Hra ChooseGameType(Hra validGameTypes)
        {
            if (TestGameType.HasValue)
            {
                if ((TestGameType.Value & validGameTypes) == 0)
                {
                    TestGameType = Hra.Hra;
                }
                if (TestGameType.Value == Hra.Sedma)
                {
                    TestGameType |= Hra.Hra;
                }
                return TestGameType.Value;
            }
            //TODO: urcit typ hry podle zisku ne podle pradepodobnosti
            Hra gameType;

            if ((validGameTypes & (Hra.Betl | Hra.Durch)) != 0)
            {
                if ((validGameTypes & (Hra.Hra | Hra.Kilo | Hra.Sedma)) == 0)
                {
                    var bidding = new Bidding(_g);
                    RunGameSimulations(bidding, PlayerIndex, false, true);
                }
                if ((_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][0] * _durchSimulations && _durchSimulations > 0) ||
				    validGameTypes == Hra.Durch)
                {
                    gameType = Hra.Durch;
                    DebugInfo.RuleCount = _durchBalance;
                    DebugInfo.TotalRuleCount = _durchSimulations;
                }
                else //if (_betlBalance >= Settings.GameThresholds[0] * Settings.SimulationsPerGameType)
                {
                    gameType = Hra.Betl;
                    DebugInfo.RuleCount = _betlBalance;
                    DebugInfo.TotalRuleCount = _betlSimulations;
                }
            }
            else
            {
                if (_hundredsBalance >= Settings.GameThresholdsForGameType[Hra.Kilo][0] * _goodSimulations && _goodSimulations > 0)
                {
                    gameType = Hra.Kilo;
                    DebugInfo.RuleCount = _hundredsBalance;
                }
                else
                {
                    gameType = Hra.Hra;
                    DebugInfo.RuleCount = _gamesBalance;
                }
                if (_sevensBalance >= Settings.GameThresholdsForGameType[Hra.Sedma][0] * _goodSimulations && _goodSimulations > 0)
                {
                    if (gameType == Hra.Hra)
                    {
                        DebugInfo.RuleCount = _sevensBalance;   //u sedmy me zajima sedma, u 107 kilo
                    }
                    gameType |= Hra.Sedma;
                };
                DebugInfo.TotalRuleCount = _goodSimulations;
            }
            //DebugInfo.TotalRuleCount = Settings.SimulationsPerGameType;
            DebugInfo.Rule = (gameType & (Hra.Betl | Hra.Durch)) == 0 ? string.Format("{0} {1}", gameType, _trump) : gameType.ToString();
            var allChoices = new List<RuleDebugInfo> ();
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Hra.ToDescription(_trump),
                RuleCount = _gamesBalance
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = (Hra.Hra | Hra.Sedma).ToDescription(_trump),
                RuleCount = _sevensBalance
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Kilo.ToDescription(_trump),
                RuleCount = _hundredsBalance
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Betl.ToString(),
                RuleCount = _betlBalance
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Durch.ToString(),
                RuleCount = _durchBalance
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
            var gameThresholdPrevious = bidding._gameFlek > 1 && bidding._gameFlek - 1 < Settings.GameThresholdsForGameType[Hra.Hra].Length ? Settings.GameThresholdsForGameType[Hra.Hra][bidding._gameFlek - 1] : 1f;
            var gameThresholdNext = bidding._gameFlek < Settings.GameThresholdsForGameType[Hra.Hra].Length - 1 ? Settings.GameThresholdsForGameType[Hra.Hra][bidding._gameFlek + 1] : 1f;
            var sevenThreshold = bidding._sevenFlek < Settings.GameThresholdsForGameType[Hra.Sedma].Length ? Settings.GameThresholdsForGameType[Hra.Sedma][bidding._sevenFlek] : 1f;
            var hundredThreshold = bidding._gameFlek < Settings.GameThresholdsForGameType[Hra.Kilo].Length ? Settings.GameThresholdsForGameType[Hra.Kilo][bidding._gameFlek] : 1f;
            var sevenAgainstThreshold = bidding._sevenAgainstFlek < Settings.GameThresholdsForGameType[Hra.SedmaProti].Length ? Settings.GameThresholdsForGameType[Hra.SedmaProti][bidding._sevenAgainstFlek] : 1f;
            var hundredAgainstThreshold = bidding._hundredAgainstFlek < Settings.GameThresholdsForGameType[Hra.KiloProti].Length ? Settings.GameThresholdsForGameType[Hra.KiloProti][bidding._hundredAgainstFlek] : 1f;
            var betlThreshold = bidding._betlDurchFlek < Settings.GameThresholdsForGameType[Hra.Betl].Length ? Settings.GameThresholdsForGameType[Hra.Betl][bidding._betlDurchFlek] : 1f;
            var durchThreshold = bidding._betlDurchFlek < Settings.GameThresholdsForGameType[Hra.Durch].Length ? Settings.GameThresholdsForGameType[Hra.Durch][bidding._betlDurchFlek] : 1f;
			var minRuleCount = int.MaxValue;

            //if (bidding.MaxDoubleCount > Settings.MaxDoubleCount)
            //{
            //    //uz stacilo
            //    return bid;
            //}
            if (_moneyCalculations == null)
            {
                //mame flekovat hru
                //kilo simulovat nema cenu, hrac ho asi ma, takze flekovat stejne nebudeme
                if (bidding.BetlDurchMultiplier == 0 && ((bidding.Bids & (Hra.Hra | Hra.Sedma | Hra.SedmaProti)) != 0))
                {
                    if (_initialSimulation)
                    {
                        RunGameSimulations(bidding, _g.GameStartingPlayerIndex, true, false);
                        _initialSimulation = false;
                    }
                }
                else
                {
                    //mame flekovat betl nebo durch
                    //nema smysl, nic o souperovych kartach nevime
                    //RunGameSimulations(bidding, _g.GameStartingPlayerIndex, false, true);
                }
            }
            else if ((bidding.Bids & (Hra.SedmaProti | Hra.KiloProti)) != 0 && 
                     bidding.SevenAgainstMultiplier <= 1 && 
                     bidding.HundredAgainstMultiplier <= 1)
            {
                RunGameSimulations(bidding, _g.GameStartingPlayerIndex, true, false);
            }
            //Flekovani u hry posuzuje podle pravdepodobnosti (musi byt vyssi nez prah) 
            if ((bidding.Bids & Hra.Hra) != 0 &&
                Settings.CanPlayGameType[Hra.Hra] &&
                bidding._gameFlek <= Settings.MaxDoubleCountForGameType[Hra.Hra] &&
                ((_gamesBalance / (float)_goodSimulations >= gameThreshold &&
                    //pokud jsem volil (re a vys) a:
                    //trham trumfovy hlas
                    //nebo ho netrham a mam aspon dva hlasy
                    ((TeamMateIndex == -1 && 
                      (Enum.GetValues(typeof(Barva)).Cast<Barva>().Count(b => Hand.HasK(b) && Hand.HasQ(b)) >= 2 ||
                       ((Hand.HasK(_trump.Value) || 
                         Hand.HasQ(_trump.Value)) && true)) ||
                        //(Enum.GetValues(typeof(Barva)).Cast<Barva>().Any(b => Hand.HasK(b) && Hand.HasQ(b)) ||
                        // ((_gameType & Hra.Sedma) != 0 &&
                        //  (_gamesBalance / (float)_goodSimulations >= gameThresholdNext))))) ||
                     //nebo jsem nevolil a:
                     // - (flek) trham trumfovou hlasku nebo mam aspon dva hlasy nebo
                     // - (tutti a vys) mam hlas a citim se na flek
                     (TeamMateIndex != -1 &&
                      ((bidding.GameMultiplier > 2 && 
                            Hand.Any(i => i.Value == Hodnota.Svrsek && Hand.Any(j => j.Value == Hodnota.Kral && j.Suit == i.Suit))) ||
                       (bidding.GameMultiplier < 2 &&
                            (Hand.HasK(_g.trump.Value) || 
                             Hand.HasQ(_g.trump.Value) || 
                             Enum.GetValues(typeof(Barva)).Cast<Barva>().Count(b => Hand.HasK(b) && Hand.HasQ(b)) >= 2))))))) ||
                 //nebo davam re a jsem si dost jisty nehlede na hlasy
                 (TeamMateIndex == -1 &&
                  _gamesBalance / (float)_goodSimulations >= gameThresholdNext) ||
                 //nebo kolega flekoval a ja mam nejakou hlasku a citil jsem se na flek jiz minule (tutti a vys),
                 (_teamMateDoubledGame && _gamesBalance / (float)_goodSimulations >= gameThresholdPrevious && 
                  Hand.Any(i => i.Value == Hodnota.Svrsek && Hand.Any(j => j.Value == Hodnota.Kral && j.Suit == i.Suit)))))
            {
                bid |= bidding.Bids & Hra.Hra;
                minRuleCount = Math.Min(minRuleCount, _gamesBalance);
            }
            //sedmu flekuju jen pokud jsem volil sam sedmu a v simulacich jsem ji uhral dost casto
            //nebo pokud jsem nevolil a v simulacich ani jednou nevysla
            //if ((PlayerIndex == _g.GameStartingPlayerIndex && _sevensBalance / (float)Settings.SimulationsPerGameType >= sevenThreshold) ||
            //    (PlayerIndex != _g.GameStartingPlayerIndex && _sevensBalance == Settings.SimulationsPerGameType))

            //sedmu flekuju pokud mam aspon 3 trumfy
            if ((bidding.Bids & Hra.Sedma) != 0 &&
                Settings.CanPlayGameType[Hra.Sedma] &&
                bidding._sevenFlek <= Settings.MaxDoubleCountForGameType[Hra.Sedma] &&
                Hand.CardCount(_g.trump.Value) >= 3 && _sevensBalance / (float)_goodSimulations >= sevenThreshold)
            {
                bid |= bidding.Bids & Hra.Sedma;
                minRuleCount = Math.Min(minRuleCount, _sevensBalance);
            }
            //kilo flekuju jen pokud jsem volil sam kilo a v simulacich jsem ho uhral dost casto
            //nebo pokud jsem nevolil a je nemozne aby mel volici hrac kilo (nema hlas)
            //?! Pokud bych chtel simulovat sance na to, ze volici hrac hlasene kilo neuhraje, tak musim nejak generovat "karty na kilo" (aspon 1 hlas) a ne nahodne karty
            if ((bidding.Bids & Hra.Kilo) != 0 &&
                Settings.CanPlayGameType[Hra.Kilo] &&
                bidding._gameFlek <= Settings.MaxDoubleCountForGameType[Hra.Kilo] &&
                ((PlayerIndex == _g.GameStartingPlayerIndex && _hundredsBalance / (float)_goodSimulations >= hundredThreshold) ||
			     (PlayerIndex != _g.GameStartingPlayerIndex && Probabilities.HlasProbability(_g.GameStartingPlayerIndex) == 0)))
            {
                bid |= bidding.Bids & Hra.Kilo;
                minRuleCount = Math.Min(minRuleCount, _hundredsBalance);
            }
            //sedmu proti flekuju jen pokud jsem hlasil sam sedmu proti a v simulacich jsem ji uhral dost casto a navic mam aspon 3 trumfy
            //nebo pokud jsem volil trumf a v simulacich ani jednou nevysla
            //?! Pokud bych chtel simulovat sance na to, ze volici hrac hlasenou sedmu neuhraje, tak musim nejak generovat "karty na sedmu" (aspon 4-5 trumfu) a ne nahodne karty
            if ((bidding.Bids & Hra.SedmaProti) != 0 &&
                Settings.CanPlayGameType[Hra.SedmaProti] &&
                bidding._sevenAgainstFlek <= Settings.MaxDoubleCountForGameType[Hra.SedmaProti] &&
                _sevensAgainstBalance / (float)_goodSimulations >= sevenAgainstThreshold)
                //(PlayerIndex != _g.GameStartingPlayerIndex &&                  
                // (_sevensAgainstBalance / (float)_goodSimulations >= sevenAgainstThreshold ||
                //  (((Hand.CardCount(_g.trump.Value) >= 4 &&              //neohlizej se na simulace, sedmu proti hlasim kdyz mam aspon 4 trumfy a vsechny barvy nebo aspon 5 trumfu
                //     Hand.Select(i => i.Suit).Distinct().Count() == 4) ||
                //    Hand.CardCount(_g.trump.Value) >= 5) &&
                //   Hand.Count(i => i.Suit == _g.trump.Value && i.Value >= Hodnota.Svrsek) >= 2) ||
                //(PlayerIndex == _g.GameStartingPlayerIndex && _sevensAgainstBalance / (float)_goodSimulations >= sevenAgainstThreshold))))
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
            if ((bidding.Bids & Hra.KiloProti) != 0 &&                
                ((Settings.CanPlayGameType[Hra.KiloProti] &&
                  bidding._hundredAgainstFlek <= Settings.MaxDoubleCountForGameType[Hra.KiloProti] &&
                  (PlayerIndex != _g.GameStartingPlayerIndex && _hundredsAgainstBalance / (float)_goodSimulations >= hundredAgainstThreshold)) ||
                 (PlayerIndex == _g.GameStartingPlayerIndex && //_gamesBalance / (float)_goodSimulations >= gameThreshold && //_hundredsAgainstBalance == Settings.SimulationsPerGameType))); //never monte carlu, dej na pravdepodobnost
                  (Probabilities.HlasProbability((PlayerIndex + 1) % Game.NumPlayers) == 0) &&
				  (Probabilities.HlasProbability((PlayerIndex + 2) % Game.NumPlayers) == 0))))
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
            if ((bidding.Bids & Hra.Durch) != 0 &&
                Settings.CanPlayGameType[Hra.Durch] &&
                bidding._betlDurchFlek <= Settings.MaxDoubleCountForGameType[Hra.Durch] &&
                ((PlayerIndex == _g.GameStartingPlayerIndex && _durchSimulations > 0 && _durchBalance / (float)_durchSimulations >= durchThreshold) ||
			     (PlayerIndex != _g.GameStartingPlayerIndex && Hand.Count(i => i.Value == Hodnota.Eso) == 4)))
            {
                bid |= bidding.Bids & Hra.Durch;
                minRuleCount = Math.Min(minRuleCount, _durchBalance);
            }
            //betla flekuju jen pokud jsem volil sam betla a v simulacich jsem ho uhral dost casto
            if ((bidding.Bids & Hra.Betl) != 0 &&
                Settings.CanPlayGameType[Hra.Betl] &&
                bidding._betlDurchFlek <= Settings.MaxDoubleCountForGameType[Hra.Betl] &&
                PlayerIndex == _g.GameStartingPlayerIndex && _betlSimulations > 0 && _betlBalance / (float)_betlSimulations >= betlThreshold)
            {
                bid |= bidding.Bids & Hra.Betl;
                minRuleCount = Math.Min(minRuleCount, _betlBalance);
            }
            if (minRuleCount == int.MaxValue)
            {
                minRuleCount = _gamesBalance;
            }
            DebugInfo.Rule = bid.ToString();
			DebugInfo.RuleCount = minRuleCount;
            DebugInfo.TotalRuleCount = _gameType == Hra.Betl ? _betlSimulations : _gameType == Hra.Durch ? _durchSimulations : _goodSimulations;
            var allChoices = new List<RuleDebugInfo>();
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Hra.ToString(),
                RuleCount = _gamesBalance,
                TotalRuleCount = _goodSimulations
            });
			allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Sedma.ToString(),
                RuleCount = _sevensBalance,
                TotalRuleCount = _goodSimulations
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.SedmaProti.ToString(),
                RuleCount = _sevensAgainstBalance,
                TotalRuleCount = _goodSimulations
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Kilo.ToString(),
                RuleCount = _hundredsBalance,
                TotalRuleCount = _goodSimulations
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Betl.ToString(),
                RuleCount = _betlBalance,
                TotalRuleCount = _betlSimulations
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Durch.ToString(),
                RuleCount = _durchBalance,
                TotalRuleCount = _durchSimulations
            });
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
			if (PlayerIndex == _g.GameStartingPlayerIndex)
			{
				_talon = _g.talon;
			}
			Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, _talon)
			{
				ExternalDebugString = _debugString
			};
        }

        private void GameFlavourChosen(object sender, GameFlavourChosenEventArgs e)
        {
            //pokud nekdo hlasi spatnou barvu vymaz svuj talon (uz neni relevantni) a zrus dosavadni simulace (nejsou relevantni)
            if (e.Player.PlayerIndex != PlayerIndex && e.Flavour == GameFlavour.Bad)
            {
                _talon = null;
                _initialSimulation = true;
                if (TestGameType.HasValue && TestGameType.Value != Hra.Durch)
                {
                    TestGameType = null;
                }
            }
            if (Probabilities != null) //Probabilities == null kdyz jsem nezacinal, tudiz netusim co je v talonu a nemusim nic upravovat
            {
                Probabilities.UpdateProbabilitiesAfterGameFlavourChosen(e);
            }
        }

        private void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            _trump = _g.trump;
            TrumpCard = e.TrumpCard;
            _gameType = _g.GameType;
            if (_g.GameStartingPlayerIndex != PlayerIndex)
            {
                //zapomen na predesle simulace pokud nevolis
                _initialSimulation = true;
            }
            if (PlayerIndex != _g.GameStartingPlayerIndex || Probabilities == null) //Probabilities == null by nemelo nastat, ale ...
            {
				Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, _talon)
				{
					ExternalDebugString = _debugString
				};
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
            Probabilities.UpdateProbabilitiesAfterBidMade(e, _g.Bidding);
        }

        private void CardPlayed(object sender, Round r)
        {
            UpdateProbabilitiesAfterCardPlayed(Probabilities, r.number, r.player1.PlayerIndex, r.c1, r.c2, r.c3, r.hlas1, r.hlas2, r.hlas3, TeamMateIndex, _teamMatesSuits, _teamMateDoubledGame);
        }

        private static void UpdateProbabilitiesAfterCardPlayed(Probability probabilities, int roundNumber, int roundStarterIndex, Card c1, Card c2, Card c3, bool hlas1, bool hlas2, bool hlas3, int teamMateIndex, List<Barva> teamMatesSuits, bool teamMateDoubledGame)
        {
            if (c3 != null)
            {
                probabilities.UpdateProbabilities(roundNumber, roundStarterIndex, c1, c2, c3, hlas3);
            }
            else if (c2 != null)
            {
                probabilities.UpdateProbabilities(roundNumber, roundStarterIndex, c1, c2, hlas2);
            }
            else
            {
                probabilities.UpdateProbabilities(roundNumber, roundStarterIndex, c1, hlas1);
                if (roundStarterIndex == teamMateIndex && teamMateDoubledGame)
                {
                    if (teamMatesSuits.All(i => i != c1.Suit))
                    {
                        teamMatesSuits.Add(c1.Suit);
                    }
                }
            }
        }

        public override Card PlayCard(Round r)
        {
            var roundStarterIndex = r.player1.PlayerIndex;
            Card cardToPlay = null;
            var cardScores = new ConcurrentDictionary<Card, ConcurrentQueue<GameComputationResult>>();

			_debugString.AppendFormat("PlayCard(round{0}: c1: {1} c2: {2} c3: {3})\n", r.number, r.c1, r.c2, r.c3);
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
                var goodGame = (_gameType & (Hra.Betl | Hra.Durch)) == 0;

                for (var i = 0; i < Game.NumPlayers; i++)
                {
					_log.DebugFormat("{0}'s probabilities for {1}:\n{2}", Name, _g.players[i].Name, Probabilities.FriendlyString(i, r.number));
                }
                var simulations = (int)Math.Min(Settings.SimulationsPerRound,
                    Math.Max(Probabilities.PossibleCombinations((PlayerIndex + 1) % Game.NumPlayers, r.number), //*3 abych snizil sanci ze budu generovat nektere kombinace vickrat
                             Probabilities.PossibleCombinations((PlayerIndex + 2) % Game.NumPlayers, r.number))) * 3;
                OnGameComputationProgress(new GameComputationProgressEventArgs { Current = 0, Max = Settings.SimulationsPerRoundPerSecond > 0 ? simulations : 0, Message = "Generuju karty"});
                var source = goodGame && _g.CurrentRound != null
                               ? Probabilities.GenerateHands(r.number, roundStarterIndex, 1) 
                               : Probabilities.GenerateHands(r.number, roundStarterIndex, simulations);
                var progress = 0;
                var start = DateTime.Now;

                if (goodGame && _g.CurrentRound != null)
                {
                    //pokud je hra v behu tak u klasicke hry nepotrebujeme paralelni vypocty
                    //protoze ted pouzivame pravdepodobnostni pravidla
                    options = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 1
                    };
                }
                Parallel.ForEach(source, options, (hands, loopState) =>
                {
                    Check(hands);
                    ThrowIfCancellationRequested();
                    if ((DateTime.Now - start).TotalMilliseconds > Settings.MaxSimulationTimeMs)
                    {
                        loopState.Stop();
                    }
                    else
                    {
                        var computationResult = ComputeGame(hands, r.c1, r.c2);

                        if (!cardScores.TryAdd(computationResult.CardToPlay, new ConcurrentQueue<GameComputationResult>(new[] { computationResult })))
                        {
                            cardScores[computationResult.CardToPlay].Enqueue(computationResult);
                        }
                    
                        var val = Interlocked.Increment(ref progress);
                        OnGameComputationProgress(new GameComputationProgressEventArgs { Current = val, Max = Settings.SimulationsPerRoundPerSecond > 0 ? simulations : 0, Message = "Simuluju hru" });

                        if (computationResult.Rule == AiRule.PlayTheOnlyValidCard ||
                            computationResult.Rule.SkipSimulations ||
                            canSkipSimulations)    //We have only one card to play, so there is really no need to compute anything
                        {
                            OnGameComputationProgress(new GameComputationProgressEventArgs { Current = simulations, Max = Settings.SimulationsPerRoundPerSecond > 0 ? simulations : 0 });
                            loopState.Stop();
                        }
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
            if (cardRules.Count == 0)
            {
                throw new InvalidOperationException("No card rules to choose from");
            }
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
                            DebugInfo.Rule = bestRuleWithThreshold.Value.First(i => i.UseThreshold).Description;
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

        private StringBuilder x = new StringBuilder();

        private GameComputationResult ComputeGame(Hand[] hands, Card c1, Card c2, Barva? trump = null, Hra? gameType = null, int? roundsToCompute = null, int? initialRoundNumber = null, bool ImpersonateGameStartingPlayer = false)
        {
            var result = InitGameComputationResult(hands);
            var firstTime = true;
            int player1;
            int player2;
            int player3;
            int playerIndex;
            int teamMateIndex;
            string playerName;

            Check(hands);
            if (c1 == null && c2 == null)
            {
                if (_g.CurrentRound == null && !ImpersonateGameStartingPlayer)
                {   //jeste se nezacalo hrat a simuluju celou hru a nesimuluju vlastni betl/durch
                    player1 = _g.GameStartingPlayerIndex;
                    player2 = (_g.GameStartingPlayerIndex + 1) % Game.NumPlayers;
                    player3 = (_g.GameStartingPlayerIndex + 2) % Game.NumPlayers;
                    playerName = _g.players[player1].Name;
                    playerIndex = player1;
                    teamMateIndex = _g.players[player1].TeamMateIndex;
                }
                else
                {
					//simuluju vlastni betl/durch (jestli ma cenu hlasit spatnou barvu)
                    player1 = PlayerIndex;
                    player2 = (PlayerIndex + 1) % Game.NumPlayers;
                    player3 = (PlayerIndex + 2) % Game.NumPlayers;
                    playerName = _g.players[player1].Name;
                    playerIndex = player1;
                    teamMateIndex = TeamMateIndex;
                }
            }
            else if (c2 == null)
            {
                player1 = (PlayerIndex + 2) % Game.NumPlayers;
                player2 = PlayerIndex;
                player3 = (PlayerIndex + 1) % Game.NumPlayers;
                playerName = _g.players[player2].Name;
                playerIndex = player2;
                teamMateIndex = TeamMateIndex;
            }
            else
            {
                player1 = (PlayerIndex + 1) % Game.NumPlayers;
                player2 = (PlayerIndex + 2) % Game.NumPlayers;
                player3 = PlayerIndex;
                playerName = _g.players[player3].Name;
                playerIndex = player3;
                teamMateIndex = TeamMateIndex;
            }

            if (!gameType.HasValue || !initialRoundNumber.HasValue || !roundsToCompute.HasValue)
            {
                gameType = _g.GameType;
                initialRoundNumber = _g.RoundNumber;
                roundsToCompute = Settings.RoundsToCompute;
            }
            if(!trump.HasValue && (gameType & (Hra.Betl | Hra.Durch)) == 0)
            {
                trump = _g.trump;
            }
            x.Append(hands[0].ToString() + "\n" + hands[1].ToString() + "\n" + hands[2].ToString() + "\n" + "\n");

            var useGeneratedHandsForProbabilities = roundsToCompute > 1;        //initial game simulations
            var prob = Probabilities.Clone();
            if (useGeneratedHandsForProbabilities)                              //all probabilities are based on generated hands (either 0 or 1)
            {
                prob.Set(hands);
            }
            prob.UseDebugString = false;    //otherwise we are being really slooow
            var teamMatesSuits = new List<Barva>();
            foreach (var suit in _teamMatesSuits)
            {
                teamMatesSuits.Add(suit);
            }
            var aiStrategy = AiStrategyFactory.GetAiStrategy(_g, gameType, trump, hands, _g.rounds, teamMatesSuits, prob, playerName, playerIndex, teamMateIndex, initialRoundNumber);
            
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
                    //hlas123
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
                hands[player1].Remove(c1);
                hands[player2].Remove(c2);
                hands[player3].Remove(c3);
                Check(hands);
                var hlas1 = _trump.HasValue && c1.Value == Hodnota.Svrsek && hands[player1].HasK(c1.Suit);
                var hlas2 = _trump.HasValue && c2.Value == Hodnota.Svrsek && hands[player2].HasK(c2.Suit);
                var hlas3 = _trump.HasValue && c3.Value == Hodnota.Svrsek && hands[player3].HasK(c3.Suit);
                if (useGeneratedHandsForProbabilities)
                {
                    prob.Set(hands);
                }
                else
                {
                    UpdateProbabilitiesAfterCardPlayed(prob, aiStrategy.RoundNumber, roundStarterIndex, c1, null, null, hlas1, hlas2, hlas3, TeamMateIndex, _teamMatesSuits, _teamMateDoubledGame);
                    UpdateProbabilitiesAfterCardPlayed(prob, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, null, hlas1, hlas2, hlas3, TeamMateIndex, _teamMatesSuits, _teamMateDoubledGame);
                    UpdateProbabilitiesAfterCardPlayed(prob, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, c3, hlas1, hlas2, hlas3, TeamMateIndex, _teamMatesSuits, _teamMateDoubledGame);
                }
                aiStrategy.MyIndex = roundWinnerIndex;
                aiStrategy.TeamMateIndex = _g.players[roundWinnerIndex].TeamMateIndex;
                player1 = aiStrategy.MyIndex;
                player2 = (aiStrategy.MyIndex + 1) % Game.NumPlayers;
                player3 = (aiStrategy.MyIndex + 2) % Game.NumPlayers;
                AmendGameComputationResult(result, roundStarterIndex, roundWinnerIndex, roundScore, hands, c1, c2, c3);
                Check(hands);
                _log.TraceFormat("Score: {0}/{1}/{2}", result.Score[0], result.Score[1], result.Score[2]);
            }

            _log.DebugFormat("Round {0}. Finished simulation for {1}. Card/rule to play: {2} - {3}, expected score in the end: {4}/{5}/{6}\n",
                _g.RoundNumber, _g.players[PlayerIndex].Name, result.CardToPlay, result.Rule.Description, result.Score[0], result.Score[1], result.Score[2]);

            return result;
        }

        private void Check(Hand[] hands)
        {
            if (hands[0].Count() != hands[1].Count() || hands[0].Count() != hands[2].Count())
            {
                throw new InvalidOperationException("Wrong hands count");
            }
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
				//muzu hrat jen A nebo X nebo naopak ani jedna karta kterou muzu hrat neni A ani X
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
						//muzu hrat jen jednu barvu ve ktere souperi nemaji ani A ani X
                        return true;
                    }
                }
                else
                {
                    var opponent = TeamMateIndex == player1 ? player2 : player1;

                    if (Probabilities.CardProbability(opponent, A) == 0f &&
                        Probabilities.CardProbability(opponent, X) == 0f)
                    {
						//muzu hrat jen jednu barvu ve ktere souper nema ani A ani X
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
