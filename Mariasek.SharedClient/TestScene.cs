using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

using Mariasek.SharedClient.GameComponents;
using Mariasek.SharedClient.BmFont;

namespace Mariasek.SharedClient
{
    public class TestScene : Scene
    {
        #region Child components
        #pragma warning disable 414

        private Hand hand;
        private Label label;
        private RectangleShape rect;
        private Button button;
        private SpriteButton spriteButton;
        private SpriteButton spriteButton0;
        private TextBox box;

        #pragma warning restore 414
        #endregion

        private Task t;

        public TestScene(MariasekMonoGame game)
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

            var cards = new []
            {
                    new Mariasek.Engine.New.Card(Mariasek.Engine.New.Barva.Kule, Mariasek.Engine.New.Hodnota.Eso),
                    new Mariasek.Engine.New.Card(Mariasek.Engine.New.Barva.Kule, Mariasek.Engine.New.Hodnota.Desitka),
                    new Mariasek.Engine.New.Card(Mariasek.Engine.New.Barva.Kule, Mariasek.Engine.New.Hodnota.Kral),
                    new Mariasek.Engine.New.Card(Mariasek.Engine.New.Barva.Kule, Mariasek.Engine.New.Hodnota.Svrsek),
                    new Mariasek.Engine.New.Card(Mariasek.Engine.New.Barva.Kule, Mariasek.Engine.New.Hodnota.Spodek),
                    new Mariasek.Engine.New.Card(Mariasek.Engine.New.Barva.Kule, Mariasek.Engine.New.Hodnota.Devitka),
                    new Mariasek.Engine.New.Card(Mariasek.Engine.New.Barva.Kule, Mariasek.Engine.New.Hodnota.Osma),
                    new Mariasek.Engine.New.Card(Mariasek.Engine.New.Barva.Kule, Mariasek.Engine.New.Hodnota.Sedma),
                    new Mariasek.Engine.New.Card(Mariasek.Engine.New.Barva.Cerveny, Mariasek.Engine.New.Hodnota.Eso),
                    new Mariasek.Engine.New.Card(Mariasek.Engine.New.Barva.Cerveny, Mariasek.Engine.New.Hodnota.Desitka),
                    new Mariasek.Engine.New.Card(Mariasek.Engine.New.Barva.Cerveny, Mariasek.Engine.New.Hodnota.Kral),
                    new Mariasek.Engine.New.Card(Mariasek.Engine.New.Barva.Cerveny, Mariasek.Engine.New.Hodnota.Svrsek)
            };
//            spriteButton0 = new SpriteButton(this,
//                new Sprite(this, Game.CardTextures)
//                {
//                    Name = "spr0",
//                    SpriteRectangle = cards[0].ToTextureRect(),
//                    RotationAngle = (float)Math.PI / 2
//                })
//                {
//                    Name = "sb0",
//                    Position = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight / 2f)
//                };
//            spriteButton = new SpriteButton(this,
//                new Sprite(this, Game.ReverseTexture)
//                {
//                    Name = "spr"
//                })
//                {
//                    Name = "sb",
//                    Position = new Vector2(Game.VirtualScreenWidth / 2f + 200, Game.VirtualScreenHeight / 2f)
//                };
            hand = new Hand(this, cards) { Centre = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight - 60) };
/*            rect = new RectangleShape(this)
                {
                    Position = new Vector2(Game.VirtualScreenWidth - 300, 60),
                    Width = 200,
                    Height = 100,
                    BackgroundColors = new List<Color>
                        {
                            Color.Navy
                        },
                    BorderColors = new List<Color>
                        {
                            Color.White
                        },
                    BorderRadius = 10,
                    //BorderShadow = 5,
                    BorderThickness = 3,
                    //InitialShadowIntensity = 0.9f,
                    //FinalShadowIntensity = 0.1f
                };
            rect.UpdateTexture();
            spriteButton0 = new SpriteButton(this)
                {
                    Sprite = new Sprite(this, Game.Content, "revers")
                        {
                            Tint = Color.Yellow
                        },
                    Position = new Vector2(Game.VirtualScreenWidth - 100, 60),
                };
            spriteButton = new SpriteButton(this)
                {
                    Sprite = new Sprite(this, Game.Content, "revers")
                        { 
                            RotationAngle = (float)Math.PI / 2 
                        },
                    Position = new Vector2(Game.VirtualScreenWidth - 300, 60)
                };
            button = new Button(this)
            {
                    Text = "Click me",
                    Position = new Vector2(100, 200)
            };
            label = new Label(this)
            {
                    Text = "Příliš žluťoučký kůň úpěl ďábelské ódy",
                    TextColor = Color.Yellow,
                    Position = new Vector2(10, 10)
            };
*/
            //hand.ShowStraight((int)Game.VirtualScreenWidth - 20);
//            hand.ShowArc((float)Math.PI / 2);
            //hand.ShowTest();

            box = new TextBox(this)
            {
                Position = new Vector2(100, 100),
                Width = 400,
                Height = 100,
                BackgroundColor = Color.DimGray,
//                TextColor = Color.Yellow,
//                BorderColor = Color.Yellow,
//                TextRenderer = Game.FontRenderers["BM2Font"],

//                    Position = new Vector2(Game.VirtualScreenWidth / 2 - 75, Game.VirtualScreenHeight / 2 - 100),
//                    Width = 150,
//                    Height = 50,
//                    BackgroundColor = new Color(0x40, 0x40, 0x40),
                    TextColor = Color.Yellow,
                    BorderColor = Color.Yellow,
                    Opacity = 0.8f,
                    Text = "Lajn 1\nLajn 222\nLajn 33\nLajn 4444\nLajn 5\nLajn 6\nLajn 7\nLajn 8"
            };
//            button = new Button(this)
//                {
//                    Position = new Vector2(100, 300),
//                    Width = 400,
//                    Height = 100,
//                    BackgroundColor = Color.Brown,
//                    TextColor = Color.Yellow,
//                    BorderColor = Color.Yellow,
////                    TextRenderer = Game.FontRenderers["BM2Font"],
//                    Text = "Line 1\nLine 222\nLine 33\nLine 4444\nLine 5\nLine 6\nLine 7\nLine 8"
//                };
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);


            if (!hand.IsMoving)
            {
                if (t == null || t.Status == TaskStatus.RanToCompletion)
                {
                    t = Task.Factory.StartNew(() =>
                        {
                            Thread.Sleep(1000);
                            if (hand.IsStraight)
                            {
                                hand.ShowArc((float)Math.PI / 2);
                            }
                            else
                            {
                                hand.ShowStraight((int)Game.VirtualScreenWidth - 20);
                            }
                        });
                }
            }

        }
    }
}

