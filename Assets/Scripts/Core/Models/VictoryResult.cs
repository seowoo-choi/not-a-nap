using System.Collections.Generic;

namespace NotANap.Core
{
    /// <summary>백일째 밤 승리 판정 결과 (3중 2).</summary>
    public sealed class VictoryResult
    {
        public List<VictoryCondition> Met = new List<VictoryCondition>();
        public int Count => Met.Count;
        public int RequiredCount = GameConfig.VictoryConditionsRequired;
        public bool IsVictory => Count >= RequiredCount;
    }
}
