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
            "품에 안긴 상태를 유지해 더 빨리 진정시킵니다.",
            "시간마다 체력 소모 · 반복하면 아기띠 수면 습관이 생깁니다.");

        public static readonly ItemDef Pacifier = new ItemDef(ItemId.Pacifier, "쪽쪽이", "🍭",
            "즉시 진정 · 밤당 3회 · 시간 소모 없음",
            "선잠에 빠지면 다시 깰 수 있습니다.");

        public static readonly ItemDef Noise = new ItemDef(ItemId.Noise, "백색소음기", "🔊",
            "켜 둔 동안 아기를 달래고 소음 돌발 상황을 막습니다.",
            "반복할수록 익숙해져 진정 효과가 줄어듭니다.");

        public static readonly ItemDef Bouncer = new ItemDef(ItemId.Bouncer, "바운서", "🪑",
            "침대의 아기를 체력 소모 없이 달랩니다.",
            "자극에 민감한 아기에게는 울음이 커집니다.");

        public static readonly ItemDef Monitor = new ItemDef(ItemId.Monitor, "베이비 모니터", "📟",
            "부엌·욕실에서도 아기 상태를 말로 읽어 줍니다.",
            "아기 곁을 떠나 있을 때만 쓸 수 있고, 볼 때마다 2분이 갑니다.");

        public static readonly ItemDef[] All = { Carrier, Pacifier, Noise, Bouncer, Monitor };

        public static ItemDef Get(ItemId id)
        {
            foreach (var item in All)
                if (item.Id == id) return item;
            throw new System.ArgumentOutOfRangeException(nameof(id), id, "정의되지 않은 아이템");
        }
    }
}
