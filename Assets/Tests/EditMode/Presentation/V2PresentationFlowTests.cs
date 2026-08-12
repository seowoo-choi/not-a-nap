using System.Linq;
using NotANap.Core;
using NotANap.Presentation;
using NUnit.Framework;

namespace NotANap.Presentation.Tests
{
    public sealed class V2PresentationFlowTests
    {
        private static GameFlowController StartV2(int seed = 41)
        {
            var flow = new GameFlowController(new SystemRandomSource(seed));
            flow.StartGame();
            flow.ToggleItem(ItemId.Monitor);
            flow.ToggleItem(ItemId.Noise);
            flow.ToggleItem(ItemId.Pacifier);
            flow.ConfirmV2Setup();
            return flow;
        }

        [Test]
        public void V2Setup_CreatesMinuteBasedNight_WithoutReplacingV1Api()
        {
            var flow = StartV2();

            Assert.AreEqual(ScreenState.Play, flow.Screen);
            Assert.IsNotNull(flow.Session.Night.V2);
            Assert.AreEqual("21:00", flow.BuildV2Play().Clock);
            Assert.AreEqual(540, flow.BuildV2Play().RemainingMinutes);
            Assert.IsFalse(flow.BuildV2Play().Actions.Any(a => a.Action == V2ActionId.SterilizeBottle));
        }

        [Test]
        public void LeavingTheBabyHidesBabyStateUntilTheMonitorIsRead()
        {
            var flow = StartV2();
            Assert.IsTrue(flow.BuildV2Play().BabyStateVisible,
                "아기방에서는 눈으로 보이므로 상태가 열려 있어야 한다.");

            flow.MoveToHomeLocation(HomeLocation.Kitchen);
            var away = flow.BuildV2Play();
            Assert.IsFalse(away.BabyStateVisible,
                "아기 곁을 떠나면 상태 수치가 닫혀야 베이비 모니터가 의미를 갖는다.");
            Assert.IsFalse(away.BabyStateViaMonitor);
            StringAssert.Contains("베이비 모니터", away.BabyStateBlockedReason);

            flow.ActV2(V2ActionId.CheckMonitor);
            var read = flow.BuildV2Play();
            Assert.IsTrue(read.BabyStateVisible, "모니터를 보면 상태가 열려야 한다.");
            Assert.IsTrue(read.BabyStateViaMonitor);
            Assert.IsNull(read.BabyStateBlockedReason);
        }

        [Test]
        public void WithoutTheMonitorLeavingTheBabyIsAOneWayBlackout()
        {
            var flow = new GameFlowController(new SystemRandomSource(41));
            flow.StartGame();
            flow.ToggleItem(ItemId.Carrier);
            flow.ToggleItem(ItemId.Noise);
            flow.ToggleItem(ItemId.Pacifier);
            flow.ConfirmV2Setup();
            flow.MoveToHomeLocation(HomeLocation.Kitchen);

            var away = flow.BuildV2Play();
            Assert.IsFalse(away.BabyStateVisible);
            StringAssert.Contains("아기방으로 돌아가야", away.BabyStateBlockedReason);
        }

        [Test]
        public void V2Snapshot_ExposesConfiguredEnvironmentRangesForObservedMeterValues()
        {
            var vm = StartV2().BuildV2Play();

            Assert.AreEqual(20, vm.RecommendedTemperatureMin);
            Assert.AreEqual(22, vm.RecommendedTemperatureMax);
            Assert.AreEqual(40, vm.RecommendedHumidityMin);
            Assert.AreEqual(60, vm.RecommendedHumidityMax);
        }

        [Test]
        public void SterilizeActionAppearsOnlyForExceptionalUnsanitizedBottle()
        {
            var flow = StartV2();
            flow.Session.Night.V2.Feeding.BottleSanitized = false;
            flow.MoveToHomeLocation(HomeLocation.Kitchen);

            Assert.IsTrue(flow.BuildV2Play().Actions.Any(a =>
                a.Action == V2ActionId.SterilizeBottle && a.Enabled));
        }

