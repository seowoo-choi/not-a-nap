using System.Collections.Generic;

namespace NotANap.Core
{
    /// <summary>행동 처리 결과 값 객체. ActionResolver.Apply()가 반환.</summary>
    public sealed class ActionOutcome
    {
        public GameAction Action;
        /// <summary>행동 가능 여부 (거부되면 false).</summary>
        public bool Accepted;
        /// <summary>턴 소비 여부. true면 호출측이 TurnResolver.EndTurn()을 호출해야 한다.</summary>
        public bool ConsumedTurn;
        /// <summary>이 행동으로 발생한 로그 (밤 전체 로그에도 동일하게 기록됨).</summary>
        public List<GameLogEntry> Log = new List<GameLogEntry>();
        /// <summary>눕히기를 실제로 시도했는지.</summary>
        public bool LaydownAttempted;
        /// <summary>눕히기 성공 여부.</summary>
        public bool LaydownSucceeded;
        /// <summary>맨손 눕히기 성공으로 기록되었는지.</summary>
        public bool BareHandsLaydown;
        /// <summary>습관(memory)으로 인한 확률/효과 보정이 적용되었는지.</summary>
        public bool HabitPenaltyApplied;
    }
}
