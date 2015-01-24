using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public class GameTypeEventArgs
    {
        public readonly Hra GameType;

        public GameTypeEventArgs(Hra gameType)
        {
            GameType = gameType;
        }
    }
}
