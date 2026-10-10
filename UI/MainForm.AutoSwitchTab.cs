using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TurboToggle
{
    partial class MainForm
    {
        CheckBox _chkAuto;
        NumericUpDown _numInterval;
        ListBox _lstTriggers;

        void BuildAutoSwitchTab()
        {
            var tpAuto = new TabPage("Auto-Switch");
            _tabs.TabPages.Add(tpAuto);

            _chkAuto = new CheckBox
            {
                Text = "Aktifkan Auto-Switch",
                Left = 12, Top = 12, Width = 400, Height = 24,
                Checked = _cfg.AutoSwitchEnabled
            };
            _chkAuto.CheckedChanged += (s, e) => { _cfg.AutoSwitchEnabled = _chkAuto.Checked; _cfg.Save(); };
            tpAuto.Controls.Add(_chkAuto);

            tpAuto.Controls.Add(new Label { Text = "Cek tiap (ms):", Left = 12, Top = 44, Width = 90, Height = 22 });
            _numInterval = new NumericUpDown
            {
                Left = 106, Top = 42, Width = 90,
                Minimum = 500, Maximum = 60000, Increment = 500,
                Value = _cfg.PollIntervalMs
            };
            _numInterval.ValueChanged += (s, e) => { _cfg.PollIntervalMs = (int)_numInterval.Value; _cfg.Save(); };
            tpAuto.Controls.Add(_numInterval);

            tpAuto.Controls.Add(new Label { Text = "Aplikasi trigger (satu per baris di list):", Left = 12, Top = 78, Width = 400, Height = 20 });
            _lstTriggers = new ListBox
            {
                Left = 12, Top = 100, Width = 430, Height = 175,
                Font = new Font("Consolas", 9.5f)
            };
            foreach (var t in _cfg.Triggers) _lstTriggers.Items.Add(t);
            tpAuto.Controls.Add(_lstTriggers);

            var btnAddExe = new Button { Text = "Browse .exe...", Left = 12, Top = 285, Width = 135, Height = 28 };
            btnAddExe.Click += (s, e) => AddFromExe();
            tpAuto.Controls.Add(btnAddExe);

            var btnAddWin = new Button { Text = "Capture Window...", Left = 155, Top = 285, Width = 155, Height = 28 };
            btnAddWin.Click += (s, e) => AddFromWindow();
            tpAuto.Controls.Add(btnAddWin);

            var btnRemove = new Button { Text = "Remove", Left = 318, Top = 285, Width = 124, Height = 28 };
            btnRemove.Click += (s, e) =>
            {
                if (_lstTriggers.SelectedIndex >= 0)
                {
                    _cfg.Triggers.RemoveAt(_lstTriggers.SelectedIndex);
                    _lstTriggers.Items.RemoveAt(_lstTriggers.SelectedIndex);
                    _cfg.Save();
                }
            };
            tpAuto.Controls.Add(btnRemove);

            tpAuto.Controls.Add(new Label
            {
                Text = "Turbo OFF saat app trigger berjalan, Turbo ON saat tidak ada trigger aktif.",
                Left = 12, Top = 320, Width = 430, Height = 20, ForeColor = Color.Gray
            });
        }

        void AddFromExe()
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "Executable (*.exe)|*.exe|All files|*.*",
                Title = "Pilih file .exe aplikasi/game"
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string name = Path.GetFileNameWithoutExtension(dlg.FileName).ToLowerInvariant();
                    if (!_cfg.Triggers.Contains(name))
                    {
                        _cfg.Triggers.Add(name);
                        _lstTriggers.Items.Add(name);
                        _cfg.Save();
                    }
                    else
                    {
                        MessageBox.Show(this, "Sudah ada di list.", "Info",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        void AddFromWindow()
        {
            using (var dlg = new WindowPickerForm())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Selected != null)
                {
                    string name = dlg.Selected.ProcessName.ToLowerInvariant();
                    if (!_cfg.Triggers.Contains(name))
                    {
                        _cfg.Triggers.Add(name);
                        _lstTriggers.Items.Add(name);
                        _cfg.Save();
                    }
                    else
                    {
                        MessageBox.Show(this, "Sudah ada di list.", "Info",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }
    }
}