        [Test]
        public void UnselectedNoiseItemDoesNotExposeClickableNoiseAction()
        {
            var flow = new GameFlowController(new SystemRandomSource(41));
            flow.StartGame();
            flow.ToggleItem(ItemId.Monitor);
            flow.ToggleItem(ItemId.Carrier);
            flow.ToggleItem(ItemId.Pacifier);
            flow.ConfirmV2Setup();

            var play = flow.BuildV2Play();
            Assert.IsFalse(play.Actions.Any(a => a.Action == V2ActionId.ToggleNoise && a.Enabled));
            // 베이비 모니터는 아기 곁을 떠나 있을 때 쓰는 물건이다.
            // 아기방에서는 잠기고, 방을 비운 동안에만 열린다.
            Assert.IsFalse(play.Actions.Any(a => a.Action == V2ActionId.CheckMonitor && a.Enabled));

            flow.MoveToHomeLocation(HomeLocation.Kitchen);
            var awayFromBaby = flow.BuildV2Play();
            Assert.IsTrue(awayFromBaby.Actions.Any(
                a => a.Action == V2ActionId.CheckMonitor && a.Enabled));
        }

        [Test]
        public void FeedingPreparationStateIsExposedForDiegeticKitchenObjects()
        {
            var flow = StartV2();
            var feeding = flow.Session.Night.V2.Feeding;
            feeding.BottleSanitized = true;
            feeding.WaterReady = true;
            feeding.FormulaMeasured = true;
            feeding.BottleMixed = true;
            feeding.BottleCooled = false;
            feeding.TemperatureChecked = false;

            var vm = flow.BuildV2Play();

            Assert.IsTrue(vm.BottleSanitized);
            Assert.IsTrue(vm.FeedingWaterReady);
            Assert.IsTrue(vm.FormulaMeasured);
            Assert.IsTrue(vm.BottleMixed);
            Assert.IsFalse(vm.BottleCooled);
            Assert.IsFalse(vm.BottleTemperatureChecked);
            Assert.IsFalse(vm.FeedingReady);
        }

        [Test]
        public void PreparedPacifierIsExposedAsANurserySceneProp()
        {
            var flow = StartV2();

            var vm = flow.BuildV2Play();

            Assert.IsTrue(vm.HasPacifier);
            Assert.IsTrue(vm.Actions.Exists(action => action.Action == V2ActionId.Pacifier));
        }

        [Test]
        public void SecondNightShowsSanitationIncidentThenAllowsSterilizing()
        {
            var presenter = new GameSessionPresenter(new SystemRandomSource(4));
            presenter.StartRun();
            presenter.Run.CurrentNightId = NightId.SecondNight;
            presenter.StartV2Night(new[] { ItemId.Monitor, ItemId.Noise, ItemId.Pacifier });

            Assert.IsTrue(presenter.InputLocked);
            Assert.AreEqual("준비해 둔 젖병이 없다", presenter.PendingOverlay.Title);
            presenter.DismissOverlay();
            presenter.MoveToHomeLocation(HomeLocation.Kitchen);
            Assert.IsTrue(presenter.BuildV2Play().Actions.Any(a =>
                a.Action == V2ActionId.SterilizeBottle && a.Enabled));

            presenter.PerformV2Action(V2ActionId.SterilizeBottle);
            Assert.IsTrue(presenter.Night.V2.Feeding.BottleSanitized);
            Assert.IsFalse(presenter.BuildV2Play().Actions.Any(a => a.Action == V2ActionId.SterilizeBottle));
        }

        [Test]
        public void MapMovementUpdatesLocationsAndRoomSpecificButtons()
        {
            var flow = StartV2();
            Assert.AreEqual(HomeLocation.Nursery, flow.BuildV2Play().CaregiverLocation);
            Assert.IsFalse(flow.BuildV2Play().Actions.Single(a =>
                a.Action == V2ActionId.PrepareWater).Enabled);

            var moved = flow.MoveToHomeLocation(HomeLocation.Kitchen);
            var vm = flow.BuildV2Play();

            Assert.IsTrue(moved.Accepted);
            Assert.AreEqual(HomeLocation.Kitchen, vm.CaregiverLocation);
            Assert.AreEqual(HomeLocation.Nursery, vm.BabyLocation);
            Assert.IsTrue(vm.Actions.Single(a => a.Action == V2ActionId.PrepareWater).Enabled);
            Assert.IsFalse(vm.Actions.Single(a => a.Action == V2ActionId.Hold).Enabled);
        }

