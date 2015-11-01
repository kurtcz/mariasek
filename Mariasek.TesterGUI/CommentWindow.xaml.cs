using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mariasek.Engine.New.Logger;

namespace Mariasek.TesterGUI
{
    /// <summary>
    /// Interaction logic for LoggerWindow.xaml
    /// </summary>
    public partial class CommentWindow : Window, INotifyPropertyChanged
    {
        private readonly WindowResizer windowResizer;

        public event PropertyChangedEventHandler PropertyChanged;

        public string _comment;
        public string Comment
        {
            get
            {
                return _comment;
            }
            set
            {
                if (value != _comment)
                {
                    _comment = value;
                    NotifyPropertyChanged("Comment");
                }
            }
        }

        public CommentWindow(string comment)
        {
            InitializeComponent();
            var scaleFactor = AppSettings.GetDouble("ScaleFactor", 0.8);
            Width *= scaleFactor;
            Height *= scaleFactor;

            windowResizer = new WindowResizer(this);
            textBox.FontSize = AppSettings.GetDouble("LogWindowFontSize", 10);

            Comment = comment;
        }

        private void NotifyPropertyChanged(string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        // for each rectangle, assign the following method to its MouseEnter event.
        private void DisplayResizeCursor(object sender, MouseEventArgs e)
        {
            windowResizer.displayResizeCursor(sender);
        }

        private void DisplayDragCursor(object sender, MouseEventArgs e)
        {
            windowResizer.displayDragCursor(sender);
        }

        // for each rectangle, assign the following method to its MouseLeave event.
        private void ResetCursor(object sender, MouseEventArgs e)
        {
            windowResizer.resetCursor();
        }

        // for each rectangle, assign the following method to its PreviewMouseDown event.
        private void Resize(object sender, MouseButtonEventArgs e)
        {
            windowResizer.resizeWindow(sender);
        }

        private void Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                windowResizer.dragWindow();
            }
            else
            {
                windowResizer.maximizeOrRestore();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
            }
        }
    }
}
