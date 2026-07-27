namespace NotANap.Presentation
{
    /// <summary>
    /// Presentation 전용 화면 상태. Core 로직과 무관하며 화면 흐름만 표현한다.
    /// 이번 수직 슬라이스 범위: TITLE → INTRO → SETUP → PLAY → DIARY → ENDING.
    /// </summary>
    public enum ScreenState
    {
        Title,
        Intro,
        Setup,
        Play,
        Diary,
        Ending
    }
}
