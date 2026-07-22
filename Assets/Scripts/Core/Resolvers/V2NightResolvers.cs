using System;
using System.Collections.Generic;

namespace NotANap.Core
{
    public static class V2NightFactory
    {
        private static readonly ItemId[] NewSelectableItems =
            { ItemId.Carrier, ItemId.Pacifier, ItemId.Noise, ItemId.Monitor };
        public static IReadOnlyList<ItemId> SelectableItems => NewSelectableItems;
        public static bool IsSelectableItem(ItemId item) => item != ItemId.Bouncer;

        public static NightState Create(
            RunState run, IReadOnlyList<ItemId> items, BabyProfile profile,
            GameBalanceConfig config, NightModifierId modifier = NightModifierId.None,
            IEnumerable<ProductCapability> capabilities = null)
        {
            var night = NightFactory.CreateNight(run, items, config);
            var source = config.V2.NightModifiers[modifier];
            night.V2 = new V2NightState
            {
                Profile = profile ?? new BabyProfile { Temperament = run.Temperament },
                Modifier = new NightModifierState
                {
                    Id = source.Id,
                    WakeFrequencyMultiplier = source.WakeFrequencyMultiplier,
                    CryEscalationMultiplier = source.CryEscalationMultiplier,
                    SleepGainMultiplier = source.SleepGainMultiplier,
                    ComfortActionModifier = source.ComfortActionModifier,
                    FeedingNeedModifier = source.FeedingNeedModifier
                }
            };
            night.V2.Environment.TemperatureCelsius = 21;
            night.V2.Environment.HumidityPercent = 50;
            // 가정에서 젖병은 평소 세척·소독해 둔 상태가 기본이다.
            // 돌발 상황이 이 값을 false로 바꾼 밤에만 소독 행동이 필요하다.
            night.V2.Feeding.BottleSanitized = true;
            if (capabilities != null) night.V2.ProductCapabilities.UnionWith(capabilities);
            if (night.V2.ProductCapabilities.Contains(ProductCapability.PreSanitizedBottle))
            {
                night.V2.Feeding.BottleSanitized = true;
                night.V2.Feeding.SanitationIncident = false;
            }
            // 세 밤 중 둘째 밤에만 발생하는 결정론적 준비 돌발이다.
            // 사전 소독 제품 능력이 있으면 돌발을 예방한다.
            if (run.CurrentNightId == NightId.SecondNight &&
                !night.V2.ProductCapabilities.Contains(ProductCapability.PreSanitizedBottle))
            {
                night.V2.Feeding.BottleSanitized = false;
                night.V2.Feeding.SanitationIncident = true;
                night.AddEvent(GameEventId.BottleFoundUnsanitized);
            }
            if (night.V2.ProductCapabilities.Contains(ProductCapability.AutoFormulaPrep))
            {
                night.V2.Feeding.WaterReady = true;
                night.V2.Feeding.FormulaMeasured = true;
                night.V2.Feeding.BottleMixed = true;
            }
            if (night.V2.ProductCapabilities.Contains(ProductCapability.TemperatureControl))
            {
                night.V2.Feeding.BottleCooled = true;
                night.V2.Feeding.TemperatureChecked = true;
            }
            return night;
        }
    }

    public static class WakeScheduler
    {
        private static readonly WakeCause[] Causes =
        {
            WakeCause.Hunger, WakeCause.Diaper,
            WakeCause.Temperature, WakeCause.Humidity
        };

        public static ScheduledWake Schedule(NightState night, GameBalanceConfig config, IRandomSource rng)
        {
            RequireV2(night);
            int min = config.V2.WakeDelayMinMinutes;
            int max = config.V2.WakeDelayMaxMinutes;
            int rawDelay = min + rng.NextInt(max - min + 1);
            int delay = Math.Max(1, (int)Math.Round(rawDelay / night.V2.Modifier.WakeFrequencyMultiplier));
            var cause = Causes[rng.NextInt(Causes.Length)];
            var scheduled = new ScheduledWake
            {
                AtElapsedMinute = night.V2.ElapsedMinutes + delay,
                Cause = cause
            };
            night.V2.NextWake = scheduled;
            return scheduled;
        }

