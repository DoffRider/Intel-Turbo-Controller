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
using System.Net.NetworkInformation;
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
		
		// Overlay checkboxes
		public bool ShowCpuPower = false;
		public bool ShowCpuFan = false;
		public bool ShowCpuVolt = false;
		public bool ShowGpuHotspot = false;
		public bool ShowGpuPower = false;
		public bool ShowGpuVram = false;
		public bool ShowRam = false;
		public bool ShowMbTemp = false;
		public bool ShowNetDown = false;
		public bool ShowNetUp = false;
		public bool ShowDiskAct = false;
		public bool ShowUptime = false;

		// Tray checkboxes
		public bool TrayShowCpuPower = false;
		public bool TrayShowCpuFan = false;
		public bool TrayShowCpuVolt = false;
		public bool TrayShowGpuHotspot = false;
		public bool TrayShowGpuPower = false;
		public bool TrayShowGpuVram = false;
		public bool TrayShowRam = false;
		public bool TrayShowMbTemp = false;
		public bool TrayShowNetDown = false;
		public bool TrayShowNetUp = false;
		public bool TrayShowDiskAct = false;
		public bool TrayShowUptime = false;
		
		public int TraySysColor = unchecked((int)0xFFFFAA00);     // oranye
		public int OverlaySysColor = unchecked((int)0xFFFFAA00);  // oranye
		
		public bool OverlayVertical = false;   // false = horizontal, true = vertical
		public int OverlayVAlign = 1;   // 0 = kiri, 1 = tengah, 2 = kanan
		
		public int SensorIntervalMs = 1000;
		
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
				
				c.ShowCpuPower   = GB("ShowCpuPower", false);
				c.ShowCpuFan     = GB("ShowCpuFan", false);
				c.ShowCpuVolt    = GB("ShowCpuVolt", false);
				c.ShowGpuHotspot = GB("ShowGpuHotspot", false);
				c.ShowGpuPower   = GB("ShowGpuPower", false);
				c.ShowGpuVram    = GB("ShowGpuVram", false);
				c.ShowRam        = GB("ShowRam", false);
				c.ShowMbTemp     = GB("ShowMbTemp", false);
				c.ShowNetDown    = GB("ShowNetDown", false);
				c.ShowNetUp      = GB("ShowNetUp", false);
				c.ShowDiskAct    = GB("ShowDiskAct", false);
				c.ShowUptime     = GB("ShowUptime", false);

				c.TrayShowCpuPower   = GB("TrayShowCpuPower", false);
				c.TrayShowCpuFan     = GB("TrayShowCpuFan", false);
				c.TrayShowCpuVolt    = GB("TrayShowCpuVolt", false);
				c.TrayShowGpuHotspot = GB("TrayShowGpuHotspot", false);
				c.TrayShowGpuPower   = GB("TrayShowGpuPower", false);
				c.TrayShowGpuVram    = GB("TrayShowGpuVram", false);
				c.TrayShowRam        = GB("TrayShowRam", false);
				c.TrayShowMbTemp     = GB("TrayShowMbTemp", false);
				c.TrayShowNetDown    = GB("TrayShowNetDown", false);
				c.TrayShowNetUp      = GB("TrayShowNetUp", false);
				c.TrayShowDiskAct    = GB("TrayShowDiskAct", false);
				c.TrayShowUptime     = GB("TrayShowUptime", false);
				
				c.TraySysColor = GI("TraySysColor", unchecked((int)0xFFFFAA00));
				c.OverlaySysColor = GI("OverlaySysColor", unchecked((int)0xFFFFAA00));
				
				c.OverlayVertical = GB("OverlayVertical", false);
				c.OverlayVAlign = GI("OverlayVAlign", 1);
				
				c.SensorIntervalMs = GI("SensorIntervalMs", 1000);
				if (c.SensorIntervalMs < 500) c.SensorIntervalMs = 500;
				if (c.SensorIntervalMs > 10000) c.SensorIntervalMs = 10000;

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
				sb.AppendLine("ShowCpuPower=" + (ShowCpuPower ? "true" : "false"));
				sb.AppendLine("ShowCpuFan=" + (ShowCpuFan ? "true" : "false"));
				sb.AppendLine("ShowCpuVolt=" + (ShowCpuVolt ? "true" : "false"));
				sb.AppendLine("ShowGpuHotspot=" + (ShowGpuHotspot ? "true" : "false"));
				sb.AppendLine("ShowGpuPower=" + (ShowGpuPower ? "true" : "false"));
				sb.AppendLine("ShowGpuVram=" + (ShowGpuVram ? "true" : "false"));
				sb.AppendLine("ShowRam=" + (ShowRam ? "true" : "false"));
				sb.AppendLine("ShowMbTemp=" + (ShowMbTemp ? "true" : "false"));
				sb.AppendLine("ShowNetDown=" + (ShowNetDown ? "true" : "false"));
				sb.AppendLine("ShowNetUp=" + (ShowNetUp ? "true" : "false"));
				sb.AppendLine("ShowDiskAct=" + (ShowDiskAct ? "true" : "false"));
				sb.AppendLine("ShowUptime=" + (ShowUptime ? "true" : "false"));

				sb.AppendLine("TrayShowCpuPower=" + (TrayShowCpuPower ? "true" : "false"));
				sb.AppendLine("TrayShowCpuFan=" + (TrayShowCpuFan ? "true" : "false"));
				sb.AppendLine("TrayShowCpuVolt=" + (TrayShowCpuVolt ? "true" : "false"));
				sb.AppendLine("TrayShowGpuHotspot=" + (TrayShowGpuHotspot ? "true" : "false"));
				sb.AppendLine("TrayShowGpuPower=" + (TrayShowGpuPower ? "true" : "false"));
				sb.AppendLine("TrayShowGpuVram=" + (TrayShowGpuVram ? "true" : "false"));
				sb.AppendLine("TrayShowRam=" + (TrayShowRam ? "true" : "false"));
				sb.AppendLine("TrayShowMbTemp=" + (TrayShowMbTemp ? "true" : "false"));
				sb.AppendLine("TrayShowNetDown=" + (TrayShowNetDown ? "true" : "false"));
				sb.AppendLine("TrayShowNetUp=" + (TrayShowNetUp ? "true" : "false"));
				sb.AppendLine("TrayShowDiskAct=" + (TrayShowDiskAct ? "true" : "false"));
				sb.AppendLine("TrayShowUptime=" + (TrayShowUptime ? "true" : "false"));
				
				sb.AppendLine("OverlayVertical=" + (OverlayVertical ? "true" : "false"));
				sb.AppendLine("OverlayVAlign=" + OverlayVAlign);
				
				sb.AppendLine("TraySysColor=" + TraySysColor);
				sb.AppendLine("OverlaySysColor=" + OverlaySysColor);
				
				sb.AppendLine("SensorIntervalMs=" + SensorIntervalMs);
                File.WriteAllText(PathFile, sb.ToString());
            }
            catch { }
        }
    }

    // ============================================================
    class SensorSample
	{
		// CPU
		public float? CpuFreqMHz, CpuTempC, CpuLoadPct, CpuPowerW, CpuFanRpm, CpuVoltage;
		// GPU
		public float? GpuFreqMHz, GpuTempC, GpuLoadPct, GpuHotspotC, GpuPowerW;
		public float? GpuVramUsedMB, GpuVramTotalMB;
		public string GpuName;
		// System
		public float? RamUsedPct, RamUsedGB, RamTotalGB;
		public float? MbTempC;
		public float? NetDownMbps, NetUpMbps;
		public float? DiskActivityPct;
		public TimeSpan Uptime;
	}
	


        static class SensorReader
    {
        static PerformanceCounter _cpuFreqPct, _cpuLoad;
        static float _cpuBaseMHz = 0;
        static bool _init = false;
        static readonly object _lock = new object();

        static LibreHardwareMonitor.Hardware.Computer _lhm;
        static bool _lhmInit = false;
		
		[DllImport("kernel32.dll")]
		static extern ulong GetTickCount64();

		static PerformanceCounter _diskAct;
		static long _lastNetRx = 0, _lastNetTx = 0;
		static DateTime _lastNetTime = DateTime.MinValue;
		
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
				
				try { _diskAct = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total"); _diskAct.NextValue(); }
				catch { _diskAct = null; }

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
					IsMemoryEnabled = true
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

    // --- CPU freq/load dari PerformanceCounter (fallback) ---
    if (_cpuFreqPct != null && _cpuBaseMHz > 0)
    {
        try { s.CpuFreqMHz = _cpuBaseMHz * _cpuFreqPct.NextValue() / 100f; } catch { }
    }
    if (_cpuLoad != null)
    {
        try { s.CpuLoadPct = _cpuLoad.NextValue(); } catch { }
    }

    // --- LHM (CPU/GPU/MB) ---
    if (_lhm != null)
    {
        try
        {
            foreach (var hw in _lhm.Hardware)
            {
                hw.Update();
                if (hw.SubHardware != null)
                    foreach (var sub in hw.SubHardware) sub.Update();

                bool isCpu = hw.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.Cpu;
                bool isGpu = hw.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.GpuNvidia
                          || hw.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.GpuAmd
                          || hw.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.GpuIntel;
                bool isMem = hw.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.Memory;
                bool isMb  = hw.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.Motherboard;

                if (isCpu)
                {
                    float? pkgTemp = null, coreMax = null, firstCore = null, maxClock = null;
                    foreach (var sensor in hw.Sensors)
                    {
                        if (sensor.Value == null) continue;
                        float v = sensor.Value.Value;
                        string lname = sensor.Name.ToLowerInvariant();

                        if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Temperature)
                        {
                            if (lname.Contains("package")) pkgTemp = v;
                            else if (lname.Contains("core max")) coreMax = v;
                            else if (lname.StartsWith("cpu core #") && !lname.Contains("distance"))
                                if (!firstCore.HasValue) firstCore = v;
                        }
                        else if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Clock
                                 && lname.StartsWith("cpu core #"))
                        {
                            if (!maxClock.HasValue || v > maxClock.Value) maxClock = v;
                        }
                        else if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Load
                                 && lname.Contains("cpu total"))
                        {
                            s.CpuLoadPct = v;
                        }
                        else if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Power
                                 && lname.Contains("package"))
                        {
                            s.CpuPowerW = v;
                        }
                        else if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Voltage
                                 && (lname == "cpu core" || lname == "vcore"))
                        {
                            s.CpuVoltage = v;
                        }
                    }
                    s.CpuTempC = pkgTemp ?? coreMax ?? firstCore;
                    if (maxClock.HasValue) s.CpuFreqMHz = maxClock;
                }
                else if (isGpu && string.IsNullOrEmpty(s.GpuName))
                {
                    s.GpuName = hw.Name;
                    foreach (var sensor in hw.Sensors)
                    {
                        if (sensor.Value == null) continue;
                        float v = sensor.Value.Value;
                        string lname = sensor.Name.ToLowerInvariant();

                        if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Temperature)
                        {
                            if (lname.Contains("hot spot")) s.GpuHotspotC = v;
                            else if (lname.Contains("gpu core") || lname == "gpu core") s.GpuTempC = v;
                        }
                        else if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Clock
                                 && lname == "gpu core") s.GpuFreqMHz = v;
                        else if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Load
                                 && lname == "gpu core") s.GpuLoadPct = v;
                        else if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Power
                                 && (lname.Contains("package") || lname == "gpu power")) s.GpuPowerW = v;
                        else if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.SmallData)
                        {
                            if (lname.Contains("gpu memory used")) s.GpuVramUsedMB = v;
                            else if (lname.Contains("gpu memory total")) s.GpuVramTotalMB = v;
                        }
                    }
                }
                else if (isMem)
                {
                    foreach (var sensor in hw.Sensors)
                    {
                        if (sensor.Value == null) continue;
                        float v = sensor.Value.Value;
                        string lname = sensor.Name.ToLowerInvariant();
                        if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Load
                            && lname == "memory") s.RamUsedPct = v;
                        else if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Data
                                 && lname.Contains("memory used")) s.RamUsedGB = v;
                        else if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Data
                                 && lname.Contains("memory available")) { /* skip */ }
                    }
                }
                else if (isMb)
                {
                    // cari fan & temp dari SuperIO di sub-hardware
                    foreach (var sub in hw.SubHardware)
                    {
                        foreach (var sensor in sub.Sensors)
                        {
                            if (sensor.Value == null) continue;
                            float v = sensor.Value.Value;
                            string lname = sensor.Name.ToLowerInvariant();

                            if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Fan
                                && lname.StartsWith("fan #") && v > 0)
                            {
                                if (!s.CpuFanRpm.HasValue) s.CpuFanRpm = v;
                            }
                            else if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Temperature
                                     && lname.StartsWith("temperature #"))
                            {
                                // ambil yang paling wajar sebagai MB temp
                                if (!s.MbTempC.HasValue && v > 20 && v < 80) s.MbTempC = v;
                            }
                        }
                    }
                }
            }
        }
        catch { }
    }

    // --- hitung total RAM dari WMI kalau belum ada ---
    if (!s.RamTotalGB.HasValue)
    {
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
            foreach (ManagementObject mo in searcher.Get())
            {
                double kb = Convert.ToDouble(mo["TotalVisibleMemorySize"]);
                s.RamTotalGB = (float)(kb / 1024.0 / 1024.0);
                break;
            }
        }
        catch { }
    }
    if (s.RamUsedPct.HasValue && s.RamTotalGB.HasValue && !s.RamUsedGB.HasValue)
        s.RamUsedGB = s.RamTotalGB.Value * s.RamUsedPct.Value / 100f;

    // --- Network speed ---
    try
    {
        long totalRx = 0, totalTx = 0;
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            try
            {
                var st = ni.GetIPv4Statistics();
                totalRx += st.BytesReceived;
                totalTx += st.BytesSent;
            }
            catch { }
        }
        var now = DateTime.Now;
        if (_lastNetTime != DateTime.MinValue)
        {
            double sec = (now - _lastNetTime).TotalSeconds;
            if (sec > 0.1)
            {
                double dRx = (totalRx - _lastNetRx) / sec / 1024.0 / 1024.0 * 8; // Mbps
                double dTx = (totalTx - _lastNetTx) / sec / 1024.0 / 1024.0 * 8;
                if (dRx < 0) dRx = 0;
                if (dTx < 0) dTx = 0;
                s.NetDownMbps = (float)dRx;
                s.NetUpMbps = (float)dTx;
            }
        }
        _lastNetRx = totalRx;
        _lastNetTx = totalTx;
        _lastNetTime = now;
    }
    catch { }

    // --- Disk activity ---
    if (_diskAct != null)
    {
        try { s.DiskActivityPct = _diskAct.NextValue(); } catch { }
    }

    // --- System uptime ---
    try { s.Uptime = TimeSpan.FromMilliseconds(GetTickCount64()); } catch { }

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
        Label _lblCpu, _lblGpu, _lblSys;
        bool _drag = false;
        Point _dragOffset;
        bool _vertical = false;
		
		int _vAlign = 1;   // 0 = kiri, 1 = tengah, 2 = kanan

		public int VAlign
		{
			get { return _vAlign; }
			set { _vAlign = value; }
		}

        public bool Vertical
        {
            get { return _vertical; }
            set { _vertical = value; }
        }

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
            _lblSys = new Label
            {
                AutoSize = true, Location = new Point(6, 3),
                Font = new Font("Consolas", 9.5f, FontStyle.Bold),
                Text = "SYS --", BackColor = Color.Transparent
            };
            Controls.Add(_lblCpu);
            Controls.Add(_lblGpu);
            Controls.Add(_lblSys);

            MouseEventHandler down = (s, e) => { if (e.Button == MouseButtons.Left) { _drag = true; _dragOffset = e.Location; } };
            MouseEventHandler move = (s, e) => { if (_drag) Location = new Point(Location.X + e.X - _dragOffset.X, Location.Y + e.Y - _dragOffset.Y); };
            MouseEventHandler up = (s, e) => { _drag = false; };
            MouseDown += down; MouseMove += move; MouseUp += up;
            _lblCpu.MouseDown += down; _lblCpu.MouseMove += move; _lblCpu.MouseUp += up;
            _lblGpu.MouseDown += down; _lblGpu.MouseMove += move; _lblGpu.MouseUp += up;
            _lblSys.MouseDown += down; _lblSys.MouseMove += move; _lblSys.MouseUp += up;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000;
                return cp;
            }
        }

        public void SetItems(string cpuText, string gpuText, string sysText,
                             bool showCpu, bool showGpu, bool showSys)
        {
            _lblCpu.Text = cpuText;
            _lblGpu.Text = gpuText;
            _lblSys.Text = sysText;
            _lblCpu.Visible = showCpu;
            _lblGpu.Visible = showGpu;
            _lblSys.Visible = showSys;

            if (_vertical)
			{
				// Hitung lebar konten dulu (max dari ketiga label)
				int contentW = 0;
				if (showCpu && _lblCpu.PreferredWidth > contentW) contentW = _lblCpu.PreferredWidth;
				if (showGpu && _lblGpu.PreferredWidth > contentW) contentW = _lblGpu.PreferredWidth;
				if (showSys && _lblSys.PreferredWidth > contentW) contentW = _lblSys.PreferredWidth;

				int pad = 6;
				int totalW = contentW + pad * 2;
				if (totalW < 60) totalW = 60;
				if (totalW > 800) totalW = 800;

				// Tentukan X awal per label berdasarkan alignment
				Func<Label, int> calcX = lbl =>
				{
					if (_vAlign == 0) return pad;                                   // kiri
					if (_vAlign == 2) return totalW - pad - lbl.PreferredWidth;     // kanan
					return (totalW - lbl.PreferredWidth) / 2;                       // tengah
				};

				int y = 3;
				if (showCpu) { _lblCpu.Location = new Point(calcX(_lblCpu), y); y += _lblCpu.PreferredHeight + 2; }
				if (showGpu) { _lblGpu.Location = new Point(calcX(_lblGpu), y); y += _lblGpu.PreferredHeight + 2; }
				if (showSys) { _lblSys.Location = new Point(calcX(_lblSys), y); y += _lblSys.PreferredHeight + 6; }

				if (y < 22) y = 22;
				if (y > 400) y = 400;
				Size = new Size(totalW, y);
			}
			else
			{
				// Mode horizontal (biarkan sama seperti sebelumnya)
				int x = 6;
				if (showCpu) { _lblCpu.Location = new Point(x, 3); x += _lblCpu.PreferredWidth + 10; }
				if (showGpu) { _lblGpu.Location = new Point(x, 3); x += _lblGpu.PreferredWidth + 10; }
				if (showSys) { _lblSys.Location = new Point(x, 3); x += _lblSys.PreferredWidth + 6; }

				int h = 22;
				if (_lblCpu.PreferredHeight > h) h = _lblCpu.PreferredHeight + 6;
				if (_lblGpu.PreferredHeight > h) h = _lblGpu.PreferredHeight + 6;
				if (_lblSys.PreferredHeight > h) h = _lblSys.PreferredHeight + 6;
				if (x < 80) x = 80;
				if (x > 1100) x = 1100;
				Size = new Size(x, h);
			}
        }

        public void ApplyColors(Color bg, Color cpuFg, Color gpuFg, Color sysFg, int opacityPct)
        {
            BackColor = bg;
            _lblCpu.ForeColor = cpuFg;
            _lblGpu.ForeColor = gpuFg;
            _lblSys.ForeColor = sysFg;
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
			Color.FromArgb(_cfg.OverlaySysColor),
			_cfg.OverlayOpacity); 

			_overlay.Vertical = _cfg.OverlayVertical;	
			_overlay.VAlign = _cfg.OverlayVAlign;			
			
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

			_sensorTimer = new System.Windows.Forms.Timer { Interval = _cfg.SensorIntervalMs };
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
		
		public void ApplyOverlayLayout()
		{
			_overlay.Vertical = _cfg.OverlayVertical;
			_overlay.VAlign = _cfg.OverlayVAlign;
			_overlay.PerformLayout();
			SensorTick();
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
				case "cpu_power": return "CPU Power";
				case "cpu_fan": return "CPU Fan";
				case "cpu_volt": return "CPU Voltage";
				case "gpu_temp": return "GPU Temp";
				case "gpu_freq": return "GPU Freq";
				case "gpu_load": return "GPU Load";
				case "gpu_hotspot": return "GPU Hotspot";
				case "gpu_power": return "GPU Power";
				case "gpu_vram": return "VRAM";
				case "ram": return "RAM";
				case "mb_temp": return "MB Temp";
				case "net_down": return "Net Down";
				case "net_up": return "Net Up";
				case "disk_act": return "Disk Activity";
				case "uptime": return "Uptime";
			}
			return key;
		}

		List<string> GetActiveSensorKeys()
		{
			var list = new List<string>();
			if (_cfg.TrayShowCpuTemp) list.Add("cpu_temp");
			if (_cfg.TrayShowCpuFreq) list.Add("cpu_freq");
			if (_cfg.TrayShowCpuLoad) list.Add("cpu_load");
			if (_cfg.TrayShowCpuPower) list.Add("cpu_power");
			if (_cfg.TrayShowCpuFan) list.Add("cpu_fan");
			if (_cfg.TrayShowCpuVolt) list.Add("cpu_volt");
			if (_cfg.TrayShowGpuTemp) list.Add("gpu_temp");
			if (_cfg.TrayShowGpuFreq) list.Add("gpu_freq");
			if (_cfg.TrayShowGpuLoad) list.Add("gpu_load");
			if (_cfg.TrayShowGpuHotspot) list.Add("gpu_hotspot");
			if (_cfg.TrayShowGpuPower) list.Add("gpu_power");
			if (_cfg.TrayShowGpuVram) list.Add("gpu_vram");
			if (_cfg.TrayShowRam) list.Add("ram");
			if (_cfg.TrayShowMbTemp) list.Add("mb_temp");
			if (_cfg.TrayShowNetDown) list.Add("net_down");
			if (_cfg.TrayShowNetUp) list.Add("net_up");
			if (_cfg.TrayShowDiskAct) list.Add("disk_act");
			if (_cfg.TrayShowUptime) list.Add("uptime");
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
			bool isCpu = key.StartsWith("cpu_");
			bool isGpu = key.StartsWith("gpu_");

			switch (key)
			{
				case "cpu_temp":
					shortVal = s.CpuTempC.HasValue ? s.CpuTempC.Value.ToString("0") + "°" : "--";
					fullVal = s.CpuTempC.HasValue ? s.CpuTempC.Value.ToString("0") + "°C" : "N/A"; break;
				case "cpu_freq":
					shortVal = s.CpuFreqMHz.HasValue ? (s.CpuFreqMHz.Value / 1000f).ToString("0.0") : "--";
					fullVal = s.CpuFreqMHz.HasValue ? (s.CpuFreqMHz.Value / 1000f).ToString("0.00") + " GHz" : "N/A"; break;
				case "cpu_load":
					shortVal = s.CpuLoadPct.HasValue ? s.CpuLoadPct.Value.ToString("0") + "%" : "--";
					fullVal = s.CpuLoadPct.HasValue ? s.CpuLoadPct.Value.ToString("0") + " %" : "N/A"; break;
				case "cpu_power":
					shortVal = s.CpuPowerW.HasValue ? s.CpuPowerW.Value.ToString("0") + "W" : "--";
					fullVal = s.CpuPowerW.HasValue ? s.CpuPowerW.Value.ToString("0.0") + " W" : "N/A"; break;
				case "cpu_fan":
					shortVal = s.CpuFanRpm.HasValue ? s.CpuFanRpm.Value.ToString("0") : "--";
					fullVal = s.CpuFanRpm.HasValue ? s.CpuFanRpm.Value.ToString("0") + " RPM" : "N/A"; break;
				case "cpu_volt":
					shortVal = s.CpuVoltage.HasValue ? s.CpuVoltage.Value.ToString("0.00") : "--";
					fullVal = s.CpuVoltage.HasValue ? s.CpuVoltage.Value.ToString("0.000") + " V" : "N/A"; break;
				case "gpu_temp":
					shortVal = s.GpuTempC.HasValue ? s.GpuTempC.Value.ToString("0") + "°" : "--";
					fullVal = s.GpuTempC.HasValue ? s.GpuTempC.Value.ToString("0") + "°C" : "N/A"; break;
				case "gpu_freq":
					shortVal = s.GpuFreqMHz.HasValue ? (s.GpuFreqMHz.Value / 1000f).ToString("0.0") : "--";
					fullVal = s.GpuFreqMHz.HasValue ? (s.GpuFreqMHz.Value / 1000f).ToString("0.00") + " GHz" : "N/A"; break;
				case "gpu_load":
					shortVal = s.GpuLoadPct.HasValue ? s.GpuLoadPct.Value.ToString("0") + "%" : "--";
					fullVal = s.GpuLoadPct.HasValue ? s.GpuLoadPct.Value.ToString("0") + " %" : "N/A"; break;
				case "gpu_hotspot":
					shortVal = s.GpuHotspotC.HasValue ? s.GpuHotspotC.Value.ToString("0") + "°" : "--";
					fullVal = s.GpuHotspotC.HasValue ? s.GpuHotspotC.Value.ToString("0") + "°C" : "N/A"; break;
				case "gpu_power":
					shortVal = s.GpuPowerW.HasValue ? s.GpuPowerW.Value.ToString("0") + "W" : "--";
					fullVal = s.GpuPowerW.HasValue ? s.GpuPowerW.Value.ToString("0.0") + " W" : "N/A"; break;
				case "gpu_vram":
					shortVal = s.GpuVramUsedMB.HasValue ? (s.GpuVramUsedMB.Value / 1024f).ToString("0.0") : "--";
					fullVal = s.GpuVramUsedMB.HasValue
						? (s.GpuVramUsedMB.Value / 1024f).ToString("0.0") + " / "
						  + (s.GpuVramTotalMB.HasValue ? (s.GpuVramTotalMB.Value / 1024f).ToString("0.0") : "?") + " GB"
						: "N/A"; break;
				case "ram":
					shortVal = s.RamUsedPct.HasValue ? s.RamUsedPct.Value.ToString("0") + "%" : "--";
					fullVal = s.RamUsedPct.HasValue
						? s.RamUsedPct.Value.ToString("0") + " %"
						  + (s.RamUsedGB.HasValue && s.RamTotalGB.HasValue
							 ? " (" + s.RamUsedGB.Value.ToString("0.0") + " / " + s.RamTotalGB.Value.ToString("0.0") + " GB)"
							 : "")
						: "N/A"; break;
				case "mb_temp":
					shortVal = s.MbTempC.HasValue ? s.MbTempC.Value.ToString("0") + "°" : "--";
					fullVal = s.MbTempC.HasValue ? s.MbTempC.Value.ToString("0") + "°C" : "N/A"; break;
				case "net_down":
					shortVal = s.NetDownMbps.HasValue ? FormatNet(s.NetDownMbps.Value) : "--";
					fullVal = s.NetDownMbps.HasValue ? s.NetDownMbps.Value.ToString("0.00") + " Mbps" : "N/A"; break;
				case "net_up":
					shortVal = s.NetUpMbps.HasValue ? FormatNet(s.NetUpMbps.Value) : "--";
					fullVal = s.NetUpMbps.HasValue ? s.NetUpMbps.Value.ToString("0.00") + " Mbps" : "N/A"; break;
				case "disk_act":
					shortVal = s.DiskActivityPct.HasValue ? s.DiskActivityPct.Value.ToString("0") + "%" : "--";
					fullVal = s.DiskActivityPct.HasValue ? s.DiskActivityPct.Value.ToString("0") + " %" : "N/A"; break;
				case "uptime":
					shortVal = FormatUptimeShort(s.Uptime);
					fullVal = FormatUptimeLong(s.Uptime); break;
				default: return;
			}

			Color fg;
			if (isCpu) fg = Color.FromArgb(_cfg.TrayCpuColor);
			else if (isGpu) fg = Color.FromArgb(_cfg.TrayGpuColor);
			else fg = Color.FromArgb(_cfg.TraySysColor);

			var bmp = DrawSingleValueIcon(fg, shortVal);
			var ic = IconFromBitmap(bmp);
			bmp.Dispose();
			ni.Icon = ic;

			string tip = SensorName(key) + ": " + fullVal;
			if (tip.Length > 63) tip = tip.Substring(0, 63);
			ni.Text = tip;
		}

		static string FormatNet(float mbps)
		{
			if (mbps >= 100) return mbps.ToString("0");
			if (mbps >= 10) return mbps.ToString("0.0");
			return mbps.ToString("0.00");
		}

		static string FormatUptimeShort(TimeSpan t)
		{
			if (t.TotalDays >= 1) return t.Days + "d" + t.Hours.ToString("0");
			if (t.TotalHours >= 1) return t.Hours + "h" + t.Minutes.ToString("0");
			return t.Minutes + "m";
		}

		static string FormatUptimeLong(TimeSpan t)
		{
			if (t.TotalDays >= 1) return (int)t.TotalDays + "d " + t.Hours + "h " + t.Minutes + "m";
			if (t.TotalHours >= 1) return t.Hours + "h " + t.Minutes + "m " + t.Seconds + "s";
			return t.Minutes + "m " + t.Seconds + "s";
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

					// ---- deklarasi DI LUAR if agar bisa diakses BeginInvoke ----
					string cpuText = "", gpuText = "", sysText = "";
					bool showCpu = false, showGpu = false, showSys = false;

					if (wantOverlay)
					{
						var cpuParts = new List<string>();
						if (snap.ShowCpuTemp) cpuParts.Add("CPU " + (s.CpuTempC.HasValue ? s.CpuTempC.Value.ToString("0") + "°C" : "--"));
						if (snap.ShowCpuFreq) cpuParts.Add(s.CpuFreqMHz.HasValue ? (s.CpuFreqMHz.Value / 1000f).ToString("0.00") + "GHz" : "----");
						if (snap.ShowCpuLoad) cpuParts.Add(s.CpuLoadPct.HasValue ? s.CpuLoadPct.Value.ToString("0") + "%" : "--");
						if (snap.ShowCpuPower) cpuParts.Add(s.CpuPowerW.HasValue ? s.CpuPowerW.Value.ToString("0") + "W" : "--");
						if (snap.ShowCpuFan) cpuParts.Add(s.CpuFanRpm.HasValue ? s.CpuFanRpm.Value.ToString("0") + "RPM" : "--");
						if (snap.ShowCpuVolt) cpuParts.Add(s.CpuVoltage.HasValue ? s.CpuVoltage.Value.ToString("0.00") + "V" : "--");
						cpuText = string.Join(" ", cpuParts);
						showCpu = cpuParts.Count > 0;

						var gpuParts = new List<string>();
						if (snap.ShowGpuTemp) gpuParts.Add("GPU " + (s.GpuTempC.HasValue ? s.GpuTempC.Value.ToString("0") + "°C" : "--"));
						if (snap.ShowGpuFreq) gpuParts.Add(s.GpuFreqMHz.HasValue ? (s.GpuFreqMHz.Value / 1000f).ToString("0.00") + "GHz" : "----");
						if (snap.ShowGpuLoad) gpuParts.Add(s.GpuLoadPct.HasValue ? s.GpuLoadPct.Value.ToString("0") + "%" : "--");
						if (snap.ShowGpuHotspot) gpuParts.Add("HS " + (s.GpuHotspotC.HasValue ? s.GpuHotspotC.Value.ToString("0") + "°C" : "--"));
						if (snap.ShowGpuPower) gpuParts.Add(s.GpuPowerW.HasValue ? s.GpuPowerW.Value.ToString("0") + "W" : "--");
						if (snap.ShowGpuVram) gpuParts.Add(s.GpuVramUsedMB.HasValue ? "VRAM " + (s.GpuVramUsedMB.Value / 1024f).ToString("0.0") + "G" : "VRAM --");
						gpuText = string.Join(" ", gpuParts);
						showGpu = gpuParts.Count > 0;

						var sysParts = new List<string>();
						if (snap.ShowRam) sysParts.Add("RAM " + (s.RamUsedPct.HasValue ? s.RamUsedPct.Value.ToString("0") + "%" : "--"));
						if (snap.ShowMbTemp) sysParts.Add("MB " + (s.MbTempC.HasValue ? s.MbTempC.Value.ToString("0") + "°C" : "--"));
						if (snap.ShowNetDown) sysParts.Add("↓" + (s.NetDownMbps.HasValue ? s.NetDownMbps.Value.ToString("0.0") + "M" : "--"));
						if (snap.ShowNetUp) sysParts.Add("↑" + (s.NetUpMbps.HasValue ? s.NetUpMbps.Value.ToString("0.0") + "M" : "--"));
						if (snap.ShowDiskAct) sysParts.Add("Disk " + (s.DiskActivityPct.HasValue ? s.DiskActivityPct.Value.ToString("0") + "%" : "--"));
						if (snap.ShowUptime) sysParts.Add(FormatUptimeLong(s.Uptime));
						sysText = string.Join(" ", sysParts);            // <-- TANPA "string"
						showSys = sysParts.Count > 0;                    // <-- TANPA "bool"

						if (!showCpu && !showGpu && !showSys) { cpuText = "CPU --°C"; showCpu = true; }
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
									if (wantOverlay) _overlay.SetItems(cpuText, gpuText, sysText, showCpu, showGpu, showSys);

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
		
		//set sensor interval
		public void SetSensorInterval(int ms)
		{
			if (ms < 500) ms = 500;
			if (ms > 10000) ms = 10000;
			_cfg.SensorIntervalMs = ms;
			_cfg.Save();
			_sensorTimer.Interval = ms;
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
		CheckBox _chkOverlayEnabled;
        CheckBox _chkCpuTemp, _chkCpuFreq, _chkCpuLoad, _chkCpuPower, _chkCpuFan, _chkCpuVolt;
		CheckBox _chkGpuTemp, _chkGpuFreq, _chkGpuLoad, _chkGpuHotspot, _chkGpuPower, _chkGpuVram;
        NumericUpDown _numInterval;
        ListBox _lstTriggers;
        
        //Panel _bgPreview, _fgPreview;
        TextBox _txtHotkey;

        public MainForm(Config cfg, TaskbarOverlay overlay, TrayContext ctx)
        {
            _cfg = cfg;
            _overlay = overlay;
            _ctx = ctx;

            Text = "Turbo Boost Control";
            Width = 540; Height = 720;
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

			tpOver.AutoScroll = true;

			tpOver.Controls.Add(new Label { Text = "Item yang ditampilkan:", Left = 12, Top = 44, Width = 200, Height = 20 });

			// CPU group
			var grpCpu = new GroupBox { Text = "CPU", Left = 12, Top = 66, Width = 220, Height = 170 };
			tpOver.Controls.Add(grpCpu);
			_chkCpuTemp  = MakeCheck(grpCpu, "CPU Temp",  16, _cfg.ShowCpuTemp,  v => { _cfg.ShowCpuTemp = v;  _cfg.Save(); });
			_chkCpuFreq  = MakeCheck(grpCpu, "CPU Freq",  38, _cfg.ShowCpuFreq,  v => { _cfg.ShowCpuFreq = v;  _cfg.Save(); });
			_chkCpuLoad  = MakeCheck(grpCpu, "CPU Load",  60, _cfg.ShowCpuLoad,  v => { _cfg.ShowCpuLoad = v;  _cfg.Save(); });
			_chkCpuPower = MakeCheck(grpCpu, "CPU Power", 82, _cfg.ShowCpuPower, v => { _cfg.ShowCpuPower = v; _cfg.Save(); });
			_chkCpuFan   = MakeCheck(grpCpu, "CPU Fan",  104, _cfg.ShowCpuFan,   v => { _cfg.ShowCpuFan = v;   _cfg.Save(); });
			_chkCpuVolt  = MakeCheck(grpCpu, "CPU Volt", 126, _cfg.ShowCpuVolt,  v => { _cfg.ShowCpuVolt = v;  _cfg.Save(); });

			// GPU group
			var grpGpu = new GroupBox { Text = "GPU", Left = 240, Top = 66, Width = 220, Height = 170 };
			tpOver.Controls.Add(grpGpu);
			_chkGpuTemp    = MakeCheck(grpGpu, "GPU Temp",    16, _cfg.ShowGpuTemp,    v => { _cfg.ShowGpuTemp = v;    _cfg.Save(); });
			_chkGpuFreq    = MakeCheck(grpGpu, "GPU Freq",    38, _cfg.ShowGpuFreq,    v => { _cfg.ShowGpuFreq = v;    _cfg.Save(); });
			_chkGpuLoad    = MakeCheck(grpGpu, "GPU Load",    60, _cfg.ShowGpuLoad,    v => { _cfg.ShowGpuLoad = v;    _cfg.Save(); });
			_chkGpuHotspot = MakeCheck(grpGpu, "GPU Hotspot", 82, _cfg.ShowGpuHotspot, v => { _cfg.ShowGpuHotspot = v; _cfg.Save(); });
			_chkGpuPower   = MakeCheck(grpGpu, "GPU Power",  104, _cfg.ShowGpuPower,   v => { _cfg.ShowGpuPower = v;   _cfg.Save(); });
			_chkGpuVram    = MakeCheck(grpGpu, "VRAM",       126, _cfg.ShowGpuVram,    v => { _cfg.ShowGpuVram = v;    _cfg.Save(); });

			// System group
			var grpSys = new GroupBox { Text = "System", Left = 12, Top = 244, Width = 448, Height = 130 };
			tpOver.Controls.Add(grpSys);
			// 3 kolom
			Action<string, bool, int, int, Action<bool>> mkSysCheck = (text, val, left, top, onChange) =>
			{
				var cb = new CheckBox { Text = text, Left = left, Top = top, Width = 130, Height = 22, Checked = val };
				cb.CheckedChanged += (s, e) => onChange(cb.Checked);
				grpSys.Controls.Add(cb);
			};
			mkSysCheck("RAM",       _cfg.ShowRam,       10,  16, v => { _cfg.ShowRam = v;       _cfg.Save(); });
			mkSysCheck("MB Temp",   _cfg.ShowMbTemp,    10,  38, v => { _cfg.ShowMbTemp = v;   _cfg.Save(); });
			mkSysCheck("Net ↓",     _cfg.ShowNetDown,   10,  60, v => { _cfg.ShowNetDown = v;  _cfg.Save(); });
			mkSysCheck("Net ↑",     _cfg.ShowNetUp,     10,  82, v => { _cfg.ShowNetUp = v;    _cfg.Save(); });
			mkSysCheck("Disk Act",  _cfg.ShowDiskAct,  150,  16, v => { _cfg.ShowDiskAct = v;  _cfg.Save(); });
			mkSysCheck("Uptime",    _cfg.ShowUptime,   150,  38, v => { _cfg.ShowUptime = v;   _cfg.Save(); });
			
			
			// --- Warna Background overlay ---
			tpOver.Controls.Add(new Label { Text = "Background:", Left = 12, Top = 384, Width = 90, Height = 20 });
			var bgPreview = new Panel
			{
				Left = 108, Top = 382, Width = 30, Height = 22,
				BackColor = Color.FromArgb(_cfg.OverlayBg),
				BorderStyle = BorderStyle.FixedSingle
			};
			tpOver.Controls.Add(bgPreview);
			var btnBg = new Button { Text = "Pilih...", Left = 148, Top = 380, Width = 80, Height = 26 };
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
						Color.FromArgb(_cfg.OverlaySysColor),   // <-- harus ada
						_cfg.OverlayOpacity);
						_cfg.Save();
					}
			};
			tpOver.Controls.Add(btnBg);

			// --- Warna Teks CPU ---
			tpOver.Controls.Add(new Label { Text = "Teks CPU:", Left = 240, Top = 384, Width = 70, Height = 20 });
			var cpuPreview = new Panel
			{
				Left = 316, Top = 382, Width = 30, Height = 22,
				BackColor = Color.FromArgb(_cfg.OverlayCpuColor),
				BorderStyle = BorderStyle.FixedSingle
			};
			tpOver.Controls.Add(cpuPreview);
			var btnCpu = new Button { Text = "Pilih...", Left = 356, Top = 380, Width = 80, Height = 26 };
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
							Color.FromArgb(_cfg.OverlaySysColor),
							_cfg.OverlayOpacity);
						_cfg.Save();
					}
			};
			tpOver.Controls.Add(btnCpu);

			// --- Warna Teks GPU ---
			tpOver.Controls.Add(new Label { Text = "Teks GPU:", Left = 12, Top = 416, Width = 90, Height = 20 });
			var gpuPreview = new Panel
			{
				Left = 108, Top = 414, Width = 30, Height = 22,
				BackColor = Color.FromArgb(_cfg.OverlayGpuColor),
				BorderStyle = BorderStyle.FixedSingle
			};
			tpOver.Controls.Add(gpuPreview);
			var btnGpu = new Button { Text = "Pilih...", Left = 148, Top = 412, Width = 80, Height = 26 };
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
							Color.FromArgb(_cfg.OverlaySysColor),
							_cfg.OverlayOpacity);
						_cfg.Save();
					}
			};
			tpOver.Controls.Add(btnGpu);

			// --- Opacity slider ---
			tpOver.Controls.Add(new Label { Text = "Opacity:", Left = 240, Top = 416, Width = 70, Height = 20 });
			var trackOpacity = new TrackBar
			{
				Left = 310, Top = 410, Width = 120, Height = 32,
				Minimum = 10, Maximum = 100, TickFrequency = 10,
				Value = _cfg.OverlayOpacity
			};
			var lblOpacity = new Label
			{
				Text = _cfg.OverlayOpacity + "%", Left = 434, Top = 418, Width = 40, Height = 20
			};
			trackOpacity.ValueChanged += (s, e) =>
			{
				_cfg.OverlayOpacity = trackOpacity.Value;
				lblOpacity.Text = trackOpacity.Value + "%";
				_overlay.ApplyColors(
					Color.FromArgb(_cfg.OverlayBg),
					Color.FromArgb(_cfg.OverlayCpuColor),
					Color.FromArgb(_cfg.OverlayGpuColor),
					Color.FromArgb(_cfg.OverlaySysColor),
					_cfg.OverlayOpacity);
				_cfg.Save();
			};
			tpOver.Controls.Add(trackOpacity);
			tpOver.Controls.Add(lblOpacity);

			// --- Warna Teks System ---
			tpOver.Controls.Add(new Label { Text = "Teks System:", Left = 12, Top = 448, Width = 100, Height = 20 });
			var sysOverPreview = new Panel
			{
				Left = 118, Top = 446, Width = 30, Height = 22,
				BackColor = Color.FromArgb(_cfg.OverlaySysColor),
				BorderStyle = BorderStyle.FixedSingle
			};
			tpOver.Controls.Add(sysOverPreview);
			var btnSysOver = new Button { Text = "Pilih...", Left = 158, Top = 444, Width = 80, Height = 26 };
			btnSysOver.Click += (s, e) =>
			{
				using (var dlg = new ColorDialog { Color = Color.FromArgb(_cfg.OverlaySysColor) })
					if (dlg.ShowDialog(this) == DialogResult.OK)
					{
						_cfg.OverlaySysColor = dlg.Color.ToArgb();
						sysOverPreview.BackColor = dlg.Color;
						_overlay.ApplyColors(
							Color.FromArgb(_cfg.OverlayBg),
							Color.FromArgb(_cfg.OverlayCpuColor),
							Color.FromArgb(_cfg.OverlayGpuColor),
							Color.FromArgb(_cfg.OverlaySysColor),
							_cfg.OverlayOpacity);
						_cfg.Save();
					}
			};
			tpOver.Controls.Add(btnSysOver);

			// --- Layout ---
			tpOver.Controls.Add(new Label
			{
				Text = "Layout overlay:",
				Left = 12, Top = 480, Width = 100, Height = 20,
				Font = new Font("Segoe UI", 9f, FontStyle.Bold)
			});

			var rbHorizontal = new RadioButton
			{
				Text = "Horizontal (1 baris)",
				Left = 20, Top = 504, Width = 180, Height = 22,
				Checked = !_cfg.OverlayVertical
			};
			var rbVertical = new RadioButton
			{
				Text = "Vertical (bertingkat)",
				Left = 20, Top = 528, Width = 180, Height = 22,
				Checked = _cfg.OverlayVertical
			};
			rbHorizontal.CheckedChanged += (s, e) =>
			{
				if (rbHorizontal.Checked)
				{
					_cfg.OverlayVertical = false;
					_cfg.Save();
					_ctx.ApplyOverlayLayout();
				}
			};
			rbVertical.CheckedChanged += (s, e) =>
			{
				if (rbVertical.Checked)
				{
					_cfg.OverlayVertical = true;
					_cfg.Save();
					_ctx.ApplyOverlayLayout();
				}
			};
			tpOver.Controls.Add(rbHorizontal);
			tpOver.Controls.Add(rbVertical);

			tpOver.Controls.Add(new Label
			{
				Text = "Perataan vertikal:",
				Left = 240, Top = 480, Width = 150, Height = 20,
				Font = new Font("Segoe UI", 9f, FontStyle.Bold)
			});

			var rbAlignLeft = new RadioButton
			{
				Text = "Kiri",
				Left = 248, Top = 504, Width = 70, Height = 22,
				Checked = _cfg.OverlayVAlign == 0
			};
			var rbAlignCenter = new RadioButton
			{
				Text = "Tengah",
				Left = 248, Top = 528, Width = 90, Height = 22,
				Checked = _cfg.OverlayVAlign == 1
			};
			var rbAlignRight = new RadioButton
			{
				Text = "Kanan",
				Left = 248, Top = 552, Width = 80, Height = 22,
				Checked = _cfg.OverlayVAlign == 2
			};
			rbAlignLeft.CheckedChanged += (s, e) =>
			{
				if (rbAlignLeft.Checked) { _cfg.OverlayVAlign = 0; _cfg.Save(); _ctx.ApplyOverlayLayout(); }
			};
			rbAlignCenter.CheckedChanged += (s, e) =>
			{
				if (rbAlignCenter.Checked) { _cfg.OverlayVAlign = 1; _cfg.Save(); _ctx.ApplyOverlayLayout(); }
			};
			rbAlignRight.CheckedChanged += (s, e) =>
			{
				if (rbAlignRight.Checked) { _cfg.OverlayVAlign = 2; _cfg.Save(); _ctx.ApplyOverlayLayout(); }
			};
			tpOver.Controls.Add(rbAlignLeft);
			tpOver.Controls.Add(rbAlignCenter);
			tpOver.Controls.Add(rbAlignRight);


			// --- Reset posisi ---
			var btnReset = new Button
			{
				Text = "Reset Posisi ke Pojok Kanan Bawah",
				Left = 12, Top = 590, Width = 260, Height = 28
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
				Left = 12, Top = 625, Width = 430, Height = 40, ForeColor = Color.Gray
			});
			
			// ---------- Tab Tray Icon ----------
			var tpTray = new TabPage("Tray Icon");
			tabs.TabPages.Add(tpTray);
			tpTray.AutoScroll = true;
			
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

			Action<string, bool, int, int, Action<bool>> mkTrayChk = (text, val, left, top, onChange) =>
			{
				var cb = new CheckBox { Text = text, Left = left, Top = top, Width = 100, Height = 22, Checked = val };
				cb.CheckedChanged += (s, e) => { onChange(cb.Checked); _ctx.RebuildSensorIcons(); };
				tpTray.Controls.Add(cb);
			};

			// CPU group
			var trayGrpCpu = new GroupBox { Text = "CPU", Left = 12, Top = 44, Width = 220, Height = 170 };
			tpTray.Controls.Add(trayGrpCpu);
			Action<string, bool, int, Action<bool>> mkCpu = (text, val, top, oc) =>
			{
				var cb = new CheckBox { Text = text, Left = 10, Top = top, Width = 200, Height = 22, Checked = val };
				cb.CheckedChanged += (s, e) => { oc(cb.Checked); _ctx.RebuildSensorIcons(); };
				trayGrpCpu.Controls.Add(cb);
			};
			mkCpu("CPU Temp",   _cfg.TrayShowCpuTemp,  16, v => { _cfg.TrayShowCpuTemp = v;   _cfg.Save(); });
			mkCpu("CPU Freq",   _cfg.TrayShowCpuFreq,  38, v => { _cfg.TrayShowCpuFreq = v;   _cfg.Save(); });
			mkCpu("CPU Load",   _cfg.TrayShowCpuLoad,  60, v => { _cfg.TrayShowCpuLoad = v;   _cfg.Save(); });
			mkCpu("CPU Power",  _cfg.TrayShowCpuPower, 82, v => { _cfg.TrayShowCpuPower = v;  _cfg.Save(); });
			mkCpu("CPU Fan",   _cfg.TrayShowCpuFan,   104, v => { _cfg.TrayShowCpuFan = v;    _cfg.Save(); });
			mkCpu("CPU Volt",  _cfg.TrayShowCpuVolt,  126, v => { _cfg.TrayShowCpuVolt = v;   _cfg.Save(); });

			// GPU group
			var trayGrpGpu = new GroupBox { Text = "GPU", Left = 240, Top = 44, Width = 220, Height = 170 };
			tpTray.Controls.Add(trayGrpGpu);
			Action<string, bool, int, Action<bool>> mkGpu = (text, val, top, oc) =>
			{
				var cb = new CheckBox { Text = text, Left = 10, Top = top, Width = 200, Height = 22, Checked = val };
				cb.CheckedChanged += (s, e) => { oc(cb.Checked); _ctx.RebuildSensorIcons(); };
				trayGrpGpu.Controls.Add(cb);
			};
			mkGpu("GPU Temp",    _cfg.TrayShowGpuTemp,    16, v => { _cfg.TrayShowGpuTemp = v;     _cfg.Save(); });
			mkGpu("GPU Freq",    _cfg.TrayShowGpuFreq,    38, v => { _cfg.TrayShowGpuFreq = v;     _cfg.Save(); });
			mkGpu("GPU Load",    _cfg.TrayShowGpuLoad,    60, v => { _cfg.TrayShowGpuLoad = v;     _cfg.Save(); });
			mkGpu("GPU Hotspot", _cfg.TrayShowGpuHotspot, 82, v => { _cfg.TrayShowGpuHotspot = v;  _cfg.Save(); });
			mkGpu("GPU Power",   _cfg.TrayShowGpuPower,  104, v => { _cfg.TrayShowGpuPower = v;    _cfg.Save(); });
			mkGpu("VRAM",        _cfg.TrayShowGpuVram,   126, v => { _cfg.TrayShowGpuVram = v;     _cfg.Save(); });

			// System group
			var trayGrpSys = new GroupBox { Text = "System", Left = 12, Top = 222, Width = 448, Height = 130 };
			tpTray.Controls.Add(trayGrpSys);
			Action<string, bool, int, int, Action<bool>> mkSys = (text, val, left, top, oc) =>
			{
				var cb = new CheckBox { Text = text, Left = left, Top = top, Width = 130, Height = 22, Checked = val };
				cb.CheckedChanged += (s, e) => { oc(cb.Checked); _ctx.RebuildSensorIcons(); };
				trayGrpSys.Controls.Add(cb);
			};
			mkSys("RAM",       _cfg.TrayShowRam,       10,  16, v => { _cfg.TrayShowRam = v;        _cfg.Save(); });
			mkSys("MB Temp",   _cfg.TrayShowMbTemp,    10,  38, v => { _cfg.TrayShowMbTemp = v;    _cfg.Save(); });
			mkSys("Net ↓",     _cfg.TrayShowNetDown,   10,  60, v => { _cfg.TrayShowNetDown = v;   _cfg.Save(); });
			mkSys("Net ↑",     _cfg.TrayShowNetUp,     10,  82, v => { _cfg.TrayShowNetUp = v;     _cfg.Save(); });
			mkSys("Disk Act",  _cfg.TrayShowDiskAct,  150,  16, v => { _cfg.TrayShowDiskAct = v;   _cfg.Save(); });
			mkSys("Uptime",    _cfg.TrayShowUptime,   150,  38, v => { _cfg.TrayShowUptime = v;    _cfg.Save(); });

			tpTray.Controls.Add(new Label { Text = "SYS:", Left = 20, Top = 272, Width = 40, Height = 20 });
			var sysPreview = new Panel
			{
				Left = 66, Top = 270, Width = 30, Height = 22,
				BackColor = Color.FromArgb(_cfg.TraySysColor),
				BorderStyle = BorderStyle.FixedSingle
			};
			tpTray.Controls.Add(sysPreview);
			var btnSysColor = new Button { Text = "Pilih...", Left = 106, Top = 268, Width = 80, Height = 26 };
			btnSysColor.Click += (s, e) =>
			{
				using (var dlg = new ColorDialog { Color = Color.FromArgb(_cfg.TraySysColor), FullOpen = true })
					if (dlg.ShowDialog(this) == DialogResult.OK)
					{
						_cfg.TraySysColor = dlg.Color.ToArgb();
						sysPreview.BackColor = dlg.Color;
						_cfg.Save();
					}
			};
			tpTray.Controls.Add(btnSysColor);


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
			
			tpBeh.Controls.Add(new Label
			{
				Text = "Interval update sensor (ms):",
				Left = 12, Top = 140, Width = 180, Height = 20,
				Font = new Font("Segoe UI", 9f, FontStyle.Bold)
			});

			var numSensorInterval = new NumericUpDown
			{
				Left = 200, Top = 138, Width = 100,
				Minimum = 500, Maximum = 10000,
				Increment = 100,
				Value = _cfg.SensorIntervalMs
			};
			numSensorInterval.ValueChanged += (s, e) =>
			{
				_ctx.SetSensorInterval((int)numSensorInterval.Value);
			};
			tpBeh.Controls.Add(numSensorInterval);

			tpBeh.Controls.Add(new Label
			{
				Text = "Range: 500 – 10000 ms. Default 1000.\n" +
					   "Naikkan kalau CPU usage TurboToggle > 1%.\n" +
					   "Turunkan kalau ingin update lebih cepat (sensor lebih reaktif).",
				Left = 12, Top = 168, Width = 430, Height = 60,
				ForeColor = Color.Gray
			});


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