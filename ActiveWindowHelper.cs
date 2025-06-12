using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WorkPartner
{
    public static class ActiveWindowHelper
    {
        // Windows API 함수를 C#에서 사용하기 위한 선언
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        /// <summary>
        /// 현재 활성화된 창의 프로세스 이름을 가져옵니다. (예: "chrome", "devenv")
        /// </summary>
        public static string GetActiveProcessName()
        {
            try
            {
                IntPtr handle = GetForegroundWindow(); // 활성화된 창의 핸들(ID) 가져오기

                GetWindowThreadProcessId(handle, out uint processId); // 핸들로 프로세스 ID 가져오기
                Process process = Process.GetProcessById((int)processId); // 프로세스 ID로 프로세스 객체 가져오기

                // "Idle" 프로세스는 실제 사용 프로그램이 아니므로 제외
                if (process.ProcessName.ToLower() == "idle")
                {
                    return "unknown";
                }

                return process.ProcessName.ToLower(); // 프로세스 이름을 소문자로 반환
            }
            catch
            {
                return "unknown"; // 오류 발생 시 "unknown" 반환
            }

        }
        // ActiveWindowHelper.cs 파일 내부

        // ... GetActiveProcessName() 함수는 그대로 둡니다 ...

        // [코드 추가] -----------------------------------------------------------------

        // 마지막 입력 정보를 위한 구조체
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public static readonly int SizeOf = Marshal.SizeOf(typeof(LASTINPUTINFO));
            [MarshalAs(UnmanagedType.U4)]
            public UInt32 cbSize;
            [MarshalAs(UnmanagedType.U4)]
            public UInt32 dwTime;
        }

        // Windows API 함수 선언
        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();

        /// <summary>
        /// 사용자가 아무 입력(키보드, 마우스)이 없었던 유휴 시간을 TimeSpan 형태로 가져옵니다.
        /// </summary>
        public static TimeSpan GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

            if (GetLastInputInfo(ref lastInputInfo))
            {
                // 시스템 부팅 후 경과 시간과 마지막 입력 시간의 차이를 계산
                uint lastInputTick = lastInputInfo.dwTime;
                uint idleTime = GetTickCount() - lastInputTick;
                return TimeSpan.FromMilliseconds(idleTime);
            }

            return TimeSpan.Zero; // 오류 발생 시 0 반환
        }
        // -------------------------------------------------------------------------
    }
}
