using System.Windows;
using System.Windows.Media;

namespace WorkPartner
{
    public partial class ColorPickerWindow : Window
    {
        public Color SelectedColor { get; private set; }

        public ColorPickerWindow(Color initialColor)
        {
            InitializeComponent();
            MyColorPicker.SelectedColor = initialColor;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (MyColorPicker.SelectedColor.HasValue)
            {
                SelectedColor = MyColorPicker.SelectedColor.Value;
                this.DialogResult = true;
            }
        }

        private void MyColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            // This event can be used for live preview if needed, but for now, we only care about the final value.
        }
    }
}
