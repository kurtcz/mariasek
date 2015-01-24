using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public class TalonEventArgs : EventArgs
    {
        public readonly List<Card> Talon;

        public TalonEventArgs(List<Card> talon)
        {
            Talon = talon;
        }
    }
}
