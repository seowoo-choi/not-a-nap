using System;
using NUnit.Framework;
using NotANap.Core;

namespace NotANap.Core.Tests
{
    public class FinalNightTests
    {
        [Test]
        public void FinalNight_RejectsThreeItems()
        {
            var run = TestHelpers.FinalRun();
            Assert.Throws<ArgumentException>(() => NightFactory.CreateNight(run,
                new[] { ItemId.Carrier, ItemId.Noise, ItemId.Monitor }));
        }

        [Test]
        public void FinalNight_GrandmaIsRejectedWithoutConsumingTurn()
        {
            var run = TestHelpers.FinalRun();
            var night = NightFactory.CreateNight(run, new[] { ItemId.Noise, ItemId.Monitor });
            var result = ActionResolver.Apply(run, night, GameAction.Grandma,
                new SequenceRandomSource(0.0));

            Assert.IsFalse(result.Accepted);
            Assert.IsFalse(result.ConsumedTurn);
            Assert.IsFalse(run.GrandmaUsed);
            Assert.AreEqual(21, night.Hour);
        }

        [Test]
        public void FinalNight_MemoryStorageIsUnchangedAndEffectsAreScaledAndClamped()
        {
            var run = TestHelpers.FinalRun();
            run.Memory.Carrier = 0.4;
            run.Memory.NoiseHab = 0.8;

            var effective = run.GetEffectiveMemory();

            Assert.AreEqual(0.4, run.Memory.Carrier, 1e-12);
            Assert.AreEqual(0.8, run.Memory.NoiseHab, 1e-12);
            Assert.AreEqual(0.6, effective.Carrier, 1e-12);
            Assert.AreEqual(1.0, effective.NoiseHab, 1e-12);
            Assert.AreNotSame(run.Memory, effective);
        }

        [Test]
        public void CarrierHabit_FiresBuckleFailureAtMidnightOnlyOnce()
        {
            var run = TestHelpers.FinalRun();
            run.Memory.Carrier = 0.3;
            var night = NightFactory.CreateNight(run, new[] { ItemId.Carrier, ItemId.Monitor });
            night.Hour = 0;

            FinalNightResolver.RunScheduledEvents(run, night, new SequenceRandomSource(0.0));
            FinalNightResolver.RunScheduledEvents(run, night, new SequenceRandomSource(0.0));

            Assert.AreEqual(GameConfig.CarrierBuckleDisabledTurns, night.CarrierDisabledTurns);
            Assert.AreEqual(1, night.FiredEventIds.Count);
        }

        [Test]
        public void NoiseHabit_FiresBatteryFailureAtOne()
        {
            var run = TestHelpers.FinalRun();
            run.Memory.NoiseHab = 0.3;
            var night = NightFactory.CreateNight(run, new[] { ItemId.Noise, ItemId.Monitor });
            night.Wearing.Noise = true;
            night.Hour = 1;

            FinalNightResolver.RunScheduledEvents(run, night, new SequenceRandomSource(0.0));

            Assert.IsTrue(night.NoiseDisabled);
            Assert.IsFalse(night.Wearing.Noise);
        }

        [Test]
        public void HeldDependency_FiresDawnWakingAtThree()
        {
            var run = TestHelpers.FinalRun();
            run.Memory.HeldDep = 0.3;
            var night = NightFactory.CreateNight(run, new[] { ItemId.Noise, ItemId.Monitor });
            night.Baby.Sleep = 70;
            night.Hour = 3;

            FinalNightResolver.RunScheduledEvents(run, night, new SequenceRandomSource(0.0));

            Assert.AreEqual(40, night.Baby.Sleep, 1e-12);
            Assert.AreEqual(0.1, night.LaydownExtraPenalty, 1e-12);
            Assert.IsFalse(night.Baby.Crying);
        }

        [Test]
        public void TargetedInterference_SelectsAtMostTwoWithDocumentedTieBreak()
        {
            var run = TestHelpers.FinalRun();
            run.Memory.Carrier = run.Memory.NoiseHab = run.Memory.HeldDep = 0.4;
            run.Memory.SelfSoothe = 0.3;

            var selected = FinalNightResolver.SelectTargetedEvents(run);

            CollectionAssert.AreEqual(new[]
            {
                TargetedEventId.CarrierBuckle,
                TargetedEventId.NoiseBattery
            }, selected);
            Assert.IsTrue(FinalNightResolver.IsSelfSootheActive(run),
                "selfSoothe 보상 패시브는 방해 이벤트 2개 제한과 무관하게 활성화되어야 한다.");
        }

        [TestCase(0.0, true)]
        [TestCase(0.5, false)]
        public void SelfSootheResettle_IsDeterministic(double randomValue, bool succeeds)
        {
            var run = TestHelpers.FinalRun();
            run.Memory.SelfSoothe = 0.3;
            var night = NightFactory.CreateNight(run, new[] { ItemId.Noise, ItemId.Monitor });
            night.Baby.Sleep = 70;
            night.Baby.Calm = 60;

            TurnResolver.WakeBaby(run, night, "테스트", new SequenceRandomSource(randomValue));

            Assert.AreEqual(!succeeds, night.Baby.Crying);
            Assert.AreEqual(succeeds ? 50 : 12, night.Baby.Sleep, 1e-12);
            Assert.AreEqual(succeeds ? 48 : 38, night.Baby.Calm, 1e-12);
        }
    }
}
