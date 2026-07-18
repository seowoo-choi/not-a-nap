using NUnit.Framework;
using NotANap.Core;

namespace NotANap.Core.Tests
{
    /// <summary>테스트 1·2: 결정론과 밤 길이.</summary>
    public class DeterminismTests
    {
        private static string PlayScriptedNight(int seed)
        {
            var run = RunState.Create(Temperament.Sensitive);
            var night = NightFactory.CreateNight(run,
                new[] { ItemId.Carrier, ItemId.Pacifier, ItemId.Noise });
            var rng = new SystemRandomSource(seed);
            var script = new[]
            {
                GameAction.ToggleNoise, GameAction.Hold, GameAction.Pat, GameAction.Pacifier,
                GameAction.Laydown, GameAction.Watch, GameAction.Hold, GameAction.Pat,
                GameAction.Pat, GameAction.Pat, GameAction.Pat,
            };
            foreach (var action in script)
            {
                if (night.Over) break;
                var outcome = ActionResolver.Apply(run, night, action, rng);
                if (outcome.ConsumedTurn) TurnResolver.EndTurn(run, night, rng);
            }
            MemoryConsolidator.Consolidate(run, night);
            return TestHelpers.Snapshot(run, night);
        }

        [Test]
        public void SameSeedAndInputs_ProduceIdenticalStateLogsAndResult()
        {
            var first = PlayScriptedNight(1234);
            var second = PlayScriptedNight(1234);
            Assert.AreEqual(first, second, "동일 시드·동일 입력이면 상태·로그·결과가 완전히 같아야 한다.");
        }

        [Test]
        public void ScriptedNight_AlwaysReachesMorning()
        {
            var snapshot = PlayScriptedNight(1234);
            StringAssert.Contains("over=True", snapshot);
            StringAssert.Contains("hour=6", snapshot);
        }

        [Test]
        public void NightEndsAfterExactlyNineConsumedTurns()
        {
            var run = RunState.Create(Temperament.Soft);
            var night = NightFactory.CreateNight(run,
                new[] { ItemId.Carrier, ItemId.Noise, ItemId.Monitor });
            var rng = new SystemRandomSource(42);
            int consumed = 0;
            while (!night.Over)
            {
                var outcome = ActionResolver.Apply(run, night, GameAction.Pat, rng);
                Assert.IsTrue(outcome.ConsumedTurn, "Pat은 항상 턴을 소비해야 한다.");
                TurnResolver.EndTurn(run, night, rng);
                consumed++;
                Assert.Less(consumed, 20, "밤이 끝나지 않는다.");
            }
            Assert.AreEqual(GameConfig.TurnsPerNight, consumed);
            Assert.AreEqual(GameConfig.TurnsPerNight, night.ConsumedTurns);
            Assert.AreEqual(GameConfig.EndHour, night.Hour);
            Assert.IsTrue(night.Result.HasValue, "밤 종료 시 결과가 결정되어야 한다.");
        }
    }
}
