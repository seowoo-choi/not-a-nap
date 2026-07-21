using System;
using System.Collections.Generic;

namespace NotANap.Core
{
    public static class ObservationResolver
    {
        public static HungerSignalStage GetHungerStage(double hunger, V2BalanceConfig config)
        {
            if (hunger >= config.HungerLateThreshold) return HungerSignalStage.Late;
            if (hunger >= config.HungerActiveThreshold) return HungerSignalStage.Active;
            if (hunger >= config.HungerEarlyThreshold) return HungerSignalStage.Early;
            return HungerSignalStage.None;
        }

        public static void AddHungerSignals(HungerSignalStage stage, IList<ObservationSignalId> output)
        {
            if (stage >= HungerSignalStage.Early)
            {
                output.Add(ObservationSignalId.LipSmacking);
                output.Add(ObservationSignalId.MouthOpening);
                output.Add(ObservationSignalId.HandSucking);
            }
            if (stage >= HungerSignalStage.Active)
            {
                output.Add(ObservationSignalId.Rooting);
                output.Add(ObservationSignalId.LeaningToCaregiver);
                output.Add(ObservationSignalId.Squirming);
                output.Add(ObservationSignalId.HeadTurning);
            }
            if (stage >= HungerSignalStage.Late)
            {
                output.Add(ObservationSignalId.RapidBreathing);
                output.Add(ObservationSignalId.HungerCry);
            }
        }

        public static void AddSleepSignals(SleepCycleState sleep, IList<ObservationSignalId> output)
        {
            if (sleep.Stage == V2SleepStage.RemActiveSleep)
            {
                output.Add(ObservationSignalId.EyelidFlutter);
                output.Add(ObservationSignalId.IrregularBreathing);
                output.Add(ObservationSignalId.FacialMovement);
                output.Add(ObservationSignalId.LimbMovement);
            }
            else if (sleep.Stage == V2SleepStage.NremDeepSleep)
            {
                output.Add(ObservationSignalId.RegularBreathing);
                output.Add(ObservationSignalId.CalmFace);
                output.Add(ObservationSignalId.RelaxedLimbs);
            }
        }
    }

