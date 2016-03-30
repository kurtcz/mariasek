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

    public class AiPlayerSettings
    {
        public bool Cheat { get; set; }
        public CardSelectionStrategy CardSelectionStrategy { get; set; }
        public int SimulationsPerRound { get; set; }
        public int RoundsToCompute { get; set; }
        public float RuleThreshold { get; set; }
        public Dictionary<Hra, float> RuleThresholdForGameType { get; set; }
        public float SingleRuleThreshold { get; set; }
        public Dictionary<Hra, float> SingleRuleThresholdForGameType { get; set; }
        public float[] GameThresholds { get; set; }
        public int MaxDoubleCount { get; set; }

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
