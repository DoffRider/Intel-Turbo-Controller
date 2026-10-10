using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace TurboToggle
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            if (!IsAdmin())
            {
                RelaunchAsAdmin();
                return;
            }
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

 static class Turbo
{
    const string GUID_PROC  = "54533251-82be-4824-96c1-47b60b740d00";
    const string GUID_BOOST = "be337238-0d82-4146-a960-4f3749d470c7";

    static bool _state  = true;
    static bool _loaded = false;
    static readonly object _lock = new object();

    public static void Set(bool enabled)
    {
        lock (_lock)
        {
            Run("/SETACVALUEINDEX SCHEME_CURRENT " + GUID_PROC + " " + GUID_BOOST + " " + (enabled ? "1" : "0"));
            Run("/SETDCVALUEINDEX SCHEME_CURRENT " + GUID_PROC + " " + GUID_BOOST + " " + (enabled ? "1" : "0"));
            Run("/SETACTIVE SCHEME_CURRENT");
            // Percayai nilai yang kita set — jangan query ulang
            _state  = enabled;
            _loaded = true;
        }
    }

    public static bool Get()
    {
        lock (_lock)
        {
            if (!_loaded)
            {
                _state  = ReadPowerCfg();
                _loaded = true;
            }
            return _state;
        }
    }

    // Paksa baca ulang dari sistem (misal habis diubah app/program lain)
    public static bool ReloadFromSystem()
    {
        lock (_lock)
        {
            _state  = ReadPowerCfg();
            _loaded = true;
            return _state;
        }
    }