        /// <summary>기존 Trace/FutureEvent 예약을 V2 분 단위 각성으로 연결하는 호환 어댑터.</summary>
        public static ScheduledWake ScheduleFromFutureEvent(
            NightState night, EventSeed seed, int minutesPerTriggerUnit)
        {
            RequireV2(night);
            if (seed == null) throw new ArgumentNullException(nameof(seed));
            if (minutesPerTriggerUnit <= 0) throw new ArgumentOutOfRangeException(nameof(minutesPerTriggerUnit));
            var scheduled = new ScheduledWake
            {
                AtElapsedMinute = seed.TriggerTurn * minutesPerTriggerUnit,
                Cause = WakeCause.Unknown,
                SourceFutureEventId = seed.EventId
            };
            night.V2.NextWake = scheduled;
            return scheduled;
        }

        internal static void RequireV2(NightState night)
        {
            if (night?.V2 == null) throw new InvalidOperationException("V2 night state is required.");
        }
    }

    public static class V2TimeResolver
    {
        public static void Advance(RunState run, NightState night, int minutes,
            GameBalanceConfig config, IRandomSource rng)
        {
            WakeScheduler.RequireV2(night);
            if (minutes < 0) throw new ArgumentOutOfRangeException(nameof(minutes));
            int target = Math.Min(config.V2.NightDurationMinutes, night.V2.ElapsedMinutes + minutes);
            while (night.V2.ElapsedMinutes < target)
            {
                int step = target - night.V2.ElapsedMinutes;
                if (night.V2.NextWake != null && !night.V2.NextWake.Triggered)
                    step = Math.Min(step, Math.Max(0, night.V2.NextWake.AtElapsedMinute - night.V2.ElapsedMinutes));

                if (step > 0) AdvanceContinuous(run, night, step, config);

                if (night.V2.NextWake != null && !night.V2.NextWake.Triggered &&
                    night.V2.ElapsedMinutes >= night.V2.NextWake.AtElapsedMinute)
                {
                    TriggerWake(night, night.V2.NextWake.Cause, config);
                    night.V2.NextWake.Triggered = true;
                }
                else if (step == 0) break;
            }

            night.Hour = (GameConfig.StartHour + night.V2.ElapsedMinutes / 60) % 24;
            if (night.V2.ElapsedMinutes >= config.V2.NightDurationMinutes)
            {
                night.Over = true;
                night.V2.Metrics.ParentStaminaAtDawn = night.Parent.Stamina;
                night.Stats.StaminaLeft = night.Parent.Stamina;
                night.Stats.Wakes = night.V2.Metrics.WakeCount;
                bool sleepingAtDawn = night.V2.SleepCycle.Stage == V2SleepStage.NremDeepSleep ||
                                      night.V2.SleepCycle.Stage == V2SleepStage.RemActiveSleep;
                night.Result = sleepingAtDawn
                    ? (night.Baby.Held ? NightOutcome.Arms : NightOutcome.Crib)
                    : NightOutcome.Awake;
                night.AddEvent(GameEventId.NightCompleted);
            }
        }

        private static void AdvanceContinuous(RunState run, NightState night, int minutes, GameBalanceConfig config)
        {
            var v2 = night.V2;
            night.Baby.Hunger = CoreMath.Clamp(night.Baby.Hunger + minutes * .25, 0, 100);
            if (night.Wearing.Noise && night.HasItem(ItemId.Noise) && !night.NoiseDisabled)
            {
                double effectiveness = 1 - run.GetEffectiveMemory().NoiseHab;
                night.Baby.Calm = CoreMath.Clamp(night.Baby.Calm + minutes * .4 * effectiveness, 0, 100);
                night.Stats.NoiseTurns += Math.Max(1, (int)Math.Ceiling(minutes / 15d));
            }
            bool sleeping = v2.SleepCycle.Stage == V2SleepStage.RemActiveSleep ||
                            v2.SleepCycle.Stage == V2SleepStage.NremDeepSleep;
            if (sleeping)
            {
                v2.Metrics.RecordSleep(minutes);
                v2.SleepCycle.CurrentSleepStretchMinutes = v2.Metrics.CurrentSleepStretchMinutes;
                v2.SleepCycle.MinutesInStage += minutes;
                if (v2.SleepCycle.Stage == V2SleepStage.RemActiveSleep && v2.SleepCycle.MinutesInStage >= 30)
                    SetStage(v2.SleepCycle, V2SleepStage.NremDeepSleep);
                else if (v2.SleepCycle.Stage == V2SleepStage.NremDeepSleep && v2.SleepCycle.MinutesInStage >= 60)
                    SetStage(v2.SleepCycle, V2SleepStage.RemActiveSleep);
            }
            else if (night.Baby.Crying)
            {
                double holdFactor = v2.HoldWhilePreparing ? config.V2.HoldPreparingCryMultiplier : 1;
                v2.CryIntensity = CoreMath.Clamp(v2.CryIntensity + minutes * .2 *
                    v2.Modifier.CryEscalationMultiplier * holdFactor, 0, 100);
            }
            v2.ElapsedMinutes += minutes;
        }

