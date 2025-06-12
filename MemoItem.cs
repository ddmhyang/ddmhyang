// 파일 경로: WorkPartner/MemoItem.cs
using System;
using System.ComponentModel;
using System.Linq;

namespace WorkPartner
{
    public class MemoItem : INotifyPropertyChanged
    {
        public Guid Id { get; set; }

        private string _title;
        public string Title
        {
            get { return _title; }
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        private string _content;
        public string Content
        {
            get { return _content; }
            set
            {
                _content = value;
                OnPropertyChanged(nameof(Content));
                OnPropertyChanged(nameof(Snippet)); // 내용이 바뀌면 미리보기도 바뀌도록 알림
            }
        }

        // 목록에 표시될 내용 미리보기 (최대 30자)
        public string Snippet
        {
            get
            {
                if (string.IsNullOrEmpty(Content)) return string.Empty;
                return new string(Content.Replace('\n', ' ').Replace('\r', ' ').Take(30).ToArray());
            }
        }

        public MemoItem()
        {
            Id = Guid.NewGuid();
            Title = "새 메모";
            Content = "";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}