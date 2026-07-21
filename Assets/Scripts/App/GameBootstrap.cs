using System.Collections.Generic;
using NotANap.Core;
using NotANap.Presentation;
using UnityEngine;

namespace NotANap.App
{
    /// <summary>
    /// TITLE → SETUP → PLAY → DIARY 수직 슬라이스를 실제로 플레이하기 위한 얇은 View.
    /// 게임 판정 로직은 전혀 없다. GameFlowController/GameSessionPresenter를 통해 Core만 호출하고
    /// 반환된 ViewModel을 IMGUI로 그린다. (Core 상태를 직접 계산·변경하지 않는다.)
    ///
    /// 씬을 편집하지 않도록 RuntimeInitializeOnLoadMethod로 자동 생성된다.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private GameFlowController _flow;
        private Vector2 _playScroll;
        private V2PresentationActionResult _lastV2Result;
        private int _timedEncounterSequence = -1;
        private float _decisionDeadline;
        private bool _timeoutSent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<GameBootstrap>() != null) return;
            var go = new GameObject("[NotANap] GameBootstrap");
            DontDestroyOnLoad(go);
            go.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            // Presentation은 별도 RNG를 만들지 않는다. Run/Night/Apply/EndTurn이 이 하나의 주입 RNG를 공유한다.
            IRandomSource rng = new SystemRandomSource(System.Environment.TickCount);
            _flow = new GameFlowController(rng);
        }

        // ── IMGUI 렌더링 ────────────────────────────────────────

        private Vector2 _scale;
        private GUIStyle _title, _h1, _body, _btn, _logSys, _logGood, _logWarn, _logBaby;

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _h1 = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };
            _logSys = new GUIStyle(_body) { normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
            _logGood = new GUIStyle(_body) { normal = { textColor = new Color(0.45f, 0.85f, 0.5f) } };
            _logWarn = new GUIStyle(_body) { normal = { textColor = new Color(0.95f, 0.75f, 0.3f) } };
            _logBaby = new GUIStyle(_body) { normal = { textColor = new Color(0.5f, 0.7f, 1f) } };
        }

        private void OnGUI()
        {
            EnsureStyles();

            // 720x1280 기준 좌표를 실제 화면에 맞춰 스케일.
            const float refW = 720f, refH = 1280f;
            _scale = new Vector2(Screen.width / refW, Screen.height / refH);
            float s = Mathf.Min(_scale.x, _scale.y);
            GUIUtility.ScaleAroundPivot(new Vector2(s, s), Vector2.zero);

            var area = new Rect(20, 20, refW - 40, refH - 40);
            GUILayout.BeginArea(area);
            switch (_flow.Screen)
            {
                case ScreenState.Title: DrawTitle(); break;
                case ScreenState.Setup: DrawSetup(); break;
                case ScreenState.Play: DrawPlay(); break;
                case ScreenState.Diary: DrawDiary(); break;
            }
            GUILayout.EndArea();
        }

        private void DrawTitle()
        {
            GUILayout.Space(200);
            GUILayout.Label("NOT A NAP\n: 백일의 밤", _title);
            GUILayout.Space(20);
            GUILayout.Label("아기가 재우는 방식을 기억한다.\n오늘의 습관이 내일의 규칙이 된다.", _body);
            GUILayout.Space(60);
            if (GUILayout.Button("게임 시작", _btn, GUILayout.Height(64)))
                _flow.StartGame();
        }

        private void DrawSetup()
        {
            var vm = _flow.BuildV2Setup();
            GUILayout.Label($"{vm.NightLabel} · 준비", _h1);
            GUILayout.Space(6);
            GUILayout.Label($"\"{vm.TemperamentHint}\"", _body);
            GUILayout.Space(10);
            GUILayout.Label($"침실에 가져갈 아이템 — {vm.SelectedCount}/{vm.Slots}", _h1);
            GUILayout.Space(6);

            foreach (var card in vm.Cards)
            {
                var prev = GUI.enabled;
                GUI.enabled = !card.Disabled;
                string mark = card.Selected ? "✔ " : "";
                string legacy = card.Legacy ? "  [LEGACY]" : "";
                string label = $"{mark}{card.Emoji} {card.Name}{legacy}\n{card.Desc}\n부작용: {card.Side}";
                if (GUILayout.Button(label, _btn, GUILayout.Height(96)))
                    _flow.ToggleV2Item(card.Id);
                GUI.enabled = prev;
                GUILayout.Space(4);
            }

            GUILayout.Space(10);
            var pe = GUI.enabled;
            GUI.enabled = vm.CanStart;
            if (GUILayout.Button(vm.CanStart ? "밤 시작하기" : $"아이템 {vm.Slots}개를 골라주세요", _btn, GUILayout.Height(60)))
                _flow.ConfirmV2Setup();
            GUI.enabled = pe;
        }

        private void DrawPlay()
        {
            var vm = _flow.BuildV2Play();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"🕘 {vm.Clock}", _h1);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"새벽까지 {vm.RemainingMinutes}분", _h1);
            GUILayout.EndHorizontal();
            GUILayout.Label($"현재 연속 수면 {vm.CurrentSleepStretchMinutes}분  ·  최장 {vm.LongestSleepStretchMinutes}분", _h1);
            DrawBar(vm.RemainingMinutes == 540 ? 0 : vm.CurrentSleepStretchMinutes / 300f);
            GUILayout.Label($"수면 단계: {PresentationCopyMapper.V2StageLabel(vm.SleepStage)}", _body);
            GUILayout.Label($"보호자 체력 {vm.ParentStamina:0}  ·  울음 강도 {vm.CryIntensity:0}", _body);
            DrawBar((float)vm.ParentStamina / 100f);

            if (!vm.CauseResolved)
            {
                int remainingSeconds = UpdateDecisionTimer(vm);
                GUILayout.Space(6);
                GUILayout.Label("아기가 깼다 — 원인을 확인하세요", _h1);
                GUILayout.Label(vm.RevealedCause.HasValue
                    ? $"확인된 원인: {PresentationCopyMapper.WakeCauseLabel(vm.RevealedCause.Value)}"
                    : "원인은 아직 확인되지 않았다.", _body);
                GUILayout.Label($"결정 제한 {remainingSeconds}초", _logWarn);
            }
            if (vm.TemperatureChecked || vm.HumidityChecked)
                GUILayout.Label($"환경: {vm.TemperatureCelsius:0.#}°C · 습도 {vm.HumidityPercent:0.#}%", _body);
            GUILayout.Label(vm.FeedingReady ? "🍼 수유 준비 완료" : "🍼 수유 준비 중", _body);

            _playScroll = GUILayout.BeginScrollView(_playScroll, GUILayout.Height(560));
            DrawV2Outcome(_lastV2Result?.Outcome);
            GUILayout.Space(8);

            bool sleeping = vm.SleepStage == V2SleepStage.RemActiveSleep ||
                            vm.SleepStage == V2SleepStage.NremDeepSleep;
            var previous = GUI.enabled;
            GUI.enabled = previous && !_flow.InputLocked;
            if (sleeping && GUILayout.Button("잠든 동안 시간 빠르게 보내기", _btn, GUILayout.Height(56)))
            {
                _flow.FastForwardV2Sleep();
                _lastV2Result = null;
            }

            GUILayout.Label("관찰과 진단", _h1);
            DrawV2Actions(vm, V2ActionId.CheckDiaper, V2ActionId.ChangeDiaper,
                V2ActionId.CheckHungerSignals, V2ActionId.CheckEnvironment,
                V2ActionId.CheckLimbRelaxation, V2ActionId.Hesitate);
            GUILayout.Label("돌봄과 재입면", _h1);
            DrawV2Actions(vm, V2ActionId.Hold, V2ActionId.Pat, V2ActionId.Pacifier,
                V2ActionId.Laydown, V2ActionId.AdjustTemperature, V2ActionId.AdjustHumidity);
            GUILayout.Label("새벽 수유 준비", _h1);
            DrawV2Actions(vm, V2ActionId.SterilizeBottle, V2ActionId.PrepareWater,
                V2ActionId.MeasureFormula, V2ActionId.MixFormula, V2ActionId.CoolBottle,
                V2ActionId.CheckBottleTemperature, V2ActionId.HoldWhilePreparing,
                V2ActionId.FeedPreparedBottle);
            GUI.enabled = previous;
            GUILayout.EndScrollView();

            if (_flow.PendingOverlay != null)
                DrawOverlay(_flow.PendingOverlay);
        }

        private int UpdateDecisionTimer(V2PlayViewModel vm)
        {
            int sequence = _flow.Session.Night.V2.Diagnosis.EncounterSequence;
            if (_timedEncounterSequence != sequence)
            {
                _timedEncounterSequence = sequence;
                _decisionDeadline = Time.unscaledTime + vm.DecisionSecondsRemaining;
                _timeoutSent = false;
            }

            int remaining = Mathf.Max(0, Mathf.CeilToInt(_decisionDeadline - Time.unscaledTime));
            if (remaining == 0 && !_timeoutSent && !_flow.InputLocked)
            {
                _timeoutSent = true;
                _lastV2Result = _flow.ActV2(V2ActionId.Hesitate);
            }
            return remaining;
        }

        private void DrawV2Actions(V2PlayViewModel vm, params V2ActionId[] actions)
        {
            const int columns = 2;
            for (int i = 0; i < actions.Length; i += columns)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < i + columns && j < actions.Length; j++)
                {
                    var id = actions[j];
                    var button = vm.Actions.Find(a => a.Action == id);
                    var previous = GUI.enabled;
                    GUI.enabled = previous && button != null && button.Enabled;
                    if (GUILayout.Button(button?.Label ?? id.ToString(), _btn, GUILayout.Height(52)))
                        _lastV2Result = _flow.ActV2(id);
                    GUI.enabled = previous;
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawV2Outcome(V2ActionOutcome outcome)
        {
            if (outcome == null) return;
            GUILayout.Label(outcome.Accepted ? "행동 결과" : "아직 실행할 수 없음", _h1);
            if (outcome.ConsumedTime)
                GUILayout.Label($"시간 +{outcome.TimeDeltaMinutes}분 · 체력 {outcome.StaminaDelta:+0;-0;0}", _body);
            if (outcome.ObservedSignals.Count > 0)
            {
                GUILayout.Label("관찰된 신호", _body);
                foreach (var signal in outcome.ObservedSignals)
                    GUILayout.Label($"• {PresentationCopyMapper.ObservationLabel(signal)}", _logBaby);
            }
            if (outcome.MissingPreparationSteps.Count > 0)
            {
                GUILayout.Label("먼저 필요한 준비", _logWarn);
                foreach (var step in outcome.MissingPreparationSteps)
                    GUILayout.Label($"• {PresentationCopyMapper.FeedingStepLabel(step)}", _logWarn);
            }
        }

        private void DrawActionRow(PlayViewModel vm, bool timeConsuming)
        {
            GUILayout.BeginHorizontal();
            foreach (var a in vm.Actions)
            {
                if (a.ConsumesTime != timeConsuming) continue;
                var prev = GUI.enabled;
                GUI.enabled = prev && a.Enabled;
                string toggle = a.Toggled ? " ●" : "";
                string badge = a.BadgeText != null ? $" {a.BadgeText}" : "";
                if (GUILayout.Button($"{a.Label}{toggle}{badge}", _btn, GUILayout.Height(56)))
                    _flow.Act(a.Action);
                GUI.enabled = prev;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawOverlay(OverlayViewModel overlay)
        {
            var box = new Rect(40, 400, 600, 320);
            GUI.Box(box, GUIContent.none);
            GUILayout.BeginArea(new Rect(box.x + 24, box.y + 24, box.width - 48, box.height - 48));
            GUILayout.Label(overlay.Title, _h1);
            GUILayout.Space(10);
            foreach (var line in overlay.Lines)
                GUILayout.Label(line, _body);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("계속", _btn, GUILayout.Height(52)))
                _flow.DismissOverlay();
            GUILayout.EndArea();
        }

        private void DrawDiary()
        {
            var vm = _flow.BuildV2Diary();
            GUILayout.Label($"📖 {vm.NightLabel} · 밤의 기록", _h1);
            GUILayout.Space(8);
            GUILayout.Label($"등급 {vm.Grade}", _title);
            GUILayout.Space(10);
            GUILayout.Label($"최장 연속 수면  {vm.LongestSleepStretchMinutes}분", _h1);
            GUILayout.Label($"총 수면 {vm.TotalSleepMinutes}분 · 각성 {vm.WakeCount}회", _body);
            GUILayout.Label($"첫 진단 적중 {vm.CorrectFirstChecks}회 · 오판 {vm.MisdiagnosisCount}회", _body);
            GUILayout.Label($"안전 위반 {vm.UnsafeChoiceCount}회 · 남은 체력 {vm.ParentStaminaAtDawn:0}", _body);
            GUILayout.Space(20);
            var prev = GUI.enabled;
            GUI.enabled = false;
            GUILayout.Button("다음 밤 연결은 후속 단계", _btn, GUILayout.Height(56));
            GUI.enabled = prev;
        }

        private void DrawBar(float t)
        {
            t = Mathf.Clamp01(t);
            var rect = GUILayoutUtility.GetRect(100, 18, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none);
            var fill = new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * t, rect.height - 4);
            var c = GUI.color;
            GUI.color = t > 0.3f ? new Color(0.45f, 0.85f, 0.5f) : new Color(0.95f, 0.5f, 0.3f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = c;
        }

        private GUIStyle LogStyle(LogClass cls) => cls switch
        {
            LogClass.Good => _logGood,
            LogClass.Warn => _logWarn,
            LogClass.Baby => _logBaby,
            _ => _logSys
        };
    }
}
