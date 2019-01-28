using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Mariasek.Engine.New;

namespace Mariasek.SharedClient
{
    public class HumanPlayer : AbstractPlayer, IStatsPlayer
    {
        private MainScene _scene;
        private bool firstTimeChoosingFlavour;
        private List<Card> _talon;
        private Hra _previousBid;
        private Hra _gameType;
        private Barva _trump;
        private Card _trumpCard;
        //private Game _g;
        private AiPlayer _aiPlayer;
        private Task _aiTask;
        private CancellationTokenSource _cancellationTokenSource;
        public Probability Probabilities { get; set; }
        private Func<IStringLogger> _stringLoggerFactory;
        private bool _givenUp;
		private int _t0;
		private int _t1;

        public HumanPlayer(Game g, Mariasek.Engine.New.Configuration.ParameterConfigurationElementCollection aiConfig, MainScene scene, bool showHint)
            : base(g)
        {
            var b = bool.Parse(aiConfig["DoLog"].Value);
            _stringLoggerFactory = () => new StringLogger(b);
            _scene = scene;
            _g = g;
			if (showHint)
			{
				_aiPlayer = new AiPlayer(_g, aiConfig) { Name = "Advisor", AdvisorMode = true };
				DebugInfo = _aiPlayer.DebugInfo;
			}
			else
			{
				_aiPlayer = null;
				DebugInfo = new PlayerDebugInfo();
			}
            _g.GameFlavourChosen += GameFlavourChosen;
            _g.GameTypeChosen += GameTypeChosen;
        }

        public override void Init()
        {
            _gameType = 0;
            _talon = null;
            _trumpCard = null;
            firstTimeChoosingFlavour = true;
            Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, 
                                            _g.AllowAXTalon, _g.AllowTrumpTalon, _g.CancellationToken, _stringLoggerFactory, _g.talon);
            _cancellationTokenSource = new CancellationTokenSource();
            if (_aiPlayer != null)
            {
                _aiPlayer.Init();
                _aiPlayer.ThrowIfCancellationRequested = ThrowIfAiCancellationRequested;
                _aiPlayer.Hand = new List<Card>();
                _aiPlayer.Hand.AddRange(Hand);
                _aiPlayer.GameComputationProgress += _scene.GameComputationProgress;
            }
            if (_g.GameStartingPlayerIndex != 0)
            {
                _scene.SortHand();
                _scene.UpdateHand(flipCardsUp: true);
            }
        }

		public override void Die()
		{
			_scene = null;
			base.Die();
		}

		public void CancelAiTask()
        {
            if (_aiTask != null && _aiTask.Status == TaskStatus.Faulted)
            {
                _scene.GameException(this, new GameExceptionEventArgs(){ e = _aiTask.Exception });
            }
            if (_aiTask != null && _aiTask.Status == TaskStatus.Running)
            {
                _cancellationTokenSource.Cancel();
                try
                {
                    _aiTask.Wait();
                }
                catch (Exception ex)
                {
                    if (!ex.ContainsCancellationException())
                    {
                        throw;
                    }
                }
                _cancellationTokenSource = new CancellationTokenSource();
            }
        }
            
