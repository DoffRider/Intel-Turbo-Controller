using System;

namespace TurboToggle
{
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
}