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
                SafetyGameThreshold = 80,
                SafetyHundredThreshold = 80,
                SafetyBetlThreshold = 64
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
            }).Where(i => !bannedSuits.Contains(i.Item1.Suit) && i.Item4 > 0);

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
                //pokud mas nejake vyssi plonky, tak pouzij radsi ty
                if (temp.Count > 1)
                {
                    var higherSoloCards = holesByCard.Where(i => !talon.Contains(i.Item1) &&
                                                                 i.Item2 == 1 &&    //CardCount
                                                                 temp.Any(j => j.BadValue < i.Item1.BadValue))
                                                     .Select(i => i.Item1)          //Card
                                                     .ToList();
                    if (higherSoloCards.Any())
                    {
                        temp = temp.Concat(higherSoloCards)
                                   .OrderByDescending(i => i.BadValue)
                                   .Take(2 - talon.Count)
                                   .ToList();
                        talon.AddRange(temp);
                    }
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
                var topHoleDeltaPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                              .ToDictionary(b => b, b => holesByCard.Where(i => i.Item1.Suit == b)
                                                                                    .OrderByDescending(i => i.Item1.BadValue)
                                                                                    .Select(i => i.Item3)
                                                                                    .FirstOrDefault());

                talon.AddRange(holesByCard.Where(i => i.Item2 < 7 &&  !talon.Contains(i.Item1))		//Card
				               		   	  .OrderByDescending(i => topHoleDeltaPerSuit[i.Item1.Suit])//i.Item3)			//holesDelta
				               			  .ThenByDescending(i => i.Item4)			//holes
									   	  .ThenByDescending(i => i.Item1.BadValue)
									   	  .Take(2 - talon.Count)
									   	  .Select(i => i.Item1)						//Card
									   	  .ToList());
			}
            
            //pokud je potreba, doplnime o nejake nizke karty (abych zhorsil talon na durcha)
            if(talon.Count < 2)
            {
                talon.AddRange(hand.Where(i => !bannedSuits.Contains(i.Suit) &&
                                               !talon.Contains(i))
                                   .OrderBy(i => i.BadValue)
                                   .Take(2 - talon.Count));
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
            //nejdriv zkus vzit karty v barve kde krom esa mam max 2 plivy (a nemam hlasku)
            talon.AddRange(hand.Where(i => i.Suit != trumpCard.Suit &&             //nevybirej trumfy
                                           i.Value != Hodnota.Eso &&               //ani esa
                                           hand.Count(j => i.Suit == j.Suit &&     //max. 2 karty jine nez A
                                                           j.Value != Hodnota.Eso) <= 2 &&
                                           !hand.HasX(i.Suit) &&                   //v barve kde neznam X ani nemam hlas
                                           !(hand.HasK(i.Suit) && hand.HasQ(i.Suit)))
                               .OrderBy(i => hand.CardCount(i.Suit))    //vybirej od nejkratsich barev
                               .ThenBy(i => hand.Count(j => j.Suit == i.Suit &&    //v pripade stejne delky barev
                                                            j.Value == Hodnota.Eso))             //dej prednost barve s esem
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
                                           !hand.HasX(i.Suit) &&
                                           !(hand.HasK(i.Suit) && hand.HasQ(i.Suit)))
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
                                           .Any(b => hand.CardCount(b) >= 5))
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
                                             hand.CardCount(i.Suit) == 3))
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

            //nakonec cokoli co je podle pravidel
            talon.AddRange(hand.Where(i => !(i.Value == trumpCard.Value &&         //nevybirej trumfovou kartu
                                             i.Suit == trumpCard.Suit) &&
                                           Game.IsValidTalonCard(i.Value, i.Suit, _trumpCard.Suit, _g.AllowAXTalon, _g.AllowTrumpTalon))
                               .OrderByDescending(i => i.Suit == trumpCard.Suit
                                                       ? -1 : (int)trumpCard.Suit)//nejdriv zkus jine nez trumfy
                               .ThenBy(i => i.Value));                            //vybirej od nejmensich karet

            talon = talon.Distinct().ToList();

            //pokud to vypada na sedmu se 4 kartama, tak se snaz mit vsechny barvy na ruce
            if (hand.Has7(trumpCard.Suit) &&
                hand.CardCount(trumpCard.Suit) == 4 &&
                hand.Select(i => i.Suit).Distinct().Count() == Game.NumSuits &&
                hand.Where(i => !talon.Take(2).Contains(i))
                    .Select(i => i.Suit).Distinct().Count() < Game.NumSuits)
            {
                var topCardPerSuit = hand.Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                         .Where(h => h > i.Value)
                                                         .All(h => !hand.Contains(new Card(i.Suit, h))))
                                         .ToDictionary(k => k.Suit, v => new { TopCard = v, SuitCount = hand.CardCount(v.Suit) });

                //u barvy kde mam dve karty se snaz si nechat tu nizsi
                var reducedTalon = talon.Where(i => topCardPerSuit[i.Suit].SuitCount > 2 ||
                                                    (topCardPerSuit[i.Suit].SuitCount == 2 &&
                                                     topCardPerSuit[i.Suit].TopCard != i))
                                        .ToList();
                //pouze pokud mi po odebrani v talonu zustane dost karet
                if (reducedTalon.Count >= 2 &&
                    !IsSevenTooRisky(hand.Where(i => !reducedTalon.Take(2).Contains(i)).ToList()))  //reducedTalon.Take(2) je zjednoduseni, nize muzu nekdy vybrat i jine karty
                {
                    talon = reducedTalon;
                }
            }
            //dej trumf do talonu pokud bys jinak prisel o hlas
            if (_g.AllowTrumpTalon &&
                hand.CardCount(trumpCard.Suit) >= 5 &&
                talon.Count > 2 &&                
                talon.Count(i => i.Suit != trumpCard.Suit &&
                                 (i.Value < Hodnota.Svrsek ||
                                  i.Value > Hodnota.Kral ||
                                  (i.Value == Hodnota.Kral &&
                                   !hand.HasQ(i.Suit)) ||
                                  (i.Value == Hodnota.Svrsek &&
                                   !hand.HasK(i.Suit)))) <= 1)
            {
                //vezmi nizke karty nebo K, S od netrumfove barvy pokud v barve nemam hlasku
                var trumpTalon = talon.Where(i => i.Value < Hodnota.Svrsek ||
                                                  (i.Suit != trumpCard.Suit &&
                                                   (i.Value > Hodnota.Kral ||
                                                    (i.Value == Hodnota.Kral &&
                                                     !hand.HasQ(i.Suit)) ||
                                                    (i.Value == Hodnota.Svrsek &&
                                                     !hand.HasK(i.Suit)))))
                                      .OrderBy(i => i.Suit == trumpCard.Suit
                                                     ? 1 : 0)
                                      .ThenBy(i => i.Value)
                                      .ToList();
                //pokud mam trumfovou sedmu, tak dam do talonu druhy nejnizsi trumf
                if (talon.Has7(trumpCard.Suit) &&
                    talon.CardCount(trumpCard.Suit) > 1)
                {
                    trumpTalon = trumpTalon.Where(i => i.Suit != trumpCard.Suit ||
                                                       i.Value != Hodnota.Sedma)
                                           .OrderBy(i => i.Value)
                                           .ToList();
                }
                if (trumpTalon.Count >= 2)
                {
                    talon = trumpTalon;
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
                    replacementCards.AddRange(hand.Where(i => cardToRemove.Suit == i.Suit)                                           
                                                  .OrderBy(i => i.Value)
                                                  .Take(Math.Min(2, hand.Count(i => i.Suit == cardToRemove.Suit &&
                                                                                    i.Value < Hodnota.Desitka) - 1)));
                    talon.Remove(cardToRemove);
                }
                replacementCards = replacementCards.Distinct().Take(cardsToRemove.Count).ToList();
                talon = replacementCards.Concat(talon).Distinct().ToList();
            }
            //pokud je v talonu neco vyssiho nez spodek a mas i spodka, tak ho dej do talonu
            foreach(var card in talon.Where(i => i.Suit != trumpCard.Suit &&
                                                 i.Value > Hodnota.Spodek &&
                                                 hand.HasJ(i.Suit)).ToList())
            {
                if (!talon.HasJ(card.Suit) ||
                    (talon.HasJ(card.Suit) &&
                     talon.CardCount(card.Suit) > 2))
                {
                    talon.Remove(card);
                }
                if (!talon.HasJ(card.Suit))
                {
                    talon.Insert(0, new Card(card.Suit, Hodnota.Spodek));
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
                                                    _g.AllowFakeSeven, _g.AllowAXTalon, _g.AllowTrumpTalon,
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
                                               _g.AllowFakeSeven, _g.AllowAXTalon, _g.AllowTrumpTalon,
                                               _g.CancellationToken, _stringLoggerFactory, _talon)
                {
                    ExternalDebugString = _debugString,
                    UseDebugString = true
                };

                return flavour;
            }
            //zpocitej ztraty v pripade kila proti
            var lossPerPointsLost = Enumerable.Range(0, 10)
                                              .ToDictionary(k => 100 + k * 10,
                                                            v => (_g.CalculationStyle == CalculationStyle.Adding
                                                                  ? (v + 1) * _g.HundredValue
                                                                  : _g.HundredValue * (1 << v)) *
                                                                 (PlayerIndex == _g.GameStartingPlayerIndex &&
                                                                  TrumpCard.Suit == Barva.Cerveny
                                                                  ? 2
                                                                  : 1));
            var tempTalon = Hand.Count == 12 ? ChooseNormalTalon(Hand, TrumpCard) : new List<Card>();
            var tempHand = new List<Card>(Hand.Where(i => !tempTalon.Contains(i)));
            var estimatedPointsLost = _trump != null ? EstimateTotalPointsLost(tempHand, tempTalon) : 0;
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
                                                _g.AllowFakeSeven, _g.AllowAXTalon, _g.AllowTrumpTalon,
                                                _g.CancellationToken, _stringLoggerFactory, _talon)
				{
					ExternalDebugString = _debugString
				};

                if (PlayerIndex == _g.OriginalGameStartingPlayerIndex && bidding.BetlDurchMultiplier == 0)
                {
                    //Sjedeme simulaci hry, betlu, durcha i normalni hry a vratit talon pro to nejlepsi. 
                    //Zapamatujeme si vysledek a pouzijeme ho i v ChooseGameFlavour() a ChooseGameType()
                    RunGameSimulations(bidding, _g.GameStartingPlayerIndex, true, true);
                    if (Settings.CanPlayGameType[Hra.Durch] && 
                        _durchBalance >= Settings.GameThresholdsForGameType[Hra.Durch][0] * _durchSimulations && 
                        _durchSimulations > 0 &&
                        !(_hundredOverDurch &&
                          !IsHundredTooRisky(tempHand)))
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
                                 !IsHundredTooRisky(tempHand))) ||
                              (Settings.SafetyBetlThreshold > 0 &&
                               !AdvisorMode &&
                               !Settings.AiMayGiveUp &&
                               ((lossPerPointsLost.ContainsKey(estimatedPointsLost) &&
                                 lossPerPointsLost[estimatedPointsLost] >= Settings.SafetyBetlThreshold) ||
                                (_maxMoneyLost <= -Settings.SafetyBetlThreshold &&
                                 _avgBasicPointsLost > 60)))))  //utec na betla pokud nemas na ruce nic a hrozi kilo proti
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
                var durchThresholdIndex = PlayerIndex == _g.GameStartingPlayerIndex ? 0 : Math.Min(Settings.GameThresholdsForGameType[Hra.Durch].Length - 1, 1);    //85%
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
                          !IsHundredTooRisky(tempHand))))) ||
                       (Settings.SafetyBetlThreshold > 0 &&
                        !AdvisorMode &&
                        !Settings.AiMayGiveUp &&
                        lossPerPointsLost.ContainsKey(estimatedPointsLost) &&
                        lossPerPointsLost[estimatedPointsLost] >= Settings.SafetyBetlThreshold) ||
                       (_maxMoneyLost <= -Settings.SafetyBetlThreshold &&
                        _avgBasicPointsLost > 60)))
                {
                    if ((_betlSimulations > 0 && 
                         (!Settings.CanPlayGameType[Hra.Durch] ||
                          _durchSimulations == 0 || 
                          (float)_betlBalance / (float)_betlSimulations > (float)_durchBalance / (float)_durchSimulations)) ||
                        (Settings.SafetyBetlThreshold > 0 &&
                         lossPerPointsLost.ContainsKey(estimatedPointsLost) &&
                         lossPerPointsLost[estimatedPointsLost] >= Settings.SafetyBetlThreshold))
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
                return GameFlavour.Good;
            }
        }

        private void UpdateGeneratedHandsByChoosingTalon(Hand[] hands, Func<List<Card>, Card, List<Card>> chooseTalonFunc, int GameStartingPlayerIndex)
        {
            const int talonIndex = 3;

            //volicimu hraci dame i to co je v talonu, aby mohl vybrat skutecny talon
            hands[GameStartingPlayerIndex].AddRange(hands[talonIndex]);

            var talon = chooseTalonFunc(hands[GameStartingPlayerIndex], TrumpCard);

            hands[GameStartingPlayerIndex].RemoveAll(i => talon.Contains(i));
            hands[talonIndex] = new Hand(talon);
        }

        private void UpdateGeneratedHandsByChoosingTrumpAndTalon(Hand[] hands, Func<List<Card>, Card, List<Card>> chooseTalonFunc, int GameStartingPlayerIndex)
        {
            const int talonIndex = 3;

            //volicimu hraci dame i to co je v talonu, aby mohl vybrat skutecny talon
            hands[GameStartingPlayerIndex].AddRange(hands[talonIndex]);

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
                        Parallel.ForEach(source ?? Probabilities.GenerateHands(1, gameStartingPlayerIndex, Settings.SimulationsPerGameType), options, (hh, loopState) =>
                        //foreach (var hh in source ?? Probabilities.GenerateHands(1, gameStartingPlayerIndex, Settings.SimulationsPerGameType))
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

                                    var gameComputationResult = ComputeGame(hands, null, null, _trump ?? _g.trump, _gameType != null ? (_gameType | Hra.SedmaProti) : (Hra.Hra | Hra.SedmaProti), 10, 1);
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
                                        var gt = !IsHundredTooRisky(hands[PlayerIndex]) &&
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
                            Parallel.ForEach(source ?? Probabilities.GenerateHands(1, gameStartingPlayerIndex, Settings.SimulationsPerGameType), options, (hh, loopState) =>
                            //foreach (var hh in source ?? Probabilities.GenerateHands(1, gameStartingPlayerIndex, Settings.SimulationsPerGameType))
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
				totalGameSimulations = (simulateGoodGames ? Settings.SimulationsPerGameType : 0) +
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
                        Parallel.ForEach(source ?? Probabilities.GenerateHands(1, gameStartingPlayerIndex, Settings.SimulationsPerGameType), (hands, loopState) =>
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
                                    {   //pokud jsem volil ja tak v UpdateGeneratedHandsByChoosingTalon() pouziju skutecne zvoleny trumf
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
                            if ((DateTime.Now - start).TotalMilliseconds > Settings.MaxSimulationTimeMs)
                            {
                                Probabilities.StopGeneratingHands();
                                loopState.Stop();
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
            _maxMoneyLost = moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0)
                                             .DefaultIfEmpty()
                                             .Min(i => i?.MoneyWon?[PlayerIndex] ?? 0);
            _hundredOverBetl = _avgWinForHundred >= 2 * _g.BetlValue;
            _hundredOverDurch = _avgWinForHundred >= 2 * _g.DurchValue;
            _gamesBalance = PlayerIndex == gameStartingPlayerIndex
                            ? moneyCalculations.Where(i => (i.GameType & Hra.Hra) != 0 &&
                                                           (i.GameType & Hra.Sedma) == 0).Count(i => i.GameWon)
                            : moneyCalculations.Where(i => (i.GameType & (Hra.Betl | Hra.Durch)) == 0).Count(i => !i.GameWon);
            _gameSimulations = PlayerIndex == gameStartingPlayerIndex
                                ? moneyCalculations.Count(i => (i.GameType & Hra.Hra) != 0 &&
                                                               (i.GameType & Hra.Sedma) == 0)
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
            _runSimulations = false;
            GC.Collect();
        }

        private MoneyCalculatorBase GetMoneyCalculator(Hra gameType, Barva? trump, int gameStartingPlayerIndex, Bidding bidding, GameComputationResult result)
        {
            switch (_g.CalculationStyle)
            {
                case CalculationStyle.Multiplying:
                    return new MultiplyingMoneyCalculator(gameType, trump, gameStartingPlayerIndex, bidding, _g, _g.Calculate107Separately, _g.HlasConsidered, result);
                case CalculationStyle.Adding:
                default:
                    return new AddingMoneyCalculator(gameType, trump, gameStartingPlayerIndex, bidding, _g, _g.Calculate107Separately, _g.HlasConsidered, result);
            }
        }

        public string GetComputationResultString(//string filename, 
                                            GameComputationResult result, MoneyCalculatorBase money)
        {
            //using (var fs = _g.GetFileStream(filename))
            using(var fs = new MemoryStream())
            {
                var gameDto = new GameDto
                {
                    Kolo = 10,
                    Voli = (Hrac)_g.GameStartingPlayerIndex,
                    Trumf = result.Trump,
                    Hrac1 = ((List<Card>)result.Hands[0])
                                    .Select(i => new Karta
                                    {
                                        Barva = i.Suit,
                                        Hodnota = i.Value
                                    }).ToArray(),
                    Hrac2 = ((List<Card>)result.Hands[1])
                                    .Select(i => new Karta
                                    {
                                        Barva = i.Suit,
                                        Hodnota = i.Value
                                    }).ToArray(),
                    Hrac3 = ((List<Card>)result.Hands[2])
                                    .Select(i => new Karta
                                    {
                                        Barva = i.Suit,
                                        Hodnota = i.Value
                                    }).ToArray(),
                    //Fleky = fleky,
                    Stychy = result.Rounds
                                   .Select((r, idx) => new Stych
                                    {
                                        Kolo = idx + 1,
                                        Zacina = (Hrac)r.RoundStarterIndex
                                    }).ToArray(),
                    //Talon = talon
                    //                .Select(i => new Karta
                    //                {
                    //                    Barva = i.Suit,
                    //                    Hodnota = i.Value
                    //                }).ToArray()
                };
                try
                {
                    foreach (var stych in gameDto.Stychy)
                    {
                        var r = result.Rounds[stych.Kolo - 1];
                        var cards = new[] { r.c1, r.c2, r.c3 };
                        var debugInfo = new[] { r.r1, r.r2, r.r3 };
                        var playerIndices = new[] { r.RoundStarterIndex, (r.RoundStarterIndex + 1) % Game.NumPlayers, (r.RoundStarterIndex + 2) % Game.NumPlayers };
                        int index = Array.IndexOf(playerIndices, 0);
                        stych.Hrac1 = new Karta
                        {
                            Barva = cards[index].Suit,
                            Hodnota = cards[index].Value,
                            Poznamka = debugInfo[index]
                        };
                        index = Array.IndexOf(playerIndices, 1);
                        stych.Hrac2 = new Karta
                        {
                            Barva = cards[index].Suit,
                            Hodnota = cards[index].Value,
                            Poznamka = debugInfo[index]
                        };
                        index = Array.IndexOf(playerIndices, 2);
                        stych.Hrac3 = new Karta
                        {
                            Barva = cards[index].Suit,
                            Hodnota = cards[index].Value,
                            Poznamka = debugInfo[index]
                        };
                    }
                }
                catch (Exception)
                {
                }
                if (money != null)
                {
                    gameDto.Zuctovani = new Zuctovani
                    {
                        Hrac1 = new Skore
                        {
                            Body = _g.GameStartingPlayerIndex == 0 ? money.PointsWon : money.PointsLost,
                            Zisk = money.MoneyWon[0]
                        },
                        Hrac2 = new Skore
                        {
                            Body = _g.GameStartingPlayerIndex == 1 ? money.PointsWon : money.PointsLost,
                            Zisk = money.MoneyWon[1]
                        },
                        Hrac3 = new Skore
                        {
                            Body = _g.GameStartingPlayerIndex == 2 ? money.PointsWon : money.PointsLost,
                            Zisk = money.MoneyWon[2]
                        }
                    };
                }

                gameDto.SaveGame(fs, false);
                var sr = new StreamReader(fs);
                return sr.ReadToEnd();;
            }
        }

		public bool ShouldChooseDurch()
		{
			var holesPerSuit = new Dictionary<Barva, int>();
			var hiHolePerSuit = new Dictionary<Barva, Card>();
            var dummyTalon = TeamMateIndex == -1 ? ChooseDurchTalon(Hand, _trumpCard) : Enumerable.Empty<Card>();

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
                                          (topCards.Count(j => j.Suit == i.Key) > 0 &&              //barva ve ktere mam o 2 mene nejvyssich karet nez der 
                                           topCards.Count(j => j.Suit == i.Key) + 2 >= i.Value &&  //doufam v dobrou rozlozenost (simulace ukaze)
                                           Enum.GetValues(typeof(Barva)).Cast<Barva>().Count(b => Hand.CardCount(b) == 1 &&
                                                                                                  !Hand.HasA(b)) <= 1)))
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
            var talon = TeamMateIndex == -1 ? ChooseBetlTalon(Hand, null) : new List<Card>();                //nasimuluj talon
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
                (hiCardsPerSuit.Sum(i => i.Value) <= 4 && hiCardsPerSuit.All(i => i.Value <= 2)) ||
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

        public int EstimateFinalBasicScore2()
        {
            var score = 0;
            var tigrovo1 = 0;
            var tigrovo2 = 0;
            var oneCardSuit = new List<Barva>();
            var noSuit = new List<Barva>();
            var kqSuit = new List<Barva>();
            var talon = Hand.Count == 12 ? ChooseNormalTalon(Hand, _trumpCard) : new List<Card>();
            var hand = Hand.Where(i => !talon.Contains(i)).ToList();

            if (Hand.CardCount(_trump.Value) >= 3 &&
                Hand.CardCount(_trump.Value) + Hand.CardCount(Hodnota.Eso) >= 6)
            {
                tigrovo2 = 1;
            }
            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                if (Hand.HasA(b))
                {
                    tigrovo1 += 5;
                }
                if (Hand.HasK(b))
                {
                    tigrovo1 += 3;
                }
                if (Hand.HasX(b))
                {
                    tigrovo1 += 1;
                }
                if (!Hand.HasSuit(b))
                {
                    tigrovo1 += 3;
                }
                if (hand.HasA(b) && hand.HasX(b))
                {
                    if (b != _trump.Value && Hand.CardCount(b) >= 5)
                    {
                        score += 10;
                    }
                    else
                    {
                        score += 20;
                    }
                }
                else if (hand.HasA(b))
                {
                    score += 10;
                }
                else if (hand.HasX(b) && 
                         hand.HasK(b) && 
                         (hand.CardCount(b) <= 5 ||
                          b == _trump.Value))
                {
                    score += 10;
                }
                else if (hand.HasX(b) &&
                         hand.HasQ(b) &&
                         hand.CardCount(b) >= 3 &&
                         hand.CardCount(_trump.Value) >= 3)
                {
                    score += 10;
                }
                else if (hand.HasX(b) &&
                         hand.HasJ(b) &&
                         hand.CardCount(b) >= 4 &&
                         hand.CardCount(_trump.Value) >= 4)
                {
                    score += 10;
                }
                if (hand.CardCount(b) == 1 &&
                    b != _trump.Value)
                {
                    oneCardSuit.Add(b);
                }
                if (b != _trump.Value &&
                    !hand.HasSuit(b))
                {
                    noSuit.Add(b);
                }
                if (Hand.HasK(b) &&
                    Hand.HasQ(b))
                {
                    kqSuit.Add(b);
                }
            }
            var kqMax = kqSuit.Any(b => b == _trump.Value) ? 40 : kqSuit.Any() ? 20 : 0;
            score += 10 * Math.Min(Hand.CardCount(_trump.Value) / 2, noSuit.Count * 2 + oneCardSuit.Count);
            if (Hand.CardCount(_trump.Value) >= 4 ||
                Hand.Average(i => (float)i.Value) >= (float)Hodnota.Kral)
            {
                score += 10;
            }
            if (PlayerIndex == _g.GameStartingPlayerIndex && !Hand.HasA(_trump.Value))
            {
                score -= 10;
            }
            if (score + kqMax >= 100)
            {
                score += kqSuit.Sum(b => b == _trump.Value ? 40 : 20);
            }
            else
            {
                score += kqMax;
            }
            DebugInfo.EstimatedFinalBasicScore2 = score;
            DebugInfo.Tygrovo = tigrovo1;
            DebugInfo.Strong = tigrovo2;
            _debugString.AppendFormat("EstimatedFinalBasicScore2: {0}\n", score);

            return score;
        }

        public int EstimateFinalBasicScore(List<Card> hand = null)
        {
            hand = hand ?? Hand;

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
                                               hand.CardCount(i.Suit) > 2))))));
            var trumpCount = hand.CardCount(trump.Value);
            var cardsPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>().ToDictionary(b => b, b => hand.CardCount(b));
            var aceOnlySuits = cardsPerSuit.Count(i => i.Value == 1 && 
                                                       hand.HasA(i.Key) &&
                                                       (_talon == null ||
                                                        !_talon.HasX(i.Key))); //zapocitame desitku pokud mame od barvy jen eso a desitka neni v talonu
            var emptySuits = cardsPerSuit.Count(i => i.Value == 0 &&
                                                     (_talon == null ||
                                                      (!_talon.HasA(i.Key)) &&
                                                       !_talon.HasX(i.Key)));
            if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    .All(b => !hand.HasA(b) &&
                              !(hand.HasX(b) &&
                                hand.HasK(b))))
            {
                emptySuits = 0;
                aceOnlySuits = 0;
            }
            var axPotentialDeduction = GetTotalHoles(hand, false) / 3;
            var axWinPotential = Math.Min(2 * emptySuits + aceOnlySuits, (int)Math.Ceiling(hand.CardCount(trump.Value) / 2f)); // ne kazdym trumfem prebiju a nebo x

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
            if (n >= 60 && emptySuits > 0)  //pokud to vypada na kilo, odecti body za diry na ktere si souperi muzou namazat
            {
                n -= axPotentialDeduction * 10;
            }
            if (n < 0)
            {
                n = 0;
            }
            _debugString.AppendFormat("EstimatedFinalBasicScore: {0}\n", n);
            DebugInfo.EstimatedFinalBasicScore = n;
            EstimateFinalBasicScore2();

            return n;
        }

        public bool Is100AgainstPossible(int scoreToQuery = 100)
        {
            return EstimateTotalPointsLost() >= scoreToQuery;
        }

        public int EstimateTotalPointsLost(List<Card> hand = null, List<Card> talon = null)
        {
            hand = hand ?? Hand;
            talon = talon ?? _talon;

            var trump = _trump ?? _g.trump;
            var noKQSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => !hand.HasK(b) &&
                                            !hand.HasQ(b) &&
                                            (talon == null ||
                                             (!talon.HasK(b) &&
                                              !talon.HasQ(b))));
            var estimatedKQPointsLost = noKQSuits.Sum(b => b == _trump ? 40 : 20);
            var estimatedBasicPointsLost = 90 - EstimateFinalBasicScore(hand);
            var estimatedPointsLost = estimatedBasicPointsLost + estimatedKQPointsLost;

            DebugInfo.MaxEstimatedPointsLost = estimatedPointsLost;
            _debugString.AppendFormat("MaxEstimatedLoss: {0} pts\n", estimatedPointsLost);

            return estimatedPointsLost;
        }

        public bool IsSevenTooRisky(List<Card> hand = null)
        {
            hand = hand ?? Hand;            
            var result = hand.CardCount(_trump.Value) < 4 ||                      //1.  mene nez 4 trumfy
                         (hand.CardCount(_trump.Value) == 4 &&
                          TeamMateIndex != -1 &&
                          (!_teamMateDoubledGame ||
                           _g.MandatoryDouble)) ||
                         (hand.CardCount(_trump.Value) <= 4 &&
                          (TeamMateIndex == -1 ||
                           (!_teamMateDoubledGame ||
                            _g.MandatoryDouble)) &&
                          (!hand.HasA(_trump.Value) ||
                           !hand.HasX(_trump.Value)) &&
                          !(hand.HasK(_trump.Value) &&
                            hand.HasQ(_trump.Value)) &&
                          !(Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hand.HasSuit(b))
                                .Any(b => hand.CardCount(b) >= 5) ||
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hand.HasSuit(b))
                                .All(b => hand.HasA(b) ||
                                          (hand.HasX(b) &&
                                           (hand.HasK(b) ||
                                            hand.CardCount(b) > 2))) ||
                            hand.Where(i => i.Suit != _trump.Value)
                                .Count(i => i.Value == Hodnota.Eso) >= 2)) ||
                         (hand.CardCount(_trump.Value) == 4 &&                    //2.  nebo 4 trumfy a jedno z
                          (TeamMateIndex == -1 ||
                           (!_teamMateDoubledGame ||
                            _g.MandatoryDouble)) &&
                          (hand.Count(i => i.Value >= Hodnota.Svrsek) < 3 ||      //2a. mene nez 3 vysoke karty celkem
                           (hand.Count(i => i.Value == Hodnota.Eso) +              //2b. nebo mene nez 2 (resp. 3) uhratelne A, X
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Count(b => (hand.HasX(b) &&
                                             (hand.HasK(b) ||
                                              hand.HasA(b) ||
                                              (hand.HasQ(b) &&
                                               hand.CardCount(b) > 2)))) < (TeamMateIndex == -1 ? 2 : 3) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump.Value)
                                .All(b => hand.CardCount(b) < 4)) ||              //   (vyjma pripadu kdy mam dlouhou netrumfovou tlacnou barvu)
                           (hand.Select(i => i.Suit).Distinct().Count() < 4 &&   //2c. nebo nevidim do nejake barvy
                           !(((hand.HasA(_trump.Value) ||                             //    (vyjma pripadu kdy mam trumfove A nebo X a max. 2 neodstranitelne netrumfove diry)
                               hand.HasX(_trump.Value)) &&
                              GetTotalHoles(Hand, false, false) <= 2) ||                //    (nebo vyjma pripadu kdy mam X,K,Q trumfove a k tomu netrumfove eso
                             (hand.HasX(_trump.Value) &&
                              hand.HasK(_trump.Value) &&
                              (hand.HasQ(_trump.Value) ||
                               hand.HasJ(_trump.Value)) &&
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => b != _trump.Value &&
                                              hand.HasSuit(b))
                                  .Any(b => hand.HasA(b))))))) ||
                         (hand.CardCount(_trump.Value) == 5 &&                    //3.  5 trumfu a nemam trumfove A+K+S
                          (TeamMateIndex == -1 ||
                           (!_teamMateDoubledGame ||
                            _g.MandatoryDouble)) &&
                          hand.Count(i => i.Suit == _trump.Value &&
                                          i.Value >= Hodnota.Svrsek) < 3 &&
                          ((hand.Count(i => i.Value >= Hodnota.Svrsek) < 3 &&
                            hand.Count(i => i.Value >= Hodnota.Spodek) < 4) ||    //3a. mene nez 3 (resp. 4) vysoke karty celkem (plati pro aktera i protihrace)
                           (hand.Select(i => i.Suit).Distinct().Count() < 4 &&    //3b. nebo nevidim do nejake barvy a zaroven mam 4 a vice netrumfovych der
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump.Value)
                                .Count(b => !hand.HasA(b) &&
                                            hand.Count(i => i.Suit == b &
                                                            i.Value >= Hodnota.Svrsek) <
                                            hand.Count(i => i.Suit == b &&
                                                            i.Value <= Hodnota.Spodek)) > 1)));

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

        public bool IsHundredTooRisky(List<Card> hand = null)
        {
            hand = hand ?? Hand;

			var n = GetTotalHoles(hand);
            var nn = GetTotalHoles(hand, false);
            var sh = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                         .Where(b => GetTotalHoles(hand, b) > 0)
                         .ToList();
            var axCount = hand.Count(i => i.Value == Hodnota.Eso || i.Value == Hodnota.Desitka);

            if (Settings.SafetyHundredThreshold > 0 &&
                _minWinForHundred <= -Settings.SafetyHundredThreshold)
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
                Hand.CardCount(_trump.Value) >= 5 &&
                Hand.HasK(_trump.Value) &&
                Hand.HasQ(_trump.Value))
            {
                return false;
            }
            if (hand.CardCount(_trump.Value) <= 3)
            {
                DebugInfo.HundredTooRisky = true;
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
                 !hand.HasA(_trump.Value)) ||
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
            if (hand.Select(i => i.Suit).Distinct().Count() < 4 &&
                n > 3 &&
                Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    .Where(b => b != _trump.Value)
                    .Any(b => hand.CardCount(b) > 2 &&
                              ((!hand.HasA(b) &&
                                !hand.HasX(b)) ||
                               (hand.HasX(b) &&
                                !hand.HasA(b) &&
                                !hand.HasK(b)))))
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
                          (nn > 3 ||
                           (nn == 3 &&
                            sh.Count > 2))) ||
                         (n == 3 &&                       //nebo u 3 neodstranitelnych der pokud nemam trumfove eso
                          (!hand.HasA(_trump.Value) ||    //a mam pet nebo mene trumfu
                           !hand.HasX(_trump.Value)) &&
                          hand.CardCount(_trump.Value) <= 6 &&
                          !(hand.HasX(_trump.Value) &&   //a nemam jednu z trumfovych X, K, F
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
                            hand.HasQ(_trump.Value)));

            DebugInfo.HundredTooRisky = result;

            return result;
        }

        public int GetTotalHoles(List<Card> hand, Barva b)
        {
            hand = hand ?? Hand;

            var hiCards = hand.Count(i => i.Suit == b &&
                                          Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                              .Select(h => new Card(b, h))
                                              .All(j => j.Value < i.Value ||
                                                        hand.Contains(j))); //pocet nejvyssich karet v barve
            var loCards = hand.CardCount(b) - hiCards;                      //pocet karet ktere maji nad sebou diru v barve
            var opCards = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()   //vsechny hodnoty ktere v dane barve neznam
                            .Where(h => !hand.Any(i => i.Suit == b &&
                                                       i.Value == h))
                            .ToList();
            var opA = 0;
            if (opCards.Count() >= hiCards)                                 //odstran tolik nejmensich hodnot kolik mam nejvyssich karet
            {
                opCards = opCards.OrderBy(h => (int)h).Skip(hiCards).ToList();
                if (hiCards == 0)
                {
                    //Korekce: -XKS--8- by podle tohoto vyslo jako 3, ale ve skutecnosti vytlacim eso (1) kralem
                    //zbyvajici 2 nejvyssi trumfy pokryjou 2 diry a zbyde jedna dira. Cili spravny vysledek ma byt 2

                    //spocitej nevyssi karty bez esa
                    hiCards = hand.Count(i => i.Suit == b &&
                                          Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                              .Where(h => h < Hodnota.Eso)
                                              .Select(h => new Card(b, h))
                                              .All(j => j.Value < i.Value ||
                                                        hand.Contains(j)));
                    opCards = opCards.OrderBy(h => (int)h).Skip(hiCards).ToList();
                    if (hiCards > 1) //mame aspon X a K - eso vytlacime kralem, cili pocitame, ze mame o trumf mene a o diru mene (nize)
                    {
                        loCards = hand.CardCount(b) - hiCards;
                        opA++;
                    }
                    else if (hiCards == 1 &&        //mame aspon X+S+1 - eso vytlacime svrskem a jeste jednou, cili pocitame, ze mame o diru mene
                             hand.HasQ(b) &&
                             hand.CardCount(b) > 2)
                    {
                        loCards -= 2;
                        opA++;
                    }
                    else if (hiCards == 0)
                    {
                        //spocitej nevyssi karty bez esa
                        hiCards = hand.Count(i => i.Suit == b &&
                                              Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                  .Where(h => h < Hodnota.Desitka)
                                                  .Select(h => new Card(b, h))
                                                  .All(j => j.Value < i.Value ||
                                                            hand.Contains(j)));
                        opCards = opCards.OrderBy(h => (int)h).Skip(hiCards).ToList();
                        if (hiCards > 2)    //mame aspon 2 nejvyssi karty na vytlaceni desitky a esa
                        {
                            loCards = hand.CardCount(b) - hiCards;
                            opA += 2;       //zneuzijeme citac es i na desitku
                        }
                    }
                }
            }
            else
            {
                opCards.Clear();
            }
            var holes = opCards.Count(h => hand.Any(i => i.Suit == b &&     //pocet zbylych der vyssich nez moje nejnizsi karta plus puvodni dira (eso)
                                                         i.Value < h)) + opA;

            return Math.Min(holes, loCards + opA);
        }

        public int GetTotalHoles(List<Card> hand = null, bool includeTrumpSuit = true, bool includeAceSuits = true)
        {
            hand = hand ?? Hand;

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

                n += GetTotalHoles(hand, b);
            }
            _debugString.AppendFormat("TotalHoles: {0}\n", n);
            DebugInfo.TotalHoles = n;

            return n;
        }

        //private int GetTotalHoles()
        //{
        //    //neodstranitelne diry jsou takove, ktere zbydou pokud zahraju nejvyssi karty od dane barvy
        //    //kdyz jich je moc, tak hrozi, ze se nedostanu do stychu a souperi ze me budou tahat trumfy
        //    //proto v takovem pripade sedmu nehraj
        //    var totalHoles = 0;
        //    foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
        //    {
        //        var topCards = Hand.Where(i => i.Suit == b &&
        //                                       Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
        //                                           .Where(h => h > i.Value)
        //                                           .Select(h => new Card(b, h))
        //                                           .All(j => Probabilities.CardProbability((PlayerIndex + 1) % Game.NumPlayers, j) == 0 &&
        //                                                     Probabilities.CardProbability((PlayerIndex + 2) % Game.NumPlayers, j) == 0)).ToList();
        //        var nonTopCards = Hand.CardCount(b) - topCards.Count;
        //        var holes = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
        //                        .Select(h => new Card(b, h))
        //                        .Where(i => !Hand.Contains(i) &&
        //                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
        //                                        .Where(h => h < i.Value)
        //                                        .Select(h => new Card(b, h))
        //                                        .Any(j => Hand.Contains(j))).ToList();
        //        var n = holes.Count - topCards.Count;

        //        if (n > 0)
        //        {
        //            if (n > nonTopCards)
        //            {
        //                n = nonTopCards;
        //            }
        //            totalHoles += n;
        //        }
        //    }
        //    _debugString.AppendFormat("TotalHoles: {0}\n", totalHoles);

        //    return totalHoles;
        //}

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
                         !_hundredOverBetl)
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
                         ((!Settings.AiMayGiveUp &&
                           !AdvisorMode) ||
                          ((_gamesBalance >= Settings.GameThresholdsForGameType[Hra.Hra][0] * _gameSimulations && _gameSimulations > 0) ||
                           //estimatedFinalBasicScore + kqScore >= estimatefOpponentFinalBasicScore + 40 ||
                           (estimatedFinalBasicScore >= 10 &&
                            estimatedFinalBasicScore + kqScore >= 40 &&
                            _maxMoneyLost > -Settings.SafetyBetlThreshold) ||
                           (estimatedFinalBasicScore + kqScore >= estimatefOpponentFinalBasicScore &&
                            (Hand.HasK(_trump.Value) || Hand.HasQ(_trump.Value))))))
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
                       (gameType & Hra.Hra) != 0 &&  //pri hre
                       avgPointsWon >= 110) ||
                      (_g.AllowFake107 &&
                       _g.Calculate107Separately &&
                       _g.Top107 &&
                       (gameType & Hra.Kilo) != 0 &&  //pri kilu
                       avgPointsWon >= 140)) ||
                     (Hand.Has7(_trump.Value) &&
                      _sevensBalance >= Settings.GameThresholdsForGameType[Hra.Sedma][0] * _sevenSimulations && _sevenSimulations > 0 &&
                      (!IsSevenTooRisky() ||                  //sedmu hlas pokud neni riskantni nebo pokud nelze uhrat hru (doufej ve flek na hru a konec)
                       (!_g.PlayZeroSumGames &&
                        Hand.CardCount(_trump.Value) >= 4 &&
                        Hand.Any(i => i.Suit == _trump.Value &&
                                      i.Value >= Hodnota.Svrsek) &&
                        _gamesBalance < Settings.GameThresholdsForGameType[Hra.Hra][0] * _gameSimulations && 
                        _gameSimulations > 0)))))
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
                                              .Where(b => !Hand.HasK(b) && !Hand.HasQ(b))
                                              .Sum(b => b == _g.trump.Value ? 40 : 20)
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
           
            if ((bidding.Bids & Hra.Hra) != 0 &&                //pokud byla zvolena hra (nebo hra a sedma]
                Settings.CanPlayGameType[Hra.Hra] &&
                bidding._gameFlek <= Settings.MaxDoubleCountForGameType[Hra.Hra] &&
                (bidding._gameFlek < 3 ||                      //ai nedava tutti pokud neflekoval i clovek
                 PlayerIndex == 0 || 
                 _teamMateDoubledGame) &&
                _gameSimulations > 0 &&                         //pokud v simulacich vysla dost casto
                _gamesBalance / (float)_gameSimulations >= gameThreshold &&
                //pokud jsem volil (re a vys) a:
                //trham trumfovy hlas
                //nebo ho netrham a mam aspon hlas a nehrozi kilo proti
                //nebo to vypada, ze muzu uhrajat vic nez souperi a nehrozi kilo proti
                ((TeamMateIndex == -1 &&
                  (Hand.HasK(_g.trump.Value) ||                         //davej si re jen pokud trhas trumfovou hlasku
                   Hand.HasQ(_g.trump.Value) ||
                   Settings.SafetyGameThreshold == 0 ||
                   _maxMoneyLost >= -Settings.SafetyGameThreshold) &&   //nebo pokud netrhas ale v zadne simulaci nevysla vysoka prohra
                  (((Hand.HasK(_g.trump.Value) ||
                     Hand.HasQ(_g.trump.Value) ||
                     kqScore >= 40) &&
                    ((kqScore >= 20 &&
                      estimatedFinalBasicScore >= 40 &&
                      (bidding.Bids & Hra.SedmaProti) == 0 &&
                      !Is100AgainstPossible()) ||
                     (kqScore >= 40 &&
                      estimatedFinalBasicScore >= 30 &&
                      axCount >= 3 &&
                      !Is100AgainstPossible(110)) ||
                     (kqScore >= 60 &&
                      estimatedFinalBasicScore >= 20 &&
                      axCount >= 2 &&
                      !Is100AgainstPossible(120)) ||
                     estimatedFinalBasicScore + kqScore > estimatedOpponentFinalBasicScore + kqMaxOpponentScore ||
                     (estimatedFinalBasicScore + kqScore > estimatedOpponentFinalBasicScore + 10 &&
                      (bidding.Bids & Hra.SedmaProti) == 0 &&
                      !Is100AgainstPossible(110)))) ||
                   (kqScore >= 20 &&                            //davam si re kdyz mam aspon jeden hlas
                    Hand.CardCount(_trump.Value) >= 4 &&        //4 trumfy 
                    totalHoles <= 4 &&                          //a max 4 diry (tj. jinak same vysoke karty), netreba trhat trumfovou hlasku
                    (bidding.Bids & Hra.SedmaProti) == 0 &&
                    estimatedFinalBasicScore >= 40) || 
                   ((estimatedFinalBasicScore > 60 ||            //pokud si davam re a nethram, musim mit velkou jistotu
                     (estimatedFinalBasicScore >= 60 &&
                      kqScore >= 20)) &&
                    (bidding.Bids & Hra.SedmaProti) == 0 &&
                    !Is100AgainstPossible(110)) ||              //ze uhraju vic bodu i bez trhaka a ze souper neuhraje kilo (110 - kilo jeste risknu)
                   (estimatedFinalBasicScore >= 60 &&           //pokud mam dost trumfu, bude kriterium mekci
                    Hand.CardCount(_trump.Value) >= 4 &&
                    !Is100AgainstPossible(110)) ||
                   (estimatedFinalBasicScore >= 50 &&           //pokud mam dost trumfu, bude kriterium mekci
                    Hand.CardCount(_trump.Value) >= 4 &&
                    (bidding.Bids & Hra.SedmaProti) == 0 &&
                    kqScore >= kqMaxOpponentScore) ||
                   (estimatedFinalBasicScore >= 50 &&           //pokud mam dost trumfu, bude kriterium mekci
                    Hand.CardCount(_trump.Value) >= 5 &&
                    (Hand.HasK(_g.trump.Value) ||
                     Hand.HasQ(_g.trump.Value) ||
                     axCount >= 4) &&
                    !Is100AgainstPossible(110)) ||
                   (estimatedFinalBasicScore >= 50 &&           //pokud mam dost trumfu, bude kriterium mekci
                    Hand.CardCount(_trump.Value) >= 4 &&
                    Hand.HasA(_g.trump.Value) &&
                    (Hand.HasK(_g.trump.Value) ||
                     Hand.HasQ(_g.trump.Value)) &&
                    axCount >= 3 &&
                    Hand.CardCount(Hodnota.Eso) >= 2 &&
                    !Is100AgainstPossible()))) ||
                 //nebo jsem nevolil a:
                 (TeamMateIndex != -1 &&                  
                  ((bidding.GameMultiplier > 2 &&               //Tutti:
                    ((Hand.CardCount(_g.trump.Value) >= 2 &&    //mam aspon 2 trumfy a k tomu aspon jednu hlasku nebo trham aspon 2 hlasky
                      ((estimatedFinalBasicScore + kqScore > estimatedOpponentFinalBasicScore &&
                        _teamMateDoubledGame &&
                        !_g.MandatoryDouble) ||
                       (estimatedFinalBasicScore + kqScore > estimatedOpponentFinalBasicScore + kqMaxOpponentScore &&
                        Hand.CardCount(_g.trump.Value) >= 3 &&
                        !Is100AgainstPossible())) &&
                      (kqScore >= 20 ||
                       Enum.GetValues(typeof(Barva)).Cast<Barva>()
                           .Count(b => Hand.HasK(b) || Hand.HasQ(b)) >= 2)) ||
                     (_teamMateDoubledGame &&                   //nebo kolega flekoval
                      (kqScore >= 40 ||                         //a mam aspon 40 bodu v hlasech
                       (kqScore >= 20 &&                        //nebo aspon 20 bodu v hlasech a 20 bodu odhadem k tomu
                        estimatedFinalBasicScore >= 20 &&
                        !_g.MandatoryDouble))) ||
                     (Hand.CardCount(_g.trump.Value) >= 4 &&    //nebo mam aspon 4 trumfy
                      Hand.HasA(_g.trump.Value) &&              //eso, trhak
                      (Hand.HasK(_g.trump.Value) ||              //a aspon 50 bodu
                       Hand.HasQ(_g.trump.Value)) &&
                      estimatedFinalBasicScore >= 50) ||
                     (Hand.HasA(_g.trump.Value) &&
                      Hand.HasK(_g.trump.Value) &&
                      Hand.HasQ(_g.trump.Value) &&
                      estimatedFinalBasicScore + kqScore >= 60) ||
                     (Hand.HasA(_g.trump.Value) &&
                      kqScore >= 40 &&
                      estimatedFinalBasicScore + kqScore >= 60) ||
                     (Hand.CardCount(_g.trump.Value) >= 4 &&    //nebo mam aspon 4 trumfy
                      DebugInfo.Tygrovo >= 20))) ||             //a k tomu silne karty
                   (bidding.GameMultiplier < 2 &&               //Flek: ******
                    ((Hand.CardCount(_g.trump.Value) >= 2 &&    //aspon 2 trumfy
                      (Hand.HasK(_g.trump.Value) ||             //a k tomu trhak nebo 2 hlasy
                       Hand.HasQ(_g.trump.Value) ||
                       kqScore >= 40) &&
                      (estimatedFinalBasicScore >= 30 ||        //a aspon 30 nebo 20+20 bodu na ruce
                       (estimatedFinalBasicScore >= 10 &&
                        kqScore >= 20) &&
                       kqMaxOpponentScore <= 20)) ||          //nebo
                     (Hand.CardCount(_g.trump.Value) >= 3 &&    //aspon 3 trumfy a trhaka
                      (Hand.HasK(_g.trump.Value) ||             //a aspon 3 ostre karty
                       Hand.HasQ(_g.trump.Value)) &&            //a aspon jedno eso
                      axCount >= 3 &&
                      Hand.CardCount(Hodnota.Eso) >= 1) ||
                     (Hand.CardCount(_g.trump.Value) >= 2 &&    //aspon 2 trumfy
                      (Hand.HasA(_g.trump.Value) ||             //z toho aspon jeden A nebo X
                       Hand.HasX(_g.trump.Value)) &&
                      Hand.CardCount(Hodnota.Eso) >= 2 &&       //a aspon 2 eso celkem
                      axCount >= 6) ||                          //a dohromady aspon 6 desitek a es nebo
                     (axCount >= 5 &&                           //aspon 5 desitek a es a
                      Hand.HasA(_g.trump.Value) &&              //k tomu trumfove AX
                      Hand.HasX(_g.trump.Value)) ||             //nebo
                     (kqMaxOpponentScore == 0 ||                //vidim do vsech hlasek
                      kqScore >= 60 ||
                      (kqScore >= 40 &&
                       kqMaxOpponentScore <= 20 &&
                       estimatedFinalBasicScore >= 20) ||
                      (estimatedFinalBasicScore >= 30 &&        //nebo vidim do tri hlasek a mam aspon 30 bodu
                       kqMaxOpponentScore <= 20)) ||            //nebo
                     ((Hand.HasK(_g.trump.Value) ||             //pouze trhak a 40 bodu v hlasech na ruce
                       Hand.HasQ(_g.trump.Value)) &&
                      kqScore >= 40) ||                         //nebo
                     ((Hand.HasK(_g.trump.Value) ||             //trhak a vidim do vsech hlasek (u sedmy jeste pokud mam vic bodu nez souper)
                       Hand.HasQ(_g.trump.Value)) &&
                       ((bidding.Bids & Hra.Sedma) == 0 ||
                        estimatedFinalBasicScore + kqScore > estimatedOpponentFinalBasicScore) &&
                      !Is100AgainstPossible()) ||
                     ((Hand.HasK(_g.trump.Value) ||
                       Hand.HasQ(_g.trump.Value)) &&
                      DebugInfo.Tygrovo >= 15) ||
                     ((Hand.HasA(_g.trump.Value) ||            //nebo mam trumfove eso
                       Hand.HasX(_g.trump.Value)) &&           //popr. desitku
                      (Hand.HasK(_g.trump.Value) ||            //a k tomu trhak
                      Hand.HasQ(_g.trump.Value)) &&
                      bestCaseNonTrumpScore >= 20) ||          //a aspon 2 netrumfove desitky
                     ((Hand.HasK(_g.trump.Value) ||            //nebo mam trhak
                       Hand.HasQ(_g.trump.Value)) &&             
                      Hand.CardCount(_g.trump.Value) >= 2 &&   //a aspon dva trumfy
                      kqScore >= 20 &&                         //a aspon 1 netrumfovou desitku
                      kqMaxOpponentScore <= 20 &&              //a vidim aspon do tri hlasu
                      estimatedFinalBasicScore >= 20) ||       //a odhaduju ze uhraju aspon 20 bodu v desitkach
                     (Hand.HasA(_g.trump.Value) &&             //nebo mam aspon trumfove eso
                      kqScore >= 40 &&                         //40 bodu v hlasech
                      estimatedFinalBasicScore >= 40) ||       //a odhaduju ze uhraju aspon 40 bodu v desitkach
                     ((Hand.HasK(_g.trump.Value) ||
                       Hand.HasQ(_g.trump.Value)) &&           //nebo mam trhaka
                      Hand.CardCount(_g.trump.Value) >= 4 &&   //a aspon 4 trumfy
                      !Is100AgainstPossible(130)) ||
                     ((Hand.HasA(_g.trump.Value) ||
                       Hand.HasX(_g.trump.Value)) &&           //nebo trumfove A nebo X
                      Hand.CardCount(_g.trump.Value) >= 3 &&   //a aspon 3 trumfy
                      estimatedFinalBasicScore + kqScore >= 50 &&
                      axCount >= 3 &&
                      !Is100AgainstPossible(120)) ||
                     ((Hand.HasK(_g.trump.Value) ||
                       Hand.HasQ(_g.trump.Value)) &&           //nebo trumfove K nebo Q
                      Hand.CardCount(_g.trump.Value) >= 2 &&   //a aspon 2 trumfy
                      Hand.CardCount(Hodnota.Eso) >= 2 &&      //a aspon 2 esa
                      (bidding.Bids & Hra.Sedma) == 0 &&       //a ppuze pokud nbyla hlasena sedma
                      axCount >= 3 &&
                      !Is100AgainstPossible(130)) ||
                     //(Hand.HasA(_g.trump.Value) &&             //nebo mam aspon trumfove eso
                     // (Hand.HasX(_g.trump.Value) ||            //a desitku nebo
                     //  Hand.CardCount(_g.trump.Value) >= 3) && //aspon 3 trumfy
                     // estimatedFinalBasicScore >= 60 &&        //a aspon 60 bodu na ruce v desitkach
                     // kqMaxOpponentScore <= 80) ||             //a trham aspon jeden hlas
                     (//Hand.CardCount(_g.trump.Value) >= 3 &&    //nebo mam aspon 3 trumfy vcetne A,X
                      Hand.HasA(_g.trump.Value) &&
                      (Hand.HasX(_g.trump.Value) ||
                       Hand.CardCount(_g.trump.Value) >= 3) &&
                      estimatedFinalBasicScore >= 40 &&         //a aspon 40 bodu v desitkach a 20 v hlasech
                      axCount >= 4 &&
                      (estimatedFinalBasicScore + kqScore >= 60 ||
                       (estimatedFinalBasicScore >= 50 &&
                        axCount >= 5)) &&       //nebo aspon 50 bodu
                      !Is100AgainstPossible(140)) ||
                     (Hand.CardCount(_g.trump.Value) >= 4 &&   //nebo mam aspon 4 trumfy
                      (Hand.HasA(_g.trump.Value) ||
                       Hand.HasX(_g.trump.Value) ||
                       Hand.HasK(_g.trump.Value) ||
                       Hand.HasQ(_g.trump.Value)) &&
                      estimatedFinalBasicScore >= 50 &&       //a aspon 50 bodu
                      !Is100AgainstPossible(110)) ||
                     (Hand.CardCount(_g.trump.Value) >= 5 &&   //nebo mam aspon 5 trumfu
                      estimatedFinalBasicScore >= 40 &&       //a aspon 40 bodu
                      !Is100AgainstPossible(110)) ||
                     (Hand.CardCount(_g.trump.Value) >= 5 &&   //nebo mam aspon 5 trumfu vcetne A,X
                      Hand.HasA(_g.trump.Value) &&
                      Hand.HasX(_g.trump.Value)) ||
                     (Hand.CardCount(_g.trump.Value) >= 5 &&   //nebo mam aspon 5 trumfu a trhaka
                      (Hand.HasK(_g.trump.Value) &&
                       Hand.HasQ(_g.trump.Value))) ||
                     (Hand.CardCount(_g.trump.Value) >= 4 &&   //nebo mam aspon 4 trumfy
                      (Hand.HasK(_g.trump.Value) ||            //a trhak a bud A nebo X trumfovou
                       Hand.HasQ(_g.trump.Value)) &&
                      (Hand.HasA(_g.trump.Value) ||
                       Hand.HasX(_g.trump.Value))) ||
                     (_teamMateDoubledSeven &&                //nebo spoluhrac dal flek na sedmu a ja mam aspon 40 bodu na ruce
                      !_g.AllowFakeSeven &&
                      //!Is100AgainstPossible() &&
                      (kqScore >= 40 ||
                       (kqScore >= 20 &&
                        estimatedFinalBasicScore >= 20) ||
                       ((Hand.HasK(_g.trump.Value) ||
                         Hand.HasQ(_g.trump.Value)) &&
                        estimatedFinalBasicScore >= 40))) ||  //nebo mam aspon jeden hlas, dost trumfu a dost bodu na ruce a akter nemuze uhrat kilo
                     (kqScore >= 20 &&
                      Hand.CardCount(_g.trump.Value) >= 4 &&
                      estimatedFinalBasicScore + kqScore > estimatedOpponentFinalBasicScore &&
                      estimatedOpponentFinalBasicScore + kqMaxOpponentScore < 100) ||
                     ((Hand.HasK(_g.trump.Value) ||            //nebo mam trhak, aspon 2 desitky, aspon ctyri trumfy a souper ma max. 40 bodu v hlasech
                       Hand.HasQ(_g.trump.Value)) &&
                      Hand.CardCount(_g.trump.Value) >= 4 &&
                      kqMaxOpponentScore <= 40 &&
                      bestCaseNonTrumpScore >= 20)))))))
            {
                bid |= bidding.Bids & Hra.Hra;
                //minRuleCount = Math.Min(minRuleCount, _gamesBalance);
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
                  ((Hand.CardCount(_g.trump.Value) >= 4 ||           //ctyri a vice trumfu nebo
                    (Hand.CardCount(_g.trump.Value) >= 3 &&          //tri trumfy 3-3-2-2                     
                     (Hand.CardCount(Hodnota.Eso) >= 3 &&
                      ((axCount >= 4 &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .All(b => Hand.CardCount(b) >= 2)) ||
                       (axCount >= 4 &&                               //hodne ostrych karet a vsechny barvy
                        estimatedFinalBasicScore >= 40 &&
                        handSuits == Game.NumSuits) //||
                       //(Hand.Any(i => i.Suit == _trump.Value &&
                       //               i.Value >= Hodnota.Svrsek) &&
                       // Hand.Count(i => i.Value >= Hodnota.Svrsek) >= 6 &&
                       // Enum.GetValues(typeof(Barva)).Cast<Barva>()
                       //     .Where(b => Hand.HasSuit(b))
                       //     .All(b => Hand.CardCount(b) >= 2)) ||
                       //(Hand.HasA(_trump.Value) &&                    //trumfove eso a desitka
                       // Hand.HasX(_trump.Value) &&
                       // Hand.CardCount(Hodnota.Eso) >= 2 &&           //aspon dve esa a
                       // Enum.GetValues(typeof(Barva)).Cast<Barva>()   //jedna dlouha barva
                       //     .Where(b => b != _trump.Value)
                       //     .Any(b => Hand.CardCount(b) >= 4))
                            )))) ||
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
            else if ((bidding.Bids & Hra.Sedma) != 0 &&
                     (bidding.Bids & Hra.Kilo) == 0 &&
                     ((bid & Hra.Hra) != 0 ||
                      (_teamMateDoubledGame &&
                       !_g.MandatoryDouble)) &&
                     Settings.CanPlayGameType[Hra.Sedma] &&
                     TeamMateIndex != -1 &&
                     bidding._sevenFlek == 1 &&                         //flekni krome hry i sedmu i kdyz v simulacich nevysla
                     ((kqScore >= 40 &&                                 //pokud je sance uhrat tichych 110 proti
                       estimatedFinalBasicScore + kqScore >= 90)))      //90 bodu uhraju sam a zbytek snad bude mit kolega
            {
                bid |= bidding.Bids & Hra.Sedma;
                //minRuleCount = Math.Min(minRuleCount, _sevensBalance);
                DebugInfo.RuleCount = _sevensBalance;
                DebugInfo.TotalRuleCount = _sevenSimulations;
            }

            if ((bidding.Bids & Hra.Sedma) != 0 &&
                Settings.CanPlayGameType[Hra.Sedma] &&
                bidding._sevenFlek < 3 &&
                TeamMateIndex != -1 &&
                Hand.Has7(_trump.Value) &&
                (_g.MinimalBidsForSeven == 0 || //pokud by akter mohl uhrat 130 a zaroven se sedma nehraje bez fleku, je lepsi neflekovat a snizit tak ztratu
                 (_teamMateDoubledGame &&
                  !_g.MandatoryDouble) ||
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
            //sedmu proti flekuju pokud mam aspon 3 trumfy a vsechny barvy
            if ((bidding.Bids & Hra.SedmaProti) != 0 &&
                Settings.CanPlayGameType[Hra.SedmaProti] &&
                bidding._sevenAgainstFlek <= Settings.MaxDoubleCountForGameType[Hra.SedmaProti] &&
                (bidding._sevenAgainstFlek < 3 ||                      //ai nedava tutti pokud neflekoval i clovek
                 PlayerIndex == 0 ||
                 (bidding.PlayerBids[0] & Hra.SedmaProti) != 0) &&
                ((bidding._sevenAgainstFlek == 0 &&                                  //nez poprve zahlasime sedmu proti zjistime jestli to neni riskantni
                  !IsSevenTooRisky()) ||
                 (bidding._sevenAgainstFlek > 0 &&                                  //flek na sedmu proti dam jen kdyz mam sam dost trumfu
                  Hand.CardCount(_g.trump.Value) >= 4)) &&
                _gameSimulations > 0 && _sevensAgainstBalance / (float)_gameSimulations >= sevenAgainstThreshold)
            {
                bid |= bidding.Bids & Hra.SedmaProti;
                //minRuleCount = Math.Min(minRuleCount, _sevensAgainstBalance);
                DebugInfo.RuleCount = _sevensAgainstBalance;
                DebugInfo.TotalRuleCount = _gameSimulations;
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
                (TeamMateIndex != -1 || //pokud jsem volil betla, tak si dej re a vys jen kdyz ma max 1 diru (kterou vyjede)
                 GetBetlHoles() <= 1))
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
            if ((bidding.Bids & Hra.SedmaProti) != 0)
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
            if ((bidding.Bids & Hra.KiloProti) != 0)
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
                                            _g.AllowFakeSeven, _g.AllowAXTalon, _g.AllowTrumpTalon,
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
                                                _g.AllowFakeSeven, _g.AllowAXTalon, _g.AllowTrumpTalon,
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
        }

        private void CardPlayed(object sender, Round r)
        {
            UpdateProbabilitiesAfterCardPlayed(Probabilities, r.number, r.player1.PlayerIndex, r.c1, r.c2, r.c3, r.hlas1, r.hlas2, r.hlas3, TeamMateIndex, _teamMatesSuits, _trump, _teamMateDoubledGame);
        }

        private static void UpdateProbabilitiesAfterCardPlayed(Probability probabilities, int roundNumber, int roundStarterIndex, Card c1, Card c2, Card c3, bool hlas1, bool hlas2, bool hlas3, int teamMateIndex, List<Barva> teamMatesSuits, Barva? trump, bool teamMateDoubledGame)
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
                        }
                    }
                }
            }

            _log.InfoFormat("{0} plays card: {1} - {2}", Name, cardToPlay, DebugInfo.Rule);
            return cardToPlay;                
        }

        private Card ComputeBestCardToPlay(IEnumerable<Hand[]> source, int roundNumber)
        {
            const int talonIndex = 3;
            var results = new ConcurrentQueue<Tuple<Card, MoneyCalculatorBase, GameComputationResult, Hand[]>>();
            var likelyResults = new ConcurrentQueue<Tuple<Card, MoneyCalculatorBase, GameComputationResult, Hand[]>>();
            var averageResults = new Dictionary<Card, double>();
            var averageLikelyResults = new Dictionary<Card, double>();
            var minResults = new Dictionary<Card, double>();
            var maxResults = new Dictionary<Card, double>();
            var winCount = new Dictionary<Card, int>();
            var n = 0;
            var start = DateTime.Now;
            var prematureStop = false;
            var uncertainTalonTrumps = _g.trump.HasValue
                                       ? Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                             .Select(h => new Card(_g.trump.Value, h))
                                             .Where(i => Probabilities.CardProbability(talonIndex, i) > 0)
                                             .ToList()
                                       : Enumerable.Empty<Card>();
            var gameStartingHand = new List<Card>();

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
            var estimatedCombinations = (int)Probabilities.EstimateTotalCombinations(roundNumber);
            var maxtime = Math.Max(2000, 2 * Settings.MaxSimulationTimeMs);
            var exceptionOccured = false;

            try
            {
                //source = new[] { _g.players.Select(i => new Hand(i.Hand)).ToArray() };
                //foreach (var hands in source)
                Parallel.ForEach(source, (hands, loopState) =>
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
                        //List<Card> talon = null;
                        //if (PlayerIndex != _g.GameStartingPlayerIndex)
                        //{
                        //    var initialHand = gameStartedInitialCards.Concat((List<Card>)hands[_g.GameStartingPlayerIndex]).Concat((List<Card>)hands[talonIndex]).ToList();
                        //    switch (_g.GameType)
                        //    {
                        //        case Hra.Betl:
                        //            talon = ChooseBetlTalon(initialHand, _g.TrumpCard);
                        //            break;
                        //        case Hra.Durch:
                        //            talon = ChooseDurchTalon(initialHand, _g.TrumpCard);
                        //            break;
                        //        default:
                        //            talon = ChooseNormalTalon(initialHand, _g.TrumpCard);
                        //            break;
                        //    }
                        //}
                        var hh = new[] {
                            new Hand((List<Card>)hands[0]),
                            new Hand((List<Card>)hands[1]),
                            new Hand((List<Card>)hands[2])
                            };
                        var result = ComputeMinMax(new List<Round>(_g.rounds.Where(i => i?.c3 != null)), hh, roundNumber);

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
                            results.Enqueue(new Tuple<Card, MoneyCalculatorBase, GameComputationResult, Hand[]>(res.Item1, res.Item2, res.Item3, hh));
                            if (!_g.trump.HasValue ||
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
                                    !(Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                          .Any(b => hands[TeamMateIndex].HasK(b) &&
                                                    hands[TeamMateIndex].HasQ(b))) &&
                                    //!hands[_g.GameStartingPlayerIndex].Any(i => talon != null &&
                                    //                                           talon.Contains(i) &&
                                    uncertainTalonTrumps.All(i => hands[0].Any(j => i == j) ||
                                                                  hands[1].Any(j => i == j) ||
                                                                  hands[2].Any(j => i == j)))))
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
                averageResults.Add(card, results.Where(i => i.Item1 == card)
                                                .Average(i => 100 * i.Item2.MoneyWon[PlayerIndex] +
                                                                (TeamMateIndex == -1 ? i.Item2.BasicPointsWon : i.Item2.BasicPointsLost)));
                averageLikelyResults.Add(card, likelyResults.Where(i => i.Item1 == card)
                                                            .Average(i => 100 * i.Item2.MoneyWon[PlayerIndex] +
                                                                          (TeamMateIndex == -1 ? i.Item2.BasicPointsWon : i.Item2.BasicPointsLost)));

                minResults.Add(card, results.Where(i => i.Item1 == card)
                                            .Min(i => 100 * i.Item2.MoneyWon[PlayerIndex] +
                                                      (TeamMateIndex == -1 ? i.Item2.BasicPointsWon : i.Item2.BasicPointsLost)));
                maxResults.Add(card, results.Where(i => i.Item1 == card)
                                            .Max(i => 100 * i.Item2.MoneyWon[PlayerIndex] +
                                                      (TeamMateIndex == -1 ? i.Item2.BasicPointsWon : i.Item2.BasicPointsLost)));
                winCount.Add(card, likelyResults.Where(i => i.Item1 == card)
                                                .Count(i => i.Item2.MoneyWon[PlayerIndex] >= 0));
            }

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
                                                .ThenByDescending(i => averageLikelyResults[i])
                                                .ThenByDescending(i => maxResults[i])
                                                .ThenBy(i => (int)i.Value)
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

            const int talonIndex = Game.NumPlayers;
            var prob1 = 0 == PlayerIndex && !ImpersonateGameStartingPlayer ? prob : new Probability(0, player1, hands[0], trump, _g.AllowFakeSeven, _g.AllowAXTalon, _g.AllowTrumpTalon, _g.CancellationToken, _stringLoggerFactory, 0 == PlayerIndex ? (List<Card>)hands[talonIndex] : null);
            prob1.UseDebugString = false;
            var prob2 = 1 == PlayerIndex && !ImpersonateGameStartingPlayer ? prob : new Probability(1, player1, hands[1], trump, _g.AllowFakeSeven, _g.AllowAXTalon, _g.AllowTrumpTalon, _g.CancellationToken, _stringLoggerFactory, 1 == PlayerIndex ? (List<Card>)hands[talonIndex] : null);
            prob2.UseDebugString = false;
            var prob3 = 2 == PlayerIndex && !ImpersonateGameStartingPlayer ? prob : new Probability(2, player1, hands[2], trump, _g.AllowFakeSeven, _g.AllowAXTalon, _g.AllowTrumpTalon, _g.CancellationToken, _stringLoggerFactory, 2 == PlayerIndex ? (List<Card>)hands[talonIndex] : null);
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
            _log.TraceFormat("{0}: {1} cerveny, {2} zeleny, {3} kule, {4} zaludy", _g.players[player2].Name, hands[player2].Count(i => i.Suit == Barva.Cerveny), hands[player2].Count(i => i.Suit == Barva.Zeleny), hands[player2].Count(i => i.Suit == Barva.Kule), hands[player2].Count(i => i.Suit == Barva.Zaludy));
            _log.TraceFormat("{0}: {1} cerveny, {2} zeleny, {3} kule, {4} zaludy", _g.players[player3].Name, hands[player3].Count(i => i.Suit == Barva.Cerveny), hands[player3].Count(i => i.Suit == Barva.Zeleny), hands[player3].Count(i => i.Suit == Barva.Kule), hands[player3].Count(i => i.Suit == Barva.Zaludy));
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

                    UpdateProbabilitiesAfterCardPlayed(prob1, aiStrategy.RoundNumber, roundStarterIndex, c1, null, null, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame);
                    UpdateProbabilitiesAfterCardPlayed(prob1, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, null, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame);
                    UpdateProbabilitiesAfterCardPlayed(prob1, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, c3, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame);

                    UpdateProbabilitiesAfterCardPlayed(prob2, aiStrategy.RoundNumber, roundStarterIndex, c1, null, null, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame);
                    UpdateProbabilitiesAfterCardPlayed(prob2, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, null, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame);
                    UpdateProbabilitiesAfterCardPlayed(prob2, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, c3, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame);

                    UpdateProbabilitiesAfterCardPlayed(prob3, aiStrategy.RoundNumber, roundStarterIndex, c1, null, null, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame);
                    UpdateProbabilitiesAfterCardPlayed(prob3, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, null, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame);
                    UpdateProbabilitiesAfterCardPlayed(prob3, aiStrategy.RoundNumber, roundStarterIndex, c1, c2, c3, hlas1, hlas2, hlas3, TeamMateIndex, teamMatesSuits, _trump, _teamMateDoubledGame);
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
