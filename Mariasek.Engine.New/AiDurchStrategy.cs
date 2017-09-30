using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
{
    public class AiDurchStrategy : AiStrategyBase
    {
        public AiDurchStrategy(Barva? trump, Hra gameType, Hand[] hands, Round[] rounds, List<Barva> teamMatesSuits, Probability probabilities)
            :base(trump, gameType, hands, rounds, teamMatesSuits, probabilities)
        {
        }

        protected override IEnumerable<AiRule> GetRules1(Hand[] hands)
        {
            var player2 = (MyIndex + 1) % Game.NumPlayers;
            var player3 = (MyIndex + 2) % Game.NumPlayers;

            yield return new AiRule()
            {
                Order = 0,
                Description = "hrát od A nejdelší vítěznou barvu",
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();
                    var minCard = Hodnota.Sedma;

                    foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                    {
                        var cards = hands[MyIndex].Where(i => i.Suit == barva && 
                                                              ValidCards(i, hands[player2]).All(j =>
                                                                ValidCards(i, j, hands[player3]).All(k =>
                                                                    Round.WinningCard(i, j, k, null) == i)))
                                                  .OrderByDescending(i => i.BadValue)
                                                  .ToList();
                        var hi = cards.FirstOrDefault();
                        var lo = cards.LastOrDefault();

						if(lo != null && (Hodnota)lo.BadValue <= minCard)
                        {
							minCard = (Hodnota)lo.BadValue;
                            cardsToPlay.Clear();
                            cardsToPlay.Add(hi);
                        }
                    }

                    return cardsToPlay.FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 1,
                Description = "hrát nejdelší barvu",
                ChooseCard1 = () =>
                {
                    IEnumerable<Card> cardsToPlay = Enumerable.Empty<Card>();
                    var maxCount = 0;

                    foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                    {
                        var cards = hands[MyIndex].Where(i => i.Suit == barva).ToList();

                        if (cards.Count > maxCount)
                        {
                            maxCount = cards.Count;
                            cardsToPlay = cards;
                        }
                    }

                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };
        }

        protected override IEnumerable<AiRule> GetRules2(Hand[] hands)
        {
            var player3 = (MyIndex + 1) % Game.NumPlayers;
            var player1 = (MyIndex + 2) % Game.NumPlayers;

            yield return new AiRule()
            {
                Order = 0,
                Description = "hrát vítěznou kartu",
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => !c1.IsHigherThan(i, null));

                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 1,
                Description = "hrát nejmenší kartu v barvě",
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == c1.Suit);

                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                }
            };

			yield return new AiRule()
			{
				Order = 2,
				Description = "hrát barvu kde asi nechytám",
				ChooseCard2 = (Card c1) =>
				{
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => _probabilities.SuitProbability(player1, i.Suit, RoundNumber) == 0)
																.OrderByDescending(i => i.BadValue);

                    if (!cardsToPlay.Any())
                    {
                        //barvy kde nejsou diry vyjma nizkych karet
                        var suits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                        .Where(b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                        .Where(h => h != Hodnota.Eso &&
                                                                    _probabilities.CardProbability(player1, new Card(b, h)) > 0)
                                                        .All(h => h < Hodnota.Spodek || h == Hodnota.Desitka));
                        cardsToPlay = ValidCards(hands[MyIndex]).Where(i => suits.Contains(i.Suit))
                                                                .OrderByDescending(i => i.BadValue);
                    }

					return cardsToPlay.FirstOrDefault();
				}
			};

			yield return new AiRule()
			{
				Order = 3,
				Description = "ukázat barvu kterou chytám",
				ChooseCard2 = (Card c1) =>
				{
					var aces = Enum.GetValues(typeof(Barva)).Cast<Barva>()
								   .Where(b => hands[MyIndex].HasA(b));
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => aces.Contains(i.Suit) &&
																			i.Value != Hodnota.Eso)
																.OrderByDescending(i => i.BadValue);
					return cardsToPlay.FirstOrDefault();
				}
			};

			yield return new AiRule()
            {
                Order = 4,
                Description = "hrát spoluhráčovu barvu",
                ChooseCard2 = (Card c1) =>
                {
                    var opponentsCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                                      .Select(r => r.player2.PlayerIndex == TeamMateIndex
                                                                    ? r.c2
                                                                    : r.c3).ToList();
                    var topOpponentCardPlayedPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                           .ToDictionary(b => b,
                                                                         b => opponentsCardsPlayed.Where(i => i.Suit == b)
                                                                                                  .OrderByDescending(i => i.BadValue)
                                                                                                  .FirstOrDefault());
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => topOpponentCardPlayedPerSuit[i.Suit] != null &&
                                                                            i.BadValue < topOpponentCardPlayedPerSuit[i.Suit].BadValue)
                                                                .OrderBy(i => i.BadValue);

                    return cardsToPlay.FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 5,
                Description = "hrát nejmenší kartu v barvě, ve které chytám",
                ChooseCard2 = (Card c1) =>
                {
                    var topCardsPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                              .ToDictionary(b => b,
                                                            b => hands[MyIndex].Where(i => i.Suit == b)
                                                                               .OrderByDescending(i => i.BadValue)
                                                                               .FirstOrDefault());
                    var holesPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                           .ToDictionary(b => b,
                                                         b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                  .Where(h => topCardsPerSuit[b] != null &&
                                                                              new Card(Barva.Cerveny, h).BadValue < topCardsPerSuit[b].BadValue)
                                                                  .Count(h => _probabilities.CardProbability(player1, new Card(b, h)) > 0));
					var opponentTopCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
											   .ToDictionary(b => b,
															 b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
															 		  .Where(h => topCardsPerSuit[b] != null &&
																	 			  new Card(Barva.Cerveny, h).BadValue > topCardsPerSuit[b].BadValue)
																	  .Count(h => _probabilities.CardProbability(player1, new Card(b, h)) > 0));
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => topCardsPerSuit[i.Suit] != null &&
																			holesPerSuit[i.Suit] > 0 &&
																			opponentTopCards[i.Suit] > 0 &&
                                                                            opponentTopCards[i.Suit] < hands[MyIndex].CardCount(i.Suit) - 1)
                                                                .OrderByDescending(i => hands[MyIndex].CardCount(i.Suit) - opponentTopCards[i.Suit])
                                                                .ThenBy(i => i.BadValue);

                    return cardsToPlay.FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 6,
                Description = "hrát nejmenší kartu",
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]);

                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                }
            };
        }

        protected override IEnumerable<AiRule> GetRules3(Hand[] hands)
        {
            var player1 = (MyIndex + 1) % Game.NumPlayers;
            var player2 = (MyIndex + 2) % Game.NumPlayers;

            yield return new AiRule()
            {
                Order = 0,
                Description = "hrát vítěznou kartu",
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => !c1.IsHigherThan(i, null));

                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 1,
                Description = "hrát nejmenší kartu v barvě",
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit == c1.Suit);

                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                }
            };

			yield return new AiRule()
			{
				Order = 2,
				Description = "hrát barvu kde asi nechytám",
				ChooseCard3 = (Card c1, Card c2) =>
				{
					var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => _probabilities.SuitProbability(player1, i.Suit, RoundNumber) == 0)
																.OrderByDescending(i => i.BadValue);

					if (!cardsToPlay.Any())
					{
						//barvy kde nejsou diry vyjma nizkych karet
						var suits = Enum.GetValues(typeof(Barva)).Cast<Barva>()
										.Where(b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
														.Where(h => h != Hodnota.Eso &&
																	_probabilities.CardProbability(player1, new Card(b, h)) > 0)
														.All(h => h < Hodnota.Spodek || h == Hodnota.Desitka));
						cardsToPlay = ValidCards(hands[MyIndex]).Where(i => suits.Contains(i.Suit))
																.OrderByDescending(i => i.BadValue);
					}

					return cardsToPlay.FirstOrDefault();
                }
			};

			yield return new AiRule()
			{
				Order = 3,
				Description = "ukázat barvu kterou chytám",
				ChooseCard3 = (Card c1, Card c2) =>
				{
					var aces = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                   .Where(b => hands[MyIndex].HasA(b));
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => aces.Contains(i.Suit) &&
                                                                            i.Value != Hodnota.Eso)
																.OrderByDescending(i => i.BadValue);

					return cardsToPlay.FirstOrDefault();
				}
			};

			yield return new AiRule()
			{
				Order = 4,
				Description = "hrát nízkou kartu ve spoluhráčově barvě",
				ChooseCard3 = (Card c1, Card c2) =>
				{
					var opponentsCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
													  .Select(r => r.player2.PlayerIndex == TeamMateIndex
																	? r.c2
																	: r.c3).ToList();
					var topOpponentCardPlayedPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
														   .ToDictionary(b => b,
																		 b => opponentsCardsPlayed.Where(i => i.Suit == b)
																								  .OrderByDescending(i => i.BadValue)
																								  .FirstOrDefault());
					var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => topOpponentCardPlayedPerSuit[i.Suit] != null &&
																			i.BadValue < topOpponentCardPlayedPerSuit[i.Suit].BadValue)
																.OrderBy(i => i.BadValue);

					return cardsToPlay.FirstOrDefault();
				}
			};

			yield return new AiRule()
			{
				Order = 5,
				Description = "hrát nejmenší kartu v barvě, ve které chytám",
				ChooseCard3 = (Card c1, Card c2) =>
				{
					var topCardsPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
											  .ToDictionary(b => b,
															b => hands[MyIndex].Where(i => i.Suit == b)
																			   .OrderByDescending(i => i.BadValue)
																			   .FirstOrDefault());
					var holesPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
											  .ToDictionary(b => b,
															b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
																	 .Where(h => topCardsPerSuit[b] != null &&
																				 new Card(Barva.Cerveny, h).BadValue < topCardsPerSuit[b].BadValue)
																	 .Count(h => _probabilities.CardProbability(player1, new Card(b, h)) > 0));
					var opponentTopCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
											  .ToDictionary(b => b,
															b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
																	 .Where(h => topCardsPerSuit[b] != null &&
																				 new Card(Barva.Cerveny, h).BadValue > topCardsPerSuit[b].BadValue)
																	 .Count(h => _probabilities.CardProbability(player1, new Card(b, h)) > 0));
                    var cardsToPlay = ValidCards(hands[MyIndex]).Where(i => topCardsPerSuit[i.Suit] != null &&
                                                                            holesPerSuit[i.Suit] > 0 &&
                                                                            opponentTopCards[i.Suit] > 0 &&
                                                                            opponentTopCards[i.Suit] < hands[MyIndex].CardCount(i.Suit) - 1)
                                                                .OrderByDescending(i => hands[MyIndex].CardCount(i.Suit) - opponentTopCards[i.Suit])
                                                                .ThenBy(i => i.BadValue);

					return cardsToPlay.FirstOrDefault();
				}
			};

            yield return new AiRule()
            {
                Order = 6,
                Description = "hrát nejmenší kartu",
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]);

                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                }
            };
        }
    }
}
