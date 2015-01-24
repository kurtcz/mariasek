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
        /// </summary>
        public Hra Bids { get; private set; }

        public AbstractPlayer GameLastBidder { get; private set; }
        public AbstractPlayer SevenLastBidder { get; private set; }
        public AbstractPlayer SevenAgainstLastBidder { get; private set; }
        public AbstractPlayer HundredAgainstLastBidder { get; private set; }
        public AbstractPlayer BetlDurchLastBidder { get; private set; }

        public int Round { get; private set; }

        private int _gameFlek;
        private int _sevenFlek;
        private int _sevenAgainstFlek;
        private int _hundredAgainstFlek;
        private int _betlDurchFlek;

        public int GameMultiplier { get { return _gameFlek > 0 ? 1 << (_gameFlek - 1) : 0; } }
        public int SevenMultiplier { get { return _sevenFlek > 0 ? 1 << (_sevenFlek - 1) : 0; } }
        public int SevenAgainstMultiplier { get { return _sevenAgainstFlek > 0 ? 1 << (_sevenAgainstFlek - 1) : 0; } }
        public int HundredAgainstMultiplier { get { return _hundredAgainstFlek > 0 ? 1 << (_hundredAgainstFlek - 1) : 0; } }
        public int BetlDurchMultiplier { get { return _betlDurchFlek > 0 ? 1 << (_betlDurchFlek - 1) : 0; } }

        public Bidding(Game g)
        {
            _g = g;

            _gameFlek = 0;
            _sevenFlek = 0;
            _sevenAgainstFlek= 0;
            _hundredAgainstFlek = 0;
            _betlDurchFlek = 0;
            PlayerBids = new Hra[Game.NumPlayers];
            var x = Hra.Hra | Hra.Sedma;
            SetLastBidder(_g.GameStartingPlayer, _g.GameType);
        }

        private void SetLastBidder(AbstractPlayer player, Hra bid)
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
                BetlDurchLastBidder = player;
                _betlDurchFlek++;
            }
            PlayerBids[player.PlayerIndex] = bid;
        }

        private void ResetBidsOfPlayer(AbstractPlayer player)
        {
            if (GameLastBidder == player)
            {
                GameLastBidder = null;
                PlayerBids[player.PlayerIndex] &= ((Hra)~0 ^ (Hra.Hra | Hra.Kilo));
            }
            if (SevenLastBidder == player)
            {
                SevenLastBidder = null;
                PlayerBids[player.PlayerIndex] &= ((Hra)~0 ^ Hra.Sedma);
            }
            if (SevenAgainstLastBidder == player)
            {
                SevenAgainstLastBidder = null;
                PlayerBids[player.PlayerIndex] &= ((Hra)~0 ^ Hra.SedmaProti);
            }
            if (HundredAgainstLastBidder == player)
            {
                HundredAgainstLastBidder = null;
                PlayerBids[player.PlayerIndex] &= ((Hra)~0 ^ Hra.KiloProti);
            }
            if (BetlDurchLastBidder == player)
            {
                BetlDurchLastBidder = null;
                PlayerBids[player.PlayerIndex] &= ((Hra)~0 ^ (Hra.Betl | Hra.Durch));
            }
        }

        public Hra StartBidding()
        {
            var gameType = PlayerBids[_g.GameStartingPlayerIndex];

            Round = 0;
            var e = GetEventArgs(_g.GameStartingPlayer, Bids, 0);
            _log.DebugFormat("Bidding: {0}: {1} ({2})", e.Player.Name, e.Description, Bids);
            _g.OnBidMade(e);

            var i = (_g.GameStartingPlayerIndex + 1) % Game.NumPlayers;
            for (var j = 0; ; j++, i = ++i % Game.NumPlayers)
            {
                Round = j % Game.NumPlayers;
                //zrus priznak u her ktere cele kolo nikdo neflekoval aby uz nesly flekovat dal
                ResetBidsOfPlayer(_g.players[i]);
                if ((PlayerBids[0] | PlayerBids[1] | PlayerBids[2]) == 0)
                {
                    break;
                }
                if(_g.players[i].TeamMateIndex != -1)
                {
                    //zajistime aby 2. souper nemohl znovu flekovat co uz fleknul 1. souper
                    if (_g.players[(i + 2) % Game.NumPlayers].TeamMateIndex != -1)
                    {
                        Bids = PlayerBids[_g.GameStartingPlayerIndex] & ((Hra)~0 ^ PlayerBids[(i + 2) % Game.NumPlayers]);
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
                    Bids = PlayerBids[(i + 1)%Game.NumPlayers] | PlayerBids[(i + 2)%Game.NumPlayers];
                }
                //v prvnim kole muzou souperi hlasit 100/7 proti
                if (j < Game.NumPlayers && _g.players[i].TeamMateIndex != -1)
                {
                    if (_g.players[i].Hand.Contains(new Card(_g.trump, Hodnota.Sedma)))
                    {
                        Bids |= Hra.SedmaProti;
                    }
                    Bids |= Hra.KiloProti;
                }
                var bid = _g.players[i].GetBidsAndDoubles(this);
                //nastav priznak co hrac hlasil a flekoval
                SetLastBidder(_g.players[i], bid);
                gameType |= bid;
                _log.DebugFormat("Bidding: {0}: {1} ({2})", e.Player.Name, e.Description, bid);
                _g.OnBidMade(GetEventArgs(_g.players[i], bid, Bids));
                _g.ThrowIfCancellationRequested();
            }

            return gameType;
        }

        private BidEventArgs GetEventArgs(AbstractPlayer player, Hra bid, Hra previousBid)
        {
            var e = new BidEventArgs
            {
                Player = player,
                BidMade = bid
            };

            if (Round == 0)
            {
                if (player == _g.GameStartingPlayer)
                {
                    if ((_g.GameType & Hra.Hra) != 0 &&
                        (_g.GameType & Hra.Sedma) != 0)
                    {
                        e.Description = string.Format("Sedma {0}", _g.trump.Description());
                    }
                    else if ((_g.GameType & Hra.Kilo) != 0 &&
                                (_g.GameType & Hra.Sedma) != 0)
                    {
                        e.Description = string.Format("Stosedm {0}", _g.trump.Description());
                    }
                    else if ((_g.GameType & Hra.Kilo) != 0)
                    {
                        e.Description = string.Format("Kilo {0}", _g.trump.Description());
                    }
                    else if ((_g.GameType & Hra.Sedma) != 0)
                    {
                        e.Description = string.Format("Sedma {0}", _g.trump.Description());
                    }
                    else if ((_g.GameType & Hra.Hra) != 0)
                    {
                        e.Description = string.Format("Hra {0}", _g.trump.Description());
                    }
                    else if ((_g.GameType & Hra.Betl) != 0)
                    {
                        e.Description = "Betl";
                    }
                    else if ((_g.GameType & Hra.Durch) != 0)
                    {
                        e.Description = "Durch";
                    }
                    else
                    {
                        e.Description = string.Format(_g.GameType.ToString());
                    }
                }
                else
                {
                    if ((e.BidMade & (Hra.Hra | Hra.Kilo)) != 0 &&
                        (e.BidMade & Hra.SedmaProti) != 0)
                    {
                        e.Description = "Flek a sedma proti";
                    }
                    else if ((e.BidMade & (Hra.Hra | Hra.Kilo)) != 0 &&
                                (e.BidMade & Hra.Sedma) != 0)
                    {
                        e.Description = "Stří­hat a holit";
                    }
                    else if ((e.BidMade & (Hra.Hra | Hra.Kilo)) != 0)
                    {
                        if ((previousBid & Hra.Sedma) != 0)
                        {
                            if (GameMultiplier == 2)
                            {
                                e.Description = "Flek na hru";
                            }
                            else
                            {
                                e.Description = "Na hru vejš";
                            }
                        }
                        else
                        {
                            e.Description = MultiplierToString(GameMultiplier);
                        }
                    }
                    else if ((e.BidMade & (Hra.Sedma)) != 0)
                    {
                        e.Description = "Flek na sedmu";
                    }
                    else if ((e.BidMade & Hra.SedmaProti) != 0)
                    {
                        e.Description = "Sedma proti";
                    }
                    else if ((e.BidMade & Hra.KiloProti) != 0)
                    {
                        e.Description = "Kilo proti";
                    }
                    else if (e.BidMade == 0)
                    {
                        e.Description = "Dobrý";
                    }
                    else
                    {
                        e.Description = string.Format(e.BidMade.ToString());
                    }
                }
            }
            else //Round > 0
            {
                var sb = new StringBuilder();
                if ((e.BidMade & (Hra.Hra | Hra.Kilo)) != 0)
                {
                    sb.AppendFormat("{0}", MultiplierToString(GameMultiplier));
                }
                if ((e.BidMade & (Hra.Sedma | Hra.SedmaProti)) != 0)
                {
                    sb.AppendFormat("{0}Na sedmu vejš", sb.Length > 0 ? "\n" : "");
                }
                if ((e.BidMade & Hra.KiloProti) != 0)
                {
                    sb.AppendFormat("{0}Na kilo vejš", sb.Length > 0 ? "\n" : "");
                }
                if (e.BidMade == 0)
                {
                    e.Description = "Dobrý";
                }
                else
                {
                    e.Description = sb.ToString();
                }
            }

            return e;
        }

        public static string MultiplierToString(int multiplier)
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
                    return "Na hru vejš";
            }
        }
    }
}
