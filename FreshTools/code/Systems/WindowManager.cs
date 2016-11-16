using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Reflection;

namespace FreshTools
{
    /// <summary>
    /// Created as a Windows 10 replacement for the discontinued winsplit revolution
    /// </summary>
    public static class WindowManager
    {   
        private const float ComparisonRoundingLimit = 0.01f;//this will need to be broader for lower resolutions since they have less pixes to round to

        //public ajustable settings
        public static bool WrapLeftRightScreens = true;
        public const bool SnapHotKeysEnabled_Default = true;
        public const bool SnapAltHotKeysEnabled_Default = false;
        public const bool MiscHotKeysEnabled_Default = true;
        public static bool SnapHotKeysEnabled { get { return snapHotKeysEnabled; } set { if (value)EnableSnapHotKeys(); else DisableSnapHotKeys(); } }
        public static bool SnapAltHotKeysEnabled { get { return snapAltHotKeysEnabled; } set { if (value)EnableSnapAltHotKeys(); else DisableSnapAltHotKeys(); } }
        public static bool MiscHotKeysEnabled { get { return miscHotKeysEnabled; } set { if (value)EnableMiscHotKeys(); else DisableMiscHotKeys(); } }

        //private local variables
        private static bool snapHotKeysEnabled = false;
        private static bool snapAltHotKeysEnabled = false;
        private static bool miscHotKeysEnabled = false;

        //window info for saving and restoring window possitions
		private const int LAYOUT_COUNT = 4;//menu and hotkeys 1-3
        private static Layout[] layouts = new Layout[LAYOUT_COUNT];
        private static string LayoutSaveFileBaseName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\" + Assembly.GetExecutingAssembly().GetName().Name + @"\windowLayouts\layout";

        //snap region sizes
        const int SnapSizeMaxCount = 9;
        public static List<RectangleF> CornerSnapSizes;
        public static List<RectangleF> TopSnapSizes;
        public static List<RectangleF> SideSnapSizes;
        public static List<RectangleF> CenterSnapSizes;

        //these offsets are callibrated for my 2560x1440 monitors, not sure if they are the same on other resolutions or zoom levels
        private static Point positionOffset = new Point(-7, 0);
        private static Point resizeOffset = new Point(14, 7);

        //alpha control variables
        private static IntPtr lastWindowAlphaHandle = IntPtr.Zero;
        private static byte lastWindowAlpha = 0;
        private const byte WindowAlphaIncrement = 32;
        private const byte MinWindowAlpha = 32;

        #region Setup and teardown
        static WindowManager()
        {
			for(int i=0;i<LAYOUT_COUNT;i++)
				layouts[i] = new Layout();
			
            if (!FreshArchives.IsWindows10())
            {
                positionOffset = new Point(0, 0);
                resizeOffset = new Point(0, 0);
            }
            LoadSnapSizes();
        }

