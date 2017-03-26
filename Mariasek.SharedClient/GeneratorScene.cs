using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;
using Mariasek.SharedClient.GameComponents;
using Mariasek.Engine.New;
using Mariasek.Engine.New.Schema;

namespace Mariasek.SharedClient
{
	[XmlRoot]
	public class Volba
	{
		[XmlArray]
		public Karta[] Karty { get; set; }
		[XmlArray]
		public Karta[] Talon { get; set; }
		[XmlAttribute]
		public Hra Hra { get; set; }
		[XmlIgnore]
		public Barva? Trumf;
		[XmlIgnore]
		public bool TrumfValueSpecified { get { return Trumf.HasValue; } }
		[XmlAttribute(AttributeName = "Trumf")]
		public Barva TrumfValue
		{
			get { return Trumf.HasValue ? Trumf.Value : 0; }
			set { Trumf = value; }
		}
	}

	public class GeneratorScene : Scene
	{
		private string _dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Volby");

		Label _lblCount;
		Label _lblMsg;
		Button _sendBtn;
		Button _saveBtn;
		Button _generateBtn;
		Button _menuBtn;
		Button gtHraButton;
		Button gt7Button;
		Button gt100Button;
		Button gt107Button;
		Button gtBetlButton;
		Button gtDurchButton;
		Button[] gtButtons;
		GameComponents.Hand _hand;
		List<Card> _unsortedData;
		Mariasek.Engine.New.Hand _handData;
		Card _trumpCard;
		Hra _gameType;
		List<Card> _talon;
		List<Volba> _data;
		GameState _state;

