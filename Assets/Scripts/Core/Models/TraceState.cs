using System;
using System.Collections.Generic;

namespace NotANap.Core
{
    public readonly struct TraceId : IEquatable<TraceId>
    {
        public string Value { get; }
        public TraceId(string value) => Value = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("TraceId cannot be empty.", nameof(value)) : value;
        public bool Equals(TraceId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is TraceId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public enum TraceSourceKind { Action, Event }

    public sealed class TraceRecord
    {
        public TraceId Id;
        public TraceSourceKind SourceKind;
        public ActionId? SourceActionId;
        public GameEventId? SourceEventId;
        public NightId CreatedNight;
        public int CreatedTurn;
        public double Strength;
        public int TriggerCount;
        public NightId? LastTriggeredNight;
        public readonly HashSet<string> Tags = new HashSet<string>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Metadata = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>수치형 Memory와 별도로 런 전체에 누적되는 가치중립적 과거 흔적.</summary>
    public sealed class TraceState
    {
        private readonly List<TraceRecord> _records = new List<TraceRecord>();
        private readonly Dictionary<string, int> _eventTriggerCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        public IReadOnlyList<TraceRecord> Records => _records;

        public TraceRecord Add(TraceRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            record.Strength = CoreMath.Clamp01(record.Strength);
            _records.Add(record);
            return record;
        }

        public bool Contains(TraceId id)
        {
            foreach (var record in _records) if (record.Id.Equals(id)) return true;
            return false;
        }

        public bool HasTag(string tag)
        {
            foreach (var record in _records) if (record.Tags.Contains(tag)) return true;
            return false;
        }

        public int TriggerCount(string eventId) =>
            _eventTriggerCounts.TryGetValue(eventId, out var count) ? count : 0;

        public void RecordTrigger(string eventId, NightId night, IReadOnlyList<TraceId> sources)
        {
            _eventTriggerCounts[eventId] = TriggerCount(eventId) + 1;
            foreach (var record in _records)
            foreach (var source in sources)
                if (record.Id.Equals(source))
                {
                    record.TriggerCount++;
                    record.LastTriggeredNight = night;
                }
        }
    }
}
