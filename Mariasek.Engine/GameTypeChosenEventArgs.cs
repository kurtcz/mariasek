﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine
{
    public class GameTypeChosenEventArgs
    {
        public int GameStartingPlayerIndex;
        public Hra GameType;
        public Card TrumpCard;
        public List<Card> axTalon;
    }
}
