using System;
using System.Linq;
using System.Reflection;
using System.Text;
//using log4net;
using Mariasek.Engine.New.Logger;

namespace Mariasek.Engine.New
{
    public class Bidding
    {
#if !PORTABLE
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
#else
        private static readonly ILog _log = new DummyLogWrapper();
#endif
        private Game _g;

        public Hra[] PlayerBids { get; private set; }
        /// <summary>
        /// When a AbstractPlayer.GetBidsAndDoubles() is called this property holds valid bids for that player.
        /// This property is set before each call to GetBidsAndDoubles() inside the StartBidding() method
        /// </summary>
        public Hra Bids { get; private set; }

        public AbstractPlayer GameLastBidder { get; private set; }
        public AbstractPlayer SevenLastBidder { get; private set; }
        public AbstractPlayer SevenAgainstLastBidder { get; private set; }
        public AbstractPlayer HundredAgainstLastBidder { get; private set; }
        public AbstractPlayer BetlDurchLastBidder { get; private set; }

        public int Round { get; set; }

        public int _gameFlek;
        public int _sevenFlek;
        public int _sevenAgainstFlek;
        public int _hundredAgainstFlek;
        public int _betlDurchFlek;

        public int GameMultiplier { get { return _gameFlek > 0 ? 1 << (_gameFlek - 1) : 0; } }
        public int SevenMultiplier { get { return _sevenFlek > 0 ? 1 << (_sevenFlek - 1) : 0; } }
        public int SevenAgainstMultiplier { get { return _sevenAgainstFlek > 0 ? 1 << (_sevenAgainstFlek - 1) : 0; } }
        public int HundredAgainstMultiplier { get { return _hundredAgainstFlek > 0 ? 1 << (_hundredAgainstFlek - 1) : 0; } }
        public int BetlDurchMultiplier { get { return _betlDurchFlek > 0 ? 1 << (_betlDurchFlek - 1) : 0; } }
        //public int MaxDoubleCount
        //{
        //    get
        //    {
        //        var maxFlek = 0;
        //        if (_gameFlek > maxFlek) { maxFlek = _gameFlek; }
        //        if (_sevenFlek > maxFlek) { maxFlek = _sevenFlek; }
        //        if (_sevenAgainstFlek > maxFlek) { maxFlek = _sevenAgainstFlek; }
        //        if (_hundredAgainstFlek > maxFlek) { maxFlek = _hundredAgainstFlek; }
        //        if (_betlDurchFlek > maxFlek) { maxFlek = _betlDurchFlek; }

        //        return maxFlek;
        //    }
        //}

        public Bidding(Game g)
        {
            _g = g;

            _gameFlek = 0;
            _sevenFlek = 0;
            _sevenAgainstFlek= 0;
            _hundredAgainstFlek = 0;
            _betlDurchFlek = 0;
            PlayerBids = new Hra[Game.NumPlayers];
            SetLastBidder(_g.GameStartingPlayer, _g.GameType);
        }

        public Bidding(Bidding b)
        {
            _g = b._g;

            _gameFlek = b._gameFlek;
            _sevenFlek = b._sevenFlek;
            _sevenAgainstFlek = b._sevenAgainstFlek;
            _hundredAgainstFlek = b._hundredAgainstFlek;
            _betlDurchFlek = b._betlDurchFlek;
            PlayerBids = new Hra[Game.NumPlayers];
            for (var i = 0; i < Game.NumPlayers; i++)
            {
                PlayerBids[i] = b.PlayerBids[i];
            }
            GameLastBidder = b.GameLastBidder;
            SevenLastBidder = b.SevenLastBidder;
            SevenAgainstLastBidder = b.SevenAgainstLastBidder;
            HundredAgainstLastBidder = b.HundredAgainstLastBidder;
            BetlDurchLastBidder = b.BetlDurchLastBidder;
        }

        public void SetLastBidder(AbstractPlayer player, Hra bid)
        {
            if ((bid & (Hra.Hra | Hra.Kilo)) != 0)
            {
                GameLastBidder = player;
                _gameFlek++;
            }
            if ((bid & Hra.Sedma) != 0)
            {
                SevenLastBidder = player;
                _sevenFlek++;
            }
            if ((bid & Hra.SedmaProti) != 0)
            {
                SevenAgainstLastBidder = player;
                _sevenAgainstFlek++;
            }
            if ((bid & Hra.KiloProti) != 0)
            {
                HundredAgainstLastBidder = player;
                _hundredAgainstFlek++;
            }
            if ((bid & (Hra.Betl | Hra.Durch)) != 0)
            {
				if (BetlDurchLastBidder != null && PlayerBids[BetlDurchLastBidder.PlayerIndex] != bid)
				{
					//sli jsme z betla na durcha: resetuj pocitadlo fleku
					_betlDurchFlek = 0;
				}
                BetlDurchLastBidder = player;
                _betlDurchFlek++;
            }
            PlayerBids[player.PlayerIndex] = bid;
        }