    public static class V2ActionResolver
    {
        public static V2ActionOutcome Apply(RunState run, NightState night, V2ActionId action,
            GameBalanceConfig config, IRandomSource rng)
        {
            WakeScheduler.RequireV2(night);
            var outcome = new V2ActionOutcome { Action = action, Accepted = true };
            if (night.Over) return Reject(outcome);

            switch (action)
            {
                case V2ActionId.CheckDiaper:
                    Diagnose(run, night, WakeCause.Diaper, outcome, config);
                    break;
                case V2ActionId.ChangeDiaper:
                    Consume(outcome, config.V2.DiagnosisActionMinutes, -4);
                    if (night.V2.Diagnosis.ActiveCause == WakeCause.Diaper &&
                        night.V2.Diagnosis.CheckedCauses.Contains(WakeCause.Diaper))
                        ResolveCause(night, outcome);
                    else ApplyMisdiagnosis(night, outcome, config);
                    break;
                case V2ActionId.CheckHungerSignals:
                    Diagnose(run, night, WakeCause.Hunger, outcome, config);
                    outcome.HungerSignalStage = ObservationResolver.GetHungerStage(night.Baby.Hunger, config.V2);
                    ObservationResolver.AddHungerSignals(outcome.HungerSignalStage, outcome.ObservedSignals);
                    break;
                case V2ActionId.CheckEnvironment:
                    Consume(outcome, config.V2.DiagnosisActionMinutes, -2);
                    night.V2.Environment.IsTemperatureChecked = true;
                    night.V2.Environment.IsHumidityChecked = true;
                    RegisterCheck(run, night, night.V2.Diagnosis.ActiveCause == WakeCause.Humidity
                        ? WakeCause.Humidity : WakeCause.Temperature, outcome, config, true);
                    break;
                case V2ActionId.AdjustTemperature:
                    Consume(outcome, config.V2.DefaultActionMinutes, -config.V2.EnvironmentAdjustmentStaminaCost);
                    night.V2.Environment.TemperatureCelsius = CoreMath.Clamp(
                        night.V2.Environment.TemperatureCelsius,
                        config.V2.RecommendedTemperatureMin, config.V2.RecommendedTemperatureMax);
                    if (night.V2.Diagnosis.ActiveCause == WakeCause.Temperature && night.V2.Environment.IsTemperatureChecked)
                        ResolveCause(night, outcome);
                    break;
                case V2ActionId.AdjustHumidity:
                    Consume(outcome, config.V2.DefaultActionMinutes, -config.V2.EnvironmentAdjustmentStaminaCost);
                    night.V2.Environment.HumidityPercent = CoreMath.Clamp(
                        night.V2.Environment.HumidityPercent,
                        config.V2.RecommendedHumidityMin, config.V2.RecommendedHumidityMax);
                    if (night.V2.Diagnosis.ActiveCause == WakeCause.Humidity && night.V2.Environment.IsHumidityChecked)
                        ResolveCause(night, outcome);
                    break;
                case V2ActionId.Hesitate:
                    Consume(outcome, config.V2.DiagnosisActionMinutes, -config.V2.HesitationStaminaPenalty);
                    night.V2.CryIntensity = CoreMath.Clamp(night.V2.CryIntensity +
                        config.V2.HesitationCryIncrease * night.V2.Modifier.CryEscalationMultiplier, 0, 100);
                    break;
                case V2ActionId.CheckLimbRelaxation:
                    outcome.HungerSignalStage = HungerSignalStage.None;
                    ObservationResolver.AddSleepSignals(night.V2.SleepCycle, outcome.ObservedSignals);
                    if (night.V2.SleepCycle.Stage == V2SleepStage.NremDeepSleep &&
                        night.V2.SleepCycle.IsLimbRelaxed)
                        night.V2.SleepCycle.DeepSleepObserved = true;
                    break;
                case V2ActionId.Laydown:
                    if (!night.Baby.Held) return Reject(outcome, V2ActionBlockReason.BabyNotHeld);
                    if (night.V2.SleepCycle.Stage != V2SleepStage.RemActiveSleep &&
                        night.V2.SleepCycle.Stage != V2SleepStage.NremDeepSleep)
                        return Reject(outcome, V2ActionBlockReason.BabyNotAsleep);
                    ApplyLaydown(run, night, outcome, config, rng);
                    break;
                case V2ActionId.Pacifier:
                    if (!night.HasItem(ItemId.Pacifier))
                        return Reject(outcome, V2ActionBlockReason.ItemUnavailable);
                    ApplyPacifier(run, night, outcome, config);
                    break;
                case V2ActionId.ToggleNoise:
                    if (!night.HasItem(ItemId.Noise) || night.NoiseDisabled)
                        return Reject(outcome, V2ActionBlockReason.ItemUnavailable);
                    night.Wearing.Noise = !night.Wearing.Noise;
                    break;
                case V2ActionId.CheckMonitor:
                    if (!night.HasItem(ItemId.Monitor))
                        return Reject(outcome, V2ActionBlockReason.ItemUnavailable);
                    outcome.MonitorRead = true;
                    break;
                case V2ActionId.Hold:
                case V2ActionId.Pat:
                    Consume(outcome, config.V2.DefaultActionMinutes, action == V2ActionId.Hold ? -8 : -4);
                    night.Baby.Calm = CoreMath.Clamp(night.Baby.Calm +
                        12 * night.V2.Modifier.ComfortActionModifier, 0, 100);
                    night.Baby.Sleep = CoreMath.Clamp(night.Baby.Sleep +
                        (action == V2ActionId.Hold ? config.V2.HoldSleepGain : config.V2.PatSleepGain) *
                        night.V2.Modifier.SleepGainMultiplier, 0, 100);
                    if (!night.V2.Diagnosis.CauseResolved &&
                        (night.V2.Diagnosis.ActiveCause == WakeCause.NaturalCycle ||
                         night.V2.Diagnosis.ActiveCause == WakeCause.MoroReflex))
                        ResolveCause(night, outcome);
                    if (!night.V2.Diagnosis.CauseResolved && night.V2.Diagnosis.ActiveCause == WakeCause.Diaper)
                        ApplyMisdiagnosis(night, outcome, config);
                    else if (night.V2.Diagnosis.CauseResolved)
                    {
                        if (night.Baby.Calm >= config.V2.SleepStartCalmThreshold)
                        {
                            V2TimeResolver.BeginSleep(night, V2SleepStage.RemActiveSleep);
                            if (night.V2.NextWake == null || night.V2.NextWake.Triggered)
                                WakeScheduler.Schedule(night, config, rng);
                        }
                        else if (night.Baby.Calm >= config.V2.DrowsyCalmThreshold)
                            V2TimeResolver.SetDrowsy(night);
                    }
                    break;
                case V2ActionId.SterilizeBottle:
                    Prepare(night, outcome, config, FeedingPreparationStep.SanitizeBottle);
                    night.V2.Feeding.BottleSanitized = true;
                    break;
                case V2ActionId.PrepareWater:
                    Prepare(night, outcome, config, FeedingPreparationStep.PrepareWater);
                    night.V2.Feeding.WaterReady = true;
                    break;
                case V2ActionId.MeasureFormula:
                    Prepare(night, outcome, config, FeedingPreparationStep.MeasureFormula);
                    night.V2.Feeding.FormulaMeasured = true;
                    break;
                case V2ActionId.MixFormula:
                    if (!night.V2.Feeding.WaterReady || !night.V2.Feeding.FormulaMeasured) return Reject(outcome);
                    Prepare(night, outcome, config, FeedingPreparationStep.MixFormula);
                    night.V2.Feeding.BottleMixed = true;
                    break;
                case V2ActionId.CoolBottle:
                    if (!night.V2.Feeding.BottleMixed) return Reject(outcome);
                    Prepare(night, outcome, config, FeedingPreparationStep.CoolBottle);
                    night.V2.Feeding.BottleCooled = true;
                    break;
                case V2ActionId.CheckBottleTemperature:
                    if (!night.V2.Feeding.BottleCooled) return Reject(outcome);
                    Prepare(night, outcome, config, FeedingPreparationStep.CheckTemperature);
                    night.V2.Feeding.TemperatureChecked = true;
                    if (night.V2.Feeding.IsReadyToFeed)
                        AddTrace(run, night, outcome, CoreTraceIds.FeedingPreparationCompleted, ActionId.CheckBottleTemperature);
                    break;
                case V2ActionId.HoldWhilePreparing:
                    Consume(outcome, config.V2.DefaultActionMinutes, -config.V2.HoldPreparingExtraStaminaCost);
                    night.V2.HoldWhilePreparing = true;
                    night.Baby.Held = true;
                    break;
                case V2ActionId.FeedPreparedBottle:
                    ApplyPreparedFeed(run, night, outcome, config);
                    break;
            }

            ApplyOutcomeAndTime(run, night, outcome, config, rng);
            return outcome;
        }

