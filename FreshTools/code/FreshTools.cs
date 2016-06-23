using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;

namespace FreshTools
{
    public class FreshTools : ApplicationContext
    {
        //Notification Icon
        private Icon freshToolsIcon;
        private NotifyIcon freshToolsNotifyIcon;
        private MenuItem startIdlePreventionMenuItem;
        private MenuItem stopIdlePreventionMenuItem;

        //Settings
        private readonly string configFilePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\" + Assembly.GetExecutingAssembly().GetName().Name + @"\config.txt";
        private VariablesFile settingsFile;

        //Tools
        private IdleMonitor idleMonitor;

        public FreshTools()
        {
            Thread.CurrentThread.Name = "FreshTools Thread";
            LogSystem.Init();
            LoadConfig();

            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);
            InitializeNotificationIcon();
            freshToolsNotifyIcon.Visible = true;

            RegisterHotkeys();

            LogSystem.Log("FreshTools started sucsessfully");
        }

        /// <summary>
        /// Create NotificationIcon loading embeded icon and defining menu items
        /// </summary>
        public void InitializeNotificationIcon()
        {
            // Create notify icons and assign idle icon and show it
            freshToolsNotifyIcon = new NotifyIcon();
            freshToolsNotifyIcon.Text = "Fresh Tools";

            // Load icons from embeded resources
            freshToolsIcon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("FreshTools.HDD_Idle.ico"));
            freshToolsNotifyIcon.Icon = freshToolsIcon;

            // Create all context menu items and add them to notification tray icon
            MenuItem titleMenuItem = new MenuItem("Fresh Tools v" + FreshArchives.TrimVersionNumber(Assembly.GetExecutingAssembly().GetName().Version));
            MenuItem breakMenuItem = new MenuItem("-");

            startIdlePreventionMenuItem = new MenuItem("Start Idle Prevention");
            stopIdlePreventionMenuItem = new MenuItem("Stop Idle Prevention");

            MenuItem windowManagerHotKeysEnabledMenuItem = new MenuItem("Window Control Hot Keys");
            windowManagerHotKeysEnabledMenuItem.Checked = WindowManager.HotKeysEnabled;

            MenuItem windowManagerSaveWindowsMenuItem = new MenuItem("Save All Window Positions");
            MenuItem windowManagerRestoreWindowsMenuItem = new MenuItem("Restore All Window Positions");
            MenuItem windowManagerUndoRestoreWindowsMenuItem = new MenuItem("Restore All Window Positions (undo)");

            MenuItem startupEnabledMenuItem = new MenuItem("Start With Windows");
            startupEnabledMenuItem.Checked = FreshArchives.IsApplicationInStartup();

            MenuItem quitMenuItem = new MenuItem("Quit");

            ContextMenu contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add(titleMenuItem);
            contextMenu.MenuItems.Add(breakMenuItem);
            contextMenu.MenuItems.Add(startIdlePreventionMenuItem);
            contextMenu.MenuItems.Add(windowManagerHotKeysEnabledMenuItem);
            contextMenu.MenuItems.Add(windowManagerSaveWindowsMenuItem);
            contextMenu.MenuItems.Add(windowManagerRestoreWindowsMenuItem);
            contextMenu.MenuItems.Add(windowManagerUndoRestoreWindowsMenuItem);
            contextMenu.MenuItems.Add(startupEnabledMenuItem);
            contextMenu.MenuItems.Add(quitMenuItem);
            freshToolsNotifyIcon.ContextMenu = contextMenu;

