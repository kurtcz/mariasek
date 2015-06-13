using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
{
    public class AiDurchStrategy : AiStrategyBase
    {
        public AiDurchStrategy(Barva trump, Hra gameType, Hand[] hands)
            :base(trump, gameType, hands)
        {
        }

        protected override IEnumerable<AiRule> GetRules1(Hand[] hands)
        {
            throw new NotImplementedException();
        }

        protected override IEnumerable<AiRule> GetRules2(Hand[] hands)
        {
            throw new NotImplementedException();
        }

        protected override IEnumerable<AiRule> GetRules3(Hand[] hands)
        {
            throw new NotImplementedException();
        }
    }
}
