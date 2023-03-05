using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public class PlayerDebugInfo : RuleDebugInfo
    {
        public RuleDebugInfo[] AllChoices { get; set; }
		public TimeSpan ComputationTime { get; set; }
        public int EstimatedFinalBasicScore { get; set; }
        public int EstimatedFinalBasicScore2 { get; set; }
        public int MinBasicPointsLost { get; set; }
        public int Tygrovo { get; set; }
        public int Strong { get; set; }
        public int MaxSimulatedLoss { get; set; }
        public int MaxEstimatedBasicPointsLost { get; set; }
        public int MaxEstimatedHlasPointsLost { get; set; }
        public int MaxEstimatedMoneyLost { get; set; }
        public int EstimatedHundredLoss { get; set; }
        public int AvgSimulatedPointsWon { get; set; }
        public int MaxSimulatedHundredLoss { get; set; }
        public int TotalHoles { get; set; }
        public bool SevenTooRisky { get; set; }
        public bool HundredTooRisky { get; set; }
        public string ProbDebugInfo { get; set; }

        public PlayerDebugInfo()
        {
            AllChoices = new RuleDebugInfo[0];
        }
    }

    public class RuleDebugInfo
    {
        public Card Card { get; set; }
        public string Rule { get; set; }        
        public int RuleCount { get; set; }
        public int TotalRuleCount { get; set; }
        public string AiDebugInfo { get; set; }
    }
}
