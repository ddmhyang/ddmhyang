// 파일: DashboardPage.xaml.cs (수정)
// [수정] TreeView 자체의 PreviewMouseLeftButtonDown 이벤트를 사용하여 항목 선택 로직을 안정적으로 변경했습니다.
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
        public ObservableCollection<TodoItem> TodoItems { get; set; } // 모든 ToDo 항목
        public ObservableCollection<TodoItem> FilteredTodoItems { get; set; } // 화면에 표시될 ToDo 항목
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
        private MiniTimerWindow _miniTimer;

        // AI 및 미디어 기능용 변수
        private PredictionService _predictionService;
        private SoundManager _soundManager; // MediaPlayer를 SoundManager로 변경
        private bool _isMuted = false;
        private Dictionary<string, double> _lastVolumes = new Dictionary<string, double>();
        private DateTime _lastSuggestionTime;
        private DateTime _currentDateForTimeline = DateTime.Today; // 타임라인에 표시할 날짜

        // 자리 비움 감지 관련 변수
        private bool _isPausedForIdle = false;
        private DateTime _idleStartTime;
        private const int IdleGraceSeconds = 10; // 10초 유예 시간

        // 타임라인 드래그 선택 관련 변수
        private Point _dragStartPoint;
        private Rectangle _selectionBox;
        private bool _isDragging = false;
        #endregion

        public DashboardPage()
        {
            InitializeComponent();
            InitializeData();
            LoadAllData();
            InitializeTimer();

            _predictionService = new PredictionService();
            _soundManager = new SoundManager(); // SoundManager 인스턴스 생성
            _lastSuggestionTime = DateTime.MinValue;
            DataManager.SettingsUpdated += OnSettingsUpdated;

            // 드래그 선택 상자 초기화
            _selectionBox = new Rectangle
            {
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 1,
                Fill = new SolidColorBrush(Color.FromArgb(50, 30, 144, 255)),
                Visibility = Visibility.Collapsed
            };
            if (!SelectionCanvas.Children.Contains(_selectionBox))
            {
                SelectionCanvas.Children.Add(_selectionBox);
            }
        }

        private void InitializeData()
        {
            TaskItems = new ObservableCollection<TaskItem>();
            TaskListBox.ItemsSource = TaskItems;

            TodoItems = new ObservableCollection<TodoItem>();
            FilteredTodoItems = new ObservableCollection<TodoItem>();
            TodoTreeView.ItemsSource = FilteredTodoItems;

            TimeLogEntries = new ObservableCollection<TimeLogEntry>();
            SuggestedTags = new ObservableCollection<string>();
            SuggestedTagsItemsControl.ItemsSource = SuggestedTags;
        }

        private SolidColorBrush GetColorForTask(string taskName)
        {
            if (_settings.TaskColors.TryGetValue(taskName, out string colorHex))
            {
                try
                {
                    return (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex);
                }
                catch { /* 잘못된 색상 코드 무시 */ }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        #region 데이터 저장 / 불러오기
        private void InitializeTimer() { _stopwatch = new Stopwatch(); _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; _timer.Tick += Timer_Tick; _timer.Start(); }

        public void LoadAllData()
        {
            LoadSettings();
            LoadTasks();
            LoadTodos();
            LoadTimeLogs();
            UpdateCoinDisplay();
        }

        public AppSettings Settings
        {
            get { return _settings; }
            set { _settings = value; }
        }


        public void LoadSettings() { _settings = DataManager.LoadSettings(); }
        private void OnSettingsUpdated() { _settings = DataManager.LoadSettings(); }
        private void SaveSettings() { DataManager.SaveSettingsAndNotify(Settings); }

        private void LoadTasks()
        {
            if (!File.Exists(_tasksFilePath)) return;
            var json = File.ReadAllText(_tasksFilePath);
            var loadedTasks = JsonSerializer.Deserialize<ObservableCollection<TaskItem>>(json) ?? new ObservableCollection<TaskItem>();
            TaskItems.Clear();
            foreach (var task in loadedTasks)
            {
                TaskItems.Add(task);
            }
        }
        private void SaveTasks()
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(TaskItems, options);
            File.WriteAllText(_tasksFilePath, json);
        }

        private void LoadTodos()
        {
            if (!File.Exists(_todosFilePath)) return;
            var json = File.ReadAllText(_todosFilePath);
            var loadedTodos = JsonSerializer.Deserialize<ObservableCollection<TodoItem>>(json) ?? new ObservableCollection<TodoItem>();
            TodoItems.Clear();
            foreach (var todo in loadedTodos)
            {
                TodoItems.Add(todo);
            }
            FilterTodos();
        }
        private void SaveTodos()
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(TodoItems, options);
            File.WriteAllText(_todosFilePath, json);
        }

        private void LoadTimeLogs()
        {
            if (!File.Exists(_timeLogFilePath)) return;
            var json = File.ReadAllText(_timeLogFilePath);
            var loadedLogs = JsonSerializer.Deserialize<ObservableCollection<TimeLogEntry>>(json) ?? new ObservableCollection<TimeLogEntry>();
            TimeLogEntries.Clear();
            foreach (var log in loadedLogs)
            {
                TimeLogEntries.Add(log);
            }
        }
        private void SaveTimeLogs()
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(TimeLogEntries, options);
            File.WriteAllText(_timeLogFilePath, json);
        }
        #endregion

        #region UI 이벤트 핸들러
        private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            RecalculateAllTotals();
            RenderTimeTable();
            TodoDatePicker.SelectedDate = DateTime.Today;
            LoadAllData();
            UpdateCharacterInfo();
            LoadSoundSettings();
            _soundManager.PlayAll();
        }

        private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
        {
            SaveSoundSettings();
            _soundManager?.StopAll();
        }

        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                var parent = (UIElement)((Control)sender).Parent;
                parent.RaiseEvent(eventArg);
            }
        }

        private void TodoTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                var treeViewItem = FindVisualParent<TreeViewItem>(source);
                if (treeViewItem != null)
                {
                    treeViewItem.Focus();
                }
            }
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            var parent = parentObject as T;
            return parent ?? FindVisualParent<T>(parentObject);
        }

        private void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            string newTaskText = TaskInput.Text.Trim();
            if (!string.IsNullOrWhiteSpace(newTaskText))
            {
                if (TaskItems.Any(t => t.Text.Equals(newTaskText, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("이미 존재하는 과목입니다.", "중복 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var newTask = new TaskItem { Text = newTaskText };
                TaskItems.Add(newTask);

                var colorPicker = new ColorPickerWindow
                {
                    Owner = Window.GetWindow(this)
                };
                if (colorPicker.ShowDialog() == true)
                {
                    _settings.TaskColors[newTask.Text] = colorPicker.SelectedColor.ToString();
                    SaveSettings();
                }

                TaskInput.Clear();
                SaveTasks();
            }
        }

        private void EditTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskListBox.SelectedItem is TaskItem selectedTask)
            {
                var inputWindow = new InputWindow("과목 이름 수정", selectedTask.Text)
                {
                    Owner = Window.GetWindow(this)
                };

                if (inputWindow.ShowDialog() == true)
                {
                    string newName = inputWindow.ResponseText.Trim();
                    string oldName = selectedTask.Text;
                    if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;
                    if (TaskItems.Any(t => t.Text.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show("이미 존재하는 과목 이름입니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    selectedTask.Text = newName;
                    foreach (var log in TimeLogEntries.Where(l => l.TaskText == oldName)) log.TaskText = newName;
                    if (_settings.TaskColors.ContainsKey(oldName))
                    {
                        var color = _settings.TaskColors[oldName];
                        _settings.TaskColors.Remove(oldName);
                        _settings.TaskColors[newName] = color;
                    }
                    SaveTasks();
                    SaveTimeLogs();
                    SaveSettings();
                    TaskListBox.Items.Refresh();
                    RenderTimeTable();
                }
            }
            else
            {
                MessageBox.Show("수정할 과목을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskListBox.SelectedItem is TaskItem selectedTask)
            {
                if (_settings.TaskColors.ContainsKey(selectedTask.Text))
                {
                    _settings.TaskColors.Remove(selectedTask.Text);
                    SaveSettings();
                }
                TaskItems.Remove(selectedTask);
                SaveTasks();
            }
        }

        private void TaskListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedTask = TaskListBox.SelectedItem as TaskItem;
            if (_currentWorkingTask != selectedTask)
            {
                SessionReviewPanel.Visibility = Visibility.Collapsed;
                if (_stopwatch.IsRunning)
                {
                    LogWorkSession(); _stopwatch.Reset();
                }
                _currentWorkingTask = selectedTask;
                UpdateSelectedTaskTotalTimeDisplay();
                if (_currentWorkingTask != null)
                {
                    CurrentTaskDisplay.Text = $"현재 과목: {_currentWorkingTask.Text}";
                    CurrentTaskDisplayDashboard.Text = $"현재 작업: {_currentWorkingTask.Text}";
                }
                else
                {
                    CurrentTaskDisplay.Text = "선택된 과목 없음";
                    CurrentTaskDisplayDashboard.Text = "현재 작업: 없음";
                }
            }
        }

        private void AddTodoButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TodoInput.Text)) return;
            var newTodo = new TodoItem
            {
                Text = TodoInput.Text,
                Date = TodoDatePicker.SelectedDate?.Date ?? DateTime.Today
            };
            TodoItems.Add(newTodo);
            _lastAddedTodo = newTodo;
            UpdateTagSuggestions(_lastAddedTodo);
            TodoInput.Clear();
            SaveTodos();
            FilterTodos();
        }

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
                    SoundPlayer.PlayCompleteSound();
                }
            }
            SaveTodos();
        }

        private void EditTodoDateButton_Click(object sender, RoutedEventArgs e)
        {
            if (TodoTreeView.SelectedItem is TodoItem selectedTodo)
            {
                var dateEditWindow = new DateEditWindow(selectedTodo.Date)
                {
                    Owner = Window.GetWindow(this)
                };

                if (dateEditWindow.ShowDialog() == true)
                {
                    selectedTodo.Date = dateEditWindow.SelectedDate;
                    SaveTodos();
                    FilterTodos();
                }
            }
            else
            {
                MessageBox.Show("날짜를 수정할 할 일을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteTodoButton_Click(object sender, RoutedEventArgs e)
        {
            if (TodoTreeView.SelectedItem is TodoItem selectedTodo)
            {
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
                FilterTodos();
            }
            else
            {
                MessageBox.Show("삭제할 할 일을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SuggestedTag_Click(object sender, RoutedEventArgs e) { if (_lastAddedTodo != null && sender is Button button) { string tagToAdd = button.Content.ToString(); if (!_lastAddedTodo.Tags.Contains(tagToAdd)) { _lastAddedTodo.Tags.Add(tagToAdd); SuggestedTags.Remove(tagToAdd); SaveTodos(); } } }

        private void AddManualLogButton_Click(object sender, RoutedEventArgs e) { var win = new AddLogWindow(TaskItems) { Owner = Window.GetWindow(this) }; if (win.ShowDialog() == true) { if (win.NewLogEntry != null) TimeLogEntries.Add(win.NewLogEntry); SaveTimeLogs(); RecalculateAllTotals(); RenderTimeTable(); } }

        private void MemoButton_Click(object sender, RoutedEventArgs e) { if (_memoWindow == null || !_memoWindow.IsVisible) { _memoWindow = new MemoWindow { Owner = Window.GetWindow(this) }; _memoWindow.Show(); } else { _memoWindow.Activate(); } }

        private void TimeLogRect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if ((sender as FrameworkElement)?.Tag is TimeLogEntry log) { var win = new AddLogWindow(TaskItems, log) { Owner = Window.GetWindow(this) }; if (win.ShowDialog() == true) { if (win.IsDeleted) TimeLogEntries.Remove(log); else { log.StartTime = win.NewLogEntry.StartTime; log.EndTime = win.NewLogEntry.EndTime; log.TaskText = win.NewLogEntry.TaskText; log.FocusScore = win.NewLogEntry.FocusScore; } SaveTimeLogs(); RecalculateAllTotals(); RenderTimeTable(); } } }

        private void TodoDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e) { FilterTodos(); }
        private void TodoPrevDayButton_Click(object sender, RoutedEventArgs e) { if (TodoDatePicker.SelectedDate.HasValue) { TodoDatePicker.SelectedDate = TodoDatePicker.SelectedDate.Value.AddDays(-1); } }
        private void TodoNextDayButton_Click(object sender, RoutedEventArgs e) { if (TodoDatePicker.SelectedDate.HasValue) { TodoDatePicker.SelectedDate = TodoDatePicker.SelectedDate.Value.AddDays(1); } }

        private void SoundSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_soundManager == null || sender == null) return;

            var slider = (Slider)sender;
            string soundName = slider.Tag.ToString();
            _soundManager.SetVolume(soundName, slider.Value);

            // Check if all sliders are at 0 to update the mute button
            CheckMuteState();
        }

        private void MuteAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isMuted) // Unmute
            {
                // Restore previous volumes if they exist
                if (_lastVolumes.Any())
                {
                    foreach (var entry in _lastVolumes)
                    {
                        _soundManager.SetVolume(entry.Key, entry.Value);
                        GetSliderByName(entry.Key).Value = entry.Value;
                    }
                }
                else // If no previous volumes, set to a default value (e.g., 50%)
                {
                    WaveSlider.Value = 0.5;
                    ForestSlider.Value = 0.5;
                    RainSlider.Value = 0.5;
                    CampfireSlider.Value = 0.5;
                }
                MuteAllButton.Content = "모두 끄기";
                _isMuted = false;
            }
            else // Mute
            {
                // Store current volumes
                _lastVolumes["Wave"] = WaveSlider.Value;
                _lastVolumes["Forest"] = ForestSlider.Value;
                _lastVolumes["Rain"] = RainSlider.Value;
                _lastVolumes["Campfire"] = CampfireSlider.Value;

                // Set all volumes to 0
                WaveSlider.Value = 0;
                ForestSlider.Value = 0;
                RainSlider.Value = 0;
                CampfireSlider.Value = 0;

                MuteAllButton.Content = "모두 켜기";
                _isMuted = true;
            }
        }

        private void CustomizeButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.NavigateToAvatarCustomization();
            }
        }

        #endregion

        #region Core Logic
        public void SetMiniTimerReference(MiniTimerWindow miniTimer)
        {
            _miniTimer = miniTimer;
            if (_miniTimer != null)
            {
                _miniTimer.UpdateTask(_currentWorkingTask?.Text ?? "과목 선택 안됨");
                _miniTimer.UpdateTime(_stopwatch.Elapsed);
            }
        }

        private void UpdateCoinDisplay()
        {
            // This method might be called from a non-UI thread if settings are updated globally
            Dispatcher.Invoke(() =>
            {
                CoinsTextBlock.Text = $"💰 {_settings.Coins}";
            });
        }
        private void Timer_Tick(object sender, EventArgs e)
        {
            // ... (rest of Timer_Tick method)

            ActiveWindowHelper.ActiveAppInfo activeApp = ActiveWindowHelper.GetActiveAppInfo();
            string activeProcessName = activeApp.ProcessName;
            ActiveProcessDisplay.Text = $"활성 프로그램: {activeProcessName}";

            if (_currentWorkingTask != null)
            {
                bool isWorkProcess = _settings.WorkProcesses.Contains(activeProcessName);
                bool isDistractionProcess = _settings.DistractionProcesses.Contains(activeProcessName);
                bool isPassiveProcess = _settings.PassiveProcesses.Contains(activeProcessName);

                if (isWorkProcess || (!isDistractionProcess && !isPassiveProcess))
                {
                    if (!_stopwatch.IsRunning)
                    {
                        _stopwatch.Start();
                        if (_sessionStartTime == DateTime.MinValue) _sessionStartTime = DateTime.Now;
                    }
                }
                else
                {
                    if (_stopwatch.IsRunning) _stopwatch.Stop();
                }

                if (isDistractionProcess && _isFocusModeActive)
                {
                    if ((DateTime.Now - _lastNagTime).TotalSeconds > _settings.FocusModeNagIntervalSeconds)
                    {
                        var alert = new AlertWindow(_settings.FocusModeNagMessage)
                        {
                            Owner = Window.GetWindow(this),
                            Topmost = true
                        };
                        alert.Show();
                        _lastNagTime = DateTime.Now;
                    }
                }
            }
            else
            {
                if (_stopwatch.IsRunning) _stopwatch.Stop();
            }

            MainTimeDisplay.Text = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
            if (_miniTimer != null) _miniTimer.UpdateTime(_stopwatch.Elapsed);

            RecalculateAllTotals();
            // ... (rest of Timer_Tick method)

        }

        private void LogWorkSession()
        {
            if (_currentWorkingTask != null && _stopwatch.Elapsed.TotalSeconds > 10) // 10초 이상 작업한 경우만 기록
            {
                var endTime = DateTime.Now;
                var newLog = new TimeLogEntry
                {
                    StartTime = _sessionStartTime,
                    EndTime = endTime,
                    TaskText = _currentWorkingTask.Text
                };
                TimeLogEntries.Add(newLog);
                _lastUnratedSession = newLog;
                SaveTimeLogs();
                _settings.Coins += (int)(_stopwatch.Elapsed.TotalMinutes); // 1분에 1코인
                SaveSettings();
                UpdateCoinDisplay();
                ShowSessionReviewPanel();
            }

            // 초기화
            _sessionStartTime = DateTime.MinValue;
            if (_stopwatch.IsRunning) _stopwatch.Restart();
            else _stopwatch.Reset();

            RecalculateAllTotals();
            RenderTimeTable();
        }

        private void RecalculateAllTotals()
        {
            var todayLogs = TimeLogEntries.Where(log => log.StartTime.Date == DateTime.Today);
            _totalTimeTodayFromLogs = new TimeSpan(todayLogs.Sum(log => (log.EndTime - log.StartTime).Ticks));

            UpdateSelectedTaskTotalTimeDisplay();
        }

        private void UpdateSelectedTaskTotalTimeDisplay()
        {
            TimeSpan totalTimeForSelectedTask = TimeSpan.Zero;
            if (_currentWorkingTask != null)
            {
                var taskLogs = TimeLogEntries.Where(log => log.TaskText == _currentWorkingTask.Text && log.StartTime.Date == DateTime.Today);
                totalTimeForSelectedTask = new TimeSpan(taskLogs.Sum(log => (log.EndTime - log.StartTime).Ticks));
            }
            SelectedTaskTotalTimeDisplay.Text = $"선택 과목 총계: {totalTimeForSelectedTask:hh\\:mm\\:ss}";
        }

        private void RenderTimeTable()
        {
            TimeTableContainer.Children.Clear();
            var logsForDate = TimeLogEntries.Where(l => l.StartTime.Date == _currentDateForTimeline).OrderBy(l => l.StartTime).ToList();

            for (int hour = 0; hour < 24; hour++)
            {
                var hourBlock = new Border
                {
                    Height = 60,
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0, 0, 0, 1)
                };
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                var label = new TextBlock
                {
                    Text = $"{hour:00}:00",
                    Width = 50,
                    VerticalAlignment = VerticalAlignment.Top,
                    Foreground = Brushes.Gray
                };
                var canvas = new Canvas();
                sp.Children.Add(label);
                sp.Children.Add(canvas);
                hourBlock.Child = sp;
                TimeTableContainer.Children.Add(hourBlock);

                var logsInHour = logsForDate.Where(l => l.StartTime.Hour == hour || l.EndTime.Hour == hour).ToList();
                foreach (var log in logsInHour)
                {
                    double top = (log.StartTime.Minute * 60 + log.StartTime.Second) / 60.0;
                    double height = Math.Max(1, (log.EndTime - log.StartTime).TotalMinutes);

                    if (log.StartTime.Hour != hour)
                    {
                        top = 0;
                        height = (log.EndTime - new DateTime(log.EndTime.Year, log.EndTime.Month, log.EndTime.Day, hour, 0, 0)).TotalMinutes;
                    }

                    if (log.EndTime.Hour > hour)
                    {
                        height = 60 - top;
                    }

                    var rect = new Rectangle
                    {
                        Width = TimeTableContainer.ActualWidth - 60, // Adjust for label width
                        Height = height,
                        Fill = GetColorForTask(log.TaskText),
                        ToolTip = $"{log.TaskText}\n{log.StartTime:HH:mm} - {log.EndTime:HH:mm}\n집중도: {log.FocusScore}",
                        Tag = log
                    };
                    rect.MouseLeftButtonDown += TimeLogRect_MouseLeftButtonDown;
                    Canvas.SetLeft(rect, 0);
                    Canvas.SetTop(rect, top);
                    canvas.Children.Add(rect);
                }
            }
        }

        private void PrevDayButton_Click(object sender, RoutedEventArgs e) { _currentDateForTimeline = _currentDateForTimeline.AddDays(-1); TimelineDatePicker.SelectedDate = _currentDateForTimeline; RenderTimeTable(); }
        private void TodayButton_Click(object sender, RoutedEventArgs e) { _currentDateForTimeline = DateTime.Today; TimelineDatePicker.SelectedDate = _currentDateForTimeline; RenderTimeTable(); }
        private void NextDayButton_Click(object sender, RoutedEventArgs e) { _currentDateForTimeline = _currentDateForTimeline.AddDays(1); TimelineDatePicker.SelectedDate = _currentDateForTimeline; RenderTimeTable(); }
        private void TimelineDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e) { if (TimelineDatePicker.SelectedDate.HasValue) { _currentDateForTimeline = TimelineDatePicker.SelectedDate.Value; RenderTimeTable(); } }

        private void BulkEditButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedLogs = GetSelectedLogs();
            if (!selectedLogs.Any())
            {
                MessageBox.Show("먼저 타임라인에서 수정할 기록을 드래그하여 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var bulkEditWindow = new BulkEditLogsWindow(TaskItems.Select(t => t.Text).ToList())
            {
                Owner = Window.GetWindow(this)
            };

            if (bulkEditWindow.ShowDialog() == true)
            {
                string newTag = bulkEditWindow.SelectedTag;
                bool shouldDelete = bulkEditWindow.ShouldDelete;

                foreach (var log in selectedLogs)
                {
                    if (shouldDelete)
                    {
                        TimeLogEntries.Remove(log);
                    }
                    else if (!string.IsNullOrEmpty(newTag))
                    {
                        log.TaskText = newTag;
                    }
                }

                SaveTimeLogs();
                RenderTimeTable();
            }
        }

        private List<TimeLogEntry> GetSelectedLogs()
        {
            List<TimeLogEntry> selected = new List<TimeLogEntry>();
            if (_selectionBox.Visibility != Visibility.Visible) return selected;

            Rect selectionRect = new Rect(Canvas.GetLeft(_selectionBox), Canvas.GetTop(_selectionBox), _selectionBox.Width, _selectionBox.Height);

            foreach (var border in TimeTableContainer.Children.OfType<Border>())
            {
                if (border.Child is StackPanel sp && sp.Children.Count > 1 && sp.Children[1] is Canvas canvas)
                {
                    foreach (var rect in canvas.Children.OfType<Rectangle>())
                    {
                        var rectBounds = new Rect(
                            Canvas.GetLeft(rect),
                            Canvas.GetTop(rect) + (TimeTableContainer.Children.IndexOf(border) * 60), // Adjust for hour block height
                            rect.Width,
                            rect.Height
                        );

                        // This intersection logic is tricky because the selection box is on a different canvas.
                        // A more robust implementation might need a shared coordinate system.
                        // For now, we'll use a simplified check.

                        // We need to transform the rect's position relative to the SelectionCanvas
                        GeneralTransform transform = rect.TransformToAncestor(SelectionCanvas);
                        Point relativePoint = transform.Transform(new Point(0, 0));
                        Rect rectInCanvasCoords = new Rect(relativePoint, new Size(rect.ActualWidth, rect.ActualHeight));

                        if (selectionRect.IntersectsWith(rectInCanvasCoords))
                        {
                            if (rect.Tag is TimeLogEntry log && !selected.Contains(log))
                            {
                                selected.Add(log);
                            }
                        }
                    }
                }
            }
            return selected;
        }

        private void SelectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(SelectionCanvas);
            _selectionBox.Width = 0;
            _selectionBox.Height = 0;
            _selectionBox.Visibility = Visibility.Visible;
            Canvas.SetLeft(_selectionBox, _dragStartPoint.X);
            Canvas.SetTop(_selectionBox, _dragStartPoint.Y);
            SelectionCanvas.CaptureMouse();
        }

        private void SelectionCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            Point currentPoint = e.GetPosition(SelectionCanvas);
            double x = Math.Min(currentPoint.X, _dragStartPoint.X);
            double y = Math.Min(currentPoint.Y, _dragStartPoint.Y);
            double width = Math.Abs(currentPoint.X - _dragStartPoint.X);
            double height = Math.Abs(currentPoint.Y - _dragStartPoint.Y);

            Canvas.SetLeft(_selectionBox, x);
            Canvas.SetTop(_selectionBox, y);
            _selectionBox.Width = width;
            _selectionBox.Height = height;
        }

        private void SelectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging) return;
            _isDragging = false;
            SelectionCanvas.ReleaseMouseCapture();

            // Optionally, hide the selection box after a delay or keep it visible
            // For now, let's keep it visible until the next drag starts
        }

        private void FocusModeButton_Click(object sender, RoutedEventArgs e) { _isFocusModeActive = !_isFocusModeActive; FocusModeButton.Background = _isFocusModeActive ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Color.FromRgb(0xEF, 0xEF, 0xEF)); }

        private void ShowSessionReviewPanel() { SessionReviewPanel.Visibility = Visibility.Visible; }
        private void RateSessionButton_Click(object sender, RoutedEventArgs e) { if (_lastUnratedSession != null) { _lastUnratedSession.FocusScore = int.Parse((sender as Button).Tag.ToString()); SaveTimeLogs(); RenderTimeTable(); } SessionReviewPanel.Visibility = Visibility.Collapsed; }

        private void FilterTodos()
        {
            FilteredTodoItems.Clear();
            if (TodoDatePicker.SelectedDate.HasValue)
            {
                var selectedDate = TodoDatePicker.SelectedDate.Value.Date;
                var todosForDate = TodoItems.Where(t => t.Date.Date == selectedDate);
                foreach (var todo in todosForDate)
                {
                    FilteredTodoItems.Add(todo);
                }
            }
        }

        private TodoItem FindParent(TodoItem parent, ObservableCollection<TodoItem> items, TodoItem child)
        {
            foreach (var item in items)
            {
                if (item == child) return parent;
                var foundParent = FindParent(item, item.SubTasks, child);
                if (foundParent != null) return foundParent;
            }
            return null;
        }

        private void AddSubTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (TodoTreeView.SelectedItem is TodoItem selectedTodo)
            {
                var inputWindow = new InputWindow("하위 항목 추가", "") { Owner = Window.GetWindow(this) };
                if (inputWindow.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputWindow.ResponseText))
                {
                    selectedTodo.SubTasks.Add(new TodoItem { Text = inputWindow.ResponseText, Date = selectedTodo.Date });
                    SaveTodos();
                }
            }
        }

        private void AddTagButton_Click(object sender, RoutedEventArgs e)
        {
            if (TodoTreeView.SelectedItem is TodoItem selectedTodo)
            {
                var inputWindow = new InputWindow("태그 추가", "") { Owner = Window.GetWindow(this) };
                if (inputWindow.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputWindow.ResponseText))
                {
                    selectedTodo.Tags.Add(inputWindow.ResponseText);
                    SaveTodos();
                }
            }
        }
        private void TodoInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddTodoButton_Click(sender, e); }
        private void TaskInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddTaskButton_Click(sender, e); }
        private void TodoTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SaveTodos(); }

        private void UpdateTagSuggestions(TodoItem todo)
        {
            // Simple suggestion logic: suggest tags from other todos with similar words
            SuggestedTags.Clear();
            var todoWords = todo.Text.Split(' ').Where(w => w.Length > 2).ToList();
            var relevantTags = TodoItems
                .Where(t => t != todo && todoWords.Any(w => t.Text.Contains(w, StringComparison.OrdinalIgnoreCase)))
                .SelectMany(t => t.Tags)
                .Distinct()
                .Except(todo.Tags)
                .Take(5);

            foreach (var tag in relevantTags)
            {
                SuggestedTags.Add(tag);
            }
        }

        private void LoadSoundSettings()
        {
            if (_settings.SoundVolumes != null && _soundManager != null)
            {
                WaveSlider.Value = _settings.SoundVolumes.ContainsKey("Wave") ? _settings.SoundVolumes["Wave"] : 0;
                ForestSlider.Value = _settings.SoundVolumes.ContainsKey("Forest") ? _settings.SoundVolumes["Forest"] : 0;
                RainSlider.Value = _settings.SoundVolumes.ContainsKey("Rain") ? _settings.SoundVolumes["Rain"] : 0;
                CampfireSlider.Value = _settings.SoundVolumes.ContainsKey("Campfire") ? _settings.SoundVolumes["Campfire"] : 0;

                // Immediately apply the loaded volumes
                _soundManager.SetAllVolumes(_settings.SoundVolumes);
            }
            CheckMuteState();
        }

        public void SaveSoundSettings()
        {
            if (Settings == null) return;
            if (Settings.SoundVolumes == null)
            {
                Settings.SoundVolumes = new Dictionary<string, double>();
            }
            Settings.SoundVolumes["Wave"] = WaveSlider.Value;
            Settings.SoundVolumes["Forest"] = ForestSlider.Value;
            Settings.SoundVolumes["Rain"] = RainSlider.Value;
            Settings.SoundVolumes["Campfire"] = CampfireSlider.Value;
            DataManager.SaveSettings(Settings); // Use the non-notifying version on unload
        }

        private void UpdateCharacterInfo()
        {
            if (_settings != null)
            {
                UsernameTextBlock.Text = _settings.Username ?? "User";
                LevelTextBlock.Text = $"Level: {_settings.Level}";
                CoinsTextBlock.Text = $"💰 {_settings.Coins}";
                DashboardCharacterDisplay.UpdateCharacter(); // Refresh the character visual
            }
        }

        private Slider GetSliderByName(string name)
        {
            switch (name)
            {
                case "Wave": return WaveSlider;
                case "Forest": return ForestSlider;
                case "Rain": return RainSlider;
                case "Campfire": return CampfireSlider;
                default: return null;
            }
        }

        private void CheckMuteState()
        {
            if (WaveSlider.Value == 0 && ForestSlider.Value == 0 && RainSlider.Value == 0 && CampfireSlider.Value == 0)
            {
                MuteAllButton.Content = "소리 켜기";
                _isMuted = true;
            }
            else
            {
                MuteAllButton.Content = "모두 끄기";
                _isMuted = false;
            }
        }
        #endregion
    }
}

