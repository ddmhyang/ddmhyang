using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace WorkPartner
{
    public partial class SettingsPage : UserControl
    {
        private readonly string _settingsFilePath = "app_settings.json";
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
            WorkProcessListBox.ItemsSource = Settings.WorkProcesses;
            PassiveProcessListBox.ItemsSource = Settings.PassiveProcesses;
            DistractionProcessListBox.ItemsSource = Settings.DistractionProcesses;
            NagMessageTextBox.Text = Settings.FocusModeNagMessage;
            NagIntervalTextBox.Text = Settings.FocusModeNagIntervalSeconds.ToString();

            // [로직 추가] 태그 규칙 리스트를 UI에 연결합니다.
            TagRulesListView.ItemsSource = Settings.TagRules;
        }

        // --- 기존 이벤트 핸들러 (생략) ---
        private void Setting_Changed(object sender, RoutedEventArgs e) { if (Settings == null) return; Settings.IsIdleDetectionEnabled = IdleDetectionCheckBox.IsChecked ?? true; SaveSettings(); }
        private void Setting_Changed_IdleTimeout(object sender, TextChangedEventArgs e) { if (Settings == null) return; if (int.TryParse(IdleTimeoutTextBox.Text, out int timeout)) { Settings.IdleTimeoutSeconds = timeout; SaveSettings(); } }
        private void AddWorkProcessButton_Click(object sender, RoutedEventArgs e) { var newProcess = WorkProcessInputTextBox.Text.Trim().ToLower(); if (!string.IsNullOrEmpty(newProcess) && !Settings.WorkProcesses.Contains(newProcess)) { Settings.WorkProcesses.Add(newProcess); WorkProcessInputTextBox.Clear(); SaveSettings(); } }
        private void DeleteWorkProcessButton_Click(object sender, RoutedEventArgs e) { if (WorkProcessListBox.SelectedItem is string selectedProcess) { Settings.WorkProcesses.Remove(selectedProcess); SaveSettings(); } }
        private void AddPassiveProcessButton_Click(object sender, RoutedEventArgs e) { var newProcess = PassiveProcessInputTextBox.Text.Trim().ToLower(); if (!string.IsNullOrEmpty(newProcess) && !Settings.PassiveProcesses.Contains(newProcess)) { Settings.PassiveProcesses.Add(newProcess); PassiveProcessInputTextBox.Clear(); SaveSettings(); } }
        private void DeletePassiveProcessButton_Click(object sender, RoutedEventArgs e) { if (PassiveProcessListBox.SelectedItem is string selectedProcess) { Settings.PassiveProcesses.Remove(selectedProcess); SaveSettings(); } }
        private void AddDistractionProcessButton_Click(object sender, RoutedEventArgs e) { var newProcess = DistractionProcessInputTextBox.Text.Trim().ToLower(); if (!string.IsNullOrEmpty(newProcess) && !Settings.DistractionProcesses.Contains(newProcess)) { Settings.DistractionProcesses.Add(newProcess); DistractionProcessInputTextBox.Clear(); SaveSettings(); } }
        private void DeleteDistractionProcessButton_Click(object sender, RoutedEventArgs e) { if (DistractionProcessListBox.SelectedItem is string selectedProcess) { Settings.DistractionProcesses.Remove(selectedProcess); SaveSettings(); } }
        private void NagMessageTextBox_TextChanged(object sender, TextChangedEventArgs e) { if (Settings == null) return; Settings.FocusModeNagMessage = NagMessageTextBox.Text; SaveSettings(); }
        private void NagIntervalTextBox_TextChanged(object sender, TextChangedEventArgs e) { if (Settings == null) return; if (int.TryParse(NagIntervalTextBox.Text, out int interval)) { if (interval > 0) { Settings.FocusModeNagIntervalSeconds = interval; SaveSettings(); } } }


        // --- [이벤트 핸들러 추가] AI 태그 규칙 관리 ---
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
                TagRulesListView.ItemsSource = null; // UI 새로고침을 위해 잠시 연결을 끊었다가
                TagRulesListView.ItemsSource = Settings.TagRules; // 다시 연결합니다.
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
    }
}
