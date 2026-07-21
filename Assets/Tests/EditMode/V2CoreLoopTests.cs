using System.Linq;
using NUnit.Framework;
using NotANap.Core;

namespace NotANap.Core.Tests
{
    public class V2CoreLoopTests
    {
        private static NightState Night(RunState run = null, GameBalanceConfig config = null,
            BabyProfile profile = null, NightModifierId modifier = NightModifierId.None,
            ProductCapability[] capabilities = null)
        {
            run = run ?? RunState.Create(Temperament.Soft);
            config = config ?? GameBalanceConfig.Default();
            return NightFactory.CreateV2Night(run,
                new[] { ItemId.Carrier, ItemId.Pacifier, ItemId.Monitor },
                profile ?? new BabyProfile { Temperament = run.Temperament }, config, modifier, capabilities);
        }

        [Test]
        public void SleepingBabyCanWakeBeforeDawn()
        {
            var config = GameBalanceConfig.Default();
            config.V2.WakeDelayMinMinutes = config.V2.WakeDelayMaxMinutes = 30;
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run, config);
            V2TimeResolver.BeginSleep(night, V2SleepStage.RemActiveSleep);
            WakeScheduler.Schedule(night, config, new SequenceRandomSource(0));
            V2TimeResolver.Advance(run, night, 31, config, new SequenceRandomSource(0));
            Assert.AreEqual(V2SleepStage.Awake, night.V2.SleepCycle.Stage);
            Assert.IsTrue(night.Baby.Crying);
            Assert.IsFalse(night.Over);
        }

        [Test]
        public void SameSeedSchedulesSameWakeCauseAndMinute()
        {
            var config = GameBalanceConfig.Default();
            var a = WakeScheduler.Schedule(Night(), config, new SystemRandomSource(17));
            var b = WakeScheduler.Schedule(Night(), config, new SystemRandomSource(17));
            Assert.AreEqual(a.Cause, b.Cause);
            Assert.AreEqual(a.AtElapsedMinute, b.AtElapsedMinute);
        }

        [Test]
        public void DifferentSeedsCanScheduleDifferentWake()
        {
            var config = GameBalanceConfig.Default();
            var variants = Enumerable.Range(0, 20).Select(seed =>
            {
                var wake = WakeScheduler.Schedule(Night(), config, new SystemRandomSource(seed));
                return wake.Cause + ":" + wake.AtElapsedMinute;
            }).Distinct();
            Assert.Greater(variants.Count(), 1);
        }

        [Test]
        public void ScheduledWakeAlwaysHasAPlayerResolutionPath()
        {
            var config = GameBalanceConfig.Default();
            var actionable = new[] { WakeCause.Hunger, WakeCause.Diaper, WakeCause.Temperature, WakeCause.Humidity };
            for (int seed = 0; seed < 100; seed++)
            {
                var wake = WakeScheduler.Schedule(Night(), config, new SystemRandomSource(seed));
                CollectionAssert.Contains(actionable, wake.Cause);
            }
        }

