﻿using System;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Mariasek.Engine
{
    public enum CalculationStyle
    {
        Adding,
        Multiplying,
        Fixed
    }

    public enum HlasConsidered
    {
        Highest,
        First,
        Each
    }

    [XmlInclude(typeof(FixedMoneyCalculator))]
    [XmlInclude(typeof(AddingMoneyCalculator))]
    [XmlInclude(typeof(MultiplyingMoneyCalculator))]
    [XmlRoot(ElementName = "Money")]
    public abstract class MoneyCalculatorBase
    {
        protected Hra _gameType;
        protected Barva? _trump;
        protected Bidding _bidding;
        protected int _gameStartingPlayerIndex;

        protected string[] PlayerNames;

        public bool GoodGame
        {
            get { return (_gameType & Hra.Betl) == 0 && (_gameType & Hra.Durch) == 0; }
        }

        public HlasConsidered HlasConsidered { get; private set; }
        public bool Calculate107Separately { get; private set; }
        public bool CountHlasAgainst { get; private set; }

        public bool GamePlayed { get; private set; }
        public bool GivenUp { get; private set; }
        [XmlIgnore]
        public float BaseBet { get; set; }
        [XmlIgnore]
        public string Locale { get; set; }
        [XmlIgnore]
		public int MaxWin { get; set; }
        public Hra GameType { get { return _gameType; } }
        public string GameTypeString { get; set; }
        public float GameTypeConfidence { get; set; }   //GameStartingPlayer confidence at the beginning of the game
        public int PointsWon { get; private set; }
        public int PointsLost { get; private set; }
        public int BasicPointsWon { get; private set; }
        public int BasicPointsLost { get; private set; }
        public int MaxHlasWon { get; private set; }
        public int MaxHlasLost { get; private set; }
        public int HlasPointsWasted { get; private set; }
        public bool FinalCardWon { get; private set; }

        public bool GameWon { get; private set; }
        public bool SevenWon { get; private set; }
        public bool QuietSevenWon { get; private set; }
        public bool SevenAgainstWon { get; private set; }
        public bool QuietSevenAgainstWon { get; private set; }
        public bool KilledSeven { get; private set; }
        public bool KilledSevenAgainst { get; private set; }
        public bool HundredWon { get; private set; }
        public bool QuietHundredWon { get; private set; }
        public bool HundredAgainstWon { get; private set; }
        public bool QuietHundredAgainstWon { get; private set; }
        public bool BetlWon { get; private set; }
        public bool DurchWon { get; private set; }

        [XmlIgnore]
        public int GameMoneyWon { get; protected set; }
        [XmlIgnore]
        public int SevenMoneyWon { get; protected set; }
        [XmlIgnore]
        public int QuietSevenMoneyWon { get; protected set; }
        [XmlIgnore]
        public int SevenAgainstMoneyWon { get; protected set; }
        [XmlIgnore]
        public int QuietSevenAgainstMoneyWon { get; protected set; }
        [XmlIgnore]
        public int HundredMoneyWon { get; protected set; }
        [XmlIgnore]
        public int QuietHundredMoneyWon { get; protected set; }
        [XmlIgnore]
        public int HundredAgainstMoneyWon { get; protected set; }
        [XmlIgnore]
        public int QuietHundredAgainstMoneyWon { get; protected set; }
        [XmlIgnore]
        public int BetlMoneyWon { get; protected set; }
        [XmlIgnore]
        public int DurchMoneyWon { get; protected set; }

        public int GameValue { get; private set; }
        public int SevenValue { get; private set; }
        public int QuietSevenValue { get; private set; }
        public int HundredValue { get; private set; }
        public int QuietHundredValue { get; private set; }
        public int BetlValue { get; private set; }
        public int DurchValue { get; private set; }

        [XmlArray]
        public int[] MoneyWon { get; set; }
        [XmlIgnore]
        public float SimulatedSuccessRate { get; set; } //HumanPlayer confidence at the beginning of the game. Set from MainScene

        public int GameId { get; set; }
        [XmlIgnore]
        public bool GameIdSpecified { get { return GameId > 0; } }
        public bool ShouldSerializeNewFileName() { return GameId > 0; }

        [XmlIgnore]
        public Barva? Trumf;
        [XmlAttribute(AttributeName = "Trumf")]
        public Barva TrumfValue
        {
            get { return Trumf.HasValue ? Trumf.Value : default(Barva); }
            set { Trumf = value; }
        }
        [XmlIgnore]
        public bool TrumfValueSpecified { get { return Trumf.HasValue; } }
        public bool ShouldSerializeTrumfValue() { return Trumf.HasValue; }

        public bool IsArchived
        {
            get
            {
                return !((GameMoneyWon == 1 &&  //hra bez fleku
                          SevenMoneyWon == 0 &&
                          SevenAgainstMoneyWon == 0 &&
                          HundredMoneyWon == 0 &&
                          HundredAgainstMoneyWon == 0 &&
                          BetlMoneyWon == 0 &&
                          DurchMoneyWon == 0) ||
                         (GameMoneyWon == -2 && //sedma bez fleku s flekem na hru
                          SevenMoneyWon == 2 &&
                          SevenAgainstMoneyWon == 0 &&
                          HundredMoneyWon == 0 &&
                          HundredAgainstMoneyWon == 0 &&
                          BetlMoneyWon == 0 &&
                          DurchMoneyWon == 0));
            }
        }

        protected NumberFormatInfo _nfi;

        //Default constructor for XmlSerialize purposes
        [Preserve]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public MoneyCalculatorBase()
        {
            PlayerNames = new[] { "Hráč1", "Hráč2", "Hráč3" };
            BaseBet = 1f;
			MaxWin = 500;
            _nfi = new CultureInfo("cs-CZ").NumberFormat;
            SimulatedSuccessRate = -1;
            GameValue = 1;
            QuietSevenValue = 1;
            SevenValue = 2;
            QuietHundredValue = 2;
            HundredValue = 4;
            BetlValue = 5;
            DurchValue = 10;
            Calculate107Separately = true;
            HlasConsidered = HlasConsidered.Highest;
        }

        //vola se na konci hry
        protected MoneyCalculatorBase(Game g, NumberFormatInfo nfi = null)//CultureInfo ci = null)
        {
            _gameStartingPlayerIndex = g.GameStartingPlayerIndex;
            _trump = g.trump;
            _bidding = g.Bidding;
            _gameType = g.GameType;
            GameTypeString = _gameType.ToDescription();
            GameTypeConfidence = g.GameTypeConfidence;
            PlayerNames = g.players.Select(i => i.Name).ToArray();
            BaseBet = g.BaseBet;
            Locale = g.Locale;
			MaxWin = g.MaxWin;
            GamePlayed = g.rounds[0] != null;
            GivenUp = g.GivenUp;
            SimulatedSuccessRate = -1;
            GameValue = g.GameValue;
            QuietSevenValue = g.QuietSevenValue;
            SevenValue = g.SevenValue;
            QuietHundredValue = g.QuietHundredValue;
            HundredValue = g.HundredValue;
            BetlValue = g.BetlValue;
            DurchValue = g.DurchValue;
            Calculate107Separately = g.Calculate107Separately;
            CountHlasAgainst = g.CountHlasAgainst;
            HlasConsidered = g.HlasConsidered;

            if (nfi == null)
            {
                _nfi = nfi;
            }
            if (GoodGame)
            {
                var score = new int[Game.NumPlayers];
                var basicScore = new int[Game.NumPlayers];
                var maxHlasScore = new int[Game.NumPlayers];
                var totalHlasScore = new int[Game.NumPlayers];

                foreach (var r in g.rounds.Where(i => i != null))
                {
                    score[r.player1.PlayerIndex] += r.points1;
                    score[r.player2.PlayerIndex] += r.points2;
                    score[r.player3.PlayerIndex] += r.points3;

                    basicScore[r.player1.PlayerIndex] += r.basicPoints1;
                    basicScore[r.player2.PlayerIndex] += r.basicPoints2;
                    basicScore[r.player3.PlayerIndex] += r.basicPoints3;

                    if (r.hlas1)
                    {
                        totalHlasScore[r.player1.PlayerIndex] += r.hlasPoints1;
                        if ((HlasConsidered == HlasConsidered.Highest &&
                             r.hlasPoints1 > maxHlasScore[r.player1.PlayerIndex]) ||
                            (HlasConsidered == HlasConsidered.First && 
                             maxHlasScore[r.player1.PlayerIndex] == 0))
                        {
                            maxHlasScore[r.player1.PlayerIndex] = r.hlasPoints1;
                        }
                    }
                    if (r.hlas2)
                    {
                        totalHlasScore[r.player2.PlayerIndex] += r.hlasPoints2;
                        if ((HlasConsidered == HlasConsidered.Highest &&
                             r.hlasPoints2 > maxHlasScore[r.player2.PlayerIndex]) ||
                            (HlasConsidered == HlasConsidered.First && 
                             maxHlasScore[r.player2.PlayerIndex] == 0))
                        {
                            maxHlasScore[r.player2.PlayerIndex] = r.hlasPoints2;
                        }
                    }
                    if (r.hlas3)
                    {
                        totalHlasScore[r.player3.PlayerIndex] += r.hlasPoints3;
                        if ((HlasConsidered == HlasConsidered.Highest &&
                             r.hlasPoints3 > maxHlasScore[r.player3.PlayerIndex]) ||
                            (HlasConsidered == HlasConsidered.First && 
                             maxHlasScore[r.player3.PlayerIndex] == 0))
                        {
                            maxHlasScore[r.player3.PlayerIndex] = r.hlasPoints3;
                        }
                    }
                }
                if (HlasConsidered == HlasConsidered.Each)
                {
                    for (var i = 0; i < Game.NumPlayers; i++)
                    {
                        maxHlasScore[i] = totalHlasScore[i];
                    }
                }
                var talonPoints = GamePlayed
                                    ? 10 * g.talon.Count(i => i.Value == Hodnota.Eso ||
                                                              i.Value == Hodnota.Desitka)
                                    : 0;
                
                PointsWon = score[_gameStartingPlayerIndex];
                PointsLost = score[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] + score[(_gameStartingPlayerIndex + 2) % Game.NumPlayers] + talonPoints;

                BasicPointsWon = basicScore[_gameStartingPlayerIndex];
                BasicPointsLost = basicScore[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] + basicScore[(_gameStartingPlayerIndex + 2) % Game.NumPlayers] + talonPoints;

                MaxHlasWon = maxHlasScore[_gameStartingPlayerIndex];
                MaxHlasLost = Math.Max(maxHlasScore[(_gameStartingPlayerIndex + 1) % Game.NumPlayers], maxHlasScore[(_gameStartingPlayerIndex + 2) % Game.NumPlayers]);

                var finalRound = g.rounds[Game.NumRounds - 1];
                if (finalRound != null) //hra se hrala
                {
                    var lastWinningCard = Round.WinningCard(finalRound.c1, finalRound.c2, finalRound.c3, g.trump);

                    GameWon = PointsWon > PointsLost;
                    FinalCardWon = finalRound.roundWinner == g.GameStartingPlayer;

                    SevenWon = FinalCardWon &&
                               lastWinningCard.Suit == g.trump.Value &&
                               lastWinningCard.Value == Hodnota.Sedma;
                    QuietSevenWon = SevenWon && (_gameType & Hra.Sedma) == 0;

                    SevenAgainstWon = !FinalCardWon &&
                    lastWinningCard.Suit == g.trump.Value &&
                    lastWinningCard.Value == Hodnota.Sedma;
                    QuietSevenAgainstWon = SevenAgainstWon && (_gameType & Hra.SedmaProti) == 0;

                    var playerIndexWithKilledSeven = GetPlayerIndexWithKilledSeven(finalRound);
                    var killedSeven = playerIndexWithKilledSeven >= 0;

                    KilledSeven = killedSeven && playerIndexWithKilledSeven == g.GameStartingPlayerIndex;
                    KilledSevenAgainst = killedSeven && playerIndexWithKilledSeven != g.GameStartingPlayerIndex;
                }
                else //hra se nehrala
                {
                    //neflekovana hra se bere jako vyhrana, flekovana hra pri sedme se bere jako prohrana
                    GameWon = !g.GivenUp && 
                              _bidding.GameMultiplier == 1;
                    SevenWon = (g.GameType & Hra.Sedma) != 0 &&
                               _bidding.SevenMultiplier == 1;
                    QuietSevenWon = false;
                    SevenAgainstWon = false;
                    QuietSevenAgainstWon = false;
                    KilledSeven = false;
                    KilledSevenAgainst = false;
                }

                QuietHundredWon = PointsWon >= 100 && (_gameType & Hra.Kilo) == 0;
                QuietHundredAgainstWon = PointsLost >= 100 && (_gameType & Hra.KiloProti) == 0;

                HundredWon = BasicPointsWon + MaxHlasWon >= 100;
                HundredAgainstWon = BasicPointsLost + MaxHlasLost >= 100;

                HlasPointsWasted = 0;
                if ((g.GameType & Hra.Kilo) != 0 && !HundredWon)
                {
                    PointsWon = BasicPointsWon + MaxHlasWon;
                    HlasPointsWasted = totalHlasScore[_gameStartingPlayerIndex] - maxHlasScore[_gameStartingPlayerIndex];
                }
                if ((g.GameType & Hra.KiloProti) != 0 && !HundredAgainstWon)
				{
					PointsLost = BasicPointsLost + MaxHlasLost;
                    HlasPointsWasted = totalHlasScore[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] +
                                       totalHlasScore[(_gameStartingPlayerIndex + 2) % Game.NumPlayers] -
                                       Math.Max(maxHlasScore[(_gameStartingPlayerIndex + 1) % Game.NumPlayers],
                                                maxHlasScore[(_gameStartingPlayerIndex + 2) % Game.NumPlayers]);
				}
                if (!Calculate107Separately)
                {
                    if (_gameType == (Hra.Kilo | Hra.Sedma) &&
                        (!HundredWon || !SevenWon))
                    {
                        HundredWon = false;
                        SevenWon = false;
                    }
                    if ((_gameType & Hra.KiloProti) != 0 &&
                        (_gameType & Hra.SedmaProti) != 0 &&
                        (!HundredAgainstWon || !SevenAgainstWon))
                    {
                        HundredWon = false;
                        SevenWon = false;
                    }
                }
            }
            else //bad game
            {
                if (g.GameType == Hra.Betl)
                {
                    BetlWon = g.rounds.All(r => r != null && r.roundWinner != g.GameStartingPlayer);
                }
                else
                {
                    DurchWon = g.rounds.All(r => r != null && r.roundWinner == g.GameStartingPlayer);
                }
            }
            MoneyWon = new int[Game.NumPlayers];
        }

        protected MoneyCalculatorBase(Game g, Bidding bidding, GameComputationResult res)
            : this(g.GameType, g.trump, g.GameStartingPlayerIndex, bidding, g, g.Calculate107Separately, g.HlasConsidered, g.CountHlasAgainst, res)
        {
        }

        //vola se na konci simulace
        protected MoneyCalculatorBase(Hra gameType, Barva? trump, int gameStartingPlayerIndex, Bidding bidding, IGameTypeValues values, 
        bool calculate107Separately, HlasConsidered hlasConsidered, bool countHlasAgainst, GameComputationResult res)
            : this()
        {
            _gameType = gameType;
            _trump = trump;
            GameTypeString = _gameType.ToDescription();
            _bidding = bidding;
            _gameStartingPlayerIndex = gameStartingPlayerIndex;
            SimulatedSuccessRate = -1;
            GameValue = values.GameValue;
            QuietSevenValue = values.QuietSevenValue;
            SevenValue = values.SevenValue;
            QuietHundredValue = values.QuietHundredValue;
            HundredValue = values.HundredValue;
            BetlValue = values.BetlValue;
            DurchValue = values.DurchValue;
            Calculate107Separately = calculate107Separately;
            CountHlasAgainst = countHlasAgainst;
            HlasConsidered = hlasConsidered;
            GamePlayed = true;

            if (GoodGame)
            {
                PointsWon = res.Score[_gameStartingPlayerIndex];
                PointsLost = res.Score[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] + res.Score[(_gameStartingPlayerIndex + 2) % Game.NumPlayers];
                BasicPointsWon = res.BasicScore[_gameStartingPlayerIndex];
                BasicPointsLost = res.BasicScore[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] + res.BasicScore[(_gameStartingPlayerIndex + 2) % Game.NumPlayers];
                MaxHlasWon = res.MaxHlasScore[_gameStartingPlayerIndex];
                MaxHlasLost = res.MaxHlasScore[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] + res.MaxHlasScore[(_gameStartingPlayerIndex + 2) % Game.NumPlayers];
                GameWon = PointsWon > PointsLost;

                var finalRound = res.Rounds[Game.NumRounds - 1];
                var lastWinningCard = Round.WinningCard(finalRound.c1, finalRound.c2, finalRound.c3, trump);

                FinalCardWon = finalRound.RoundWinnerIndex == _gameStartingPlayerIndex;
                SevenWon = res.Final7Won.HasValue && res.Final7Won.Value;
                QuietSevenWon = SevenWon && (_gameType & Hra.Sedma) == 0;
                SevenAgainstWon = trump.HasValue &&
                                  !FinalCardWon &&
                                  lastWinningCard.Suit == trump.Value &&
                                  lastWinningCard.Value == Hodnota.Sedma;
                QuietSevenAgainstWon = SevenAgainstWon && (_gameType & Hra.SedmaProti) == 0;

                var playerIndexWithKilledSeven = GetPlayerIndexWithKilledSeven(finalRound);
                var killedSeven = playerIndexWithKilledSeven >= 0;

                KilledSeven = killedSeven && playerIndexWithKilledSeven == gameStartingPlayerIndex;
                KilledSevenAgainst = killedSeven && playerIndexWithKilledSeven != gameStartingPlayerIndex;

                QuietHundredWon = PointsWon >= 100 && (_gameType & Hra.Kilo) == 0;
                QuietHundredAgainstWon = PointsLost >= 100 && (_gameType & Hra.KiloProti) == 0;

                HundredWon = BasicPointsWon + MaxHlasWon >= 100;
                HundredAgainstWon = BasicPointsLost + MaxHlasLost >= 100;

                HlasPointsWasted = 0;
                if ((_gameType & Hra.Kilo) != 0 && !HundredWon)
                {
                    HlasPointsWasted = res.TotalHlasScore[_gameStartingPlayerIndex] - res.MaxHlasScore[_gameStartingPlayerIndex];
                }
                if ((_gameType & Hra.KiloProti) != 0 && !HundredAgainstWon)
                {
                    HlasPointsWasted = res.TotalHlasScore[(_gameStartingPlayerIndex + 1) % Game.NumPlayers] +
                                       res.TotalHlasScore[(_gameStartingPlayerIndex + 2) % Game.NumPlayers] -
                                       Math.Max(res.MaxHlasScore[(_gameStartingPlayerIndex + 1) % Game.NumPlayers],
                                                res.MaxHlasScore[(_gameStartingPlayerIndex + 2) % Game.NumPlayers]);
                }

                if (!Calculate107Separately)
                {
                    if (_gameType == (Hra.Kilo | Hra.Sedma) &&
                        (!HundredWon || !SevenWon))
                    {
                        HundredWon = false;
                        SevenWon = false;
                    }
                    if ((_gameType & Hra.KiloProti) != 0 &&
                        (_gameType & Hra.SedmaProti) != 0 &&
                        (!HundredAgainstWon || !SevenAgainstWon))
                    {
                        HundredWon = false;
                        SevenWon = false;
                    }
                }
            }
            else
            {
                DurchWon = res.Rounds.All(i => i.RoundWinnerIndex == _gameStartingPlayerIndex);
                BetlWon = res.Rounds.All(i => i.RoundWinnerIndex != _gameStartingPlayerIndex);
            }
            MoneyWon = new int[Game.NumPlayers];
        }

        public abstract void CalculateMoney();

        public override string ToString()
        {
            var sb = new StringBuilder();

            if (!GamePlayed)
            {
                if (GivenUp)
                {
                    sb.Append("Zahozená hra\t»»»»»»\n");
                }
                else
                {
                    sb.Append("Nehrálo se\t»»»»»»\n");
                }
            }
            else
            {
                if (MoneyWon[0] > 0)
                {
                    sb.Append("Vyhráls!\t»»»»»»\n");
                }
                else if (MoneyWon[0] < 0)
                {
                    sb.Append("Prohráls!\t»»»»»»\n");
                }
                else
                {
                    sb.Append("Plichta\t»»»»»»\n");
                }
            }
            var items = 0;
            if (!Calculate107Separately &&
                _gameType == (Hra.Kilo | Hra.Sedma))
            {
                var won = HundredWon;
                var multiplier = _bidding.GameMultiplier;
                var money = HundredMoneyWon + SevenMoneyWon;
                var status = won ? "Vyhranejch" : "Prohranejch";
                var other = string.Empty;
                var score = string.Empty;
                var gtString = "stosedm";
                score = string.Format("Skóre: {0}:{1}{2}\n",
                                      PointsWon,
                                      PointsLost,
                                      won || 100 - (BasicPointsWon + MaxHlasWon) <= 0
                                      ? string.Empty
                                      : string.Format("\nDo kila schází: {0} bodů{1}{2}", 100 - (BasicPointsWon + MaxHlasWon),
                                                      HlasPointsWasted == 0
                                                      ? string.Empty
                                                      : string.Format("\nPropadlé hlasy: {0} bodů", HlasPointsWasted),
                                                      PointsLost == BasicPointsLost || !CountHlasAgainst
                                                      ? string.Empty
                                                      : string.Format("\nHlasy proti: {0} bodů", PointsLost - BasicPointsLost)));
                sb.AppendFormat("{0}: {1} {2}{3}{4}\t{5}\n{6}",
                    PlayerNames[_gameStartingPlayerIndex],
                    status,
                    gtString,
                    multiplier > 1 ? string.Format(" ({0}x flek)", MultiplierToDoubleCount(multiplier)) : string.Empty,
                    other,
                    (money * BaseBet).ToString("C", _nfi),
                    score);
                items++;
            }
            foreach (var gt in Enum.GetValues(typeof(Hra)).Cast<Hra>().Where(i => (_gameType & i) != 0))
            {
                var won = false;
                var multiplier = 0;
                var genre = Genre.Masculine;
                var status = string.Empty;
                var other = string.Empty;
                var score = string.Empty;
                var money = 0;
                var gtString = gt.ToString().ToLower();

                switch (gt)
                {
                    case Hra.Hra:
                        won = GameWon;
                        genre = Genre.Feminine;
                        multiplier = (_gameType & Hra.KiloProti) == 0 ? _bidding.GameMultiplier : 0;
                        money = GameMoneyWon;
                        score = string.Format("{0}Skóre: {1}:{2}\t \n", QuietHundredWon
                                                                        ? "Tichý kilo, "
                                                                        : QuietHundredAgainstWon
                                                                            ? "Tichý kilo proti, "
                                                                            : string.Empty,
                                                                        PointsWon, PointsLost);
                        break;
                    case Hra.Sedma:
                        won = SevenWon;
                        genre = Genre.Feminine;
                        multiplier = _bidding.SevenMultiplier;
                        money = SevenMoneyWon;// + KilledSevenMoneyWon;
                        other = KilledSeven ? "\nzabitá" : string.Empty;
                        break;
                    case Hra.Kilo:
                        won = HundredWon;
                        genre = Genre.Neutral;
                        multiplier = _bidding.GameMultiplier;
                        money = HundredMoneyWon;
                        score = string.Format("Skóre: {0}:{1}{2}\n", 
                                              PointsWon, 
                                              PointsLost, 
                                              won || 100 - (BasicPointsWon + MaxHlasWon) <= 0
                                              ? string.Empty 
                                              : string.Format("\nDo kila schází: {0} bodů{1}{2}", 100 - (BasicPointsWon + MaxHlasWon),
                                                              HlasPointsWasted == 0
                                                              ? string.Empty
                                                              : string.Format("\nPropadlé hlasy: {0} bodů", HlasPointsWasted),
                                                              PointsLost == BasicPointsLost || !CountHlasAgainst
                                                              ? string.Empty
                                                              : string.Format("\nHlasy proti: {0} bodů", PointsLost - BasicPointsLost)));
                        break;
                    case Hra.SedmaProti:                        
                        won = SevenAgainstWon;
                        genre = Genre.Feminine;
                        gtString = "sedma proti";
                        multiplier = _bidding.SevenAgainstMultiplier;
                        money = SevenAgainstMoneyWon;// + KilledSevenAgainstMoneyWon;
                        other = KilledSevenAgainst ? "\nzabitá" : string.Empty;
                        break;
                    case Hra.KiloProti:
                        won = HundredAgainstWon;
                        genre = Genre.Neutral;
                        gtString = "kilo proti";
                        multiplier = _bidding.HundredAgainstMultiplier;
                        money = HundredAgainstMoneyWon;
                        score = won || 100 - (BasicPointsLost + MaxHlasLost) <= 0
                                ? string.Empty
                                : string.Format("Do kila schází: {0} bodů{1}{2}\n", 100 - (BasicPointsLost + MaxHlasLost),
                                                  HlasPointsWasted == 0
                                                  ? string.Empty
                                                  : string.Format("\nPropadlé hlasy: {0} bodů", HlasPointsWasted),
                                                  PointsWon == BasicPointsWon || !CountHlasAgainst
                                                  ? string.Empty
                                                  : string.Format("\nHlasy proti: {0} bodů", PointsWon - BasicPointsWon));
                        break;
                    case Hra.Betl:
                        won = BetlWon;
                        genre = Genre.Masculine;
                        multiplier = _bidding.BetlDurchMultiplier;
                        money = BetlMoneyWon;
                        break;
                    case Hra.Durch:
                        won = DurchWon;
                        genre = Genre.Masculine;
                        multiplier = _bidding.BetlDurchMultiplier;
                        money = DurchMoneyWon;
                        break;
                }
                switch (genre)
                {
                    case Genre.Masculine:
                        status = won ? "Vyhranej" : "Prohranej";
                        break;
                    case Genre.Feminine:
                        status = won ? "Vyhraná" : "Prohraná";
                        break;
                    case Genre.Neutral:
                        status = won ? "Vyhraný" : "Prohraný";
                        break;
                }

                if ((_gameType & Hra.KiloProti) != 0 &&
                    (_gameType & Hra.SedmaProti) != 0 &&
                    !Calculate107Separately &&
                    //HundredAgainstMoneyWon * SevenAgainstMoneyWon > 0 &&
                    (gt == Hra.KiloProti ||
                     gt == Hra.SedmaProti))
                {
                    continue;
                }
                if (!Calculate107Separately &&
                    _gameType == (Hra.Kilo | Hra.Sedma) &&
                    (gt == Hra.Kilo ||
                     gt == Hra.Sedma))
                {
                    continue;
                }
                if (gt == Hra.SedmaProti)
                {
                    if ((_gameType & Hra.KiloProti) == 0 ||
                        HundredAgainstMoneyWon * SevenAgainstMoneyWon < 0)
                    {
                        if (_bidding.AllPlayerBids.Any(i => (i & Hra.SedmaProti) != 0))
                        {
                            var playerIndex = _bidding.AllPlayerBids.Select((i, idx) => new { value = i, idx = idx })
                                                                    .Where(i => (i.value & Hra.SedmaProti) != 0 &&
                                                                                i.idx != _gameStartingPlayerIndex)
                                                                    .Select(i => i.idx)
                                                                    .FirstOrDefault();
                            if (playerIndex == _gameStartingPlayerIndex)
                            {
                                playerIndex = (playerIndex + 1) % Game.NumPlayers;  //sedmu proti nemohl hlasit akter
                            }
                            sb.AppendFormat("{0}: ", PlayerNames[playerIndex]);
                        }
                    }
                }
                else if (gt == Hra.KiloProti)
                {
                    if ((_gameType & Hra.SedmaProti) == 0 ||
                        HundredAgainstMoneyWon * SevenAgainstMoneyWon < 0)
                    {
                        if (_bidding.PlayerBids.Any(i => (i & Hra.KiloProti) != 0))
                        {
                            var playerIndex = _bidding.PlayerBids.Select((i, idx) => new { value = i, idx = idx })
                                                                 .Where(i => (i.value & Hra.KiloProti) != 0 &&
                                                                             i.idx != _gameStartingPlayerIndex)
                                                                 .Select(i => i.idx)
                                                                 .FirstOrDefault();
                            if (playerIndex == _gameStartingPlayerIndex)
                            {
                                playerIndex = (playerIndex + 1) % Game.NumPlayers;  //kilo proti nemohl hlasit akter
                            }
                            sb.AppendFormat("{0}: ", PlayerNames[playerIndex]);
                        }
                    }
                }
                else
                {
                    sb.AppendFormat("{0}: ", PlayerNames[_gameStartingPlayerIndex]);
                }
                sb.AppendFormat("{0} {1}{2}{3}\t{4}\n{5}",
                    status,
                    gtString,
                    multiplier > 1 ? string.Format(" ({0}x flek)", MultiplierToDoubleCount(multiplier)) : string.Empty,
                    other,
                    (money * BaseBet).ToString("C", _nfi),
                    score);
                items++;
            }
            if ((_gameType & Hra.KiloProti) != 0 &&
                (_gameType & Hra.SedmaProti) != 0 &&
                !Calculate107Separately)
                //HundredAgainstMoneyWon * SevenAgainstMoneyWon > 0) //pokud jsem bud oboje vyhral nebo oboje prohral
            {
                var won = HundredAgainstWon;
                var multiplier = _bidding.HundredAgainstMultiplier;
                var money = HundredAgainstMoneyWon + SevenAgainstMoneyWon;
                var status = won ? "Vyhranejch" : "Prohranejch";
                var other = string.Empty;
                var score = string.Empty;
                var gtString = "stosedm proti";
                score = won || 100 - (BasicPointsLost + MaxHlasLost) <= 0
                        ? string.Empty
                        : string.Format("Do kila schází: {0} bodů{1}{2}\n", 100 - (BasicPointsLost + MaxHlasLost),
                                          HlasPointsWasted == 0
                                          ? string.Empty
                                          : string.Format("\nPropadlé hlasy: {0} bodů", HlasPointsWasted),
                                          PointsWon == BasicPointsWon || !CountHlasAgainst
                                          ? string.Empty
                                          : string.Format("\nHlasy proti: {0} bodů", PointsWon - BasicPointsWon));
                sb.AppendFormat("{0} {1}{2}{3}\t{4}\n{5}",
                    status,
                    gtString,
                    multiplier > 1 ? string.Format(" ({0}x flek)", MultiplierToDoubleCount(multiplier)) : string.Empty,
                    other,
                    (money * BaseBet).ToString("C", _nfi),
                    score);
                items++;
            }
            if (QuietSevenWon)
            {
                sb.AppendFormat("Tichá sedma\t{0}\n", (QuietSevenMoneyWon * BaseBet).ToString("C", _nfi));
                items++;
            }
            if (KilledSeven && (_gameType & Hra.Sedma) == 0)
            {
                sb.AppendFormat("Tichá sedma zabitá\t{0}\n", (QuietSevenMoneyWon * BaseBet).ToString("C", _nfi));
                items++;
            }
            if (QuietSevenAgainstWon)
            {
                sb.AppendFormat("Tichá sedma proti\t{0}\n", (QuietSevenAgainstMoneyWon * BaseBet).ToString("C", _nfi));
                items++;
            }
            if (KilledSevenAgainst && (_gameType & Hra.SedmaProti) == 0)
            {
                sb.AppendFormat("Tichá sedma proti zabitá\t{0}\n", (QuietSevenAgainstMoneyWon * BaseBet).ToString("C", _nfi));
                items++;
            }
            if (_trump.HasValue)
            {
                if (_trump.Value == Barva.Cerveny)
                {
                    sb.AppendFormat("V červenejch:\t{0}\n", (MoneyWon[_gameStartingPlayerIndex] / 2 * BaseBet).ToString("C", _nfi));
                }
                else if (items > 1)
                {
                    sb.AppendFormat("Celkem:\t{0}\n", (MoneyWon[_gameStartingPlayerIndex] / 2 * BaseBet).ToString("C", _nfi));
                }
            }
            for (var i = 0; i < Game.NumPlayers; i++)
            {
                sb.AppendFormat("\n{0}:\t{1}",
                    PlayerNames[(_gameStartingPlayerIndex + i) % Game.NumPlayers], (MoneyWon[(_gameStartingPlayerIndex + i) % Game.NumPlayers] * BaseBet).ToString("C", _nfi));
            }

            return sb.ToString();
        }

        private int MultiplierToDoubleCount(int multiplier)
        {
            for (var i = 0; multiplier != 0; i++, multiplier >>= 1)
            {
                if ((multiplier & 1) != 0)
                {
                    return i;
                }
            }

            return 0;
        }

        private int GetPlayerIndexWithKilledSeven(Round finalRound)
        {
            if (!_trump.HasValue)
            {
                return -1;
            }

            var winningCard = Round.WinningCard(finalRound.c1, finalRound.c2, finalRound.c3, _trump);
            var finalCards = new[] { finalRound.c1, finalRound.c2, finalRound.c3 };
            var players = new[] { finalRound.player1, finalRound.player2, finalRound.player3 };
            var playerWithTrumpSeven = finalCards.ToList()
                                                 .FindIndex(i => i.Suit == _trump.Value &&
                                                                 i.Value == Hodnota.Sedma);

            if (playerWithTrumpSeven >= 0 && winningCard.Value != Hodnota.Sedma)
            {
                playerWithTrumpSeven = (finalRound.player1.PlayerIndex + playerWithTrumpSeven) % Game.NumPlayers;

                return playerWithTrumpSeven;
            }
            return -1;
        }

        private int GetPlayerIndexWithKilledSeven(RoundDebugContext finalRound)
        {
            if (!_trump.HasValue)
            {
                return -1;
            }

            var winningCard = Round.WinningCard(finalRound.c1, finalRound.c2, finalRound.c3, _trump);
            var finalCards = new[] { finalRound.c1, finalRound.c2, finalRound.c3 };
            var playerWithTrumpSeven = finalCards.ToList()
                                                 .FindIndex(i => i.Suit == _trump.Value &&
                                                                 i.Value == Hodnota.Sedma);

            if (playerWithTrumpSeven >= 0 && winningCard.Value != Hodnota.Sedma)
            {
                playerWithTrumpSeven = (finalRound.RoundStarterIndex + playerWithTrumpSeven) % Game.NumPlayers;

                return playerWithTrumpSeven;
            }
            return -1;
        }

        enum Genre
        {
            Masculine,
            Feminine,
            Neutral
        }
    }
}
