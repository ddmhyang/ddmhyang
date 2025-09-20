using System;

namespace WorkPartner
{
    public enum ItemType
    {
        // 얼굴 및 머리
        HairColor,
        HairStyle,
        EyeShape,
        EyeColor,
        MouthShape,
        FaceDeco1,
        FaceDeco2,
        FaceDeco3,
        FaceDeco4,

        // 의류 및 장신구
        Clothes,
        ClothesColor,
        AnimalEar,
        AnimalTail,
        Accessory1,
        Accessory2,
        Accessory3,

        // 배경
        Cushion,
        CushionColor,
        Background
    }

    public class ShopItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Price { get; set; }
        public ItemType Type { get; set; }

        // 이미지 파일의 경로를 저장합니다.
        public string ImagePath { get; set; }

        // [추가] 색상 아이템의 실제 색상 값(Hex 코드)을 저장합니다.
        public string ColorValue { get; set; }

        public ShopItem()
        {
            Id = Guid.NewGuid();
        }
    }
}
