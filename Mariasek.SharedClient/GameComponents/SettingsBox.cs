﻿using System;
using System.Collections.Generic;
using System.Linq;
using Mariasek.Engine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace Mariasek.SharedClient.GameComponents
{
    public class SettingsBox : ScrollBox
    {
        //offsety puvodnich stranek
        public const int page2Offset = 420;
        public const int page3Offset = 900;
        public const int page4Offset = 1320;
        public const int page5Offset = 1800;
        public const int page6Offset = 2220;
        public const int boundsRectHeight = 2640;
        //nove tematicke offsety zacinaji na ruznych mistech puvodnich stranek
        public readonly int[] TopicOffsets = new [] { 0, page2Offset, page4Offset - 300, page6Offset };

        private Tuple<string, int, int>[] _locales;
        private Label _hint;
        private Label _sounds;
        private Label _bgsounds;
        private Label _handSorting;
        private Label _autoSorting;
        private Label _baseBet;
        private Label _kiloCounting;
        private Label _playZeroSumGames;
        private Label _fakeSeven;
        private Label _top107;
        private Label _calculate107Separately;
        private Label _hlasConsidered;
        private Label _thinkingTime;
        private Label _cardFace;
        private Label _cardBack;
        private Label _roundFinishedWaitTime;
        private Label _autoFinishRounds;
        private Label _autoFinishLastRound;
        private Label _autoPlaySingletonCard;
        private Label _autoDisable100Against;
        private Label _showScoreDuringGame;
        private Label _cardSize;
        private Label _gameValue;
        private Label _quietSevenValue;
        private Label _sevenValue;
        private Label _quietHundredValue;
        private Label _hundredValue;
        private Label _betlValue;
        private Label _durchValue;
        private Label _player1;
        private Label _player2;
        private Label _player3;
        private Label _allowAXTalon;
        private Label _allowTrumpTalon;
        private Label _allowAIAutoFinish;
        private Label _allowPlayerAutoFinish;
        private Label _minBidsForGame;
        private Label _minBidsForSeven;
        private Label _whenToShuffle;
        //private Label _maxHistoryLength;
        private Label _directionOfPlay;
        private Label _bubbleTime;
        private Label _maxWin;
        //private Label _showStatusBar;
        private Label _bgImage;
        private Label _aiMayGiveUp;
        private ToggleButton _hintBtn;
        private ToggleButton _soundBtn;
        private ToggleButton _bgsoundBtn;
        private LeftRightSelector _handSortingSelector;
        private LeftRightSelector _autoSortingSelector;
        private LeftRightSelector _baseBetSelector;
        private LeftRightSelector _kiloCountingSelector;
        private LeftRightSelector _playZeroSumGamesSelector;
        private LeftRightSelector _fakeSevenSelector;
        private LeftRightSelector _top107Selector;
        private LeftRightSelector _calculate107Selector;
        private LeftRightSelector _hlasSelector;
        private LeftRightSelector _thinkingTimeSelector;
        private LeftRightSelector _cardFaceSelector;
        private LeftRightSelector _cardBackSelector;
        private LeftRightSelector _roundFinishedWaitTimeSelector;
        private LeftRightSelector _autoFinishRoundsSelector;
        private LeftRightSelector _autoFinishLastRoundSelector;
        private LeftRightSelector _autoPlaySingletonCardSelector;
        private LeftRightSelector _autoDisable100AgainstSelector;
        private LeftRightSelector _showScoreDuringGameSelector;
        private LeftRightSelector _cardSizeSelector;
        private LeftRightSelector _bubbleTimeSelector;
        private LeftRightSelector _maxWinSelector;
        //private LeftRightSelector _showStatusBarSelector;
        private LeftRightSelector _bgImageSelector;
        private LeftRightSelector _aiMayGiveUpSelector;
        private LeftRightSelector _gameValueSelector;
        private LeftRightSelector _quietSevenValueSelector;
        private LeftRightSelector _sevenValueSelector;
        private LeftRightSelector _quietHundredValueSelector;
        private LeftRightSelector _hundredValueSelector;
        private LeftRightSelector _betlValueSelector;
        private LeftRightSelector _durchValueSelector;
        private Button _player1Name;
        private Button _player2Name;
        private Button _player3Name;
        private LeftRightSelector _allowAXTalonSelector;
        private LeftRightSelector _allowTrumpTalonSelector;
        private LeftRightSelector _allowAIAutoFinishSelector;
        private LeftRightSelector _allowPlayerAutoFinishSelector;
        private LeftRightSelector _minBidsForGameSelector;
        private LeftRightSelector _minBidsForSevenSelector;
        private LeftRightSelector _whenToShuffleSelector;
        //private LeftRightSelector _maxHistoryLengthSelector;
        private LeftRightSelector _directionOfPlaySelector;

        public SettingsBox(GameComponent parent) : base(parent)
        {
            _clickableArea.TouchDown += HandleTouchDown;
            _clickableArea.Click += HandleClick;
            #region Page 1
            _sounds = new Label(this)
            {
                Position = new Vector2(200, 10),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Zvuk",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _bgsounds = new Label(this)
            {
                Position = new Vector2(200, 70),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Zvuk pozadí",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _soundBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 10),
                Width = 270,
                Height = 50,
                Group = 1,
                Text = Game.Settings.SoundEnabled ? "Zapnutý" : "Vypnutý",
                BorderColor = Color.Transparent,
                BackgroundColor = Color.Transparent,
                IsSelected = Game.Settings.SoundEnabled,
                UseCommonScissorRect = true
            };
            _soundBtn.Click += SoundBtnClick;
            _bgsoundBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 70),
                Width = 270,
                Height = 50,
                Group = 1,
                Text = Game.Settings.BgSoundEnabled ? "Zapnutý" : "Vypnutý",
                BorderColor = Color.Transparent,
                BackgroundColor = Color.Transparent,
                IsSelected = Game.Settings.BgSoundEnabled,
                UseCommonScissorRect = true
            };
            _bgsoundBtn.Click += BgSoundBtnClick;
            _hint = new Label(this)
            {
                Position = new Vector2(200, 130),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Nápověda",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _hintBtn = new ToggleButton(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 130),
                Width = 270,
                Height = 50,
                Group = 1,
                Text = Game.Settings.HintEnabled ? "Zapnutá" : "Vypnutá",
                BorderColor = Color.Transparent,
                BackgroundColor = Color.Transparent,
                IsSelected = Game.Settings.HintEnabled,
                UseCommonScissorRect = true
            };
            _hintBtn.Click += HintBtnClick;
            _cardFace = new Label(this)
            {
                Position = new Vector2(200, 190),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Vzor karet",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _cardFaceSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 190),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Jednohlavé", CardFace.Single }, { "Dvouhlavé", CardFace.Double }, { "Pikety", CardFace.Pikety } },
                UseCommonScissorRect = true
            };
            _cardFaceSelector.SelectedIndex = _cardFaceSelector.Items.FindIndex(Game.Settings.CardDesign);
            _cardFaceSelector.SelectionChanged += CardFaceChanged;
            if (_cardFaceSelector.SelectedIndex < 0)
            {
                _cardFaceSelector.SelectedIndex = 0;
            }
            _cardBack = new Label(this)
            {
                Position = new Vector2(200, 250),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Rub karet",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _cardBackSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 250),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Káro", CardBackSide.Tartan }, { "Koník", CardBackSide.Horse }, { "Krajka", CardBackSide.Lace } },
                UseCommonScissorRect = true
            };
            _cardBackSelector.SelectedIndex = _cardBackSelector.Items.FindIndex(Game.Settings.CardBackSide);
            _cardBackSelector.SelectionChanged += CardBackChanged;
            if (_cardBackSelector.SelectedIndex < 0)
            {
                _cardBackSelector.SelectedIndex = 0;
            }
            _thinkingTime = new Label(this)
            {
                Position = new Vector2(200, 310),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Jak dlouho AI přemýšlí",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _thinkingTimeSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 310),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Krátce", 750 }, { "Středně", 1500 }, { "Dlouho", 2500 } },
                UseCommonScissorRect = true
            };
            _thinkingTimeSelector.SelectedIndex = _thinkingTimeSelector.Items.FindIndex(Game.Settings.ThinkingTimeMs);
            _thinkingTimeSelector.SelectionChanged += ThinkingTimeChanged;
            if (_thinkingTimeSelector.SelectedIndex < 0)
            {
                _thinkingTimeSelector.SelectedIndex = 1;
            }
            _baseBet = new Label(this)
            {
                Position = new Vector2(200, 370),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
#if __IOS__
                Text = "Základní sazba pro hru",
#else
                Text = "Základní sazba",
#endif
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
#if __IOS__
            _baseBetSelector = new LeftRightSelector(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 300, 370),
				Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "0,01", 0.01f}, { "0,02", 0.02f}, { "0,05", 0.05f}, { "0,10", 0.1f}, { "0,20", 0.2f}, { "0,50", 0.5f }, { "1", 1f }, { "2", 2f}, { "5", 5f}, { "10", 10f} },
                UseCommonScissorRect = true
			};
            _locales = new[]
            {
                new Tuple<string, int, int>("cs-CZ", 0, 10),
                new Tuple<string, int, int>("sk-SK", 0, 10)
            };
