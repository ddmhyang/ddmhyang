using System.Windows;

namespace WorkPartner
{
    public partial class MainWindow : Window
    {
        private DashboardPage _dashboardPage;
        private SettingsPage _settingsPage;
        private AnalysisPage _analysisPage;
        private ShopPage _shopPage;

        public MainWindow()
        {
            InitializeComponent();
            _dashboardPage = new DashboardPage();
            _settingsPage = new SettingsPage();
            _analysisPage = new AnalysisPage();
            _shopPage = new ShopPage();
            PageContent.Content = _dashboardPage;
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            _dashboardPage.LoadAllData(); // 대시보드로 돌아올 때마다 데이터 새로고침
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
    }
}
