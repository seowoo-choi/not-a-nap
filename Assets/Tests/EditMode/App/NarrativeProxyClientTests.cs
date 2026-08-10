using NotANap.App;
using NUnit.Framework;

namespace NotANap.App.Tests
{
    /// <summary>
    /// 프록시 응답 해석과 URL 정책. 전송(UnityWebRequest) 자체는 실제 프록시가 설정된
    /// 빌드에서만 동작하므로, 여기서는 응답이 경계를 통과하는 경로와 설정 규칙을 고정한다.
    /// </summary>
    public sealed class NarrativeProxyClientTests
    {
        private const string ValidBody =
            "{\"noticedSignal\":\"아기는 울기 전에 입을 오물거렸다.\"," +
            "\"caregiverGrowth\":\"오늘은 서두르지 않고 하나씩 확인했다.\"," +
            "\"habitReflection\":\"토닥임을 가장 자주 골랐다.\"," +
            "\"familyUnderstanding\":\"다음 밤에는 준비를 먼저 하기로 했다.\"," +
            "\"shareCard\":\"최장 수면 95분\"}";

        [Test]
        public void ValidBodyBecomesBoundedNarrative()
        {
            var response = NarrativeProxyClient.Parse(ValidBody);

            Assert.IsNotNull(response);
            Assert.IsTrue(response.IsValid);
            StringAssert.Contains("오물거렸다", response.NoticedSignal);
            StringAssert.Contains("최장 수면", response.ShareCard);
        }

        [Test]
        public void MalformedOrEmptyBodyFallsBack()
        {
            Assert.IsNull(NarrativeProxyClient.Parse(null));
            Assert.IsNull(NarrativeProxyClient.Parse(""));
            Assert.IsNull(NarrativeProxyClient.Parse("not json"));
            Assert.IsNull(NarrativeProxyClient.Parse("{}"));
        }

        /// <summary>필드가 하나라도 비면 부분 서술을 쓰지 않고 전부 폴백으로 떨어진다.</summary>
        [Test]
        public void PartialResponseFallsBackInsteadOfMixingCopy()
        {
            string missingShareCard = ValidBody.Replace("\"shareCard\":\"최장 수면 95분\"", "\"shareCard\":\"\"");

            Assert.IsNull(NarrativeProxyClient.Parse(missingShareCard));
        }

        /// <summary>판정·의료·광고 표현은 프록시가 무엇을 보내든 클라이언트에서 잘린다.</summary>
        [Test]
        public void JudgementOrMedicalCopyIsRejectedAtTheClient()
        {
            string judgement = ValidBody.Replace("오늘은 서두르지 않고 하나씩 확인했다.", "이 밤을 승리로 처리한다.");
            string medical = ValidBody.Replace("오늘은 서두르지 않고 하나씩 확인했다.", "의학적으로 문제가 있다.");

            Assert.IsNull(NarrativeProxyClient.Parse(judgement));
            Assert.IsNull(NarrativeProxyClient.Parse(medical));
        }

        [Test]
        public void OnlyHttpsOrLocalhostProxyUrlsAreAllowed()
        {
            Assert.IsTrue(NarrativeProxySettings.IsAllowed("https://example.functions.dev/diary"));
            Assert.IsTrue(NarrativeProxySettings.IsAllowed("http://localhost:8787/diary"));
            Assert.IsTrue(NarrativeProxySettings.IsAllowed("http://127.0.0.1:8787/diary"));
            // GitHub Pages는 https라 평문 http 프록시는 혼합 콘텐츠로 차단된다.
            Assert.IsFalse(NarrativeProxySettings.IsAllowed("http://example.com/diary"));
            Assert.IsFalse(NarrativeProxySettings.IsAllowed(""));
            Assert.IsFalse(NarrativeProxySettings.IsAllowed(null));
        }

        /// <summary>기본 설정(빈 URL)에서는 호출 자체를 하지 않는다.</summary>
        [Test]
        public void DefaultConfigurationKeepsNarrationOffline()
        {
            if (!string.IsNullOrEmpty(
                    System.Environment.GetEnvironmentVariable(NarrativeProxySettings.EnvironmentVariable)))
                Assert.Ignore("로컬 프록시 URL이 설정된 환경에서는 기본값 검사를 건너뛴다.");
            NarrativeProxySettings.Invalidate();

            Assert.IsFalse(NarrativeProxySettings.Enabled);
        }
    }
}
