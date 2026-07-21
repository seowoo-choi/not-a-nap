using System.Collections.Generic;

namespace NotANap.Core
{
    /// <summary>Unity 직렬화와 무관한 순수 설정 모델. 기본값은 현재 프로토타입 동작을 보존한다.</summary>
    public sealed class GameBalanceConfig
    {
        public double InitialCalm = 55;
        public double InitialSleep;
        public double InitialHunger = 30;
        public double InitialStamina = 100;
        public int StartHour = 21;
        public int EndHour = 6;
        public int TurnsPerNight = 9;
        public int NormalNightItemSlots = 3;
        public int FinalNightItemSlots = 2;
        public double CarrierHabitThreshold = 3;
        public double HeldSleepHabitThreshold = 3;
        public double NoiseHabitThreshold = 4;
        public double WatchHabitThreshold = 2;
        public double CarrierHabitGain = .35;
        public double HeldHabitGain = .30;
        public double NoiseHabitGain = .35;
        public double SelfSootheGain = .22;
        public VictoryRuleDefinition Victory = VictoryRuleDefinition.Default();
        public FeatureFlags Features = new FeatureFlags();
        public V2BalanceConfig V2 = V2BalanceConfig.Default();
        public readonly Dictionary<string, TemperamentModifierDefinition> TemperamentModifiers =
            new Dictionary<string, TemperamentModifierDefinition>
            {
                { "soft", new TemperamentModifierDefinition { CribSensitivity = .10 } },
                { "sensitive", new TemperamentModifierDefinition { CribSensitivity = .32 } },
                { "hungry", new TemperamentModifierDefinition { CribSensitivity = .18 } }
            };

        public static GameBalanceConfig Default() => new GameBalanceConfig();
    }

    public sealed class V2BalanceConfig
    {
        public readonly Dictionary<NightModifierId, NightModifierState> NightModifiers =
            new Dictionary<NightModifierId, NightModifierState>
            {
                { NightModifierId.None, new NightModifierState { Id = NightModifierId.None } },
                { NightModifierId.Vaccination, new NightModifierState
                    {
                        Id = NightModifierId.Vaccination,
                        WakeFrequencyMultiplier = 1.35,
                        CryEscalationMultiplier = 1.25,
                        SleepGainMultiplier = .85,
                        ComfortActionModifier = 1.10,
                        FeedingNeedModifier = 1.10
                    }
                }
            };
        public int NightDurationMinutes = 540;
        public int DefaultActionMinutes = 15;
        public int DiagnosisActionMinutes = 10;
        public int PreparationActionMinutes = 15;
        public int DecisionSeconds = 20;
        public double MisdiagnosisStaminaPenalty = 6;
        public double MisdiagnosisCryIncrease = 12;
        public double HesitationStaminaPenalty = 4;
        public double HesitationCryIncrease = 10;
        public int WakeDelayMinMinutes = 45;
        public int WakeDelayMaxMinutes = 120;
        public double RecommendedTemperatureMin;
        public double RecommendedTemperatureMax;
        public double RecommendedHumidityMin;
        public double RecommendedHumidityMax;
        public double TemperatureAdjustment;
        public double HumidityAdjustment;
        public double EnvironmentAdjustmentStaminaCost = 4;
        public double DeepSleepLaydownBonus = .25;
        public double RemLaydownWakeChance = .75;
        public double PacifierLovesCalmGain = 22;
        public double PacifierNeutralCalmGain = 12;
        public double HoldPreparingCryMultiplier = .35;
        public double FeedingHungerReduction = 65;
        public double FeedingCalmGain = 20;
        public double FeedingPreparationStaminaCost = 3;
        public double HoldPreparingExtraStaminaCost = 5;
        public int GradeSLongestMinutes = 300;
        public int GradeALongestMinutes = 240;
        public int GradeBLongestMinutes = 180;
        public int GradeCLongestMinutes = 120;
        public double HungerEarlyThreshold = 35;
        public double HungerActiveThreshold = 60;
        public double HungerLateThreshold = 82;
        public double SleepMinuteGain = 1;
        public double DrowsyCalmThreshold = 65;
        public double SleepStartCalmThreshold = 78;
        public double HoldSleepGain = 12;
        public double PatSleepGain = 9;

        public static V2BalanceConfig Default() => new V2BalanceConfig();
    }

    public sealed class TemperamentModifierDefinition
    {
        public double CribSensitivity;
    }

    public sealed class NightDefinition
    {
        public NightId Id;
        public int ItemSlots;
        public readonly List<ScheduledEventDefinition> Events = new List<ScheduledEventDefinition>();
    }

    public sealed class ScheduledEventDefinition
    {
        public GameEventId EventId;
        public int Turn;
        public double Chance = 1;
    }

    public sealed class VictoryRuleDefinition
    {
        public int RequiredCount;
        public double DeepSleepThreshold;
        public double ParentStaminaThreshold;

        public static VictoryRuleDefinition Default() => new VictoryRuleDefinition
        {
            RequiredCount = 2,
            DeepSleepThreshold = 85,
            ParentStaminaThreshold = 30
        };
    }
}