            // Wire up menu items
            startIdlePreventionMenuItem.Click += startIdlePreventionMenuItem_Click;
            stopIdlePreventionMenuItem.Click += stopIdlePreventionMenuItem_Click;
            windowManagerHotKeysEnabledMenuItem.Click += windowHotKeysEnabledMenuItem_Click;
            windowManagerSaveWindowsMenuItem.Click += WindowManager.SaveAllWindowPositions;
            windowManagerRestoreWindowsMenuItem.Click += WindowManager.RestoreAllWindowPositions;
            windowManagerUndoRestoreWindowsMenuItem.Click += WindowManager.UndoRestoreAllWindowPositions;
            startupEnabledMenuItem.Click += startupEnabledMenuItem_Click;
            quitMenuItem.Click += quitMenuItem_Click;
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            string mutex_id = "FreshTools";
            bool createdNew = true;
            using (Mutex mutex = new Mutex(true, mutex_id, out createdNew))
            {
                if (!createdNew)
                {
                    Process thisProccess = Process.GetCurrentProcess();
                    foreach (var process in Process.GetProcessesByName(thisProccess.ProcessName))
                    {
                        if (process.Id != thisProccess.Id)
                            process.Kill();
                    }
                    //MessageBox.Show("Killed old process and started new!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new FreshTools());
            }
        }

        public void LoadConfig()
        {
            settingsFile = new VariablesFile(configFilePath, null, false);
            VariableLibrary vars = settingsFile.variables;
            
            //load variables
            bool windowHotKeys = WindowManager.HotKeysEnabled;
            WindowManager.HotKeysEnabled = vars.GetVariable("EnableWindowManager", ref windowHotKeys, true).Boolean;

            //vars.GetVariable("testVariable", ref testVariable, true);
            LogSystem.Log("Finisihed loading config");
        }

        /// <summary>
        /// Make sure config variables that can be changed are updated in config file
        /// </summary>
        private void SaveVariables()
        {
            settingsFile.variables.SetValue("EnableWindowManager", "" + WindowManager.HotKeysEnabled);
            //settingsFile.variables.SetValue("testVariable", "" + testVariable);

            settingsFile.SaveAs(configFilePath);
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

        private void windowHotKeysEnabledMenuItem_Click(object sender, EventArgs e)
        {
            MenuItem i = (MenuItem)sender;
            i.Checked = !i.Checked;
            WindowManager.HotKeysEnabled = i.Checked;
        }

        private void startupEnabledMenuItem_Click(object sender, EventArgs e)
        {
            MenuItem i = (MenuItem)sender;
            if (FreshArchives.IsApplicationInStartup())
                FreshArchives.RemoveApplicationFromStartup();
            else
                FreshArchives.AddApplicationToStartup();
            i.Checked = FreshArchives.IsApplicationInStartup();
        }

        /// <summary>
        /// Close the application on click of 'quit' button on context menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void quitMenuItem_Click(object sender, EventArgs e)
        {
            LogSystem.Log("quitMenuItem_Click()");
            freshToolsNotifyIcon.Dispose();
            SaveVariables();
            Application.Exit();
        }
        #endregion

        private void OnApplicationExit(object sender, EventArgs args)
        {
            //cleanup
        }

        #region HotKey Events
        private static void RegisterHotkeys()
        {
            //register hotkey(s)
            //GenericsClass.LogSystem("Registering Hotkeys");
            HotKeyManager.GenericHotKeyPressedHandler += new EventHandler<HotKeyEventArgs>(GenericHotKeyPressed);
            //HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Shift), Keys.C);
            //HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Shift), Keys.X);
            //HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Shift), Keys.Z);
        }

        private static void GenericHotKeyPressed(object sender, HotKeyEventArgs args)
        {
            try
            {
                if (args.Modifiers == (KeyModifiers.Control | KeyModifiers.Shift) && args.Key == Keys.C)
                {
                    //WindowManager.SaveAllWindowPositions();
                }
                else if (args.Modifiers == (KeyModifiers.Control | KeyModifiers.Shift) && args.Key == Keys.X)
                {
                    //WindowManager.RestoreAllWindowPositions();
                }
                else if (args.Modifiers == (KeyModifiers.Control | KeyModifiers.Shift) && args.Key == Keys.Z)
                {
                    //WindowManager.UndoRestoreAllWindowPositions();
                }
                else //unknown hot key pressed
                {
                    //uncaught hotkey
                    LogSystem.Log("UnActioned - " + args.Modifiers + "+" + args.Key + "");
                }
            }
            catch (Exception e)
            {
                LogSystem.Log("Exception#" + LogSystem.IncrementExceptionCount() + " in FreshTools.HotKeyPressed(object,HotKeyEventArgs) - " + e);
            }
        }
        #endregion
    }
}
