using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Security.Principal;

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

        public static bool IsUserAdministrator()
        {
            bool isAdmin;
            WindowsIdentity user = null;
            try
            {
                //get the currently logged in user
                user = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(user);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (UnauthorizedAccessException)
            {
                isAdmin = false;
            }
            catch (Exception)
            {
                isAdmin = false;
            }
            finally
            {
                if (user != null)
                    user.Dispose();
            }
            return isAdmin;
        }

        public static void AddApplicationToStartup()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                key.SetValue(Assembly.GetExecutingAssembly().GetName().Name, "\"" + Assembly.GetEntryAssembly().Location + "\"");
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
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false))
            {
                var val = key.GetValue(Assembly.GetExecutingAssembly().GetName().Name);
                return val != null;
            }
        }

        /// <summary>
        /// Parses "X,Y,Width,Hight" into RectangleF. Returns zero rectangle if failure
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static RectangleF ParseRectangleF(string input)
        {
            RectangleF result = RectangleF.Empty;
            string[] vals = input.Split(',');
            float x = 0, y = 0, w = 0, h = 0;
            bool valid = true;
            valid = valid && float.TryParse(vals[0], out x);
            valid = valid && float.TryParse(vals[1], out y);
            valid = valid && float.TryParse(vals[2], out w);
            valid = valid && float.TryParse(vals[3], out h);

            if (valid)
            {
                result.X = x;
                result.Y = y;
                result.Width = w;
                result.Height = h;
            }
            return result;
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
