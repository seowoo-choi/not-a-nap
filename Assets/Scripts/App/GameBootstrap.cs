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
        private const float LandscapeWidth = 1920f;
        private const float LandscapeHeight = 1080f;
        private const float PortraitWidth = 1080f;
        private const float PortraitHeight = 1920f;

        private GameFlowController _flow;
        private BabyVisualPresenter _babyVisual;
        private V2PresentationActionResult _lastResult;
        private int _timedEncounterSequence = -1;
        private float _decisionDeadline;
        private bool _timeoutSent;
        private ActionGroup _actionGroup = ActionGroup.Diagnose;
        private int _actionEncounterSequence = -1;
        private bool _portrait;

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
        private GUIStyle _tabButton;
        private GUIStyle _tabSelected;
        private Texture2D _speechBubble;

        private System.Random _ambientRandom;
        private int _ambientFrame;
        private int _previousAmbientFrame;
        private float _ambientTransitionStarted;
        private float _ambientTransitionDuration = 0.24f;
        private float _nextAmbientMotionAt;
        private float _nextBabbleAt;
        private float _babbleUntil;
        private string _currentBabble;
        private V2ActionOutcome _trackedVisualOutcome;
        private float _visualOutcomeUntil;

        private static readonly string[] AwakeBabble = { "아우…", "으응?", "응아", "에…", "아으" };
        private static readonly string[] FussBabble = { "으응…", "에에…", "아으…" };
        private static readonly string[] CryBabble = { "으아앙!", "에앵!", "아앙…" };

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
            _babyVisual = new BabyVisualPresenter();
            _room = Resources.Load<Texture2D>("Art/nursery-night-empty");
            _ambientRandom = new System.Random(Environment.TickCount ^ GetInstanceID());
            _nextAmbientMotionAt = Time.unscaledTime + RandomRange(0.4f, 1.4f);
            _nextBabbleAt = Time.unscaledTime + RandomRange(1.8f, 4.5f);
        }

        private void EnsureStyles()
        {
            if (_display != null) return;
            _font = Resources.Load<Font>("Fonts/NotoSansKR");

            _display = LabelStyle(52, FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f), TextAnchor.MiddleLeft);
            _headline = LabelStyle(34, FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f));
            _title = LabelStyle(82, FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f), TextAnchor.MiddleCenter);
            _body = LabelStyle(26, FontStyle.Normal, new Color(0.82f, 0.85f, 0.88f));
            _caption = LabelStyle(20, FontStyle.Bold, new Color(0.62f, 0.68f, 0.74f));

            _button = ButtonStyle(28, new Color(0.09f, 0.14f, 0.21f, 0.98f), new Color(0.91f, 0.72f, 0.42f), new Color(0.97f, 0.94f, 0.87f));
            _buttonSmall = ButtonStyle(23, new Color(0.07f, 0.11f, 0.17f, 0.94f), new Color(0.28f, 0.36f, 0.45f), new Color(0.82f, 0.85f, 0.88f));
            _buttonSelected = ButtonStyle(23, new Color(0.78f, 0.54f, 0.23f, 0.98f), new Color(0.95f, 0.76f, 0.44f), Color.white);
            _tabButton = ButtonStyle(18, new Color(0.07f, 0.11f, 0.17f, 0.94f), new Color(0.28f, 0.36f, 0.45f), new Color(0.82f, 0.85f, 0.88f));
            _tabSelected = ButtonStyle(18, new Color(0.78f, 0.54f, 0.23f, 0.98f), new Color(0.95f, 0.76f, 0.44f), Color.white);
            _speechBubble = RoundedTexture(new Color(0.97f, 0.94f, 0.87f, 0.98f), 14);
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

        private static Texture2D RoundedTexture(Color color, int radius)
        {
            const int width = 64;
            const int height = 32;
            var texture = new Texture2D(width, height) { hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Max(radius - x, x - (width - radius - 1), 0);
                float dy = Mathf.Max(radius - y, y - (height - radius - 1), 0);
                texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? color : Color.clear);
            }
            texture.Apply();
            return texture;
        }

        private void OnGUI()
        {
            EnsureStyles();
            var oldMatrix = GUI.matrix;
            _portrait = Screen.height > Screen.width * 1.15f;
            float referenceWidth = _portrait ? PortraitWidth : LandscapeWidth;
            float referenceHeight = _portrait ? PortraitHeight : LandscapeHeight;
            float scale = Mathf.Min(Screen.width / referenceWidth, Screen.height / referenceHeight);
            float offsetX = (Screen.width - referenceWidth * scale) * 0.5f;
            float offsetY = (Screen.height - referenceHeight * scale) * 0.5f;
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
            float width = _portrait ? PortraitWidth : LandscapeWidth;
            float height = _portrait ? PortraitHeight : LandscapeHeight;
            if (_room != null) GUI.DrawTexture(new Rect(0, 0, width, height), _room, ScaleMode.ScaleAndCrop);
            else Fill(new Rect(0, 0, width, height), new Color(0.025f, 0.055f, 0.1f));
            Fill(new Rect(0, 0, width, height), new Color(0.01f, 0.025f, 0.05f, 0.25f));
        }

        private void DrawTitle()
        {
            if (_portrait) { DrawPortraitTitle(); return; }
            Fill(new Rect(0, 0, LandscapeWidth, LandscapeHeight), new Color(0.01f, 0.025f, 0.05f, 0.34f));
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
            if (_portrait) { DrawPortraitSetup(vm); return; }
            Fill(new Rect(0, 0, LandscapeWidth, LandscapeHeight), new Color(0.015f, 0.035f, 0.065f, 0.78f));
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
                var rect = new Rect(90 + col * 580, 220 + row * 270, cardW, cardH);
                DrawItemCard(rect, card, vm.SelectedCount, vm.Slots);
            }
            // 후속 해금 후보 3종은 Figma M_UNLOCK_CANDIDATES와 동일하게 전부 노출한다(선택 불가).
            DrawLockedCandidate(new Rect(670, 490, cardW, cardH), "옆잠베개", "월령과 제품별 안전 조건을 확인한 뒤 해금됩니다.");
            DrawLockedCandidate(new Rect(1250, 490, cardW, cardH), "토닥이인형", "사용 환경과 안전 기준을 확인한 뒤 해금됩니다.");
            DrawLockedCandidate(new Rect(90, 760, cardW, cardH), "수면 포지셔너", "안전 수면 자세 지침을 확인한 뒤 해금됩니다.");

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
            GUI.Label(new Rect(rect.x + 24, rect.y + 20, 500, 52), card.Name, _headline);
            GUI.Label(new Rect(rect.x + 24, rect.y + 82, 500, 105), card.Desc, _body);
            GUI.Label(new Rect(rect.x + 24, rect.y + rect.height - 56, 500, 54), $"주의  {card.Side}", _caption);
            var oldEnabled = GUI.enabled;
            GUI.enabled = !card.Disabled;
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) _flow.ToggleV2Item(card.Id);
            GUI.enabled = oldEnabled;
        }

        private void DrawLockedCandidate(Rect rect, string name, string description)
        {
            Fill(rect, new Color(0.055f, 0.075f, 0.1f, 0.82f));
            Fill(new Rect(rect.x, rect.y, 5, rect.height), new Color(0.35f, 0.39f, 0.44f));
            GUI.Label(new Rect(rect.x + 24, rect.y + 18, rect.width - 48, 52), $"잠김 · {name}", _headline);
            GUI.Label(new Rect(rect.x + 24, rect.y + 76, rect.width - 48, Mathf.Max(38f, rect.height - 122)), description, _body);
            GUI.Label(new Rect(rect.x + 24, rect.y + rect.height - 42, rect.width - 48, 38), "후속 해금 후보 · 현재 선택 불가", _caption);
        }

        private void DrawPlay()
        {
            var vm = _flow.BuildV2Play();
            int encounterSequence = _flow.Session.Night.V2.Diagnosis.EncounterSequence;
            if (!vm.CauseResolved && _actionEncounterSequence != encounterSequence)
            {
                _actionEncounterSequence = encounterSequence;
                _actionGroup = ActionGroup.Diagnose;
            }
            UpdateBabyAmbient(vm);

            if (_portrait)
            {
                DrawPortraitPlay(vm);
                if (_flow.PendingOverlay != null) DrawPortraitOverlay(_flow.PendingOverlay);
                return;
            }

            DrawTopBar(vm);
            DrawLandscapeBaby(vm);
            DrawStatusPanel(vm);
            DrawActionPanel(vm);
            DrawEventPanel(vm);

            if (_flow.PendingOverlay != null) DrawOverlay(_flow.PendingOverlay);
        }

        private void DrawTopBar(V2PlayViewModel vm)
        {
            Fill(new Rect(0, 0, LandscapeWidth, 94), new Color(0.02f, 0.045f, 0.08f, 0.94f));
            GUI.Label(new Rect(70, 20, 320, 52), vm.Clock, _display);
            GUI.Label(new Rect(395, 28, 500, 40), vm.CauseResolved ? "조용한 밤을 이어가는 중" : "아기가 깼어요 · 원인을 찾아주세요", _body);
            GUI.Label(new Rect(1440, 25, 410, 46), $"새벽까지  {FormatDuration(vm.RemainingMinutes)}", Right(_headline));
            Fill(new Rect(0, 92, LandscapeWidth * (1f - vm.RemainingMinutes / 540f), 2), new Color(0.91f, 0.7f, 0.36f));
        }

        private void DrawPortraitPlay(V2PlayViewModel vm)
        {
            Fill(new Rect(0, 0, PortraitWidth, PortraitHeight), new Color(0.01f, 0.03f, 0.06f, 0.38f));
            Fill(new Rect(0, 0, PortraitWidth, 130), new Color(0.02f, 0.045f, 0.08f, 0.96f));
            GUI.Label(new Rect(48, 30, 300, 72), vm.Clock, _display);
            GUI.Label(new Rect(430, 35, 600, 62), $"새벽까지 {FormatDuration(vm.RemainingMinutes)}", Right(_headline));
            Fill(new Rect(0, 126, PortraitWidth * (1f - vm.RemainingMinutes / 540f), 4), new Color(0.91f, 0.7f, 0.36f));

            DrawBabyStateVisual(vm, new Rect(70, 170, 940, 440));

            Panel(new Rect(48, 640, 984, 250));
            GUI.Label(new Rect(82, 675, 430, 44), "연속 수면", _caption);
            GUI.Label(new Rect(82, 720, 430, 62), FormatDuration(vm.CurrentSleepStretchMinutes), _headline);
            DrawProgress(new Rect(82, 802, 420, 18), Mathf.Clamp01(vm.CurrentSleepStretchMinutes / 300f), new Color(0.38f, 0.68f, 0.86f));
            GUI.Label(new Rect(570, 675, 380, 44), "보호자 체력", _caption);
            GUI.Label(new Rect(570, 720, 380, 62), $"{vm.ParentStamina:0}", _headline);
            DrawProgress(new Rect(570, 802, 380, 18), Mathf.Clamp01((float)vm.ParentStamina / 100f), vm.ParentStamina >= 30 ? new Color(0.49f, 0.82f, 0.6f) : new Color(0.9f, 0.38f, 0.34f));

            DrawPortraitEvent(vm);
            DrawPortraitActions(vm);
        }

        private void DrawBabyStateVisual(V2PlayViewModel vm, Rect rect)
        {
            if (_room != null)
                GUI.DrawTexture(rect, _room, ScaleMode.ScaleAndCrop, true);
            else
                Fill(rect, new Color(0.025f, 0.055f, 0.1f));
            Fill(rect, new Color(0.01f, 0.025f, 0.05f, 0.38f));
            var babyRect = new Rect(rect.x + rect.width * 0.5f - 175, rect.y + 14, 350, 350);
            DrawAnimatedBaby(vm, babyRect);
            DrawBabbleBubble(vm, babyRect, true);

            string state = BabyStateHeadline(vm);
            GUI.Label(new Rect(rect.x + 45, rect.y + 340, rect.width - 90, 48), state, Centered(_headline));
            GUI.Label(new Rect(rect.x + 60, rect.y + 392, rect.width - 120, 38), BabyStepHint(vm), Centered(_body));
        }

        private void DrawLandscapeBaby(V2PlayViewModel vm)
        {
            var babyRect = new Rect(690, 210, 520, 520);
            DrawAnimatedBaby(vm, babyRect);
            DrawBabbleBubble(vm, babyRect, false);
        }

        private void DrawAnimatedBaby(V2PlayViewModel vm, Rect baseRect)
        {
            bool sleeping = IsSleeping(vm);
            float now = Time.unscaledTime;
            float phase = now * (sleeping ? 1.1f : 1.55f);
            float breath = (Mathf.Sin(phase) + 1f) * 0.5f;
            float scale = 1f + breath * (sleeping ? 0.014f : 0.009f);
            float wiggle = sleeping ? 0f : Mathf.Sin(now * 0.73f) * 1.4f + Mathf.Sin(now * 1.17f) * 0.7f;
            if (vm.CryIntensity > 35) wiggle += Mathf.Sin(now * 5.3f) * 3.5f;
            float width = baseRect.width * scale;
            float height = baseRect.height * scale;
            var animated = new Rect(
                baseRect.center.x - width * 0.5f + wiggle,
                baseRect.center.y - height * 0.5f - breath * (sleeping ? 3f : 6f),
                width,
                height);

            var outcome = ActiveVisualOutcome();
            var current = _babyVisual.AnimationFrameFor(vm, outcome, _ambientFrame);
            var previous = _babyVisual.AnimationFrameFor(vm, outcome, _previousAmbientFrame);
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((now - _ambientTransitionStarted) / _ambientTransitionDuration));
            Color oldColor = GUI.color;
            if (current == previous)
            {
                if (current != null) GUI.DrawTexture(animated, current, ScaleMode.ScaleToFit, true);
                return;
            }
            if (previous != null && blend < 1f)
            {
                GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * (1f - blend));
                GUI.DrawTexture(animated, previous, ScaleMode.ScaleToFit, true);
            }
            if (current != null)
            {
                GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * blend);
                GUI.DrawTexture(animated, current, ScaleMode.ScaleToFit, true);
            }
            GUI.color = oldColor;
        }

        private void DrawBabbleBubble(V2PlayViewModel vm, Rect babyRect, bool portrait)
        {
            if (IsSleeping(vm) || Time.unscaledTime >= _babbleUntil || string.IsNullOrEmpty(_currentBabble)) return;

            float bubbleWidth = portrait ? 190f : 220f;
            float bubbleHeight = portrait ? 74f : 82f;
            var bubble = new Rect(
                babyRect.xMax - (portrait ? 18f : 38f),
                babyRect.y + (portrait ? 30f : 40f),
                bubbleWidth,
                bubbleHeight);
            GUI.DrawTexture(bubble, _speechBubble, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(new Rect(bubble.x - 17f, bubble.y + bubble.height * 0.62f, 13f, 13f), _speechBubble, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(new Rect(bubble.x - 31f, bubble.y + bubble.height * 0.76f, 8f, 8f), _speechBubble, ScaleMode.StretchToFill, true);
            var style = LabelStyle(portrait ? 28 : 31, FontStyle.Bold, new Color(0.09f, 0.12f, 0.17f), TextAnchor.MiddleCenter);
            GUI.Label(bubble, _currentBabble, style);
        }

        private void UpdateBabyAmbient(V2PlayViewModel vm)
        {
            float now = Time.unscaledTime;
            bool sleeping = IsSleeping(vm);

            if (now >= _nextAmbientMotionAt)
            {
                _previousAmbientFrame = _ambientFrame;
                int candidate = sleeping
                    ? (_ambientFrame + _ambientRandom.Next(1, 4)) % 4
                    : _ambientFrame == 0 ? _ambientRandom.Next(1, 4) : 0;
                _ambientFrame = candidate;
                _ambientTransitionStarted = now;
                _ambientTransitionDuration = RandomRange(sleeping ? 0.32f : 0.16f, sleeping ? 0.58f : 0.34f);
                if (sleeping)
                    _nextAmbientMotionAt = now + RandomRange(0.9f, 2.8f);
                else if (_ambientFrame == 0)
                    _nextAmbientMotionAt = now + RandomRange(vm.CryIntensity > 35 ? 0.3f : 0.8f, vm.CryIntensity > 35 ? 1.0f : 4.2f);
                else
                    _nextAmbientMotionAt = now + RandomRange(0.24f, 0.72f);
            }

            if (sleeping)
            {
                _currentBabble = null;
                _babbleUntil = 0f;
                if (_nextBabbleAt < now) _nextBabbleAt = now + RandomRange(2.5f, 6f);
                return;
            }

            if (now >= _nextBabbleAt)
            {
                string[] choices = vm.CryIntensity > 35 ? CryBabble : vm.CryIntensity > 0 ? FussBabble : AwakeBabble;
                _currentBabble = choices[_ambientRandom.Next(choices.Length)];
                _babbleUntil = now + RandomRange(vm.CryIntensity > 35 ? 1.5f : 0.9f, vm.CryIntensity > 35 ? 2.8f : 1.9f);
                _nextBabbleAt = _babbleUntil + RandomRange(vm.CryIntensity > 35 ? 1.4f : 3.2f, vm.CryIntensity > 35 ? 4f : 9.5f);
            }
        }

        private V2ActionOutcome ActiveVisualOutcome()
        {
            var latest = _lastResult?.Outcome;
            if (!ReferenceEquals(latest, _trackedVisualOutcome))
            {
                _trackedVisualOutcome = latest;
                _visualOutcomeUntil = Time.unscaledTime + 1.35f;
            }
            return latest != null && Time.unscaledTime < _visualOutcomeUntil ? latest : null;
        }

        private float RandomRange(float min, float max)
            => min + (float)_ambientRandom.NextDouble() * (max - min);

        private static bool IsSleeping(V2PlayViewModel vm)
            => vm.SleepStage == V2SleepStage.RemActiveSleep || vm.SleepStage == V2SleepStage.NremDeepSleep;

        private void DrawPortraitEvent(V2PlayViewModel vm)
        {
            Panel(new Rect(48, 920, 984, 250));
            GUI.Label(new Rect(82, 950, 900, 42), !vm.CauseResolved ? $"결정까지 {UpdateDecisionTimer(vm)}초" : "방금 일어난 일", _caption);
            string title = vm.CauseResolved ? "아기의 숨소리를 지켜보고 있어요." : "왜 깼는지 먼저 살펴보세요.";
            string detail = vm.CauseResolved ? BabyStepHint(vm) : CauseSignal(vm);
            var outcome = _lastResult?.Outcome;
            if (outcome != null)
            {
                title = outcome.Accepted ? "당신의 선택이 밤을 바꿨어요." : "아직 그 행동을 할 수 없어요.";
                detail = OutcomeDetail(vm, outcome, detail);
            }
            GUI.Label(new Rect(82, 1002, 900, 52), title, _headline);
            GUI.Label(new Rect(82, 1062, 900, 76), detail, _body);
        }

        private void DrawPortraitActions(V2PlayViewModel vm)
        {
            Panel(new Rect(0, 1200, PortraitWidth, 720), 0.98f);
            GUI.Label(new Rect(48, 1230, 500, 52), "어떻게 할까요?", _headline);
            // 세로 화면에도 가로와 같은 수면 중 시간 보내기 입력을 제공한다(Figma M_SLEEP_FAST_FORWARD).
            if (IsSleeping(vm))
            {
                var oldFastForward = GUI.enabled;
                GUI.enabled = oldFastForward && !_flow.InputLocked;
                if (GUI.Button(new Rect(560, 1212, 472, 80), "잠든 동안 조용히 시간 보내기  ›", _buttonSmall))
                {
                    _flow.FastForwardV2Sleep();
                    _lastResult = null;
                }
                GUI.enabled = oldFastForward;
            }
            DrawTab(new Rect(48, 1300, 305, 70), "살펴보기", ActionGroup.Diagnose);
            DrawTab(new Rect(388, 1300, 305, 70), "돌보기", ActionGroup.Care);
            DrawTab(new Rect(727, 1300, 305, 70), "수유 준비", ActionGroup.Feed);

            var actions = ActionsFor(_actionGroup);
            for (int i = 0; i < actions.Length; i++)
            {
                var action = vm.Actions.Find(a => a.Action == actions[i]);
                if (action == null) continue;
                int col = i % 2;
                int row = i / 2;
                var rect = new Rect(48 + col * 510, 1405 + row * 112, 474, 88);
                var oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && action.Enabled && !_flow.InputLocked;
                if (GUI.Button(rect, action.Label, _buttonSmall)) _lastResult = _flow.ActV2(action.Action);
                GUI.enabled = oldEnabled;
            }
        }

        private void DrawPortraitOverlay(OverlayViewModel overlay)
        {
            Fill(new Rect(0, 0, PortraitWidth, PortraitHeight), new Color(0, 0, 0, 0.72f));
            var box = new Rect(80, 560, 920, 680);
            Panel(box, 0.99f);
            GUI.Label(new Rect(135, 625, 810, 80), overlay.Title, Centered(_headline));
            float y = 740;
            foreach (var line in overlay.Lines)
            {
                GUI.Label(new Rect(150, y, 780, 76), line, Centered(_body));
                y += 86;
            }
            if (GUI.Button(new Rect(190, 1080, 700, 100), "계속하기", _button)) _flow.DismissOverlay();
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
            string detail = BabyStepHint(vm);
            var outcome = _lastResult?.Outcome;
            if (outcome != null)
            {
                title = outcome.Accepted ? "당신의 선택이 밤을 조금 바꿨다." : "아직 그 행동을 할 수 없어요.";
                detail = OutcomeDetail(vm, outcome, detail);
            }
            GUI.Label(new Rect(478, 856, 840, 42), title, _headline);
            GUI.Label(new Rect(478, 910, 840, 52), detail, _body);
        }

        private void DrawTab(Rect rect, string label, ActionGroup group)
        {
            GUIStyle normal = _portrait ? _buttonSmall : _tabButton;
            GUIStyle selected = _portrait ? _buttonSelected : _tabSelected;
            if (GUI.Button(rect, label, _actionGroup == group ? selected : normal)) _actionGroup = group;
        }

        private static V2ActionId[] ActionsFor(ActionGroup group)
        {
            switch (group)
            {
                case ActionGroup.Diagnose:
                    return new[] { V2ActionId.CheckDiaper, V2ActionId.CheckHungerSignals, V2ActionId.CheckEnvironment, V2ActionId.CheckMonitor, V2ActionId.CheckLimbRelaxation, V2ActionId.Hesitate };
                case ActionGroup.Care:
                    return new[] { V2ActionId.Hold, V2ActionId.Pat, V2ActionId.Pacifier, V2ActionId.ToggleNoise, V2ActionId.Laydown, V2ActionId.ChangeDiaper, V2ActionId.AdjustTemperature, V2ActionId.AdjustHumidity };
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
            Fill(new Rect(0, 0, LandscapeWidth, LandscapeHeight), new Color(0, 0, 0, 0.62f));
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
            if (_portrait) { DrawPortraitDiary(vm); return; }
            Fill(new Rect(0, 0, LandscapeWidth, LandscapeHeight), new Color(0.015f, 0.035f, 0.065f, 0.84f));
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
            string nextLabel = vm.HasNextNight ? NextNightButtonLabel(vm.NightId) : "처음부터 다시 보기";
            if (GUI.Button(new Rect(1290, 920, 520, 76), nextLabel, _button))
            {
                if (vm.HasNextNight) _flow.AdvanceFromV2Diary();
                else _flow = new GameFlowController(new SystemRandomSource(Environment.TickCount));
                _lastResult = null;
                _actionGroup = ActionGroup.Diagnose;
                _timedEncounterSequence = -1;
            }
        }

        private void DrawPortraitTitle()
        {
            Fill(new Rect(0, 0, PortraitWidth, PortraitHeight), new Color(0.01f, 0.025f, 0.05f, 0.48f));
            GUI.Label(new Rect(90, 350, 900, 130), "NOT A NAP", new GUIStyle(_title) { fontSize = 96 });
            GUI.Label(new Rect(190, 500, 700, 80), "백일의 밤", Centered(_headline));
            GUI.Label(new Rect(140, 690, 800, 160), "오늘 밤은 아빠 차례다.\n아기는 당신의 모든 선택을 기억한다.", Centered(_body));
            if (GUI.Button(new Rect(140, 1110, 800, 120), "첫째 밤 시작하기", _button)) _flow.StartGame();
            GUI.Label(new Rect(140, 1260, 800, 60), "약 5분 · 선택은 다음 밤의 규칙이 됩니다", Centered(_caption));
        }

        private void DrawPortraitSetup(SetupViewModel vm)
        {
            Fill(new Rect(0, 0, PortraitWidth, PortraitHeight), new Color(0.015f, 0.035f, 0.065f, 0.86f));
            GUI.Label(new Rect(48, 55, 750, 74), $"{vm.NightLabel} · 밤 준비", _display);
            GUI.Label(new Rect(48, 145, 984, 100), $"“{vm.TemperamentHint}”", _body);
            GUI.Label(new Rect(48, 265, 984, 60), $"가져갈 물건  {vm.SelectedCount} / {vm.Slots}", _headline);
            const float cardW = 474f;
            const float cardH = 420f;
            for (int i = 0; i < vm.Cards.Count; i++)
            {
                var card = vm.Cards[i];
                int col = i % 2;
                int row = i / 2;
                var rect = new Rect(48 + col * 510, 350 + row * 450, cardW, cardH);
                Color panel = card.Selected ? new Color(0.32f, 0.23f, 0.12f, 0.98f) : new Color(0.055f, 0.09f, 0.14f, 0.96f);
                if (card.Disabled) panel.a = 0.52f;
                Fill(rect, panel);
                Fill(new Rect(rect.x, rect.y, card.Selected ? 9 : 3, rect.height), card.Selected ? new Color(0.92f, 0.7f, 0.36f) : new Color(0.2f, 0.28f, 0.36f));
                GUI.Label(new Rect(rect.x + 24, rect.y + 24, 426, 66), card.Name, _headline);
                GUI.Label(new Rect(rect.x + 24, rect.y + 105, 426, 150), card.Desc, _body);
                GUI.Label(new Rect(rect.x + 24, rect.y + 280, 426, 100), $"주의 · {card.Side}", _caption);
                var oldEnabled = GUI.enabled;
                GUI.enabled = !card.Disabled;
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) _flow.ToggleV2Item(card.Id);
                GUI.enabled = oldEnabled;
            }
            // 후속 해금 후보 3종은 Figma M_UNLOCK_CANDIDATES와 동일하게 전부 노출한다(선택 불가).
            DrawLockedCandidate(new Rect(48, 1240, 984, 150), "옆잠베개", "월령과 제품별 안전 조건 확인 후 해금됩니다.");
            DrawLockedCandidate(new Rect(48, 1400, 984, 150), "수면 포지셔너", "안전 수면 자세 지침 확인 후 해금됩니다.");
            DrawLockedCandidate(new Rect(48, 1560, 984, 150), "토닥이인형", "사용 환경과 안전 기준 확인 후 해금됩니다.");
            var previous = GUI.enabled;
            GUI.enabled = vm.CanStart;
            if (GUI.Button(new Rect(100, 1740, 880, 120), vm.CanStart ? "이 준비로 밤 시작하기 →" : $"물건을 {vm.Slots}개 골라주세요", _button)) _flow.ConfirmV2Setup();
            GUI.enabled = previous;
        }

        private void DrawPortraitDiary(V2DiaryViewModel vm)
        {
            Fill(new Rect(0, 0, PortraitWidth, PortraitHeight), new Color(0.015f, 0.035f, 0.065f, 0.9f));
            GUI.Label(new Rect(60, 70, 960, 80), $"{vm.NightLabel} · 밤의 기록", _display);
            Panel(new Rect(60, 210, 960, 620));
            GUI.Label(new Rect(110, 260, 860, 48), "오늘 밤의 등급", _caption);
            GUI.Label(new Rect(110, 320, 860, 190), vm.Grade.ToString(), new GUIStyle(_title) { fontSize = 150 });
            GUI.Label(new Rect(110, 550, 860, 60), $"최장 연속 수면  {FormatDuration(vm.LongestSleepStretchMinutes)}", _headline);
            GUI.Label(new Rect(110, 640, 860, 120), $"총 수면 {FormatDuration(vm.TotalSleepMinutes)} · 깨어남 {vm.WakeCount}회\n남은 체력 {vm.ParentStaminaAtDawn:0}", _body);
            Panel(new Rect(60, 880, 960, 550));
            GUI.Label(new Rect(110, 930, 860, 48), "육아일지", _caption);
            GUI.Label(new Rect(110, 1010, 860, 180), "완벽하게 재우는 밤은 없었다.\n그래도 우리 가족이 계속할 수 있는 방법을 조금 배웠다.", _headline);
            GUI.Label(new Rect(110, 1240, 860, 100), "오늘의 선택은 다음 밤에 돌아옵니다.", _body);
            string nextLabel = vm.HasNextNight ? NextNightButtonLabel(vm.NightId) : "처음부터 다시 보기";
            if (GUI.Button(new Rect(100, 1600, 880, 120), nextLabel, _button))
            {
                if (vm.HasNextNight) _flow.AdvanceFromV2Diary();
                else _flow = new GameFlowController(new SystemRandomSource(Environment.TickCount));
                _lastResult = null;
                _actionGroup = ActionGroup.Diagnose;
                _timedEncounterSequence = -1;
                _actionEncounterSequence = -1;
            }
        }

        private static string NextNightButtonLabel(NightId night)
            => night == NightId.FirstNight ? "둘째 밤 준비하기 →" : "백일째 밤 준비하기 →";

        private static string OutcomeDetail(V2PlayViewModel vm, V2ActionOutcome outcome, string fallback)
        {
            if (outcome.BlockReason == V2ActionBlockReason.BabyNotHeld)
                return "아기는 이미 침대에 있어요. 먼저 품에 안아주세요.";
            if (outcome.BlockReason == V2ActionBlockReason.BabyNotAsleep)
                return "아직 잠들지 않았어요. 먼저 충분히 달래주세요.";
            if (outcome.BlockReason == V2ActionBlockReason.ItemUnavailable)
                return "이 물건을 가져오지 않아 사용할 수 없어요.";
            if (outcome.Action == V2ActionId.CheckHungerSignals)
            {
                switch (outcome.HungerSignalStage)
                {
                    case HungerSignalStage.Late: return "입을 찾고 빠르게 숨 쉬며 배고픈 울음을 내요. 수유가 필요해요.";
                    case HungerSignalStage.Active: return "고개를 돌리고 보호자 쪽으로 몸을 기울여요. 배고픔 신호예요.";
                    case HungerSignalStage.Early: return "입맛을 다시고 손을 빨아요. 초기 배고픔 신호예요.";
                    default: return "지금은 배고픔 신호가 보이지 않아요.";
                }
            }
            if (outcome.Action == V2ActionId.CheckEnvironment)
                return $"온도 {vm.TemperatureCelsius:0.#}°C · 습도 {vm.HumidityPercent:0.#}% (권장 20–22°C · 40–60%)";
            if (outcome.Action == V2ActionId.AdjustTemperature)
                return $"온도를 {vm.TemperatureCelsius:0.#}°C로 조절했어요.";
            if (outcome.Action == V2ActionId.AdjustHumidity)
                return $"습도를 {vm.HumidityPercent:0.#}%로 조절했어요.";
            if (outcome.MonitorRead)
                return $"울음 {vm.CryIntensity:0} · 진정 {vm.Calm:0} · 허기 {vm.Hunger:0}";
            if (outcome.Action == V2ActionId.ToggleNoise)
                return vm.NoiseOn ? "백색소음기를 켰어요." : "백색소음기를 껐어요.";
            if (outcome.ObservedSignals.Count > 0)
                return "관찰 · " + PresentationCopyMapper.ObservationLabel(outcome.ObservedSignals[0]);
            if (outcome.MissingPreparationSteps.Count > 0)
                return "먼저 필요함 · " + PresentationCopyMapper.FeedingStepLabel(outcome.MissingPreparationSteps[0]);
            if (outcome.ConsumedTime)
                return $"{outcome.TimeDeltaMinutes}분이 흘렀고, 체력이 {outcome.StaminaDelta:+0;-0;0} 변했어요.";
            return fallback;
        }

        private static string CauseSignal(V2PlayViewModel vm)
            => vm.RevealedCause.HasValue ? $"확인된 원인\n{PresentationCopyMapper.WakeCauseLabel(vm.RevealedCause.Value)}" : "왜 깼는지 아직 몰라요.\n먼저 작은 신호부터 살펴보세요.";

        private static string SleepSignal(V2PlayViewModel vm)
        {
            if (!vm.CauseResolved) return CauseSignal(vm);
            if (vm.SleepStage == V2SleepStage.RemActiveSleep)
                return "활동 수면이에요.\n아직 눕히기보다 기다려주세요.";
            if (vm.SleepStage == V2SleepStage.NremDeepSleep && !vm.DeepSleepObserved)
                return "깊은 수면이에요.\n팔다리 이완을 확인해보세요.";
            if (vm.SleepStage == V2SleepStage.NremDeepSleep)
                return "팔다리 힘이 빠졌어요.\n이제 눕히기를 시도할 수 있어요.";
            if (vm.CryIntensity > 45) return "울음이 커지고 있어요.\n자극을 줄이고 천천히 반응하세요.";
            if (vm.Calm < vm.DrowsyCalmThreshold)
                return $"진정도 {vm.Calm:0} / {vm.SleepStartCalmThreshold:0}\n안기나 토닥이기로 달래주세요.";
            return $"진정도 {vm.Calm:0} / {vm.SleepStartCalmThreshold:0}\n한 번만 더 차분히 달래주세요.";
        }

        private static string BabyStepHint(V2PlayViewModel vm)
        {
            if (!vm.CauseResolved) return "먼저 깨어난 원인을 확인해주세요";
            if (vm.SleepStage == V2SleepStage.RemActiveSleep) return "활동 수면 · 아직 눕히기엔 일러요";
            if (vm.SleepStage == V2SleepStage.NremDeepSleep && !vm.DeepSleepObserved) return "깊은 수면 · 팔다리 이완을 확인하세요";
            if (vm.SleepStage == V2SleepStage.NremDeepSleep) return "깊은 수면 확인 · 이제 눕혀도 좋아요";
            if (vm.Calm < vm.DrowsyCalmThreshold)
                return $"진정도 {vm.Calm:0} / {vm.SleepStartCalmThreshold:0} · 안기 또는 토닥이기";
            return $"진정도 {vm.Calm:0} / {vm.SleepStartCalmThreshold:0} · 한 번 더 달래주세요";
        }

        private static string BabyStateHeadline(V2PlayViewModel vm)
        {
            switch (vm.SleepStage)
            {
                case V2SleepStage.Drowsy:
                    return "눈이 반쯤 감기고 움직임이 줄었다";
                case V2SleepStage.RemActiveSleep:
                    return "눈꺼풀이 떨리고 손끝이 가끔 움직인다";
                case V2SleepStage.NremDeepSleep:
                    return vm.DeepSleepObserved
                        ? "팔다리 힘이 빠지고 깊이 잠들었다"
                        : "호흡이 고르고 몸의 긴장이 풀린다";
                default:
                    if (vm.CryIntensity > 35) return "얼굴이 붉어지고 울음이 커졌다";
                    if (vm.CryIntensity > 0) return "조금 불편한 듯 몸을 꼼지락거린다";
                    return "울지 않고 조용히 주변을 본다";
            }
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
