using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public class Score
    {
        public int[] Points = new int[Game.NumPlayers];

        public Score(int[] initialPoints)
        {
            for (int i = 0; i < Game.NumPlayers; i++)
            {
                Points[i] = initialPoints[i];
            }
        }
    }
}
