using NotANap.Core;

namespace NotANap.Presentation
{
    /// <summary>
    /// GameEventId·상태값 → 화면 문장 매핑. 화면 카피는 전부 여기 있고 Core 로직에는 넣지 않는다.
    /// (screen-spec 2절: "Core는 화면 문장을 만들지 않는다.")
    /// </summary>
    public static class PresentationCopyMapper
    {
        public static string NightLabel(NightId id) => id switch
        {
            NightId.FirstNight => "첫째 밤",
            NightId.SecondNight => "둘째 밤",
            NightId.HundredthNight => "백일째 밤",
            _ => id.ToString()
        };

        public static string EndingTitle(EndingId id) => id switch
        {
            EndingId.MorningWon => "아침이 이겼다",
            EndingId.FamilyRoutine => "우리 집의 루틴",
            EndingId.UniverseInArms => "품 안의 우주",
            EndingId.GrandmaBest => "할머니가 최고야",
            EndingId.GearMaster => "장비의 지배자",
            _ => "새벽의 생존자"
        };

        public static string EndingSubtitle(EndingId id) => id switch
        {
            EndingId.MorningWon => "완벽하지 않아도 괜찮다. 오늘의 신호는 다음 밤의 기억이 된다.",
            EndingId.FamilyRoutine => "서로에게 무리 없는 리듬이 마침내 우리 집의 밤이 되었다.",
            EndingId.UniverseInArms => "기억된 품의 온기 속에서 백일의 밤을 함께 건넜다.",
            EndingId.GrandmaBest => "건네받은 품도 가족의 루틴이 되어 밤을 지켰다.",
            EndingId.GearMaster => "도구를 정답이 아니라 우리에게 맞는 언어로 사용했다.",
            _ => "흔들리는 새벽에도 서로를 놓치지 않고 아침에 닿았다."
        };

        public static string EndingSymbol(EndingId id) => id switch
        {
            EndingId.MorningWon => "☀",
            EndingId.FamilyRoutine => "⌂",
            EndingId.UniverseInArms => "●",
            EndingId.GrandmaBest => "♡",
            EndingId.GearMaster => "✦",
            _ => "☾"
        };

        public static string EndingStatusLabel(bool isSuccess)
            => isSuccess ? "지켜 낸 밤" : "아쉬운 밤";

        public static string VictoryConditionLabel(VictoryCondition condition) => condition switch
        {
            VictoryCondition.DeepSleepMorning => "아기가 깊은 잠으로 아침을 맞음",
            VictoryCondition.ParentStamina => "보호자 체력 30 이상",
            VictoryCondition.BareHandsLaydown => "맨손 눕히기 성공",
            _ => condition.ToString()
        };

        /// <summary>screen-spec 3절 상태 단어 표.</summary>
        public static string StageWord(SleepStage stage) => stage switch
        {
            SleepStage.Cry => "대성통곡",
            SleepStage.Deep => "깊은 잠",
            SleepStage.Shallow => "선잠",
            SleepStage.Drowsy => "꾸벅꾸벅",
            SleepStage.Fussy => "짜증 폭발 직전",
            SleepStage.Awake => "말똥말똥",
            _ => stage.ToString()
        };

        public static string OutcomePhrase(NightOutcome outcome) => outcome switch
        {
            NightOutcome.Crib => "침대에서 아침을 맞았다.",
            NightOutcome.Arms => "품에 안긴 채 아침을 맞았다.",
            NightOutcome.Awake => "끝내 잠들지 못한 채 아침이 왔다.",
            _ => outcome.ToString()
        };

        public static string ActionLabel(GameAction action) => action switch
        {
            GameAction.Hold => "안기",
            GameAction.Pat => "토닥",
            GameAction.Feed => "수유",
            GameAction.Laydown => "눕히기",
            GameAction.Watch => "지켜보기",
            GameAction.Grandma => "할머니 찬스",
            GameAction.Pacifier => "쪽쪽이",
            GameAction.ToggleCarrier => "아기띠",
            GameAction.ToggleNoise => "소음기",
            GameAction.ToggleBouncer => "바운서",
            _ => action.ToString()
        };

