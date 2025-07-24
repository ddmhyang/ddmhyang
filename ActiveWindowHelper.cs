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
                            var tabItems = tabContainer.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));

                            foreach (AutomationElement tabItem in tabItems)
                            {
                                // 탭의 제목을 직접 가져옴
                                string tabTitle = tabItem.Current.Name;
                                if (string.IsNullOrWhiteSpace(tabTitle)) continue;

                                // 탭의 URL을 가져오는 더 견고한 로직
                                string url = GetUrlFromTabElement(tabItem);

                                if (!string.IsNullOrWhiteSpace(url))
                                {
                                    try
                                    {
                                        string urlKeyword = new Uri(url).Host.ToLower();
                                        tabs.Add((tabTitle, urlKeyword));
                                    }
                                    catch (UriFormatException)
                                    {
                                        // URL 형식이 유효하지 않은 경우
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

        public static string GetUrlFromTabElement(AutomationElement tabItem)
        {
            try
            {
                // TabItem에서 Pattern을 찾아 URL을 가져오는 로직 (예: InvokePattern)
                if (tabItem.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object selectionPattern))
                {
                    SelectionItemPattern selectedItem = (SelectionItemPattern)selectionPattern;
                    selectedItem.Select(); // 탭을 선택하여 주소창 내용이 변경되도록 함

                    // 현재 활성화된 탭의 URL을 가져오는 로직 재사용
                    return GetActiveBrowserTabUrl();
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