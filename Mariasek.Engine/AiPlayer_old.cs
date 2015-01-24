using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Logger;
using log4net;

namespace Mariasek.Engine
{
    public class AiPlayer : AbstractPlayer
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<Card>[] hands = new List<Card>[Game.NumPlayers];
        private List<Card>[] probHands = new List<Card>[Game.NumPlayers];
        private ulong counter;
        private bool mycardsremoved = false;

        public AiPlayer(string name, Game g)
            : base(name, g)
        {
            LoggerSetup.TraceFileSetup("LogFile.txt", LoggerSetup.FileMode.Append);

            //na zacatku muzou mit vsichni vsechny karty
            for(int i = 0; i < Game.NumPlayers; i++)
            {
                probHands[i] = new List<Card>();
                for (int j = 0; j < 32; j++)
                {
                    Card c = new Card((Barva)Enum.ToObject(typeof(Barva), j / 8),
                                      (Hodnota)Enum.ToObject(typeof(Hodnota), j % 8));
                    c.Num = j;
                    probHands[i].Add(c);
                }
            }
        }

        protected static Int64 Factorial(int factor)
        {
            Int64 factorial = 1;

            for (int i = 1; i <= factor; i++)
            {
                factorial *= i;
            }

            return factorial;
        }

        //spocita pocet kombinaci karet z mnoziny moznych karet pro daneho hrace
        protected long TotalCombinations(int playerNum)
        {
            return Komb(probHands.Length, 10 - _g.RoundNumber + 1);
        }

        //spocita pocet kombinaci karet pri kterych hrac nedokaze prebit moji kartu
        //jestlize existuje k prebijejicich karet ktere teoreticky muze mit
        protected long TotalSafeCombinations(int playerNum, int k)
        {
            return Komb(probHands.Length, 10 - _g.RoundNumber + 1);
        }

        //spocita pravdepodobnost ze moje karta projde pokud existuje k karet ktere muzou moji kartu prebit
        //ktere souper teoreticky muze mit
        protected long SuccessProbability(int playerNum, int k)
        {
            return 100 * TotalSafeCombinations(playerNum, k)/TotalCombinations(playerNum);
        }

        protected static Int64 Komb(int n, int k)
        {
            return Factorial(n)/(Factorial(n - k)*Factorial(k));
        }

        //odmaz souperum tyto karty
        protected void RemoveCards(List<Card> cards)
        {
            //gameIndex je muj index ve hre obecne
            int gameIndex = Array.IndexOf(_g.players, this);

            for (int i = 0; i < Game.NumPlayers; i++)
            {
                if (i != gameIndex)
                {
                    probHands[i].RemoveAll(
                        opponentCard =>
                        cards.Exists(item => item == opponentCard));
                }
                else
                {
                    probHands[i].Clear();
                }
            }
        }

        //odmaz souperum tuto kartu
        protected void RemoveCard(Card card)
        {
            int gameIndex = Array.IndexOf(_g.players, this);

            for (int i = 0; i < Game.NumPlayers; i++)
            {
                if (i != gameIndex)
                {
                    probHands[i].Remove(card);
                }
            }
        }

        protected List<Card> ValidCards()
        {
            return ValidCards(Hand);
        }

        protected List<Card> ValidCards(Card first)
        {
            return ValidCards(Hand, first);
        }

        protected List<Card> ValidCards(Card first, Card second)
        {
            return ValidCards(Hand, first, second);
        }

        protected List<Card> ValidCards(List<Card> hand)
        {
            return hand.FindAll(item => IsCardUnplayed(item) && (IsCardValid(hand, item) == Renonc.Ok));
        }

        protected List<Card> ValidCards(List<Card> hand, Card first)
        {
            return hand.FindAll(item => IsCardUnplayed(item) && (IsCardValid(hand, _g.trump, item, first) == Renonc.Ok));
        }

        protected List<Card> ValidCards(List<Card> hand, Card first, Card second)
        {
            return hand.FindAll(item => IsCardUnplayed(item) && (IsCardValid(hand, _g.trump, item, first, second) == Renonc.Ok));
        }

        public override void ChooseTalon()
        {
            throw new NotImplementedException();
        }

        public override void ChooseTrump()
        {
            throw new NotImplementedException();
        }