        public void StartBidding(Hra gameType)
        {
            Round = 0;
            var e = GetEventArgs(_g.GameStartingPlayer, gameType, 0);
            _log.DebugFormat("Bidding: {0}: {1} ({2})", e.Player.Name, e.Description, Bids);
            _g.DebugString.AppendFormat("StartBidding: Player{0}: {1} ({2})\n", e.Player.PlayerIndex + 1, e.Description, Bids);
            _g.BiddingDebugInfo.AppendFormat("\nPlayer {0}: {1}\n", e.Player.PlayerIndex + 1, e.Description);
            _g.AddBiddingDebugInfo(e.Player.PlayerIndex);
            _g.OnBidMade(e);
        }

        public Hra CompleteBidding()
        {
            //vola se pokud hrajeme normalni hru. U betla a durcha se flekuje rovnou
            var gameType = PlayerBids[_g.GameStartingPlayerIndex];

            Round = 0;
            var e = GetEventArgs(_g.GameStartingPlayer, gameType, 0);
            _log.DebugFormat("Bidding: {0}: {1} ({2})", e.Player.Name, e.Description, Bids);
            _g.DebugString.AppendFormat("CompleteBidding: Player{0}: {1} ({2})\n", e.Player.PlayerIndex + 1, e.Description, Bids);
            _g.BiddingDebugInfo.AppendFormat("\nPlayer {0}: {1}\n", e.Player.PlayerIndex + 1, e.Description);
            _g.AddBiddingDebugInfo(e.Player.PlayerIndex);
            _g.OnBidMade(e);

            var i = (_g.GameStartingPlayerIndex + 1) % Game.NumPlayers;
            for (var j = 1; ; j++, i = ++i % Game.NumPlayers)
            {
                Round = j % Game.NumPlayers;
                //zrus priznak u her ktere cele kolo nikdo neflekoval aby uz nesly flekovat dal
                AdjustValidBidsForPlayer(i, j);
                _g.DebugString.AppendFormat("Player{0} GetBidsAndDoubles()\n", i + 1);
            
                var bid = _g.players[i].GetBidsAndDoubles(this);
                //nastav priznak co hrac hlasil a flekoval
                SetLastBidder(_g.players[i], bid);
                gameType |= bid;
                e = GetEventArgs(_g.players[i], bid, Bids);
                _log.DebugFormat("Bidding: {0}: {1} ({2})", e.Player.Name, e.Description, bid);
                _g.DebugString.AppendFormat("\nBidding: Player{0}: {1} ({2})\n", e.Player.PlayerIndex + 1, e.Description, bid);
                _g.BiddingDebugInfo.AppendFormat("\nPlayer {0}: {1}\n", e.Player.PlayerIndex + 1, e.Description);
                e.Player.BidMade += bid == 0 ? "" : e.Description + " ";
                _g.AddBiddingDebugInfo(e.Player.PlayerIndex);
                _g.OnBidMade(e);
                _g.ThrowIfCancellationRequested();

                var teamMate = _g.players[i].TeamMateIndex;
                //pokud volici hrac mlci nebo pokud mlci druhy z protihracu pricemz prvni nic nerikal, tak muzeme flekovani ukoncit
                if (bid == 0 && (teamMate == -1 || (teamMate == _g.players[(i + 2) % Game.NumPlayers].PlayerIndex && PlayerBids[teamMate] == 0)))
                {
                    break;
                }
            }

            return gameType;
        }

