using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TurboToggle
{
    class HotkeyManager : NativeWindow, IDisposable
    {
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const int WM_HOTKEY = 0x0312;

        int _nextId = 0xAA00;
        readonly Dictionary<int, Action> _actions = new Dictionary<int, Action>();

        public HotkeyManager() { CreateHandle(new CreateParams()); }

        public int Register(uint mods, uint vk, Action callback)
        {
            int id = _nextId++;
            if (vk != 0 && RegisterHotKey(Handle, id, mods, vk))
            {
                _actions[id] = callback;
                return id;
            }
            return -1;
        }

        public void Unregister(int id)
        {
            if (_actions.ContainsKey(id))
            {
                UnregisterHotKey(Handle, id);
                _actions.Remove(id);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                Action a;
                if (_actions.TryGetValue(m.WParam.ToInt32(), out a)) a();
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            foreach (var id in _actions.Keys.ToList()) UnregisterHotKey(Handle, id);
            _actions.Clear();
            try { DestroyHandle(); } catch { }
        }

        public static string Format(uint mods, uint vk)
        {
            if (vk == 0) return "(none)";
            var sb = new StringBuilder();
            if ((mods & MOD_CONTROL) != 0) sb.Append("Ctrl+");
            if ((mods & MOD_ALT) != 0) sb.Append("Alt+");
            if ((mods & MOD_SHIFT) != 0) sb.Append("Shift+");
            if ((mods & MOD_WIN) != 0) sb.Append("Win+");
            sb.Append(((Keys)vk).ToString());
            return sb.ToString();
        }
    }
}