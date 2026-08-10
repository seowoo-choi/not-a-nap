using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NotANap.Core
{
    /// <summary>
    /// 밤 종료 서술 요청을 밤당 정확히 1회로 제한한다.
    /// 판정과 무관한 호출 관리 전용이며 Core 상태를 읽지도 바꾸지도 않는다.
    /// </summary>
    public sealed class NarrativeCallGate
    {
        private readonly HashSet<NightId> _requested = new HashSet<NightId>();

        /// <summary>이 밤의 첫 요청이면 true. 두 번째부터는 항상 false.</summary>
        public bool TryBegin(NightId night) => _requested.Add(night);

        public bool WasRequested(NightId night) => _requested.Contains(night);

        /// <summary>런 전체에서 실제로 허용된 호출 수. 세 밤을 모두 지나도 3을 넘을 수 없다.</summary>
        public int RequestCount => _requested.Count;

        public void Reset() => _requested.Clear();
    }

    /// <summary>
    /// 검증된 밤 기록(NarrativeFacts)을 서버리스 프록시로 보낼 JSON으로 직렬화한다.
    /// 나가는 값은 열거형 ID와 수치뿐이다. 플레이어 입력 문자열과 프롬프트 문구는 포함하지 않으며,
    /// 프롬프트와 API 키는 프록시가 소유한다. 클라이언트는 사실만 내보낸다.
    /// </summary>
    public static class NarrativeRequest
    {
        /// <summary>프록시가 프롬프트 계약을 식별하는 버전 태그.</summary>
        public const string ContractVersion = "diary.v2";

        public static string BuildPayload(NarrativeFacts facts, NightGrade grade)
        {
            if (facts == null) return null;
            var sb = new StringBuilder(512);
            sb.Append('{');
            Text(sb, "contract", ContractVersion).Append(',');
            Text(sb, "night", facts.NightId.ToString()).Append(',');
            Text(sb, "grade", grade.ToString()).Append(',');

            sb.Append("\"metrics\":{");
            Number(sb, "longestSleepMinutes", facts.LongestSleepMinutes).Append(',');
            Number(sb, "wakeCount", facts.WakeCount).Append(',');
            Number(sb, "parentStamina", facts.ParentStamina).Append(',');
            Number(sb, "bareHandsLaydownAttempts", facts.BareHandsLaydownAttempts).Append(',');
            Bool(sb, "bareHandsLaydownSucceeded", facts.BareHandsLaydownSucceeded).Append(',');
            Bool(sb, "usedCatchBreath", facts.UsedCatchBreath).Append(',');
            Bool(sb, "feedingPreparationIncident", facts.FeedingPreparationIncident).Append(',');
            Number(sb, "longestMovementMinutes", facts.LongestMovementMinutes);
            sb.Append("},");

            sb.Append("\"signals\":{");
            Enum(sb, "firstNoticed", facts.FirstNoticedSignal);
            sb.Append("},");

            sb.Append("\"actions\":{");
            Enum(sb, "mostRepeated", facts.MostRepeatedAction).Append(',');
            Number(sb, "mostRepeatedCount", facts.MostRepeatedActionCount).Append(',');
            Enum(sb, "rejected", facts.RejectedAction).Append(',');
            Enum(sb, "followup", facts.FollowupAction).Append(',');
            Enum(sb, "longestPreparationStep", facts.LongestPreparationStep).Append(',');
            Enum(sb, "sleepIntervalChoice", facts.SleepIntervalChoice).Append(',');
            Enum(sb, "longestMovementDestination", facts.LongestMovementDestination);
            sb.Append("},");

            sb.Append("\"rhythms\":[");
            for (int i = 0; i < facts.Rhythms.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var rhythm = facts.Rhythms[i];
                sb.Append('{');
                Text(sb, "id", rhythm.Id.ToString()).Append(',');
                Number(sb, "strength", rhythm.Strength).Append(',');
                Number(sb, "sourceCount", rhythm.SourceCount);
                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static StringBuilder Text(StringBuilder sb, string key, string value)
            => Key(sb, key).Append('"').Append(Escape(value)).Append('"');

        private static StringBuilder Number(StringBuilder sb, string key, int value)
            => Key(sb, key).Append(value.ToString(CultureInfo.InvariantCulture));

        private static StringBuilder Number(StringBuilder sb, string key, double value)
            => Key(sb, key).Append(value.ToString("0.###", CultureInfo.InvariantCulture));

        private static StringBuilder Bool(StringBuilder sb, string key, bool value)
            => Key(sb, key).Append(value ? "true" : "false");

        private static StringBuilder Enum<T>(StringBuilder sb, string key, T? value) where T : struct
            => value.HasValue ? Text(sb, key, value.Value.ToString()) : Key(sb, key).Append("null");

        private static StringBuilder Key(StringBuilder sb, string key)
            => sb.Append('"').Append(key).Append("\":");

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var sb = new StringBuilder(value.Length + 8);
            foreach (char c in value)
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            return sb.ToString();
        }
    }
}
