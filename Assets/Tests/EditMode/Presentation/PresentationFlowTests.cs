using System.Collections.Generic;
using System.Linq;
using NotANap.Core;
using NotANap.Presentation;
using NUnit.Framework;

namespace NotANap.Presentation.Tests
{
    /// <summary>
    /// Presentation 순수 로직 테스트. Core 판정은 재검증하지 않고,
    /// "화면 흐름이 Core를 올바른 순서·횟수로 부르는지"만 검증한다.
    /// </summary>
    public sealed class PresentationFlowTests
    {
        private static GameFlowController NewFlow(int seed = 12345)
            => new GameFlowController(new SystemRandomSource(seed));

        /// <summary>TITLE→SETUP→PLAY 진입 헬퍼. items는 슬롯 수(일반 밤 3)와 정확히 일치해야 한다.</summary>
        private static GameFlowController InPlay(int seed, params ItemId[] items)
        {
            var flow = NewFlow(seed);
            flow.StartGame();
            foreach (var id in items) flow.ToggleItem(id);
            flow.ConfirmSetup();
            Assert.AreEqual(ScreenState.Play, flow.Screen, "PLAY 진입 실패");
            return flow;
        }

        // ── TITLE / SETUP ───────────────────────────────────────

        [Test]
        public void StartGame_CreatesRunOnce_AndMovesToSetup()
        {
            var flow = NewFlow();
            flow.StartGame();
            var run = flow.Session.Run;
            Assert.IsNotNull(run);
            Assert.AreEqual(ScreenState.Setup, flow.Screen);

            flow.StartGame(); // 중복 클릭
            Assert.AreSame(run, flow.Session.Run, "중복 시작이 RunState를 다시 만들면 안 된다.");
        }

        [Test]
        public void Setup_SlotOverflow_IsBlocked()
        {
            var flow = NewFlow();
            flow.StartGame();
            Assert.AreEqual(3, flow.Session.ItemSlots);

            flow.ToggleItem(ItemId.Monitor);
            flow.ToggleItem(ItemId.Noise);
            flow.ToggleItem(ItemId.Pacifier);
            flow.ToggleItem(ItemId.Carrier); // 4번째 — 차단되어야 함
            Assert.AreEqual(3, flow.SelectedItems.Count);
            Assert.IsFalse(flow.SelectedItems.Contains(ItemId.Carrier));
        }

        [Test]
        public void Setup_ConfirmRequiresExactSlots_MatchingCoreContract()
        {
            var flow = NewFlow();
            flow.StartGame();
            flow.ToggleItem(ItemId.Monitor);
            flow.ToggleItem(ItemId.Noise);

            flow.ConfirmSetup(); // 슬롯 미충족(2/3) → 무시
            Assert.AreEqual(ScreenState.Setup, flow.Screen);
            Assert.IsNull(flow.Session.Night);

            flow.ToggleItem(ItemId.Pacifier); // 3/3
            Assert.IsTrue(flow.BuildSetup().CanStart);
            flow.ConfirmSetup();
            Assert.AreEqual(ScreenState.Play, flow.Screen);
            Assert.IsNotNull(flow.Session.Night, "밤이 정확히 한 번 생성되어야 한다.");
        }

        // ── Apply / EndTurn 순서·횟수 ────────────────────────────

        [Test]
        public void RejectedAction_DoesNotInvokeEndTurn()
        {
            var flow = InPlay(1, ItemId.Monitor, ItemId.Noise, ItemId.Pacifier);
            int hour = flow.Session.Night.Hour;
            int turns = flow.Session.Night.ConsumedTurns;

            // 밤 시작 시 아기는 안겨 있지 않음 → 눕히기는 거부된다.
            var r = flow.Act(GameAction.Laydown);
            Assert.IsFalse(r.Ignored);
            Assert.IsFalse(r.Accepted);
            Assert.IsFalse(r.EndTurnInvoked, "거부된 행동은 EndTurn을 부르면 안 된다.");
            Assert.AreEqual(hour, flow.Session.Night.Hour);
            Assert.AreEqual(turns, flow.Session.Night.ConsumedTurns);
        }

        [Test]
        public void NoTimeAction_DoesNotInvokeEndTurn()
        {
            var flow = InPlay(2, ItemId.Monitor, ItemId.Noise, ItemId.Pacifier);
            int turns = flow.Session.Night.ConsumedTurns;

            var r = flow.Act(GameAction.ToggleNoise); // 시간 무소모 토글
            Assert.IsTrue(r.Accepted);
            Assert.IsFalse(r.ConsumedTurn);
            Assert.IsFalse(r.EndTurnInvoked, "ConsumedTurn=false면 EndTurn을 부르면 안 된다.");
            Assert.AreEqual(turns, flow.Session.Night.ConsumedTurns);
        }

