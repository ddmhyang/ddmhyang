using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace WorkPartner
{
    public class TodoItem : INotifyPropertyChanged
    {
        private string _text;
        private bool _isCompleted;
        private DateTime? _dueDate;

        public string Text
        {
            get => _text;
            set { _text = value; OnPropertyChanged(nameof(Text)); }
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set { _isCompleted = value; OnPropertyChanged(nameof(IsCompleted)); }
        }

        public DateTime? DueDate
        {
            get => _dueDate;
            set { _dueDate = value; OnPropertyChanged(nameof(DueDate)); }
        }

        public ObservableCollection<TodoItem> SubTasks { get; set; }

        // [속성 추가] 태그 목록을 저장하기 위한 컬렉션입니다.
        public ObservableCollection<string> Tags { get; set; }

        public TodoItem()
        {
            Text = "새로운 할 일";
            SubTasks = new ObservableCollection<TodoItem>();
            Tags = new ObservableCollection<string>(); // Tags 컬렉션 초기화
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
