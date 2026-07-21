using System;
using System.Collections.Generic;
using System.Linq;
using NotANap.Core;

namespace NotANap.Presentation
{
    /// <summary>
    /// RunState / NightState / 주입 RNG를 보관하고 Core를 호출하는 유일한 지점.
    /// Presentation은 여기서만 Core에 접근하며, 수치를 직접 계산하거나 변경하지 않는다.
    /// Run/Night 생성부터 Apply/EndTurn까지 동일한 주입 RNG 흐름을 사용한다.
    /// </summary>
    public sealed class GameSessionPresenter
    {
        private readonly IRandomSource _rng;
        private readonly GameBalanceConfig _config;

        public RunState Run { get; private set; }
        public NightState Night { get; private set; }

        /// <summary>현재 화면에 떠 있어야 하는 오버레이. null이면 없음.</summary>
        public OverlayViewModel PendingOverlay { get; private set; }

        /// <summary>오버레이가 떠 있으면 모든 행동 입력을 차단한다.</summary>
        public bool InputLocked => PendingOverlay != null;

        /// <summary>이미 연출한 이벤트 개수 (새 GameEventId만 한 번 연출).</summary>
        private int _eventCursor;
        /// <summary>PerformAction 재진입 방지 플래그.</summary>
        private bool _busy;
        /// <summary>DIARY 기억 형성 1회 호출 보장.</summary>
        private bool _diaryBuilt;
        private DiaryViewModel _diary;
        private bool _v2DiaryBuilt;

        public GameSessionPresenter(IRandomSource rng, GameBalanceConfig config = null)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _config = config ?? GameBalanceConfig.Default();
        }

        // ── 런/밤 생성 ──────────────────────────────────────────

        /// <summary>새 RunState를 정확히 한 번 생성. 기질은 주입 RNG로 무작위 결정된다.</summary>
        public void StartRun()
        {
            if (Run != null) return;
            Run = RunState.CreateRandom(_rng);
        }

        public int ItemSlots => Run == null ? 0 : NightFactory.ItemSlots(Run.CurrentNightId);

        /// <summary>
        /// 선택 아이템으로 밤을 정확히 한 번 생성.
        /// NightFactory는 슬롯 수와 정확히 일치하는 서로 다른 아이템을 요구한다(Core 계약).
        /// </summary>
        public void StartNight(IReadOnlyList<ItemId> items)
        {
            if (Run == null) throw new InvalidOperationException("StartRun을 먼저 호출해야 한다.");
            Night = NightFactory.CreateNight(Run, items);
            _eventCursor = Night.Events.Count;
            PendingOverlay = null;
            _diaryBuilt = false;
            _diary = null;
            _v2DiaryBuilt = false;
        }

        /// <summary>V1 밤 생성 API를 유지하면서 V2 분 단위 루프를 명시적으로 시작한다.</summary>
        public void StartV2Night(IReadOnlyList<ItemId> items, BabyProfile profile = null,
            NightModifierId modifier = NightModifierId.None,
            IEnumerable<ProductCapability> capabilities = null)
        {
            if (Run == null) throw new InvalidOperationException("StartRun을 먼저 호출해야 한다.");
            if (items.Any(item => !V2NightFactory.IsSelectableItem(item)))
                throw new ArgumentException("LEGACY 아이템은 V2 신규 선택 목록에 포함할 수 없다.", nameof(items));
            Night = NightFactory.CreateV2Night(Run, items,
                profile ?? new BabyProfile { Temperament = Run.Temperament },
                _config, modifier, capabilities);
            _eventCursor = Night.Events.Count;
            PendingOverlay = null;
            _diaryBuilt = false;
            _diary = null;
            _v2DiaryBuilt = false;
        }

        // ── 행동 실행 (★ Apply → EndTurn 순서 고정) ───────────────

