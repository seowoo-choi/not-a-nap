namespace NotANap.Core
{
    /// <summary>밤 1회의 결과 요약. RunState.NightResults에 누적.</summary>
    public sealed class NightResult
    {
        public NightId NightId;
        public NightOutcome Outcome;
        public int Wakes;
        public int LaydownFail;
        public double StaminaLeft;
        public bool Grandma;
        /// <summary>백일째 밤 배터리 방전 이벤트 조건(1~2일차 noiseTurns 합)에 사용.</summary>
        public int NoiseTurns;
    }
}
