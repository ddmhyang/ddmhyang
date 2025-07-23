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
        private List<InstalledProgram> _allPrograms; // 설치된 프로그램 + 실행 중인 프로그램 목록
        private TextBox _currentTextBox;

        public SettingsPage()
        {
            InitializeComponent();
            LoadSettings();
            UpdateUIFromSettings();

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (s, e) => { _allPrograms = GetAllPrograms(); };
            worker.RunWorkerAsync();
        }

        #region 데이터 로드 및 저장
        private void LoadSettings() { if (File.Exists(_settingsFilePath)) { var json = File.ReadAllText(_settingsFilePath); Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings(); } else { Settings = new AppSettings(); } }
        private void SaveSettings() { var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }; var json = JsonSerializer.Serialize(Settings, options); File.WriteAllText(_settingsFilePath, json); }
        private void UpdateUIFromSettings() { IdleDetectionCheckBox.IsChecked = Settings.IsIdleDetectionEnabled; IdleTimeoutTextBox.Text = Settings.IdleTimeoutSeconds.ToString(); MiniTimerCheckBox.IsChecked = Settings.IsMiniTimerEnabled; WorkProcessListBox.ItemsSource = Settings.WorkProcesses; PassiveProcessListBox.ItemsSource = Settings.PassiveProcesses; DistractionProcessListBox.ItemsSource = Settings.DistractionProcesses; NagMessageTextBox.Text = Settings.FocusModeNagMessage; NagIntervalTextBox.Text = Settings.FocusModeNagIntervalSeconds.ToString(); TagRulesListView.ItemsSource = Settings.TagRules; }
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
                    {
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
                                            // UI 스레드에서 아이콘을 생성하도록 경로만 저장
                                            programs[processName] = new InstalledProgram
                                            {
                                                DisplayName = displayName,
                                                ProcessName = processName,
                                                IconPath = executablePath
                                            };
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception) { /* 레지스트리 접근 오류 무시 */ }
            }

            // 아이콘 생성은 UI 스레드에 위임
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
                    return Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
            }
            catch { return null; }
        }
        #endregion

        #region 버튼 이벤트 핸들러
        // SettingsPage.xaml.cs

        // SettingsPage.xaml.cs

        // SettingsPage.xaml.cs

        private void SelectRunningAppButton_Click(object sender, RoutedEventArgs e)
        {
            var regularApps = new List<InstalledProgram>();
            var websitesAndFiles = new List<InstalledProgram>();
            var browserProcesses = new HashSet<string> { "chrome", "msedge", "firefox", "whale" };
            var addedProcesses = new HashSet<string>();

            var runningProcesses = Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.MainWindowTitle));

            foreach (var process in runningProcesses)
            {
                try
                {
                    string processName = process.ProcessName.ToLower();
                    string windowTitle = process.MainWindowTitle;
                    string executablePath = process.MainModule.FileName;

                    // 1. 모든 프로그램을 '전체 프로그램' 목록에 추가
                    if (!addedProcesses.Contains(processName))
                    {
                        regularApps.Add(new InstalledProgram { DisplayName = windowTitle, ProcessName = processName, Icon = GetIcon(executablePath) });
                        addedProcesses.Add(processName);
                    }

                    // 2. 브라우저인 경우, 모든 탭을 '웹사이트' 목록에 추가
                    if (browserProcesses.Contains(processName))
                    {
                        foreach (var browserName in browserProcesses)
                        {
                            var tabs = ActiveWindowHelper.GetBrowserTabInfos(browserName);
                            foreach (var tab in tabs)
                            {
                                // 중복되지 않은 탭만 추가
                                if (!websites.Any(w => w.DisplayName == tab.Title))
                                {
                                    // 아이콘은 해당 브라우저의 아이콘을 가져옵니다.
                                    var browserProcess = Process.GetProcessesByName(browserName).FirstOrDefault();
                                    if (browserProcess != null)
                                    {
                                        websites.Add(new InstalledProgram { DisplayName = tab.Title, ProcessName = tab.UrlKeyword, Icon = GetIcon(browserProcess.MainModule.FileName) });
                                    }
                                }
                            }
                        }
                    }
                    var sortedApps = allRunningApps.OrderBy(p => p.DisplayName).ToList();

                    // 3. 파일 탐색기인 경우, 열려있는 폴더 경로를 목록에 추가
                    else if (processName == "explorer")
                    {
                        // (이 부분은 더 복잡한 로직이 필요하여 추후 구현 가능합니다)
                        // 현재는 탐색기 자체만 표시됩니다.
                    }
                }
                catch (Exception) { /* 접근 권한 없는 프로세스 무시 */ }
            }

            var sortedApps = regularApps.OrderBy(p => p.DisplayName).ToList();
            var sortedWebsites = websitesAndFiles.OrderBy(p => p.DisplayName).ToList();

            var selectionWindow = new AppSelectionWindow(sortedApps, sortedWebsites) { Owner = Window.GetWindow(this) };

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

        // 창 제목에서 사이트 이름을 추출하는 도우미 메서드 (클래스 내부에 추가)
        private string ParseSiteNameFromTitle(string title)
        {
            title = title.Replace(" - Google Chrome", "")
                         .Replace(" - Microsoft​ Edge", "")
                         .Replace(" — Mozilla Firefox", "")
                         .Replace(" - Naver Whale", "");

            if (title.Equals("New Tab", StringComparison.OrdinalIgnoreCase) ||
                title.Equals("새 탭", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(title))
            {
                return null;
            }
            return title.Trim();
        }

        private void AddWorkProcessButton_Click(object sender, RoutedEventArgs e) { var newProcess = WorkProcessInputTextBox.Text.Trim().ToLower(); if (!string.IsNullOrEmpty(newProcess) && !Settings.WorkProcesses.Contains(newProcess)) { Settings.WorkProcesses.Add(newProcess); WorkProcessInputTextBox.Clear(); SaveSettings(); WorkProcessListBox.ItemsSource = null; WorkProcessListBox.ItemsSource = Settings.WorkProcesses; } }
        private void AddPassiveProcessButton_Click(object sender, RoutedEventArgs e) { var newProcess = PassiveProcessInputTextBox.Text.Trim().ToLower(); if (!string.IsNullOrEmpty(newProcess) && !Settings.PassiveProcesses.Contains(newProcess)) { Settings.PassiveProcesses.Add(newProcess); PassiveProcessInputTextBox.Clear(); SaveSettings(); PassiveProcessListBox.ItemsSource = null; PassiveProcessListBox.ItemsSource = Settings.PassiveProcesses; } }
        private void AddDistractionProcessButton_Click(object sender, RoutedEventArgs e) { var newProcess = DistractionProcessInputTextBox.Text.Trim().ToLower(); if (!string.IsNullOrEmpty(newProcess) && !Settings.DistractionProcesses.Contains(newProcess)) { Settings.DistractionProcesses.Add(newProcess); DistractionProcessInputTextBox.Clear(); SaveSettings(); DistractionProcessListBox.ItemsSource = null; DistractionProcessListBox.ItemsSource = Settings.DistractionProcesses; } }

        private void DeleteWorkProcessButton_Click(object sender, RoutedEventArgs e) { if (WorkProcessListBox.SelectedItem is string selected) { Settings.WorkProcesses.Remove(selected); SaveSettings(); WorkProcessListBox.ItemsSource = null; WorkProcessListBox.ItemsSource = Settings.WorkProcesses; } }
        private void DeletePassiveProcessButton_Click(object sender, RoutedEventArgs e) { if (PassiveProcessListBox.SelectedItem is string selected) { Settings.PassiveProcesses.Remove(selected); SaveSettings(); PassiveProcessListBox.ItemsSource = null; PassiveProcessListBox.ItemsSource = Settings.PassiveProcesses; } }
        private void DeleteDistractionProcessButton_Click(object sender, RoutedEventArgs e) { if (DistractionProcessListBox.SelectedItem is string selected) { Settings.DistractionProcesses.Remove(selected); SaveSettings(); DistractionProcessListBox.ItemsSource = null; DistractionProcessListBox.ItemsSource = Settings.DistractionProcesses; } }

        private void Setting_Changed(object sender, RoutedEventArgs e) { if (Settings == null) return; if (sender == IdleDetectionCheckBox) { Settings.IsIdleDetectionEnabled = IdleDetectionCheckBox.IsChecked ?? true; } else if (sender == MiniTimerCheckBox) { Settings.IsMiniTimerEnabled = MiniTimerCheckBox.IsChecked ?? false; (Application.Current.MainWindow as MainWindow)?.ToggleMiniTimer(); } SaveSettings(); }
        private void Setting_Changed_IdleTimeout(object sender, TextChangedEventArgs e) { if (Settings != null && int.TryParse(IdleTimeoutTextBox.Text, out int timeout)) { Settings.IdleTimeoutSeconds = timeout; SaveSettings(); } }
        private void NagMessageTextBox_TextChanged(object sender, TextChangedEventArgs e) { if (Settings != null) { Settings.FocusModeNagMessage = NagMessageTextBox.Text; SaveSettings(); } }
        private void NagIntervalTextBox_TextChanged(object sender, TextChangedEventArgs e) { if (Settings != null && int.TryParse(NagIntervalTextBox.Text, out int interval) && interval > 0) { Settings.FocusModeNagIntervalSeconds = interval; SaveSettings(); } }
        private void AddTagRuleButton_Click(object sender, RoutedEventArgs e) { string keyword = KeywordInput.Text.Trim(); string tag = TagInput.Text.Trim(); if (string.IsNullOrEmpty(keyword) || string.IsNullOrEmpty(tag)) { MessageBox.Show("키워드와 태그를 모두 입력해주세요."); return; } if (!tag.StartsWith("#")) tag = "#" + tag; if (!Settings.TagRules.ContainsKey(keyword)) { Settings.TagRules[keyword] = tag; TagRulesListView.ItemsSource = null; TagRulesListView.ItemsSource = Settings.TagRules; SaveSettings(); KeywordInput.Clear(); TagInput.Clear(); } else { MessageBox.Show("이미 존재하는 키워드입니다."); } }
        private void DeleteTagRuleButton_Click(object sender, RoutedEventArgs e) { if (TagRulesListView.SelectedItem is KeyValuePair<string, string> selectedRule) { if (MessageBox.Show($"'{selectedRule.Key}' -> '{selectedRule.Value}' 규칙을 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { Settings.TagRules.Remove(selectedRule.Key); TagRulesListView.ItemsSource = null; TagRulesListView.ItemsSource = Settings.TagRules; SaveSettings(); } } else { MessageBox.Show("삭제할 규칙을 목록에서 선택해주세요."); } }
        private void ResetDataButton_Click(object sender, RoutedEventArgs e) { if (MessageBox.Show("정말로 모든 데이터를 영구적으로 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.", "데이터 초기화 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) { try { var filesToDelete = new string[] { DataManager.SettingsFilePath, DataManager.TimeLogFilePath, DataManager.TasksFilePath, DataManager.TodosFilePath, DataManager.MemosFilePath, DataManager.ModelFilePath }; foreach (var filePath in filesToDelete) { if (File.Exists(filePath)) File.Delete(filePath); } MessageBox.Show("모든 데이터가 성공적으로 초기화되었습니다.\n프로그램을 다시 시작해주세요.", "초기화 완료"); Application.Current.Shutdown(); } catch (Exception ex) { MessageBox.Show($"데이터 초기화 중 오류가 발생했습니다: {ex.Message}", "오류"); } } }
        #endregion
    }
}