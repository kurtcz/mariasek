using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine
{
    public class AiBetlStrategy : AiStrategyBase
    {
        public float RiskFactor { get; set; }

        public AiBetlStrategy(Barva? trump, Hra gameType, Hand[] hands, Round[] rounds, List<Barva> teamMatesSuits, Probability probabilities, IStringLogger debugString)
            : base(trump, gameType, hands, rounds, teamMatesSuits, probabilities, debugString)
        {
            RiskFactor = 0.275f; //0.2727f ~ (9 nad 5) / (11 nad 5)
		}

        protected override IEnumerable<AiRule> GetRules1(Hand[] hands)
        {
            #region InitVariables
            var player2 = (MyIndex + 1) % Game.NumPlayers;
            var player3 = (MyIndex + 2) % Game.NumPlayers;
            var opponent = TeamMateIndex == player2 ? player3 : player2;

            var bannedSuits = new List<Barva>();
            var preferredSuits = new List<Barva>();
            var hochCards = new List<Card>();
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
            var initialTopCards = myInitialHand.Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                               .Select(h => new Card(i.Suit, h))
                                                               .Where(j => j.BadValue > i.BadValue)
                                                               .All(j => myInitialHand.Contains(j)))
                                               .ToList();

            if (TeamMateIndex != -1 && _rounds != null && _rounds[0] != null)
            {
                //pokud v 1.kole spoluhrac nepriznal barvu a jeste nejake karty v barve zbyvaji
                //a zaroven souper muze mit vyssi kartu v barve nez mam ja sam
                if (hands[MyIndex].HasSuit(_rounds[0].c1.Suit) &&
                    hands[MyIndex].CardCount(_rounds[0].c1.Suit) < 6 &&
                    ((_rounds[0].player2.PlayerIndex == TeamMateIndex &&
                      _rounds[0].c1.Suit != _rounds[0].c2.Suit) ||
                     (_rounds[0].player3.PlayerIndex == TeamMateIndex &&
                      _rounds[0].c1.Suit != _rounds[0].c3.Suit)) &&
                    (_probabilities.PotentialCards(opponent).HasSuit(_rounds[0].c1.Suit) ||     //akter musi mit aspon jednu kartu v barve na ruce (plus 2 mohl dat do talonu)
                     hands[MyIndex].Any(i => i.Suit == _rounds[0].c1.Suit &&                          //nebo ma nejakou vyssi kartu
                                             Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                 .Select(h => new Card(_rounds[0].c1.Suit, h))
                                                 .Where(j => j.BadValue > i.BadValue)
                                                 .Any(j => _probabilities.CardProbability(opponent, j) > 0))))
                {
                    if (_probabilities.PotentialCards(opponent).CardCount(_rounds[0].c1.Suit) > 2)  //nejdrive hraj akterovu barvu (dokud ji jiste ma)
                    {
                        preferredSuits.Add(_rounds[0].c1.Suit);
                    }
                    else if (_rounds[0].player2.PlayerIndex == TeamMateIndex)                       //a potom barvu kterou kolega odmazaval
                    {
                        preferredSuits.Add(_rounds[0].c2.Suit);
                    }
                    else
                    {
                        preferredSuits.Add(_rounds[0].c3.Suit);
                    }
                }
                if (RoundNumber == 2)
                {
                    ////pokud v 1.kole vsichni priznali barvu ale spoluhrac nesel vejs (a bylo kam jit vejs)
                    ////a zaroven souper muze mit vyssi kartu v barve nez mam ja sam
                    if (((_rounds[0].player2.PlayerIndex == TeamMateIndex &&
                          _rounds[0].c1.Suit == _rounds[0].c2.Suit &&
                          _rounds[0].c1.BadValue > _rounds[0].c2.BadValue) ||
                         (_rounds[0].player3.PlayerIndex == TeamMateIndex &&
                          _rounds[0].c1.Suit == _rounds[0].c3.Suit &&
                          (_rounds[0].c1.BadValue > _rounds[0].c3.BadValue ||
                           (_rounds[0].c2.BadValue > _rounds[0].c3.BadValue &&
                            _rounds[0].c2.Value != Hodnota.Eso)))) &&
                        hands[MyIndex].Any(i => i.Suit == _rounds[0].c1.Suit &&
                                                          Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                              .Select(h => new Card(_rounds[0].c1.Suit, h))
                                                              .Where(j => j.BadValue > i.BadValue)
                                                              .Any(j => _probabilities.CardProbability(opponent, j) > 0)))
                    {
                        preferredSuits.Add(_rounds[0].c1.Suit);
                    }
                    if (_rounds[0].c1.Suit == _rounds[0].c2.Suit && _rounds[0].c1.Suit == _rounds[0].c3.Suit)
                    {
                        bannedSuits.Add(_rounds[0].c1.Suit);
                    }
                }
                //else
                if (RoundNumber > 2)
                {
                    var playedCards = new List<Card>() { _rounds[0].c1, _rounds[0].c2, _rounds[0].c3 };

                    for (var i = 1; i < RoundNumber - 1; i++)
                    {
                        //pokud v nejakem kole zahral akter nejnizsi kartu v barve a ja nemam nizkyho chytaka, tak predpokladej, ze barvu nezna
                        var minPossibleOpponentCardInSuit = Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                .Select(h => new Card(_rounds[i].c1.Suit, h))
                                                                .Where(c => !playedCards.Contains(c) &&
                                                                            !hands[MyIndex].Any(j => j.Suit == c.Suit &&
                                                                                                      j.Value == c.Value))
                                                                .OrderBy(c => c.BadValue)
                                                                .FirstOrDefault()
                                                            ?? new Card(_rounds[i].c1.Suit, Hodnota.Eso);

                        if (_rounds[i].player2.PlayerIndex == MyIndex ||
                            _rounds[i].player2.PlayerIndex == TeamMateIndex)
                        {
                            if (_rounds[i].c1.Suit == _rounds[i].c3.Suit &&
                                _rounds[i].c1.BadValue > _rounds[i].c3.BadValue &&
                                _rounds[i].c3.BadValue == minPossibleOpponentCardInSuit.BadValue &&
                                hands[MyIndex].Where(j => j.Suit == _rounds[i].c1.Suit &&
                                                           j.BadValue < Card.GetBadValue(Hodnota.Spodek))
                                               .All(j => _probabilities.SuitHigherThanCardProbability(_rounds[i].player3.PlayerIndex, j, RoundNumber) == 0))
                            {
                                bannedSuits.Add(_rounds[i].c1.Suit);
                            }
                        }
                        else if (_rounds[i].player3.PlayerIndex == MyIndex ||
                                 _rounds[i].player3.PlayerIndex == TeamMateIndex)
                        {
                            if (_rounds[i].c1.Suit == _rounds[i].c2.Suit &&
                                _rounds[i].c1.BadValue > _rounds[i].c2.BadValue &&
                                _rounds[i].c2.BadValue == minPossibleOpponentCardInSuit.BadValue &&
                                hands[MyIndex].Where(j => j.Suit == _rounds[i].c1.Suit &&
                                                           j.BadValue < Card.GetBadValue(Hodnota.Spodek))
                                               .All(j => _probabilities.SuitHigherThanCardProbability(_rounds[i].player2.PlayerIndex, j, RoundNumber) == 0))
                            {
                                bannedSuits.Add(_rounds[i].c1.Suit);
                            }
                        }

                        playedCards.Add(_rounds[i].c1);
                        playedCards.Add(_rounds[i].c2);
                        playedCards.Add(_rounds[i].c3);
                    }
                    if (!preferredSuits.Contains(_rounds[0].c1.Suit))
                    {
                        bannedSuits.Add(_rounds[0].c1.Suit);
                    }
                    bannedSuits = bannedSuits.Distinct().ToList();
                    if (bannedSuits.Count() == Game.NumSuits)
                    {
                        bannedSuits.Clear();
                    }
                }
                if (!preferredSuits.Any())
                {
                    //primarne zkus hrat barvu kterou akter odmazaval
                    for (var i = 0; i < RoundNumber - 1; i++)
                    {
                        if (_rounds[i].player2.TeamMateIndex == -1 &&
                            _rounds[i].c2.Suit != _rounds[i].c1.Suit)
                        {
                            preferredSuits.Add(_rounds[i].c2.Suit);
                        }
                        else if (_rounds[i].player3.TeamMateIndex == -1 &&
                                 _rounds[i].c3.Suit != _rounds[i].c1.Suit)
                        {
                            preferredSuits.Add(_rounds[i].c3.Suit);
                        }
                    }
                }
                if (!preferredSuits.Any())
                {
                    //nasledne zkus hrat barvu kterou akter hral a nebyla nejnizsi (ignoruj prvni kolo)
                    for (var i = 1; i < RoundNumber - 1; i++)
                    {
                        if (_rounds[i].c1.Suit == _rounds[i].c2.Suit &&
                            _rounds[i].c1.Suit == _rounds[i].c3.Suit)
                        {
                            if (_rounds[i].player2.TeamMateIndex == -1 &&
                                (_rounds[i].c2.BadValue > _rounds[i].c1.BadValue ||
                                 _rounds[i].c2.BadValue > _rounds[i].c3.BadValue))
                            {
                                preferredSuits.Add(_rounds[i].c2.Suit);
                            }
                            else if (_rounds[i].player3.TeamMateIndex == -1 &&
                                (_rounds[i].c3.BadValue > _rounds[i].c1.BadValue ||
                                 _rounds[i].c3.BadValue > _rounds[i].c2.BadValue))
                            {
                                preferredSuits.Add(_rounds[i].c3.Suit);
                            }
                        }
                    }
                }
                //if (!preferredSuits.Any())
                //{
                //    //dale zkus hrat barvu kterou spoluhrac neznal
                //    for (var i = 0; i < RoundNumber - 1; i++)
                //    {
                //        if (_rounds[i].player2.PlayerIndex == TeamMateIndex &&
                //            _rounds[i].c1.Suit != _rounds[i].c2.Suit &&
                //            _rounds[i].c1.Suit == _rounds[i].c3.Suit &&
                //            hands[MyIndex].HasSuit(_rounds[i].c3.Suit))
                //        {
                //            preferredSuits.Add(_rounds[i].c3.Suit);
                //        }
                //        if (_rounds[i].player3.PlayerIndex == TeamMateIndex &&
                //            _rounds[i].c1.Suit != _rounds[i].c3.Suit &&
                //            _rounds[i].c1.Suit == _rounds[i].c2.Suit &&
                //            hands[MyIndex].HasSuit(_rounds[i].c2.Suit))
                //        {
                //            preferredSuits.Add(_rounds[i].c2.Suit);
                //        }
                //    }
                //}
                //if (!preferredSuits.Any())
                {
                    //nakonec zkus hrat barvu kterou spoluhrac odmazaval
                    for (var i = 0; i < RoundNumber - 1; i++)
                    {
                        if (_rounds[i].player2.PlayerIndex == TeamMateIndex &&
                             _rounds[i].c1.Suit != _rounds[i].c2.Suit)
                        {
                            for (var j = 0; j < i; j++)
                            {
                                //pokud kolega v nejakem kole vyjel barvou, kterou pote ale neodmazaval, tak uz ji asi nema
                                if (_rounds[j].player1.PlayerIndex == TeamMateIndex &&
                                    _rounds[j].c1.Suit != _rounds[i].c2.Suit)
                                {
                                    preferredSuits.Add(_rounds[j].c1.Suit);
                                }
                            }
                            preferredSuits.Add(_rounds[i].c2.Suit);
                        }
                        if (_rounds[i].player3.PlayerIndex == TeamMateIndex &&
                            _rounds[i].c1.Suit != _rounds[i].c3.Suit)
                        {
                            for (var j = 0; j < i; j++)
                            {
                                //pokud kolega v nejakem kole vyjel barvou, kterou pote ale neodmazaval, tak uz ji asi nema
                                if (_rounds[j].player1.PlayerIndex == TeamMateIndex &&
                                    _rounds[j].c1.Suit != _rounds[i].c3.Suit)
                                {
                                    preferredSuits.Add(_rounds[j].c1.Suit);
                                }
                            }
                            preferredSuits.Add(_rounds[i].c3.Suit);
                        }
                    }
                }
                preferredSuits = preferredSuits.Distinct().ToList();

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
            #endregion

            if (TeamMateIndex == -1)
            {
                yield return new AiRule()
                {
                    Order = 0,
                    Description = "Vytlač jedinou díru v barvě",
                    SkipSimulations = true,
                    #region GetRules1 Rule0
                    ChooseCard1 = () =>
                    {
                        var cardsToPlay = hands[MyIndex].Where(i => hands[MyIndex].CardCount(i.Suit) +
                                                                    _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit) == 7 &&
                                                                    (hands[MyIndex].Has7(i.Suit) ||
                                                                     hands[Game.TalonIndex].Has7(i.Suit)) &&
                                                                    (hands[MyIndex].HasA(i.Suit) ||
                                                                     hands[Game.TalonIndex].HasA(i.Suit)) &&
                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Select(h => new Card(i.Suit, h))
                                                                        .Where(j => j.BadValue > i.BadValue)
                                                                        .Any(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                  _probabilities.CardProbability(player3, j) > 0));

                        return cardsToPlay.ToList().RandomOneOrDefault();
                    }
                    #endregion
                };

                yield return new AiRule()
                {
                    Order = 1,
                    Description = "Vytlač dvě díry v barvě",
                    SkipSimulations = true,
                    #region GetRules1 Rule1
                    ChooseCard1 = () =>
                    {
                        var cardsToPlay = hands[MyIndex].Where(i => hands[MyIndex].CardCount(i.Suit) +
                                                                    _probabilities.CertainCards(Game.TalonIndex).CardCount(i.Suit) == 6 &&
                                                                    hands[MyIndex].Any(j => j.Suit == i.Suit &&
                                                                                            (_probabilities.PotentialCards(player2).Any(k => k.Suit == j.Suit &&
                                                                                                                                             k.BadValue < j.BadValue) ||
                                                                                             _probabilities.PotentialCards(player3).Any(k => k.Suit == j.Suit &&
                                                                                                                                             k.BadValue < j.BadValue))) &&
                                                                    (hands[MyIndex].HasA(i.Suit) ||
                                                                     hands[MyIndex].HasK(i.Suit) ||
                                                                     hands[Game.TalonIndex].HasA(i.Suit) ||
                                                                     hands[Game.TalonIndex].HasK(i.Suit)) &&
                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Select(h => new Card(i.Suit, h))
                                                                        .Where(j => j.BadValue > i.BadValue)
                                                                        .Any(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                  _probabilities.CardProbability(player3, j) > 0));

                        return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                    }
                    #endregion
                };

                yield return new AiRule()
                {
                    Order = 2,
                    Description = "Zbav se plonka",
                    SkipSimulations = true,
                    #region GetRules1 Rule2
                    ChooseCard1 = () =>
                    {
                        var hiCards = ValidCards(hands[MyIndex]).Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                .Select(h => new Card(i.Suit, h))
                                                                                .Where(j => j.BadValue > i.BadValue)
                                                                                .Any(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                          _probabilities.CardProbability(player3, j) > 0) &&
                                                                            Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                .Select(h => new Card(i.Suit, h))
                                                                                .Where(j => j.BadValue < i.BadValue)
                                                                                .Any(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                          _probabilities.CardProbability(player3, j) > 0))
                                                                .Select(i => new Tuple<Card, int>(i,
                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Select(h => new Card(i.Suit, h))
                                                                        .Where(j => j.BadValue < i.BadValue)
                                                                        .Count(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                    _probabilities.CardProbability(player3, j) > 0)))
                                                                .ToList();
                        var cardsToPlay = hands[MyIndex].Where(i => i.Value != Hodnota.Eso &&
                                                                    i.Value != Hodnota.Osma &&
                                                                    i.Value != Hodnota.Sedma &&
                                                                    hands[MyIndex].CardCount(i.Suit) == 1 &&
                                                                    (_probabilities.SuitHigherThanCardProbability(player2, i, RoundNumber, false) > 0 ||
                                                                     _probabilities.SuitHigherThanCardProbability(player3, i, RoundNumber, false) > 0) &&
                                                                    hiCards.Any(j => j.Item1 == i) &&   //nehraj plonka pokud jina karta odmaze vic der
                                                                    i == hiCards.OrderByDescending(j => j.Item2)
                                                                                .ThenByDescending(j => j.Item1.BadValue)
                                                                                .Select(j => j.Item1)
                                                                                .First());

                        return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                    }
                    #endregion
                };

                yield return new AiRule()
                {
                    Order = 3,
                    Description = "Odmazat si vysokou kartu",
                    SkipSimulations = true,
                    #region GetRules1 Rule3
                    ChooseCard1 = () =>
                    {
                        var hiCards = ValidCards(hands[MyIndex]).Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                .Select(h => new Card(i.Suit, h))
                                                                                .Where(j => j.BadValue > i.BadValue)
                                                                                .Any(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                          _probabilities.CardProbability(player3, j) > 0) &&
                                                                            Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                .Select(h => new Card(i.Suit, h))
                                                                                .Where(j => j.BadValue < i.BadValue)
                                                                                .Any(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                          _probabilities.CardProbability(player3, j) > 0))
                                                                .Select(i => new Tuple<Card, int>(i,
                                                                    Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Select(h => new Card(i.Suit, h))
                                                                        .Where(j => j.BadValue < i.BadValue)
                                                                        .Count(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                    _probabilities.CardProbability(player3, j) > 0)))
                                                                .ToList();
                        var hiCardsPerSuit = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                                 .ToDictionary(k => k, v => hiCards.Count(i => i.Item1.Suit == v));
                        var cardsToPlay = hiCards.Where(i => hiCardsPerSuit[i.Item1.Suit] == 1)
                                                 .OrderByDescending(i => i.Item2)
                                                 .ThenBy(i => hands[MyIndex].CardCount(i.Item1.Suit))
                                                 .ThenByDescending(i => i.Item1.BadValue)
                                                 .Select(i => i.Item1);

                        if(!cardsToPlay.Any())
                        {
                            cardsToPlay = hiCards.OrderByDescending(i => i.Item2)
                                                 .ThenByDescending(i => i.Item1.BadValue)
                                                 .Select(i => i.Item1);
                        }

                        return cardsToPlay.FirstOrDefault();
                    }
                    #endregion
                };
                yield return new AiRule()
                {
                    Order = 4,
                    Description = "Hrát barvu s dírou",
                    SkipSimulations = true,
                    #region GetRules1 Rule4
                    ChooseCard1 = () =>
                    {
                        var cardsToPlay = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                        .Select(h => new Card(i.Suit, h))
                                                                        .Where(j => j.BadValue > i.BadValue)
                                                                        .Any(j => _probabilities.CardProbability(player2, j) > 0 ||
                                                                                  _probabilities.CardProbability(player3, j) > 0));

                        return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                    }
                    #endregion
                };
            }
            else
            {
                yield return new AiRule()
                {
                    Order = 5,
                    Description = "Nechat spoluhráče odmazat",
                    SkipSimulations = true,
                    #region GetRules1 Rule5
                    ChooseCard1 = () =>
                    {
                        var cardsToPlay = hands[MyIndex].Where(i => _probabilities.PotentialCards(opponent).CardCount(i.Suit) >= 1 &&
                                                                    _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) == 0);

                        if (cardsToPlay.Any(i => _probabilities.PotentialCards(opponent).Any(j => j.Suit == i.Suit && j.BadValue > i.BadValue)))
                        {
                            //pokud akter muze mit vyssi kartu v barve, tak pravidlo nehraj. Soupere chytime v pozdejsim pravidle
                            return null;
                        }

                        if (!cardsToPlay.Any())
                        {
                            var prefSuits = new List<Barva>();

                            for (var i = 1; i < RoundNumber - 1; i++)
                            {
                                //pokud v nejakem kole vsichni priznali barvu ale spoluhrac nesel vejs (a bylo kam jit vejs)
                                //a zaroven souper muze mit vyssi kartu v barve nez mam ja sam
                                if (_rounds[i] != null)
                                {
                                    if (_rounds[i].player2.PlayerIndex == TeamMateIndex &&
                                        _rounds[i].c1.Suit == _rounds[i].c2.Suit &&
                                        _rounds[i].c1.Suit == _rounds[i].c3.Suit &&
                                        _rounds[i].c1.BadValue > _rounds[i].c2.BadValue &&
                                        _rounds[i].c1.Value != Hodnota.Eso &&
                                        hands[MyIndex].Any(j => j.Suit == _rounds[i].c2.Suit &&
                                                                _probabilities.PotentialCards(opponent).CardCount(j.Suit) > 1))
                                    {
                                        if (_probabilities.PotentialCards(opponent).Where(j => j.Suit == _rounds[i].c2.Suit)
                                                                                   .All(j => j.BadValue > _rounds[i].c2.BadValue) &&
                                            hands[MyIndex].Where(j => j.Suit == _rounds[i].c2.Suit)
                                                          .Any(j => _probabilities.PotentialCards(opponent).Any(k => k.Suit == j.Suit &&
                                                                                                                     k.BadValue > j.BadValue)))
                                        {
                                            //pokud kolega odmazal nejnizsi kartu tak uz asi zadne dalsi nema a muzu zkusit soupere chytit
                                            return null;
                                        }
                                        prefSuits.Add(_rounds[i].c2.Suit);
                                    }
                                    else if (_rounds[i].player3.PlayerIndex == TeamMateIndex &&
                                             _rounds[i].c1.Suit == _rounds[i].c2.Suit &&
                                             _rounds[i].c1.Suit == _rounds[i].c3.Suit &&
                                             ((_rounds[i].c1.BadValue > _rounds[i].c3.BadValue &&
                                               _rounds[i].c1.Value != Hodnota.Eso) ||
                                              (_rounds[i].c2.BadValue > _rounds[i].c3.BadValue &&
                                               _rounds[i].c2.Value != Hodnota.Eso)) &&
                                             hands[MyIndex].Any(j => j.Suit == _rounds[i].c3.Suit &&
                                                                     _probabilities.PotentialCards(opponent).CardCount(j.Suit) > 1))
                                    {
                                        if (_probabilities.PotentialCards(opponent).Where(j => j.Suit == _rounds[i].c3.Suit)
                                                                                   .All(j => j.BadValue > _rounds[i].c3.BadValue) &&
                                            hands[MyIndex].Where(j => j.Suit == _rounds[i].c3.Suit)
                                                          .Any(j => _probabilities.PotentialCards(opponent).Any(k => k.Suit == j.Suit &&
                                                                                                                     k.BadValue > j.BadValue)))
                                        {
                                            //pokud kolega odmazal nejnizsi kartu tak uz asi zadne dalsi nema a muzu zkusit soupere chytit
                                            return null;
                                        }
                                        prefSuits.Add(_rounds[i].c3.Suit);
                                    }
                                }
                            }
                            cardsToPlay = hands[MyIndex].Where(i => prefSuits.Contains(i.Suit));

                            if (!cardsToPlay.Any())
                            {
                                //pokud mas na zacatku v barve 3-5 nejvyssich karet a nic jineho, tak je pravdepodobne, ze akter ma zbyvajici nizke
                                //muzes proto nechat spoluhrace odmazavat
                                foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                                {
                                    if (initialTopCards.CardCount(b) >= 3 &&
                                        initialTopCards.CardCount(b) <= 5 &&                                        
                                        myInitialHand.CardCount(b) == initialTopCards.CardCount(b) &&
                                        _probabilities.PotentialCards(opponent).HasSuit(b))
                                    {
                                        prefSuits.Add(b);
                                    }
                                }
                                if (!prefSuits.Any())
                                {
                                    foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
                                    {
                                        if (initialTopCards.CardCount(b) >= 2 &&
                                            initialTopCards.CardCount(b) <= 4 &&
                                            myInitialHand.CardCount(b) == initialTopCards.CardCount(b) + 1 &&
                                            _probabilities.PotentialCards(opponent).HasSuit(b))
                                        {
                                            prefSuits.Add(b);
                                        }
                                    }
                                }
                                cardsToPlay = hands[MyIndex].Where(i => prefSuits.Contains(i.Suit));
                            }
                        }

                        return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                    }
                    #endregion
                };

                yield return new AiRule()
                {
                    Order = 6,
                    Description = "Hrát nejnižší od esa",
                    SkipSimulations = true,
                    #region GetRules1 Rule6
                    ChooseCard1 = () =>
                    {
                        if (RoundNumber == 2 && preferredSuits.Any())
                        {
                            //ve 2. pokud muzu soupere chytit v nasl. pravidle, tak toto pravidlo nehraj
                            return null;
                        }
                        if(Enum.GetValues(typeof(Barva)).Cast<Barva>()
                               .Any(b => hands[MyIndex].CardCount(b) == 1 &&
                                         hands[MyIndex].Any(i => i.Suit == b &&
                                                                 _probabilities.PotentialCards(opponent).Any(j => j.Suit == b &&
                                                                                                                  j.BadValue > i.BadValue))))
                        {
                            //pokud se muzes zbavit plonka, tak to udelej a toto pravidlo nehraj
                            return null;
                        }
                        //pokud mam A, K, S, X tak hraj X (souper muze mit spodka)
                        var topCards = hands[MyIndex].Where(i => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                     .Select(h => new Card(i.Suit, h))
                                                                     .Where(j => j.BadValue > i.BadValue)
                                                                     .All(j => _probabilities.CardProbability(player2, j) == 0 &&
                                                                               _probabilities.CardProbability(player3, j) == 0 &&
                                                                               _probabilities.SuitProbability(opponent, i.Suit, RoundNumber) > 0))
                                                     .Distinct()
                                                     .ToList();
                        var cardsToPlay = hands[MyIndex].Where(i => !bannedSuits.Contains(i.Suit) &&
                                                                    topCards.Count(j => j.Suit == i.Suit) > 2 &&
                                                                    !topCards.Contains(i));
                        var prefCards = cardsToPlay.Where(i => preferredSuits.Contains(i.Suit));

                        if (prefCards.Any())
                        {
                            return prefCards.OrderBy(i => i.BadValue).First();
                        }

                        return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                    }
                    #endregion
                };

                yield return new AiRule()
                {
                    Order = 7,
                    Description = "Chytit soupeře",
                    SkipSimulations = true,
                    #region GetRules1 Rule7
                    ChooseCard1 = () =>
                    {
                        var cardsToPlay = Enumerable.Empty<Card>();

                        if (RoundNumber == 2 &&
                            preferredSuits.Any() &&
                            !(_rounds != null && _rounds[0] != null &&               //pokud si kolega v 1. kole odmazaval a
                              ((_rounds[0].player2.PlayerIndex == TeamMateIndex &&
                                _rounds[0].c2.Suit != _rounds[0].c1.Suit) ||
                               (_rounds[0].player3.PlayerIndex == TeamMateIndex &&
                                _rounds[0].c3.Suit != _rounds[0].c1.Suit)) &&
                              _probabilities.PotentialCards(TeamMateIndex)
                                            .CardCount(preferredSuits.First()) >= 4)) //pokud je ve hre jeste hodne karet, muze kolega stale mit dalsi vysoke
                        {
                            //v 2.kole zkus rovnou zahrat preferovanou barvu. Pokud takova je, tak je to vitezna barva
                            cardsToPlay = hands[MyIndex].Where(i => i.Suit == preferredSuits.First() &&
                                                                    _probabilities.SuitProbability(opponent, i.Suit, RoundNumber) > 0 &&
                                                                    (_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) == 0 ||
                                                                     (_probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) < 1 - RiskFactor &&
                                                                      _probabilities.PotentialCards(opponent).Any(j => j.Suit == i.Suit &&
                                                                                                                       j.BadValue > i.BadValue)) ||
                                                                     _probabilities.PotentialCards(opponent).Count(j => j.Suit == i.Suit &&
                                                                                                                        j.BadValue > i.BadValue) > 2));

                            if (cardsToPlay.Any())
                            {
                                return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                            }
                        }
                        else if (RoundNumber > 2 &&
                            preferredSuits.Any() &&
                            !cardsToPlay.Any())
                        {
                            cardsToPlay = hands[MyIndex].Where(i => i.Suit == preferredSuits.First() &&
                                                                    _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) < 1 - RiskFactor &&
                                                                    _probabilities.PotentialCards(opponent).Any(j => j.Suit == i.Suit &&
                                                                                                                     j.BadValue > i.BadValue));
                        }
                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = hands[MyIndex].Where(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) == 0 &&
                                                                _probabilities.PotentialCards(opponent).Any(j => j.Suit == i.Suit &&
                                                                                                                 j.BadValue > i.BadValue));
                        }
                        if (cardsToPlay.Any())
                        {
                            return cardsToPlay.OrderBy(i => i.BadValue).First();
                        }

                        if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                               .Any(b => hands[MyIndex].CardCount(b) == 1 &&
                                         hands[MyIndex].Any(i => i.Suit == b &&
                                                                 _probabilities.PotentialCards(opponent).Any(j => j.Suit == b &&
                                                                                                                  j.BadValue > i.BadValue))))
                        {
                            //pokud se muzes zbavit plonka, tak to udelej a toto pravidlo nehraj
                            return null;
                        }
                        if (hands[MyIndex].Any(i => _probabilities.PotentialCards(opponent).CardCount(i.Suit) > 2 &&
                                                    _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber) == 0))
                        {
                            //pokud muzes nechat kolegu odmazavat (akter barvu jiste ma), tak pravidlo nehraj
                            return null;
                        }
                        if (TeamMateIndex == player2)//co-
                        {
                            cardsToPlay = hands[MyIndex].Where(i =>
                                                           !bannedSuits.Contains(i.Suit) &&
                                                           Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                               .Select(h => new Card(i.Suit, h))
                                                               .Where(j => j.BadValue > i.BadValue)
                                                               .Any(j => _probabilities.CardProbability(player3, j) > 0 &&
                                                                         Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                             .Select(h => new Card(i.Suit, h))
                                                                             .Where(k => k.BadValue < j.BadValue)
                                                                             .Any(k => _probabilities.CardProbability(player2, k) > 0)));
                            if (!cardsToPlay.Any())
                            {
                                cardsToPlay = hands[MyIndex].Where(i =>
                                                               !bannedSuits.Contains(i.Suit) &&
                                                               Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                   .Select(h => new Card(i.Suit, h))
                                                                   .Where(j => j.BadValue > i.BadValue)
                                                                   .Any(j => _probabilities.CardProbability(player3, j) > 0 &&
                                                                             preferredSuits.Contains(i.Suit)));
                            }
                        }
                        else if (TeamMateIndex == player3)//c-o
                        {
                            cardsToPlay = hands[MyIndex].Where(i =>
                                   !bannedSuits.Contains(i.Suit) &&
                                   Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                       .Select(h => new Card(i.Suit, h))
                                       .Where(j => j.BadValue > i.BadValue)
                                       .Any(j => _probabilities.CardProbability(player2, j) > 0 &&
                                                 Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                     .Select(h => new Card(i.Suit, h))
                                                     .Where(k => k.BadValue >= j.BadValue)
                                                     .All(k => _probabilities.CardProbability(player3, k) == 0)));
                            if (!cardsToPlay.Any())
                            {
                                cardsToPlay = hands[MyIndex].Where(i =>
                                                                !bannedSuits.Contains(i.Suit) &&
                                                                Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                    .Select(h => new Card(i.Suit, h))
                                                                    .Where(j => j.BadValue > i.BadValue)
                                                                    .Any(j => _probabilities.CardProbability(player2, j) > 0 &&
                                                                              preferredSuits.Contains(i.Suit)));
                            }
                        }
                        var prefCards = cardsToPlay.Where(i => preferredSuits.Contains(i.Suit));

                        if (prefCards.Any())
                        {
                            return prefCards.OrderBy(i => i.BadValue).First();
                        }

                        return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                    }
                    #endregion
                };

                yield return new AiRule()
                {
                    Order = 8,
                    Description = "Hrát nízkou kartu (nieder)",
                    SkipSimulations = true,
                    #region GetRules1 Rule8
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
                        var prefCards = loCards.Where(i => !bannedSuits.Contains(i.Item1.Suit) &&
                                                           preferredSuits.Contains(i.Item1.Suit));

                        if (prefCards.Any())
                        {
                            var prefSuit = prefCards.OrderByDescending(i => i.Item2)
                                                    .ThenBy(i => i.Item1.BadValue)
                                                    .Select(i => i.Item1.Suit)
                                                    .First();

                            cardsToPlay = loCards.Where(i => i.Item1.Suit == prefSuit)
                                                 .Select(i => i.Item1);
                        }
                        if (!cardsToPlay.Any())
                        {
                            //pokud mam hodne vysokych karet (napr. jako na durcha), tak hrat rovnou nizkou
                            if (hiCards.Count() > 6 && loCards.Any())
                            {
                                //ber jen barvy kde nemam vysokou kartu
                                cardsToPlay = loCards.OrderByDescending(i => i.Item2)
                                                        .ThenBy(i => i.Item1.BadValue)
                                                        .Select(i => i.Item1)
                                                        .Take(1);
                            }
                            else if (RoundNumber > 2) //nizke karty nehraj zbytecne moc brzo
                            {
                                //hraj jen barvy kde nemam vysokou kartu (nejdriv hoch a az pak nieder)
                                //uprednostnuj barvu, ve ktere uz jsme hrali hoch
                                loCards = hands[MyIndex].Where(i => !bannedSuits.Contains(i.Suit) &&
                                                                    i.BadValue < spodek.BadValue &&
                                                                    (!hiCards.Any(j => j.Suit == i.Suit) ||
                                                                     hochCards.Any(j => j.Suit == i.Suit)))
                                                        .Select(i => new Tuple<Card, int>(i,
                                                                        Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                            .Select(h => new Card(i.Suit, h))
                                                                            .Where(j => j.BadValue > i.BadValue)
                                                                            .Count(j => _probabilities.CardProbability(opponent, j) > 0)))
                                                        .Where(i => i.Item2 > 0);

                                cardsToPlay = loCards.Where(i => preferredSuits.Contains(i.Item1.Suit))
                                                     .Select(i => i.Item1)
                                                     .OrderBy(i => i.BadValue)
                                                     .Take(1);
                                if (!cardsToPlay.Any())
                                {
                                    //if (opponentsRoundOneSuit.HasValue &&
                                    //    loCards.Any(i => i.Item1.Suit != opponentsRoundOneSuit.Value))
                                    //{
                                    //    loCards = loCards.Where(i => i.Item1.Suit != opponentsRoundOneSuit.Value);
                                    //}

                                    cardsToPlay = loCards.OrderBy(i => hochCards.Any(j => j.Suit == i.Item1.Suit)
                                                                        ? 0
                                                                        : 1)
                                                         .ThenByDescending(i => i.Item2)
                                                         .ThenBy(i => i.Item1.BadValue)
                                                         .ThenBy(i => _probabilities.SuitProbability(opponent, i.Item1.Suit, RoundNumber))
                                                         .Select(i => i.Item1)
                                                         .Take(1);
                                }
                            }
                        }

                        return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                    }
                    #endregion
                };

                yield return new AiRule()
                {
                    Order = 9,
                    Description = "Odmazat si plonkovou kartu",
                    SkipSimulations = true,
                    #region GetRules1 Rule9
                    ChooseCard1 = () =>
                    {
                        //pri 2. kole hraj jen devitku a vyssi (zbavovat se plonkove 7 nebo 8 moc brzy neni dobre)
                        //pozdeji hraj jakoukoli plonkovou kartu
                        var cardsToPlay = hands[MyIndex].Where(i => !bannedSuits.Contains(i.Suit) &&
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
                    #endregion
                };

                yield return new AiRule()
                {
                    Order = 10,
                    Description = "Hrát vysokou kartu (hoch)",
                    SkipSimulations = true,
                    #region GetRules1 Rule10
                    ChooseCard1 = () =>
                    {
                        //nejprv zkus hrat barvu, kterou akter jiste ma, ale kolega ne, aby si mohl odmazat
                        var cardsToPlay = Enumerable.Empty<Card>();

                        if (cardsToPlay.Any())
                        {
                            return cardsToPlay.OrderByDescending(i => i.BadValue).First();
                        }
                        //napr. A a 8
                        //vysoka bude A nebo K, musim k ni mit nizkou a co nejvetsi diru mezi nima
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
                                                    .Where(i => i.Item2 > 2);   //pokud je der 2 a min, tak hraj rovnou nizkou kartu
                        //Item1 je nizka karta a Item2 je pocet der mezi ni a vysokou kartou
                        //nehrat pokud mam prilis mnoho vysokych karet
                        if (hiCards.Count() <= 6 && loCards.Any())
                        {
                            var prefCards = loCards.Where(i => !bannedSuits.Contains(i.Item1.Suit) &&
                                                               preferredSuits.Contains(i.Item1.Suit));

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
                    #endregion
                };
            }

            yield return new AiRule()
            {
                Order = 11,
                Description = "Zkusit soupeře chytit",
                #region GetRules1 Rule11
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex != -1)
                    {
                        var cardsToPlay = hands[MyIndex].Where(i =>
                                                       !bannedSuits.Contains(i.Suit) &&
                                                       _probabilities.SuitHigherThanCardProbability(opponent, i, RoundNumber, false) > 0);

                        return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault(); //nejmensi karta
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 12,
                Description = "Dostat spoluhráče do štychu",
                #region GetRules1 Rule12
                ChooseCard1 = () =>
                {
                    if (TeamMateIndex != -1)
                    {
                        var cardsToPlay = hands[MyIndex].Where(i =>
                                                       !bannedSuits.Contains(i.Suit)  &&
                                                       _probabilities.SuitHigherThanCardProbability(TeamMateIndex, i, RoundNumber, false) > 0);   //seskup podle barev

                        if (!cardsToPlay.Any())
                        {
                            cardsToPlay = hands[MyIndex].Where(i => _probabilities.SuitHigherThanCardProbability(TeamMateIndex, i, RoundNumber, false) > 0);   //seskup podle barev
                        }

                        return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault(); //nejmensi karta
                    }
                    return null;
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 13,
                Description = "Hrát krátkou barvu",
                #region GetRules1 Rule13
                ChooseCard1 = () =>
                {
                    var cardsToPlay = hands[MyIndex].Where(i => i != null);

                    if (TeamMateIndex != -1)
                    {
                        return cardsToPlay.OrderByDescending(i => _probabilities.SuitProbability(opponent, i.Suit, RoundNumber))
                                          .ThenBy(i => _probabilities.SuitProbability(TeamMateIndex, i.Suit, RoundNumber))
                                          .ThenBy(i => hands[MyIndex].CardCount(i.Suit))
                                          .ThenBy(i => i.BadValue).FirstOrDefault();
                    }
                    else
                    {
                        return cardsToPlay.OrderBy(i => hands[MyIndex].CardCount(i.Suit))
                                          .ThenBy(i => i.BadValue).FirstOrDefault();
                    }
                }
                #endregion
            };
        }

        protected override IEnumerable<AiRule> GetRules2(Hand[] hands)
        {
            #region InitVariables
            var player3 = (MyIndex + 1) % Game.NumPlayers;
            var player1 = (MyIndex + 2) % Game.NumPlayers;
            var opponent = TeamMateIndex == (MyIndex + 1) % Game.NumPlayers
                            ? (MyIndex + 2) % Game.NumPlayers 
                            : (MyIndex + 1) % Game.NumPlayers;

            var preferredSuits = new List<Barva>();
            //var hochCards = new List<Card>();
            var holesByCard = ((List<Card>)hands[MyIndex]).ToDictionary(k => k, v => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                                         .Select(h => new Card(v.Suit, h))
                                                                                         .Count(i => i.BadValue < v.BadValue &&
                                                                                                     _probabilities.PotentialCards(player3)
                                                                                                                   .Any(j => j.Suit == i.Suit &&
                                                                                                                             j.BadValue == i.BadValue)));
            var topCards = hands[MyIndex].Where(i => !Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                          .Select(h => new Card(i.Suit, h))
                                                          .Any(j => j.BadValue > i.BadValue &&
                                                                    _probabilities.PotentialCards(player3)
                                                                                  .Any(k => k.Suit == j.Suit &&
                                                                                            k.BadValue == j.BadValue)))
                                         .ToList();

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
                    //primarne zkus hrat barvu kterou akter odmazaval
                    for (var i = 0; i < RoundNumber - 1; i++)
                    {
                        if (_rounds[i].player2.TeamMateIndex == -1 &&
                            _rounds[i].c2.Suit != _rounds[i].c1.Suit &&
                                 hands[MyIndex].Any(j => j.Suit == _rounds[i].c2.Suit &&
                                                         j.BadValue > _rounds[i].c2.BadValue))
                        {
                            preferredSuits.Add(_rounds[i].c2.Suit);
                        }
                        else if (_rounds[i].player3.TeamMateIndex == -1 &&
                                 _rounds[i].c3.Suit != _rounds[i].c1.Suit &&
                                 hands[MyIndex].Any(j => j.Suit == _rounds[i].c3.Suit &&
                                                         j.BadValue > _rounds[i].c3.BadValue))
                        {
                            preferredSuits.Add(_rounds[i].c3.Suit);
                        }
                    }
                }
                if (!preferredSuits.Any())
                {
                    //nasledne zkus hrat barvu kterou akter hral a nebyla nejnizsi
                    for (var i = 0; i < RoundNumber - 1; i++)
                    {
                        if (_rounds[i].c1.Suit == _rounds[i].c2.Suit &&
                            _rounds[i].c1.Suit == _rounds[i].c3.Suit)
                        {
                            if (_rounds[i].player2.TeamMateIndex == -1 &&
                                (_rounds[i].c2.BadValue > _rounds[i].c1.BadValue ||
                                 _rounds[i].c2.BadValue > _rounds[i].c3.BadValue) &&
                                hands[MyIndex].Any(j => j.Suit == _rounds[i].c2.Suit &&
                                                        j.BadValue > _rounds[i].c2.BadValue))
                            {
                                preferredSuits.Add(_rounds[i].c2.Suit);
                            }
                            else if (_rounds[i].player3.TeamMateIndex == -1 &&
                                (_rounds[i].c3.BadValue > _rounds[i].c1.BadValue ||
                                 _rounds[i].c3.BadValue > _rounds[i].c2.BadValue) &&
                                hands[MyIndex].Any(j => j.Suit == _rounds[i].c3.Suit &&
                                                        j.BadValue > _rounds[i].c3.BadValue))
                            {
                                preferredSuits.Add(_rounds[i].c3.Suit);
                            }
                        }
                    }
                }
                //if (!preferredSuits.Any())
                {
                    //nakonec zkus hrat barvu kterou spoluhrac odmazaval
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
                //for (var i = 1; i < RoundNumber - 1; i++)
                //{
                //    //pokud v nejakem kole vsichni priznali barvu ale spoluhrac nesel vejs (a bylo kam jit vejs)
                //    //a zaroven souper muze mit vyssi kartu v barve nez mam ja sam
                //    if (_rounds[i] != null)
                //    {
                //        if (_rounds[i].player2.PlayerIndex == TeamMateIndex &&
                //            _rounds[i].c1.Suit == _rounds[i].c2.Suit &&
                //            _rounds[i].c1.Suit == _rounds[i].c3.Suit &&
                //            _rounds[i].c1.BadValue > _rounds[i].c2.BadValue &&
                //            hands[MyIndex].Any(j => j.Suit == _rounds[i].c2.Suit &&
                //                                          Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                //                                              .Select(h => new Card(_rounds[i].c2.Suit, h))
                //                                              .Where(k => k.BadValue > j.BadValue)
                //                                              .Any(k => _probabilities.CardProbability(opponent, k) > 0)))
                //        {
                //            preferredSuits.Add(_rounds[i].c2.Suit);
                //        }
                //        else if (_rounds[i].player3.PlayerIndex == TeamMateIndex &&
                //                 _rounds[i].c1.Suit == _rounds[i].c2.Suit &&
                //                 _rounds[i].c1.Suit == _rounds[i].c3.Suit &&
                //                 (_rounds[i].c1.BadValue > _rounds[i].c3.BadValue ||
                //                  (_rounds[i].c2.BadValue > _rounds[i].c3.BadValue &&
                //                   _rounds[i].c2.Value != Hodnota.Eso)) &&
                //                 hands[MyIndex].Any(j => j.Suit == _rounds[i].c3.Suit &&
                //                                          Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                //                                              .Select(h => new Card(_rounds[0].c3.Suit, h))
                //                                              .Where(k => k.BadValue > j.BadValue)
                //                                              .Any(k => _probabilities.CardProbability(opponent, k) > 0)))
                //        {
                //            preferredSuits.Add(_rounds[i].c3.Suit);
                //        }
                //    }
                //}
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
            #endregion

            yield return new AiRule()
            {
                Order = 0,
                Description = "Dostat se do štychu (hoch)",
                SkipSimulations = true,
                #region GetRules2 Rule0
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();

                    if (TeamMateIndex == player3) //Tohle plati jen v 1. kole
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
                    }
                    return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 1,
                Description = "Hrát nízkou kartu (nieder)",
                SkipSimulations = true,
                #region GetRules2 Rule1
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = Enumerable.Empty<Card>();
                    var svrsek = new Card(Barva.Cerveny, Hodnota.Svrsek);

                    if (TeamMateIndex != -1 && RoundNumber > 1)
                    {
                        cardsToPlay = ValidCards(c1, hands[MyIndex]).Where(i => i.Suit == c1.Suit &&
                                                                                i.BadValue > c1.BadValue);
                    }
                    return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "Odmazat si vysokou kartu",
                SkipSimulations = true,
                #region GetRules2 Rule2
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

                            return cardsToPlay.OrderBy(i => hands[MyIndex].CardCount(i.Suit))
                                              .ThenByDescending(i => i.BadValue).FirstOrDefault();
                        }
                    }
                    else //oc-
                    {
                        var hiCards = ValidCards(c1, hands[MyIndex]).Select(i => new Tuple<Card, int>(i,
                                                                        Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                                            .Select(h => new Card(i.Suit, h))
                                                                            .Where(j => j.BadValue < i.BadValue)
                                                                            .Count(j => _probabilities.CardProbability(opponent, j) > 0)))
                                                                    .Where(i => i.Item2 > 0)
                                                                    .ToList();
                        var prefCards = hiCards.Where(i => preferredSuits.Any(j => j == i.Item1.Suit))
                                                .Select(i => i.Item1);

                        if (prefCards.Any())
                        {
                            cardsToPlay = prefCards.OrderByDescending(i => i.BadValue);
                        }
                        else
                        {
                            var hiCardsPerSuit = hiCards.Where(i => hands[MyIndex].Where(j => j.Suit == i.Item1.Suit)
                                                                                  .All(j => j.BadValue <= i.Item1.BadValue))
                                                        .ToDictionary(k => k.Item1.Suit, v => v);
                            var topCards = hiCards.Where(i => i.Item2 == hiCardsPerSuit[i.Item1.Suit].Item2)
                                                  .Select(i => i.Item1)
                                                  .ToList();
                            //odmazavej samotne vysoke karty nebo dvojice
                            if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                    .Any(b => topCards.CardCount(b) > 0 &&
                                              topCards.CardCount(b) <= 2 &&
                                              topCards.Where(i => i.Suit == b)
                                                      .Any(i => i.BadValue > Card.GetBadValue(Hodnota.Devitka))))
                            {
                                hiCards = hiCards.Where(i => (hands[MyIndex].CardCount(i.Item1.Suit) <= 3 ||
                                                              (topCards.CardCount(i.Item1.Suit) > 0 &&
                                                               topCards.CardCount(i.Item1.Suit) <= 2)) &&
                                                             topCards.Where(j => j.Suit == i.Item1.Suit)
                                                                     .Any(j => j.BadValue > Card.GetBadValue(Hodnota.Devitka)))
                                                 .ToList();
                            }
                            //setrid pote podle poctu der a nakonec podle velikosti
                            cardsToPlay = hiCards//.Where(i => i.Item2 > 0 &&
                                                 //            (i.Item2 <= 3 ||
                                                 //             hands[MyIndex].CardCount(i.Item1.Suit) <= 3))
                                                 .OrderByDescending(i => i.Item2)
                                                 .ThenByDescending(i => i.Item1.BadValue)
                                                 .Select(i => i.Item1);
                            if (!cardsToPlay.Any())
                            {
                                cardsToPlay = hiCards.OrderByDescending(i => i.Item2)
                                                     .ThenByDescending(i => i.Item1.BadValue)
                                                     .Select(i => i.Item1);
                            }
                        }
                        //pokud muzes, tak hraj stejnou barvu jako v minulem kole
                        if (RoundNumber > 1 &&
                            _rounds[RoundNumber - 2] != null)
                        {
                            if (_rounds[RoundNumber - 2].player2.PlayerIndex == MyIndex &&
                                _rounds[RoundNumber - 2].c2.Suit != _rounds[RoundNumber - 2].c1.Suit &&
                                cardsToPlay.Any(i => i.BadValue > Card.GetBadValue(Hodnota.Devitka) &&
                                                     i.Suit == _rounds[RoundNumber - 2].c2.Suit))
                            {
                                cardsToPlay = cardsToPlay.Where(i => i.Suit == _rounds[RoundNumber - 2].c2.Suit);
                            }
                            else if (_rounds[RoundNumber - 2].player3.PlayerIndex == MyIndex &&
                                     _rounds[RoundNumber - 2].c3.Suit != _rounds[RoundNumber - 2].c1.Suit &&
                                     cardsToPlay.Any(i => i.BadValue > Card.GetBadValue(Hodnota.Devitka) &&
                                                          i.Suit == _rounds[RoundNumber - 2].c3.Suit))
                            {
                                cardsToPlay = cardsToPlay.Where(i => i.Suit == _rounds[RoundNumber - 2].c3.Suit);
                            }
                        }
                    }

                    return cardsToPlay.FirstOrDefault();
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 3,
                Description = "Hrát cokoli",
                SkipSimulations = true,
                #region GetRules2 Rule3
                ChooseCard2 = (Card c1) =>
                {
                    var cardsToPlay = ValidCards(c1, hands[MyIndex]);

                    if (TeamMateIndex != player3 &&     //pokud na treti pozici hraje souper tak se snaz hrat pod jeho karty
                        cardsToPlay.Any(i => i.Suit == c1.Suit &&
                                             i.BadValue > c1.BadValue &&
                                             !topCards.Contains(i)))
                    {
                        return cardsToPlay.OrderBy(i => holesByCard[i])
                                          .ThenByDescending(i => i.BadValue)
                                          .FirstOrDefault();
                    }
                    else if (cardsToPlay.Any(i => i.BadValue > Card.GetBadValue(Hodnota.Devitka)))
                    {
                        cardsToPlay = cardsToPlay.Where(i => i.BadValue > Card.GetBadValue(Hodnota.Devitka)).ToList();

                        return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                    }
                    else
                    {
                        return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                    }
                }
                #endregion
            };
        }

        protected override IEnumerable<AiRule> GetRules3(Hand[] hands)
        {
            #region InitVariables
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
                    //primarne zkus hrat barvu kterou akter odmazaval
                    for (var i = 0; i < RoundNumber - 1; i++)
                    {
                        if (_rounds[i].player2.TeamMateIndex == -1 &&
                            _rounds[i].c2.Suit != _rounds[i].c1.Suit &&
                                 hands[MyIndex].Any(j => j.Suit == _rounds[i].c2.Suit &&
                                                         j.BadValue > _rounds[i].c2.BadValue))
                        {
                            preferredSuits.Add(_rounds[i].c2.Suit);
                        }
                        else if (_rounds[i].player3.TeamMateIndex == -1 &&
                                 _rounds[i].c3.Suit != _rounds[i].c1.Suit &&
                                 hands[MyIndex].Any(j => j.Suit == _rounds[i].c3.Suit &&
                                                         j.BadValue > _rounds[i].c3.BadValue))
                        {
                            preferredSuits.Add(_rounds[i].c3.Suit);
                        }
                    }
                }
                if (!preferredSuits.Any())
                {
                    //nasledne zkus hrat barvu kterou akter hral a nebyla nejnizsi
                    for (var i = 0; i < RoundNumber - 1; i++)
                    {
                        if (_rounds[i].c1.Suit == _rounds[i].c2.Suit &&
                            _rounds[i].c1.Suit == _rounds[i].c3.Suit)
                        {
                            if (_rounds[i].player2.TeamMateIndex == -1 &&
                                (_rounds[i].c2.BadValue > _rounds[i].c1.BadValue ||
                                 _rounds[i].c2.BadValue > _rounds[i].c3.BadValue) &&
                                hands[MyIndex].Any(j => j.Suit == _rounds[i].c2.Suit &&
                                                        j.BadValue > _rounds[i].c2.BadValue))
                            {
                                preferredSuits.Add(_rounds[i].c2.Suit);
                            }
                            else if (_rounds[i].player3.TeamMateIndex == -1 &&
                                (_rounds[i].c3.BadValue > _rounds[i].c1.BadValue ||
                                 _rounds[i].c3.BadValue > _rounds[i].c2.BadValue) &&
                                hands[MyIndex].Any(j => j.Suit == _rounds[i].c3.Suit &&
                                                        j.BadValue > _rounds[i].c3.BadValue))
                            {
                                preferredSuits.Add(_rounds[i].c3.Suit);
                            }
                        }
                    }
                }
                //if (!preferredSuits.Any())
                {
                    //nakonec zkousej hrat barvu kterou spoluhrac odmazaval
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
                //for (var i = 1; i < RoundNumber - 1; i++)
                //{
                //    //pokud v nejakem kole vsichni priznali barvu ale spoluhrac nesel vejs (a bylo kam jit vejs)
                //    //a zaroven souper muze mit vyssi kartu v barve nez mam ja sam
                //    if (_rounds[i] != null)
                //    {
                //        if (_rounds[i].player2.PlayerIndex == TeamMateIndex &&
                //            _rounds[i].c1.Suit == _rounds[i].c2.Suit &&
                //            _rounds[i].c1.Suit == _rounds[i].c3.Suit &&
                //            _rounds[i].c1.BadValue > _rounds[i].c2.BadValue &&
                //            hands[MyIndex].Any(j => j.Suit == _rounds[i].c2.Suit &&
                //                                          Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                //                                              .Select(h => new Card(_rounds[i].c2.Suit, h))
                //                                              .Where(k => k.BadValue > j.BadValue)
                //                                              .Any(k => _probabilities.CardProbability(opponent, k) > 0)))
                //        {
                //            preferredSuits.Add(_rounds[i].c2.Suit);
                //        }
                //        else if (_rounds[i].player3.PlayerIndex == TeamMateIndex &&
                //                 _rounds[i].c1.Suit == _rounds[i].c2.Suit &&
                //                 _rounds[i].c1.Suit == _rounds[i].c3.Suit &&
                //                 (_rounds[i].c1.BadValue > _rounds[i].c3.BadValue ||
                //                  (_rounds[i].c2.BadValue > _rounds[i].c3.BadValue &&
                //                   _rounds[i].c2.Value != Hodnota.Eso)) &&
                //                 hands[MyIndex].Any(j => j.Suit == _rounds[i].c3.Suit &&
                //                                          Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                //                                              .Select(h => new Card(_rounds[0].c3.Suit, h))
                //                                              .Where(k => k.BadValue > j.BadValue)
                //                                              .Any(k => _probabilities.CardProbability(opponent, k) > 0)))
                //        {
                //            preferredSuits.Add(_rounds[i].c3.Suit);
                //        }
                //    }
                //}
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
            #endregion

            yield return new AiRule()
            {
                Order = 0,
                Description = "Hrát vítěznou kartu",
                SkipSimulations = true,
                #region GetRules3 Rule0
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
                #endregion
            };

            yield return new AiRule()
            {
                Order = 1,
                Description = "Odmazat si vysokou kartu",
                SkipSimulations = true,
                #region GetRules3 Rule1
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
                                                                        .Where(i => i.Item2 > 0)
                                                                        .ToList();
                        var prefCards = hiCards.Where(i => preferredSuits.Any(j => j == i.Item1.Suit))
                                               .Select(i => i.Item1);
                        
                        if (prefCards.Any())
                        {
                            cardsToPlay = prefCards.OrderByDescending(i => i.BadValue);
                        }
                        else
                        {
                            var hiCardsPerSuit = hiCards.Where(i => hands[MyIndex].Where(j => j.Suit == i.Item1.Suit)
                                                                                  .All(j => j.BadValue <= i.Item1.BadValue))
                                                        .ToDictionary(k => k.Item1.Suit, v => v);
                            var topCards = hiCards.Where(i => i.Item2 == hiCardsPerSuit[i.Item1.Suit].Item2)
                                                  .Select(i => i.Item1)
                                                  .ToList();
                            //odmazavej samotne vysoke karty nebo dvojice
                            if (Enum.GetValues(typeof(Barva)).Cast<Barva>()
                                    .Any(b => topCards.CardCount(b) > 0 &&
                                              topCards.CardCount(b) <= 2 &&
                                              topCards.Where(i => i.Suit == b)
                                                      .Any(i => i.BadValue > Card.GetBadValue(Hodnota.Devitka))))
                            {
                                hiCards = hiCards.Where(i => topCards.CardCount(i.Item1.Suit) > 0 &&
                                                             topCards.CardCount(i.Item1.Suit) <= 2 &&
                                                             topCards.Where(j => j.Suit == i.Item1.Suit)
                                                                     .Any(j => j.BadValue > Card.GetBadValue(Hodnota.Devitka)))
                                                 .ToList();
                            }
                            //setrid pote podle poctu der a nakonec podle velikosti
                            cardsToPlay = hiCards.OrderByDescending(i => i.Item2)
                                                 .ThenByDescending(i => i.Item1.BadValue)
                                                 .Select(i => i.Item1);
                        }
                        //pokud muzes, tak hraj stejnou barvu jako v minulem kole
                        if (RoundNumber > 1 &&
                            _rounds[RoundNumber - 2] != null)
                        {
                            if (_rounds[RoundNumber - 2].player2.PlayerIndex == MyIndex &&
                                _rounds[RoundNumber - 2].c2.Suit != _rounds[RoundNumber - 2].c1.Suit &&
                                cardsToPlay.Any(i => i.Suit == _rounds[RoundNumber - 2].c2.Suit))
                            {
                                cardsToPlay = cardsToPlay.Where(i => i.Suit == _rounds[RoundNumber - 2].c2.Suit);
                            }
                            else if (_rounds[RoundNumber - 2].player3.PlayerIndex == MyIndex &&
                                     _rounds[RoundNumber - 2].c3.Suit != _rounds[RoundNumber - 2].c1.Suit &&
                                     cardsToPlay.Any(i => i.Suit == _rounds[RoundNumber - 2].c3.Suit))
                            {
                                cardsToPlay = cardsToPlay.Where(i => i.Suit == _rounds[RoundNumber - 2].c3.Suit);
                            }
                        }
                    }

                    return cardsToPlay.FirstOrDefault();
                }
                #endregion
            };

            yield return new AiRule()
            {
                Order = 2,
                Description = "Hrát cokoli",
                SkipSimulations = true,
                #region GetRules3 Rule2
                ChooseCard3 = (Card c1, Card c2) =>
                {
                    var cardsToPlay = ValidCards(c1, c2, hands[MyIndex]);

                    if (cardsToPlay.Any(i => i.BadValue > Card.GetBadValue(Hodnota.Devitka)))
                    {
                        cardsToPlay = cardsToPlay.Where(i => i.BadValue > Card.GetBadValue(Hodnota.Devitka)).ToList();

                        return cardsToPlay.OrderBy(i => i.BadValue).FirstOrDefault();
                    }
                    else
                    {
                        return cardsToPlay.OrderByDescending(i => i.BadValue).FirstOrDefault();
                    }
                }
                #endregion
            };
        }
    }
}
