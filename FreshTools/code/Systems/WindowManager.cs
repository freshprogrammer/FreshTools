using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FreshTools
{
    /// <summary>
    /// Created as a replacement for the discontinued winsplit revolution on windows 10
    /// </summary>
    static class WindowManager
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref Rect Rect);

        [Serializable, StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int X { get { return Left; } }
            public int Y { get { return Top; } }
            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }

            public Rectangle ToRectangle()
            {
                return Rectangle.FromLTRB(Left, Top, Right, Bottom);
            }
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

        //these offsets are callibrated for my 2560x1440 monitors, not sure if they are the same on other resolutions or zoom levels
        private static Point positionOffset = new Point(7, 0);
        private static Point resizeOffset = new Point(14, 7);

        public static bool wrapLeftRightScreens = true;

        public static void MoveActiveWindowToRightScreen()
        {
            MoveActiveWindowOffScreenInDirection(new Point(1,0));
        }

        public static void MoveActiveWindowToLeftScreen()
        {
            MoveActiveWindowOffScreenInDirection(new Point(-1,0));
        }

        /// <summary>
        /// Move this window off this screen in the given direction eg (1,0) for right.
        /// </summary>
        /// <param name="dir">Direction to move window in units of the active screens' resolution.</param>
        public static void MoveActiveWindowOffScreenInDirection(Point dir)
        {
            if(Screen.AllScreens.Length==1)return;
            IntPtr handle = GetForegroundWindow();
            Rect rect = new Rect();
            GetWindowRect(handle, ref rect);
            Rectangle childRect = rect.ToRectangle();

            Screen currentScreen = GetScreenContainingWindow(childRect);
            Rectangle workingArea = currentScreen.WorkingArea;

            childRect.Offset(workingArea.Width * dir.X, workingArea.Height * dir.Y);

            Screen newScreen = GetScreenContainingWindow(childRect);

            if (wrapLeftRightScreens)
            {
                if (newScreen == currentScreen)
                {
                    if (dir.Y == 0)
                    {
                        if (dir.X == 1)//wrap right to left most
                            newScreen = GetLeftMostScreen();
                        else if (dir.X == -1)//wrap Left to right most
                            newScreen = GetRightMostScreen();
                    }
                }
            }

            MoveActiveWindowToScreen(newScreen);
        }

        #region Move window to all 8 directions
        public static void MoveActiveWindowToTop()
        {
            Rectangle workingArea = GetScreenActiveWindowIsOn().WorkingArea;
            MoveActiveWindowTo(workingArea.X, workingArea.Y, workingArea.Width, workingArea.Height / 2);
        }

        public static void MoveActiveWindowToBottom()
        {
            Rectangle workingArea = GetScreenActiveWindowIsOn().WorkingArea;
            MoveActiveWindowTo(workingArea.X, workingArea.Y + workingArea.Height / 2, workingArea.Width, workingArea.Height / 2);
        }

        public static void MoveActiveWindowToLeft()
        {
            Rectangle workingArea = GetScreenActiveWindowIsOn().WorkingArea;
            MoveActiveWindowTo(workingArea.X, workingArea.Y, workingArea.Width / 2, workingArea.Height);
        }

        public static void MoveActiveWindowToRight()
        {
            Rectangle workingArea = GetScreenActiveWindowIsOn().WorkingArea;
            MoveActiveWindowTo(workingArea.X + workingArea.Width / 2, workingArea.Y, workingArea.Width / 2, workingArea.Height);
        }

        public static void MoveActiveWindowToTopLeft()
        {
            Rectangle workingArea = GetScreenActiveWindowIsOn().WorkingArea;
            MoveActiveWindowTo(workingArea.X, workingArea.Y, workingArea.Width / 2, workingArea.Height / 2);
        }

        public static void MoveActiveWindowToTopRight()
        {
            Rectangle workingArea = GetScreenActiveWindowIsOn().WorkingArea;
            MoveActiveWindowTo(workingArea.X + workingArea.Width / 2, workingArea.Y, workingArea.Width / 2, workingArea.Height / 2);
        }

        public static void MoveActiveWindowToBottomLeft()
        {
            Rectangle workingArea = GetScreenActiveWindowIsOn().WorkingArea;
            MoveActiveWindowTo(workingArea.X, workingArea.Y + workingArea.Height / 2, workingArea.Width / 2, workingArea.Height / 2);
        }

        public static void MoveActiveWindowToBottomRight()
        {
            Rectangle workingArea = GetScreenActiveWindowIsOn().WorkingArea;
            MoveActiveWindowTo(workingArea.X + workingArea.Width / 2, workingArea.Y + workingArea.Height / 2, workingArea.Width / 2, workingArea.Height / 2);
        }
        #endregion

        /// <summary>
        /// Move window to new screen and scale it as necisarry
        /// </summary>
        /// <param name="newScreen"></param>
        public static void MoveActiveWindowToScreen(Screen newScreen)
        {
            IntPtr handle = GetForegroundWindow();
            Rect rect = new Rect();
            GetWindowRect(handle, ref rect);
            Rectangle childRect = rect.ToRectangle();

            Screen currentScreen = GetScreenContainingWindow(childRect);
            Rectangle currentWorkingArea = currentScreen.WorkingArea;

            double xPosPercentage = (1.0*childRect.X - currentWorkingArea.X) / currentWorkingArea.Width;
            double yPosPercentage = (1.0 * childRect.Y - currentWorkingArea.Y) / currentWorkingArea.Height;
            int newX = (int)(newScreen.WorkingArea.X + newScreen.WorkingArea.Width * xPosPercentage);
            int newY = (int)(newScreen.WorkingArea.Y + newScreen.WorkingArea.Height * yPosPercentage);
            
            if (newScreen.WorkingArea.Width != currentScreen.WorkingArea.Width || newScreen.WorkingArea.Height != currentScreen.WorkingArea.Height)
            {
                //different size working area/resolution
                //scale window to new resolution
                double widthPercentage = 1.0 * rect.Width / currentWorkingArea.Width;
                double heightPercentage = 1.0 * rect.Height / currentWorkingArea.Height;
                int newWidth = (int)(newScreen.WorkingArea.Width * widthPercentage);
                int newHeight = (int)(newScreen.WorkingArea.Height * heightPercentage);
                MoveActiveWindowTo(newX, newY, newWidth, newHeight);
            }
            else
                MoveActiveWindowTo(newX, newY);

        }

        public static void MoveActiveWindowTo(int x, int y)
        {
            const short SWP_NOSIZE = 1;
            //const short SWP_NOMOVE = 0X2;
            const short SWP_NOZORDER = 0X4;
            const int SWP_SHOWWINDOW = 0x0040;

            IntPtr handle = GetForegroundWindow();
            if (handle != IntPtr.Zero)
            {
                const int cx = 0;
                const int cy = 0;
                SetWindowPos(handle, 0, x - positionOffset.X, y - positionOffset.Y, cx, cy, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
        }

        public static void MoveActiveWindowTo(int x, int y, int newWidth, int newHeight)
        {
            const short SWP_NOSIZE = 0;
            //const short SWP_NOMOVE = 0X2;
            const short SWP_NOZORDER = 0X4;
            const int SWP_SHOWWINDOW = 0x0040;

            IntPtr handle = GetForegroundWindow();
            if (handle != IntPtr.Zero)
            {
                SetWindowPos(handle, 0, x - positionOffset.X, y - positionOffset.Y, newWidth + resizeOffset.X, newHeight + resizeOffset.Y, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
        }

        public static Screen GetScreenActiveWindowIsOn()
        {
            IntPtr handle = GetForegroundWindow();
            Rect rect = new Rect();
            GetWindowRect(handle, ref rect);
            Rectangle childRect = rect.ToRectangle();

            return GetScreenContainingWindow(childRect);
        }

        /// <summary>
        /// Return screen that contians midpoint of child or is closes to it
        /// </summary>
        /// <param name="child">Rectangle of child window to be located</param>
        /// <returns></returns>
        public static Screen GetScreenContainingWindow(Rectangle child)
        {
            Point childCenter = new Point(child.X + child.Width / 2, child.Y + child.Height / 2);
            foreach (Screen s in Screen.AllScreens)
            {
                if (s.Bounds.Contains(childCenter))
                    return s;
            }

            //if child is off screen calculate distance to center of each screen
            Screen closestScreen = Screen.PrimaryScreen;
            double minDistance = double.MaxValue;
            foreach (Screen s in Screen.AllScreens)
            {
                Point screenCenter = new Point(s.Bounds.X + s.Bounds.Width / 2, s.Bounds.Y + s.Bounds.Height / 2);
                double distance = FreshMath.Distance(childCenter, screenCenter);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestScreen = s;
                }
            }
            return closestScreen;
        }

        public static Screen GetScreenToTheLeft(Screen screen)
        {
            if (screen == GetLeftMostScreen())
                return GetRightMostScreen();

            return screen;
        }

        public static Screen GetScreenToTheRight(Screen screen)
        {
            if (screen == GetRightMostScreen())
                return GetLeftMostScreen();

            return screen;
        }

        //these probably only NEED to be calculated once (Assuming the screens dont move or change)
        public static Screen GetLeftMostScreen()
        {
            Screen result = Screen.PrimaryScreen;
            foreach (Screen s in Screen.AllScreens)
            {
                if (s.Bounds.Left < result.Bounds.Left) result = s;
            }
            return result;
        }

        public static Screen GetRightMostScreen()
        {
            Screen result = Screen.PrimaryScreen;
            foreach (Screen s in Screen.AllScreens)
            {
                if (s.Bounds.Right > result.Bounds.Right) result = s;
            }
            return result;
        }

        public static int GetTaskbarHeight()
        {
            return Screen.PrimaryScreen.Bounds.Height - Screen.PrimaryScreen.WorkingArea.Height;
        }
    }
}
