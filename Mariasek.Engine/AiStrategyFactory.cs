using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mariasek.Engine
{
    public static class AiStrategyFactory
    {
        public static AiStrategyBase GetAiStrategy(Game g, Hra? gameType, Barva? trump, Hand[] hands, Round[] rounds, List<Barva> teamMatesSuits, 
                                                   Probability probabilities, string name, int playerIndex, int teamMateIndex, int? initialRoundNumber, 
                                                   float riskFactor, float riskFactorSevenDefense, float solitaryXThreshold, float solitaryXThresholdDefense,
                                                   Bidding bidding, int gameValue, int hundredValue, int sevenValue)
        {
            var gt = gameType.HasValue ? gameType.Value : g.GameType;

            switch(gt)
            {
                case Hra.Durch:
                    return new AiDurchStrategy(trump, gameType.HasValue ? gameType.Value : g.GameType, hands, rounds, teamMatesSuits, probabilities)
                    {
                        MyIndex = playerIndex,
                        MyName = name,
                        TeamMateIndex = teamMateIndex,
                        RoundNumber = initialRoundNumber.HasValue ? initialRoundNumber.Value : g.RoundNumber
                    };
                case Hra.Betl:
                    return new AiBetlStrategy(trump, gameType.HasValue ? gameType.Value : g.GameType, hands, rounds, teamMatesSuits, probabilities)
                    {
                        MyIndex = playerIndex,
                        MyName = name,
                        TeamMateIndex = teamMateIndex,
                        RoundNumber = initialRoundNumber.HasValue ? initialRoundNumber.Value : g.RoundNumber,
                        RiskFactor = riskFactor
                    };
                default:
                    return new AiStrategy(trump, gameType.HasValue ? gameType.Value : g.GameType, hands, rounds, teamMatesSuits, g.HlasConsidered, g.CalculationStyle, probabilities)
                    {
                        MyIndex = playerIndex,
                        MyName = name,
                        TeamMateIndex = teamMateIndex,
                        RoundNumber = initialRoundNumber.HasValue ? initialRoundNumber.Value : g.RoundNumber,
                        RiskFactor = riskFactor,
                        RiskFactorSevenDefense = riskFactorSevenDefense,
                        SolitaryXThreshold = solitaryXThreshold,
                        SolitaryXThresholdDefense = solitaryXThresholdDefense,
                        PlayerBids = bidding.AllPlayerBids,
                        GameValue = ((gameType.HasValue ? gameType.Value : g.GameType) & Hra.Hra) != 0
                                    ? bidding.GameMultiplier * gameValue
                                    : bidding.GameMultiplier * hundredValue,
                        SevenValue = bidding.SevenMultiplier * sevenValue + bidding.SevenAgainstMultiplier * sevenValue
                    };
            }
        }
    }
}