        public static void LoadSnapSizes(VariablesFile settingsFile=null)
        {
            float oneThird = 1f / 3f;
            float oneThirdRoundUp = 1 - 2 * oneThird;//catch rounding error for last third

            List<RectangleF> defaultCornerSnapSizes = new List<RectangleF>(3);
            defaultCornerSnapSizes.Add(new RectangleF(0, 0, 0.5f, 0.5f));
            defaultCornerSnapSizes.Add(new RectangleF(0, 0, 1 - oneThirdRoundUp, 0.5f));
            defaultCornerSnapSizes.Add(new RectangleF(0, 0, oneThird, 0.5f));

            List<RectangleF> defaultSideSnapSizes = new List<RectangleF>(3);
            defaultSideSnapSizes.Add(new RectangleF(0, 0, 0.5f, 1));
            defaultSideSnapSizes.Add(new RectangleF(0, 0, 1 - oneThirdRoundUp, 1));
            defaultSideSnapSizes.Add(new RectangleF(0, 0, oneThird, 1));

            List<RectangleF> defaultTopSnapSizes = new List<RectangleF>(4);
            defaultTopSnapSizes.Add(new RectangleF(0, 0, 1, 0.5f));
            defaultTopSnapSizes.Add(new RectangleF(oneThird / 2, 0, oneThirdRoundUp * 2, 0.5f));//2/3 center full height with 1/6 open edges
            defaultTopSnapSizes.Add(new RectangleF(0.25f, 0, 0.5f, 0.5f));//1/2 center full height with 1/4 open edges
            defaultTopSnapSizes.Add(new RectangleF(oneThird, 0, oneThirdRoundUp, 0.5f));

            List<RectangleF> defaultCenterSnapSizes = new List<RectangleF>(4);
            defaultCenterSnapSizes.Add(new RectangleF(0, 0, 1, 1));
            defaultCenterSnapSizes.Add(new RectangleF(oneThird / 2, 0, oneThirdRoundUp * 2, 1));//2/3 center full height with 1/6 open edges
            defaultCenterSnapSizes.Add(new RectangleF(0.25f, 0, 0.5f, 1));//1/2 center full height with 1/4 open edges
            defaultCenterSnapSizes.Add(new RectangleF(oneThird, 0, oneThirdRoundUp, 1));

            if (settingsFile == null)
            {//default values
                CornerSnapSizes = defaultCornerSnapSizes;
                SideSnapSizes = defaultSideSnapSizes;
                TopSnapSizes = defaultTopSnapSizes;
                CenterSnapSizes = defaultCenterSnapSizes;
            }
            else
            {
                CornerSnapSizes = new List<RectangleF>();
                SideSnapSizes = new List<RectangleF>();
                TopSnapSizes = new List<RectangleF>();
                CenterSnapSizes = new List<RectangleF>();

                bool anyFound = false;
                for (int i = 0; i <= SnapSizeMaxCount; i++)
                {
                    Variable var = settingsFile.variables.FindVariable("CornerSnapSizes" + i);
                    if (var != null)//add to snapSizes
                    {
                        RectangleF val = FreshArchives.ParseRectangleF(var.GetValueSaveString());
                        if (val != RectangleF.Empty)
                        {
                            anyFound = true;
                            CornerSnapSizes.Add(val);
                        }
                        else
                            settingsFile.variables.RemoveVariable(var);
                    }
                }
                if (!anyFound) CornerSnapSizes = defaultCornerSnapSizes;

                anyFound = false;
                for (int i = 0; i <= SnapSizeMaxCount; i++)
                {
                    Variable var = settingsFile.variables.FindVariable("SideSnapSizes" + i);
                    if (var != null)//add to snapSizes
                    {
                        RectangleF val = FreshArchives.ParseRectangleF(var.GetValueSaveString());
                        if (val != RectangleF.Empty)
                        {
                            anyFound = true;
                            SideSnapSizes.Add(val);
                        }
                        else
                            settingsFile.variables.RemoveVariable(var);
                    }
                }
                if (!anyFound) SideSnapSizes = defaultSideSnapSizes;

                anyFound = false;
                for (int i = 0; i <= SnapSizeMaxCount; i++)
                {
                    Variable var = settingsFile.variables.FindVariable("TopSnapSizes" + i);
                    if (var != null)//add to snapSizes
                    {
                        RectangleF val = FreshArchives.ParseRectangleF(var.GetValueSaveString());
                        if (val != RectangleF.Empty)
                        {
                            anyFound = true;
                            TopSnapSizes.Add(val);
                        }
                        else
                            settingsFile.variables.RemoveVariable(var);
                    }
                }
                if (!anyFound) TopSnapSizes = defaultTopSnapSizes;

                anyFound = false;
                for (int i = 0; i <= SnapSizeMaxCount; i++)
                {
                    Variable var = settingsFile.variables.FindVariable("CenterSnapSizes" + i);
                    if (var != null)//add to snapSizes
                    {
                        RectangleF val = FreshArchives.ParseRectangleF(var.GetValueSaveString());
                        if (val != RectangleF.Empty)
                        {
                            anyFound = true;
                            CenterSnapSizes.Add(val);
                        }
                        else
                            settingsFile.variables.RemoveVariable(var);
                    }
                }
                if (!anyFound) CenterSnapSizes = defaultCenterSnapSizes;
            }
        }

