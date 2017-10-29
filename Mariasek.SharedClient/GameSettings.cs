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
        public bool ShouldSerializeTestMode() { return TestMode.HasValue && TestMode.Value; }
        public bool HintEnabled { get; set; }
        public bool SoundEnabled { get; set; }
        public bool BgSoundEnabled { get; set; }
        public string[] PlayerNames { get; set; }
        public SortMode SortMode { get; set; }
        public float BaseBet { get; set; }
        public int ThinkingTimeMs { get; set; }
        public int BubbleTimeMs { get; set; }
        public CalculationStyle CalculationStyle { get; set; }
        public int GameTypeSimulationsPerSecond { get; set; }
        public int RoundSimulationsPerSecond { get; set; }
        public int CurrentStartingPlayerIndex { get; set; }
        public CardFace CardDesign { get; set; }
        public CardBackSide CardBackSide { get; set; }
        public BidThresholdSettings[] Thresholds { get; set; }
        public float RiskFactor { get; set; }

        public GameSettings()
        {
            HintEnabled = true;
            SoundEnabled = true;
            BgSoundEnabled = true;
            SortMode = SortMode.Descending;
            BaseBet = 1f;
            CalculationStyle = CalculationStyle.Adding;
            CurrentStartingPlayerIndex = 0;
            BubbleTimeMs = 1000;
			CardDesign = CardFace.Single;
            CardBackSide = CardBackSide.Horse;
            PlayerNames = new [] { "Já", "Karel", "Pepa" };
			ResetThresholds();
        }

        public void ResetThresholds()
        {
			Default = true;

			//celkem jsou ve hre 2 nezname karty v dane barve
			//souper ma 5 z 11 neznamych karet ve hre
			//pravdepodobnost, ze souper nezna ani jednu z 2 neznamych karet v dane barve
			RiskFactor = 0.28f; //0.2727f ~ (9 nad 5) / (11 nad 5)
            ThinkingTimeMs = 1500;
			Thresholds = new []
            {
                new BidThresholdSettings
                {
                    GameType = Hra.Hra,
                    Use = true,
                    MaxBidCount = 3,
                    Thresholds = "0|25|40|80"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.Sedma,
                    Use = true,
                    MaxBidCount = 2,
                    Thresholds = "40|65|75|85"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.Kilo,
                    Use = true,
                    MaxBidCount = 1,
                    Thresholds = "65|80|95|100"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.SedmaProti,
                    Use = true,
                    MaxBidCount = 1,
                    Thresholds = "35|80|95|100"
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
                    Thresholds = "60|85|100|100"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.Durch,
                    Use = true,
                    MaxBidCount = 2,
                    Thresholds = "70|85|100|100"
                }
            };
		}
    }
}

