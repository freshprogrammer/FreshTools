using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FreshTools
{
    static class WindowManager
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref Rectangle rectangle);

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

        private static Point offset = new Point(7, 0);
        private static Point res = new Point(2560, 1440);

        public static int GetTaskbarHeight()
        {
            return Screen.PrimaryScreen.Bounds.Height - Screen.PrimaryScreen.WorkingArea.Height;
        }

        public static void MoveActiveWindowToRightMonitor()
        {
            IntPtr handle = GetForegroundWindow();
            Rectangle rect = new Rectangle();
            GetWindowRect(handle, ref rect);

            MoveActiveWindowTo(rect.X + res.X, rect.Y);
        }

        public static void MoveActiveWindowToLeftMonitor()
        {
            IntPtr handle = GetForegroundWindow();
            Rectangle rect = new Rectangle();
            GetWindowRect(handle, ref rect);

            MoveActiveWindowTo(rect.X - res.X, rect.Y);
        }

        public static void MoveActiveWindowToTopLeft()
        {
            MoveActiveWindowTo(0, 0);
        }

        public static void MoveActiveWindowToTopRight()
        {
            MoveActiveWindowTo(res.X / 2, 0);
        }

        public static void MoveActiveWindowToBottomLeft()
        {
            MoveActiveWindowTo(0, (res.Y - GetTaskbarHeight()) / 2);
        }

        public static void MoveActiveWindowToBottomRight()
        {
            MoveActiveWindowTo(res.X / 2, (res.Y-GetTaskbarHeight()) / 2);
        }

        public static void MoveActiveWindowTo(int x, int y)
        {
            const short SWP_NOSIZE = 1;
            const short SWP_NOMOVE = 0X2;
            const short SWP_NOZORDER = 0X4;
            const int SWP_SHOWWINDOW = 0x0040;

            IntPtr handle = GetForegroundWindow();
            if (handle != IntPtr.Zero)
            {
                const int cx = 0;
                const int cy = 0;
                SetWindowPos(handle, 0, x - offset.X, y - offset.Y, cx, cy, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
        }
    }
}