        [Test]
        public void V2Action_ExposesStructuredObservationAndAdvancesConfiguredMinutes()
        {
            var flow = StartV2();
            flow.Session.Night.Baby.Hunger = 90;

            var result = flow.ActV2(V2ActionId.CheckHungerSignals);

            Assert.IsFalse(result.Ignored);
            Assert.IsTrue(result.Outcome.Accepted);
            Assert.AreEqual(HungerSignalStage.Early, result.Outcome.HungerSignalStage);
            Assert.IsFalse(result.Outcome.HungerSignalsMatchCause);
            CollectionAssert.Contains(result.Outcome.ObservedSignals, ObservationSignalId.LipSmacking);
            CollectionAssert.DoesNotContain(result.Outcome.ObservedSignals, ObservationSignalId.HungerCry);
            Assert.AreEqual(10, flow.BuildV2Play().ElapsedMinutes);
            Assert.AreEqual("21:10", flow.BuildV2Play().Clock);
        }

        [Test]
        public void DecisionTimeout_IsExplicitPresentationInput()
        {
            var flow = StartV2();
            double stamina = flow.Session.Night.Parent.Stamina;

            var result = flow.Session.ApplyDecisionTimeout();

            Assert.AreEqual(V2ActionId.Hesitate, result.Outcome.Action);
            Assert.Less(flow.Session.Night.Parent.Stamina, stamina);
            Assert.Greater(flow.Session.Night.V2.ElapsedMinutes, 0);
        }

        [Test]
        public void V2ActionLabels_MatchMobileStoryboardCopy()
        {
            Assert.AreEqual("목을 받치고 품에 안기", PresentationCopyMapper.V2ActionLabel(V2ActionId.Hold));
            Assert.AreEqual("천천히 토닥이기", PresentationCopyMapper.V2ActionLabel(V2ActionId.Pat));
            Assert.AreEqual("쪽쪽이 건네기", PresentationCopyMapper.V2ActionLabel(V2ActionId.Pacifier));
            Assert.AreEqual("조심히 눕히기", PresentationCopyMapper.V2ActionLabel(V2ActionId.Laydown));
            Assert.AreEqual("기저귀 갈기", PresentationCopyMapper.V2ActionLabel(V2ActionId.ChangeDiaper));
            Assert.AreEqual("싸서 버리기", PresentationCopyMapper.V2ActionLabel(V2ActionId.DisposeDiaper));
            Assert.AreEqual("손 씻기", PresentationCopyMapper.V2ActionLabel(V2ActionId.WashHands));
            Assert.AreEqual("온도·습도", PresentationCopyMapper.V2ActionLabel(V2ActionId.CheckEnvironment));
            Assert.AreEqual("아기 체온 확인", PresentationCopyMapper.V2ActionLabel(V2ActionId.CheckBodyTemperature));
            Assert.AreEqual("분유 준비", PresentationCopyMapper.V2ActionLabel(V2ActionId.PrepareWater));
            Assert.AreEqual("식히고 온도 확인", PresentationCopyMapper.V2ActionLabel(V2ActionId.CoolBottle));
            Assert.AreEqual("잠시 망설임", PresentationCopyMapper.V2ActionLabel(V2ActionId.Hesitate));
            Assert.AreEqual("백색소음기 켜기/끄기", PresentationCopyMapper.V2ActionLabel(V2ActionId.ToggleNoise));
            Assert.AreEqual("베이비 모니터 확인", PresentationCopyMapper.V2ActionLabel(V2ActionId.CheckMonitor));
            Assert.AreEqual("숨 고르고 신호 기다리기", PresentationCopyMapper.V2ActionLabel(V2ActionId.CatchBreath));
            Assert.AreEqual("할머니에게 도움 청하기", PresentationCopyMapper.V2ActionLabel(V2ActionId.Grandma));
        }

        [Test]
        public void GrandmaActionIsExposedBeforeFinalNightAndSetsReachableEndingState()
        {
            var flow = StartV2();
            Assert.IsTrue(flow.BuildV2Play().Actions.Any(a =>
                a.Action == V2ActionId.Grandma && a.Enabled));

            var result = flow.ActV2(V2ActionId.Grandma);

            Assert.IsTrue(result.Outcome.Accepted);
            Assert.IsTrue(flow.Session.Run.GrandmaUsed);
            Assert.IsFalse(flow.BuildV2Play().Actions.Any(a =>
                a.Action == V2ActionId.Grandma && a.Enabled));
        }

