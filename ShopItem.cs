using System;

namespace WorkPartner
{
    // 아이템의 종류를 구분하기 위한 열거형 (나중에 더 추가할 수 있습니다)
    public enum ItemType
    {
        Hat,
        Clothes,
        Background
    }

    public class ShopItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int Price { get; set; }
        public ItemType Type { get; set; }

        // 나중에 실제 이미지 파일 경로를 연결할 속성입니다.
        public string ImagePath { get; set; }

        public ShopItem()
        {
            Id = Guid.NewGuid();
        }
    }
}
