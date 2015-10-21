using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
{
    public class AiDurchStrategy : AiStrategyBase
    {
        public AiDurchStrategy(Barva? trump, Hra gameType, Hand[] hands)
            :base(trump, gameType, hands)
        {
        }

        protected override IEnumerable<AiRule> GetRules1(Hand[] hands)
        {
            var player2 = (MyIndex + 1) % Game.NumPlayers;
            var player3 = (MyIndex + 2) % Game.NumPlayers;

            yield return new AiRule()
            {
                Order = 0,
                Description = "hrat od A nejdelsi viteznou barvu",
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
                                                  .OrderByDescending(i => i.Value)
                                                  .ToList();
                        var hi = cards.FirstOrDefault();
                        var lo = cards.LastOrDefault();

                        if(lo != null && lo.Value <= minCard)
                        {
                            minCard = lo.Value;
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
                Description = "hrat nejdelsi barvu",
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

                    return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
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
                Description = "hrat viteznou kartu",
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => !c1.IsHigherThan(i, null));

                    return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 1,
                Description = "hrat nejmensi kartu v barve",
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == c1.Suit);

                    return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "hrat nejmensi kartu v barve, ve ktere nechytam",
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = new List<Card>();
                    var maxCard = Hodnota.Eso;

                    foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                    {
                        var cards = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == barva && 
                                                                                hands[player1].Where(j => j.Suit == barva)
                                                                                              .All(k => k.Value > i.Value))
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
                Description = "hrat nejmensi kartu",
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
                Description = "hrat viteznou kartu",
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => !c1.IsHigherThan(i, null));

                    return cardsToPlay.OrderByDescending(i => i.Value).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 1,
                Description = "hrat nejmensi kartu v barve",
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit == c1.Suit);

                    return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "hrat nejmensi kartu v barve, ve ktere nechytam",
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = new List<Card>();
                    var maxCard = Hodnota.Eso;

                    foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                    {
                        var cards = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit == barva &&
                                                                                hands[player1].Where(j => j.Suit == barva)
                                                                                              .All(k => k.Value > i.Value))
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
                Description = "hrat nejmensi kartu",
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]);

                    return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault();
                }
            };
        }
    }
}