        [Test]
        public void GrandmaCardStatesItsGainsSoThePlayerCanJudgeWhetherToSpendIt()
        {
            var flow = StartV2();
            var grandma = flow.BuildV2Play().Actions
                .First(a => a.Action == V2ActionId.Grandma);

            // 런당 한 번뿐인 카드다. 체력이 오히려 회복된다는 사실과 한도를
            // 눌러 보기 전에 읽을 수 없으면 아낄지 쓸지 판단할 수 없다.
            Assert.IsNotNull(grandma.CostLabel);
            StringAssert.Contains("체력 +35", grandma.CostLabel);
            StringAssert.Contains("울음 0", grandma.CostLabel);
            StringAssert.Contains("런당 1회", grandma.CostLabel);
            // 습관이 붙는다는 뒷값까지 적혀야 "공짜 궁"으로 읽히지 않는다.
            StringAssert.Contains("안아재우기 습관", grandma.CostLabel);
        }

        [Test]
        public void CryIntensityReadsAsAStageNotABareNumber()
        {
            Assert.AreEqual("조용함", PresentationCopyMapper.CryStageLabel(0));
            Assert.AreEqual("칭얼거림", PresentationCopyMapper.CryStageLabel(14));
            Assert.AreEqual("우는 중", PresentationCopyMapper.CryStageLabel(15));
            // 35는 판정이 이미 경고 색을 켜는 경계다. 라벨도 같은 지점에서 넘어가야 한다.
            Assert.AreEqual("크게 움", PresentationCopyMapper.CryStageLabel(35));
            Assert.AreEqual("자지러짐", PresentationCopyMapper.CryStageLabel(60));
        }

        [Test]
        public void GaugeRowsCarryTheThresholdsTheJudgementActuallyUses()
        {
            var vm = StartV2().BuildV2Play();
            var config = new GameBalanceConfig();

            // 게이지 눈금은 화면이 지어낸 값이 아니라 판정 경계여야 한다.
            // 어긋나면 "넘으면 늦은 지점"이 막대와 다른 곳에 찍힌다.
            Assert.AreEqual(config.V2.HungerActiveThreshold, vm.HungerActiveThreshold);
            Assert.AreEqual(config.V2.HungerLateThreshold, vm.HungerLateThreshold);
            Assert.AreEqual(config.V2.CryWarningThreshold, vm.CryWarningThreshold);

            // CryStageLabel은 설정을 읽지 않고 35에서 단계를 넘긴다. 경고 눈금을
            // 튜닝하면 막대와 라벨이 다른 말을 하므로 여기서 잡는다.
            Assert.AreEqual(35d, config.V2.CryWarningThreshold,
                "CryWarningThreshold를 바꿨다면 PresentationCopyMapper.CryStageLabel의 경계도 함께 옮겨야 한다.");
        }

        [Test]
        public void FirstNightSetup_ConfiguresCaregiverAndBabyWithoutACompatibilityScore()
        {
            var flow = new GameFlowController(new SystemRandomSource(1));
            flow.StartGame();

            flow.SelectCaregiverStyle(CaregiverStyle.Methodical);
            flow.SelectBabyTemperament(Temperament.Sensitive);
            var vm = flow.BuildV2Setup();

            Assert.AreEqual(CaregiverStyle.Methodical, flow.Session.Run.CaregiverStyle);
            Assert.AreSame(Temperament.Sensitive, flow.Session.Run.Temperament);
            Assert.AreEqual("차례로 확인하는 보호자", vm.CaregiverStyleName);
            StringAssert.Contains("적응할 시간", vm.PairGuidance);
        }

