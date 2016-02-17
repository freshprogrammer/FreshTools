using System.Threading;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace FreshTools
{
    class IdleMonitor
    {
        private TimeSpan idleTime;
        private DateTime lastCheckTime;
        private Point lastMousePos;

        private const int DEFAULT_CHECK_INTERVAL = 30;
        private const int DEFAULT_PREVENTION_ACTION_INTERVAL = 4;

        private int idleCheckInterval = DEFAULT_CHECK_INTERVAL;
        private int idlePreventionActionInterval = DEFAULT_PREVENTION_ACTION_INTERVAL;

        private Thread clockThread;

        public bool BalloonOnIdlePrevention = false;
        public NotifyIcon NotifyIcon;

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
            Console.WriteLine("IdleMonitor.StartClockThread()");
            if (clockThread == null)
            {
                lastCheckTime = DateTime.Now;
                idleTime = TimeSpan.Zero;

                clockThread = new Thread(new ThreadStart(ClockThreadRunning));
                clockThread.Start();
            }
        }

        public void StopClockThread()
        {
            Console.WriteLine("IdleMonitor.StopClockThread()");
            if (clockThread != null)
            {
                try
                {
                    clockThread.Abort();
                    clockThread = null;
                }
                catch (ThreadAbortException)
                {
                }
                finally
                {
                    clockThread = null;
                }
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
                        if (idleTime.TotalMinutes >= idlePreventionActionInterval)
                        {
                            DoIdlePreventionAction();

                            if (BalloonOnIdlePrevention && NotifyIcon!=null)
                            {
                                NotifyIcon.BalloonTipText = "Prevented Idle";
                                NotifyIcon.ShowBalloonTip(1000);
                            }
                        }
                    }
                    else
                        idleTime = TimeSpan.Zero;

                    Console.WriteLine("IdleMonitor.ClockThreadRunning() - Computer Idle time: " + idleTime + "");
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
            //Console.WriteLine("IsComputerIdle() - " + result);
            return result;
        }

        /// <summary>
        /// Perform action to prevent this computer from being Idle. (like press the ctrl key, or move Cursor)
        /// </summary>
        private void DoIdlePreventionAction()
        {
            Console.WriteLine("IdleMonitor.DoIdlePreventionAction()");
            //Move Cursor 1,1
            Point newPos = Cursor.Position;
            newPos.X += 5;
            newPos.Y += 5;
            Cursor.Position = newPos;//tricks IsComputerIdle() test but doesn't trick windows
            SendKeys.SendWait("^");//sends ctrl key - keeps computer awake
        }
    }
}
