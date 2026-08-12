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
                    Text = "오늘 밤, 아기띠 안에서 잠드는 걸 배웠다",
                    Sub = "아기띠 진정 ↑ · 맨손 눕히기 난이도 ↑"
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
                        ? "오늘 밤, 할머니 품에서 잠드는 걸 배웠다"
                        : "오늘 밤, 내 품에서 잠드는 걸 배웠다",
                    Sub = "안기 진정 ↑ · 혼자 두면 쉽게 깸"
                });
            }
            if (s.NoiseTurns >= config.NoiseHabitThreshold)
            {
                m.NoiseHab = CoreMath.Clamp01(m.NoiseHab + config.NoiseHabitGain);
                night.AddEvent(GameEventId.HabitFormed);
                notes.Add(new MemoryNote
                {
                    Positive = false,
                    Text = "오늘 밤, 백색소음이 깔린 방에 익숙해졌다",
                    Sub = "백색소음기 진정 효과 ↓"
                });
            }
            if (s.WatchOk >= config.WatchHabitThreshold)
            {
                m.SelfSoothe = CoreMath.Clamp01(m.SelfSoothe + config.SelfSootheGain);
                night.AddEvent(GameEventId.HabitFormed);
                notes.Add(new MemoryNote
                {
                    Positive = true,
                    Text = "오늘 밤, 혼자 가라앉는 법을 배웠다",
                    Sub = "혼자 잠들 확률 ↑ · 다른 수면 습관 완화"
                });
            }
            if (notes.Count == 0)
            {
                notes.Add(new MemoryNote
                {
                    Positive = true,
                    Text = "오늘은 새로 밴 습관이 없다",
                    Sub = "내일 밤에도 방법은 얼마든지 바꿀 수 있다"
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
                CarrierTurns = s.CarrierTurns,
                HeldSleepTurns = s.HeldSleepTurns,
                WatchOk = s.WatchOk,
            });
            return notes;
        }
    }
}
