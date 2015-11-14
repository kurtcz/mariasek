using System;
using System.Collections.Generic;
using System.Linq;
#if !PORTABLE
using System.Reflection;
#endif
using System.Text;
using System.Threading.Tasks;
using Mariasek.Engine.New.Logger;

namespace Mariasek.Engine.New
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
        public int MyIndex { get; set; }
        public string MyName { get; set; }
        public int TeamMateIndex { get; set; }
        public int RoundNumber { get; set; }

        public AiStrategyBase(Barva? trump, Hra gameType, Hand[] hands)
        {
            _trump = trump;
            _hands = hands;
            _gameType = gameType;
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

        public Card PlayCard1(out AiRule selectedRule)
        {
            if (((List<Card>)_hands[MyIndex]).Count() == 1)
            {
                var cardToPlay = ((List<Card>)_hands[MyIndex]).First();

                selectedRule = AiRule.PlayTheOnlyValidCard;
                _hands[MyIndex].Remove(cardToPlay);
                return cardToPlay;
            }

            var t0 = DateTime.Now;
            foreach (var rule in GetRules1(_hands))
            {
                var cardToPlay = rule.ChooseCard1();
                if (cardToPlay != null)
                {
                    if (cardToPlay.Value == Hodnota.Kral)
                    {
                        var svrsek = _hands[MyIndex].FirstOrDefault(i => i.Value == Hodnota.Svrsek && i.Suit == cardToPlay.Suit);
                        cardToPlay = svrsek ?? cardToPlay;
                    }
                    _hands[MyIndex].Remove(cardToPlay);
                    _log.TraceFormat("{0}: {1} - {2}", MyName, cardToPlay, rule.Description);
                    selectedRule = rule;
                    return cardToPlay;
                }
            }
            selectedRule = null;
            return null;
        }

        public Card PlayCard2(Card c1, out AiRule selectedRule)
        {
            var validCards = ValidCards(c1, _hands[MyIndex]);

            if (validCards.Count == 1)
            {
                var cardToPlay = validCards.First();

                selectedRule = AiRule.PlayTheOnlyValidCard;
                _hands[MyIndex].Remove(cardToPlay);
                return cardToPlay;
            }

            foreach (var rule in GetRules2(_hands))
            {
                var t0 = DateTime.Now;
                var cardToPlay = rule.ChooseCard2(c1);
                if (cardToPlay != null)
                {
                    if (cardToPlay.Value == Hodnota.Kral)
                    {
                        var svrsek = _hands[MyIndex].FirstOrDefault(i => i.Value == Hodnota.Svrsek && i.Suit == cardToPlay.Suit);
                        cardToPlay = svrsek ?? cardToPlay;
                    }
                    _hands[MyIndex].Remove(cardToPlay);
                    _log.TraceFormat("{0}: {1} - {2}", MyName, cardToPlay, rule.Description);
                    selectedRule = rule;
                    return cardToPlay;
                }
            }
            selectedRule = null;
            return null;
        }

        public Card PlayCard3(Card c1, Card c2, out AiRule selectedRule)
        {
            var validCards = ValidCards(c1, c2, _hands[MyIndex]);

            if (validCards.Count == 1)
            {
                var cardToPlay = validCards.First();

                selectedRule = AiRule.PlayTheOnlyValidCard;
                _hands[MyIndex].Remove(cardToPlay);
                return cardToPlay;
            }

            foreach (var rule in GetRules3(_hands))
            {
                var t0 = DateTime.Now;
                var cardToPlay = rule.ChooseCard3(c1, c2);
                if (cardToPlay != null)
                {
                    if (cardToPlay.Value == Hodnota.Kral)
                    {
                        var svrsek = _hands[MyIndex].FirstOrDefault(i => i.Value == Hodnota.Svrsek && i.Suit == cardToPlay.Suit);
                        cardToPlay = svrsek ?? cardToPlay;
                    }
                    _hands[MyIndex].Remove(cardToPlay);
                    _log.TraceFormat("{0}: {1} - {2}", MyName, cardToPlay, rule.Description);
                    selectedRule = rule;
                    return cardToPlay;
                }
            }
            selectedRule = null;
            return null;
        }
    }
}
