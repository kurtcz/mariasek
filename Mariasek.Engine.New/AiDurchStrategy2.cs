using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
{
    public class AiDurchStrategy2 : AiStrategyBase
    {
        public AiDurchStrategy2(Barva? trump, Hra gameType, Hand[] hands, Round[] rounds, List<Barva> teamMatesSuits, Probability probabilities)
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
                SkipSimulations = true,
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();
                    var minCard = Hodnota.Sedma;

                    foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                    {
                        var cards = hands[MyIndex].Where(i => i.Suit == barva && 
                                                             _probabilities.SuitHigherThanCardProbability(player2, i, RoundNumber, false) == 0 &&
                                                             _probabilities.SuitHigherThanCardProbability(player3, i, RoundNumber, false) == 0)
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
                SkipSimulations = true,
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
                SkipSimulations = true,
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
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == c1.Suit);

                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "hrát nejmenší kartu v barvě, ve které nechytám",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = new List<Card>();
                    var maxCard = Hodnota.Eso;

                    foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                    {
                        var cards = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == barva && 
                                                                              Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                  .Where(h => (int)h < i.BadValue)
                                                                                  .All(h => _probabilities.CardProbability(player1, new Card(i.Suit, h)) == 0))
                                                  .OrderByDescending(i => i.Value)
                                                  .ToList();
                        var hi = cards.FirstOrDefault();
                        var lo = cards.LastOrDefault();

                        if(hi != null && hi.Value >= maxCard)
                        {
                            maxCard = lo.Value;
                            cardsToPlay.Clear();
                            cardsToPlay.Add(lo);
                        }
                    }

                    return cardsToPlay.FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "hrát nejmenší kartu",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]);

                    return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
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
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => !c1.IsHigherThan(i, null));

                    return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 1,
                Description = "hrát nejmenší kartu v barvě",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit == c1.Suit);

                    return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "hrát nejmenší kartu v barvě, ve které nechytám",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = new List<Card>();
                    var maxCard = Hodnota.Eso;

                    foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                    {
                        var cards = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit == barva &&
                                                                                  Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                      .Where(h => (int)h<i.BadValue)
                                                                                      .All(h => _probabilities.CardProbability(player1, new Card(i.Suit, h)) == 0))
                                                  .OrderByDescending(i => i.Value)
                                                  .ToList();
                        var hi = cards.FirstOrDefault();
                        var lo = cards.LastOrDefault();

                        if (hi != null && hi.Value >= maxCard)
                        {
                            maxCard = lo.Value;
                            cardsToPlay.Clear();
                            cardsToPlay.Add(lo);
                        }
                    }

                    return cardsToPlay.FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "hrát nejmenší kartu",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]);

                    return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                }
            };
        }
    }
}
