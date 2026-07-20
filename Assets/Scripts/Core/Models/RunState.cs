using System.Collections.Generic;

namespace NotANap.Core
{
    /// <summary>
    /// 런(세 밤: 첫째 밤 → 둘째 밤 → 백일째 밤) 전체 상태.
    /// </summary>
    public sealed class RunState
    {
        public NightId CurrentNightId = NightId.FirstNight;
        public Temperament Temperament = Temperament.Soft;
        public MemoryState Memory = new MemoryState();
        public TraceState Traces = new TraceState();
        /// <summary>누적 습관 설명 (기억 분석 카드·엔딩 습관 목록용).</summary>
        public List<string> MemoryNotes = new List<string>();
        public bool GrandmaUsed;
        /// <summary>세 밤 동안 침실에 가져간 아이템 종류 누적. "장비의 지배자" 엔딩 판정용.</summary>
        public HashSet<ItemId> UsedItemKinds = new HashSet<ItemId>();
        public List<NightResult> NightResults = new List<NightResult>();

        public bool IsFinalNight => CurrentNightId == NightId.HundredthNight;

        /// <summary>
        /// 효과 계산용 memory. 백일째 밤에만 1.5배(적용 후 [0,1] 클램프).
        /// 저장값(Memory)은 절대 변경하지 않는다 (docs/final-night-spec.md).
        /// </summary>
        public MemoryState GetEffectiveMemory()
            => IsFinalNight ? Memory.Scaled(GameConfig.FinalNightMemoryMultiplier) : Memory;

        /// <summary>1~2일차 noiseTurns 합 (백일째 밤 배터리 방전 이벤트 조건).</summary>
        public int PreFinalNoiseTurns
        {
            get
            {
                int sum = 0;
                foreach (var r in NightResults)
                    if (r.NightId != NightId.HundredthNight) sum += r.NoiseTurns;
                return sum;
            }
        }

        public static RunState Create(Temperament temperament)
            => new RunState { Temperament = temperament };

        /// <summary>기질 무작위 선택. 원본: prototype newRun().</summary>
        public static RunState CreateRandom(IRandomSource rng)
            => Create(Temperament.All[rng.NextInt(Temperament.All.Length)]);

        public static RunState Create(RunSeed seed) => CreateRandom(seed.CreateRandomSource());

        /// <summary>다음 밤으로 진행. 백일째 밤 이후에는 false.</summary>
        public bool AdvanceNight()
        {
            switch (CurrentNightId)
            {
                case NightId.FirstNight: CurrentNightId = NightId.SecondNight; return true;
                case NightId.SecondNight: CurrentNightId = NightId.HundredthNight; return true;
                default: return false;
            }
        }
    }
}
