using System.Collections.Generic;
using NotANap.Core;

namespace NotANap.Presentation
{
    /// <summary>
    /// 화면이 그릴 값만 담은 읽기 전용 스냅샷들. 전부 Core 상태에서 파생되며
    /// Presentation은 여기서 수치를 재계산하거나 Core 상태를 바꾸지 않는다.
    /// </summary>

    /// <summary>SETUP 화면 스냅샷.</summary>
    public sealed class SetupViewModel
    {
        public NightId NightId;
        public string NightLabel;
        /// <summary>기질 힌트 문장만 노출. 실제 TemperamentId는 숨긴다.</summary>
        public string TemperamentHint;
        public int Slots;
        public int SelectedCount;
        public bool CanStart;
        public List<ItemCardViewModel> Cards = new List<ItemCardViewModel>();
    }

    /// <summary>SETUP 아이템 카드 한 장.</summary>
    public sealed class ItemCardViewModel
    {
        public ItemId Id;
        public string Emoji;
        public string Name;
        public string Desc;
        public string Side;
        public bool Selected;
        /// <summary>슬롯이 다 찼고 미선택이면 true (흐리게/입력 차단).</summary>
        public bool Disabled;
        /// <summary>Bouncer 등 LEGACY 표시용.</summary>
        public bool Legacy;
    }

    /// <summary>PLAY 화면 스냅샷.</summary>
    public sealed class PlayViewModel
    {
        public NightId NightId;
        public string Clock;          // "21:00"
        public int TurnsLeft;         // 남은 소비 턴 수
        public string StageWord;      // GetStage() 기반 상태 단어
        public double Stamina;        // 보호자 체력 (0~100)

        /// <summary>Monitor 소지 시에만 채워진다. 미소지면 null.</summary>
        public double? Calm;
        public double? Sleep;
        public double? Hunger;
        public bool HasMonitor;

        public bool BabyHeld;
        public bool BabyCrying;

        public List<LogLineViewModel> RecentLog = new List<LogLineViewModel>();
        public List<ActionButtonViewModel> Actions = new List<ActionButtonViewModel>();

        public bool NightOver;

