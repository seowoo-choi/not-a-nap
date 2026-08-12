namespace NotANap.Core
{
    /// <summary>기질 3종. 수치 원본: prototype TEMPERAMENTS.</summary>
    public sealed class Temperament
    {
        public string Id { get; }
        public string Name { get; }
        /// <summary>소리 민감도.</summary>
        public double Sens { get; }
        /// <summary>시간당 허기 증가 기준치.</summary>
        public double HungerRate { get; }
        /// <summary>안기 요구도.</summary>
        public double HoldNeed { get; }
        /// <summary>침대 눕히기 민감도 (눕히기 성공 확률 감소).</summary>
        public double CribSens { get; }
        /// <summary>기본 자기 진정력.</summary>
        public double SelfSoothe { get; }
        /// <summary>수유 성공 시 진정 보너스.</summary>
        public double FeedBonus { get; }
        public string Hint { get; }

        private Temperament(string id, string name, double sens, double hungerRate, double holdNeed,
                            double cribSens, double selfSoothe, double feedBonus, string hint)
        {
            Id = id; Name = name; Sens = sens; HungerRate = hungerRate; HoldNeed = holdNeed;
            CribSens = cribSens; SelfSoothe = selfSoothe; FeedBonus = feedBonus; Hint = hint;
        }

        public static readonly Temperament Soft = new Temperament(
            "soft", "반응이 잔잔한", 0.2, 9, 0.3, 0.10, 0.45, 0,
            "웬만한 소리에는 꿈쩍도 안 한다. 이 밤의 몇 안 되는 행운.");

        public static readonly Temperament Sensitive = new Temperament(
            "sensitive", "자극에 민감한", 0.8, 10, 0.7, 0.32, 0.10, 0,
            "저 멀리 나는 소리에도 온몸을 움찔한다. 발소리부터 죽여야 한다.");

        public static readonly Temperament Hungry = new Temperament(
            "hungry", "배고픔 신호가 빠른", 0.4, 16, 0.4, 0.18, 0.25, 14,
            "입을 오물거리며 자꾸 뭔가를 찾는다. 배가 비면 곧바로 티가 난다.");

        public static readonly Temperament[] All = { Soft, Sensitive, Hungry };
    }
}
