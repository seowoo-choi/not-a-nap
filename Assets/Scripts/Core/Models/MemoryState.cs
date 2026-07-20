namespace NotANap.Core
{
    /// <summary>
    /// 아기의 기억(습관) 상태. 각 값은 [0, 1].
    /// 저장값은 규칙 기반 MemoryConsolidator만 수정한다. AI 서술은 이 상태에 접근하지 않는다.
    /// 백일째 밤의 1.5배는 저장값을 바꾸지 않고 Scaled()로 효과 계산 시에만 적용한다.
    /// </summary>
    public sealed class MemoryState
    {
        /// <summary>아기띠 의존.</summary>
        public double Carrier;
        /// <summary>안겨잠 의존.</summary>
        public double HeldDep;
        /// <summary>백색소음 익숙해짐.</summary>
        public double NoiseHab;
        /// <summary>자기 진정력 (보상 습관).</summary>
        public double SelfSoothe;

        /// <summary>효과 계산용 스케일 복사본. 적용 후 [0, 1] 클램프 (final-night-spec 구현 확정 사항).</summary>
        public MemoryState Scaled(double factor) => new MemoryState
        {
            Carrier = CoreMath.Clamp01(Carrier * factor),
            HeldDep = CoreMath.Clamp01(HeldDep * factor),
            NoiseHab = CoreMath.Clamp01(NoiseHab * factor),
            SelfSoothe = CoreMath.Clamp01(SelfSoothe * factor),
        };
    }
}