        public Hra GetBidsForPlayer(Hra gameType, AbstractPlayer player, int bidNumber)
        {
            AdjustValidBidsForPlayer(player.PlayerIndex, bidNumber);

            _g.DebugString.AppendFormat("Player{0} GetBidsAndDoubles()\n", player.PlayerIndex + 1);
            var bid = player.GetBidsAndDoubles(this);

            //nastav priznak co hrac hlasil a flekoval
            SetLastBidder(player, bid);
            gameType |= bid;
            var e = GetEventArgs(player, bid, Bids);
            _log.DebugFormat("Bidding: {0}: {1} ({2})", e.Player.Name, e.Description, bid);
            _g.DebugString.AppendFormat("Bidding: Player{0}: {1} ({2})\n", player.PlayerIndex + 1, e.Description, bid);
            _g.BiddingDebugInfo.AppendFormat("\nPlayer {0}: {1}\n", player.PlayerIndex + 1, e.Description);
            e.Player.BidMade += bid == 0 ? "" : e.Description + " ";
            _g.AddBiddingDebugInfo(player.PlayerIndex);
            _g.OnBidMade(e);

            //return gameType;
            return bid;
        }

        /// <summary>
        /// Sets valid Bids for a given player before a call to AbstractPlayer.GetBidsAndDoubles() is made
        /// </summary>
        /// <param name="playerIndex">Player's index in the game</param>
        /// <param name="bidNumber">Zero based sequential bid number (increments with each bid made, each round grows by Game.NumPlayers)</param>
        private void AdjustValidBidsForPlayer(int playerIndex, int bidNumber)
        {
            if (_g.players[playerIndex].TeamMateIndex != -1)
            {
                //zajistime aby 2. souper nemohl znovu flekovat co uz fleknul 1. souper
                if (_g.players[(playerIndex + 2) % Game.NumPlayers].TeamMateIndex != -1)
                {
                    Bids = PlayerBids[_g.GameStartingPlayerIndex] & ~PlayerBids[(playerIndex + 2) % Game.NumPlayers];
                    if ((PlayerBids[(playerIndex + 2) % Game.NumPlayers] & Hra.KiloProti) != 0)
                    {
                        Bids &= (Hra)~Hra.Hra; //u kila proti uz nejde dat flek na hru
                    }
                }
                else
                {
                    //1. souper muze flekovat jen hry a fleky hrace co volil
                    Bids = PlayerBids[_g.GameStartingPlayerIndex];
                }
            }
            else
            {
                //hrac co volil muze flekovat jen souperovy hry a fleky
                Bids = PlayerBids[(playerIndex + 1) % Game.NumPlayers] | PlayerBids[(playerIndex + 2) % Game.NumPlayers];
            }
            //v prvnim kole muzou souperi hlasit 100/7 proti
            if (_g.trump.HasValue &&
                bidNumber < Game.NumPlayers && 
                _g.players[playerIndex].TeamMateIndex != -1)
            {
                if (_g.players[playerIndex].Hand.Contains(new Card(_g.trump.Value, Hodnota.Sedma)))
                {
                    Bids |= Hra.SedmaProti;
                }
                if ((!_g.AutoDisable100Against ||
                     Enum.GetValues(typeof(Barva)).Cast<Barva>()   //aby neslo omylem hlasit kilo proti bez hlasky
                         .Any(b => _g.players[playerIndex].Hand.HasK(b) &&
                                   _g.players[playerIndex].Hand.HasQ(b))) &&
                    (_g.players[playerIndex].TeamMateIndex == (playerIndex + 1) % Game.NumPlayers ||
                     (PlayerBids[_g.players[playerIndex].TeamMateIndex] & Hra.KiloProti) == 0))
                {
                    Bids |= Hra.KiloProti;
                }
            }
        }