        public static V2ActionOutcome ApplyDecisionTimeout(RunState run, NightState night,
            GameBalanceConfig config, IRandomSource rng) => Apply(run, night, V2ActionId.Hesitate, config, rng);

        private static void Diagnose(RunState run, NightState night, WakeCause cause,
            V2ActionOutcome outcome, GameBalanceConfig config, bool penalizeWrong = true)
        {
            Consume(outcome, config.V2.DiagnosisActionMinutes, -2);
            RegisterCheck(run, night, cause, outcome, config, penalizeWrong);
        }

        private static void RegisterCheck(RunState run, NightState night, WakeCause cause,
            V2ActionOutcome outcome, GameBalanceConfig config, bool penalizeWrong)
        {
            var diagnosis = night.V2.Diagnosis;
            if (diagnosis.CauseResolved) return;
            diagnosis.CheckAttempts++;
            if (diagnosis.FirstCheck == WakeCause.Unknown)
            {
                diagnosis.FirstCheck = cause;
                if (cause == diagnosis.ActiveCause)
                {
                    night.V2.Metrics.CorrectFirstChecks++;
                    if (cause == WakeCause.Diaper)
                        AddTrace(run, night, outcome, CoreTraceIds.DiaperCheckedFirst, ActionId.CheckDiaper);
                }
            }
            diagnosis.CheckedCauses.Add(cause);
            if (cause == diagnosis.ActiveCause && diagnosis.MisdiagnosisCount > 0)
                AddTrace(run, night, outcome, CoreTraceIds.CauseRecheckedAfterMismatch,
                    cause == WakeCause.Diaper ? ActionId.CheckDiaper : ActionId.CheckHungerSignals);
            else if (cause != diagnosis.ActiveCause && penalizeWrong)
                ApplyMisdiagnosis(night, outcome, config);
        }