        public ActionResult PerformAction(GameAction action)
        {
            // 입력 잠금 / 오버레이 / 밤 종료 / 재진입 시 무시 → 중복 클릭이 Apply를 재호출하지 않는다.
            if (_busy || InputLocked || Night == null || Night.Over)
                return ActionResult.IgnoredResult();

            _busy = true;
            try
            {
                var outcome = ActionResolver.Apply(Run, Night, action, _rng);
                var result = new ActionResult
                {
                    Accepted = outcome.Accepted,
                    ConsumedTurn = outcome.ConsumedTurn,
                    Outcome = outcome,
                    EndTurnInvoked = false
                };

                // 거부: 상태·시간 변경 없음(Core가 이미 보장). 사유 로그만 남고 EndTurn 호출 안 함.
                if (!outcome.Accepted)
                {
                    // 거부도 outcome.Log에 사유가 들어 있으나 오버레이는 띄우지 않는다.
                    return result;
                }

                // 턴 소비 행동만 시간을 흘린다.
                if (outcome.ConsumedTurn)
                {
                    TurnResolver.EndTurn(Run, Night, _rng);
                    result.EndTurnInvoked = true;
                }

                // Apply/EndTurn으로 새로 쌓인 이벤트만 한 번 오버레이로 승격.
                result.Overlay = DrainOverlay();
                PendingOverlay = result.Overlay;
                return result;
            }
            finally
            {
                _busy = false;
            }
        }

        public V2PresentationActionResult PerformV2Action(V2ActionId action)
        {
            if (_busy || InputLocked || Night?.V2 == null || Night.Over)
                return V2PresentationActionResult.IgnoredResult();

            _busy = true;
            try
            {
                var outcome = ActionResolver.ApplyV2(Run, Night, action, _config, _rng);
                var result = new V2PresentationActionResult { Outcome = outcome };
                result.Overlay = DrainOverlay();
                PendingOverlay = result.Overlay;
                return result;
            }
            finally { _busy = false; }
        }

        /// <summary>Presentation의 결정 제한시간 만료 입력. Core 타이머를 실행하지 않는다.</summary>
        public V2PresentationActionResult ApplyDecisionTimeout()
            => PerformV2Action(V2ActionId.Hesitate);

        /// <summary>자는 동안 다음 예약 각성 또는 06:00까지 Core 시간을 빠르게 진행한다.</summary>
        public void FastForwardV2Sleep()
        {
            if (Night?.V2 == null || Night.Over) return;
            var stage = Night.V2.SleepCycle.Stage;
            if (stage != V2SleepStage.RemActiveSleep && stage != V2SleepStage.NremDeepSleep) return;
            int target = Night.V2.NextWake != null && !Night.V2.NextWake.Triggered
                ? Night.V2.NextWake.AtElapsedMinute
                : _config.V2.NightDurationMinutes;
            TurnResolver.AdvanceMinutes(Run, Night,
                Math.Max(0, target - Night.V2.ElapsedMinutes), _config, _rng);
            PendingOverlay = DrainOverlay();
        }

        /// <summary>이벤트 커서 이후 새 이벤트 중 오버레이 후보를 모아 한 개 오버레이로 만든다.</summary>
        private OverlayViewModel DrainOverlay()
        {
            OverlayViewModel overlay = null;
            var events = Night.Events;
            for (int i = _eventCursor; i < events.Count; i++)
            {
                var id = events[i].Id;
                if (!PresentationCopyMapper.IsOverlayEvent(id)) continue;
                if (overlay == null)
                    overlay = new OverlayViewModel { Title = PresentationCopyMapper.OverlayTitle(id) };
                var line = PresentationCopyMapper.OverlayLine(id);
                if (!string.IsNullOrEmpty(line)) overlay.Lines.Add(line);
            }
            _eventCursor = events.Count; // 이미 본 이벤트는 다시 연출하지 않는다.
            return overlay;
        }

        /// <summary>오버레이를 닫아 입력 잠금을 해제한다. 밤이 끝났으면 true.</summary>
        public bool DismissOverlay()
        {
            PendingOverlay = null;
            return Night != null && Night.Over;
        }

