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
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == c1.Suit &&
                                                                                i.BadValue > c1.BadValue);

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
                    var myCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                               .Select(r => r.player2.PlayerIndex == MyIndex
                                                                    ? r.c2
                                                                    : r.c3)
                                               .ToList();
                    var myInitialHand = myCardsPlayed.Concat((List<Card>)hands[MyIndex]).Distinct();
                    var teamMatesCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                                      .Select(r => r.player2.PlayerIndex == TeamMateIndex
                                                                    ? r.c2
                                                                    : r.c3)
                                                      .ToList();
                    var svrsek = new Card(Barva.Cerveny, Hodnota.Svrsek);
                    var catchingCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Select(b => _rounds.Select(r =>
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
                    //chytaky: A, K+1, S+2, s+3
                    //ukazat chytak muzeme pokud mame: A+K, K+S+1, S+s+2
                    //toto jsou spoluhracovy ukazane chytaky
                    //(ignorujeme barvy kde sam chytam)
                    var teamMatesTopCardsPlayed = teamMatesCardsPlayed.Where(i => catchingCards.Contains(i));
                                                                             //i.BadValue >= svrsek.BadValue &&
                                                                             //!(myInitialHand.Any(j => j.Suit == i.Suit &&
                                                                                      //                       j.Value == Hodnota.Eso) ||
                                                                                      //(myInitialHand.Any(j => j.Suit == i.Suit &&
                                                                                      //                        j.Value == Hodnota.Kral) &&
                                                                                      // myInitialHand.Count(j => j.Suit == i.Suit) > 1) ||
                                                                                      //(myInitialHand.Any(j => j.Suit == i.Suit &&
                                                                                      //                        j.Value == Hodnota.Svrsek) &&
                                                                                      // myInitialHand.Count(j => j.Suit == i.Suit) > 2) ||
                                                                                      //(myInitialHand.Any(j => j.Suit == i.Suit &&
                                                                                       //                       j.Value == Hodnota.Spodek) &&
                                                                                       //myInitialHand.Count(j => j.Suit == i.Suit) > 3)))

                    //pokud mam nebo jsem mel vsechny karty vyssi nez spoluhracuv chytak, tak se jich muzu zbavit
                    var cardsToPlay = ValidCards(c1, hands[MyIndex])
                                    .Where(i => teamMatesTopCardsPlayed.Any(j => i.Suit == j.Suit &&
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
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => (i.Value == Hodnota.Eso &&
                                                                                 hands[MyIndex].HasK(i.Suit)) ||
                                                                                (i.Value == Hodnota.Kral &&
                                                                                 hands[MyIndex].HasQ(i.Suit) &&
                                                                                 hands[MyIndex].CardCount(i.Suit) > 2) ||
                                                                                (i.Value == Hodnota.Svrsek &&
                                                                                 hands[MyIndex].HasJ(i.Suit) &&
                                                                                 hands[MyIndex].CardCount(i.Suit) > 3));
                    if (!cardsToPlay.Any())
                    {
                        //zbav se karet pokud uz jsou vysoke karty pryc (neni jiz treba je chytat) a
                        //nebude to mit vliv na pocet der (napr. pokud mam 2 nejvyssi, tak staci drzet 1)
                        var holesByCard = ValidCards(c1, hands[MyIndex])
                                            .ToDictionary(i => i,
                                                          i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                   .Select(h => new Card(i.Suit, h))
                                                                   .Count(j => i.BadValue > j.BadValue &&
                                                                               _probabilities.CardProbability(player1, j) > 0));
                        var holesPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .ToDictionary(b => b,
                                                             b => holesByCard.Keys.Any(i => i.Suit == b)
                                                                    ? holesByCard.OrderByDescending(i => i.Key.BadValue)
                                                                                 .First(i => i.Key.Suit == b)
                                                                                 .Value
                                                                    : 0);
                        cardsToPlay = ValidCards(c1, hands[MyIndex])
                                        .Where(i => holesByCard[i] == holesPerSuit[i.Suit] &&
                                                    hands[MyIndex].Count(j => i.Suit == j.Suit &&
                                                    holesByCard[j] == holesPerSuit[j.Suit]) > 1 &&
                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                        .Select(h => new Card(i.Suit, h))
                                                        .Where(j => j.BadValue > i.BadValue)
                                                        .All(j => _probabilities.CardProbability(player1, j) == 0));
                    }

                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "hrát nízkou kartu v spoluhráčově barvě",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsPlayed = _rounds.Where(i => i != null && i.c3 != null).SelectMany(i => new[] { i.c1, i.c2, i.c3 }).ToList();
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
                    var myInitialHand = myCardsPlayed.Concat((List<Card>)hands[MyIndex]).Distinct();
                    //hraj mensi karty nez hral spoluhrac (pokud to neni barva kterou chytam)
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => teamMatesCardsPlayed.Any(j => i.Suit == j.Suit &&
                                                                                                                  i.BadValue < j.BadValue) &&
                                                                                !(myInitialHand.Any(j => j.Suit == i.Suit &&
                                                                                                         j.Value == Hodnota.Eso) ||
                                                                                  (myInitialHand.Any(j => j.Suit == i.Suit &&
                                                                                                          j.Value == Hodnota.Kral) &&
                                                                                   myInitialHand.Count(j => j.Suit == i.Suit) > 1) ||
                                                                                  (myInitialHand.Any(j => j.Suit == i.Suit &&
                                                                                                          j.Value == Hodnota.Svrsek) &&
                                                                                   myInitialHand.Count(j => j.Suit == i.Suit) > 2) ||
                                                                                  (myInitialHand.Any(j => j.Suit == i.Suit &&
                                                                                                          j.Value == Hodnota.Spodek) &&
                                                                                   myInitialHand.Count(j => j.Suit == i.Suit) > 3)));                    
                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 4,
                //Description = "hrát největší kartu, kterou nechytám",
                Description = "hrát nejmenší kartu, kterou nechytám",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsPlayed = _rounds.Where(i => i != null && i.c3 != null).SelectMany(i => new[] { i.c1, i.c2, i.c3 }).ToList();
                    var myCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                               .Select(r => r.player2.PlayerIndex == MyIndex
                                                                    ? r.c2
                                                                    : r.c3)
                                               .ToList();
                    var myInitialHand = new Hand(myCardsPlayed.Concat((List<Card>)hands[MyIndex]));
                    var svrsek = new Card(Barva.Cerveny, Hodnota.Svrsek);
                    var catchingCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Select(b => _rounds.Select(r =>
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
                    //hrej jen barvy ktere nechytam,
                    //barvy, ktere chyta spoluhrac
                    //barvy, ktere chytam ale mam v nich i tak dost karet
                    //barvy, ktere jsem sice chytal, ale musel jsem se rozhodnout jednu
                    var suitsToPlay = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                          .Where(b => myInitialHand.Any(i => i.Suit == b) &&
                                                      (!myInitialHand.Any(i => catchingCards.Contains(i)) ||
                                                       (myInitialHand.HasA(b) &&
                                                        hands[MyIndex].CardCount(b) > 1) ||
                                                       (!myInitialHand.HasA(b) &&
                                                        myInitialHand.HasK(b) &&
                                                        hands[MyIndex].CardCount(b) > 2) ||
                                                       (!myInitialHand.HasA(b) &&
                                                        !myInitialHand.HasK(b) &&
                                                        myInitialHand.HasQ(b) &&
                                                        hands[MyIndex].CardCount(b) > 3) ||
                                                       Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                           .Select(h => new Card(b, h))
                                                           .Where(i => i.BadValue < svrsek.BadValue)
                                                           .All(i => _probabilities.CardProbability(player1, i) == 0)));
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => suitsToPlay.Contains(i.Suit) &&
                                                                                i.BadValue < svrsek.BadValue);
                    //if (!cardsToPlay.Any())
                    //{
                    //    cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                    //                                                                .Select(h => new Card(i.Suit, h))
                    //                                                                .Where(j => j.BadValue < i.BadValue)
                    //                                                                .All(j => _probabilities.CardProbability(player1, j) == 0));
                    //}

                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 5,
                Description = "hrát kartu od spoluhráčova chytáka",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var myCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                               .Select(r => r.player2.PlayerIndex == MyIndex
                                                                    ? r.c2
                                                                    : r.c3)
                                               .ToList();
                    var myInitialHand = new Hand(myCardsPlayed.Concat((List<Card>)hands[MyIndex]));
                    var svrsek = new Card(Barva.Cerveny, Hodnota.Svrsek);
                    var catchingCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Select(b => _rounds.Select(r =>
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
                    var teamMatesCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                                      .Select(r => r.player2.PlayerIndex == TeamMateIndex
                                                                    ? r.c2
                                                                    : r.c3)
                                               .ToList();
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => teamMatesCardsPlayed.Any(j => j.Value == Hodnota.Eso &&
                                                                                                              i.Suit == j.Suit &&
                                                                                                              catchingCards.Contains(j)));
                    
                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 6,
                Description = "zbav se zbytečných chytáků",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    //zbav se karet pokud uz jsou vysoke karty pryc (neni jiz treba je chytat) a
                    //nebude to mit vliv na pocet der (napr. pokud mam 2 nejvyssi, tak staci drzet 1)
                    var holesByCard = ValidCards(c1, hands[MyIndex])
                                        .ToDictionary(i => i,
                                                      i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                               .Select(h => new Card(i.Suit, h))
                                                               .Count(j => i.BadValue > j.BadValue &&
                                                                           _probabilities.CardProbability(player1, j) > 0));
                    var holesPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                           .ToDictionary(b => b,
                                                         b => holesByCard.Keys.Any(i => i.Suit == b)
                                                                ? holesByCard.First(i => i.Key.Suit == b)
                                                                             .Value
                                                                : 0);
                    var cardsToPlay = ValidCards(c1, hands[MyIndex])
                                        .Where(i => holesByCard[i] == holesPerSuit[i.Suit] &&
                                                    hands[MyIndex].Count(j => i.Suit == j.Suit &&
                                                    holesByCard[j] == holesPerSuit[j.Suit]) > 1 &&
                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                        .Select(h => new Card(i.Suit, h))
                                                        .Where(j => j.BadValue > i.BadValue)
                                                        .All(j => _probabilities.CardProbability(player1, j) == 0));

                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 7,
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
                    var myCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                               .Select(r => r.player2.PlayerIndex == MyIndex
                                                                    ? r.c2
                                                                    : r.c3)
                                               .ToList();
                    var myInitialHand = myCardsPlayed.Concat((List<Card>)hands[MyIndex]).Distinct();
                    var teamMatesCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                                      .Select(r => r.player2.PlayerIndex == TeamMateIndex
                                                                    ? r.c2
                                                                    : r.c3)
                                                      .Concat(new [] {c2})
                                                      .ToList();
                    var svrsek = new Card(Barva.Cerveny, Hodnota.Svrsek);
                    var catchingCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Select(b => _rounds.Select(r =>
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
                    //chytaky: A, K+1, S+2, s+3
                    //ukazat chytak muzeme pokud mame: A+K, K+S+1, S+s+2
                    //toto jsou spoluhracovy ukazane chytaky
                    //(ignorujeme barvy kde sam chytam)
                    var teamMatesTopCardsPlayed = teamMatesCardsPlayed.Where(i => catchingCards.Contains(i));

                    //pokud mam nebo jsem mel vsechny karty vyssi nez spoluhracuv chytak, tak se jich muzu zbavit
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex])
                                    .Where(i => teamMatesTopCardsPlayed.Any(j => i.Suit == j.Suit &&
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
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => (i.Value == Hodnota.Eso &&
                                                                                     hands[MyIndex].HasK(i.Suit)) ||
                                                                                    (i.Value == Hodnota.Kral &&
                                                                                     hands[MyIndex].HasQ(i.Suit) &&
                                                                                     hands[MyIndex].CardCount(i.Suit) > 2) ||
                                                                                    (i.Value == Hodnota.Svrsek &&
                                                                                     hands[MyIndex].HasJ(i.Suit) &&
                                                                                     hands[MyIndex].CardCount(i.Suit) > 3));

                    if (!cardsToPlay.Any())
                    {
                        //zbav se karet pokud uz jsou vysoke karty pryc (neni jiz treba je chytat) a
                        //nebude to mit vliv na pocet der (napr. pokud mam 2 nejvyssi, tak staci drzet 1)
                        var holesByCard = ValidCards(c1, c2, hands[MyIndex])
                                            .ToDictionary(i => i,
                                                          i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                   .Select(h => new Card(i.Suit, h))
                                                                   .Count(j => i.BadValue > j.BadValue &&
                                                                               _probabilities.CardProbability(player1, j) > 0));
                        var holesPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                               .ToDictionary(b => b,
                                                             b => holesByCard.Keys.Any(i => i.Suit == b)
                                                                    ? holesByCard.OrderByDescending(i => i.Key.BadValue)
                                                                                 .First(i => i.Key.Suit == b)
                                                                                 .Value
                                                                    : 0);
                        cardsToPlay = ValidCards(c1, c2, hands[MyIndex])
                                        .Where(i => holesByCard[i] == holesPerSuit[i.Suit] &&
                                                    hands[MyIndex].Count(j => i.Suit == j.Suit &&
                                                    holesByCard[j] == holesPerSuit[j.Suit]) > 1 &&
                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                        .Select(h => new Card(i.Suit, h))
                                                        .Where(j => j.BadValue > i.BadValue)
                                                        .All(j => _probabilities.CardProbability(player1, j) == 0));
                    }

                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "hrát nízkou kartu v spoluhráčově barvě",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsPlayed = _rounds.Where(i => i != null && i.c3 != null).SelectMany(i => new[] { i.c1, i.c2, i.c3 }).ToList();
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
                    var myInitialHand = myCardsPlayed.Concat((List<Card>)hands[MyIndex]).Distinct();
                    //hraj mensi karty nez hral spoluhrac (pokud to neni barva kterou chytam)
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => teamMatesCardsPlayed.Any(j => i.Suit == j.Suit &&
                                                                                                                  i.BadValue < j.BadValue) &&
                                                                                    !(myInitialHand.Any(j => j.Suit == i.Suit &&
                                                                                                             j.Value == Hodnota.Eso) ||
                                                                                      (myInitialHand.Any(j => j.Suit == i.Suit &&
                                                                                                              j.Value == Hodnota.Kral) &&
                                                                                       myInitialHand.Count(j => j.Suit == i.Suit) > 1) ||
                                                                                      (myInitialHand.Any(j => j.Suit == i.Suit &&
                                                                                                              j.Value == Hodnota.Svrsek) &&
                                                                                       myInitialHand.Count(j => j.Suit == i.Suit) > 2) ||
                                                                                      (myInitialHand.Any(j => j.Suit == i.Suit &&
                                                                                                              j.Value == Hodnota.Spodek) &&
                                                                                       myInitialHand.Count(j => j.Suit == i.Suit) > 3)));
                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 4,
                //Description = "hrát největší kartu, kterou nechytám",
                Description = "hrát nejmenší kartu, kterou nechytám",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsPlayed = _rounds.Where(i => i != null && i.c3 != null).SelectMany(i => new[] { i.c1, i.c2, i.c3 }).ToList();
                    var myCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                               .Select(r => r.player2.PlayerIndex == MyIndex
                                                                    ? r.c2
                                                                    : r.c3)
                                               .ToList();
                    var myInitialHand = new Hand(myCardsPlayed.Concat((List<Card>)hands[MyIndex]));
                    var svrsek = new Card(Barva.Cerveny, Hodnota.Svrsek);
                    var catchingCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Select(b => _rounds.Select(r =>
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
                    var suitsToPlay = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                          .Where(b => myInitialHand.Any(i => i.Suit == b) &&
                                                      (!myInitialHand.Any(i => catchingCards.Contains(i)) ||
                                                       (myInitialHand.HasA(b) &&
                                                        hands[MyIndex].CardCount(b) > 1) ||
                                                       (!myInitialHand.HasA(b) &&
                                                        myInitialHand.HasK(b) &&
                                                        hands[MyIndex].CardCount(b) > 2) ||
                                                       (!myInitialHand.HasA(b) &&
                                                        !myInitialHand.HasK(b) &&
                                                        myInitialHand.HasQ(b) &&
                                                        hands[MyIndex].CardCount(b) > 3) ||
                                                       Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                           .Select(h => new Card(b, h))
                                                           .Where(i => i.BadValue < svrsek.BadValue)
                                                           .All(i => _probabilities.CardProbability(player1, i) == 0)));
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => suitsToPlay.Contains(i.Suit) &&
                                                                                i.BadValue < svrsek.BadValue);
                    //if (!cardsToPlay.Any())
                    //{
                    //    cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                    //                                                                .Select(h => new Card(i.Suit, h))
                    //                                                                .Where(j => j.BadValue < i.BadValue)
                    //                                                                .All(j => _probabilities.CardProbability(player1, j) == 0));
                    //}

                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 5,
                Description = "hrát kartu od spoluhráčova chytáka",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var myCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                               .Select(r => r.player2.PlayerIndex == MyIndex
                                                                    ? r.c2
                                                                    : r.c3)
                                               .ToList();
                    var myInitialHand = new Hand(myCardsPlayed.Concat((List<Card>)hands[MyIndex]));
                    var svrsek = new Card(Barva.Cerveny, Hodnota.Svrsek);
                    var catchingCards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                            .Select(b => _rounds.Select(r =>
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
                    var teamMatesCardsPlayed = _rounds.Where(r => r != null && r.c3 != null)
                                                      .Select(r => r.player2.PlayerIndex == TeamMateIndex
                                                                    ? r.c2
                                                                    : r.c3)
                                               .ToList();
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => teamMatesCardsPlayed.Any(j => j.Value == Hodnota.Eso &&
                                                                                                                  i.Suit == j.Suit &&
                                                                                                                  catchingCards.Contains(j)));

                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 6,
                Description = "zbav se zbytečných chytáků",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    //zbav se karet pokud uz jsou vysoke karty pryc (neni jiz treba je chytat) a
                    //nebude to mit vliv na pocet der (napr. pokud mam 2 nejvyssi, tak staci drzet 1)
                    var holesByCard = ValidCards(c1, c2, hands[MyIndex])
                                        .ToDictionary(i => i,
                                                      i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                               .Select(h => new Card(i.Suit, h))
                                                               .Count(j => i.BadValue > j.BadValue &&
                                                                           _probabilities.CardProbability(player1, j) > 0));
                    var holesPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                           .ToDictionary(b => b,
                                                         b => holesByCard.Keys.Any(i => i.Suit == b)
                                                                ? holesByCard.First(i => i.Key.Suit == b)
                                                                             .Value
                                                                : 0);
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex])
                                        .Where(i => holesByCard[i] == holesPerSuit[i.Suit] &&
                                                    hands[MyIndex].Count(j => i.Suit == j.Suit &&
                                                    holesByCard[j] == holesPerSuit[j.Suit]) > 1 &&
                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                        .Select(h => new Card(i.Suit, h))
                                                        .Where(j => j.BadValue > i.BadValue)
                                                        .All(j => _probabilities.CardProbability(player1, j) == 0));

                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 7,
                Description = "hrát nejmenší kartu",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]);

                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                }
            };
        }
    }
}
