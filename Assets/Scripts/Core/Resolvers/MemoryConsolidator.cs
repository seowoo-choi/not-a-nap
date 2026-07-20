using System.Collections.Generic;

namespace NotANap.Core
{
    /// <summary>
    /// 밤 종료 시 기억(습관) 형성. 원본: prototype consolidateMemory().
    /// AI 응답과 무관한 규칙 기반 기억만 처리한다.
    /// </summary>
    public static class MemoryConsolidator
    {
        public static List<MemoryNote> Consolidate(RunState run, NightState night)
            => Consolidate(run, night, GameBalanceConfig.Default());

        public static List<MemoryNote> Consolidate(RunState run, NightState night, GameBalanceConfig config)
        {
            var m = run.Memory;
            var s = night.Stats;
            var notes = new List<MemoryNote>();

            if (s.CarrierTurns >= config.CarrierHabitThreshold)
            {
                m.Carrier = CoreMath.Clamp01(m.Carrier + config.CarrierHabitGain);
                night.AddEvent(GameEventId.HabitFormed);
                notes.Add(new MemoryNote
                {
                    Positive = false,
                    Text = "아기띠에서 잠드는 습관이 형성되었습니다.",
                    Sub = "아기띠 진정 효과 ↑ / 침대에 눕히기 성공률 ↓"
                });
            }
            if (s.HeldSleepTurns >= config.HeldSleepHabitThreshold || s.Grandma)
            {
                m.HeldDep = CoreMath.Clamp01(m.HeldDep + config.HeldHabitGain);
                night.AddEvent(GameEventId.HabitFormed);
                notes.Add(new MemoryNote
                {
                    Positive = false,
                    Text = s.Grandma
                        ? "할머니 품의 기억이 남았습니다. 안겨 자는 것에 더 익숙해졌습니다."
                        : "안겨서 자는 것에 익숙해졌습니다.",
                    Sub = "품에서는 빨리 잠듦 / 혼자 두면 쉽게 깸"
                });
            }
            if (s.NoiseTurns >= config.NoiseHabitThreshold)
            {
                m.NoiseHab = CoreMath.Clamp01(m.NoiseHab + config.NoiseHabitGain);
                night.AddEvent(GameEventId.HabitFormed);
                notes.Add(new MemoryNote
                {
                    Positive = false,
                    Text = "백색소음에 익숙해졌습니다.",
                    Sub = "백색소음기 효과 감소"
                });
            }
            if (s.WatchOk >= config.WatchHabitThreshold)
            {
                m.SelfSoothe = CoreMath.Clamp01(m.SelfSoothe + config.SelfSootheGain);
                night.AddEvent(GameEventId.HabitFormed);
                notes.Add(new MemoryNote
                {
                    Positive = true,
                    Text = "스스로 진정하는 힘이 자랐습니다.",
                    Sub = "혼자서 잠들 확률 ↑ — 의존 습관을 되돌리는 열쇠"
                });
            }
            if (notes.Count == 0)
            {
                notes.Add(new MemoryNote
                {
                    Positive = true,
                    Text = "뚜렷한 새 습관 없이 밤을 넘겼습니다.",
                    Sub = "아기는 아직 당신을 관찰하는 중"
                });
            }

            foreach (var note in notes) run.MemoryNotes.Add(note.Text);
            run.NightResults.Add(new NightResult
            {
                NightId = night.NightId,
                Outcome = night.Result ?? NightOutcome.Awake,
                Wakes = s.Wakes,
                LaydownFail = s.LaydownFail,
                StaminaLeft = s.StaminaLeft,
                Grandma = s.Grandma,
                NoiseTurns = s.NoiseTurns,
            });
            return notes;
        }
    }
}
