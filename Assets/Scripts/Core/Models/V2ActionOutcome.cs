using System.Collections.Generic;

namespace NotANap.Core
{
    public sealed class V2ActionOutcome
    {
        public V2ActionId Action;
        public bool Accepted;
        public bool ConsumedTime;
        public int TimeDeltaMinutes;
        public double StaminaDelta;
        public bool CauseResolved;
        public V2ActionBlockReason BlockReason;
        public HungerSignalStage HungerSignalStage;
        public DiaperCheckResult DiaperCheckResult;
        public bool MonitorRead;
        public readonly List<ObservationSignalId> ObservedSignals = new List<ObservationSignalId>();
        public readonly List<FeedingPreparationStep> MissingPreparationSteps = new List<FeedingPreparationStep>();
        public readonly List<GameEventId> EventIds = new List<GameEventId>();
        public readonly List<TraceId> TraceIds = new List<TraceId>();
        public StateDelta StateDelta = new StateDelta();
    }
}
