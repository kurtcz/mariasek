using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine.New
{
    public class PlayerDebugInfo : RuleDebugInfo
    {
        public RuleDebugInfo[] AllChoices { get; set; }
		public TimeSpan ComputationTime { get; set; }
        public int EstimatedFinalBasicScore { get; set; }
        public int EstimatedFinalBasicScore2 { get; set; }
        public int Tygrovo { get; set; }
        public int Strong { get; set; }
        public int MaxEstimatedLoss { get; set; }
        public int MaxSimulatedLoss { get; set; }
        public int TotalHoles { get; set; }
        public bool HunderTooRisky { get; set; }

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
    }
}