        // ── 화면 스냅샷 생성 ────────────────────────────────────

        public SetupViewModel BuildSetup(IReadOnlyCollection<ItemId> selected)
            => BuildSetup(selected, ItemDef.All);

        public SetupViewModel BuildV2Setup(IReadOnlyCollection<ItemId> selected)
            => BuildSetup(selected, ItemDef.All.Where(def => V2NightFactory.IsSelectableItem(def.Id)));

        private SetupViewModel BuildSetup(IReadOnlyCollection<ItemId> selected, IEnumerable<ItemDef> definitions)
        {
            var vm = new SetupViewModel
            {
                NightId = Run.CurrentNightId,
                NightLabel = PresentationCopyMapper.NightLabel(Run.CurrentNightId),
                TemperamentHint = Run.Temperament.Hint,
                Slots = ItemSlots,
                SelectedCount = selected.Count
            };
            bool full = selected.Count >= vm.Slots;
            foreach (var def in definitions)
            {
                bool sel = selected.Contains(def.Id);
                vm.Cards.Add(new ItemCardViewModel
                {
                    Id = def.Id,
                    Emoji = def.Emoji,
                    Name = def.Name,
                    Desc = def.Desc,
                    Side = def.Side,
                    Selected = sel,
                    Disabled = !sel && full,
                    Legacy = def.Id == ItemId.Bouncer
                });
            }
            // Core 계약상 밤은 슬롯 수와 정확히 일치할 때만 생성 가능 → 그때만 시작 허용.
            vm.CanStart = selected.Count == vm.Slots;
            return vm;
        }

        public PlayViewModel BuildPlay()
        {
            var n = Night;
            var b = n.Baby;
            bool monitor = n.HasItem(ItemId.Monitor);
            var vm = new PlayViewModel
            {
                NightId = n.NightId,
                Clock = $"{n.Hour:00}:00",
                TurnsLeft = n.HoursLeft(),
                StageWord = PresentationCopyMapper.StageWord(b.GetStage()),
                Stamina = n.Parent.Stamina,
                HasMonitor = monitor,
                BabyHeld = b.Held,
                BabyCrying = b.Crying,
                NightOver = n.Over
            };
            if (monitor)
            {
                vm.Calm = b.Calm;
                vm.Sleep = b.Sleep;
                vm.Hunger = b.Hunger;
            }

            // 최근 로그 2~3줄.
            int from = Math.Max(0, n.Log.Count - 3);
            for (int i = from; i < n.Log.Count; i++)
            {
                var e = n.Log[i];
                vm.RecentLog.Add(new LogLineViewModel { Hour = e.Hour, Text = e.Text, Cls = e.Cls });
            }

            BuildActions(vm);
            return vm;
        }

