﻿using System.Threading;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace FreshMonitor
{
    class IdleMonitor
    {
        private TimeSpan idleTime;
        private DateTime lastCheckTime;
        private Point lastMousePos;

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
                        idleTime = idleTime.Add(DateTime.Now.Subtract(lastCheckTime));
                        
                        //if idle for too long
                        if(idleTime.TotalMinutes>=idlePreventionActionInterval)
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
            bool result = false;
            if (lastMousePos.Equals(Cursor.Position))
                result = true;
            lastMousePos = Cursor.Position;
            return result;
        }

        /// <summary>
        /// Perform action to prevent this computer from being Idle. (like press the caps lock key, or move Cursor)
        /// </summary>
        private void DoIdlePreventionAction()
        {
            //Move Cursor 1,1
            Point newPos = Cursor.Position;
            newPos.X += 10;
            newPos.Y += 10;
            Cursor.Position = newPos;
        }
    }
}
