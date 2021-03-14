//using System.Collections.Generic;
//using System.Threading.Tasks;

//using Microsoft.Xna.Framework;

//using Mariasek.SharedClient.GameComponents;
//using Mariasek.SharedClient.BmFont;

//namespace Mariasek.SharedClient
//{
//    public class TestScene : Scene
//    {
//        #region Child components
//        #pragma warning disable 414

//        private Hand hand;
//        private Label label;
//        private Label label2;
//        private Label label3;
//        private Label label4;
//        private RectangleShape rect;
//        private Button button;
//        private SpriteButton spriteButton;
//        private SpriteButton spriteButton0;
//        private TextBox box1;
//		private TextBox box2;
//		private TextBox box3;
//		private TextBox box4;
//		private TextBox box5;
//		private TextBox box6;
//        private CardButton cb;
//        private bool firstTime = true;

//        #pragma warning restore 414
//        #endregion

//        private Task t;

//        public TestScene(MariasekMonoGame game)
//            : base(game)
//        {
//        }

//        /// <summary>
//        /// Allows the game component to perform any initialization it needs to before starting
//        /// to run.  This is where it can query for any required services and load content.
//        /// </summary>
//        public override void Initialize()
//        {
//            base.Initialize();

//            var cards = new []
//            {
//                    new Mariasek.Engine.Card(Mariasek.Engine.Barva.Kule, Mariasek.Engine.Hodnota.Eso),
//                    new Mariasek.Engine.Card(Mariasek.Engine.Barva.Kule, Mariasek.Engine.Hodnota.Desitka),
//                    new Mariasek.Engine.Card(Mariasek.Engine.Barva.Kule, Mariasek.Engine.Hodnota.Kral),
//                    new Mariasek.Engine.Card(Mariasek.Engine.Barva.Kule, Mariasek.Engine.Hodnota.Svrsek),
//                    new Mariasek.Engine.Card(Mariasek.Engine.Barva.Kule, Mariasek.Engine.Hodnota.Spodek),
//                    new Mariasek.Engine.Card(Mariasek.Engine.Barva.Kule, Mariasek.Engine.Hodnota.Devitka),
//                    new Mariasek.Engine.Card(Mariasek.Engine.Barva.Kule, Mariasek.Engine.Hodnota.Osma),
//                    new Mariasek.Engine.Card(Mariasek.Engine.Barva.Kule, Mariasek.Engine.Hodnota.Sedma),
//                    new Mariasek.Engine.Card(Mariasek.Engine.Barva.Cerveny, Mariasek.Engine.Hodnota.Eso),
//                    new Mariasek.Engine.Card(Mariasek.Engine.Barva.Cerveny, Mariasek.Engine.Hodnota.Desitka),
//                    new Mariasek.Engine.Card(Mariasek.Engine.Barva.Cerveny, Mariasek.Engine.Hodnota.Kral),
//                    new Mariasek.Engine.Card(Mariasek.Engine.Barva.Cerveny, Mariasek.Engine.Hodnota.Svrsek)
//            };
//            //cb = new CardButton(this, new Sprite(this, Game.CardTextures, cards[0].ToTextureRect()){ Name = "FrontSprite" })
//            //{
//            //        Name = "cb",
//            //        Position = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight / 2f)
//            //};
////            cb.Click += CardButtonClick;
////            spriteButton0 = new SpriteButton(this,
////                new Sprite(this, Game.CardTextures)
////                {
////                    Name = "spr0",
////                    SpriteRectangle = cards[0].ToTextureRect(),
////                    RotationAngle = (float)Math.PI / 2
////                })
////                {
////                    Name = "sb0",
////                    Position = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight / 2f)
////                };
////            spriteButton = new SpriteButton(this,
////                new Sprite(this, Game.ReverseTexture)
////                {
////                    Name = "spr"
////                })
////                {
////                    Name = "sb",
////                    Position = new Vector2(Game.VirtualScreenWidth / 2f + 200, Game.VirtualScreenHeight / 2f)
////                };
////            hand = new Hand(this, cards) { Centre = new Vector2(Game.VirtualScreenWidth / 2f, Game.VirtualScreenHeight - 60) };
//            //rect = new RectangleShape(this)
//            //    {
//            //        Position = new Vector2(100,10),//Game.VirtualScreenWidth - 300, 60),
//            //        Width = 400,
//            //        Height = 100,
//            //        BackgroundColors = new List<Color>
//            //            {
//            //                Color.Navy
//            //            },
//            //        BorderColors = new List<Color>
//            //            {
//            //                Color.White
//            //            },
//            //        BorderRadius = 10,
//            //        //BorderShadow = 5,
//            //        BorderThickness = 3,
//            //        //InitialShadowIntensity = 0.9f,
//            //        //FinalShadowIntensity = 0.1f
//            //    };
//            //rect.UpdateTexture();
//			//label = new Label(this)
//   //         {
//   //                 Text = "Příliš žluťoučký kůň úpěl ďábelské ódy",
//   //                 TextColor = Color.Yellow,
//   //                 Position = new Vector2(0, 0),
//   //                 Width = 500,
//   //                 Height = 50
//   //         };
//   //         label2 = new Label(this)
//   //             {
//   //                 Text = "Příliš žluťoučký kůň úpěl ďábelské ódy",
//   //                 TextColor = Color.Yellow,
//   //                 Position = new Vector2(Game.VirtualScreenWidth - 500, 0),
//   //                 Width = 500,
//   //                 Height = 50,
//   //                 HorizontalAlign = HorizontalAlignment.Right
//   //             };
//   //         label3 = new Label(this)
//   //             {
//   //                 Text = "Příliš žluťoučký kůň úpěl ďábelské ódy",
//   //                 TextColor = Color.Yellow,
//   //                 Position = new Vector2(0, Game.VirtualScreenHeight - 50),
//   //                 Width = 500,
//   //                 Height = 50,
//   //                 VerticalAlign = VerticalAlignment.Bottom
//   //             };
//   //         label4 = new Label(this)
//   //             {
//   //                 Text = "Příliš žluťoučký kůň úpěl ďábelské ódy",
//   //                 TextColor = Color.Yellow,
//   //                 Position = new Vector2(Game.VirtualScreenWidth - 500, Game.VirtualScreenHeight - 50),
//   //                 Width = 500,
//   //                 Height = 50,
//   //                 HorizontalAlign = HorizontalAlignment.Right,
//   //                 VerticalAlign = VerticalAlignment.Bottom
//   //             };

