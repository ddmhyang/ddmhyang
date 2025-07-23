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

                if (process.ProcessName.ToLower() != "chrome" && process.ProcessName.ToLower() != "msedge") return null;

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

        // ActiveWindowHelper.cs

        public static List<(string Title, string UrlKeyword)> GetBrowserTabInfos(string browserProcessName)
        {
            var tabs = new List<(string Title, string UrlKeyword)>();
            var addedTitles = new HashSet<string>(); // 중복 탭 제목 추가 방지

            // 이름이 같은 모든 브라우저 프로세스를 찾습니다 (예: 모든 'msedge' 프로세스).
            var processes = Process.GetProcessesByName(browserProcessName);

            foreach (var process in processes)
            {
                if (process.MainWindowHandle == IntPtr.Zero) continue;

                try
                {
                    var rootElement = AutomationElement.FromHandle(process.MainWindowHandle);
                    if (rootElement == null) continue;

                    // 각 창의 탭 항목을 찾습니다.
                    var tabItems = rootElement.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));

                    foreach (AutomationElement tabItem in tabItems)
                    {
                        string tabTitle = tabItem.Current.Name;

                        // " - Google Chrome" 같은 브라우저 이름 부분을 제거합니다.
                        if (browserProcessName == "chrome") tabTitle = tabTitle.Replace(" - Google Chrome", "");
                        else if (browserProcessName == "msedge") tabTitle = tabTitle.Replace(" - Microsoft Edge", "");
                        else if (browserProcessName == "whale") tabTitle = tabTitle.Replace(" - Naver Whale", "");

                        // "새 탭", "tab-", 비어있는 제목 등 의미 없는 항목과 중복된 제목을 걸러냅니다.
                        if (!string.IsNullOrWhiteSpace(tabTitle) &&
                            !tabTitle.Equals("새 탭") &&
                            !tabTitle.StartsWith("tab-") &&
                            addedTitles.Add(tabTitle))
                        {
                            // 간단한 URL 추출 (도메인만 키워드로 사용)
                            string urlKeyword = new Uri("http://" + tabTitle.Split(' ')[0]).Host.ToLower();
                            tabs.Add((tabTitle, urlKeyword));
                        }
                    }
                }
                catch (Exception) { /* 오류 무시 */ }
            }

            return tabs;
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