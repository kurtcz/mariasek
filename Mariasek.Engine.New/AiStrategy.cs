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
    public class AiStrategy : AiStrategyBase
    {
#if !PORTABLE
        private static readonly new ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
#else
        private static readonly new ILog _log = new DummyLogWrapper();
#endif   
        private new Barva _trump { get { return base._trump.Value; } } //dirty

		public AiStrategy(Barva? trump, Hra gameType, Hand[] hands, Round[] rounds)
            :base(trump, gameType, hands, rounds)
        {
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
                                                                          else if (winnerCard == j)
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
                            cardsToPlay = hands[MyIndex].Where(i => ValidCards(i, hands[player2]).All(j =>
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
                                                                              return Round.WinningCard(k2, i2, j2, _trump) != k2;
                                                                          }
                                                                      })));
                        }
                        else
                        {
                            //: c-o
                            cardsToPlay = hands[MyIndex].Where(i => ValidCards(i, hands[player2]).All(j =>
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
                                                                              return Round.WinningCard(j2, k2, i2, _trump) != j2;
                                                                          }
                                                                          else
                                                                          {
                                                                              return Round.WinningCard(k2, i2, j2, _trump) != j2;
                                                                          }
                                                                      })));
                        }

                        //zkus zahrat trumfovou sedmu nakonec pokud to jde
                        //toho docilime tak, ze v devatem kole nebudeme hrat trumfovou sedmu aby zustala do posledniho kola
                        return cardsToPlay.FirstOrDefault(i => i.Suit != _trump || i.Value != Hodnota.Sedma) ??
                               cardsToPlay.ToList().RandomOneOrDefault();
                    }
                };
            }

            yield return new AiRule()
            {
                Order = 1,
                Description = "hraj vítěznou X",
                UseThreshold = true,
                ChooseCard1 = () =>
                {
                    IEnumerable<Card> cardsToPlay;

                    if (TeamMateIndex == -1)
                    {
                        //: X--
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                i.Suit != _trump && 
                                                                ValidCards(i, hands[player2]).All(j =>
                                                                  ValidCards(i, j, hands[player3]).All(k =>
                                                                      Round.WinningCard(i, j, k, _trump) == i)));
                    }
                    else if (TeamMateIndex == (MyIndex + 1) % Game.NumPlayers)
                    {
                        //: Xo-
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                i.Suit != _trump && 
                                                                ValidCards(i, hands[player2]).Any(j =>
                                                                  ValidCards(i, j, hands[player3]).All(k =>
                                                                      Round.WinningCard(i, j, k, _trump) != k)));
                    }
                    else
                    {
                        //: X-o
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Desitka &&
                                                                i.Suit != _trump && 
                                                                ValidCards(i, hands[player2]).All(j =>
                                                                  ValidCards(i, j, hands[player3]).Any(k =>
                                                                      Round.WinningCard(i, j, k, _trump) != j)));
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 2,
                //Description = "hraj viteznou A pokud souper nema X",
                Description = TeamMateIndex == -1 ? "hraj vítěznou A" : "hraj vítěznou A pokud soupeř nemá X",
                UseThreshold = true,
                ChooseCard1 = () =>
                {
                    IEnumerable<Card> cardsToPlay;

                    if (TeamMateIndex == -1)
                    {
                        //: A--
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                i.Suit != _trump &&
                                                                ValidCards(i, hands[player2]).All(j =>
                                                                    ValidCards(i, j, hands[player3]).All(k =>
                                                                        Round.WinningCard(i, j, k, _trump) == i))); //jako volici hrac stejne nemuzu ze souperu dostat X tak to neresim
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //: Ao-
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                i.Suit != _trump &&
                                                                (ValidCards(i, hands[player2]).Any(j =>
                                                                  ValidCards(i, j, hands[player3]).All(k =>
                                                                      Round.WinningCard(i, j, k, _trump) != k))) &&
                                                              (!hands[player3].HasX(i.Suit) || //souper nema X
                                                               (hands[player3].HasAtMostNCardsOfSuit(i.Suit, 1) &&
                                                                hands[player3].HasSuit(_trump)))); //souper ma posledni kartu v barve a navic jeste nejake trumfy
                    }
                    else
                    {
                        //: A-o
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                i.Suit != _trump &&
                                                                (ValidCards(i, hands[player2]).All(j =>
                                                                  ValidCards(i, j, hands[player3]).Any(k =>
                                                                      Round.WinningCard(i, j, k, _trump) != j))) &&
                                                              (!hands[player2].HasX(i.Suit) || //souper nema X
                                                               (hands[player2].HasAtMostNCardsOfSuit(i.Suit, 1) &&
                                                                hands[player2].HasSuit(_trump)))); //soupers ma posledni kartu v barve a navic jeste nejake trumfy
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "uhrát spoluhráčovu X",
                UseThreshold = true,
                ChooseCard1 = () =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == player2)
                    {
                        //: co-
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => ValidCards(i, hands[player2]).Any(j =>
                                                                    j.Value == Hodnota.Desitka &&
                                                                    j.Suit != _trump &&
                                                                    ValidCards(i, j, hands[player3]).All(k => j.IsHigherThan(k, _trump))));
                    }
                    else if (TeamMateIndex == player3)
                    {
                        //: c-o
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => ValidCards(i, hands[player2]).All(j =>
                                                                  ValidCards(i, j, hands[player3]).Any(k =>
                                                                      k.Value == Hodnota.Desitka &&
                                                                      j.Suit != _trump &&
                                                                      Round.WinningCard(i, j, k, _trump) != j)));
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 4,
                Description = "uhrát spoluhráčovo A",
                UseThreshold = true,
                ChooseCard1 = () =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == player2)
                    {
                        //: co-
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => ValidCards(i, hands[player2]).Any(j =>
                                                                    j.Value == Hodnota.Eso &&
                                                                    j.Suit != _trump &&
                                                                    ValidCards(i, j, hands[player3]).All(k => Round.WinningCard(i, j, k, _trump) == j) &&
                                                                    ((!hands[player3].HasX(j.Suit)) || //souper nema X
                                                                     (hands[player3].HasAtMostNCardsOfSuit(j.Suit, 1) &&
                                                                      hands[player3].HasSuit(_trump))))); //nebo ma posledni kartu v barve a navic jeste nejake trumfy
                    }
                    else if (TeamMateIndex == player3)
                    {
                        //: c-o
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => ValidCards(i, hands[player2]).All(j =>
                                                                  ValidCards(i, j, hands[player3]).Any(k =>
                                                                        k.Value == Hodnota.Eso &&
                                                                        Round.WinningCard(i, j, k, _trump) != j &&
                                                                        ((!hands[player2].HasX(k.Suit)) ||      //2. hrac nema desitku nebo uz barvu nezna a navic ma jeste nejake trumfy
                                                                         (!hands[player2].HasSuit(k.Suit) &&
                                                                          hands[player2].HasSuit(_trump))))));
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 5,
                Description = "vytáhnout plonkovou X",
				UseThreshold = true, //aby se tohle pravidlo nehralo zbytecne, rozlozeni karet muze byt ruzne a muze byt lepsi a esem pockat
                ChooseCard1 = () =>
                {
                    IEnumerable<Card> cardsToPlay;

                    if (TeamMateIndex == -1)
                    {
                        //: A--
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                ((hands[player2].HasX(i.Suit) &&     //jeden souper ma plonkovou desitku
                                                                 hands[player2].HasAtMostNCardsOfSuit(i.Suit, 1) &&
                                                                 (hands[player3].HasSuit(i.Suit) || //a druhy zna barvu nebo nema trumfy
                                                                  !hands[player3].HasSuit(_trump))) ||
                                                                (hands[player3].HasX(i.Suit) &&     //nebo obracene
                                                                 hands[player3].HasAtMostNCardsOfSuit(i.Suit, 1) &&
                                                                 (hands[player2].HasSuit(i.Suit) ||
                                                                  !hands[player2].HasSuit(_trump)))));
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //: Ao-
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                (hands[player3].HasX(i.Suit) &&     //souper ma plonkovou desitku
                                                                 hands[player3].HasAtMostNCardsOfSuit(i.Suit, 1) &&
                                                                 (hands[player2].HasSuit(i.Suit) || //a spoluhrac zna barvu nebo nema trumfy
                                                                  !hands[player2].HasSuit(_trump))));
                    }
                    else
                    {
                        //: A-o
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value == Hodnota.Eso &&
                                                                (hands[player2].HasX(i.Suit) &&     //souper ma plonkovou desitku
                                                                 hands[player2].HasAtMostNCardsOfSuit(i.Suit, 1) &&
                                                                 (hands[player3].HasSuit(i.Suit) || //a spoluhrac zna barvu nebo nema trumfy
                                                                  !hands[player3].HasSuit(_trump))));
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 6,
                Description = "vytlačit trumfové A nebo X",
                ChooseCard1 = () =>
                {
                    IEnumerable<Card> cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == -1)
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&                       //pokud mam trumf
                                                                i.Value != Hodnota.Desitka &&                         //a neni to desitka
                                                                ((hands[player2].HasX(_trump) ||                      //a aspon jeden ze souperu ma trumfove A nebo X
                                                                  hands[player2].HasA(_trump) ||
                                                                  hands[player3].HasX(_trump) ||
                                                                  hands[player3].HasA(_trump)) &&
                                                                  hands[player2].HasSuit(_trump) &&                   //a oba souperi maji aspon jeden trumf
                                                                  hands[player3].HasSuit(_trump) &&
                                                                  hands[MyIndex].CardCount(_trump) >=                 //a ja mam aspon tolik trumfu co oba souperi dohromady
                                                                  hands[player2].CardCount(_trump) +
                                                                  hands[player3].CardCount(_trump)) &&
                                                                ((hands[MyIndex].Has7(_trump)) ||                     //a navic muzu uhrat sedmu nakonec 
                                                                 (hands[MyIndex].Any(j => j.Suit != _trump &&         //nebo mam a muzu uhrat nejake A, X v jine barve
                                                                                         (j.Value == Hodnota.Eso ||
                                                                                          j.Value == Hodnota.Desitka)))));
                    }
                    return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 7,
                Description = "snaž se vytlačit eso",
                ChooseCard1 = () =>
                {
                    IEnumerable<Card> cardsToPlay;

                    if (TeamMateIndex == -1)
                    {
                        //: X--
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                i.Suit != _trump &&
                                                                hands[MyIndex].HasX(i.Suit) &&      //pokud mam desitku a
                                                                ((hands[player2].HasA(i.Suit) &&    //2. hrac ma eso a ja mam kartu na kterou 2. hrac muze zahrat jen eso a nic jineho
                                                                  ValidCards(i, hands[player2]).All(j => j.Value == Hodnota.Eso && j.Suit == i.Suit)) ||
                                                                 ((hands[player3].HasA(i.Suit) &&   //nebo 3. hrac ma eso a ja mam kartu na kterou 3. hrac muze zahrat jen eso a nic jineho
                                                                   ValidCards(i, hands[player2]).All(j =>
                                                                       ValidCards(i, j, hands[player3]).All(k => k.Value == Hodnota.Eso && k.Suit == i.Suit))))));
                    }
                    else if(TeamMateIndex == player2)   //muze se stat ze tlacim eso ze spoluhrace (pri souhre nahod)
                    {
                        //: Xo-
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                i.Suit != _trump &&
                                                                hands[MyIndex].HasX(i.Suit) &&      //pokud mam desitku a
                                                                ((hands[player3].HasA(i.Suit) &&    //3. hrac ma eso a ja mam kartu na kterou 3. hrac muze zahrat jen eso a nic jineho
                                                                   ValidCards(i, hands[player2]).Any(j =>
                                                                       ValidCards(i, j, hands[player3]).All(k => k.Value == Hodnota.Eso && k.Suit == i.Suit)))));
                    }
                    else
                    {
                        //: X-o
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                i.Suit != _trump &&
                                                                hands[MyIndex].HasX(i.Suit) &&      //pokud mam desitku a
                                                                ((hands[player2].HasA(i.Suit) &&    //2. hrac ma eso a ja mam kartu na kterou 2. hrac muze zahrat jen eso a nic jineho
                                                                  ValidCards(i, hands[player2]).All(j => j.Value == Hodnota.Eso && j.Suit == i.Suit))));
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 8,
                Description = "zbav se plev",
                ChooseCard1 = () =>
                {
					var cardsToPlay = new List<Card>();

                    if (TeamMateIndex == -1)
                    {
                        //Hraj plivu.
                        //Odmazeme si tim slabou kartu driv nez na ni bude moct souper namazat svoje A, X.
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&                                 //Pokud mam plivy(ani A, X ani trumf)
                                                                !hands[MyIndex].HasA(i.Suit) &&                                 //v barve kde neznam ani A ani X
                                                                !hands[MyIndex].HasX(i.Suit) &&
                                                                hands[MyIndex].HasSuit(_trump) &&                               //a nejake trumfy
                                                                ((hands[player2].Any(j => (j.Value == Hodnota.Eso ||            //a souper ma A, X v barve kterou neznam
                                                                                         j.Value == Hodnota.Desitka) &&
                                                                                         j.Suit != _trump &&                    //a ktera neni trumfova
                                                                                         !hands[MyIndex].HasSuit(j.Suit) &&     
                                                                                         hands[player2].CardCount(j.Suit) < hands[MyIndex].CardCount(_trump) &&
                                                                                                                                //a pokud ma min tech barev nez ja trumfu
                                                                                         (hands[player2].HasSuit(i.Suit) ||     //a navic ma jeste trumf nebo moji barvu
                                                                                          hands[player2].HasSuit(_trump)))) ||
                                                                 (hands[player3].Any(j => (j.Value == Hodnota.Eso ||
                                                                                         j.Value == Hodnota.Desitka) &&
                                                                                         j.Suit != _trump && 
                                                                                         !hands[MyIndex].HasSuit(j.Suit) &&
                                                                                         hands[player2].CardCount(j.Suit) < hands[MyIndex].CardCount(_trump) &&
                                                                                         (hands[player3].HasSuit(i.Suit) ||
                                                                                          hands[player3].HasSuit(_trump)))))).ToList();
                    }
					if (!cardsToPlay.Any())	//další verze (nezávislá na TeamMateIndex)
					{						
						var topCards = ValidCards(hands[MyIndex]).Where(i => 
                            i.Suit != _trump &&
							hands[player2].All(j => 
								hands[player3].All(k =>
									Round.WinningCard(i, j, k, _trump) == i))).ToList();
						if(topCards.Any())	//a pokud mám nějaké nejvyšší karty ve hře
						{
							cardsToPlay =  ValidCards(hands[MyIndex]).Where(i => !hands[MyIndex].HasA(i.Suit) && //a pokud mám plívy v barvě
								!hands[MyIndex].HasX(i.Suit) && 
								!hands[player2].HasA(i.Suit) && //ve které ani jeden soupeř/spoluhráč nemá A,X
								!hands[player2].HasX(i.Suit) &&
								!hands[player3].HasA(i.Suit) &&
								!hands[player3].HasX(i.Suit)).ToList();
						}
					}

                    return cardsToPlay.RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 9,
                Description = "vytáhnout trumfy",
                ChooseCard1 = () =>
                {
                    IEnumerable<Card> cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == -1)
                    {
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump &&                           //pokud mam trumfy
                                                                (hands[player2].HasSuit(_trump) ||                        //a aspon jeden ze souperu ma taky trumfy
                                                                 hands[player3].HasSuit(_trump)) &&
                                                                hands[MyIndex].CardCount(_trump) > hands[player2].CardCount(_trump) &&  //a pokud jich mam vic nez
                                                                hands[MyIndex].CardCount(_trump) > hands[player3].CardCount(_trump) &&  //kazdy ze souperu
                                                                ValidCards(i, hands[player2]).All(j =>                    //a nektere moje trumfy jsou vetsi nez vsechny trumfy soupere
                                                                    ValidCards(i, j, hands[player3]).All(k =>
                                                                        Round.WinningCard(i, j, k, _trump) == i)) &&
                                                                ((hands[MyIndex].Has7(_trump)) ||                         //a navic muzu uhrat sedmu nakonec 
                                                                 (hands[MyIndex].Any(j => j.Suit != _trump &&             //nebo mam a muzu uhrat nejake A, X v jine barve
                                                                                         (j.Value == Hodnota.Eso ||
                                                                                          j.Value == Hodnota.Desitka)))));
						//cardsToPlay.Count() nyni muze byt rovno 0
						if (cardsToPlay.Count() <
                            Math.Max(hands[player2].CardCount(_trump),
                                     hands[player3].CardCount(_trump)))
                        {
                            //nemam dost trumfu vyssich nez souperovy trumfy
                            if ((_gameType & Hra.Kilo) == 0)
                            {
                                cardsToPlay = Enumerable.Empty<Card>(); //nema cenu tahat trumfy, souperum by zustaly vyssi trumfy a me nizsi
							}
                            else if (hands[MyIndex].CardCount(_trump) >
                                     Math.Max(hands[player2].CardCount(_trump),
                                     hands[player3].CardCount(_trump)))
                            {
                                //hraju kilo a mam vic trumfu nez souperi
                                //ale nekdo z nich ma vetsi trumf nez ja -
                                //pokud ho nemohu vytlacit jinou barvou, tak ho vytahnu
                                var muzuVytlacitTrumf = false;

                                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                                {
                                    if (b != _trump &&
                                       hands[MyIndex].HasSuit(b) &&
                                        ((!hands[player2].HasSuit(b) && hands[player2].HasSuit(_trump)) ||
                                         (!hands[player3].HasSuit(b) && hands[player3].HasSuit(_trump))))
                                    {
                                        muzuVytlacitTrumf = true;
                                        break;
                                    }
                                }
                                if (!muzuVytlacitTrumf)
                                {
									cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit == _trump && i.Value != Hodnota.Sedma);
                                }

                                return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                            }
                        }
                    }

                    return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 10,
                Description = "snaž se vytlačit trumf",
                UseThreshold = true, //protoze muzu omylem vytlacit A,X ze spoluhrace
                ChooseCard1 = () =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == -1)
                    {
                        //: c--
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka && 
                                                                i.Value != Hodnota.Eso &&
                                                                i.Suit != _trump &&
                                                                ((!hands[player2].HasSuit(i.Suit) &&    //ani jeden souper nezna barvu a aspon jeden ma trumfy nebo
                                                                  !hands[player3].HasSuit(i.Suit) &&
                                                                  (hands[player2].HasSuit(_trump) ||
                                                                   hands[player3].HasSuit(_trump))) ||
                                                                 (!hands[player2].HasSuit(i.Suit) &&    //2.hrac nezna barvu a ma max. stejne trumfu jako ja tlacnych
                                                                  hands[player2].HasSuit(_trump) &&
                                                                  hands[player2].HasAtMostNCardsOfSuit(_trump, hands[MyIndex].CardCount(i.Suit))) ||
                                                                 (!hands[player3].HasSuit(i.Suit) &&    //3. hrac nezna barvu a ma max. stejne trumfu jako ja tlacnych
                                                                  hands[player3].HasSuit(_trump) &&
                                                                  hands[player3].HasAtMostNCardsOfSuit(_trump, hands[MyIndex].CardCount(i.Suit))) ||
                                                                 (!hands[player2].HasSuit(i.Suit) &&    //2.hrac nezna barvu a ma trumfy a 3.hrac ma max stejne karet v barve jako ja
                                                                  hands[player2].HasSuit(_trump) &&
                                                                  hands[player3].HasAtMostNCardsOfSuit(i.Suit, hands[MyIndex].CardCount(i.Suit))) ||
                                                                 (!hands[player3].HasSuit(i.Suit) &&    //3.hrac nezna barvu a ma trumfy a 2.hrac ma max stejne karet v barve jako ja
                                                                  hands[player3].HasSuit(_trump) &&
                                                                  hands[player2].HasAtMostNCardsOfSuit(i.Suit, hands[MyIndex].CardCount(i.Suit)))));
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //: co-
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                i.Value != Hodnota.Eso &&
                                                                i.Suit != _trump &&
                                                                !hands[player3].HasSuit(i.Suit) &&      //pokud 3. hrac nezna barvu a ma trumfy a
                                                                hands[player3].HasSuit(_trump) &&
                                                                ((hands[player2].HasSuit(i.Suit) &&     //2.hrac zna barvu a aspon jedna z tech co muze hrat neni desitka nebo eso
                                                                  ValidCards(i, hands[player2]).Any(j => j.Value != Hodnota.Desitka && 
                                                                                                         j.Value != Hodnota.Eso)) ||
                                                                 (!hands[player2].HasSuit(i.Suit) &&    //nebo nezna ani barvu ani nema trumf
                                                                  !hands[player2].HasSuit(_trump))));
                    }
                    else
                    {
                        //: c-o
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                i.Value != Hodnota.Eso &&
                                                                i.Suit != _trump &&
                                                                !hands[player2].HasSuit(i.Suit) &&      //pokud 2. hrac nezna barvu a ma trumfy a
                                                                hands[player2].HasSuit(_trump) &&
                                                                ((hands[player3].HasSuit(i.Suit) &&     //3.hrac zna barvu a aspon jedna z tech co muze hrat neni desitka nebo eso
                                                                  hands[player3].All(j => ValidCards(i, j, hands[player3]).Any(k => k.Value != Hodnota.Desitka && 
                                                                                                                                    k.Value != Hodnota.Eso))) ||
                                                                 (!hands[player3].HasSuit(i.Suit) &&    //nebo nezna ani barvu ani nema trumf
                                                                  !hands[player3].HasSuit(_trump))));
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 11,
                Description = "zůstat ve štychu",
                ChooseCard1 = () =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == -1)
                    {
                        //: c--
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                i.Value != Hodnota.Desitka &&
                                                                i.Value != Hodnota.Eso &&
                                                                ValidCards(i, hands[player2]).All(j => 
                                                                    ValidCards(i, j, hands[player3]).All(k => Round.WinningCard(i, j, k, _trump) == i)));
                    }
                    else if (TeamMateIndex == player2)
                    {
                        //: co-
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                i.Value != Hodnota.Desitka &&
                                                                i.Value != Hodnota.Eso &&
                                                                ValidCards(i, hands[player2]).Any(j =>
                                                                    j.Value != Hodnota.Eso &&
                                                                    j.Value != Hodnota.Desitka &&
                                                                    ValidCards(i, j, hands[player3]).All(k => Round.WinningCard(i, j, k, _trump) != k)));
                    }
                    else
                    {
                        //: c-o
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Suit != _trump &&
                                                                i.Value != Hodnota.Desitka &&
                                                                i.Value != Hodnota.Eso &&
                                                                ValidCards(i, hands[player2]).All(j =>
                                                                    ValidCards(i, j, hands[player3]).Any(k =>
                                                                        k.Value != Hodnota.Eso &&
                                                                        k.Value != Hodnota.Desitka &&
                                                                        Round.WinningCard(i, j, k, _trump) != j)));
                    }

                    return cardsToPlay.OrderByDescending(i => i.Value).LastOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 12,
                Description = "hrát dlouhou barvu (mimo A, X)",
				UseThreshold = true, //protoze muzu omylem vytlacit A,X ze spoluhrace
				ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();
                    var maxCount = 0;

                    foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>().Where(i => i != _trump))
                    {
                        var count = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka).Count(i => i.Suit == barva);

                        //pokud bych hranim barvy vytlacil ze spoluhrace A nebo X ktere by souper sebral trumfem tak barvu nehraj
                        //pokud bych hranim barvy vytlacil ze spoluhrace trumf ktery by souper nebral trumfem a zatoven by nehral A, X tak bravu nehraj
                        if (TeamMateIndex == player2)
                        {
                            if (ValidCards(hands[MyIndex]).Any(i => i.Suit == barva &&
                                                        ValidCards(i, hands[player2])
                                                            .All(j => (j.Value == Hodnota.Eso ||
                                                                       j.Value == Hodnota.Desitka) &&
                                                                      ValidCards(i, j, hands[player3]).Any(k =>
                                                                          Round.WinningCard(i, j, k, _trump) == k))))
                            {
                                count = 0;
                            }
                            if (ValidCards(hands[MyIndex]).Any(i => i.Suit == barva &&
                                                        ValidCards(i, hands[player2])
                                                            .All(j => j.Suit == _trump &&
                                                                      ValidCards(i, j, hands[player3]).Any(k =>
                                                                          k.Suit != _trump && 
                                                                          k.Value != Hodnota.Eso && 
                                                                          k.Value != Hodnota.Desitka))))
                            {
                                count = 0;
                            }
                        }
                        else if (TeamMateIndex == player3)
                        {
                            if (ValidCards(hands[MyIndex]).Any(i => i.Suit == barva &&
                                                        ValidCards(i, hands[player2])
                                                            .Any(j => ValidCards(i, j, hands[player3]).All(k =>
                                                                          (k.Value == Hodnota.Eso ||
                                                                           k.Value == Hodnota.Desitka) &&
                                                                          Round.WinningCard(i, j, k, _trump) == j))))
                            {
                                count = 0;
                            }
                            if (ValidCards(hands[MyIndex]).Any(i => i.Suit == barva &&
                                                        ValidCards(i, hands[player2])
                                                            .Any(j => ValidCards(i, j, hands[player3]).All(k =>
                                                                          j.Suit != _trump &&
                                                                          k.Suit == _trump &&
                                                                          j.Value != Hodnota.Eso &&
                                                                          j.Value != Hodnota.Desitka))))
                            {
                                count = 0;
                            }
                        }
                        if (count > maxCount)
                        {
                            cardsToPlay.Clear();
							cardsToPlay = new List<Card>
													{
														ValidCards(hands[MyIndex]).First(i => i.Suit == barva && i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka)
													};
                            maxCount = count;
                        }
                        else if (count == maxCount && count > 0)
                        {
                            cardsToPlay.Add(ValidCards(hands[MyIndex]).First(i => i.Suit == barva && i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka));
                        }
                    }

                    if (TeamMateIndex == player2)
                    {
                        return cardsToPlay.OrderByDescending(i => i.Value).LastOrDefault();
                    }
                    else
                    {
                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }
                }
            };

            yield return new AiRule()
            {
                Order = 13,
                Description = "hrát cokoli mimo A, X",
                ChooseCard1 = () =>
                {
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka);

                    if (TeamMateIndex == player2)
                    {
                        return cardsToPlay.OrderByDescending(i => i.Value).LastOrDefault();
                    }
                    else
                    {
                        return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                    }
                }
            };

            yield return new AiRule()
            {
                Order = 14,
                Description = "hrát cokoli",
                ChooseCard1 = () =>
                {
                    var cardsToPlay = ValidCards(hands[MyIndex]);

                    return cardsToPlay.RandomOneOrDefault();
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
                                else if(winnerCard == i)
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
                        return cardsToPlay.FirstOrDefault(i => i.Suit != _trump || i.Value != Hodnota.Sedma) ??
                               cardsToPlay.ToList().RandomOneOrDefault();
                    }
                };
            }

            yield return new AiRule
            {
                Order = 1,
                Description = "hraj vítěznou X",
                UseThreshold = TeamMateIndex != player3,
				SkipSimulations = TeamMateIndex == player3,
                ChooseCard2 = (Card c1) =>
                {
                    IEnumerable<Card> cardsToPlay;
                    var validCardCount = ValidCards(c1, hands[MyIndex]).Count();

                    if (TeamMateIndex == -1)
                    {
                        //: -X-
                        cardsToPlay =
                            ValidCards(c1, hands[MyIndex])
                                .Where(i => i.Value == Hodnota.Desitka &&
                                            (i.Suit != _trump ||                //viteznou X hrajem jen kdyz neni trumfova
                                             (hands[player1].HasA(_trump) &&    //nebo je trumfova a hrozi, ze pokud ji nezahraju ted,
                                              validCardCount <= 2)) &&          //tak mi ji pozdejc souper vytahne trumfovym esem
                                            ValidCards(c1, i, hands[player3]).All(j => Round.WinningCard(c1, i, j, _trump) == i));
                    }
                    else if (TeamMateIndex == player1)
                    {
                        //: oX-
                        cardsToPlay =
                            ValidCards(c1, hands[MyIndex])
                                .Where(i => i.Value == Hodnota.Desitka &&
                                            i.Suit != _trump &&                 //v tomto pripade hrajeme jen pokud X neni trumfova
                                            ValidCards(c1, i, hands[player3]).All(j => Round.WinningCard(c1, i, j, _trump) != j));
                    }
                    else
                    {
                        //: -Xo
                        cardsToPlay =
                            ValidCards(c1, hands[MyIndex])
                                .Where(i => i.Value == Hodnota.Desitka &&
                                            (i.Suit != _trump ||                //viteznou X hrajem jen kdyz neni trumfova
                                             (hands[player1].HasA(_trump) &&    //nebo je trumfova a hrozi, ze pokud ji nezahraju ted,
                                              validCardCount <= 2)) &&          //tak mi ji pozdejc souper vytahne trumfovym esem
                                            ValidCards(c1, i, hands[player3]).All(j => Round.WinningCard(c1, i, j, _trump) != c1));
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule
            {
                Order = 2,
                Description = "hraj vítěznou A pokud nemohu chytit soupeřovu X",
                UseThreshold = true,
                ChooseCard2 = (Card c1) =>
                {
                    IEnumerable<Card> cardsToPlay;

                    if (TeamMateIndex == -1)
                    {
                        //: -A-
                        cardsToPlay =
                            ValidCards(c1, hands[MyIndex])
                                .Where(i => i.Value == Hodnota.Eso &&
                                            i.Suit != _trump &&
                                            ValidCards(c1, i, hands[player3]).All(j => Round.WinningCard(c1, i, j, _trump) == i) &&
                                            ((!hands[player3].HasX(i.Suit) &&
                                              !hands[player1].HasX(i.Suit)) ||      //ani jeden souper nema X
                                             (!hands[player1].HasSuit(i.Suit) &&
                                              hands[player1].HasSuit(_trump)) ||  //nebo 1. hrac uz nezna barvu a navic ma jeste nejake trumfy
                                             (hands[player3].HasAtMostNCardsOfSuit(i.Suit, 1) &&
                                              hands[player3].HasSuit(_trump))));  //nebo 3. hrac ma posledni kartu v barve a navic jeste nejake trumfy
                    }
                    else if (TeamMateIndex == player1)
                    {
                        //: oA-
                        cardsToPlay =
                            ValidCards(c1, hands[MyIndex])
                                .Where(i => i.Value == Hodnota.Eso &&
                                            i.Suit != _trump &&
                                            ValidCards(c1, i, hands[player3]).All(j => Round.WinningCard(c1, i, j, _trump) != j) &&
                                            (!hands[player3].HasX(i.Suit) || //souper nema X
                                             (hands[player3].HasAtMostNCardsOfSuit(i.Suit, 1) &&
                                              hands[player3].HasSuit(_trump)))); //nebo ma posledni kartu v barve a navic jeste nejake trumfy
                    }
                    else
                    {
                        //: -Ao
                        cardsToPlay =
                            ValidCards(c1, hands[MyIndex])
                                .Where(i => i.Value == Hodnota.Eso &&
                                            i.Suit != _trump &&
                                            ValidCards(c1, i, hands[player3]).All(j => Round.WinningCard(c1, i, j, _trump) != c1) && //bud ja nebo kolega budeme brat stych
                                            (!hands[player1].HasX(i.Suit) ||     //souper nema X
                                            (!hands[player1].HasSuit(i.Suit) && //nebo uz nema dalsi barvu (X ani jinou) a pritom ma trumfy
                                              hands[player1].HasSuit(_trump))));
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule
            {
                Order = 3,
                Description = "vytáhni plonkovou X",
                UseThreshold = true,
                ChooseCard2 = (Card c1) =>
                {
                    IEnumerable<Card> cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == -1)
                    {
                        //: -A-
                        cardsToPlay =
                            ValidCards(c1, hands[MyIndex])
                                .Where(i => i.Value == Hodnota.Eso &&                                                   //hraj vitezne eso kdyz
                                            c1.IsLowerThan(i, _trump) &&
                                            ValidCards(c1, i, hands[player3]).All(j => i.Value == Hodnota.Desitka));    //3. hrac ma plonkovou desitku
                    }
                    else if (TeamMateIndex == player1)
                    {
                        //: oA-
                        cardsToPlay =
                            ValidCards(c1, hands[MyIndex])
                                .Where(i => i.Value == Hodnota.Eso &&                                                   //hraj vitezne eso kdyz
                                            ValidCards(c1, i, hands[player3]).All(j => i.Value == Hodnota.Desitka));    //3. hrac ma plonkovou desitku
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 4,
                Description = "namazat A nebo X",
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == player3)
                    {
                        //: -co
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => (i.Value == Hodnota.Eso ||
                                                                                 i.Value == Hodnota.Desitka) &&
                                                                                i.Suit != _trump &&
                                                                                !hands[player1].HasSuit(i.Suit) &&
                                                                                ValidCards(c1, i, hands[player3]).All(j => Round.WinningCard(c1, i, j, _trump) != c1));
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 5,
                Description = "snaž se vytlačit eso",
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == -1 || TeamMateIndex == player1)
                    {
                        //: -X- oX-
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                c1.IsLowerThan(i, _trump) &&
                                                                                i.Suit != _trump && 
                                                                                hands[MyIndex].HasX(i.Suit) &&
                                                                                hands[player3].HasA(i.Suit) && //souper ma eso a ja mam kartu na kterou ho musi zahrat
                                                                                ValidCards(c1, i, hands[player3]).All(j => j.Value == Hodnota.Eso && j.Suit == i.Suit));
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }

            };

            yield return new AiRule()
            {
                Order = 6,
                Description = "snaž se vytlačit trumf",
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == -1)
                    {
                        //: -c-
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Value != Hodnota.Eso &&
                                                                                i.Suit != _trump &&
                                                                                ((!hands[player1].HasSuit(i.Suit) &&    //ani jeden souper nezna barvu a aspon jeden ma trumfy nebo
                                                                                  !hands[player3].HasSuit(i.Suit) &&
                                                                                  (hands[player1].HasSuit(_trump) ||
                                                                                   hands[player3].HasSuit(_trump))) ||
                                                                                 (!hands[player3].HasSuit(i.Suit) &&    //3. hrac nezna barvu a ma max. stejne trumfu jako ja tlacnych
                                                                                  hands[player3].HasSuit(_trump) &&
                                                                                  hands[player3].HasAtMostNCardsOfSuit(_trump, hands[MyIndex].CardCount(i.Suit))) ||
                                                                                 (!hands[player3].HasSuit(i.Suit) &&    //3.hrac nezna barvu a ma trumfy a 1.hrac ma max o kartu min v barve nez ja
                                                                                  hands[player3].HasSuit(_trump) &&
                                                                                  hands[player1].HasAtMostNCardsOfSuit(i.Suit, hands[MyIndex].CardCount(i.Suit)-1))));
                    }
                    else if (TeamMateIndex == player1)
                    {
                        //: oc-
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value != Hodnota.Desitka &&
                                                                                i.Value != Hodnota.Eso &&
                                                                                i.Suit != _trump &&
                                                                                !hands[player3].HasSuit(i.Suit) &&
                                                                                hands[player3].HasSuit(_trump));
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }

            };

            yield return new AiRule()
            {
                Order = 7,
                Description = "zůstat ve štychu (nehrát A, X)",
				SkipSimulations = TeamMateIndex == player3,
                ChooseCard2 = (Card c1) =>
                {
                    IEnumerable<Card> cardsToPlay;

                    if (TeamMateIndex == -1)
                    {
                        //: -c-
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value != Hodnota.Eso && 
                                                                                i.Value != Hodnota.Desitka &&
                                                                                i.Suit != _trump &&
                                                                                ValidCards(c1, i, hands[player3]).All(j => Round.WinningCard(c1, i, j, _trump) == i));
                    }
                    else if (TeamMateIndex == player3)
                    {
                        //: -co
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value != Hodnota.Eso && 
                                                                                i.Value != Hodnota.Desitka &&
                                                                                i.Suit != _trump &&
                                                                                c1.IsLowerThan(i, _trump));
                    }
                    else
                    {
                        //: oc-
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value != Hodnota.Eso && 
                                                                                i.Value != Hodnota.Desitka &&
                                                                                i.Suit != _trump &&
                                                                                ValidCards(c1, i, hands[player3]).All(j => i.IsHigherThan(j, _trump)));
                    }

                    return cardsToPlay.OrderByDescending(i => i.Value).LastOrDefault();
                }

            };

            yield return new AiRule()
            {
                Order = 8,
                Description = "hrát dlouhou barvu (mimo A, X)",
				SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = new List<Card>();
                    var maxCount = 0;

                    foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>().Where(i => i != _trump))
                    {
                        var count = ValidCards(c1, hands[MyIndex]).Where(i => i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka).Count(i => i.Suit == barva);

                        if (count > maxCount)
                        {
                            cardsToPlay.Clear();
                            cardsToPlay = new List<Card>
                                                    {
                                                        ValidCards(c1, hands[MyIndex]).Last(i => i.Suit == barva && i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka)
                                                    };
                            maxCount = count;
                        }
                        else if (count == maxCount && count > 0)
                        {
                            cardsToPlay.Add(ValidCards(c1, hands[MyIndex]).Last(i => i.Suit == barva && i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka));
                        }
                    }

                    return cardsToPlay.FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 9,
                Description = "hrát cokoli mimo A, X",
				SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka);

                    return cardsToPlay.OrderByDescending(i => i.Value).LastOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 10,
                Description = "hrát cokoli",
				SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]);

                    return cardsToPlay.ToList().RandomOneOrDefault();
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
                        return cardsToPlay.FirstOrDefault(i => i.Suit != _trump || i.Value != Hodnota.Sedma) ??
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
                    IEnumerable<Card> cardsToPlay;

                    if (TeamMateIndex == -1)
                    {
                        //: --X
                        cardsToPlay =
                            ValidCards(c1, c2, hands[MyIndex])
                                .Where(i => i.Value == Hodnota.Desitka &&
                                            Round.WinningCard(c1, c2, i, _trump) == i);
                    }
                    else if (TeamMateIndex == (MyIndex + 1) % Game.NumPlayers)
                    {
                        //: o-X
                        cardsToPlay =
                            ValidCards(c1, c2, hands[MyIndex])
                                .Where(i => i.Value == Hodnota.Desitka &&
                                            Round.WinningCard(c1, c2, i, _trump) != c2);
                    }
                    else
                    {
                        //: -oX
                        cardsToPlay =
                            ValidCards(c1, c2, hands[MyIndex])
                                .Where(i => i.Value == Hodnota.Desitka &&
                                            Round.WinningCard(c1, c2, i, _trump) != c1);
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule
            {
                Order = 2,
                Description = "hraj vítězné A pokud nemohu chytit soupeřovu X",
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    IEnumerable<Card> cardsToPlay;

                    if (TeamMateIndex == -1)
                    {
                        //: --A
                        //v tomto pripade hraju A take jeste pokud jeden nebo druhy souper muze v dalsi hre brat mou kartu trumfem
                        cardsToPlay =
                            ValidCards(c1, c2, hands[MyIndex])
                                .Where(i => i.Value == Hodnota.Eso &&
								       		i.Suit != _trump &&
                                            Round.WinningCard(c1, c2, i, _trump) == i &&
                                            ((!hands[player1].HasX(i.Suit) &&
                                              !hands[player2].HasX(i.Suit)) ||
                                             (!hands[player1].HasSuit(i.Suit) && hands[player1].HasSuit(_trump)) ||
                                             (!hands[player2].HasSuit(i.Suit) && hands[player2].HasSuit(_trump))));
                    }
                    else if (TeamMateIndex == (MyIndex + 1) % Game.NumPlayers)
                    {
                        //: o-A
                        cardsToPlay =
                            ValidCards(c1, c2, hands[MyIndex])
                                .Where(i => i.Value == Hodnota.Eso &&
								       		i.Suit != _trump &&
                                            Round.WinningCard(c1, c2, i, _trump) != c2 &&
                                            ((!hands[player2].HasX(i.Suit)) ||      //2. hrac nema desitku nebo uz barvu nezna a navic ma jeste nejake trumfy
                                             (!hands[player2].HasSuit(i.Suit) &&
                                              hands[player2].HasSuit(_trump))));
                    }
                    else
                    {
                        //: -oA
                        cardsToPlay =
                            ValidCards(c1, c2, hands[MyIndex])
                                .Where(i => i.Value == Hodnota.Eso &&
								       		i.Suit != _trump &&
                                            Round.WinningCard(c1, c2, i, _trump) != c1 &&
                                            ((!hands[player1].HasX(i.Suit)) ||      //1. hrac nema desitku nebo uz barvu nezna a navic ma jeste nejake trumfy
                                             (!hands[player1].HasSuit(i.Suit) &&
                                              hands[player1].HasSuit(_trump))));
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "zůstat ve štychu (nehrát A, X)",
				SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    IEnumerable<Card> cardsToPlay;

                    if (TeamMateIndex == -1)
                    {
                        //: --c
                        cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka &&
                                                                                    Round.WinningCard(c1, c2, i, _trump) == i);
                    }
                    else if (TeamMateIndex == player1)
                    {
                        //: o-c
                        cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka &&
                                                                                    Round.WinningCard(c1, c2, i, _trump) != c2);
                    }
                    else
                    {
                        //: -oc
                        cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka &&
                                                                                    Round.WinningCard(c1, c2, i, _trump) != c1);
                    }
                    return cardsToPlay.OrderByDescending(i => i.Value).LastOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 4,
                Description = "hrát dlouhou barvu (mimo A, X)",
				SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = new List<Card>();
                    var maxCount = 0;

                    foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>().Where(i => i != _trump))
                    {
                        var count = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka).Count(i => i.Suit == barva);

                        if (count > maxCount)
                        {
                            cardsToPlay.Clear();
                            cardsToPlay = new List<Card>
                                                    {
                                                        ValidCards(c1, c2, hands[MyIndex]).Last(i => i.Suit == barva && i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka)
                                                    };
                            maxCount = count;
                        }
                        else if (count == maxCount && count > 0)
                        {
                            cardsToPlay.Add(ValidCards(c1, c2, hands[MyIndex]).Last(i => i.Suit == barva && i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka));
                        }
                    }

                    return cardsToPlay.FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 5,
                Description = "hrát cokoli mimo A, X",
				SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Value != Hodnota.Eso && i.Value != Hodnota.Desitka);

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 6,
                Description = "hrát cokoli",
				SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]);

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };
        }
    }
}
