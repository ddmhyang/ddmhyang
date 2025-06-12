using System.Windows;

namespace WorkPartner
{
    public partial class MainWindow : Window
    {
        // 각 페이지들을 담아둘 변수
        private DashboardPage _dashboardPage;
        private SettingsPage _settingsPage;

        // [페이지 추가] 분석 페이지를 담을 변수를 추가합니다.
        private AnalysisPage _analysisPage;

        public MainWindow()
        {
            InitializeComponent();

            // 앱이 시작될 때 각 페이지를 한 번만 생성
            _dashboardPage = new DashboardPage();
            _settingsPage = new SettingsPage();

            // [페이지 추가] 분석 페이지 인스턴스 생성
            _analysisPage = new AnalysisPage();

            // 시작 페이지를 '대시보드'로 설정
            PageContent.Content = _dashboardPage;
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            PageContent.Content = _dashboardPage;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            PageContent.Content = _settingsPage;
        }

        // [이벤트 핸들러 추가] 분석 버튼 클릭 시, 분석 페이지를 보여줍니다.
        private void AnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            // 분석 페이지로 전환하기 전에, 최신 데이터를 다시 불러와 분석하도록 합니다.
            _analysisPage.LoadAndAnalyzeData();
            PageContent.Content = _analysisPage;
        }
    }
}
