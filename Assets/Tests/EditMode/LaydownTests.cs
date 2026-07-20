using NUnit.Framework;
using NotANap.Core;

namespace NotANap.Core.Tests
{
    /// <summary>테스트 4·18: 눕히기 확률과 맨손 눕히기 기록.</summary>
    public class LaydownTests
    {
        [Test]
        public void MemoryPenalty_LowersLaydownProbability_ByExactAmount()
        {
            var run = RunState.Create(Temperament.Soft);
            var night = NightFactory.CreateNight(run,
                new[] { ItemId.Noise, ItemId.Pacifier, ItemId.Monitor });
            night.Baby.Sleep = 60;

            double before = ActionResolver.CalculateLaydownSuccessProbability(run, night);
            Assert.AreEqual(0.6 - 0.10, before, 1e-12); // 선잠 0.6 - soft cribSens 0.10

            run.Memory.HeldDep = 0.3;
            double afterHeldDep = ActionResolver.CalculateLaydownSuccessProbability(run, night);
            Assert.AreEqual(0.3 * 0.45, before - afterHeldDep, 1e-12,
                "heldDep 0.3이면 성공 확률이 정확히 0.135 낮아져야 한다.");

            run.Memory.Carrier = 0.2;
            double afterCarrier = ActionResolver.CalculateLaydownSuccessProbability(run, night);
            Assert.AreEqual(0.2 * 0.20, afterHeldDep - afterCarrier, 1e-12,
                "carrier 0.2이면 성공 확률이 추가로 0.04 낮아져야 한다.");
        }

        [Test]
        public void FinalNight_Multiplier_AmplifiesLaydownPenalty()
        {
            var normalRun = RunState.Create(Temperament.Soft);
            normalRun.Memory.HeldDep = 0.3;
            var normalNight = NightFactory.CreateNight(normalRun,
                new[] { ItemId.Noise, ItemId.Pacifier, ItemId.Monitor });
            normalNight.Baby.Sleep = 60;
            double normal = ActionResolver.CalculateLaydownSuccessProbability(normalRun, normalNight);

            var finalRun = TestHelpers.FinalRun(Temperament.Soft);
            finalRun.Memory.HeldDep = 0.3;
            var finalNight = NightFactory.CreateNight(finalRun, new[] { ItemId.Pacifier, ItemId.Monitor });
            finalNight.Baby.Sleep = 60;
            double final = ActionResolver.CalculateLaydownSuccessProbability(finalRun, finalNight);

            Assert.AreEqual(0.6 - 0.10 - 0.3 * 0.45, normal, 1e-12);
            Assert.AreEqual(0.6 - 0.10 - 0.3 * 1.5 * 0.45, final, 1e-12,
                "백일째 밤에는 heldDep 페널티가 1.5배로 증폭되어야 한다.");
            Assert.Less(final, normal);
        }

        [Test]
        public void DawnWakingPenalty_LowersLaydownProbability()
        {
            var run = TestHelpers.FinalRun(Temperament.Soft);
            var night = NightFactory.CreateNight(run, new[] { ItemId.Pacifier, ItemId.Monitor });
            night.Baby.Sleep = 60;
            double before = ActionResolver.CalculateLaydownSuccessProbability(run, night);
            night.LaydownExtraPenalty = GameConfig.DawnWakingLaydownPenalty;
            double after = ActionResolver.CalculateLaydownSuccessProbability(run, night);
            Assert.AreEqual(0.1, before - after, 1e-12);
        }

        [Test]
        public void BareHandsLaydown_Recorded_WhenNoAssistiveEquipmentIsActive()
        {
            var run = RunState.Create(Temperament.Soft);
            var night = NightFactory.CreateNight(run,
                new[] { ItemId.Pacifier, ItemId.Noise, ItemId.Monitor });
            night.Baby.Sleep = 90;
            var rng = new SequenceRandomSource(0.0); // 성공 확정

            ActionResolver.Apply(run, night, GameAction.Hold, rng);
            var outcome = ActionResolver.Apply(run, night, GameAction.Laydown, rng);

            Assert.IsTrue(outcome.LaydownAttempted);
            Assert.IsTrue(outcome.LaydownSucceeded);
            Assert.IsTrue(outcome.BareHandsLaydown);
            Assert.IsTrue(night.Stats.BareHandsLaydownSucceeded);
        }

        [Test]
        public void BareHandsLaydown_NotRecorded_WhenWearingCarrier()
        {
            var run = RunState.Create(Temperament.Soft);
            var night = NightFactory.CreateNight(run,
                new[] { ItemId.Carrier, ItemId.Noise, ItemId.Monitor });
            night.Baby.Sleep = 90;
            var rng = new SequenceRandomSource(0.0);

            ActionResolver.Apply(run, night, GameAction.ToggleCarrier, rng);
            var outcome = ActionResolver.Apply(run, night, GameAction.Laydown, rng);

            Assert.IsTrue(outcome.LaydownSucceeded, "아기띠에서 눕히기 자체는 성공");
            Assert.IsFalse(outcome.BareHandsLaydown, "아기띠 착용 상태는 맨손 눕히기가 아니다.");
            Assert.IsFalse(night.Stats.BareHandsLaydownSucceeded);
        }

    }
}
