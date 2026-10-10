using System;
using System.Drawing;
using System.Windows.Forms;

namespace TurboToggle
{
    partial class MainForm
    {
        NumericUpDown _numBoostMinutes;
        CheckBox _chkBoostRestore;
        CheckBox _chkBoostCountdown;
        Button _btnBoostStart;
        Button _btnBoostCancel;
        Label _lblBoostStatus;
        System.Windows.Forms.Timer _boostStatusTimer;

        void BuildBoostTab()
        {
            var tpBoost = new TabPage("Boost");
            _tabs.TabPages.Add(tpBoost);

            tpBoost.Controls.Add(new Label
            {
                Text = "Temporary Boost — nyalakan Turbo untuk durasi tertentu, otomatis OFF setelahnya.",
                Left = 12, Top = 12, Width = 440, Height = 40,
                ForeColor = Color.DimGray
            });

            // === Durasi ===
            tpBoost.Controls.Add(new Label
            {
                Text = "Durasi boost (menit):",
                Left = 12, Top = 60, Width = 150, Height = 20,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            });

            _numBoostMinutes = new NumericUpDown
            {
                Left = 170, Top = 58, Width = 80,
                Minimum = 1, Maximum = 1440,
                Value = _cfg.BoostDefaultMinutes
            };
            _numBoostMinutes.ValueChanged += (s, e) =>
            {
                _cfg.BoostDefaultMinutes = (int)_numBoostMinutes.Value;
                _cfg.Save();
            };
            tpBoost.Controls.Add(_numBoostMinutes);

            tpBoost.Controls.Add(new Label
            {
                Text = "menit (1 – 1440 = 24 jam)",
                Left = 260, Top = 60, Width = 180, Height = 20, ForeColor = Color.Gray
            });

            // === Preset buttons ===
            tpBoost.Controls.Add(new Label
            {
                Text = "Preset cepat:",
                Left = 12, Top = 92, Width = 100, Height = 20
            });

            var presetValues = new[] { 5, 15, 30, 45, 60, 120 };
            int px = 12;
            foreach (int min in presetValues)
            {
                var btn = new Button
                {
                    Text = min + "m",
                    Left = px, Top = 114, Width = 55, Height = 28
                };
                int m = min;
                btn.Click += (s, e) =>
                {
                    _numBoostMinutes.Value = m;
                    StartBoost(m);
                };
                tpBoost.Controls.Add(btn);
                px += 60;
            }

            // === Opsi ===
            _chkBoostRestore = new CheckBox
            {
                Text = "Kembalikan ke state sebelum boost (kalau tidak dicentang, Turbo OFF)",
                Left = 12, Top = 156, Width = 440, Height = 24,
                Checked = _cfg.BoostRestorePrevious
            };
            _chkBoostRestore.CheckedChanged += (s, e) =>
            {
                _cfg.BoostRestorePrevious = _chkBoostRestore.Checked;
                _cfg.Save();
            };
            tpBoost.Controls.Add(_chkBoostRestore);

            _chkBoostCountdown = new CheckBox
            {
                Text = "Toast countdown tiap menit",
                Left = 12, Top = 184, Width = 400, Height = 24,
                Checked = _cfg.BoostShowCountdown
            };
            _chkBoostCountdown.CheckedChanged += (s, e) =>
            {
                _cfg.BoostShowCountdown = _chkBoostCountdown.Checked;
                _cfg.Save();
            };
            tpBoost.Controls.Add(_chkBoostCountdown);

            // === Tombol aksi ===
            _btnBoostStart = new Button
            {
                Text = "Mulai Boost",
                Left = 12, Top = 224, Width = 140, Height = 36,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnBoostStart.FlatAppearance.BorderSize = 0;
            _btnBoostStart.Click += (s, e) => StartBoost((int)_numBoostMinutes.Value);
            tpBoost.Controls.Add(_btnBoostStart);

            _btnBoostCancel = new Button
            {
                Text = "Batalkan Boost",
                Left = 160, Top = 224, Width = 140, Height = 36,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(231, 76, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            _btnBoostCancel.FlatAppearance.BorderSize = 0;
            _btnBoostCancel.Click += (s, e) => CancelBoost();
            tpBoost.Controls.Add(_btnBoostCancel);

            // === Status ===
            _lblBoostStatus = new Label
            {
                Left = 12, Top = 280, Width = 440, Height = 80,
                Font = new Font("Consolas", 10f),
                ForeColor = Color.DimGray
            };
            tpBoost.Controls.Add(_lblBoostStatus);

            // === Tips ===
            tpBoost.Controls.Add(new Label
            {
                Text = "Info:\n" +
                       "• Selama boost, Auto-Disable & Auto-Switch di-pause supaya tidak ganggu.\n" +
                       "• Bisa juga mulai boost dari tray menu (klik kanan ikon T).\n" +
                       "• Kalau BoostRestorePrevious aktif, Turbo kembali ke state sebelumnya.",
                Left = 12, Top = 380, Width = 440, Height = 80,
                ForeColor = Color.Gray, Font = new Font("Segoe UI", 8.5f)
            });

            // Timer untuk update status live setiap 1 detik
            _boostStatusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _boostStatusTimer.Tick += (s, e) => UpdateBoostStatus();
            _boostStatusTimer.Start();

            UpdateBoostStatus();
        }

        void StartBoost(int minutes)
        {
            if (minutes < 1) minutes = 1;
            if (minutes > 1440) minutes = 1440;
            _ctx.StartTemporaryBoost(minutes);
            UpdateBoostStatus();
        }

        void CancelBoost()
        {
            _ctx.CancelTemporaryBoost();
            UpdateBoostStatus();
        }

        public void UpdateBoostStatus()
        {
            try
            {
                if (_lblBoostStatus == null) return;

                if (_ctx.IsBoostActive())
                {
                    var remaining = _ctx.GetBoostRemaining();
                    _lblBoostStatus.Text =
                        "Status   : AKTIF ⚡\n" +
                        "Sisa     : " + FormatRemaining(remaining) + "\n" +
                        "Total    : " + _ctx.GetBoostTotalMinutes() + " menit";
                    _lblBoostStatus.ForeColor = Color.FromArgb(46, 204, 113);

                    if (_btnBoostStart != null) _btnBoostStart.Enabled = false;
                    if (_btnBoostCancel != null) _btnBoostCancel.Enabled = true;
                }
                else
                {
                    _lblBoostStatus.Text = "Status   : tidak aktif";
                    _lblBoostStatus.ForeColor = Color.DimGray;

                    if (_btnBoostStart != null) _btnBoostStart.Enabled = true;
                    if (_btnBoostCancel != null) _btnBoostCancel.Enabled = false;
                }
            }
            catch { }
        }

        static string FormatRemaining(TimeSpan t)
        {
            if (t.TotalHours >= 1)
                return (int)t.TotalHours + " jam " + t.Minutes + " menit " + t.Seconds + " detik";
            if (t.TotalMinutes >= 1)
                return t.Minutes + " menit " + t.Seconds + " detik";
            return t.Seconds + " detik";
        }
    }
}