        public V2PlayViewModel BuildV2Play()
        {
            if (Night?.V2 == null) throw new InvalidOperationException("V2 밤이 시작되지 않았다.");
            var v2 = Night.V2;
            int total = _config.V2.NightDurationMinutes;
            int clockMinutes = (_config.StartHour * 60 + v2.ElapsedMinutes) % (24 * 60);
            var vm = new V2PlayViewModel
            {
                NightId = Night.NightId,
                Clock = $"{clockMinutes / 60:00}:{clockMinutes % 60:00}",
                ElapsedMinutes = v2.ElapsedMinutes,
                RemainingMinutes = Math.Max(0, total - v2.ElapsedMinutes),
                SleepStage = v2.SleepCycle.Stage,
                RevealedCause = v2.Diagnosis.ActiveCause != WakeCause.Unknown &&
                    v2.Diagnosis.CheckedCauses.Contains(v2.Diagnosis.ActiveCause)
                        ? v2.Diagnosis.ActiveCause : (WakeCause?)null,
                CauseResolved = v2.Diagnosis.CauseResolved,
                DecisionSecondsRemaining = v2.Diagnosis.RemainingDecisionSeconds,
                CurrentSleepStretchMinutes = v2.Metrics.CurrentSleepStretchMinutes,
                LongestSleepStretchMinutes = v2.Metrics.LongestSleepStretchMinutes,
                TotalSleepMinutes = v2.Metrics.TotalSleepMinutes,
                WakeCount = v2.Metrics.WakeCount,
                CorrectFirstChecks = v2.Metrics.CorrectFirstChecks,
                MisdiagnosisCount = v2.Metrics.MisdiagnosisCount,
                Calm = Night.Baby.Calm,
                DrowsyCalmThreshold = _config.V2.DrowsyCalmThreshold,
                SleepStartCalmThreshold = _config.V2.SleepStartCalmThreshold,
                ParentStamina = Night.Parent.Stamina,
                CryIntensity = v2.CryIntensity,
                Hunger = Night.Baby.Hunger,
                IsLimbRelaxed = v2.SleepCycle.IsLimbRelaxed,
                IsBreathingRegular = v2.SleepCycle.IsBreathingRegular,
                DeepSleepObserved = v2.SleepCycle.DeepSleepObserved,
                TemperatureCelsius = v2.Environment.TemperatureCelsius,
                HumidityPercent = v2.Environment.HumidityPercent,
                TemperatureChecked = v2.Environment.IsTemperatureChecked,
                HumidityChecked = v2.Environment.IsHumidityChecked,
                FeedingReady = v2.Feeding.IsReadyToFeed,
                HasNoise = Night.HasItem(ItemId.Noise) && !Night.NoiseDisabled,
                NoiseOn = Night.Wearing.Noise,
                HasMonitor = Night.HasItem(ItemId.Monitor),
                Grade = Night.Over ? NightEvaluationResolver.Evaluate(Night, _config).Grade : null
            };
            foreach (V2ActionId action in Enum.GetValues(typeof(V2ActionId)))
                vm.Actions.Add(new V2ActionButtonViewModel
                {
                    Action = action,
                    Label = PresentationCopyMapper.V2ActionLabel(action),
                    Enabled = !Night.Over && IsV2ActionAvailable(action)
                });
            return vm;
        }

        private bool IsV2ActionAvailable(V2ActionId action)
        {
            if (action == V2ActionId.Pacifier) return Night.HasItem(ItemId.Pacifier);
            if (action == V2ActionId.ToggleNoise) return Night.HasItem(ItemId.Noise) && !Night.NoiseDisabled;
            if (action == V2ActionId.CheckMonitor) return Night.HasItem(ItemId.Monitor);
            return true;
        }

        public V2DiaryViewModel BuildV2Diary()
        {
            if (Night?.V2 == null || !Night.Over)
                throw new InvalidOperationException("종료된 V2 밤이 필요하다.");
            if (!_v2DiaryBuilt)
            {
                MemoryConsolidator.Consolidate(Run, Night, _config);
                _v2DiaryBuilt = true;
            }
            var evaluation = NightEvaluationResolver.Evaluate(Night, _config);
            var m = evaluation.Metrics;
            return new V2DiaryViewModel
            {
                NightId = Night.NightId,
                NightLabel = PresentationCopyMapper.NightLabel(Night.NightId),
                Grade = evaluation.Grade,
                LongestSleepStretchMinutes = m.LongestSleepStretchMinutes,
                TotalSleepMinutes = m.TotalSleepMinutes,
                WakeCount = m.WakeCount,
                CorrectFirstChecks = m.CorrectFirstChecks,
                MisdiagnosisCount = m.MisdiagnosisCount,
                UnsafeChoiceCount = m.UnsafeChoiceCount,
                ParentStaminaAtDawn = m.ParentStaminaAtDawn,
                HasNextNight = Run.CurrentNightId != NightId.HundredthNight
            };
        }

        public bool AdvanceToNextV2Night()
        {
            if (Night?.V2 == null || !Night.Over || Run.CurrentNightId == NightId.HundredthNight)
                return false;
            BuildV2Diary();
            if (!Run.AdvanceNight()) return false;
            Night = null;
            PendingOverlay = null;
            _eventCursor = 0;
            _v2DiaryBuilt = false;
            return true;
        }

