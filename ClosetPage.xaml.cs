// ClosetPage.xaml.cs (새 C# 파일)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WorkPartner
{
    public partial class ClosetPage : UserControl
    {
        private readonly string _settingsFilePath = "app_settings.json";
        private AppSettings _settings;
        private List<ShopItem> _fullShopInventory; // 꾸미기 아이템 전체 목록

        private bool _isSliderUpdate = false; // 슬라이더 값 변경 시 무한 루프 방지용


        public ClosetPage()
        {
            InitializeComponent();
        }

        // MainWindow에서 페이지를 보여주기 전에 호출할 함수입니다.
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

        // 상점에 있는 모든 꾸미기 아이템 목록을 불러옵니다. (임시 데이터)
        private void LoadFullInventory()
        {
            // TODO: 나중에 이 목록을 별도의 DB나 파일에서 불러오도록 수정해야 합니다.
            _fullShopInventory = new List<ShopItem>
            {
                // 머리 스타일
                new ShopItem { Name = "기본 머리", Price = 0, Type = ItemType.HairStyle, ImagePath="LightBlue" },
                new ShopItem { Name = "긴 머리", Price = 100, Type = ItemType.HairStyle, ImagePath="Blue" },
                // 머리 색
                new ShopItem { Name = "검은색", Price = 10, Type = ItemType.HairColor, ImagePath="Black" },
                new ShopItem { Name = "갈색", Price = 10, Type = ItemType.HairColor, ImagePath="Brown" },
                // 옷
                new ShopItem { Name = "기본 옷", Price = 0, Type = ItemType.Clothes, ImagePath="Gray" },
                new ShopItem { Name = "후드티", Price = 150, Type = ItemType.Clothes, ImagePath="DarkGray" },
                // 눈 모양
                new ShopItem { Name = "기본 눈", Price = 0, Type = ItemType.EyeShape, ImagePath="SlateGray" },
                new ShopItem { Name = "웃는 눈", Price = 50, Type = ItemType.EyeShape, ImagePath="LightSlateGray" },
            };
        }

        // 아이템 카테고리 목록을 채웁니다.
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

                // [수정] 색상을 다루는 카테고리(HairColor, EyeColor 등)를 선택했을 때만
                // 커스텀 색상 UI를 보여줍니다.
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


        // 아이템 버튼을 클릭했을 때 착용/해제하는 로직입니다.
        private void ItemButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Guid itemId)
            {
                var clickedItem = _fullShopInventory.FirstOrDefault(item => item.Id == itemId);
                if (clickedItem == null) return;

                // TODO: 아직 구매하지 않은 아이템은 상점으로 보내는 로직 추가 가능
                // if (!_settings.OwnedItemIds.Contains(itemId)) { ... }

                // 같은 부위에 이미 다른 아이템을 착용중이고, 그 아이템이 지금 클릭한 아이템이라면 -> 착용 해제
                if (_settings.EquippedItems.ContainsKey(clickedItem.Type) && _settings.EquippedItems[clickedItem.Type] == itemId)
                {
                    _settings.EquippedItems.Remove(clickedItem.Type);
                }
                else // 새로운 아이템 착용
                {
                    _settings.EquippedItems[clickedItem.Type] = itemId;
                }

                SaveSettings();
                UpdateCharacterPreview();
            }
        }

        // 현재 착용한 아이템들을 바탕으로 캐릭터 미리보기를 업데이트합니다.
        private void UpdateCharacterPreview()
        {
            CharacterPreviewGrid.Children.Clear();
            // ... (기존 미리보기 로직은 거의 그대로 유지)

            // [수정] 셰이더 효과를 적용할 때, 커스텀 색상이 있는지 먼저 확인합니다.
            if (hairStyleItem != null)
            {
                var hairImage = new Image { /* ... */ };
                var tintEffect = new TintColorEffect();

                // 1. AppSettings에 저장된 커스텀 머리색이 있다면 그것을 최우선으로 적용
                if (_settings.CustomColors.ContainsKey(ItemType.HairColor))
                {
                    tintEffect.TintColor = (Color)ColorConverter.ConvertFromString(_settings.CustomColors[ItemType.HairColor]);
                }
                // 2. 커스텀 색이 없다면, 장착된 아이템의 색을 적용
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
            // 눈, 옷 등 다른 색상 파츠도 위와 동일한 방식으로 로직을 확장할 수 있습니다.
        }

        #region 커스텀 색상 관련 함수

        // [추가] 색상 슬라이더 값이 변경될 때마다 호출되는 이벤트 핸들러
        private void ColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSliderUpdate || !(CategoryListBox.SelectedItem is ItemType selectedType)) return;

            // 현재 선택된 카테고리가 색상 카테고리가 아니면 무시
            if (!IsColorCategory(selectedType)) return;

            // 슬라이더 값으로 새로운 색상 생성
            Color newColor = Color.FromRgb((byte)SliderR.Value, (byte)SliderG.Value, (byte)SliderB.Value);

            // AppSettings에 커스텀 색상 저장 (Hex 코드 형태)
            _settings.CustomColors[selectedType] = newColor.ToString();
            SaveSettings();

            // 캐릭터 미리보기 실시간 업데이트
            UpdateCharacterPreview();
        }

        // [추가] 현재 선택된 카테고리의 커스텀 색상 값을 슬라이더에 로드하는 함수
        private void LoadCustomColorToSliders(ItemType type)
        {
            _isSliderUpdate = true; // 무한 루프 방지 플래그 설정
            if (_settings.CustomColors.ContainsKey(type))
            {
                var color = (Color)ColorConverter.ConvertFromString(_settings.CustomColors[type]);
                SliderR.Value = color.R;
                SliderG.Value = color.G;
                SliderB.Value = color.B;
            }
            else // 저장된 커스텀 색이 없으면 흰색으로 초기화
            {
                SliderR.Value = 255;
                SliderG.Value = 255;
                SliderB.Value = 255;
            }
            _isSliderUpdate = false; // 플래그 해제
        }

        // [추가] 주어진 ItemType이 색상을 다루는 카테고리인지 확인하는 도우미 함수
        private bool IsColorCategory(ItemType type)
        {
            return type == ItemType.HairColor ||
                   type == ItemType.EyeColor ||
                   type == ItemType.ClothesColor ||
                   type == ItemType.CushionColor;
        }

        #endregion

    }
}
