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
        }
    }

    public sealed class EnvironmentState
    {
        public double TemperatureCelsius;
        public double HumidityPercent;
        public bool IsTemperatureChecked;
        public bool IsHumidityChecked;
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
        public bool ExhaustionWarned;
        public readonly HashSet<ProductCapability> ProductCapabilities = new HashSet<ProductCapability>();
    }

    public sealed class NightEvaluation
    {
        public NightGrade Grade;
        public NightMetrics Metrics;
    }
}
