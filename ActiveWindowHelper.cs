// ActiveWindowHelper.cs
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace WorkPartner
{
    public static class ActiveWindowHelper
    {
        [DllImport("user2.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO { public static readonly int SizeOf = Marshal.SizeOf(typeof(LASTINPUTINFO)); [MarshalAs(UnmanagedType.U4)] public UInt32 cbSize; [MarshalAs(UnmanagedType.U4)] public UInt32 dwTime; }
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();


        public static string GetActiveProcessName()
        {
            try
            {
                IntPtr handle = GetForegroundWindow();
                if (handle == IntPtr.Zero) return "unknown";
                GetWindowThreadProcessId(handle, out uint processId);
                if (processId == 0) return "unknown";
                Process process = Process.GetProcessById((int)processId);
                if (process.ProcessName.ToLower() == "idle") return "unknown";
                return process.ProcessName.ToLower();
            }
            catch { return "unknown"; }
        }

        public static string GetActiveWindowTitle()
        {
            try
            {
                IntPtr handle = GetForegroundWindow();
                int length = GetWindowTextLength(handle) + 1;
                StringBuilder sb = new StringBuilder(length);
                GetWindowText(handle, sb, length);
                return sb.ToString();
            }
            catch { return "unknown"; }
        }

        public static string GetActiveBrowserTabUrl()
        {
            try
            {
                IntPtr handle = GetForegroundWindow();
                if (handle == IntPtr.Zero) return null;
                GetWindowThreadProcessId(handle, out uint processId);
                if (processId == 0) return null;
                var process = Process.GetProcessById((int)processId);

                if (process.MainWindowHandle == IntPtr.Zero) return null;

                string processName = process.ProcessName.ToLower();
                if (processName != "chrome" && processName != "msedge" && processName != "whale" && processName != "firefox")
                {
                    return null;
                }

                var element = AutomationElement.FromHandle(handle);
                if (element == null) return null;

                var conditions = new OrCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "주소창 및 검색창"),
                    new PropertyCondition(AutomationElement.NameProperty, "Address and search bar"),
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "urlbar-input"),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)
                );

                var addressBar = element.FindFirst(TreeScope.Descendants, conditions);

                if (addressBar != null && addressBar.TryGetCurrentPattern(ValuePattern.Pattern, out object pattern))
                {
                    return ((ValuePattern)pattern).Current.Value as string;
                }
            }
            catch { }
            return null;
        }

        public static TimeSpan GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            if (GetLastInputInfo(ref lastInputInfo))
            {
                uint lastInputTick = lastInputInfo.dwTime;
                uint idleTime = GetTickCount() - lastInputTick;
                return TimeSpan.FromMilliseconds(idleTime);
            }
            return TimeSpan.Zero;
        }
    }
}
