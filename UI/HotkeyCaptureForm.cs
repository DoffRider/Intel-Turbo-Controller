using System;
using System.Drawing;
using System.Windows.Forms;

namespace TurboToggle
{
    class HotkeyCaptureForm : Form
    {
        public uint Modifiers;
        public uint Vk;
        public bool Captured;
        Label _lbl;

        public HotkeyCaptureForm()
        {
            Text = "Set Hotkey";
            Width = 380; Height = 170;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            KeyPreview = true;

            _lbl = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Tekan kombinasi hotkey...\n(contoh: Ctrl+Alt+T)",
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            Controls.Add(_lbl);

            KeyDown += (s, e) =>
            {
                e.SuppressKeyPress = true;
                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey ||
                    e.KeyCode == Keys.Menu || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
                    return;
                uint mods = 0;
                if (e.Control) mods |= HotkeyManager.MOD_CONTROL;
                if (e.Alt) mods |= HotkeyManager.MOD_ALT;
                if (e.Shift) mods |= HotkeyManager.MOD_SHIFT;
                if (mods == 0) { _lbl.Text = "Minimal 1 modifier (Ctrl/Alt/Shift)"; return; }
                Modifiers = mods;
                Vk = (uint)e.KeyCode;
                Captured = true;
                DialogResult = DialogResult.OK;
                Close();
            };
        }
    }
}