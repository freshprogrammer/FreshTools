using System;
using System.Collections.Generic;
using System.IO;

namespace FreshTools
{
    class LogSystem
    {
        //file
        private static object logFileLock = new Object();
        private static string logFileName = @"log.txt";
        private static int logFileCount = 9;//This can only handle up to single digits
        private static int logHistoryCount = 50;

        //data
        private static int exceptionCount = 0;
        private static List<string> logRecords = new List<string>(logHistoryCount);

        /// <summary>
        /// Create Log file with rolling logs - move log to next log file up (1 to 2) up to the limit
        /// </summary>
        /// <param name="logFileFullName"></param>
        public static void Init(string logFileFullName)
        {
            logFileName = logFileFullName;
            int logStorageMax = logFileCount;//single digits
            int logIndex = logStorageMax;
            while (logIndex >= 1)
            {
                string logOlder = logFileName.Replace(".txt", "_" + logIndex + ".txt");
                string logNewer;
                if (logIndex > 1)
                    logNewer = logFileName.Replace(".txt", "_" + (logIndex - 1) + ".txt");
                else
                    logNewer = logFileName;
                MoveFileOverwrite(logNewer, logOlder);
                logIndex--;
            }
            //create any necisarry directories for logs
            Directory.CreateDirectory(Path.GetDirectoryName(logFileName));
        }

        public static void Log(string log)
        {
            string timeStamp = DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss:ffff tt");
            log = timeStamp + "::" + log;
            AppendLog(log);
            Console.WriteLine(log);

            lock (logFileLock)
            {
                using (StreamWriter sw = (File.Exists(logFileName)) ? File.AppendText(logFileName) : File.CreateText(logFileName))
                {
                    sw.WriteLine(log);
                    sw.Flush();
                    sw.Close();
                }
            }
        }

        public static int IncrementExceptionCount()
        {
            return ++exceptionCount;
        }

        public static int GetLogCount()
        {
            return logRecords.Count;
        }

        /// <summary>
        /// Returns X log records
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public static string GetLogData(int count=-1)
        {
            string result = "";
            if (count == -1)//include all
            {
                foreach (string l in logRecords)
                {
                    result += l + "\n";
                }
            }
            else
            {
                if (count > logRecords.Count) count = logRecords.Count;
                for (int x = logRecords.Count - count; x < logRecords.Count; x++)
                {
                    result += logRecords[x] + "\n";
                }
            }
            return result.TrimEnd();
        }

        private static void AppendLog(string log)
        {
            //not a great implementation but it works. Total waste of ReShuffleing RAM when full and removing
            if (logRecords.Count == logRecords.Capacity)
                logRecords.RemoveAt(0);
            logRecords.Add(log);
        }

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
