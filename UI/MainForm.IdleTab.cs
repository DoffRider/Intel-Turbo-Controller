using System;
using System.Drawing;
using System.Windows.Forms;

namespace TurboToggle
{
    partial class MainForm
    {
        CheckBox _chkIdleEnabled;
        NumericUpDown _numIdleLoadPct;
        NumericUpDown _numIdleDurationSec;
        NumericUpDown _numActiveLoadPct;
        NumericUpDown _numActiveDurationSec;
        CheckBox _chkIdleShowToast;

        void BuildIdleTab()
        {
            var tpIdle = new TabPage("Idle");
            _tabs.TabPages.Add(tpIdle);

            _chkIdleEnabled = new CheckBox
            {
                Text = "Aktifkan Auto-Disable saat CPU idle",
                Left = 12, Top = 12, Width = 400, Height = 24,
                Checked = _cfg.IdleAutoDisableEnabled
            };
            _chkIdleEnabled.CheckedChanged += (s, e) =>
            {
                _cfg.IdleAutoDisableEnabled = _chkIdleEnabled.Checked;
                _cfg.Save();
                _ctx.ResetIdleTracking();
            };
            tpIdle.Controls.Add(_chkIdleEnabled);

            // === INFO PRIORITAS ===
            tpIdle.Controls.Add(new Label
            {
                Text = "ℹ Prioritas: kalau app trigger (tab Auto-Switch) sedang berjalan,\n" +
                       "   Auto-Switch yang menang. Auto-Disable hanya jalan kalau tidak ada\n" +
                       "   app trigger aktif. Cocok dipakai bersama!",
                Left = 12, Top = 42, Width = 430, Height = 50,
                ForeColor = Color.FromArgb(0, 100, 180),
                Font = new Font("Segoe UI", 8.5f)
            });

            // === Kondisi IDLE (Turbo OFF) ===
            tpIdle.Controls.Add(new Label
            {
                Text = "Kondisi IDLE → Turbo OFF",
                Left = 12, Top = 100, Width = 400, Height = 20,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            });

            tpIdle.Controls.Add(new Label { Text = "CPU load < ", Left = 20, Top = 128, Width = 70, Height = 22 });
            _numIdleLoadPct = new NumericUpDown
            {
                Left = 90, Top = 126, Width = 60,
                Minimum = 1, Maximum = 90,
                Value = _cfg.IdleLoadThresholdPct
            };
            _numIdleLoadPct.ValueChanged += (s, e) =>
            {
                _cfg.IdleLoadThresholdPct = (int)_numIdleLoadPct.Value;
                _cfg.Save();
            };
            tpIdle.Controls.Add(_numIdleLoadPct);
            tpIdle.Controls.Add(new Label { Text = "%", Left = 154, Top = 128, Width = 30, Height = 22 });
            tpIdle.Controls.Add(new Label { Text = "selama", Left = 184, Top = 128, Width = 60, Height = 22 });
            _numIdleDurationSec = new NumericUpDown
            {
                Left = 244, Top = 126, Width = 80,
                Minimum = 10, Maximum = 3600, Increment = 10,
                Value = _cfg.IdleDurationSec
            };
            _numIdleDurationSec.ValueChanged += (s, e) =>
            {
                _cfg.IdleDurationSec = (int)_numIdleDurationSec.Value;
                _cfg.Save();
            };
            tpIdle.Controls.Add(_numIdleDurationSec);
            tpIdle.Controls.Add(new Label { Text = "detik → Turbo OFF", Left = 328, Top = 128, Width = 130, Height = 22, ForeColor = Color.Gray });

            // === Kondisi AKTIF (Turbo ON) ===
            tpIdle.Controls.Add(new Label
            {
                Text = "Kondisi AKTIF → Turbo ON",
                Left = 12, Top = 168, Width = 400, Height = 20,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            });

            tpIdle.Controls.Add(new Label { Text = "CPU load > ", Left = 20, Top = 196, Width = 70, Height = 22 });
            _numActiveLoadPct = new NumericUpDown
            {
                Left = 90, Top = 194, Width = 60,
                Minimum = 5, Maximum = 100,
                Value = _cfg.ActiveLoadThresholdPct
            };
            _numActiveLoadPct.ValueChanged += (s, e) =>
            {
                _cfg.ActiveLoadThresholdPct = (int)_numActiveLoadPct.Value;
                _cfg.Save();
            };
            tpIdle.Controls.Add(_numActiveLoadPct);
            tpIdle.Controls.Add(new Label { Text = "%", Left = 154, Top = 196, Width = 30, Height = 22 });
            tpIdle.Controls.Add(new Label { Text = "selama", Left = 184, Top = 196, Width = 60, Height = 22 });
            _numActiveDurationSec = new NumericUpDown
            {
                Left = 244, Top = 194, Width = 80,
                Minimum = 1, Maximum = 600,
                Value = _cfg.ActiveDurationSec
            };
            _numActiveDurationSec.ValueChanged += (s, e) =>
            {
                _cfg.ActiveDurationSec = (int)_numActiveDurationSec.Value;
                _cfg.Save();
            };
            tpIdle.Controls.Add(_numActiveDurationSec);
            tpIdle.Controls.Add(new Label { Text = "detik → Turbo ON", Left = 328, Top = 196, Width = 130, Height = 22, ForeColor = Color.Gray });

            // === Toast setting ===
            _chkIdleShowToast = new CheckBox
            {
                Text = "Tampilkan toast saat idle-auto ON/OFF",
                Left = 20, Top = 224, Width = 400, Height = 24,
                Checked = _cfg.IdleShowToast
            };
            _chkIdleShowToast.CheckedChanged += (s, e) =>
            {
                _cfg.IdleShowToast = _chkIdleShowToast.Checked;
                _cfg.Save();
            };
            tpIdle.Controls.Add(_chkIdleShowToast);

            // === Override info ===
            tpIdle.Controls.Add(new Label
            {
                Text = "Override manual: klik tombol TURBO ON/OFF manual → auto-disable pause 5 menit.",
                Left = 12, Top = 252, Width = 430, Height = 20,
                ForeColor = Color.Gray
            });

            // === Zone info ===
            tpIdle.Controls.Add(new Label
            {
                Text = "Zona antara threshold (load 10-30%): state tidak berubah.",
                Left = 12, Top = 275, Width = 430, Height = 20,
                ForeColor = Color.Gray
            });

            // === Rekomendasi ===
            tpIdle.Controls.Add(new Label
            {
                Text = "Rekomendasi:\n" +
                       "• Idle: 10% selama 120s (2 menit) → Turbo OFF\n" +
                       "• Aktif: 30% selama 5s → Turbo ON\n" +
                       "• Kombinasi dengan Auto-Switch: game OFF, idle OFF, kerja ON",
                Left = 12, Top = 307, Width = 430, Height = 80,
                ForeColor = Color.DimGray, Font = new Font("Segoe UI", 9f)
            });
        }
    }
}