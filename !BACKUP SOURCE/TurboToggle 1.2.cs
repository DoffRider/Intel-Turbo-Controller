using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TurboToggle
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            if (!IsAdmin()) { RelaunchAsAdmin(); return; }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayContext());
        }
        static bool IsAdmin()
        {
            using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                var p = new System.Security.Principal.WindowsPrincipal(id);
                return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
        }
        static void RelaunchAsAdmin()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch { }
        }
    }

    // ============================================================
    static class Turbo
    {
        const string GUID_PROC  = "54533251-82be-4824-96c1-47b60b740d00";
        const string GUID_BOOST = "be337238-0d82-4146-a960-4f3749d470c7";
        static bool _state = true;
        static bool _loaded = false;
        static readonly object _lock = new object();

        public static void Set(bool enabled)
        {
            lock (_lock)
            {
                string v = enabled ? "1" : "0";
                Run("/SETACVALUEINDEX SCHEME_CURRENT " + GUID_PROC + " " + GUID_BOOST + " " + v);
                Run("/SETDCVALUEINDEX SCHEME_CURRENT " + GUID_PROC + " " + GUID_BOOST + " " + v);
                Run("/SETACTIVE SCHEME_CURRENT");
                _state = enabled; _loaded = true;
            }
        }

        public static bool Get()
        {
            lock (_lock)
            {
                if (!_loaded) { _state = ReadPowerCfg(); _loaded = true; }
                return _state;
            }
        }

        public static bool ReloadFromSystem()
        {
            lock (_lock) { _state = ReadPowerCfg(); _loaded = true; return _state; }
        }

        static bool ReadPowerCfg()
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg",
                    "/query SCHEME_CURRENT " + GUID_PROC + " " + GUID_BOOST)
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using (var p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    foreach (var rawLine in output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (rawLine.IndexOf("Current AC", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            int idx = rawLine.LastIndexOf("0x", StringComparison.OrdinalIgnoreCase);
                            if (idx >= 0)
                            {
                                string tail = rawLine.Substring(idx + 2);
                                var sb = new StringBuilder();
                                foreach (char c in tail)
                                {
                                    if (Uri.IsHexDigit(c)) sb.Append(c);
                                    else if (sb.Length > 0) break;
                                }
                                uint v;
                                if (uint.TryParse(sb.ToString(), System.Globalization.NumberStyles.HexNumber, null, out v))
                                    return v != 0;
                            }
                        }
                    }
                }
            }
            catch { }
            return _state;
        }

        static void Run(string args)
        {
            try
            {
                using (var p = Process.Start(new ProcessStartInfo("powercfg", args)
                { UseShellExecute = false, CreateNoWindow = true }))
                { p.WaitForExit(5000); }
            }
            catch { }
        }
    }

    // ============================================================
    class Config
    {
        public bool AutoSwitchEnabled = false;
        public int PollIntervalMs = 2000;
        public List<string> Triggers = new List<string>();

        public bool ShowCpuFreq = false;
        public bool ShowCpuTemp = true;
        public bool ShowCpuLoad = false;
        public bool ShowGpuFreq = false;
        public bool ShowGpuTemp = true;
        public bool ShowGpuLoad = false;

        public int OverlayBg = unchecked((int)0xFF101010);
        public int OverlayFg = unchecked((int)0xFF00FF88);
        public bool OverlayEnabled = true;
        public int OverlayX = -1;
        public int OverlayY = -1;

        public uint HotkeyMods = 0;
        public uint HotkeyVk = 0;

        public bool TurboOnExit = true;
        public bool Notify = true;

        static string PathFile
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TurboToggle");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "config.ini");
            }
        }

        public static Config Load()
        {
            var c = new Config();
            try
            {
                if (!File.Exists(PathFile))
                {
                    c.HotkeyMods = HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_ALT;
                    c.HotkeyVk = 0x54; // T
                    return c;
                }
                var dict = new Dictionary<string, string>();
                foreach (var raw in File.ReadAllLines(PathFile))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    dict[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
                Func<string, string, string> G = (k, d) => dict.ContainsKey(k) ? dict[k] : d;
                Func<string, bool, bool> GB = (k, d) => { var v = G(k, null); return v == null ? d : v.Equals("true", StringComparison.OrdinalIgnoreCase); };
                Func<string, int, int> GI = (k, d) => { var v = G(k, null); int x; return (v != null && int.TryParse(v, out x)) ? x : d; };

                c.AutoSwitchEnabled = GB("AutoSwitchEnabled", false);
                c.PollIntervalMs = GI("PollIntervalMs", 2000);
                c.Triggers = G("Triggers", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).Distinct().ToList();

                c.ShowCpuFreq = GB("ShowCpuFreq", false);
                c.ShowCpuTemp = GB("ShowCpuTemp", true);
                c.ShowCpuLoad = GB("ShowCpuLoad", false);
                c.ShowGpuFreq = GB("ShowGpuFreq", false);
                c.ShowGpuTemp = GB("ShowGpuTemp", true);
                c.ShowGpuLoad = GB("ShowGpuLoad", false);

                c.OverlayBg = GI("OverlayBg", unchecked((int)0xFF101010));
                c.OverlayFg = GI("OverlayFg", unchecked((int)0xFF00FF88));
                c.OverlayEnabled = GB("OverlayEnabled", true);
                c.OverlayX = GI("OverlayX", -1);
                c.OverlayY = GI("OverlayY", -1);

                c.HotkeyMods = (uint)GI("HotkeyMods", (int)(HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_ALT));
                c.HotkeyVk = (uint)GI("HotkeyVk", 0x54);

                c.TurboOnExit = GB("TurboOnExit", true);
                c.Notify = GB("Notify", true);
            }
            catch { }
            return c;
        }

        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Turbo Toggle Config");
                sb.AppendLine("AutoSwitchEnabled=" + (AutoSwitchEnabled ? "true" : "false"));
                sb.AppendLine("PollIntervalMs=" + PollIntervalMs);
                sb.AppendLine("Triggers=" + string.Join(",", Triggers));
                sb.AppendLine("ShowCpuFreq=" + (ShowCpuFreq ? "true" : "false"));
                sb.AppendLine("ShowCpuTemp=" + (ShowCpuTemp ? "true" : "false"));
                sb.AppendLine("ShowCpuLoad=" + (ShowCpuLoad ? "true" : "false"));
                sb.AppendLine("ShowGpuFreq=" + (ShowGpuFreq ? "true" : "false"));
                sb.AppendLine("ShowGpuTemp=" + (ShowGpuTemp ? "true" : "false"));
                sb.AppendLine("ShowGpuLoad=" + (ShowGpuLoad ? "true" : "false"));
                sb.AppendLine("OverlayBg=" + OverlayBg);
                sb.AppendLine("OverlayFg=" + OverlayFg);
                sb.AppendLine("OverlayEnabled=" + (OverlayEnabled ? "true" : "false"));
                sb.AppendLine("OverlayX=" + OverlayX);
                sb.AppendLine("OverlayY=" + OverlayY);
                sb.AppendLine("HotkeyMods=" + HotkeyMods);
                sb.AppendLine("HotkeyVk=" + HotkeyVk);
                sb.AppendLine("TurboOnExit=" + (TurboOnExit ? "true" : "false"));
                sb.AppendLine("Notify=" + (Notify ? "true" : "false"));
                File.WriteAllText(PathFile, sb.ToString());
            }
            catch { }
        }
    }

    // ============================================================
    class SensorSample
    {
        public float? CpuFreqMHz, CpuTempC, CpuLoadPct;
        public float? GpuFreqMHz, GpuTempC, GpuLoadPct;
        public string GpuName;
    }

    static class SensorReader
    {
        static PerformanceCounter _cpuFreqPct, _cpuLoad;
        static float _cpuBaseMHz = 0;
        static bool _smiMissing = false;
        static bool _init = false;
        static readonly object _lock = new object();

        public static void Init()
        {
            lock (_lock)
            {
                if (_init) return;
                _init = true;
                try { _cpuFreqPct = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total"); _cpuFreqPct.NextValue(); }
                catch { _cpuFreqPct = null; }
                try { _cpuLoad = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _cpuLoad.NextValue(); }
                catch { _cpuLoad = null; }
                try
                {
                    var searcher = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor");
                    foreach (ManagementObject mo in searcher.Get())
                    { _cpuBaseMHz = Convert.ToSingle(mo["MaxClockSpeed"]); break; }
                }
                catch { }
            }
        }

        public static SensorSample Read()
        {
            Init();
            var s = new SensorSample();

            if (_cpuFreqPct != null && _cpuBaseMHz > 0)
            {
                try { s.CpuFreqMHz = _cpuBaseMHz * _cpuFreqPct.NextValue() / 100f; } catch { }
            }
            if (_cpuLoad != null)
            {
                try { s.CpuLoadPct = _cpuLoad.NextValue(); } catch { }
            }

            try
            {
                var searcher = new ManagementObjectSearcher(
                    @"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                foreach (ManagementObject mo in searcher.Get())
                {
                    double t = Convert.ToDouble(mo["CurrentTemperature"]);
                    s.CpuTempC = (float)(t / 10.0 - 273.15);
                    break;
                }
            }
            catch { }

            if (!_smiMissing)
            {
                try
                {
                    var psi = new ProcessStartInfo("nvidia-smi",
                        "--query-gpu=name,temperature.gpu,clocks.current.graphics,utilization.gpu --format=csv,noheader,nounits")
                    { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                    using (var p = Process.Start(psi))
                    {
                        string line = p.StandardOutput.ReadLine();
                        p.WaitForExit(2000);
                        if (!string.IsNullOrEmpty(line))
                        {
                            var parts = line.Split(',').Select(x => x.Trim()).ToArray();
                            if (parts.Length >= 4)
                            {
                                s.GpuName = parts[0];
                                float v;
                                if (float.TryParse(parts[1], out v)) s.GpuTempC = v;
                                if (float.TryParse(parts[2], out v)) s.GpuFreqMHz = v;
                                if (float.TryParse(parts[3], out v)) s.GpuLoadPct = v;
                            }
                        }
                    }
                }
                catch { _smiMissing = true; }
            }

            return s;
        }
    }

    // ============================================================
    class WindowInfo
    {
        public IntPtr Handle;
        public string Title;
        public string ProcessName;
        public override string ToString()
        {
            return ProcessName + ".exe   —   " + Title;
        }
    }

    static class WindowEnumerator
    {
        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        public static List<WindowInfo> GetVisibleWindows()
        {
            var list = new List<WindowInfo>();
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                int len = GetWindowTextLength(hWnd);
                if (len == 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();
                if (string.IsNullOrWhiteSpace(title)) return true;

                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                string procName = "";
                try { using (var p = Process.GetProcessById((int)pid)) procName = p.ProcessName; }
                catch { }
                if (string.IsNullOrEmpty(procName)) return true;

                list.Add(new WindowInfo { Handle = hWnd, Title = title, ProcessName = procName });
                return true;
            }, IntPtr.Zero);
            return list;
        }
    }

    // ============================================================
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

    // ============================================================
    class HotkeyCaptureForm : Form
    {
        public uint Modifiers;
        public uint Vk;
        public bool Captured;
        Label _lbl;

        public HotkeyCaptureForm()
        {
            Text = "Set Hotkey";
            Width = 380; Height = 170;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            KeyPreview = true;

            _lbl = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Tekan kombinasi hotkey...\n(contoh: Ctrl+Alt+T)",
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            Controls.Add(_lbl);

            KeyDown += (s, e) =>
            {
                e.SuppressKeyPress = true;
                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey ||
                    e.KeyCode == Keys.Menu || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
                    return;
                uint mods = 0;
                if (e.Control) mods |= HotkeyManager.MOD_CONTROL;
                if (e.Alt) mods |= HotkeyManager.MOD_ALT;
                if (e.Shift) mods |= HotkeyManager.MOD_SHIFT;
                if (mods == 0) { _lbl.Text = "Minimal 1 modifier (Ctrl/Alt/Shift)"; return; }
                Modifiers = mods;
                Vk = (uint)e.KeyCode;
                Captured = true;
                DialogResult = DialogResult.OK;
                Close();
            };
        }
    }

    // ============================================================
    class TaskbarOverlay : Form
    {
        Label _lbl;
        bool _drag = false;
        Point _dragOffset;

        public TaskbarOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(280, 26);
            MinimumSize = new Size(80, 22);

            _lbl = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Consolas", 9.5f, FontStyle.Bold),
                Text = "CPU --°C  GPU --°C"
            };
            Controls.Add(_lbl);

            MouseEventHandler down = (s, e) => { if (e.Button == MouseButtons.Left) { _drag = true; _dragOffset = e.Location; } };
            MouseEventHandler move = (s, e) => { if (_drag) Location = new Point(Location.X + e.X - _dragOffset.X, Location.Y + e.Y - _dragOffset.Y); };
            MouseEventHandler up = (s, e) => { _drag = false; };
            MouseDown += down; MouseMove += move; MouseUp += up;
            _lbl.MouseDown += down; _lbl.MouseMove += move; _lbl.MouseUp += up;
        }

        public void SetText(string t) { _lbl.Text = t; AutoSizeToText(); }
        public void ApplyColors(Color bg, Color fg) { BackColor = bg; _lbl.ForeColor = fg; _lbl.BackColor = bg; }

        void AutoSizeToText()
        {
            using (var g = _lbl.CreateGraphics())
            {
                var sz = g.MeasureString(_lbl.Text, _lbl.Font);
                int w = (int)Math.Ceiling(sz.Width) + 18;
                int h = (int)Math.Ceiling(sz.Height) + 6;
                if (w < 80) w = 80;
                if (w > 800) w = 800;
                Size = new Size(w, h);
            }
        }

        public void PositionBottomRight()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 8, wa.Bottom - Height - 4);
        }
    }

    // ============================================================
    class TrayContext : ApplicationContext
    {
        readonly NotifyIcon _icon;
        readonly Config _cfg;
        readonly System.Windows.Forms.Timer _autoTimer, _sensorTimer;
        readonly MainForm _form;
        readonly TaskbarOverlay _overlay;
        readonly HotkeyManager _hotkey;
        int _hotkeyId = -1;
        static Icon _iconOn, _iconOff;

        public TrayContext()
        {
            _cfg = Config.Load();

            _overlay = new TaskbarOverlay();
            _overlay.ApplyColors(Color.FromArgb(_cfg.OverlayBg), Color.FromArgb(_cfg.OverlayFg));
            if (_cfg.OverlayX >= 0 && _cfg.OverlayY >= 0)
                _overlay.Location = new Point(_cfg.OverlayX, _cfg.OverlayY);
            else
                _overlay.PositionBottomRight();
            _overlay.LocationChanged += (s, e) =>
            {
                _cfg.OverlayX = _overlay.Location.X;
                _cfg.OverlayY = _overlay.Location.Y;
                _cfg.Save();
            };
            if (_cfg.OverlayEnabled) _overlay.Show();

            _hotkey = new HotkeyManager();
            RegisterHotkeyFromConfig();

            _form = new MainForm(_cfg, _overlay, this);
            _form.OnHotkeyChanged += RegisterHotkeyFromConfig;
            _form.OnExitRequested += ExitApp;

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open Control Panel", null, (s, e) => ShowForm());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Turbo ON", null, (s, e) => SetTurbo(true, "tray"));
            menu.Items.Add("Turbo OFF", null, (s, e) => SetTurbo(false, "tray"));
            menu.Items.Add("Toggle Turbo", null, (s, e) => SetTurbo(!Turbo.Get(), "tray toggle"));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Reload from System", null, (s, e) => { Turbo.ReloadFromSystem(); _form.RefreshStatus(); UpdateIcon(); });
            menu.Items.Add("Exit", null, (s, e) => ExitApp());

            _icon = new NotifyIcon
            {
                Visible = true,
                ContextMenuStrip = menu,
                Icon = GetIcon(Turbo.Get())
            };
            _icon.Text = "Turbo Boost: " + (Turbo.Get() ? "ON" : "OFF");
            _icon.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowForm(); };

            _autoTimer = new System.Windows.Forms.Timer { Interval = _cfg.PollIntervalMs };
            _autoTimer.Tick += (s, e) => AutoTick();
            _autoTimer.Start();

            _sensorTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _sensorTimer.Tick += (s, e) => SensorTick();
            _sensorTimer.Start();

            SensorTick();
            UpdateIcon();
        }

        void RegisterHotkeyFromConfig()
        {
            if (_hotkeyId != -1) { _hotkey.Unregister(_hotkeyId); _hotkeyId = -1; }
            if (_cfg.HotkeyVk != 0)
            {
                _hotkeyId = _hotkey.Register(_cfg.HotkeyMods, _cfg.HotkeyVk, () =>
                {
                    SetTurbo(!Turbo.Get(), "hotkey");
                });
            }
        }

        public void ShowForm()
        {
            _form.RefreshStatus();
            if (!_form.Visible) _form.Show();
            if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
            _form.Activate();
            _form.BringToFront();
        }

        public void SetTurbo(bool on, string source)
        {
            bool changed = Turbo.Get() != on;
            Turbo.Set(on);
            UpdateIcon();
            _form.RefreshStatus();
            if (changed && _cfg.Notify)
            {
                _icon.ShowBalloonTip(1500, "Turbo Boost",
                    (on ? "Turbo: ON" : "Turbo: OFF") + "   [" + source + "]",
                    ToolTipIcon.Info);
            }
        }

        void AutoTick()
        {
            if (!_cfg.AutoSwitchEnabled) return;
            bool desired = _cfg.Triggers.Count > 0 && IsAnyTriggerRunning();
            bool current = Turbo.Get();
            if (desired != current) SetTurbo(desired, "auto-switch");
        }

        bool IsAnyTriggerRunning()
        {
            try
            {
                var set = new HashSet<string>(_cfg.Triggers, StringComparer.OrdinalIgnoreCase);
                foreach (var p in Process.GetProcesses())
                {
                    try { if (set.Contains(p.ProcessName)) return true; }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }
            return false;
        }

        void SensorTick()
        {
            if (!_cfg.OverlayEnabled || !_overlay.Visible) return;
            var snap = _cfg;
            Task.Run(() =>
            {
                try
                {
                    var s = SensorReader.Read();
                    var parts = new List<string>();
                    if (snap.ShowCpuTemp) parts.Add("CPU " + (s.CpuTempC.HasValue ? s.CpuTempC.Value.ToString("0") + "°C" : "--"));
                    if (snap.ShowCpuFreq) parts.Add((s.CpuFreqMHz.HasValue ? (s.CpuFreqMHz.Value / 1000f).ToString("0.00") + "GHz" : "----"));
                    if (snap.ShowCpuLoad) parts.Add("Load " + (s.CpuLoadPct.HasValue ? s.CpuLoadPct.Value.ToString("0") + "%" : "--"));
                    if (snap.ShowGpuTemp) parts.Add("GPU " + (s.GpuTempC.HasValue ? s.GpuTempC.Value.ToString("0") + "°C" : "--"));
                    if (snap.ShowGpuFreq) parts.Add((s.GpuFreqMHz.HasValue ? (s.GpuFreqMHz.Value / 1000f).ToString("0.00") + "GHz" : "----"));
                    if (snap.ShowGpuLoad) parts.Add("GPU Load " + (s.GpuLoadPct.HasValue ? s.GpuLoadPct.Value.ToString("0") + "%" : "--"));
                    string text = parts.Count > 0 ? string.Join(" | ", parts) : "CPU --°C";
                    try
                    {
                        if (_overlay.IsHandleCreated && !_overlay.IsDisposed)
                            _overlay.BeginInvoke(new Action(() => _overlay.SetText(text)));
                    }
                    catch { }
                }
                catch { }
            });
        }

        void UpdateIcon()
        {
            bool on = Turbo.Get();
            _icon.Icon = GetIcon(on);
            _icon.Text = "Turbo Boost: " + (on ? "ON" : "OFF");
        }

        static Icon GetIcon(bool on)
        {
            if (on) return _iconOn ?? (_iconOn = CreateIcon(true));
            return _iconOff ?? (_iconOff = CreateIcon(false));
        }

        static Icon CreateIcon(bool on)
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(on ? Color.FromArgb(46, 204, 113) : Color.FromArgb(231, 76, 60)))
                    g.FillEllipse(b, 2, 2, 28, 28);
                using (var f = new Font("Arial", 15, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var tb = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("T", f, tb, new RectangleF(0, 0, 32, 32), sf);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        public void ExitApp()
        {
            try { _cfg.Save(); } catch { }
            if (_cfg.TurboOnExit) Turbo.Set(true);

            _autoTimer.Stop();
            _sensorTimer.Stop();
            try { _hotkey.Dispose(); } catch { }
            _icon.Visible = false;
            _icon.Dispose();
            try { _overlay.Close(); _overlay.Dispose(); } catch { }
            Application.Exit();
        }
    }

    // ============================================================
    class MainForm : Form
    {
        public event Action OnHotkeyChanged;
        public event Action OnExitRequested;

        readonly Config _cfg;
        readonly TaskbarOverlay _overlay;
        readonly TrayContext _ctx;

        Label _statusLabel;
        Panel _statusDot;
        Button _btnOn, _btnOff;
        CheckBox _chkNotify, _chkTurboOnExit;
        CheckBox _chkAuto;
        NumericUpDown _numInterval;
        ListBox _lstTriggers;
        CheckBox _chkOverlayEnabled;
        CheckBox _chkCpuFreq, _chkCpuTemp, _chkCpuLoad, _chkGpuFreq, _chkGpuTemp, _chkGpuLoad;
        Panel _bgPreview, _fgPreview;
        TextBox _txtHotkey;

        public MainForm(Config cfg, TaskbarOverlay overlay, TrayContext ctx)
        {
            _cfg = cfg;
            _overlay = overlay;
            _ctx = ctx;

            Text = "Turbo Boost Control";
            Width = 520; Height = 620;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; MinimizeBox = true;
            Icon = CreateFormIcon();

            _statusDot = new Panel { Left = 20, Top = 22, Width = 22, Height = 22 };
            _statusDot.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush((Color)(_statusDot.Tag ?? Color.Gray)))
                    g.FillEllipse(b, 0, 0, _statusDot.Width - 1, _statusDot.Height - 1);
            };
            Controls.Add(_statusDot);

            _statusLabel = new Label
            {
                Left = 52, Top = 24, Width = 430, Height = 24,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Text = "Status: -"
            };
            Controls.Add(_statusLabel);

            _btnOn = new Button
            {
                Text = "TURBO ON", Left = 20, Top = 60, Width = 230, Height = 56,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(46, 204, 113), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnOn.FlatAppearance.BorderSize = 0;
            _btnOn.Click += (s, e) => _ctx.SetTurbo(true, "manual");
            Controls.Add(_btnOn);

            _btnOff = new Button
            {
                Text = "TURBO OFF", Left = 258, Top = 60, Width = 230, Height = 56,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(231, 76, 60), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnOff.FlatAppearance.BorderSize = 0;
            _btnOff.Click += (s, e) => _ctx.SetTurbo(false, "manual");
            Controls.Add(_btnOff);

            var tabs = new TabControl { Left = 20, Top = 130, Width = 468, Height = 400 };
            Controls.Add(tabs);

            // ---------- Tab Auto-Switch ----------
            var tpAuto = new TabPage("Auto-Switch");
            tabs.TabPages.Add(tpAuto);
            _chkAuto = new CheckBox
            {
                Text = "Aktifkan Auto-Switch",
                Left = 12, Top = 12, Width = 400, Height = 24,
                Checked = _cfg.AutoSwitchEnabled
            };
            _chkAuto.CheckedChanged += (s, e) => { _cfg.AutoSwitchEnabled = _chkAuto.Checked; _cfg.Save(); };
            tpAuto.Controls.Add(_chkAuto);

            tpAuto.Controls.Add(new Label { Text = "Cek tiap (ms):", Left = 12, Top = 44, Width = 90, Height = 22 });
            _numInterval = new NumericUpDown
            {
                Left = 106, Top = 42, Width = 90,
                Minimum = 500, Maximum = 60000, Increment = 500,
                Value = _cfg.PollIntervalMs
            };
            _numInterval.ValueChanged += (s, e) => { _cfg.PollIntervalMs = (int)_numInterval.Value; _cfg.Save(); };
            tpAuto.Controls.Add(_numInterval);

            tpAuto.Controls.Add(new Label { Text = "Aplikasi trigger (satu per baris di list):", Left = 12, Top = 78, Width = 400, Height = 20 });
            _lstTriggers = new ListBox
            {
                Left = 12, Top = 100, Width = 430, Height = 175,
                Font = new Font("Consolas", 9.5f)
            };
            foreach (var t in _cfg.Triggers) _lstTriggers.Items.Add(t);
            tpAuto.Controls.Add(_lstTriggers);

            var btnAddExe = new Button { Text = "Browse .exe...", Left = 12, Top = 285, Width = 135, Height = 28 };
            btnAddExe.Click += (s, e) => AddFromExe();
            tpAuto.Controls.Add(btnAddExe);

            var btnAddWin = new Button { Text = "Capture Window...", Left = 155, Top = 285, Width = 155, Height = 28 };
            btnAddWin.Click += (s, e) => AddFromWindow();
            tpAuto.Controls.Add(btnAddWin);

            var btnRemove = new Button { Text = "Remove", Left = 318, Top = 285, Width = 124, Height = 28 };
            btnRemove.Click += (s, e) =>
            {
                if (_lstTriggers.SelectedIndex >= 0)
                {
                    _cfg.Triggers.RemoveAt(_lstTriggers.SelectedIndex);
                    _lstTriggers.Items.RemoveAt(_lstTriggers.SelectedIndex);
                    _cfg.Save();
                }
            };
            tpAuto.Controls.Add(btnRemove);

            tpAuto.Controls.Add(new Label
            {
                Text = "Tips: Jalankan game dulu, klik Capture Window, pilih dari list.",
                Left = 12, Top = 320, Width = 430, Height = 20, ForeColor = Color.Gray
            });

            // ---------- Tab Overlay ----------
            var tpOver = new TabPage("Taskbar Overlay");
            tabs.TabPages.Add(tpOver);
            _chkOverlayEnabled = new CheckBox
            {
                Text = "Tampilkan overlay",
                Left = 12, Top = 12, Width = 400, Height = 24,
                Checked = _cfg.OverlayEnabled
            };
            _chkOverlayEnabled.CheckedChanged += (s, e) =>
            {
                _cfg.OverlayEnabled = _chkOverlayEnabled.Checked;
                _cfg.Save();
                if (_chkOverlayEnabled.Checked) _overlay.Show(); else _overlay.Hide();
            };
            tpOver.Controls.Add(_chkOverlayEnabled);

            tpOver.Controls.Add(new Label { Text = "Item yang ditampilkan:", Left = 12, Top = 44, Width = 200, Height = 20 });
            _chkCpuTemp = MakeCheck(tpOver, "CPU Temp", 68, _cfg.ShowCpuTemp, v => { _cfg.ShowCpuTemp = v; _cfg.Save(); });
            _chkCpuFreq = MakeCheck(tpOver, "CPU Freq", 90, _cfg.ShowCpuFreq, v => { _cfg.ShowCpuFreq = v; _cfg.Save(); });
            _chkCpuLoad = MakeCheck(tpOver, "CPU Load", 112, _cfg.ShowCpuLoad, v => { _cfg.ShowCpuLoad = v; _cfg.Save(); });
            _chkGpuTemp = MakeCheck(tpOver, "GPU Temp", 140, _cfg.ShowGpuTemp, v => { _cfg.ShowGpuTemp = v; _cfg.Save(); });
            _chkGpuFreq = MakeCheck(tpOver, "GPU Freq", 162, _cfg.ShowGpuFreq, v => { _cfg.ShowGpuFreq = v; _cfg.Save(); });
            _chkGpuLoad = MakeCheck(tpOver, "GPU Load", 184, _cfg.ShowGpuLoad, v => { _cfg.ShowGpuLoad = v; _cfg.Save(); });

            tpOver.Controls.Add(new Label { Text = "Warna Background:", Left = 12, Top = 220, Width = 130, Height = 20 });
            _bgPreview = new Panel { Left = 150, Top = 218, Width = 30, Height = 22, BackColor = Color.FromArgb(_cfg.OverlayBg), BorderStyle = BorderStyle.FixedSingle };
            tpOver.Controls.Add(_bgPreview);
            var btnBg = new Button { Text = "Pilih...", Left = 190, Top = 216, Width = 80, Height = 26 };
            btnBg.Click += (s, e) =>
            {
                using (var dlg = new ColorDialog { Color = Color.FromArgb(_cfg.OverlayBg) })
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        _cfg.OverlayBg = dlg.Color.ToArgb();
                        _bgPreview.BackColor = dlg.Color;
                        _overlay.ApplyColors(Color.FromArgb(_cfg.OverlayBg), Color.FromArgb(_cfg.OverlayFg));
                        _cfg.Save();
                    }
            };
            tpOver.Controls.Add(btnBg);

            tpOver.Controls.Add(new Label { Text = "Warna Teks:", Left = 12, Top = 250, Width = 130, Height = 20 });
            _fgPreview = new Panel { Left = 150, Top = 248, Width = 30, Height = 22, BackColor = Color.FromArgb(_cfg.OverlayFg), BorderStyle = BorderStyle.FixedSingle };
            tpOver.Controls.Add(_fgPreview);
            var btnFg = new Button { Text = "Pilih...", Left = 190, Top = 246, Width = 80, Height = 26 };
            btnFg.Click += (s, e) =>
            {
                using (var dlg = new ColorDialog { Color = Color.FromArgb(_cfg.OverlayFg) })
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        _cfg.OverlayFg = dlg.Color.ToArgb();
                        _fgPreview.BackColor = dlg.Color;
                        _overlay.ApplyColors(Color.FromArgb(_cfg.OverlayBg), Color.FromArgb(_cfg.OverlayFg));
                        _cfg.Save();
                    }
            };
            tpOver.Controls.Add(btnFg);

            var btnReset = new Button { Text = "Reset Posisi ke Pojok Kanan Bawah", Left = 12, Top = 290, Width = 260, Height = 28 };
            btnReset.Click += (s, e) =>
            {
                _overlay.PositionBottomRight();
                _cfg.OverlayX = _overlay.Location.X;
                _cfg.OverlayY = _overlay.Location.Y;
                _cfg.Save();
            };
            tpOver.Controls.Add(btnReset);

            tpOver.Controls.Add(new Label
            {
                Text = "Tips: drag overlay pakai mouse untuk pindah posisi.",
                Left = 12, Top = 328, Width = 430, Height = 40, ForeColor = Color.Gray
            });

            // ---------- Tab Hotkey ----------
            var tpKey = new TabPage("Hotkey");
            tabs.TabPages.Add(tpKey);
            tpKey.Controls.Add(new Label
            {
                Text = "Hotkey global untuk toggle Turbo (aktif di mana saja):",
                Left = 12, Top = 12, Width = 430, Height = 20
            });
            _txtHotkey = new TextBox
            {
                Left = 12, Top = 40, Width = 240, Height = 26,
                ReadOnly = true, Font = new Font("Consolas", 11),
                Text = HotkeyManager.Format(_cfg.HotkeyMods, _cfg.HotkeyVk)
            };
            tpKey.Controls.Add(_txtHotkey);

            var btnSet = new Button { Text = "Set...", Left = 260, Top = 38, Width = 80, Height = 28 };
            btnSet.Click += (s, e) =>
            {
                using (var dlg = new HotkeyCaptureForm())
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Captured)
                    {
                        _cfg.HotkeyMods = dlg.Modifiers;
                        _cfg.HotkeyVk = dlg.Vk;
                        _cfg.Save();
                        _txtHotkey.Text = HotkeyManager.Format(_cfg.HotkeyMods, _cfg.HotkeyVk);
                        if (OnHotkeyChanged != null) OnHotkeyChanged();
                    }
                }
            };
            tpKey.Controls.Add(btnSet);

            var btnClear = new Button { Text = "Clear", Left = 348, Top = 38, Width = 80, Height = 28 };
            btnClear.Click += (s, e) =>
            {
                _cfg.HotkeyMods = 0; _cfg.HotkeyVk = 0; _cfg.Save();
                _txtHotkey.Text = "(none)";
                if (OnHotkeyChanged != null) OnHotkeyChanged();
            };
            tpKey.Controls.Add(btnClear);

            tpKey.Controls.Add(new Label
            {
                Text = "Syarat: minimal 1 modifier (Ctrl/Alt/Shift). Contoh: Ctrl+Alt+T",
                Left = 12, Top = 80, Width = 430, Height = 20, ForeColor = Color.Gray
            });

            // ---------- Tab Behavior ----------
            var tpBeh = new TabPage("Behavior");
            tabs.TabPages.Add(tpBeh);

            _chkNotify = new CheckBox
            {
                Text = "Tampilkan notifikasi Windows saat Turbo ON/OFF",
                Left = 12, Top = 12, Width = 400, Height = 24,
                Checked = _cfg.Notify
            };
            _chkNotify.CheckedChanged += (s, e) => { _cfg.Notify = _chkNotify.Checked; _cfg.Save(); };
            tpBeh.Controls.Add(_chkNotify);

            _chkTurboOnExit = new CheckBox
            {
                Text = "Set Turbo = ON saat aplikasi ditutup (default ON)",
                Left = 12, Top = 42, Width = 400, Height = 24,
                Checked = _cfg.TurboOnExit
            };
            _chkTurboOnExit.CheckedChanged += (s, e) => { _cfg.TurboOnExit = _chkTurboOnExit.Checked; _cfg.Save(); };
            tpBeh.Controls.Add(_chkTurboOnExit);

            // ---------- Bottom Buttons ----------
            var btnHide = new Button { Text = "Hide to Tray", Left = 20, Top = 542, Width = 130, Height = 30 };
            btnHide.Click += (s, e) => Hide();
            Controls.Add(btnHide);

            var btnExit = new Button { Text = "Exit App", Left = 358, Top = 542, Width = 130, Height = 30 };
            btnExit.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Keluar dari Turbo Toggle?", "Konfirmasi",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    if (OnExitRequested != null) OnExitRequested();
                }
            };
            Controls.Add(btnExit);

            FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };

            RefreshStatus();
        }

        CheckBox MakeCheck(Control parent, string text, int top, bool value, Action<bool> onChange)
        {
            var cb = new CheckBox
            {
                Text = text, Left = 20, Top = top, Width = 200, Height = 22,
                Checked = value
            };
            cb.CheckedChanged += (s, e) => onChange(cb.Checked);
            parent.Controls.Add(cb);
            return cb;
        }

        public void RefreshStatus()
        {
            bool on = Turbo.Get();
            _statusDot.Tag = on ? Color.FromArgb(46, 204, 113) : Color.FromArgb(231, 76, 60);
            _statusDot.Invalidate();
            _statusLabel.Text = "Turbo Boost: " + (on ? "ON" : "OFF");
            _statusLabel.ForeColor = on ? Color.FromArgb(39, 174, 96) : Color.FromArgb(192, 57, 43);
            _btnOn.Enabled = !on;
            _btnOff.Enabled = on;
        }

        void AddFromExe()
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "Executable (*.exe)|*.exe|All files|*.*",
                Title = "Pilih file .exe aplikasi/game"
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string name = Path.GetFileNameWithoutExtension(dlg.FileName).ToLowerInvariant();
                    if (!_cfg.Triggers.Contains(name))
                    {
                        _cfg.Triggers.Add(name);
                        _lstTriggers.Items.Add(name);
                        _cfg.Save();
                    }
                    else
                    {
                        MessageBox.Show(this, "Sudah ada di list.", "Info",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        void AddFromWindow()
        {
            using (var dlg = new WindowPickerForm())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Selected != null)
                {
                    string name = dlg.Selected.ProcessName.ToLowerInvariant();
                    if (!_cfg.Triggers.Contains(name))
                    {
                        _cfg.Triggers.Add(name);
                        _lstTriggers.Items.Add(name);
                        _cfg.Save();
                    }
                    else
                    {
                        MessageBox.Show(this, "Sudah ada di list.", "Info",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        static Icon CreateFormIcon()
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(Color.FromArgb(52, 152, 219)))
                    g.FillEllipse(b, 2, 2, 28, 28);
                using (var f = new Font("Arial", 15, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var tb = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString("T", f, tb, new RectangleF(0, 0, 32, 32), sf);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    // ============================================================
    class WindowPickerForm : Form
    {
        public WindowInfo Selected;
        ListBox _list;
        System.Windows.Forms.Timer _refreshTimer;
        bool _autoRefresh = true;

        public WindowPickerForm()
        {
            Text = "Pilih Window / Game yang Berjalan";
            Width = 560; Height = 420;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;

            Controls.Add(new Label
            {
                Text = "Pilih dari daftar window yang sedang terbuka (jalankan game dulu):",
                Left = 12, Top = 12, Width = 520, Height = 20
            });

            _list = new ListBox
            {
                Left = 12, Top = 38, Width = 520, Height = 300,
                Font = new Font("Consolas", 9.5f)
            };
            _list.DoubleClick += (s, e) => Accept();
            Controls.Add(_list);

            var btnRefresh = new Button { Text = "Refresh", Left = 12, Top = 346, Width = 90, Height = 28 };
            btnRefresh.Click += (s, e) => ReloadList();
            Controls.Add(btnRefresh);

            var chkAuto = new CheckBox
            {
                Text = "Auto-refresh tiap 2 detik",
                Left = 116, Top = 350, Width = 200, Height = 22,
                Checked = true
            };
            chkAuto.CheckedChanged += (s, e) => _autoRefresh = chkAuto.Checked;
            Controls.Add(chkAuto);

            var btnOk = new Button
            {
                Text = "OK", Left = 340, Top = 346, Width = 90, Height = 28,
                DialogResult = DialogResult.OK
            };
            btnOk.Click += (s, e) => Accept();
            Controls.Add(btnOk);

            var btnCancel = new Button
            {
                Text = "Cancel", Left = 440, Top = 346, Width = 90, Height = 28,
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _refreshTimer.Tick += (s, e) => { if (_autoRefresh) ReloadList(); };
            _refreshTimer.Start();

            ReloadList();
        }

        void Accept()
        {
            if (_list.SelectedItem is WindowInfo)
            {
                Selected = (WindowInfo)_list.SelectedItem;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(this, "Belum ada window yang dipilih.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        void ReloadList()
        {
            try
            {
                var sel = _list.SelectedItem as WindowInfo;
                string selKey = sel != null ? (sel.Title + "|" + sel.ProcessName) : null;

                var windows = WindowEnumerator.GetVisibleWindows()
                    .OrderBy(w => w.ProcessName).ThenBy(w => w.Title).ToList();

                _list.BeginUpdate();
                _list.Items.Clear();
                foreach (var w in windows) _list.Items.Add(w);
                _list.EndUpdate();

                if (selKey != null)
                {
                    for (int i = 0; i < _list.Items.Count; i++)
                    {
                        var w = (WindowInfo)_list.Items[i];
                        if ((w.Title + "|" + w.ProcessName) == selKey) { _list.SelectedIndex = i; break; }
                    }
                }
            }
            catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { _refreshTimer.Stop(); _refreshTimer.Dispose(); } catch { }
            base.OnFormClosed(e);
        }
    }
}