        public static string V2ActionLabel(V2ActionId action) => action switch
        {
            V2ActionId.Hold => "목을 받치고 품에 안기",
            V2ActionId.Pat => "천천히 토닥이기",
            V2ActionId.Laydown => "조심히 눕히기",
            V2ActionId.Pacifier => "쪽쪽이 건네기",
            V2ActionId.CheckLimbRelaxation => "팔다리 이완 확인",
            V2ActionId.CheckDiaper => "기저귀 확인",
            V2ActionId.ChangeDiaper => "기저귀 갈기",
            V2ActionId.CheckHungerSignals => "배고픔 신호 확인",
            V2ActionId.CheckEnvironment => "온도·습도",
            V2ActionId.CheckBodyTemperature => "아기 체온 확인",
            V2ActionId.AdjustTemperature => "온도 조절",
            V2ActionId.AdjustHumidity => "습도 조절",
            V2ActionId.Hesitate => "잠시 망설임",
            V2ActionId.SterilizeBottle => "젖병 소독",
            V2ActionId.PrepareWater => "분유 준비",
            V2ActionId.MeasureFormula => "분유 계량",
            V2ActionId.MixFormula => "분유 혼합",
            V2ActionId.CoolBottle => "식히고 온도 확인",
            V2ActionId.CheckBottleTemperature => "분유 온도 확인",
            V2ActionId.FeedPreparedBottle => "준비한 분유 수유",
            V2ActionId.HoldWhilePreparing => "안고 준비하기",
            V2ActionId.ToggleCarrier => "아기띠 착용/벗기",
            V2ActionId.ToggleNoise => "백색소음기 켜기/끄기",
            V2ActionId.CheckMonitor => "베이비 모니터 확인",
            V2ActionId.CatchBreath => "숨 고르고 신호 기다리기",
            _ => action.ToString()
        };

        public static string CaregiverStyleName(CaregiverStyle style) => style switch
        {
            CaregiverStyle.Responsive => "바로 반응하는 보호자",
            CaregiverStyle.Methodical => "차례로 확인하는 보호자",
            _ => "잠시 관찰하는 보호자"
        };

        public static string CaregiverStyleDescription(CaregiverStyle style) => style switch
        {
            CaregiverStyle.Responsive => "빠르게 다가가는 힘이 있어요. 반응 사이에 아기의 다음 신호도 기다려보세요.",
            CaregiverStyle.Methodical => "원인을 하나씩 찾는 힘이 있어요. 계획보다 아기의 변화를 먼저 봐도 괜찮아요.",
            _ => "작은 변화를 발견하는 힘이 있어요. 강한 신호에는 조금 더 빠르게 반응해도 괜찮아요."
        };

        public static string ObservationSignal(ObservationSignalId signal) => signal switch
        {
            ObservationSignalId.LipSmacking => "입맛을 다시고 있어요",
            ObservationSignalId.MouthOpening => "입을 벌려 무언가를 찾고 있어요",
            ObservationSignalId.HandSucking => "손을 입으로 가져가 빨고 있어요",
            ObservationSignalId.Rooting => "볼이 닿는 쪽으로 고개를 돌려요",
            ObservationSignalId.LeaningToCaregiver => "보호자 쪽으로 몸을 기울여요",
            ObservationSignalId.Squirming => "몸을 꼼지락거리며 불편함을 알려요",
            ObservationSignalId.RapidBreathing => "숨이 빠르고 울음이 커지고 있어요",
            ObservationSignalId.HeadTurning => "고개를 좌우로 돌리며 찾아요",
            ObservationSignalId.HungerCry => "배고픔 울음이 강해졌어요",
            ObservationSignalId.Yawning => "하품하며 졸린 신호를 보내요",
            ObservationSignalId.RubbingEyes => "눈을 비비며 쉬고 싶다고 알려요",
            ObservationSignalId.EyelidFlutter => "눈꺼풀이 가볍게 움직여요",
            ObservationSignalId.IrregularBreathing => "숨의 간격이 아직 고르지 않아요",
            ObservationSignalId.FacialMovement => "잠든 얼굴이 조금씩 움직여요",
            ObservationSignalId.LimbMovement => "팔다리에 작은 움직임이 남아 있어요",
            ObservationSignalId.RegularBreathing => "숨이 천천히 고르게 이어져요",
            ObservationSignalId.CalmFace => "얼굴의 힘이 편안하게 풀렸어요",
            ObservationSignalId.RelaxedLimbs => "팔다리가 묵직하게 이완됐어요",
            _ => "말 대신 몸으로 작은 신호를 보내고 있어요"
        };

        public static string V2StageLabel(V2SleepStage stage) => stage switch
        {
            V2SleepStage.Awake => "깨어 있음",
            V2SleepStage.Drowsy => "졸림",
            V2SleepStage.RemActiveSleep => "활동 수면",
            V2SleepStage.NremDeepSleep => "깊은 수면",
            _ => stage.ToString()
        };

