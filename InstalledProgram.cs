// InstalledProgram.cs (새 파일)
using System.Windows.Media;

namespace WorkPartner
{
    public class InstalledProgram
    {
        public string DisplayName { get; set; } // 예: "카카오톡"
        public string ProcessName { get; set; } // 예: "kakaotalk"
        public ImageSource Icon { get; set; }    // 프로그램 아이콘
    }
}