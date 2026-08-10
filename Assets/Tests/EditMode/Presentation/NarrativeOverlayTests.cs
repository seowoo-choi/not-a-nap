using NotANap.Core;
using NotANap.Presentation;
using NUnit.Framework;

namespace NotANap.Presentation.Tests
{
    /// <summary>
    /// AI 서술이 화면 문구만 바꾸고 판정에는 닿지 못한다는 계약을 고정한다.
    /// 응답이 없거나 경계를 통과하지 못하면 규칙 기반 폴백 서술이 그대로 남아야 한다.
    /// </summary>
    public sealed class NarrativeOverlayTests
    {
        private static GameFlowController FinishedFirstNight(int seed = 41)
        {
            var flow = new GameFlowController(new SystemRandomSource(seed));
            flow.StartGame();
            flow.ToggleV2Item(ItemId.Monitor);
            flow.ToggleV2Item(ItemId.Noise);
            flow.ToggleV2Item(ItemId.Pacifier);
            flow.ConfirmV2Setup();
            TurnResolver.AdvanceMinutes(flow.Session.Run, flow.Session.Night, 540,
                GameBalanceConfig.Default(), new SystemRandomSource(8));
            flow.ActV2(V2ActionId.Hesitate);
            if (flow.PendingOverlay != null) flow.DismissOverlay();
            return flow;
        }

        private static NarrativeResponse Response(string marker = "AI") => new NarrativeResponse
        {
            NoticedSignal = $"{marker} 아기는 울기 전에 입을 오물거렸다.",
            CaregiverGrowth = $"{marker} 오늘은 서두르지 않고 한 가지씩 확인했다.",
            HabitReflection = $"{marker} 토닥임을 가장 자주 골랐다.",
            FamilyUnderstanding = $"{marker} 다음 밤에는 준비를 먼저 하기로 했다.",
            ShareCard = $"{marker} 최장 수면 95분"
        };

        [Test]
        public void ValidNarrativeReplacesOnlyDiaryCopy()
        {
            var flow = FinishedFirstNight();
            var before = flow.BuildV2Diary();
            int longestSleep = before.LongestSleepStretchMinutes;
            int wakeCount = before.WakeCount;
            var grade = before.Grade;
            Assert.IsFalse(before.NarrativeFromAi);

            Assert.IsTrue(flow.Session.ApplyNarrative(NightId.FirstNight, Response()));
            var after = flow.BuildV2Diary();

            Assert.IsTrue(after.NarrativeFromAi);
            StringAssert.StartsWith("AI", after.BabyResponseReflection);
            StringAssert.StartsWith("AI", after.CaregiverGrowth);
            StringAssert.StartsWith("AI", after.ActionLearning);
            StringAssert.StartsWith("AI", after.FamilyUnderstanding);
            StringAssert.StartsWith("AI", after.ShareCardText);
            // 판정에서 온 수치는 서술과 무관하게 그대로다.
            Assert.AreEqual(longestSleep, after.LongestSleepStretchMinutes);
            Assert.AreEqual(wakeCount, after.WakeCount);
            Assert.AreEqual(grade, after.Grade);
        }

        [Test]
        public void NarrativeDoesNotTouchRunOrNightState()
        {
            var flow = FinishedFirstNight();
            var run = flow.Session.Run;
            flow.BuildV2Diary(); // 기억 형성은 서술과 무관하게 먼저 끝난다.
            double carrier = run.Memory.Carrier;
            double heldDep = run.Memory.HeldDep;
            double noiseHabit = run.Memory.NoiseHab;
            double selfSoothe = run.Memory.SelfSoothe;
            int nightResults = run.NightResults.Count;
            int traces = run.Traces.Records.Count;

            flow.Session.ApplyNarrative(NightId.FirstNight, Response());
            flow.BuildV2Diary();

            Assert.AreEqual(carrier, run.Memory.Carrier);
            Assert.AreEqual(heldDep, run.Memory.HeldDep);
            Assert.AreEqual(noiseHabit, run.Memory.NoiseHab);
            Assert.AreEqual(selfSoothe, run.Memory.SelfSoothe);
            Assert.AreEqual(nightResults, run.NightResults.Count);
            Assert.AreEqual(traces, run.Traces.Records.Count);
        }

        [Test]
        public void RejectedNarrativeKeepsRuleBasedFallbackCopy()
        {
            var flow = FinishedFirstNight();
            string fallback = flow.BuildV2Diary().CaregiverGrowth;
            var judgement = Response();
            judgement.CaregiverGrowth = "이 밤을 승리로 처리한다.";

            Assert.IsFalse(flow.Session.ApplyNarrative(NightId.FirstNight, judgement));
            Assert.IsFalse(flow.Session.ApplyNarrative(NightId.FirstNight, null));

            var diary = flow.BuildV2Diary();
            Assert.IsFalse(diary.NarrativeFromAi);
            Assert.AreEqual(fallback, diary.CaregiverGrowth);
        }

        [Test]
        public void NarrativeFromEarlierNightDoesNotLeakIntoNextNight()
        {
            var flow = FinishedFirstNight();
            flow.Session.ApplyNarrative(NightId.FirstNight, Response());
            Assert.IsTrue(flow.BuildV2Diary().NarrativeFromAi);

            Assert.IsTrue(flow.AdvanceFromV2Diary());
            flow.ToggleV2Item(ItemId.Monitor);
            flow.ToggleV2Item(ItemId.Noise);
            flow.ToggleV2Item(ItemId.Pacifier);
            flow.ConfirmV2Setup();
            TurnResolver.AdvanceMinutes(flow.Session.Run, flow.Session.Night, 540,
                GameBalanceConfig.Default(), new SystemRandomSource(8));
            flow.ActV2(V2ActionId.Hesitate);
            if (flow.PendingOverlay != null) flow.DismissOverlay();

            var secondNight = flow.BuildV2Diary();
            Assert.AreEqual(NightId.SecondNight, secondNight.NightId);
            Assert.IsFalse(secondNight.NarrativeFromAi);
            StringAssert.DoesNotStartWith("AI", secondNight.CaregiverGrowth);
        }

        [Test]
        public void PayloadIsAvailableOnlyAfterTheNightIsOver()
        {
            var flow = new GameFlowController(new SystemRandomSource(41));
            flow.StartGame();
            flow.ToggleV2Item(ItemId.Monitor);
            flow.ToggleV2Item(ItemId.Noise);
            flow.ToggleV2Item(ItemId.Pacifier);
            flow.ConfirmV2Setup();

            Assert.IsNull(flow.Session.BuildNarrativePayload());

            TurnResolver.AdvanceMinutes(flow.Session.Run, flow.Session.Night, 540,
                GameBalanceConfig.Default(), new SystemRandomSource(8));
            flow.ActV2(V2ActionId.Hesitate);

            string payload = flow.Session.BuildNarrativePayload();
            StringAssert.Contains("\"contract\":\"diary.v2\"", payload);
            StringAssert.Contains("\"night\":\"FirstNight\"", payload);
        }
    }
}
