using NUnit.Framework;
using NotANap.Core;

namespace NotANap.Core.Tests
{
    /// <summary>테스트 3·5·6: 기억 형성 규칙.</summary>
    public class MemoryConsolidationTests
    {
        [Test]
        public void CarrierUsedThreeTurns_FormsCarrierHabit()
        {
            var run = RunState.Create(Temperament.Soft);
            var night = NightFactory.CreateNight(run,
                new[] { ItemId.Carrier, ItemId.Noise, ItemId.Monitor });
            var rng = new SystemRandomSource(7);

            var wear = ActionResolver.Apply(run, night, GameAction.ToggleCarrier, rng);
            Assert.IsTrue(wear.Accepted);
            for (int i = 0; i < 3; i++) TestHelpers.Step(run, night, GameAction.Pat, rng);
            Assert.AreEqual(3, night.Stats.CarrierTurns);
            ActionResolver.Apply(run, night, GameAction.ToggleCarrier, rng); // 벗기
            TestHelpers.PlayUntilMorning(run, night, rng);

            Assert.AreEqual(3, night.Stats.CarrierTurns, "벗은 뒤에는 더 누적되지 않아야 한다.");
            MemoryConsolidator.Consolidate(run, night);
            Assert.GreaterOrEqual(run.Memory.Carrier, 0.3,
                "아기띠 3턴 이상 사용 시 carrier 습관이 형성되어야 한다.");
        }

        [Test]
        public void GrandmaUse_IncreasesHeldDep()
        {
            var run = RunState.Create(Temperament.Soft);
            var night = NightFactory.CreateNight(run,
                new[] { ItemId.Noise, ItemId.Pacifier, ItemId.Monitor });
            var rng = new SystemRandomSource(3);

            var outcome = TestHelpers.Step(run, night, GameAction.Grandma, rng);
            Assert.IsTrue(outcome.Accepted);
            Assert.IsTrue(run.GrandmaUsed);
            Assert.IsTrue(night.Stats.Grandma);
            TestHelpers.PlayUntilMorning(run, night, rng);

            Assert.AreEqual(0, run.Memory.HeldDep, 1e-12, "밤 중에는 기억이 변하지 않는다.");
            MemoryConsolidator.Consolidate(run, night);
            Assert.GreaterOrEqual(run.Memory.HeldDep, 0.3, "할머니 찬스 사용 시 heldDep이 올라야 한다.");
        }

        [Test]
        public void TwoWatchSuccesses_IncreaseSelfSoothe()
        {
            var run = RunState.Create(Temperament.Soft);
            var night = NightFactory.CreateNight(run,
                new[] { ItemId.Noise, ItemId.Pacifier, ItemId.Monitor });
            // 항상 0.0 → 지켜보기 성공 확률 판정 통과 (soft: 0.45+0.15=0.6 > 0)
            var rng = new SequenceRandomSource(0.0);

            TestHelpers.Step(run, night, GameAction.Watch, rng);
            TestHelpers.Step(run, night, GameAction.Watch, rng);
            Assert.AreEqual(2, night.Stats.WatchOk, "지켜보기 2회 성공이 기록되어야 한다.");
            TestHelpers.PlayUntilMorning(run, night, rng);

            MemoryConsolidator.Consolidate(run, night);
            Assert.Greater(run.Memory.SelfSoothe, 0, "지켜보기 성공 2회면 selfSoothe가 올라야 한다.");
            Assert.AreEqual(0.22, run.Memory.SelfSoothe, 1e-12);
        }
    }
}
