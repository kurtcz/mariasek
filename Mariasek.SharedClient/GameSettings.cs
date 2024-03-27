using System;
using Microsoft.Xna.Framework;
using Mariasek.Engine;
using System.Xml.Serialization;
using System.Linq;
using System.Globalization;
#if __IOS__
using PreserveAttribute = Foundation.PreserveAttribute;
#endif

namespace Mariasek.SharedClient
{
    #region test
    public enum CardFace
    {
        Single,
        Double,
        Pikety
    }
    #endregion

    public enum CardBackSide
    {
        Tartan,
        Horse,
        Lace,
        Pikety
    }

    public enum BackgroundImage
    {
        Default,
        Canvas,
        Dark
    }

    public enum ShuffleTrigger
    {
        Never,
        AfterAutomaticVictory,
        Daily,
        AfterRestart,
        Always
    }

    public enum DirectionOfPlay
    {
        Clockwise,
        Counterclockwise
    }

    public enum TrumpSorting
    {
        Random,
        Left,
        Right
    }

    public class BidThresholdSettings
    {
        public Hra GameType { get; set; }
        public bool Use { get; set; }
        public int MaxBidCount { get; set; }
        public string Thresholds { get; set; }
    }

    public class GameSettings
    {    
        public bool? Default { get; set; }
        public bool? ShowRatingOffer { get; set; }
        public bool ShouldSerializeShowRatingOffer() { return ShowRatingOffer.HasValue && !ShowRatingOffer.Value; }
        public bool LogProbabilities { get; set; }
        public bool? AiCheating { get; set; }
        public bool ShouldSerializeAiCheating() { return AiCheating.HasValue && AiCheating.Value; }
        public bool? TestMode { get; set; }
        public bool DoLog { get; set; }
        public bool ShouldSerializeTestMode() { return TestMode.HasValue && TestMode.Value; }
        public bool HintEnabled { get; set; }
        public bool SoundEnabled { get; set; }
        public bool BgSoundEnabled { get; set; }
        public bool CheerAndBooSoundEnabled { get; set; }
        public int BgMusicMinDelayMs { get; set; }
        public int BgMusicMaxDelayMs { get; set; }
        public int HistoryDaysToShow { get; set; }
        public string[] PlayerNames { get; set; }
        public SortMode SortMode { get; set; }
        public TrumpSorting TrumpSort { get; set; }
        public bool NaturalSort { get; set; }
        public ShuffleTrigger WhenToShuffle { get; set; }
        public bool AutoSort { get; set; }
        public bool AutoPlaySingletonCard { get; set; }
        public float BaseBet { get; set; }
        public string Locale { get; set; }
        public int MaxWin { get; set; }
        public int GameValue { get; set; }
        public int QuietSevenValue { get; set; }
        public int SevenValue { get; set; }
        public int QuietHundredValue { get; set; }
        public int HundredValue { get; set; }
        public int BetlValue { get; set; }
        public int DurchValue { get; set; }
        public bool AllowFakeSeven { get; set; }
        public bool AllowFake107 { get; set; }
        public bool AllowAXTalon { get; set; }
        public bool AllowTrumpTalon { get; set; }
        public bool AllowAIAutoFinish { get; set; }
        public bool AllowPlayerAutoFinish { get; set; }
        public bool OptimisticAutoFinish { get; set; }
        public int ThinkingTimeMs { get; set; }
        public int BubbleTimeMs { get; set; }
        public int MaxHistoryLength { get; set; }
        public int MaxHistoryTableLength { get; set; }
        public DirectionOfPlay DirectionOfPlay { get; set; }
        public bool AiMayGiveUp { get; set; }
        public bool PlayerMayGiveUp { get; set; }
        public bool KeepScreenOn { get; set; }                  //xml-only
        public CalculationStyle CalculationStyle { get; set; }
        public bool CountHlasAgainst { get; set; }
        public int GameTypeSimulationsPerSecond { get; set; }
        public int RoundSimulationsPerSecond { get; set; }
        public int CurrentStartingPlayerIndex { get; set; }
        public CardFace CardDesign { get; set; }
        public CardBackSide CardBackSide { get; set; }
        public BidThresholdSettings[] Thresholds { get; set; }
        public float RiskFactor { get; set; }
        public float RiskFactorSevenDefense { get; set; }    //xml-only
        public float SolitaryXThreshold { get; set; }           //xml-only
        public float SolitaryXThresholdDefense { get; set; }    //xml-only
        public int SafetyGameThreshold { get; set; }            //xml-only
        public int SafetyHundredThreshold { get; set; }            //xml-only
        public int SafetyBetlThreshold { get; set; }            //xml-only
        public int FirstMinMaxRound { get; set; }
        public int RoundFinishedWaitTimeMs { get; set; }
        public bool AutoFinishRounds { get; set; }
        public bool AutoFinish { get; set; }
        public int MinimalBidsForGame { get; set; }
        public int MinimalBidsForSeven { get; set; }
        public bool PlayZeroSumGames { get; set; }
        public bool MandatoryDouble { get; set; }
        public bool Top107 { get; set; }
        public bool Calculate107Separately { get; set; }
        public HlasConsidered HlasConsidered { get; set; }
        public int MaxDegreeOfParallelism { get; set; }
        public bool ShowStatusBar { get; set; }                 //xml-only
        public bool ShowScoreDuringGame { get; set; }
        public bool WhiteScore { get; set; }
        public bool AutoDisable100Against { get; set; }
        public float CardScaleFactor { get; set; }
        public bool FatFingers { get; set; }
        public BackgroundImage BackgroundImage { get; set; }
        public bool InvertedToggleButton { get; set; }
        public string DefaultTextRgba { get; set; }             //xml-only
        public Color DefaultTextColor { get { return FromRgbaString(DefaultTextRgba); } }
        public string HighlightedTextRgba { get; set; }         //xml-only
        public Color HighlightedTextColor { get { return FromRgbaString(HighlightedTextRgba); } }
        public string Player1Rgba { get; set; }                 //xml-only
        public Color Player1Color { get { return FromRgbaString(Player1Rgba); } }
        public string Player2Rgba { get; set; }                 //xml-only
        public Color Player2Color { get { return FromRgbaString(Player2Rgba); } }
        public string Player3Rgba { get; set; }                 //xml-only
        public Color Player3Color { get { return FromRgbaString(Player3Rgba); } }
        public string NegativeScoreRgba { get; set; }           //xml-only
        public Color NegativeScoreColor { get { return FromRgbaString(NegativeScoreRgba); } }
        public string PositiveScoreRgba { get; set; }           //xml-only
        public Color PositiveScoreColor { get { return FromRgbaString(PositiveScoreRgba); } }
        public string HintRgba { get; set; }                    //xml-only
        public Color HintColor { get { return FromRgbaString(HintRgba); } }
        public string WinRgba { get; set; }                     //xml-only
        public Color WinColor { get { return FromRgbaString(WinRgba); } }
        public string LossRgba { get; set; }                    //xml-only
        public Color LossColor { get { return FromRgbaString(LossRgba); } }
        public string TieRgba { get; set; }                     //xml-only
        public Color TieColor { get { return FromRgbaString(TieRgba); } }
        public string ButtonRgba { get; set; }                  //xml-only
        public Color ButtonColor { get { return FromRgbaString(ButtonRgba); } }
        public string PressedButtonRgba { get; set; }           //xml-only
        public Color PressedButtonColor { get { return FromRgbaString(PressedButtonRgba); } }
        public string SelectedButtonRgba { get; set; }          //xml-only
        public Color SelectedButtonColor { get { return FromRgbaString(SelectedButtonRgba); } }
        public string SelectedButtonTextRgba { get; set; }             //xml-only
        public Color SelectedButtonTextColor { get { return FromRgbaString(SelectedButtonTextRgba); } }
        public string ReviewPtsWonRgba { get; set; }            //xml-only
        public Color ReviewPtsWonColor { get { return FromRgbaString(ReviewPtsWonRgba); } }
        public string ReviewPtsLostRgba { get; set; }           //xml-only
        public Color ReviewPtsLostColor { get { return FromRgbaString(ReviewPtsLostRgba); } }

