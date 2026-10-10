using System;
using System.Drawing;
using System.Windows.Forms;

namespace TurboToggle
{
    class ToastForm : Form
    {
        System.Windows.Forms.Timer _timer;

        public ToastForm(string title, string message, int durationMs)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(340, 86);
            BackColor = Color.FromArgb(40, 40, 40);

            var pnlAccent = new Panel { Left = 0, Top = 0, Width = 6, Height = 86, BackColor = Color.FromArgb(52, 152, 219) };
            Controls.Add(pnlAccent);

            var lblTitle = new Label
            {
                Text = title, Left = 18, Top = 10, Width = 310, Height = 22,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White, BackColor = Color.Transparent
            };
            Controls.Add(lblTitle);

            var lblMsg = new Label
            {
                Text = message, Left = 18, Top = 36, Width = 310, Height = 44,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(220, 220, 220), BackColor = Color.Transparent
            };
            Controls.Add(lblMsg);

            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 16, wa.Bottom - Height - 16);

            EventHandler dismiss = (s, e) => Close();
            Click += dismiss;
            pnlAccent.Click += dismiss;
            lblTitle.Click += dismiss;
            lblMsg.Click += dismiss;

            _timer = new System.Windows.Forms.Timer { Interval = durationMs };
            _timer.Tick += (s, e) => { _timer.Stop(); Close(); };
            _timer.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000;
                cp.ExStyle |= 0x00000080;
                return cp;
            }
        }
    }
}