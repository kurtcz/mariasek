using System;
using Mariasek.Engine.New;

namespace Mariasek.SharedClient
{
    public enum SortMode
    {
        Ascending,
        Descending,
        None
    }

	public enum CardFace
	{
		Single,
		Double
	}

	public enum CardBackSide
	{
		Tartan,
		Horse,
        Lace
	}

    public enum BackgroundImage
    {
        Default,
        Canvas,
        Dark
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
        public bool? TestMode { get; set; }
        public bool DoLog { get; set; }
        public bool ShouldSerializeTestMode() { return TestMode.HasValue && TestMode.Value; }
        public bool HintEnabled { get; set; }
        public bool SoundEnabled { get; set; }
        public bool BgSoundEnabled { get; set; }
        public string[] PlayerNames { get; set; }
        public SortMode SortMode { get; set; }
        public float BaseBet { get; set; }
        public int ThinkingTimeMs { get; set; }
        public int BubbleTimeMs { get; set; }
        public int MaxHistoryLength { get; set; }
        public bool AiMayGiveUp { get; set; }
        public bool KeepScreenOn { get; set; }
        public CalculationStyle CalculationStyle { get; set; }
        public int GameTypeSimulationsPerSecond { get; set; }
        public int RoundSimulationsPerSecond { get; set; }
        public int CurrentStartingPlayerIndex { get; set; }
        public CardFace CardDesign { get; set; }
        public CardBackSide CardBackSide { get; set; }
        public BidThresholdSettings[] Thresholds { get; set; }
        public float RiskFactor { get; set; }
        public float SolitaryXThreshold { get; set; }
        public int RoundFinishedWaitTimeMs { get; set; }
        public bool AutoFinishRounds { get; set; }
        public bool AutoFinish { get; set; }
        public int MinimalBidsForGame { get; set; }
        public int MinimalBidsForSeven { get; set; }
        public bool Top107 { get; set; }
        public bool ShowStatusBar { get; set; }
        public BackgroundImage BackgroundImage { get; set; }

        public GameSettings()
        {
            DoLog = true;
#if __IOS__
            DoLog = false;
#endif
            HintEnabled = true;
            SoundEnabled = true;
            BgSoundEnabled = true;
            SortMode = SortMode.Descending;
            BaseBet = 1f;
            CalculationStyle = CalculationStyle.Adding;
            CurrentStartingPlayerIndex = 0;
            BubbleTimeMs = 1000;
            ThinkingTimeMs = 1500;
            MaxHistoryLength = 0;
            KeepScreenOn = true;
            RoundFinishedWaitTimeMs = 1000;
            AutoFinishRounds = true;
            AutoFinish = true;
			CardDesign = CardFace.Single;
            CardBackSide = CardBackSide.Horse;
            PlayerNames = new [] { "Já", "Karel", "Pepa" };
            MinimalBidsForGame = 1;
            MinimalBidsForSeven = 0;
            Top107 = false;
            ShowStatusBar = false;
            AiMayGiveUp = true;
            BackgroundImage = BackgroundImage.Default;
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
			Thresholds = new []
            {
                new BidThresholdSettings
                {
                    GameType = Hra.Hra,
                    Use = true,
                    MaxBidCount = 3,
                    Thresholds = "10|25|40|85"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.Sedma,
                    Use = true,
                    MaxBidCount = 2,
                    Thresholds = "35|65|85|95"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.Kilo,
                    Use = true,
                    MaxBidCount = 0,
                    Thresholds = "65|80|95|100"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.SedmaProti,
                    Use = true,
                    MaxBidCount = 0,
                    Thresholds = "35|75|95|100"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.KiloProti,
                    Use = true,
                    MaxBidCount = 0,
                    Thresholds = "95|100|100|100"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.Betl,
                    Use = true,
                    MaxBidCount = 2,
                    Thresholds = "65|70|95|100"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.Durch,
                    Use = true,
                    MaxBidCount = 2,
                    Thresholds = "80|85|100|100"
                }
            };
		}
    }
}

