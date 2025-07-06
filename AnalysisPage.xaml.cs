using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;
using WorkPartner.AI; // [추가] AI 모델 관련 네임스페이스

namespace WorkPartner
{
    public partial class AnalysisPage : UserControl
    {
        private readonly string _timeLogFilePath = "timelogs.json";
        private readonly string _tasksFilePath = "tasks.json";
        private List<TimeLogEntry> _allTimeLogs;

        // [추가] AI 예측 서비스 객체
        private PredictionService _predictionService;

        public SeriesCollection HourAnalysisSeries { get; set; }
        public string[] HourLabels { get; set; }
        public Func<double, string> YFormatter { get; set; }

        public AnalysisPage()
        {
            InitializeComponent();
            _allTimeLogs = new List<TimeLogEntry>();
            HourAnalysisSeries = new SeriesCollection();
            _predictionService = new PredictionService(); // 예측 서비스 초기화
            DataContext = this;

            InitializePredictionUI(); // 예측 UI 초기화
        }

        private void InitializePredictionUI()
        {
            // 요일 콤보박스 채우기
            DayOfWeekPredictionComboBox.ItemsSource = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().Select(d => ToKoreanDayOfWeek(d));
            DayOfWeekPredictionComboBox.SelectedIndex = (int)DateTime.Today.DayOfWeek;

            // 시간 콤보박스 채우기
            HourPredictionComboBox.ItemsSource = Enumerable.Range(0, 24).Select(h => $"{h} 시");
            HourPredictionComboBox.SelectedIndex = DateTime.Now.Hour;
        }

        // [오류 수정] public void -> public async void 로 변경
        public async void LoadAndAnalyzeData()
        {
            LoadTimeLogs();
            LoadTasksForPrediction();
            if (!_allTimeLogs.Any()) return;

            // [오류 수정] await Task.Run을 사용하여 UI 멈춤 없이 비동기적으로 모델 훈련
            await Task.Run(() => _predictionService.TrainModel());

            // UI와 관련된 작업은 다시 UI 스레드에서 처리하도록 합니다.
            AnalyzeOverallStats();
            UpdateTaskAnalysis(DateTime.MinValue, DateTime.MaxValue);
            GenerateFocusBasedAiSuggestion();
            GenerateWorkRestPatternSuggestion();
        }
        // [추가] 예측 UI의 과목 콤보박스를 채우기 위한 함수
        private void LoadTasksForPrediction()
        {
            if (!File.Exists(_tasksFilePath)) return;
            var json = File.ReadAllText(_tasksFilePath);
            var tasks = JsonSerializer.Deserialize<List<TaskItem>>(json);
            TaskPredictionComboBox.ItemsSource = tasks;
            if (tasks != null && tasks.Any()) TaskPredictionComboBox.SelectedIndex = 0;
        }

        // --- 예측 버튼 클릭 이벤트 ---
        private void PredictButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskPredictionComboBox.SelectedItem == null ||
               DayOfWeekPredictionComboBox.SelectedItem == null ||
               HourPredictionComboBox.SelectedItem == null)
            {
                MessageBox.Show("모든 예측 조건을 선택해주세요.");
                return;
            }

            var input = new ModelInput
            {
                TaskName = (TaskPredictionComboBox.SelectedItem as TaskItem).Text,
                DayOfWeek = (float)DayOfWeekPredictionComboBox.SelectedIndex,
                Hour = (float)HourPredictionComboBox.SelectedIndex,
                Duration = 60 // 예측을 위한 기본 작업 시간(예: 60분)
            };

            float predictedScore = _predictionService.Predict(input);
            string suggestion = GetSuggestionForScore(predictedScore); // [추가] 점수에 따른 해석 함수 호출

