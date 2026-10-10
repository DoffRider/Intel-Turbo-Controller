using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace TurboToggle
{
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

        public static float GetCpuLoadPct()
        {
            Init();
            if (_cpuLoad == null) return 0f;
            try { return _cpuLoad.NextValue(); } catch { return 0f; }
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
                    IsMotherboardEnabled = true,
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

            if (_cpuFreqPct != null && _cpuBaseMHz > 0)
            {
                try { s.CpuFreqMHz = _cpuBaseMHz * _cpuFreqPct.NextValue() / 100f; } catch { }
            }
            if (_cpuLoad != null)
            {
                try { s.CpuLoadPct = _cpuLoad.NextValue(); } catch { }
            }

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
                            }
                        }
                        else if (isMb)
                        {
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
                                        if (!s.MbTempC.HasValue && v > 20 && v < 80) s.MbTempC = v;
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

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
                        double dRx = (totalRx - _lastNetRx) / sec / 1024.0 / 1024.0 * 8;
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

            if (_diskAct != null)
            {
                try { s.DiskActivityPct = _diskAct.NextValue(); } catch { }
            }

            try { s.Uptime = TimeSpan.FromMilliseconds(GetTickCount64()); } catch { }

            return s;
        }
    }
}