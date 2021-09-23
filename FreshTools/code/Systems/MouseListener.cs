using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;

/// <summary>
/// Based on  https://github.com/justcoding121/Windows-User-Action-Hook
/// </summary>
namespace FreshTools
{
    internal class RawMouseEventArgs : EventArgs
    {
        internal MouseMessages Message { get; set; }
        internal POINT Point { get; set; }
        internal uint MouseData { get; set; }
    }

    public enum MouseMessages
    {
        WM_LBUTTONDOWN = 0x0201,
        WM_LBUTTONUP = 0x0202,
        WM_MOUSEMOVE = 0x0200,
        WM_MOUSEWHEEL = 0x020A,
        WM_RBUTTONDOWN = 0x0204,
        WM_RBUTTONUP = 0x0205
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public readonly int x;
        public readonly int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSLLHOOKSTRUCT
    {
        internal POINT pt;
        internal readonly uint mouseData;
        internal readonly uint flags;
        internal readonly uint time;
        internal readonly IntPtr dwExtraInfo;
    }

    internal class MouseHook
    {
        private const int WH_MOUSE_LL = 14;

        private readonly LowLevelMouseProc Proc;
        private static IntPtr _hookId = IntPtr.Zero;
        internal event EventHandler<RawMouseEventArgs> MouseAction = delegate { };

        internal MouseHook()
        {
            Proc = HookCallback;
        }
        internal void Start()
        {
            _hookId = SetHook(Proc);
        }

        internal void Stop()
        {
            UnhookWindowsHookEx(_hookId);
        }

        private static IntPtr SetHook(LowLevelMouseProc proc)
        {
            var hook = SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle("user32"), 0);
            if (hook == IntPtr.Zero) throw new Win32Exception();
            return hook;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {

            MSLLHOOKSTRUCT hookStruct;
            if (nCode < 0) return CallNextHookEx(_hookId, nCode, wParam, lParam);

            hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

            MouseAction(null, new RawMouseEventArgs() { Message = (MouseMessages)wParam, Point = hookStruct.pt, MouseData = hookStruct.mouseData });

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    }

    class MouseEventArgs : EventArgs
    {
        public MouseMessages Message { get; set; }
        public POINT Point { get; set; }
        public short WheelDelta { get; set; }
    }

    class MouseListener
    { 
        private static bool isRunning { get; set; }
        private static object accesslock = new object();

        //private static AsyncCollection<object> mouseQueue;
        private static Queue queue;
        private static MouseHook mouseHook;

        public static event EventHandler<MouseEventArgs> OnMouseInput;

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(UInt16 virtualKeyCode);
        public static bool RightMouseButtonDown { get { return (GetAsyncKeyState((ushort)Keys.RButton) & 0x8000) != 0; } }

        /// <summary>
        /// Start watching mouse events
        /// </summary>
        public static void Start()
        {
            if (!isRunning)
            {
                lock (accesslock)
                {
                    //mouseQueue = new AsyncCollection<object>();
                    queue = new Queue();

                    mouseHook = new MouseHook();
                    mouseHook.MouseAction += MListener;

                    //low level hooks need to be registered in the context of a UI thread
                    Task.Factory.StartNew(() => { }).ContinueWith(x =>
                    {
                        mouseHook.Start();

                    }, SharedMessagePump.GetTaskScheduler());

                    Task.Factory.StartNew(() => ConsumeKeyAsync());

                    isRunning = true;
                }
            }
        }

        /// <summary>
        /// Stop watching mouse events
        /// </summary>
        public static void Stop()
        {
            if (isRunning)
            {
                lock (accesslock)
                {
                    if (mouseHook != null)
                    {
                        mouseHook.MouseAction -= MListener;
                        mouseHook.Stop();
                        mouseHook = null;
                    }
                    //mouseQueue.Add(false);
                    queue.Enqueue(false);
                    isRunning = false;
                }
            }
        }

