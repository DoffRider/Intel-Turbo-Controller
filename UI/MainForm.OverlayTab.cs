using System;
using System.Drawing;
using System.Windows.Forms;

namespace TurboToggle
{
    partial class MainForm
    {
        CheckBox _chkOverlayEnabled;
        CheckBox _chkCpuTemp, _chkCpuFreq, _chkCpuLoad, _chkCpuPower, _chkCpuFan, _chkCpuVolt;
        CheckBox _chkGpuTemp, _chkGpuFreq, _chkGpuLoad, _chkGpuHotspot, _chkGpuPower, _chkGpuVram;

        void BuildOverlayTab()
        {
            var tpOver = new TabPage("Taskbar Overlay");
            _tabs.TabPages.Add(tpOver);

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

            var grpCpu = new GroupBox { Text = "CPU", Left = 12, Top = 66, Width = 220, Height = 170 };
            tpOver.Controls.Add(grpCpu);
            _chkCpuTemp  = MakeCheck(grpCpu, "CPU Temp",  16, _cfg.ShowCpuTemp,  v => { _cfg.ShowCpuTemp = v;  _cfg.Save(); });
            _chkCpuFreq  = MakeCheck(grpCpu, "CPU Freq",  38, _cfg.ShowCpuFreq,  v => { _cfg.ShowCpuFreq = v;  _cfg.Save(); });
            _chkCpuLoad  = MakeCheck(grpCpu, "CPU Load",  60, _cfg.ShowCpuLoad,  v => { _cfg.ShowCpuLoad = v;  _cfg.Save(); });
            _chkCpuPower = MakeCheck(grpCpu, "CPU Power", 82, _cfg.ShowCpuPower, v => { _cfg.ShowCpuPower = v; _cfg.Save(); });
            _chkCpuFan   = MakeCheck(grpCpu, "CPU Fan",  104, _cfg.ShowCpuFan,   v => { _cfg.ShowCpuFan = v;   _cfg.Save(); });
            _chkCpuVolt  = MakeCheck(grpCpu, "CPU Volt", 126, _cfg.ShowCpuVolt,  v => { _cfg.ShowCpuVolt = v;  _cfg.Save(); });

            var grpGpu = new GroupBox { Text = "GPU", Left = 240, Top = 66, Width = 220, Height = 170 };
            tpOver.Controls.Add(grpGpu);
            _chkGpuTemp    = MakeCheck(grpGpu, "GPU Temp",    16, _cfg.ShowGpuTemp,    v => { _cfg.ShowGpuTemp = v;    _cfg.Save(); });
            _chkGpuFreq    = MakeCheck(grpGpu, "GPU Freq",    38, _cfg.ShowGpuFreq,    v => { _cfg.ShowGpuFreq = v;    _cfg.Save(); });
            _chkGpuLoad    = MakeCheck(grpGpu, "GPU Load",    60, _cfg.ShowGpuLoad,    v => { _cfg.ShowGpuLoad = v;    _cfg.Save(); });
            _chkGpuHotspot = MakeCheck(grpGpu, "GPU Hotspot", 82, _cfg.ShowGpuHotspot, v => { _cfg.ShowGpuHotspot = v; _cfg.Save(); });
            _chkGpuPower   = MakeCheck(grpGpu, "GPU Power",  104, _cfg.ShowGpuPower,   v => { _cfg.ShowGpuPower = v;   _cfg.Save(); });
            _chkGpuVram    = MakeCheck(grpGpu, "VRAM",       126, _cfg.ShowGpuVram,    v => { _cfg.ShowGpuVram = v;    _cfg.Save(); });

            var grpSys = new GroupBox { Text = "System", Left = 12, Top = 244, Width = 448, Height = 130 };
            tpOver.Controls.Add(grpSys);
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
                            Color.FromArgb(_cfg.OverlaySysColor),
                            _cfg.OverlayOpacity);
                        _cfg.Save();
                    }
            };
            tpOver.Controls.Add(btnBg);

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
        }
    }
}