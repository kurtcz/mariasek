using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Mariasek.Engine
{
    /// <summary>
    /// Encapsulation of a human player
    /// </summary>
    public class Player : AbstractPlayer, IPlayerStats
    {
        private object lck;

        public Probability Probabilities { get; set; }

        public Player(string name, Game g) : base(name, g)
        {
            lck = new object();
        }

        public int MyIndex
        {
            get { return Array.IndexOf(_g.players, this); }
        }

        public override void ChooseTalon()
        {
            OnChooseTalonCommand();
        }

        public override void ChooseTrump()
        {
            OnChooseTrumpCommand();
        }

        public override void ChooseGameType()
        {
            OnChooseGameTypeCommand();
        }

        public override void PlayCard(Renonc err)
        {
            if (Probabilities == null)
            {
                Probabilities = _g.GameStartingPlayer == this ? new Probability(MyIndex, GameStarterIndex, new Hand(Hand), _g.trump, _g.talon)
                                                              : new Probability(MyIndex, GameStarterIndex, new Hand(Hand), _g.trump);
            }

            OnPlayCardCommand(err);
        }

        public override void PlayCard2(Card first, Renonc err)
        {
            var testhands = Probabilities.GenerateHands(_g.RoundNumber, MyIndex);
            OnPlayCardCommand(err);
        }

        public override void PlayCard3(Card first, Card second, Renonc err)
        {
            var testhands = Probabilities.GenerateHands(_g.RoundNumber, MyIndex);
            OnPlayCardCommand(err);
        }

        public override void RoundInfo(Card first, AbstractPlayer player1, bool hlas)
        {
            var roundStartingIndex = Array.IndexOf(_g.players, _g.RoundStartingPlayer);

            if (Probabilities == null)
            {
                Probabilities = new Probability(MyIndex, GameStarterIndex, new Hand(Hand), _g.trump);
            }
            Probabilities.UpdateProbabilities(_g.RoundNumber, roundStartingIndex, _g.CurrentRound.c1, _g.CurrentRound.player1Hlas != 0, _g.trump);

            OnPlayer1Played(new CardPlayerEventArgs(first, player1, hlas));
        }

        public override void RoundInfo(Card first, Card second, AbstractPlayer player1, AbstractPlayer player2, bool hlas)
        {
            var roundStartingIndex = Array.IndexOf(_g.players, _g.RoundStartingPlayer);

            Probabilities.UpdateProbabilities(_g.RoundNumber, roundStartingIndex, _g.CurrentRound.c1, _g.CurrentRound.c2, _g.CurrentRound.player2Hlas != 0);

            OnPlayer2Played(new CardPlayerEventArgs(second, player2, hlas));
        }

        public override void RoundInfo(Card first, Card second, Card third, AbstractPlayer player1, AbstractPlayer player2, AbstractPlayer player3, bool hlas)
        {
            var roundStartingIndex = Array.IndexOf(_g.players, player1);

            Probabilities.UpdateProbabilities(_g.RoundNumber, roundStartingIndex, _g.CurrentRound.c1, _g.CurrentRound.c2, _g.CurrentRound.c3, _g.CurrentRound.player3Hlas != 0);

            OnPlayer3Played(new CardPlayerEventArgs(third, player3, hlas));
        }

        public override void RoundFinishedInfo(AbstractPlayer winner, int points)
        {
            OnRoundFinished(new RoundFinishedEventArgs(winner, points));
        }

        #region Events and Delegates

        //public delegate void ChooseTalonCommandDelegate();
        //public event ChooseTalonCommandDelegate ChooseTalonCommand;
        //protected virtual void OnChooseTalonCommand()
        //{
        //    if (ChooseTalonCommand != null)
        //        ChooseTalonCommand();
        //}

        //public delegate void ChooseTrumpCommandDelegate();
        //public event ChooseTrumpCommandDelegate ChooseTrumpCommand;
        //protected virtual void OnChooseTrumpCommand()
        //{
        //    if (ChooseTrumpCommand != null)
        //        ChooseTrumpCommand();
        //}

        //public delegate void PlayCardCommandDelegate(Renonc err);
        //public event PlayCardCommandDelegate PlayCardCommand;
        //protected virtual void OnPlayCardCommand(Renonc err)
        //{
        //    if (PlayCardCommand != null)
        //        PlayCardCommand(err);
        //}

        public delegate void CardPlayerEventHandler(object sender, CardPlayerEventArgs e);
        public event CardPlayerEventHandler Player1Played;
        protected virtual void OnPlayer1Played(CardPlayerEventArgs e)
        {
            if (Player1Played != null)
                Player1Played(this, e);
        }

        public event CardPlayerEventHandler Player2Played;
        protected virtual void OnPlayer2Played(CardPlayerEventArgs e)
        {
            if (Player2Played != null)
                Player2Played(this, e);
        }

        public event CardPlayerEventHandler Player3Played;
        protected virtual void OnPlayer3Played(CardPlayerEventArgs e)
        {
            if (Player3Played != null)
                Player3Played(this, e);
        }

        #endregion
    }
}
