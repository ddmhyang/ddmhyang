using System.IO;
using System.Windows;
using System.Collections.ObjectModel;
using System.Text.Json; // JSON 사용을 위해 추가
using System.Linq;
using System.Windows.Controls;

namespace WorkPartner
{
    public partial class MemoWindow : Window
    {
        private readonly string _memoFilePath = "memos.json"; // 저장 파일 이름을 memos.json으로 변경
        public ObservableCollection<MemoItem> Memos { get; set; }
        private bool _isSaving = false; // 무한 루프 방지용 플래그

        public MemoWindow()
        {
            InitializeComponent();
            Memos = new ObservableCollection<MemoItem>();
            MemoListBox.ItemsSource = Memos;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadMemos();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveMemos();
        }

        private void LoadMemos()
        {
            if (File.Exists(_memoFilePath))
            {
                var json = File.ReadAllText(_memoFilePath);
                var loadedMemos = JsonSerializer.Deserialize<ObservableCollection<MemoItem>>(json);
                if (loadedMemos != null)
                {
                    Memos = loadedMemos;
                    MemoListBox.ItemsSource = Memos;
                }
            }
            // 불러올 메모가 없으면 새 메모 하나를 기본으로 추가
            if (!Memos.Any())
            {
                Memos.Add(new MemoItem());
            }
            MemoListBox.SelectedIndex = 0;
        }

        private void SaveMemos()
        {
            var options = new JsonSerializerOptions { WriteIndented = true }; // JSON을 예쁘게 저장
            var json = JsonSerializer.Serialize(Memos, options);
            File.WriteAllText(_memoFilePath, json);
        }

        private void NewMemoButton_Click(object sender, RoutedEventArgs e)
        {
            var newMemo = new MemoItem { Title = "새 제목" }; // 기본 제목 설정
            Memos.Add(newMemo);
            MemoListBox.SelectedItem = newMemo;
            MemoTitleTextBox.Focus(); // 제목 입력란에 바로 포커스
        }

        private void DeleteMemoButton_Click(object sender, RoutedEventArgs e)
        {
            if (MemoListBox.SelectedItem is MemoItem selectedMemo)
            {
                if (MessageBox.Show($"'{selectedMemo.Title}' 메모를 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Memos.Remove(selectedMemo);
                    if (!Memos.Any()) // 모든 메모가 삭제되었다면
                    {
                        Memos.Add(new MemoItem()); // 새 메모 추가
                    }
                    MemoListBox.SelectedIndex = 0;
                }
            }
        }

        private void MemoListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isSaving && MemoListBox.SelectedItem is MemoItem selectedMemo)
            {
                // [로직 추가] 이제 제목과 내용을 모두 채워줍니다.
                MemoTitleTextBox.Text = selectedMemo.Title;
                MemoContentTextBox.Text = selectedMemo.Content;
            }
        }

        private void MemoContentTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MemoListBox.SelectedItem is MemoItem selectedMemo)
            {
                _isSaving = true;
                selectedMemo.Content = MemoContentTextBox.Text; // Content 속성 업데이트
                _isSaving = false;
            }
        }

        // [이벤트 핸들러 추가] 제목 텍스트박스의 내용이 바뀔 때
        private void MemoTitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (MemoListBox.SelectedItem is MemoItem selectedMemo)
            {
                _isSaving = true;
                selectedMemo.Title = MemoTitleTextBox.Text; // Title 속성 업데이트
                _isSaving = false;
            }
        }
    }
}