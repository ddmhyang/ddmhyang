// 파일: SettingsPage.xaml.cs

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading; // 타이머를 위해 추가
using Microsoft.Win32;
using System.Windows.Controls.Primitives; //  <-- 이 줄을 추가하세요!
using System.Windows.Media;


namespace WorkPartner
{
    public partial class SettingsPage : UserControl
    {
        private readonly string _settingsFilePath = DataManager.SettingsFilePath;
        public AppSettings Settings { get; set; }
        private List<InstalledProgram> _allPrograms;
        private string _targetProcessList; // 3초 타이머가 어느 목록에 추가할지 기억하기 위한 변수

        public SettingsPage()
        {
            InitializeComponent();
            LoadSettings();
            UpdateUIFromSettings();

            // 프로그램 목록을 백그라운드에서 불러옵니다.
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (s, e) => { _allPrograms = GetAllPrograms(); };
            worker.RunWorkerCompleted += (s, e) => { };
            worker.RunWorkerAsync();
            LoadTaskColors();

        }

        // SettingsPage.xaml.cs 파일의 SettingsPage 클래스 내부에 아래 코드를 추가하세요.
        // public SettingsPage() 생성자 바로 아래에 추가하면 좋습니다.

        #region 과목별 색상 설정

        private void LoadTaskColors()
        {
            // Settings 속성을 사용하여 TaskColors를 가져옵니다.
            // Dictionary를 ListBox에 바인딩하기 쉽게 KeyValuePair의 리스트로 변환합니다.
            if (Settings != null && Settings.TaskColors != null)
            {
                TaskColorsListBox.ItemsSource = Settings.TaskColors.ToList();
            }
        }