        private void ThrowIfAiCancellationRequested() //callback function for _aiPlayer to cancel _aiTask
        {
            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();
            }
        }

        public override Card ChooseTrump()
        {
            _g.ThrowIfCancellationRequested();
            if (_aiPlayer != null)
            {
                _t0 = Environment.TickCount;
                _aiTask = Task.Run(() =>
                {
                    try
                    {
                        _t1 = Environment.TickCount;
                        var trump = _aiPlayer.ChooseTrump();
                        _scene.SuggestTrump(trump, _t1 - _t0);
                    }
                    catch (Exception ex)
                    {
                        _scene.GameException(this, new GameExceptionEventArgs { e = ex });
                    }
                }, _cancellationTokenSource.Token);
            }
            do
            {
                _trumpCard = _scene.ChooseTrump();
            } while (_trumpCard == null);

            CancelAiTask();
            _g.ThrowIfCancellationRequested();
            _trump = _trumpCard.Suit;
			if (_aiPlayer != null)
			{
				_aiPlayer.TrumpCard = _trumpCard;
			}
			return _trumpCard;
        }

        public override List<Card> ChooseTalon()
        {
            if (_aiPlayer != null)
            {
                _t0 = Environment.TickCount;
                _aiTask = Task.Run(() =>
                {
                    try
                    {
    					_t1 = Environment.TickCount;
                        _aiPlayer._talon = null;
                        _aiPlayer.Hand = Hand;
                        var talon = _aiPlayer.ChooseTalon();

    					_scene.SuggestTalon(talon, _t1 - _t0);
                    }
                    catch (Exception ex)
                    {
                        _scene.GameException(this, new GameExceptionEventArgs { e = ex });
                    }
                }, _cancellationTokenSource.Token);
            }
            do
            {
                _talon = _scene.ChooseTalon();
            } while (_talon == null || _talon.Count() != 2);

            CancelAiTask();
            _g.ThrowIfCancellationRequested();
            if (_aiPlayer != null)
            {
                _aiPlayer.Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump,
                                                          _g.AllowAXTalon, _g.AllowTrumpTalon, _g.CancellationToken, _stringLoggerFactory, _talon)
                {
                    ExternalDebugString = _aiPlayer._debugString
                };
                _aiPlayer._talon = new List<Card>(_talon);
                _aiPlayer.Probabilities.UpdateProbabilitiesAfterTalon(Hand, _aiPlayer._talon);
            }
            return _talon;
        }

        public override GameFlavour ChooseGameFlavour()
        {
            _g.ThrowIfCancellationRequested();
            if (firstTimeChoosingFlavour)
            {                
                firstTimeChoosingFlavour = false;
                //poprve volici hrac nehlasi dobra/spatna ale vybira z typu her, cimz se dobra/spatna implicitne zvoli
                if (PlayerIndex == _g.GameStartingPlayerIndex)
                {
                    var validGameTypes = Hra.Hra | Hra.Betl | Hra.Durch;

                    if (Hand.Contains(new Card(_trump, Hodnota.Sedma)))
                    {
                        validGameTypes |= Hra.Sedma;
                    }
                    if (Enum.GetValues(typeof(Barva)).Cast<Barva>().Any(b => Hand.HasK(b) && Hand.HasQ(b)))
                    {
                        validGameTypes |= Hra.Kilo;
                    }
                    if (_aiPlayer != null)
                        
                    {
						//var gt2 = _aiPlayer.ChooseGameTypeNew(validGameTypes);
						//_scene.SuggestGameTypeNew(gt2);
						//_scene.SuggestGameFlavourNew(gt2);

						_t0 = Environment.TickCount;
                        _aiTask = Task.Run(() =>
                        {
                            try
                            {
								_t1 = Environment.TickCount;
								//dej 2 karty z ruky do talonu aby byl _aiPlayer v aktualnim stavu
                                _aiPlayer._talon = new List<Card>(_talon);
                                _aiPlayer.Hand = Hand;
                                var flavour = _scene.TrumpCardTakenBack ? GameFlavour.Bad : _aiPlayer.ChooseGameFlavour(); //uvnitr se zvoli talon, ale clovek muze ve skutecnosti volit jinak nez ai!!!
                                if (flavour == GameFlavour.Good ||
                                    flavour == GameFlavour.Good107)
                                {
                                    validGameTypes &= ((Hra)~0 ^ (Hra.Betl | Hra.Durch));
                                }
                                else
                                {
                                    validGameTypes &= (Hra.Betl | Hra.Durch);
                                }
                                var gameType = _aiPlayer.ChooseGameType(validGameTypes);
                                //var e = _g.Bidding.GetEventArgs(_aiPlayer, gameType, 0);
                                var msg = new StringBuilder();
                                var k = 0;
                                foreach (var debugInfo in _aiPlayer.DebugInfo.AllChoices.Where(j => j.RuleCount > 0))
    							{
                                    if (debugInfo.TotalRuleCount > 0)
                                    {
                                        msg.AppendFormat(string.Format("{0}: {1}%{2}", debugInfo.Rule, 100 * debugInfo.RuleCount / debugInfo.TotalRuleCount, (k++) % 2 == 1 ? "\n" : "\t"));
                                    }
                                    else
                                    {
                                        msg.AppendFormat(string.Format("{0}{1}", debugInfo.Rule, (k++) % 2 == 1 ? "\n" : "\t"));
                                    }
    							}
                                if (_aiPlayer.DebugInfo.TotalRuleCount > 0)
                                {
                                    _scene.SuggestGameType(gameType.ToDescription(_trump, true), msg.ToString().TrimEnd(), _t1 - _t0);
                                    _scene.SuggestGameTypeNew(gameType);
                                    _scene.SuggestGameFlavourNew(gameType);
                                }
                                //nasimulovany talon musime nahradit skutecnym pokud ho uz znam, jinak to udelam v ChooseTalon
                                if (_talon != null)
                                {
                                    _aiPlayer.Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, 
                                                                              _g.AllowAXTalon, _g.AllowTrumpTalon, _g.CancellationToken, _stringLoggerFactory, _talon)
									{
										ExternalDebugString = _aiPlayer._debugString
									};
									_aiPlayer.Probabilities.UpdateProbabilitiesAfterTalon(Hand, _talon);
                                }
                            }
                            catch (Exception ex)
                            {
                                _scene.GameException(this, new GameExceptionEventArgs { e = ex });
                            }
                        }, _cancellationTokenSource.Token);
                    }
                    _gameType = _scene.ChooseGameType(validGameTypes, true);
                    _givenUp = _gameType == 0;
                    CancelAiTask();
                    _g.ThrowIfCancellationRequested();
                    if ((_gameType & (Hra.Betl | Hra.Durch)) != 0)
                    {
                        return GameFlavour.Bad;
                    }
                    else if (_g.Top107 && _gameType == (Hra.Kilo | Hra.Sedma))
                    {
                        return GameFlavour.Good107;
                    }
                    else
                    {
                        return GameFlavour.Good;
                    }
                }
            }
            //nevolim, odpovidam na barvu
            _gameType = 0;

            if (_aiPlayer != null)
            {
				_t0 = Environment.TickCount;
                _aiTask = Task.Run(() =>
                {
                    try
                    {
                        _t1 = Environment.TickCount;
                        var flavour = _aiPlayer.ChooseGameFlavour();
                        var msg = _aiPlayer.DebugInfo.TotalRuleCount > 0
                                           ? string.Format("{0} ({1}%)\n", flavour.Description(), 100 * _aiPlayer.DebugInfo.RuleCount / _aiPlayer.DebugInfo.TotalRuleCount)
                                           : string.Format("{0}\n", flavour.Description());

                        _scene.SuggestGameFlavour(msg.TrimEnd(), _t1 - _t0);
                        _scene.SuggestGameFlavourNew(flavour);
                        //nasimulovany talon musime nahradit skutecnym pokud ho uz znam, jinak to udelam v ChooseTalon
                        if (_talon != null)
                        {
                            _aiPlayer.Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, 
                                                                      _g.AllowAXTalon, _g.AllowTrumpTalon, _g.CancellationToken, _stringLoggerFactory, _talon)
                            {
                                ExternalDebugString = _aiPlayer._debugString
                            };
                            _aiPlayer.Probabilities.UpdateProbabilitiesAfterTalon(Hand, _talon);
                        }
                    }
                    catch (Exception ex)
                    {
                        _scene.GameException(this, new GameExceptionEventArgs { e = ex });
                    }
                }, _cancellationTokenSource.Token);              
            }
            var gf = _scene.ChooseGameFlavour();

            CancelAiTask();
            _g.ThrowIfCancellationRequested();
            return gf;
        }

        public override Hra ChooseGameType(Hra validGameTypes)
        {
            _g.ThrowIfCancellationRequested();
            if(_gameType != 0 || _givenUp)
            {
                return _gameType;
            }
            if (_aiPlayer != null)
            {
                //var gt2 = _aiPlayer.ChooseGameTypeNew(validGameTypes);
                //_scene.SuggestGameTypeNew(gt2);

                _t0 = Environment.TickCount;
                _aiTask = Task.Run(() =>
                {
                    try
                    {
                        _t1 = Environment.TickCount;
                        var gameType = _aiPlayer.ChooseGameType(validGameTypes);
                        //var temp = new Bidding(_g.Bidding);
                        //temp.SetLastBidder(_aiPlayer, gameType);
                        //var e = temp.GetEventArgs(_aiPlayer, gameType, 0);
                        //var msg = new StringBuilder(string.Format("{0} ({1}%)\n", e.Description, _aiPlayer.DebugInfo.TotalRuleCount > 0 ? 100 * _aiPlayer.DebugInfo.RuleCount / _aiPlayer.DebugInfo.TotalRuleCount : -1));
                        var msg = new StringBuilder();
                        var k = 0;
                        foreach (var debugInfo in _aiPlayer.DebugInfo.AllChoices.Where(i => i.RuleCount > 0))
                        {
                            if (debugInfo.TotalRuleCount > 0)
                            {
                                msg.AppendFormat(string.Format("{0}: {1}%{2}", debugInfo.Rule, 100 * debugInfo.RuleCount / debugInfo.TotalRuleCount, (k++) % 2 == 1 ? "\n" : "\t"));
                            }
                            else
                            {
                                msg.AppendFormat(string.Format("{0}{1}", debugInfo.Rule, (k++) % 2 == 1 ? "\n" : "\t"));
                            }
                        }
                        if (_aiPlayer.DebugInfo.TotalRuleCount > 0)
                        {
                            _scene.SuggestGameType(gameType.ToDescription(_trump, true), msg.ToString().TrimEnd(), _t1 - _t0);
                            _scene.SuggestGameTypeNew(gameType);
                        }
                    }
                    catch (Exception ex)
                    {
                        _scene.GameException(this, new GameExceptionEventArgs { e = ex });
                    }
                }, _cancellationTokenSource.Token);
            }
            var gt = _scene.ChooseGameType(validGameTypes, true);

            CancelAiTask();
            _g.ThrowIfCancellationRequested();
            return gt;
        }

        public override Hra GetBidsAndDoubles(Bidding bidding)
        {
            _g.ThrowIfCancellationRequested();
            if (_givenUp &&
                bidding.SevenAgainstMultiplier == 0 &&
                bidding.HundredAgainstMultiplier == 0)
            {
                return 0;
            }
            if (_aiPlayer != null)
            {
                _t0 = Environment.TickCount;
                var temp = new Bidding(bidding);                                    //vyrobit kopii objektu
                _aiTask = Task.Run(() =>
                { 
                    try
                    {
					_t1 = Environment.TickCount;
                        var bid = _aiPlayer.GetBidsAndDoubles(bidding);
                        temp.SetLastBidder(_aiPlayer, bid);                         //nasimulovat reakci (tato operace manipuluje s vnitrnim stavem - proto pracujeme s kopii)
                        var e = temp.GetEventArgs(_aiPlayer, bid, _previousBid);    //a zformatovat ji do stringu

                        BidConfidence = _aiPlayer.DebugInfo.TotalRuleCount > 0 ? (float)_aiPlayer.DebugInfo.RuleCount / (float)_aiPlayer.DebugInfo.TotalRuleCount : -1;
                        if (_aiPlayer.DebugInfo.TotalRuleCount > 0)
                        {
                            _scene.SuggestGameType(string.Format("{0} ({1}%)", e.Description, 100 * _aiPlayer.DebugInfo.RuleCount / _aiPlayer.DebugInfo.TotalRuleCount),
                                                       string.Empty, _t1 - _t0);
                        }
                        else
                        {
                            _scene.SuggestGameType(e.Description, string.Empty, _t1 - _t0);
                        }
                        _scene.SuggestBidsAndDoublesNew(bid);
                    }
                    catch (Exception ex)
                    {
                        _scene.GameException(this, new GameExceptionEventArgs { e = ex });
                    }
                }, _cancellationTokenSource.Token);
            }
            var bd = _scene.GetBidsAndDoubles(bidding);

            CancelAiTask();
            _g.ThrowIfCancellationRequested();
            return bd;
        }

        public override Card PlayCard(Round r)
        {
            Card card;
            var validationState = Renonc.Ok;

            _g.ThrowIfCancellationRequested();
            if (_aiPlayer != null)
            {
				_t0 = Environment.TickCount;
                _aiPlayer.ResetDebugInfo();
                _aiTask = Task.Run(() =>
                {
                    try
                    {
                        _t1 = Environment.TickCount;
                        if (_aiPlayer.Probabilities.IsUpdateProbabilitiesAfterTalonNeeded())
                        {
                            if (_talon == null || _talon.Count() == 0)
                            {
                                _talon = new List<Card>(_g.talon);
                            }
                            _aiPlayer._talon = new List<Card>(_talon);
                            _aiPlayer.Probabilities.UpdateProbabilitiesAfterTalon(Hand, _talon);
                        }
                        var cardToplay = _aiPlayer.PlayCard(r);

                        _scene.SimulatedSuccessRate = _aiPlayer.DebugInfo.TotalRuleCount > 0 ? _aiPlayer.DebugInfo.RuleCount / _aiPlayer.DebugInfo.TotalRuleCount : -1;
                        if (_aiPlayer.DebugInfo.TotalRuleCount > 0)
                        {
                            _scene.SuggestCardToPlay(_aiPlayer.DebugInfo.Card, _aiPlayer.DebugInfo.Card.ToString(), _aiPlayer.DebugInfo.Rule, _t1 - _t0);
                        }
                    }
                    catch(Exception ex)
                    {
                        _scene.GameException(this, new GameExceptionEventArgs{ e = ex });
                    }
                }, _cancellationTokenSource.Token);
            }
            while (true)
            {
                card = _scene.PlayCard(validationState);
                if (card == null)
                {
                    continue;
                }
                if (r.c2 != null)
                {
                    validationState = IsCardValid(card, r.c1, r.c2);
                    if (validationState == Renonc.Ok)
                        break;
                }
                else if (r.c1 != null)
                {
                    validationState = IsCardValid(card, r.c1);
                    if (validationState == Renonc.Ok)
                        break;
                }
                else
                {
                    validationState = IsCardValid(card);
                    if (validationState == Renonc.Ok)
                        break;
                }
            }

            CancelAiTask();
            _g.ThrowIfCancellationRequested();
            return card;
        }
            
        private void GameFlavourChosen(object sender, GameFlavourChosenEventArgs e)
        {
            if (e.Flavour == GameFlavour.Bad && e.Player.PlayerIndex != PlayerIndex)
            {
                _talon = null;
                if (_aiPlayer != null && _aiPlayer.Probabilities != null)
                {
                    _aiPlayer.Probabilities.UpdateProbabilitiesAfterGameFlavourChosen(e);
                }
            }
        }

        private void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            _previousBid = e.GameType;
            if (e.GameStartingPlayerIndex != PlayerIndex && _aiPlayer != null)
            {
                _aiPlayer.Probabilities = new Probability(PlayerIndex, e.GameStartingPlayerIndex, new Hand(Hand), 
                                                          e.TrumpCard != null ? e.TrumpCard.Suit : (Barva?)null, 
                                                          _g.AllowAXTalon, _g.AllowTrumpTalon, _g.CancellationToken, _stringLoggerFactory, _talon)
                {
                    ExternalDebugString = _aiPlayer._debugString
                };
                _aiPlayer.Probabilities.UpdateProbabilitiesAfterGameTypeChosen(e);
            }
        }

        private new void BidMade(object sender, BidEventArgs e)
        {
            _previousBid = e.BidMade;
            if (_aiPlayer != null)
            {
                _aiPlayer.Probabilities.UpdateProbabilitiesAfterBidMade(e, _g.Bidding);
            }
        }
    }
}
