using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Mariasek.Engine.New;

namespace Mariasek.TesterGUI
{
    /// <summary>
    /// Interaction logic for EditorWindow.xaml
    /// </summary>
    public partial class EditorWindow : Window
    {
        public List<List<Card>> Hands = new List<List<Card>>
                                            {
                                                new List<Card>(),
                                                new List<Card>(),
                                                new List<Card>(),
                                                new List<Card>() //posledni je talon
                                            };
        public int RoundNumber;
        private Image[][] imgs;

        #region ScaleValue Dependency Property

        public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue", typeof(double), typeof(EditorWindow), new UIPropertyMetadata(1.0, new PropertyChangedCallback(OnScaleValueChanged), new CoerceValueCallback(OnCoerceScaleValue)));

        private static object OnCoerceScaleValue(DependencyObject o, object value)
        {
            EditorWindow editorWindow = o as EditorWindow;
            if (editorWindow != null)
                return editorWindow.OnCoerceScaleValue((double)value);
            else
                return value;
        }

        private static void OnScaleValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            EditorWindow editorWindow = o as EditorWindow;
            if (editorWindow != null)
                editorWindow.OnScaleValueChanged((double)e.OldValue, (double)e.NewValue);
        }

        protected virtual double OnCoerceScaleValue(double value)
        {
            if (double.IsNaN(value))
                return 1.0d;

            value = Math.Max(0.1, value);
            return value;
        }

        protected virtual void OnScaleValueChanged(double oldValue, double newValue)
        {

        }

        public double ScaleValue
        {
            get
            {
                return (double)GetValue(ScaleValueProperty);
            }
            set
            {
                SetValue(ScaleValueProperty, value);
            }
        }
        #endregion

        private void EditorGrid_SizeChanged(object sender, EventArgs e)
        {
            CalculateScale();
        }

        private void CalculateScale()
        {
            double yScale = ActualHeight / 430;
            double xScale = ActualWidth / 350.0d;
            double value = Math.Min(xScale, yScale);
            ScaleValue = (double)OnCoerceScaleValue(EditorGrid, value);
        }

        public EditorWindow(Game g)
        {
            InitializeComponent();
            var scaleFactor = AppSettings.GetDouble("ScaleFactor", 0.8);
            Width *= scaleFactor;
            Height *= scaleFactor;

            var imgSrcConverter = new ImageSourceConverter();

            imgs = new[]
                   {
                       new [] {imgCervenyEso, imgCervenyDesitka, imgCervenyKral, imgCervenySvrsek, imgCervenySpodek, imgCervenyDevitka, imgCervenyOsma, imgCervenySedma},
                       new [] {imgZelenyEso, imgZelenyDesitka, imgZelenyKral, imgZelenySvrsek, imgZelenySpodek, imgZelenyDevitka, imgZelenyOsma, imgZelenySedma},
                       new [] {imgKuleEso, imgKuleDesitka, imgKuleKral, imgKuleSvrsek, imgKuleSpodek, imgKuleDevitka, imgKuleOsma, imgKuleSedma},
                       new [] {imgZaludyEso, imgZaludyDesitka, imgZaludyKral, imgZaludySvrsek, imgZaludySpodek, imgZaludyDevitka, imgZaludyOsma, imgZaludySedma}
                   };

            foreach (var b in Enum.GetValues(typeof(Barva)).Cast<Barva>())
            {
                foreach (var h in Enum.GetValues(typeof(Hodnota)).Cast<Hodnota>())
                {
                    var c = new Card(b, h);
                    string source =
                        string.Format(
                            "pack://application:,,,/Mariasek.TesterGUI;component/Images/karty/{0}",
                            CardResources.Images[c.Num]);

                    imgs[c.Num / 8][7 - (c.Num % 8)].SetValue(UIElement.OpacityProperty, 1.0);
                    imgs[c.Num / 8][7 - (c.Num % 8)].SetValue(FrameworkElement.TagProperty, c);
                    imgs[c.Num / 8][7 - (c.Num % 8)].SetValue(Image.SourceProperty, imgSrcConverter.ConvertFromString(source) as ImageSource);
                }
            }

            HandComboBox.Items.Add("Human");
            HandComboBox.Items.Add("Player2");
            HandComboBox.Items.Add("Player3");
            HandComboBox.Items.Add("Talon");
            //HandComboBox.SelectedIndex = 0;

            GameStarterComboBox.Items.Add("Human");
            GameStarterComboBox.Items.Add("Player2");
            GameStarterComboBox.Items.Add("Player3");
            //GameStarterComboBox.SelectedIndex = 0;

            RoundStarterComboBox.Items.Add("Human");
            RoundStarterComboBox.Items.Add("Player2");
            RoundStarterComboBox.Items.Add("Player3");
            //RoundStarterComboBox.SelectedIndex = 0;

            foreach (var b in Enum.GetValues(typeof (Barva)).Cast<Barva>())
            {
                TrumpComboBox.Items.Add(b);
            }
            //TrumpComboBox.SelectedIndex = 0;

            ///////////////////
            Hands = g.players.Select(i => new List<Card>(i.Hand)).ToList();
            if (g.CurrentRound != null)
            {
                if (g.CurrentRound.c1 != null)
                {
                    Hands[Array.IndexOf(g.players, g.CurrentRound.player1)].Add(g.CurrentRound.c1);
                }
                if (g.CurrentRound.c2 != null)
                {
                    Hands[(Array.IndexOf(g.players, g.CurrentRound.player1) + 1) % Game.NumPlayers].Add(g.CurrentRound.c2);
                }
                if (g.CurrentRound.c3 != null)
                {
                    Hands[(Array.IndexOf(g.players, g.CurrentRound.player1) + 2) % Game.NumPlayers].Add(g.CurrentRound.c3);
                }
            }
            if (g.talon != null)
            {
                Hands.Add(new List<Card>(g.talon));
            }
            else
            {
                Hands.Add(new List<Card>());
            }
            GameStarterComboBox.SelectedIndex = Array.IndexOf(g.players, g.GameStartingPlayer);
            if (g.RoundNumber == 0)
            {
                RoundStarterComboBox.SelectedIndex = GameStarterComboBox.SelectedIndex;
                TrumpComboBox.SelectedIndex = -1;
            }
            else
            {
                if (g.CurrentRound != null)
                {
                    RoundStarterComboBox.SelectedIndex = Array.IndexOf(g.players, g.CurrentRound.player1);
                }
                else
                {
                    RoundStarterComboBox.SelectedIndex = 0;
                }
                TrumpComboBox.SelectedIndex = (int)g.trump;
            }                        
        }

