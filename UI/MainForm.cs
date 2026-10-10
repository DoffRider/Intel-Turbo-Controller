using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TurboToggle
{
    partial class MainForm : Form
    {
        public event Action OnHotkeyChanged;
        public event Action OnExitRequested;

        readonly Config _cfg;
        readonly TaskbarOverlay _overlay;
        readonly TrayContext _ctx;

        Label _statusLabel;
        Panel _statusDot;
        Button _btnOn, _btnOff;
        TabControl _tabs;

        public MainForm(Config cfg, TaskbarOverlay overlay, TrayContext ctx)
        {
            _cfg = cfg;
            _overlay = overlay;
            _ctx = ctx;

            Text = "Turbo Boost Control";
            Width = 540; Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; MinimizeBox = true;
            Icon = CreateFormIcon();

            _statusDot = new Panel { Left = 20, Top = 22, Width = 22, Height = 22 };
            _statusDot.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush((Color)(_statusDot.Tag ?? Color.Gray)))
                    g.FillEllipse(b, 0, 0, _statusDot.Width - 1, _statusDot.Height - 1);
            };
            Controls.Add(_statusDot);

            _statusLabel = new Label
            {
                Left = 52, Top = 24, Width = 430, Height = 24,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Text = "Status: -"
            };
            Controls.Add(_statusLabel);

            _btnOn = new Button
            {
                Text = "TURBO ON", Left = 20, Top = 60, Width = 230, Height = 56,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(46, 204, 113), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnOn.FlatAppearance.BorderSize = 0;
            _btnOn.Click += (s, e) => _ctx.SetTurbo(true, "manual");
            Controls.Add(_btnOn);

            _btnOff = new Button
            {
                Text = "TURBO OFF", Left = 258, Top = 60, Width = 230, Height = 56,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(231, 76, 60), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnOff.FlatAppearance.BorderSize = 0;
            _btnOff.Click += (s, e) => _ctx.SetTurbo(false, "manual");
            Controls.Add(_btnOff);

            _tabs = new TabControl { Left = 20, Top = 130, Width = 468, Height = 400 };
            Controls.Add(_tabs);

            BuildAutoSwitchTab();
            BuildOverlayTab();
            BuildTrayTab();
            BuildHotkeyTab();
            BuildBehaviorTab();
            BuildAlertTab();
            BuildLogTab();
            BuildIdleTab();
            BuildBoostTab();
            
            var btnHide = new Button { Text = "Hide to Tray", Left = 20, Top = 542, Width = 130, Height = 30 };
            btnHide.Click += (s, e) => Hide();
            Controls.Add(btnHide);

            var btnExit = new Button { Text = "Exit App", Left = 358, Top = 542, Width = 130, Height = 30 };
            btnExit.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Keluar dari Turbo Toggle?", "Konfirmasi",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    if (OnExitRequested != null) OnExitRequested();
                }
            };
            Controls.Add(btnExit);

            FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            };

            RefreshStatus();
        }

        CheckBox MakeCheck(Control parent, string text, int top, bool value, Action<bool> onChange)
        {
            var cb = new CheckBox
            {
                Text = text, Left = 20, Top = top, Width = 200, Height = 22,
                Checked = value
            };
            cb.CheckedChanged += (s, e) => onChange(cb.Checked);
            parent.Controls.Add(cb);
            return cb;
        }

        public void RefreshStatus()
        {
            bool on = Turbo.Get();
            _statusDot.Tag = on ? Color.FromArgb(46, 204, 113) : Color.FromArgb(231, 76, 60);
            _statusDot.Invalidate();
            _statusLabel.Text = "Turbo Boost: " + (on ? "ON" : "OFF");
            _statusLabel.ForeColor = on ? Color.FromArgb(39, 174, 96) : Color.FromArgb(192, 57, 43);
            _btnOn.Enabled = !on;
            _btnOff.Enabled = on;
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
}