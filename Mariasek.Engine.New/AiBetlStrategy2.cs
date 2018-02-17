using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
{
    public class AiBetlStrategy2 : AiStrategyBase
    {
        public float RiskFactor { get; set; }

        public AiBetlStrategy2(Barva? trump, Hra gameType, Hand[] hands, Round[] rounds, List<Barva> teamMatesSuits, Probability probabilities)
            : base(trump, gameType, hands, rounds, teamMatesSuits, probabilities)
        {
            RiskFactor = 0.275f; //0.2727f ~ (9 nad 5) / (11 nad 5)
		}

        protected override IEnumerable<AiRule> GetRules1(Hand[] hands)
        {
            var player2 = (MyIndex + 1) % Game.NumPlayers;
            var player3 = (MyIndex + 2) % Game.NumPlayers;
            var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                            ? (MyIndex + 2) % Game.NumPlayers 
                            : (MyIndex + 1) % Game.NumPlayers;

            Barva? bannedSuit = null;
            var preferredSuits = new List<Barva>();
            var hochCards = new List<Card>();

            if (TeamMateIndex != -1 && _rounds != null && _rounds[0] != null)
            {
                if (RoundNumber == 2)
                {
                    //pokud v 1.kole vsichni priznali barvu ale spoluhrac nesel vejs
                    if (_rounds[0].c1.Suit == _rounds[0].c2.Suit &&
                        (_rounds[0].player2.PlayerIndex == TeamMateIndex &&
                         _rounds[0].c1.BadValue > _rounds[0].c2.BadValue) ||
                        (_rounds[0].player3.PlayerIndex == TeamMateIndex &&
                         _rounds[0].c1.BadValue > _rounds[0].c3.BadValue))
                    {
                        preferredSuits.Add(_rounds[0].c1.Suit);
                    }
                    if (_rounds[0].c1.Suit == _rounds[0].c2.Suit &&
                        (_rounds[0].player2.PlayerIndex == TeamMateIndex &&
                         _rounds[0].c1.BadValue > _rounds[0].c2.BadValue) ||
                        (_rounds[0].player3.PlayerIndex == TeamMateIndex &&
                         _rounds[0].c1.BadValue > _rounds[0].c3.BadValue))
                    {
                        preferredSuits.Add(_rounds[0].c1.Suit);
                    }
                    //pokud v 2.kole spoluhrac nepriznal barvu a jeste nejake karty v barve zbyvaji
                    if (hands[MyIndex].CardCount(_rounds[0].c1.Suit) < 6 &&
                        ((_rounds[0].player2.PlayerIndex == TeamMateIndex &&
                          _rounds[0].c1.Suit != _rounds[0].c2.Suit) ||
                         (_rounds[0].player3.PlayerIndex == TeamMateIndex &&
                          _rounds[0].c1.Suit != _rounds[0].c3.Suit)))
                    {
                        preferredSuits.Add(_rounds[0].c1.Suit);
                    }
                }
                if (!preferredSuits.Any())
                {
                    //prednostne zkousej hrat barvu kterou spoluhrac odmazaval
                    for (var i = 0; i < RoundNumber - 1; i++)
                    {
                        if (_rounds[i].player2.PlayerIndex == TeamMateIndex &&
                             _rounds[i].c1.Suit != _rounds[i].c2.Suit)
                        {
                            preferredSuits.Add(_rounds[i].c2.Suit);
                        }
                        if (_rounds[i].player3.PlayerIndex == TeamMateIndex &&
                            _rounds[i].c1.Suit != _rounds[i].c3.Suit)
                        {
                            preferredSuits.Add(_rounds[i].c3.Suit);
                        }
                    }
                }
                var svrsek = new Card(Barva.Cerveny, Hodnota.Svrsek);

                for (var i = 0; i < RoundNumber - 1; i++)
                {
                    if (_rounds[i].roundWinner.PlayerIndex == MyIndex)
                    {
                        if (_rounds[i].player1.PlayerIndex == MyIndex &&
                          _rounds[i].c1.BadValue > svrsek.BadValue)
                        {
                            hochCards.Add(_rounds[i].c1);
                        }
                        else if (_rounds[i].player2.PlayerIndex == MyIndex &&
                          _rounds[i].c2.BadValue > svrsek.BadValue)
                        {
                            hochCards.Add(_rounds[i].c2);
                        }
                        else if (_rounds[i].player3.PlayerIndex == MyIndex &&
                          _rounds[i].c3.BadValue > svrsek.BadValue)
                        {
                            hochCards.Add(_rounds[i].c3);
                        }
                    }
                }
            }
            if (TeamMateIndex == -1)
            {
                yield return new AiRule()
                {
                    Order = 0,
                    Description = "Vytlač jedinou díru v barvě",
                    SkipSimulations = true,
                    ChooseCard1 = () =>
                    {
                        var cardsToPlay = hands[MyIndex].Where(i => hands[MyIndex].CardCount(i.Suit) == 7 &&
                                                                    hands[MyIndex].Has7(i.Suit) &&
                                                                    hands[MyIndex].HasA(i.Suit) &&
                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Select(h => new Card(i.Suit, h))
                                                                        .Where(j => j.BadValue > i.BadValue)
                                                                        .Any(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                  _probabilities.CardProbability(player3, j) > 0));

                        return cardsToPlay.ToList().RandomOneOrDefault();
                    }
                };

                yield return new AiRule()
                {
                    Order = 1,
                    Description = "Vytlač dvě díry v barvě",
                    SkipSimulations = true,
                    ChooseCard1 = () =>
                    {
                        var cardsToPlay = hands[MyIndex].Where(i => hands[MyIndex].CardCount(i.Suit) == 6 &&
                                                                    (hands[MyIndex].Has7(i.Suit) ||
                                                                     hands[MyIndex].Has8(i.Suit)) &&
                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Select(h => new Card(i.Suit, h))
                                                                        .Where(j => j.BadValue > i.BadValue)
                                                                        .Any(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                  _probabilities.CardProbability(player3, j) > 0));

                        return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                    }
                };

                yield return new AiRule()
                {
                    Order = 2,
                    Description = "Zbav se plonka",
                    SkipSimulations = true,
                    ChooseCard1 = () =>
                    {
                        var cardsToPlay = hands[MyIndex].Where(i => i.Value != Hodnota.Eso &&
                                                                    i.Value != Hodnota.Sedma &&
                                                                    hands[MyIndex].CardCount(i.Suit) == 1 &&
                                                                    (_probabilities.SuitHigherThanCardProbability(player2, i, RoundNumber, false) > 0 ||
                                                                     _probabilities.SuitHigherThanCardProbability(player3, i, RoundNumber, false) > 0));

                        return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                    }
                };

                yield return new AiRule()
                {
                    Order = 3,
                    Description = "Odmazat si vysokou kartu",
                    SkipSimulations = true,
                    ChooseCard1 = () =>
                    {
                        var cardsToPlay = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Select(h => new Card(i.Suit, h))
                                                                        .Where(j => j.BadValue > i.BadValue)
                                                                        .Any(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                  _probabilities.CardProbability(player3, j) > 0) &&
                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Select(h => new Card(i.Suit, h))
                                                                        .Where(j => j.BadValue < i.BadValue)
                                                                        .Any(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                  _probabilities.CardProbability(player3, j) > 0));

                        return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                    }
                };
                yield return new AiRule()
                {
                    Order = 4,
                    Description = "Hrát barvu s dírou",
                    SkipSimulations = true,
                    ChooseCard1 = () =>
                    {
                        var cardsToPlay = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Select(h => new Card(i.Suit, h))
                                                                        .Where(j => j.BadValue > i.BadValue)
                                                                        .Any(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                  _probabilities.CardProbability(player3, j) > 0));

                        return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                    }
                };
            }
            else
            {
                if (RoundNumber == 2 && _rounds != null && _rounds[0] != null) //pri simulaci hry jsou skutecny kola jeste neodehrany
                {
                    if (_rounds[0].c1.Suit == _rounds[0].c2.Suit && _rounds[0].c1.Suit == _rounds[0].c3.Suit)
                    {
                        bannedSuit = _rounds[0].c1.Suit;
                    }
                }

                yield return new AiRule()
                {
                    Order = 5,
                    Description = "Hraj vítěznou kartu",
                    SkipSimulations = true,
                    ChooseCard1 = () =>
                    {
                        var cardsToPlay = Enumerable.Empty<Card>();
                        if (RoundNumber == 2 && preferredSuits.Any())
                        {
                            //v 2.kole zkus rovnou zahrat preferovanou barvu. Pokud takova je, tak je to vitezna barva
                            cardsToPlay = hands[MyIndex].Where(i => i.Suit == preferredSuits.First());

                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                            }
                        }

                        if (TeamMateIndex == player2)//co-
                        {
                            cardsToPlay = hands[MyIndex].Where(i =>
                                                   (!bannedSuit.HasValue || i.Suit != bannedSuit.Value) &&
                                                   Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                            .Select(h => new Card(i.Suit, h))
                                                            .Where(j => j.BadValue > i.BadValue)
                                                            .Any(j => _probabilities.CardProbability(player3, j) > 0 &&
                                                                        Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                            .Select(h => new Card(i.Suit, h))
                                                                            .Where(k => k.BadValue >= j.BadValue)
                                                                            .All(k => _probabilities.CardProbability(player2, k) == 0)));
                        }
                        else if (TeamMateIndex == player3)//c-o
                        {
                            cardsToPlay = hands[MyIndex].Where(i =>
                                                   (!bannedSuit.HasValue || i.Suit != bannedSuit.Value) &&
                                                   Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                            .Select(h => new Card(i.Suit, h))
                                                            .Where(j => j.BadValue > i.BadValue)
                                                            .Any(j => _probabilities.CardProbability(player2, j) > 0 &&
                                                                        Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                            .Select(h => new Card(i.Suit, h))
                                                                            .Where(k => k.BadValue >= j.BadValue)
                                                                            .All(k => _probabilities.CardProbability(player3, k) == 0)));
                        }
                        var prefCards = cardsToPlay.Where(i => preferredSuits.Contains(i.Suit));

                        if (prefCards.Any())
                        {
                            return prefCards.OrderBy(i => i.BadValue).First();
                        }

                        return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                    }
                };
                //        yield return new AiRule()
                //        {
                //            Order = 1,
                //            Description = "Zkus vítěznou kartu",
                //SkipSimulations = true,
                //           ChooseCard1 = () =>
                //           {
                //               IEnumerable<Card> cardsToPlay = Enumerable.Empty<Card>();

                //               if (TeamMateIndex == player2)//co-
                //               {
                //                   cardsToPlay = hands[MyIndex].Where(i => (!bannedSuit.HasValue || i.Suit != bannedSuit.Value) &&
                //                                                           _probabilities.SuitHigherThanCardProbability(player2, i, RoundNumber, false) <= RiskFactor &&
                //                                                           _probabilities.SuitHigherThanCardProbability(player3, i, RoundNumber, false) >= 1 - RiskFactor);

                //                   var certainCardsToPlay = cardsToPlay.Where(i => _probabilities.SuitProbability(player2, i.Suit, RoundNumber) == 0);
                //
                //                   if (certainCardsToPlay.Any())
                //                   {
                //                       return cardsToPlay.OrderBy(i => i.BadValue).First();
                //                   }
                //               }
                //               else if (TeamMateIndex == player3)//c-o
                //               {
                //                   cardsToPlay = hands[MyIndex].Where(i => (!bannedSuit.HasValue || i.Suit != bannedSuit.Value) &&
                //                                                           _probabilities.SuitHigherThanCardProbability(player2, i, RoundNumber, false) >= 1 - RiskFactor &&
                //                                                           _probabilities.SuitHigherThanCardProbability(player3, i, RoundNumber, false) <= RiskFactor);
                //
                //                   var certainCardsToPlay = cardsToPlay.Where(i => _probabilities.SuitProbability(player3, i.Suit, RoundNumber) == 0);
                //
                //                   if (certainCardsToPlay.Any())
                //                   {
                //                       return cardsToPlay.OrderBy(i => i.BadValue).First();
                //                   }
                //               }
                //
                //if (RoundNumber == 2 && _rounds != null && _rounds[0] != null) //pri simulaci hry jsou skutecny kola jeste neodehrany
                //{
                //	//zahrat viteznou kartu v 2. kole (kolega asi nezna barvu voliciho hrace z prvniho kola)
                //	var winner = cardsToPlay.FirstOrDefault(i => i.Suit == _rounds[0].c1.Suit);
                //
                //	if (winner != null)
                //	{
                //		return winner;
                //	}
                //}
                //        return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                //    }
                //};

                yield return new AiRule()
                {
                    Order = 7,
                    Description = "Hrát nízkou kartu (nieder)",
                    SkipSimulations = true,
                    ChooseCard1 = () =>
                    {
                        var cardsToPlay = Enumerable.Empty<Card>();
                        var spodek = new Card(Barva.Cerveny, Hodnota.Svrsek);
                        var hiCards = hands[MyIndex].Where(i => i.BadValue >= spodek.BadValue);
                        var loCards = hands[MyIndex].Where(i => i.BadValue < spodek.BadValue &&
                                                                hiCards.Any(j => j.Suit == i.Suit))
                                                    .Select(i => new Tuple<Card, int>(i,
                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Select(h => new Card(i.Suit, h))
                                                                        .Where(j => j.BadValue > i.BadValue &&
                                                                                    hiCards.First(k => k.Suit == i.Suit)
                                                                                           .BadValue > j.BadValue)
                                                                        .Count(j => _probabilities.CardProbability(opponent, j) > 0)))
                                                    .Where(i => i.Item2 > 0);
                        //pokud mam hodne vysokych karet (napr. jako na durcha), tak hrat rovnou nizkou
                        if (hiCards.Count() > 6 && loCards.Any())
                        {
                            var prefCards = loCards.Where(i => preferredSuits.Contains(i.Item1.Suit));

                            if (prefCards.Any())
                            {
                                var prefSuit = prefCards.OrderByDescending(i => i.Item2)
                                                        .ThenBy(i => i.Item1.BadValue)
                                                        .Select(i => i.Item1.Suit)
                                                        .First();

                                cardsToPlay = loCards.Where(i => i.Item1.Suit == prefSuit)
                                                     .Select(i => i.Item1);
                            }
                            else
                            {
                                //ber jen barvy kde nemam vysokou kartu
                                cardsToPlay = loCards.OrderByDescending(i => i.Item2)
                                                     .ThenBy(i => i.Item1.BadValue)
                                                     .Select(i => i.Item1)
                                                     .Take(1);
                            }
                        }
                        else if (RoundNumber > 2) //nizke karty nehraj zbytecne moc brzo
                        {
                            //hraj jen barvy kde nemam vysokou kartu (nejdriv hoch a az pak nieder)
                            //uprednostnuj barvu, ve ktere uz jsme hrali hoch
                            loCards = hands[MyIndex].Where(i => i.BadValue < spodek.BadValue &&
                                                                    (!hiCards.Any(j => j.Suit == i.Suit) ||
                                                                     hochCards.Any(j => j.Suit == i.Suit)))
                                                    .Select(i => new Tuple<Card, int>(i,
                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Select(h => new Card(i.Suit, h))
                                                                        .Where(j => j.BadValue > i.BadValue)
                                                                        .Count(j => _probabilities.CardProbability(opponent, j) > 0)))
                                                    .Where(i => i.Item2 > 0);
                            cardsToPlay = loCards.OrderBy(i => hochCards.Any(j => j.Suit == i.Item1.Suit)
                                                                ? 0
                                                                : 1)
                                                 .ThenByDescending(i => i.Item2)
                                                 .ThenBy(i => i.Item1.BadValue)
                                                 .ThenBy(i => _probabilities.SuitProbability(opponent, i.Item1.Suit, RoundNumber))
                                                 .Select(i => i.Item1)
                                                 .Take(1);
                        }

                        return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                    }
                };

                yield return new AiRule()
                {
                    Order = 6,
                    Description = "Odmazat si plonkovou kartu",
                    SkipSimulations = true,
                    ChooseCard1 = () =>
                    {
                        //pri 2. kole hraj jen devitku a vyssi (zbavovat se plonkove 7 nebo 8 moc brzy neni dobre)
                        //pozdeji hraj jakoukoli plonkovou kartu
                        var cardsToPlay = hands[MyIndex].Where(i => (!bannedSuit.HasValue || bannedSuit.Value != i.Suit) &&
                                                                        hands[MyIndex].CardCount(i.Suit) == 1 &&
                                                                        (RoundNumber > 2 || i.Value >= Hodnota.Devitka) &&
                                                                        Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                            .Select(h => new Card(i.Suit, h))
                                                                            .Where(j => j.BadValue > i.BadValue)
                                                                            .Any(j => _probabilities.CardProbability(opponent, j) > 0));
                        var prefCards = cardsToPlay.Where(i => preferredSuits.Contains(i.Suit));

                        if (prefCards.Any())
                        {
                            return prefCards.OrderByDescending(i => i.BadValue).First();
                        }

                        return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                    }
                };

                yield return new AiRule()
                {
                    Order = 8,
                    Description = "Hrát nejnižší od esa",
                    SkipSimulations = true,
                    ChooseCard1 = () =>
                    {
                        //pokud mam A, K, S, X tak hraj X (souper muze mit spodka)
                        var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                         .Select(h => new Card(i.Suit, h))
                                                                         .Where(j => j.BadValue > i.BadValue)
                                                                         .All(j => _probabilities.CardProbability(player2, j) == 0 &&
                                                                                   _probabilities.CardProbability(player3, j) == 0))
                                                         .Distinct();
                        var cardsToPlay = hands[MyIndex].Where(i => (!bannedSuit.HasValue || bannedSuit.Value != i.Suit) &&
                                                                    topCards.Count(j => j.Suit == i.Suit) > 2 &&
                                                                    !topCards.Contains(i));
                        var prefCards = cardsToPlay.Where(i => preferredSuits.Contains(i.Suit));

                        if (prefCards.Any())
                        {
                            return prefCards.OrderBy(i => i.BadValue).First();
                        }

                        return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                    }
                };  

                yield return new AiRule()
                {
                    Order = 9,
                    Description = "Hrát vysokou kartu (hoch)",
                    SkipSimulations = true,
                    ChooseCard1 = () =>
                    {
                        //napr. A a 8
                        //vysoka bude A nebo K, musim k ni mit nizkou a co nejvetsi diru mezi nima
                        var cardsToPlay = Enumerable.Empty<Card>();
                        var svrsek = new Card(Barva.Cerveny, Hodnota.Svrsek);
                        var hiCards = hands[MyIndex].Where(i => i.BadValue > svrsek.BadValue);
                        var loCards = hands[MyIndex].Where(i => i.BadValue <= svrsek.BadValue &&
                                                                hiCards.Any(j => j.Suit == i.Suit))
                                                    .Select(i => new Tuple<Card, int>(i,
                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Select(h => new Card(i.Suit, h))
                                                                        .Where(j => j.BadValue > i.BadValue &&
                                                                                    hiCards.First(k => k.Suit == i.Suit)
                                                                                           .BadValue > j.BadValue)
                                                                        .Count(j => _probabilities.CardProbability(opponent, j) > 0)))
                                                    .Where(i => i.Item2 > 0);
                        //Item1 je nizka karta a Item2 je pocet der mezi ni a vysokou kartou
                        //nehrat pokud mam prilis mnoho vysokych karet
                        if (hiCards.Count() <= 6 && loCards.Any())
                        {
                            var prefCards = loCards.Where(i => preferredSuits.Contains(i.Item1.Suit));

                            if (prefCards.Any())
                            {
                                var prefSuit = prefCards.OrderByDescending(i => i.Item2)
                                                        .ThenByDescending(i => i.Item1.BadValue)
                                                        .Select(i => i.Item1.Suit)
                                                        .First();
                                cardsToPlay = hiCards.Where(i => i.Suit == prefSuit);
                            }
                            else
                            {
                                var loCard = loCards.OrderByDescending(i => i.Item2)
                                                    .ThenByDescending(i => i.Item1.BadValue)
                                                    .Select(i => i.Item1)
                                                    .FirstOrDefault();
                                if (loCard != null)
                                {
                                    cardsToPlay = hiCards.Where(i => i.Suit == loCard.Suit);
                                }
                            }
                        }
                        return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                    }
                };
                //       yield return new AiRule()
                //       {
                //           Order = 7,
                //           Description = "Odmazat si vysokou kartu",
                //           SkipSimulations = true,
                //           ChooseCard1 = () =>
                //           {
                //               var cardsToPlay = new List<Card>();

                //               //hi1 = pocet mych karet > nejmensi souperova
                //               //hi2 = pocet kolegovych karet > nejmensi souperova
                //               //mid1 = pocet souperovych karet > nejmensi moje
                //               //mid2 = pocet souperovych karet > nejmensi kolegy
                //               foreach (var barva in Enum.GetValues(typeof(Barva)).Cast<Barva>().Where(i => (!bannedSuit.HasValue || i != bannedSuit.Value)))
                //               {
                //                   if (hands[MyIndex].HasSuit(barva) && _probabilities.SuitProbability(opponent, barva, RoundNumber) > 0)
                //                   {
                //                       var low1 = hands[MyIndex].Where(i => i.Suit == barva)
                //                                                .OrderBy(i => i.BadValue)
                //                                                .FirstOrDefault()
                //                                  ?? new Card(barva, Hodnota.Eso);
                //                       var low2 = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                //                                      .Select(h => new Card(barva, h))
                //                                      .OrderBy(i => i.BadValue)
                //                                      .FirstOrDefault(i => _probabilities.CardProbability(TeamMateIndex, i) > 0)
                //                                  ?? new Card(barva, Hodnota.Eso);
                //                       var oplow = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                //                                       .Select(h => new Card(barva, h))
                //                                       .OrderBy(i => i.BadValue)
                //                                       .FirstOrDefault(i => _probabilities.CardProbability(opponent, i) > 0)
                //                                   ?? new Card(barva, Hodnota.Eso);
                //                       var hi1 = hands[MyIndex].Count(i => i.Suit == barva && i.BadValue > oplow.BadValue);
                //                       var hi2 = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                //                                     .Select(h => new Card(barva, h))
                //                                     .Count(i => i.BadValue > oplow.BadValue &&
                //                                                 _probabilities.CardProbability(TeamMateIndex, i) > 0);
                //                       var mid1 = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                //                                      .Select(h => new Card(barva, h))
                //                                      .Count(i => i.BadValue > low1.BadValue &&
                //                                                  _probabilities.CardProbability(opponent, i) > 0);
                //                       var mid2 = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                //                                                                      .Select(h => new Card(barva, h))
                //                                      .Count(i => i.BadValue > low2.BadValue &&
                //                                                  _probabilities.CardProbability(opponent, i) > 0);

                //                       //odmazavat ma smysl jen tehdy pokud:
                //                       //mame nejmensi kartu a
                //                       //nasich vysokych karet je mene nez souperovych strednich karet
                //                       //(aby souperovi nejake karty zbyly pote co si vysoke odmazeme)
                //                       if ((low1.IsLowerThan(oplow, null) && hi2 > 0 && hi2 < mid1) ||
                //                           (low2.IsLowerThan(oplow, null) && hi1 > 0 && hi1 < mid2))
                //                       {
                //                           cardsToPlay.Add(hands[MyIndex].Where(i => i.Suit == barva)
                //                                                         .OrderByDescending(i => i.BadValue)
                //                                                         .First());
                //                       }
                //                   }
                //               }

                //return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                //    }
                //};
            }

            yield return new AiRule()
            {
                Order = 10,
                Description = "Zkusit soupeře chytit",
                ChooseCard1 = () =>
                {
                    var cardsToPlay = hands[MyIndex].Where(i => 
                                                   (!bannedSuit.HasValue || i.Suit != bannedSuit.Value) &&
                                                   _probabilities.SuitHigherThanCardProbability(opponent, i, RoundNumber, false) > 0);

                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault(); //nejmensi karta
                }
            };

            yield return new AiRule()
            {
                Order = 11,
                Description = "Dostat spoluhráče do štychu",
                ChooseCard1 = () =>
                {
                    var cardsToPlay = hands[MyIndex].Where(i => 
                                                   (!bannedSuit.HasValue || i.Suit != bannedSuit.Value) &&
                                                   _probabilities.SuitHigherThanCardProbability(TeamMateIndex, i, RoundNumber, false) > 0);   //seskup podle barev

                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault(); //nejmensi karta
                }
            };

            yield return new AiRule()
            {
                Order = 12,
                Description = "Hrát krátkou barvu",
                ChooseCard1 = () =>
                {
                    var cardsToPlay = hands[MyIndex].Where(i => i != null);

                    return cardsToPlay.OrderByDescending(i => _probabilities.SuitProbability(opponent, i.Suit, RoundNumber))
                                      .ThenBy(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
                                      .ThenBy(i => hands[MyIndex].CardCount(i.Suit))
                                      .ThenBy(i => i.BadValue).FirstOrDefault();
                }
            };
        }

        protected override IEnumerable<AiRule> GetRules2(Hand[] hands)
        {
            var player3 = (MyIndex + 1) % Game.NumPlayers;
            var player1 = (MyIndex + 2) % Game.NumPlayers;
            var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                            ? (MyIndex + 2) % Game.NumPlayers 
                            : (MyIndex + 1) % Game.NumPlayers;

            var preferredSuits = new List<Barva>();
            //var hochCards = new List<Card>();

            if (TeamMateIndex != -1 && _rounds != null && _rounds[0] != null)
            {
                if (RoundNumber == 2)
                {
                    //pokud v 1.kole vsichni priznali barvu ale spoluhrac nesel vejs
                    if (_rounds[0].c1.Suit == _rounds[0].c2.Suit &&
                        (_rounds[0].player2.PlayerIndex == TeamMateIndex &&
                         _rounds[0].c1.BadValue > _rounds[0].c2.BadValue) ||
                        (_rounds[0].player3.PlayerIndex == TeamMateIndex &&
                         _rounds[0].c1.BadValue > _rounds[0].c3.BadValue))
                    {
                        preferredSuits.Add(_rounds[0].c1.Suit);
                    }
                    if (_rounds[0].c1.Suit == _rounds[0].c2.Suit &&
                        (_rounds[0].player2.PlayerIndex == TeamMateIndex &&
                         _rounds[0].c1.BadValue > _rounds[0].c2.BadValue) ||
                        (_rounds[0].player3.PlayerIndex == TeamMateIndex &&
                         _rounds[0].c1.BadValue > _rounds[0].c3.BadValue))
                    {
                        preferredSuits.Add(_rounds[0].c1.Suit);
                    }
                    //pokud v 2.kole spoluhrac nepriznal barvu a jeste nejake karty v barve zbyvaji
                    if (hands[MyIndex].CardCount(_rounds[0].c1.Suit) < 6 &&
                        ((_rounds[0].player2.PlayerIndex == TeamMateIndex &&
                          _rounds[0].c1.Suit != _rounds[0].c2.Suit) ||
                         (_rounds[0].player3.PlayerIndex == TeamMateIndex &&
                          _rounds[0].c1.Suit != _rounds[0].c3.Suit)))
                    {
                        preferredSuits.Add(_rounds[0].c1.Suit);
                    }
                }
                if (!preferredSuits.Any())
                {
                    //prednostne zkousej hrat barvu kterou spoluhrac odmazaval
                    for (var i = 0; i < RoundNumber - 1; i++)
                    {
                        if (_rounds[i].player2.PlayerIndex == TeamMateIndex &&
                             _rounds[i].c1.Suit != _rounds[i].c2.Suit)
                        {
                            preferredSuits.Add(_rounds[i].c2.Suit);
                        }
                        if (_rounds[i].player3.PlayerIndex == TeamMateIndex &&
                            _rounds[i].c1.Suit != _rounds[i].c3.Suit)
                        {
                            preferredSuits.Add(_rounds[i].c3.Suit);
                        }
                    }
                }
                //var svrsek = new Card(Barva.Cerveny, Hodnota.Svrsek);

                //for (var i = 0; i < RoundNumber - 1; i++)
                //{
                //    if (_rounds[i].roundWinner.PlayerIndex == MyIndex)
                //    {
                //        if (_rounds[i].player1.PlayerIndex == MyIndex &&
                //          _rounds[i].c1.BadValue > svrsek.BadValue)
                //        {
                //            hochCards.Add(_rounds[i].c1);
                //        }
                //        else if (_rounds[i].player2.PlayerIndex == MyIndex &&
                //          _rounds[i].c2.BadValue > svrsek.BadValue)
                //        {
                //            hochCards.Add(_rounds[i].c2);
                //        }
                //        else if (_rounds[i].player3.PlayerIndex == MyIndex &&
                //          _rounds[i].c3.BadValue > svrsek.BadValue)
                //        {
                //            hochCards.Add(_rounds[i].c3);
                //        }
                //    }
                //}
            }

            yield return new AiRule()
            {
                Order = 0,
                Description = "Dostat se do štychu (hoch)",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex != -1)
                    {
                        if (ValidCards(c1, hands[MyIndex]).All(i => i.Suit == c1.Suit))
                        {
                            var svrsek = new Card(Barva.Cerveny, Hodnota.Svrsek);
                            var hiCards = hands[MyIndex].Where(i => i.Suit == c1.Suit &&
                                                                    i.BadValue > svrsek.BadValue);
                            var loCards = hands[MyIndex].Where(i => i.Suit == c1.Suit &&
                                                                    i.BadValue <= svrsek.BadValue)
                                                        .Select(i => new Tuple<Card, int>(i,
                                                                        Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                            .Select(h => new Card(i.Suit, h))
                                                                            .Where(j => j.BadValue > i.BadValue &&
                                                                                        hiCards.First(k => k.Suit == i.Suit)
                                                                                               .BadValue > j.BadValue)
                                                                            .Count(j => _probabilities.CardProbability(opponent, j) > 0)))
                                                        .Where(i => i.Item2 > 0);

                            if (hiCards.Any() && loCards.Any())
                            {
                                cardsToPlay = ValidCards(c1, hands[MyIndex]);
                            }
                        }

                        if (c1.Value == Hodnota.Sedma ||
                            c1.Value == Hodnota.Osma)
                        {
                            //zkusíme soupeře dostat nízkýma kartama (v následujícím pravidle)
                            return null;
                        }
                    }
                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 1,
                Description = "Hrát nízkou kartu (nieder)",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();
                    var svrsek = new Card(Barva.Cerveny, Hodnota.Svrsek);

                    if (TeamMateIndex != -1)
                    {
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == c1.Suit &&
                                                                                i.BadValue > c1.BadValue);
                    }
                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "Odmazat si vysokou kartu",
                SkipSimulations = true,
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == -1)//-c-
                    {
                        if (ValidCards(c1, hands[MyIndex]).All(i => i.Suit != c1.Suit))
                        {
                            var hiCards = ValidCards(c1, hands[MyIndex]).Select(i => new Tuple<Card, int>(i,
                                                                           Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                               .Select(h => new Card(i.Suit, h))
                                                                               .Where(j => j.BadValue < i.BadValue)
                                                                               .Count(j => _probabilities.CardProbability(player1, j) > 0 ||
                                                                                           _probabilities.CardProbability(player3, j) > 0)))
                                                                        .Where(i => i.Item2 > 0);
                            cardsToPlay = hiCards.OrderByDescending(i => i.Item2)
                                                 .ThenByDescending(i => i.Item1.BadValue)
                                                 .Select(i => i.Item1);
                        }
                        else
                        {
                            cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == c1.Suit &&
                                                                                    i.BadValue < c1.BadValue &&
                                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                        .Select(h => new Card(i.Suit, h))
                                                                                        .Where(j => j.BadValue < i.BadValue)
                                                                                        .Any(j => _probabilities.CardProbability(player1, j) > 0 ||
                                                                                                  _probabilities.CardProbability(player3, j) > 0));

                            return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                        }
                    }
                    else //oc-
                    {
                        var hiCards = ValidCards(c1, hands[MyIndex]).Select(i => new Tuple<Card, int>(i,
                                                                       Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                           .Select(h => new Card(i.Suit, h))
                                                                           .Where(j => j.BadValue < i.BadValue)
                                                                           .Count(j => _probabilities.CardProbability(opponent, j) > 0)))
                                                                    .Where(i => i.Item2 > 0);
                        var prefCards = hiCards.Where(i => preferredSuits.Any(j => j == i.Item1.Suit))
                                               .Select(i => i.Item1);

                        if (prefCards.Any())
                        {
                            cardsToPlay = prefCards.OrderByDescending(i => i.BadValue);
                        }
                        else
                        {
                            cardsToPlay = hiCards.OrderByDescending(i => i.Item2)
                                                 .ThenByDescending(i => i.Item1.BadValue)
                                                 .Select(i => i.Item1);
                        }
                    }

                    return cardsToPlay.FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "Hrát cokoli",
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
            var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                            ? (MyIndex + 2) % Game.NumPlayers 
                            : (MyIndex + 1) % Game.NumPlayers;

            var preferredSuits = new List<Barva>();
            //var hochCards = new List<Card>();

            if (TeamMateIndex != -1 && _rounds != null && _rounds[0] != null)
            {
                if (RoundNumber == 2)
                {
                    //pokud v 1.kole vsichni priznali barvu ale spoluhrac nesel vejs
                    if (_rounds[0].c1.Suit == _rounds[0].c2.Suit &&
                        (_rounds[0].player2.PlayerIndex == TeamMateIndex &&
                         _rounds[0].c1.BadValue > _rounds[0].c2.BadValue) ||
                        (_rounds[0].player3.PlayerIndex == TeamMateIndex &&
                         _rounds[0].c1.BadValue > _rounds[0].c3.BadValue))
                    {
                        preferredSuits.Add(_rounds[0].c1.Suit);
                    }
                    if (_rounds[0].c1.Suit == _rounds[0].c2.Suit &&
                        (_rounds[0].player2.PlayerIndex == TeamMateIndex &&
                         _rounds[0].c1.BadValue > _rounds[0].c2.BadValue) ||
                        (_rounds[0].player3.PlayerIndex == TeamMateIndex &&
                         _rounds[0].c1.BadValue > _rounds[0].c3.BadValue))
                    {
                        preferredSuits.Add(_rounds[0].c1.Suit);
                    }
                    //pokud v 2.kole spoluhrac nepriznal barvu a jeste nejake karty v barve zbyvaji
                    if (hands[MyIndex].CardCount(_rounds[0].c1.Suit) < 6 &&
                        ((_rounds[0].player2.PlayerIndex == TeamMateIndex &&
                          _rounds[0].c1.Suit != _rounds[0].c2.Suit) ||
                         (_rounds[0].player3.PlayerIndex == TeamMateIndex &&
                          _rounds[0].c1.Suit != _rounds[0].c3.Suit)))
                    {
                        preferredSuits.Add(_rounds[0].c1.Suit);
                    }
                }
                if (!preferredSuits.Any())
                {
                    //prednostne zkousej hrat barvu kterou spoluhrac odmazaval
                    for (var i = 0; i < RoundNumber - 1; i++)
                    {
                        if (_rounds[i].player2.PlayerIndex == TeamMateIndex &&
                             _rounds[i].c1.Suit != _rounds[i].c2.Suit)
                        {
                            preferredSuits.Add(_rounds[i].c2.Suit);
                        }
                        if (_rounds[i].player3.PlayerIndex == TeamMateIndex &&
                            _rounds[i].c1.Suit != _rounds[i].c3.Suit)
                        {
                            preferredSuits.Add(_rounds[i].c3.Suit);
                        }
                    }
                }

                //var svrsek = new Card(Barva.Cerveny, Hodnota.Svrsek);

                //for (var i = 0; i < RoundNumber - 1; i++)
                //{
                //    if (_rounds[i].roundWinner.PlayerIndex == MyIndex)
                //    {
                //        if (_rounds[i].player1.PlayerIndex == MyIndex &&
                //          _rounds[i].c1.BadValue > svrsek.BadValue)
                //        {
                //            hochCards.Add(_rounds[i].c1);
                //        }
                //        else if (_rounds[i].player2.PlayerIndex == MyIndex &&
                //          _rounds[i].c2.BadValue > svrsek.BadValue)
                //        {
                //            hochCards.Add(_rounds[i].c2);
                //        }
                //        else if (_rounds[i].player3.PlayerIndex == MyIndex &&
                //          _rounds[i].c3.BadValue > svrsek.BadValue)
                //        {
                //            hochCards.Add(_rounds[i].c3);
                //        }
                //    }
                //}
            }

            yield return new AiRule()
            {
                Order = 0,
                Description = "Hrát vítěznou kartu",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex != -1) 
                    {
                        if (RoundNumber == 1) //-oc
                        {
                            cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => Round.WinningCard(c1, c2, i, null) == c1);
                        }
                        else //o-c
                        {
                            cardsToPlay = ValidCards(c1, c2, hands[MyIndex]).Where(i => Round.WinningCard(c1, c2, i, null) == c2);
                        }
                    }

                    return cardsToPlay.ToList().RandomOneOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "Odmazat si vysokou kartu",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == -1)//--c
                    {
                        if (ValidCards(c1, c2, hands[MyIndex]).All(i => i.Suit != c1.Suit))
                        {
                            var hiCards = ValidCards(c1, c2, hands[MyIndex]).Select(i => new Tuple<Card, int>(i,
                                                                           Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                               .Select(h => new Card(i.Suit, h))
                                                                               .Where(j => j.BadValue < i.BadValue)
                                                                               .Count(j => _probabilities.CardProbability(player1, j) > 0 ||
                                                                                           _probabilities.CardProbability(player2, j) > 0)))
                                                                        .Where(i => i.Item2 > 0);
                            cardsToPlay = hiCards.OrderByDescending(i => i.Item2)
                                                 .ThenByDescending(i => i.Item1.BadValue)
                                                 .Select(i => i.Item1);
                        }
                    }
                    else //o-c
                    {
                        var hiCards = ValidCards(c1, c2, hands[MyIndex]).Select(i => new Tuple<Card, int>(i,
                                                                       Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                           .Select(h => new Card(i.Suit, h))
                                                                           .Where(j => j.BadValue < i.BadValue)
                                                                           .Count(j => _probabilities.CardProbability(opponent, j) > 0)))
                                                                    .Where(i => i.Item2 > 0);
                        var prefCards = hiCards.Where(i => preferredSuits.Any(j => j == i.Item1.Suit))
                                               .Select(i => i.Item1);

                        if (prefCards.Any())
                        {
                            cardsToPlay = prefCards.OrderByDescending(i => i.BadValue);
                        }
                        else
                        {
                            cardsToPlay = hiCards.OrderByDescending(i => i.Item2)
                                                 .ThenByDescending(i => i.Item1.BadValue)
                                                 .Select(i => i.Item1);
                        }
                    }

                    return cardsToPlay.FirstOrDefault();
                }
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "Hrát cokoli",
                SkipSimulations = true,
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]);

                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
            };
        }
    }
}
