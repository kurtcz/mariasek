using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine.New
{
    public static class AiStrategyFactory
    {
        public static AiStrategyBase GetAiStrategy(Game g, Hra? gameType, Barva? trump, Hand[] hands, string name, int playerIndex, int teamMateIndex, int? initialRoundNumber)
        {
            var gt = gameType.HasValue ? gameType.Value : g.GameType;

            switch(gt)
            {
                case Hra.Durch:
                    return new AiDurchStrategy(trump, gameType.HasValue ? gameType.Value : g.GameType, hands)
                    {
                        MyIndex = playerIndex,
                        MyName = name,
                        TeamMateIndex = teamMateIndex,
                        RoundNumber = initialRoundNumber.HasValue ? initialRoundNumber.Value : g.RoundNumber
                    };
                case Hra.Betl:
                    return new AiBetlStrategy(trump, gameType.HasValue ? gameType.Value : g.GameType, hands)
                    {
                        MyIndex = playerIndex,
                        MyName = name,
                        TeamMateIndex = teamMateIndex,
                        RoundNumber = initialRoundNumber.HasValue ? initialRoundNumber.Value : g.RoundNumber
                    };
                default:
                    return new AiStrategy(trump, gameType.HasValue ? gameType.Value : g.GameType, hands)
                    {
                        MyIndex = playerIndex,
                        MyName = name,
                        TeamMateIndex = teamMateIndex,
                        RoundNumber = initialRoundNumber.HasValue ? initialRoundNumber.Value : g.RoundNumber
                    };
            }
        }
    }
}
