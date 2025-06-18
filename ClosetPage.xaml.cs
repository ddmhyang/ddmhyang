using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit; // ColorPicker 사용을 위해 추가

namespace WorkPartner
{
    public partial class ClosetPage : UserControl
    {
        private readonly string _settingsFilePath = "app_settings.json";
        private readonly string _itemsDbFilePath = "items_db.json";
        private AppSettings _settings;
        private List<ShopItem> _fullShopInventory;

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
            if (categories.Any())
            {
                CategoryListBox.SelectedIndex = 0;
            }
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
                    LoadCustomColorToPicker(selectedType);
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

                // 색상 아이템이 아닌 경우에만 착용/해제 로직 실행
                if (!IsColorCategory(clickedItem.Type))
                {
                    if (_settings.EquippedItems.ContainsKey(clickedItem.Type) && _settings.EquippedItems[clickedItem.Type] == itemId)
                    {
                        _settings.EquippedItems.Remove(clickedItem.Type);
                    }
                    else
                    {
                        _settings.EquippedItems[clickedItem.Type] = itemId;
                    }
                }

                SaveSettings();
                UpdateCharacterPreview();
                UpdateItemButtonsState();
            }
        }

        private void MyColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (CategoryListBox.SelectedItem is ItemType selectedType && MyColorPicker.SelectedColor.HasValue)
            {
                if (!IsColorCategory(selectedType)) return;

                _settings.CustomColors[selectedType] = MyColorPicker.SelectedColor.Value.ToString();
                SaveSettings();
                UpdateCharacterPreview();
            }
        }

        private void LoadCustomColorToPicker(ItemType type)
        {
            if (_settings.CustomColors.ContainsKey(type))
            {
                var color = (Color)ColorConverter.ConvertFromString(_settings.CustomColors[type]);
                MyColorPicker.SelectedColor = color;
            }
            else
            {
                MyColorPicker.SelectedColor = Colors.White;
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
                    bool isEquipped = !IsColorCategory(shopItem.Type) && _settings.EquippedItems.ContainsKey(shopItem.Type) && _settings.EquippedItems[shopItem.Type] == itemId;

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

            foreach (var item in sortedEquippedItems.Where(i => i.Type != ItemType.HairStyle))
            {
                var partElement = CreateItemVisual(item);
                Panel.SetZIndex(partElement, GetZIndex(item.Type));
                CharacterPreviewGrid.Children.Add(partElement);
            }

            if (hairStyleItem != null)
            {
                var hairImage = CreateItemVisual(hairStyleItem) as Image;
                if (hairImage == null) return;

                var tintEffect = new TintColorEffect();
                if (_settings.CustomColors.ContainsKey(ItemType.HairColor))
                {
                    tintEffect.TintColor = (Color)ColorConverter.ConvertFromString(_settings.CustomColors[ItemType.HairColor]);
                }
                else
                {
                    tintEffect.TintColor = Colors.White; // 기본 색상 (회색조 그대로)
                }
                hairImage.Effect = tintEffect;
                Panel.SetZIndex(hairImage, GetZIndex(ItemType.HairStyle));
                CharacterPreviewGrid.Children.Add(hairImage);
            }
        }

        private FrameworkElement CreateItemVisual(ShopItem item)
        {
            // 색상 값만 있는 아이템은 시각적 표현이 없으므로 null 반환
            if (string.IsNullOrEmpty(item.ImagePath)) return null;

            // 이미지 경로가 있는 경우 Image 컨트롤 생성
            try
            {
                return new Image
                {
                    Source = new BitmapImage(new Uri(item.ImagePath, UriKind.RelativeOrAbsolute)),
                    Width = 150,
                    Height = 150, // 크기 조정
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Stretch = Stretch.Uniform
                };
            }
            catch (Exception ex)
            {
                // 이미지 로드 실패 시 오류 메시지 표시
                System.Diagnostics.Debug.WriteLine($"이미지 로드 실패: {item.ImagePath}, 오류: {ex.Message}");
                return new TextBlock { Text = "이미지 오류", Foreground = Brushes.Red };
            }
        }

        private bool IsColorCategory(ItemType type)
        {
            return type == ItemType.HairColor ||
                   type == ItemType.EyeColor ||
                   type == ItemType.ClothesColor ||
                   type == ItemType.CushionColor;
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
                case ItemType.EyeShape: return 23;
                case ItemType.MouthShape: return 25;
                case ItemType.FaceDeco1: return 30;
                case ItemType.FaceDeco2: return 31;
                case ItemType.FaceDeco3: return 32;
                case ItemType.FaceDeco4: return 33;
                case ItemType.AnimalEar: return 40; // 머리보다 위에 오도록 수정
                case ItemType.Accessory1: return 41;
                case ItemType.Accessory2: return 42;
                case ItemType.Accessory3: return 43;
                default: return 5;
            }
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