        public static void BeginSleep(NightState night, V2SleepStage stage)
        {
            WakeScheduler.RequireV2(night);
            SetStage(night.V2.SleepCycle, stage);
            night.Baby.Crying = false;
        }

        public static void SetDrowsy(NightState night)
        {
            WakeScheduler.RequireV2(night);
            SetStage(night.V2.SleepCycle, V2SleepStage.Drowsy);
        }

        public static void TriggerWake(NightState night, WakeCause cause, GameBalanceConfig config)
        {
            WakeScheduler.RequireV2(night);
            night.V2.Metrics.RecordWake();
            night.V2.SleepCycle.CurrentSleepStretchMinutes = 0;
            SetStage(night.V2.SleepCycle, V2SleepStage.Awake);
            night.Baby.Crying = true;
            night.V2.CryIntensity = Math.Max(night.V2.CryIntensity, 20);
            night.V2.Diagnosis.Begin(cause, config.V2.DecisionSeconds);
            if (cause == WakeCause.Hunger)
                night.Baby.Hunger = Math.Max(night.Baby.Hunger, config.V2.HungerLateThreshold);
            else if (cause == WakeCause.Temperature)
                night.V2.Environment.TemperatureCelsius = config.V2.RecommendedTemperatureMax + 5;
            else if (cause == WakeCause.Humidity)
                night.V2.Environment.HumidityPercent = config.V2.RecommendedHumidityMin - 10;
            night.AddEvent(GameEventId.BabyFullyWoke);
        }

        public static bool TrySelfResettle(RunState run, NightState night, IRandomSource rng)
        {
            WakeScheduler.RequireV2(night);
            if (night.V2.Diagnosis.ActiveCause != WakeCause.NaturalCycle ||
                rng.NextDouble() >= CoreMath.Clamp01(run.Memory.SelfSoothe)) return false;
            night.V2.Diagnosis.CauseResolved = true;
            BeginSleep(night, V2SleepStage.RemActiveSleep);
            TraceRecorder.FromAction(run.Traces, CoreTraceIds.SelfResettled, ActionId.Watch,
                night.NightId, night.V2.ElapsedMinutes, .5);
            night.AddEvent(GameEventId.TraceCreated);
            return true;
        }

        private static void SetStage(SleepCycleState state, V2SleepStage stage)
        {
            state.Stage = stage;
            state.MinutesInStage = 0;
            state.IsLimbRelaxed = stage == V2SleepStage.NremDeepSleep;
            state.IsBreathingRegular = stage == V2SleepStage.NremDeepSleep;
        }
    }

    public static class NightEvaluationResolver
    {
        public static NightEvaluation Evaluate(NightState night, GameBalanceConfig config)
        {
            WakeScheduler.RequireV2(night);
            var metrics = night.V2.Metrics;
            NightGrade grade;
            if (metrics.LongestSleepStretchMinutes >= config.V2.GradeSLongestMinutes && metrics.UnsafeChoiceCount == 0)
                grade = NightGrade.S;
            else if (metrics.LongestSleepStretchMinutes >= config.V2.GradeALongestMinutes) grade = NightGrade.A;
            else if (metrics.LongestSleepStretchMinutes >= config.V2.GradeBLongestMinutes) grade = NightGrade.B;
            else if (metrics.LongestSleepStretchMinutes >= config.V2.GradeCLongestMinutes) grade = NightGrade.C;
            else grade = NightGrade.D;
            return new NightEvaluation { Grade = grade, Metrics = metrics };
        }
    }
}
