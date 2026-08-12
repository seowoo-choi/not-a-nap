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

        public static string NightGradeLabel(NightGrade grade) => grade switch
        {
            NightGrade.S => "네가 가장 길게 자준 밤 (S)",
            NightGrade.A => "긴 잠을 끝까지 지킨 밤 (A)",
            NightGrade.B => "몇 번 흔들렸지만 아침까지 온 밤 (B)",
            NightGrade.C => "겨우겨우 버틴 밤 (C)",
            _ => "아침이 먼저 와버린 밤 (D)"
        };

        public static string NightRoleTitle(NightId id) => id switch
        {
            NightId.FirstNight => "울기 전에, 너는 먼저 몸으로 말한다",
            NightId.SecondNight => "어젯밤의 내 손이 오늘의 규칙이 된다",
            _ => "백일. 그 모든 밤이 돌아온다"
        };

        public static string NightRoleSummary(NightId id) => id switch
        {
            NightId.FirstNight => "입과 손, 그리고 숨. 우는 이유는 늘 그 전에 먼저 보인다.",
            NightId.SecondNight => "익숙한 방법은 더 잘 듣는다. 대신 준비가 늦으면 더 크게 운다.",
            _ => "지난 선택이 두 번 돌아온다. 세 가지 중 둘만 지키면 된다."
        };

        public static RhythmCardViewModel RhythmCard(RhythmFact fact)
        {
            switch (fact.Id)
            {
                case RhythmId.Carrier:
                    return new RhythmCardViewModel { Id = fact.Id,
                        PreviousChoice = "생긴 습관 · 아기띠",
                        Help = "아기띠 안에서는 금방 잠잠해진다.",
                        Burden = "아기띠가 없는 밤엔 맨손으로 안거나 토닥여야 한다." };
                case RhythmId.HeldSleep:
                    return new RhythmCardViewModel { Id = fact.Id,
                        PreviousChoice = "생긴 습관 · 품에서 잠들기",
                        Help = "안아 주면 더 빨리 잠든다.",
                        Burden = "깊이 잠든 걸 확인하지 않고 내려놓으면 그대로 깬다." };
                case RhythmId.Noise:
                    return new RhythmCardViewModel { Id = fact.Id,
                        PreviousChoice = "생긴 습관 · 백색소음",
                        Help = "익숙한 소리가 바깥 소리를 덮어 준다.",
                        Burden = "매일 켜면 익숙해져서, 언젠가는 다른 손이 필요해진다." };
                case RhythmId.SelfSoothe:
                    return new RhythmCardViewModel { Id = fact.Id,
                        PreviousChoice = "생긴 습관 · 스스로 진정하기",
                        Help = "조금 기다리면 혼자 가라앉기도 한다.",
                        Burden = "울음이 커지면 기다리지 말 것. 이유가 따로 있다는 뜻이다." };
                default:
                    return new RhythmCardViewModel { Id = fact.Id,
                        PreviousChoice = "아직 굳어진 습관은 없다",
                        Help = "오늘은 어떤 방법으로 시작해도 된다.",
                        Burden = "오늘 반복한 손이 내일 밤의 규칙이 된다." };
            }
        }

        public static string HomeLocationLabel(HomeLocation location) => location switch
        {
            HomeLocation.Kitchen => "주방",
            HomeLocation.Bathroom => "욕실",
            _ => "아기방"
        };

        public static EchoSourceViewModel EchoSource(TargetedEventId id) => id switch
        {
            TargetedEventId.CarrierBuckle => new EchoSourceViewModel { EventId = id,
                Cause = "이 아이는 아기띠 안에서 잠드는 법을 배웠다.",
                Change = "하필 오늘, 버클이 나갔다.",
                ResponseHint = "맨손으로 안고, 늘 하던 그 박자로 토닥일 것." },
            TargetedEventId.NoiseBattery => new EchoSourceViewModel { EventId = id,
                Cause = "이 아이는 소리가 깔린 밤에 익숙하다.",
                Change = "하필 오늘, 배터리가 다 됐다.",
                ResponseHint = "숨과 팔다리의 힘을 보면서 다른 손으로 달랠 것." },
            _ => new EchoSourceViewModel { EventId = id,
                Cause = "이 아이는 품에서 잠드는 법을 배웠다.",
                Change = "오늘은 잠이 얕아 내려놓기가 더 어렵다.",
                ResponseHint = "숨이 고르고 팔다리가 늘어진 걸 확인한 뒤에 눕힐 것." }
        };

        public static string EndingTitle(EndingId id) => id switch
        {
            EndingId.MorningWon => "아침까지, 같이",
            EndingId.FamilyRoutine => "우리 집의 밤이 생겼다",
            EndingId.UniverseInArms => "품에서 맞은 아침",
            EndingId.GrandmaBest => "혼자가 아니었다",
            EndingId.GearMaster => "손이 둘뿐이라서",
            _ => "그래도 아침은 왔다"
        };

        public static string EndingSubtitle(EndingId id) => id switch
        {
            EndingId.MorningWon => "세 가지 중 둘을 지켰다. 새벽은 길었지만, 우리는 건넜다.",
            EndingId.FamilyRoutine => "너도 나도 버틸 수 있는 순서를 찾았다. 내일 밤에도 이대로 하면 된다.",
            EndingId.UniverseInArms => "끝까지 품을 내어 줬다. 백일의 아침을 팔 안에서 맞았다.",
            EndingId.GrandmaBest => "혼자 버티지 않았다. 이 밤을 지킨 건 여러 개의 손이었다.",
            EndingId.GearMaster => "필요할 때 필요한 걸 꺼냈다. 요령도 사랑의 한 종류였다.",
            _ => "흔들리는 새벽을 버텼다. 잘한 건 하나, 끝까지 곁에 있었다는 것."
        };

        public static string EndingSubtitle(EndingId id, bool isSuccess)
            => isSuccess
                ? EndingSubtitle(id)
                : "아침은 왔지만, 두 가지 조건을 함께 지키지는 못했다.";

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
            => isSuccess ? "밤을 지켰다" : "다시 도전";

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
            NightOutcome.Crib => "제 침대에서 아침을 맞았다.",
            NightOutcome.Arms => "내 팔 위에서 아침이 왔다.",
            NightOutcome.Awake => "끝내 잠들지 못한 채로 창이 밝았다.",
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
            V2ActionId.DisposeDiaper => "싸서 버리기",
            V2ActionId.WashHands => "손 씻기",
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
            V2ActionId.Grandma => "할머니에게 도움 청하기",
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
            CaregiverStyle.Responsive => "신호가 보이면 일단 움직이고 본다.",
            CaregiverStyle.Methodical => "짚이는 이유를 하나씩 지워 나간다.",
            _ => "한 박자 기다리며 다음 신호를 본다."
        };

        public static string ObservationSignal(ObservationSignalId signal) => signal switch
        {
            ObservationSignalId.LipSmacking => "쩝, 쩝. 입맛을 다신다",
            ObservationSignalId.MouthOpening => "입을 벌린 채 뭔가를 찾는다",
            ObservationSignalId.HandSucking => "제 손을 입에 넣고 빨고 있다",
            ObservationSignalId.Rooting => "볼이 닿는 쪽으로 고개를 돌린다",
            ObservationSignalId.LeaningToCaregiver => "내 쪽으로 몸을 기울인다",
            ObservationSignalId.Squirming => "꼼지락거린다. 어딘가 불편하다는 뜻이다",
            ObservationSignalId.RapidBreathing => "숨이 가빠지고 울음이 올라온다",
            ObservationSignalId.HeadTurning => "고개를 좌우로 돌리며 찾는다",
            ObservationSignalId.HungerCry => "배고픔 울음. 이미 한발 늦었다",
            ObservationSignalId.Yawning => "하—암. 졸리다는 신호다",
            ObservationSignalId.RubbingEyes => "눈을 비빈다. 이제 쉬고 싶다는 뜻이다",
            ObservationSignalId.EyelidFlutter => "눈꺼풀이 가볍게 떨린다",
            ObservationSignalId.IrregularBreathing => "숨의 간격이 아직 고르지 않다",
            ObservationSignalId.FacialMovement => "잠든 얼굴이 조금씩 움직인다",
            ObservationSignalId.LimbMovement => "팔다리에 아직 힘이 남아 있다",
            ObservationSignalId.RegularBreathing => "숨이 천천히, 고르게 이어진다",
            ObservationSignalId.CalmFace => "얼굴의 힘이 편안하게 풀렸다",
            ObservationSignalId.RelaxedLimbs => "팔다리가 묵직하게 늘어졌다",
            _ => "말은 못 해도, 몸으로는 계속 말하고 있다"
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
            ObservationSignalId.LeaningToCaregiver => "내 쪽으로 몸을 기댄다",
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

        /// <summary>배고픔 단계. "울면 이미 늦었다"가 읽히게 후기 단계를 명시한다.</summary>
        public static string HungerStageLabel(HungerSignalStage stage) => stage switch
        {
            HungerSignalStage.Early => "초기 신호",
            HungerSignalStage.Active => "배고픔",
            HungerSignalStage.Late => "늦음 · 울음",
            _ => "괜찮음"
        };

        /// <summary>
        /// 울음 세기. 생 숫자만 보여 주면 35가 큰 값인지 작은 값인지 알 수 없다.
        /// 배고픔·피로와 같은 "라벨 · 수치" 형식으로 읽히도록 단계를 붙인다.
        /// 경계값은 판정이 이미 쓰고 있는 35(경고 색)와 45(자극 줄이기 안내)에 맞춘다.
        /// </summary>
        public static string CryStageLabel(double cryIntensity) => cryIntensity switch
        {
            <= 0 => "조용함",
            < 15 => "칭얼거림",
            < 35 => "우는 중",
            < 60 => "크게 움",
            _ => "자지러짐"
        };

        /// <summary>피로 단계. 과각성은 달래기가 오히려 어려워지는 구간이다.</summary>
        public static string FatigueStageLabel(FatigueSignalStage stage) => stage switch
        {
            FatigueSignalStage.Early => "졸린 신호",
            FatigueSignalStage.Active => "많이 피곤",
            FatigueSignalStage.Overtired => "과각성",
            _ => "말똥말똥"
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
            GameEventId.LaydownSucceeded => "손을 뗐는데, 계속 잔다",
            GameEventId.LaydownFailed => "등이 닿자마자 움찔",
            GameEventId.BabyFullyWoke => "완전히 깼다",
            GameEventId.HungerCueAppeared => "배꼽시계",
            GameEventId.BottleFoundUnsanitized => "준비해 둔 젖병이 없다",
            GameEventId.ParentExhausted => "내 다리에 힘이 없다",
            GameEventId.NightCompleted => "창이 밝았다",
            _ => "…"
        };

        public static string OverlayLine(GameEventId id) => id switch
        {
            GameEventId.LaydownSucceeded => "숨이 그대로 이어진다. 이불만 덮고 물러난다.",
            GameEventId.LaydownFailed => "등이 닿자 몸이 움찔하고, 눈이 떠졌다. 다시 안는다.",
            GameEventId.BabyFullyWoke => "숨이 빨라지고 눈이 완전히 떠졌다. 처음부터 다시다.",
            GameEventId.HungerCueAppeared => "입을 찾고 손을 빤다. 배가 고프다는 말이다.",
            GameEventId.BottleFoundUnsanitized => "쓸 수 있는 젖병이 없다. 소독부터 해야 한다.",
            GameEventId.ParentExhausted => "손에 힘이 빠진다. 여기서 무리하면 둘 다 힘들어진다.",
            GameEventId.NightCompleted => "창밖이 밝아온다. 아홉 시간을 건넜다.",
            _ => string.Empty
        };
    }
}
