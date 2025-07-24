// 파일: DashboardPage.xaml.cs
// [수정] SetMiniTimerReference 메서드에 본문( { } )을 추가했습니다.
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
using WorkPartner.AI;
using WorkPartner.Properties;

namespace WorkPartner
{
    public partial class DashboardPage : UserControl
    {
        #region 변수 선언
        private readonly string _tasksFilePath = DataManager.TasksFilePath;
        private readonly string _todosFilePath = DataManager.TodosFilePath;
        private readonly string _timeLogFilePath = DataManager.TimeLogFilePath;
        private readonly string _settingsFilePath = DataManager.SettingsFilePath;
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
        private DateTime _lastNagTime;
        private TodoItem _lastAddedTodo;
        private TimeLogEntry _lastUnratedSession;
        private MiniTimerWindow _miniTimer; // 미니 타이머 참조

        // AI 및 미디어 기능용 변수
        private PredictionService _predictionService;
        private MediaPlayer _bgmPlayer;
        private bool _isBgmPlaying = false;
        private DateTime _lastSuggestionTime;
        private DateTime _currentDateForTimeline = DateTime.Today; // 타임라인에 표시할 날짜
        #endregion

        // [추가] 과목별 색상을 저장하기 위한 Dictionary와 색상 팔레트
        private Dictionary<string, SolidColorBrush> _taskColors = new Dictionary<string, SolidColorBrush>();
        private List<SolidColorBrush> _colorPalette = new List<SolidColorBrush>
        {
            new SolidColorBrush(Color.FromRgb(255, 182, 193)), // LightPink
            new SolidColorBrush(Color.FromRgb(173, 216, 230)), // LightBlue
            new SolidColorBrush(Color.FromRgb(144, 238, 144)), // LightGreen
            new SolidColorBrush(Color.FromRgb(255, 255, 224)), // LightYellow
            new SolidColorBrush(Color.FromRgb(221, 160, 221)), // Plum
            new SolidColorBrush(Color.FromRgb(255, 218, 185)), // PeachPuff
            new SolidColorBrush(Color.FromRgb(175, 238, 238)), // PaleTurquoise
            new SolidColorBrush(Color.FromRgb(240, 230, 140)), // Khaki
        };
        private int _colorIndex = 0;

        public DashboardPage()
        {
            InitializeComponent();
            InitializeData();
            LoadAllData();
            InitializeTimer();

            _predictionService = new PredictionService();
            _bgmPlayer = new MediaPlayer();
            _lastSuggestionTime = DateTime.MinValue;
        }

        private void InitializeData()
        {
            TaskItems = new ObservableCollection<TaskItem>(); TaskListBox.ItemsSource = TaskItems;
            TodoItems = new ObservableCollection<TodoItem>(); TodoTreeView.ItemsSource = TodoItems;
            TimeLogEntries = new ObservableCollection<TimeLogEntry>();
            SuggestedTags = new ObservableCollection<string>(); SuggestedTagsItemsControl.ItemsSource = SuggestedTags;
        }

        // [메서드 추가] 과목에 대한 색상을 가져오거나 새로 할당합니다.
        private SolidColorBrush GetColorForTask(string taskName)
        {
            if (!_taskColors.ContainsKey(taskName))
            {
                _taskColors[taskName] = _colorPalette[_colorIndex % _colorPalette.Count];
                _colorIndex++;
            }
            return _taskColors[taskName];
        }

        private void OnSettingsUpdated()
        {
            // AppSettings.Load()는 인스턴스 메서드이므로, 새로운 인스턴스를 로드하여 갱신합니다.
            _settings = AppSettings.Load();
            DataContext = _settings;
        }

        #region 데이터 저장 / 불러오기
        private void InitializeTimer() { _stopwatch = new Stopwatch(); _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; _timer.Tick += Timer_Tick; _timer.Start(); }
        public void LoadAllData() { LoadSettings(); LoadTasks(); LoadTodos(); LoadTimeLogs(); UpdateCoinDisplay(); }
        public void LoadSettings() { if (!File.Exists(_settingsFilePath)) { _settings = new AppSettings(); return; } var json = File.ReadAllText(_settingsFilePath); _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings(); }
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

