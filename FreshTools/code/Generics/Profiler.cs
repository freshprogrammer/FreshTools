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
        private double minTime = int.MaxValue;
        private double maxTime = -1;
        private double totalTime = 0;
        private int totalCalls = 0;

        private List<double> callTimes;

        public string Name;
        public double MinTime { get { return minTime; } }
        public double MaxTime { get { return maxTime; } }
        public double TotalTime { get { return totalTime; } }
        public double AverageTime { get { if (totalCalls == 0)return 0; return totalTime / totalCalls; } }
        public int TotalCalls { get { return totalCalls; } }
        public List<double> CallTimes { get { return callTimes; } }

#pragma warning disable
        /// <summary>
        /// This is used temperaraly to record start time - nothing else
        /// </summary>
        public double StartTime;
#pragma warning restore

        public CodeProfile(string name)
        {
            Name = name;
            callTimes = new List<double>();
        }

        public void AddProfileTime(double time)
        {
            if (time < minTime)
                minTime = time;
            if (time > maxTime)
                maxTime = time;

            totalCalls++;
            totalTime += time;
            callTimes.Add(time);
        }

        public override string ToString()
        {
            return Name + "(Avg:" + AverageTime + ")";
        }
    }
}