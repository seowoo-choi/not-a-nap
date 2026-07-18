using System;

namespace NotANap.Core
{
    /// <summary>
    /// 시간 경과 처리(아이템 패시브 → 허기 → 진정 감소 → 수면 진행 → 쪽쪽이 이탈 → 시간 이동
    /// → 예정 이벤트 → 밤 종료). 원본: prototype endTurn().
    /// </summary>
    public static class TurnResolver
    {
        public static void EndTurn(RunState run, NightState night, IRandomSource rng)
        {
            if (night.Over) return;

            var b = night.Baby;
            var p = night.Parent;
            var t = run.Temperament;
            var m = run.GetEffectiveMemory();
            var w = night.Wearing;

            // 백일째 밤: 버클 고장 카운트다운 (발동 후 2턴 사용 불가)
            if (night.CarrierDisabledTurns > 0) night.CarrierDisabledTurns--;

            // 아이템 패시브
            if (w.Carrier)
            {
                b.Held = true;
                b.Calm = CoreMath.Clamp(b.Calm + 12 * (1 + m.Carrier * 0.6), 0, 100);
                p.Stamina = CoreMath.Clamp(p.Stamina - 6, 0, 100);
                night.Stats.CarrierTurns++;
            }
            if (w.Noise && night.HasItem(ItemId.Noise))
            {
                b.Calm = CoreMath.Clamp(b.Calm + 6 * (1 - m.NoiseHab), 0, 100);
                night.Stats.NoiseTurns++;
            }
            if (w.Bouncer && !b.Held)
            {
                if (t.Sens > 0.6)
                {
                    b.Calm = CoreMath.Clamp(b.Calm - 6, 0, 100);
                    night.AddLog("바운서의 흔들림이 오히려 자극이 됐다.", LogClass.Warn);
                }
                else b.Calm = CoreMath.Clamp(b.Calm + 9, 0, 100);
            }

            // 배고픔
            b.Hunger = CoreMath.Clamp(b.Hunger + t.HungerRate * (0.7 + rng.NextDouble() * 0.6), 0, 100);
            if (b.Hunger > 78 && !b.Crying)
            {
                night.AddEvent(GameEventId.HungerCueAppeared);
                WakeBaby(run, night, "배꼽시계 발동", rng);
            }

            // 진정도 자연 감소 (깊은 잠 제외)
            if (b.Sleep < 85) b.Calm = CoreMath.Clamp(b.Calm - 4, 0, 100);
            if (b.Calm <= 20 && !b.Crying)
            {
                b.Crying = true;
                night.AddLog("결국 울음이 터졌다.", LogClass.Warn);
            }

            // 수면 진행
            if (!b.Crying && b.Calm >= 68 && b.Hunger < 70)
            {
                double gain = 12;
                if (b.Held) gain += 5 + t.HoldNeed * 8;
                else gain += -4 + (t.SelfSoothe + m.SelfSoothe) * 16;
                if (w.Carrier) gain += 4 * (1 + m.Carrier);
                b.Sleep = CoreMath.Clamp(b.Sleep + gain, 0, 100);
                if (b.Sleep >= 50 && b.Held) night.Stats.HeldSleepTurns++;
            }
            else if (b.Crying)
            {
                b.Sleep = CoreMath.Clamp(b.Sleep - 12, 0, 100);
            }

            // 쪽쪽이 이탈
            if (night.PacifierInUse && b.Sleep >= 50 && b.Sleep < 85 && rng.NextDouble() < 0.3)
            {
                night.PacifierInUse = false;
                if (rng.NextDouble() < 0.5) WakeBaby(run, night, "쪽쪽이가 쏙 빠졌다", rng);
                else night.AddLog("쪽쪽이가 빠졌지만… 다행히 계속 잔다.");
            }

            // 시간 이동
            night.Hour = (night.Hour + 1) % 24;
            night.ConsumedTurns++;

            // 예정 이벤트
            ScheduledEventResolver.Run(run, night, rng);
            FinalNightResolver.RunScheduledEvents(run, night, rng);

            // 밤 종료
            if (night.Hour == GameConfig.EndHour)
            {
                night.Over = true;
                var stage = b.GetStage();
                if ((stage == SleepStage.Deep || stage == SleepStage.Shallow) && !b.Held)
                    night.Result = NightOutcome.Crib;
                else if (stage == SleepStage.Deep || stage == SleepStage.Shallow)
                    night.Result = NightOutcome.Arms;
                else
                    night.Result = NightOutcome.Awake;
                night.Stats.StaminaLeft = p.Stamina;
                night.AddEvent(GameEventId.NightCompleted);
            }
        }

        /// <summary>
        /// 아기 깨우기. 원본: prototype wakeBaby().
        /// 백일째 밤에는 수면 중 깬 경우 selfSoothe 재입면 판정이 이어진다 (docs/final-night-spec.md).
        /// </summary>
        public static void WakeBaby(RunState run, NightState night, string reason, IRandomSource rng)
        {
            var b = night.Baby;
            bool wasAsleep = b.Sleep >= 50;
            b.Sleep = Math.Min(b.Sleep, 12);
            b.Calm = CoreMath.Clamp(b.Calm - 22, 0, 100);
            b.Crying = true;
            if (wasAsleep) night.Stats.Wakes++;
            if (wasAsleep) night.AddEvent(GameEventId.BabyFullyWoke);
            night.AddLog(
                wasAsleep ? $"😱 {reason} — 아기가 깼다!" : $"😖 {reason} — 아기가 자지러지게 운다!",
                LogClass.Warn);
            if (wasAsleep) FinalNightResolver.TrySelfSootheResettle(run, night, rng);
        }
    }
}
