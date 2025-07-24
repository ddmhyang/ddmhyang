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
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading; // 타이머를 위해 추가
using Microsoft.Win32;

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
        }

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
    }
}
