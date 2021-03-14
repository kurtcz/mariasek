using System;
using System.Collections.Generic;

namespace Mariasek.Engine.New
{
    public class GameWonPrematurelyEventArgs
    {
        public AbstractPlayer winner;
        public List<Card> winningHand;
        public int roundNumber;
    }
}

