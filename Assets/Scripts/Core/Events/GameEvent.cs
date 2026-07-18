namespace NotANap.Core
{
    public sealed class GameEvent
    {
        public GameEventId Id { get; }
        public int Turn { get; }
        public TraceId? TraceId { get; }
        public FeedforwardCueId? FeedforwardCueId { get; }
        public EventOutcomeType? OutcomeType { get; }

        public GameEvent(GameEventId id, int turn, TraceId? traceId = null,
            FeedforwardCueId? feedforwardCueId = null, EventOutcomeType? outcomeType = null)
        {
            Id = id;
            Turn = turn;
            TraceId = traceId;
            FeedforwardCueId = feedforwardCueId;
            OutcomeType = outcomeType;
        }
    }
}
