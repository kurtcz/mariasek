using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Mariasek.TesterGUI
{
    public class WindowResizer
    {
        private const int WM_SYSCOMMAND = 0x112;
        private HwndSource hwndSource;
        Window activeWin;

        public WindowResizer(Window activeW)
        {
            activeWin = activeW as Window;

            activeWin.SourceInitialized += new EventHandler(InitializeWindowSource);
        }


        public void resetCursor()
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed)
            {
                activeWin.Cursor = Cursors.Arrow;
            }
        }

        public void dragWindow()
        {
            activeWin.DragMove();
        }

        private void InitializeWindowSource(object sender, EventArgs e)
        {
            hwndSource = PresentationSource.FromVisual((Visual)sender) as HwndSource;
            hwndSource.AddHook(new HwndSourceHook(WndProc));
        }

        IntPtr retInt = IntPtr.Zero;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            Debug.WriteLine("WndProc messages: " + msg.ToString());
            //
            // Check incoming window system messages
            //
            if (msg == WM_SYSCOMMAND)
            {
                Debug.WriteLine("WndProc messages: " + msg.ToString());
            }

            return IntPtr.Zero;
        }



        public enum ResizeDirection
        {
            Left = 1,
            Right = 2,
            Top = 3,
            TopLeft = 4,
            TopRight = 5,
            Bottom = 6,
            BottomLeft = 7,
            BottomRight = 8,
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);


        private void ResizeWindow(ResizeDirection direction)
        {
            SendMessage(hwndSource.Handle, WM_SYSCOMMAND, (IntPtr)(61440 + direction), IntPtr.Zero);
        }


        public void resizeWindow(object sender)
        {
            Rectangle clickedRectangle = sender as Rectangle;

            switch (clickedRectangle.Name)
            {
                case "top":
                    activeWin.Cursor = Cursors.SizeNS;
                    ResizeWindow(ResizeDirection.Top);
                    break;
                case "bottom":
                    activeWin.Cursor = Cursors.SizeNS;
                    ResizeWindow(ResizeDirection.Bottom);
                    break;
                case "left":
                    activeWin.Cursor = Cursors.SizeWE;
                    ResizeWindow(ResizeDirection.Left);
                    break;
                case "right":
                    activeWin.Cursor = Cursors.SizeWE;
                    ResizeWindow(ResizeDirection.Right);
                    break;
                case "topLeft":
                    activeWin.Cursor = Cursors.SizeNWSE;
                    ResizeWindow(ResizeDirection.TopLeft);
                    break;
                case "topRight":
                    activeWin.Cursor = Cursors.SizeNESW;
                    ResizeWindow(ResizeDirection.TopRight);
                    break;
                case "bottomLeft":
                    activeWin.Cursor = Cursors.SizeNESW;
                    ResizeWindow(ResizeDirection.BottomLeft);
                    break;
                case "bottomRight":
                    activeWin.Cursor = Cursors.SizeNWSE;
                    ResizeWindow(ResizeDirection.BottomRight);
                    break;
                default:
                    break;
            }
        }

        public void maximizeOrRestore()
        {
            activeWin.WindowState = activeWin.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        public void displayDragCursor(object sender)
        {
            activeWin.Cursor = Cursors.SizeAll;
        }

        public void displayResizeCursor(object sender)
        {

            Rectangle clickedRectangle = sender as Rectangle;

            switch (clickedRectangle.Name)
            {
                case "top":
                    activeWin.Cursor = Cursors.SizeNS;
                    break;
                case "bottom":
                    activeWin.Cursor = Cursors.SizeNS;
                    break;
                case "left":
                    activeWin.Cursor = Cursors.SizeWE;
                    break;
                case "right":
                    activeWin.Cursor = Cursors.SizeWE;
                    break;
                case "topLeft":
                    activeWin.Cursor = Cursors.SizeNWSE;
                    break;
                case "topRight":
                    activeWin.Cursor = Cursors.SizeNESW;
                    break;
                case "bottomLeft":
                    activeWin.Cursor = Cursors.SizeNESW;
                    break;
                case "bottomRight":
                    activeWin.Cursor = Cursors.SizeNWSE;
                    break;
                default:
                    break;
            }
        }
    }
}