        private static void ApplyMisdiagnosis(NightState night, V2ActionOutcome outcome, GameBalanceConfig config)
        {
            night.V2.Diagnosis.MisdiagnosisCount++;
            night.V2.Metrics.MisdiagnosisCount++;
            outcome.StaminaDelta -= config.V2.MisdiagnosisStaminaPenalty;
            night.V2.CryIntensity = CoreMath.Clamp(night.V2.CryIntensity +
                config.V2.MisdiagnosisCryIncrease * night.V2.Modifier.CryEscalationMultiplier, 0, 100);
        }

        private static void ResolveCause(NightState night, V2ActionOutcome outcome)
        {
            night.V2.Diagnosis.CauseResolved = true;
            outcome.CauseResolved = true;
            night.Baby.Crying = false;
            night.V2.CryIntensity = Math.Max(0, night.V2.CryIntensity - 25);
        }

        private static void ApplyLaydown(RunState run, NightState night, V2ActionOutcome outcome,
            GameBalanceConfig config, IRandomSource rng)
        {
            Consume(outcome, config.V2.DefaultActionMinutes, -4);
            double chance = ActionResolver.CalculateLaydownSuccessProbability(run, night, config);
            bool deepObserved = night.V2.SleepCycle.Stage == V2SleepStage.NremDeepSleep &&
                                night.V2.SleepCycle.IsLimbRelaxed && night.V2.SleepCycle.DeepSleepObserved;
            if (deepObserved) chance = CoreMath.Clamp(chance + config.V2.DeepSleepLaydownBonus, 0, 1);
            if (night.V2.SleepCycle.Stage == V2SleepStage.RemActiveSleep)
                chance *= 1 - config.V2.RemLaydownWakeChance;
            if (rng.NextDouble() < chance)
            {
                night.Baby.Held = false;
                outcome.EventIds.Add(GameEventId.LaydownSucceeded);
                if (deepObserved)
                    AddTrace(run, night, outcome, CoreTraceIds.DeepSleepObservedBeforeLaydown, ActionId.Laydown);
                if (night.V2.NextWake == null || night.V2.NextWake.Triggered)
                    WakeScheduler.Schedule(night, config, rng);
            }
            else
            {
                outcome.EventIds.Add(GameEventId.LaydownFailed);
                V2TimeResolver.TriggerWake(night, night.V2.SleepCycle.Stage == V2SleepStage.RemActiveSleep
                    ? WakeCause.MoroReflex : WakeCause.NaturalCycle, config);
            }
        }

        private static void ApplyPacifier(RunState run, NightState night, V2ActionOutcome outcome,
            GameBalanceConfig config)
        {
            if (night.V2.Profile.PacifierAffinity == PacifierAffinity.Rejects)
            {
                outcome.Accepted = false;
                AddTrace(run, night, outcome, CoreTraceIds.PacifierRejected, ActionId.Pacifier);
                return;
            }
            Consume(outcome, config.V2.DefaultActionMinutes, -1);
            double gain = night.V2.Profile.PacifierAffinity == PacifierAffinity.Loves
                ? config.V2.PacifierLovesCalmGain : config.V2.PacifierNeutralCalmGain;
            night.Baby.Calm = CoreMath.Clamp(night.Baby.Calm + gain, 0, 100);
            AddTrace(run, night, outcome, CoreTraceIds.PacifierAccepted, ActionId.Pacifier);
        }