        public BidEventArgs GetEventArgs(AbstractPlayer player, Hra bid, Hra previousBid)
        {
            var e = new BidEventArgs
            {
                Player = player,
                BidMade = bid
            };

            if ((e.BidMade & Hra.Hra) != 0 &&
                (e.BidMade & Hra.Sedma) != 0)
            {
                switch (GameMultiplier)
                {
                    case 1:
                        e.Description = e.BidMade.ToDescription(_g.trump);
                        break;
                    case 2:
                        e.Description = "Stříhat a holit";
                        break;
                    default:
                        e.Description = "Na oboje vejš";
                        break;
                }
            }
            else if ((e.BidMade & Hra.Kilo) != 0 &&
                     (e.BidMade & Hra.Sedma) != 0)
            {
                switch (GameMultiplier)
                {
                    case 1:
                        e.Description = e.BidMade.ToDescription(_g.trump);
                        break;
                    case 2:
                        e.Description = "Stříhat a holit";
                        break;
                    default:
                        e.Description = "Na oboje vejš";
                        break;
                }
            }
            else if ((e.BidMade & Hra.Kilo) != 0)
            {
                if ((previousBid & Hra.Sedma) != 0)
                {
                    switch (GameMultiplier)
                    {
                        case 2:
                            e.Description = "Flek na hru";
                            break;
                        default:
                            e.Description = "Na hru vejš";
                            break;
                    }
                }
                else
                {
                    switch (GameMultiplier)
                    {
                        case 1:
                            e.Description = e.BidMade.ToDescription(_g.trump);
                            break;
                        default:
                            e.Description = MultiplierToString(GameMultiplier);
                            break;
                    }
                }
            }
            else if ((e.BidMade & Hra.Sedma) != 0 &&
                     (e.BidMade & Hra.KiloProti) != 0)
            {
                switch (HundredAgainstMultiplier)
                {
                    case 1:
                        e.Description = "Na sedmu a kilo proti";
                        break;
                    default:
                        e.Description = "Na oboje vejš";
                        break;
                }
            }
            else if ((e.BidMade & Hra.Sedma) != 0)
            {
                switch (SevenMultiplier)
                {
                    case 1:
                        e.Description = e.BidMade.ToDescription(_g.trump);
                        break;
                    case 2:
                        e.Description = "Flek na sedmu";
                        break;
                    default:
                        e.Description = "Na sedmu vejš";
                        break;
                }
            }
            else if ((e.BidMade & (Hra.Hra | Hra.Kilo)) != 0 &&
                     (e.BidMade & Hra.SedmaProti) != 0)
            {
                switch (SevenAgainstMultiplier)
                {
                    case 1:
                        e.Description = "Flek a sedma proti";
                        break;
                    default:
                        e.Description = "Na oboje vejš";
                        break;
                }
            }
            else if ((e.BidMade & Hra.SedmaProti) != 0 &&
                     (e.BidMade & Hra.KiloProti) != 0)
            {
                switch (SevenAgainstMultiplier)
                {
                    case 1:
                        e.Description = "Stosedm proti";
                        break;
                    default:
                        e.Description = "Na stosedm vejš";
                        break;
                }
            }
            else if ((e.BidMade & Hra.SedmaProti) != 0)
            {
                switch (SevenAgainstMultiplier)
                {
                    case 1:
                        e.Description = "Sedma proti";
                        break;
                    default:
                        e.Description = "Na sedmu vejš";
                        break;
                }
            }
            else if ((e.BidMade & Hra.KiloProti) != 0)
            {
                switch (HundredAgainstMultiplier)
                {
                    case 1:
                        e.Description = "Kilo proti";
                        break;
                    default:
                        e.Description = "Na kilo vejš";
                        break;
                }
            }
            else if ((e.BidMade & Hra.Hra) != 0)
            {
                if ((previousBid & Hra.Sedma) != 0)
                {
                    switch (GameMultiplier)
                    {
                        case 2:
                            e.Description = "Flek na hru";
                            break;
                        default:
                            e.Description = "Na hru vejš";
                            break;
                    }
                }
                else
                {
                    switch (GameMultiplier)
                    {
                        case 1:
                            e.Description = e.BidMade.ToDescription(_g.trump);
                            break;
                        default:
                            e.Description = MultiplierToString(GameMultiplier);
                            break;
                    }
                }
            }
            else if ((e.BidMade & Hra.Betl) != 0)
            {
                switch (BetlDurchMultiplier)
                {
                    case 1:
                        e.Description = e.BidMade.ToDescription(_g.trump);
                        break;
                    default:
                        e.Description = MultiplierToString(BetlDurchMultiplier);
                        break;
                }
            }
            else if ((e.BidMade & Hra.Durch) != 0)
            {
                switch (BetlDurchMultiplier)
                {
                    case 1:
                        e.Description = e.BidMade.ToDescription(_g.trump);
                        break;
                    default:
                        e.Description = MultiplierToString(BetlDurchMultiplier);
                        break;
                }
            }
            else if (e.BidMade == 0)
            {
                e.Description = "Dobrý";
            }
            else
            {
                e.Description = "?";
            }

            return e;
        }

        public static string MultiplierToString(int multiplier, string defaultString = null)
        {
            switch (multiplier)
            {
                case 2:
                    return "Flek";
                case 4:
                    return "Re";
                case 8:
                    return "Tutti";
                case 16:
                    return "Boty";
                case 32:
                    return "Kalhoty";
                default:
                    return defaultString ?? "Vejš";
            }
        }
    }
}
