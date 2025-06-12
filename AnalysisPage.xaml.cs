using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;

namespace WorkPartner
{
    public partial class AnalysisPage : UserControl
    {
        private readonly string _timeLogFilePath = "timelogs.json";
        private List<TimeLogEntry> _allTimeLogs;

        public SeriesCollection HourAnalysisSeries { get; set; }
        public string[] HourLabels { get; set; }
        public Func<double, string> YFormatter { get; set; }

        public AnalysisPage()
        {
            InitializeComponent();
            _allTimeLogs = new List<TimeLogEntry>();
            HourAnalysisSeries = new SeriesCollection();
            DataContext = this;
        }

        public void LoadAndAnalyzeData()
        {
            LoadTimeLogs();
            if (!_allTimeLogs.Any()) return;

            AnalyzeOverallStats();
            UpdateTaskAnalysis(DateTime.MinValue, DateTime.MaxValue);
            GenerateFocusBasedAiSuggestion(); // [호출 변경] 새로운 AI 제안 함수를 호출합니다.
        }

        private void LoadTimeLogs()
        {
            _allTimeLogs.Clear();
            if (File.Exists(_timeLogFilePath))
            {
                var json = File.ReadAllText(_timeLogFilePath);
                var loadedLogs = JsonSerializer.Deserialize<List<TimeLogEntry>>(json);
                if (loadedLogs != null) _allTimeLogs = loadedLogs;
            }
        }

        private void AnalyzeOverallStats()
        {
            // 전체 요약, 시간대별 차트 등 기존 통계 로직은 그대로 유지합니다.
            var totalWorkTime = new TimeSpan(_allTimeLogs.Sum(log => log.Duration.Ticks));
            var totalDays = _allTimeLogs.Select(log => log.StartTime.Date).Distinct().Count();
            TotalWorkTimeTextBlock.Text = $"{(int)totalWorkTime.TotalHours} 시간 {totalWorkTime.Minutes} 분";
            TotalDaysTextBlock.Text = $"{totalDays} 일";
            var maxConcentrationTime = _allTimeLogs.Any() ? _allTimeLogs.Max(log => log.Duration) : TimeSpan.Zero;
            MaxConcentrationTimeTextBlock.Text = $"{(int)maxConcentrationTime.TotalMinutes} 분";
            var hourlyWork = _allTimeLogs.GroupBy(log => log.StartTime.Hour).Select(g => new { Hour = g.Key, TotalMinutes = g.Sum(log => log.Duration.TotalMinutes) }).ToList();
            var peakHourData = hourlyWork.OrderByDescending(h => h.TotalMinutes).FirstOrDefault();
            PeakConcentrationHourTextBlock.Text = peakHourData != null ? $"{peakHourData.Hour} 시 ~ {peakHourData.Hour + 1} 시" : "-";
            var chartValues = new ChartValues<double>();
            var labels = new string[24];
            for (int i = 0; i < 24; i++) { var hourData = hourlyWork.FirstOrDefault(h => h.Hour == i); chartValues.Add(hourData?.TotalMinutes ?? 0); labels[i] = i.ToString(); }
            HourAnalysisSeries.Clear();
            HourAnalysisSeries.Add(new ColumnSeries { Title = "작업량", Values = chartValues });
            HourLabels = labels;
            YFormatter = value => value.ToString("N0");
        }

        private void UpdateTaskAnalysis(DateTime startDate, DateTime endDate)
        {
            var filteredLogs = _allTimeLogs.Where(log => log.StartTime.Date >= startDate.Date && log.StartTime.Date <= endDate.Date).ToList();
            var taskAnalysis = filteredLogs.GroupBy(log => log.TaskText).Select(group => new TaskAnalysisResult { TaskName = group.Key, TotalTime = new TimeSpan(group.Sum(log => log.Duration.Ticks)) }).OrderByDescending(result => result.TotalTime).ToList();
            TaskAnalysisListView.ItemsSource = taskAnalysis;
        }

