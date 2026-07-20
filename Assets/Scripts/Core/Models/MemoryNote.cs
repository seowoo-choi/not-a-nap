namespace NotANap.Core
{
    /// <summary>밤 종료 기억 분석 카드 한 장. 원본: prototype consolidateMemory()의 notes.</summary>
    public sealed class MemoryNote
    {
        /// <summary>true면 보상 습관(초록), false면 의존 습관(경고).</summary>
        public bool Positive;
        public string Text;
        public string Sub;
    }
}
