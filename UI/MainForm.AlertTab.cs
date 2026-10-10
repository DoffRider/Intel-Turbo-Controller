using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TurboToggle
{
    partial class MainForm
    {
        CheckBox _chkAlertEnabled;
        NumericUpDown _numAlertCpu;
        NumericUpDown _numAlertGpu;
        NumericUpDown _numAlertHotspot;
        NumericUpDown _numAlertCooldown;
        CheckBox _chkAlertSound;
        CheckBox _chkAlertToast;

        void BuildAlertTab()
        {
            var tpAlert = new TabPage("Alert");
            _tabs.TabPages.Add(tpAlert);
            tpAlert.AutoScroll = true;

            _chkAlertEnabled = new CheckBox
            {
                Text = "Aktifkan alert suhu",
                Left = 12, Top = 12, Width = 400, Height = 24,
                Checked = _cfg.AlertEnabled
            };
            _chkAlertEnabled.CheckedChanged += (s, e) =>
            {
                _cfg.AlertEnabled = _chkAlertEnabled.Checked;
                _cfg.Save();
            };
            tpAlert.Controls.Add(_chkAlertEnabled);

            tpAlert.Controls.Add(new Label
            {
                Text = "Batas suhu (notifikasi muncul kalau suhu melewati nilai ini):",
                Left = 12, Top = 44, Width = 430, Height = 20,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            });

            // --- CPU Temp ---
            tpAlert.Controls.Add(new Label { Text = "CPU Temp:", Left = 20, Top = 72, Width = 90, Height = 22 });
            _numAlertCpu = new NumericUpDown
            {
                Left = 120, Top = 70, Width = 70,
                Minimum = 30, Maximum = 120,
                Value = _cfg.AlertCpuTemp
            };
            _numAlertCpu.ValueChanged += (s, e) =>
            {
                _cfg.AlertCpuTemp = (int)_numAlertCpu.Value;
                _cfg.Save();
            };
            tpAlert.Controls.Add(_numAlertCpu);
            tpAlert.Controls.Add(new Label { Text = "°C", Left = 196, Top = 72, Width = 30, Height = 22 });

            // --- GPU Temp ---
            tpAlert.Controls.Add(new Label { Text = "GPU Temp:", Left = 20, Top = 100, Width = 90, Height = 22 });
            _numAlertGpu = new NumericUpDown
            {
                Left = 120, Top = 98, Width = 70,
                Minimum = 30, Maximum = 120,
                Value = _cfg.AlertGpuTemp
            };
            _numAlertGpu.ValueChanged += (s, e) =>
            {
                _cfg.AlertGpuTemp = (int)_numAlertGpu.Value;
                _cfg.Save();
            };
            tpAlert.Controls.Add(_numAlertGpu);
            tpAlert.Controls.Add(new Label { Text = "°C", Left = 196, Top = 100, Width = 30, Height = 22 });

            // --- GPU Hotspot ---
            tpAlert.Controls.Add(new Label { Text = "GPU Hotspot:", Left = 20, Top = 128, Width = 90, Height = 22 });
            _numAlertHotspot = new NumericUpDown
            {
                Left = 120, Top = 126, Width = 70,
                Minimum = 30, Maximum = 130,
                Value = _cfg.AlertGpuHotspot
            };
            _numAlertHotspot.ValueChanged += (s, e) =>
            {
                _cfg.AlertGpuHotspot = (int)_numAlertHotspot.Value;
                _cfg.Save();
            };
            tpAlert.Controls.Add(_numAlertHotspot);
            tpAlert.Controls.Add(new Label { Text = "°C", Left = 196, Top = 128, Width = 30, Height = 22 });

            // --- Cooldown ---
            tpAlert.Controls.Add(new Label { Text = "Cooldown:", Left = 20, Top = 164, Width = 90, Height = 22 });
            _numAlertCooldown = new NumericUpDown
            {
                Left = 120, Top = 162, Width = 70,
                Minimum = 5, Maximum = 3600,
                Increment = 5,
                Value = _cfg.AlertCooldownSec
            };
            _numAlertCooldown.ValueChanged += (s, e) =>
            {
                _cfg.AlertCooldownSec = (int)_numAlertCooldown.Value;
                _cfg.Save();
            };
            tpAlert.Controls.Add(_numAlertCooldown);
            tpAlert.Controls.Add(new Label
            {
                Text = "detik (jeda minimum antar notifikasi)",
                Left = 196, Top = 164, Width = 240, Height = 22, ForeColor = Color.Gray
            });

            // --- Metode notifikasi ---
            tpAlert.Controls.Add(new Label
            {
                Text = "Metode notifikasi:",
                Left = 12, Top = 200, Width = 200, Height = 20,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            });

            _chkAlertToast = new CheckBox
            {
                Text = "Tampilkan toast visual di pojok layar",
                Left = 20, Top = 226, Width = 400, Height = 24,
                Checked = _cfg.AlertShowToast
            };
            _chkAlertToast.CheckedChanged += (s, e) =>
            {
                _cfg.AlertShowToast = _chkAlertToast.Checked;
                _cfg.Save();
            };
            tpAlert.Controls.Add(_chkAlertToast);

            _chkAlertSound = new CheckBox
            {
                Text = "Mainkan suara saat alert",
                Left = 20, Top = 252, Width = 400, Height = 24,
                Checked = _cfg.AlertPlaySound
            };
            _chkAlertSound.CheckedChanged += (s, e) =>
            {
                _cfg.AlertPlaySound = _chkAlertSound.Checked;
                _cfg.Save();
            };
            tpAlert.Controls.Add(_chkAlertSound);

            // --- Custom sounds ---
            tpAlert.Controls.Add(new Label
            {
                Text = "Custom sound (WAV) — biarkan kosong untuk pakai suara sistem:",
                Left = 12, Top = 288, Width = 430, Height = 20,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            });

            BuildSoundRow(tpAlert, "CPU:",     318, () => _cfg.AlertCpuSoundPath,      v => _cfg.AlertCpuSoundPath = v);
            BuildSoundRow(tpAlert, "GPU:",     348, () => _cfg.AlertGpuSoundPath,      v => _cfg.AlertGpuSoundPath = v);
            BuildSoundRow(tpAlert, "Hotspot:", 378, () => _cfg.AlertHotspotSoundPath,  v => _cfg.AlertHotspotSoundPath = v);

            // --- Tips ---
            tpAlert.Controls.Add(new Label
            {
                Text = "Tips:\n" +
                       "• Set threshold sekitar 10-15°C di atas suhu normal saat gaming.\n" +
                       "• Sound format harus .wav (PCM). Untuk MP3, convert dulu via converter online.\n" +
                       "• Cooldown mencegah notifikasi spam saat suhu naik-turun di sekitar threshold.",
                Left = 12, Top = 415, Width = 430, Height = 70, ForeColor = Color.Gray
            });
        }

        void BuildSoundRow(Control parent, string label, int top, Func<string> getter, Action<string> setter)
        {
            parent.Controls.Add(new Label
            {
                Text = label,
                Left = 20, Top = top, Width = 70, Height = 22
            });

            string initial = getter();
            var lblPath = new Label
            {
                Left = 96, Top = top, Width = 220, Height = 22,
                Text = string.IsNullOrEmpty(initial) ? "(default sistem)" : Path.GetFileName(initial),
                ForeColor = string.IsNullOrEmpty(initial) ? Color.Gray : Color.Black,
                AutoEllipsis = true
            };
            parent.Controls.Add(lblPath);

            var btnBrowse = new Button { Text = "Browse...", Left = 322, Top = top - 3, Width = 80, Height = 26 };
            btnBrowse.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog
                {
                    Filter = "WAV files (*.wav)|*.wav|All files (*.*)|*.*",
                    Title = "Pilih file suara untuk " + label.Replace(":", "")
                })
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        setter(dlg.FileName);
                        lblPath.Text = Path.GetFileName(dlg.FileName);
                        lblPath.ForeColor = Color.Black;
                        _cfg.Save();
                    }
                }
            };
            parent.Controls.Add(btnBrowse);

            var btnClear = new Button { Text = "Clear", Left = 408, Top = top - 3, Width = 60, Height = 26 };
            btnClear.Click += (s, e) =>
            {
                setter("");
                lblPath.Text = "(default sistem)";
                lblPath.ForeColor = Color.Gray;
                _cfg.Save();
            };
            parent.Controls.Add(btnClear);
        }
    }
}