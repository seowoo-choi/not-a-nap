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
        public void SterilizeActionAppearsOnlyForExceptionalUnsanitizedBottle()
        {
            var flow = StartV2();
            flow.Session.Night.V2.Feeding.BottleSanitized = false;

            Assert.IsTrue(flow.BuildV2Play().Actions.Any(a =>
                a.Action == V2ActionId.SterilizeBottle && a.Enabled));
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
            Assert.IsTrue(presenter.BuildV2Play().Actions.Any(a =>
                a.Action == V2ActionId.SterilizeBottle && a.Enabled));

            presenter.PerformV2Action(V2ActionId.SterilizeBottle);
            Assert.IsTrue(presenter.Night.V2.Feeding.BottleSanitized);
            Assert.IsFalse(presenter.BuildV2Play().Actions.Any(a => a.Action == V2ActionId.SterilizeBottle));
        }

        [Test]
        public void V2Action_ExposesStructuredObservationAndAdvancesConfiguredMinutes()
        {
            var flow = StartV2();
            flow.Session.Night.Baby.Hunger = 90;

            var result = flow.ActV2(V2ActionId.CheckHungerSignals);

            Assert.IsFalse(result.Ignored);
            Assert.IsTrue(result.Outcome.Accepted);
            Assert.AreEqual(HungerSignalStage.Late, result.Outcome.HungerSignalStage);
            CollectionAssert.Contains(result.Outcome.ObservedSignals, ObservationSignalId.HungerCry);
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
            Assert.AreEqual("품에 안기", PresentationCopyMapper.V2ActionLabel(V2ActionId.Hold));
            Assert.AreEqual("천천히 토닥이기", PresentationCopyMapper.V2ActionLabel(V2ActionId.Pat));
            Assert.AreEqual("쪽쪽이 건네기", PresentationCopyMapper.V2ActionLabel(V2ActionId.Pacifier));
            Assert.AreEqual("조심히 눕히기", PresentationCopyMapper.V2ActionLabel(V2ActionId.Laydown));
            Assert.AreEqual("기저귀 갈기", PresentationCopyMapper.V2ActionLabel(V2ActionId.ChangeDiaper));
            Assert.AreEqual("온도·습도", PresentationCopyMapper.V2ActionLabel(V2ActionId.CheckEnvironment));
            Assert.AreEqual("아기 체온 확인", PresentationCopyMapper.V2ActionLabel(V2ActionId.CheckBodyTemperature));
            Assert.AreEqual("분유 준비", PresentationCopyMapper.V2ActionLabel(V2ActionId.PrepareWater));
            Assert.AreEqual("식히고 온도 확인", PresentationCopyMapper.V2ActionLabel(V2ActionId.CoolBottle));
            Assert.AreEqual("잠시 망설임", PresentationCopyMapper.V2ActionLabel(V2ActionId.Hesitate));
            Assert.AreEqual("백색소음기 켜기/끄기", PresentationCopyMapper.V2ActionLabel(V2ActionId.ToggleNoise));
            Assert.AreEqual("베이비 모니터 확인", PresentationCopyMapper.V2ActionLabel(V2ActionId.CheckMonitor));
            Assert.AreEqual("물 한 잔 마시며 숨 고르기", PresentationCopyMapper.V2ActionLabel(V2ActionId.CatchBreath));
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
    }
}
