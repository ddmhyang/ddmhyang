// SettingsPage.xaml.cs (기존 내용을 모두 지우고 아래 코드로 교체)
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
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WorkPartner
{
    public partial class SettingsPage : UserControl
    {
        private readonly string _settingsFilePath = DataManager.SettingsFilePath;
        public AppSettings Settings { get; set; }
        private List<InstalledProgram> _allPrograms;
        private TextBox _currentTextBox;

        // SettingsPage.xaml.cs
        public SettingsPage()
        {
            InitializeComponent();
            LoadSettings();
            UpdateUIFromSettings();

            // 로딩 시작 시 프로그레스 바를 보이게 함
            LoadingProgressBar.Visibility = Visibility.Visible;

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (s, e) => { _allPrograms = GetAllPrograms(); };
            worker.RunWorkerCompleted += (s, e) =>
            {
                // 작업 완료 시 프로그레스 바를 숨김
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            };
            worker.RunWorkerAsync();
        }

        #region 데이터 로드 및 저장
        // LoadSettings와 SaveSettings 메서드를 제거하거나 DataManager의 정적 메서드를 호출하도록 수정
        private void LoadSettings()
        {
            // DataManager.LoadSettings()를 호출하도록 변경
            Settings = DataManager.LoadSettings();
        }

        // SaveSettings 메서드를 제거하거나 DataManager.SaveSettings()를 호출하도록 변경
        private void SaveSettings()
        {
            // DataManager.SaveSettings()를 호출하도록 변경
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

        #region 자동 완성 검색 로직
        private void ProcessInputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allPrograms == null || _allPrograms.Count == 0) return;

            _currentTextBox = sender as TextBox;
            string searchText = _currentTextBox.Text.ToLower();

            var (popup, suggestionListBox) = GetControlsForTextBox(_currentTextBox);
            if (popup == null) return;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                popup.IsOpen = false;
                return;
            }

            var suggestions = _allPrograms.Where(p => p.DisplayName.ToLower().Contains(searchText) || p.ProcessName.ToLower().Contains(searchText)).ToList();
            if (suggestions.Any())
            {
                suggestionListBox.ItemsSource = suggestions;
                popup.IsOpen = true;
            }
            else
            {
                popup.IsOpen = false;
            }
        }

        private void SuggestionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            var (popup, _) = GetControlsForListBox(listBox);
            if (popup != null && listBox.SelectedItem is InstalledProgram selectedProgram && _currentTextBox != null)
            {
                _currentTextBox.Text = selectedProgram.ProcessName;
                popup.IsOpen = false;
            }
        }

        private void ProcessInputTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var popup = GetControlsForTextBox(sender as TextBox).Popup;
            if (popup != null && !popup.IsMouseOver)
            {
                popup.IsOpen = false;
            }
        }

        private (Popup Popup, ListBox ListBox) GetControlsForTextBox(TextBox textBox)
        {
            if (textBox == WorkProcessInputTextBox) return (WorkProcessPopup, WorkProcessSuggestionListBox);
            if (textBox == PassiveProcessInputTextBox) return (PassiveProcessPopup, PassiveProcessSuggestionListBox);
            if (textBox == DistractionProcessInputTextBox) return (DistractionProcessPopup, DistractionProcessSuggestionListBox);
            return (null, null);
        }

        private (Popup Popup, ListBox ListBox) GetControlsForListBox(ListBox listBox)
        {
            if (listBox == WorkProcessSuggestionListBox) return (WorkProcessPopup, WorkProcessSuggestionListBox);
            if (listBox == PassiveProcessSuggestionListBox) return (PassiveProcessPopup, PassiveProcessSuggestionListBox);
            if (listBox == DistractionProcessSuggestionListBox) return (DistractionProcessPopup, DistractionProcessSuggestionListBox);
            return (null, null);
        }
        #endregion

        #region 프로그램 목록 및 아이콘 추출 로직
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
            if (string.IsNullOrEmpty(filePath)) return null;
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

        #region 버튼 이벤트 핸들러
        private void SelectRunningAppButton_Click(object sender, RoutedEventArgs e)
        {
            var runningApps = new List<InstalledProgram>();

            // 기존의 ActiveWindowHelper.GetRunningAppInfos() 대신
            // Process.GetProcesses()를 사용하여 앱 목록을 가져옵니다.
            var allProcesses = Process.GetProcesses()
                .Where(p => !string.IsNullOrWhiteSpace(p.MainWindowTitle) && p.MainWindowHandle != IntPtr.Zero)
                .ToList();

            foreach (var process in allProcesses)
            {
                var app = new InstalledProgram
                {
                    DisplayName = process.MainWindowTitle,
                    ProcessName = process.ProcessName
                };
                runningApps.Add(app);
            }

            // 브라우저 탭 정보를 가져오는 새로운 헬퍼 메서드를 호출합니다.
            runningApps.AddRange(BrowserTabHelper.GetAllBrowserTabs());

            var selectionWindow = new AppSelectionWindow(runningApps);
            if (selectionWindow.ShowDialog() == true)
            {
                {
                string selectedKeyword = selectionWindow.SelectedAppKeyword;
                if (string.IsNullOrEmpty(selectedKeyword)) return;
                string targetList = (sender as Button)?.Tag as string;
                if (targetList == "Work") WorkProcessInputTextBox.Text = selectedKeyword;
                else if (targetList == "Passive") PassiveProcessInputTextBox.Text = selectedKeyword;
                else if (targetList == "Distraction") DistractionProcessInputTextBox.Text = selectedKeyword;
            }
        }

        private void AddWorkProcessButton_Click(object sender, RoutedEventArgs e)
        {
            var newProcess = WorkProcessInputTextBox.Text.Trim().ToLower();
            if (!string.IsNullOrEmpty(newProcess) && !Settings.WorkProcesses.Contains(newProcess))
            {
                Settings.WorkProcesses.Add(newProcess);
                WorkProcessInputTextBox.Clear();
                // DataManager의 정적 메서드 호출
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
            DataManager.SaveSettingsAndNotify(Settings); // DataManager의 정적 메서드 호출
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
    }
}