using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using WorkPartner.Helpers;

namespace WorkPartner.Services
{
    public class BrowserTabHelper
    {
        public static List<InstalledProgram> GetAllBrowserTabs()
        {
            var allTabs = new List<InstalledProgram>();
            
            // 이 부분은 외부 라이브러리(CefSharp 등)를 통해 구현해야 합니다.
            // 아래 코드는 원리를 설명하는 가이드라인입니다.
            
            // 크롬 탭 정보 가져오기 (예시)
            var chromeProcesses = Process.GetProcessesByName("chrome");
            if (chromeProcesses.Length > 0)
            {
                // 크롬 프로세스의 탭 정보를 가져오는 로직을 여기에 구현
                // (예시) CefSharp의 TabCollection API를 사용하여 탭 목록을 얻습니다.
                // foreach (var tab in chromeTabs)
                // {
                //     allTabs.Add(new InstalledProgram { DisplayName = tab.Title, ProcessName = tab.Url });
                // }
            }
            
            // 엣지 탭 정보 가져오기 (예시)
            var edgeProcesses = Process.GetProcessesByName("msedge");
            if (edgeProcesses.Length > 0)
            {
                // 엣지 탭 정보를 가져오는 로직을 여기에 구현
            }
            
            return allTabs;
        }
    }
}