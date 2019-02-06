using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
{
    public enum CardSelectionStrategy
    {
        [Description("Nejčastější karta")]
        MaxCount = 0,
        [Description("Min. skóre soupeřů")]
        MinScore = 1,
        [Description("Průměrné skóre")]
        AverageScore = 2
    }

	public enum GameFlavourSelectionStrategy
	{
		Standard = 0,
		Fast = 1
	}

    public class AiPlayerSettings
    {
        public bool Cheat { get; set; }
        public bool AiMayGiveUp { get; set; }
        public CardSelectionStrategy CardSelectionStrategy { get; set; }
        public int SimulationsPerGameType { get; set; }
        public int SimulationsPerGameTypePerSecond { get; set; }
        public int MaxSimulationTimeMs { get; set; }
        public int SimulationsPerRound { get; set; }
        public int SimulationsPerRoundPerSecond { get; set; }
        public int RoundsToCompute { get; set; }
        public float RuleThreshold { get; set; }
        public Dictionary<Hra, float> RuleThresholdForGameType { get; set; }
        public float[] GameThresholds { get; set; }
        public Dictionary<Hra, float[]> GameThresholdsForGameType { get; set; } //TODO: move ThresholdSettings over here and use instead
        //public int MaxDoubleCount { get; set; }
        public Dictionary<Hra, int> MaxDoubleCountForGameType { get; set; }
        public Dictionary<Hra, bool> CanPlayGameType { get; set; }
        public int SigmaMultiplier { get; set; }
		public GameFlavourSelectionStrategy GameFlavourSelectionStrategy { get; set; }
        public float RiskFactor { get; set; }
        public float SolitaryXThreshold { get; set; }
        public float SolitaryXThresholdDefense { get; set; }

#if !PORTABLE
        public override string ToString()
        {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                      .Select(i => new
                                      {
                                          Name = i.Name,
                                          Value = i.GetValue(this, null)
                                      });

            var builder = new StringBuilder();

            foreach (var property in properties)
            {
                builder
                    .Append(property.Name)
                    .Append(": ")
                    .Append(property.Value)
                    .AppendLine();
            }

            return builder.ToString();
        }
#endif
    }
}
