using System;
using System.Collections.Generic;
using System.Windows;
using Mariasek.Engine.New;

namespace Mariasek.SharedClient
{
    public class HumanPlayer : AbstractPlayer, IStatsPlayer
    {
        private MainScene _scene;

        public Probability Probabilities { get; set; }

        public HumanPlayer(Game g, MainScene scene)
            : base(g)
        {
            _scene = scene;
            g.GameFlavourChosen += GameFlavourChosen;
            g.GameTypeChosen += GameTypeChosen;
            g.CardPlayed += CardPlayed;
        }

        public override void Init()
        {
            Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, _g.talon);
        }

        public override Card ChooseTrump()
        {
            return _scene.ChooseTrump();
        }

        public override List<Card> ChooseTalon()
        {
            return _scene.ChooseTalon();
        }

        public override GameFlavour ChooseGameFlavour()
        {
            return _scene.ChooseGameFlavour();
        }

        public override Hra ChooseGameType(Hra minimalBid = Hra.Hra)
        {
            return _scene.ChooseGameType(minimalBid);
        }

        public override Hra GetBidsAndDoubles(Bidding bidding)
        {
            return _scene.GetBidsAndDoubles(bidding);
        }

        public override Card PlayCard(Round r)
        {
            Card card;
            var validationState = Renonc.Ok;

            while (true)
            {
                card = _scene.PlayCard(validationState);
                if (r.c2 != null && (validationState = IsCardValid(card, r.c1, r.c2)) == Renonc.Ok)
                    break;
                else if (r.c1 != null && (validationState = IsCardValid(card, r.c1)) == Renonc.Ok)
                    break;
                else if (r.c1 == null && (validationState = IsCardValid(card)) == Renonc.Ok)
                    break;
            }

            return card;
        }

        private void GameFlavourChosen(object sender, GameFlavourChosenEventArgs e)
        {
            _scene.GameFlavourChosen(sender, e);
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
                Probabilities.UpdateProbabilities(r.number, r.player1.PlayerIndex, r.c1, r.hlas1, _g.trump);
            }
        }
    }
}
