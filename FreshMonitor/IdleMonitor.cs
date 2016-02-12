using System.Threading;
using System;

namespace FreshMonitor
{
    class IdleMonitor
    {
        private bool isIdle = false;
        private TimeSpan idleTime;
        private DateTime lastCheckTime;

        private const int DEFAULT_CHECK_INTERVAL = 60;
        private const int DEFAULT_PREVENTION_ACTION_INTERVAL = 13;

        private int idleCheckInterval = DEFAULT_CHECK_INTERVAL;
        private int idlePreventionActionInterval = DEFAULT_PREVENTION_ACTION_INTERVAL;

        private Thread clockThread;

        public IdleMonitor()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="checkInterval">How frequently this will check if you are idle (seconds)</param>
        /// <param name="actionInterval">How long you need to be idle before an action is taken (minutes)</param>
        public void StartIdleProtection(int checkInterval = DEFAULT_CHECK_INTERVAL, int actionInterval = DEFAULT_PREVENTION_ACTION_INTERVAL)
        {
            this.idleCheckInterval = checkInterval*1000;
            this.idlePreventionActionInterval = actionInterval;

            StartClockThread();
        }

        private void StartClockThread()
        {
            if (clockThread == null)
            {
                lastCheckTime = DateTime.Now;
                idleTime = TimeSpan.Zero;

                clockThread = new Thread(new ThreadStart(ClockThreadRunning));
                clockThread.Start();
            }
        }

        private void ClockThreadRunning()
        {
            Thread.CurrentThread.Name = "IdleMonitor.ClockThread";
            Thread.CurrentThread.IsBackground = true;
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

            try
            {
                while (true)
                {
                    if (IsComputerIdle())
                    {
                        idleTime.Add(DateTime.Now - lastCheckTime);
                        
                        //if idle for too long
                        if(idleTime.Minutes>=idlePreventionActionInterval)
                            DoIdlePreventionAction();
                    }
                    else
                        idleTime = TimeSpan.Zero;

                    lastCheckTime = DateTime.Now;
                    Thread.Sleep(idleCheckInterval);
                }
            }
            catch (ThreadAbortException)
            {
                // Thead was aborted
                clockThread = null;
            }
        }

        private bool IsComputerIdle()
        {
            //TODO STUB
            return true;
        }

        /// <summary>
        /// Perform action to prevent this computer from being Idle. (like press the caps lock key)
        /// </summary>
        private void DoIdlePreventionAction()
        {
            //TODO STUB
        }
    }
}
