using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Threading;
using System.Xml.Linq;

namespace FreshTools
{
    /// <summary>
    /// Created as a Windows 10 replacement for the discontinued winsplit revolution
    /// </summary>
    public static class WindowManager
    {   
        private const float ComparisonRoundingLimit = 0.003f;//this will need to be broader for lower resolutions since they have less pixes to round to

        //public ajustable settings
        public static bool WrapLeftRightScreens = true;
        public const bool SnapHotKeysEnabled_Default = true;
        public const bool SnapAltHotKeysEnabled_Default = false;
        public const bool MiscHotKeysEnabled_Default = true;
        public static string OffsetWindowTitles_WhiteList_String = "Google Chrome,Firefox"; //windows that wont use offset adjustment
        public static string OffsetWindowTitles_BlackList_String = "Microsoft Teams,Outlook,WhatsApp,Visual Studio,Excel,Word,Slack"; //windows that will use offset adjustment

        private static string[] OffsetWindowTitles_WhiteList;
        private static string[] OffsetWindowTitles_BlackList;

        public static bool SnapHotKeysEnabled { get { return snapHotKeysEnabled; } set { if (value)EnableSnapHotKeys(); else DisableSnapHotKeys(); } }
        public static bool SnapAltHotKeysEnabled { get { return snapAltHotKeysEnabled; } set { if (value)EnableSnapAltHotKeys(); else DisableSnapAltHotKeys(); } }
        public static bool MiscHotKeysEnabled { get { return miscHotKeysEnabled; } set { if (value)EnableMiscHotKeys(); else DisableMiscHotKeys(); } }
        private static List<HotKey> snapHotKeys = new List<HotKey>();
        private static List<HotKey> snapAltHotKeys = new List<HotKey>();
        private static List<HotKey> miscHotKeys = new List<HotKey>();

        //private local variables
        private static bool snapHotKeysEnabled = false;
        private static bool snapAltHotKeysEnabled = false;
        private static bool miscHotKeysEnabled = false;

        //window info for saving and restoring window possitions
        private const int LAYOUT_MAX_SUPPORTED_MONITOR_COUNT = 6;// only supported up to 6? monitors
        private const int LAYOUT_HOTKEY_COUNT = 3;
        private const int LAYOUT_COUNT = 1 + LAYOUT_HOTKEY_COUNT + LAYOUT_MAX_SUPPORTED_MONITOR_COUNT;//menu and hotkeys 1-3 and 1 for each distinct monitor count
        private static Layout[] layouts = new Layout[LAYOUT_COUNT];
        private static Layout backupLayout; // spare layout when a layout is loaded
        private static Layout lastSavedLayout; // lastSavedLayout - used to compare if new one is different
        private static string LayoutSaveFileBaseName = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\" + Assembly.GetExecutingAssembly().GetName().Name + @"\windowLayouts\layout";
        private static bool layoutSnapshotThreadRunning = true;// start by default
        private static Thread layoutSnapshotThread;
        private static int layoutSnapshotThread_sleepTime = 30*1000;

        //snap region sizes
        const int SnapSizeMaxCount = 9;
        public static List<RectangleF> CornerSnapSizes;
        public static List<RectangleF> TopSnapSizes;
        public static List<RectangleF> SideSnapSizes;
        public static List<RectangleF> CenterSnapSizes;

        //these offsets are callibrated for my 2560x1440 monitors, not sure if they are the same on other resolutions or zoom levels
        private static Point positionOffsetMain = new Point(-7, 0);
        private static Point resizeOffsetMain = new Point(14, 7);

        //alpha control variables
        private static IntPtr lastWindowAlphaHandle = IntPtr.Zero;
        private static byte lastWindowAlpha = 0;
        private const byte WindowAlphaIncrement = 16;
        private const byte MinWindowAlpha = 16;

        #region Setup and teardown
        static WindowManager()
        {
			for(int i=0;i<LAYOUT_COUNT;i++)
				layouts[i] = new Layout();
			
            if (!FreshArchives.IsWindows10())
            {
                positionOffsetMain = new Point(0, 0);
                resizeOffsetMain = new Point(0, 0);
            }
            LoadSnapSizes();
            LoadLayoutsFromDisk();
        }