        public static string WakeCauseLabel(WakeCause cause) => cause switch
        {
            WakeCause.Diaper => "기저귀",
            WakeCause.Hunger => "배고픔",
            WakeCause.Temperature => "온도",
            WakeCause.Humidity => "습도",
            WakeCause.MoroReflex => "모로반사",
            WakeCause.PainOrCondition => "컨디션",
            WakeCause.NaturalCycle => "자연 수면 주기",
            _ => "알 수 없음"
        };

        public static string ObservationLabel(ObservationSignalId signal) => signal switch
        {
            ObservationSignalId.LipSmacking => "입맛을 다신다",
            ObservationSignalId.MouthOpening => "입을 벌린다",
            ObservationSignalId.HandSucking => "손을 빤다",
            ObservationSignalId.Rooting => "젖을 찾는 듯 고개를 움직인다",
            ObservationSignalId.LeaningToCaregiver => "보호자 쪽으로 몸을 기댄다",
            ObservationSignalId.Squirming => "몸을 꼼지락거린다",
            ObservationSignalId.RapidBreathing => "호흡이 빨라졌다",
            ObservationSignalId.HeadTurning => "머리를 좌우로 돌린다",
            ObservationSignalId.HungerCry => "배고픔 신호와 함께 운다",
            ObservationSignalId.EyelidFlutter => "눈꺼풀이 떨린다",
            ObservationSignalId.IrregularBreathing => "호흡이 불규칙하다",
            ObservationSignalId.FacialMovement => "얼굴 근육이 움직인다",
            ObservationSignalId.LimbMovement => "팔다리가 움직인다",
            ObservationSignalId.RegularBreathing => "호흡이 규칙적이다",
            ObservationSignalId.CalmFace => "표정이 편안하다",
            ObservationSignalId.RelaxedLimbs => "팔다리에 힘이 빠졌다",
            _ => signal.ToString()
        };

        public static string FeedingStepLabel(FeedingPreparationStep step) => step switch
        {
            FeedingPreparationStep.SanitizeBottle => "젖병 소독",
            FeedingPreparationStep.PrepareWater => "물 준비",
            FeedingPreparationStep.MeasureFormula => "분유 계량",
            FeedingPreparationStep.MixFormula => "분유 혼합",
            FeedingPreparationStep.CoolBottle => "젖병 식히기",
            FeedingPreparationStep.CheckTemperature => "온도 확인",
            _ => step.ToString()
        };

        /// <summary>결과 오버레이로 승격할 의미 이벤트인지 (screen-spec 4.3 결과 표현 모델).</summary>
        public static bool IsOverlayEvent(GameEventId id) => id switch
        {
            GameEventId.LaydownSucceeded => true,
            GameEventId.LaydownFailed => true,
            GameEventId.BabyFullyWoke => true,
            GameEventId.HungerCueAppeared => true,
            GameEventId.BottleFoundUnsanitized => true,
            GameEventId.ParentExhausted => true,
            GameEventId.NightCompleted => true,
            _ => false
        };

        public static string OverlayTitle(GameEventId id) => id switch
        {
            GameEventId.LaydownSucceeded => "눕히기 성공",
            GameEventId.LaydownFailed => "아직 품이 필요한 순간",
            GameEventId.BabyFullyWoke => "아기가 깼다",
            GameEventId.HungerCueAppeared => "배꼽시계",
            GameEventId.BottleFoundUnsanitized => "준비해 둔 젖병이 없다",
            GameEventId.ParentExhausted => "보호자에게도 돌봄이 필요해요",
            GameEventId.NightCompleted => "아침이 밝았다",
            _ => "…"
        };

        public static string OverlayLine(GameEventId id) => id switch
        {
            GameEventId.LaydownSucceeded => "숨을 죽이고… 아기가 침대에서 계속 잔다.",
            GameEventId.LaydownFailed => "등이 침대에 닿는 순간 센서 발동! 아기가 깼다.",
            GameEventId.BabyFullyWoke => "겨우 재웠는데… 다시 처음부터다.",
            GameEventId.HungerCueAppeared => "배꼽시계가 울렸다. 아기가 배고파 깬다.",
            GameEventId.BottleFoundUnsanitized => "오늘은 소독된 젖병을 다 썼다. 이번 수유에만 먼저 소독해야 한다.",
            GameEventId.ParentExhausted => "지금은 행동을 서두르지 않아도 괜찮아요. 물을 마시고 숨을 고르면 다시 돌볼 수 있어요.",
            GameEventId.NightCompleted => "긴 밤이 끝났다. 오늘의 육아일지를 확인하자.",
            _ => string.Empty
        };
    }
}
