using System;
using Mariasek.Engine;

namespace Mariasek.SharedClient
{
	public class HistoryItem
	{
		public int GameId { get; set; }
		public string GameTypeString { get; set; }
		public float GameTypeConfidence { get; set; }
		public int MoneyWon1 { get; set; }
        public int MoneyWon2 { get; set; }
        public int MoneyWon3 { get; set; }
		public int[] MoneyWon => new[] { MoneyWon1, MoneyWon2, MoneyWon3 };
        public bool GameIdSpecified => GameId > 0;
		public bool GoodGame => GameTypeString != "Betl" && GameTypeString != "Durch";

        public HistoryItem()
		{
		}

		public HistoryItem(MoneyCalculatorBase result)
		{
			GameId = result.GameId;
			GameTypeString = result.GameTypeString;
			GameTypeConfidence = result.GameTypeConfidence;
			MoneyWon1 = result.MoneyWon[0];
            MoneyWon2 = result.MoneyWon[1];
            MoneyWon3 = result.MoneyWon[2];
        }
    }
}

