using System;
using System.Collections.Generic;

namespace NotANap.Core
{
    public readonly struct BabyStateSnapshot
    {
        public readonly double Calm, Sleep, Hunger;
        public readonly bool Held, Crying;
        public BabyStateSnapshot(BabyState state)
        {
            Calm = state.Calm; Sleep = state.Sleep; Hunger = state.Hunger;
            Held = state.Held; Crying = state.Crying;
        }
    }

    public readonly struct ParentStateSnapshot
    {
        public readonly double Stamina;
        public ParentStateSnapshot(ParentState state) => Stamina = state.Stamina;
    }

    public sealed class EventContext
    {
        public NightId NightId;
        public int Turn;
        public BabyStateSnapshot Baby;
        public ParentStateSnapshot Parent;
        public TraceState Traces;
        public MemoryState Memory;
        public readonly List<ItemId> SelectedItems = new List<ItemId>();
        public readonly HashSet<string> ActiveConditionTags = new HashSet<string>(StringComparer.Ordinal);

        public static EventContext Capture(RunState run, NightState night, IEnumerable<string> tags = null)
        {
            var context = new EventContext
            {
                NightId = night.NightId,
                Turn = night.ConsumedTurns,
                Baby = new BabyStateSnapshot(night.Baby),
                Parent = new ParentStateSnapshot(night.Parent),
                Traces = run.Traces,
                Memory = run.Memory
            };
            context.SelectedItems.AddRange(night.Items);
            if (tags != null) context.ActiveConditionTags.UnionWith(tags);
            return context;
        }
    }

    public sealed class StateDelta
    {
        public double Calm;
        public double Sleep;
        public double Hunger;
        public double Stamina;
        public GameEventId? SemanticResultId;
    }

    public sealed class EventResolution
    {
        public FutureEventId EventId;
        public readonly List<TraceId> SourceTraceIds = new List<TraceId>();
        public EventOutcomeType OutcomeType;
        public StateDelta StateDelta = new StateDelta();
        public readonly List<TraceRecord> NewTraces = new List<TraceRecord>();
        public bool ConsumedTurn;
    }
}
