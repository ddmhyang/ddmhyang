using Microsoft.ML.Data;

namespace WorkPartner.AI
{
    public class ModelInput
    {
        [LoadColumn(0)] public float DayOfWeek { get; set; } // 요일 (숫자로 변환)
        [LoadColumn(1)] public float Hour { get; set; }      // 시간
        [LoadColumn(2)] public float Duration { get; set; }  // 작업 시간
        [LoadColumn(3)] public string TaskName { get; set; } // 과목 이름
        [LoadColumn(4), ColumnName("Label")] public float FocusScore { get; set; } // 정답(집중도 점수)
    }
}
