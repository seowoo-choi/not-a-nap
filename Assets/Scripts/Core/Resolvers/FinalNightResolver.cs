using System;
using System.Collections.Generic;

namespace NotANap.Core
{
    /// <summary>
    /// 백일째 밤 전용 규칙. 원본: docs/final-night-spec.md.
    /// 방해 이벤트 선택(결정론적, 최대 2개), 시각별 발동, selfSoothe 재입면 보상 패시브.
    /// </summary>
    public static class FinalNightResolver
    {
        /// <summary>
        /// 습관 표적 방해 이벤트 선택 (결정론적).
        /// 1) 조건 충족 이벤트 수집 (저장 memory 기준) 2) memory×1.5 내림차순 정렬
        /// 3) 동률이면 carrier → noiseHab → heldDep 4) 상위 2개만 활성화.
        /// selfSoothe는 보상 패시브라 이 제한에 포함하지 않는다.
        /// </summary>
        public static List<TargetedEventId> SelectTargetedEvents(RunState run)
        {
            var m = run.Memory;
            var candidates = new List<(TargetedEventId id, double sortValue, int tieOrder)>();

            if (m.Carrier >= GameConfig.HabitEventThreshold)
                candidates.Add((TargetedEventId.CarrierBuckle,
                    m.Carrier * GameConfig.FinalNightMemoryMultiplier, 0));
            if (m.NoiseHab >= GameConfig.HabitEventThreshold
                || run.PreFinalNoiseTurns >= GameConfig.FinalNightNoiseTurnsThreshold)
                candidates.Add((TargetedEventId.NoiseBattery,
                    m.NoiseHab * GameConfig.FinalNightMemoryMultiplier, 1));
            if (m.HeldDep >= GameConfig.HabitEventThreshold)
                candidates.Add((TargetedEventId.DawnWaking,
                    m.HeldDep * GameConfig.FinalNightMemoryMultiplier, 2));

            candidates.Sort((a, b) => a.sortValue != b.sortValue
                ? b.sortValue.CompareTo(a.sortValue)
                : a.tieOrder.CompareTo(b.tieOrder));

            var selected = new List<TargetedEventId>();
            for (int i = 0; i < candidates.Count && i < GameConfig.MaxTargetedInterferenceEvents; i++)
                selected.Add(candidates[i].id);
            return selected;
        }

        /// <summary>selfSoothe 보상 패시브 활성 여부 (저장 memory 기준).</summary>
        public static bool IsSelfSootheActive(RunState run)
            => run.Memory.SelfSoothe >= GameConfig.HabitEventThreshold;

        /// <summary>백일째 밤 시각별 표적 이벤트 발동. ID 기반으로 중복 발동을 막는다.</summary>
        public static void RunScheduledEvents(RunState run, NightState night, IRandomSource rng)
        {
            if (night.NightId != NightId.HundredthNight) return;
            var b = night.Baby;

            if (night.Hour == 0
                && night.ActiveTargetedEvents.Contains(TargetedEventId.CarrierBuckle)
                && night.FiredEventIds.Add("final-carrier-buckle"))
            {
                night.CarrierDisabledTurns = GameConfig.CarrierBuckleDisabledTurns;
                bool wasWearing = night.Wearing.Carrier;
                night.Wearing.Carrier = false;
                night.AddLog("🚨 아기띠 버클이 고장 났다! 2시간 동안 아기띠를 쓸 수 없다.", LogClass.Warn);
                if (wasWearing) night.AddLog("아기를 급히 품으로 옮겨 안았다.");
            }

            if (night.Hour == 1
                && night.ActiveTargetedEvents.Contains(TargetedEventId.NoiseBattery)
                && night.FiredEventIds.Add("final-noise-battery"))
            {
                night.NoiseDisabled = true;
                night.Wearing.Noise = false;
                night.AddLog("🚨 백색소음기 배터리가 방전됐다. 오늘 밤은 다시 켤 수 없다.", LogClass.Warn);
            }

            if (night.Hour == 3
                && night.ActiveTargetedEvents.Contains(TargetedEventId.DawnWaking)
                && night.FiredEventIds.Add("final-dawn-waking"))
            {
                b.Sleep = CoreMath.Clamp(b.Sleep - GameConfig.DawnWakingSleepPenalty, 0, 100);
                night.LaydownExtraPenalty += GameConfig.DawnWakingLaydownPenalty;
                night.AddLog("🚨 새벽 각성 — 아기가 얕은 숨을 쉬며 뒤척인다. 눕히기가 더 어려워졌다.", LogClass.Warn);
            }
        }

        /// <summary>
        /// selfSoothe 재입면: 수면(sleep ≥ 50) 중 깬 직후에만 50% 판정.
        /// 성공 시 울음 해제, sleep 최소 50 회복, calm +10 (docs/final-night-spec.md 구현 확정 사항).
        /// WakeBaby()에서 호출된다.
        /// </summary>
        public static void TrySelfSootheResettle(RunState run, NightState night, IRandomSource rng)
        {
            if (night.NightId != NightId.HundredthNight || !IsSelfSootheActive(run)) return;
            if (rng.NextDouble() < GameConfig.SelfSootheResettleChance)
            {
                var b = night.Baby;
                b.Crying = false;
                b.Sleep = Math.Max(b.Sleep, 50);
                b.Calm = CoreMath.Clamp(b.Calm + 10, 0, 100);
                night.AddLog("👶 아기가 칭얼대다… 스스로 다시 잠들었다.", LogClass.Good);
            }
        }
    }
}
