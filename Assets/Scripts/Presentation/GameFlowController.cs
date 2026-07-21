using System;
using System.Collections.Generic;
using NotANap.Core;

namespace NotANap.Presentation
{
    /// <summary>
    /// TITLE → SETUP → PLAY → DIARY 화면 흐름만 담당하는 Presentation 전용 상태 머신.
    /// 게임 판정은 전혀 하지 않고 GameSessionPresenter를 통해서만 Core를 부른다.
    /// </summary>
    public sealed class GameFlowController
    {
        public ScreenState Screen { get; private set; } = ScreenState.Title;
        public GameSessionPresenter Session { get; }

        /// <summary>SETUP에서 고른 아이템 (선택 순서 유지).</summary>
        private readonly List<ItemId> _selected = new List<ItemId>();
        public IReadOnlyList<ItemId> SelectedItems => _selected;

        /// <summary>DIARY로 전환된 횟수 (한 번만 전환되어야 함 — 테스트 검증용).</summary>
        public int DiaryTransitionCount { get; private set; }

        /// <summary>TITLE 시작 버튼 중복 입력 방지.</summary>
        private bool _runStarted;

        public event Action<ScreenState> ScreenChanged;

        public GameFlowController(IRandomSource rng)
            => Session = new GameSessionPresenter(rng);

        private void GoTo(ScreenState next)
        {
            if (Screen == next) return;
            Screen = next;
            if (next == ScreenState.Diary) DiaryTransitionCount++;
            ScreenChanged?.Invoke(next);
        }

        // ── TITLE ───────────────────────────────────────────────

        /// <summary>새 런을 정확히 한 번 생성하고 SETUP으로 이동. 중복 클릭은 무시.</summary>
        public void StartGame()
        {
            if (_runStarted) return;
            _runStarted = true;
            Session.StartRun();
            _selected.Clear();
            GoTo(ScreenState.Setup);
        }

        // ── SETUP ───────────────────────────────────────────────

        /// <summary>아이템 선택/해제. 슬롯 초과 선택은 차단한다.</summary>
        public void ToggleItem(ItemId id)
        {
            if (Screen != ScreenState.Setup) return;
            if (_selected.Contains(id)) { _selected.Remove(id); return; }
            if (_selected.Count >= Session.ItemSlots) return; // 슬롯 초과 차단
            _selected.Add(id);
        }

        public SetupViewModel BuildSetup() => Session.BuildSetup(_selected);
        public SetupViewModel BuildV2Setup() => Session.BuildV2Setup(_selected);

        public void ToggleV2Item(ItemId id)
        {
            if (!V2NightFactory.IsSelectableItem(id)) return;
            ToggleItem(id);
        }

        /// <summary>밤을 정확히 한 번 생성하고 PLAY로 이동. 슬롯 미충족이면 무시.</summary>
        public void ConfirmSetup()
        {
            if (Screen != ScreenState.Setup) return;
            // Core(NightFactory)는 슬롯 수와 정확히 일치하는 서로 다른 아이템을 요구한다.
            if (_selected.Count != Session.ItemSlots) return;
            Session.StartNight(_selected);
            GoTo(ScreenState.Play);
        }

        /// <summary>V2 화면에서 호출하는 명시적 진입점. 기존 V1 흐름은 보존한다.</summary>
        public void ConfirmV2Setup(BabyProfile profile = null,
            NightModifierId modifier = NightModifierId.None)
        {
            if (Screen != ScreenState.Setup || _selected.Count != Session.ItemSlots) return;
            Session.StartV2Night(_selected, profile, modifier);
            GoTo(ScreenState.Play);
        }

        // ── PLAY ────────────────────────────────────────────────

        public PlayViewModel BuildPlay() => Session.BuildPlay();

        /// <summary>행동 실행. 오버레이가 없고 밤이 끝났으면 즉시 DIARY로 전환.</summary>
        public ActionResult Act(GameAction action)
        {
            if (Screen != ScreenState.Play) return ActionResult.IgnoredResult();
            var result = Session.PerformAction(action);
            // 오버레이가 없고(대개 밤은 NightCompleted 오버레이로 끝나지만) 밤이 끝났으면 바로 전환.
            if (result.Overlay == null && Session.Night.Over)
                GoTo(ScreenState.Diary);
            return result;
        }

        public V2PresentationActionResult ActV2(V2ActionId action)
        {
            if (Screen != ScreenState.Play) return V2PresentationActionResult.IgnoredResult();
            var result = Session.PerformV2Action(action);
            if (result.Overlay == null && Session.Night.Over) GoTo(ScreenState.Diary);
            return result;
        }

        public V2PlayViewModel BuildV2Play() => Session.BuildV2Play();

        public void FastForwardV2Sleep()
        {
            if (Screen != ScreenState.Play) return;
            Session.FastForwardV2Sleep();
            if (Session.PendingOverlay == null && Session.Night.Over) GoTo(ScreenState.Diary);
        }

        /// <summary>오버레이를 닫는다. 밤이 끝났으면 DIARY로 전환(한 번만).</summary>
        public void DismissOverlay()
        {
            bool nightOver = Session.DismissOverlay();
            if (nightOver && Screen == ScreenState.Play)
                GoTo(ScreenState.Diary);
        }

        public OverlayViewModel PendingOverlay => Session.PendingOverlay;
        public bool InputLocked => Session.InputLocked;

        // ── DIARY ───────────────────────────────────────────────

        public DiaryViewModel BuildDiary() => Session.BuildDiary();
        public V2DiaryViewModel BuildV2Diary() => Session.BuildV2Diary();

        public bool AdvanceFromV2Diary()
        {
            if (Screen != ScreenState.Diary || !Session.AdvanceToNextV2Night()) return false;
            _selected.Clear();
            GoTo(ScreenState.Setup);
            return true;
        }
    }
}
