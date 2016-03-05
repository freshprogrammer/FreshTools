﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace FreshTools
{
    //used implementation from here http://stackoverflow.com/questions/3654787/global-hotkey-in-console-application
    public static class HotKeyManager
    {
        private static int _id = 0;
        public static EventHandler<HotKeyEventArgs> GenericHotKeyPressedHandler;
        private static List<HotKey> hotKeys = new List<HotKey>();

        #region External Functions
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        #endregion

        public static HotKey RegisterHotKey(KeyModifiers modifiers, Keys key)
        {
            _windowReadyEvent.WaitOne();
            int id = System.Threading.Interlocked.Increment(ref _id);
            _wnd.Invoke(new RegisterHotKeyDelegate(RegisterHotKeyInternal), _hwnd, id, (uint)modifiers, (uint)key);

            HotKey k = new HotKey(id, modifiers, key, GenericHotKeyPressedHandler);
            hotKeys.Add(k);
            return k;
        }

        public static HotKey RegisterHotKey(KeyModifiers modifiers, Keys key, EventHandler handler)
        {
            _windowReadyEvent.WaitOne();
            int id = System.Threading.Interlocked.Increment(ref _id);
            _wnd.Invoke(new RegisterHotKeyDelegate(RegisterHotKeyInternal), _hwnd, id, (uint)modifiers, (uint)key);

            HotKey k = new HotKey(id, modifiers, key, handler);
            hotKeys.Add(k);
            return k;
        }

        public static HotKey RegisterHotKey(KeyModifiers modifiers, Keys key, EventHandler<HotKeyEventArgs> handler)
        {
            _windowReadyEvent.WaitOne();
            int id = System.Threading.Interlocked.Increment(ref _id);
            _wnd.Invoke(new RegisterHotKeyDelegate(RegisterHotKeyInternal), _hwnd, id, (uint)modifiers, (uint)key);

            HotKey k = new HotKey(id, modifiers, key, handler);
            hotKeys.Add(k);
            return k;
        }

        public static void UnregisterHotKey(int id)
        {
            for (int i = hotKeys.Count - 1; i >= 0; i--)
            {
                if (hotKeys[i].Id == id)
                {
                    _wnd.Invoke(new UnRegisterHotKeyDelegate(UnRegisterHotKeyInternal), _hwnd, id);
                    hotKeys.RemoveAt(i);
                    break;
                }
            }
        }

        public static void UnregisterHotKey(KeyModifiers modifiers, Keys keys)
        {
            for (int i = hotKeys.Count - 1; i >= 0; i--)
            {
                if (hotKeys[i].Modifiers == modifiers && hotKeys[i].Keys == keys)
                {
                    _wnd.Invoke(new UnRegisterHotKeyDelegate(UnRegisterHotKeyInternal), _hwnd, hotKeys[i].Id);
                    hotKeys.RemoveAt(i);
                    break;
                }
            }
        }

        delegate void RegisterHotKeyDelegate(IntPtr hwnd, int id, uint modifiers, uint key);
        delegate void UnRegisterHotKeyDelegate(IntPtr hwnd, int id);

        private static void RegisterHotKeyInternal(IntPtr hwnd, int id, uint modifiers, uint key)
        {
            RegisterHotKey(hwnd, id, modifiers, key);
        }

        private static void UnRegisterHotKeyInternal(IntPtr hwnd, int id)
        {
            UnregisterHotKey(_hwnd, id);
        }

        private static void OnHotKeyPressed(HotKeyEventArgs e)
        {
            foreach (HotKey k in hotKeys)
            {
                if (e.ID == k.Id)
                {
                    if (k.Handler != null)
                        k.Handler(null, e);
                    else if (k.GenericHandler != null)
                        k.GenericHandler(null, e);
                    break;
                }
            }
        }

        private static volatile MessageWindow _wnd;
        private static volatile IntPtr _hwnd;
        private static ManualResetEvent _windowReadyEvent = new ManualResetEvent(false);
        static HotKeyManager()
        {
            Thread messageLoop = new Thread(delegate()
            {
                Application.Run(new MessageWindow());
            });
            messageLoop.Name = "HotKeyManager.MessageLoopThread";
            messageLoop.IsBackground = true;
            messageLoop.Start();
        }

        private class MessageWindow : Form
        {
            private const int WM_HOTKEY = 0x312;

            public MessageWindow()
            {
                _wnd = this;
                _hwnd = this.Handle;
                _windowReadyEvent.Set();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    HotKeyEventArgs e = new HotKeyEventArgs(m.LParam, m.WParam);
                    HotKeyManager.OnHotKeyPressed(e);
                }

                base.WndProc(ref m);
            }

            protected override void SetVisibleCore(bool value)
            {
                // Ensure the window never becomes visible
                base.SetVisibleCore(false);
            }
        }
    }

    protected struct HotKey
    {
        public int Id;
        public KeyModifiers Modifiers;
        public Keys Keys;
        public EventHandler Handler;
        public EventHandler<HotKeyEventArgs> GenericHandler;

        public HotKey(int id, KeyModifiers modifiers, Keys key, EventHandler handler)
        {
            this.Id = id;
            this.Modifiers = modifiers;
            this.Keys = key;
            this.Handler = handler;
            this.GenericHandler = null;
        }

        public HotKey(int id, KeyModifiers modifiers, Keys key, EventHandler<HotKeyEventArgs> genericHandler)
        {
            this.Id = id;
            this.Modifiers = modifiers;
            this.Keys = key;
            this.Handler = null;
            this.GenericHandler = genericHandler;
        }
    }

    public class HotKeyEventArgs : EventArgs
    {
        public readonly Keys Key;
        public readonly KeyModifiers Modifiers;
        public readonly int ID;

        public HotKeyEventArgs(Keys key, KeyModifiers modifiers)
        {
            this.Key = key;
            this.Modifiers = modifiers;
            this.ID = -1;
        }

        public HotKeyEventArgs(IntPtr hotKeyParam, IntPtr wParam)
        {
            uint param = (uint)hotKeyParam.ToInt64();
            Key = (Keys)((param & 0xffff0000) >> 16);
            Modifiers = (KeyModifiers)(param & 0x0000ffff);
            this.ID = (int)wParam;
        }
    }

    [Flags]
    public enum KeyModifiers
    {
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8,
        NoRepeat = 0x4000
    }
}