        /// <summary>
        /// Add mouse event to our producer queue
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void MListener(object sender, RawMouseEventArgs e)
        {
            //mouseQueue.Add(e);
            try
            {
                queue.Enqueue(e);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }

        /// <summary>
        /// Consume mouse events in our producer queue asynchronously
        /// </summary>
        /// <returns></returns>
        //private static async Task ConsumeKeyAsync()
        private static void ConsumeKeyAsync()
        {
            while (isRunning)
            {

                //blocking here until a key is added to the queue
                //var item = await mouseQueue.TakeAsync();
                while(queue.Count==0)
                {
                    Thread.Sleep(3);
                }
                var item = queue.Dequeue();
                if (item is bool)
                    break;

                if(item!=null)
                    MListener_MouseEvent(item as RawMouseEventArgs);
            }
        }

        /// <summary>
        /// Invoke user callbacks with the argument
        /// </summary>
        /// <param name="kd"></param>
        private static void MListener_MouseEvent(RawMouseEventArgs kd)
        {
            //only care about wheel events
            if (kd.Message.ToString() == "WM_MOUSEWHEEL")
            {
                short wheelDelta = (short)(kd.MouseData >> 16);
                OnMouseInput?.Invoke(null, new MouseEventArgs() { Message = kd.Message, Point = kd.Point, WheelDelta = wheelDelta });
            }
            //OnMouseInput?.Invoke(null, new MouseEventArgs() { Message = kd.Message, Point = kd.Point });
        }
    }

    /// <summary>
    /// A class to create a dummy message pump if we don't have one
    /// A message pump is required for most of our hooks to succeed
    /// </summary>
    internal class SharedMessagePump
    {
        private static bool hasUIThread = false;

        static Lazy<TaskScheduler> scheduler;
        static Lazy<MessageHandler> messageHandler;

        static SharedMessagePump()
        {
            scheduler = new Lazy<TaskScheduler>(() =>
            {
                //if the calling thread is a UI thread then return its synchronization context
                //no need to create a message pump
                
                Dispatcher dispatcher = Dispatcher.FromThread(Thread.CurrentThread);
                if (dispatcher != null)
                {
                    if (SynchronizationContext.Current != null)
                    {
                        hasUIThread = true;
                        return TaskScheduler.FromCurrentSynchronizationContext();
                    }
                }

                TaskScheduler current = null;

                //if current task scheduler is null, create a message pump 
                //http://stackoverflow.com/questions/2443867/message-pump-in-net-windows-service
                //use async for performance gain!
                new Task(() =>
                {
                    Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                    {
                        current = TaskScheduler.FromCurrentSynchronizationContext();
                    }

               ), DispatcherPriority.Normal);
                    Dispatcher.Run();
                }).Start();

                //we called dispatcher begin invoke to get the Message Pump Sync Context
                //we check every 10ms until synchronization context is copied
                while (current == null)
                {
                    Thread.Sleep(10);
                }

                return current;

            });

            messageHandler = new Lazy<MessageHandler>(() =>
            {
                MessageHandler msgHandler = null;
                //get the mesage handler dummy window created using the UI sync context
                new Task((e) =>
                {
                    msgHandler = new MessageHandler();

                }, GetTaskScheduler()).Start();

                //wait here until the window is created on UI thread
                while (msgHandler == null)
                {
                    Thread.Sleep(10);
                };

                return msgHandler;
            });

            Initialize();
        }
        /// <summary>
        /// Initialize the required message pump for all the hooks
        /// </summary>
        private static void Initialize()
        {
            GetTaskScheduler();
            GetHandle();
        }
        /// <summary>
        /// Get the UI task scheduler
        /// </summary>
        /// <returns></returns>
        internal static TaskScheduler GetTaskScheduler()
        {
            return scheduler.Value;
        }

        /// <summary>
        /// Get the handle of the window we created on the UI thread
        /// </summary>
        /// <returns></returns>
        internal static IntPtr GetHandle()
        {
            var handle = IntPtr.Zero;

            if (hasUIThread)
            {
                try
                {
                    handle = Process.GetCurrentProcess().MainWindowHandle;

                    if (handle != IntPtr.Zero)
                        return handle;
                }
                catch { }
            }

            return messageHandler.Value.Handle;
        }

    }

    /// <summary>
    /// A dummy class to create a dummy invisible window object
    /// </summary>
    internal class MessageHandler : NativeWindow
    {

        internal MessageHandler()
        {
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message msg)
        {
            base.WndProc(ref msg);
        }
    }
}
