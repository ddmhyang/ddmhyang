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

        /// <summary>
        /// [속성 추가] 미니 타이머(항상 위) 기능 활성화 여부
        /// </summary>
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
            Coins = 0;
            OwnedItemIds = new List<Guid>();
            EquippedItems = new Dictionary<ItemType, Guid>();
            CustomColors = new Dictionary<ItemType, string>();
            IsMiniTimerEnabled = false; // 기본값은 비활성화
        }
    }
}