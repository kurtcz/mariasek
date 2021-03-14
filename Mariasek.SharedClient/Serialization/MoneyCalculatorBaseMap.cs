using CsvHelper.Configuration;
using Mariasek.Engine;

namespace Mariasek.SharedClient.Serialization
{
    public class MoneyCalculatorBaseMap : ClassMap<AddingMoneyCalculator>
    {
        [Preserve]
        public MoneyCalculatorBaseMap()
        {
            Map(m => m.GameId).Index(0);
            Map(m => m.GameTypeString).Index(1);
            Map(m => m.GameTypeConfidence).Index(2);
            Map(m => m.MoneyWon).Index(3, 5);
        }
    }
}