        /// <summary>결정론 비교용 안정 문자열 스냅샷.</summary>
        public string ToSnapshot()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(NightId).Append('|')
              .Append(Clock).Append('|')
              .Append(TurnsLeft).Append('|')
              .Append(StageWord).Append('|')
              .Append(Stamina.ToString("0.###")).Append('|')
              .Append(HasMonitor ? "M" : "-").Append('|')
              .Append(Calm?.ToString("0.###") ?? "?").Append('/')
              .Append(Sleep?.ToString("0.###") ?? "?").Append('/')
              .Append(Hunger?.ToString("0.###") ?? "?").Append('|')
              .Append(BabyHeld ? "H" : "-").Append(BabyCrying ? "C" : "-").Append('|')
              .Append(NightOver ? "OVER" : "ON").Append('|');
            foreach (var a in Actions)
                sb.Append(a.Action).Append(a.Enabled ? '+' : '-')
                  .Append(a.Toggled ? 't' : '.')
                  .Append(a.BadgeText ?? "").Append(',');
            sb.Append('|');
            foreach (var l in RecentLog)
                sb.Append(l.Hour).Append(':').Append(l.Cls).Append(':').Append(l.Text).Append('\n');
            return sb.ToString();
        }
    }

    /// <summary>로그 한 줄.</summary>
    public sealed class LogLineViewModel
    {
        public int Hour;
        public string Text;
        public LogClass Cls;
    }

    /// <summary>행동 버튼 한 개.</summary>
    public sealed class ActionButtonViewModel
    {
        public GameAction Action;
        public string Label;
        public bool Enabled;
        /// <summary>토글 아이템 ON 상태 표시.</summary>
        public bool Toggled;
        /// <summary>쪽쪽이 잔여 횟수 등 뱃지 텍스트. 없으면 null.</summary>
        public string BadgeText;
        /// <summary>시간을 소비하는 행동인지 (화면 힌트용).</summary>
        public bool ConsumesTime;
    }

    /// <summary>PLAY 위에 뜨는 결과 오버레이 스냅샷.</summary>
    public sealed class OverlayViewModel
    {
        public string Title;
        public List<string> Lines = new List<string>();
    }

    /// <summary>DIARY 화면 스냅샷.</summary>
    public sealed class DiaryViewModel
    {
        public NightId NightId;
        public string NightLabel;
        public NightOutcome Outcome;
        public string OutcomePhrase;
        /// <summary>규칙 기반 임시 일지 문구 (AI 연동은 이번 범위 아님).</summary>
        public string DiaryText;
        public List<MemoryNoteViewModel> Notes = new List<MemoryNoteViewModel>();
        public bool HasNextNight;
    }

    /// <summary>형성된 습관 카드 한 장.</summary>
    public sealed class MemoryNoteViewModel
    {
        public bool Positive;
        public string Text;
        public string Sub;
    }

    /// <summary>행동 실행 결과. 흐름 제어와 테스트 검증용.</summary>
    public sealed class ActionResult
    {
        /// <summary>입력 잠금/오버레이/밤 종료로 무시된 클릭.</summary>
        public bool Ignored;
        public bool Accepted;
        public bool ConsumedTurn;
        /// <summary>Presentation이 실제로 TurnResolver.EndTurn을 호출했는지.</summary>
        public bool EndTurnInvoked;
        public ActionOutcome Outcome;
        /// <summary>이번 행동으로 새로 떠야 하는 오버레이 (없으면 null).</summary>
        public OverlayViewModel Overlay;

        public static ActionResult IgnoredResult() => new ActionResult { Ignored = true };
    }

    /// <summary>V2 분 단위 밤 루프를 위한 화면 스냅샷. Core 값만 전달한다.</summary>
    public sealed class V2PlayViewModel
    {
        public NightId NightId;
        public string Clock;
        public int ElapsedMinutes;
        public int RemainingMinutes;
        public V2SleepStage SleepStage;
        public WakeCause? RevealedCause;
        public bool CauseResolved;
        public int DecisionSecondsRemaining;
        public int CurrentSleepStretchMinutes;
        public int LongestSleepStretchMinutes;
        public int TotalSleepMinutes;
        public int WakeCount;
        public int CorrectFirstChecks;
        public int MisdiagnosisCount;
        public double Calm;
        public double DrowsyCalmThreshold;
        public double SleepStartCalmThreshold;
        public double ParentStamina;
        public double CryIntensity;
        public double Hunger;
        public bool IsLimbRelaxed;
        public bool IsBreathingRegular;
        public bool DeepSleepObserved;
        public double TemperatureCelsius;
        public double HumidityPercent;
        public bool TemperatureChecked;
        public bool HumidityChecked;
        public bool FeedingReady;
        public bool HasNoise;
        public bool NoiseOn;
        public bool HasMonitor;
        public NightGrade? Grade;
        public readonly List<V2ActionButtonViewModel> Actions = new List<V2ActionButtonViewModel>();
    }

    public sealed class V2ActionButtonViewModel
    {
        public V2ActionId Action;
        public string Label;
        public bool Enabled;
    }

    /// <summary>V2 Core의 구조화된 결과를 손실 없이 Presentation에 전달한다.</summary>
    public sealed class V2PresentationActionResult
    {
        public bool Ignored;
        public V2ActionOutcome Outcome;
        public OverlayViewModel Overlay;

        public static V2PresentationActionResult IgnoredResult()
            => new V2PresentationActionResult { Ignored = true };
    }

    public sealed class V2DiaryViewModel
    {
        public NightId NightId;
        public string NightLabel;
        public NightGrade Grade;
        public int LongestSleepStretchMinutes;
        public int TotalSleepMinutes;
        public int WakeCount;
        public int CorrectFirstChecks;
        public int MisdiagnosisCount;
        public int UnsafeChoiceCount;
        public double ParentStaminaAtDawn;
        public bool HasNextNight;
    }
}
