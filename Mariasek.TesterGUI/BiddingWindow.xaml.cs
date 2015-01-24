using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Mariasek.Engine.New;

namespace Mariasek.TesterGUI
{
    /// <summary>
    /// Interaction logic for BiddingWindow.xaml
    /// </summary>
    public partial class BiddingWindow : Window
    {
        /// <summary>
        /// The bidding object. Bidding.Bids represents player's valid bids.
        /// </summary>
        public Bidding Bidding { get; set; }
        /// <summary>
        /// Holds the bids that the player has commited to or doubled.
        /// </summary>
        public Hra Bid { get; private set; }

        public BiddingWindow(Bidding bidding)
        {
            Bidding = bidding;

            var bids = Bidding.Bids;

            InitializeComponent();
            flekBtn.Tag = Hra.Hra | Hra.Kilo | Hra.Betl | Hra.Durch;
            sedmaBtn.Tag = Hra.Sedma | Hra.SedmaProti;
            kiloBtn.Tag = Hra.KiloProti;

            flekBtn.IsEnabled = (bids & (Hra.Hra | Hra.Kilo | Hra.Betl | Hra.Durch)) != 0;
            sedmaBtn.IsEnabled = (bids & (Hra.Sedma | Hra.SedmaProti)) != 0;
            kiloBtn.IsEnabled = (bids & Hra.KiloProti) != 0;

            sedmaBtn.Content = (bids & Hra.SedmaProti) != 0 && (Bidding.SevenAgainstMultiplier == 0)? "Sedma proti" : "Na sedmu";
            kiloBtn.Content = (bids & Hra.KiloProti) != 0 && (Bidding.HundredAgainstMultiplier == 0) ? "Kilo proti" : "Na kilo";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Bid = Bidding.Bids &
                    ((flekBtn.IsChecked.HasValue && flekBtn.IsChecked.Value
                        ? (Hra)flekBtn.Tag
                        : 0) |
                     (sedmaBtn.IsChecked.HasValue && sedmaBtn.IsChecked.Value
                        ? (Hra)sedmaBtn.Tag
                        : 0) |
                     (kiloBtn.IsChecked.HasValue && kiloBtn.IsChecked.Value
                        ? (Hra)kiloBtn.Tag
                        : 0));
            Hide();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.RemoveCloseButton();
            this.CenterWithMainWindow();
        }
    }
}
