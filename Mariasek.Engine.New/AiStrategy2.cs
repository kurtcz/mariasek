using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
#if !PORTABLE
using System.Reflection;
#endif
using System.Runtime.CompilerServices;
using System.Text;
//using log4net;
using Mariasek.Engine.New.Logger;

namespace Mariasek.Engine.New
{
    public class AiStrategy2 : AiStrategyBase
    {
#if !PORTABLE
        private static readonly new ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
#else
        private static readonly new ILog _log = new DummyLogWrapper();
#endif   
        private new Barva _trump { get { return base._trump.Value; } } //dirty
        private List<Barva> _bannedSuits = new List<Barva>();
        public float RiskFactor { get; set; }
        public float SolitaryXThreshold { get; set; }
        private const float _epsilon = 0.01f;

        public AiStrategy2(Barva? trump, Hra gameType, Hand[] hands, Round[] rounds, List<Barva> teamMatesSuits, Probability probabilities)
            : base(trump, gameType, hands, rounds, teamMatesSuits, probabilities)
        {
            if (!trump.HasValue)
            {
                throw new InvalidOperationException("AiStrategy2: trump is null");
            }
            RiskFactor = 0.275f; //0.2727f ~ (9 nad 5) / (11 nad 5)
            SolitaryXThreshold = 0.13f;
		}

        private void BeforeGetRules()
        {
            _bannedSuits.Clear();
            //u sedmy mi nevadi, kdyz se spoluhrace tlacim desitky a esa, snazim se hlavne hrat proti sedme
            if (TeamMateIndex != -1 && _gameType != (Hra.Hra | Hra.Sedma))
            {
                foreach (var r in _rounds.Where(i => i != null && i.c3 != null))
                {
                    //if (r.player1.PlayerIndex == TeamMateIndex &&
                    //    (r.c1.Value == Hodnota.Eso ||
                    //     r.c1.Value == Hodnota.Desitka) &&
                    //    (_probabilities.CardProbability(r.player1.PlayerIndex, new Card(r.c1.Suit, Hodnota.Eso)) > _epsilon ||
                    //     _probabilities.CardProbability(r.player1.PlayerIndex, new Card(r.c1.Suit, Hodnota.Desitka)) > _epsilon) &&
                    //    r.roundWinner.PlayerIndex != r.player1.PlayerIndex &&
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
                             r.roundWinner.PlayerIndex != r.player2.PlayerIndex &&
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
                             r.roundWinner.PlayerIndex != r.player3.PlayerIndex &&
                             r.roundWinner.PlayerIndex != MyIndex)
                    {
                        _bannedSuits.Add(r.c3.Suit);
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

            BeforeGetRules();
            if (RoundNumber == 9)
            {
                yield return new AiRule()
                {
                    Order = 0,
                    Description = "hrát tak abych bral poslední štych",
                    ChooseCard1 = () =>
                    {
                        IEnumerable<Card> cardsToPlay;

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
                };
            }

			yield return new AiRule()
			{
				Order = 1,
				Description = "zkus vytlačit eso",
				SkipSimulations = true,
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
						var suits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
										.Where(b => b != _trump &&
													hands[MyIndex].HasX(b) &&
													hands[MyIndex].CardCount(b) > 1 &&
													_probabilities.CardProbability(player2, new Card(b, Hodnota.Eso)) > _epsilon);
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
					}
					return null;
				}
			};

            yield return new AiRule()
            {
                Order = 2,
                Description = "vytáhnout trumf",
                SkipSimulations = true,
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

                    if (TeamMateIndex == -1)
                    {
                        //c--
                        var holes = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().Where(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > _epsilon ||
                                                                                               _probabilities.CardProbability(player3, new Card(_trump, h)) > _epsilon).ToList();
                        var topTrumps = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump && holes.All(h => h < i.Value)).ToList();
                        var lowcards = hands[MyIndex].Where(i => i.Suit != _trump &&
                                                                 Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Any(h => i.Value < h &&
                                                                               (_probabilities.CardProbability(player2, new Card(i.Suit, h)) > _epsilon ||
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, h)) > _epsilon)));

                        if (holes.Count > 0 && 
                            topTrumps.Count >= holes.Count &&
                            lowcards.Count() <= hands[MyIndex].CardCount(_trump))
                        {
                            cardsToPlay = topTrumps;
                        }
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //co-
                        //pouzivam 0 misto epsilon protoze jinak bych mohl hrat trumfovou x a myslet si ze souper nema eso a on by ho zrovna mel!
                        var holes = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().Where(h => _probabilities.CardProbability(player3, new Card(_trump, h)) > _epsilon).ToList();
                        var topTrumps = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump && holes.All(h => h < i.Value)).ToList();
                        var lowCards = hands[MyIndex].Where(i => i.Suit != _trump &&
                                                                 Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Any(h => h > i.Value &&
                                                                               _probabilities.CardProbability(player3, new Card(i.Suit, h)) > _epsilon)).ToList();

                        if (holes.Count > 0 && 
                            topTrumps.Count >= holes.Count && 
                            lowCards.Count < hands[MyIndex].CardCount(_trump))
                        {
                            cardsToPlay = topTrumps;
                        }
                    }
                    else
                    {
						//c-o
						//pouzivam 0 misto epsilon protoze jinak bych mohl hrat trumfovou x a myslet si ze souper nema eso a on by ho zrovna mel!
						var holes = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>().Where(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > _epsilon).ToList();
                        var topTrumps = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump && holes.All(h => h < i.Value)).ToList();
                        var lowCards = hands[MyIndex].Where(i => i.Suit != _trump &&
                                                                 Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Any(h => h > i.Value &&
                                                                               _probabilities.CardProbability(player2, new Card(i.Suit, h)) > _epsilon)).ToList();

