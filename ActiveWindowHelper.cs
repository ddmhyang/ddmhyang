// ActiveWindowHelper.cs (수정)
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

                // Try to find the address bar. Different browser versions/languages can have different names.
                var conditions = new OrCondition(
                    new PropertyCondition(AutomationElement.NameProperty, "주소 및 검색창"), // Korean
                    new PropertyCondition(AutomationElement.NameProperty, "Address and search bar"), // English
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "address and search bar") // Fallback for some Edge versions
                );

                var addressBar = element.FindFirst(TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                        conditions
                    ));

                // Fallback for when the name is not found
                if (addressBar == null)
                {
                    var conditionControl = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ToolBar);
                    var toolBar = element.FindFirst(TreeScope.Children, conditionControl);
                    if (toolBar != null)
                    {
                        var conditionEdit = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
                        addressBar = toolBar.FindFirst(TreeScope.Children, conditionEdit);
                    }
                }

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
                                string url = GetBrowserTabUrlForTabItem(tabItem);

                                if (!string.IsNullOrWhiteSpace(url))
                                {
                                    try
                                    {
                                        string tabTitle = tabItem.Current.Name;
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

        public static string GetBrowserTabUrlForTabItem(AutomationElement tabItem)
        {
            try
            {
                AutomationElement rootElement = tabItem;
                while (rootElement.Current.ControlType != ControlType.Window)
                {
                    rootElement = TreeWalker.ControlViewWalker.GetParent(rootElement);
                    if (rootElement == null) return null;
                }

                var addressBar = rootElement.FindFirst(TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                        new PropertyCondition(AutomationElement.NameProperty, "주소창 및 검색창")
                    ));

                if (addressBar == null)
                {
                    addressBar = rootElement.FindFirst(TreeScope.Descendants,
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
