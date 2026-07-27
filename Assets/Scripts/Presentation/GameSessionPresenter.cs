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
        private List<MemoryNote> _v2MemoryNotes = new List<MemoryNote>();

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

        public void ConfigureCarePair(CaregiverStyle caregiverStyle, Temperament temperament)
        {
            if (Run == null || Night != null)
                throw new InvalidOperationException("밤이 시작되기 전에만 성향을 설정할 수 있다.");
            Run.ConfigureCarePair(caregiverStyle, temperament);
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
            _v2MemoryNotes.Clear();
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
            _eventCursor = 0;
            PendingOverlay = DrainOverlay();
            _diaryBuilt = false;
            _diary = null;
            _v2DiaryBuilt = false;
            _v2MemoryNotes.Clear();
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

        public HomeMoveOutcome MoveToHomeLocation(HomeLocation destination)
        {
            if (_busy || InputLocked || Night?.V2 == null || Night.Over)
                return new HomeMoveOutcome();
            _busy = true;
            try
            {
                var outcome = HomeMovementResolver.MoveTo(Run, Night, destination, _config, _rng);
                PendingOverlay = DrainOverlay();
                return outcome;
            }
            finally { _busy = false; }
        }

        /// <summary>Presentation의 결정 제한시간 만료 입력. Core 타이머를 실행하지 않는다.</summary>
        public V2PresentationActionResult ApplyDecisionTimeout()
            => PerformV2Action(V2ActionId.Hesitate);

        /// <summary>자는 동안 다음 예약 각성 또는 06:00까지 Core 시간을 빠르게 진행한다.</summary>
        public void FastForwardV2Sleep()
            => ChooseV2SleepInterval(SleepIntervalChoice.RestTogether);

        public bool ChooseV2SleepInterval(SleepIntervalChoice choice)
        {
            if (Night?.V2 == null || Night.Over) return false;
            if (!V2SleepIntervalResolver.Apply(Run, Night, choice, _config, _rng)) return false;
            PendingOverlay = DrainOverlay();
            return true;
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
                SelectedCount = selected.Count,
                IsFirstNight = Run.CurrentNightId == NightId.FirstNight,
                CaregiverStyle = Run.CaregiverStyle,
                CaregiverStyleName = PresentationCopyMapper.CaregiverStyleName(Run.CaregiverStyle),
                CaregiverStyleDescription = PresentationCopyMapper.CaregiverStyleDescription(Run.CaregiverStyle),
                TemperamentName = Run.Temperament.Name,
                PairGuidance = BuildPairGuidance(Run.CaregiverStyle, Run.Temperament)
            };
            vm.NightRoleTitle = PresentationCopyMapper.NightRoleTitle(Run.CurrentNightId);
            vm.NightRoleSummary = PresentationCopyMapper.NightRoleSummary(Run.CurrentNightId);
            int rhythmMaximum = Run.CurrentNightId == NightId.HundredthNight ? 2 : 1;
            if (Run.CurrentNightId != NightId.FirstNight)
                foreach (var fact in ReflectionResolver.GetRhythms(Run, rhythmMaximum))
                    vm.RhythmCards.Add(PresentationCopyMapper.RhythmCard(fact));
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
                CaregiverComposure = v2.CaregiverComposure,
                CryIntensity = v2.CryIntensity,
                Hunger = Night.Baby.Hunger,
                BabyHeld = Night.Baby.Held,
                IsLimbRelaxed = v2.SleepCycle.IsLimbRelaxed,
                IsBreathingRegular = v2.SleepCycle.IsBreathingRegular,
                DeepSleepObserved = v2.SleepCycle.DeepSleepObserved,
                TemperatureCelsius = v2.Environment.TemperatureCelsius,
                HumidityPercent = v2.Environment.HumidityPercent,
                BabyTemperatureCelsius = v2.Environment.BabyTemperatureCelsius,
                TemperatureChecked = v2.Environment.IsTemperatureChecked,
                HumidityChecked = v2.Environment.IsHumidityChecked,
                BabyTemperatureChecked = v2.Environment.IsBabyTemperatureChecked,
                FeedingReady = v2.Feeding.IsReadyToFeed,
                BottleSanitized = v2.Feeding.BottleSanitized,
                FeedingWaterReady = v2.Feeding.WaterReady,
                FormulaMeasured = v2.Feeding.FormulaMeasured,
                BottleMixed = v2.Feeding.BottleMixed,
                BottleCooled = v2.Feeding.BottleCooled,
                BottleTemperatureChecked = v2.Feeding.TemperatureChecked,
                HasCarrier = Night.HasItem(ItemId.Carrier),
                CarrierOn = Night.Wearing.Carrier,
                HasPacifier = Night.HasItem(ItemId.Pacifier),
                HasNoise = Night.HasItem(ItemId.Noise) && !Night.NoiseDisabled,
                NoiseOn = Night.Wearing.Noise,
                HasMonitor = Night.HasItem(ItemId.Monitor),
                HeadSupported = v2.HeadSupported,
                CaregiverLocation = v2.CaregiverLocation,
                BabyLocation = Night.Baby.Held ? v2.CaregiverLocation : HomeLocation.Nursery,
                BabyAccompaniesCaregiver = Night.Baby.Held,
                BathThermometerRetrieved = v2.BathThermometerRetrieved,
                CurrentSignal = v2.VisibleSignals.Count > 0
                    ? PresentationCopyMapper.ObservationSignal(v2.VisibleSignals[0])
                    : DefaultSignal(v2, Night.Baby.Hunger),
                CaregiverReflection = v2.CaregiverComposure >= 65
                    ? "숨을 고르니 아기의 작은 변화가 조금 더 잘 보여요."
                    : "서두르지 않아도 괜찮아요. 먼저 한 번 숨을 고르세요.",
                NightRoleTitle = PresentationCopyMapper.NightRoleTitle(Night.NightId),
                NightRuleChange = PresentationCopyMapper.NightRoleSummary(Night.NightId),
                Grade = Night.Over ? NightEvaluationResolver.Evaluate(Night, _config).Grade : null
            };
            if (Night.NightId == NightId.HundredthNight)
                foreach (var id in Night.ActiveTargetedEvents)
                    if (WasTargetedEventFired(Night, id))
                        vm.EchoSources.Add(PresentationCopyMapper.EchoSource(id));
            foreach (V2ActionId action in Enum.GetValues(typeof(V2ActionId)))
            {
                // 이미 끝난 일상 준비는 화면에서 감춘다. 미소독 돌발 상태에서만 노출된다.
                if (action == V2ActionId.SterilizeBottle && v2.Feeding.BottleSanitized) continue;
                // 실제 플레이는 분유 준비 → 식히고 온도 확인 → 수유의 3단계만 노출한다.
                if (action == V2ActionId.MeasureFormula || action == V2ActionId.MixFormula ||
                    action == V2ActionId.CheckBottleTemperature || action == V2ActionId.HoldWhilePreparing)
                    continue;
                vm.Actions.Add(new V2ActionButtonViewModel
                {
                    Action = action,
                    Label = action == V2ActionId.ToggleCarrier
                        ? (Night.Wearing.Carrier ? "아기띠 벗기" : "아기띠 착용")
                        : PresentationCopyMapper.V2ActionLabel(action),
                    Enabled = !Night.Over && IsV2ActionAvailable(action)
                });
            }
            return vm;
        }

        private bool IsV2ActionAvailable(V2ActionId action)
        {
            if (Night.Parent.Stamina <= 0)
                return action == V2ActionId.CatchBreath;
            var location = Night.V2.CaregiverLocation;
            bool withBaby = Night.Baby.Held || location == HomeLocation.Nursery;
            if (action == V2ActionId.SterilizeBottle || action == V2ActionId.PrepareWater ||
                action == V2ActionId.MeasureFormula || action == V2ActionId.MixFormula ||
                action == V2ActionId.CoolBottle || action == V2ActionId.CheckBottleTemperature)
                return location == HomeLocation.Kitchen;
            if (action == V2ActionId.FeedPreparedBottle) return withBaby;
            if (action == V2ActionId.CheckEnvironment || action == V2ActionId.AdjustTemperature ||
                action == V2ActionId.AdjustHumidity || action == V2ActionId.ToggleNoise)
                return location == HomeLocation.Nursery;
            if (action == V2ActionId.CheckBodyTemperature)
                return withBaby && Night.V2.BathThermometerRetrieved && Night.V2.CryIntensity >= 45;
            if (action == V2ActionId.CheckMonitor) return Night.HasItem(ItemId.Monitor);
            if (action == V2ActionId.CatchBreath || action == V2ActionId.Hesitate) return true;
            if (!withBaby) return false;
            if (action == V2ActionId.Pacifier) return Night.HasItem(ItemId.Pacifier);
            if (action == V2ActionId.ToggleCarrier)
                return Night.HasItem(ItemId.Carrier) &&
                    !(Night.CarrierDisabledTurns > 0 && !Night.Wearing.Carrier);
            if (action == V2ActionId.ToggleNoise) return Night.HasItem(ItemId.Noise) && !Night.NoiseDisabled;
            if (action == V2ActionId.CheckMonitor) return Night.HasItem(ItemId.Monitor);
            if (action == V2ActionId.Laydown)
                return location == HomeLocation.Nursery && Night.Baby.Held &&
                    (Night.V2.SleepCycle.Stage == V2SleepStage.RemActiveSleep ||
                     Night.V2.SleepCycle.Stage == V2SleepStage.NremDeepSleep);
            if (action == V2ActionId.CheckBodyTemperature) return Night.V2.CryIntensity >= 45;
            if (action == V2ActionId.Hold) return !Night.Wearing.Carrier;
            return true;
        }

        public V2DiaryViewModel BuildV2Diary()
        {
            if (Night?.V2 == null || !Night.Over)
                throw new InvalidOperationException("종료된 V2 밤이 필요하다.");
            if (!_v2DiaryBuilt)
            {
                _v2MemoryNotes = MemoryConsolidator.Consolidate(Run, Night, _config);
                _v2DiaryBuilt = true;
            }
            var evaluation = NightEvaluationResolver.Evaluate(Night, _config);
            var m = evaluation.Metrics;
            var facts = ReflectionResolver.BuildDiaryFacts(Run, Night);
            var viewModel = new V2DiaryViewModel
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
                Facts = facts,
                HasNextNight = Run.CurrentNightId != NightId.HundredthNight,
                LearnedSignal = facts.FirstNoticedSignal.HasValue
                    ? $"처음 제대로 알아차린 신호는 ‘{PresentationCopyMapper.ObservationSignal(facts.FirstNoticedSignal.Value)}’였다."
                    : "오늘은 서두르기보다 아기의 작은 신호를 다시 살펴보는 법을 배웠다.",
                NextNightNote = m.MisdiagnosisCount > 0
                    ? "다음 밤에는 바로 달래기 전에 기저귀·배고픔·환경을 차례로 확인하자."
                    : "다음 밤에도 관찰한 신호에 맞는 반응을 하나씩 이어가자.",
                Encouragement = m.ParentStaminaAtDawn >= 30
                    ? "아기와 보호자 모두를 돌보는 지속 가능한 밤에 가까워지고 있다."
                    : "힘든 밤을 버틴 것도 돌봄이다. 다음에는 보호자의 숨 고르기도 먼저 챙기자.",
                CaregiverGrowth = BuildCaregiverGrowth(Run.CaregiverStyle, Night.V2),
                MotherInsight = BuildFamilyUnderstanding(facts),
                FamilyUnderstanding = BuildFamilyUnderstanding(facts),
                HabitReflection = BuildHabitReflection(facts),
                ActionLearning = BuildActionLearning(facts),
                CaregiverFactReflection = BuildCaregiverFactReflection(facts),
                CompanionMessage = CompanionMessageFor(Night.NightId),
                ShareCardText = $"오늘 알아차린 신호 · {PrimaryLearnedSignal(Night.V2)}\n" +
                    "정답보다 서로의 리듬을 알아가는 밤"
            };
            foreach (var note in _v2MemoryNotes)
            {
                viewModel.HabitNotes.Add(note.Text);
                viewModel.HabitEffects.Add(note.Sub);
            }
            return viewModel;
        }

        public EndingViewModel BuildEnding()
        {
            if (Night?.V2 == null || !Night.Over || Night.NightId != NightId.HundredthNight)
                throw new InvalidOperationException("종료된 백일째 밤이 필요하다.");

            // 백일째 밤의 기억까지 먼저 반영한 뒤, Core가 승리와 엔딩을 판정한다.
            BuildV2Diary();
            var victory = VictoryResolver.Evaluate(Night);
            var ending = EndingResolver.Decide(Run, victory);
            var viewModel = new EndingViewModel
            {
                Id = ending.Id,
                IsSuccess = ending.IsSuccess,
                Title = PresentationCopyMapper.EndingTitle(ending.Id),
                Subtitle = PresentationCopyMapper.EndingSubtitle(ending.Id),
                Symbol = PresentationCopyMapper.EndingSymbol(ending.Id),
                MetConditionCount = victory.Count,
                RequiredConditionCount = victory.RequiredCount
            };
            foreach (var condition in ending.MetConditions)
                viewModel.MetConditions.Add(PresentationCopyMapper.VictoryConditionLabel(condition));
            foreach (VictoryCondition condition in Enum.GetValues(typeof(VictoryCondition)))
                if (!ending.MetConditions.Contains(condition))
                    viewModel.UnmetConditions.Add(PresentationCopyMapper.VictoryConditionLabel(condition));
            viewModel.RetrySuggestion =
                $"다음에는 {PresentationCopyMapper.CaregiverStyleName(NextStyle(Run.CaregiverStyle))} 보호자와 " +
                $"{NextTemperament(Run.Temperament).Name} 아기의 밤을 선택해볼 수 있어요.";
            return viewModel;
        }

        private static bool WasTargetedEventFired(NightState night, TargetedEventId id)
            => night.FiredEventIds.Contains(id == TargetedEventId.CarrierBuckle
                ? "final-carrier-buckle" : id == TargetedEventId.NoiseBattery
                    ? "final-noise-battery" : "final-dawn-waking");

        private static string BuildHabitReflection(DiaryFacts facts)
        {
            if (facts.Rhythms.Count == 0 || facts.Rhythms[0].Id == RhythmId.Neutral)
                return "아직 굳어진 리듬은 없다. 오늘 알아차린 신호가 다음 밤의 출발점이 된다.";
            var card = PresentationCopyMapper.RhythmCard(facts.Rhythms[0]);
            return $"{card.Help} {card.Burden}";
        }

        private static string BuildFamilyUnderstanding(DiaryFacts facts)
        {
            if (facts.FeedingPreparationIncident)
                return "평소 준비돼 있던 소독 젖병이 없자, 엄마를 비롯해 함께 밤을 지켜 온 보호자의 준비가 어떤 시간을 아껴줬는지 몸으로 알게 됐다.";
            if (facts.LongestPreparationStep.HasValue)
                return $"{PresentationCopyMapper.FeedingStepLabel(facts.LongestPreparationStep.Value)}를 직접 마치며, 엄마와 다른 보호자의 보이지 않던 준비도 돌봄의 일부였음을 알게 됐다.";
            return "아기의 신호를 기다리고 내 숨도 돌보며, 엄마를 비롯해 함께 밤을 지켜 온 보호자의 시간을 조금 더 구체적으로 이해했다.";
        }

        private static string BuildActionLearning(DiaryFacts facts)
        {
            if (facts.RejectedAction.HasValue && facts.FollowupAction.HasValue)
                return $"{PresentationCopyMapper.V2ActionLabel(facts.RejectedAction.Value)}이 이어지지 않자 " +
                    $"{PresentationCopyMapper.V2ActionLabel(facts.FollowupAction.Value)}으로 바꿔 아기의 답을 다시 살폈다.";
            if (facts.MostRepeatedAction.HasValue)
                return $"가장 자주 건넨 돌봄은 {PresentationCopyMapper.V2ActionLabel(facts.MostRepeatedAction.Value)}였다. " +
                    "반복할수록 같은 행동에도 아기의 작은 반응이 다르게 보였다.";
            if (facts.SleepIntervalChoice == SleepIntervalChoice.PrepareNextFeed)
                return "아기가 잠든 사이 다음 수유를 준비해, 깨어난 뒤의 서두름을 줄였다.";
            return "행동을 서두르기보다 관찰한 신호에 맞춰 한 가지씩 바꿔 보았다.";
        }

        private static string BuildCaregiverFactReflection(DiaryFacts facts)
        {
            if (facts.UsedCatchBreath)
                return $"깨어남이 {facts.WakeCount}번 이어진 밤에도 숨을 고르고 다시 돌아왔다. 새벽의 남은 체력은 {facts.ParentStamina:0}이었다.";
            if (facts.BareHandsLaydownAttempts > 0)
                return facts.BareHandsLaydownSucceeded
                    ? "도구 없이 품에서 침대로 옮기는 시도를 끝내 이어 냈다."
                    : "맨손으로 내려놓아 본 시도는 실패가 아니라 더 깊은 잠 신호를 배우는 계기가 됐다.";
            if (facts.LongestMovementDestination.HasValue)
                return $"{PresentationCopyMapper.HomeLocationLabel(facts.LongestMovementDestination.Value)}으로 직접 움직이며 준비와 돌봄 사이의 시간을 체감했다.";
            return $"아기가 {facts.WakeCount}번 깨어난 뒤에도 보호자의 체력 {facts.ParentStamina:0}을 남기며 밤을 건넜다.";
        }

        private static CaregiverStyle NextStyle(CaregiverStyle style)
            => style == CaregiverStyle.Responsive ? CaregiverStyle.Observant
                : style == CaregiverStyle.Observant ? CaregiverStyle.Methodical : CaregiverStyle.Responsive;

        private static Temperament NextTemperament(Temperament temperament)
            => temperament == Temperament.Soft ? Temperament.Sensitive
                : temperament == Temperament.Sensitive ? Temperament.Hungry : Temperament.Soft;

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
            _v2MemoryNotes.Clear();
            return true;
        }

        private static string BuildPairGuidance(CaregiverStyle style, Temperament temperament)
        {
            if (temperament == Temperament.Sensitive)
                return style == CaregiverStyle.Responsive
                    ? "빠르게 다가가되 한 번에 한 가지 자극만 건네보세요."
                    : "작은 소리와 자세 변화 뒤에 적응할 시간을 주세요.";
            if (temperament == Temperament.Hungry)
                return style == CaregiverStyle.Observant
                    ? "입과 손의 초기 신호를 보면 울음이 커지기 전에 반응할 수 있어요."
                    : "강한 울음 전의 입맛 다시기와 손 빨기를 기억해보세요.";
            return "조용한 반응도 하나의 신호예요. 반응이 작다고 서둘러 행동을 바꾸지 않아도 괜찮아요.";
        }

        private static string DefaultSignal(V2NightState v2, double hunger)
        {
            if (v2.SleepCycle.Stage == V2SleepStage.NremDeepSleep)
                return "숨이 고르고 팔다리의 힘이 편안하게 풀려 있어요.";
            if (v2.SleepCycle.Stage == V2SleepStage.RemActiveSleep)
                return "눈꺼풀과 손끝이 움직여요. 아직 깊은 잠은 아니에요.";
            if (hunger >= 35) return "입을 오물거리거나 손을 입으로 가져가는지 살펴보세요.";
            return "표정만 보지 말고 입·손·호흡·몸의 방향을 함께 살펴보세요.";
        }

        private static string PrimaryLearnedSignal(V2NightState v2)
            => v2.VisibleSignals.Count > 0
                ? PresentationCopyMapper.ObservationSignal(v2.VisibleSignals[0])
                : "아기의 호흡과 몸의 긴장";

        private static string BuildCaregiverGrowth(CaregiverStyle style, V2NightState v2)
            => $"{PresentationCopyMapper.CaregiverStyleName(style)}로 시작한 나는 " +
               (v2.GentleObservationCount > 0
                   ? "행동하기 전에 기다리고 관찰하는 순간을 만들었다."
                   : "다음 밤에는 행동 하나 사이에 아기의 답을 기다려보기로 했다.");

        private static string CompanionMessageFor(NightId night) => night switch
        {
            NightId.FirstNight => "함께 이 밤을 건너는 보호자의 문장 · “완벽하지 않아도, 알아차리려는 마음은 전해져요.”",
            NightId.SecondNight => "함께 이 밤을 건너는 보호자의 문장 · “울음은 실패가 아니라 아직 해석 중인 말이래요.”",
            _ => "당신과 같은 밤을 건너는 보호자들이 있어요. 서로 다른 리듬도 모두 돌봄의 기록입니다."
        };

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
