using System.Collections.Generic;

namespace NotANap.Core
{
    /// <summary>
    /// 밤 1회(21시 → 06시)의 전체 상태. 생성은 NightFactory, 변경은 Resolver를 통해서만.
    /// </summary>
    public sealed class NightState
    {
        public NightId NightId;
        public int Hour = GameConfig.StartHour;
        public BabyState Baby = new BabyState();
        public ParentState Parent = new ParentState();
        public WearingState Wearing = new WearingState();
        public List<ItemId> Items = new List<ItemId>();
        public int PacifierLeft = GameConfig.PacifierUsesPerNight;
        public bool PacifierInUse;
        public NightStats Stats = new NightStats();
        public List<GameLogEntry> Log = new List<GameLogEntry>();
        public List<GameEvent> Events = new List<GameEvent>();
        public bool Over;
        public NightOutcome? Result;
        /// <summary>이 밤에 소비된 턴 수 (21→06 = 9턴).</summary>
        public int ConsumedTurns;
        /// <summary>V2 분 단위 루프. V1 API와 저장 호환을 위해 별도 합성 상태로 둔다.</summary>
        public V2NightState V2;

        // ── 백일째 밤 전용 상태 ──
        /// <summary>이 밤에 발동 예정인 습관 표적 방해 이벤트 (최대 2개, 결정론적 선택).</summary>
        public List<TargetedEventId> ActiveTargetedEvents = new List<TargetedEventId>();
        /// <summary>이벤트 중복 발동 방지용 ID 기록.</summary>
        public HashSet<string> FiredEventIds = new HashSet<string>();
        /// <summary>버클 고장으로 아기띠 사용 불가인 남은 턴 수.</summary>
        public int CarrierDisabledTurns;
        /// <summary>배터리 방전으로 그 밤 내내 백색소음기 사용 불가.</summary>
        public bool NoiseDisabled;
        /// <summary>새벽 각성으로 인한 눕히기 성공 확률 추가 페널티.</summary>
        public double LaydownExtraPenalty;

        public bool HasItem(ItemId id) => Items.Contains(id);

        public void AddLog(string text, LogClass cls = LogClass.Sys)
            => Log.Add(new GameLogEntry(Hour, text, cls));

        public void AddEvent(GameEventId id) => Events.Add(new GameEvent(id, ConsumedTurns));

        /// <summary>아침(06시)까지 남은 시간. 원본: prototype hoursLeft().</summary>
        public int HoursLeft()
        {
            int h = Hour, c = 0;
            while (h != GameConfig.EndHour) { h = (h + 1) % 24; c++; }
            return c;
        }
    }
}
