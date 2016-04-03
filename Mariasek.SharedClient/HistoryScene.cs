using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
//using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

using Mariasek.SharedClient.GameComponents;

namespace Mariasek.SharedClient
{
    public class HistoryScene : Scene
    {
        private Button _menuButton;
        private Button _resetHistoryButton;
        private Label _header;
        private Label _footer;
        private TextBox _historyBox;
        private bool _useMockData;// = true;

        public HistoryScene(MariasekMonoGame game)
            : base(game)
        {
        }

        /// <summary>
        /// Allows the game component to perform any initialization it needs to before starting
        /// to run.  This is where it can query for any required services and load content.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            _menuButton = new Button(this)
                {
                    Position = new Vector2(10, 10),
                    Width = 200,
                    Height = 50,
                    Text = "Menu"
                };
            _menuButton.Click += MenuClicked;
            _resetHistoryButton = new Button(this)
                {
                    Position = new Vector2(10, (int)Game.VirtualScreenHeight - 60),
                    Width = 200,
                    Height = 50,
                    Text = "Smazat historii"
                };
            _resetHistoryButton.Click += ResetHistoryClicked;
            _header = new Label(this)
                {
                    Position = new Vector2(220, 10),
                    Width = (int)Game.VirtualScreenWidth - 180,
                    Height = 50
                };
            _historyBox = new TextBox(this)
                {
                    Position = new Vector2(220, 60),
                    Width = (int)Game.VirtualScreenWidth - 230,
                    Height = (int)Game.VirtualScreenHeight - 120,
                    HorizontalAlign = HorizontalAlignment.Left,
                    VerticalAlign = VerticalAlignment.Top
                };
            _footer = new Label(this)
                {
                    Position = new Vector2(220, Game.VirtualScreenHeight - 60),
                    Width = (int)Game.VirtualScreenWidth - 230,
                    Height = 50
                };
            Background = Game.Content.Load<Texture2D>("wood2");
            BackgroundTint = Color.DimGray;

            PopulateControls();
        }

        public void PopulateControls()
        {
            _header.Text = string.Format("Hráč:\t\t{0}\t\t{1}\t{2}", "Hráč 1", "Hráč 2 (AI)", "Hráč 3 (AI)");

            var culture = CultureInfo.CreateSpecificCulture("cs-CZ");
            var sb = new StringBuilder();
            if (_useMockData)
            {
                for (var i = 0; i < 20; i++)
                {
                    sb.AppendFormat("\t\t{0}\t{1}\t{2}\n", 
                        ((i + 1) * 1f).ToString("C", culture), 
                        ((i + 1) * -0.5f).ToString("C", culture), 
                        ((i + 1) * -0.5f).ToString("C", culture));
                }
            }
            foreach (var historyItem in Game.Money)
            {
                sb.AppendFormat("\t\t{0}\t{1}\t{2}\n", 
                    historyItem.MoneyWon[0].ToString("C", culture), 
                    historyItem.MoneyWon[1].ToString("C", culture), 
                    historyItem.MoneyWon[2].ToString("C", culture));
            }
            _historyBox.Text = sb.ToString();

            var sum1 = Game.Money.Sum(i => i.MoneyWon[0]).ToString("C", culture);
            var sum2 = Game.Money.Sum(i => i.MoneyWon[1]).ToString("C", culture);
            var sum3 = Game.Money.Sum(i => i.MoneyWon[2]).ToString("C", culture);

            _footer.Text = string.Format("Součet:\t{0}\t{1}\t{2}", sum1, sum2, sum3);
        }

        private void ResetHistoryClicked(object sender)
        {
            _useMockData = false;
            Game.Money.Clear();
            PopulateControls();
            Game.MainScene.SaveHistory();
        }

        private void MenuClicked(object sender)
        {
            Game.MenuScene.SetActive();
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}

