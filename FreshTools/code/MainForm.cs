﻿using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace FreshTools
{
    public class MainForm : Form
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

        public MainForm()
        {
            Thread.CurrentThread.Name = "FreshTools MainForm Thread";
            LogSystem.Init();
            LoadConfig();

            // Load icons from embeded resources
            freshToolsIcon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("FreshTools.HDD_Idle.ico"));

            // Create notify icons and assign idle icon and show it
            freshToolsNotifyIcon = new NotifyIcon();
            freshToolsNotifyIcon.Icon = freshToolsIcon;
            freshToolsNotifyIcon.Visible = true;

            // Create all context menu items and add them to notification tray icon
            MenuItem titleMenuItem = new MenuItem("Fresh Tools v" + FreshArchives.TrimVersionNumber(Assembly.GetExecutingAssembly().GetName().Version));
            MenuItem breakMenuItem = new MenuItem("-");
            startIdlePreventionMenuItem = new MenuItem("Start Idle Prevention");
            stopIdlePreventionMenuItem = new MenuItem("Stop Idle Prevention");
            MenuItem windowHotKeysEnabledManagerMenuItem = new MenuItem("Window Control Hot Keys");
            windowHotKeysEnabledManagerMenuItem.Checked = WindowManager.HotKeysEnabled;
            MenuItem quitMenuItem = new MenuItem("Quit");
            ContextMenu contextMenu = new ContextMenu();
            contextMenu.MenuItems.Add(titleMenuItem);
            contextMenu.MenuItems.Add(breakMenuItem);
            contextMenu.MenuItems.Add(startIdlePreventionMenuItem);
            contextMenu.MenuItems.Add(windowHotKeysEnabledManagerMenuItem);
            contextMenu.MenuItems.Add(quitMenuItem);
            freshToolsNotifyIcon.ContextMenu = contextMenu;

            // Wire up menu items
            startIdlePreventionMenuItem.Click += startIdlePreventionMenuItem_Click;
            stopIdlePreventionMenuItem.Click += stopIdlePreventionMenuItem_Click;
            windowHotKeysEnabledManagerMenuItem.Click += windowHotKeysEnabledMenuItem_Click;
            quitMenuItem.Click += quitMenuItem_Click;

            //  Hide the form because we don't need it, this is a notification tray application
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            RegisterHotkeys();

            LogSystem.Log("FreshTools started sucsessfully");
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.Run(new MainForm());
        }

        public void LoadConfig()
        {
            settingsFile = new VariablesFile(configFilePath, null, false);
            VariableLibrary vars = settingsFile.variables;
            
            //load variables
            bool windowHotKeys = WindowManager.HotKeysEnabled;
            WindowManager.HotKeysEnabled = vars.GetVariable("EnableWindowManager", ref windowHotKeys, true).Boolean;

            //vars.GetVariable("testVariable", ref testVariable, true);
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
            this.Close();
        }
        #endregion

        #region HotKey Events
        private static void RegisterHotkeys()
        {
            //register hotkey(s)
            //GenericsClass.LogSystem("Registering Hotkeys");
            HotKeyManager.GenericHotKeyPressedHandler += new EventHandler<HotKeyEventArgs>(GenericHotKeyPressed);
            HotKeyManager.RegisterHotKey((KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift), Keys.Oemtilde);
        }

        private static void GenericHotKeyPressed(object sender, HotKeyEventArgs args)
        {
            try
            {
                if (args.Modifiers == (KeyModifiers.NoRepeat | KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift) && args.Key == Keys.Oemtilde)
                {
                    LogSystem.Log("MainForm.HotKeyPressed() - Super Tilde - " + args.Modifiers + "+" + args.Key + "");
                }
                else //unknown hot key pressed
                {
                    //uncaught hotkey
                    LogSystem.Log("MainForm.HotKeyPressed() - UnActioned - " + args.Modifiers + "+" + args.Key + "");
                }
            }
            catch (Exception e)
            {
                LogSystem.Log("Exception#" + LogSystem.IncrementExceptionCount() + " in MainForm.HotKeyPressed(object,HotKeyEventArgs) - " + e);
            }
        }
        #endregion
    }
}
