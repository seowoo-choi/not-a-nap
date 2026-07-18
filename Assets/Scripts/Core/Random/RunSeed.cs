namespace NotANap.Core
{
    public readonly struct RunSeed
    {
        public int Value { get; }
        public RunSeed(int value) => Value = value;
        public IRandomSource CreateRandomSource() => new SystemRandomSource(Value);
    }
}
