// 파일: ModelInput.cs (수정)
// [수정] TimeLogEntry와 동일한 구조를 가지도록 하고, 학습에 사용하지 않을 속성에 [Ignore]를 추가합니다.
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;

namespace WorkPartner.AI
{
    public class ModelInput
    {
        // 학습에 사용할 특성(Feature)들
        public float DayOfWeek { get; set; }
        public float Hour { get; set; }
        public float Duration { get; set; }
        public string TaskName { get; set; }

        // 예측할 값(Label)
        [ColumnName("Label")]
        public float FocusScore { get; set; }

        // 학습에 사용하지 않을 속성들은 [Ignore] 처리합니다.
        [Ignore]
        public DateTime StartTime { get; set; }
        [Ignore]
        public DateTime EndTime { get; set; }
        [Ignore]
        public List<string> BreakActivities { get; set; }
    }
}