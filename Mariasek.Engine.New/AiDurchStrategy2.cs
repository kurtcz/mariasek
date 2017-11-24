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

            var teamMatesCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                              .Select(r => r.player2.PlayerIndex == TeamMateIndex
                                                            ? r.c2
                                                            : r.c3)
                                              .ToList();
            var myCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                       .Select(r => r.player2.PlayerIndex == MyIndex
                                                            ? r.c2
                                                            : r.c3)
                                       .ToList();
            var myInitialHand = new Hand(myCardsPlayed.Concat((List<Card>)hands[MyIndex]).Distinct());
            var spodek = new Card(Barva.Cerveny, Hodnota.Spodek);
            var catchingCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                    .Select(b => 
                                            _rounds.Select(r =>
                                                    {
                                                        if (r != null && r.c3 != null)
                                                        {
                                                            if (r.c2.Suit == b && r.c2.BadValue > spodek.BadValue)
                                                            {
                                                                return r.c2;
                                                            }
                                                            if (r.c3.Suit == b && r.c3.BadValue > spodek.BadValue)
                                                            {
                                                                return r.c3;
                                                            }
                                                        }
                                                        return null;
                                                    })
                                                   .FirstOrDefault(i => i != null))
                                    .Where(i => i != null);
            var teamMatesCatchingCards = catchingCards.Where(i => teamMatesCardsPlayed.Contains(i));
            var myCatchingCards = catchingCards.Where(i => myInitialHand.Any(j => j == i))  //pripocti chytaky, ktere mam v ruce
                                               .Concat(hands[MyIndex].Where(i => teamMatesCatchingCards.All(j => j.Suit != i.Suit) &&
                                                                                 (i.Value == Hodnota.Eso ||
                                                                                  (i.Value == Hodnota.Kral &&
                                                                                   hands[MyIndex].CardCount(i.Suit) >= 2) ||
                                                                                  (i.Value == Hodnota.Svrsek &&
                                                                                   hands[MyIndex].CardCount(i.Suit) >= 3) ||
                                                                                  (i.Value == Hodnota.Spodek &&
                                                                                   hands[MyIndex].CardCount(i.Suit) >= 4))));
            var cardsToKeep = new Dictionary<Barva, IEnumerable<Card>>();

            foreach(var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                var numCardsToKeep = 0;

                if (myInitialHand.HasA(b))
                {
                    numCardsToKeep = 1;
                }
                else if (myInitialHand.HasK(b))
                {
                    numCardsToKeep = 2;
                }
                else if (myInitialHand.HasQ(b))
                {
                    numCardsToKeep = 3;
                }
                else if (myInitialHand.HasJ(b))
                {
                    numCardsToKeep = 4;
                }
                if (numCardsToKeep > 0 &&
                    hands[MyIndex].CardCount(b) >= numCardsToKeep)
                {
                    var topCard = myInitialHand.Where(i => i.Suit == b)
                                               .OrderByDescending(i => i.BadValue)
                                               .First();
                    var bottomCards = myInitialHand.Where(i => i.Suit == b)
                                                   .OrderBy(i => i.BadValue)
                                                   .Take(numCardsToKeep - 1);
                    var holesAboveTopCard = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                .Select(h => new Card(b, h))
                                                .Count(i => i.BadValue > topCard.BadValue &&
                                                            _probabilities.CardProbability(player1, i) > 0);
                    //drz nejnizsi z karet, nad kterou je stejne der jako nad nejvyssi kartou v dane barve
                    topCard = myInitialHand.Where(i => i.Suit == b)
                                           .OrderByDescending(i => i.BadValue)
                                           .Last(i => bottomCards.All(j => j.BadValue < i.BadValue) &&
                                                      Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                          .Select(h => new Card(b, h))
                                                          .Count(j => j.BadValue > i.BadValue &&
                                                                      _probabilities.CardProbability(player1, j) > 0) == holesAboveTopCard);
                    if (holesAboveTopCard == 0)
                    {
                        //zadne vysoke karty v dane barve nezbyly
                        //postaci drzet nejnizsi z nejvyssich karet abych chytal nizke karty
                        bottomCards = Enumerable.Empty<Card>();
                        topCard = myInitialHand.Where(i => i.Suit == b)
                                               .OrderByDescending(i => i.BadValue)
                                               .Last(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                              .Select(h => new Card(b, h))
                                                              .Count(j => j.BadValue > i.BadValue &&
                                                                          _probabilities.CardProbability(player1, j) > 0) == holesAboveTopCard);
                        var holesBelowTopCard = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                    .Select(h => new Card(b, h))
                                                    .Count(i => i.BadValue < topCard.BadValue &&
                                                                _probabilities.CardProbability(player1, i) > 0);
                        if (holesBelowTopCard == 0)
                        {
                            //pokud uz nezbyly diry ani dole, tak neni treba barvu drzet
                            topCard = null;
                        }
                    }
                    if (topCard != null)
                    {
                        var list = new List<Card>() { topCard };
                        list.AddRange(bottomCards);

                        //jen pokud danou barvu nechyta spoluhrac
                        if (!teamMatesCatchingCards.Any(i => i.Suit == b))
                        {
                            cardsToKeep.Add(b, list);
                        }
                    }
                }
            }

            yield return new AiRule()
            {
                Order = 0,
                Description = "hrát vítěznou kartu",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == c1.Suit &&
                                                                                i.BadValue > c1.BadValue);
                    System.Diagnostics.Debug.WriteLine(cardsToKeep);
                    return cardsToPlay.FirstOrDefault();
                }
            };


            yield return new AiRule()
            {
                Order = 1,
                Description = "odmazat vysokou kartu ve spoluhráčově barvě",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    //chytaky: A, K+1, S+2, s+3
                    //ukazat chytak muzeme pokud mame: A+K, K+S+1, S+s+2
                    //toto jsou spoluhracovy ukazane chytaky
                    //(ignorujeme barvy kde sam chytam)
                    //pokud mam nebo jsem mel vsechny karty vyssi nez spoluhracuv chytak, tak se jich muzu zbavit
                    var cardsToPlay = ValidCards(c1, hands[MyIndex])
                                        .Where(i => teamMatesCatchingCards.Any(j => i.Suit == j.Suit &&
                                                                                    i.BadValue > j.BadValue &&
                                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                        .Select(h => new Card(i.Suit, h))
                                                                                        .Where(k => k.BadValue > j.BadValue)
                                                                                        .All(k => _probabilities.CardProbability(player1, k) == 0)));

                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "ukázat barvu kterou chytám",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => cardsToKeep.Keys.Contains(i.Suit) &&
                                                                                cardsToKeep[i.Suit].All(j => i.BadValue > j.BadValue));

                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "hrát největší kartu, kterou nechytám",
                //Description = "hrát nejmenší kartu, kterou nechytám",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    if (cardsToKeep.Any() && !hands[MyIndex].HasSuit(c1.Suit))
                    {
                        var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => cardsToKeep.SelectMany(j => j.Value)
                                                                                               .All(j => i != j));

                        return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                    }
                    return null;
                }
            };

            yield return new AiRule()
            {
                Order = 4,
                Description = "hrát kartu od spoluhráčova chytáka",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => teamMatesCatchingCards.Any(j => i.Suit == j.Suit &&
                                                                                                                i.BadValue < j.BadValue));
                    
                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 5,
                Description = "obětovat chytáka",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var mySuits = ValidCards(c1, hands[MyIndex]).Select(i => i.Suit).Distinct();
                    var player = TeamMateIndex != -1 ? TeamMateIndex : player1;

                    if (mySuits.All(i => myCatchingCards.Any(j => j.Suit == i)))
                    {
                        //v každé barvě mám chytáka, musím se jednoho bavit
                        //vezmeme barvu, kde je nejnizsi dira, tu drzi asi nejspis spoluhrac?
                        var minHole = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                          .Select(b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                           .Select(h => new Card(b, h))
                                                           .Where(i => _probabilities.CardProbability(player, i) > 0)
                                                           .OrderBy(i => i.BadValue)
                                                           .FirstOrDefault())
                                          .Where(i => i != null)
                                          .OrderBy(i => i.BadValue)
                                          .FirstOrDefault();
                        if (minHole != null)
                        {
                            var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == minHole.Suit);

                            return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                        }
                    }
                    return null;
                }
            };

            yield return new AiRule()
            {
                Order = 6,
                Description = "hrát nejmenší kartu",
                SkipSimulations = true,
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
            var teamMatesCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                              .Select(r => r.player2.PlayerIndex == TeamMateIndex
                                                            ? r.c2
                                                            : r.c3)
                                              .ToList();
            var myCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                       .Select(r => r.player2.PlayerIndex == MyIndex
                                                            ? r.c2
                                                            : r.c3)
                                       .ToList();
            var myInitialHand = new Hand(myCardsPlayed.Concat((List<Card>)hands[MyIndex]).Distinct());
            var svrsek = new Card(Barva.Cerveny, Hodnota.Svrsek);
            var catchingCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                    .Select(b =>
                                            _rounds.Select(r =>
                                            {
                                                if (r != null && r.c3 != null)
                                                {
                                                    if (r.c2.Suit == b && r.c2.BadValue >= svrsek.BadValue)
                                                    {
                                                        return r.c2;
                                                    }
                                                    if (r.c3.Suit == b && r.c3.BadValue >= svrsek.BadValue)
                                                    {
                                                        return r.c3;
                                                    }
                                                }
                                                return null;
                                            })
                                                   .FirstOrDefault(i => i != null))
                                    .Where(i => i != null);
            var teamMatesCatchingCards = catchingCards.Where(i => teamMatesCardsPlayed.Contains(i));
            var myCatchingCards = catchingCards.Where(i => myInitialHand.Any(j => j == i));
            var cardsToKeep = new Dictionary<Barva, IEnumerable<Card>>();

            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                var numCardsToKeep = 0;

                if (myInitialHand.HasA(b))
                {
                    numCardsToKeep = 1;
                }
                else if (myInitialHand.HasK(b))
                {
                    numCardsToKeep = 2;
                }
                else if (myInitialHand.HasQ(b))
                {
                    numCardsToKeep = 3;
                }
                else if (myInitialHand.HasJ(b))
                {
                    numCardsToKeep = 4;
                }
                if (numCardsToKeep > 0 &&
                    hands[MyIndex].CardCount(b) >= numCardsToKeep)
                {
                    var topCard = myInitialHand.Where(i => i.Suit == b)
                                               .OrderByDescending(i => i.BadValue)
                                               .First();
                    var bottomCards = myInitialHand.Where(i => i.Suit == b)
                                                   .OrderBy(i => i.BadValue)
                                                   .Take(numCardsToKeep - 1);
                    var holesAboveTopCard = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                .Select(h => new Card(b, h))
                                                .Count(i => i.BadValue > topCard.BadValue &&
                                                            _probabilities.CardProbability(player1, i) > 0);
                    //drz nejnizsi z karet, nad kterou je stejne der jako nad nejvyssi kartou v dane barve
                    topCard = myInitialHand.Where(i => i.Suit == b)
                                           .OrderByDescending(i => i.BadValue)
                                           .Last(i => bottomCards.All(j => j.BadValue < i.BadValue) &&
                                                      Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                          .Select(h => new Card(b, h))
                                                          .Count(j => j.BadValue > i.BadValue &&
                                                                      _probabilities.CardProbability(player1, j) > 0) == holesAboveTopCard);
                    if (holesAboveTopCard == 0)
                    {
                        //zadne vysoke karty v dane barve nezbyly
                        //postaci drzet nejnizsi z nejvyssich karet abych chytal nizke karty
                        bottomCards = Enumerable.Empty<Card>();
                        topCard = myInitialHand.Where(i => i.Suit == b)
                                               .OrderByDescending(i => i.BadValue)
                                               .Last(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                              .Select(h => new Card(b, h))
                                                              .Count(j => j.BadValue > i.BadValue &&
                                                                          _probabilities.CardProbability(player1, j) > 0) == holesAboveTopCard);
                        var holesBelowTopCard = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                    .Select(h => new Card(b, h))
                                                    .Count(i => i.BadValue < topCard.BadValue &&
                                                                _probabilities.CardProbability(player1, i) > 0);
                        if (holesBelowTopCard == 0)
                        {
                            //pokud uz nezbyly diry ani dole, tak neni treba barvu drzet
                            topCard = null;
                        }
                    }
                    if (topCard != null)
                    {
                        var list = new List<Card>() { topCard };
                        list.AddRange(bottomCards);

                        //jen pokud danou barvu nechyta spoluhrac
                        if (!teamMatesCatchingCards.Any(i => i.Suit == b))
                        {
                            cardsToKeep.Add(b, list);
                        }
                    }
                }
            }

            yield return new AiRule()
            {
                Order = 0,
                Description = "hrát vítěznou kartu",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => i.Suit == c1.Suit &&
                                                                                    i.BadValue > c1.BadValue);

                    return cardsToPlay.FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 1,
                Description = "odmazat vysokou kartu ve spoluhráčově barvě",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    //chytaky: A, K+1, S+2, s+3
                    //ukazat chytak muzeme pokud mame: A+K, K+S+1, S+s+2
                    //toto jsou spoluhracovy ukazane chytaky
                    //(ignorujeme barvy kde sam chytam)
                    //pokud mam nebo jsem mel vsechny karty vyssi nez spoluhracuv chytak, tak se jich muzu zbavit
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex])
                                        .Where(i => teamMatesCatchingCards.Any(j => i.Suit == j.Suit &&
                                                                                    i.BadValue > j.BadValue &&
                                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                        .Select(h => new Card(i.Suit, h))
                                                                                        .Where(k => k.BadValue > j.BadValue)
                                                                                        .All(k => _probabilities.CardProbability(player1, k) == 0)));

                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "ukázat barvu kterou chytám",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => cardsToKeep.Keys.Contains(i.Suit) &&
                                                                                    cardsToKeep[i.Suit].All(j => i.BadValue > j.BadValue));

                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "hrát největší kartu, kterou nechytám",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    if (cardsToKeep.Any() && !hands[MyIndex].HasSuit(c1.Suit))
                    {
                        var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => cardsToKeep.SelectMany(j => j.Value)
                                                                                                   .All(j => i != j));

                        return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                    }
                    return null;
                }
            };

            yield return new AiRule()
            {
                Order = 4,
                Description = "hrát kartu od spoluhráčova chytáka",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => teamMatesCatchingCards.Any(j => i.Suit == j.Suit &&
                                                                                                                    i.BadValue < j.BadValue));

                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 5,
                Description = "obětovat chytáka",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var mySuits = ValidCards(c1, hands[MyIndex]).Select(i => i.Suit).Distinct();
                    var player = TeamMateIndex != -1 ? TeamMateIndex : player1;

                    if (mySuits.All(i => myCatchingCards.Any(j => j.Suit == i)))
                    {
                        //v každé barvě mám chytáka, musím se jednoho bavit
                        //vezmeme barvu, kde je nejnizsi dira, tu drzi asi nejspis spoluhrac?
                        var minHole = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                          .Select(b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                           .Select(h => new Card(b, h))
                                                           .Where(i => _probabilities.CardProbability(player, i) > 0)
                                                           .OrderBy(i => i.BadValue)
                                                           .FirstOrDefault())
                                          .Where(i => i != null)
                                          .OrderBy(i => i.BadValue)
                                          .FirstOrDefault();
                        if (minHole != null)
                        {
                            var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == minHole.Suit);

                            return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                        }
                    }
                    return null;
                }
            };

            yield return new AiRule()
            {
                Order = 6,
                Description = "hrát nejmenší kartu",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]);

                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                }
            };
        }
    }
}
