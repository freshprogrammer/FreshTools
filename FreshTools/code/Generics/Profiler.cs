//#define FreshProfilerEnabled

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace FreshTools
{
    /// <summary>
    /// Profiler running is based on the preprocessor directive "FreshProfilerEnabled" at the head of this file
    /// </summary>
    public class Profiler
    {

        private const string lineBreak = "\n";
        private const bool CaseSensitive = false;
        private static readonly DateTime zeroHour;
        private static List<CodeProfile> profiles;
        private static List<CodeProfile> activeProfiles;

        static Profiler()
        {
            zeroHour = new DateTime(1970, 1, 1, 0, 0, 0);
            profiles = new List<CodeProfile>();
            activeProfiles = new List<CodeProfile>();
        }

        public static void Start()
        {
#if (FreshProfilerEnabled)
            MethodBase mb = new StackTrace().GetFrame(1).GetMethod();
            string methodName = mb.DeclaringType + "." + mb.Name;
            Start(methodName);
#endif
        }

        public static CodeProfile Stop()
        {
#if FreshProfilerEnabled
            MethodBase mb = new StackTrace().GetFrame(1).GetMethod();
            string methodName = mb.DeclaringType + "." + mb.Name;
            return Stop(methodName);
#else
            return null;
#endif
        }

        public static void Start(string methodName)
        {
#if (FreshProfilerEnabled)
            bool active;
            CodeProfile profile = GetProfile(methodName, out active);

            if (active)
            {
                //already profiling...
            }
            else
            {
                if (profile == null)
                {
                    profile = new CodeProfile(methodName);
                    profiles.Add(profile);
                }

                activeProfiles.Add(profile);

                // get cur time last - right before leaving the method - as little time overhead as possible
                double curTime = (DateTime.Now - zeroHour).TotalMilliseconds;
                profile.StartTime = curTime;
            }
#endif
        }

        public static CodeProfile Stop(string methodName)
        {
#if (FreshProfilerEnabled)
            // get cur time First - As soon as possible - as little time overhead as possible
            double curTime = (DateTime.Now - zeroHour).TotalMilliseconds;
            bool active;
            CodeProfile profile = GetProfile(methodName, out active);

            if (active)
            {
                double durration = curTime - profile.StartTime;
                profile.AddProfileTime(durration);
                activeProfiles.Remove(profile);
                return profile;
            }
            else
            {
                //not profiling... cant do anything
            }
#endif
            return null;
        }

        public static void SaveDataToFile(string filePath, bool includeData)
        {
#if (FreshProfilerEnabled)
            bool csvFormat = true;
            string comma = "\t ";
            string saveString = "";
            string indent = "--";

            //should probably sort 

            foreach (CodeProfile profile in profiles)
            {
                if (!csvFormat)
                {
                    saveString += profile.Name + lineBreak;
                    saveString += indent + " Total Calls:" + profile.TotalCalls + lineBreak;
                    saveString += indent + " Total Time:" + profile.TotalTime + lineBreak;
                    saveString += indent + " Min:" + profile.MinTime + lineBreak;
                    saveString += indent + " Max:" + profile.MaxTime + lineBreak;
                    saveString += indent + " Average Time:" + profile.AverageTime + lineBreak;
                }
                else
                {
                    saveString += profile.Name + comma;
                    saveString += indent + profile.TotalCalls + comma;
                    saveString += indent + profile.TotalTime + comma;
                    saveString += indent + profile.MinTime + comma;
                    saveString += indent + profile.MaxTime + comma;
                    saveString += indent + profile.AverageTime + lineBreak;
                }
                if (includeData)
                {
                    string profileData = null;
                    foreach (double d in profile.CallTimes)
                    {
                        if (profileData == null)
                            profileData = "" + d;
                        else
                            profileData += "," + d;
                    }
                    saveString += indent + " Data:" + profileData + lineBreak;
                }
            }
            System.IO.File.WriteAllText(filePath, saveString);
#endif
        }

        public static CodeProfile GetProfile(string name, out bool active)
        {
            CodeProfile result = null;
            active = false;
#if (FreshProfilerEnabled)
            foreach (CodeProfile profile in activeProfiles)
            {
                if (String.Compare(profile.Name, name, !CaseSensitive) == 0)
                {
                    result = profile;
                    active = true;
                    break;
                }
            }

            if (result == null)
            {
                foreach (CodeProfile profile in profiles)
                {
                    if (String.Compare(profile.Name, name, !CaseSensitive) == 0)
                    {
                        result = profile;
                        active = false;
                        break;
                    }
                }
            }
#endif
            return result;
        }
    }
    public class CodeProfile
    {
        private double recentMinTime;
        private double recentMaxTime;
        private double recentTotalTime;
        private int recentCalls;
        private List<double> recentCallTimes;

        private double allTimeMinTime;
        private double allTimeMaxTime;
        private double allTimeTotalTime;
        private int allTimeCalls;
        private List<double> allTimeCallTimes;

        public string Name;

        public double MinTime { get { return recentMinTime; } }
        public double MaxTime { get { return recentMaxTime; } }
        public double TotalTime { get { return recentTotalTime; } }
        public double AverageTime { get { if (recentCalls == 0)return 0; return recentTotalTime / recentCalls; } }
        public int TotalCalls { get { return recentCalls; } }
        public List<double> CallTimes { get { return recentCallTimes; } }

        public double AllTimeMinTime { get { return allTimeMinTime; } }
        public double AllTimeMaxTime { get { return allTimeMaxTime; } }
        public double AllTimeTotalTime { get { return allTimeTotalTime; } }
        public double AllTimeAverageTime { get { if (allTimeCalls == 0)return 0; return allTimeTotalTime / allTimeCalls; } }
        public int AllTimeTotalCalls { get { return allTimeCalls; } }
        public List<double> AllTimeCallTimes { get { return allTimeCallTimes; } }

#pragma warning disable
        /// <summary>
        /// This is used temperaraly to record start time - nothing else
        /// </summary>
        public double StartTime;
#pragma warning restore

        public CodeProfile(string name)
        {
            Name = name;
            recentCallTimes = new List<double>();
            allTimeCallTimes = new List<double>();
            ResetAll();
        }

        public void ResetRecent()
        {
            recentMinTime = int.MaxValue;
            recentMaxTime = int.MinValue;
            recentTotalTime = 0;
            recentCalls = 0;
            recentCallTimes.Clear();
        }

        public void ResetAll()
        {
            ResetRecent();
            allTimeMinTime = int.MaxValue;
            allTimeMaxTime = int.MinValue;
            allTimeTotalTime = 0;
            allTimeCalls = 0;
            allTimeCallTimes.Clear();
        }

        public void AddProfileTime(double time)
        {
            if (time < recentMinTime)
                recentMinTime = time;
            if (time > recentMaxTime)
                recentMaxTime = time;

            recentCalls++;
            recentTotalTime += time;
            recentCallTimes.Add(time);

            if (time < allTimeMinTime)
                allTimeMinTime = time;
            if (time > allTimeMaxTime)
                allTimeMaxTime = time;

            allTimeCalls++;
            allTimeTotalTime += time;
            allTimeCallTimes.Add(time);
        }

        public override string ToString()
        {
            return Name + "(" + recentCalls + " calls, Avg:" + AverageTime.ToString("F4") + ") (" + allTimeCalls + " calls All Time, Avg:" + AllTimeAverageTime.ToString("F4") + ")";
        }
    }
}