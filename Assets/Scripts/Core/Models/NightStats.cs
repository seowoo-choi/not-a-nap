namespace NotANap.Core
{
    /// <summary>밤 1회의 누적 통계. 원본: prototype newNight().stats + 맨손 눕히기 기록.</summary>
    public sealed class NightStats
    {
        public int Wakes;
        public int LaydownFail;
        public int LaydownOk;
        public int CarrierTurns;
        public int HeldSleepTurns;
        public int NoiseTurns;
        public int WatchOk;
        public int Feeds;
        public int Refusals;
        public int Holds;
        public bool Grandma;
        public double StaminaLeft;

        /// <summary>
        /// 맨손 눕히기 성공: 눕히기 시작 시점에 아기띠 미착용·바운서 미사용·품에 안은 상태였고 성공.
        /// 백일째 밤 승리 조건 3에 사용 (docs/final-night-spec.md).
        /// </summary>
        public bool BareHandsLaydownSucceeded;
    }
}
