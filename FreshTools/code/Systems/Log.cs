using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace FreshTools
{
    public static class Log
    {
        //file
        private static object logFileLock = new Object();
        private static string logFileName = @"log.txt";
        private static int logFileCount = 9;//This can only handle up to single digits

        private const int LOG_MEMORY_DEFAULT_COUNT = 10000;
        private static int logMemoryCount;//number of records to hold in memory

        public static string TimeStampFormat = "MM/dd/yyyy HH:mm:ss:ffff";
        public static bool IncludeTimeStampInConsole = false;
        public static LogLevel ConsoleLogLevel = LogLevel.Information;
        public static LogLevel LogFileLogLevel = LogLevel.Verbose;
        public static int LogCount { get { return logCount; } set { } }

        //data
        private static int logCount = 0;
        private static int exceptionCount = 0;
        private static FixedSizeArray<LogRecord> logRecords;

        /// <summary>
        /// Create Log file with rolling logs in users local appdata folder for this application
        /// </summary>
        public static void Init(int logMemoryCount = LOG_MEMORY_DEFAULT_COUNT)
        {
            //C:\Users\USER\AppData\Local\APPNAME\logs\log.txt
            string logFile = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\" + Assembly.GetExecutingAssembly().GetName().Name + @"\logs\" + logFileName;
            Init(logFile, logMemoryCount);
        }

        /// <summary>
        /// Create Log file with rolling logs - move log to next log file up (1 to 2) up to the limit
        /// </summary>
        /// <param name="logFileFullName">full path to destination log file</param>
        public static void Init(string logFileFullName, int logMemoryCount = LOG_MEMORY_DEFAULT_COUNT)
        {
            //setup local variables
            Log.logMemoryCount = logMemoryCount;
            logCount = 0;
            exceptionCount = 0;
            logRecords = new FixedSizeArray<LogRecord>(logMemoryCount);

            //setup log file(s)
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

            //create first log record
            I(Assembly.GetExecutingAssembly().GetName().Name + " (v" + FreshArchives.TrimVersionNumber(Assembly.GetExecutingAssembly().GetName().Version) + ")");
        }

        private static void LogMessage(string msg, LogLevel logLevel = LogLevel.Information, string tag = null, int stackDepth = 2)
        {
            MethodBase mb = new StackTrace().GetFrame(stackDepth).GetMethod();
            string methodName = mb.DeclaringType + "." + mb.Name;

            LogRecord rec = new LogRecord(msg, methodName, logLevel, tag);
            AppendLog(rec);

            if (rec.LogLevel <= ConsoleLogLevel)
                Console.WriteLine(rec.ToConsoleString(IncludeTimeStampInConsole, TimeStampFormat));

            if (rec.LogLevel <= LogFileLogLevel)
            {
                lock (logFileLock)
                {
                    using (StreamWriter sw = (File.Exists(logFileName)) ? File.AppendText(logFileName) : File.CreateText(logFileName))
                    {
                        sw.WriteLine(rec.ToLogFileString(TimeStampFormat));
                        sw.Flush();
                        sw.Close();
                    }
                }
            }
        }

        public static void V(string msg, string tag = null) { LogMessage(msg, LogLevel.Verbose, tag); }
        public static void I(string msg, string tag = null) { LogMessage(msg, LogLevel.Information, tag); }
        public static void W(string msg, string tag = null) { LogMessage(msg, LogLevel.Warning, tag); }
        public static void E(string msg, string tag = null) { LogMessage(msg, LogLevel.Error, tag); }
        public static void F(string msg, string tag = null) { LogMessage(msg, LogLevel.FatalError, tag); }

        public static void WTF(string msg, string tag = null)
        {
            Debug.Assert(false);
            LogMessage(msg, LogLevel.FatalError, tag);
        }

        public static void Exception(Exception e)
        {
            IncrementExceptionCount();
            MethodBase mb = new StackTrace().GetFrame(1).GetMethod();
            string methodName = mb.DeclaringType + "." + mb.Name;
            E("Exception#" + exceptionCount + " in " + methodName + " - " + e);
        }

        public static int IncrementExceptionCount()
        {
            return ++exceptionCount;
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
                    result += l.Method + "-" + l.Message + "\n";
                }
            }
            else
            {
                if (count > logRecords.Count) count = logRecords.Count;
                for (int x = logRecords.Count - count; x < logRecords.Count; x++)
                {
                    result += logRecords[x].Method + "-" + logRecords[x].Message + "\n";
                }
            }
            return result.TrimEnd();
        }

        private static void AppendLog(LogRecord log)
        {
            logCount++;
            //not a great implementation but it works. Total waste of ReShuffleing RAM when full and removing
            if (logRecords.Count == logRecords.Capacity)
                logRecords.Remove(0);
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
        public LogLevel LogLevel;
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
            LogLevel = level;
            Tag = tag;
            Time = time;
        }

        public string ToConsoleString(bool includeDate, string timeFormat)
        {
            if (includeDate)
                return Time.ToString(timeFormat) + "::" + Method + "-" + Message;
            return Method + "-" + Message;
        }

        public string ToLogFileString(string timeFormat)
        {
            return Time.ToString(timeFormat) + "::" + Method + "-" + Message;
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
