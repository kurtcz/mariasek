using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Mariasek.Engine.Logger;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Mariasek.Engine.Schema;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices.ComTypes;
using System.Data;

namespace Mariasek.Engine
{
    public class AiPlayer : AbstractPlayer, IStatsPlayer
    {
#if !PORTABLE
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
#else
        private static readonly ILog _log = new DummyLogWrapper();
#endif   
        public Barva? _trump;
        public Hra? _gameType;
        public List<Card> _talon; //public so that HumanPlayer can set it
        private float _avgWinForHundred;
        private float _avgBasicPointsLost;
        private float _maxBasicPointsLost;
        private bool _hundredOverBetl;
        private bool _hundredOverDurch;
        private int _minWinForHundred;
        private int _maxMoneyLost;
        private int _gamesBalance;
        private int _hundredsBalance;
        private int _hundredsAgainstBalance;
        private int _sevensBalance;
        private int _sevensAgainstBalance;
        private int _betlBalance;
        private int _durchBalance;
        private int _gameSimulations;
        private int _sevenSimulations;
        private int _hundredSimulations;
        private int _betlSimulations;
        private int _durchSimulations;
        private bool _initialSimulation;
        private bool _runSimulations;
        private bool _rerunSimulations;
        private bool _teamMateDoubledGame;
        private bool _teamMateDoubledSeven;
        private bool _shouldMeasureThroughput;
        private List<Barva> _teamMatesSuits;
        
        private ParallelOptions options = new ParallelOptions
        {
            //MaxDegreeOfParallelism = 1  //Uncomment before debugging
            //MaxDegreeOfParallelism = 2 * Environment.ProcessorCount - 1
        };
		public bool AdvisorMode { get; set; }
		private Card _trumpCard;
		public Card TrumpCard
		{
			get { return _trumpCard; }
			set { _trumpCard = value; _trump = value != null ? (Barva?)value.Suit : null; }
		}
        public Func<IStringLogger> _stringLoggerFactory { get; set; }
		public IStringLogger _debugString { get; set; }
        public string _minMaxDebugString { get; set; }
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
                RuleThresholdForGameType = new Dictionary<Hra, float> { { Hra.Kilo, 0.99f } },
                GameThresholds = new[] { 0.75f, 0.8f, 0.85f, 0.9f, 0.95f },
                GameThresholdsForGameType = new Dictionary<Hra, float[]>
                {
                    { Hra.Hra, new[] { 0.00f, 0.50f, 0.65f, 0.80f, 0.95f } },
                    { Hra.Sedma, new[] { 0.75f, 0.80f, 0.85f, 0.90f, 0.95f } },
                    { Hra.SedmaProti, new[] { 0.75f, 0.80f, 0.85f, 0.90f, 0.95f } },
                    { Hra.Kilo, new[] { 0.80f, 0.85f, 0.90f, 0.95f, 0.99f } },
                    { Hra.KiloProti, new[] { 0.95f, 0.96f, 0.97f, 0.98f, 0.99f } },
                    { Hra.Betl, new[] { 0.75f, 0.80f, 0.85f, 0.90f, 0.95f } },
                    { Hra.Durch, new[] { 0.80f, 0.85f, 0.90f, 0.95f, 0.99f } }
                },
                MaxDoubleCountForGameType = new Dictionary<Hra, int>
                {
                    { Hra.Hra, 3 },
                    { Hra.Sedma, 3 },
                    { Hra.SedmaProti, 2 },
                    { Hra.Kilo, 3 },
                    { Hra.KiloProti, 0 },
                    { Hra.Betl, 3 },
                    { Hra.Durch, 3 }
                },
                CanPlayGameType = new Dictionary<Hra, bool>
                {
                    { Hra.Hra, true },
                    { Hra.Sedma, true },
                    { Hra.SedmaProti, true },
                    { Hra.Kilo, true },
                    { Hra.KiloProti, false },
                    { Hra.Betl, true },
                    { Hra.Durch, true }
                },
                SigmaMultiplier = 0,
                GameFlavourSelectionStrategy = GameFlavourSelectionStrategy.Standard,
                RiskFactor = 0.275f,
                RiskFactorSevenDefense = 0.5f,
                SolitaryXThreshold = 0.13f,
                SolitaryXThresholdDefense = 0.5f,
                SafetyGameThreshold = 40,
                SafetyHundredThreshold = 80,
                SafetyBetlThreshold = g.CalculationStyle == CalculationStyle.Adding ? 48 : 128
            };
            _log.InfoFormat("AiPlayerSettings:\n{0}", Settings);

