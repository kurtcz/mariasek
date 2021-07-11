using System;
using System.Collections.Generic;
using System.Linq;
#if !PORTABLE
using System.Reflection;
#endif
using System.Text;
using System.Threading.Tasks;
using Mariasek.Engine.Logger;

namespace Mariasek.Engine
{
    public abstract class AiStrategyBase
    {
#if !PORTABLE
        protected static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
#else
        protected static readonly ILog _log = new DummyLogWrapper();
#endif   
        protected Barva? _trump;
        protected Hra _gameType;
        protected Hand[] _hands;
		protected Round[] _rounds;
        protected Probability _probabilities;
        protected List<Barva> _teamMatesSuits;
        public int MyIndex { get; set; }
        public string MyName { get; set; }
        public int TeamMateIndex { get; set; }
        public int RoundNumber { get; set; }

        public AiStrategyBase(Barva? trump, Hra gameType, Hand[] hands, Round[] rounds, List<Barva> teamMatesSuits, Probability probabilities)
        {
            _trump = trump;
            _hands = hands;
            _gameType = gameType;
            _rounds = rounds;
            _probabilities = probabilities;
            _teamMatesSuits = teamMatesSuits;
        }

        protected abstract IEnumerable<AiRule> GetRules1(Hand[] hands);
        protected abstract IEnumerable<AiRule> GetRules2(Hand[] hands);
        protected abstract IEnumerable<AiRule> GetRules3(Hand[] hands);

        protected List<Card> ValidCards(Hand hand)
        {
            return AbstractPlayer.ValidCards(hand, _trump, _gameType, TeamMateIndex);
        }

        protected List<Card> ValidCards(Card c, Hand hand)
        {
            return AbstractPlayer.ValidCards(hand, _trump, _gameType, TeamMateIndex, c);
        }

        protected List<Card> ValidCards(Card c1, Card c2, Hand hand)
        {
            return AbstractPlayer.ValidCards(hand, _trump, _gameType, TeamMateIndex, c1, c2);
        }

        public Dictionary<AiRule, Card> GetApplicableRules()
        {
            var result = new Dictionary<AiRule, Card>();
            
            if (((List<Card>)_hands[MyIndex]).Count() == 1)
            {
                var cardToPlay = ((List<Card>)_hands[MyIndex]).First();

                _hands[MyIndex].Remove(cardToPlay);
                result.Add(AiRule.PlayTheOnlyValidCard, cardToPlay);

                return result;
            }

            var t0 = DateTime.Now;
            foreach (var rule in GetRules1(_hands))
            {
                var cardToPlay = rule.ChooseCard1();
                if (cardToPlay != null)
                {
                    if (cardToPlay.Value == Hodnota.Kral && _trump.HasValue)
                    {
                        var svrsek = _hands[MyIndex].FirstOrDefault(i => i.Value == Hodnota.Svrsek && i.Suit == cardToPlay.Suit);
                        cardToPlay = svrsek ?? cardToPlay;
                    }
                    if (result.Count() == 0)
                    {
                        //if this is the 1st aplicable rule then this is the card the strategy advises us to play
                        //remove the card so that the simulation can continue
                        _hands[MyIndex].Remove(cardToPlay);
                    }
                    _log.TraceFormat("{0}: {1} - {2}", MyName, cardToPlay, rule.Description);
                    result.Add(rule, cardToPlay);
                    if(!rule.UseThreshold)
                    {
                        break; //chceme jen prvni nebodovane pravidlo
                    }
                }
            }

            return result;
        }

        public Dictionary<AiRule, Card> GetApplicableRules2(Card c1)
        {
            var validCards = ValidCards(c1, _hands[MyIndex]);
            var result = new Dictionary<AiRule, Card>();

            if (validCards.Count == 1)
            {
                var cardToPlay = validCards.First();

                _hands[MyIndex].Remove(cardToPlay);
                result.Add(AiRule.PlayTheOnlyValidCard, cardToPlay);

                return result;
            }

            foreach (var rule in GetRules2(_hands))
            {
                var t0 = DateTime.Now;
                var cardToPlay = rule.ChooseCard2(c1);
                if (cardToPlay != null)
                {
                    if (cardToPlay.Value == Hodnota.Kral && _trump.HasValue)
                    {
                        var svrsek = _hands[MyIndex].FirstOrDefault(i => i.Value == Hodnota.Svrsek && i.Suit == cardToPlay.Suit);
                        cardToPlay = svrsek ?? cardToPlay;
                    }
                    if (result.Count() == 0)
                    {
                        //if this is the 1st aplicable rule then this is the card the strategy advises us to play
                        //remove the card so that the simulation can continue
                        _hands[MyIndex].Remove(cardToPlay);
                    }
                    _log.TraceFormat("{0}: {1} - {2}", MyName, cardToPlay, rule.Description);
                    result.Add(rule, cardToPlay);
                    if (!rule.UseThreshold)
                    {
                        break; //chceme jen prvni nebodovane pravidlo
                    }
                }
            }

            return result;
        }

        public Dictionary<AiRule, Card> GetApplicableRules3(Card c1, Card c2)
        {
            var validCards = ValidCards(c1, c2, _hands[MyIndex]);
            var result = new Dictionary<AiRule, Card>();

            if (validCards.Count == 1)
            {
                var cardToPlay = validCards.First();

                _hands[MyIndex].Remove(cardToPlay);
                result.Add(AiRule.PlayTheOnlyValidCard, cardToPlay);

                return result;
            }

            foreach (var rule in GetRules3(_hands))
            {
                var t0 = DateTime.Now;
                var cardToPlay = rule.ChooseCard3(c1, c2);
                if (cardToPlay != null)
                {
                    if (cardToPlay.Value == Hodnota.Kral && _trump.HasValue)
                    {
                        var svrsek = _hands[MyIndex].FirstOrDefault(i => i.Value == Hodnota.Svrsek && i.Suit == cardToPlay.Suit);
                        cardToPlay = svrsek ?? cardToPlay;
                    }
                    if (result.Count() == 0)
                    {
                        //if this is the 1st aplicable rule then this is the card the strategy advises us to play
                        //remove the card so that the simulation can continue
                        _hands[MyIndex].Remove(cardToPlay);
                    }
                    _log.TraceFormat("{0}: {1} - {2}", MyName, cardToPlay, rule.Description);
                    result.Add(rule, cardToPlay);
                    if (!rule.UseThreshold)
                    {
                        break; //chceme jen prvni nebodovane pravidlo
                    }
                }
            }

            return result;
        }
    }
}
