using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
{
    public class AiBetlStrategy : AiStrategyBase
    {
        public AiBetlStrategy(Barva? trump, Hra gameType, Hand[] hands)
            : base(trump, gameType, hands)
        {
        }

        protected override IEnumerable<AiRule> GetRules1(Hand[] hands)
        {
            var player2 = (MyIndex + 1) % Game.NumPlayers;
            var player3 = (MyIndex + 2) % Game.NumPlayers;
            var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                           ? (MyIndex + 2) % Game.NumPlayers : (MyIndex + 1) % Game.NumPlayers;

            yield return new AiRule()
            {
                Order = 0,
                Description = "Hraj viteznou kartu",
                ChooseCard1 = () =>
                {
                    IEnumerable<Card> cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == player2)//co-
                    {
                        cardsToPlay = hands[MyIndex].Where(i =>
                                            ValidCards(i, hands[player2]).Any(j =>
                                                ValidCards(i, j, hands[player3]).All(k =>
                                                    Round.WinningCard(i, j, k, null) == k)));
                    }
                    else if (TeamMateIndex == player3)//c-o
                    {
                        cardsToPlay = hands[MyIndex].Where(i =>
                                            ValidCards(i, hands[player2]).All(j =>
                                                ValidCards(i, j, hands[player3]).Any(k =>
                                                    Round.WinningCard(i, j, k, null) == j)));
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 1,
                Description = "Odmazat si vysokou kartu",
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

                    if (TeamMateIndex == -1)//c--
                    {
                        var lo = hands[MyIndex].Where(i => //vezmi karty nizsi nez souperi
                                        hands[player2].Any(j => j.Suit == i.Suit && j.IsHigherThan(i, null)) ||
                                            hands[player3].Any(j => j.Suit == i.Suit && j.IsHigherThan(i, null)));
                        var hi = lo.Where(i => //vezmi karty Vyssi nez souperi
                                        hands[player2].Any(j => j.Suit == i.Suit && i.IsHigherThan(j, null)) ||
                                            hands[player3].Any(j => j.Suit == i.Suit && i.IsHigherThan(j, null)))
                                   .GroupBy(g => g.Suit);   //seskup podle barev
                        //vyber z nizkych karet jen karty kde je max 1 karta vyssi nez souperovy
                        //cardsToPlay = hi.Where(g => g.Count() == 1).SelectMany(g => g).ToList();
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
                            if(hands[MyIndex].HasSuit(barva) && hands[opponent].HasSuit(barva))
                            {
                                var low1 = hands[MyIndex].Min(barva, null);
                                var low2 = hands[TeamMateIndex].Min(barva, null);
                                var oplow = hands[opponent].Min(barva, null);
                                var lowCard1 = new Card(barva, low1);
                                var lowCard2 = new Card(barva, low2);
                                var oplowCard = new Card(barva, oplow);
                                var hi1 = hands[MyIndex].Count(i => i.Suit == barva && i.Value > oplow);
                                var hi2 = hands[TeamMateIndex].Count(i => i.Suit == barva && i.Value > oplow);
                                var mid1 = hands[opponent].Count(i => i.Suit == barva && i.Value > low1);
                                var mid2 = hands[opponent].Count(i => i.Suit == barva && i.Value > low2);

                                //odmazavat ma smysl jen tehdy pokud je nasich vysokych karet mene nez souperovych strednich karet
                                if ((lowCard1.IsLowerThan(oplowCard, null) && hi2 < mid1) ||
                                    (lowCard2.IsLowerThan(oplowCard, null) && hi1 < mid2))
                                {
                                    cardsToPlay.Add(hands[MyIndex].Where(i => i.Suit == barva)
                                                                  .OrderByDescending(i => i.Value)
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
                Description = "Hrat kratkou barvu",
                ChooseCard1 = () =>
                {
                    var cardsToPlay = new List<Card>();

                    var lo = hands[MyIndex].GroupBy(g => g.Suit);   //seskup podle barev
                    //vyber nejkratsi barvu
                    cardsToPlay = lo.OrderBy(g => g.Count()).Select(g => g.ToList()).FirstOrDefault();

                    return cardsToPlay.OrderBy(i => i.Value).FirstOrDefault(); //nejmensi karta
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
                Description = "Hraj viteznou kartu",
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
                                var hi1 = hands[MyIndex].Count(i => i.Suit == barva && i.Value > oplow);
                                var hi2 = hands[TeamMateIndex].Count(i => i.Suit == barva && i.Value > oplow);
                                var mid1 = hands[opponent].Count(i => i.Suit == barva && i.Value > low1);
                                var mid2 = hands[opponent].Count(i => i.Suit == barva && i.Value > low2);

                                //odmazavat ma smysl jen tehdy pokud je nasich vysokych karet mene nez souperovych strednich karet
                                if ((lowCard1.IsLowerThan(oplowCard, null) && hi2 < mid1) ||
                                    (lowCard2.IsLowerThan(oplowCard, null) && hi1 < mid2)) 
                                {
                                    cardsToPlay.Add(ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == barva)
                                                                                  .OrderByDescending(i => i.Value)
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

                    //TODO: zjistit jestli je tohle pravidlo jine pro zacinajiciho hrace a pro soupere
                    var cards = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit != c1.Suit)
                                                                .GroupBy(i => i.Suit)
                                                                .OrderBy(g => g.Count())
                                                                .Select(g => g.ToList()).FirstOrDefault();
                    if (cards != null)
                    {
                        cardsToPlay = cards;
                    }

                    return cardsToPlay.RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "Hrat kratkou barvu",
                ChooseCard2 = (Card c1) =>
                {
                    var lo = ValidCards(c1, hands[MyIndex]).GroupBy(g => g.Suit);   //seskup podle barev
                    //vyber nejkratsi barvu
                    var cardsToPlay = lo.OrderBy(g => g.Count()).Select(g => g.ToList()).FirstOrDefault();

                    return cardsToPlay.RandomOneOrDefault();
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
                Description = "Hraj viteznou kartu",
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
                        //cardsToPlay = hi.Where(g => g.Count() == 1).SelectMany(g => g).ToList();
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
                                var hi1 = hands[MyIndex].Count(i => i.Suit == barva && i.Value > oplow);
                                var hi2 = hands[TeamMateIndex].Count(i => i.Suit == barva && i.Value > oplow);
                                var mid1 = hands[opponent].Count(i => i.Suit == barva && i.Value > low1);
                                var mid2 = hands[opponent].Count(i => i.Suit == barva && i.Value > low2);

                                //odmazavat ma smysl jen tehdy pokud je nasich vysokych karet mene nez souperovych strednich karet
                                if ((lowCard1.IsLowerThan(oplowCard, null) && hi2 < mid1) ||
                                    (lowCard2.IsLowerThan(oplowCard, null) && hi1 < mid2))
                                {
                                    cardsToPlay.Add(ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == barva)
                                                                                  .OrderByDescending(i => i.Value)
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

                    //TODO: zjistit jestli je tohle pravidlo jine pro zacinajiciho hrace a pro soupere
                    var cards = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit != c1.Suit)
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
                Description = "Hrat kratkou barvu",
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
