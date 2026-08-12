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
        /// <summary>경계를 통과한 AI 서술. 화면 문구만 덮어쓰며 판정에는 쓰이지 않는다.</summary>
        private NarrativeResponse _narrative;
        private NightId? _narrativeNight;

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

        public void SetBabyName(string name)
        {
            if (Run == null || Night != null)
                throw new InvalidOperationException("밤이 시작되기 전에만 이름을 설정할 수 있다.");
            Run.BabyName = string.IsNullOrWhiteSpace(name) ? "아기" : name.Trim();
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
                profile ?? DefaultProfileFor(Run.Temperament),
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
                    Role = def.Role,
                    Effects = def.Effects,
                    Cost = def.Cost,
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
            // 배고픔으로 깬 걸 확인했는데 젖병이 준비되지 않은 상태. 수유 버튼은 준비가
            // 끝나야 열리므로, 이 상태에서는 주방으로 가야 한다는 사실을 화면이 직접
            // 말해주지 않으면 각성이 통째로 막다른 길로 보인다.
            bool needsKitchenForFeed = !v2.Diagnosis.CauseResolved &&
                v2.Diagnosis.ActiveCause == WakeCause.Hunger &&
                v2.Diagnosis.CheckedCauses.Contains(WakeCause.Hunger) &&
                !v2.Feeding.IsReadyToFeed;
            var vm = new V2PlayViewModel
            {
                NeedsKitchenForFeed = needsKitchenForFeed,
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
                BabyMood = BabyMoodResolver.Evaluate(Night, _config),
                BabyMoodLabel = BabyMoodResolver.Label(BabyMoodResolver.Evaluate(Night, _config)),
                CryIntensity = v2.CryIntensity,
                Hunger = Night.Baby.Hunger,
                HungerStage = ObservationResolver.GetHungerStage(Night.Baby.Hunger, _config.V2),
                HungerLabel = PresentationCopyMapper.HungerStageLabel(
                    ObservationResolver.GetHungerStage(Night.Baby.Hunger, _config.V2)),
                HungerActiveThreshold = _config.V2.HungerActiveThreshold,
                HungerLateThreshold = _config.V2.HungerLateThreshold,
                CryWarningThreshold = _config.V2.CryWarningThreshold,
                FatigueStage = BabyClockResolver.GetFatigueStage(v2, _config),
                FatigueLabel = PresentationCopyMapper.FatigueStageLabel(
                    BabyClockResolver.GetFatigueStage(v2, _config)),
                AwakeMinutes = BabyClockResolver.AwakeMinutes(v2),
                MinutesSinceFeed = BabyClockResolver.MinutesSinceFeed(v2),
                MinutesSinceDiaperChange = BabyClockResolver.MinutesSinceDiaperChange(v2),
                BabyHeld = Night.Baby.Held,
                IsLimbRelaxed = v2.SleepCycle.IsLimbRelaxed,
                IsBreathingRegular = v2.SleepCycle.IsBreathingRegular,
                DeepSleepObserved = v2.SleepCycle.DeepSleepObserved,
                TemperatureCelsius = v2.Environment.TemperatureCelsius,
                HumidityPercent = v2.Environment.HumidityPercent,
                RecommendedTemperatureMin = _config.V2.RecommendedTemperatureMin,
                RecommendedTemperatureMax = _config.V2.RecommendedTemperatureMax,
                RecommendedHumidityMin = _config.V2.RecommendedHumidityMin,
                RecommendedHumidityMax = _config.V2.RecommendedHumidityMax,
                BabyTemperatureCelsius = v2.Environment.BabyTemperatureCelsius,
                TemperatureChecked = v2.Environment.IsTemperatureChecked,
                HumidityChecked = v2.Environment.IsHumidityChecked,
                BabyTemperatureChecked = v2.Environment.IsBabyTemperatureChecked,
                FeedingReady = v2.Feeding.IsReadyToFeed,
                FeedingNextStep = v2.Feeding.IsReadyToFeed ? null : FeedingNextStepHint(),
                HungerChecked = v2.Diagnosis.CheckedCauses.Contains(WakeCause.Hunger),
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
                BabyStateVisible = IsBabyStateVisible(Night, v2, _config),
                BabyStateViaMonitor = !IsBesideBaby(Night, v2) && IsMonitorReadFresh(Night, v2, _config),
                BabyStateBlockedReason = BabyStateBlockedReason(Night, v2, _config),
                HeadSupported = v2.HeadSupported,
                CaregiverLocation = v2.CaregiverLocation,
                BabyLocation = Night.Baby.Held ? v2.CaregiverLocation : HomeLocation.Nursery,
                BabyAccompaniesCaregiver = Night.Baby.Held,
                BathThermometerRetrieved = v2.BathThermometerRetrieved,
                DiaperCheckedThisEncounter = v2.Diagnosis.CheckedCauses.Contains(WakeCause.Diaper),
                DiaperWetConfirmed = v2.Diagnosis.DiaperWetConfirmed,
                DiaperStoolConfirmed = v2.Diagnosis.DiaperStoolConfirmed,
                DiaperChangedPendingDisposal = v2.Diagnosis.DiaperChangedPendingDisposal,
                DiaperRecommendationVisible = !v2.Diagnosis.CauseResolved &&
                    v2.ElapsedMinutes >= v2.Diagnosis.DiaperRecommendationSuppressedUntilMinute &&
                    !v2.Diagnosis.CheckedCauses.Contains(WakeCause.Diaper),
                HandsNeedWashing = v2.HandsNeedWashing,
                // 관찰 기록(VisibleSignals)은 밤의 기록용으로 남지만, 아기가 잠든 뒤에도
                // 그대로 현재 신호로 보이면 "몸의 긴장이 풀린다 / 입맛을 다시고 있어요"처럼
                // 헤드라인과 정반대인 문장이 한 카드에 같이 뜬다. 자는 동안은 수면 서술로 돌린다.
                // 수유가 필요한 상태에서는 관찰 문구보다 다음에 할 일이 우선이다.
                CurrentSignal = needsKitchenForFeed
                    ? FeedingNextStepHint()
                    : !IsAsleep(v2) && v2.VisibleSignals.Count > 0
                        ? PresentationCopyMapper.ObservationSignal(v2.VisibleSignals[0])
                        : DefaultSignal(v2, Night.Baby.Hunger),
                // 보호자 상태가 아니라 아기 상태를 말한다. 화면에 서는 수치가 아기 기분이므로
                // 곁의 한 줄도 그 수치가 왜 그 값인지 설명하는 문장이어야 한다.
                CaregiverReflection = ReadMoodReason(Night, v2, _config),
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
                    CostLabel = V2ActionCostLabel(action),
                    Enabled = !Night.Over && IsV2ActionAvailable(action),
                    DisabledReason = !Night.Over && IsV2ActionAvailable(action)
                        ? null
                        : V2ActionDisabledReason(action)
                });
            }
            return vm;
        }

        /// <summary>
        /// 수유까지 남은 단계 중 지금 해야 할 하나를 한 줄로 돌려준다. 준비가 세 단계로
        /// 나뉘어 있는데 화면에 "먹일 수 없다"만 뜨면 어디로 가야 하는지 알 수 없다.
        /// </summary>
        private string FeedingNextStepHint()
        {
            var feeding = Night.V2.Feeding;
            if (!feeding.BottleSanitized) return "주방에서 젖병을 소독해야 해요";
            if (!feeding.BottleMixed) return "주방에서 분유를 타야 해요";
            if (!feeding.BottleCooled || !feeding.TemperatureChecked)
                return "주방에서 젖병을 식혀야 해요";
            return "아기 곁에서 먹일 수 있어요";
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
            // 준비가 덜 된 젖병으로도 버튼이 눌리면 Core가 조용히 거절해 "눌러도 아무 일도
            // 안 일어나는" 행동이 된다. 여기서 준비 완료까지 확인해 화면과 판정을 일치시킨다.
            if (action == V2ActionId.FeedPreparedBottle)
                return withBaby && !Night.V2.HandsNeedWashing && Night.V2.Feeding.IsReadyToFeed;
            // 같은 각성에서 배고픔 신호를 다시 살펴도 새로 알 수 있는 게 없다.
            // 열어 두면 추천이 이 행동에 영원히 고정된다.
            if (action == V2ActionId.CheckHungerSignals)
                return withBaby && !Night.V2.Diagnosis.CheckedCauses.Contains(WakeCause.Hunger);
            if (action == V2ActionId.CheckDiaper)
                return withBaby && !Night.V2.Diagnosis.DiaperWetConfirmed &&
                    !Night.V2.Diagnosis.DiaperChangedPendingDisposal &&
                    !Night.V2.Diagnosis.CheckedCauses.Contains(WakeCause.Diaper) &&
                    Night.V2.ElapsedMinutes >= Night.V2.Diagnosis.DiaperRecommendationSuppressedUntilMinute;
            if (action == V2ActionId.ChangeDiaper)
                return withBaby && Night.V2.Diagnosis.DiaperWetConfirmed &&
                    !Night.V2.Diagnosis.DiaperChangedPendingDisposal;
            if (action == V2ActionId.DisposeDiaper)
                return withBaby && Night.V2.Diagnosis.DiaperChangedPendingDisposal;
            if (action == V2ActionId.WashHands)
                return location == HomeLocation.Bathroom && Night.V2.HandsNeedWashing;
            if (action == V2ActionId.CheckEnvironment)
                return location == HomeLocation.Nursery || location == HomeLocation.Bathroom;
            if (action == V2ActionId.AdjustTemperature ||
                action == V2ActionId.AdjustHumidity)
                return location == HomeLocation.Nursery;
            if (action == V2ActionId.CheckBodyTemperature)
                return location == HomeLocation.Bathroom && withBaby &&
                    Night.V2.BathThermometerRetrieved;
            if (action == V2ActionId.ToggleNoise)
                return location == HomeLocation.Nursery && Night.HasItem(ItemId.Noise) && !Night.NoiseDisabled;
            // 베이비 모니터는 아기 곁을 떠나 있을 때 쓰는 물건이다. 아기방에서만 켜지면
            // 눈앞의 아기를 화면으로 보는 셈이라 물건의 존재 이유가 사라진다.
            if (action == V2ActionId.CheckMonitor)
                return location != HomeLocation.Nursery && Night.HasItem(ItemId.Monitor);
            if (action == V2ActionId.CatchBreath)
                return Night.Parent.Stamina <= 0 || Night.V2.CatchBreathUses < 3;
            if (action == V2ActionId.Grandma)
                return location == HomeLocation.Nursery && !Run.IsFinalNight && !Run.GrandmaUsed;
            if (action == V2ActionId.Hesitate) return true;
            if (!withBaby) return false;
            if (action == V2ActionId.Pacifier)
                return Night.HasItem(ItemId.Pacifier) && Night.PacifierLeft > 0;
            if (action == V2ActionId.ToggleCarrier)
                return Night.HasItem(ItemId.Carrier) &&
                    !(Night.CarrierDisabledTurns > 0 && !Night.Wearing.Carrier);
            // 얕은 잠(REM)에 눕히면 성공률이 한 자리수까지 떨어진다. 그 확률을
            // 선택지로 내놓으면 플레이어는 "무조건 깬다"고 읽는다. 깊은 잠에서만 연다.
            if (action == V2ActionId.Laydown)
                return location == HomeLocation.Nursery && Night.Baby.Held &&
                    Night.V2.SleepCycle.Stage == V2SleepStage.NremDeepSleep;
            // 욕실까지 이동해 탕온계를 챙긴 플레이어가 울음 수치 때문에 아무것도 못 하는
            // 상황을 만들지 않는다. 위치·아기 동행·탕온계 조건은 위에서 이미 검증된다.
            if (action == V2ActionId.CheckBodyTemperature) return true;
            if (action == V2ActionId.Hold) return !Night.Wearing.Carrier;
            return true;
        }

        /// <summary>
        /// 지금 고를 수 없는 이유를 한 줄로 돌려준다. 목록에서 항목을 통째로 지우면
        /// "분유 먹이기가 없어졌다"처럼 읽히고, 어디로 가야 하는지도 알 수 없다.
        /// </summary>
        private string V2ActionDisabledReason(V2ActionId action)
        {
            if (Night.Over) return "밤이 끝났어요";
            if (Night.Parent.Stamina <= 0) return "먼저 숨을 고르세요";
            var location = Night.V2.CaregiverLocation;
            bool withBaby = Night.Baby.Held || location == HomeLocation.Nursery;
            switch (action)
            {
                case V2ActionId.SterilizeBottle:
                case V2ActionId.PrepareWater:
                case V2ActionId.CoolBottle:
                    return "주방에서 할 수 있어요";
                case V2ActionId.FeedPreparedBottle:
                    return Night.V2.HandsNeedWashing
                        ? "손을 씻고 나서 먹일 수 있어요"
                        : !Night.V2.Feeding.IsReadyToFeed
                            ? FeedingNextStepHint()
                            : "아기 곁에서 먹일 수 있어요";
                case V2ActionId.CheckHungerSignals:
                    return Night.V2.Diagnosis.CheckedCauses.Contains(WakeCause.Hunger)
                        ? "이번 각성에는 이미 살펴봤어요"
                        : "아기 곁에서 살펴볼 수 있어요";
                case V2ActionId.WashHands:
                    return Night.V2.HandsNeedWashing
                        ? "욕실에서 씻을 수 있어요"
                        : "지금은 손을 씻지 않아도 돼요";
                case V2ActionId.CheckDiaper:
                    return Night.V2.Diagnosis.DiaperWetConfirmed ||
                           Night.V2.Diagnosis.DiaperChangedPendingDisposal
                        ? "이미 확인했어요 · 갈아주면 돼요"
                        : Night.V2.Diagnosis.CheckedCauses.Contains(WakeCause.Diaper)
                            ? "이번 각성에는 이미 확인했어요"
                            : withBaby ? "조금 전에 확인해 깨끗했어요" : "아기 곁에서 확인할 수 있어요";
                case V2ActionId.ChangeDiaper:
                    return "기저귀를 먼저 확인해야 해요";
                case V2ActionId.DisposeDiaper:
                    return "갈아 준 기저귀가 있어야 해요";
                case V2ActionId.CheckBodyTemperature:
                    return Night.V2.BathThermometerRetrieved
                        ? "욕실에서 아기와 함께 잴 수 있어요"
                        : "욕실에서 탕온계를 먼저 챙기세요";
                case V2ActionId.AdjustTemperature:
                case V2ActionId.AdjustHumidity:
                case V2ActionId.Grandma:
                    return Run.GrandmaUsed && action == V2ActionId.Grandma
                        ? "이번 밤에는 이미 도움을 받았어요"
                        : Run.IsFinalNight && action == V2ActionId.Grandma
                            ? "백일째 밤은 혼자 건너야 해요"
                            : "아기방에서 할 수 있어요";
                case V2ActionId.CheckEnvironment:
                    return "아기방이나 욕실에서 살필 수 있어요";
                case V2ActionId.ToggleNoise:
                    return !Night.HasItem(ItemId.Noise) ? "오늘 밤 챙기지 않은 물건이에요"
                        : Night.NoiseDisabled ? "오늘은 더 쓸 수 없어요"
                        : "아기방에서 켤 수 있어요";
                case V2ActionId.CheckMonitor:
                    return !Night.HasItem(ItemId.Monitor)
                        ? "오늘 밤 챙기지 않은 물건이에요"
                        : "아기 곁을 떠나 있을 때 쓰는 물건이에요";
                case V2ActionId.Pacifier:
                    return !Night.HasItem(ItemId.Pacifier) ? "오늘 밤 챙기지 않은 물건이에요"
                        : Night.PacifierLeft <= 0 ? "오늘은 더 물릴 수 없어요"
                        : "아기 곁에서 물릴 수 있어요";
                case V2ActionId.ToggleCarrier:
                    return !Night.HasItem(ItemId.Carrier) ? "오늘 밤 챙기지 않은 물건이에요"
                        : Night.CarrierDisabledTurns > 0 ? "지금은 다시 맬 수 없어요"
                        : "아기 곁에서 맬 수 있어요";
                case V2ActionId.Laydown:
                    return !withBaby || !Night.Baby.Held ? "아기를 안고 있어야 눕힐 수 있어요"
                        : location != HomeLocation.Nursery ? "아기방 침대에 눕힐 수 있어요"
                        : "깊이 잠든 뒤에 눕히면 성공해요";
                case V2ActionId.Hold:
                    return Night.Wearing.Carrier ? "아기띠를 벗고 안아주세요" : "아기 곁에서 안을 수 있어요";
                case V2ActionId.CatchBreath:
                    return "이번 밤에 숨 고르기를 다 썼어요";
                default:
                    return withBaby ? "지금은 고를 수 없어요" : "아기 곁에서 할 수 있어요";
            }
        }

        private string V2ActionCostLabel(V2ActionId action)
        {
            if (action == V2ActionId.CheckDiaper)
                return $"{_config.V2.DiaperCheckMinutes}분 · 체력 -{_config.V2.DiaperCheckStaminaCost:0}";
            if (action == V2ActionId.ChangeDiaper)
                return Night.V2.Diagnosis.DiaperStoolConfirmed
                    ? $"{_config.V2.DiaperStoolChangeMinutes}분 · 체력 -{_config.V2.DiaperStoolChangeStaminaCost:0}"
                    : $"{_config.V2.DiaperChangeMinutes}분 · 체력 -{_config.V2.DiaperChangeStaminaCost:0}";
            if (action == V2ActionId.DisposeDiaper)
                return Night.V2.Diagnosis.DiaperStoolConfirmed
                    ? $"{_config.V2.DiaperStoolDisposeMinutes}분 · 체력 -{_config.V2.DiaperStoolDisposeStaminaCost:0}"
                    : $"{_config.V2.DiaperDisposeMinutes}분 · 체력 -{_config.V2.DiaperDisposeStaminaCost:0}";
            if (action == V2ActionId.WashHands)
                return $"{_config.V2.WashHandsMinutes}분 · 체력 -{_config.V2.WashHandsStaminaCost:0}";
            // 할머니 찬스는 런당 한 번뿐인 큰 카드다. 무엇이 얼마나 바뀌는지 모르면
            // 아낄지 쓸지 판단할 수 없으므로 아기 상태 변화를 그대로 적는다.
            // 얻는 것을 먼저 적는다. 시간부터 나오면 "쓰면 손해"로 읽혀 카드의 성격이 뒤집힌다.
            // 대신 값은 반드시 함께 적는다. 이 카드는 MemoryConsolidator에서 HeldDep을 올려
            // 백일밤에 안아재우기 습관으로 돌아오는데, 그 사실을 감추면 공짜 궁으로 보인다.
            if (action == V2ActionId.Grandma)
                return "체력 +35 · 울음 0 · 진정 95 · 바로 잠듦 · 깬 원인 해결 · " +
                       $"{_config.V2.DefaultActionMinutes}분 · 런당 1회 · 안아재우기 습관";
            if (action == V2ActionId.Pacifier)
                return $"{_config.V2.DefaultActionMinutes}분 · 체력 -1 · 진정 +" +
                       $"{(Night.V2.Profile.PacifierAffinity == PacifierAffinity.Loves ? _config.V2.PacifierLovesCalmGain : _config.V2.PacifierNeutralCalmGain):0}" +
                       $" · 남은 {Night.PacifierLeft}회";
            if (action == V2ActionId.CheckMonitor)
                return "2분 · 체력 -1 · 아기방 밖에서만";
            if (action == V2ActionId.ToggleNoise)
                return "0분 · 진정 효과 없음 · 각성 간격 +" +
                       $"{NoiseMachine.PotentialWakeDelayBonusMinutes(Night, _config)}분 · 외부 소음 차단";
            return null;
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
            var facts = ReflectionResolver.BuildNarrativeFacts(Run, Night);
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
                    ? $"오늘 처음 알아챈 신호 · {PresentationCopyMapper.ObservationSignal(facts.FirstNoticedSignal.Value)}"
                    : "오늘의 신호 · 울기 전에 몸이 먼저 말한다.",
                NextNightNote = m.MisdiagnosisCount > 0
                    ? "다음 밤 목표 · 달래기 전에 기저귀·배고픔·방부터 짚어 보기"
                    : "다음 밤 목표 · 첫 신호를 놓치지 않기",
                Encouragement = m.ParentStaminaAtDawn >= 30
                    ? "너도 자고 나도 남았다. 이 방식은 내일 밤에도 통한다."
                    : "아침까지 오긴 왔다. 다음엔 나부터 숨 고르는 걸 잊지 말자.",
                CaregiverGrowth = BuildCaregiverGrowth(Run.CaregiverStyle, Night),
                MotherInsight = BuildFamilyUnderstanding(facts),
                FamilyUnderstanding = BuildFamilyUnderstanding(facts),
                HabitReflection = BuildHabitReflection(facts),
                ActionLearning = BuildActionLearning(facts),
                CaregiverFactReflection = BuildCaregiverFactReflection(facts),
                BabyResponseReflection = BuildBabyResponseReflection(facts),
                CompanionMessage = CompanionMessageFor(Night.NightId),
                ShareCardText = $"오늘 알아차린 신호 · {PrimaryLearnedSignal(Night.V2)}\n" +
                    $"최장 수면 {m.LongestSleepStretchMinutes}분 · 깨어남 {m.WakeCount}회"
            };
            foreach (var note in _v2MemoryNotes)
            {
                viewModel.HabitNotes.Add(note.Text);
                viewModel.HabitEffects.Add(note.Sub);
            }
            ApplyNarrativeOverrides(viewModel);
            return viewModel;
        }

        // ── AI 서술 (판정 불개입) ───────────────────────────────

        /// <summary>이번 밤의 서술 요청 본문. 허용된 사실 ID와 수치만 담긴다.</summary>
        public string BuildNarrativePayload()
        {
            if (Night?.V2 == null || !Night.Over) return null;
            var evaluation = NightEvaluationResolver.Evaluate(Night, _config);
            return NarrativeRequest.BuildPayload(ReflectionResolver.BuildNarrativeFacts(Run, Night), evaluation.Grade);
        }

        /// <summary>
        /// 프록시 응답을 화면 문구로만 받아들인다. 경계를 통과하지 못하면 폴백 서술을 유지한다.
        /// Run/Night 상태와 판정은 이 경로에서 절대 변경되지 않는다.
        /// </summary>
        public bool ApplyNarrative(NightId night, NarrativeResponse response)
        {
            if (response == null) return false;
            var validated = NarrativeBoundary.ValidateStructured(response);
            if (!validated.IsValid) return false;
            _narrative = validated;
            _narrativeNight = night;
            return true;
        }

        /// <summary>현재 밤의 일지가 AI 서술로 표시되는지 (화면 표기·테스트용).</summary>
        public bool HasNarrativeFor(NightId night) => _narrative != null && _narrativeNight == night;

        private void ApplyNarrativeOverrides(V2DiaryViewModel viewModel)
        {
            if (!HasNarrativeFor(viewModel.NightId)) return;
            viewModel.BabyResponseReflection = _narrative.NoticedSignal;
            viewModel.CaregiverGrowth = _narrative.CaregiverGrowth;
            viewModel.ActionLearning = _narrative.HabitReflection;
            viewModel.FamilyUnderstanding = _narrative.FamilyUnderstanding;
            viewModel.ShareCardText = _narrative.ShareCard;
            viewModel.NarrativeFromAi = true;
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
                Subtitle = PresentationCopyMapper.EndingSubtitle(ending.Id, ending.IsSuccess),
                Symbol = PresentationCopyMapper.EndingSymbol(ending.Id),
                MetConditionCount = victory.Count,
                RequiredConditionCount = victory.RequiredCount,
                TotalConditionCount = Enum.GetValues(typeof(VictoryCondition)).Length
            };
            foreach (var condition in ending.MetConditions)
                viewModel.MetConditions.Add(PresentationCopyMapper.VictoryConditionLabel(condition));
            foreach (VictoryCondition condition in Enum.GetValues(typeof(VictoryCondition)))
                if (!ending.MetConditions.Contains(condition))
                    viewModel.UnmetConditions.Add(PresentationCopyMapper.VictoryConditionLabel(condition));
            viewModel.RetrySuggestion =
                $"다음 도전 · {PresentationCopyMapper.CaregiverStyleName(NextStyle(Run.CaregiverStyle))} / " +
                $"{NextTemperament(Run.Temperament).Name} 아기";
            return viewModel;
        }

        private static bool WasTargetedEventFired(NightState night, TargetedEventId id)
            => night.FiredEventIds.Contains(id == TargetedEventId.CarrierBuckle
                ? "final-carrier-buckle" : id == TargetedEventId.NoiseBattery
                    ? "final-noise-battery" : "final-dawn-waking");

        private static string BuildHabitReflection(NarrativeFacts facts)
        {
            if (facts.Rhythms.Count == 0 || facts.Rhythms[0].Id == RhythmId.Neutral)
                return "아직 굳어진 습관은 없다. 오늘 반복한 방식이 내일 밤의 규칙이 된다.";
            var card = PresentationCopyMapper.RhythmCard(facts.Rhythms[0]);
            return $"{card.Help} {card.Burden}";
        }

        private static string BuildFamilyUnderstanding(NarrativeFacts facts)
        {
            if (facts.FeedingPreparationIncident)
                return "소독된 젖병이 하나도 없었다. 엄마가 매일 밤 뭘 해뒀던 건지 그제야 알았다.";
            if (facts.LongestPreparationStep.HasValue)
                return $"{PresentationCopyMapper.FeedingStepLabel(facts.LongestPreparationStep.Value)}. 엄마가 늘 미리 해두던 일이었다.";
            return "아기를 기다리며 엄마가 채워 온 밤의 시간이 그제야 눈에 들어왔다.";
        }

        private static string BuildActionLearning(NarrativeFacts facts)
        {
            if (facts.RejectedAction.HasValue && facts.FollowupAction.HasValue)
                return $"{PresentationCopyMapper.V2ActionLabel(facts.RejectedAction.Value)} 대신 " +
                    $"{PresentationCopyMapper.V2ActionLabel(facts.FollowupAction.Value)}. 그게 오늘의 답이었다.";
            if (facts.MostRepeatedAction.HasValue)
                return $"오늘 가장 많이 한 것 · {PresentationCopyMapper.V2ActionLabel(facts.MostRepeatedAction.Value)}";
            if (facts.SleepIntervalChoice == SleepIntervalChoice.PrepareNextFeed)
                return "잠든 사이 다음 수유를 미리 준비해 뒀다. 깨어났을 때 허둥대지 않았다.";
            return "신호를 보고 방법을 바꿨다. 울음이 커지기 전에 움직였다.";
        }

        private static string BuildCaregiverFactReflection(NarrativeFacts facts)
        {
            if (facts.UsedCatchBreath)
                return $"{facts.WakeCount}번을 깨고도 사이사이 숨을 골랐다. 남은 체력 {facts.ParentStamina:0}.";
            if (facts.BareHandsLaydownAttempts > 0)
                return facts.BareHandsLaydownSucceeded
                    ? "도구 하나 없이, 품에서 침대까지 끝내 옮겼다."
                    : "한 번 실패하고 나서는, 더 깊은 숨을 기다릴 줄 알게 됐다.";
            if (facts.LongestMovementDestination.HasValue)
                return $"{PresentationCopyMapper.HomeLocationLabel(facts.LongestMovementDestination.Value)}까지 안고 가서 직접 준비했다.";
            return $"{facts.WakeCount}번 깨어난 밤인데도 체력 {facts.ParentStamina:0}을 남겼다.";
        }

        private static string BuildBabyResponseReflection(NarrativeFacts facts)
        {
            if (facts.RejectedAction.HasValue && facts.FollowupAction.HasValue)
                return $"{PresentationCopyMapper.V2ActionLabel(facts.RejectedAction.Value)}에는 보채고, " +
                    $"{PresentationCopyMapper.V2ActionLabel(facts.FollowupAction.Value)}에는 숨을 골랐다. 그게 이 아기의 대답이었다.";
            if (facts.FirstNoticedSignal.HasValue)
                return $"첫 신호는 ‘{PresentationCopyMapper.ObservationLabel(facts.FirstNoticedSignal.Value)}’. 아기는 그걸로 먼저 말을 걸었다.";
            if (facts.BareHandsLaydownAttempts > 0)
                return facts.BareHandsLaydownSucceeded
                    ? "품에서 침대로 옮겨졌는데도, 아기의 숨은 그대로 이어졌다."
                    : "등이 닿자 움찔했다. 조금만 더 기다려 달라는 아기의 대답이었다.";
            return "울음만 말이 아니었다. 숨과 몸의 힘도 아기가 건네는 대답이었다.";
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
            _narrative = null;
            _narrativeNight = null;
            return true;
        }

        private static string BuildPairGuidance(CaregiverStyle style, Temperament temperament)
        {
            if (temperament == Temperament.Sensitive)
                return style == CaregiverStyle.Responsive
                    ? "빨리 다가가되, 자극은 한 번에 하나씩만 건넬 것."
                    : "소리를 낮추고 자세를 바꾼 뒤에는 적응할 시간을 줄 것.";
            if (temperament == Temperament.Hungry)
                return style == CaregiverStyle.Observant
                    ? "입과 손의 첫 신호만 잡아도 울음까지 가지 않는다."
                    : "크게 울기 전에 나오는 입맛 다시기와 손 빨기를 기억해 둘 것.";
            return "조용한 것도 대답이다. 반응이 작다고 서둘러 방법을 바꾸지 않아도 된다.";
        }

        // ── 아기 상태를 읽을 수 있는가 ──────────────────────────
        // 예전에는 어디에 있든 진정도·울음·배고픔이 늘 보였다. 그러면 베이비 모니터가
        // "아기방 밖에서 아기를 본다"는 자기 역할을 잃고 분위기 문장만 돌려주는
        // 물건이 된다. 아기 곁을 떠나면 수치를 닫고, 모니터만 그 문을 연다.

        private static bool IsBesideBaby(NightState night, V2NightState v2)
            => night.Baby.Held || v2.CaregiverLocation == HomeLocation.Nursery;

        private static bool IsMonitorReadFresh(NightState night, V2NightState v2, GameBalanceConfig config)
            => night.HasItem(ItemId.Monitor) &&
               v2.ElapsedMinutes - v2.MonitorReadAtMinute <= config.V2.MonitorReadFreshMinutes;

        private static bool IsBabyStateVisible(NightState night, V2NightState v2, GameBalanceConfig config)
            => IsBesideBaby(night, v2) || IsMonitorReadFresh(night, v2, config);

        private static string BabyStateBlockedReason(NightState night, V2NightState v2, GameBalanceConfig config)
        {
            if (IsBabyStateVisible(night, v2, config)) return null;
            return night.HasItem(ItemId.Monitor)
                ? "여기서는 아기가 보이지 않는다. 베이비 모니터로 확인하자."
                : "여기서는 아기가 보이지 않는다. 아기방으로 돌아가야 알 수 있다.";
        }

        private static bool IsAsleep(V2NightState v2)
            => v2.SleepCycle.Stage == V2SleepStage.RemActiveSleep ||
               v2.SleepCycle.Stage == V2SleepStage.NremDeepSleep;

        /// <summary>
        /// 아기 기분 수치가 왜 그 값인지 한 줄로 되짚는다. 기분을 가장 크게 깎고 있는
        /// 항목을 먼저 말해 "무엇을 먼저 손보면 되는지"가 수치와 함께 읽히게 한다.
        /// </summary>
        private static string ReadMoodReason(NightState night, V2NightState v2, GameBalanceConfig config)
        {
            if (IsAsleep(v2))
                return v2.SleepCycle.Stage == V2SleepStage.NremDeepSleep
                    ? "깊이 잠들었다. 웬만한 일에는 흔들리지 않는다."
                    : "얕은 잠이다. 작은 소리 하나에도 기분이 출렁인다.";
            double cryDrop = v2.CryIntensity * config.V2.MoodCryWeight;
            double hungerDrop = System.Math.Max(0, night.Baby.Hunger - config.V2.HungerEarlyThreshold)
                                * config.V2.MoodHungerWeight;
            if (cryDrop <= 0 && hungerDrop <= 0)
                return night.Baby.Calm >= config.V2.DrowsyCalmThreshold
                    ? "기분이 좋다. 이대로면 잠든다."
                    : "나쁘진 않은데, 아직 잠들 만큼 가라앉지는 않았다.";
            return cryDrop >= hungerDrop
                ? $"울음이 기분을 {cryDrop:0}만큼 깎고 있다. 달래기 전에 이유부터 찾아야 한다."
                : $"배고픔이 기분을 {hungerDrop:0}만큼 깎고 있다. 분유를 타 와야 한다.";
        }

        private static string DefaultSignal(V2NightState v2, double hunger)
        {
            if (v2.SleepCycle.Stage == V2SleepStage.NremDeepSleep)
                return "숨이 고르고 팔다리가 편안하게 늘어져 있다.";
            if (v2.SleepCycle.Stage == V2SleepStage.RemActiveSleep)
                return "눈꺼풀과 손끝이 아직 움직인다. 깊은 잠은 아니다.";
            if (hunger >= 35) return "입을 오물거리는지, 손을 입으로 가져가는지 보자.";
            return "얼굴만 보면 놓친다. 입·손·숨·몸이 향하는 쪽을 같이 볼 것.";
        }

        private static string PrimaryLearnedSignal(V2NightState v2)
            => v2.VisibleSignals.Count > 0
                ? PresentationCopyMapper.ObservationSignal(v2.VisibleSignals[0])
                : "아기의 숨과 몸에 남은 힘";

        private static string BuildCaregiverGrowth(CaregiverStyle style, NightState night)
        {
            var v2 = night.V2;
            if (night.NightId == NightId.FirstNight)
                return v2.VisibleSignals.Count > 0
                    ? $"오늘 밤 나는 네가 울기 전에 보낸 신호 {v2.VisibleSignals.Count}가지를 먼저 봤다."
                    : "오늘 밤 나는 서두르지 않고 네 다음 움직임을 기다렸다.";
            if (night.NightId == NightId.SecondNight)
                return v2.Feeding.SanitationIncident
                    ? "오늘 밤 나는 허둥대는 대신 수유 순서를 처음부터 다시 세웠다."
                    : $"오늘 밤 {v2.Metrics.WakeCount}번을 깨고도, 나는 매번 다른 방법을 시험했다.";
            return night.FiredEventIds.Count > 0
                ? $"돌아온 습관 {night.FiredEventIds.Count}번을, 오늘 밤 나는 새로운 방법으로 받아 냈다."
                : "백일 동안 밴 습관을, 오늘 밤 우리 가족이 버틸 리듬으로 바꿨다.";
        }

        private static BabyProfile DefaultProfileFor(Temperament temperament)
            => new BabyProfile
            {
                Temperament = temperament,
                PacifierAffinity = temperament == Temperament.Sensitive
                    ? PacifierAffinity.Rejects
                    : temperament == Temperament.Soft
                        ? PacifierAffinity.Loves
                        : PacifierAffinity.Neutral
            };

        private static string CompanionMessageFor(NightId night) => night switch
        {
            NightId.FirstNight => "첫째 밤 완료 · 이제 네가 울기 전에 뭘 하는지 안다.",
            NightId.SecondNight => "둘째 밤 완료 · 내가 반복한 방식이 그대로 습관이 됐다.",
            _ => "백일의 밤 완료 · 우리 집의 밤을 끝까지 지켰다."
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
                NightOutcome.Crib => "오늘 밤은 결국 제 침대에 눕히는 데 성공했다.",
                NightOutcome.Arms => "품에서 나는 숨소리를 들으며 아침을 맞았다.",
                _ => "밤새 뒤척였지만 끝내 깊이 잠들지는 못했다."
            };
            return $"{head} (수유 {s.Feeds}회 · 깸 {s.Wakes}회 · 눕히기 성공 {s.LaydownOk}회, 실패 {s.LaydownFail}회, 남은 체력 {s.StaminaLeft:0})";
        }
    }
}
