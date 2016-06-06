﻿using System;
using System.Collections.Generic;
using System.Linq;
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
        private Game _g;
        private AiPlayer _aiPlayer;
        private Task _aiTask;
        private CancellationTokenSource _cancellationTokenSource;
        public Probability Probabilities { get; set; }

        public HumanPlayer(Game g, Mariasek.Engine.New.Configuration.ParameterConfigurationElementCollection aiConfig, MainScene scene, bool showHint)
            : base(g)
        {
            _scene = scene;
            _g = g;
            _aiPlayer = showHint ? new AiPlayer(_g, aiConfig) { Name = "Advisor" } : null;
            _g.GameFlavourChosen += GameFlavourChosen;
            _g.GameTypeChosen += GameTypeChosen;
        }

        public override void Init()
        {
            _gameType = 0;
            _talon = null;
            firstTimeChoosingFlavour = true;
            Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, _g.talon);
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

        public void CancelAiTask()
        {
            if (_aiTask != null && _aiTask.Status == TaskStatus.Running)
            {
                _cancellationTokenSource.Cancel();
                try
                {
                    _aiTask.Wait();
                }
                catch (Exception ex)
                {
                    while (ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                    }
                    if (!(ex is OperationCanceledException)) //ignore OperationCanceledException
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
            if (_aiPlayer != null)
            {
                _aiTask = Task.Run(() =>
                    { 
                        var trump = _aiPlayer.ChooseTrump();
                        _scene.SuggestTrump(trump);
                    }, _cancellationTokenSource.Token);
            }
            var trumpCard = _scene.ChooseTrump();

            CancelAiTask();
            _trump = trumpCard.Suit;
            return trumpCard;
        }

        public override List<Card> ChooseTalon()
        {
            _talon = _scene.ChooseTalon();

            CancelAiTask();
            return _talon;
        }

        public override GameFlavour ChooseGameFlavour()
        {
            if (firstTimeChoosingFlavour)
            {                
                firstTimeChoosingFlavour = false;
                //poprve volici hrac nehlasi dobra/spatna ale vybira z typu her, cimz se dobra/spatna implicitne zvoli
                if (PlayerIndex == _g.GameStartingPlayerIndex)
                {
                    var validGameTypes = Hra.Hra | Hra.Kilo | Hra.Betl | Hra.Durch;

                    if (Hand.Contains(new Card(_trump, Hodnota.Sedma)))
                    {
                        validGameTypes |= Hra.Sedma;
                    }
                    if (_aiPlayer != null)
                    {
                        _aiTask = Task.Run(() =>
                            {
                                //dej 2 karty z ruky do talonu aby byl _aiPlayer v aktualnim stavu
                                _aiPlayer._talon = _talon;
                                _aiPlayer.Hand = Hand;
                                var flavour = _aiPlayer.ChooseGameFlavour();
                                if (flavour == GameFlavour.Good)
                                {
                                    validGameTypes &= ((Hra)~0 ^ (Hra.Betl | Hra.Durch));
                                }
                                else
                                {
                                    validGameTypes &= (Hra.Betl | Hra.Durch);
                                }
                                var gameType = _aiPlayer.ChooseGameType(validGameTypes);
                                var e = _g.Bidding.GetEventArgs(_aiPlayer, gameType, 0);
                                _scene.SuggestGameType(string.Format("{0} ({1}%)", e.Description, 100 * _aiPlayer.DebugInfo.RuleCount / _aiPlayer.DebugInfo.TotalRuleCount));
                            }, _cancellationTokenSource.Token);
                    }
                    _gameType = _scene.ChooseGameType(validGameTypes);

                    CancelAiTask();
                    if ((_gameType & (Hra.Betl | Hra.Durch)) != 0)
                    {
                        return GameFlavour.Bad;
                    }
                    else
                    {
                        return GameFlavour.Good;
                    }
                }
            }
            _gameType = 0;

            if (_aiPlayer != null)
            {
                _aiTask = Task.Run(() =>
                    { 
                        var flavour = _aiPlayer.ChooseGameFlavour();
                        _scene.SuggestGameFlavour(flavour);
                    }, _cancellationTokenSource.Token);                
            }
            var gf = _scene.ChooseGameFlavour();

            CancelAiTask();
            return gf;
        }

        public override Hra ChooseGameType(Hra validGameTypes)
        {
            if(_gameType != 0)
            {
                return _gameType;
            }
            if (_aiPlayer != null)
            {
                _aiTask = Task.Run(() =>
                    { 
                        var gameType = _aiPlayer.ChooseGameType(validGameTypes);
                        var temp = new Bidding(_g.Bidding);
                        temp.SetLastBidder(_aiPlayer, gameType);
                        var e = temp.GetEventArgs(_aiPlayer, gameType, 0);
                        _scene.SuggestGameType(string.Format("{0} ({1}%)", e.Description, 100 * _aiPlayer.DebugInfo.RuleCount / _aiPlayer.DebugInfo.TotalRuleCount));
                    }, _cancellationTokenSource.Token);
            }
            var gt = _scene.ChooseGameType(validGameTypes);

            CancelAiTask();
            return gt;
        }

        public override Hra GetBidsAndDoubles(Bidding bidding)
        {
            if (_aiPlayer != null)
            {
                _aiTask = Task.Run(() =>
                    { 
                        var bid = _aiPlayer.GetBidsAndDoubles(bidding);
                        var temp = new Bidding(bidding);                            //vyrobit kopii objektu
                        temp.SetLastBidder(_aiPlayer, bid);                         //nasimulovat reakci (tato operace manipuluje s vnitrnim stavem - proto pracujeme s kopii)
                        var e = temp.GetEventArgs(_aiPlayer, bid, _previousBid);    //a zformatovat ji do stringu
                        _scene.SuggestGameType(e.Description);
                    }, _cancellationTokenSource.Token);
            }
            var bd = _scene.GetBidsAndDoubles(bidding);

            CancelAiTask();
            return bd;
        }

        public override Card PlayCard(Round r)
        {
            Card card;
            var validationState = Renonc.Ok;

            if (_aiPlayer != null)
            {
                _aiTask = Task.Run(() =>
                    { 
                        var cardToplay = _aiPlayer.PlayCard(r);
                        var hint = string.Format("{2}\n{0} ({1}%)", _aiPlayer.DebugInfo.Rule, (100 * _aiPlayer.DebugInfo.RuleCount) / _aiPlayer.DebugInfo.TotalRuleCount, _aiPlayer.DebugInfo.Card);
                        _scene.SuggestCardToPlay(_aiPlayer.DebugInfo.Card, hint);
                    }, _cancellationTokenSource.Token);
            }
            while (true)
            {
                card = _scene.PlayCard(validationState);
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
            return card;
        }
            
        private void GameFlavourChosen(object sender, GameFlavourChosenEventArgs e)
        {
            if (e.Flavour == GameFlavour.Bad && e.Player.PlayerIndex != PlayerIndex)
            {
                _talon = null;
            }
        }

        private void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            _previousBid = e.GameType;
        }

        private void BidMade(object sender, BidEventArgs e)
        {
            _previousBid = e.BidMade;
        }
    }
}
