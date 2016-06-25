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

    public class GameSettings
    {
        public bool HintEnabled { get; set; }
        public bool SoundEnabled { get; set; }
        public SortMode SortMode { get; set; }
        public float BaseBet { get; set; }
        public int ThinkingTimeMs { get; set; }
        public CalculationStyle CalculationStyle { get; set; }
        public int GameTypeSimulationsPerSecond { get; set; }
        public int RoundSimulationsPerSecond { get; set; }
        public int CurrentStartingPlayerIndex { get; set; }

        public GameSettings()
        {
            HintEnabled = true;
            SoundEnabled = true;
            SortMode = SortMode.Descending;
            BaseBet = 1f;
            CalculationStyle = CalculationStyle.Adding;
            CurrentStartingPlayerIndex = 0;
            ThinkingTimeMs = 3000;
        }
    }
}

