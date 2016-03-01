using System.Windows.Forms;
using System.Threading;
using System.Drawing;
using System.Reflection;
using System;

namespace FreshTools
{
    public class MainForm : Form
    {
        //Notification Icon
        private Icon freshToolsIcon;
        private NotifyIcon freshToolsNotifyIcon;
        private MenuItem startIdlePreventionMenuItem;
        private MenuItem stopIdlePreventionMenuItem;

        //Threads
        private Thread pollingThread;
        private bool sitePollingEnabled = true;

        private IdleMonitor idleMonitor;

        public MainForm()
        {
            Thread.CurrentThread.Name = "FreshTools MainForm Thread";

            LogSystem.Init();

            // Load icons from embeded resources
            freshToolsIcon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("FreshTools.HDD_Idle.ico"));

            // Create notify icons and assign idle icon and show it
            freshToolsNotifyIcon = new NotifyIcon();
            freshToolsNotifyIcon.Icon = freshToolsIcon;
            freshToolsNotifyIcon.Visible = true;

            // Create all context menu items and add them to notification tray icon
            MenuItem titleMenuItem = new MenuItem("Fresh Monitor");
            MenuItem breakMenuItem = new MenuItem("-");
            startIdlePreventionMenuItem = new MenuItem("Start Idle Prevention");
            stopIdlePreventionMenuItem = new MenuItem("Stop Idle Prevention");
            MenuItem toggleMenuItem = new MenuItem("Toggle");
            MenuItem quitMenuItem = new MenuItem("Quit");
            ContextMenu contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add(titleMenuItem);
            contextMenu.MenuItems.Add(breakMenuItem);
            contextMenu.MenuItems.Add(startIdlePreventionMenuItem);
            contextMenu.MenuItems.Add(toggleMenuItem);
            contextMenu.MenuItems.Add(quitMenuItem);
            freshToolsNotifyIcon.ContextMenu = contextMenu;

            // Wire up menu items
            startIdlePreventionMenuItem.Click += startIdlePreventionMenuItem_Click;
            stopIdlePreventionMenuItem.Click += stopIdlePreventionMenuItem_Click;
            toggleMenuItem.Click += toggleMenuItem_Click;
            quitMenuItem.Click += quitMenuItem_Click;

            //  Hide the form because we don't need it, this is a notification tray application
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            RegisterHotkeys();

            // Start worker thread that pulls HDD activity
            pollingThread = new Thread(new ThreadStart(PollingThread));
            pollingThread.Start();

            LogSystem.Log("FreshTools started sucsessfully");
        }

        #region Context Menu Event Handlers
        private void startIdlePreventionMenuItem_Click(object sender, EventArgs e)
        {
            if(idleMonitor==null)
            {
                idleMonitor = new IdleMonitor();
            }
            idleMonitor.StartIdleProtection();
            idleMonitor.NotifyIcon = freshToolsNotifyIcon;
            idleMonitor.BalloonOnIdlePrevention = false;

            int index = freshToolsNotifyIcon.ContextMenu.MenuItems.IndexOf(startIdlePreventionMenuItem);
            freshToolsNotifyIcon.ContextMenu.MenuItems.RemoveAt(index);
            freshToolsNotifyIcon.ContextMenu.MenuItems.Add(index, stopIdlePreventionMenuItem);
        }

        private void stopIdlePreventionMenuItem_Click(object sender, EventArgs e)
        {
            if(idleMonitor!=null)
            {
                idleMonitor.StopClockThread();
            }

            int index = freshToolsNotifyIcon.ContextMenu.MenuItems.IndexOf(stopIdlePreventionMenuItem);
            freshToolsNotifyIcon.ContextMenu.MenuItems.RemoveAt(index);
            freshToolsNotifyIcon.ContextMenu.MenuItems.Add(index, startIdlePreventionMenuItem);
        }

        private void toggleMenuItem_Click(object sender, EventArgs e)
        {
            sitePollingEnabled = !sitePollingEnabled;
        }

        /// <summary>
        /// Close the application on click of 'quit' button on context menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void quitMenuItem_Click(object sender, EventArgs e)
        {
            LogSystem.Log("quitMenuItem_Click()");
            pollingThread.Abort();
            freshToolsNotifyIcon.Dispose();
            this.Close();
        }
        #endregion

