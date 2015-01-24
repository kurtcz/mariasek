using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public class GameFinishedEventArgs : EventArgs
    {
        public readonly int Score1;
        public readonly int Score2;
        public readonly int Score3;

        public GameFinishedEventArgs(int score1, int score2, int score3)
        {
            Score1 = score1;
            Score2 = score2;
            Score3 = score3;
        }
    }
}
