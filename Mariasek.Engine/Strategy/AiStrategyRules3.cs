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

        protected override IEnumerable<AiRule> GetRules3(Hand[] hands)
        {
            #region Init Variables3
            var player1 = (MyIndex + 1) % Game.NumPlayers;
            var player2 = (MyIndex + 2) % Game.NumPlayers;
            var opponent = TeamMateIndex == player1 ? player2 : player1;
            var opponentCards = TeamMateIndex == -1
                                ? _probabilities.PotentialCards(player1).Concat(_probabilities.PotentialCards(player2)).Distinct().ToList()
                                : _probabilities.PotentialCards(opponent).ToList();
            #endregion

            BeforeGetRules23(hands);
            if (RoundNumber == 9)
            {
                yield return new AiRule()
                {
                    Order = 0,
                    Description = "hrát tak abych bral poslední štych",
                    #region ChooseCard3 Rule0
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
                        //pokud muzes hrat viteznou desitku, tak pravidlo nehraj
                        if (TeamMateIndex == player1 &&
                            ValidCards(c1, c2, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka &&
                                                                        Round.WinningCard(c1, c2, i, _trump) != c2 &&
                                                                        _probabilities.PotentialCards(player2).HasA(i.Suit)))
                        {
                            return null;
                        }
                        if (TeamMateIndex == player2 &&
                            ValidCards(c1, c2, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka &&
                                                                        Round.WinningCard(c1, c2, i, _trump) != c1 &&
                                                                        _probabilities.PotentialCards(player1).HasA(i.Suit)))
                        {
                            return null;
                        }
                        if (TeamMateIndex == -1 &&
                            ValidCards(c1, c2, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka &&
                                                                        Round.WinningCard(c1, c2, i, _trump) == i &&
                                                                        (_probabilities.PotentialCards(player1).HasA(i.Suit) ||
                                                                         _probabilities.PotentialCards(player2).HasA(i.Suit))))
                        {
                            return null;
                        }
                        var opponent = TeamMateIndex != -1 ? TeamMateIndex == player1 ? player2 : player1 : player1;

                        if (hands[MyIndex].CardCount(_trump) == 2 &&
                            (_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            !(hands[MyIndex].HasX(_trump) &&
                              ((TeamMateIndex == -1 &&
                                (_probabilities.PotentialCards(player1).HasA(_trump) ||
                                 _probabilities.PotentialCards(player2).HasA(_trump))) ||
                               (TeamMateIndex != -1 &&
                                _probabilities.PotentialCards(opponent).HasA(_trump)))) &&
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
                            if (c1.IsHigherThan(c1, _trump) &&
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
                    #endregion
                };
            }

            yield return new AiRule
            {
                Order = 1,
                Description = "hraj vítěznou X",
                SkipSimulations = true,
                #region ChooseCard3 Rule1
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var basicPointsWonSoFar = 0;
                    var basicPointsWonThisRound = (c1.Value >= Hodnota.Desitka ? 10 : 0) +
                                                  (c2.Value >= Hodnota.Desitka ? 10 : 0) + 10;
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
                    var opponent = TeamMateIndex == player1 ? player2 : player1;
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
                                                         (!(c2.Suit == b &&
                                                            c2.Value == Hodnota.Svrsek) &&
                                                          _probabilities.PotentialCards(player2).HasK(b) &&
                                                          _probabilities.PotentialCards(player2).HasQ(b))
                                                          ? b == _trump ? 40 : 20
                                                          : 0)
                                         : Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .Sum(b => !(c1.Suit == b &&
                                                           c1.Value == Hodnota.Svrsek) &&
                                                         !(c2.Suit == b &&
                                                           c2.Value == Hodnota.Svrsek) &&
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

                    if (!gameWinningCard &&
                        (ValidCards(c1, c2, hands[MyIndex]).Count > ((c1.Suit == _trump && c1.Value == Hodnota.Eso) ||
                                                                     (c2.Suit == _trump && c2.Value == Hodnota.Eso) ? 1 : 2) &&
                         ValidCards(c1, c2, hands[MyIndex]).HasX(_trump)))
                    {
                        if (TeamMateIndex != -1 &&
                            hands[MyIndex].CardCount(_trump) == 3)
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
                    //trumfovou desitku musim hrat pokud mi zbyvaji posledni 2 trumfy a souperi maji jeste na ruce trumfove eso
                    if (ValidCards(c1, c2, hands[MyIndex]).HasX(_trump) &&
                        ValidCards(c1, c2, hands[MyIndex]).Count == 2 &&
                        ((TeamMateIndex == -1 &&
                          (_probabilities.PotentialCards(player1).Where(i => i != c1).HasA(_trump) ||
                           _probabilities.PotentialCards(player2).Where(i => i != c2).HasA(_trump))) ||
                         (TeamMateIndex == player1 &&
                          _probabilities.PotentialCards(player2).Where(i => i != c2).HasA(_trump)) ||
                         (TeamMateIndex == player2 &&
                          _probabilities.PotentialCards(player1).Where(i => i != c1).HasA(_trump))))
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

                        var cardToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                       i.Value == Hodnota.Desitka)
                                                                           .FirstOrDefault();
                        if (cardToPlay != null)
                        {
                            return cardToPlay;
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
                        if (cardToPlay == null &&
                           hands[MyIndex].HasX(_trump) &&
                           hands[MyIndex].Has7(_trump) &&
                           hands[MyIndex].SuitCount == 1 &&
                           !_probabilities.PotentialCards(player1).HasSuit(_trump) &&
                           !_probabilities.PotentialCards(player2).HasSuit(_trump))
                        {
                            cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Desitka &&
                                                                                                i.Suit == _trump);
                        }
                        return cardToPlay;
                    }
                    else if (TeamMateIndex == player1)
                    {
                        //nehraj pokud ma oponent jiste dalsi karty v barve a muzes hrat i neco jineho
                        if (!gameWinningCard &&
                            ValidCards(c1, hands[MyIndex]).Count > 2 &&
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
                                                                                                j.Value < i.Value) > (hands[MyIndex].HasA(i.Suit) ? 3 : 2) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)) ||
                                                                     _probabilities.CertainCards(player2)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1 &&
                                                                                              j != c2 &&
                                                                                              j.Value < i.Value) > (hands[MyIndex].HasA(i.Suit) ? 1 : 0))))
                        {
                            return null;
                        }
                        if (!gameWinningCard &&
                            c1.Suit != _trump &&
                            ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka &&
                                                                        i.Suit == c1.Suit) &&
                            !(ValidCards(c1, c2, hands[MyIndex]).CardCount(c1.Suit) == 2 &&
                              _probabilities.PotentialCards(player2).HasA(c1.Suit)) &&
                            (_probabilities.LikelyCards(player2).Where(i => i != c2).HasSuit(c1.Suit) ||
                             (!_probabilities.PotentialCards(player1).HasSuit(c1.Suit) &&
                              _probabilities.PotentialCards(player2).Where(i => i != c2).CardCount(c1.Suit) > 2 - _probabilities.CertainCards(Game.TalonIndex).CardCount(c1.Suit))))
                        {
                            return null;
                        }
                        var opHiTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                             .Where(h => h > myHighestTrumpAfterX)
                                             .Count(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > _epsilon);

                        var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                      !hands[MyIndex].HasA(i.Suit) &&
                                                                                      !(_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) == 0 &&  //ignoruj kartu pokus s ni muzes prebot akterovu nizkou barvu
                                                                                        Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                            .Select(h => new Card(i.Suit, h))
                                                                                            .Where(j => j != c2)
                                                                                            .Any(j => _probabilities.CardProbability(player2, j) >= 1 - _epsilon)) &&
                                                                                      !(!_probabilities.PotentialCards(player1).HasSuit(i.Suit) &&          //nehraj pokud ma oponent jiste nizsi kartu v barve
                                                                                        _probabilities.PotentialCards(player2)
                                                                                                      .Where(j => j != c2 &&
                                                                                                                  j != new Card(i.Suit, Hodnota.Eso))
                                                                                                      .CardCount(i.Suit) > 2 - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)) &&    //(kdyz kolega urcite barvu nezna a souper ma aspon 3 karty v barve)
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
                                                                                      Round.WinningCard(c1, c2, i, _trump) != c2)
                                                                            .ToList();
                        if (cardsToPlay.Any(i => hands[MyIndex].HasA(i.Suit)))
                        {
                            return null; //pokud mas v nejake barve A i X, tak pravidlo nehraj a nejprv se zbav esa
                        }
                        var cardToPlay = cardsToPlay.OrderBy(i => _probabilities.SuitProbability(player2, i.Suit, RoundNumber))
                                                    .FirstOrDefault();
                        if (cardToPlay == null &&
                           hands[MyIndex].HasX(_trump) &&
                           hands[MyIndex].Has7(_trump) &&
                           hands[MyIndex].SuitCount == 1 &&
                           !_probabilities.PotentialCards(player1).HasSuit(_trump) &&
                           !_probabilities.PotentialCards(player2).HasSuit(_trump))
                        {
                            cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Desitka &&
                                                                                                i.Suit == _trump);
                        }
                        if (cardToPlay == null &&
                            c1.IsHigherThan(c2, _trump) &&
                            ValidCards(c1, c2, hands[MyIndex]).CardCount(c1.Suit) == 2 &&
                            _probabilities.PotentialCards(player1).HasA(c1.Suit))
                        {
                            cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Desitka);
                        }
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
                        if (!gameWinningCard &&
                            (_probabilities.SuitProbability(player2, c1.Suit, RoundNumber) == 0 ||
                             (c2.Suit == c1.Suit &&
                              c2.Value == Hodnota.Eso)) &&
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
                            ((_probabilities.SuitProbability(player2, c1.Suit, RoundNumber) == 0 &&
                              _probabilities.PotentialCards(player1)
                                            .Where(j => j.Suit == c1.Suit)
                                            .Count(j => j != c1 &&
                                                        j != c2 &&
                                                        j.Value < c1.Value) > 2 - _probabilities.CertainCards(Game.TalonIndex).CardCount(c1.Suit)) ||
                             (_probabilities.SuitProbability(player2, c1.Suit, RoundNumber) == 0 &&
                              c1.IsLowerThan(c2, _trump) &&
                              _probabilities.PotentialCards(player1)
                                            .Where(j => j.Suit == c1.Suit)
                                            .Count(j => j != c1 &&
                                                        j != c2 &&
                                                        j.Value < Hodnota.Desitka) > 2 - _probabilities.CertainCards(Game.TalonIndex).CardCount(c1.Suit)) ||
                             _probabilities.CertainCards(player1)
                                           .Where(j => j.Suit == c1.Suit)
                                           .Any(j => j != c1 &&
                                                     j != c2 &&
                                                     j.Value < Hodnota.Desitka)))
                        {
                            return null;
                        }
                        //Pokud akter ma dalsi karty v barve kterou vyjizdel (vyjma esa)
                        //a pokud mam desitku a muj spoluhrac hral trumfem nebo esem v barve, tak desitku nehraj
                        //Pokud by spoluhracovi pozdeji dosly trumfy udrzi me pak desitka ve stychu
                        if (!gameWinningCard &&
                            c1.Suit != _trump &&
                            (c2.Suit == _trump ||
                             (c2.Suit == c1.Suit &&
                              c2.Value == Hodnota.Eso &&
                              !(_calculationStyle == CalculationStyle.Multiplying &&
                                (_gameType & Hra.Kilo) != 0))) &&
                            ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka) &&
                            ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka)
                                                              .All(i => (c1.IsLowerThan(c2, _trump) ||
                                                                         c1.IsLowerThan(i, _trump)) &&
                                                                        _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                        ((!_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                                          Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                              .Where(h => h < i.Value)
                                                                              .Select(h => new Card(i.Suit, h))
                                                                              .Where(j => j != c1)
                                                                              .Count(j => _probabilities.PotentialCards(player1).Contains(j)) > 2 - _probabilities.CertainCards(Game.TalonIndex).Length) ||
                                                                         Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                            .Where(h => h < i.Value)
                                                                            .Select(h => new Card(i.Suit, h))
                                                                            .Where(j => j != c1)
                                                                            .Any(j => _probabilities.CertainCards(player1).Contains(j)))))
                        {
                            return null;
                        }
                        var nonTrumpAXCount = hands[MyIndex].Where(i => i.Suit != _trump)
                                                            .Count(i => i.Value >= Hodnota.Desitka);
                        if (!gameWinningCard &&
                            c1.Suit != _trump &&
                            ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Value == Hodnota.Desitka &&
                                                                        i.Suit == c1.Suit) &&
                            !(ValidCards(c1, c2, hands[MyIndex]).CardCount(c1.Suit) == 2 &&
                              _probabilities.PotentialCards(player1).HasA(c1.Suit)) &&
                            (_probabilities.LikelyCards(player1).Where(i => i != c1).CardCount(c1.Suit) > nonTrumpAXCount ||
                             (!_probabilities.PotentialCards(player2).HasSuit(c1.Suit) &&
                              _probabilities.PotentialCards(player1).Where(i => i != c1).CardCount(c1.Suit) - 2
                              + _probabilities.CertainCards(Game.TalonIndex).CardCount(c1.Suit) > nonTrumpAXCount)))
                        {
                            return null;
                        }
                        //pocet souperovych trumfu vyssi nez muj nejvyssi trumf mensi nez X
                        var opHiTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                             .Where(h => h > myHighestTrumpAfterX)
                                             .Count(h => _probabilities.CardProbability(player1, new Card(_trump, h)) > _epsilon);

                        var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                        (gameWinningCard ||
                                                                                         (!(_probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                            Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                                .Select(h => new Card(i.Suit, h))
                                                                                                .Where(j => j != c1)
                                                                                                .Any(j => _probabilities.CardProbability(player1, j) == 1)) &&
                                                                                          !(!_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&          //nehraj pokud ma oponent jiste nizsi kartu v barve
                                                                                            _probabilities.PotentialCards(player1)
                                                                                                          .Where(j => j != c1 &&
                                                                                                                      j != new Card(i.Suit, Hodnota.Eso))
                                                                                                          .CardCount(i.Suit) > 2 - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)) &&    //(kdyz kolega urcite barvu nezna a souper ma aspon 3 karty v barve)
                                                                                          !(_probabilities.PotentialCards(player1)
                                                                                                          .Where(j => j != c1 &&
                                                                                                                      j != new Card(i.Suit, Hodnota.Eso))
                                                                                                          .HasSuit(i.Suit) &&
                                                                                            _probabilities.PotentialCards(player1)
                                                                                                          .Where(j => j != c1)
                                                                                                          .SuitCount() == 1))) &&
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
                                                                                            Round.WinningCard(c1, c2, i, _trump) != c1)
                                                                           .ToList();
                        if (cardsToPlay.Any(i => hands[MyIndex].HasA(i.Suit)))
                        {
                            return null; //pokud mas v nejake barve A i X, tak pravidlo nehraj a nejprv se zbav esa
                        }
                        if (cardsToPlay.Any(i => !opponentCards.HasSuit(i.Suit)))
                        {
                            cardsToPlay = cardsToPlay.Where(i => !opponentCards.HasSuit(i.Suit)).ToList();
                        }
                        var cardToPlay = cardsToPlay.OrderBy(i => _probabilities.SuitProbability(player1, i.Suit, RoundNumber))
                                                    .FirstOrDefault();

                        if (cardToPlay == null &&
                           hands[MyIndex].HasX(_trump) &&
                           hands[MyIndex].Has7(_trump) &&
                           hands[MyIndex].SuitCount == 1 &&
                           !_probabilities.PotentialCards(player1).HasSuit(_trump) &&
                           !_probabilities.PotentialCards(player2).HasSuit(_trump))
                        {
                            cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Desitka &&
                                                                                                i.Suit == _trump);
                        }
                        if (cardToPlay == null &&
                            c1.IsLowerThan(c2, _trump) &&
                            ValidCards(c1, c2, hands[MyIndex]).CardCount(c1.Suit) == 2 &&
                            _probabilities.PotentialCards(player1).HasA(c1.Suit))
                        {
                            cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Desitka);
                        }
                        if (cardToPlay != null &&
                            ValidCards(c1, c2, hands[MyIndex]).HasA(cardToPlay.Suit))
                        {
                            cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Suit == cardToPlay.Suit &&
                                                                                                i.Value == Hodnota.Eso);
                        }
                        return cardToPlay;
                    }
                }
                #endregion
            };

            yield return new AiRule
            {
                Order = 2,
                Description = "hraj vítězné A",
                SkipSimulations = true,
                #region ChooseCard3 Rule2
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var basicPointsWonSoFar = 0;
                    var basicPointsWonThisRound = (c1.Value >= Hodnota.Desitka ? 10 : 0) +
                                                  (c2.Value >= Hodnota.Desitka ? 10 : 0) + 10;
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
                    var opponent = TeamMateIndex == player1 ? player2 : player1;
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
                                                         (!(c2.Suit == b &&
                                                            c2.Value == Hodnota.Svrsek) &&
                                                          _probabilities.PotentialCards(player2).HasK(b) &&
                                                          _probabilities.PotentialCards(player2).HasQ(b))
                                                          ? b == _trump ? 40 : 20
                                                          : 0)
                                         : Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .Sum(b => !(c1.Suit == b &&
                                                           c1.Value == Hodnota.Svrsek) &&
                                                         !(c2.Suit == b &&
                                                           c2.Value == Hodnota.Svrsek) &&
                                                         _probabilities.PotentialCards(opponent).HasK(b) &&
                                                         _probabilities.PotentialCards(opponent).HasQ(b)
                                                         ? b == _trump ? 40 : 20
                                                         : 0); var opponentPotentialPoints = basicPointsLost + hlasPointsLost + basicPointsLeft + hlasPointsLeft;
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

                    if (TeamMateIndex == -1)
                    {
                        //--c
                        var cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
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

                        if (cardToPlay == null &&
                           hands[MyIndex].HasA(_trump) &&
                           hands[MyIndex].Has7(_trump) &&
                           hands[MyIndex].SuitCount == 1 &&
                           !_probabilities.PotentialCards(player1).HasSuit(_trump) &&
                           !_probabilities.PotentialCards(player2).HasSuit(_trump))
                        {
                            cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                                i.Suit == _trump);
                        }
                        return cardToPlay;
                    }
                    else if (TeamMateIndex == player1)
                    {
                        //o-c
                        //nehraj pokud ma prvni hrac jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (!gameWinningCard &&
                            ValidCards(c1, c2, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Value == Hodnota.Eso) &&
                            ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso)
                                                          .All(i => i.Suit != _trump &&
                                                                    i.Suit != _trump &&
                                                                    i.Suit == c2.Suit &&
                                                                    ((_probabilities.SuitProbability(player1, i.Suit, RoundNumber) == 0 &&
                                                                      _probabilities.PotentialCards(player2)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1 &&
                                                                                                j != c2) > (hands[MyIndex].HasX(i.Suit) ? 3 : 2) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)) ||
                                                                     _probabilities.CertainCards(player2)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1 &&
                                                                                                j != c2) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0))))
                        {
                            return null;
                        }
                        if (!gameWinningCard &&
                            c1.Suit != _trump &&
                            ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Value == Hodnota.Eso &&
                                                                        i.Suit == c1.Suit) &&
                            (_probabilities.LikelyCards(player2).Where(i => i != c2).HasSuit(c1.Suit) ||
                             (!_probabilities.PotentialCards(player1).HasSuit(c1.Suit) &&
                              _probabilities.PotentialCards(player2).Where(i => i != c2).CardCount(c1.Suit) > 2)))
                        {
                            return null;
                        }
                        var cardToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                                      i.Suit != _trump &&
                                                                                      Round.WinningCard(c1, c2, i, _trump) != c2 &&
                                                                                      ((c2.Suit == i.Suit &&
                                                                                        c2.Value == Hodnota.Desitka) ||
                                                                                        _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) == 0 ||
                                                                                       (RoundNumber >= 8 &&
                                                                                        !_probabilities.LikelyCards(player2).HasX(i.Suit) &&
                                                                                        _probabilities.PotentialCards(player2).Any(j => j.Suit == _trump &&
                                                                                                                                        j != c1 &&
                                                                                                                                        j != c2)) ||
                                                                                       (((_gameType & Hra.Kilo) != 0) &&
                                                                                        _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon)) &&
                                                                                      ((gameWinningCard &&
                                                                                        !_probabilities.CertainCards(player2).HasX(i.Suit)) ||
                                                                                       !((_probabilities.SuitProbability(player1, i.Suit, RoundNumber) == 0 &&
                                                                                          _probabilities.PotentialCards(player2)
                                                                                                        .Where(j => j.Suit == i.Suit)
                                                                                                        .Count(j => j != c1 &&
                                                                                                                    j != c2) > 2) ||
                                                                                         _probabilities.CertainCards(player2)
                                                                                                       .Where(j => j.Suit == i.Suit)
                                                                                                       .Count(j => j != c1 &&
                                                                                                                   j != c2) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0))))
                                                                            .OrderBy(i => _probabilities.SuitProbability(player2, i.Suit, RoundNumber))
                                                                            .FirstOrDefault();
                        if (cardToPlay == null &&
                           hands[MyIndex].HasA(_trump) &&
                           hands[MyIndex].Has7(_trump) &&
                           hands[MyIndex].SuitCount == 1 &&
                           !_probabilities.PotentialCards(player1).HasSuit(_trump) &&
                           !_probabilities.PotentialCards(player2).HasSuit(_trump))
                        {
                            cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                                i.Suit == _trump);
                        }
                        return cardToPlay;
                    }
                    else
                    {
                        //-oc
                        //nehraj pokud ma prvni hrac jiste dalsi male karty v barve a muzes hrat i neco jineho
                        if (!gameWinningCard &&
                            ValidCards(c1, c2, hands[MyIndex]).Count > 1 &&
                            ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Value == Hodnota.Eso) &&
                            ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso)
                                                          .All(i => i.Suit != _trump &&
                                                                    i.Suit != _trump &&
                                                                    i.Suit == c1.Suit &&
                                                                    ((_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0 &&
                                                                      _probabilities.PotentialCards(player1)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1) > 2 - _probabilities.CertainCards(Game.TalonIndex).Length) ||
                                                                     _probabilities.CertainCards(player1)
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0))))
                        {
                            return null;
                        }
                        var nonTrumpAXCount = hands[MyIndex].Where(i => i.Suit != _trump)
                                                            .Count(i => i.Value >= Hodnota.Desitka);
                        if (!gameWinningCard &&
                            c1.Suit != _trump &&
                            ValidCards(c1, c2, hands[MyIndex]).Any(i => i.Value == Hodnota.Eso &&
                                                                        i.Suit == c1.Suit) &&
                            (_probabilities.LikelyCards(player1).Where(i => i != c1).CardCount(c1.Suit) > nonTrumpAXCount ||
                             (!_probabilities.PotentialCards(player2).HasSuit(c1.Suit) &&
                              _probabilities.PotentialCards(player1).Where(i => i != c1).CardCount(c1.Suit) - 2 > nonTrumpAXCount)))
                        {
                            return null;
                        }
                        //if (hands[MyIndex].HasSuit(_trump) &&
                        //    myInitialHand.CardCount(_trump) <= 1 &&
                        //    !hands[MyIndex].HasA(_trump) &&
                        //    !hands[MyIndex].HasX(_trump) &&
                        //    potentialGreaseCards.All(i => myInitialHand.CardCount(i.Suit) >= 5 &&
                        //                                  !_probabilities.CertainCards(opponent).HasSuit(i.Suit)))
                        //{
                        //    return ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit == _trump).FirstOrDefault();
                        //}
                        var cardToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                                      i.Suit != _trump &&
                                                                                      Round.WinningCard(c1, c2, i, _trump) != c1 &&
                                                                                      ((gameWinningCard &&
                                                                                        !_probabilities.CertainCards(player1).HasX(i.Suit)) ||
                                                                                       (((c1.Suit == i.Suit &&
                                                                                          c1.Value == Hodnota.Desitka) ||
                                                                                         _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) == 0 ||
                                                                                         (RoundNumber >= 8 &&
                                                                                          !_probabilities.LikelyCards(player1).HasX(i.Suit) &&
                                                                                          _probabilities.PotentialCards(player1).Any(j => j.Suit == _trump &&
                                                                                                                                          j != c1 &&
                                                                                                                                          j != c2)) ||
                                                                                         (((_gameType & Hra.Kilo) != 0) &&
                                                                                         _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon)) &&
                                                                                        !((_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0 &&
                                                                                           _probabilities.PotentialCards(player1)
                                                                                                         .Where(j => j.Suit == i.Suit)
                                                                                                         .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 3 : 2) - _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit)) ||
                                                                                          _probabilities.CertainCards(player1)
                                                                                                        .Where(j => j.Suit == i.Suit)
                                                                                                        .Count(j => j != c1) > (hands[MyIndex].HasX(i.Suit) ? 1 : 0) ||
                                                                                          (_probabilities.PotentialCards(player1)
                                                                                                         .Where(j => j.Suit == i.Suit)
                                                                                                         .Count(j => j != c1) > 1) &&
                                                                                           _probabilities.PotentialCards(player1)
                                                                                                         .Where(j => j != c1)
                                                                                                         .SuitCount() == 1))))
                                                                            .OrderBy(i => _probabilities.SuitProbability(player1, i.Suit, RoundNumber))
                                                                            .FirstOrDefault();
                        if (cardToPlay == null &&
                           hands[MyIndex].HasA(_trump) &&
                           hands[MyIndex].Has7(_trump) &&
                           hands[MyIndex].SuitCount == 1 &&
                           !_probabilities.PotentialCards(player1).HasSuit(_trump) &&
                           !_probabilities.PotentialCards(player2).HasSuit(_trump))
                        {
                            cardToPlay = ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                                i.Suit == _trump);
                        }
                        return cardToPlay;
                    }
                }
                #endregion
            };

            yield return new AiRule
            {
                Order = 3,
                Description = "odmazat si barvu",
                SkipSimulations = true,
                #region ChooseCard3 Rule3
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    return ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                         i.Suit != c1.Suit &&
                                                                         !hands[MyIndex].HasSuit(_trump) &&
                                                                         i.Value != Hodnota.Eso &&
                                                                         i.Value != Hodnota.Desitka &&
                                                                         ((TeamMateIndex != -1 &&
                                                                           _probabilities.PotentialCards(TeamMateIndex).HasSuit(i.Suit) &&
                                                                           hands[MyIndex].Any(j => j.Value == Hodnota.Eso ||
                                                                                                   j.Value == Hodnota.Desitka)) ||
                                                                          (TeamMateIndex == -1 &&
                                                                           hands[MyIndex].HasSuit(_trump))) &&
                                                                         hands[MyIndex].CardCount(i.Suit) == 1 &&
                                                                         !_bannedSuits.Contains(i.Suit))
                                                             .OrderBy(i => i.Value)
                                                             .FirstOrDefault();
                }
                #endregion
            };

            yield return new AiRule
            {
                Order = 4,
                Description = "hrát vysokou kartu mimo A,X",
                SkipSimulations = true,
                #region ChooseCard3 Rule4
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
                #endregion
            };

            yield return new AiRule
            {
                Order = 5,
                Description = "hrát nízkou kartu mimo A,X",
                SkipSimulations = true,
                #region ChooseCard3 Rule5
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var opponentTrumps = TeamMateIndex == -1
                        ? _probabilities.PotentialCards(player1).Where(i => i.Suit == _trump &&
                                                                            i != c1)
                                        .Union(_probabilities.PotentialCards(player2).Where(i => i.Suit == _trump &&
                                                                                                 i != c2))
                                        .Distinct()
                                        .ToList()
                        : TeamMateIndex == player1
                            ? _probabilities.PotentialCards(player2).Where(i => i.Suit == _trump &&
                                                                                i != c2).ToList()
                            : _probabilities.PotentialCards(player1).Where(i => i.Suit == _trump &&
                                                                                i != c1).ToList();

                    if (ValidCards(c1, c2, hands[MyIndex]).HasX(_trump) &&
                        ValidCards(c1, c2, hands[MyIndex]).Count == 2 &&
                        opponentTrumps.HasA(_trump))
                    {
                        return null;
                    }
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
                    if (RoundNumber < 8 &&
                        catchCardsPerSuitNoAX.Any())
                    {
                        var preferredSuit = catchCardsPerSuitNoAX.Where(i => !_bannedSuits.Contains(i.Key) &&
                                                                             (TeamMateIndex == -1 ||
                                                                              PlayerBids[TeamMateIndex] == 0 ||
                                                                              !_teamMatesSuits.Contains(i.Key)))
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
                    if (cardsToPlay.Has7(_trump) &&
                        cardsToPlay.Count > 1 &&
                        hands[MyIndex].Any(i => i.Suit == _trump &&
                                                i.Value != Hodnota.Sedma &&
                                                i.Value <= Hodnota.Svrsek))
                    {
                        cardsToPlay = cardsToPlay.Where(i => i.Value != Hodnota.Sedma).ToList();
                    }
                    if (cardsToPlay.HasSuit(_trump) ||
                        cardsToPlay.All(i => i.Suit != c1.Suit) ||
                        (TeamMateIndex == player1 && c1.IsHigherThan(c2, _trump)) ||
                        (TeamMateIndex == player2 && c1.IsLowerThan(c2, _trump)) ||
                        (TeamMateIndex != -1 &&
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
                        if (cardsToPlay.Any(i => i.Value < Hodnota.Desitka))
                        {
                            cardsToPlay = cardsToPlay.Where(i => i.Value < Hodnota.Desitka).ToList();
                        }
                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }
                }
                #endregion
            };

            yield return new AiRule
            {
                Order = 6,
                Description = "hrát nízkou kartu",
                SkipSimulations = true,
                #region ChooseCard3 Rule6
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = new List<Card>();
                    var opponentTrumps = TeamMateIndex == -1
                        ? _probabilities.PotentialCards(player1).Where(i => i.Suit == _trump &&
                                                                            i != c1)
                                        .Union(_probabilities.PotentialCards(player2).Where(i => i.Suit == _trump &&
                                                                                                 i != c2))
                                        .Distinct()
                                        .ToList()
                        : TeamMateIndex == player1
                            ? _probabilities.PotentialCards(player2).Where(i => i.Suit == _trump &&
                                                                                i != c2).ToList()
                            : _probabilities.PotentialCards(player1).Where(i => i.Suit == _trump &&
                                                                                i != c1).ToList();

                    if (ValidCards(c1, c2, hands[MyIndex]).Has7(_trump) &&
                        ValidCards(c1, c2, hands[MyIndex]).Count > 1 &&
                        !opponentTrumps.Any())
                    {
                        var cardToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value != Hodnota.Sedma)
                                                                           .OrderBy(i => i.Value)
                                                                           .FirstOrDefault();
                        if (cardToPlay != null)
                        {
                            return cardToPlay;
                        }
                    }

                    if (ValidCards(c1, c2, hands[MyIndex]).HasX(_trump) &&
                        ValidCards(c1, c2, hands[MyIndex]).Count == 2 &&
                        opponentTrumps.HasA(_trump) &&
                        !(c1.Suit == _trump &&
                          c1.Value == Hodnota.Eso) &&
                        !(c2.Suit == _trump &&
                          c2.Value == Hodnota.Eso))
                    {
                        cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                    i.Value == Hodnota.Desitka)
                                                                        .ToList();
                    }
                    if (TeamMateIndex != -1)
                    {
                        //preferuj barvu kde soupere nechytam
                        var holesPerSuit = new Dictionary<Barva, int>();
                        var hiCardsPerSuit = new Dictionary<Barva, int>();
                        var catchCardsPerSuit = new Dictionary<Barva, int>();
                        var opponent = TeamMateIndex == player1 ? player2 : player1;

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
                        var validSuits = ValidCards(c1, c2, hands[MyIndex]).Select(i => i.Suit).Distinct().ToList();
                        var catchCardsPerSuitNoAX = catchCardsPerSuit.Where(i => validSuits.Contains(i.Key) &&
                                                                                 hands[MyIndex].Any(j => j.Suit == i.Key &&
                                                                                                         j.Value != Hodnota.Eso &&
                                                                                                         j.Value != Hodnota.Desitka));
                        if (catchCardsPerSuitNoAX.Any() || hands[MyIndex].All(i => i.Suit == _trump ||
                                                                                   i.Value >= Hodnota.Desitka))
                        {
                            var preferredSuit = catchCardsPerSuitNoAX.Where(i => !_bannedSuits.Contains(i.Key) &&
                                                                                 !(hands[MyIndex].HasX(i.Key) &&
                                                                                   !hands[MyIndex].HasA(i.Key) &&
                                                                                   hands[MyIndex].CardCount(i.Key) == 2))
                                                                     .OrderBy(i => i.Value)
                                                                     .Select(i => (Barva?)i.Key)
                                                                     .FirstOrDefault();

                            if (!preferredSuit.HasValue &&
                                hands[MyIndex].All(i => i.Suit == _trump ||
                                                        i.Value >= Hodnota.Desitka))
                            {
                                preferredSuit = catchCardsPerSuit.Where(i => validSuits.Contains(i.Key) &&
                                                                             !_bannedSuits.Contains(i.Key) &&
                                                                             (TeamMateIndex == -1 ||
                                                                              PlayerBids[TeamMateIndex] == 0 ||
                                                                              !_teamMatesSuits.Contains(i.Key)) &&
                                                                             !(hands[MyIndex].HasX(i.Key) &&
                                                                               !hands[MyIndex].HasA(i.Key) &&
                                                                               hands[MyIndex].CardCount(i.Key) == 2))
                                                                 .OrderBy(i => i.Value)
                                                                 .Select(i => (Barva?)i.Key)
                                                                 .FirstOrDefault();
                            }
                            if (preferredSuit.HasValue)
                            {
                                cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit == preferredSuit.Value &&
                                                                                            i.Value != Hodnota.Eso &&
                                                                                            i.Value != Hodnota.Desitka &&
                                                                                            (hands[MyIndex].CardCount(i.Suit) == 1 ||
                                                                                             !hands[MyIndex].Where(j => j.Suit == i.Suit)
                                                                                                            .All(j => j.Value <= i.Value)))
                                                                                .ToList();
                                if (hands[MyIndex].All(i => i.Suit == _trump ||
                                                            i.Value >= Hodnota.Desitka))
                                {
                                    cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit == preferredSuit.Value)
                                                                                    .ToList();
                                }
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
                        }
                    }

                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).ToList();
                    }

                    if (TeamMateIndex != -1 &&
                        cardsToPlay.Any(i => !(i.Value == Hodnota.Eso &&
                                               (c1.Value != Hodnota.Desitka ||
                                                c1.Suit != i.Suit) &&
                                               (c2.Value != Hodnota.Desitka ||
                                                c2.Suit != i.Suit) &&
                                               _probabilities.PotentialCards(opponent).HasX(i.Suit))))
                    {
                        cardsToPlay = cardsToPlay.Where(i => !(i.Value == Hodnota.Eso &&
                                                               (c1.Value != Hodnota.Desitka ||
                                                                c1.Suit != i.Suit) &&
                                                               (c2.Value != Hodnota.Desitka ||
                                                                c2.Suit != i.Suit) &&
                                                               _probabilities.PotentialCards(opponent)
                                                                             .HasX(i.Suit)))
                                                 .ToList();
                    }
                    if (cardsToPlay.HasX(_trump) &&
                        cardsToPlay.Count == 2 &&
                        ((TeamMateIndex != player1 &&
                          c1.Suit == _trump &&
                          c1.Value == Hodnota.Eso) ||
                         (TeamMateIndex != player2 &&
                          c2.Suit == _trump &&
                          c2.Value == Hodnota.Eso)))
                    {
                        cardsToPlay = cardsToPlay.Where(i => i.Value != Hodnota.Desitka).ToList();
                    }
                    else if (cardsToPlay.Has7(_trump) &&
                             cardsToPlay.Count > 1 &&
                             hands[MyIndex].Any(i => i.Suit == _trump &&
                                                     i.Value != Hodnota.Sedma &&
                                                     i.Value <= Hodnota.Svrsek))
                    {
                        cardsToPlay = cardsToPlay.Where(i => i.Value != Hodnota.Sedma).ToList();
                    }
                    else if (cardsToPlay.Has7(_trump) &&
                             hands[MyIndex].CardCount(_trump) > 1 &&
                             opponentTrumps.Count + 1 < hands[MyIndex].CardCount(_trump))
                    {
                        cardsToPlay = cardsToPlay.Where(i => i.Value != Hodnota.Sedma).ToList();
                    }
                    if (TeamMateIndex != -1 &&
                        cardsToPlay.Any(i => PlayerBids[TeamMateIndex] == 0 ||
                                             !_teamMatesSuits.Contains(i.Suit)))
                    {
                        cardsToPlay = cardsToPlay.Where(i => PlayerBids[TeamMateIndex] == 0 ||
                                                             !_teamMatesSuits.Contains(i.Suit)).ToList();
                    }
                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(c1, c2, hands[MyIndex]);
                    }
                    if (cardsToPlay.HasSuit(_trump) ||
                        cardsToPlay.All(i => i.Suit != c1.Suit) ||
                        (TeamMateIndex == player1 &&
                         c1.IsHigherThan(c2, _trump) &&
                         !cardsToPlay.Any(i => i.Value == Hodnota.Desitka &&
                                               hands[MyIndex].CardCount(i.Suit) == 2 &&
                                               _probabilities.PotentialCards(opponent).HasA(i.Suit))) ||
                        (TeamMateIndex == player2 &&
                         c1.IsLowerThan(c2, _trump) &&
                         !cardsToPlay.Any(i => i.Value == Hodnota.Desitka &&
                                               hands[MyIndex].CardCount(i.Suit) == 2 &&
                                               _probabilities.PotentialCards(opponent).HasA(i.Suit))) ||
                        (TeamMateIndex != -1 &&
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
                        if (cardsToPlay.Any(i => i.Value < Hodnota.Desitka))
                        {
                            cardsToPlay = cardsToPlay.Where(i => i.Value < Hodnota.Desitka).ToList();
                        }
                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }
                }
                #endregion
            };
        }
    }
}

