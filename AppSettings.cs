using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkPartner
{
    public class AppSettings
    {
        public bool IsIdleDetectionEnabled { get; set; }
        public int IdleTimeoutSeconds { get; set; }
        public ObservableCollection<string> WorkProcesses { get; set; }
        public ObservableCollection<string> PassiveProcesses { get; set; }
        public ObservableCollection<string> DistractionProcesses { get; set; }
        public string FocusModeNagMessage { get; set; }
        public int FocusModeNagIntervalSeconds { get; set; }
        public Dictionary<string, string> TagRules { get; set; }
        public int Coins { get; set; }

        public AppSettings()
        {
            IsIdleDetectionEnabled = true;
            IdleTimeoutSeconds = 60;
            WorkProcesses = new ObservableCollection<string> { "devenv", "chrome", "code" };
            PassiveProcesses = new ObservableCollection<string> { "vlc", "potplayer" };
            DistractionProcesses = new ObservableCollection<string> { "kakaotalk" };
            FocusModeNagMessage = "할 일을 합시다!";
            FocusModeNagIntervalSeconds = 30;

            // [기본 규칙 추가] 사용자가 처음 시작할 때를 위한 기본 규칙들을 추가합니다.
            TagRules = new Dictionary<string, string>
            {
                { "공부", "#공부" },
                { "강의", "#인강" },
                { "문제", "#문제풀이" },
                { "작업", "#작업" },
                { "마감", "#작업" },
                { "게임", "#취미" }
            };

            Coins = 0; // 코인 초기값 설정

        }
    }
}
