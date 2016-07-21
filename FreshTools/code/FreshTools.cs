using System;
using System.Drawing;
using System.Net;
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

        //Settings
        private readonly string configFilePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\" + Assembly.GetExecutingAssembly().GetName().Name + @"\config.txt";
        private VariablesFile settingsFile;

        public FreshTools()
        {
            Thread.CurrentThread.Name = "FreshTools Thread";
            Log.Init();
            LoadConfig();

            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);
            InitializeNotificationIcon();
            freshToolsNotifyIcon.Visible = true;

            RegisterHotkeys();

            Log.I("FreshTools started sucsessfully");
            Log.ConsoleLogLevel = LogLevel.Verbose;

            NetworkMonitor netMan = new NetworkMonitor();
            //netMan.TestCode();
            netMan.AddMonitor("www.gogle.com", true, true);
            netMan.AddMonitor("www.freshdistraction.com", true, true);
            netMan.NotifyIcon = freshToolsNotifyIcon;
            netMan.StartMonitoring();
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

            MenuItem windowManagerSnapHotKeysEnabledMenuItem = new MenuItem("Window Snap Hotkeys");
            windowManagerSnapHotKeysEnabledMenuItem.Checked = WindowManager.SnapHotKeysEnabled;
            MenuItem windowManagerSnapAltHotKeysEnabledMenuItem = new MenuItem("Window Snap Alt Hotkeys");
            windowManagerSnapAltHotKeysEnabledMenuItem.Checked = WindowManager.SnapAltHotKeysEnabled;
            MenuItem windowManagerMiscHotKeysEnabledMenuItem = new MenuItem("Window General Hotkeys");
            windowManagerMiscHotKeysEnabledMenuItem.Checked = WindowManager.MiscHotKeysEnabled;

            MenuItem windowManagerMenu = new MenuItem("Window Manager");
            MenuItem windowManagerSaveWindowsMenuItem = new MenuItem("Save All Window Positions");
            MenuItem windowManagerRestoreWindowsMenuItem = new MenuItem("Restore All Window Positions");
            MenuItem windowManagerUndoRestoreWindowsMenuItem = new MenuItem("Restore All Window Positions (undo)");

            windowManagerMenu.MenuItems.Add(windowManagerSnapHotKeysEnabledMenuItem);
            windowManagerMenu.MenuItems.Add(windowManagerSnapAltHotKeysEnabledMenuItem);
            windowManagerMenu.MenuItems.Add(windowManagerMiscHotKeysEnabledMenuItem);
            windowManagerMenu.MenuItems.Add(new MenuItem("-"));
            windowManagerMenu.MenuItems.Add(windowManagerSaveWindowsMenuItem);
            windowManagerMenu.MenuItems.Add(windowManagerRestoreWindowsMenuItem);
            windowManagerMenu.MenuItems.Add(windowManagerUndoRestoreWindowsMenuItem);

            MenuItem settingsMenu = new MenuItem("Settings");
            MenuItem settingsDirMenuItem = new MenuItem("Open AppData");
            MenuItem launchAsAdminMenuItem = new MenuItem("ReLaunch As Admin");

            MenuItem startupEnabledMenuItem = new MenuItem("Start With Windows");
            startupEnabledMenuItem.Checked = FreshArchives.IsApplicationInStartup();

            settingsMenu.MenuItems.Add(settingsDirMenuItem);
            settingsMenu.MenuItems.Add(launchAsAdminMenuItem);
            settingsMenu.MenuItems.Add(startupEnabledMenuItem);

            MenuItem quitMenuItem = new MenuItem("Quit");

            ContextMenu contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add(titleMenuItem);
            contextMenu.MenuItems.Add(new MenuItem("-"));
            contextMenu.MenuItems.Add(windowManagerMenu);
            contextMenu.MenuItems.Add(settingsMenu);
            contextMenu.MenuItems.Add(new MenuItem("-"));
            contextMenu.MenuItems.Add(quitMenuItem);
            freshToolsNotifyIcon.ContextMenu = contextMenu;

            // Wire up menu items
            titleMenuItem.Click += titleMenuItem_Click;
            windowManagerSnapHotKeysEnabledMenuItem.Click += windowManagerSnapHotKeysEnabledMenuItem_Click;
            windowManagerSnapAltHotKeysEnabledMenuItem.Click += windowManagerSnapAltHotKeysEnabledMenuItem_Click;
            windowManagerMiscHotKeysEnabledMenuItem.Click += windowManagerMiscHotKeysEnabledMenuItem_Click;
            windowManagerSaveWindowsMenuItem.Click += WindowManager.SaveAllWindowPositions;
            windowManagerRestoreWindowsMenuItem.Click += WindowManager.RestoreAllWindowPositions;
            windowManagerUndoRestoreWindowsMenuItem.Click += WindowManager.UndoRestoreAllWindowPositions;
            startupEnabledMenuItem.Click += startupEnabledMenuItem_Click;
            launchAsAdminMenuItem.Click += launchAsAdminMenuItem_Click;
            settingsDirMenuItem.Click += settingsDirMenuItem_Click;
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

        /// <summary>
        /// Load variables from config file and update real time settings
        /// </summary>
        public void LoadConfig()
        {
            settingsFile = new VariablesFile(configFilePath, null, false);
            VariableLibrary vars = settingsFile.variables;

            //load variables
            bool snapHotKeysEnabled = WindowManager.SnapHotKeysEnabled_Default;
            WindowManager.SnapHotKeysEnabled = vars.GetVariable("SnapWindowHotKeysEnabled", ref snapHotKeysEnabled, true).Boolean;
            bool snapAltHotKeysEnabled = WindowManager.SnapAltHotKeysEnabled_Default;
            WindowManager.SnapAltHotKeysEnabled = vars.GetVariable("SnapAltWindowHotKeysEnabled", ref snapAltHotKeysEnabled, true).Boolean;
            bool miscHotKeysEnabled = WindowManager.MiscHotKeysEnabled_Default;
            WindowManager.MiscHotKeysEnabled = vars.GetVariable("MiscWindowHotKeysEnabled", ref miscHotKeysEnabled, true).Boolean;
            WindowManager.LoadSnapSizes(settingsFile);
            Log.I("Finisihed loading config");
            //re-write config file in case one didn't exist already
            SaveConfig();
        }

        /// <summary>
        /// Make sure config variables that can be changed are updated in config file
        /// </summary>
        private void SaveConfig()
        {
            settingsFile.variables.SetValue("SnapWindowHotKeysEnabled", "" + WindowManager.SnapHotKeysEnabled);
            settingsFile.variables.SetValue("SnapAltWindowHotKeysEnabled", "" + WindowManager.SnapAltHotKeysEnabled);
            settingsFile.variables.SetValue("MiscWindowHotKeysEnabled", "" + WindowManager.MiscHotKeysEnabled);

            WindowManager.SaveSnapSizes(settingsFile);
            settingsFile.SaveAs(configFilePath);

            Log.I("Finisihed updating config");
        }

        #region Context Menu Event Handlers
        private void titleMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Fresh Tools\n" +
                            "Version " + Assembly.GetExecutingAssembly().GetName().Version + "\n" +
                            "By FreshProgrammer on GitHub", 
                "Fresh Tools", MessageBoxButtons.OK);
        }

        private void windowManagerSnapHotKeysEnabledMenuItem_Click(object sender, EventArgs e)
        {
            MenuItem i = (MenuItem)sender;
            i.Checked = !i.Checked;
            WindowManager.SnapHotKeysEnabled = i.Checked;
            SaveConfig();
        }

        private void windowManagerSnapAltHotKeysEnabledMenuItem_Click(object sender, EventArgs e)
        {
            MenuItem i = (MenuItem)sender;
            i.Checked = !i.Checked;
            WindowManager.SnapAltHotKeysEnabled = i.Checked;
            SaveConfig();
        }

        private void windowManagerMiscHotKeysEnabledMenuItem_Click(object sender, EventArgs e)
        {
            MenuItem i = (MenuItem)sender;
            i.Checked = !i.Checked;
            WindowManager.MiscHotKeysEnabled = i.Checked;
            SaveConfig();
        }

        private void startupEnabledMenuItem_Click(object sender, EventArgs e)
        {
            MenuItem i = (MenuItem)sender;
            if (FreshArchives.IsApplicationInStartup())
                FreshArchives.RemoveApplicationFromStartup();
            else
                FreshArchives.AddApplicationToStartup();
            i.Checked = FreshArchives.IsApplicationInStartup();
            Log.I("Finisihed updating config");
        }

        private void launchAsAdminMenuItem_Click(object sender, EventArgs e)
        {
            Log.I("Relaunching as Admin");
            Process p = new Process();
            p.StartInfo.FileName = Application.ExecutablePath;
            p.StartInfo.Verb = "runas";
            p.Start();
            //this process will be killed by the single instance check in the new process
            Application.Exit();
        }

        private void settingsDirMenuItem_Click(object sender, EventArgs e)
        {
            //open this appdata folder
            Process.Start(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\" + Assembly.GetExecutingAssembly().GetName().Name);
        }

        /// <summary>
        /// Close the application on click of 'quit' button on context menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void quitMenuItem_Click(object sender, EventArgs e)
        {
            Log.I("quitMenuItem_Click()");
            freshToolsNotifyIcon.Dispose();
            SaveConfig();
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
            //GenericsClass.Log("Registering Hotkeys");
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
                    Log.I("UnActioned - " + args.Modifiers + "+" + args.Key + "");
                }
            }
            catch (Exception e)
            {
                Log.Exception(e);
            }
        }
        #endregion
    }
}
