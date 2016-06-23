using System;
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
    }
}