                        if (holes.Count > 0 && 
                            topTrumps.Count >= holes.Count && 
                            lowCards.Count < hands[MyIndex].CardCount(_trump))
                        {
                            cardsToPlay = topTrumps;
                        }
                    }

                    return cardsToPlay.RandomOneOrDefault();
                }

            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "vytlačit trumf",
                SkipSimulations = true,
                ChooseCard1 = () =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == -1)
                    {
                        //: c--
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                i.Value != Hodnota.Eso &&
                                                                i.Suit != _trump &&
                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                ((_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0f &&
                                                                  _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0f) ||
                                                                 (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0f &&
                                                                  _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0f)));

                        //zkus vytlacit trumf trumfem pokud jich mam dost a v ruce mam jen trumfy A nebo X
                        var opponentTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                 .Count(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > 0 ||
                                                             _probabilities.CardProbability(player3, new Card(_trump, h)) > 0);
                        if (!cardsToPlay.Any() && 
                            hands[MyIndex].All(i => i.Value == Hodnota.Eso ||
                                                    i.Value == Hodnota.Desitka ||
                                                    i.Suit == _trump) &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps && 
                            opponentTrumps > 0)
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Suit == _trump);
                        }

                        if (!cardsToPlay.Any() && 
                            opponentTrumps  == 1 && 
                            hands[MyIndex].CardCount(_trump) >= 3 &&
                            hands[MyIndex].Any(i => i.Suit != _trump &&
                                                    (i.Value == Hodnota.Eso ||
                                                     i.Value == Hodnota.Desitka)))
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Suit == _trump);
                        }

                        //zkus vytlacit trumf trumfem pokud jich mam dost a je velka sance, 
                        //ze na mou plivu (kterou bych zahral v nasl. pravidlech) jeden souper namaze a druhej ji prebije
                        if(!cardsToPlay.Any() && opponentTrumps > 0 && ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 ||
                                                                        (_gameType & (Hra.Kilo | Hra.KiloProti)) != 0))
                        {
                            //jeden protivnik nezna nejakou barvu, ale ma A nebo X v jine netrumfove barve a nezna trumfy
                            //a druhy protivnik ma bud vyssi barvu nebo trumf, cili jeden muze druhemu namazat
                            if (ValidCards(hands[MyIndex]).Any(i => (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) < RiskFactor &&
                                                                     _probabilities.SuitProbability(player2, _trump, RoundNumber) < RiskFactor &&
                                                                     (_probabilities.SuitHigherThanCardProbability(player3, i, RoundNumber) >= 1 - RiskFactor ||
                                                                      _probabilities.SuitProbability(player3, i.Suit, RoundNumber) < RiskFactor &&
                                                                      _probabilities.SuitProbability(player3, _trump, RoundNumber) >=  1 - RiskFactor) &&
                                                                     Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                         .Where(b => b != _trump)
                                                                         .Any(b => _probabilities.CardProbability(player2, new Card(b, Hodnota.Eso)) > _epsilon ||
                                                                                   _probabilities.CardProbability(player2, new Card(b, Hodnota.Desitka)) > _epsilon)) ||    //nebo naopak
                                                                    (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) < RiskFactor &&
                                                                     _probabilities.SuitProbability(player3, _trump, RoundNumber) < RiskFactor &&
                                                                     (_probabilities.SuitHigherThanCardProbability(player2, i, RoundNumber) >= 1 - RiskFactor ||
                                                                      _probabilities.SuitProbability(player2, i.Suit, RoundNumber) < RiskFactor &&
                                                                      _probabilities.SuitProbability(player2, _trump, RoundNumber) >= 1 - RiskFactor) &&
                                                                     Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                                         .Where(b => b != _trump)
                                                                         .Any(b => _probabilities.CardProbability(player3, new Card(b, Hodnota.Eso)) > _epsilon ||
                                                                                   _probabilities.CardProbability(player3, new Card(b, Hodnota.Desitka)) > _epsilon))))
                            {
                                cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                    i.Suit == _trump);
                            }
                        }
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //: co-
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                i.Value != Hodnota.Eso &&
                                                                i.Suit != _trump &&
																!_bannedSuits.Contains(i.Suit) &&
                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0f &&
                                                                _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0f &&
                                                                _probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0f &&
                                                                Enum.GetValues(typeof(Hodnota))                                         //musi byt sance, ze spoluhrac ma
                                                                    .Cast<Hodnota>().Any(h => h > i.Value &&                            //v barve i neco jineho nez A nebo X
                                                                                              h != Hodnota.Eso &&
                                                                                              h != Hodnota.Desitka &&
                                                                                              _probabilities.CardProbability(player2, new Card(i.Suit, h)) > _epsilon));

						//zkus vytlacit trumf svou nebo spoluhracovou desitkou nebo esem pokud hraje souper sedmu
                        if (!cardsToPlay.Any() && _gameType == (Hra.Hra | Hra.Sedma))// && _rounds[0] != null)
						{
							cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
																				!_bannedSuits.Contains(i.Suit) &&
																				_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0f &&
																				_probabilities.SuitProbability(player3, _trump, RoundNumber) > 0f &&
																				_probabilities.SuitProbability(player2, i.Suit, RoundNumber) > 0f);
						}
                        //zkus vytlacit trumf trumfem pokud jich mam dost a v ruce mam jen trumfy A nebo X
                        var opponentTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                 .Count(h => _probabilities.CardProbability(player3, new Card(_trump, h)) > 0);
						if (!cardsToPlay.Any() &&
							hands[MyIndex].All(i => i.Value == Hodnota.Eso ||
													i.Value == Hodnota.Desitka ||
													i.Suit == _trump) &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps && 
                            opponentTrumps > 0)
						{
							cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
																				i.Suit == _trump);
						}
					}
                    else
                    {
                        //: c-o
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                i.Value != Hodnota.Eso &&
                                                                i.Suit != _trump &&
                                                                !_bannedSuits.Contains(i.Suit) &&
                                                                _probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0f &&
                                                                _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0f &&
                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0f &&
                                                                Enum.GetValues(typeof(Hodnota))                                         //musi byt sance, ze spoluhrac ma
                                                                    .Cast<Hodnota>().Any(h => h > i.Value &&                            //v barve i neco jineho nez A nebo X
                                                                                              h != Hodnota.Eso &&
                                                                                              h != Hodnota.Desitka &&
                                                                                              _probabilities.CardProbability(player3, new Card(i.Suit, h)) > _epsilon));
						//zkus vytlacit trumf svou nebo spoluhracovou desitkou nebo esem pokud hraje souper sedmu
                        if (!cardsToPlay.Any() && _gameType == (Hra.Hra | Hra.Sedma))// && _rounds[0] != null)
						{
							cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
																				!_bannedSuits.Contains(i.Suit) &&
																				_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0f &&
																				_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0f &&
																				_probabilities.SuitProbability(player3, i.Suit, RoundNumber) > 0f);
						}						
                        //zkus vytlacit trumf trumfem pokud jich mam dost a v ruce mam jen trumfy A nebo X
						var opponentTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                 .Count(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > 0);
						if (!cardsToPlay.Any() &&
							hands[MyIndex].All(i => i.Value == Hodnota.Eso ||
													i.Value == Hodnota.Desitka ||
													i.Suit == _trump) &&
                            hands[MyIndex].CardCount(_trump) > opponentTrumps && 
                            opponentTrumps > 0)
						{
							cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
																				i.Suit == _trump);
						}
                    }

                    return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 4,
                Description = "zkusit vytáhnout plonkovou X",
                SkipSimulations = true,
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();
					//pokud mam na zacatku 6 karet, tak P(souper ma plonkovou X) = (18 nad 9) / (20 nad 10) ~ 0.263
                    //pokud mam na zacatku 5 karet, tak P(souper ma plonkovou X) ~ 0.131
                    //var SolitaryXThreshold = 0.13f;//25f;

					if (TeamMateIndex == -1)
                    {
                        //c--
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            //(_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Kral)) > _epsilon ||
                                                                            // _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Kral)) > _epsilon) &&
                                                                            (i.Suit == _trump || 
                                                                             ((_probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                               _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                              (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                                _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0))) &&
                                                                            (_probabilities.HasSolitaryX(player2, i.Suit, RoundNumber) >= SolitaryXThreshold ||
                                                                             _probabilities.HasSolitaryX(player3, i.Suit, RoundNumber) >= SolitaryXThreshold)).ToList();
                    }
                   // else if (TeamMateIndex == player2)
                   // {
                   //     //co-
                   //     cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
																			////_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Kral)) > _epsilon &&
																			//(i.Suit == _trump ||
                   //                                                          (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                   //                                                           _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0)) &&
                   //                                                         _probabilities.HasSolitaryX(player3, i.Suit, RoundNumber) >= SolitaryXThreshold).ToList();
                   // }
                   // else
                   // {
                   //     //c-o
                   //     cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
																			////_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Kral)) > _epsilon &&
																			//(i.Suit == _trump ||
                    //                                                         (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor||
                    //                                                          _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0)) &&
                    //                                                        _probabilities.HasSolitaryX(player2, i.Suit, RoundNumber) >= SolitaryXThreshold).ToList();
                    //}

                    return cardsToPlay.RandomOneOrDefault();
                }
            };

            if (RoundNumber == 8)
            {
                yield return new AiRule()
                {
                    Order = 5,
                    Description = "šetřit trumfy nakonec",
                    SkipSimulations = true,
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
                                    (trumpsLeft.Count() >= 2 ||                                         //a pokud alespon z jeden ze souperovych trumfu je vetsi nez muj
                                     hands[MyIndex].Any(i => i.Suit == _trump &&                        //tak hraj netrumfovou kartu at je to A nebo X
                                                          i.Value < trumpsLeft.Last().Value)))          //abychom uhrali sedmu nakonec resp. aby ji souper neuhral
                                {
                                    return hands[MyIndex].First(i => i.Suit != _trump);
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
                };
            }

            yield return new AiRule()
            {
                Order = 6,
                Description = "zkusit uhrát bodovanou kartu",
                SkipSimulations = true,
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex == -1 && 
                        ((_gameType & Hra.Kilo) == 0 ||
                         _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0 ||
                         _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0))
                    {
                        //c--
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

                        //TODO: hrat treba jen jednu ostrou kartu pokud jsem na zacatku mel 3 nebo 4 karty v barve?
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) <= _epsilon &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) <= _epsilon)) &&
                                                                                  _probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor &&
                                                                                  _probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - RiskFactor
                     //                                                             (myInitialHand.CardCount(i.Suit) <= 2 ||
                     //                                                              (myInitialHand.CardCount(i.Suit) <= 3 &&
                     //                                                               myInitialHand.CardCount(_trump) <= 3) ||
																				 //  (myInitialHand.CardCount(i.Suit) <= 4 &&
																					//myInitialHand.CardCount(_trump) <= 2))
                                                                          )
                                                                    .ToList();
                        return cardsToPlay.OrderByDescending(i => myInitialHand.CardCount(i.Suit))
                                          .ThenBy(i => i.Value)
                                          .FirstOrDefault();
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //co-
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

                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                ((i.Value == Hodnota.Eso &&
                                                                                  (_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon ||
                                                                                   (_gameType & (Hra.Kilo | Hra.KiloProti)) != 0)) ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) <= _epsilon)) &&
                                                                                _probabilities.SuitProbability(player3, i.Suit, RoundNumber) >= 1 - RiskFactor
                                                                                //(myInitialHand.CardCount(i.Suit) <= 2 ||
                                                                                 //(myInitialHand.CardCount(i.Suit) <= 3 &&
                                                                                 // myInitialHand.CardCount(_trump) <= 3) ||
                                                                                 //(myInitialHand.CardCount(i.Suit) <= 4 &&
                                                                                  //myInitialHand.CardCount(_trump) <= 2))
                                                                          )
                                                                    .ToList();
                        return cardsToPlay.OrderByDescending(i => myInitialHand.CardCount(i.Suit))
                                          .ThenBy(i => i.Value)
                                          .FirstOrDefault();
                    }
                    else if (TeamMateIndex == player3)
                    {
                        //c-o
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

                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                ((i.Value == Hodnota.Eso &&
                                                                                  (_probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon ||
                                                                                   (_gameType & (Hra.Kilo | Hra.KiloProti)) != 0)) ||
                                                                                 (i.Value == Hodnota.Desitka &&
                                                                                  _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) <= _epsilon)) &&
                                                                                _probabilities.SuitProbability(player2, i.Suit, RoundNumber) >= 1 - RiskFactor
                                                                                //(myInitialHand.CardCount(i.Suit) <= 2 ||
                                                                                 //(myInitialHand.CardCount(i.Suit) <= 3 &&
                                                                                 // myInitialHand.CardCount(_trump) <= 3) ||
                                                                                 //(myInitialHand.CardCount(i.Suit) <= 4 &&
                                                                                  //myInitialHand.CardCount(_trump) <= 2))
                                                                          )
                                                                    .ToList();
                        return cardsToPlay.OrderByDescending(i => myInitialHand.CardCount(i.Suit))
                                          .ThenBy(i => i.Value)
                                          .FirstOrDefault();
                    }
                    return null;
                }
            };

            yield return new AiRule()
            {
                Order = 7,
                Description = "vytlačit bodovanou kartu",
                SkipSimulations = true,
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

                    if (TeamMateIndex == player2)
                    {
                        //co-
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            (_probabilities.HasAOrXAndNothingElse(player3, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                            (_probabilities.SuitProbability(player2, _trump, RoundNumber) >= 1 - RiskFactor ||
                                                                             (_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 &&
                                                                              _probabilities.SuitProbability(player2, i.Suit, RoundNumber) <= RiskFactor))).ToList();
                    }
                    else if (TeamMateIndex == player3)
                    {
                        //c-o
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                            (_probabilities.HasAOrXAndNothingElse(player2, i.Suit, RoundNumber) >= 1 - RiskFactor ||
                                                                             _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                            (_probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor ||
                                                                             (_probabilities.SuitProbability(player3, _trump, RoundNumber) > 0 &&
                                                                              _probabilities.SuitProbability(player3, i.Suit, RoundNumber) <= RiskFactor))).ToList();
                    }

                    return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                }
            };

			yield return new AiRule
			{
				Order = 8,
				Description = "odmazat si barvu",
				SkipSimulations = true,
				ChooseCard1 = () =>
				{
					//var poorSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                    //                   .Where(b => b != _trump &&
                    //                               hands[MyIndex].CardCount(b) > 0 &&                                                     
                    //                               ValidCards(hands[MyIndex]).Where(i => i.Suit == b)
					//														 .All(i => i.Value != Hodnota.Desitka &&
					//																   i.Value != Hodnota.Eso))
					//				   .ToDictionary(k => k, v => hands[MyIndex].CardCount(v))
					//				   .OrderBy(kv => kv.Value)
					//				   .Select(kv => new Tuple<Barva, int>(kv.Key, kv.Value))
					//				   .FirstOrDefault();

					//if (poorSuit != null && poorSuit.Item2 == 1)
					{
						//odmazat si barvu pokud mam trumfy abych souperum mohl brat a,x
                        //pokud jsem volil sedmu tak si trumfy setrim
                        if (TeamMateIndex == -1 && (_gameType & Hra.Sedma) == 0)
                        {   
							//c--
                            var cardToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
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
                            var cardToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump && 
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
					if (TeamMateIndex != -1 && (_gameType & Hra.SedmaProti) == 0)
					{
						//odmazat si barvu pokud nemam trumfy abych mohl mazat
						return ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
																	 i.Value != Hodnota.Eso &&
																	 i.Value != Hodnota.Desitka &&
																	 !hands[MyIndex].HasSuit(_trump) &&
																	 hands[MyIndex].Any(j => j.Value == Hodnota.Eso ||
																							 j.Value == Hodnota.Desitka) &&
																	 hands[MyIndex].CardCount(i.Suit) == 1)
														 .OrderBy(i => i.Value)
														 .FirstOrDefault();
					}
                    return null;
				}
			};

            yield return new AiRule()
            {
                Order = 9,
                Description = "bodovat nebo vytlačit trumf",
                SkipSimulations = true,
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex == -1 && (_gameType & Hra.SedmaProti) != 0)
                    {
                        //c--

                        //u sedmy proti hraju od nejvyssi karty (A nebo X) v nejdelsi netrumfove barve
                        //bud projde nebo ze soupere vytlacim trumf
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                (i.Value == Hodnota.Desitka &&
                                                                                 _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) == 0 &&
                                                                                 _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0)))
                                                                    .OrderByDescending(i => hands[MyIndex].CardCount(i.Suit))
                                                                    .ThenByDescending(i => i.Value)
                                                                    .Take(1)
                                                                    .ToList();
                        return cardsToPlay.FirstOrDefault();
                    }
                    else if (TeamMateIndex == player2 && (_gameType & Hra.Sedma) != 0)
                    {
                        //co-
                        //u sedmy hraju od nejvyssi karty (A nebo X) v nejdelsi netrumfove barve
                        //bud projde nebo ze soupere vytlacim trumf
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                (i.Value == Hodnota.Desitka &&
                                                                                 _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0)))
                                                                    .OrderByDescending(i => hands[MyIndex].CardCount(i.Suit))
                                                                    .ThenByDescending(i => i.Value)
                                                                    .Take(1)
                                                                    .ToList();
                        return cardsToPlay.FirstOrDefault();

                    }
                    else if (TeamMateIndex == player3 && (_gameType & Hra.Sedma) != 0)
                    {
                        //c-o
                        //u sedmy hraju od nejvyssi karty (A nebo X) v nejdelsi netrumfove barve
                        //bud projde nebo ze soupere vytlacim trumf
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                (i.Value == Hodnota.Eso ||
                                                                                (i.Value == Hodnota.Desitka &&
                                                                                 _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Eso)) == 0)))
                                                                    .OrderByDescending(i => hands[MyIndex].CardCount(i.Suit))
                                                                    .ThenByDescending(i => i.Value)
                                                                    .Take(1)
                                                                    .ToList();
                        return cardsToPlay.FirstOrDefault();

                    }

                    return null;
                }
            };

            yield return new AiRule()
            {
                Order = 10,
                Description = "hrát dlouhou barvu mimo A,X,trumf",
                SkipSimulations = true,
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex == -1)
                    {
                        //c--
                        if ((_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 ||
                             _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0) &&
                            (_gameType & Hra.Kilo) == 0 &&              //tohle pravidlo nehraju pro kilu
                            (((_gameType & Hra.Sedma)!=0 &&             //pokud hraju sedmu tak se pokusim uhrat A,X nize 
                              hands[MyIndex].CardCount(_trump) > 1) ||  //a dalsi karty pripadne hrat v ramci "hrat cokoli mimo A,X,trumf a dalsich"
                             (hands[MyIndex].CardCount(_trump) > 0)))   //to same pokud jsem volil, sedmu nehraju a uz nemam zadny trumf v ruce
                        {
                            var suits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .OrderBy(b => Math.Min(_probabilities.SuitProbability(player2, b, RoundNumber),
                                                                   _probabilities.SuitProbability(player3, b, RoundNumber)))
                                            .Where(b => b != _trump &&
                                                        !_bannedSuits.Contains(b) &&                                                    
                                                        ValidCards(hands[MyIndex]).Any(i => i.Suit == b &&
                                                                                            i.Value != Hodnota.Eso &&
                                                                                            i.Value != Hodnota.Desitka));
                            if (suits.Any())
                            {
                                var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == suits.First() &&
                                                                                        i.Value != Hodnota.Desitka &&
                                                                                        i.Value != Hodnota.Eso);

                                return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                            }
                        }
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //co-
                        if (_probabilities.SuitProbability(player3, _trump, RoundNumber) > 0)
                        {
                            var suits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Where(b => !_bannedSuits.Contains(b) &&
                                                        _probabilities.SuitProbability(player2, b, RoundNumber) >=
                                                        _probabilities.SuitProbability(player3, b, RoundNumber) &&
                                                        _probabilities.SuitProbability(player2, b, RoundNumber) > 0 &&
                                                        _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0)
                                            .OrderBy(b => _probabilities.SuitProbability(player3, b, RoundNumber))
                                            .Where(b => b != _trump &&
                                                        ValidCards(hands[MyIndex]).Any(i => i.Suit == b &&
                                                                                            i.Value != Hodnota.Eso &&
                                                                                            i.Value != Hodnota.Desitka &&
                                                                                            Enum.GetValues(typeof(Hodnota))                 //musi byt sance, ze spoluhrac ma
                                                                                                .Cast<Hodnota>().Count(h => h > i.Value &&    //v barve i neco jineho nez A nebo X
                                                                                                                            h != Hodnota.Eso &&
                                                                                                                            h != Hodnota.Desitka &&
                                                                                                                            _probabilities.CardProbability(player2, new Card(i.Suit, h)) > 0) > 2));
                            if (suits.Any())
                            {
                                var preferredSuits = suits.Where(b => _teamMatesSuits.Contains(b)).ToList();

                                if (preferredSuits.Any())
                                {
                                    suits = preferredSuits;
                                }
                                var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == suits.First() &&
                                                                                        i.Value != Hodnota.Desitka &&
                                                                                        i.Value != Hodnota.Eso);

                                return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                            }
                        }
                    }
                    else
                    {
                        //c-o
                        if (_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0)
                        {
                            var suits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Where(b => !_bannedSuits.Contains(b) &&
                                                        _probabilities.SuitProbability(player3, b, RoundNumber) >=
                                                        _probabilities.SuitProbability(player2, b, RoundNumber) &&
                                                        _probabilities.SuitProbability(player3, b, RoundNumber) > 0 &&
                                                        _probabilities.SuitProbability(player2, _trump, RoundNumber) > 0)
                                            .OrderBy(b => _probabilities.SuitProbability(player2, b, RoundNumber))
                                            .Where(b => b != _trump &&
                                                        ValidCards(hands[MyIndex]).Any(i => i.Suit == b &&
                                                                                            i.Value != Hodnota.Eso &&
                                                                                            i.Value != Hodnota.Desitka &&
                                                                                            Enum.GetValues(typeof(Hodnota))                 //musi byt sance, ze spoluhrac ma
                                                                                                .Cast<Hodnota>().Count(h => h > i.Value &&    //v barve i neco jineho nez A nebo X
                                                                                                                            h != Hodnota.Eso &&
                                                                                                                            h != Hodnota.Desitka &&
                                                                                                                            _probabilities.CardProbability(player3, new Card(i.Suit, h)) > 0) > 2));
                            if (suits.Any())
                            {
                                var preferredSuits = suits.Where(b => _teamMatesSuits.Contains(b)).ToList();

                                if (preferredSuits.Any())
                                {
                                    suits = preferredSuits;
                                }
                                var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == suits.First() &&
                                                                                        i.Value != Hodnota.Desitka &&
                                                                                        i.Value != Hodnota.Eso);

                                return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                            }
                        }
                    }

                    return null;
                }
            };

            yield return new AiRule()
            {
                Order = 11,
                Description = "obětuj plonkovou X",
                SkipSimulations = true,
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex != -1 ||
                        (((_gameType & Hra.Sedma) != 0 &&           //pokud jsem volil a hraju sedmu a mam ji jako posledni trumf, tak se pokusim uhrat A,X nize 
                          hands[MyIndex].CardCount(_trump) > 1) ||  //a dalsi karty pripadne hrat v ramci "hrat cokoli mimo A,X,trumf a dalsich"
                         (hands[MyIndex].CardCount(_trump) > 0)))   //to same pokud jsem volil, sedmu nehraju a uz nemam zadny trumf v ruce
                    {
                        var topCards = hands[MyIndex].Count(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Where(h => h > i.Value)
                                                                     .All(h => _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0 &&
                                                                               _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0));
                        var solitaryXs = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                             .Where(b => hands[MyIndex].HasSolitaryX(b) &&                                      //plonkova X
                                                    (_probabilities.CardProbability(player2, new Card(b, Hodnota.Eso)) > 0 ||   //aby byla plonkova tak musi byt A jeste ve hre
                                                     _probabilities.CardProbability(player3, new Card(b, Hodnota.Eso)) > 0))
                                             .Select(b => new Card(b, Hodnota.Desitka))
                                             .ToList();
                        var totalCards = 10 - RoundNumber + 1;

                        if (solitaryXs.Count > 0 && topCards == totalCards - solitaryXs.Count)
                        {
                            return solitaryXs.RandomOneOrDefault();
                        }
                    }
                    return null;
                }
            };

            yield return new AiRule()
            {
                Order = 12,
                Description = "hrát vítěznou kartu",
                SkipSimulations = true,
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

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
                                                                            (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 1 ||
                                                                             _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0)).ToList();
                    }
                    return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 13,
                Description = "hrát vítězné A",
                SkipSimulations = true,
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

                    if (TeamMateIndex == -1)
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            i.Suit != _trump &&
                                                                            _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) == 0 &&
                                                                            _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) == 0 &&
                                                                            (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 1 ||
                                                                             _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0) &&
                                                                            (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1 ||
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0)).ToList();
                    }
                    else if (TeamMateIndex == player2)
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            i.Suit != _trump &&
                                                                            _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) == 0 &&
                                                                            (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1 ||
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0)).ToList();
                    }
                    else
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                            i.Suit != _trump &&
                                                                            _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) == 0 &&
                                                                            (_probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 1 ||
                                                                             _probabilities.SuitProbability(player2, _trump, RoundNumber) == 0)).ToList();
                    }
                    return cardsToPlay.RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 14,
                Description = "hrát největší trumf",
                SkipSimulations = true,
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex == -1 && 
                        (_gameType & Hra.Kilo) != 0 &&
                        (_probabilities.SuitProbability(player2, _trump, RoundNumber) > 0 ||
                         _probabilities.SuitProbability(player3, _trump, RoundNumber) > 0))
                    {
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                ((_probabilities.CardProbability(player2, new Card(_trump, Hodnota.Eso)) == 0 &&                                                                                    
                                                                                  _probabilities.CardProbability(player3, new Card(_trump, Hodnota.Eso)) == 0) ||
                                                                                 i.Value < Hodnota.Desitka));

                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }
                    return null;
                }
            };

            yield return new AiRule()
            {
                Order = 15,
                Description = "zbavit se plev",
                SkipSimulations = true,
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
                                                                                !topCards.Contains(i)).ToList();

                        if (TeamMateIndex != -1)
                        {
                            var opponentIndex = Enumerable.Range(0, Game.NumPlayers).First(i => i != MyIndex && i != TeamMateIndex);

                            //neobetuj karty kterymi bych ze spoluhrace vytahl A,X ktery by souper vzal trumfem
                            return cardsToPlay.Where(i => (_probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Eso)) < 1 - _epsilon &&
                                                           _probabilities.CardProbability(TeamMateIndex, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon) ||
                                                          _probabilities.SuitProbability(opponentIndex, i.Suit, RoundNumber) > 0 ||
                                                          _probabilities.SuitProbability(opponentIndex, _trump, RoundNumber) == 0)
                                              .ToList()
                                              .OrderBy(i => i.Value)
                                              .FirstOrDefault();
                        }
                        if ((_gameType & Hra.Kilo) == 0)
                        {
                            return cardsToPlay.OrderBy(i => hands[MyIndex].CardCount(i.Suit))
                                              .ThenBy(i => i.Value)
                                              .FirstOrDefault();
                        }
					}
                    return null;
                }
            };

			yield return new AiRule()
			{
				Order = 16,
				Description = "hrát cokoli mimo A,X,trumf",
				SkipSimulations = true,
				ChooseCard1 = () =>
				{
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
                    return cardsToPlay.OrderByDescending(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
									  .ThenBy(i => i.Value)
									  .FirstOrDefault();
				}
			};

			//if ((_gameType & (Hra.Sedma | Hra.SedmaProti)) == 0 || 
                //((_gameType & Hra.Kilo) != 0 && TeamMateIndex == -1) ||
                //((_gameType & Hra.KiloProti) != 0 && TeamMateIndex != -1) ||
                //!hands[MyIndex].Has7(_trump))
            {
                //yield return new AiRule()
                //{
                //    Order = 15,
                //    Description = "hrát cokoli mimo A,X",
                //    SkipSimulations = true,
                //    ChooseCard1 = () =>
                //    {
                //        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Eso &&
                //                                                                i.Value != Hodnota.Desitka &&
                //                                                                !_bannedSuits.Contains(i.Suit)).ToList();
                //        if (!cardsToPlay.Any())
                //        {
                //            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Eso &&
                //                                                                i.Value != Hodnota.Desitka).ToList();
                //        }
                //        if (TeamMateIndex == -1)
                //        {
                //            return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                //        }

                //        return cardsToPlay.OrderByDescending(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
                //                          .ThenBy(i => i.Value)
                //                          .FirstOrDefault();
                //    }
                //};
            }
            //else
            {
                yield return new AiRule()
                {
                    Order = 17,
                    Description = "hrát cokoli mimo trumf",
                    SkipSimulations = true,
                    ChooseCard1 = () =>
                    {
                        var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                                !_bannedSuits.Contains(i.Suit)).ToList();
                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump).ToList();
                        }
                        if (TeamMateIndex == -1)
                        {
                            return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                        }

                        return cardsToPlay.OrderByDescending(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
                                          .ThenBy(i => i.Value)
                                          .FirstOrDefault();
                    }
                };
            }

            yield return new AiRule()
            {
                Order = 18,
                Description = "hrát cokoli",
                SkipSimulations = true,
                ChooseCard1 = () =>
                {
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump && 
                                                                            !_bannedSuits.Contains(i.Suit)).ToList();

                    if (!cardsToPlay.Any())
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).ToList();
                    }
                    if (TeamMateIndex == -1)
                    {
                        return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                    }

                    return cardsToPlay.OrderByDescending(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
                                      .ThenBy(i => i.Value)
                                      .FirstOrDefault();
                }
            };
        }

        protected override IEnumerable<AiRule> GetRules2(Hand[] hands)
        {
            var player3 = (MyIndex + 1) % Game.NumPlayers;
            var player1 = (MyIndex + 2) % Game.NumPlayers;
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
						//pocet souperovych trumfu vyssi nez muj nejvyssi trumf mensi nez X
						var opHiTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
											 .Where(h => h > myHighestTrumpAfterX)
											 .Count(h => _probabilities.CardProbability(player1, new Card(_trump, h)) > _epsilon);
						return ValidCards(c1, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Desitka &&
                                                                                  c1.IsLowerThan(i, _trump) &&
                                                                                  (i.Suit != _trump ||                          //pokud to neni trumfova X
                                                                                   (_probabilities.CardProbability(player1, new Card(_trump, Hodnota.Eso)) > _epsilon &&
                                                                                    (hands[MyIndex].CardCount(_trump) <= opHiTrumps + 1 ||
                                                                                     ((_gameType & Hra.SedmaProti) != 0 &&
                                                                                      hands[MyIndex].Has7(_trump) &&
                                                                                      hands[MyIndex].CardCount(_trump) <= opHiTrumps + 2)) &&
                                                                                    (c1.Suit != i.Suit ||
                                                                                     c1.Value != Hodnota.Eso))));            //a navic nemam trumfove A
                    }
                    else if (TeamMateIndex == player1)
                    {
                        var hiCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                          .SelectMany(b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                               .Select(h => new Card(b, h)))
                                                               .Where(i => _probabilities.CardProbability(player3, i) > _epsilon &&
                                                                           c1.IsLowerThan(i, _trump));
                        //oc-
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    !hiCards.Any() &&                     //spoluhrac hral nejvyssi kartu co ve hre zbyva
                                                                                    (i.Suit != _trump ||                  //a pokud moje X neni trumfova
                                                                                     !hands[MyIndex].HasA(_trump)))       //trumfovou X hraju jen kdyz nemam A
                                                                        .ToList();

                        if (cardsToPlay.Any())
                        {
                            return cardsToPlay.RandomOneOrDefault();
                        }
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                c1.IsLowerThan(i, _trump) &&          //moje karta prebiji prvni kartu
                                                                                (i.Suit != _trump ||                  //a pokud moje X neni trumfova
                                                                                 !hands[MyIndex].HasA(_trump)) &&     //trumfovou X hraju jen kdyz nemam A
                                                                                _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
																				(_probabilities.NoSuitHigherThanCardProbability(player3, c1, RoundNumber) >= 1 - RiskFactor &&
																				 (_probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor ||
																				  _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor))).ToList();
						if (cardsToPlay.Any())
                        {
                            return cardsToPlay.RandomOneOrDefault();
                        }
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&         //1. karta je nejvetsi
                                                                                (i.Suit != _trump ||                  //a pokud moje X neni trumfova
                                                                                 !hands[MyIndex].HasA(_trump)) &&     //trumfovou X hraju jen kdyz nemam A
                                                                                _probabilities.CardProbability(player3, new Card(c1.Suit, Hodnota.Eso)) == 0 &&
																				(_probabilities.NoSuitHigherThanCardProbability(player3, c1, RoundNumber) >= 1 - RiskFactor &&
																				 (_probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor ||
																				  _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor))).ToList();
						return cardsToPlay.RandomOneOrDefault();
                    }
                    else
                    {
                        //-c-
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                                    c1.IsLowerThan(i, _trump) &&          //moje karta prebiji prvni kartu
                                                                                    (i.Suit != _trump ||                  //a pokud moje X neni trumfova
                                                                                     !hands[MyIndex].HasA(_trump)) &&     //trumfovou X hraju jen kdyz nemam A
                                                                                    _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Eso)) == 0 &&
																				    (_probabilities.NoSuitHigherThanCardProbability(player3, c1, RoundNumber) >= 1 - RiskFactor &&
																				     (_probabilities.SuitProbability(player3, _trump, RoundNumber) <= RiskFactor ||
																				      _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor))).ToList();
						return cardsToPlay.RandomOneOrDefault();
                    }
                }
            };

            yield return new AiRule
            {
                Order = 2,
                Description = "hraj vítěznou A",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    if (TeamMateIndex == -1)
                    {
                        //-c-
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
                                                                                       .All(h => _probabilities.CardProbability(player3, new Card(i.Suit, h)) <= _epsilon)));
                    }
                    else if (TeamMateIndex == player3)
                    {
                        //-co
                        return ValidCards(c1, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                  i.Suit != _trump &&
                                                                                  c1.IsLowerThan(i, _trump) &&
                                                                                  ((c1.Suit == i.Suit &&
                                                                                    c1.Value == Hodnota.Desitka) ||
                                                                                   _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon));
                    }
                    else
                    {
                        var hiCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                          .SelectMany(b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                               .Where(h => _probabilities.CardProbability(player3, new Card(b, h)) > 0 &&
                                                                           c1.IsLowerThan(new Card(b, h), _trump))
                                                               .Select(h => new Card(b, h)));
                        //oc-
                        return ValidCards(c1, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                  i.Suit != _trump &&
                                                                                  (c1.IsLowerThan(i, _trump) ||       //vitezna X
                                                                                   !hiCards.Any()) &&                   //nebo spoluhrac hral nejvyssi kartu co ve hre zbyva
                                                                                  (_probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 1 ||
                                                                                   _probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) >= 1 -_epsilon  ||
                                                                                   _probabilities.SuitProbability(player3, _trump, RoundNumber) == 0) &&
                                                                                  (_probabilities.CardProbability(player3, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon ||
                                                                                   Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                       .Where(h => h != Hodnota.Desitka)
                                                                                       .All(h => _probabilities.CardProbability(player3, new Card(i.Suit, h)) <= _epsilon)));
                    }
                }
            };

            yield return new AiRule
            {
                Order = 3,
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
                Order = 4,
                Description = "namazat",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    if (TeamMateIndex == player3)
                    {                        
                        //-co
                        return ValidCards(c1, hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                         (i.Value == Hodnota.Desitka ||
                                                                          (i.Value == Hodnota.Eso &&        //eso namaz jen kdyz nemuzu chytit desitku nebo pri kilu (proti)
                                                                           (_probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) <= _epsilon ||
                                                                            (((_gameType & Hra.Kilo) != 0) &&
                                                                              _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) < 1)))) &&
                                                                         !(c1.Value == Hodnota.Desitka &&   //nemaz pokud prvni hrac vyjel desitkou a nevim kdo ma eso
                                                                           _probabilities.CardProbability(player3, new Card(c1.Suit, Hodnota.Eso)) <= 1 - _epsilon) &&
                                                                         (_probabilities.SuitHigherThanCardProbability(player3, c1, RoundNumber) >= 1 - RiskFactor ||
//                                                                          ((_gameType & Hra.Kilo) != 0 &&
//                                                                           _probabilities.SuitHigherThanCardProbability(player3, c1, RoundNumber) >= 0.5f) ||
                                                                          (c1.Suit != _trump &&
                                                                           _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) <= RiskFactor &&
                                                                           (_probabilities.SuitProbability(player3, _trump, RoundNumber) >= 1 - RiskFactor ||
                                                                            ((_gameType & Hra.Kilo) != 0) &&  //u kila zkousim mazat vice
                                                                             _probabilities.SuitProbability(player3, _trump, RoundNumber) >= RiskFactor))))
                                                             .OrderBy(i => _probabilities.SuitProbability(player1, i.Suit, RoundNumber))    //namaz v barve kterou souper nema
                                                             .ToList()
                                                             .FirstOrDefault();
                    }
                    return null;
                }
            };

            yield return new AiRule
            {
                Order = 5,
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
							//-c-
							var cardToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit != _trump && 
                                                                                       hands[MyIndex].CardCount(i.Suit) == 1 &&//i.Suit == poorSuit.Item1 &&
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
                        return ValidCards(c1, hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                         i.Suit != c1.Suit &&
                                                                         i.Value != Hodnota.Eso &&
                                                                         i.Value != Hodnota.Desitka &&
                                                                         !hands[MyIndex].HasSuit(_trump) &&
                                                                         hands[MyIndex].Any(j => j.Value == Hodnota.Eso ||
                                                                                                 j.Value == Hodnota.Desitka) &&
                                                                         hands[MyIndex].CardCount(i.Suit) == 1)
                                                             .OrderBy(i => i.Value)
                                                             .FirstOrDefault();
                    }
                    return null;
                }
            };

            yield return new AiRule
            {
                Order = 6,
                Description = "zkusit uhrát trumfovou X",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    if (TeamMateIndex == -1)
                    {
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                    i.Value == Hodnota.Desitka &&
                                                                                    i.IsHigherThan(c1, _trump) &&
                                                                                    (_probabilities.CardProbability(player1, new Card(_trump, Hodnota.Eso)) > 0 ||
                                                                                     (_probabilities.CardProbability(player3, new Card(_trump, Hodnota.Eso)) > 0 &&
                                                                                      _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor)) &&                                                                                    
                                                                                    (((_gameType & Hra.Sedma) != 0 && hands[MyIndex].CardCount(_trump) == 3) ||
                                                                                     hands[MyIndex].CardCount(_trump) == 2)).ToList();
                        return cardsToPlay.FirstOrDefault();
                    }
                    else if (TeamMateIndex == player1)
                    {
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                    i.Value == Hodnota.Desitka &&
                                                                                    i.IsHigherThan(c1, _trump) &&
                                                                                    _probabilities.CardProbability(player3, new Card(_trump, Hodnota.Eso)) > 0 &&
                                                                                    _probabilities.SuitProbability(player3, c1.Suit, RoundNumber) >= 1 - RiskFactor &&
                                                                                    (((_gameType & Hra.Sedma) != 0 && hands[MyIndex].CardCount(_trump) == 3) ||
                                                                                     hands[MyIndex].CardCount(_trump) == 2)).ToList();
                        return cardsToPlay.FirstOrDefault();
                    }
                    else
                    {
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == _trump &&
                                                                                    i.Value == Hodnota.Desitka &&
                                                                                    i.IsHigherThan(c1, _trump) &&
                                                                                    _probabilities.CardProbability(player1, new Card(_trump, Hodnota.Eso)) > 0 &&
                                                                                    (((_gameType & Hra.Sedma) != 0 && hands[MyIndex].CardCount(_trump) == 3) ||
                                                                                     hands[MyIndex].CardCount(_trump) == 2)).ToList();
                        return cardsToPlay.FirstOrDefault();
                    }
                }
            };

            yield return new AiRule
            {
                Order = 7,
                Description = "hrát vysokou kartu mimo A,X",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    if (TeamMateIndex == -1 || TeamMateIndex == player1)
                    {
                        //-c-
                        //oc-
                        return ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == c1.Suit &&
                                                                         i.Value != Hodnota.Desitka &&
                                                                         i.Value != Hodnota.Eso)
                                                             .OrderByDescending(i => i.Value)
                                                             .FirstOrDefault();
                    }
                    return null;
                }
            };

            yield return new AiRule
            {
                Order = 8,
                Description = "hrát nízkou kartu mimo A,X",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    if (TeamMateIndex == player3)
                    {
                        //-co
                        return ValidCards(c1, hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                         i.Value != Hodnota.Eso)
                                                             .OrderBy(i => i.Value)
                                                             .FirstOrDefault();
                    }
                    return null;
                }
            };

            yield return new AiRule
            {
                Order = 9,
                Description = "hrát nízkou kartu",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    return ValidCards(c1, hands[MyIndex]).OrderBy(i => i.Value).FirstOrDefault();
                }
            };
        }

        protected override IEnumerable<AiRule> GetRules3(Hand[] hands)
        {
            var player1 = (MyIndex + 1) % Game.NumPlayers;
            var player2 = (MyIndex + 2) % Game.NumPlayers;

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
						return ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Desitka &&
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
                                                                                        (hands[MyIndex].CardCount(_trump) <= opHiTrumps + 1 ||
                                                                                         ((_gameType & Hra.Sedma) != 0 &&
                                                                                          hands[MyIndex].Has7(_trump) &&
                                                                                          hands[MyIndex].CardCount(_trump) <= opHiTrumps + 2)))) &&
                                                                                      Round.WinningCard(c1, c2, i, _trump) == i);
                    }
                    else if (TeamMateIndex == player1)
                    {
                        var opHiTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                             .Where(h => h > myHighestTrumpAfterX)
                                             .Count(h => _probabilities.CardProbability(player2, new Card(_trump, h)) > _epsilon);
                        return ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Desitka &&
                                                                                      (i.Suit != _trump ||              //pokud to neni trumfova X
                                                                                      // (!hands[MyIndex].HasA(i.Suit) && //nebo pokud mam 2 a mene trumfu a nemam trumfove A
                                                                                      //  hands[MyIndex].CardCount(i.Suit) <= 2 &&
                                                                                      //  (c1.Suit != i.Suit ||           //a pokud nikdo nehral trumfove eso v tomto kole
                                                                                      //   c1.Value != Hodnota.Eso) &&
                                                                                      //  (c2.Suit != i.Suit ||
                                                                                      //   c2.Value != Hodnota.Eso))) &&
                                                                                       (_probabilities.CardProbability(player2, new Card(_trump, Hodnota.Eso)) > _epsilon &&
                                                                                        (hands[MyIndex].CardCount(_trump) <= opHiTrumps + 1 ||
                                                                                         ((_gameType & Hra.SedmaProti) != 0 &&
                                                                                          hands[MyIndex].Has7(_trump) &&
                                                                                          hands[MyIndex].CardCount(_trump) <= opHiTrumps + 2)))) &&
                                                                                      Round.WinningCard(c1, c2, i, _trump) != c2);
                    }
                    else
                    {
                        //pocet souperovych trumfu vyssi nez muj nejvyssi trumf mensi nez X
						var opHiTrumps = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
											 .Where(h => h > myHighestTrumpAfterX)
											 .Count(h => _probabilities.CardProbability(player1, new Card(_trump, h)) > _epsilon);
						return ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Desitka &&
                                                                                      (i.Suit != _trump ||              //pokud to neni trumfova X
                                                                                       //(!hands[MyIndex].HasA(i.Suit) && //nebo pokud mam 2 a mene trumfu a nemam trumfove A
                                                                                        //hands[MyIndex].CardCount(i.Suit) <= 2 &&
                                                                                        //(c1.Suit != i.Suit ||           //a pokud nikdo nehral trumfove eso v tomto kole
                                                                                        // c1.Value != Hodnota.Eso) &&
                                                                                        //(c2.Suit != i.Suit ||
                                                                                         //c2.Value != Hodnota.Eso))) &&
                                                                                       (_probabilities.CardProbability(player1, new Card(_trump, Hodnota.Eso)) > _epsilon &&
																						(hands[MyIndex].CardCount(_trump) <= opHiTrumps + 1 ||
																						 ((_gameType & Hra.SedmaProti) != 0 &&
                                                                                          hands[MyIndex].Has7(_trump) &&
																						  hands[MyIndex].CardCount(_trump) <= opHiTrumps + 2)))) &&
                                                                                      Round.WinningCard(c1, c2, i, _trump) != c1);
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
                        return ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                      i.Suit != _trump &&
                                                                                      Round.WinningCard(c1, c2, i, _trump) != c2 &&
                                                                                      ((c2.Suit == i.Suit &&
                                                                                        c2.Value == Hodnota.Desitka) ||
                                                                                        _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) == 0 ||
                                                                                       (((_gameType & Hra.Kilo) != 0) &&
                                                                                        _probabilities.CardProbability(player2, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon)));
                    }
                    else
                    {
                        //-oc
                        return ValidCards(c1, c2, hands[MyIndex]).FirstOrDefault(i => i.Value == Hodnota.Eso &&
                                                                                      i.Suit != _trump &&
                                                                                      Round.WinningCard(c1, c2, i, _trump) != c1 &&
                                                                                      ((c1.Suit == i.Suit &&
                                                                                        c1.Value == Hodnota.Desitka) ||
                                                                                        _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) == 0 ||
                                                                                       (((_gameType & Hra.Kilo) != 0) &&
                                                                                        _probabilities.CardProbability(player1, new Card(i.Suit, Hodnota.Desitka)) < 1 - _epsilon)));
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
                                                                         hands[MyIndex].CardCount(i.Suit) == 1)
                                                             .OrderBy(i => i.Value)
                                                             .FirstOrDefault();
				}
			};

			yield return new AiRule
            {
                Order = 4,
                Description = "hrát nízkou kartu",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    return ValidCards(c1, c2, hands[MyIndex]).OrderBy(i => i.Value).FirstOrDefault();
                }
            };
        }
    }
}
