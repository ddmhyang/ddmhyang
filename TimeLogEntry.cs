using System;
using System.Collections.Generic;

namespace WorkPartner
{
    public class TimeLogEntry
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string TaskText { get; set; }
        public int FocusScore { get; set; } // 1~5점, 평가 안됐으면 0

        /// <summary>
        /// [속성 추가] 휴식 시간에 한 활동 목록입니다. (예: "식사", "스트레칭")
        /// </summary>
        public List<string> BreakActivities { get; set; }

        public TimeSpan Duration => EndTime - StartTime;

        public TimeLogEntry()
        {
            // BreakActivities 리스트를 초기화해줍니다.
            BreakActivities = new List<string>();
        }

        public override string ToString()
        {
            return $"{StartTime:HH:mm} - {EndTime:HH:mm} ({TaskText})";
        }
    }
}