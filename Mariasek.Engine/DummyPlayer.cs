namespace Mariasek.Engine
{
    public class DummyPlayer : AbstractPlayer
    {
        public DummyPlayer(Game g) : base(g)
        {
            
        }

        public override async Task<Card> ChooseTrump()
        {
            return Hand.RandomOne();
        }

        public override async Task<List<Card>> ChooseTalon()
        {
            var talon = Hand.Where(i => i.Suit != _g.trump.Value &&
                                   i.Value < Hodnota.Svrsek)
                            .Take(2)
                            .ToList();
            if (talon.Count < 2)
            {
                talon.AddRange(Hand.Where(i => i.Suit != _g.trump.Value &&
                                       i.Value < Hodnota.Desitka)
                                   .Take(2 - talon.Count)
                                   .ToList());
                if (talon.Count < 2)
                {
                    talon.AddRange(Hand.Where(i => i.Suit != _g.trump.Value)
                                       .Take(2 - talon.Count)
                                       .ToList());
                }
            }

            return talon;
        }

        public override async Task<GameFlavour> ChooseGameFlavour()
        {
            return GameFlavour.Good;
        }

        public override async Task<Hra> ChooseGameType(Hra validGameTypes)
        {
            return Hra.Hra;
        }

        private int _numberOfDoubles = 0;

        public override async Task<Hra> GetBidsAndDoubles(Bidding bidding)
        {
            //1x flekujeme hru, jinak mlcime
            if (_numberOfDoubles++ == 0)
            {
                return bidding.Bids & Hra.Hra;
            }
            return 0;
        }

        public override void Init()
        {            
        }

        public override async Task<Card> PlayCard(Round r)
        {
            if (r.c1 == null)
            {
                return ValidCards(Hand, _g.trump, _g.GameType, TeamMateIndex).RandomOne();
            }
            else if (r.c2 == null)
            {
                return ValidCards(Hand, _g.trump, _g.GameType, TeamMateIndex, r.c1).RandomOne();
            }
            else
            {
                return ValidCards(Hand, _g.trump, _g.GameType, TeamMateIndex, r.c1, r.c2).RandomOne();
            }
        }
    }
}
