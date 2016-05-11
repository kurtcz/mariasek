using System.Collections.Generic;
using System.Windows;
using Mariasek.Engine.New;

namespace Mariasek.TesterGUI
{
    public class HumanPlayer : AbstractPlayer, IStatsPlayer
    {
        private MainWindow _window;
        private Barva _trump;
        private bool firstTimeChoosingFlavour;
        private Hra _gameType;

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
            _gameType = 0;
            firstTimeChoosingFlavour = true;
            Probabilities = new Probability(PlayerIndex, _g.GameStartingPlayerIndex, new Hand(Hand), _g.trump, _g.talon);
        }

        public override Card ChooseTrump()
        {
            var trumpCard = _window.ChooseTrump();
            _trump = trumpCard.Suit;

            return trumpCard;
        }

        public override List<Card> ChooseTalon()
        {
            return _window.ChooseTalon();
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
                    _gameType = _window.ChooseGameType(validGameTypes);
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

            return _window.ChooseGameFlavour();
        }

        public override Hra ChooseGameType(Hra validGameTypes)
        {
            if(_gameType != 0)
            {
                return _gameType;
            }
            return _window.ChooseGameType(validGameTypes);
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
                if (r.c2 != null)
                {
                    validationState = IsCardValid(card, r.c1, r.c2);
                    if (validationState == Renonc.Ok)
                        break;
                }
                else if (r.c1 != null)
                {
                    validationState = IsCardValid(card, r.c1);
                    if (validationState == Renonc.Ok)
                        break;
                }
                else
                {
                    validationState = IsCardValid(card);
                    if (validationState == Renonc.Ok)
                        break;
                }
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
