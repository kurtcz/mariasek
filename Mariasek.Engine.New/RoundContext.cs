using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
{
    public class RoundContext
    {
        public AbstractPlayer Player1 { get; set; }
        public AbstractPlayer Player2 { get; set; }
        public AbstractPlayer Player3 { get; set; }

        public Card c1 { get; set; }
        public Card c2 { get; set; }
        public Card c3 { get; set; }

        public bool hlas1 { get; set; }
        public bool hlas2 { get; set; }
        public bool hlas3 { get; set; }

        public int points1 { get; set; }
        public int points2 { get; set; }
        public int points3 { get; set; }

        public AbstractPlayer RoundStarter { get; set; }
        public AbstractPlayer RoundWinner { get; set; }
    }
}
