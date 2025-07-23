using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace WorkPartner
{
    public partial class AppSelectionWindow : Window
    {
        // 선택된 앱 이름을 저장할 속성
        public string SelectedAppName { get; private set; }

        // 생성자: 앱 이름 목록을 받아서 ListBox에 채웁니다.
        public AppSelectionWindow(List<string> appNames)
        {
            InitializeComponent();
            AppListBox.ItemsSource = appNames;
        }

        // '확인' 버튼 클릭 시
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSelection();
        }

        // 목록에서 항목을 더블 클릭 시
        private void AppListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ConfirmSelection();
        }

        // 선택을 확정하고 창을 닫는 공통 메서드
        private void ConfirmSelection()
        {
            if (AppListBox.SelectedItem != null)
            {
                SelectedAppName = AppListBox.SelectedItem.ToString();
                this.DialogResult = true; // 성공적으로 선택했음을 알림
                this.Close();
            }
            else
            {
                MessageBox.Show("목록에서 프로그램을 선택해주세요.");
            }
        }
    }
}