        [TestCase(WakeCause.NaturalCycle, V2ActionId.Pat)]
        [TestCase(WakeCause.MoroReflex, V2ActionId.Hold)]
        public void ComfortResolvesLaydownRecoveryWake(WakeCause cause, V2ActionId action)
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run, config);
            night.Baby.Calm = 70;
            V2TimeResolver.TriggerWake(night, cause, config);

            var outcome = V2ActionResolver.Apply(run, night, action, config, new SequenceRandomSource(0));

            Assert.IsTrue(outcome.CauseResolved);
            Assert.IsTrue(night.V2.Diagnosis.CauseResolved);
            Assert.AreEqual(V2SleepStage.RemActiveSleep, night.V2.SleepCycle.Stage);
        }

        [Test]
        public void OneSuccessfulLaydownDoesNotEndNight()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run, config);
            night.Baby.Held = true;
            night.Baby.Sleep = 90;
            V2TimeResolver.BeginSleep(night, V2SleepStage.NremDeepSleep);
            V2ActionResolver.Apply(run, night, V2ActionId.CheckLimbRelaxation, config, new SequenceRandomSource(0));
            var result = V2ActionResolver.Apply(run, night, V2ActionId.Laydown, config, new SequenceRandomSource(0));
            CollectionAssert.Contains(result.EventIds, GameEventId.LaydownSucceeded);
            Assert.IsFalse(night.Over);
            Assert.IsNotNull(night.V2.NextWake);
        }

        [Test]
        public void LongestSleepStretchIsCalculatedExactly()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run, config);
            V2TimeResolver.BeginSleep(night, V2SleepStage.RemActiveSleep);
            V2TimeResolver.Advance(run, night, 95, config, new SequenceRandomSource(0));
            Assert.AreEqual(95, night.V2.Metrics.CurrentSleepStretchMinutes);
            Assert.AreEqual(95, night.V2.Metrics.LongestSleepStretchMinutes);
            Assert.AreEqual(95, night.V2.Metrics.TotalSleepMinutes);
        }

        [Test]
        public void WakeEndsCurrentStretchButPreservesLongest()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run, config);
            V2TimeResolver.BeginSleep(night, V2SleepStage.RemActiveSleep);
            V2TimeResolver.Advance(run, night, 80, config, new SequenceRandomSource(0));
            V2TimeResolver.TriggerWake(night, WakeCause.NaturalCycle, config);
            Assert.AreEqual(0, night.V2.Metrics.CurrentSleepStretchMinutes);
            Assert.AreEqual(80, night.V2.Metrics.LongestSleepStretchMinutes);
            Assert.AreEqual(1, night.V2.Metrics.WakeCount);
        }

        [Test]
        public void DiaperFirstCheckThenChangeResolvesCause()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run, config);
            V2TimeResolver.TriggerWake(night, WakeCause.Diaper, config);
            V2ActionResolver.Apply(run, night, V2ActionId.CheckDiaper, config, new SequenceRandomSource(0));
            var changed = V2ActionResolver.Apply(run, night, V2ActionId.ChangeDiaper, config, new SequenceRandomSource(0));
            Assert.IsTrue(changed.CauseResolved);
            Assert.AreEqual(1, night.V2.Metrics.CorrectFirstChecks);
            Assert.IsTrue(run.Traces.Contains(CoreTraceIds.DiaperCheckedFirst));
        }

        [Test]
        public void NonDiagnosticCareDoesNotResolveDiaperCause()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run, config);
            V2TimeResolver.TriggerWake(night, WakeCause.Diaper, config);
            V2ActionResolver.Apply(run, night, V2ActionId.Hold, config, new SequenceRandomSource(0));
            Assert.IsFalse(night.V2.Diagnosis.CauseResolved);
            Assert.AreEqual(1, night.V2.Metrics.MisdiagnosisCount);
        }

        [Test]
        public void CauseCanBeDiagnosedAgainAfterMismatch()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run, config);
            V2TimeResolver.TriggerWake(night, WakeCause.Diaper, config);
            V2ActionResolver.Apply(run, night, V2ActionId.CheckHungerSignals, config, new SequenceRandomSource(0));
            V2ActionResolver.Apply(run, night, V2ActionId.CheckDiaper, config, new SequenceRandomSource(0));
            Assert.IsTrue(night.V2.Diagnosis.CheckedCauses.Contains(WakeCause.Diaper));
            Assert.IsTrue(run.Traces.Contains(CoreTraceIds.CauseRecheckedAfterMismatch));
        }

        [Test]
        public void DecisionTimeoutUsesHesitationPenalty()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var a = Night(run, config);
            var b = Night(RunState.Create(Temperament.Soft), config);
            V2TimeResolver.TriggerWake(a, WakeCause.Diaper, config);
            V2TimeResolver.TriggerWake(b, WakeCause.Diaper, config);
            var timeout = V2ActionResolver.ApplyDecisionTimeout(run, a, config, new SequenceRandomSource(0));
            var hesitate = V2ActionResolver.Apply(RunState.Create(Temperament.Soft), b,
                V2ActionId.Hesitate, config, new SequenceRandomSource(0));
            Assert.AreEqual(hesitate.TimeDeltaMinutes, timeout.TimeDeltaMinutes);
            Assert.AreEqual(hesitate.StaminaDelta, timeout.StaminaDelta);
            Assert.AreEqual(b.V2.CryIntensity, a.V2.CryIntensity);
        }

        [TestCase(20, HungerSignalStage.None, ObservationSignalId.LipSmacking, false)]
        [TestCase(40, HungerSignalStage.Early, ObservationSignalId.LipSmacking, true)]
        [TestCase(65, HungerSignalStage.Active, ObservationSignalId.Rooting, true)]
        [TestCase(90, HungerSignalStage.Late, ObservationSignalId.HungerCry, true)]
        public void HungerCheckReturnsStageSignals(double hunger, HungerSignalStage stage,
            ObservationSignalId signal, bool contains)
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run, config);
            night.Baby.Hunger = hunger;
            var result = V2ActionResolver.Apply(run, night, V2ActionId.CheckHungerSignals,
                config, new SequenceRandomSource(0));
            Assert.AreEqual(stage, result.HungerSignalStage);
            Assert.AreEqual(contains, result.ObservedSignals.Contains(signal));
        }

        [Test]
        public void LimbCheckObservesWithoutChangingPhysicalState()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run, config);
            V2TimeResolver.BeginSleep(night, V2SleepStage.NremDeepSleep);
            double calm = night.Baby.Calm, sleep = night.Baby.Sleep, stamina = night.Parent.Stamina;
            var result = V2ActionResolver.Apply(run, night, V2ActionId.CheckLimbRelaxation,
                config, new SequenceRandomSource(0));
            Assert.AreEqual(calm, night.Baby.Calm);
            Assert.AreEqual(sleep, night.Baby.Sleep);
            Assert.AreEqual(stamina, night.Parent.Stamina);
            CollectionAssert.Contains(result.ObservedSignals, ObservationSignalId.RelaxedLimbs);
        }

        [Test]
        public void DeepSleepObservationProvidesLaydownBonus()
        {
            var config = GameBalanceConfig.Default();
            var deepRun = RunState.Create(Temperament.Soft);
            var remRun = RunState.Create(Temperament.Soft);
            var deep = Night(deepRun, config); var rem = Night(remRun, config);
            deep.Baby.Held = rem.Baby.Held = true;
            deep.Baby.Sleep = rem.Baby.Sleep = 60;
            V2TimeResolver.BeginSleep(deep, V2SleepStage.NremDeepSleep);
            V2TimeResolver.BeginSleep(rem, V2SleepStage.RemActiveSleep);
            V2ActionResolver.Apply(deepRun, deep, V2ActionId.CheckLimbRelaxation, config, new SequenceRandomSource(0));
            var deepResult = V2ActionResolver.Apply(deepRun, deep, V2ActionId.Laydown, config, new SequenceRandomSource(.55, 0));
            var remResult = V2ActionResolver.Apply(remRun, rem, V2ActionId.Laydown, config, new SequenceRandomSource(.55, 0));
            CollectionAssert.Contains(deepResult.EventIds, GameEventId.LaydownSucceeded);
            CollectionAssert.Contains(remResult.EventIds, GameEventId.LaydownFailed);
        }

        [Test]
        public void FeedIsRejectedBeforeRequiredPreparation()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var result = V2ActionResolver.Apply(run, Night(run, config), V2ActionId.FeedPreparedBottle,
                config, new SequenceRandomSource(0));
            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(6, result.MissingPreparationSteps.Count);
            Assert.IsTrue(run.Traces.Contains(CoreTraceIds.FeedingAttemptedBeforeReady));
        }

        [Test]
        public void PreSanitizedCapabilitySkipsSanitizingStep()
        {
            var config = GameBalanceConfig.Default();
            var night = Night(config: config, capabilities: new[] { ProductCapability.PreSanitizedBottle });
            Assert.IsTrue(night.V2.Feeding.BottleSanitized);
            var result = V2ActionResolver.Apply(RunState.Create(Temperament.Soft), night,
                V2ActionId.FeedPreparedBottle, config, new SequenceRandomSource(0));
            CollectionAssert.DoesNotContain(result.MissingPreparationSteps, FeedingPreparationStep.SanitizeBottle);
        }

        [Test]
        public void HoldingWhilePreparingSlowsCryEscalation()
        {
            var config = GameBalanceConfig.Default();
            var heldRun = RunState.Create(Temperament.Soft); var plainRun = RunState.Create(Temperament.Soft);
            var held = Night(heldRun, config); var plain = Night(plainRun, config);
            held.Baby.Crying = plain.Baby.Crying = true;
            V2ActionResolver.Apply(heldRun, held, V2ActionId.HoldWhilePreparing, config, new SequenceRandomSource(0));
            V2TimeResolver.Advance(plainRun, plain, config.V2.DefaultActionMinutes, config, new SequenceRandomSource(0));
            Assert.Less(held.V2.CryIntensity, plain.V2.CryIntensity);
            Assert.Less(held.Parent.Stamina, plain.Parent.Stamina);
        }

        [Test]
        public void RejectingPacifierProfileNeverForcesAcceptance()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var profile = new BabyProfile { Temperament = Temperament.Soft, PacifierAffinity = PacifierAffinity.Rejects };
            var night = Night(run, config, profile);
            for (int i = 0; i < 3; i++)
                Assert.IsFalse(V2ActionResolver.Apply(run, night, V2ActionId.Pacifier,
                    config, new SequenceRandomSource(0)).Accepted);
            Assert.IsTrue(run.Traces.Contains(CoreTraceIds.PacifierRejected));
            Assert.IsFalse(run.Traces.Contains(CoreTraceIds.PacifierAccepted));
        }

        [Test]
        public void BouncerRemainsLoadableButIsNotV2Selectable()
        {
            Assert.IsTrue(System.Enum.IsDefined(typeof(ItemId), ItemId.Bouncer));
            Assert.IsFalse(V2NightFactory.IsSelectableItem(ItemId.Bouncer));
            CollectionAssert.DoesNotContain(V2NightFactory.SelectableItems, ItemId.Bouncer);
        }

        [Test]
        public void TraceContractStillHasNoOutcomePolarity()
        {
            Assert.IsNull(typeof(TraceRecord).GetField("OutcomeType"));
            Assert.IsNull(typeof(TraceRecord).GetField("Good"));
            Assert.IsNotNull(typeof(EventResolution).GetField("OutcomeType"));
        }

        [Test]
        public void FeedforwardStillDoesNotMutateV2OrTraceState()
        {
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run);
            var seed = new EventSeed { FeedforwardCue = new FeedforwardCueId("sleep.cycle.possible-wake") };
            int traces = run.Traces.Records.Count, elapsed = night.V2.ElapsedMinutes;
            Assert.IsTrue(DelayedEchoEngine.GetFeedforwardCue(seed).HasValue);
            Assert.AreEqual(traces, run.Traces.Records.Count);
            Assert.AreEqual(elapsed, night.V2.ElapsedMinutes);
        }

        [TestCase(300, 0, NightGrade.S)]
        [TestCase(300, 1, NightGrade.A)]
        [TestCase(240, 0, NightGrade.A)]
        [TestCase(180, 0, NightGrade.B)]
        [TestCase(120, 0, NightGrade.C)]
        [TestCase(119, 0, NightGrade.D)]
        public void NightEvaluationUsesConfiguredBoundaries(int longest, int unsafeCount, NightGrade grade)
        {
            var config = GameBalanceConfig.Default();
            var night = Night(config: config);
            night.V2.Metrics.LongestSleepStretchMinutes = longest;
            night.V2.Metrics.UnsafeChoiceCount = unsafeCount;
            Assert.AreEqual(grade, NightEvaluationResolver.Evaluate(night, config).Grade);
        }

        [Test]
        public void VaccinationModifierChangesWakeFrequencyDeterministically()
        {
            var config = GameBalanceConfig.Default();
            var normal = Night(config: config);
            var modified = Night(config: config, modifier: NightModifierId.Vaccination);
            var a = WakeScheduler.Schedule(normal, config, new SequenceRandomSource(.5));
            var b = WakeScheduler.Schedule(modified, config, new SequenceRandomSource(.5));
            Assert.Less(b.AtElapsedMinute, a.AtElapsedMinute);
        }

        [Test]
        public void ExistingFutureEventCanScheduleV2Wake()
        {
            var night = Night();
            var seed = new EventSeed
            {
                EventId = new FutureEventId("sleep.trace-return"),
                NightId = night.NightId,
                TriggerTurn = 4
            };
            var wake = WakeScheduler.ScheduleFromFutureEvent(night, seed, 15);
            Assert.AreEqual(60, wake.AtElapsedMinute);
            Assert.AreEqual(seed.EventId, wake.SourceFutureEventId);
        }

        [Test]
        public void ComfortActionsCanStartSleepAndScheduleAWake()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run, config);
            var rng = new SequenceRandomSource(.5, .25);

            V2ActionResolver.Apply(run, night, V2ActionId.Hold, config, rng);
            Assert.AreEqual(V2SleepStage.Drowsy, night.V2.SleepCycle.Stage);

            V2ActionResolver.Apply(run, night, V2ActionId.Pat, config, rng);
            Assert.AreEqual(V2SleepStage.RemActiveSleep, night.V2.SleepCycle.Stage);
            Assert.IsNotNull(night.V2.NextWake);
            Assert.Greater(night.V2.NextWake.AtElapsedMinute, night.V2.ElapsedMinutes);
        }

        [Test]
        public void EachDiagnosisEncounterGetsAStableIncreasingSequence()
        {
            var state = new DiagnosisState();
            state.Begin(WakeCause.Diaper, 20);
            Assert.AreEqual(1, state.EncounterSequence);
            state.Begin(WakeCause.Hunger, 20);
            Assert.AreEqual(2, state.EncounterSequence);
        }

        [Test]
        public void LaydownBeforeSleepIsRejectedWithoutWakeOrTimePassing()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run, config);
            night.Baby.Held = true;
            int events = night.Events.Count;

            var result = V2ActionResolver.Apply(run, night, V2ActionId.Laydown,
                config, new SequenceRandomSource(0));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(V2ActionBlockReason.BabyNotAsleep, result.BlockReason);
            Assert.AreEqual(0, night.V2.ElapsedMinutes);
            Assert.AreEqual(events, night.Events.Count);
        }

        [Test]
        public void HungerWakeCreatesVisibleLateHungerSignals()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run, config);
            V2TimeResolver.TriggerWake(night, WakeCause.Hunger, config);

            var result = V2ActionResolver.Apply(run, night, V2ActionId.CheckHungerSignals,
                config, new SequenceRandomSource(0));

            Assert.AreEqual(HungerSignalStage.Late, result.HungerSignalStage);
            CollectionAssert.Contains(result.ObservedSignals, ObservationSignalId.HungerCry);
        }

        [Test]
        public void EnvironmentWakeShowsAbnormalValueAndAdjustmentReturnsToRange()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var night = Night(run, config);
            V2TimeResolver.TriggerWake(night, WakeCause.Humidity, config);
            Assert.AreEqual(30, night.V2.Environment.HumidityPercent);

            V2ActionResolver.Apply(run, night, V2ActionId.CheckEnvironment, config, new SequenceRandomSource(0));
            V2ActionResolver.Apply(run, night, V2ActionId.AdjustHumidity, config, new SequenceRandomSource(0));

            Assert.AreEqual(40, night.V2.Environment.HumidityPercent);
            Assert.IsTrue(night.V2.Diagnosis.CauseResolved);
        }

        [Test]
        public void NoiseAndMonitorItemsHavePlayableV2Actions()
        {
            var config = GameBalanceConfig.Default();
            var run = RunState.Create(Temperament.Soft);
            var night = V2NightFactory.Create(run, new[] { ItemId.Noise, ItemId.Monitor, ItemId.Pacifier },
                new BabyProfile(), config);

            Assert.IsTrue(V2ActionResolver.Apply(run, night, V2ActionId.ToggleNoise,
                config, new SequenceRandomSource(0)).Accepted);
            Assert.IsTrue(night.Wearing.Noise);
            Assert.IsTrue(V2ActionResolver.Apply(run, night, V2ActionId.CheckMonitor,
                config, new SequenceRandomSource(0)).MonitorRead);
        }
    }
}
