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

        protected override IEnumerable<AiRule> GetRules1(Hand[] hands)
        {
            #region InitVariables1
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

            var opponent = TeamMateIndex == player2 ? player3 : player2;
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
            //var unwinnableLowCards = hands[MyIndex].Where(i => holesPerSuit[i.Suit].Any(j => j.Value > i.Value))
            //                                       .ToList();
            var unwinnableLowCards = GetUnwinnableLowCards();
            //pouzivam 0 misto epsilon protoze jinak bych mohl hrat trumfovou x a myslet si ze souper nema eso a on by ho zrovna mel!
            //holes zavisi jen na dirach vuci souperum narozdil od holesPerSuit ktery zavisi na vsech mych dirach
            var holes = TeamMateIndex == -1
                        ? Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                              .Where(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > (h == Hodnota.Eso ? 0 : _epsilon) ||
                                          _probabilities.CardProbability(player3, new Card(_trump, h)) > (h == Hodnota.Eso ? 0 : _epsilon))
                              .ToList()
                        : Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                              .Where(h => _probabilities.CardProbability(opponent, new Card(_trump, h)) > (h == Hodnota.Eso ? 0 : _epsilon))
                              .ToList();
            var opponentCards = TeamMateIndex == -1
                                ? _probabilities.PotentialCards(player2).Concat(_probabilities.PotentialCards(player3)).Distinct().ToList()
                                : _probabilities.PotentialCards(opponent).ToList();
            var opponentTrumps = opponentCards.Where(i => i.Suit == _trump).ToList();
            var opponentLoCards = TeamMateIndex != -1
                                  ? opponentCards.Where(i => !hands[MyIndex].HasSuit(i.Suit) &&
                                                             _probabilities.SuitHigherThanCardProbability(TeamMateIndex, i, RoundNumber) > 0)
                                                 .ToList()
                                  : new List<Card>();
            var teamMatePlayedCards = TeamMateIndex != -1
                                      ? _rounds.Where(r => r != null && r.c3 != null)
                                             .Select(r =>
                                             {
                                                 if (r.player1.PlayerIndex == TeamMateIndex)
                                                 {
                                                     return r.c1;
                                                 }
                                                 else if (r.player2.PlayerIndex == TeamMateIndex)
                                                 {
                                                     return r.c2;
                                                 }
                                                 else
                                                 {
                                                     return r.c3;
                                                 }
                                             }).ToList()
                                       : new List<Card>();
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
            var teamMatePlayedWinningAXNonTrumpCards = TeamMateIndex != -1
                                                       ? _rounds.Where(r => r != null && r.c3 != null)
                                                                .Select(r =>
                                                                {
                                                                    if (r.player2.PlayerIndex == TeamMateIndex &&
                                                                        r.c2.Suit == r.c1.Suit &&
                                                                        r.c2.Suit != _trump &&
                                                                        r.c2.Value >= Hodnota.Desitka &&
                                                                        r.roundWinner.PlayerIndex != opponent)
                                                                    {
                                                                        return r.c2;
                                                                    }
                                                                    else if (r.player3.PlayerIndex == TeamMateIndex &&
                                                                            r.c3.Suit == r.c1.Suit &&
                                                                            r.c3.Suit != _trump &&
                                                                            r.c3.Value >= Hodnota.Desitka &&
                                                                            r.roundWinner.PlayerIndex != opponent)
                                                                    {
                                                                        return r.c3;
                                                                    }
                                                                    return null;
                                                                })
                                                                .Where(i => i != null).ToList()
                                                       : new List<Card>();
            var teamMatePlayedGreaseCards = TeamMateIndex != -1
                                            ? _rounds.Where(r => r != null && r.c3 != null)
                                                     .Select(r =>
                                                     {
                                                         if (r.player2.PlayerIndex == TeamMateIndex &&
                                                             r.c2.Suit != _trump &&
                                                             r.c2.Suit != r.c1.Suit &&
                                                             r.c2.Value >= Hodnota.Desitka &&
                                                             r.roundWinner.PlayerIndex == MyIndex)
                                                         {
                                                             return r.c2;
                                                         }
                                                         else if (r.player3.PlayerIndex == TeamMateIndex &&
                                                             r.c3.Suit != _trump &&
                                                             r.c3.Suit != r.c1.Suit &&
                                                             r.c3.Value >= Hodnota.Desitka &&
                                                             r.roundWinner.PlayerIndex == MyIndex)
                                                         {
                                                             return r.c3;
                                                         }
                                                         return null;
                                                     })
                                                     .Where(i => i != null).ToList()
                                       : new List<Card>();
            var teamMatesLikelyAXPerSuit = TeamMateIndex != -1
                                           ? Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                 .ToDictionary(b => b,
                                                               b => _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) > 0
                                                                    ? (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Eso)) > _epsilon ? 1 : 0) +
                                                                      (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Desitka)) > _epsilon ? 1 : 0)
                                                                    : int.MaxValue)
                                           : new Dictionary<Barva, int>();
            var topTrumps = hands[MyIndex].Where(i => i.Suit == _trump && holes.All(h => h < i.Value)).ToList();
            var longSuits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hands[MyIndex].CardCount(b) >= 4)
                                .ToList();
            var lowCards = hands[MyIndex].Where(i => opponentCards.Any(j => j.Suit == i.Suit &&
                                                                            j.Value > i.Value))
                                         .ToList();

            var bannedHlasCards = hands[MyIndex].Where(i => _hlasConsidered == HlasConsidered.First &&
                                                            (_gameType & (Hra.Kilo | Hra.KiloProti)) != 0 &&
                                                            i.Suit != _trump &&
                                                            i.Value == Hodnota.Svrsek &&
                                                            hands[MyIndex].HasK(i.Suit) &&
                                                            hands[MyIndex].HasK(_trump) &&
                                                            hands[MyIndex].HasQ(_trump))
                                                .ToList();

            var potentialGreaseCards = TeamMateIndex != -1 &&
                                       hands[MyIndex].CardCount(_trump) <= 1 &&
                                       Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                           .Any(b => b != _trump &&
                                                     (!hands[MyIndex].HasSuit(b) ||
                                                      (hands[MyIndex].CardCount(b) <= 1 &&
                                                       RoundNumber <= 5)) &&
                                                     _probabilities.PotentialCards(opponent).CardCount(b) > 2 - _probabilities.CertainCards(Game.TalonIndex).CardCount(b) &&
                                                     _probabilities.PotentialCards(TeamMateIndex).Any(i => i.Suit == b &&
                                                                                                           i.Value >= Hodnota.Svrsek))
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

            BeforeGetRules(hands);
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
                                hands[MyIndex].HasA(_trump) &&
                                _probabilities.CardProbability(TeamMateIndex == player2 ? player3 : player2, new Card(_trump, Hodnota.Desitka)) >= 1 - _epsilon)
                            {
                                return ValidCards(hands[MyIndex]).OrderByDescending(i => i.Value).FirstOrDefault();
                            }
                            if (TeamMateIndex == -1 &&
                                (_gameType & Hra.SedmaProti) != 0 &&
                                hands[MyIndex].HasA(_trump) &&
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
            else
            {
                yield return new AiRule()
                {
                    Order = 0,
                    Description = "hrát trumfovou hlášku",
                    #region ChooseCard1 Rule00
                    ChooseCard1 = () =>
                    {
                        //pokud se hraje kilo na prvni hlasku a souper muze mit eso kterym mi vytahne bocni hlasku
                        //tak radsi ihned zahraj trumfovou hlasku
                        if (TeamMateIndex == -1 &&
                            _hlasConsidered == HlasConsidered.First &&
                            (_gameType & Hra.Kilo) != 0 &&
                            hands[MyIndex].HasK(_trump) &&
                            hands[MyIndex].HasQ(_trump) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b))
                                .Any(b => hands[MyIndex].HasK(b) &&
                                          hands[MyIndex].HasQ(b) &&
                                          hands[MyIndex].Where(j => j.Suit == b)
                                                        .All(j => j.Value >= Hodnota.Svrsek) &&
                                          (_probabilities.PotentialCards(player2).HasA(b) ||
                                           _probabilities.PotentialCards(player3).HasA(b))))
                        {
                            var cardToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                   i.Value == Hodnota.Svrsek)
                                                                       .FirstOrDefault();

                            return cardToPlay;
                        }

                        return null;
                    }
                    #endregion
                };
            }

            yield return new AiRule()
            {
                Order = 1,
                Description = "hrát nízkou kartu",
                SkipSimulations = true,
                #region ChooseCard1 Rule1
                ChooseCard1 = () =>
                {
                    try
                    {
                        var cardsToPlay = new List<Card>();

                        if (RoundNumber <= 5)
                        {
                            //pokud mas vsechny trumfy vyjma desitky, tak pravidlo nehraj a radsi vytahni trumfovou X
                            if (hands[MyIndex].HasA(_trump) &&
                                _probabilities.PotentialCards(player2).CardCount(_trump) <= 1 &&
                                _probabilities.PotentialCards(player3).CardCount(_trump) <= 1 &&
                                (_probabilities.PotentialCards(player2).HasX(_trump) ||
                                 _probabilities.PotentialCards(player3).HasX(_trump)))
                            {
                                return null;
                            }
                            if (TeamMateIndex == -1 &&
                                (_gameType & Hra.Kilo) == 0 &&
                                myInitialHand.CardCount(_trump) > 0 &&
                                myInitialHand.CardCount(_trump) <= 4 &&
                                unwinnableLowCards.Count > 2)
                            {
                                return null;
                            }
                            if (TeamMateIndex == -1 &&
                                (_gameType & Hra.Sedma) != 0 &&
                                hands[MyIndex].Has7(_trump) &&
                                unwinnableLowCards.Count(i => i.Suit != _trump) >= hands[MyIndex].CardCount(_trump) - 1)
                            {
                                return null;
                            }
                            //pokud nemas v bocnich kartach neodstranitelnou diru, tak pravidlo nehraj
                            if (!Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                     .Where(b => hands[MyIndex].HasSuit(b) &&
                                                 b != _trump)
                                     .Any(b => unwinnableLowCards.HasSuit(b)))
                            {
                                return null;
                            }
                            ///pokud hrajes kilo na 4 nejvyssi trumfy a ve vsech barvach mas A+X a k tomu jednu nizkou
                            if ((_gameType & Hra.Kilo) != 0 &&
                                myInitialHand.HasA(_trump) &&
                                myInitialHand.HasX(_trump) &&
                                myInitialHand.HasK(_trump) &&
                                myInitialHand.HasQ(_trump) &&
                                myInitialHand.CardCount(_trump) == 4 &&
                                myInitialHand.SuitCount() < Game.NumSuits &&
                                myInitialHand.CardCount(Hodnota.Eso) +
                                myInitialHand.Count(i => i.Value == Hodnota.Desitka &&
                                                         myInitialHand.HasA(i.Suit)) >= Math.Min(5, 2 * myInitialHand.SuitCount()) &&
                                unwinnableLowCards.Count == 1 &&
                                unwinnableLowCards.All(i => hands[MyIndex].HasA(i.Suit) &&
                                                            hands[MyIndex].HasX(i.Suit)))
                            {
                                return null;
                            }

                            if (TeamMateIndex != -1 &&
                                hands[MyIndex].CardCount(_trump) < _probabilities.PotentialCards(opponent).CardCount(_trump))
                            {
                                return null;
                            }
                            //pokud ti zbyva jen jedna nizka karta a mas dost vysokych trumfu, tak pravidlo nehraj a radsi vytahni trumfy
                            //nizkou kartu si nech na predposledni stych
                            //if (lowCards.Count == 1 &&
                            //    myInitialHand.CardCount(_trump) >= 5 &&
                            //    topCards.CardCount(_trump) >= holesPerSuit[_trump].Count)
                            //{
                            //    return null;
                            //}
                            if (myInitialHand.CardCount(_trump) >= 4 &&
                                unwinnableLowCards.Any() &&
                                !topCards.Any())
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump)
                                                                        .ToList();
                            }
                            if (myInitialHand.CardCount(_trump) >= 4 &&
                                unwinnableLowCards.Any() &&
                                (topCards.Count >= 4 ||
                                 (topCards.Count > unwinnableLowCards.Count &&
                                  SevenValue >= GameValue) ||
                                 myInitialHand.CardCount(Hodnota.Eso) +
                                 myInitialHand.Count(i => i.Value == Hodnota.Desitka &&
                                                          myInitialHand.HasA(i.Suit)) >= 3) &&
                                (unwinnableLowCards.Count <= 4 ||
                                 (topCards.Count > unwinnableLowCards.Count &&
                                  SevenValue >= GameValue) ||
                                 myInitialHand.CardCount(Hodnota.Eso) +
                                 myInitialHand.Count(i => i.Value == Hodnota.Desitka &&
                                                          myInitialHand.HasA(i.Suit)) >= 3 ||
                                 opponentCards.Where(i => i.Value >= Hodnota.Desitka &&
                                                          myInitialHand.HasSuit(i.Suit))
                                              .Count() <= 3))
                            {
                                if ((_gameType & Hra.Kilo) != 0 && unwinnableLowCards.All(i => i.Value == Hodnota.Desitka))
                                {
                                    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                        i.Value == Hodnota.Desitka &&
                                                                                        hands[MyIndex].CardCount(i.Suit) == 1 &&
                                                                                        hands[MyIndex].CardCount(_trump) > 0 &&
                                                                                        unwinnableLowCards.Contains(i))
                                                                            .ToList();
                                }
                                if (!cardsToPlay.Any() &&
                                    TeamMateIndex == -1 &&
                                    (((_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&    //pri sedme (proti) je lepsi hrat od nejvyssich karet
                                      (_gameType & Hra.Kilo) == 0 &&
                                      (hands[MyIndex].CardCount(_trump) > opponentTrumps.Count ||
                                       hands[MyIndex].CardCount(_trump) <= opponentTrumps.Count &&
                                       hands[MyIndex].SuitCount == Game.NumSuits)) ||
                                    (unwinnableLowCards.Count <= 4 &&
                                     !((_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&    //pri sedme (proti) je lepsi hrat od nejvyssich karet
                                       (_gameType & Hra.Kilo) == 0))))
                                {
                                    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                        i.Value < Hodnota.Desitka &&
                                                                                        ((hands[MyIndex].HasX(i.Suit) &&
                                                                                          (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) > 0 ||
                                                                                           _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > 0)) ||
                                                                                         hands[MyIndex].HasA(i.Suit) ||
                                                                                         (!hands[MyIndex].HasA(i.Suit) &&
                                                                                          !hands[MyIndex].HasX(i.Suit) &&
                                                                                          (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                               .Where(b => b != i.Suit &&
                                                                                                           hands[MyIndex].HasSuit(b))
                                                                                               .All(b => topCards.HasSuit(b)) ||
                                                                                           hands[MyIndex].All(j => j.Suit == _trump ||
                                                                                                                   unwinnableLowCards.Contains(j))))) &&
                                                                                        unwinnableLowCards.Contains(i) &&
                                                                                        !(myInitialHand.HasA(i.Suit) &&
                                                                                          myInitialHand.HasX(i.Suit) &&
                                                                                          //Napr. AX987: AX vytahnou 2 diry, zbyva 1 dira: budu hrat radsi trumf
                                                                                          myInitialHand.CardCount(i.Suit) >= 5 &&
                                                                                          myInitialHand.CardCount(_trump) >= 4))
                                                                            .ToList();
                                }
                                if (!cardsToPlay.Any())
                                {   //je tohle dobre? proc hraju plonkovou desitku pokud muzu hrat i jinou nizkou?
                                    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                        i.Value == Hodnota.Desitka &&
                                                                                        hands[MyIndex].CardCount(i.Suit) == 1 &&
                                                                                        hands[MyIndex].CardCount(_trump) >= 4 &&
                                                                                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                            .Where(b => b != i.Suit)
                                                                                            .All(b => topCards.HasSuit(b)) &&
                                                                                                      unwinnableLowCards.Contains(i))
                                                                            .ToList();
                                }
                                if (!cardsToPlay.Any())
                                {   //zbav se kratke plonkove barvy pokud neni nejaka delsi barva s nizkymi kartami
                                    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                        (!myInitialHand.HasA(i.Suit) ||
                                                                                         !myInitialHand.HasX(i.Suit)) &&
                                                                                        myInitialHand.CardCount(i.Suit) <= 2 &&
                                                                                        unwinnableLowCards.Contains(i) &&
                                                                                        ((_gameType & Hra.Kilo) == 0 ||
                                                                                         !unwinnableLowCards.Any(j => j.Suit != i.Suit &&
                                                                                                                      myInitialHand.CardCount(j.Suit) > myInitialHand.CardCount(i.Suit))))
                                                                            .ToList();
                                }
                            }
                            if (!cardsToPlay.Any() &&
                                TeamMateIndex == -1 &&
                                myInitialHand.CardCount(_trump) >= 4 &&
                                !myInitialHand.HasA(_trump) &&
                                myInitialHand.HasX(_trump) &&
                                unwinnableLowCards.Any(i => i.Suit != _trump) &&
                                (unwinnableLowCards.Count(i => i.Suit != _trump) <= 2 ||
                                 (_gameType & Hra.Kilo) != 0))
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    unwinnableLowCards.Contains(i) &&
                                                                                    (GameValue > SevenValue ||
                                                                                     (GameValue < SevenValue && //pri sedme se nezbavuj barvy ani krale a vyssi
                                                                                      i.Value < Hodnota.Kral &&
                                                                                      hands[MyIndex].CardCount(i.Suit) > 1)))
                                                                        .ToList();
                            }
                            if (!cardsToPlay.Any() &&
                                TeamMateIndex == -1 &&
                                (_gameType & Hra.Kilo) != 0 &&
                                unwinnableLowCards.Any(i => i.Suit != _trump))
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    unwinnableLowCards.Contains(i))
                                                                        .ToList();
                            }
                            if ((_gameType & (Hra.Kilo | Hra.KiloProti)) != 0 &&
                                _hlasConsidered == HlasConsidered.First &&
                                hands[MyIndex].HasK(_trump) &&
                                hands[MyIndex].HasQ(_trump) &&
                                unwinnableLowCards.Any(i => i.Suit != _trump &&
                                                            ((i.Value == Hodnota.Svrsek &&
                                                              hands[MyIndex].HasK(i.Suit)) ||
                                                             (i.Value == Hodnota.Kral &&
                                                              hands[MyIndex].HasQ(i.Suit)))))
                            {
                                //toto zpusobi, ze se zahraje bocni hlaska, ktera se na konci PlayCard() zmeni na pravidlo "Hraj trumfovou hlášku"
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value == Hodnota.Svrsek &&
                                                                                    hands[MyIndex].HasK(i.Suit))
                                                                        .ToList();
                            }
                        }
                        if (cardsToPlay.Any(i => i.Value != Hodnota.Desitka &&
                            //hands[MyIndex].CardCount(i.Suit) == 2 &&
                            hands[MyIndex].HasX(i.Suit) &&
                            !myInitialHand.HasA(i.Suit)))
                        {
                            if (cardsToPlay.Any(i => !myInitialHand.HasA(i.Suit) &&
                                                     !myInitialHand.HasX(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => !myInitialHand.HasA(i.Suit) &&
                                                                     !myInitialHand.HasX(i.Suit))
                                                         .ToList();
                            }
                            else
                            {
                                cardsToPlay = cardsToPlay.Where(i => i.Value != Hodnota.Desitka &&
                                                                     //hands[MyIndex].CardCount(i.Suit) == 2 &&
                                                                     hands[MyIndex].HasX(i.Suit) &&
                                                                     !myInitialHand.HasA(i.Suit))
                                                         .OrderByDescending(i => i.Value)
                                                         .ToList();
                                if (cardsToPlay.Any())
                                {
                                    return cardsToPlay.First();
                                }
                            }
                        }
                        //nejdrive zkusime nizke karty kde mam A a X, tim zustane teoreticka sance,
                        //ze v dalsim stychu budu hrat nizkou kartu kde A,X nemam a souperi si nenamazou nebo si namazou mene
                        else if (cardsToPlay.Any(i => hands[MyIndex].HasA(i.Suit) &&
                                                 hands[MyIndex].HasX(i.Suit)) &&
                            cardsToPlay.Any(i => !myInitialHand.HasA(i.Suit) &&
                                                 !myInitialHand.HasX(i.Suit)))
                        {
                            cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].HasA(i.Suit) &&
                                                                 hands[MyIndex].HasX(i.Suit))
                                                     .ToList();
                        }
                        //potom delam stejnou uvahu pro karty kde mam A ale ne X
                        else if (cardsToPlay.Any(i => hands[MyIndex].HasA(i.Suit)) &&
                                 cardsToPlay.Any(i => !myInitialHand.HasA(i.Suit) &&
                                                      !myInitialHand.HasX(i.Suit)))
                        {
                            cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].HasA(i.Suit))
                                                     .ToList();
                        }

                        if (cardsToPlay.Any(i => i.Value == Hodnota.Desitka &&
                                                 ((TeamMateIndex == -1 &&
                                                   (_probabilities.PotentialCards(player2).HasA(i.Suit) ||
                                                    _probabilities.PotentialCards(player3).HasA(i.Suit))) ||
                                                  (TeamMateIndex != -1 &&
                                                   _probabilities.PotentialCards(opponent).HasA(i.Suit))) &&
                                                 cardsToPlay.Any(j => j.Suit == i.Suit &&
                                                                      j.Value < Hodnota.Desitka)))
                        {
                            cardsToPlay = cardsToPlay.Where(i => !(i.Value == Hodnota.Desitka &&
                                                                   ((TeamMateIndex == -1 &&
                                                                     (_probabilities.PotentialCards(player2).HasA(i.Suit) ||
                                                                      _probabilities.PotentialCards(player3).HasA(i.Suit))) ||
                                                                    (TeamMateIndex != -1 &&
                                                                     _probabilities.PotentialCards(opponent).HasA(i.Suit))) &&
                                                                   cardsToPlay.Any(j => j.Suit == i.Suit &&
                                                                                        j.Value < Hodnota.Desitka)))
                                                    .ToList();
                        }
                        return cardsToPlay//.OrderBy(i => myInitialHand.CardCount(_trump) >= 5
                                          //              ? hands[MyIndex].CardCount(i.Suit)
                                          //              : 0)
                                          .OrderByDescending(i => cardsToPlay.CardCount(i.Suit))
                                          .ThenBy(i => hands[MyIndex].CardCount(i.Suit))
                                          .ThenBy(i => (!myInitialHand.HasA(i.Suit) &&
                                                        hands[MyIndex].HasX(i.Suit)) ||
                                                       (topCards.HasSuit(i.Suit) &&
                                                        !hands[MyIndex].HasA(i.Suit) &&
                                                        !hands[MyIndex].HasX(i.Suit))
                                                       ? -(int)i.Value
                                                       : (int)i.Value)
                                          .FirstOrDefault();
                    }
                    catch (Exception ex)
                    {
                        LogIfNull(hands, nameof(hands));
                        LogIfNull(hands?[0], "hands[0]");
                        LogIfNull(hands?[1], "hands[1]");
                        LogIfNull(hands?[2], "hands[2]");
                        LogIfNull(hands?[3], "hands[3]");
                        _debugString.AppendLine($"Hand1: {hands?[0]}");
                        _debugString.AppendLine($"Hand2: {hands?[1]}");
                        _debugString.AppendLine($"Hand3: {hands?[2]}");
                        _debugString.AppendLine($"Talon: {hands?[3]}");
                        LogIfNull(_probabilities, nameof(_probabilities));
                        LogIfNull(_probabilities?.PotentialCards(0), "_probabilities.PotentialCards(0)");
                        LogIfNull(_probabilities?.PotentialCards(1), "_probabilities.PotentialCards(1)");
                        LogIfNull(_probabilities?.PotentialCards(2), "_probabilities.PotentialCards(2)");
                        LogIfNull(_probabilities?.CertainCards(0), "_probabilities.CertainCards(0)");
                        LogIfNull(_probabilities?.CertainCards(1), "_probabilities.CertainCards(1)");
                        LogIfNull(_probabilities?.CertainCards(2), "_probabilities.CertainCards(2)");
                        _debugString.AppendLine($"PotentialCards1: {new Hand(_probabilities?.PotentialCards(0))}");
                        _debugString.AppendLine($"PotentialCards2: {new Hand(_probabilities?.PotentialCards(1))}");
                        _debugString.AppendLine($"PotentialCards3: {new Hand(_probabilities?.PotentialCards(2))}");
                        _debugString.AppendLine($"CertainCards1: {new Hand(_probabilities?.CertainCards(0))}");
                        _debugString.AppendLine($"CertainCards2: {new Hand(_probabilities?.CertainCards(1))}");
                        _debugString.AppendLine($"CertainCards3: {new Hand(_probabilities?.CertainCards(2))}");
                        LogIfNull(_rounds, nameof(_rounds));
                        LogIfNull(_rounds?[RoundNumber], "_rounds[RoundNumber]");
                        LogIfNull(myInitialHand, nameof(myInitialHand));
                        LogIfNull(myPlayedCards, nameof(myPlayedCards));
                        LogIfNull(topCards, nameof(topCards));
                        LogIfNull(holesPerSuit, nameof(holesPerSuit));
                        LogIfNull(unwinnableLowCards, nameof(unwinnableLowCards));
                        LogIfNull(holes, nameof(holes));
                        LogIfNull(opponentCards, nameof(opponentCards));
                        LogIfNull(opponentTrumps, nameof(opponentTrumps));
                        LogIfNull(opponentLoCards, nameof(opponentLoCards));
                        LogIfNull(teamMatePlayedCards, nameof(teamMatePlayedCards));
                        LogIfNull(teamMatePlayedWinningAXNonTrumpCards, nameof(teamMatePlayedWinningAXNonTrumpCards));
                        LogIfNull(teamMatePlayedGreaseCards, nameof(teamMatePlayedGreaseCards));
                        LogIfNull(teamMatesLikelyAXPerSuit, nameof(teamMatesLikelyAXPerSuit));
                        LogIfNull(topTrumps, nameof(topTrumps));
                        LogIfNull(longSuits, nameof(longSuits));
                        LogIfNull(lowCards, nameof(lowCards));
                        throw;
                    }
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "zkus vytlačit eso",
                SkipSimulations = true,
                #region ChooseCard1 Rule3
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex == -1)
                    {
                        //c--
                        //pokud mas vsechny trumfy vyjma desitky, tak pravidlo nehraj a radsi vytahni trumfovou X
                        if (hands[MyIndex].HasA(_trump) &&
                            _probabilities.PotentialCards(player2).CardCount(_trump) <= 1 &&
                            _probabilities.PotentialCards(player3).CardCount(_trump) <= 1 &&
                            (_probabilities.PotentialCards(player2).HasX(_trump) ||
                             _probabilities.PotentialCards(player3).HasX(_trump)))
                        {
                            return null;
                        }
                        if (SevenValue >= GameValue &&      //pri sedme pokud mas nejvyssi karty jen v trumfech
                            topCards.SuitCount() == 1 &&    //a v kazde dalsi barve mas uhratelnou desitku
                            topCards.HasSuit(_trump) &&     //tak nejdriv vytahni ze souperu trumfy a nezbavuj se tempa
                            topCards.Count >= opponentTrumps.Count &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b))
                                .All(b => hands[MyIndex].HasX(b) &&
                                          hands[MyIndex].CardCount(b) > 1))
                        {
                            return null;
                        }
                        //pokud mas stejne trumfu jako souperi, z toho jeden nejvyssi a k tomu barvu co souperi nemaji
                        //a jeden ze souperu muze mit stejne trumfu jako ty a navic ma nejakou barvu kterou nemas
                        //tak nejdriv vytahni jeden trumf ze soupere a nasledne muzes vytlacit trumf bocni barvou
                        if (GameValue > SevenValue &&
                            topCards.HasSuit(_trump) &&
                            hands[MyIndex].CardCount(_trump) == opponentTrumps.Count &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Any(b => !hands[MyIndex].HasSuit(b) &&
                                          ((_probabilities.PotentialCards(player2).HasSuit(b) &&
                                            _probabilities.PotentialCards(player2).HasSuit(_trump) &&
                                            _probabilities.PotentialCards(player2).CardCount(_trump) == hands[MyIndex].CardCount(_trump) &&
                                            hands[MyIndex].Any(i => !_probabilities.PotentialCards(player2).HasSuit(i.Suit))) ||
                                           (_probabilities.PotentialCards(player3).HasSuit(b) &&
                                            _probabilities.PotentialCards(player3).HasSuit(_trump) &&
                                            _probabilities.PotentialCards(player3).CardCount(_trump) == hands[MyIndex].CardCount(_trump) &&
                                            hands[MyIndex].Any(i => !_probabilities.PotentialCards(player3).HasSuit(i.Suit))))))
                        {
                            return null;
                        }
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

                            ////pokud hrajes sedmu a souperi uz nemaji zadne trumfy a nemas uz zadne eso, tak zkousej primarne vytlacit eso
                            //if (!cardsToPlay.Any() &&
                            //    SevenValue >= GameValue &&
                            //    hands[MyIndex].CardCount(Hodnota.Eso) == 0 &&
                            //    !_probabilities.PotentialCards(player2).HasSuit(_trump) &&
                            //    !_probabilities.PotentialCards(player3).HasSuit(_trump))
                            //{
                            //    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => suits.Contains(i.Suit) &&
                            //                                                        i.Value < Hodnota.Desitka);
                            //}
                            return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                        }
                    }
                    else if (TeamMateIndex == player3)
                    {
                        //c-o
                        if (_probabilities.SuitProbability(player3, _trump, RoundNumber) > 0)
                        {
                            if (//(_gameType & Hra.Kilo) != 0 &&
                            hands[MyIndex].CardCount(_trump) == 1 &&
                            !hands[MyIndex].HasA(_trump) &&
                            !hands[MyIndex].HasX(_trump))
                            {
                                return null;
                            }
                            if (PlayerBids[TeamMateIndex] != 0)
                            {
                                return null;
                            }
                        }
                        //pokud kolega flekoval sedmu a muzes hrat jeho barvu, tak pravidlo nehraj
                        //var opponent = TeamMateIndex == player2 ? player3 : player2;
                        if (ValidCards(hands[MyIndex]).Any(i => i.Suit != _trump &&
                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                _teamMatesSuits.Contains(i.Suit) &&
                                                                (SevenValue >= GameValue &&
                                                                 (PlayerBids[TeamMateIndex] & Hra.Sedma) != 0) &&
                                                                _probabilities.SuitProbability(opponent, _trump, RoundNumber) > 0 &&
                                                                _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) > 0))
                        {
                            return null;
                        }

                        var suits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                        .Where(b => b != _trump &&
                                                    hands[MyIndex].HasX(b) &&
                                                    hands[MyIndex].CardCount(b) > 1 &&
                                                    hands[MyIndex].CardCount(b) < 5 &&
                                                    _probabilities.CardProbability(opponent, new Card(b, Hodnota.Eso)) > _epsilon &&
                                                    (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Eso)) == 0 ||
                                                     _probabilities.PotentialCards(TeamMateIndex).CardCount(b) > 3));
                        if (suits.Any())
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => suits.Contains(i.Suit) &&
                                                                                    i.Value != Hodnota.Desitka &&
                                                                                    i.Value >= Hodnota.Spodek &&
                                                                                    (Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                         .Where(h => h > i.Value)
                                                                                         .Count(h => _probabilities.CardProbability(opponent, new Card(i.Suit, h)) > _epsilon) == 1 ||
                                                                                     hands[MyIndex].CardCount(i.Suit) > 2));

                            return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                        }
                        if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Count(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b) &&
                                            !_bannedSuits.Contains(b)) == 1)
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value != Hodnota.Desitka &&
                                                                                    i.Value >= Hodnota.Spodek &&
                                                                                    hands[MyIndex].HasX(i.Suit) &&
                                                                                    !_bannedSuits.Contains(i.Suit) &&
                                                                                    _probabilities.CardProbability(opponent, new Card(i.Suit, Hodnota.Eso)) > _epsilon);

                            return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                        }
                    }
                    else
                    {
                        //co-
                        if (_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0)
                        {
                            if (//(_gameType & Hra.Kilo) != 0 &&
                                hands[MyIndex].CardCount(_trump) == 1 &&
                                !hands[MyIndex].HasA(_trump) &&
                                !hands[MyIndex].HasX(_trump))
                            {
                                return null;
                            }
                            if (PlayerBids[TeamMateIndex] != 0)
                            {
                                return null;
                            }
                        }
                        //pokud kolega flekoval sedmu a muzes hrat jeho barvu, tak pravidlo nehraj
                        //var opponent = TeamMateIndex == player2 ? player3 : player2;
                        if (ValidCards(hands[MyIndex]).Any(i => i.Suit != _trump &&
                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                _teamMatesSuits.Contains(i.Suit) &&
                                                                (SevenValue >= GameValue &&
                                                                 (PlayerBids[TeamMateIndex] & Hra.Sedma) != 0) &&
                                                                _probabilities.SuitProbability(opponent, _trump, RoundNumber) > 0 &&
                                                                _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) > 0))
                        {
                            return null;
                        }
                        //pokud muzes desitku namazat tak pravidlo nehraj
                        if (_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => hands[MyIndex].HasX(b) &&
                                          _probabilities.CardProbability(player3, new Card(b, Hodnota.Eso)) > 0) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => !hands[MyIndex].HasSuit(b) &&
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
                                                                                    _probabilities.CardProbability(opponent, new Card(i.Suit, Hodnota.Eso)) > _epsilon &&
                                                                                    (_probabilities.CardProbability(opponent, new Card(i.Suit, Hodnota.Eso)) == 0 ||
                                                                                     (_probabilities.PotentialCards(TeamMateIndex).CardCount(i.Suit) > 3 &&
                                                                                      _probabilities.PotentialCards(TeamMateIndex).Any(j => j.Suit == i.Suit &&
                                                                                                                                            j.Value > i.Value &&
                                                                                                                                            j.Value < Hodnota.Desitka))));

                            return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                        }
                        if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Count(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b) &&
                                            !_bannedSuits.Contains(b)) == 1)
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value != Hodnota.Desitka &&
                                                                                    //i.Value >= Hodnota.Spodek &&
                                                                                    hands[MyIndex].HasX(i.Suit) &&
                                                                                    !_bannedSuits.Contains(i.Suit) &&
                                                                                    _probabilities.CardProbability(opponent, new Card(i.Suit, Hodnota.Eso)) > _epsilon &&
                                                                                    _probabilities.PotentialCards(TeamMateIndex).CardCount(i.Suit) > 3 &&
                                                                                    _probabilities.PotentialCards(TeamMateIndex).Any(j => j.Suit == i.Suit &&
                                                                                                                                          j.Value > i.Value &&
                                                                                                                                          j.Value < Hodnota.Desitka));

                            return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                        }
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "zkusit vytáhnout plonkovou X",
                SkipSimulations = true,
                #region ChooseCard1 Rule4
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

                    if (TeamMateIndex == -1)
                    {
                        //c--
                        if (hands[MyIndex].HasA(_trump) &&
                            ((_probabilities.PotentialCards(player2).CardCount(_trump) <= 1 &&
                              _probabilities.PotentialCards(player3).CardCount(_trump) <= 1) ||
                             ((_gameType & Hra.Kilo) != 0 &&
                              _probabilities.PotentialCards(player2).CardCount(_trump) <= 3 &&
                              _probabilities.PotentialCards(player3).CardCount(_trump) <= 3)) &&
                            (_probabilities.PotentialCards(player2).HasX(_trump) ||
                             _probabilities.PotentialCards(player3).HasX(_trump)))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                                i.Suit == _trump).ToList();
                        }
                        else
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                                i.Suit == _trump &&
                                                                                ((_probabilities.PotentialCards(player2).HasSolitaryX(i.Suit) &&
                                                                                  _probabilities.LikelyCards(player2).HasSolitaryX(i.Suit)) ||
                                                                                 (_probabilities.PotentialCards(player3).HasSolitaryX(i.Suit) &&
                                                                                  _probabilities.LikelyCards(player3).HasSolitaryX(i.Suit)))).ToList();
                        }
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //co-
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            (i.Suit == _trump ||
                                                                             hands[MyIndex].Where(j => j.Suit != i.Suit)    //neturmfovou X vytahni jen pokud neni nic lepsiho co hrat
                                                                                           .All(j => j.Suit == _trump ||
                                                                                                     j.Value >= Hodnota.Desitka)) &&
                                                                            (_probabilities.PotentialCards(player3).HasSolitaryX(i.Suit) &&
                                                                             _probabilities.LikelyCards(player3).HasSolitaryX(i.Suit))).ToList();
                    }
                    else
                    {
                        //c-o
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            (i.Suit == _trump ||
                                                                             hands[MyIndex].Where(j => j.Suit != i.Suit)    //neturmfovou X vytahni jen pokud neni nic lepsiho co hrat
                                                                                           .All(j => j.Suit == _trump ||
                                                                                                     j.Value >= Hodnota.Desitka)) &&
                                                                            (_probabilities.PotentialCards(player2).HasSolitaryX(i.Suit) &&
                                                                             _probabilities.LikelyCards(player2).HasSolitaryX(i.Suit))).ToList();
                    }

                    return cardsToPlay.RandomOneOrDefault();
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 4,
                Description = "vytáhnout trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule5
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

                    //pokud ma vic trumfu ale nezna zadnou z tvych netrumfovych karet,
                    //tak pravidlo nehraj. Zkus z nej vytlacit trumfy bocni barvou
                    if (TeamMateIndex != -1 &&
                        topCards.HasSuit(_trump) &&
                        hands[MyIndex].CardCount(_trump) < _probabilities.PotentialCards(opponent).CardCount(_trump) &&
                        hands[MyIndex].CardCount(_trump) >= _probabilities.PotentialCards(opponent).CardCount(_trump) / 2 &&
                        hands[MyIndex].CardCount(_trump) < hands[MyIndex].Count() &&
                        hands[MyIndex].Where(i => i.Suit != _trump)
                                      .All(i => !_probabilities.PotentialCards(opponent).HasSuit(i.Suit)))
                    {
                        return null;
                    }
                    if (TeamMateIndex == -1 &&
                        topCards.HasSuit(_trump) &&
                        hands[MyIndex].CardCount(_trump) < _probabilities.PotentialCards(player2).CardCount(_trump) &&
                        hands[MyIndex].CardCount(_trump) >= _probabilities.PotentialCards(player2).CardCount(_trump) / 2 &&
                        hands[MyIndex].CardCount(_trump) < hands[MyIndex].Count() &&
                        hands[MyIndex].Where(i => i.Suit != _trump)
                                      .All(i => !_probabilities.PotentialCards(player2).HasSuit(i.Suit)))
                    {
                        return null;
                    }
                    if (TeamMateIndex == -1 &&
                        topCards.HasSuit(_trump) &&
                        hands[MyIndex].CardCount(_trump) < _probabilities.PotentialCards(player3).CardCount(_trump) &&
                        hands[MyIndex].CardCount(_trump) >= _probabilities.PotentialCards(player3).CardCount(_trump) / 2 &&
                        hands[MyIndex].CardCount(_trump) < hands[MyIndex].Count() &&
                        hands[MyIndex].Where(i => i.Suit != _trump)
                                      .All(i => !_probabilities.PotentialCards(player3).HasSuit(i.Suit)))
                    {
                        return null;
                    }
                    //pokud mas stejne trumfu jako souperi, z toho jeden nejvyssi a k tomu barvu co souperi nemaji
                    //a jeden ze souperu muze mit stejne trumfu jako ty a navic ma nejakou barvu kterou nemas
                    //tak nejdriv vytahni jeden trumf ze soupere a nasledne muzes vytlacit trumf bocni barvou
                    if (TeamMateIndex == -1 &&
                        GameValue > SevenValue &&
                        topCards.HasSuit(_trump) &&
                        hands[MyIndex].CardCount(_trump) == opponentTrumps.Count &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Any(b => !hands[MyIndex].HasSuit(b) &&
                                      ((_probabilities.PotentialCards(player2).HasSuit(b) &&
                                        _probabilities.PotentialCards(player2).HasSuit(_trump) &&
                                        _probabilities.PotentialCards(player2).CardCount(_trump) == hands[MyIndex].CardCount(_trump) &&
                                        hands[MyIndex].Any(i => !_probabilities.PotentialCards(player2).HasSuit(i.Suit))) ||
                                       (_probabilities.PotentialCards(player3).HasSuit(b) &&
                                        _probabilities.PotentialCards(player3).HasSuit(_trump) &&
                                        _probabilities.PotentialCards(player3).CardCount(_trump) == hands[MyIndex].CardCount(_trump) &&
                                        hands[MyIndex].Any(i => !_probabilities.PotentialCards(player3).HasSuit(i.Suit))))))
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => topTrumps.Contains(i))
                                                                .ToList();
                    }
                    //pokud mas dost trumfu na ruce a zadnou dloubou bocni barvu,
                    //tak zkus nejdriv vytahnout trumfy ze souperu abys neohrozil vlastni bodovane karty
                    if (!cardsToPlay.Any() &&
                        GameValue > SevenValue &&
                        topCards.Any(i => i.Suit != _trump &&
                                          i.Value >= Hodnota.Desitka) &&
                        topTrumps.Count >= 2 &&
                        opponentTrumps.Any() &&
                        !hands[MyIndex].Has7(_trump) &&
                        hands[MyIndex].CardCount(_trump) > opponentTrumps.Count &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump &&
                                        hands[MyIndex].HasSuit(b))
                            .All(b => hands[MyIndex].CardCount(b) < 4 &&
                                      ((hands[MyIndex].HasA(b) ||
                                        hands[MyIndex].HasX(b)))) &&
                        !hands[MyIndex].Any(i => i.Suit != _trump &&    //nehraj pravidlo pokud muzes tlacit trumf ze soupere bocni barvou
                                                 i.Value < Hodnota.Desitka &&
                                                 ((!_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                   _probabilities.PotentialCards(player2).HasSuit(_trump)) ||
                                                  (!_probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                   _probabilities.PotentialCards(player3).HasSuit(_trump)))))
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => topTrumps.Contains(i))
                                                                .ToList();
                    }
                    //pokud mas min trumfu nez souperi, z toho jeden nejvyssi a k tomu barvu co souperi nemaji
                    //tak nejdriv vytahni trumfy ze souperu a nasledne muzes vytlacit trumf bocni barvou
                    if (!cardsToPlay.Any() &&
                        TeamMateIndex == -1 &&
                        topTrumps.Any() &&
                        hands[MyIndex].CardCount(_trump) >= 2 &&
                        opponentTrumps.Count >= hands[MyIndex].CardCount(_trump) &&
                        opponentTrumps.Count <= 2 * hands[MyIndex].CardCount(_trump) &&
                        _probabilities.PotentialCards(player2).HasSuit(_trump) &&
                        _probabilities.PotentialCards(player3).HasSuit(_trump) &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump &&
                                        hands[MyIndex].HasSuit(b))
                            .Any(b => !_probabilities.PotentialCards(player2).HasSuit(b) &&
                                      !_probabilities.PotentialCards(player3).HasSuit(b)))
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => topTrumps.Contains(i))
                                                                .ToList();
                    }
                    //if (!cardsToPlay.Any() &&
                    //    TeamMateIndex != -1 &&
                    //    (SevenValue >= GameValue ||
                    //     topCards.All(i => i.Suit == _trump)) &&
                    //    topTrumps.Any() &&
                    //    opponentTrumps.Any() &&
                    //    hands[MyIndex].CardCount(_trump) >= opponentTrumps.Count &&//4 &&
                    //    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    //        .Where(b => b != _trump &&
                    //                    hands[MyIndex].HasSuit(b))
                    //        .All(b => hands[MyIndex].CardCount(b) < 4 ||
                    //                  !topCards.HasSuit(b)))
                    //{
                    //    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => topTrumps.Contains(i))
                    //                                            .ToList();
                    //}

                    if (cardsToPlay.Any())
                    {
                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }
                    if (TeamMateIndex == -1)
                    {
                        //c--
                        if (_probabilities.SuitProbability(player2, _trump, RoundNumber) == 0 &&
                            _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0)
                        {
                            return null;
                        }
                        if (hands[MyIndex].Any(i => i.Value == Hodnota.Desitka &&
                                                    myInitialHand.CardCount(i.Suit) == 1) &&
                            !(unwinnableLowCards.Count == 1 &&
                              topTrumps.Count >= opponentCards.CardCount(_trump)) &&
                            !((_gameType & Hra.Kilo) != 0 &&
                               hands[MyIndex].HasA(_trump) &&
                               myInitialHand.HasX(_trump) &&
                               myInitialHand.HasK(_trump) &&
                               myInitialHand.HasQ(_trump) &&
                               myInitialHand.CardCount(_trump) == 4 &&
                               Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                   .Any(b => b != _trump &&
                                             hands[MyIndex].HasA(b) &&
                                             _probabilities.SuitProbability(player2, b, RoundNumber) > 0 &&
                                             _probabilities.SuitProbability(player3, b, RoundNumber) > 0)))
                        {
                            return null;
                        }
                        //pri sedme pokud zbyva malo trumfu a muzes vytlacit trumf necim jinym tak pravidlo nehraj
                        if ((_gameType & Hra.Sedma) != 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            hands[MyIndex].CardCount(_trump) == 2 &&
                            unwinnableLowCards.Any() &&
                            hands[MyIndex].Any(i => i.Suit != _trump &&
                                                    ((!_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                      _probabilities.PotentialCards(player2).HasSuit(_trump)) ||
                                                     (!_probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                      _probabilities.PotentialCards(player3).HasSuit(_trump)))))
                        {
                            return null;
                        }
                        if ((_gameType & Hra.Sedma) != 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            hands[MyIndex].CardCount(_trump) <= unwinnableLowCards.Where(i => i.Suit != _trump).Count() + 1 &&
                            !(topTrumps.Any() &&
                              opponentTrumps.Count <= 2 * topTrumps.Count &&
                              _probabilities.PotentialCards(player2).HasSuit(_trump) &&
                              _probabilities.PotentialCards(player3).HasSuit(_trump) &&
                              topCards.SuitCount() == 1 &&
                              hands[MyIndex].Any(i => i.Suit != _trump &&
                                                      i.Value == Hodnota.Desitka)) &&
                            !(hands[MyIndex].CardCount(_trump) == 3 &&
                              opponentTrumps.Count == 2 &&
                              hands[MyIndex].SuitCount == Game.NumSuits))
                        {
                            return null;
                        }

                        if (SevenValue >= GameValue &&
                            //(_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&
                            //(_gameType & (Hra.Kilo | Hra.KiloProti)) == 0 &&
                            myInitialHand.CardCount(_trump) <= 5 &&
                            topTrumps.Any() &&
                            !(myInitialHand.CardCount(_trump) <= 3 &&
                              topCards.SuitCount() == Game.NumSuits) &&
                            opponentCards.HasSuit(_trump) &&
                            (!hands[MyIndex].Has7(_trump) ||
                             hands[MyIndex].CardCount(_trump) >= (hands[MyIndex].SuitCount == Game.NumSuits ? 2 : 3)) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b))
                                .All(b => hands[MyIndex].CardCount(b) <= 3))
                        {
                            return ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump)
                                                             .OrderByDescending(i => i.Value)
                                                             .FirstOrDefault();
                        }

                        //pokud mas 4 nevyssi trumfy a ve vsech barvach nejvyssi karty, tak nejdriv vytahni trumfy ze souperu a potom muzes hrat odshora
                        if ((_gameType & Hra.Kilo) != 0 &&
                            hands[MyIndex].HasA(_trump) &&
                            myInitialHand.HasX(_trump) &&
                            myInitialHand.HasK(_trump) &&
                            myInitialHand.HasQ(_trump) &&
                            myInitialHand.CardCount(_trump) == 4 &&
                            hands[MyIndex].CardCount(Hodnota.Eso) +
                            hands[MyIndex].Count(i => i.Value == Hodnota.Desitka &&
                                                       myInitialHand.HasA(i.Suit)) >= 5)
                        //Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        //    .Where(b => hands[MyIndex].HasSuit(b))
                        //    .All(b => topCards.Any(i => i.Suit == b)))
                        {
                            return ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump)
                                                             .OrderByDescending(i => i.Value)
                                                             .FirstOrDefault();
                        }

                        if ((topTrumps.Count >= holes.Count ||
                             (myInitialHand.HasA(_trump) &&
                              myInitialHand.HasX(_trump) &&
                              hands[MyIndex].HasK(_trump) &&
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
                              hands[MyIndex].CardCount(_trump) > topTrumps.Count) ||
                             (SevenValue >= GameValue &&
                              !longSuits.Any())))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => topTrumps.Contains(i)).ToList();

                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.OrderBy(i => i.Value).First();
                            }
                        }
                        //a vsechny cizi trumfy ma souper, tak pravidlo nehraj
                        //hrozi, ze bych na konci nemel dost trumfu
                        if ((_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                         .Where(b => b != _trump)
                                                         .Any(b => myInitialHand.CardCount(b) >= 4 &&
                                                                   _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0))
                        {
                            return null;
                        }
                        if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => hands[MyIndex].HasSuit(b) &&
                                          !hands[MyIndex].HasA(b) &&
                                          !hands[MyIndex].HasX(b)))
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
                                 hands[MyIndex].CardCount(_trump) > opponentCards.CardCount(_trump) + 1 &&
                                 _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 &&
                                 _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0) ||
                                ((_gameType & Hra.Sedma) == 0 &&
                                 hands[MyIndex].CardCount(_trump) >= 4) &&
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
                              hands[MyIndex].CardCount(_trump) > opponentCards.CardCount(_trump) &&
                              unwinnableLowCards.Count < hands[MyIndex].CardCount(_trump)))))
                        {
                            //nehraj trumfove eso, pokud maji souperi desitku ktera neni plonkova. Souperi by si mohli pozdeji zbytecne mazat.
                            if (topTrumps.Any(i => i.Value == Hodnota.Eso) &&
                                (_probabilities.CardProbability(player2, new Card(_trump, Hodnota.Desitka)) > 0 ||
                                 _probabilities.CardProbability(player3, new Card(_trump, Hodnota.Desitka)) > 0) &&
                                ((hands[MyIndex].HasK(_trump) &&
                                  (_probabilities.PotentialCards(player2).CardCount(_trump) >= 2 ||
                                   _probabilities.PotentialCards(player3).CardCount(_trump) >= 2)) ||
                                  (_probabilities.HasSolitaryX(player2, _trump, RoundNumber) < SolitaryXThreshold &&
                                   _probabilities.HasSolitaryX(player3, _trump, RoundNumber) < SolitaryXThreshold)))
                            {
                                return null;
                            }
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => topTrumps.Contains(i)).ToList();
                        }

                        return cardsToPlay.RandomOneOrDefault();
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //co-
                        if ((_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            hands[MyIndex].CardCount(_trump) == 2)
                        {
                            return null;
                        }
                        if ((PlayerBids[TeamMateIndex] & (Hra.Sedma | Hra.SedmaProti)) != 0)
                        {
                            return null;
                        }
                        if (_probabilities.SuitProbability(player3, _trump, RoundNumber) == 0)
                        {
                            return null;
                        }
                        if (hands[MyIndex].CardCount(_trump) <= opponentTrumps.Count &&
                            topCards.Where(i => i.Suit != _trump)
                                    .Any(i => i.Value < Hodnota.Desitka &&
                                              !_probabilities.PotentialCards(player2).HasA(i.Suit) &&
                                              !_probabilities.PotentialCards(player2).HasX(i.Suit) &&
                                              !_probabilities.PotentialCards(player3).HasA(i.Suit) &&
                                              !_probabilities.PotentialCards(player3).HasX(i.Suit)))
                        {
                            return null;
                        }
                        //pouzivam 0 misto epsilon protoze jinak bych mohl hrat trumfovou x a myslet si ze souper nema eso a on by ho zrovna mel!
                        //var holes = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().Where(h => _probabilities.CardProbability(player3, new Card(_trump, h)) > (h == Hodnota.Eso ? 0 : _epsilon)).ToList();
                        //var topTrumps = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump && holes.All(h => h < i.Value)).ToList();
                        //var lowCards = hands[MyIndex].Where(i => i.Suit != _trump &&
                        //                                         Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                        //                                             .Any(h => h > i.Value &&
                        //                                                       _probabilities.CardProbability(player3, new Card(i.Suit, h)) > _epsilon)).ToList();

                        if ((_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            hands[MyIndex].CardCount(_trump) <= unwinnableLowCards.Where(i => i.Suit != _trump).Count())
                        {
                            return null;
                        }

                        if (unwinnableLowCards.Any() &&
                            hands[MyIndex].Where(i => i.Suit != _trump)
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
                            hands[MyIndex].Has7(_trump) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => myInitialHand.CardCount(b) >= 4 &&
                                          _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0))
                        {
                            return null;
                        }
                        if (SevenValue >= GameValue &&
                            (_gameType & Hra.Sedma) != 0 &&
                            hands[MyIndex].SuitCount == Game.NumSuits &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .All(b => hands[MyIndex].CardCount(b) <= 4 &&
                                          !topCards.HasSuit(b)) &&
                            topTrumps.Any())
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => topTrumps.Contains(i)).ToList();
                        }
                        if (!cardsToPlay.Any() &&
                            holes.Count > 0 &&
                            topTrumps.Count > 0 &&
                            (topTrumps.Count >= holes.Count ||
                             //pokud akter hraje sedmu a ma posledni dva nebo tri trumfy
                             //a jak mam trumfove eso nebo eso a desitku a jeste neco jineho co akter nezna
                             //a akter ma barvu kterou neznam ja, tak nejdriv z nej vytahni jeden trumf a nasledne vytlac sedmu
                             ((_gameType & Hra.Sedma) != 0 &&
                              SevenValue >= GameValue &&
                              _probabilities.PotentialCards(player3).CardCount(_trump) <= topTrumps.Count + 1 &&
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Any(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b) &&
                                            _probabilities.SuitProbability(player3, b, RoundNumber) == 0)// &&
                                                                                                         //Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                                         //    .Any(b => b != _trump &&
                                                                                                         //              !hands[MyIndex].HasSuit(b) &&
                                                                                                         //              _probabilities.PotentialCards(player3).CardCount(b) > 1)
                                      ) ||
                             (hands[MyIndex].CardCount(_trump) >= holes.Count &&
                              _probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor &&
                              //pokud ve vsech netrumfovych barvach mam nejvyssi kartu
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => b != _trump &&
                                              hands[MyIndex].HasSuit(b))
                                  .All(b => topCards.HasSuit(b)) &&
                             unwinnableLowCards.Count < hands[MyIndex].CardCount(_trump))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => topTrumps.Contains(i)).ToList();
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
                        if ((PlayerBids[TeamMateIndex] & (Hra.Sedma | Hra.SedmaProti)) != 0)
                        {
                            return null;
                        }
                        if (_probabilities.SuitProbability(player2, _trump, RoundNumber) == 0)
                        {
                            return null;
                        }
                        if (hands[MyIndex].CardCount(_trump) <= opponentTrumps.Count &&
                            topCards.Where(i => i.Suit != _trump)
                                    .All(i => i.Value < Hodnota.Desitka &&
                                              !_probabilities.PotentialCards(player2).HasA(i.Suit) &&
                                              !_probabilities.PotentialCards(player2).HasX(i.Suit) &&
                                              !_probabilities.PotentialCards(player3).HasA(i.Suit) &&
                                              !_probabilities.PotentialCards(player3).HasX(i.Suit)))
                        {
                            return null;
                        }

                        if ((_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            hands[MyIndex].CardCount(_trump) <= unwinnableLowCards.Where(i => i.Suit != _trump).Count())
                        {
                            return null;
                        }

                        if (unwinnableLowCards.Any() &&
                            hands[MyIndex].Where(i => i.Suit != _trump)
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
                            hands[MyIndex].Has7(_trump) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => myInitialHand.CardCount(b) >= 4 &&
                                          _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0))
                        {
                            return null;
                        }
                        if (SevenValue >= GameValue &&
                            (_gameType & Hra.Sedma) != 0 &&
                            hands[MyIndex].SuitCount == Game.NumSuits &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .All(b => hands[MyIndex].CardCount(b) <= 4 &&
                                          !topCards.HasSuit(b)) &&
                            topTrumps.Any())
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => topTrumps.Contains(i)).ToList();
                        }
                        if (!cardsToPlay.Any() &&
                            topTrumps.Count > 0 &&
                            (topTrumps.Count >= holes.Count ||
                             //pokud akter hraje sedmu a ma posledni dva nebo tri trumfy
                             //a jak mam trumfove eso nebo eso a desitku a jeste neco jineho co akter nezna
                             //a akter ma barvu kterou neznam ja, tak nejdriv z nej vytahni jeden trumf a nasledne vytlac sedmu
                             ((_gameType & Hra.Sedma) != 0 &&
                              SevenValue >= GameValue &&
                              _probabilities.PotentialCards(player2).CardCount(_trump) <= topTrumps.Count + 1 &&
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Any(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b) &&
                                            _probabilities.SuitProbability(player2, b, RoundNumber) == 0)// &&
                                                                                                         //Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                                         //    .Any(b => b != _trump &&
                                                                                                         //              !hands[MyIndex].HasSuit(b) &&
                                                                                                         //              _probabilities.SuitProbability(player2, b, RoundNumber) >= 1 - RiskFactor)
                                      ) ||
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
                             unwinnableLowCards.Count < hands[MyIndex].CardCount(_trump)))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => topTrumps.Contains(i)).ToList();
                        }

                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 5,
                Description = "vytlačit trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule6
                ChooseCard1 = () =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == -1 &&
                        (_gameType & Hra.Sedma) != 0 &&
                        hands[MyIndex].Has7(_trump) &&
                        hands[MyIndex].CardCount(_trump) <= unwinnableLowCards.Where(i => i.Suit != _trump).Count() + 1 &&
                        !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                             .Where(b => b != _trump)
                             .All(b => myInitialHand.CardCount(b) <= 3))
                    {
                        return null;
                    }
                    if (TeamMateIndex == -1 &&
                        (_gameType & Hra.Sedma) != 0 &&
                        (_gameType & Hra.Kilo) == 0 &&
                        hands[MyIndex].Has7(_trump) &&
                        hands[MyIndex].CardCount(_trump) <= unwinnableLowCards.Count + 1 &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump)
                            .Any(b => topCards.HasSuit(b)) &&
                        !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                             .Where(b => b != _trump)
                             .All(b => myInitialHand.CardCount(b) <= 4))
                    {
                        return null;
                    }
                    if (TeamMateIndex != -1 &&
                        //(_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&
                        SevenValue > GameValue &&
                        (PlayerBids[TeamMateIndex] & (Hra.Sedma | Hra.SedmaProti)) != 0 &&    //pokud kolega flekoval sedmu tak pravidlo nehraj
                        !Enum.GetValues(typeof(Barva)).Cast<Barva>()    //to neplati pokud vim o barve, kterou muzu z aktera tlacit trumf
                             .Where(b => b != _trump)                   //a nehrozi, ze kolega tu barvu nebude mit
                             .Any(b => hands[MyIndex].HasSuit(b) &&
                                       !_probabilities.PotentialCards(opponent).HasSuit(b) &&
                                       _probabilities.PotentialCards(TeamMateIndex).HasSuit(b)))
                    {
                        return null;
                    }
                    if (hands[MyIndex].Any(i => i.Value == Hodnota.Desitka &&   //mas-li plonkovou X tak pravidlo nehraj
                                                myInitialHand.CardCount(i.Suit) == 1) &&
                        !((_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&    //neplati pri sedme pokud mas dost trumfu
                          myInitialHand.Has7(_trump) &&                         //a netrumfy souperi maji a nejde jima tlacit trumf
                          (myInitialHand.CardCount(_trump) >= 6 ||              //tak zkus vytlacit trumf trumfem
                           (myInitialHand.CardCount(_trump) >= 5 &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            myInitialHand.HasSuit(b))
                                .All(b => myInitialHand.CardCount(b) <= 3))) &&
                          (_probabilities.PotentialCards(player2)
                                         .HasSuit(_trump) ||
                           _probabilities.PotentialCards(player3)
                                         .HasSuit(_trump)) &&
                          hands[MyIndex].CardCount(_trump) >
                          _probabilities.PotentialCards(player2).Union(_probabilities.PotentialCards(player3))
                                        .Distinct()
                                        .CardCount(_trump) + 1 &&
                          (TeamMateIndex != -1 ||
                            hands[MyIndex].All(i => i.Suit == _trump ||
                                                    (i.Value == Hodnota.Desitka &&
                                                     hands[MyIndex].CardCount(i.Suit) == 1) ||
                                                    (_probabilities.CertainCards(player2).HasSuit(i.Suit) &&
                                                     !_probabilities.PotentialCards(player3).HasSuit(_trump)) ||
                                                    (_probabilities.CertainCards(player3).HasSuit(i.Suit) &&
                                                     !_probabilities.PotentialCards(player2).HasSuit(_trump))))) &&
                        !((_gameType & Hra.Kilo) != 0 &&
                           hands[MyIndex].HasA(_trump) &&
                           myInitialHand.HasX(_trump) &&
                           myInitialHand.HasK(_trump) &&
                           myInitialHand.HasQ(_trump) &&
                           myInitialHand.CardCount(_trump) == 4 &&
                           Enum.GetValues(typeof(Barva)).Cast<Barva>()
                               .Any(b => b != _trump &&
                                         hands[MyIndex].HasA(b) &&
                                         _probabilities.SuitProbability(player2, b, RoundNumber) > 0 &&
                                         _probabilities.SuitProbability(player3, b, RoundNumber) > 0)) &&
                        !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                             .Where(b => b != _trump &&
                                         hands[MyIndex].HasSuit(b))
                             .All(b => hands[MyIndex].CardCount(b) < 4 &&
                                       ((hands[MyIndex].HasA(b) ||
                                         hands[MyIndex].HasX(b)))))
                    {
                        return null;
                    }

                    //proti kilu zkus vytlacit netrumfovou hlasku pokud se hraje kilo na prvni hlas
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
                            .Any(b => hands[MyIndex].Any(i => i.Suit == b &&
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
                    if (TeamMateIndex != -1 &&
                        (_gameType & Hra.Kilo) != 0 &&
                        hands[MyIndex].CardCount(_trump) <= 2 &&
                        hands[MyIndex].CardCount(_trump) < _probabilities.PotentialCards(opponent).CardCount(_trump) &&
                        hands[MyIndex].Any(i => i.Suit != _trump &&
                                                 i.Value >= Hodnota.Desitka) &&
                        _probabilities.SuitProbability(opponent, _trump, RoundNumber) >= 1 - RiskFactor)
                    {
                        //pokud hrajes proti kilu a mas malo trumfu a k tomu co namazat tak pravidlo nehraj
                        return null;
                    }
                    //pokud mas dost trumfu na ruce a zadnou dloubou bocni barvu,
                    //tak zkus nejdriv vytlacit trumfy ze souperu abys neohrozil vlastni bodovane karty
                    if (GameValue > SevenValue &&
                        topCards.Any(i => i.Suit != _trump &&
                                          i.Value >= Hodnota.Desitka) &&
                        opponentTrumps.Count > 0 &&
                        (//!hands[MyIndex].Has7(_trump) &&
                         hands[MyIndex].CardCount(_trump) > opponentTrumps.Count ||
                         ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 &&
                          hands[MyIndex].CardCount(_trump) >= opponentTrumps.Count)) &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump &&
                                        hands[MyIndex].HasSuit(b))
                            .All(b => hands[MyIndex].CardCount(b) < 4 &&
                                      ((hands[MyIndex].HasA(b) ||
                                        hands[MyIndex].HasX(b)))) &&
                        !hands[MyIndex].Any(i => i.Suit != _trump &&    //nehraj pravidlo pokud muzes tlacit trumf ze soupere bocni barvou
                                                 i.Value < Hodnota.Desitka &&
                                                 ((!_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                   _probabilities.PotentialCards(player2).HasSuit(_trump)) ||
                                                  (!_probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                   _probabilities.PotentialCards(player3).HasSuit(_trump)))))
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                            i.Value < Hodnota.Desitka);
                    }

                    //pokud proti sedme mas same trumfy vyjma jedne karty, tak se zbav posledni netrumfove karty
                    if (!cardsToPlay.Any() &&
                        SevenValue >= GameValue &&
                        hands[MyIndex].CardCount(_trump) == Game.NumRounds - RoundNumber)
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump);
                    }
                    //pokud proti sedme nemas dlouhou bocni barvu a mas aspon tri trumfy na zacatku
                    //a mas nejvyssi trumf nebo neni barva kterou akter urcite nezna
                    //tak vyraz z aktera trumf trumfem
                    if (!cardsToPlay.Any() &&
                        SevenValue >= GameValue &&
                        TeamMateIndex != -1 &&
                        hands[MyIndex].CardCount(_trump) > opponentTrumps.Count &&
                        hands[MyIndex].CardCount(_trump) > unwinnableLowCards.Count &&
                        //myInitialHand.CardCount(_trump) >= 3 &&
                        hands[MyIndex].HasSuit(_trump) &&
                        _probabilities.PotentialCards(opponent).HasSuit(_trump) &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .All(b => myInitialHand.CardCount(b) <= 4 &&
                                      (!hands[MyIndex].HasSuit(b) ||
                                       _probabilities.PotentialCards(opponent).HasSuit(b))))  //jen kdyz neni barva kterou souper urcite nezna
                    {
                        if (topCards.HasSuit(_trump))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                topCards.Contains(i));
                        }
                        else if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                     .Where(b => hands[MyIndex].HasSuit(b))
                                     .All(b => _probabilities.PotentialCards(opponent).HasSuit(b)) ||
                                 (_probabilities.PotentialCards(opponent).CardCount(_trump) == 2 &&
                                  Enum.GetValues(typeof(Barva)).Cast<Barva>()   //predposledniho trumfa vyrazim ted a posledniho potom bocni barvou
                                      .Any(b => !hands[MyIndex].HasSuit(b) &&   //pokud by akter mohl jinak meho trumfa vytlacit necim co nemam
                                                _probabilities.PotentialCards(opponent).HasSuit(b))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump)
                                                                    .OrderBy(i => i.Value)
                                                                    .Take(1);
                        }
                    }
                    //pokud mam pri kilu diry jen v trumfove barve, tak se je snaz vytlacit a nehraj bocni barvu na kterou lze mazat
                    if (!cardsToPlay.Any() &&
                        (_gameType & (Hra.Kilo | Hra.KiloProti)) != 0 &&
                        topCards.Any(i => i.Suit != _trump &&
                                          i.Value >= Hodnota.Desitka) &&
                        opponentTrumps.Count > 0 &&
                        hands[MyIndex].CardCount(_trump) > opponentTrumps.Count &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump &&
                                        hands[MyIndex].HasSuit(b))
                            .All(b => holesPerSuit[b].Count == 0))
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                            i.Value < Hodnota.Desitka);
                    }
                    if (TeamMateIndex == -1)
                    {
                        //: c--
                        //pri sedme zkousej nejprve vytlacit trumf dllouhou bocni barvou
                        if (!cardsToPlay.Any() &&
                            SevenValue >= GameValue &&
                            //(_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&
                            //(_gameType & (Hra.Kilo | Hra.KiloProti)) == 0 &&
                            myInitialHand.CardCount(_trump) <= 5 &&
                            hands[MyIndex].Count(i => i.Suit != _trump &&
                                                      i.Value >= Hodnota.Kral) >= 1 &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => myInitialHand.CardCount(b) + hands[Game.TalonIndex].CardCount(b) >= 4 &&
                                          myInitialHand.HasA(b)))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                i.Value < Hodnota.Desitka &&
                                                                                myInitialHand.CardCount(i.Suit) +
                                                                                hands[Game.TalonIndex].CardCount(i.Suit) >= 4 &&
                                                                                myInitialHand.HasA(i.Suit) &&
                                                                                !hands[MyIndex].HasA(i.Suit) &&
                                                                                !((!_probabilities.PotentialCards(player2).HasSuit(_trump) &&
                                                                                   !_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                                                   _probabilities.CertainCards(player3).HasSuit(i.Suit)) ||
                                                                                  (!_probabilities.PotentialCards(player3).HasSuit(_trump) &&
                                                                                   !_probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                                                   _probabilities.CertainCards(player2).HasSuit(i.Suit))));

                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.OrderByDescending(i => myInitialHand.CardCount(i.Suit))
                                                  .ThenByDescending(i => i.Value)
                                                  .FirstOrDefault();
                            }
                        }
                        if (opponentTrumps.Count == 0)
                        {
                            return null;
                        }
                        //pokud mas dost trumfu na ruce a zadnou dloubou bocni barvu,
                        //tak zkus nejdriv vytlacit trumfy ze souperu abys neohrozil vlastni bodovane karty
                        if (!cardsToPlay.Any() &&
                            SevenValue >= GameValue &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps.Count + 1 &&
                            (_probabilities.PotentialCards(player2).HasA(_trump) ||
                             _probabilities.PotentialCards(player3).HasA(_trump) ||
                             _probabilities.PotentialCards(player2).HasX(_trump) ||
                             _probabilities.PotentialCards(player3).HasX(_trump)) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .All(b => hands[MyIndex].CardCount(b) < 4 ||
                                            (hands[MyIndex].CardCount(b) == 4 &&
                                             !hands[MyIndex].HasA(b))) &&
                            !hands[MyIndex].Any(i => i.Suit != _trump &&    //nehraj pravidlo pokud muzes tlacit trumf ze soupere bocni barvou
                                                     i.Value < Hodnota.Desitka &&
                                                     ((!_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                       _probabilities.PotentialCards(player2).HasSuit(_trump)) ||
                                                      (!_probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                       _probabilities.PotentialCards(player3).HasSuit(_trump)))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                i.Value < Hodnota.Desitka);
                        }
                        if (!cardsToPlay.Any() &&
                            (_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&
                            hands[MyIndex].CardCount(_trump) <= opponentTrumps.Count &&
                            !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Where(b => b != _trump)
                                 .Any(b => myInitialHand.CardCount(b) >= 4 ||
                                           (topCards.HasSuit(b) &&
                                            hands[MyIndex].CardCount(b) >= 2 &&
                                            (_probabilities.PotentialCards(player2).HasSuit(b) ||
                                             !_probabilities.PotentialCards(player2).HasSuit(_trump)) &&
                                            (_probabilities.PotentialCards(player3).HasSuit(b) ||
                                             !_probabilities.PotentialCards(player3).HasSuit(_trump)))) &&
                            !hands[MyIndex].Any(i => i.Suit != _trump &&    //nehraj pravidlo pokud muzes tlacit trumf ze soupere bocni barvou
                                                     i.Value < Hodnota.Desitka &&
                                                     ((!_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                       _probabilities.PotentialCards(player2).HasSuit(_trump)) ||
                                                      (!_probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                       _probabilities.PotentialCards(player3).HasSuit(_trump)))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                i.Value < Hodnota.Desitka &&
                                                                                !(!myInitialHand.Has7(_trump) &&    //pokud se sedmou uz nejde nic delat, tak si nenechavej na ruce polonkovou X
                                                                                  _hands[MyIndex].CardCount(_trump) == 2 &&
                                                                                  _hands[MyIndex].HasX(_trump) &&
                                                                                  opponentTrumps.HasA(_trump) &&
                                                                                  opponentTrumps.Count > 2));
                        }
                        //pokud mas dost trumfu a na ruce jen trumfy a nejvyssi karty, tak vytlac nejprve trumfy ze souperu a
                        //vysoke karty setri na pozdeji
                        if (!cardsToPlay.Any() &&
                            hands[MyIndex].CardCount(_trump) >= opponentTrumps.Count &&
                            (!Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => hands[MyIndex].HasSuit(b) &&
                                              b != _trump)
                                  .Any(b => unwinnableLowCards.HasSuit(b))) &&
                            !hands[MyIndex].Any(i => i.Suit != _trump &&    //nehraj pravidlo pokud muzes tlacit trumf ze soupere bocni barvou
                                                     i.Value < Hodnota.Desitka &&
                                                     ((!_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                       _probabilities.PotentialCards(player2).HasSuit(_trump)) ||
                                                      (!_probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                       _probabilities.PotentialCards(player3).HasSuit(_trump)))) &&
                            ((SevenValue < GameValue &&
                              (_gameType & Hra.Kilo) == 0 &&
                              (hands[MyIndex].CardCount(_trump) > opponentTrumps.Count ||
                               (hands[MyIndex].CardCount(_trump) == opponentTrumps.Count &&
                                _probabilities.PotentialCards(player2).HasSuit(_trump) &&
                                _probabilities.PotentialCards(player3).HasSuit(_trump)))) ||
                             hands[MyIndex].CardCount(_trump) > opponentTrumps.Count + 1 ||
                             ((_gameType & Hra.Kilo) != 0 &&
                              !(hands[MyIndex].CardCount(_trump) == opponentTrumps.Count &&
                                topCards.CardCount(_trump) < opponentTrumps.Count &&
                                hands[MyIndex].SuitCount < Game.NumSuits))))
                        {
                            if (topCards.HasSuit(_trump))
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump);
                            }
                            else if ((_gameType & Hra.Kilo) != 0 ||
                                     !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                          .Where(b => b != _trump)
                                          .Any(b => hands[MyIndex].CardCount(b) >= 5))
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                    i.Value < Hodnota.Desitka);
                            }
                        }
                        if (!cardsToPlay.Any() &&
                            opponentTrumps.Count == 1 &&
                            SevenValue >= GameValue &&
                            hands[MyIndex].CardCount(_trump) == 2 &&
                            hands[MyIndex].SuitCount == Game.NumSuits &&
                            !topCards.Any())
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump);
                        }
                        if (!cardsToPlay.Any() &&
                            opponentTrumps.Count > 0 &&
                            !(_gameType == (Hra.Hra | Hra.Sedma) &&     //pokud hraju sedmu a mam posl. dva trumfy setri trumfy nakonec
                              hands[MyIndex].CardCount(_trump) == 2) &&
                            hands[MyIndex].CardCount(_trump) > 1 &&
                            !(hands[MyIndex].CardCount(_trump) <= opponentTrumps.Count &&
                              opponentTrumps.Any(i => hands[MyIndex].All(j => i.IsHigherThan(j, _trump)))) &&
                            !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                          .Where(b => b != _trump)
                                          .Any(b => hands[MyIndex].CardCount(b) >= 5) &&
                            hands[MyIndex].All(i => (i.Suit != _trump &&
                                                        !holesPerSuit[i.Suit].Any() &&
                                                        _probabilities.PotentialCards(player2).CardCount(i.Suit) > 1 &&
                                                        _probabilities.PotentialCards(player3).CardCount(i.Suit) > 1) ||
                                                    (i.Suit == _trump &&
                                                        unwinnableLowCards.Contains(i))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                i.Value < Hodnota.Desitka);
                        }
                        //pri sedme pokud mas o dva trumfy vic nez souperi a nemas dlouhou bocni barvu s esem
                        //ani bocni barvu kterou souper nezna a pritom muze mit trumf
                        //tak hraj trumf
                        if (!cardsToPlay.Any() &&
                            SevenValue >= GameValue &&
                            opponentTrumps.Count > 0 &&
                            opponentTrumps.Count <= 2 &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps.Count + 1 &&
                            hands[MyIndex].CardCount(_trump) > unwinnableLowCards.Where(i => i.Suit != _trump).Count() + 1 &&
                            !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Any(b => b != _trump &&
                                           hands[MyIndex].CardCount(b) >= 4) &&
                            !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Any(b => b != _trump &&
                                           hands[MyIndex].Any(i => i.Suit == b &&
                                                                   i.Value < Hodnota.Desitka) &&
                                           ((!_probabilities.PotentialCards(player2).HasSuit(b) &&
                                             _probabilities.PotentialCards(player2).HasSuit(_trump)) ||
                                            (!_probabilities.PotentialCards(player3).HasSuit(b) &&
                                             _probabilities.PotentialCards(player2).HasSuit(_trump)))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                (topCards.HasSuit(_trump) ||
                                                                                 i.Value < Hodnota.Desitka));
                        }
                        if (!cardsToPlay.Any() &&
                            opponentTrumps.Count > 0 &&
                            (_gameType & Hra.Kilo) != 0 &&
                            hands[MyIndex].HasA(_trump) &&
                            hands[MyIndex].HasK(_trump) &&
                            (_probabilities.CardProbability(player2, new Card(_trump, Hodnota.Desitka)) > 0 ||
                                _probabilities.CardProbability(player3, new Card(_trump, Hodnota.Desitka)) > 0) &&
                            hands[MyIndex].All(i => topCards.HasSuit(i.Suit)))
                        {
                            //pokud hrajeme kilo a dostali jsme se az sem (tedy se nehrala nizka karta), tak vytlac trumfovou X
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                i.Value < Hodnota.Desitka);
                        }
                        if (!cardsToPlay.Any() &&
                            (RoundNumber == 8 ||
                                RoundNumber == 9) &&
                            (_gameType & Hra.Sedma) == 0 &&
                            hands[MyIndex].CardCount(_trump) == 2 &&
                            hands[MyIndex].Has7(_trump) &&
                            opponentTrumps.Count >= 2)
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
                        if (_gameType == (Hra.Hra | Hra.Sedma) &&
                            hands[MyIndex].CardCount(_trump) == 2 &&
                            !(opponentTrumps.Count == 1 &&
                              SevenValue >= GameValue &&
                              hands[MyIndex].SuitCount == Game.NumSuits &&
                              !topCards.Any()))
                        {
                            //pokud hraju sedmu a mam posl. dva trumfy setri trumfy nakonec
                            return null;
                        }
                        //pokud mas jen trumfy nebo ostre karty ktere nejdou uhrat, tak hraj trumf
                        if (!cardsToPlay.Any() &&
                            opponentTrumps.Any() &&
                            hands[MyIndex].All(i => i.Suit == _trump ||
                                                    (i.Suit != _trump &&
                                                     i.Value >= Hodnota.Desitka &&
                                                     (unwinnableLowCards.Contains(i) ||
                                                      (!_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                       _probabilities.PotentialCards(player2).HasSuit(_trump)) ||
                                                      (!_probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                       _probabilities.PotentialCards(player3).HasSuit(_trump))))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value < Hodnota.Desitka &&
                                                                                i.Suit == _trump);
                        }
                        //zkus vytlacit trumf trumfem pokud pri sedme nemam dlouhou netrumfovou barvu
                        if (!cardsToPlay.Any() &&
                            SevenValue >= GameValue &&
                            !longSuits.Any() &&
                            hands[MyIndex].CardCount(_trump) > unwinnableLowCards.Count &&
                            myInitialHand.CardCount(_trump) >= 4 &&
                            !topCards.Any(i => i.Suit != _trump) &&
                            _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 &&
                            _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0)
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value < Hodnota.Desitka &&
                                                                                i.Suit == _trump &&
                                                                                !(!myInitialHand.Has7(_trump) &&    //pokud se sedmou uz nejde nic delat, tak si nenechavej na ruce polonkovou X
                                                                                  _hands[MyIndex].CardCount(_trump) == 2 &&
                                                                                  _hands[MyIndex].HasX(_trump) &&
                                                                                  opponentTrumps.HasA(_trump) &&
                                                                                  opponentTrumps.Count > 2));
                        }
                        //zkus vytlacit trumf trumfem pokud pri sedme nemam dlouhou netrumfovou barvu
                        if (!cardsToPlay.Any() &&
                            SevenValue >= GameValue &&
                            !longSuits.Any() &&
                            hands[MyIndex].CardCount(_trump) > unwinnableLowCards.Count &&
                            myInitialHand.CardCount(_trump) >= 4 &&
                            !topCards.Any(i => i.Suit != _trump) &&
                            ((_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 &&
                              _probabilities.PotentialCards(player2).All(i => hands[MyIndex].HasSuit(i.Suit))) ||
                             (_probabilities.SuitProbability(player3, _trump, RoundNumber) > 0 &&
                              _probabilities.PotentialCards(player3).All(i => hands[MyIndex].HasSuit(i.Suit)))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value < Hodnota.Desitka &&
                                                                                i.Suit == _trump &&
                                                                                !(!myInitialHand.Has7(_trump) &&    //pokud se sedmou uz nejde nic delat, tak si nenechavej na ruce polonkovou X
                                                                                  _hands[MyIndex].CardCount(_trump) == 2 &&
                                                                                  _hands[MyIndex].HasX(_trump) &&
                                                                                  opponentTrumps.HasA(_trump) &&
                                                                                  opponentTrumps.Count > 2));
                        }
                        //zkus vytlacit trumf trumfem pokud pri sedme nemam dlouhou netrumfovou barvu
                        if (!cardsToPlay.Any() &&
                            SevenValue >= GameValue &&
                            !longSuits.Any() &&
                            hands[MyIndex].CardCount(_trump) > unwinnableLowCards.Count + 1 &&
                            myInitialHand.CardCount(_trump) >= 4 &&
                            !topCards.Any(i => i.Suit != _trump) &&
                            (_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 ||
                             _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value < Hodnota.Desitka &&
                                                                                i.Suit == _trump &&
                                                                                !(!myInitialHand.Has7(_trump) &&    //pokud se sedmou uz nejde nic delat, tak si nenechavej na ruce polonkovou X
                                                                                  _hands[MyIndex].CardCount(_trump) == 2 &&
                                                                                  _hands[MyIndex].HasX(_trump) &&
                                                                                  opponentTrumps.HasA(_trump) &&
                                                                                  opponentTrumps.Count > 2));
                        }
                        if (!cardsToPlay.Any() &&
                            _gameType == (Hra.Hra | Hra.Sedma) &&
                            hands[MyIndex].SuitCount < Game.NumSuits &&
                            (myInitialHand.CardCount(_trump) < 5 ||
                             ((myInitialHand.CardCount(_trump) == 5 &&
                               !myInitialHand.HasA(_trump) &&
                               !(myInitialHand.HasX(_trump) &&
                                 myInitialHand.HasK(_trump))))) &&
                            !(topCards.Any() &&
                              hands[MyIndex].CardCount(_trump) >= 3 &&
                              hands[MyIndex].All(i => i.Suit == _trump ||
                                                      topCards.Contains(i))))
                        {
                            //pokud hraju sedmu a mam malo trumfu, tak je setri na nakonec
                            return null;
                        }
                        //zkus vytlacit trumf trumfem pokud jich mam dost a v ruce mam jen trumfy A nebo X
                        if (!cardsToPlay.Any() &&
                            (hands[MyIndex].All(i => i.Value == Hodnota.Eso ||
                                                     i.Value == Hodnota.Desitka ||
                                                     i.Suit == _trump) ||
                             (hands[MyIndex].All(i => i.Suit == _trump ||
                                                      topCards.Contains(i)) &&
                              !topCards.HasSuit(_trump) &&
                              (hands[MyIndex].CardCount(_trump) > 1 ||
                               topCards.All(i => _probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 1 &&
                                                 _probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1)))) &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps.Count &&
                            opponentTrumps.Count > 0)
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
                            if (!(opponentTrumps.Count == 2 &&
                                  hands[MyIndex].HasA(_trump) &&
                                  hands[MyIndex].CardCount(_trump) > 1))
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                    i.Suit == _trump);
                            }
                        }
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

                        if (!cardsToPlay.Any() &&
                            unwinnableLowCards.Any(i => i.Suit != _trump &&
                                              i.Value < Hodnota.Desitka &&
                                              ((_probabilities.SuitProbability(player2, i.Suit, RoundNumber) < 1 &&
                                                _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0) ||
                                               (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) < 1 &&
                                                _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0))))
                        {
                            return null; //pokud mas nizke netrumfove barvy, tak si trumfy setri
                        }
                        if (!cardsToPlay.Any() &&
                            opponentTrumps.Count > 0 &&
                            (((_gameType & Hra.Sedma) != 0 &&
                              hands[MyIndex].CardCount(_trump) > opponentTrumps.Count + 1) ||
                             ((_gameType & Hra.Sedma) == 0 &&
                              hands[MyIndex].CardCount(_trump) > opponentTrumps.Count) ||
                              (topCards.HasSuit(_trump) &&
                               hands[MyIndex].CardCount(_trump) > opponentTrumps.Count)) &&
                            unwinnableLowCards.HasSuit(_trump) &&
                            hands[MyIndex].All(i => i.Suit == _trump ||
                                                    i.Value >= Hodnota.Desitka ||
                                                    (unwinnableLowCards.Contains(i) &&
                                                     hands[MyIndex].HasA(i.Suit))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                i.Value < Hodnota.Desitka).ToList();
                        }
                        //if (!cardsToPlay.Any() && 
                        //    opponentTrumps  == 1 &&
                        //    //hands[MyIndex].CardCount(_trump) >= 3 &&
                        //    lowCards.Count < hands[MyIndex].CardCount(_trump) &&
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
                                .Any(b => hands[MyIndex].HasA(b) &&
                                          _probabilities.HasSolitaryX(player3, b, RoundNumber) >= 1 - _epsilon))
                        {
                            //pokud muzes vytahnout plonkouvouo desitku z aktera, tak pravidlo nehraj
                            return null;
                        }
                        //zkus vytlacit trumf trumfem pokud jich mam dost a v ruce mam jen trumfy A nebo X
                        if ((_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].CardCount(_trump) == opponentTrumps.Count &&
                            hands[MyIndex].Any(i => i.Value >= Hodnota.Desitka &&
                                                      i.Suit != _trump &&
                                                      (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                       _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0)))
                        {
                            return null;
                        }


                        if (topCards.HasSuit(_trump) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b))
                                .All(b => (_probabilities.PotentialCards(TeamMateIndex).HasA(b) ||
                                           _probabilities.PotentialCards(TeamMateIndex).HasX(b)) &&
                                          !_probabilities.PotentialCards(opponent).HasSuit(b) &&
                                          _probabilities.PotentialCards(opponent).HasSuit(_trump)))
                        {
                            return null;
                        }

                        if (!cardsToPlay.Any() &&
                            (_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            myInitialHand.CardCount(_trump) <= 5 &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => myInitialHand.CardCount(b) >= 4 &&
                                          myInitialHand.HasA(b)))
                        {
                            //pokud hrajes sedmu proti s dlouhou bocni barvou tak pravidlo nehraj a zkus vyjet dlouhou bocni barvou
                            return null;
                        }

                        //if (!cardsToPlay.Any() &&
                        //    SevenValue >= GameValue &&
                        //    (_gameType & Hra.Sedma) != 0 &&
                        //    hands[MyIndex].SuitCount == Game.NumSuits &&
                        //    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        //        .Where(b => b != _trump)
                        //        .All(b => hands[MyIndex].CardCount(b) <= 4 &&
                        //                  !topCards.HasSuit(b)) &&
                        //    hands[MyIndex].HasSuit(_trump))
                        //{
                        //    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                        //                                                        ((i.Value < Hodnota.Desitka &&
                        //                                                          !(hands[MyIndex].HasX(i.Suit) &&
                        //                                                            hands[MyIndex].CardCount(i.Suit) == 2)) ||
                        //                                                         !_probabilities.PotentialCards(player3).HasA(i.Suit))).ToList();
                        //}
                        if (!cardsToPlay.Any() &&
                            SevenValue >= GameValue &&
                            (_gameType & Hra.Sedma) != 0 &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b))
                                .Any(b => !_probabilities.PotentialCards(player3).HasSuit(b)))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                i.Value < Hodnota.Desitka &&
                                                                                !_probabilities.PotentialCards(player3).HasSuit(i.Suit)).ToList();
                            if (!cardsToPlay.Any())
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    _probabilities.PotentialCards(player3).CardCount(_trump) == 1 &&
                                                                                    !_probabilities.PotentialCards(player3).HasA(i.Suit)).ToList();
                            }
                        }
                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Value != Hodnota.Eso &&
                                                                                i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                (!hands[MyIndex].HasA(i.Suit) ||
                                                                                 (!_probabilities.PotentialCards(player2).HasX(i.Suit) &&
                                                                                  !_probabilities.PotentialCards(player3).HasX(i.Suit)) ||
                                                                                 myInitialHand.CardCount(_trump) <= 2) &&
                                                                                (SevenValue < GameValue ||
                                                                                 !(topCards.HasSuit(i.Suit) &&
                                                                                   (topCards.HasA(i.Suit) ||
                                                                                    topCards.HasX(i.Suit)))) &&
                                                                                _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor &&
                                                                                (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0f ||
                                                                                 (PlayerBids[MyIndex] == 0 &&
                                                                                  _preferredSuits.Contains(i.Suit))) &&
                                                                                _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0f &&
                                                                                ((_probabilities.SuitProbability(player2, _trump, RoundNumber) == 0f &&
                                                                                  _probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0f) ||
                                                                                 (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0f &&
                                                                                  ((_probabilities.PotentialCards(player2)                      //musi byt sance, ze spoluhrac ma
                                                                                                  .Count(j => j.Suit == i.Suit &&               //v barve i neco jineho nez A nebo X
                                                                                                              j.Value > i.Value &&
                                                                                                              j.Value < Hodnota.Desitka) > (_probabilities.PotentialCards(player3).HasSuit(i.Suit)
                                                                                                                                            ? 1 : 2)) ||
                                                                                    !_probabilities.PotentialCards(player2).Any(j => j.Suit == i.Suit &&
                                                                                                                                    j.Value >= Hodnota.Desitka)))));

                        }
                        //zkus vytlacit trumf svou nebo spoluhracovou desitkou nebo esem pokud hraje souper sedmu nebo pokud x nemuze uhrat
                        if (!cardsToPlay.Any())// && _gameType == (Hra.Hra | Hra.Sedma))// && _rounds[0] != null)
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Value != Hodnota.Eso &&
                                                                                i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                (!hands[MyIndex].HasA(i.Suit) ||
                                                                                 (!_probabilities.PotentialCards(player2).HasX(i.Suit) &&
                                                                                  !_probabilities.PotentialCards(player3).HasX(i.Suit)) ||
                                                                                 myInitialHand.CardCount(_trump) <= 2) &&
                                                                                ((_gameType == (Hra.Hra | Hra.Sedma) &&
                                                                                  SevenValue > GameValue) ||
                                                                                 (SevenValue >= GameValue &&
                                                                                  hands[MyIndex].Has7(_trump)) ||
                                                                                 _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor) &&
                                                                                (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0f ||
                                                                                 (PlayerBids[MyIndex] == 0 &&
                                                                                  _preferredSuits.Contains(i.Suit))) &&
                                                                                _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0f &&
                                                                                (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0f ||
                                                                                 _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0f ||
                                                                                 (SevenValue >= GameValue &&
                                                                                  hands[MyIndex].Has7(_trump))) &&
                                                                                (SevenValue < GameValue ||
                                                                                 !(topCards.HasSuit(i.Suit) &&
                                                                                   (topCards.HasA(i.Suit) ||
                                                                                    topCards.HasX(i.Suit)))) &&
                                                                                (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0 &&
                                                                                ((_gameType == (Hra.Hra | Hra.Sedma) &&
                                                                                  SevenValue > GameValue) ||
                                                                                 (SevenValue >= GameValue &&
                                                                                  hands[MyIndex].Has7(_trump)) ||
                                                                                 (_probabilities.LikelyCards(player2).Any(j => j.Suit == i.Suit) &&
                                                                                  _probabilities.LikelyCards(player2).Where(j => j.Suit == i.Suit)
                                                                                                                     .Any(j => j.Value > i.Value &&
                                                                                                                               j.Value < Hodnota.Desitka)) ||
                                                                                 (!_probabilities.PotentialCards(player2).HasA(i.Suit) &&
                                                                                  !Enum.GetValues(typeof(Barva)).Cast<Barva>()   //pokud neni barva na kterou by kolega mohl namazat
                                                                                       .Where(b => b != _trump)
                                                                                       .Any(b => _probabilities.SuitProbability(player3, b, RoundNumber) == 1 &&
                                                                                                 (b == i.Suit ||
                                                                                                  (topCards.HasSuit(b) &&
                                                                                                   _probabilities.SuitProbability(player2, b, RoundNumber) == 0))))));
                            if (!cardsToPlay.Any() &&
                                !topCards.HasSuit(_trump) &&
                                hands[MyIndex].CardCount(_trump) <= opponentTrumps.Count)
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    !_bannedSuits.Contains(i.Suit) &&
                                                                                    (!hands[MyIndex].HasA(i.Suit) ||
                                                                                     (!_probabilities.PotentialCards(player2).HasX(i.Suit) &&
                                                                                      !_probabilities.PotentialCards(player3).HasX(i.Suit)) ||
                                                                                     myInitialHand.CardCount(_trump) <= 2) &&
                                                                                    (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0f ||
                                                                                     (PlayerBids[MyIndex] == 0 &&
                                                                                      _preferredSuits.Contains(i.Suit))) &&
                                                                                    _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0f &&
                                                                                    (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0f ||
                                                                                     _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0f) &&
                                                                                    (SevenValue < GameValue ||
                                                                                     !(topCards.HasSuit(i.Suit) &&
                                                                                       (topCards.HasA(i.Suit) ||
                                                                                        topCards.HasX(i.Suit)))) &&
                                                                                    (_gameType & Hra.Kilo) == 0 &&
                                                                                    ((_gameType == (Hra.Hra | Hra.Sedma) &&
                                                                                      SevenValue > GameValue) ||
                                                                                     (SevenValue >= GameValue &&
                                                                                      hands[MyIndex].Has7(_trump)) ||
                                                                                     _probabilities.PotentialCards(player2).Where(j => j.Suit == i.Suit)
                                                                                                                           .Any(j => j.Value > i.Value &&
                                                                                                                                     j.Value < Hodnota.Desitka) ||
                                                                                     (!_probabilities.PotentialCards(player2).HasA(i.Suit) &&
                                                                                      _probabilities.PotentialCards(opponent).CardCount(_trump) > hands[MyIndex].CardCount(_trump) &&
                                                                                      !Enum.GetValues(typeof(Barva)).Cast<Barva>()   //pokud neni barva na kterou by kolega mohl namazat
                                                                                           .Where(b => b != _trump)
                                                                                           .Any(b => _probabilities.SuitProbability(player3, b, RoundNumber) == 1 &&
                                                                                                     (b == i.Suit ||
                                                                                                      (topCards.HasSuit(b) &&
                                                                                                       _probabilities.SuitProbability(player3, b, RoundNumber) > 0 &&
                                                                                                       _probabilities.SuitProbability(player2, b, RoundNumber) == 0))))
                                                                                          ));
                                if (cardsToPlay.Any() &&
                                    cardsToPlay.All(i => i.Value >= Hodnota.Desitka ||
                                                         (i.Value == Hodnota.Kral && //pokud bys lib. kartou tlacil z kolegy osrou, tak nehled na bannedSuits
                                                          (_probabilities.PotentialCards(player2).HasA(i.Suit) ||
                                                           _probabilities.PotentialCards(player2).HasX(i.Suit)))) &&
                                    _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0f)
                                {
                                    var temp = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                     i.Value != Hodnota.Eso &&
                                                                                     i.Suit != _trump &&
                                                                                     !_bannedSuits.Contains(i.Suit) &&
                                                                                     (!hands[MyIndex].HasA(i.Suit) ||
                                                                                      (!_probabilities.PotentialCards(player2).HasX(i.Suit) &&
                                                                                       !_probabilities.PotentialCards(player3).HasX(i.Suit)) ||
                                                                                      myInitialHand.CardCount(_trump) <= 2) &&
                                                                                    (SevenValue < GameValue ||
                                                                                     !(topCards.HasSuit(i.Suit) &&
                                                                                       (topCards.HasA(i.Suit) ||
                                                                                        topCards.HasX(i.Suit)))) &&
                                                                                         (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0f ||
                                                                                      (PlayerBids[MyIndex] == 0 &&
                                                                                       _preferredSuits.Contains(i.Suit))) &&
                                                                                     (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0 &&
                                                                                     ((_gameType == (Hra.Hra | Hra.Sedma) &&
                                                                                       SevenValue > GameValue) ||
                                                                                      (SevenValue >= GameValue &&
                                                                                       hands[MyIndex].Has7(_trump)) ||
                                                                                      (_probabilities.LikelyCards(player2).Any(j => j.Suit == i.Suit) &&
                                                                                       _probabilities.LikelyCards(player2).Where(j => j.Suit == i.Suit)
                                                                                                                          .Any(j => j.Value > i.Value &&
                                                                                                                                    j.Value < Hodnota.Desitka)) ||
                                                                                      PlayerBids[TeamMateIndex] == 0 ||
                                                                                      (!_probabilities.PotentialCards(player2).HasA(i.Suit) &&
                                                                                       !Enum.GetValues(typeof(Barva)).Cast<Barva>()   //pokud neni barva na kterou by kolega mohl namazat
                                                                                            .Where(b => b != _trump)
                                                                                            .Any(b => _probabilities.SuitProbability(player3, b, RoundNumber) == 1 &&
                                                                                                      (b == i.Suit ||
                                                                                                       (topCards.HasSuit(b) &&
                                                                                                        _probabilities.SuitProbability(player3, b, RoundNumber) > 0 &&
                                                                                                        _probabilities.SuitProbability(player2, b, RoundNumber) == 0))))));
                                    if (temp.Any())
                                    {
                                        cardsToPlay = temp;
                                    }
                                    //nezbavuj se zbytecne ostrych karet, pokud muzes hrat nejakou nizkou
                                    if ((_gameType & (Hra.Sedma | Hra.Kilo | Hra.KiloProti)) == 0 &&
                                        (cardsToPlay.All(j => j.Value == Hodnota.Kral && //pokud bys kralem tlacil z kolegy ostrou
                                                              (_probabilities.PotentialCards(player2).HasA(j.Suit) ||
                                                               _probabilities.PotentialCards(player2).HasX(j.Suit))) ||
                                         (hands[MyIndex].Any(i => i.Suit != _trump &&
                                                                  !_bannedSuits.Contains(i.Suit) &&
                                                                  i.Value < Hodnota.Desitka &&
                                                                  ((_probabilities.PotentialCards(player2).Where(j => j.Suit == i.Suit)
                                                                                                          .Any(j => j.Value > i.Value &&
                                                                                                                    j.Value < Hodnota.Desitka) &&
                                                                    _probabilities.LikelyCards(player3).Where(j => j.Suit == i.Suit)
                                                                                                       .All(j => j.Value < Hodnota.Desitka)) ||
                                                                    (GameValue > SevenValue &&
                                                                     _probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0 &&
                                                                     _probabilities.SuitProbability(player2, _trump, RoundNumber) >= 1 - RiskFactor &&
                                                                     _probabilities.HasAOrXAndNothingElse(player3, i.Suit, RoundNumber) == 1)))) &&
                                          //vytlac trumf ostrou kartou pokud mas jen dve barvy
                                          //a ve zbyvajici barve mas vyssi karty nez souper
                                          !(cardsToPlay.All(i => i.Value >= Hodnota.Desitka) &&
                                            hands[MyIndex].SuitCount == 2 &&
                                            !hands[MyIndex].HasSuit(_trump) &&
                                            hands[MyIndex].All(i => (cardsToPlay.HasSuit(i.Suit) &&
                                                                     cardsToPlay.CardCount(i.Suit) == hands[MyIndex].CardCount(i.Suit)) ||
                                                                    (topCards.HasSuit(i.Suit) &&
                                                                     _probabilities.SuitProbability(opponent, i.Suit, RoundNumber) >= 0.95f)))))
                                    {
                                        cardsToPlay = Enumerable.Empty<Card>();
                                    }
                                }
                            }
                        }
                        if (!cardsToPlay.Any() &&
                            (_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            SevenValue >= GameValue &&
                            hands[MyIndex].CardCount(_trump) <= opponentTrumps.Count + 1)
                        {
                            //pokud hraju sedmu proti setri trumfy nakonec
                            return null;
                        }
                        //zkus vytlacit trumf trumfem pokud jich mam dost a v ruce mam jen trumfy A nebo X
                        if (!cardsToPlay.Any() &&
                            hands[MyIndex].All(i => i.Value == Hodnota.Eso ||
                                                    i.Value == Hodnota.Desitka ||
                                                    i.Suit == _trump) &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps.Count &&
                            opponentTrumps.Count > 0 &&
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
                            opponentTrumps.Count > 0 &&
                            (hands[MyIndex].CardCount(_trump) > opponentTrumps.Count ||
                             (hands[MyIndex].CardCount(_trump) == opponentTrumps.Count &&
                              hands[MyIndex].CardCount(_trump) > 1) &&
                              hands[MyIndex].Any(i => i.Suit == _trump &&
                                                      Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                          .Where(h => h > i.Value)
                                                          .Select(h => new Card(_trump, i.Value))
                                                          .Count(j => _probabilities.CardProbability(player3, j) > 0) < hands[MyIndex].CardCount(_trump))) &&
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
                            ((myInitialHand.CardCount(_trump) >= 5 &&
                              hands[MyIndex].CardCount(_trump) > opponentTrumps.Count) ||
                             hands[MyIndex].CardCount(_trump) > opponentTrumps.Count + 1))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Suit == _trump &&
                                                                                (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) > 0 ||
                                                                                 _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > 0));
                        }
                        if (!cardsToPlay.Any() &&
                            (_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].CardCount(_trump) == opponentTrumps.Count &&
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
                                .Any(b => hands[MyIndex].HasA(b) &&
                                          _probabilities.HasSolitaryX(player2, b, RoundNumber) >= 1 - _epsilon))
                        {
                            //pokud muzes vytahnout plonkouvouo desitku z aktera, tak pravidlo nehraj
                            return null;
                        }
                        //zkus vytlacit trumf trumfem pokud jich mam dost a v ruce mam jen trumfy A nebo X
                        if ((_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].CardCount(_trump) == opponentTrumps.Count &&
                            hands[MyIndex].Any(i => i.Value >= Hodnota.Desitka &&
                                                      i.Suit != _trump &&
                                                      (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                       _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0)))
                        {
                            return null;
                        }

                        if (topCards.HasSuit(_trump) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b))
                                .All(b => (_probabilities.PotentialCards(TeamMateIndex).HasA(b) ||
                                           _probabilities.PotentialCards(TeamMateIndex).HasX(b)) &&
                                          !_probabilities.PotentialCards(opponent).HasSuit(b) &&
                                          _probabilities.PotentialCards(opponent).HasSuit(_trump)))
                        {
                            return null;
                        }
                        if (!cardsToPlay.Any() &&
                            (_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            myInitialHand.CardCount(_trump) <= 5 &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => myInitialHand.CardCount(b) >= 4 &&
                                          myInitialHand.HasA(b)))
                        {
                            //pokud hrajes sedmu proti s dlouhou bocni barvou tak pravidlo nehraj a zkus vyjet dlouhou bocni barvou
                            return null;
                        }

                        //if (!cardsToPlay.Any() &&
                        //    SevenValue >= GameValue &&
                        //    (_gameType & Hra.Sedma) != 0 &&
                        //    hands[MyIndex].SuitCount == Game.NumSuits &&
                        //    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                        //        .Where(b => b != _trump)
                        //        .All(b => hands[MyIndex].CardCount(b) <= 4 &&
                        //                  !topCards.HasSuit(b)) &&
                        //    hands[MyIndex].HasSuit(_trump))
                        //{
                        //    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                        //                                                        (i.Value < Hodnota.Desitka ||
                        //                                                         !_probabilities.PotentialCards(player2).HasA(i.Suit))).ToList();
                        //}
                        if (!cardsToPlay.Any() &&
                            SevenValue >= GameValue &&
                            (_gameType & Hra.Sedma) != 0 &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b))
                                .Any(b => !_probabilities.PotentialCards(player2).HasSuit(b)))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                i.Value < Hodnota.Desitka &&
                                                                                !_probabilities.PotentialCards(player2).HasSuit(i.Suit)).ToList();
                            if (!cardsToPlay.Any())
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    _probabilities.PotentialCards(player2).CardCount(_trump) == 1 &&
                                                                                    !_probabilities.PotentialCards(player2).HasA(i.Suit)).ToList();
                            }
                        }
                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                    i.Value != Hodnota.Eso &&
                                                                    i.Suit != _trump &&
                                                                    !_bannedSuits.Contains(i.Suit) &&
                                                                    (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0f ||
                                                                     (PlayerBids[MyIndex] == 0 &&
                                                                      _preferredSuits.Contains(i.Suit))) &&
                                                                    _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0f &&
                                                                    ((_probabilities.SuitProbability(player3, _trump, RoundNumber) == 0f &&
                                                                      _probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0f) ||
                                                                     PlayerBids[TeamMateIndex] == 0 ||
                                                                     (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0f &&
                                                                      _probabilities.NoSuitOrSuitLowerThanXProbability(player3, i.Suit, RoundNumber) > 1 - RiskFactor &&
                                                                      _probabilities.PotentialCards(player3)                      //musi byt sance, ze spoluhrac ma
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j.Value > i.Value &&              //v barve i neco jineho nez A nebo X
                                                                                                j.Value < Hodnota.Desitka) > 1)));
                        }
                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                    i.Value != Hodnota.Eso &&
                                                                    i.Suit != _trump &&
                                                                    !_bannedSuits.Contains(i.Suit) &&
                                                                    (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0f ||
                                                                     (PlayerBids[MyIndex] == 0 &&
                                                                      _preferredSuits.Contains(i.Suit))) &&
                                                                    _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0f &&
                                                                    ((_probabilities.SuitProbability(player3, _trump, RoundNumber) == 0f &&
                                                                      _probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0f) ||
                                                                     (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0f &&
                                                                      _probabilities.NoSuitOrSuitLowerThanXProbability(player3, i.Suit, RoundNumber) > 1 - RiskFactor &&
                                                                      _probabilities.PotentialCards(player3)                      //musi byt sance, ze spoluhrac ma
                                                                                    .Where(j => j.Suit == i.Suit)
                                                                                    .Count(j => j.Value > i.Value &&              //v barve i neco jineho nez A nebo X
                                                                                                j.Value < Hodnota.Desitka) > 1)));
                        }
                        //zkus vytlacit trumf svou nebo spoluhracovou desitkou nebo esem pokud hraje souper sedmu nebo pokud desitku ci eso nemuze uhrat
                        if (!cardsToPlay.Any())// && _gameType == (Hra.Hra | Hra.Sedma))// && _rounds[0] != null)
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Value != Hodnota.Eso &&
                                                                                i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0f ||
                                                                                 (PlayerBids[MyIndex] == 0 &&
                                                                                  _preferredSuits.Contains(i.Suit))) &&
                                                                                _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0f &&
                                                                                (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0f ||
                                                                                 _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0f) &&
                                                                                (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0 &&
                                                                                ((_gameType == (Hra.Hra | Hra.Sedma) &&
                                                                                  SevenValue > GameValue) ||
                                                                                 (SevenValue >= GameValue &&
                                                                                  hands[MyIndex].Has7(_trump)) ||
                                                                                 (_probabilities.LikelyCards(player3).Any(j => j.Suit == i.Suit) &&
                                                                                  _probabilities.LikelyCards(player3).Where(j => j.Suit == i.Suit)
                                                                                                                     .Any(j => j.Value > i.Value &&
                                                                                                                               j.Value < Hodnota.Desitka)) ||
                                                                                 (_probabilities.LikelyCards(player3).Where(j => j.Suit == i.Suit)
                                                                                                                     .All(j => j.Value < Hodnota.Desitka) &&
                                                                                  !Enum.GetValues(typeof(Barva)).Cast<Barva>()   //pokud neni barva na kterou by kolega mohl namazat
                                                                                       .Where(b => b != _trump)
                                                                                       .Any(b => _probabilities.SuitProbability(player2, b, RoundNumber) == 1 &&
                                                                                                 (b == i.Suit ||
                                                                                                  (topCards.HasSuit(b) &&
                                                                                                   _probabilities.SuitProbability(player2, b, RoundNumber) > 0 &&
                                                                                                   _probabilities.SuitProbability(player3, b, RoundNumber) == 0))))));
                            if (!cardsToPlay.Any())
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                    i.Value != Hodnota.Eso &&
                                                                                    i.Suit != _trump &&
                                                                                    !_bannedSuits.Contains(i.Suit) &&
                                                                                    (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0f ||
                                                                                     (PlayerBids[MyIndex] == 0 &&
                                                                                      _preferredSuits.Contains(i.Suit))) &&
                                                                                    _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0f &&
                                                                                    (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0f ||
                                                                                     _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0f ||
                                                                                     (SevenValue >= GameValue &&
                                                                                      hands[MyIndex].Has7(_trump))) &&
                                                                                    (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0 &&
                                                                                    ((_gameType == (Hra.Hra | Hra.Sedma) &&
                                                                                      SevenValue > GameValue) ||
                                                                                     (SevenValue >= GameValue &&
                                                                                      hands[MyIndex].Has7(_trump)) ||
                                                                                     (_probabilities.LikelyCards(player3).Any(j => j.Suit == i.Suit) &&
                                                                                      _probabilities.LikelyCards(player3).Where(j => j.Suit == i.Suit)
                                                                                                                         .Any(j => j.Value > i.Value &&
                                                                                                                                   j.Value < Hodnota.Desitka)) ||
                                                                                     (_probabilities.LikelyCards(player3).Where(j => j.Suit == i.Suit)
                                                                                                                         .All(j => j.Value < Hodnota.Desitka) &&
                                                                                      !Enum.GetValues(typeof(Barva)).Cast<Barva>()   //pokud neni barva na kterou by kolega mohl namazat
                                                                                           .Where(b => b != _trump)
                                                                                           .Any(b => _probabilities.SuitProbability(player2, b, RoundNumber) == 1 &&
                                                                                                     (b == i.Suit ||
                                                                                                      (topCards.HasSuit(b) &&
                                                                                                      _probabilities.SuitProbability(player2, b, RoundNumber) > 0 &&
                                                                                                      _probabilities.SuitProbability(player3, b, RoundNumber) == 0))))));
                                if (cardsToPlay.Any() &&
                                    cardsToPlay.All(i => i.Value >= Hodnota.Desitka) &&
                                    _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0f)
                                {
                                    var temp = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                     i.Value != Hodnota.Eso &&
                                                                                     i.Suit != _trump &&
                                                                                     !_bannedSuits.Contains(i.Suit) &&
                                                                                     (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0f ||
                                                                                      (PlayerBids[MyIndex] == 0 &&
                                                                                       _preferredSuits.Contains(i.Suit))) &&
                                                                                     ((_gameType & (Hra.Kilo | Hra.KiloProti)) == 0 ||
                                                                                      hands[MyIndex].CardCount(_trump) >= _probabilities.PotentialCards(opponent).CardCount(_trump)) &&
                                                                                     ((_gameType == (Hra.Hra | Hra.Sedma) &&
                                                                                       SevenValue > GameValue) ||
                                                                                      (SevenValue >= GameValue &&
                                                                                       hands[MyIndex].Has7(_trump)) ||
                                                                                      _probabilities.LikelyCards(player3).Where(j => j.Suit == i.Suit)
                                                                                                                         .Any(j => j.Value > i.Value &&
                                                                                                                                   j.Value < Hodnota.Desitka) ||
                                                                                      (_probabilities.LikelyCards(player3).Where(j => j.Suit == i.Suit)
                                                                                                                          .All(j => j.Value < Hodnota.Desitka) &&
                                                                                       !Enum.GetValues(typeof(Barva)).Cast<Barva>()   //pokud neni barva na kterou by kolega mohl namazat
                                                                                            .Where(b => b != _trump)
                                                                                            .Any(b => _probabilities.SuitProbability(player2, b, RoundNumber) == 1 &&
                                                                                                      (b == i.Suit ||
                                                                                                       (topCards.HasSuit(b) &&
                                                                                                        _probabilities.SuitProbability(player2, b, RoundNumber) > 0 &&
                                                                                                        _probabilities.SuitProbability(player3, b, RoundNumber) == 0))))));
                                    if (temp.Any())
                                    {
                                        cardsToPlay = temp;
                                    }
                                    //nezbavuj se zbytecne ostrych karet, pokud muzes hrat nejakou nizkou
                                    if ((_gameType & (Hra.Sedma | Hra.Kilo | Hra.KiloProti)) == 0 &&
                                        (cardsToPlay.All(j => j.Value == Hodnota.Kral && //pokud bys kralem tlacil z kolegy ostrou
                                                              (_probabilities.PotentialCards(TeamMateIndex).HasA(j.Suit) ||
                                                               _probabilities.PotentialCards(TeamMateIndex).HasX(j.Suit))) ||
                                         (hands[MyIndex].Any(i => i.Suit != _trump &&
                                                                  !_bannedSuits.Contains(i.Suit) &&
                                                                  i.Value < Hodnota.Desitka &&
                                                                  ((_probabilities.PotentialCards(player2).Where(j => j.Suit == i.Suit)
                                                                                                          .Any(j => j.Value > i.Value &&
                                                                                                                    j.Value < Hodnota.Desitka) &&
                                                                    _probabilities.LikelyCards(player3).Where(j => j.Suit == i.Suit)
                                                                                                       .All(j => j.Value < Hodnota.Desitka)) ||
                                                                    (GameValue > SevenValue &&
                                                                     _probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0 &&
                                                                     _probabilities.SuitProbability(player2, _trump, RoundNumber) >= 1 - RiskFactor &&
                                                                     _probabilities.HasAOrXAndNothingElse(player3, i.Suit, RoundNumber) == 1)))) &&
                                          //vytlac trumf ostrou kartou pokud mas jen dve barvy
                                          //a ve zbyvajici barve mas vyssi karty nez souper
                                          !(cardsToPlay.All(i => i.Value >= Hodnota.Desitka) &&
                                            hands[MyIndex].SuitCount == 2 &&
                                            !hands[MyIndex].HasSuit(_trump) &&
                                            hands[MyIndex].All(i => (cardsToPlay.HasSuit(i.Suit) &&
                                                                     cardsToPlay.CardCount(i.Suit) == hands[MyIndex].CardCount(i.Suit)) ||
                                                                    (topCards.HasSuit(i.Suit) &&
                                                                     _probabilities.SuitProbability(opponent, i.Suit, RoundNumber) >= 0.95f)))))
                                    {
                                        cardsToPlay = Enumerable.Empty<Card>();
                                    }
                                }
                            }
                        }
                        if (!cardsToPlay.Any() &&
                            (_gameType & Hra.SedmaProti) != 0 &&
                            hands[MyIndex].Has7(_trump) &&
                            SevenValue >= GameValue &&
                            hands[MyIndex].CardCount(_trump) <= _probabilities.PotentialCards(opponent).CardCount(_trump) + 1)
                        {
                            //pokud hraju sedmu proti setri trumfy nakonec
                            return null;
                        }
                        if (!cardsToPlay.Any() &&
                            opponentTrumps.Count > 0 &&
                            hands[MyIndex].All(i => i.Value == Hodnota.Eso ||
                                                    i.Value == Hodnota.Desitka ||
                                                    i.Suit == _trump) &&
                            (hands[MyIndex].CardCount(_trump) > opponentTrumps.Count ||
                             (hands[MyIndex].CardCount(_trump) == opponentTrumps.Count &&
                              hands[MyIndex].CardCount(_trump) > 1) &&
                              hands[MyIndex].Any(i => i.Suit == _trump &&
                                                      Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                          .Where(h => h > i.Value)
                                                          .Select(h => new Card(_trump, i.Value))
                                                          .Count(j => _probabilities.CardProbability(player2, j) > 0) < hands[MyIndex].CardCount(_trump))) &&
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
                            opponentTrumps.Count > 0 &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps.Count &&
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
                            hands[MyIndex].CardCount(_trump) > opponentTrumps.Count)
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Suit == _trump &&
                                                                                (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) > 0 ||
                                                                                 _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > 0 ||
                                                                                 Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                     .Where(b => b != _trump &&
                                                                                                 hands[MyIndex].HasSuit(b))
                                                                                     .All(b => hands[MyIndex].HasA(b))));
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

            if (RoundNumber == 8)
            {
                yield return new AiRule()
                {
                    Order = 6,
                    Description = "šetřit trumfy nakonec",
                    SkipSimulations = true,
                    #region ChooseCard1 Rule7
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
                Description = "zkus vytlačit eso nebo desítku",
                SkipSimulations = true,
                #region ChooseCard1 Rule 2
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex == player3 &&
                        (GameValue > SevenValue ||
                         !topCards.Any(i => i.Suit != _trump)) &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump)
                            .Any(b => hands[MyIndex].Any(i => i.Suit == b &&
                                                              i.Value >= Hodnota.Svrsek) &&
                                      !myInitialHand.HasA(b) &&
                                      !myInitialHand.HasX(b) &&
                                      (_probabilities.PotentialCards(player3).HasA(b) ||
                                       _probabilities.PotentialCards(player3).HasSuit(_trump)) &&
                                      (_probabilities.PotentialCards(opponent).HasA(b) ||
                                       _probabilities.PotentialCards(opponent).HasX(b))))
                    {
                        //pokud po me hraje akter snaz se nejprve vytlacit bodovanou kartu pres barvu kde nemam ani A ani X
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                i.Value >= Hodnota.Svrsek &&
                                                                                !myInitialHand.HasA(i.Suit) &&
                                                                                !myInitialHand.HasX(i.Suit) &&
                                                                                (_probabilities.PotentialCards(player3).HasA(i.Suit) ||
                                                                                 _probabilities.PotentialCards(player3).HasSuit(_trump)) &&
                                                                                (_probabilities.PotentialCards(opponent).HasA(i.Suit) ||
                                                                                 _probabilities.PotentialCards(opponent).HasX(i.Suit)));

                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }

                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 8,
                Description = "hrát nízkou kartu od desítky",
                SkipSimulations = true,
                #region ChooseCard1 Rule8
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex != -1 &&
                        (GameValue > SevenValue ||
                         !topCards.Any(i => i.Suit != _trump)))
                    {
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                i.Value < Hodnota.Desitka &&
                                                                                hands[MyIndex].HasX(i.Suit) &&
                                                                                _probabilities.PotentialCards(opponent).HasA(i.Suit))
                                                                    .ToList();

                        if (TeamMateIndex == player2)
                        {
                            cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
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

            yield return new AiRule()
            {
                Order = 9,
                Description = "zkusit uhrát bodovanou kartu",
                SkipSimulations = true,
                #region ChooseCard1 Rule9
                ChooseCard1 = () =>
                {
                    //proti kilu zkus vytlacit netrumfovou hlasku pokud se hraje kilo na prvni hlas
                    //pokud kolega flekoval sedmu nebo pokud hlasil sedmu proti tak pravidlo nehraj
                    if (TeamMateIndex != -1 &&
                        ((PlayerBids[TeamMateIndex] & Hra.Sedma) != 0 ||
                         ((_gameType & Hra.SedmaProti) != 0 &&
                          !myInitialHand.Has7(_trump))))
                    {
                        return null;
                    }
                    //pokud jsem vyjel esem od delsi bocni barvy, tak pokracuj hranim v barve, mela by vytlacit trumf
                    if (TeamMateIndex == -1 &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump &&
                                        hands[MyIndex].HasSuit(b))
                            .Any(b => myInitialHand.CardCount(b) >= 4 &&
                                      myPlayedCards.HasA(b) &&
                                      !((_probabilities.CertainCards(player2).HasSuit(b) &&          //neplati pokud jeden souper barvu zna 
                                         !_probabilities.PotentialCards(player3).HasSuit(_trump)) || //a druhy nema trumfy
                                        (_probabilities.CertainCards(player3).HasSuit(b) &&
                                         !_probabilities.PotentialCards(player2).HasSuit(_trump)))))
                    {
                        return null;
                    }
                    //pokud jsem vyjel esem od delsi bocni barvy, tak pokracuj hranim v barve, mela by vytlacit trumf
                    if (TeamMateIndex != -1 &&
                        ((_gameType & Hra.SedmaProti) == 0 ||
                         myInitialHand.Has7(_trump)) &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump &&
                                        hands[MyIndex].HasSuit(b))
                            .Any(b => myInitialHand.CardCount(b) >= 4 &&
                                      myPlayedCards.HasA(b) &&
                                      _probabilities.PotentialCards(TeamMateIndex).HasSuit(b)))
                    {
                        return null;
                    }
                    //pokud jsem vyjel esem a desitkou od bocni barvy, tak pokracuj hranim v barve, mela by vytlacit trumf
                    if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump &&
                                        hands[MyIndex].HasSuit(b))
                            .Any(b => myPlayedCards.HasA(b) &&
                                      myPlayedCards.HasX(b) &&
                                      !(TeamMateIndex != -1 &&  //neplati pokud kolega flekoval a muzu z nej vytlacit trumf
                                        PlayerBids[TeamMateIndex] != 0 &&
                                        _probabilities.PotentialCards(TeamMateIndex).HasSuit(_trump) &&
                                        _probabilities.PotentialCards(TeamMateIndex).CardCount(b) <= 1)))
                    {
                        return null;
                    }
                    if (TeamMateIndex != -1 &&
                        PlayerBids[TeamMateIndex] != 0 &&
                        PlayerBids[MyIndex] == 0 &&
                        _teamMatesSuits.Any(b => hands[MyIndex].Any(i => i.Suit == b &&
                                                                         i.Value < Hodnota.Desitka)))
                    {
                        return null;
                    }
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
                            .Any(b => hands[MyIndex].Any(i => i.Suit == b &&
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
                    //proti kilu pravidlo nehraj pokud mas nejakou kratkou plonkovou barvu
                    //jestli ma akter nizkou kartu v barve kde mam A nebo X, tak s ni bude drive nebo pozdeji muset vyjet
                    if (TeamMateIndex != -1 &&
                        (_gameType & Hra.Kilo) != 0 &&
                        hands[MyIndex].Any(i => i.Suit != _trump &&
                                                 !hands[MyIndex].HasA(i.Suit) &&
                                                 !hands[MyIndex].HasX(i.Suit) &&
                                                 hands[MyIndex].CardCount(i.Suit) <= 2 &&
                                                 ((TeamMateIndex == player3 &&
                                                  _probabilities.PotentialCards(TeamMateIndex).Count(j => j.Suit == i.Suit &&
                                                                                                          j.Value < Hodnota.Desitka) >= 2) ||
                                                  _probabilities.PotentialCards(TeamMateIndex).Count(j => j.Suit == i.Suit &&
                                                                                                          j.Value > i.Value &&
                                                                                                          j.Value < Hodnota.Desitka) >= 2)))
                    {
                        return null;
                    }
                    //nehraj pri sedme pokud mas pokracovat v dlouhe tlacne barve - dobiraky si nech na pozdeji
                    if (TeamMateIndex == -1 &&
                        (_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&
                        hands[MyIndex].Any(i => i.Suit != _trump &&
                                                myPlayedCards.HasA(i.Suit) &&
                                                myInitialHand.CardCount(i.Suit) >= 5))
                    {
                        return null;
                    }
                    //nehraj pravidlo pokud spoluhrac hlasil sedmu proti nebo flekoval - bodovane karty budeme mazat
                    if (TeamMateIndex != -1 &&
                        (PlayerBids[TeamMateIndex] != 0 ||
                         (_gameType & Hra.SedmaProti) != 0) &&
                        _probabilities.CardProbability(TeamMateIndex, new Card(_trump, Hodnota.Sedma)) == 1)
                    {
                        return null;
                    }
                    //nehraj pokud mas hodne trumfu (neplati pri sedme na 5 karet bez esa nebo desitky, tehdy se pokusime uhrat netrumfove eso nebo desitku)
                    if ((hands[MyIndex].CardCount(_trump) >= 5 &&
                        !(SevenValue >= GameValue &&
                          (!hands[MyIndex].HasA(_trump) ||
                           !hands[MyIndex].HasX(_trump)) &&
                          hands[MyIndex].CardCount(Hodnota.Eso) +
                          hands[MyIndex].Where(i => i.Suit != _trump)
                                        .CardCount(Hodnota.Desitka) >= 1)) ||
                        (TeamMateIndex != -1 &&
                         (_gameType & Hra.Kilo) == 0 &&
                         SevenValue < GameValue &&
                         hands[MyIndex].CardCount(_trump) >= 2 &&
                         hands[MyIndex].HasA(_trump) &&
                         myInitialHand.CardCount(Hodnota.Eso) <= 2) &&    //pokud mas na zacatku 3 a vice es, tak zkus nejake uhrat
                         !(hands[MyIndex].SuitCount == Game.NumSuits &&
                           hands[MyIndex].Any(i => i.Suit != _trump &&     //pokud mas v bocni barve AX a max 4 karty, tak zkus neco uhrat
                                                  i.Value >= Hodnota.Desitka &&
                                                  myInitialHand.HasA(i.Suit) &&
                                                  myInitialHand.HasX(i.Suit) &&
                                                  myInitialHand.CardCount(i.Suit) <= 4)))
                    {
                        return null;
                    }
                    //nehraj pokud si muzes odmazat trumfy a pozdeji ostre namazat
                    if (TeamMateIndex != -1 &&
                        SevenValue < GameValue &&
                        (TeamMateIndex == player3 ||
                         PlayerBids[TeamMateIndex] == 0) &&
                        !((_gameType & Hra.SedmaProti) != 0 &&
                          hands[MyIndex].Has7(_trump)) &&
                        //hands[MyIndex].SuitCount == 2 &&
                        (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                             .Where(b => b != _trump)
                             .Any(b => !hands[MyIndex].HasSuit(b) &&
                                       teamMatePlayedCards.HasSuit(b) &&
                                       _probabilities.PotentialCards(TeamMateIndex).HasSuit(b) &&
                                       _probabilities.PotentialCards(opponent).HasSuit(b)) ||
                         (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                              .Any(b => hands[MyIndex].HasSuit(b) &&
                                        !_probabilities.PotentialCards(TeamMateIndex).HasSuit(b) &&
                                        _probabilities.PotentialCards(opponent).HasSuit(b) &&
                                        (b == _trump ||
                                         topCards.HasSuit(b))) &&
                          !_probabilities.PotentialCards(TeamMateIndex).HasSuit(_trump))) &&
                        (!Enum.GetValues(typeof(Barva)).Cast<Barva>()
                              .Where(b => b != _trump)
                              .All(b => myInitialHand.HasA(b)) ||
                         hands[MyIndex].HasA(_trump) ||
                         (_gameType & Hra.Kilo) != 0) &&
                        //PlayerBids[MyIndex] == 0 &&
                        potentialGreaseCards.Any() &&
                        hands[MyIndex].HasSuit(_trump) &&
                        hands[MyIndex].CardCount(_trump) <= 1 &&
                        //!hands[MyIndex].HasA(_trump) &&
                        !hands[MyIndex].HasX(_trump))
                    {
                        return null;
                    }
                    //if (TeamMateIndex == player3 &&
                    //    hands[MyIndex].HasSuit(_trump) &&
                    //    myInitialHand.CardCount(_trump) <= 1 &&
                    //    !hands[MyIndex].HasA(_trump) &&
                    //    !hands[MyIndex].HasX(_trump) &&
                    //    potentialGreaseCards.All(i => myInitialHand.CardCount(i.Suit) >= 5 &&
                    //                                  !_probabilities.CertainCards(opponent).HasSuit(i.Suit)))
                    //{
                    //    return null;
                    //}
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
                        //pokud kolega flekoval a mam jen trumfy nebo nejvyssi karty, ktere souper zna
                        //tak zkus uhrat bodovanou kartu abys z kolegy netlacil trumfy
                        if (PlayerBids[TeamMateIndex] != 0 &&
                            topCards.Any(i => i.Suit != _trump &&
                                              i.Value >= Hodnota.Desitka) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b))
                                .All(b => topCards.HasSuit(b) &&
                                          (_probabilities.LikelyCards(opponent).HasSuit(b) ||
                                           _probabilities.SuitProbability(opponent, b, RoundNumber) >= 1 - _epsilon)))
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value >= Hodnota.Desitka &&
                                                                                    topCards.Contains(i));

                            return cardsToPlay.OrderByDescending(i => i.Value).First();
                        }

                        //pokud mas malo trumfu a zbyva jen jedna netrumfova barva kde mas A nebo X kterou souper muze mit
                        //a v ostatnich netrumfovych barvach ma asi nejvyssi karty souper
                        //tak zkus uhrat posledni teoretickou bodovanou kartu
                        if (hands[MyIndex].CardCount(_trump) < opponentTrumps.Count &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Count(b => _probabilities.PotentialCards(opponent).HasSuit(b) &&
                                            !_probabilities.LikelyCards(opponent).HasX(b) &&
                                            topCards.HasSuit(b) &&
                                            (hands[MyIndex].HasA(b) ||
                                             hands[MyIndex].HasX(b))) == 1 &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b) &&
                                            !topCards.HasSuit(b))
                                .All(b => _probabilities.PotentialCards(opponent).HasSuit(b)))
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value >= Hodnota.Desitka &&
                                                                                    topCards.HasSuit(i.Suit) &&
                                                                                    _probabilities.PotentialCards(opponent).HasSuit(i.Suit) &&
                                                                                    !_probabilities.LikelyCards(opponent).HasX(i.Suit) &&
                                                                                    !(potentialGreaseCards.Contains(i) &&
                                                                                      myInitialHand.CardCount(i.Suit) >= 6));

                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.OrderByDescending(i => i.Value).First();
                            }
                        }

                        if (SevenValue >= GameValue &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .All(b => myInitialHand.CardCount(b) <= 4) &&
                            topCards.Any(i => i.Suit != _trump &&
                                              i.Value >= Hodnota.Desitka &&
                                              _probabilities.PotentialCards(opponent).HasSuit(i.Suit)))
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    topCards.Contains(i) &&
                                                                                    i.Value >= Hodnota.Desitka &&
                                                                                    _probabilities.PotentialCards(opponent).HasSuit(i.Suit));

                            return cardsToPlay.OrderBy(i => _probabilities.PotentialCards(opponent).CardCount(i.Suit))
                                              .ThenByDescending(i => i.Value).First();
                        }

                        //pokud mas jen jednu barvu a mas A i X a souper muze v barve mit aspon 3 karty, tak to riskni
                        if (hands[MyIndex].SuitCount == 1 &&
                            !hands[MyIndex].HasSuit(_trump) &&
                            hands[MyIndex].Count(i => i.Value >= Hodnota.Desitka) == 2 &&
                            _probabilities.PotentialCards(opponent).CardCount(hands[MyIndex].First(i => true).Suit) >= 3)
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]);

                            return cardsToPlay.OrderByDescending(i => i.Value).First();
                        }
                        var actorSuits = _rounds.Where(r => r != null &&
                                                            r.player1.PlayerIndex == opponent)
                                                .Select(r => r.c1.Suit)
                                                .Distinct().ToList();

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
                                                                                    _probabilities.SuitProbability(opponent, i.Suit, RoundNumber) > 0 &&
                                                                                    !(_probabilities.LikelyCards(opponent).HasSuit(i.Suit) ||
                                                                                      _probabilities.SuitProbability(opponent, i.Suit, RoundNumber) >= 1 - _epsilon ||
                                                                                      (!_probabilities.PotentialCards(TeamMateIndex).HasSuit(i.Suit) &&
                                                                                       _probabilities.PotentialCards(opponent).CardCount(i.Suit) > 2)))
                                                                        .ToList();

                            if (cardsToPlay.Any(i => !actorSuits.Contains(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => !actorSuits.Contains(i.Suit)).ToList();
                            }

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

                            if (cardsToPlay.Any(i => !actorSuits.Contains(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => !actorSuits.Contains(i.Suit)).ToList();
                            }

                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.First();
                            }
                        }

                        //pokud nemuzes eso namazat a lze ho teoreticky uhrat, tak do toho jdi
                        if (//hands[MyIndex].CardCount(_trump) > 1 &&
                            (SevenValue >= GameValue ||                      //pri sedme vzdy
                             ((hands[MyIndex].HasSuit(_trump) ||
                              hands[MyIndex].SuitCount == 3) &&
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()    //pri hre jen kdyz nemas nejakou plonkovou barvu
                                  .Where(b => b != _trump &&
                                              hands[MyIndex].HasSuit(b))
                                  .All(b => hands[MyIndex].HasA(b) ||        //mam jen barvy kde znam A
                                            (hands[MyIndex].HasX(b) &&       //nebo kde jsem znal AX
                                             myInitialHand.HasA(b)) ||
                                            (hands[MyIndex].Where(i => i.Suit == b)     //nebo barvy kde by moje nebo kolegovy AX bral souper trumfem
                                                           .All(i => i.Value >= Hodnota.Desitka) &&
                                             !_probabilities.PotentialCards(opponent).HasSuit(b)) ||
                                            (!myInitialHand.HasA(b) &&
                                             !myInitialHand.HasX(b) &&
                                             hands[MyIndex].Any(i => i.Suit == b &&
                                                                     (_probabilities.CertainCards(TeamMateIndex).HasA(i.Suit) ||
                                                                      _probabilities.CertainCards(TeamMateIndex).HasX(i.Suit)) &&
                                                                     !_probabilities.PotentialCards(opponent).HasSuit(i.Suit) &&
                                                                     _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) < 1 - RiskFactor))) &&
                              !(hands[MyIndex].SuitCount < Game.NumSuits &&  //vyjma situace kdy kolega flekoval a ty mas malo barev (muzes mazat)
                                PlayerBids[TeamMateIndex] != 0))) &&
                            _probabilities.PotentialCards(opponent).CardCount(_trump) > hands[MyIndex].CardCount(_trump))
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    (i.Value == Hodnota.Eso ||
                                                                                     (i.Value == Hodnota.Desitka &&
                                                                                      !hands[MyIndex].HasA(i.Suit) &&
                                                                                      myInitialHand.HasA(i.Suit))) &&
                                                                                    _probabilities.PotentialCards(opponent).HasSuit(i.Suit) &&
                                                                                    !_probabilities.LikelyCards(opponent).HasX(i.Suit) &&
                                                                                    !_probabilities.CertainCards(Game.TalonIndex).HasX(i.Suit) &&
                                                                                    !(_probabilities.CertainCards(opponent).HasSuit(i.Suit) ||
                                                                                      (!_probabilities.PotentialCards(TeamMateIndex).HasSuit(i.Suit) &&
                                                                                       _probabilities.PotentialCards(opponent).CardCount(i.Suit) > 2)) &&
                                                                                    !(potentialGreaseCards.Contains(i) &&
                                                                                      myInitialHand.CardCount(i.Suit) >= 6))
                                                                        .ToList();
                            if (cardsToPlay.Any(i => !_probabilities.PotentialCards(opponent).HasX(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => !_probabilities.PotentialCards(opponent).HasX(i.Suit)).ToList();
                            }
                            if (cardsToPlay.Any(i => !actorSuits.Contains(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => !actorSuits.Contains(i.Suit)).ToList();
                            }

                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.OrderByDescending(i => i.Value).First();
                            }
                        }
                    }

                    if (TeamMateIndex != -1)
                    {
                        //pokud mas eso v netrumfove barve, o desitce nic nevim
                        //a ve vsech dalsich barvach bud ma souper jiste desitku nebo bych ohrozil kolegovy trumfy
                        //tak hraj eso
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                i.Value == Hodnota.Eso &&
                                                                                _probabilities.PotentialCards(opponent).HasSuit(i.Suit) &&
                                                                                !_probabilities.LikelyCards(opponent).HasX(i.Suit) &&
                                                                                !_probabilities.CertainCards(Game.TalonIndex).HasX(i.Suit) &&
                                                                                !(potentialGreaseCards.Contains(i) &&
                                                                                  myInitialHand.CardCount(i.Suit) >= 6) &&
                                                                                Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                    .Where(b => b != _trump &&
                                                                                                b != i.Suit &&
                                                                                                hands[MyIndex].HasSuit(b))
                                                                                    .All(b => _probabilities.LikelyCards(opponent).HasX(b) ||
                                                                                              (PlayerBids[TeamMateIndex] != 0 &&
                                                                                               _probabilities.PotentialCards(TeamMateIndex).CardCount(b) <= 1 &&
                                                                                               !_probabilities.LikelyCards(TeamMateIndex).HasSuit(b) &&
                                                                                               _probabilities.PotentialCards(TeamMateIndex).HasSuit(_trump))))
                                                                    .ToList();

                        if (cardsToPlay.Any())
                        {
                            return cardsToPlay.First();
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
                        hands[MyIndex].CardCount(_trump) <= opponentTrumps.Count) //pokud mas hodne trumfu a propad si az sem, tak pravidlo nehraj (asi si mas odmazat plonk x)
                    {
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                 i.Value >= Hodnota.Desitka &&
                                                                                 (i.Value == Hodnota.Desitka ||
                                                                                  (hands[MyIndex].CardCount(i.Suit) > 1 ||
                                                                                   hands[MyIndex].All(j => j.Suit == _trump ||
                                                                                                           j.Value >= Hodnota.Desitka))) &&
                                                                                 myInitialHand.CardCount(i.Suit) <= 5 &&
                                                                                 _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                                                 _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 &&
                                                                                 _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                 _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0)
                                                                    .ToList();

                        return cardsToPlay.OrderByDescending(i => myInitialHand.CardCount(i.Suit)).FirstOrDefault();
                    }
                    if (TeamMateIndex == -1 &&
                        (_gameType & Hra.Kilo) != 0 &&
                        myInitialHand.CardCount(_trump) <= 4 &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump)
                            .Any(b => hands[MyIndex].HasA(b) &&
                                      lowCards.CardCount(b) >= 3) &&
                        hands[MyIndex].CardCount(_trump) <= opponentTrumps.Count) //pokud mas hodne trumfu a propad si az sem, tak pravidlo nehraj (asi si mas odmazat plonk x)
                    {
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                i.Value >= Hodnota.Desitka &&
                                                                                hands[MyIndex].HasA(i.Suit) &&
                                                                                lowCards.CardCount(i.Suit) >= 3 &&
                                                                                _probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                                                _probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                                                opponentCards.CardCount(i.Suit) >= 2)
                                                                    .ToList();

                        return cardsToPlay.OrderByDescending(i => myInitialHand.CardCount(i.Suit))
                                          .ThenByDescending(i => i.Value)
                                          .FirstOrDefault();
                    }

                    //nehraj pokud mas dost trumfu i ostrych karet a k tomu neco nizkeho
                    if (((myInitialHand.CardCount(_trump) >= 5 ||
                         (TeamMateIndex != -1 &&
                          hands[MyIndex].CardCount(_trump) >= 4)) &&
                        hands[MyIndex].Count(i => i.Value == Hodnota.Eso) >= 2 &&
                        topCards.HasSuit(_trump)) &&
                        !(TeamMateIndex == -1 &&
                          (SevenValue >= GameValue &&    //neplati pro slabou sedmu (bez esa)
                           !topCards.HasSuit(_trump) &&
                           hands[MyIndex].CardCount(Hodnota.Eso) +
                           hands[MyIndex].Where(i => i.Suit != _trump)
                                         .CardCount(Hodnota.Desitka) >= 1)))
                    {
                        if ((_gameType & (Hra.Sedma | Hra.SedmaProti | Hra.Kilo)) == 0 &&
                            unwinnableLowCards.Any(i => i.Suit != _trump))
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
                        //pokud mam v barve A nebo A,X a celkem 2 az 5 karet, navic malo trumfu a nemuzu chtit vytlacit ze souperu nejake eso
                        //tak zkus stesti, pokud to nevyjde, stejne by uhrat nesly
                        if ((_gameType & Hra.Kilo) == 0 &&
                            (myInitialHand.CardCount(_trump) <= 4 ||
                             ((_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&//SevenValue >= GameValue &&
                              hands[MyIndex].CardCount(_trump) <= 5 &&
                              hands[MyIndex].CardCount(Hodnota.Eso) +
                              hands[MyIndex].Where(i => i.Suit != _trump)
                                            .CardCount(Hodnota.Desitka) >= 1) &&
                              !myInitialHand.HasA(_trump)) &&
                            !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Where(b => b != _trump)
                                 .Any(b => hands[MyIndex].HasX(b) &&
                                           (hands[MyIndex].HasK(b) ||
                                            (hands[MyIndex].HasQ(b) &&
                                             hands[MyIndex].HasJ(b))) &&
                                           opponentCards.HasA(b)))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                (myInitialHand.CardCount(i.Suit) >= 2 ||
                                                                                 (myInitialHand.HasA(i.Suit) &&
                                                                                  myInitialHand.HasX(i.Suit))) &&
                                                                                myInitialHand.CardCount(i.Suit) <= 5 &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  !opponentCards.HasA(i.Suit))) &&
                                                                                _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 &&
                                                                                (opponentCards.CardCount(i.Suit) >= 3 ||
                                                                                 (SevenValue >= GameValue &&
                                                                                  opponentCards.CardCount(i.Suit) >= 2)))
                                                                     .ToList();
                            if (cardsToPlay.Any(i => myInitialHand.HasA(i.Suit) &&
                                                     myInitialHand.HasX(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => myInitialHand.HasA(i.Suit) &&
                                                                     myInitialHand.HasX(i.Suit)).ToList();
                            }
                            if (!cardsToPlay.Any())
                            {
                                //pokud ma jeden ze souperu stejne trumfu nebo vic nez ja
                                //tak riskni A,X od delsi bocni barvy, pokud to nevyjde, stejne by uhrat nesly
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    myInitialHand.CardCount(i.Suit) >= 2 &&
                                                                                    myInitialHand.CardCount(i.Suit) <= 5 &&
                                                                                    (i.Value == Hodnota.Eso ||
                                                                                     (i.Value == Hodnota.Desitka &&
                                                                                      !opponentCards.HasA(i.Suit))) &&
                                                                                    ((_probabilities.CertainCards(player2).CardCount(_trump) >= 3 &&
                                                                                      _probabilities.CertainCards(player2).CardCount(_trump) >= hands[MyIndex].CardCount(_trump)) ||
                                                                                     (_probabilities.CertainCards(player3).CardCount(_trump) >= 3 &&
                                                                                      _probabilities.CertainCards(player3).CardCount(_trump) >= hands[MyIndex].CardCount(_trump))) &&
                                                                                    ((!_probabilities.PotentialCards(player2).HasSuit(_trump) &&
                                                                                      _probabilities.PotentialCards(player3).HasSuit(i.Suit)) ||
                                                                                     (!_probabilities.PotentialCards(player3).HasSuit(_trump) &&
                                                                                      _probabilities.PotentialCards(player2).HasSuit(i.Suit)) ||
                                                                                     (_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                                                      _probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                                                      opponentCards.CardCount(i.Suit) >= 2))).ToList();
                            }
                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.OrderBy(i => hands[MyIndex].HasX(i.Suit) ? 0 : 1)
                                                  .ThenByDescending(i => i.Value)
                                                  .ThenByDescending(i => opponentCards.CardCount(i.Suit))
                                                  .First();
                            }
                        }
                        //pokud mam malo trumfu a mam v barve A nebo A,X a souperi maji ve stejne barve max 3 resp. 4 karty,
                        //tak zkus stesti, pokud to nevyjde, stejne by uhrat nesly
                        if ((_gameType & Hra.Kilo) == 0 &&
                            //myInitialHand.CardCount(_trump) <= 3 &&
                            (myInitialHand.CardCount(_trump) <= 3 ||
                             (myInitialHand.CardCount(_trump) <= 5 &&
                              (_gameType & Hra.Sedma) != 0)) &&
                            //SevenValue > GameValue)) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => (_probabilities.SuitProbability(player2, b, RoundNumber) > 0 ||
                                           _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                          (_probabilities.SuitProbability(player3, b, RoundNumber) > 0 ||
                                           _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0)))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                i.Value >= Hodnota.Desitka &&
                                                                                (i.Value == Hodnota.Desitka ||
                                                                                 (hands[MyIndex].CardCount(i.Suit) > 1 ||
                                                                                  hands[MyIndex].All(j => j.Suit == _trump ||
                                                                                                          j.Value >= Hodnota.Desitka))) &&
                                                                                myInitialHand.CardCount(i.Suit) <= 5 &&
                                                                                !_probabilities.PotentialCards(player2).HasA(i.Suit) &&
                                                                                !_probabilities.PotentialCards(player3).HasA(i.Suit) &&
                                                                                ((!_probabilities.PotentialCards(player2).HasSuit(_trump) &&
                                                                                  !_probabilities.PotentialCards(player3).HasSuit(_trump)) ||
                                                                                 (!_probabilities.PotentialCards(player2).HasSuit(_trump) &&
                                                                                  _probabilities.PotentialCards(player3).HasSuit(i.Suit)) ||
                                                                                 (!_probabilities.PotentialCards(player3).HasSuit(_trump) &&
                                                                                  _probabilities.PotentialCards(player2).HasSuit(i.Suit)) ||
                                                                                 (_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                                                  _probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                                                  opponentCards.CardCount(i.Suit) >= 2)))
                                                                    .ToList();
                            //pokud to jde pokracuj v puvodni barve od esa desitkou
                            if (cardsToPlay.Any(i => i.Value == Hodnota.Desitka &&
                                                     !hands[MyIndex].HasA(i.Suit) &&
                                                     myInitialHand.HasA(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => i.Value == Hodnota.Desitka &&
                                                                     !hands[MyIndex].HasA(i.Suit) &&
                                                                     myInitialHand.HasA(i.Suit))
                                                         .ToList();
                            }
                            //uprednostni barvu kde mam A i X
                            if (cardsToPlay.Any(i => i.Value == Hodnota.Eso &&
                                                     cardsToPlay.HasX(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => i.Value == Hodnota.Eso &&
                                                                     cardsToPlay.HasX(i.Suit))
                                                         .ToList();
                            }
                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.OrderBy(i => hands[MyIndex].HasX(i.Suit) ? 0 : 1)
                                                  .ThenByDescending(i => i.Value)
                                                  .FirstOrDefault();
                            }
                        }
                        //nehraj pokud mas plonkovou barvu (neplati pri sedme)
                        if (GameValue > SevenValue &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Any(b => b != _trump &&
                                          hands[MyIndex].HasSuit(b) &&
                                          !myInitialHand.HasA(b) &&
                                          !myInitialHand.HasX(b)))
                        {
                            return null;
                        }
                        //nehraj pokud mam dost nejvyssich trumfu a muzu je ze souperu vytahnout
                        //nebo pokud mam dost trumfu celkem a k tomu barvu kde nemam nejvyssi kartu
                        if (holes.Count > 0 &&
                            (topTrumps.Count > 0 ||
                             hands[MyIndex].CardCount(_trump) >= 6) &&
                            (topTrumps.Count >= holes.Count ||
                             (hands[MyIndex].CardCount(_trump) >= holes.Count &&
                              (_gameType & (Hra.Sedma | Hra.SedmaProti | Hra.Kilo)) == 0 &&
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
                                                                            myInitialHand.CardCount(i.Suit) <= 5 &&
                                                                            ((i.Value == Hodnota.Eso &&
                                                                              (hands[MyIndex].CardCount(i.Suit) > 1 ||
                                                                               hands[MyIndex].All(j => j.Suit == _trump ||
                                                                                                       j.Value >= Hodnota.Desitka)) &&
                                                                              ((_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) == 0 &&
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) == 0) ||
                                                                               (myInitialHand.CardCount(i.Suit) >= 4 &&
                                                                                myInitialHand.CardCount(_trump) <= 3) ||
                                                                               (RoundNumber >= 7 &&
                                                                                hands[MyIndex].HasA(_trump) &&
                                                                                hands[MyIndex].Has7(_trump)))) ||
                                                                             (i.Value == Hodnota.Desitka &&
                                                                              _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                              _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) <= _epsilon)) &&
                                                                            (((_gameType & Hra.Hra) != 0 && //pokud hrajes slabou hru nebo sedmu (na malo trumfu) tak riskuj vic
                                                                              hands[MyIndex].CardCount(_trump) <= Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                                                      .Select(h => new Card(_trump, h))
                                                                                                                      .Count(j => _probabilities.PotentialCards(player2).Contains(j) ||
                                                                                                                                  _probabilities.PotentialCards(player3).Contains(j)) &&
                                                                              (myInitialHand.CardCount(_trump) <= 2 ||
                                                                               (myInitialHand.CardCount(_trump) <= 3 &&
                                                                                !myInitialHand.HasA(_trump)) ||
                                                                               (myInitialHand.CardCount(_trump) <= 4 &&
                                                                                (_gameType & Hra.Sedma) != 0 &&
                                                                                !myInitialHand.HasA(_trump))) &&
                                                                              _probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                                              _probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                                              opponentCards.CardCount(i.Suit) >= 2) || //jinak se rid risk faktorem
                                                                             (((_probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor &&
                                                                                !_probabilities.UnlikelyCards(player2).HasSuit(i.Suit)) ||
                                                                               _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                              ((_probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - RiskFactor &&
                                                                                !_probabilities.UnlikelyCards(player3).HasSuit(i.Suit)) ||
                                                                               _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                              (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0 ||
                                                                               _probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0 ||
                                                                               opponentCards.CardCount(i.Suit) > 1)))
                                                                          //                                                             (myInitialHand.CardCount(i.Suit) <= 2 ||
                                                                          //                                                              (myInitialHand.CardCount(i.Suit) <= 3 &&
                                                                          //                                                               myInitialHand.CardCount(_trump) <= 3) ||
                                                                          //  (myInitialHand.CardCount(i.Suit) <= 4 &&
                                                                          //myInitialHand.CardCount(_trump) <= 2))
                                                                          )
                                                                    .ToList();

                        return cardsToPlay.OrderByDescending(i => myInitialHand.CardCount(i.Suit))
                                          .ThenBy(i => hands[MyIndex].HasX(i.Suit) ? 0 : 1)
                                          .ThenByDescending(i => i.Value)
                                          .FirstOrDefault();
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //co-
                        var actorSuits = _rounds.Where(r => r != null &&
                                                            r.player1.PlayerIndex == opponent)
                                                .Select(r => r.c1.Suit)
                                                .Distinct().ToList();
                        //pokud mam hodne trumfu, tak je zbytecne riskovat a mel bych hrat bodovanou kartu
                        //az pote co ze soupere vytlacim vsechny trumfy
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                !_probabilities.CertainCards(Game.TalonIndex).HasX(i.Suit) &&
                                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 &&
                                                                                myInitialHand.CardCount(_trump) <= 3 &&
                                                                                ((i.Value == Hodnota.Eso &&
                                                                                  _probabilities.MaxCardCount(player3, i.Suit) >= 3 &&
                                                                                  (_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) == 0 ||//<= _epsilon ||
                                                                                   ((_gameType & (Hra.Kilo | Hra.KiloProti | Hra.SedmaProti)) != 0 &&
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon))) ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) <= _epsilon)) &&
                                                                                ((_probabilities.CertainCards(player3).Any(j => j.Suit == i.Suit) ||
                                                                                  _probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1 ||
                                                                                  _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) ||
                                                                                 (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 &&
                                                                                  hands[MyIndex].CardCount(_trump) > 2 &&
                                                                                  myInitialHand.CardCount(_trump) <= 4) ||
                                                                                 (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor &&
                                                                                  (hands[MyIndex].CardCount(_trump) > 1 ||
                                                                                   (hands[MyIndex].CardCount(i.Suit) <= 5 &&
                                                                                    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                        .Where(b => b != _trump &&
                                                                                                    hands[MyIndex].HasSuit(b))
                                                                                        .All(b => hands[MyIndex].HasA(b) ||
                                                                                                  hands[MyIndex].HasX(b)))) &&
                                                                                   !(hands[MyIndex].CardCount(_trump) <= 2 &&
                                                                                     Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                         .Where(b => b != _trump)
                                                                                         .Any(b => !hands[MyIndex].HasSuit(b) &&
                                                                                                   _probabilities.SuitProbability(TeamMateIndex, b, RoundNumber) >= 1 - RiskFactor)))) &&
                                                                                !(_probabilities.LikelyCards(player3).HasSuit(i.Suit) ||
                                                                                  (!_probabilities.PotentialCards(TeamMateIndex).HasSuit(i.Suit) &&
                                                                                   _probabilities.PotentialCards(player3).CardCount(i.Suit) > 2)))
                                                                    .ToList();
                        cardsToPlay = cardsToPlay.Where(i => !(_probabilities.CertainCards(player3).Any(j => j.Suit == i.Suit &&      //nehraj pokud ma akter jiste nizke karty v barve
                                                                                                             j.Value < i.Value) &&
                                                               _probabilities.PotentialCards(player3).HasSuit(_trump) &&             //a navic jeste trumf
                                                               hands[MyIndex].Any(j => j.Suit != _trump &&                          //a lze hrat neco jineho
                                                                                        j.Suit != i.Suit &&
                                                                                        !_bannedSuits.Contains(j.Suit) &&
                                                                                        j.Value < Hodnota.Desitka))).ToList();
                        if (cardsToPlay.Any(i => !actorSuits.Contains(i.Suit)))
                        {
                            cardsToPlay = cardsToPlay.Where(i => !actorSuits.Contains(i.Suit)).ToList();
                        }

                        //pokud mam v barve A nebo A,X a celkem 4 az 5 karet, navic malo trumfu a nemuzu chtit vytlacit ze souperu nejake eso
                        //tak zkus stesti, pokud to nevyjde, stejne by uhrat nesly
                        if (!cardsToPlay.Any() &&
                            (_gameType & Hra.Kilo) == 0 &&
                            (myInitialHand.CardCount(_trump) <= 4 ||
                             ((_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&//SevenValue >= GameValue &&
                              hands[MyIndex].CardCount(_trump) <= 5 &&
                              hands[MyIndex].CardCount(Hodnota.Eso) +
                              hands[MyIndex].Where(i => i.Suit != _trump)
                                            .CardCount(Hodnota.Desitka) >= 1) &&
                              !myInitialHand.HasA(_trump)) &&
                            !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Where(b => b != _trump)
                                 .Any(b => hands[MyIndex].HasX(b) &&
                                           hands[MyIndex].CardCount(b) > 1 &&
                                           _probabilities.PotentialCards(player3).HasA(b)))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !actorSuits.Contains(i.Suit) &&
                                                                                myInitialHand.CardCount(i.Suit) >= 3 &&
                                                                                myInitialHand.CardCount(i.Suit) <= 5 &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  !_probabilities.PotentialCards(player3).HasA(i.Suit))) &&
                                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 &&
                                                                                (_probabilities.PotentialCards(player3).CardCount(i.Suit) >= 3 ||
                                                                                 (SevenValue >= GameValue &&
                                                                                  _probabilities.PotentialCards(player3).CardCount(i.Suit) >= 2)) &&
                                                                                !(_probabilities.LikelyCards(player3).HasSuit(i.Suit) ||
                                                                                  (!_probabilities.PotentialCards(TeamMateIndex).HasSuit(i.Suit) &&
                                                                                   _probabilities.PotentialCards(player3).CardCount(i.Suit) > 2)))
                                                                     .ToList();
                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.OrderBy(i => hands[MyIndex].HasX(i.Suit) ? 0 : 1)
                                                  .ThenByDescending(i => i.Value)
                                                  .First();
                            }
                        }
                        if (!cardsToPlay.Any() &&
                            (_gameType & Hra.Kilo) != 0 &&
                            hands[MyIndex].Count(i => i.Value >= Hodnota.Desitka &&
                                                        i.Suit != _trump) >= 2 &&
                            (hands[MyIndex].SuitCount == Game.NumSuits ||
                                (hands[MyIndex].SuitCount == 3 &&
                                !hands[MyIndex].HasSuit(_trump))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 &&
                                                                                !(_probabilities.LikelyCards(player3).HasSuit(i.Suit) ||
                                                                                  (!_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                                                   _probabilities.PotentialCards(player3).CardCount(i.Suit) > 2)) &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0)))
                                                                    .ToList();
                        }

                        if (!cardsToPlay.Any() &&
                            hands[MyIndex].Count(i => i.Value >= Hodnota.Desitka &&
                                                      i.Suit != _trump) >= 2 &&
                            (hands[MyIndex].SuitCount == Game.NumSuits ||
                             (hands[MyIndex].SuitCount == 3 &&
                              !hands[MyIndex].HasSuit(_trump)) ||
                             (hands[MyIndex].HasSuit(_trump) &&
                              hands[MyIndex].CardCount(_trump) <= 3 &&
                              !hands[MyIndex].HasA(_trump) &&
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => hands[MyIndex].HasSuit(b))
                                  .All(b => hands[MyIndex].HasA(b) ||
                                            hands[MyIndex].HasX(b)))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 &&
                                                                                !(_probabilities.LikelyCards(player3).HasSuit(i.Suit) ||
                                                                                  (!_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                                                   _probabilities.PotentialCards(player3).CardCount(i.Suit) > 2)) &&
                                                                                ((i.Value == Hodnota.Eso &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) == 0) ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0)))
                                                                    .ToList();
                        }


                        //pokud jsem mel na zacatku malo trumfu a hodne desitek a es, zkus neco uhrat
                        if (!cardsToPlay.Any() &&
                            hands[MyIndex].HasSuit(_trump) &&
                            myInitialHand.CardCount(_trump) <= 3 &&
                            !((_gameType & Hra.Kilo) != 0 &&
                              myInitialHand.CardCount(_trump) <= 2) &&
                            (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Where(b => b != _trump &&
                                             hands[MyIndex].HasSuit(b))
                                 .All(b => _probabilities.CardProbability(player3, new Card(b, Hodnota.Eso)) == 0) ||
                             (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => b != _trump &&
                                              hands[MyIndex].HasSuit(b))
                                  .All(b => hands[MyIndex].HasA(b)) &&
                              hands[MyIndex].SuitCount > 2) ||
                             Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Where(b => b != _trump)
                                 .Sum(b => (myInitialHand.HasA(b) ? 1 : 0) + (myInitialHand.HasX(b) ? 1 : 0)) >= 3))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) <= _epsilon)) &&
                                                                                (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                                 ((_gameType & Hra.Kilo) != 0 &&
                                                                                  _probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1)) &&
                                                                                !((_probabilities.CertainCards(player3).Any(j => j.Suit == i.Suit &&      //nehraj pokud ma akter jiste nizke karty v barve
                                                                                                                                j.Value < i.Value) ||
                                                                                   (!_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                                                    _probabilities.PotentialCards(player3).CardCount(i.Suit) > 2)) &&
                                                                                  _probabilities.PotentialCards(player3).HasSuit(_trump) &&             //a navic jeste trumf
                                                                                  hands[MyIndex].Any(j => j.Suit != _trump &&                          //a lze hrat neco jineho
                                                                                                           j.Suit != i.Suit &&
                                                                                                           !_bannedSuits.Contains(j.Suit) &&
                                                                                                           j.Value < Hodnota.Desitka)))
                                                                    .ToList();

                            if (cardsToPlay.Any(i => !actorSuits.Contains(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => !actorSuits.Contains(i.Suit)).ToList();
                            }
                            return cardsToPlay.OrderBy(i => myInitialHand.CardCount(i.Suit))
                                              .ThenBy(i => hands[MyIndex].HasX(i.Suit) ? 0 : 1)
                                              .ThenByDescending(i => i.Value)
                                              .FirstOrDefault();
                        }
                        if (cardsToPlay.Any(i => !actorSuits.Contains(i.Suit)))
                        {
                            cardsToPlay = cardsToPlay.Where(i => !actorSuits.Contains(i.Suit)).ToList();
                        }
                        return cardsToPlay.OrderByDescending(i => myInitialHand.CardCount(i.Suit))
                                          .ThenBy(i => hands[MyIndex].HasX(i.Suit) ? 0 : 1)
                                          .ThenByDescending(i => i.Value)
                                          .FirstOrDefault();
                    }
                    else if (TeamMateIndex == player3)
                    {
                        //c-o
                        var actorSuits = _rounds.Where(r => r != null &&
                                                            r.player1.PlayerIndex == opponent)
                                                .Select(r => r.c1.Suit)
                                                .Distinct().ToList();
                        //pokud mam hodne trumfu, tak je zbytecne riskovat a mel bych hrat bodovnou kartu
                        //az pote co ze soupere vytlacim vsechny trumfy
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                !_probabilities.CertainCards(Game.TalonIndex).HasX(i.Suit) &&
                                                                                _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                                                myInitialHand.CardCount(_trump) <= 3 &&
                                                                                ((i.Value == Hodnota.Eso &&
                                                                                  (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) == 0 ||
                                                                                   ((_gameType & (Hra.Kilo | Hra.KiloProti | Hra.SedmaProti)) != 0 &&
                                                                                    _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon))) ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) <= _epsilon)) &&
                                                                                (//(_probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor &&
                                                                                 // hands[MyIndex].CardCount(_trump) < _probabilities.MaxCardCount(player2, _trump)) ||
                                                                                 (_probabilities.CertainCards(player2).Any(j => j.Suit == i.Suit) ||
                                                                                  _probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 1 ||
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
                        cardsToPlay = cardsToPlay.Where(i => !(_probabilities.CertainCards(player2).Any(j => j.Suit == i.Suit &&      //nehraj pokud ma akter jiste nizke karty v barve
                                                                                                             j.Value < i.Value) &&
                                                               _probabilities.PotentialCards(player2).HasSuit(_trump) &&             //a navic jeste trumf
                                                               hands[MyIndex].Any(j => j.Suit != _trump &&                          //a lze hrat neco jineho
                                                                                        j.Suit != i.Suit &&
                                                                                        !_bannedSuits.Contains(j.Suit) &&
                                                                                        j.Value < Hodnota.Desitka))).ToList();

                        if (cardsToPlay.Any(i => !actorSuits.Contains(i.Suit)))
                        {
                            cardsToPlay = cardsToPlay.Where(i => !actorSuits.Contains(i.Suit)).ToList();
                        }

                        //pokud mam v barve A nebo A,X a celkem 4 az 5 karet, navic malo trumfu a nemuzu chtit vytlacit ze souperu nejake eso
                        //tak zkus stesti, pokud to nevyjde, stejne by uhrat nesly
                        if (!cardsToPlay.Any() &&
                            (_gameType & Hra.Kilo) == 0 &&
                            (myInitialHand.CardCount(_trump) <= 4 ||
                             ((_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&//SevenValue >= GameValue &&
                              hands[MyIndex].CardCount(_trump) <= 5 &&
                              hands[MyIndex].CardCount(Hodnota.Eso) +
                              hands[MyIndex].Where(i => i.Suit != _trump)
                                            .CardCount(Hodnota.Desitka) >= 1) &&
                              !myInitialHand.HasA(_trump)) &&
                            !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Where(b => b != _trump)
                                 .Any(b => hands[MyIndex].HasX(b) &&
                                           hands[MyIndex].CardCount(b) > 1 &&
                                           _probabilities.PotentialCards(player2).HasA(b)))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !actorSuits.Contains(i.Suit) &&
                                                                                myInitialHand.CardCount(i.Suit) >= 3 &&
                                                                                myInitialHand.CardCount(i.Suit) <= 5 &&
                                                                                !(Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                      .Where(b => b != _trump &&
                                                                                                  hands[MyIndex].HasSuit(b))
                                                                                      .Any(b => !hands[MyIndex].HasA(b) &&
                                                                                                !hands[MyIndex].HasX(b) &&
                                                                                                _probabilities.PotentialCards(TeamMateIndex).HasSuit(b))) &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  !_probabilities.PotentialCards(player2).HasA(i.Suit))) &&
                                                                                _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                                                (_probabilities.PotentialCards(player2).CardCount(i.Suit) >= 3 ||
                                                                                 (SevenValue >= GameValue &&
                                                                                  _probabilities.PotentialCards(player2).CardCount(i.Suit) >= 2)) &&
                                                                                !(_probabilities.LikelyCards(player2).HasSuit(i.Suit) ||
                                                                                  (!_probabilities.PotentialCards(TeamMateIndex).HasSuit(i.Suit) &&
                                                                                   _probabilities.PotentialCards(player2).CardCount(i.Suit) > 2)))
                                                                     .ToList();
                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.OrderBy(i => hands[MyIndex].HasX(i.Suit) ? 0 : 1)
                                                  .ThenByDescending(i => i.Value)
                                                  .First();
                            }
                        }
                        if (!cardsToPlay.Any() &&
                            (_gameType & Hra.Kilo) != 0 &&
                            hands[MyIndex].Count(i => i.Value >= Hodnota.Desitka &&
                                                      i.Suit != _trump) >= 2 &&
                            (hands[MyIndex].SuitCount == Game.NumSuits ||
                             (hands[MyIndex].SuitCount == 3 &&
                              !hands[MyIndex].HasSuit(_trump))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                                                !(_probabilities.LikelyCards(player2).HasSuit(i.Suit) ||
                                                                                  (!_probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                                                   _probabilities.PotentialCards(player2).CardCount(i.Suit) > 2)) &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) == 0)))
                                                                    .ToList();
                        }

                        if (!cardsToPlay.Any() &&
                            hands[MyIndex].Count(i => i.Value >= Hodnota.Desitka &&
                                                      i.Suit != _trump) >= 2 &&
                            (hands[MyIndex].SuitCount == Game.NumSuits ||
                             (hands[MyIndex].SuitCount == 3 &&
                              !hands[MyIndex].HasSuit(_trump)) ||
                             (hands[MyIndex].HasSuit(_trump) &&
                              hands[MyIndex].CardCount(_trump) <= 3 &&
                              !hands[MyIndex].HasA(_trump) &&
                              Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => hands[MyIndex].HasSuit(b))
                                  .All(b => hands[MyIndex].HasA(b) ||
                                            hands[MyIndex].HasX(b)))))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 &&
                                                                                !(_probabilities.LikelyCards(player2).HasSuit(i.Suit) ||
                                                                                  (!_probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                                                   _probabilities.PotentialCards(player2).CardCount(i.Suit) > 2)) &&
                                                                                ((i.Value == Hodnota.Eso &&
                                                                                  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) == 0) ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) == 0)))
                                                                    .ToList();
                        }

                        //pokud jsem mel na zacatku malo trumfu a hodne desitek a es, zkus neco uhrat
                        if (!cardsToPlay.Any() &&
                            hands[MyIndex].HasSuit(_trump) &&
                            myInitialHand.CardCount(_trump) <= 3 &&
                            !((_gameType & Hra.Kilo) != 0 &&
                              myInitialHand.CardCount(_trump) <= 2) &&
                            (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Where(b => b != _trump &&
                                             hands[MyIndex].HasSuit(b))
                                 .All(b => _probabilities.CardProbability(player2, new Card(b, Hodnota.Eso)) == 0) ||
                             (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                  .Where(b => b != _trump &&
                                              hands[MyIndex].HasSuit(b))
                                  .All(b => hands[MyIndex].HasA(b)) &&
                              hands[MyIndex].SuitCount > 2) ||
                             Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Where(b => b != _trump)
                                 .Sum(b => (myInitialHand.HasA(b) ? 1 : 0) + (myInitialHand.HasX(b) ? 1 : 0)) >= 3))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) <= _epsilon)) &&
                                                                                (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                                 ((_gameType & Hra.Kilo) != 0 &&
                                                                                  _probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 1)) &&
                                                                                !((_probabilities.CertainCards(player2).Any(j => j.Suit == i.Suit &&      //nehraj pokud ma akter jiste nizke karty v barve
                                                                                                                                 j.Value < i.Value) ||
                                                                                   (!_probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                                                    _probabilities.PotentialCards(player2).CardCount(i.Suit) > 2)) &&
                                                                                  _probabilities.PotentialCards(player2).HasSuit(_trump) &&             //a navic jeste trumf
                                                                                  hands[MyIndex].Any(j => j.Suit != _trump &&                          //a lze hrat neco jineho
                                                                                                           j.Suit != i.Suit &&
                                                                                                           !_bannedSuits.Contains(j.Suit) &&
                                                                                                           j.Value < Hodnota.Desitka)))
                                                                    .ToList();

                            if (cardsToPlay.Any(i => !actorSuits.Contains(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => !actorSuits.Contains(i.Suit)).ToList();
                            }
                            return cardsToPlay.OrderBy(i => myInitialHand.CardCount(i.Suit))
                                              .ThenBy(i => hands[MyIndex].HasX(i.Suit) ? 0 : 1)
                                              .ThenByDescending(i => i.Value)
                                              .FirstOrDefault();
                        }
                        if (cardsToPlay.Any(i => !actorSuits.Contains(i.Suit)))
                        {
                            cardsToPlay = cardsToPlay.Where(i => !actorSuits.Contains(i.Suit)).ToList();
                        }
                        return cardsToPlay.OrderByDescending(i => myInitialHand.CardCount(i.Suit))
                                          .ThenBy(i => hands[MyIndex].HasX(i.Suit) ? 0 : 1)
                                          .ThenByDescending(i => i.Value)
                                          .FirstOrDefault();
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 10,
                Description = "vytlačit bodovanou kartu",
                SkipSimulations = true,
                #region ChooseCard1 Rule10
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

                    //pokud muze mit kolega dost karet v barve tak zkus vytlacit eso i pri sedme proti
                    if (TeamMateIndex == player3)
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            i.Value < Hodnota.Desitka &&
                                                                            !hands[MyIndex].HasA(i.Suit) &&
                                                                            hands[MyIndex].HasX(i.Suit) &&
                                                                            hands[MyIndex].HasK(i.Suit) &&
                                                                            _probabilities.PotentialCards(player2).HasA(i.Suit) &&
                                                                            _probabilities.PotentialCards(player3).CardCount(i.Suit) >= 4)
                                                                .ToList();

                    }
                    //toto pravidlo by melo ze soupere vytlacit bodovanou kartu kterou muj spoluhrac sebere trumfem
                    //prilis prisne podminky budou znamenat, ze se pravidlo skoro nikdy neuplatni
                    //prilis benevolentni podminky zase, ze se pravidlo zahraje i kdyz by nemelo
                    //(napr. hraju X, souper prebije A, a kolegovi zbyde jedna barva na ruce, takze nemuze prebit trumfem)
                    if (!cardsToPlay.Any() &&
                        (_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 ||
                        (_gameType & Hra.Kilo) != 0)
                    {
                        //nehraj pravidlo pokud si muzes odmazat trumf
                        //a existuje barva kterou kolega asi dobira
                        //a mas co mazat
                        if (TeamMateIndex != -1 &&
                            hands[MyIndex].CardCount(_trump) == 1 &&
                            !hands[MyIndex].HasX(_trump) &&
                            !topTrumps.Any() &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            !hands[MyIndex].HasSuit(b))
                                .Any(b => teamMatePlayedWinningAXNonTrumpCards.HasSuit(b) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b))
                                .Any(b => (hands[MyIndex].HasA(b) ||
                                           hands[MyIndex].HasX(b)) &&
                                          !_probabilities.CertainCards(opponent).HasSuit(b))))
                        {
                            return null;
                        }
                        if (TeamMateIndex == player2)
                        {
                            //co-
                            if (RoundNumber == 9 &&
                                ValidCards(hands[MyIndex]).Contains(new Card(_trump, Hodnota.Sedma)) &&
                                _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0)
                            {
                                return null;
                            }
                            //if (PlayerBids[TeamMateIndex] == 0)
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !hands[MyIndex].HasA(i.Suit) && //pokud mas eso tak si pockej na akterovu desitku a pravidlo nehraj
                                                                                (((_probabilities.CertainCards(player3).HasA(i.Suit) ||
                                                                                   _probabilities.CertainCards(player3).HasX(i.Suit)) &&
                                                                                  _probabilities.PotentialCards(player3).CardCount(i.Suit) <= 2 &&
                                                                                  //_probabilities.HasAOrXAndNothingElse(player3, i.Suit, RoundNumber) >= 1 - RiskFactor &&
                                                                                  !_probabilities.LikelyCards(player3).Any(j => j.Suit == i.Suit &&   //pokud ma souper nizke karty v barve, tak pravidlo nehraj
                                                                                                                                j.Value < Hodnota.Desitka) &&
                                                                                  _probabilities.PotentialCards(player3).Count(j => j.Suit == i.Suit &&
                                                                                                                                    j.Value < Hodnota.Desitka) < 2 &&
                                                                                  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                                  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon) ||
                                                                                 _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                                (_probabilities.SuitProbability(player2, _trump, RoundNumber) >= 1 - RiskFactor ||
                                                                                 (_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 &&
                                                                                  _probabilities.SuitProbability(player2, i.Suit, RoundNumber) <= RiskFactor))).ToList();
                            }
                            if (!cardsToPlay.Any() &&
                                (_gameType & Hra.Kilo) != 0)
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    !hands[MyIndex].HasA(i.Suit) && //pokud mas eso tak si pockej na akterovu desitku a pravidlo nehraj
                                                                                                                    //_probabilities.HasAOrXAndNothingElse(player3, i.Suit, RoundNumber) >= 0.5f &&
                                                                                    _probabilities.PotentialCards(player3).CardCount(i.Suit) == 1 &&
                                                                                    _probabilities.LikelyCards(player3).Any(j => j.Suit == i.Suit &&
                                                                                                                                 j.Value >= Hodnota.Desitka) &&
                                                                                    !_probabilities.LikelyCards(player3).Any(j => j.Suit == i.Suit &&   //pokud ma souper nizke karty v barve, tak pravidlo nehraj
                                                                                                                                  j.Value < Hodnota.Desitka) &&
                                                                                    _probabilities.PotentialCards(player3).Count(j => j.Suit == i.Suit &&
                                                                                                                                    j.Value < Hodnota.Desitka) < 2 &&
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
                            //if (PlayerBids[TeamMateIndex] == 0)
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    !hands[MyIndex].HasA(i.Suit) && //pokud mas eso tak si pockej na akterovu desitku a pravidlo nehraj
                                                                                    (((_probabilities.CertainCards(player2).HasA(i.Suit) ||
                                                                                       _probabilities.CertainCards(player2).HasX(i.Suit)) &&
                                                                                      _probabilities.PotentialCards(player2).CardCount(i.Suit) <= 2 &&
                                                                                      //_probabilities.HasAOrXAndNothingElse(player2, i.Suit, RoundNumber) >= 1 - RiskFactor &&
                                                                                      _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                                      _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon) ||
                                                                                     _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                                    _probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor &&
                                                                                    _probabilities.SuitProbability(player3, i.Suit, RoundNumber) <= RiskFactor).ToList();
                                if (!cardsToPlay.Any())
                                {
                                    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                        i.Value < Hodnota.Desitka &&
                                                                                        !hands[MyIndex].HasA(i.Suit) && //pokud mas eso tak si pockej na akterovu desitku a pravidlo nehraj
                                                                                        _probabilities.PotentialCards(player2).HasX(i.Suit) &&
                                                                                        !_probabilities.PotentialCards(player2).Any(j => j.Suit == i.Suit &&
                                                                                                                                         j.Value > i.Value &&
                                                                                                                                         j.Value < Hodnota.Desitka) &&
                                                                                        (_probabilities.PotentialCards(player3).HasA(i.Suit) ||
                                                                                         (_probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor &&
                                                                                          _probabilities.SuitProbability(player3, i.Suit, RoundNumber) <= RiskFactor))).ToList();
                                }
                                if (!cardsToPlay.Any())
                                {
                                    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                        i.Value < Hodnota.Desitka &&
                                                                                        !hands[MyIndex].HasA(i.Suit) && //pokud mas eso tak si pockej na akterovu desitku a pravidlo nehraj
                                                                                        (_probabilities.PotentialCards(player2).HasA(i.Suit) ||
                                                                                         _probabilities.PotentialCards(player2).HasX(i.Suit)) &&
                                                                                        !_probabilities.PotentialCards(player2).Any(j => j.Suit == i.Suit &&
                                                                                                                                         j.Value > i.Value &&
                                                                                                                                         j.Value < Hodnota.Desitka) &&
                                                                                        (_probabilities.PotentialCards(player3).HasA(i.Suit) ||
                                                                                         (_probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor &&
                                                                                          _probabilities.SuitProbability(player3, i.Suit, RoundNumber) <= RiskFactor))).ToList();
                                }
                            }
                            if (!cardsToPlay.Any() &&
                                (_gameType & Hra.Kilo) != 0)
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    !hands[MyIndex].HasA(i.Suit) && //pokud mas eso tak si pockej na akterovu desitku a pravidlo nehraj
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
                                                                                !hands[MyIndex].HasA(i.Suit) && //pokud mas eso tak si pockej na akterovu desitku a pravidlo nehraj
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
                Order = 11,
                Description = "bodovat nebo vytlačit trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule11
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
                          SevenValue >= GameValue) ||
                         (_gameType & Hra.SedmaProti) != 0) &&
                        (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0)
                    {
                        //c--
                        //pri sedme zkousej nejprve vytlacit trumf dllouhou bocni barvou
                        if (SevenValue > GameValue &&
                            //(_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&
                            //(_gameType & (Hra.Kilo | Hra.KiloProti)) == 0 &&
                            myInitialHand.CardCount(_trump) <= 5 &&
                            hands[MyIndex].Count(i => i.Suit != _trump &&
                                                      i.Value >= Hodnota.Kral) >= 1 &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => hands[MyIndex].CardCount(b) + hands[Game.TalonIndex].CardCount(b) >= 4 &&
                                          myInitialHand.HasA(b)))
                        {
                            return null;
                        }
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
                        //         .Any(b => hands[MyIndex].HasX(b) &&
                        //                   hands[MyIndex].CardCount(b) > 1 &&
                        //                   opponentPotentialCards.HasA(b)))
                        //{
                        //    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
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
                                                                                ((i.Value == Hodnota.Eso &&
                                                                                  (hands[MyIndex].CardCount(i.Suit) > 1 ||
                                                                                   hands[MyIndex].All(j => j.Suit == _trump ||
                                                                                                           j.Value >= Hodnota.Desitka))) ||
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
                        if (unwinnableLowCards.Any(i => !cardsToPlay.Any(j => j.Suit == i.Suit)) &&
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
                             SevenValue >= GameValue &&
                             (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0)
                    {
                        //co-
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
                            unwinnableLowCards.Any(i => !cardsToPlay.Any(j => j.Suit == i.Suit)))
                        {
                            return null;
                        }
                        return cardsToPlay.FirstOrDefault();

                    }
                    else if (TeamMateIndex == player3 &&
                             SevenValue >= GameValue &&
                             (_gameType & (Hra.Kilo | Hra.KiloProti)) == 0)
                    {
                        //c-o
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
                            unwinnableLowCards.Any(i => !cardsToPlay.Any(j => j.Suit == i.Suit)))
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
                Order = 12,
                Description = "odmazat si barvu",
                SkipSimulations = true,
                #region ChooseCard1 Rule12
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
                    if (TeamMateIndex == -1 &&
                        hands[MyIndex].Any(i => i.Value >= Hodnota.Desitka &&
                                                i.Suit != _trump &&
                                                opponentTrumps.Count == 0 &&
                                                !_probabilities.PotentialCards(player2).HasA(i.Suit) &&
                                                !_probabilities.PotentialCards(player3).HasA(i.Suit) &&
                                                !_probabilities.PotentialCards(player2).HasX(i.Suit) &&
                                                !_probabilities.PotentialCards(player3).HasX(i.Suit)))
                    {
                        return null;
                    }
                    if (TeamMateIndex != -1 &&
                        hands[MyIndex].Any(i => i.Value >= Hodnota.Desitka &&
                                                i.Suit != _trump &&
                                                opponentTrumps.Count == 0 &&
                                                !_probabilities.PotentialCards(opponent).HasA(i.Suit) &&
                                                !_probabilities.PotentialCards(opponent).HasX(i.Suit)))
                    {
                        return null;
                    }
                    if (TeamMateIndex == -1 &&
                        hands[MyIndex].Any(i => i.Suit != _trump &&
                                                opponentTrumps.Count == 0 &&
                                                !_probabilities.PotentialCards(player2).HasHigherCardThan(i) &&
                                                !_probabilities.PotentialCards(player3).HasHigherCardThan(i)))
                    {
                        return null;
                    }
                    if (TeamMateIndex != -1 &&
                        hands[MyIndex].Any(i => i.Suit != _trump &&
                                                opponentTrumps.Count == 0 &&
                                                !_probabilities.PotentialCards(opponent).HasHigherCardThan(i)))
                    {
                        return null;
                    }
                    if (TeamMateIndex != -1 &&
                        SevenValue >= GameValue &&                           //pri sedme se nezbavuj plonka (abych si udrzel trumfy)
                        hands[MyIndex].HasSuit(_trump) &&
                        hands[MyIndex].Any(i => i.Suit != _trump &&         //pokud muzes uhrat nejake eso
                                                i.Value == Hodnota.Eso &&
                                                _probabilities.PotentialCards(opponent).HasSuit(i.Suit)))
                    {
                        return null;
                    }
                    if (TeamMateIndex != -1 &&
                        ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 ||
                         (_gameType & (Hra.Kilo | Hra.KiloProti)) != 0) &&
                        hands[MyIndex].CardCount(_trump) == 1 &&
                        !hands[MyIndex].HasX(_trump) &&
                        !topTrumps.Any() &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump &&
                                        !hands[MyIndex].HasSuit(b))
                            .Any(b => teamMatePlayedWinningAXNonTrumpCards.HasSuit(b)) &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump &&
                                        hands[MyIndex].HasSuit(b))
                            .Any(b => (hands[MyIndex].HasA(b) ||
                                       hands[MyIndex].HasX(b)) &&
                                      !_probabilities.CertainCards(opponent).HasSuit(b)))
                    {
                        var cardToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                               (!_probabilities.PotentialCards(TeamMateIndex).Any(j => j.Suit == _trump &&
                                                                                                                                       j.Value >= Hodnota.Desitka) ||
                                                                                _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor))
                                                                   .FirstOrDefault();
                        if (cardToPlay != null)
                        {
                            return cardToPlay;
                        }
                    }
                    //pokud hrajes proti kilu, tak se zbav trumfu, pokud mas v ostatnich barvach ostre karty
                    if (TeamMateIndex != -1 &&
                        PlayerBids[MyIndex] == 0 &&
                        (_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 &&
                        hands[MyIndex].HasSuit(_trump) &&
                        (topTrumps.Any() ||
                         (!hands[MyIndex].HasX(_trump) &&
                          (TeamMateIndex == player3 ||
                           PlayerBids[TeamMateIndex] == 0) &&
                          (hands[MyIndex].SuitCount < Game.NumSuits ||     //neodmazavej trumf pokud znas vsechny barvy a nehrajes kilo
                           (_gameType & Hra.Kilo) != 0))) &&
                        (hands[MyIndex].CardCount(_trump) <= 1 ||
                         (hands[MyIndex].CardCount(_trump) <= 2 &&
                          (_gameType & Hra.Kilo) != 0)) &&
                        myInitialHand.CardCount(_trump) <= 2 &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump &&
                                        hands[MyIndex].HasSuit(b))
                            .Any(b => hands[MyIndex].HasA(b) ||
                                      hands[MyIndex].HasX(b)) &&
                        !(hands[MyIndex].Any(i => i.Suit != _trump &&    //neodmazavej pokud muzes ze soupere vytlacit trumf necim jinym
                                                  i.Value < Hodnota.Desitka &&
                                                  !_probabilities.PotentialCards(TeamMateIndex).HasA(i.Suit) &&
                                                  !_probabilities.PotentialCards(TeamMateIndex).HasX(i.Suit) &&
                                                  !_probabilities.PotentialCards(opponent).HasSuit(i.Suit) &&
                                                  _probabilities.PotentialCards(opponent).HasSuit(_trump))))
                    {
                        var cardToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                               i.Value < Hodnota.Desitka &&
                                                                               (!_probabilities.PotentialCards(TeamMateIndex).HasX(_trump) ||
                                                                                _probabilities.PotentialCards(TeamMateIndex).Count(j => j.Suit == _trump &&
                                                                                                                                        (j.Value > i.Value ||
                                                                                                                                         TeamMateIndex == player3) &&
                                                                                                                                        j.Value < Hodnota.Desitka) > 2))
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
                                                                               hands[MyIndex].CardCount(_trump) == 10 - RoundNumber &&
                                                                               !(TeamMateIndex != -1 &&
                                                                                 topTrumps.Count >= 2 &&  //pokud muzes hrat nejvyssi trumf a je sance ze kolega bude mazat tak pravidlo nehraj
                                                                                 _probabilities.PotentialCards(TeamMateIndex).Any(j => j.Suit == i.Suit &&
                                                                                                                                       j.Value >= Hodnota.Desitka) &&
                                                                                 !_probabilities.CertainCards(opponent).HasSuit(i.Suit) &&
                                                                                 _probabilities.PotentialCards(opponent).HasSuit(_trump) &&
                                                                                 _probabilities.PotentialCards(TeamMateIndex)
                                                                                               .Where(j => j.Suit != _trump)
                                                                                               .Any(j => j.Value >= Hodnota.Desitka &&
                                                                                                         !_probabilities.PotentialCards(opponent).HasA(j.Suit))))
                                                                   .FirstOrDefault();
                        if (cardToPlay != null)
                        {
                            return cardToPlay;
                        }
                        //odmazat si barvu pokud mam trumfy abych souperum mohl brat a,x
                        //pokud jsem volil sedmu tak si trumfy setrim
                        if (TeamMateIndex == -1 &&
                            (_gameType & Hra.Sedma) == 0 &&
                            !Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                 .Where(b => b != _trump)
                                 .Any(b => myInitialHand.CardCount(b) >= 4))
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
                        else if (TeamMateIndex == player3 && (_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0)
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
                        GameValue > SevenValue &&
                        (_gameType & Hra.SedmaProti) == 0)
                    //((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 ||
                    // (_gameType & Hra.Kilo) != 0))
                    {
                        //odmazat si barvu pokud nemam trumfy abych mohl mazat
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                i.Value != Hodnota.Eso &&
                                                                                i.Value != Hodnota.Desitka &&
                                                                                _probabilities.PotentialCards(TeamMateIndex).HasSuit(i.Suit) &&
                                                                                ((TeamMateIndex == player2 &&
                                                                                  _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor) ||
                                                                                 (TeamMateIndex == player3 &&
                                                                                  _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor)) &&
                                                                                !hands[MyIndex].HasSuit(_trump) &&
                                                                                hands[MyIndex].Any(j => j.Value == Hodnota.Eso ||
                                                                                                        j.Value == Hodnota.Desitka) &&
                                                                                hands[MyIndex].CardCount(i.Suit) == 1);
                        if (!cardsToPlay.Any() &&
                            (_gameType & Hra.Kilo) != 0)
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                !hands[MyIndex].HasA(i.Suit) &&
                                                                                !hands[MyIndex].HasX(i.Suit) &&
                                                                                !hands[MyIndex].HasSuit(_trump) &&
                                                                                _probabilities.PotentialCards(TeamMateIndex).HasSuit(i.Suit) &&
                                                                                hands[MyIndex].CardCount(i.Suit) <= 2 &&
                                                                                hands[MyIndex].Any(j => j.Value == Hodnota.Eso ||
                                                                                                        j.Value == Hodnota.Desitka));
                            if (!cardsToPlay.Any())
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                    myInitialHand.CardCount(i.Suit) <= 2 &&
                                                                                    !hands[MyIndex].HasA(i.Suit) &&
                                                                                    !hands[MyIndex].HasX(i.Suit) &&
                                                                                    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                        .Where(b => b != _trump)
                                                                                        .Any(b => (hands[MyIndex].HasA(b) ||
                                                                                                   hands[MyIndex].HasX(b)) &&
                                                                                                   _probabilities.PotentialCards(opponent).HasSuit(b)));
                            }
                        }

                        if (!cardsToPlay.Any() &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump)
                                .Any(b => hands[MyIndex].HasA(b) ||
                                          hands[MyIndex].HasX(b)))
                        {
                            //odmazat si trumf abych pozdeji mohl mazat 
                            //(musi existovat barva, kterou neznam a muj spoluhrac v ni doufejme ma vyssi karty nez akter)
                            if (((_gameType & Hra.Kilo) != 0 ||
                                 (myInitialHand.CardCount(_trump) <= 2 &&
                                  hands[MyIndex].CardCount(_trump) <= 1)) &&
                               !(hands[MyIndex].CardCount(_trump) == 2 &&
                                 hands[MyIndex].HasX(_trump) &&
                                 _probabilities.PotentialCards(opponent).HasA(_trump)) &&
                               GameValue > SevenValue &&
                               (PlayerBids[TeamMateIndex] & Hra.Sedma) == 0 &&
                               potentialGreaseCards.Any() &&
                               (TeamMateIndex == player3 ||
                                PlayerBids[TeamMateIndex] == 0) &&
                               opponentTrumps.Count > 0 &&
                               //opponentTrumps <= 6 &&  //abych nehral trumfem hned ale az pozdeji
                               Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                   .Any(b => b != _trump &&
                                             (hands[MyIndex].HasA(b) ||
                                              hands[MyIndex].HasX(b)) &&   //A,X neni treba mazat pokud vim, ze souper barvu zna a nema eso
                                             !(_probabilities.CardProbability(opponent, new Card(b, Hodnota.Eso)) <= _epsilon &&
                                               _probabilities.SuitProbability(opponent, b, RoundNumber) == 1)) &&
                               opponentLoCards.Any() &&
                               !(hands[MyIndex].HasX(_trump) &&            //neodmazavej pokud mam v barve jen X+1
                                 hands[MyIndex].CardCount(_trump) == 2) &&
                               !(hands[MyIndex].Any(i => i.Suit != _trump &&    //neodmazavej pokud muzes ze soupere vytlacit trumf necim jinym
                                                         i.Value < Hodnota.Desitka &&
                                                         !_probabilities.PotentialCards(TeamMateIndex).HasA(i.Suit) &&
                                                         !_probabilities.PotentialCards(TeamMateIndex).HasX(i.Suit) &&
                                                         !_probabilities.PotentialCards(opponent).HasSuit(i.Suit) &&
                                                         _probabilities.PotentialCards(opponent).HasSuit(_trump))))
                            {
                                //chci se vyhnout tomu aby moji nebo spoluhracovu trumfovou desitku sebral akter esem
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                    i.Value < Hodnota.Desitka &&
                                                                                    ((_gameType & Hra.Kilo) != 0 ||
                                                                                     hands[MyIndex].SuitCount < Game.NumSuits) &&
                                                                                    (_probabilities.CardProbability(opponent, new Card(_trump, Hodnota.Eso)) <= _epsilon ||
                                                                                     (i.Value != Hodnota.Desitka &&
                                                                                      _probabilities.CardProbability(TeamMateIndex, new Card(_trump, Hodnota.Desitka)) <= _epsilon) ||
                                                                                     _probabilities.PotentialCards(TeamMateIndex).Count(j => j.Suit == _trump &&
                                                                                                                                             j.Value > i.Value) > 2));
                            }
                            //pokud kolega neflekoval a mam jen trumfy nebo nejvyssi karty, ktere souper zna
                            //tak hraj trumf, nejvyssima netrumfovyma kartama budu pozdeji brat souperovy nizsi karty
                            if (!cardsToPlay.Any() &&
                                PlayerBids[TeamMateIndex] == 0 &&
                                Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                    .Where(b => b != _trump &&
                                                hands[MyIndex].HasSuit(b))
                                    .All(b => topCards.HasSuit(b) &&
                                              _probabilities.LikelyCards(opponent).HasSuit(b)) &&
                                !(hands[MyIndex].Has7(_trump) &&
                                  !_probabilities.PotentialCards(opponent).HasSuit(_trump)))
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                    i.Value < Hodnota.Desitka);
                            }
                            if (!cardsToPlay.Any() &&
                               GameValue > SevenValue &&
                               PlayerBids[MyIndex] == 0 &&
                               (PlayerBids[TeamMateIndex] & Hra.Sedma) == 0 &&
                               !(hands[MyIndex].CardCount(_trump) == 2 &&
                                 hands[MyIndex].HasX(_trump) &&
                                 _probabilities.PotentialCards(opponent).HasA(_trump)) &&
                               !(hands[MyIndex].Any(i => i.Suit != _trump &&    //neodmazavej pokud muzes ze soupere vytlacit trumf necim jinym
                                                         i.Value < Hodnota.Desitka &&
                                                         !_probabilities.PotentialCards(TeamMateIndex).HasA(i.Suit) &&
                                                         !_probabilities.PotentialCards(TeamMateIndex).HasX(i.Suit) &&
                                                         !_probabilities.PotentialCards(opponent).HasSuit(i.Suit) &&
                                                         _probabilities.PotentialCards(opponent).HasSuit(_trump))))
                            {
                                //zbav se trumfu, pokud jich mas malo a kolega ma nejake vyssi karty nez akter v netrumfove barve
                                if (ValidCards(hands[MyIndex]).Any(i => i.Suit == _trump &&
                                                                        i.Value < Hodnota.Desitka &&
                                                                        (TeamMateIndex == player3 ||                                    //pokud je kolega player2 
                                                                         !_probabilities.PotentialCards(TeamMateIndex).HasX(i.Suit) ||  //nechci z nej nahodou vytlacit 
                                                                         !_probabilities.PotentialCards(opponent).HasA(i.Suit)) && //pripadnou trumfovou x
                                                                        ((hands[MyIndex].CardCount(_trump) <= 1 &&
                                                                          hands[MyIndex].SuitCount < Game.NumSuits) ||
                                                                         (hands[MyIndex].CardCount(_trump) <= 2 &&
                                                                          hands[MyIndex].SuitCount == 2)) &&
                                                                        !hands[MyIndex].HasA(_trump) &&
                                                                        !hands[MyIndex].HasX(_trump) &&
                                                                        potentialGreaseCards.Any() &&
                                                                        (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                             .Where(b => b != _trump)
                                                                             .Any(b => (_probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Eso)) >= 1 - _epsilon ||
                                                                                        _probabilities.CardProbability(TeamMateIndex, new Card(b, Hodnota.Desitka)) >= 1 - _epsilon) &&
                                                                                       _probabilities.PotentialCards(opponent).Count(j => j.Suit == i.Suit &&
                                                                                                                                          j.Value < Hodnota.Desitka) > 2) ||
                                                                         _probabilities.CertainCards(opponent).Any(j => j.Suit == i.Suit &&
                                                                                                                        j.Value < Hodnota.Desitka))))
                                {
                                    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                        i.Value < Hodnota.Desitka &&
                                                                                        ((_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0 &&
                                                                                          _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) == 0) ||
                                                                                         _probabilities.PotentialCards(TeamMateIndex).Count(j => j.Suit == i.Suit &&
                                                                                                                                               j.Value > i.Value &&
                                                                                                                                               j.Value < Hodnota.Desitka) > 2)).ToList();
                                }

                                if (!cardsToPlay.Any() &&
                                    PlayerBids[MyIndex] == 0 &&
                                    (TeamMateIndex == player3 ||
                                     PlayerBids[TeamMateIndex] == 0) &&
                                    myInitialHand.CardCount(_trump) <= 2 &&
                                    !(hands[MyIndex].CardCount(_trump) == 2 &&
                                      hands[MyIndex].HasX(_trump) &&
                                      _probabilities.PotentialCards(opponent).HasA(_trump)) &&
                                    ((hands[MyIndex].CardCount(_trump) == 1 &&
                                     hands[MyIndex].SuitCount < Game.NumSuits) ||     //neodmazavej trumf pokud znas vsechny barvy
                                     (hands[MyIndex].CardCount(_trump) <= 2 &&
                                      hands[MyIndex].SuitCount == 2)) &&
                                    (_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 &&
                                    Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                        .Where(b => b != _trump)
                                        .Any(b => hands[MyIndex].HasX(b) &&
                                                  _probabilities.PotentialCards(opponent).Any(i => i.Suit == b && i.Value < Hodnota.Desitka)) &&
                                    !(hands[MyIndex].Any(i => i.Suit != _trump &&    //neodmazavej pokud muzes ze soupere vytlacit trumf necim jinym
                                                         i.Value < Hodnota.Desitka &&
                                                         !_probabilities.PotentialCards(TeamMateIndex).HasA(i.Suit) &&
                                                         !_probabilities.PotentialCards(TeamMateIndex).HasX(i.Suit) &&
                                                         !_probabilities.PotentialCards(opponent).HasSuit(i.Suit) &&
                                                         _probabilities.PotentialCards(opponent).HasSuit(_trump))))
                                {
                                    cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                        i.Value < Hodnota.Desitka &&
                                                                                        (_probabilities.PotentialCards(TeamMateIndex)
                                                                                                       .Where(j => j.Suit == i.Suit)
                                                                                                       .All(j => j.Value <= Hodnota.Desitka) ||
                                                                                         _probabilities.PotentialCards(TeamMateIndex)
                                                                                                       .Count(j => j.Suit == i.Suit &&
                                                                                                                   j.Value > i.Value &&
                                                                                                                   j.Value < Hodnota.Desitka) > 1)).ToList();
                                }
                            }
                        }
                        if (!cardsToPlay.Any(i => i.Suit != _trump))
                        {
                            var temp = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                             i.Value != Hodnota.Eso &&
                                                                             i.Value != Hodnota.Desitka &&
                                                                             _probabilities.PotentialCards(TeamMateIndex).HasSuit(i.Suit) &&
                                                                             hands[MyIndex].CardCount(_trump) <= 1 &&
                                                                             hands[MyIndex].Any(j => j.Value == Hodnota.Eso ||
                                                                                                     j.Value == Hodnota.Desitka) &&
                                                                             hands[MyIndex].CardCount(i.Suit) == 1);
                            if (temp.Any())
                            {
                                cardsToPlay = temp;
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
                Order = 13,
                Description = "hrát spoluhráčovu barvu",
                SkipSimulations = true,
                #region ChooseCard1 Rule13
                ChooseCard1 = () =>
                {
                    if (SevenValue >= GameValue &&
                        topCards.Any(i => i.Suit != _trump) &&
                        !(TeamMateIndex != -1 &&
                          (PlayerBids[TeamMateIndex] & (Hra.Sedma | Hra.SedmaProti)) != 0))
                    {
                        return null;    //pravidlo nehraj, zkus radsi zustat ve stychu
                    }

                    var suitsToPlay = _teamMatesSuits.Concat(teamMatePlayedGreaseCards.Select(i => i.Suit)).Distinct().ToList();

                    if (TeamMateIndex != -1 && suitsToPlay.Any())
                    {
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                //!_bannedSuits.Contains(i.Suit) &&
                                                                                suitsToPlay.Contains(i.Suit) &&
                                                                                (i.Value >= Hodnota.Desitka ||
                                                                                 (((SevenValue >= GameValue &&
                                                                                    (PlayerBids[TeamMateIndex] & (Hra.Sedma | Hra.SedmaProti)) != 0) ||
                                                                                  (PlayerBids[TeamMateIndex] != 0 &&
                                                                                   PlayerBids[MyIndex] == 0)) &&
                                                                                  (_probabilities.PotentialCards(TeamMateIndex).Any(j => j.Suit == i.Suit &&
                                                                                                                                         j.Value > i.Value &&
                                                                                                                                         j.Value < Hodnota.Desitka) ||
                                                                                   (_probabilities.PotentialCards(TeamMateIndex).HasSuit(i.Suit) &&
                                                                                    _probabilities.PotentialCards(TeamMateIndex).Any(j => j.Suit == i.Suit &&
                                                                                                                                          j.Value < i.Value)))) ||
                                                                                 (_probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor &&
                                                                                  _probabilities.HasSolitaryX(TeamMateIndex, i.Suit, RoundNumber) < SolitaryXThreshold)) &&
                                                                                _probabilities.SuitProbability(opponent, _trump, RoundNumber) > 0 &&
                                                                                _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) > 0);

                        if (cardsToPlay.Any(i => i.Value < Hodnota.Desitka &&
                                                 !(cardsToPlay.HasX(i.Suit) &&
                                                   _probabilities.CertainCards(opponent).HasSuit(i.Suit))))
                        {
                            cardsToPlay = cardsToPlay.Where(i => i.Value < Hodnota.Desitka);
                        }

                        if (SevenValue >= GameValue &&
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
                Order = 14,
                Description = "hrát dlouhou barvu mimo A,X,trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule14
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex != -1 &&
                        topCards.Any(i => i.Suit != _trump &&
                                          i.Value < Hodnota.Desitka))
                    {
                        return null;
                    }
                    if (TeamMateIndex == -1 &&
                        (_gameType & Hra.Kilo) != 0 &&
                         hands[MyIndex].Any(i => i.Value == Hodnota.Desitka &&
                                                  myInitialHand.CardCount(i.Suit) == 1))
                    {
                        return null;
                    }
                    if (unwinnableLowCards.Count == 1 &&
                        !unwinnableLowCards.HasSuit(_trump))
                    {
                        return null;
                    }
                    if (SevenValue >= GameValue &&          //pri sedme hraj odshora
                        (TeamMateIndex == -1 ||
                         PlayerBids[TeamMateIndex] == 0) && //neplati pokud kolega flekoval sedmu (tehdy bude lepsi mu mazat)
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Any(b => topCards.HasSuit(b) &&
                                      hands[MyIndex].Any(i => i.Value >= Hodnota.Desitka) &&
                                      opponentCards.HasSuit(b)))
                    {
                        return null;
                    }
                    //nehraj pravidlo pokud mas malo trumfu a nejaky esa, pokus se zustat ve stychu
                    if (TeamMateIndex == -1 &&
                        (_gameType & Hra.Kilo) == 0 &&
                        SevenValue < GameValue &&   //pri sedme hraj dlouhou bocni barvu
                        myInitialHand.CardCount(_trump) <= 4 &&
                        myInitialHand.Count(i => i.Value == Hodnota.Eso &&
                                                 myInitialHand.CardCount(i.Suit) > 1) >= 2 && //jen pokud eso neni samotne (samotne eso neni dobre hrat)
                        hands[MyIndex].Any(i => i.Value == Hodnota.Eso &&
                                                i.Suit != _trump &&
                                                hands[MyIndex].CardCount(i.Suit) > 1 &&
                                                hands[MyIndex].CardCount(i.Suit) < 6)) //pokud je barva hodne dlouha, hraj ji a tlac trumfy
                    {
                        return null;
                    }
                    //nehraj pravidlo pokud mas malo trumfu a nejaky esa, pokus se odmazat trumf
                    if (TeamMateIndex == -1 &&
                        (_gameType & Hra.Kilo) != 0 &&
                        myInitialHand.CardCount(_trump) <= 2 &&
                        myInitialHand.Count(i => i.Value >= Hodnota.Desitka &&
                                                 i.Suit != _trump) >= 2 && //jen pokud eso neni samotne (samotne eso neni dobre hrat)
                        hands[MyIndex].Any(i => i.Value >= Hodnota.Desitka))
                    {
                        return null;
                    }
                    //nehraj pravidlo pokud mas nejvyssi trumfy a souper ma zrejme malo malych trumfu (nehlasil sedmu ani kilo)
                    if (TeamMateIndex != -1 &&
                        myInitialHand.CardCount(_trump) >= 3 &&
                        myInitialHand.HasA(_trump) &&
                        hands[MyIndex].HasX(_trump) &&
                        (_gameType & Hra.Hra) != 0 &&
                        (_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0)
                    {
                        return null;
                    }
                    //nehraj pravidlo pokud spoluhrac hlasil sedmu proti
                    if (TeamMateIndex != -1 &&
                        (PlayerBids[TeamMateIndex] & Hra.SedmaProti) != 0)
                    {
                        return null;
                    }
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
                        if (SevenValue > GameValue)
                        {
                            var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                    i.Value < Hodnota.Desitka &&
                                                                                    myInitialHand.CardCount(i.Suit) >= 4);
                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.OrderByDescending(i => myInitialHand.CardCount(i.Suit))
                                                  .ThenByDescending(i => i.Value)
                                                  .FirstOrDefault();
                            }
                        }

                        if ((_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 ||
                             _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0) &&
                            //(_gameType & Hra.Kilo) == 0 &&              //tohle pravidlo nehraju pri kilu
                            (((_gameType & Hra.Sedma) != 0 &&             //pokud hraju sedmu tak se pokusim uhrat A,X nize 
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
                                                        myInitialHand.CardCount(b) + hands[Game.TalonIndex].CardCount(b) > 3 &&
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
                                    if (opponentTrumps.Count == 1 &&
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
                                                                                    _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor &&
                                                                                    !(Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                          .Select(h => new Card(i.Suit, h))
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

                            if (cardsToPlay.Any(i => topCards.Contains(i)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => topCards.Contains(i)).ToList();
                            }

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
                            if (cardsToPlay.Any(i => topCards.Contains(i)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => topCards.Contains(i)).ToList();
                            }
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
                Order = 15,
                Description = "obětuj plonkovou X",
                SkipSimulations = true,
                #region ChooseCard1 Rule15
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex == -1 &&
                        RoundNumber < 9 &&
                        (_gameType & Hra.Sedma) != 0 &&
                              hands[MyIndex].CardCount(_trump) == 1)
                    {
                        return null;
                    }
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
                        var solitaryXs = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                             .Where(b => b != _trump &&
                                                         hands[MyIndex].HasSolitaryX(b) &&                                      //plonkova X
                                                         (_probabilities.PotentialCards(player2).HasA(b) ||   //aby byla plonkova tak musi byt A nebo trumf jeste ve hre
                                                          _probabilities.PotentialCards(player3).HasA(b) ||
                                                          (!_probabilities.PotentialCards(player2).HasSuit(b) &&
                                                           _probabilities.PotentialCards(player2).HasSuit(_trump)) ||
                                                          (!_probabilities.PotentialCards(player3).HasSuit(b) &&
                                                           _probabilities.PotentialCards(player3).HasSuit(_trump))))
                                             .Select(b => new Card(b, Hodnota.Desitka))
                                             .ToList();
                        //pokud mas vic plonkovych desitek, tak se zbav te kde maji souperi eso
                        //a plonkovou desitku kde souperi eso nemaji si nech - treba pujde konec za tebou a desitka ti zustane
                        var prefSolitaryXs = TeamMateIndex == -1
                                             ? solitaryXs.Where(i => _probabilities.PotentialCards(player2).HasA(i.Suit) ||
                                                                     _probabilities.PotentialCards(player3).HasA(i.Suit))
                                                         .ToList()
                                             : solitaryXs.Where(i => _probabilities.PotentialCards(opponent).HasA(i.Suit))
                                                         .ToList();
                        var totalCards = 10 - RoundNumber + 1;

                        if (solitaryXs.Any() &&
                            (topCards.Count == totalCards - solitaryXs.Count ||
                             (unwinnableLowCards.Count == 1 &&
                              !unwinnableLowCards.HasSuit(_trump)) ||
                            ((_gameType & Hra.Kilo) != 0) &&
                             TeamMateIndex == player3))
                        {
                            if (prefSolitaryXs.Any())
                            {
                                return prefSolitaryXs.RandomOneOrDefault();
                            }
                            return solitaryXs.RandomOneOrDefault();
                        }
                        if (solitaryXs.Any() &&
                            TeamMateIndex == -1 &&
                            SevenValue >= GameValue &&
                            hands[MyIndex].Has7(_trump) &&
                            hands[MyIndex].CardCount(_trump) <= 2 &&
                            solitaryXs.All(i => (!_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                 _probabilities.PotentialCards(player2).HasSuit(_trump)) ||
                                                (!_probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                 _probabilities.PotentialCards(player3).HasSuit(_trump))))
                        {
                            if (prefSolitaryXs.Any())
                            {
                                return prefSolitaryXs.RandomOneOrDefault();
                            }
                            return solitaryXs.RandomOneOrDefault();
                        }
                        if (solitaryXs.Any() &&
                            hands[MyIndex].All(i => i.Suit == _trump ||
                                                    (i.Suit != _trump &&
                                                     i.Value >= Hodnota.Desitka &&
                                                     (_probabilities.PotentialCards(player2).HasA(i.Suit) ||
                                                      _probabilities.PotentialCards(player3).HasA(i.Suit) ||
                                                      (!_probabilities.PotentialCards(player2).HasSuit(i.Suit) &&
                                                       _probabilities.PotentialCards(player2).HasSuit(_trump)) ||
                                                      (!_probabilities.PotentialCards(player3).HasSuit(i.Suit) &&
                                                       _probabilities.PotentialCards(player3).HasSuit(_trump))))))
                        {
                            if (prefSolitaryXs.Any())
                            {
                                return prefSolitaryXs.RandomOneOrDefault();
                            }
                            return solitaryXs.RandomOneOrDefault();
                        }
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 16,
                Description = "hrát vítěznou kartu",
                SkipSimulations = true,
                #region ChooseCard1 Rule16
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
                                                                            (i.Value != Hodnota.Eso ||
                                                                             hands[MyIndex].CardCount(i.Suit) > 1 ||
                                                                             hands[MyIndex].All(j => j.Suit == _trump ||
                                                                                                     j.Value >= Hodnota.Desitka)) &&
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
                        //nehraj pokud ma prvni hrac jiste dalsi male karty v barve a muzes hrat i neco jineho
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => (i.Suit != _trump &&
                                                                             i.Value != Hodnota.Eso) &&
                                                                            Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                .Where(h => h > i.Value)
                                                                                .All(h => _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0) &&
                                                                            ((_gameType & Hra.SedmaProti) == 0 ||
                                                                             (hands[MyIndex].Has7(_trump) &&
                                                                              topCards.Contains(i))) &&
                                                                            (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 ||  //netahat zbytecne trumfy ze spoluhrace
                                                                             _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                            (_probabilities.CertainCards(player3).HasSuit(i.Suit) ||
                                                                             _probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - _epsilon ||
                                                                             !_probabilities.PotentialCards(player3).HasSuit(_trump)) &&
                                                                            !(_probabilities.CertainCards(player3).Any(j => j.Suit == i.Suit &&      //nehraj pokud ma akter jiste nizke karty v barve
                                                                                                                            j.Value < i.Value) &&
                                                                              _probabilities.PotentialCards(player3).HasSuit(_trump) &&             //a navic jeste trumf
                                                                              hands[MyIndex].Any(j => j.Suit != _trump &&                          //a lze hrat neco jineho
                                                                                                       j.Suit != i.Suit &&
                                                                                                       !_bannedSuits.Contains(j.Suit) &&
                                                                                                       j.Value < Hodnota.Desitka))).ToList();
                    }
                    else
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => (i.Suit != _trump &&
                                                                             i.Value != Hodnota.Eso) &&
                                                                            Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                .Where(h => h > i.Value)
                                                                                .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0) &&
                                                                            ((_gameType & Hra.SedmaProti) == 0 ||
                                                                             (hands[MyIndex].Has7(_trump) &&
                                                                              topCards.Contains(i))) &&
                                                                            (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 ||  //netahat zbytecne trumfy ze spoluhrace
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                            (_probabilities.CertainCards(player2).HasSuit(i.Suit) ||
                                                                             _probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - _epsilon ||
                                                                             !_probabilities.PotentialCards(player2).HasSuit(_trump)) &&
                                                                            !(_probabilities.CertainCards(player2).Any(j => j.Suit == i.Suit &&      //nehraj pokud ma akter jiste nizke karty v barve
                                                                                                                            j.Value < i.Value) &&
                                                                              _probabilities.PotentialCards(player2).HasSuit(_trump) &&             //a navic jeste trumf
                                                                              hands[MyIndex].Any(j => j.Suit != _trump &&                          //a lze hrat neco jineho
                                                                                                       j.Suit != i.Suit &&
                                                                                                       !_bannedSuits.Contains(j.Suit) &&
                                                                                                       j.Value < Hodnota.Desitka))).ToList();
                    }
                    return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 17,
                Description = "hrát vítězné A",
                SkipSimulations = true,
                #region ChooseCard1 Rule17
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
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            i.Suit != _trump &&
                                                                            (hands[MyIndex].CardCount(i.Suit) > 1 ||
                                                                             hands[MyIndex].All(j => j.Suit == _trump ||
                                                                                                     j.Value >= Hodnota.Desitka)) &&
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
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            i.Suit != _trump &&
                                                                            _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon &&
                                                                            (_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) == 0 ||
                                                                             Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                 .Where(b => hands[MyIndex].HasSuit(b))
                                                                                 .All(b => topCards.Any(j => j.Suit == b))) &&
                                                                            (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0 ||  //netahat zbytecne trumfy ze spoluhrace
                                                                             _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                            (_probabilities.CertainCards(player3).HasSuit(i.Suit) ||
                                                                             _probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - _epsilon ||
                                                                             !_probabilities.PotentialCards(player3).HasSuit(_trump)) &&
                                                                            !(_probabilities.CertainCards(player3).Any(j => j.Suit == i.Suit &&      //nehraj pokud ma akter jiste nizke karty v barve
                                                                                                                            j.Value < i.Value) &&
                                                                              _probabilities.PotentialCards(player3).HasSuit(_trump) &&             //a navic jeste trumf
                                                                              hands[MyIndex].Any(j => j.Suit != _trump &&                          //a lze hrat neco jineho
                                                                                                       j.Suit != i.Suit &&
                                                                                                       !_bannedSuits.Contains(j.Suit) &&
                                                                                                       j.Value < Hodnota.Desitka))).ToList();
                    }
                    else
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            i.Suit != _trump &&
                                                                            _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon &&
                                                                            (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) == 0 ||
                                                                             Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                 .Where(b => hands[MyIndex].HasSuit(b))
                                                                                 .All(b => topCards.Any(j => j.Suit == b))) &&
                                                                            (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0 ||  //netahat zbytecne trumfy ze spoluhrace
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                            (_probabilities.CertainCards(player2).HasSuit(i.Suit) ||
                                                                             _probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - _epsilon ||
                                                                             !_probabilities.PotentialCards(player2).HasSuit(_trump)) &&
                                                                            !(_probabilities.CertainCards(player2).Any(j => j.Suit == i.Suit &&      //nehraj pokud ma akter jiste nizke karty v barve
                                                                                                                            j.Value < i.Value) &&
                                                                              _probabilities.PotentialCards(player2).HasSuit(_trump) &&             //a navic jeste trumf
                                                                              hands[MyIndex].Any(j => j.Suit != _trump &&                          //a lze hrat neco jineho
                                                                                                       j.Suit != i.Suit &&
                                                                                                       !_bannedSuits.Contains(j.Suit) &&
                                                                                                       j.Value < Hodnota.Desitka))).ToList();
                    }
                    return cardsToPlay.RandomOneOrDefault();
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 18,
                Description = "zůstat ve štychu",
                SkipSimulations = true,
                #region ChooseCard1 Rule18
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex != -1)
                    {
                        //dame sanci spoluhraci aby namazal
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                                !((_gameType & Hra.Kilo) != 0 &&
                                                                                  _probabilities.CardProbability(opponent, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                  _probabilities.CardProbability(opponent, new Card(i.Suit, Hodnota.Desitka)) == 0 &&
                                                                                  hands[MyIndex].Any(j => j.Suit == i.Suit &&
                                                                                                          j.Value >= Hodnota.Desitka)) &&
                                                                                i.Value != Hodnota.Eso &&
                                                                                i.Value != Hodnota.Desitka &&
                                                                                _probabilities.PotentialCards(opponent).HasSuit(i.Suit) &&
                                                                                ((TeamMateIndex == player2 &&
                                                                                  _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor) ||
                                                                                 (!_probabilities.PotentialCards(TeamMateIndex).HasA(i.Suit) &&
                                                                                  !_probabilities.PotentialCards(TeamMateIndex).HasX(i.Suit)) ||
                                                                                 ((_gameType & Hra.SedmaProti) != 0 &&
                                                                                  hands[MyIndex].Has7(_trump))) &&
                                                                                (_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) > 0 ||
                                                                                 _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) == 0 ||
                                                                                 ((_gameType & Hra.SedmaProti) != 0 &&
                                                                                  hands[MyIndex].Has7(_trump))) &&
                                                                                Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                    .Select(h => new Card(i.Suit, h))
                                                                                    .Where(j => _probabilities.CardProbability(opponent, j) > _epsilon)
                                                                                    .All(j => j.Value < i.Value));

                        if (!cardsToPlay.Any() &&
                            SevenValue >= GameValue &&
                            topCards.Any(i => i.Suit != _trump))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                topCards.Contains(i) &&
                                                                                (PlayerBids[TeamMateIndex] == 0 ||
                                                                                 !_bannedSuits.Contains(i.Suit)));
                        }
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
                            if (opponentTrumps.Count == 1 &&
                                hands[MyIndex].Where(i => i.Suit == _trump)
                                              .Any(i => i.Value > opponentTrumps.First().Value))
                            {
                                return ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump)
                                                                 .OrderByDescending(i => i.Value)
                                                                 .FirstOrDefault();
                            }
                        }
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
                                .Any(b => !hands[MyIndex].HasA(b) &&
                                          !hands[MyIndex].HasX(b)))
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
                        if (!cardsToPlay.Any() &&
                            SevenValue >= GameValue &&
                            topCards.Any(i => i.Suit != _trump))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                topCards.Contains(i));
                        }

                        return cardsToPlay.OrderBy(i => _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) > _epsilon ||
                                                        _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) > _epsilon ? 0 : 1)
                                          .ThenByDescending(i => i.Value).FirstOrDefault();
                    }
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 19,
                Description = "hrát největší trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule19
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex != -1 &&
                        topCards.HasSuit(_trump) &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump &&
                                        hands[MyIndex].HasSuit(b))
                            .All(b => (_probabilities.PotentialCards(TeamMateIndex).HasA(b) ||
                                       _probabilities.PotentialCards(TeamMateIndex).HasX(b)) &&
                                      !_probabilities.PotentialCards(opponent).HasSuit(b) &&
                                      _probabilities.PotentialCards(opponent).HasSuit(_trump)))
                    {
                        return ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump).OrderByDescending(i => i.Suit).FirstOrDefault();
                    }

                    if (TeamMateIndex != -1 &&
                        !(hands[MyIndex].HasA(_trump) &&
                          _probabilities.CertainCards(TeamMateIndex).Any(i => i.Suit != _trump &&
                                                                              i.Value >= Hodnota.Desitka) &&
                          _probabilities.LikelyCards(opponent).HasSuit(_trump) &&
                          _probabilities.PotentialCards(TeamMateIndex)
                                        .Where(i => i.Suit == _trump)
                                        .All(i => _probabilities.CardProbability(TeamMateIndex, i) <= 0.1f)))
                    {
                        if ((_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&
                            (_gameType & Hra.Hra) != 0 &&
                            SevenValue >= GameValue)
                        {
                            return null;
                        }

                        if (hands[MyIndex].Any(i => i.Suit != _trump &&
                                                    !hands[MyIndex].HasA(i.Suit) &&
                                                    !hands[MyIndex].HasX(i.Suit) &&
                                                    _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                    _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0 &&
                                                    _probabilities.SuitProbability(opponent, i.Suit, RoundNumber) < 1))
                        {
                            return null;
                        }
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
                        //nehraj pokud maji souperi vic trumfu nez ja
                        //popr. pokud jeden trumfy nezna, druhy jich ma trumfu stejne jako ja, ale moje trumfy nejsou nejvyssi
                        if (hands[MyIndex].CardCount(_trump) < opponentTrumps.Count ||
                            (hands[MyIndex].CardCount(_trump) == opponentTrumps.Count &&
                             (_probabilities.SuitProbability(player2, _trump, RoundNumber) == 0 ||
                              _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                              opponentTrumps.Any(i => hands[MyIndex].Where(j => j.Suit == _trump)
                                                                    .Any(j => i.Value > j.Value)) &&
                              !hands[MyIndex].All(i => i.Suit == _trump ||
                                                       topCards.Contains(i))))
                        {
                            return null;
                        }
                        //nehraj pokud mas dlouhou netrumfovou barvu ve ktere muzes zkusit vytlacit trumf
                        var longSuits5 = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Where(b => b != _trump &&
                                                        hands[MyIndex].CardCount(b) >= 5)
                                            .ToList();
                        if (longSuits5.Any())
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

                    //pokud mas nevyssi trumfy a nemas co mazat ale kolega asi ma co mazat, hraj ho
                    if (TeamMateIndex != -1 &&
                        topCards.HasSuit(_trump) &&
                        _probabilities.PotentialCards(TeamMateIndex).Any(i => i.Value >= Hodnota.Desitka) &&
                        ((_probabilities.PotentialCards(TeamMateIndex).CardCount(_trump) <= 2 &&
                          Enum.GetValues(typeof(Barva)).Cast<Barva>()    //pokud maji akter a kolega barvu kterou neznam, je sance, ze kolega namaze od 3. barvy
                              .Where(b => b != _trump)
                              .Any(b => !hands[MyIndex].HasSuit(b) &&
                                        _probabilities.PotentialCards(opponent).HasSuit(b) &&
                                        _probabilities.PotentialCards(TeamMateIndex).HasSuit(b))) ||
                         (hands[MyIndex].CardCount(_trump) == 1 &&
                          hands[MyIndex].HasA(_trump) &&
                          ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 ||
                           (_gameType & Hra.Hra) == 0 ||
                           SevenValue < GameValue) &&
                          _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) == 0 &&
                          _probabilities.PotentialCards(TeamMateIndex).Any(i => i.Value >= Hodnota.Desitka &&
                                                                                i.Suit != _trump)) ||
                         (myInitialHand.HasA(_trump) &&
                          myInitialHand.HasX(_trump) &&
                          myInitialHand.CardCount(_trump) >= 3 &&
                          ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 ||
                           (_gameType & Hra.Hra) == 0 ||
                           SevenValue < GameValue) &&
                          _probabilities.PotentialCards(TeamMateIndex).Any(i => i.Value >= Hodnota.Desitka &&
                                                                                i.Suit != _trump)) ||
                         ((hands[MyIndex].Where(i => i.Suit != _trump)
                                          .All(i => i.Value < Hodnota.Desitka) ||
                           hands[MyIndex].Where(i => i.Suit != _trump).SuitCount() == 3) &&
                          (hands[MyIndex].Where(i => i.Suit != _trump)
                                         .All(i => topCards.HasSuit(i.Suit)) ||
                           _rounds == null || //pokud v minulych kolech kolega na tvuj nejvyssi trumf nenamazal, tak pravidlo nehraj (kolega nema co mazat). Trumfy setri na pozdeji
                           (_rounds.Any(r => r?.c3 != null &&
                                             r.player1.PlayerIndex == MyIndex &&
                                             r.c1.Suit == _trump &&
                                             r.roundWinner.PlayerIndex == MyIndex) &&
                            !_rounds.Any(r => r?.c3 != null &&
                                              r.player1.PlayerIndex == MyIndex &&
                                              r.c1.Suit == _trump &&
                                              !((r.player2.PlayerIndex == TeamMateIndex &&
                                                 r.c2.Suit != _trump &&
                                                 r.c2.Value < Hodnota.Desitka) ||
                                                (r.player3.PlayerIndex == TeamMateIndex &&
                                                 r.c3.Suit != _trump &&
                                                 r.c3.Value < Hodnota.Desitka)) &&
                                              r.roundWinner.PlayerIndex == MyIndex))))))
                    {
                        return ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump).OrderByDescending(i => i.Suit).FirstOrDefault();
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 20,
                Description = "zbavit se plev",
                SkipSimulations = true,
                #region ChooseCard1 Rule20
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex == -1 &&
                        SevenValue >= GameValue &&
                        Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .Where(b => b != _trump)
                            .Any(b => hands[MyIndex].CardCount(b) >= 3))
                    {
                        return null;
                    }
                    if (topCards.Any() ||
                        (TeamMateIndex != -1 &&
                         PlayerBids[TeamMateIndex] != 0))
                    {
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                i.Value != Hodnota.Eso &&
                                                                                i.Value != Hodnota.Desitka &&
                                                                                (!((_gameType & Hra.Kilo) == 0 &&
                                                                                   (_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&
                                                                                   hands[MyIndex].CardCount(i.Suit) == 1) || //samotne karty se mely hrat v "odmazat si barvu"
                                                                                 Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                     .Where(b => b != _trump &&
                                                                                                 hands[MyIndex].HasSuit(b))
                                                                                     .All(b => hands[MyIndex].CardCount(b) == 1)) &&
                                                                                !(hands[MyIndex].HasX(i.Suit) &&
                                                                                  hands[MyIndex].CardCount(i.Suit) == 2 &&
                                                                                  GameValue > SevenValue) &&
                                                                                !topCards.Contains(i)).ToList();
                        //pokud jsi nevolil - pozice 2: prednostne se zbavuj barvy kde mas X ale ne A
                        if (TeamMateIndex == player2)
                        {
                            if (cardsToPlay.Any(i => i.Suit != _trump &&
                                                     i.Value < Hodnota.Desitka &&
                                                     hands[MyIndex].HasX(i.Suit) &&
                                                     !hands[MyIndex].HasA(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => i.Suit != _trump &&
                                                                     i.Value < Hodnota.Desitka &&
                                                                     hands[MyIndex].HasX(i.Suit)).ToList();
                            }
                            else
                            {
                                cardsToPlay = cardsToPlay.Where(i => !(hands[MyIndex].HasA(i.Suit) &&
                                                                       _probabilities.PotentialCards(player3).HasX(i.Suit))).ToList();
                            }
                        }
                        else
                        {
                            if (cardsToPlay.Any(i => !hands[MyIndex].HasA(i.Suit) &&
                                                     !hands[MyIndex].HasX(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => !hands[MyIndex].HasA(i.Suit) &&
                                                                     !hands[MyIndex].HasX(i.Suit)).ToList();
                            }
                        }
                        //pokud jsi nevolil - pozice 3: nezbabuj se plev od barev kde mas A nebo X pokud mas trumfy
                        if (TeamMateIndex == player3)
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
                                                           _probabilities.SuitProbability(opponent, _trump, RoundNumber) <= RiskFactor) &&
                                                          ((_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) < 1 - _epsilon &&
                                                            _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon) ||
                                                           _probabilities.SuitProbability(opponent, i.Suit, RoundNumber) > 0 ||
                                                           _probabilities.SuitProbability(opponent, _trump, RoundNumber) == 0))
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
                            if (SevenValue >= GameValue &&                           //pri sedme se nezbavuj plonka (abych si udrzel trumfy)
                                hands[MyIndex].HasSuit(_trump) &&                   //nebo karty kde bys ztratil tempo
                                hands[MyIndex].Any(i => i.Suit != _trump &&         //pokud muzes uhrat nejake eso
                                                        i.Value == Hodnota.Eso &&
                                                        _probabilities.PotentialCards(opponent).HasSuit(i.Suit)))
                            {
                                cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].CardCount(i.Suit) > 1 &&
                                                                     !_probabilities.LikelyCards(opponent).Any(j => j.Suit == i.Suit &&
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
                            if (cardsToPlay.Any(i => xSuits.Contains(i.Suit)))
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
                                                                                    (!((_gameType & Hra.Kilo) == 0 &&
                                                                                       (_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&
                                                                                       hands[MyIndex].CardCount(i.Suit) == 1) || //samotne karty se mely hrat v "odmazat si barvu"
                                                                                     Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                                         .Where(b => b != _trump &&
                                                                                                     hands[MyIndex].HasSuit(b))
                                                                                         .All(b => hands[MyIndex].CardCount(b) == 1)) &&
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
                Order = 21,
                Description = "hrát trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule21
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex != -1 &&
                        (_gameType & (Hra.Sedma | Hra.SedmaProti)) != 0 &&
                        (_gameType & Hra.Hra) != 0 &&
                        SevenValue >= GameValue)
                    {
                        return null;
                    }

                    if (TeamMateIndex == player2)
                    {
                        if (!_probabilities.PotentialCards(player3).HasSuit(_trump))
                        {
                            return null;
                        }
                        //hraj trumf pokud jsou vsechny ostatni barvy zakazane a zaroven mas v nejake netrumfove barve eso
                        if (hands[MyIndex].HasSuit(_trump) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Any(b => b != _trump &&
                                            hands[MyIndex].HasA(b)) &&
                            Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                .Where(b => b != _trump &&
                                            hands[MyIndex].HasSuit(b))
                                //hands[MyIndex].HasA(b))
                                .All(b => _bannedSuits.Contains(b)))
                        {
                            var trumps = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                               ((i.Value != Hodnota.Desitka &&
                                                                                 !(hands[MyIndex].HasX(_trump) &&
                                                                                   hands[MyIndex].CardCount(_trump) == 2) &&
                                                                                 (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon ||
                                                                                  _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor)) ||
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0))
                                                                   .ToList();

                            if (trumps.Has7(_trump) &&
                                trumps.CardCount(_trump) > 1 &&
                                myInitialHand.CardCount(_trump) >= 4)
                            {
                                trumps = trumps.Where(i => i.Value != Hodnota.Sedma)
                                               .ToList();
                            }
                            if (topTrumps.Any() ||
                                (hands[MyIndex].HasX(_trump) &&
                                 !_probabilities.PotentialCards(player2).HasA(_trump) &&
                                 _probabilities.PotentialCards(player3).HasA(_trump)))
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
                    if (cardsToPlay.Has7(_trump) &&
                        cardsToPlay.Count > 1)
                    {
                        cardsToPlay = cardsToPlay.Where(i => i.Suit != _trump ||
                                                             i.Value != Hodnota.Sedma)
                                                 .ToList();
                    }
                    if (TeamMateIndex != -1)
                    {
                        if (!_probabilities.PotentialCards(opponent).HasSuit(_trump))
                        {
                            return null;
                        }

                        if (cardsToPlay.All(i => i.Suit == _trump) &&
                            ((_gameType & Hra.Kilo) != 0 ||
                             !(cardsToPlay.Count == 1 && //nehraj pokud ma souper vyssi trumf a jen bys odevzdal tempo
                               _probabilities.PotentialCards(opponent).Any(i => i.Suit == _trump &&
                                                                                i.Value > cardsToPlay.First().Value))))
                        {
                            if (hands[MyIndex].HasX(_trump) &&
                                _probabilities.PotentialCards(opponent).HasA(_trump))
                            {
                                return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                            }
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
                                            hands[MyIndex].Any(i => i.Suit == b &&
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
                                                                 !(hands[MyIndex].HasX(_trump) &&
                                                                   hands[MyIndex].CardCount(_trump) == 2 &&
                                                                   _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) > _epsilon) &&
                                                                 (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon ||
                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) <= _epsilon))
                                                     .ToList();

                            if (hands[MyIndex].HasX(_trump) &&
                                !_probabilities.PotentialCards(TeamMateIndex).HasA(_trump) &&
                                _probabilities.PotentialCards(player3).HasA(_trump))
                            {
                                return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                            }
                            if (cardsToPlay.Has7(_trump) &&
                                cardsToPlay.CardCount(_trump) > 1 &&
                                myInitialHand.CardCount(_trump) >= 4)
                            {
                                cardsToPlay = cardsToPlay.Where(i => i.Value != Hodnota.Sedma)
                                                         .ToList();
                            }

                            return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                        }
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 22,
                Description = "hrát cokoli mimo A,X,trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule22
                ChooseCard1 = () =>
                {
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            !_bannedSuits.Contains(i.Suit) &&
                                                                            !hands[MyIndex].HasA(i.Suit) &&
                                                                            i.Value != Hodnota.Eso &&
                                                                            i.Value != Hodnota.Desitka &&
                                                                            (TeamMateIndex == -1 ||
                                                                             (!(hands[MyIndex].HasA(i.Suit) &&
                                                                                _probabilities.CertainCards(opponent).HasX(i.Suit)) &&
                                                                              ((_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                               _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon &&
                                                                               (_probabilities.PotentialCards(TeamMateIndex).CardCount(i.Suit) > 1 ||
                                                                                //_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                                _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) == 0)) ||
                                                                              ((TeamMateIndex == player2 &&
                                                                                (_probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor ||
                                                                                 (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                  _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0)))) ||
                                                                               (TeamMateIndex == player3 &&
                                                                                _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor)) &&
                                                                              !(_probabilities.CertainCards(opponent).Any(j => j.Suit == i.Suit &&      //nehraj pokud ma akter jiste nizke karty v barve
                                                                                                                               j.Value > i.Value) &&
                                                                                hands[MyIndex].Any(j => j.Suit != _trump &&                          //a lze hrat neco jineho
                                                                                                        j.Suit != i.Suit &&
                                                                                                        !_bannedSuits.Contains(j.Suit) &&
                                                                                                        j.Value < Hodnota.Desitka))))).ToList();

                    if (TeamMateIndex == -1)
                    {
                        if (cardsToPlay.Any(i => hands[MyIndex].CardCount(i.Suit) > 1))
                        {
                            cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].CardCount(i.Suit) > 1).ToList();
                        }
                        if ((_gameType & Hra.Kilo) == 0)
                        {
                            return cardsToPlay.OrderBy(i => i.Value)
                                              .FirstOrDefault();
                        }
                        return null;
                    }
                    cardsToPlay = cardsToPlay.Where(i => (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                          _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon &&
                                                          (_probabilities.PotentialCards(TeamMateIndex).CardCount(i.Suit) > 1 ||
                                                           //_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                           _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) == 0)) ||
                                                         ((TeamMateIndex == player2 &&
                                                           (_probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor ||
                                                            (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                             _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0))) ||
                                                          (TeamMateIndex == player3 &&
                                                           _probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor))).ToList();
                    //_probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor))).ToList();
                    if (cardsToPlay.Any(i => _teamMatesSuits.Contains(i.Suit) &&
                                             !(hands[MyIndex].HasA(i.Suit) &&
                                               _probabilities.PotentialCards(opponent).HasX(i.Suit))))
                    {
                        cardsToPlay = cardsToPlay.Where(i => _teamMatesSuits.Contains(i.Suit) &&
                                                             !(hands[MyIndex].HasA(i.Suit) &&
                                                               _probabilities.PotentialCards(opponent).HasX(i.Suit))).ToList();
                    }
                    if (cardsToPlay.Any(i => hands[MyIndex].CardCount(i.Suit) < 3))
                    {
                        cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].CardCount(i.Suit) < 3).ToList();
                    }
                    if (TeamMateIndex != -1 &&
                        cardsToPlay.Any(i => !(hands[MyIndex].HasA(i.Suit) &&
                                               _probabilities.PotentialCards(opponent).HasX(i.Suit))))
                    {
                        cardsToPlay = cardsToPlay.Where(i => !(hands[MyIndex].HasA(i.Suit) &&
                                                               _probabilities.PotentialCards(opponent).HasX(i.Suit))).ToList();
                    }
                    else if (TeamMateIndex == -1 &&
                             cardsToPlay.Any(i => !(hands[MyIndex].HasA(i.Suit) &&
                                                    (_probabilities.PotentialCards(player2).HasX(i.Suit) ||
                                                     _probabilities.PotentialCards(player3).HasX(i.Suit)))))
                    {
                        cardsToPlay = cardsToPlay.Where(i => !(hands[MyIndex].HasA(i.Suit) &&
                                                               (_probabilities.PotentialCards(player2).HasX(i.Suit) ||
                                                                _probabilities.PotentialCards(player3).HasX(i.Suit)))).ToList();
                    }

                    if (cardsToPlay.Any(i => hands[MyIndex].CardCount(i.Suit) > 1))
                    {
                        cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].CardCount(i.Suit) > 1).ToList();
                    }
                    return cardsToPlay.OrderByDescending(i => _probabilities.PotentialCards(TeamMateIndex)
                                                                            .Count(j => j.Suit == i.Suit &&
                                                                                        (j.Value > i.Value ||
                                                                                         TeamMateIndex == player3) &&
                                                                                        j.Value < Hodnota.Desitka))
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
                Order = 23,
                Description = "hrát cokoli mimo trumf",
                SkipSimulations = true,
                #region ChooseCard1 Rule23
                ChooseCard1 = () =>
                {
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            !_bannedSuits.Contains(i.Suit) &&
                                                                            !hands[MyIndex].HasA(i.Suit) &&
                                                                            i.Value != Hodnota.Eso &&
                                                                            i.Value != Hodnota.Desitka &&
                                                                            (TeamMateIndex == -1 ||
                                                                             (!(hands[MyIndex].HasA(i.Suit) &&
                                                                                _probabilities.CertainCards(opponent).HasX(i.Suit)) &&
                                                                              ((_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                               _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon &&
                                                                               (_probabilities.PotentialCards(TeamMateIndex).CardCount(i.Suit) > 1 ||
                                                                                //_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                                _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) == 0)) ||
                                                                              ((TeamMateIndex == player2 &&
                                                                                (_probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor ||
                                                                                 (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                  _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0)))) ||
                                                                               (TeamMateIndex == player3 &&
                                                                                _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor)) &&
                                                                              !(_probabilities.CertainCards(opponent).Any(j => j.Suit == i.Suit &&      //nehraj pokud ma akter jiste nizke karty v barve
                                                                                                                               j.Value > i.Value) &&
                                                                                hands[MyIndex].Any(j => j.Suit != _trump &&                          //a lze hrat neco jineho
                                                                                                        j.Suit != i.Suit &&
                                                                                                        !_bannedSuits.Contains(j.Suit) &&
                                                                                                        j.Value < Hodnota.Desitka))))).ToList();
                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            !_bannedSuits.Contains(i.Suit) &&
                                                                            !hands[MyIndex].HasA(i.Suit) &&
                                                                            i.Value != Hodnota.Eso &&
                                                                            i.Value != Hodnota.Desitka &&
                                                                            (TeamMateIndex == -1 ||
                                                                             (!(hands[MyIndex].HasA(i.Suit) &&
                                                                                _probabilities.CertainCards(opponent).HasX(i.Suit)) &&
                                                                              ((_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                                _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon &&
                                                                                (_probabilities.PotentialCards(TeamMateIndex).CardCount(i.Suit) > 1 ||
                                                                                 //_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                                 _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) == 0)) ||
                                                                               ((TeamMateIndex == player2 &&
                                                                                 (_probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 0.5 ||
                                                                                  (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                   _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0))) ||
                                                                                (TeamMateIndex == player3 &&
                                                                                 _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 0.5))) &&
                                                                              !(_probabilities.CertainCards(opponent).Any(j => j.Suit == i.Suit &&      //nehraj pokud ma akter jiste nizke karty v barve
                                                                                                                               j.Value > i.Value) &&
                                                                                hands[MyIndex].Any(j => j.Suit != _trump &&                          //a lze hrat neco jineho
                                                                                                        j.Suit != i.Suit &&
                                                                                                        !_bannedSuits.Contains(j.Suit) &&
                                                                                                        j.Value < Hodnota.Desitka))))).ToList();
                    }
                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            !_bannedSuits.Contains(i.Suit) &&
                                                                            !hands[MyIndex].HasA(i.Suit) &&
                                                                            i.Value != Hodnota.Eso &&
                                                                            i.Value != Hodnota.Desitka).ToList();
                    }
                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            i.Value != Hodnota.Eso &&
                                                                            i.Value != Hodnota.Desitka &&
                                                                            (TeamMateIndex == -1 ||
                                                                             (!(hands[MyIndex].HasA(i.Suit) &&
                                                                                _probabilities.CertainCards(opponent).HasX(i.Suit)) &&
                                                                              ((_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                                _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon &&
                                                                                (_probabilities.PotentialCards(TeamMateIndex).CardCount(i.Suit) > 1 ||
                                                                                 //_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                                 _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) == 0)) ||
                                                                               ((TeamMateIndex == player2 &&
                                                                                 (_probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor ||
                                                                                  (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                   _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0))) ||
                                                                                (TeamMateIndex == player3 &&
                                                                                 _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor))) &&
                                                                               !(_probabilities.CertainCards(opponent).Any(j => j.Suit == i.Suit &&      //nehraj pokud ma akter jiste nizke karty v barve
                                                                                                                                j.Value > i.Value) &&
                                                                                _probabilities.PotentialCards(opponent).HasSuit(_trump) &&             //a navic jeste trumf
                                                                                hands[MyIndex].Any(j => j.Suit != _trump &&                          //a lze hrat neco jineho
                                                                                                        j.Suit != i.Suit &&
                                                                                                        !_bannedSuits.Contains(j.Suit) &&
                                                                                                        j.Value < Hodnota.Desitka))))).ToList();
                    }
                    if (TeamMateIndex != -1 &&
                        !cardsToPlay.Any() &&
                        hands[MyIndex].Any(i => hands[MyIndex].HasA(i.Suit) &&
                                                _probabilities.CertainCards(opponent).HasX(i.Suit)))
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            i.Value != Hodnota.Eso &&
                                                                            i.Value != Hodnota.Desitka &&
                                                                            !(hands[MyIndex].HasA(i.Suit) &&
                                                                              _probabilities.CertainCards(opponent).HasX(i.Suit))).ToList();

                        if (cardsToPlay.Any(i => hands[MyIndex].CardCount(i.Suit) > 1))
                        {
                            cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].CardCount(i.Suit) > 1).ToList();
                        }
                        if (cardsToPlay.Any())
                        {
                            return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                        }
                    }
                    else if (!cardsToPlay.Any())
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
                    if (TeamMateIndex != -1 &&
                        cardsToPlay.Any(i => !(hands[MyIndex].HasA(i.Suit) &&
                                               _probabilities.PotentialCards(opponent).HasX(i.Suit))))
                    {
                        cardsToPlay = cardsToPlay.Where(i => !(hands[MyIndex].HasA(i.Suit) &&
                                                               _probabilities.PotentialCards(opponent).HasX(i.Suit))).ToList();
                    }
                    else if (TeamMateIndex == -1 &&
                             cardsToPlay.Any(i => !(hands[MyIndex].HasA(i.Suit) &&
                                                    (_probabilities.PotentialCards(player2).HasX(i.Suit) ||
                                                     _probabilities.PotentialCards(player3).HasX(i.Suit)))))
                    {
                        cardsToPlay = cardsToPlay.Where(i => !(hands[MyIndex].HasA(i.Suit) &&
                                                               (_probabilities.PotentialCards(player2).HasX(i.Suit) ||
                                                                _probabilities.PotentialCards(player3).HasX(i.Suit)))).ToList();
                    }
                    if (TeamMateIndex == -1)
                    {
                        if (cardsToPlay.Any(i => hands[MyIndex].CardCount(i.Suit) > 1))
                        {
                            cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].CardCount(i.Suit) > 1).ToList();
                        }
                        return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                    }
                    cardsToPlay = cardsToPlay.Where(i => (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                          _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon &&
                                                          (_probabilities.PotentialCards(TeamMateIndex).CardCount(i.Suit) > 1 ||
                                                           //_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                           _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) == 0)) ||
                                                         (TeamMateIndex != -1 &&
                                                          !(hands[MyIndex].HasA(i.Suit) &&
                                                            _probabilities.CertainCards(opponent).HasX(i.Suit)) &&
                                                          ((TeamMateIndex == player2 &&
                                                            (_probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 0.5 ||
                                                             (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                              _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0))) ||
                                                           (TeamMateIndex == player3 &&
                                                            _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 0.5)))).ToList();
                    if (cardsToPlay.Any(i => _teamMatesSuits.Contains(i.Suit) &&
                                             !(hands[MyIndex].HasA(i.Suit) &&
                                               _probabilities.PotentialCards(opponent).HasX(i.Suit))))
                    {
                        cardsToPlay = cardsToPlay.Where(i => _teamMatesSuits.Contains(i.Suit) &&
                                                             !(hands[MyIndex].HasA(i.Suit) &&
                                                               _probabilities.PotentialCards(opponent).HasX(i.Suit))).ToList();
                    }

                    if (cardsToPlay.Any(i => hands[MyIndex].CardCount(i.Suit) > 1))
                    {
                        cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].CardCount(i.Suit) > 1).ToList();
                    }

                    if (cardsToPlay.Any(i => hands[MyIndex].CardCount(i.Suit) < 3))
                    {
                        cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].CardCount(i.Suit) < 3).ToList();
                    }

                    return cardsToPlay.OrderByDescending(i => _probabilities.PotentialCards(TeamMateIndex)
                                                                            .Count(j => j.Suit == i.Suit &&
                                                                                        (j.Value > i.Value ||
                                                                                         TeamMateIndex == player3) &&
                                                                                        j.Value < Hodnota.Desitka))
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
                Order = 24,
                Description = "hrát cokoli",
                SkipSimulations = true,
                #region ChooseCard1 Rule24
                ChooseCard1 = () =>
                {
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            i.Value < Hodnota.Desitka &&
                                                                            !_bannedSuits.Contains(i.Suit) &&
                                                                            !hands[MyIndex].HasA(i.Suit) &&
                                                                            (TeamMateIndex == -1 ||
                                                                             (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                              _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon &&
                                                                              (_probabilities.PotentialCards(TeamMateIndex).CardCount(i.Suit) > 1 ||
                                                                               //_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                               _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) == 0)) ||
                                                                             ((TeamMateIndex == player2 &&
                                                                               (_probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor ||
                                                                                (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                 _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0))) ||
                                                                              (TeamMateIndex == player3 &&
                                                                               _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor)))).ToList();

                    if (!cardsToPlay.Any() &&
                        (TeamMateIndex == -1 ||
                         (PlayerBids[TeamMateIndex] == 0 &&
                          !(SevenValue >= GameValue &&
                            !hands[MyIndex].Has7(_trump)))))
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            i.Value < Hodnota.Desitka &&
                                                                            !hands[MyIndex].HasA(i.Suit) &&
                                                                            (TeamMateIndex == -1 ||
                                                                             (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                              _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon &&
                                                                              (_probabilities.PotentialCards(TeamMateIndex).CardCount(i.Suit) > 1 ||
                                                                               //_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                               _probabilities.SuitProbability(TeamMateIndex, _trump, RoundNumber) == 0)) ||
                                                                             ((TeamMateIndex == player2 &&
                                                                               (_probabilities.SuitHigherThanCardExceptAXProbability(TeamMateIndex, i, RoundNumber) >= 1 - RiskFactor ||
                                                                                (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                 _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) == 0))) ||
                                                                              (TeamMateIndex == player3 &&
                                                                               _probabilities.NoSuitOrSuitLowerThanXProbability(TeamMateIndex, i.Suit, RoundNumber) >= 1 - RiskFactor)))).ToList();
                    }
                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            i.Value < Hodnota.Desitka &&
                                                                            !_bannedSuits.Contains(i.Suit) &&
                                                                            ((TeamMateIndex != -1 &&
                                                                              !(hands[MyIndex].HasA(i.Suit) &&
                                                                                _probabilities.PotentialCards(opponent).HasX(i.Suit))) ||
                                                                             (TeamMateIndex == -1 &&
                                                                              !(hands[MyIndex].HasA(i.Suit) &&
                                                                                (_probabilities.PotentialCards(player2).HasX(i.Suit) ||
                                                                                 _probabilities.PotentialCards(player3).HasX(i.Suit)))))).ToList();
                    }
                    if (TeamMateIndex != -1 &&
                        !cardsToPlay.Any() &&
                        hands[MyIndex].Any(i => hands[MyIndex].HasA(i.Suit) &&
                        _probabilities.CertainCards(opponent).HasX(i.Suit)))
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            i.Value != Hodnota.Eso &&
                                                                            i.Value != Hodnota.Desitka &&
                                                                            !(hands[MyIndex].HasA(i.Suit) &&
                                                                              _probabilities.CertainCards(opponent).HasX(i.Suit))).ToList();
                        if (cardsToPlay.Any())
                        {
                            if (cardsToPlay.Any(i => hands[MyIndex].CardCount(i.Suit) > 1))
                            {
                                cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].CardCount(i.Suit) > 1).ToList();
                            }
                            return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                        }
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
                        if (cardsToPlay.Any(i => hands[MyIndex].CardCount(i.Suit) > 1))
                        {
                            cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].CardCount(i.Suit) > 1).ToList();
                        }
                        return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                    }

                    if (cardsToPlay.Any(i => _teamMatesSuits.Contains(i.Suit) &&
                                             !(hands[MyIndex].HasA(i.Suit) &&
                                               _probabilities.PotentialCards(opponent).HasX(i.Suit))))
                    {
                        cardsToPlay = cardsToPlay.Where(i => _teamMatesSuits.Contains(i.Suit) &&
                                                             !(hands[MyIndex].HasA(i.Suit) &&
                                                               _probabilities.PotentialCards(opponent).HasX(i.Suit))).ToList();
                    }
                    //if (cardsToPlay.Any(i => hands[MyIndex].CardCount(i.Suit) < 3))
                    //{
                    //    cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].CardCount(i.Suit) < 3).ToList();
                    //}
                    if (TeamMateIndex != -1 &&
                        cardsToPlay.Any(i => !(hands[MyIndex].HasA(i.Suit) &&
                                               _probabilities.PotentialCards(opponent).HasX(i.Suit))))
                    {
                        cardsToPlay = cardsToPlay.Where(i => !(hands[MyIndex].HasA(i.Suit) &&
                                                               _probabilities.PotentialCards(opponent).HasX(i.Suit))).ToList();
                    }
                    else if (TeamMateIndex == -1 &&
                             cardsToPlay.Any(i => !(hands[MyIndex].HasA(i.Suit) &&
                                                    (_probabilities.PotentialCards(player2).HasX(i.Suit) ||
                                                     _probabilities.PotentialCards(player3).HasX(i.Suit)))))
                    {
                        cardsToPlay = cardsToPlay.Where(i => !(hands[MyIndex].HasA(i.Suit) &&
                                                               (_probabilities.PotentialCards(player2).HasX(i.Suit) ||
                                                                _probabilities.PotentialCards(player3).HasX(i.Suit)))).ToList();
                    }
                    if (cardsToPlay.Any(i => hands[MyIndex].CardCount(i.Suit) > 1))
                    {
                        cardsToPlay = cardsToPlay.Where(i => hands[MyIndex].CardCount(i.Suit) > 1).ToList();
                    }

                    return cardsToPlay.OrderByDescending(i => _probabilities.PotentialCards(TeamMateIndex)
                                                                            .Count(j => j.Suit == i.Suit &&
                                                                                        (j.Value > i.Value ||
                                                                                         TeamMateIndex == player3) &&
                                                                                        j.Value < Hodnota.Desitka))
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
    }
}