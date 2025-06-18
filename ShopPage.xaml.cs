using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace WorkPartner
{
    public partial class ShopPage : UserControl
    {
        private readonly string _settingsFilePath = "app_settings.json";
        private readonly string _itemsDbFilePath = "items_db.json"; // [추가] 아이템 DB 파일 경로
        private AppSettings _settings;
        private List<ShopItem> _shopInventory;

        public ShopPage()
        {
            InitializeComponent();
            LoadShopInventory();
            this.Loaded += (s, e) => LoadSettings();
        }

        public void LoadSettings()
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

        // [수정] 하드코딩된 목록 대신, items_db.json 파일을 읽어와 상점 인벤토리를 구성합니다.
        private void LoadShopInventory()
        {
            if (File.Exists(_itemsDbFilePath))
            {
                var json = File.ReadAllText(_itemsDbFilePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                var allItems = JsonSerializer.Deserialize<List<ShopItem>>(json, options) ?? new List<ShopItem>();

                // 상점에서는 가격이 0보다 큰 아이템, 즉 판매용 아이템만 보여줍니다.
                _shopInventory = allItems.Where(item => item.Price > 0).ToList();
            }
            else
            {
                System.Windows.MessageBox.Show("아이템 데이터베이스 파일(items_db.json)을 찾을 수 없습니다.", "오류");
                _shopInventory = new List<ShopItem>();
            }

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
                    System.Windows.MessageBox.Show("이미 보유하고 있는 아이템입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (_settings.Coins >= itemToBuy.Price)
                {
                    if (System.Windows.MessageBox.Show($"{itemToBuy.Name} 아이템을 {itemToBuy.Price} 코인으로 구매하시겠습니까?", "구매 확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        _settings.Coins -= itemToBuy.Price;
                        _settings.OwnedItemIds.Add(itemId);
                        SaveSettings();
                        SoundPlayer.PlayPurchaseSound();
                        System.Windows.MessageBox.Show("구매가 완료되었습니다!", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("코인이 부족합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