        [Preserve]
        public GameSettings()
        {
            DoLog = true;
#if __IOS__
            DoLog = false;
#endif
            HintEnabled = true;
            SoundEnabled = true;
            BgSoundEnabled = true;
            CheerAndBooSoundEnabled = true;
            BgMusicMinDelayMs = 60000;
            BgMusicMaxDelayMs = 120000;
            SortMode = SortMode.Descending;
            TrumpSort = TrumpSorting.Left;
            NaturalSort = false;
            WhenToShuffle = ShuffleTrigger.AfterAutomaticVictory;
            AutoSort = true;
            AutoPlaySingletonCard = false;
            BaseBet = 1f;
            Locale = "cs-CZ";
            MaxWin = 500;
            CalculationStyle = CalculationStyle.Adding;
            CountHlasAgainst = true;
            CurrentStartingPlayerIndex = 0;
            BubbleTimeMs = 1000;
            ThinkingTimeMs = 1500;
            MaxHistoryLength = 0;
            MaxHistoryTableLength = 0;
            DirectionOfPlay = DirectionOfPlay.Clockwise;
            KeepScreenOn = true;
            RoundFinishedWaitTimeMs = 1000;
            AutoFinishRounds = true;
            AutoFinish = true;
            CardDesign = CardFace.Single;
            CardBackSide = CardBackSide.Horse;
            PlayerNames = new [] { "Já", "Karel", "Pepa" };
            MinimalBidsForGame = 1;
            MinimalBidsForSeven = 0;
            PlayZeroSumGames = false;
            MandatoryDouble = false;
            Top107 = false;
            Calculate107Separately = true;
            HlasConsidered = HlasConsidered.Highest;
            AllowFakeSeven = false;
            AllowFake107 = false;
            AllowAXTalon = false;
            AllowTrumpTalon = true;
            AllowAIAutoFinish = true;
            AllowPlayerAutoFinish = true;
            OptimisticAutoFinish = true;
            MaxDegreeOfParallelism = -1;
            ShowStatusBar = false;
            AiMayGiveUp = false;
            PlayerMayGiveUp = false;
            ShowScoreDuringGame = true;
            WhiteScore = false;
            AutoDisable100Against = true;
            CardScaleFactor = 0.6f;
            FatFingers = false;
            BackgroundImage = BackgroundImage.Default;
            InvertedToggleButton = false;
            GameValue = 1;
            QuietSevenValue = 1;
            SevenValue = 2;
            QuietHundredValue = 2;
            HundredValue = 4;
            BetlValue = 5;
            DurchValue = 10;
            FirstMinMaxRound = 8;
            DefaultTextRgba = "ffffff";
            HighlightedTextRgba = "ffdd00";
            Player1Rgba = "ff4c4c";
            Player2Rgba = "34bf49";
            Player3Rgba = "0399e5";
            NegativeScoreRgba = "ff4c4c";
            PositiveScoreRgba = "34bf49";
            HintRgba = "34bf49";
            WinRgba = "34bf49";
            LossRgba = "ff4c4c";
            TieRgba = "0399e5";
            ButtonRgba = "603010";
            PressedButtonRgba = "994d1a";
            SelectedButtonRgba = "006400";
            SelectedButtonTextRgba = "ffffff";
            ReviewPtsWonRgba = "90ee90";
            ReviewPtsLostRgba = "ff90a0";
            ResetThresholds();
        }

