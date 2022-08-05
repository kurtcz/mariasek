using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mariasek.Engine;
using Mariasek.SharedClient.GameComponents;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Mariasek.SharedClient
{
    public class EditorScene : Scene
    {
        private static string _editorPath = Path.Combine(MariasekMonoGame.RootPath, "Editor");
        private string _newGameFilePath = Path.Combine(MariasekMonoGame.RootPath, "_def.hra");

        private Button _menuButton;
        private Button _newGameButton;
        private Button _loadGameButton;
        private Button _saveGameButton;
        private Button _playGameButton;
        private ToggleButton _gameListButton;
        private Button _deleteGameButton;
        private Button _startingPlayerButton;
        private TextBox _gameListBox;
        private Button _sendBtn;

        private int _gameStartingPlayerIndex;
        private Label _fileLabel;
        private Label[] _labels;
        private CardButton[] _cards;
        private Vector2[] _cardPositions;

        private readonly Vector2 _editorCardScaleFactor = new Vector2(0.38f, 0.38f);
        private Vector2 _origPosition;
        private Vector2 _hiddenPosition;
        private string[] _files;
        private string _filename;
        private bool _loadGameCalled;

        public EditorScene(MariasekMonoGame game)
            : base(game)
        {
            SceneActivated += Activated;
        }

        public override void Initialize()
        {
            base.Initialize();

            _origPosition = new Vector2(220, 45);
            _hiddenPosition = new Vector2(Game.VirtualScreenWidth, 45);

            Background = Game.Assets.GetTexture("wood2");
            BackgroundTint = Color.DimGray;

            _menuButton = new Button(this)
            {
                Position = new Vector2(10, (int)Game.VirtualScreenHeight - 60),
                Width = 200,
                Height = 50,
                Text = "Menu",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _menuButton.Click += MenuClicked;
            _newGameButton = new Button(this)
            {
                Position = new Vector2(10, 10),
                Width = 200,
                Height = 50,
                Text = "Nová hra",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _newGameButton.Click += NewGameClicked;
            _gameListButton = new ToggleButton(this)
            {
                Position = new Vector2(10, 70),
                Width = 200,
                Height = 50,
                Text = "Uložené hry",
                IsSelected = false,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _gameListButton.Click += GameListClicked;
            _loadGameButton = new Button(this)
            {
                Position = new Vector2(10, 130),
                Width = 200,
                Height = 50,
                Text = "Zobrazit hru",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _loadGameButton.Click += LoadGameClicked;
            _playGameButton = new Button(this)
            {
                Position = new Vector2(10, 190),
                Width = 200,
                Height = 50,
                Text = "Sehrát hru",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _playGameButton.Click += PlayGameClicked;
            _saveGameButton = new Button(this)
            {
                Position = new Vector2(10, 250),
                Width = 200,
                Height = 50,
                Text = "Uložit hru",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _saveGameButton.Click += SaveGameClicked;
            _deleteGameButton = new Button(this)
            {
                Position = new Vector2(10, 310),
                Width = 200,
                Height = 50,
                Text = "Smazat hru",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _deleteGameButton.Click += DeleteGameClicked;
            _startingPlayerButton = new Button(this)
            {
                Position = new Vector2(10, 370),
                Width = 200,
                Height = 50,
                Text = "Volí: ",
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Main
            };
            _startingPlayerButton.Click += StartingPlayerClicked;

            _gameListBox = new TextBox(this)
            {
                Position = _hiddenPosition,
                Width = (int)Game.VirtualScreenWidth - 230,
                Height = (int)Game.VirtualScreenHeight - 120,
                HorizontalAlign = HorizontalAlignment.Left,
                VerticalAlign = VerticalAlignment.Bottom,
                Text = "",
                Tabs = new[]
                {
                    new Tab
                    {
                        TabPosition = 440,
                        TabAlignment = HorizontalAlignment.Left
                    }
                },
                FontScaleFactor = 0.9f,
                HighlightColor = Game.Settings.HighlightedTextColor,
                TapToHighlight = true
            };
            _gameListBox.Hide();

            _fileLabel = new Label(this)
            {
                Position = new Vector2(220, Game.VirtualScreenHeight - 50),
                Width = (int)Game.VirtualScreenWidth - 230,
                Height = 50,
                TextColor = Game.Settings.HighlightedTextColor,
                Tabs = new[]
                {
                    new Tab
                    {
                        TabPosition = 440,
                        TabAlignment = HorizontalAlignment.Left
                    }
                }
            };
            //tlacitka na prave strane
            _sendBtn = new Button(this)
            {
                Text = "@",
                Position = new Vector2(Game.VirtualScreenWidth - 60, Game.VirtualScreenHeight / 2f - 25),
                ZIndex = 100,
                Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Right : AnchorType.Main,
                Width = 50
            };
            _sendBtn.Click += SendBtnClicked;

            NewGameClicked(this);
            PopulateGameList(true);
        }

        private void Activated(object sender)
        {
            var newTextures = Game.Settings.CardDesign == CardFace.Single
                                ? Game.CardTextures1
                                : Game.Settings.CardDesign == CardFace.Double
                                    ? Game.CardTextures2
                                    : Game.CardTextures3;

            foreach(var c in _cards.Where(i => i != null &&
                                               i.Sprite != null &&
                                               i.Sprite.Texture != Game.CardTextures))
            {
                c.Sprite.Texture = newTextures;
            }
            Game.MainScene.UpdateToggleButtons(this);
        }

        private void SendBtnClicked(object sender)
        {
            if (Game.EmailSender != null)
            {
                var saveGamePath = _filename != null ? Path.Combine(_editorPath, $"{_filename}.hra") : Path.Combine(MariasekMonoGame.RootPath, "_editor.hra");
                var subject = $"Mariášek: komentář v{MariasekMonoGame.Version} ({MariasekMonoGame.Platform})";

                SaveGame(saveGamePath);
                Game.EmailSender.SendEmail(new[] { "mariasek.app@gmail.com" }, subject, "Napište svůj komentář k této hře\n:",
                                            new[] { saveGamePath, SettingsScene._settingsFilePath });
            }
        }

        private void PopulateCards(IEnumerable<Card> cards)
        {
            var i = 0;

            if (_cards == null)
            {
                _cards = new CardButton[cards.Count()];
            }
            _cardPositions = new Vector2[cards.Count()];

            foreach (var c in cards)
            {
                _cardPositions[i] = new Vector2(220 + (i < 7 ? i+0.5f : i < 12 ? i + 0.9f : (i - 12) % 10 + 0.5f) * (GameComponents.Hand.CardWidth * _editorCardScaleFactor.X - 16),
                                                90 + (i < 12 ? 0 : i < 22 ? 1 : 2) * (GameComponents.Hand.CardHeight * _editorCardScaleFactor.Y + 50));
                if (_cards[i] == null)
                {
                    _cards[i] = new CardButton(this, new Sprite(this, Game.CardTextures) { Scale = _editorCardScaleFactor })
                    {
                        Name = $"Karta{i + 1}",
                        CanDrag = true,
                        MinimalDragDistance = 0
                    };
                    _cards[i].DragEnd += CardDragged;
                }
                else
                {
                    _cards[i].Sprite.Texture = Game.CardTextures;
                }
                _cards[i].Position = _cardPositions[i];
                _cards[i].Tag = c;
                _cards[i].ZIndex = i + 1;
                _cards[i].Sprite.SpriteRectangle = c.ToTextureRect();

                i++;
            }
        }

        private void PopulateLabels()
        {
            if (_labels == null)
            {
                _labels = new Label[Mariasek.Engine.Game.NumPlayers];
            }
            for (var i = 0; i < _labels.Length; i++)
            {
                if (_labels[i] == null)
                {
                    _labels[i] = new Label(this)
                    {
                        Position = new Vector2(220, i * (GameComponents.Hand.CardHeight * _editorCardScaleFactor.Y + 50)),
                        Width = 100,
                        Height = 30
                    };
                }
                _labels[i].Text = Game.Settings.PlayerNames[(_gameStartingPlayerIndex + i) % Mariasek.Engine.Game.NumPlayers];
            }

            _startingPlayerButton.Text = $"Volí: {Game.Settings.PlayerNames[_gameStartingPlayerIndex]}";
            var lines = _gameListBox.Text.Split('\n').ToArray();

            if (lines.Any() && _gameListBox.HighlightedLine >= 0 && _gameListBox.HighlightedLine < lines.Length)
            {
                _fileLabel.Text = lines[_gameListBox.HighlightedLine];
            }
            else
            {
                _fileLabel.Text = _filename ?? "";
            }
        }

        private void PopulateGameList(bool firstTime = false)
        {
            Task.Run(() =>
            {
                try
                {
                    Game.StorageAccessor.GetStorageAccess();
                    if (!Directory.Exists(_editorPath))
                    {
                        Directory.CreateDirectory(_editorPath);
                    }
                    var fileInfos = Directory.GetFiles(_editorPath, "*.hra")
                                             .Select(i => new FileInfo(i))
                                             .OrderBy(i => i.CreationTime)
                                             .ToArray();

                    _files = fileInfos.Select(i => i.FullName).ToArray();
                    _gameListBox.Text = string.Join('\n', fileInfos.Select(i => $"{i.CreationTime.ToString("dd.MM.yyyy HH:mm:ss")}\t{Path.GetFileNameWithoutExtension(i.FullName)}"));
                    _gameListBox.ScrollToBottom();
                    if (firstTime && !_loadGameCalled)
                    {
                        if (_files.Any())
                        {
                            _gameListButton.IsSelected = true;
                            _fileLabel.Text = "";
                            GameListClicked(this);
                        }
                        else
                        {
                            NewGameClicked(this);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _fileLabel.Text = ex.Message;
                }
            });
        }

        private void CardDragged(object sender, DragEndEventArgs e)
        {
            var cb = sender as CardButton;

            foreach (var card in _cards)
            {
                if (card == cb)
                {
                    continue;
                }
                if (cb.CollidesWithPosition(card.Position))
                {
                    var temp = (Card)cb.Tag;

                    cb.Tag = card.Tag;
                    card.Tag = temp;

                    card.Sprite.SpriteRectangle = new Card((Barva)(temp.Num / 8), (Hodnota)(temp.Num % 8)).ToTextureRect();
                    temp = (Card)cb.Tag;
                    cb.Sprite.SpriteRectangle = new Card((Barva)(temp.Num / 8), (Hodnota)(temp.Num % 8)).ToTextureRect();
                    break;
                }
            }

            cb.Position = cb.PreDragPosition;
        }

        private void MenuClicked(object sender)
        {
            Game.MenuScene.SetActive();
        }

        private void NewGameClicked(object sender)
        {
            var cards = Enum.GetValues(typeof(Barva)).Cast<Barva>()
                            .SelectMany(b => Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>()
                                                 .Select(h => new Card(b, h))
                                                 .OrderByDescending(i => i.Value))
                            .ToList();

            cards.InsertRange(8, cards.Skip(28).Take(4).ToList());
            cards.RemoveRange(32, 4);
            cards.InsertRange(20, cards.Skip(28).Take(2).ToList());
            cards.RemoveRange(30, 2);
            _gameStartingPlayerIndex = 0;
            _filename = null;
            _fileLabel.Text = "";
            PopulateCards(cards);
            PopulateLabels();
            ShowEditor();
        }

        private void GameListClicked(object sender)
        {
            if (_gameListButton.IsSelected)
            {
                PopulateGameList();
                ShowGameList();
            }
            else
            {
                ShowEditor();
            }
        }

        private void ShowGameList()
        {
            if (_cards != null)
            {
                foreach (var card in _cards)
                {
                    card.Hide();
                }
            }
            if (_labels != null)
            {
                foreach (var label in _labels)
                {
                    label.Hide();
                }
            }
            _gameListBox.Show();
            _gameListBox.MoveTo(_origPosition, 5000);
        }

        private void ShowEditor()
        {
            _gameListButton.IsSelected = false;
            _gameListBox.MoveTo(_hiddenPosition, 5000)
                        .Invoke(() =>
                        {
                            _gameListBox.Hide();
                            foreach (var card in _cards)
                            {
                                card.Show();
                            }
                            foreach (var label in _labels)
                            {
                                label.Show();
                            }
                        });
        }

        private void LoadGameClicked(object sender)
        {
            var file = _gameListBox.HighlightedLine >= 0 && _gameListBox.HighlightedLine < _files.Length ? _files[_gameListBox.HighlightedLine] : null;

            if (!string.IsNullOrEmpty(file))
            {
                LoadGame(file);
            }
        }

        public void LoadGame(string path)
        {
            Task.Run(() =>
            {
                try
                {
                    _filename = Path.GetFileNameWithoutExtension(path);
                    if (_filename == "_def")
                    {
                        _filename = null;
                    }
                    var g = new Mariasek.Engine.Game()
                    {
                        BaseBet = Game.Settings.BaseBet,
                        Locale = Game.Settings.Locale,
                        MaxWin = Game.Settings.MaxWin,
                        SkipBidding = false,
                        MinimalBidsForGame = Game.Settings.MinimalBidsForGame,
                        MinimalBidsForSeven = Game.Settings.MinimalBidsForSeven,
                        CalculationStyle = Game.Settings.CalculationStyle,
                        CountHlasAgainst = Game.Settings.CountHlasAgainst,
                        PlayZeroSumGames = Game.Settings.PlayZeroSumGames,
                        Top107 = Game.Settings.Top107,
                        Calculate107Separately = Game.Settings.Calculate107Separately,
                        HlasConsidered = Game.Settings.HlasConsidered,
                        AutoDisable100Against = Game.Settings.AutoDisable100Against,
                        //GetFileStream = GetFileStream,
                        //GetVersion = () => MariasekMonoGame.Version,
                        GameValue = Game.Settings.GameValue,
                        QuietSevenValue = Game.Settings.QuietSevenValue,
                        SevenValue = Game.Settings.SevenValue,
                        QuietHundredValue = Game.Settings.QuietHundredValue,
                        HundredValue = Game.Settings.HundredValue,
                        BetlValue = Game.Settings.BetlValue,
                        DurchValue = Game.Settings.DurchValue,
                        AllowFakeSeven = Game.Settings.AllowFakeSeven,
                        AllowFake107 = Game.Settings.AllowFake107,
                        AllowAXTalon = Game.Settings.AllowAXTalon,
                        AllowTrumpTalon = Game.Settings.AllowTrumpTalon,
                        AllowAIAutoFinish = Game.Settings.AllowAIAutoFinish,
                        AllowPlayerAutoFinish = Game.Settings.AllowPlayerAutoFinish,
                        OptimisticAutoFinish = Game.Settings.OptimisticAutoFinish
                    };
                    g.RegisterPlayers(new Engine.AbstractPlayer[]
                                      {
                                          new DummyPlayer(g) { Name = Game.Settings.PlayerNames[0] },
                                          new DummyPlayer(g) { Name = Game.Settings.PlayerNames[1] },
                                          new DummyPlayer(g) { Name = Game.Settings.PlayerNames[2] }
                                      });
                    try
                    {
                        Game.StorageAccessor.GetStorageAccess();
                        using (var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            g.LoadGame(fs);
                        }
                    }
                    catch (Exception ex)
                    {
                        _fileLabel.Text = ex.Message;
                        _gameListBox.ClearOperations();
                        GameListClicked(this);
                        return;
                    }
                    if (g.RoundNumber == 0)
                    {
                        _gameStartingPlayerIndex = g.GameStartingPlayerIndex;

                        var cards = new List<Card>();
                        for (var i = 0; i < Mariasek.Engine.Game.NumPlayers; i++)
                        {
                            var hand = g.players[(_gameStartingPlayerIndex + i) % Mariasek.Engine.Game.NumPlayers].Hand;

                            if (i > 0)
                            {
                                hand.Sort(Game.Settings.SortMode, Game.Settings.NaturalSort);
                            }
                            cards.AddRange(hand);
                        }

                        PopulateCards(cards);
                        PopulateLabels();
                        ShowEditor();
                        _loadGameCalled = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Unexpected error in ShowGame: {0}\n{1}", ex.Message, ex.StackTrace);
                    _fileLabel.Text = ex.Message;
                    _gameListBox.ClearOperations();
                    GameListClicked(this);
                }
            });
        }

        private void PlayGameClicked(object sender)
        {
            Task.Run(() =>
            {
                SaveGame(_newGameFilePath);
                Game.MainScene.LoadGame(_newGameFilePath, true);
            });
        }

        private async void SaveGameClicked(object sender)
        {
            if (KeyboardInput.IsVisible)
            {
                return;
            }
            const int MaxNameLength = 25;
            var filename = await KeyboardInput.Show("Uložit hru", "Zadej jméno hry", _filename ?? "Moje hra");

            if (string.IsNullOrWhiteSpace(filename))
            {
                return;
            }
            filename = filename.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).First().Trim();
            if (filename.Length > MaxNameLength)
            {
                filename = filename.Substring(0, MaxNameLength);
            }
            _filename = filename;

            var saveGamePath = Path.Combine(_editorPath, $"{_filename}.hra");

            if (File.Exists(saveGamePath) && !MessageBox.IsVisible)
            {
                var buttonIndex = await MessageBox.Show("Soubor již existuje", $"Přepsat hru {_filename}?", new string[] { "Storno", "OK" });

                if (!buttonIndex.HasValue || buttonIndex.Value != 1)
                {
                    return;
                }
            }
            SaveGame(saveGamePath);
        }

        private void SaveGame(string saveGamePath)
        {
            try
            {
                var g = new Mariasek.Engine.Game(gameStartingPlayerIndex: _gameStartingPlayerIndex)
                {
                    BaseBet = Game.Settings.BaseBet,
                    Locale = Game.Settings.Locale,
                    MaxWin = Game.Settings.MaxWin,
                    SkipBidding = false,
                    MinimalBidsForGame = Game.Settings.MinimalBidsForGame,
                    MinimalBidsForSeven = Game.Settings.MinimalBidsForSeven,
                    CalculationStyle = Game.Settings.CalculationStyle,
                    CountHlasAgainst = Game.Settings.CountHlasAgainst,
                    PlayZeroSumGames = Game.Settings.PlayZeroSumGames,
                    Top107 = Game.Settings.Top107,
                    Calculate107Separately = Game.Settings.Calculate107Separately,
                    HlasConsidered = Game.Settings.HlasConsidered,
                    AutoDisable100Against = Game.Settings.AutoDisable100Against,
                    //GetFileStream = GetFileStream,
                    //GetVersion = () => MariasekMonoGame.Version,
                    GameValue = Game.Settings.GameValue,
                    QuietSevenValue = Game.Settings.QuietSevenValue,
                    SevenValue = Game.Settings.SevenValue,
                    QuietHundredValue = Game.Settings.QuietHundredValue,
                    HundredValue = Game.Settings.HundredValue,
                    BetlValue = Game.Settings.BetlValue,
                    DurchValue = Game.Settings.DurchValue,
                    AllowFakeSeven = Game.Settings.AllowFakeSeven,
                    AllowFake107 = Game.Settings.AllowFake107,
                    AllowAXTalon = Game.Settings.AllowAXTalon,
                    AllowTrumpTalon = Game.Settings.AllowTrumpTalon,
                    AllowAIAutoFinish = Game.Settings.AllowAIAutoFinish,
                    AllowPlayerAutoFinish = Game.Settings.AllowPlayerAutoFinish,
                    OptimisticAutoFinish = Game.Settings.OptimisticAutoFinish
                };
                g.RegisterPlayers(new Engine.AbstractPlayer[]
                                  {
                                          new DummyPlayer(g) { Name = Game.Settings.PlayerNames[0] },
                                          new DummyPlayer(g) { Name = Game.Settings.PlayerNames[1] },
                                          new DummyPlayer(g) { Name = Game.Settings.PlayerNames[2] }
                                  });
                g.players[_gameStartingPlayerIndex].Hand = _cards.Select(i => (Card)i.Tag).Take(12).ToList();
                g.players[(_gameStartingPlayerIndex + 1) % Mariasek.Engine.Game.NumPlayers].Hand = _cards.Select(i => (Card)i.Tag).Skip(12).Take(10).ToList();
                g.players[(_gameStartingPlayerIndex + 2) % Mariasek.Engine.Game.NumPlayers].Hand = _cards.Select(i => (Card)i.Tag).Skip(22).Take(10).ToList();

                Game.StorageAccessor.GetStorageAccess();
                MainScene.CreateDirectoryForFilePath(saveGamePath);

                using (var fs = File.Open(saveGamePath, FileMode.Create))
                {
                    g.SaveGame(fs, saveFromEditor: true);
                }
                if (_filename != null)
                {
                    var dt = new FileInfo(saveGamePath).CreationTime.ToString("dd.MM.yyyy HH:mm:ss");
                    _fileLabel.Text = $"{dt}\t{_filename}";
                }
            }
            catch (Exception ex)
            {
                _fileLabel.Text = ex.Message;
                return;
            }
        }

        private string _fileToDelete;

        private async void DeleteGameClicked(object sender)
        {
            _fileToDelete = null;
            if (_gameListBox.IsVisible &&
                _gameListBox.Text.Length > 0 &&
                _gameListBox.HighlightedLine >= 0 &&
                _gameListBox.HighlightedLine < _files.Length)
            {
                var path = _files[_gameListBox.HighlightedLine];
                _fileToDelete = Path.GetFileNameWithoutExtension(path);
            }
            else if (_filename != null)
            {
                _fileToDelete = _filename;
            }
            if (_fileToDelete != null && !MessageBox.IsVisible)
            {
                var buttonIndex = await MessageBox.Show("Potvrzení", $"Smazat hru {_fileToDelete}?", new string[] { "Zpět", "Smazat" });

                if (buttonIndex.HasValue && buttonIndex.Value == 1)
                {
                    var path = Path.Combine(_editorPath, $"{_fileToDelete}.hra");

                    Game.StorageAccessor.GetStorageAccess();
                    File.Delete(path);
                    if (_fileToDelete == _filename)
                    {
                        _filename = null;
                        _fileLabel.Text = "";
                    }
                    GameListClicked(this);
                }
            }
        }

        private void StartingPlayerClicked(object sender)
        {
            _gameStartingPlayerIndex = (_gameStartingPlayerIndex + 1) % Mariasek.Engine.Game.NumPlayers;
            PopulateLabels();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            _loadGameButton.IsEnabled = _gameListBox.IsVisible &&
                                        _gameListBox.Text.Length > 0 &&
                                        _gameListBox.HighlightedLine >= 0 &&
                                        _gameListBox.HighlightedLine < _files.Length;
            _saveGameButton.IsEnabled = !_gameListBox.IsVisible;
            _playGameButton.IsEnabled = !_gameListBox.IsVisible;
            _deleteGameButton.IsEnabled = (_gameListBox.IsVisible &&
                                           _gameListBox.Text.Length > 0 &&
                                           _gameListBox.HighlightedLine >= 0 &&
                                           _gameListBox.HighlightedLine < _files.Length) || _filename != null;
            _saveGameButton.IsEnabled = !_gameListBox.IsVisible;
            _startingPlayerButton.IsEnabled = !_gameListBox.IsVisible;
            _sendBtn.IsEnabled = _filename != null;

            if (!_gameListBox.IsVisible &&
                _filename == null &&
                string.IsNullOrEmpty(_fileLabel.Text))
            {
                _fileLabel.Text = "Upravte hru přetáhnutím jednotlivých karet.";
            }
        }
    }
}