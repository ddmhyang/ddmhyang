using System;
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
        public List<Guid> OwnedItemIds { get; set; }
        public Dictionary<ItemType, Guid> EquippedItems { get; set; }
        public Dictionary<ItemType, string> CustomColors { get; set; }
        public Dictionary<string, string> TaskColors { get; set; } = new Dictionary<string, string>();

        // --- 추가된 속성 ---
        public string Username { get; set; }
        public int Level { get; set; }
        public Dictionary<string, double> SoundVolumes { get; set; }
        // ------------------

        public bool IsMiniTimerEnabled { get; set; }

        public AppSettings()
        {
            IsIdleDetectionEnabled = true;
            IdleTimeoutSeconds = 60;
            WorkProcesses = new ObservableCollection<string>();
            PassiveProcesses = new ObservableCollection<string>();
            DistractionProcesses = new ObservableCollection<string>();
            FocusModeNagMessage = "할 일을 합시다!";
            FocusModeNagIntervalSeconds = 30;
            TagRules = new Dictionary<string, string>();
            Coins = 1000; // 초기 코인
            OwnedItemIds = new List<Guid>();
            EquippedItems = new Dictionary<ItemType, Guid>();
            CustomColors = new Dictionary<ItemType, string>();
            IsMiniTimerEnabled = false;

            // --- 추가된 속성 초기화 ---
            Username = "신규 사용자";
            Level = 1;
            SoundVolumes = new Dictionary<string, double>
            {
                { "Wave", 0.0 },
                { "Forest", 0.0 },
                { "Rain", 0.0 },
                { "Campfire", 0.0 }
            };
        }
    }
}