        public static void SaveSnapSizes(VariablesFile settingsFile)
        {
            for (int i = 0; i < CornerSnapSizes.Count; i++)
            {
                string name = "CornerSnapSizes" + i;
                string val = CornerSnapSizes[i].X + "," + CornerSnapSizes[i].Y + "," + CornerSnapSizes[i].Width + "," + CornerSnapSizes[i].Height;
                settingsFile.variables.GetVariable(name, val).SetValue(val);
            }
            for (int i = 0; i < SideSnapSizes.Count; i++)
            {
                string name = "SideSnapSizes" + i;
                string val = SideSnapSizes[i].X + "," + SideSnapSizes[i].Y + "," + SideSnapSizes[i].Width + "," + SideSnapSizes[i].Height;
                settingsFile.variables.GetVariable(name, val).SetValue(val);
            }
            for (int i = 0; i < TopSnapSizes.Count; i++)
            {
                string name = "TopSnapSizes" + i;
                string val = TopSnapSizes[i].X + "," + TopSnapSizes[i].Y + "," + TopSnapSizes[i].Width + "," + TopSnapSizes[i].Height;
                settingsFile.variables.GetVariable(name, val).SetValue(val);
            }
            for (int i = 0; i < CenterSnapSizes.Count; i++)
            {
                string name = "CenterSnapSizes" + i;
                string val = CenterSnapSizes[i].X + "," + CenterSnapSizes[i].Y + "," + CenterSnapSizes[i].Width + "," + CenterSnapSizes[i].Height;
                settingsFile.variables.GetVariable(name, val).SetValue(val);
            }
        }

