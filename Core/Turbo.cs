using System;
using System.Diagnostics;
using System.Text;

namespace TurboToggle
{
    static class Turbo
    {
        const string GUID_PROC  = "54533251-82be-4824-96c1-47b60b740d00";
        const string GUID_BOOST = "be337238-0d82-4146-a960-4f3749d470c7";
        static bool _state = true;
        static bool _loaded = false;
        static readonly object _lock = new object();

        public static void Set(bool enabled)
        {
            lock (_lock)
            {
                string v = enabled ? "1" : "0";
                Run("/SETACVALUEINDEX SCHEME_CURRENT " + GUID_PROC + " " + GUID_BOOST + " " + v);
                Run("/SETDCVALUEINDEX SCHEME_CURRENT " + GUID_PROC + " " + GUID_BOOST + " " + v);
                Run("/SETACTIVE SCHEME_CURRENT");
                _state = enabled; _loaded = true;
            }
        }

        public static bool Get()
        {
            lock (_lock)
            {
                if (!_loaded) { _state = ReadPowerCfg(); _loaded = true; }
                return _state;
            }
        }

        public static bool ReloadFromSystem()
        {
            lock (_lock) { _state = ReadPowerCfg(); _loaded = true; return _state; }
        }

        static bool ReadPowerCfg()
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg",
                    "/query SCHEME_CURRENT " + GUID_PROC + " " + GUID_BOOST)
                { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using (var p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    foreach (var rawLine in output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (rawLine.IndexOf("Current AC", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            int idx = rawLine.LastIndexOf("0x", StringComparison.OrdinalIgnoreCase);
                            if (idx >= 0)
                            {
                                string tail = rawLine.Substring(idx + 2);
                                var sb = new StringBuilder();
                                foreach (char c in tail)
                                {
                                    if (Uri.IsHexDigit(c)) sb.Append(c);
                                    else if (sb.Length > 0) break;
                                }
                                uint v;
                                if (uint.TryParse(sb.ToString(), System.Globalization.NumberStyles.HexNumber, null, out v))
                                    return v != 0;
                            }
                        }
                    }
                }
            }
            catch { }
            return _state;
        }

        static void Run(string args)
        {
            try
            {
                using (var p = Process.Start(new ProcessStartInfo("powercfg", args)
                { UseShellExecute = false, CreateNoWindow = true }))
                { p.WaitForExit(5000); }
            }
            catch { }
        }
    }
}