using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Serialization.Json;
using System.Text;
using Mariasek.Engine.Logger;

namespace Mariasek.Engine
{
	public partial class AiStrategy
	{

        //: - souper
        //: o spoluhrac
        //: c libovolna karta
        //: X desitka
        //: A eso

        protected override IEnumerable<AiRule> GetRules2(Hand[] hands)
        {
            #region Init Variables2
            var player3 = (MyIndex + 1) % Game.NumPlayers;
            var player1 = (MyIndex + 2) % Game.NumPlayers;
            var opponent = TeamMateIndex == player1 ? player3 : player1;
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

            var actorPlayedCards = TeamMateIndex != -1
                          ? _rounds.Where(r => r != null && r.c3 != null)
                                 .Select(r =>
                                 {
                                     if (r.player1.PlayerIndex == opponent)
                                     {
                                         return r.c1;
                                     }
                                     else if (r.player2.PlayerIndex == opponent)
                                     {
                                         return r.c2;
                                     }
                                     else
                                     {
                                         return r.c3;
                                     }
                                 }).ToList()
                           : new List<Card>();
            var unwinnableLowCards = GetUnwinnableLowCards();
            var opponentCards = TeamMateIndex == -1
                    ? _probabilities.PotentialCards(player1).Concat(_probabilities.PotentialCards(player3)).Distinct().ToList()
                    : _probabilities.PotentialCards(opponent).ToList();
            var opponentTrumps = opponentCards.Where(i => i.Suit == _trump).ToList();

            //holes zavisi jen na dirach vuci souperum narozdil od holesPerSuit ktery zavisi na vsech mych dirach
            var holes = TeamMateIndex == -1
                        ? Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                              .Where(h => _probabilities.CardProbability(player1, new Card(_trump, h)) > (h == Hodnota.Eso ? 0 : _epsilon) ||
                                          _probabilities.CardProbability(player3, new Card(_trump, h)) > (h == Hodnota.Eso ? 0 : _epsilon))
                              .ToList()
                        : Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                              .Where(h => _probabilities.CardProbability(opponent, new Card(_trump, h)) > (h == Hodnota.Eso ? 0 : _epsilon))
                              .ToList();
            var topTrumps = hands[MyIndex].Where(i => i.Suit == _trump && holes.All(h => h < i.Value)).ToList();

            var potentialGreaseCards = TeamMateIndex != -1 &&
                                       hands[MyIndex].CardCount(_trump) <= 1 &&
                                       Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                           .Any(b => b != _trump &&
                                                     (!hands[MyIndex].HasSuit(b) ||
                                                      (hands[MyIndex].CardCount(b) <= 1 &&
                                                       RoundNumber <= 5)) &&
                                                     _probabilities.PotentialCards(opponent).HasSuit(b) &&
                                                     _probabilities.PotentialCards(TeamMateIndex).HasSuit(b))
                                       ? hands[MyIndex].Where(i => i.Suit != _trump &&
                                                                   i.Value >= Hodnota.Desitka &&
                                                                   !(!_probabilities.PotentialCards(opponent).HasA(i.Suit) &&
                                                                     (_probabilities.CertainCards(opponent).Any(j => j.Suit == i.Suit &&
                                                                                                                     j.Value < Hodnota.Desitka) ||
                                                                      (!_probabilities.PotentialCards(TeamMateIndex).HasSuit(i.Suit) &&
                                                                       _probabilities.PotentialCards(opponent).CardCount(i.Suit) > 2) ||
                                                                      (!_probabilities.PotentialCards(TeamMateIndex).HasSuit(i.Suit) &&
                                                                       _probabilities.PotentialCards(opponent).HasSuit(i.Suit) &&
                                                                       !actorPlayedCards.HasA(i.Suit) &&    //pokud akter hral kartu v barve a nemel ani nema eso
                                                                       actorPlayedCards.HasSuit(i.Suit))))) //a pokud muze stale barvu mit, tak ji asi nedal do talonu
                                                       .ToList()
                                       : new List<Card>();
            #endregion

            BeforeGetRules23(hands);
            if (RoundNumber == 9)
            {
                yield return new AiRule()
                {
                    Order = 0,
                    Description = "hrát tak abych bral poslední štych",
                    #region ChooseCard2 Rule0
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
                        //pokud muzes hrat viteznou desitku, tak pravidlo nehraj
                        if (TeamMateIndex == player3 &&
                            ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka &&
                                                                    !c1.IsHigherThan(i, _trump) &&
                                                                    _probabilities.PotentialCards(player1).HasA(i.Suit)))
                        {
                            return null;
                        }
                        //pri kilu pravidlo nehraj (pokusime se namazat, pokud je co)
                        if (TeamMateIndex != -1 &&
                            (_gameType & Hra.Kilo) != 0)
                        {
                            return null;
                        }
                        //pokud zbyva vic trumfu nez je kol do konce, riskni, ze ma jeden trumf kolega a namaz mu
                        if (c1.Suit != _trump &&
                            TeamMateIndex == player3 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Suit != _trump &&
                                                                    i.Value == Hodnota.Desitka) &&
                            _probabilities.SuitProbability(TeamMateIndex, c1.Suit, RoundNumber) < 1 &&
                            _probabilities.PotentialCards(player1).CardCount(_trump) > 2)
                        {
                            return null;
                        }
                        if (c1.Suit == _trump &&
                            TeamMateIndex == player3 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Suit != _trump &&
                                                                    i.Value >= Hodnota.Desitka) &&
                            _probabilities.PotentialCards(TeamMateIndex).Count(i => i.Suit == _trump &&
                                                                                    i.Value > c1.Value) >= 1 &&
                            _probabilities.PotentialCards(player1).CardCount(_trump) > 2)
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
                    #endregion
                };
            }

            yield return new AiRule
            {
                Order = 1,
                Description = "hraj vítěznou X",
                SkipSimulations = true,
                #region ChooseCard2 Rule1
                ChooseCard2 = (Card c1) =>
                {
                    if (ValidCards(c1, hands[MyIndex]).Count > (c1.Suit == _trump && c1.Value == Hodnota.Eso ? 1 : 2) &&
                        ValidCards(c1, hands[MyIndex]).HasX(_trump) &&
                        !(_probabilities.CertainCards(player1).HasA(_trump) &&
                          !_probabilities.PotentialCards(player3).HasSuit(_trump) &&
                          unwinnableLowCards.CardCount(_trump) + 1 >= hands[MyIndex].CardCount(_trump)))
                    {
                        if (TeamMateIndex == player3 &&
                            hands[MyIndex].CardCount(_trump) == 3)
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
                    var basicPointsWonSoFar = 0;
                    var basicPointsWonThisRound = c1.Value >= Hodnota.Desitka ? 20 : 10;
                    var basicPointsLost = 0;
                    var hlasPointsWon = 0;
                    var hlasPointsLost = 0;

                    foreach (var r in _rounds.Where(i => i?.c3 != null))
                    {
                        if (r.c1.Value >= Hodnota.Desitka)
                        {
                            if (r.roundWinner.PlayerIndex == MyIndex ||
                                r.roundWinner.PlayerIndex == TeamMateIndex)
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
                            if (r.roundWinner.PlayerIndex == MyIndex ||
                                r.roundWinner.PlayerIndex == TeamMateIndex)
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
                            if (r.roundWinner.PlayerIndex == MyIndex ||
                                r.roundWinner.PlayerIndex == TeamMateIndex)
                            {
                                basicPointsWonSoFar += 10;
                            }
                            else
                            {
                                basicPointsLost += 10;
                            }
                        }
                        if (r.hlas1)
                        {
                            if (r.player1.PlayerIndex == MyIndex ||
                                r.player1.PlayerIndex == TeamMateIndex)
                            {
                                hlasPointsWon += r.c1.Suit == _trump ? 40 : 20;
                            }
                            else
                            {
                                hlasPointsLost += r.c1.Suit == _trump ? 40 : 20;
                            }
                        }
                        if (r.hlas2)
                        {
                            if (r.player2.PlayerIndex == MyIndex ||
                                r.player2.PlayerIndex == TeamMateIndex)
                            {
                                hlasPointsWon += r.c2.Suit == _trump ? 40 : 20;
                            }
                            else
                            {
                                hlasPointsLost += r.c2.Suit == _trump ? 40 : 20;
                            }
                        }
                        if (r.hlas3)
                        {
                            if (r.player3.PlayerIndex == MyIndex ||
                                r.player3.PlayerIndex == TeamMateIndex)
                            {
                                hlasPointsWon += r.c3.Suit == _trump ? 40 : 20;
                            }
                            else
                            {
                                hlasPointsLost += r.c3.Suit == _trump ? 40 : 20;
                            }
                        }
                    }
                    var basicPointsLeft = 90 - basicPointsWonSoFar - basicPointsWonThisRound - basicPointsLost;
                    var kqScore = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                      .Sum(b => hands[MyIndex].HasK(b) &&
                                                hands[MyIndex].HasQ(b)
                                                ? b == _trump ? 40 : 20
                                                : 0);
                    var hlasPointsLeft = TeamMateIndex == -1
                                         ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .Sum(b => (!(c1.Suit == b &&
                                                            c1.Value == Hodnota.Svrsek) &&
                                                          _probabilities.PotentialCards(player1).HasK(b) &&
                                                          _probabilities.PotentialCards(player1).HasQ(b)) ||
                                                         (_probabilities.PotentialCards(player3).HasK(b) &&
                                                          _probabilities.PotentialCards(player3).HasQ(b))
                                                          ? b == _trump ? 40 : 20
                                                          : 0)
                                         : Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .Sum(b => !(c1.Suit == b &&
                                                           c1.Value == Hodnota.Svrsek) &&
                                                         _probabilities.PotentialCards(opponent).HasK(b) &&
                                                         _probabilities.PotentialCards(opponent).HasQ(b)
                                                         ? b == _trump ? 40 : 20
                                                         : 0);
                    var opponentPotentialPoints = basicPointsLost + hlasPointsLost + basicPointsLeft + hlasPointsLeft;
                    var gameWinningCard = false;

                    if (TeamMateIndex != -1)
                    {
                        if ((_gameType & Hra.Kilo) != 0 &&
                            basicPointsWonSoFar <= 30 &&
                            basicPointsWonSoFar + basicPointsWonThisRound >= 40)
                        {
                            gameWinningCard = true;
                        }
                        else if ((_gameType & Hra.Kilo) == 0 &&
                                 basicPointsWonSoFar + hlasPointsWon + kqScore <= opponentPotentialPoints &&
                                 basicPointsWonSoFar + basicPointsWonThisRound + hlasPointsWon + kqScore > opponentPotentialPoints)
                        {
                            gameWinningCard = true;
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
                        if (!gameWinningCard &&
                            ValidCards(c1, hands[MyIndex]).Count > 1 &&
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
                                                                                                                        j.Value != Hodnota.Eso) > (hands[MyIndex].HasA(i.Suit) ? 2 : 1) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)) ||
                                                                     _probabilities.CertainCards(player1).Count(j => j.Suit == i.Suit &&
                                                                                                                     j != c1 &&
                                                                                                                     j.Value != Hodnota.Eso) > (hands[MyIndex].HasA(i.Suit) ? 1 : 0) ||
                                                                     (_probabilities.PotentialCards(player1).Count(j => j.Suit == i.Suit &&
                                                                                                                        j != c1 &&
                                                                                                                        j.Value != Hodnota.Eso) > 1 &&
                                                                      _probabilities.PotentialCards(player1).Where(j => j != c1 &&
                                                                                                                        j.Value != Hodnota.Eso)
                                                                                                            .SuitCount() == 1))))
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
                                                                                              !(!_probabilities.PotentialCards(player3).HasSuit(i.Suit) &&          //nehraj pokud ma oponent jiste nizsi kartu v barve
                                                                                                _probabilities.PotentialCards(player1)
                                                                                                              .Where(j => j != c1 &&
                                                                                                                          j != new Card(i.Suit, Hodnota.Eso))
                                                                                                              .CardCount(i.Suit) > 2 - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)) &&    //(kdyz kolega urcite barvu nezna a souper ma aspon 3 karty v barve)
                                                                                              (gameWinningCard ||
                                                                                               hands[MyIndex].CardCount(_trump) <= 3 &&
                                                                                               (hands[MyIndex].CardCount(_trump) <= opHiTrumps + 1 ||
                                                                                                ((_gameType & Hra.SedmaProti) != 0 &&
                                                                                                 hands[MyIndex].Has7(_trump) &&
                                                                                                 hands[MyIndex].CardCount(_trump) <= opHiTrumps + 2))) &&
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
                        if (!gameWinningCard &&
                            ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka &&
                                                                    i.Suit != _trump) &&
                            ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                      i.Suit != _trump)
                                                          .All(i =>
                                            i.Suit != c1.Suit &&
                                            ((_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > 0 &&
                                              hands[MyIndex].CardCount(i.Suit) > 1) ||
                                             _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0) &&
                                            (_probabilities.CertainCards(player3).Count(j => j.Suit == i.Suit &&
                                                                                             j.Value < i.Value) > (hands[MyIndex].HasA(i.Suit) ? 2 : 1) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit) ||
                                             (!_probabilities.PotentialCards(player1).HasSuit(i.Suit) &&
                                              _probabilities.PotentialCards(player3).Count(j => j.Suit == i.Suit &&
                                                                                                j.Value < i.Value) > (hands[MyIndex].HasA(i.Suit) ? 3 : 2) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)))))
                        {
                            return null;
                        }
                        //nehraj pokud akterovi mozna zbyva posledni trumf - eso
                        if (ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka &&
                                                                    i.Suit == _trump) &&
                            _probabilities.PotentialCards(player3).CardCount(_trump) == 1 &&
                            _probabilities.PotentialCards(player3).HasA(_trump) &&
                            !_probabilities.CertainCards(player3).HasSuit(c1.Suit))
                        {
                            return null;
                        }
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    i.Suit != _trump &&
                                                                                    _probabilities.SuitHigherThanCardProbability(player3, c1, RoundNumber) == 0 &&
                                                                                    (c1.Suit == _trump ||
                                                                                     _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                                    (gameWinningCard ||
                                                                                     !(_probabilities.CertainCards(player3).Count(j => j.Suit == i.Suit &&
                                                                                                                                       j.Value < i.Value) > (hands[MyIndex].HasA(i.Suit) ? 2 : 1) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit) ||
                                                                                       _probabilities.PotentialCards(player3).CardCount(i.Suit) > (hands[MyIndex].HasA(i.Suit) ? 3 : 2 - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)))))
                                                                        .ToList();
                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    i.Suit != _trump &&
                                                                                    i.Suit != c1.Suit &&
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) >= 1 - _epsilon &&
                                                                                    hands[MyIndex].CardCount(i.Suit) == 1 &&
                                                                                    _probabilities.SuitLowerThanCardProbability(player3, c1, RoundNumber) == 1 &&
                                                                                    (gameWinningCard ||
                                                                                     !(_probabilities.CertainCards(player3).Count(j => j.Suit == i.Suit &&
                                                                                                                                       j.Value < i.Value) > (hands[MyIndex].HasA(i.Suit) ? 2 : 1) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit) ||
                                                                                       _probabilities.PotentialCards(player3).CardCount(i.Suit) > (hands[MyIndex].HasA(i.Suit) ? 3 : 2 - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)))))
                                                                        .ToList();
                        }
                        if (!cardsToPlay.Any())
                        {
                            //tohle se ma hrat v pravidle "namazat"
                            var hiCards = _probabilities.PotentialCards(player3)
                                                        .Where(i => c1.IsLowerThan(i, _trump));

                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    !hands[MyIndex].HasA(i.Suit) &&
                                                                                    !(_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1 &&  //ignoruj kartu pokud s ni muzu prebit akterovu nizkou kartu v barve
                                                                                      _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Eso)) == 0) &&
                                                                                      !hiCards.Any() &&                     //spoluhrac hral nejvyssi kartu co ve hre zbyva
                                                                                      (i.Suit != _trump ||                  //a pokud moje X neni trumfova
                                                                                       !hands[MyIndex].HasA(_trump)) &&     //trumfovou X hraju jen kdyz nemam A
                                                                                    (i.Suit == _trump ||
                                                                                     (i.Suit != c1.Suit &&
                                                                                      (gameWinningCard ||
                                                                                       (_probabilities.PotentialCards(player3).HasA(i.Suit) ||
                                                                                        !_probabilities.CertainCards(player3).HasSuit(i.Suit) ||
                                                                                        _probabilities.PotentialCards(player3).CardCount(i.Suit) <= 2 - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit))))))
                                                                        .ToList();
                        }
                        if (cardsToPlay.Any())
                        {
                            return cardsToPlay.RandomOneOrDefault();
                        }

                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                c1.IsLowerThan(i, _trump) &&          //moje karta prebiji prvni kartu
                                                                                i.Suit != _trump &&                  //a pokud moje X neni trumfova
                                                                                (PlayerBids[TeamMateIndex] == 0 ||   //a pokud kolega neflekoval - pokud flekoval je lepsi mu mazat
                                                                                 hands[MyIndex].Count(j => j.Value >= Hodnota.Desitka) >= 3 ||
                                                                                 hands[MyIndex].HasSuit(_trump)) &&  //pokud mam trumf, stejne bych mazat pozdeji nemohl, tak zkus kartu uhrat rovnou
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                (_probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor ||
                                                                                 (_probabilities.PotentialCards(player3).CardCount(i.Suit) > 1 &&
                                                                                  _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor))).ToList();
                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    c1.IsLowerThan(i, _trump) &&          //moje karta prebiji prvni kartu
                                                                                    i.Suit == _trump &&                  //a pokud moje X je trumfova
                                                                                    !hands[MyIndex].HasA(_trump) &&      //trumfovou X hraju jen kdyz nemam A
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                    _probabilities.PotentialCards(player3).HasSuit(_trump)) //nehraj zbytecne vysoky trumf pokud posledni hrac nema trumfy
                                                                        .ToList();
                        }
                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    c1.Suit != _trump &&          //moje karta prebiji prvni kartu
                                                                                    i.Suit == _trump &&                  //a pokud moje X je trumfova
                                                                                    hands[MyIndex].CardCount(_trump) == 2 &&
                                                                                    (_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 ||
                                                                                     _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor) &&
                                                                                    _probabilities.PotentialCards(player3).HasA(_trump)) //nehraj zbytecne vysoky trumf pokud posledni hrac nema trumfy
                                                                        .ToList();
                        }
                        if (cardsToPlay.Any())
                        {
                            return cardsToPlay.RandomOneOrDefault();
                        }
                        //tohle se ma hrat v pravidle "namazat"
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&         //1. karta je nejvetsi
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                ((_gameType & Hra.Kilo) == 0 ||
                                                                                 hands[MyIndex].Count(j => j.Suit != _trump &&
                                                                                                            j.Value >= Hodnota.Desitka) > 2) &&
                                                                                (PlayerBids[TeamMateIndex] == 0 ||
                                                                                 hands[MyIndex].Count(j => j.Value >= Hodnota.Desitka) >= 3 ||
                                                                                 hands[MyIndex].HasSuit(_trump)) &&   //a pokud kolega neflekoval - pokud flekoval je lepsi mu mazat
                                                                                                                      //_probabilities.SuitProbability(player3, i.Suit, RoundNumber) != 1 &&  //ignoruj kartu pokud s ni muzu prebit akterovu nizkou kartu v barve
                                                                                (gameWinningCard ||
                                                                                 !(_probabilities.CertainCards(player3).Count(j => j.Suit == i.Suit &&
                                                                                                                                   j.Value < i.Value) > (hands[MyIndex].HasA(i.Suit) ? 2 : 1) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit) ||
                                                                                 _probabilities.PotentialCards(player3).CardCount(i.Suit) > (hands[MyIndex].HasA(i.Suit) ? 3 : 2 - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)))) &&
                                                                                (i.Suit != _trump ||                  //a pokud moje X neni trumfova
                                                                                 !hands[MyIndex].HasA(_trump)) &&     //trumfovou X hraju jen kdyz nemam A
                                                                                _probabilities.NoSuitHigherThanCardProbability(player3, c1, RoundNumber) >= 1 - RiskFactor &&
                                                                                (_probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor ||
                                                                                 _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor)).ToList();

                        return cardsToPlay.OrderBy(i => _probabilities.SuitProbability(player3, i.Suit, RoundNumber))
                                          .FirstOrDefault();
                    }
                    else
                    {
                        //-c-
                        //nehraj pokud ma prvni hrac jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (ValidCards(c1, hands[MyIndex]).Count > 2 &&
                            ValidCards(c1, hands[MyIndex]).HasX(_trump) &&
                            !(_probabilities.CertainCards(player1).HasA(_trump) &&
                              !_probabilities.PotentialCards(player3).HasSuit(_trump) &&
                              unwinnableLowCards.CardCount(_trump) + 1 >= hands[MyIndex].CardCount(_trump)))
                        {
                            return null;
                        }
                        //nehraj pokud souperovi mozna zbyva posledni trumf - eso
                        if (ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka &&
                                                                    i.Suit == _trump) &&
                            _probabilities.PotentialCards(player3).CardCount(_trump) == 1 &&
                            _probabilities.PotentialCards(player3).HasA(_trump) &&
                            !_probabilities.CertainCards(player3).HasSuit(c1.Suit))
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
                            //v predposlednim kole pokud mas nahrano dost bodu ale na kilo jich neni dost
                            //radsi obetuj trumfovou desitku nez by sis mel nechat zabit sedmu
                            if (RoundNumber >= 8 &&
                                (_gameType & Hra.Kilo) == 0 &&
                                hands[MyIndex].CardCount(_trump) == 2 &&
                                hands[MyIndex].Has7(_trump) &&
                                basicPointsWonSoFar + basicPointsWonThisRound - 10 + hlasPointsWon + kqScore < 100 &&
                                basicPointsWonSoFar + basicPointsWonThisRound - 10 + hlasPointsWon + kqScore > opponentPotentialPoints)
                            {
                                return null;
                            }

                            return ValidCards(c1, hands[MyIndex]).FirstOrDefault(i => i.Suit == _trump &&
                                                                                      i.Value == Hodnota.Desitka);
                        }
                        //pokud ma souper vysoke trumfy a nemuzes je z nej vytlacit, tak desitku hraj
                        if (ValidCards(c1, hands[MyIndex]).Any(i => i.Suit == _trump &&
                                            i.Value == Hodnota.Desitka) &&
                            c1.IsLowerThan(new Card(_trump, Hodnota.Desitka), _trump) &&
                            _probabilities.CertainCards(player1).HasA(_trump) &&
                            !_probabilities.PotentialCards(player3).HasSuit(_trump) &&
                            unwinnableLowCards.CardCount(_trump) + 1 >= hands[MyIndex].CardCount(_trump))//desitka se do unwinnableLowCards nemusi zapocitat proto +1 a >=
                        {
                            return ValidCards(c1, hands[MyIndex]).FirstOrDefault(i => i.Suit == _trump &&
                                                                                      i.Value == Hodnota.Desitka);
                        }
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    c1.IsLowerThan(i, _trump) &&          //moje karta prebiji prvni kartu
                                                                                    i.Suit != _trump &&                  //a pokud moje X neni trumfova
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                    (_probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor ||
                                                                                     _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor))
                                                                        .ToList();
                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    c1.IsLowerThan(i, _trump) &&          //moje karta prebiji prvni kartu
                                                                                    i.Suit == _trump &&                  //a pokud moje X je trumfova
                                                                                    !hands[MyIndex].HasA(_trump) &&      //trumfovou X hraju jen kdyz nemam A
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                    _probabilities.PotentialCards(player3).HasSuit(_trump)) //nehraj zbytecne vysoky trumf pokud posledni hrac nema trumfy
                                                                        .ToList();
                        }
                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    c1.Suit != _trump &&          //moje karta prebiji prvni kartu
                                                                                    i.Suit == _trump &&                  //a pokud moje X je trumfova
                                                                                    hands[MyIndex].CardCount(_trump) == 2 &&
                                                                                    (_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 ||
                                                                                     _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor) &&
                                                                                    (_probabilities.PotentialCards(player3).HasA(_trump) ||
                                                                                     _probabilities.PotentialCards(player1).HasA(_trump))) //nehraj zbytecne vysoky trumf pokud posledni hrac nema trumfy
                                                                        .ToList();
                        }

                        return cardsToPlay.OrderBy(i => _probabilities.SuitProbability(player1, i.Suit, RoundNumber))
                                          .FirstOrDefault();
                    }
                }
                #endregion
            };

            yield return new AiRule
            {
                Order = 2,
                Description = "hrát vítězné A",
                SkipSimulations = true,
                #region ChooseCard2 Rule2
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                                i.Suit == c1.Suit &&
                                                                                _probabilities.HasSolitaryX(player3, i.Suit, RoundNumber) >= 1 - _epsilon)
                                                                    .ToList();
                    var basicPointsWonSoFar = 0;
                    var basicPointsWonThisRound = c1.Value >= Hodnota.Desitka ? 20 : 10;
                    var basicPointsLost = 0;
                    var hlasPointsWon = 0;
                    var hlasPointsLost = 0;

                    foreach (var r in _rounds.Where(i => i?.c3 != null))
                    {
                        if (r.c1.Value >= Hodnota.Desitka)
                        {
                            if (r.roundWinner.PlayerIndex == MyIndex ||
                                r.roundWinner.PlayerIndex == TeamMateIndex)
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
                            if (r.roundWinner.PlayerIndex == MyIndex ||
                                r.roundWinner.PlayerIndex == TeamMateIndex)
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
                            if (r.roundWinner.PlayerIndex == MyIndex ||
                                r.roundWinner.PlayerIndex == TeamMateIndex)
                            {
                                basicPointsWonSoFar += 10;
                            }
                            else
                            {
                                basicPointsLost += 10;
                            }
                        }
                        if (r.hlas1)
                        {
                            if (r.player1.PlayerIndex == MyIndex ||
                                r.player1.PlayerIndex == TeamMateIndex)
                            {
                                hlasPointsWon += r.c1.Suit == _trump ? 40 : 20;
                            }
                            else
                            {
                                hlasPointsLost += r.c1.Suit == _trump ? 40 : 20;
                            }
                        }
                        if (r.hlas2)
                        {
                            if (r.player2.PlayerIndex == MyIndex ||
                                r.player2.PlayerIndex == TeamMateIndex)
                            {
                                hlasPointsWon += r.c2.Suit == _trump ? 40 : 20;
                            }
                            else
                            {
                                hlasPointsLost += r.c2.Suit == _trump ? 40 : 20;
                            }
                        }
                        if (r.hlas3)
                        {
                            if (r.player3.PlayerIndex == MyIndex ||
                                r.player3.PlayerIndex == TeamMateIndex)
                            {
                                hlasPointsWon += r.c3.Suit == _trump ? 40 : 20;
                            }
                            else
                            {
                                hlasPointsLost += r.c3.Suit == _trump ? 40 : 20;
                            }
                        }
                    }
                    var basicPointsLeft = 90 - basicPointsWonSoFar - basicPointsWonThisRound - basicPointsLost;
                    var kqScore = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                      .Sum(b => hands[MyIndex].HasK(b) &&
                                                hands[MyIndex].HasQ(b)
                                                ? b == _trump ? 40 : 20
                                                : 0);
                    var hlasPointsLeft = TeamMateIndex == -1
                                         ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .Sum(b => (!(c1.Suit == b &&
                                                            c1.Value == Hodnota.Svrsek) &&
                                                          _probabilities.PotentialCards(player1).HasK(b) &&
                                                          _probabilities.PotentialCards(player1).HasQ(b)) ||
                                                         (_probabilities.PotentialCards(player3).HasK(b) &&
                                                          _probabilities.PotentialCards(player3).HasQ(b))
                                                          ? b == _trump ? 40 : 20
                                                          : 0)
                                         : Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .Sum(b => !(c1.Suit == b &&
                                                           c1.Value == Hodnota.Svrsek) &&
                                                         _probabilities.PotentialCards(opponent).HasK(b) &&
                                                         _probabilities.PotentialCards(opponent).HasQ(b)
                                                         ? b == _trump ? 40 : 20
                                                         : 0);
                    var opponentPotentialPoints = basicPointsLost + hlasPointsLost + basicPointsLeft + hlasPointsLeft;
                    var gameWinningCard = false;

                    if (TeamMateIndex != -1)
                    {
                        if ((_gameType & Hra.Kilo) != 0 &&
                            basicPointsWonSoFar <= 30 &&
                            basicPointsWonSoFar + basicPointsWonThisRound >= 40)
                        {
                            gameWinningCard = true;
                        }
                        else if ((_gameType & Hra.Kilo) == 0 &&
                                 basicPointsWonSoFar + hlasPointsWon + kqScore <= opponentPotentialPoints &&
                                 basicPointsWonSoFar + basicPointsWonThisRound + hlasPointsWon + kqScore > opponentPotentialPoints)
                        {
                            gameWinningCard = true;
                        }
                    }

                    if (cardsToPlay.Any())
                    {
                        return cardsToPlay.First();
                    }

                    if (TeamMateIndex == -1)
                    {
                        //-c-
                        //nehraj pokud ma prvni hrac jiste dalsi male karty v barve a muzes hrat i neco jineho
                        //neplati pokud mi zahrani karty zajisti vyhru
                        if (!gameWinningCard &&
                            ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Eso) &&
                            ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso)
                                                          .All(i => i.Suit != _trump &&
                                                                    i.Suit == c1.Suit &&
                                                                    ((_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                                      _probabilities.PotentialCards(player1)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 2 : 1) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)) ||
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
                                                                                  (gameWinningCard ||
                                                                                   !((_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                                                      _probabilities.PotentialCards(player1)
                                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                                    .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 2 : 1) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)) ||
                                                                                     _probabilities.CertainCards(player1)
                                                                                                   .Where(j => j.Suit == i.Suit)
                                                                                                   .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0))));
                    }
                    else if (TeamMateIndex == player3)
                    {
                        //-co
                        //nehraj pokud ma prvni hrac jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (!gameWinningCard &&
                            ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Eso) &&
                            ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso)
                                                          .All(i => i.Suit != _trump &&
                                                                    i.Suit == c1.Suit &&
                                                                    ((_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                                      _probabilities.PotentialCards(player1)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 3 : 2) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)) ||
                                                                     _probabilities.CertainCards(player1)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0) ||
                                                                     (_probabilities.PotentialCards(player1).Count(j => j.Suit == i.Suit &&
                                                                                                                        j != c1) > 1 &&
                                                                      _probabilities.PotentialCards(player1).Where(j => j != c1)
                                                                                                            .SuitCount() == 1))))
                        {
                            return null;
                        }
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                                  i.Suit != _trump &&
                                                                                  c1.IsLowerThan(i, _trump) &&
                                                                                  ((c1.Suit == i.Suit &&
                                                                                    c1.Value == Hodnota.Desitka) ||
                                                                                   (_probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon &&
                                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                        .Where(h => h != Hodnota.Desitka)
                                                                                        .All(h => _probabilities.CardProbability(player3, new Card(i.Suit, h)) < 1 - _epsilon))) &&
                                                                                   ((gameWinningCard &&
                                                                                     !_probabilities.CertainCards(player1).HasX(i.Suit)) ||
                                                                                    !((_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                                                       _probabilities.PotentialCards(player1)
                                                                                                     .Where(j => j.Suit == i.Suit)
                                                                                                     .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 3 : 2) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)) ||
                                                                                      _probabilities.CertainCards(player1)
                                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                                    .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0))))
                                                                    .ToList();

                        return cardsToPlay.OrderBy(i => _probabilities.SuitProbability(opponent, i.Suit, RoundNumber))
                                          .FirstOrDefault();
                    }
                    else
                    {
                        //oc-
                        //nehraj pokud ma akter jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (!gameWinningCard &&
                            ValidCards(c1, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, hands[MyIndex]).Any(i => i.Value == Hodnota.Eso) &&
                            ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso)
                                                          .All(i =>
                                            i.Suit != _trump &&
                                            i.Suit != c1.Suit &&
                                            (_probabilities.CertainCards(player3).CardCount(i.Suit) > (hands[MyIndex].HasX(i.Suit) ? 3 : 2) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit))))
                        {
                            return null;
                        }

                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                                  i.Suit != _trump &&
                                                                                  _probabilities.SuitHigherThanCardProbability(player3, c1, RoundNumber) == 0 &&
                                                                                  ((c1.Suit == _trump &&
                                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                        .All(h => _probabilities.CardProbability(player3, new Card(i.Suit, h)) < 1 - _epsilon)) ||
                                                                                   _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                                  ((gameWinningCard &&
                                                                                    !_probabilities.CertainCards(player3).HasX(i.Suit)) ||
                                                                                   !(_probabilities.CertainCards(player3).CardCount(i.Suit) > (hands[MyIndex].HasX(i.Suit) ? 3 : 2) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit))))
                                                                   .ToList();

                        return cardsToPlay.OrderBy(i => _probabilities.SuitProbability(opponent, i.Suit, RoundNumber))
                                          .FirstOrDefault();
                    }
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "zkusit uhrát bodovanou kartu",
                SkipSimulations = true,
                #region ChooseCard2 Rule3
                ChooseCard2 = (Card c1) =>
                {
                    if (TeamMateIndex == player1 &&
                        ValidCards(c1, hands[MyIndex]).HasA(c1.Suit) &&
                        (_probabilities.PotentialCards(player3).CardCount(c1.Suit) >= 5 ||
                         ((!potentialGreaseCards.HasA(c1.Suit) ||
                           myInitialHand.Count(i => i.Suit != _trump &&
                                                    i.Value >= Hodnota.Desitka) >= 3) &&
                          _probabilities.PotentialCards(player3).HasSuit(c1.Suit))) &&
                        myInitialHand.CardCount(_trump) <= 4)
                    {
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                                    i.Suit == c1.Suit).ToList();

                        if (cardsToPlay.Any())
                        {
                            return cardsToPlay.FirstOrDefault();
                        }
                    }
                    if (TeamMateIndex == player1 &&
                        c1.Suit != _trump &&
                        ValidCards(c1, hands[MyIndex]).HasX(c1.Suit) &&
                        !_probabilities.PotentialCards(player3).HasA(c1.Suit) &&
                        (_probabilities.PotentialCards(player3).CardCount(c1.Suit) >= 3 ||
                         ((!potentialGreaseCards.HasX(c1.Suit) ||
                           myInitialHand.Count(i => i.Suit != _trump &&
                                                    i.Value >= Hodnota.Desitka) >= 3) &&
                          _probabilities.PotentialCards(player3).HasSuit(c1.Suit))) &&
                        myInitialHand.CardCount(_trump) <= 4)
                    {
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    i.Suit == c1.Suit).ToList();

                        if (cardsToPlay.Any())
                        {
                            return cardsToPlay.FirstOrDefault();
                        }
                    }
                    if (TeamMateIndex != player3 &&
                        c1.Suit != _trump &&
                        ValidCards(c1, hands[MyIndex]).HasX(_trump) &&
                        ValidCards(c1, hands[MyIndex]).Count == 2 &&
                        !ValidCards(c1, hands[MyIndex]).HasK(_trump) &&
                        _probabilities.PotentialCards(player3).HasA(_trump) &&
                        (_probabilities.LikelyCards(player3).HasSuit(c1.Suit) ||
                         (RoundNumber >= 8 &&
                          _probabilities.PotentialCards(player3).HasSuit(c1.Suit))))
                    {
                        return ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                         i.Value == Hodnota.Desitka)
                                                             .FirstOrDefault();
                    }
                    if (TeamMateIndex == -1 &&
                        (_gameType & Hra.Kilo) != 0 &&
                        c1.Suit != _trump &&
                        c1.Value >= Hodnota.Desitka &&
                        !hands[MyIndex].HasSuit(c1.Suit) &&
                        topTrumps.Count > opponentTrumps.Count &&
                        _probabilities.PotentialCards(player3).HasSuit(_trump))
                    {
                        return ValidCards(c1, hands[MyIndex]).Where(i => topTrumps.Contains(i))
                                                             .OrderBy(i => i.Value)
                                                             .FirstOrDefault();
                    }
                    if (TeamMateIndex == -1 &&
                        c1.Suit != _trump &&
                        c1.Value >= Hodnota.Desitka &&
                        !hands[MyIndex].HasSuit(c1.Suit) &&
                        topTrumps.Count >= opponentTrumps.Count &&
                        _probabilities.PotentialCards(player3).HasSuit(_trump) &&
                        !_probabilities.PotentialCards(player3).HasSuit(c1.Suit))
                    {
                        return ValidCards(c1, hands[MyIndex]).Where(i => topTrumps.Contains(i))
                                                             .OrderBy(i => i.Value)
                                                             .FirstOrDefault();
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule
            {
                Order = 4,
                Description = "bodovat nebo vytlačit trumf",
                SkipSimulations = true,
                #region ChooseCard2 Rule4
                ChooseCard2 = (Card c1) =>
                {
                    if (TeamMateIndex == -1 &&
                        (_gameType & Hra.Kilo) != 0)
                    {
                        return null;
                    }
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
                    if (TeamMateIndex != player3 &&
                        SevenValue >= GameValue &&
                        ((hands[MyIndex].SuitCount == 3 &&
                          !hands[MyIndex].HasSuit(_trump) &&
                          (TeamMateIndex == -1 ||
                           (PlayerBids[TeamMateIndex] & Hra.Hra) == 0)) ||
                         hands[MyIndex].SuitCount == 1 ||
                         hands[MyIndex].CardCount(_trump) >= 2) ||
                        (TeamMateIndex == player1 &&
                         (_gameType & (Hra.Sedma | Hra.Kilo)) != 0 &&
                         hands[MyIndex].Count(i => i.Suit != _trump &&
                                                   i.Value >= Hodnota.Desitka) >= 3))
                    {
                        return ValidCards(c1, hands[MyIndex]).OrderByDescending(i => i.Value)
                                                             .FirstOrDefault(i => (i.Value == Hodnota.Eso ||
                                                                                   (i.Value == Hodnota.Desitka &&
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0)) &&
                                                                                  i.Suit != _trump &&
                                                                                  c1.IsLowerThan(i, _trump) &&
                                                                                  !(hands[MyIndex].CardCount(i.Suit) >= 4 &&
                                                                                    hands[MyIndex].CardCount(_trump) >= 3) &&
                                                                                  (hands[MyIndex].Count(j => j.Value >= Hodnota.Desitka &&
                                                                                                             j.Suit != _trump) > 2 ||
                                                                                   hands[MyIndex].HasX(i.Suit)) &&
                                                                                  (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 ||
                                                                                   _probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor));
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule
            {
                Order = 5,
                Description = "vytlačit A",
                SkipSimulations = true,
                #region ChooseCard2 Rule5
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
                #endregion
            };

            yield return new AiRule
            {
                Order = 6,
                Description = "namazat",
                SkipSimulations = true,
                #region ChooseCard2 Rule6
                ChooseCard2 = (Card c1) =>
                {
                    var basicPointsWonSoFar = 0;
                    var basicPointsWonThisRound = c1.Value >= Hodnota.Desitka ? 20 : 10;
                    var basicPointsLost = 0;
                    var hlasPointsWon = 0;
                    var hlasPointsLost = 0;

                    foreach (var r in _rounds.Where(i => i?.c3 != null))
                    {
                        if (r.c1.Value >= Hodnota.Desitka)
                        {
                            if (r.roundWinner.PlayerIndex == MyIndex ||
                                r.roundWinner.PlayerIndex == TeamMateIndex)
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
                            if (r.roundWinner.PlayerIndex == MyIndex ||
                                r.roundWinner.PlayerIndex == TeamMateIndex)
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
                            if (r.roundWinner.PlayerIndex == MyIndex ||
                                r.roundWinner.PlayerIndex == TeamMateIndex)
                            {
                                basicPointsWonSoFar += 10;
                            }
                            else
                            {
                                basicPointsLost += 10;
                            }
                        }
                        if (r.hlas1)
                        {
                            if (r.player1.PlayerIndex == MyIndex ||
                                r.player1.PlayerIndex == TeamMateIndex)
                            {
                                hlasPointsWon += r.c1.Suit == _trump ? 40 : 20;
                            }
                            else
                            {
                                hlasPointsLost += r.c1.Suit == _trump ? 40 : 20;
                            }
                        }
                        if (r.hlas2)
                        {
                            if (r.player2.PlayerIndex == MyIndex ||
                                r.player2.PlayerIndex == TeamMateIndex)
                            {
                                hlasPointsWon += r.c2.Suit == _trump ? 40 : 20;
                            }
                            else
                            {
                                hlasPointsLost += r.c2.Suit == _trump ? 40 : 20;
                            }
                        }
                        if (r.hlas3)
                        {
                            if (r.player3.PlayerIndex == MyIndex ||
                                r.player3.PlayerIndex == TeamMateIndex)
                            {
                                hlasPointsWon += r.c3.Suit == _trump ? 40 : 20;
                            }
                            else
                            {
                                hlasPointsLost += r.c3.Suit == _trump ? 40 : 20;
                            }
                        }
                    }
                    var basicPointsLeft = 90 - basicPointsWonSoFar - basicPointsWonThisRound - basicPointsLost;
                    var kqScore = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                      .Sum(b => hands[MyIndex].HasK(b) &&
                                                hands[MyIndex].HasQ(b)
                                                ? b == _trump ? 40 : 20
                                                : 0);
                    var hlasPointsLeft = TeamMateIndex == -1
                                         ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .Sum(b => (!(c1.Suit == b &&
                                                            c1.Value == Hodnota.Svrsek) &&
                                                          _probabilities.PotentialCards(player1).HasK(b) &&
                                                          _probabilities.PotentialCards(player1).HasQ(b)) ||
                                                         (_probabilities.PotentialCards(player3).HasK(b) &&
                                                          _probabilities.PotentialCards(player3).HasQ(b))
                                                          ? b == _trump ? 40 : 20
                                                          : 0)
                                         : Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .Sum(b => !(c1.Suit == b &&
                                                           c1.Value == Hodnota.Svrsek) &&
                                                         _probabilities.PotentialCards(opponent).HasK(b) &&
                                                         _probabilities.PotentialCards(opponent).HasQ(b)
                                                         ? b == _trump ? 40 : 20
                                                         : 0);
                    var opponentPotentialPoints = basicPointsLost + hlasPointsLost + basicPointsLeft + hlasPointsLeft;
                    var gameWinningCard = false;

                    if (TeamMateIndex != -1)
                    {
                        if ((_gameType & Hra.Kilo) != 0 &&
                            basicPointsWonSoFar <= 30 &&
                            basicPointsWonSoFar + basicPointsWonThisRound >= 40)
                        {
                            gameWinningCard = true;
                        }
                        else if ((_gameType & Hra.Kilo) == 0 &&
                                 basicPointsWonSoFar + hlasPointsWon + kqScore <= opponentPotentialPoints &&
                                 basicPointsWonSoFar + basicPointsWonThisRound + hlasPointsWon + kqScore > opponentPotentialPoints)
                        {
                            gameWinningCard = true;
                        }
                    }

                    if (TeamMateIndex == player3)
                    {
                        //-co
                        //nehraj pokud ma prvni hrac jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (!gameWinningCard &&
                            ValidCards(c1, hands[MyIndex]).Count > 1 &&
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
                            (c1.Value >= Hodnota.Desitka ||
                             !(Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                   .Any(b => (hands[MyIndex].HasX(b) &&
                                              (hands[MyIndex].HasA(b) ||
                                               hands[MyIndex].CardCount(b) >= 5)) ||
                               myInitialHand.Count(i => i.Value >= Hodnota.Desitka) > 2))))
                        {
                            //nemaz pokud akter vyjel trumfem a jeste nesla ani desitka ani eso
                            return null;
                        }

                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                         ((i.Value == Hodnota.Desitka &&
                                                                           !(c1.Suit == i.Suit &&
                                                                             c1.Value == Hodnota.Eso)) ||
                                                                          (i.Value == Hodnota.Eso &&        //eso namaz jen kdyz nemuzu chytit desitku nebo pri kilu (proti)
                                                                           (_probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon ||
                                                                            ((_gameType & Hra.Kilo) != 0 &&
                                                                             _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon &&
                                                                             _probabilities.SuitHigherThanCardProbability(player3, c1, RoundNumber) >= 1 / 3f &&
                                                                             (_calculationStyle == CalculationStyle.Multiplying ||
                                                                              pointsWonSoFar >= 20 ||
                                                                              hands[MyIndex].HasX(i.Suit)))))) &&
                                                                         //))) &&
                                                                         !(c1.Value == Hodnota.Desitka &&   //nemaz pokud prvni hrac vyjel desitkou a nevim kdo ma eso (nebylo-li jeste hrano)
                                                                           (_gameType & Hra.Kilo) == 0 &&   //(neplati pri kilu)
                                                                           (_probabilities.PotentialCards(player3).CardCount(c1.Suit) > 2 ||
                                                                            _probabilities.PotentialCards(player1).CardCount(c1.Suit) <= 2) &&
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
                                                                            (RoundNumber >= 8 &&
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) >= 0.5f) ||
                                                                            (RoundNumber >= 6 &&
                                                                             !_bannedSuits.Contains(i.Suit) &&
                                                                             hands[MyIndex].Where(j => !_bannedSuits.Contains(j.Suit))
                                                                                           .SuitCount() == 1 &&
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 / 3f) ||
                                                                            (_probabilities.PotentialCards(player3).Any(j => j.Suit == c1.Suit &&
                                                                                                                             j.Value > c1.Value) &&
                                                                             potentialGreaseCards.Contains(i) &&
                                                                             potentialGreaseCards.Count >= 2) ||
                                                                            ((_gameType & Hra.Kilo) != 0) &&  //u kila zkousim mazat vice
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 / 3f))) &&
                                                                         (gameWinningCard ||
                                                                          !((_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 &&
                                                                             _probabilities.PotentialCards(player1)
                                                                                           .Where(j => j.Suit == i.Suit)
                                                                                           .Count(j => j != c1 &&
                                                                                                       j.Value < Hodnota.Desitka) > 2) ||
                                                                            _probabilities.CertainCards(player1)
                                                                                          .Where(j => j.Suit == i.Suit)
                                                                                          .Any(j => j != c1 &&
                                                                                                    j.Value < Hodnota.Desitka))))
                                                                        .OrderBy(i => _probabilities.SuitProbability(player1, i.Suit, RoundNumber))    //namaz v barve kterou souper nema
                                                                        .ToList();
                        if (RoundNumber >= 6 &&
                            (_gameType & Hra.Kilo) != 0 &&
                            !cardsToPlay.Any())
                        {
                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value >= Hodnota.Desitka &&
                                                                                    c1.Suit != _trump &&
                                                                                    c1.Suit != i.Suit &&
                                                                                    _probabilities.PotentialCards(player3).Any(j => j.Suit == c1.Suit &&
                                                                                                                                    j.Value > c1.Value))
                                                                        .ToList();
                        }
                        //pokud to jde nemaz karty v barve kde souper muze mit desitku
                        if (cardsToPlay.Any(i => !_probabilities.PotentialCards(opponent).HasX(i.Suit)))
                        {
                            cardsToPlay = cardsToPlay.Where(i => !_probabilities.PotentialCards(opponent).HasX(i.Suit))
                                                     .ToList();
                        }
                        var cardToPlay = cardsToPlay.OrderBy(i => _probabilities.SuitProbability(player1, i.Suit, RoundNumber))
                                                    .FirstOrDefault();

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
                                                                                    (_probabilities.UnlikelyCards(player3).HasX(i.Suit) ||
                                                                                     (RoundNumber >= 8 &&
                                                                                     !_probabilities.LikelyCards(player3).HasX(i.Suit) &&
                                                                                     _probabilities.PotentialCards(player3).Any(j => j.Suit == _trump &&
                                                                                                                                     j != c1))) &&
                                                                                    (!hiCards.Any() ||                   //spoluhrac hral nejvyssi kartu co ve hre zbyva
                                                                                     (c1.Suit == _trump &&                  //kolega by byl blazen kdyby se zbavoval trumfove desitky a nemel i eso
                                                                                      c1.Value == Hodnota.Desitka)) &&
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
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value >= Hodnota.Desitka &&               //1. karta je nejvetsi
                                                                                i.Suit != _trump &&                     //a pokud moje A neni trumfove
                                                                                c1.IsHigherThan(i, _trump) &&
                                                                                (PlayerBids[TeamMateIndex] == 0 ||
                                                                                 i.Suit != c1.Suit ||
                                                                                 !potentialGreaseCards.Contains(i)) &&
                                                                                (_probabilities.UnlikelyCards(player3).HasX(i.Suit) ||
                                                                                 (RoundNumber >= 8 &&
                                                                                  !_probabilities.LikelyCards(player3).HasX(i.Suit) &&
                                                                                  _probabilities.PotentialCards(player3).Any(j => j.Suit == _trump &&
                                                                                                                                        j != c1))) &&
                                                                                (_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 ||
                                                                                 (c1.Suit == _trump &&                  //kolega by byl blazen kdyby se zbavoval trumfove desitky a nemel i eso
                                                                                  c1.Value == Hodnota.Desitka)) &&
                                                                                _probabilities.NoSuitHigherThanCardProbability(player3, c1, RoundNumber) >= 1 - RiskFactor &&
                                                                                (_probabilities.PotentialCards(player3).Where(j => j.Suit == c1.Suit)
                                                                                                                       .All(j => j.Value < c1.Value) ||
                                                                                 !_probabilities.PotentialCards(player3).HasSuit(c1.Suit)) &&
                                                                                (_probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor ||
                                                                                 _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                                 (_probabilities.PotentialCards(player3).HasSuit(c1.Suit) &&
                                                                                  potentialGreaseCards.Contains(i) &&
                                                                                  potentialGreaseCards.Count >= 2) ||
                                                                                 ((_gameType & Hra.Kilo) != 0 &&
                                                                                  _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 / 3f))).ToList();
                        if (cardsToPlay.Any(i => i.Value == Hodnota.Eso))
                        {
                            cardsToPlay = cardsToPlay.Where(i => i.Value == Hodnota.Eso).ToList();
                        }
                        cardsToPlay = cardsToPlay.Where(i => !(_probabilities.SuitProbability(player1, i.Suit, RoundNumber) == 0 &&
                                                               _probabilities.PotentialCards(player3)
                                                                             .Where(j => j.Suit == i.Suit)
                                                                             .Count(j => j != c1 &&
                                                                                         j.Value < Hodnota.Desitka) > 2) ||
                                                               _probabilities.CertainCards(player1)
                                                                             .Where(j => j.Suit == i.Suit)
                                                                             .Any(j => j != c1 &&
                                                                                       j.Value < Hodnota.Desitka)).ToList();
                        var cardToPlay = cardsToPlay.OrderBy(i => _probabilities.SuitProbability(player3, i.Suit, RoundNumber))
                                                    .FirstOrDefault();
                        if (cardToPlay != null &&
                            cardsToPlay.HasA(cardToPlay.Suit))
                        {
                            cardToPlay = cardsToPlay.FirstOrDefault(i => i.Suit == cardToPlay.Suit &&
                                                                         i.Value == Hodnota.Eso);
                        }
                        return cardsToPlay.FirstOrDefault();
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule
            {
                Order = 7,
                Description = "odmazat si barvu",
                SkipSimulations = true,
                #region ChooseCard2 Rule7
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
                                                                                    _probabilities.PotentialCards(TeamMateIndex).HasSuit(i.Suit) &&
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
                #endregion
            };

            yield return new AiRule
            {
                Order = 8,
                Description = "zkusit vytáhnout trumfovou X",
                SkipSimulations = true,
                #region ChooseCard2 Rule8
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
                #endregion
            };

            yield return new AiRule
            {
                Order = 9,
                Description = "hrát vysokou kartu mimo A,X",
                SkipSimulations = true,
                #region ChooseCard2 Rule9
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
                                                                         (_probabilities.PotentialCards(player3).HasA(i.Suit) ||
                                                                          _probabilities.PotentialCards(player3).Where(j => j.Suit == i.Suit)
                                                                                                                .All(j => j.Value < i.Value)))
                                                             .OrderByDescending(i => i.Value)
                                                             .FirstOrDefault();
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule
            {
                Order = 10,
                Description = "hrát nízkou kartu mimo A,X",
                SkipSimulations = true,
                #region ChooseCard2 Rule10
                ChooseCard2 = (Card c1) =>
                {
                    var opponentTrumps = TeamMateIndex == -1
                                            ? _probabilities.PotentialCards(player1).Where(i => i.Suit == _trump &&
                                                                                                i != c1)
                                                            .Union(_probabilities.PotentialCards(player3).Where(i => i.Suit == _trump))
                                                            .Distinct()
                                                            .ToList()
                                            : TeamMateIndex == player1
                                                ? _probabilities.PotentialCards(player3).Where(i => i.Suit == _trump).ToList()
                                                : _probabilities.PotentialCards(player1).Where(i => i.Suit == _trump &&
                                                                                                    i != c1).ToList();

                    if (TeamMateIndex == player3 &&
                        ValidCards(c1, hands[MyIndex]).HasX(_trump) &&
                        ValidCards(c1, hands[MyIndex]).Count == 2 &&
                        opponentTrumps.HasA(_trump))
                    {
                        return null;
                    }
                    if (TeamMateIndex != player1)
                    {
                        //-co
                        //-c-
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
                                                                                        i.Value < Hodnota.Desitka &&
                                                                                        (hands[MyIndex].CardCount(i.Suit) == 1 ||
                                                                                         !hands[MyIndex].Where(j => j.Suit == i.Suit)
                                                                                                        .All(j => j.Value <= i.Value)))
                                                                            .ToList();
                                if ((_gameType & Hra.Kilo) != 0 &&
                                    _hlasConsidered == HlasConsidered.First &&
                                    hands[MyIndex].HasK(_trump) &&
                                    hands[MyIndex].HasQ(_trump) &&
                                    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                        .Where(b => b != _trump)
                                        .Any(b => hands[MyIndex].HasK(b) &&
                                                  hands[MyIndex].HasQ(b) &&
                                                  cardsToPlay.HasQ(b)) &&
                                    cardsToPlay.Any(i => i.Value < Hodnota.Svrsek))
                                {
                                    cardsToPlay = cardsToPlay.Where(i => i.Value < Hodnota.Svrsek).ToList();
                                }
                            }
                            if (TeamMateIndex == player3 &&
                                ValidCards(c1, hands[MyIndex]).HasX(_trump) &&
                                ValidCards(c1, hands[MyIndex]).Count == 2 &&
                                opponentTrumps.HasA(_trump))
                            {
                                return null;
                            }
                            if (cardsToPlay.Has7(_trump) &&
                                cardsToPlay.Count > 1 &&
                                hands[MyIndex].Any(i => i.Suit == _trump &&
                                                        i.Value != Hodnota.Sedma &&
                                                        i.Value <= Hodnota.Spodek))
                            {
                                cardsToPlay = cardsToPlay.Where(i => i.Value != Hodnota.Sedma).ToList();
                            }
                            else if (cardsToPlay.Has7(_trump) &&
                                     hands[MyIndex].CardCount(_trump) > 1 &&
                                     opponentTrumps.Count + 1 < hands[MyIndex].CardCount(_trump))
                            {
                                cardsToPlay = cardsToPlay.Where(i => i.Value != Hodnota.Sedma).ToList();
                            }
                            if (cardsToPlay.HasSuit(_trump) ||
                                cardsToPlay.All(i => i.Suit != c1.Suit) ||
                                cardsToPlay.Any(i => hands[MyIndex].HasX(i.Suit) &&
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
                        if (cardsToPlay.Has7(_trump) &&
                            cardsToPlay.Count > 1 &&
                            hands[MyIndex].Any(i => i.Suit == _trump &&
                                                    i.Value != Hodnota.Sedma &&
                                                    i.Value <= Hodnota.Spodek))
                        {
                            cardsToPlay = cardsToPlay.Where(i => i.Value != Hodnota.Sedma).ToList();
                        }
                        if (cardsToPlay.HasSuit(_trump) ||
                            cardsToPlay.All(i => i.Suit != c1.Suit) ||
                            (TeamMateIndex != -1 &&
                             (_probabilities.SuitHigherThanCardProbability(TeamMateIndex, c1, RoundNumber) >= 1 - RiskFactor ||
                              (_probabilities.SuitProbability(TeamMateIndex, c1.Suit, RoundNumber) == 0 &&
                               _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) >= 1 - RiskFactor))) ||
                            (TeamMateIndex == player3 &&
                             cardsToPlay.All(i => i.Suit == c1.Suit) &&
                             cardsToPlay.Any(i => _probabilities.PotentialCards(opponent)
                                                                .Any(j => j.Suit == i.Suit &&
                                                                          j.Value != c1.Value &&
                                                                          j.Value < i.Value))))
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
                #endregion
            };

            yield return new AiRule
            {
                Order = 11,
                Description = "hrát nízkou kartu",
                SkipSimulations = true,
                #region ChooseCard2 Rule11
                ChooseCard2 = (Card c1) =>
                {
                    var opponentTrumps = TeamMateIndex == -1
                        ? _probabilities.PotentialCards(player1).Where(i => i.Suit == _trump &&
                                                                            i != c1)
                                        .Union(_probabilities.PotentialCards(player3).Where(i => i.Suit == _trump))
                                        .Distinct()
                                        .ToList()
                        : TeamMateIndex == player1
                            ? _probabilities.PotentialCards(player3).Where(i => i.Suit == _trump).ToList()
                            : _probabilities.PotentialCards(player1).Where(i => i.Suit == _trump &&
                                                                                i != c1).ToList();

                    if (ValidCards(c1, hands[MyIndex]).Has7(_trump) &&
                        ValidCards(c1, hands[MyIndex]).Count > 1 &&
                        !opponentTrumps.Any())
                    {
                        var cardToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value != Hodnota.Sedma)
                                                                       .OrderBy(i => i.Value)
                                                                       .FirstOrDefault();

                        if (cardToPlay != null)
                        {
                            return cardToPlay;
                        }
                    }
                    if (TeamMateIndex == player3 &&
                        ValidCards(c1, hands[MyIndex]).HasX(_trump) &&
                        ValidCards(c1, hands[MyIndex]).Count == 2 &&
                        opponentTrumps.HasA(_trump) &&
                        !(c1.Suit == _trump &&
                          c1.Value == Hodnota.Eso))
                    {
                        var cardToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                   i.Value == Hodnota.Desitka)
                                                                       .FirstOrDefault();

                        if (cardToPlay != null)
                        {
                            return cardToPlay;
                        }
                    }
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => hands[MyIndex].CardCount(i.Suit) == 1 ||
                                                                                !hands[MyIndex].Where(j => j.Suit == i.Suit)
                                                                                               .All(j => j.Value <= i.Value))
                                                                    .ToList();

                    if (cardsToPlay.Any(i => i.Value < Hodnota.Desitka))
                    {
                        cardsToPlay = cardsToPlay.Where(i => i.Value < Hodnota.Desitka).ToList();
                    }
                    if (TeamMateIndex != player1 &&
                        cardsToPlay.Any(i => !(i.Value == Hodnota.Eso &&
                                               (c1.Value != Hodnota.Desitka ||
                                                c1.Suit != i.Suit) &&
                                               _probabilities.PotentialCards(player1).HasX(i.Suit))))
                    {
                        cardsToPlay = cardsToPlay.Where(i => !(i.Value == Hodnota.Eso &&
                                                               (c1.Value != Hodnota.Desitka ||
                                                                c1.Suit != i.Suit) &&
                                                               _probabilities.PotentialCards(opponent)
                                                                             .HasX(i.Suit)))
                                                 .ToList();
                    }
                    if (cardsToPlay.Any(i => !(i.Suit != _trump &&
                                               hands[MyIndex].HasX(i.Suit) &&
                                               !hands[MyIndex].HasA(i.Suit) &&
                                               hands[MyIndex].CardCount(i.Suit) == 2)))
                    {
                        cardsToPlay = cardsToPlay.Where(i => !(i.Suit != _trump &&
                                                               hands[MyIndex].HasX(i.Suit) &&
                                                               !hands[MyIndex].HasA(i.Suit) &&
                                                               hands[MyIndex].CardCount(i.Suit) == 2)).ToList();
                    }
                    if (cardsToPlay.Any(i => !_bannedSuits.Contains(i.Suit)))
                    {
                        cardsToPlay = cardsToPlay.Where(i => !_bannedSuits.Contains(i.Suit)).ToList();
                    }
                    if (cardsToPlay.Has7(_trump) &&
                             cardsToPlay.Count > 1 &&
                             hands[MyIndex].Any(i => i.Suit == _trump &&
                                                     i.Value != Hodnota.Sedma &&
                                                     i.Value <= Hodnota.Spodek))
                    {
                        cardsToPlay = cardsToPlay.Where(i => i.Value != Hodnota.Sedma ||
                                                             i.Suit != _trump).ToList();
                    }
                    else if (cardsToPlay.Has7(_trump) &&
                             hands[MyIndex].CardCount(_trump) > 1 &&
                             opponentTrumps.Count + 1 < hands[MyIndex].CardCount(_trump))
                    {
                        cardsToPlay = cardsToPlay.Where(i => i.Value != Hodnota.Sedma ||
                                                             i.Suit == _trump).ToList();
                    }
                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(c1, hands[MyIndex]);
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
                            _probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor))) ||
                        (TeamMateIndex == player3 &&
                         cardsToPlay.All(i => i.Suit == c1.Suit) &&
                         cardsToPlay.Any(i => _probabilities.PotentialCards(opponent)
                                                            .Any(j => j.Suit == i.Suit &&
                                                                      j.Value != c1.Value &&
                                                                      j.Value < i.Value))))
                    {
                        return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                    }
                    else
                    {
                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }
                }
                #endregion
            };
        }
    }
}

