using System;
using System.Drawing;
using System.Windows.Forms;

namespace TurboToggle
{
    partial class MainForm
    {
        void BuildTrayTab()
        {
            var tpTray = new TabPage("Tray Icon");
            _tabs.TabPages.Add(tpTray);
            tpTray.AutoScroll = true;

            // ================= Checkbox enabled =================
            var chkTrayEnabled = new CheckBox
            {
                Text = "Tampilkan info sensor di ikon tray",
                Left = 12, Top = 12, Width = 430, Height = 24,
                Checked = _cfg.TrayInfoEnabled
            };
            chkTrayEnabled.CheckedChanged += (s, e) =>
            {
                _cfg.TrayInfoEnabled = chkTrayEnabled.Checked;
                _cfg.Save();
                _ctx.RebuildSensorIcons();
            };
            tpTray.Controls.Add(chkTrayEnabled);

            // ================= CPU group =================
            var trayGrpCpu = new GroupBox { Text = "CPU", Left = 12, Top = 44, Width = 220, Height = 170 };
            tpTray.Controls.Add(trayGrpCpu);
            Action<Control, string, bool, int, Action<bool>> mkCpu = (parent, text, val, top, oc) =>
            {
                var cb = new CheckBox { Text = text, Left = 10, Top = top, Width = 200, Height = 22, Checked = val };
                cb.CheckedChanged += (s, e) => { oc(cb.Checked); _ctx.RebuildSensorIcons(); };
                parent.Controls.Add(cb);
            };
            mkCpu(trayGrpCpu, "CPU Temp",   _cfg.TrayShowCpuTemp,  16, v => { _cfg.TrayShowCpuTemp = v;   _cfg.Save(); });
            mkCpu(trayGrpCpu, "CPU Freq",   _cfg.TrayShowCpuFreq,  38, v => { _cfg.TrayShowCpuFreq = v;   _cfg.Save(); });
            mkCpu(trayGrpCpu, "CPU Load",   _cfg.TrayShowCpuLoad,  60, v => { _cfg.TrayShowCpuLoad = v;   _cfg.Save(); });
            mkCpu(trayGrpCpu, "CPU Power",  _cfg.TrayShowCpuPower, 82, v => { _cfg.TrayShowCpuPower = v;  _cfg.Save(); });
            mkCpu(trayGrpCpu, "CPU Fan",   _cfg.TrayShowCpuFan,   104, v => { _cfg.TrayShowCpuFan = v;    _cfg.Save(); });
            mkCpu(trayGrpCpu, "CPU Volt",  _cfg.TrayShowCpuVolt,  126, v => { _cfg.TrayShowCpuVolt = v;   _cfg.Save(); });

            // ================= GPU group =================
            var trayGrpGpu = new GroupBox { Text = "GPU", Left = 240, Top = 44, Width = 220, Height = 170 };
            tpTray.Controls.Add(trayGrpGpu);
            Action<Control, string, bool, int, Action<bool>> mkGpu = (parent, text, val, top, oc) =>
            {
                var cb = new CheckBox { Text = text, Left = 10, Top = top, Width = 200, Height = 22, Checked = val };
                cb.CheckedChanged += (s, e) => { oc(cb.Checked); _ctx.RebuildSensorIcons(); };
                parent.Controls.Add(cb);
            };
            mkGpu(trayGrpGpu, "GPU Temp",    _cfg.TrayShowGpuTemp,    16, v => { _cfg.TrayShowGpuTemp = v;     _cfg.Save(); });
            mkGpu(trayGrpGpu, "GPU Freq",    _cfg.TrayShowGpuFreq,    38, v => { _cfg.TrayShowGpuFreq = v;     _cfg.Save(); });
            mkGpu(trayGrpGpu, "GPU Load",    _cfg.TrayShowGpuLoad,    60, v => { _cfg.TrayShowGpuLoad = v;     _cfg.Save(); });
            mkGpu(trayGrpGpu, "GPU Hotspot", _cfg.TrayShowGpuHotspot, 82, v => { _cfg.TrayShowGpuHotspot = v;  _cfg.Save(); });
            mkGpu(trayGrpGpu, "GPU Power",   _cfg.TrayShowGpuPower,  104, v => { _cfg.TrayShowGpuPower = v;    _cfg.Save(); });
            mkGpu(trayGrpGpu, "VRAM",        _cfg.TrayShowGpuVram,   126, v => { _cfg.TrayShowGpuVram = v;     _cfg.Save(); });

            // ================= System group =================
            var trayGrpSys = new GroupBox { Text = "System", Left = 12, Top = 222, Width = 448, Height = 130 };
            tpTray.Controls.Add(trayGrpSys);
            Action<string, bool, int, int, Action<bool>> mkSys = (text, val, left, top, oc) =>
            {
                var cb = new CheckBox { Text = text, Left = left, Top = top, Width = 130, Height = 22, Checked = val };
                cb.CheckedChanged += (s, e) => { oc(cb.Checked); _ctx.RebuildSensorIcons(); };
                trayGrpSys.Controls.Add(cb);
            };
            mkSys("RAM",       _cfg.TrayShowRam,       10,  16, v => { _cfg.TrayShowRam = v;        _cfg.Save(); });
            mkSys("MB Temp",   _cfg.TrayShowMbTemp,    10,  38, v => { _cfg.TrayShowMbTemp = v;    _cfg.Save(); });
            mkSys("Net ↓",     _cfg.TrayShowNetDown,   10,  60, v => { _cfg.TrayShowNetDown = v;   _cfg.Save(); });
            mkSys("Net ↑",     _cfg.TrayShowNetUp,     10,  82, v => { _cfg.TrayShowNetUp = v;     _cfg.Save(); });
            mkSys("Disk Act",  _cfg.TrayShowDiskAct,  150,  16, v => { _cfg.TrayShowDiskAct = v;   _cfg.Save(); });
            mkSys("Uptime",    _cfg.TrayShowUptime,   150,  38, v => { _cfg.TrayShowUptime = v;    _cfg.Save(); });

            // ================= Warna teks group =================
            var grpColor = new GroupBox { Text = "Warna teks ikon", Left = 12, Top = 360, Width = 448, Height = 110 };
            tpTray.Controls.Add(grpColor);

            // Baris 1: CPU (kiri) + GPU (kanan)
            grpColor.Controls.Add(new Label { Text = "CPU:", Left = 10, Top = 24, Width = 40, Height = 20 });
            var cpuColorPreview = new Panel
            {
                Left = 56, Top = 22, Width = 30, Height = 22,
                BackColor = Color.FromArgb(_cfg.TrayCpuColor),
                BorderStyle = BorderStyle.FixedSingle
            };
            grpColor.Controls.Add(cpuColorPreview);
            var btnCpuColor = new Button { Text = "Pilih...", Left = 96, Top = 20, Width = 80, Height = 26 };
            btnCpuColor.Click += (s, e) =>
            {
                using (var dlg = new ColorDialog { Color = Color.FromArgb(_cfg.TrayCpuColor), FullOpen = true })
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        _cfg.TrayCpuColor = dlg.Color.ToArgb();
                        cpuColorPreview.BackColor = dlg.Color;
                        _cfg.Save();
                    }
            };
            grpColor.Controls.Add(btnCpuColor);

