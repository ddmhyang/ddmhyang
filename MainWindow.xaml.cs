// 파일: MainWindow.xaml.cs (수정)
// [수정] Owner 속성 설정을 제거하고, Closing 이벤트를 통해 미니 타이머를 직접 닫도록 변경했습니다.
using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;

namespace WorkPartner
{
    public partial class MainWindow : Window
    {
        private DashboardPage _dashboardPage;
        private SettingsPage _settingsPage;
        private AnalysisPage _analysisPage;
        private ShopPage _shopPage;
        private ClosetPage _closetPage;
        private MiniTimerWindow _miniTimerWindow;

        public MainWindow()
        {
            InitializeComponent();

            DataManager.PrepareFileForEditing("FocusPredictionModel.zip");

            _dashboardPage = new DashboardPage();
            _settingsPage = new SettingsPage();
            _analysisPage = new AnalysisPage();
            _shopPage = new ShopPage();
            _closetPage = new ClosetPage();

            PageContent.Content = _dashboardPage;
            UpdateNavButtonSelection(DashboardButton);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ToggleMiniTimer();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // 메인 창이 닫힐 때 미니 타이머 창도 함께 닫습니다.
            _miniTimerWindow?.Close();
        }

        private void UpdateNavButtonSelection(Button selectedButton)
        {
            foreach (var child in NavigationPanel.Children)
            {
                if (child is Button button)
                {
                    button.Background = Brushes.Transparent;
                }
            }
            if (selectedButton != null)
            {
                selectedButton.Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            }
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            _dashboardPage.LoadAllData();
            _dashboardPage.SetMiniTimerReference(_miniTimerWindow);
            PageContent.Content = _dashboardPage;
            UpdateNavButtonSelection(sender as Button);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            PageContent.Content = _settingsPage;
            UpdateNavButtonSelection(sender as Button);
        }

        private void AnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            _analysisPage.LoadAndAnalyzeData();
            PageContent.Content = _analysisPage;
            UpdateNavButtonSelection(sender as Button);
        }

        private void ShopButton_Click(object sender, RoutedEventArgs e)
        {
            _shopPage.LoadSettings();
            PageContent.Content = _shopPage;
            UpdateNavButtonSelection(sender as Button);
        }

        private void ClosetButton_Click(object sender, RoutedEventArgs e)
        {
            _closetPage.LoadData();
            PageContent.Content = _closetPage;
            UpdateNavButtonSelection(sender as Button);
        }

        public void ToggleMiniTimer()
        {
            var settings = LoadSettings();
            if (settings.IsMiniTimerEnabled)
            {
                if (_miniTimerWindow == null || !_miniTimerWindow.IsVisible)
                {
                    _miniTimerWindow = new MiniTimerWindow();
                    // [수정] 오류의 원인이 되는 Owner 속성 설정 제거
                    _miniTimerWindow.Show();
                    _dashboardPage?.SetMiniTimerReference(_miniTimerWindow);
                }
            }
            else
            {
                _miniTimerWindow?.Close();
                _miniTimerWindow = null;
                _dashboardPage?.SetMiniTimerReference(null);
            }
        }

        private AppSettings LoadSettings()
        {
            string settingsFilePath = "app_settings.json";
            if (File.Exists(settingsFilePath))
            {
                var json = File.ReadAllText(settingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            return new AppSettings();
        }
    }
}