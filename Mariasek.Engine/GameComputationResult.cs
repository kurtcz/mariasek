using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public class GameComputationResult
    {
        public Hand[] Hands;                    //for debugging purposees
        public List<RoundDebugContext> Rounds;  //for debugging purposees
        public Barva? Trump { get; set; }
        public Hra GameType { get; set; }
        public Card CardToPlay { get; set; }
        public int[] Score { get; set; }
        public int[] BasicScore { get; set; }
        public int[] MaxHlasScore { get; set; }
        public int[] TotalHlasScore { get; set; }
        public AiRule Rule { get; set; }
        public string Note { get; set; }
        public Dictionary<AiRule, Card> ToplevelRuleDictionary { get; set; }
        public bool? Final7Won { get; set; }
        public bool? Final7AgainstWon { get; set; }
    }
}
