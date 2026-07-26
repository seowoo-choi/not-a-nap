using System.Collections.Generic;

namespace NotANap.Core
{
    public enum RhythmId { Carrier, HeldSleep, Noise, SelfSoothe, Neutral }
    public enum NightAuditKind { Action, Movement, SleepInterval }

    /// <summary>MemoryConsolidator가 확정한 저장값을 화면에 전달하는 의미 데이터.</summary>
    public sealed class RhythmFact
    {
        public RhythmId Id;
        public double Strength;
        public int SourceCount;
    }

    /// <summary>AI와 Presentation이 공유하는 검증된 밤 기록. ID와 수치만 보관한다.</summary>
    public sealed class DiaryFacts
    {
        public NightId NightId;
        public ObservationSignalId? FirstNoticedSignal;
        public V2ActionId? MostRepeatedAction;
        public int MostRepeatedActionCount;
        public V2ActionId? RejectedAction;
        public V2ActionId? FollowupAction;
        public int LongestSleepMinutes;
        public int WakeCount;
        public double ParentStamina;
        public bool UsedCatchBreath;
        public FeedingPreparationStep? LongestPreparationStep;
        public int BareHandsLaydownAttempts;
        public bool BareHandsLaydownSucceeded;
        public bool FeedingPreparationIncident;
        public HomeLocation? LongestMovementDestination;
        public int LongestMovementMinutes;
        public SleepIntervalChoice? SleepIntervalChoice;
        public readonly List<RhythmFact> Rhythms = new List<RhythmFact>();
    }

    public sealed class ActionAuditEntry
    {
        public NightAuditKind Kind = NightAuditKind.Action;
        public V2ActionId Action;
        public bool Accepted;
        public int TimeDeltaMinutes;
        public int EncounterSequence;
        public int ElapsedMinutes;
        public HomeLocation? MovementDestination;
        public SleepIntervalChoice? IntervalChoice;
        public readonly List<ObservationSignalId> ObservedSignals = new List<ObservationSignalId>();
    }
}
