using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace FreshTools
{
    public static class LogSystem
    {
        //file
        private static object logFileLock = new Object();
        private static string logFileName = @"log.txt";
        private static int logFileCount = 9;//This can only handle up to single digits
        private static int logHistoryCount = 50;//number of records to hold in memory

        public static string TimeStampFormat = "MM/dd/yyyy hh:mm:ss:ffff tt";
        public static bool IncludeTimeStampInConsole = false;

        //data
        private static int exceptionCount = 0;
        private static List<LogRecord> logRecords = new List<LogRecord>(logHistoryCount);


        /// <summary>
        /// Create Log file with rolling logs in users local appdata folder for this application
        /// </summary>
        public static void Init()
        {
            //C:\Users\USER\AppData\Local\APPNAME\logs\log.txt
            string logFile = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\" + Assembly.GetExecutingAssembly().GetName().Name + @"\logs\" + logFileName;
            Init(logFile);
        }

        /// <summary>
        /// Create Log file with rolling logs - move log to next log file up (1 to 2) up to the limit
        /// </summary>
        /// <param name="logFileFullName">full path to destination log file</param>
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

            Log(Assembly.GetExecutingAssembly().GetName().Name + " (v" + FreshArchives.TrimVersionNumber(Assembly.GetExecutingAssembly().GetName().Version) + ")");
        }

        public static void Log(string log, LogLevel logLevel=LogLevel.Information, string tag=null)
        {
            MethodBase mb = new StackTrace().GetFrame(1).GetMethod();
            string methodName = mb.DeclaringType + "." + mb.Name;

            LogRecord rec = new LogRecord(log, methodName, logLevel, tag);
            AppendLog(rec);

            string timeStamp = rec.Time.ToString(TimeStampFormat);
            string consoleOutput = rec.Method + "-" + rec.Message;
            string logFileOutput = timeStamp + "::" + rec.Method + "-" + rec.Message;

            if (IncludeTimeStampInConsole)
                consoleOutput = timeStamp + consoleOutput;
            Console.WriteLine(consoleOutput);

            lock (logFileLock)
            {
                using (StreamWriter sw = (File.Exists(logFileName)) ? File.AppendText(logFileName) : File.CreateText(logFileName))
                {
                    sw.WriteLine(logFileOutput);
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
        /// Returns X log records sepereated by line feeds
        /// </summary>
        /// <param name="count">Number of records to return. -1 for all in memory</param>
        /// <returns></returns>
        public static string GetLogData(int count=-1)
        {
            string result = "";
            if (count == -1)//include all
            {
                foreach (LogRecord l in logRecords)
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

        private static void AppendLog(LogRecord log)
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

    public struct LogRecord
    {
        public string Message;
        public string Method;
        public LogLevel Level;
        public string Tag;
        public DateTime Time;

        public LogRecord(string message, string method, LogLevel level=LogLevel.Information, string tag=null, DateTime time=default(DateTime))
        {
            if (time == default(DateTime))
                time = DateTime.Now;
            if (tag == null)
                tag = "";

            Message = message;
            Method = method;
            Level = level;
            Tag = tag;
            Time = time;
        }

        public override string ToString()
        {
            return "LogRecord("+Time+" "+ Method + "-" + Message + ")";
        }
    }

    public enum LogLevel
    {
        Verbose=5,
        Information=4,
        Warning=3,
        Error=2,
        FatalError=1
    }
}