        private static void EnableSnapHotKeys()
        {
            Log.I(!snapHotKeysEnabled ? "Enabled" : "Did Nothing");
            if (!snapHotKeysEnabled)
            {
                snapHotKeysEnabled = true;
                //snap window to zones
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad1, MoveActiveWindowToBottomLeft);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad2, MoveActiveWindowToBottom);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad3, MoveActiveWindowToBottomRight);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad4, MoveActiveWindowToLeft);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad5, MoveActiveWindowToCenter);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad6, MoveActiveWindowToRight);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad7, MoveActiveWindowToTopLeft);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad8, MoveActiveWindowToTop);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad9, MoveActiveWindowToTopRight);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.D1, RestoreLayout1);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.D2, RestoreLayout2);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.D3, RestoreLayout3);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift), Keys.D1, SaveLayout1);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift), Keys.D2, SaveLayout2);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift), Keys.D3, SaveLayout3);
            }
        }

        private static void DisableSnapHotKeys()
        {
            Log.I(snapHotKeysEnabled ? "Disabled" : "Did Nothing");
            if (snapHotKeysEnabled)
            {
                snapHotKeysEnabled = false;
                //snap window to zones
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad1);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad2);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad3);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad4);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad5);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad6);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad7);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad8);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad9);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.D1);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.D2);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.D3);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift), Keys.D1);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift), Keys.D2);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift), Keys.D3);
            }
        }

        private static void EnableSnapAltHotKeys()
        {
            Log.I(!snapAltHotKeysEnabled ? "Enabled" : "Did Nothing");
            if (!snapAltHotKeysEnabled)
            {
                snapAltHotKeysEnabled = true;
                //snap window to zones - alternate keys
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.Oemcomma, MoveActiveWindowToBottomLeft);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.OemPeriod, MoveActiveWindowToBottom);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.OemQuestion, MoveActiveWindowToBottomRight);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.K, MoveActiveWindowToLeft);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.L, MoveActiveWindowToCenter);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.OemSemicolon, MoveActiveWindowToRight);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.I, MoveActiveWindowToTopLeft);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.O, MoveActiveWindowToTop);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.P, MoveActiveWindowToTopRight);
            }
        }

        private static void DisableSnapAltHotKeys()
        {
            Log.I(snapAltHotKeysEnabled ? "Disabled" : "Did Nothing");
            if (snapAltHotKeysEnabled)
            {
                snapAltHotKeysEnabled = false;
                //snap window to zones - alternate keys
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.Oemcomma);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.OemPeriod);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.OemQuestion);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.K);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.L);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.OemSemicolon);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.I);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.O);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.P);
            }
        }

        private static void EnableMiscHotKeys()
        {
            Log.I(!miscHotKeysEnabled ? "Enabled" : "Did Nothing");
            if (!miscHotKeysEnabled)
            {
                miscHotKeysEnabled = true;
                //move window between monitors
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Shift), Keys.A, MoveActiveWindowToLeftScreen);
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Shift), Keys.S, MoveActiveWindowToRightScreen);
                //Adjust window transprency
                HotKeyManager.RegisterHotKey((KeyModifiers.Control | KeyModifiers.Alt), Keys.Add, IncreaseWindowTranspancy);
                HotKeyManager.RegisterHotKey((KeyModifiers.Control | KeyModifiers.Alt), Keys.Subtract, DecreaseWindowTranspancy);
                //send windw to back
                HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.W, SendActiveWindowToBack);
            }
        }

        private static void DisableMiscHotKeys()
        {
            Log.I(miscHotKeysEnabled ? "Disabled" : "Did Nothing");
            if (miscHotKeysEnabled)
            {
                miscHotKeysEnabled = false;
                //move window between monitors
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Shift), Keys.A);
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Shift), Keys.S);
                //Adjust window transprency
                HotKeyManager.UnregisterHotKey((KeyModifiers.Control | KeyModifiers.Alt), Keys.Add);
                HotKeyManager.UnregisterHotKey((KeyModifiers.Control | KeyModifiers.Alt), Keys.Subtract);
                //send windw to back
                HotKeyManager.UnregisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.W);
            }
        }
        #endregion

        #region External functions
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
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

        //SetWindowPos flags
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOZORDER = 0x0004;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int SWP_SHOWWINDOW = 0x0040;
        //SetWindowPos zlayer flags
        private const short HWND_BOTTOM = 1;
        private const short HWND_NOTOPMOST = -2;
        private const short HWND_TOP = 0;
        private const short HWND_TOPMOST = -1;
        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

        //ShowWindow flags
        private const short SW_SHOWNORMAL = 1;
        [DllImport("user32.dll", EntryPoint = "ShowWindow")]
        public static extern bool ShowWindow(IntPtr hWnd, int wFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        //SetLayeredWindowAttributes - layered window attributes
        public const int LWA_COLORKEY = 0x1;
        public const int LWA_ALPHA = 0x2;
        [DllImport("user32.dll")]
        static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        //GetWindowStyle - get extended window styles
        public const int GWL_EXSTYLE = -20;
        [DllImport("user32.dll", SetLastError = true)]
        static extern UInt32 GetWindowLong(IntPtr hWnd, int nIndex);

        //extended window styles - layered
        public const int WS_EX_LAYERED = 0x80000;
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, UInt32 dwNewLong);
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

        #region Window movement & snap (control) logic
        public static void MoveActiveWindowTo(int x, int y, bool includePosOffset = true)
        {
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

                SetWindowPos(handle, HWND_TOP, x, y, cx, cy, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
        }

        //if moving window on same screen you need to offset its pos. If moving window between screens the x,y pos should already be know and not need to be re-offset
        private static void MoveActiveWindowTo(int x, int y, int newWidth, int newHeight, bool includePosOffset=true)
        {
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

                ShowWindow(handle, SW_SHOWNORMAL);
                SetWindowPos(handle, HWND_TOP, x, y, newWidth, newHeight, SWP_NOZORDER | SWP_SHOWWINDOW);
            }
        }

        private static void SetWindowTransparancy(int d)
        {
            byte a;
            IntPtr handle = GetForegroundWindow();

            if (lastWindowAlphaHandle == handle)
            {//new change to last window
                if (d < 0)
                {
                    a = (byte)(lastWindowAlpha - WindowAlphaIncrement);
                    if (a > lastWindowAlpha) a = 0;//set to 0 and dont wrap
                }
                else
                {
                    a = (byte)(lastWindowAlpha + WindowAlphaIncrement);
                    if (a < lastWindowAlpha) a = 255;//set to 255 and dont wrap
                }
            }
            else
            {//new window
                if (d < 0) a = 255 - WindowAlphaIncrement;
                else a = 255;
            }
            if (a < MinWindowAlpha) a = MinWindowAlpha;
            //Log.Log("set alpha " + a + " on " + handle, LogLevel.Information);

            //Enable extended layered style on window if not enabled
            SetWindowLong(handle, GWL_EXSTYLE, GetWindowLong(handle, GWL_EXSTYLE) | WS_EX_LAYERED);
            //set window transparency
            SetLayeredWindowAttributes(handle, 0, a, LWA_ALPHA);

            lastWindowAlpha = a;
            lastWindowAlphaHandle = handle;
        }

        private static void SendActiveWindowToBack()
        {
            IntPtr handle = GetForegroundWindow();
            if (handle != IntPtr.Zero)
            {
                SetWindowPos(handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
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
                    snapAreas = new RectangleF[CornerSnapSizes.Count];
                    CornerSnapSizes.CopyTo(snapAreas);
                    break;
                case SnapDirection.Top:
                case SnapDirection.Bottom:
                    snapAreas = new RectangleF[TopSnapSizes.Count];
                    TopSnapSizes.CopyTo(snapAreas);
                    break;
                case SnapDirection.Left:
                case SnapDirection.Right:
                    snapAreas = new RectangleF[SideSnapSizes.Count];
                    SideSnapSizes.CopyTo(snapAreas);
                    break;
                case SnapDirection.Center:
                    snapAreas = new RectangleF[CenterSnapSizes.Count];
                    CenterSnapSizes.CopyTo(snapAreas);
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
                case SnapDirection.Center:
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
                case SnapDirection.Center:
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

        #region HotKey Functions
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

        public static void MoveActiveWindowToCenter(object o, HotKeyEventArgs args)
        {
            SnapActiveWindow(SnapDirection.Center);
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

        public static void SendActiveWindowToBack(object sender = null, HotKeyEventArgs e = null)
        {
            SendActiveWindowToBack();
        }

        public static void IncreaseWindowTranspancy(object sender = null, HotKeyEventArgs e = null)
        {
            SetWindowTransparancy(1);
        }

        public static void DecreaseWindowTranspancy(object sender = null, HotKeyEventArgs e = null)
        {
            SetWindowTransparancy(-1);
        }

        public static void SaveLayout1(object sender = null, HotKeyEventArgs e = null)
        {
            SaveAllWindowPositions(1);
        }

        public static void RestoreLayout1(object sender = null, HotKeyEventArgs e = null)
        {
            RestoreAllWindowPositions(1);
        }

        public static void SaveLayout2(object sender = null, HotKeyEventArgs e = null)
        {
            SaveAllWindowPositions(2);
        }

        public static void RestoreLayout2(object sender = null, HotKeyEventArgs e = null)
        {
            RestoreAllWindowPositions(2);
        }

        public static void SaveLayout3(object sender = null, HotKeyEventArgs e = null)
        {
            SaveAllWindowPositions(3);
        }

        public static void RestoreLayout3(object sender = null, HotKeyEventArgs e = null)
        {
            RestoreAllWindowPositions(3);
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

        #region Enumerate windows
        // Delegate to filter which windows to include
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        /// <summary> Get the text for the window pointed to by hWnd </summary>
        public static string GetWindowText(IntPtr hWnd)
        {
            int size = GetWindowTextLength(hWnd);
            if (size > 0)
            {
                var builder = new StringBuilder(size + 1);
                GetWindowText(hWnd, builder, builder.Capacity);
                return builder.ToString();
            }

            return String.Empty;
        }

        /// <summary> Find all windows that match the given filter </summary>
        /// <param name="filter"> A delegate that returns true for windows
        ///    that should be returnThis and false for windows that should
        ///    not be returnThis </param>
        public static IEnumerable<IntPtr> FindWindows(EnumWindowsProc filter)
        {
            IntPtr found = IntPtr.Zero;
            List<IntPtr> windows = new List<IntPtr>();

            EnumWindows(delegate(IntPtr wnd, IntPtr param)
            {
                if (filter(wnd, param))
                {
                    // only add the windows that pass the filter
                    windows.Add(wnd);
                }

                // but return true here so that we iterate all windows
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        /// <summary> Find all windows that contain the given title text </summary>
        /// <param name="titleText"> The text that the window title must contain. </param>
        public static IEnumerable<IntPtr> FindWindowsWithText(string titleText)
        {
            return FindWindows(delegate(IntPtr wnd, IntPtr param)
            {
                return GetWindowText(wnd).Contains(titleText);
            });
        }

        /// <summary> Find all windows that contain the given title text </summary>
        /// <param name="titleText"> The text that the window title must contain. </param>
        public static IEnumerable<IntPtr> FindAllVisibleWindows()
        {
            var shell = GetShellWindow();
            return FindWindows(delegate(IntPtr wnd, IntPtr param)
            {
                bool returnThis = wnd != shell;
                if (returnThis) returnThis = IsWindowVisible(wnd);
                if (returnThis) returnThis = GetWindowTextLength(wnd) > 0;
                return returnThis;
            });
        }
        #endregion

        #region Save & Restore all window positions
        public static void SaveLayout0(object sender = null, EventArgs e = null)
        {
			//called from menu - save to layout 0
            SaveAllWindowPositions(0);
        }

        public static void RestoreLayout0(object sender = null, EventArgs e = null)
        {
			//called from menu - save to layout 0
            RestoreAllWindowPositions(0);
        }

        public static void SaveAllWindowPositions(int saveSlot)
        {
			if(saveSlot<LAYOUT_COUNT)
            {
                layouts[saveSlot].Capture();
                layouts[saveSlot].SaveToDisk(saveSlot);
                Log.I("Saved " + layouts[saveSlot].WindowCount + " window positions to slot#" + saveSlot);
                FreshTools.GetNotifyIcon().ShowBalloonTip(750, "FreshTools", layouts[saveSlot].WindowCount + " Windows saved to layout#" + saveSlot, ToolTipIcon.None);
			}
        }

        public static void RestoreAllWindowPositions(int saveSlot)
        {
			if(saveSlot<LAYOUT_COUNT)
            {
				layouts[saveSlot].Restore();
                Log.I("Restored " + layouts[saveSlot].WindowCount + " window positions from slot#" + saveSlot);
                FreshTools.GetNotifyIcon().ShowBalloonTip(75, "FreshTools", layouts[saveSlot].WindowCount + " Windows restored from layout#" + saveSlot, ToolTipIcon.None);
			}
        }

        private class WindowInfo
        {
            public IntPtr Handle;
            public Rectangle Rectangle;
            public string Text;

            public WindowInfo(IntPtr hwnd)
            {
                Handle = hwnd;

                Text = GetWindowText(Handle);

                Rect rect = default(Rect);
                GetWindowRect(Handle, ref rect);
                Rectangle = rect.ToRectangle();
            }

            public bool RestorePosition()
            {
                const short SWP_NOSIZE = 0;
                //const short SWP_NOMOVE = 0X2;
                const short SWP_NOZORDER = 0X4;
                const int SWP_SHOWWINDOW = 0x0040;

                if (Handle != IntPtr.Zero)
                {
                    return SetWindowPos(Handle, 0, Rectangle.X, Rectangle.Y, Rectangle.Width, Rectangle.Height, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW);
                }
                return false;
            }

            public string SaveString()
            {
                return Rectangle.X + "," + Rectangle.Y + "," + Rectangle.Width + "," + Rectangle.Height + "," + Text;
            }

            public override string ToString()
            {
                return "WindowInfo() - " + Text + " {" + Rectangle.X + "," + Rectangle.Y + "," + Rectangle.Width + "," + Rectangle.Height + "}";
            }
        }
		
		//stores all window possitions
		private class Layout
		{
			public List<WindowInfo> WindowInfos = new List<WindowInfo>();
			public int WindowCount  { get { return WindowInfos.Count; } }
			
			public Layout()
			{
				WindowInfos = new List<WindowInfo>();
			}
			
			public void Capture()
			{
				var windows = WindowManager.FindAllVisibleWindows();
				WindowInfos.Clear();

				foreach (IntPtr w in windows)
				{
					WindowInfo wInfo = new WindowInfo(w);
					WindowInfos.Add(wInfo);
				}
			}

            public void SaveToDisk(int slot)
            {
                string saveData = "";
                foreach (WindowInfo wi in WindowInfos)
                {
                    saveData += wi.SaveString() + "\n";
                }
                saveData.Trim();

                string fileName = LayoutSaveFileBaseName + slot + ".txt";
                Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                File.WriteAllText(fileName, saveData);

            }
			
			public void Restore()
			{
				int successCount = 0;
				foreach (WindowInfo i in WindowInfos)
				{
					if (i.RestorePosition())
						successCount++;
				}
			}
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
            Center,
        }
    }
}
