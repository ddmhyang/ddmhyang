using System;

namespace WorkPartner
{
    public class TimeLogEntry
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string TaskText { get; set; }

        // [속성 추가] 사용자가 평가한 집중도 점수 (1~5점). 평가되지 않은 경우 0.
        public int FocusScore { get; set; }

        public TimeSpan Duration => EndTime - StartTime;

        public override string ToString()
        {
            return $"{StartTime:HH:mm} - {EndTime:HH:mm} ({TaskText})";
        }
    }
}