        #region HotKey Events
        private static void RegisterHotkeys()
        {
            //register hotkey(s)
            //GenericsClass.LogSystem("Registering Hotkeys");
            HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(HotKeyPressed);
            HotKeyManager.RegisterHotKey((KeyModifiers.Control | KeyModifiers.Shift), Keys.A);
            HotKeyManager.RegisterHotKey((KeyModifiers.Control | KeyModifiers.Shift), Keys.S);
            HotKeyManager.RegisterHotKey((KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad7);
            HotKeyManager.RegisterHotKey((KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad9);
            HotKeyManager.RegisterHotKey((KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad1);
            HotKeyManager.RegisterHotKey((KeyModifiers.Control | KeyModifiers.Alt), Keys.NumPad3);
        }

        static void HotKeyPressed(object sender, HotKeyEventArgs args)
        {
            try
            {
                if (args.Modifiers == (KeyModifiers.Control | KeyModifiers.Shift) && args.Key == Keys.A)
                {
                    LogSystem.Log("HotKeyManager_HotKeyPressed() - " + args.Modifiers + "+" + args.Key + " - MoveActiveWindowToLeftMonitor()");
                    WindowManager.MoveActiveWindowToLeftMonitor();
                }
                else if (args.Modifiers == (KeyModifiers.Control | KeyModifiers.Shift) && args.Key == Keys.S)
                {
                    LogSystem.Log("HotKeyManager_HotKeyPressed() - " + args.Modifiers + "+" + args.Key + " - MoveActiveWindowToRightMonitor()");
                    WindowManager.MoveActiveWindowToRightMonitor();
                }
                else if (args.Modifiers == (KeyModifiers.Control | KeyModifiers.Alt) && args.Key == Keys.NumPad7)
                {
                    LogSystem.Log("HotKeyManager_HotKeyPressed() - " + args.Modifiers + "+" + args.Key + " - MoveActiveWindowToRightMonitor()");
                    WindowManager.MoveActiveWindowToTopLeft();
                }
                else if (args.Modifiers == (KeyModifiers.Control | KeyModifiers.Alt) && args.Key == Keys.NumPad9)
                {
                    LogSystem.Log("HotKeyManager_HotKeyPressed() - " + args.Modifiers + "+" + args.Key + " - MoveActiveWindowToRightMonitor()");
                    WindowManager.MoveActiveWindowToTopRight();
                }
                else if (args.Modifiers == (KeyModifiers.Control | KeyModifiers.Alt) && args.Key == Keys.NumPad1)
                {
                    LogSystem.Log("HotKeyManager_HotKeyPressed() - " + args.Modifiers + "+" + args.Key + " - MoveActiveWindowToRightMonitor()");
                    WindowManager.MoveActiveWindowToBottomLeft();
                }
                else if (args.Modifiers == (KeyModifiers.Control | KeyModifiers.Alt) && args.Key == Keys.NumPad3)
                {
                    LogSystem.Log("HotKeyManager_HotKeyPressed() - " + args.Modifiers + "+" + args.Key + " - MoveActiveWindowToRightMonitor()");
                    WindowManager.MoveActiveWindowToBottomRight();
                }
                else //unknown hot key pressed
                {
                    //uncaught hotkey
                    LogSystem.Log("HotKeyManager_HotKeyPressed() - UnActioned - " + args.Modifiers + "+" + args.Key + "");
                }
            }
            catch (Exception e)
            {
                LogSystem.Log("Exception#" + LogSystem.IncrementExceptionCount() + " in MainForm.HotKeyPressed(object,HotKeyEventArgs) - " + e);
            }
        }
        #endregion

        #region Polling thread
        /// <summary>
        /// This is the thread that calls the site poller
        /// </summary>
        public void PollingThread()
        {
            try
            {
                // Main loop where all the magic happens
                while (true)
                {
                    if (sitePollingEnabled)
                    {

                    }
                    Thread.Sleep(100);
                }
            }
            catch (ThreadAbortException)
            {
                // Thead was aborted
            }
        }
        #endregion
    }
}
