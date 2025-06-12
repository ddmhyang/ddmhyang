using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace WorkPartner
{
    public partial class ShopPage : UserControl
    {
        private readonly string _settingsFilePath = "app_settings.json";
        private AppSettings _settings;
        private List<ShopItem> _shopInventory;

        public ShopPage()
        {
            InitializeComponent();
            LoadShopInventory();
            LoadSettings();
            this.IsVisibleChanged += (s, e) => { if (e.NewValue is true) LoadSettings(); };
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

        // 임시로 코드에서 상점 아이템 목록을 생성합니다. (나중에는 파일에서 불러올 수 있습니다)
        private void LoadShopInventory()
        {
            _shopInventory = new List<ShopItem>
            {
                new ShopItem { Name = "빨간 후드티", Price = 100, Type = ItemType.Clothes },
                new ShopItem { Name = "노란 모자", Price = 50, Type = ItemType.Hat },
                new ShopItem { Name = "숲 속 배경", Price = 200, Type = ItemType.Background },
                new ShopItem { Name = "파란 비니", Price = 70, Type = ItemType.Hat }
            };
            ShopItemsListView.ItemsSource = _shopInventory;
        }

        private void BuyButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Guid itemId)
            {
                var itemToBuy = _shopInventory.Find(item => item.Id == itemId);
                if (itemToBuy == null) return;

                if (_settings.OwnedItemIds.Contains(itemId))
                {
                    MessageBox.Show("이미 보유하고 있는 아이템입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (_settings.Coins >= itemToBuy.Price)
                {
                    if (MessageBox.Show($"{itemToBuy.Name} 아이템을 {itemToBuy.Price} 코인으로 구매하시겠습니까?", "구매 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        _settings.Coins -= itemToBuy.Price;
                        _settings.OwnedItemIds.Add(itemId);
                        SaveSettings();
                        MessageBox.Show("구매가 완료되었습니다!", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    MessageBox.Show("코인이 부족합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
