using System.Collections.Generic;

namespace NotANap.Core
{
    public sealed class NightMetrics
    {
        public int CurrentSleepStretchMinutes;
        public int LongestSleepStretchMinutes;
        public int TotalSleepMinutes;
        public int WakeCount;
        public int CorrectFirstChecks;
        public int MisdiagnosisCount;
        public int UnsafeChoiceCount;
        public double ParentStaminaAtDawn;

        public void RecordSleep(int minutes)
        {
            if (minutes <= 0) return;
            CurrentSleepStretchMinutes += minutes;
            TotalSleepMinutes += minutes;
            if (CurrentSleepStretchMinutes > LongestSleepStretchMinutes)
                LongestSleepStretchMinutes = CurrentSleepStretchMinutes;
        }

        public void RecordWake()
        {
            if (CurrentSleepStretchMinutes > 0) WakeCount++;
            CurrentSleepStretchMinutes = 0;
        }
    }

    public sealed class SleepCycleState
    {
        public V2SleepStage Stage = V2SleepStage.Awake;
        public int MinutesInStage;
        public int CurrentSleepStretchMinutes;
        public bool IsLimbRelaxed;
        public bool IsBreathingRegular;
        public bool DeepSleepObserved;
    }

    public sealed class DiagnosisState
    {
        public WakeCause ActiveCause = WakeCause.Unknown;
        public bool CauseResolved = true;
        public readonly HashSet<WakeCause> CheckedCauses = new HashSet<WakeCause>();
        public WakeCause FirstCheck = WakeCause.Unknown;
        public int RemainingDecisionSeconds;
        public int MisdiagnosisCount;
        public int CheckAttempts;
        public int EncounterSequence;
        public bool DiaperWetConfirmed;
        public bool DiaperStoolConfirmed;
        public bool DiaperChangedPendingDisposal;
        public int DiaperRecommendationSuppressedUntilMinute;
        /// <summary>이번 각성에서 이미 울음을 달래 준 물건. 토글 연타로 울음을 0까지 지우는 것을 막는다.</summary>
        public readonly HashSet<V2ActionId> SoothedByAction = new HashSet<V2ActionId>();

        public void Begin(WakeCause cause, int decisionSeconds)
        {
            EncounterSequence++;
            ActiveCause = cause;
            CauseResolved = false;
            CheckedCauses.Clear();
            FirstCheck = WakeCause.Unknown;
            RemainingDecisionSeconds = decisionSeconds;
            MisdiagnosisCount = 0;
            CheckAttempts = 0;
            DiaperWetConfirmed = false;
            DiaperStoolConfirmed = false;
            DiaperChangedPendingDisposal = false;
            SoothedByAction.Clear();
        }
    }

    public sealed class EnvironmentState
    {
        public RoomSeason Season;
        public double TemperatureCelsius;
        public double HumidityPercent;
        public double BabyTemperatureCelsius;
        public bool IsTemperatureChecked;
        public bool IsHumidityChecked;
        public bool IsBabyTemperatureChecked;
    }

    public sealed class FeedingPreparationState
    {
        public bool BottleSanitized;
        public bool SanitationIncident;
        public bool WaterReady;
        public bool FormulaMeasured;
        public bool BottleMixed;
        public bool BottleCooled;
        public bool TemperatureChecked;
        public bool IsReadyToFeed => BottleSanitized && WaterReady && FormulaMeasured &&
            BottleMixed && BottleCooled && TemperatureChecked;
    }

    public sealed class BabyProfile
    {
        public string VisualId = "default";
        public VisualGender VisualGender = VisualGender.Unspecified;
        public Temperament Temperament = Temperament.Soft;
        public PacifierAffinity PacifierAffinity = PacifierAffinity.Neutral;
        public double MoroSensitivity = .5;
        public double HungerSensitivity = .5;
        public double SleepCycleSensitivity = .5;
    }

    public sealed class NightModifierState
    {
        public NightModifierId Id;
        public double WakeFrequencyMultiplier = 1;
        public double CryEscalationMultiplier = 1;
        public double SleepGainMultiplier = 1;
        public double ComfortActionModifier = 1;
        public double FeedingNeedModifier = 1;
    }

    public sealed class ScheduledWake
    {
        public int AtElapsedMinute;
        public WakeCause Cause;
        public bool Triggered;
        public FutureEventId? SourceFutureEventId;
    }

    public sealed class V2NightState
    {
        public int ElapsedMinutes;
        public NightMetrics Metrics = new NightMetrics();
        public SleepCycleState SleepCycle = new SleepCycleState();
        public DiagnosisState Diagnosis = new DiagnosisState();
        public EnvironmentState Environment = new EnvironmentState();
        public FeedingPreparationState Feeding = new FeedingPreparationState();
        public BabyProfile Profile = new BabyProfile();
        public NightModifierState Modifier = new NightModifierState();
        public ScheduledWake NextWake;
        public double CryIntensity;
        public bool HoldWhilePreparing;
        public bool HeadSupported;
        /// <summary>
        /// 밤 시작 시점의 백색소음 습관화(0~1) 스냅샷. 밤 도중에는 변하지 않으므로
        /// 각성 예약처럼 RunState를 받지 않는 계산에서도 결정론적으로 쓸 수 있다.
        /// </summary>
        public double NoiseHabituation;
        /// <summary>
        /// 마지막으로 베이비 모니터를 본 경과 분. 아기 곁을 떠나 있는 동안
        /// 아기 상태를 읽을 수 있는지는 이 값의 신선도로 결정한다.
        /// </summary>
        public int MonitorReadAtMinute = int.MinValue / 2;
        /// <summary>
        /// 마지막 수유·기저귀 교체·각성 시점(경과 분). 0은 밤 시작 직전을 뜻한다.
        /// 아기를 눕히기 직전에 먹이고 갈아 둔 상태에서 21:00을 시작하기 때문이다.
        /// 다음에 무엇을 해야 할지는 수치보다 "얼마나 지났는지"로 읽힌다.
        /// </summary>
        public int LastFeedMinute;
        public int LastDiaperChangeMinute;
        public int AwakeSinceMinute;
        public int GentleObservationCount;
        public int SelfResettleCount;
        public int CatchBreathUses;
        public readonly List<ObservationSignalId> VisibleSignals = new List<ObservationSignalId>();
        public HomeLocation CaregiverLocation = HomeLocation.Nursery;
        public bool BathThermometerRetrieved;
        public bool HandsNeedWashing;
        public bool ExhaustionWarned;
        public int BareHandsLaydownAttempts;
        public int DiaperWakeCount;
        public int NextDiaperEligibleMinute = 60;
        public readonly List<ActionAuditEntry> ActionAudit = new List<ActionAuditEntry>();
        public readonly HashSet<ProductCapability> ProductCapabilities = new HashSet<ProductCapability>();
    }

    public sealed class NightEvaluation
    {
        public NightGrade Grade;
        public NightMetrics Metrics;
    }
}
