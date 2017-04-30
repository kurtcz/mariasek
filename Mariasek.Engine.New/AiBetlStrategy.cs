using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
{
    public class AiBetlStrategy : AiStrategyBase
    {
        public AiBetlStrategy(Barva? trump, Hra gameType, Hand[] hands, Round[] rounds, List<Barva> teamMatesSuits, Probability probabilities)
            : base(trump, gameType, hands, rounds, teamMatesSuits, probabilities)
        {
        }

        protected override IEnumerable<AiRule> GetRules1(Hand[] hands)
        {
            var player2 = (MyIndex + 1) % Game.NumPlayers;
            var player3 = (MyIndex + 2) % Game.NumPlayers;
            var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                           ? (MyIndex + 2) % Game.NumPlayers : (MyIndex + 1) % Game.NumPlayers;

			Barva? bannedSuit = null;

			if (RoundNumber == 1 && TeamMateIndex == -1)
			{
				yield return new AiRule()
				{
					Order = 0,
					Description = "Vytlač jedinou díru v barvě",
					SkipSimulations = true,
					ChooseCard1 = () =>
					{
                        var cardsToPlay = hands[MyIndex].Where(i => i.Value == Hodnota.Sedma && hands[MyIndex].CardCount(i.Suit) == 7 && hands[MyIndex].HasA(i.Suit));

						return cardsToPlay.ToList().RandomOneOrDefault();
					}
				};
			}

			if (RoundNumber == 2 && _rounds != null && _rounds[0] != null) //pri simulaci hry jsou skutecny kola jeste neodehrany
			{
				if (_rounds[0].c1.Suit == _rounds[0].c2.Suit && _rounds[0].c1.Suit == _rounds[0].c3.Suit)
				{
					bannedSuit = _rounds[0].c1.Suit;
				}

				if (bannedSuit.HasValue)
				{
					yield return new AiRule()
					{
						Order = 0,
						Description = "Hraj A v jiné barvě",
						SkipSimulations = true,
						ChooseCard1 = () =>
						{
							IEnumerable<Card> cardsToPlay = Enumerable.Empty<Card>();

							cardsToPlay = hands[MyIndex].Where(i => i.Suit != bannedSuit.Value && i.Value == Hodnota.Eso);

							return cardsToPlay.ToList().RandomOneOrDefault();
						}
					};
				}
			}

            yield return new AiRule()
            {
                Order = 1,
                Description = "Hraj vítěznou kartu",
				//UseThreshold = true, //protoze generovani je nahodne a casto generuje nebetlove rozlozeni
                ChooseCard1 = () =>
                {
                    IEnumerable<Card> cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == player2)//co-
                    {
                        cardsToPlay = hands[MyIndex].Where(i =>
		                                    (!bannedSuit.HasValue || i.Suit != bannedSuit.Value) &&
                                            ValidCards(i, hands[player2]).Any(j =>
                                                ValidCards(i, j, hands[player3]).All(k =>
                                                    Round.WinningCard(i, j, k, null) == k)));
                    }
                    else if (TeamMateIndex == player3)//c-o
                    {
                        cardsToPlay = hands[MyIndex].Where(i =>
		                                    (!bannedSuit.HasValue || i.Suit != bannedSuit.Value) &&
                                            ValidCards(i, hands[player2]).All(j =>
                                                ValidCards(i, j, hands[player3]).Any(k =>
                                                    Round.WinningCard(i, j, k, null) == j)));
                    }

					if (RoundNumber == 2 && _rounds != null && _rounds[0] != null) //pri simulaci hry jsou skutecny kola jeste neodehrany
					{
						//zahrat viteznou kartu v 2. kole (kolega asi nezna barvu voliciho hrace z prvniho kola)
						var winner = cardsToPlay.FirstOrDefault(i => i.Suit == _rounds[0].c1.Suit);

						if (winner != null)
						{
							return winner;
						}
					}
                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "Odmazat si vysokou kartu",
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

                    if (TeamMateIndex == -1)//c--
                    {
                        var lo = hands[MyIndex].Where(i => //vezmi karty nizsi nez souperi
										hands[player2].Any(j => j.Suit == i.Suit && j.IsHigherThan(i, null)) ||
                                            hands[player3].Any(j => j.Suit == i.Suit && j.IsHigherThan(i, null)));
                        var hi = lo.ToDictionary(k => k, v => //vezmi karty Vyssi nez souperi
                                        Math.Max(hands[player2].Count(j => j.Suit == v.Suit && v.IsHigherThan(j, null)),
                                                 hands[player3].Count(j => j.Suit == v.Suit && v.IsHigherThan(j, null))))
                                   .OrderByDescending(i => i.Value)
                                   .ThenByDescending(i => i.Key.BadValue);

                        if (hi.Any())
                        {
                            cardsToPlay.Add(hi.First().Key);
                        }
                        //je treba odmazavat pokud to jde, v nejhorsim hru neuhraju, simulace by mely ukazat
                    }
                    else
                    {
                        //hi1 = pocet mych karet > nejmensi souperova
                        //hi2 = pocet kolegovych karet > nejmensi souperova
                        //mid1 = pocet souperovych karet > nejmensi moje
                        //mid2 = pocet souperovych karet > nejmensi kolegy
						foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>().Where(i => (!bannedSuit.HasValue || i != bannedSuit.Value)))
                        {
                            if(hands[MyIndex].HasSuit(barva) && hands[opponent].HasSuit(barva))
                            {
                                var low1 = hands[MyIndex].Min(barva, null);
                                var low2 = hands[TeamMateIndex].Min(barva, null);
                                var oplow = hands[opponent].Min(barva, null);
                                var lowCard1 = new Card(barva, low1);
                                var lowCard2 = new Card(barva, low2);
                                var oplowCard = new Card(barva, oplow);
                                var hi1 = hands[MyIndex].Count(i => i.Suit == barva && (Hodnota)i.BadValue > oplow);
                                var hi2 = hands[TeamMateIndex].Count(i => i.Suit == barva && (Hodnota)i.BadValue > oplow);
                                var mid1 = hands[opponent].Count(i => i.Suit == barva && (Hodnota)i.BadValue > low1);
                                var mid2 = hands[opponent].Count(i => i.Suit == barva && (Hodnota)i.BadValue > low2);

                                //odmazavat ma smysl jen tehdy pokud:
								//mame nejmensi kartu a
								//nasich vysokych karet je mene nez souperovych strednich karet
								//(aby souperovi nejake karty zbyly pote co si vysoke odmazeme)
                                if ((lowCard1.IsLowerThan(oplowCard, null) && hi2 > 0 && hi2 < mid1) ||
                                    (lowCard2.IsLowerThan(oplowCard, null) && hi1 > 0 && hi1 < mid2))
                                {
                                    cardsToPlay.Add(hands[MyIndex].Where(i => i.Suit == barva)
                                                                  .OrderByDescending(i => i.BadValue)
                                                                  .First());
                                }
                            }
                        }
                    }

					return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

			yield return new AiRule()
			{
				Order = 3,
				Description = "Vytlačit soupeřovu vyšší kartu",
				ChooseCard1 = () =>
				{
					var cardsToPlay = new List<Card>();

					if (TeamMateIndex == -1)
					{
						var hi = hands[MyIndex].Where(i => //vezmi karty vyssi nez souperi
									hands[player2].Any(j => j.Suit == i.Suit && i.IsHigherThan(j, null)) ||
										hands[player3].Any(j => j.Suit == i.Suit && i.IsHigherThan(j, null)));
						var lo = hands[MyIndex].Where(i => //vezmi karty nizsi nez souperii
									(hands[player2].Any(j => j.Suit == i.Suit && j.IsHigherThan(j, null)) ||
									 hands[player3].Any(j => j.Suit == i.Suit && j.IsHigherThan(j, null))) &&
									hi.Any(j => j.Suit == i.Suit && j.IsHigherThan(i, null)));//v barve kde mam i vysoke kartyy

						cardsToPlay = lo.ToList();
					}

					return cardsToPlay.RandomOneOrDefault();
				}
			};

			yield return new AiRule()
			{
				Order = 4,
				Description = "Odmazat spoluhráčovu kartu",
				ChooseCard1 = () =>
				{
					var cardsToPlay = new List<Card>();

					if (TeamMateIndex == player2)	//co-
					{
						cardsToPlay = hands[MyIndex].Where(i => (!bannedSuit.HasValue || i.Suit != bannedSuit.Value) &&
																!hands[player2].HasSuit(i.Suit) && 
						                                   		hands[player3].HasSuit(i.Suit)).ToList();
					}
					else if (TeamMateIndex == player3)	//c-o
					{
						cardsToPlay = hands[MyIndex].Where(i => (!bannedSuit.HasValue || i.Suit != bannedSuit.Value) &&
																hands[player2].HasSuit(i.Suit) &&
																!hands[player3].HasSuit(i.Suit)).ToList();
					}

					return cardsToPlay.RandomOneOrDefault();
				}
			};

			yield return new AiRule()
			{
				Order = 5,
				Description = "Dostat spoluhráče do štychu",
				ChooseCard1 = () =>
				{
					var cardsToPlay = new List<Card>();

					if (TeamMateIndex == player2)   //co--
					{
						var winningCards = hands[player2].Where(i =>												//ma spoluhrac viteznou kartu?
																ValidCards(i, hands[player3]).All(j =>
																  	ValidCards(i, j, hands[MyIndex]).Any(k =>
																	   	Round.WinningCard(i, j, k, null) == j)));
						if (winningCards.Any())
						{
							//je karta kterou ho dostanu do stychu aniz by pritom musel hrat viteznou kartu?
							cardsToPlay = hands[MyIndex].Where(i => (!bannedSuit.HasValue || i.Suit != bannedSuit.Value) &&
																	hands[player2].Any(j => !i.IsHigherThan(j, null) && 
	                                                           		!winningCards.Contains(j))).ToList();
						}
					}
					else if (TeamMateIndex == player3)  //c-oo
					{
						var winningCards = hands[player3].Where(i =>                                                //ma spoluhrac viteznou kartu??
																ValidCards(i, hands[MyIndex]).Any(j =>
																  	ValidCards(i, j, hands[player2]).Any(k =>
																	   	Round.WinningCard(i, j, k, null) == k)));
						if (winningCards.Any())
						{
							//je karta kterou ho dostanu do stychu aniz by pritom musel hrat viteznou kartu??
							cardsToPlay = hands[MyIndex].Where(i => (!bannedSuit.HasValue || i.Suit != bannedSuit.Value) &&
																	hands[player3].Any(j => !i.IsHigherThan(j, null) &&
																    !winningCards.Contains(j))).ToList();
						}
					}

					return cardsToPlay.RandomOneOrDefault();
				}
			};

			yield return new AiRule()
            {
                Order = 6,
                Description = "Hrát krátkou barvu",
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

					var lo = hands[MyIndex].Where(i => (!bannedSuit.HasValue || i.Suit != bannedSuit.Value))
					                       .GroupBy(g => g.Suit);   //seskup podle barev
                    //vyber nejkratsi barvu
                    cardsToPlay = lo.OrderBy(g => g.Count()).Select(g => g.ToList()).FirstOrDefault();

					return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault(); //nejmensi karta
                }
            };
        }

        protected override IEnumerable<AiRule> GetRules2(Hand[] hands)
        {
            var player3 = (MyIndex + 1) % Game.NumPlayers;
            var player1 = (MyIndex + 2) % Game.NumPlayers;
            var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
               ? (MyIndex + 2) % Game.NumPlayers : (MyIndex + 1) % Game.NumPlayers;

            yield return new AiRule()
            {
                Order = 0,
                Description = "Hraj vítěznou kartu",
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == player1)//oc-
                    {
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i =>
                                               ValidCards(c1, i, hands[player3]).All(j =>
                                                   Round.WinningCard(c1, i, j, null) == j));
                    }
                    else if (TeamMateIndex == player3)//-co
                    {
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i =>
                                               ValidCards(c1, i, hands[player3]).Any(j =>
                                                   Round.WinningCard(c1, i, j, null) == c1));
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 1,
                Description = "Odmazat si vysokou kartu",
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = new List<Card>();

                    if (TeamMateIndex == -1)//-c-
                    {
                        var lo = ValidCards(c1, hands[MyIndex]).Where(i =>
                            //vezmi karty nizsi nez souperi
                                            hands[player1].Any(j => j.Suit == i.Suit && j.IsHigherThan(i, null)) ||
                                                hands[player3].Any(j => j.Suit == i.Suit && j.IsHigherThan(i, null)));
                        var hi = lo.Where(i =>
                            //vezmi karty Vyssi nez souperi
                                        hands[player1].Any(j => j.Suit == i.Suit && i.IsHigherThan(j, null)) ||
                                            hands[player3].Any(j => j.Suit == i.Suit && i.IsHigherThan(j, null)))
                                   .GroupBy(g => g.Suit);   //seskup podle barev
                        //vyber z nizkych karet jen karty kde je max 1 karta vyssi nez souperovy
                        //cardsToPlay = hi.Where(g => g.Count() == 1).SelectMany(g => g).ToList();
                        cardsToPlay = hi.SelectMany(g => g).ToList(); //podminka s jednou kartou nefunguje
                        //je treba odmazavat pokud to jde, v nejhorsim hru neuhraju, simulace by mely ukazat
                    }
                    else //oc-
                    {
                        //hi1 = pocet mych karet > nejmensi souperova
                        //hi2 = pocet kolegovych karet > nejmensi souperova
                        //mid1 = pocet souperovych karet > nejmensi moje
                        //mid2 = pocet souperovych karet > nejmensi kolegy
                        foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                        {
                            if (ValidCards(c1, hands[MyIndex]).HasSuit(barva) && ValidCards(c1, hands[opponent]).HasSuit(barva))
                            {
                                var low1 = hands[MyIndex].Min(barva, null);
                                var low2 = hands[TeamMateIndex].Min(barva, null);
                                var oplow = hands[opponent].Min(barva, null);
                                var lowCard1 = new Card(barva, low1);
                                var lowCard2 = new Card(barva, low2);
                                var oplowCard = new Card(barva, oplow);
								var hi1 = hands[MyIndex].Count(i => i.Suit == barva && (Hodnota)i.BadValue > oplow);
                                var hi2 = hands[TeamMateIndex].Count(i => i.Suit == barva && (Hodnota)i.BadValue > oplow);
                                var mid1 = hands[opponent].Count(i => i.Suit == barva && (Hodnota)i.BadValue > low1);
								var mid2 = hands[opponent].Count(i => i.Suit == barva && (Hodnota)i.BadValue > low2);

                                //odmazavat ma smysl jen tehdy pokud je nasich vysokych karet mene nez souperovych strednich karet
                                if ((lowCard1.IsLowerThan(oplowCard, null) && hi2 < mid1) ||
                                    (lowCard2.IsLowerThan(oplowCard, null) && hi1 < mid2)) 
                                {
                                    cardsToPlay.Add(ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == barva)
                                                                                  .OrderByDescending(i => i.BadValue)
                                                                                  .First());
                                }
                            }
                        }
                    }

                    return cardsToPlay.RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "Odmazat si barvu",
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = new List<Card>();
                    var lowCards = new List<Card>();

                    if (TeamMateIndex == player1)
                    {
                        lowCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota))
                                                                 .Cast<Hodnota>()
                                                                 .All(h => (int)h > i.BadValue ||
                                                                           _probabilities.CardProbability(player3, new Card(i.Suit, h)) == 0)).ToList();
                    }
                    else if (TeamMateIndex == player3)
                    {
                        lowCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota))
                                                                 .Cast<Hodnota>()
                                                                 .All(h => (int)h > i.BadValue ||
                                                                           _probabilities.CardProbability(player1, new Card(i.Suit, h)) == 0)).ToList();
                    }
                    var cards = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit != c1.Suit &&
                                                                          !lowCards.Contains(i))         //nejnizsi karty v barve nema smysl odmazavat
                                                              .GroupBy(i => i.Suit)
                                                              .OrderBy(g => g.Count())
                                                              .Select(g => g.ToList()).FirstOrDefault();
                    if (cards != null)
                    {
                        cardsToPlay = cards;
                    }

					return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "Hrát krátkou barvu",
                ChooseCard2 = (Card c1) =>
                {
                    var lo = ValidCards(c1, hands[MyIndex]).GroupBy(g => g.Suit);   //seskup podle barev
                    //vyber nejkratsi barvu
                    var cardsToPlay = lo.OrderBy(g => g.Count()).Select(g => g.ToList()).FirstOrDefault();

                    if (TeamMateIndex == player3)
                    {
                        return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                    }
                    else
                    {
                        return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                    }
                }
            };
        }

        protected override IEnumerable<AiRule> GetRules3(Hand[] hands)
        {
            var player1 = (MyIndex + 1) % Game.NumPlayers;
            var player2 = (MyIndex + 2) % Game.NumPlayers;
            var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                           ? (MyIndex + 2) % Game.NumPlayers : (MyIndex + 1) % Game.NumPlayers;

            yield return new AiRule()
            {
                Order = 0,
                Description = "Hraj vítěznou kartu",
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == player1)//o-c
                    {
                        cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i =>
                                                    Round.WinningCard(c1, c2, i, null) == c2);
                    }
                    else if(TeamMateIndex == player2)//-oc
                    {
                        cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i =>
                                                    Round.WinningCard(c1, c2, i, null) == c1);
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 1,
                Description = "Odmazat si vysokou kartu",
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = new List<Card>();

                    if (TeamMateIndex == -1)//--c
                    {
                        var lo = ValidCards(c1, c2, hands[MyIndex]).Where(i =>
                            //vezmi karty nizsi nez souperi
                                            hands[player1].Any(j => j.Suit == i.Suit && j.IsHigherThan(i, null)) ||
                                                hands[player2].Any(j => j.Suit == i.Suit && j.IsHigherThan(i, null)));
                        var hi = lo.Where(i =>
                            //vezmi karty Vyssi nez souperi
                                        hands[player1].Any(j => j.Suit == i.Suit && i.IsHigherThan(j, null)) ||
                                            hands[player2].Any(j => j.Suit == i.Suit && i.IsHigherThan(j, null)))
                                    .GroupBy(g => g.Suit);   //seskup podle barev
                        //vyber z nizkych karet jen karty kde je max 1 karta vyssi nez souperovy
                        cardsToPlay = hi.SelectMany(g => g).ToList(); //podminka s jednou kartou nefunguje
                        //je treba odmazavat pokud to jde, v nejhorsim hru neuhraju, simulace by mely ukazat
                    }
                    else
                    {
                        //hi1 = pocet mych karet > nejmensi souperova
                        //hi2 = pocet kolegovych karet > nejmensi souperova
                        //mid1 = pocet souperovych karet > nejmensi moje
                        //mid2 = pocet souperovych karet > nejmensi kolegy
                        foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                        {
                            if (ValidCards(c1, hands[MyIndex]).HasSuit(barva) && ValidCards(c1, hands[opponent]).HasSuit(barva))
                            {
                                var low1 = hands[MyIndex].Min(barva, null);
                                var low2 = hands[TeamMateIndex].Min(barva, null);
                                var oplow = hands[opponent].Min(barva, null);
                                var lowCard1 = new Card(barva, low1);
                                var lowCard2 = new Card(barva, low2);
                                var oplowCard = new Card(barva, oplow);
								var hi1 = hands[MyIndex].Count(i => i.Suit == barva && (Hodnota)i.BadValue > oplow);
                                var hi2 = hands[TeamMateIndex].Count(i => i.Suit == barva && (Hodnota)i.BadValue > oplow);
                                var mid1 = hands[opponent].Count(i => i.Suit == barva && (Hodnota)i.BadValue > low1);
                                var mid2 = hands[opponent].Count(i => i.Suit == barva && (Hodnota)i.BadValue > low2);

                                //odmazavat ma smysl jen tehdy pokud nasich vysokych karet neni vice nez souperovych strednich karet
                                if ((lowCard1.IsLowerThan(oplowCard, null) && hi2 <= mid1) ||
                                    (lowCard2.IsLowerThan(oplowCard, null) && hi1 <= mid2))
                                {
                                    cardsToPlay.Add(ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == barva)
                                                                                  .OrderByDescending(i => i.BadValue)
                                                                                  .First());
                                }
                            }
                        }
                    }

                    return cardsToPlay.RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "Odmazat si barvu",
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = new List<Card>();
                    var lowCards = new List<Card>();

                    if (TeamMateIndex == player1)
                    {
                        lowCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota))
                                                                 .Cast<Hodnota>()
                                                                 .All(h => (int)h > i.BadValue ||
                                                                           _probabilities.CardProbability(player2, new Card(i.Suit, h)) == 0)).ToList();
                    }
                    else
                    {
                        lowCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota))
                                                                 .Cast<Hodnota>()
                                                                 .All(h => (int)h > i.BadValue ||
                                                                           _probabilities.CardProbability(player1, new Card(i.Suit, h)) == 0)).ToList();
                    }
                    var cards = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit != c1.Suit &&
                                                                              !lowCards.Contains(i))
                                                                  .GroupBy(i => i.Suit)
                                                                  .OrderBy(g => g.Count())
                                                                  .Select(g => g.ToList()).FirstOrDefault();
                    if(cards != null)
                    {
                        cardsToPlay = cards;
                    }

                    return cardsToPlay.RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "Hrát kratkou barvu",
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var lo = ValidCards(c1, c2, hands[MyIndex]).GroupBy(g => g.Suit);   //seskup podle barev
                    //vyber nejkratsi barvu
                    var cardsToPlay = lo.OrderBy(g => g.Count()).Select(g => g.ToList()).FirstOrDefault();

                    return cardsToPlay.RandomOneOrDefault();
                }
            };
        }
    }
}
