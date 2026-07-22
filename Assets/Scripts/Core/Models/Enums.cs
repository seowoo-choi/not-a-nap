namespace NotANap.Core
{
    /// <summary>제출작의 세 밤. 세 번째 밤은 Day 3가 아니라 백일째 밤이다.</summary>
    public enum NightId
    {
        FirstNight,
        SecondNight,
        HundredthNight
    }

    public enum ItemId
    {
        Carrier,
        Pacifier,
        Noise,
        Bouncer,
        Monitor
    }

    public enum TemperamentId { Soft, Sensitive, Hungry }

    public enum ActionId
    {
        Hold, Pat, Feed, Laydown, Watch, Grandma, Pacifier,
        ToggleCarrier, ToggleNoise, ToggleBouncer,
        CheckDiaper, ChangeDiaper, CheckHungerSignals, CheckEnvironment,
        AdjustTemperature, AdjustHumidity, Hesitate, CheckLimbRelaxation,
        SterilizeBottle, PrepareWater, MeasureFormula, MixFormula, CoolBottle,
        CheckBottleTemperature, FeedPreparedBottle, HoldWhilePreparing
    }

    /// <summary>Presentation이 현지화 문구로 변환하는 의미 기반 이벤트.</summary>
    public enum GameEventId
    {
        ActionRejected,
        ActiveSleepStarted,
        ActiveSleepObserved,
        SleepingBabyWokenByIntervention,
        BabyFullyWoke,
        HungerCueAppeared,
        BottleFoundUnsanitized,
        LaydownSucceeded,
        LaydownFailed,
        HabitFormed,
        PartnerHintRequested,
        CueMatchedResponse,
        FinalChallengeStarted,
        NightCompleted,
        TraceCreated,
        TraceStrengthChanged,
        FeedforwardCueAvailable,
        FutureEventTriggered,
        ContextualConsequenceResolved
    }

    public enum EventOutcomeType { Positive, Negative, Neutral }

    public enum GameAction
    {
        Hold,
        Pat,
        Feed,
        Laydown,
        Watch,
        Grandma,
        Pacifier,
        ToggleCarrier,
        ToggleNoise,
        ToggleBouncer
    }

    /// <summary>밤 종료 시 아기 상태. 원본: prototype endTurn()의 result.</summary>
    public enum NightOutcome
    {
        /// <summary>침대에서 잠든 채 아침.</summary>
        Crib,
        /// <summary>품에 안긴 채 잠들어 아침.</summary>
        Arms,
        /// <summary>끝내 잠들지 않음.</summary>
        Awake
    }

    /// <summary>수면 상태 단어. 원본: prototype sleepStage().</summary>
    public enum SleepStage
    {
        Cry,
        Deep,
        Shallow,
        Drowsy,
        Fussy,
        Awake
    }

    public enum LogClass
    {
        Sys,
        Good,
        Warn,
        Baby
    }

    /// <summary>백일째 밤 습관 표적 방해 이벤트 (selfSoothe 보상 패시브는 별도).</summary>
    public enum TargetedEventId
    {
        /// <summary>00시 아기띠 버클 고장 (2턴 사용 불가).</summary>
        CarrierBuckle,
        /// <summary>01시 백색소음기 배터리 방전 (그 밤 내내).</summary>
        NoiseBattery,
        /// <summary>03시 새벽 각성 (수면 -30, 눕히기 페널티 +0.1).</summary>
        DawnWaking
    }

    /// <summary>백일째 밤 승리 조건 3종.</summary>
    public enum VictoryCondition
    {
        /// <summary>아기가 깊은 잠(sleep ≥ 85, 울지 않음)으로 아침을 맞음.</summary>
        DeepSleepMorning,
        /// <summary>보호자 체력 ≥ 30.</summary>
        ParentStamina,
        /// <summary>맨손 눕히기 성공 1회 이상.</summary>
        BareHandsLaydown
    }

    /// <summary>엔딩 6종. 판정 우선순위는 EndingResolver가 담당.</summary>
    public enum EndingId
    {
        MorningWon,
        FamilyRoutine,
        UniverseInArms,
        GrandmaBest,
        GearMaster,
        DawnSurvivor
    }
}