        [Test]
        public void DiaryCentersLearningCaregiverGrowthAndSharedNight()
        {
            var flow = StartV2();
            flow.Session.Night.Stats.NoiseTurns =
                (int)System.Math.Ceiling(GameBalanceConfig.Default().NoiseHabitThreshold);
            TurnResolver.AdvanceMinutes(flow.Session.Run, flow.Session.Night, 540,
                GameBalanceConfig.Default(), new SystemRandomSource(8));
            flow.ActV2(V2ActionId.Hesitate);

            var diary = flow.BuildV2Diary();

            StringAssert.Contains("신호", diary.LearnedSignal);
            // 보호자 서술은 1인칭으로 쓴다. "오늘 밤"이 빠지면 그 밤의 회고가 아니게 된다.
            StringAssert.Contains("오늘 밤", diary.CaregiverGrowth);
            StringAssert.Contains("엄마", diary.MotherInsight);
            StringAssert.Contains("완료", diary.CompanionMessage);
            StringAssert.Contains("최장 수면", diary.ShareCardText);
            StringAssert.DoesNotContain("정답보다", diary.ShareCardText);
            StringAssert.Contains("아기", diary.BabyResponseReflection);
            StringAssert.Contains("백색소음", diary.HabitNotes.Single());
            Assert.IsNotEmpty(diary.HabitEffects.Single());
        }

        [Test]
        public void HundredthNightDiaryAdvancesToCoreResolvedEnding()
        {
            var flow = new GameFlowController(new SystemRandomSource(12));
            flow.StartGame();
            flow.Session.Run.CurrentNightId = NightId.HundredthNight;
            flow.ToggleV2Item(ItemId.Monitor);
            flow.ToggleV2Item(ItemId.Pacifier);
            flow.ConfirmV2Setup();

            TurnResolver.AdvanceMinutes(flow.Session.Run, flow.Session.Night, 540,
                GameBalanceConfig.Default(), new SystemRandomSource(8));
            flow.ActV2(V2ActionId.Hesitate);
            if (flow.PendingOverlay != null) flow.DismissOverlay();

            Assert.AreEqual(ScreenState.Diary, flow.Screen);
            Assert.IsFalse(flow.BuildV2Diary().HasNextNight);
            Assert.IsTrue(flow.AdvanceToEnding());
            Assert.AreEqual(ScreenState.Ending, flow.Screen);
            Assert.AreEqual(EndingId.MorningWon, flow.BuildEnding().Id);
            Assert.IsFalse(flow.BuildEnding().IsSuccess);
            Assert.AreEqual("아침은 왔지만, 두 가지 조건을 함께 지키지는 못했다.",
                flow.BuildEnding().Subtitle);
            Assert.AreEqual(2, flow.BuildEnding().RequiredConditionCount);
            Assert.AreEqual(3, flow.BuildEnding().TotalConditionCount);
            Assert.AreEqual("다시 도전",
                PresentationCopyMapper.EndingStatusLabel(flow.BuildEnding().IsSuccess));
        }

        [Test]
        public void V2SelectableItems_ExcludeLegacyBouncer()
        {
            Assert.IsFalse(V2NightFactory.SelectableItems.Contains(ItemId.Bouncer));
            var flow = new GameFlowController(new SystemRandomSource(1));
            flow.StartGame();
            flow.ToggleV2Item(ItemId.Bouncer);
            Assert.IsFalse(flow.SelectedItems.Contains(ItemId.Bouncer));
            Assert.IsFalse(flow.BuildV2Setup().Cards.Any(card => card.Id == ItemId.Bouncer));

            var presenter = new GameSessionPresenter(new SystemRandomSource(1));
            presenter.StartRun();
            Assert.Throws<System.ArgumentException>(() => presenter.StartV2Night(
                new[] { ItemId.Bouncer, ItemId.Monitor, ItemId.Noise }));
        }

        [Test]
        public void V2Snapshot_ReportsDawnGradeOnlyAfterNightEnds()
        {
            var flow = StartV2();
            Assert.IsNull(flow.BuildV2Play().Grade);

            TurnResolver.AdvanceMinutes(flow.Session.Run, flow.Session.Night, 540,
                GameBalanceConfig.Default(), new SystemRandomSource(8));

            Assert.IsNotNull(flow.BuildV2Play().Grade);
            Assert.AreEqual("06:00", flow.BuildV2Play().Clock);
        }

        [Test]
        public void ExhaustedPresentationEnablesOnlyCatchBreath()
        {
            var flow = StartV2();
            flow.Session.Night.Parent.Stamina = 0;

            var actions = flow.BuildV2Play().Actions;

            Assert.IsTrue(actions.Single(a => a.Action == V2ActionId.CatchBreath).Enabled);
            Assert.IsFalse(actions.Where(a => a.Action != V2ActionId.CatchBreath)
                .Any(a => a.Enabled));
        }

