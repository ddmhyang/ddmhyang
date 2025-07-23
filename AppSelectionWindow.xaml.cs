using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WorkPartner
{
    public partial class AppSelectionWindow : Window
    {
        public string SelectedAppKeyword { get; private set; }

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
                SelectedAppKeyword = selectedProgram?.ProcessName;
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