        private Hashtable hashtable = new Hashtable();
        protected string ComputeHash(int playerNum, Card card)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0:x2}", card.Num);
            int i = 1;
            for (int j = 0; j < Game.NumPlayers; j++)
            {
                foreach (var c in hands[(playerNum + j) % Game.NumPlayers])
                {
                    if (c.RoundPlayed == 0)
                    {
                        sb.AppendFormat("{0:x2}", c.Num);
                    }
                }
            }

            return sb.ToString();
        }

        public override void PlayCard(AbstractPlayer.Renonc err)
        {
            counter = 0;
            hashtable.Clear();
            _log.InfoFormat("Playcard started. Round {0}", _g.RoundNumber);
            //Zkusime vymyslet optimalni strategii pro dane rozlozeni karet
            //Pro zacatek budeme koukat hracum do karet ...
            int myindex = Array.IndexOf(_g.players, this);
            hands[0] = Hand;
            hands[1] = _g.players[(myindex + 1) % Game.NumPlayers].Hand;
            hands[2] = _g.players[(myindex + 2) % Game.NumPlayers].Hand;

            int maxscore = int.MinValue;
            List<Card> cards = ValidCards(hands[0]);
            Card cardToPlay = cards[0];

            //foreach (var card in cards)
            //{
            //    int score = EvalCard(0, card, _g.RoundNumber);
            //    if (score > maxscore)
            //    {
            //        maxscore = score;
            //        cardToPlay = card;
            //    }
            //}
            _log.InfoFormat("Play {0}", cardToPlay, maxscore);
            OnCardPlayed(new CardEventArgs(cardToPlay));
            _log.InfoFormat("Playcard finished. {0} Calls to EvalCard made. Hashtable has {1} entries", counter, hashtable.Count);
        }

        private int EvalCard(int playerNum, Card card, int round)
        {
            counter++;

            int minscore = int.MaxValue;
            int roundscore = 0, score;
            card.RoundPlayed = round;
            string hash = ComputeHash(playerNum, card);
            if (hashtable.ContainsKey(hash))
            {
                card.RoundPlayed = 0;
                return (int)hashtable[hash];
            }

            List<Card> cards2 = ValidCards(hands[(playerNum + 1) % Game.NumPlayers], card);
            foreach (var c2 in cards2)
            {
                c2.RoundPlayed = round;
                List<Card> cards3 = ValidCards(hands[(playerNum + 2) % Game.NumPlayers], card, c2);
                foreach (var c3 in cards3)
                {
                    c3.RoundPlayed = round;
                    //winner: 0 = ten kdo vynasi v tomhle kole
                    int winner = WinnerOfTheRound(card, c2, c3);
                    //winnerIndex: 0 = ten pro koho pocitame skore celkove
                    int winnerIndex = (playerNum + winner) % Game.NumPlayers;

                    card.CardWinner = winnerIndex;
                    c2.CardWinner = winnerIndex;
                    c3.CardWinner = winnerIndex;

                    //gameIndex je muj index ve hre obecne
                    //gameStarterIndex je index toho, kdo volil trumfy (a hraje sam) ve hre obecne
                    //gameStarterRoundIndex je index toho, kdo volil trumfy (a hraje sam) v tomto kole (0 = ten, pro koho pocitame)
                    int gameIndex = Array.IndexOf(_g.players, this);
                    int gameStarterIndex = Array.IndexOf(_g.players, _g.GameStartingPlayer);
                    int delta = Game.NumPlayers - gameIndex;
                    int gameStarterRoundIndex = (gameStarterIndex + delta) % Game.NumPlayers;

                    if (winnerIndex == 0 || winnerIndex != gameStarterRoundIndex)
                    {
                        if (card.Value == Hodnota.Desitka || card.Value == Hodnota.Eso)
                        {
                            roundscore += 10;
                        }
                        if (c2.Value == Hodnota.Desitka || c2.Value == Hodnota.Eso)
                        {
                            roundscore += 10;
                        }
                        if (c3.Value == Hodnota.Desitka || c3.Value == Hodnota.Eso)
                        {
                            roundscore += 10;
                        }
                    }
                    else
                    {
                        if (card.Value == Hodnota.Desitka || card.Value == Hodnota.Eso)
                        {
                            roundscore -= 10;
                        }
                        if (c2.Value == Hodnota.Desitka || c2.Value == Hodnota.Eso)
                        {
                            roundscore -= 10;
                        }
                        if (c3.Value == Hodnota.Desitka || c3.Value == Hodnota.Eso)
                        {
                            roundscore -= 10;
                        }
                    }

                    if (round == 10)
                    {
                        //score = 0;
                        //spocitej skore (hraje vzdy prvni proti druhymu a tretimu)
                        //TODO: hlasky
                        //TODO: misto rozdilu skore vracej skore pro a proti (kvuli kilu)

                        //posledni stych
                        if (winnerIndex == 0 || winnerIndex != gameStarterRoundIndex)
                        {
                            roundscore += 10;
                        }
                        else
                        {
                            roundscore -= 10;
                        }

                        minscore = roundscore;
                    }
                    else
                    {
                        List<Card> cards = ValidCards(hands[winnerIndex]);
                        foreach (var c1 in cards)
                        {
                            score = roundscore + EvalCard(winnerIndex, c1, round + 1);
                            if (minscore > score)
                            {
                                minscore = score;
                            }
                        }
                    }
                    c3.RoundPlayed = 0;
                }
                c2.RoundPlayed = 0;
            }
            card.RoundPlayed = 0;

            if (!hashtable.ContainsKey(hash))
            {
                hashtable.Add(hash, minscore);
            }
            return minscore;
        }

        public override void PlayCard(Card first, AbstractPlayer.Renonc err)
        {
            counter = 0;
            _log.InfoFormat("Playcard2 started. Round {0}. {1} played", _g.RoundNumber, first);
            //Zkusime vymyslet optimalni strategii pro dane rozlozeni karet
            //Pro zacatek budeme koukat hracum do karet ...
            int myindex = Array.IndexOf(_g.players, this);
            hands[0] = Hand;
            hands[1] = _g.players[(myindex + 1) % Game.NumPlayers].Hand;
            hands[2] = _g.players[(myindex + 2) % Game.NumPlayers].Hand;

            int maxscore = int.MinValue;
            List<Card> cards = ValidCards(hands[0], first);
            Card cardToPlay = cards[0];

            //foreach (var card in cards)
            //{
            //    int score = EvalCard2(card, first, _g.RoundNumber);
            //    if (score > maxscore)
            //    {
            //        maxscore = score;
            //        cardToPlay = card;
            //    }
            //}
            _log.InfoFormat("Play {0}", cardToPlay, maxscore);
            OnCardPlayed(new CardEventArgs(cardToPlay));
            _log.InfoFormat("Playcard finished. {0} Calls to EvalCard made. Hashtable has {1} entries", counter, hashtable.Count);
        }

        private int EvalCard2(Card c2, Card card, int round)
        {
            counter++;

            int minscore = int.MaxValue;
            card.RoundPlayed = round;
            c2.RoundPlayed = round;
            List<Card> cards3 = ValidCards(hands[1], card, c2);
            foreach (var c3 in cards3)
            {
                c3.RoundPlayed = round;
                int score = 0;
                //winnerIndex: 0 = ten kdo vynasi v tomhle kole
                //zde je to i ten pro koho pocitame skore celkove
                int winnerIndex = (WinnerOfTheRound(card, c2, c3) + 2) % Game.NumPlayers;

                card.CardWinner = winnerIndex;
                c2.CardWinner = winnerIndex;
                c3.CardWinner = winnerIndex;

                if (round == 10)
                {
                    //v poslednim kole nemusime nic resit
                    minscore = 0;
                }
                else
                {
                    List<Card> cards = ValidCards(hands[winnerIndex]);
                    foreach (var c1 in cards)
                    {
                        score = EvalCard(winnerIndex, c1, round + 1);
                        if (minscore > score)
                        {
                            minscore = score;
                        }
                    }
                }
                c3.RoundPlayed = 0;
            }
            c2.RoundPlayed = 0;
            card.RoundPlayed = 0;

            return minscore;
        }

        public override void PlayCard(Card first, Card second, AbstractPlayer.Renonc err)
        {
            counter = 0;
            _log.InfoFormat("Playcard3 started. Round {0}. {1} {2} played", _g.RoundNumber, first, second);
            //Zkusime vymyslet optimalni strategii pro dane rozlozeni karet
            //Pro zacatek budeme koukat hracum do karet ...
            int myindex = Array.IndexOf(_g.players, this);
            hands[0] = Hand;
            hands[1] = _g.players[(myindex + 1) % Game.NumPlayers].Hand;
            hands[2] = _g.players[(myindex + 2) % Game.NumPlayers].Hand;

            int maxscore = int.MinValue;
            List<Card> cards = ValidCards(hands[0], first, second);
            Card cardToPlay = cards[0];

            //foreach (var card in cards)
            //{
            //    int score = EvalCard3(card, first, second, _g.RoundNumber);
            //    if (score > maxscore)
            //    {
            //        maxscore = score;
            //        cardToPlay = card;
            //    }
            //}
            _log.InfoFormat("Play {0}", cardToPlay, maxscore);
            OnCardPlayed(new CardEventArgs(cardToPlay));
            _log.InfoFormat("Playcard finished. {0} Calls to EvalCard made. Hashtable has {1} entries", counter, hashtable.Count);
        }

        private int EvalCard3(Card c3, Card card, Card c2, int round)
        {
            counter++;

            int minscore = int.MaxValue;
            card.RoundPlayed = round;
            c2.RoundPlayed = round;
            c3.RoundPlayed = round;
            int score = 0;
            //winnerIndex: 0 = ten kdo vynasi v tomhle kole
            //zde je to stejne jako ten pro koho pocitame skore celkove
            int winnerIndex = (WinnerOfTheRound(card, c2, c3) + 1) % Game.NumPlayers;

            card.CardWinner = winnerIndex;
            c2.CardWinner = winnerIndex;
            c3.CardWinner = winnerIndex;

            if (round == 10)
            {
                //v poslednim kole nemusime nic resit
                minscore = 0;
            }
            else
            {
                List<Card> cards = ValidCards(hands[winnerIndex]);
                foreach (var c1 in cards)
                {
                    score = EvalCard(winnerIndex, c1, round + 1);
                    if (minscore > score)
                    {
                        minscore = score;
                    }
                }
            }
            c3.RoundPlayed = 0;
            c2.RoundPlayed = 0;
            card.RoundPlayed = 0;

            return minscore;
        }

        protected int WinnerOfTheRound(Card c1, Card c2, Card c3)
        {
            if (c1.IsHigherThan(c2, _g.trump))
            {
                if (c1.IsHigherThan(c3, _g.trump))
                {
                    return 0;
                }
                else
                {
                    return 2;
                }
            }
            else
            {
                if (c2.IsHigherThan(c3, _g.trump))
                {
                    return 1;
                }
                else
                {
                    return 2;
                }
            }
        }

        protected void RemoveCardsFromPlayer(int playerIndex, Card first, Card second)
        {
            //zkontroluj jestli player2 nesel vejs
            if (second.Suit == first.Suit && second.Value < first.Value)
            {
                probHands[playerIndex].RemoveAll(item => item.Suit == first.Suit && item.Value > first.Value);
            }
            else if (first.Suit != second.Suit)
            {
                //zkontroluj jestli player2 nehral trumf
                if (second.Suit == _g.trump)
                {
                    probHands[playerIndex].RemoveAll(item => item.Suit == first.Suit);
                }
                else
                {
                    probHands[playerIndex].RemoveAll(item => (item.Suit == first.Suit) || (item.Suit == _g.trump));
                }
            }
        }

        public override void RoundInfo(Card first, AbstractPlayer player1, bool hlas)
        {
            RemoveCard(first);
            if(!mycardsremoved)
            {
                RemoveCards(Hand);
                mycardsremoved = true;
            }
        }

        public override void RoundInfo(Card first, Card second, AbstractPlayer player1, AbstractPlayer player2, bool hlas)
        {
            int myIndex = Array.IndexOf(_g.players, this);
            int player2Index = Array.IndexOf(_g.players, player2);

            RemoveCard(second);

            if (myIndex == player2Index)
            {
                return;
            }

            RemoveCardsFromPlayer(player2Index, first, second);
        }

        public override void RoundInfo(Card first, Card second, Card third, AbstractPlayer player1, AbstractPlayer player2, AbstractPlayer player3, bool hlas)
        {
            int myIndex = Array.IndexOf(_g.players, this);
            int player2Index = Array.IndexOf(_g.players, player2);
            int player3Index = Array.IndexOf(_g.players, player3);

            RemoveCard(third);

            if (myIndex == player3Index)
            {
                return;
            }

            //porovnej kartu s tou nejvyssi hranou v tomhle kole
            if(first.IsHigherThan(second, _g.trump))
            {
                //prvni karta je nejvyssi
                RemoveCardsFromPlayer(player3Index, first, third);
            }
            else
            {
                //druha karta prebiji prvni
                if (first.Suit == second.Suit)
                {
                    RemoveCardsFromPlayer(player3Index, second, third);
                }
                else
                {
                    //druha karta je trumf
                    if(!third.IsHigherThan(second, _g.trump))
                    {
                        if(second.Suit == third.Suit)
                        {
                            //treti karta je taky trumf, ale nizsi nez druha karta
                            probHands[player3Index].RemoveAll(
                                item =>
                                (item.Suit == first.Suit) || (item.Suit == second.Suit && item.Value > second.Value));
                        }
                        else
                        {
                            //treti karta neni trumf
                            probHands[player3Index].RemoveAll(
                                item =>
                                (item.Suit == first.Suit) || (item.Suit == second.Suit));
                        }
                    }
                }
            }
        }

        public override void RoundFinishedInfo(AbstractPlayer winner, int points)
        {
            OnEndOfRoundConfirmed();
        }
    }
}