#else
            _baseBetSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, 370),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "0,10 Kč", 0.1f}, { "0,20 Kč", 0.2f}, { "0,50 Kč", 0.5f }, { "1 Kč", 1f }, { "2 Kč", 2f}, { "5 Kč", 5f}, { "10 Kč", 10f},
                                              { "0,01 €", 0.01f}, { "0,02 €", 0.02f}, { "0,05 €", 0.05f}, { "0,10 €", 0.1f}, { "0,20 €", 0.2f}, { "0,50 €", 0.5f}, { "1 €", 1f} },
                UseCommonScissorRect = true
            };
            _locales = new[]
            {
                new Tuple<string, int, int>("cs-CZ", 0, 6),
                new Tuple<string, int, int>("sk-SK", 7, 13)
            };
#endif
            var startIndex = _locales.First(i => i.Item1 == Game.Settings.Locale).Item2;
            _baseBetSelector.SelectedIndex = _baseBetSelector.Items.FindIndex(startIndex, i => (i.Value as float?) == Game.Settings.BaseBet);
            _baseBetSelector.SelectionChanged += BaseBetChanged;
            #endregion
            #region Page 2
            _minBidsForGame = new Label(this)
            {
                Position = new Vector2(200, page2Offset + 10),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Hru hrát",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _minBidsForGameSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page2Offset + 10),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Vždy", 0 }, { "Jen po fleku", 1 }, { "Jen po re", 2 } },
                UseCommonScissorRect = true
            };
            _minBidsForGameSelector.SelectedIndex = _minBidsForGameSelector.Items.FindIndex(Game.Settings.MinimalBidsForGame);
            _minBidsForGameSelector.SelectionChanged += MinBidsForGameChanged;
            if (_minBidsForGameSelector.SelectedIndex < 0)
            {
                _minBidsForGameSelector.SelectedIndex = 0;
            }
            _minBidsForSeven = new Label(this)
            {
                Position = new Vector2(200, page2Offset + 70),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Sedmu hrát",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _minBidsForSevenSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page2Offset + 70),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Vždy", 0 }, { "Jen po fleku", 1 }, { "Jen po re", 2 } },
                UseCommonScissorRect = true
            };
            _minBidsForSevenSelector.SelectedIndex = _minBidsForSevenSelector.Items.FindIndex(Game.Settings.MinimalBidsForSeven);
            _minBidsForSevenSelector.SelectionChanged += MinBidsForSevenChanged;
            if (_minBidsForSevenSelector.SelectedIndex < 0)
            {
                _minBidsForSevenSelector.SelectedIndex = 0;
            }
            _kiloCounting = new Label(this)
            {
                Position = new Vector2(200, page2Offset + 130),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Počítání peněz u kila",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _kiloCountingSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page2Offset + 130),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Sčítat", CalculationStyle.Adding }, { "Násobit", CalculationStyle.Multiplying } },
                UseCommonScissorRect = true
            };
            _kiloCountingSelector.SelectedIndex = _kiloCountingSelector.Items.FindIndex(Game.Settings.CalculationStyle);
            _kiloCountingSelector.SelectionChanged += KiloCountingChanged;
            if (_kiloCountingSelector.SelectedIndex < 0)
            {
                _kiloCountingSelector.SelectedIndex = 0;
            }
            _top107 = new Label(this)
            {
                Position = new Vector2(200, page2Offset + 190),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Hrát stosedm z ruky",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _top107Selector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page2Offset + 190),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Ne", false }, { "Ano", true } },
                UseCommonScissorRect = true
            };
            _top107Selector.SelectedIndex = _top107Selector.Items.FindIndex(Game.Settings.Top107);
            _top107Selector.SelectionChanged += Top107Changed;
            if (_top107Selector.SelectedIndex < 0)
            {
                _top107Selector.SelectedIndex = 0;
            }
            _calculate107Separately = new Label(this)
            {
                Position = new Vector2(200, page2Offset + 250),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Kilo a sedmu počítat",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _calculate107Selector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page2Offset + 250),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Zvlášť", true }, { "Dohromady", false } },
                UseCommonScissorRect = true
            };
            _calculate107Selector.SelectedIndex = _calculate107Selector.Items.FindIndex(Game.Settings.Calculate107Separately);
            _calculate107Selector.SelectionChanged += Calculate107Changed;
            if (_calculate107Selector.SelectedIndex < 0)
            {
                _calculate107Selector.SelectedIndex = 1;
            }
            _hlasConsidered = new Label(this)
            {
                Position = new Vector2(200, page2Offset + 310),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Jaký hlas počítat do kila",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _hlasSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page2Offset + 310),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Největší", HlasConsidered.Highest }, { "První", HlasConsidered.First }, { "Každý", HlasConsidered.Each } },
                UseCommonScissorRect = true
            };
            _hlasSelector.SelectedIndex = _hlasSelector.Items.FindIndex(Game.Settings.HlasConsidered);
            _hlasSelector.SelectionChanged += HlasChanged;
            if (_hlasSelector.SelectedIndex < 0)
            {
                _hlasSelector.SelectedIndex = 0;
            }
            _playZeroSumGames = new Label(this)
            {
                Position = new Vector2(200, page2Offset + 370),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Sedma + flek na hru se",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _playZeroSumGamesSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page2Offset + 370),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Nehraje", false }, { "Hraje", true } },
                UseCommonScissorRect = true
            };
            _playZeroSumGamesSelector.SelectedIndex = _playZeroSumGamesSelector.Items.FindIndex(Game.Settings.PlayZeroSumGames);
            _playZeroSumGamesSelector.SelectionChanged += PlayZeroSumGamesChanged;
            if (_playZeroSumGamesSelector.SelectedIndex < 0)
            {
                _playZeroSumGamesSelector.SelectedIndex = 0;
            }

            _fakeSeven = new Label(this)
            {
                Position = new Vector2(200, page2Offset + 430),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Hlásit falešnou sedmu",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _fakeSevenSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page2Offset + 430),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Zakázáno", false }, { "Povoleno", true } },
                UseCommonScissorRect = true
            };
            _fakeSevenSelector.SelectedIndex = _fakeSevenSelector.Items.FindIndex(Game.Settings.AllowFakeSeven);
            _fakeSevenSelector.SelectionChanged += FakeSevenChanged;
            if (_fakeSevenSelector.SelectedIndex < 0)
            {
                _fakeSevenSelector.SelectedIndex = 0;
            }
            #endregion
            #region Page 3
            _allowAXTalon = new Label(this)
            {
                Position = new Vector2(200, page3Offset + 10),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Ostré karty v talonu",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _allowAXTalonSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page3Offset + 10),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Povolit", true }, { "Zakázat", false } },
                UseCommonScissorRect = true
            };
            _allowAXTalonSelector.SelectedIndex = _allowAXTalonSelector.Items.FindIndex(Game.Settings.AllowAXTalon);
            _allowAXTalonSelector.SelectionChanged += AllowAXTalonChanged;
            if (_allowAXTalonSelector.SelectedIndex < 0)
            {
                _allowAXTalonSelector.SelectedIndex = 0;
            }
            _allowTrumpTalon = new Label(this)
            {
                Position = new Vector2(200, page3Offset + 70),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Trumfy v talonu",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _allowTrumpTalonSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page3Offset + 70),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Povolit", true }, { "Zakázat", false } },
                UseCommonScissorRect = true
            };
            _allowTrumpTalonSelector.SelectedIndex = _allowTrumpTalonSelector.Items.FindIndex(Game.Settings.AllowTrumpTalon);
            _allowTrumpTalonSelector.SelectionChanged += AllowTrumpTalonChanged;
            if (_allowTrumpTalonSelector.SelectedIndex < 0)
            {
                _allowTrumpTalonSelector.SelectedIndex = 0;
            }
            _autoDisable100Against = new Label(this)
            {
                Position = new Vector2(200, page3Offset + 130),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Hlásit kilo proti lze",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _autoDisable100AgainstSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page3Offset + 130),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Jen s hlasem", true }, { "Vždy", false } },
                UseCommonScissorRect = true
            };
            _autoDisable100AgainstSelector.SelectedIndex = _autoDisable100AgainstSelector.Items.FindIndex(Game.Settings.AutoDisable100Against);
            _autoDisable100AgainstSelector.SelectionChanged += AutoDisable100AgainstChanged;
            if (_autoDisable100AgainstSelector.SelectedIndex < 0)
            {
                _autoDisable100AgainstSelector.SelectedIndex = 0;
            }
            _aiMayGiveUp = new Label(this)
            {
                Position = new Vector2(200, page3Offset + 190),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "AI může hru zahodit",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _aiMayGiveUpSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page3Offset + 190),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Ano", true }, { "Ne", false } },
                UseCommonScissorRect = true
            };
            _aiMayGiveUpSelector.SelectedIndex = _aiMayGiveUpSelector.Items.FindIndex(Game.Settings.AiMayGiveUp);
            _aiMayGiveUpSelector.SelectionChanged += AiMayGiveUpChanged;
            if (_aiMayGiveUpSelector.SelectedIndex < 0)
            {
                _aiMayGiveUpSelector.SelectedIndex = 0;
            }
            _allowAIAutoFinish = new Label(this)
            {
                Position = new Vector2(200, page3Offset + 250),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "AI ložené hry",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _allowAIAutoFinishSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page3Offset + 250),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Nedohrává", true }, { "Dohrává", false } },
                UseCommonScissorRect = true
            };
            _allowAIAutoFinishSelector.SelectedIndex = _allowAIAutoFinishSelector.Items.FindIndex(Game.Settings.AllowAIAutoFinish);
            _allowAIAutoFinishSelector.SelectionChanged += AllowAIAutoFinishChanged;
            if (_allowAIAutoFinishSelector.SelectedIndex < 0)
            {
                _allowAIAutoFinishSelector.SelectedIndex = 0;
            }
            _allowPlayerAutoFinish = new Label(this)
            {
                Position = new Vector2(200, page3Offset + 310),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Hráč ložené hry",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _allowPlayerAutoFinishSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page3Offset + 310),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Nedohrává", true }, { "Dohrává", false } },
                UseCommonScissorRect = true
            };
            _allowPlayerAutoFinishSelector.SelectedIndex = _allowPlayerAutoFinishSelector.Items.FindIndex(Game.Settings.AllowPlayerAutoFinish);
            _allowPlayerAutoFinishSelector.SelectionChanged += AllowPlayerAutoFinishChanged;
            if (_allowPlayerAutoFinishSelector.SelectedIndex < 0)
            {
                _allowPlayerAutoFinishSelector.SelectedIndex = 0;
            }
            _whenToShuffle = new Label(this)
            {
                Position = new Vector2(200, page3Offset + 370),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Karty míchat",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _whenToShuffleSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page3Offset + 370),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Nikdy", ShuffleTrigger.Never }, { "Po ložené hrře", ShuffleTrigger.AfterAutomaticVictory }, { "Po každé hře", ShuffleTrigger.Always } },
                UseCommonScissorRect = true
            };
            _whenToShuffleSelector.SelectedIndex = _whenToShuffleSelector.Items.FindIndex(Game.Settings.WhenToShuffle);
            _whenToShuffleSelector.SelectionChanged += WhenToShuffleChanged;
            if (_whenToShuffleSelector.SelectedIndex < 0)
            {
                _whenToShuffleSelector.SelectedIndex = 0;
            }
            #endregion
            #region Page 4
            _bgImage = new Label(this)
            {
                Position = new Vector2(200, page4Offset + 10),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Pozadí při hře",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _bgImageSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page4Offset + 10),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Výchozí", BackgroundImage.Default }, { "Plátno", BackgroundImage.Canvas }, { "Tmavé", BackgroundImage.Dark } },
                UseCommonScissorRect = true
            };
            _bgImageSelector.SelectedIndex = _bgImageSelector.Items.FindIndex(Game.Settings.BackgroundImage);
            _bgImageSelector.SelectionChanged += BackgroundImageChanged;
            if (_bgImageSelector.SelectedIndex < 0)
            {
                _bgImageSelector.SelectedIndex = 0;
            }
            _cardSize = new Label(this)
            {
                Position = new Vector2(200, page4Offset + 70),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Velikost karet",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _cardSizeSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page4Offset + 70),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Standardní", 0.6f }, { "Větší", 0.7f }, { "Menší", 0.5f } },
                UseCommonScissorRect = true
            };
            _cardSizeSelector.SelectedIndex = _cardSizeSelector.Items.FindIndex(Game.Settings.CardScaleFactor);
            _cardSizeSelector.SelectionChanged += CardSizeChanged;
            if (_cardSizeSelector.SelectedIndex < 0)
            {
                _cardSizeSelector.SelectedIndex = 0;
            }
            _handSorting = new Label(this)
            {
                Position = new Vector2(200, page4Offset + 130),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Jak řadit karty",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _handSortingSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page4Offset + 130),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Vzestupně", SortMode.Ascending }, { "Sestupně", SortMode.Descending }, { "Jen barvy", SortMode.SuitsOnly }, { "Vůbec", SortMode.None } },
                UseCommonScissorRect = true
            };
            _handSortingSelector.SelectedIndex = _handSortingSelector.Items.FindIndex(Game.Settings.SortMode);
            _handSortingSelector.SelectionChanged += SortModeChanged;
            if (_handSortingSelector.SelectedIndex < 0)
            {
                _handSortingSelector.SelectedIndex = 0;
            }
            _autoSorting = new Label(this)
            {
                Position = new Vector2(200, page4Offset + 190),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Kdy řadit karty",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _autoSortingSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page4Offset + 190),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Automaticky", true }, { "Poklepem", false } },
                UseCommonScissorRect = true
            };
            _autoSortingSelector.SelectedIndex = _autoSortingSelector.Items.FindIndex(Game.Settings.AutoSort);
            _autoSortingSelector.SelectionChanged += AutoSortChanged;
            if (_autoSortingSelector.SelectedIndex < 0)
            {
                _autoSortingSelector.SelectedIndex = 0;
            }
            _autoFinishRounds = new Label(this)
            {
                Position = new Vector2(200, page4Offset + 250),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Ukončení kola",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _autoFinishRoundsSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page4Offset + 250),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Automaticky", true }, { "Dotykem", false } },
                UseCommonScissorRect = true
            };
            _autoFinishRoundsSelector.SelectedIndex = _autoFinishRoundsSelector.Items.FindIndex(Game.Settings.AutoFinishRounds);
            _autoFinishRoundsSelector.SelectionChanged += AutoFinishRoundsChanged;
            if (_autoFinishRoundsSelector.SelectedIndex < 0)
            {
                _autoFinishRoundsSelector.SelectedIndex = 0;
            }
            _roundFinishedWaitTime = new Label(this)
            {
                Position = new Vector2(200, page4Offset + 310),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Prodleva na konci kola",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _roundFinishedWaitTimeSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page4Offset + 310),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Krátká", 1000 }, { "Dlouhá", 2000 } },
                UseCommonScissorRect = true
            };
            _roundFinishedWaitTimeSelector.SelectedIndex = _roundFinishedWaitTimeSelector.Items.FindIndex(Game.Settings.RoundFinishedWaitTimeMs);
            _roundFinishedWaitTimeSelector.SelectionChanged += RoundFinishedWaitTimeChanged;
            if (_roundFinishedWaitTimeSelector.SelectedIndex < 0)
            {
                _roundFinishedWaitTimeSelector.SelectedIndex = 0;
            }
            _autoFinishLastRound = new Label(this)
            {
                Position = new Vector2(200, page4Offset + 370),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Poslední kolo sehrát",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _autoFinishLastRoundSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page4Offset + 370),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Automaticky", true }, { "Ručně", false } },
                UseCommonScissorRect = true
            };
            _autoFinishLastRoundSelector.SelectedIndex = _autoFinishLastRoundSelector.Items.FindIndex(Game.Settings.AutoFinish);
            _autoFinishLastRoundSelector.SelectionChanged += AutoFinishLastRoundChanged;
            if (_autoFinishLastRoundSelector.SelectedIndex < 0)
            {
                _autoFinishLastRoundSelector.SelectedIndex = 0;
            }
            _autoPlaySingletonCard = new Label(this)
            {
                Position = new Vector2(200, page4Offset + 430),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Jedinou možnou kartu hrát",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _autoPlaySingletonCardSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page4Offset + 430),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Automaticky", true }, { "Ručně", false } },
                UseCommonScissorRect = true
            };
            _autoPlaySingletonCardSelector.SelectedIndex = _autoPlaySingletonCardSelector.Items.FindIndex(Game.Settings.AutoFinish);
            _autoPlaySingletonCardSelector.SelectionChanged += AutoPlaySingletonCardChanged;
            if (_autoPlaySingletonCardSelector.SelectedIndex < 0)
            {
                _autoPlaySingletonCardSelector.SelectedIndex = 0;
            }
            #endregion
            #region Page 5
            _player1 = new Label(this)
            {
                Position = new Vector2(200, page5Offset + 10),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Jméno hráče č.1",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _player1Name = new Button(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page5Offset + 10),
                Width = 270,
                Height = 50,
                Group = 1,
                Text = Game.Settings.PlayerNames[0],
                Tag = 1,
                BorderColor = Color.Transparent,
                BackgroundColor = Color.Transparent,
                UseCommonScissorRect = true
            };
            _player1Name.Click += ChangePlayerName;
            _player2 = new Label(this)
            {
                Position = new Vector2(200, page5Offset + 70),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Jméno hráče č.2",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _player2Name = new Button(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page5Offset + 70),
                Width = 270,
                Height = 50,
                Group = 1,
                Text = Game.Settings.PlayerNames[1],
                Tag = 2,
                BorderColor = Color.Transparent,
                BackgroundColor = Color.Transparent,
                UseCommonScissorRect = true
            };
            _player2Name.Click += ChangePlayerName;
            _player3 = new Label(this)
            {
                Position = new Vector2(200, page5Offset + 130),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Jméno hráče č.3",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _player3Name = new Button(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page5Offset + 130),
                Width = 270,
                Height = 50,
                Group = 1,
                Text = Game.Settings.PlayerNames[2],
                Tag = 3,
                BorderColor = Color.Transparent,
                BackgroundColor = Color.Transparent,
                UseCommonScissorRect = true
            };
            _player3Name.Click += ChangePlayerName;
            _showScoreDuringGame = new Label(this)
            {
                Position = new Vector2(200, page5Offset + 190),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Skóre zobrazovat",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _showScoreDuringGameSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page5Offset + 190),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Stále", 3 }, { "Stále, barevně", 1 }, { "Na konci hry", 0 } },
                UseCommonScissorRect = true
            };
            var val = (Game.Settings.ShowScoreDuringGame ? 1 : 0) + (Game.Settings.WhiteScore ? 1 : 0) * 2;
            _showScoreDuringGameSelector.SelectedIndex = _showScoreDuringGameSelector.Items.FindIndex(val);
            _showScoreDuringGameSelector.SelectionChanged += ShowScoreDuringGameChanged;
            if (_showScoreDuringGameSelector.SelectedIndex < 0)
            {
                _showScoreDuringGameSelector.SelectedIndex = 0;
            }
            _bubbleTime = new Label(this)
            {
                Position = new Vector2(200, page5Offset + 250),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Rychlost dialogů",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _bubbleTimeSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page5Offset + 250),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Rychlá", 500 }, { "Střední", 1000 }, { "Pomalá", 2000 } },
                UseCommonScissorRect = true
            };
            _bubbleTimeSelector.SelectedIndex = _bubbleTimeSelector.Items.FindIndex(Game.Settings.BubbleTimeMs);
            _bubbleTimeSelector.SelectionChanged += BubbleTimeChanged;
            if (_bubbleTimeSelector.SelectedIndex < 0)
            {
                _bubbleTimeSelector.SelectedIndex = 1;
            }
            _directionOfPlay = new Label(this)
            {
                Position = new Vector2(200, page5Offset + 310),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Směr hraní",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _directionOfPlaySelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page5Offset + 310),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "Po směru hodin", DirectionOfPlay.Clockwise }, { "Proti směru hod", DirectionOfPlay.Counterclockwise } },
                UseCommonScissorRect = true
            };
            _directionOfPlaySelector.SelectedIndex = _directionOfPlaySelector.Items.FindIndex(Game.Settings.DirectionOfPlay);
            _directionOfPlaySelector.SelectionChanged += DirectionOfPlayChanged;
            if (_directionOfPlaySelector.SelectedIndex < 0)
            {
                _directionOfPlaySelector.SelectedIndex = 0;
            }
            _maxWin = new Label(this)
            {
                Position = new Vector2(200, page5Offset + 370),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Text = "Maximální sazba za hru",
                Group = 1,
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _maxWinSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page5Offset + 370),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "500x základ", 500 }, { "Neomezená", 0 } },
                UseCommonScissorRect = true
            };
            _maxWinSelector.SelectedIndex = _maxWinSelector.Items.FindIndex(Game.Settings.MaxWin);
            _maxWinSelector.SelectionChanged += MaxWinChanged;
            if (_maxWinSelector.SelectedIndex < 0)
            {
                _maxWinSelector.SelectedIndex = 1;
            }
            //_maxHistoryLength = new Label(this)
            //{
            //    Position = new Vector2(200, page5Offset + 370),
            //    Width = (int)Game.VirtualScreenWidth / 2 - 150,
            //    Height = 50,
            //    Text = "Historii mazat",
            //    Group = 1,
            //    HorizontalAlign = HorizontalAlignment.Center,
            //    VerticalAlign = VerticalAlignment.Middle
            //};
            //_maxHistoryLengthSelector = new LeftRightSelector(this)
            //{
            //    Position = new Vector2(Game.VirtualScreenWidth - 300, page5Offset + 370),
            //    Width = 270,
            //    Group = 1,
            //    Items = new SelectorItems() { { "Ručně", 0 }, { "Po 1000 hrách", 1000 }, { "Po 5000 hrách", 5000 }, { "Po 10000 hrách", 10000 } }
            //};
            //_maxHistoryLengthSelector.SelectedIndex = _maxHistoryLengthSelector.Items.FindIndex(Game.Settings.MaxHistoryLength);
            //_maxHistoryLengthSelector.SelectionChanged += MaxHistoryLengthChanged;
            //if (_maxHistoryLengthSelector.SelectedIndex < 0)
            //{
            //    _maxHistoryLengthSelector.SelectedIndex = 0;
            //}
            #endregion
            #region Page 6
            _gameValue = new Label(this)
            {
                Position = new Vector2(200, page6Offset + 10),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Hodnota hry",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _gameValueSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page6Offset + 10),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "1x základ", 1 }, { "2x základ", 2 } },
                UseCommonScissorRect = true
            };
            _gameValueSelector.SelectedIndex = _gameValueSelector.Items.FindIndex(Game.Settings.GameValue);
            _gameValueSelector.SelectionChanged += GameValueChanged;
            if (_gameValueSelector.SelectedIndex < 0)
            {
                _gameValueSelector.SelectedIndex = 0;
            }
            _quietSevenValue = new Label(this)
            {
                Position = new Vector2(200, page6Offset + 70),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Hodnota tiché sedmy",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _quietSevenValueSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page6Offset + 70),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "1x základ", 1 }, { "2x základ", 2 }, { "3x základ", 3 }, { "4x základ", 4 } },
                UseCommonScissorRect = true
            };
            _quietSevenValueSelector.SelectedIndex = _quietSevenValueSelector.Items.FindIndex(Game.Settings.QuietSevenValue);
            _quietSevenValueSelector.SelectionChanged += QuietSevenValueChanged;
            if (_quietSevenValueSelector.SelectedIndex < 0)
            {
                _quietSevenValueSelector.SelectedIndex = 0;
            }
            _sevenValue = new Label(this)
            {
                Position = new Vector2(200, page6Offset + 130),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Hodnota sedmy",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _sevenValueSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page6Offset + 130),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "1x základ", 1 }, { "2x základ", 2 }, { "3x základ", 3 }, { "4x základ", 4 } },
                UseCommonScissorRect = true
            };
            _sevenValueSelector.SelectedIndex = _sevenValueSelector.Items.FindIndex(Game.Settings.SevenValue);
            _sevenValueSelector.SelectionChanged += SevenValueChanged;
            if (_sevenValueSelector.SelectedIndex < 0)
            {
                _sevenValueSelector.SelectedIndex = 1;
            }
            _quietHundredValue = new Label(this)
            {
                Position = new Vector2(200, page6Offset + 190),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Hodnota tichého kila",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _quietHundredValueSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page6Offset + 190),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "2x základ", 2 }, { "3x základ", 3 }, { "4x základ", 4 } },
                UseCommonScissorRect = true
            };
            _quietHundredValueSelector.SelectedIndex = _quietHundredValueSelector.Items.FindIndex(Game.Settings.QuietHundredValue);
            _quietHundredValueSelector.SelectionChanged += QuietHundredValueChanged;
            if (_quietHundredValueSelector.SelectedIndex < 0)
            {
                _quietHundredValueSelector.SelectedIndex = 0;
            }
            _hundredValue = new Label(this)
            {
                Position = new Vector2(200, page6Offset + 250),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Hodnota kila",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _hundredValueSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page6Offset + 250),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "2x základ", 2 }, { "4x základ", 4 }, { "6x základ", 6 }, { "8x základ", 8 } },
                UseCommonScissorRect = true
            };
            _hundredValueSelector.SelectedIndex = _hundredValueSelector.Items.FindIndex(Game.Settings.HundredValue);
            _hundredValueSelector.SelectionChanged += HundredValueChanged;
            if (_hundredValueSelector.SelectedIndex < 0)
            {
                _hundredValueSelector.SelectedIndex = 1;
            }
            _betlValue = new Label(this)
            {
                Position = new Vector2(200, page6Offset + 310),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Hodnota betla",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _betlValueSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page6Offset + 310),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "5x základ", 5 }, { "10x základ", 10 }, { "15x základ", 15 } },
                UseCommonScissorRect = true
            };
            _betlValueSelector.SelectedIndex = _betlValueSelector.Items.FindIndex(Game.Settings.BetlValue);
            _betlValueSelector.SelectionChanged += BetlValueChanged;
            if (_betlValueSelector.SelectedIndex < 0)
            {
                _betlValueSelector.SelectedIndex = 0;
            }
            _durchValue = new Label(this)
            {
                Position = new Vector2(200, page6Offset + 370),
                Width = (int)Game.VirtualScreenWidth / 2 - 150,
                Height = 50,
                Group = 1,
                Text = "Hodnota durcha",
                HorizontalAlign = HorizontalAlignment.Center,
                VerticalAlign = VerticalAlignment.Middle,
                UseCommonScissorRect = true
            };
            _durchValueSelector = new LeftRightSelector(this)
            {
                Position = new Vector2(Game.VirtualScreenWidth - 300, page6Offset + 370),
                Width = 270,
                Group = 1,
                Items = new SelectorItems() { { "10x základ", 10 }, { "20x základ", 20 }, { "30x základ", 30 } },
                UseCommonScissorRect = true
            };
            _durchValueSelector.SelectedIndex = _durchValueSelector.Items.FindIndex(Game.Settings.DurchValue);
            _durchValueSelector.SelectionChanged += DurchValueChanged;
            if (_durchValueSelector.SelectedIndex < 0)
            {
                _durchValueSelector.SelectedIndex = 0;
            }
            #endregion

            BoundsRect = new Rectangle(0, 0, (int)Game.VirtualScreenWidth - (int)Position.X, boundsRectHeight - (int)Position.Y);
        }

        private void HandleTouchDown(object sender, TouchLocation tl)
        {
        }

        private void HandleClick(object sender)
        {
        }

        private void ShowScoreDuringGameChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.ShowScoreDuringGame = (((int)selector.SelectedValue) & 1) > 0;
            Game.Settings.WhiteScore = (((int)selector.SelectedValue) & 2) > 0;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private void CardSizeChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.CardScaleFactor = (float)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private void AutoDisable100AgainstChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.AutoDisable100Against = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void HintBtnClick(object sender)
        {
            var btn = sender as ToggleButton;

            Game.Settings.HintEnabled = btn.IsSelected;
            _hintBtn.Text = Game.Settings.HintEnabled ? "Zapnutá" : "Vypnutá";
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void SoundBtnClick(object sender)
        {
            var btn = sender as ToggleButton;

            Game.Settings.SoundEnabled = btn.IsSelected;
            _soundBtn.Text = Game.Settings.SoundEnabled ? "Zapnutý" : "Vypnutý";
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void BgSoundBtnClick(object sender)
        {
            var btn = sender as ToggleButton;

            Game.Settings.BgSoundEnabled = btn.IsSelected;
            _bgsoundBtn.Text = Game.Settings.BgSoundEnabled ? "Zapnutý" : "Vypnutý";
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void SortModeChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.SortMode = (SortMode)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void AutoSortChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.AutoSort = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void BaseBetChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.BaseBet = (float)selector.SelectedValue;
            Game.Settings.Locale = _locales.First(i => selector.SelectedIndex >= i.Item2 &&
                                                       selector.SelectedIndex <= i.Item3)
                                           .Item1;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void ThinkingTimeChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.ThinkingTimeMs = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void KiloCountingChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.CalculationStyle = (CalculationStyle)selector.SelectedValue;
            if (Game.Settings.Default.HasValue &&
                Game.Settings.Default.Value)
            {
                Game.Settings.ResetThresholds();
            }
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void CardFaceChanged(object sender)
        {
            var selector = sender as LeftRightSelector;
            var origValue = Game.Settings.CardDesign;

            Game.Settings.CardDesign = (CardFace)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void CardBackChanged(object sender)
        {
            var selector = sender as LeftRightSelector;
            var origValue = Game.Settings.CardBackSide;

            Game.Settings.CardBackSide = (CardBackSide)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void AutoFinishRoundsChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.AutoFinishRounds = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void AutoFinishLastRoundChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.AutoFinish = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void AutoPlaySingletonCardChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.AutoPlaySingletonCard = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void RoundFinishedWaitTimeChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.RoundFinishedWaitTimeMs = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void PlayZeroSumGamesChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.PlayZeroSumGames = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void FakeSevenChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.AllowFakeSeven = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void Top107Changed(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.Top107 = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void Calculate107Changed(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.Calculate107Separately = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void HlasChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.HlasConsidered = (HlasConsidered)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void WhenToShuffleChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.WhenToShuffle = (ShuffleTrigger)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void DirectionOfPlayChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.DirectionOfPlay = (DirectionOfPlay)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void MaxHistoryLengthChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.MaxHistoryLength = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void AiMayGiveUpChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.AiMayGiveUp = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void BubbleTimeChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.BubbleTimeMs = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void MaxWinChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.MaxWin = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void ShowStatusBarChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.ShowStatusBar = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void BackgroundImageChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.BackgroundImage = (BackgroundImage)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void MinBidsForGameChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.MinimalBidsForGame = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        void MinBidsForSevenChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.MinimalBidsForSeven = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private void GameValueChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.GameValue = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private void QuietSevenValueChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.QuietSevenValue = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private void SevenValueChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.SevenValue = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private void QuietHundredValueChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.QuietHundredValue = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private void HundredValueChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.HundredValue = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private void BetlValueChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.BetlValue = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private void DurchValueChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.DurchValue = (int)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private void AllowAXTalonChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.AllowAXTalon = (bool)selector.SelectedValue;
            //obe nastaveni nemuzou byt false protoze by nemuselo zbyt dost karet ktere jdou dat do talonu
            if (!Game.Settings.AllowTrumpTalon && !Game.Settings.AllowAXTalon)
            {
                //toto zavola i SaveGameSettings() a OnSettingsChanged()
                _allowTrumpTalonSelector.SelectedIndex = _allowTrumpTalonSelector.Items.FindIndex(true);
                return;
            }
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private void AllowTrumpTalonChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.AllowTrumpTalon = (bool)selector.SelectedValue;
            //obe nastaveni nemuzou byt false protoze by nemuselo zbyt dost karet ktere jdou dat do talonu
            if (!Game.Settings.AllowTrumpTalon && !Game.Settings.AllowAXTalon)
            {
                //toto zavola i SaveGameSettings() a OnSettingsChanged()
                _allowAXTalonSelector.SelectedIndex = _allowAXTalonSelector.Items.FindIndex(true);
                return;
            }
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private void AllowAIAutoFinishChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.AllowAIAutoFinish = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private void AllowPlayerAutoFinishChanged(object sender)
        {
            var selector = sender as LeftRightSelector;

            Game.Settings.AllowPlayerAutoFinish = (bool)selector.SelectedValue;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }

        private int editedPlayerIndex;

        async void ChangePlayerName(object sender)
        {
            const int MaxNameLength = 12;
            var button = sender as Button;
            var playerIndex = (int)button.Tag;

            editedPlayerIndex = playerIndex;
            var text = await KeyboardInput.Show("Jméno hráče", $"Zadej jméno hráče č.{playerIndex}", Game.Settings.PlayerNames[playerIndex - 1]);

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            text = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).First().Trim();
            if (text.Length > MaxNameLength)
            {
                text = text.Substring(0, MaxNameLength);
            }
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (button != null)
            {
                button.Text = text;
            }
            Game.Settings.PlayerNames[playerIndex - 1] = text;
            Game.SaveGameSettings();
            Game.OnSettingsChanged();
        }
    }
}