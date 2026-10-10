using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TurboToggle
{
    class TrayContext : ApplicationContext
    {
        readonly NotifyIcon _icon;
        readonly Config _cfg;
        readonly System.Windows.Forms.Timer _autoTimer, _sensorTimer, _topMostTimer;
        readonly MainForm _form;
        readonly TaskbarOverlay _overlay;
        readonly HotkeyManager _hotkey;
        readonly Dictionary<string, NotifyIcon> _sensorIcons = new Dictionary<string, NotifyIcon>();
        readonly Dictionary<string, string> _lastIconSignature = new Dictionary<string, string>();
        readonly Dictionary<string, DateTime> _lastIconUpdateTime = new Dictionary<string, DateTime>();

        DateTime _lastAlertTime = DateTime.MinValue;
        string _lastAlertSource = "";

        DateTime _idleSince = DateTime.MinValue;
        DateTime _activeSince = DateTime.MinValue;
        DateTime _idleOverrideUntil = DateTime.MinValue;

        // --- Temporary Boost ---
        DateTime _boostActiveUntil = DateTime.MinValue;
        DateTime _boostStartTime = DateTime.MinValue;
        bool _boostPreviousState = true;
        int _boostTotalMinutes = 0;
        int _lastCountdownMinute = -1;
        readonly System.Windows.Forms.Timer _boostTimer;
        readonly ToolStripMenuItem _menuBoost;
        readonly ToolStripMenuItem _menuBoostCancel;

        System.Media.SoundPlayer _alertSoundPlayer;

        readonly System.Windows.Forms.Timer _logTimer;
        System.IO.StreamWriter _logWriter;
        DateTime _currentLogDay = DateTime.MinValue;
        string _currentLogPath = "";
        long _logLineCount = 0;

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

            if (_cfg.OverlayEnabled) _overlay.Show();

            _overlay.PositionChanged += () =>
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                int cx = _overlay.Location.X + _overlay.Width / 2;
                int cy = _overlay.Location.Y + _overlay.Height / 2;
                _cfg.OverlayAnchorRight = cx >= wa.Left + wa.Width / 2;
                _cfg.OverlayAnchorBottom = cy >= wa.Top + wa.Height / 2;
                if (_cfg.OverlayAnchorRight)
                    _cfg.OverlayOffsetX = _overlay.Location.X + _overlay.Width - wa.Right;
                else
                    _cfg.OverlayOffsetX = _overlay.Location.X - wa.Left;
                if (_cfg.OverlayAnchorBottom)
                    _cfg.OverlayOffsetY = _overlay.Location.Y + _overlay.Height - wa.Bottom;
                else
                    _cfg.OverlayOffsetY = _overlay.Location.Y - wa.Top;
                _cfg.Save();
            };

            _overlay.AfterResize += ReapplyOverlayAnchor;
            ReapplyOverlayAnchor();

            _hotkey = new HotkeyManager();
            RegisterHotkeyFromConfig();

            _form = new MainForm(_cfg, _overlay, this);
            var _forceHandle = _form.Handle;
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

            _menu.Items.Add("Reset Idle Timer", null, (s, e) =>
            {
                _idleOverrideUntil = DateTime.Now.AddMinutes(5);
                _idleSince = DateTime.MinValue;
                _activeSince = DateTime.MinValue;
                ShowToast("Turbo Toggle", "Idle timer di-reset (pause 5 menit)");
            });

            _menuBoost = new ToolStripMenuItem("Temporary Boost");
            _menuBoost.DropDownItems.Add("5 menit", null, (s, e) => StartTemporaryBoost(5));
            _menuBoost.DropDownItems.Add("15 menit", null, (s, e) => StartTemporaryBoost(15));
            _menuBoost.DropDownItems.Add("30 menit", null, (s, e) => StartTemporaryBoost(30));
            _menuBoost.DropDownItems.Add("45 menit", null, (s, e) => StartTemporaryBoost(45));
            _menuBoost.DropDownItems.Add("60 menit", null, (s, e) => StartTemporaryBoost(60));
            _menuBoost.DropDownItems.Add("120 menit", null, (s, e) => StartTemporaryBoost(120));
            _menuBoost.DropDownItems.Add(new ToolStripSeparator());
            _menuBoost.DropDownItems.Add("Custom...", null, (s, e) => ShowCustomBoostDialog());

            _menuBoostCancel = new ToolStripMenuItem("Cancel Boost");
            _menuBoostCancel.Visible = false;
            _menuBoostCancel.Click += (s, e) => CancelTemporaryBoost();

            _menu.Items.Add(_menuBoost);
            _menu.Items.Add(_menuBoostCancel);

            _menu.Items.Add("Start/Stop Log", null, (s, e) =>
            {
                _cfg.LogEnabled = !_cfg.LogEnabled;
                _cfg.Save();
                ApplyLogSetting();
                _form.RefreshLogStatus();
                try
                {
                    ShowToast("Turbo Toggle", "Log: " + (_cfg.LogEnabled ? "AKTIF" : "nonaktif"));
                }
                catch { }
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

            //semakin kecil maka semakin memakan resource, tapi cepat
            _topMostTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _topMostTimer.Tick += (s, e) =>
            {
                if (_cfg.OverlayEnabled && _overlay.Visible)
                    _overlay.ForceTopMost();
            };
            _topMostTimer.Start();

            _logTimer = new System.Windows.Forms.Timer { Interval = _cfg.LogIntervalMs };
            _logTimer.Tick += (s, e) => LogTick();
            if (_cfg.LogEnabled) StartLogging();

            _boostTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _boostTimer.Tick += (s, e) => BoostTick();
            _boostTimer.Start();

            SensorTick();
            UpdateIcon();

            bool launchedAtStartup = args.Any(a =>
                a.Equals("--startup", StringComparison.OrdinalIgnoreCase));

            if (_cfg.ShowFormOnStartup && !launchedAtStartup)
            {
                var t = new System.Windows.Forms.Timer { Interval = 800 };
                t.Tick += (s2, e2) => { t.Stop(); t.Dispose(); ShowForm(); };
                t.Start();
            }
        }

        public void ApplyOverlayLayout()
        {
            _overlay.Vertical = _cfg.OverlayVertical;
            _overlay.VAlign = _cfg.OverlayVAlign;
            _overlay.PerformLayout();
            SensorTick();
            ReapplyOverlayAnchor();
        }

        void ReapplyOverlayAnchor()
        {
            if (!_cfg.OverlayEnabled) return;
            if (!_overlay.Visible) return;
            try
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                int x = _cfg.OverlayAnchorRight
                    ? (wa.Right - _overlay.Width + _cfg.OverlayOffsetX)
                    : (wa.Left + _cfg.OverlayOffsetX);
                int y = _cfg.OverlayAnchorBottom
                    ? (wa.Bottom - _overlay.Height + _cfg.OverlayOffsetY)
                    : (wa.Top + _cfg.OverlayOffsetY);
                _overlay.Location = new Point(x, y);
            }
            catch { }
        }

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
            // Kalau tray info dimatikan → hapus semua
            if (!_cfg.TrayInfoEnabled)
            {
                foreach (var kv in _sensorIcons)
                {
                    try { kv.Value.Visible = false; kv.Value.Dispose(); } catch { }
                }
                _sensorIcons.Clear();
                return;
            }

            var wantedKeys = GetActiveSensorKeys();

            // 1) Hapus ikon yang tidak lagi diminta (uncheck)
            var toRemove = new List<string>();
            foreach (var key in _sensorIcons.Keys)
            {
                if (!wantedKeys.Contains(key)) toRemove.Add(key);
            }
            foreach (var key in toRemove)
            {
                try { _sensorIcons[key].Visible = false; _sensorIcons[key].Dispose(); } catch { }
                _sensorIcons.Remove(key);
                _lastIconSignature.Remove(key);
                _lastIconUpdateTime.Remove(key);
            }

            // 2) Tambah ikon baru (yang belum ada) — ikon lama TIDAK disentuh
            bool on = Turbo.Get();
            foreach (var key in wantedKeys)
            {
                if (_sensorIcons.ContainsKey(key)) continue;
                var ni = new NotifyIcon
                {
                    Visible = true,
                    Icon = GetIcon(on),
                    ContextMenuStrip = _menu,
                    Text = SensorName(key) + ": --"
                };
                ni.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowForm(); };
                _sensorIcons[key] = ni;
            }
        }
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

            // ---- Update tooltip (hanya kalau berubah) ----
            string tip = SensorName(key) + ": " + fullVal;
            if (tip.Length > 63) tip = tip.Substring(0, 63);
            if (ni.Text != tip) ni.Text = tip;

            // ---- Cek apakah nilai ikon benar-benar berubah ----
            string signature = key + "::" + shortVal;
            string lastSig;
            bool signatureChanged = !(_lastIconSignature.TryGetValue(key, out lastSig) && lastSig == signature);
            if (!signatureChanged) return;   // ← nilai sama, JANGAN sentuh ikon sama sekali

            // ---- Throttle: minimal 2 detik antar update ikon per sensor ----
            // Ini mencegah Windows tray "reset layout" saat user sedang drag-reorder
            DateTime lastTime;
            if (_lastIconUpdateTime.TryGetValue(key, out lastTime))
            {
                if ((DateTime.Now - lastTime).TotalSeconds < 2.0) return;
            }

            // ---- Render ikon baru ----
            Color fg;
            if (isCpu) fg = Color.FromArgb(_cfg.TrayCpuColor);
            else if (isGpu) fg = Color.FromArgb(_cfg.TrayGpuColor);
            else fg = Color.FromArgb(_cfg.TraySysColor);

            var bmp = DrawSingleValueIcon(fg, shortVal);
            var ic = IconFromBitmap(bmp);
            bmp.Dispose();
            ni.Icon = ic;

            // ---- Simpan cache ----
            _lastIconSignature[key] = signature;
            _lastIconUpdateTime[key] = DateTime.Now;
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

        Bitmap DrawSingleValueIcon(Color fg, string value)
        {
            int size = _cfg.TrayIconSize;
            if (size < 32) size = 32;
            if (size > 512) size = 512;

            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.Clear(Color.Transparent);

                if (string.IsNullOrEmpty(value)) return bmp;

                // Tentukan FontStyle dari config
                FontStyle fs = FontStyle.Regular;
                switch (_cfg.TrayFontStyle)
                {
                    case 1: fs = FontStyle.Bold; break;
                    case 2: fs = FontStyle.Italic; break;
                    case 3: fs = FontStyle.Bold | FontStyle.Italic; break;
                }

                float fontSize = size * 1.6f;
                GraphicsPath bestPath = null;

                while (fontSize >= 8f)
                {
                    Font testFont = null;
                    GraphicsPath testPath = null;
                    try
                    {
                        testFont = new Font(_cfg.TrayFontFamily, fontSize, fs, GraphicsUnit.Pixel);
                        testPath = new GraphicsPath();
                        testPath.AddString(
                            value,
                            testFont.FontFamily,
                            (int)testFont.Style,
                            testFont.Size,
                            new PointF(0, 0),
                            StringFormat.GenericTypographic);

                        var bounds = testPath.GetBounds();
                        float margin = size * (_cfg.TrayMarginPct / 100f);
                        float outlineHalf = _cfg.TrayOutlineWidth / 2f;

                        if (bounds.Width + outlineHalf * 2 <= size - margin &&
                            bounds.Height + outlineHalf * 2 <= size - margin)
                        {
                            float offsetX = (size - bounds.Width) / 2f - bounds.X;
                            float offsetY = (size - bounds.Height) / 2f - bounds.Y;

                            var finalPath = new GraphicsPath();
                            finalPath.AddString(
                                value,
                                testFont.FontFamily,
                                (int)testFont.Style,
                                testFont.Size,
                                new PointF(offsetX, offsetY),
                                StringFormat.GenericTypographic);

                            bestPath = finalPath;
                            break;
                        }
                    }
                    catch { }
                    finally
                    {
                        if (testPath != null) testPath.Dispose();
                        if (testFont != null) testFont.Dispose();
                    }

                    fontSize -= 2f;
                }

                if (bestPath != null)
                {
                    // Outline
                    if (_cfg.TrayOutlineWidth > 0)
                    {
                        using (var outlinePen = new Pen(Color.FromArgb(_cfg.TrayOutlineColor), _cfg.TrayOutlineWidth))
                        {
                            outlinePen.LineJoin = LineJoin.Round;
                            g.DrawPath(outlinePen, bestPath);
                        }
                    }

                    // Fill
                    if (_cfg.TrayUseGradient)
                    {
                        var rect = bestPath.GetBounds();
                        float strength = _cfg.TrayGradientStrength / 100f;
                        using (var gradBrush = new LinearGradientBrush(
                            rect,
                            LightenColor(fg, strength),
                            DarkenColor(fg, strength),
                            LinearGradientMode.Vertical))
                        {
                            g.FillPath(gradBrush, bestPath);
                        }
                    }
                    else
                    {
                        using (var brush = new SolidBrush(fg))
                            g.FillPath(brush, bestPath);
                    }
                    bestPath.Dispose();
                }
            }
            return bmp;
        }
        static Color LightenColor(Color c, float amount)
        {
            int r = c.R + (int)((255 - c.R) * amount);
            int g = c.G + (int)((255 - c.G) * amount);
            int b = c.B + (int)((255 - c.B) * amount);
            if (r > 255) r = 255;
            if (g > 255) g = 255;
            if (b > 255) b = 255;
            return Color.FromArgb(c.A, r, g, b);
        }

        static Color DarkenColor(Color c, float amount)
        {
            int r = (int)(c.R * (1 - amount));
            int g = (int)(c.G * (1 - amount));
            int b = (int)(c.B * (1 - amount));
            return Color.FromArgb(c.A, r, g, b);
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

            // Kalau ini manual (bukan auto), aktifkan override 5 menit
            if (source != "idle-auto" && source != "auto-switch")
            {
                _idleOverrideUntil = DateTime.Now.AddMinutes(5);
                ResetIdleTracking();
            }

            if (changed && _cfg.Notify)
            {
                // Cek suppression untuk idle-auto
                bool suppress = (source == "idle-auto" && !_cfg.IdleShowToast);

                if (!suppress)
                {
                    ShowToast("Turbo Boost",
                        (on ? "Turbo: ON" : "Turbo: OFF") + "   [" + source + "]");
                }
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
            bool appRunning = _cfg.Triggers.Count > 0 && IsAnyTriggerRunning();
            bool current = Turbo.Get();
            var now = DateTime.Now;

            // Kalau boost sedang aktif, skip semua auto logic
            if (IsBoostActive()) return;

            // ============================================================
            // PRIORITAS 1: App trigger sedang jalan → Auto-Switch yang menang
            // ============================================================
            if (_cfg.AutoSwitchEnabled && appRunning)
            {
                // Game jalan → paksa Turbo OFF
                if (current) SetTurbo(false, "auto-switch");

                // Reset idle tracking supaya tidak "carry over" saat game ditutup
                _idleSince = DateTime.MinValue;
                _activeSince = DateTime.MinValue;
                return;
            }

            // ============================================================
            // PRIORITAS 2: Auto-Disable (idle-based)
            // ============================================================
            if (_cfg.IdleAutoDisableEnabled)
            {
                // Skip kalau override manual masih aktif
                if (DateTime.Now < _idleOverrideUntil) return;

                float load = SensorReader.GetCpuLoadPct();

                if (load < _cfg.IdleLoadThresholdPct)
                {
                    if (_idleSince == DateTime.MinValue) _idleSince = now;
                    _activeSince = DateTime.MinValue;

                    if ((now - _idleSince).TotalSeconds >= _cfg.IdleDurationSec)
                    {
                        if (current) SetTurbo(false, "idle-auto");
                    }
                }
                else if (load > _cfg.ActiveLoadThresholdPct)
                {
                    if (_activeSince == DateTime.MinValue) _activeSince = now;
                    _idleSince = DateTime.MinValue;

                    if ((now - _activeSince).TotalSeconds >= _cfg.ActiveDurationSec)
                    {
                        if (!current) SetTurbo(true, "idle-auto");
                    }
                }
                else
                {
                    _idleSince = DateTime.MinValue;
                    _activeSince = DateTime.MinValue;
                }
                return;
            }

            // ============================================================
            // PRIORITAS 3: Auto-Switch saja (tanpa Auto-Disable)
            // ============================================================
            if (_cfg.AutoSwitchEnabled)
            {
                bool desired = !appRunning;
                if (desired != current) SetTurbo(desired, "auto-switch");
            }
        }

        public void ResetIdleTracking()
        {
            _idleSince = DateTime.MinValue;
            _activeSince = DateTime.MinValue;
            _idleOverrideUntil = DateTime.MinValue;
        }

       public bool IsBoostActive()
        {
            return _boostActiveUntil > DateTime.Now;
        }

        public TimeSpan GetBoostRemaining()
        {
            var rem = _boostActiveUntil - DateTime.Now;
            return rem > TimeSpan.Zero ? rem : TimeSpan.Zero;
        }

        public int GetBoostTotalMinutes()
        {
            return _boostTotalMinutes;
        }

        public void StartTemporaryBoost(int minutes)
        {
            if (minutes < 1) minutes = 1;
            if (minutes > 1440) minutes = 1440;

            // Kalau sudah aktif, restart dengan durasi baru
            if (IsBoostActive()) _boostPreviousState = true;   // fallback
            else _boostPreviousState = Turbo.Get();

            _boostStartTime = DateTime.Now;
            _boostActiveUntil = DateTime.Now.AddMinutes(minutes);
            _boostTotalMinutes = minutes;
            _lastCountdownMinute = -1;

            Turbo.Set(true);
            UpdateIcon();
            _form.RefreshStatus();

            // Update menu state
            try
            {
                _menuBoost.Text = "Temporary Boost  (AKTIF)";
                _menuBoostCancel.Visible = true;
                _menuBoostCancel.Text = "Cancel Boost (" + FormatRemaining(_boostActiveUntil - DateTime.Now) + ")";
            }
            catch { }

            if (_cfg.Notify)
            {
                ShowToast("Turbo Toggle", "⚡ Boost " + minutes + " menit dimulai");
            }

            _form.UpdateBoostStatus();
        }

        public void CancelTemporaryBoost()
        {
            if (!IsBoostActive()) return;

            _boostActiveUntil = DateTime.MinValue;
            _lastCountdownMinute = -1;

            // Restore state
            if (_cfg.BoostRestorePrevious)
            {
                Turbo.Set(_boostPreviousState);
            }
            else
            {
                Turbo.Set(false);
            }
            UpdateIcon();
            _form.RefreshStatus();

            // Update menu
            try
            {
                _menuBoost.Text = "Temporary Boost";
                _menuBoostCancel.Visible = false;
            }
            catch { }

            if (_cfg.Notify)
            {
                ShowToast("Turbo Toggle", "Boost dibatalkan");
            }

            _form.UpdateBoostStatus();
        }

        void BoostTick()
        {
            if (!IsBoostActive()) return;

            var remaining = _boostActiveUntil - DateTime.Now;

            // Update menu text
            try
            {
                _menuBoostCancel.Text = "Cancel Boost (" + FormatRemaining(remaining) + ")";
            }
            catch { }

            // Countdown toast tiap menit
            if (_cfg.BoostShowCountdown)
            {
                int currentMin = (int)Math.Ceiling(remaining.TotalMinutes);
                if (currentMin != _lastCountdownMinute && currentMin > 0 && currentMin < _boostTotalMinutes)
                {
                    _lastCountdownMinute = currentMin;
                    if (_cfg.Notify)
                    {
                        ShowToast("Turbo Toggle",
                            "Boost: " + FormatRemaining(remaining) + " lagi");
                    }
                }
            }

            // Habis?
            if (remaining <= TimeSpan.Zero)
            {
                _boostActiveUntil = DateTime.MinValue;
                _lastCountdownMinute = -1;

                if (_cfg.BoostRestorePrevious)
                {
                    Turbo.Set(_boostPreviousState);
                }
                else
                {
                    Turbo.Set(false);
                }
                UpdateIcon();
                _form.RefreshStatus();

                try
                {
                    _menuBoost.Text = "Temporary Boost";
                    _menuBoostCancel.Visible = false;
                }
                catch { }

                if (_cfg.Notify)
                {
                    ShowToast("Turbo Toggle", "⏱ Boost selesai");
                }

                _form.UpdateBoostStatus();
            }
        }

        void ShowCustomBoostDialog()
        {
            using (var dlg = new Form())
            {
                dlg.Text = "Custom Boost Duration";
                dlg.Width = 320;
                dlg.Height = 160;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterScreen;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;

                dlg.Controls.Add(new Label
                {
                    Text = "Durasi (menit):",
                    Left = 20, Top = 20, Width = 100, Height = 20
                });

                var num = new NumericUpDown
                {
                    Left = 130, Top = 18, Width = 150,
                    Minimum = 1, Maximum = 1440,
                    Value = _cfg.BoostDefaultMinutes
                };
                dlg.Controls.Add(num);

                var btnOk = new Button
                {
                    Text = "Mulai", Left = 100, Top = 60, Width = 90, Height = 30,
                    DialogResult = DialogResult.OK
                };
                dlg.Controls.Add(btnOk);

                var btnCancel = new Button
                {
                    Text = "Batal", Left = 200, Top = 60, Width = 80, Height = 30,
                    DialogResult = DialogResult.Cancel
                };
                dlg.Controls.Add(btnCancel);

                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    StartTemporaryBoost((int)num.Value);
                }
            }
        }

        static string FormatRemaining(TimeSpan t)
        {
            if (t.TotalHours >= 1)
                return (int)t.TotalHours + "h " + t.Minutes + "m " + t.Seconds + "s";
            if (t.TotalMinutes >= 1)
                return t.Minutes + "m " + t.Seconds + "s";
            return t.Seconds + "s";
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
                        sysText = string.Join(" ", sysParts);
                        showSys = sysParts.Count > 0;

                        if (!showCpu && !showGpu && !showSys) { cpuText = "CPU --°C"; showCpu = true; }
                    }

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
                                        foreach (var key in keys)
                                        {
                                            NotifyIcon ni;
                                            if (_sensorIcons.TryGetValue(key, out ni))
                                                UpdateSensorIcon(ni, key, s, on);
                                        }
                                    }

                                    // --- Cek alert suhu ---
                                    CheckTempAlerts(s);
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

       void CheckTempAlerts(SensorSample s)
        {
            if (!_cfg.AlertEnabled) return;

            string source = null;
            string message = null;
            string soundPath = null;
            int value = 0;
            int threshold = 0;

            if (s.CpuTempC.HasValue && s.CpuTempC.Value >= _cfg.AlertCpuTemp)
            {
                source = "CPU Temp";
                value = (int)Math.Round(s.CpuTempC.Value);
                threshold = _cfg.AlertCpuTemp;
                message = "CPU " + value + "°C (batas " + threshold + "°C)";
                soundPath = _cfg.AlertCpuSoundPath;
            }
            else if (s.GpuTempC.HasValue && s.GpuTempC.Value >= _cfg.AlertGpuTemp)
            {
                source = "GPU Temp";
                value = (int)Math.Round(s.GpuTempC.Value);
                threshold = _cfg.AlertGpuTemp;
                message = "GPU " + value + "°C (batas " + threshold + "°C)";
                soundPath = _cfg.AlertGpuSoundPath;
            }
            else if (s.GpuHotspotC.HasValue && s.GpuHotspotC.Value >= _cfg.AlertGpuHotspot)
            {
                source = "GPU Hotspot";
                value = (int)Math.Round(s.GpuHotspotC.Value);
                threshold = _cfg.AlertGpuHotspot;
                message = "GPU Hotspot " + value + "°C (batas " + threshold + "°C)";
                soundPath = _cfg.AlertHotspotSoundPath;
            }

            if (source == null) return;

            // Cooldown
            if ((DateTime.Now - _lastAlertTime).TotalSeconds < _cfg.AlertCooldownSec) return;

            _lastAlertTime = DateTime.Now;
            _lastAlertSource = source;

            // Copy supaya aman dipakai di lambda
            string finalSource = source;
            string finalMessage = message;
            string finalSoundPath = soundPath;

            try
            {
                if (_form.IsHandleCreated && !_form.IsDisposed)
                {
                    _form.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (_cfg.AlertShowToast)
                            {
                                var t = new ToastForm("⚠ Suhu Tinggi — " + finalSource, finalMessage, 4000);
                                t.Show();
                            }
                            if (_cfg.AlertPlaySound)
                            {
                                PlayAlertSound(finalSoundPath);
                            }
                        }
                        catch { }
                    }));
                }
            }
            catch { }
        }

