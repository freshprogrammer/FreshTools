using System.Windows.Forms;
using System.Threading;
using System.Drawing;
using System.Reflection;
using System;

namespace FreshMonitor
{
    public partial class BackgroundForm : Form
    {
        //Notification Icon
        private Icon freshMonitorIcon;
        private NotifyIcon freshMonitorNotifyIcon;
        private MenuItem startIdlePreventionMenuItem;
        private MenuItem stopIdlePreventionMenuItem;

        //Threads
        private Thread pollingThread;
        private bool sitePollingEnabled = true;

        private IdleMonitor idleMonitor;

        public BackgroundForm()
        {
            InitializeComponent();

            // Load icons from embeded resources
            var x = Assembly.GetExecutingAssembly();
            freshMonitorIcon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("FreshMonitor.HDD_Idle.ico"));

            // Create notify icons and assign idle icon and show it
            freshMonitorNotifyIcon = new NotifyIcon();
            freshMonitorNotifyIcon.Icon = freshMonitorIcon;
            freshMonitorNotifyIcon.Visible = true;

            // Create all context menu items and add them to notification tray icon
            MenuItem progNameMenuItem = new MenuItem("Fresh Monitor");
            MenuItem breakMenuItem = new MenuItem("-");
            startIdlePreventionMenuItem = new MenuItem("Start Idle Prevention");
            stopIdlePreventionMenuItem = new MenuItem("Stop Idle Prevention");
            MenuItem toggleMenuItem = new MenuItem("Toggle");
            MenuItem quitMenuItem = new MenuItem("Quit");
            ContextMenu contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add(progNameMenuItem);
            contextMenu.MenuItems.Add(breakMenuItem);
            contextMenu.MenuItems.Add(startIdlePreventionMenuItem);
            contextMenu.MenuItems.Add(toggleMenuItem);
            contextMenu.MenuItems.Add(quitMenuItem);
            freshMonitorNotifyIcon.ContextMenu = contextMenu;

            // Wire up quit button to close application
            toggleMenuItem.Click += toggleMenuItem_Click;
            startIdlePreventionMenuItem.Click += startIdlePreventionMenuItem_Click;
            stopIdlePreventionMenuItem.Click += stopIdlePreventionMenuItem_Click;
            quitMenuItem.Click += quitMenuItem_Click;

            //  Hide the form because we don't need it, this is a notification tray application
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            // Start worker thread that pulls HDD activity
            pollingThread = new Thread(new ThreadStart(PollingThread));
            pollingThread.Start();
        }

        #region Context Menu Event Handlers
        private void startIdlePreventionMenuItem_Click(object sender, EventArgs e)
        {
            if(idleMonitor==null)
            {
                idleMonitor = new IdleMonitor();
            }
            idleMonitor.StartIdleProtection(10, 5);
            idleMonitor.NotifyIcon = freshMonitorNotifyIcon;
            idleMonitor.BalloonOnIdlePrevention = true;

            int index = freshMonitorNotifyIcon.ContextMenu.MenuItems.IndexOf(startIdlePreventionMenuItem);
            freshMonitorNotifyIcon.ContextMenu.MenuItems.RemoveAt(index);
            freshMonitorNotifyIcon.ContextMenu.MenuItems.Add(index, stopIdlePreventionMenuItem);
        }

        private void stopIdlePreventionMenuItem_Click(object sender, EventArgs e)
        {
            if(idleMonitor!=null)
            {
                idleMonitor.StopClockThread();
            }

            int index = freshMonitorNotifyIcon.ContextMenu.MenuItems.IndexOf(stopIdlePreventionMenuItem);
            freshMonitorNotifyIcon.ContextMenu.MenuItems.RemoveAt(index);
            freshMonitorNotifyIcon.ContextMenu.MenuItems.Add(index, startIdlePreventionMenuItem);
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
            pollingThread.Abort();
            freshMonitorNotifyIcon.Dispose();
            this.Close();
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