            grpColor.Controls.Add(new Label { Text = "GPU:", Left = 220, Top = 24, Width = 40, Height = 20 });
            var gpuColorPreview = new Panel
            {
                Left = 266, Top = 22, Width = 30, Height = 22,
                BackColor = Color.FromArgb(_cfg.TrayGpuColor),
                BorderStyle = BorderStyle.FixedSingle
            };
            grpColor.Controls.Add(gpuColorPreview);
            var btnGpuColor = new Button { Text = "Pilih...", Left = 306, Top = 20, Width = 80, Height = 26 };
            btnGpuColor.Click += (s, e) =>
            {
                using (var dlg = new ColorDialog { Color = Color.FromArgb(_cfg.TrayGpuColor), FullOpen = true })
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        _cfg.TrayGpuColor = dlg.Color.ToArgb();
                        gpuColorPreview.BackColor = dlg.Color;
                        _cfg.Save();
                    }
            };
            grpColor.Controls.Add(btnGpuColor);

            // Baris 2: SYS (kiri)
            grpColor.Controls.Add(new Label { Text = "SYS:", Left = 10, Top = 54, Width = 40, Height = 20 });
            var sysColorPreview = new Panel
            {
                Left = 56, Top = 52, Width = 30, Height = 22,
                BackColor = Color.FromArgb(_cfg.TraySysColor),
                BorderStyle = BorderStyle.FixedSingle
            };
            grpColor.Controls.Add(sysColorPreview);
            var btnSysColor = new Button { Text = "Pilih...", Left = 96, Top = 50, Width = 80, Height = 26 };
            btnSysColor.Click += (s, e) =>
            {
                using (var dlg = new ColorDialog { Color = Color.FromArgb(_cfg.TraySysColor), FullOpen = true })
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        _cfg.TraySysColor = dlg.Color.ToArgb();
                        sysColorPreview.BackColor = dlg.Color;
                        _cfg.Save();
                    }
            };
            grpColor.Controls.Add(btnSysColor);

            // ================= Text Style group =================
            var grpStyle = new GroupBox { Text = "Text Style", Left = 12, Top = 480, Width = 448, Height = 220 };
            tpTray.Controls.Add(grpStyle);

            // Font
            grpStyle.Controls.Add(new Label { Text = "Font:", Left = 10, Top = 24, Width = 80, Height = 20 });
            var cmbFont = new ComboBox
            {
                Left = 100, Top = 22, Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbFont.Items.AddRange(new object[]
            {
                "Segoe UI",
                "Segoe UI Semibold",
                "Segoe UI Black",
                "Arial",
                "Arial Black",
                "Bahnschrift",
                "Bahnschrift SemiBold",
                "Consolas",
                "Tahoma",
                "Verdana",
                "Impact"
            });
            int fontIdx = cmbFont.Items.IndexOf(_cfg.TrayFontFamily);
            if (fontIdx < 0)
            {
                cmbFont.Items.Add(_cfg.TrayFontFamily);
                fontIdx = cmbFont.Items.Count - 1;
            }
            cmbFont.SelectedIndex = fontIdx;
            cmbFont.SelectedIndexChanged += (s, e) =>
            {
                _cfg.TrayFontFamily = cmbFont.SelectedItem.ToString();
                _cfg.Save();
            };
            grpStyle.Controls.Add(cmbFont);

            // Style
            grpStyle.Controls.Add(new Label { Text = "Style:", Left = 10, Top = 52, Width = 80, Height = 20 });
            var cmbStyle = new ComboBox
            {
                Left = 100, Top = 50, Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbStyle.Items.AddRange(new object[] { "Regular", "Bold", "Italic", "Bold Italic" });
            cmbStyle.SelectedIndex = Math.Max(0, Math.Min(3, _cfg.TrayFontStyle));
            cmbStyle.SelectedIndexChanged += (s, e) =>
            {
                _cfg.TrayFontStyle = cmbStyle.SelectedIndex;
                _cfg.Save();
            };
            grpStyle.Controls.Add(cmbStyle);

            // Size
            grpStyle.Controls.Add(new Label { Text = "Bitmap size:", Left = 10, Top = 80, Width = 80, Height = 20 });
            var cmbSize = new ComboBox
            {
                Left = 100, Top = 78, Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbSize.Items.AddRange(new object[] { "32", "64", "128", "192", "256" });
            string sizeStr = _cfg.TrayIconSize.ToString();
            int sizeIdx = cmbSize.Items.IndexOf(sizeStr);
            if (sizeIdx < 0) { cmbSize.Items.Add(sizeStr); sizeIdx = cmbSize.Items.Count - 1; }
            cmbSize.SelectedIndex = sizeIdx;
            cmbSize.SelectedIndexChanged += (s, e) =>
            {
                int sz;
                if (int.TryParse(cmbSize.SelectedItem.ToString(), out sz))
                {
                    _cfg.TrayIconSize = sz;
                    _cfg.Save();
                }
            };
            grpStyle.Controls.Add(cmbSize);
            grpStyle.Controls.Add(new Label { Text = "(resolusi render)", Left = 210, Top = 80, Width = 120, Height = 20, ForeColor = Color.Gray });

            // Margin
            grpStyle.Controls.Add(new Label { Text = "Margin (%):", Left = 10, Top = 108, Width = 80, Height = 20 });
            var numMargin = new NumericUpDown
            {
                Left = 100, Top = 106, Width = 60,
                Minimum = 0, Maximum = 20,
                Value = _cfg.TrayMarginPct
            };
            numMargin.ValueChanged += (s, e) =>
            {
                _cfg.TrayMarginPct = (int)numMargin.Value;
                _cfg.Save();
            };
            grpStyle.Controls.Add(numMargin);

            // Outline
            grpStyle.Controls.Add(new Label { Text = "Outline (px):", Left = 10, Top = 136, Width = 80, Height = 20 });
            var numOutline = new NumericUpDown
            {
                Left = 100, Top = 134, Width = 60,
                Minimum = 0, Maximum = 40,
                Value = _cfg.TrayOutlineWidth
            };
            numOutline.ValueChanged += (s, e) =>
            {
                _cfg.TrayOutlineWidth = (int)numOutline.Value;
                _cfg.Save();
            };
            grpStyle.Controls.Add(numOutline);

            var outlineColorPreview = new Panel
            {
                Left = 170, Top = 134, Width = 30, Height = 22,
                BackColor = Color.FromArgb(_cfg.TrayOutlineColor),
                BorderStyle = BorderStyle.FixedSingle
            };
            grpStyle.Controls.Add(outlineColorPreview);
            var btnOutlineColor = new Button { Text = "Warna...", Left = 210, Top = 132, Width = 90, Height = 26 };
            btnOutlineColor.Click += (s, e) =>
            {
                using (var dlg = new ColorDialog { Color = Color.FromArgb(_cfg.TrayOutlineColor), FullOpen = true })
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        _cfg.TrayOutlineColor = dlg.Color.ToArgb();
                        outlineColorPreview.BackColor = dlg.Color;
                        _cfg.Save();
                    }
            };
            grpStyle.Controls.Add(btnOutlineColor);

            // Gradient
            var chkGradient = new CheckBox
            {
                Text = "Gunakan gradient vertical",
                Left = 10, Top = 164, Width = 200, Height = 22,
                Checked = _cfg.TrayUseGradient
            };
            chkGradient.CheckedChanged += (s, e) =>
            {
                _cfg.TrayUseGradient = chkGradient.Checked;
                _cfg.Save();
            };
            grpStyle.Controls.Add(chkGradient);

            grpStyle.Controls.Add(new Label { Text = "Strength (%):", Left = 220, Top = 166, Width = 70, Height = 20 });
            var numGradStrength = new NumericUpDown
            {
                Left = 296, Top = 164, Width = 60,
                Minimum = 0, Maximum = 60,
                Value = _cfg.TrayGradientStrength
            };
            numGradStrength.ValueChanged += (s, e) =>
            {
                _cfg.TrayGradientStrength = (int)numGradStrength.Value;
                _cfg.Save();
            };
            grpStyle.Controls.Add(numGradStrength);

            // Tips
            grpStyle.Controls.Add(new Label
            {
                Text = "Tips: perubahan berlaku saat sensor tick berikutnya (≤1 detik).",
                Left = 10, Top = 190, Width = 430, Height = 20,
                ForeColor = Color.Gray
            });
        }
    }
}