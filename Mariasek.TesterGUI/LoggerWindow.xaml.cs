using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mariasek.Engine.New.Logger;

namespace Mariasek.TesterGUI
{
    /// <summary>
    /// Interaction logic for LoggerWindow.xaml
    /// </summary>
    public partial class LoggerWindow : Window
    {
        private readonly WindowResizer windowResizer;

        public NotifyAppender MyAppender { get { return NotifyAppender.Instance; } }

        public LoggerWindow()
        {
            InitializeComponent();
            var scaleFactor = AppSettings.GetDouble("ScaleFactor", 0.8);
            Width *= scaleFactor;
            Height *= scaleFactor;

            windowResizer = new WindowResizer(this);
            textBox.FontSize = AppSettings.GetDouble("LogWindowFontSize", 10);
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
            if (e.Key == Key.Tab)
            {
                Hide();
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            textBox.ScrollToEnd();
        }
    }
}
