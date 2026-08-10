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
            if (night.Over) return RejectAndAudit(night, outcome);
            // 체력이 바닥난 상태에서는 시간을 흘려 밤을 넘길 수 없다.
            // 숨 고르기만이 다시 돌봄 행동으로 돌아가는 결정론적 회복 경로다.
            if (night.Parent.Stamina <= 0 && action != V2ActionId.CatchBreath)
                return RejectAndAudit(night, outcome, V2ActionBlockReason.CaregiverExhausted);
            var locationBlock = LocationBlockReason(night, action);
            if (locationBlock != V2ActionBlockReason.None) return RejectAndAudit(night, outcome, locationBlock);

            switch (action)
            {
                case V2ActionId.CheckDiaper:
                    // 기저귀 우선 확인은 젖지 않았어도 안전한 배제 검사이므로 오판이 아니다.
                    Consume(outcome, config.V2.DiaperCheckMinutes, -config.V2.DiaperCheckStaminaCost);
                    RegisterCheck(run, night, WakeCause.Diaper, outcome, config, false);
                    if (night.V2.Diagnosis.ActiveCause == WakeCause.Diaper)
                    {
                        bool stool = rng.NextDouble() >= 1d - config.V2.DiaperStoolChance;
                        outcome.DiaperCheckResult = stool
                            ? DiaperCheckResult.Stool : DiaperCheckResult.Wet;
                        night.V2.Diagnosis.DiaperWetConfirmed = true;
                        night.V2.Diagnosis.DiaperStoolConfirmed = stool;
                        if (stool)
                            night.V2.CryIntensity = CoreMath.Clamp(night.V2.CryIntensity +
                                config.V2.DiaperStoolCryIncrease *
                                night.V2.Modifier.CryEscalationMultiplier, 0, 100);
                    }
                    else
                    {
                        outcome.DiaperCheckResult = DiaperCheckResult.Clean;
                        night.V2.Diagnosis.DiaperRecommendationSuppressedUntilMinute =
                            night.V2.ElapsedMinutes + config.V2.DiaperCleanRecommendationCooldownMinutes;
                    }
                    break;
                case V2ActionId.ChangeDiaper:
                    if (!night.V2.Diagnosis.DiaperWetConfirmed ||
                        night.V2.Diagnosis.DiaperChangedPendingDisposal)
                        return RejectAndAudit(night, outcome, V2ActionBlockReason.ToolRequired);
                    Consume(outcome,
                        night.V2.Diagnosis.DiaperStoolConfirmed
                            ? config.V2.DiaperStoolChangeMinutes : config.V2.DiaperChangeMinutes,
                        -(night.V2.Diagnosis.DiaperStoolConfirmed
                            ? config.V2.DiaperStoolChangeStaminaCost : config.V2.DiaperChangeStaminaCost));
                    night.V2.Diagnosis.DiaperChangedPendingDisposal = true;
                    break;
                case V2ActionId.DisposeDiaper:
                    if (!night.V2.Diagnosis.DiaperChangedPendingDisposal)
                        return RejectAndAudit(night, outcome, V2ActionBlockReason.ToolRequired);
                    bool stoolDiaper = night.V2.Diagnosis.DiaperStoolConfirmed;
                    outcome.DiaperCheckResult = stoolDiaper
                        ? DiaperCheckResult.Stool : DiaperCheckResult.Wet;
                    Consume(outcome,
                        stoolDiaper ? config.V2.DiaperStoolDisposeMinutes : config.V2.DiaperDisposeMinutes,
                        -(stoolDiaper ? config.V2.DiaperStoolDisposeStaminaCost : config.V2.DiaperDisposeStaminaCost));
                    night.V2.Diagnosis.DiaperChangedPendingDisposal = false;
                    night.V2.HandsNeedWashing = stoolDiaper;
                    ResolveCause(night, outcome);
                    night.V2.Diagnosis.DiaperWetConfirmed = false;
                    night.V2.Diagnosis.DiaperStoolConfirmed = false;
                    break;
                case V2ActionId.WashHands:
                    if (!night.V2.HandsNeedWashing)
                        return RejectAndAudit(night, outcome, V2ActionBlockReason.ToolRequired);
                    Consume(outcome, config.V2.WashHandsMinutes, -config.V2.WashHandsStaminaCost);
                    night.V2.HandsNeedWashing = false;
                    break;
                case V2ActionId.CheckHungerSignals:
                    Diagnose(run, night, WakeCause.Hunger, outcome, config, false);
                    outcome.HungerSignalStage = ObservationResolver.GetHungerStage(night.Baby.Hunger, config.V2);
                    outcome.HungerSignalsMatchCause =
                        night.V2.Diagnosis.ActiveCause == WakeCause.Hunger;
                    // 배가 조금 고파지는 생리 상태와 '이번에 깬 이유'를 같은 정답처럼 말하지 않는다.
                    // 다른 불편으로 깬 밤에는 강한 루팅/배고픔 울음까지 확정적으로 노출하지 않는다.
                    if (!outcome.HungerSignalsMatchCause &&
                        outcome.HungerSignalStage > HungerSignalStage.Early)
                        outcome.HungerSignalStage = HungerSignalStage.Early;
                    ObservationResolver.AddHungerSignals(outcome.HungerSignalStage, outcome.ObservedSignals);
                    RememberVisibleSignals(night, outcome);
                    break;
                case V2ActionId.CheckEnvironment:
                    Consume(outcome, config.V2.DiagnosisActionMinutes, -2);
                    night.V2.Environment.IsTemperatureChecked = true;
                    night.V2.Environment.IsHumidityChecked = true;
                    RegisterCheck(run, night, night.V2.Diagnosis.ActiveCause == WakeCause.Humidity
                        ? WakeCause.Humidity : WakeCause.Temperature, outcome, config, false);
                    break;
                case V2ActionId.CheckBodyTemperature:
                    Consume(outcome, config.V2.DiagnosisActionMinutes, -2);
                    night.V2.Environment.IsBabyTemperatureChecked = true;
                    if (night.V2.Diagnosis.ActiveCause == WakeCause.PainOrCondition)
                        RegisterCheck(run, night, WakeCause.PainOrCondition, outcome, config, false);
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
                    Consume(outcome, config.V2.DiagnosisActionMinutes, -2);
                    outcome.HungerSignalStage = HungerSignalStage.None;
                    ObservationResolver.AddSleepSignals(night.V2.SleepCycle, outcome.ObservedSignals);
                    RememberVisibleSignals(night, outcome);
                    if (night.V2.SleepCycle.Stage == V2SleepStage.NremDeepSleep &&
                        night.V2.SleepCycle.IsLimbRelaxed)
                        night.V2.SleepCycle.DeepSleepObserved = true;
                    break;
                case V2ActionId.Laydown:
                    if (!night.Baby.Held) return RejectAndAudit(night, outcome, V2ActionBlockReason.BabyNotHeld);
                    if (night.V2.SleepCycle.Stage != V2SleepStage.RemActiveSleep &&
                        night.V2.SleepCycle.Stage != V2SleepStage.NremDeepSleep)
                        return RejectAndAudit(night, outcome, V2ActionBlockReason.BabyNotAsleep);
                    ApplyLaydown(run, night, outcome, config, rng);
                    break;
                case V2ActionId.Pacifier:
                    if (!night.HasItem(ItemId.Pacifier) || night.PacifierLeft <= 0)
                        return RejectAndAudit(night, outcome, V2ActionBlockReason.ItemUnavailable);
                    ApplyPacifier(run, night, outcome, config);
                    break;
                case V2ActionId.ToggleNoise:
                    if (!night.HasItem(ItemId.Noise) || night.NoiseDisabled)
                        return RejectAndAudit(night, outcome, V2ActionBlockReason.ItemUnavailable);
                    night.Wearing.Noise = !night.Wearing.Noise;
                    break;
                case V2ActionId.ToggleCarrier:
                    if (!night.HasItem(ItemId.Carrier) ||
                        (night.CarrierDisabledTurns > 0 && !night.Wearing.Carrier))
                        return RejectAndAudit(night, outcome, V2ActionBlockReason.ItemUnavailable);
                    night.Wearing.Carrier = !night.Wearing.Carrier;
                    // 벗긴 직후에는 아기가 맨손 품에 남아 있어 Held와 Carrier가 독립된다.
                    night.Baby.Held = true;
                    night.V2.HeadSupported = true;
                    outcome.HeadSupported = true;
                    break;
                case V2ActionId.CheckMonitor:
                    if (!night.HasItem(ItemId.Monitor))
                        return RejectAndAudit(night, outcome, V2ActionBlockReason.ItemUnavailable);
                    // 화면을 들여다보는 짧은 시간. 공짜면 무한 반복으로 관찰이 무의미해진다.
                    Consume(outcome, 2, -1);
                    outcome.MonitorRead = true;
                    break;
                case V2ActionId.CatchBreath:
                    if (night.V2.CatchBreathUses >= 3 && night.Parent.Stamina > 0)
                        return RejectAndAudit(night, outcome, V2ActionBlockReason.ActionLimitReached);
                    if (night.V2.CatchBreathUses >= 3)
                    {
                        // 탈진 교착만 막는 비상 호흡. 시간을 보내거나 밤의 주력 회복기로 쓸 수 없다.
                        outcome.StaminaDelta = 5;
                        ChangeComposure(night, outcome, 3);
                        AddAmbientSignals(night, outcome, config);
                        RememberVisibleSignals(night, outcome);
                        break;
                    }
                    Consume(outcome, config.V2.DefaultActionMinutes, 9);
                    night.V2.CryIntensity = CoreMath.Clamp(night.V2.CryIntensity + 3, 0, 100);
                    ChangeComposure(night, outcome, 15);
                    night.V2.GentleObservationCount++;
                    night.V2.CatchBreathUses++;
                    AddAmbientSignals(night, outcome, config);
                    RememberVisibleSignals(night, outcome);
                    break;
                case V2ActionId.Grandma:
                    if (run.IsFinalNight || run.GrandmaUsed)
                        return RejectAndAudit(night, outcome, V2ActionBlockReason.ActionLimitReached);
                    Consume(outcome, config.V2.DefaultActionMinutes, 35);
                    run.GrandmaUsed = true;
                    night.Stats.Grandma = true;
                    night.Baby.Calm = 95;
                    night.Baby.Sleep = System.Math.Max(night.Baby.Sleep, 60);
                    night.Baby.Crying = false;
                    night.Baby.Held = true;
                    night.V2.HeadSupported = true;
                    outcome.HeadSupported = true;
                    ResolveCause(night, outcome);
                    V2TimeResolver.BeginSleep(night, V2SleepStage.RemActiveSleep);
                    if (night.V2.NextWake == null || night.V2.NextWake.Triggered)
                        WakeScheduler.Schedule(night, config, rng);
                    break;
                case V2ActionId.Hold:
                    if (night.Wearing.Carrier)
                        return RejectAndAudit(night, outcome, V2ActionBlockReason.CarrierAlreadyWorn);
                    Consume(outcome, config.V2.DefaultActionMinutes, -8);
                    night.Baby.Held = true;
                    night.V2.HeadSupported = true;
                    outcome.HeadSupported = true;
                    night.Baby.Calm = CoreMath.Clamp(night.Baby.Calm +
                        12 * night.V2.Modifier.ComfortActionModifier, 0, 100);
                    night.Baby.Sleep = CoreMath.Clamp(night.Baby.Sleep +
                        config.V2.HoldSleepGain *
                        night.V2.Modifier.SleepGainMultiplier, 0, 100);
                    ApplyComfort(run, night, outcome, config, rng);
                    break;
                case V2ActionId.Pat:
                    Consume(outcome, config.V2.DefaultActionMinutes, -4);
                    night.Baby.Calm = CoreMath.Clamp(night.Baby.Calm +
                        PatCalmGain(night, config), 0, 100);
                    night.Baby.Sleep = CoreMath.Clamp(night.Baby.Sleep +
                        config.V2.PatSleepGain *
                        night.V2.Modifier.SleepGainMultiplier, 0, 100);
                    ApplyComfort(run, night, outcome, config, rng);
                    break;
                case V2ActionId.SterilizeBottle:
                    outcome.ActivityLocation = "주방";
                    Prepare(night, outcome, config, FeedingPreparationStep.SanitizeBottle);
                    night.V2.Feeding.BottleSanitized = true;
                    break;
                case V2ActionId.PrepareWater:
                    outcome.ActivityLocation = "주방";
                    // 실제 플레이의 첫 단계는 물·계량·혼합을 묶은 '분유 준비'다.
                    Prepare(night, outcome, config, FeedingPreparationStep.PrepareWater);
                    night.V2.Feeding.WaterReady = true;
                    night.V2.Feeding.FormulaMeasured = true;
                    night.V2.Feeding.BottleMixed = true;
                    break;
                case V2ActionId.MeasureFormula:
                    outcome.ActivityLocation = "주방";
                    Prepare(night, outcome, config, FeedingPreparationStep.MeasureFormula);
                    night.V2.Feeding.FormulaMeasured = true;
                    break;
                case V2ActionId.MixFormula:
                    outcome.ActivityLocation = "주방";
                    if (!night.V2.Feeding.WaterReady || !night.V2.Feeding.FormulaMeasured) return RejectAndAudit(night, outcome);
                    Prepare(night, outcome, config, FeedingPreparationStep.MixFormula);
                    night.V2.Feeding.BottleMixed = true;
                    break;
                case V2ActionId.CoolBottle:
                    outcome.ActivityLocation = "주방";
                    if (!night.V2.Feeding.BottleMixed) return RejectAndAudit(night, outcome);
                    // 두 번째 단계에서 식힘과 온도 확인을 함께 끝낸다.
                    Prepare(night, outcome, config, FeedingPreparationStep.CoolBottle);
                    night.V2.Feeding.BottleCooled = true;
                    night.V2.Feeding.TemperatureChecked = true;
                    if (night.V2.Feeding.IsReadyToFeed)
                        AddTrace(run, night, outcome, CoreTraceIds.FeedingPreparationCompleted,
                            ActionId.CheckBottleTemperature);
                    break;
                case V2ActionId.CheckBottleTemperature:
                    outcome.ActivityLocation = "주방";
                    if (!night.V2.Feeding.BottleCooled) return RejectAndAudit(night, outcome);
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
                    if (night.V2.HandsNeedWashing)
                        return RejectAndAudit(night, outcome, V2ActionBlockReason.HandsDirty);
                    ApplyPreparedFeed(run, night, outcome, config);
                    break;
            }

            ApplyOutcomeAndTime(run, night, outcome, config, rng);
            RecordAudit(night, outcome);
            return outcome;
        }

        private static V2ActionBlockReason LocationBlockReason(NightState night, V2ActionId action)
        {
            var location = night.V2.CaregiverLocation;
            bool withBaby = night.Baby.Held || location == HomeLocation.Nursery;
            if (action == V2ActionId.SterilizeBottle || action == V2ActionId.PrepareWater ||
                action == V2ActionId.MeasureFormula || action == V2ActionId.MixFormula ||
                action == V2ActionId.CoolBottle || action == V2ActionId.CheckBottleTemperature)
                return location == HomeLocation.Kitchen
                    ? V2ActionBlockReason.None : V2ActionBlockReason.WrongLocation;
            if (action == V2ActionId.WashHands)
                return location == HomeLocation.Bathroom
                    ? V2ActionBlockReason.None : V2ActionBlockReason.WrongLocation;
            if (action == V2ActionId.CheckEnvironment)
                return location == HomeLocation.Nursery || location == HomeLocation.Bathroom
                    ? V2ActionBlockReason.None : V2ActionBlockReason.WrongLocation;
            if (action == V2ActionId.AdjustTemperature ||
                action == V2ActionId.AdjustHumidity || action == V2ActionId.ToggleNoise ||
                action == V2ActionId.Laydown)
                return location == HomeLocation.Nursery
                    ? V2ActionBlockReason.None : V2ActionBlockReason.WrongLocation;
            if (action == V2ActionId.Grandma)
                return location == HomeLocation.Nursery
                    ? V2ActionBlockReason.None : V2ActionBlockReason.WrongLocation;
            if (action == V2ActionId.CheckBodyTemperature && !night.V2.BathThermometerRetrieved)
                return V2ActionBlockReason.ToolRequired;
            if (action != V2ActionId.CheckMonitor && action != V2ActionId.CatchBreath &&
                action != V2ActionId.Hesitate && !withBaby)
                return V2ActionBlockReason.WrongLocation;
            return V2ActionBlockReason.None;
        }

        private static void ChangeComposure(NightState night, V2ActionOutcome outcome, double delta)
        {
            double before = night.V2.CaregiverComposure;
            night.V2.CaregiverComposure = CoreMath.Clamp(before + delta, 0, 100);
            outcome.ComposureDelta = night.V2.CaregiverComposure - before;
        }

        private static void AddAmbientSignals(NightState night, V2ActionOutcome outcome,
            GameBalanceConfig config)
        {
            ObservationResolver.AddHungerSignals(
                ObservationResolver.GetHungerStage(night.Baby.Hunger, config.V2),
                outcome.ObservedSignals);
            ObservationResolver.AddSleepSignals(night.V2.SleepCycle, outcome.ObservedSignals);
            if (night.V2.SleepCycle.Stage == V2SleepStage.Awake)
            {
                if (night.Baby.Calm < config.V2.DrowsyCalmThreshold)
                    outcome.ObservedSignals.Add(ObservationSignalId.Squirming);
                else
                {
                    outcome.ObservedSignals.Add(ObservationSignalId.Yawning);
                    outcome.ObservedSignals.Add(ObservationSignalId.RubbingEyes);
                }
            }
        }

        private static void RememberVisibleSignals(NightState night, V2ActionOutcome outcome)
        {
            night.V2.VisibleSignals.Clear();
            foreach (var signal in outcome.ObservedSignals)
                if (!night.V2.VisibleSignals.Contains(signal))
                    night.V2.VisibleSignals.Add(signal);
        }

        /// <summary>
        /// 토닥임의 진정 폭. 원인을 아직 못 찾았으면 절반만 준다.
        /// 다만 토닥임 자체가 정답인 자연 각성·모로 반사는 ApplyComfort에서
        /// 곧바로 원인이 풀리므로 온전한 폭을 유지한다.
        /// </summary>
        private static double PatCalmGain(NightState night, GameBalanceConfig config)
        {
            double gain = 12 * night.V2.Modifier.ComfortActionModifier;
            var diagnosis = night.V2.Diagnosis;
            if (diagnosis.CauseResolved ||
                diagnosis.ActiveCause == WakeCause.NaturalCycle ||
                diagnosis.ActiveCause == WakeCause.MoroReflex)
                return gain;
            return gain * config.V2.UnresolvedCauseComfortMultiplier;
        }

        private static void ApplyComfort(RunState run, NightState night, V2ActionOutcome outcome,
            GameBalanceConfig config, IRandomSource rng)
        {
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
            outcome.WasMisdiagnosis = true;
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
            // 관찰 당시의 문구가 원인 해소 뒤에도 남아 현재 상태와 충돌하지 않게 한다.
            night.V2.VisibleSignals.Clear();
        }

        private static void ApplyLaydown(RunState run, NightState night, V2ActionOutcome outcome,
            GameBalanceConfig config, IRandomSource rng)
        {
            Consume(outcome, config.V2.DefaultActionMinutes, -4);
            bool bareHands = night.Baby.Held && !night.Wearing.Carrier && !night.Wearing.Bouncer;
            if (bareHands) night.V2.BareHandsLaydownAttempts++;
            night.Wearing.Carrier = false;
            double chance = ActionResolver.CalculateLaydownSuccessProbability(run, night, config);
            bool deepObserved = night.V2.SleepCycle.Stage == V2SleepStage.NremDeepSleep &&
                                night.V2.SleepCycle.IsLimbRelaxed && night.V2.SleepCycle.DeepSleepObserved;
            if (deepObserved) chance = CoreMath.Clamp(chance + config.V2.DeepSleepLaydownBonus, 0, 1);
            if (night.V2.SleepCycle.Stage == V2SleepStage.RemActiveSleep)
                chance *= 1 - config.V2.RemLaydownWakeChance;
            // REM 감쇠를 곱한 뒤에 다시 하한을 잡는다. 곱하기 전에만 클램프하면
            // 확률 하한 0.05가 무력화되어 실질 1%대까지 내려간다.
            chance = CoreMath.Clamp(chance, .05, .95);
            if (rng.NextDouble() < chance)
            {
                night.Baby.Held = false;
                night.V2.HeadSupported = false;
                if (bareHands) night.Stats.BareHandsLaydownSucceeded = true;
                outcome.EventIds.Add(GameEventId.LaydownSucceeded);
                if (deepObserved)
                    AddTrace(run, night, outcome, CoreTraceIds.DeepSleepObservedBeforeLaydown, ActionId.Laydown);
                if (night.V2.NextWake == null || night.V2.NextWake.Triggered)
                    WakeScheduler.Schedule(night, config, rng);
            }
            else
            {
                outcome.EventIds.Add(GameEventId.LaydownFailed);
                night.AddEvent(GameEventId.LaydownFailed);
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
            night.PacifierLeft--;
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
            feeding.WaterReady = false;
            feeding.FormulaMeasured = false;
            feeding.BottleMixed = false;
            feeding.BottleCooled = false;
            feeding.TemperatureChecked = false;
            night.V2.HoldWhilePreparing = false;
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

        private static V2ActionOutcome RejectAndAudit(NightState night, V2ActionOutcome outcome,
            V2ActionBlockReason reason = V2ActionBlockReason.None)
        {
            Reject(outcome, reason);
            RecordAudit(night, outcome);
            return outcome;
        }

        private static void RecordAudit(NightState night, V2ActionOutcome outcome)
        {
            var entry = new ActionAuditEntry
            {
                Action = outcome.Action,
                Accepted = outcome.Accepted,
                TimeDeltaMinutes = outcome.TimeDeltaMinutes,
                EncounterSequence = night.V2.Diagnosis.EncounterSequence,
                ElapsedMinutes = night.V2.ElapsedMinutes
            };
            entry.ObservedSignals.AddRange(outcome.ObservedSignals);
            night.V2.ActionAudit.Add(entry);
        }

        private static void ApplyOutcomeAndTime(RunState run, NightState night, V2ActionOutcome outcome,
            GameBalanceConfig config, IRandomSource rng)
        {
            if (!outcome.Accepted) return;
            int carrierDisabledBefore = night.CarrierDisabledTurns;
            double staminaBefore = night.Parent.Stamina;
            night.Parent.Stamina = CoreMath.Clamp(night.Parent.Stamina + outcome.StaminaDelta, 0, 100);
            if (staminaBefore > 0 && night.Parent.Stamina <= 0 && !night.V2.ExhaustionWarned)
            {
                night.V2.ExhaustionWarned = true;
                outcome.EventIds.Add(GameEventId.ParentExhausted);
            }
            foreach (var id in outcome.EventIds)
                if (!night.Events.Exists(e => e.Id == id && e.Turn == night.ConsumedTurns))
                    night.AddEvent(id);
            if (outcome.ConsumedTime)
            {
                V2TimeResolver.Advance(run, night, outcome.TimeDeltaMinutes, config, rng);
                // V2의 "2턴"은 사건 발동 이후 수락된 시간 소비 행동 두 번이다.
                // 발동시킨 행동 자체는 차감하지 않는다.
                if (carrierDisabledBefore > 0)
                    night.CarrierDisabledTurns = System.Math.Max(0, carrierDisabledBefore - 1);
            }
        }
    }
}
