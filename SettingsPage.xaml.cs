// 파일: SettingsPage.xaml.cs (수정)
// [수정] 누락되었던 컨트롤 관련 로직을 복원했습니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace WorkPartner
{
    public partial class SettingsPage : UserControl
    {
        private readonly string _settingsFilePath = DataManager.SettingsFilePath;
        public AppSettings Settings { get; set; }

        public SettingsPage()
        {
            InitializeComponent();
            LoadSettings();
            UpdateUIFromSettings();
        }

        private void LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                Settings = new AppSettings();
            }
        }

        private void SaveSettings()
        {

            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(Settings, options);

            File.WriteAllText(_settingsFilePath, json);
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

            SaveSettings();
        }
        private void Setting_Changed_IdleTimeout(object sender, TextChangedEventArgs e) { if (Settings == null) return; if (int.TryParse(IdleTimeoutTextBox.Text, out int timeout)) { Settings.IdleTimeoutSeconds = timeout; SaveSettings(); } }
        private void AddWorkProcessButton_Click(object sender, RoutedEventArgs e) { var newProcess = WorkProcessInputTextBox.Text.Trim().ToLower(); if (!string.IsNullOrEmpty(newProcess) && !Settings.WorkProcesses.Contains(newProcess)) { Settings.WorkProcesses.Add(newProcess); WorkProcessInputTextBox.Clear(); SaveSettings(); } }
        private void DeleteWorkProcessButton_Click(object sender, RoutedEventArgs e) { if (WorkProcessListBox.SelectedItem is string selectedProcess) { Settings.WorkProcesses.Remove(selectedProcess); SaveSettings(); } }
        private void AddPassiveProcessButton_Click(object sender, RoutedEventArgs e) { var newProcess = PassiveProcessInputTextBox.Text.Trim().ToLower(); if (!string.IsNullOrEmpty(newProcess) && !Settings.PassiveProcesses.Contains(newProcess)) { Settings.PassiveProcesses.Add(newProcess); PassiveProcessInputTextBox.Clear(); SaveSettings(); } }
        private void DeletePassiveProcessButton_Click(object sender, RoutedEventArgs e) { if (PassiveProcessListBox.SelectedItem is string selectedProcess) { Settings.PassiveProcesses.Remove(selectedProcess); SaveSettings(); } }
        private void AddDistractionProcessButton_Click(object sender, RoutedEventArgs e) { var newProcess = DistractionProcessInputTextBox.Text.Trim().ToLower(); if (!string.IsNullOrEmpty(newProcess) && !Settings.DistractionProcesses.Contains(newProcess)) { Settings.DistractionProcesses.Add(newProcess); DistractionProcessInputTextBox.Clear(); SaveSettings(); } }
        private void DeleteDistractionProcessButton_Click(object sender, RoutedEventArgs e) { if (DistractionProcessListBox.SelectedItem is string selectedProcess) { Settings.DistractionProcesses.Remove(selectedProcess); SaveSettings(); } }

        private void NagMessageTextBox_TextChanged(object sender, TextChangedEventArgs e) { if (Settings == null) return; Settings.FocusModeNagMessage = NagMessageTextBox.Text; SaveSettings(); }
        private void NagIntervalTextBox_TextChanged(object sender, TextChangedEventArgs e) { if (Settings == null) return; if (int.TryParse(NagIntervalTextBox.Text, out int interval)) { if (interval > 0) { Settings.FocusModeNagIntervalSeconds = interval; SaveSettings(); } } }


        private void AddTagRuleButton_Click(object sender, RoutedEventArgs e)
        {
            string keyword = KeywordInput.Text.Trim();
            string tag = TagInput.Text.Trim();

            if (string.IsNullOrEmpty(keyword) || string.IsNullOrEmpty(tag))
            {
                MessageBox.Show("키워드와 태그를 모두 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!tag.StartsWith("#"))
            {
                tag = "#" + tag;
            }

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
                MessageBox.Show("이미 존재하는 키워드입니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteTagRuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (TagRulesListView.SelectedItem is KeyValuePair<string, string> selectedRule)
            {
                if (MessageBox.Show($"'{selectedRule.Key}' -> '{selectedRule.Value}' 규칙을 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Settings.TagRules.Remove(selectedRule.Key);
                    TagRulesListView.ItemsSource = null;
                    TagRulesListView.ItemsSource = Settings.TagRules;
                    SaveSettings();
                }
            }
            else
            {
                MessageBox.Show("삭제할 규칙을 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // SettingsPage.xaml.cs 파일 내부

        private void ResetDataButton_Click(object sender, RoutedEventArgs e)
        {
            // 사용자에게 다시 한번 확인받는 메시지 박스를 띄웁니다.
            MessageBoxResult result = MessageBox.Show(
                "정말로 모든 데이터를 영구적으로 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.",
                "데이터 초기화 확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            // 사용자가 'Yes'를 눌렀을 때만 삭제 로직을 실행합니다.
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // DataManager에서 관리하는 모든 파일 경로를 가져옵니다.
                    var filesToDelete = new string[]
                    {
                DataManager.SettingsFilePath,
                DataManager.TimeLogFilePath,
                DataManager.TasksFilePath,
                DataManager.TodosFilePath,
                DataManager.MemosFilePath,
                DataManager.ModelFilePath
                    };

                    // 각 파일을 순회하며 존재하면 삭제합니다.
                    foreach (var filePath in filesToDelete)
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }

                    MessageBox.Show("모든 데이터가 성공적으로 초기화되었습니다.\n프로그램을 다시 시작해주세요.", "초기화 완료");

                    // 프로그램을 종료하여 사용자가 다시 시작하도록 유도합니다.
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"데이터 초기화 중 오류가 발생했습니다: {ex.Message}", "오류");
                }
            }
        }
    }
}