        public static void Startup()
        {
            if (layoutSnapshotThreadRunning)
                StartLayoutSnapshotThread();
        }

        public static void Teardown()
        {
            StopLayoutSnapshotThread();
        }

        public static void StartLayoutSnapshotThread()
        {
            layoutSnapshotThreadRunning = true;
            if(layoutSnapshotThread==null)
            {
                Log.I("Layout Snapshot Thread Started - sleep " + layoutSnapshotThread_sleepTime);
                layoutSnapshotThread = new Thread(new ThreadStart(LayoutSnapshotThreadRun));
                layoutSnapshotThread.Start();
            }
            //startProcessItem.Text = "Stop Backup Thread";
        }

        public static void StopLayoutSnapshotThread()
        {
            layoutSnapshotThreadRunning = false;
            Log.I("Backup Thread Stopped");
            if (layoutSnapshotThread != null) layoutSnapshotThread.Interrupt();
            layoutSnapshotThread = null;
            //startProcessItem.Text = "Start Backup Thread";
        }
        
        private static void LayoutSnapshotThreadRun()
        {
            int monitorCount = Screen.AllScreens.Length;
            try
            {
                while (layoutSnapshotThreadRunning)
                {
                    if(monitorCount != Screen.AllScreens.Length)
                    {//screen Count Changed - auto restore
                        RestoreLayoutAuto();
                        monitorCount = Screen.AllScreens.Length;
                    }
                    SaveLayoutAuto(layoutSnapshotThread);
                    Thread.Sleep(layoutSnapshotThread_sleepTime);
                }
            }
            catch (ThreadInterruptedException ex)
            {
                Log.E($"Caught ThreadInterruptedException - {ex}");
            }
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

        public static void SaveTitleLists(VariablesFile settingsFile)
        {
            string whiteList = WindowManager.OffsetWindowTitles_WhiteList_String;
            WindowManager.OffsetWindowTitles_WhiteList_String = settingsFile.variables.GetVariable("WindowTitleOffset_Whitelist", ref whiteList, true).String;
            string blackList = WindowManager.OffsetWindowTitles_BlackList_String;
            WindowManager.OffsetWindowTitles_BlackList_String = settingsFile.variables.GetVariable("WindowTitleOffset_Blacklist", ref blackList, true).String;

            OffsetWindowTitles_WhiteList = OffsetWindowTitles_WhiteList_String.Split(',');
            OffsetWindowTitles_BlackList = OffsetWindowTitles_BlackList_String.Split(',');
        }

        public static void LoadHotKeys(VariablesFile settingsFile)
        {
            //modifiers |=NoRepeat, #=Windows, !=Alt, ^=Ctrl, +=Shift
            //based on autohotkey modifiers https://autohotkey.com/docs/Hotkeys.htm
            //and strait conversion from the Keys Enum

            HotKey hk;

            //snap hotkeys - with defaults
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_SnapActiveWindowToBottomLeft", "|^!NumPad1").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToBottomLeft));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_SnapActiveWindowToBottom", "|^!NumPad2").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToBottom));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_SnapActiveWindowToBottomRight", "|^!NumPad3").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToBottomRight));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_SnapActiveWindowToLeft", "|^!NumPad4").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToLeft));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_SnapActiveWindowToCenter", "|^!NumPad5").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToCenter));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_SnapActiveWindowToRight", "|^!NumPad6").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToRight));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_SnapActiveWindowToTopLeft", "|^!NumPad7").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToTopLeft));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_SnapActiveWindowToTop", "|^!NumPad8").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToTop));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_SnapActiveWindowToTopRight", "|^!NumPad9").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToTopRight));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_SaveLayout1", "|^!+D1").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SaveLayout1));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_SaveLayout2", "|^!+D2").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SaveLayout2));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_SaveLayout3", "|^!+D3").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SaveLayout3));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_RestoreLayout1", "|^!D1").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, RestoreLayout1));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_RestoreLayout2", "|^!D2").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, RestoreLayout2));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_RestoreLayout3", "|^!D3").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, RestoreLayout3));
            //corner move hotkeys - with defaults
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_MoveActiveWindowToBottomLeft", "|^!End").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, MoveActiveWindowToBottomLeft));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_MoveActiveWindowToBottomRight", "|^!PageDown").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, MoveActiveWindowToBottomRight));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_MoveActiveWindowToTopLeft", "|^!Home").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, MoveActiveWindowToTopLeft));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_MoveActiveWindowToTopRight", "|^!PageUp").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, MoveActiveWindowToTopRight));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_MoveActiveWindowToCenter", "|^!Multiply").String, out hk))
                snapHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, MoveActiveWindowToCenter));

            //altsnap hotkeys - with defaults
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_Alt_SnapActiveWindowToBottomLeft", "|^!Oemcomma").String, out hk))
                snapAltHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToBottomLeft));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_Alt_SnapActiveWindowToBottom", "|^!OemPeriod").String, out hk))
                snapAltHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToBottom));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_Alt_SnapActiveWindowToBottomRight", "|^!OemQuestion").String, out hk))
                snapAltHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToBottomRight));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_Alt_SnapActiveWindowToLeft", "|^!K").String, out hk))
                snapAltHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToLeft));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_Alt_SnapActiveWindowToCenter", "|^!L").String, out hk))
                snapAltHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToCenter));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_Alt_SnapActiveWindowToRight", "|^!OemSemicolon").String, out hk))
                snapAltHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToRight));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_Alt_SnapActiveWindowToTopLeft", "|^!I").String, out hk))
                snapAltHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToTopLeft));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_Alt_SnapActiveWindowToTop", "|^!O").String, out hk))
                snapAltHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToTop));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_Alt_SnapActiveWindowToTopRight", "|^!P").String, out hk))
                snapAltHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SnapActiveWindowToTopRight));

            //misc hotkeys - with defaults
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_MoveActiveWindowToLeftScreen", "|^+A").String, out hk))
                miscHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, MoveActiveWindowToLeftScreen));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_MoveActiveWindowToRightScreen", "|^+S").String, out hk))
                miscHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, MoveActiveWindowToRightScreen));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_IncreaseWindowTransparency", "^!Add").String, out hk))
                miscHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, IncreaseWindowTransparency));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_DecreaseWindowTransparency", "^!Subtract").String, out hk))
                miscHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, DecreaseWindowTransparency));
            if (HotKey.TryParseHotKey(settingsFile.variables.GetVariable("HotKey_SendActiveWindowToBack", "|^!W").String, out hk))
                miscHotKeys.Add(new HotKey(hk.Modifiers, hk.Key, SendWindowToBack));


            //run enablers
            bool t = SnapHotKeysEnabled;
            SnapHotKeysEnabled = false;
            SnapHotKeysEnabled = t;
            t = SnapAltHotKeysEnabled;
            SnapAltHotKeysEnabled = false;
            SnapAltHotKeysEnabled = t;
            t = MiscHotKeysEnabled;
            MiscHotKeysEnabled = false;
            MiscHotKeysEnabled = t;
        }

        private static void EnableSnapHotKeys()
        {
            Log.I(!snapHotKeysEnabled ? "Enabled" : "Did Nothing");
            if (!snapHotKeysEnabled)
            {
                snapHotKeysEnabled = true;
                //register hotkeys
                foreach(HotKey hk in snapHotKeys)
                {
                    HotKeyManager.RegisterHotKey(hk.Modifiers, hk.Key,hk.GenericHandler);
                }
            }
        }

        private static void DisableSnapHotKeys()
        {
            Log.I(snapHotKeysEnabled ? "Disabled" : "Did Nothing");
            if (snapHotKeysEnabled)
            {
                snapHotKeysEnabled = false;
                //unregister hotkeys
                foreach (HotKey hk in snapHotKeys)
                {
                    HotKeyManager.UnregisterHotKey(hk.Modifiers, hk.Key);
                }
            }
        }

        private static void EnableSnapAltHotKeys()
        {
            Log.I(!snapAltHotKeysEnabled ? "Enabled" : "Did Nothing");
            if (!snapAltHotKeysEnabled)
            {
                snapAltHotKeysEnabled = true;
                //register hotkeys
                foreach (HotKey hk in snapAltHotKeys)
                {
                    HotKeyManager.RegisterHotKey(hk.Modifiers, hk.Key, hk.GenericHandler);
                }
            }
        }

        private static void DisableSnapAltHotKeys()
        {
            Log.I(snapAltHotKeysEnabled ? "Disabled" : "Did Nothing");
            if (snapAltHotKeysEnabled)
            {
                snapAltHotKeysEnabled = false;
                //unregister hotkeys
                foreach (HotKey hk in snapAltHotKeys)
                {
                    HotKeyManager.UnregisterHotKey(hk.Modifiers, hk.Key);
                }
            }
        }

        private static void EnableMiscHotKeys()
        {
            Log.I(!miscHotKeysEnabled ? "Enabled" : "Did Nothing");
            if (!miscHotKeysEnabled)
            {
                miscHotKeysEnabled = true;
                //register hotkeys
                foreach (HotKey hk in miscHotKeys)
                {
                    HotKeyManager.RegisterHotKey(hk.Modifiers, hk.Key, hk.GenericHandler);
                }
            }
        }

        private static void DisableMiscHotKeys()
        {
            Log.I(miscHotKeysEnabled ? "Disabled" : "Did Nothing");
            if (miscHotKeysEnabled)
            {
                miscHotKeysEnabled = false;
                //unregister hotkeys
                foreach (HotKey hk in miscHotKeys)
                {
                    HotKeyManager.UnregisterHotKey(hk.Modifiers, hk.Key);
                }
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
            MoveWindowOffScreenInDirection(GetForegroundWindow(), new Point(1,0));
        }

        public static void MoveActiveWindowToLeftScreen(object o, HotKeyEventArgs args)
        {
            MoveWindowOffScreenInDirection(GetForegroundWindow(), new Point(-1,0));
        }

        /// <summary>
        /// Move this window off this screen in the given direction eg (1,0) for right.
        /// </summary>
        /// <param name="dir">Direction to move window in units of the active screens' resolution.</param>
        public static void MoveWindowOffScreenInDirection(IntPtr handle, Point dir)
        {
            if(Screen.AllScreens.Length==1)return;
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

            Log.V("MoveWindowOffScreenInDirection(\"" + GetWindowText(handle) + "\"," + dir + ")");
            MoveWindowToScreen(handle, newScreen);
        }

        /// <summary>
        /// Move window to new screen and scale it as necisarry
        /// </summary>
        /// <param name="newScreen"></param>
        public static void MoveWindowToScreen(IntPtr handle, Screen newScreen)
        {
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
                Point resizeOffset = GetResizeOffsetForWindowByTitle(GetWindowText(handle));
                //different size working area/resolution
                //scale window to new resolution
                double widthPercentage = 1.0 * (rect.Width - resizeOffset.X) / currentWorkingArea.Width;
                double heightPercentage = 1.0 * (rect.Height - resizeOffset.Y) / currentWorkingArea.Height;
                int newWidth = (int)(newScreen.WorkingArea.Width * widthPercentage);
                int newHeight = (int)(newScreen.WorkingArea.Height * heightPercentage);
                newWidth += resizeOffset.X;
                newHeight += resizeOffset.Y;
                MoveWindowTo(handle, newX, newY, newWidth, newHeight, false);
            }
            else
                MoveWindowTo(handle, newX, newY, false);

        }

        public static Point GetResizeOffsetForWindowByTitle(string title)
        {
            return GetOffsetForWindowByTitle(title, false);
        }

        public static Point GetPositionOffsetForWindowByTitle(string title)
        {
            return GetOffsetForWindowByTitle(title, true);
        }

        public static Point GetOffsetForWindowByTitle(string title, bool position)
        {
            //need to use regular 7p offset for certain applications and no offset for other. Seems to be new/microsoft application that use a new window backend. very anoying
            bool whiteListed = false;
            foreach (var titlePart in OffsetWindowTitles_WhiteList)// ignore sub titles from web browsers
            {
                if (title.Contains(titlePart))
                {
                    whiteListed = true;
                    break;
                }
            }
            bool blackListed = false;
            foreach (var titlePart in OffsetWindowTitles_BlackList)
            {
                if (title.Contains(titlePart))
                {
                    blackListed = true;
                    break;
                }
            }

            bool useOffset = whiteListed || !blackListed;
            
            if (position)
                if (useOffset) return positionOffsetMain;
                else return new Point(0, 0);
            else
                if (useOffset) return resizeOffsetMain;
                else return new Point(0, 0);
        }

        #endregion

        #region Window movement & snap (control) logic
        public static void MoveWindowTo(IntPtr handle, int x, int y, bool includePosOffset = true)
        {
            const int cx = 0;
            const int cy = 0;

            if (handle != IntPtr.Zero)
            {
                if (includePosOffset)
                {
                    Point positionOffset = GetPositionOffsetForWindowByTitle(GetWindowText(handle));
                    x += positionOffset.X;
                    y += positionOffset.Y;
                }

                Log.V("MoveWindowTo(\"" + GetWindowText(handle) + "\"," + x + "," + y + ")");
                SetWindowPos(handle, HWND_TOP, x, y, cx, cy, SWP_NOZORDER | SWP_NOSIZE | SWP_SHOWWINDOW);
            }
        }

        //if moving window on same screen you need to offset its pos. If moving window between screens the x,y pos should already be know and not need to be re-offset
        private static void MoveWindowTo(IntPtr handle, int x, int y, int w, int h, bool includePosOffset=true)
        {
            if (handle != IntPtr.Zero)
            {
                if (includePosOffset)
                {
                    Point positionOffset = GetPositionOffsetForWindowByTitle(GetWindowText(handle));
                    Point resizeOffset = GetResizeOffsetForWindowByTitle(GetWindowText(handle));
                    x += positionOffset.X;
                    y += positionOffset.Y;

                    w += resizeOffset.X;
                    h += resizeOffset.Y;
                }

                Log.V("MoveWindowTo(\"" + GetWindowText(handle) + "\"," + x + "," + y + "," + w + "," + h + ")");
                ShowWindow(handle, SW_SHOWNORMAL);
                SetWindowPos(handle, HWND_TOP, x, y, w, h, SWP_NOZORDER | SWP_SHOWWINDOW);
            }
        }

        private static void SetWindowTransparency(IntPtr handle, int d)
        {
            byte a;
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
            //set window Transparency
            SetLayeredWindowAttributes(handle, 0, a, LWA_ALPHA);

            lastWindowAlpha = a;
            lastWindowAlphaHandle = handle;
        }

        private static void SendWindowToBack(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
            {
                SetWindowPos(handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
            }
        }

        private static void SnapWindow(IntPtr handle, SnapDirection dir)
        {
            RectangleF relativeRectangle = GetWindowRelativeRectangleF(handle);
            Rectangle workingArea = GetScreenWindowIsOn(handle).WorkingArea;

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
                if(CloseEnough(relativeRectangle, snapAreas[i]))
                {
                    snapIndex = ++i;
                    break;
                }
            }

            int newX = workingArea.X + (int)(snapAreas[snapIndex].X * workingArea.Width);
            int newY = workingArea.Y + (int)(snapAreas[snapIndex].Y * workingArea.Height);
            int newW = (int)(snapAreas[snapIndex].Width * workingArea.Width);
            int newH = (int)(snapAreas[snapIndex].Height * workingArea.Height);

            Log.V("SnapWindow(\"" + GetWindowText(handle) + "\"," + dir + ")  -> (" + newX + "," + newY + "," + newW + "," + newH + ") - snapIndex:" + snapIndex + " was at relative " + relativeRectangle +"");


            MoveWindowTo(handle, newX, newY, newW, newH);
        }


        private static void MoveWindowTo(IntPtr handle, SnapDirection dir)
        {
            //keep window size and move to a given corner or center
            if (!(dir == SnapDirection.TopLeft || dir == SnapDirection.TopRight || dir == SnapDirection.BottomLeft || dir == SnapDirection.BottomRight || dir == SnapDirection.Center))
                return;

            Rect windowRect = new Rect();
            GetWindowRect(handle, ref windowRect);
            Point positionOffset = GetPositionOffsetForWindowByTitle(GetWindowText(handle));
            Point resizeOffset = GetResizeOffsetForWindowByTitle(GetWindowText(handle));
            Rectangle workingArea = GetScreenWindowIsOn(handle).WorkingArea;

            float newX = workingArea.X;
            float newY = workingArea.Y;

            //set new X
            if (dir == SnapDirection.TopRight || dir == SnapDirection.BottomRight)
                newX += workingArea.Width - windowRect.Width + resizeOffset.X;
            //set new Y
            if (dir == SnapDirection.BottomLeft || dir == SnapDirection.BottomRight)
                newY += workingArea.Height - windowRect.Height + resizeOffset.Y;

            if (dir == SnapDirection.Center)
            {
                newX += workingArea.Width/2 - (windowRect.Width + resizeOffset.X)/2;
                newY += workingArea.Height/2 - (windowRect.Height + resizeOffset.Y)/2;
            }

            MoveWindowTo(handle, (int)newX, (int)newY);
        }
        #endregion

        #region HotKey Functions
        #region Snap active window screen to all 8 directions
        public static void SnapActiveWindowToTop(object o, HotKeyEventArgs args)
        {
            SnapWindow(GetForegroundWindow(), SnapDirection.Top);
        }

        public static void SnapActiveWindowToBottom(object o, HotKeyEventArgs args)
        {
            SnapWindow(GetForegroundWindow(), SnapDirection.Bottom);
        }

        public static void SnapActiveWindowToLeft(object o, HotKeyEventArgs args)
        {
            SnapWindow(GetForegroundWindow(), SnapDirection.Left);
        }

        public static void SnapActiveWindowToCenter(object o, HotKeyEventArgs args)
        {
            SnapWindow(GetForegroundWindow(), SnapDirection.Center);
        }

        public static void SnapActiveWindowToRight(object o, HotKeyEventArgs args)
        {
            SnapWindow(GetForegroundWindow(), SnapDirection.Right);
        }

        public static void SnapActiveWindowToTopLeft(object o, HotKeyEventArgs args)
        {
            SnapWindow(GetForegroundWindow(), SnapDirection.TopLeft);
        }

        public static void SnapActiveWindowToTopRight(object o, HotKeyEventArgs args)
        {
            SnapWindow(GetForegroundWindow(), SnapDirection.TopRight);
        }

        public static void SnapActiveWindowToBottomLeft(object o, HotKeyEventArgs args)
        {
            SnapWindow(GetForegroundWindow(), SnapDirection.BottomLeft);
        }

        public static void SnapActiveWindowToBottomRight(object o, HotKeyEventArgs args)
        {
            SnapWindow(GetForegroundWindow(), SnapDirection.BottomRight);
        }
        #endregion

        #region Move active window to all 4 corners & center
        public static void MoveActiveWindowToTopLeft(object o, HotKeyEventArgs args)
        {
            MoveWindowTo(GetForegroundWindow(), SnapDirection.TopLeft);
        }

        public static void MoveActiveWindowToTopRight(object o, HotKeyEventArgs args)
        {
            MoveWindowTo(GetForegroundWindow(), SnapDirection.TopRight);
        }

        public static void MoveActiveWindowToBottomLeft(object o, HotKeyEventArgs args)
        {
            MoveWindowTo(GetForegroundWindow(), SnapDirection.BottomLeft);
        }

        public static void MoveActiveWindowToBottomRight(object o, HotKeyEventArgs args)
        {
            MoveWindowTo(GetForegroundWindow(), SnapDirection.BottomRight);
        }

        public static void MoveActiveWindowToCenter(object o, HotKeyEventArgs args)
        {
            MoveWindowTo(GetForegroundWindow(), SnapDirection.Center);
        }
        #endregion

        public static void SendWindowToBack(object sender = null, HotKeyEventArgs e = null)
        {
            SendWindowToBack(GetForegroundWindow());
        }

        public static void IncreaseWindowTransparency(object sender = null, HotKeyEventArgs e = null)
        {
            SetWindowTransparency(GetForegroundWindow(), 1);
        }

        public static void DecreaseWindowTransparency(object sender = null, HotKeyEventArgs e = null)
        {
            SetWindowTransparency(GetForegroundWindow() , - 1);
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

        public static void RestoreLayoutAuto(object sender = null, HotKeyEventArgs e = null)
        {
            int monitorCount = Screen.AllScreens.Length;
            RestoreAllWindowPositions(LAYOUT_HOTKEY_COUNT + monitorCount, true);
        }
        #endregion

        #region Calculate Screen(s) info and Generics
        /// <summary>
        /// Returns the screen containing the window
        /// </summary>
        /// <returns></returns>
        public static Screen GetScreenWindowIsOn(IntPtr handle)
        {
            Rect rect = new Rect();
            GetWindowRect(handle, ref rect);
            Rectangle childRect = rect.ToRectangle();

            return GetScreenContainingWindow(childRect);
        }

        public static RectangleF GetWindowRelativeRectangleF(IntPtr handle)
        {
            Rect rect = new Rect();
            GetWindowRect(handle, ref rect);
            Point positionOffset = GetPositionOffsetForWindowByTitle(GetWindowText(handle));
            Point resizeOffset = GetResizeOffsetForWindowByTitle(GetWindowText(handle));
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
        public static void SaveLayoutAuto(object sender = null, EventArgs e = null)
        {
            //called from snapshot Thread
            SaveAllWindowPositions(-1);
        }

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

        public static void RestoreBackupLayout(object sender = null, EventArgs e = null)
        {
            RestoreAllWindowPositionsFromBackup();
        }

        public static void SaveAllWindowPositions(int saveSlot)
        {
            bool autoMode = false;
            bool validSave = true; // false if auto and duplicate
            int screenCount = 0;
            if (saveSlot == -1)// auto mode
            {
                autoMode = true;
                screenCount = Screen.AllScreens.Length;
                if (screenCount > LAYOUT_MAX_SUPPORTED_MONITOR_COUNT) return;
                saveSlot = LAYOUT_HOTKEY_COUNT + screenCount;


            }
            if (saveSlot < LAYOUT_COUNT && validSave)
            {

                Layout newSnapshot = new Layout();
                newSnapshot.Capture();
                if (!autoMode || lastSavedLayout == null || !lastSavedLayout.Equals(newSnapshot) )
                {
                    layouts[saveSlot] = newSnapshot;
                    layouts[saveSlot].SaveToDisk(saveSlot, autoMode, screenCount);
                    var slotName = "to slot#" + saveSlot;
                    if (autoMode)
                        slotName = "for " + (saveSlot - LAYOUT_HOTKEY_COUNT) + " monitors [" + saveSlot + "]";

                    Log.I("Saved " + layouts[saveSlot].WindowCount + " window positions " + slotName + " autoMode=" + (autoMode ? "True" : "False"));
                    if (!autoMode)
                    {
                        var notifyIcon = FreshTools.GetNotifyIcon();
                        if (notifyIcon != null) notifyIcon.ShowBalloonTip(750, "FreshTools", layouts[saveSlot].WindowCount + " Windows saved to layout#" + saveSlot, ToolTipIcon.None);
                    }
                }
                else
                {
                    //Log.I("Didn't save layout - no change");
                }

                lastSavedLayout = layouts[saveSlot];
            }
        }

        public static void LoadLayoutsFromDisk()
        {
            for (int i = 0; i < LAYOUT_COUNT; i++)
            {
                string path = LayoutSaveFileBaseName + i + ".txt"; ;
                layouts[i].LoadFromDisk(path);
            }
        }

        public static void RestoreAllWindowPositions(int saveSlot, bool autoMode = false)
        {
            if (saveSlot < LAYOUT_COUNT && layouts[saveSlot] != null)
            {
                backupLayout = new Layout();
                backupLayout.Capture();

                int restoreCount = layouts[saveSlot].Restore();
                string slotName = "slot#" + saveSlot;
                if (autoMode)
                    slotName = "Auto backup for " + (saveSlot - LAYOUT_HOTKEY_COUNT) + " monitors";
                Log.I("Restored " + restoreCount + " window positions from " + slotName);
                FreshTools.GetNotifyIcon().ShowBalloonTip(75, "FreshTools", restoreCount + " Windows restored from " + slotName, ToolTipIcon.None);
            }
        }

        public static void RestoreAllWindowPositionsFromBackup()
        {
            if (backupLayout != null)
            {
                Layout tempBackupLayout = new Layout();
                tempBackupLayout.Capture();

                int restoreCount = backupLayout.Restore();
                string slotName = "backup layout";
                Log.I("Restored " + restoreCount + " window positions from " + slotName);
                FreshTools.GetNotifyIcon().ShowBalloonTip(75, "FreshTools", restoreCount + " Windows restored from " + slotName, ToolTipIcon.None);

                if(restoreCount!=0)
                    backupLayout = tempBackupLayout;
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

            public WindowInfo(string text, Rectangle rec)
            {
                Text = text;
                Rectangle = rec;
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

            public static WindowInfo ParseSaveString(string data)
            {
                try
                {
                    string[] values = data.Split(",".ToCharArray(), 5);
                    int x = int.Parse(values[0]);
                    int y = int.Parse(values[1]);
                    int w = int.Parse(values[2]);
                    int h = int.Parse(values[3]);
                    WindowInfo result = new WindowInfo(values[4],new Rectangle(x,y,w,h));
                    return result;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            public override string ToString()
            {
                return "WindowInfo() - " + Text + " {" + Rectangle.X + "," + Rectangle.Y + "," + Rectangle.Width + "," + Rectangle.Height + "}";
            }

            public override bool Equals(Object obj)
            {
                if (!(obj is WindowInfo)) return false;

                WindowInfo other = obj as WindowInfo;
                if (!Handle.Equals(other.Handle)) return false;

                return Rectangle.Equals(other.Rectangle);
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

            public void SaveToDisk(int slot, bool autoMode=false, int screenCount=0)
            {
                string fileName = LayoutSaveFileBaseName + slot + ".txt";
                if (autoMode)
                    fileName = LayoutSaveFileBaseName + "_Snapshot_"+ screenCount+"Monitors" + ".txt"; ;
                SaveToDisk(fileName);
            }

            public void SaveToDisk(string fileName)
            {
                string saveData = "";
                foreach (WindowInfo wi in WindowInfos)
                {
                    saveData += wi.SaveString() + "\n";
                }
                saveData.Trim();

                Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                File.WriteAllText(fileName, saveData);
            }

            public void LoadFromDisk(string path)
            {//read layout details and attempt to link to window handle by window title
                string[] fileLines;
                if (File.Exists(path))
                {
                    fileLines = File.ReadAllLines(path);
                    WindowInfos.Clear();

                    foreach (string s in fileLines)
                    {
                        WindowInfo wInfo = WindowInfo.ParseSaveString(s);
                        if (wInfo != null)
                        {
                            int matches = 0;
                            IntPtr handle = IntPtr.Zero;
                            var windows = WindowManager.FindWindowsWithText(wInfo.Text);
                            foreach (IntPtr h in windows)
                            {
                                string title = GetWindowText(h);
                                if(title.Equals(wInfo.Text))//test for exact match
                                {
                                    handle = h;
                                    matches++;
                                }
                            }

                            if (handle != IntPtr.Zero)//could be multiple matches
                            {
                                wInfo.Handle = handle;
                                WindowInfos.Add(wInfo);
                            }
                            else if (matches == 0)
                            {//failed exact match - try for partial match

                            }
                            else
                            {

                            }
                        }
                    }
                }
            }
			
			public int Restore()
			{
				int successCount = 0;
				foreach (WindowInfo i in WindowInfos)
				{
					if (i.RestorePosition())
						successCount++;
				}
                return successCount;
			}

            public override bool Equals(Object obj)
            {
                if (!(obj is Layout)) return false;

                Layout other = obj as Layout;
                if (other.WindowCount != WindowCount) return false;


                for (var i = 0; i < WindowInfos.Count; i++)
                {
                    if (!WindowInfos[i].Equals(other.WindowInfos[i])) return false;
                }
                return true;
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
