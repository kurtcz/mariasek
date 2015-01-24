using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public class CardEventArgs
    {
        public readonly Card card;

        public CardEventArgs(Card c)
        {
            card = c;
        }
    }
}
