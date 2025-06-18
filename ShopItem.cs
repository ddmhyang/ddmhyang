using System;

namespace WorkPartner
{
    // [수정] 아이템의 종류를 커스터마이징 목록에 맞게 세분화합니다.
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
        Background // 기존 상점 아이템과의 호환을 위해 유지
    }

    public class ShopItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Price { get; set; }
        public ItemType Type { get; set; }

        // 나중에 실제 이미지 파일 경로를 연결할 속성입니다.
        // 예: "Images/Hair/style_01.png"
        public string ImagePath { get; set; }

        public ShopItem()
        {
            Id = Guid.NewGuid();
        }
    }
}
