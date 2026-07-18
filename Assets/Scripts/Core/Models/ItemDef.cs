namespace NotANap.Core
{
    /// <summary>아이템 5종 정의. 원본: prototype ITEMS.</summary>
    public sealed class ItemDef
    {
        public ItemId Id { get; }
        public string Name { get; }
        public string Emoji { get; }
        public string Desc { get; }
        public string Side { get; }

        private ItemDef(ItemId id, string name, string emoji, string desc, string side)
        {
            Id = id; Name = name; Emoji = emoji; Desc = desc; Side = side;
        }

        public static readonly ItemDef Carrier = new ItemDef(ItemId.Carrier, "아기띠", "🎒",
            "착용하면 계속 안은 상태가 됩니다. 진정 효과가 크고 잠들기 쉬워집니다.",
            "매 시간 체력 소모. 반복 사용 시 아기가 습관으로 학습합니다.");

        public static readonly ItemDef Pacifier = new ItemDef(ItemId.Pacifier, "쪽쪽이", "🍭",
            "즉시 진정 (밤당 3회, 시간 소모 없음).",
            "선잠 중 빠지면 아기가 깰 수 있습니다.");

        public static readonly ItemDef Noise = new ItemDef(ItemId.Noise, "백색소음기", "🔊",
            "켜두면 매 시간 진정 + 소음 이벤트를 막아줍니다.",
            "매일 반복하면 익숙해져 효과가 줄어듭니다.");

        public static readonly ItemDef Bouncer = new ItemDef(ItemId.Bouncer, "바운서", "🪑",
            "내려놓은 아기를 체력 소모 없이 달래줍니다.",
            "자극에 민감한 아기에게는 역효과.");

        public static readonly ItemDef Monitor = new ItemDef(ItemId.Monitor, "베이비 모니터", "📟",
            "아기의 상태 수치를 정확히 보여줍니다.",
            "진정 효과는 없습니다. 정보가 곧 무기.");

        public static readonly ItemDef[] All = { Carrier, Pacifier, Noise, Bouncer, Monitor };

        public static ItemDef Get(ItemId id)
        {
            foreach (var item in All)
                if (item.Id == id) return item;
            throw new System.ArgumentOutOfRangeException(nameof(id), id, "정의되지 않은 아이템");
        }
    }
}
