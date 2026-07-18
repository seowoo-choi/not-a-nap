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
        public readonly Dictionary<string, TemperamentModifierDefinition> TemperamentModifiers =
            new Dictionary<string, TemperamentModifierDefinition>
            {
                { "soft", new TemperamentModifierDefinition { CribSensitivity = .10 } },
                { "sensitive", new TemperamentModifierDefinition { CribSensitivity = .32 } },
                { "hungry", new TemperamentModifierDefinition { CribSensitivity = .18 } }
            };

        public static GameBalanceConfig Default() => new GameBalanceConfig();
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
