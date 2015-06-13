using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine.New
{
    public class GameComputationResult
    {
        public Hand[] Hands;                    //for debugging purposees
        public List<RoundDebugContext> Rounds;  //for debugging purposees
        public Card CardToPlay { get; set; }
        public int[] Score { get; set; }
        public int[] BasicScore { get; set; }
        public int[] MaxHlasScore { get; set; }
        public AiRule Rule { get; set; }
        public bool? Final7Won { get; set; }
    }
}
