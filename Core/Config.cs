using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TurboToggle
{
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

        public int OverlayOpacity = 85;
        public int OverlayCpuColor = unchecked((int)0xFF00FF88);
        public int OverlayGpuColor = unchecked((int)0xFF00BFFF);

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

        public int TrayCpuColor = unchecked((int)0xFF00FF88);
        public int TrayGpuColor = unchecked((int)0xFF00BFFF);

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

        public int TraySysColor = unchecked((int)0xFFFFAA00);
        public int OverlaySysColor = unchecked((int)0xFFFFAA00);

        // --- Gaya visual ikon tray ---
        public string TrayFontFamily = "Segoe UI";
        public int TrayFontStyle = 1;          // 0=Regular, 1=Bold, 2=Italic, 3=Bold Italic
        public int TrayIconSize = 128;         // 32 / 64 / 128 / 192 / 256
        public int TrayMarginPct = 2;          // 0-20 (%)
        public int TrayOutlineWidth = 0;       // 0 = off, dalam px
        public int TrayOutlineColor = unchecked((int)0xB0000000);
        public bool TrayUseGradient = false;
        public int TrayGradientStrength = 20;  // 0-60 (%)

        public bool OverlayVertical = false;
        public int OverlayVAlign = 1;

        public bool OverlayAnchorRight = true;
        public bool OverlayAnchorBottom = true;
        public int OverlayOffsetX = -8;
        public int OverlayOffsetY = -4;

        public int SensorIntervalMs = 1000;

        // --- Alert Suhu ---
        public bool AlertEnabled = false;
        public int AlertCpuTemp = 85;         // °C
        public int AlertGpuTemp = 80;         // °C
        public int AlertGpuHotspot = 95;      // °C
        public int AlertCooldownSec = 60;     // detik
        public bool AlertPlaySound = true;
        public bool AlertShowToast = true;
        public string AlertCpuSoundPath = "";
        public string AlertGpuSoundPath = "";
        public string AlertHotspotSoundPath = "";
        
        // --- Log Sensor ---
        public bool LogEnabled = false;
        public string LogFolder = "";
        public int LogIntervalMs = 5000;
        public bool LogAutoRotateDaily = true;
        // --- Idle Auto-Disable ---
        public bool IdleAutoDisableEnabled = false;
        public int IdleLoadThresholdPct = 10;
        public int IdleDurationSec = 120;
        public int ActiveLoadThresholdPct = 30;
        public int ActiveDurationSec = 5;
        public bool IdleShowToast = true;

        // --- Temporary Boost ---
        public int BoostDefaultMinutes = 30;
        public bool BoostRestorePrevious = false;
        public bool BoostShowCountdown = true;

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
                    c.HotkeyVk = 0x54;
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

                c.TrayFontFamily = G("TrayFontFamily", "Segoe UI");
                if (string.IsNullOrEmpty(c.TrayFontFamily)) c.TrayFontFamily = "Segoe UI";
                c.TrayFontStyle = GI("TrayFontStyle", 1);
                if (c.TrayFontStyle < 0 || c.TrayFontStyle > 3) c.TrayFontStyle = 1;
                c.TrayIconSize = GI("TrayIconSize", 128);
                if (c.TrayIconSize < 32) c.TrayIconSize = 32;
                if (c.TrayIconSize > 512) c.TrayIconSize = 512;
                c.TrayMarginPct = GI("TrayMarginPct", 2);
                if (c.TrayMarginPct < 0) c.TrayMarginPct = 0;
                if (c.TrayMarginPct > 20) c.TrayMarginPct = 20;
                c.TrayOutlineWidth = GI("TrayOutlineWidth", 0);
                if (c.TrayOutlineWidth < 0) c.TrayOutlineWidth = 0;
                if (c.TrayOutlineWidth > 40) c.TrayOutlineWidth = 40;
                c.TrayOutlineColor = GI("TrayOutlineColor", unchecked((int)0xB0000000));
                c.TrayUseGradient = GB("TrayUseGradient", false);
                c.TrayGradientStrength = GI("TrayGradientStrength", 20);
                if (c.TrayGradientStrength < 0) c.TrayGradientStrength = 0;
                if (c.TrayGradientStrength > 60) c.TrayGradientStrength = 60;

                c.OverlayVertical = GB("OverlayVertical", false);
                c.OverlayVAlign = GI("OverlayVAlign", 1);

                c.OverlayAnchorRight = GB("OverlayAnchorRight", true);
                c.OverlayAnchorBottom = GB("OverlayAnchorBottom", true);
                c.OverlayOffsetX = GI("OverlayOffsetX", -8);
                c.OverlayOffsetY = GI("OverlayOffsetY", -4);

                c.SensorIntervalMs = GI("SensorIntervalMs", 1000);
                if (c.SensorIntervalMs < 500) c.SensorIntervalMs = 500;
                if (c.SensorIntervalMs > 10000) c.SensorIntervalMs = 10000;

                c.AlertEnabled = GB("AlertEnabled", false);
                c.AlertCpuTemp = GI("AlertCpuTemp", 85);
                if (c.AlertCpuTemp < 30) c.AlertCpuTemp = 30;
                if (c.AlertCpuTemp > 120) c.AlertCpuTemp = 120;
                c.AlertGpuTemp = GI("AlertGpuTemp", 80);
                if (c.AlertGpuTemp < 30) c.AlertGpuTemp = 30;
                if (c.AlertGpuTemp > 120) c.AlertGpuTemp = 120;
                c.AlertGpuHotspot = GI("AlertGpuHotspot", 95);
                if (c.AlertGpuHotspot < 30) c.AlertGpuHotspot = 30;
                if (c.AlertGpuHotspot > 130) c.AlertGpuHotspot = 130;
                c.AlertCooldownSec = GI("AlertCooldownSec", 60);
                if (c.AlertCooldownSec < 5) c.AlertCooldownSec = 5;
                if (c.AlertCooldownSec > 3600) c.AlertCooldownSec = 3600;
                c.AlertPlaySound = GB("AlertPlaySound", true);
                c.AlertShowToast = GB("AlertShowToast", true);

                c.AlertCpuSoundPath = G("AlertCpuSoundPath", "");
                c.AlertGpuSoundPath = G("AlertGpuSoundPath", "");
                c.AlertHotspotSoundPath = G("AlertHotspotSoundPath", "");

                c.LogEnabled = GB("LogEnabled", false);
                c.LogFolder = G("LogFolder", "");
                if (string.IsNullOrEmpty(c.LogFolder))
                {
                    c.LogFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "TurboToggle Logs");
                }
                c.LogIntervalMs = GI("LogIntervalMs", 5000);
                if (c.LogIntervalMs < 500) c.LogIntervalMs = 500;
                if (c.LogIntervalMs > 60000) c.LogIntervalMs = 60000;
                c.LogAutoRotateDaily = GB("LogAutoRotateDaily", true);

                c.IdleAutoDisableEnabled = GB("IdleAutoDisableEnabled", false);
                c.IdleLoadThresholdPct = GI("IdleLoadThresholdPct", 10);
                if (c.IdleLoadThresholdPct < 1) c.IdleLoadThresholdPct = 1;
                if (c.IdleLoadThresholdPct > 90) c.IdleLoadThresholdPct = 90;
                c.IdleDurationSec = GI("IdleDurationSec", 120);
                if (c.IdleDurationSec < 10) c.IdleDurationSec = 10;
                if (c.IdleDurationSec > 3600) c.IdleDurationSec = 3600;
                c.ActiveLoadThresholdPct = GI("ActiveLoadThresholdPct", 30);
                if (c.ActiveLoadThresholdPct < 5) c.ActiveLoadThresholdPct = 5;
                if (c.ActiveLoadThresholdPct > 100) c.ActiveLoadThresholdPct = 100;
                c.ActiveDurationSec = GI("ActiveDurationSec", 5);
                if (c.ActiveDurationSec < 1) c.ActiveDurationSec = 1;
                if (c.ActiveDurationSec > 600) c.ActiveDurationSec = 600;

                c.IdleShowToast = GB("IdleShowToast", true);

                c.BoostDefaultMinutes = GI("BoostDefaultMinutes", 30);
                if (c.BoostDefaultMinutes < 1) c.BoostDefaultMinutes = 1;
                if (c.BoostDefaultMinutes > 1440) c.BoostDefaultMinutes = 1440;
                c.BoostRestorePrevious = GB("BoostRestorePrevious", false);
                c.BoostShowCountdown = GB("BoostShowCountdown", true);

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

                sb.AppendLine("OverlayAnchorRight=" + (OverlayAnchorRight ? "true" : "false"));
                sb.AppendLine("OverlayAnchorBottom=" + (OverlayAnchorBottom ? "true" : "false"));
                sb.AppendLine("OverlayOffsetX=" + OverlayOffsetX);
                sb.AppendLine("OverlayOffsetY=" + OverlayOffsetY);

                sb.AppendLine("TraySysColor=" + TraySysColor);
                sb.AppendLine("OverlaySysColor=" + OverlaySysColor);

                sb.AppendLine("TrayFontFamily=" + TrayFontFamily);
                sb.AppendLine("TrayFontStyle=" + TrayFontStyle);
                sb.AppendLine("TrayIconSize=" + TrayIconSize);
                sb.AppendLine("TrayMarginPct=" + TrayMarginPct);
                sb.AppendLine("TrayOutlineWidth=" + TrayOutlineWidth);
                sb.AppendLine("TrayOutlineColor=" + TrayOutlineColor);
                sb.AppendLine("TrayUseGradient=" + (TrayUseGradient ? "true" : "false"));
                sb.AppendLine("TrayGradientStrength=" + TrayGradientStrength);

                sb.AppendLine("SensorIntervalMs=" + SensorIntervalMs);

                sb.AppendLine("AlertEnabled=" + (AlertEnabled ? "true" : "false"));
                sb.AppendLine("AlertCpuTemp=" + AlertCpuTemp);
                sb.AppendLine("AlertGpuTemp=" + AlertGpuTemp);
                sb.AppendLine("AlertGpuHotspot=" + AlertGpuHotspot);
                sb.AppendLine("AlertCooldownSec=" + AlertCooldownSec);
                sb.AppendLine("AlertPlaySound=" + (AlertPlaySound ? "true" : "false"));
                sb.AppendLine("AlertShowToast=" + (AlertShowToast ? "true" : "false"));

                sb.AppendLine("AlertCpuSoundPath=" + AlertCpuSoundPath);
                sb.AppendLine("AlertGpuSoundPath=" + AlertGpuSoundPath);
                sb.AppendLine("AlertHotspotSoundPath=" + AlertHotspotSoundPath);

                sb.AppendLine("LogEnabled=" + (LogEnabled ? "true" : "false"));
                sb.AppendLine("LogFolder=" + LogFolder);
                sb.AppendLine("LogIntervalMs=" + LogIntervalMs);
                sb.AppendLine("LogAutoRotateDaily=" + (LogAutoRotateDaily ? "true" : "false"));

                sb.AppendLine("IdleAutoDisableEnabled=" + (IdleAutoDisableEnabled ? "true" : "false"));
                sb.AppendLine("IdleLoadThresholdPct=" + IdleLoadThresholdPct);
                sb.AppendLine("IdleDurationSec=" + IdleDurationSec);
                sb.AppendLine("ActiveLoadThresholdPct=" + ActiveLoadThresholdPct);
                sb.AppendLine("ActiveDurationSec=" + ActiveDurationSec);

                sb.AppendLine("IdleShowToast=" + (IdleShowToast ? "true" : "false"));

                sb.AppendLine("BoostDefaultMinutes=" + BoostDefaultMinutes);
                sb.AppendLine("BoostRestorePrevious=" + (BoostRestorePrevious ? "true" : "false"));
                sb.AppendLine("BoostShowCountdown=" + (BoostShowCountdown ? "true" : "false"));

                File.WriteAllText(PathFile, sb.ToString());
            }
            catch { }
        }
    }
}