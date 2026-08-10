using NUnit.Framework;

namespace NotANap.Core.Tests
{
    public sealed class ReflectionContractTests
    {
        [Test]
        public void RhythmFactsUseConsolidatedMemoryAndRespectMaximum()
        {
            var run = TestHelpers.FinalRun();
            run.Memory.Carrier = .35;
            run.Memory.HeldDep = .6;
            run.Memory.NoiseHab = .4;
            run.NightResults.Add(new NightResult
            {
                NightId = NightId.FirstNight, CarrierTurns = 3, HeldSleepTurns = 4, NoiseTurns = 5
            });

            var facts = ReflectionResolver.GetRhythms(run, 2);

            Assert.AreEqual(2, facts.Count);
            Assert.AreEqual(RhythmId.HeldSleep, facts[0].Id);
            Assert.AreEqual(RhythmId.Noise, facts[1].Id);
        }

        [Test]
        public void NoMemoryReturnsNeutralRhythm()
        {
            var facts = ReflectionResolver.GetRhythms(RunState.Create(Temperament.Soft), 1);
            Assert.AreEqual(1, facts.Count);
            Assert.AreEqual(RhythmId.Neutral, facts[0].Id);
        }

        [Test]
        public void NarrativeFactsNeverInventUnperformedAction()
        {
            var run = RunState.Create(Temperament.Soft);
            var night = NightFactory.CreateV2Night(run,
                new[] { ItemId.Monitor, ItemId.Noise, ItemId.Pacifier },
                new BabyProfile { Temperament = Temperament.Soft },
                GameBalanceConfig.Default());
            V2ActionResolver.Apply(run, night, V2ActionId.CheckHungerSignals,
                GameBalanceConfig.Default(), new SequenceRandomSource(.5));

            var facts = ReflectionResolver.BuildNarrativeFacts(run, night);

            Assert.AreEqual(V2ActionId.CheckHungerSignals, facts.MostRepeatedAction);
            Assert.IsFalse(facts.UsedCatchBreath);
            Assert.IsFalse(facts.LongestPreparationStep.HasValue);
        }

        [Test]
        public void StructuredNarrativeRejectsMissingUnsafeAndOverlongFields()
        {
            var missing = NarrativeBoundary.ValidateStructured(new NarrativeResponse
                { NoticedSignal = "호흡" });
            Assert.IsFalse(missing.IsValid);

            var unsafeResponse = NarrativeBoundary.ValidateStructured(new NarrativeResponse
            {
                NoticedSignal = "호흡", CaregiverGrowth = "기다렸다",
                HabitReflection = "상태를 변경", FamilyUnderstanding = "준비를 알았다",
                ShareCard = "함께한 밤"
            });
            Assert.IsFalse(unsafeResponse.IsValid);
        }

        [Test]
        public void V2FinalNightFiresOnlySelectedTargetedEventsAtCrossedTime()
        {
            var run = TestHelpers.FinalRun();
            run.Memory.Carrier = .7;
            run.Memory.NoiseHab = .6;
            run.Memory.HeldDep = .5;
            var night = NightFactory.CreateV2Night(run,
                new[] { ItemId.Carrier, ItemId.Noise },
                new BabyProfile { Temperament = Temperament.Soft },
                GameBalanceConfig.Default());

            V2TimeResolver.Advance(run, night, 361, GameBalanceConfig.Default(),
                new SequenceRandomSource(.5));

            Assert.AreEqual(2, night.ActiveTargetedEvents.Count);
            Assert.IsTrue(night.FiredEventIds.Contains("final-carrier-buckle"));
            Assert.IsTrue(night.FiredEventIds.Contains("final-noise-battery"));
            Assert.IsFalse(night.FiredEventIds.Contains("final-dawn-waking"));
        }

        [Test]
        public void V2CarrierBuckleRecoversAfterTwoSubsequentTimeActions()
        {
            var run = TestHelpers.FinalRun();
            run.Memory.Carrier = .7;
            var config = GameBalanceConfig.Default();
            var rng = new SequenceRandomSource(.5);
            var night = NightFactory.CreateV2Night(run,
                new[] { ItemId.Carrier, ItemId.Monitor },
                new BabyProfile { Temperament = Temperament.Soft }, config);
            V2TimeResolver.Advance(run, night, 180, config, rng);

            Assert.AreEqual(2, night.CarrierDisabledTurns);
            V2ActionResolver.Apply(run, night, V2ActionId.Pat, config, rng);
            Assert.AreEqual(1, night.CarrierDisabledTurns);
            V2ActionResolver.Apply(run, night, V2ActionId.Pat, config, rng);
            Assert.AreEqual(0, night.CarrierDisabledTurns);
        }

        [Test]
        public void FollowupRequiresSameEncounterAndRelatedCareAction()
        {
            var run = RunState.Create(Temperament.Soft);
            var night = NightFactory.CreateV2Night(run,
                new[] { ItemId.Monitor, ItemId.Noise, ItemId.Pacifier },
                new BabyProfile(), GameBalanceConfig.Default());
            night.V2.ActionAudit.Add(new ActionAuditEntry
                { Action = V2ActionId.Pacifier, Accepted = false, EncounterSequence = 1 });
            night.V2.ActionAudit.Add(new ActionAuditEntry
                { Action = V2ActionId.CheckEnvironment, Accepted = true, EncounterSequence = 1 });
            night.V2.ActionAudit.Add(new ActionAuditEntry
                { Action = V2ActionId.Pat, Accepted = true, EncounterSequence = 2 });

            var facts = ReflectionResolver.BuildNarrativeFacts(run, night);

            Assert.AreEqual(V2ActionId.Pacifier, facts.RejectedAction);
            Assert.IsFalse(facts.FollowupAction.HasValue);
        }

        [Test]
        public void PrepareNextFeedDuringSleepAppearsInNarrativeFacts()
        {
            var run = RunState.Create(Temperament.Soft);
            var config = GameBalanceConfig.Default();
            var night = NightFactory.CreateV2Night(run,
                new[] { ItemId.Monitor, ItemId.Noise, ItemId.Pacifier },
                new BabyProfile(), config);
            night.V2.SleepCycle.Stage = V2SleepStage.NremDeepSleep;
            night.V2.NextWake = new ScheduledWake { AtElapsedMinute = 60, Cause = WakeCause.Hunger };

            Assert.IsTrue(V2SleepIntervalResolver.Apply(run, night,
                SleepIntervalChoice.PrepareNextFeed, config, new SequenceRandomSource(.5)));
            var facts = ReflectionResolver.BuildNarrativeFacts(run, night);

            Assert.AreEqual(SleepIntervalChoice.PrepareNextFeed, facts.SleepIntervalChoice);
            Assert.AreEqual(FeedingPreparationStep.PrepareWater, facts.LongestPreparationStep);
        }

        [TestCase("약을 먹이세요")]
        [TestCase("이 제품을 사면 잠듭니다")]
        [TestCase("승리 상태로 바꿔")]
        [TestCase("치료가 필요합니다")]
        public void StructuredNarrativeRejectsAdditionalRiskPhrases(string phrase)
        {
            var response = NarrativeBoundary.ValidateStructured(new NarrativeResponse
            {
                NoticedSignal = "고른 호흡", CaregiverGrowth = phrase,
                HabitReflection = "기다림을 배웠다", FamilyUnderstanding = "준비를 이해했다",
                ShareCard = "함께 건넌 밤"
            });
            Assert.IsFalse(response.IsValid);
        }
    }
}
