using System.Linq;
using NUnit.Framework;
using NotANap.Core;

namespace NotANap.Core.Tests
{
    public class CoreFoundationTests
    {
        [Test]
        public void NonTurnAction_DoesNotAdvanceTime()
        {
            var run = RunState.Create(Temperament.Soft);
            var night = NightFactory.CreateNight(run, new[] { ItemId.Pacifier, ItemId.Noise, ItemId.Monitor });
            var result = ActionResolver.Apply(run, night, GameAction.ToggleNoise, new SequenceRandomSource(0));
            Assert.IsFalse(result.ConsumedTurn);
            Assert.AreEqual(21, night.Hour);
            Assert.AreEqual(0, night.ConsumedTurns);
        }

        [Test]
        public void StateValuesRemainInValidRange()
        {
            foreach (var temperament in Temperament.All)
            {
                var run = RunState.Create(temperament);
                var night = NightFactory.CreateNight(run, new[] { ItemId.Carrier, ItemId.Noise, ItemId.Monitor });
                TestHelpers.PlayUntilMorning(run, night, new SystemRandomSource(17), GameAction.Pat);
                Assert.That(night.Baby.Calm, Is.InRange(0, 100));
                Assert.That(night.Baby.Sleep, Is.InRange(0, 100));
                Assert.That(night.Baby.Hunger, Is.InRange(0, 100));
                Assert.That(night.Parent.Stamina, Is.InRange(0, 100));
            }
        }

        [Test]
        public void MemoryFormationUsesConfiguredThreshold()
        {
            var run = RunState.Create(Temperament.Soft);
            var night = NightFactory.CreateNight(run, new[] { ItemId.Carrier, ItemId.Noise, ItemId.Monitor });
            night.Stats.CarrierTurns = 3;
            var config = GameBalanceConfig.Default();
            config.CarrierHabitThreshold = 4;
            MemoryConsolidator.Consolidate(run, night, config);
            Assert.AreEqual(0, run.Memory.Carrier);
        }

        [Test]
        public void TemperamentModifiersCanChangeRuleOutcome()
        {
            var soft = RunState.Create(Temperament.Soft);
            var sensitive = RunState.Create(Temperament.Sensitive);
            var a = NightFactory.CreateNight(soft, new[] { ItemId.Pacifier, ItemId.Noise, ItemId.Monitor });
            var b = NightFactory.CreateNight(sensitive, new[] { ItemId.Pacifier, ItemId.Noise, ItemId.Monitor });
            a.Baby.Sleep = b.Baby.Sleep = 60;
            Assert.AreNotEqual(ActionResolver.CalculateLaydownSuccessProbability(soft, a),
                ActionResolver.CalculateLaydownSuccessProbability(sensitive, b));
        }

        [Test]
        public void TemperamentModifierCanBeReconfiguredWithoutBouncerContract()
        {
            var run = RunState.Create(Temperament.Sensitive);
            var night = NightFactory.CreateNight(run, new[] { ItemId.Pacifier, ItemId.Noise, ItemId.Monitor });
            night.Baby.Sleep = 60;
            var config = GameBalanceConfig.Default();
            var before = ActionResolver.CalculateLaydownSuccessProbability(run, night, config);
            config.TemperamentModifiers[run.Temperament.Id].CribSensitivity = .05;
            var after = ActionResolver.CalculateLaydownSuccessProbability(run, night, config);
            Assert.Greater(after, before);
        }

        [Test]
        public void HungryTemperamentGainsMoreHunger()
        {
            var soft = RunState.Create(Temperament.Soft);
            var hungry = RunState.Create(Temperament.Hungry);
            var a = NightFactory.CreateNight(soft, new[] { ItemId.Carrier, ItemId.Noise, ItemId.Monitor });
            var b = NightFactory.CreateNight(hungry, new[] { ItemId.Carrier, ItemId.Noise, ItemId.Monitor });
            TurnResolver.EndTurn(soft, a, new SequenceRandomSource(.5));
            TurnResolver.EndTurn(hungry, b, new SequenceRandomSource(.5));
            Assert.Greater(b.Baby.Hunger, a.Baby.Hunger);
        }

        [Test]
        public void DifferentSeedsCanChooseDifferentTemperaments()
        {
            var ids = Enumerable.Range(0, 20).Select(x => RunState.Create(new RunSeed(x)).Temperament.Id).Distinct();
            Assert.Greater(ids.Count(), 1);
        }

        [Test]
        public void RequiredVictoryCountComesFromDefinition()
        {
            var night = new NightState();
            // 정확히 하나의 승리 조건만 충족되도록 상태를 고정한다: 깊은 잠만 충족,
            // 체력은 임계값 미만으로 낮춰 ParentStamina 조건을 제외한다.
            night.Baby.Sleep = 90;
            night.Parent.Stamina = 0;
            var definition = VictoryRuleDefinition.Default();
            definition.RequiredCount = 1;
            Assert.IsTrue(VictoryResolver.Evaluate(night, definition).IsVictory);
            definition.RequiredCount = 2;
            Assert.IsFalse(VictoryResolver.Evaluate(night, definition).IsVictory);
        }

        [Test]
        public void NarrativeResponseCannotMutateGameStateAndHasFallback()
        {
            var run = RunState.Create(Temperament.Soft);
            run.Memory.Carrier = .25;
            var before = run.Memory.Carrier;
            Assert.IsTrue(NarrativeBoundary.Validate("AI diary").IsValid);
            Assert.AreEqual(before, run.Memory.Carrier);
            Assert.IsFalse(NarrativeBoundary.Validate(null).IsValid);
            Assert.IsNotEmpty(NarrativeBoundary.GetFallback(NightOutcome.Awake));
        }

        [Test]
        public void EndingResultContainsIdButNoPresentationCopy()
        {
            var result = EndingResolver.Decide(TestHelpers.FinalRun(), new VictoryResult());
            Assert.AreEqual(EndingId.MorningWon, result.Id);
            Assert.IsNull(typeof(EndingResult).GetField("Title"));
            Assert.IsNull(typeof(EndingResult).GetField("Sub"));
        }

        [Test]
        public void CoreRecordsSemanticNightCompletionEvent()
        {
            var run = RunState.Create(Temperament.Soft);
            var night = NightFactory.CreateNight(run, new[] { ItemId.Carrier, ItemId.Noise, ItemId.Monitor });
            TestHelpers.PlayUntilMorning(run, night, new SystemRandomSource(1));
            Assert.IsTrue(night.Events.Any(e => e.Id == GameEventId.NightCompleted));
        }
    }
}
