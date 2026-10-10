using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace TurboToggle
{
    class SensorDiagForm : Form
    {
        public SensorDiagForm()
        {
            Text = "Sensor Diagnostics — LibreHardwareMonitor";
            Width = 780;
            Height = 600;
            StartPosition = FormStartPosition.CenterScreen;

            var txt = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9f),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            Controls.Add(txt);

            var sb = new StringBuilder();
            sb.AppendLine("=== LHM Init Status ===");
            if (SensorReader.LastLhmError != null)
            {
                sb.AppendLine("FAILED: " + SensorReader.LastLhmError);
                sb.AppendLine();
                sb.AppendLine("Kemungkinan penyebab:");
                sb.AppendLine("- DLL salah versi (net6.0/net8.0, bukan net472)");
                sb.AppendLine("- DLL corrupt saat download");
                sb.AppendLine("- Aplikasi tidak jalan sebagai Administrator");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("OK (atau belum pernah dicoba)");
                sb.AppendLine();
            }

            try
            {
                var comp = new LibreHardwareMonitor.Hardware.Computer
                {
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMotherboardEnabled = true,
                    IsControllerEnabled = true,
                    IsMemoryEnabled = true,
                    IsStorageEnabled = false,
                    IsNetworkEnabled = false
                };
                comp.Open();

                sb.AppendLine("=== Hardware yang terdeteksi ===");
                sb.AppendLine();

                foreach (var hw in comp.Hardware)
                    DumpHardware(sb, hw, 0);

                comp.Close();
            }
            catch (Exception ex)
            {
                sb.AppendLine("ERROR membuka LHM:");
                sb.AppendLine(ex.GetType().Name + ": " + ex.Message);
                sb.AppendLine();
                sb.AppendLine(ex.StackTrace);
            }

            txt.Text = sb.ToString();
        }

        static void DumpHardware(StringBuilder sb, LibreHardwareMonitor.Hardware.IHardware hw, int indent)
        {
            string pad = new string(' ', indent);
            sb.AppendLine(pad + "[" + hw.HardwareType + "] " + hw.Name);
            try { hw.Update(); } catch { }
            foreach (var sensor in hw.Sensors)
            {
                string val = sensor.Value.HasValue ? sensor.Value.Value.ToString("0.00") : "n/a";
                sb.AppendLine(pad + "   " + sensor.SensorType + " | " + sensor.Name + " = " + val);
            }
            foreach (var sub in hw.SubHardware)
                DumpHardware(sb, sub, indent + 3);
        }
    }
}