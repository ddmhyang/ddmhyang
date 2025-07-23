using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WorkPartner
{
    public partial class AppSelectionWindow : Window
    {
        public string SelectedAppKeyword { get; private set; }

        // [수정] 생성자: 프로그램 리스트와 웹사이트 리스트를 별도로 받습니다.
        public AppSelectionWindow(List<InstalledProgram> runningApps, List<InstalledProgram> websites)
        {
            InitializeComponent();
            AppListBox.ItemsSource = runningApps;
            WebsiteListBox.ItemsSource = websites;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSelection();
        }

        private void AppListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConfirmSelection();
        }

        private void WebsiteListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConfirmSelection();
        }

        // [수정] 선택 로직: 어느 리스트에서 선택되었는지 확인하고 키워드를 반환합니다.
        private void ConfirmSelection()
        {
            InstalledProgram selectedProgram = null;

            if (AppListBox.SelectedItem != null)
            {
                selectedProgram = AppListBox.SelectedItem as InstalledProgram;
                SelectedAppKeyword = selectedProgram?.ProcessName;
            }
            else if (WebsiteListBox.SelectedItem != null)
            {
                selectedProgram = WebsiteListBox.SelectedItem as InstalledProgram;
                SelectedAppKeyword = selectedProgram?.ProcessName; // 웹사이트의 경우 ProcessName에 URL 키워드가 저장됩니다.
            }

            if (selectedProgram != null)
            {
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("목록에서 항목을 선택해주세요.");
            }
        }
    }
}