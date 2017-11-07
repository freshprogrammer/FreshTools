using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Input;
using System.Windows.Forms;
using Microsoft.Win32;


namespace FreshTools
{
    public class FreshTools : ApplicationContext
    {
        //Notification Icon
        private static Icon freshToolsIcon;
        private static NotifyIcon freshToolsNotifyIcon;

        //Settings
        private readonly string configFilePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\" + Assembly.GetExecutingAssembly().GetName().Name + @"\config.txt";
        private VariablesFile settingsFile;

        public FreshTools()
        {
            Thread.CurrentThread.Name = "FreshTools Thread";
            Log.Init();
            LoadConfig();
			UpdateRegistryForStartup();

            Application.ApplicationExit += new EventHandler(this.OnApplicationExit);
            InitializeNotificationIcon();
            freshToolsNotifyIcon.Visible = true;

            RegisterHotkeys();

            Log.I("FreshTools started sucsessfully");
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
            MenuItem windowManagerMenu = new MenuItem("Window Manager");

            MenuItem windowManagerSnapHotKeysEnabledMenuItem = new MenuItem("Window Snap Hotkeys");
            windowManagerSnapHotKeysEnabledMenuItem.Checked = WindowManager.SnapHotKeysEnabled;
            windowManagerSnapHotKeysEnabledMenuItem.Click += windowManagerSnapHotKeysEnabledMenuItem_Click;
            MenuItem windowManagerSnapAltHotKeysEnabledMenuItem = new MenuItem("Window Snap Alt Hotkeys");
            windowManagerSnapAltHotKeysEnabledMenuItem.Checked = WindowManager.SnapAltHotKeysEnabled;
            windowManagerSnapAltHotKeysEnabledMenuItem.Click += windowManagerSnapAltHotKeysEnabledMenuItem_Click;
            MenuItem windowManagerMiscHotKeysEnabledMenuItem = new MenuItem("Window General Hotkeys");
            windowManagerMiscHotKeysEnabledMenuItem.Checked = WindowManager.MiscHotKeysEnabled;
            windowManagerMiscHotKeysEnabledMenuItem.Click += windowManagerMiscHotKeysEnabledMenuItem_Click;

            windowManagerMenu.MenuItems.Add(windowManagerSnapHotKeysEnabledMenuItem);
            windowManagerMenu.MenuItems.Add(windowManagerSnapAltHotKeysEnabledMenuItem);
            windowManagerMenu.MenuItems.Add(windowManagerMiscHotKeysEnabledMenuItem);
            windowManagerMenu.MenuItems.Add(new MenuItem("-"));
            windowManagerMenu.MenuItems.Add(new MenuItem("Save Window Layout",WindowManager.SaveLayout0));
            windowManagerMenu.MenuItems.Add(new MenuItem("Restore Window Layout",WindowManager.RestoreLayout0));

            MenuItem settingsMenu = new MenuItem("Settings");

            MenuItem launchAsAdminMenuItem = new MenuItem("ReLaunch As Admin", launchAsAdminMenuItem_Click);
            if (FreshArchives.IsUserAdministrator())
                launchAsAdminMenuItem.Enabled = false;

            MenuItem startupEnabledMenuItem = new MenuItem("Start With Windows",startupEnabledMenuItem_Click);
            startupEnabledMenuItem.Checked = FreshArchives.IsApplicationInStartup();


            settingsMenu.MenuItems.Add(new MenuItem("Open AppData", new EventHandler(delegate(Object o, EventArgs a) { Process.Start(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\" + Assembly.GetExecutingAssembly().GetName().Name); })));
            settingsMenu.MenuItems.Add(launchAsAdminMenuItem);
            settingsMenu.MenuItems.Add(new MenuItem("Reload config", reloadConfigMenuItem_Click));
            settingsMenu.MenuItems.Add(startupEnabledMenuItem);

            ContextMenu contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add(new MenuItem("Fresh Tools v" + FreshArchives.TrimVersionNumber(Assembly.GetExecutingAssembly().GetName().Version), titleMenuItem_Click));
            contextMenu.MenuItems.Add(new MenuItem("-"));
            contextMenu.MenuItems.Add(windowManagerMenu);
            contextMenu.MenuItems.Add(settingsMenu);
            contextMenu.MenuItems.Add(new MenuItem("-"));
            contextMenu.MenuItems.Add(new MenuItem("GitHub Page", new EventHandler(delegate (Object o, EventArgs a) {Process.Start("http://www.github.com/freshprogrammer/freshtools"); })));
            contextMenu.MenuItems.Add(new MenuItem("Quit", quitMenuItem_Click));
            freshToolsNotifyIcon.ContextMenu = contextMenu;
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
            WindowManager.LoadSnapSizes(settingsFile);
            WindowManager.LoadHotKeys(settingsFile);
            bool snapHotKeysEnabled = WindowManager.SnapHotKeysEnabled_Default;
            WindowManager.SnapHotKeysEnabled = vars.GetVariable("SnapWindowHotKeysEnabled", ref snapHotKeysEnabled, true).Boolean;
            bool snapAltHotKeysEnabled = WindowManager.SnapAltHotKeysEnabled_Default;
            WindowManager.SnapAltHotKeysEnabled = vars.GetVariable("SnapAltWindowHotKeysEnabled", ref snapAltHotKeysEnabled, true).Boolean;
            bool miscHotKeysEnabled = WindowManager.MiscHotKeysEnabled_Default;
            WindowManager.MiscHotKeysEnabled = vars.GetVariable("MiscWindowHotKeysEnabled", ref miscHotKeysEnabled, true).Boolean;
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

        private void reloadConfigMenuItem_Click(object sender, EventArgs e)
        {
            Log.I("Reloading config file");
            LoadConfig();
        }

        private void launchAsAdminMenuItem_Click(object sender, EventArgs e)
        {
            Log.I("Relaunching as Admin");
            Process p = new Process();
            p.StartInfo.FileName = Assembly.GetEntryAssembly().Location;
            p.StartInfo.Verb = "runas";
            p.Start();
            //this process will be killed by the single instance check in the new process
            Application.Exit();
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

		public static void UpdateRegistryForStartup()
        {//should check for any existing key and delete if the name is outdated and create a new one - especialy since this app kills other processes with the same name
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
				bool partOfStartup = false;
				
				//look for any relevant key values
                if(key.GetValue(Assembly.GetExecutingAssembly().GetName().Name) != null)
					partOfStartup = true;
				
				//should delete old key values here
                //key.DeleteValue(Assembly.GetExecutingAssembly().GetName().Name, false);
				
				//update key value to current path
				if(partOfStartup)
				{
					key.SetValue(Assembly.GetExecutingAssembly().GetName().Name, "\"" + Assembly.GetEntryAssembly().Location + "\"");
				}
            }
        }
		
        private void OnApplicationExit(object sender, EventArgs args)
        {
            //cleanup
        }

        public static NotifyIcon GetNotifyIcon()
        {
            return freshToolsNotifyIcon;
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

            //HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt), Keys.Q, TestKeyPressed);

            MouseListener.Start();
            MouseListener.OnMouseInput += (s, e) =>
            {
                Thread thread = new Thread(AdjustVolumeThread);
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start(e);
            };
        }
        
        private static void TestKeyPressed(object sender, HotKeyEventArgs args)
        {
            Log.E("Test Key Pressed");
        }

        private static void AdjustVolumeThread(object o)
        {
            if ((Keyboard.GetKeyStates(Key.Pause) & KeyStates.Down) > 0)
            {
                MouseEventArgs args = (MouseEventArgs)o;
                if (args.WheelDelta > 0)
                    FreshArchives.VolumeUp();
                else
                    FreshArchives.VolumeDown();
            }
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
