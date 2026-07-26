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
            // 세 밤의 계절 환경은 NightId로 결정해 저장/재실행 시 동일하게 재현한다.
            night.V2.Environment.Season = run.CurrentNightId == NightId.FirstNight
                ? RoomSeason.Summer : RoomSeason.Winter;
            night.V2.Environment.TemperatureCelsius =
                night.V2.Environment.Season == RoomSeason.Summer
                    ? config.V2.SummerScenarioTemperature
                    : config.V2.WinterScenarioTemperature;
            night.V2.Environment.HumidityPercent = 50;
            night.V2.Environment.BabyTemperatureCelsius = 36.7;
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

    public static class HomeMovementResolver
    {
        public static HomeMoveOutcome MoveTo(RunState run, NightState night, HomeLocation destination,
            GameBalanceConfig config, IRandomSource rng)
        {
            WakeScheduler.RequireV2(night);
            var from = night.V2.CaregiverLocation;
            var outcome = new HomeMoveOutcome
            {
                From = from,
                To = destination,
                BabyAccompanied = night.Baby.Held
            };
            if (night.Over || from == destination) return outcome;
            // 방 이동도 시간을 소비하므로 체력 0에서의 시간 소진 우회로로 쓰지 못한다.
            if (night.Parent.Stamina <= 0) return outcome;

            outcome.Accepted = true;
            outcome.TimeDeltaMinutes = TravelMinutes(from, destination);
            night.V2.CaregiverLocation = destination;
            if (destination == HomeLocation.Bathroom && !night.V2.BathThermometerRetrieved)
            {
                night.V2.BathThermometerRetrieved = true;
                outcome.RetrievedBathThermometer = true;
            }
            V2TimeResolver.Advance(run, night, outcome.TimeDeltaMinutes, config, rng);
            night.V2.ActionAudit.Add(new ActionAuditEntry
            {
                Kind = NightAuditKind.Movement,
                Accepted = true,
                TimeDeltaMinutes = outcome.TimeDeltaMinutes,
                EncounterSequence = night.V2.Diagnosis.EncounterSequence,
                ElapsedMinutes = night.V2.ElapsedMinutes,
                MovementDestination = destination
            });
            return outcome;
        }

        public static int TravelMinutes(HomeLocation from, HomeLocation to)
        {
            if (from == to) return 0;
            if ((from == HomeLocation.Kitchen && to == HomeLocation.Bathroom) ||
                (from == HomeLocation.Bathroom && to == HomeLocation.Kitchen))
                return 3;
            return 2;
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
            FinalNightResolver.RunScheduledEvents(run, night, rng);
            if (night.V2.ElapsedMinutes >= config.V2.NightDurationMinutes)
            {
                night.Over = true;
                night.V2.Metrics.ParentStaminaAtDawn = night.Parent.Stamina;
                night.Stats.StaminaLeft = night.Parent.Stamina;
                night.Stats.Wakes = night.V2.Metrics.WakeCount;
                night.Stats.WatchOk = night.V2.GentleObservationCount;
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
            if (night.Wearing.Carrier)
                night.Stats.CarrierTurns += Math.Max(1, (int)Math.Ceiling(minutes / 15d));
            bool sleeping = v2.SleepCycle.Stage == V2SleepStage.RemActiveSleep ||
                            v2.SleepCycle.Stage == V2SleepStage.NremDeepSleep;
            if (sleeping)
            {
                if (night.Baby.Held)
                    night.Stats.HeldSleepTurns += Math.Max(1, (int)Math.Ceiling(minutes / 15d));
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
                night.V2.Environment.TemperatureCelsius =
                    night.V2.Environment.Season == RoomSeason.Summer
                        ? config.V2.SummerScenarioTemperature
                        : config.V2.WinterScenarioTemperature;
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

    public static class V2SleepIntervalResolver
    {
        public static bool Apply(RunState run, NightState night, SleepIntervalChoice choice,
            GameBalanceConfig config, IRandomSource rng)
        {
            WakeScheduler.RequireV2(night);
            if (night.Over) return false;
            var stage = night.V2.SleepCycle.Stage;
            if (stage != V2SleepStage.RemActiveSleep && stage != V2SleepStage.NremDeepSleep)
                return false;

            switch (choice)
            {
                case SleepIntervalChoice.RestTogether:
                    night.Parent.Stamina = CoreMath.Clamp(night.Parent.Stamina +
                        config.V2.SleepRestStaminaRecovery, 0, 100);
                    break;
                case SleepIntervalChoice.CheckEnvironment:
                    night.V2.Environment.IsTemperatureChecked = true;
                    night.V2.Environment.IsHumidityChecked = true;
                    break;
                case SleepIntervalChoice.PrepareNextFeed:
                    night.Parent.Stamina = CoreMath.Clamp(night.Parent.Stamina -
                        config.V2.SleepPreparationStaminaCost, 0, 100);
                    night.V2.Feeding.WaterReady = true;
                    night.V2.Feeding.FormulaMeasured = true;
                    night.V2.Feeding.BottleMixed = true;
                    break;
            }

            int target = night.V2.NextWake != null && !night.V2.NextWake.Triggered
                ? night.V2.NextWake.AtElapsedMinute
                : config.V2.NightDurationMinutes;
            int intervalMinutes = Math.Max(0, target - night.V2.ElapsedMinutes);
            V2TimeResolver.Advance(run, night, intervalMinutes, config, rng);
            night.V2.ActionAudit.Add(new ActionAuditEntry
            {
                Kind = NightAuditKind.SleepInterval,
                Accepted = true,
                TimeDeltaMinutes = intervalMinutes,
                EncounterSequence = night.V2.Diagnosis.EncounterSequence,
                ElapsedMinutes = night.V2.ElapsedMinutes,
                IntervalChoice = choice
            });
            return true;
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
