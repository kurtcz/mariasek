using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Mariasek.Engine;

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
        public AiPlayer _aiPlayer;
        private Task _aiTask;
        private CancellationTokenSource _cancellationTokenSource;
        public Probability Probabilities { get; set; }
        private Func<IStringLogger> _stringLoggerFactory;
        private bool _givenUp;
        private int _t0;
        private int _t1;

        //public HumanPlayer(Game g, Mariasek.Engine.Configuration.ParameterConfigurationElementCollection aiConfig, MainScene scene, bool showHint)
        public HumanPlayer(Game g, AiPlayerSettings settings, MainScene scene, bool showHint)
            : base(g)
        {
            //var b = bool.Parse(aiConfig["DoLog"].Value);
            _stringLoggerFactory = () => new StringLogger(false);// b);
            _scene = scene;
            _g = g;
            if (showHint)
            {
                _aiPlayer = new AiPlayer(_g, settings) { Name = "Advisor", AdvisorMode = true };
                DebugInfo = _aiPlayer.DebugInfo;
            }
            else
            {
                _aiPlayer = null;
                DebugInfo = new PlayerDebugInfo();
            }
            _g.GameFlavourChosen += GameFlavourChosen;
            _g.GameTypeChosen += GameTypeChosen;
            _g.CardPlayed += CardPlayed;
        }

        public override void Init()
        {
            _gameType = 0;
            _talon = null;
            _trumpCard = null;
            firstTimeChoosingFlavour = true;
            Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump,
                                            _g.AllowFakeSeven || _g.AllowFake107, _g.AllowAXTalon, _g.AllowTrumpTalon,
                                            _g.CancellationToken, _stringLoggerFactory, _talon);
            _cancellationTokenSource = new CancellationTokenSource();
            if (_aiPlayer != null)
            {
                _aiPlayer.Init();
                _aiPlayer.ThrowIfCancellationRequested = ThrowIfAiCancellationRequested;
                _aiPlayer.Hand = new List<Card>();
                _aiPlayer.Hand.AddRange(Hand);
                _aiPlayer.GameComputationProgress += _scene.GameComputationProgress;
            }
            //if (_g.GameStartingPlayerIndex != 0)
            //{
            //    _scene.UpdateHand(flipCardsUp: true);
            //}
        }

        public override void Die()
        {
            CancelAiTask();
            _scene = null;
            base.Die();
        }

        public void CancelAiTask()
        {
            if (_aiTask != null && _aiTask.Status == TaskStatus.Faulted)
            {
                _scene.GameException(this, new GameExceptionEventArgs(){ e = _aiTask.Exception });
            }
            if (_aiTask != null && (_aiTask.Status == TaskStatus.Created ||
                                    _aiTask.Status == TaskStatus.Running ||
                                    _aiTask.Status == TaskStatus.WaitingForActivation ||
                                    _aiTask.Status == TaskStatus.WaitingToRun ||
                                    _aiTask.Status == TaskStatus.WaitingForChildrenToComplete))
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

        public override async Task<Card> ChooseTrump()
        {
            _g.ThrowIfCancellationRequested();
            if (_aiPlayer != null)
            {
                _t0 = Environment.TickCount;
                _aiTask = Task.Run(async () =>
                {
                    try
                    {
                        _t1 = Environment.TickCount;
                        var trump = await _aiPlayer.ChooseTrump();
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

        public override async Task<List<Card>> ChooseTalon()
        {
            if (_aiPlayer != null)
            {
                _t0 = Environment.TickCount;
                _aiTask = Task.Run(async () =>
                {
                    _g.PreGameHook();
                    try
                    {
                        _t1 = Environment.TickCount;
                        _aiPlayer._talon = null;
                        _aiPlayer.Hand = new List<Card>(Hand);
                        var talon = await _aiPlayer.ChooseTalon();

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
                _talon = new List<Card>(_scene.ChooseTalon());
            } while (_talon == null || _talon.Count() != 2);

            CancelAiTask();
            _g.ThrowIfCancellationRequested();
            if (_aiPlayer != null)
            {
                _aiPlayer.Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump,
                                                          _g.AllowFakeSeven || _g.AllowFake107, _g.AllowAXTalon, _g.AllowTrumpTalon,
                                                          _g.CancellationToken, _stringLoggerFactory, _talon)
                {
                    ExternalDebugString = _aiPlayer._debugString
                };
                //pokud ai radil talon na betla ale my hrac vybral jiny talon, tak zapomen co ai radil
                if (_aiPlayer._gameType == Hra.Betl &&
                    (_aiPlayer._talon == null ||
                     _aiPlayer._talon.Any(i => !_talon.Contains(i))))
                {
                    _aiPlayer._gameType = null;
                }
                _aiPlayer.Hand = new List<Card>(Hand.Where(i => !_talon.Contains(i)));
                _aiPlayer._talon = new List<Card>(_talon);
                _aiPlayer.Probabilities.UpdateProbabilitiesAfterTalon(_aiPlayer.Hand, _aiPlayer._talon);
            }
            Probabilities.UpdateProbabilitiesAfterTalon(Hand.Where(i => !_talon.Contains(i)).ToList(), _talon);

            return _talon;
        }

        public override async Task<GameFlavour> ChooseGameFlavour()
        {
            _g.ThrowIfCancellationRequested();
            if (firstTimeChoosingFlavour)
            {                
                firstTimeChoosingFlavour = false;
                //poprve volici hrac nehlasi dobra/spatna ale vybira z typu her, cimz se dobra/spatna implicitne zvoli
                if (PlayerIndex == _g.GameStartingPlayerIndex)
                {
                    var validGameTypes = Hra.Hra | Hra.Betl | Hra.Durch;

                    if (Hand.Contains(new Card(_trump, Hodnota.Sedma)) ||
                        _g.AllowFakeSeven)
                    {
                        validGameTypes |= Hra.Sedma;
                    }
                    if (Enum.GetValues(typeof(Barva)).Cast<Barva>().Any(b => Hand.HasK(b) && Hand.HasQ(b)))
                    {
                        validGameTypes |= Hra.Kilo;

                        if (_g.AllowFake107)
                        {
                            validGameTypes |= Hra.Sedma;
                        }
                    }
                    if (_aiPlayer != null)
                        
                    {
                        //var gt2 = _aiPlayer.ChooseGameTypeNew(validGameTypes);
                        //_scene.SuggestGameTypeNew(gt2);
                        //_scene.SuggestGameFlavourNew(gt2);

                        _t0 = Environment.TickCount;
                        _aiTask = Task.Run(async () =>
                        {
                            try
                            {
                                _t1 = Environment.TickCount;
                                _g.PreGameHook();
                                //dej 2 karty z ruky do talonu aby byl _aiPlayer v aktualnim stavu
                                _aiPlayer._talon = new List<Card>(_talon);
                                _aiPlayer.Hand = new List<Card>(Hand);
                                var flavour = _scene.TrumpCardTakenBack ? GameFlavour.Bad : await _aiPlayer.ChooseGameFlavour(); //uvnitr se zvoli talon, ale clovek muze ve skutecnosti volit jinak nez ai!!!
                                if (flavour == GameFlavour.Good ||
                                    flavour == GameFlavour.Good107)
                                {
                                    validGameTypes &= ((Hra)~0 ^ (Hra.Betl | Hra.Durch));
                                }
                                else
                                {
                                    validGameTypes &= (Hra.Betl | Hra.Durch);
                                }
                                var gameType = await _aiPlayer.ChooseGameType(validGameTypes);
                                //var e = _g.Bidding.GetEventArgs(_aiPlayer, gameType, 0);
                                var msg = new StringBuilder();
                                var k = 0;
                                if (_aiPlayer.DebugInfo != null)
                                {
                                    foreach (var debugInfo in _aiPlayer.DebugInfo.AllChoices.Where(j => j.RuleCount > 0))
                                    {
                                        if (debugInfo.TotalRuleCount > 0)
                                        {
                                            if (debugInfo.Rule.StartsWith("Kilo"))
                                            {
                                                msg.AppendFormat(string.Format("{0}: {1}%{2}", debugInfo.Rule, _aiPlayer.DebugInfo.EstimatedHundredWinProbability, (k++) % 2 == 1 ? "\n" : "\t"));
                                            }
                                            else
                                            {
                                                msg.AppendFormat(string.Format("{0}: {1}%{2}", debugInfo.Rule, 100 * debugInfo.RuleCount / debugInfo.TotalRuleCount, (k++) % 2 == 1 ? "\n" : "\t"));
                                            }
                                        }
                                        else
                                        {
                                            msg.AppendFormat(string.Format("{0}{1}", debugInfo.Rule, (k++) % 2 == 1 ? "\n" : "\t"));
                                        }
                                    }
                                }
                                if (_aiPlayer.DebugInfo?.TotalRuleCount > 0 ||
                                    (gameType & (Hra.Betl | Hra.Durch)) != 0 ||
                                    (!Hand.Has7(_g.trump.Value) &&
                                     ((gameType == (Hra.Kilo | Hra.Sedma) &&
                                       _g.AllowFake107) ||
                                      (gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&
                                       _g.AllowFakeSeven)))
                                {
                                    _scene.SuggestGameType(gameType.ToDescription(_trump, true), msg.ToString().TrimEnd(), _t1 - _t0);
                                    _scene.SuggestGameTypeNew(gameType);
                                    _scene.SuggestGameFlavourNew(gameType);
                                }
                                //nasimulovany talon musime nahradit skutecnym pokud ho uz znam, jinak to udelam v ChooseTalon
                                if (_talon != null)
                                {
                                    _aiPlayer.Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump,
                                                                              _g.AllowFakeSeven || _g.AllowFake107, _g.AllowAXTalon, _g.AllowTrumpTalon,
                                                                              _g.CancellationToken, _stringLoggerFactory, _aiPlayer._talon)
                                    {
                                        ExternalDebugString = _aiPlayer._debugString
                                    };
                                    _aiPlayer.Probabilities.UpdateProbabilitiesAfterTalon(Hand, _aiPlayer._talon);
                                }
                            }
                            catch (Exception ex)
                            {
                                _scene.GameException(this, new GameExceptionEventArgs { e = ex });
                            }
                        }, _cancellationTokenSource.Token);
                    }
                    _gameType = _scene.ChooseGameType(validGameTypes);
                    _givenUp = _gameType == 0;
                    //upravim procenta podle skutecne zvoleneho typu hry (hrac muze zavolit jinak nez napoveda)
                    if (_aiPlayer != null)
                    {
                        if (_gameType == Hra.Durch)
                        {
                            _aiPlayer.DebugInfo.RuleCount = _aiPlayer.DebugInfo.AllChoices
                                                                     .Where(i => i.Rule == Hra.Durch.ToString())
                                                                     .Select(i => i.RuleCount)
                                                                     .FirstOrDefault();
                            _aiPlayer.DebugInfo.TotalRuleCount = _aiPlayer.DebugInfo.AllChoices
                                                                          .Where(i => i.Rule == Hra.Durch.ToString())
                                                                          .Select(i => i.TotalRuleCount)
                                                                          .FirstOrDefault();
                        }
                        else if (_gameType == Hra.Betl)
                        {
                            _aiPlayer.DebugInfo.RuleCount = _aiPlayer.DebugInfo.AllChoices
                                                                     .Where(i => i.Rule == Hra.Betl.ToString())
                                                                     .Select(i => i.RuleCount)
                                                                     .FirstOrDefault();
                            _aiPlayer.DebugInfo.TotalRuleCount = _aiPlayer.DebugInfo.AllChoices
                                                                          .Where(i => i.Rule == Hra.Betl.ToString())
                                                                          .Select(i => i.TotalRuleCount)
                                                                          .FirstOrDefault();
                        }
                        else if ((_gameType & Hra.Kilo) != 0)
                        {
                            _aiPlayer.DebugInfo.RuleCount = _aiPlayer.DebugInfo.AllChoices
                                                                     .Where(i => i.Rule.StartsWith(Hra.Kilo.ToString()))
                                                                     .Select(i => i.RuleCount)
                                                                     .FirstOrDefault();
                            _aiPlayer.DebugInfo.TotalRuleCount = _aiPlayer.DebugInfo.AllChoices
                                                                          .Where(i => i.Rule.StartsWith(Hra.Kilo.ToString()))
                                                                          .Select(i => i.TotalRuleCount)
                                                                          .FirstOrDefault();
                        }
                        else if ((_gameType & Hra.Sedma) != 0)
                        {
                            _aiPlayer.DebugInfo.RuleCount = _aiPlayer.DebugInfo.AllChoices
                                                                     .Where(i => i.Rule.StartsWith(Hra.Sedma.ToString()))
                                                                     .Select(i => i.RuleCount)
                                                                     .FirstOrDefault();
                            _aiPlayer.DebugInfo.TotalRuleCount = _aiPlayer.DebugInfo.AllChoices
                                                                          .Where(i => i.Rule.StartsWith(Hra.Sedma.ToString()))
                                                                          .Select(i => i.TotalRuleCount)
                                                                          .FirstOrDefault();
                        }
                        else if ((_gameType & Hra.Hra) != 0)
                        {
                            _aiPlayer.DebugInfo.RuleCount = _aiPlayer.DebugInfo.AllChoices
                                                                     .Where(i => i.Rule.StartsWith(Hra.Hra.ToString()))
                                                                     .Select(i => i.RuleCount)
                                                                     .FirstOrDefault();
                            _aiPlayer.DebugInfo.TotalRuleCount = _aiPlayer.DebugInfo.AllChoices
                                                                          .Where(i => i.Rule.StartsWith(Hra.Hra.ToString()))
                                                                          .Select(i => i.TotalRuleCount)
                                                                          .FirstOrDefault();
                        }
                    }
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
                _aiTask = Task.Run(async () =>
                {
                    try
                    {
                        _t1 = Environment.TickCount;
                        var flavour = await _aiPlayer.ChooseGameFlavour();
                        var msg = _aiPlayer.DebugInfo?.TotalRuleCount > 0
                                           ? string.Format("{0} ({1}%)\n", flavour.Description(), 100 * _aiPlayer.DebugInfo.RuleCount / _aiPlayer.DebugInfo.TotalRuleCount)
                                           : string.Format("{0}\n", flavour.Description());

                        _scene.SuggestGameFlavour(msg.TrimEnd(), _t1 - _t0);
                        _scene.SuggestGameFlavourNew(flavour);
                        //nasimulovany talon musime nahradit skutecnym pokud ho uz znam, jinak to udelam v ChooseTalon
                        if (_talon != null)
                        {
                            _aiPlayer.Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump,
                                                                      _g.AllowFakeSeven || _g.AllowFake107, _g.AllowAXTalon, _g.AllowTrumpTalon,
                                                                      _g.CancellationToken, _stringLoggerFactory, _talon)
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

        public override async Task<Hra> ChooseGameType(Hra validGameTypes)
        {
            _g.ThrowIfCancellationRequested();
            if (_gameType != 0 || _givenUp)
            {
                return _gameType;
            }
            if (_aiPlayer != null)
            {
                _t0 = Environment.TickCount;
                _aiTask = Task.Run(async () =>
                {
                    try
                    {
                        _t1 = Environment.TickCount;
                        var gameType = await _aiPlayer.ChooseGameType(validGameTypes);
                        //var temp = new Bidding(_g.Bidding);
                        //temp.SetLastBidder(_aiPlayer, gameType);
                        //var e = temp.GetEventArgs(_aiPlayer, gameType, 0);
                        //var msg = new StringBuilder(string.Format("{0} ({1}%)\n", e.Description, _aiPlayer.DebugInfo.TotalRuleCount > 0 ? 100 * _aiPlayer.DebugInfo.RuleCount / _aiPlayer.DebugInfo.TotalRuleCount : -1));
                        var msg = new StringBuilder();
                        var k = 0;
                        if (_aiPlayer.DebugInfo != null)
                        {
                            foreach (var debugInfo in _aiPlayer.DebugInfo.AllChoices.Where(i => i.RuleCount > 0))
                            {
                                if (debugInfo.TotalRuleCount > 0)
                                {
                                    if (debugInfo.Rule.StartsWith("Kilo"))
                                    {
                                        msg.AppendFormat(string.Format("{0}: {1}%{2}", debugInfo.Rule, _aiPlayer.DebugInfo.EstimatedHundredWinProbability, (k++) % 2 == 1 ? "\n" : "\t"));
                                    }
                                    else
                                    {
                                        msg.AppendFormat(string.Format("{0}: {1}%{2}", debugInfo.Rule, 100 * debugInfo.RuleCount / debugInfo.TotalRuleCount, (k++) % 2 == 1 ? "\n" : "\t"));
                                    }
                                }
                                else
                                {
                                    msg.AppendFormat(string.Format("{0}{1}", debugInfo.Rule, (k++) % 2 == 1 ? "\n" : "\t"));
                                }
                            }
                        }
                        if (_aiPlayer.DebugInfo?.TotalRuleCount > 0)
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
            var gt = _scene.ChooseGameType(validGameTypes);

            CancelAiTask();
            _g.ThrowIfCancellationRequested();
            return gt;
        }

        public override async Task<Hra> GetBidsAndDoubles(Bidding bidding)
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
                var temp = bidding.Clone();                                         //vyrobit kopii objektu
                _aiTask = Task.Run(async () =>
                { 
                    try
                    {
                    _t1 = Environment.TickCount;
                        var bid = await _aiPlayer.GetBidsAndDoubles(temp);
                        temp.SetLastBidder(_aiPlayer, bid);                         //nasimulovat reakci (tato operace manipuluje s vnitrnim stavem - proto pracujeme s kopii)
                        var e = temp.GetEventArgs(_aiPlayer, bid, _previousBid);    //a zformatovat ji do stringu

                        BidConfidence = _aiPlayer.DebugInfo?.TotalRuleCount > 0 ? (float)_aiPlayer.DebugInfo.RuleCount / (float)_aiPlayer.DebugInfo.TotalRuleCount : -1;
                        var msg = new StringBuilder();
#if DEBUG
                        var k = 0;
                        if (_aiPlayer.DebugInfo != null)
                        {
                            foreach (var debugInfo in _aiPlayer.DebugInfo.AllChoices.Where(i => i.RuleCount > 0 &&
                                                                                                (i.Rule != Hra.KiloProti.ToString() ||
                                                                                                 (bid & Hra.KiloProti) != 0)))
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
                        }
#endif
                        if (_aiPlayer.DebugInfo?.TotalRuleCount > 0)
                        {
                            _scene.SuggestGameType(string.Format("{0} ({1}%)", e.Description, 100 * _aiPlayer.DebugInfo.RuleCount / _aiPlayer.DebugInfo.TotalRuleCount),
                                                       msg.ToString(), _t1 - _t0);
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
            var bd = _scene.GetBidsAndDoubles(bidding.Clone());

            if ((bidding.Bids & Hra.Hra) != 0 &&
                _g.MandatoryDouble &&
                TeamMateIndex != -1 &&
                bidding.GameMultiplier < 2 &&
                (Hand.HasK(_g.trump.Value) ||
                 Hand.HasQ(_g.trump.Value)))
            {
                bd &= ~Hra.Hra;
            }
            if ((_g.GameType & Hra.Sedma) != 0) //pokud je hlasena sedma, pak tlacitko [7] znamena flek na sedmu, ne sedmu proti
            {
                bd &= ~Hra.SedmaProti;
            }
            else
            {
                bd &= ~Hra.Sedma;
            }
            CancelAiTask();
            _g.ThrowIfCancellationRequested();
            return bd;
        }

        public override async Task<Card> PlayCard(Round r)
        {
            Card card;
            var validationState = Renonc.Ok;

            _g.ThrowIfCancellationRequested();
            if (_aiPlayer != null)
            {
                _t0 = Environment.TickCount;
                _aiPlayer.ResetDebugInfo();
                _aiTask = Task.Run(async () =>
                {
                    try
                    {
                        _t1 = Environment.TickCount;
                        _aiPlayer.Hand = new List<Card>(Hand);
                        if (_aiPlayer.Probabilities.IsUpdateProbabilitiesAfterTalonNeeded())
                        {
                            if (_talon == null || _talon.Count() == 0)
                            {
                                _talon = new List<Card>(_g.talon);
                            }
                            _aiPlayer._talon = new List<Card>(_talon);
                            _aiPlayer.Probabilities.UpdateProbabilitiesAfterTalon(Hand, _talon);
                        }

                        var cardToplay = await _aiPlayer.PlayCard(r);

                        _scene.SimulatedSuccessRate = _aiPlayer.DebugInfo?.TotalRuleCount > 0 ? _aiPlayer.DebugInfo.RuleCount / _aiPlayer.DebugInfo.TotalRuleCount : -1;
                        if (_aiPlayer.DebugInfo?.TotalRuleCount > 0)
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
                //if (_scene.Game.Settings.AutoPlaySingletonCard)
                //{
                //    var validCards = r.c2 != null ? ValidCards(r.c1, r.c2) : r.c1 != null ? ValidCards(r.c1) : ValidCards();

                //    if (validCards.Count == 1)
                //    {
                //        return validCards.First();
                //    }
                //}
                var myPlayedCards = _g.rounds.Where(rr => rr != null && rr.c3 != null)
                                      .Select(rr =>
                                       {
                                           if (rr.player1.PlayerIndex == PlayerIndex)
                                           {
                                               return rr.c1;
                                           }
                                           else if (rr.player2.PlayerIndex == PlayerIndex)
                                           {
                                               return rr.c2;
                                           }
                                           else
                                           {
                                               return rr.c3;
                                           }
                                       }).ToList();
                card = _scene.PlayCard(validationState);
                if (card == null || myPlayedCards.Contains(card))
                {
                    validationState = Renonc.Ok;
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
            System.Diagnostics.Debug.WriteLine("HumanPlayer - PlayCard");
            return card;
        }
            
        private void GameFlavourChosen(object sender, GameFlavourChosenEventArgs e)
        {
            if (e.Flavour == GameFlavour.Bad && e.Player.PlayerIndex != PlayerIndex)
            {
                _talon = null;
                _givenUp = false;
                if (_aiPlayer != null && _aiPlayer.Probabilities != null)
                {
                    _aiPlayer.Probabilities.UpdateProbabilitiesAfterGameFlavourChosen(e);
                }
            }
            Probabilities.UpdateProbabilitiesAfterGameFlavourChosen(e);
        }

        private void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            _previousBid = e.GameType;
            if (e.GameStartingPlayerIndex != PlayerIndex)
            {
                if (_aiPlayer != null)
                {
                    _aiPlayer.Probabilities = new Probability(PlayerIndex, e.GameStartingPlayerIndex, new Hand(Hand),
                                                              e.TrumpCard != null ? e.TrumpCard.Suit : (Barva?)null,
                                                              _g.AllowFakeSeven || _g.AllowFake107, _g.AllowAXTalon, _g.AllowTrumpTalon,
                                                              _g.CancellationToken, _stringLoggerFactory, _talon)
                    {
                        ExternalDebugString = _aiPlayer._debugString
                    };
                    _aiPlayer.Probabilities.UpdateProbabilitiesAfterGameTypeChosen(e);
                }
            }
            Probabilities.UpdateProbabilitiesAfterGameTypeChosen(e);
        }

        private bool ShouldComputeBestCard(int roundNumber)
        {
            return _g.FirstMinMaxRound > 0 &&
                   roundNumber >= _g.FirstMinMaxRound &&
                   roundNumber < Game.NumRounds &&
                   //r.c1 == null &&
                   _g.GameType != Hra.Durch;
        }

        private void CardPlayed(object sender, Round r)
        {
            var gameWinningRound = IsGameWinningRound(r, _g.rounds, PlayerIndex, TeamMateIndex, new List<Card>(Hand), Probabilities);
            UpdateProbabilitiesAfterCardPlayed(Probabilities, r.number, r.player1.PlayerIndex, r.c1, r.c2, r.c3, r.hlas1, r.hlas2, r.hlas3, TeamMateIndex, _trump, gameWinningRound, ShouldComputeBestCard(r.number));
        }

        private static void UpdateProbabilitiesAfterCardPlayed(Probability probabilities, int roundNumber, int roundStarterIndex, Card c1, Card c2, Card c3, bool hlas1, bool hlas2, bool hlas3, int teamMateIndex, Barva? trump, bool gameWinningRound, bool shouldComputeBestCard)
        {
            if (c3 != null)
            {
                probabilities.UpdateProbabilities(roundNumber, roundStarterIndex, c1, c2, c3, hlas3, gameWinningRound, shouldComputeBestCard);
            }
            else if (c2 != null)
            {
                probabilities.UpdateProbabilities(roundNumber, roundStarterIndex, c1, c2, hlas2, gameWinningRound);
            }
            else
            {
                probabilities.UpdateProbabilities(roundNumber, roundStarterIndex, c1, hlas1);
            }
        }

        private new void BidMade(object sender, BidEventArgs e)
        {
            _previousBid = e.BidMade;
            if (_aiPlayer != null)
            {
                _aiPlayer.Probabilities.UpdateProbabilitiesAfterBidMade(e, _g.Bidding);
                if (_g.Bidding.SevenMultiplier > 0 &&
                    _g.Bidding.SevenMultiplier * _g.SevenValue < _g.Bidding.GameMultiplier * _g.GameValue &&
                    _g.GameStartingPlayer.DebugInfo.TotalRuleCount > 0)
                {
                    _aiPlayer.DebugInfo.RuleCount = _aiPlayer.DebugInfo.AllChoices
                                                             .Where(i => i.Rule.StartsWith(Hra.Hra.ToString()))
                                                             .Select(i => i.RuleCount)
                                                             .FirstOrDefault();
                    _aiPlayer.DebugInfo.TotalRuleCount = _aiPlayer.DebugInfo.AllChoices
                                                                  .Where(i => i.Rule.StartsWith(Hra.Hra.ToString()))
                                                                  .Select(i => i.TotalRuleCount)
                                                                  .FirstOrDefault();
                }
            }
            Probabilities.UpdateProbabilitiesAfterBidMade(e, _g.Bidding);
        }

        private bool IsGameWinningRound(Round round, Round[] rounds, int playerIndex, int teamMateIndex, List<Card> hand, Probability prob)
        {
            var basicPointsWonSoFar = 0;
            var basicPointsWonThisRound = (round?.c1?.Value >= Hodnota.Desitka ? 10 : 0) +
                                          (round?.c2?.Value >= Hodnota.Desitka ? 10 : 0) +
                                          (round?.c3?.Value >= Hodnota.Desitka ? 10 : 0);
            var basicPointsLost = 0;
            var hlasPointsLost = 0;
            var hlasPointsWon = 0;
            var hlasPointsWonThisRound = 0;
            var maxHlasPointsWon = 0;

            foreach (var r in rounds.Where(r => r?.number < round?.number))
            {
                if (r.c1.Value >= Hodnota.Desitka)
                {
                    if (r.roundWinner.PlayerIndex == playerIndex ||
                        r.roundWinner.PlayerIndex == teamMateIndex)
                    {
                        basicPointsWonSoFar += 10;
                    }
                    else
                    {
                        basicPointsLost += 10;
                    }
                }
                if (r.c2.Value >= Hodnota.Desitka)
                {
                    if (r.roundWinner.PlayerIndex == playerIndex ||
                        r.roundWinner.PlayerIndex == teamMateIndex)
                    {
                        basicPointsWonSoFar += 10;
                    }
                    else
                    {
                        basicPointsLost += 10;
                    }
                }
                if (r.c3.Value >= Hodnota.Desitka)
                {
                    if (r.roundWinner.PlayerIndex == playerIndex ||
                        r.roundWinner.PlayerIndex == teamMateIndex)
                    {
                        basicPointsWonSoFar += 10;
                    }
                    else
                    {
                        basicPointsLost += 10;
                    }
                }
                hlasPointsWonThisRound = 0;
                if (r.hlas1)
                {
                    if (r.player1.PlayerIndex == playerIndex ||
                        r.player1.PlayerIndex == teamMateIndex)
                    {
                        hlasPointsWonThisRound = r.c1.Suit == _trump ? 40 : 20;
                        hlasPointsWon += hlasPointsWonThisRound;
                        maxHlasPointsWon = teamMateIndex == -1
                                            ? _g.HlasConsidered == HlasConsidered.Highest
                                                ? Math.Max(maxHlasPointsWon, hlasPointsWonThisRound)
                                                : _g.HlasConsidered == HlasConsidered.First && maxHlasPointsWon == 0
                                                    ? hlasPointsWonThisRound
                                                    : _g.HlasConsidered == HlasConsidered.Each
                                                        ? hlasPointsWon
                                                        : 0
                                            : 0;
                    }
                    else
                    {
                        hlasPointsLost += r.c1.Suit == _trump ? 40 : 20;
                    }
                }
                if (r.hlas2)
                {
                    if (r.player2.PlayerIndex == playerIndex ||
                        r.player2.PlayerIndex == teamMateIndex)
                    {
                        hlasPointsWonThisRound = r.c2.Suit == _trump ? 40 : 20;
                        hlasPointsWon += hlasPointsWonThisRound;
                        maxHlasPointsWon = teamMateIndex == -1
                                            ? _g.HlasConsidered == HlasConsidered.Highest
                                                ? Math.Max(maxHlasPointsWon, hlasPointsWonThisRound)
                                                : _g.HlasConsidered == HlasConsidered.First && maxHlasPointsWon == 0
                                                    ? hlasPointsWonThisRound
                                                    : _g.HlasConsidered == HlasConsidered.Each
                                                        ? hlasPointsWon
                                                        : 0
                                            : 0;
                    }
                    else
                    {
                        hlasPointsLost += r.c2.Suit == _trump ? 40 : 20;
                    }
                }
                if (r.hlas3)
                {
                    if (r.player3.PlayerIndex == playerIndex ||
                        r.player3.PlayerIndex == teamMateIndex)
                    {
                        hlasPointsWonThisRound = r.c3.Suit == _trump ? 40 : 20;
                        hlasPointsWon += hlasPointsWonThisRound;
                        maxHlasPointsWon = teamMateIndex == -1
                                            ? _g.HlasConsidered == HlasConsidered.Highest
                                                ? Math.Max(maxHlasPointsWon, hlasPointsWonThisRound)
                                                : _g.HlasConsidered == HlasConsidered.First && maxHlasPointsWon == 0
                                                    ? hlasPointsWonThisRound
                                                    : _g.HlasConsidered == HlasConsidered.Each
                                                        ? hlasPointsWon
                                                        : 0
                                            : 0;
                    }
                    else
                    {
                        hlasPointsLost += r.c3.Suit == _trump ? 40 : 20;
                    }
                }
            }
            var basicPointsLeft = 90 - basicPointsWonSoFar - basicPointsWonThisRound - basicPointsLost;
            var player2 = (playerIndex + 1) % Game.NumPlayers;
            var player3 = (playerIndex + 2) % Game.NumPlayers;
            var opponent = teamMateIndex == player2 ? player3 : player2;
            var kqScore = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                              .Sum(b => hand.HasK(b) &&
                                        hand.HasQ(b)
                                        ? b == _trump ? 40 : 20
                                        : 0);
            var hlasCards = new List<Card>();
            if (round != null)
            {
                if (round.hlas1)
                {
                    hlasCards.Add(round.c1);
                }
                if (round.hlas2)
                {
                    hlasCards.Add(round.c2);
                }
                if (round.hlas3)
                {
                    hlasCards.Add(round.c3);
                }
            }
            var hlasPointsLeft = teamMateIndex == -1
                                 ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                       .Sum(b => !hlasCards.HasQ(b) &&
                                                 ((prob.PotentialCards(player2).HasK(b) &&
                                                   prob.PotentialCards(player2).HasQ(b)) ||
                                                  (prob.PotentialCards(player3).HasK(b) &&
                                                   prob.PotentialCards(player3).HasQ(b)))
                                                 ? b == _trump ? 40 : 20
                                                 : 0)
                                 : Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                       .Sum(b => prob.PotentialCards(opponent).HasK(b) &&
                                                 prob.PotentialCards(opponent).HasQ(b)
                                                 ? b == _trump ? 40 : 20
                                                 : 0);
            var opponentPotentialPoints = basicPointsLost + hlasPointsLost + basicPointsLeft + hlasPointsLeft;
            var gameWinningCard = false;

            if ((_gameType & Hra.Kilo) != 0 &&
                ((teamMateIndex == -1 &&
                  basicPointsWonSoFar + maxHlasPointsWon <= 90 &&
                  basicPointsWonSoFar + basicPointsWonThisRound + maxHlasPointsWon >= 100) ||
                 (teamMateIndex != -1 &&
                  basicPointsWonSoFar <= 30 &&
                  basicPointsWonSoFar + basicPointsWonThisRound >= 40)))
            {
                gameWinningCard = true;
            }
            else if ((_gameType & Hra.Kilo) == 0 &&
                     basicPointsWonSoFar + hlasPointsWon + kqScore <= opponentPotentialPoints &&
                     basicPointsWonSoFar + basicPointsWonThisRound + hlasPointsWon + kqScore > opponentPotentialPoints)
            {
                gameWinningCard = true;
            }

            return gameWinningCard;
        }
    }
}
