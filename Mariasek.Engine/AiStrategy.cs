﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
//using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
#if !PORTABLE
using System.Reflection;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Serialization.Json;
using System.Text;
//using log4net;
using Mariasek.Engine.Logger;

namespace Mariasek.Engine
{
    public class AiStrategy : AiStrategyBase
    {
#if !PORTABLE
        private static readonly new ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
#else
        private static readonly new ILog _log = new DummyLogWrapper();
#endif   
        private new Barva _trump { get { return base._trump.Value; } } //dirty
        protected HlasConsidered _hlasConsidered;
        private List<Barva> _bannedSuits = new List<Barva>();
        private List<Barva> _preferredSuits = new List<Barva>();
        public float RiskFactor { get; set; }
        public float RiskFactorSevenDefense { get; set; }
        public float SolitaryXThreshold { get; set; }
        public float SolitaryXThresholdDefense { get; set; }
        public Hra[] PlayerBids { get; set; }
        public int GameValue { get; set; }
        public int SevenValue { get; set; }
        private const float _epsilon = 0.01f;

        public AiStrategy(Barva? trump, Hra gameType, Hand[] hands, Round[] rounds, List<Barva> teamMatesSuits, HlasConsidered hlasConsidered, Probability probabilities)
            : base(trump, gameType, hands, rounds, teamMatesSuits, probabilities)
        {
            _hlasConsidered = hlasConsidered;
            if (!trump.HasValue)
            {
                throw new InvalidOperationException("AiStrategy2: trump is null");
            }
            RiskFactor = 0.275f; //0.2727f ~ (9 nad 5) / (11 nad 5)
            SolitaryXThreshold = 0.13f;
            SolitaryXThresholdDefense = 0.5f;
            RiskFactorSevenDefense = 0.5f;
        }