        [Test]
        public void FirstNightDiaryAdvancesSameRunToSecondNightSetup()
        {
            var flow = StartV2();
            TurnResolver.AdvanceMinutes(flow.Session.Run, flow.Session.Night, 540,
                GameBalanceConfig.Default(), new SystemRandomSource(8));
            flow.ActV2(V2ActionId.Hesitate);

            Assert.AreEqual(ScreenState.Diary, flow.Screen);
            Assert.IsTrue(flow.BuildV2Diary().HasNextNight);
            Assert.IsTrue(flow.AdvanceFromV2Diary());
            Assert.AreEqual(ScreenState.Setup, flow.Screen);
            Assert.AreEqual(NightId.SecondNight, flow.Session.Run.CurrentNightId);
            Assert.AreEqual(1, flow.Session.Run.NightResults.Count);
        }

        [Test]
        public void FirstNightOverlayDismissAndDiaryButtonReachPlayableSecondNight()
        {
            var flow = StartV2();
            TurnResolver.AdvanceMinutes(flow.Session.Run, flow.Session.Night, 540,
                GameBalanceConfig.Default(), new SystemRandomSource(8));
            flow.ActV2(V2ActionId.Hesitate);
            if (flow.PendingOverlay != null) flow.DismissOverlay();
            Assert.AreEqual(ScreenState.Diary, flow.Screen);
            Assert.IsTrue(flow.AdvanceFromV2Diary());

            flow.ToggleV2Item(ItemId.Monitor);
            flow.ToggleV2Item(ItemId.Noise);
            flow.ToggleV2Item(ItemId.Pacifier);
            flow.ConfirmV2Setup();

            Assert.AreEqual(ScreenState.Play, flow.Screen);
            Assert.AreEqual(NightId.SecondNight, flow.Session.Night.NightId);
            Assert.IsNotNull(flow.Session.Night.V2);
        }

        [Test]
        public void FeedActionStaysLockedUntilThePreparationIsActuallyFinished()
        {
            var flow = StartV2();
            var feeding = flow.Session.Night.V2.Feeding;
            feeding.BottleSanitized = true;
            feeding.WaterReady = true;
            feeding.FormulaMeasured = true;
            feeding.BottleMixed = true;
            feeding.BottleCooled = false;
            feeding.TemperatureChecked = false;

            var vm = flow.BuildV2Play();
            var feed = vm.Actions.First(a => a.Action == V2ActionId.FeedPreparedBottle);
            // 예전에는 버튼이 열려 있는데 Core가 조용히 거절해, 눌러도 아무 일도
            // 일어나지 않는 행동으로 보였다.
            Assert.IsFalse(feed.Enabled);
            Assert.AreEqual("주방에서 젖병을 식혀야 해요", feed.DisabledReason);
            Assert.AreEqual("주방에서 젖병을 식혀야 해요", vm.FeedingNextStep);

            feeding.BottleCooled = true;
            feeding.TemperatureChecked = true;

            var ready = flow.BuildV2Play();
            Assert.IsTrue(ready.Actions.First(a => a.Action == V2ActionId.FeedPreparedBottle).Enabled);
            Assert.IsNull(ready.FeedingNextStep);
        }

        [Test]
        public void HungerSignalCheckIsNotOfferedTwiceInTheSameEncounter()
        {
            var flow = StartV2();
            V2TimeResolver.TriggerWake(flow.Session.Night, WakeCause.Hunger,
                GameBalanceConfig.Default());

            Assert.IsTrue(flow.BuildV2Play().Actions
                .First(a => a.Action == V2ActionId.CheckHungerSignals).Enabled);

            flow.ActV2(V2ActionId.CheckHungerSignals);

            var vm = flow.BuildV2Play();
            var check = vm.Actions.First(a => a.Action == V2ActionId.CheckHungerSignals);
            // 열어 두면 몸 위의 추천이 이 관찰에 영원히 고정돼 다음 행동이 사라진다.
            Assert.IsTrue(vm.HungerChecked);
            Assert.IsFalse(check.Enabled);
            Assert.AreEqual("이번 각성에는 이미 살펴봤어요", check.DisabledReason);
        }
    }
}
