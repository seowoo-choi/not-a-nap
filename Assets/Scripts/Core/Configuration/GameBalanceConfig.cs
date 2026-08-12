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
        public int DiaperCheckMinutes = 1;
        public double DiaperCheckStaminaCost = 1;
        // 소변 처리 전체(확인+갈기+버리기): 10분, 체력 -4.
        public int DiaperChangeMinutes = 7;
        public double DiaperChangeStaminaCost = 2;
        public int DiaperDisposeMinutes = 2;
        public double DiaperDisposeStaminaCost = 1;
        // 대변 처리 전체(확인+갈기+버리기+손 씻기): 20분, 체력 -8.
        public int DiaperStoolChangeMinutes = 13;
        public double DiaperStoolChangeStaminaCost = 4;
        public int DiaperStoolDisposeMinutes = 4;
        public double DiaperStoolDisposeStaminaCost = 2;
        public int WashHandsMinutes = 2;
        public double WashHandsStaminaCost = 1;
        public double DiaperStoolChance = .25;
        public double DiaperStoolCryIncrease = 8;
        public int DiaperCleanRecommendationCooldownMinutes = 75;
        public int DiaperWakeSpacingMinutes = 90;
        public int MaxDiaperWakesPerNight = 3;
        public int PreparationActionMinutes = 15;
        public int DecisionSeconds = 20;
        public double MisdiagnosisStaminaPenalty = 6;
        public double MisdiagnosisCryIncrease = 12;
        public double HesitationStaminaPenalty = 4;
        public double HesitationCryIncrease = 10;
        public int WakeDelayMinMinutes = 45;
        public int WakeDelayMaxMinutes = 120;
        public double RecommendedTemperatureMin = 20;
        public double RecommendedTemperatureMax = 22;
        // 환경 원인일 때는 권장 범위와 최소 3°C 차이를 두어 실제로
        // 불편한 수치라는 점이 관찰만으로 읽히게 한다.
        public double SummerScenarioTemperature = 25;
        public double WinterScenarioTemperature = 17;
        public double RecommendedHumidityMin = 40;
        public double RecommendedHumidityMax = 60;
        // 온습도 조절은 고정 증감이 아니라 위 권장 밴드로 클램프한다(V2ActionResolver).
        public double EnvironmentAdjustmentStaminaCost = 4;
        public double DeepSleepLaydownBonus = .25;
        public double RemLaydownWakeChance = .45;
        public double PacifierLovesCalmGain = 22;
        public double PacifierNeutralCalmGain = 12;
        // 백색소음기는 아기를 달래는 물건이 아니다. 켜 두면 진정도가 저절로 오르던
        // 예전 규칙은 "켜 두면 돌보지 않아도 된다"로 읽혔고 실제 백색소음의 역할과도
        // 달랐다. 지금은 이미 든 잠을 '이어 주는' 쪽으로만 관여한다.
        public int NoiseWakeDelayBonusMinutes = 25;
        public double NoiseSelfResettleBonus = .15;
        public double NoiseExternalWakeGuard = .6;
        // 외부 소음 돌발. 둘째 밤 23:30 초인종, 백일째 밤 02:30 윗집 소리.
        public int SecondNightExternalNoiseMinute = 150;
        public int FinalNightExternalNoiseMinute = 330;
        // 아기띠는 두 손을 비워 준다. 안고 준비할 때 붙는 추가 체력 소모를 없애고
        // 방을 옮기는 동안의 울음 상승도 눌러 준다.
        public double CarrierCryMultiplier = .55;
        // 모니터로 본 아기 상태가 유효한 시간. 지나면 화면이 다시 막히고
        // 아기가 어떤지 알려면 한 번 더 봐야 한다.
        public int MonitorReadFreshMinutes = 30;
        // 아기 기분(0~100) 가중치. 울음과 배고픔이 진정도를 깎는 정도.
        public double MoodCryWeight = .6;
        public double MoodHungerWeight = .4;
        public double HoldPreparingCryMultiplier = .35;
        public double FeedingHungerReduction = 65;
        public double FeedingCalmGain = 20;
        public double FeedingPreparationStaminaCost = 3;
        public double HoldPreparingExtraStaminaCost = 5;
        public double SleepRestStaminaRecovery = 15;
        public double SleepPreparationStaminaCost = 3;
        public int GradeSLongestMinutes = 300;
        public int GradeALongestMinutes = 240;
        public int GradeBLongestMinutes = 180;
        public int GradeCLongestMinutes = 120;
        // 피로는 진정도가 아니라 '깨어 있던 시간'으로 쌓인다. 놓치면 과각성이 되어
        // 오히려 달래기가 힘들어진다는 육아 도메인 규칙을 그대로 옮겼다.
        public int FatigueEarlyMinutes = 45;
        public int FatigueActiveMinutes = 75;
        public int FatigueOvertiredMinutes = 105;
        public double OvertiredComfortMultiplier = .7;
        public double HungerEarlyThreshold = 35;
        public double HungerActiveThreshold = 60;
        public double HungerLateThreshold = 82;
        // 이 위로는 자극을 줄여야 하는 울음 구간. 화면 게이지의 경고 눈금도 이 값을 쓴다.
        public double CryWarningThreshold = 35;
        public double SleepMinuteGain = 1;
        public double DrowsyCalmThreshold = 65;
        public double SleepStartCalmThreshold = 78;
        public double HoldSleepGain = 12;
        public double PatSleepGain = 9;
        // 원인을 못 찾은 채 토닥이면 아기는 절반만 진정한다. 무진단 연타를
        // 막으면서도 "일단 달래보기"라는 실제 육아 행동은 남겨 둔다.
        public double UnresolvedCauseComfortMultiplier = .5;
        // 원인을 아직 해소하지 못한 각성에서도 안기·토닥임은 울음을 실제로 누그러뜨린다.
        // 이 완화가 없으면 배고픔·온습도 각성에서 CryIntensity가 단조 증가만 해
        // 플레이어가 무엇을 하든 울음이 새벽까지 커지기만 한다.
        public double ComfortCryRelief = 9;
        // 쪽쪽이·백색소음·아기띠는 원인을 해소하지 않지만 한 각성에 한 번은
        // 실제로 아기를 달랜다. 시간 비용과 1회 제한으로 토글 연타를 막는다.
        public double SoothingItemCryRelief = 6;
        public int SoothingItemMinutes = 2;

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