        private void BeforeGetRules()
        {
            _bannedSuits.Clear();
            _preferredSuits.Clear();
            //u sedmy mi nevadi, kdyz se spoluhrace tlacim desitky a esa, snazim se hlavne hrat proti sedme
            if (TeamMateIndex != -1)
            {
                var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                                ? (MyIndex + 2) % Game.NumPlayers
                                : (MyIndex + 1) % Game.NumPlayers;

                //nehraj barvu pokud mam eso a souper muze mit desitku
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                     .Where(b => b != _trump &&
                                                 _probabilities.CardProbability(MyIndex, new Card(b, Hodnota.Eso)) == 1 &&
                                                 _probabilities.CardProbability(opponent, new Card(b, Hodnota.Desitka)) > _epsilon))// &&
                                                 //_probabilities.HasSolitaryX(TeamMateIndex, b, RoundNumber) < SolitaryXThreshold))
                {
                    _bannedSuits.Add(b);
                }

                //pokud se nehraje sedma (proti), tak nehraj barvu
                //ve ktere na spoluhrac A nebo X a souper barvu nezna a ma trumfy
                if ((_gameType & Hra.Hra) != 0 &&
                    (_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 &&
                    _probabilities.SuitProbability(opponent, _trump, RoundNumber) > 0)
                {
                    foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                          .Where(b => _probabilities.SuitProbability(opponent, b, RoundNumber) == 0 &&
                                                      (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Eso)) >= 1 - _epsilon ||
                                                       _probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Desitka)) >= 1 - _epsilon)))
                    {
                        _bannedSuits.Add(b);
                    }
                }

                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>().Where(b => b != _trump))
                {
                    if (_probabilities.HasAOrXAndNothingElse(TeamMateIndex, b, RoundNumber) >= 1 - RiskFactor &&
                        _probabilities.SuitProbability(opponent, b, RoundNumber) == 0 &&
                        _probabilities.SuitProbability(opponent, _trump, RoundNumber) > 0)
                    {
                        _bannedSuits.Add(b);
                    }
                    if (_probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) == 0 &&
                        _hands[MyIndex].Where(i => i.Suit == b)
                                        .Any(i => _probabilities.SuitLowerThanCardProbability(opponent, i, RoundNumber) == 1))
                    {
                        _bannedSuits.Add(b);
                    }
                }

                foreach (var r in _rounds.Where(i => i != null && i.c3 != null))
                {
                    if (r.player2.PlayerIndex == TeamMateIndex &&
                        r.c2.Suit != r.c1.Suit &&
                        r.c2.Suit == _trump)
                    {
                        _bannedSuits.Add(r.c1.Suit);
                    }
                    else if (r.player3.PlayerIndex == TeamMateIndex &&
                        r.c3.Suit != r.c1.Suit &&
                        r.c3.Suit == _trump)
                    {
                        _bannedSuits.Add(r.c1.Suit);
                    }
                    if ((_gameType & Hra.Kilo) == 0 &&
                        r.player1.TeamMateIndex == -1 &&
                        _hands[MyIndex].CardCount(r.c1.Suit) > 1 &&   //pokud mam jen jednu kartu v barve, dovol mi ji odmazat
                        _probabilities.SuitProbability(r.player1.PlayerIndex, r.c1.Suit, RoundNumber) > 0 &&  //pouze pokud akter barvu zna, jinak je barva bezpecna
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => _hands[MyIndex].HasSuit(b) &&
                                        b != _trump &&
                                        b != r.c1.Suit)
                            .Any(b => !_bannedSuits.Contains(b)))
                    {
                        _bannedSuits.Add(r.c1.Suit);
                    }
                    if (r.player3.TeamMateIndex == -1 &&
                        (r.c3.Value == Hodnota.Eso ||
                         r.c3.Value == Hodnota.Desitka) &&
                        r.roundWinner.PlayerIndex != r.player3.PlayerIndex)
                    {
                        _preferredSuits.Add(r.c3.Suit);
                    }
                    if (r.player1.TeamMateIndex == -1 &&
                        r.player2.PlayerIndex == TeamMateIndex &&
                        r.c1.Suit != r.c2.Suit &&
                        r.c2.Suit != _trump &&
                        r.c2.Value >= Hodnota.Desitka &&
                        r.roundWinner.PlayerIndex != r.player1.PlayerIndex)
                    {
                        _preferredSuits.Add(r.c2.Suit);
                    }
                    else if (r.player1.TeamMateIndex == -1 &&
                             r.player3.PlayerIndex == TeamMateIndex &&
                             r.c1.Suit != r.c3.Suit &&
                             r.c3.Suit != _trump &&
                             r.c3.Value >= Hodnota.Desitka &&
                             r.roundWinner.PlayerIndex != r.player1.PlayerIndex)
                    {
                        _preferredSuits.Add(r.c3.Suit);
                    }
                    //deje se v AiPlayer.UpdateProbabilitiesAfterCardPlayed()
                    //if (r.player1.TeamMateIndex == MyIndex)
                    //{
                    //    _teamMatesSuits.Add(r.c1.Suit);
                    //}

                    if (r.player1.TeamMateIndex == -1 &&
                        RoundNumber <= 4 &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => _hands[MyIndex].HasSuit(b) &&
                                        b != _trump &&
                                        b != r.c1.Suit)
                            .Any(b => !_bannedSuits.Contains(b)))
                    {
                        _bannedSuits.Add(r.c1.Suit);
                    }
                }
                if (TeamMateIndex != -1)
                {
                    var teamMatesLikelyAXSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                     .Where(b => _hands[MyIndex].HasSuit(b) &&
                                                                 (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Eso)) > _epsilon ||
                                                                  _probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Desitka)) > _epsilon))
                                               .ToList();
                    if (teamMatesLikelyAXSuits.Count == 1 &&
                        !(SevenValue > GameValue &&
                          (PlayerBids[TeamMateIndex] & Hra.Sedma) != 0))
                    {
                        _bannedSuits.Add(teamMatesLikelyAXSuits.First());
                    }
                }
                if (_gameType != (Hra.Hra | Hra.Sedma))
                {
                    foreach (var r in _rounds.Where(i => i != null && i.c3 != null))
                    {
                        //if (r.player1.PlayerIndex == TeamMateIndex &&
                        //    (r.c1.Value == Hodnota.Eso ||
                        //     r.c1.Value == Hodnota.Desitka) &&
                        //    (_probabilities.CardProbability(r.player1.PlayerIndex, new Card(r.c1.Suit, Hodnota.Eso)) > _epsilon ||
                        //     _probabilities.CardProbability(r.player1.PlayerIndex, new Card(r.c1.Suit, Hodnota.Desitka)) > _epsilon) &&
                        //    r.roundWinner.PlayerIndex != TeamMateIndex &&
                        //    r.roundWinner.PlayerIndex != MyIndex)
                        //{
                        //    _bannedSuits.Add(r.c1.Suit);
                        //}
                        //else 
                        if (r.player2.PlayerIndex == TeamMateIndex &&
                                 (r.c2.Value == Hodnota.Eso ||
                                  r.c2.Value == Hodnota.Desitka) &&
                                 (_probabilities.CardProbability(r.player2.PlayerIndex, new Card(r.c2.Suit, Hodnota.Eso)) > _epsilon ||
                                  _probabilities.CardProbability(r.player2.PlayerIndex, new Card(r.c2.Suit, Hodnota.Desitka)) > _epsilon) &&
                                 r.c2.Suit == r.c1.Suit &&
                                 r.roundWinner.PlayerIndex != TeamMateIndex &&
                                 r.roundWinner.PlayerIndex != MyIndex)
                        {
                            _bannedSuits.Add(r.c2.Suit);
                        }
                        else if (r.player3.PlayerIndex == TeamMateIndex &&
                                 (r.c3.Value == Hodnota.Eso ||
                                  r.c3.Value == Hodnota.Desitka) &&
                                 (_probabilities.CardProbability(r.player3.PlayerIndex, new Card(r.c3.Suit, Hodnota.Eso)) > _epsilon ||
                                  _probabilities.CardProbability(r.player3.PlayerIndex, new Card(r.c3.Suit, Hodnota.Desitka)) > _epsilon) &&
                                 r.c3.Suit == r.c1.Suit &&
                                 r.roundWinner.PlayerIndex != TeamMateIndex &&
                                 r.roundWinner.PlayerIndex != MyIndex)
                        {
                            _bannedSuits.Add(r.c3.Suit);
                        }
                        if (r.player3.TeamMateIndex == MyIndex &&
                            (r.c3.Value == Hodnota.Eso ||
                             r.c3.Value == Hodnota.Desitka) &&
                            r.roundWinner.PlayerIndex != TeamMateIndex &&
                            r.roundWinner.PlayerIndex != MyIndex)
                        {
                            _bannedSuits.Add(r.c3.Suit);
                        }
                    }
                }
                if (_rounds[RoundNumber - 1] != null)
                {
                    //1. obrance:
                    if (_rounds[RoundNumber - 1].player1.PlayerIndex == MyIndex &&
                        _rounds[RoundNumber - 1].player2.PlayerIndex == TeamMateIndex)
                    {
                        //nehraj barvu, kde vis, ze akter ma jiste bodovanou kartu
                        var opponentXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                 .Where(b => b != _trump &&
                                                             _hands[MyIndex].HasSuit(b) &&
                                                             (_probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Eso)) >= 1 - _epsilon ||
                                                              _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Desitka)) >= 1 - _epsilon))
                                                 .ToList();
                        // nehraj barvu kde nemas A ani X a zatim je nikdo nehral
                        var noAXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Where(b => b != _trump &&
                                                        !_hands[MyIndex].HasA(b) &&
                                                        !_hands[MyIndex].HasX(b) &&
                                                        (_probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Eso)) > 0 ||
                                                         _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Eso)) > 0) &&
                                                        (_probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Desitka)) > 0 ||
                                                         _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Desitka)) > 0))
                                            .ToList();
                        // nehraj barvu kde mas A + neco ale ne X, pokud X muze mit akter
                        var AnoXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Where(b => b != _trump &&
                                                        _hands[MyIndex].HasA(b) &&
                                                        _hands[MyIndex].CardCount(b) > 1 &&
                                                        _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Desitka)) > _epsilon)
                                            .ToList();
                        var AKnoXsuits = AnoXsuits.Where(b => _hands[MyIndex].HasK(b))
                                                  .ToList();
                        // nehraj barvu kde nemas A ani X a zatim je nikdo nehral
                        var noAorXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Where(b => b != _trump &&
                                                        !_hands[MyIndex].HasA(b) &&
                                                        !_hands[MyIndex].HasX(b) &&
                                                        (_probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Eso)) > 0 ||
                                                         _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Eso)) > 0 ||
                                                         _probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Desitka)) > 0 ||
                                                         _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Desitka)) > 0))
                                            .ToList();
                        // nehraj barvu kterou kolega nezna pokud muze mit trumfy
                        var teamMateLackingSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                       .Where(b => b != _trump &&
                                                                   _probabilities.SuitProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, b, RoundNumber) > 0 &&
                                                                   _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) == 0 &&
                                                                   _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) > 0)
                                                       .ToList();
                        var suits = ((List<Card>)_hands[MyIndex]).Select(i => i.Suit).Distinct();

                        _bannedSuits.AddRange(opponentXsuits);
                        if (!(SevenValue > GameValue &&
                              (PlayerBids[TeamMateIndex] & Hra.Sedma) != 0))
                        {
                            if (suits.Where(i => i != _trump &&
                                                 !_bannedSuits.Contains(i))
                                     .Any(i => !noAXsuits.Contains(i)))
                            {
                                _bannedSuits.AddRange(noAXsuits);
                            }
                            if (suits.Where(i => i != _trump &&
                                                 !_bannedSuits.Contains(i))
                                     .Any(i => !AKnoXsuits.Contains(i)))
                            {
                                _bannedSuits.AddRange(AKnoXsuits);
                            }
                            if (suits.Where(i => i != _trump &&
                                                 !_bannedSuits.Contains(i))
                                     .Any(i => !AnoXsuits.Contains(i)))
                            {
                                _bannedSuits.AddRange(AnoXsuits);
                            }
                            if (suits.Where(i => i != _trump &&
                                                 !_bannedSuits.Contains(i))
                                     .Any(i => !noAorXsuits.Contains(i)))
                            {
                                _bannedSuits.AddRange(noAorXsuits);
                            }
                        }
                        if (suits.Where(i => i != _trump &&
                                             !_bannedSuits.Contains(i))
                                 .Any(i => !teamMateLackingSuits.Contains(i)))
                        {
                            _bannedSuits.AddRange(teamMateLackingSuits);
                        }
                    }
                    else
                    {
                        //2. obrance
                        //nehraj barvu, kde vis, ze akter ma jiste bodovanou kartu
                        var opponentXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                 .Where(b => b != _trump &&
                                                             _hands[MyIndex].HasSuit(b) &&
                                                             (_probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Eso)) >= 1 - _epsilon ||
                                                              _probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Desitka)) >= 1 - _epsilon))
                                                 .ToList();
                        // nehraj barvu kterou kolega nezna pokud muze mit trumfy
                        var teamMateLackingSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                       .Where(b => b != _trump &&
                                                                   _probabilities.SuitProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, b, RoundNumber) > 0 &&
                                                                   _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) == 0 &&
                                                                   _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) > 0);
                        var suits = ((List<Card>)_hands[MyIndex]).Select(i => i.Suit).Distinct();

                        _bannedSuits.AddRange(opponentXsuits);
                        if (suits.Where(i => i != _trump &&
                                             !_bannedSuits.Contains(i))
                                 .Any(i => !teamMateLackingSuits.Contains(i)))
                        {
                            _bannedSuits.AddRange(teamMateLackingSuits);
                        }
                        // nehraj barvu kde mas A + neco ale ne X, pokud X muze mit akter
                        var AnoXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Where(b => b != _trump &&
                                                        _hands[MyIndex].HasA(b) &&
                                                        _hands[MyIndex].CardCount(b) > 1 &&
                                                        _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Desitka)) > _epsilon)
                                            .ToList();
                        var AKnoXsuits = AnoXsuits.Where(b => _hands[MyIndex].HasK(b))
                                                  .ToList();
                        if (suits.Where(i => i != _trump &&
                                             !_bannedSuits.Contains(i))
                                 .Any(i => !AKnoXsuits.Contains(i)))
                        {
                            _bannedSuits.AddRange(AKnoXsuits);
                        }
                        if (suits.Where(i => i != _trump &&
                                             !_bannedSuits.Contains(i))
                                 .Any(i => !AnoXsuits.Contains(i)))
                        {
                            _bannedSuits.AddRange(AnoXsuits);
                        }
                    }
                }
            }
            else if (TeamMateIndex == -1)
            {
                foreach (var r in _rounds.Where(i => i != null && i.c3 != null))
                {
                    if (r.player1.PlayerIndex == MyIndex &&
                        ((r.c2.Suit != r.c1.Suit &&
                          r.c2.Suit != _trump &&
                          (r.c2.Value == Hodnota.Eso ||
                           r.c2.Value == Hodnota.Desitka) &&
                           (_probabilities.CardProbability(r.player2.PlayerIndex, new Card(r.c2.Suit, Hodnota.Eso)) > _epsilon ||
                            _probabilities.CardProbability(r.player2.PlayerIndex, new Card(r.c2.Suit, Hodnota.Desitka)) > _epsilon)) ||
                         ((r.c3.Value == Hodnota.Eso ||
                           r.c3.Value == Hodnota.Desitka) &&
                           (_probabilities.CardProbability(r.player3.PlayerIndex, new Card(r.c3.Suit, Hodnota.Eso)) > _epsilon ||
                            _probabilities.CardProbability(r.player3.PlayerIndex, new Card(r.c3.Suit, Hodnota.Desitka)) > _epsilon))) &&
                        r.roundWinner.PlayerIndex != MyIndex)
                    {
                        _bannedSuits.Add(r.c1.Suit);
                    }
                    else if (r.player2.PlayerIndex == MyIndex &&
                        (r.c3.Suit != r.c1.Suit &&
                         r.c3.Suit != _trump &&
                         (r.c3.Value == Hodnota.Eso ||
                          r.c3.Value == Hodnota.Desitka) &&
                          (_probabilities.CardProbability(r.player3.PlayerIndex, new Card(r.c1.Suit, Hodnota.Eso)) > _epsilon ||
                           _probabilities.CardProbability(r.player3.PlayerIndex, new Card(r.c1.Suit, Hodnota.Desitka)) > _epsilon)) &&
                        r.roundWinner.PlayerIndex != MyIndex)
                    {
                        _bannedSuits.Add(r.c1.Suit);
                    }
                    else if (r.player3.PlayerIndex == MyIndex &&
                        (r.c2.Suit != r.c1.Suit &&
                         r.c2.Suit != _trump &&
                         (r.c2.Value == Hodnota.Eso ||
                          r.c2.Value == Hodnota.Desitka) &&
                          (_probabilities.CardProbability(r.player2.PlayerIndex, new Card(r.c1.Suit, Hodnota.Eso)) > _epsilon ||
                           _probabilities.CardProbability(r.player2.PlayerIndex, new Card(r.c1.Suit, Hodnota.Desitka)) > _epsilon)) &&
                        r.roundWinner.PlayerIndex != MyIndex)
                    {
                        _bannedSuits.Add(r.c1.Suit);
                    }
                    if (r.player3.TeamMateIndex != -1 &&
                        (r.c3.Value == Hodnota.Eso ||
                         r.c3.Value == Hodnota.Desitka) &&
                        r.roundWinner.PlayerIndex != r.player3.PlayerIndex)
                    {
                        _preferredSuits.Add(r.c3.Suit);
                    }
                }
            }
            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                if (_hands[MyIndex].HasX(b) &&
                    _hands[MyIndex].CardCount(b) == 2 &&
                    (_probabilities.CardProbability((MyIndex + 1) % Game.NumPlayers, new Card(b, Hodnota.Eso)) > 0 ||
                     _probabilities.CardProbability((MyIndex + 2) % Game.NumPlayers, new Card(b, Hodnota.Eso)) > 0))
                {
                    _bannedSuits.Add(b);
                }
            }
            _bannedSuits = _bannedSuits.Distinct().ToList();
            if (_hands[MyIndex].Where(i => i.Suit != _trump &&
                                           i.Value < Hodnota.Desitka)
                               .All(i => _bannedSuits.Contains(i.Suit)))// &&
                //(!_hands[MyIndex].HasSuit(_trump) ||
                // !(_rounds[RoundNumber - 1] != null &&   //1. obrance muze hrat trumf, pokud nelze hrat nic jineho, ostatni hraci ne
                //    _rounds[RoundNumber - 1].player1.PlayerIndex == MyIndex &&
                //    _rounds[RoundNumber - 1].player2.PlayerIndex == TeamMateIndex)))
            {
                _bannedSuits.Clear();
                //pokus se nektere barvy vratit zpet na seznam zakazanych barev
                if (TeamMateIndex != -1 && _rounds[RoundNumber - 1] != null)
                {
                    if (_rounds[RoundNumber - 1].player1.PlayerIndex == MyIndex &&
                        _rounds[RoundNumber - 1].player2.PlayerIndex == TeamMateIndex)
                    {
                        // co-
                        //nehraj barvu, kde vis, ze akter ma jiste bodovanou kartu
                        var nonTrumpSuitCount = ((List<Card>)_hands[MyIndex]).Select(i => i.Suit).Where(b => b != _trump).Distinct().Count();
                        var opponentXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                 .Where(b => b != _trump &&
                                                             _hands[MyIndex].HasSuit(b) &&
                                                             (_probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Eso)) >= 1 - _epsilon ||
                                                              _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Desitka)) >= 1 - _epsilon))
                                                 .ToList();
                        _bannedSuits.AddRange(opponentXsuits.Take(nonTrumpSuitCount));
                    }
                    else
                    {
                        // c-o
                        //nehraj barvu, kde vis, ze akter ma jiste bodovanou kartu
                        var nonTrumpSuitCount = ((List<Card>)_hands[MyIndex]).Select(i => i.Suit).Where(b => b != _trump).Distinct().Count();
                        var opponentXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                 .Where(b => b != _trump &&
                                                             _hands[MyIndex].HasSuit(b) &&
                                                             (_probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Eso)) >= 1 - _epsilon ||
                                                              _probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Desitka)) >= 1 - _epsilon))
                                                 .ToList();
                        _bannedSuits.AddRange(opponentXsuits.Take(nonTrumpSuitCount));
                    }
                }
            }
        }

        private void BeforeGetRules23()
        {
            _bannedSuits.Clear();

            if (TeamMateIndex != -1)
            {
                var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                    ? (MyIndex + 2) % Game.NumPlayers
                    : (MyIndex + 1) % Game.NumPlayers;

                //nehraj barvu pokud mam eso a souper muze mit desitku
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                      .Where(b => b != _trump &&
                                                  _probabilities.CardProbability(MyIndex, new Card(b, Hodnota.Eso)) == 1 &&
                                                  _probabilities.CardProbability(opponent, new Card(b, Hodnota.Desitka)) > _epsilon))
                {
                    _bannedSuits.Add(b);
                }
                //nehraj barvu pokud mam vyssi v barve nez souper a kolega barvu nezna
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                      .Where(b => b != _trump &&
                                                  _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) == 0 &&
                                                  _probabilities.PotentialCards(opponent).HasSuit(b) &&
                                                  _probabilities.PotentialCards(opponent).Where(i => i.Suit == b)
                                                                                         .All(i => _hands[MyIndex].Any(j => j.Suit == i.Suit &&
                                                                                                                            j.Value > i.Value))))
                {
                    if (!_bannedSuits.Contains(b))
                    {
                        _bannedSuits.Add(b);
                    }
                }
            }
        }

        //: - souper
        //: o spoluhrac
        //: c libovolna karta
        //: X desitka
        //: A eso

        protected override IEnumerable<AiRule> GetRules1(Hand[] hands)
        {
            var player2 = (MyIndex + 1) % Game.NumPlayers;
            var player3 = (MyIndex + 2) % Game.NumPlayers;
            var lastRound = RoundNumber >= 2 ? _rounds[RoundNumber - 2] : null;
            var lastPlayer1 = lastRound != null ? lastRound.player1.PlayerIndex : -1;
            var lastOpponentLeadSuit = lastRound != null ? lastRound.c1.Suit : Barva.Cerveny;
            var isLastPlayer1Opponent = lastPlayer1 != MyIndex && lastPlayer1 != TeamMateIndex;
            var myPlayedCards = _rounds.Where(r => r != null && r.c3 != null)
                                       .Select(r =>
                                       {
                                           if (r.player1.PlayerIndex == MyIndex)
                                           {
                                               return r.c1;
                                           }
                                           else if (r.player2.PlayerIndex == MyIndex)
                                           {
                                               return r.c2;
                                           }
                                           else
                                           {
                                               return r.c3;
                                           }
                                       }).ToList();
            var myInitialHand = new List<Card>();

            myInitialHand.AddRange((List<Card>)hands[MyIndex]);
            myInitialHand.AddRange(myPlayedCards);
            BeforeGetRules();
            if (RoundNumber == 9)
            {
                yield return new AiRule()
                {
                    Order = 0,
                    Description = "hrát tak abych bral poslední štych",
                    #region ChooseCard1 Rule0
                    ChooseCard1 = () =>
                    {
                        IEnumerable<Card> cardsToPlay;

                        //pokud spoluhrac zahlasil sedmu proti, tak pravidlo nehraj
                        if (TeamMateIndex != -1 &&
                            (_gameType & Hra.SedmaProti) != 0 &&
                             _probabilities.CardProbability(TeamMateIndex, new Card(_trump, Hodnota.Sedma)) == 1)
                        {
                            return null;
                        }
                        //pokud mam jen jeden trumf a neni to sedma, tak ho posetri nakonec
                        //vyjimka je pokud spoluhrac zahlasil sedmu proti
                        if (hands[MyIndex].CardCount(_trump) == 1 &&
                            !hands[MyIndex].Has7(_trump))
                        {
                            var finalTrump = hands[MyIndex].First(i => i.Suit == _trump);
                            var other = hands[MyIndex].First(i => i.Suit != _trump);

                            //vyjimka je jen pokud mam nejvyssi trumfovou i netrumfovou kartu
                            //a zbyva ve hre uz jen jeden dalsi trumf - potom nejdrive áahneme trumf
                            if (Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                    .Count(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > 0 ||
                                                _probabilities.CardProbability(player3, new Card(_trump, h)) > 0) == 1 &&
                                Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                    .Where(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > 0 ||
                                                _probabilities.CardProbability(player3, new Card(_trump, h)) > 0)
                                    .All(h => h < finalTrump.Value) &&
                                _probabilities.SuitHigherThanCardProbability(player2, other, RoundNumber) == 0 &&
                                _probabilities.SuitHigherThanCardProbability(player3, other, RoundNumber) == 0)
                            {
                                return finalTrump;
                            }
                            return other;
                        }
                        if (hands[MyIndex].CardCount(_trump) == 2)
                        {
                            if ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 &&
                                hands[MyIndex].Has7(_trump) &&
                                Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                    .Count(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > 0 ||
                                                _probabilities.CardProbability(player3, new Card(_trump, h)) > 0) == 2)
                            {
                                return ValidCards(hands[MyIndex]).OrderBy(i => i.Value).FirstOrDefault();
                            }
                            if (TeamMateIndex != -1 &&
                                (_gameType & Hra.Sedma) != 0 &&
                                _hands[MyIndex].HasA(_trump) &&
                                _probabilities.CardProbability(TeamMateIndex == player2 ? player3 : player2, new Card(_trump, Hodnota.Desitka)) >= 1 - _epsilon)
                            {
                                return ValidCards(hands[MyIndex]).OrderByDescending(i => i.Value).FirstOrDefault();
                            }
                            if (TeamMateIndex == -1 &&
                                (_gameType & Hra.SedmaProti) != 0 &&
                                _hands[MyIndex].HasA(_trump) &&
                                ((_probabilities.CardProbability(player2, new Card(_trump, Hodnota.Desitka)) >= 1 - _epsilon &&
                                  _probabilities.CardProbability(player2, new Card(_trump, Hodnota.Sedma)) >= 1 - _epsilon) ||
                                 (_probabilities.CardProbability(player3, new Card(_trump, Hodnota.Desitka)) >= 1 - _epsilon &&
                                  _probabilities.CardProbability(player3, new Card(_trump, Hodnota.Sedma)) >= 1 - _epsilon)))
                            {
                                return ValidCards(hands[MyIndex]).OrderByDescending(i => i.Value).FirstOrDefault();
                            }

                            //pokus se uhrat tichou sedmu
                            if (hands[MyIndex].Has7(_trump) && Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                   .Count(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > 0 ||
                                                                               _probabilities.CardProbability(player3, new Card(_trump, h)) > 0) <= 1)
                            {
                                return ValidCards(hands[MyIndex]).OrderByDescending(i => i.Value).FirstOrDefault();
                            }
                            return ValidCards(hands[MyIndex]).OrderBy(i => i.Value).FirstOrDefault();
                        }
                        if (TeamMateIndex == -1)
                        {
                            //: c--
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => ValidCards(i, hands[player2]).All(j =>
                                                                      ValidCards(i, j, hands[player3]).All(k =>
                                                                      {
                                                                          var i2 = hands[MyIndex].First(l => l != i);
                                                                          var j2 = hands[player2].First(m => m != j);
                                                                          var k2 = hands[player3].First(n => n != k);

                                                                          var winnerCard = Round.WinningCard(i, j, k, _trump);

                                                                          if (winnerCard == i)
                                                                          {
                                                                              return Round.WinningCard(i2, j2, k2, _trump) == i2;
                                                                          }
                                                                          else
                                                                          {
                                                                              return false;
                                                                          }
                                                                      })));
                            //pokud to jde tak uhraj 9. i 10. stych
                            if (cardsToPlay.Any())
                            {
                                //zkus zahrat trumfovou sedmu nakonec pokud to jde
                                //toho docilime tak, ze v devatem kole nebudeme hrat trumfovou sedmu aby zustala do posledniho kola
                                return cardsToPlay.OrderBy(i => i.Value)
                                                  .FirstOrDefault(i => i.Suit != _trump || i.Value != Hodnota.Sedma) ??
                                                   cardsToPlay.ToList().RandomOneOrDefault();
                            }
                            //pokud souperi hrajou sedmu proti a ja mam jeste jeden trumf, tak se snaz jim ji zabit
                            if ((_gameType & Hra.SedmaProti) != 0 &&
                                hands[MyIndex].HasSuit(_trump) &&
                                hands[MyIndex].Any(i => i.Suit != _trump))
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump);
                            }
                            //jinak zkus uhrat aspon posledni stych
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => ValidCards(i, hands[player2]).All(j =>

                                                                      ValidCards(i, j, hands[player3]).All(k =>

                                                                      {
                                                                          var i2 = hands[MyIndex].First(l => l != i);
                                                                          var j2 = hands[player2].First(m => m != j);
                                                                          var k2 = hands[player3].First(n => n != k);

                                                                          var winnerCard = Round.WinningCard(i, j, k, _trump);

                                                                          if (winnerCard == j)
                                                                          {
                                                                              return Round.WinningCard(j2, k2, i2, _trump) == i2;
                                                                          }
                                                                          else
                                                                          {
                                                                              return Round.WinningCard(k2, i2, j2, _trump) == i2;
                                                                          }
                                                                      })));
                        }
                        else if (TeamMateIndex == player2)
                        {
                            //: co-
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => ValidCards(i, hands[player2]).All(j =>

                                                                      ValidCards(i, j, hands[player3]).All(k =>

                                                                      {
                                                                          var i2 = hands[MyIndex].First(l => l != i);
                                                                          var j2 = hands[player2].First(m => m != j);
                                                                          var k2 = hands[player3].First(n => n != k);

                                                                          var winnerCard = Round.WinningCard(i, j, k, _trump);

                                                                          if (winnerCard == i)
                                                                          {
                                                                              return Round.WinningCard(i2, j2, k2, _trump) != k2;
                                                                          }
                                                                          else if (winnerCard == j)
                                                                          {
                                                                              return Round.WinningCard(j2, k2, i2, _trump) != k2;
                                                                          }
                                                                          else
                                                                          {
                                                                              return false;
                                                                          }
                                                                      })));
                            //pokud to jde tak uhraj 9. i 10. stych
                            if (cardsToPlay.Any())
                            {
                                //zkus zahrat trumfovou sedmu nakonec pokud to jde
                                //toho docilime tak, ze v devatem kole nebudeme hrat trumfovou sedmu aby zustala do posledniho kola
                                return cardsToPlay.OrderBy(i => i.Value)
                                                  .FirstOrDefault(i => i.Suit != _trump || i.Value != Hodnota.Sedma) ??
                                                   cardsToPlay.ToList().RandomOneOrDefault();
                            }
                            //pokud souper hraje sedmu a ja mam jeste jeden trumf, tak se snaz mu ji zabit
                            if ((_gameType & Hra.Sedma) != 0 &&
                                hands[MyIndex].HasSuit(_trump) &&
                                hands[MyIndex].Any(i => i.Suit != _trump))
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump);
                            }
                            //jinak zkus uhrat aspon posledni stych
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => ValidCards(i, hands[player2]).All(j =>
                                                                      ValidCards(i, j, hands[player3]).All(k =>
                                                                      {
                                                                          var i2 = hands[MyIndex].First(l => l != i);
                                                                          var j2 = hands[player2].First(m => m != j);
                                                                          var k2 = hands[player3].First(n => n != k);

                                                                          var winnerCard = Round.WinningCard(i, j, k, _trump);

                                                                          return Round.WinningCard(k2, i2, j2, _trump) != k2;
                                                                      })));
                        }
                        else
                        {
                            //: c-o
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => ValidCards(i, hands[player2]).All(j =>

                                                                      ValidCards(i, j, hands[player3]).All(k =>

                                                                      {
                                                                          var i2 = hands[MyIndex].First(l => l != i);
                                                                          var j2 = hands[player2].First(m => m != j);
                                                                          var k2 = hands[player3].First(n => n != k);

                                                                          var winnerCard = Round.WinningCard(i, j, k, _trump);

                                                                          if (winnerCard == i)
                                                                          {
                                                                              return Round.WinningCard(i2, j2, k2, _trump) != j2;
                                                                          }
                                                                          else if (winnerCard == j)
                                                                          {
                                                                              return false;
                                                                          }
                                                                          else
                                                                          {
                                                                              return Round.WinningCard(k2, i2, j2, _trump) != j2;
                                                                          }
                                                                      })));
                            //pokud to jde tak uhraj 9. i 10. stych
                            if (cardsToPlay.Any())
                            {
                                //zkus zahrat trumfovou sedmu nakonec pokud to jde
                                //toho docilime tak, ze v devatem kole nebudeme hrat trumfovou sedmu aby zustala do posledniho kola
                                return cardsToPlay.OrderBy(i => i.Value)
                                                  .FirstOrDefault(i => i.Suit != _trump || i.Value != Hodnota.Sedma) ??
                                                   cardsToPlay.ToList().RandomOneOrDefault();
                            }
                            //pokud souper hraje sedmu a ja mam jeste jeden trumf, tak se snaz mu ji zabit
                            if ((_gameType & Hra.Sedma) != 0 &&
                                hands[MyIndex].HasSuit(_trump) &&
                                hands[MyIndex].Any(i => i.Suit != _trump))
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump);
                            }
                            //jinak zkus uhrat aspon posledni stych
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => ValidCards(i, hands[player2]).All(j =>
                                                                      ValidCards(i, j, hands[player3]).All(k =>
                                                                      {
                                                                          var i2 = hands[MyIndex].First(l => l != i);
                                                                          var j2 = hands[player2].First(m => m != j);
                                                                          var k2 = hands[player3].First(n => n != k);

                                                                          var winnerCard = Round.WinningCard(i, j, k, _trump);

                                                                          return Round.WinningCard(j2, k2, i2, _trump) != j2;
                                                                      })));
                        }

                        //zkus zahrat trumfovou sedmu nakonec pokud to jde
                        //toho docilime tak, ze v devatem kole nebudeme hrat trumfovou sedmu aby zustala do posledniho kola
                        return cardsToPlay.OrderBy(i => i.Value)
                                          .FirstOrDefault(i => i.Suit != _trump || i.Value != Hodnota.Sedma) ??
                               cardsToPlay.ToList().RandomOneOrDefault();
                    }
                    #endregion
                };
            }

			yield return new AiRule()
			{
				Order = 1,
				Description = "zkus vytlačit eso",
				SkipSimulations = true,
                #region ChooseCard1 Rule1
                ChooseCard1 = () =>
				{
					if (TeamMateIndex == -1)
					{
						//c--
						var suits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
										.Where(b => b != _trump &&
													hands[MyIndex].HasX(b) &&
                                                    hands[MyIndex].CardCount(b) > 1 &&
													(_probabilities.CardProbability(player2, new Card(b, Hodnota.Eso)) > _epsilon ||
													 _probabilities.CardProbability(player3, new Card(b, Hodnota.Eso)) > _epsilon));
						if (suits.Any())
						{
							var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => suits.Contains(i.Suit) &&
																					i.Value != Hodnota.Desitka &&
																					i.Value >= Hodnota.Spodek &&
																					(Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
																						 .Where(h => h > i.Value)
																						 .Count(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) > _epsilon ||
																									 _probabilities.CardProbability(player3, new Card(i.Suit, h)) > _epsilon) == 1 ||
																					 hands[MyIndex].CardCount(i.Suit) > 2));

							return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
						}
					}
					else if (TeamMateIndex == player3)
					{
						//c-o
                        if (//(_gameType & Hra.Kilo) != 0 &&
                            hands[MyIndex].CardCount(_trump) == 1 &&
                            !hands[MyIndex].HasA(_trump) &&
                            !hands[MyIndex].HasX(_trump))
                        {
                            return null;
                        }
						var suits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
										.Where(b => b != _trump &&
													hands[MyIndex].HasX(b) &&
													hands[MyIndex].CardCount(b) > 1 &&
                                                    hands[MyIndex].CardCount(b) < 5 &&
                                                    _probabilities.CardProbability(player2, new Card(b, Hodnota.Eso)) > _epsilon &&
                                                    (_probabilities.CardProbability(player3, new Card(b, Hodnota.Eso)) == 0 ||
                                                     Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                         .Count(h => _probabilities.CardProbability(player3, new Card(b, h)) > _epsilon) > 3));
						if (suits.Any())
						{
							var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => suits.Contains(i.Suit) &&
																					i.Value != Hodnota.Desitka &&
																					i.Value >= Hodnota.Spodek &&
																					(Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
																						 .Where(h => h > i.Value)
																						 .Count(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) > _epsilon) == 1 ||
																					 hands[MyIndex].CardCount(i.Suit) > 2));

							return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
						}
                        if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Count(b => b != _trump &&
                                            _hands[MyIndex].HasSuit(b) &&
                                            !_bannedSuits.Contains(b)) == 1)
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value != Hodnota.Desitka &&
                                                                                    i.Value >= Hodnota.Spodek &&
                                                                                    hands[MyIndex].HasX(i.Suit) &&
                                                                                    !_bannedSuits.Contains(i.Suit) &&
                                                                                    _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) > _epsilon);

                            return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                        }
                    }
                    else
                    {
                        //co-
                        if (//(_gameType & Hra.Kilo) != 0 &&
                            hands[MyIndex].CardCount(_trump) == 1 &&
                            !hands[MyIndex].HasA(_trump) &&
                            !hands[MyIndex].HasX(_trump))
                        {
                            return null;
                        }
                        //pokud muzes desitku namazat tak pravidlo nehraj
                        if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => _hands[MyIndex].HasX(b) &&
                                          _probabilities.CardProbability(player3, new Card(b, Hodnota.Eso)) > 0) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => !_hands[MyIndex].HasSuit(b) &&
                                          _probabilities.PotentialCards(TeamMateIndex).Any(i => i.Suit == b &&
                                                                                                i.Value >= Hodnota.Svrsek)))
                        {
                            return null;
                        }
                        //zkus vytlacit eso jen pokud mas 2 barvy a tvoje druha barva je trumf
                        //nebo pokud jsou vsechny ostatni barvy zakazane
                        var suits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                        .Where(b => hands[MyIndex].HasSuit(b));
                        if (suits.Count() == 2 &&
                            hands[MyIndex].HasSuit(_trump) &&
                            myInitialHand.CardCount(_trump) >= 5)
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value != Hodnota.Desitka &&
                                                                                    i.Value >= Hodnota.Spodek &&
                                                                                    hands[MyIndex].HasX(i.Suit) &&
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > _epsilon);

                            return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                        }
                        if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Count(b => b != _trump &&
                                            _hands[MyIndex].HasSuit(b) &&
                                            !_bannedSuits.Contains(b)) == 1)
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value != Hodnota.Desitka &&
                                                                                    //i.Value >= Hodnota.Spodek &&
                                                                                    hands[MyIndex].HasX(i.Suit) &&
                                                                                    !_bannedSuits.Contains(i.Suit) &&
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > _epsilon);

                            return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                        }
                    }
					return null;
				}
                #endregion
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "hrát nízkou kartu",
                SkipSimulations = true,
                #region ChooseCard1 Rule2
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

                    if (TeamMateIndex == -1 && RoundNumber <= 5)
                    {
                        if ((_gameType & Hra.Kilo) == 0 &&
                            myInitialHand.CardCount(_trump) > 0 &&
                            myInitialHand.CardCount(_trump) <= 4)
                        {
                            return null;
                        }
                        var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Where(h => h > i.Value)
                                                                     .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                               _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0))
                                                     .ToList();
                        var holesPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .ToDictionary(k => k, v =>
                                                   Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                       .Select(h => new Card(v, h))
                                                       .Where(i => _probabilities.CardProbability(player2, i) > 0 ||
                                                                   _probabilities.CardProbability(player3, i) > 0)
                                                       .OrderBy(i => i.Value)
                                                       .Skip(topCards.CardCount(v))
                                                       .ToList());
                        var lowCards = hands[MyIndex].Where(i => holesPerSuit[i.Suit].Any(j => j.Value > i.Value))
                                                     .ToList();

                        ///pokud hrajes kilo na 4 nejvyssi trumfy a souperi muzou uhrat max 30 bodu (mam apspon 50 bodu v ostrych), tak pravidlo nehraj a nejdriv vytahni trumfy
                        //if ((_gameType & Hra.Kilo) != 0 &&
                        //     myInitialHand.HasA(_trump) &&
                        //     myInitialHand.HasX(_trump) &&
                        //     myInitialHand.HasK(_trump) &&
                        //     myInitialHand.HasQ(_trump) &&
                        //     myInitialHand.CardCount(_trump) == 4 &&
                        //     (myInitialHand.CardCount(Hodnota.Eso) +
                        //      myInitialHand.Count(i => i.Value == Hodnota.Desitka &&
                        //                               myInitialHand.HasA(i.Suit)) >= 5 ||
                        //      Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        //          .Where(b => _hands[MyIndex].HasSuit(b))
                        //          .All(b => topCards.Any(i => i.Suit == b))))
                        //{
                        //    return null;
                        //}

                        if (myInitialHand.CardCount(_trump) >= 4 &&
                            lowCards.Any() &&
                            (topCards.Count >= 4 ||
                             myInitialHand.CardCount(Hodnota.Eso) +
                             myInitialHand.Count(i => i.Value == Hodnota.Desitka &&
                                                      myInitialHand.HasA(i.Suit)) >= 3) &&
                            (lowCards.Count <= 4 ||
                             myInitialHand.CardCount(Hodnota.Eso) +
                             myInitialHand.Count(i => i.Value == Hodnota.Desitka &&
                                                      myInitialHand.HasA(i.Suit)) >= 3))
                        {
                            if ((_gameType & Hra.Kilo) != 0)
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value == Hodnota.Desitka &&
                                                                                    hands[MyIndex].CardCount(i.Suit) == 1 &&
                                                                                    hands[MyIndex].CardCount(_trump) > 0 &&
                                                                                    lowCards.Contains(i))
                                                                        .ToList();
                            }
                            if (!cardsToPlay.Any())
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                i.Value < Hodnota.Desitka &&
                                                                                ((hands[MyIndex].HasX(i.Suit) &&
                                                                                  (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) > 0 ||
                                                                                   _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > 0)) ||
                                                                                 hands[MyIndex].HasA(i.Suit) ||
                                                                                 (!hands[MyIndex].HasA(i.Suit) &&
                                                                                  !hands[MyIndex].HasX(i.Suit) &&
                                                                                  Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                      .Where(b => b != i.Suit &&
                                                                                                  _hands[MyIndex].HasSuit(b))
                                                                                      .All(b => topCards.HasSuit(b)))) &&
                                                                                lowCards.Contains(i) &&
                                                                                !(myInitialHand.HasA(i.Suit) &&
                                                                                  myInitialHand.HasX(i.Suit) &&
                                                                                  //Napr. AX987: AX vytahnou 2 diry, zbyva 1 dira: budu hrat radsi trumf
                                                                                  myInitialHand.CardCount(i.Suit) >= 5 &&
                                                                                  myInitialHand.CardCount(_trump) >= 4))
                                                                    .ToList();
                            }
                            if (!cardsToPlay.Any())
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value == Hodnota.Desitka &&
                                                                                    hands[MyIndex].CardCount(i.Suit) == 1 &&
                                                                                    hands[MyIndex].CardCount(_trump) >= 4 &&
                                                                                    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                        .Where(b => b != i.Suit)
                                                                                        .All(b => topCards.HasSuit(b)) &&
                                                                                    lowCards.Contains(i))
                                                                        .ToList();
                            }
                        }
                        if (!cardsToPlay.Any() &&
                            myInitialHand.CardCount(_trump) >= 4 &&
                            !myInitialHand.HasA(_trump) &&
                            myInitialHand.HasX(_trump) &&
                            lowCards.Any(i => i.Suit != _trump) &&
                            lowCards.Count(i => i.Suit != _trump) <= 2)
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                lowCards.Contains(i))
                                                                    .ToList();
                        }
                    }

                    return cardsToPlay.OrderByDescending(i => cardsToPlay.CardCount(i.Suit))
                                      .ThenBy(i => i.Value)
                                      .FirstOrDefault();
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "vytáhnout trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule3
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

                    if (TeamMateIndex == -1)
                    {
                        //c--
                        if (_probabilities.SuitProbability(player2, _trump, RoundNumber) == 0 &&
                            _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0)
                        {
                            return null;
                        }
                        if (_hands[MyIndex].Any(i => i.Value == Hodnota.Desitka &&
                                                     myInitialHand.CardCount(i.Suit) == 1) &&
                            !((_gameType & Hra.Kilo) != 0 &&
                               _hands[MyIndex].HasA(_trump) &&
                               myInitialHand.HasX(_trump) &&
                               myInitialHand.HasK(_trump) &&
                               myInitialHand.HasQ(_trump) &&
                               myInitialHand.CardCount(_trump) == 4 &&
                               Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                   .Any(b => b != _trump &&
                                             _hands[MyIndex].HasA(b) &&
                                             _probabilities.SuitProbability(player2, b, RoundNumber) > 0 &&
                                             _probabilities.SuitProbability(player3, b, RoundNumber) > 0)))
                        {
                            return null;
                        }
                        var topCards = _hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                      .Where(h => h > i.Value)
                                                                      .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0))
                                                      .ToList();
                        var holes = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().Where(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > _epsilon ||
                                                                                               _probabilities.CardProbability(player3, new Card(_trump, h)) > _epsilon).ToList();
                        var opponentCards = _probabilities.PotentialCards(player2).Concat(_probabilities.PotentialCards(player3)).Distinct().ToList();
                        var topTrumps = hands[MyIndex].Where(i => i.Suit == _trump && holes.All(h => h < i.Value)).ToList();
                        var lowCards = hands[MyIndex].Where(i => i.Suit != _trump &&
                                                                 Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Any(h => i.Value < h &&
                                                                               (_probabilities.CardProbability(player2, new Card(i.Suit, h)) > _epsilon ||
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, h)) > _epsilon)))
                                                     .ToList();
                        //pokud mas 4 nevyssi trumfy a ve vsech barvach nejvyssi karty, tak nejdriv vytahni trumfy ze souperu a potom muzes hrat odshora
                        if ((_gameType & Hra.Kilo) != 0 &&
                            _hands[MyIndex].HasA(_trump) &&
                            myInitialHand.HasX(_trump) &&
                            myInitialHand.HasK(_trump) &&
                            myInitialHand.HasQ(_trump) &&
                            myInitialHand.CardCount(_trump) == 4 &&
                            _hands[MyIndex].CardCount(Hodnota.Eso) +
                            _hands[MyIndex].Count(i => i.Value == Hodnota.Desitka &&
                                                       myInitialHand.HasA(i.Suit)) >= 5)
                            //Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            //    .Where(b => _hands[MyIndex].HasSuit(b))
                            //    .All(b => topCards.Any(i => i.Suit == b)))
                        {
                            return ValidCards(_hands[MyIndex]).Where(i => i.Suit == _trump)
                                                              .OrderByDescending(i => i.Value)
                                                              .FirstOrDefault();
                        }
                        if ((topTrumps.Count >= holes.Count ||
                             (myInitialHand.HasA(_trump) &&
                              myInitialHand.HasX(_trump) &&
                              _hands[MyIndex].HasK(_trump) &&
                              myInitialHand.CardCount(_trump) >= 4 &&
                              holes.Count <= 4 &&
                              _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 &&
                              _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0)) &&
                            ((hands[MyIndex].Where(i => i.Suit != _trump)
                                            .All(i => topCards.Contains(i) ||
                                                      opponentCards.CardCount(i.Suit) <= topCards.CardCount(i.Suit))) ||
                             (hands[MyIndex].Any(i => i.Suit != _trump &&
                                                      i.Value >= Hodnota.Desitka &&
                                                      topCards.Contains(i)) &&
                              hands[MyIndex].Count(i => i.Suit != _trump &&
                                                        !(topCards.Contains(i) ||
                                                          opponentCards.CardCount(i.Suit) <= topCards.CardCount(i.Suit))) == 1 &&
                              hands[MyIndex].CardCount(_trump) > topTrumps.Count)))                        
                        {
                            cardsToPlay = ValidCards(_hands[MyIndex]).Where(i => i.Suit == _trump).ToList();

                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.OrderByDescending(i => i.Value).First();
                            }
                        }
                        //a vsechny cizi trumfy ma souper, tak pravidlo nehraj
                        //hrozi, ze bych na konci nemel dost trumfu
                        if ((_gameType & Hra.SedmaProti) != 0 &&
                            _hands[MyIndex].Has7(_trump) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                         .Where(b => b != _trump)
                                                         .Any(b => myInitialHand.CardCount(b) >= 4 &&
                                                                   _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0))
                        {
                            return null;
                        }
                        if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => _hands[MyIndex].HasSuit(b) &&
                                          !_hands[MyIndex].HasA(b) &&
                                          !_hands[MyIndex].HasX(b)))
                        {
                            return null;
                        }
                        //pokud souperum zbyva posledni trumf a krom trumfu mas na ruce jen same desitky a esa, tak nejdriv vytahni posledni trumf ze soupere
                        if (topTrumps.Any() &&
                            holes.Count() == 1 &&
                            hands[MyIndex].Any(i => i.Value == Hodnota.Eso &&
                                                    i.Suit != _trump) &&
                            hands[MyIndex].Where(i => i.Suit != _trump)
                                          .All(i => i.Value >= Hodnota.Desitka))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump)
                                                                    .OrderByDescending(i => i.Value)
                                                                    .Take(1)
                                                                    .ToList();
                        }
                        else if (holes.Count > 0 &&
                            topTrumps.Count > 0 &&
                            ((((((_gameType & Hra.Sedma) != 0 &&
                                 topTrumps.Count >= holes.Count + 1) ||
                                ((_gameType & Hra.Sedma) == 0 &&
                                 topTrumps.Count >= holes.Count)) ||
                               (((_gameType & Hra.Sedma) != 0 &&
                                  hands[MyIndex].CardCount(_trump) >= 4) ||
                                ((_gameType & Hra.Sedma) == 0 &&
                                 hands[MyIndex].CardCount(_trump) >= 3) &&
                               _probabilities.SuitProbability(player2, _trump, RoundNumber) >= 1 - RiskFactor &&
                               _probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor &&
                               topTrumps.Count + 1 == holes.Count)) &&
                              (//_gameType != (Hra.Hra | Hra.Sedma) ||
                               Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                    .Where(b => b != _trump &&
                                                hands[MyIndex].HasSuit(b))
                                    .All(b => hands[MyIndex].HasA(b) //||
                                              //hands[MyIndex].HasX(b)
                                              ))) ||
                             (_probabilities.SuitProbability(player2, _trump, RoundNumber) >= 1 - RiskFactor &&
                              _probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor &&
                              //pokud ve vsech netrumfovych barvach mam nejvyssi kartu
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => b != _trump &&
                                              hands[MyIndex].HasSuit(b))
                                  .All(b => hands[MyIndex].Any(i => i.Suit == b &&
                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Where(h => h > i.Value)
                                                                        .Any(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) <= _epsilon &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, h)) <= _epsilon)) &&
                             lowCards.Count() < hands[MyIndex].CardCount(_trump)))))
                        {
                            //nehraj trumfove eso, pokud maji souperi desitku ktera neni plonkova. Souperi by si mohli pozdeji zbytecne mazat.
                            if (topTrumps.Any(i => i.Value == Hodnota.Eso) &&
                                (_probabilities.CardProbability(player2, new Card(_trump, Hodnota.Desitka)) > 0 ||
                                 _probabilities.CardProbability(player3, new Card(_trump, Hodnota.Desitka)) > 0) &&
                                _probabilities.HasSolitaryX(player2, _trump, RoundNumber) < SolitaryXThreshold &&
                                _probabilities.HasSolitaryX(player3, _trump, RoundNumber) < SolitaryXThreshold)
                            {
                                return null;
                            }
                            cardsToPlay = topTrumps;
                        }

                        return cardsToPlay.RandomOneOrDefault();
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //co-
                        if ((_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].Has7(_trump)&&
                            hands[MyIndex].CardCount(_trump) == 2)
                        {
                            return null;
                        }
                        if (_probabilities.SuitProbability(player3, _trump, RoundNumber) == 0)
                        {
                            return null;
                        }
                        //pouzivam 0 misto epsilon protoze jinak bych mohl hrat trumfovou x a myslet si ze souper nema eso a on by ho zrovna mel!
                        var holes = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().Where(h => _probabilities.CardProbability(player3, new Card(_trump, h)) > _epsilon).ToList();
                        var topTrumps = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump && holes.All(h => h < i.Value)).ToList();
                        var lowCards = hands[MyIndex].Where(i => i.Suit != _trump &&
                                                                 Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Any(h => h > i.Value &&
                                                                               _probabilities.CardProbability(player3, new Card(i.Suit, h)) > _epsilon)).ToList();

                        if (lowCards.Any() &&
                            _hands[MyIndex].Where(i => i.Suit != _trump)
                                           .All(i => i.Value < Hodnota.Desitka) &&
                                                     _rounds != null && //pokud v minulych kolech kolega na tvuj nejvyssi trumf nenamazal, tak pravidlo nehraj (kolega nema co mazat). Trumfy setri na pozdeji
                                                     _rounds.Any(r => r?.c3 != null &&
                                                                      r.player1.PlayerIndex == MyIndex &&
                                                                      r.c1.Suit == _trump &&
                                                                      r.roundWinner.PlayerIndex == MyIndex &&
                                                                      ((r.player2.PlayerIndex == TeamMateIndex &&
                                                                        r.c2.Suit != _trump &&
                                                                        r.c2.Value < Hodnota.Desitka) ||
                                                                       (r.player3.PlayerIndex == TeamMateIndex &&
                                                                        r.c3.Suit != _trump &&
                                                                        r.c3.Value < Hodnota.Desitka))))
                        {
                            return null;
                        }
                        //pokud se hraje sedma proti a mam dlouhou netrumfovou barvu
                        //a vsechny cizi trumfy ma souper, tak pravidlo nehraj
                        //hrozi, ze bych na konci nemel dost trumfu
                        if ((_gameType & Hra.SedmaProti) != 0 &&
                            _hands[MyIndex].Has7(_trump) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => myInitialHand.CardCount(b) >= 4 &&
                                          _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0))
                        {
                            return null;
                        }
                        if (holes.Count > 0 &&
                            topTrumps.Count > 0 &&
                            (topTrumps.Count >= holes.Count ||
                             (hands[MyIndex].CardCount(_trump) >= holes.Count &&
                              _probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor &&
                              //pokud ve vsech netrumfovych barvach mam nejvyssi kartu
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => b != _trump &&
                                              hands[MyIndex].HasSuit(b))
                                  .All(b => hands[MyIndex].Any(i => i.Suit == b &&
                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Where(h => h > i.Value)
                                                                        .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) <= _epsilon &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, h)) <= _epsilon)) &&
                             lowCards.Count() < hands[MyIndex].CardCount(_trump)))))
                        {
                            cardsToPlay = topTrumps;
                        }

                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }
                    else
                    {
                        //c-o
                        if ((_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            hands[MyIndex].CardCount(_trump) == 2)
                        {
                            return null;
                        }
                        if (_probabilities.SuitProbability(player2, _trump, RoundNumber) == 0)
                        {
                            return null;
                        }

                        //pouzivam 0 misto epsilon protoze jinak bych mohl hrat trumfovou x a myslet si ze souper nema eso a on by ho zrovna mel!
                        var holes = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().Where(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > _epsilon).ToList();
                        var topTrumps = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump && holes.All(h => h < i.Value)).ToList();
                        var lowCards = hands[MyIndex].Where(i => i.Suit != _trump &&
                                                                 Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Any(h => h > i.Value &&
                                                                               _probabilities.CardProbability(player2, new Card(i.Suit, h)) > _epsilon)).ToList();

                        if (lowCards.Any() &&
                            _hands[MyIndex].Where(i => i.Suit != _trump)
                                           .All(i => i.Value < Hodnota.Desitka) &&
                                                     _rounds != null && //pokud v minulych kolech kolega na tvuj nejvyssi trumf nenamazal, tak pravidlo nehraj (kolega nema co mazat). Trumfy setri na pozdeji
                                                     _rounds.Any(r => r?.c3 != null &&
                                                                      r.player1.PlayerIndex == MyIndex &&
                                                                      r.c1.Suit == _trump &&
                                                                      r.roundWinner.PlayerIndex == MyIndex &&
                                                                      ((r.player2.PlayerIndex == TeamMateIndex &&
                                                                        r.c2.Suit != _trump &&
                                                                        r.c2.Value < Hodnota.Desitka) ||
                                                                       (r.player3.PlayerIndex == TeamMateIndex &&
                                                                        r.c3.Suit != _trump &&
                                                                        r.c3.Value < Hodnota.Desitka))))
                        {
                            return null;
                        }
                        //pokud se hraje sedma proti a mam dlouhou netrumfovou barvu
                        //a vsechny cizi trumfy ma souper, tak pravidlo nehraj
                        //hrozi, ze bych na konci nemel dost trumfu
                        if ((_gameType & Hra.SedmaProti) != 0 &&
                            _hands[MyIndex].Has7(_trump) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => myInitialHand.CardCount(b) >= 4 &&
                                          _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0))
                        {
                            return null;
                        }
                        if (holes.Count > 0 &&
                            topTrumps.Count > 0 &&
                            (topTrumps.Count >= holes.Count ||
                             (hands[MyIndex].CardCount(_trump) >= holes.Count &&
                              _probabilities.SuitProbability(player2, _trump, RoundNumber) >= 1 - RiskFactor &&
                              //pokud ve vsech netrumfovych barvach mam nejvyssi kartu
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => b != _trump &&
                                              hands[MyIndex].HasSuit(b))
                                  .All(b => hands[MyIndex].Any(i => i.Suit == b &&
                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Where(h => h > i.Value)
                                                                        .Any(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) <= _epsilon &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, h)) <= _epsilon)) &&
                             lowCards.Count() < hands[MyIndex].CardCount(_trump)))))
                        {
                            cardsToPlay = topTrumps;
                        }

                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 4,
                Description = "vytlačit trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule4
                ChooseCard1 = () =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    //proti kilu zkus vytlacit netrumfovou hlasku pokud se hraje kilo na prvni hlas
                    var opponent = TeamMateIndex == player2 ? player3 : player2;

                    if (TeamMateIndex != -1 &&
                        (_gameType & Hra.Kilo) != 0 &&
                        !cardsToPlay.Any() &&
                        _hlasConsidered == HlasConsidered.First &&
                        _rounds != null &&
                        _rounds.All(r => r == null ||
                                         !((r.hlas1 && r.player1.PlayerIndex == opponent && r.c1.Suit == _trump) ||
                                           (r.hlas2 && r.player2.PlayerIndex == opponent && r.c2.Suit == _trump) ||
                                           (r.hlas3 && r.player3.PlayerIndex == opponent && r.c3.Suit == _trump))) &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump)
                            .Any(b => _hands[MyIndex].Any(i => i.Suit == b &&
                                                               i.Value < Hodnota.Svrsek &&
                                                               !_probabilities.PotentialCards(TeamMateIndex).HasSuit(b) &&
                                                               _probabilities.PotentialCards(opponent).HasK(b) &&
                                                               _probabilities.PotentialCards(opponent).HasQ(b) &&
                                                               _probabilities.PotentialCards(opponent)
                                                                             .Where(j => j.Suit == b &&
                                                                                         j.Value > i.Value)
                                                                             .All(j => j.Value == Hodnota.Kral ||
                                                                                       j.Value == Hodnota.Svrsek))))
                    {
                        return null;
                    }
                    if (TeamMateIndex == -1)
                    {
                        //: c--
                        if (_gameType == (Hra.Hra | Hra.Sedma) &&
                            hands[MyIndex].CardCount(_trump) == 2)
                        {
                            //pokud hraju sedmu a mam posl. dva trumfy setri trumfy nakonec
                            return null;
                        }
                        var opponentTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                 .Count(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > 0 ||
                                                             _probabilities.CardProbability(player3, new Card(_trump, h)) > 0);
                        if ((RoundNumber == 8 ||
                             RoundNumber == 9) &&
                            (_gameType & Hra.Sedma) == 0 &&
                            hands[MyIndex].CardCount(_trump) == 2 &&
                            hands[MyIndex].Has7(_trump) &&
                            opponentTrumps >= 2)
                        {
                            //v osmem kole nebo devatem pokud nehraju sedmu a mam posl. dva trumfy a souperi maji taky aspon dva
                            //tak hraj trumfovou sedmu aby nebyla ticha sedma zabita
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                               i.Value == Hodnota.Sedma);
                        }
                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                    i.Value != Hodnota.Eso &&
                                                                    i.Suit != _trump &&
                                                                    !_bannedSuits.Contains(i.Suit) &&
                                                                    (((_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0f ||
                                                                       _preferredSuits.Contains(i.Suit)) &&
                                                                      _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0f) ||
                                                                     ((_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0f ||
                                                                       _preferredSuits.Contains(i.Suit)) &&
                                                                      _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0f)));
                        }
                        //zkus vytlacit trumf trumfem pokud jich mam dost a v ruce mam jen trumfy A nebo X
                        var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Where(h => h > i.Value)
                                                                     .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                               _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0))
                                                     .ToList();

                        if (!cardsToPlay.Any() && 
                            (hands[MyIndex].All(i => i.Value == Hodnota.Eso ||
                                                     i.Value == Hodnota.Desitka ||
                                                     i.Suit == _trump) ||
                             (hands[MyIndex].All(i => i.Suit == _trump ||
                                                      topCards.Contains(i)) &&
                              !topCards.HasSuit(_trump))) &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps && 
                            opponentTrumps > 0)
                        {
                            //pokud ale mas plonkovou X, tak pravidlo nehraj (nejprv se zbav plonkove X)
                            if (hands[MyIndex].Any(i => i.Suit != _trump &&
                                                        i.Value == Hodnota.Desitka &&
                                                        hands[MyIndex].CardCount(i.Suit) == 1 &&
                                                        (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) > 0 ||
                                                         _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > 0)))
                            {
                                return null;
                            }
                            //nehraj pokud souperi maji 2 trumfy a ja ma, trumfove eso
                            if (!(opponentTrumps == 2 &&
                                  hands[MyIndex].HasA(_trump) &&
                                  hands[MyIndex].CardCount(_trump) > 1))
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                    i.Suit == _trump);
                            }
                        }
                        var longSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Where(b => b != _trump &&
                                                        hands[MyIndex].CardCount(b) >= 4)
                                            .ToList();
                        //zkus vytlacit trumf trumfem pokud jich mam dost a
                        //v dlouhych netrumfovych barvach mam vzdycky taky eso
                        //(cili hrozi, ze kdyz s takovym esem vyjedu, tak o nej prijdu)
                        //if (!cardsToPlay.Any() &&
                        //    opponentTrumps > 0 &&
                        //    (hands[MyIndex].CardCount(_trump) > opponentTrumps ||
                        //     (hands[MyIndex].CardCount(_trump) == opponentTrumps &&
                        //      _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 &&
                        //      _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0)) &&
                        //    longSuits.Any() &&
                        //    longSuits.All(b => hands[MyIndex].HasA(b)))
                        //{
                        //    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                        //                                                        i.Suit == _trump);
                        //}

                        var lowcards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Any(h => i.Value < h &&
                                                                               (_probabilities.CardProbability(player2, new Card(i.Suit, h)) > _epsilon ||
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, h)) > _epsilon))).ToList();
                        if (!cardsToPlay.Any() &&
                            lowcards.Any(i => i.Suit != _trump &&
                                              i.Value < Hodnota.Desitka &&
                                              ((_probabilities.SuitProbability(player2, i.Suit, RoundNumber) < 1 &&
                                                _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0) ||
                                               (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) < 1 &&
                                                _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0))))
                        {
                            return null; //pokud mas nizke netrumfove barvy, tak si trumfy setri
                        }
                        if (!cardsToPlay.Any() &&
                            opponentTrumps > 0 &&
                            (((_gameType & Hra.Sedma) != 0 &&
                              hands[MyIndex].CardCount(_trump) > opponentTrumps + 1) ||
                             ((_gameType & Hra.Sedma) == 0 &&
                              hands[MyIndex].CardCount(_trump) > opponentTrumps)) &&
                            lowcards.HasSuit(_trump) &&
                            hands[MyIndex].All(i => i.Suit == _trump ||
                                                    i.Value >= Hodnota.Desitka ||
                                                    (lowcards.Contains(i) &&
                                                     hands[MyIndex].HasA(i.Suit))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                i.Value < Hodnota.Desitka).ToList();
                        }
                        //if (!cardsToPlay.Any() && 
                        //    opponentTrumps  == 1 &&
                        //    //hands[MyIndex].CardCount(_trump) >= 3 &&
                        //    lowcards.Count() < hands[MyIndex].CardCount(_trump) &&
                        //    hands[MyIndex].Any(i => i.Suit != _trump &&
                        //                            (i.Value == Hodnota.Eso ||
                        //                             i.Value == Hodnota.Desitka)))
                        //{
                        //    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                        //                                                        i.Suit == _trump);
                        //}

                        //zkus vytlacit trumf trumfem pokud jich mam dost a je velka sance, 
                        //ze na mou plivu (kterou bych zahral v nasl. pravidlech) jeden souper namaze a druhej ji prebije
                        //!! Tohle je asi spatne, protihrac totiz muze namazat A,X i na muj trumf pokud sam trumfy nezna
                        //if(!cardsToPlay.Any() && 
                        //   //opponentTrumps > 0 && 
                        //   _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 &&
                        //   _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0 &&
                        //   ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 ||
                        //    (_gameType & (Hra.Kilo | Hra.KiloProti)) != 0))
                        //{
                        ////jeden protivnik nezna nejakou barvu, ale ma A nebo X v jine netrumfove barve a nezna trumfy
                        ////a druhy protivnik ma bud vyssi barvu nebo trumf, cili jeden muze druhemu namazat
                        //if (ValidCards(hands[MyIndex]).Any(i => (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) < RiskFactor &&
                        //                                         _probabilities.SuitProbability(player2, _trump, RoundNumber) < RiskFactor &&
                        //                                         (_probabilities.SuitHigherThanCardProbability(player3, i, RoundNumber) >= 1 - RiskFactor ||
                        //                                          _probabilities.SuitProbability(player3, i.Suit, RoundNumber) < RiskFactor &&
                        //                                          _probabilities.SuitProbability(player3, _trump, RoundNumber) >=  1 - RiskFactor) &&
                        //                                         Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        //                                             .Where(b => b != _trump)
                        //                                             .Any(b => _probabilities.CardProbability(player2, new Card(b, Hodnota.Eso)) > _epsilon ||
                        //                                                       _probabilities.CardProbability(player2, new Card(b, Hodnota.Desitka)) > _epsilon)) ||    //nebo naopak
                        //                                        (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) < RiskFactor &&
                        //                                         _probabilities.SuitProbability(player3, _trump, RoundNumber) < RiskFactor &&
                        //                                         (_probabilities.SuitHigherThanCardProbability(player2, i, RoundNumber) >= 1 - RiskFactor ||
                        //                                          _probabilities.SuitProbability(player2, i.Suit, RoundNumber) < RiskFactor &&
                        //                                          _probabilities.SuitProbability(player2, _trump, RoundNumber) >= 1 - RiskFactor) &&
                        //                                         Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        //                                             .Where(b => b != _trump)
                        //                                             .Any(b => _probabilities.CardProbability(player3, new Card(b, Hodnota.Eso)) > _epsilon ||
                        //                                                       _probabilities.CardProbability(player3, new Card(b, Hodnota.Desitka)) > _epsilon))))
                        //{
                        //    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                        //                                                        i.Suit == _trump);
                        //}
                        //}
                        if ((_gameType & Hra.Kilo) != 0)    //nehraj barvy na ktere si souperi urcite namazou
                        {
                            cardsToPlay = cardsToPlay.Where(i => !((_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0 &&
                                                                    _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 &&
                                                                    _probabilities.PotentialCards(player3).Any(j => j.Value >= Hodnota.Desitka) ||
                                                                   (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                                    _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0 &&
                                                                    _probabilities.PotentialCards(player2).Any(j => j.Value >= Hodnota.Desitka)))));
                        }
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //: co-
                        if (RoundNumber == 8 &&
                            (_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            hands[MyIndex].CardCount(_trump) == 2)
                        {
                            //v osmem kole pokud hraju sedmu proti a mam posl. dva trumfy setri trumfy nakonec
                            return null;
                        }
                        if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Any(b => _hands[MyIndex].HasA(b) &&
                                          _probabilities.HasSolitaryX(player3, b, RoundNumber) >= 1 - _epsilon))
                        {
                            //pokud muzes vytahnout plonkouvouo desitku z aktera, tak pravidlo nehraj
                            return null;
                        }
                        //zkus vytlacit trumf trumfem pokud jich mam dost a v ruce mam jen trumfy A nebo X
                        var opponentTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                 .Count(h => _probabilities.CardProbability(player3, new Card(_trump, h)) > 0);
                        if ((_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].CardCount(_trump) == opponentTrumps &&
                            hands[MyIndex].Any(i => i.Value >= Hodnota.Desitka &&
                                                      i.Suit != _trump &&
                                                      (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                       _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0)))
                        {
                            return null;
                        }
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                i.Value != Hodnota.Eso &&
                                                                i.Suit != _trump &&
                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                (!hands[MyIndex].HasA(i.Suit) ||
                                                                 myInitialHand.CardCount(_trump) <= 2) &&
                                                                (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0f ||
                                                                 _preferredSuits.Contains(i.Suit)) &&
                                                                _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0f &&
                                                                _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0f &&
                                                                _probabilities.SuitHigherThanCardExceptAXProbability(player2, i, RoundNumber) > 1 - RiskFactor &&
                                                                _probabilities.PotentialCards(player2)                      //musi byt sance, ze spoluhrac ma
                                                                              .Count(j => j.Value > i.Value &&              //v barve i neco jineho nez A nebo X
                                                                                          j.Value < Hodnota.Desitka) > 1);

                        //zkus vytlacit trumf svou nebo spoluhracovou desitkou nebo esem pokud hraje souper sedmu nebo pokud anebo x nemuze uhrat
                        if (!cardsToPlay.Any())// && _gameType == (Hra.Hra | Hra.Sedma))// && _rounds[0] != null)
						{
                            var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                         .Where(h => h > i.Value)
                                                                         .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                                   _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0))
                                                         .ToList();

                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                    i.Value != Hodnota.Eso &&
                                                                    i.Suit != _trump &&
                                                                    !_bannedSuits.Contains(i.Suit) &&
                                                                    (!hands[MyIndex].HasA(i.Suit) ||
                                                                     myInitialHand.CardCount(_trump) <= 2) &&
                                                                    _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) > 1 - RiskFactor &&
                                                                    (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0f ||
                                                                     _preferredSuits.Contains(i.Suit)) &&
                                                                    _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0f &&
                                                                    (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0f ||
                                                                     _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0f) &&
                                                                    (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0 &&
                                                                    ((_gameType == (Hra.Hra | Hra.Sedma) &&
                                                                      SevenValue > GameValue) ||
                                                                     (_probabilities.LikelyCards(player2).Any(j => j.Suit == i.Suit) &&
                                                                      _probabilities.LikelyCards(player2).Where(j => j.Suit == i.Suit)
                                                                                                         .Any(j => j.Value > i.Value &&
                                                                                                                   j.Value < Hodnota.Desitka)) ||
                                                                     !Enum.GetValues(typeof(Barva)).Cast<Barva>()   //pokud neni barva na kterou by kolega mohl namazat
                                                                          .Where(b => b != _trump)
                                                                          .Any(b => _probabilities.SuitProbability(player3, b, RoundNumber) == 1 &&
                                                                                    (b == i.Suit ||
                                                                                     (topCards.HasSuit(b) &&
                                                                                      _probabilities.SuitProbability(player3, b, RoundNumber) > 0 &&
                                                                                      _probabilities.SuitProbability(player2, b, RoundNumber) == 0)))));
                            if (!cardsToPlay.Any())
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    !_bannedSuits.Contains(i.Suit) &&
                                                                                    (!hands[MyIndex].HasA(i.Suit) ||
                                                                                     myInitialHand.CardCount(_trump) <= 2) &&
                                                                                    (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0f ||
                                                                                     _preferredSuits.Contains(i.Suit)) &&
                                                                                    _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0f &&
                                                                                    (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0f ||
                                                                                     _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0f) &&
                                                                                    (_gameType & Hra.Kilo) == 0 &&
                                                                                    ((_gameType == (Hra.Hra | Hra.Sedma) &&
                                                                                      SevenValue > GameValue) ||
                                                                                     _probabilities.PotentialCards(player2).Where(j => j.Suit == i.Suit)
                                                                                                                           .Any(j => j.Value > i.Value &&
                                                                                                                                     j.Value < Hodnota.Desitka) ||
                                                                                     !Enum.GetValues(typeof(Barva)).Cast<Barva>()   //pokud neni barva na kterou by kolega mohl namazat
                                                                                          .Where(b => b != _trump)
                                                                                          .Any(b => _probabilities.SuitProbability(player3, b, RoundNumber) == 1 &&
                                                                                                    (b == i.Suit ||
                                                                                                     (topCards.HasSuit(b) &&
                                                                                                      _probabilities.SuitProbability(player3, b, RoundNumber) > 0 &&
                                                                                                      _probabilities.SuitProbability(player2, b, RoundNumber) == 0)))
                                                                                          ));
                                if (cardsToPlay.Any() &&
                                    cardsToPlay.All(i => i.Value >= Hodnota.Desitka) &&
                                    _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0f)
                                {
                                    var temp = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                     i.Value != Hodnota.Eso &&
                                                                                     i.Suit != _trump &&
                                                                                     !_bannedSuits.Contains(i.Suit) &&
                                                                                     (!hands[MyIndex].HasA(i.Suit) ||
                                                                                      myInitialHand.CardCount(_trump) <= 2) &&
                                                                                     (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0f ||
                                                                                      _preferredSuits.Contains(i.Suit)) &&
                                                                                     (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0 &&
                                                                                     ((_gameType == (Hra.Hra | Hra.Sedma) &&
                                                                                       SevenValue > GameValue) ||
                                                                                      (_probabilities.LikelyCards(player2).Any(j => j.Suit == i.Suit) &&
                                                                                       _probabilities.LikelyCards(player2).Where(j => j.Suit == i.Suit)
                                                                                                                          .Any(j => j.Value > i.Value &&
                                                                                                                                    j.Value < Hodnota.Desitka)) ||
                                                                                      !Enum.GetValues(typeof(Barva)).Cast<Barva>()   //pokud neni barva na kterou by kolega mohl namazat
                                                                                           .Where(b => b != _trump)
                                                                                           .Any(b => _probabilities.SuitProbability(player3, b, RoundNumber) == 1 &&
                                                                                                     (b == i.Suit ||
                                                                                                      (topCards.HasSuit(b) &&
                                                                                                       _probabilities.SuitProbability(player3, b, RoundNumber) > 0 &&
                                                                                                       _probabilities.SuitProbability(player2, b, RoundNumber) == 0)))));
                                    if (temp.Any())
                                    {
                                        cardsToPlay = temp;
                                    }
                                }
                            }
						}
                        //zkus vytlacit trumf trumfem pokud jich mam dost a v ruce mam jen trumfy A nebo X
						if (!cardsToPlay.Any() &&
							hands[MyIndex].All(i => i.Value == Hodnota.Eso ||
													i.Value == Hodnota.Desitka ||
													i.Suit == _trump) &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps &&
                            opponentTrumps > 0 &&
                            !((_gameType & Hra.SedmaProti) != 0 &&
                              hands[MyIndex].Has7(_trump) &&
                              hands[MyIndex].CardCount(_trump) == 2 &&
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Any(b => !hands[MyIndex].HasSuit(b) &&
                                            _probabilities.SuitProbability(player3, b, RoundNumber) > 0)))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Suit == _trump);
						}
                        //zkus vytlacit trumf trumfem pokud jic mam dost a hranim cehokoli jineho vytlacim ze spoluhrace A nebo X
                        if (!cardsToPlay.Any() &&
                            opponentTrumps > 0 &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps &&
                            hands[MyIndex].Where(i => i.Suit != _trump)
                                          .All(i => _probabilities.SuitHigherThanCardExceptAXProbability(player2, i, RoundNumber) < 1 - RiskFactor &&
                                                    (_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > _epsilon ||
                                                     (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                      _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0))))
                                                    //_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                    //_probabilities.HasAOrXAndNothingElse(player2, i.Suit, RoundNumber) > 0.5))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Suit == _trump);
                        }

                        if (!cardsToPlay.Any() &&
                            (_gameType & Hra.SedmaProti) != 0 &&
                            myInitialHand.CardCount(_trump) >= 5 &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps)
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Suit == _trump &&
                                                                                (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) > 0 ||
                                                                                 _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > 0));
                        }
                        if (!cardsToPlay.Any() &&
                            (_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].CardCount(_trump) == opponentTrumps &&
                            hands[MyIndex].Any(i => i.Value >= Hodnota.Desitka &&
                                                      i.Suit != _trump &&
                                                      (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                       _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0)))
                        {
                            return null;
                            //cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value >= Hodnota.Desitka &&
                            //                                                    i.Suit != _trump &&
                            //                                                    (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                            //                                                     _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0));
                        }
                        //zkus vytlacit trumf trumfem pokud si trumfy odmazu a pozdeji budu moct mazat A nebo X
                        //presunuto do "odmazat si barvu"
                        //if ((_gameType & Hra.SedmaProti) == 0 &&
                        //    hands[MyIndex].CardCount(_trump) > 0 &&
                        //    hands[MyIndex].CardCount(_trump) <= 1 &&
                        //    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        //        .Any(b => !hands[MyIndex].HasSuit(b) &&
                        //                  (_probabilities.SuitProbability(player2, b, RoundNumber) > 0 ||
                        //                   _probabilities.SuitProbability(player3, b, RoundNumber) > 0)) &&
                        //    hands[MyIndex].Any(i => (i.Value == Hodnota.Eso ||
                        //                             i.Value == Hodnota.Desitka) &&
                        //                            i.Suit != _trump &&
                        //                            _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > RiskFactor))
                        //{
                        //    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                        //                                                   (i.Value != Hodnota.Desitka ||
                        //                                                    _probabilities.CardProbability(player3, new Card(_trump, Hodnota.Eso)) == 0));
                        //}
                        if (cardsToPlay.Any(i => _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0))
                        {
                            cardsToPlay = cardsToPlay.Where(i => _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0);
                        }
                    }
                    else
                    {
                        //: c-o
                        if (RoundNumber == 8 &&
                            (_gameType & Hra.SedmaProti) != 0 &&
                             hands[MyIndex].Has7(_trump) &&
                             hands[MyIndex].CardCount(_trump) == 2)
                        {
                            //v osmem kole pokud hraju sedmu proti a mam posl. dva trumfy setri trumfy nakonec
                            return null;
                        }
                        if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Any(b => _hands[MyIndex].HasA(b) &&
                                            _probabilities.HasSolitaryX(player2, b, RoundNumber) >= 1 - _epsilon))
                        {
                            //pokud muzes vytahnout plonkouvouo desitku z aktera, tak pravidlo nehraj
                            return null;
                        }
                        //zkus vytlacit trumf trumfem pokud jich mam dost a v ruce mam jen trumfy A nebo X
                        var opponentTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                 .Count(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > 0);
                        if ((_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].CardCount(_trump) == opponentTrumps &&
                            hands[MyIndex].Any(i => i.Value >= Hodnota.Desitka &&
                                                      i.Suit != _trump &&
                                                      (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                       _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0)))
                        {
                            return null;
                        }
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                i.Value != Hodnota.Eso &&
                                                                i.Suit != _trump &&
                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0f ||
                                                                 _preferredSuits.Contains(i.Suit)) &&
                                                                _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0f &&
                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0f &&
                                                                _probabilities.NoSuitOrSuitLowerThanXProbability(player3, i.Suit, RoundNumber) > 1 - RiskFactor &&
                                                                _probabilities.PotentialCards(player3)                      //musi byt sance, ze spoluhrac ma
                                                                              .Where(j => j.Suit == i.Suit)
                                                                              .Count(j => j.Value > i.Value &&              //v barve i neco jineho nez A nebo X
                                                                                          j.Value < Hodnota.Desitka) > 1);
                        //zkus vytlacit trumf svou nebo spoluhracovou desitkou nebo esem pokud hraje souper sedmu nebo pokud desitku ci eso nemuze uhrat
                        if (!cardsToPlay.Any())// && _gameType == (Hra.Hra | Hra.Sedma))// && _rounds[0] != null)
						{
                            var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                         .Where(h => h > i.Value)
                                                                         .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                                   _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0))
                                                         .ToList();

                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Value != Hodnota.Eso &&
                                                                                i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0f ||
                                                                                 _preferredSuits.Contains(i.Suit)) &&
                                                                                _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0f &&
                                                                                (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0f ||
                                                                                 _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0f) &&
                                                                                (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0 &&
                                                                                ((_gameType == (Hra.Hra | Hra.Sedma) &&
                                                                                  SevenValue > GameValue) ||
                                                                                 (_probabilities.LikelyCards(player3).Any(j => j.Suit == i.Suit) &&
                                                                                  _probabilities.LikelyCards(player3).Where(j => j.Suit == i.Suit)
                                                                                                                     .Any(j => j.Value > i.Value &&
                                                                                                                               j.Value < Hodnota.Desitka)) ||
                                                                                 !Enum.GetValues(typeof(Barva)).Cast<Barva>()   //pokud neni barva na kterou by kolega mohl namazat
                                                                                      .Where(b => b != _trump)
                                                                                      .Any(b => _probabilities.SuitProbability(player2, b, RoundNumber) == 1 &&
                                                                                                (b == i.Suit ||
                                                                                                 (topCards.HasSuit(b) &&
                                                                                                  _probabilities.SuitProbability(player2, b, RoundNumber) > 0 &&
                                                                                                  _probabilities.SuitProbability(player3, b, RoundNumber) == 0)))));
                            if (!cardsToPlay.Any())
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                    i.Value != Hodnota.Eso &&
                                                                                    i.Suit != _trump &&
                                                                                    !_bannedSuits.Contains(i.Suit) &&
                                                                                    (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0f ||
                                                                                     _preferredSuits.Contains(i.Suit)) &&
                                                                                    _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0f &&
                                                                                    (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0f ||
                                                                                     _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0f) &&
                                                                                    (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0 &&
                                                                                    ((_gameType == (Hra.Hra | Hra.Sedma) &&
                                                                                      SevenValue > GameValue) ||
                                                                                     (_probabilities.LikelyCards(player3).Any(j => j.Suit == i.Suit) &&
                                                                                      _probabilities.LikelyCards(player3).Where(j => j.Suit == i.Suit)
                                                                                                                         .Any(j => j.Value > i.Value &&
                                                                                                                                   j.Value < Hodnota.Desitka)) ||
                                                                                     !Enum.GetValues(typeof(Barva)).Cast<Barva>()   //pokud neni barva na kterou by kolega mohl namazat
                                                                                          .Where(b => b != _trump)
                                                                                          .Any(b => _probabilities.SuitProbability(player2, b, RoundNumber) == 1 &&
                                                                                                    (b == i.Suit ||
                                                                                                     (topCards.HasSuit(b) &&
                                                                                                      _probabilities.SuitProbability(player2, b, RoundNumber) > 0 &&
                                                                                                      _probabilities.SuitProbability(player3, b, RoundNumber) == 0)))));
                                if (cardsToPlay.Any() &&
                                    cardsToPlay.All(i => i.Value >= Hodnota.Desitka) &&
                                    _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0f)
                                {
                                    var temp = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                     i.Value != Hodnota.Eso &&
                                                                                     i.Suit != _trump &&
                                                                                     !_bannedSuits.Contains(i.Suit) &&
                                                                                     (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0f ||
                                                                                      _preferredSuits.Contains(i.Suit)) &&
                                                                                     (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0 &&
                                                                                     ((_gameType == (Hra.Hra | Hra.Sedma) &&
                                                                                       SevenValue > GameValue) ||
                                                                                      _probabilities.LikelyCards(player3).Where(j => j.Suit == i.Suit)
                                                                                                                         .Any(j => j.Value > i.Value &&
                                                                                                                                   j.Value < Hodnota.Desitka) ||
                                                                                      !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                           .Where(b => b != _trump)
                                                                                           .Any(b => _probabilities.SuitProbability(player2, b, RoundNumber) == 1 &&
                                                                                                     (b == i.Suit ||
                                                                                                      (topCards.HasSuit(b) &&
                                                                                                       _probabilities.SuitProbability(player2, b, RoundNumber) > 0 &&
                                                                                                       _probabilities.SuitProbability(player3, b, RoundNumber) == 0)))));
                                    if (temp.Any())
                                    {
                                        cardsToPlay = temp;
                                    }
                                }
                            }
						}
						if (!cardsToPlay.Any() &&
							hands[MyIndex].All(i => i.Value == Hodnota.Eso ||
													i.Value == Hodnota.Desitka ||
													i.Suit == _trump) &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps && 
                            opponentTrumps > 0 &&
                            !((_gameType & Hra.SedmaProti) != 0 &&
                              hands[MyIndex].Has7(_trump) &&
                              hands[MyIndex].CardCount(_trump) == 2 &&
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Any(b => !hands[MyIndex].HasSuit(b) &&
                                            _probabilities.SuitProbability(player2, b, RoundNumber) > 0)))
						{
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Suit == _trump);
						}
                        //zkus vytlacit trumf trumfem pokud jic mam dost a hranim cehokoli jineho vytlacim ze spoluhrace A nebo X
                        if (!cardsToPlay.Any() &&
                            opponentTrumps > 0 &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps &&
                            hands[MyIndex].Where(i => i.Suit != _trump)
                                          .All(i => _probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0 &&
                                                    _probabilities.HasAOrXAndNothingElse(player3, i.Suit, RoundNumber) > 0.5))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Suit == _trump);
                        }

                        if (!cardsToPlay.Any() &&
                            (_gameType & Hra.SedmaProti) != 0 &&
                            myInitialHand.CardCount(_trump) >= 5 &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps)
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Suit == _trump &&
                                                                                (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) > 0 ||
                                                                                 _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > 0));
                        }

                        if (cardsToPlay.Any(i => _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0))
                        {
                            cardsToPlay = cardsToPlay.Where(i => _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0);
                        }
                    }

                    if (TeamMateIndex == player2)
                    {
                        return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                    }

                    return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 5,
                Description = "zkusit vytáhnout plonkovou X",
                SkipSimulations = true,
                #region ChooseCard1 Rule5
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();
                    //pokud mam na zacatku 6 karet, tak P(souper ma plonkovou X) = (18 nad 9) / (20 nad 10) ~ 0.263
                    //pokud mam na zacatku 5 karet, tak P(souper ma plonkovou X) ~ 0.131
                    //var SolitaryXThreshold = 0.13f;//25f;

					if (TeamMateIndex == -1)
                    {
                        //c--
                        //pokud muzes vytahnout trumfovou x ale mas v jine barve plonkovou x, tak se ji nejprve zbav a toto pravidlo nehraj
                        if (_hands[MyIndex].HasA(_trump) &&
                            !myInitialHand.HasX(_trump) &&
                            myInitialHand.CardCount(_trump) >= 5 &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => _hands[MyIndex].CardCount(b) == 1 &&
                                          _hands[MyIndex].HasX(b)))
                        {
                            return null;
                        }
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            //(_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Kral)) > _epsilon ||
                                                                            // _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Kral)) > _epsilon) &&
                                                                            (i.Suit == _trump || 
                                                                             ((_probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                               _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                              (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                                _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                              (_probabilities.SuitProbability(player2, _trump, RoundNumber) == 0 ||
                                                                               _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0))) &&
                                                                            (_probabilities.HasSolitaryX(player2, i.Suit, RoundNumber) >= SolitaryXThreshold ||
                                                                             _probabilities.HasSolitaryX(player3, i.Suit, RoundNumber) >= SolitaryXThreshold)).ToList();
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //co-
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            _probabilities.HasSolitaryX(player3, i.Suit, RoundNumber) == 1).ToList();
                                                                            //(_probabilities.HasSolitaryX(player3, i.Suit, RoundNumber) == 1 ||
                                                                            // (i.Suit != _trump &&                                   //pokud mas jen 1 barvu nebo 2 barvy
                                                                            //  (hands[MyIndex].SuitCount == 1 ||                     //a jedna z nich je trumfova
                                                                            //   (hands[MyIndex].SuitCount == 2 &&                    //a souper ma jiste X od meho esa
                                                                            //    hands[MyIndex].HasSuit(_trump)))) &&                //a max jednu dalsi kartu v barve
                                                                            //  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) == 1 &&
                                                                            //  _probabilities.PotentialCards(player3).CardCount(i.Suit) <= 2)).ToList();
                                                                            //(i.Suit == _trump ||
                                                                            // (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                            //  _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0)) &&
                                                                            // _probabilities.HasSolitaryX(player3, i.Suit, RoundNumber) >= SolitaryXThresholdDefense).ToList();
                    }
                    else
                    {
                        //c-o
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            _probabilities.HasSolitaryX(player2, i.Suit, RoundNumber) == 1).ToList();
                                                                            //(_probabilities.HasSolitaryX(player2, i.Suit, RoundNumber) == 1 ||
                                                                            // (i.Suit != _trump &&                                   //pokud mas jen 1 barvu nebo 2 barvy
                                                                            //  (hands[MyIndex].SuitCount == 1 ||                     //a jedna z nich je trumfova
                                                                            //   (hands[MyIndex].SuitCount == 2 &&                    //a souper ma jiste X od meho esa
                                                                            //    hands[MyIndex].HasSuit(_trump)))) &&                //a max jednu dalsi kartu v barve
                                                                            //  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) == 1 &&
                                                                            //  _probabilities.PotentialCards(player2).CardCount(i.Suit) <= 2)).ToList();
                                                                            //(i.Suit == _trump ||
                                                                            // (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                            //  _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0)) &&
                                                                            //_probabilities.HasSolitaryX(player2, i.Suit, RoundNumber) >= SolitaryXThresholdDefense).ToList();
                    }

                    return cardsToPlay.RandomOneOrDefault();
                }
                #endregion
            };
            
            if (RoundNumber == 8)
            {
                yield return new AiRule()
                {
                    Order = 6,
                    Description = "šetřit trumfy nakonec",
                    SkipSimulations = true,
                    #region ChooseCard1 Rule6
                    ChooseCard1 = () =>
                    {
                        if ((_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0)                            //pokud se hraje sedma nebo sedma proti
                        {
                            if (TeamMateIndex == -1)
                            {
                                var trumpsLeft = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                     .Select(i => new Card(_trump, i))
                                                     .Where(i => _probabilities.CardProbability(player2, i) > _epsilon ||
                                                                 _probabilities.CardProbability(player3, i) > _epsilon);

                                if (hands[MyIndex].CardCount(_trump) == 2 &&                            //a mam v ruce 2 posledni trumfy a neco tretiho
                                    trumpsLeft.Count() > 0 &&                                           //a pokud souper ma max. dva trumfy v ruce
                                    (trumpsLeft.Count() >= 2 ||                                         //a pokud alespon jeden ze souperovych trumfu je vetsi nez muj
                                     (hands[MyIndex].Any(i => i.Suit == _trump &&                       //tak hraj netrumfovou kartu at je to A nebo X
                                                              i.Value < trumpsLeft.Last().Value))))     //abychom uhrali sedmu nakonec resp. aby ji souper neuhral
                                {
                                    var cardToPlay = hands[MyIndex].First(i => i.Suit != _trump);

                                    //pravidlo nehraj pokud hraju sedmu, souperi maji vetsi kartu v barve, kterou chci hrat
                                    //a zbyva jim jeden trumf a navic maji barvu, kterou neznam,
                                    //protoze v devatem kole by me o trumf pripravili a v poslednim kole mi sedmu zabili trumfem
                                    if ((_gameType & Hra.Sedma) != 0 &&
                                        trumpsLeft.Count() == 1 &&
                                        Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                            .Where(h => h > cardToPlay.Value)
                                            .Select(h => new Card(cardToPlay.Suit, h))
                                            .Any(i => (_probabilities.CardProbability(player2, i) > _epsilon &&
                                                       Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                            .Any(b => _probabilities.SuitProbability(player2, b, RoundNumber) > 0)) ||
                                                      (_probabilities.CardProbability(player3, i) > _epsilon &&
                                                       Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                            .Any(b => _probabilities.SuitProbability(player3, b, RoundNumber) > 0))))
                                    {
                                        return null;
                                    }

                                    return cardToPlay;
                                }
                            }
                            else if (TeamMateIndex == player2)
                            {
                                var trumpsLeft = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                     .Select(i => new Card(_trump, i))
                                                     .Where(i => _probabilities.CardProbability(player3, i) > _epsilon);
                                if (hands[MyIndex].CardCount(_trump) == 2 &&                            //a mam v ruce 2 posledni trumfy a neco tretiho
                                    trumpsLeft.Count() > 0 &&                                           //a pokud souper ma max. dva trumfy v ruce
                                    (trumpsLeft.Count() >= 2 ||                                         //a pokud alespon z jeden ze souperovych trumfu je vetsi nez muj
                                     hands[MyIndex].Any(i => i.Suit == _trump &&                        //tak hraj netrumfovou kartu at je to A nebo X
                                                          i.Value < trumpsLeft.Last().Value)))          //abychom uhrali sedmu nakonec
                                {
                                    return hands[MyIndex].First(i => i.Suit != _trump);
                                }
                            }
                            else
                            {
                                var trumpsLeft = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                     .Select(i => new Card(_trump, i))
                                                     .Where(i => _probabilities.CardProbability(player2, i) > _epsilon);
                                if (hands[MyIndex].CardCount(_trump) == 2 &&                            //pokud hraju sedmu a mam v ruce 2 posledni trumfy a neco tretiho
                                    trumpsLeft.Count() > 0 &&                                           //a pokud souper ma max. dva trumfy v ruce
                                    (trumpsLeft.Count() >= 2 ||                                         //a pokud alespon z jeden ze souperovych trumfu je vetsi nez muj
                                     hands[MyIndex].Any(i => i.Suit == _trump &&                        //tak hraj netrumfovou kartu at je to A nebo X
                                                          i.Value < trumpsLeft.Last().Value)))          //abychom uhrali sedmu nakonec
                                {
                                    return hands[MyIndex].First(i => i.Suit != _trump);
                                }
                            }
                        }
                        return null;
                    }
                    #endregion
                };
            }

            yield return new AiRule()
            {
                Order = 7,
                Description = "zkusit uhrát bodovanou kartu",
                SkipSimulations = true,
                #region ChooseCard1 Rule7
                ChooseCard1 = () =>
                {
                    //proti kilu zkus vytlacit netrumfovou hlasku pokud se hraje kilo na prvni hlas
                    var opponent = TeamMateIndex == player2 ? player3 : player2;

                    if (TeamMateIndex != -1 &&
                        (_gameType & Hra.Kilo) != 0 &&
                        _hlasConsidered == HlasConsidered.First &&
                        _rounds != null &&
                        _rounds.All(r => r == null ||
                                         !((r.hlas1 && r.player1.PlayerIndex == opponent && r.c1.Suit == _trump) ||
                                           (r.hlas2 && r.player2.PlayerIndex == opponent && r.c2.Suit == _trump) ||
                                           (r.hlas3 && r.player3.PlayerIndex == opponent && r.c3.Suit == _trump))) &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump)
                            .Any(b => _hands[MyIndex].Any(i => i.Suit == b &&
                                                               i.Value < Hodnota.Svrsek &&
                                                               !_probabilities.PotentialCards(TeamMateIndex).HasSuit(b) &&
                                                               _probabilities.PotentialCards(opponent).HasK(b) &&
                                                               _probabilities.PotentialCards(opponent).HasQ(b) &&
                                                               _probabilities.PotentialCards(opponent)
                                                                             .Where(j => j.Suit == b &&
                                                                                         j.Value > i.Value)
                                                                             .All(j => j.Value == Hodnota.Kral ||
                                                                                       j.Value == Hodnota.Svrsek))))
                    {
                        return null;
                    }
                    //nehraj pravidlo pokud spoluhrac hlasil sedmu proti - bodovane karty budeme mazat
                    if (TeamMateIndex != -1 &&
                        (_gameType & Hra.SedmaProti) != 0 &&
                        _probabilities.CardProbability(TeamMateIndex, new Card(_trump, Hodnota.Sedma)) == 1)
                    {
                        return null;
                    }
                    //nehraj pokud mas hodne trumfu
                    if (hands[MyIndex].CardCount(_trump) >= 5)
                    {
                        return null;
                    }
                    if (TeamMateIndex != -1 &&
                        hands[MyIndex].CardCount(_trump) == 1 &&
                        hands[MyIndex].HasA(_trump) &&
                        ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 ||
                         (_gameType & Hra.Hra) == 0 ||
                         SevenValue < GameValue) &&
                        _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) == 0 &&
                        _probabilities.PotentialCards(TeamMateIndex).Any(i => i.Value >= Hodnota.Desitka &&
                                                                              i.Suit != _trump))
                    {
                        return null;
                    }
                    if (TeamMateIndex != -1)
                    {
                        //pokud ma oponent jen jednu neznamou kartu a na zadnou z jeho znamych karet nemuzu pozdeji namazat
                        var opponentsLikelyCards = _probabilities.LikelyCards(player2);
                        if (opponentsLikelyCards.Length == 10 - RoundNumber &&
                            (hands[MyIndex].CardCount(_trump) > 1 ||
                             ((List<Card>)hands[MyIndex]).Select(i => i.Suit).Distinct().Where(b => b != _trump).Count() == 3))
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value >= Hodnota.Desitka &&
                                                                                    i.Suit != _trump &&
                                                                                    _probabilities.CardProbability(opponent, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                    _probabilities.CardProbability(opponent, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon &&
                                                                                    _probabilities.SuitProbability(opponent, i.Suit, RoundNumber) > 0)
                                                                        .ToList();

                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.First();
                            }
                        }

                        if (opponentsLikelyCards.Length == 10 - RoundNumber &&
                            !hands[MyIndex].HasSuit(_trump) &&
                            opponentsLikelyCards.Where(i => i.Suit != _trump)
                                                .All(i => (hands[MyIndex].HasSuit(i.Suit) &&
                                                           hands[MyIndex].CardCount(i.Suit) <= opponentsLikelyCards.Count(j => j.Suit == i.Suit)) ||
                                                          _probabilities.SuitHigherThanCardProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor))
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value >= Hodnota.Desitka &&
                                                                                    i.Suit != _trump &&
                                                                                    _probabilities.CardProbability(opponent, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                    _probabilities.CardProbability(opponent, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon &&
                                                                                    _probabilities.SuitProbability(opponent, i.Suit, RoundNumber) > 0)
                                                                    .ToList();

                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.First();
                            }
                        }

                        //pokud nemuzes eso namazat a lze ho teoreticky uhrat, tak do toho jdi
                        if (hands[MyIndex].CardCount(_trump) > 1 &&
                            (SevenValue > GameValue ||                      //pri sedme vzdy
                             Enum.GetValues(typeof(Barva)).Cast<Barva>()    //pri hre jen kdyz nemas nejakou plonkovou barvu
                                 .Where(b => b != _trump &&
                                             hands[MyIndex].HasSuit(b))
                                 .All(b => hands[MyIndex].HasA(b))) &&
                            _probabilities.PotentialCards(opponent).CardCount(_trump) > _hands[MyIndex].CardCount(_trump) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b))
                                .Any(b => hands[MyIndex].HasA(b) &&
                                          _probabilities.PotentialCards(opponent).HasSuit(b)))
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value == Hodnota.Eso &&
                                                                                    _probabilities.PotentialCards(opponent).HasSuit(i.Suit))
                                                                        .ToList();
                            if (cardsToPlay.Any(i => !_probabilities.PotentialCards(opponent).HasX(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => !_probabilities.PotentialCards(opponent).HasX(i.Suit)).ToList();
                            }

                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.First();
                            }
                        }
                    }

                    //pokud hrajes kilo na 4 trumfy (nebo min) a mas vic nez pet ostrych karet (cili souperi maji maximalne 2), muzes zkusit jestli eso projde
                    if (TeamMateIndex == -1 &&
                        (_gameType & Hra.Kilo) != 0 &&
                        //(myInitialHand.HasK(_trump) ||
                        // myInitialHand.HasQ(_trump)) &&
                        myInitialHand.CardCount(_trump) <= 4 &&
                        myInitialHand.CardCount(Hodnota.Eso) +
                        myInitialHand.CardCount(Hodnota.Desitka) > 5 &&
                        myInitialHand.CardCount(Hodnota.Eso) > 1 &&
                        myInitialHand.Count(i => i.Value == Hodnota.Desitka &&
                                                 myInitialHand.HasA(i.Suit)) >= 2 &&
                        _hands[MyIndex].CardCount(_trump) <= _probabilities.PotentialCards(player2) //pokud mas hodne trumfu a propad si az sem, tak pravidlo nehraj (asi si mas odmazat plonk x)
                                                                           .Concat(_probabilities.PotentialCards(player3))
                                                                           .Distinct()
                                                                           .Count(i => i.Suit == _trump))
                    {
                        var cardsToPlay = ValidCards(_hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                 i.Value >= Hodnota.Desitka &&
                                                                                 myInitialHand.CardCount(i.Suit) <= 5 &&
                                                                                 _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                                                 _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 &&
                                                                                 _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                 _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0)
                                                                     .ToList();

                        return cardsToPlay.OrderByDescending(i => myInitialHand.CardCount(i.Suit)).FirstOrDefault();
                    }

                    var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                 .Where(h => h > i.Value)
                                                                 .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                           _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0))
                                                 .ToList();
                    //nehraj pokud mas dost trumfu i ostrych karet a k tomu neco nizkeho
                    if ((hands[MyIndex].CardCount(_trump) >= 5 ||
                         (TeamMateIndex != -1 &&
                          hands[MyIndex].CardCount(_trump) >= 4)) &&
                        hands[MyIndex].Count(i => i.Value == Hodnota.Eso) >= 2 &&
                        hands[MyIndex].HasA(_trump))
                    {
                        var holesPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .ToDictionary(k => k, v =>
                                                   Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                       .Select(h => new Card(v, h))
                                                       .Where(i => myInitialHand.All(j => j != i))
                                                       .OrderBy(i => i.Value)
                                                       .Skip(topCards.CardCount(v))
                                                       .ToList());
                        var lowCards = hands[MyIndex].Where(i => holesPerSuit[i.Suit].Any(j => j.Value > i.Value))
                                                     .ToList();

                        if (lowCards.Any(i => i.Suit != _trump))
                        {
                            return null;
                        }
                    }

                    if (TeamMateIndex == -1 && 
                        ((_gameType & Hra.Kilo) == 0 ||
                         _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0 ||
                         _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0))
                    {
                        //c--
                        List<Card> cardsToPlay = null;
                        var opponentPotentialCards = _probabilities.PotentialCards(player2)
                                                                   .Concat(_probabilities.PotentialCards(player3))
                                                                   .Distinct()
                                                                   .ToList();
                        //pokud mam v barve A nebo A,X a celkem 4 az 5 karet, navic malo trumfu a nemuzu chtit vytlacit ze souperu nejake eso
                        //tak zkus stesti, pokud to nevyjde, stejne by uhrat nesly
                        if ((_gameType & Hra.Kilo) == 0 &&
                            myInitialHand.CardCount(_trump) <= 4 &&
                            !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Where(b => b != _trump)
                                 .Any(b => _hands[MyIndex].HasX(b) &&
                                           _hands[MyIndex].CardCount(b) > 1 &&
                                           opponentPotentialCards.HasA(b)))
                        {
                            cardsToPlay = ValidCards(_hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                 myInitialHand.CardCount(i.Suit) >= 4 &&
                                                                                 myInitialHand.CardCount(i.Suit) <= 5 &&
                                                                                 (i.Value == Hodnota.Eso ||
                                                                                  (i.Value == Hodnota.Desitka &&
                                                                                   !opponentPotentialCards.HasA(i.Suit))) &&
                                                                                 _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                                                 _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 &&
                                                                                 opponentPotentialCards.CardCount(i.Suit) >= 2)
                                                                     .ToList();
                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.OrderByDescending(i => i.Value).First();
                            }
                        }
                        //pokud mam malo trumfu a mam v barve A nebo A,X a souperi maji ve stejne barve max 3 resp. 4 karty,
                        //tak zkus stesti, pokud to nevyjde, stejne by uhrat nesly
                        if ((_gameType & Hra.Kilo) == 0 &&
                            (myInitialHand.CardCount(_trump) <= 3 ||
                             (myInitialHand.CardCount(_trump) <= 4 &&
                              !myInitialHand.HasA(_trump))) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => (_probabilities.SuitProbability(player2, b, RoundNumber) > 0 ||
                                           _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                          (_probabilities.SuitProbability(player3, b, RoundNumber) > 0 ||
                                           _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                          opponentPotentialCards.Where(i => i.Suit == b)
                                                                .Count() > 1 &&
                                          opponentPotentialCards.Where(i => i.Suit == b)
                                                                .Count() <= (hands[MyIndex].HasA(b) ? 1 : 0) +
                                                                            (hands[MyIndex].HasX(b) &&
                                                                             _probabilities.CardProbability(player2, new Card(b, Hodnota.Eso)) == 0 &&
                                                                             _probabilities.CardProbability(player3, new Card(b, Hodnota.Eso)) == 0 ? 1 : 0) +
                                                                            (_probabilities.SuitProbability(player2, b, RoundNumber) > 0 &&
                                                                             _probabilities.SuitProbability(player3, b, RoundNumber) > 0 ? 2 : 0)))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                i.Value >= Hodnota.Desitka &&
                                                                                _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 ||
                                                                                 _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                                (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 ||
                                                                                 _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                                _probabilities.PotentialCards(player2)
                                                                                              .Where(j => j.Suit == i.Suit)
                                                                                              .Concat(_probabilities.PotentialCards(player3)
                                                                                              .Where(j => j.Suit == i.Suit))
                                                                                              .Distinct()
                                                                                              .Count() <= (hands[MyIndex].HasA(i.Suit) ? 1 : 0) +
                                                                                                          (hands[MyIndex].HasX(i.Suit) &&
                                                                                                          _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                                          _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 ? 1 : 0) +
                                                                                                          (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                                                                           _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 ? 2 : 0))
                                                                    .ToList();
                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                            }
                        }
                        //nehraj pokud mas plonkovou barvu
                        if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Any(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b) &&
                                            !myInitialHand.HasA(b) &&
                                            !myInitialHand.HasX(b)))
                        {
                            return null;
                        }
                        //nehraj pokud mam dost nejvyssich trumfu a muzu je ze souperu vytahnout
                        //nebo pokud mam dost trumfu celkem a k tomu barvu kde nemam nejvyssi kartu
                        var holes = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().Where(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > _epsilon ||
                                                                                               _probabilities.CardProbability(player3, new Card(_trump, h)) > _epsilon).ToList();
                        var topTrumps = ValidCards(hands[MyIndex]).Count(i => i.Suit == _trump && holes.All(h => h < i.Value));
                        if (holes.Count > 0 &&
                            (topTrumps > 0 ||
                             hands[MyIndex].CardCount(_trump) >= 6) &&
                            (topTrumps >= holes.Count ||
                             (hands[MyIndex].CardCount(_trump) >= holes.Count &&
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Any(b => b != _trump &&
                                            hands[MyIndex].Where(i => i.Suit == b)
                                                          .All(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Where(h => h > i.Value)
                                                                        .Select(h => new Card(b, h))
                                                                        .Any(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                  _probabilities.CardProbability(player3, j) > 0))))))
                        {
                            return null;
                        }
                        
                        //TODO: hrat treba jen jednu ostrou kartu pokud jsem na zacatku mel 3 nebo 4 karty v barve?
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            ((i.Value == Hodnota.Eso &&
                                                                              ((_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) == 0 &&
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) == 0) ||
                                                                               myInitialHand.CardCount(i.Suit) >= 4 ||
                                                                               (RoundNumber >= 7 &&
                                                                                hands[MyIndex].HasA(_trump) &&
                                                                                hands[MyIndex].Has7(_trump)))) ||
                                                                             (i.Value == Hodnota.Desitka &&
                                                                              _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                              _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) <= _epsilon)) &&
                                                                            ((_probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                              _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                             (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                              _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0))
                                                                          //                                                             (myInitialHand.CardCount(i.Suit) <= 2 ||
                                                                          //                                                              (myInitialHand.CardCount(i.Suit) <= 3 &&
                                                                          //                                                               myInitialHand.CardCount(_trump) <= 3) ||
                                                                          //  (myInitialHand.CardCount(i.Suit) <= 4 &&
                                                                          //myInitialHand.CardCount(_trump) <= 2))
                                                                          )
                                                                    .ToList();

                        return cardsToPlay.OrderByDescending(i => myInitialHand.CardCount(i.Suit))
                                          .ThenByDescending(i => i.Value)
                                          .FirstOrDefault();
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //co-
                        //pokud mam hodne trumfu, tak je zbytecne riskovat a mel bych hrat bodovanou kartu
                        //az pote co ze soupere vytlacim vsechny trumfy
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 &&
                                                                                ((i.Value == Hodnota.Eso &&
                                                                                  _probabilities.MaxCardCount(player3, i.Suit) >= 3 &&
                                                                                  (_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) == 0 ||//<= _epsilon ||
                                                                                   ((_gameType & (Hra.Kilo | Hra.KiloProti | Hra.SedmaProti)) != 0 &&
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon))) ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) <= _epsilon)) &&
                                                                                (//(_probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - RiskFactor &&
                                                                                 // hands[MyIndex].CardCount(_trump) < _probabilities.MaxCardCount(player3, _trump)) ||
                                                                                 (_probabilities.CertainCards(player3).Any(j => j.Suit == i.Suit) ||
                                                                                  _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) ||
                                                                                 (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 &&
                                                                                  hands[MyIndex].CardCount(_trump) > 2 &&
                                                                                  myInitialHand.CardCount(_trump) <= 4) ||
                                                                                 (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor &&
                                                                                  (hands[MyIndex].CardCount(_trump) > 1 ||
                                                                                   (_hands[MyIndex].CardCount(i.Suit) <= 5 &&
                                                                                    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                        .Where(b => b != _trump &&                                                                                                   
                                                                                                    hands[MyIndex].HasSuit(b))
                                                                                        .All(b => hands[MyIndex].HasA(b) ||
                                                                                                  hands[MyIndex].HasX(b)))) &&
                                                                                   !(hands[MyIndex].CardCount(_trump) <= 2 &&
                                                                                     Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                         .Where(b => b != _trump)
                                                                                         .Any(b => !hands[MyIndex].HasSuit(b) &&
                                                                                                   _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) >= 1 - RiskFactor))))
                                                                          //(myInitialHand.CardCount(i.Suit) <= 2 ||
                                                                          //(myInitialHand.CardCount(i.Suit) <= 3 &&
                                                                          // myInitialHand.CardCount(_trump) <= 3) ||
                                                                          //(myInitialHand.CardCount(i.Suit) <= 4 &&
                                                                          //myInitialHand.CardCount(_trump) <= 2))
                                                                          )
                                                                    .ToList();

                        //pokud jsem mel na zacatku malo trumfu a hodne desitek a es, zkus neco uhrat
                        if (!cardsToPlay.Any() &&
                            (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Where(b => b != _trump)
                                 .All(b => _probabilities.CardProbability(player3, new Card(b, Hodnota.Eso)) == 0) ||
                             (hands[MyIndex].HasSuit(_trump) &&
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => hands[MyIndex].HasSuit(b))
                                  .All(b => hands[MyIndex].HasA(b)))) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Sum(b => (myInitialHand.HasA(b) ? 1 : 0) + (myInitialHand.HasX(b) ? 1 : 0)) >= 3 &&
                            myInitialHand.CardCount(_trump) <= 3 &&
                            myInitialHand.HasSuit(_trump))  //neplati pokud jsem nemel trumfy - je lepsi si pockat na mazani
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) <= _epsilon)) &&
                                                                                (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                                 ((_gameType & Hra.Kilo) != 0 &&
                                                                                  _probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1)))
                                                                    .ToList();

                            return cardsToPlay.OrderBy(i => myInitialHand.CardCount(i.Suit))
                                              .ThenByDescending(i => i.Value)
                                              .FirstOrDefault();
                        }
                        return cardsToPlay.OrderByDescending(i => myInitialHand.CardCount(i.Suit))
                                          .ThenByDescending(i => i.Value)
                                          .FirstOrDefault();
                    }
                    else if (TeamMateIndex == player3)
                    {
                        //c-o
                        //pokud mam hodne trumfu, tak je zbytecne riskovat a mel bych hrat bodovnou kartu
                        //az pote co ze soupere vytlacim vsechny trumfy
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                                                ((i.Value == Hodnota.Eso &&
                                                                                  (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) == 0 ||
                                                                                   ((_gameType & (Hra.Kilo | Hra.KiloProti | Hra.SedmaProti)) != 0 &&
                                                                                    _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon))) ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) <= _epsilon)) &&
                                                                                (//(_probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor &&
                                                                                 // hands[MyIndex].CardCount(_trump) < _probabilities.MaxCardCount(player2, _trump)) ||
                                                                                 (_probabilities.CertainCards(player2).Any(j => j.Suit == i.Suit) ||
                                                                                  _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) ||
                                                                                 //(_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                                                 // hands[MyIndex].CardCount(_trump) > 2) ||
                                                                                  (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - RiskFactor &&
                                                                                   (hands[MyIndex].CardCount(_trump) > 1 ||
                                                                                    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                        .Where(b => b != _trump &&
                                                                                                    hands[MyIndex].HasSuit(b))
                                                                                        .All(b => hands[MyIndex].HasA(b) ||
                                                                                                  hands[MyIndex].HasX(b))) &&
                                                                                   !(hands[MyIndex].CardCount(_trump) <= 2 &&
                                                                                     Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                         .Where(b => b != _trump)
                                                                                         .Any(b => !hands[MyIndex].HasSuit(b) &&
                                                                                                   _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) >= 1 - RiskFactor))))
                                                                          //(myInitialHand.CardCount(i.Suit) <= 2 ||
                                                                          //(myInitialHand.CardCount(i.Suit) <= 3 &&
                                                                          // myInitialHand.CardCount(_trump) <= 3) ||
                                                                          //(myInitialHand.CardCount(i.Suit) <= 4 &&
                                                                          //myInitialHand.CardCount(_trump) <= 2))
                                                                          )
                                                                    .ToList();

                        //pokud jsem mel na zacatku malo trumfu a hodne desitek a es, zkus neco uhrat
                        if (!cardsToPlay.Any() &&
                            (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Where(b => b != _trump)
                                 .All(b => _probabilities.CardProbability(player2, new Card(b, Hodnota.Eso)) == 0) ||
                             (hands[MyIndex].HasSuit(_trump) &&
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => hands[MyIndex].HasSuit(b))
                                  .All(b => hands[MyIndex].HasA(b)))) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Sum(b => (myInitialHand.HasA(b) ? 1 : 0) + (myInitialHand.HasX(b) ? 1 : 0)) >= 3 &&
                            myInitialHand.CardCount(_trump) <= 3 &&
                            myInitialHand.HasSuit(_trump))  //neplati pokud jsem nemel trumfy - je lepsi si pockat na mazani
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) <= _epsilon)) &&
                                                                                (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                                 ((_gameType & Hra.Kilo) != 0 &&
                                                                                  _probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 1)))
                                                                    .ToList();

                            return cardsToPlay.OrderBy(i => myInitialHand.CardCount(i.Suit))
                                              .ThenByDescending(i => i.Value)
                                              .FirstOrDefault();
                        }
                        return cardsToPlay.OrderByDescending(i => myInitialHand.CardCount(i.Suit))
                                          .ThenByDescending(i => i.Value)
                                          .FirstOrDefault();
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 8,
                Description = "vytlačit bodovanou kartu",
                SkipSimulations = true,
                #region ChooseCard1 Rule7
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

                    //toto pravidlo by melo ze soupere vytlacit bodovanou kartu kterou muj spoluhrac sebere trumfem
                    //prilis prisne podminky budou znamenat, ze se pravidlo skoro nikdy neuplatni
                    //prilis benevolentni podminky zase, ze se pravidlo zahraje i kdyz by nemelo
                    //(napr. hraju X, souper prebije A, a kolegovi zbyde jedna barva na ruce, takze nemuze prebit trumfem)
                    if ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 ||
                        (_gameType & Hra.Kilo) != 0)
                    {
                        if (TeamMateIndex == player2)
                        {
                            //co-
                            if (RoundNumber == 9 &&
                                ValidCards(hands[MyIndex]).Contains(new Card(_trump, Hodnota.Sedma)) &&
                                _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0)
                            {
                                return null;
                            }
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !hands[MyIndex].HasA(i.Suit) && //pokud mas eso tak si pockej na akterovu desitku a pravidlo nehraj
                                                                                ((_probabilities.HasAOrXAndNothingElse(player3, i.Suit, RoundNumber) >= 1 - RiskFactor &&
                                                                                  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                                  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon) ||
                                                                                 _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                                (_probabilities.SuitProbability(player2, _trump, RoundNumber) >= 1 - RiskFactor ||
                                                                                 (_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 &&
                                                                                  _probabilities.SuitProbability(player2, i.Suit, RoundNumber) <= RiskFactor))).ToList();
                            if (!cardsToPlay.Any() &&
                                (_gameType & Hra.Kilo) != 0)
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    !hands[MyIndex].HasA(i.Suit) && //pokud mas eso tak si pockej na akterovu desitku a pravidlo nehraj
                                                                                    _probabilities.HasAOrXAndNothingElse(player3, i.Suit, RoundNumber) >= 0.5f &&
                                                                                    _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) <= 0.1f &&
                                                                                    _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) <= 0.1f &&
                                                                                    (_probabilities.SuitProbability(player2, _trump, RoundNumber) >= 1 - RiskFactor ||
                                                                                     (_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 &&
                                                                                      _probabilities.SuitProbability(player2, i.Suit, RoundNumber) <= RiskFactor))).ToList();
                            }
                            //cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                            //(_probabilities.HasAOrXAndNothingElse(player3, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                            // _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                            //_probabilities.SuitProbability(player2, _trump, RoundNumber) >= 1 - RiskFactor &&
                            //_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0).ToList();
                        }
                        else if (TeamMateIndex == player3)
                        {
                            //c-o
                            if (RoundNumber == 9 &&
                                ValidCards(hands[MyIndex]).Contains(new Card(_trump, Hodnota.Sedma)) &&
                                _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0)
                            {
                                return null;
                            }
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                ((_probabilities.HasAOrXAndNothingElse(player2, i.Suit, RoundNumber) >= 1 - RiskFactor &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon) ||
                                                                                 _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                                _probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor &&
                                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) <= RiskFactor).ToList();
                            if (!cardsToPlay.Any() &&
                                (_gameType & Hra.Kilo) != 0)
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    _probabilities.HasAOrXAndNothingElse(player2, i.Suit, RoundNumber) >= 0.5f &&
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) <= 0.1f &&
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) <= 0.1f &&
                                                                                    (_probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor ||
                                                                                     (_probabilities.SuitProbability(player3, _trump, RoundNumber) > 0 &&
                                                                                      _probabilities.SuitProbability(player3, i.Suit, RoundNumber) <= RiskFactor))).ToList();
                            }
                            //cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                            //(_probabilities.HasAOrXAndNothingElse(player2, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                            // _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                            //_probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor &&
                            //_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0).ToList();
                        }
                        //proti kilu zkus vytlacit netrumfovou hlasku pokud se hraje kilo na prvni hlas
                        var opponent = TeamMateIndex == player2 ? player3 : player2;

                        if (TeamMateIndex != -1 &&
                            (_gameType & Hra.Kilo) != 0 &&
                            !cardsToPlay.Any() &&
                            _hlasConsidered == HlasConsidered.First &&
                            _rounds != null &&
                            _rounds.All(r => r == null ||
                                             !((r.hlas1 && r.player1.PlayerIndex == opponent && r.c1.Suit == _trump) ||
                                               (r.hlas2 && r.player2.PlayerIndex == opponent && r.c2.Suit == _trump) ||
                                               (r.hlas3 && r.player3.PlayerIndex == opponent && r.c3.Suit == _trump))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                i.Value < Hodnota.Svrsek &&
                                                                                !_probabilities.PotentialCards(TeamMateIndex).HasA(i.Suit) &&
                                                                                !_probabilities.PotentialCards(TeamMateIndex).HasX(i.Suit) &&
                                                                                !_probabilities.PotentialCards(opponent).HasA(i.Suit) &&
                                                                                !_probabilities.PotentialCards(opponent).HasX(i.Suit) &&
                                                                                _probabilities.PotentialCards(opponent).HasK(i.Suit) &&
                                                                                _probabilities.PotentialCards(opponent).HasQ(i.Suit) &&
                                                                                _probabilities.PotentialCards(opponent).Where(j => j.Suit == i.Suit &&
                                                                                                                                  j.Value > i.Value)
                                                                                              .All(j => j.Value == Hodnota.Svrsek ||
                                                                                                        j.Value == Hodnota.Kral)).ToList();
                        }
                    }
                    return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                }
                #endregion
            };
            
            yield return new AiRule()
            {
                Order = 9,
                Description = "bodovat nebo vytlačit trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule9
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex != -1 &&
                        hands[MyIndex].CardCount(_trump) == 1 &&
                        hands[MyIndex].HasA(_trump) &&
                        ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 ||
                         (_gameType & Hra.Hra) == 0 ||
                         SevenValue < GameValue) &&
                        _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) == 0 &&
                        _probabilities.PotentialCards(TeamMateIndex).Any(i => i.Value >= Hodnota.Desitka &&
                                                                              i.Suit != _trump))
                    {
                        return null;
                    }
                    if (TeamMateIndex != -1 &&
                        (PlayerBids[TeamMateIndex] & Hra.Sedma) != 0)
                    {
                        return null;
                    }
                    if (TeamMateIndex == -1 &&
                        (((_gameType & Hra.Sedma) != 0 &&
                          SevenValue > GameValue) ||
                         (_gameType & Hra.SedmaProti) != 0) &&
                        (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0)
                    {
                        //c--
                        var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Where(h => h > i.Value)
                                                                     .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                               _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0))
                                                     .ToList();
                        var holesPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .ToDictionary(k => k, v =>
                                                   Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                       .Select(h => new Card(v, h))
                                                       .Where(i => myInitialHand.All(j => j != i))
                                                       .OrderBy(i => i.Value)
                                                       .Skip(topCards.CardCount(v))
                                                       .ToList());
                        var lowCards = hands[MyIndex].Where(i => holesPerSuit[i.Suit].Any(j => j.Value > i.Value))
                                                     .ToList();

                        //nehraj zbytecne pokud mas nejakou plonkovou barvu
                        if (//_gameType != (Hra.Hra | Hra.Sedma) &&
                            //_gameType != (Hra.Hra | Hra.SedmaProti) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Any(b => b != _trump &&
                                          !_bannedSuits.Contains(b) &&
                                          //hands[MyIndex].CardCount(b) <= 2 &&//i.Suit == poorSuit.Item1 &&
                                          hands[MyIndex].HasSuit(_trump) &&
                                          hands[MyIndex].HasSuit(b) &&
                                          (_probabilities.CardProbability(player2, new Card(b, Hodnota.Eso)) > _epsilon ||
                                           _probabilities.CardProbability(player3, new Card(b, Hodnota.Eso)) > _epsilon) &&
                                          (_probabilities.CardProbability(player2, new Card(b, Hodnota.Desitka)) > _epsilon ||
                                           _probabilities.CardProbability(player3, new Card(b, Hodnota.Desitka)) > _epsilon)))
                        {
                            return null;
                        }
                        /****/
                        var opponentPotentialCards = _probabilities.PotentialCards(player2)
                                                                   .Concat(_probabilities.PotentialCards(player3))
                                                                   .Distinct()
                                                                   .ToList();
                        ////pokud mam v barve A nebo A,X a celkem 4 az 5 karet, navic malo trumfu a nemuzu chtit vytlacit ze souperu nejake eso
                        ////tak zkus stesti, pokud to nevyjde, stejne by uhrat nesly
                        //if ((_gameType & Hra.Kilo) == 0 &&
                        //    myInitialHand.CardCount(_trump) <= 4 &&
                        //    !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        //         .Where(b => b != _trump)
                        //         .Any(b => _hands[MyIndex].HasX(b) &&
                        //                   _hands[MyIndex].CardCount(b) > 1 &&
                        //                   opponentPotentialCards.HasA(b)))
                        //{
                        //    cardsToPlay = ValidCards(_hands[MyIndex]).Where(i => i.Suit != _trump &&
                        //                                                         myInitialHand.CardCount(i.Suit) >= 4 &&
                        //                                                         myInitialHand.CardCount(i.Suit) <= 5 &&
                        //                                                         (i.Value == Hodnota.Eso ||
                        //                                                          (i.Value == Hodnota.Desitka &&
                        //                                                           !opponentPotentialCards.HasA(i.Suit))) &&
                        //                                                         _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                        //                                                         _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0)
                        //                                             .ToList();
                        //    if (cardsToPlay.Any())
                        //    {
                        //        return cardsToPlay.OrderByDescending(i => i.Value).First();
                        //    }
                        //}
                        /****/
                        //u sedmy (proti) hraju od nejvyssi karty (A nebo X) v nejdelsi netrumfove barve
                        //bud projde nebo ze soupere vytlacim trumf
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                opponentPotentialCards.CardCount(i.Suit) >= 2 &&
                                                                                (_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 ||
                                                                                 _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0) &&
                                                                                _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&                                                                                
                                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 &&                                                                                
                                                                                (i.Value == Hodnota.Eso ||
                                                                                (i.Value == Hodnota.Desitka &&
                                                                                 _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                 _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0)))
                                                                    .ToList();
                        if (hands[MyIndex].CardCount(_trump) >= 6 ||
                            (hands[MyIndex].CardCount(_trump) >= 5 &&
                             Enum.GetValues(typeof(Barva)).Cast<Barva>().Count(b => hands[MyIndex].HasSuit(b)) == 2))
                        {
                            return null;
                        }

                        var trumpHoles = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                             .Select(h => new Card(_trump, h))
                                             .Where(i => _probabilities.CardProbability(player2, i) > _epsilon ||
                                                         _probabilities.CardProbability(player3, i) > _epsilon)
                                             .ToList();

                        //pokud mam vic trumfu nez souperi, byl by to zbytecny risk
                        if (hands[MyIndex].CardCount(_trump) > trumpHoles.Count &&
                            hands[MyIndex].Where(i => i.Suit == _trump)
                                          .Any(i => trumpHoles.All(j => i.Value > j.Value)))
                        {
                            return null;
                        }
                        //pokud mas nejake nizke karty v jinych barvach, tak by to byl zbytecny risk
                        //neplati pokud mas malo trumfu
                        if (lowCards.Any(i => !cardsToPlay.Any(j => j.Suit == i.Suit)) &&
                            (myInitialHand.CardCount(_trump) >= 5 ||
                             (_gameType & Hra.Kilo) != 0))
                        {
                            return null;
                        }

                        return cardsToPlay.OrderByDescending(i => hands[MyIndex].CardCount(i.Suit))
                                          .ThenByDescending(i => i.Value)
                                          .FirstOrDefault();
                    }
                    else if (TeamMateIndex == player2 &&
                             (_gameType & Hra.Sedma) != 0 &&  //jen pri sedme, pri sedme proti neriskuj ze o ostrou kartu prijdes
                             SevenValue > GameValue &&
                             (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0)
                    {
                        //co-
                        var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Where(h => h > i.Value)
                                                                     .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                               _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0))
                                                     .ToList();

                        var holesPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .ToDictionary(k => k, v =>
                                                   Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                       .Select(h => new Card(v, h))
                                                       .Where(i => myInitialHand.All(j => j != i))
                                                       .OrderBy(i => i.Value)
                                                       .Skip(topCards.CardCount(v))
                                                       .ToList());

                        var lowCards = hands[MyIndex].Where(i => holesPerSuit[i.Suit].Any(j => j.Value > i.Value));
                        //u sedmy hraju od nejvyssi karty (A nebo X) v nejdelsi netrumfove barve
                        //bud projde nebo ze soupere vytlacim trumf
                        //nehraj pokud mas plonkovou barvu a nehrajes sedmu proti ani sedmu s flekama na hru
                        if ((_gameType & Hra.SedmaProti) == 0 &&
                            !(_gameType == (Hra.Hra | Hra.Sedma) &&
                              GameValue > SevenValue) &&
                            ValidCards(hands[MyIndex]).Any(i => i.Suit != _trump &&
                                                                //hands[MyIndex].CardCount(i.Suit) > 2 &&
                                                                !hands[MyIndex].HasA(i.Suit) &&
                                                                !hands[MyIndex].HasX(i.Suit) &&
                                                                _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0))
                        {
                            return null;
                        }
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - RiskFactorSevenDefense &&
                                                                                _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0 &&
                                                                                (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 ||
                                                                                 _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0)))
                                                                    .OrderByDescending(i => hands[MyIndex].CardCount(i.Suit))
                                                                    .ThenByDescending(i => i.Value)
                                                                    .Take(1)
                                                                    .ToList();

                        //pokud mas nejake nizke karty v jinych barvach a muzes mazat, tak by to byl zbytecny risk
                        if ((_gameType & Hra.SedmaProti) == 0 &&
                            !(_gameType == (Hra.Hra | Hra.Sedma) &&
                              GameValue > SevenValue) &&
                            !hands[MyIndex].HasSuit(_trump) &&
                            hands[MyIndex].SuitCount <= 2 &&
                            lowCards.Any(i => !cardsToPlay.Any(j => j.Suit == i.Suit)))
                        {
                            return null;
                        }
                        return cardsToPlay.FirstOrDefault();

                    }
                    else if (TeamMateIndex == player3 &&
                             (_gameType & Hra.Sedma) != 0 &&  //jen pri sedme, pri sedme proti neriskuj ze o ostrou kartu prijdes
                             SevenValue > GameValue &&
                             (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0)
                    {
                        //c-o
                        var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Where(h => h > i.Value)
                                                                     .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                               _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0))
                                                     .ToList();

                        var holesPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .ToDictionary(k => k, v =>
                                                   Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                       .Select(h => new Card(v, h))
                                                       .Where(i => myInitialHand.All(j => j != i))
                                                       .OrderBy(i => i.Value)
                                                       .Skip(topCards.CardCount(v))
                                                       .ToList());

                        var lowCards = hands[MyIndex].Where(i => holesPerSuit[i.Suit].Any(j => j.Value > i.Value));
                        //u sedmy hraju od nejvyssi karty (A nebo X) v nejdelsi netrumfove barve
                        //bud projde nebo ze soupere vytlacim trumf
                        //nehraj pokud mas plonkovou barvu a hrajes sedmu proti nebo sedmu s flekama na hru
                        if ((_gameType & Hra.SedmaProti) == 0 &&
                            !(_gameType == (Hra.Hra | Hra.Sedma) &&
                              GameValue > SevenValue) &&
                            ValidCards(hands[MyIndex]).Any(i => i.Suit != _trump &&
                                                                //hands[MyIndex].CardCount(i.Suit) > 2 &&
                                                                !hands[MyIndex].HasA(i.Suit) &&
                                                                !hands[MyIndex].HasX(i.Suit) &&
                                                                _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0))
                        {
                            return null;
                        }
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                _probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactorSevenDefense &&
                                                                                _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 &&
                                                                                (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 ||
                                                                                 _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) == 0)))
                                                                    .OrderByDescending(i => hands[MyIndex].CardCount(i.Suit))
                                                                    .ThenByDescending(i => i.Value)
                                                                    .Take(1)
                                                                    .ToList();

                        //pokud mas nejake nizke karty v jinych barvach a muzes mazat, tak by to byl zbytecny risk
                        if ((_gameType & Hra.SedmaProti) == 0 &&
                            !(_gameType == (Hra.Hra | Hra.Sedma) &&
                              GameValue > SevenValue) &&
                            !hands[MyIndex].HasSuit(_trump) &&
                            hands[MyIndex].SuitCount <= 2 &&
                            lowCards.Any(i => !cardsToPlay.Any(j => j.Suit == i.Suit)))
                        {
                            return null;
                        }
                        return cardsToPlay.FirstOrDefault();

                    }

                    return null;
                }
                #endregion
            };

			yield return new AiRule
            {
                Order = 10,
                Description = "odmazat si barvu",
                SkipSimulations = true,
                #region ChooseCard1 Rule10
                ChooseCard1 = () =>
                {
                    //var poorSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    //                   .Where(b => b != _trump &&
                    //                               hands[MyIndex].CardCount(b) > 0 &&                                                     
                    //                               ValidCards(hands[MyIndex]).Where(i => i.Suit == b)
                    //                                                       .All(i => i.Value != Hodnota.Desitka &&
                    //                                                                 i.Value != Hodnota.Eso))
                    //                 .ToDictionary(k => k, v => hands[MyIndex].CardCount(v))
                    //                 .OrderBy(kv => kv.Value)
                    //                 .Select(kv => new Tuple<Barva, int>(kv.Key, kv.Value))
                    //                 .FirstOrDefault();
                    if (TeamMateIndex != -1 &&
                        (PlayerBids[TeamMateIndex] & Hra.Sedma) != 0 &&
                        hands[MyIndex].Any(i => _teamMatesSuits.Contains(i.Suit)))
                    {
                        return null;
                    }
                    var opponent = TeamMateIndex == player2 ? player3 : player2;

                    if (TeamMateIndex != -1 &&                        
                        SevenValue > GameValue &&                           //pri sedme se nezbavuj plonka (abych si udrzel trumfy)
                        hands[MyIndex].HasSuit(_trump) &&
                        hands[MyIndex].Any(i => i.Suit != _trump &&         //pokud muzes uhrat nejake eso
                                                i.Value == Hodnota.Eso &&
                                                _probabilities.PotentialCards(opponent).HasSuit(i.Suit)))
                    {
                        return null;
                    }
                    //pokud hrajes proti kilu, tak se zbav trumfu, pokud mas v ostatnich barvach ostre karty
                    var holes = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                .Where(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > 0 ||
                                            _probabilities.CardProbability(player3, new Card(_trump, h)) > 0).ToList();
                    var topTrumps = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump && holes.All(h => h < i.Value)).ToList();

                    if (TeamMateIndex != -1 &&
                        _hands[MyIndex].HasSuit(_trump) &&
                        (topTrumps.Any() ||
                         !hands[MyIndex].HasX(_trump)) &&
                        _hands[MyIndex].CardCount(_trump) <= 2 &&
                        (_gameType & Hra.Kilo) != 0 &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump &&
                                        _hands[MyIndex].HasSuit(b))
                            .Any(b => _hands[MyIndex].HasA(b) ||
                                      _hands[MyIndex].HasX(b)))
                    {
                        var cardToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                               i.Value < Hodnota.Desitka &&
                                                                               (!_probabilities.PotentialCards(TeamMateIndex).HasX(_trump) ||
                                                                                _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor))
                                                                   .OrderBy(i => i.Value)
                                                                   .FirstOrDefault();
                        if (cardToPlay != null)
                        {
                            return cardToPlay;
                        }
                    }
                    //if (poorSuit != null && poorSuit.Item2 == 1)
                    {
                        //pokud ti zbyva posledni netrumfova karta, tak se ji zbav
                        var cardToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                               i.Value < Hodnota.Desitka &&
                                                                               !_bannedSuits.Contains(i.Suit) &&
                                                                               hands[MyIndex].CardCount(i.Suit) == 1 &&
                                                                               hands[MyIndex].CardCount(_trump) == 10 - RoundNumber)
                                                                   .FirstOrDefault();
                        if (cardToPlay != null)
                        {
                            return cardToPlay;
                        }
                        //odmazat si barvu pokud mam trumfy abych souperum mohl brat a,x
                        //pokud jsem volil sedmu tak si trumfy setrim
                        if (TeamMateIndex == -1 && (_gameType & Hra.Sedma) == 0)
                        {
                            //c--
                            cardToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                               !_bannedSuits.Contains(i.Suit) &&
                                                                               hands[MyIndex].CardCount(i.Suit) == 1 &&//i.Suit == poorSuit.Item1 &&
                                                                               hands[MyIndex].HasSuit(_trump) &&
                                                                               (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) > _epsilon ||
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > _epsilon) &&
                                                                               (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) > _epsilon ||
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) > _epsilon))
                                                                   .OrderByDescending(i => i.Value)
                                                                   .FirstOrDefault();
                            if (cardToPlay != null)
                            {
                                return cardToPlay;
                            }
                        }
                        else if (TeamMateIndex == player3 && (_gameType & Hra.SedmaProti) == 0)
                        {
                            //c-o
                            cardToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                               !_bannedSuits.Contains(i.Suit) &&
                                                                               _probabilities.NoSuitOrSuitLowerThanXProbability(player3, i.Suit, RoundNumber) > 1 - RiskFactor &&
                                                                               hands[MyIndex].CardCount(i.Suit) == 1 &&//i.Suit == poorSuit.Item1 &&
                                                                               hands[MyIndex].HasSuit(_trump) &&
                                                                               _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) > _epsilon &&
                                                                               _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) > _epsilon)
                                                                   .OrderByDescending(i => i.Value)
                                                                   .FirstOrDefault();
                            if (cardToPlay != null)
                            {
                                return cardToPlay;
                            }
                        }
                    }
                    if (TeamMateIndex != -1 && 
                        (_gameType & Hra.SedmaProti) == 0)
                    {
                        //odmazat si barvu pokud nemam trumfy abych mohl mazat
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                i.Value != Hodnota.Eso &&
                                                                                i.Value != Hodnota.Desitka &&
                                                                                !hands[MyIndex].HasSuit(_trump) &&
                                                                                hands[MyIndex].Any(j => j.Value == Hodnota.Eso ||
                                                                                                        j.Value == Hodnota.Desitka) &&
                                                                                hands[MyIndex].CardCount(i.Suit) == 1);
                        if (!cardsToPlay.Any() &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => hands[MyIndex].HasA(b) ||
                                          hands[MyIndex].HasX(b)))
                        {
                            var opponentIndex = Enumerable.Range(0, Game.NumPlayers).First(i => i != MyIndex && i != TeamMateIndex);
                            var opponentTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
							                         .Count(h => _probabilities.CardProbability(opponentIndex, new Card(_trump, h)) > _epsilon);
							var opponentCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
													.SelectMany(b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
							                                             .Select(h => new Card(b, h))
																         .Where(i => _probabilities.CardProbability(opponentIndex, i) > _epsilon));
							var opponentLoCards = opponentCards.Where(i => !hands[MyIndex].HasSuit(i.Suit) &&
                                                                           _probabilities.SuitHigherThanCardProbability(TeamMateIndex, i, RoundNumber) > 0);

                            //odmazat si trumf abych pozdeji mohl mazat 
                            //(musi existovat barva, kterou neznam a muj spoluhrac v ni doufejme ma vyssi karty nez akter)
                            if (((_gameType & Hra.Kilo) != 0 ||
                                 myInitialHand.CardCount(_trump) <= 1 ||
                                 (myInitialHand.CardCount(_trump) <= 2 &&
                                  hands[MyIndex].Where(i => i.Suit != _trump).ToList()
                                                .CardCount(Hodnota.Desitka) +
                                  hands[MyIndex].Where(i => i.Suit != _trump).ToList()
                                                .CardCount(Hodnota.Eso) >= 2)) &&
                               (PlayerBids[TeamMateIndex] & Hra.Sedma) == 0 &&
                               (TeamMateIndex == player3 ||
                                (_gameType & Hra.Kilo) != 0 ||
                                (hands[MyIndex].HasA(_trump) &&
                                 RoundNumber >= 4)) &&
                               Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                   .Any(b => !hands[MyIndex].HasSuit(b) &&
                                             _probabilities.PotentialCards(TeamMateIndex).Any(i => i.Suit == b &&
                                                                                                   i.Value >= Hodnota.Svrsek)) &&
                                //((_gameType & Hra.Kilo) != 0 ||
                                // _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) <= RiskFactor) &&
                               opponentTrumps > 0 &&
                                //opponentTrumps <= 6 &&  //abych nehral trumfem hned ale az pozdeji
                               Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                   .Any(b => b != _trump &&
                                             (hands[MyIndex].HasA(b) ||
                                              hands[MyIndex].HasX(b)) &&   //A,X neni treba mazat pokud vim, ze souper barvu zna a nema eso
                                             !(_probabilities.CardProbability(opponentIndex, new Card(b, Hodnota.Eso)) <= _epsilon &&
                                               _probabilities.SuitProbability(opponentIndex, b, RoundNumber) == 1)) &&
							   opponentLoCards.Any() &&
                               !(hands[MyIndex].HasX(_trump) &&            //neodmazavej pokud mam v barve jen X+1
                                 hands[MyIndex].CardCount(_trump) == 2))
                            {
                                //chci se vyhnout tomu aby moji nebo spoluhracovu trumfovou desitku sebral akter esem
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                    ((_gameType & Hra.Kilo) != 0 ||
                                                                                     hands[MyIndex].SuitCount < Game.NumSuits) &&
                                                                                    (_probabilities.CardProbability(opponentIndex, new Card(_trump, Hodnota.Eso)) <= _epsilon ||
                                                                                     (i.Value != Hodnota.Desitka &&
                                                                                      _probabilities.CardProbability(TeamMateIndex, new Card(_trump, Hodnota.Desitka)) <= _epsilon) ||
                                                                                     _probabilities.PotentialCards(TeamMateIndex).Count(j => j.Suit == _trump &&
                                                                                                                                             j.Value > i.Value) > 1));
                            }
                            if(!cardsToPlay.Any() &&
                               (PlayerBids[MyIndex] & Hra.Sedma) == 0 &&
                               (PlayerBids[TeamMateIndex] & Hra.Sedma) == 0)
                            {
                                //zbav se trumfu, pokud jich mas malo a kolega ma nejake vyssi karty nez akter v netrumfove barve
                                if (ValidCards(hands[MyIndex]).Any(i => i.Suit == _trump &&
                                                                        (i.Value < Hodnota.Desitka ||
                                                                         i.Value == Hodnota.Eso) &&
                                                                        (TeamMateIndex == player3 ||                                    //pokud je kolega player2 
                                                                         !_probabilities.PotentialCards(TeamMateIndex).HasX(i.Suit) ||  //nechci z nej nahodou vytlacit 
                                                                         !_probabilities.PotentialCards(opponentIndex).HasA(i.Suit)) && //pripadnou trumfovou x
                                                                        hands[MyIndex].CardCount(_trump) <= 1 &&
                                                                        !hands[MyIndex].HasX(_trump) &&
                                                                        hands[MyIndex].SuitCount < Game.NumSuits &&
                                                                        (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                            .Where(b => b != _trump)
                                                                            .Any(b => (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Eso)) >= 1 - _epsilon ||
                                                                                       _probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Desitka)) >= 1 - _epsilon) &&
                                                                                     _probabilities.PotentialCards(opponentIndex).Count(j => j.Suit == i.Suit &&
                                                                                                                                             j.Value < Hodnota.Desitka) > 1) ||
                                                                         _probabilities.CertainCards(opponentIndex).Any(j => j.Suit == i.Suit &&
                                                                                                                             j.Value < Hodnota.Desitka))))
                                {
                                    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                        i.Value < Hodnota.Desitka &&
                                                                                        (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0 ||
                                                                                         _probabilities.PotentialCards(TeamMateIndex).Any(j => j.Suit == i.Suit &&
                                                                                                                                               j.Value > i.Value &&
                                                                                                                                               j.Value < Hodnota.Desitka))).ToList();
                                }
                            }
                        }

                        if (cardsToPlay.Any(i => i.Suit == _trump))
                        {
                            return cardsToPlay.OrderByDescending(i => i.Value)
                                              .FirstOrDefault();
                        }
                        return cardsToPlay.OrderBy(i => i.Value)
                                          .FirstOrDefault();
                    }
                    //if (TeamMateIndex != -1 &&
                    //    (_gameType & Hra.SedmaProti) != 0 &&
                    //    !hands[MyIndex].Has7(_trump))
                    //{
                    //    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                    //                                                            i.Value < Hodnota.Desitka &&
                    //                                                            hands[MyIndex].CardCount(_trump) == 1 &&
                    //                                                            hands[MyIndex].Any(j => j.Value >= Hodnota.Desitka))
                    //                                                .ToList();

                    //    return cardsToPlay.FirstOrDefault();
                    //}
                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 11,
                Description = "hrát spoluhráčovu barvu",
                SkipSimulations = true,
                #region ChooseCard1 Rule11
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex != -1 && _teamMatesSuits.Any())
                    {
                        var opponent = TeamMateIndex == player2 ? player3 : player2;
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                _teamMatesSuits.Contains(i.Suit) &&
                                                                                (i.Value >= Hodnota.Desitka ||
                                                                                 (SevenValue > GameValue &&
                                                                                  (PlayerBids[TeamMateIndex] & Hra.Sedma) != 0) ||
                                                                                 (_probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) > 1 - RiskFactor &&
                                                                                  _probabilities.HasSolitaryX(TeamMateIndex, i.Suit, RoundNumber) < SolitaryXThreshold)) &&
                                                                                _probabilities.SuitProbability(opponent, _trump, RoundNumber) > 0 &&
                                                                                _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) > 0);

                        if (cardsToPlay.Any(i => i.Value < Hodnota.Desitka))
                        {
                            cardsToPlay = cardsToPlay.Where(i => i.Value < Hodnota.Desitka);
                        }
                        if (SevenValue > GameValue &&
                            (PlayerBids[TeamMateIndex] & Hra.Sedma) != 0)
                        {
                            return cardsToPlay.OrderByDescending(i => i.Value)
                                              .ThenByDescending(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
                                              .FirstOrDefault();
                        }
                        cardsToPlay = cardsToPlay.Where(i => i.Value < Hodnota.Desitka);

                        return cardsToPlay.OrderByDescending(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
                                          .ThenBy(i => i.Value)
                                          .FirstOrDefault();
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 12,
                Description = "hrát dlouhou barvu mimo A,X,trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule12
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex == -1 &&
                        (_gameType & Hra.Kilo) != 0 &&
                         _hands[MyIndex].Any(i => i.Value == Hodnota.Desitka &&
                                                  myInitialHand.CardCount(i.Suit) == 1))
                    {
                        return null;
                    }
                    //var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                    //                                             .Where(h => h > i.Value)
                    //                                             .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                    //                                                       _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0))
                    //                             .ToList();
                    //if (TeamMateIndex != -1 &&
                    //    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    //        .Where(b => hands[MyIndex].HasSuit(b))
                    //        .All(b => topCards.HasSuit(b)))
                    //{
                    //    return null;
                    //}
                    //nehraj pravidlo pokud mas malo trumfu a nejaky esa, pokus se zustat ve stychu
                    if (TeamMateIndex == -1 &&
                        (_gameType & Hra.Kilo) == 0 &&
                        myInitialHand.CardCount(_trump) <= 4 &&
                        myInitialHand.Count(i => i.Value == Hodnota.Eso &&
                                                 myInitialHand.CardCount(i.Suit) > 1) >= 2 && //jen pokud eso neni samotne (samotne eso neni dobre hrat)
                        _hands[MyIndex].Any(i => i.Value == Hodnota.Eso &&
                                                 _hands[MyIndex].CardCount(i.Suit) > 1))
                    {
                        return null;
                    }
                    //nehraj pravidlo pokud mas malo trumfu a nejaky esa, pokus se odmazat trumf
                    if (TeamMateIndex == -1 &&
                        (_gameType & Hra.Kilo) != 0 &&
                        myInitialHand.CardCount(_trump) <= 2 &&
                        myInitialHand.Count(i => i.Value >= Hodnota.Desitka &&
                                                 i.Suit != _trump) >= 2 && //jen pokud eso neni samotne (samotne eso neni dobre hrat)
                        _hands[MyIndex].Any(i => i.Value >= Hodnota.Desitka))
                    {
                        return null;
                    }
                    //nehraj pravidlo pokud spoluhrac hlasil sedmu proti - bodovane karty budeme mazat
                    if (TeamMateIndex != -1 &&
                        (_gameType & Hra.SedmaProti) != 0 &&
                        _probabilities.CardProbability(TeamMateIndex, new Card(_trump, Hodnota.Sedma)) == 1)
                    {
                        return null;
                    }
                    var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                 .Where(h => h > i.Value)
                                                                 .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                           _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0));
                    //pokud mas same nejvyssi karty a trumfove eso bez desitky, tak pravidlo nehraj (zkus nejdriv vytahnout trumfy)
                    if (TeamMateIndex == -1 &&
                        hands[MyIndex].HasA(_trump) &&
                        (_probabilities.CardProbability(player2, new Card(_trump, Hodnota.Desitka)) > 0 ||
                         _probabilities.CardProbability(player3, new Card(_trump, Hodnota.Desitka)) > 0) &&
                        hands[MyIndex].Where(i => i.Suit != _trump)
                                      .All(i => topCards.Contains(i)) &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump &&
                                        hands[MyIndex].HasSuit(b))
                            .All(b => topCards.Any(j => j.Suit == b &&
                                                        j.Value >= Hodnota.Desitka)))
                    {
                        return null;
                    }
                    if ((_gameType & Hra.SedmaProti) != 0 &&
                        hands[MyIndex].Has7(_trump))
                    {
                        var opponent = TeamMateIndex == player2 ? player3 : player2;
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            i.Value != Hodnota.Eso &&
                                                                            i.Value != Hodnota.Desitka)
                                                                    .ToList();

                        if (cardsToPlay.Any(i => _preferredSuits.Contains(i.Suit) &&
                                                 !_bannedSuits.Contains(i.Suit)))
                        {
                            cardsToPlay = cardsToPlay.Where(i => _preferredSuits.Contains(i.Suit) &&
                                                                 !_bannedSuits.Contains(i.Suit)).ToList();
                        }

                        return cardsToPlay.OrderBy(i => _probabilities.SuitProbability(opponent, i.Suit, RoundNumber))
                                          .ThenBy(i => i.Value)
                                          .FirstOrDefault();
                    }

                    if (TeamMateIndex == -1)
                    {
                        //c--
                        if ((_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 ||
                             _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0) &&
                            //(_gameType & Hra.Kilo) == 0 &&              //tohle pravidlo nehraju pri kilu
                            (((_gameType & Hra.Sedma)!=0 &&             //pokud hraju sedmu tak se pokusim uhrat A,X nize 
                              hands[MyIndex].CardCount(_trump) > 1) ||  //a dalsi karty pripadne hrat v ramci "hrat cokoli mimo A,X,trumf a dalsich"
                             (hands[MyIndex].CardCount(_trump) > 0)))   //to same pokud jsem volil, sedmu nehraju a uz nemam zadny trumf v ruce
                        {
                            var suits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .OrderBy(b => hands[MyIndex].Count(i => i.Suit == b &&
                                                                                    (i.Value == Hodnota.Eso ||
                                                                                     i.Value == Hodnota.Desitka)))
                                            .ThenBy(b => Math.Min(_probabilities.SuitProbability(player2, b, RoundNumber),
                                                                  _probabilities.SuitProbability(player3, b, RoundNumber)))
                                            .ThenBy(b => hands[MyIndex].CardCount(b))
                                            .Where(b => b != _trump &&
                                                        !_bannedSuits.Contains(b) &&
                                                        myInitialHand.CardCount(b) > 3 &&
                                                        ValidCards(hands[MyIndex]).Any(i => i.Suit == b &&
                                                                                            i.Value != Hodnota.Eso &&
                                                                                            i.Value != Hodnota.Desitka &&
                                                                                            !(_probabilities.SuitHigherThanCardProbability(player2, i, RoundNumber) > 0 &&
                                                                                              _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0 &&
                                                                                              _probabilities.PotentialCards(player3).Any(j => j.Value >= Hodnota.Desitka) &&
                                                                                            !(_probabilities.SuitHigherThanCardProbability(player3, i, RoundNumber) > 0 &&
                                                                                              _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0 &&
                                                                                              _probabilities.PotentialCards(player2).Any(j => j.Value >= Hodnota.Desitka)))));
                            if (suits.Any())
                            {
                                var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == suits.First() &&
                                                                                        i.Value != Hodnota.Desitka &&
                                                                                        i.Value != Hodnota.Eso);
                                var cardToPlay = cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();

                                //pravidlo nehraj pokud hraju sedmu, souperi maji vetsi kartu v barve, kterou chci hrat
                                //a zbyva jim jeden trumf a navic maji barvu, kterou neznam,
                                //protoze v devatem kole by me o trumf pripravili a v poslednim kole mi sedmu zabili trumfem
                                if ((_gameType & Hra.Sedma) != 0 &&
                                    RoundNumber == 8 &&
                                    hands[MyIndex].CardCount(_trump) == 2)
                                {
                                    var trumpsLeft = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                         .Select(i => new Card(_trump, i))
                                                         .Where(i => _probabilities.CardProbability(player2, i) > _epsilon ||
                                                                     _probabilities.CardProbability(player3, i) > _epsilon);

                                    if(trumpsLeft.Count() == 1 &&
                                       Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                           .Where(h => h > cardToPlay.Value)
                                           .Select(h => new Card(cardToPlay.Suit, h))
                                           .Any(i => (_probabilities.CardProbability(player2, i) > _epsilon &&
                                                      Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                           .Any(b => _probabilities.SuitProbability(player2, b, RoundNumber) > 0)) ||
                                                     (_probabilities.CardProbability(player3, i) > _epsilon &&
                                                      Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                           .Any(b => _probabilities.SuitProbability(player3, b, RoundNumber) > 0))))
                                    {
                                        return null;
                                    }
                                }

                                return cardToPlay;
                            }
                        }
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //co-
                        if (_probabilities.SuitProbability(player3, _trump, RoundNumber) > 0)
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    !_bannedSuits.Contains(i.Suit) &&
                                                                                    myInitialHand.CardCount(i.Suit) > 3 &&
                                                                                    !(hands[MyIndex].HasSuit(_trump) &&
                                                                                      (hands[MyIndex].HasA(i.Suit) ||
                                                                                       hands[MyIndex].HasX(i.Suit))) &&
                                                                                    _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) > 1 - RiskFactor &&
                                                                                    !(Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                          .Select(h => new Card(i.Suit,h))
                                                                                          .Count(j => _probabilities.CardProbability(TeamMateIndex, j) > _epsilon) <= 1 &&
                                                                                      _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) > 0) &&
                                                                                    i.Value != Hodnota.Eso &&
                                                                                    i.Value != Hodnota.Desitka &&
                                                                                    _probabilities.HasAOrXAndNothingElse(TeamMateIndex, i.Suit, RoundNumber) < RiskFactor)
                                                                        .ToList();
                            if (cardsToPlay.Any(i => _teamMatesSuits.Contains(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => _teamMatesSuits.Contains(i.Suit)).ToList();
                            }
                            var teamMatesLikelyAXPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                               .ToDictionary(b => b,
                                                                             b => _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) > 0
                                                                                    ? (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Eso)) > _epsilon ? 1 : 0) +
                                                                                      (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Desitka)) > _epsilon ? 1 : 0)
                                                                                    : int.MaxValue);
                            var teamMatesLikelyHigherCards = cardsToPlay.ToDictionary(k => k, v =>
                                                                Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()                                                                    
                                                                    .Where(h => h > v.Value &&
                                                                                h <= Hodnota.Kral)
                                                                    .Select(h => new Card(v.Suit, h))
                                                                    .Count(i => _probabilities.CardProbability(TeamMateIndex, i) > 0));

                            if ((_gameType & Hra.Sedma) != 0)
                            {
                                return cardsToPlay.Where(i => teamMatesLikelyHigherCards[i] > 0)
                                                  .OrderBy(i => teamMatesLikelyAXPerSuit[i.Suit])
                                                  .ThenBy(i => _probabilities.SuitProbability(player3, i.Suit, RoundNumber))
                                                  .ThenByDescending(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
                                                  .ThenBy(i => i.Value)
                                                  .FirstOrDefault();
                            }
                            return cardsToPlay.Where(i => teamMatesLikelyHigherCards[i] > 0)
                                              .OrderByDescending(i => teamMatesLikelyHigherCards[i])
                                              .ThenBy(i => teamMatesLikelyAXPerSuit[i.Suit])
                                              .ThenByDescending(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
                                              .ThenBy(i => i.Value)
                                              .FirstOrDefault();
                        }
                    }
                    else
                    {
                        //c-o
                        if (_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0)
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value != Hodnota.Eso &&
                                                                                    i.Value != Hodnota.Desitka &&
                                                                                    !(hands[MyIndex].HasSuit(_trump) &&
                                                                                      (hands[MyIndex].HasA(i.Suit) ||
                                                                                       hands[MyIndex].HasX(i.Suit))) &&
                                                                                    myInitialHand.CardCount(i.Suit) > 3 &&
                                                                                    !(Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                          .Select(h => new Card(i.Suit, h))
                                                                                          .Count(j => _probabilities.CardProbability(TeamMateIndex, j) > _epsilon) <= 1 &&
                                                                                      _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) > 0) &&
                                                                                    _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) > 1 - RiskFactor &&
                                                                                    !_bannedSuits.Contains(i.Suit) &&
                                                                                    _probabilities.HasAOrXAndNothingElse(TeamMateIndex, i.Suit, RoundNumber) < RiskFactor)
                                                                        .ToList();
                            if (cardsToPlay.Any(i => hands[MyIndex].Where(j => j.Suit == i.Suit)
                                                                   .All(j => j.Value != Hodnota.Eso &&
                                                                             j.Value != Hodnota.Desitka) &&
                                                     _probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor))
                            {
                                cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].Where(j => j.Suit == i.Suit)
                                                                                   .All(j => j.Value != Hodnota.Eso &&
                                                                                             j.Value != Hodnota.Desitka) &&
                                                                     _probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor).ToList();
                            }
                            if (cardsToPlay.Any(i => _teamMatesSuits.Contains(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => _teamMatesSuits.Contains(i.Suit)).ToList();
                            }
                            var teamMatesLikelyAXPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                               .ToDictionary(b => b,
                                                                             b => _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) > 0
                                                                                    ? (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Eso)) > _epsilon ? 1 : 0) +
                                                                                      (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Desitka)) > _epsilon ? 1 : 0)
                                                                                    : int.MaxValue);
                            var teamMatesLikelyHigherCards = cardsToPlay.ToDictionary(k => k, v =>
                                                                Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                    .Where(h => h > v.Value &&
                                                                                h <= Hodnota.Kral)
                                                                    .Select(h => new Card(v.Suit, h))
                                                                    .Count(i => _probabilities.CardProbability(TeamMateIndex, i) > 0));
                            if ((_gameType & Hra.Sedma) != 0)
                            {
                                return cardsToPlay.Where(i => teamMatesLikelyHigherCards[i] > 0)
                                                  .OrderBy(i => teamMatesLikelyAXPerSuit[i.Suit])
                                                  .ThenBy(i => _probabilities.SuitProbability(player2, i.Suit, RoundNumber))
                                                  .ThenByDescending(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
                                                  .ThenBy(i => i.Value)
                                                  .FirstOrDefault();
                            }
                            return cardsToPlay.OrderBy(i => teamMatesLikelyAXPerSuit[i.Suit])
                                              .ThenByDescending(i => hands[MyIndex].CardCount(i.Suit))//_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
                                              .ThenByDescending(i => i.Value)
                                              .FirstOrDefault();
                        }
                    }

                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 13,
                Description = "obětuj plonkovou X",
                SkipSimulations = true,
                #region ChooseCard1 Rule13
                ChooseCard1 = () =>
                {
                    if ((TeamMateIndex == -1 &&
                         (((_gameType & Hra.Sedma) != 0 &&           //pokud jsem volil a hraju sedmu a mam ji jako posledni trumf, tak se pokusim uhrat A,X nize 
                           hands[MyIndex].CardCount(_trump) > 1) ||  //a dalsi karty pripadne hrat v ramci "hrat cokoli mimo A,X,trumf a dalsich"
                          (hands[MyIndex].CardCount(_trump) > 0))) ||
                        (TeamMateIndex != -1 &&
                         (((_gameType & Hra.SedmaProti) != 0 &&      //pokud jsem nevolil a hraju sedmu proti a mam ji jako posledni trumf, tak se pokusim uhrat A,X nize 
                           hands[MyIndex].Has7(_trump) &&
                           hands[MyIndex].CardCount(_trump) > 1) ||  //a dalsi karty pripadne hrat v ramci "hrat cokoli mimo A,X,trumf a dalsich"
                          (hands[MyIndex].CardCount(_trump) > 0))))  //to same pokud jsem volil, sedmu nehraju a uz nemam zadny trumf v ruce
                    {
                        var topCards = hands[MyIndex].Count(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Where(h => h > i.Value)
                                                                     .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                               _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0));
                        var solitaryXs = Enum.GetValues(typeof(Barva)).Cast<Barva>()
						                     .Where(b => b != _trump &&
						                            hands[MyIndex].HasSolitaryX(b) &&                                      //plonkova X
                                                    (_probabilities.CardProbability(player2, new Card(b, Hodnota.Eso)) > 0 ||   //aby byla plonkova tak musi byt A jeste ve hre
                                                     _probabilities.CardProbability(player3, new Card(b, Hodnota.Eso)) > 0))
                                             .Select(b => new Card(b, Hodnota.Desitka))
                                             .ToList();
                        var totalCards = 10 - RoundNumber + 1;

                        if ((solitaryXs.Count > 0 &&
                             topCards == totalCards - solitaryXs.Count) ||
                            (_gameType & Hra.Kilo) != 0)
                        {
                            return solitaryXs.RandomOneOrDefault();
                        }
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 14,
                Description = "hrát vítěznou kartu",
                SkipSimulations = true,
                #region ChooseCard1 Rule14
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

                    //nehraj pravidlo pokud spoluhrac hlasil sedmu proti - bodovane karty budeme mazat
                    if (TeamMateIndex != -1 &&
                        (_gameType & Hra.SedmaProti) != 0 &&
                        _probabilities.CardProbability(TeamMateIndex, new Card(_trump, Hodnota.Sedma)) == 1)
                    {
                        return null;
                    }
                    if (TeamMateIndex == -1)
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => (i.Suit != _trump &&            //trumfu se zbytecne nezbavovat
                                                                             i.Value != Hodnota.Eso) &&
                                                                            Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                .Where(h => h > i.Value)
                                                                                .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0) &&
                                                                            (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 1 ||
                                                                             _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                            Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                .Where(h => h > i.Value)
                                                                                .All(h => _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0) &&
                                                                            (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1 ||
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0)).ToList();

                    }
                    else if (TeamMateIndex == player2)
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => (i.Suit != _trump &&
                                                                             i.Value != Hodnota.Eso) &&
                                                                            Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                .Where(h => h > i.Value)
                                                                                .All(h => _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0) &&
                                                                            (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 ||
                                                                             _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&  //netahat zbytecne trumfy ze spoluhrace
                                                                            (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1 ||
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0)).ToList();
                    }
                    else
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => (i.Suit != _trump &&
                                                                             i.Value != Hodnota.Eso) &&
                                                                            Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                .Where(h => h > i.Value)
                                                                                .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0) &&
                                                                            (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 ||  //netahat zbytecne trumfy ze spoluhrace
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                            (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 1 ||
                                                                             _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0)).ToList();
                    }
                    return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 15,
                Description = "hrát vítězné A",
                SkipSimulations = true,
                #region ChooseCard1 Rule15
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

                    //nehraj pravidlo pokud spoluhrac hlasil sedmu proti - bodovane karty budeme mazat
                    if (TeamMateIndex != -1 &&
                        (_gameType & Hra.SedmaProti) != 0 &&
                        _probabilities.CardProbability(TeamMateIndex, new Card(_trump, Hodnota.Sedma)) == 1)
                    {
                        return null;
                    }
                    if (TeamMateIndex == -1)
                    {
                        var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Where(h => h > i.Value)
                                                                     .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                               _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0)).ToList();

                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            i.Suit != _trump &&
                                                                            ((_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) == 0 &&
                                                                              _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) == 0) ||
                                                                             Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                 .Where(b => hands[MyIndex].HasSuit(b))
                                                                                 .All(b => topCards.Any(j => j.Suit == b))) &&
                                                                            (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 1 ||
                                                                             _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                            (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1 ||
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0)).ToList();
                    }
                    else if (TeamMateIndex == player2)
                    {
                        var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Where(h => h > i.Value)
                                                                     .All(h => _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0)).ToList();

                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            i.Suit != _trump &&
                                                                            _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon &&
                                                                            (_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) == 0 ||
                                                                             Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                 .Where(b => hands[MyIndex].HasSuit(b))
                                                                                 .All(b => topCards.Any(j => j.Suit == b))) &&
                                                                            (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 ||  //netahat zbytecne trumfy ze spoluhrace
                                                                             _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                            (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1 ||
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0)).ToList();
                    }
                    else
                    {
                        var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Where(h => h > i.Value)
                                                                     .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                               _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0)).ToList();

                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            i.Suit != _trump &&
                                                                            _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon &&
                                                                            (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) == 0 ||
                                                                             Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                 .Where(b => hands[MyIndex].HasSuit(b))
                                                                                 .All(b => topCards.Any(j => j.Suit == b))) &&
                                                                            (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 ||  //netahat zbytecne trumfy ze spoluhrace
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                            (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 1 ||
                                                                             _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0)).ToList();
                    }
                    return cardsToPlay.RandomOneOrDefault();
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 16,
                Description = "zůstat ve štychu",
                SkipSimulations = true,
                #region ChooseCard1 Rule16
                ChooseCard1 = () =>
                {
                    var opponent = TeamMateIndex == player2 ? player3 : player2;

                    if (TeamMateIndex != -1)
                    {
                        //dame sanci spoluhraci aby namazal
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                i.Value != Hodnota.Eso &&
                                                                                i.Value != Hodnota.Desitka &&
                                                                                _probabilities.SuitProbability(opponent, i.Suit, RoundNumber) > 0 &&
                                                                                (_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) > 0 ||
                                                                                 _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) == 0) &&
                                                                                Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                    .Select(h => new Card(i.Suit, h))
                                                                                    .Where(j => _probabilities.CardProbability(opponent, j) > _epsilon)
                                                                                    .All(j => j.Value < i.Value));

                        return cardsToPlay.OrderBy(i => _probabilities.CardProbability(opponent, new Card(i.Suit, Hodnota.Desitka)) > _epsilon ? 0 : 1)
                                          .ThenByDescending(i => _probabilities.SuitProbability(opponent, i.Suit, RoundNumber))
                                          .ThenByDescending(i => i.Value).FirstOrDefault();
                    }
                    else
                    {
                        //pokud v osmem kole mam dva trumfy a souperum zbyva jeden trumf a muj druhy trumf je vetsi, tak ho zahraj
                        //protoze jinak by me v devatem kole o trumf pripravili a v poslednim kole by mi zbyvajici trumf mohli prebit
                        if (RoundNumber == 8 &&
                            hands[MyIndex].CardCount(_trump) == 2)
                        {
                            var nonTrumpCard = hands[MyIndex].First(i => i.Suit != _trump);
                            var trumpsLeft = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                 .Select(i => new Card(_trump, i))
                                                 .Where(i => _probabilities.CardProbability(player2, i) > _epsilon ||
                                                             _probabilities.CardProbability(player3, i) > _epsilon);

                            if (trumpsLeft.Count() == 1 &&
                                hands[MyIndex].Where(i => i.Suit == _trump)
                                              .Any(i => i.Value > trumpsLeft.First().Value))
                            {
                                return ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump)
                                                                 .OrderByDescending(i => i.Value)
                                                                 .FirstOrDefault();
                            }
                        }
                        var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Where(h => h > i.Value)
                                                                     .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                               _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0));
                        //pokud mas same nejvyssi karty a trumfove eso bez desitky, tak pravidlo nehraj (zkus nejdriv vytahnout trumfy)
                        if (hands[MyIndex].HasA(_trump) &&
                            (_probabilities.CardProbability(player2, new Card(_trump, Hodnota.Desitka)) > 0 ||
                             _probabilities.CardProbability(player3, new Card(_trump, Hodnota.Desitka)) > 0) &&
                            hands[MyIndex].Where(i => i.Suit != _trump)
                                          .All(i => topCards.Contains(i)) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b))
                                .All(b => topCards.Any(j => j.Suit == b &&
                                                            j.Value >= Hodnota.Desitka)))
                        {
                            return null;
                        }
                        //pokud mas barvu jen s plivama tak se jich nejdriv zbav
                        if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => !_hands[MyIndex].HasA(b) &&
                                          !_hands[MyIndex].HasX(b)))
                        {
                            return null;
                        }
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                i.Value != Hodnota.Eso &&
                                                                                i.Value != Hodnota.Desitka &&
                                                                                _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 &&
                                                                                Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                    .Select(h => new Card(i.Suit, h))
                                                                                    .Where(j => _probabilities.CardProbability(player2, j) > _epsilon ||
                                                                                                _probabilities.CardProbability(player3, j) > _epsilon)
                                                                                    .All(j => j.Value < i.Value));

                        return cardsToPlay.OrderBy(i => _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) > _epsilon ||
                                                        _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) > _epsilon ? 0 : 1)
                                          .ThenByDescending(i => i.Value).FirstOrDefault();
                    }
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 17,
                Description = "hrát největší trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule17
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex != -1 &&
                        (_gameType & Hra.Sedma) != 0 &&
                        (_gameType & Hra.Hra) != 0 &&
                        SevenValue > GameValue)
                    {
                        return null;
                    }
                    var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                                    ? (MyIndex + 2) % Game.NumPlayers
                                    : (MyIndex + 1) % Game.NumPlayers;

                    if (TeamMateIndex != -1 &&
                        hands[MyIndex].Any(i => i.Suit != _trump &&
                                                !hands[MyIndex].HasA(i.Suit) &&
                                                !hands[MyIndex].HasX(i.Suit) &&
                                                _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0 &&
                                                _probabilities.SuitProbability(opponent, i.Suit, RoundNumber) < 1))
                    {
                        return null;
                    }

                    if (TeamMateIndex == -1 && 
                        (_gameType & Hra.Kilo) != 0 &&
                        hands[MyIndex].CardCount(_trump) > 1 &&
                        (_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 ||
                         _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0))
                    {
                        //pokud ale mas plonkovou X, tak pravidlo nehraj (nejprv se zbav plonkove X)
                        if (hands[MyIndex].Any(i => i.Suit != _trump &&
                                                    i.Value == Hodnota.Desitka &&
                                                    hands[MyIndex].CardCount(i.Suit) == 1 &&
                                                    (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) > 0 ||
                                                     _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > 0)))
                        {
                            return null;
                        }
                        var opponentTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                 .Where(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > 0 ||
                                                             _probabilities.CardProbability(player3, new Card(_trump, h)) > 0)
                                                 .ToList();

                        //nehraj pokud maji souperi vic trumfu nez ja
                        //popr. pokud jeden trumfy nezna, druhy jich ma trumfu stejne jako ja, ale moje trumfy nejsou nejvyssi
                        if (hands[MyIndex].CardCount(_trump) < opponentTrumps.Count() ||
                            (hands[MyIndex].CardCount(_trump) == opponentTrumps.Count() &&
                             (_probabilities.SuitProbability(player2, _trump, RoundNumber) == 0 ||
                              _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                              opponentTrumps.Any(i => hands[MyIndex].Where(j => j.Suit == _trump)
                                                                    .Any(j => i > j.Value))))
                        {
                            return null;
                        }
                        //nehraj pokud mas dlouhou netrumfovou barvu ve ktere muzes zkusit vytlacit trumf
                        var longSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Where(b => b != _trump &&
                                                        hands[MyIndex].CardCount(b) >= 5)
                                            .ToList();
                        if (longSuits.Any())
                        {
                            return null;
                        }
                        if (RoundNumber <= 4 &&                                 //na pocatku hry
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()         //pokud mas nejakeho plonka, tak pravidlo nehraj
                                .Where(b => b != _trump)
                                .Any(b => hands[MyIndex].HasSuit(b) &&
                                          !hands[MyIndex].HasA(b) &&
                                          !hands[MyIndex].HasX(b) &&
                                          (_probabilities.CardProbability(player2, new Card(b, Hodnota.Eso)) > _epsilon ||
                                           _probabilities.CardProbability(player3, new Card(b, Hodnota.Eso)) > _epsilon ||
                                           _probabilities.CardProbability(player2, new Card(b, Hodnota.Desitka)) > _epsilon ||
                                           _probabilities.CardProbability(player3, new Card(b, Hodnota.Desitka)) > _epsilon)))
                        {
                            return null;
                        }
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                ((_probabilities.CardProbability(player2, new Card(_trump, Hodnota.Eso)) == 0 &&
                                                                                  _probabilities.CardProbability(player3, new Card(_trump, Hodnota.Eso)) == 0) ||
                                                                                 i.Value < Hodnota.Desitka));
                                                                                  
                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }

                    var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                 .Where(h => h > i.Value)
                                                                 .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                           _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0)).ToList();
                    var opponentIndex = Enumerable.Range(0, Game.NumPlayers).First(i => i != MyIndex && i != TeamMateIndex);

                    //pokud mas nevyssi trumfy a nemas co mazat ale kolega asi ma co mazat, hraj ho
                    if (TeamMateIndex != -1 &&
                        topCards.HasSuit(_trump) &&
                        _probabilities.PotentialCards(TeamMateIndex).Any(i => i.Value >= Hodnota.Desitka) &&
                        (Enum.GetValues(typeof(Barva)).Cast<Barva>()    //pokud maji akter a kolega barvu kterou neznam, je sance, ze kolega namaze od 3. barvy
                             .Where(b => b != _trump)
                             .Any(b => !hands[MyIndex].HasSuit(b) &&
                                       _probabilities.PotentialCards(opponentIndex).HasSuit(b) &&
                                       _probabilities.PotentialCards(TeamMateIndex).HasSuit(b)) ||
                        (hands[MyIndex].CardCount(_trump) == 1 &&
                         hands[MyIndex].HasA(_trump) &&
                         ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 ||
                          (_gameType & Hra.Hra) == 0 ||
                          SevenValue < GameValue) &&
                         _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) == 0 &&
                         _probabilities.PotentialCards(TeamMateIndex).Any(i => i.Value >= Hodnota.Desitka &&
                                                                               i.Suit != _trump)) ||
                         ((hands[MyIndex].Where(i => i.Suit != _trump)
                                         .All(i => i.Value < Hodnota.Desitka) ||
                           hands[MyIndex].Where(i => i.Suit != _trump).SuitCount() == 3) &&
                          (hands[MyIndex].Where(i => i.Suit != _trump)
                                         .All(i => topCards.HasSuit(i.Suit)) ||
                           _rounds == null || //pokud v minulych kolech kolega na tvuj nejvyssi trumf nenamazal, tak pravidlo nehraj (kolega nema co mazat). Trumfy setri na pozdeji
                           !_rounds.Any(r => r?.c3 != null &&
                                             r.player1.PlayerIndex == MyIndex &&
                                             r.c1.Suit == _trump &&
                                             !((r.player2.PlayerIndex == TeamMateIndex &&
                                                r.c2.Suit != _trump &&
                                                r.c2.Value < Hodnota.Desitka) ||
                                               (r.player3.PlayerIndex == TeamMateIndex &&
                                                r.c3.Suit != _trump &&
                                                r.c3.Value < Hodnota.Desitka)) &&
                                             r.roundWinner.PlayerIndex == MyIndex)))))
                    {
                        return ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump).OrderByDescending(i => i.Suit).FirstOrDefault();
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 18,
                Description = "zbavit se plev",
                SkipSimulations = true,
                #region ChooseCard1 Rule18
                ChooseCard1 = () =>
                {
                    var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                 .Where(h => h > i.Value)
                                                                 .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                           _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0)).ToList();
                    if (topCards.Any())
                    {
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                i.Value != Hodnota.Eso &&
                                                                                i.Value != Hodnota.Desitka &&
                                                                                !(hands[MyIndex].HasX(i.Suit) &&
                                                                                  hands[MyIndex].CardCount(i.Suit) == 2) &&
                                                                                !topCards.Contains(i)).ToList();
                        //pokud jsi nevolil: nezbabuj se plev od barev kde mas A nebo X pokud mas trumfy
                        if (TeamMateIndex != -1)
                        {
                            cardsToPlay = cardsToPlay.Where(i => !(hands[MyIndex].HasSuit(_trump) &&
                                                                   (hands[MyIndex].HasA(i.Suit) ||
                                                                    hands[MyIndex].HasX(i.Suit)))).ToList();
                        }
                        //nezbavuj se plev od barev ve kterych mam eso a souper ma desitku
                        if (TeamMateIndex == -1)
                        {
                            cardsToPlay = cardsToPlay.Where(i => !(hands[MyIndex].HasA(i.Suit) &&
                                                                   (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) > _epsilon ||
                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) > _epsilon))).ToList();
                        }
                        else if (TeamMateIndex == player2)
                        {
                            cardsToPlay = cardsToPlay.Where(i => !(hands[MyIndex].HasA(i.Suit) &&
                                                                   _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) > _epsilon)).ToList();
                        }
                        else
                        {
                            cardsToPlay = cardsToPlay.Where(i => !(hands[MyIndex].HasA(i.Suit) &&
                                                                   _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) > _epsilon)).ToList();
                        }

                        if (TeamMateIndex != -1)
                        {
                            var opponentIndex = Enumerable.Range(0, Game.NumPlayers).First(i => i != MyIndex && i != TeamMateIndex);

                            if (cardsToPlay.Any(i => _teamMatesSuits.Contains(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => _teamMatesSuits.Contains(i.Suit)).ToList();
                            }

                            //neobetuj karty kterymi bych ze spoluhrace vytahl A,X ktery by souper vzal trumfem
                            cardsToPlay = cardsToPlay.Where(i => !_bannedSuits.Contains(i.Suit) &&
                                                                 ((TeamMateIndex == player2 &&
                                                                   _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor) ||
                                                                  (TeamMateIndex == player3 &&
                                                                   _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor)) &&
                                                          (_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                           _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) <= RiskFactor ||
                                                           _probabilities.SuitProbability(opponentIndex, _trump, RoundNumber) <= RiskFactor) &&
                                                          ((_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) < 1 - _epsilon &&
                                                            _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon) ||
                                                           _probabilities.SuitProbability(opponentIndex, i.Suit, RoundNumber) > 0 ||
                                                           _probabilities.SuitProbability(opponentIndex, _trump, RoundNumber) == 0))
                                                     .ToList();
                            //uprednostni barvy, kde neznam A,X
                            if (cardsToPlay.Any(i => hands[MyIndex].HasA(i.Suit) ||
                                                     hands[MyIndex].HasX(i.Suit)) &&
                                cardsToPlay.Any(i => !hands[MyIndex].HasA(i.Suit) &&
                                                     !hands[MyIndex].HasX(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => !hands[MyIndex].HasA(i.Suit) &&
                                                                     !hands[MyIndex].HasX(i.Suit))
                                                         .ToList();
                            }
                            if (SevenValue > GameValue &&                           //pri sedme se nezbavuj plonka (abych si udrzel trumfy)
                                hands[MyIndex].HasSuit(_trump) &&                   //nebo karty kde bys ztratil tempo
                                hands[MyIndex].Any(i => i.Suit != _trump &&         //pokud muzes uhrat nejake eso
                                                        i.Value == Hodnota.Eso &&
                                                        _probabilities.PotentialCards(opponentIndex).HasSuit(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].CardCount(i.Suit) > 1 &&
                                                                     !_probabilities.LikelyCards(opponentIndex).Any(j => j.Suit == i.Suit &&
                                                                                                                         j.Value > i.Value))
                                                         .ToList();
                            }

                            return cardsToPlay.OrderByDescending(i => TeamMateIndex == player3 &&
                                                                      _probabilities.PotentialCards(player2).Any(j => j.Suit == i.Suit &&
                                                                                                                      j.Value >= Hodnota.Desitka)
                                                                      ? i.Value
                                                                      : 0)
                                              .ThenBy(i => i.Value)
                                              .FirstOrDefault();
                        }
                        //if ((_gameType & Hra.Kilo) == 0)
                        {
                            var xSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                             .Where(b => hands[MyIndex].HasX(b) &&
                                                         (_probabilities.CardProbability(player2, new Card(b, Hodnota.Eso)) > _epsilon ||
                                                          _probabilities.CardProbability(player3, new Card(b, Hodnota.Eso)) > _epsilon))
                                             .ToList();
                            //prednostne se zbav plev od barvy kde mam X a souper ma A
                            if(cardsToPlay.Any(i  => xSuits.Contains(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => xSuits.Contains(i.Suit)).ToList();
                            }
                            if (cardsToPlay.All(i => topCards.Any(j => j.Suit == i.Suit &&
                                                                       j.Value < Hodnota.Desitka)))
                            {
                                //pokud mam v kazde barve kde se muzu zbavit plev i A,X a jeste neco nejvyssiho, tak hraj nejvyssi mimo A,X
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value != Hodnota.Eso &&
                                                                                    i.Value != Hodnota.Desitka &&
                                                                                    topCards.Contains(i))
                                                                        .OrderByDescending(i => i.Value)
                                                                        .Take(1)
                                                                        .ToList();
                            }
                            return cardsToPlay.OrderBy(i => hands[MyIndex].CardCount(i.Suit))
                                              .ThenBy(i => i.Value)
                                              .FirstOrDefault();
                        }
					}
                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 19,
                Description = "hrát trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule19
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex != -1 &&
                        (_gameType & Hra.Sedma) != 0 &&
                        (_gameType & Hra.Hra) != 0 &&
                        SevenValue > GameValue)
                    {
                        return null;
                    }

                    if (TeamMateIndex == player2)
                    {
                        //hraj trumf pokud jsou vsechny ostatni barvy zakazane a zaroven mas v nejake netrumfove barve eso
                        if (_hands[MyIndex].HasSuit(_trump) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Any(b => b != _trump &&
                                            _hands[MyIndex].HasA(b)) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            _hands[MyIndex].HasA(b))
                                .All(b => _bannedSuits.Contains(b)))
                        {
                            var holes = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().Where(h => _probabilities.CardProbability(player3, new Card(_trump, h)) > _epsilon).ToList();
                            var topTrumps = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump && holes.All(h => h < i.Value)).ToList();
                            var trumps = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                               ((i.Value != Hodnota.Desitka &&
                                                                                 !(_hands[MyIndex].HasX(_trump) &&
                                                                                   _hands[MyIndex].CardCount(_trump) == 2) &&
                                                                                 (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon ||
                                                                                  _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor)) ||
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0))
                                                                   .ToList();

                            if (topTrumps.Any())
                            {
                                return trumps.OrderByDescending(i => i.Value).FirstOrDefault();
                            }
                            return trumps.OrderBy(i => i.Value).FirstOrDefault();
                        }
                    }
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Eso &&
                                                                            i.Value != Hodnota.Desitka &&
                                                                            !_bannedSuits.Contains(i.Suit))
                                                                .ToList();

                    if (TeamMateIndex != -1)
                    {
                        var opponent = TeamMateIndex == player2 ? player3 : player2;

                        if (cardsToPlay.All(i => i.Suit == _trump) &&
                            ((_gameType & Hra.Kilo) != 0 ||
                             !(cardsToPlay.Count == 1 && //nehraj pokud ma souper vyssi trumf a jen bys odevzdal tempo
                               _probabilities.PotentialCards(opponent).Any(i => i.Suit == _trump &&
                                                                                i.Value > cardsToPlay.First().Value))))
                        {
                            return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                        }
                    }

                    if (TeamMateIndex != -1 &&
                        PlayerBids[TeamMateIndex] == 0 &&
                        ((PlayerBids[MyIndex] & (Hra.Hra | Hra.Sedma)) != 0))
                    {
                        return null;
                    }
                    if (TeamMateIndex != -1)
                    {
                        //pokud existuje jedina nezakazana netrumfova barva, tak pravidlo nehraj
                        if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Count(b => b != _trump &&
                                            !_bannedSuits.Contains(b) &&
                                            _hands[MyIndex].Any(i => i.Suit == b &&
                                                                     i.Value < Hodnota.Desitka)) == 1)
                        {
                            return null;
                        }
                    }
                    if (TeamMateIndex == player2)
                    {
                        //hraj trumf pokud bys cimkoli jinym mohl vytlacit ze spoluhrace A, X ktere by akter sebral                        
                        if (cardsToPlay.Where(i => i.Suit != _trump)
                                       .All(i => (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) > _epsilon ||
                                                  _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) > _epsilon) &&
                                                 //_probabilities.NoneOfCardsInSuitProbability(TeamMateIndex, i.Suit, RoundNumber, i.Value, Hodnota.Kral) >= RiskFactor &&
                                                 !(_probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor) &&
                                                 (_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > _epsilon ||
                                                  (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) <= 1 - RiskFactor &&
                                                   _probabilities.SuitProbability(player3, _trump, RoundNumber) > 1 - RiskFactor))))
                        {
                            cardsToPlay = cardsToPlay.Where(i => i.Suit == _trump &&
                                                                 !(_hands[MyIndex].HasX(_trump) &&
                                                                   _hands[MyIndex].CardCount(_trump) == 2 &&
                                                                   _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > _epsilon) &&
                                                                 (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon ||
                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) <= _epsilon)).ToList();

                            return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                        }
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule()
			{
				Order = 20,
				Description = "hrát cokoli mimo A,X,trumf",
				SkipSimulations = true,
                #region ChooseCard1 Rule20
                ChooseCard1 = () =>
				{
                    var opponent = TeamMateIndex == player2 ? player3 : player2;
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Eso &&
																			i.Value != Hodnota.Desitka &&
																			i.Suit != _trump &&
																			!_bannedSuits.Contains(i.Suit)).ToList();

                    if (TeamMateIndex == -1)
                    {
                        if ((_gameType & Hra.Kilo) == 0)
                        {
                            return cardsToPlay.OrderBy(i => i.Value)
                                              .FirstOrDefault();
                        }
                        return null;
					}
                    cardsToPlay = cardsToPlay.Where(i => (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                          _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon) ||
                                                         ((TeamMateIndex == player2 &&
                                                           _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor) ||
                                                          (TeamMateIndex == player3 &&
                                                           _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor))).ToList();
                    if (cardsToPlay.Any(i => _teamMatesSuits.Contains(i.Suit)))
                    {
                        cardsToPlay = cardsToPlay.Where(i => _teamMatesSuits.Contains(i.Suit)).ToList();
                    }
                    if (cardsToPlay.Any(i => _hands[MyIndex].CardCount(i.Suit) < 3))
                    {
                        cardsToPlay = cardsToPlay.Where(i => _hands[MyIndex].CardCount(i.Suit) < 3).ToList();
                    }
                    var teamMatesLikelyAXPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                       .ToDictionary(b => b,
                                                                     b => _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) > 0
                                                                            ? (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Eso)) > _epsilon ? 1 : 0) +
                                                                              (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Desitka)) > _epsilon ? 1 : 0)
                                                                            : int.MaxValue);
                    return cardsToPlay.OrderByDescending(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
                                      .ThenBy(i => teamMatesLikelyAXPerSuit[i.Suit])
                                      .ThenByDescending(i => TeamMateIndex == player3 &&
                                                             _probabilities.PotentialCards(player2).Any(j => j.Suit == i.Suit &&
                                                                                                             j.Value >= Hodnota.Desitka)
                                                             ? i.Value
                                                             : 0)
                                      .ThenBy(i => i.Value)
                                      .FirstOrDefault();
				}
                #endregion
            };

            yield return new AiRule()
            {
                Order = 21,
                Description = "hrát cokoli mimo trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule21
                ChooseCard1 = () =>
                {
                    var opponent = TeamMateIndex == player2 ? player3 : player2;
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            !_bannedSuits.Contains(i.Suit) &&
                                                                            i.Value != Hodnota.Eso &&
                                                                            i.Value != Hodnota.Desitka &&
                                                                            (TeamMateIndex == -1 ||
                                                                             (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                              _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon) ||
                                                                             ((TeamMateIndex == player2 &&
                                                                               _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor) ||
                                                                              (TeamMateIndex == player3 &&
                                                                               _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor)))).ToList();
                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            !_bannedSuits.Contains(i.Suit) &&
                                                                            i.Value != Hodnota.Eso &&
                                                                            i.Value != Hodnota.Desitka &&
                                                                            (TeamMateIndex == -1 ||
                                                                             (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                              _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon) ||
                                                                             ((TeamMateIndex == player2 &&
                                                                               _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 0.5) ||
                                                                              (TeamMateIndex == player3 &&
                                                                               _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 0.5)))).ToList();
                    }
                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            !_bannedSuits.Contains(i.Suit) &&
                                                                            i.Value != Hodnota.Eso &&
                                                                            i.Value != Hodnota.Desitka).ToList();
                    }
                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            i.Value != Hodnota.Eso &&
                                                                            i.Value != Hodnota.Desitka &&
                                                                            (TeamMateIndex == -1 ||
                                                                             (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                              _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon) ||
                                                                             ((TeamMateIndex == player2 &&
                                                                               _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor) ||
                                                                              (TeamMateIndex == player3 &&
                                                                               _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor)))).ToList();
                    }
                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            i.Value != Hodnota.Eso &&
                                                                            i.Value != Hodnota.Desitka).ToList();
                    }
                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            !_bannedSuits.Contains(i.Suit)).ToList();
                    }
                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump).ToList();
                    }
                    if (TeamMateIndex == -1)
                    {
                        return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                    }
                    cardsToPlay = cardsToPlay.Where(i => (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                          _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon) ||
                                                         ((TeamMateIndex == player2 &&
                                                           _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor) ||
                                                          (TeamMateIndex == player3 &&
                                                           _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor))).ToList();
                    if (cardsToPlay.Any(i => _teamMatesSuits.Contains(i.Suit)))
                    {
                        cardsToPlay = cardsToPlay.Where(i => _teamMatesSuits.Contains(i.Suit)).ToList();
                    }
                    if (cardsToPlay.Any(i => _hands[MyIndex].CardCount(i.Suit) < 3))
                    {
                        cardsToPlay = cardsToPlay.Where(i => _hands[MyIndex].CardCount(i.Suit) < 3).ToList();
                    }
                    var teamMatesLikelyAXPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                       .ToDictionary(b => b,
                                                                     b => _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) > 0
                                                                            ? (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Eso)) > _epsilon ? 1 : 0) +
                                                                              (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Desitka)) > _epsilon ? 1 : 0)
                                                                            : int.MaxValue);
                    return cardsToPlay.OrderByDescending(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
                                      .ThenBy(i => teamMatesLikelyAXPerSuit[i.Suit])
                                      .ThenByDescending(i => TeamMateIndex == player3 &&
                                                             _probabilities.PotentialCards(player2).Any(j => j.Suit == i.Suit &&
                                                                                                             j.Value >= Hodnota.Desitka)
                                                             ? i.Value
                                                             : 0)
                                      .ThenBy(i => i.Value)
                                      .FirstOrDefault();
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 22,
                Description = "hrát cokoli",
                SkipSimulations = true,
                #region ChooseCard1 Rule22
                ChooseCard1 = () =>
                {
                    var opponent = TeamMateIndex == player2 ? player3 : player2;
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            i.Value < Hodnota.Desitka &&
                                                                            !_bannedSuits.Contains(i.Suit) &&
                                                                            (TeamMateIndex == -1 ||
                                                                             (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                              _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon) ||
                                                                             ((TeamMateIndex == player2 &&
                                                                               (_probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor ||
                                                                                (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                 _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0))) ||
                                                                              (TeamMateIndex == player3 &&
                                                                               _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor)))).ToList();

                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            i.Value < Hodnota.Desitka &&
                                                                            !_bannedSuits.Contains(i.Suit)).ToList();
                    }
                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            i.Value < Hodnota.Desitka).ToList();
                    }
                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).ToList();
                    }
                    if (TeamMateIndex == -1)
                    {
                        return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                    }

                    if (cardsToPlay.Any(i => _teamMatesSuits.Contains(i.Suit)))
                    {
                        cardsToPlay = cardsToPlay.Where(i => _teamMatesSuits.Contains(i.Suit)).ToList();
                    }
                    if (cardsToPlay.Any(i => _hands[MyIndex].CardCount(i.Suit) < 3))
                    {
                        cardsToPlay = cardsToPlay.Where(i => _hands[MyIndex].CardCount(i.Suit) < 3).ToList();
                    }
                    var teamMatesLikelyAXPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                       .ToDictionary(b => b,
                                                                     b => _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) > 0
                                                                            ? (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Eso)) > _epsilon ? 1 : 0) +
                                                                              (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Desitka)) > _epsilon ? 1 : 0)
                                                                            : int.MaxValue);
                    return cardsToPlay.OrderByDescending(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
                                      .ThenBy(i => teamMatesLikelyAXPerSuit[i.Suit])
                                      .ThenByDescending(i => TeamMateIndex == player3 &&
                                                             _probabilities.PotentialCards(player2).Any(j => j.Suit == i.Suit &&
                                                                                                             j.Value >= Hodnota.Desitka)
                                                             ? i.Value
                                                             : 0)
                                      .ThenBy(i => i.Value)
                                      .FirstOrDefault();
                }
                #endregion
            };
        }

        protected override IEnumerable<AiRule> GetRules2(Hand[] hands)
        {
            var player3 = (MyIndex + 1) % Game.NumPlayers;
            var player1 = (MyIndex + 2) % Game.NumPlayers;

            BeforeGetRules23();
            if (RoundNumber == 9)
            {
                yield return new AiRule()
                {
                    Order = 0,
                    Description = "hrát tak abych bral poslední štych",
                    ChooseCard2 = (Card c1) =>
                    {
                        //hrat tak abych bral posledni stych
                        IEnumerable<Card> cardsToPlay;

                        //pokud spoluhrac zahlasil sedmu proti, tak pravidlo nehraj
                        if (TeamMateIndex != -1 &&
                            (_gameType & Hra.SedmaProti) != 0 &&
                             _probabilities.CardProbability(TeamMateIndex, new Card(_trump, Hodnota.Sedma)) == 1)
                        {
                            return null;
                        }
                        //pri kilu pravidlo nehraj (pokusime se namazat, pokud je co)
                        if (TeamMateIndex != -1 &&
                            (_gameType & Hra.Kilo) != 0)
                        {
                            return null;
                        }
                        if (hands[MyIndex].CardCount(_trump) == 2 &&
                            (_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            (Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                .Count(h => (c1.Suit != _trump || c1.Value != h) &&
                                            _probabilities.CardProbability(player1, new Card(_trump, h)) > 0) == 1 ||
                             Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                .Count(h => _probabilities.CardProbability(player1, new Card(_trump, h)) > 0 ||
                                            _probabilities.CardProbability(player3, new Card(_trump, h)) > 0) == 2))
                        {
                            return ValidCards(hands[MyIndex]).OrderBy(i => i.Value).FirstOrDefault();
                        }
                        if (TeamMateIndex == -1)
                        {
                            //: -c-
                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => ValidCards(c1, i, hands[player3]).All(j =>
                            {
                                var i2 = hands[MyIndex].First(l => l != i);
                                var j2 = hands[player3].First(m => m != j);
                                var k2 = hands[player1].First(n => n != c1);

                                var winnerCard = Round.WinningCard(c1, i, j, _trump);

                                if (winnerCard == c1)
                                {
                                    return Round.WinningCard(k2, i2, j2, _trump) == i2;
                                }
                                else if (winnerCard == i)
                                {
                                    return Round.WinningCard(i2, j2, k2, _trump) == i2;
                                }
                                else
                                {
                                    return Round.WinningCard(j2, k2, i2, _trump) == i2;
                                }
                            }));
                        }
                        else if (TeamMateIndex == player3)
                        {
                            //: -co
                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => ValidCards(c1, i, hands[player3]).All(j =>
                            {
                                var i2 = hands[MyIndex].First(l => l != i);
                                var j2 = hands[player3].First(m => m != j);
                                var k2 = hands[player1].First(n => n != c1);

                                var winnerCard = Round.WinningCard(c1, i, j, _trump);

                                if (winnerCard == c1)
                                {
                                    return Round.WinningCard(k2, i2, j2, _trump) != k2;
                                }
                                else if (winnerCard == i)
                                {
                                    return Round.WinningCard(i2, j2, k2, _trump) != k2;
                                }
                                else
                                {
                                    return Round.WinningCard(j2, k2, i2, _trump) != k2;
                                }
                            }));
                        }
                        else
                        {
                            //: oc-
                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => ValidCards(c1, i, hands[player3]).All(j =>
                            {
                                var i2 = hands[MyIndex].First(l => l != i);
                                var j2 = hands[player3].First(m => m != j);
                                var k2 = hands[player1].First(n => n != c1);

                                var winnerCard = Round.WinningCard(c1, i, j, _trump);

                                if (winnerCard == c1)
                                {
                                    return Round.WinningCard(k2, i2, j2, _trump) != j2;
                                }
                                else if (winnerCard == i)
                                {
                                    return Round.WinningCard(i2, j2, k2, _trump) != j2;
                                }
                                else
                                {
                                    return Round.WinningCard(j2, k2, i2, _trump) != j2;
                                }
                            }));
                        }

                        //zkus zahrat trumfovou sedmu nakonec pokud to jde
                        //toho docilime tak, ze v devatem kole nebudeme hrat trumfovou sedmu aby zustala do posledniho kola
                        return cardsToPlay.OrderBy(i => i.Value)
                                          .FirstOrDefault(i => i.Suit != _trump || i.Value != Hodnota.Sedma) ??
                               cardsToPlay.ToList().RandomOneOrDefault();
                    }
                };
            }

            yield return new AiRule
            {
                Order = 1,
                Description = "hraj vítěznou X",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    if (ValidCards(c1, hands[MyIndex]).Count > 2 &&
                        ValidCards(c1, hands[MyIndex]).HasX(_trump))
                    {
                        if (TeamMateIndex == player3 &&
                            _hands[MyIndex].CardCount(_trump) == 3)
                        {
                            if (!(_probabilities.CardProbability(player1, new Card(_trump, Hodnota.Eso)) >= 1 - _epsilon &&
                                  _probabilities.LikelyCards(player1).Count(i => i != c1 &&
                                                                                 i.Suit == _trump) == 2))
                            {
                                return null;
                            }
                        }
                        else
                        {
                            return null;
                        }
                    }
                    //trumfovou desitku musim hrat pokud ma souper eso a k tomu vyssi trumfy nez ja
                    //takze ja nebudu mit sanci z nej to eso vytlacit
                    var myHighestTrumpAfterX = hands[MyIndex].Where(i => i.Suit == _trump &&
																	    i.Value < Hodnota.Desitka)
															     .Select(i => i.Value)
															     .OrderByDescending(h => h)
															     .FirstOrDefault();
				    if (TeamMateIndex == player3)
                    {
                        //-co
                        //nehraj pokud ma prvni hrac jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka) &&
                            ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka)
                                                          .All(i => i.Suit != _trump &&
                                                                    i.Suit == c1.Suit &&
                                                                    ((_probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Eso)) > 0 &&
                                                                      hands[MyIndex].CardCount(i.Suit) > 2) ||
                                                                     _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Eso)) == 0) &&
                                                                    c1.IsLowerThan(i, _trump) &&
                                                                    ((_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                                      _probabilities.PotentialCards(player1).Count(j => j.Suit == i.Suit &&
                                                                                                                        j != c1 &&
                                                                                                                        j.Value != Hodnota.Eso) > (hands[MyIndex].HasA(i.Suit) ? 2 : 1)) ||
                                                                     _probabilities.CertainCards(player1).Count(j => j.Suit == i.Suit &&
                                                                                                                   j != c1 &&
                                                                                                                   j.Value != Hodnota.Eso) > (hands[MyIndex].HasA(i.Suit) ? 1 : 0))))
                        {
                            return null;
                        }
                        if (ValidCards(c1, hands[MyIndex]).Any(i => i.Suit == _trump &&
                                                                    i.Value == Hodnota.Desitka) &&
                            c1.IsLowerThan(new Card(_trump, Hodnota.Desitka), _trump) &&
                            (_probabilities.CardProbability(player1, new Card(_trump, Hodnota.Eso)) > _epsilon ||
                                _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor) &&
                            hands[MyIndex].CardCount(_trump) == 2 &&
                                                                    !hands[MyIndex].HasA(_trump))
                        {
                            return ValidCards(c1, hands[MyIndex]).FirstOrDefault(i => i.Suit == _trump &&
                                                                                      i.Value == Hodnota.Desitka);
                        }
                        //pocet souperovych trumfu vyssi nez muj nejvyssi trumf mensi nez X
                        var opHiTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
											    .Where(h => h > myHighestTrumpAfterX)
											    .Count(h => _probabilities.CardProbability(player1, new Card(_trump, h)) > _epsilon);
					    var cardToPlay = ValidCards(c1, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Desitka &&
                                                                                            c1.IsLowerThan(i, _trump) &&
                                                                                            (i.Suit != _trump ||                          //pokud to neni trumfova X
                                                                                             (_probabilities.CardProbability(player1, new Card(_trump, Hodnota.Eso)) > _epsilon &&
                                                                                              !(c1.Suit != i.Suit &&
                                                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1) &&  //ignoruj kartu pokud s ni muzu prebit akterovu nizkou kartu v barve
                                                                                              hands[MyIndex].CardCount(_trump) <= 3 &&
                                                                                              (hands[MyIndex].CardCount(_trump) <= opHiTrumps + 1 ||
                                                                                               ((_gameType & Hra.SedmaProti) != 0 &&
                                                                                                hands[MyIndex].Has7(_trump) &&
                                                                                                hands[MyIndex].CardCount(_trump) <= opHiTrumps + 2)) &&
                                                                                              (c1.Suit != i.Suit ||
                                                                                               c1.Value != Hodnota.Eso))));            //a navic nemam trumfove A
                        if (cardToPlay != null &&
                            hands[MyIndex].HasA(cardToPlay.Suit))
                        {
                            return null;
                        }
                        return cardToPlay;
                    }
                    else if (TeamMateIndex == player1)
                    {
                        //oc-
                        //nehraj pokud ma akter jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka &&
                                                                    i.Suit != _trump) &&
                            ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                      i.Suit != _trump)
                                                          .All(i =>
                                            i.Suit != c1.Suit &&
                                            ((_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > 0 &&
                                              hands[MyIndex].CardCount(i.Suit) > 1) ||
                                             _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0) &&
                                            (_probabilities.CertainCards(player3).Any(j => j.Suit == i.Suit &&
                                                                                           j.Value < i.Value) ||
                                             (!_probabilities.PotentialCards(player1).HasSuit(i.Suit) &&
                                              _probabilities.PotentialCards(player3).Count(j => j.Suit == i.Suit &&
                                                                                                j.Value < i.Value) > (hands[MyIndex].HasA(i.Suit) ? 2 : 1)))))
                        {
                            return null;
                        }
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    i.Suit != _trump &&
                                                                                    _probabilities.SuitHigherThanCardProbability(player3, c1, RoundNumber) == 0 &&
                                                                                    (c1.Suit == _trump ||
                                                                                     _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                                    !(_probabilities.CertainCards(player3).Any(j => j.Suit == i.Suit &&
                                                                                                                                    j.Value < i.Value) ||
                                                                                      _probabilities.PotentialCards(player3).CardCount(i.Suit) > 2))
                                                                        .ToList();
                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    i.Suit != _trump &&
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) >= 1 - _epsilon &&
                                                                                    _probabilities.SuitLowerThanCardProbability(player3, i, RoundNumber) == 1)
                                                                        .ToList();
                        }
                        if (!cardsToPlay.Any())
                        {
                            //tohle se ma hrat v pravidle "namazat"
                            var hiCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                .SelectMany(b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                    .Select(h => new Card(b, h)))
                                                                    .Where(i => _probabilities.CardProbability(player3, i) > _epsilon &&
                                                                                c1.IsLowerThan(i, _trump));
                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    !(_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1 &&  //ignoruj kartu pokud s ni muzu prebit akterovu nizkou kartu v barve
                                                                                      _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Eso)) == 0) &&
                                                                                      !hiCards.Any() &&                     //spoluhrac hral nejvyssi kartu co ve hre zbyva
                                                                                      (i.Suit != _trump ||                  //a pokud moje X neni trumfova
                                                                                       !hands[MyIndex].HasA(_trump)))       //trumfovou X hraju jen kdyz nemam A
                                                                        .ToList();
                        }
                        if (cardsToPlay.Any())
                        {
                            return cardsToPlay.RandomOneOrDefault();
                        }

                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    c1.IsLowerThan(i, _trump) &&          //moje karta prebiji prvni kartu
                                                                                    i.Suit != _trump &&                  //a pokud moje X neni trumfova
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                    (_probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor ||
                                                                                     (_probabilities.PotentialCards(player3).CardCount(i.Suit) == 1 &&
                                                                                      _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor))).ToList();
                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                        c1.IsLowerThan(i, _trump) &&          //moje karta prebiji prvni kartu
                                                                                        i.Suit == _trump &&                  //a pokud moje X je trumfova
                                                                                        !hands[MyIndex].HasA(_trump) &&      //trumfovou X hraju jen kdyz nemam A
                                                                                        _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                        (_probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor ||
                                                                                         _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor)).ToList();
                        }
                        if (cardsToPlay.Any())
                        {
                            return cardsToPlay.RandomOneOrDefault();
                        }
                        //tohle se ma hrat v pravidle "namazat"
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&         //1. karta je nejvetsi
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                ((_gameType & Hra.Kilo) == 0 ||
                                                                                 _hands[MyIndex].Count(j => j.Suit != _trump &&
                                                                                                            j.Value >= Hodnota.Desitka) > 2) &&
                                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) != 1 &&  //ignoruj kartu pokud s ni muzu prebit akterovu nizkou kartu v barve
                                                                                (i.Suit != _trump ||                  //a pokud moje X neni trumfova
                                                                                 !hands[MyIndex].HasA(_trump)) &&     //trumfovou X hraju jen kdyz nemam A
                                                                                _probabilities.NoSuitHigherThanCardProbability(player3, c1, RoundNumber) >= 1 - RiskFactor &&
																			    (_probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor ||
																			        _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor)).ToList();
					    return cardsToPlay.RandomOneOrDefault();
                    }
                    else
                    {
                        //-c-
                        //nehraj pokud ma prvni hrac jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (ValidCards(c1, hands[MyIndex]).Count > 2 &&
                            ValidCards(c1, hands[MyIndex]).HasX(_trump))
                        {
                            return null;
                        }
                        if (ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka &&
                                                                    i.Suit != _trump) &&
                            ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                    i.Suit != _trump)
                                                          .All(i =>
                                                                    i.Suit == c1.Suit &&
                                                                    c1.IsLowerThan(i, _trump) &&
                                                                    (((_probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Eso)) > 0 ||
                                                                       _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > 0) &&
                                                                      hands[MyIndex].CardCount(i.Suit) > 2) ||
                                                                     (_probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                      _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0)) &&
                                                                    ((_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                                      Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                         .Select(h => new Card(i.Suit, h))
                                                                         .Where(j => j != c1 &&
                                                                                     j.Value != Hodnota.Eso)
                                                                         .Count(j => _probabilities.CardProbability(player1, j) > _epsilon) > (hands[MyIndex].HasA(i.Suit) ? 2 : 1)) ||
                                                                     Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                         .Select(h => new Card(i.Suit, h))
                                                                         .Where(j => j != c1 &&
                                                                                     j.Value != Hodnota.Eso)
                                                                         .Count(j => _probabilities.CardProbability(player1, j) >= 1 - _epsilon) > (hands[MyIndex].HasA(i.Suit) ? 1 : 0))))
                        {
                            return null;
                        }
                        if (ValidCards(c1, hands[MyIndex]).Any(i => i.Suit == _trump &&
                                                                    i.Value == Hodnota.Desitka) &&
                            c1.IsLowerThan(new Card(_trump, Hodnota.Desitka), _trump) &&
                            (_probabilities.CardProbability(player1, new Card(_trump, Hodnota.Eso)) > _epsilon ||
                                _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor) &&
                            hands[MyIndex].CardCount(_trump) == 2)
                        {
                            return ValidCards(c1, hands[MyIndex]).FirstOrDefault(i => i.Suit == _trump &&
                                                                                      i.Value == Hodnota.Desitka);
                        }
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    c1.IsLowerThan(i, _trump) &&          //moje karta prebiji prvni kartu
                                                                                    i.Suit != _trump &&                  //a pokud moje X neni trumfova
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
																				    (_probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor ||
																				     _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor)).ToList();
                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                        c1.IsLowerThan(i, _trump) &&          //moje karta prebiji prvni kartu
                                                                                        i.Suit == _trump &&                  //a pokud moje X je trumfova
                                                                                        !hands[MyIndex].HasA(_trump) &&      //trumfovou X hraju jen kdyz nemam A
                                                                                        _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                        (_probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor ||
                                                                                         _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor)).ToList();
                        }
                        return cardsToPlay.RandomOneOrDefault();
                    }
                }
            };

            yield return new AiRule
            {
                Order = 2,
                Description = "hrát vítězné A",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    if (TeamMateIndex == -1)
                    {
                        //-c-
                        //nehraj pokud ma prvni hrac jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Eso) &&
                            ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso)
                                                          .All(i => i.Suit != _trump &&
                                                                    i.Suit == c1.Suit &&
                                                                    ((_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                                      _probabilities.PotentialCards(player1)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 2 : 1)) ||
                                                                     _probabilities.CertainCards(player1)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0))))
                        {
                            return null;
                        }
                        return ValidCards(c1, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                  i.Suit != _trump &&
                                                                                  c1.IsLowerThan(i, _trump) &&
                                                                                  (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1 ||
                                                                                   _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                                  ((c1.Suit == i.Suit &&
                                                                                    c1.Value == Hodnota.Desitka) ||
                                                                                   _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon) &&
                                                                                  (_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon ||
                                                                                   Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                       .Where(h => h != Hodnota.Desitka)
                                                                                       .All(h => _probabilities.CardProbability(player3, new Card(i.Suit, h)) <= _epsilon)) &&
                                                                                  !((_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                                                     _probabilities.PotentialCards(player1)
                                                                                                   .Where(j => j.Suit == i.Suit)
                                                                                                   .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 2 : 1)) ||
                                                                                    _probabilities.CertainCards(player1)
                                                                                                  .Where(j => j.Suit == i.Suit)
                                                                                                  .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0)));
                    }
                    else if (TeamMateIndex == player3)
                    {
                        //-co
                        //nehraj pokud ma prvni hrac jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Eso) &&
                            ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso)
                                                          .All(i => i.Suit != _trump &&
                                                                    i.Suit != _trump &&
                                                                    i.Suit == c1.Suit &&
                                                                    ((_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                                      _probabilities.PotentialCards(player1)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 2 : 1)) ||
                                                                     _probabilities.CertainCards(player1)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0))))
                        {
                            return null;
                        }
                        return ValidCards(c1, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                  i.Suit != _trump &&
                                                                                  c1.IsLowerThan(i, _trump) &&
                                                                                  ((c1.Suit == i.Suit &&
                                                                                    c1.Value == Hodnota.Desitka) ||
                                                                                   (_probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon &&
                                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                        .Where(h => h != Hodnota.Desitka)
                                                                                        .All(h => _probabilities.CardProbability(player3, new Card(i.Suit, h)) < 1 - _epsilon))) &&
                                                                                  !((_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                                                     _probabilities.PotentialCards(player1)
                                                                                                   .Where(j => j.Suit == i.Suit)
                                                                                                   .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 2 : 1)) ||
                                                                                    _probabilities.CertainCards(player1)
                                                                                                  .Where(j => j.Suit == i.Suit)
                                                                                                  .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0)));
                    }
                    else
                    {
                        //oc-
                        //nehraj pokud ma akter jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Eso) &&
                            ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso)
                                                          .All(i =>
                                            i.Suit != _trump &&
                                            i.Suit != c1.Suit &&
                                            (_probabilities.CertainCards(player3).Any(j => j.Suit == i.Suit &&
                                                                                          j.Value < i.Value) ||
                                             (!_probabilities.PotentialCards(player1).HasSuit(i.Suit) &&
                                              _probabilities.PotentialCards(player3).CardCount(i.Suit) > 1))))
                        {
                            return null;
                        }

                        return ValidCards(c1, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                  i.Suit != _trump &&
                                                                                  _probabilities.SuitHigherThanCardProbability(player3, c1, RoundNumber) == 0 &&
                                                                                  ((c1.Suit == _trump &&
                                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                        .All(h => _probabilities.CardProbability(player3, new Card(i.Suit, h)) < 1 - _epsilon)) ||
                                                                                   _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                                  !(_probabilities.CertainCards(player3).Any(j => j.Suit == i.Suit &&
                                                                                                                                  j.Value < i.Value) ||
                                                                                    (!_probabilities.PotentialCards(player1).HasSuit(i.Suit) &&
                                                                                     _probabilities.PotentialCards(player3).CardCount(i.Suit) > 1)));
                    }
                }
            };

            yield return new AiRule
            {
                Order = 3,
                Description = "bodovat nebo vytlačit trumf",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    //pokud ma oponent jen jednu neznamou kartu a na zadnou z jeho znamych karet nemuzu pozdeji namazat
                    if (TeamMateIndex == player1)
                    {
                        var opponentsLikelyCards = _probabilities.LikelyCards(player3);
                        if (opponentsLikelyCards.Length == 10 - RoundNumber &&
                            !hands[MyIndex].HasSuit(_trump) &&
                            opponentsLikelyCards.Where(i => i.Suit != _trump)
                                                .All(i => (hands[MyIndex].HasSuit(i.Suit) &&
                                                           hands[MyIndex].CardCount(i.Suit) <= opponentsLikelyCards.Count(j => j.Suit == i.Suit)) ||
                                                          _probabilities.SuitHigherThanCardProbability(player1, i, RoundNumber) >= 1 - RiskFactor))
                        {
                            var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value >= Hodnota.Desitka &&
                                                                                        c1.IsLowerThan(i, _trump) &&
                                                                                        i.Suit != _trump &&
                                                                                        _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                        _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) > 0)
                                                                            .ToList();

                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.First();
                            }
                        }
                    }
                    if (TeamMateIndex == player1 &&
                        //(_gameType & Hra.Sedma) != 0 &&
                        ((hands[MyIndex].SuitCount == 3 &&
                          !hands[MyIndex].HasSuit(_trump)) ||
                         hands[MyIndex].SuitCount == 1 ||
                         hands[MyIndex].HasSuit(_trump)))
                    {
                        return ValidCards(c1, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                  i.Suit != _trump &&
                                                                                  c1.IsLowerThan(i, _trump) &&
                                                                                  !(hands[MyIndex].CardCount(i.Suit) >= 4 &&
                                                                                    hands[MyIndex].CardCount(_trump) >= 3) &&
                                                                                  (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 ||
                                                                                   _probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor));
                    }
                    if (TeamMateIndex != player3 &&
                        (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0)
                    {
                        return ValidCards(c1, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                  i.Suit != _trump &&
                                                                                  c1.IsLowerThan(i, _trump) &&
                                                                                  !(hands[MyIndex].CardCount(i.Suit) >= 4 &&
                                                                                    hands[MyIndex].CardCount(_trump) >= 3) &&
                                                                                  (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                                  _probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor));
                    }
                    return null;
                }
            };

            yield return new AiRule
            {
                Order = 4,
                Description = "vytlačit A",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    if (TeamMateIndex == -1 || TeamMateIndex == player1)
                    {
                        //-c-
                        //oc-
                        return ValidCards(c1, hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                         i.Value >= Hodnota.Spodek &&                                                                         
                                                                         (i.Suit != _trump ||
                                                                          _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) == 0) &&
                                                                         c1.IsLowerThan(i, _trump) &&
                                                                         _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > 0)
                                                             .OrderByDescending(i => i.Value)
                                                             .FirstOrDefault();
                    }
                    return null;
                }
            };

            yield return new AiRule
            {
                Order = 5,
                Description = "namazat",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    if (TeamMateIndex == player3)
                    {
                        //-co
                        //nehraj pokud ma prvni hrac jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value >= Hodnota.Desitka) &&
                            ValidCards(c1, hands[MyIndex]).Where(i => i.Value >= Hodnota.Desitka)
                                                          .All(i => (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                                     _probabilities.PotentialCards(player1)
                                                                                   .Where(j => j.Suit == i.Suit)
                                                                                   .Count(j => j != c1 &&
                                                                                               j.Value < Hodnota.Desitka) > 2) ||
                                                                    _probabilities.CertainCards(player1)
                                                                                  .Where(j => j.Suit == i.Suit)
                                                                                  .Any(j => j != c1 &&
                                                                                            j.Value < Hodnota.Desitka)))
                        {
                            return null;
                        }
                        var pointsWonSoFar = _rounds?.Where(r => r != null &&
                                                                 (r?.roundWinner?.PlayerIndex ?? -1) != player1)
                                                    ?.Sum(r => r.basicPoints1 + r.basicPoints2 + r.basicPoints3);
                        if ((_gameType & Hra.Kilo) != 0 &&
                            c1.Suit == _trump &&
                            c1.Value >= Hodnota.Spodek &&
                            _probabilities.CardProbability(player1, new Card(_trump, Hodnota.Eso)) > _epsilon &&
                            _probabilities.CardProbability(player1, new Card(_trump, Hodnota.Desitka)) > _epsilon &&
                            pointsWonSoFar < 20 &&
                            !(Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Any(b => (hands[MyIndex].HasX(b) &&
                                             (hands[MyIndex].HasA(b) ||
                                              hands[MyIndex].CardCount(b) >= 5)) ||
                              hands[MyIndex].CardCount(Hodnota.Desitka) > 2)))
                        {
                            //nemaz pokud akter vyjel trumfem a jeste nesla ani desitka ani eso
                            return null;
                        }
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit != _trump  &&
                                                                         ((i.Value == Hodnota.Desitka &&
                                                                           !(c1.Suit == i.Suit &&
                                                                             c1.Value == Hodnota.Eso) &&
                                                                           !((_gameType & Hra.Kilo) != 0 &&
                                                                              hands[MyIndex].CardCount(Hodnota.Desitka) <= 2 &&
                                                                              hands[MyIndex].CardCount(i.Suit) >= 5)) ||
                                                                          (i.Value == Hodnota.Eso &&        //eso namaz jen kdyz nemuzu chytit desitku nebo pri kilu (proti)
                                                                           (_probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon ||
                                                                            ((_gameType & Hra.Kilo) != 0 &&
                                                                             _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) < 1 &&
                                                                             _probabilities.SuitHigherThanCardProbability(player3, c1, RoundNumber) >= 1 - RiskFactor &&
                                                                             (pointsWonSoFar >= 20 ||
                                                                              hands[MyIndex].HasX(i.Suit)))))) &&
                                                                           //))) &&
                                                                         !(c1.Value == Hodnota.Desitka &&   //nemaz pokud prvni hrac vyjel desitkou a nevim kdo ma eso (nebylo-li jeste hrano)
                                                                           (_gameType & Hra.Kilo) == 0 &&   //(neplati pri kilu)
                                                                           _probabilities.PotentialCards(player3).Count(j => j.Suit == c1.Suit) > 2 &&
                                                                           _probabilities.CardProbability(player3, new Card(c1.Suit, Hodnota.Eso)) > _epsilon &&
                                                                           _probabilities.CardProbability(player3, new Card(c1.Suit, Hodnota.Eso)) < 1 - _epsilon &&
                                                                           _probabilities.CardProbability(player1, new Card(c1.Suit, Hodnota.Eso)) > _epsilon) &&
                                                                         (_probabilities.SuitHigherThanCardProbability(player3, c1, RoundNumber) >= 1 / 3f || //1 - RiskFactor ||
                                                                                                                                                              //((_gameType & Hra.Kilo) != 0 &&
                                                                                                                                                              // _probabilities.SuitHigherThanCardProbability(player3, c1, RoundNumber) >= 1/3f) ||
                                                                          (c1.Suit != _trump &&
                                                                           ((c1.Value != Hodnota.Eso &&
                                                                             _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) <= 0.5f) ||
                                                                            (c1.Value == Hodnota.Eso &&
                                                                             _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) == 0)) && //RiskFactor &&
                                                                           (_probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor ||
                                                                            ((_gameType & Hra.Kilo) != 0) &&  //u kila zkousim mazat vice
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) >= RiskFactor))) &&
                                                                         !((_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                                            _probabilities.PotentialCards(player1)
                                                                                          .Where(j => j.Suit == i.Suit)
                                                                                          .Count(j => j != c1 &&
                                                                                                      j.Value < Hodnota.Desitka) > 2) ||
                                                                           _probabilities.CertainCards(player1)
                                                                                         .Where(j => j.Suit == i.Suit)
                                                                                         .Any(j => j != c1 &&
                                                                                                   j.Value < Hodnota.Desitka)))
                                                                        .OrderBy(i => _probabilities.SuitProbability(player1, i.Suit, RoundNumber))    //namaz v barve kterou souper nema
                                                                        .ToList();
                        var cardToPlay = cardsToPlay.FirstOrDefault();

                        if (cardToPlay != null &&
                            cardsToPlay.HasA(cardToPlay.Suit))
                        {
                            cardToPlay = cardsToPlay.FirstOrDefault(i => i.Suit == cardToPlay.Suit &&
                                                                         i.Value == Hodnota.Eso);
                        }
                        return cardsToPlay.FirstOrDefault();
                    }
                    else if (TeamMateIndex == player1)
                    {
                        //oc-
                        //nehraj pokud ma akter jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Eso) &&
                            ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso)
                                                          .All(i =>
                                            i.Suit != _trump &&
                                            i.Suit != c1.Suit &&
                                            (_probabilities.CertainCards(player3).Any(j => j.Suit == i.Suit &&
                                                                                          j.Value < i.Value) ||
                                             (!_probabilities.PotentialCards(player1).HasSuit(i.Suit) &&
                                              _probabilities.PotentialCards(player3).CardCount(i.Suit) > 1))))
                        {
                            return null;
                        }
                        //zkusit namazat eso
                        var hiCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                          .SelectMany(b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                               .Select(h => new Card(b, h)))
                                                               .Where(i => _probabilities.CardProbability(player3, i) > _epsilon &&
                                                                           c1.IsLowerThan(i, _trump));
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon &&
                                                                                    !hiCards.Any() &&                   //spoluhrac hral nejvyssi kartu co ve hre zbyva
                                                                                    i.Suit != _trump)
                                                                        .ToList();

                        if (cardsToPlay.Any())
                        {
                            return cardsToPlay.RandomOneOrDefault();
                        }
                        //cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                        //                                                        c1.IsLowerThan(i, _trump) &&            //moje karta prebiji prvni kartu
                        //                                                        i.Suit != _trump &&                    //a pokud moje A neni trumfove
                        //                                                        (_probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor ||
                        //                                                         _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor))
                        //                                            .ToList();

                        //if (cardsToPlay.Any())
                        //{
                        //    return cardsToPlay.RandomOneOrDefault();
                        //}
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&               //1. karta je nejvetsi
                                                                                i.Suit != _trump &&                     //a pokud moje A neni trumfove
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon &&
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                _probabilities.NoSuitHigherThanCardProbability(player3, c1, RoundNumber) >= 1 - RiskFactor &&
                                                                                (_probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor ||
                                                                                 _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor)).ToList();

                        cardsToPlay = cardsToPlay.Where(i => !(_probabilities.SuitProbability(player1, i.Suit, RoundNumber) == 0 &&
                                                                     _probabilities.PotentialCards(player3)
                                                                                   .Where(j => j.Suit == i.Suit)
                                                                                   .Count(j => j != c1 &&
                                                                                               j.Value < Hodnota.Desitka) > 2) ||
                                                                    _probabilities.CertainCards(player1)
                                                                                   .Where(j => j.Suit == i.Suit)
                                                                                   .Any(j => j != c1 &&
                                                                                             j.Value < Hodnota.Desitka)).ToList();
                        return cardsToPlay.RandomOneOrDefault();
                    }
                    return null;
                }
            };

            yield return new AiRule
            {
                Order = 6,
                Description = "odmazat si barvu",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    //var poorSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    //                   .Where(b => b != _trump &&
                    //                               hands[MyIndex].CardCount(b) > 0 &&
                    //                               ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == b)
                    //                                                             .All(i => i.Value != Hodnota.Desitka &&
                    //                                                                       i.Value != Hodnota.Eso))
                    //                   .ToDictionary(k => k, v => hands[MyIndex].CardCount(v))
                    //                   .OrderBy(kv => kv.Value)
                    //                   .Select(kv => new Tuple<Barva, int>(kv.Key, kv.Value))
                    //                   .FirstOrDefault();
                    
                    //if (poorSuit != null && poorSuit.Item2 == 1)
                    {
                        //odmazat si barvu pokud mam trumfy abych souperum mohl brat a,x
                        if (TeamMateIndex == -1)
                        {
                            //-c-
                            var cardToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit != _trump && 
                                                                                       hands[MyIndex].CardCount(i.Suit) == 1 &&//i.Suit == poorSuit.Item1 &&
                                                                                       hands[MyIndex].HasSuit(_trump) &&
																					   (_probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Eso)) > _epsilon ||
                                                                                        _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > _epsilon) &&
                                                                                       (_probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) > _epsilon ||
                                                                                        _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) > _epsilon))
                                                                           .OrderByDescending(i => i.Value)
                                                                           .FirstOrDefault();

                            if (cardToPlay != null)
                            {
                                return cardToPlay;
                            }
                        }
						else if (TeamMateIndex == player1)
						{
							//oc-
							var cardToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit != _trump && 
                                                                                       hands[MyIndex].CardCount(i.Suit) == 1 &&//i.Suit == poorSuit.Item1 &&
                                                                                       !_bannedSuits.Contains(i.Suit) &&
                                                                                       hands[MyIndex].HasSuit(_trump) &&
																					   _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > _epsilon &&
																					   _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) > _epsilon)
																		   .OrderByDescending(i => i.Value)
																		   .FirstOrDefault();

							if (cardToPlay != null)
							{
								return cardToPlay;
							}
						}
					}
                    if (TeamMateIndex != -1)
                    {
                        //odmazat si barvu pokud nemam trumfy abych mohl mazat
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                         i.Suit != c1.Suit &&
                                                                         i.Value != Hodnota.Eso &&
                                                                         i.Value != Hodnota.Desitka &&
                                                                         !hands[MyIndex].HasSuit(_trump) &&
                                                                         hands[MyIndex].Any(j => j.Value == Hodnota.Eso ||
                                                                                                 j.Value == Hodnota.Desitka) &&
                                                                         hands[MyIndex].CardCount(i.Suit) == 1)
                                                                        .ToList();

                        if ((TeamMateIndex == player3 &&
                             (_probabilities.SuitHigherThanCardProbability(TeamMateIndex, c1, RoundNumber) >= 1 - RiskFactor ||
                              (_probabilities.SuitProbability(TeamMateIndex, c1.Suit, RoundNumber) == 0 &&
                               _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) >= 1 - RiskFactor))) ||
                            (TeamMateIndex == player1 &&
                             !(_probabilities.SuitHigherThanCardProbability(player3, c1, RoundNumber) >= 1 - RiskFactor ||
                               (_probabilities.SuitProbability(player3, c1.Suit, RoundNumber) == 0 &&
                                _probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor))))
                        {
                            return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                        }
                        else
                        {
                            return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                        }
                    }
                    return null;
                }
            };

            yield return new AiRule
            {
                Order = 8,
                Description = "zkusit vytáhnout trumfovou X",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    if (TeamMateIndex != player3)
                    {
                        //-c-
                        //oc-
                        //normalne se tohle pravidlo neuplatni, protoze driv vytahne plnkovou trumfovou x z prvni pozice
                        //pokud ale clovek neposlechne napovedu, tak v dalsich kolech tohle pravidlo muze poradit vytahnout trumfovou desitku zde
                        if (c1.Suit == _trump &&
                            _probabilities.HasSolitaryX(player3, c1.Suit, RoundNumber) >= (TeamMateIndex == -1 ? SolitaryXThreshold : SolitaryXThresholdDefense))
                        {
                            return ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == c1.Suit &&
                                                                             i.Value == Hodnota.Eso)
                                                                 .FirstOrDefault();
                        }
                    }
                    return null;
                }
            };

            yield return new AiRule
            {
                Order = 9,
                Description = "hrát vysokou kartu mimo A,X",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
					if (TeamMateIndex != player3 &&
					    ValidCards(c1, hands[MyIndex]).Any(i => i.Suit == c1.Suit &&
					                                            i.Value > c1.Value))
                    {
                        //-c-
                        //oc-
                        return ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == c1.Suit &&
                                                                         i.Value != Hodnota.Desitka &&
                                                                         i.Value != Hodnota.Eso &&
                                                                         _probabilities.PotentialCards(player3).HasA(i.Suit))
                                                             .OrderByDescending(i => i.Value)
                                                             .FirstOrDefault();
                    }
                    return null;
                }
            };

            yield return new AiRule
            {
                Order = 10,
                Description = "hrát nízkou kartu mimo A,X",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    if (TeamMateIndex != player1)
                    {
                        //-co
                        //preferuj barvu kde nemam a,x a kde soupere nechytam
                        var axPerSuit = new Dictionary<Barva, int>(); 
                        var holesPerSuit = new Dictionary<Barva, int>();
                        var hiCardsPerSuit = new Dictionary<Barva, int>();
                        var catchCardsPerSuit = new Dictionary<Barva, int>();
                        var cardsToPlay = new List<Card>();

                        foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                        {
                            var holes = 0;      //pocet der v barve
                            var hiCards = 0;    //pocet karet ktere maji pod sebou diru v barve
                            var axCount = ValidCards(c1, hands[MyIndex]).Count(i => i.Suit == b &&
                                                                                    (i.Value == Hodnota.Desitka ||
                                                                                     i.Value == Hodnota.Eso));

                            foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                            {
                                var c = new Card(b, h);
                                var n = ValidCards(c1, hands[MyIndex]).Count(i => i.Suit == b &&
                                                                                  i.Value > c.Value &&
                                                                                  _probabilities.CardProbability(player1, c) > _epsilon);

                                if (n > 0)
                                {
                                    holes++;
                                    hiCards = Math.Max(hiCards, n);
                                }
                            }
                            axPerSuit.Add(b, axCount);
                            holesPerSuit.Add(b, holes);
                            hiCardsPerSuit.Add(b, hiCards);
                            catchCardsPerSuit.Add(b, Math.Min(hiCards, holes));
                        }
                        var validSuits = ValidCards(c1, hands[MyIndex]).Select(i => i.Suit).Distinct();
                        var catchCardsPerSuitNoAX = catchCardsPerSuit.Where(i => validSuits.Contains(i.Key) &&
                                                                                 hands[MyIndex].Any(j => j.Suit == i.Key &&
                                                                                                         j.Value != Hodnota.Eso &&
                                                                                                         j.Value != Hodnota.Desitka) &&
                                                                                 !(hands[MyIndex].HasX(i.Key) &&
                                                                                   hands[MyIndex].CardCount(i.Key) == 2));
                        if (catchCardsPerSuitNoAX.Any())
                        {
                            var preferredSuit = catchCardsPerSuitNoAX.Where(i => !_bannedSuits.Contains(i.Key))
                                                                     .OrderBy(i => axPerSuit[i.Key])
                                                                     .ThenBy(i => i.Value)
                                                                     .Select(i => (Barva?)i.Key)
                                                                     .FirstOrDefault();

                            if (preferredSuit.HasValue)
                            {
                                cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == preferredSuit.Value &&
                                                                                        (hands[MyIndex].CardCount(i.Suit) == 1 ||
                                                                                         !hands[MyIndex].Where(j => j.Suit == i.Suit)
                                                                                                        .All(j => j.Value <= i.Value)))
                                                                            .ToList();
                            }
                            if (cardsToPlay.HasSuit(_trump) ||
                                cardsToPlay.All(i => i.Suit != c1.Suit) ||
                                cardsToPlay.Any(i => _hands[MyIndex].HasX(i.Suit) &&
                                                     _probabilities.PotentialCards(player1).HasA(i.Suit) &&
                                                     !_probabilities.PotentialCards(player3).HasA(i.Suit)) ||
                                (TeamMateIndex == player3 &&
                                 (_probabilities.SuitHigherThanCardProbability(TeamMateIndex, c1, RoundNumber) >= 1 - RiskFactor ||
                                  (_probabilities.SuitProbability(TeamMateIndex, c1.Suit, RoundNumber) == 0 &&
                                   _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) >= 1 - RiskFactor))))
                            {
                                return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                            }
                            else
                            {
                                return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                            }
                        }
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value != Hodnota.Eso &&
                                                                                i.Value != Hodnota.Desitka &&
                                                                                (hands[MyIndex].CardCount(i.Suit) == 1 ||
                                                                                 !hands[MyIndex].Where(j => j.Suit == i.Suit)
                                                                                                .All(j => j.Value <= i.Value)))
                                                                        .ToList();
                        if (cardsToPlay.Any(i => !_bannedSuits.Contains(i.Suit)))
                        {
                            cardsToPlay = cardsToPlay.Where(i => !_bannedSuits.Contains(i.Suit)).ToList();
                        }
                        if (cardsToPlay.HasSuit(_trump) ||
                            cardsToPlay.All(i => i.Suit != c1.Suit) ||
                            (TeamMateIndex != -1 &&
                             (_probabilities.SuitHigherThanCardProbability(TeamMateIndex, c1, RoundNumber) >= 1 - RiskFactor ||
                              (_probabilities.SuitProbability(TeamMateIndex, c1.Suit, RoundNumber) == 0 &&
                               _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) >= 1 - RiskFactor))))
                        {
                            return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                        }
                        else
                        {
                            return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                        }
                    }
                    return null;
                }
            };

            yield return new AiRule
            {
                Order = 11,
                Description = "hrát nízkou kartu",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => hands[MyIndex].CardCount(i.Suit) == 1 ||
                                                                                !hands[MyIndex].Where(j => j.Suit == i.Suit)
                                                                                               .All(j => j.Value <= i.Value))
                                                                    .ToList();

                    if (cardsToPlay.Any(i => i.Value < Hodnota.Desitka))
                    {
                        cardsToPlay = cardsToPlay.Where(i => i.Value < Hodnota.Desitka).ToList();
                    }
                    if (cardsToPlay.Any(i => !_bannedSuits.Contains(i.Suit)))
                    {
                        cardsToPlay = cardsToPlay.Where(i => !_bannedSuits.Contains(i.Suit)).ToList();
                    }
                    if (cardsToPlay.HasSuit(_trump) ||
                        cardsToPlay.All(i => i.Suit != c1.Suit) ||
                        (TeamMateIndex == player3 &&
                         (_probabilities.SuitHigherThanCardProbability(TeamMateIndex, c1, RoundNumber) >= 1 - RiskFactor ||
                          (_probabilities.SuitProbability(TeamMateIndex, c1.Suit, RoundNumber) == 0 &&
                           _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) >= 1 - RiskFactor))) ||
                         (TeamMateIndex == player1 &&
                          !(_probabilities.SuitHigherThanCardProbability(player3, c1, RoundNumber) >= 1 - RiskFactor ||
                            (_probabilities.SuitProbability(player3, c1.Suit, RoundNumber) == 0 &&
                             _probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor))))
                    {
                        return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                    }
                    else
                    {
                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }
                }
            };
        }

        protected override IEnumerable<AiRule> GetRules3(Hand[] hands)
        {
            var player1 = (MyIndex + 1) % Game.NumPlayers;
            var player2 = (MyIndex + 2) % Game.NumPlayers;

            BeforeGetRules23();
            if (RoundNumber == 9)
            {
                yield return new AiRule()
                {
                    Order = 0,
                    Description = "hrát tak abych bral poslední štych",
                    ChooseCard3 = (Card c1, Card c2) =>
                    {
                        //hrat tak abych bral posledni stych
                        IEnumerable<Card> cardsToPlay;

                        //pokud spoluhrac zahlasil sedmu proti, tak pravidlo nehraj
                        if (TeamMateIndex != -1 &&
                            (_gameType & Hra.SedmaProti) != 0 &&
                             _probabilities.CardProbability(TeamMateIndex, new Card(_trump, Hodnota.Sedma)) == 1)
                        {
                            return null;
                        }
                        if (hands[MyIndex].CardCount(_trump) == 2 &&
                            (_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                .Count(h => ((c1.Suit != _trump || c1.Value != h) &&
                                             _probabilities.CardProbability(player1, new Card(_trump, h)) > 0) ||
                                            ((c2.Suit != _trump || c2.Value != h) &&
                                             _probabilities.CardProbability(player2, new Card(_trump, h)) > 0)) == 1)
                        {
                            return ValidCards(hands[MyIndex]).OrderBy(i => i.Value).FirstOrDefault();
                        }
                        if (TeamMateIndex == -1)
                        {
                            //: --c
                            cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i =>
                            {
                                var i2 = hands[player1].First(l => l != c1);
                                var j2 = hands[player2].First(m => m != c2);
                                var k2 = hands[MyIndex].First(n => n != i);

                                var winnerCard = Round.WinningCard(c1, c2, i, _trump);

                                if (winnerCard == c1)
                                {
                                    return Round.WinningCard(i2, j2, k2, _trump) == k2;
                                }
                                else if (winnerCard == c2)
                                {
                                    return Round.WinningCard(j2, k2, i2, _trump) == k2;
                                }
                                else
                                {
                                    return Round.WinningCard(k2, i2, j2, _trump) == k2;
                                }
                            });
                        }
                        else if (TeamMateIndex == player1)
                        {
                            //: o-c
                            if (c2.IsHigherThan(c1, _trump) &&
                                ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Suit != _trump &&
                                                                            (i.Value == Hodnota.Eso ||
                                                                             i.Value == Hodnota.Desitka)))
                            {
                                return null;    //pouzijeme pravidlo o namazani
                            }
                            cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i =>
                            {
                                var i2 = hands[player1].First(l => l != c1);
                                var j2 = hands[player2].First(m => m != c2);
                                var k2 = hands[MyIndex].First(n => n != i);

                                var winnerCard = Round.WinningCard(c1, c2, i, _trump);

                                if (winnerCard == c1)
                                {
                                    return Round.WinningCard(i2, j2, k2, _trump) != j2;
                                }
                                else if (winnerCard == c2)
                                {
                                    return Round.WinningCard(j2, k2, i2, _trump) != j2;
                                }
                                else
                                {
                                    return Round.WinningCard(k2, i2, j2, _trump) != j2;
                                }
                            });
                        }
                        else
                        {
                            //: -oc
                            if (c2.IsHigherThan(c1, _trump) &&
                                ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Suit != _trump &&
                                                                            (i.Value == Hodnota.Eso ||
                                                                             i.Value == Hodnota.Desitka)))
                            {
                                return null;    //pouzijeme pravidlo o namazani
                            }
                            cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i =>
                            {
                                var i2 = hands[player1].First(l => l != c1);
                                var j2 = hands[player2].First(m => m != c2);
                                var k2 = hands[MyIndex].First(n => n != i);

                                var winnerCard = Round.WinningCard(c1, c2, i, _trump);

                                if (winnerCard == c1)
                                {
                                    return Round.WinningCard(i2, j2, k2, _trump) != i2;
                                }
                                else if (winnerCard == c2)
                                {
                                    return Round.WinningCard(j2, k2, i2, _trump) != i2;
                                }
                                else
                                {
                                    return Round.WinningCard(k2, i2, j2, _trump) != i2;
                                }
                            });
                        }

                        //zkus zahrat trumfovou sedmu nakonec pokud to jde
                        //toho docilime tak, ze v devatem kole nebudeme hrat trumfovou sedmu aby zustala do posledniho kola
                        return cardsToPlay.OrderBy(i => i.Value)
                                          .FirstOrDefault(i => i.Suit != _trump || i.Value != Hodnota.Sedma) ??
                               cardsToPlay.ToList().RandomOneOrDefault();
                    }
                };
            }

            yield return new AiRule
            {
                Order = 1,
                Description = "hraj vítěznou X",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    if (ValidCards(c1, c2, hands[MyIndex]).Count > 2 &&
                        ValidCards(c1, c2, hands[MyIndex]).HasX(_trump))
                    {
                        if (TeamMateIndex != -1 &&
                            _hands[MyIndex].CardCount(_trump) == 3)
                        {
                            if (!((_probabilities.CardProbability(player1, new Card(_trump, Hodnota.Eso)) >= 1 - _epsilon &&
                                   _probabilities.LikelyCards(player1).Count(i => i != c2 &&
                                                                                  i.Suit == _trump) == 2) ||
                                  (_probabilities.CardProbability(player2, new Card(_trump, Hodnota.Eso)) >= 1 - _epsilon &&
                                   _probabilities.LikelyCards(player2).Count(i => i != c1 &&
                                                                                  i.Suit == _trump) == 2)))
                            {
                                return null;
                            }
                        }
                        else
                        {
                            return null;
                        }
                        if (RoundNumber == 8)
                        {
                            if (!((TeamMateIndex == player1 &&
                                   _probabilities.LikelyCards(player2).Count(i => i != c2 &&
                                                                                  i.Suit == _trump) == 2) ||
                                  (TeamMateIndex == player2 &&
                                   _probabilities.LikelyCards(player1).Count(i => i != c1 &&
                                                                                  i.Suit == _trump) == 2)))
                            {
                                return null;
                            }
                        }
                        else
                        {
                            return null;
                        }
                    }
                    //trumfovou desitku musim hrat pokud ma souper eso a k tomu vyssi trumfy nez ja
                    //takze ja nebudu mit sanci z nej to eso vytlacit
                    var myHighestTrumpAfterX = hands[MyIndex].Where(i => i.Suit == _trump &&
                                                                         i.Value < Hodnota.Desitka)
                                                             .Select(i => i.Value)
													         .OrderByDescending(h => h)
                                                             .FirstOrDefault();
					if (TeamMateIndex == -1)
                    {
						var opHiTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
											 .Where(h => h > myHighestTrumpAfterX)
											 .Count(h => _probabilities.CardProbability(player1, new Card(_trump, h)) > _epsilon ||
                                                         _probabilities.CardProbability(player2, new Card(_trump, h)) > _epsilon);
						var cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Desitka &&
                                                                                      !hands[MyIndex].HasA(i.Suit) &&
                                                                                      (i.Suit != _trump ||              //pokud to neni trumfova X
                                                                                       //(!hands[MyIndex].HasA(i.Suit) && //nebo pokud mam 2 a mene trumfu a nemam trumfove A
                                                                                        //(hands[MyIndex].CardCount(i.Suit) <= 2 ||   //nebo hraju sedmu a
                                                                                        // (hands[MyIndex].CardCount(i.Suit) <= 3 &&  //mam 3 a mene trumfu a nemam trumfove A
                                                                                        //  (_gameType & Hra.Sedma) != 0)) &&
                                                                                        //(c1.Suit != i.Suit ||           //a pokud nikdo nehral trumfove eso v tomto kole
                                                                                        // c1.Value != Hodnota.Eso) &&
                                                                                        //(c2.Suit != i.Suit ||
                                                                                         //c2.Value != Hodnota.Eso))) &&
                                                                                       ((_probabilities.CardProbability(player1, new Card(_trump, Hodnota.Eso)) > _epsilon ||
                                                                                         _probabilities.CardProbability(player2, new Card(_trump, Hodnota.Eso)) > _epsilon) &&
                                                                                        hands[MyIndex].CardCount(_trump) <= 3 &&
                                                                                        (hands[MyIndex].CardCount(_trump) <= opHiTrumps + 1 ||
                                                                                         ((_gameType & Hra.Sedma) != 0 &&
                                                                                          hands[MyIndex].Has7(_trump) &&
                                                                                          hands[MyIndex].CardCount(_trump) <= opHiTrumps + 2)))) &&
                                                                                      Round.WinningCard(c1, c2, i, _trump) == i);
                        return cardToPlay;
                    }
                    else if (TeamMateIndex == player1)
                    {
                        //nehraj pokud ma oponent jiste dalsi karty v barve a muzes hrat i neco jineho
                        if (ValidCards(c1, hands[MyIndex]).Count > 2 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka &&
                                                                    i.Suit != _trump) &&
                            ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                      i.Suit != _trump)
                                                          .All(i => (c1.IsHigherThan(c2, _trump) ||
                                                                     c2.IsLowerThan(i, _trump)) &&
                                                                    ((_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) == 0 &&
                                                                      _probabilities.PotentialCards(player2)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1 &&
                                                                                                j != c2 &&
                                                                                                j.Value < i.Value) > (hands[MyIndex].HasA(i.Suit) ? 2 : 1)) ||
                                                                     _probabilities.CertainCards(player2)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1 &&
                                                                                              j != c2 &&
                                                                                              j.Value < i.Value) > (hands[MyIndex].HasA(i.Suit) ? 1 : 0))))
                        {
                            return null;
                        }
                        var opHiTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                             .Where(h => h > myHighestTrumpAfterX)
                                             .Count(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > _epsilon);

                        var cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Desitka &&
                                                                                      !hands[MyIndex].HasA(i.Suit) &&
                                                                                      !(_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) == 0 &&  //ignoruj kartu pokus s ni muzes prebot akterovu nizkou barvu
                                                                                        Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                            .Select(h => new Card(i.Suit, h))
                                                                                            .Where(j => j != c2)
                                                                                            .Any(j => _probabilities.CardProbability(player2, j) >= 1 - _epsilon)) &&
                                                                                      (i.Suit != _trump ||              //pokud to neni trumfova X
                                                                                      // (!hands[MyIndex].HasA(i.Suit) && //nebo pokud mam 2 a mene trumfu a nemam trumfove A
                                                                                      //  hands[MyIndex].CardCount(i.Suit) <= 2 &&
                                                                                      //  (c1.Suit != i.Suit ||           //a pokud nikdo nehral trumfove eso v tomto kole
                                                                                      //   c1.Value != Hodnota.Eso) &&
                                                                                      //  (c2.Suit != i.Suit ||
                                                                                      //   c2.Value != Hodnota.Eso))) &&
                                                                                       (_probabilities.CardProbability(player2, new Card(_trump, Hodnota.Eso)) > _epsilon &&
                                                                                        hands[MyIndex].CardCount(_trump) <= 3 &&
                                                                                        (hands[MyIndex].CardCount(_trump) <= opHiTrumps + 1 ||
                                                                                         ((_gameType & Hra.SedmaProti) != 0 &&
                                                                                          hands[MyIndex].Has7(_trump) &&
                                                                                          hands[MyIndex].CardCount(_trump) <= opHiTrumps + 2)))) &&
                                                                                      Round.WinningCard(c1, c2, i, _trump) != c2);
                        if (cardToPlay != null &&
                            ValidCards(c1, c2, hands[MyIndex]).HasA(cardToPlay.Suit))
                        {
                            cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Suit == cardToPlay.Suit &&
                                                                                                i.Value == Hodnota.Eso);
                        }
                        return cardToPlay;
                    }
                    else
                    {
                        //Pokud akter ma jeste dalsi kartu od barvy, kterou hral (ukazal hlas)
                        //a muj spoluhrac bere stych esem nebo trumfem
                        //a pokud mam krome desitky jeste jinou kartu v barve
                        //tak si desitku setri na pozdeji aby prebijela akterova zbyvajiciho krale
                        if (//(_gameType & Hra.Kilo) == 0 &&
                            _probabilities.SuitProbability(player2, c1.Suit, RoundNumber) == 0 &&
                            Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                .Where(h => h != c1.Value &&
                                            h < Hodnota.Desitka)
                                .Any(h => _probabilities.CardProbability(player1, new Card(c1.Suit, h)) > 0) &&
                            hands[MyIndex].HasX(c1.Suit) &&
                            (hands[MyIndex].CardCount(c1.Suit) > 2 ||
                             (hands[MyIndex].CardCount(c1.Suit) > 1 &&
                              (_probabilities.CardProbability(player1, new Card(c1.Suit, Hodnota.Eso)) == 0 ||
                               (c1.Value == Hodnota.Eso &&
                                c1.IsLowerThan(c2, _trump))))) &&
                            ((_probabilities.PotentialCards(player1)
                                            .Where(j => j.Suit == c1.Suit)
                                            .Count(j => j != c1 &&
                                                        j != c2 &&
                                                        j.Value < c1.Value) > 1) ||
                            (c1.IsLowerThan(c2, _trump) &&
                             _probabilities.PotentialCards(player1)
                                           .Where(j => j.Suit == c1.Suit)
                                           .Count(j => j != c1 &&
                                                       j != c2 &&
                                                       j.Value < Hodnota.Desitka) > 1) ||
                             _probabilities.CertainCards(player1)
                                        .Where(j => j.Suit == c1.Suit)
                                        .Any(j => j != c1 &&
                                                    j != c2 &&
                                                    j.Value < c1.Value)))
                        {
                            return null;
                        }
                        //Pokud akter ma dalsi karty v barve kterou vyjizdel (vyjma esa)
                        //a pokud mam desitku a muj spoluhrac hral trumfem, tak desitku nehraj
                        //Pokud by spoluhracovi pozdeji dosly trumfy udrzi me pak desitka ve stychu
                        if (c1.Suit != _trump &&
                            c2.Suit == _trump &&
                            ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka) &&
                            ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka)
                                                              .All(i => (c1.IsLowerThan(c2, _trump) ||
                                                                         c1.IsLowerThan(i, _trump)) &&
                                                                        _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                        Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                            .Where(h => h < i.Value)
                                                                            .Select(h => new Card(i.Suit, h))
                                                                            .Where(j => j != c1)
                                                                            .Any(j => _probabilities.CardProbability(player1, j) >= 1 - _epsilon)))
                        {
                            return null;
                        }
                        //pocet souperovych trumfu vyssi nez muj nejvyssi trumf mensi nez X
                        var opHiTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
											 .Where(h => h > myHighestTrumpAfterX)
											 .Count(h => _probabilities.CardProbability(player1, new Card(_trump, h)) > _epsilon);

                        var cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Desitka &&
                                                                                      !hands[MyIndex].HasA(i.Suit) &&
                                                                                      !(_probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                        Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                            .Select(h => new Card(i.Suit, h))
                                                                                            .Where(j => j != c1)
                                                                                            .Any(j => _probabilities.CardProbability(player1, j) == 1)) &&
                                                                                      (i.Suit != _trump ||              //pokud to neni trumfova X
                                                                                       //(!hands[MyIndex].HasA(i.Suit) && //nebo pokud mam 2 a mene trumfu a nemam trumfove A
                                                                                        //hands[MyIndex].CardCount(i.Suit) <= 2 &&
                                                                                        //(c1.Suit != i.Suit ||           //a pokud nikdo nehral trumfove eso v tomto kole
                                                                                        // c1.Value != Hodnota.Eso) &&
                                                                                        //(c2.Suit != i.Suit ||
                                                                                         //c2.Value != Hodnota.Eso))) &&
                                                                                       (_probabilities.CardProbability(player1, new Card(_trump, Hodnota.Eso)) > _epsilon &&
                                                                                        hands[MyIndex].CardCount(_trump) <= 3 &&
                                                                                        (hands[MyIndex].CardCount(_trump) <= opHiTrumps + 1 ||
																						 ((_gameType & Hra.SedmaProti) != 0 &&
                                                                                          hands[MyIndex].Has7(_trump) &&
																						  hands[MyIndex].CardCount(_trump) <= opHiTrumps + 2)))) &&
                                                                                      Round.WinningCard(c1, c2, i, _trump) != c1);
                        if (cardToPlay != null &&
                            ValidCards(c1, c2, hands[MyIndex]).HasA(cardToPlay.Suit))
                        {
                            cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Suit == cardToPlay.Suit &&
                                                                                                i.Value == Hodnota.Eso);
                        }
                        return cardToPlay;
                    }
                }
            };

            yield return new AiRule
            {
                Order = 2,
                Description = "hraj vítěznou A",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    if (TeamMateIndex == -1)
                    {
                        //--c
                        return ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                      i.Suit != _trump &&
                                                                                      Round.WinningCard(c1, c2, i, _trump) == i &&
                                                                                      ((c1.Suit == i.Suit &&
                                                                                        c1.Value == Hodnota.Desitka) ||
                                                                                        _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon ||
                                                                                       (((_gameType & Hra.KiloProti) != 0) &&
                                                                                        _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon)) &&
                                                                                       ((c2.Suit == i.Suit &&
                                                                                        c2.Value == Hodnota.Desitka) ||
                                                                                        _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon ||
                                                                                       (((_gameType & Hra.KiloProti) != 0) &&
                                                                                        _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon)));
                    }
                    else if (TeamMateIndex == player1)
                    {
                        //o-c
                        //nehraj pokud ma prvni hrac jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (ValidCards(c1, c2, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Value == Hodnota.Eso) &&
                            ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso)
                                                          .All(i => i.Suit != _trump &&
                                                                    i.Suit != _trump &&
                                                                    i.Suit == c2.Suit &&
                                                                    ((_probabilities.SuitProbability(player1, i.Suit, RoundNumber) == 0 &&
                                                                      _probabilities.PotentialCards(player2)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1 &&
                                                                                                j != c2) > 2) ||
                                                                     _probabilities.CertainCards(player2)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1 &&
                                                                                                j != c2) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0))))
                        {
                            return null;
                        }
                        return ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                      i.Suit != _trump &&
                                                                                      Round.WinningCard(c1, c2, i, _trump) != c2 &&
                                                                                      ((c2.Suit == i.Suit &&
                                                                                        c2.Value == Hodnota.Desitka) ||
                                                                                        _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) == 0 ||
                                                                                       (((_gameType & Hra.Kilo) != 0) &&
                                                                                        _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon)) &&
                                                                                      !((_probabilities.SuitProbability(player1, i.Suit, RoundNumber) == 0 &&
                                                                                         _probabilities.PotentialCards(player2)
                                                                                                       .Where(j => j.Suit == i.Suit)
                                                                                                       .Count(j => j != c1 &&
                                                                                                                   j != c2) > 2) ||
                                                                                        _probabilities.CertainCards(player2)
                                                                                                      .Where(j => j.Suit == i.Suit)
                                                                                                      .Count(j => j != c1 &&
                                                                                                                  j != c2) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0)));
                    }
                    else
                    {
                        //-oc
                        //nehraj pokud ma prvni hrac jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (ValidCards(c1, c2, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Value == Hodnota.Eso) &&
                            ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso)
                                                          .All(i => i.Suit != _trump &&
                                                                    i.Suit != _trump &&
                                                                    i.Suit == c1.Suit &&
                                                                    ((_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0 &&
                                                                      _probabilities.PotentialCards(player1)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1) > 2) ||
                                                                     _probabilities.CertainCards(player1)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0))))
                        {
                            return null;
                        }
                        return ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                      i.Suit != _trump &&
                                                                                      Round.WinningCard(c1, c2, i, _trump) != c1 &&
                                                                                      ((c1.Suit == i.Suit &&
                                                                                        c1.Value == Hodnota.Desitka) ||
                                                                                        _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) == 0 ||
                                                                                       (((_gameType & Hra.Kilo) != 0) &&
                                                                                        _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon)) &&
                                                                                      !((_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0 &&
                                                                                         _probabilities.PotentialCards(player1)
                                                                                                       .Where(j => j.Suit == i.Suit)
                                                                                                       .Count(j => j != c1) > 2) ||
                                                                                        _probabilities.CertainCards(player1)
                                                                                                      .Where(j => j.Suit == i.Suit)
                                                                                                      .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0)));
                    }
                }
            };

			yield return new AiRule
			{
				Order = 3,
				Description = "odmazat si barvu",
				SkipSimulations = true,
				ChooseCard3 = (Card c1, Card c2) =>
				{
                    return ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                         i.Suit != c1.Suit &&
                                                                         !hands[MyIndex].HasSuit(_trump) &&
                                                                         i.Value != Hodnota.Eso &&
                                                                         i.Value != Hodnota.Desitka &&
                                                                         hands[MyIndex].Any(j => j.Value == Hodnota.Eso ||
                                                                                                 j.Value == Hodnota.Desitka) &&                                                                    
                                                                         hands[MyIndex].CardCount(i.Suit) == 1 &&
                                                                         !_bannedSuits.Contains(i.Suit))
                                                             .OrderBy(i => i.Value)
                                                             .FirstOrDefault();
				}
			};

            yield return new AiRule
            {
                Order = 4,
                Description = "hrát vysokou kartu mimo A,X",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    //hrat vysokou kartu, pokud hrozi, ze bych s ni mohl v budoucnu vytlacit spoluhracovu A nebo X kterou by pak souper vzal trumfem
                    if (TeamMateIndex != -1)
                    {
                        var opponent = TeamMateIndex == player1 ? player2 : player1;
                        var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                        i.Value != Hodnota.Eso &&
                                                                                        i.Value != Hodnota.Desitka &&
                                                                                        _probabilities.SuitProbability(opponent, i.Suit, RoundNumber) == 0 &&
                                                                                        _probabilities.SuitProbability(opponent, _trump, RoundNumber) > 0 &&
                                                                                        (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) > _epsilon ||
                                                                                         _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) > _epsilon));

                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }
                    return null;
                }
            };

            yield return new AiRule
            {
                Order = 5,
                Description = "hrát nízkou kartu mimo A,X",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    //preferuj barvu kde nemam A,X a kde soupere nechytam
                    var axPerSuit = new Dictionary<Barva, int>();
                    var holesPerSuit = new Dictionary<Barva, int>();
                    var hiCardsPerSuit = new Dictionary<Barva, int>();
                    var catchCardsPerSuit = new Dictionary<Barva, int>();

                    if (TeamMateIndex == -1)
                    {
                        //--c
                        foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                        {
                            var holes = 0;      //pocet der v barve
                            var hiCards = 0;    //pocet karet ktere maji pod sebou diru v barve
                            var axCount = ValidCards(c1, c2, hands[MyIndex]).Count(i => i.Suit == b &&
                                                                                        (i.Value == Hodnota.Desitka ||
                                                                                         i.Value == Hodnota.Eso));
                            foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                            {
                                var c = new Card(b, h);
                                var n = ValidCards(c1, c2, hands[MyIndex]).Count(i => i.Suit == b &&
                                                                                  i.Value > c.Value &&
                                                                                  (_probabilities.CardProbability(player1, c) > _epsilon ||
                                                                                   _probabilities.CardProbability(player2, c) > _epsilon));

                                if (n > 0)
                                {
                                    holes++;
                                    hiCards = Math.Max(hiCards, n);
                                }
                            }
                            axPerSuit.Add(b, axCount);
                            holesPerSuit.Add(b, holes);
                            hiCardsPerSuit.Add(b, hiCards);
                            catchCardsPerSuit.Add(b, Math.Min(hiCards, holes));
                        }
                    }
                    else
                    {
                        //-oc
                        //o-c
                        var opponent = TeamMateIndex == player1 ? player2 : player1;
                        foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                        {
                            var holes = 0;      //pocet der v barve
                            var hiCards = 0;    //pocet karet ktere maji pod sebou diru v barve
                            var axCount = ValidCards(c1, c2, hands[MyIndex]).Count(i => i.Suit == b &&
                                                                                        (i.Value == Hodnota.Desitka ||
                                                                                         i.Value == Hodnota.Eso));

                            foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                            {
                                var c = new Card(b, h);
                                var n = ValidCards(c1, c2, hands[MyIndex]).Count(i => i.Suit == b &&
                                                                                 i.Value > c.Value &&
                                                                                 _probabilities.CardProbability(opponent, c) > _epsilon);

                                if (n > 0)
                                {
                                    holes++;
                                    hiCards = Math.Max(hiCards, n);
                                }
                            }
                            axPerSuit.Add(b, axCount);
                            holesPerSuit.Add(b, holes);
                            hiCardsPerSuit.Add(b, hiCards);
                            catchCardsPerSuit.Add(b, Math.Min(hiCards, holes));
                        }
                    }
                    var cardsToPlay = new List<Card>();
                    var validSuits = ValidCards(c1, c2, hands[MyIndex]).Select(i => i.Suit).Distinct();
                    var catchCardsPerSuitNoAX = catchCardsPerSuit.Where(i => validSuits.Contains(i.Key) &&
                                                                             hands[MyIndex].Any(j => j.Suit == i.Key &&
                                                                                                     j.Value != Hodnota.Eso &&
                                                                                                     j.Value != Hodnota.Desitka) &&
                                                                             !(hands[MyIndex].HasX(i.Key) &&
                                                                               hands[MyIndex].CardCount(i.Key) == 2));
                    if (catchCardsPerSuitNoAX.Any())
                    {
                        var preferredSuit = catchCardsPerSuitNoAX.Where(i => !_bannedSuits.Contains(i.Key))
                                                                 .OrderBy(i => axPerSuit[i.Key])
                                                                 .ThenBy(i => i.Value)
                                                                 .Select(i => (Barva?)i.Key)
                                                                 .FirstOrDefault();

                        if (preferredSuit.HasValue)
                        {
                            cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit == preferredSuit.Value &&
                                                                                        i.Value != Hodnota.Eso &&
                                                                                        i.Value != Hodnota.Desitka &&
                                                                                        (hands[MyIndex].CardCount(i.Suit) == 1 ||
                                                                                         !hands[MyIndex].Where(j => j.Suit == i.Suit)
                                                                                                        .All(j => j.Value <= i.Value)))
                                                                                .ToList();
                        }
                        if (cardsToPlay.HasSuit(_trump) ||
                            cardsToPlay.All(i => i.Suit != c1.Suit) ||
                            (TeamMateIndex == player1 && c1.IsHigherThan(c2, _trump)) ||
                            (TeamMateIndex == player2 && c1.IsLowerThan(c2, _trump)))
                        {
                            return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                        }
                        else
                        {
                            if (cardsToPlay.Any(i => i.Value < Hodnota.Desitka))
                            {
                                cardsToPlay = cardsToPlay.Where(i => i.Value < Hodnota.Desitka).ToList();
                            }
                            return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                        }
                    }

                    if (cardsToPlay.Any(i => !_bannedSuits.Contains(i.Suit)))
                    {
                        cardsToPlay = cardsToPlay.Where(i => !_bannedSuits.Contains(i.Suit)).ToList();
                    }
                    if (cardsToPlay.HasSuit(_trump) ||
                        cardsToPlay.All(i => i.Suit != c1.Suit) ||
                        (TeamMateIndex == player1 && c1.IsHigherThan(c2, _trump)) ||
                        (TeamMateIndex == player2 && c1.IsLowerThan(c2, _trump)))
                    {
                        return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                    }
                    else
                    {
                        if (cardsToPlay.Any(i => i.Value < Hodnota.Desitka))
                        {
                            cardsToPlay = cardsToPlay.Where(i => i.Value < Hodnota.Desitka).ToList();
                        }
                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }
                }
            }; 

            yield return new AiRule
            {
                Order = 6,
                Description = "hrát nízkou kartu",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    if (TeamMateIndex != -1)
                    {
                        //preferuj barvu kde soupere nechytam
                        var holesPerSuit = new Dictionary<Barva, int>();
                        var hiCardsPerSuit = new Dictionary<Barva, int>();
                        var catchCardsPerSuit = new Dictionary<Barva, int>();
                        var opponent = TeamMateIndex == player1 ? player2 : player1;
                        var cardsToPlay = new List<Card>();

                        foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                        {
                            var holes = 0;      //pocet der v barve
                            var hiCards = 0;    //pocet karet ktere maji pod sebou diru v barve

                            foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                            {
                                var c = new Card(b, h);
                                var n = ValidCards(c1, c2, hands[MyIndex]).Count(i => i.Suit == b &&
                                                                                      i.Value > c.Value &&
                                                                                      _probabilities.CardProbability(opponent, c) > _epsilon);

                                if (n > 0)
                                {
                                    holes++;
                                    hiCards = Math.Max(hiCards, n);
                                }
                            }
                            holesPerSuit.Add(b, holes);
                            hiCardsPerSuit.Add(b, hiCards);
                            catchCardsPerSuit.Add(b, Math.Min(hiCards, holes));
                        }
                        var validSuits = ValidCards(c1, c2, hands[MyIndex]).Select(i => i.Suit).Distinct();
                        var catchCardsPerSuitNoAX = catchCardsPerSuit.Where(i => validSuits.Contains(i.Key) &&
                                                                                 hands[MyIndex].Any(j => j.Suit == i.Key &&
                                                                                                         j.Value != Hodnota.Eso &&
                                                                                                         j.Value != Hodnota.Desitka));
                        if (catchCardsPerSuitNoAX.Any())
                        {
                            var preferredSuit = catchCardsPerSuitNoAX.Where(i => !_bannedSuits.Contains(i.Key))
                                                                     .OrderBy(i => i.Value)
                                                                     .Select(i => (Barva?)i.Key)
                                                                     .FirstOrDefault();

                            if (preferredSuit.HasValue)
                            {
                                cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit == preferredSuit.Value &&
                                                                                            i.Value != Hodnota.Eso &&
                                                                                            i.Value != Hodnota.Desitka &&
                                                                                            (hands[MyIndex].CardCount(i.Suit) == 1 ||
                                                                                             !hands[MyIndex].Where(j => j.Suit == i.Suit)
                                                                                                            .All(j => j.Value <= i.Value)))
                                                                                .ToList();
                            }
                            if (cardsToPlay.Any(i => !_bannedSuits.Contains(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => !_bannedSuits.Contains(i.Suit)).ToList();
                            }
                            if (cardsToPlay.Any())
                            {
                                if (cardsToPlay.HasSuit(_trump) ||
                                    cardsToPlay.All(i => i.Suit != c1.Suit) ||
                                    (TeamMateIndex == player1 && c1.IsHigherThan(c2, _trump)) ||
                                    (TeamMateIndex == player2 && c1.IsLowerThan(c2, _trump)))
                                {
                                    return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                                }
                                else
                                {
                                    if (cardsToPlay.Any(i => i.Value < Hodnota.Desitka))
                                    {
                                        cardsToPlay = cardsToPlay.Where(i => i.Value < Hodnota.Desitka).ToList();
                                    }
                                    return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                                }
                            }
                        }
                    }

                    return ValidCards(c1, c2, hands[MyIndex]).OrderBy(i => i.Value).FirstOrDefault();
                }
            };
        }
    }
}