        private void AddTaskColorButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 다음 단계에서 InputWindow를 사용하여 사용자 입력을 받도록 수정합니다.
            // 현재는 테스트를 위해 고정된 값을 사용합니다.
            var inputWindow = new InputWindow("새 과목 추가", "과목 이름을 입력하세요:");
            if (inputWindow.ShowDialog() == true)
            {
                string newTask = inputWindow.ResponseText;
                if (string.IsNullOrWhiteSpace(newTask))
                {
                    MessageBox.Show("과목 이름은 비워둘 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 색상은 임시로 랜덤 생성 (나중에 색상 선택 기능 추가 가능)
                Random r = new Random();
                string newColor = $"#{r.Next(0x1000000):X6}";

                if (!Settings.TaskColors.ContainsKey(newTask))
                {
                    Settings.TaskColors.Add(newTask, newColor);
                    DataManager.SaveSettings(this.Settings); // 올바른 Settings 속성을 저장
                    LoadTaskColors(); // 목록 새로고침
                }
                else
                {
                    MessageBox.Show("이미 동일한 이름의 과목이 존재합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void RemoveTaskColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (TaskColorsListBox.SelectedItem is KeyValuePair<string, string> selectedItem)
            {
                if (MessageBox.Show($"'{selectedItem.Key}' 색상 설정을 삭제하시겠습니까?", "확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Settings.TaskColors.Remove(selectedItem.Key);
                    DataManager.SaveSettings(this.Settings); // 올바른 Settings 속성을 저장
                    LoadTaskColors(); // 목록 새로고침
                }
            }
            else
            {
                MessageBox.Show("삭제할 항목을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region 데이터 로드 및 저장
        private void LoadSettings()
        {
            Settings = DataManager.LoadSettings();
        }

        private void SaveSettings()
        {
            DataManager.SaveSettings(Settings);
        }

        private void UpdateUIFromSettings()
        {
            IdleDetectionCheckBox.IsChecked = Settings.IsIdleDetectionEnabled;
            IdleTimeoutTextBox.Text = Settings.IdleTimeoutSeconds.ToString();
            MiniTimerCheckBox.IsChecked = Settings.IsMiniTimerEnabled;
            WorkProcessListBox.ItemsSource = Settings.WorkProcesses;
            PassiveProcessListBox.ItemsSource = Settings.PassiveProcesses;
            DistractionProcessListBox.ItemsSource = Settings.DistractionProcesses;
            NagMessageTextBox.Text = Settings.FocusModeNagMessage;
            NagIntervalTextBox.Text = Settings.FocusModeNagIntervalSeconds.ToString();
            TagRulesListView.ItemsSource = Settings.TagRules;
        }
        #endregion

        #region 프로그램/아이콘 관련 로직
        private List<InstalledProgram> GetAllPrograms()
        {
            var programs = new Dictionary<string, InstalledProgram>();
            string registryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            var registryViews = new[] { RegistryView.Registry32, RegistryView.Registry64 };

            foreach (var view in registryViews)
            {
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (var key = baseKey.OpenSubKey(registryPath))
                    {
                        if (key == null) continue;
                        foreach (string subkeyName in key.GetSubKeyNames())
                        {
                            using (RegistryKey subkey = key.OpenSubKey(subkeyName))
                            {
                                if (subkey == null) continue;
                                var displayName = subkey.GetValue("DisplayName") as string;
                                var iconPath = subkey.GetValue("DisplayIcon") as string;
                                var systemComponent = subkey.GetValue("SystemComponent") as int?;

                                if (!string.IsNullOrWhiteSpace(displayName) && systemComponent != 1)
                                {
                                    string executablePath = GetExecutablePathFromIconPath(iconPath);
                                    if (string.IsNullOrEmpty(executablePath)) continue;
                                    string processName = Path.GetFileNameWithoutExtension(executablePath).ToLower();
                                    if (!programs.ContainsKey(processName))
                                    {
                                        programs[processName] = new InstalledProgram { DisplayName = displayName, ProcessName = processName, IconPath = executablePath };
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var program in programs.Values)
                {
                    program.Icon = GetIcon(program.IconPath);
                }
            });

            return programs.Values.OrderBy(p => p.DisplayName).ToList();
        }

        private string GetExecutablePathFromIconPath(string iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath)) return null;
            string executablePath = iconPath.Split(',')[0].Replace("\"", "");
            if (File.Exists(executablePath)) return executablePath;
            return null;
        }

        private BitmapSource GetIcon(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;
            try
            {
                using (Icon icon = Icon.ExtractAssociatedIcon(filePath))
                {
                    return Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
            }
            catch { return null; }
        }
        #endregion

        #region UI 이벤트 핸들러
        private void SelectRunningAppButton_Click(object sender, RoutedEventArgs e)
        {
            var allRunningApps = new List<InstalledProgram>();
            var addedProcesses = new HashSet<string>();

            var runningProcesses = Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) && p.MainWindowHandle != IntPtr.Zero);
            foreach (var process in runningProcesses)
            {
                try
                {
                    string processName = process.ProcessName.ToLower();
                    if (addedProcesses.Contains(processName) || new[] { "chrome", "msedge", "whale" }.Contains(processName)) continue;

                    allRunningApps.Add(new InstalledProgram
                    {
                        DisplayName = process.MainWindowTitle,
                        ProcessName = processName,
                        Icon = GetIcon(process.MainModule.FileName)
                    });
                    addedProcesses.Add(processName);
                }
                catch { }
            }

            var sortedApps = allRunningApps.OrderBy(p => p.DisplayName).ToList();
            if (!sortedApps.Any())
            {
                MessageBox.Show("목록에 표시할 실행 중인 프로그램이 없습니다.");
                return;
            }

            var selectionWindow = new AppSelectionWindow(sortedApps) { Owner = Window.GetWindow(this) };
            if (selectionWindow.ShowDialog() == true)
            {
                string selectedKeyword = selectionWindow.SelectedAppKeyword;
                if (string.IsNullOrEmpty(selectedKeyword)) return;

                string targetList = (sender as Button)?.Tag as string;
                if (targetList == "Work") WorkProcessInputTextBox.Text = selectedKeyword;
                else if (targetList == "Passive") PassiveProcessInputTextBox.Text = selectedKeyword;
                else if (targetList == "Distraction") DistractionProcessInputTextBox.Text = selectedKeyword;
            }
        }

        private void AddActiveTabButton_Click(object sender, RoutedEventArgs e)
        {
            _targetProcessList = (sender as Button)?.Tag as string;

            // [수정] 안내 메시지 박스를 제거했습니다.
            // MessageBox.Show("3초 안에 추가하고 싶은 브라우저 탭을 클릭하여 활성화하세요.", "알림");

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                string activeUrl = ActiveWindowHelper.GetActiveBrowserTabUrl();

                if (string.IsNullOrEmpty(activeUrl))
                {
                    // 실패한 경우에도 팝업을 띄우지 않습니다.
                    return;
                }

                string urlKeyword;
                try
                {
                    urlKeyword = new Uri(activeUrl).Host.ToLower();
                }
                catch
                {
                    return;
                }

                bool added = false;
                if (_targetProcessList == "Work")
                {
                    if (!Settings.WorkProcesses.Contains(urlKeyword))
                    {
                        Settings.WorkProcesses.Add(urlKeyword);
                        WorkProcessListBox.ItemsSource = null;
                        WorkProcessListBox.ItemsSource = Settings.WorkProcesses;
                        added = true;
                    }
                }
                else if (_targetProcessList == "Passive")
                {
                    if (!Settings.PassiveProcesses.Contains(urlKeyword))
                    {
                        Settings.PassiveProcesses.Add(urlKeyword);
                        PassiveProcessListBox.ItemsSource = null;
                        PassiveProcessListBox.ItemsSource = Settings.PassiveProcesses;
                        added = true;
                    }
                }
                else if (_targetProcessList == "Distraction")
                {
                    if (!Settings.DistractionProcesses.Contains(urlKeyword))
                    {
                        Settings.DistractionProcesses.Add(urlKeyword);
                        DistractionProcessListBox.ItemsSource = null;
                        DistractionProcessListBox.ItemsSource = Settings.DistractionProcesses;
                        added = true;
                    }
                }

                if (added)
                {
                    DataManager.SaveSettingsAndNotify(Settings);
                }
            };
            timer.Start();
        }

        private void AddWorkProcessButton_Click(object sender, RoutedEventArgs e)
        {
            var newProcess = WorkProcessInputTextBox.Text.Trim().ToLower();
            if (!string.IsNullOrEmpty(newProcess) && !Settings.WorkProcesses.Contains(newProcess))
            {
                Settings.WorkProcesses.Add(newProcess);
                WorkProcessInputTextBox.Clear();
                DataManager.SaveSettingsAndNotify(Settings);
                WorkProcessListBox.ItemsSource = null;
                WorkProcessListBox.ItemsSource = Settings.WorkProcesses;
            }
        }

        private void AddPassiveProcessButton_Click(object sender, RoutedEventArgs e)
        {
            var newProcess = PassiveProcessInputTextBox.Text.Trim().ToLower();
            if (!string.IsNullOrEmpty(newProcess) && !Settings.PassiveProcesses.Contains(newProcess))
            {
                Settings.PassiveProcesses.Add(newProcess);
                PassiveProcessInputTextBox.Clear();
                SaveSettings();
                PassiveProcessListBox.ItemsSource = null;
                PassiveProcessListBox.ItemsSource = Settings.PassiveProcesses;
            }
        }

        private void AddDistractionProcessButton_Click(object sender, RoutedEventArgs e)
        {
            var newProcess = DistractionProcessInputTextBox.Text.Trim().ToLower();
            if (!string.IsNullOrEmpty(newProcess) && !Settings.DistractionProcesses.Contains(newProcess))
            {
                Settings.DistractionProcesses.Add(newProcess);
                DistractionProcessInputTextBox.Clear();
                SaveSettings();
                DistractionProcessListBox.ItemsSource = null;
                DistractionProcessListBox.ItemsSource = Settings.DistractionProcesses;
            }
        }

        private void DeleteWorkProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (WorkProcessListBox.SelectedItem is string selected)
            {
                Settings.WorkProcesses.Remove(selected);
                DataManager.SaveSettingsAndNotify(Settings);
                WorkProcessListBox.ItemsSource = null;
                WorkProcessListBox.ItemsSource = Settings.WorkProcesses;
            }
        }

        private void DeletePassiveProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (PassiveProcessListBox.SelectedItem is string selected)
            {
                Settings.PassiveProcesses.Remove(selected);
                SaveSettings();
                PassiveProcessListBox.ItemsSource = null;
                PassiveProcessListBox.ItemsSource = Settings.PassiveProcesses;
            }
        }

        private void DeleteDistractionProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (DistractionProcessListBox.SelectedItem is string selected)
            {
                Settings.DistractionProcesses.Remove(selected);
                SaveSettings();
                DistractionProcessListBox.ItemsSource = null;
                DistractionProcessListBox.ItemsSource = Settings.DistractionProcesses;
            }
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (Settings == null) return;
            if (sender == IdleDetectionCheckBox)
            {
                Settings.IsIdleDetectionEnabled = IdleDetectionCheckBox.IsChecked ?? true;
            }
            else if (sender == MiniTimerCheckBox)
            {
                Settings.IsMiniTimerEnabled = MiniTimerCheckBox.IsChecked ?? false;
                (Application.Current.MainWindow as MainWindow)?.ToggleMiniTimer();
            }
            DataManager.SaveSettingsAndNotify(Settings);
        }

        private void Setting_Changed_IdleTimeout(object sender, TextChangedEventArgs e)
        {
            if (Settings != null && int.TryParse(IdleTimeoutTextBox.Text, out int timeout))
            {
                Settings.IdleTimeoutSeconds = timeout;
                SaveSettings();
            }
        }

        private void NagMessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Settings != null)
            {
                Settings.FocusModeNagMessage = NagMessageTextBox.Text;
                SaveSettings();
            }
        }

        private void NagIntervalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Settings != null && int.TryParse(NagIntervalTextBox.Text, out int interval) && interval > 0)
            {
                Settings.FocusModeNagIntervalSeconds = interval;
                SaveSettings();
            }
        }