public void ApplyLogSetting()
{
    try
    {
        _logTimer.Interval = _cfg.LogIntervalMs;

        if (_cfg.LogEnabled)
        {
            if (!_logTimer.Enabled) StartLogging();
        }
        else
        {
            StopLogging();
        }
    }
    catch { }
}

public string GetCurrentLogPath()
{
    return _currentLogPath;
}

void StartLogging()
{
    try
    {
        if (!Directory.Exists(_cfg.LogFolder))
            Directory.CreateDirectory(_cfg.LogFolder);

        OpenNewLogFileIfNeeded(force: true);
        _logTimer.Interval = _cfg.LogIntervalMs;
        _logTimer.Start();
    }
    catch { }
}

void StopLogging()
{
    try
    {
        _logTimer.Stop();
        if (_logWriter != null)
        {
            try { _logWriter.Flush(); _logWriter.Dispose(); } catch { }
            _logWriter = null;
        }
    }
    catch { }
}

void OpenNewLogFileIfNeeded(bool force = false)
{
    var today = DateTime.Today;

    if (!force && _cfg.LogAutoRotateDaily && _currentLogDay == today && _logWriter != null)
        return;

    // Tutup file lama
    if (_logWriter != null)
    {
        try { _logWriter.Flush(); _logWriter.Dispose(); } catch { }
        _logWriter = null;
    }

    try
    {
        if (!Directory.Exists(_cfg.LogFolder))
            Directory.CreateDirectory(_cfg.LogFolder);

        string filename = _cfg.LogAutoRotateDaily
            ? "TurboToggle_" + DateTime.Now.ToString("yyyy-MM-dd") + ".csv"
            : "TurboToggle_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".csv";

        _currentLogPath = Path.Combine(_cfg.LogFolder, filename);
        _currentLogDay = today;

        bool newFile = !File.Exists(_currentLogPath);
        _logWriter = new StreamWriter(_currentLogPath, append: true);
        _logWriter.AutoFlush = true;

        if (newFile)
        {
            _logLineCount = 0;
            _logWriter.WriteLine(BuildLogHeader());
        }
        else
        {
            // Hitung baris yang sudah ada
            try
            {
                _logLineCount = File.ReadAllLines(_currentLogPath).Length - 1;
                if (_logLineCount < 0) _logLineCount = 0;
            }
            catch { _logLineCount = 0; }
        }
    }
    catch
    {
        _logWriter = null;
    }
}

