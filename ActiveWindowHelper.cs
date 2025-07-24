// 파일: ActiveWindowHelper.cs (수정 후)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace WorkPartner
{
    // [신규 추가] 프로세스와 창 제목을 함께 담기 위한 구조체
    public struct ProcessInfo
    {
        public Process Process { get; set; }
        public string WindowTitle { get; set; }
    }

    public static class ActiveWindowHelper
    {
        #region Windows API Imports
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
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
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        #endregion

        public static string GetActiveProcessName()
        {
            try
            {
                IntPtr handle = GetForegroundWindow();
                GetWindowThreadProcessId(handle, out uint processId);
                Process process = Process.GetProcessById((int)processId);
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

        // [핵심 수정] MainWindowTitle이 없는 앱(카카오톡 등)도 찾기 위한 메서드
        public static List<ProcessInfo> GetVisibleWindowProcesses()
        {
            var processInfos = new List<ProcessInfo>();
            var processIds = new HashSet<uint>();

            EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                if (IsWindowVisible(hWnd) && GetWindowTextLength(hWnd) > 0)
                {
                    GetWindowThreadProcessId(hWnd, out uint processId);
                    if (processId != 0 && !processIds.Contains(processId))
                    {
                        try
                        {
                            Process p = Process.GetProcessById((int)processId);
                            int length = GetWindowTextLength(hWnd) + 1;
                            StringBuilder sb = new StringBuilder(length);
                            GetWindowText(hWnd, sb, length);

                            processInfos.Add(new ProcessInfo { Process = p, WindowTitle = sb.ToString() });
                            processIds.Add(processId);
                        }
                        catch { /* 프로세스가 이미 종료된 경우 등 예외 무시 */ }
                    }
                }
                return true;
            }, IntPtr.Zero);
            return processInfos;
        }

        public static string GetActiveBrowserTabUrl()
        {
            try
            {
                IntPtr handle = GetForegroundWindow();
                if (handle == IntPtr.Zero) return null;
                GetWindowThreadProcessId(handle, out uint processId);
                var process = Process.GetProcessById((int)processId);

                string processName = process.ProcessName.ToLower();
                if (processName != "chrome" && processName != "msedge" && processName != "whale") return null;

                var element = AutomationElement.FromHandle(handle);
                if (element == null) return null;

                var addressBar = element.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));

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
