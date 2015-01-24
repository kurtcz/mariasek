using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace Mariasek.TesterGUI
{
    public static class WindowExtensions
    {
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public static void RemoveCloseButton(this Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
        }

        public static void CenterWithMainWindow(this Window window)
        {
            var mainWindow = Application.Current.MainWindow;

            window.Left = mainWindow.Left + (mainWindow.Width - window.ActualWidth) / 2;
            window.Top = mainWindow.Top + (mainWindow.Height - window.ActualHeight) / 2;
        }
    }
}
