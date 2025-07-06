// 파일: MainWindow.xaml.cs (수정)
// [수정] 누락된 필드 변수 선언, using 구문, 메서드 호출 방식을 모두 수정했습니다.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using System.Text.Json;

namespace WorkPartner
{
    public partial class MainWindow : Window
    {
        // [수정] 페이지 참조를 위한 필드 변수 선언
        private DashboardPage _dashboardPage;
        private SettingsPage _settingsPage;
        private AnalysisPage _analysisPage;
        private ShopPage _shopPage;
        private ClosetPage _closetPage;
        private MiniTimerWindow _miniTimerWindow;

        public MainWindow()
        {
            InitializeComponent();
            _dashboardPage = new DashboardPage();
            _settingsPage = new SettingsPage();
            _analysisPage = new AnalysisPage();
            _shopPage = new ShopPage();
            _closetPage = new ClosetPage();

            PageContent.Content = _dashboardPage;
            // [수정] 버튼 객체를 직접 전달하도록 변경
            UpdateNavButtonSelection(DashboardButton);

            ToggleMiniTimer();
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

        // [수정] public으로 변경하여 외부에서 접근 가능하도록 함
        public void ToggleMiniTimer()
        {
            var settings = LoadSettings();
            if (settings.IsMiniTimerEnabled)
            {
                if (_miniTimerWindow == null || !_miniTimerWindow.IsVisible)
                {
                    _miniTimerWindow = new MiniTimerWindow();
                    _miniTimerWindow.Show();
                }
            }
            else
            {
                _miniTimerWindow?.Close();
                _miniTimerWindow = null;
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