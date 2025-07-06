// 파일: TimeLogEntry.cs (수정)
// [수정] ML.NET이 처리할 수 없는 복잡한 타입의 속성에 [Ignore] 어트리뷰트를 추가합니다.
using System;
using System.Collections.Generic;
using Microsoft.ML.Data; // [Ignore] 어트리뷰트를 사용하기 위해 추가

namespace WorkPartner
{
    public class TimeLogEntry
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string TaskText { get; set; }
        public int FocusScore { get; set; }

        // ML.NET이 List<string> 타입을 직접 처리할 수 없으므로,
        // 데이터를 불러올 때 이 속성을 무시하도록 설정합니다.
        [Ignore]
        public List<string> BreakActivities { get; set; }

        [Ignore] // Duration은 계산된 속성이므로 학습에서 제외합니다.
        public TimeSpan Duration => EndTime - StartTime;

        public TimeLogEntry()
        {
            BreakActivities = new List<string>();
        }

        public override string ToString()
        {
            return $"{StartTime:HH:mm} - {EndTime:HH:mm} ({TaskText})";
        }
    }
}