            _runSimulations = true;
            _debugString = g.GetStringLogger();//g.DebugString;
            _teamMatesSuits = new List<Barva>();
            DebugInfo = new PlayerDebugInfo();
            g.GameLoaded += GameLoaded;
            g.GameFlavourChosen += GameFlavourChosen;
            g.GameTypeChosen += GameTypeChosen;
            g.BidMade += BidMade;
            g.CardPlayed += CardPlayed;
            ThrowIfCancellationRequested = g.ThrowIfCancellationRequested;
        }

        public AiPlayer(Game g, AiPlayerSettings settings) : this(g)
        {
            _stringLoggerFactory = () => new StringLogger(false);// b);

            Settings = settings;
            _teamMatesSuits = new List<Barva>();
        }

		public override void Die()
		{
			ThrowIfCancellationRequested = null;
			base.Die();
		}

		private int GetSuitScoreForTrumpChoice(List<Card> hand, Barva b)
        {
            var score = 0;
            var count = hand.Count(i => i.Suit == b);

            if (count > 1)
            {
                if (hand.HasK(b) &&
                    hand.HasQ(b) &&
                    count == 2)
                {
                    score += 30;
                }
                else
                {
                    if (hand.HasK(b))
                        score += 20;
                    if (hand.HasQ(b))
                        score += 20;
                }
                if (hand.HasA(b))
                    score += 10;
                if (hand.HasX(b))
                    score += 10;
                if (hand.Has7(b) && count >= 4)
                    score += count * 10;
                else if (hand.Has7(b) && count == 3)
                    score += 15;
                else if (hand.Has7(b) && count == 2)
                    score += 10;
                else if (count >= 4)
                    score += 15;
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
            var hh = new List<Card>(hand);
            return ChooseBetlTalonImpl(hh, trumpCard);
        }

        private List<Card> ChooseBetlTalonImpl(List<Card> hand, Card trumpCard)
        {
            //nedavej do talonu karty v barve kde mam 7 karet vcetne esa a sedmy
            var bannedSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => hand.CardCount(b) == 7 &&
                                              hand.HasA(b) &&
                                              hand.Has7(b))
                                  .ToList();

			var holesByCard = hand.Select(i =>
			{
				//pro kazdou kartu spocitej diry (mensi karty v barve ktere nemam)
				var holes = 0;
				var holesDelta = 0;
                var higherCards = 0;

				foreach (var h in Enum.GetValues(typeof(Hodnota))
									  .Cast<Hodnota>()
                                      .Where(h => i.BadValue > Card.GetBadValue(h)))
				{
					if (!hand.Any(j => j.Suit == i.Suit && j.Value == h))
					{
						holes++;
					}
				}
                foreach (var h in Enum.GetValues(typeof(Hodnota))
                                      .Cast<Hodnota>()
                                      .Where(h => i.BadValue < Card.GetBadValue(h)))
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

            if (holesByCard.Count(i => !bannedSuits.Contains(i.Item1.Suit)) > 2)
            {
                holesByCard = holesByCard.Where(i => !bannedSuits.Contains(i.Item1.Suit));
            }
            else
            {
                bannedSuits.Clear();
            }
            //nejprve vem karty ktere jsou ve hre nejvyssi a tudiz nejvice rizikove (A, K)
            var talon = holesByCard.Where(i => (i.Item2 < 6 &&
                                                i.Item5 == 0) ||
                                               (i.Item1.Value == Hodnota.Eso &&
                                                i.Item2 == 6 &&
                                                !hand.HasK(i.Item1.Suit)))
                                   .OrderByDescending(i => i.Item1.BadValue)
                                   .ThenBy(i => i.Item2)
                                   .Take(2)
                                   .Select(i => i.Item1)                    //Card
                                   .ToList();

            if (talon.Count < 2)
            {
                //pak vezmi karty od barev kde mas 2-3 prostredni karty s 2 dirama
                var temp = holesByCard.Where(i => !talon.Contains(i.Item1) &&
                                                  i.Item2 >= 2 &&			    //CardCount
                                                  i.Item2 <= 3 &&			    //CardCount
                                                  i.Item4 >= 2)			        //holes
                                      .Select(i => i.Item1)                     //Card
                                      .ToList();    //
                temp = holesByCard.Where(i => !talon.Contains(i.Item1) &&
                                              i.Item2 >= 2 &&               //CardCount
                                              i.Item2 <= 3 &&               //CardCount
                                              i.Item4 >= 2)                 //holes
                                  .OrderByDescending(i => temp.CardCount(i.Item1.Suit))     //snaz se vybrat barvu kde je vic karet kde me muzou chytit
                                  .ThenByDescending(i => i.Item1.BadValue)
                                  .Take(2 - talon.Count)
                                  .Select(i => i.Item1)                     //Card
                                  .ToList();
                //pokud jsou jen 2 karty s dirou, tak se zbav jedne z nich
                //zbyvajici druhou kartou potom betl zahajis a bude lozeny
                //pritom se zhorsi talon na pripadneho durcha
                if (holesByCard.Count() == 2)
                {
                    temp = holesByCard.Where(i => !talon.Contains(i.Item1))
                                      .OrderByDescending(i => i.Item4)
                                      .ThenBy(i => i.Item1.BadValue)
                                      .Take(1)
                                      .Select(i => i.Item1)
                                      .ToList();
                }
                //pokud jsou jen 3 karty s dirou ve dvou barvach, tak se zbav dvou karet stejne barvy
                //zbyvajici treti kartou potom betl zahajis a bude lozeny
                //pritom se zhorsi talon na pripadneho durcha
                if (holesByCard.Count() == 3 &&
                    holesByCard.Select(i => i.Item1).SuitCount() == 2)
                {
                    temp = holesByCard.Where(i => !talon.Contains(i.Item1) &&
                                                  holesByCard.Select(j => j.Item1)
                                                             .CardCount(i.Item1.Suit) == 2)
                                      .Select(j => j.Item1)
                                      .ToList();
                }
                //pokud jsou jen 4 karty s dirou ve max trech barvach
                //a jen max. 3 karty s vic nez jednou dirou, tak se zbav dvou karet stejne barvy
                //zbyvajici treti kartou potom betl zahajis a bude skoro lozeny - zbyde jen osmicka
                //pritom se zhorsi talon na pripadneho durcha
                if (holesByCard.Count() == 4 &&
                    holesByCard.Select(i => i.Item1).SuitCount() <= 3 &&
                    holesByCard.Count(i => i.Item4 > 1) <= 3)
                {
                    temp = holesByCard.Where(i => !talon.Contains(i.Item1) &&
                                                  holesByCard.Select(j => j.Item1)
                                                             .CardCount(i.Item1.Suit) >= 2)
                                      .Select(i => i.Item1)
                                      .OrderByDescending(i => i.BadValue)
                                      .ToList();
                }
                //pokud mas vsechny karty s dirou v jedne barve, tak vezmi druhe dve nejvyssi (nejvyssi karty se zbavis v 1. kole)
                if (holesByCard.Count() > 2 &&
                    holesByCard.Select(i => i.Item1).SuitCount() == 1 &&
                    holesByCard.All(i => i.Item5 > 0))
                {
                    talon.AddRange(holesByCard.Where(i => !talon.Contains(i.Item1))
                                              .Select(i => i.Item1)
                                              .OrderByDescending(i => i.BadValue)
                                              .Skip(1));
                }
                //pokud mas nejake vyssi plonky, tak pouzij radsi ty
                else if (temp.Count > 1)
                {
                    var higherSoloCards = holesByCard.Where(i => !talon.Contains(i.Item1) &&
                                                                 i.Item2 == 1 &&    //CardCount
                                                                 temp.Any(j => j.BadValue < i.Item1.BadValue))
                                                     .Select(i => i.Item1)          //Card
                                                     .ToList();
                    if (higherSoloCards.Count > 1)
                    {
                        var temp2 = temp.Concat(higherSoloCards)
                                        .OrderByDescending(i => i.BadValue)
                                        .Take(Math.Min(2 - talon.Count, higherSoloCards.Count - 1))
                                        .ToList();
                        talon.AddRange(temp2);
                    }
                    talon.AddRange(temp);
                }
                else
                {
                    talon.AddRange(temp);
                }
            }
            if (talon.Count < 2)
            {
                //potom vezmi karty od nejkratsich barev a alespon stredni hodnoty (1 karta > osma)
                //radime podle poctu poctu der ktere odstranime sestupne, poctu der celkem sestupne a hodnoty karty sestupne
                talon.AddRange(holesByCard.Where(i => i.Item2 == 1 &&			    //CardCount
                                                      i.Item5 > 0 &&                //HigherCards
                                                      !talon.Contains(i.Item1) &&
                                                      i.Item1.Value > Hodnota.Osma)
                                          .OrderByDescending(i => i.Item3)          //holesDelta
                                          .ThenByDescending(i => i.Item4)           //holes
                                          .ThenByDescending(i => i.Item1.BadValue)
                                          .Take(2 - talon.Count)
                                          .Select(i => i.Item1)				//Card
                                          .ToList());
            }
            if (talon.Count < 2)
			{
                //dopln kartami od delsich barev
                var top2HolesDeltaPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                              .ToDictionary(b => b, b => holesByCard.Where(i => i.Item1.Suit == b)
                                                                                    .OrderByDescending(i => i.Item1.BadValue)
                                                                                    .Select(i => i.Item3)
                                                                                    .Take(2)
                                                                                    .Sum());

                talon.AddRange(holesByCard.Where(i => i.Item2 < 7 &&  !talon.Contains(i.Item1))		//Card
				               		   	  .OrderByDescending(i => top2HolesDeltaPerSuit[i.Item1.Suit])//i.Item3)			//holesDelta
				               			  .ThenByDescending(i => i.Item4)			//holes
									   	  .ThenByDescending(i => i.Item1.BadValue)
									   	  .Select(i => i.Item1)						//Card
									   	  .ToList());
                //pokud bys mel dat do talonu dve karty stejne barvy a muzes misto jedne dat jinou barvu, tak si radsi nech jednu z dvojice na vynos
                if(talon.Count > 2 &&
                   talon[0].Suit == talon[1].Suit &&
                   hand.CardCount(talon[0].Suit) == 2 &&
                   top2HolesDeltaPerSuit.Count(kvp => kvp.Value > 0) > 1 &&
                   !talon.Skip(2).Any(i => i.Suit != talon[0].Suit &&
                                           i.Value > Hodnota.Sedma &&
                                           hand.CardCount(i.Suit) == 1))
                {
                    talon.RemoveAt(1);
                }
                talon = talon.Take(2).ToList();
            }
            //pokud je potreba, doplnime o nejake nizke karty (abych zhorsil talon na durcha)
            if (talon.Count < 2)
            {
                talon.AddRange(hand.Where(i => !bannedSuits.Contains(i.Suit) &&
                                               !talon.Contains(i) &&
                                               !(hand.CardCount(i.Suit) == 7 &&
                                                 !hand.Has8(i.Suit) &&
                                                 i.Value == Hodnota.Sedma))
                                   .OrderBy(i => i.BadValue)
                                   .ThenByDescending(i => hand.CardCount(i.Suit))
                                   .Take(2 - talon.Count));
            }
            //pokud to jde najdi nizsi karty se stejnymi parametry (abych souperi stizil pripadneho durcha)
            var cardsToReplaceDict = new Dictionary<int, Card>();
            foreach (var card in talon)
            {
                var cardToReplace = holesByCard.Where(i => !cardsToReplaceDict.Values.Contains(i.Item1) &&
                                                           i.Item2 < 7 &&
                                                           i.Item1.Suit == card.Suit &&
                                                           i.Item1.BadValue < card.BadValue &&
                                                           i.Item4 == holesByCard.First(j => j.Item1 == card).Item4)
                                               .OrderBy(i => i.Item1.BadValue)
                                               .Select(i => new KeyValuePair<int, Card>(talon.IndexOf(card), i.Item1))
                                               .FirstOrDefault();
                if (cardToReplace.Value != null &&
                    talon.IndexOf(cardToReplace.Value) > 1)
                {
                    cardsToReplaceDict.Add(cardToReplace.Key, cardToReplace.Value);
                }
            }
            foreach (var kvp in cardsToReplaceDict)
            {
                talon.RemoveAt(kvp.Key);
                talon.Insert(kvp.Key, kvp.Value);
            }
            talon = talon.Distinct().Take(2).ToList();
            var cardsToReplace = new List<Card>();
            var lowHoles = holesByCard.Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                      .Select(h => new Card(i.Item1.Suit, h))
                                                      .Where(j => j.BadValue > i.Item1.BadValue)
                                                      .Any(j => !hand.Contains(j)))
                                      .Select(i => i.Item1)
                                      .ToList();
            if (lowHoles.Count >= 1 &&
                holesByCard.Count() <= 2)
            {
                //jen dve nebo jedna karta ma diru
                //jednu si necham na ruce (s tou betla zacnu) a do talonu dam misto ni neco nizkeho
                cardsToReplace = hand.Where(i => !talon.Contains(i) &&
                                                 !lowHoles.Contains(i))
                                     .OrderByDescending(i => hand.CardCount(i.Suit))
                                     .ThenBy(i => i.BadValue)
                                     .Take(1)
                                     .ToList();
                talon = cardsToReplace.Concat(talon)
                                      .OrderBy(i => lowHoles.Contains(i) ? 1 : 0)
                                      .Distinct()
                                      .Take(2)
                                      .ToList();
            }
            if (talon == null || talon.Distinct().Count() != 2)
			{
                var msg = talon == null ? "(null)" : string.Format("Count: {0}\nHand: {1}", talon.Distinct().Count(), new Hand(hand));
				throw new InvalidOperationException("Bad talon: " + msg);
			}
            return talon;
        }

        private List<Card> ChooseDurchTalon(List<Card> hand, Card trumpCard)
        {
            var hh = new List<Card>(hand);
            return ChooseDurchTalonImpl(hh, trumpCard);
        }

        private List<Card> ChooseDurchTalonImpl(List<Card> hand, Card trumpCard)
        {
            var talon = hand.Where(i => hand.CardCount(i.Suit) <= 2 && //nejdriv zkus odmazat kratkou barvu ve ktere nemam eso
                                        !hand.HasA(i.Suit))
                            .Take(2)
                            .ToList();
            var holesPerSuit = new Dictionary<Barva, int>();
            var topCards = new List<Card>();

            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                var hiCards = hand.Count(i => i.Suit == b &&
                                              Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                  .Select(h => new Card(i.Suit, h))
                                                  .All(j => j.BadValue < i.BadValue ||
                                                            hand.Contains(j))); //pocet nejvyssich karet v barve
                var loCards = hand.CardCount(b) - hiCards;                      //pocet karet ktere maji nad sebou diru v barve
                var opCards = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()   //vsechny hodnoty ktere v dane barve neznam
                                .Where(h => !hand.Any(i => i.Suit == b &&
                                                           i.BadValue == Card.GetBadValue(h)))
                                .ToList();
                if (opCards.Count() >= hiCards)                                 //odstran tolik nejmensich hodnot kolik mam nejvyssich karet
                {
                    opCards = opCards.OrderBy(h => Card.GetBadValue(h)).Skip(hiCards).ToList();
                }
                else
                {
                    opCards.Clear();
                }
                var holes = opCards.Count(h => hand.Any(i => i.Suit == b &&     //pocet zbylych der vyssich nez moje nejnizsi karta plus puvodni dira (eso)
                                                             i.BadValue < Card.GetBadValue(h)));
                var n = Math.Min(holes, loCards);

                holesPerSuit.Add(b, n);
                topCards.AddRange(hand.Where(i => i.Suit == b)
                                      .OrderByDescending(i => i.BadValue)
                                      .Take(hiCards));
            }
            talon.AddRange(hand.Where(i => !talon.Contains(i))
                               .OrderByDescending(i => holesPerSuit[i.Suit])
                               .ThenBy(i => topCards.Contains(i) ? 1 : 0)   //snaz se nebrat karty co nad sebou nemaji diru
                               .ThenBy(i => hand.CardCount(i.Suit))         //prednostne ber kratsi barvy
                               .ThenBy(i => i.BadValue)                     //a nizsi karty
                               .Take(2 - talon.Count()));
            var count = talon.Count();

            //pokud je potreba, doplnime o nejake nizke karty
            if (count < 2)
            {
                talon.AddRange(hand.Where(i => !talon.Contains(i)).OrderBy(i => i.BadValue).Take(2 - count));
            }

			if (talon == null || talon.Distinct().Count() != 2)
			{
				var msg = talon == null ? "(null)" : string.Format("Count: {0}\nHand: {1}", talon.Distinct().Count(), new Hand(hand));
				throw new InvalidOperationException("Bad talon: " + msg);
			}

			//mozna by stacilo negrupovat podle barev ale jen sestupne podle hodnot a vzit prvni dve?
			return talon;
        }

        private List<Card> ChooseNormalTalon(List<Card> hand, Card trumpCard)
        {
            var hh = new List<Card>(hand);
            return ChooseNormalTalonImpl(hh, trumpCard);
        }

        private List<Card> ChooseNormalTalonImpl(List<Card> hand, Card trumpCard)
        {
            var talon = new List<Card>();
            var kqScore = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                              .Where(b => Hand.HasK(b) && Hand.HasQ(b))
                              .Sum(b => b == trumpCard.Suit ? 40 : 20);

            if (trumpCard == null)
            {
                trumpCard = hand.OrderByDescending(i => hand.CardCount(i.Suit))
                                .ThenByDescending(i => i.Value).First();
            }
            if (_g.AllowAXTalon)
            {
                //nejdriv se zbav plonkovych desitek (pokud se to smi)
                talon.AddRange(hand.Where(i => i.Value == Hodnota.Desitka &&
                                               i.Suit != trumpCard.Suit &&
                                               hand.HasSolitaryX(i.Suit)));
            }
            //pokud jsi zvolil spatne a hrozi sedma proti ale ne kilo proti
            if (hand.CardCount(trumpCard.Suit) <= 3 &&
                !hand.Has7(trumpCard.Suit) &&
                !hand.HasA(trumpCard.Suit) &&
                hand.CardCount(Hodnota.Eso) + hand.CardCount(Hodnota.Desitka) <= 3 &&
                Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    .Where(b => b != trumpCard.Suit)
                    .Any(b => hand.CardCount(b) >= 4))
            {
                var basicPointsLost = EstimateBasicPointsLost(hand, new List<Card>());
                var totalPointsLost = EstimateMaxTotalPointsLost(hand, new List<Card>());
                var noKQSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                    .Count(b => !hand.HasK(b) &&
                                                !hand.HasQ(b));
                if (90 - basicPointsLost + kqScore < basicPointsLost &&
                    totalPointsLost < (noKQSuits <= 1 ? 100 :120))
                {
                    var longSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                       .Where(b => b != trumpCard.Suit &&
                                                   hand.CardCount(b) >= 4)
                                       .OrderByDescending(b => hand.CardCount(b))
                                       .First();
                    talon.AddRange(hand.Where(i => i.Suit == longSuit &&
                                                   i.Value > Hodnota.Sedma &&
                                                   i.Value < Hodnota.Svrsek)
                                       .Take(2));
                }
            }
            //nejdriv zkus vzit karty v barve kde krom esa mam max 2 plivy (a nemam hlasku)
            talon.AddRange(hand.Where(i => i.Suit != trumpCard.Suit &&             //nevybirej trumfy
                                           i.Value != Hodnota.Eso &&               //ani esa
                                           hand.Count(j => i.Suit == j.Suit &&     //max. 2 karty jine nez A
                                                           j.Value != Hodnota.Eso) <= 2 &&
                                           !hand.HasX(i.Suit) &&                   //v barve kde neznam X ani nemam hlas
                                           !(hand.HasK(i.Suit) && hand.HasQ(i.Suit)))
                               .OrderBy(i => hand.CardCount(i.Suit))               //vybirej od nejkratsich barev
                               .ThenBy(i => hand.HasA(i.Suit)
                                            ? hand.CardCount(i.Suit) == 2       //v pripade stejne delky barev:
                                                ? 2 : 0                         //u dvou karet dej prednost barve bez esa
                                            : 1)                                //u tri karet dej prednost barve s esem
                               .ThenBy(i=> i.Suit));

            //potom zkus vzit plivy od barvy kde mam A + X + plivu
            talon.AddRange(hand.Where(i => i.Suit != trumpCard.Suit &&
                                           i.Value != Hodnota.Eso &&
                                           i.Value != Hodnota.Desitka &&
                                           hand.HasA(i.Suit) &&
                                           hand.HasX(i.Suit) &&
                                           hand.CardCount(i.Suit) == 3)
                               .OrderBy(i => hand.CardCount(i.Suit))
                               .ThenByDescending(i => i.BadValue));  //vybirej od nejkratsich barev

            //potom zkus vzit karty v barve kde krom esa mam 3 plivy (a nemam hlasku)
            talon.AddRange(hand.Where(i => i.Suit != trumpCard.Suit &&             //nevybirej trumfy
                                           !hand.HasA(i.Suit) &&                   //v barve kde neznam A, X ani nemam hlas
                                           !hand.HasX(i.Suit) &&                   //popr. kdyz mam hlas, tak vyber nizsi kartu
                                           (i.Value < Hodnota.Svrsek ||
                                            !(hand.HasK(i.Suit) &&
                                              hand.HasQ(i.Suit))))
                               .OrderBy(i => hand.CardCount(i.Suit))
                               .ThenByDescending(i => i.BadValue));  //vybirej od nejkratsich barev

            //pokud mas X + 2 plivy, tak vezmi tu mensi
            var c = hand.Where(i => //!(i.Value == trumpCard.Value &&         //nevybirej trumfovou kartu
                                    //  i.Suit == trumpCard.Suit) &&
                                    i.Suit != trumpCard.Suit &&
                                    i.Value != Hodnota.Eso &&             //ani A,X
                                    i.Value != Hodnota.Desitka &&
                                    !((i.Value == Hodnota.Kral ||         //ani hlasy
                                       i.Value == Hodnota.Svrsek) &&
                                      hand.HasK(i.Suit) && hand.HasQ(i.Suit)) &&
                                     !(i.Value == Hodnota.Sedma &&         //ani trumfovou sedmu
                                       i.Suit == trumpCard.Suit) &&
                                      hand.HasX(i.Suit) &&                 //pokud mam jen X+2 plivy, viz nasl. krok
                                      !hand.HasA(i.Suit) &&
                                      hand.CardCount(i.Suit) == 3 &&
                                      !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                           .Where(b => b != trumpCard.Suit)
                                           .Any(b => hand.CardCount(b) >= 5) &&
                                      (hand.HasK(i.Suit) ||
                                       !(hand.CardCount(trumpCard.Suit) >= 5 &&
                                         hand.All(j => j.Suit == _trump ||   //pri kilu pokud mas jen trumfy nebo ostre karty
                                                       j.Suit == i.Suit ||   //a v jedne barve X+2 (bez krale)
                                                       (j.Suit != i.Suit &&  //je lepsi se zbavit trumfu
                                                        (j.Value >= Hodnota.Desitka ||
                                                         (j.Value == Hodnota.Kral &&
                                                          hand.HasQ(j.Suit)) ||
                                                         (j.Value == Hodnota.Svrsek &&
                                                          hand.HasK(j.Suit))))))))
                         .OrderBy(i => i.Value)
                        .FirstOrDefault();

            if (c != null)
            {
                talon.Add(c);
            }
            var lowCardsPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                      .ToDictionary(b => b, b => hand.FirstOrDefault(i => i.Suit == b &&
                                                                                          i.Value < Hodnota.Desitka &&
                                                                                          hand.Where(j => j.Suit == i.Suit)
                                                                                              .All(j => j.Value >= i.Value)));
            var lowCardCountsPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                           .ToDictionary(b => b, b => hand.Count(i => i.Suit == b &&
                                                                                      i.Value < Hodnota.Desitka));
            //potom zkus cokoli mimo trumfu,A,X,7, hlasu a samotne plivy ktera doplnuje X
            talon.AddRange(hand.Where(i => i.Suit != trumpCard.Suit &&             //nevybirej trumfy
                                           i.Value != Hodnota.Eso &&               //ani A,X
                                           i.Value != Hodnota.Desitka &&
                                           !((i.Value == Hodnota.Kral ||           //ani hlasy
                                              i.Value == Hodnota.Svrsek) &&
                                             hand.HasK(i.Suit) && hand.HasQ(i.Suit)) &&
                                           (i != lowCardsPerSuit[i.Suit] ||        //ani nejnizsi kartu
                                            (hand.CardCount(i.Suit) == 3 &&        //(vyjma situace kdy mam prave X,K,7)
                                             hand.HasX(i.Suit) &&
                                             hand.HasK(i.Suit))) &&
                                           !(hand.HasX(i.Suit) &&                  //ani pokud mam jen X+plivu
                                             hand.CardCount(i.Suit) <= 2) &&
                                           !(hand.HasX(i.Suit) &&                  //nebo pokud mam jen X+2 plivy
                                             !hand.HasA(i.Suit) &&
                                             hand.CardCount(i.Suit) == 3) &&
                                           !(hand.HasX(i.Suit) &&                  //nebo pokud mam jen X+plivy a v jine barve mam A+X+plivy
                                             !hand.HasA(i.Suit) &&
                                             !(hand.HasK(i.Suit) &&
                                               hand.HasQ(i.Suit)) &&
                                             hand.CardCount(i.Suit) <= 4 &&
                                             hand.Any(j => j.Suit != trumpCard.Suit &&
                                                           j.Suit != i.Suit &&
                                                           hand.HasA(j.Suit) &&
                                                           hand.HasX(j.Suit) &&
                                                           !(hand.HasK(j.Suit) &&
                                                             hand.HasQ(j.Suit)) &&
                                                           hand.CardCount(j.Suit) >= 4)))
                                .OrderBy(i => i.BadValue));                           //vybirej od nejmensich karet

            //potom zkus cokoli mimo trumfu,A,X,trumfove 7, hlasu a samotne plivy ktera doplnuje X
            //talon.AddRange(hand.Where(i => !(i.Value == trumpCard.Value &&         //nevybirej trumfovou kartu
                                //             i.Suit == trumpCard.Suit) &&
                                //           i.Value != Hodnota.Eso &&               //ani A,X
                                //           i.Value != Hodnota.Desitka &&
                                //           !((i.Value == Hodnota.Kral ||           //ani hlasy
                                //               i.Value == Hodnota.Svrsek) &&
                                //           hand.HasK(i.Suit) && hand.HasQ(i.Suit)) &&
                                //           !(i.Value == Hodnota.Sedma &&           //ani trumfovou sedmu
                                //             i.Suit == trumpCard.Suit) &&
                                //           !(hand.HasX(i.Suit) &&                  //pokud mam jen X+plivu, tak nech plivu byt
                                //             hand.CardCount(i.Suit) <= 2) &&
                                //           !(hand.HasX(i.Suit) &&                  //nebo pokud mam jen X+2 plivy
                                //             !hand.HasA(i.Suit) &&
                                //             hand.CardCount(i.Suit) == 3))
                                //.OrderByDescending(i => i.Value));               //vybirej od nejvetsich karet (spodek -> sedma)
            //potom zkus netrumfove 7 pokud nejsou s desitkou nebo je v barve dost karet
            talon.AddRange(hand.Where(i => i.Suit != trumpCard.Suit &&
                                           i.Value == Hodnota.Sedma &&
                                           !(hand.HasX(i.Suit) &&                  //pokud mam jen X+plivu, tak nech plivu byt
                                             hand.CardCount(i.Suit) <= 2) &&
                                           !(hand.HasX(i.Suit) &&                  //nebo pokud mam jen X+2 plivy
                                             !hand.HasA(i.Suit) &&
                                             hand.CardCount(i.Suit) == 3)));

            //potom pokud mam ve vsech barvach same nejvyssi karty a v jedne barve jen K,S
            //tak obetuj samotne K,S v netrumfove barve
            if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    .Where(b => hand.HasSuit(b))
                    .All(b => (hand.HasK(b) && hand.HasQ(b) && hand.CardCount(b) == 2) ||                              
                              hand.Where(i => !(i.Value == trumpCard.Value &&         //nevybirej trumfovou kartu
                                                i.Suit == trumpCard.Suit) &&
                                              i.Suit == b)
                                  .All(i => i.Value >= Hodnota.Svrsek &&
                                            Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                .Where(h => h > i.Value)
                                                .Select(h => new Card(i.Suit, h))
                                                .All(j => hand.Any(k => j == k)))))
            {
                talon.AddRange(hand.Where(i => i.Suit != trumpCard.Suit &&
                                               hand.HasK(i.Suit) &&
                                               hand.HasQ(i.Suit) &&
                                               hand.CardCount(i.Suit) == 2)
                                    .OrderBy(i => i.Suit)
                                    .Take(2));
            }

            //potom zkus vzit odspoda kartu v barve kde nemam X bez A (stejna podminka jako vyse, ale bere v potaz i nejnizsi kartu)
            talon.AddRange(hand.Where(i => i.Suit != trumpCard.Suit &&             //nevybirej trumfy
                                           i.Value < Hodnota.Desitka &&            //ani A,X
                                           !((i.Value == Hodnota.Kral ||           //ani hlasy
                                              i.Value == Hodnota.Svrsek) &&
                                             hand.HasK(i.Suit) && hand.HasQ(i.Suit)) &&
                                           !(hand.CardCount(i.Suit) == 3 &&        //(vyjma situace kdy mam prave X,K,7)
                                             hand.HasX(i.Suit) &&
                                             hand.HasK(i.Suit)) &&
                                           !(hand.HasX(i.Suit) &&                  //ani pokud mam jen X+plivu
                                             hand.CardCount(i.Suit) <= 2) &&
                                           !(hand.HasX(i.Suit) &&                  //nebo pokud mam jen X+2 plivy
                                             !hand.HasA(i.Suit) &&
                                             hand.CardCount(i.Suit) == 3))
                                .OrderBy(i => i.BadValue));
            if (hand.All(i => i.Suit == trumpCard.Suit ||       //pokud mas jen trumfy, ostre karty, hlasy nebo X+2 bez krale
                              (i.Suit != trumpCard.Suit &&
                               (i.Value >= Hodnota.Desitka ||
                                (i.Value == Hodnota.Kral &&
                                 hand.HasQ(i.Suit)) ||
                                (i.Value == Hodnota.Svrsek &&
                                 hand.HasK(i.Suit)) ||
                                (hand.HasX(i.Suit) &&
                                 !hand.HasA(i.Suit) &&
                                 !hand.HasK(i.Suit) &&
                                 hand.CardCount(i.Suit) == 3)))))
            {
                if (hand.CardCount(trumpCard.Suit) >= 6 &&          //pri 6+ trumfech dej klidne 2 do talonu
                    !(hand.HasK(trumpCard.Suit) &&                  //neplati kdyz budu hrat sedmu a mam trumfovy flas
                      hand.HasQ(trumpCard.Suit) &&                  //(cili nehrozi flek na hru)
                      hand.Has7(trumpCard.Suit) &&                  //ale nemam dobiraky (esa) resp. mam max. jedno
                      hand.CardCount(Hodnota.Eso) <= 1))
                {
                    talon.AddRange(hand.Where(i => i.Suit == trumpCard.Suit &&
                                                   i != trumpCard &&
                                                   i.Value > Hodnota.Sedma &&
                                                   i.Value < Hodnota.Svrsek)
                                   .OrderBy(i => i.Value)
                                   .Take(2));
                }
            }
            //nakonec cokoli co je podle pravidel
            talon.AddRange(hand.Where(i => !(i.Value == trumpCard.Value &&         //nevybirej trumfovou kartu
                                             i.Suit == trumpCard.Suit) &&
                                           Game.IsValidTalonCard(i.Value, i.Suit, _trumpCard.Suit, _g.AllowAXTalon, _g.AllowTrumpTalon))
                               .OrderBy(i => 0)
                                             //i.Value == Hodnota.Sedma &&
                                             //i.Suit == trumpCard.Suit
                                             //? 2 : (i.Value == Hodnota.Svrsek &&
                                             //       hand.HasK(i.Suit)) ||
                                             //      (i.Value == Hodnota.Kral &&
                                             //       hand.HasQ(i.Suit))                                             
                                             //      ? 1 : 0)
                               .ThenByDescending(i => i.Suit == trumpCard.Suit
                                                       ? -1 : (int)trumpCard.Suit)//nejdriv zkus jine nez trumfy
                               .ThenBy(i => i.Value));                            //vybirej od nejmensich karet

            talon = talon.Distinct().ToList();

            //pokud to vypada na sedmu se 4 kartama nebo na slabou sedmu s 5 kartama, tak se snaz mit vsechny barvy na ruce
            if (hand.Has7(trumpCard.Suit) &&
                ((hand.CardCount(trumpCard.Suit) <= 5 &&
                  hand.Count(i => i.Value >= Hodnota.Desitka) * 10 + kqScore <= 50 &&
                  !hand.HasA(trumpCard.Suit)) ||
                 (hand.CardCount(trumpCard.Suit) == 4 &&
                  hand.Count(i => i.Value >= Hodnota.Desitka) * 10 + kqScore <= 50) ||
                 (hand.CardCount(trumpCard.Suit) == 5 &&
                  !hand.HasA(trumpCard.Suit) &&
                  !(hand.HasX(trumpCard.Suit) &&
                    hand.HasK(trumpCard.Suit)))) &&
                //!(Enum.GetValues(typeof(Barva)).Cast<Barva>()
                //      .Where(b => hand.HasSuit(b) &&
                //                  b != _trump)
                //      .Count(b => !hand.HasA(b)) == 1) &&
                hand.SuitCount() >= 3 &&
                hand.Where(i => !talon.Take(2).Contains(i))
                    .Select(i => i.Suit).Distinct().Count() < hand.SuitCount())
            {
                var topCardPerSuit = hand.Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                         .Where(h => h > i.Value)
                                                         .All(h => !hand.Contains(new Card(i.Suit, h))))
                                         .ToDictionary(k => k.Suit, v => new { TopCard = v, CardCount = hand.CardCount(v.Suit) });

                //u barvy kde mam dve karty se snaz si nechat tu nizsi
                var reducedTalon = talon.Where(i => i.Suit != trumpCard.Suit &&
                                                    i.Value < Hodnota.Desitka &&
                                                    !(hand.HasK(i.Suit) &&
                                                      i.Value == Hodnota.Svrsek) &&
                                                    !(hand.HasQ(i.Suit) &&
                                                      i.Value == Hodnota.Kral) &&
                                                    (topCardPerSuit[i.Suit].CardCount > 2 ||
                                                     (topCardPerSuit[i.Suit].CardCount == 2 &&
                                                      topCardPerSuit[i.Suit].TopCard != i)))
                                        .OrderBy(i => topCardPerSuit[i.Suit].CardCount)
                                        .ThenByDescending(i => i.Value)
                                        .ToList();
                //pouze pokud mi po odebrani v talonu zustane dost karet
                if (reducedTalon.Count >= 2 &&  //reducedTalon.Take(2) je zjednoduseni, nize muzu nekdy vybrat i jine karty
                    !IsSevenTooRisky(hand.Where(i => !reducedTalon.Take(2).Contains(i)).ToList(), reducedTalon.Take(2).ToList()))
                {
                    talon = reducedTalon;
                }
            }
            //dej trumf do talonu pokud bys jinak prisel o hlas
            if (_g.AllowTrumpTalon &&
                //hand.CardCount(trumpCard.Suit) >= 5 &&
                hand.Any(i => i != trumpCard &&
                              i.Suit == trumpCard.Suit &&
                              i.Value < Hodnota.Svrsek) &&
                talon.Count > 2 &&
                talon.Take(2).Any(i => i.Suit != trumpCard.Suit &&
                                       ((i.Value == Hodnota.Kral &&
                                         hand.HasQ(i.Suit)) ||
                                        (i.Value == Hodnota.Svrsek &&
                                       hand.HasK(i.Suit)))) &&
                talon.Count(i => i.Suit != trumpCard.Suit &&
                                 ((i.Value < Hodnota.Svrsek &&
                                   !(hand.HasX(i.Suit) &&   //ignoruj barvu kde mam X+2 plivy
                                     !hand.HasA(i.Suit) &&
                                     hand.CardCount(i.Suit) <= 3)) ||
                                  i.Value > Hodnota.Kral ||
                                  (i.Value == Hodnota.Kral &&
                                   !hand.HasQ(i.Suit)) ||
                                  (i.Value == Hodnota.Svrsek &&
                                   !hand.HasK(i.Suit)))) <= 2 &&
                talon.Where(i => i.Suit != trumpCard.Suit &&
                                 i.Value < Hodnota.Svrsek &&
                                 hand.HasX(i.Suit) &&       //pokud je jen jedna
                                 !hand.HasA(i.Suit) &&
                                 hand.CardCount(i.Suit) <= 3)
                     .Select(i => i.Suit)
                     .Distinct()
                     .Count() <= 1)
            {
                //vezmi nizke karty nebo K, S od netrumfove barvy pokud v barve nemam hlasku
                var trumpTalon = talon.Where(i => !talon.Take(2).Contains(i) &&
                                                  (i.Value < Hodnota.Svrsek &&
                                                   !(hand.HasX(i.Suit) &&
                                                     !hand.HasA(i.Suit) &&
                                                     (hand.CardCount(i.Suit) <= 3 ||
                                                      (hand.CardCount(i.Suit) <= 4 &&
                                                       hand.HasK(i.Suit) &&
                                                       hand.HasQ(i.Suit)))) &&
                                                    !(hand.HasX(i.Suit) &&
                                                      hand.CardCount(i.Suit) == 2)) ||
                                                  (i.Suit != trumpCard.Suit &&
                                                   (i.Value > Hodnota.Kral ||
                                                    (!(hand.HasX(i.Suit) &&
                                                       hand.CardCount(i.Suit) == 2) &&
                                                     ((i.Value == Hodnota.Kral &&
                                                       !hand.HasQ(i.Suit)) ||
                                                      (i.Value == Hodnota.Svrsek &&
                                                       !hand.HasK(i.Suit)))))))
                                      .OrderBy(i => i.Suit == trumpCard.Suit
                                                     ? 1 : 0)
                                      .ThenBy(i => i.Value)
                                      .ToList();

                //pokud mam trumfovou sedmu, tak dam do talonu druhy nejnizsi trumf
                if (talon.Has7(trumpCard.Suit) &&
                    talon.CardCount(trumpCard.Suit) > 2 &&
                    (_g.AllowFakeSeven ||
                     _g.AllowFake107))
                {
                    trumpTalon = trumpTalon.Where(i => i.Suit != trumpCard.Suit ||
                                                       i.Value != Hodnota.Sedma)
                                           .OrderBy(i => i.Value)
                                           .ToList();
                }
                if (talon.Has7(trumpCard.Suit) &&
                    talon.CardCount(trumpCard.Suit) > 1 &&
                    !_g.AllowFakeSeven &&
                    !_g.AllowFake107)
                {
                    trumpTalon = trumpTalon.Where(i => i.Suit != trumpCard.Suit ||
                                                       i.Value != Hodnota.Sedma)
                                           .OrderBy(i => i.Value)
                                           .ToList();
                }
                var problemCount = talon.Take(2)
                                        .Count(i => i.Suit != trumpCard.Suit &&
                                                    (i.Value > Hodnota.Kral ||
                                                     (hand.HasX(i.Suit) &&
                                                      !hand.HasA(i.Suit) &&
                                                      !hand.HasK(i.Suit) &&
                                                      hand.CardCount(i.Suit) == 2) ||
                                                      (i.Value == Hodnota.Kral &&
                                                       hand.HasQ(i.Suit)) ||
                                                      (i.Value == Hodnota.Svrsek &&
                                                       hand.HasK(i.Suit))));
                if (trumpTalon.Count >= 2 &&
                    problemCount == 2)
                {
                    talon = trumpTalon;
                }
                else if (trumpTalon.Any())
                {
                    talon = trumpTalon.Take(1).Concat(talon).Distinct().ToList();
                }
            }
            if (talon.Count < 2)
            {
                var talonstr = string.Join(",", talon);
                throw new InvalidOperationException(string.Format("Badly generated talon for player{0}\nTalon:\n{1}\nHand:\n{2}", PlayerIndex + 1, talonstr, new Hand(hand)));
            }

            //pokud bych vybral do talonu barvu kde mam X ale ne A, tak vybirej tyto barvy odspodu
            var cardsToRemove = talon.Take(2)
                                     .Where(i => i.Suit != _trumpCard.Suit &&
                                                 hand.HasX(i.Suit) &&
                                                 !hand.HasA(i.Suit) &&
                                                 (!hand.HasK(i.Suit) ||
                                                  i.Value == Hodnota.Kral) &&
                                                 talon.Count(j => j.Suit == i.Suit &&
                                                                  j.Value < Hodnota.Desitka) >= 2)
                                    .ToList();

            if (cardsToRemove.Any())
            {
                var replacementCards = new List<Card>();
                foreach(var cardToRemove in cardsToRemove)
                {
                    replacementCards.AddRange(hand.Where(i => _g.IsValidTalonCard(i) &&
                                                              cardToRemove.Suit == i.Suit &&
                                                              !replacementCards.Contains(i) &&
                                                              !talon.Take(2).Contains(i))
                                                  .OrderBy(i => i.Value)
                                                  .Take(Math.Min(2, hand.Count(i => i.Suit == cardToRemove.Suit &&
                                                                                    i.Value < Hodnota.Desitka) - 1)));
                    if (replacementCards.Any())
                    {
                        talon.Remove(cardToRemove);
                    }
                }
                replacementCards = replacementCards.Distinct().Take(cardsToRemove.Count).ToList();
                talon = replacementCards.Concat(talon).Distinct().ToList();
            }
            //u barev kde muzes dat do talonu aspon 3 karty vynechej tu nejnizsi a dale vybirej odspoda
            foreach (var card in talon.Take(2)
                                      .Where(i => i.Suit != trumpCard.Suit &&
                                                  lowCardCountsPerSuit[i.Suit] >= 3 &&
                                                  hand.Any(j => j.Suit == i.Suit &&
                                                                j.Value < i.Value))
                                      .ToList())
            {
                var cardIndex = Math.Max(0, talon.IndexOf(card));
                var replacementCard = hand.Where(i => _g.IsValidTalonCard(i) &&
                                                      i.Suit == card.Suit &&
                                                      i.Value < card.Value &&
                                                      talon.IndexOf(i) > 1)
                                          .OrderBy(i => i.Value)
                                          .Skip(1)
                                          .FirstOrDefault();
                if (replacementCard != null)
                {
                    talon.Remove(card);
                    talon.Remove(replacementCard);
                    talon.Insert(cardIndex, replacementCard);
                }
            }
            //pokud je v talonu neco vyssiho nez spodek a mas i spodka, tak ho dej do talonu
            foreach (var card in talon.Where(i => i.Suit != trumpCard.Suit &&
                                                 i.Value > Hodnota.Spodek &&
                                                 hand.HasJ(i.Suit)).ToList())
            {
                var cardIndex = Math.Max(0, talon.IndexOf(card));

                if (!talon.HasJ(card.Suit) ||
                    (talon.HasJ(card.Suit) &&
                     talon.CardCount(card.Suit) > 2))
                {
                    talon.Remove(card);
                }
                if (!talon.HasJ(card.Suit))
                {
                    talon.Insert(cardIndex, new Card(card.Suit, Hodnota.Spodek));
                }
            }
            if (talon.Count > 2 &&
                talon.Has7(trumpCard.Suit) &&
                hand.CardCount(trumpCard.Suit) >= 5 &&
                talon.Any(i => i.Suit == trumpCard.Suit &&
                               i.Value <= Hodnota.Spodek &&
                               i.Value >= Hodnota.Osma))
            {
                talon.Remove(new Card(trumpCard.Suit, Hodnota.Sedma));
            }
            //pokud bych do talonu dal plonka a 1 kartu z 2listu, tak preferuj cely 2list a plonka si nech
            var talon3 = talon.Take(3).ToList();
            if (talon3.SuitCount() == 2 &&
                talon3.All(i => hand.CardCount(i.Suit) <= 2) &&
                talon3.Any(i => hand.CardCount(i.Suit) == 2) &&
                talon3.Any(i => hand.CardCount(i.Suit) == 1))
            {
                talon = talon3.Where(i => hand.CardCount(i.Suit) == 2)
                              .ToList();
            }
            //pokud davam do talonu 2 karty od barvy kde mam A a muzu dat do talonu stejne dlouhou barvu kde mam X, K tak to udelej
            var talon2 = talon.Take(2).ToList();
            if (talon2.SuitCount() == 1 &&
                hand.HasA(talon2.First().Suit) &&
                talon.Count(i => i.Suit != _trump &&
                                 i.Value <= Hodnota.Spodek &&
                                 hand.HasX(i.Suit) &&
                                 hand.HasK(i.Suit) &&
                                 !hand.HasA(i.Suit) &&
                                 hand.CardCount(i.Suit) <= hand.CardCount(talon2.First().Suit)) >= 2)
            {
                talon = talon.Where(i => i.Suit != _trump &&
                                         i.Value <= Hodnota.Spodek &&
                                         hand.HasX(i.Suit) &&
                                         hand.HasK(i.Suit) &&
                                         !hand.HasA(i.Suit) &&
                                         hand.CardCount(i.Suit) <= hand.CardCount(talon2.First().Suit))
                             .OrderByDescending(i => i.Value)
                             .Take(2)
                             .ToList();
            }

            talon = talon.Take(2).ToList();

			if (talon == null || talon.Count != 2 || talon.Contains(trumpCard))
			{
				var msg = talon == null ? "(null)" : string.Format("Count: {0}\nHand: {1}", talon.Distinct().Count(), new Hand(hand));
				throw new InvalidOperationException("Bad talon: " + msg);
			}

			return talon;
        }

        public override List<Card> ChooseTalon()
        {
            //zacinajici hrac nejprve vybira talon a az pak rozhoduje jakou hru bude hrat (my mame oboje implementovane uvnitr ChooseGameFlavour())
            if (PlayerIndex == _g.OriginalGameStartingPlayerIndex && _g.GameType == 0)
            {
                ChooseGameFlavour();
				//pokud delam poradce pro cloveka, musim vybrat talon i kdyz bych normalne nehral betla nebo durcha
				//v tom pripade jestli uz byl vybranej betl, tak my musime jit na durch a podle toho vybirat talon
				//jiank vybirame betlovej talon
				if (AdvisorMode && (_talon == null || !_talon.Any()))
				{
                    if ((_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][0] * _durchSimulations && 
                         _durchSimulations > 0) ||
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
                //pokud se AI chce rozhodovat po hlaseni spatne barvy, musi sjet simulace znovu se skutecnym talonem (neni treba pokud jsem volil)
                if (_rerunSimulations)
                {
                    var bidding = new Bidding(_g);
                    Probabilities = new Probability(PlayerIndex, PlayerIndex, new Hand(Hand), null,
                                                    _g.AllowFakeSeven || _g.AllowFake107, _g.AllowAXTalon, _g.AllowTrumpTalon,
                                                    _g.CancellationToken, _stringLoggerFactory, new List<Card>())
                    {
                        ExternalDebugString = _debugString,
                        UseDebugString = true
                    };
                    RunGameSimulations(bidding, PlayerIndex, false, true);
                }
                //pokud je min. hra betl, zbyva uz jen talon na durcha
                if (_g.GameType == Hra.Betl)
                {
                    _talon = ChooseDurchTalon(Hand, null);
                }
                else
                {
                    //pokud vysel durch pres prah, vyber pro nej talon
                    if (_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][1] * _durchSimulations && _durchSimulations > 0)
                    {
                        _talon = ChooseDurchTalon(Hand, null);
                    }
                    //pokud vysel betl pres prah, vyber pro nej talon
                    else if (_betlBalance >= Settings.GameThresholdsForGameType[Hra.Betl][1] * _betlSimulations && _betlSimulations > 0)
                    {
                        _talon = ChooseBetlTalon(Hand, null);
                    }
                    else
                    {
                        //ani jedno nevyslo pres prah, vezmeme to co vyslo lepe
                        if (_durchBalance > 0 &&
                            _durchSimulations > 0 &&
                            (_betlSimulations == 0 ||
                             (float)_durchBalance / (float)_durchSimulations > (float)_betlBalance / (float)_betlSimulations))
                        {
                            _talon = ChooseDurchTalon(Hand, null);
                        }
                        else
                        {
                            _talon = ChooseBetlTalon(Hand, null);
                        }
                    }
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
                Probabilities = new Probability(PlayerIndex, PlayerIndex, new Hand(Hand), TrumpCard?.Suit,
                                               _g.AllowFakeSeven || _g.AllowFake107, _g.AllowAXTalon, _g.AllowTrumpTalon,
                                               _g.CancellationToken, _stringLoggerFactory, _talon)
                {
                    ExternalDebugString = _debugString,
                    UseDebugString = true
                };

                return flavour;
            }
            //zpocitej ztraty v pripade v pripade fleku na hru a ticheho kila
            //pokud ma akter trumfovou hlasku, tak ber ztraty bez fleku (v pripade sedmy by se mohlo hrat i bez fleku)
            var gameMultiplier = PlayerIndex == _g.GameStartingPlayerIndex
                                 ? TrumpCard != null
                                   ? Hand.HasK(TrumpCard.Suit) &&
                                     Hand.HasQ(TrumpCard.Suit) &&
                                     (Hand.HasA(TrumpCard.Suit) ||
                                      Hand.HasX(TrumpCard.Suit) ||
                                      Hand.CardCount(TrumpCard.Suit) >= 5)
                                     ? 1 : 2
                                   : 0
                                 : 2;
            var gameValue = PlayerIndex == _g.GameStartingPlayerIndex
                            ? gameMultiplier * _g.QuietHundredValue
                            : Math.Max(_g.HundredValue, gameMultiplier * _g.QuietHundredValue);
            var lossPerPointsLost = TrumpCard != null
                                    ? Enumerable.Range(0, 10)
                                              .Select(i => 100 + i * 10)
                                              .ToDictionary(k => k,
                                                            v => ((_g.CalculationStyle == CalculationStyle.Adding
                                                                   ? (v - 90) / 10 * gameValue
                                                                   : (1 << (v - 100) / 10) * gameValue) -
                                                                  (Hand.CardCount(TrumpCard.Suit) >= 5 &&
                                                                   Hand.Has7(TrumpCard.Suit)
                                                                    ? _g.SevenValue : 0)) *
                                                                 (PlayerIndex == _g.GameStartingPlayerIndex //ztrata pro aktera je dvojnasobna (plati obema souperum)
                                                                  ? TrumpCard.Suit == Barva.Cerveny
                                                                    ? 4
                                                                    : 2
                                                                  : 1))
                                    : new Dictionary<int, int>();
            var tempTalon = Hand.Count == 12 ? ChooseNormalTalon(Hand, TrumpCard) : _talon ?? new List<Card>();
            var tempHand = Hand.Where(i => !tempTalon.Contains(i)).ToList();
            var kqScore = _g.trump.HasValue
                ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
                      .Where(b => Hand.HasK(b) && Hand.HasQ(b))
                      .Sum(b => b == _g.trump.Value ? 40 : 20)
                : 0;
            var estimatedPointsWon = _trump != null ? EstimateFinalBasicScore(tempHand, tempTalon) : 0;
            var estimatedPointsLost = _trump != null ? EstimateMaxTotalPointsLost(tempHand, tempTalon) : 0;
            
            DebugInfo.MaxEstimatedMoneyLost = _trump != null &&
                                              lossPerPointsLost.ContainsKey(estimatedPointsLost)
                                                ? -lossPerPointsLost[estimatedPointsLost]
                                                : 0;
            if (_initialSimulation || AdvisorMode)
            {
                var bidding = new Bidding(_g);

                //pokud volim hru tak mam 12 karet a nechci generovat talon,
                //jinak mam 10 karet a talon si necham nagenerovat a potom ho vymenim za talon zvoleny podle logiky
                if (_talon == null) //pokud je AiPlayer poradce cloveka, tak uz je _talon definovanej
                {
                    _talon = PlayerIndex == _g.GameStartingPlayerIndex ? new List<Card>() : null;
                }
                var probabilities = Probabilities;
                Probabilities = new Probability(PlayerIndex, PlayerIndex, new Hand(Hand), null,
                                                _g.AllowFakeSeven || _g.AllowFake107, _g.AllowAXTalon, _g.AllowTrumpTalon,
                                                _g.CancellationToken, _stringLoggerFactory, _talon)
				{
					ExternalDebugString = _debugString
				};

                if (PlayerIndex == _g.OriginalGameStartingPlayerIndex && bidding.BetlDurchMultiplier == 0)
                {
                    //Sjedeme simulaci hry, betlu, durcha i normalni hry a vratit talon pro to nejlepsi. 
                    //Zapamatujeme si vysledek a pouzijeme ho i v ChooseGameFlavour() a ChooseGameType()
                    RunGameSimulations(bidding, _g.GameStartingPlayerIndex, true, true);
                    _initialSimulation = false;
                    if (Settings.CanPlayGameType[Hra.Durch] && 
                        _durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][0] * _durchSimulations && 
                        _durchSimulations > 0 &&
                        !(_hundredOverDurch &&
                          !IsHundredTooRisky(tempHand, tempTalon)))
                    {
                        if (_talon == null || !_talon.Any())
                        {
                            _talon = ChooseDurchTalon(Hand, null);

                        }
                        DebugInfo.Rule = "Durch";
                        DebugInfo.RuleCount = _durchBalance;
                        DebugInfo.TotalRuleCount = _durchSimulations;

                        return GameFlavour.Bad;
                    }
                    else if (Settings.CanPlayGameType[Hra.Betl] && 
                             ((_betlBalance >= Settings.GameThresholdsForGameType[Hra.Betl][0] * _betlSimulations && 
                               _betlSimulations > 0 &&
                               !(_hundredOverBetl &&
                                 !IsHundredTooRisky(tempHand, tempTalon)) &&
                               !(_betlBalance < Settings.GameThresholdsForGameType[Hra.Betl][1] * _betlSimulations &&
                                 _sevensBalance >= Settings.GameThresholdsForGameType[Hra.Sedma][0] * _sevenSimulations && _sevenSimulations > 0)) ||
                              (Settings.SafetyBetlThreshold > 0 &&
                               ((!AdvisorMode &&
                                 !Settings.AiMayGiveUp) ||
                                (AdvisorMode &&
                                 !Settings.PlayerMayGiveUp)) &&
                               Settings.MinimalBidsForGame <= 1 &&
                               ((lossPerPointsLost.ContainsKey(estimatedPointsLost) &&
                                 lossPerPointsLost[estimatedPointsLost] >= Settings.SafetyBetlThreshold &&
                                 (!_trump.HasValue ||
                                  estimatedPointsWon <= 40 ||
                                  (!Hand.HasK(_trump.Value) &&
                                   !Hand.HasQ(_trump.Value) &&
                                   !(Hand.CardCount(_trump.Value) >= 5 &&
                                     (Hand.HasA(_trump.Value) &&                                     
                                      estimatedPointsWon >= 50) ||
                                     estimatedPointsWon >= 70)))) ||
                                (_maxMoneyLost <= -Settings.SafetyBetlThreshold &&
                                 _avgBasicPointsLost >= 50)))))  //utec na betla pokud nemas na ruce nic a hrozi kilo proti
                    {
                        if (_talon == null || !_talon.Any())
                        {
                            _talon = ChooseBetlTalon(Hand, null);
                        }
                        DebugInfo.Rule = "Betl";
                        DebugInfo.RuleCount = _betlBalance;
                        DebugInfo.TotalRuleCount = _betlSimulations;

                        return GameFlavour.Bad;
                    }
                    else
                    {
                        if (_talon == null || !_talon.Any())
                        {
                            _talon = ChooseNormalTalon(Hand, TrumpCard);
                        }
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

                        return GameFlavour.Good;
                    }
                    _rerunSimulations = false;
                }
                else
                {
                    try
                    {
                        RunGameSimulations(bidding, PlayerIndex, false, true);
                    }
                    finally
                    {
                        if (probabilities != null)
                        {
                            Probabilities = probabilities;
                        }
                        _rerunSimulations = true;
                    }
                }
                _initialSimulation = false;
            }
            _runSimulations = true; //abychom v GetBidsAndDoubles znovu sjeli simulaci normalni hry

			//byla uz zavolena nejaka hra?
            if (_gameType == Hra.Durch)
            {
				DebugInfo.RuleCount = _durchBalance;
                DebugInfo.TotalRuleCount = _durchSimulations;
                return GameFlavour.Good;
            }
            else if (_gameType == Hra.Betl)
            {
                if (TeamMateIndex == -1)
                {
                    //v ChooseTalon() jsem zvolil talon na betla a chci hrat utikacka
                    return GameFlavour.Bad;
                }
                var topCards = Hand.Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                   .Where(h => Card.GetBadValue(h) > i.BadValue)
                                                   .All(h => Probabilities.CardProbability((PlayerIndex + 1) % Game.NumPlayers, new Card(i.Suit, h)) == 0 &&
                                                             Probabilities.CardProbability((PlayerIndex + 2) % Game.NumPlayers, new Card(i.Suit, h)) == 0))
                                   .ToList();
                var holesPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                       .ToDictionary(k => k, v =>
                                           Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                               .Select(h => new Card(v, h))
                                               .Where(i => Probabilities.CardProbability((PlayerIndex + 1) % Game.NumPlayers, i) > 0 ||
                                                           Probabilities.CardProbability((PlayerIndex + 2) % Game.NumPlayers, i) > 0)
                                               .OrderBy(i => i.BadValue)
                                               .Skip(topCards.CardCount(v))
                                               .ToList());
                var lowCards = Hand.Where(i => holesPerSuit[i.Suit].Any(j => j.BadValue > i.BadValue))
                                   .ToList();
                //pouzivam vyssi prahy: pokud nam vysel durch (beru 70% prah), abych kompenzoval, ze simulace nejsou presne
                var thresholdIndex = Math.Min(Settings.GameThresholdsForGameType[Hra.Durch].Length - 1, 1);    //70%
                if (_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][thresholdIndex] * _durchSimulations && 
                    _durchSimulations > 0 &&
                    lowCards.Count() < 2)   //pokud mas v nejake barve 2 neodstranitelne diry, tak je durch riskantni (talon by musel byt perfektni)
                {
					_talon = ChooseDurchTalon(Hand, null);
					DebugInfo.RuleCount = _durchBalance;
                    DebugInfo.TotalRuleCount = _durchSimulations;
                    return GameFlavour.Bad;
                }
                DebugInfo.RuleCount = _durchSimulations - _durchBalance;
                DebugInfo.TotalRuleCount = _durchSimulations;
                return GameFlavour.Good;
            }
            else
            {
                var betlThresholdIndex = PlayerIndex == _g.GameStartingPlayerIndex ? 0 : Math.Min(Settings.GameThresholdsForGameType[Hra.Betl].Length - 1, 1);     //85%
                var durchThresholdIndex = 0;// PlayerIndex == _g.GameStartingPlayerIndex ? 0 : Math.Min(Settings.GameThresholdsForGameType[Hra.Durch].Length - 1, 1);    //85%
                if ((Settings.CanPlayGameType[Hra.Durch] &&
                     _durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][durchThresholdIndex] * _durchSimulations &&
                     _durchSimulations > 0 &&
                     (TeamMateIndex != -1 ||
                      !(_hundredOverDurch &&
                        !IsHundredTooRisky(tempHand)))) ||
                    (Settings.CanPlayGameType[Hra.Betl] && 
                     (_betlBalance >= Settings.GameThresholdsForGameType[Hra.Betl][betlThresholdIndex] * _betlSimulations &&
                      _betlSimulations > 0 &&
                      ((TeamMateIndex != -1 &&
                        (Hand.CardCount(Hodnota.Eso) <= 1 ||
                         GetBetlHoles() <= 3)) ||
                       (TeamMateIndex == -1 &&
                        !(_hundredOverBetl &&
                          !IsHundredTooRisky(tempHand, tempTalon)) &&
                        !(_betlBalance < Settings.GameThresholdsForGameType[Hra.Betl][1] * _betlSimulations &&
                          _sevensBalance >= Settings.GameThresholdsForGameType[Hra.Sedma][0] * _sevenSimulations && _sevenSimulations > 0)))) ||
                       (Settings.SafetyBetlThreshold > 0 &&
                        ((!AdvisorMode &&
                          !Settings.AiMayGiveUp) ||
                         (AdvisorMode &&
                          !Settings.PlayerMayGiveUp)) &&
                        Settings.MinimalBidsForGame <= 1 &&
                        ((lossPerPointsLost.ContainsKey(estimatedPointsLost) &&
                          lossPerPointsLost[estimatedPointsLost] >= Settings.SafetyBetlThreshold &&
                          (!_trump.HasValue ||
                           estimatedPointsWon <= 20 ||
                           (!Hand.HasK(_trump.Value) &&
                            !Hand.HasQ(_trump.Value) &&
                            !(Hand.CardCount(_trump.Value) >= 5 &&
                              (Hand.HasA(_trump.Value) &&                              
                               estimatedPointsWon >= 50) ||
                              estimatedPointsWon >= 70)))) ||
                         (_maxMoneyLost <= -Settings.SafetyBetlThreshold &&
                          _avgBasicPointsLost >= 50)))))
                {
                    if ((_betlSimulations > 0 && 
                         (!Settings.CanPlayGameType[Hra.Durch] ||
                          _durchSimulations == 0 || 
                          (float)_betlBalance / (float)_betlSimulations > (float)_durchBalance / (float)_durchSimulations)) ||
                        (Settings.SafetyBetlThreshold > 0 &&
                         ((lossPerPointsLost.ContainsKey(estimatedPointsLost) &&
                           lossPerPointsLost[estimatedPointsLost] >= Settings.SafetyBetlThreshold &&
                          (!_trump.HasValue ||
                           estimatedPointsWon <= 20 ||
                           (!Hand.HasK(_trump.Value) &&
                            !Hand.HasQ(_trump.Value) &&
                            !(Hand.CardCount(_trump.Value) >= 5 &&
                              (Hand.HasA(_trump.Value) &&                              
                               estimatedPointsWon >= 50) ||
                              estimatedPointsWon >= 70)))) ||
                          (_maxMoneyLost <= -Settings.SafetyBetlThreshold &&
                           _avgBasicPointsLost >= 50))))
                    {
                        _gameType = Hra.Betl;   //toto zajisti, ze si umysl nerozmysli po odhozeni talonu na betla
                                                //(odhadovane skore se muze zmenit a s tim i odhodlani hrat betla,
                                                //ale protoze talon uz byl odhozen na betla, tak si zde vynutime ho sehrat)
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
                //GameFlovour.Good, GameFlovour.Good107
                if (_betlSimulations > 0 && 
                    (_durchSimulations == 0 || (float)_betlBalance / (float)_betlSimulations > (float)_durchBalance / (float)_durchSimulations))
                {
                    DebugInfo.RuleCount = _betlSimulations - _betlBalance;
                    DebugInfo.TotalRuleCount = _betlSimulations;
                }
                else
                {
                    DebugInfo.RuleCount = _durchSimulations - _durchBalance;
                    DebugInfo.TotalRuleCount = _durchSimulations;
                }
                if (_g.Top107 &&
                    PlayerIndex == _g.GameStartingPlayerIndex &&
                    _hundredSimulations > 0 && 
                    _sevenSimulations > 0 &&
                    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        .Any(b => Hand.HasK(b) && Hand.HasQ(b)) &&  //protoze prah pro kilo muze nekdo nastavit na nulu
                    Hand.Has7(_trump ?? _g.trump.Value) &&          //protoze prah pro sedmu muze nekdo nastavit na nulu
                    _sevensBalance >= Settings.GameThresholdsForGameType[Hra.Sedma][0] * _sevenSimulations &&
                    _hundredsBalance >= Settings.GameThresholdsForGameType[Hra.Kilo][0] * _hundredSimulations &&
                    !IsHundredTooRisky(tempHand))
                {
                    return GameFlavour.Good107;
                }
                //falesnych 107
                if (PlayerIndex == _g.GameStartingPlayerIndex &&
                    _g.AllowFake107 &&
                    _g.Calculate107Separately &&
                    _g.Top107 &&
                    _hundredSimulations > 0 &&
                    _hundredsBalance >= Settings.GameThresholdsForGameType[Hra.Kilo][0] * _hundredSimulations &&
                    !IsHundredTooRisky(tempHand, tempTalon) &&
                    _avgWinForHundred > 2 * (_g.DurchValue + 2 * _g.SevenValue))
                {
                    return GameFlavour.Good107;
                }
                return GameFlavour.Good;
            }
        }

        private void UpdateGeneratedHandsByChoosingTalon(Hand[] hands, Func<List<Card>, Card, List<Card>> chooseTalonFunc, int gameStartingPlayerIndex)
        {
            //volicimu hraci dame i to co je v talonu, aby mohl vybrat skutecny talon
            hands[gameStartingPlayerIndex].AddRange(hands[Game.TalonIndex]);

            var talon = chooseTalonFunc(hands[gameStartingPlayerIndex], TrumpCard);

            hands[gameStartingPlayerIndex].RemoveAll(i => talon.Contains(i));
            hands[Game.TalonIndex] = new Hand(talon);
        }

        private void UpdateGeneratedHandsByChoosingTrumpAndTalon(Hand[] hands, Func<List<Card>, Card, List<Card>> chooseTalonFunc, int GameStartingPlayerIndex)
        {
            //volicimu hraci dame i to co je v talonu, aby mohl vybrat skutecny talon
            hands[GameStartingPlayerIndex].AddRange(hands[Game.TalonIndex]);

            var trumpCard = ChooseTrump(hands[GameStartingPlayerIndex]);
            var talon = chooseTalonFunc(hands[GameStartingPlayerIndex], trumpCard);

            hands[GameStartingPlayerIndex].RemoveAll(i => talon.Contains(i));
            hands[Game.TalonIndex] = new Hand(talon);
        }

        private Hand[] GetPlayersHandsAndTalon()
        {

            var hands = new List<Hand>(_g.players.Select(i => new Hand(i.Hand)));

            hands.Add(new Hand(_g.talon));

            return hands.ToArray();
        }

        public void ResetDebugInfo()
        {
            DebugInfo.Card = null;
            DebugInfo.Rule = null;
            DebugInfo.RuleCount = 0;
            DebugInfo.TotalRuleCount = 0;
            DebugInfo.AllChoices = new RuleDebugInfo[0];
        }

        //vola se jak pro voliciho hrace tak pro oponenty 
        private void RunGameSimulations(Bidding bidding, int gameStartingPlayerIndex, bool simulateGoodGames, bool simulateBadGames)
        {
            var gameComputationResults = new ConcurrentQueue<GameComputationResult>();
            var durchComputationResults = new ConcurrentQueue<GameComputationResult>();
            var betlComputationResults = new ConcurrentQueue<GameComputationResult>();
            var totalGameSimulations = (simulateGoodGames ? 2 * Settings.SimulationsPerGameType : 0) +
                                       (simulateBadGames ? 2 * Settings.SimulationsPerGameType : 0);
            var progress = 0;
            var maxSimulationsPerGameType = 1000;

            OnGameComputationProgress(new GameComputationProgressEventArgs { Current = progress, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 1, Message = "Generuju karty"});

            //nasimuluj hry v barve
            var source = Settings.Cheat
                ? new [] { GetPlayersHandsAndTalon() }
                : null;
            var tempSource = new ConcurrentQueue<Hand[]>();

            options.MaxDegreeOfParallelism = Settings.MaxDegreeOfParallelism > 0 ? Settings.MaxDegreeOfParallelism : -1;
            //pokud volim hru tak se ted rozhoduju jaky typ hry hrat (hra, betl, durch)
            //pokud nevolim hru, tak bud simuluju betl a durch nebo konkretni typ hry
            //tak ci tak nevim co je/bude v talonu
            _log.DebugFormat("Running game simulations for {0} ...", Name);
            if (simulateGoodGames)
            {
                var start = DateTime.Now;
                var actualSimulations = 0;
                var actualSimulations7 = 0;

                if (_g.CancellationToken.IsCancellationRequested)
                {
                    _log.DebugFormat("Task cancellation requested");
                }
                else
                {
                    _debugString.Append("Simulating good games\n");
                    try
                    {
                        Parallel.ForEach(source ?? Probabilities.GenerateHands(1, gameStartingPlayerIndex, maxSimulationsPerGameType), options, (hh, loopState) =>
                        //foreach (var hh in source ?? Probabilities.GenerateHands(1, gameStartingPlayerIndex, maxSimulationsPerGameType))
                        {
                            ThrowIfCancellationRequested();
                            try
                            {
                                if (source == null)
                                {
                                    tempSource.Enqueue(hh);
                                }
                                if ((DateTime.Now - start).TotalMilliseconds > Settings.MaxSimulationTimeMs)
                                {
                                    Probabilities.StopGeneratingHands();
                                    loopState.Stop();
                                    //break;
                                }
                                else
                                {
                                    Interlocked.Increment(ref actualSimulations);
                                    var hands = new Hand[Game.NumPlayers + 1];
                                    for (var i = 0; i < hands.Length; i++)
                                    {
                                        hands[i] = new Hand(new List<Card>((List<Card>)hh[i]));   //naklonuj karty aby v pristich simulacich nebyl problem s talonem
                                    }
                                    if (_talon == null || !_talon.Any())
                                    {
                                        UpdateGeneratedHandsByChoosingTalon(hands, ChooseNormalTalon, gameStartingPlayerIndex);
                                    }

                                    var gameComputationResult = ComputeGame(hands, null, null, _trump ?? _g.trump, (Hra.Hra | Hra.SedmaProti), 10, 1);
                                    gameComputationResults.Enqueue(gameComputationResult);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(ex.Message + "\n" + ex.StackTrace);
                            }
                            var val = Interlocked.Increment(ref progress);
                            OnGameComputationProgress(new GameComputationProgressEventArgs { Current = val, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0, Message = "Simuluju hru" });
                        });
                    }
                    catch(OperationCanceledException ex)
                    {
                        throw;
                    }
                }
                var end = DateTime.Now;
                if (source == null)
                {
                    source = tempSource.ToArray();
                }
                if (PlayerIndex == _g.GameStartingPlayerIndex && 
//                    _gameType == null && 
                    (Hand.Has7(_trump ?? _g.trump.Value) ||
                     (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                          .Any(b => Hand.HasK(b) && Hand.HasQ(b)))))
                {
                    var start7 = DateTime.Now;

                    if (_g.CancellationToken.IsCancellationRequested)
                    {
                        _debugString.AppendFormat("Task cancellation requested");
                    }
                    else
                    {
                        _debugString.Append("Simulating hundreds and sevens\n");
                        var exceptions = new ConcurrentQueue<Exception>();
                        try
                        {
                            Parallel.ForEach(source, options, (hh, loopState) =>
                            //foreach(var hh in source)
                            {
                                ThrowIfCancellationRequested();
                                try
                                {
                                    if (source == null)
                                    {
                                        tempSource.Enqueue(hh);
                                    }
                                    if ((DateTime.Now - start7).TotalMilliseconds > Settings.MaxSimulationTimeMs)
                                    {
                                        Probabilities.StopGeneratingHands();
                                        loopState.Stop();
                                        //break;
                                    }
                                    else
                                    {
                                        Interlocked.Increment(ref actualSimulations7);
                                        var hands = new Hand[Game.NumPlayers + 1];
                                        for (var i = 0; i < hands.Length; i++)
                                        {
                                            hands[i] = new Hand(new List<Card>((List<Card>)hh[i]));   //naklonuj karty aby v pristich simulacich nebyl problem s talonem
                                        }

                                        if (_talon == null || !_talon.Any())
                                        {
                                            UpdateGeneratedHandsByChoosingTalon(hands, ChooseNormalTalon, gameStartingPlayerIndex);
                                        }
                                        var gt = EstimateBasicPointsLost(hands[PlayerIndex], hands[Game.TalonIndex]) <= 50 &&
                                                 Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                     .Any(b => hands[PlayerIndex].HasK(b) && hands[PlayerIndex].HasQ(b))
                                                     ? Hra.Kilo
                                                     : Hra.Hra;
                                        var trump = _trump ?? _g.trump ?? Barva.Cerveny;
                                        if (hands[PlayerIndex].Has7(trump))
                                        {
                                            gt |= Hra.Sedma;
                                        }
                                        var gameComputationResult = ComputeGame(hands, null, null, trump, AdvisorMode ? gt : _gameType ?? gt, 10, 1);
                                        gameComputationResults.Enqueue(gameComputationResult);
                                    }
                                    var val = Interlocked.Increment(ref progress);

                                    OnGameComputationProgress(new GameComputationProgressEventArgs { Current = val, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0, Message = "Simuluju kilo a sedmu" });
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine(ex.Message + "\n" + ex.StackTrace);
                                    exceptions.Enqueue(ex);
                                }
                            });
                        }
                        catch (OperationCanceledException ex)
                        {
                            throw;
                        }
                        //if (exceptions.Count > 0)
                        //{
                        //    throw new AggregateException(exceptions);
                        //}
                    }
                    _sevenSimulations = actualSimulations7;
                    _hundredSimulations = actualSimulations7;
                }
                else
                {
                    _sevenSimulations = actualSimulations;
                }
                _gameSimulations = actualSimulations;
                if (PlayerIndex != _g.GameStartingPlayerIndex)
                {
                    _hundredSimulations = actualSimulations;
                    _sevenSimulations = actualSimulations;
                }
                //Settings.SimulationsPerGameType = _gameSimulations;
                //Settings.SimulationsPerGameTypePerSecond = (int)((float)_gameSimulations / Settings.MaxSimulationTimeMs * 1000);
                if (end != start)
                {
                    Settings.SimulationsPerGameType = actualSimulations;
                    Settings.SimulationsPerGameTypePerSecond = (int)((float)actualSimulations / (end - start).TotalMilliseconds * 1000);
                }
                totalGameSimulations = (simulateGoodGames? _gameSimulations + _sevenSimulations : 0) +
                    (simulateBadGames? 2 * Settings.SimulationsPerGameType : 0);
            }
            if (simulateBadGames)
            {
                var initialProgress = progress;
                var start = DateTime.Now;
                var actualSimulations = 0;
				var fastSelection = Settings.GameFlavourSelectionStrategy == GameFlavourSelectionStrategy.Fast;
                var shouldChooseBetl = false;
                try
                {
                    shouldChooseBetl = ShouldChooseBetl();
                }
                catch
                {                    
                }
				if (!fastSelection || shouldChooseBetl)
				{
                    if (_g.CancellationToken.IsCancellationRequested)
                    {
                        _debugString.AppendFormat("Task cancellation requested");
                    }
                    else
                    {
                        _debugString.AppendFormat("Simulating betl. Fast guess: {0}\n", ShouldChooseBetl());
                        var exceptions = new ConcurrentQueue<Exception>();
                        try
                        {
                            Parallel.ForEach(source ?? Probabilities.GenerateHands(1, gameStartingPlayerIndex, maxSimulationsPerGameType), options, (hh, loopState) =>
                            //foreach (var hh in source ?? Probabilities.GenerateHands(1, gameStartingPlayerIndex, maxSimulationsPerGameType))
                            {
                                ThrowIfCancellationRequested();
                                try
                                {
                                    if (source == null)
                                    {
                                        tempSource.Enqueue(hh);
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
                                    else if (!_rerunSimulations) //pokud volim (poprve, tak v UpdateGeneratedHandsByChoosingTalon() beru v potaz trumfovou kartu kterou jsem zvolil
                                    {
                                        UpdateGeneratedHandsByChoosingTalon(hands, ChooseNormalTalon, _g.GameStartingPlayerIndex);
                                    }
                                    if (!AdvisorMode || _talon == null || !_talon.Any())
                                    {
                                        UpdateGeneratedHandsByChoosingTalon(hands, ChooseBetlTalon, gameStartingPlayerIndex);
                                    }
                                    var betlComputationResult = ComputeGame(hands, null, null, null, Hra.Betl, 10, 1, true);
                                    betlComputationResults.Enqueue(betlComputationResult);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine(ex.Message + "\n" + ex.StackTrace);
                                    exceptions.Enqueue(ex);
                                }
                                if ((DateTime.Now - start).TotalMilliseconds > Settings.MaxSimulationTimeMs)
                                {
                                    Probabilities.StopGeneratingHands();
                                    loopState.Stop();
                                }

                                var val = Interlocked.Increment(ref progress);
                                OnGameComputationProgress(new GameComputationProgressEventArgs { Current = val, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0, Message = "Simuluju betl" });
                                ThrowIfCancellationRequested();
                            });
                        }
                        catch (OperationCanceledException ex)
                        {
                            throw;
                        }
                    }
                    var end = DateTime.Now;
                    if (end != start)
                    {
                        Settings.SimulationsPerGameType = actualSimulations;
                        //Settings.SimulationsPerGameTypePerSecond = (int)((float)actualSimulations / Settings.MaxSimulationTimeMs * 1000);
                        Settings.SimulationsPerGameTypePerSecond = (int)((float)actualSimulations / (end - start).TotalMilliseconds * 1000);
                    }
				}
				else
				{
					Interlocked.Add(ref progress, Settings.SimulationsPerGameType);
				}
				totalGameSimulations = (simulateGoodGames ? _gameSimulations + _sevenSimulations : 0) +
                    (simulateBadGames ? 2 * Settings.SimulationsPerGameType : 0);
				OnGameComputationProgress(new GameComputationProgressEventArgs { Current = progress, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0 });
                if (source == null && tempSource.Any())
                {
                    source = tempSource.ToArray();
                }
                start = DateTime.Now;
                var shouldChooseDurch = false;
                try
                {
                    shouldChooseDurch = ShouldChooseDurch();
                }
                catch
                {                    
                }
				if (!fastSelection || shouldChooseDurch)
				{
                    if (_g.CancellationToken.IsCancellationRequested)
                    {
                        _debugString.AppendFormat("Task cancellation requested");
                    }
                    else
                    {
                        _debugString.AppendFormat("Simulating durch. fast guess: {0}\n", ShouldChooseDurch());
                        Parallel.ForEach(source ?? Probabilities.GenerateHands(1, gameStartingPlayerIndex, maxSimulationsPerGameType), options, (hands, loopState) =>
                        {
                            try
                            {
                                ThrowIfCancellationRequested();
                                if (source == null)
                                {
                                    tempSource.Enqueue(hands);
                                }
                                //nasimuluj ze volici hrac vybral trumfy a/nebo talon
                                if (PlayerIndex != _g.GameStartingPlayerIndex)
                                {
                                    if (_g.GameType == Hra.Betl)                                        
                                    {
                                        if (_g.OriginalGameStartingPlayerIndex != _g.GameStartingPlayerIndex &&
                                            PlayerIndex != _g.OriginalGameStartingPlayerIndex)
                                        {
                                            UpdateGeneratedHandsByChoosingTalon(hands, ChooseNormalTalon, _g.OriginalGameStartingPlayerIndex);
                                        }
                                        UpdateGeneratedHandsByChoosingTalon(hands, ChooseBetlTalon, _g.GameStartingPlayerIndex);
                                    }
                                    else
                                    {
                                        UpdateGeneratedHandsByChoosingTrumpAndTalon(hands, ChooseNormalTalon, _g.GameStartingPlayerIndex);
                                    }
                                }
                                UpdateGeneratedHandsByChoosingTalon(hands, ChooseDurchTalon, gameStartingPlayerIndex);

                                var durchComputationResult = ComputeGame(hands, null, null, null, Hra.Durch, 10, 1, true);
                                durchComputationResults.Enqueue(durchComputationResult);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(ex.Message + "\n" + ex.StackTrace);
                            }

                            var val = Interlocked.Increment(ref progress);
                            OnGameComputationProgress(new GameComputationProgressEventArgs { Current = val, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0, Message = "Simuluju durch" });

                            ThrowIfCancellationRequested();
                            if (NoChanceToWinDurch(PlayerIndex, hands))
                            {
                                OnGameComputationProgress(new GameComputationProgressEventArgs { Current = initialProgress + Settings.SimulationsPerGameType, Max = Settings.SimulationsPerGameTypePerSecond > 0 ? totalGameSimulations : 0, Message = "Neuhratelnej durch" });
                                Probabilities.StopGeneratingHands();
                                loopState.Stop();
                            }
                            if ((DateTime.Now - start).TotalMilliseconds > Settings.MaxSimulationTimeMs)
                            {
                                Probabilities.StopGeneratingHands();
                                loopState.Stop();
                            }
                        });
                    }
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
            //var _gameStrings = new List<string>();
            //var _sevenStrings = new List<string>();
            //var _durchStrings = new List<string>();
            //var _betlStrings = new List<string>();
            Bidding gameBidding;
            Bidding sevenBidding;
            Bidding betlBidding;
            Bidding durchBidding;

            if (!bidding.HasBids)
            {
                gameBidding = new Bidding(_g);
                gameBidding.SetLastBidder(_g.players[gameStartingPlayerIndex], Hra.Hra);
                gameBidding.SetLastBidder(_g.players[(gameStartingPlayerIndex + 1) % Game.NumPlayers], Hra.Hra); //flek
                sevenBidding = new Bidding(_g);
                sevenBidding.SetLastBidder(_g.players[gameStartingPlayerIndex], Hra.Kilo | Hra.Sedma);
                betlBidding = new Bidding(_g);
                betlBidding.SetLastBidder(_g.players[gameStartingPlayerIndex], Hra.Betl);
                durchBidding = new Bidding(_g);
                durchBidding.SetLastBidder(_g.players[gameStartingPlayerIndex], Hra.Durch);
            }
            else
            {
                gameBidding = bidding;
                sevenBidding = bidding;
                betlBidding = bidding;
                durchBidding = bidding;
            }

            //bidding nema zadne zavazky, cili CalculateMoney() nic nespocita
            var moneyCalculations = gameComputationResults.Where(i => (i.GameType & Hra.Hra) != 0 &&
                                                                      (i.GameType & Hra.Sedma) == 0).Select((i, idx) =>
            {
                var calc = GetMoneyCalculator(Hra.Hra, _trump ?? _g.trump, gameStartingPlayerIndex, gameBidding, i);

                calc.CalculateMoney();
                //_gameStrings.Add(GetComputationResultString(i, calc));

                return calc;
            }).Union(gameComputationResults.Where(i => (i.GameType & Hra.Sedma) != 0).Select((i, idx) =>
            {
                var calc = GetMoneyCalculator(Hra.Hra | Hra.Sedma, _trump ?? _g.trump, gameStartingPlayerIndex, sevenBidding, i);

                calc.CalculateMoney();
                //_sevenStrings.Add(GetComputationResultString(i, calc));

                return calc;
            }).Union(gameComputationResults.Where(i => (i.GameType & Hra.Kilo) != 0).Select((i, idx) =>
            {
                var calc = GetMoneyCalculator(Hra.Kilo, _trump ?? _g.trump, gameStartingPlayerIndex, sevenBidding, i);

                calc.CalculateMoney();
                //_sevenStrings.Add(GetComputationResultString(i, calc));

                return calc;
            }).Union(durchComputationResults.Select(i =>
            {
                var calc = GetMoneyCalculator(Hra.Durch, null, gameStartingPlayerIndex, durchBidding, i);
                //_durchStrings.Add(GetComputationResultString(i, calc));

                calc.CalculateMoney();

                return calc;
            })).Union(betlComputationResults.Select(i =>
            {
                var calc = GetMoneyCalculator(Hra.Betl, null, gameStartingPlayerIndex, betlBidding, i);

                calc.CalculateMoney();
                //_betlStrings.Add(GetComputationResultString(i, calc));

                return calc;
            })))).ToList();
            _betlSimulations = moneyCalculations.Count(i => i.GameType == Hra.Betl);
            _durchSimulations = moneyCalculations.Count(i => i.GameType == Hra.Durch);

            var avgPointsForHundred = moneyCalculations.Where(i => PlayerIndex == gameStartingPlayerIndex
                                                                   ? (i.GameType & Hra.Kilo) != 0
                                                                   : (i.GameType & (Hra.Betl | Hra.Durch)) == 0)
                                                       .DefaultIfEmpty()
                                                       .Average(i => (i?.BasicPointsWon ?? 0) + (i?.MaxHlasWon ?? 0));
            _avgWinForHundred = moneyCalculations.Where(i => PlayerIndex == gameStartingPlayerIndex
                                                                ? (i.GameType & Hra.Kilo) != 0
                                                                : (i.GameType & (Hra.Betl | Hra.Durch)) == 0)
                                                 .DefaultIfEmpty()
                                                 .Average(i => (float)(i?.MoneyWon?[gameStartingPlayerIndex] ?? 0));
            _minWinForHundred = moneyCalculations.Where(i => PlayerIndex == gameStartingPlayerIndex
                                                                ? (i.GameType & Hra.Kilo) != 0
                                                                : (i.GameType & (Hra.Betl | Hra.Durch)) == 0)
                                                 .DefaultIfEmpty()
                                                 .Min(i => i?.MoneyWon?[gameStartingPlayerIndex] ?? 0);
            _avgBasicPointsLost = moneyCalculations.Where(i => (i.GameType & Hra.Hra) != 0)
                                                   .DefaultIfEmpty()
                                                   .Average(i => (float)(i?.BasicPointsLost ?? 0));
            _maxBasicPointsLost = moneyCalculations.Where(i => (i.GameType & Hra.Hra) != 0)
                                                   .DefaultIfEmpty()
                                                   .Max(i => (float)(i?.BasicPointsLost ?? 0));
            //_maxMoneyLost rozhoduje o tom jestli utect na betla
            //proto u aktera do max ztrat pocitam jen hru (s tichym kilem proti)
            //ale u obrancu beru vsechny simulace vcetne kila
            //_maxMoneyLost = moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0)
            _maxMoneyLost = moneyCalculations.Where(i => (TeamMateIndex == -1 &&
                                                          (i.GameType & Hra.Hra) != 0) ||
                                                         (TeamMateIndex != -1 &&
                                                          (i.GameType & (Hra.Betl | Hra.Durch)) == 0))
                                             .DefaultIfEmpty()
                                             .Min(i => i?.MoneyWon?[PlayerIndex] ?? 0);
            _hundredOverBetl = _avgWinForHundred >= 2 * _g.BetlValue;
            _hundredOverDurch = _avgWinForHundred >= 2 * _g.DurchValue;
            _gamesBalance = PlayerIndex == gameStartingPlayerIndex
                            ? moneyCalculations.Any(i => (i.GameType & Hra.Hra) != 0 &&
                                                         (i.GameType & Hra.Sedma) == 0)
                              ? moneyCalculations.Where(i => (i.GameType & Hra.Hra) != 0 &&
                                                             (i.GameType & Hra.Sedma) == 0).Count(i => i.GameWon)
                              : moneyCalculations.Where(i => (i.GameType & Hra.Hra) != 0).Count(i => i.GameWon)
                            : moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => !i.GameWon);
            _gameSimulations = PlayerIndex == gameStartingPlayerIndex
                                ? moneyCalculations.Any(i => (i.GameType & Hra.Hra) != 0 &&
                                                             (i.GameType & Hra.Sedma) == 0)
                                  ? moneyCalculations.Count(i => (i.GameType & Hra.Hra) != 0 &&
                                                                 (i.GameType & Hra.Sedma) == 0)
                                  : moneyCalculations.Count(i => (i.GameType & Hra.Hra) != 0)
                                : moneyCalculations.Count(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0);
            _hundredsBalance =  PlayerIndex == gameStartingPlayerIndex
                                ? moneyCalculations.Where(i => (i.GameType & Hra.Kilo) != 0).Count(i => i.HundredWon)
                                : moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => !i.HundredWon);
            _hundredsAgainstBalance = PlayerIndex == gameStartingPlayerIndex
                                        ? moneyCalculations.Where(i => (i.GameType & Hra.Hra) != 0).Count(i => !i.HundredAgainstWon)
                                        : moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => i.HundredAgainstWon);
            _sevensBalance = PlayerIndex == gameStartingPlayerIndex
                                ? moneyCalculations.Where(i => (i.GameType & Hra.Sedma) != 0).Count(i => i.SevenWon)
                                : moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => !i.SevenWon);
            _sevenSimulations = PlayerIndex == gameStartingPlayerIndex
                                ? moneyCalculations.Count(i => (i.GameType & Hra.Sedma) != 0)
                                : moneyCalculations.Count(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0);
            _sevensAgainstBalance = PlayerIndex == gameStartingPlayerIndex
                                        ? moneyCalculations.Where(i => (i.GameType & Hra.Hra) != 0).Count(i => !i.SevenAgainstWon)
                                        : moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => i.SevenAgainstWon);
            _durchBalance = PlayerIndex == gameStartingPlayerIndex
                                ? moneyCalculations.Where(i => (i.GameType & Hra.Durch) != 0).Count(i => i.DurchWon)
                                : moneyCalculations.Where(i => (i.GameType & Hra.Durch) != 0).Count(i => !i.DurchWon);
            _betlBalance = PlayerIndex == gameStartingPlayerIndex
                                ? moneyCalculations.Where(i => (i.GameType & Hra.Betl) != 0).Count(i => i.BetlWon)
                                : moneyCalculations.Where(i => (i.GameType & Hra.Betl) != 0).Count(i => !i.BetlWon);
            _log.DebugFormat("** Game {0} by {1} {2} times ({3}%)", PlayerIndex == gameStartingPlayerIndex ? "won" : "lost", _g.GameStartingPlayer.Name,
                             _gamesBalance, 100 * _gamesBalance / (_gameSimulations > 0 ? _gameSimulations : 1));
            _log.DebugFormat("** Hundred {0} by {1} {2} times ({3}%)", PlayerIndex == gameStartingPlayerIndex ? "won" : "lost", _g.GameStartingPlayer.Name,
                             _hundredsBalance, 100 * _hundredsBalance / (_hundredSimulations > 0 ? _hundredSimulations : 1));            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Hundred against won {0} times ({1}%)",
                _hundredsAgainstBalance, 100f * _hundredsAgainstBalance / (_gameSimulations > 0 ? _gameSimulations : 1));            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Seven {0} by {1} {2} times ({3}%)", PlayerIndex == gameStartingPlayerIndex ? "won" : "lost", _g.GameStartingPlayer.Name,
                _sevensBalance, 100 * _sevensBalance / (_sevenSimulations > 0 ? _sevenSimulations : 1));            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Seven against won {0} times ({1}%)",
                _sevensAgainstBalance, 100 * _sevensAgainstBalance / (_gameSimulations > 0 ? _gameSimulations : 1));            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Durch won {0} times ({1}%)",
                _durchBalance, 100 * _durchBalance / (_durchSimulations > 0 ? _durchSimulations : 1));            //sgrupuj simulace podle vysledku skore
            _log.DebugFormat("** Betl won {0} times ({1}%)",
                _betlBalance, 100 * _betlBalance / (_betlSimulations > 0 ? _betlSimulations : 1));            //sgrupuj simulace podle vysledku skore
            var scores = moneyCalculations.GroupBy(i => i.PointsWon)
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
                RuleCount = _gamesBalance,
                TotalRuleCount = _gameSimulations
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = "Sedma",
                RuleCount = _sevensBalance,
                TotalRuleCount = _sevenSimulations
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = "Kilo",
                RuleCount = _hundredsBalance,
                TotalRuleCount = _hundredSimulations
            });
            if (_g.trump.HasValue &&
                TeamMateIndex != -1 &&
                Hand.Has7(_g.trump.Value))
            {
                allChoices.Add(new RuleDebugInfo
                {
                    Rule = "Sedma proti",
                    RuleCount = _sevensAgainstBalance,
                    TotalRuleCount = _gameSimulations
                });
            }
            if (_g.trump.HasValue &&
                TeamMateIndex != -1 &&
                Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    .Any(b => Hand.HasK(b) &&
                              Hand.HasQ(b)))
            {
                allChoices.Add(new RuleDebugInfo
                {
                    Rule = "Kilo proti",
                    RuleCount = _hundredsAgainstBalance,
                    TotalRuleCount = _gameSimulations
                });
            }
            allChoices.Add(new RuleDebugInfo
            {
                Rule = "Betl",
                RuleCount = _betlBalance,
                TotalRuleCount = _betlSimulations
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = "Durch",
                RuleCount = _durchBalance,
                TotalRuleCount = _durchSimulations
            });
            DebugInfo.AllChoices = allChoices.ToArray();
            DebugInfo.MaxSimulatedHundredLoss = _minWinForHundred;
            DebugInfo.MaxSimulatedLoss = _maxMoneyLost;
            //DebugInfo.TotalRuleCount = Settings.SimulationsPerGameType;
            gameComputationResults = null;
            moneyCalculations = null;
            scores = null;
            GC.Collect();
        }

        private MoneyCalculatorBase GetMoneyCalculator(Hra gameType, Barva? trump, int gameStartingPlayerIndex, Bidding bidding, GameComputationResult result)
        {
            switch (_g.CalculationStyle)
            {
                case CalculationStyle.Multiplying:
                    return new MultiplyingMoneyCalculator(gameType, trump, gameStartingPlayerIndex, bidding, _g, _g.Calculate107Separately, _g.HlasConsidered, _g.CountHlasAgainst, result);
                case CalculationStyle.Adding:
                default:
                    return new AddingMoneyCalculator(gameType, trump, gameStartingPlayerIndex, bidding, _g, _g.Calculate107Separately, _g.HlasConsidered, _g.CountHlasAgainst, result);
            }
        }

        //public string GetComputationResultString(//string filename, 
        //                                    GameComputationResult result, MoneyCalculatorBase money)
        //{
        //    //using (var fs = _g.GetFileStream(filename))
        //    using(var fs = new MemoryStream())
        //    {
        //        var gameDto = new GameDto
        //        {
        //            Kolo = 10,
        //            Voli = (Hrac)_g.GameStartingPlayerIndex,
        //            Trumf = result.Trump,
        //            Hrac1 = ((List<Card>)result.Hands[0])
        //                            .Select(i => new Karta
        //                            {
        //                                Barva = i.Suit,
        //                                Hodnota = i.Value
        //                            }).ToArray(),
        //            Hrac2 = ((List<Card>)result.Hands[1])
        //                            .Select(i => new Karta
        //                            {
        //                                Barva = i.Suit,
        //                                Hodnota = i.Value
        //                            }).ToArray(),
        //            Hrac3 = ((List<Card>)result.Hands[2])
        //                            .Select(i => new Karta
        //                            {
        //                                Barva = i.Suit,
        //                                Hodnota = i.Value
        //                            }).ToArray(),
        //            //Fleky = fleky,
        //            Stychy = result.Rounds
        //                           .Select((r, idx) => new Stych
        //                            {
        //                                Kolo = idx + 1,
        //                                Zacina = (Hrac)r.RoundStarterIndex
        //                            }).ToArray(),
        //            //Talon = talon
        //            //                .Select(i => new Karta
        //            //                {
        //            //                    Barva = i.Suit,
        //            //                    Hodnota = i.Value
        //            //                }).ToArray()
        //        };
        //        try
        //        {
        //            foreach (var stych in gameDto.Stychy)
        //            {
        //                var r = result.Rounds[stych.Kolo - 1];
        //                var cards = new[] { r.c1, r.c2, r.c3 };
        //                var debugInfo = new[] { r.r1, r.r2, r.r3 };
        //                var playerIndices = new[] { r.RoundStarterIndex, (r.RoundStarterIndex + 1) % Game.NumPlayers, (r.RoundStarterIndex + 2) % Game.NumPlayers };
        //                int index = Array.IndexOf(playerIndices, 0);
        //                stych.Hrac1 = new Karta
        //                {
        //                    Barva = cards[index].Suit,
        //                    Hodnota = cards[index].Value,
        //                    Poznamka = debugInfo[index],
        //                };
        //                index = Array.IndexOf(playerIndices, 1);
        //                stych.Hrac2 = new Karta
        //                {
        //                    Barva = cards[index].Suit,
        //                    Hodnota = cards[index].Value,
        //                    Poznamka = debugInfo[index]
        //                };
        //                index = Array.IndexOf(playerIndices, 2);
        //                stych.Hrac3 = new Karta
        //                {
        //                    Barva = cards[index].Suit,
        //                    Hodnota = cards[index].Value,
        //                    Poznamka = debugInfo[index]
        //                };
        //            }
        //        }
        //        catch (Exception)
        //        {
        //        }
        //        if (money != null)
        //        {
        //            gameDto.Zuctovani = new Zuctovani
        //            {
        //                Hrac1 = new Skore
        //                {
        //                    Body = _g.GameStartingPlayerIndex == 0 ? money.PointsWon : money.PointsLost,
        //                    Zisk = money.MoneyWon[0]
        //                },
        //                Hrac2 = new Skore
        //                {
        //                    Body = _g.GameStartingPlayerIndex == 1 ? money.PointsWon : money.PointsLost,
        //                    Zisk = money.MoneyWon[1]
        //                },
        //                Hrac3 = new Skore
        //                {
        //                    Body = _g.GameStartingPlayerIndex == 2 ? money.PointsWon : money.PointsLost,
        //                    Zisk = money.MoneyWon[2]
        //                }
        //            };
        //        }

        //        gameDto.SaveGame(fs, false);
        //        var sr = new StreamReader(fs);
        //        return sr.ReadToEnd();;
        //    }
        //}

		public bool ShouldChooseDurch()
		{
			var holesPerSuit = new Dictionary<Barva, int>();
			var hiHolePerSuit = new Dictionary<Barva, Card>();
            var dummyTalon = TeamMateIndex == -1
                                ? _talon != null && _talon.Any()
                                    ? _talon
                                    : ChooseDurchTalon(Hand, _trumpCard)
                                : Enumerable.Empty<Card>();

			foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
			{
				var holes = 0;
				var hiHole = new Card(Barva.Cerveny, Hodnota.Sedma);

                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                {
                    var c = new Card(b, h);

                    if (Hand.Any(i => i.Suit == b &&
                                      i.BadValue < c.BadValue &&
                                      !dummyTalon.Contains(i)  &&
                                      !Hand.Contains(c)))
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
                var topCards = Hand.Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                   .Where(h => Card.GetBadValue(h) > i.BadValue)
                                                   .All(h => Probabilities.CardProbability((PlayerIndex + 1) % Game.NumPlayers, new Card(i.Suit, h)) == 0 &&
                                                             Probabilities.CardProbability((PlayerIndex + 2) % Game.NumPlayers, new Card(i.Suit, h)) == 0))
                                   .ToList();
                if (holesPerSuit.All(i => topCards.Count(j => j.Suit == i.Key) >= i.Value || 
                                          hiHolePerSuit[i.Key].BadValue <= minHole.BadValue))
                {
                    return true;
                }
            }
            else //pokud se rozmyslim, zda vzit talon a hrat durcha, tak mam mensi pozadavky
            {
                var minHole = new Card(Barva.Cerveny, Hodnota.Spodek);
                var topCards = Hand.Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                   .Where(h => Card.GetBadValue(h) > i.BadValue)
                                                   .All(h => Probabilities.CardProbability((PlayerIndex + 1) % Game.NumPlayers, new Card(i.Suit, h)) == 0 &&
                                                             Probabilities.CardProbability((PlayerIndex + 2) % Game.NumPlayers, new Card(i.Suit, h)) == 0))
                                   .ToList();
                if (holesPerSuit.All(i => i.Value == 0 ||
                                          hiHolePerSuit[i.Key].BadValue <= minHole.BadValue ||
                                          Hand.CardCount(i.Key) == 1 ||
                                          (topCards.Count(j => j.Suit == i.Key) > 0 &&              //barva ve ktere mam o 2 mene nejvyssich karet nez der 
                                           topCards.Count(j => j.Suit == i.Key) + 2 >= i.Value &&  //doufam v dobrou rozlozenost (simulace ukaze)
                                           Enum.GetValues(typeof(Barva)).Cast<Barva>().Count(b => Hand.CardCount(b) == 1 &&
                                                                                                  !Hand.HasA(b)) <= 1) ||
                                          (topCards.Count(j => j.Suit == i.Key) > 0 &&
                                           Hand.CardCount(i.Key) == 2 &&
                                           Enum.GetValues(typeof(Barva)).Cast<Barva>().Count(b => Hand.CardCount(b) == 2 &&
                                                                                                  Hand.HasA(b) &&
                                                                                                  topCards.Count(j => j.Suit == b) + 2 < holesPerSuit[b]) <= 1) ||
                                          (topCards.Count(j => j.Suit == i.Key) > 0 &&
                                           Hand.CardCount(i.Key) >= 3 &&
                                           Enum.GetValues(typeof(Barva)).Cast<Barva>().Count(b => Hand.CardCount(b) >= 3 &&
                                                                                                  Hand.HasA(b) &&
                                                                                                  Hand.HasQ(b) &&
                                                                                                  topCards.Count(j => j.Suit == b) + 2 < holesPerSuit[b]) <= 1)))
                {
                    return true;
                }
            }
			return false;
		}

        public int GetBetlHoles()   //vola se ChooseGameFlavour pri rozhodovani zda hlasit spatnou barvu a z GetBidsAndDoubles pri rozhodovani zda si dat re na betla
        {
            var totalHoles = 0;

            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                var hiCards = Hand.Count(i => i.Suit == b &&
                                              Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                  .Where(h => i.BadValue > Card.GetBadValue(h))
                                                  .Select(h => new Card(b, h))
                                                  .Any(j => !Hand.Contains(j) &&
                                                            (_talon == null ||
                                                             !_talon.Contains(j))));
                var holes = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                .Where(h => Hand.Any(i => i.BadValue > Card.GetBadValue(h)))
                                .Select(h => new Card(b, h))
                                .Count(i => !Hand.Contains(i) &&
                                            (_talon == null ||
                                             !_talon.Contains(i)));

                totalHoles += Math.Min(hiCards, holes);
            }

            return totalHoles;
        }

        public bool ShouldChooseBetl()
		{
            var talon = TeamMateIndex == -1 ? _talon ?? ChooseBetlTalon(Hand, null) : new List<Card>();                //nasimuluj talon
            var hh = Hand.Where(i => !talon.Contains(i)).ToList();
            var holesPerSuit = new Dictionary<Barva, int>();
            var hiCardsPerSuit = new Dictionary<Barva, int>();

            if (!hh.Any(i => i.Value == Hodnota.Sedma))
            {
                return false;
            }
            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                var holes = 0;      //pocet der v barve
                var hiCards = 0;    //pocet karet ktere maji pod sebou diru v barve

                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                {
                    var c = new Card(b, h);
                    var n = hh.Count(i => i.Suit == b &&
                                          i.BadValue > c.BadValue &&
                                          !hh.Contains(c) &&
                                          !talon.Contains(c));

                    if (n > 0)
                    {
                        holes++;
                        hiCards = Math.Max(hiCards, n);
                    }
                }
                holesPerSuit.Add(b, holes);
                hiCardsPerSuit.Add(b, hiCards);
            }

            var spodek = new Card(Barva.Cerveny, Hodnota.Spodek);
            //max 4 vysoke karty s dirama celkove a max 2 v jedne barve => jednou kartou zacnu hrat, dalsi diry risknu
            //simulace ukazou. pokud jsem puvodne nevolil, je sance, ze nekterou diru zalepi talon ...
            //nebo max jedna barva s hodne vysokymi kartami ale prave jednou dirou (musim mit sedmu v dane barve)
            //nebo pokud mam max 2 vysoke karty
            if (//hh.Count(i => i.BadValue >= spodek.BadValue) <= 2 ||
                (hiCardsPerSuit.Sum(i => i.Value) <= 4 && hiCardsPerSuit.All(i => i.Value <= 3)) ||
                (hiCardsPerSuit.Count(i => i.Value > 2 &&
                                           holesPerSuit[i.Key] <= 2 &&
                                           (hh.Has7(i.Key) ||
                                            talon.Has7(i.Key) ||
                                            hh.Where(j => j.Suit == i.Key)
                                              .All(j => j.BadValue <= Card.GetBadValue(Hodnota.Desitka)))) == 1))
            {
                return true;
            }
            return false;
		}   

        public int EstimateMinBasicPointsLost(List<Card> hand = null, List<Card> talon = null)
        {
            hand = hand ?? Hand;
            talon = talon ?? _talon ?? new List<Card>();

            var minBasicPointsLost = EstimateBasicPointsLost(hand, talon, estimateMaxPointsLost: false);

            DebugInfo.MinBasicPointsLost = minBasicPointsLost;

            return minBasicPointsLost;
        }

        public int EstimateFinalBasicScore(List<Card> hand = null, List<Card> talon = null)
        {
            hand = hand ?? Hand;
            talon = talon ?? _talon;

            var trump = _trump ?? _g.trump;
            var axCount = hand.Count(i => i.Value == Hodnota.Eso ||         //pocitej vsechny esa
                                           (i.Value == Hodnota.Desitka &&   //desitky pocitej tehdy
                                            (i.Suit == trump.Value ||       //pokud je bud trumfova 
                                            ((hand.CardCount(i.Suit) <= 3 || //nebo pokud mas 3 a mene karet v barve (jen v pripade aktera)
                                              hand.CardCount(trump.Value) >= 5 || //nebo pokud mas 5 a vice trumfu (jen v pripade aktera)
                                              (hand.CardCount(trump.Value) >= 4 &&
                                               hand.HasA(trump.Value)) || //nebo pokud mas trumfove eso a 4 a vice trumfu (jen v pripade aktera)
                                              TeamMateIndex != -1) &&
                                             (hand.HasA(i.Suit) ||          //(pokud jsem akter, mam v barve hodne karet a malo trumfu, tak asi X neuhraju)
                                              hand.HasK(i.Suit) ||          //netrumfovou desitku pocitej jen pokud ma k sobe A, K nebo filka+1
                                              (hand.HasQ(i.Suit) &&
                                               hand.CardCount(i.Suit) > 2) ||
                                              (hand.HasJ(i.Suit) &&
                                               hand.CardCount(i.Suit) > 3))))));
            var trumpCount = hand.CardCount(trump.Value);
            var cardsPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>().ToDictionary(b => b, b => hand.CardCount(b));
            var aceOnlySuits = cardsPerSuit.Count(i => i.Value == 1 && 
                                                       hand.HasA(i.Key) &&
                                                       (talon == null ||
                                                        !talon.HasX(i.Key))); //zapocitame desitku pokud mame od barvy jen eso a desitka neni v talonu
            var emptySuits = cardsPerSuit.Count(i => i.Value == 0 &&
                                                     (talon == null ||
                                                      (!talon.HasA(i.Key)) &&
                                                       !talon.HasX(i.Key)));
            var singleLowSuits = cardsPerSuit.Count(i => i.Value == 1 &&
                                                         i.Key != trump.Value &&
                                                         !hand.HasA(i.Key) &&
                                                         !hand.HasX(i.Key) &&
                                                         !hand.HasK(i.Key) &&
                                                         !hand.HasQ(i.Key) &&
                                                         (talon == null ||
                                                          !talon.HasX(i.Key))); //zapocitame desitku pokud mame od barvy jen eso a desitka neni v talonu
            if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    .All(b => !hand.HasA(b) &&
                              !(hand.HasX(b) &&
                                (hand.HasK(b) ||
                                 (hand.HasQ(b) &&
                                  hand.CardCount(b) >= 3)))) &&
                !(hand.CardCount(trump.Value) >= 5 &&
                  hand.HasK(trump.Value) &&
                  hand.HasQ(trump.Value)))
            {
                emptySuits = 0;
                aceOnlySuits = 0;
                singleLowSuits = 0;
            }
            var axPotentialDeduction = GetTotalHoles(hand, talon, false) / 2;
            var axWinPotential = Math.Min(2 * emptySuits + aceOnlySuits + (trumpCount >= 4 ? singleLowSuits : 0),
                                          trumpCount == 4 ? 3 : (int)Math.Ceiling(trumpCount / 2f)); // ne kazdym trumfem prebiju a nebo x

            if (PlayerIndex == _g.GameStartingPlayerIndex &&    //mam-li malo trumfu, tak ze souperu moc A,X nedostanu
                (trumpCount <= 3 ||
                 (trumpCount == 4 &&
                  (!hand.HasA(trump.Value) ||
                   !hand.HasX(trump.Value) ||
                   !hand.HasK(trump.Value) ||
                   !hand.HasQ(trump.Value)))))
            {
                if (_g.Bidding.SevenMultiplier > 1)
                {
                    axWinPotential = 0;
                }
                else if (cardsPerSuit.Count(i => i.Value > 0) == 2 &&
                         hand.CardCount(Hodnota.Eso) < 2)
                {
                    axWinPotential = 0;
                }
                else if (cardsPerSuit.Count(i => i.Value > 0) == 3 &&
                         trumpCount < 4 &&
                         axWinPotential > 1)
                {
                    axWinPotential = 1;
                }
                if(trumpCount <= 2)
                {
                    axWinPotential = 0;
                }
            }
            var n = 10 * (axCount + axWinPotential);
            var hiTrumps = hand.Count(i => i.Suit == trump.Value &&
                                           i.Value >= Hodnota.Svrsek);

            if (trumpCount > 5 ||
                (trumpCount >= 4 &&
                 (hiTrumps >= 2 ||
                  hand.CardCount(Hodnota.Eso) >= 3) &&
                 (hand.HasA(trump.Value) ||
                  (hand.HasX(trump.Value) &&
                   ((PlayerIndex == _g.GameStartingPlayerIndex &&
                     cardsPerSuit.All(i => i.Value > 0)) ||
                    hand.Average(i => (float)i.Value) >= (float)Hodnota.Kral)))))
            {
                n += 10;
            }
            var kqScore = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                              .Where(b => Hand.HasK(b) && Hand.HasQ(b))
                              .Sum(b => b == trump.Value ? 40 : 20);

            //if (trumpCount <= 4 &&
            //    PlayerIndex == _g.GameStartingPlayerIndex &&
            //    !hand.HasA(trump.Value))
            //{
            //    n -= 10;
            //}
            //if (trumpCount <= 2 && PlayerIndex != _g.GameStartingPlayerIndex)
            //{
            //    n -= 10;
            //}
            if (n >= 60 && emptySuits > 0 && kqScore > 0)  //pokud to vypada na kilo, odecti body za diry na ktere si souperi muzou namazat
            {
                n -= axPotentialDeduction * 10;
            }
            if (n < 0)
            {
                n = 0;
            }
            _debugString.AppendFormat("EstimatedFinalBasicScore: {0}\n", n);
            DebugInfo.EstimatedFinalBasicScore = n;

            return n;
        }

        public bool Is100AgainstPossible(int scoreToQuery = 100)
        {
            return EstimateMaxTotalPointsLost() >= scoreToQuery;
        }

        public int EstimateMaxTotalPointsLost(List<Card> hand = null, List<Card> talon = null)
        {
            hand = hand ?? Hand;
            talon = talon ?? _talon;

            var noKQSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => !hand.HasK(b) &&
                                            !hand.HasQ(b) &&
                                            (talon == null ||
                                             (!talon.HasK(b) &&
                                              !talon.HasQ(b))));
            var estimatedKQPointsLost = noKQSuits.Sum(b => b == _trump ? 40 : 20);
            var estimatedBasicPointsLost = EstimateBasicPointsLost(hand, talon);
            var estimatedPointsLost = estimatedBasicPointsLost + estimatedKQPointsLost;

            DebugInfo.MaxEstimatedBasicPointsLost = estimatedBasicPointsLost;
            DebugInfo.MaxEstimatedHlasPointsLost = estimatedKQPointsLost;
            _debugString.AppendFormat("MaxEstimatedLoss: {0} pts\n", estimatedPointsLost);

            return estimatedPointsLost;
        }

        public int EstimateBasicPointsLost(List<Card> hand = null, List<Card> talon = null, bool estimateMaxPointsLost = true)
        {
            hand = hand ?? Hand;
            talon = talon ?? _talon ?? new List<Card>();

            var trump = _trump ?? _g.trump;
            var noKQSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => !hand.HasK(b) &&
                                            !hand.HasQ(b) &&
                                            !talon.HasK(b) &&
                                            !talon.HasQ(b));
            var opponentAXCount = 8 - hand.CardCount(Hodnota.Eso) - hand.CardCount(Hodnota.Desitka);
            var weakXs = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => hand.HasX(b) &&
                                            ((b == _trump.Value &&
                                              hand.CardCount(b) == 1) ||
                                             (b != _trump.Value &&
                                              !(hand.HasA(b) ||
                                                hand.HasK(b) ||
                                                (hand.HasQ(b) &&
                                                 hand.HasJ(b)) ||
                                                ((hand.HasQ(b) ||
                                                  hand.HasJ(b)) &&
                                                 hand.Has9(b) &&
                                                 hand.Has8(b) &&
                                                 hand.CardCount(_trump.Value) >= 3) ||
                                                (hand.CardCount(b) >= 5 &&
                                                 hand.CardCount(_trump.Value) >= 4)))))
                                .ToList();

            var estimatedBasicPointsLost = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .Where(b => hand.HasSuit(b) &&
                                                           GetTotalHoles(hand, talon, b) > 0)   //pro kazdou barvu kde mam diry souperum pricti
                                               .Sum(b => (hand.HasA(b) ? 0 : 10) +              //body za souperovo eso
                                                         ((hand.HasX(b) &&                      //body za souperovu desitku
                                                           !weakXs.Contains(b)) ||              //nebo za mou desitku kterou asi neuhraju
                                                          (hand.CardCount(b) == 1 &&            //mam-li jen jednu kartu v barve unikne mi jen jedna desitka
                                                           !weakXs.Contains(b))                 //pokud to neni plonkova desitka
                                                          ? 0 : 10) +
                                                         (estimateMaxPointsLost                 //body ktere souperi muzou namazat
                                                          ? 10 * GetTotalHoles(hand, talon, b) : 0));
            estimatedBasicPointsLost = Math.Min(estimatedBasicPointsLost, (opponentAXCount + weakXs.Count) * 10);//uprav maximum souperovych bodu

            if (estimateMaxPointsLost &&
                ((estimatedBasicPointsLost >= 50 &&   //mam-li malo trumfu a hodne prohranych bodu, asi neuhraju ani posledni stych
                  (hand.CardCount(_trump.Value) == 4 &&
                   !hand.HasA(_trump.Value))) ||
                 (estimatedBasicPointsLost >= 40 &&
                  hand.CardCount(_trump.Value) <= 3)))
            {
                estimatedBasicPointsLost += 10;
            }

            return estimatedBasicPointsLost;
        }

        public bool IsSevenTooRisky(List<Card> hand = null, List<Card> talon = null)
        {
            hand = hand ?? Hand;
            talon = talon ?? _talon ?? new List<Card>();
            
            var result = hand.CardCount(_trump.Value) < 4 ||                      //1.  mene nez 4 trumfy
                         (hand.CardCount(_trump.Value) == 4 &&
                          TeamMateIndex != -1 &&
                          (!_teamMateDoubledGame ||
                           _g.MandatoryDouble) &&
                          !(hand.HasA(_trump.Value) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => hand.HasSuit(b))
                                .All(b => hand.HasA(b) ||
                                          (hand.HasX(b) &&
                                           (hand.HasK(b) ||
                                            (hand.HasQ(b) &&
                                             hand.CardCount(b) >= 3))))) &&
                          !(hand.HasK(_trump.Value) &&
                            hand.HasQ(_trump.Value) &&
                            hand.SuitCount() == 4 &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => hand.HasSuit(b) &&
                                            b != _trump.Value)
                                .All(b => hand.CardCount(b) >= 2 &&
                                          hand.Any(i => i.Suit == b &&
                                                        i.Value >= Hodnota.Desitka))) &&
                          !(hand.HasA(_trump.Value) &&
                            (hand.HasX(_trump.Value) ||
                             hand.HasK(_trump.Value) ||
                             hand.HasQ(_trump.Value)) &&
                            hand.SuitCount() == 4 &&
                            hand.Count(i => i.Value == Hodnota.Eso ||
                                            (i.Value == Hodnota.Desitka &&
                                             hand.HasA(i.Suit))) >= 4));
            result |= hand.CardCount(_trump.Value) <= 4 &&                  //2. nebo max. 4 trumfy a
                      (TeamMateIndex == -1 ||
                       (!_teamMateDoubledGame ||
                        _g.MandatoryDouble)) &&
                       hand.SuitCount() >= 3 &&
                       !(Enum.GetValues(typeof(Barva)).Cast<Barva>()         //aspon v jedne barve nemam A nebo X+K nebo K+Q a
                             .Where(b => hand.HasSuit(b))
                             .All(b => hand.HasA(b) ||
                                       (hand.HasX(b) &&
                                        (hand.HasK(b) ||
                                         hand.CardCount(b) > 2)) ||
                                        (hand.HasK(b) &&
                                         hand.HasQ(b))) ||
                         Enum.GetValues(typeof(Barva)).Cast<Barva>()         //aspon v jedne barve nemam A nebo X+K a
                             .Where(b => hand.HasA(b))
                             .Sum(b => hand.Count(i => i.Value >= Hodnota.Kral)) > 4) &&
                       (!hand.HasA(_trump.Value) ||                          //nemam trumfove A nebo X a
                        !hand.HasX(_trump.Value)) &&
                       !(hand.HasK(_trump.Value) &&                          //nevidim do trumfoveho hlasu a
                         hand.HasQ(_trump.Value)) &&
                       !(//Enum.GetValues(typeof(Barva)).Cast<Barva>()       //neni pravda ze
                         //    .Where(b => b != _trump &&                    //2a. mam dlouhou tlacnou barvu nebo
                         //                hand.HasSuit(b))
                         //    .Any(b => hand.CardCount(b) >= 5) ||
                         hand.Where(i => i.Suit != _trump)                   //2b. mam vysoke netrumfove karty nebo
                             .All(i => i.Value >= Hodnota.Svrsek) ||
                         Enum.GetValues(typeof(Barva)).Cast<Barva>()         //2c. mam v kazde netrumfove barve A nebo X+K nebo X+2 nebo
                             .Where(b => b != _trump &&
                                         hand.HasSuit(b))
                             .All(b => hand.HasA(b) ||
                                       (hand.HasX(b) &&
                                        (hand.HasK(b) ||
                                         hand.CardCount(b) > 2)) ||
                                        (hand.HasK(b) &&
                                         hand.HasQ(b))));// ||
                         //hand.Where(i => i.Suit != _trump.Value)             //2d. aspon dve esa
                         //    .Count(i => i.Value == Hodnota.Eso) >= 2);
            result |= hand.CardCount(_trump.Value) <= 4 &&
                      hand.CardCount(Hodnota.Eso) <= 2 &&
                      !(hand.HasA(_trump.Value) &&
                        hand.HasX(_trump.Value)) &&
                      !((hand.HasA(_trump.Value) ||
                         hand.HasX(_trump.Value)) &&
                        hand.HasK(_trump.Value) &&
                        hand.HasQ(_trump.Value)) &&
                      !(Enum.GetValues(typeof(Barva)).Cast<Barva>()         //nemam v kazde netrumfove barve A nebo X+K nebo X+2 a jedno z
                            .Where(b => hand.HasSuit(b) &&
                                        b != _trump)
                            .All(b => hand.HasA(b) ||
                                      (hand.HasX(b) &&
                                       (hand.HasK(b) ||
                                        hand.CardCount(b) > 2)) ||
                                       (hand.HasK(b) &&
                                        hand.HasQ(b))));
            result |= hand.CardCount(_trump.Value) == 4 &&                  //3. nebo 4 trumfy a
                      (TeamMateIndex == -1 ||
                       (!_teamMateDoubledGame ||
                        _g.MandatoryDouble)) &&
                      !(Enum.GetValues(typeof(Barva)).Cast<Barva>()         //nemam v kazde netrumfove barve A nebo X+K nebo X+2 a jedno z
                        .Where(b => hand.HasSuit(b))
                        .All(b => hand.HasA(b) ||
                                    (hand.HasX(b) &&
                                    (hand.HasK(b) ||
                                    hand.CardCount(b) > 2)) ||
                                    (hand.HasK(b) &&
                                    hand.HasQ(b)))) &&
                      (hand.Count(i => i.Value >= Hodnota.Svrsek) < 3 ||    //3a. mene nez 3 vysoke karty celkem
                       (!hand.HasA(_trump.Value) &&                         //3a2.mam 4 trumfy bez esa a aspon jednu dalsi barvu bez esa
                        hand.CardCount(Hodnota.Eso) < 2) ||
                       (!(((hand.HasA(_trump.Value) &&
                            (hand.HasX(_trump.Value) ||
                             hand.HasK(_trump.Value) ||
                             hand.HasQ(_trump.Value))) ||
                           (hand.HasK(_trump.Value) &&
                            hand.HasQ(_trump.Value)) ||
                           GetTotalHoles(hand, talon, false) <= 3) &&
                        hand.SuitCount() == 4 &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => hand.HasSuit(b) &&
                                        b != _trump.Value)
                            .All(b => hand.CardCount(b) >= 2 &&
                                        hand.Any(i => i.Suit == b &&
                                                    i.Value >= Hodnota.Desitka))) &&
                        hand.Count(i => i.Value == Hodnota.Eso) +           //3b. nebo mene nez 2 (resp. 3) uhratelne A, X
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Count(b => (hand.HasX(b) &&
                                            (hand.HasK(b) ||
                                            hand.HasA(b) ||
                                            (hand.HasQ(b) &&
                                            hand.CardCount(b) > 2)))) < (TeamMateIndex == -1 ? 2 : 3) &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump.Value)
                            .All(b => hand.CardCount(b) < 4)) ||            //   (vyjma pripadu kdy mam dlouhou netrumfovou tlacnou barvu)
                        (hand.Select(i => i.Suit).Distinct().Count() < 4 &&  //3c. nebo nevidim do nejake barvy
                        !(((hand.HasA(_trump.Value) ||                             //    (vyjma pripadu kdy mam trumfove A nebo X a max. 2 neodstranitelne netrumfove diry)
                            hand.HasX(_trump.Value)) &&
                            GetTotalHoles(hand, talon, false, false) <= 2) ||                //    (nebo vyjma pripadu kdy mam X,K,Q trumfove a k tomu netrumfove eso
                            (hand.HasX(_trump.Value) &&
                            hand.HasK(_trump.Value) &&
                            (hand.HasQ(_trump.Value) ||
                            hand.HasJ(_trump.Value)) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump.Value &&
                                            hand.HasSuit(b))
                                .Any(b => hand.HasA(b))))));
        result |= hand.CardCount(_trump.Value) == 5 &&                  //4.  5 trumfu a nemam trumfove A+K+S
                  (TeamMateIndex == -1 ||
                   (!_teamMateDoubledGame ||
                    _g.MandatoryDouble)) &&
                  hand.Count(i => i.Suit == _trump.Value &&
                                  i.Value >= Hodnota.Svrsek) < 3 &&
                  !(hand.HasA(_trump.Value) &&
                    (hand.HasX(_trump.Value) ||
                     hand.HasK(_trump.Value) ||
                     hand.HasQ(_trump.Value))) &&
                  ((hand.Count(i => i.Value >= Hodnota.Svrsek) < 3 &&
                    hand.Count(i => i.Value >= Hodnota.Spodek) < 4) ||  //4a. mene nez 3 (resp. 4) vysoke karty celkem (plati pro aktera i protihrace)
                   (hand.Select(i => i.Suit).Distinct().Count() < 4 &&  //4b. nebo nevidim do nejake barvy a zaroven mam 4 a vice netrumfovych der
                    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        .Where(b => b != _trump.Value)
                        .Count(b => !hand.HasA(b) &&
                                    hand.Count(i => i.Suit == b &
                                                    i.Value >= Hodnota.Svrsek) <
                                    hand.Count(i => i.Suit == b &&
                                                    i.Value <= Hodnota.Spodek)) > 1));
            DebugInfo.SevenTooRisky = hand.Has7(_trump.Value) && result;

            return result;
        }

        public bool IsDurchCertain()
        {
            var topCardsPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                      .ToDictionary(b => b,
                                                    b => Hand.Count(i => i.Suit == b &&
                                                                         Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                             .Where(h => Card.GetBadValue(h) > i.BadValue)
                                                                             .All(h => Probabilities.CardProbability((PlayerIndex + 1) % Game.NumPlayers, new Card(i.Suit, h)) == 0 &&
                                                                                       Probabilities.CardProbability((PlayerIndex + 2) % Game.NumPlayers, new Card(i.Suit, h)) == 0)));
            var holesPerSuit = new Dictionary<Barva, int>();
            var hiHolePerSuit = new Dictionary<Barva, Card>();

            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                var holes = 0;
                var hiHole = new Card(Barva.Cerveny, Hodnota.Sedma);

                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                {
                    var c = new Card(b, h);

                    if (Hand.Any(i => i.Suit == b &&
                                      i.BadValue < c.BadValue &&
                                      !_talon.Contains(i) &&
                                      !Hand.Contains(c)))
                    {
                        holes++;
                        if (c.BadValue > hiHole.BadValue)
                        {
                            hiHole = c;
                        }
                    }
                }

                holesPerSuit.Add(b, holes);
            }
            var isDurchTooRisky = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                      .Any(b => Hand.HasSuit(b) &&
                                                holesPerSuit[b] > topCardsPerSuit[b]);

            return !isDurchTooRisky;
        }

        public bool IsHundredTooRisky(List<Card> hand = null, List<Card> talon = null)
        {
            hand = hand ?? Hand;
            talon = talon ?? _talon ?? new List<Card>();

            var minBasicPointsLost = EstimateMinBasicPointsLost(hand, talon);
            var maxAllowedPointsLost = hand.HasK(_g.trump.Value) &&
                                       hand.HasQ(_g.trump.Value)
                                        ? 30 : 10;
            var kqScore = _g.trump.HasValue
                ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
                      .Where(b => Hand.HasK(b) && Hand.HasQ(b))
                      .Sum(b => b == _g.trump.Value ? 40 : 20)
                : 0;

            if (kqScore == 0 ||                
                minBasicPointsLost > maxAllowedPointsLost)
            {
                return true;
            }
            if (Settings.SafetyHundredThreshold > 0 &&
                _minWinForHundred < -Settings.SafetyHundredThreshold)
            {
                DebugInfo.HundredTooRisky = true;
                return true;
            }

            var maxBasicPointsLost = EstimateBasicPointsLost(hand, talon);
            //var result = basicPointsLost > (hand.HasK(_g.trump.Value) &&
            //                                hand.HasQ(_g.trump.Value)
            //                                ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
            //                                      .Count(b => GetTotalHoles(hand.Union(talon).ToList(), b) > 0) > 2 ||
            //                                  Enum.GetValues(typeof(Barva)).Cast<Barva>()
            //                                      .Where(b => b != _g.trump.Value &&
            //                                                  GetTotalHoles(hand.Union(talon).ToList(), b) > 0)
            //                                      .Any(b => hand.CardCount(b) >= 5) || //u dlouhych barev s dirou je sance ze budou mazat
            //                                  hand.CardCount(_g.trump.Value) >= 5 //maji-li souperi malo trumfu, je vetsi sance ze budou mazat
            //                                      ? 30 : 40
            //                                : 10);
            ////mam-li trumf. hlas a diry v max 2 barvach tak 40 bodu proti risknu a uvidime jestli vyjde
            //if (_g.HlasConsidered == HlasConsidered.Each)
            //{
            //    result = 90 - basicPointsLost + kqScore <= 90;
            //}

            if (maxBasicPointsLost <= maxAllowedPointsLost)
            {
                DebugInfo.HundredTooRisky = false;
                return false;
            }
            var handPlusTalon = hand.Concat(talon).ToList();
            var n = GetTotalHoles(hand, talon);
            var nn = GetTotalHoles(hand, talon, false);
            var sh = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                         .Where(b => GetTotalHoles(hand, talon, b) > 0)
                         .ToList();
            var axCount = hand.Count(i => i.Value == Hodnota.Eso || i.Value == Hodnota.Desitka);
            var noKQsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Count(b => !handPlusTalon.HasK(b) &&
                                            !handPlusTalon.HasQ(b));
            var kqMaxOpponentScore = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                         .Where(b => !hand.HasK(b) &&
                                                     !hand.HasQ(b) &&
                                                     (!talon.HasK(b) &&
                                                      !talon.HasQ(b)))
                                         .Sum(b => b == _g.trump.Value ? 40 : 20);
            var kqLikelyOpponentScore = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Count(b => !hand.HasK(b) &&
                                                        !hand.HasQ(b) &&
                                                        !talon.HasK(b) &&
                                                        !talon.HasQ(b)) <= 1
                                        ? kqMaxOpponentScore
                                        : kqMaxOpponentScore - 20;
            var potentialBasicPointsLost = minBasicPointsLost + (hand.HasA(_g.trump.Value) ? 0 : 10);
            var lossPerPointsLost = Enumerable.Range(1, maxAllowedPointsLost == 30 ? 16 : 18)
                                              .Select(i => maxAllowedPointsLost + i * 10)
                                              .ToDictionary(k => k,
                                                            v => (_g.CalculationStyle == CalculationStyle.Adding
                                                                  ? (v - maxAllowedPointsLost)/ 10 * _g.HundredValue
                                                                  : (1 << (v - maxAllowedPointsLost - 10) / 10) * _g.HundredValue) *
                                                                 (_g.trump.Value == Barva.Cerveny
                                                                  ? 4
                                                                  : 2));
            if (lossPerPointsLost.ContainsKey(maxBasicPointsLost + kqLikelyOpponentScore))
            {
                DebugInfo.EstimatedHundredLoss = -lossPerPointsLost[maxBasicPointsLost + kqLikelyOpponentScore];
            }
            //pokud hrozi vysoka prohra, tak do kila nejdi
            if (lossPerPointsLost.ContainsKey(maxBasicPointsLost + kqLikelyOpponentScore) &&
                 lossPerPointsLost[maxBasicPointsLost + kqLikelyOpponentScore] > Settings.SafetyHundredThreshold)
            {
                DebugInfo.HundredTooRisky = true;
                return true;
            }
            if (lossPerPointsLost.ContainsKey(potentialBasicPointsLost + kqMaxOpponentScore) &&
                lossPerPointsLost[potentialBasicPointsLost + kqMaxOpponentScore] > Settings.SafetyHundredThreshold)
            {
                DebugInfo.EstimatedHundredLoss = Math.Min(DebugInfo.EstimatedHundredLoss, -lossPerPointsLost[potentialBasicPointsLost + kqMaxOpponentScore]);
                DebugInfo.HundredTooRisky = true;
                return true;
            }

            if ((!hand.HasA(_trump.Value) ||                    //na souperuv A nebo X trumf muze prijit namaz: 20 bodu
                 (!hand.HasX(_trump.Value) &&
                  hand.CardCount(_trump.Value) <= 6)) &&        //neplati pokud mam vic nez 6 trumfu - trumfovou X pak vytahnu
                Enum.GetValues(typeof(Barva)).Cast<Barva>()     //k tomu prijdu o dalsich 20 bodu u plonkove X
                    .Where(b => hand.HasSuit(b) &&
                                b != _trump.Value)
                    .Any(b => hand.HasSolitaryX(b)))
            {
                DebugInfo.HundredTooRisky = true;
                return true;
            }
            if (axCount >= 6)
			{
                DebugInfo.HundredTooRisky = false;
				return false;
			}
            if (nn == 0 &&
                hand.CardCount(_trump.Value) >= 5 &&
                hand.HasK(_trump.Value) &&
                hand.HasQ(_trump.Value) &&
                hand.CardCount(Hodnota.Eso) >= 3)
            {
                return false;
            }
            if (hand.HasA(_trump.Value) &&
                hand.HasX(_trump.Value) &&
                hand.HasK(_trump.Value) &&
                hand.HasQ(_trump.Value) &&
                Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    .Where(b => hand.HasSuit(b) &&
                                b != _trump.Value)
                    .All(b => (hand.CardCount(b) == 2 ||
                               hand.Count == 12) &&
                              hand.Any(i => i.Suit == b &&
                                            i.Value >= Hodnota.Desitka) &&
                              hand.Any(i => i.Suit == b &&
                                            i.Value >= Hodnota.Spodek)))
            {
                return false;
            }
            if (hand.CardCount(_trump.Value) <= 3 &&
                (n >= 3 ||
                 !(hand.HasK(_trump.Value) &&
                   hand.HasQ(_trump.Value) &&
                   (hand.HasA(_trump.Value) ||
                    hand.HasX(_trump.Value)) &&
                   //hand.CardCount(Hodnota.Eso) == hand.SuitCount()
                   Enum.GetValues(typeof(Barva)).Cast<Barva>()
                       .Where(b => b != _trump &&
                                   hand.HasSuit(b))
                       .All(b => hand.HasA(b)))))
            {
                DebugInfo.HundredTooRisky = true;
                return true;
            }
            if ((!hand.HasK(_trump.Value) ||
                 !hand.HasQ(_trump.Value)) &&
                hand.Any(i => i.Value < Hodnota.Desitka &&
                              !hand.HasA(i.Suit) &&
                              !hand.HasX(i.Suit)))
            {
                return true;
            }
            if (!hand.HasA(_trump.Value) &&
                !hand.HasX(_trump.Value) &&
                Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    .Any(b => b != _trump.Value &&
                              !hand.HasA(b) &&
                              !hand.HasX(b)) &&
                Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    .Count(b => !hand.HasK(b) &&
                                !hand.HasQ(b)) > 1)
            {
                DebugInfo.HundredTooRisky = true;
                return true;
            }
            //if (!((hand.HasK(_trump.Value) || hand.HasQ(_trump.Value)) || //abych nehral kilo pokud aspon netrham a nemam aspon 2 hlasky
            //		 Enum.GetValues(typeof(Barva)).Cast<Barva>()
            //			 .Count(b => hand.HasK(b) && hand.HasQ(b)) >= 2))
            //{
            //	return true;
            //}
            if ((hand.CardCount(_trump.Value) < 4 &&
                 !hand.HasA(_trump.Value) &&                 
                  n >= 3) ||
				(hand.CardCount(_trump.Value) == 4 &&
                 (!hand.HasA(_trump.Value) ||
				  !hand.HasX(_trump.Value)) &&
				 Enum.GetValues(typeof(Barva)).Cast<Barva>()
					 .Where(b => b != _trump && hand.HasSuit(b))
					 .Any(b => !hand.HasA(b) &&
                               !(hand.HasX(b) &&
                                 hand.HasK(b))) &&
				 !(hand.CardCount(Hodnota.Eso) == 3 &&
                   hand.HasK(_trump.Value) &&
                   hand.HasQ(_trump.Value) &&
                   hand.Count(i => i.Value == Hodnota.Desitka &&
                                   (hand.HasA(i.Suit) ||
                                    hand.HasK(i.Suit))) >= 2)))
			{
                DebugInfo.HundredTooRisky = true;
                return true;
			}
			if (hand.Count(i => i.Suit == _trump.Value && i.Value >= Hodnota.Spodek) < 3 &&
                (!hand.HasA(_trump.Value) ||
                 !hand.HasX(_trump.Value)))
			{
                DebugInfo.HundredTooRisky = true;
                return true;
			}
            //Pokud nevidis do trumfoveho hlasu a mas malo trumfu, tak kilo nehraj
            if (hand.CardCount(_trump.Value) <= 4 &&
                !hand.HasK(_trump.Value) &&
                !hand.HasQ(_trump.Value))
            {
                DebugInfo.HundredTooRisky = true;
                return true;
            }
            //Pokud vic nez v jedne barve nemas eso nebo mas vetsi diru tak kilo nehraj. Souperi by si mohli uhrat desitky
            //var dict = new Dictionary<Barva, Tuple<int, Hodnota, Hodnota>>();

            //foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            //{
            //	var topCard = hand.Where(i => i.Suit == b).OrderByDescending(i => i.Value).FirstOrDefault();
            //	var secondCard = hand.Where(i => i.Suit == b).OrderByDescending(i => i.Value).Skip(1).FirstOrDefault();
            //	var holeSize = topCard == null || secondCard == null
            //					? 0
            //					: topCard.Value - secondCard.Value - 1;

            //	dict.Add(b, new Tuple<int, Hodnota, Hodnota>(holeSize, topCard?.Value ?? Hodnota.Eso, secondCard?.Value ?? Hodnota.Eso));
            //}
            //var hiCards = 0;
            //foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            //{
            //    if (hand.HasX(b) ||
            //        (hand.HasA(b) &&
            //         hand.HasK(b)))
            //    {
            //        hiCards += hand.Count(i => i.Suit == b &&
            //                             i.Value >= Hodnota.Spodek);
            //    }
            //}
            //Pokud nevidis do vsech barev a mas barvu s vic nez 2 nizkyma kartama bez A nebo X tak kilo nehraj. Souperi by si mohli uhrat desitky
            if (hand.SuitCount() < Game.NumSuits &&
                n > 3 &&
                Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    .Where(b => b != _trump.Value)
                    .Any(b => hand.CardCount(b) > 2 &&
                              ((!hand.HasA(b) &&
                                !hand.HasX(b)) ||
                               (hand.HasX(b) &&
                                !hand.HasA(b) &&
                                !hand.HasK(b)))) &&
               !(n == 4 &&
                 nn == 3 &&
                 sh.Count == 2))
            {
                DebugInfo.HundredTooRisky = true;
                return true;
            }
            //Pokud mas aspon 2 barvy bez A nebo X tak kilo nehraj. Souperi by si mohli uhrat desitky
            if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    .Where(b => b != _trump.Value)
                    .Count(b => hand.CardCount(b) > 1 &&
                                //hand.CardCount(Hodnota.Eso) + hand.CardCount(Hodnota.Desitka) <= 6 &&
                                (!hand.HasA(b) &&
                                 !hand.HasX(b)) ||
                                (hand.HasX(b) &&
                                 !hand.HasA(b) &&
                                 !hand.HasK(b))) > 1)
            {
                DebugInfo.HundredTooRisky = true;
                return true;
            }
            //Pokud ve vice nez jedne barve mas nizke karty a nemas desitku (ani ji nemuzes vytahnout) tak kilo nehraj. Souperi by si mohli uhrat desitky
            if (axCount < 5 &&
                Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    .Count(b => hand.HasSuit(b) &&
                                !hand.HasX(b) &&
                                //hand.CardCount(b) < 5) &&
                                //!(hand.CardCount(b) == 1 &&
                                //  hand.HasA(b))) > 1)
                                hand.CardCount(b) > 2 &&        //???
                                hand.CardCount(b) < 5) > 1)
            {
                DebugInfo.HundredTooRisky = true;
                return true;
            }

            if (axCount <= 2 &&
                hand.CardCount(_trump.Value) == 7 &&
                n >= 3)
            {
                DebugInfo.HundredTooRisky = true;
                return true;
            }
            //Pokud ve vice nez jedne barve mas diru vetsi nez 2 a mas od ni A ani X+K 
            //if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
            //        .Count(b => dict[b].Item1 > 2 ||
            //                    (dict[b].Item2 != Hodnota.Eso &&
            //                     (dict[b].Item2 != Hodnota.Desitka ||
            //                      dict[b].Item3 < Hodnota.Kral))) > 1)
            //{
            //    return true;
            //}
            var result = n > 4 ||                         //u vice nez 4 neodstranitelnych der kilo urcite neuhraju
                         (n > 3 &&
                          (//nn > 3 ||
                           (nn >= 3 &&//nn == 3 &&
                            sh.Count > 2) ||
                           noKQsuits == 3)) ||
                         (n == 3 &&                       //nebo u 3 neodstranitelnych der pokud nemam trumfove eso
                          (!hand.HasA(_trump.Value) ||    //a mam pet nebo mene trumfu
                           !hand.HasX(_trump.Value)) &&
                          hand.CardCount(_trump.Value) <= 6 &&
                          !(//hand.HasX(_trump.Value) &&   //a nemam jednu z trumfovych X, K, F
                            hand.CardCount(_trump.Value) >= 4 &&
                            hand.HasK(_trump.Value) &&
                            hand.HasQ(_trump.Value))) ||
			             (n > 2 &&                        //pokud mam vic nez 2 neodstranitelne diry
                          !(hand.HasK(_trump.Value) &&    //a nemam trumfovou hlasku, tak taky ne
                            hand.HasQ(_trump.Value))) ||
                         (n > 1 &&                        //pokud mam vic nez 2 neodstranitelne diry
                          !(hand.HasA(_trump.Value) &&    //a nemam trumfove eso ani desitku na navic
                            hand.HasX(_trump.Value)) &&
                          !(hand.HasK(_trump.Value) &&    //a nemam trumfovou hlasku, tak taky ne
                            hand.HasQ(_trump.Value))) ||
                         (n == 1 &&                       //nebo pokud mam jednu diru
                          !hand.HasA(_trump.Value) &&     //nemam trumfove eso
                          !(hand.HasK(_trump.Value) &&    //a nemam trumfovou hlasku, tak taky ne
                            hand.HasQ(_trump.Value))) ||
                         (n >= 1 &&                       //pokud mam nejake diry
                          !(hand.HasK(_trump.Value) &&    //a nemam trumfovou hlasku
                            hand.HasQ(_trump.Value)) &&
                          hand.Any(i => i.Value == Hodnota.Desitka &&   //a mam desitku bez esa
                                        !hand.HasA(i.Suit) &&
                                        hand.CardCount(i.Suit) +
                                        _talon?.CardCount(i.Suit) >= 4 &&  //od delsi barvy (souper muze mit eso i vsechny zbyle trumfy)
                                        hand.CardCount(_trump.Value) < 8 &&
                                        hand.CardCount(Hodnota.Eso) +
                                        hand.CardCount(Hodnota.Desitka) < 7));
            System.Diagnostics.Debug.WriteLine(n);
            System.Diagnostics.Debug.WriteLine(nn);
            DebugInfo.HundredTooRisky = result;

            return result;
        }

        public int GetTotalHoles(List<Card> hand, List<Card> talon, Barva b)
        {
            hand = hand ?? Hand;
            talon = talon ?? _talon ?? new List<Card>();

            if (!hand.HasSuit(b))
            {
                return 0;
            }

            var myCards = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                              .OrderByDescending(h => (int)h)
                              .Select(h => new KeyValuePair<Hodnota, bool>(h, hand.Any(j => j.Suit == b &&
                                                                                            j.Value == h)))
                              .ToList();
            var opCards = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()   //vsechny hodnoty ktere v dane barve neznam
                              .OrderByDescending(h => (int)h)
                              .Select(h => new KeyValuePair<Hodnota, bool>(h, !hand.Any(i => i.Suit == b &&
                                                                                             i.Value == h) &&
                                                                              !talon.Any(i => i.Suit == b &&
                                                                                              i.Value == h)))
                              .ToList();
            var totalHoles = 0;

            for (var i = 0;
                 i < myCards.Count &&            //prochazej vsechny karty v barve odshora, skonci kdyz
                 opCards.Any(j => j.Value) &&    //souperum nezbyly zadne karty nebo
                 myCards.Skip(i)
                        .Any(j => j.Value);      //ja nemam na ruce zadne dalsi karty
                 i++)
            {
                if (myCards[i].Value)
                {
                    if (i > 0 &&
                        opCards.Take(i)
                               .Any(j => j.Value))
                    {
                        //nemame nejvyssi kartu
                        //odeberu nejblizsi diru vyssi nez moje aktualni karta
                        var opHighCardIndex = opCards.Take(i)
                                                     .ToList()
                                                     .FindLastIndex(j => j.Value);

                        opCards[opHighCardIndex] = new KeyValuePair<Hodnota, bool>(opCards[opHighCardIndex].Key, false);
                        totalHoles++;
                    }
                    else
                    {
                        //mame nejvyssi kartu, odeberu diru odspoda
                        var loHoleIndex = opCards.FindLastIndex(j => j.Value);

                        opCards[loHoleIndex] = new KeyValuePair<Hodnota, bool>(opCards[loHoleIndex].Key, false);
                    }
                }
            }

            return totalHoles;
        }

        public int GetTotalHoles(List<Card> hand = null, List<Card> talon = null, bool includeTrumpSuit = true, bool includeAceSuits = true)
        {
            hand = hand ?? Hand;
            talon = talon ?? _talon ?? new List<Card>();

            //neodstranitelne diry jsou takove, ktere zbydou pokud zahraju nejvyssi karty od dane barvy
            //kdyz jich je moc, tak hrozi, ze se nedostanu do stychu a souperi ze me budou tahat trumfy
            //proto v takovem pripade sedmu nehraj
            var n = 0;

            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                if (!includeTrumpSuit && _trump.HasValue && b == _trump.Value)
                {
                    continue;
                }
                if (!includeAceSuits && hand.HasA(b))
                {
                    continue;
                }

                n += GetTotalHoles(hand, talon, b);
            }
            if (includeTrumpSuit && includeAceSuits)
            {
                _debugString.Append($"TotalHoles: {n}\n");
                DebugInfo.TotalHoles = n;
            }

            return n;
        }

        public bool ShouldChooseHundred()
		{
            var trump = _trump ?? _g.trump;
			var axCount = Hand.Count(i => i.Value == Hodnota.Eso || 
			                         	  (i.Value == Hodnota.Desitka && 
			                          	   Hand.Any(j => j.Suit == i.Suit && 
			                                   			 (j.Value == Hodnota.Eso || j.Value == Hodnota.Kral))));
			var trumpCount = Hand.Count(i => i.Suit == trump && i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka); //bez A,X
			var axTrumpCount = Hand.Count(i => i.Suit == trump && (i.Value == Hodnota.Eso ||
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
			if (kqs.Any(i => i == trump))
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
            Hra gameType = 0;

            if ((validGameTypes & (Hra.Betl | Hra.Durch)) != 0)
            {
                if (Settings.CanPlayGameType[Hra.Durch] && 
                    (_durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][0] * _durchSimulations && 
                     _durchSimulations > 0 &&
                     !_hundredOverDurch) ||
				    validGameTypes == Hra.Durch)
                {
                    gameType = Hra.Durch;
                    DebugInfo.RuleCount = _durchBalance;
                    DebugInfo.TotalRuleCount = _durchSimulations;
                }
                else if (Settings.CanPlayGameType[Hra.Betl] && 
                         _betlBalance >= Settings.GameThresholdsForGameType[Hra.Betl][0] * _betlSimulations && _betlSimulations > 0 &&
                         !_hundredOverBetl &&
                         !(_betlBalance < Settings.GameThresholdsForGameType[Hra.Betl][1] * _betlSimulations &&
                           _sevensBalance >= Settings.GameThresholdsForGameType[Hra.Sedma][0] * _sevenSimulations && _sevenSimulations > 0))
                {
                    gameType = Hra.Betl;
                    DebugInfo.RuleCount = _betlBalance;
                    DebugInfo.TotalRuleCount = _betlSimulations;
                }
                else if (validGameTypes == (Hra.Betl | Hra.Durch))
                {
                    //po posledni simulaci nevysel pres prah ani betl ani durch, zvolime tedy to, co vyslo lepe
                    if (_durchBalance > 0 &&
                        _durchSimulations > 0 && 
                        (_betlSimulations == 0 ||
                         (float)_durchBalance / (float)_durchSimulations > (float)_betlBalance  / (float)_betlSimulations))
                    {
                        gameType = Hra.Durch;
                        DebugInfo.RuleCount = _durchBalance;
                        DebugInfo.TotalRuleCount = _durchSimulations;
                    }
                    else
                    {
                        gameType = Hra.Betl;
                        DebugInfo.RuleCount = _betlBalance;
                        DebugInfo.TotalRuleCount = _betlSimulations;
                    }
				}
            }
            else
            {
                var kqScore = _g.trump.HasValue
                                ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                      .Where(b => Hand.HasK(b) && Hand.HasQ(b))
                                      .Sum(b => b == _g.trump.Value ? 40 : 20)
                                : 0;
                var estimatedFinalBasicScore = _g.trump.HasValue ? EstimateFinalBasicScore() : 0;
                var estimatefOpponentFinalBasicScore = 90 - estimatedFinalBasicScore;
                var totalHoles = GetTotalHoles();
                var minBasicPointsLost = EstimateMinBasicPointsLost();

                var gameMultiplier = _trump.HasValue
                                     ? Hand.HasK(_trump.Value) &&
                                       Hand.HasQ(_trump.Value) &&
                                       (Hand.HasA(_trump.Value) ||
                                        Hand.HasX(_trump.Value) ||
                                        Hand.CardCount(_trump.Value) >= 5)
                                       ? 1 : 2
                                     : 0;
                var gameValue = gameMultiplier * _g.QuietHundredValue;
                var lossPerPointsLost = _trump.HasValue
                                        ? Enumerable.Range(0, 10)
                                                    .Select(i => 100 + i * 10)
                                                    .ToDictionary(k => k,
                                                                  v => ((_g.CalculationStyle == CalculationStyle.Adding
                                                                         ? (v - 90) / 10 * gameValue 
                                                                         : (1 << (v - 100) / 10) * gameValue) -
                                                                        (Hand.CardCount(_trump.Value) >= 5 &&
                                                                         Hand.Has7(_trump.Value)
                                                                         ? _g.SevenValue : 0)) *
                                                                        (_trump.Value == Barva.Cerveny
                                                                         ? 4
                                                                         : 2)) //ztrata pro aktera je dvojnasobna (plati obema souperum)
                                        : new Dictionary<int, int>();

                var estimatedPointsLost = _trump.HasValue ? EstimateMaxTotalPointsLost(Hand, _talon) : 0;

                if (Settings.CanPlayGameType[Hra.Kilo] && 
                    _hundredsBalance >= Settings.GameThresholdsForGameType[Hra.Kilo][0] * _hundredSimulations &&
                    _hundredSimulations > 0 &&
                    !IsHundredTooRisky())
                {
                    gameType = Hra.Kilo;
                    DebugInfo.RuleCount = _hundredsBalance;
                    DebugInfo.TotalRuleCount = _hundredSimulations;
                }
                else if (Settings.CanPlayGameType[Hra.Hra] &&
                         (((!Settings.AiMayGiveUp &&
                            !AdvisorMode) ||
                           (!Settings.PlayerMayGiveUp &&
                            AdvisorMode)) ||
                          Settings.MinimalBidsForGame > 1 ||
                          ((_gamesBalance > 0 &&
                            _gamesBalance >= Settings.GameThresholdsForGameType[Hra.Hra][0] * _gameSimulations && _gameSimulations > 0 &&
                            (Settings.SafetyBetlThreshold == 0 ||
                             _maxMoneyLost > -Settings.SafetyBetlThreshold) &&
                            (Settings.SafetyBetlThreshold == 0 ||
                             !lossPerPointsLost.ContainsKey(estimatedPointsLost) ||
                             lossPerPointsLost[estimatedPointsLost] < Settings.SafetyBetlThreshold)))))
                {
                    gameType = Hra.Hra;
                    DebugInfo.RuleCount = _gamesBalance;
                    DebugInfo.TotalRuleCount = _gameSimulations;
                }
                else
                {
                    gameType = 0; //vzdat se
                    DebugInfo.RuleCount = _gamesBalance;
                    DebugInfo.TotalRuleCount = _gameSimulations;
                }
                var avgPointsWon = 90 - _avgBasicPointsLost + kqScore;
                DebugInfo.AvgSimulatedPointsWon = (int)avgPointsWon;
                if (Settings.CanPlayGameType[Hra.Sedma] &&
                    (((_g.AllowFakeSeven &&
                       (gameType & Hra.Hra) != 0 && //pri hre
                       avgPointsWon >= 115) ||      //110 bodu = plichta, 120+ = vyhra
                      (_g.AllowFake107 &&
                       _g.Calculate107Separately &&
                       _g.Top107 &&
                       (gameType & Hra.Kilo) != 0 && //pri kilu
                       _avgWinForHundred > 2 * (_g.DurchValue + 2 * _g.SevenValue))) ||
                     (Hand.Has7(_trump.Value) &&
                      _sevensBalance >= Settings.GameThresholdsForGameType[Hra.Sedma][0] * _sevenSimulations && _sevenSimulations > 0 &&
                      !IsSevenTooRisky()
                      //(!IsSevenTooRisky() ||                  //sedmu hlas pokud neni riskantni nebo pokud nelze uhrat hru (doufej ve flek na hru a konec)
                      // (!_g.PlayZeroSumGames &&
                      //  Hand.CardCount(_trump.Value) >= 4 &&
                      //  Hand.Any(i => i.Suit == _trump.Value &&
                      //                i.Value >= Hodnota.Svrsek) &&
                      //  _gamesBalance < Settings.GameThresholdsForGameType[Hra.Hra][0] * _gameSimulations && 
                      //  _gameSimulations > 0))
                      )))
                {
                    if (gameType == 0)
                    {
                        gameType = Hra.Hra;
                    }
                    if (gameType == Hra.Hra)    //u sedmy budu zobrazovat sedmu a u 107 kilo
                    {
                        DebugInfo.RuleCount = _sevensBalance;
                        DebugInfo.TotalRuleCount = _sevenSimulations;
                    }
                    gameType |= Hra.Sedma;
                }
            }
            DebugInfo.Rule = (gameType & (Hra.Betl | Hra.Durch)) == 0 ? string.Format("{0} {1}", gameType, _trump) : gameType.ToString();
            var allChoices = new List<RuleDebugInfo> ();
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Hra.ToDescription(_trump),
                RuleCount = _gamesBalance,
                TotalRuleCount = _gameSimulations
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = (Hra.Hra | Hra.Sedma).ToDescription(_trump),
                RuleCount = _sevensBalance,
                TotalRuleCount = _sevenSimulations
            });
            allChoices.Add(new RuleDebugInfo
            {
                Rule = Hra.Kilo.ToDescription(_trump),
                RuleCount = _hundredsBalance,
                TotalRuleCount = _hundredSimulations
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
//#if DEBUG
//            allChoices.Add(new RuleDebugInfo
//            {
//                Rule = "Skóre2",
//                RuleCount = DebugInfo.EstimatedFinalBasicScore2,
//                TotalRuleCount = 100
//            });
//            allChoices.Add(new RuleDebugInfo
//            {
//                Rule = "Tygrovo",
//                RuleCount = DebugInfo.Tygrovo,
//                TotalRuleCount = 100
//            });
//            allChoices.Add(new RuleDebugInfo
//            {
//                Rule = "Silná",
//                RuleCount = DebugInfo.Strong * 100,
//                TotalRuleCount = 100
//            });
//#endif
            DebugInfo.AllChoices = allChoices.OrderByDescending(i => i.RuleCount).ToArray();
            _log.DebugFormat("Selected game type: {0}", gameType);

            return gameType;
        }

        private int EstimateLowBetlCardCount()
        {
            var betlLowCards = 0;
            var sevenCardsAdded = false;

            if (PlayerIndex != _g.GameStartingPlayerIndex)
            {
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                {
                    if (Hand.CardCount(b) == 1 &&
                        !Hand.Has7(b) &&
                        !sevenCardsAdded)
                    {
                        betlLowCards += 7;
                        sevenCardsAdded = true;
                    }
                    else
                    {
                        if (Hand.HasSuit(b))
                        {
                            var loCards = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                              .Select(h => new Card(b, h))
                                              .Count(i => Probabilities.CardProbability(_g.GameStartingPlayerIndex, i) > 0 &&
                                                          Hand.Where(j => j.Suit == b)
                                                              .All(j => j.BadValue > i.BadValue));
                            betlLowCards += loCards;
                        }
                        else
                        {
                            betlLowCards += Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                              .Select(h => new Card(b, h))
                                              .Count(i => Probabilities.CardProbability(_g.GameStartingPlayerIndex, i) > 0);
                        }
                    }
                }
            }

            return betlLowCards;
        }

        //V tehle funkci muzeme dat flek nebo hlasit protihru
        public override Hra GetBidsAndDoubles(Bidding bidding)
        {
            Hra bid = 0;
            const int MaxFlek = 3;
            var gameThreshold = bidding._gameFlek < Settings.GameThresholdsForGameType[Hra.Hra].Length ? Settings.GameThresholdsForGameType[Hra.Hra][bidding._gameFlek] : 1f;
            var gameThresholdPrevious = bidding._gameFlek > 1 && bidding._gameFlek - 1 < Settings.GameThresholdsForGameType[Hra.Hra].Length ? Settings.GameThresholdsForGameType[Hra.Hra][bidding._gameFlek - 1] : 1f;
            var gameThresholdNext = bidding._gameFlek < Settings.GameThresholdsForGameType[Hra.Hra].Length - 1 ? Settings.GameThresholdsForGameType[Hra.Hra][bidding._gameFlek + 1] : 1f;
            var certaintyThreshold = Settings.GameThresholdsForGameType[Hra.KiloProti].Length > 0 ? Settings.GameThresholdsForGameType[Hra.KiloProti][0] : 1f; //kilo proti je obtizna hra, proto jeho prah beru jako "jistotu"
            var sevenThreshold = bidding._sevenFlek < Settings.GameThresholdsForGameType[Hra.Sedma].Length ? Settings.GameThresholdsForGameType[Hra.Sedma][bidding._sevenFlek] : 1f;
            var hundredThreshold = bidding._gameFlek < Settings.GameThresholdsForGameType[Hra.Kilo].Length ? Settings.GameThresholdsForGameType[Hra.Kilo][bidding._gameFlek] : 1f;
            var sevenAgainstThreshold = bidding._sevenAgainstFlek < Settings.GameThresholdsForGameType[Hra.SedmaProti].Length ? Settings.GameThresholdsForGameType[Hra.SedmaProti][bidding._sevenAgainstFlek] : 1f;
            var hundredAgainstThreshold = bidding._hundredAgainstFlek < Settings.GameThresholdsForGameType[Hra.KiloProti].Length ? Settings.GameThresholdsForGameType[Hra.KiloProti][bidding._hundredAgainstFlek] : 1f;
            var betlThreshold = bidding._betlDurchFlek < Settings.GameThresholdsForGameType[Hra.Betl].Length ? Settings.GameThresholdsForGameType[Hra.Betl][bidding._betlDurchFlek] : 1f;
            var durchThreshold = bidding._betlDurchFlek < Settings.GameThresholdsForGameType[Hra.Durch].Length ? Settings.GameThresholdsForGameType[Hra.Durch][bidding._betlDurchFlek] : 1f;

            if (AdvisorMode && TeamMateIndex == -1)
            {
                _runSimulations = true;
            }
            if (_runSimulations)
            {
                //mame flekovat hru
                //kilo simulovat nema cenu, hrac ho asi ma, takze flekovat stejne nebudeme
                if (bidding.BetlDurchMultiplier == 0 && ((bidding.Bids & (Hra.Hra | Hra.Sedma | Hra.SedmaProti)) != 0))
                {
                    if (_initialSimulation)
                    {
                        RunGameSimulations(bidding, _g.GameStartingPlayerIndex, true, false);
                        _initialSimulation = false;
                        //po opakovane simulaci sedmy pri novem rozlozeni trumfu po fleku na sedmu aktualizuj statistiku
                        if (TeamMateIndex == -1 &&
                            (bidding.Bids & Hra.Sedma) != 0)
                        {
                            DebugInfo.RuleCount = _sevensBalance;
                            DebugInfo.TotalRuleCount = _sevenSimulations;
                        }
                    }
                }
                else
                {
                    //mame flekovat betl nebo durch
                    //nema smysl poustet simulace, nic o souperovych kartach nevime
                    //RunGameSimulations(bidding, _g.GameStartingPlayerIndex, false, true);
                }
            }
            else if ((bidding.Bids & (Hra.SedmaProti | Hra.KiloProti)) != 0 && 
                     bidding.SevenAgainstMultiplier <= 1 && 
                     bidding.HundredAgainstMultiplier <= 1)
            {
                RunGameSimulations(bidding, _g.GameStartingPlayerIndex, true, false);
            }
            DebugInfo.RuleCount = -1;
            var axCount = Hand.CardCount(Hodnota.Eso) + Hand.CardCount(Hodnota.Desitka);
            var kqScore = _g.trump.HasValue
                            ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => Hand.HasK(b) && Hand.HasQ(b))
                                  .Sum(b => b == _g.trump.Value ? 40 : 20)
                            : 0;
            var kqMaxOpponentScore = _g.trump.HasValue
                                        ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                              .Where(b => !Hand.HasK(b) &&
                                                          !Hand.HasQ(b) &&
                                                          (_talon == null ||
                                                           (!_talon.HasK(b) &&
                                                            !_talon.HasQ(b))))
                                              .Sum(b => b == _g.trump.Value ? 40 : 20)
                                        : 0;
            var kqLikelyOpponentScore = _g.trump.HasValue
                                        ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                              .Count(b => !Hand.HasK(b) &&
                                                          !Hand.HasQ(b) &&
                                                          (_talon == null ||
                                                          (!_talon.HasK(b) &&
                                                          !_talon.HasQ(b)))) <= 1
                                            ? kqMaxOpponentScore
                                            : kqMaxOpponentScore - 20
                                        : 0;
            var estimatedFinalBasicScore = _g.trump.HasValue ? EstimateFinalBasicScore() : 0;
            var estimatedOpponentFinalBasicScore = 90 - estimatedFinalBasicScore;
            var bestCaseNonTrumpScore = _g.trump.HasValue
                                        ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                              .Where(b => b != _g.trump.Value)
                                              .Sum(b => (Hand.HasA(b) ? 10 : 0) +
                                                        ((Hand.HasX(b) && Hand.CardCount(b) >= 2) ? 10 : 0))
                                        : 0;
            var totalHoles = GetTotalHoles();
            var handSuits = Hand.SuitCount();
            var estimatedPointsLost = _trump.HasValue ? EstimateMaxTotalPointsLost(Hand, _talon) : 0;
            var gameValue = 2 * bidding.GameMultiplier * (estimatedPointsLost >= 100 ? _g.QuietHundredValue : _g.GameValue);
            var lossPerPointsLost = _trump.HasValue
                                    ? Enumerable.Range(0, 10)
                                                .Select(i => 100 + i * 10)
                                                .ToDictionary(k => k,
                                                              v => ((_g.CalculationStyle == CalculationStyle.Adding
                                                                     ? (v - 90) / 10 * gameValue
                                                                     : (1 << (v - 100) / 10) * gameValue) -
                                                                    (Hand.CardCount(_trump.Value) >= 5 &&
                                                                     Hand.Has7(_trump.Value)
                                                                     ? _g.SevenValue : 0)) *
                                                                    (_trump.Value == Barva.Cerveny
                                                                     ? 4
                                                                     : 2)) //ztrata pro aktera je dvojnasobna (plati obema souperum)
                                    : new Dictionary<int, int>();

            if ((bidding.Bids & Hra.Hra) != 0 &&                //pokud byla zvolena hra (nebo hra a sedma]
                Settings.CanPlayGameType[Hra.Hra] &&
                bidding._gameFlek <= Settings.MaxDoubleCountForGameType[Hra.Hra] &&
                (bidding._gameFlek < 3 ||                      //ai nedava tutti pokud neflekoval i clovek
                 PlayerIndex == 0 ||
                 _teamMateDoubledGame) &&
                _gameSimulations > 0 &&                         //pokud v simulacich vysla dost casto
                _gamesBalance / (float)_gameSimulations >= gameThreshold &&
                ((TeamMateIndex != -1 &&                        //Tutti: 50+ bodu na ruce                  
                  bidding.GameMultiplier > 2 &&
                  estimatedFinalBasicScore + kqScore >= 50 &&
                  (axCount >= 5 ||
                   kqScore >= 60 ||
                   (kqScore >= 40 &&
                    axCount >= 1) ||
                   (kqScore >= 20 &&
                    axCount >= 3)) &&
                  (_teamMateDoubledGame ||
                   estimatedFinalBasicScore + kqScore > estimatedOpponentFinalBasicScore + kqLikelyOpponentScore)) ||
                  (TeamMateIndex == -1 &&                       //Re: pokud mam vic nez souperi s Max(0, n-1) z moznych hlasek a
                   bidding.GameMultiplier == 2 &&
                   //estimatedFinalBasicScore + kqScore > estimatedOpponentFinalBasicScore + kqLikelyOpponentScore &&
                   estimatedFinalBasicScore + kqScore > estimatedOpponentFinalBasicScore + Math.Max(0, kqMaxOpponentScore - 20) &&
                   estimatedOpponentFinalBasicScore + kqLikelyOpponentScore < 100 &&
                   2 * _maxMoneyLost >= -Settings.SafetyGameThreshold &&    // simulace byly jen na flek, pro re vynasobim ztratu dvema
                   (Settings.SafetyGameThreshold == 0 ||
                    !lossPerPointsLost.ContainsKey(estimatedPointsLost) ||
                    lossPerPointsLost[estimatedPointsLost] <= Settings.SafetyGameThreshold)) ||   //souper nemuze uhrat kilo proti s Min(1, n-1) z moznych hlasek
                  (TeamMateIndex != -1 &&                       //Flek:
                   bidding.GameMultiplier == 1 &&
                   (((Hand.HasK(_trumpCard.Suit) ||             //pokud trham a
                      Hand.HasQ(_trumpCard.Suit)) &&
                     (kqScore >= 40 ||                          //mam aspon 40 bodu v hlaskach nebo
                      ((bidding.Bids & Hra.Sedma) == 0 &&
                       kqScore >= 20 &&                         //aspon 20 bodu v hlaskach a aspon 10 bodu v ostrych nebo
                       axCount >= 1 &&
                       estimatedFinalBasicScore >= 10) ||
                      ((bidding.Bids & Hra.Sedma) == 0 &&
                       axCount >= 2 &&                          //aspon 2 ostre karty
                       estimatedFinalBasicScore >= 10) ||
                      ((bidding.Bids & Hra.Sedma) != 0 &&       //pri sedme
                       axCount >= 3 &&                          //aspon 3 ostre karty
                       estimatedFinalBasicScore >= 20) ||
                      ((bidding.Bids & Hra.Sedma) != 0 &&       //pri sedme
                       kqScore >= 20 &&                         //aspon 20 bodu v hlaskach a
                       axCount >= 2 &&                          //aspon 2 ostre karty
                       estimatedFinalBasicScore >= 20) ||
                      (Hand.CardCount(_trumpCard.Suit) >= 4 &&
                       axCount >= 1 &&
                       estimatedFinalBasicScore >= 10) ||
                      (Hand.CardCount(_trumpCard.Suit) >= 3 &&
                       (bidding.Bids & Hra.Sedma) == 0 &&
                       axCount >= 1 &&
                       estimatedFinalBasicScore >= 10 &&
                       kqMaxOpponentScore <= 20))) ||
                    (!Hand.HasK(_trumpCard.Suit) &&             //netrham a
                     !Hand.HasQ(_trumpCard.Suit) &&
                     Hand.HasA(_trumpCard.Suit) &&              //mam trumfove AX a
                     (Hand.HasX(_trumpCard.Suit) ||
                      Hand.CardCount(_trumpCard.Suit) >= 4) &&
                     (Hand.CardCount(Hodnota.Eso) >= 2 ||       //aspon jeste jedno eso nebo
                      (axCount >= 4 &&                          //aspon jeste dve desitky a
                       estimatedFinalBasicScore >= 40)) &&      //akter nemuze uhrat kilo
                     !Is100AgainstPossible()) ||
                    (Hand.HasA(_trumpCard.Suit) &&              //mam trumfove AX a
                     Hand.HasX(_trumpCard.Suit) &&
                     Hand.CardCount(_trumpCard.Suit) >= 3 &&    //a aspon 3 trumfy
                     (kqScore >= 20 &&                          //mam aspon 20 bodu v hlasech
                      axCount >= 4 &&                            //a aspon jeste dve dalsi desitky
                      estimatedFinalBasicScore >= 40)) ||
                    (Hand.HasA(_trumpCard.Suit) &&              //mam trumfove AX a
                     Hand.HasX(_trumpCard.Suit) &&
                     ((kqScore >= 40 &&                          //mam aspon 40 bodu v hlasech
                       axCount >= 3 &&                            //a aspon jeste jednu dalsi desitku
                       estimatedFinalBasicScore >= 30) ||
                      (kqScore >= 20 &&                          //mam aspon 20 bodu v hlasech
                       axCount >= 5 &&                            //a aspon jeste dve dalsi desitky
                       estimatedFinalBasicScore >= 50))) ||
                    (Hand.HasA(_trumpCard.Suit) &&              //mam trumfove AX a
                     Hand.HasX(_trumpCard.Suit) &&
                     Hand.CardCount(_trumpCard.Suit) >= 4 &&    //a aspon 4 trumfy
                     (kqScore >= 20 ||                          //mam aspon 20 bodu v hlasech
                      (axCount >= 4 &&                          //a aspon 4 desitky celkem
                       estimatedFinalBasicScore >= 50))) ||
                    (Hand.HasA(_trumpCard.Suit) &&              //mam aspon 40 bodu v hlasech a trumfove eso
                     kqScore >= 40 &&
                     estimatedFinalBasicScore >= 40 &&          //a aspon tri dalsi desitky
                     axCount >= 4)))))
            {
                bid |= bidding.Bids & Hra.Hra;
                //minRuleCount = Math.Min(minRuleCount, _gamesBalance);
                DebugInfo.RuleCount = _gamesBalance;
                DebugInfo.TotalRuleCount = _gameSimulations;
            }
            if ((bidding.Bids & Hra.Hra) != 0 &&
                TeamMateIndex == -1 &&
                ((_gameType & Hra.Sedma) != 0 &&
                  !Hand.Has7(_g.trump.Value)) &&
                bidding.GameMultiplier < 2)
            {
                bid |= bidding.Bids & Hra.Hra;
                DebugInfo.RuleCount = _gamesBalance;
                DebugInfo.TotalRuleCount = _gameSimulations;
            }
            if ((bidding.Bids & Hra.Hra) != 0 &&
                _g.MandatoryDouble &&
                TeamMateIndex != -1 &&
                bidding.GameMultiplier < 2 &&
                (Hand.HasK(_g.trump.Value) ||
                 Hand.HasQ(_g.trump.Value)))
            {
                bid |= bidding.Bids & Hra.Hra;
                DebugInfo.RuleCount = _gamesBalance;
                DebugInfo.TotalRuleCount = _gameSimulations;
            }
            //sedmu flekuju pokud mam aspon 3 trumfy a vsechny barvy
            if ((bidding.Bids & Hra.Sedma) != 0 &&
                Settings.CanPlayGameType[Hra.Sedma] &&
                (bidding._sevenFlek < 3 ||                      //ai nedava tutti pokud neflekoval i clovek
                 PlayerIndex == 0 ||
                 (bidding.PlayerBids[0] & Hra.Sedma) != 0) &&
                _sevenSimulations > 0 && 
                bidding._sevenFlek <= Settings.MaxDoubleCountForGameType[Hra.Sedma] &&
                ((TeamMateIndex != -1 &&
                  (_gameType & Hra.Kilo) == 0 &&
                  (((Hand.CardCount(_g.trump.Value) >= 4 &&           //ctyri a vice trumfu nebo
                     (Hand.HasA(_g.trump.Value) ||
                      Hand.CardCount(Hodnota.Eso) >= 2 ||
                      (Hand.HasX(_g.trump.Value) &&
                       Hand.HasK(_g.trump.Value) &&
                       Hand.HasQ(_g.trump.Value) &&
                       Hand.SuitCount() == Game.NumSuits) ||
                      (Hand.HasX(_g.trump.Value) &&
                       Enum.GetValues(typeof(Barva)).Cast<Barva>()
                           .All(b => Hand.CardCount(b) >= 2 &&
                                     Hand.Any(i => i.Suit == b &&
                                                   i.Value >= Hodnota.Kral))))) ||
                    (Hand.CardCount(_g.trump.Value) >= 3 &&         //tri trumfy vcetne esa a jeste jednoho velkeho trumfu
                     Hand.HasA(_g.trump.Value) &&
                     (Hand.HasX(_g.trump.Value) ||
                      Hand.HasK(_g.trump.Value) ||
                      Hand.HasQ(_g.trump.Value)) &&
                     Hand.CardCount(Hodnota.Eso) >= 2 &&            //navic jeste aspon jedno dalsi eso
                     Enum.GetValues(typeof(Barva)).Cast<Barva>()    //a k tomu nejaka dlouha barva
                         .Any(b => b != _trump &&
                                   Hand.CardCount(b) >= 5) &&
                     Hand.SuitCount() == Game.NumSuits) ||
                    (Hand.CardCount(_g.trump.Value) >= 3 &&         //tri trumfy vcetne esa a jeste jednoho velkeho trumfu
                     Hand.HasA(_g.trump.Value) &&
                     (Hand.HasX(_g.trump.Value) ||
                      Hand.HasK(_g.trump.Value) ||
                      Hand.HasQ(_g.trump.Value)) &&
                     Hand.CardCount(Hodnota.Eso) >= 3 &&            //navic jeste dalsi dve esa
                     Enum.GetValues(typeof(Barva)).Cast<Barva>()    //z nichz jedno eso je od delsi barvy
                         .Any(b => b != _trump &&
                                   Hand.HasA(b) &&
                                   Hand.CardCount(b) >= 4) &&
                     Hand.SuitCount() == Game.NumSuits) ||
                    (Hand.CardCount(_g.trump.Value) >= 3 &&
                     Hand.HasA(_g.trump.Value) &&
                     Hand.SuitCount() == Game.NumSuits &&
                     Hand.Count(i => i.Value >= Hodnota.Kral) >= 5 &&
                     Enum.GetValues(typeof(Barva)).Cast<Barva>()
                         .Count(b => Hand.Any(i => i.Suit == b &&
                                                 i.Value >= Hodnota.Kral) &&
                                   !Hand.HasSolitaryX(b)) >= 3) ||
                    (Hand.CardCount(_g.trump.Value) >= 3 &&          //tri trumfy 3-3-2-2                     
                     ((kqScore >= 60 &&
                       ((Hand.HasA(_g.trump.Value) &&
                         estimatedFinalBasicScore >= 40 &&
                         axCount >= 2) ||
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Count(b => Hand.HasA(b) ||
                                        (Hand.HasX(b) &&
                                         Hand.HasK(b)) ||
                                        (Hand.HasK(b) &&
                                         Hand.HasQ(b) &&
                                         Hand.HasJ(b))) >= 3)) //||
                      //(Hand.Count(i => i.Suit == _g.trump.Value &&
                      //                 i.Value >= Hodnota.Svrsek) >= 3 &&
                      // handSuits == Game.NumSuits &&
                      // Enum.GetValues(typeof(Barva)).Cast<Barva>()
                      //     .All(b => Hand.Any(i => i.Suit == b &&
                      //                             i.Value >= Hodnota.Kral))) ||
                      //((Hand.CardCount(Hodnota.Eso) >= 2 ||
                      //  (Hand.HasA(_g.trump.Value) &&
                      //   kqScore >= 40)) &&
                      // ((axCount >= 4 &&
                      //   Enum.GetValues(typeof(Barva)).Cast<Barva>()
                      //       .All(b => Hand.CardCount(b) >= 2)) ||
                      //  (axCount >= 3 &&
                      //   Hand.HasA(_g.trump.Value) &&
                      //   Hand.HasK(_g.trump.Value) &&
                      //   (Hand.HasX(_g.trump.Value) ||
                      //    Hand.HasQ(_g.trump.Value)) &&
                      //   (kqScore >= 40 ||
                      //    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                      //        .All(b => Hand.CardCount(b) >= 2))) ||
                      //  (axCount >= 4 &&                               //hodne ostrych karet a vsechny barvy
                      //   estimatedFinalBasicScore >= 40 &&
                      //   handSuits == Game.NumSuits) //||
                      //  //(Hand.Any(i => i.Suit == _trump.Value &&
                      //  //               i.Value >= Hodnota.Svrsek) &&
                      //  // Hand.Count(i => i.Value >= Hodnota.Svrsek) >= 6 &&
                      //  // Enum.GetValues(typeof(Barva)).Cast<Barva>()
                      //  //     .Where(b => Hand.HasSuit(b))
                      //  //     .All(b => Hand.CardCount(b) >= 2)) ||
                      //  //(Hand.HasA(_trump.Value) &&                    //trumfove eso a desitka
                      //  // Hand.HasX(_trump.Value) &&
                      //  // Hand.CardCount(Hodnota.Eso) >= 2 &&           //aspon dve esa a
                      //  // Enum.GetValues(typeof(Barva)).Cast<Barva>()   //jedna dlouha barva
                      //  //     .Where(b => b != _trump.Value)
                      //  //     .Any(b => Hand.CardCount(b) >= 4))
                      //      ))
                            ))) ||
                   (_teamMateDoubledGame &&                          //nebo pokud kolega flekoval
                    !_g.MandatoryDouble &&
                    Hand.HasA(_trump.Value) &&                       //a ja mam aspon 3 trumfy a navic eso a neco velkeho a aspon 40 bodu
                    (Hand.HasX(_trump.Value) ||
                     Hand.HasK(_trump.Value) ||
                     Hand.HasQ(_trump.Value)) &&
                     ((Hand.CardCount(_trump.Value) >= 3 &&
                       estimatedFinalBasicScore >= 40) ||
                      (estimatedFinalBasicScore >= 50 &&
                       kqScore >= 20)) ||
                   (_teamMateDoubledGame &&
                    !_g.MandatoryDouble &&
                    Hand.CardCount(_trump.Value) >= 3 &&
                    estimatedFinalBasicScore >= 40 &&
                    kqScore >= 40)))) ||
                 (TeamMateIndex == -1 &&
                 (Hand.CardCount(_g.trump.Value) >= 6 ||
                  (Hand.CardCount(_g.trump.Value) >= 5 &&       //5-4-1
                   Hand.SuitCount() >= 3 &&
                   Enum.GetValues(typeof(Barva)).Cast<Barva>()
                       .Where(b => b != _g.trump.Value)
                       .Any(b => Hand.CardCount(b) >= 4)) ||
                  (Hand.CardCount(_g.trump.Value) >= 4 &&       //4-3-2-1 4-2-2-2 5-3-2 5-2-2-1 a dobiraky
                   Hand.SuitCount() == 4 &&
                   Enum.GetValues(typeof(Barva)).Cast<Barva>()
                       .Where(b => b != _g.trump.Value)
                       .All(b => Hand.HasA(b) ||
                                 (Hand.HasX(b) &&
                                  Hand.CardCount(b) > 1) ||
                                 Hand.HasK(b))) ||
                  totalHoles <= 2 ||
                  (Hand.SuitCount() == 2 &&
                   Hand.CardCount(_g.trump.Value) == 5 &&
                   Hand.CardCount(Hodnota.Eso) == 2 &&
                   Hand.Count(i => i.Value >= Hodnota.Kral) >= 4 &&
                   Hand.Count(i => i.Value >= Hodnota.Spodek) >= 6)))) &&
                (_sevensBalance / (float)_sevenSimulations >= sevenThreshold))
            {
                bid |= bidding.Bids & Hra.Sedma;
                //minRuleCount = Math.Min(minRuleCount, _sevensBalance);
                DebugInfo.RuleCount = _sevensBalance;
                DebugInfo.TotalRuleCount = _sevenSimulations;
            }
            //else if ((bidding.Bids & Hra.Sedma) != 0 &&
            //         (bidding.Bids & Hra.Kilo) == 0 &&
            //         ((bid & Hra.Hra) != 0 ||
            //          (_teamMateDoubledGame &&
            //           !_g.MandatoryDouble)) &&
            //         Settings.CanPlayGameType[Hra.Sedma] &&
            //         TeamMateIndex != -1 &&
            //         bidding._sevenFlek == 1 &&                         //flekni krome hry i sedmu i kdyz v simulacich nevysla
            //         ((kqScore >= 40 &&                                 //pokud je sance uhrat tichych 110 proti
            //           estimatedFinalBasicScore + kqScore >= 90)))      //90 bodu uhraju sam a zbytek snad bude mit kolega
            //{
            //    bid |= bidding.Bids & Hra.Sedma;
            //    //minRuleCount = Math.Min(minRuleCount, _sevensBalance);
            //    DebugInfo.RuleCount = _sevensBalance;
            //    DebugInfo.TotalRuleCount = _sevenSimulations;
            //}

            if ((bidding.Bids & Hra.Sedma) != 0 &&
                Settings.CanPlayGameType[Hra.Sedma] &&
                bidding._sevenFlek < 3 &&
                TeamMateIndex != -1 &&
                Hand.Has7(_trump.Value) &&
                (_g.MinimalBidsForSeven == 0 || //pokud by akter mohl uhrat 130 a zaroven se sedma nehraje bez fleku, je lepsi neflekovat a snizit tak ztratu
                 (_teamMateDoubledGame &&
                  !_g.MandatoryDouble) ||
                 (bidding.Bids & Hra.Kilo) != 0 ||
                 !Is100AgainstPossible(130)))
            {
                bid |= bidding.Bids & Hra.Sedma;
                DebugInfo.RuleCount = _sevensBalance;
                DebugInfo.TotalRuleCount = _sevenSimulations;
            }

            //kilo flekuju jen pokud jsem volil sam kilo a v simulacich jsem ho uhral dost casto
            //nebo pokud jsem nevolil a je nemozne aby mel volici hrac kilo (nema hlas)
            //?! Pokud bych chtel simulovat sance na to, ze volici hrac hlasene kilo neuhraje, tak musim nejak generovat "karty na kilo" (aspon 1 hlas) a ne nahodne karty
            if ((bidding.Bids & Hra.Kilo) != 0 &&
                Settings.CanPlayGameType[Hra.Kilo] &&
                _hundredSimulations > 0 &&
                (bidding._gameFlek <= Settings.MaxDoubleCountForGameType[Hra.Kilo] ||
				 (bidding._gameFlek <= MaxFlek &&
                  _hundredsBalance / (float)_hundredSimulations >= certaintyThreshold)) &&
                ((PlayerIndex == _g.GameStartingPlayerIndex &&
                  _hundredsBalance / (float)_hundredSimulations >= hundredThreshold &&
                  Enum.GetValues(typeof(Barva)).Cast<Barva>().Where(b => Hand.HasSuit(b)).All(b => Hand.HasA(b))) ||    //Re na kilo si dej jen pokud mas ve vsech barvach eso
			     (PlayerIndex != _g.GameStartingPlayerIndex &&                                                          //Flek na kilo si dej jen pokud akter nema hlas
                  (Probabilities.HlasProbability(_g.GameStartingPlayerIndex) == 0 ||
                   (_hundredsBalance == _hundredSimulations &&                                                          //nebo pokud v simulacich nevyslo ani jednou
                    Hand.HasK(_g.trump.Value) &&
                    Hand.HasQ(_g.trump.Value) &&
                    estimatedFinalBasicScore >= 40)))))
            {
                bid |= bidding.Bids & Hra.Kilo;
                //minRuleCount = Math.Min(minRuleCount, _hundredsBalance);
                DebugInfo.RuleCount = _hundredsBalance;
                DebugInfo.TotalRuleCount = _hundredSimulations;
            }
            //sedmu proti flekuju pokud mam aspon 4 trumfy a vsechny barvy
            if ((bidding.Bids & Hra.SedmaProti) != 0 &&
                Settings.CanPlayGameType[Hra.SedmaProti] &&
                bidding._sevenAgainstFlek <= Settings.MaxDoubleCountForGameType[Hra.SedmaProti] &&
                (bidding._sevenAgainstFlek < 3 ||                      //ai nedava tutti pokud neflekoval i clovek
                 PlayerIndex == 0 ||
                 (bidding.PlayerBids[0] & Hra.SedmaProti) != 0) &&
                ((bidding._sevenAgainstFlek == 0 &&                                  //nez poprve zahlasime sedmu proti zjistime jestli to neni riskantni
                  ((Hand.Has7(_g.trump.Value) &&
                    !IsSevenTooRisky()) ||
                   (_g.AllowFakeSeven &&                                            //falesnou sedmu proti hlas jen pokud se bez re nehraje
                    (_g.MinimalBidsForGame > 1 ||
                     ((_g.GameStartingPlayerIndex == 0 &&                           //nebo kdyz muze akter hru zahodit
                       Settings.PlayerMayGiveUp) ||
                      (_g.GameStartingPlayerIndex != 0 &&
                       Settings.AiMayGiveUp)))))) ||
                 (bidding._sevenAgainstFlek > 0 &&                                  //flek na sedmu proti dam jen kdyz mam sam dost trumfu
                  (Hand.CardCount(_g.trump.Value) >= 4 ||
                   (Hand.CardCount(_g.trump.Value) >= 3 &&
                    (Hand.HasA(_g.trump.Value) ||
                     Hand.HasX(_g.trump.Value)) &&
                    Hand.Where(i => i.Suit != _g.trump.Value)
                        .CardCount(Hodnota.Eso) >= 2 &&
                    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        .All(b => Hand.CardCount(b) >= 2 &&
                                  (Hand.HasA(b) ||
                                   Hand.HasX(b) ||
                                   Hand.HasK(b))) &&
                    Hand.SuitCount() == Game.NumSuits)))) &&
                ((_gameSimulations > 0 && _sevensAgainstBalance / (float)_gameSimulations >= sevenAgainstThreshold) ||
                 (_avgBasicPointsLost + kqScore >= 110 &&
                  kqScore >= 40 &&
                  (Hand.Has7(_g.trump.Value) ||
                   (bidding._sevenAgainstFlek < 2 &&
                    _g.AllowFakeSeven)))))
            {
                bid |= bidding.Bids & Hra.SedmaProti;
                DebugInfo.RuleCount = _sevensAgainstBalance;
                if (TeamMateIndex == -1 &&
                    (Hand.Has7(_g.trump.Value) ||
                     _talon.Has7(_g.trump.Value)))
                {
                    DebugInfo.TotalRuleCount = _sevensAgainstBalance; //falesna 7 proti
                }
                else
                {
                    DebugInfo.TotalRuleCount = _gameSimulations;
                }
            }

            if ((bidding.Bids & Hra.SedmaProti) != 0 &&
                Settings.CanPlayGameType[Hra.SedmaProti] &&
                bidding._sevenAgainstFlek < 3 &&
                TeamMateIndex == -1 &&
                Hand.Has7(_trump.Value))
            {
                bid |= bidding.Bids & Hra.SedmaProti;
                DebugInfo.RuleCount = _sevensBalance;
                DebugInfo.TotalRuleCount = _sevenSimulations;
            }

            //kilo proti flekuju jen pokud jsem hlasil sam kilo proti a v simulacich jsem ho uhral dost casto
            //nebo pokud jsem volil trumf a je nemozne aby meli protihraci kilo (nemaji hlas)
            if ((bidding.Bids & Hra.KiloProti) != 0 &&
                (bid & Hra.Sedma) == 0 &&
                ((Settings.CanPlayGameType[Hra.KiloProti] &&
                  estimatedFinalBasicScore >= 60 &&
                  (bidding._hundredAgainstFlek <= Settings.MaxDoubleCountForGameType[Hra.KiloProti] ||
                   (PlayerIndex == _g.GameStartingPlayerIndex &&
                    (Probabilities.HlasProbability((PlayerIndex + 1) % Game.NumPlayers) == 0) &&
                    (Probabilities.HlasProbability((PlayerIndex + 2) % Game.NumPlayers) == 0))) &&                  
                  (PlayerIndex != _g.GameStartingPlayerIndex &&
                   kqScore >= 20 &&
                   _gameSimulations > 0 &&
                   _hundredsAgainstBalance / (float)_gameSimulations >= hundredAgainstThreshold))))
            {
                bid |= bidding.Bids & Hra.KiloProti;
                bid &= (Hra)~Hra.Hra; //u kila proti uz nehlasime flek na hru
                //minRuleCount = Math.Min(minRuleCount, _hundredsAgainstBalance);
                DebugInfo.RuleCount = _hundredsAgainstBalance;
                DebugInfo.TotalRuleCount = _gameSimulations;
            }
            //durch flekuju jen pokud jsem volil sam durch a v simulacich jsem ho uhral dost casto
            //nebo pokud jsem nevolil a nejde temer uhrat            
            if ((bidding.Bids & Hra.Durch) != 0 &&
                Settings.CanPlayGameType[Hra.Durch] &&
                _durchSimulations > 0 && 
                (bidding._betlDurchFlek <= Settings.MaxDoubleCountForGameType[Hra.Durch] ||
                 (bidding._betlDurchFlek <= MaxFlek &&
                  _durchBalance / (float)_durchSimulations >= certaintyThreshold)) &&
                ((PlayerIndex == _g.GameStartingPlayerIndex && _durchBalance / (float)_durchSimulations >= durchThreshold && IsDurchCertain()) ||
			     (PlayerIndex != _g.GameStartingPlayerIndex && Hand.Count(i => i.Value == Hodnota.Eso) >= 3)))
            {
                bid |= bidding.Bids & Hra.Durch;
                //minRuleCount = Math.Min(minRuleCount, _durchBalance);
                DebugInfo.RuleCount = _durchBalance;
                DebugInfo.TotalRuleCount = _durchSimulations;
            }
            //betla flekuju jen pokud jsem volil sam betla a v simulacich jsem ho uhral dost casto
            if ((bidding.Bids & Hra.Betl) != 0 &&
                Settings.CanPlayGameType[Hra.Betl] &&
                _betlSimulations > 0 && 
                (bidding._betlDurchFlek <= Settings.MaxDoubleCountForGameType[Hra.Betl] ||
				 (bidding._betlDurchFlek <= MaxFlek &&
                  _betlBalance / (float)_betlSimulations >= certaintyThreshold)) &&
                PlayerIndex == _g.GameStartingPlayerIndex &&
                _betlBalance / (float)_betlSimulations >= betlThreshold &&
                (TeamMateIndex != -1 ||     //pokud jsem volil betla, tak si dej re a vys jen kdyz mas jedn jednu diru (kterou vyjedes)
                 GetBetlHoles() <= 1 ||
                 (GetBetlHoles() == 2 &&    //nebo pokud mas 2 diry z nichz jedna je samotna osmicka
                  Enum.GetValues(typeof(Barva)).Cast<Barva>()
                      .Any(b => (Hand.Has8(b) &&
                                 Hand.CardCount(b) == 1)))))
            {
                bid |= bidding.Bids & Hra.Betl;
                //minRuleCount = Math.Min(minRuleCount, _betlBalance);
                DebugInfo.RuleCount = _betlBalance;
                DebugInfo.TotalRuleCount = _betlSimulations;
            }
            var opponentLowCards = EstimateLowBetlCardCount();
            var opponentMidCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                       .SelectMany(b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                            .Select(h => new Card(b, h))
                                                            .Where(i => i.BadValue <= Card.GetBadValue(Hodnota.Desitka) &&
                                                                        !Hand.Contains(i) &&
                                                                        Hand.Any(j => j.Suit == i.Suit &&
                                                                                      j.BadValue < i.BadValue) &&
                                                                        Hand.Any(j => j.Suit == i.Suit &&
                                                                                      j.BadValue > i.BadValue)));
            var opMidSuits = opponentMidCards.Select(i => i.Suit).Distinct().Count();            
            var minCardPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                     .ToDictionary(b => b,
                                                   b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                            .Select(h => new Card(b, h))
                                                            .OrderBy(i => i.BadValue)
                                                            .FirstOrDefault(i => Hand.Any(j => j.Suit == i.Suit &&
                                                                                               j.Value == i.Value)));

            if ((bidding.Bids & Hra.Betl) != 0 &&
                Settings.CanPlayGameType[Hra.Betl] &&
                bidding._betlDurchFlek <= 1 &&
                PlayerIndex != _g.GameStartingPlayerIndex &&
                (opponentLowCards <= 6 ||                                   //1. flekuj pokud akter nemuze mit moc nizkych karet
                 (TeamMateIndex == (PlayerIndex + 1) % Game.NumPlayers &&   //2. flekuj pokud jsi na druhe pozici
                  opponentMidCards.Count() >= 5 &&                          //a vidis aspon 5 der
                  opponentLowCards <= 8 &&
                  opMidSuits >= 2 &&                                        //a mas nizsi karty nez diry aspon ve dvou barvach
                  (handSuits < Game.NumSuits ||                             //nebo znas vsechny barvy a mas nizke karty ve trech barvach
                   opMidSuits >= 3 &&
                   minCardPerSuit.All(i => i.Value == null ||               //z nichz je kazda mensi nez desitka
                                           i.Value.BadValue < Card.GetBadValue(Hodnota.Desitka))
                   ))))
            {
                bid |= bidding.Bids & Hra.Betl;
                //minRuleCount = Math.Min(minRuleCount, _betlBalance);
                DebugInfo.RuleCount = _betlBalance;
                DebugInfo.TotalRuleCount = _betlSimulations;
            }

            if (DebugInfo.RuleCount == -1)
            {
                DebugInfo.RuleCount = _gameType == Hra.Betl
                                            ? _betlBalance
                                            : _gameType == Hra.Durch
                                                ? _durchBalance
                                                : (bidding.Bids & Hra.Kilo) != 0
                                                    ? _hundredsBalance
                                                    : (bidding.Bids & Hra.Sedma) != 0
                                                        ? _sevensBalance
                                                        : _gamesBalance;
                DebugInfo.TotalRuleCount = _gameType == Hra.Betl
                                            ? _betlSimulations
                                            : _gameType == Hra.Durch
                                                ? _durchSimulations
                                                : (bidding.Bids & Hra.Kilo) != 0
                                                    ? _hundredSimulations
                                                    : (bidding.Bids & Hra.Sedma) != 0
                                                        ? _sevenSimulations
                                                        : _gameSimulations;
            }
            DebugInfo.Rule = bid.ToString();
            BidConfidence = DebugInfo.TotalRuleCount > 0 ? (float)DebugInfo.RuleCount / (float)DebugInfo.TotalRuleCount : -1;
            var allChoices = new List<RuleDebugInfo>();

            if ((bidding.Bids & Hra.Hra) != 0)
            {
                allChoices.Add(new RuleDebugInfo
                {
                    Rule = Hra.Hra.ToString(),
                    RuleCount = _gamesBalance,
                    TotalRuleCount = _gameSimulations
                });
            }
            if ((bidding.Bids & Hra.Sedma) != 0)
            {
                allChoices.Add(new RuleDebugInfo
                {
                    Rule = Hra.Sedma.ToString(),
                    RuleCount = _sevensBalance,
                    TotalRuleCount = _sevenSimulations
                });
            }
            if ((bidding.Bids & Hra.SedmaProti) != 0 &&
                (TeamMateIndex == -1 ||
                 Hand.Has7(_g.trump.Value)))
            {
                allChoices.Add(new RuleDebugInfo
                {
                    Rule = Hra.SedmaProti.ToString(),
                    RuleCount = _sevensAgainstBalance,
                    TotalRuleCount = _gameSimulations
                });
            }
            if ((bidding.Bids & Hra.Kilo) != 0)
            {
                allChoices.Add(new RuleDebugInfo
                {
                    Rule = Hra.Kilo.ToString(),
                    RuleCount = _hundredsBalance,
                    TotalRuleCount = _hundredSimulations
                });
            }
            if ((bidding.Bids & Hra.KiloProti) != 0 &&
                (TeamMateIndex == -1 ||
                 Enum.GetValues(typeof(Barva)).Cast<Barva>()
                     .Any(b => Hand.HasK(b) &&
                               Hand.HasQ(b))))
            {
                allChoices.Add(new RuleDebugInfo
                {
                    Rule = Hra.KiloProti.ToString(),
                    RuleCount = _hundredsAgainstBalance,
                    TotalRuleCount = _gameSimulations
                });
            }
            if ((bidding.Bids & Hra.Betl) != 0)
            {
                allChoices.Add(new RuleDebugInfo
                {
                    Rule = Hra.Betl.ToString(),
                    RuleCount = _betlBalance,
                    TotalRuleCount = _betlSimulations
                });
            }
            if ((bidding.Bids & Hra.Durch) != 0)
            {
                allChoices.Add(new RuleDebugInfo
                {
                    Rule = Hra.Durch.ToString(),
                    RuleCount = _durchBalance,
                    TotalRuleCount = _durchSimulations
                });
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
            _teamMateDoubledSeven = false;
            _shouldMeasureThroughput = true;
            Settings.SimulationsPerRoundPerSecond = 0;
        }

		public void GameLoaded(object sender)
		{
            _gameType = _g.GameType;
            _trump = _g.trump;
			if (PlayerIndex == _g.GameStartingPlayerIndex)
			{
                _talon = new List<Card>(_g.talon); //TODO: tohle by se melo delat v Game.LoadGame()!
			}
            Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump,
                                            _g.AllowFakeSeven || _g.AllowFake107, _g.AllowAXTalon, _g.AllowTrumpTalon,
                                            _g.CancellationToken, _stringLoggerFactory, _talon)
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
                TrumpCard = null;
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
                Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump,
                                                _g.AllowFakeSeven || _g.AllowFake107, _g.AllowAXTalon, _g.AllowTrumpTalon,
                                                _g.CancellationToken, _stringLoggerFactory, _talon)
				{
					ExternalDebugString = _debugString
				};
			}
            Probabilities.UpdateProbabilitiesAfterGameTypeChosen(e);
        }

        private new void BidMade (object sender, BidEventArgs e)
        {
            //spoluhrac flekoval hru nebo kilo
            if (e.Player.PlayerIndex == TeamMateIndex && (e.BidMade & (Hra.Hra | Hra.Kilo)) != 0)
            {
                _teamMateDoubledGame = true;
            }
            if (e.Player.PlayerIndex == TeamMateIndex && (e.BidMade & Hra.Sedma) != 0)
            {
                _teamMateDoubledSeven = true;
            }
            if (e.Player.PlayerIndex != PlayerIndex &&
                e.Player.PlayerIndex != _g.GameStartingPlayerIndex &&
                (e.BidMade & Hra.Sedma) != 0 &&
                ((PlayerIndex != _g.GameStartingPlayerIndex &&
                  !_g.AllowFakeSeven) ||
                 (PlayerIndex == _g.GameStartingPlayerIndex &&
                  Hand.Has7(_g.trump.Value))))
            {
                _initialSimulation = true;
            }
            Probabilities.UpdateProbabilitiesAfterBidMade(e, _g.Bidding);

            if (_g.Bidding.SevenMultiplier > 0 &&
                _g.Bidding.SevenMultiplier * _g.SevenValue < _g.Bidding.GameMultiplier * _g.GameValue &&
                _g.GameStartingPlayer.DebugInfo.TotalRuleCount > 0)
            {
                DebugInfo.RuleCount = DebugInfo.AllChoices
                                               .Where(i => i.Rule.StartsWith(Hra.Hra.ToString()))
                                               .Select(i => i.RuleCount)
                                               .FirstOrDefault();
                DebugInfo.TotalRuleCount = DebugInfo.AllChoices
                                                    .Where(i => i.Rule.StartsWith(Hra.Hra.ToString()))
                                                    .Select(i => i.TotalRuleCount)
                                                    .FirstOrDefault();
            }
        }

        private bool IsGameWinningRound(Round round, Round[] rounds, int playerIndex, int teamMateIndex, List<Card> hand, Probability prob)
        {
            var basicPointsWonSoFar = 0;
            var basicPointsWonThisRound = (round?.c1?.Value >= Hodnota.Desitka ? 10 : 0) +
                                          (round?.c2?.Value >= Hodnota.Desitka ? 10 : 0) +
                                          (round?.c3?.Value >= Hodnota.Desitka ? 10 : 0);
            var basicPointsLost = 0;
            var hlasPointsLost = 0;
            var hlasPointsWon = 0;
            var hlasPointsWonThisRound = 0;
            var maxHlasPointsWon = 0;

            foreach (var r in rounds.Where(r => r?.number < round?.number))
            {
                if (r.c1.Value >= Hodnota.Desitka)
                {
                    if (r.roundWinner.PlayerIndex == playerIndex ||
                        r.roundWinner.PlayerIndex == teamMateIndex)
                    {
                        basicPointsWonSoFar += 10;
                    }
                    else
                    {
                        basicPointsLost += 10;
                    }
                }
                if (r.c2.Value >= Hodnota.Desitka)
                {
                    if (r.roundWinner.PlayerIndex == playerIndex ||
                        r.roundWinner.PlayerIndex == teamMateIndex)
                    {
                        basicPointsWonSoFar += 10;
                    }
                    else
                    {
                        basicPointsLost += 10;
                    }
                }
                if (r.c3.Value >= Hodnota.Desitka)
                {
                    if (r.roundWinner.PlayerIndex == playerIndex ||
                        r.roundWinner.PlayerIndex == teamMateIndex)
                    {
                        basicPointsWonSoFar += 10;
                    }
                    else
                    {
                        basicPointsLost += 10;
                    }
                }
                hlasPointsWonThisRound = 0;
                if (r.hlas1)
                {
                    if (r.player1.PlayerIndex == playerIndex ||
                        r.player1.PlayerIndex == teamMateIndex)
                    {
                        hlasPointsWonThisRound = r.c1.Suit == _trump ? 40 : 20;
                        hlasPointsWon += hlasPointsWonThisRound;
                        maxHlasPointsWon = teamMateIndex == -1
                                            ? _g.HlasConsidered == HlasConsidered.Highest
                                                ? Math.Max(maxHlasPointsWon, hlasPointsWonThisRound)
                                                : _g.HlasConsidered == HlasConsidered.First && maxHlasPointsWon == 0
                                                    ? hlasPointsWonThisRound
                                                    : _g.HlasConsidered == HlasConsidered.Each
                                                        ? hlasPointsWon
                                                        : 0
                                            : 0;
                    }
                    else
                    {
                        hlasPointsLost += r.c1.Suit == _trump ? 40 : 20;
                    }
                }
                if (r.hlas2)
                {
                    if (r.player2.PlayerIndex == playerIndex ||
                        r.player2.PlayerIndex == teamMateIndex)
                    {
                        hlasPointsWonThisRound = r.c2.Suit == _trump ? 40 : 20;
                        hlasPointsWon += hlasPointsWonThisRound;
                        maxHlasPointsWon = teamMateIndex == -1
                                            ? _g.HlasConsidered == HlasConsidered.Highest
                                                ? Math.Max(maxHlasPointsWon, hlasPointsWonThisRound)
                                                : _g.HlasConsidered == HlasConsidered.First && maxHlasPointsWon == 0
                                                    ? hlasPointsWonThisRound
                                                    : _g.HlasConsidered == HlasConsidered.Each
                                                        ? hlasPointsWon
                                                        : 0
                                            : 0;
                    }
                    else
                    {
                        hlasPointsLost += r.c2.Suit == _trump ? 40 : 20;
                    }
                }
                if (r.hlas3)
                {
                    if (r.player3.PlayerIndex == playerIndex ||
                        r.player3.PlayerIndex == teamMateIndex)
                    {
                        hlasPointsWonThisRound = r.c3.Suit == _trump ? 40 : 20;
                        hlasPointsWon += hlasPointsWonThisRound;
                        maxHlasPointsWon = teamMateIndex == -1
                                            ? _g.HlasConsidered == HlasConsidered.Highest
                                                ? Math.Max(maxHlasPointsWon, hlasPointsWonThisRound)
                                                : _g.HlasConsidered == HlasConsidered.First && maxHlasPointsWon == 0
                                                    ? hlasPointsWonThisRound
                                                    : _g.HlasConsidered == HlasConsidered.Each
                                                        ? hlasPointsWon
                                                        : 0
                                            : 0;
                    }
                    else
                    {
                        hlasPointsLost += r.c3.Suit == _trump ? 40 : 20;
                    }
                }
            }
            var basicPointsLeft = 90 - basicPointsWonSoFar - basicPointsWonThisRound - basicPointsLost;
            var player2 = (playerIndex + 1) % Game.NumPlayers;
            var player3 = (playerIndex + 2) % Game.NumPlayers;
            var opponent = teamMateIndex == player2 ? player3 : player2;
            var kqScore = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                              .Sum(b => hand.HasK(b) &&
                                        hand.HasQ(b)
                                        ? b == _trump ? 40 : 20
                                        : 0);
            var hlasPointsLeft = teamMateIndex == -1
                                 ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                       .Sum(b => (prob.PotentialCards(player2).HasK(b) &&
                                                  prob.PotentialCards(player2).HasQ(b)) ||
                                                 (prob.PotentialCards(player3).HasK(b) &&
                                                  prob.PotentialCards(player3).HasQ(b))
                                                  ? b == _trump ? 40 : 20
                                                  : 0)
                                 : Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                       .Sum(b => prob.PotentialCards(opponent).HasK(b) &&
                                                 prob.PotentialCards(opponent).HasQ(b)
                                                 ? b == _trump ? 40 : 20
                                                 : 0);
            var opponentPotentialPoints = basicPointsLost + hlasPointsLost + basicPointsLeft + hlasPointsLeft;
            var gameWinningCard = false;

            if ((_gameType & Hra.Kilo) != 0 &&
                ((teamMateIndex == -1 &&
                  basicPointsWonSoFar + maxHlasPointsWon <= 90 &&
                  basicPointsWonSoFar + basicPointsWonThisRound + maxHlasPointsWon >= 100) ||
                 (teamMateIndex != -1 &&
                  basicPointsWonSoFar <= 30 &&
                  basicPointsWonSoFar + basicPointsWonThisRound >= 40)))
            {
                gameWinningCard = true;
            }
            else if ((_gameType & Hra.Kilo) == 0 &&
                        basicPointsWonSoFar + hlasPointsWon + kqScore <= opponentPotentialPoints &&
                        basicPointsWonSoFar + basicPointsWonThisRound + hlasPointsWon + kqScore > opponentPotentialPoints)
            {
                gameWinningCard = true;
            }

            return gameWinningCard;
        }

        private void CardPlayed(object sender, Round r)
        {
            var gameWinningRound = IsGameWinningRound(r, _g.rounds, PlayerIndex, TeamMateIndex, Hand, Probabilities);
            UpdateProbabilitiesAfterCardPlayed(Probabilities, r.number, r.player1.PlayerIndex, r.c1, r.c2, r.c3, r.hlas1, r.hlas2, r.hlas3, TeamMateIndex, _teamMatesSuits, _trump, _teamMateDoubledGame, gameWinningRound);

            if (_g.RoundNumber > 0 &&           //pri vlastni hre
                r.number < Game.NumRounds &&    //pred posledim kolem
                r.c3 != null)                   //na konci kola
            {
                DebugInfo.ProbDebugInfo = GetProbabilityString(r);
            }
            else
            {
                DebugInfo.ProbDebugInfo = null;
            }
        }

        private static void UpdateProbabilitiesAfterCardPlayed(Probability probabilities, int roundNumber, int roundStarterIndex, Card c1, Card c2, Card c3, bool hlas1, bool hlas2, bool hlas3, int teamMateIndex, List<Barva> teamMatesSuits, Barva? trump, bool teamMateDoubledGame, bool gameWinningRound)
        {
            if (c3 != null)
            {
                probabilities.UpdateProbabilities(roundNumber, roundStarterIndex, c1, c2, c3, hlas3, gameWinningRound);
            }
            else if (c2 != null)
            {
                probabilities.UpdateProbabilities(roundNumber, roundStarterIndex, c1, c2, hlas2, gameWinningRound);
            }
            else
            {
                probabilities.UpdateProbabilities(roundNumber, roundStarterIndex, c1, hlas1);
                if (roundStarterIndex == teamMateIndex)// && teamMateDoubledGame)
                {
                    if (!teamMatesSuits.Contains(c1.Suit))
                    {
                        teamMatesSuits.Add(c1.Suit);
                    }
                }
            }
        }

        private bool ShouldComputeBestCard(Round r)
        {
            return _g.FirstMinMaxRound > 0 && r.number >= _g.FirstMinMaxRound && r.number < Game.NumRounds && r.c1 == null && _g.GameType != Hra.Durch;
        }

        private string GetProbabilityString(Round r)
        {
            var certainCards1 = 0 == PlayerIndex || !Probabilities.CertainCards(0).Any() ? null : Probabilities.CertainCards(0).ToHandString();
            var certainCards2 = 1 == PlayerIndex || !Probabilities.CertainCards(1).Any() ? null : Probabilities.CertainCards(1).ToHandString();
            var certainCards3 = 2 == PlayerIndex || !Probabilities.CertainCards(2).Any() ? null : Probabilities.CertainCards(2).ToHandString();
            var potentialCards1 = 0 == PlayerIndex || !Probabilities.PotentialCards(0).Any() ? null : Probabilities.PotentialCards(0).ToHandString();
            var potentialCards2 = 1 == PlayerIndex || !Probabilities.PotentialCards(1).Any() ? null : Probabilities.PotentialCards(1).ToHandString();
            var potentialCards3 = 2 == PlayerIndex || !Probabilities.PotentialCards(2).Any() ? null : Probabilities.PotentialCards(2).ToHandString();
            var unlikelyCards1 = 0 == PlayerIndex || !Probabilities.UnlikelyCards(0).Any() ? null : Probabilities.UnlikelyCards(0).ToHandString();
            var unlikelyCards2 = 1 == PlayerIndex || !Probabilities.UnlikelyCards(1).Any() ? null : Probabilities.UnlikelyCards(1).ToHandString();
            var unlikelyCards3 = 2 == PlayerIndex || !Probabilities.UnlikelyCards(2).Any() ? null : Probabilities.UnlikelyCards(2).ToHandString();
            var certainCardsT = Probabilities.CertainCards(3).Any() ? Probabilities.CertainCards(3).ToHandString() : null;

            var sb = new StringBuilder();

            sb.Append($"Hráč{PlayerIndex + 1} si myslí že\n");
            if (certainCards1 != null)
            {
                sb.Append($"Hráč1 určitě má\n{certainCards1}\n");
            }
            if (!(certainCards1 != null &&
                  potentialCards1 != null &&
                  certainCards1 == potentialCards1))
            {
                if (potentialCards1 != null)
                {
                    sb.Append($"Hráč1 může mít\n{potentialCards1}\n");
                }
                if (unlikelyCards1 != null)
                {
                    sb.Append($"Hráč1 určitě nemá\n{unlikelyCards1}\n");
                }
            }
            if (certainCards2 != null)
            {
                sb.Append($"Hráč2 určitě má\n{certainCards2}\n");
            }
            if (!(certainCards2 != null &&
                  potentialCards2 != null &&
                  certainCards2 == potentialCards2))
            {
                if (potentialCards2 != null)
                {
                    sb.Append($"Hráč2 může mít\n{potentialCards2}\n");
                }
                if (unlikelyCards2 != null)
                {
                    sb.Append($"Hráč2 určitě nemá\n{unlikelyCards2}\n");
                }
            }
            if (certainCards3 != null)
            {
                sb.Append($"Hráč3 určitě má\n{certainCards3}\n");
            }
            if (!(certainCards3 != null &&
                  potentialCards3 != null &&
                  certainCards3 == potentialCards3))
            {
                if (potentialCards3 != null)
                {
                    sb.Append($"Hráč3 může mít\n{potentialCards3}\n");
                }
                if (unlikelyCards3 != null)
                {
                    sb.Append($"Hráč3 určitě nemá\n{unlikelyCards3}\n");
                }
            }
            if (certainCardsT != null)
            {
                sb.Append($"V talonu určitě je\n{certainCardsT}");
            }

            return sb.ToString().Replace("-", "");
        }

        public override Card PlayCard(Round r)
        {
            var roundStarterIndex = r.player1.PlayerIndex;
            Card cardToPlay = null;
            var cardScores = new ConcurrentDictionary<Card, ConcurrentQueue<GameComputationResult>>();

			_debugString.AppendFormat("PlayCard(round{0}: c1: {1} c2: {2} c3: {3})\n", r.number, r.c1, r.c2, r.c3);
            if (Settings.Cheat)
            {
                ResetDebugInfo();

                var hands = _g.players.Select(i => new Hand(i.Hand)).Concat(new[] { new Hand(_g.talon) }).ToArray();
                for (var i = 0; i < Game.NumPlayers + 1; i++)
                {
                    if (r.c1 != null && !hands[r.player1.PlayerIndex].Any(j => j == r.c1))
                    {
                        hands[r.player1.PlayerIndex] = new Hand(((List<Card>)(hands[r.player1.PlayerIndex])).Concat(new [] { r.c1 }));
                    }
                    if (r.c2 != null && !hands[r.player2.PlayerIndex].Any(j => j == r.c2))
                    {
                        hands[r.player2.PlayerIndex] = new Hand(((List<Card>)(hands[r.player2.PlayerIndex])).Concat(new[] { r.c2 }));
                    }
                }
                var computationResult = ComputeGame(hands, r.c1, r.c2);

                cardToPlay = computationResult.CardToPlay;
                DebugInfo.Card = cardToPlay;
                DebugInfo.Rule = computationResult.Rule.Description;
                DebugInfo.RuleCount = 1;
                DebugInfo.TotalRuleCount = 1;
                DebugInfo.AiDebugInfo = computationResult.Rule.AiDebugInfo;
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
                               ? ShouldComputeBestCard(r)
                                    ? Probabilities.GenerateAllHandCombinations(r.number)
                                    : Probabilities.GenerateHands(r.number, roundStarterIndex, 1)
                               : Probabilities.GenerateHands(r.number, roundStarterIndex, simulations);
                var progress = 0;
                var start = DateTime.Now;

                if (_g.CurrentRound != null)
                {
                    //pokud je hra v behu tak krome betla nepotrebujeme paralelni vypocty
                    //protoze ted pouzivame pravdepodobnostni pravidla
                    options = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 1
                    };
                }
                var exceptions = new ConcurrentQueue<Exception>();
                if (ShouldComputeBestCard(r))
                {
                    System.Diagnostics.Debug.WriteLine("Uhraj co nejlepší výsledek");
                    cardToPlay = ComputeBestCardToPlay(source, r.number);
                    GC.Collect();

                    if (cardToPlay != null)
                    {
                        return cardToPlay;
                    }
                    //minimax nestihl dobehnout, pokracuj klasicky
                    source = Probabilities.GenerateHands(r.number, roundStarterIndex, 1);
                    start = DateTime.Now;
                }
                Parallel.ForEach(source, options, (hands, loopState) =>
                {
                    ThrowIfCancellationRequested();
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"PlayCard (player{PlayerIndex + 1} r {r.number} c1 {r.c1} c2 {r.c2})");
                        System.Diagnostics.Debug.WriteLine(hands[0]);
                        System.Diagnostics.Debug.WriteLine(hands[1]);
                        System.Diagnostics.Debug.WriteLine(hands[2]);
                        System.Diagnostics.Debug.WriteLine(hands[3]);
                        Check(hands);
                        if ((DateTime.Now - start).TotalMilliseconds > Settings.MaxSimulationTimeMs)
                        {
                            Probabilities.StopGeneratingHands();
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
                                (computationResult.Rule.SkipSimulations &&
                                    r.number != 9) ||     //in round no. 9 we want to run simulations every time to mitigate a chance of bad ending
                                canSkipSimulations)    //We have only one card to play, so there is really no need to compute anything
                            {
                                OnGameComputationProgress(new GameComputationProgressEventArgs { Current = simulations, Max = Settings.SimulationsPerRoundPerSecond > 0 ? simulations : 0 });
                                Probabilities.StopGeneratingHands();
                                loopState.Stop();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message + "\n" + ex.StackTrace);
                        exceptions.Enqueue(ex);
                    }
                });
                ThrowIfCancellationRequested();
                if (_shouldMeasureThroughput) // only do this 1st time when we calculate most to get a more realistic benchmark
                {
                    var end = DateTime.Now;
                    Settings.SimulationsPerRoundPerSecond = (int)((float)progress / Settings.MaxSimulationTimeMs * 1000);
                    //Settings.SimulationsPerRoundPerSecond = (int)((float)progress / (end - start).TotalMilliseconds * 1000);
                    _shouldMeasureThroughput = false;
                }
                if (canSkipSimulations)
                {
                    _log.InfoFormat("Other simulations have been skipped");
                }
                try
                {
                    cardToPlay = ChooseCardToPlay(cardScores.ToDictionary(k => k.Key, v => new List<GameComputationResult>(v.Value)));
                }
                catch
                {
                }
                finally
                {
                    List<Card> cardsToPlay;

                    if (cardToPlay == null)
                    {
                        DebugInfo.Rule = "Náhodná karta (simulace neproběhla)";
                        DebugInfo.RuleCount = 1;
                        DebugInfo.TotalRuleCount = 1;
                        if (r.c2 != null)
                        {
                            cardsToPlay = ValidCards(Hand, _trump, _gameType.Value, TeamMateIndex, r.c1, r.c2);
                        }
                        else if (r.c1 != null)
                        {
                            cardsToPlay = ValidCards(Hand, _trump, _gameType.Value, TeamMateIndex, r.c1);
                        }
                        else
                        {
                            cardsToPlay = ValidCards(Hand, _trump, _gameType.Value, TeamMateIndex);
                        }
                        if ((_gameType.Value & (Hra.Betl | Hra.Durch)) != 0)
                        {
                            cardToPlay = cardsToPlay.OrderByDescending(i => i.BadValue).First();
                        }
                        else
                        {
                            cardToPlay = cardsToPlay.OrderBy(i => i.Value).First();
                        }
                        DebugInfo.Card = cardToPlay;
                    }
                    else if (((TeamMateIndex == -1 &&
                               (_gameType & Hra.Kilo) != 0) ||
                              (TeamMateIndex != -1 &&
                               (_gameType & Hra.KiloProti) != 0)) &&
                             _g.HlasConsidered == HlasConsidered.First &&                             
                             cardToPlay.Value == Hodnota.Svrsek &&
                             cardToPlay.Suit != _g.trump.Value &&
                             Hand.HasK(cardToPlay.Suit) &&
                             Hand.HasK(_g.trump.Value) &&
                             Hand.HasQ(_g.trump.Value))
                    {
                        if (r.c2 != null)
                        {
                            cardsToPlay = ValidCards(Hand, _trump, _gameType.Value, TeamMateIndex, r.c1, r.c2);
                        }
                        else if (r.c1 != null)
                        {
                            cardsToPlay = ValidCards(Hand, _trump, _gameType.Value, TeamMateIndex, r.c1);
                        }
                        else
                        {
                            cardsToPlay = ValidCards(Hand, _trump, _gameType.Value, TeamMateIndex);
                        }
                        if (cardsToPlay.HasQ(_trump.Value))
                        {
                            DebugInfo.Rule = "Hraj trumfovou hlášku";
                            DebugInfo.RuleCount = 1;
                            DebugInfo.TotalRuleCount = 1;

                            cardToPlay = cardsToPlay.First(i => i.Value == Hodnota.Svrsek &&
                                                                i.Suit == _trump.Value);
                            DebugInfo.Card = cardToPlay;
                        }
                    }
                }
            }

            _log.InfoFormat("{0} plays card: {1} - {2}", Name, cardToPlay, DebugInfo.Rule);
            return cardToPlay;                
        }

        private Card ComputeBestCardToPlay(IEnumerable<Hand[]> source, int roundNumber)
        {
            var results = new ConcurrentQueue<Tuple<Card, MoneyCalculatorBase, GameComputationResult, Hand[]>>();
            var likelyResults = new ConcurrentQueue<Tuple<Card, MoneyCalculatorBase, GameComputationResult, Hand[]>>();
            var averageResults = new Dictionary<Card, double>();
            var minResults = new Dictionary<Card, double>();
            var maxResults = new Dictionary<Card, double>();
            var winCount = new Dictionary<Card, int>();
            var n = 0;
            var start = DateTime.Now;
            var prematureStop = false;
            var gameStartingHand = new List<Card>();

            var estimatedCombinations = (int)Probabilities.EstimateTotalCombinations(roundNumber);
            var maxtime = 3 * Settings.MaxSimulationTimeMs;
            var exceptionOccured = false;

            try
            {
                options.MaxDegreeOfParallelism = Settings.MaxDegreeOfParallelism > 0 ? Settings.MaxDegreeOfParallelism : -1;
                //source = new[] { _g.players.Select(i => new Hand(i.Hand)).ToArray() };
                //foreach (var hands in source)
                Parallel.ForEach(source, options, (hands, loopState) =>
                {
                    ThrowIfCancellationRequested();
                    try
                    {
                        if ((DateTime.Now - start).TotalMilliseconds > maxtime || exceptionOccured)
                        {
                            prematureStop = true;
                            loopState.Stop();
                            //break;
                        }
                        var gameStartedInitialCards = _g.rounds.Where(r => r != null && r.c3 != null)
                                                               .Select(r =>
                                                               {
                                                                   if (r.player1.PlayerIndex == _g.GameStartingPlayerIndex)
                                                                   {
                                                                       return r.c1;
                                                                   }
                                                                   else if (r.player2.PlayerIndex == _g.GameStartingPlayerIndex)
                                                                   {
                                                                       return r.c2;
                                                                   }
                                                                   else
                                                                   {
                                                                       return r.c3;
                                                                   }
                                                               }).ToList();
                        var initialHand = gameStartedInitialCards.Concat((List<Card>)hands[_g.GameStartingPlayerIndex]).Concat((List<Card>)hands[3]).ToList();
                        var gameStarterPlayedCards = _g.rounds.Where(r => r?.c1 != null && r.player1.PlayerIndex == _g.GameStartingPlayerIndex)
                                                              .Select(r => r.c1).ToList();
                        var maxPlayedSuitLength = gameStarterPlayedCards.Max(i => initialHand.CardCount(i.Suit));
                        var longUnplayedSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                    .Where(b => b != _trump &&
                                                                !gameStarterPlayedCards.HasSuit(b) &&
                                                                initialHand.CardCount(b) > maxPlayedSuitLength);
                        var talon = ChooseNormalTalon(initialHand, _g.TrumpCard);
                        var isLikelyTalonForHand = IsLikelyTalonForHand(hands);
                        var hh = new[] {
                            new Hand((List<Card>)hands[0]),
                            new Hand((List<Card>)hands[1]),
                            new Hand((List<Card>)hands[2]),
                            new Hand((List<Card>)hands[3]),
                            new Hand(initialHand),
                            new Hand(talon)};
                        var result = ComputeMinMax(new List<Round>(_g.rounds.Where(r => r?.c3 != null)), hh, roundNumber);

                        foreach (var res in result)
                        {
                            var opponentBidders = _g.Bidding.PlayerBids
                                                            .Select((i, idx) => new { bid = i, idx })
                                                            .Where(i => i.idx != PlayerIndex &&
                                                                        i.bid != 0)
                                                            .Select(i => i.idx)
                                                            .ToList();
                            var teamMateDoubledGame = TeamMateIndex != -1 &&
                                                      (_g.Bidding.PlayerBids[TeamMateIndex] & Hra.Hra) != 0;
                            var player2 = (PlayerIndex + 1) % Game.NumPlayers;
                            var player3 = (PlayerIndex + 2) % Game.NumPlayers;
                            const float epsilon = 0.01f;

                            results.Enqueue(new Tuple<Card, MoneyCalculatorBase, GameComputationResult, Hand[]>(res.Item1, res.Item2, res.Item3, hh));
                            if (!_g.trump.HasValue ||
                                (!hands[player2].Any(i => Probabilities.CardProbability(player2, new Card(i.Suit, i.Value)) <= epsilon) && //neber v potaz rozlohy s malou pravdepodobnosti na zaklade predchozi hry
                                 !hands[player3].Any(i => Probabilities.CardProbability(player3, new Card(i.Suit, i.Value)) <= epsilon) &&
                                 ((PlayerIndex == _g.GameStartingPlayerIndex &&
                                   (!opponentBidders.Any() ||
                                    opponentBidders.Any(i => Probabilities.SuitProbability(i, _g.trump.Value, roundNumber) == 0 ||
                                                             hands[i].HasSuit(_g.trump.Value)))) ||
                                  (PlayerIndex != _g.GameStartingPlayerIndex &&
                                   !(teamMateDoubledGame &&
                                     (Probabilities.CardProbability(TeamMateIndex, new Card(_g.trump.Value, Hodnota.Kral)) > 0 ||
                                      Probabilities.CardProbability(TeamMateIndex, new Card(_g.trump.Value, Hodnota.Svrsek)) > 0) &&
                                     (!hands[TeamMateIndex].HasK(_g.trump.Value) &&
                                      !hands[TeamMateIndex].HasQ(_g.trump.Value))) &&
                                   !(Enum.GetValues(typeof(Barva)).Cast<Barva>()     //hlasku kolegy nepocitej pokud ji do ted neukazal
                                         .Any(b => hands[TeamMateIndex].HasK(b) &&
                                                   hands[TeamMateIndex].HasQ(b))) &&
                                   !longUnplayedSuits.Any() &&                       //pokud ma akter dlouhou barvu kterou nehral, asi barvu nezna
                                   isLikelyTalonForHand))))
                            {
                                likelyResults.Enqueue(new Tuple<Card, MoneyCalculatorBase, GameComputationResult, Hand[]>(res.Item1, res.Item2, res.Item3, hh));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Exception in ComputeBestCardToPlay: {0}\n{1}", ex.Message, ex.StackTrace);
                        exceptionOccured = true;
                    }
                    OnGameComputationProgress(new GameComputationProgressEventArgs { Current = ++n, Max = estimatedCombinations, Message = "Generuju karty" });
                });
            }
            catch(OperationCanceledException)
            {
                return null;
            }
            if (prematureStop)
            {
                return null;
            }

            if(!likelyResults.Any())
            {
                likelyResults = results;
            }

            foreach (var card in results.Select(i => i.Item1).Distinct())
            {
                averageResults.Add(card, likelyResults.Where(i => i.Item1 == card)
                                                      .Average(i => 100 * i.Item2.MoneyWon[PlayerIndex] +
                                                                    (TeamMateIndex == -1 ? i.Item2.BasicPointsWon : i.Item2.BasicPointsLost)));
                minResults.Add(card, likelyResults.Where(i => i.Item1 == card)
                                                  .Min(i => 100 * i.Item2.MoneyWon[PlayerIndex] +
                                                            (TeamMateIndex == -1 ? i.Item2.BasicPointsWon : i.Item2.BasicPointsLost)));
                maxResults.Add(card, likelyResults.Where(i => i.Item1 == card)
                                                  .Max(i => 100 * i.Item2.MoneyWon[PlayerIndex] +
                                                            (TeamMateIndex == -1 ? i.Item2.BasicPointsWon : i.Item2.BasicPointsLost)));
                winCount.Add(card, likelyResults.Where(i => i.Item1 == card)
                                                .Count(i => i.Item2.MoneyWon[PlayerIndex] >= 0));
            }

            if (roundNumber == _g.FirstMinMaxRound)
            {
                var sb = new StringBuilder();

                sb.Append($"Karta,");
                sb.Append($"Hráč1,Hráč2,Hráč3,Talon,");
                sb.Append($"VolbaTalonuZ,SimTalon,");
                sb.Append($"Váha,");
                sb.Append($"Skóre1,Skóre2,Skóre3,");
                sb.Append($"Výhra1,Výhra2,Výhra3,");
                sb.Append($"Začíná8,");
                sb.Append($"Vyhrál8,");
                sb.Append($"Karta1,Karta2,Karta3,");
                sb.Append($"Začíná9,");
                sb.Append($"Vyhrál9,");
                sb.Append($"Karta1,Karta2,Karta3,");
                sb.Append($"Začíná10,");
                sb.Append($"Vyhrál10,");
                sb.Append($"Karta1,Karta2,Karta3,");
                sb.Append("\n");

                foreach (var r in results)
                {
                    sb.Append($"{r.Item1},");
                    sb.Append($"{r.Item4[0]},{r.Item4[1]},{r.Item4[2]},{r.Item4[3]},");
                    sb.Append($"{r.Item4[4]},{r.Item4[5]},");
                    sb.Append($"{((!likelyResults.Any() || likelyResults.Contains(r)) ? "Vysoká" : "Nízká")},");
                    sb.Append($"{r.Item3.Score[0]},{r.Item3.Score[1]},{r.Item3.Score[2]},");
                    sb.Append($"{r.Item2.MoneyWon[0]},{r.Item2.MoneyWon[1]},{r.Item2.MoneyWon[2]},");
                    sb.Append($"Hráč{r.Item3.Rounds[7].RoundStarterIndex + 1},");
                    sb.Append($"Hráč{r.Item3.Rounds[7].RoundWinnerIndex + 1},");
                    sb.Append($"{r.Item3.Rounds[7].c1},{r.Item3.Rounds[7].c2},{r.Item3.Rounds[7].c3},");
                    sb.Append($"Hráč{r.Item3.Rounds[8].RoundStarterIndex + 1},");
                    sb.Append($"Hráč{r.Item3.Rounds[8].RoundWinnerIndex + 1},");
                    sb.Append($"{r.Item3.Rounds[8].c1},{r.Item3.Rounds[8].c2},{r.Item3.Rounds[8].c3},");
                    sb.Append($"Hráč{r.Item3.Rounds[9].RoundStarterIndex + 1},");
                    sb.Append($"Hráč{r.Item3.Rounds[9].RoundWinnerIndex + 1},");
                    sb.Append($"{r.Item3.Rounds[9].c1},{r.Item3.Rounds[9].c2},{r.Item3.Rounds[9].c3},");
                    sb.Append("\n");
                }
                _minMaxDebugString = sb.ToString();
#if PORTABLE
                using (var fs = _g.GetFileStream("_minmax.csv"))
                {
                    var bytes = Encoding.Default.GetBytes(_minMaxDebugString);

                    fs.Write(bytes, 0, bytes.Length);
                }
#endif
            }

            var topCards = Hand.Where(i => Probabilities.PotentialCards((PlayerIndex + 1) % Game.NumPlayers)
                                                        .Where(j => j.Suit == i.Suit)
                                                        .All(j => j.Value < i.Value) &&
                                           Probabilities.PotentialCards((PlayerIndex + 2) % Game.NumPlayers)
                                                        .Where(j => j.Suit == i.Suit)
                                                        .All(j => j.Value < i.Value))
                               .ToList();
            Card cardToPlay;
            switch(_g.GameType)
            {
                case Hra.Durch:
                    cardToPlay = minResults.Keys.OrderByDescending(i => winCount[i])
                                                .ThenByDescending(i => minResults[i])
                                                .ThenByDescending(i => averageResults[i])
                                                .ThenByDescending(i => maxResults[i])
                                                .ThenByDescending(i => i.BadValue)
                                                .FirstOrDefault();
                    break;
                case Hra.Betl:
                    cardToPlay = minResults.Keys.OrderByDescending(i => winCount[i])
                                                .ThenByDescending(i => minResults[i])
                                                .ThenByDescending(i => averageResults[i])
                                                .ThenByDescending(i => maxResults[i])
                                                .ThenBy(i => i.BadValue)
                                                .FirstOrDefault();
                    break;
                default:
                    cardToPlay = minResults.Keys.OrderByDescending(i => winCount[i])
                                                .ThenByDescending(i => minResults[i])
                                                .ThenByDescending(i => averageResults[i])
                                                .ThenByDescending(i => maxResults[i])
                                                .ThenBy(i => topCards.Contains(i) ? -(int)i.Value : (int)i.Value)
                                                .FirstOrDefault();
                    break;
            }
            DebugInfo.Card = cardToPlay;
            DebugInfo.Rule = "Uhrát co nejlepší výsledek";
            DebugInfo.RuleCount = cardToPlay != null ? winCount[cardToPlay] : 0;
            DebugInfo.TotalRuleCount = n;
            DebugInfo.AllChoices = minResults.Keys.Select(i => new RuleDebugInfo
            {
                Card = i,
                Rule = "Uhrát co nejlepší výsledek",
                RuleCount = winCount[i],
                TotalRuleCount = n
            }).ToArray();

            OnGameComputationProgress(new GameComputationProgressEventArgs { Current = estimatedCombinations, Max = estimatedCombinations, Message = "Generuju karty" });

            return cardToPlay;
        }

        private bool IsLikelyTalonForHand(Hand[] hands)
        {
            if (_talon != null && _talon.Any() && hands[Game.TalonIndex].Any(i => !_talon.Contains(i)))
            {
                return false;
            }

            if (Probabilities.CertainCards(Game.TalonIndex).Any() &&
                hands[Game.TalonIndex].All(i => !Probabilities.CertainCards(Game.TalonIndex).Contains(i)))
            {
                return false;
            }
            var gameStartedInitialCards = _g.rounds.Where(r => r != null && r.c3 != null)
                                                   .Select(r =>
                                                   {
                                                       if (r.player1.PlayerIndex == _g.GameStartingPlayerIndex)
                                                       {
                                                           return r.c1;
                                                       }
                                                       else if (r.player2.PlayerIndex == _g.GameStartingPlayerIndex)
                                                       {
                                                           return r.c2;
                                                       }
                                                       else
                                                       {
                                                           return r.c3;
                                                       }
                                                   }).ToList();

            var initialHand = gameStartedInitialCards.Concat((List<Card>)hands[_g.GameStartingPlayerIndex]).Concat((List<Card>)hands[Game.TalonIndex]).ToList();

            //if (hands[Game.TalonIndex].HasK(Barva.Zaludy) &&
            //    hands[Game.TalonIndex].HasJ(Barva.Zeleny) &&
            //    talon.HasJ(Barva.Zaludy) &&
            //    talon.HasJ(Barva.Zeleny))
            //{
            //    var x = !hands[_g.GameStartingPlayerIndex].Any(i => talon != null && talon.Contains(i));
            //    var y = hands[Game.TalonIndex].All(i => talon.Contains(i));
            //}

            var talonCandidates = initialHand.Where(i => (i.Suit != _g.TrumpCard.Suit &&
                                                          ((!initialHand.HasX(i.Suit) &&
                                                            initialHand.CardCount(i.Suit) - hands[Game.TalonIndex].CardCount(i.Suit) == 1) ||
                                                           !initialHand.HasSuit(i.Suit))) &&
                                                         (i.Value <= Hodnota.Spodek ||
                                                           (i.Value == Hodnota.Svrsek &&
                                                            !initialHand.HasK(i.Suit)) ||
                                                           (i.Value == Hodnota.Kral &&
                                                            !initialHand.HasQ(i.Suit) &&
                                                            (!initialHand.HasX(i.Suit) ||
                                                             initialHand.HasA(i.Suit)))));

            if ((_g.GameType & (Hra.Betl | Hra.Durch)) != 0 ||
                (((talonCandidates.Count() < 2 &&
                   talonCandidates.All(i => ((List<Card>)hands[Game.TalonIndex]).Contains(i))) ||
                  (talonCandidates.Count() >= 2 &&
                   hands[Game.TalonIndex].All(i => talonCandidates.Contains(i)))) &&
                 (!hands[Game.TalonIndex].HasSuit(_g.TrumpCard.Suit) ||
                  (hands[Game.TalonIndex].HasSuit(_g.TrumpCard.Suit) &&
                   initialHand.CardCount(_g.TrumpCard.Suit) >= 5 &&
                   initialHand.HasA(_g.TrumpCard.Suit) &&
                   ChooseNormalTalon(initialHand, _g.TrumpCard).HasSuit(_g.TrumpCard.Suit)))))
            {
                return true;
            }

            return false;
        }

        private IEnumerable<Tuple<Card, MoneyCalculatorBase, GameComputationResult>> ComputeMinMax(List<Round> previousRounds, Hand[] hands, int currentRound)
        {
            ThrowIfCancellationRequested();

            var previousRound = previousRounds.Last();
            var roundNumber = previousRounds.Count() + 1;
            var player1 = previousRound.roundWinner.PlayerIndex;
            var player2 = (player1 + 1) % Game.NumPlayers;
            var player3 = (player1 + 2) % Game.NumPlayers;
            var candidateRounds = new List<Round>();
            var results = new List<Tuple<Card, Card, Card, MoneyCalculatorBase, GameComputationResult>>();
            var cards1 = ValidCards(hands[player1], _g.trump, _g.GameType, _g.players[player1].TeamMateIndex);

            foreach (var c1 in cards1)
            {
                var cards2 = ValidCards(hands[player2], _g.trump, _g.GameType, _g.players[player2].TeamMateIndex, c1);

                foreach (var c2 in cards2)
                {
                    var cards3 = ValidCards(hands[player3], _g.trump, _g.GameType, _g.players[player3].TeamMateIndex, c1, c2);

                    foreach (var c3 in cards3)
                    {
                        var hlas1 = _g.trump.HasValue && c1.Value == Hodnota.Svrsek && hands[player1].HasK(c1.Suit);
                        var hlas2 = _g.trump.HasValue && c2.Value == Hodnota.Svrsek && hands[player2].HasK(c2.Suit);
                        var hlas3 = _g.trump.HasValue && c3.Value == Hodnota.Svrsek && hands[player3].HasK(c3.Suit);
                        var round = new Round(_g, _g.players[player1], c1, c2, c3, roundNumber, hlas1, hlas2, hlas3);

                        if (roundNumber == Game.NumRounds)
                        {
                            var result = InitGameComputationResult(
                                new[]
                                {
                                    new Hand(Enumerable.Empty<Card>()),
                                    new Hand(Enumerable.Empty<Card>()),
                                    new Hand(Enumerable.Empty<Card>()),
                                    new Hand(Enumerable.Empty<Card>())
                                });

                            //System.Diagnostics.Debug.WriteLine("\n*** Minimax ***");
                            foreach (var r in previousRounds.Concat(new[] { round }))
                            {
                                result.Rounds.Add(new RoundDebugContext
                                {
                                    c1 = r.c1,
                                    c2 = r.c2,
                                    c3 = r.c3,
                                    RoundStarterIndex = r.player1.PlayerIndex,
                                    RoundWinnerIndex = r.roundWinner.PlayerIndex
                                });
                                if (r.number >= currentRound)
                                {
                                    result.Score[r.player1.PlayerIndex] += r.points1;
                                    result.Score[r.player2.PlayerIndex] += r.points2;
                                    result.Score[r.player3.PlayerIndex] += r.points3;
                                    result.BasicScore[r.player1.PlayerIndex] += r.basicPoints1;
                                    result.BasicScore[r.player2.PlayerIndex] += r.basicPoints2;
                                    result.BasicScore[r.player3.PlayerIndex] += r.basicPoints3;
                                    result.MaxHlasScore[r.player1.PlayerIndex] = Math.Max(r.hlasPoints1, result.MaxHlasScore[r.player1.PlayerIndex]);
                                    result.MaxHlasScore[r.player2.PlayerIndex] = Math.Max(r.hlasPoints2, result.MaxHlasScore[r.player2.PlayerIndex]);
                                    result.MaxHlasScore[r.player3.PlayerIndex] = Math.Max(r.hlasPoints3, result.MaxHlasScore[r.player3.PlayerIndex]);
                                    result.TotalHlasScore[r.player1.PlayerIndex] += r.hlasPoints1;
                                    result.TotalHlasScore[r.player2.PlayerIndex] += r.hlasPoints2;
                                    result.TotalHlasScore[r.player3.PlayerIndex] += r.hlasPoints3;
                                    if (r.number == Game.NumRounds && _g.trump.HasValue)
                                    {
                                        var roundWinnerCard = Round.WinningCard(r.c1, r.c2, r.c3, _g.trump);

                                        result.Final7Won = roundWinnerCard.Suit == _g.trump.Value && roundWinnerCard.Value == Hodnota.Sedma && r.roundWinner.PlayerIndex == _g.GameStartingPlayerIndex;
                                        result.Final7AgainstWon = roundWinnerCard.Suit == _g.trump.Value && roundWinnerCard.Value == Hodnota.Sedma && r.roundWinner.PlayerIndex != _g.GameStartingPlayerIndex;
                                    }
                                }
                                //System.Diagnostics.Debug.WriteLine($"Round {r.number} Starter: {r.player1.PlayerIndex + 1}");
                                //System.Diagnostics.Debug.WriteLine($"{r.c1} {r.c2} {r.c3} {r.points1} {r.points2} {r.points3}");
                                //System.Diagnostics.Debug.WriteLine($"Winner: {r.roundWinner.PlayerIndex + 1} Score: {result.Score[0]} {result.Score[1]} {result.Score[2]}");
                            }
                            var calc = GetMoneyCalculator(_g.GameType, _g.trump, _g.GameStartingPlayerIndex, _g.Bidding, result);

                            calc.CalculateMoney();

                            return new[] { new Tuple<Card, MoneyCalculatorBase, GameComputationResult>(c1, calc, result) };
                        }
                        else
                        {
                            candidateRounds.Add(round);
                        }
                    }
                }
            }
            foreach(var r in candidateRounds)
            {
                var hh = new[] {
                        new Hand(((List<Card>)hands[0]).Where(i => i != (r.player1.PlayerIndex == 0 ? r.c1 : r.player2.PlayerIndex == 0 ? r.c2 : r.c3))),
                        new Hand(((List<Card>)hands[1]).Where(i => i != (r.player1.PlayerIndex == 1 ? r.c1 : r.player2.PlayerIndex == 1 ? r.c2 : r.c3))),
                        new Hand(((List<Card>)hands[2]).Where(i => i != (r.player1.PlayerIndex == 2 ? r.c1 : r.player2.PlayerIndex == 2 ? r.c2 : r.c3))),
                    };
                //var c = PlayerIndex == r.player1.PlayerIndex ? r.c1 : PlayerIndex == r.player2.PlayerIndex ? r.c2 : r.c3;
                var result = ComputeMinMax(previousRounds.Concat(new[] { r }).ToList(), hh, currentRound).First();

                results.Add(new Tuple<Card, Card, Card, MoneyCalculatorBase, GameComputationResult>(r.c1, r.c2, r.c3, result.Item2, result.Item3));
            }


            var filteredResults = new List<Tuple<Card, Card, Card, MoneyCalculatorBase, GameComputationResult>>();

            foreach(var c1 in results.Select(i => i.Item1).Distinct())
            {
                var filtered1 = new List<Tuple<Card, Card, Card, MoneyCalculatorBase, GameComputationResult>>();

                foreach (var c2 in results.Where(i => i.Item1 == c1).Select(i => i.Item2).Distinct())
                {
                    var filtered2 = new List<Tuple<Card, Card, Card, MoneyCalculatorBase, GameComputationResult>>();

                    foreach (var c3 in results.Where(i => i.Item1 == c1 &&
                                                          i.Item2 == c2).Select(i => i.Item3).Distinct())
                    {
                        var res3 = results.Single(i => i.Item1 == c1 && i.Item2 == c2 && i.Item3 == c3);

                        if (!filtered2.Any(i => i.Item1 == c1 &&
                                                i.Item2 == c2))
                        {
                            filtered2.Add(res3);
                            continue;
                        }
                        
                        var minmax3 = filtered2.Single(i => i.Item1 == c1 && i.Item2 == c2);

                        if (_g.players[player3].TeamMateIndex == -1)
                        {
                            if (res3.Item4.MoneyWon[player3] > minmax3.Item4.MoneyWon[player3] ||
                                (res3.Item4.MoneyWon[player3] == minmax3.Item4.MoneyWon[player3] &&
                                 (res3.Item4.BasicPointsWon > minmax3.Item4.BasicPointsWon ||
                                  (res3.Item4.BasicPointsWon == minmax3.Item4.BasicPointsWon &&
                                   _g.trump.HasValue &&
                                   minmax3.Item3.Suit == _g.trump.Value &&
                                   c3.Suit != _g.trump.Value))))
                            {
                                filtered2.Remove(minmax3);
                                filtered2.Add(res3);
                            }
                        }
                        else
                        {
                            if (res3.Item4.MoneyWon[player3] > minmax3.Item4.MoneyWon[player3] ||
                                (res3.Item4.MoneyWon[player3] == minmax3.Item4.MoneyWon[player3] &&
                                 (res3.Item4.BasicPointsLost > minmax3.Item4.BasicPointsLost ||
                                  (res3.Item4.BasicPointsLost == minmax3.Item4.BasicPointsLost &&
                                   _g.trump.HasValue &&
                                   minmax3.Item3.Suit == _g.trump.Value &&
                                   c3.Suit != _g.trump.Value))))
                            {
                                filtered2.Remove(minmax3);
                                filtered2.Add(res3);
                            }
                        }
                    }
                    var res2 = filtered2.First();

                    if (!filtered1.Any(i => i.Item1 == c1))
                    {
                        filtered1.Add(res2);
                        continue;
                    }

                    var minmax2 = filtered1.Single(i => i.Item1 == c1);

                    if (_g.players[player2].TeamMateIndex == -1)
                    {
                        if (res2.Item4.MoneyWon[player2] > minmax2.Item4.MoneyWon[player2] ||
                            (res2.Item4.MoneyWon[player2] == minmax2.Item4.MoneyWon[player2] &&
                             (res2.Item4.BasicPointsWon > minmax2.Item4.BasicPointsWon ||
                              (res2.Item4.BasicPointsWon == minmax2.Item4.BasicPointsWon &&
                               _g.trump.HasValue &&
                               minmax2.Item3.Suit == _g.trump.Value &&
                               c2.Suit != _g.trump.Value))))
                        {
                            filtered1.Remove(minmax2);
                            filtered1.Add(res2);
                        }
                    }
                    else
                    {
                        if (res2.Item4.MoneyWon[player2] > minmax2.Item4.MoneyWon[player2] ||
                            (res2.Item4.MoneyWon[player2] == minmax2.Item4.MoneyWon[player2] &&
                             (res2.Item4.BasicPointsLost > minmax2.Item4.BasicPointsLost ||
                              (res2.Item4.BasicPointsLost == minmax2.Item4.BasicPointsLost &&
                               _g.trump.HasValue &&
                               minmax2.Item3.Suit == _g.trump.Value &&
                               c2.Suit != _g.trump.Value))))
                        {
                            filtered1.Remove(minmax2);
                            filtered1.Add(res2);
                        }
                    }
                }
                var res1 = filtered1.First();

                if (roundNumber == currentRound ||
                    !filteredResults.Any())
                {
                    filteredResults.Add(res1);
                    continue;
                }

                var minmax1 = filteredResults.First();

                if (_g.players[player1].TeamMateIndex == -1)
                {
                    if (res1.Item4.MoneyWon[player1] > minmax1.Item4.MoneyWon[player1] ||
                        (res1.Item4.MoneyWon[player1] == minmax1.Item4.MoneyWon[player1] &&
                         (res1.Item4.BasicPointsWon > minmax1.Item4.BasicPointsWon ||
                          (res1.Item4.BasicPointsWon == minmax1.Item4.BasicPointsWon &&
                           _g.trump.HasValue &&
                           minmax1.Item3.Suit == _g.trump.Value &&
                           c1.Suit != _g.trump.Value))))
                    {
                        filteredResults.Remove(minmax1);
                        filteredResults.Add(res1);
                    }
                }
                else
                {
                    if (res1.Item4.MoneyWon[player1] > minmax1.Item4.MoneyWon[player1] ||
                        (res1.Item4.MoneyWon[player1] == minmax1.Item4.MoneyWon[player1] &&
                         (res1.Item4.BasicPointsLost > minmax1.Item4.BasicPointsLost ||
                          (res1.Item4.BasicPointsLost == minmax1.Item4.BasicPointsLost &&
                           _g.trump.HasValue &&
                           minmax1.Item3.Suit == _g.trump.Value &&
                           c1.Suit != _g.trump.Value))))
                    {
                        filteredResults.Remove(minmax1);
                        filteredResults.Add(res1);
                    }
                }
            }
            if (roundNumber != currentRound &&
                filteredResults.Count() > 1)
            {
                filteredResults = filteredResults.OrderByDescending(i => i.Item4.MoneyWon[player1])
                                                 .Take(1)
                                                 .ToList();
            }

            return filteredResults.Select(i => new Tuple<Card, MoneyCalculatorBase, GameComputationResult>(i.Item1, i.Item4, i.Item5)).ToList();
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
            if (cardRules.Any(i => i.Value.Any(j => j.SkipSimulations == true)) &&
                cardRules.Any(i => i.Value.Any(j => j.SkipSimulations == false)))
            {
                _log.InfoFormat("Rules have conflicting SkipSimulations flags");
                foreach (var key in cardRules.Keys)
                {
                    //nechame si jen karty s pravidly ktera maji SkipSimulations == true, takova pravidla jsou jistejsi
                    var temp = cardRules[key].Where(i => i.SkipSimulations == true);

                    if (temp.Any())
                    {
                        cardRules[key] = temp.ToList();
                    }
                    else
                    {
                        cardRules.Remove(key);
                    }
                }
                _log.InfoFormat("Rule count after cleanup: {0}", cardRules.Count);
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
                            DebugInfo.AiDebugInfo = kv.Value.First().AiDebugInfo;
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
                        DebugInfo.AiDebugInfo = kv.Value.First().AiDebugInfo;
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
                    DebugInfo.AiDebugInfo = kvp2.Value.Rule.AiDebugInfo;
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
                    //DebugInfo.AiDebugInfo = kv.Value.First().AiDebugInfo;
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
                Hands = new Hand[Game.NumPlayers + 1],
                Rounds = new List<RoundDebugContext>(),
                Score = new int[Game.NumPlayers],
                BasicScore = new int[Game.NumPlayers],
                MaxHlasScore = new int[Game.NumPlayers],
                TotalHlasScore = new int[Game.NumPlayers],
                Final7Won = null
            };

            for (var i = 0; i < Game.NumPlayers + 1; i++ )
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
                result.BasicScore[_g.rounds[i].player2.PlayerIndex] += _g.rounds[i].basicPoints2;
                result.BasicScore[_g.rounds[i].player3.PlayerIndex] += _g.rounds[i].basicPoints3;
                result.MaxHlasScore[_g.rounds[i].player1.PlayerIndex] = Math.Max(_g.rounds[i].hlasPoints1, result.MaxHlasScore[_g.rounds[i].player1.PlayerIndex]);
                result.MaxHlasScore[_g.rounds[i].player2.PlayerIndex] = Math.Max(_g.rounds[i].hlasPoints2, result.MaxHlasScore[_g.rounds[i].player2.PlayerIndex]);
                result.MaxHlasScore[_g.rounds[i].player3.PlayerIndex] = Math.Max(_g.rounds[i].hlasPoints3, result.MaxHlasScore[_g.rounds[i].player3.PlayerIndex]);
                result.TotalHlasScore[_g.rounds[i].player1.PlayerIndex] += _g.rounds[i].hlasPoints1;
                result.TotalHlasScore[_g.rounds[i].player2.PlayerIndex] += _g.rounds[i].hlasPoints2;
                result.TotalHlasScore[_g.rounds[i].player3.PlayerIndex] += _g.rounds[i].hlasPoints3;
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
                result.TotalHlasScore[roundStarterIndex] += hlas;
                result.MaxHlasScore[roundStarterIndex] = Math.Max(hlas, result.MaxHlasScore[roundStarterIndex]);
            }
            if (c2.Value == Hodnota.Svrsek && hands[(roundStarterIndex + 1) % Game.NumPlayers].HasK(c2.Suit))
            {
                var hlas = _g.trump.HasValue && c2.Suit == _g.trump.Value ? 40 : 20;
                result.Score[(roundStarterIndex + 1) % Game.NumPlayers] += hlas;
                result.TotalHlasScore[(roundStarterIndex + 1) % Game.NumPlayers] += hlas;
                result.MaxHlasScore[(roundStarterIndex + 1) % Game.NumPlayers] = Math.Max(hlas, result.MaxHlasScore[(roundStarterIndex + 1) % Game.NumPlayers]);
            }
            if (c3.Value == Hodnota.Svrsek && hands[(roundStarterIndex + 2) % Game.NumPlayers].HasK(c3.Suit))
            {
                var hlas = _g.trump.HasValue && c3.Suit == _g.trump.Value ? 40 : 20;
                result.Score[(roundStarterIndex + 2) % Game.NumPlayers] += hlas;
                result.TotalHlasScore[(roundStarterIndex + 2) % Game.NumPlayers] += hlas;
                result.MaxHlasScore[(roundStarterIndex + 2) % Game.NumPlayers] = Math.Max(hlas, result.MaxHlasScore[(roundStarterIndex + 2) % Game.NumPlayers]);
            }
            if (c1.Value == Hodnota.Kral && hands[roundStarterIndex].HasQ(c1.Suit))
            {
                var hlas = _g.trump.HasValue && c1.Suit == _g.trump.Value ? 40 : 20;
                result.Score[roundStarterIndex] += hlas;
                result.TotalHlasScore[roundStarterIndex] += hlas;
                result.MaxHlasScore[roundStarterIndex] = Math.Max(hlas, result.MaxHlasScore[roundStarterIndex]);
            }
            if (c2.Value == Hodnota.Kral && hands[(roundStarterIndex + 1) % Game.NumPlayers].HasQ(c2.Suit))
            {
                var hlas = _g.trump.HasValue && c2.Suit == _g.trump.Value ? 40 : 20;
                result.Score[(roundStarterIndex + 1) % Game.NumPlayers] += hlas;
                result.TotalHlasScore[(roundStarterIndex + 1) % Game.NumPlayers] += hlas;
                result.MaxHlasScore[(roundStarterIndex + 1) % Game.NumPlayers] = Math.Max(hlas, result.MaxHlasScore[(roundStarterIndex + 1) % Game.NumPlayers]);
            }
            if (c3.Value == Hodnota.Kral && hands[(roundStarterIndex + 2) % Game.NumPlayers].HasQ(c3.Suit))
            {
                var hlas = _g.trump.HasValue && c3.Suit == _g.trump.Value ? 40 : 20;
                result.Score[(roundStarterIndex + 2) % Game.NumPlayers] += hlas;
                result.TotalHlasScore[(roundStarterIndex + 2) % Game.NumPlayers] += hlas;
                result.MaxHlasScore[(roundStarterIndex + 2) % Game.NumPlayers] = Math.Max(hlas, result.MaxHlasScore[(roundStarterIndex + 2) % Game.NumPlayers]);
            }
        }

        private bool NoChanceToWinDurch(int gameStarterIndex, Hand[] hands)
        {
            try
            {
                var player2 = (gameStarterIndex + 1) % Game.NumPlayers;
                var player3 = (gameStarterIndex + 2) % Game.NumPlayers;

                return hands[player2].Any(i => hands[gameStarterIndex].All(j => !j.IsHigherThan(i, null))) ||
                       hands[player3].Any(i => hands[gameStarterIndex].All(j => !j.IsHigherThan(i, null)));
            }
            catch(Exception ex)
            {
                return true;
            }
        }

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
            var simRounds = new Round[Game.NumRounds];

            Check(hands);
            result.Trump = trump;
            result.GameType = gameType ?? _g.GameType;
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
                    //simuluju vlastni betl/durch (jestli ma cenu hlasit spatnou barvu) nebo pri samotne hre
                    player1 = PlayerIndex;
                    player2 = (PlayerIndex + 1) % Game.NumPlayers;
                    player3 = (PlayerIndex + 2) % Game.NumPlayers;
                    playerName = _g.players[player1].Name;
                    playerIndex = player1;
                    teamMateIndex = ImpersonateGameStartingPlayer ?  -1 : TeamMateIndex;
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
            if (!trump.HasValue && (gameType & (Hra.Betl | Hra.Durch)) == 0)
            {
                trump = _g.trump;
                if (!trump.HasValue && (gameType & (Hra.Betl | Hra.Durch)) == 0)
                {
                    throw new InvalidOperationException("AiPlayer: trump is null");
                }
            }

            var prob = Probabilities.Clone();
            if (Settings.Cheat)                              //all probabilities are based on generated hands (either 0 or 1)
            {
                prob.Set(hands);
            }
            //prob.UpdateProbabilitiesAfterTalon((List<Card>)hands[player1], (List<Card>)hands[3]);
            //prob.UseDebugString = false;    //otherwise we are being really slooow

            var prob1 = 0 == PlayerIndex && !ImpersonateGameStartingPlayer ? prob : new Probability(0, _g.RoundNumber == 0 ? player1 : _g.GameStartingPlayerIndex, hands[0], trump, _g.AllowFakeSeven || _g.AllowFake107, _g.AllowAXTalon, _g.AllowTrumpTalon, _g.CancellationToken, _stringLoggerFactory, 0 == PlayerIndex ? (List<Card>)hands[Game.TalonIndex] : null);
            prob1.UseDebugString = false;
            var prob2 = 1 == PlayerIndex && !ImpersonateGameStartingPlayer ? prob : new Probability(1, _g.RoundNumber == 0 ? player1 : _g.GameStartingPlayerIndex, hands[1], trump, _g.AllowFakeSeven || _g.AllowFake107, _g.AllowAXTalon, _g.AllowTrumpTalon, _g.CancellationToken, _stringLoggerFactory, 1 == PlayerIndex ? (List<Card>)hands[Game.TalonIndex] : null);
            prob2.UseDebugString = false;
            var prob3 = 2 == PlayerIndex && !ImpersonateGameStartingPlayer ? prob : new Probability(2, _g.RoundNumber == 0 ? player1 : _g.GameStartingPlayerIndex, hands[2], trump, _g.AllowFakeSeven || _g.AllowFake107, _g.AllowAXTalon, _g.AllowTrumpTalon, _g.CancellationToken, _stringLoggerFactory, 2 == PlayerIndex ? (List<Card>)hands[Game.TalonIndex] : null);
            prob3.UseDebugString = false;

            if (Settings.Cheat)
            {
                prob1.Set(hands);
                prob2.Set(hands);
                prob3.Set(hands);
            }
            var teamMatesSuits = new List<Barva>(_teamMatesSuits);
            //var aiStrategy = AiStrategyFactory.GetAiStrategy(_g, gameType, trump, hands, _g.RoundNumber >= 1 ? _g.rounds : simRounds, teamMatesSuits,
            //    prob, playerName, playerIndex, teamMateIndex, initialRoundNumber,
            //    Settings.RiskFactor, Settings.RiskFactorSevenDefense, Settings.SolitaryXThreshold, Settings.SolitaryXThresholdDefense);

            var teamMateIndex1 = ImpersonateGameStartingPlayer
                                    ? (0 == PlayerIndex ? -1 : 1 == PlayerIndex ? 2 : 1)
                                    : _g.players[0].TeamMateIndex;
            var teamMateIndex2 = ImpersonateGameStartingPlayer
                                    ? (1 == PlayerIndex ? -1 : 0 == PlayerIndex ? 2 : 0)
                                    : _g.players[1].TeamMateIndex;
            var teamMateIndex3 = ImpersonateGameStartingPlayer
                                    ? (2 == PlayerIndex ? -1 : 0 == PlayerIndex ? 1 : 0)
                                    : _g.players[2].TeamMateIndex;
            var aiStrategy1 = AiStrategyFactory.GetAiStrategy(_g, gameType, trump, hands, _g.RoundNumber >= 1 ? _g.rounds : simRounds,
                0 == PlayerIndex ? teamMatesSuits : new List<Barva>(), prob1, _g.players[0].Name, 0, teamMateIndex1, initialRoundNumber,
                Settings.RiskFactor, Settings.RiskFactorSevenDefense, Settings.SolitaryXThreshold, Settings.SolitaryXThresholdDefense,
                _g.Bidding, _g.GameValue, _g.HundredValue, _g.SevenValue);

            var aiStrategy2 = AiStrategyFactory.GetAiStrategy(_g, gameType, trump, hands, _g.RoundNumber >= 1 ? _g.rounds : simRounds,
                1 == PlayerIndex ? teamMatesSuits : new List<Barva>(), prob2, _g.players[1].Name, 1, teamMateIndex2, initialRoundNumber,
                Settings.RiskFactor, Settings.RiskFactorSevenDefense, Settings.SolitaryXThreshold, Settings.SolitaryXThresholdDefense,
                _g.Bidding, _g.GameValue, _g.HundredValue, _g.SevenValue);

            var aiStrategy3 = AiStrategyFactory.GetAiStrategy(_g, gameType, trump, hands, _g.RoundNumber >= 1 ? _g.rounds : simRounds,
                2 == PlayerIndex ? teamMatesSuits : new List<Barva>(), prob3, _g.players[2].Name, 2, teamMateIndex3, initialRoundNumber,
                Settings.RiskFactor, Settings.RiskFactorSevenDefense, Settings.SolitaryXThreshold, Settings.SolitaryXThresholdDefense,
                _g.Bidding, _g.GameValue, _g.HundredValue, _g.SevenValue);
            var aiStrategies = new[] { aiStrategy1, aiStrategy2, aiStrategy3 };
            var aiStrategy = aiStrategies[player1];

            _log.DebugFormat("Round {0}. Starting simulation for {1}", _g.RoundNumber, _g.players[PlayerIndex].Name);
            if (c1 != null) _log.DebugFormat("First card: {0}", c1);
            if (c2 != null) _log.DebugFormat("Second card: {0}", c2);
            _log.DebugFormat("{0}: {1} cerveny, {2} zeleny, {3} kule, {4} zaludy", _g.players[player2].Name, hands[player2].Count(i => i.Suit == Barva.Cerveny), hands[player2].Count(i => i.Suit == Barva.Zeleny), hands[player2].Count(i => i.Suit == Barva.Kule), hands[player2].Count(i => i.Suit == Barva.Zaludy));
            _log.DebugFormat("{0}: {1} cerveny, {2} zeleny, {3} kule, {4} zaludy", _g.players[player3].Name, hands[player3].Count(i => i.Suit == Barva.Cerveny), hands[player3].Count(i => i.Suit == Barva.Zeleny), hands[player3].Count(i => i.Suit == Barva.Kule), hands[player3].Count(i => i.Suit == Barva.Zaludy));
            for (initialRoundNumber = aiStrategy.RoundNumber;
                 aiStrategy.RoundNumber < initialRoundNumber + roundsToCompute;
                 //aiStrategy.RoundNumber++)
                 aiStrategy1.RoundNumber++, aiStrategy2.RoundNumber++, aiStrategy3.RoundNumber++)
            {
                if (aiStrategy.RoundNumber > 10) break;

                var roundStarterIndex = player1;
                AiRule r1 = null, r2 = null, r3 = null;
                Dictionary<AiRule, Card> ruleDictionary;

                if (!firstTime || c1 == null)
                {
                    aiStrategy = aiStrategies[player1];
                    ruleDictionary = aiStrategy.GetApplicableRules();

                    r1 = ruleDictionary.Keys.OrderBy(i => i.Order).FirstOrDefault();
                    c1 = ruleDictionary.OrderBy(i => i.Key.Order).Select(i => i.Value).FirstOrDefault();

                    //aiStrategy.MyIndex = player2;
                    //aiStrategy.TeamMateIndex = aiStrategy.TeamMateIndex == player2 ? player1 : (aiStrategy.TeamMateIndex == -1 ? player3 : -1);
                    if (firstTime)
                    {
                        result.CardToPlay = c1;
                        result.Rule = r1;
                        if ((_gameType & (Hra.Durch | Hra.Betl)) == 0 &&
                            (aiStrategy as AiStrategy)._bannedSuits.Any())
                        {
                            result.Rule.AiDebugInfo = "\nZakázané barvy: " + string.Join(" ", (aiStrategy as AiStrategy)._bannedSuits);
                        }
                        result.ToplevelRuleDictionary = ruleDictionary;
                        firstTime = false;
                    }
                }
                if (!firstTime || c2 == null)
                {
                    aiStrategy = aiStrategies[player2];
                    ruleDictionary = aiStrategy.GetApplicableRules2(c1);

                    r2 = ruleDictionary.Keys.OrderBy(i => i.Order).FirstOrDefault();
                    c2 = ruleDictionary.OrderBy(i => i.Key.Order).Select(i => i.Value).FirstOrDefault();

                    //aiStrategy.MyIndex = player3;
                    //aiStrategy.TeamMateIndex = aiStrategy.TeamMateIndex == player3 ? player2 : (aiStrategy.TeamMateIndex == -1 ? player1 : -1);
                    if (firstTime)
                    {
                        result.CardToPlay = c2;
                        result.Rule = r2;
                        result.ToplevelRuleDictionary = ruleDictionary;
                        firstTime = false;
                    }
                }
                aiStrategy = aiStrategies[player3];
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
                if (_g.RoundNumber == 0)
                {
                    simRounds[aiStrategy.RoundNumber - 1] = new Round(_g.players, trump, roundStarterIndex, c1, c2, c3, aiStrategy.RoundNumber);
                }
                _log.TraceFormat("{0}: {1}, {2}: {3}, {4}: {5}", _g.players[player1].Name, c1, _g.players[player2].Name, c2, _g.players[player3].Name, c3);
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
                    result.Final7AgainstWon = roundWinnerCard.Suit == trump.Value && roundWinnerCard.Value == Hodnota.Sedma && roundWinnerIndex != _g.GameStartingPlayerIndex;
                }
                hands[player1].Remove(c1);
                hands[player2].Remove(c2);
                hands[player3].Remove(c3);
                Check(hands);
                var hlas1 = _trump.HasValue && c1.Value == Hodnota.Svrsek && hands[player1].HasK(c1.Suit);
                var hlas2 = _trump.HasValue && c2.Value == Hodnota.Svrsek && hands[player2].HasK(c2.Suit);
                var hlas3 = _trump.HasValue && c3.Value == Hodnota.Svrsek && hands[player3].HasK(c3.Suit);
                if (Settings.Cheat)
                {
                    //prob.Set(hands);
                    prob1.Set(hands);
                    prob2.Set(hands);
                    prob3.Set(hands);
                }
                else
                {
                    //UpdateProbabilitiesAfterCardPlayed(prob, aiStrategy.RoundNumber, roundStarterIndex, c1, null, null, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _teamMateDoubledGame);
                    //UpdateProbabilitiesAfterCardPlayed(prob, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, null, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _teamMateDoubledGame);
                    //UpdateProbabilitiesAfterCardPlayed(prob, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, c3, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _teamMateDoubledGame);

                    var gameWinningRound1 = IsGameWinningRound(simRounds[aiStrategy.RoundNumber - 1], simRounds, 0, teamMateIndex1, hands[0], prob1);
                    var gameWinningRound2 = IsGameWinningRound(simRounds[aiStrategy.RoundNumber - 1], simRounds, 0, teamMateIndex2, hands[1], prob2);
                    var gameWinningRound3 = IsGameWinningRound(simRounds[aiStrategy.RoundNumber - 1], simRounds, 0, teamMateIndex3, hands[2], prob3);

                    UpdateProbabilitiesAfterCardPlayed(prob1, aiStrategy.RoundNumber, roundStarterIndex, c1, null, null, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame, gameWinningRound1);
                    UpdateProbabilitiesAfterCardPlayed(prob1, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, null, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame, gameWinningRound1);
                    UpdateProbabilitiesAfterCardPlayed(prob1, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, c3, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame, gameWinningRound1);

                    UpdateProbabilitiesAfterCardPlayed(prob2, aiStrategy.RoundNumber, roundStarterIndex, c1, null, null, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame, gameWinningRound2);
                    UpdateProbabilitiesAfterCardPlayed(prob2, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, null, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame, gameWinningRound2);
                    UpdateProbabilitiesAfterCardPlayed(prob2, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, c3, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame, gameWinningRound2);

                    UpdateProbabilitiesAfterCardPlayed(prob3, aiStrategy.RoundNumber, roundStarterIndex, c1, null, null, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame, gameWinningRound3);
                    UpdateProbabilitiesAfterCardPlayed(prob3, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, null, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame, gameWinningRound3);
                    UpdateProbabilitiesAfterCardPlayed(prob3, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, c3, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame, gameWinningRound3);
                }
                //aiStrategy.MyIndex = roundWinnerIndex;
                //aiStrategy.TeamMateIndex = _g.players[roundWinnerIndex].TeamMateIndex;
                //player1 = aiStrategy.MyIndex;
                //player2 = (aiStrategy.MyIndex + 1) % Game.NumPlayers;
                //player3 = (aiStrategy.MyIndex + 2) % Game.NumPlayers;
                player1 = roundWinnerIndex;
                player2 = (roundWinnerIndex + 1) % Game.NumPlayers;
                player3 = (roundWinnerIndex + 2) % Game.NumPlayers;
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
                throw new InvalidOperationException(string.Format("Wrong hands count for player{0}: {1} {2} {3}\nGame says: {4} {5} {6} {7}\nDebug string:\n{8}\nVerbose:\n{9}\nExternal:\n{10}\n---\n", 
                                                                  PlayerIndex + 1,
                                                                  hands[0].ToString(),
                                                                  hands[1].ToString(),
                                                                  hands[2].ToString(),
                                                                  _g.players[0].Hand.ToString(),
                                                                  _g.players[1].Hand.ToString(),
                                                                  _g.players[2].Hand.ToString(),
                                                                  _g.talon != null ? _g.talon.ToString() : "-",
                                                                  Probabilities._debugString,
                                                                  Probabilities._verboseString,
                                                                  Probabilities.ExternalDebugString));
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
