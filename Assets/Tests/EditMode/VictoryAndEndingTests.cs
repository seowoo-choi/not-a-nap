using NUnit.Framework;
using NotANap.Core;

namespace NotANap.Core.Tests
{
    public class VictoryAndEndingTests
    {
        private static NightState NightWithConditions(bool sleep, bool stamina, bool bareHands)
        {
            var night = new NightState { NightId = NightId.HundredthNight };
            night.Baby.Sleep = sleep ? 85 : 20;
            night.Baby.Crying = false;
            night.Parent.Stamina = stamina ? 30 : 29;
            night.Stats.BareHandsLaydownSucceeded = bareHands;
            return night;
        }

        [TestCase(false, false, false, 0, false)]
        [TestCase(true, false, false, 1, false)]
        [TestCase(true, true, false, 2, true)]
        [TestCase(true, true, true, 3, true)]
        public void Victory_RequiresTwoOfThreeConditions(
            bool sleep, bool stamina, bool bareHands, int count, bool victory)
        {
            var result = VictoryResolver.Evaluate(NightWithConditions(sleep, stamina, bareHands));
            Assert.AreEqual(count, result.Count);
            Assert.AreEqual(victory, result.IsVictory);
        }

        private static VictoryResult WinningVictory()
        {
            var result = new VictoryResult();
            result.Met.Add(VictoryCondition.DeepSleepMorning);
            result.Met.Add(VictoryCondition.ParentStamina);
            return result;
        }

        [Test]
        public void Ending_MorningWon()
        {
            var run = TestHelpers.FinalRun();
            var result = EndingResolver.Decide(run, new VictoryResult());
            AssertEnding(result, EndingId.MorningWon, false);
        }

        [Test]
        public void Ending_FamilyRoutine()
        {
            var run = TestHelpers.FinalRun();
            var result = EndingResolver.Decide(run, WinningVictory());
            AssertEnding(result, EndingId.FamilyRoutine, true);
        }

        [Test]
        public void Ending_UniverseInArms()
        {
            var run = TestHelpers.FinalRun();
            run.Memory.Carrier = 0.5;
            var result = EndingResolver.Decide(run, WinningVictory());
            AssertEnding(result, EndingId.UniverseInArms, true);
        }

        [Test]
        public void Ending_GrandmaBest()
        {
            var run = TestHelpers.FinalRun();
            run.Memory.NoiseHab = 0.4;
            run.GrandmaUsed = true;
            var result = EndingResolver.Decide(run, WinningVictory());
            AssertEnding(result, EndingId.GrandmaBest, true);
        }

        [Test]
        public void Ending_GearMaster()
        {
            var run = TestHelpers.FinalRun();
            run.Memory.NoiseHab = 0.4;
            run.UsedItemKinds.UnionWith(new[]
            {
                ItemId.Carrier, ItemId.Noise, ItemId.Bouncer, ItemId.Monitor
            });
            var result = EndingResolver.Decide(run, WinningVictory());
            AssertEnding(result, EndingId.GearMaster, true);
        }

        [Test]
        public void Ending_DawnSurvivor()
        {
            var run = TestHelpers.FinalRun();
            run.Memory.NoiseHab = 0.4;
            var result = EndingResolver.Decide(run, WinningVictory());
            AssertEnding(result, EndingId.DawnSurvivor, true);
        }

        [Test]
        public void Ending_OverlappingConditionsUseDocumentedPriority()
        {
            var run = TestHelpers.FinalRun();
            run.Memory.Carrier = 0.6;
            run.Memory.NoiseHab = 0.6;
            run.GrandmaUsed = true;
            run.UsedItemKinds.UnionWith(new[]
            {
                ItemId.Carrier, ItemId.Pacifier, ItemId.Noise, ItemId.Bouncer, ItemId.Monitor
            });

            var result = EndingResolver.Decide(run, WinningVictory());

            Assert.AreEqual(EndingId.UniverseInArms, result.Id,
                "품 안의 우주는 할머니와 장비 엔딩보다 우선해야 한다.");
        }

        private static void AssertEnding(EndingResult result, EndingId id, bool success)
        {
            Assert.AreEqual(id, result.Id);
            Assert.AreEqual(success, result.IsSuccess);
            Assert.AreEqual(success ? 2 : 0, result.MetConditions.Count);
        }
    }
}
