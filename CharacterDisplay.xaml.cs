// CharacterDisplay.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WorkPartner
{
    public partial class CharacterDisplay : UserControl
    {
        private readonly string _settingsFilePath = "app_settings.json";
        private readonly string _itemsDbFilePath = "items_db.json";
        private AppSettings _settings;
        private List<ShopItem> _fullShopInventory;

        public CharacterDisplay()
        {
            InitializeComponent();
            // UserControl이 화면에 보일 때마다 캐릭터를 자동으로 업데이트합니다.
            this.IsVisibleChanged += (s, e) =>
            {
                if ((bool)e.NewValue)
                {
                    UpdateCharacter();
                }
            };
        }

        // 외부에서 캐릭터를 새로고침할 때 호출할 public 메서드
        public void UpdateCharacter()
        {
            LoadData();
            RenderCharacter();
        }

        private void LoadData()
        {
            // AppSettings 로드
            if (File.Exists(_settingsFilePath))
            {
                var settingsJson = File.ReadAllText(_settingsFilePath);
                _settings = JsonSerializer.Deserialize<AppSettings>(settingsJson) ?? new AppSettings();
            }
            else { _settings = new AppSettings(); }

            // 아이템 DB 로드
            if (File.Exists(_itemsDbFilePath))
            {
                var itemsJson = File.ReadAllText(_itemsDbFilePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                _fullShopInventory = JsonSerializer.Deserialize<List<ShopItem>>(itemsJson, options) ?? new List<ShopItem>();
            }
            else { _fullShopInventory = new List<ShopItem>(); }
        }

        // 캐릭터를 그리는 핵심 로직
        private void RenderCharacter()
        {
            if (_settings == null || _fullShopInventory == null) return;

            CharacterGrid.Children.Clear();

            var sortedEquippedItems = _settings.EquippedItems
                .Select(pair => _fullShopInventory.FirstOrDefault(i => i.Id == pair.Value))
                .Where(item => item != null)
                .OrderBy(item => GetZIndex(item.Type));

            ShopItem hairStyleItem = sortedEquippedItems.FirstOrDefault(i => i.Type == ItemType.HairStyle);

            foreach (var item in sortedEquippedItems.Where(i => i.Type != ItemType.HairStyle))
            {
                var partElement = CreateItemVisual(item);
                if (partElement != null)
                {
                    Panel.SetZIndex(partElement, GetZIndex(item.Type));
                    CharacterGrid.Children.Add(partElement);
                }
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
                else { tintEffect.TintColor = Colors.White; }
                hairImage.Effect = tintEffect;
                Panel.SetZIndex(hairImage, GetZIndex(ItemType.HairStyle));
                CharacterGrid.Children.Add(hairImage);
            }
        }

        private FrameworkElement CreateItemVisual(ShopItem item)
        {
            if (string.IsNullOrEmpty(item.ImagePath)) return null;
            try
            {
                return new Image
                {
                    Source = new BitmapImage(new Uri(item.ImagePath, UriKind.RelativeOrAbsolute)),
                    Stretch = Stretch.Uniform
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"이미지 로드 실패: {item.ImagePath}, 오류: {ex.Message}");
                return null;
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
                case ItemType.EyeShape: return 23;
                case ItemType.MouthShape: return 25;
                case ItemType.FaceDeco1: return 30;
                case ItemType.FaceDeco2: return 31;
                case ItemType.FaceDeco3: return 32;
                case ItemType.FaceDeco4: return 33;
                case ItemType.AnimalEar: return 40;
                case ItemType.Accessory1: return 41;
                case ItemType.Accessory2: return 42;
                case ItemType.Accessory3: return 43;
                default: return 5;
            }
        }
    }
}
