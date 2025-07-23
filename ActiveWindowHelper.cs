// ActiveWindowHelper.cs (기존 내용을 모두 지우고 아래 코드로 교체)
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

        // 윈도우 열거를 위한 P/Invoke
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

                if (process.ProcessName.ToLower() != "chrome" && process.ProcessName.ToLower() != "msedge" && process.ProcessName.ToLower() != "whale") return null;

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

        public static List<(string Title, string UrlKeyword)> GetBrowserTabInfos(string browserProcessName)
        {
            var tabs = new List<(string Title, string UrlKeyword)>();
            var addedTitles = new HashSet<string>();
            var processes = Process.GetProcessesByName(browserProcessName);

            foreach (var process in processes)
            {
                List<IntPtr> windowHandles = GetWindowHandlesForProcess(process.Id);

                foreach (var handle in windowHandles)
                {
                    try
                    {
                        var rootElement = AutomationElement.FromHandle(handle);
                        if (rootElement == null) continue;

                        var tabContainerCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Tab);
                        var tabContainer = rootElement.FindFirst(TreeScope.Descendants, tabContainerCondition);

                        if (tabContainer != null)
                        {
                            // [수정] 탭 컨테이너의 '직계 자식'이 아닌 '모든 후손' 중에서 탭 아이템을 찾도록 변경
                            var tabItems = tabContainer.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));

                            foreach (AutomationElement tabItem in tabItems)
                            {
                                string tabTitle = tabItem.Current.Name;
                                if (browserProcessName == "chrome") tabTitle = tabTitle.Replace(" - Google Chrome", "");
                                else if (browserProcessName == "msedge") tabTitle = tabTitle.Replace(" - Microsoft Edge", "");
                                else if (browserProcessName == "whale") tabTitle = tabTitle.Replace(" - Naver Whale", "");

                                if (!string.IsNullOrWhiteSpace(tabTitle) && !tabTitle.Equals("새 탭") && !tabTitle.StartsWith("tab-") && addedTitles.Add(tabTitle))
                                {
                                    try
                                    {
                                        string urlKeyword = new Uri("http://" + tabTitle.Split(' ')[0]).Host.ToLower();
                                        tabs.Add((tabTitle, urlKeyword));
                                    }
                                    catch (UriFormatException)
                                    {
                                        tabs.Add((tabTitle, tabTitle.ToLower()));
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            return tabs;
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