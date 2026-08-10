namespace NotANap.Core
{
    /// <summary>
    /// 백색소음기의 결정론적 효과. 이 물건은 아기를 달래지 않는다.
    /// 외부 소리를 덮어 이미 든 잠을 이어 주는 것이 전부이며,
    /// 매일 켜면 아기가 그 소리에 익숙해져(NoiseHab) 효과가 줄어든다.
    /// </summary>
    public static class NoiseMachine
    {
        public static bool IsActive(NightState night)
            => night != null && night.Wearing.Noise && night.HasItem(ItemId.Noise) && !night.NoiseDisabled;

        /// <summary>0~1. 습관화가 진행될수록 0에 가까워진다.</summary>
        public static double Effectiveness(NightState night)
            => IsActive(night) ? 1 - CoreMath.Clamp01(night.V2?.NoiseHabituation ?? 0) : 0;

        /// <summary>다음 각성까지 늘어나는 분. 잠들어 있을 때만 의미가 있다.</summary>
        public static int WakeDelayBonusMinutes(NightState night, GameBalanceConfig config)
            => (int)System.Math.Round(config.V2.NoiseWakeDelayBonusMinutes * Effectiveness(night));

        /// <summary>
        /// 지금 켠다면 받게 될 각성 지연. 꺼져 있을 때 버튼에 "+0분"이 뜨면
        /// 이 물건이 아무 일도 안 하는 것처럼 읽히므로 표시에는 이쪽을 쓴다.
        /// </summary>
        public static int PotentialWakeDelayBonusMinutes(NightState night, GameBalanceConfig config)
            => (int)System.Math.Round(config.V2.NoiseWakeDelayBonusMinutes *
                (1 - CoreMath.Clamp01(night?.V2?.NoiseHabituation ?? 0)));

        /// <summary>외부 소음이 아기를 깨우지 못할 확률.</summary>
        public static double ExternalWakeGuard(NightState night, GameBalanceConfig config)
            => config.V2.NoiseExternalWakeGuard * Effectiveness(night);
    }

    /// <summary>
    /// 아기 기분(0~100). 보호자의 '집중력'을 대신해 화면에 서는 수치이며
    /// 진정도·울음 세기·배고픔에서 결정론적으로 파생된다. 상태를 바꾸지 않는다.
    /// </summary>
    public static class BabyMoodResolver
    {
        public static double Evaluate(NightState night, GameBalanceConfig config)
        {
            if (night?.V2 == null) return CoreMath.Clamp(night?.Baby.Calm ?? 0, 0, 100);
            double hungerOver = System.Math.Max(0, night.Baby.Hunger - config.V2.HungerEarlyThreshold);
            return CoreMath.Clamp(
                night.Baby.Calm
                - night.V2.CryIntensity * config.V2.MoodCryWeight
                - hungerOver * config.V2.MoodHungerWeight,
                0, 100);
        }

        public static string Label(double mood)
        {
            if (mood >= 80) return "아주 좋음";
            if (mood >= 60) return "좋음";
            if (mood >= 40) return "보통";
            if (mood >= 20) return "나쁨";
            return "아주 나쁨";
        }
    }
}
