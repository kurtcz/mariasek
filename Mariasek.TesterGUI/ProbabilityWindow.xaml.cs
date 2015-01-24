using System.Windows;
using System.Windows.Controls;

namespace Mariasek.TesterGUI
{
    /// <summary>
    /// Interaction logic for Probabilites.xaml
    /// </summary>
    public partial class ProbabilityWindow : Window
    {
        private MainWindow _mainWindow;
        public int showPlayer;
        public int viewAsPlayer;
        public bool initializing;

        public ProbabilityWindow(MainWindow mainWindow)
        {
            showPlayer = 1;
            _mainWindow = mainWindow;
            InitializeComponent();
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            showPlayer = 0;
            _mainWindow.UpdateProbabilityWindow();
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            showPlayer = 1;
            _mainWindow.UpdateProbabilityWindow();
        }

        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            showPlayer = 2;
            _mainWindow.UpdateProbabilityWindow();
        }

        private void Button4_Click(object sender, RoutedEventArgs e)
        {
            showPlayer = 3;
            _mainWindow.UpdateProbabilityWindow();
        }

        private void ViewAsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!initializing)
            {
                viewAsPlayer = ViewAsComboBox.SelectedIndex;
                _mainWindow.UpdateProbabilityWindow();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.CenterWithMainWindow();
        }
    }
}
