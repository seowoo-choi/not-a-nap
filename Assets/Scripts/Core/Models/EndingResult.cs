using System.Collections.Generic;

namespace NotANap.Core
{
    /// <summary>엔딩 판정 결과. 원본: docs/final-night-spec.md 엔딩 6종.</summary>
    public sealed class EndingResult
    {
        public EndingId Id;
        /// <summary>성공(승리) 여부.</summary>
        public bool IsSuccess;
        /// <summary>충족한 승리 조건 목록.</summary>
        public List<VictoryCondition> MetConditions = new List<VictoryCondition>();
    }
}
