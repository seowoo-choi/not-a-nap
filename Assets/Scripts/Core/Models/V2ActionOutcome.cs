using System.Collections.Generic;

namespace NotANap.Core
{
    public sealed class V2ActionOutcome
    {
        public V2ActionId Action;
        public bool Accepted;
        public bool ConsumedTime;
        public int TimeDeltaMinutes;
        public double StaminaDelta;
        public bool CauseResolved;
        /// <summary>선택한 돌봄이 현재 각성 원인과 달라 추가 부담이 발생했는지.</summary>
        public bool WasMisdiagnosis;
        /// <summary>관찰된 배고픔 신호가 이번 각성 원인과 같은 방향인지.</summary>
        public bool HungerSignalsMatchCause;
        public V2ActionBlockReason BlockReason;
        public HungerSignalStage HungerSignalStage;
        public DiaperCheckResult DiaperCheckResult;
        public bool MonitorRead;
        public bool HeadSupported;
        /// <summary>이 행동으로 줄어든 울음 세기(양수). 원인 해소와 무관한 '달래기'의 결과다.</summary>
        public double CryRelief;
        /// <summary>이 행동 전후로 아기 기분(0~100)이 얼마나 움직였는지. 표시 전용.</summary>
        public double MoodBefore;
        public double MoodAfter;
        public double MoodDelta => MoodAfter - MoodBefore;
        public string ActivityLocation;
        public readonly List<ObservationSignalId> ObservedSignals = new List<ObservationSignalId>();
        public readonly List<FeedingPreparationStep> MissingPreparationSteps = new List<FeedingPreparationStep>();
        public readonly List<GameEventId> EventIds = new List<GameEventId>();
        public readonly List<TraceId> TraceIds = new List<TraceId>();
        public StateDelta StateDelta = new StateDelta();
    }

    public sealed class HomeMoveOutcome
    {
        public bool Accepted;
        public HomeLocation From;
        public HomeLocation To;
        public int TimeDeltaMinutes;
        public bool BabyAccompanied;
        public bool RetrievedBathThermometer;
    }
}
