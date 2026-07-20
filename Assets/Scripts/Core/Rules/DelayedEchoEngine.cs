using System;
using System.Collections.Generic;

namespace NotANap.Core
{
    public interface IContextualEventRule
    {
        EventResolution Resolve(EventSeed seed, EventContext context);
    }

    public static class TraceRecorder
    {
        public static TraceRecord FromAction(
            TraceState state, TraceId id, ActionId action, NightId night, int turn,
            double strength, IEnumerable<string> tags = null)
        {
            var record = new TraceRecord
            {
                Id = id,
                SourceKind = TraceSourceKind.Action,
                SourceActionId = action,
                CreatedNight = night,
                CreatedTurn = turn,
                Strength = strength
            };
            if (tags != null) record.Tags.UnionWith(tags);
            return state.Add(record);
        }
    }

    public static class FutureEventScheduler
    {
        public static EventSeed ScheduleOne(
            IReadOnlyList<FutureEventDefinition> definitions,
            NightId night,
            TraceState traces,
            IRandomSource rng)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (traces == null) throw new ArgumentNullException(nameof(traces));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var eligible = new List<FutureEventDefinition>();
            double totalWeight = 0;
            foreach (var definition in definitions)
            {
                if (!IsEligible(definition, night, traces)) continue;
                eligible.Add(definition);
                totalWeight += Math.Max(0, definition.BaseWeight);
            }
            if (eligible.Count == 0 || totalWeight <= 0) return null;

            double pick = rng.NextDouble() * totalWeight;
            FutureEventDefinition selected = eligible[eligible.Count - 1];
            foreach (var candidate in eligible)
            {
                pick -= Math.Max(0, candidate.BaseWeight);
                if (pick < 0) { selected = candidate; break; }
            }

            if (selected.LatestTurn < selected.EarliestTurn)
                throw new InvalidOperationException("Future event turn window is invalid.");
            int turn = selected.EarliestTurn + rng.NextInt(selected.LatestTurn - selected.EarliestTurn + 1);
            var seed = new EventSeed
            {
                EventId = selected.EventId,
                NightId = night,
                TriggerTurn = turn,
                FeedforwardCue = selected.FeedforwardCue
            };
            seed.SourceTraceIds.AddRange(selected.RequiredTraceIds);
            return seed;
        }

        private static bool IsEligible(FutureEventDefinition definition, NightId night, TraceState traces)
        {
            if (definition == null || !definition.EligibleNights.Contains(night)) return false;
            if (definition.MaxTriggersPerRun <= traces.TriggerCount(definition.EventId.Value)) return false;
            foreach (var required in definition.RequiredTraceIds)
                if (!traces.Contains(required)) return false;
            foreach (var excluded in definition.ExcludedTraceIds)
                if (traces.Contains(excluded)) return false;
            foreach (var tag in definition.RequiredTags)
                if (!traces.HasTag(tag)) return false;
            return true;
        }
    }

    public static class DelayedEchoEngine
    {
        /// <summary>암시만 공개한다. Baby/Parent/Memory/Trace 상태는 변경하지 않는다.</summary>
        public static FeedforwardCueId? GetFeedforwardCue(EventSeed seed) => seed?.FeedforwardCue;

        public static EventResolution TryResolve(
            EventSeed seed, EventContext context, TraceState traces,
            int maxTriggersPerRun, IContextualEventRule rule)
        {
            if (seed == null || context == null || traces == null || rule == null) return null;
            if (seed.NightId != context.NightId || seed.TriggerTurn != context.Turn) return null;
            if (traces.TriggerCount(seed.EventId.Value) >= maxTriggersPerRun) return null;

            var resolution = rule.Resolve(seed, context);
            if (resolution == null) return null;
            traces.RecordTrigger(seed.EventId.Value, context.NightId, resolution.SourceTraceIds);
            foreach (var trace in resolution.NewTraces) traces.Add(trace);
            return resolution;
        }
    }
}