        private void AddTagRuleButton_Click(object sender, RoutedEventArgs e)
        {
            string keyword = KeywordInput.Text.Trim();
            string tag = TagInput.Text.Trim();
            if (string.IsNullOrEmpty(keyword) || string.IsNullOrEmpty(tag))
            {
                MessageBox.Show("키워드와 태그를 모두 입력해주세요.");
                return;
            }

            if (!tag.StartsWith("#")) tag = "#" + tag;
            if (!Settings.TagRules.ContainsKey(keyword))
            {
                Settings.TagRules[keyword] = tag;
                TagRulesListView.ItemsSource = null;
                TagRulesListView.ItemsSource = Settings.TagRules;
                SaveSettings();
                KeywordInput.Clear();
                TagInput.Clear();
            }
            else
            {
                MessageBox.Show("이미 존재하는 키워드입니다.");
            }
        }

        private void DeleteTagRuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (TagRulesListView.SelectedItem is KeyValuePair<string, string> selectedRule)
            {
                if (MessageBox.Show($"'{selectedRule.Key}' -> '{selectedRule.Value}' 규칙을 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Settings.TagRules.Remove(selectedRule.Key);
                    TagRulesListView.ItemsSource = null;
                    TagRulesListView.ItemsSource = Settings.TagRules;
                    SaveSettings();
                }
            }
            else
            {
                MessageBox.Show("삭제할 규칙을 목록에서 선택해주세요.");
            }
        }

