using NotANap.Core;
using NUnit.Framework;

namespace NotANap.Core.Tests
{
    /// <summary>밤 종료 서술 요청의 경계 검증. 나가는 사실과 호출 횟수를 코드로 고정한다.</summary>
    public sealed class NarrativeRequestTests
    {
        private static NarrativeFacts Facts()
        {
            var facts = new NarrativeFacts
            {
                NightId = NightId.SecondNight,
                FirstNoticedSignal = ObservationSignalId.Rooting,
                MostRepeatedAction = V2ActionId.Pat,
                MostRepeatedActionCount = 4,
                RejectedAction = V2ActionId.Pacifier,
                FollowupAction = V2ActionId.Hold,
                LongestSleepMinutes = 95,
                WakeCount = 3,
                ParentStamina = 42.5,
                UsedCatchBreath = true,
                BareHandsLaydownAttempts = 2,
                BareHandsLaydownSucceeded = true,
                LongestMovementDestination = HomeLocation.Kitchen,
                LongestMovementMinutes = 6
            };
            facts.Rhythms.Add(new RhythmFact { Id = RhythmId.Carrier, Strength = .25, SourceCount = 3 });
            return facts;
        }

        [Test]
        public void PayloadCarriesContractNightAndMeasuredFacts()
        {
            string payload = NarrativeRequest.BuildPayload(Facts(), NightGrade.B);

            StringAssert.Contains("\"contract\":\"diary.v2\"", payload);
            StringAssert.Contains("\"night\":\"SecondNight\"", payload);
            StringAssert.Contains("\"grade\":\"B\"", payload);
            StringAssert.Contains("\"longestSleepMinutes\":95", payload);
            StringAssert.Contains("\"parentStamina\":42.5", payload);
            StringAssert.Contains("\"firstNoticed\":\"Rooting\"", payload);
            StringAssert.Contains("\"mostRepeated\":\"Pat\"", payload);
            StringAssert.Contains("\"id\":\"Carrier\"", payload);
        }

        /// <summary>사실이 없으면 문자열을 지어내지 않고 null로 내보낸다.</summary>
        [Test]
        public void AbsentFactsSerializeAsNullInsteadOfInventedText()
        {
            string payload = NarrativeRequest.BuildPayload(new NarrativeFacts(), NightGrade.D);

            StringAssert.Contains("\"firstNoticed\":null", payload);
            StringAssert.Contains("\"rejected\":null", payload);
            StringAssert.Contains("\"sleepIntervalChoice\":null", payload);
            StringAssert.Contains("\"rhythms\":[]", payload);
        }

        /// <summary>
        /// 요청 본문에는 ID와 수치만 담긴다. 화면 문구와 아기 이름 같은 플레이어 입력은
        /// 프록시로 나가지 않는다. 한글이 한 글자라도 있으면 자유 문장이 섞인 것이다.
        /// </summary>
        [Test]
        public void PayloadCarriesNoFreeTextOrPlayerInput()
        {
            string payload = NarrativeRequest.BuildPayload(Facts(), NightGrade.S);

            foreach (char c in payload)
                Assert.Less(c, 128, $"요청 본문에 자유 문장이 섞였다: {payload}");
        }

        [Test]
        public void SameFactsProduceIdenticalPayload()
        {
            Assert.AreEqual(NarrativeRequest.BuildPayload(Facts(), NightGrade.A),
                NarrativeRequest.BuildPayload(Facts(), NightGrade.A));
        }

        [Test]
        public void GateAllowsExactlyOneRequestPerNight()
        {
            var gate = new NarrativeCallGate();

            Assert.IsTrue(gate.TryBegin(NightId.FirstNight));
            Assert.IsFalse(gate.TryBegin(NightId.FirstNight));
            Assert.IsFalse(gate.TryBegin(NightId.FirstNight));
            Assert.IsTrue(gate.WasRequested(NightId.FirstNight));
            Assert.AreEqual(1, gate.RequestCount);
        }

        [Test]
        public void GateCapsWholeRunAtThreeCalls()
        {
            var gate = new NarrativeCallGate();

            foreach (var night in new[] { NightId.FirstNight, NightId.SecondNight, NightId.HundredthNight })
                for (int attempt = 0; attempt < 5; attempt++)
                    gate.TryBegin(night);

            Assert.AreEqual(3, gate.RequestCount);
        }

        [Test]
        public void BoundaryRejectsJudgementAndAdvertisingResponses()
        {
            var judgement = NarrativeBoundary.ValidateStructured(new NarrativeResponse
            {
                NoticedSignal = "아기가 입을 오물거렸다.",
                CaregiverGrowth = "오늘 밤은 승리로 처리한다.",
                HabitReflection = "같은 방법을 두 번 썼다.",
                FamilyUnderstanding = "내일은 더 일찍 준비하기로 했다.",
                ShareCard = "최장 수면 95분"
            });
            var advertisement = NarrativeBoundary.ValidateStructured(new NarrativeResponse
            {
                NoticedSignal = "아기가 입을 오물거렸다.",
                CaregiverGrowth = "이 제품을 사면 편해진다.",
                HabitReflection = "같은 방법을 두 번 썼다.",
                FamilyUnderstanding = "내일은 더 일찍 준비하기로 했다.",
                ShareCard = "최장 수면 95분"
            });

            Assert.IsFalse(judgement.IsValid);
            Assert.IsFalse(advertisement.IsValid);
        }
    }
}