		public GeneratorScene(MariasekMonoGame game)
			: base(game)
		{
			_data = new List<Volba>();
			_talon = new List<Card>();
			_lblCount = new Label(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth - 110, 10),
				Width = 100,
				Height = 32,
				HorizontalAlign = HorizontalAlignment.Right,
				Text = "0"
			};
			_lblMsg = new Label(this)
			{
				Position = new Vector2(Game.VirtualScreenWidth/2 - 200, Game.VirtualScreenHeight / 2f + 40),
				Width = 400,
				Height = 32,
				HorizontalAlign = HorizontalAlignment.Center,
				Text = ""
			};
			_lblMsg.Hide();
			_menuBtn = new Button(this)
			{
				Position = new Vector2(10, 0),
				Width = 250,
				Height = 50,
				Text = "Menu",
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Bottom
			};
			_menuBtn.Click += MenuBtnClick;
			_sendBtn = new Button(this)
			{
				Position = new Vector2(10, 60),
				Width = 250,
				Height = 50,
				Text = "Odeslat data",
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Bottom,
				IsEnabled = Directory.GetFiles(_dataFolder).Count() > 0
			};
			_sendBtn.Click += SendBtnClick;
			_generateBtn = new Button(this)
			{
				Position = new Vector2(10, Game.VirtualScreenHeight / 2f - 25),
				Width = 250,
				Height = 50,
				Text = "Generovat karty",
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Bottom
			};
			_generateBtn.Click += GenerateBtnClick;
			_saveBtn = new Button(this)
			{
				Position = new Vector2(10, Game.VirtualScreenHeight / 2f + 40),
				Width = 250,
				Height = 50,
				Text = "Uložit data",
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Left : AnchorType.Bottom,
				IsEnabled = false
			};
			_saveBtn.Click += SaveBtnClick;
			gtHraButton = new Button(this)
			{
				Text = "Hra",
				Position = new Vector2(Game.VirtualScreenWidth / 2f - 325, Game.VirtualScreenHeight / 2f - 100),
				Tag = Hra.Hra,
				ZIndex = 100
			};
			gtHraButton.Click += GtButtonClicked;
			gtHraButton.Hide();
			gt7Button = new Button(this)
			{
				Text = "Sedma",
				Position = new Vector2(Game.VirtualScreenWidth / 2f - 215, Game.VirtualScreenHeight / 2f - 100),
				Tag = Hra.Hra | Hra.Sedma,
				ZIndex = 100
			};
			gt7Button.Click += GtButtonClicked;
			gt7Button.Hide();
			gt100Button = new Button(this)
			{
				Text = "Kilo",
				Position = new Vector2(Game.VirtualScreenWidth / 2f - 105, Game.VirtualScreenHeight / 2f - 100),
				Tag = Hra.Kilo,
				ZIndex = 100
			};
			gt100Button.Click += GtButtonClicked;
			gt100Button.Hide();
			gt107Button = new Button(this)
			{
				Text = "Stosedm",
				Position = new Vector2(Game.VirtualScreenWidth / 2f + 5, Game.VirtualScreenHeight / 2f - 100),
				Tag = Hra.Kilo | Hra.Sedma,
				ZIndex = 100
			};
			gt107Button.Click += GtButtonClicked;
			gt107Button.Hide();
			gtBetlButton = new Button(this)
			{
				Text = "Betl",
				Position = new Vector2(Game.VirtualScreenWidth / 2f + 115, Game.VirtualScreenHeight / 2f - 100),
				Tag = Hra.Betl,
				ZIndex = 100
			};
			gtBetlButton.Click += GtButtonClicked;
			gtBetlButton.Hide();
			gtDurchButton = new Button(this)
			{
				Text = "Durch",
				Position = new Vector2(Game.VirtualScreenWidth / 2f + 225, Game.VirtualScreenHeight / 2f - 100),
				Tag = Hra.Durch,
				ZIndex = 100
			};
			gtDurchButton.Click += GtButtonClicked;
			gtDurchButton.Hide();
			gtButtons = new[] { gtHraButton, gt7Button, gt100Button, gt107Button, gtBetlButton, gtDurchButton };
			_hand = new GameComponents.Hand(this, Game.Content)
			{
				Centre = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight - 90),
				ZIndex = 50,
				Anchor = Game.RealScreenGeometry == ScreenGeometry.Wide ? AnchorType.Main : AnchorType.Bottom
			};
			_hand.Click += CardClicked;
			_hand.Hide();
			Background = Game.Content.Load<Texture2D>("wood2");
			BackgroundTint = Color.DimGray;
		}

		void MenuBtnClick(object sender)
		{
			Game.MenuScene.SetActive();
		}

		void SendBtnClick(object sender)
		{
			if (Game.EmailSender != null)
			{
				var files = Directory.GetFiles(_dataFolder);

				Game.EmailSender.SendEmail(new[] { "mariasek.app@gmail.com" }, "Mariasek volby", string.Format("{0} files attached", files.Count()), files);

				foreach (var file in files)
				{
					try
					{
						File.Delete(file);
					}
					catch (Exception e)
					{
						System.Diagnostics.Debug.WriteLine(string.Format("Cannot delete file {0}\n{1}", file, e.Message));
					}
				}
				_sendBtn.IsEnabled = Directory.GetFiles(_dataFolder).Count() > 0;
			}
		}

		void SaveBtnClick(object sender)
		{
			if (!Directory.Exists(_dataFolder))
			{
				Directory.CreateDirectory(_dataFolder);
			}

			var filename = Path.Combine(_dataFolder, string.Format("volby-{0}.xml", DateTime.Now.ToString("yyyyMMdd_HHmmss")));

			try
			{
				var xml = new XmlSerializer(typeof(List<Volba>));

				using (var fs = File.Open(filename, FileMode.Create))
				{
					xml.Serialize(fs, _data);
				}
				_data.Clear();
				_lblCount.Text = "0";
				_sendBtn.IsEnabled = Directory.GetFiles(_dataFolder).Count() > 0;
			}
			catch (Exception e)
			{
				System.Diagnostics.Debug.WriteLine(string.Format("Cannot save file\n{0}", e.Message));
			}

		}

		void GenerateBtnClick(object sender)
		{
			var deck = new Deck();

			deck.Shuffle();

			_unsortedData = deck.Take(12);
			_talon.Clear();
			_handData = new Mariasek.Engine.New.Hand(Enumerable.Empty<Card>());
			_handData.AddRange(_unsortedData);
			_hand.Show();
			_hand.IsEnabled = true;
			UpdateHand(true, 5);
			_state = GameState.ChooseTrump;
			ShowMsg("Vyber si trumf");
		}

		private static Stream GetFileStream(string filename)
		{
			var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), filename);

			return new FileStream(path, FileMode.Create);
		}

		public void SortHand(Card cardToHide = null)
		{
			_handData.Sort(false, false);

			_hand.UpdateHand((List<Card>)_handData, 0, null);
			_hand.SortHand(_handData);
			_hand.ShowStraight((int)Game.VirtualScreenWidth - 20);
			_hand.SelectCard(cardToHide); //misto schovani trumfovou kartu oznacime
			//_hand.WaitUntil(() => !_hand.SpritesBusy);
		}

		public void UpdateHand(bool flipCardsUp = false, int cardsNotRevealed = 0, Card cardToHide = null)
		{
			_hand.UpdateHand(((List<Card>)_handData).ToArray(), cardsNotRevealed, cardToHide);
			_hand.ShowStraight((int)Game.VirtualScreenWidth - 20);
			if (flipCardsUp)
			{
				_hand.WaitUntil(() => !_hand.SpritesBusy)
					 .Invoke(() =>
					   {
						   _hand.UpdateHand(((List<Card>)_handData).ToArray(), cardsNotRevealed, cardToHide);
					   });
			}
			if (_state == GameState.ChooseTalon)
			{
				_hand.WaitUntil(() => !_hand.SpritesBusy)
					 .Invoke(() =>
						{
							SortHand(cardToHide);
						});
			}
		}

		public void CardClicked(object sender)
		{
			var button = sender as CardButton;
			var card = (Card)button.Tag;

			switch (_state)
			{
				case GameState.ChooseTrump:
					_trumpCard = card;
					_hand.Invoke(() => button.IsSelected = true)
						 .Wait(1000)
						 .Invoke(() =>
						 {
							 UpdateHand(cardToHide: _trumpCard); //abych si otocil zbyvajicich 5 karet
						 });
					ShowMsg("Vyber si talon");
					_state = GameState.ChooseTalon;
					break;
					
				case GameState.ChooseTalon:
					if (button.IsFaceUp)
					{
						//selected
						button.FlipToBack();
						_talon.Add(card);
					}
					else
					{
						//unselected
						button.FlipToFront();
						_talon.Remove(card);
					}
					if (_talon.Count == 2)
					{
						_handData.Remove(_talon[0]);
						_handData.Remove(_talon[1]);
						UpdateHand(cardToHide: _trumpCard);
						foreach (var btn in gtButtons)
						{
							btn.Show();
						}
						_hand.IsEnabled = false;
						ShowMsg("Zvol si hru");
						_state = GameState.ChooseGameType;
					}
					break;
			}
		}

		void GtButtonClicked(object sender)
		{
			_gameType = (Hra)(sender as Button).Tag;

			var volba = new Volba()
			{
				Karty = _unsortedData.Select(i => new Karta
				{
					Barva = i.Suit,
					Hodnota = i.Value
				}).ToArray(),
				Hra = _gameType,
				Trumf = _trumpCard.Suit,
				Talon = _talon.Select(i => new Karta
				{
					Barva = i.Suit,
					Hodnota = i.Value
				}).ToArray()
			};
			_data.Add(volba);
			_lblCount.Text = _data.Count.ToString();
			_saveBtn.IsEnabled = true;

			foreach (var btn in gtButtons)
			{
				btn.Hide();
			}
			UpdateHand(false, 12);
			_lblMsg.Hide();
		}

		private void ShowMsg(string message)
		{
			_lblMsg.Text = message;
			_lblMsg.Show();
		}
	}
}
