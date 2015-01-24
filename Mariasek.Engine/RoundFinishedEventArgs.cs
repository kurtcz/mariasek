using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public class RoundFinishedEventArgs : EventArgs
    {
        public readonly AbstractPlayer Winner;
        public int PointsWon;

        public RoundFinishedEventArgs(AbstractPlayer winner, int points)
        {
            Winner = winner;
            PointsWon = points;
        }
    }
}
