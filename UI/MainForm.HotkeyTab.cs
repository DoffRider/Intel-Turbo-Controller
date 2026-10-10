using System;
using System.Drawing;
using System.Windows.Forms;

namespace TurboToggle
{
    partial class MainForm
    {
        TextBox _txtHotkey;

        void BuildHotkeyTab()
        {
            var tpKey = new TabPage("Hotkey");
            _tabs.TabPages.Add(tpKey);

            tpKey.Controls.Add(new Label
            {
                Text = "Hotkey global untuk toggle Turbo (aktif di mana saja):",
                Left = 12, Top = 12, Width = 430, Height = 20
            });

            _txtHotkey = new TextBox
            {
                Left = 12, Top = 40, Width = 240, Height = 26,
                ReadOnly = true, Font = new Font("Consolas", 11),
                Text = HotkeyManager.Format(_cfg.HotkeyMods, _cfg.HotkeyVk)
            };
            tpKey.Controls.Add(_txtHotkey);

            var btnSet = new Button { Text = "Set...", Left = 260, Top = 38, Width = 80, Height = 28 };
            btnSet.Click += (s, e) =>
            {
                using (var dlg = new HotkeyCaptureForm())
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Captured)
                    {
                        _cfg.HotkeyMods = dlg.Modifiers;
                        _cfg.HotkeyVk = dlg.Vk;
                        _cfg.Save();
                        _txtHotkey.Text = HotkeyManager.Format(_cfg.HotkeyMods, _cfg.HotkeyVk);
                        if (OnHotkeyChanged != null) OnHotkeyChanged();
                    }
                }
            };
            tpKey.Controls.Add(btnSet);

            var btnClear = new Button { Text = "Clear", Left = 348, Top = 38, Width = 80, Height = 28 };
            btnClear.Click += (s, e) =>
            {
                _cfg.HotkeyMods = 0; _cfg.HotkeyVk = 0; _cfg.Save();
                _txtHotkey.Text = "(none)";
                if (OnHotkeyChanged != null) OnHotkeyChanged();
            };
            tpKey.Controls.Add(btnClear);

            tpKey.Controls.Add(new Label
            {
                Text = "Syarat: minimal 1 modifier (Ctrl/Alt/Shift). Contoh: Ctrl+Alt+T",
                Left = 12, Top = 80, Width = 430, Height = 20, ForeColor = Color.Gray
            });
        }
    }
}