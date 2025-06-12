using System.Windows;

namespace WorkPartner
{
    public partial class MainWindow : Window
    {
        private DashboardPage _dashboardPage;
        private SettingsPage _settingsPage;
        private AnalysisPage _analysisPage;

        // [페이지 추가] 상점 페이지를 담을 변수를 추가합니다.
        private ShopPage _shopPage;

        public MainWindow()
        {
            InitializeComponent();
            _dashboardPage = new DashboardPage();
            _settingsPage = new SettingsPage();
            _analysisPage = new AnalysisPage();

            // [페이지 추가] 상점 페이지 인스턴스 생성
            _shopPage = new ShopPage();

            PageContent.Content = _dashboardPage;
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e) { PageContent.Content = _dashboardPage; }
        private void SettingsButton_Click(object sender, RoutedEventArgs e) { PageContent.Content = _settingsPage; }
        private void AnalysisButton_Click(object sender, RoutedEventArgs e) { _analysisPage.LoadAndAnalyzeData(); PageContent.Content = _analysisPage; }

        // [이벤트 핸들러 추가] 상점 버튼 클릭 시, 상점 페이지를 보여줍니다.
        private void ShopButton_Click(object sender, RoutedEventArgs e)
        {
            PageContent.Content = _shopPage;
        }
    }
}
