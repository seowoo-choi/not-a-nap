namespace NotANap.Core
{
    /// <summary>
    /// 아기의 "시계". 마지막 수유·기저귀 교체로부터 얼마나 지났는지, 얼마나 깨어
    /// 있었는지를 읽는다. 밤 돌봄의 실제 판단은 진정도 같은 절대 수치보다
    /// 이 경과 시간으로 이루어진다. 상태를 바꾸지 않는 순수 조회다.
    /// </summary>
    public static class BabyClockResolver
    {
        public static bool IsAsleep(V2NightState v2)
            => v2.SleepCycle.Stage == V2SleepStage.RemActiveSleep ||
               v2.SleepCycle.Stage == V2SleepStage.NremDeepSleep;

        /// <summary>연속으로 깨어 있는 분. 자는 동안은 0이다.</summary>
        public static int AwakeMinutes(V2NightState v2)
            => IsAsleep(v2) ? 0 : System.Math.Max(0, v2.ElapsedMinutes - v2.AwakeSinceMinute);

        public static int MinutesSinceFeed(V2NightState v2)
            => System.Math.Max(0, v2.ElapsedMinutes - v2.LastFeedMinute);

        public static int MinutesSinceDiaperChange(V2NightState v2)
            => System.Math.Max(0, v2.ElapsedMinutes - v2.LastDiaperChangeMinute);

        public static FatigueSignalStage GetFatigueStage(int awakeMinutes, V2BalanceConfig config)
        {
            if (awakeMinutes >= config.FatigueOvertiredMinutes) return FatigueSignalStage.Overtired;
            if (awakeMinutes >= config.FatigueActiveMinutes) return FatigueSignalStage.Active;
            if (awakeMinutes >= config.FatigueEarlyMinutes) return FatigueSignalStage.Early;
            return FatigueSignalStage.None;
        }

        public static FatigueSignalStage GetFatigueStage(V2NightState v2, GameBalanceConfig config)
            => GetFatigueStage(AwakeMinutes(v2), config.V2);

        /// <summary>
        /// 과각성이면 안기·토닥임의 진정 폭이 줄어든다. 피곤 신호를 제때 읽고
        /// 재우는 편이 이득이 되게 만드는 유일한 규칙이므로 여기 한 곳에서만 준다.
        /// </summary>
        public static double ComfortMultiplier(NightState night, GameBalanceConfig config)
            => GetFatigueStage(night.V2, config) == FatigueSignalStage.Overtired
                ? config.V2.OvertiredComfortMultiplier : 1;
    }
}