        public void ResetThresholds()
        {
            Default = true;

            //celkem jsou ve hre 2 nezname karty v dane barve
            //souper ma 5 z 11 neznamych karet ve hre
            //pravdepodobnost, ze souper nezna ani jednu z 2 neznamych karet v dane barve
            RiskFactor = 0.28f; //0.2727f ~ (9 nad 5) / (11 nad 5)
            SolitaryXThreshold = 0.13f; //pokud mam na zacatku 5 karet, tak P(souper ma plonkovou X) ~ 0.131
            SolitaryXThresholdDefense = 0.5f; //v obrane musi mit prah vyssi hodnotu aby tahali jen kdyz je to skoro jiste
            RiskFactorSevenDefense = 0.5f; //pouziva se u pravidla bodovat nebo vytlacit trumf proti sedme
            SafetyGameThreshold = 48;
            SafetyHundredThreshold = 64;
            SafetyBetlThreshold = 64;
            Thresholds = new []
            {
                new BidThresholdSettings
                {
                    GameType = Hra.Hra,
                    Use = true,
                    MaxBidCount = 3,
                    Thresholds = "5|25|40|50"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.Sedma,
                    Use = true,
                    MaxBidCount = 2,
                    Thresholds = "30|60|85|100"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.Kilo,
                    Use = true,
                    MaxBidCount = 2,
                    Thresholds = "60|90|100|100"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.SedmaProti,
                    Use = true,
                    MaxBidCount = 2,
                    Thresholds = "40|65|85|100"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.KiloProti,
                    Use = true,
                    MaxBidCount = 1,
                    Thresholds = "90|100|100|100"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.Betl,
                    Use = true,
                    MaxBidCount = 2,
                    Thresholds = "60|80|95|100"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.Durch,
                    Use = true,
                    MaxBidCount = 2,
                    Thresholds = "70|90|100|100"
                }
            };
        }

        private static Color FromRgbaString(string str)
        {
            if (str == null ||
                (str.Length != 6 &&
                 str.Length != 8) ||
                !str.All(i => (i >= '0' && i <= '9') || (i >= 'a' && i <= 'f') || (i >= 'A' && i <= 'F')))
            {
                return default(Color);
            }
            var rgba = uint.Parse(str, NumberStyles.HexNumber);
            var r = rgba >> (str.Length == 8 ? 24 : 16);
            var g = (rgba >> (str.Length == 8 ? 16 : 8)) & 0xff;
            var b = (rgba >> (str.Length == 8 ? 8 : 0)) & 0xff;
            var a = str.Length == 8 ? rgba & 0xff : 0xff;

            return new Color((int)r, (int)g, (int)b, (int)a);
        }
    }
}

