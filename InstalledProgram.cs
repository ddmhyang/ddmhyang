// InstalledProgram.cs
using System.Windows.Media;

namespace WorkPartner
{
    public class InstalledProgram
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string ProcessName { get; set; }
        public string ExePath { get; set; }
        public ImageSource Icon { get; set; }
        public string IconPath { get; set; }
    }
}