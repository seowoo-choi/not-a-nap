namespace NotANap.Core
{
    /// <summary>시드 주입 가능한 난수 소스. Core는 UnityEngine.Random을 사용하지 않는다.</summary>
    public interface IRandomSource
    {
        /// <summary>[0, 1) 난수.</summary>
        double NextDouble();

        /// <summary>[0, maxExclusive) 정수 난수.</summary>
        int NextInt(int maxExclusive);
    }
}
