using System.Windows;
using System.Windows.Input;

namespace WorkPartner
{
    public partial class MiniTimerWindow : Window
    {
        public MiniTimerWindow()
        {
            InitializeComponent();
        }

        // 창 드래그 기능
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        // 외부에서 시간을 업데이트하기 위한 메서드
        public void UpdateTime(string time)
        {
            TimeTextBlock.Text = time;
        }
    }
}