        private static void Prepare(NightState night, V2ActionOutcome outcome,
            GameBalanceConfig config, FeedingPreparationStep step)
        {
            Consume(outcome, config.V2.PreparationActionMinutes, -config.V2.FeedingPreparationStaminaCost);
        }

        private static void ApplyPreparedFeed(RunState run, NightState night,
            V2ActionOutcome outcome, GameBalanceConfig config)
        {
            var feeding = night.V2.Feeding;
            AddMissingSteps(feeding, outcome.MissingPreparationSteps);
            if (!feeding.IsReadyToFeed)
            {
                outcome.Accepted = false;
                AddTrace(run, night, outcome, CoreTraceIds.FeedingAttemptedBeforeReady, ActionId.FeedPreparedBottle);
                return;
            }
            Consume(outcome, config.V2.DefaultActionMinutes, -4);
            if (night.V2.Diagnosis.ActiveCause == WakeCause.Hunger && !night.V2.Diagnosis.CauseResolved)
            {
                night.Baby.Hunger = CoreMath.Clamp(night.Baby.Hunger - config.V2.FeedingHungerReduction, 0, 100);
                night.Baby.Calm = CoreMath.Clamp(night.Baby.Calm + config.V2.FeedingCalmGain, 0, 100);
                ResolveCause(night, outcome);
            }
            else if (!night.V2.Diagnosis.CauseResolved)
                ApplyMisdiagnosis(night, outcome, config);
        }

        private static void AddMissingSteps(FeedingPreparationState state, IList<FeedingPreparationStep> output)
        {
            if (!state.BottleSanitized) output.Add(FeedingPreparationStep.SanitizeBottle);
            if (!state.WaterReady) output.Add(FeedingPreparationStep.PrepareWater);
            if (!state.FormulaMeasured) output.Add(FeedingPreparationStep.MeasureFormula);
            if (!state.BottleMixed) output.Add(FeedingPreparationStep.MixFormula);
            if (!state.BottleCooled) output.Add(FeedingPreparationStep.CoolBottle);
            if (!state.TemperatureChecked) output.Add(FeedingPreparationStep.CheckTemperature);
        }

        private static void AddTrace(RunState run, NightState night, V2ActionOutcome outcome,
            TraceId id, ActionId action)
        {
            TraceRecorder.FromAction(run.Traces, id, action, night.NightId, night.V2.ElapsedMinutes, .5);
            outcome.TraceIds.Add(id);
            outcome.EventIds.Add(GameEventId.TraceCreated);
        }

        private static void Consume(V2ActionOutcome outcome, int minutes, double stamina)
        {
            outcome.ConsumedTime = true;
            outcome.TimeDeltaMinutes += minutes;
            outcome.StaminaDelta += stamina;
        }

        private static V2ActionOutcome Reject(V2ActionOutcome outcome,
            V2ActionBlockReason reason = V2ActionBlockReason.None)
        {
            outcome.Accepted = false;
            outcome.BlockReason = reason;
            outcome.ConsumedTime = false;
            outcome.TimeDeltaMinutes = 0;
            return outcome;
        }

        private static void ApplyOutcomeAndTime(RunState run, NightState night, V2ActionOutcome outcome,
            GameBalanceConfig config, IRandomSource rng)
        {
            if (!outcome.Accepted) return;
            night.Parent.Stamina = CoreMath.Clamp(night.Parent.Stamina + outcome.StaminaDelta, 0, 100);
            foreach (var id in outcome.EventIds) night.AddEvent(id);
            if (outcome.ConsumedTime)
                V2TimeResolver.Advance(run, night, outcome.TimeDeltaMinutes, config, rng);
        }
    }
}
