using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public class HandInfo
    {
        /// <summary>
        /// Player's hand
        /// </summary>
        public List<Card> Hand { get; set; }
        /// <summary>
        /// Returns true if this hand belongs to an opponent, false otherwise.
        /// </summary>
        public bool IsOpponent { get; set; }
        /// <summary>
        /// 0 if this player starts the round, 1 if he plays 2nd, 2 if he plays last
        /// </summary>
        public int RoundOrder { get; set; }
    }
}