        private void Card_Click(object sender, RoutedEventArgs e)
        {
            Image img = DependencyObjectExtensions.findElementOfType<Image>(sender as DependencyObject);
            Card card = (Card)img.Tag;
            var handIndex = HandComboBox.SelectedIndex;
            
            if (Hands[handIndex].Contains(card))
            {
                Hands[handIndex].Remove(card);
                ShowHand();
            }
            else if ((double) img.GetValue(OpacityProperty) == 0.5)
            {
                Hands[handIndex].Add(card);
                ShowHand();
            }            
        }

        private void clearButton_Click(object sender, RoutedEventArgs e)
        {
            var handIndex = HandComboBox.SelectedIndex;

            Hands[handIndex].Clear();
            ShowHand();
        }

        private void HandComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowHand();
        }

        private void ShowHand()
        {
            var handIndex = HandComboBox.SelectedIndex;

            foreach (var b in Enum.GetValues(typeof (Barva)).Cast<Barva>())
            {
                foreach (var h in Enum.GetValues(typeof (Hodnota)).Cast<Hodnota>())
                {
                    var c = new Card(b, h);

                    imgs[c.Num / 8][7 - (c.Num % 8)].SetValue(OpacityProperty, 0.5);
                    imgs[c.Num / 8][7 - (c.Num % 8)].SetValue(IsEnabledProperty, true);
                }
            }

            foreach (var hand in Hands)
            {
                foreach (var card in hand)
                {
                    if (hand == Hands[handIndex])
                    {
                        imgs[card.Num / 8][7 - (card.Num % 8)].SetValue(OpacityProperty, 1.0);
                    }
                    else
                    {
                        imgs[card.Num / 8][7 - (card.Num % 8)].SetValue(IsEnabledProperty, false);
                        imgs[card.Num / 8][7 - (card.Num % 8)].SetValue(OpacityProperty, 0.0);
                    }
                }
                if (hand == Hands[handIndex])
                {
                    cardCountLabel.Content = string.Format("Počet karet: {0}", hand.Count);
                }
            }
        }

        private void MyEditorWindow_Activated(object sender, EventArgs e)
        {
            ShowHand();
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            if (Hands[0].Count == Hands[1].Count &&
                Hands[0].Count == Hands[2].Count &&
                Hands[3].Count == 2)
            {
                RoundNumber = 10 - Hands[0].Count + 1;
                DialogResult = true;
            }
            else if (Hands.Count(i => i.Count == 10) == 2 &&
                     Hands.Count(i => i.Count == 12) == 1 &&
                     Hands[3].Count == 0)
            {
                RoundNumber = 0;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show(this, "Počet karet nesedí", "Chyba", MessageBoxButton.OK);
                DialogResult = null;
            }
        }
    }
}