//            box1 = new TextBox(this)
//            {
//                Position = new Vector2(50, 50),
//                Width = 200,
//                Height = 200,
//                BackgroundColor = Color.DimGray,
//                TextColor = Color.Yellow,
//                BorderColor = Color.Yellow,
//                Opacity = 0.8f,
//				VerticalAlign = VerticalAlignment.Top,
//                Text = "Lajn 1\nLajn 222\nLajn 33\nLajn 4444\nLajn 5\nLajn 6\nLajn 7\nLajn 8"
//            };
//			box2 = new TextBox(this)
//			{
//				Position = new Vector2(300, 50),
//				Width = 200,
//				Height = 200,
//				BackgroundColor = Color.DimGray,
//				TextColor = Color.Yellow,
//				BorderColor = Color.Yellow,
//				Opacity = 0.8f,
//				VerticalAlign = VerticalAlignment.Middle,
//				Text = "Lajn 1\nLajn 222\nLajn 33\nLajn 4444\nLajn 5\nLajn 6\nLajn 7\nLajn 8"
//			};
//			box3 = new TextBox(this)
//			{
//				Position = new Vector2(550, 50),
//				Width = 200,
//				Height = 200,
//				BackgroundColor = Color.DimGray,
//				TextColor = Color.Yellow,
//				BorderColor = Color.Yellow,
//				Opacity = 0.8f,
//				VerticalAlign = VerticalAlignment.Bottom,
//				Text = "Lajn 1\nLajn 222\nLajn 33\nLajn 4444\nLajn 5\nLajn 6\nLajn 7\nLajn 8"
//			};

//			box4 = new TextBox(this)
//			{
//				Position = new Vector2(50, 300),
//				Width = 200,
//				Height = 200,
//				BackgroundColor = Color.DimGray,
//				TextColor = Color.Yellow,
//				BorderColor = Color.Yellow,
//				Opacity = 0.8f,
//				HorizontalAlign = HorizontalAlignment.Left,
//				VerticalAlign = VerticalAlignment.Top,
//				Text = "Příliš žluťoučký kůň úpěl ďábelské ódy"
//			};
//			box5 = new TextBox(this)
//			{
//				Position = new Vector2(300, 300),
//				Width = 200,
//				Height = 200,
//				BackgroundColor = Color.DimGray,
//				TextColor = Color.Yellow,
//				BorderColor = Color.Yellow,
//				Opacity = 0.8f,
//				HorizontalAlign = HorizontalAlignment.Center,
//				VerticalAlign = VerticalAlignment.Middle,
//				Text = "Příliš žluťoučký kůň úpěl ďábelské ódy"
//			};
//			box6 = new TextBox(this)
//			{
//				Position = new Vector2(550, 300),
//				Width = 200,
//				Height = 200,
//				BackgroundColor = Color.DimGray,
//				TextColor = Color.Yellow,
//				BorderColor = Color.Yellow,
//				Opacity = 0.8f,
//				HorizontalAlign = HorizontalAlignment.Right,
//				VerticalAlign = VerticalAlignment.Bottom,
//				Text = "Příliš žluťoučký kůň úpěl ďábelské ódy"
//			};
//		}

//        public override void Update(GameTime gameTime)
//        {
//            base.Update(gameTime);

//            //if (!cb.Sprite.IsBusy)
//            //{
//            //    cb.FlipToBack(1)
//            //      .FlipToFront(1);
//            //}
//        }

//        //public void CardButtonClick(object sender)
//        //{
//        //    cb.FlipToBack(1).FlipToFront(1);
//        //}
//    }
//}

