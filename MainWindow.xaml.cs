using System.Windows;
using System.IO;            // File 관련
using System.Text.Json;     // JsonSerializer 관련


namespace WorkPartner
{
    public partial class MainWindow : Window
    {
        private DashboardPage _dashboardPage;
        private SettingsPage _settingsPage;
        private AnalysisPage _analysisPage;
        private ShopPage _shopPage;
        private ClosetPage _closetPage; // [추가] 옷장 페이지 변수
        private MiniTimerWindow _miniTimerWindow; // [변수 추가]


        public MainWindow()
        {
            InitializeComponent();
            _dashboardPage = new DashboardPage();
            _settingsPage = new SettingsPage();
            _analysisPage = new AnalysisPage();
            _shopPage = new ShopPage();
            _closetPage = new ClosetPage(); // [추가] 옷장 페이지 초기화
            PageContent.Content = _dashboardPage;
            ToggleMiniTimer();

        }

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

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            _dashboardPage.LoadAllData();
            _dashboardPage.SetMiniTimerReference(_miniTimerWindow); // 참조 전달
            PageContent.Content = _dashboardPage;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            PageContent.Content = _settingsPage;
        }

        private void AnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            _analysisPage.LoadAndAnalyzeData();
            PageContent.Content = _analysisPage;
        }

        // [수정] 상점 버튼 클릭 시, 상점 페이지의 데이터를 새로고침합니다.
        private void ShopButton_Click(object sender, RoutedEventArgs e)
        {
            _shopPage.LoadSettings(); // 상점 페이지를 보여주기 전에 최신 설정(코인 정보)을 불러옵니다.
            PageContent.Content = _shopPage;
        }

        private void ClosetButton_Click(object sender, RoutedEventArgs e)
        {
            _closetPage.LoadData(); // 페이지를 보여주기 전에 최신 데이터를 불러옵니다.
            PageContent.Content = _closetPage;
        }

    }
}
