using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WorkPartner
{
    public class ShopItemViewModel : INotifyPropertyChanged
    {
        public ShopItem Item { get; set; }
        private bool _isOwned;
        public bool IsOwned
        {
            get => _isOwned;
            set { _isOwned = value; OnPropertyChanged(nameof(IsOwned)); }
        }

        private bool _isEquipped;
        public bool IsEquipped
        {
            get => _isEquipped;
            set { _isEquipped = value; OnPropertyChanged(nameof(IsEquipped)); }
        }

        public string Name => Item.Name;
        public int Price => Item.Price;
        public string ImagePath => Item.ImagePath;
        public Guid Id => Item.Id;
        public ItemType Type => Item.Type;
        public string ColorValue => Item.ColorValue;

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void UpdateStatus(AppSettings settings)
        {
            IsOwned = settings.OwnedItemIds.Contains(Id) || Price == 0;
            if (IsColorCategory(Type))
            {
                IsEquipped = settings.CustomColors.ContainsKey(Type) && settings.CustomColors[Type] == ColorValue;
            }
            else
            {
                IsEquipped = settings.EquippedItems.ContainsKey(Type) && settings.EquippedItems[Type] == Id;
            }

            OnPropertyChanged(nameof(IsOwned));
            OnPropertyChanged(nameof(IsEquipped));
        }

        private bool IsColorCategory(ItemType type)
        {
            return type.ToString().Contains("Color");
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorString)
            {
                try
                {
                    return (SolidColorBrush)new BrushConverter().ConvertFromString(colorString);
                }
                catch
                {
                    return Brushes.Transparent;
                }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class AvatarCustomizationPage : UserControl
    {
        private AppSettings _settings;
        private List<ShopItem> _fullShopInventory;
        private Dictionary<ItemType, List<ShopItemViewModel>> _itemsByCategory = new Dictionary<ItemType, List<ShopItemViewModel>>();

        public AvatarCustomizationPage()
        {
            InitializeComponent();
            this.Loaded += (s, e) => LoadData(); // Re-load data every time the page is shown
        }

        public void LoadData()
        {
            LoadSettings();
            LoadFullInventory();
            UpdateCharacterInfo();
            PopulateTabs();
            UpdateCharacterPreview();
        }

        private void LoadSettings()
        {
            _settings = DataManager.LoadSettings();
        }

        private void SaveSettings()
        {
            DataManager.SaveSettingsAndNotify(_settings);
        }

        private void LoadFullInventory()
        {
            _itemsByCategory.Clear(); // Clear old data

            if (File.Exists(DataManager.ItemsDbFilePath))
            {
                var json = File.ReadAllText(DataManager.ItemsDbFilePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                _fullShopInventory = JsonSerializer.Deserialize<List<ShopItem>>(json, options) ?? new List<ShopItem>();
            }
            else
            {
                MessageBox.Show($"아이템 데이터베이스 파일({DataManager.ItemsDbFilePath})을 찾을 수 없습니다.", "오류");
                _fullShopInventory = new List<ShopItem>();
                return;
            }

            // Group items and create ViewModels
            foreach (var item in _fullShopInventory)
            {
                if (!_itemsByCategory.ContainsKey(item.Type))
                {
                    _itemsByCategory[item.Type] = new List<ShopItemViewModel>();
                }
                var vm = new ShopItemViewModel { Item = item };
                vm.UpdateStatus(_settings);
                _itemsByCategory[item.Type].Add(vm);
            }
        }

        private void PopulateTabs()
        {
            var selectedTab = CategoryTabControl.SelectedItem as TabItem;
            string selectedHeader = selectedTab?.Header.ToString();

            CategoryTabControl.Items.Clear();

            var categories = Enum.GetValues(typeof(ItemType))
                                 .Cast<ItemType>()
                                 .Where(t => !IsColorCategory(t)) // Exclude color types from main tabs
                                 .ToList();

            // Add color tab first
            var colorTab = new TabItem { Header = "색깔 꾸미기" };
            colorTab.Content = CreateColorPickerPanel();
            CategoryTabControl.Items.Add(colorTab);

            foreach (var category in categories)
            {
                TabItem tabItem = new TabItem { Header = category.ToString() };
                var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                var itemsPanel = new WrapPanel { Orientation = Orientation.Horizontal };

                scrollViewer.Content = itemsPanel;
                tabItem.Content = scrollViewer;

                if (_itemsByCategory.TryGetValue(category, out var items))
                {
                    foreach (var itemViewModel in items)
                    {
                        var button = new Button
                        {
                            DataContext = itemViewModel,
                            Tag = itemViewModel,
                            Style = (Style)this.FindResource("ItemButtonStyle")
                        };
                        button.Click += ItemButton_Click;
                        itemsPanel.Children.Add(button);
                    }
                }
                CategoryTabControl.Items.Add(tabItem);
            }

            // Restore previously selected tab if possible
            if (!string.IsNullOrEmpty(selectedHeader))
            {
                var tabToSelect = CategoryTabControl.Items.OfType<TabItem>().FirstOrDefault(t => t.Header.ToString() == selectedHeader);
                if (tabToSelect != null)
                {
                    CategoryTabControl.SelectedItem = tabToSelect;
                }
            }
            else if (CategoryTabControl.Items.Count > 0)
            {
                CategoryTabControl.SelectedIndex = 0;
            }
        }

        private FrameworkElement CreateColorPickerPanel()
        {
            var mainPanel = new StackPanel { Margin = new Thickness(20) };

            CreateColorSelector(mainPanel, "머리 색상", ItemType.HairColor);
            CreateColorSelector(mainPanel, "옷 색상", ItemType.ClothesColor);
            CreateColorSelector(mainPanel, "눈 색상", ItemType.EyeColor);
            CreateColorSelector(mainPanel, "방석 색상", ItemType.CushionColor);

            return mainPanel;
        }

        private void CreateColorSelector(Panel parentPanel, string title, ItemType type)
        {
            var group = new GroupBox { Header = title, Margin = new Thickness(0, 0, 0, 15) };
            var panel = new WrapPanel();
            group.Content = panel;
            if (_itemsByCategory.TryGetValue(type, out var colors))
            {
                foreach (var colorItem in colors)
                {
                    var colorButton = new Button
                    {
                        DataContext = colorItem,
                        Tag = colorItem,
                        Style = (Style)this.FindResource("ColorButtonStyle")
                    };
                    colorButton.Click += ColorButton_Click;
                    panel.Children.Add(colorButton);
                }
            }
            parentPanel.Children.Add(group);
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.Tag is ShopItemViewModel itemViewModel)) return;

            var item = itemViewModel.Item;

            if (!itemViewModel.IsOwned)
            {
                if (_settings.Coins >= item.Price)
                {
                    var result = MessageBox.Show($"'{item.Name}' 색상을 {item.Price} 코인으로 구매하시겠습니까?", "구매 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        _settings.Coins -= item.Price;
                        _settings.OwnedItemIds.Add(item.Id);
                        itemViewModel.IsOwned = true;
                        SoundPlayer.PlayPurchaseSound();
                        UpdateCharacterInfo(); // Update coin display
                        SaveSettings();
                    }
                    else
                    {
                        return; // Don't proceed if purchase is cancelled
                    }
                }
                else
                {
                    MessageBox.Show("코인이 부족합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Unequip if already selected
            if (_settings.CustomColors.ContainsKey(item.Type) && _settings.CustomColors[item.Type] == item.ColorValue)
            {
                _settings.CustomColors.Remove(item.Type);
            }
            else // Equip new color
            {
                _settings.CustomColors[item.Type] = item.ColorValue;
            }

            SaveSettings();
            UpdateCharacterPreview();
            RefreshItemStatuses();
        }

        private void ItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as Button)?.Tag is ShopItemViewModel itemViewModel)) return;

            var clickedItem = itemViewModel.Item;

            if (IsColorCategory(clickedItem.Type)) return;

            if (!itemViewModel.IsOwned)
            {
                if (_settings.Coins >= clickedItem.Price)
                {
                    var result = MessageBox.Show($"'{clickedItem.Name}' 아이템을 {clickedItem.Price} 코인으로 구매하시겠습니까?", "구매 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        _settings.Coins -= clickedItem.Price;
                        _settings.OwnedItemIds.Add(clickedItem.Id);
                        itemViewModel.IsOwned = true;
                        SoundPlayer.PlayPurchaseSound();
                        UpdateCharacterInfo(); // Update coin display
                    }
                    else
                    {
                        return; // Don't equip if not purchased
                    }
                }
                else
                {
                    MessageBox.Show("코인이 부족합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Equip/Unequip logic
            var itemType = clickedItem.Type;
            if (_settings.EquippedItems.ContainsKey(itemType) && _settings.EquippedItems[itemType] == clickedItem.Id)
            {
                _settings.EquippedItems.Remove(itemType); // Unequip
            }
            else
            {
                _settings.EquippedItems[itemType] = clickedItem.Id; // Equip
            }

            SaveSettings();
            UpdateCharacterPreview();
            RefreshItemStatuses();
        }

        private void RefreshItemStatuses()
        {
            foreach (var categoryList in _itemsByCategory.Values)
            {
                foreach (var vm in categoryList)
                {
                    vm.UpdateStatus(_settings);
                }
            }
        }

        private void CategoryTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Not needed for now
        }

        private void UpdateCharacterPreview()
        {
            CharacterPreviewControl.UpdateCharacter();
        }

        private void UpdateCharacterInfo()
        {
            if (_settings != null)
            {
                UsernameTextBlock.Text = _settings.Username ?? "User";
                LevelTextBlock.Text = $"Level: {_settings.Level}";
                CoinsTextBlock.Text = $"💰 {_settings.Coins}";
            }
        }

        private bool IsColorCategory(ItemType type)
        {
            return type.ToString().Contains("Color");
        }
    }
}