        private void BuildActions(PlayViewModel vm)
        {
            var n = Night;
            bool live = !n.Over;

            void Add(GameAction a, bool show, bool enabled, bool toggled = false,
                     string badge = null, bool consumesTime = true)
            {
                if (!show) return;
                vm.Actions.Add(new ActionButtonViewModel
                {
                    Action = a,
                    Label = PresentationCopyMapper.ActionLabel(a),
                    Enabled = live && enabled,
                    Toggled = toggled,
                    BadgeText = badge,
                    ConsumesTime = consumesTime
                });
            }

            // 시간 소비 행동 (항상 노출).
            Add(GameAction.Hold, true, true);
            Add(GameAction.Pat, true, true);
            Add(GameAction.Feed, true, true);
            Add(GameAction.Laydown, true, true);
            Add(GameAction.Watch, true, true);

            // 시간 무소비 준비/토글 — 가진 아이템만 노출.
            Add(GameAction.Pacifier, n.HasItem(ItemId.Pacifier),
                n.PacifierLeft > 0, badge: $"x{n.PacifierLeft}", consumesTime: false);
            Add(GameAction.ToggleCarrier, n.HasItem(ItemId.Carrier),
                !(n.CarrierDisabledTurns > 0 && !n.Wearing.Carrier),
                toggled: n.Wearing.Carrier, consumesTime: false);
            Add(GameAction.ToggleNoise, n.HasItem(ItemId.Noise),
                !(n.NoiseDisabled && !n.Wearing.Noise),
                toggled: n.Wearing.Noise, consumesTime: false);
            Add(GameAction.ToggleBouncer, n.HasItem(ItemId.Bouncer),
                true, toggled: n.Wearing.Bouncer, consumesTime: false);

            // 할머니 찬스: 백일밤 금지, 런당 1회.
            Add(GameAction.Grandma, !Run.IsFinalNight, !Run.GrandmaUsed);
        }

        // ── DIARY (기억 형성 1회) ───────────────────────────────

        /// <summary>밤 종료 후 기억을 정확히 한 번 형성하고 DIARY 스냅샷을 만든다.</summary>
        public DiaryViewModel BuildDiary()
        {
            if (_diaryBuilt) return _diary;
            _diaryBuilt = true;

            var notes = MemoryConsolidator.Consolidate(Run, Night); // ★ 정확히 1회
            var outcome = Night.Result ?? NightOutcome.Awake;

            _diary = new DiaryViewModel
            {
                NightId = Night.NightId,
                NightLabel = PresentationCopyMapper.NightLabel(Night.NightId),
                Outcome = outcome,
                OutcomePhrase = PresentationCopyMapper.OutcomePhrase(outcome),
                DiaryText = BuildFallbackDiary(outcome),
                HasNextNight = Run.CurrentNightId != NightId.HundredthNight
            };
            foreach (var note in notes)
                _diary.Notes.Add(new MemoryNoteViewModel
                {
                    Positive = note.Positive,
                    Text = note.Text,
                    Sub = note.Sub
                });
            return _diary;
        }

        /// <summary>AI 연동 전 규칙 기반 임시 일지 문구 (screen-spec 4.7 폴백).</summary>
        private string BuildFallbackDiary(NightOutcome outcome)
        {
            var s = Night.Stats;
            string head = outcome switch
            {
                NightOutcome.Crib => "오늘 밤은 결국 아기를 침대에 눕히는 데 성공했다.",
                NightOutcome.Arms => "품에 안긴 아기의 숨소리를 들으며 아침을 맞았다.",
                _ => "밤새 뒤척였지만 아기는 끝내 깊이 잠들지 못했다."
            };
            return $"{head} (수유 {s.Feeds}회 · 깸 {s.Wakes}회 · 눕히기 성공 {s.LaydownOk}회, 실패 {s.LaydownFail}회, 남은 체력 {s.StaminaLeft:0})";
        }
    }
}
