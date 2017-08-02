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

    public class BidThresholdSettings
    {
        public Hra GameType { get; set; }
        public bool Use { get; set; }
        public int MaxBidCount { get; set; }
        public string Thresholds { get; set; }
    }

    public class GameSettings
    {
        public bool HintEnabled { get; set; }
        public bool SoundEnabled { get; set; }
        public bool BgSoundEnabled { get; set; }
        public SortMode SortMode { get; set; }
        public float BaseBet { get; set; }
        public int ThinkingTimeMs { get; set; }
        public CalculationStyle CalculationStyle { get; set; }
        public int GameTypeSimulationsPerSecond { get; set; }
        public int RoundSimulationsPerSecond { get; set; }
        public int CurrentStartingPlayerIndex { get; set; }
        public CardFace CardDesign { get; set; }
        public BidThresholdSettings[] Thresholds { get; set; }

        public GameSettings()
        {
            HintEnabled = true;
            SoundEnabled = true;
            BgSoundEnabled = true;
            SortMode = SortMode.Descending;
            BaseBet = 1f;
            CalculationStyle = CalculationStyle.Adding;
            CurrentStartingPlayerIndex = 0;
            ThinkingTimeMs = 2000;
			CardDesign = CardFace.Single;
            ResetThresholds();
        }

        public void ResetThresholds()
        {
            Thresholds = new []
            {
                new BidThresholdSettings
                {
                    GameType = Hra.Hra,
                    Use = true,
                    MaxBidCount = 3,
                    Thresholds = "0|25|55|70"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.Sedma,
                    Use = true,
                    MaxBidCount = 2,
                    Thresholds = "40|60|80|90"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.Kilo,
                    Use = true,
                    MaxBidCount = 1,
                    Thresholds = "55|70|90|95"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.SedmaProti,
                    Use = true,
                    MaxBidCount = 1,
                    Thresholds = "40|75|95|100"
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
                    MaxBidCount = 0,
                    Thresholds = "65|75|85|95"
                },
                new BidThresholdSettings
                {
                    GameType = Hra.Durch,
                    Use = true,
                    MaxBidCount = 0,
                    Thresholds = "70|75|85|100"
                }
            };
        }
    }
}

