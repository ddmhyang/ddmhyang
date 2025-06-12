using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WorkPartner
{
    public partial class DashboardPage : UserControl
    {
        #region 변수 선언
        private readonly string _tasksFilePath = "tasks.json";
        private readonly string _todosFilePath = "todos.json";
        private readonly string _timeLogFilePath = "timelogs.json";
        private readonly string _settingsFilePath = "app_settings.json";
        public ObservableCollection<TaskItem> TaskItems { get; set; }
        public ObservableCollection<TodoItem> TodoItems { get; set; }
        public ObservableCollection<TimeLogEntry> TimeLogEntries { get; set; }
        public ObservableCollection<string> SuggestedTags { get; set; }
        private DispatcherTimer _timer;
        private Stopwatch _stopwatch;
        private TaskItem _currentWorkingTask;
        private DateTime _sessionStartTime;
        private TimeSpan _totalTimeTodayFromLogs;
        private TimeSpan _selectedTaskTotalTimeFromLogs;
        private MemoWindow _memoWindow;
        private AppSettings _settings;
        private bool _isFocusModeActive = false;
        private bool _isPausedForIdle = false;
        private bool _isPausedForDistraction = false;
        private DateTime _lastNagTime;
        private TodoItem _lastAddedTodo;
        private TimeLogEntry _lastUnratedSession;
        #endregion

        public DashboardPage()
        {
            InitializeComponent();
            InitializeData();
            LoadAllData();
            InitializeTimer();
        }

        private void InitializeData()
        {
            TaskItems = new ObservableCollection<TaskItem>(); TaskListBox.ItemsSource = TaskItems;
            TodoItems = new ObservableCollection<TodoItem>(); TodoTreeView.ItemsSource = TodoItems;
            TimeLogEntries = new ObservableCollection<TimeLogEntry>();
            SuggestedTags = new ObservableCollection<string>(); SuggestedTagsItemsControl.ItemsSource = SuggestedTags;
        }

        #region 데이터 저장 / 불러오기
        private void InitializeTimer() { _stopwatch = new Stopwatch(); _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; _timer.Tick += Timer_Tick; _timer.Start(); }
        private void LoadAllData() { LoadSettings(); LoadTasks(); LoadTodos(); LoadTimeLogs(); UpdateCoinDisplay(); }
        private void LoadSettings() { if (!File.Exists(_settingsFilePath)) { _settings = new AppSettings(); return; } var json = File.ReadAllText(_settingsFilePath); _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings(); }
        private void SaveSettings() { var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }; var json = JsonSerializer.Serialize(_settings, options); File.WriteAllText(_settingsFilePath, json); }
        private void LoadTasks() { if (!File.Exists(_tasksFilePath)) return; var json = File.ReadAllText(_tasksFilePath); TaskItems = JsonSerializer.Deserialize<ObservableCollection<TaskItem>>(json) ?? new ObservableCollection<TaskItem>(); TaskListBox.ItemsSource = TaskItems; }
        private void SaveTasks() { var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }; var json = JsonSerializer.Serialize(TaskItems, options); File.WriteAllText(_tasksFilePath, json); }
        private void LoadTodos() { if (!File.Exists(_todosFilePath)) return; var json = File.ReadAllText(_todosFilePath); TodoItems = JsonSerializer.Deserialize<ObservableCollection<TodoItem>>(json) ?? new ObservableCollection<TodoItem>(); TodoTreeView.ItemsSource = TodoItems; }
        private void SaveTodos() { var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }; var json = JsonSerializer.Serialize(TodoItems, options); File.WriteAllText(_todosFilePath, json); }
        private void LoadTimeLogs() { if (!File.Exists(_timeLogFilePath)) return; var json = File.ReadAllText(_timeLogFilePath); TimeLogEntries = JsonSerializer.Deserialize<ObservableCollection<TimeLogEntry>>(json) ?? new ObservableCollection<TimeLogEntry>(); }
        private void SaveTimeLogs() { var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }; var json = JsonSerializer.Serialize(TimeLogEntries, options); File.WriteAllText(_timeLogFilePath, json); }
        #endregion

        #region UI 이벤트 핸들러
        private void Window_Loaded(object sender, RoutedEventArgs e) { RecalculateAllTotals(); RenderTimeTable(); }
        private void DashboardPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) { if (e.NewValue is true) { LoadSettings(); UpdateCoinDisplay(); } }

        private void SaveTodos_Event(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is TodoItem todoItem)
            {
                if (todoItem.IsCompleted && !todoItem.HasBeenRewarded)
                {
                    _settings.Coins += 10;
                    todoItem.HasBeenRewarded = true;
                    UpdateCoinDisplay();
                    SaveSettings();
                }
            }
            SaveTodos();
        }

        private void RateSessionButton_Click(object sender, RoutedEventArgs e) { if (_lastUnratedSession != null && sender is Button button) { int score = int.Parse(button.Tag.ToString()); _lastUnratedSession.FocusScore = score; SaveTimeLogs(); SessionReviewPanel.Visibility = Visibility.Collapsed; _lastUnratedSession = null; } }
        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { var selectedTask = TaskListBox.SelectedItem as TaskItem; if (_currentWorkingTask != selectedTask) { SessionReviewPanel.Visibility = Visibility.Collapsed; if (_stopwatch.IsRunning) { LogWorkSession(); _stopwatch.Reset(); } _currentWorkingTask = selectedTask; UpdateSelectedTaskTotalTimeDisplay(); if (_currentWorkingTask != null) CurrentTaskDisplay.Text = $"현재 과목: {_currentWorkingTask.Text}"; } }
        private void AddTaskButton_Click(object sender, RoutedEventArgs e) { if (!string.IsNullOrWhiteSpace(TaskInput.Text)) { TaskItems.Add(new TaskItem { Text = TaskInput.Text }); TaskInput.Clear(); SaveTasks(); } }
        private void TaskInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { AddTaskButton_Click(sender, e); } }
        private void DeleteTaskButton_Click(object sender, RoutedEventArgs e) { if (TaskListBox.SelectedItem is TaskItem selectedTask) { TaskItems.Remove(selectedTask); SaveTasks(); } }
        private void AddTodoButton_Click(object sender, RoutedEventArgs e) { if (string.IsNullOrWhiteSpace(TodoInput.Text)) return; var newTodo = new TodoItem { Text = TodoInput.Text }; TodoItems.Add(newTodo); _lastAddedTodo = newTodo; UpdateTagSuggestions(_lastAddedTodo); TodoInput.Clear(); SaveTodos(); }
        private void DeleteTodoButton_Click(object sender, RoutedEventArgs e) { DeleteSelectedTodo(); }
        private void TodoInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddTodoButton_Click(sender, e); }
        private void TodoTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SaveTodos(); }
        private void AddSubTaskMenuItem_Click(object sender, RoutedEventArgs e) { if ((sender as MenuItem)?.DataContext is TodoItem parentTodo) { parentTodo.SubTasks.Add(new TodoItem { Text = "새 하위 작업" }); SaveTodos(); } }
        private void DeleteTodoMenuItem_Click(object sender, RoutedEventArgs e) { DeleteSelectedTodo(); }
        private void AddTagMenuItem_Click(object sender, RoutedEventArgs e) { if ((sender as MenuItem)?.DataContext is TodoItem selectedTodo) { var inputWindow = new InputWindow("추가할 태그를 입력하세요:", "#태그") { Owner = Window.GetWindow(this) }; if (inputWindow.ShowDialog() == true) { string newTag = inputWindow.ResponseText; if (!string.IsNullOrWhiteSpace(newTag) && !selectedTodo.Tags.Contains(newTag)) { selectedTodo.Tags.Add(newTag); SaveTodos(); } } } }
        private void SuggestedTag_Click(object sender, RoutedEventArgs e) { if (_lastAddedTodo != null && sender is Button button) { string tagToAdd = button.Content.ToString(); if (!_lastAddedTodo.Tags.Contains(tagToAdd)) { _lastAddedTodo.Tags.Add(tagToAdd); SuggestedTags.Remove(tagToAdd); SaveTodos(); } } }
        private void DiaryButton_Click(object sender, RoutedEventArgs e) { if (DashboardDetailsPanel.Visibility == Visibility.Visible) { DashboardDetailsPanel.Visibility = Visibility.Collapsed; DiaryButton.Content = "다이어리 보기"; } else { DashboardDetailsPanel.Visibility = Visibility.Visible; DiaryButton.Content = "다이어리 닫기"; } }
        private void FocusModeButton_Click(object sender, RoutedEventArgs e) { _isFocusModeActive = !_isFocusModeActive; if (_isFocusModeActive) { FocusModeButton.Content = "집중 모드 진행 중..."; _lastNagTime = DateTime.Now; } else { FocusModeButton.Content = "집중 모드 시작"; } }
        private void AddManualLogButton_Click(object sender, RoutedEventArgs e) { var win = new AddLogWindow(TaskItems) { Owner = Window.GetWindow(this) }; if (win.ShowDialog() == true) { if (win.NewLogEntry != null) TimeLogEntries.Add(win.NewLogEntry); SaveTimeLogs(); RecalculateAllTotals(); RenderTimeTable(); } }
        private void MemoButton_Click(object sender, RoutedEventArgs e) { if (_memoWindow == null || !_memoWindow.IsVisible) { _memoWindow = new MemoWindow { Owner = Window.GetWindow(this) }; _memoWindow.Show(); } else { _memoWindow.Activate(); } }
        private void TimeLogRect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if ((sender as FrameworkElement)?.Tag is TimeLogEntry log) { var win = new AddLogWindow(TaskItems, log) { Owner = Window.GetWindow(this) }; if (win.ShowDialog() == true) { if (win.IsDeleted) TimeLogEntries.Remove(log); else { log.StartTime = win.NewLogEntry.StartTime; log.EndTime = win.NewLogEntry.EndTime; log.TaskText = win.NewLogEntry.TaskText; } SaveTimeLogs(); RecalculateAllTotals(); RenderTimeTable(); } } }
        private void DeleteSelectedTodo() { if (TodoTreeView.SelectedItem is TodoItem selectedTodo) { var parent = FindParent(null, TodoItems, selectedTodo); if (parent != null) { parent.SubTasks.Remove(selectedTodo); } else { TodoItems.Remove(selectedTodo); } SaveTodos(); } else { MessageBox.Show("삭제할 할 일을 목록에서 선택해주세요."); } }
        #endregion

        #region 핵심 로직
        private void UpdateCoinDisplay() { if (_settings != null) { CoinDisplayTextBlock.Text = _settings.Coins.ToString("N0"); } }
        private void LogWorkSession() { if (_currentWorkingTask == null || _stopwatch.Elapsed.TotalSeconds < 1) return; var entry = new TimeLogEntry { StartTime = _sessionStartTime, EndTime = _sessionStartTime.Add(_stopwatch.Elapsed), TaskText = _currentWorkingTask.Text, FocusScore = 0 }; TimeLogEntries.Insert(0, entry); SaveTimeLogs(); RecalculateAllTotals(); RenderTimeTable(); _lastUnratedSession = entry; SessionReviewPanel.Visibility = Visibility.Visible; }
        private void Timer_Tick(object sender, EventArgs e) { if (_stopwatch.IsRunning && SessionReviewPanel.Visibility == Visibility.Visible) { SessionReviewPanel.Visibility = Visibility.Collapsed; _lastUnratedSession = null; } HandleStopwatchMode(); }
        private void HandleStopwatchMode() { string activeProcess = ActiveWindowHelper.GetActiveProcessName(); ActiveProcessDisplay.Text = $"활성 프로그램: {activeProcess}"; if (_settings.DistractionProcesses.Contains(activeProcess)) { if (_stopwatch.IsRunning) { LogWorkSession(); _stopwatch.Reset(); } _isPausedForDistraction = true; CurrentTaskDisplay.Text = "[딴짓 중!]"; ShowNagMessageIfNeeded(); } else { _isPausedForDistraction = false; bool isTrackable = _settings.WorkProcesses.Contains(activeProcess); bool isPassive = _settings.PassiveProcesses.Contains(activeProcess); if (isTrackable || isPassive) { bool isIdle = ActiveWindowHelper.GetIdleTime().TotalSeconds > _settings.IdleTimeoutSeconds; if (_settings.IsIdleDetectionEnabled && !isPassive && isIdle) { if (_stopwatch.IsRunning) { LogWorkSession(); _stopwatch.Reset(); } _isPausedForIdle = true; CurrentTaskDisplay.Text = $"[자리 비움] {_currentWorkingTask?.Text ?? ""}"; } else { _isPausedForIdle = false; if (!_stopwatch.IsRunning) { if (_currentWorkingTask == null && TaskItems.Any()) { TaskListBox.SelectedIndex = 0; } if (_currentWorkingTask != null) { _sessionStartTime = DateTime.Now; _stopwatch.Start(); } } CurrentTaskDisplay.Text = $"현재 과목: {_currentWorkingTask?.Text ?? "선택된 과목 없음"}"; } } else { if (_stopwatch.IsRunning) { LogWorkSession(); _stopwatch.Reset(); } CurrentTaskDisplay.Text = "선택된 과목 없음"; } } UpdateLiveTimeDisplays(); }
        private void ShowNagMessageIfNeeded() { TimeSpan nagInterval = TimeSpan.FromSeconds(_settings.FocusModeNagIntervalSeconds); if (_isFocusModeActive && (DateTime.Now - _lastNagTime) > nagInterval) { MessageBox.Show(Window.GetWindow(this), _settings.FocusModeNagMessage, "경고", MessageBoxButton.OK, MessageBoxImage.Warning); _lastNagTime = DateTime.Now; } }
        private void UpdateLiveTimeDisplays() { if (_stopwatch.IsRunning) { TimeSpan realTimeTotal = _totalTimeTodayFromLogs + _stopwatch.Elapsed; MainTimeDisplay.Text = realTimeTotal.ToString(@"hh\:mm\:ss"); if (_currentWorkingTask != null) { TimeSpan realTimeSelectedTaskTotal = _selectedTaskTotalTimeFromLogs + _stopwatch.Elapsed; SelectedTaskTotalTimeDisplay.Text = $"선택 과목 총계: {realTimeSelectedTaskTotal:hh\\:mm\\:ss}"; } } }
        private void UpdateTagSuggestions(TodoItem todo) { SuggestedTags.Clear(); if (todo == null) return; var suggestions = new List<string>(); if (_currentWorkingTask != null && !string.IsNullOrWhiteSpace(_currentWorkingTask.Text)) { suggestions.Add($"#{_currentWorkingTask.Text}"); } if (_settings != null && _settings.TagRules != null) { foreach (var rule in _settings.TagRules) { if (todo.Text.ToLower().Contains(rule.Key.ToLower())) { suggestions.Add(rule.Value); } } } foreach (var suggestion in suggestions.Distinct().Except(todo.Tags)) { SuggestedTags.Add(suggestion); } }
        private void UpdateSelectedTaskTotalTimeDisplay() { if (_currentWorkingTask != null) { var taskLogs = TimeLogEntries.Where(log => log.TaskText == _currentWorkingTask.Text && log.StartTime.Date == DateTime.Today.Date); _selectedTaskTotalTimeFromLogs = new TimeSpan(taskLogs.Sum(log => log.Duration.Ticks)); SelectedTaskTotalTimeDisplay.Text = $"선택 과목 총계: {_selectedTaskTotalTimeFromLogs:hh\\:mm\\:ss}"; } else { _selectedTaskTotalTimeFromLogs = TimeSpan.Zero; SelectedTaskTotalTimeDisplay.Text = "선택 과목 총계: 00:00:00"; } }
        private void RecalculateAllTotals() { var todayLogs = TimeLogEntries.Where(log => log.StartTime.Date == DateTime.Today.Date); _totalTimeTodayFromLogs = new TimeSpan(todayLogs.Sum(log => log.Duration.Ticks)); MainTimeDisplay.Text = _totalTimeTodayFromLogs.ToString(@"hh\:mm\:ss"); UpdateSelectedTaskTotalTimeDisplay(); }
        private void ResetStopwatchState() { _stopwatch.Reset(); _isPausedForIdle = false; _isPausedForDistraction = false; RecalculateAllTotals(); }
        private void RenderTimeTable() { TimeTableCanvas.Children.Clear(); for (int i = 0; i < 24; i++) { var line = new Line { X1 = 35, Y1 = i * 60, X2 = TimeTableCanvas.ActualWidth, Y2 = i * 60, Stroke = Brushes.LightGray, StrokeThickness = (i % 6 == 0) ? 1 : 0.5 }; var txt = new TextBlock { Text = $"{i:00}:00", Foreground = Brushes.Gray, FontSize = 10 }; Canvas.SetTop(line, i * 60); Canvas.SetLeft(line, 0); Canvas.SetTop(txt, i * 60 - 7); Canvas.SetLeft(txt, 5); TimeTableCanvas.Children.Add(line); TimeTableCanvas.Children.Add(txt); } foreach (var entry in TimeLogEntries.Where(log => log.StartTime.Date == DateTime.Today.Date).Reverse()) { double top = entry.StartTime.TimeOfDay.TotalMinutes; double h = Math.Max(1, entry.Duration.TotalMinutes); var rect = new Border { Height = h, Width = Math.Max(10, TimeTableCanvas.ActualWidth - 50), Background = Brushes.SkyBlue, CornerRadius = new CornerRadius(2), BorderBrush = Brushes.CornflowerBlue, BorderThickness = new Thickness(1), ToolTip = new ToolTip { Content = $"{entry.TaskText}\n{entry.StartTime:HH:mm} ~ {entry.EndTime:HH:mm}" }, Tag = entry }; rect.MouseLeftButtonDown += TimeLogRect_MouseLeftButtonDown; Canvas.SetTop(rect, top); Canvas.SetLeft(rect, 40); TimeTableCanvas.Children.Add(rect); } }
        private TodoItem FindParent(TodoItem currentParent, ObservableCollection<TodoItem> items, TodoItem target) { if (items.Contains(target)) return currentParent; foreach (var item in items) { var found = FindParent(item, item.SubTasks, target); if (found != null) return found; } return null; }
        #endregion
    }
}
