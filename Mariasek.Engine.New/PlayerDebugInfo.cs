using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine.New
{
    public class PlayerDebugInfo : RuleDebugInfo
    {
        public RuleDebugInfo[] AllChoices { get; set; }
    }

    public class RuleDebugInfo
    {
        public Card Card { get; set; }
        public string Rule { get; set; }
        public int RuleCount { get; set; }
        public int TotalRuleCount { get; set; }
    }
}