        private void ResetDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("정말로 모든 데이터를 영구적으로 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.", "데이터 초기화 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    var filesToDelete = new string[]
                    {
                        DataManager.SettingsFilePath,
                        DataManager.TimeLogFilePath,
                        DataManager.TasksFilePath,
                        DataManager.TodosFilePath,
                        DataManager.MemosFilePath,
                        DataManager.ModelFilePath
                    };

                    foreach (var filePath in filesToDelete)
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                    MessageBox.Show("모든 데이터가 성공적으로 초기화되었습니다.\n프로그램을 다시 시작해주세요.", "초기화 완료");
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"데이터 초기화 중 오류가 발생했습니다: {ex.Message}", "오류");
                }
            }
        }
        #endregion

        #region 자동완성 로직

        // 텍스트가 변경될 때 호출되는 공통 이벤트 핸들러
        private void AutoComplete_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            string tag = textBox.Tag.ToString();
            string searchText = textBox.Text.ToLower();

            Popup popup;
            ListBox suggestionBox;

            if (tag == "Work")
            {
                popup = WorkAutoCompletePopup;
                suggestionBox = WorkSuggestionListBox;
            }
            else if (tag == "Passive")
            {
                // TODO: Passive 용 Popup, ListBox를 할당하세요. (e.g., popup = PassiveAutoCompletePopup;)
                return; // 지금은 Work만 구현
            }
            else // Distraction
            {
                // TODO: Distraction 용 Popup, ListBox를 할당하세요.
                return; // 지금은 Work만 구현
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                popup.IsOpen = false;
                return;
            }

            // _allPrograms 리스트에서 프로그램을 검색합니다.
            if (_allPrograms != null)
            {
                var suggestions = _allPrograms
                    .Where(p => p.DisplayName.ToLower().Contains(searchText) || p.ProcessName.ToLower().Contains(searchText))
                    .OrderBy(p => p.DisplayName)
                    .ToList();

                if (suggestions.Any())
                {
                    suggestionBox.ItemsSource = suggestions;
                    popup.IsOpen = true;
                }
                else
                {
                    popup.IsOpen = false;
                }
            }
        }
        // 추천 목록에서 항목을 선택했을 때
        private void SuggestionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            var textBox = FindAssociatedTextBox(listBox.Tag.ToString());
            var popup = FindAssociatedPopup(listBox.Tag.ToString());

            if (listBox.SelectedItem is InstalledProgram selectedProgram)
            {
                // 변경 이벤트가 다시 발생하지 않도록 잠시 이벤트를 해제합니다.
                textBox.TextChanged -= AutoComplete_TextChanged;
                textBox.Text = selectedProgram.ProcessName;
                textBox.TextChanged += AutoComplete_TextChanged;

                popup.IsOpen = false;
            }
        }

        // 키보드 입력(Enter, 화살표 키 등)을 처리하기 위한 핸들러
        private void AutoComplete_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var fe = sender as FrameworkElement;
            if (fe == null) return;

            string tag = fe.Tag.ToString();
            var popup = FindAssociatedPopup(tag);
            var suggestionBox = FindAssociatedSuggestionBox(tag);

            if (popup.IsOpen)
            {
                if (e.Key == Key.Down)
                {
                    suggestionBox.Focus();
                    if (suggestionBox.Items.Count > 0)
                    {
                        suggestionBox.SelectedIndex = 0;
                    }
                }
                else if (e.Key == Key.Escape)
                {
                    popup.IsOpen = false;
                }
                else if (e.Key == Key.Enter && suggestionBox.IsFocused)
                {
                    SuggestionListBox_SelectionChanged(suggestionBox, null);
                }
            }
        }

        // 아래는 Tag를 이용해 관련된 컨트롤을 찾아주는 도우미 메서드들입니다.
        private TextBox FindAssociatedTextBox(string tag)
        {
            if (tag == "Work") return WorkProcessInputTextBox;
            if (tag == "Passive") return PassiveProcessInputTextBox;
            if (tag == "Distraction") return DistractionProcessInputTextBox;
            return null;
        }

        private Popup FindAssociatedPopup(string tag)
        {
            if (tag == "Work") return WorkAutoCompletePopup;
            if (tag == "Passive") return PassiveAutoCompletePopup; // XAML에 추가 후 연결
            if (tag == "Distraction") return DistractionAutoCompletePopup; // XAML에 추가 후 연결
            return null;
        }

        private ListBox FindAssociatedSuggestionBox(string tag)
        {
            if (tag == "Work") return WorkSuggestionListBox;
            if (tag == "Passive") return PassiveSuggestionListBox; // XAML에 추가 후 연결
            if (tag == "Distraction") return DistractionSuggestionListBox; // XAML에 추가 후 연결
            return null;
        }


        #endregion

        // 파일: SettingsPage.xaml.cs

        #region 스크롤 개선 로직 (수정)

        /// <summary>
        /// 컨트롤의 부모 요소 중에서 특정 타입(T)의 첫 번째 부모를 찾아 반환합니다.
        /// </summary>
        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T parent = parentObject as T;
            if (parent != null)
            {
                return parent;
            }
            else
            {
                return FindVisualParent<T>(parentObject);
            }
        }

        /// <summary>
        /// 자식 컨트롤의 마우스 휠 이벤트를 가로채서 부모 ScrollViewer를 한 줄씩 스크롤하도록 제어합니다.
        /// </summary>
        private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is UIElement element && !e.Handled)
            {
                // 현재 컨트롤의 부모 중에서 ScrollViewer를 찾습니다.
                var scrollViewer = FindVisualParent<ScrollViewer>(element);
                if (scrollViewer != null)
                {
                    // 스크롤 방향에 따라 ScrollViewer를 한 줄씩 부드럽게 제어합니다.
                    if (e.Delta < 0) // 휠을 아래로
                    {
                        scrollViewer.LineDown();
                    }
                    else // 휠을 위로
                    {
                        scrollViewer.LineUp();
                    }

                    // 이벤트를 여기서 처리했음을 시스템에 알려, 더 이상 이벤트가 전파되지 않도록(중복 스크롤 방지) 합니다.
                    e.Handled = true;
                }
            }
        }
        #endregion
    }
}