        /// <summary>
        /// [핵심 AI 로직] 집중도 점수를 기반으로 개인 맞춤형 제안을 생성합니다.
        /// </summary>
        private void GenerateFocusBasedAiSuggestion()
        {
            // 평가된 데이터(FocusScore > 0)만 필터링합니다.
            var ratedLogs = _allTimeLogs.Where(log => log.FocusScore > 0).ToList();

            // AI 분석을 위한 최소 데이터 양을 확인합니다. (예: 최소 3번의 평가)
            if (ratedLogs.Count < 3)
            {
                SuggestionTextBlock.Text = "집중도 평가 데이터가 더 쌓이면, AI가 당신의 숨겨진 작업 패턴을 분석해 드릴게요!";
                return;
            }

            // 1. 평균 집중도가 가장 높았던 '요일'과 '시간대' (황금 시간대) 찾기
            var bestSlot = ratedLogs
                .GroupBy(log => new { log.StartTime.DayOfWeek, log.StartTime.Hour })
                .Select(g => new
                {
                    Day = g.Key.DayOfWeek,
                    Hour = g.Key.Hour,
                    AverageFocusScore = g.Average(log => log.FocusScore) // 평균 집중도 계산
                })
                .OrderByDescending(s => s.AverageFocusScore) // 평균 집중도가 높은 순으로 정렬
                .FirstOrDefault();

            if (bestSlot == null) return;

            // 2. 해당 '황금 시간대'에 주로 어떤 '과목'을 공부했는지 분석
            var peakTask = ratedLogs
                .Where(log => log.StartTime.DayOfWeek == bestSlot.Day && log.StartTime.Hour == bestSlot.Hour)
                .GroupBy(log => log.TaskText)
                .Select(g => new { TaskName = g.Key, TotalDuration = g.Sum(log => log.Duration.TotalSeconds) })
                .OrderByDescending(t => t.TotalDuration)
                .FirstOrDefault();

            // 3. 최종 제안 메시지 생성
            string peakDayStr = ToKoreanDayOfWeek(bestSlot.Day);
            string suggestion;

            if (peakTask != null)
            {
                suggestion = $"분석 결과, 주로 '{peakDayStr} {bestSlot.Hour}시'에 '{peakTask.TaskName}' 과목을 진행할 때 평균 집중도 점수({bestSlot.AverageFocusScore:F1})가 가장 높았습니다! 앞으로 중요한 '{peakTask.TaskName}' 작업은 이 황금 시간대를 잘 활용해보는 건 어떨까요?";
            }
            else
            {
                suggestion = $"분석 결과, 주로 '{peakDayStr} {bestSlot.Hour}시'에 가장 높은 집중력(평균 {bestSlot.AverageFocusScore:F1}점)을 보여주셨습니다. 중요한 작업을 시작하기 좋은 시간이네요!";
            }

            SuggestionTextBlock.Text = suggestion;
        }

        private string ToKoreanDayOfWeek(DayOfWeek day)
        {
            switch (day) { case DayOfWeek.Monday: return "월요일"; case DayOfWeek.Tuesday: return "화요일"; case DayOfWeek.Wednesday: return "수요일"; case DayOfWeek.Thursday: return "목요일"; case DayOfWeek.Friday: return "금요일"; case DayOfWeek.Saturday: return "토요일"; case DayOfWeek.Sunday: return "일요일"; default: return string.Empty; }
        }

        // --- 버튼 클릭 이벤트 핸들러들은 이전과 동일 ---
        #region Button Click Handlers
        private void TodayButton_Click(object sender, System.Windows.RoutedEventArgs e) { UpdateTaskAnalysis(DateTime.Today, DateTime.Today); }
        private void ThisWeekButton_Click(object sender, System.Windows.RoutedEventArgs e) { var today = DateTime.Today; var dayOfWeek = (int)today.DayOfWeek; var startDate = today.AddDays(-dayOfWeek); var endDate = startDate.AddDays(6); UpdateTaskAnalysis(startDate, endDate); }
        private void ThisMonthButton_Click(object sender, System.Windows.RoutedEventArgs e) { var today = DateTime.Today; var startDate = new DateTime(today.Year, today.Month, 1); var endDate = startDate.AddMonths(1).AddDays(-1); UpdateTaskAnalysis(startDate, endDate); }
        private void TotalButton_Click(object sender, System.Windows.RoutedEventArgs e) { UpdateTaskAnalysis(DateTime.MinValue, DateTime.MaxValue); }
        private void CustomDateButton_Click(object sender, System.Windows.RoutedEventArgs e) { if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue) { UpdateTaskAnalysis(StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value); } else { MessageBox.Show("시작 날짜와 종료 날짜를 모두 선택해주세요."); } }
        #endregion
    }

    public class TaskAnalysisResult
    {
        public string TaskName { get; set; }
        public TimeSpan TotalTime { get; set; }
        public string TotalTimeFormatted => $"{(int)TotalTime.TotalHours} 시간 {TotalTime.Minutes} 분";
    }
}
