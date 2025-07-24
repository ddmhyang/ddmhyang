// ActiveWindowHelper.cs (수정안)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace WorkPartner
{
    public static class ActiveWindowHelper
    {
        #region Windows API Imports
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

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
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
                var process = Process.GetProcessById((int)processId);

                // Edge, Chrome, Whale 프로세스만 대상으로 함
                if (process.ProcessName.ToLower() != "chrome" &&
                    process.ProcessName.ToLower() != "msedge" &&
                    process.ProcessName.ToLower() != "whale") return null;

                var element = AutomationElement.FromHandle(handle);
                if (element == null) return null;

                var addressBar = element.FindFirst(TreeScope.Subtree,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                        new PropertyCondition(AutomationElement.NameProperty, "주소창 및 검색창")
                    ));

                if (addressBar == null)
                {
                    addressBar = element.FindFirst(TreeScope.Descendants,
                       new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                }

                if (addressBar != null && addressBar.TryGetCurrentPattern(ValuePattern.Pattern, out object pattern))
                {
                    return ((ValuePattern)pattern).Current.Value as string;
                }
            }
            catch { }
            return null;
        }

        public static List<(string Title, string UrlKeyword)> GetRunningBrowserInfos()
        {
            var tabs = new List<(string Title, string UrlKeyword)>();
            var browserProcesses = new[] { "chrome", "msedge", "whale" };

            foreach (var processName in browserProcesses)
            {
                var processes = Process.GetProcessesByName(processName);
                foreach (var process in processes)
                {
                    List<IntPtr> windowHandles = GetWindowHandlesForProcess(process.Id);
                    foreach (var handle in windowHandles)
                    {
                        try
                        {
                            var element = AutomationElement.FromHandle(handle);
                            if (element == null) continue;

                            // 창의 제목 가져오기
                            string windowTitle = element.Current.Name;

                            // 주소창에서 URL 가져오기
                            string url = GetUrlFromAutomationElement(element);

                            if (!string.IsNullOrWhiteSpace(url))
                            {
                                try
                                {
                                    string urlKeyword = new Uri(url).Host.ToLower();
                                    tabs.Add((windowTitle, urlKeyword));
                                }
                                catch (UriFormatException)
                                {
                                    // 유효하지 않은 URL 형식
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            // 중복 제거 후 반환
            return tabs.GroupBy(t => t.UrlKeyword).Select(g => g.First()).ToList();
        }

        private static string GetUrlFromAutomationElement(AutomationElement element)
        {
            try
            {
                var addressBar = element.FindFirst(TreeScope.Subtree,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                        new PropertyCondition(AutomationElement.NameProperty, "주소창 및 검색창")
                    ));

                if (addressBar == null)
                {
                    addressBar = element.FindFirst(TreeScope.Descendants,
                       new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                }

                if (addressBar != null && addressBar.TryGetCurrentPattern(ValuePattern.Pattern, out object pattern))
                {
                    return ((ValuePattern)pattern).Current.Value as string;
                }
            }
            catch { }
            return null;
        }

        private static List<IntPtr> GetWindowHandlesForProcess(int processId)
        {
            var windowHandles = new List<IntPtr>();
            GCHandle gcHandles = GCHandle.Alloc(windowHandles);
            try
            {
                EnumWindows(new EnumWindowsProc((hWnd, lParam) =>
                {
                    GetWindowThreadProcessId(hWnd, out uint windowProcessId);
                    if (windowProcessId == processId && IsWindowVisible(hWnd) && GetWindowTextLength(hWnd) > 0)
                    {
                        var list = GCHandle.FromIntPtr(lParam).Target as List<IntPtr>;
                        list.Add(hWnd);
                    }
                    return true;
                }), GCHandle.ToIntPtr(gcHandles));
            }
            finally
            {
                if (gcHandles.IsAllocated)
                {
                    gcHandles.Free();
                }
            }
            return windowHandles;
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