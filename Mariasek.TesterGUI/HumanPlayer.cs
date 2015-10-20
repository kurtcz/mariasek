using System.Collections.Generic;
using System.Windows;
using Mariasek.Engine.New;

namespace Mariasek.TesterGUI
{
    public class HumanPlayer : AbstractPlayer, IStatsPlayer
    {
        private MainWindow _window;

        public Probability Probabilities { get; set; }

        public HumanPlayer(Game g)
            : base(g)
        {
            _window = (MainWindow) Application.Current.MainWindow;
            g.GameTypeChosen += GameTypeChosen;
            g.CardPlayed += CardPlayed;
        }

        public override void Init()
        {
            Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, _g.talon);
        }

        public override Card ChooseTrump()
        {
            return _window.ChooseTrump();
        }

        public override List<Card> ChooseTalon()
        {
            return _window.ChooseTalon();
        }

        public override GameFlavour ChooseGameFlavour()
        {
            return _window.ChooseGameFlavour();
        }

        public override Hra ChooseGameType(Hra minimalBid = Hra.Hra)
        {
            return _window.ChooseGameType(minimalBid);
        }

        public override Hra GetBidsAndDoubles(Bidding bidding)
        {
            return _window.GetBidsAndDoubles(bidding);
        }

        public override Card PlayCard(Round r)
        {
            Card card;
            var validationState = Renonc.Ok;

            while (true)
            {
                card = _window.PlayCard(validationState);
                if (r.c2 != null && (validationState = IsCardValid(card, r.c1, r.c2)) == Renonc.Ok)
                    break;
                else if (r.c1 != null && (validationState = IsCardValid(card, r.c1)) == Renonc.Ok)
                    break;
                else if (r.c1 == null && (validationState = IsCardValid(card)) == Renonc.Ok)
                    break;
            }
            
            return card;
        }

        private void GameTypeChosen(object sender, GameTypeChosenEventArgs e)
        {
            Probabilities.UpdateProbabilitiesAfterGameTypeChosen(e);
        }

        public void CardPlayed(object sender, Round r)
        {
            if (r.c3 != null)
            {
                Probabilities.UpdateProbabilities(r.number, r.player1.PlayerIndex, r.c1, r.c2, r.c3, r.hlas3);
            }
            else if (r.c2 != null)
            {
                Probabilities.UpdateProbabilities(r.number, r.player1.PlayerIndex, r.c1, r.c2, r.hlas2);
            }
            else
            {
                Probabilities.UpdateProbabilities(r.number, r.player1.PlayerIndex, r.c1, r.hlas1);
            }
        }
    }
}
