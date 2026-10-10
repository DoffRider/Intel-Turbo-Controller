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
		
		[DllImport("user32.dll")]
		static extern bool SetProcessDPIAware();
		
        [STAThread]
        static void Main(string[] args)
        {
			try { SetProcessDPIAware(); } catch { }     // <-- tambahkan baris ini

            if (!IsAdmin()) { RelaunchAsAdmin(args); return; }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayContext(args));
        }
		

        static bool IsAdmin()
        {
            using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                var p = new System.Security.Principal.WindowsPrincipal(id);
                return p.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
        }

        static void RelaunchAsAdmin(string[] args)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = args.Length > 0 ? string.Join(" ", args) : ""
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
		
		public int OverlayOpacity = 85;                            // %
		public int OverlayCpuColor = unchecked((int)0xFF00FF88);   // hijau
		public int OverlayGpuColor = unchecked((int)0xFF00BFFF);   // biru

		public bool AutoStartEnabled = false;
		public bool ShowFormOnStartup = false;
		
		public bool TrayInfoEnabled = false;
		public bool TrayShowCpuTemp = false;
		public bool TrayShowGpuTemp = true;
		public bool TrayShowCpuFreq = false;
		public bool TrayShowGpuFreq = false;
		public bool TrayShowCpuLoad = false;
		public bool TrayShowGpuLoad = false;
		public bool TrayBgUseTurboState = true;
		public int TrayCustomBg = unchecked((int)0xFF1E1E1E);
		public int TrayTextColor = unchecked((int)0xFFFFFFFF);
		
		public int TrayCpuColor = unchecked((int)0xFF00FF88);  // hijau terang
		public int TrayGpuColor = unchecked((int)0xFF00BFFF);  // biru terang
		
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
				c.OverlayOpacity = GI("OverlayOpacity", 85);
				c.OverlayCpuColor = GI("OverlayCpuColor", unchecked((int)0xFF00FF88));
				c.OverlayGpuColor = GI("OverlayGpuColor", unchecked((int)0xFF00BFFF));
                c.OverlayEnabled = GB("OverlayEnabled", true);
                c.OverlayX = GI("OverlayX", -1);
                c.OverlayY = GI("OverlayY", -1);

                c.HotkeyMods = (uint)GI("HotkeyMods", (int)(HotkeyManager.MOD_CONTROL | HotkeyManager.MOD_ALT));
                c.HotkeyVk = (uint)GI("HotkeyVk", 0x54);

                c.TurboOnExit = GB("TurboOnExit", true);
				c.AutoStartEnabled = GB("AutoStartEnabled", false);
				c.ShowFormOnStartup = GB("ShowFormOnStartup", false);
				
				c.TrayInfoEnabled = GB("TrayInfoEnabled", false);
				c.TrayShowCpuTemp = GB("TrayShowCpuTemp", false);
				c.TrayShowGpuTemp = GB("TrayShowGpuTemp", true);
				c.TrayShowCpuFreq = GB("TrayShowCpuFreq", false);
				c.TrayShowGpuFreq = GB("TrayShowGpuFreq", false);
				c.TrayShowCpuLoad = GB("TrayShowCpuLoad", false);
				c.TrayShowGpuLoad = GB("TrayShowGpuLoad", false);
				c.TrayBgUseTurboState = GB("TrayBgUseTurboState", true);
				c.TrayCustomBg = GI("TrayCustomBg", unchecked((int)0xFF1E1E1E));
				c.TrayTextColor = GI("TrayTextColor", unchecked((int)0xFFFFFFFF));
				
				c.TrayCpuColor = GI("TrayCpuColor", unchecked((int)0xFF00FF88));
				c.TrayGpuColor = GI("TrayGpuColor", unchecked((int)0xFF00BFFF));

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
				sb.AppendLine("OverlayOpacity=" + OverlayOpacity);
				sb.AppendLine("OverlayCpuColor=" + OverlayCpuColor);
				sb.AppendLine("OverlayGpuColor=" + OverlayGpuColor);
                sb.AppendLine("OverlayEnabled=" + (OverlayEnabled ? "true" : "false"));
                sb.AppendLine("OverlayX=" + OverlayX);
                sb.AppendLine("OverlayY=" + OverlayY);
                sb.AppendLine("HotkeyMods=" + HotkeyMods);
                sb.AppendLine("HotkeyVk=" + HotkeyVk);
                sb.AppendLine("TurboOnExit=" + (TurboOnExit ? "true" : "false"));
                sb.AppendLine("Notify=" + (Notify ? "true" : "false"));
				sb.AppendLine("AutoStartEnabled=" + (AutoStartEnabled ? "true" : "false"));
				sb.AppendLine("ShowFormOnStartup=" + (ShowFormOnStartup ? "true" : "false"));
				sb.AppendLine("TrayInfoEnabled=" + (TrayInfoEnabled ? "true" : "false"));
				sb.AppendLine("TrayShowCpuTemp=" + (TrayShowCpuTemp ? "true" : "false"));
				sb.AppendLine("TrayShowGpuTemp=" + (TrayShowGpuTemp ? "true" : "false"));
				sb.AppendLine("TrayShowCpuFreq=" + (TrayShowCpuFreq ? "true" : "false"));
				sb.AppendLine("TrayShowGpuFreq=" + (TrayShowGpuFreq ? "true" : "false"));
				sb.AppendLine("TrayShowCpuLoad=" + (TrayShowCpuLoad ? "true" : "false"));
				sb.AppendLine("TrayShowGpuLoad=" + (TrayShowGpuLoad ? "true" : "false"));
				sb.AppendLine("TrayBgUseTurboState=" + (TrayBgUseTurboState ? "true" : "false"));
				sb.AppendLine("TrayCustomBg=" + TrayCustomBg);
				sb.AppendLine("TrayTextColor=" + TrayTextColor);
				sb.AppendLine("TrayCpuColor=" + TrayCpuColor);
				sb.AppendLine("TrayGpuColor=" + TrayGpuColor);
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
        static bool _init = false;
        static readonly object _lock = new object();

        static LibreHardwareMonitor.Hardware.Computer _lhm;
        static bool _lhmInit = false;
		
		public static string LastLhmError = null;

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

        static void InitLHM()
		{
			if (_lhmInit) return;
			_lhmInit = true;
			try
			{
				_lhm = new LibreHardwareMonitor.Hardware.Computer
				{
					IsCpuEnabled = true,
					IsGpuEnabled = true,
					IsMotherboardEnabled = true,   // <-- WAJIB true untuk CPU temp
					IsControllerEnabled = false,
					IsNetworkEnabled = false,
					IsStorageEnabled = false,
					IsMemoryEnabled = false
				};
				_lhm.Open();
			}
			catch (Exception ex)
			{
				_lhm = null;
				LastLhmError = ex.GetType().Name + ": " + ex.Message;
			}
		}

		public static SensorSample Read()
{
    Init();
    InitLHM();
    var s = new SensorSample();

    // --- CPU Freq & Load: PerformanceCounter ---
    if (_cpuFreqPct != null && _cpuBaseMHz > 0)
    {
        try { s.CpuFreqMHz = _cpuBaseMHz * _cpuFreqPct.NextValue() / 100f; } catch { }
    }
    if (_cpuLoad != null)
    {
        try { s.CpuLoadPct = _cpuLoad.NextValue(); } catch { }
    }

    // --- CPU/GPU via LibreHardwareMonitor ---
    if (_lhm != null)
    {
        try
        {
            foreach (var hw in _lhm.Hardware)
            {
                // Paksa update hardware ini + sub-hardware-nya
                hw.Update();
                if (hw.SubHardware != null)
                    foreach (var sub in hw.SubHardware) sub.Update();

                bool isCpu = hw.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.Cpu;
                bool isGpu = hw.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.GpuNvidia
                          || hw.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.GpuAmd
                          || hw.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.GpuIntel;

                if (isCpu)
                {
                    float? packageTemp = null, coreMax = null, firstCore = null;
                    float? maxClock = null;

                    foreach (var sensor in hw.Sensors)
                    {
                        if (sensor.Value == null) continue;
                        float v = sensor.Value.Value;
                        string lname = sensor.Name.ToLowerInvariant();

                        if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Temperature)
                        {
                            if (lname == "cpu package" || lname.Contains("package"))
                                packageTemp = v;
                            else if (lname == "core max" || lname.Contains("core max"))
                                coreMax = v;
                            else if (lname.StartsWith("cpu core #") && !lname.Contains("distance"))
                            {
                                if (!firstCore.HasValue) firstCore = v;
                            }
                        }
                        else if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Clock
                                 && lname.StartsWith("cpu core #"))
                        {
                            if (!maxClock.HasValue || v > maxClock.Value) maxClock = v;
                        }
                    }

                    // Prioritas: Package > Core Max > Core #1
                    s.CpuTempC = packageTemp ?? coreMax ?? firstCore;
                    if (maxClock.HasValue) s.CpuFreqMHz = maxClock;
                }
                else if (isGpu && !s.GpuTempC.HasValue)
                {
                    foreach (var sensor in hw.Sensors)
                    {
                        if (sensor.Value == null) continue;
                        float v = sensor.Value.Value;
                        string lname = sensor.Name.ToLowerInvariant();

                        if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Temperature
                            && lname.Contains("gpu core"))
                            s.GpuTempC = v;
                        else if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Clock
                                 && lname == "gpu core")
                            s.GpuFreqMHz = v;
                        else if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Load
                                 && lname == "gpu core")
                            s.GpuLoadPct = v;
                    }
                    s.GpuName = hw.Name;
                }
            }
        }
        catch { }
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
    Label _lblCpu, _lblGpu;
    bool _drag = false;
    Point _dragOffset;

    public TaskbarOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(280, 26);

        _lblCpu = new Label
        {
            AutoSize = true, Location = new Point(6, 3),
            Font = new Font("Consolas", 9.5f, FontStyle.Bold),
            Text = "CPU --", BackColor = Color.Transparent
        };
        _lblGpu = new Label
        {
            AutoSize = true, Location = new Point(6, 3),
            Font = new Font("Consolas", 9.5f, FontStyle.Bold),
            Text = "GPU --", BackColor = Color.Transparent
        };
        Controls.Add(_lblCpu);
        Controls.Add(_lblGpu);

        MouseEventHandler down = (s, e) => { if (e.Button == MouseButtons.Left) { _drag = true; _dragOffset = e.Location; } };
        MouseEventHandler move = (s, e) => { if (_drag) Location = new Point(Location.X + e.X - _dragOffset.X, Location.Y + e.Y - _dragOffset.Y); };
        MouseEventHandler up = (s, e) => { _drag = false; };
        MouseDown += down; MouseMove += move; MouseUp += up;
        _lblCpu.MouseDown += down; _lblCpu.MouseMove += move; _lblCpu.MouseUp += up;
        _lblGpu.MouseDown += down; _lblGpu.MouseMove += move; _lblGpu.MouseUp += up;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }

    public void SetItems(string cpuText, string gpuText, bool showCpu, bool showGpu)
    {
        _lblCpu.Text = cpuText;
        _lblGpu.Text = gpuText;
        _lblCpu.Visible = showCpu;
        _lblGpu.Visible = showGpu;

        int x = 6;
        if (showCpu) { _lblCpu.Location = new Point(x, 3); x += _lblCpu.PreferredWidth + 10; }
        if (showGpu) { _lblGpu.Location = new Point(x, 3); x += _lblGpu.PreferredWidth + 6; }

        int h = Math.Max(
            showCpu ? _lblCpu.PreferredHeight : 0,
            showGpu ? _lblGpu.PreferredHeight : 0) + 6;
        if (h < 22) h = 22;
        if (x < 80) x = 80;
        if (x > 900) x = 900;
        Size = new Size(x, h);
    }

    public void ApplyColors(Color bg, Color cpuFg, Color gpuFg, int opacityPct)
    {
        BackColor = bg;
        _lblCpu.ForeColor = cpuFg;
        _lblGpu.ForeColor = gpuFg;
        double op = Math.Max(10, Math.Min(100, opacityPct)) / 100.0;
        Opacity = op;
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
		readonly List<NotifyIcon> _sensorIcons = new List<NotifyIcon>();
		readonly ContextMenuStrip _menu;
        int _hotkeyId = -1;
        static Icon _iconOn, _iconOff;

        public TrayContext(string[] args)
        {
            _cfg = Config.Load();

            _overlay = new TaskbarOverlay();
			_overlay.ApplyColors(
				Color.FromArgb(_cfg.OverlayBg),
				Color.FromArgb(_cfg.OverlayCpuColor),
				Color.FromArgb(_cfg.OverlayGpuColor),
				_cfg.OverlayOpacity);            
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
			var _forceHandle = _form.Handle; // supaya BeginInvoke bisa dipakai
            _form.OnHotkeyChanged += RegisterHotkeyFromConfig;
            _form.OnExitRequested += ExitApp;

            _menu = new ContextMenuStrip();
            _menu.Items.Add("Open Control Panel", null, (s, e) => ShowForm());
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("Turbo ON", null, (s, e) => SetTurbo(true, "tray"));
            _menu.Items.Add("Turbo OFF", null, (s, e) => SetTurbo(false, "tray"));
            _menu.Items.Add("Toggle Turbo", null, (s, e) => SetTurbo(!Turbo.Get(), "tray toggle"));
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("Reload from System", null, (s, e) => { Turbo.ReloadFromSystem(); _form.RefreshStatus(); UpdateIcon(); });
			_menu.Items.Add("Sensor Diagnostics...", null, (s, e) =>
				{
					var f = new SensorDiagForm();
					f.Show();
				});
            _menu.Items.Add("Exit", null, (s, e) => ExitApp());

            _icon = new NotifyIcon
            {
                Visible = true,
                ContextMenuStrip = _menu,
                Icon = GetIcon(Turbo.Get())
            };
            _icon.Text = "Turbo Boost: " + (Turbo.Get() ? "ON" : "OFF");
            _icon.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowForm(); };
			RebuildSensorIcons();
            _autoTimer = new System.Windows.Forms.Timer { Interval = _cfg.PollIntervalMs };
            _autoTimer.Tick += (s, e) => AutoTick();
            _autoTimer.Start();

            _sensorTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _sensorTimer.Tick += (s, e) => SensorTick();
            _sensorTimer.Start();

            SensorTick();
            UpdateIcon();
			
			bool launchedAtStartup = args.Any(a =>
			a.Equals("--startup", StringComparison.OrdinalIgnoreCase));

			if (_cfg.ShowFormOnStartup && !launchedAtStartup)
			{
				// tampilkan form setelah app siap (delay 800ms)
				var t = new System.Windows.Forms.Timer { Interval = 800 };
				t.Tick += (s2, e2) => { t.Stop(); t.Dispose(); ShowForm(); };
				t.Start();
			}
			// kalau launchedAtStartup && !ShowFormOnStartup -> tetap di tray (hidden)
        }
		
		static readonly string[] SensorOrder =
		{
			"cpu_temp", "cpu_freq", "cpu_load",
			"gpu_temp", "gpu_freq", "gpu_load"
		};

		static string SensorName(string key)
		{
			switch (key)
			{
				case "cpu_temp": return "CPU Temp";
				case "cpu_freq": return "CPU Freq";
				case "cpu_load": return "CPU Load";
				case "gpu_temp": return "GPU Temp";
				case "gpu_freq": return "GPU Freq";
				case "gpu_load": return "GPU Load";
			}
			return key;
		}

		List<string> GetActiveSensorKeys()
		{
			var list = new List<string>();
			if (_cfg.TrayShowCpuTemp) list.Add("cpu_temp");
			if (_cfg.TrayShowCpuFreq) list.Add("cpu_freq");
			if (_cfg.TrayShowCpuLoad) list.Add("cpu_load");
			if (_cfg.TrayShowGpuTemp) list.Add("gpu_temp");
			if (_cfg.TrayShowGpuFreq) list.Add("gpu_freq");
			if (_cfg.TrayShowGpuLoad) list.Add("gpu_load");
			return list;
		}

		public void RebuildSensorIcons()
		{
			// dispose semua ikon sensor lama
			foreach (var ni in _sensorIcons)
			{
				try { ni.Visible = false; ni.Dispose(); } catch { }
			}
			_sensorIcons.Clear();

			if (!_cfg.TrayInfoEnabled) return;

			var keys = GetActiveSensorKeys();
			bool on = Turbo.Get();
			foreach (var key in keys)
			{
				var ni = new NotifyIcon
				{
					Visible = true,
					Icon = GetIcon(on),
					ContextMenuStrip = _menu,
					Text = SensorName(key) + ": --"
				};
				ni.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowForm(); };
				_sensorIcons.Add(ni);
			}
		}

		// dipakai SensorTick
		void UpdateSensorIcon(NotifyIcon ni, string key, SensorSample s, bool turboOn)
		{
			string shortVal, fullVal;
			float? num;
			bool isCpu = key.StartsWith("cpu_");

			switch (key)
			{
				case "cpu_temp":
					num = s.CpuTempC;
					shortVal = num.HasValue ? num.Value.ToString("0") + "°" : "--";
					fullVal = num.HasValue ? num.Value.ToString("0") + "°C" : "N/A";
					break;
				case "cpu_freq":
					num = s.CpuFreqMHz;
					shortVal = num.HasValue ? (num.Value / 1000f).ToString("0.0") : "----";
					fullVal = num.HasValue ? (num.Value / 1000f).ToString("0.00") + " GHz" : "N/A";
					break;
				case "cpu_load":
					num = s.CpuLoadPct;
					shortVal = num.HasValue ? num.Value.ToString("0") + "%" : "--";
					fullVal = num.HasValue ? num.Value.ToString("0") + " %" : "N/A";
					break;
				case "gpu_temp":
					num = s.GpuTempC;
					shortVal = num.HasValue ? num.Value.ToString("0") + "°" : "--";
					fullVal = num.HasValue ? num.Value.ToString("0") + "°C" : "N/A";
					break;
				case "gpu_freq":
					num = s.GpuFreqMHz;
					shortVal = num.HasValue ? (num.Value / 1000f).ToString("0.0") : "----";
					fullVal = num.HasValue ? (num.Value / 1000f).ToString("0.00") + " GHz" : "N/A";
					break;
				case "gpu_load":
					num = s.GpuLoadPct;
					shortVal = num.HasValue ? num.Value.ToString("0") + "%" : "--";
					fullVal = num.HasValue ? num.Value.ToString("0") + " %" : "N/A";
					break;
				default:
					return;
			}

			// Warna terpisah CPU vs GPU
			Color fg = isCpu ? Color.FromArgb(_cfg.TrayCpuColor)
							 : Color.FromArgb(_cfg.TrayGpuColor);

			var bmp = DrawSingleValueIcon(fg, shortVal);
			var ic = IconFromBitmap(bmp);
			bmp.Dispose();
			ni.Icon = ic;

			string tip = SensorName(key) + ": " + fullVal;
			if (tip.Length > 63) tip = tip.Substring(0, 63);
			ni.Text = tip;
		}
		
		static Bitmap DrawSingleValueIcon(Color fg, string value)
		{
			int size = 64;
			var bmp = new Bitmap(size, size);
			using (var g = Graphics.FromImage(bmp))
			{
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
				g.Clear(Color.Transparent);

				// Auto-fit: mulai besar, kecilkan sampai muat
				float fontSize = size * 0.92f;
				Font chosen = null;
				try
				{
					var sf0 = new StringFormat
					{
						Alignment = StringAlignment.Center,
						LineAlignment = StringAlignment.Center,
						FormatFlags = StringFormatFlags.NoWrap
					};

					while (fontSize > 8f)
					{
						var test = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
						var sz = g.MeasureString(value, test, new PointF(0, 0), sf0);
						if (sz.Width <= size - 2 && sz.Height <= size - 2)
						{
							chosen = test;
							break;
						}
						test.Dispose();
						fontSize -= 1.5f;
					}
					if (chosen == null)
						chosen = new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Pixel);

					using (chosen)
					using (var tb = new SolidBrush(fg))
					using (var sf = new StringFormat
					{
						Alignment = StringAlignment.Center,
						LineAlignment = StringAlignment.Center,
						FormatFlags = StringFormatFlags.NoWrap
					})
					{
						g.DrawString(value, chosen, tb, new RectangleF(0, 0, size, size), sf);
					}
				}
				catch { }
			}
			return bmp;
		}
		
		static GraphicsPath RoundedRect(Rectangle r, int radius)
		{
			var p = new GraphicsPath();
			int d = radius * 2;
			if (d > r.Height) d = r.Height;
			if (d > r.Width) d = r.Width;
			p.AddArc(r.X, r.Y, d, d, 180, 90);
			p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
			p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
			p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
			p.CloseFigure();
			return p;
		}

		public void ApplyAutoStart(bool enabled)
		{
			try
			{
				string exe = Application.ExecutablePath;
				string args;
				if (enabled)
				{
					args = "/create /tn \"TurboToggle\" /tr \"\\\"" + exe + "\\\" --startup\" " +
						   "/sc onlogon /rl highest /f";
				}
				else
				{
					args = "/delete /tn \"TurboToggle\" /f";
				}
				var psi = new ProcessStartInfo("schtasks", args)
				{
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				};
				using (var p = Process.Start(psi))
				{
					p.StandardOutput.ReadToEnd();
					p.StandardError.ReadToEnd();
					p.WaitForExit(8000);
				}
			}
			catch { }
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
				ShowToast("Turbo Boost",
					(on ? "Turbo: ON" : "Turbo: OFF") + "   [" + source + "]");
			}
		}

		void ShowToast(string title, string message)
		{
			try
			{
				var t = new ToastForm(title, message, 2000);
				t.Show();
			}
			catch { }
		}

        void AutoTick()
		{
			if (!_cfg.AutoSwitchEnabled) return;
			bool anyTriggerRunning = _cfg.Triggers.Count > 0 && IsAnyTriggerRunning();
			bool desired = !anyTriggerRunning;   // trigger jalan -> OFF, tidak ada -> ON
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
			bool wantOverlay = _cfg.OverlayEnabled && _overlay.Visible;
			bool wantTray = _cfg.TrayInfoEnabled && _sensorIcons.Count > 0;
			if (!wantOverlay && !wantTray) return;

			var snap = _cfg;
			var keys = GetActiveSensorKeys();

			Task.Run(() =>
			{
				try
				{
					var s = SensorReader.Read();

					// ---- overlay text ----
					string cpuText = "", gpuText = "";
					bool showCpu = false, showGpu = false;
					if (wantOverlay)
					{
						var cpuParts = new List<string>();
						if (snap.ShowCpuTemp) cpuParts.Add("CPU " + (s.CpuTempC.HasValue ? s.CpuTempC.Value.ToString("0") + "°C" : "--"));
						if (snap.ShowCpuFreq) cpuParts.Add(s.CpuFreqMHz.HasValue ? (s.CpuFreqMHz.Value / 1000f).ToString("0.00") + "GHz" : "----");
						if (snap.ShowCpuLoad) cpuParts.Add(s.CpuLoadPct.HasValue ? s.CpuLoadPct.Value.ToString("0") + "%" : "--");
						cpuText = string.Join(" ", cpuParts);
						showCpu = cpuParts.Count > 0;

						var gpuParts = new List<string>();
						if (snap.ShowGpuTemp) gpuParts.Add("GPU " + (s.GpuTempC.HasValue ? s.GpuTempC.Value.ToString("0") + "°C" : "--"));
						if (snap.ShowGpuFreq) gpuParts.Add(s.GpuFreqMHz.HasValue ? (s.GpuFreqMHz.Value / 1000f).ToString("0.00") + "GHz" : "----");
						if (snap.ShowGpuLoad) gpuParts.Add(s.GpuLoadPct.HasValue ? s.GpuLoadPct.Value.ToString("0") + "%" : "--");
						gpuText = string.Join(" ", gpuParts);
						showGpu = gpuParts.Count > 0;

						if (!showCpu && !showGpu) { cpuText = "CPU --°C"; showCpu = true; }
					}

					// ---- UI apply ----
					try
					{
						if (_form.IsHandleCreated && !_form.IsDisposed)
						{
							_form.BeginInvoke(new Action(() =>
							{
								try
								{
									if (wantOverlay) _overlay.SetItems(cpuText, gpuText, showCpu, showGpu);

									bool on = Turbo.Get();

									if (_cfg.TrayInfoEnabled)
									{
										for (int i = 0; i < keys.Count && i < _sensorIcons.Count; i++)
											UpdateSensorIcon(_sensorIcons[i], keys[i], s, on);
									}
								}
								catch { }
							}));
						}
					}
					catch { }
				}
				catch { }
			});
		}
		public void UpdateIcon()        
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
			
			foreach (var ni in _sensorIcons)
			{
				try { ni.Visible = false; ni.Dispose(); } catch { }
			}
			_sensorIcons.Clear();
			
            Application.Exit();
        }
		
		//======= Tambah P/Invoke & helper 
		[DllImport("user32.dll", SetLastError = true)]
		static extern bool DestroyIcon(IntPtr hIcon);

		static Icon IconFromBitmap(Bitmap bmp)
		{
			IntPtr h = bmp.GetHicon();
			try
			{
				using (var tmp = Icon.FromHandle(h))
					return (Icon)tmp.Clone();
			}
			finally { DestroyIcon(h); }
		}

		static Bitmap DrawSensorIcon(Color bg, Color fg, string top, string bottom)
		{
			int size = 32;
			var bmp = new Bitmap(size, size);
			using (var g = Graphics.FromImage(bmp))
			{
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
				g.Clear(Color.Transparent);

				using (var b = new SolidBrush(bg))
					g.FillEllipse(b, 1, 1, size - 2, size - 2);

				if (string.IsNullOrEmpty(bottom))
				{
					using (var f = new Font("Segoe UI", 13, FontStyle.Bold, GraphicsUnit.Pixel))
					using (var tb = new SolidBrush(fg))
					{
						var sf = new StringFormat
						{
							Alignment = StringAlignment.Center,
							LineAlignment = StringAlignment.Center
						};
						g.DrawString(top, f, tb, new RectangleF(0, 0, size, size), sf);
					}
				}
				else
				{
					using (var f = new Font("Segoe UI", 9, FontStyle.Bold, GraphicsUnit.Pixel))
					using (var tb = new SolidBrush(fg))
					{
						var sf = new StringFormat
						{
							Alignment = StringAlignment.Center,
							LineAlignment = StringAlignment.Center
						};
						g.DrawString(top, f, tb, new RectangleF(0, 0, size, size / 2), sf);
						g.DrawString(bottom, f, tb, new RectangleF(0, size / 2 - 1, size, size / 2), sf);
					}
				}
			}
			return bmp;
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
		
		readonly List<NotifyIcon> _sensorIcons = new List<NotifyIcon>();
//		readonly ContextMenuStrip _menu;

        Label _statusLabel;
        Panel _statusDot;
        Button _btnOn, _btnOff;
        CheckBox _chkNotify, _chkTurboOnExit;
        CheckBox _chkAuto;
        NumericUpDown _numInterval;
        ListBox _lstTriggers;
        CheckBox _chkOverlayEnabled;
        CheckBox _chkCpuFreq, _chkCpuTemp, _chkCpuLoad, _chkGpuFreq, _chkGpuTemp, _chkGpuLoad;
        //Panel _bgPreview, _fgPreview;
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
                Text = "Turbo OFF saat app trigger berjalan, Turbo ON saat tidak ada trigger aktif.",
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

			// --- Warna Background overlay ---
			tpOver.Controls.Add(new Label { Text = "Background:", Left = 12, Top = 220, Width = 90, Height = 20 });
			var bgPreview = new Panel
			{
				Left = 108, Top = 218, Width = 30, Height = 22,
				BackColor = Color.FromArgb(_cfg.OverlayBg),
				BorderStyle = BorderStyle.FixedSingle
			};
			tpOver.Controls.Add(bgPreview);
			var btnBg = new Button { Text = "Pilih...", Left = 148, Top = 216, Width = 80, Height = 26 };
			btnBg.Click += (s, e) =>
			{
				using (var dlg = new ColorDialog { Color = Color.FromArgb(_cfg.OverlayBg) })
					if (dlg.ShowDialog(this) == DialogResult.OK)
					{
						_cfg.OverlayBg = dlg.Color.ToArgb();
						bgPreview.BackColor = dlg.Color;
						_overlay.ApplyColors(
							Color.FromArgb(_cfg.OverlayBg),
							Color.FromArgb(_cfg.OverlayCpuColor),
							Color.FromArgb(_cfg.OverlayGpuColor),
							_cfg.OverlayOpacity);
						_cfg.Save();
					}
			};
			tpOver.Controls.Add(btnBg);

			// --- Warna Teks CPU ---
			tpOver.Controls.Add(new Label { Text = "Teks CPU:", Left = 240, Top = 220, Width = 70, Height = 20 });
			var cpuPreview = new Panel
			{
				Left = 316, Top = 218, Width = 30, Height = 22,
				BackColor = Color.FromArgb(_cfg.OverlayCpuColor),
				BorderStyle = BorderStyle.FixedSingle
			};
			tpOver.Controls.Add(cpuPreview);
			var btnCpu = new Button { Text = "Pilih...", Left = 356, Top = 216, Width = 80, Height = 26 };
			btnCpu.Click += (s, e) =>
			{
				using (var dlg = new ColorDialog { Color = Color.FromArgb(_cfg.OverlayCpuColor) })
					if (dlg.ShowDialog(this) == DialogResult.OK)
					{
						_cfg.OverlayCpuColor = dlg.Color.ToArgb();
						cpuPreview.BackColor = dlg.Color;
						_overlay.ApplyColors(
							Color.FromArgb(_cfg.OverlayBg),
							Color.FromArgb(_cfg.OverlayCpuColor),
							Color.FromArgb(_cfg.OverlayGpuColor),
							_cfg.OverlayOpacity);
						_cfg.Save();
					}
			};
			tpOver.Controls.Add(btnCpu);

			// --- Warna Teks GPU ---
			tpOver.Controls.Add(new Label { Text = "Teks GPU:", Left = 12, Top = 252, Width = 90, Height = 20 });
			var gpuPreview = new Panel
			{
				Left = 108, Top = 250, Width = 30, Height = 22,
				BackColor = Color.FromArgb(_cfg.OverlayGpuColor),
				BorderStyle = BorderStyle.FixedSingle
			};
			tpOver.Controls.Add(gpuPreview);
			var btnGpu = new Button { Text = "Pilih...", Left = 148, Top = 248, Width = 80, Height = 26 };
			btnGpu.Click += (s, e) =>
			{
				using (var dlg = new ColorDialog { Color = Color.FromArgb(_cfg.OverlayGpuColor) })
					if (dlg.ShowDialog(this) == DialogResult.OK)
					{
						_cfg.OverlayGpuColor = dlg.Color.ToArgb();
						gpuPreview.BackColor = dlg.Color;
						_overlay.ApplyColors(
							Color.FromArgb(_cfg.OverlayBg),
							Color.FromArgb(_cfg.OverlayCpuColor),
							Color.FromArgb(_cfg.OverlayGpuColor),
							_cfg.OverlayOpacity);
						_cfg.Save();
					}
			};
			tpOver.Controls.Add(btnGpu);

			// --- Opacity slider ---
			tpOver.Controls.Add(new Label { Text = "Opacity:", Left = 240, Top = 252, Width = 70, Height = 20 });
			var trackOpacity = new TrackBar
			{
				Left = 310, Top = 246, Width = 120, Height = 32,
				Minimum = 10, Maximum = 100, TickFrequency = 10,
				Value = _cfg.OverlayOpacity
			};
			var lblOpacity = new Label
			{
				Text = _cfg.OverlayOpacity + "%", Left = 434, Top = 254, Width = 40, Height = 20
			};
			trackOpacity.ValueChanged += (s, e) =>
			{
				_cfg.OverlayOpacity = trackOpacity.Value;
				lblOpacity.Text = trackOpacity.Value + "%";
				_overlay.ApplyColors(
					Color.FromArgb(_cfg.OverlayBg),
					Color.FromArgb(_cfg.OverlayCpuColor),
					Color.FromArgb(_cfg.OverlayGpuColor),
					_cfg.OverlayOpacity);
				_cfg.Save();
			};
			tpOver.Controls.Add(trackOpacity);
			tpOver.Controls.Add(lblOpacity);

			// --- Reset posisi ---
			var btnReset = new Button
			{
				Text = "Reset Posisi ke Pojok Kanan Bawah",
				Left = 12, Top = 295, Width = 260, Height = 28
			};
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
			
			// ---------- Tab Tray Icon ----------
			var tpTray = new TabPage("Tray Icon");
			tabs.TabPages.Add(tpTray);

			var chkTrayEnabled = new CheckBox
			{
				Text = "Tampilkan info sensor di ikon tray",
				Left = 12, Top = 12, Width = 430, Height = 24,
				Checked = _cfg.TrayInfoEnabled
			};
			chkTrayEnabled.CheckedChanged += (s, e) =>
			{
				_cfg.TrayInfoEnabled = chkTrayEnabled.Checked;
				_cfg.Save();
				_ctx.RebuildSensorIcons();
			};
			tpTray.Controls.Add(chkTrayEnabled);

			tpTray.Controls.Add(new Label
			{
				Text = "Item yang ditampilkan sebagai ikon tray terpisah:",
				Left = 12, Top = 44, Width = 430, Height = 20, ForeColor = Color.Gray
			});

			Action<string, bool, int, Action<bool>> mkTrayChk = (text, val, top, onChange) =>
			{
				var cb = new CheckBox { Text = text, Left = 20, Top = top, Width = 300, Height = 22, Checked = val };
				cb.CheckedChanged += (s, e) => { onChange(cb.Checked); _ctx.RebuildSensorIcons(); };
				tpTray.Controls.Add(cb);
			};
			mkTrayChk("CPU Temp", _cfg.TrayShowCpuTemp, 68, v => { _cfg.TrayShowCpuTemp = v; _cfg.Save(); });
			mkTrayChk("CPU Freq", _cfg.TrayShowCpuFreq, 90, v => { _cfg.TrayShowCpuFreq = v; _cfg.Save(); });
			mkTrayChk("CPU Load", _cfg.TrayShowCpuLoad, 112, v => { _cfg.TrayShowCpuLoad = v; _cfg.Save(); });
			mkTrayChk("GPU Temp", _cfg.TrayShowGpuTemp, 140, v => { _cfg.TrayShowGpuTemp = v; _cfg.Save(); });
			mkTrayChk("GPU Freq", _cfg.TrayShowGpuFreq, 162, v => { _cfg.TrayShowGpuFreq = v; _cfg.Save(); });
			mkTrayChk("GPU Load", _cfg.TrayShowGpuLoad, 184, v => { _cfg.TrayShowGpuLoad = v; _cfg.Save(); });

			// --- Warna teks CPU & GPU ---
			tpTray.Controls.Add(new Label
			{
				Text = "Warna teks ikon:",
				Left = 12, Top = 218, Width = 150, Height = 20,
				Font = new Font("Segoe UI", 9f, FontStyle.Bold)
			});

			tpTray.Controls.Add(new Label { Text = "CPU:", Left = 20, Top = 244, Width = 40, Height = 20 });
			var cpuColorPreview = new Panel
			{
				Left = 66, Top = 242, Width = 30, Height = 22,
				BackColor = Color.FromArgb(_cfg.TrayCpuColor),
				BorderStyle = BorderStyle.FixedSingle
			};
			tpTray.Controls.Add(cpuColorPreview);
			var btnCpuColor = new Button { Text = "Pilih...", Left = 106, Top = 240, Width = 80, Height = 26 };
			btnCpuColor.Click += (s, e) =>
			{
				using (var dlg = new ColorDialog { Color = Color.FromArgb(_cfg.TrayCpuColor), FullOpen = true })
					if (dlg.ShowDialog(this) == DialogResult.OK)
					{
						_cfg.TrayCpuColor = dlg.Color.ToArgb();
						cpuColorPreview.BackColor = dlg.Color;
						_cfg.Save();
					}
			};
			tpTray.Controls.Add(btnCpuColor);

			tpTray.Controls.Add(new Label { Text = "GPU:", Left = 200, Top = 244, Width = 40, Height = 20 });
			var gpuColorPreview = new Panel
			{
				Left = 246, Top = 242, Width = 30, Height = 22,
				BackColor = Color.FromArgb(_cfg.TrayGpuColor),
				BorderStyle = BorderStyle.FixedSingle
			};
			tpTray.Controls.Add(gpuColorPreview);
			var btnGpuColor = new Button { Text = "Pilih...", Left = 286, Top = 240, Width = 80, Height = 26 };
			btnGpuColor.Click += (s, e) =>
			{
				using (var dlg = new ColorDialog { Color = Color.FromArgb(_cfg.TrayGpuColor), FullOpen = true })
					if (dlg.ShowDialog(this) == DialogResult.OK)
					{
						_cfg.TrayGpuColor = dlg.Color.ToArgb();
						gpuColorPreview.BackColor = dlg.Color;
						_cfg.Save();
					}
			};
			tpTray.Controls.Add(btnGpuColor);

			tpTray.Controls.Add(new Label
			{
				Text = "Background ikon tray transparan (mengikuti warna taskbar).",
				Left = 20, Top = 278, Width = 420, Height = 20,
				ForeColor = Color.Gray, Font = new Font("Segoe UI", 8f, FontStyle.Italic)
			});

			tpTray.Controls.Add(new Label
			{
				Text = "Tips: pilih warna kontras terhadap taskbar. Taskbar gelap → warna terang\n" +
					   "(putih, kuning, cyan). Taskbar terang → warna gelap (hitam, merah).",
				Left = 12, Top = 304, Width = 430, Height = 40, ForeColor = Color.Gray
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
			
			var chkAutoStart = new CheckBox
			{
				Text = "Jalankan otomatis saat Windows start (butuh admin)",
				Left = 12, Top = 72, Width = 420, Height = 24,
				Checked = _cfg.AutoStartEnabled
			};
			chkAutoStart.CheckedChanged += (s, e) =>
			{
				_cfg.AutoStartEnabled = chkAutoStart.Checked;
				_cfg.Save();
				_ctx.ApplyAutoStart(chkAutoStart.Checked);
			};
			tpBeh.Controls.Add(chkAutoStart);

			var chkShowOnStartup = new CheckBox
			{
				Text = "Tampilkan window saat boot (kalau tidak dicentang, otomatis hide ke tray)",
				Left = 12, Top = 102, Width = 420, Height = 24,
				Checked = _cfg.ShowFormOnStartup
			};
			chkShowOnStartup.CheckedChanged += (s, e) =>
			{
				_cfg.ShowFormOnStartup = chkShowOnStartup.Checked;
				_cfg.Save();
			};
			tpBeh.Controls.Add(chkShowOnStartup);

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
	
	//Toas Form ==================================
	class ToastForm : Form
{
    System.Windows.Forms.Timer _timer;

    public ToastForm(string title, string message, int durationMs)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(340, 86);
        BackColor = Color.FromArgb(40, 40, 40);

        var pnlAccent = new Panel { Left = 0, Top = 0, Width = 6, Height = 86, BackColor = Color.FromArgb(52, 152, 219) };
        Controls.Add(pnlAccent);

        var lblTitle = new Label
        {
            Text = title, Left = 18, Top = 10, Width = 310, Height = 22,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.White, BackColor = Color.Transparent
        };
        Controls.Add(lblTitle);

        var lblMsg = new Label
        {
            Text = message, Left = 18, Top = 36, Width = 310, Height = 44,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(220, 220, 220), BackColor = Color.Transparent
        };
        Controls.Add(lblMsg);

        var wa = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(wa.Right - Width - 16, wa.Bottom - Height - 16);

        EventHandler dismiss = (s, e) => Close();
        Click += dismiss;
        pnlAccent.Click += dismiss;
        lblTitle.Click += dismiss;
        lblMsg.Click += dismiss;

        _timer = new System.Windows.Forms.Timer { Interval = durationMs };
        _timer.Tick += (s, e) => { _timer.Stop(); Close(); };
        _timer.Start();
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW (tidak muncul di Alt+Tab)
            return cp;
        }
    }
}

// window diagnostik ============
class SensorDiagForm : Form
{
    public SensorDiagForm()
    {
        Text = "Sensor Diagnostics — LibreHardwareMonitor";
        Width = 780;
        Height = 600;
        StartPosition = FormStartPosition.CenterScreen;

        var txt = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9f),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220)
        };
        Controls.Add(txt);

        var sb = new StringBuilder();
        sb.AppendLine("=== LHM Init Status ===");
        if (SensorReader.LastLhmError != null)
        {
            sb.AppendLine("FAILED: " + SensorReader.LastLhmError);
            sb.AppendLine();
            sb.AppendLine("Kemungkinan penyebab:");
            sb.AppendLine("- DLL salah versi (net6.0/net8.0, bukan net472)");
            sb.AppendLine("- DLL corrupt saat download");
            sb.AppendLine("- Aplikasi tidak jalan sebagai Administrator");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("OK (atau belum pernah dicoba)");
            sb.AppendLine();
        }

        try
        {
            var comp = new LibreHardwareMonitor.Hardware.Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsMemoryEnabled = true,
                IsStorageEnabled = false,
                IsNetworkEnabled = false
            };
            comp.Open();

            sb.AppendLine("=== Hardware yang terdeteksi ===");
            sb.AppendLine();

            foreach (var hw in comp.Hardware)
                DumpHardware(sb, hw, 0);

            comp.Close();
        }
        catch (Exception ex)
        {
            sb.AppendLine("ERROR membuka LHM:");
            sb.AppendLine(ex.GetType().Name + ": " + ex.Message);
            sb.AppendLine();
            sb.AppendLine(ex.StackTrace);
        }

        txt.Text = sb.ToString();
    }

    static void DumpHardware(StringBuilder sb, LibreHardwareMonitor.Hardware.IHardware hw, int indent)
    {
        string pad = new string(' ', indent);
        sb.AppendLine(pad + "[" + hw.HardwareType + "] " + hw.Name);
        try { hw.Update(); } catch { }
        foreach (var sensor in hw.Sensors)
        {
            string val = sensor.Value.HasValue ? sensor.Value.Value.ToString("0.00") : "n/a";
            sb.AppendLine(pad + "   " + sensor.SensorType + " | " + sensor.Name + " = " + val);
        }
        foreach (var sub in hw.SubHardware)
            DumpHardware(sb, sub, indent + 3);
    }
}
	
}