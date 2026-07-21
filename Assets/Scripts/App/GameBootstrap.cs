using System;
using NotANap.Core;
using NotANap.Presentation;
using UnityEngine;

namespace NotANap.App
{
    /// <summary>
    /// 16:9 WebGL presentation shell. Core 판정은 Presenter에만 위임하고,
    /// 이 클래스는 화면의 정보 위계와 입력 흐름만 담당한다.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const float RefWidth = 1920f;
        private const float RefHeight = 1080f;

        private GameFlowController _flow;
        private V2PresentationActionResult _lastResult;
        private int _timedEncounterSequence = -1;
        private float _decisionDeadline;
        private bool _timeoutSent;
        private ActionGroup _actionGroup = ActionGroup.Diagnose;

        private Font _font;
        private Texture2D _room;
        private GUIStyle _display;
        private GUIStyle _headline;
        private GUIStyle _title;
        private GUIStyle _body;
        private GUIStyle _caption;
        private GUIStyle _button;
        private GUIStyle _buttonSmall;
        private GUIStyle _buttonSelected;

        private enum ActionGroup { Diagnose, Care, Feed }

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
            _flow = new GameFlowController(new SystemRandomSource(Environment.TickCount));
            _room = Resources.Load<Texture2D>("Art/nursery-night");
        }

        private void EnsureStyles()
        {
            if (_display != null) return;
            _font = Resources.Load<Font>("Fonts/NotoSansKR");

            _display = LabelStyle(46, FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f), TextAnchor.MiddleLeft);
            _headline = LabelStyle(28, FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f));
            _title = LabelStyle(82, FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f), TextAnchor.MiddleCenter);
            _body = LabelStyle(20, FontStyle.Normal, new Color(0.82f, 0.85f, 0.88f));
            _caption = LabelStyle(15, FontStyle.Bold, new Color(0.62f, 0.68f, 0.74f));

            _button = ButtonStyle(22, new Color(0.09f, 0.14f, 0.21f, 0.98f), new Color(0.91f, 0.72f, 0.42f), new Color(0.97f, 0.94f, 0.87f));
            _buttonSmall = ButtonStyle(17, new Color(0.07f, 0.11f, 0.17f, 0.94f), new Color(0.28f, 0.36f, 0.45f), new Color(0.82f, 0.85f, 0.88f));
            _buttonSelected = ButtonStyle(17, new Color(0.78f, 0.54f, 0.23f, 0.98f), new Color(0.95f, 0.76f, 0.44f), Color.white);
        }

        private GUIStyle LabelStyle(int size, FontStyle weight, Color color, TextAnchor align = TextAnchor.UpperLeft)
            => new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = size,
                fontStyle = weight,
                alignment = align,
                wordWrap = true,
                normal = { textColor = color }
            };

        private GUIStyle ButtonStyle(int size, Color background, Color border, Color text)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                font = _font,
                fontSize = size,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(14, 14, 10, 10),
                border = new RectOffset(2, 2, 2, 2)
            };
            style.normal.background = SolidTexture(background);
            style.hover.background = SolidTexture(Color.Lerp(background, border, 0.22f));
            style.active.background = SolidTexture(border);
            style.normal.textColor = text;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            return style;
        }

        private static Texture2D SolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnGUI()
        {
            EnsureStyles();
            var oldMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
            float offsetX = (Screen.width - RefWidth * scale) * 0.5f;
            float offsetY = (Screen.height - RefHeight * scale) * 0.5f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY), Quaternion.identity, new Vector3(scale, scale, 1));

            DrawBackdrop();
            switch (_flow.Screen)
            {
                case ScreenState.Title: DrawTitle(); break;
                case ScreenState.Setup: DrawSetup(); break;
                case ScreenState.Play: DrawPlay(); break;
                case ScreenState.Diary: DrawDiary(); break;
            }
            GUI.matrix = oldMatrix;
        }

        private void DrawBackdrop()
        {
            GUI.color = Color.white;
            if (_room != null) GUI.DrawTexture(new Rect(0, 0, RefWidth, RefHeight), _room, ScaleMode.ScaleAndCrop);
            else Fill(new Rect(0, 0, RefWidth, RefHeight), new Color(0.025f, 0.055f, 0.1f));
            Fill(new Rect(0, 0, RefWidth, RefHeight), new Color(0.01f, 0.025f, 0.05f, 0.25f));
        }

        private void DrawTitle()
        {
            Fill(new Rect(0, 0, RefWidth, RefHeight), new Color(0.01f, 0.025f, 0.05f, 0.34f));
            GUI.Label(new Rect(470, 220, 980, 110), "NOT A NAP", _title);
            GUI.Label(new Rect(650, 328, 620, 56), "백일의 밤", Centered(_headline));
            GUI.Label(new Rect(610, 445, 700, 80), "오늘 밤은 아빠 차례다.\n아기는 당신의 모든 선택을 기억한다.", Centered(_body));
            if (GUI.Button(new Rect(760, 690, 400, 78), "첫째 밤 시작하기", _button))
                _flow.StartGame();
            GUI.Label(new Rect(760, 790, 400, 28), "약 5분 · 선택은 다음 밤의 규칙이 됩니다", Centered(_caption));
        }

        private void DrawSetup()
        {
            var vm = _flow.BuildV2Setup();
            Fill(new Rect(0, 0, RefWidth, RefHeight), new Color(0.015f, 0.035f, 0.065f, 0.78f));
            GUI.Label(new Rect(90, 64, 900, 56), $"{vm.NightLabel}  ·  밤 준비", _display);
            GUI.Label(new Rect(92, 130, 1100, 34), $"“{vm.TemperamentHint}”", _body);
            GUI.Label(new Rect(1450, 75, 360, 44), $"가져갈 물건  {vm.SelectedCount} / {vm.Slots}", Right(_headline));

            const float cardW = 548f;
            const float cardH = 250f;
            for (int i = 0; i < vm.Cards.Count; i++)
            {
                var card = vm.Cards[i];
                int col = i % 3;
                int row = i / 3;
                var rect = new Rect(90 + col * 580, 220 + row * 280, cardW, cardH);
                DrawItemCard(rect, card, vm.SelectedCount, vm.Slots);
            }

            var oldEnabled = GUI.enabled;
            GUI.enabled = vm.CanStart;
            string next = vm.CanStart ? "이 준비로 밤 시작하기  →" : $"물건을 {vm.Slots}개 골라주세요";
            if (GUI.Button(new Rect(1300, 930, 520, 82), next, _button)) _flow.ConfirmV2Setup();
            GUI.enabled = oldEnabled;
        }

        private void DrawItemCard(Rect rect, ItemCardViewModel card, int selected, int slots)
        {
            Color panel = card.Selected ? new Color(0.32f, 0.23f, 0.12f, 0.98f) : new Color(0.055f, 0.09f, 0.14f, 0.96f);
            if (card.Disabled) panel.a = 0.52f;
            Fill(rect, panel);
            Fill(new Rect(rect.x, rect.y, card.Selected ? 7 : 2, rect.height), card.Selected ? new Color(0.92f, 0.7f, 0.36f) : new Color(0.2f, 0.28f, 0.36f));
            GUI.Label(new Rect(rect.x + 24, rect.y + 20, 500, 42), $"{card.Emoji}  {card.Name}", _headline);
            GUI.Label(new Rect(rect.x + 24, rect.y + 78, 500, 70), card.Desc, _body);
            GUI.Label(new Rect(rect.x + 24, rect.y + 168, 500, 52), $"주의  {card.Side}", _caption);
            var oldEnabled = GUI.enabled;
            GUI.enabled = !card.Disabled;
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) _flow.ToggleV2Item(card.Id);
            GUI.enabled = oldEnabled;
        }

        private void DrawPlay()
        {
            var vm = _flow.BuildV2Play();
            if (!vm.CauseResolved) _actionGroup = ActionGroup.Diagnose;

            DrawTopBar(vm);
            DrawStatusPanel(vm);
            DrawActionPanel(vm);
            DrawEventPanel(vm);

            if (_flow.PendingOverlay != null) DrawOverlay(_flow.PendingOverlay);
        }

        private void DrawTopBar(V2PlayViewModel vm)
        {
            Fill(new Rect(0, 0, RefWidth, 94), new Color(0.02f, 0.045f, 0.08f, 0.94f));
            GUI.Label(new Rect(70, 20, 320, 52), vm.Clock, _display);
            GUI.Label(new Rect(395, 28, 500, 40), vm.CauseResolved ? "조용한 밤을 이어가는 중" : "아기가 깼어요 · 원인을 찾아주세요", _body);
            GUI.Label(new Rect(1440, 25, 410, 46), $"새벽까지  {FormatDuration(vm.RemainingMinutes)}", Right(_headline));
            Fill(new Rect(0, 92, RefWidth * (1f - vm.RemainingMinutes / 540f), 2), new Color(0.91f, 0.7f, 0.36f));
        }

        private void DrawStatusPanel(V2PlayViewModel vm)
        {
            var panel = new Rect(48, 132, 360, 672);
            Panel(panel);
            GUI.Label(new Rect(76, 162, 304, 28), "아기의 지금", _caption);
            GUI.Label(new Rect(74, 205, 308, 54), PresentationCopyMapper.V2StageLabel(vm.SleepStage), _headline);
            string signal = vm.CauseResolved ? SleepSignal(vm) : CauseSignal(vm);
            GUI.Label(new Rect(74, 275, 308, 92), signal, _body);

            GUI.Label(new Rect(74, 400, 280, 28), "연속 수면", _caption);
            GUI.Label(new Rect(74, 432, 280, 48), FormatDuration(vm.CurrentSleepStretchMinutes), _headline);
            DrawProgress(new Rect(74, 488, 280, 10), Mathf.Clamp01(vm.CurrentSleepStretchMinutes / 300f), new Color(0.38f, 0.68f, 0.86f));

            GUI.Label(new Rect(74, 548, 280, 28), "보호자 체력", _caption);
            GUI.Label(new Rect(74, 580, 280, 48), $"{vm.ParentStamina:0}", _headline);
            DrawProgress(new Rect(74, 636, 280, 10), Mathf.Clamp01((float)vm.ParentStamina / 100f), vm.ParentStamina >= 30 ? new Color(0.49f, 0.82f, 0.6f) : new Color(0.9f, 0.38f, 0.34f));

            if (vm.TemperatureChecked || vm.HumidityChecked)
                GUI.Label(new Rect(74, 696, 280, 34), $"방  {vm.TemperatureCelsius:0.#}°C  ·  {vm.HumidityPercent:0.#}%", _body);
        }

        private void DrawActionPanel(V2PlayViewModel vm)
        {
            var panel = new Rect(1430, 132, 442, 858);
            Panel(panel);
            GUI.Label(new Rect(1460, 162, 380, 36), "어떻게 할까요?", _headline);

            DrawTab(new Rect(1460, 220, 120, 48), "살펴보기", ActionGroup.Diagnose);
            DrawTab(new Rect(1589, 220, 120, 48), "돌보기", ActionGroup.Care);
            DrawTab(new Rect(1718, 220, 120, 48), "수유 준비", ActionGroup.Feed);

            var actions = ActionsFor(_actionGroup);
            float y = 292;
            for (int i = 0; i < actions.Length; i++)
            {
                var id = actions[i];
                var action = vm.Actions.Find(a => a.Action == id);
                if (action == null) continue;
                var oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && action.Enabled && !_flow.InputLocked;
                if (GUI.Button(new Rect(1460, y, 378, 64), action.Label, _buttonSmall))
                    _lastResult = _flow.ActV2(id);
                GUI.enabled = oldEnabled;
                y += 74;
            }

            bool sleeping = vm.SleepStage == V2SleepStage.RemActiveSleep || vm.SleepStage == V2SleepStage.NremDeepSleep;
            if (sleeping && GUI.Button(new Rect(1460, 908, 378, 56), "잠든 동안 조용히 시간 보내기  ›", _button))
            {
                _flow.FastForwardV2Sleep();
                _lastResult = null;
            }
        }

        private void DrawEventPanel(V2PlayViewModel vm)
        {
            var rect = new Rect(448, 790, 934, 200);
            Panel(rect);
            GUI.Label(new Rect(478, 816, 840, 28), !vm.CauseResolved ? $"결정까지  {UpdateDecisionTimer(vm)}초" : "방금 일어난 일", _caption);

            string title = "아기의 숨소리만 방 안에 작게 들린다.";
            string detail = "지금은 서두르지 않아도 괜찮아요.";
            var outcome = _lastResult?.Outcome;
            if (outcome != null)
            {
                title = outcome.Accepted ? "당신의 선택이 밤을 조금 바꿨다." : "아직 그 행동을 할 수 없어요.";
                if (outcome.ObservedSignals.Count > 0)
                    detail = "관찰  ·  " + PresentationCopyMapper.ObservationLabel(outcome.ObservedSignals[0]);
                else if (outcome.MissingPreparationSteps.Count > 0)
                    detail = "먼저 필요함  ·  " + PresentationCopyMapper.FeedingStepLabel(outcome.MissingPreparationSteps[0]);
                else if (outcome.ConsumedTime)
                    detail = $"{outcome.TimeDeltaMinutes}분이 흘렀고, 체력이 {outcome.StaminaDelta:+0;-0;0} 변했어요.";
            }
            GUI.Label(new Rect(478, 856, 840, 42), title, _headline);
            GUI.Label(new Rect(478, 910, 840, 52), detail, _body);
        }

        private void DrawTab(Rect rect, string label, ActionGroup group)
        {
            if (GUI.Button(rect, label, _actionGroup == group ? _buttonSelected : _buttonSmall)) _actionGroup = group;
        }

        private static V2ActionId[] ActionsFor(ActionGroup group)
        {
            switch (group)
            {
                case ActionGroup.Diagnose:
                    return new[] { V2ActionId.CheckDiaper, V2ActionId.CheckHungerSignals, V2ActionId.CheckEnvironment, V2ActionId.CheckLimbRelaxation, V2ActionId.Hesitate };
                case ActionGroup.Care:
                    return new[] { V2ActionId.Hold, V2ActionId.Pat, V2ActionId.Pacifier, V2ActionId.Laydown, V2ActionId.ChangeDiaper, V2ActionId.AdjustTemperature, V2ActionId.AdjustHumidity };
                default:
                    return new[] { V2ActionId.SterilizeBottle, V2ActionId.PrepareWater, V2ActionId.MeasureFormula, V2ActionId.MixFormula, V2ActionId.CoolBottle, V2ActionId.CheckBottleTemperature, V2ActionId.HoldWhilePreparing, V2ActionId.FeedPreparedBottle };
            }
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
                _lastResult = _flow.ActV2(V2ActionId.Hesitate);
            }
            return remaining;
        }

        private void DrawOverlay(OverlayViewModel overlay)
        {
            Fill(new Rect(0, 0, RefWidth, RefHeight), new Color(0, 0, 0, 0.62f));
            var box = new Rect(600, 300, 720, 460);
            Panel(box, 0.99f);
            GUI.Label(new Rect(650, 350, 620, 52), overlay.Title, Centered(_headline));
            float y = 435;
            foreach (var line in overlay.Lines)
            {
                GUI.Label(new Rect(680, y, 560, 50), line, Centered(_body));
                y += 54;
            }
            if (GUI.Button(new Rect(760, 650, 400, 66), "계속하기", _button)) _flow.DismissOverlay();
        }

        private void DrawDiary()
        {
            var vm = _flow.BuildV2Diary();
            Fill(new Rect(0, 0, RefWidth, RefHeight), new Color(0.015f, 0.035f, 0.065f, 0.84f));
            GUI.Label(new Rect(110, 76, 900, 58), $"{vm.NightLabel}  ·  밤의 기록", _display);
            Panel(new Rect(110, 200, 560, 680));
            GUI.Label(new Rect(155, 245, 470, 34), "오늘 밤의 등급", _caption);
            GUI.Label(new Rect(155, 285, 470, 130), vm.Grade.ToString(), new GUIStyle(_title) { alignment = TextAnchor.MiddleLeft, fontSize = 120 });
            GUI.Label(new Rect(155, 465, 470, 46), $"최장 연속 수면  {FormatDuration(vm.LongestSleepStretchMinutes)}", _headline);
            GUI.Label(new Rect(155, 535, 470, 40), $"총 수면  {FormatDuration(vm.TotalSleepMinutes)}", _body);
            GUI.Label(new Rect(155, 585, 470, 40), $"깨어난 횟수  {vm.WakeCount}회", _body);
            GUI.Label(new Rect(155, 635, 470, 40), $"첫 진단 적중  {vm.CorrectFirstChecks}회", _body);
            GUI.Label(new Rect(155, 685, 470, 40), $"남은 체력  {vm.ParentStaminaAtDawn:0}", _body);

            Panel(new Rect(720, 200, 1090, 680));
            GUI.Label(new Rect(770, 245, 980, 36), "육아일지", _caption);
            GUI.Label(new Rect(770, 305, 940, 110), "완벽하게 재우는 밤은 없었다.\n하지만 우리 가족이 계속할 수 있는 방법을 조금 배웠다.", _headline);
            GUI.Label(new Rect(770, 465, 940, 44), "오늘의 선택은 다음 밤에 돌아옵니다.", _body);
            GUI.Label(new Rect(770, 535, 940, 120), $"오판 {vm.MisdiagnosisCount}회  ·  안전 위반 {vm.UnsafeChoiceCount}회\n아기는 반복된 행동을 새로운 잠 습관으로 기억해요.", _body);
            if (GUI.Button(new Rect(1290, 920, 520, 76), "처음부터 다시 보기", _button))
            {
                _flow = new GameFlowController(new SystemRandomSource(Environment.TickCount));
                _lastResult = null;
                _actionGroup = ActionGroup.Diagnose;
                _timedEncounterSequence = -1;
            }
        }

        private static string CauseSignal(V2PlayViewModel vm)
            => vm.RevealedCause.HasValue ? $"확인된 원인\n{PresentationCopyMapper.WakeCauseLabel(vm.RevealedCause.Value)}" : "왜 깼는지 아직 몰라요.\n먼저 작은 신호부터 살펴보세요.";

        private static string SleepSignal(V2PlayViewModel vm)
        {
            if (vm.CryIntensity > 45) return "울음이 커지고 있어요.\n자극을 줄이고 천천히 반응하세요.";
            if (vm.IsLimbRelaxed && vm.IsBreathingRegular) return "팔다리에 힘이 빠지고\n숨결이 고르게 이어져요.";
            return "표정과 숨소리를 살피며\n다음 행동을 고르세요.";
        }

        private static string FormatDuration(int minutes) => minutes >= 60 ? $"{minutes / 60}시간 {minutes % 60:00}분" : $"{minutes}분";

        private void Panel(Rect rect, float alpha = 0.94f) => Fill(rect, new Color(0.035f, 0.065f, 0.105f, alpha));

        private static void Fill(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void DrawProgress(Rect rect, float value, Color color)
        {
            Fill(rect, new Color(0.12f, 0.17f, 0.22f, 0.9f));
            Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), color);
        }

        private static GUIStyle Centered(GUIStyle source) => new GUIStyle(source) { alignment = TextAnchor.MiddleCenter };
        private static GUIStyle Right(GUIStyle source) => new GUIStyle(source) { alignment = TextAnchor.MiddleRight };
    }
}
