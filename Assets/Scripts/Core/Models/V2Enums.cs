using System;

namespace NotANap.Core
{
    public enum V2SleepStage { Awake, Drowsy, RemActiveSleep, NremDeepSleep }
    public enum WakeCause { Unknown, Diaper, Hunger, Temperature, Humidity, MoroReflex, PainOrCondition, NaturalCycle }
    public enum HungerSignalStage { None, Early, Active, Late }
    public enum DiaperCheckResult { None, Clean, Wet }
    public enum PacifierAffinity { Loves, Neutral, Rejects }
    public enum NightModifierId { None, Vaccination, WonderWeeks, Teething, SleepRegression }
    public enum NightGrade { S, A, B, C, D }
    public enum VisualGender { Unspecified, Feminine, Masculine, Neutral }
    public enum ProductCapability { AutoFormulaPrep, PreSanitizedBottle, TemperatureControl }
    public enum FeedingPreparationStep { SanitizeBottle, PrepareWater, MeasureFormula, MixFormula, CoolBottle, CheckTemperature }

    public enum ObservationSignalId
    {
        LipSmacking, MouthOpening, HandSucking, Rooting, LeaningToCaregiver,
        Squirming, RapidBreathing, HeadTurning, HungerCry,
        RedEyebrows, AvoidingEyes, TurningHeadAway, BlankStare, Yawning,
        RubbingEyes, PullingEar, PullingHair, Fussing, ArchedBack, ClenchedFist,
        EyelidFlutter, IrregularBreathing, FacialMovement, LimbMovement,
        RegularBreathing, CalmFace, RelaxedLimbs
    }

    public enum V2ActionId
    {
        Hold, Pat, Laydown, Pacifier, CheckLimbRelaxation,
        CheckDiaper, ChangeDiaper, CheckHungerSignals, CheckEnvironment, CheckBodyTemperature,
        AdjustTemperature, AdjustHumidity, Hesitate,
        SterilizeBottle, PrepareWater, MeasureFormula, MixFormula, CoolBottle,
        CheckBottleTemperature, FeedPreparedBottle, HoldWhilePreparing,
        ToggleNoise, CheckMonitor, CatchBreath
    }

    public enum V2ActionBlockReason { None, BabyNotHeld, BabyNotAsleep, ItemUnavailable }

    public static class CoreTraceIds
    {
        public static readonly TraceId DiaperCheckedFirst = new TraceId("diagnosis.diaper.checked-first");
        public static readonly TraceId CauseRecheckedAfterMismatch = new TraceId("diagnosis.cause.rechecked-after-mismatch");
        public static readonly TraceId DeepSleepObservedBeforeLaydown = new TraceId("sleep.deep-observed-before-laydown");
        public static readonly TraceId PacifierAccepted = new TraceId("pacifier.accepted");
        public static readonly TraceId PacifierRejected = new TraceId("pacifier.rejected");
        public static readonly TraceId FeedingPreparationCompleted = new TraceId("feeding.preparation.completed");
        public static readonly TraceId FeedingAttemptedBeforeReady = new TraceId("feeding.attempted-before-ready");
        public static readonly TraceId SelfResettled = new TraceId("sleep.self-resettled");
    }
}
