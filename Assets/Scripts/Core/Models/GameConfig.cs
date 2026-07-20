namespace NotANap.Core
{
    /// <summary>
    /// 전역 상수. 수치 원본: Reference/prototype.html CONFIG + docs/final-night-spec.md.
    /// 수치·공식을 임의로 바꾸지 말 것 (CLAUDE.md 절대 규칙).
    /// </summary>
    public static class GameConfig
    {
        public const int StartHour = 21;
        public const int EndHour = 6;
        /// <summary>21시 → 06시까지 소비 턴 수.</summary>
        public const int TurnsPerNight = 9;

        public const int PacifierUsesPerNight = 3;
        public const int NormalNightItemSlots = 3;

        // ── 백일째 밤 (docs/final-night-spec.md) ──
        public const int FinalNightItemSlots = 2;
        public const double FinalNightMemoryMultiplier = 1.5;
        /// <summary>습관 표적 이벤트 발동 조건 임계값 (저장 memory 기준).</summary>
        public const double HabitEventThreshold = 0.3;
        /// <summary>1~2일차 noiseTurns 합이 이 값 이상이면 배터리 방전 이벤트 조건 충족.</summary>
        public const int FinalNightNoiseTurnsThreshold = 6;
        /// <summary>방해 이벤트 최대 발동 수 (selfSoothe 보상 패시브는 제외).</summary>
        public const int MaxTargetedInterferenceEvents = 2;
        public const int CarrierBuckleDisabledTurns = 2;
        public const double DawnWakingSleepPenalty = 30;
        public const double DawnWakingLaydownPenalty = 0.1;
        public const double SelfSootheResettleChance = 0.5;

        // ── 승리 조건 (3중 2) ──
        public const double DeepSleepThreshold = 85;
        public const double VictoryStaminaThreshold = 30;
        public const int VictoryConditionsRequired = 2;
    }
}
