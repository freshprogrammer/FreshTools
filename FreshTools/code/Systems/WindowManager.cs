using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FreshTools
{
    /// <summary>
    /// Created as a replacement for the discontinued winsplit revolution on windows 10
    /// </summary>
    public static class WindowManager
    {
        const float ComparisonRoundingLimit = 0.001f;//this will be to be broader for lower resolutions since they have less pixes to round to

        public static bool WrapLeftRightScreens = true;
        public static bool HotKeysEnabled { get { return hotKeysEnabled; } set { if (value)EnableHotKeys(); else DisableHotKeys(); } }

        private static bool hotKeysEnabled = false;

        private static List<RectangleF> cornerSizes;
        private static List<RectangleF> topSizes;
        private static List<RectangleF> sideSizes;

        //these offsets are callibrated for my 2560x1440 monitors, not sure if they are the same on other resolutions or zoom levels
        private static Point positionOffset = new Point(-7, 0);
        private static Point resizeOffset = new Point(14, 7);

        #region Setup and teardown
        static WindowManager()
        {
            cornerSizes = new List<RectangleF>(3);
            cornerSizes.Add(new RectangleF(0, 0, 0.5f,  0.5f));
            cornerSizes.Add(new RectangleF(0, 0, 0.667f, 0.5f));
            cornerSizes.Add(new RectangleF(0, 0, 0.333f, 0.5f));
            
            sideSizes = new List<RectangleF>(3);
            sideSizes.Add(new RectangleF(0, 0, 0.5f,  1));
            sideSizes.Add(new RectangleF(0, 0, 0.667f, 1));
            sideSizes.Add(new RectangleF(0, 0, 0.333f, 1));

            topSizes = new List<RectangleF>(2);
            topSizes.Add(new RectangleF(0, 0, 1, 0.5f));
            topSizes.Add(new RectangleF(0.33f, 0, 0.334f, 0.5f));
        }

        private static void EnableHotKeys()
        {
            if (!hotKeysEnabled)
            {
                hotKeysEnabled = true;
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Shift), Keys.A, MoveActiveWindowToLeftScreen);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Shift), Keys.S, MoveActiveWindowToRightScreen);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad1, MoveActiveWindowToBottomLeft);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad2, MoveActiveWindowToBottom);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad3, MoveActiveWindowToBottomRight);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad4, MoveActiveWindowToLeft);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad6, MoveActiveWindowToRight);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad7, MoveActiveWindowToTopLeft);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad8, MoveActiveWindowToTop);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad9, MoveActiveWindowToTopRight);
            }
        }

        private static void DisableHotKeys()
        {
            if (hotKeysEnabled)
            {
                hotKeysEnabled = false;
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Shift), Keys.A);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Shift), Keys.S);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad1);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad2);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad3);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad4);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad6);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad7);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad8);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad9);
            }
        }
        #endregion

        #region External functions
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
        #endregion

        #region Move Window Between screens
        public static void MoveActiveWindowToRightScreen(object o, HotKeyEventArgs args)
        {
            MoveActiveWindowOffScreenInDirection(new Point(1,0));
        }

        public static void MoveActiveWindowToLeftScreen(object o, HotKeyEventArgs args)
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

            if (WrapLeftRightScreens)
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
                double widthPercentage = 1.0 * (rect.Width - resizeOffset.X) / currentWorkingArea.Width;
                double heightPercentage = 1.0 * (rect.Height - resizeOffset.Y) / currentWorkingArea.Height;
                int newWidth = (int)(newScreen.WorkingArea.Width * widthPercentage);
                int newHeight = (int)(newScreen.WorkingArea.Height * heightPercentage);
                newWidth += resizeOffset.X;
                newHeight += resizeOffset.Y;
                MoveActiveWindowTo(newX, newY, newWidth, newHeight, false);
            }
            else
                MoveActiveWindowTo(newX, newY, false);

        }