            PredictionResultTextBlock.Text = $"예상 집중도: {predictedScore:F2} / 5.0";
        }

        // [함수 추가] 점수를 해석해주는 도우미 함수
        private string GetSuggestionForScore(float score)
        {
            if (score >= 4.0) return "AI 분석: 최고의 집중력을 발휘할 수 있는 '황금 시간대'입니다!";
            if (score >= 3.0) return "AI 분석: 좋은 컨디션으로 꾸준히 작업을 이어갈 수 있겠네요.";
            if (score > 0) return "AI 분석: 집중력이 다소 떨어질 수 있으니, 중간에 짧은 휴식을 권장합니다.";
            return "분석에 필요한 데이터가 부족하거나, 이전에 학습한 적이 없는 조건입니다.";
        }

        // --- 이하 다른 코드들은 이전과 동일 ---
        #region 나머지 코드 (생략 없음)
        private void LoadTimeLogs() { _allTimeLogs.Clear(); if (File.Exists(_timeLogFilePath)) { var json = File.ReadAllText(_timeLogFilePath); var loadedLogs = JsonSerializer.Deserialize<List<TimeLogEntry>>(json); if (loadedLogs != null) _allTimeLogs = loadedLogs; } }
        private void AnalyzeOverallStats() { var totalWorkTime = new TimeSpan(_allTimeLogs.Sum(log => log.Duration.Ticks)); var totalDays = _allTimeLogs.Select(log => log.StartTime.Date).Distinct().Count(); TotalWorkTimeTextBlock.Text = $"{(int)totalWorkTime.TotalHours} 시간 {totalWorkTime.Minutes} 분"; TotalDaysTextBlock.Text = $"{totalDays} 일"; var maxConcentrationTime = _allTimeLogs.Any() ? _allTimeLogs.Max(log => log.Duration) : TimeSpan.Zero; MaxConcentrationTimeTextBlock.Text = $"{(int)maxConcentrationTime.TotalMinutes} 분"; var hourlyWork = _allTimeLogs.GroupBy(log => log.StartTime.Hour).Select(g => new { Hour = g.Key, TotalMinutes = g.Sum(log => log.Duration.TotalMinutes) }).ToList(); var peakHourData = hourlyWork.OrderByDescending(h => h.TotalMinutes).FirstOrDefault(); PeakConcentrationHourTextBlock.Text = peakHourData != null ? $"{peakHourData.Hour} 시 ~ {peakHourData.Hour + 1} 시" : "-"; var chartValues = new ChartValues<double>(); var labels = new string[24]; for (int i = 0; i < 24; i++) { var hourData = hourlyWork.FirstOrDefault(h => h.Hour == i); chartValues.Add(hourData?.TotalMinutes ?? 0); labels[i] = i.ToString(); } HourAnalysisSeries.Clear(); HourAnalysisSeries.Add(new ColumnSeries { Title = "작업량", Values = chartValues }); HourLabels = labels; YFormatter = value => value.ToString("N0"); }
        private void UpdateTaskAnalysis(DateTime startDate, DateTime endDate) { var filteredLogs = _allTimeLogs.Where(log => log.StartTime.Date >= startDate.Date && log.StartTime.Date <= endDate.Date).ToList(); var taskAnalysis = filteredLogs.GroupBy(log => log.TaskText).Select(group => new TaskAnalysisResult { TaskName = group.Key, TotalTime = new TimeSpan(group.Sum(log => log.Duration.Ticks)) }).OrderByDescending(result => result.TotalTime).ToList(); TaskAnalysisListView.ItemsSource = taskAnalysis; }
        private void GenerateFocusBasedAiSuggestion() { var ratedLogs = _allTimeLogs.Where(log => log.FocusScore > 0).ToList(); if (ratedLogs.Count < 3) { GoldenTimeSuggestionTextBlock.Text = "집중도 평가 데이터가 더 쌓이면, 당신의 '황금 시간대'를 분석해 드릴게요!"; return; } var bestSlot = ratedLogs.GroupBy(log => new { log.StartTime.DayOfWeek, log.StartTime.Hour }).Select(g => new { Day = g.Key.DayOfWeek, Hour = g.Key.Hour, AverageFocusScore = g.Average(log => log.FocusScore) }).OrderByDescending(s => s.AverageFocusScore).FirstOrDefault(); if (bestSlot == null) return; var peakTask = ratedLogs.Where(log => log.StartTime.DayOfWeek == bestSlot.Day && log.StartTime.Hour == bestSlot.Hour).GroupBy(log => log.TaskText).Select(g => new { TaskName = g.Key, TotalDuration = g.Sum(log => log.Duration.TotalSeconds) }).OrderByDescending(t => t.TotalDuration).FirstOrDefault(); string peakDayStr = ToKoreanDayOfWeek(bestSlot.Day); string suggestion = (peakTask != null) ? $"분석 결과, 주로 '{peakDayStr} {bestSlot.Hour}시'에 '{peakTask.TaskName}' 과목을 진행할 때 평균 집중도({bestSlot.AverageFocusScore:F1}점)가 가장 높았습니다!" : $"분석 결과, 주로 '{peakDayStr} {bestSlot.Hour}시'에 가장 높은 집중력(평균 {bestSlot.AverageFocusScore:F1}점)을 보여주셨습니다."; GoldenTimeSuggestionTextBlock.Text = suggestion; }
        private void GenerateWorkRestPatternSuggestion() { var ratedLogs = _allTimeLogs.Where(log => log.FocusScore > 0).OrderBy(l => l.StartTime).ToList(); if (ratedLogs.Count < 5) { WorkRestPatternSuggestionTextBlock.Text = "최적의 작업/휴식 패턴을 분석하기 위한 데이터가 조금 더 필요해요."; return; } var patterns = new List<WorkRestPattern>(); for (int i = 0; i < ratedLogs.Count - 1; i++) { var currentLog = ratedLogs[i]; var nextLog = ratedLogs[i + 1]; TimeSpan restTime = nextLog.StartTime - currentLog.EndTime; if (restTime.TotalMinutes > 5 && restTime.TotalMinutes <= 120) { patterns.Add(new WorkRestPattern { WorkDurationMinutes = (int)currentLog.Duration.TotalMinutes, RestDurationMinutes = (int)restTime.TotalMinutes, NextSessionFocusScore = nextLog.FocusScore }); } } if (!patterns.Any()) { WorkRestPatternSuggestionTextBlock.Text = "규칙적인 휴식 패턴을 분석하는 중입니다. 조금만 더 힘내주세요!"; return; } var bestPattern = patterns.GroupBy(p => (int)(p.WorkDurationMinutes / 15)).Select(g => new { WorkGroup = g.Key * 15, BestRest = g.GroupBy(p => (int)(p.RestDurationMinutes / 5)).Select(rg => new { RestGroup = rg.Key * 5, AvgFocus = rg.Average(p => p.NextSessionFocusScore) }).OrderByDescending(rg => rg.AvgFocus).FirstOrDefault() }).Where(x => x.BestRest != null).OrderByDescending(x => x.BestRest.AvgFocus).FirstOrDefault(); if (bestPattern != null) { WorkRestPatternSuggestionTextBlock.Text = $"AI 분석: 약 {bestPattern.WorkGroup}-{bestPattern.WorkGroup + 15}분 작업 후, {bestPattern.BestRest.RestGroup}-{bestPattern.BestRest.RestGroup + 5}분 휴식했을 때 다음 세션의 집중도가 가장 높았습니다. 이 패턴을 활용해보세요!"; } }
        private string ToKoreanDayOfWeek(DayOfWeek day) { switch (day) { case DayOfWeek.Monday: return "월요일"; case DayOfWeek.Tuesday: return "화요일"; case DayOfWeek.Wednesday: return "수요일"; case DayOfWeek.Thursday: return "목요일"; case DayOfWeek.Friday: return "금요일"; case DayOfWeek.Saturday: return "토요일"; case DayOfWeek.Sunday: return "일요일"; default: return string.Empty; } }
        private void TodayButton_Click(object sender, RoutedEventArgs e) { UpdateTaskAnalysis(DateTime.Today, DateTime.Today); }
        private void ThisWeekButton_Click(object sender, RoutedEventArgs e) { var today = DateTime.Today; var dayOfWeek = (int)today.DayOfWeek == 0 ? 6 : (int)today.DayOfWeek - 1; var startDate = today.AddDays(-dayOfWeek); var endDate = startDate.AddDays(6); UpdateTaskAnalysis(startDate, endDate); }
        private void ThisMonthButton_Click(object sender, RoutedEventArgs e) { var today = DateTime.Today; var startDate = new DateTime(today.Year, today.Month, 1); var endDate = startDate.AddMonths(1).AddDays(-1); UpdateTaskAnalysis(startDate, endDate); }
        private void TotalButton_Click(object sender, RoutedEventArgs e) { UpdateTaskAnalysis(DateTime.MinValue, DateTime.MaxValue); }
        private void CustomDateButton_Click(object sender, RoutedEventArgs e) { if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue) { UpdateTaskAnalysis(StartDatePicker.SelectedDate.Value, EndDatePicker.SelectedDate.Value); } else { MessageBox.Show("시작 날짜와 종료 날짜를 모두 선택해주세요."); } }
        #endregion
    }

    public class TaskAnalysisResult { public string TaskName { get; set; } public TimeSpan TotalTime { get; set; } public string TotalTimeFormatted => $"{(int)TotalTime.TotalHours} 시간 {TotalTime.Minutes} 분"; }
    public class WorkRestPattern { public int WorkDurationMinutes { get; set; } public int RestDurationMinutes { get; set; } public int NextSessionFocusScore { get; set; } }
}
