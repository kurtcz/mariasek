using System;
using System.Data;
#if !PORTABLE
using System.Reflection;
#endif

namespace Mariasek.Engine
{
    public partial class AiStrategy : AiStrategyBase
    {
        private new Barva _trump { get { return base._trump.Value; } } //dirty
        protected HlasConsidered _hlasConsidered;
        public List<Barva> _bannedSuits = new List<Barva>();
        private List<Barva> _preferredSuits = new List<Barva>();
        public float RiskFactor { get; set; }
        public float RiskFactorSevenDefense { get; set; }
        public float SolitaryXThreshold { get; set; }
        public float SolitaryXThresholdDefense { get; set; }
        public Hra[] PlayerBids { get; set; }
        public int GameValue { get; set; }
        public int SevenValue { get; set; }
        protected CalculationStyle _calculationStyle;

        private const float _epsilon = 0.01f;

        public AiStrategy(Barva? trump, Hra gameType, Hand[] hands, Round[] rounds, List<Barva> teamMatesSuits, HlasConsidered hlasConsidered, CalculationStyle calculationStyle, Probability probabilities, IStringLogger debugString)
            : base(trump, gameType, hands, rounds, teamMatesSuits, probabilities, debugString)
        {
            _hlasConsidered = hlasConsidered;
            _calculationStyle = calculationStyle;
            if (!trump.HasValue)
            {
                throw new InvalidOperationException("AiStrategy2: trump is null");
            }
            RiskFactor = 0.275f; //0.2727f ~ (9 nad 5) / (11 nad 5)
            SolitaryXThreshold = 0.13f;
            SolitaryXThresholdDefense = 0.5f;
            RiskFactorSevenDefense = 0.5f;
        }

        private void BeforeGetRules(Hand[] hands)
        {
            void ban(Barva suit)
            {
                if (!_bannedSuits.Contains(suit) &&
                    hands[MyIndex].SuitCount - 1 > _bannedSuits.Count)
                {
                    _bannedSuits.Add(suit);
                }
            }

            void banRange(IEnumerable<Barva> suits)
            {
                foreach (var b in suits)
                {
                    ban(b);
                }
            }

            _bannedSuits.Clear();
            _preferredSuits.Clear();
            //u sedmy mi nevadi, kdyz ze spoluhrace tlacim desitky a esa, snazim se hlavne hrat proti sedme
            if (TeamMateIndex != -1)
            {
                var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                                ? (MyIndex + 2) % Game.NumPlayers
                                : (MyIndex + 1) % Game.NumPlayers;

                var teamMateLackingSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .Where(b => b != _trump &&
                                                           (_probabilities.PotentialCards(opponent).HasSuit(b) ||
                                                            PlayerBids[TeamMateIndex] != 0 ||
                                                            (_gameType & Hra.Kilo) != 0) &&
                                                           !_probabilities.PotentialCards(TeamMateIndex).HasSuit(b) &&
                                                           _probabilities.PotentialCards(TeamMateIndex).HasSuit(_trump) &&
                                                           !(GameValue > SevenValue &&
                                                             !_probabilities.PotentialCards(TeamMateIndex).HasSuit(b) &&
                                                             _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) >= 1 - RiskFactor &&
                                                             _probabilities.HasAOrXAndNothingElse(opponent, b, RoundNumber) == 1))
                                               .ToList();
                if (GameValue > SevenValue)
                {

                    if (PlayerBids[MyIndex] == 0)
                    {
                        var suits = ((List<Card>)hands[MyIndex]).Select(i => i.Suit).Distinct();

                        if (suits.Where(i => i != _trump &&
                                             !_bannedSuits.Contains(i))
                                 .Any(i => !teamMateLackingSuits.Contains(i)))
                        {
                            banRange(teamMateLackingSuits);
                        }
                    }
                    //nehraj barvu pokud mam eso a souper muze mit desitku
                    foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                         .Where(b => b != _trump &&
                                                     _probabilities.CardProbability(MyIndex, new Card(b, Hodnota.Eso)) == 1 &&
                                                     _probabilities.CardProbability(opponent, new Card(b, Hodnota.Desitka)) > _epsilon))// &&
                                                                                                                                        //_probabilities.HasSolitaryX(TeamMateIndex, b, RoundNumber) < SolitaryXThreshold))
                    {
                        ban(b);
                    }
                }

