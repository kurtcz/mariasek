using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public class GameComputationResult
    {
        public Card CardToPlay { get; set; }
        public Score Score { get; set; }
        public AiRule Rule { get; set; }
        public bool? Final7Won { get; set; }
    }
}
