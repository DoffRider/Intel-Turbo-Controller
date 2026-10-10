using System;
using System.Drawing;
using System.Windows.Forms;

namespace TurboToggle
{
    partial class MainForm
    {
        CheckBox _chkNotify;
        CheckBox _chkTurboOnExit;

        void BuildBehaviorTab()
        {
            var tpBeh = new TabPage("Behavior");
            _tabs.TabPages.Add(tpBeh);

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
        }
    }
}