static string BuildLogHeader()
{
    return "Timestamp," +
           "CPU_Temp_C,CPU_Freq_MHz,CPU_Load_Pct,CPU_Power_W,CPU_Fan_RPM,CPU_Volt_V," +
           "GPU_Temp_C,GPU_Freq_MHz,GPU_Load_Pct,GPU_Hotspot_C,GPU_Power_W," +
           "GPU_VRAM_Used_MB,GPU_VRAM_Total_MB," +
           "RAM_Used_Pct,RAM_Used_GB,RAM_Total_GB," +
           "MB_Temp_C,Net_Down_Mbps,Net_Up_Mbps,Disk_Act_Pct,Uptime_Sec,Turbo_State";
}

void LogTick()
{
    if (!_cfg.LogEnabled) return;

    try
    {
        // Rotate file kalau ganti hari
        OpenNewLogFileIfNeeded();

        if (_logWriter == null) return;

        var s = SensorReader.Read();

        var sb = new System.Text.StringBuilder();
        sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.Append(',');
        sb.Append(Fmt(s.CpuTempC, "0.0")); sb.Append(',');
        sb.Append(Fmt(s.CpuFreqMHz, "0")); sb.Append(',');
        sb.Append(Fmt(s.CpuLoadPct, "0.0")); sb.Append(',');
        sb.Append(Fmt(s.CpuPowerW, "0.0")); sb.Append(',');
        sb.Append(Fmt(s.CpuFanRpm, "0")); sb.Append(',');
        sb.Append(Fmt(s.CpuVoltage, "0.000")); sb.Append(',');
        sb.Append(Fmt(s.GpuTempC, "0.0")); sb.Append(',');
        sb.Append(Fmt(s.GpuFreqMHz, "0")); sb.Append(',');
        sb.Append(Fmt(s.GpuLoadPct, "0.0")); sb.Append(',');
        sb.Append(Fmt(s.GpuHotspotC, "0.0")); sb.Append(',');
        sb.Append(Fmt(s.GpuPowerW, "0.0")); sb.Append(',');
        sb.Append(Fmt(s.GpuVramUsedMB, "0")); sb.Append(',');
        sb.Append(Fmt(s.GpuVramTotalMB, "0")); sb.Append(',');
        sb.Append(Fmt(s.RamUsedPct, "0.0")); sb.Append(',');
        sb.Append(Fmt(s.RamUsedGB, "0.00")); sb.Append(',');
        sb.Append(Fmt(s.RamTotalGB, "0.00")); sb.Append(',');
        sb.Append(Fmt(s.MbTempC, "0.0")); sb.Append(',');
        sb.Append(Fmt(s.NetDownMbps, "0.00")); sb.Append(',');
        sb.Append(Fmt(s.NetUpMbps, "0.00")); sb.Append(',');
        sb.Append(Fmt(s.DiskActivityPct, "0.0")); sb.Append(',');
        sb.Append(((long)s.Uptime.TotalSeconds).ToString());
        sb.Append(',');
        sb.Append(Turbo.Get() ? "ON" : "OFF");

        _logWriter.WriteLine(sb.ToString());
        _logLineCount++;
    }
    catch { }

    // Refresh status di UI form (kalau form terbuka)
    try
    {
        if (_form.IsHandleCreated && !_form.IsDisposed && _form.Visible)
        {
            _form.BeginInvoke(new Action(() => _form.RefreshLogStatus()));
        }
    }
    catch { }
}

