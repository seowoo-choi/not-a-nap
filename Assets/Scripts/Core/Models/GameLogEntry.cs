namespace NotANap.Core
{
    /// <summary>게임 로그 한 줄.</summary>
    public sealed class GameLogEntry
    {
        public int Hour { get; }
        public string Text { get; }
        public LogClass Cls { get; }

        public GameLogEntry(int hour, string text, LogClass cls)
        {
            Hour = hour; Text = text; Cls = cls;
        }

        public override string ToString() => $"{Hour:00}:00 [{Cls}] {Text}";
    }
}
