using System;
using System.Collections.Generic;
using System.Windows;
using Mariasek.Engine.New;

namespace Mariasek.SharedClient
{
    public class HumanPlayer : AbstractPlayer, IStatsPlayer
    {
        private MainScene _scene;
        private bool firstTimeChoosingFlavour;
        private Hra _gameType;
        private Barva _trump;
        private Game _g;

        public Probability Probabilities { get; set; }

        public HumanPlayer(Game g, MainScene scene)
            : base(g)
        {
            _scene = scene;
            _g = g;
            _g.GameTypeChosen += GameTypeChosen;
            _g.CardPlayed += CardPlayed;
        }

        public override void Init()
        {
            _gameType = 0;
            firstTimeChoosingFlavour = true;
            Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, _g.talon);
            if (_g.GameStartingPlayerIndex != 0)
            {
                _scene.UpdateHand(flipCardsUp: true);
            }
        }

        public override Card ChooseTrump()
        {
            var trumpCard = _scene.ChooseTrump();

            _trump = trumpCard.Suit;
            return trumpCard;
        }

        public override List<Card> ChooseTalon()
        {
            return _scene.ChooseTalon();
        }

        public override GameFlavour ChooseGameFlavour()
        {
            if (firstTimeChoosingFlavour)
            {
                firstTimeChoosingFlavour = false;
                //poprve volici hrac nehlasi dobra/spatna ale vybira z typu her, cimz se dobra/spatna implicitne zvoli
                if (PlayerIndex == _g.GameStartingPlayerIndex)
                {
                    var validGameTypes = Hra.Hra | Hra.Kilo | Hra.Betl | Hra.Durch;

                    if (Hand.Contains(new Card(_trump, Hodnota.Sedma)))
                    {
                        validGameTypes |= Hra.Sedma;
                    }
                    _gameType = _scene.ChooseGameType(validGameTypes);
                    if ((_gameType & (Hra.Betl | Hra.Durch)) != 0)
                    {
                        return GameFlavour.Bad;
                    }
                    else
                    {
                        return GameFlavour.Good;
                    }
                }
            }
            _gameType = 0;

            return _scene.ChooseGameFlavour();
        }

        public override Hra ChooseGameType(Hra validGameTypes)
        {
            if(_gameType != 0)
            {
                return _gameType;
            }
            return _scene.ChooseGameType(validGameTypes);
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