static string Fmt(float? v, string format)
{
    if (!v.HasValue) return "";
    return v.Value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
}
        void PlayAlertSound(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                {
                    try
                    {
                        if (_alertSoundPlayer != null)
                        {
                            _alertSoundPlayer.Stop();
                            _alertSoundPlayer.Dispose();
                        }
                    }
                    catch { }
                    _alertSoundPlayer = new System.Media.SoundPlayer(path);
                    _alertSoundPlayer.Play();
                }
                else
                {
                    // Default sistem
                    System.Media.SystemSounds.Asterisk.Play();
                }
            }
            catch
            {
                try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
            }
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

            try { _boostTimer.Stop(); } catch { }
            try { StopLogging(); } catch { }
            
            _topMostTimer.Stop();

            try
            {
                if (_alertSoundPlayer != null)
                {
                    _alertSoundPlayer.Stop();
                    _alertSoundPlayer.Dispose();
                    _alertSoundPlayer = null;
                }
            }
            catch { }

            try { _hotkey.Dispose(); } catch { }
            _icon.Visible = false;
            _icon.Dispose();
            try { _overlay.Close(); _overlay.Dispose(); } catch { }

            foreach (var kv in _sensorIcons)
            {
                try { kv.Value.Visible = false; kv.Value.Dispose(); } catch { }
            }
            _sensorIcons.Clear();

            _lastIconSignature.Clear();
            _lastIconUpdateTime.Clear();

            Application.Exit();
        }

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

        public void SetSensorInterval(int ms)
        {
            if (ms < 500) ms = 500;
            if (ms > 10000) ms = 10000;
            _cfg.SensorIntervalMs = ms;
            _cfg.Save();
            _sensorTimer.Interval = ms;
        }
    }
}