        [Test]
        public void TimeAction_InvokesEndTurnExactlyOnce()
        {
            var flow = InPlay(3, ItemId.Monitor, ItemId.Noise, ItemId.Pacifier);
            Assert.AreEqual(0, flow.Session.Night.ConsumedTurns);

            var r = flow.Act(GameAction.Pat); // 시간 소모
            Assert.IsTrue(r.Accepted);
            Assert.IsTrue(r.ConsumedTurn);
            Assert.IsTrue(r.EndTurnInvoked);
            Assert.AreEqual(1, flow.Session.Night.ConsumedTurns, "EndTurn은 정확히 한 번만 돌아야 한다.");
            Assert.AreEqual(22, flow.Session.Night.Hour);
        }

        [Test]
        public void DuplicateClickWhileOverlay_DoesNotReapplyAction()
        {
            var flow = InPlay(7, ItemId.Monitor, ItemId.Noise, ItemId.Pacifier);
            flow.Act(GameAction.Hold);         // 아기를 안는다 (눕히기 전제)
            if (flow.InputLocked) flow.DismissOverlay();

            var r1 = flow.Act(GameAction.Laydown); // 성공/실패 무관하게 오버레이 발생
            Assert.IsNotNull(r1.Overlay);
            Assert.IsTrue(flow.InputLocked, "오버레이 중에는 입력이 잠겨야 한다.");

            int turns = flow.Session.Night.ConsumedTurns;
            int hour = flow.Session.Night.Hour;
            int logs = flow.Session.Night.Log.Count;

            var r2 = flow.Act(GameAction.Pat); // 잠긴 동안 중복 클릭
            Assert.IsTrue(r2.Ignored, "잠긴 동안의 클릭은 무시되어야 한다.");
            Assert.AreEqual(turns, flow.Session.Night.ConsumedTurns, "Apply/EndTurn이 재실행되면 안 된다.");
            Assert.AreEqual(hour, flow.Session.Night.Hour);
            Assert.AreEqual(logs, flow.Session.Night.Log.Count);
        }

        // ── 결정론 ──────────────────────────────────────────────

        [Test]
        public void SameSeedSameSequence_ProducesSameUiSnapshot()
        {
            string a = RunScript(999);
            string b = RunScript(999);
            Assert.AreEqual(a, b, "같은 seed·같은 입력이면 화면 스냅샷도 동일해야 한다.");
        }

        private static string RunScript(int seed)
        {
            var flow = InPlay(seed, ItemId.Monitor, ItemId.Carrier, ItemId.Noise);
            var script = new[] { GameAction.Hold, GameAction.Pat, GameAction.Watch, GameAction.Pat };
            foreach (var act in script)
            {
                if (flow.InputLocked) flow.DismissOverlay();
                if (flow.Screen != ScreenState.Play) break;
                flow.Act(act);
            }
            if (flow.InputLocked) flow.DismissOverlay();
            return flow.Screen == ScreenState.Play
                ? flow.BuildPlay().ToSnapshot()
                : flow.BuildDiary().OutcomePhrase;
        }

        // ── 밤 종료 → DIARY 1회 + 기억 1회 ──────────────────────

        [Test]
        public void NightEnds_TransitionsToDiaryExactlyOnce_AndConsolidatesMemoryOnce()
        {
            var flow = InPlay(5, ItemId.Monitor, ItemId.Noise, ItemId.Pacifier);
            int patCount = DriveToDiary(flow);

            Assert.AreEqual(ScreenState.Diary, flow.Screen);
            Assert.AreEqual(1, flow.DiaryTransitionCount, "DIARY 전환은 정확히 한 번이어야 한다.");
            Assert.IsTrue(flow.Session.Night.Over);
            Assert.AreEqual(9, flow.Session.Night.ConsumedTurns, "밤은 9번의 시간 소모 후 끝난다.");
            Assert.AreEqual(9, patCount);

            var diary = flow.BuildDiary();
            Assert.IsNotNull(diary);
            Assert.AreEqual(1, flow.Session.Run.NightResults.Count, "MemoryConsolidator는 한 번만 호출되어야 한다.");

            flow.BuildDiary(); // 재호출해도 기억을 다시 형성하면 안 됨
            Assert.AreEqual(1, flow.Session.Run.NightResults.Count);
        }

        /// <summary>시간 소모 행동(Pat)만으로 밤을 끝까지 진행. 오버레이는 그때그때 닫는다.</summary>
        private static int DriveToDiary(GameFlowController flow)
        {
            int pats = 0;
            for (int guard = 0; guard < 200 && flow.Screen == ScreenState.Play; guard++)
            {
                if (flow.InputLocked) { flow.DismissOverlay(); continue; }
                if (flow.Session.Night.Over) { break; }
                var r = flow.Act(GameAction.Pat);
                if (r.EndTurnInvoked) pats++;
            }
            return pats;
        }
    }
}
