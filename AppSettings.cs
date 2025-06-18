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

        // [속성 추가] 사용자가 구매한 아이템의 ID를 저장할 목록입니다.
        public List<Guid> OwnedItemIds { get; set; }

        //어떤 아이템을 착용했는지
        public Dictionary<ItemType, Guid> EquippedItems { get; set; }

        // [추가] 사용자가 직접 만든 커스텀 색상을 부위별로 저장하는 Dictionary 입니다.
        // 예를 들어, Key는 ItemType.HairColor, Value는 색상 Hex 코드("#RRGGBB")가 됩니다.
        public Dictionary<ItemType, string> CustomColors { get; set; }



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

            // [추가] 착용 아이템 ID 초기화
            EquippedItems = new Dictionary<ItemType, Guid>();

            // [추가] 커스텀 색상 Dictionary 초기화
            CustomColors = new Dictionary<ItemType, string>();


        }
    }
}
