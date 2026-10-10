using System;

namespace TurboToggle
{
    class WindowInfo
    {
        public IntPtr Handle;
        public string Title;
        public string ProcessName;

        public override string ToString()
        {
            return ProcessName + ".exe   —   " + Title;
        }
    }
}