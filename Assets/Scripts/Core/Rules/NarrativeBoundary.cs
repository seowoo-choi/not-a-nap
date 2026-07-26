using System;

namespace NotANap.Core
{
    public sealed class NarrativeResponse
    {
        public string NoticedSignal;
        public string CaregiverGrowth;
        public string HabitReflection;
        public string FamilyUnderstanding;
        public string ShareCard;
        public string DiaryText;
        public bool IsValid => !string.IsNullOrWhiteSpace(DiaryText) || (!string.IsNullOrWhiteSpace(NoticedSignal) &&
            !string.IsNullOrWhiteSpace(CaregiverGrowth) &&
            !string.IsNullOrWhiteSpace(HabitReflection) &&
            !string.IsNullOrWhiteSpace(FamilyUnderstanding) &&
            !string.IsNullOrWhiteSpace(ShareCard));
    }

    /// <summary>AI 결과를 서술 문자열로만 제한한다. Core 상태와 판정 명령은 입력받지 않는다.</summary>
    public static class NarrativeBoundary
    {
        private const int FieldLimit = 180;
        private static readonly string[] RejectedTerms =
        {
            "진단", "치료", "처방", "의학적으로", "약을 먹", "투약",
            "구매", "할인", "광고", "제품을 사",
            "승패를", "승리로", "패배로", "memory", "상태를 변경", "상태로 바꿔",
            "규칙을 변경", "규칙을 바꿔"
        };

        public static NarrativeResponse ValidateStructured(NarrativeResponse response)
        {
            if (response == null || !Valid(response.NoticedSignal) || !Valid(response.CaregiverGrowth) ||
                !Valid(response.HabitReflection) || !Valid(response.FamilyUnderstanding) ||
                !Valid(response.ShareCard)) return new NarrativeResponse();
            return new NarrativeResponse
            {
                NoticedSignal = response.NoticedSignal.Trim(),
                CaregiverGrowth = response.CaregiverGrowth.Trim(),
                HabitReflection = response.HabitReflection.Trim(),
                FamilyUnderstanding = response.FamilyUnderstanding.Trim(),
                ShareCard = response.ShareCard.Trim()
            };
        }

        /// <summary>이전 단일 문자열 호출부 호환. 새 AI 계약에서는 구조화 응답을 사용한다.</summary>
        public static NarrativeResponse Validate(string diaryText)
            => new NarrativeResponse { DiaryText = Valid(diaryText) ? diaryText.Trim() : null };

        private static bool Valid(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > FieldLimit) return false;
            foreach (var term in RejectedTerms)
                if (value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return true;
        }

        public static string GetFallback(NightOutcome outcome) => outcome switch
        {
            NightOutcome.Crib => "fallback.diary.crib",
            NightOutcome.Arms => "fallback.diary.arms",
            _ => "fallback.diary.awake"
        };
    }
}
