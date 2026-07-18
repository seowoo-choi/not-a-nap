using System;
using System.Collections.Generic;

namespace NotANap.Core
{
    public readonly struct FutureEventId : IEquatable<FutureEventId>
    {
        public string Value { get; }
        public FutureEventId(string value) => Value = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("FutureEventId cannot be empty.", nameof(value)) : value;
        public bool Equals(FutureEventId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FutureEventId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct FeedforwardCueId : IEquatable<FeedforwardCueId>
    {
        public string Value { get; }
        public FeedforwardCueId(string value) => Value = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("FeedforwardCueId cannot be empty.", nameof(value)) : value;
        public bool Equals(FeedforwardCueId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is FeedforwardCueId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    }

    /// <summary>정확한 단일 시각이 아닌 eligible window에서 예약되는 데이터 정의.</summary>
    public sealed class FutureEventDefinition
    {
        public FutureEventId EventId;
        public readonly HashSet<NightId> EligibleNights = new HashSet<NightId>();
        public int EarliestTurn;
        public int LatestTurn;
        public readonly List<TraceId> RequiredTraceIds = new List<TraceId>();
        public readonly List<TraceId> ExcludedTraceIds = new List<TraceId>();
        public readonly HashSet<string> RequiredTags = new HashSet<string>(StringComparer.Ordinal);
        public double BaseWeight = 1;
        public int MaxTriggersPerRun = 1;
        public FeedforwardCueId? FeedforwardCue;
    }

    /// <summary>seed와 현재 Trace로 결정된 미래 이벤트 예약. 결과의 좋고 나쁨은 포함하지 않는다.</summary>
    public sealed class EventSeed
    {
        public FutureEventId EventId;
        public NightId NightId;
        public int TriggerTurn;
        public readonly List<TraceId> SourceTraceIds = new List<TraceId>();
        public FeedforwardCueId? FeedforwardCue;
    }
}
