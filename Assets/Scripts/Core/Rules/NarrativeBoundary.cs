namespace NotANap.Core
{
    public sealed class NarrativeResponse
    {
        public string DiaryText;
        public bool IsValid => !string.IsNullOrWhiteSpace(DiaryText);
    }

    /// <summary>AI 결과를 서술 문자열로만 제한하는 경계. 게임 상태를 받거나 변경하지 않는다.</summary>
    public static class NarrativeBoundary
    {
        public static NarrativeResponse Validate(string diaryText) => new NarrativeResponse
        {
            DiaryText = string.IsNullOrWhiteSpace(diaryText) ? null : diaryText.Trim()
        };

        public static string GetFallback(NightOutcome outcome)
        {
            switch (outcome)
            {
                case NightOutcome.Crib: return "fallback.diary.crib";
                case NightOutcome.Arms: return "fallback.diary.arms";
                default: return "fallback.diary.awake";
            }
        }
    }
}
