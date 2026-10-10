using System;
using System.Drawing;
using System.Runtime.InteropServices;   // <-- TAMBAH INI
using System.Windows.Forms;

namespace TurboToggle
{
    class TaskbarOverlay : Form
    {
        Label _lblCpu, _lblGpu, _lblSys;
        bool _drag = false;
        public event Action PositionChanged;
        public event Action AfterResize;
        Point _dragOffset;
        bool _vertical = false;
        int _vAlign = 1;

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOSIZE     = 0x0001;
        const uint SWP_NOMOVE     = 0x0002;
        const uint SWP_NOACTIVATE = 0x0010;
        const uint SWP_SHOWWINDOW = 0x0040;

        public void ForceTopMost()
        {
            if (!IsHandleCreated || IsDisposed) return;
            try
            {
                SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
            catch { }
        }

        public int VAlign
        {
            get { return _vAlign; }
            set { _vAlign = value; }
        }

        public bool Vertical
        {
            get { return _vertical; }
            set { _vertical = value; }
        }

        public TaskbarOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(280, 26);

            _lblCpu = new Label
            {
                AutoSize = true, Location = new Point(6, 3),
                Font = new Font("Consolas", 9.5f, FontStyle.Bold),
                Text = "CPU --", BackColor = Color.Transparent
            };
            _lblGpu = new Label
            {
                AutoSize = true, Location = new Point(6, 3),
                Font = new Font("Consolas", 9.5f, FontStyle.Bold),
                Text = "GPU --", BackColor = Color.Transparent
            };
            _lblSys = new Label
            {
                AutoSize = true, Location = new Point(6, 3),
                Font = new Font("Consolas", 9.5f, FontStyle.Bold),
                Text = "SYS --", BackColor = Color.Transparent
            };
            Controls.Add(_lblCpu);
            Controls.Add(_lblGpu);
            Controls.Add(_lblSys);

            MouseEventHandler down = (s, e) => { if (e.Button == MouseButtons.Left) { _drag = true; _dragOffset = e.Location; } };
            MouseEventHandler move = (s, e) => { if (_drag) Location = new Point(Location.X + e.X - _dragOffset.X, Location.Y + e.Y - _dragOffset.Y); };
            MouseEventHandler up = (s, e) =>
                {
                    bool wasDragging = _drag;
                    _drag = false;
                    if (wasDragging && PositionChanged != null) PositionChanged();
                };
                
            MouseDown += down; MouseMove += move; MouseUp += up;
            _lblCpu.MouseDown += down; _lblCpu.MouseMove += move; _lblCpu.MouseUp += up;
            _lblGpu.MouseDown += down; _lblGpu.MouseMove += move; _lblGpu.MouseUp += up;
            _lblSys.MouseDown += down; _lblSys.MouseMove += move; _lblSys.MouseUp += up;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000;
                return cp;
            }
        }

        public void SetItems(string cpuText, string gpuText, string sysText,
                             bool showCpu, bool showGpu, bool showSys)
        {
            _lblCpu.Text = cpuText;
            _lblGpu.Text = gpuText;
            _lblSys.Text = sysText;
            _lblCpu.Visible = showCpu;
            _lblGpu.Visible = showGpu;
            _lblSys.Visible = showSys;

            if (_vertical)
            {
                int contentW = 0;
                if (showCpu && _lblCpu.PreferredWidth > contentW) contentW = _lblCpu.PreferredWidth;
                if (showGpu && _lblGpu.PreferredWidth > contentW) contentW = _lblGpu.PreferredWidth;
                if (showSys && _lblSys.PreferredWidth > contentW) contentW = _lblSys.PreferredWidth;

                int pad = 6;
                int totalW = contentW + pad * 2;
                if (totalW < 60) totalW = 60;
                if (totalW > 800) totalW = 800;

                Func<Label, int> calcX = lbl =>
                {
                    if (_vAlign == 0) return pad;
                    if (_vAlign == 2) return totalW - pad - lbl.PreferredWidth;
                    return (totalW - lbl.PreferredWidth) / 2;
                };

                int y = 3;
                if (showCpu) { _lblCpu.Location = new Point(calcX(_lblCpu), y); y += _lblCpu.PreferredHeight + 2; }
                if (showGpu) { _lblGpu.Location = new Point(calcX(_lblGpu), y); y += _lblGpu.PreferredHeight + 2; }
                if (showSys) { _lblSys.Location = new Point(calcX(_lblSys), y); y += _lblSys.PreferredHeight + 6; }

                if (y < 22) y = 22;
                if (y > 400) y = 400;
                Size = new Size(totalW, y);
            }
            else
            {
                int x = 6;
                if (showCpu) { _lblCpu.Location = new Point(x, 3); x += _lblCpu.PreferredWidth + 10; }
                if (showGpu) { _lblGpu.Location = new Point(x, 3); x += _lblGpu.PreferredWidth + 10; }
                if (showSys) { _lblSys.Location = new Point(x, 3); x += _lblSys.PreferredWidth + 6; }

                int h = 22;
                if (_lblCpu.PreferredHeight > h) h = _lblCpu.PreferredHeight + 6;
                if (_lblGpu.PreferredHeight > h) h = _lblGpu.PreferredHeight + 6;
                if (_lblSys.PreferredHeight > h) h = _lblSys.PreferredHeight + 6;
                if (x < 80) x = 80;
                if (x > 1100) x = 1100;
                Size = new Size(x, h);
            }

            if (AfterResize != null) AfterResize();
        }

        public void ApplyColors(Color bg, Color cpuFg, Color gpuFg, Color sysFg, int opacityPct)
        {
            BackColor = bg;
            _lblCpu.ForeColor = cpuFg;
            _lblGpu.ForeColor = gpuFg;
            _lblSys.ForeColor = sysFg;
            double op = Math.Max(10, Math.Min(100, opacityPct)) / 100.0;
            Opacity = op;
        }

        public void PositionBottomRight()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - 8, wa.Bottom - Height - 4);
        }
    }
}