    static bool ReadPowerCfg()
    {
        try
        {
            var psi = new ProcessStartInfo("powercfg",
                "/query SCHEME_CURRENT " + GUID_PROC + " " + GUID_BOOST)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);
                foreach (var rawLine in output.Split(new[] { '\n', '\r' },
                             StringSplitOptions.RemoveEmptyEntries))
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
                            if (uint.TryParse(sb.ToString(),
                                System.Globalization.NumberStyles.HexNumber, null, out v))
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
            {
                UseShellExecute = false,
                CreateNoWindow = true
            }))
            {
                p.WaitForExit(5000);
            }
        }
        catch { }
    }
}

    class Config
    {
        public bool AutoSwitchEnabled = false;
        public int PollIntervalMs = 2000;
        public List<string> Triggers = new List<string>();

        static string PathFile
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TurboToggle");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "config.ini");
            }
        }

        public static Config Load()
        {
            var c = new Config();
            try
            {
                if (!File.Exists(PathFile)) return c;
                foreach (var raw in File.ReadAllLines(PathFile))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    switch (k)
                    {
                        case "autoSwitchEnabled":
                            c.AutoSwitchEnabled = v.Equals("true", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "pollIntervalMs":
                            int pi; int.TryParse(v, out pi); c.PollIntervalMs = pi;
                            break;
                        case "triggers":
                            c.Triggers = v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim().ToLowerInvariant())
                                .Where(s => s.Length > 0)
                                .Distinct()
                                .ToList();
                            break;
                    }
                }
            }
            catch { }
            if (c.PollIntervalMs < 500) c.PollIntervalMs = 2000;
            return c;
        }

        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# Turbo Toggle Config");
                sb.AppendLine("autoSwitchEnabled=" + (AutoSwitchEnabled ? "true" : "false"));
                sb.AppendLine("pollIntervalMs=" + PollIntervalMs);
                sb.AppendLine("triggers=" + string.Join(",", Triggers));
                File.WriteAllText(PathFile, sb.ToString());
            }
            catch { }
        }
    }

    class TrayContext : ApplicationContext
    {
        readonly NotifyIcon _icon;
        readonly Config _cfg;
        readonly System.Windows.Forms.Timer _timer;
        readonly MainForm _form;
        static Icon _iconOn, _iconOff;

        public TrayContext()
        {
            _cfg = Config.Load();

            _form = new MainForm(_cfg);
            _form.OnTurboChanged += () => UpdateIcon();
            _form.OnExitRequested += () => ExitApp();

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open Control Panel", null, (s, e) => ShowForm());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Turbo ON",  null, (s, e) => _form.SetTurbo(true));
            menu.Items.Add("Turbo OFF", null, (s, e) => _form.SetTurbo(false));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitApp());

            _icon = new NotifyIcon
            {
                Visible = true,
                ContextMenuStrip = menu,
                Icon = GetIcon(Turbo.Get())
            };
            _icon.Text = "Turbo Boost: " + (Turbo.Get() ? "ON" : "OFF");
            _icon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) ShowForm();
            };

            _timer = new System.Windows.Forms.Timer { Interval = _cfg.PollIntervalMs };
            _timer.Tick += (s, e) => AutoTick();
            _timer.Start();
        }

        void ShowForm()
        {
            _form.RefreshStatus();
            if (!_form.Visible) _form.Show();
            if (_form.WindowState == FormWindowState.Minimized)
                _form.WindowState = FormWindowState.Normal;
            _form.Activate();
            _form.BringToFront();
        }

        void AutoTick()
        {
            if (!_cfg.AutoSwitchEnabled) return;
            bool desired = _cfg.Triggers.Count > 0 && IsAnyTriggerRunning();
            bool current = Turbo.Get();
            if (desired != current)
            {
                Turbo.Set(desired);
                UpdateIcon();
                _form.RefreshStatus();
            }
        }

        bool IsAnyTriggerRunning()
        {
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        string name = p.ProcessName.ToLowerInvariant();
                        if (_cfg.Triggers.Contains(name)) return true;
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }
            return false;
        }

        void UpdateIcon()
        {
            bool on = Turbo.Get();
            _icon.Icon = GetIcon(on);
            _icon.Text = "Turbo Boost: " + (on ? "ON" : "OFF");
        }

        static Icon GetIcon(bool on)
        {
            if (on)
                return _iconOn ?? (_iconOn = CreateIcon(true));
            return _iconOff ?? (_iconOff = CreateIcon(false));
        }

        static Icon CreateIcon(bool on)
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(on
                    ? Color.FromArgb(46, 204, 113)
                    : Color.FromArgb(231, 76, 60)))
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
            IntPtr h = bmp.GetHicon();
            return Icon.FromHandle(h);
        }

        void ExitApp()
        {
            _timer.Stop();
            _icon.Visible = false;
            _icon.Dispose();
            Application.Exit();
        }
    }

    class MainForm : Form
    {
        public event Action OnTurboChanged;
        public event Action OnExitRequested;

        readonly Config _cfg;
        Label _statusLabel;
        Panel _statusDot;
        Button _btnOn, _btnOff, _btnEdit, _btnHide, _btnExit;
        CheckBox _chkAuto;
        NumericUpDown _numInterval;

        public MainForm(Config cfg)
        {
            _cfg = cfg;

            Text = "Turbo Boost Control";
            Width = 460;
            Height = 320;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            Icon = CreateFormIcon();

            // ------ Status bar ------
            _statusDot = new Panel
            {
                Left = 20, Top = 22, Width = 22, Height = 22
            };
            _statusDot.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush((Color)_statusDot.Tag))
                    g.FillEllipse(b, 0, 0, _statusDot.Width - 1, _statusDot.Height - 1);
            };
            Controls.Add(_statusDot);

            _statusLabel = new Label
            {
                Left = 52, Top = 24, Width = 380, Height = 24,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Text = "Status: -"
            };
            Controls.Add(_statusLabel);

            // ------ Tombol ON / OFF ------
            _btnOn = new Button
            {
                Text = "TURBO ON",
                Left = 20, Top = 70, Width = 200, Height = 60,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnOn.FlatAppearance.BorderSize = 0;
            _btnOn.Click += (s, e) => SetTurbo(true);
            Controls.Add(_btnOn);

            _btnOff = new Button
            {
                Text = "TURBO OFF",
                Left = 228, Top = 70, Width = 200, Height = 60,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(231, 76, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnOff.FlatAppearance.BorderSize = 0;
            _btnOff.Click += (s, e) => SetTurbo(false);
            Controls.Add(_btnOff);

            // ------ Auto-switch ------
            _chkAuto = new CheckBox
            {
                Text = "Auto-Switch (ON saat app trigger berjalan, OFF saat tidak)",
                Left = 20, Top = 150, Width = 410, Height = 24,
                Checked = _cfg.AutoSwitchEnabled
            };
            _chkAuto.CheckedChanged += (s, e) =>
            {
                _cfg.AutoSwitchEnabled = _chkAuto.Checked;
                _cfg.Save();
            };
            Controls.Add(_chkAuto);

            // ------ Interval ------
            Controls.Add(new Label
            {
                Text = "Cek tiap (ms):",
                Left = 20, Top = 184, Width = 100, Height = 22
            });
            _numInterval = new NumericUpDown
            {
                Left = 126, Top = 182, Width = 90,
                Minimum = 500, Maximum = 60000,
                Increment = 500,
                Value = _cfg.PollIntervalMs
            };
            _numInterval.ValueChanged += (s, e) =>
            {
                _cfg.PollIntervalMs = (int)_numInterval.Value;
                _cfg.Save();
            };
            Controls.Add(_numInterval);

            _btnEdit = new Button
            {
                Text = "Edit Trigger Apps...",
                Left = 230, Top = 180, Width = 200, Height = 26
            };
            _btnEdit.Click += (s, e) => EditTriggers();
            Controls.Add(_btnEdit);

            // ------ Bottom buttons ------
            _btnHide = new Button
            {
                Text = "Hide to Tray",
                Left = 20, Top = 230, Width = 130, Height = 30
            };
            _btnHide.Click += (s, e) => Hide();
            Controls.Add(_btnHide);

            _btnExit = new Button
            {
                Text = "Exit App",
                Left = 300, Top = 230, Width = 130, Height = 30
            };
            _btnExit.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Keluar dari Turbo Toggle?", "Konfirmasi",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    if (OnExitRequested != null) OnExitRequested();
                }
            };
            Controls.Add(_btnExit);

            // X di window = hide, bukan exit
            FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
            };

            RefreshStatus();
        }

        public void SetTurbo(bool on)
        {
            Turbo.Set(on);
            RefreshStatus();
            if (OnTurboChanged != null) OnTurboChanged();
        }

        public void RefreshStatus()
        {
            bool on = Turbo.Get();
            _statusDot.Tag = on
                ? Color.FromArgb(46, 204, 113)
                : Color.FromArgb(231, 76, 60);
            _statusDot.Invalidate();
            _statusLabel.Text = "Turbo Boost: " + (on ? "ON" : "OFF");
            _statusLabel.ForeColor = on
                ? Color.FromArgb(39, 174, 96)
                : Color.FromArgb(192, 57, 43);

            _btnOn.Enabled = !on;
            _btnOff.Enabled = on;
            _btnOn.BackColor = on ? Color.FromArgb(120, 200, 150) : Color.FromArgb(46, 204, 113);
            _btnOff.BackColor = !on ? Color.FromArgb(200, 130, 120) : Color.FromArgb(231, 76, 60);
        }

        void EditTriggers()
        {
            using (var f = new TriggerForm(_cfg))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    _cfg.Save();
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

    class TriggerForm : Form
    {
        readonly Config _cfg;
        readonly TextBox _txt;

        public TriggerForm(Config cfg)
        {
            _cfg = cfg;
            Text = "Edit Trigger Apps";
            Width = 520; Height = 430;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;

            Controls.Add(new Label
            {
                Text = "Satu nama proses per baris (tanpa .exe), contoh:\n" +
                       "  csgo      (untuk csgo.exe)\n" +
                       "  valorant  (untuk valorant.exe)\n\n" +
                       "Kalau salah satu proses di atas berjalan, Turbo otomatis ON.\n" +
                       "Kalau tidak ada, Turbo otomatis OFF (saat Auto-Switch aktif).",
                Left = 12, Top = 12, Width = 480, Height = 90
            });

            _txt = new TextBox
            {
                Left = 12, Top = 110, Width = 480, Height = 240,
                Multiline = true, ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10),
                Text = string.Join("\r\n", _cfg.Triggers)
            };
            Controls.Add(_txt);

            var ok = new Button { Text = "OK", Left = 322, Top = 360, Width = 80, DialogResult = DialogResult.OK };
            ok.Click += (s, e) =>
            {
                _cfg.Triggers = _txt.Text
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim().ToLowerInvariant().Replace(".exe", ""))
                    .Where(x => x.Length > 0)
                    .Distinct()
                    .ToList();
            };
            Controls.Add(ok);

            var cancel = new Button { Text = "Cancel", Left = 412, Top = 360, Width = 80, DialogResult = DialogResult.Cancel };
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}