using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TurboToggle
{
    class WindowPickerForm : Form
    {
        public WindowInfo Selected;
        ListBox _list;
        System.Windows.Forms.Timer _refreshTimer;
        bool _autoRefresh = true;

        public WindowPickerForm()
        {
            Text = "Pilih Window / Game yang Berjalan";
            Width = 560; Height = 420;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;

            Controls.Add(new Label
            {
                Text = "Pilih dari daftar window yang sedang terbuka (jalankan game dulu):",
                Left = 12, Top = 12, Width = 520, Height = 20
            });

            _list = new ListBox
            {
                Left = 12, Top = 38, Width = 520, Height = 300,
                Font = new Font("Consolas", 9.5f)
            };
            _list.DoubleClick += (s, e) => Accept();
            Controls.Add(_list);

            var btnRefresh = new Button { Text = "Refresh", Left = 12, Top = 346, Width = 90, Height = 28 };
            btnRefresh.Click += (s, e) => ReloadList();
            Controls.Add(btnRefresh);

            var chkAuto = new CheckBox
            {
                Text = "Auto-refresh tiap 2 detik",
                Left = 116, Top = 350, Width = 200, Height = 22,
                Checked = true
            };
            chkAuto.CheckedChanged += (s, e) => _autoRefresh = chkAuto.Checked;
            Controls.Add(chkAuto);

            var btnOk = new Button
            {
                Text = "OK", Left = 340, Top = 346, Width = 90, Height = 28,
                DialogResult = DialogResult.OK
            };
            btnOk.Click += (s, e) => Accept();
            Controls.Add(btnOk);

            var btnCancel = new Button
            {
                Text = "Cancel", Left = 440, Top = 346, Width = 90, Height = 28,
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _refreshTimer.Tick += (s, e) => { if (_autoRefresh) ReloadList(); };
            _refreshTimer.Start();

            ReloadList();
        }

        void Accept()
        {
            if (_list.SelectedItem is WindowInfo)
            {
                Selected = (WindowInfo)_list.SelectedItem;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(this, "Belum ada window yang dipilih.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        void ReloadList()
        {
            try
            {
                var sel = _list.SelectedItem as WindowInfo;
                string selKey = sel != null ? (sel.Title + "|" + sel.ProcessName) : null;

                var windows = WindowEnumerator.GetVisibleWindows()
                    .OrderBy(w => w.ProcessName).ThenBy(w => w.Title).ToList();

                _list.BeginUpdate();
                _list.Items.Clear();
                foreach (var w in windows) _list.Items.Add(w);
                _list.EndUpdate();

                if (selKey != null)
                {
                    for (int i = 0; i < _list.Items.Count; i++)
                    {
                        var w = (WindowInfo)_list.Items[i];
                        if ((w.Title + "|" + w.ProcessName) == selKey) { _list.SelectedIndex = i; break; }
                    }
                }
            }
            catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { _refreshTimer.Stop(); _refreshTimer.Dispose(); } catch { }
            base.OnFormClosed(e);
        }
    }
}