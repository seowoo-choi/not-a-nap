namespace NotANap.Core
{
    /// <summary>
    /// 아이템 5종 정의. 설명은 "더 빨리 진정시킵니다" 같은 형용사가 아니라
    /// 엔진이 실제로 적용하는 수치를 그대로 적는다. 문구와 판정이 어긋나면
    /// 플레이어는 아이템을 고를 근거를 잃는다.
    /// </summary>
    public sealed class ItemDef
    {
        public ItemId Id { get; }
        public string Name { get; }
        public string Emoji { get; }
        /// <summary>한 줄 정체성. 이 물건이 무엇을 하는 물건인지.</summary>
        public string Role { get; }
        /// <summary>엔진 수치 그대로의 효과 목록.</summary>
        public string[] Effects { get; }
        /// <summary>사용 비용(시간·체력·횟수). 없으면 빈 문자열.</summary>
        public string Cost { get; }
        /// <summary>대가와 습관 위험.</summary>
        public string Side { get; }

        private ItemDef(ItemId id, string name, string emoji, string role,
            string[] effects, string cost, string side)
        {
            Id = id; Name = name; Emoji = emoji;
            Role = role; Effects = effects; Cost = cost; Side = side;
        }

        public static readonly ItemDef Carrier = new ItemDef(ItemId.Carrier, "아기띠", "🎒",
            "안은 채로 두 손을 비우는 물건",
            new[] { "안고 준비할 때 체력 -5 → 0", "이동·준비 중 울음 상승 45% 감소" },
            "매고 푸는 데 시간 0분",
            "15분마다 아기띠 수면 습관 누적 · 백일째 밤 버클 고장 위험");

        public static readonly ItemDef Pacifier = new ItemDef(ItemId.Pacifier, "쪽쪽이", "🍭",
            "즉시 진정도를 올리는 물건",
            new[] { "진정 +12 (좋아하는 아기는 +22)", "쪽쪽이를 거부하는 아기에게는 실패" },
            "1회 15분 · 체력 -1 · 밤 3회",
            "각성 원인 자체는 사라지지 않아 다시 깬다");

        public static readonly ItemDef Noise = new ItemDef(ItemId.Noise, "백색소음기", "🔊",
            "달래는 물건이 아니라 든 잠을 이어 주는 물건",
            new[] { "진정 효과 0", "잠든 뒤 다음 각성까지 +25분", "외부 소음 각성 차단 60%" },
            "켜고 끄는 데 시간 0분",
            "쓴 시간이 쌓이면 익숙해져 두 효과 모두 감소 · 백일째 밤 배터리 방전 위험");

        public static readonly ItemDef Bouncer = new ItemDef(ItemId.Bouncer, "바운서", "🪑",
            "침대의 아기를 체력 없이 달래는 물건 (LEGACY · V2 선택 불가)",
            new[] { "턴마다 진정 +9 · 보호자 체력 소모 0", "예민한 기질(Sens > 0.6)에게는 진정 -6" },
            "태우고 내리는 데 시간 0분",
            "안고 있는 동안에는 쓸 수 없다");

        public static readonly ItemDef Monitor = new ItemDef(ItemId.Monitor, "베이비 모니터", "📟",
            "아기 곁을 떠난 동안 아기 상태를 여는 유일한 물건",
            new[]
            {
                "아기방 밖에서 기분·수면 단계·울음 확인",
                "한 번 보면 30분 동안 상태가 계속 보인다",
                "없으면 주방·욕실에서 아기 상태를 전혀 알 수 없다"
            },
            "1회 2분 · 체력 -1",
            "아기 곁에 있을 때는 쓸 수 없다 (직접 보면 되니까)");

        public static readonly ItemDef[] All = { Carrier, Pacifier, Noise, Bouncer, Monitor };

        public static ItemDef Get(ItemId id)
        {
            foreach (var item in All)
                if (item.Id == id) return item;
            throw new System.ArgumentOutOfRangeException(nameof(id), id, "정의되지 않은 아이템");
        }
    }
}
