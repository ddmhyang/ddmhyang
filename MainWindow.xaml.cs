using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WorkPartner
{
    public partial class MainWindow : Window
    {
        private DashboardPage _dashboardPage;
        private SettingsPage _settingsPage;
        private AnalysisPage _analysisPage;
        private AvatarCustomizationPage _avatarCustomizationPage; // ClosetPage를 AvatarCustomizationPage로 변경
        private MiniTimerWindow _miniTimerWindow;

        public MainWindow()
        {
            InitializeComponent();

            DataManager.PrepareFileForEditing("FocusPredictionModel.zip");

            _dashboardPage = new DashboardPage();
            _settingsPage = new SettingsPage();
            _analysisPage = new AnalysisPage();
            _avatarCustomizationPage = new AvatarCustomizationPage(); // 새 페이지 인스턴스 생성

            PageContent.Content = _dashboardPage;
            UpdateNavButtonSelection(DashboardButton);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ToggleMiniTimer();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            _dashboardPage?.SaveSoundSettings(); // 대시보드 페이지의 설정을 저장
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

        // 아바타 꾸미기 버튼 클릭 이벤트 핸들러
        private void AvatarButton_Click(object sender, RoutedEventArgs e)
        {
            _avatarCustomizationPage.LoadData(); // 페이지를 표시하기 전에 항상 데이터를 새로고침
            PageContent.Content = _avatarCustomizationPage;
            UpdateNavButtonSelection(sender as Button);
        }

        // 대시보드에서 호출할 수 있는 네비게이션 메서드
        public void NavigateToAvatarCustomization()
        {
            AvatarButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        private void AnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            _analysisPage.LoadAndAnalyzeData();
            PageContent.Content = _analysisPage;
            UpdateNavButtonSelection(sender as Button);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            PageContent.Content = _settingsPage;
            UpdateNavButtonSelection(sender as Button);
        }

        public void ToggleMiniTimer()
        {
            var settings = DataManager.LoadSettings();
            if (settings.IsMiniTimerEnabled)
            {
                if (_miniTimerWindow == null || !_miniTimerWindow.IsVisible)
                {
                    _miniTimerWindow = new MiniTimerWindow();
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
    }
}

