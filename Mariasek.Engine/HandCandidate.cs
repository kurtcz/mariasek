using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Mariasek.Engine
{
    public enum OneOpponentRule
    {
        /// <summary>
        ///  V barve kde mam eso ma souper tolik nizkejch karet jako ja (nebo co nejvic)
        /// </summary>
        MamASouperMaNizky,
        /// <summary>
        /// V barve kde mam eso souper nezna barvu
        /// </summary>
        MamASouperNeznaBarvu,
        /// <summary>
        /// V barve kde mam desitku ma souper eso a kolega nizkou kartu
        /// </summary>
        MamXNemamASouperMaAKolegaMaNizkou,
        /// <summary>
        /// Nemam eso ani desitku souper ma eso a desitku a kolega ma nizke v barve
        /// </summary>
        NemamANemamXSouperMaAXKolegaMaNizky,
        /// <summary>
        /// Nemam eso ani desitku souper ma tolik nizkych v barve jako kolega (nebo co nejvic) a kolega ma eso a desitku
        /// </summary>
        NemamANemamXSouperMaNizkyKolegaMaAX,
        /// <summary>
        /// Nemam eso ani desitku souper nezna barvu
        /// </summary>
        NemamANemamXSouperNeznaBarvu,
        /// <summary>
        /// Neznam barvu a souper ma tolik karet kolik ja trumfu
        /// </summary>
        NeznamBarvuSouperMaNizky
    };

    public class HandCandidate
    {
        private const int SOUPER = 0;
        private const int KOLEGA = 1;

        public Barva Trump;
        public List<Card> MyHand;
        /// <summary>
        /// Pro jednoho soupere budeme predpokladat ze souper je na indexu nula a kolega na indexu jedna
        /// </summary>
        public List<Card>[] HandPool;
        public List<Card>[] Hand;
        public bool[] cervenyNe;
        public bool[] zelenyNe;
        public bool[] zaludyNe;
        public bool[] kuleNe;

        public HandCandidate(List<Card> myHand, Barva trump, List<Card>[] handPools)
        {
            MyHand = myHand;
            Trump = trump;
            HandPool = new List<Card>[Game.NumPlayers - 1];
            Hand = new List<Card>[Game.NumPlayers - 1];
            cervenyNe = new bool[Game.NumPlayers - 1];
            zelenyNe = new bool[Game.NumPlayers - 1];
            zaludyNe = new bool[Game.NumPlayers - 1];
            kuleNe = new bool[Game.NumPlayers - 1];
            for(int i = 0; i < Game.NumPlayers - 1; i++)
            {
                HandPool[i] = new List<Card>();
                foreach (var card in handPools[i])
                {
                    HandPool[i].Add(card);
                }
                Hand[i] = new List<Card>();
            }            
        }

        public bool AddCardsBasedOnRule(Barva suit, OneOpponentRule rule)
        {
            bool ret = true;

            switch(rule)
            {
                case OneOpponentRule.MamASouperMaNizky:
                    //count obsahuje pocet nizkejch karet v barve
                    int count = MyHand.Count(card => card.Suit == suit && card.Value < Hodnota.Desitka);
                    //souper bude mit tolik nizkejch karet jako ja (nebo co nejvic)
                    int i = 0;
                    List<Card> temp = HandPool[SOUPER].FindAll(item => (i++ < count) && item.Suit == suit && item.Value < Hodnota.Desitka);
                    Hand[SOUPER].AddRange(temp);
                    HandPool[SOUPER].RemoveAll(item => temp.Contains(item));
                    break;
                case OneOpponentRule.MamASouperNeznaBarvu:
                    switch(suit)
                    {
                        case Barva.Cerveny:
                            cervenyNe[SOUPER] = true;
                            break;
                        case Barva.Zeleny:
                            zelenyNe[SOUPER] = true;
                            break;
                        case Barva.Zaludy:
                            zaludyNe[SOUPER] = true;
                            break;
                        case Barva.Kule:
                            kuleNe[SOUPER] = true;
                            break;
                    }
                    break;
                case OneOpponentRule.MamXNemamASouperMaAKolegaMaNizkou:
                    if (HandPool[SOUPER].Exists(item => item.Suit == suit && item.Value == Hodnota.Eso) &&
                        HandPool[KOLEGA].Exists(item => item.Suit == suit && item.Value < Hodnota.Desitka))
                    {
                        Card eso = HandPool[SOUPER].Find(item => item.Suit == suit && item.Value == Hodnota.Eso);
                        Card nizka = HandPool[KOLEGA].Find(item => item.Suit == suit && item.Value < Hodnota.Desitka);
                        Hand[SOUPER].Add(eso);
                        Hand[KOLEGA].Add(nizka);
                        HandPool[SOUPER].Remove(eso);
                        HandPool[KOLEGA].Remove(nizka);
                    }
                    break;
                case OneOpponentRule.NemamANemamXSouperMaAXKolegaMaNizky:
                    break;
                case OneOpponentRule.NemamANemamXSouperMaNizkyKolegaMaAX:
                    break;
                case OneOpponentRule.NemamANemamXSouperNeznaBarvu:
                    break;
                case OneOpponentRule.NeznamBarvuSouperMaNizky:
                    break;
            }

            return ret;
        }
    }
}