                //pokud se nehraje sedma (proti), tak nehraj barvu
                //ve ktere na spoluhrac A nebo X a souper barvu nezna a ma trumfy
                //if ((_gameType & Hra.Hra) != 0 &&
                //    (_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 &&
                //    _probabilities.SuitProbability(opponent, _trump, RoundNumber) > 0)
                //{
                //    foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>()
                //                          .Where(b => _probabilities.SuitProbability(opponent, b, RoundNumber) == 0 &&
                //                                      (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Eso)) >= 1 - _epsilon ||
                //                                       _probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Desitka)) >= 1 - _epsilon)))
                //    {
                //        ban(b);
                //    }
                //}

                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>().Where(b => b != _trump))
                {
                    //nehraj barvy kde ma kolega samotne AX a akter by bral barvu trumfem
                    if (_probabilities.HasAOrXAndNothingElse(TeamMateIndex, b, RoundNumber) >= 1 - RiskFactor &&
                        _probabilities.SuitProbability(opponent, b, RoundNumber) == 0 &&
                        _probabilities.SuitProbability(opponent, _trump, RoundNumber) > 0)
                    {
                        ban(b);
                    }
                    //nehraj barvu kterou kolega nezna a akter ma jiste na ruce nizkou kartu v barve
                    if (_probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) == 0 &&
                        hands[MyIndex].Where(i => i.Suit == b)
                                      .Any(i => _probabilities.SuitLowerThanCardProbability(opponent, i, RoundNumber) == 1))
                    {
                        ban(b);
                    }
                    //nehraj barvu kterou kolega urcite nezna pokud muze mit trumfy
                    if (PlayerBids[MyIndex] == 0 &&
                        _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) == 0 &&
                        _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) > 0)
                    {
                        ban(b);
                    }

                    //pokud kolega flekoval nehraj barvu kterou mozna nezna pokud muze mit trumfy
                    if (PlayerBids[TeamMateIndex] != 0 &&
                        _probabilities.PotentialCards(TeamMateIndex).CardCount(b) <= 1 &&
                        !_probabilities.LikelyCards(TeamMateIndex).HasSuit(b) &&
                        _probabilities.SuitProbability(opponent, b, RoundNumber) > 0 &&
                        _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) > 0)
                    {
                        ban(b);
                    }
                }

                foreach (var r in _rounds.Where(i => i != null && i.c3 != null))
                {
                    //proti sedme nehraj akterovy barvy pokud je stale muze mit
                    if (TeamMateIndex != -1 &&
                        r.player1.TeamMateIndex == -1 &&
                        (_gameType & Hra.Sedma) != 0 &&
                        (_gameType & Hra.Hra) != 0 &&
                        _probabilities.PotentialCards(opponent).HasSuit(r.c1.Suit))
                    {
                        //neplati pokud mas od dane barvy nejvyssi karty
                        if (!((_probabilities.PotentialCards(TeamMateIndex).HasSuit(r.c1.Suit) ||
                               !_probabilities.PotentialCards(TeamMateIndex).HasSuit(_trump)) &&
                              hands[MyIndex].Where(i => i.Suit == r.c1.Suit)
                                            .Count(i => _probabilities.PotentialCards(opponent)
                                                                      .Where(j => j.Suit == i.Suit)
                                                                      .All(j => j.Value < i.Value)) >= _probabilities.PotentialCards(opponent).CardCount(r.c1.Suit)))
                        {
                            ban(r.c1.Suit);
                        }
                    }
                    //nehraj barvy ktere tlaci z kolegy trumfy
                    if ((_gameType & Hra.Kilo) == 0 &&
                        r.player2.PlayerIndex == TeamMateIndex &&
                        r.c2.Suit != r.c1.Suit &&
                        r.c2.Suit == _trump)
                    {
                        //neplati pokud kolega neflekoval nebo uz nema trumfy nebo
                        //tak jiste vytlacis ze soupere ostrou kartu
                        if (PlayerBids[TeamMateIndex] != 0 &&
                            _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) > 0 &&
                            _probabilities.HasAOrXAndNothingElse(r.player3.PlayerIndex, r.c1.Suit, RoundNumber) < 1)
                        {
                            //pri sedme (proti) je toto dulezitejsi nez jina pravidla.
                            //proto pokud je to nutne nejdrive odstran drivejsi zakazanou barvu
                            if (SevenValue >= GameValue &&
                                _bannedSuits.Any() &&
                                hands[MyIndex].SuitCount - 1 == _bannedSuits.Count)
                            {
                                _bannedSuits.RemoveAt(_bannedSuits.Count - 1);
                            }
                            ban(r.c1.Suit);
                        }
                    }
                    else if (r.player3.PlayerIndex == TeamMateIndex &&
                        r.c3.Suit != r.c1.Suit &&
                        r.c3.Suit == _trump)
                    {
                        if (PlayerBids[TeamMateIndex] != 0 &&
                            _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) > 0 &&
                            _probabilities.HasAOrXAndNothingElse(r.player2.PlayerIndex, r.c1.Suit, RoundNumber) < 1)
                        {
                            if (SevenValue >= GameValue &&
                                _bannedSuits.Any() &&
                                hands[MyIndex].SuitCount - 1 == _bannedSuits.Count)
                            {
                                _bannedSuits.RemoveAt(_bannedSuits.Count - 1);
                            }
                            ban(r.c1.Suit);
                        }
                    }
                    //nehraj akterovu barvu pokud mam vic nez 2 karty v barve a akter barvu zna
                    //a existuje jina barva kterou muzu hrat
                    if ((_gameType & Hra.Kilo) == 0 &&
                        r.player1.TeamMateIndex == -1 &&
                        hands[MyIndex].CardCount(r.c1.Suit) > 2 &&   //pokud mam jen jednu nebo dve karty v barve, dovol mi ji odmazat
                        _probabilities.SuitProbability(r.player1.PlayerIndex, r.c1.Suit, RoundNumber) > 0 &&  //pouze pokud akter barvu zna, jinak je barva bezpecna
                        !(hands[MyIndex].Where(i => i.Suit == r.c1.Suit)              //bezpecna je i pokud mam nejvyssi karty v barve ja
                                        .Count(i => _probabilities.PotentialCards(opponent)
                                                                  .Where(j => j.Suit == i.Suit)
                                                                  .All(j => j.Value < i.Value)) >= _probabilities.PotentialCards(opponent).CardCount(r.c1.Suit)) &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => hands[MyIndex].HasSuit(b) &&
                                        b != _trump &&
                                        b != r.c1.Suit)
                            .Any(b => !_bannedSuits.Contains(b)))
                    {
                        ban(r.c1.Suit);
                    }
                    if (r.player2.TeamMateIndex == -1 &&
                        (r.c2.Value == Hodnota.Eso ||
                         r.c2.Value == Hodnota.Desitka) &&
                        r.roundWinner.PlayerIndex != r.player2.PlayerIndex)
                    {
                        _preferredSuits.Add(r.c2.Suit);
                    }
                    else if (r.player3.TeamMateIndex == -1 &&
                        (r.c3.Value == Hodnota.Eso ||
                         r.c3.Value == Hodnota.Desitka) &&
                        r.roundWinner.PlayerIndex != r.player3.PlayerIndex)
                    {
                        _preferredSuits.Add(r.c3.Suit);
                    }
                    //if (r.player1.TeamMateIndex == -1 &&
                    //    r.player2.PlayerIndex == TeamMateIndex &&
                    //    r.c1.Suit != r.c2.Suit &&
                    //    r.c2.Suit != _trump &&
                    //    r.c2.Value >= Hodnota.Desitka &&
                    //    r.roundWinner.PlayerIndex != r.player1.PlayerIndex)
                    //{
                    //    _preferredSuits.Add(r.c2.Suit);
                    //}
                    //else if (r.player1.TeamMateIndex == -1 &&
                    //         r.player3.PlayerIndex == TeamMateIndex &&
                    //         r.c1.Suit != r.c3.Suit &&
                    //         r.c3.Suit != _trump &&
                    //         r.c3.Value >= Hodnota.Desitka &&
                    //         r.roundWinner.PlayerIndex != r.player1.PlayerIndex)
                    //{
                    //    _preferredSuits.Add(r.c3.Suit);
                    //}

                    //deje se v AiPlayer.UpdateProbabilitiesAfterCardPlayed()
                    //if (r.player1.TeamMateIndex == MyIndex)
                    //{
                    //    _teamMatesSuits.Add(r.c1.Suit);
                    //}

                    var teamMatesLikelyAXSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Where(b => hands[MyIndex].HasSuit(b) &&
                                             (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Eso)) > _epsilon ||
                                              _probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Desitka)) > _epsilon))
                           .ToList();

                    //nehraj akterovu barvu pokud existuje jina barva kterou muzu hrat
                    if (r.player1.TeamMateIndex == -1 &&
                        RoundNumber <= 4 &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => hands[MyIndex].HasSuit(b) &&
                                        b != _trump &&
                                        b != r.c1.Suit)
                            .Any(b => !_bannedSuits.Contains(b) &&
                                      !teamMatesLikelyAXSuits.Contains(b)))
                    {
                        ban(r.c1.Suit);
                    }
                }
                if (TeamMateIndex != -1)
                {
                    var teamMatesLikelyAXSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                     .Where(b => hands[MyIndex].HasSuit(b) &&
                                                                 (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Eso)) > _epsilon ||
                                                                  _probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Desitka)) > _epsilon))
                                               .ToList();
                    if (teamMatesLikelyAXSuits.Count == 1 &&
                        hands[MyIndex].CardCount(teamMatesLikelyAXSuits.First()) > 1 &&
                        teamMatesLikelyAXSuits.Any(b => !_teamMatesSuits.Contains(b)))
                    {
                        ban(teamMatesLikelyAXSuits.First(b => !_teamMatesSuits.Contains(b)));
                    }
                }
                if (_gameType != (Hra.Hra | Hra.Sedma))
                {
                    foreach (var r in _rounds.Where(i => i != null && i.c3 != null))
                    {
                        //pokud kolega hral vejs esem nebo desitkou ale stych za nim nesel tak barvu nehraj

                        //if (r.player1.PlayerIndex == TeamMateIndex &&
                        //    (r.c1.Value == Hodnota.Eso ||
                        //     r.c1.Value == Hodnota.Desitka) &&
                        //    (_probabilities.CardProbability(r.player1.PlayerIndex, new Card(r.c1.Suit, Hodnota.Eso)) > _epsilon ||
                        //     _probabilities.CardProbability(r.player1.PlayerIndex, new Card(r.c1.Suit, Hodnota.Desitka)) > _epsilon) &&
                        //    r.roundWinner.PlayerIndex != TeamMateIndex &&
                        //    r.roundWinner.PlayerIndex != MyIndex)
                        //{
                        //    ban(r.c1.Suit);
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
                            ban(r.c2.Suit);
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
                            ban(r.c3.Suit);
                        }
                        if (r.player3.TeamMateIndex == MyIndex &&
                            (r.c3.Value == Hodnota.Eso ||
                             r.c3.Value == Hodnota.Desitka) &&
                            r.roundWinner.PlayerIndex != TeamMateIndex &&
                            r.roundWinner.PlayerIndex != MyIndex)
                        {
                            ban(r.c3.Suit);
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
                                                             hands[MyIndex].HasSuit(b) &&
                                                             (_probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Eso)) >= 1 - _epsilon ||
                                                              _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Desitka)) >= 1 - _epsilon) &&
                                                             !(GameValue > SevenValue &&
                                                               _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) == 0 &&
                                                               _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) >= 1 - RiskFactor &&
                                                               _probabilities.HasAOrXAndNothingElse(opponent, b, RoundNumber) == 1))
                                                 .ToList();
                        // nehraj barvu kde nemas A ani X a zatim je nikdo nehral
                        var noAXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Where(b => b != _trump &&
                                                        !hands[MyIndex].HasA(b) &&
                                                        !hands[MyIndex].HasX(b) &&
                                                        (_probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Eso)) > 0 ||
                                                         _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Eso)) > 0) &&
                                                        (_probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Desitka)) > 0 ||
                                                         _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Desitka)) > 0) &&
                                                        !(GameValue > SevenValue &&
                                                          _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) == 0 &&
                                                          _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) >= 1 - RiskFactor &&
                                                          _probabilities.HasAOrXAndNothingElse(opponent, b, RoundNumber) == 1))
                                            .ToList();
                        // nehraj barvu kde mas A + neco ale ne X, pokud X muze mit akter
                        var AnoXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Where(b => b != _trump &&
                                                        hands[MyIndex].HasA(b) &&
                                                        hands[MyIndex].CardCount(b) > 1 &&
                                                        _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Desitka)) > _epsilon)
                                            .ToList();
                        var AKnoXsuits = AnoXsuits.Where(b => hands[MyIndex].HasK(b))
                                                  .ToList();
                        // nehraj barvu kde nemas A nebo X a zatim je nikdo nehral
                        //var noAorXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        //                    .Where(b => b != _trump &&
                        //                                (!hands[MyIndex].HasA(b) ||
                        //                                 !hands[MyIndex].HasX(b)) &&
                        //                                (_probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Eso)) > 0 ||
                        //                                 _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Eso)) > 0 ||
                        //                                 _probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Desitka)) > 0 ||
                        //                                 _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Desitka)) > 0) &&
                        //                                !(GameValue > SevenValue &&
                        //                                  _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) == 0 &&
                        //                                  _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) >= 1 - RiskFactor))
                        //                    .ToList();
                        // nehraj barvu kterou kolega nezna pokud muze mit trumfy
                        //var teamMateLackingSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        //                               .Where(b => b != _trump &&
                        //                                           _probabilities.SuitProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, b, RoundNumber) > 0 &&
                        //                                           _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) == 0 &&
                        //                                           _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) > 0 &&
                        //                                           !(GameValue > SevenValue &&
                        //                                             _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) == 0 &&
                        //                                             _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) >= 1 - RiskFactor &&
                        //                                             _probabilities.HasAOrXAndNothingElse(opponent, b, RoundNumber) == 1))
                        //                               .ToList();
                        var suits = ((List<Card>)hands[MyIndex]).Select(i => i.Suit).Distinct();

                        banRange(opponentXsuits);
                        //if (suits.Where(i => i != _trump &&
                        //                     !_bannedSuits.Contains(i))
                        //         .Any(i => !teamMateLackingSuits.Contains(i)))
                        //{
                        //    banRange(teamMateLackingSuits);
                        //}
                        if (!(SevenValue >= GameValue &&
                              (PlayerBids[TeamMateIndex] & (Hra.Sedma | Hra.SedmaProti)) != 0))
                        {
                            //pokud mam nejakou barvu s ostryma, tak nehraj plonkove barvy
                            if (suits.Where(i => i != _trump &&
                                                 !_bannedSuits.Contains(i))
                                     .Any(i => !noAXsuits.Contains(i)))
                            {
                                banRange(noAXsuits);
                            }
                            //pokud nemusis, tak nehraj barvu kde mas AK bez X
                            if (suits.Where(i => i != _trump &&
                                                 !_bannedSuits.Contains(i))
                                     .Any(i => !AKnoXsuits.Contains(i)))
                            {
                                banRange(AKnoXsuits);
                            }
                            //pokud nemusis, tak nehraj barvu kde mas A bez X
                            if (suits.Where(i => i != _trump &&
                                                 !_bannedSuits.Contains(i))
                                     .Any(i => !AnoXsuits.Contains(i)))
                            {
                                banRange(AnoXsuits);
                            }
                            //if (suits.Where(i => i != _trump &&
                            //                     !_bannedSuits.Contains(i))
                            //         .Any(i => !noAorXsuits.Contains(i)))
                            //{
                            //    banRange(noAorXsuits);
                            //}
                        }
                    }
                    else
                    {
                        //2. obrance
                        //nehraj barvu, kde vis, ze akter ma jiste bodovanou kartu
                        var opponentXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                 .Where(b => b != _trump &&
                                                             hands[MyIndex].HasSuit(b) &&
                                                             (_probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Eso)) >= 1 - _epsilon ||
                                                              _probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Desitka)) >= 1 - _epsilon))
                                                 .ToList();
                        // nehraj barvu kterou kolega nezna pokud muze mit trumfy
                        //var teamMateLackingSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        //                               .Where(b => b != _trump &&
                        //                                           _probabilities.SuitProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, b, RoundNumber) > 0 &&
                        //                                           _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) == 0 &&
                        //                                           _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) > 0);
                        var suits = ((List<Card>)hands[MyIndex]).Select(i => i.Suit).Distinct();

                        banRange(opponentXsuits);
                        if (suits.Where(i => i != _trump &&
                                             !_bannedSuits.Contains(i))
                                 .Any(i => !teamMateLackingSuits.Contains(i)))
                        {
                            banRange(teamMateLackingSuits);
                        }
                        // nehraj barvu kde mas A + neco ale ne X, pokud X muze mit akter
                        var AnoXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Where(b => b != _trump &&
                                                        hands[MyIndex].HasA(b) &&
                                                        hands[MyIndex].CardCount(b) > 1 &&
                                                        _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Desitka)) > _epsilon)
                                            .ToList();
                        var AKnoXsuits = AnoXsuits.Where(b => hands[MyIndex].HasK(b))
                                                  .ToList();
                        if (suits.Where(i => i != _trump &&
                                             !_bannedSuits.Contains(i))
                                 .Any(i => !AKnoXsuits.Contains(i)))
                        {
                            banRange(AKnoXsuits);
                        }
                        if (suits.Where(i => i != _trump &&
                                             !_bannedSuits.Contains(i))
                                 .Any(i => !AnoXsuits.Contains(i)))
                        {
                            banRange(AnoXsuits);
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
                        ban(r.c1.Suit);
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
                        ban(r.c1.Suit);
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
                        ban(r.c1.Suit);
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
                if (hands[MyIndex].HasX(b) &&
                    hands[MyIndex].CardCount(b) == 2 &&
                    (_probabilities.CardProbability((MyIndex + 1) % Game.NumPlayers, new Card(b, Hodnota.Eso)) > 0 ||
                     _probabilities.CardProbability((MyIndex + 2) % Game.NumPlayers, new Card(b, Hodnota.Eso)) > 0))
                {
                    ban(b);
                }
            }
            //_bannedSuits = _bannedSuits.Distinct().ToList();
            if (hands[MyIndex].Where(i => i.Suit != _trump &&
                                           i.Value < Hodnota.Desitka)
                               .All(i => _bannedSuits.Contains(i.Suit)))// &&
                                                                        //(!hands[MyIndex].HasSuit(_trump) ||
                                                                        // !(_rounds[RoundNumber - 1] != null &&   //1. obrance muze hrat trumf, pokud nelze hrat nic jineho, ostatni hraci ne
                                                                        //    _rounds[RoundNumber - 1].player1.PlayerIndex == MyIndex &&
                                                                        //    _rounds[RoundNumber - 1].player2.PlayerIndex == TeamMateIndex)))
            {
                _bannedSuits.Clear();
                //pokus se nektere barvy vratit zpet na seznam zakazanych barev
                if (TeamMateIndex != -1 && _rounds[RoundNumber - 1] != null)
                {
                    var nonTrumpSuitCount = hands[MyIndex].SuitCount - (hands[MyIndex].HasSuit(_trump) ? 1 : 0);

                    //proti sedme nehraj akterovy barvy
                    if ((_gameType & Hra.Sedma) != 0 &&
                        (_gameType & Hra.Hra) != 0)
                    {
                        var opponentSuits = _rounds.Where(i => i != null &&
                                                               i.c3 != null &&
                                                               i.player1.TeamMateIndex == -1)
                                                   .Select(i => i.c1.Suit)
                                                   .Distinct()
                                                   .ToList();

                        banRange(opponentSuits.Where(b => hands[MyIndex].HasSuit(b))
                                              .Take(nonTrumpSuitCount - 1)); //toto zajisti, ze zustane aspon jedna barva kterou muzu hrat
                    }
                    if (_bannedSuits.Count() <= 1) //pokud zustavaji jeste aspon 2 netrumfove barvy ktere nejsou zakazane
                    {
                        if (_rounds[RoundNumber - 1].player1.PlayerIndex == MyIndex &&
                            _rounds[RoundNumber - 1].player2.PlayerIndex == TeamMateIndex)
                        {
                            // co-
                            //nehraj barvu, kde vis, ze akter ma jiste bodovanou kartu
                            var opponentXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                     .Where(b => b != _trump &&
                                                                 hands[MyIndex].HasSuit(b) &&
                                                                 (_probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Eso)) >= 1 - _epsilon ||
                                                                  _probabilities.CardProbability(_rounds[RoundNumber - 1].player3.PlayerIndex, new Card(b, Hodnota.Desitka)) >= 1 - _epsilon))
                                                     .ToList();
                            banRange(opponentXsuits.Take(nonTrumpSuitCount));
                        }
                        else
                        {
                            // c-o
                            //nehraj barvu, kde vis, ze akter ma jiste bodovanou kartu
                            var opponentXsuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                     .Where(b => b != _trump &&
                                                                 hands[MyIndex].HasSuit(b) &&
                                                                 (_probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Eso)) >= 1 - _epsilon ||
                                                                  _probabilities.CardProbability(_rounds[RoundNumber - 1].player2.PlayerIndex, new Card(b, Hodnota.Desitka)) >= 1 - _epsilon))
                                                     .ToList();
                            banRange(opponentXsuits.Take(nonTrumpSuitCount));
                        }
                    }
                }
            }
        }

        private void BeforeGetRules23(Hand[] hands)
        {
            _bannedSuits.Clear();

            if (TeamMateIndex != -1)
            {
                var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                    ? (MyIndex + 2) % Game.NumPlayers
                    : (MyIndex + 1) % Game.NumPlayers;

                //if (_rounds?[RoundNumber] != null &&
                //    _rounds[RoundNumber].c2 == null &&
                //    (TeamMateIndex == -1 ||
                //     _rounds[RoundNumber].player3.PlayerIndex == opponent))
                //{
                //    //nehraj barvu pokud mam eso a souper muze mit desitku
                //    foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>()
                //                          .Where(b => b != _trump &&
                //                                      _probabilities.CardProbability(MyIndex, new Card(b, Hodnota.Eso)) == 1 &&
                //                                      _probabilities.CardProbability(opponent, new Card(b, Hodnota.Desitka)) > _epsilon))
                //    {
                //        _bannedSuits.Add(b);
                //    }
                //}
                //nehraj barvu kterou akter nezna (muzu s ni pozdeji vytlacit trumf)
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                      .Where(b => b != _trump &&
                                                  _probabilities.SuitProbability(opponent, b, RoundNumber) == 0 &&
                                                  _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) > 0))
                {
                    if (!_bannedSuits.Contains(b))
                    {
                        _bannedSuits.Add(b);
                    }
                }
                //nehraj barvu pokud mam vyssi v barve nez souper a kolega barvu nezna
                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                      .Where(b => b != _trump &&
                                                  _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) == 0 &&
                                                  _probabilities.PotentialCards(opponent).HasSuit(b) &&
                                                  _probabilities.PotentialCards(opponent).Where(i => i.Suit == b)
                                                                                         .Any(i => hands[MyIndex].Any(j => j.Suit == i.Suit &&
                                                                                                                           j.Value > i.Value))))
                {
                    if (!_bannedSuits.Contains(b))
                    {
                        _bannedSuits.Add(b);
                    }
                }
            }
        }

        private List<Card> GetUnwinnableLowCards()
        {
            var unwinnableLowCards = new List<Card>();

            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                unwinnableLowCards.AddRange(GetUnwinnableLowCards(b));
            }

            return unwinnableLowCards;
        }

        private List<Card> GetUnwinnableLowCards(Barva b)
        {
            var player2 = (MyIndex + 1) % Game.NumPlayers;
            var player3 = (MyIndex + 2) % Game.NumPlayers;

            if (!_hands[MyIndex].HasSuit(b))
            {
                return new List<Card>();
            }

            var myCards = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                              .OrderByDescending(h => (int)h)
                              .Select(h => new KeyValuePair<Hodnota, bool>(h, _hands[MyIndex].Any(j => j.Suit == b &&
                                                                                                       j.Value == h)))
                              .ToList();
            var opCards = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()   //vsechny hodnoty ktere v dane barve neznam
                              .OrderByDescending(h => (int)h)
                              .Select(h => new KeyValuePair<Hodnota, bool>(h, _probabilities.PotentialCards(player2).Any(i => i.Suit == b && i.Value == h) ||
                                                                              _probabilities.PotentialCards(player3).Any(i => i.Suit == b && i.Value == h)))
                              .ToList();
            var totalHoles = 0;
            var unwinnableLowCards = new List<Card>();

            for (var i = 0;
                 i < myCards.Count &&            //prochazej vsechny karty v barve odshora, skonci kdyz
                 opCards.Any(j => j.Value) &&    //souperum nezbyly zadne karty nebo
                 myCards.Skip(i)
                        .Any(j => j.Value);      //ja nemam na ruce zadne dalsi karty
                 i++)
            {
                if (myCards[i].Value)
                {
                    if (i > 0 &&
                        opCards.Take(i)
                               .Any(j => j.Value))
                    {
                        //nemame nejvyssi kartu
                        if (myCards[i].Key == Hodnota.Desitka &&
                            myCards.Count(i => i.Value) > 1)
                        {
                            continue;   //desitku neobetuj pokud nemusis
                        }
                        //odeberu nejblizsi diru vyssi nez moje aktualni karta
                        var opHighCardIndex = opCards.Take(i)
                                                     .ToList()
                                                     .FindLastIndex(j => j.Value);

                        opCards[opHighCardIndex] = new KeyValuePair<Hodnota, bool>(opCards[opHighCardIndex].Key, false);
                        totalHoles++;
                        unwinnableLowCards.Add(new Card(b, myCards[i].Key));
                    }
                    else
                    {
                        //mame nejvyssi kartu, odeberu diru odspoda
                        var loHoleIndex = opCards.FindLastIndex(j => j.Value);

                        opCards[loHoleIndex] = new KeyValuePair<Hodnota, bool>(opCards[loHoleIndex].Key, false);
                    }
                    myCards[i] = new KeyValuePair<Hodnota, bool>(myCards[i].Key, false); ; //oznac moji kartu jako odehranou
                    if (i > 1 &&
                        myCards[1].Value && //pokud jsme vynechali desitku
                        !opCards[0].Value)  //a povedlo se nam vytlacit eso
                    {
                        i = 0;              //tak pokracuj od znovu desitky
                    }
                }
            }

            //return totalHoles;
            return unwinnableLowCards;
        }

        private void LogIfNull(object obj, string name)
        {
            if (obj == null)
            {
                _debugString.AppendLine($"{name} is null");
            }
            else if (obj is List<Card>)
            {
                var list = obj as List<Card>;
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] == null)
                    {
                        _debugString.AppendLine($"{name}[{i}] is null");
                    }
                }
            }
            else if (obj is Dictionary<Barva, List<Card>>)
            {
                var dict = obj as Dictionary<Barva, List<Card>>;
                foreach (var key in dict.Keys)
                {
                    for (var i = 0; i < dict[key].Count; i++)
                    {
                        if (dict[key][i] == null)
                        {
                            _debugString.AppendLine($"{name}[{key}][{i}] is null");
                        }
                    }
                }
            }
        }
    }
}