        private void DashboardPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                LoadSettings();
                UpdateCoinDisplay();
                //DashboardCharacterDisplay.UpdateCharacter();
            }
        }

        private void RateSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastUnratedSession != null && sender is Button button)
            {
                int score = int.Parse(button.Tag.ToString());
                _lastUnratedSession.FocusScore = score;
                SessionReviewPanel.Visibility = Visibility.Collapsed;

                var breakWin = new BreakActivityWindow { Owner = Window.GetWindow(this) };
                if (breakWin.ShowDialog() == true)
                {
                    _lastUnratedSession.BreakActivities = breakWin.SelectedActivities;
                }

                SaveTimeLogs();
                _lastUnratedSession = null;
            }
        }
        private void SaveTodos_Event(object sender, RoutedEventArgs e) { if (sender is CheckBox checkBox && checkBox.DataContext is TodoItem todoItem) { if (todoItem.IsCompleted && !todoItem.HasBeenRewarded) { _settings.Coins += 10; todoItem.HasBeenRewarded = true; UpdateCoinDisplay(); SaveSettings(); SoundPlayer.PlayCompleteSound(); } } SaveTodos(); }
        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { var selectedTask = TaskListBox.SelectedItem as TaskItem; if (_currentWorkingTask != selectedTask) { SessionReviewPanel.Visibility = Visibility.Collapsed; if (_stopwatch.IsRunning) { LogWorkSession(); _stopwatch.Reset(); } _currentWorkingTask = selectedTask; UpdateSelectedTaskTotalTimeDisplay(); if (_currentWorkingTask != null) CurrentTaskDisplay.Text = $"현재 과목: {_currentWorkingTask.Text}"; } }
        private void AddTaskButton_Click(object sender, RoutedEventArgs e) { if (!string.IsNullOrWhiteSpace(TaskInput.Text)) { TaskItems.Add(new TaskItem { Text = TaskInput.Text }); TaskInput.Clear(); SaveTasks(); } }
        private void TaskInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { AddTaskButton_Click(sender, e); } }
        private void DeleteTaskButton_Click(object sender, RoutedEventArgs e) { if (TaskListBox.SelectedItem is TaskItem selectedTask) { TaskItems.Remove(selectedTask); SaveTasks(); } }
        private void AddTodoButton_Click(object sender, RoutedEventArgs e) { if (string.IsNullOrWhiteSpace(TodoInput.Text)) return; var newTodo = new TodoItem { Text = TodoInput.Text }; TodoItems.Add(newTodo); _lastAddedTodo = newTodo; UpdateTagSuggestions(_lastAddedTodo); TodoInput.Clear(); SaveTodos(); }
        private void DeleteTodoButton_Click(object sender, RoutedEventArgs e)
        {
            if (TodoTreeView.SelectedItem is TodoItem selectedTodo)
            {
                if (MessageBox.Show($"'{selectedTodo.Text}' 할 일을 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    // 부모를 찾아서 자식 목록에서 제거하거나, 최상위 항목이면 루트 목록에서 제거
                    var parent = FindParent(null, TodoItems, selectedTodo);
                    if (parent != null)
                    {
                        parent.SubTasks.Remove(selectedTodo);
                    }
                    else
                    {
                        TodoItems.Remove(selectedTodo);
                    }
                    SaveTodos();
                }
            }
            else
            {
                MessageBox.Show("삭제할 할 일을 목록에서 선택해주세요.");
            }
        }
        private void TodoInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddTodoButton_Click(sender, e); }
        private void TodoTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SaveTodos(); }
        private void AddSubTaskMenuItem_Click(object sender, RoutedEventArgs e) { if ((sender as MenuItem)?.DataContext is TodoItem parentTodo) { parentTodo.SubTasks.Add(new TodoItem { Text = "새 하위 작업" }); SaveTodos(); } }
        private void DeleteTodoMenuItem_Click(object sender, RoutedEventArgs e) { DeleteSelectedTodo(); }
        private void AddTagMenuItem_Click(object sender, RoutedEventArgs e) { if ((sender as MenuItem)?.DataContext is TodoItem selectedTodo) { var inputWindow = new InputWindow("추가할 태그를 입력하세요:", "#태그") { Owner = Window.GetWindow(this) }; if (inputWindow.ShowDialog() == true) { string newTag = inputWindow.ResponseText; if (!string.IsNullOrWhiteSpace(newTag) && !selectedTodo.Tags.Contains(newTag)) { selectedTodo.Tags.Add(newTag); SaveTodos(); } } } }
        private void SuggestedTag_Click(object sender, RoutedEventArgs e) { if (_lastAddedTodo != null && sender is Button button) { string tagToAdd = button.Content.ToString(); if (!_lastAddedTodo.Tags.Contains(tagToAdd)) { _lastAddedTodo.Tags.Add(tagToAdd); SuggestedTags.Remove(tagToAdd); SaveTodos(); } } }
        private void AddManualLogButton_Click(object sender, RoutedEventArgs e) { var win = new AddLogWindow(TaskItems) { Owner = Window.GetWindow(this) }; if (win.ShowDialog() == true) { if (win.NewLogEntry != null) TimeLogEntries.Add(win.NewLogEntry); SaveTimeLogs(); RecalculateAllTotals(); RenderTimeTable(); } }
        private void MemoButton_Click(object sender, RoutedEventArgs e) { if (_memoWindow == null || !_memoWindow.IsVisible) { _memoWindow = new MemoWindow { Owner = Window.GetWindow(this) }; _memoWindow.Show(); } else { _memoWindow.Activate(); } }
        private void TimeLogRect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if ((sender as FrameworkElement)?.Tag is TimeLogEntry log) { var win = new AddLogWindow(TaskItems, log) { Owner = Window.GetWindow(this) }; if (win.ShowDialog() == true) { if (win.IsDeleted) TimeLogEntries.Remove(log); else { log.StartTime = win.NewLogEntry.StartTime; log.EndTime = win.NewLogEntry.EndTime; log.TaskText = win.NewLogEntry.TaskText; log.FocusScore = win.NewLogEntry.FocusScore; } SaveTimeLogs(); RecalculateAllTotals(); RenderTimeTable(); } } }
        private void DeleteSelectedTodo() { if (TodoTreeView.SelectedItem is TodoItem selectedTodo) { var parent = FindParent(null, TodoItems, selectedTodo); if (parent != null) { parent.SubTasks.Remove(selectedTodo); } else { TodoItems.Remove(selectedTodo); } SaveTodos(); } else { MessageBox.Show("삭제할 할 일을 목록에서 선택해주세요."); } }

        private void BgmPlayButton_Click(object sender, RoutedEventArgs e)
        {
            string bgmFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "whitenoise.mp3");
            if (!File.Exists(bgmFilePath)) { MessageBox.Show("백색 소음 파일을 찾을 수 없습니다.\n'Sounds/whitenoise.mp3' 경로에 파일을 추가해주세요.", "오류"); return; }
            if (_isBgmPlaying) { _bgmPlayer.Stop(); BgmPlayButton.Content = "백색 소음 재생"; }
            else { _bgmPlayer.Open(new Uri(bgmFilePath)); _bgmPlayer.Play(); BgmPlayButton.Content = "재생 중..."; }
            _isBgmPlaying = !_isBgmPlaying;
        }
        #endregion

        #region 핵심 로직
        // [수정] SetMiniTimerReference 메서드에 본문을 추가하여 오류를 해결합니다.
        public void SetMiniTimerReference(MiniTimerWindow timer)
        {
            _miniTimer = timer;
        }

        // ✂️ 이 코드로 기존 Timer_Tick 메서드를 교체하세요 ✂️
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_stopwatch.IsRunning && _lastUnratedSession != null)
            {
                SessionReviewPanel.Visibility = Visibility.Collapsed;
                _lastUnratedSession = null;
            }
            HandleStopwatchMode();
            CheckFocusAndSuggest();

            // [수정] 집중 모드일 때 방해 프로그램 실행 감지
            if (_isFocusModeActive)
            {
                string activeProcess = ActiveWindowHelper.GetActiveProcessName();
                string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl();
                string keywordToCheck = !string.IsNullOrEmpty(activeUrl) ? activeUrl : activeProcess;

                // [오류 수정] Settings -> _settings 로 변경
                bool isDistracted = _settings.DistractionProcesses.Any(p => keywordToCheck.Contains(p));

                if (isDistracted && (DateTime.Now - _lastNagTime).TotalSeconds > _settings.FocusModeNagIntervalSeconds)
                {
                    MessageBox.Show(_settings.FocusModeNagMessage, "집중!", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _lastNagTime = DateTime.Now;
                }
            }
        }

        private void LogWorkSession()
        {
            if (_currentWorkingTask == null || _stopwatch.Elapsed.TotalSeconds < 1) return;
            var entry = new TimeLogEntry { StartTime = _sessionStartTime, EndTime = _sessionStartTime.Add(_stopwatch.Elapsed), TaskText = _currentWorkingTask.Text, FocusScore = 0 };
            TimeLogEntries.Insert(0, entry);
            SaveTimeLogs();
            RecalculateAllTotals();
            RenderTimeTable();
            _lastUnratedSession = entry;
            SessionReviewPanel.Visibility = Visibility.Visible;
        }

        private void CheckFocusAndSuggest()
        {
            if ((DateTime.Now - _lastSuggestionTime).TotalSeconds < 60) return;
            if (!_stopwatch.IsRunning || _currentWorkingTask == null)
            {
                if (AiSuggestionTextBlock != null)
                {
                    AiSuggestionTextBlock.Text = "";
                }
                return;
            }

            _lastSuggestionTime = DateTime.Now;

            var input = new ModelInput
            {
                TaskName = _currentWorkingTask.Text,
                DayOfWeek = (float)DateTime.Now.DayOfWeek,
                Hour = (float)DateTime.Now.Hour,
                Duration = 60
            };
            float predictedScore = _predictionService.Predict(input);

            string suggestion = "";
            if (predictedScore > 0)
            {
                if (predictedScore >= 4.0)
                {
                    suggestion = "AI: 지금은 집중력이 최고조에 달할 시간입니다! 가장 어려운 과제를 처리해보세요.";
                    SoundPlayer.PlayNotificationSound();
                }
                else if (predictedScore < 2.5)
                {
                    suggestion = $"AI: 현재 '{_currentWorkingTask.Text}' 작업의 예상 집중도가 낮습니다. 5분간 휴식 후 다시 시작하는 건 어떠신가요?";
                    SoundPlayer.PlayNotificationSound();
                }
            }

            if (AiSuggestionTextBlock != null)
            {
                AiSuggestionTextBlock.Text = suggestion;
            }
        }

        private void UpdateCoinDisplay() { if (_settings != null) { CoinDisplayTextBlock.Text = _settings.Coins.ToString("N0"); } }

        // DashboardPage.xaml.cs

        private void HandleStopwatchMode()
        {
            string activeProcess = ActiveWindowHelper.GetActiveProcessName();
            string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl(); // <-- URL을 직접 가져옵니다.
            string activeTitle = string.IsNullOrEmpty(activeUrl) ? ActiveWindowHelper.GetActiveWindowTitle().ToLower() : activeUrl;

            ActiveProcessDisplay.Text = $"활성: {activeTitle}";

            // 검사할 키워드 (URL이 있으면 URL, 없으면 프로세스 이름)
            string keywordToCheck = !string.IsNullOrEmpty(activeUrl) ? activeUrl : activeProcess;

            // 방해 요소 검사
            if (_settings.DistractionProcesses.Any(p => keywordToCheck.Contains(p)))
            {
                if (_stopwatch.IsRunning) { LogWorkSession(); _stopwatch.Reset(); }
                CurrentTaskDisplay.Text = "[딴짓 중!]";
                return;
            }

            // 작업 & 수동 프로그램 검사
            bool isTrackable = _settings.WorkProcesses.Any(p => keywordToCheck.Contains(p));
            bool isPassive = _settings.PassiveProcesses.Any(p => keywordToCheck.Contains(p));

            // ... (이하 로직은 기존과 동일) ...
            if (isTrackable || isPassive)
            {
                bool isIdle = ActiveWindowHelper.GetIdleTime().TotalSeconds > _settings.IdleTimeoutSeconds;
                if (_settings.IsIdleDetectionEnabled && !isPassive && isIdle)
                {
                    if (_stopwatch.IsRunning) { LogWorkSession(); _stopwatch.Reset(); }
                    CurrentTaskDisplay.Text = $"[자리 비움] {_currentWorkingTask?.Text ?? ""}";
                }
                else
                {
                    if (!_stopwatch.IsRunning)
                    {
                        if (_currentWorkingTask == null && TaskItems.Any()) { TaskListBox.SelectedIndex = 0; }
                        if (_currentWorkingTask != null) { _sessionStartTime = DateTime.Now; _stopwatch.Start(); }
                    }
                    CurrentTaskDisplay.Text = $"현재 과목: {_currentWorkingTask?.Text ?? "선택된 과목 없음"}";
                }
            }
            else
            {
                if (_stopwatch.IsRunning) { LogWorkSession(); _stopwatch.Reset(); }
                CurrentTaskDisplay.Text = "선택된 과목 없음";
            }
            UpdateLiveTimeDisplays();
        }
        // DashboardPage.xaml.cs

        private void UpdateLiveTimeDisplays()
        {
            if (_stopwatch.IsRunning)
            {
                TimeSpan realTimeTotal = _totalTimeTodayFromLogs + _stopwatch.Elapsed;
                string timeString = realTimeTotal.ToString(@"hh\:mm\:ss");
                MainTimeDisplay.Text = timeString;

                // ▼▼▼ 이 코드를 추가하세요 ▼▼▼
                _miniTimer?.SetRunningStyle(); // 실행 중 스타일 적용
                _miniTimer?.UpdateTime(timeString);

                if (_currentWorkingTask != null)
                {
                    TimeSpan realTimeSelectedTaskTotal = _selectedTaskTotalTimeFromLogs + _stopwatch.Elapsed;
                    SelectedTaskTotalTimeDisplay.Text = $"선택 과목 총계: {realTimeSelectedTaskTotal:hh\\:mm\\:ss}";
                }
            }
            else
            {
                string timeString = _totalTimeTodayFromLogs.ToString(@"hh\:mm\:ss");
                MainTimeDisplay.Text = timeString;

                // ▼▼▼ 이 코드를 추가하세요 ▼▼▼
                _miniTimer?.SetStoppedStyle(); // 멈춤 스타일 적용
                _miniTimer?.UpdateTime(timeString);
            }
        }
        private void UpdateTagSuggestions(TodoItem todo) { SuggestedTags.Clear(); if (todo == null) return; var suggestions = new List<string>(); if (_currentWorkingTask != null && !string.IsNullOrWhiteSpace(_currentWorkingTask.Text)) { suggestions.Add($"#{_currentWorkingTask.Text}"); } if (_settings != null && _settings.TagRules != null) { foreach (var rule in _settings.TagRules) { if (todo.Text.ToLower().Contains(rule.Key.ToLower())) { suggestions.Add(rule.Value); } } } foreach (var suggestion in suggestions.Distinct().Except(todo.Tags)) { SuggestedTags.Add(suggestion); } }
        private void UpdateSelectedTaskTotalTimeDisplay() { if (_currentWorkingTask != null) { var taskLogs = TimeLogEntries.Where(log => log.TaskText == _currentWorkingTask.Text && log.StartTime.Date == DateTime.Today.Date); _selectedTaskTotalTimeFromLogs = new TimeSpan(taskLogs.Sum(log => log.Duration.Ticks)); SelectedTaskTotalTimeDisplay.Text = $"선택 과목 총계: {_selectedTaskTotalTimeFromLogs:hh\\:mm\\:ss}"; } else { _selectedTaskTotalTimeFromLogs = TimeSpan.Zero; SelectedTaskTotalTimeDisplay.Text = "선택 과목 총계: 00:00:00"; } }
        private void RecalculateAllTotals() { var todayLogs = TimeLogEntries.Where(log => log.StartTime.Date == DateTime.Today.Date); _totalTimeTodayFromLogs = new TimeSpan(todayLogs.Sum(log => log.Duration.Ticks)); UpdateLiveTimeDisplays(); UpdateSelectedTaskTotalTimeDisplay(); }

        // [수정] RenderTimeTable 메서드 개선
        private void RenderTimeTable()
        {
            TimeTableContainer.Children.Clear();

            var todayLogs = TimeLogEntries.Where(log => log.StartTime.Date == _currentDateForTimeline.Date).ToList();

            // 24시간을 세로로 반복
            for (int hour = 0; hour < 24; hour++)
            {
                // 각 시간을 위한 가로 패널 생성
                var hourRowPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 1, 0, 1)
                };

                // 1. 시간 레이블 추가
                var hourLabel = new TextBlock
                {
                    Text = $"{hour:00}",
                    Width = 30,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Foreground = Brushes.Gray
                };
                hourRowPanel.Children.Add(hourLabel);

                // 2. 해당 시간의 10분 블록 6개를 가로로 추가
                for (int minuteBlock = 0; minuteBlock < 6; minuteBlock++)
                {
                    var blockStartTime = new TimeSpan(hour, minuteBlock * 10, 0);
                    var blockEndTime = blockStartTime.Add(TimeSpan.FromMinutes(10));

                    // [수정] 10분 블록을 담을 Grid 컨테이너 생성
                    var blockContainer = new Grid
                    {
                        Width = 60,
                        Height = 20,
                        Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5)),
                        Margin = new Thickness(1, 0, 1, 0)
                    };

                    // 이 10분 블록과 겹치는 모든 로그를 찾음
                    var overlappingLogs = todayLogs.Where(log =>
                        log.StartTime.TimeOfDay < blockEndTime && log.EndTime.TimeOfDay > blockStartTime
                    ).ToList();

                    foreach (var logEntry in overlappingLogs)
                    {
                        // 블록 내에서 로그가 실제로 차지하는 시작과 끝 시간을 계산
                        var segmentStart = logEntry.StartTime.TimeOfDay > blockStartTime ? logEntry.StartTime.TimeOfDay : blockStartTime;
                        var segmentEnd = logEntry.EndTime.TimeOfDay < blockEndTime ? logEntry.EndTime.TimeOfDay : blockEndTime;

                        var segmentDuration = segmentEnd - segmentStart;

                        // 실제 시간에 비례하여 막대의 너비와 위치를 계산
                        double totalBlockWidth = blockContainer.Width;
                        double barWidth = (segmentDuration.TotalMinutes / 10.0) * totalBlockWidth;
                        double leftOffset = ((segmentStart - blockStartTime).TotalMinutes / 10.0) * totalBlockWidth;

                        if (barWidth < 1) continue; // 너무 작은 조각은 그리지 않음

                        var coloredBar = new Border
                        {
                            Width = barWidth,
                            Height = blockContainer.Height,
                            Background = GetColorForTask(logEntry.TaskText),
                            CornerRadius = new CornerRadius(2),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(leftOffset, 0, 0, 0),
                            ToolTip = new ToolTip { Content = $"{logEntry.TaskText}\n{logEntry.StartTime:HH:mm} ~ {logEntry.EndTime:HH:mm}" },
                            Tag = logEntry,
                            Cursor = Cursors.Hand
                        };
                        coloredBar.MouseLeftButtonDown += TimeLogRect_MouseLeftButtonDown;

                        blockContainer.Children.Add(coloredBar);
                    }

                    // 테두리를 포함한 최종 블록을 패널에 추가
                    var blockWithBorder = new Border
                    {
                        BorderBrush = Brushes.White,
                        BorderThickness = new Thickness(1, 0, (minuteBlock + 1) % 6 == 0 ? 1 : 0, 0),
                        Child = blockContainer
                    };

                    hourRowPanel.Children.Add(blockWithBorder);
                }

                TimeTableContainer.Children.Add(hourRowPanel);
            }
        }

        // DashboardPage.xaml.cs 클래스 내부 아무 곳에나 추가

        private void PrevDayButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDateForTimeline = _currentDateForTimeline.AddDays(-1);
            TimelineDatePicker.SelectedDate = _currentDateForTimeline; // 달력 날짜도 함께 변경
            RenderTimeTable();
            RecalculateAllTotals(); // 해당 날짜의 통계도 다시 계산
        }

        private void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDateForTimeline = DateTime.Today;
            TimelineDatePicker.SelectedDate = _currentDateForTimeline;
            RenderTimeTable();
            RecalculateAllTotals();
        }

        private void NextDayButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDateForTimeline = _currentDateForTimeline.AddDays(1);
            TimelineDatePicker.SelectedDate = _currentDateForTimeline;
            RenderTimeTable();
            RecalculateAllTotals();
        }

        private void TimelineDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TimelineDatePicker.SelectedDate.HasValue)
            {
                _currentDateForTimeline = TimelineDatePicker.SelectedDate.Value;
                RenderTimeTable();
                RecalculateAllTotals();
            }
        }

        private TodoItem FindParent(TodoItem currentParent, ObservableCollection<TodoItem> items, TodoItem target) { if (items.Contains(target)) return currentParent; foreach (var item in items) { var found = FindParent(item, item.SubTasks, target); if (found != null) return found; } return null; }
        #endregion

        // ✂️ 이 코드를 파일 맨 아래, 마지막 '}' 앞에 추가하세요 ✂️
        private void FocusModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isFocusModeActive = !_isFocusModeActive;

            if (_isFocusModeActive)
            {
                FocusModeButton.Background = new SolidColorBrush(Color.FromRgb(0, 122, 255));
                FocusModeButton.Foreground = Brushes.White;
                MessageBox.Show("집중 모드가 활성화되었습니다. 방해 앱으로 등록된 프로그램을 실행하면 경고가 표시됩니다.", "집중 모드 ON");
            }
            else
            {
                FocusModeButton.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xEF, 0xEF));
                FocusModeButton.Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            }
        }

    }
}