#endregion

        #region Window movement & snap logic
        public static void MoveActiveWindowTo(int x, int y, bool includePosOffset = true)
        {
            const short SWP_NOSIZE = 1;
            //const short SWP_NOMOVE = 0X2;
            const short SWP_NOZORDER = 0X4;
            const int SWP_SHOWWINDOW = 0x0040;

            const int cx = 0;
            const int cy = 0;

            IntPtr handle = GetForegroundWindow();
            if (handle != IntPtr.Zero)
            {
                if (includePosOffset)
                {
                    x += positionOffset.X;
                    y += positionOffset.Y;
                }

                SetWindowPos(handle, 0, x, y, cx, cy, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
        }

        //if moving window on same screen you need to offset its pos. If moving window between screens the x,y pos should already be know and not need to be re-offset
        private static void MoveActiveWindowTo(int x, int y, int newWidth, int newHeight, bool includePosOffset=true)
        {
            const short SWP_NOSIZE = 0;
            //const short SWP_NOMOVE = 0X2;
            const short SWP_NOZORDER = 0X4;
            const int SWP_SHOWWINDOW = 0x0040;

            IntPtr handle = GetForegroundWindow();
            if (handle != IntPtr.Zero)
            {
                if (includePosOffset)
                {
                    x += positionOffset.X;
                    y += positionOffset.Y;

                    newWidth += resizeOffset.X;
                    newHeight += resizeOffset.Y;
                }

                SetWindowPos(handle, 0, x, y, newWidth, newHeight, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
        }

        private static void SnapActiveWindow(SnapDirection dir)
        {
            Rectangle workingArea = GetScreenActiveWindowIsOn().WorkingArea;
            RectangleF activeRelativeRectangle = GetActiveWindowRelativeRectangleF();

            RectangleF[] snapAreas = null;
            switch (dir)
            {
                case SnapDirection.TopLeft:
                case SnapDirection.TopRight:
                case SnapDirection.BottomLeft:
                case SnapDirection.BottomRight:
                    snapAreas = new RectangleF[cornerSizes.Count];
                    cornerSizes.CopyTo(snapAreas);
                    break;
                case SnapDirection.Top:
                case SnapDirection.Bottom:
                    snapAreas = new RectangleF[topSizes.Count];
                    topSizes.CopyTo(snapAreas);
                    break;
                case SnapDirection.Left:
                case SnapDirection.Right:
                    snapAreas = new RectangleF[sideSizes.Count];
                    sideSizes.CopyTo(snapAreas);
                    break;
            }

            //offset snap areas X
            switch (dir)
            {
                case SnapDirection.TopLeft:
                case SnapDirection.Top:
                case SnapDirection.Left:
                case SnapDirection.Bottom:
                case SnapDirection.BottomLeft:
                    break;//do nothing
                case SnapDirection.BottomRight:
                case SnapDirection.TopRight:
                case SnapDirection.Right:
                    for (int i = 0; i < snapAreas.Length; i++)
                    {
                        snapAreas[i].X = 1 - snapAreas[i].Width;
                    }
                    break;
            }

            //offset snap areas Y
            switch (dir)
            {
                case SnapDirection.Top:
                case SnapDirection.Left:
                case SnapDirection.Right:
                case SnapDirection.TopLeft:
                case SnapDirection.TopRight:
                    break;//do nothing
                case SnapDirection.BottomRight:
                case SnapDirection.Bottom:
                case SnapDirection.BottomLeft:
                    for (int i = 0; i < snapAreas.Length; i++)
                    {
                        snapAreas[i].Y = 1 - snapAreas[i].Height;
                    }
                    break;
            }

            //if already snapped to 2 than use 3, ect
            int snapIndex = 0;
            for (int i = snapAreas.Length-2; i>=0 ; i--)
            {
                if(CloseEnough(activeRelativeRectangle, snapAreas[i]))
                {
                    snapIndex = ++i;
                    break;
                }
            }

            int newX = workingArea.X + (int)(snapAreas[snapIndex].X * workingArea.Width);
            int newY = workingArea.Y + (int)(snapAreas[snapIndex].Y * workingArea.Height);
            int newW = (int)(snapAreas[snapIndex].Width * workingArea.Width);
            int newH = (int)(snapAreas[snapIndex].Height * workingArea.Height);
            MoveActiveWindowTo(newX, newY, newW, newH);
        }
        #endregion

        #region Snap active window screen to all 8 directions
        public static void MoveActiveWindowToTop(object o, HotKeyEventArgs args)
        {
            SnapActiveWindow(SnapDirection.Top);
        }

        public static void MoveActiveWindowToBottom(object o, HotKeyEventArgs args)
        {
            SnapActiveWindow(SnapDirection.Bottom);
        }

        public static void MoveActiveWindowToLeft(object o, HotKeyEventArgs args)
        {
            SnapActiveWindow(SnapDirection.Left);
        }

        public static void MoveActiveWindowToRight(object o, HotKeyEventArgs args)
        {
            SnapActiveWindow(SnapDirection.Right);
        }

        public static void MoveActiveWindowToTopLeft(object o, HotKeyEventArgs args)
        {
            SnapActiveWindow(SnapDirection.TopLeft);
        }

        public static void MoveActiveWindowToTopRight(object o, HotKeyEventArgs args)
        {
            SnapActiveWindow(SnapDirection.TopRight);
        }

        public static void MoveActiveWindowToBottomLeft(object o, HotKeyEventArgs args)
        {
            SnapActiveWindow(SnapDirection.BottomLeft);
        }

        public static void MoveActiveWindowToBottomRight(object o, HotKeyEventArgs args)
        {
            SnapActiveWindow(SnapDirection.BottomRight);
        }
        #endregion

        #region Calculate Screen(s) info and Generics
        /// <summary>
        /// Returns the screen containing the currently active window
        /// </summary>
        /// <returns></returns>
        public static Screen GetScreenActiveWindowIsOn()
        {
            IntPtr handle = GetForegroundWindow();
            Rect rect = new Rect();
            GetWindowRect(handle, ref rect);
            Rectangle childRect = rect.ToRectangle();

            return GetScreenContainingWindow(childRect);
        }

        public static RectangleF GetActiveWindowRelativeRectangleF()
        {
            IntPtr handle = GetForegroundWindow();
            Rect rect = new Rect();
            GetWindowRect(handle, ref rect);
            Rectangle childRect = rect.ToRectangle();

            Rectangle workingSpace = GetScreenContainingWindow(childRect).WorkingArea;

            float relativeX = 1f * (childRect.X - positionOffset.X - workingSpace.X) / workingSpace.Width;
            float relativeY = 1f * (childRect.Y - positionOffset.Y - workingSpace.Y) / workingSpace.Height;
            float relativeW = 1f * (childRect.Width - resizeOffset.X) / workingSpace.Width;
            float relativeH = 1f * (childRect.Height - resizeOffset.Y) / workingSpace.Height;
            return new RectangleF(relativeX, relativeY, relativeW, relativeH);
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

        public static bool CloseEnough(RectangleF a, RectangleF b)
        {
            if (Math.Abs(a.X - b.X) < ComparisonRoundingLimit)
                if (Math.Abs(a.Y - b.Y) < ComparisonRoundingLimit)
                    if (Math.Abs(a.Width - b.Width) < ComparisonRoundingLimit)
                        if (Math.Abs(a.Height - b.Height) < ComparisonRoundingLimit)
                            return true;
            return false;
        }
        #endregion

        private enum SnapDirection
        {
            Top,
            Right,
            Bottom,
            Left,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
        }
    }
}
