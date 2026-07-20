namespace NotANap.Core
{
    /// <summary>아기 상태. 초기값 원본: prototype newNight().</summary>
    public sealed class BabyState
    {
        public double Calm = 55;
        public double Sleep = 0;
        public double Hunger = 30;
        public bool Held;
        public bool Crying;

        /// <summary>수면 상태 단어. 원본: prototype sleepStage().</summary>
        public SleepStage GetStage()
        {
            if (Crying) return SleepStage.Cry;
            if (Sleep >= 85) return SleepStage.Deep;
            if (Sleep >= 50) return SleepStage.Shallow;
            if (Calm >= 70) return SleepStage.Drowsy;
            if (Calm <= 30) return SleepStage.Fussy;
            return SleepStage.Awake;
        }
    }
}
