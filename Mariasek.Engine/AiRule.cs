﻿
using System;

namespace Mariasek.Engine
{
    public class AiRule
    {
        public int Order { get; set; }
        public bool UseThreshold { get; set; }
		public bool SkipSimulations { get; set; }
        public string Description { get; set; }
        public string AiDebugInfo { get; set; }

        public Func<Card> ChooseCard1;
        public Func<Card, Card> ChooseCard2;
        public Func<Card, Card, Card> ChooseCard3;

        public static readonly AiRule PlayTheOnlyValidCard = new AiRule
                                                             {
                                                                 Description = "Hraj jedinou možnou kartu"
                                                             };
        public override string ToString()
        {
            return Description ?? base.ToString();
        }
    }
}
