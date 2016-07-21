using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FreshTools
{
    class FreshArchives
    {
        /// <summary>
        /// Trims trailing zeros off a version number from 1.1.0.0 to 1.1
        /// </summary>
        /// <param name="ver">Version number to be trimmed</param>
        /// <returns></returns>
        public static string TrimVersionNumber(Version ver)
        {
            if (ver.Revision != 0) return ver.ToString(4);
            if (ver.Build != 0) return ver.ToString(3);
            if (ver.Minor != 0) return ver.ToString(2);
            else return ver.ToString(1);
        }

        public static bool IsWindows10()
        {
            var reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string productName = (string)reg.GetValue("ProductName");
            return productName.StartsWith("Windows 10");
        }

        public static void AddApplicationToStartup()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                key.SetValue(Assembly.GetExecutingAssembly().GetName().Name, "\"" + Application.ExecutablePath + "\"");
            }
        }

        public static void RemoveApplicationFromStartup()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                key.DeleteValue(Assembly.GetExecutingAssembly().GetName().Name, false);
            }
        }

        public static bool IsApplicationInStartup()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                var val = key.GetValue(Assembly.GetExecutingAssembly().GetName().Name);
                return val != null;
            }
        }

        /// <summary>
        /// Parses "X,Y,Width,Hight" into RectangleF 
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static RectangleF ParseRectangleF(string input)
        {
            try
            {
                RectangleF result = RectangleF.Empty;
                string[] vals = input.Split(',');
                result.X = float.Parse(vals[0]);
                result.Y = float.Parse(vals[1]);
                result.Width = float.Parse(vals[2]);
                result.Height = float.Parse(vals[3]);
                return result;
            }
            catch (Exception) { return RectangleF.Empty; }
        }

        /// <summary>
        /// Move the file to new destination, deleting any file that might exist there
        /// </summary>
        /// <param name="src">current full path to file</param>
        /// <param name="dest">full path to new file</param>
        public static void MoveFileOverwrite(string src, string dest)
        {
            if (File.Exists(src))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                if (File.Exists(dest))
                    File.Delete(dest);
                File.Move(src, dest);
            }
        }
    }
}
