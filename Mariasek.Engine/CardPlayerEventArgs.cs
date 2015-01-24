using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public class CardPlayerEventArgs : CardEventArgs
    {
        public readonly AbstractPlayer Player;
        public readonly bool Hlas;

        public CardPlayerEventArgs(Card card, AbstractPlayer player, bool hlas) : base(card)
        {
            Player = player;
            Hlas = hlas;
        }
    }
}
