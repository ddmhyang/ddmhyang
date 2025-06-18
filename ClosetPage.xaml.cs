using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging; // BitmapImage 사용을 위해 추가
using System.Windows.Shapes;       // Rectangle 사용을 위해 추가

namespace WorkPartner
{
    public partial class ClosetPage : UserControl
    {
        private readonly string _settingsFilePath = "app_settings.json";
        private readonly string _itemsDbFilePath = "items_db.json";
        private AppSettings _settings;
        private List<ShopItem> _fullShopInventory;
        private bool _isSliderUpdate = false;

        public ClosetPage()
        {
            InitializeComponent();
        }

        public void LoadData()
        {
            LoadSettings();
            LoadFullInventory();
            PopulateCategories();
            UpdateCharacterPreview();
        }

        private void LoadSettings()
        {
            if (!File.Exists(_settingsFilePath)) { _settings = new AppSettings(); return; }
            var json = File.ReadAllText(_settingsFilePath);
            _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }

        private void SaveSettings()
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(_settingsFilePath, json);
        }

        private void LoadFullInventory()
        {
            if (File.Exists(_itemsDbFilePath))
            {
                var json = File.ReadAllText(_itemsDbFilePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                _fullShopInventory = JsonSerializer.Deserialize<List<ShopItem>>(json, options) ?? new List<ShopItem>();
            }
            else
            {
                _fullShopInventory = new List<ShopItem>();
                MessageBox.Show("아이템 데이터베이스 파일(items_db.json)을 찾을 수 없습니다.", "오류");
            }
        }

        private void PopulateCategories()
        {
            var categories = Enum.GetValues(typeof(ItemType)).Cast<ItemType>().ToList();
            CategoryListBox.ItemsSource = categories;
            CategoryListBox.SelectedIndex = 0;
        }

        private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryListBox.SelectedItem is ItemType selectedType)
            {
                var itemsToShow = _fullShopInventory.Where(item => item.Type == selectedType).ToList();
                ItemsListView.ItemsSource = itemsToShow;

                if (IsColorCategory(selectedType))
                {
                    CustomColorPicker.Visibility = Visibility.Visible;
                    LoadCustomColorToSliders(selectedType);
                }
                else
                {
                    CustomColorPicker.Visibility = Visibility.Collapsed;
                }
                UpdateItemButtonsState();
            }
        }

        private void ItemButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Guid itemId)
            {
                var clickedItem = _fullShopInventory.FirstOrDefault(item => item.Id == itemId);
                if (clickedItem == null) return;

                if (!_settings.OwnedItemIds.Contains(itemId) && clickedItem.Price > 0)
                {
                    MessageBox.Show("아직 보유하지 않은 아이템입니다. 상점에서 먼저 구매해주세요!", "알림");
                    return;
                }

                if (_settings.EquippedItems.ContainsKey(clickedItem.Type) && _settings.EquippedItems[clickedItem.Type] == itemId)
                {
                    _settings.EquippedItems.Remove(clickedItem.Type);
                }
                else
                {
                    _settings.EquippedItems[clickedItem.Type] = itemId;
                }

                SaveSettings();
                UpdateCharacterPreview();
                UpdateItemButtonsState();
            }
        }

        private void UpdateItemButtonsState()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var item in ItemsListView.Items)
                {
                    var container = ItemsListView.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
                    if (container == null) continue;
                    var button = FindVisualChild<Button>(container);
                    if (button == null || !(button.Tag is Guid itemId)) continue;
                    var shopItem = _fullShopInventory.FirstOrDefault(i => i.Id == itemId);
                    if (shopItem == null) continue;
                    bool isOwned = _settings.OwnedItemIds.Contains(itemId) || shopItem.Price == 0;
                    bool isEquipped = _settings.EquippedItems.ContainsKey(shopItem.Type) && _settings.EquippedItems[shopItem.Type] == itemId;
                    if (isEquipped)
                    {
                        button.BorderBrush = Brushes.Gold;
                        button.BorderThickness = new Thickness(3);
                    }
                    else
                    {
                        button.BorderBrush = SystemColors.ControlDarkBrush;
                        button.BorderThickness = new Thickness(1);
                    }
                    button.Opacity = isOwned ? 1.0 : 0.5;
                }
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private void UpdateCharacterPreview()
        {
            CharacterPreviewGrid.Children.Clear();
            var sortedEquippedItems = _settings.EquippedItems
                .Select(pair => _fullShopInventory.FirstOrDefault(i => i.Id == pair.Value))
                .Where(item => item != null)
                .OrderBy(item => GetZIndex(item.Type));

            ShopItem hairStyleItem = sortedEquippedItems.FirstOrDefault(i => i.Type == ItemType.HairStyle);
            ShopItem hairColorItem = sortedEquippedItems.FirstOrDefault(i => i.Type == ItemType.HairColor);

            foreach (var item in sortedEquippedItems.Where(i => i.Type != ItemType.HairStyle && i.Type != ItemType.HairColor))
            {
                var partElement = CreateItemVisual(item);
                Panel.SetZIndex(partElement, GetZIndex(item.Type));
                CharacterPreviewGrid.Children.Add(partElement);
            }

            if (hairStyleItem != null)
            {
                var hairImage = new Image
                {
                    Source = new BitmapImage(new Uri(hairStyleItem.ImagePath, UriKind.RelativeOrAbsolute)),
                    Width = 100,
                    Height = 100,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var tintEffect = new TintColorEffect();
                if (_settings.CustomColors.ContainsKey(ItemType.HairColor))
                {
                    tintEffect.TintColor = (Color)ColorConverter.ConvertFromString(_settings.CustomColors[ItemType.HairColor]);
                }
                else if (hairColorItem != null && !string.IsNullOrEmpty(hairColorItem.ColorValue))
                {
                    tintEffect.TintColor = (Color)ColorConverter.ConvertFromString(hairColorItem.ColorValue);
                }
                else
                {
                    tintEffect.TintColor = Colors.White;
                }
                hairImage.Effect = tintEffect;
                Panel.SetZIndex(hairImage, GetZIndex(ItemType.HairStyle));
                CharacterPreviewGrid.Children.Add(hairImage);
            }
        }

        private FrameworkElement CreateItemVisual(ShopItem item)
        {
            if (string.IsNullOrEmpty(item.ImagePath) && !string.IsNullOrEmpty(item.ColorValue))
            {
                // 색상 값만 있는 경우 (눈 색, 옷 색 등)
                return new Rectangle
                {
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.ColorValue)),
                    Width = 100,
                    Height = 100,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
            }
            else
            {
                // 이미지 경로가 있는 경우
                return new Image
                {
                    Source = new BitmapImage(new Uri(item.ImagePath, UriKind.RelativeOrAbsolute)),
                    Width = 100,
                    Height = 100,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
            }
        }

        private int GetZIndex(ItemType type)
        {
            switch (type)
            {
                case ItemType.Background: return 0;
                case ItemType.Cushion: return 1;
                case ItemType.CushionColor: return 2;
                case ItemType.Clothes: return 10;
                case ItemType.ClothesColor: return 11;
                case ItemType.AnimalTail: return 12;
                case ItemType.HairStyle: return 20;
                case ItemType.HairColor: return 21;
                case ItemType.AnimalEar: return 22;
                case ItemType.EyeShape: return 23;
                case ItemType.EyeColor: return 24;
                case ItemType.MouthShape: return 25;
                case ItemType.FaceDeco1: return 30;
                case ItemType.FaceDeco2: return 31;
                case ItemType.FaceDeco3: return 32;
                case ItemType.FaceDeco4: return 33;
                case ItemType.Accessory1: return 40;
                case ItemType.Accessory2: return 41;
                case ItemType.Accessory3: return 42;
                default: return 5;
            }
        }

        private void ColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSliderUpdate || !(CategoryListBox.SelectedItem is ItemType selectedType)) return;
            if (!IsColorCategory(selectedType)) return;
            Color newColor = Color.FromRgb((byte)SliderR.Value, (byte)SliderG.Value, (byte)SliderB.Value);
            _settings.CustomColors[selectedType] = newColor.ToString();
            SaveSettings();
            UpdateCharacterPreview();
        }

        private void LoadCustomColorToSliders(ItemType type)
        {
            _isSliderUpdate = true;
            if (_settings.CustomColors.ContainsKey(type))
            {
                var color = (Color)ColorConverter.ConvertFromString(_settings.CustomColors[type]);
                SliderR.Value = color.R;
                SliderG.Value = color.G;
                SliderB.Value = color.B;
            }
            else
            {
                SliderR.Value = 255;
                SliderG.Value = 255;
                SliderB.Value = 255;
            }
            _isSliderUpdate = false;
        }

        private bool IsColorCategory(ItemType type)
        {
            return type == ItemType.HairColor || type == ItemType.EyeColor || type == ItemType.ClothesColor || type == ItemType.CushionColor;
        }

        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }
    }
}
