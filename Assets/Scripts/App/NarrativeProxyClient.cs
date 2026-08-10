using System;
using System.Collections;
using System.Text;
using NotANap.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace NotANap.App
{
    /// <summary>
    /// 서버리스 프록시 URL 설정. API 키는 빌드와 저장소 어디에도 두지 않으며,
    /// 프록시가 키와 프롬프트를 소유한다. 클라이언트가 아는 것은 URL 하나뿐이다.
    /// URL이 비어 있으면 서술 호출을 하지 않고 규칙 기반 폴백만 사용한다.
    /// </summary>
    public static class NarrativeProxySettings
    {
        /// <summary>`Assets/Resources/narrative-proxy.json` (확장자 없이 로드).</summary>
        public const string ResourcePath = "narrative-proxy";

        /// <summary>에디터·로컬 실행용 덮어쓰기. WebGL 빌드에서는 항상 비어 있다.</summary>
        public const string EnvironmentVariable = "NOTANAP_NARRATIVE_PROXY_URL";

        private static bool _loaded;
        private static string _url;
        private static int _timeoutSeconds = 8;

        public static string Url
        {
            get { EnsureLoaded(); return _url; }
        }

        public static int TimeoutSeconds
        {
            get { EnsureLoaded(); return _timeoutSeconds; }
        }

        /// <summary>URL이 설정되어 있고 안전한 스킴일 때만 호출을 시도한다.</summary>
        public static bool Enabled => IsAllowed(Url);

        /// <summary>테스트·설정 변경 후 다시 읽게 한다.</summary>
        public static void Invalidate() => _loaded = false;

        /// <summary>
        /// GitHub Pages는 https로 서비스되므로 http 프록시는 혼합 콘텐츠로 차단된다.
        /// 로컬 개발 편의를 위해 localhost만 예외로 둔다.
        /// </summary>
        public static bool IsAllowed(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            string value = url.Trim();
            if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return true;
            return value.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _url = null;
            _timeoutSeconds = 8;

            string fromEnvironment = ReadEnvironment();
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                _url = fromEnvironment.Trim();
                return;
            }

            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text)) return;
            try
            {
                var config = JsonUtility.FromJson<NarrativeProxyConfig>(asset.text);
                if (config == null) return;
                _url = string.IsNullOrWhiteSpace(config.url) ? null : config.url.Trim();
                if (config.timeoutSeconds > 0) _timeoutSeconds = Mathf.Clamp(config.timeoutSeconds, 1, 30);
            }
            catch (ArgumentException)
            {
                // 설정 파일이 깨져 있어도 게임은 폴백 서술로 그대로 진행한다.
                _url = null;
            }
        }

        private static string ReadEnvironment()
        {
            // WebGL 런타임에는 환경 변수가 없다. 에디터·스탠드얼론에서만 의미가 있다.
            if (Application.platform == RuntimePlatform.WebGLPlayer) return null;
            try { return Environment.GetEnvironmentVariable(EnvironmentVariable); }
            catch (Exception) { return null; }
        }
    }

    [Serializable]
    public sealed class NarrativeProxyConfig
    {
        public string url;
        public int timeoutSeconds;
    }

    /// <summary>프록시 응답 JSON. 서술 문자열 다섯 개 외에는 어떤 필드도 읽지 않는다.</summary>
    [Serializable]
    internal sealed class NarrativeProxyResponseDto
    {
        public string noticedSignal;
        public string caregiverGrowth;
        public string habitReflection;
        public string familyUnderstanding;
        public string shareCard;
    }

    /// <summary>
    /// 밤 종료 시 1회 호출로 육아일지 서술을 받아 온다.
    /// 요청 본문은 Core가 만든 사실 JSON뿐이고, 응답은 NarrativeBoundary를 통과한 문자열만 살아남는다.
    /// 실패·타임아웃·검증 실패는 모두 null을 돌려주고 호출부는 규칙 기반 폴백을 유지한다.
    /// </summary>
    public static class NarrativeProxyClient
    {
        public static IEnumerator Request(string payload, Action<NarrativeResponse> onComplete)
        {
            if (onComplete == null) yield break;
            if (string.IsNullOrEmpty(payload) || !NarrativeProxySettings.Enabled)
            {
                onComplete(null);
                yield break;
            }

            using (var request = new UnityWebRequest(NarrativeProxySettings.Url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = NarrativeProxySettings.TimeoutSeconds;

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[Narrative] 프록시 호출 실패({request.result}). 폴백 서술을 사용한다.");
                    onComplete(null);
                    yield break;
                }
                onComplete(Parse(request.downloadHandler.text));
            }
        }

        /// <summary>응답 본문을 경계 규칙까지 통과시킨다. 실패하면 null.</summary>
        public static NarrativeResponse Parse(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            NarrativeProxyResponseDto dto;
            try { dto = JsonUtility.FromJson<NarrativeProxyResponseDto>(body); }
            catch (ArgumentException) { return null; }
            if (dto == null) return null;

            var validated = NarrativeBoundary.ValidateStructured(new NarrativeResponse
            {
                NoticedSignal = dto.noticedSignal,
                CaregiverGrowth = dto.caregiverGrowth,
                HabitReflection = dto.habitReflection,
                FamilyUnderstanding = dto.familyUnderstanding,
                ShareCard = dto.shareCard
            });
            return validated.IsValid ? validated : null;
        }
    }
}
