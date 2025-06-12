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

            // [수정] 이 페이지가 화면에 보일 때마다 최신 코인 정보를 불러오도록 합니다.
            // UserControl에는 Window의 IsVisibleChanged 이벤트가 없으므로,
            // 이벤트를 MainWindow에서 관리하도록 로직을 변경합니다.
            // 여기서는 페이지가 로드될 때 한 번만 설정을 불러옵니다.
            this.Loaded += (s, e) => LoadSettings();
        }

        // [수정] 외부(MainWindow)에서 호출할 수 있도록 public으로 변경합니다.
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

        private void LoadShopInventory()
        {
            _shopInventory = new List<ShopItem>
            {
                new ShopItem { Id = Guid.NewGuid(), Name = "빨간 후드티", Price = 100, Type = ItemType.Clothes },
                new ShopItem { Id = Guid.NewGuid(), Name = "노란 모자", Price = 50, Type = ItemType.Hat },
                new ShopItem { Id = Guid.NewGuid(), Name = "숲 속 배경", Price = 200, Type = ItemType.Background },
                new ShopItem { Id = Guid.NewGuid(), Name = "파란 비니", Price = 70, Type = ItemType.Hat }
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
