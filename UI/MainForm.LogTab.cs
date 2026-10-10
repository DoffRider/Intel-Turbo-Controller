using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TurboToggle
{
    partial class MainForm
    {
        CheckBox _chkLogEnabled;
        TextBox _txtLogFolder;
        ComboBox _cmbLogInterval;
        CheckBox _chkLogAutoRotate;
        Label _lblLogStatus;

        void BuildLogTab()
        {
            var tpLog = new TabPage("Log");
            _tabs.TabPages.Add(tpLog);

            // ============ Enable ============
            _chkLogEnabled = new CheckBox
            {
                Text = "Aktifkan logging sensor ke CSV",
                Left = 12, Top = 12, Width = 400, Height = 24,
                Checked = _cfg.LogEnabled
            };
            _chkLogEnabled.CheckedChanged += (s, e) =>
            {
                _cfg.LogEnabled = _chkLogEnabled.Checked;
                _cfg.Save();
                _ctx.ApplyLogSetting();
                UpdateLogStatus();
            };
            tpLog.Controls.Add(_chkLogEnabled);

            // ============ Folder ============
            tpLog.Controls.Add(new Label
            {
                Text = "Folder output:",
                Left = 12, Top = 48, Width = 90, Height = 20,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            });

            _txtLogFolder = new TextBox
            {
                Left = 12, Top = 72, Width = 340, Height = 24,
                Text = _cfg.LogFolder,
                ReadOnly = true
            };
            tpLog.Controls.Add(_txtLogFolder);

            var btnBrowseFolder = new Button { Text = "Pilih...", Left = 358, Top = 70, Width = 70, Height = 26 };
            btnBrowseFolder.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "Pilih folder untuk log";
                    dlg.SelectedPath = _cfg.LogFolder;
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        _cfg.LogFolder = dlg.SelectedPath;
                        _cfg.Save();
                        _txtLogFolder.Text = dlg.SelectedPath;
                        _ctx.ApplyLogSetting();
                    }
                }
            };
            tpLog.Controls.Add(btnBrowseFolder);

            var btnOpenFolder = new Button { Text = "Open", Left = 12, Top = 104, Width = 80, Height = 26 };
            btnOpenFolder.Click += (s, e) =>
            {
                try { Process.Start("explorer.exe", "\"" + _cfg.LogFolder + "\""); } catch { }
            };
            tpLog.Controls.Add(btnOpenFolder);

            var btnOpenCurrent = new Button { Text = "Open Current Log", Left = 100, Top = 104, Width = 140, Height = 26 };
            btnOpenCurrent.Click += (s, e) =>
            {
                try
                {
                    string path = _ctx.GetCurrentLogPath();
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        Process.Start("notepad.exe", "\"" + path + "\"");
                    else
                        MessageBox.Show(this, "Belum ada file log aktif.", "Info",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Gagal buka log: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            tpLog.Controls.Add(btnOpenCurrent);

            // ============ Interval ============
            tpLog.Controls.Add(new Label
            {
                Text = "Interval log (ms):",
                Left = 12, Top = 148, Width = 120, Height = 20,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            });

            _cmbLogInterval = new ComboBox
            {
                Left = 12, Top = 172, Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbLogInterval.Items.AddRange(new object[] { "500", "1000", "2000", "5000", "10000", "30000", "60000" });
            string intervalStr = _cfg.LogIntervalMs.ToString();
            int intIdx = _cmbLogInterval.Items.IndexOf(intervalStr);
            if (intIdx < 0) { _cmbLogInterval.Items.Add(intervalStr); intIdx = _cmbLogInterval.Items.Count - 1; }
            _cmbLogInterval.SelectedIndex = intIdx;
            _cmbLogInterval.SelectedIndexChanged += (s, e) =>
            {
                int ms;
                if (int.TryParse(_cmbLogInterval.SelectedItem.ToString(), out ms))
                {
                    _cfg.LogIntervalMs = ms;
                    _cfg.Save();
                    _ctx.ApplyLogSetting();
                }
            };
            tpLog.Controls.Add(_cmbLogInterval);
            tpLog.Controls.Add(new Label
            {
                Text = "(1000 = 1 detik, 5000 = 5 detik)",
                Left = 140, Top = 174, Width = 240, Height = 20, ForeColor = Color.Gray
            });

            // ============ Auto rotate ============
            _chkLogAutoRotate = new CheckBox
            {
                Text = "Auto-rotate harian (file baru setiap hari)",
                Left = 12, Top = 208, Width = 400, Height = 24,
                Checked = _cfg.LogAutoRotateDaily
            };
            _chkLogAutoRotate.CheckedChanged += (s, e) =>
            {
                _cfg.LogAutoRotateDaily = _chkLogAutoRotate.Checked;
                _cfg.Save();
            };
            tpLog.Controls.Add(_chkLogAutoRotate);

            // ============ Status ============
            _lblLogStatus = new Label
            {
                Left = 12, Top = 248, Width = 430, Height = 100,
                ForeColor = Color.DimGray,
                Font = new Font("Consolas", 9f)
            };
            tpLog.Controls.Add(_lblLogStatus);

            // ============ Tips ============
            tpLog.Controls.Add(new Label
            {
                Text = "Format CSV: Timestamp, CPU Temp, Freq, Load, Power, Fan, Volt,\n" +
                       "GPU Temp, Freq, Load, Hotspot, Power, VRAM, RAM, MB Temp,\n" +
                       "Net Down, Net Up, Disk Act, Uptime, Turbo State",
                Left = 12, Top = 360, Width = 430, Height = 60, ForeColor = Color.Gray
            });

            // Update status saat tab dibuka
            UpdateLogStatus();
        }

        void UpdateLogStatus()
        {
            try
            {
                if (_lblLogStatus == null) return;
                string path = _ctx.GetCurrentLogPath();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    double kb = fi.Length / 1024.0;
                    string sizeStr = kb < 1024 ? kb.ToString("0.0") + " KB" : (kb / 1024).ToString("0.00") + " MB";
                    _lblLogStatus.Text =
                        "Status   : " + (_cfg.LogEnabled ? "AKTIF ✅" : "nonaktif") + "\n" +
                        "File     : " + Path.GetFileName(path) + "\n" +
                        "Size     : " + sizeStr + "\n" +
                        "Interval : " + _cfg.LogIntervalMs + " ms";
                }
                else
                {
                    _lblLogStatus.Text =
                        "Status   : " + (_cfg.LogEnabled ? "AKTIF ✅" : "nonaktif") + "\n" +
                        "File     : (belum dibuat)\n" +
                        "Folder   : " + _cfg.LogFolder;
                }
            }
            catch { }
        }

        public void RefreshLogStatus()
        {
            UpdateLogStatus();
        }
    }
}