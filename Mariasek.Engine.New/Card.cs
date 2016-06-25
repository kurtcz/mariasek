using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Mariasek.Engine.New
{
    public enum Barva
    {
        [Description("červený")]
        Cerveny,
        [Description("zelený")]
        Zeleny,
        [Description("kule")]
        Kule,
        [Description("žaludy")]
        Zaludy
    };

    public enum Hodnota
    {
        Sedma = 0,
        Osma = 1,
        Devitka = 2,
        Spodek = 3,
        Svrsek = 4,
        Kral = 5,
        Desitka = 6,
        Eso = 7
    };

    public static class CardResources
    {
        public static string[] Images = {
                                            "cerveny7.png",
                                            "cerveny8.png",
                                            "cerveny9.png",
                                            "cervenyspodek.png",
                                            "cervenysvrsek.png",
                                            "cervenykral.png",
                                            "cerveny10.png",
                                            "cervenyeso.png",
                                            "zeleny7.png",
                                            "zeleny8.png",
                                            "zeleny9.png",
                                            "zelenyspodek.png",
                                            "zelenysvrsek.png",
                                            "zelenykral.png",
                                            "zeleny10.png",
                                            "zelenyeso.png",
                                            "kule7.png",
                                            "kule8.png",
                                            "kule9.png",
                                            "kulespodek.png",
                                            "kulesvrsek.png",
                                            "kulekral.png",
                                            "kule10.png",
                                            "kuleeso.png",
                                            "zaludy7.png",
                                            "zaludy8.png",
                                            "zaludy9.png",
                                            "zaludyspodek.png",
                                            "zaludysvrsek.png",
                                            "zaludykral.png",
                                            "zaludy10.png",
                                            "zaludyeso.png",
                                        };
    }

    public class Card : IComparable
    {
        public int RoundPlayed; //pouziva se v AI simulaci
        public int CardWinner;  //pouziva se v AI simulaci
        public int Num { get { return (int)Suit * 8 + (int)Value; } }
        public const Barva MaxSuit = Barva.Cerveny;
        public const Barva MinSuit = Barva.Zaludy;

        public const Hodnota MaxValue = Hodnota.Eso;
        public const Hodnota MinValue = Hodnota.Sedma;

        public Barva Suit { get; set; }
        public Hodnota Value { get; set; }
        public int BadValue
        {
            get
            {
                if (Value == Hodnota.Desitka)
                {
                    return (int)(Hodnota.Devitka) + 1;
                }

                if (Value > Hodnota.Devitka)
                {
                    return (int)Value + 1;
                }

                return (int)Value;
            }
        }
        private const string CharCodes = "789sSKXA";
        public char CharCode
        {
            get { return CharCodes[(int)Value]; }
        }

        //default constructor for XmlSerializer
        public Card()
            :this (Barva.Cerveny, Hodnota.Sedma)
        {
			Value = Value + 1 - 1;
        }

        public Card(Barva suit, Hodnota value)
        {
            Suit = suit;
            Value = value;
            RoundPlayed = 0;
            CardWinner = -1;
        }

        public override string ToString()
        {
            return string.Format("{0} {1}", Value, Suit);
        }

        public bool IsHigherThan(Card second, Barva? trump)
        {
            if (Suit != second.Suit)
            {
                //nepriznana barva. Je to trumf?
                return !trump.HasValue || second.Suit != trump.Value;
            }         
            else
            {
                //priznana barva, je vetsi nez prvni?
                if (trump != null || (Value != Hodnota.Desitka && second.Value != Hodnota.Desitka))
                {
                    return Value > second.Value;
                }
                else
                {
                    //betl nebo durch, porovnavam desitku
                    if (Value == Hodnota.Desitka)
                    {
                        return Hodnota.Devitka >= second.Value;
                    }
                    else //second.Value == Hodnota.Desitka
                    {
                        return Value > Hodnota.Devitka;
                    }
                }
            }
        }

        public bool IsLowerThan(Card second, Barva? trump)
        {
            return !IsHigherThan(second, trump);
        }

        public static Card HigherCard(Card first, Card second, Barva trump)
        {
            return first.IsHigherThan(second, trump) ? first : second;
        }

        public override bool Equals(System.Object obj)
        {
            if(obj == null)
            {
                return false;
            }

            Card card = obj as Card;
            if((object)card == null)
            {
                return false;
            }

            return card.Suit == Suit && card.Value == Value;
        }

        public bool Equals(Card card)
        {
            if ((object)card == null)
            {
                return false;
            }

            return card.Suit == Suit && card.Value == Value;
        }

        public static bool operator == (Card a, Card b)
        {
            if(System.Object.ReferenceEquals(a, b))
            {
                return true;
            }

            if(((object)a == null) || ((object)b == null))
            {
                return false;
            }

            return a.Suit == b.Suit && a.Value == b.Value;
        }

        public static bool operator != (Card a, Card b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return Num;
        }

        public int CompareTo(object obj)
        {
            var rhs = obj as Card;

            if (rhs != null)
            {
                var result = (int) Suit - (int) rhs.Suit;

                if (result == 0)
                {
                    result = (int) Value - (int) rhs.Value;
                }

                return result;
            }
            throw new InvalidOperationException(string.Format("Cannot compare Card to {0}", obj.GetType()));
        }
    }
}
