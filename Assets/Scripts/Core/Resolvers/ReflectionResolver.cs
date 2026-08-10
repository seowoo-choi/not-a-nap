using System;
using System.Collections.Generic;

namespace NotANap.Core
{
    /// <summary>저장된 Core 결과를 읽기 전용 의미 데이터로 투영한다. 상태를 변경하지 않는다.</summary>
    public static class ReflectionResolver
    {
        public static List<RhythmFact> GetRhythms(RunState run, int maximum)
        {
            var facts = new List<RhythmFact>();
            Add(facts, RhythmId.Carrier, run.Memory.Carrier, Count(run, r => r.CarrierTurns));
            Add(facts, RhythmId.HeldSleep, run.Memory.HeldDep, Count(run, r => r.HeldSleepTurns));
            Add(facts, RhythmId.Noise, run.Memory.NoiseHab, Count(run, r => r.NoiseTurns));
            Add(facts, RhythmId.SelfSoothe, run.Memory.SelfSoothe, Count(run, r => r.WatchOk));
            facts.Sort((a, b) =>
            {
                int strength = b.Strength.CompareTo(a.Strength);
                return strength != 0 ? strength : a.Id.CompareTo(b.Id);
            });
            if (facts.Count == 0) facts.Add(new RhythmFact { Id = RhythmId.Neutral });
            if (facts.Count > maximum) facts.RemoveRange(maximum, facts.Count - maximum);
            return facts;
        }

        public static NarrativeFacts BuildNarrativeFacts(RunState run, NightState night)
        {
            if (night?.V2 == null) throw new ArgumentException("V2 night is required.", nameof(night));
            var facts = new NarrativeFacts
            {
                NightId = night.NightId,
                LongestSleepMinutes = night.V2.Metrics.LongestSleepStretchMinutes,
                WakeCount = night.V2.Metrics.WakeCount,
                ParentStamina = night.Parent.Stamina,
                BareHandsLaydownAttempts = night.V2.BareHandsLaydownAttempts,
                BareHandsLaydownSucceeded = night.Stats.BareHandsLaydownSucceeded,
                FeedingPreparationIncident = night.V2.Feeding.SanitationIncident
            };
            var counts = new Dictionary<V2ActionId, int>();
            bool waitingForFollowup = false;
            int rejectedEncounter = -1;
            for (int i = 0; i < night.V2.ActionAudit.Count; i++)
            {
                var entry = night.V2.ActionAudit[i];
                if (entry.Kind == NightAuditKind.Movement)
                {
                    if (entry.TimeDeltaMinutes > facts.LongestMovementMinutes)
                    {
                        facts.LongestMovementMinutes = entry.TimeDeltaMinutes;
                        facts.LongestMovementDestination = entry.MovementDestination;
                    }
                    continue;
                }
                if (entry.Kind == NightAuditKind.SleepInterval)
                {
                    facts.SleepIntervalChoice = entry.IntervalChoice;
                    if (entry.IntervalChoice == NotANap.Core.SleepIntervalChoice.PrepareNextFeed)
                        facts.LongestPreparationStep = FeedingPreparationStep.PrepareWater;
                    continue;
                }
                if (entry.Accepted)
                {
                    if (waitingForFollowup && !facts.FollowupAction.HasValue &&
                        entry.EncounterSequence == rejectedEncounter && IsRelatedResponse(entry.Action))
                    {
                        facts.FollowupAction = entry.Action;
                        waitingForFollowup = false;
                    }
                    counts[entry.Action] = counts.TryGetValue(entry.Action, out var count) ? count + 1 : 1;
                    if (entry.Action == V2ActionId.CatchBreath) facts.UsedCatchBreath = true;
                    if (!facts.FirstNoticedSignal.HasValue && entry.ObservedSignals.Count > 0)
                        facts.FirstNoticedSignal = entry.ObservedSignals[0];
                    if (IsPreparation(entry.Action) &&
                        (!facts.LongestPreparationStep.HasValue ||
                         entry.TimeDeltaMinutes > PreparationMinutes(night, facts.LongestPreparationStep.Value)))
                        facts.LongestPreparationStep = ToPreparationStep(entry.Action);
                }
                else if (!facts.RejectedAction.HasValue)
                {
                    facts.RejectedAction = entry.Action;
                    waitingForFollowup = true;
                    rejectedEncounter = entry.EncounterSequence;
                }
            }
            foreach (var pair in counts)
                if (!facts.MostRepeatedAction.HasValue || pair.Value > facts.MostRepeatedActionCount ||
                    (pair.Value == facts.MostRepeatedActionCount && pair.Key < facts.MostRepeatedAction.Value))
                {
                    facts.MostRepeatedAction = pair.Key;
                    facts.MostRepeatedActionCount = pair.Value;
                }
            facts.Rhythms.AddRange(GetRhythms(run, 2));
            return facts;
        }

        private static void Add(List<RhythmFact> output, RhythmId id, double strength, int count)
        {
            if (strength > 0) output.Add(new RhythmFact { Id = id, Strength = strength, SourceCount = count });
        }

        private static int Count(RunState run, Func<NightResult, int> selector)
        {
            int total = 0;
            foreach (var result in run.NightResults) total += selector(result);
            return total;
        }

        private static bool IsPreparation(V2ActionId id) => id == V2ActionId.SterilizeBottle ||
            id == V2ActionId.PrepareWater || id == V2ActionId.MeasureFormula ||
            id == V2ActionId.MixFormula || id == V2ActionId.CoolBottle ||
            id == V2ActionId.CheckBottleTemperature;

        private static bool IsRelatedResponse(V2ActionId id) => id == V2ActionId.Hold ||
            id == V2ActionId.Pat || id == V2ActionId.Pacifier || id == V2ActionId.Laydown ||
            id == V2ActionId.ChangeDiaper || id == V2ActionId.AdjustTemperature ||
            id == V2ActionId.AdjustHumidity || id == V2ActionId.FeedPreparedBottle;

        private static FeedingPreparationStep ToPreparationStep(V2ActionId id) => id switch
        {
            V2ActionId.SterilizeBottle => FeedingPreparationStep.SanitizeBottle,
            V2ActionId.PrepareWater => FeedingPreparationStep.PrepareWater,
            V2ActionId.MeasureFormula => FeedingPreparationStep.MeasureFormula,
            V2ActionId.MixFormula => FeedingPreparationStep.MixFormula,
            V2ActionId.CoolBottle => FeedingPreparationStep.CoolBottle,
            _ => FeedingPreparationStep.CheckTemperature
        };

        private static int PreparationMinutes(NightState night, FeedingPreparationStep step)
        {
            foreach (var entry in night.V2.ActionAudit)
                if (entry.Accepted && IsPreparation(entry.Action) && ToPreparationStep(entry.Action) == step)
                    return entry.TimeDeltaMinutes;
            return 0;
        }
    }
}
