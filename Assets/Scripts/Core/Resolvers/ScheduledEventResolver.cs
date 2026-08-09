namespace NotANap.Core
{
    /// <summary>
    /// 일반 예정 이벤트 (1일차 00시 기저귀, 2일차 23시 초인종). 원본: prototype runScheduledEvents().
    /// 프로토타입의 3일차 이벤트는 폐기 — 세 번째 밤은 HundredthNight이며 FinalNightResolver가 담당.
    /// </summary>
    public static class ScheduledEventResolver
    {
        public static void Run(RunState run, NightState night, IRandomSource rng)
        {
            var b = night.Baby;
            var t = run.Temperament;
            double noiseGuard = night.Wearing.Noise ? 0.35 : 0;

            if (night.NightId == NightId.FirstNight && night.Hour == 0
                && night.FiredEventIds.Add("first-night-diaper"))
            {
                night.AddLog("기저귀가 젖었다. 갈아 주기 전까지 계속 보챈다.", LogClass.Warn);
                b.Calm = CoreMath.Clamp(b.Calm - 20, 0, 100);
                if (b.Sleep >= 50 && rng.NextDouble() < 0.8 - noiseGuard * 0.3)
                    TurnResolver.WakeBaby(run, night, "기저귀 불쾌감", rng);
                return;
            }

            if (night.NightId == NightId.SecondNight && night.Hour == 23
                && night.FiredEventIds.Add("second-night-doorbell"))
            {
                night.AddLog("딩동— 초인종이 울렸다. 하필 지금.", LogClass.Warn);
                if (b.Sleep >= 50 && rng.NextDouble() < t.Sens + 0.25 - noiseGuard)
                    TurnResolver.WakeBaby(run, night, "초인종 소리", rng);
                else if (b.Sleep >= 50)
                    night.AddLog("아기가 뒤척이다 다시 잠들었다.", LogClass.Good);
            }
        }
    }
}
