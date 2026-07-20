namespace NotANap.Core
{
    /// <summary>System.Random 래퍼. 시드 고정 시 결과가 재현된다.</summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly System.Random _random;

        public SystemRandomSource(int seed)
        {
            _random = new System.Random(seed);
        }

        public double NextDouble() => _random.NextDouble();

        public int NextInt(int maxExclusive) => _random.Next(maxExclusive);
    }
}
