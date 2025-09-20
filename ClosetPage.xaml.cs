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
using Xceed.Wpf.Toolkit;

namespace WorkPartner
{
    public partial class ClosetPage : UserControl
    {
        private readonly string _settingsFilePath = DataManager.SettingsFilePath;
        private readonly string _itemsDbFilePath = DataManager.ItemsDbFilePath;
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
                // [수정] 어떤 MessageBox를 사용할지 명확히 지정합니다. (System.Windows.MessageBox)
                System.Windows.MessageBox.Show("아이템 데이터베이스 파일(items_db.json)을 찾을 수 없습니다.", "오류");
                _fullShopInventory = new List<ShopItem>();
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
                    // [수정] 어떤 MessageBox를 사용할지 명확히 지정합니다. (System.Windows.MessageBox)
                    System.Windows.MessageBox.Show("아직 보유하지 않은 아이템입니다. 상점에서 먼저 구매해주세요!", "알림");
                    return;
                }

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

        private bool IsColorCategory(ItemType type)
        {
            return type == ItemType.HairColor ||
                   type == ItemType.EyeColor ||
                   type == ItemType.ClothesColor ||
                   type == ItemType.CushionColor;
        }

        private void UpdateCharacterPreview()
        {
            // UserControl의 public 메서드를 호출하여 캐릭터를 새로고침합니다.
            CharacterPreviewControl.UpdateCharacter();
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
