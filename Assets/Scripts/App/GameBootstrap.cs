using System;
using System.Collections.Generic;
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
        private GameFeelAudio _audio;
        private V2PresentationActionResult _lastResult;
        private HomeMoveOutcome _lastMove;
        private int _timedEncounterSequence = -1;
        private float _decisionDeadline;
        private bool _timeoutSent;
        private ActionGroup _actionGroup = ActionGroup.Diagnose;
        private int _actionEncounterSequence = -1;
        private bool _portrait;
        private float _nextKeyboardMoveAt;
        private bool _directHintSeen;
        private float _directCueHiddenUntil = -10f;
        private V2ActionId? _roomObjectAction;
        private float _roomObjectAnimationStarted = -10f;

        private Font _font;
        private Texture2D _room;
        private Texture2D _kitchenRoom;
        private Texture2D _bathroomRoom;
        private Texture2D _formulaTinArt;
        private Texture2D _feedingBottleArt;
        private Texture2D _coolingBasinArt;
        private Texture2D _introBabyArt;
        private Texture2D _lyingSleepArt;
        private Texture2D _geneticMonolidStraight;
        private Texture2D _geneticMonolidCurly;
        private Texture2D _geneticDoubleStraight;
        private readonly Dictionary<V2ActionId, Texture2D[]> _interactionFrames =
            new Dictionary<V2ActionId, Texture2D[]>();
        private Texture2D[] _carrierBabyFrames;
        private readonly Dictionary<ItemId, Texture2D> _itemArt = new Dictionary<ItemId, Texture2D>();
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
        private Texture2D _lockIcon;
        private Texture2D _itemGlow;
        private Texture2D _itemShadow;
        private Texture2D _caregiverHand;
        private Texture2D _diaperCloth;
        private ItemId? _setupFocus;
        private int _introBeat;
        private bool _dadDoubleEyelid = true;
        private bool _momDoubleEyelid;
        private bool _dadCurlyHair = true;
        private bool _momCurlyHair;
        private bool _dadBigMouth;
        private bool _momBigMouth = true;
        private bool _babyDoubleEyelid;
        private bool _babyCurlyHair;
        private bool _babyBigMouth;
        private int _babyVoiceVariant;
        private int _familyRollCount;
        private bool _familyRolled;
        private string _babyNameInput = "";

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
        private ScreenState _observedScreen = ScreenState.Title;
        private float _screenTransitionStarted = -10f;
        private float _impactStarted = -10f;
        private float _impactDuration = 0.28f;
        private float _impactStrength;
        private Color _impactColor = Color.clear;
        private V2ActionId? _animatedAction;
        private float _actionAnimationStarted = -10f;
        private const float DefaultActionAnimationDuration = 1.6f;
        private float _roomTransitionStarted = -10f;
        private HomeLocation _roomTransitionFrom;
        private HomeLocation _roomTransitionTo;
        private bool _roomTransitionBabyAccompanied;
        private const float RoomTransitionDuration = 0.5f;

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
            _audio = GameFeelAudio.Attach(gameObject);
            _room = Resources.Load<Texture2D>("Art/nursery-night-empty");
            _kitchenRoom = Resources.Load<Texture2D>("Art/kitchen-night");
            _bathroomRoom = Resources.Load<Texture2D>("Art/bathroom-night");
            _formulaTinArt = Resources.Load<Texture2D>("Art/Kitchen/formula-tin");
            _feedingBottleArt = Resources.Load<Texture2D>("Art/Kitchen/feeding-bottle");
            _coolingBasinArt = Resources.Load<Texture2D>("Art/Kitchen/cooling-basin");
            _introBabyArt = Resources.Load<Texture2D>("Art/Baby/cry_hard");
            _lyingSleepArt = Resources.Load<Texture2D>("Art/Baby/Interaction/lying_sleep");
            _geneticMonolidStraight = Resources.Load<Texture2D>("Art/Baby/Genetics/monolid_straight");
            _geneticMonolidCurly = Resources.Load<Texture2D>("Art/Baby/Genetics/monolid_curly");
            _geneticDoubleStraight = Resources.Load<Texture2D>("Art/Baby/Genetics/double_straight");
            _carrierBabyFrames = LoadFrameSet("carrier", 4);
            _interactionFrames[V2ActionId.Pat] = LoadFrameSet("pat", 4);
            _interactionFrames[V2ActionId.Hold] = LoadFrameSet("hold", 4);
            _interactionFrames[V2ActionId.ToggleCarrier] = _carrierBabyFrames;
            _interactionFrames[V2ActionId.FeedPreparedBottle] = LoadFrameSet("feed", 4);
            _interactionFrames[V2ActionId.Pacifier] = LoadFrameSet("pacifier", 4);
            _interactionFrames[V2ActionId.CheckDiaper] = LoadSingleFrame("diaper_check");
            _interactionFrames[V2ActionId.ChangeDiaper] = LoadSingleFrame("diaper_change");
            _interactionFrames[V2ActionId.CheckLimbRelaxation] = LoadSingleFrame("limb_check");
            _interactionFrames[V2ActionId.CheckBodyTemperature] = LoadSingleFrame("temperature_check");
            LoadItemArt(ItemId.Carrier, "carrier");
            LoadItemArt(ItemId.Pacifier, "pacifier");
            LoadItemArt(ItemId.Noise, "noise");
            LoadItemArt(ItemId.Monitor, "monitor");
            _ambientRandom = new System.Random(Environment.TickCount ^ GetInstanceID());
            _nextAmbientMotionAt = Time.unscaledTime + RandomRange(0.4f, 1.4f);
            _nextBabbleAt = Time.unscaledTime + RandomRange(1.8f, 4.5f);
        }

        private static Texture2D[] LoadFrameSet(string name, int count)
        {
            var frames = new Texture2D[count];
            for (int i = 0; i < count; i++)
                frames[i] = Resources.Load<Texture2D>($"Art/Baby/Interaction/{name}_{i}");
            return frames;
        }

        private static Texture2D[] LoadSingleFrame(string name)
            => new[] { Resources.Load<Texture2D>($"Art/Baby/Interaction/{name}") };

        private void EnsureStyles()
        {
            if (_display != null) return;
            _font = Resources.Load<Font>("Fonts/NotoSansKR");

            _display = LabelStyle(52, FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f), TextAnchor.MiddleLeft);
            _headline = LabelStyle(34, FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f));
            _title = LabelStyle(82, FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f), TextAnchor.MiddleCenter);
            _body = LabelStyle(26, FontStyle.Normal, new Color(0.88f, 0.9f, 0.92f));
            _caption = LabelStyle(20, FontStyle.Bold, new Color(0.74f, 0.79f, 0.84f));

            _button = ButtonStyle(28, new Color(0.09f, 0.14f, 0.21f, 0.98f), new Color(0.91f, 0.72f, 0.42f), new Color(0.97f, 0.94f, 0.87f));
            _buttonSmall = ButtonStyle(23, new Color(0.07f, 0.11f, 0.17f, 0.72f), new Color(0.28f, 0.36f, 0.45f, 0.9f), new Color(0.82f, 0.85f, 0.88f));
            _buttonSelected = ButtonStyle(23, new Color(0.78f, 0.54f, 0.23f, 0.98f), new Color(0.95f, 0.76f, 0.44f), Color.white);
            _tabButton = ButtonStyle(18, new Color(0.07f, 0.11f, 0.17f, 0.68f), new Color(0.28f, 0.36f, 0.45f, 0.9f), new Color(0.82f, 0.85f, 0.88f));
            _tabSelected = ButtonStyle(18, new Color(0.78f, 0.54f, 0.23f, 0.98f), new Color(0.95f, 0.76f, 0.44f), Color.white);
            _speechBubble = RoundedTexture(new Color(0.97f, 0.94f, 0.87f, 0.98f), 14);
            _itemGlow = RoundedTexture(new Color(1f, 0.69f, 0.27f, 0.2f), 48);
            _itemShadow = RoundedTexture(new Color(0f, 0f, 0f, 0.44f), 28);
            _caregiverHand = Resources.Load<Texture2D>("Art/Caregiver/reaching-hand");
            _diaperCloth = RoundedTexture(new Color(0.97f, 0.92f, 0.77f, 0.98f), 18);
        }

        private GUIStyle LabelStyle(int size, FontStyle weight, Color color, TextAnchor align = TextAnchor.UpperLeft)
            => new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = size,
                fontStyle = weight,
                alignment = align,
                wordWrap = true,
                clipping = TextClipping.Overflow,
                normal = { textColor = color }
            };

        private GUIStyle OverlayLabelStyle(int size, FontStyle weight, Color color,
            TextAnchor align = TextAnchor.MiddleLeft, bool wordWrap = false)
        {
            var style = LabelStyle(size, weight, color, align);
            style.wordWrap = wordWrap;
            style.clipping = TextClipping.Overflow;
            return style;
        }

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

        // 안전 검토 전 후보 아이템은 실제 제품처럼 보이면 안 되므로, 그림 대신
        // 중립적인 자물쇠 아이콘을 절차적으로 그려 카드가 비어 보이지 않게 한다.
        private Texture2D LockIcon()
        {
            if (_lockIcon != null) return _lockIcon;
            const int size = 64;
            var texture = new Texture2D(size, size) { hideFlags = HideFlags.HideAndDontSave };
            var metal = new Color(0.82f, 0.72f, 0.5f, 1f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color c = Color.clear;
                // 자물쇠 몸통(아래쪽 둥근 사각형).
                if (x >= 16 && x <= 48 && y >= 6 && y <= 34) c = metal;
                // 걸쇠(위쪽 반원 고리).
                float dx = x - 32f, dy = y - 34f;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (y >= 34 && dist >= 10f && dist <= 15f) c = metal;
                // 열쇠 구멍(몸통 중앙 투명).
                float kx = x - 32f, ky = y - 22f;
                if (kx * kx + ky * ky <= 9f) c = Color.clear;
                texture.SetPixel(x, y, c);
            }
            texture.Apply();
            _lockIcon = texture;
            return _lockIcon;
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
            // 9:16보다 긴 모바일 화면에서도 Unity 기본색 막대가 드러나지 않게
            // 실제 화면 전체를 먼저 배경으로 채운 뒤 기준 해상도 UI를 얹는다.
            Color oldColor = GUI.color;
            GUI.color = Color.white;
            if (_room != null)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _room, ScaleMode.ScaleAndCrop);
            else
            {
                GUI.color = new Color(0.01f, 0.025f, 0.05f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
                    Texture2D.whiteTexture, ScaleMode.StretchToFill);
            }
            GUI.color = oldColor;
            _portrait = Screen.height > Screen.width * 1.15f;
            ApplyResponsiveTypography();
            float referenceWidth = _portrait ? PortraitWidth : LandscapeWidth;
            float referenceHeight = _portrait ? PortraitHeight : LandscapeHeight;
            float scale = Mathf.Min(Screen.width / referenceWidth, Screen.height / referenceHeight);
            float offsetX = (Screen.width - referenceWidth * scale) * 0.5f;
            float offsetY = (Screen.height - referenceHeight * scale) * 0.5f;
            if (_observedScreen != _flow.Screen)
            {
                _observedScreen = _flow.Screen;
                _screenTransitionStarted = Time.unscaledTime;
                if (_observedScreen == ScreenState.Diary) _audio.PlayDawn();
                else _audio.PlayUi();
            }
            float impactProgress = Mathf.Clamp01((Time.unscaledTime - _impactStarted) / _impactDuration);
            float shake = impactProgress < 1f
                ? Mathf.Sin(impactProgress * Mathf.PI * 7f) * _impactStrength * (1f - impactProgress)
                : 0f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX + shake * scale, offsetY), Quaternion.identity,
                new Vector3(scale, scale, 1));

            DrawBackdrop();
            switch (_flow.Screen)
            {
                case ScreenState.Title: DrawTitle(); break;
                case ScreenState.FamilySetup: DrawFamilySetup(); break;
                case ScreenState.Intro: DrawIntro(); break;
                case ScreenState.Setup: DrawSetup(); break;
                case ScreenState.Play: DrawPlay(); break;
                case ScreenState.Diary: DrawDiary(); break;
                case ScreenState.Ending: DrawEnding(); break;
            }
            DrawGameFeelOverlay(referenceWidth, referenceHeight, impactProgress);
            GUI.matrix = oldMatrix;
        }

        private void DrawGameFeelOverlay(float width, float height, float impactProgress)
        {
            if (impactProgress < 1f && _impactColor.a > 0f)
            {
                var color = _impactColor;
                color.a *= (1f - impactProgress) * 0.38f;
                Fill(new Rect(0, 0, width, height), color);
            }
            float transition = Mathf.Clamp01((Time.unscaledTime - _screenTransitionStarted) / 0.48f);
            if (transition < 1f)
                Fill(new Rect(0, 0, width, height), new Color(0.005f, 0.012f, 0.024f,
                    Mathf.SmoothStep(0.72f, 0f, transition)));
        }

        private void ApplyResponsiveTypography()
        {
            _display.fontSize = _portrait ? 64 : 52;
            _headline.fontSize = _portrait ? 42 : 34;
            _body.fontSize = _portrait ? 34 : 26;
            _caption.fontSize = _portrait ? 26 : 20;
            _button.fontSize = _portrait ? 34 : 28;
            _buttonSmall.fontSize = _portrait ? 30 : 23;
            _buttonSelected.fontSize = _portrait ? 30 : 23;
            _tabButton.fontSize = _portrait ? 24 : 18;
            _tabSelected.fontSize = _portrait ? 24 : 18;
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
            Fill(new Rect(0, 0, LandscapeWidth, LandscapeHeight), new Color(0.01f, 0.02f, 0.035f, 0.24f));
            GUI.Label(new Rect(470, 174, 980, 110), "NOT A NAP", new GUIStyle(_title) { fontSize = 88 });
            GUI.Label(new Rect(650, 282, 620, 56), "백일의 밤", Centered(_headline));
            Fill(new Rect(830, 356, 260, 3), new Color(0.93f, 0.66f, 0.29f, 0.84f));
            GUI.Label(new Rect(610, 405, 700, 100),
                "오늘 밤은 아빠 차례다.\n울음보다 먼저 오는 신호를 읽어보자.", Centered(_body));
            if (DrawPrimaryButton(new Rect(750, 650, 420, 82), "우리 아기 만들기  →"))
            {
                _introBeat = 0;
                _flow.BeginFamilySetup();
            }
            GUI.Label(new Rect(650, 762, 620, 34),
                "약 5분 · 정답보다 서로의 리듬을 알아가는 밤", Centered(_caption));
        }

        private void DrawIntro()
        {
            float width = _portrait ? PortraitWidth : LandscapeWidth;
            float height = _portrait ? PortraitHeight : LandscapeHeight;
            Fill(new Rect(0, 0, width, height), new Color(0.01f, 0.015f, 0.025f, 0.48f));

            float babySize = _portrait ? 760f : 620f;
            var babyRect = _portrait
                ? new Rect(160f, 360f, babySize, babySize)
                : new Rect(650f, 170f, babySize, babySize);
            Texture2D introPortrait = _familyRolled ? GeneticBabyPortrait() : _introBabyArt;
            if (introPortrait != null)
            {
                GUI.DrawTexture(new Rect(babyRect.center.x - babySize * 0.28f,
                    babyRect.yMax - babySize * 0.12f, babySize * 0.56f, babySize * 0.09f),
                    _itemShadow, ScaleMode.StretchToFill, true);
                GUI.DrawTexture(babyRect, introPortrait, ScaleMode.ScaleToFit, true);
            }

            // 화면 아래의 두 손으로 카메라가 아빠의 눈이라는 점을 먼저 전달한다.
            if (_introBeat >= 1)
            {
                float handWidth = _portrait ? 180f : 160f;
                float handHeight = _portrait ? 136f : 120f;
                // 손은 아기에게 다가가는 시선 영역에만 둔다. 카피·CTA 안전 영역 침범 금지.
                float handY = _portrait ? 890f : 650f;
                float handInset = _portrait ? 82f : 420f;
                DrawCaregiverHand(new Rect(handInset, handY, handWidth, handHeight));
                DrawCaregiverHand(new Rect(width - handWidth - handInset, handY,
                    handWidth, handHeight), true);
            }

            string eyebrow;
            string title;
            string body;
            switch (_introBeat)
            {
                case 0:
                    eyebrow = "21:00 · 불이 꺼진 뒤";
                    title = $"{_flow.BabyName}의 작은 울음에 눈을 떴다";
                    body = "방 건너편에서 짧은 울음이 들린다.\n오늘 밤은 내가 먼저 가보기로 했다.";
                    break;
                case 1:
                    eyebrow = "아빠의 시점";
                    title = $"{_flow.BabyName}, 문을 열자 나를 찾는다";
                    body = "정답을 고르는 대신 표정과 입, 손, 호흡을 직접 살핀다.\n내 손으로 안고 토닥이며 아기의 대답을 기다린다.";
                    break;
                default:
                    eyebrow = "첫째 밤";
                    title = $"{_flow.BabyName}의 울음보다 먼저 오는 신호를 읽어보자";
                    body = "완벽하게 재우는 밤이 아니라,\n우리 가족이 계속 이어갈 수 있는 리듬을 만드는 밤이다.";
                    break;
            }

            var copyPanel = _portrait
                ? new Rect(72f, 1120f, 936f, 410f)
                : new Rect(120f, 190f, 530f, 480f);
            DrawGlassPanel(copyPanel, 0.82f);
            GUI.Label(new Rect(copyPanel.x + 38f, copyPanel.y + 32f,
                copyPanel.width - 76f, 42f), eyebrow,
                OverlayLabelStyle(_portrait ? 25 : 20, FontStyle.Bold,
                    new Color(0.96f, 0.69f, 0.31f)));
            GUI.Label(new Rect(copyPanel.x + 38f, copyPanel.y + 92f,
                copyPanel.width - 76f, _portrait ? 120f : 110f), title,
                OverlayLabelStyle(_portrait ? 42 : 34, FontStyle.Bold,
                    Color.white, TextAnchor.UpperLeft));
            GUI.Label(new Rect(copyPanel.x + 38f, copyPanel.y + (_portrait ? 228f : 215f),
                copyPanel.width - 76f, 140f), body,
                OverlayLabelStyle(_portrait ? 29 : 23, FontStyle.Normal,
                    new Color(0.9f, 0.92f, 0.94f), TextAnchor.UpperLeft));

            var nextRect = _portrait
                ? new Rect(140f, 1605f, 800f, 118f)
                : new Rect(1180f, 820f, 560f, 82f);
            string next = _introBeat < 2 ? "한 걸음 가까이  →" : "오늘 밤 준비하기  →";
            if (DrawPrimaryButton(nextRect, next))
            {
                _audio?.PlayUi();
                if (_introBeat < 2) _introBeat++;
                else _flow.CompleteIntro();
            }

            var skipRect = _portrait
                ? new Rect(390f, 1750f, 300f, 64f)
                : new Rect(1480f, 920f, 260f, 48f);
            if (GUI.Button(skipRect, "인트로 건너뛰기", _buttonSmall))
                _flow.CompleteIntro();
        }

        private void DrawFamilySetup()
        {
            float width = _portrait ? PortraitWidth : LandscapeWidth;
            float height = _portrait ? PortraitHeight : LandscapeHeight;
            Fill(new Rect(0, 0, width, height), new Color(0.01f, 0.02f, 0.035f, 0.52f));
            GUI.Label(_portrait
                    ? new Rect(60f, 55f, 960f, 78f)
                    : new Rect(90f, 50f, 1000f, 64f),
                "우리 가족을 닮은 아기", _display);
            GUI.Label(_portrait
                    ? new Rect(60f, 130f, 960f, 60f)
                    : new Rect(90f, 112f, 1250f, 44f),
                "아빠와 엄마의 외형을 고른 뒤 무료로 뽑아보세요 · 능력치에는 영향 없음",
                _caption);

            if (_portrait)
            {
                DrawParentTraitPanel(new Rect(48f, 220f, 474f, 390f), "아빠",
                    ref _dadDoubleEyelid, ref _dadCurlyHair, ref _dadBigMouth);
                DrawParentTraitPanel(new Rect(558f, 220f, 474f, 390f), "엄마",
                    ref _momDoubleEyelid, ref _momCurlyHair, ref _momBigMouth);
                DrawBabyGachaResult(new Rect(240f, 650f, 600f, 650f));
                DrawBabyNameInput(new Rect(240f, 1290f, 600f, 64f));
                if (DrawPrimaryButton(new Rect(110f, 1370f, 860f, 115f),
                    _familyRolled ? "다시 뽑기" : "가족 특징 섞어 아기 뽑기"))
                    RollFamilyBaby();
                if (DrawPrimaryButton(new Rect(110f, 1520f, 860f, 115f),
                    "이 이름으로 시작하기  →", _familyRolled && HasBabyName()))
                {
                    _flow.SetBabyName(_babyNameInput);
                    _introBeat = 0;
                    _flow.BeginIntro();
                }
            }
            else
            {
                DrawParentTraitPanel(new Rect(90f, 200f, 480f, 420f), "아빠",
                    ref _dadDoubleEyelid, ref _dadCurlyHair, ref _dadBigMouth);
                DrawParentTraitPanel(new Rect(600f, 200f, 480f, 420f), "엄마",
                    ref _momDoubleEyelid, ref _momCurlyHair, ref _momBigMouth);
                DrawBabyGachaResult(new Rect(1120f, 165f, 610f, 650f));
                DrawBabyNameInput(new Rect(1120f, 720f, 610f, 64f));
                if (DrawPrimaryButton(new Rect(160f, 720f, 420f, 82f),
                    _familyRolled ? "다시 뽑기" : "아기 뽑기"))
                    RollFamilyBaby();
                if (DrawPrimaryButton(new Rect(610f, 720f, 470f, 82f),
                    "이 이름으로 시작하기  →", _familyRolled && HasBabyName()))
                {
                    _flow.SetBabyName(_babyNameInput);
                    _introBeat = 0;
                    _flow.BeginIntro();
                }
            }
        }

        private bool HasBabyName() => !string.IsNullOrWhiteSpace(_babyNameInput);

        private void DrawBabyNameInput(Rect rect)
        {
            DrawGlassPanel(rect, .84f);
            float labelWidth = _portrait ? 150f : 135f;
            GUI.Label(new Rect(rect.x + 18f, rect.y, labelWidth, rect.height), "아기 이름",
                OverlayLabelStyle(_portrait ? 22 : 18, FontStyle.Bold,
                    new Color(1f, .84f, .6f), TextAnchor.MiddleLeft));
            var input = new Rect(rect.x + labelWidth + 18f, rect.y + 9f,
                rect.width - labelWidth - 36f, rect.height - 18f);
            var style = new GUIStyle(GUI.skin.textField)
            {
                fontSize = _portrait ? 25 : 21,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 14, 4, 4)
            };
            _babyNameInput = GUI.TextField(input, _babyNameInput ?? "", 8, style);
            if (!HasBabyName() && Event.current.type == EventType.Repaint)
                GUI.Label(input, "이름을 입력해 주세요", OverlayLabelStyle(_portrait ? 20 : 17,
                    FontStyle.Normal, new Color(.7f, .74f, .78f), TextAnchor.MiddleLeft));
        }

        private void DrawParentTraitPanel(Rect rect, string title,
            ref bool doubleEyelid, ref bool curlyHair, ref bool bigMouth)
        {
            DrawGlassPanel(rect, 0.76f);
            GUI.Label(new Rect(rect.x + 28f, rect.y + 22f, rect.width - 56f, 48f),
                title, OverlayLabelStyle(_portrait ? 31 : 27, FontStyle.Bold,
                    new Color(1f, 0.85f, 0.62f)));
            float gap = 14f;
            float choiceWidth = (rect.width - 56f - gap) * 0.5f;
            float firstLabelY = rect.y + 72f;
            float rowStep = _portrait ? 98f : 106f;
            float buttonHeight = _portrait ? 54f : 60f;
            DrawBinaryTraitRow(firstLabelY, "눈매", "무쌍", "유쌍",
                rect, choiceWidth, gap, buttonHeight, ref doubleEyelid);
            DrawBinaryTraitRow(firstLabelY + rowStep, "머리결", "직모", "곱슬",
                rect, choiceWidth, gap, buttonHeight, ref curlyHair);
            DrawBinaryTraitRow(firstLabelY + rowStep * 2f, "입 크기", "작은 입", "큰 입",
                rect, choiceWidth, gap, buttonHeight, ref bigMouth);
        }

        private void DrawBinaryTraitRow(float labelY, string label, string left, string right,
            Rect panel, float choiceWidth, float gap, float buttonHeight, ref bool rightSelected)
        {
            GUI.Label(new Rect(panel.x + 28f, labelY, panel.width - 56f, 28f), label, _caption);
            float buttonY = labelY + 30f;
            if (DrawChoiceButton(new Rect(panel.x + 28f, buttonY, choiceWidth, buttonHeight),
                    left, !rightSelected))
            {
                rightSelected = false;
                _familyRolled = false;
            }
            if (DrawChoiceButton(new Rect(panel.x + 28f + choiceWidth + gap, buttonY,
                    choiceWidth, buttonHeight), right, rightSelected))
            {
                rightSelected = true;
                _familyRolled = false;
            }
        }

        private void DrawBabyGachaResult(Rect area)
        {
            if (!_familyRolled)
            {
                DrawGlassPanel(new Rect(area.x + 45f, area.y + 80f, area.width - 90f, 390f), 0.5f);
                GUI.Label(new Rect(area.x + 70f, area.y + 195f, area.width - 140f, 110f),
                    "아직 만나지 못한\n우리 아기", Centered(_headline));
                return;
            }

            Texture2D portrait = GeneticBabyPortrait();
            float artSize = Mathf.Min(area.width, _portrait ? 480f : 430f);
            var artRect = new Rect(area.center.x - artSize * 0.5f, area.y, artSize, artSize);
            GUI.DrawTexture(new Rect(artRect.center.x - artSize * 0.3f,
                artRect.yMax - 26f, artSize * 0.6f, 28f),
                _itemShadow, ScaleMode.StretchToFill, true);
            if (portrait != null) GUI.DrawTexture(artRect, portrait, ScaleMode.ScaleToFit, true);
            string eyeSource = TraitSource(_babyDoubleEyelid, _dadDoubleEyelid, _momDoubleEyelid);
            string hairSource = TraitSource(_babyCurlyHair, _dadCurlyHair, _momCurlyHair);
            string mouthSource = TraitSource(_babyBigMouth, _dadBigMouth, _momBigMouth);
            var resultPanel = new Rect(area.x + 20f, area.y + artSize - 5f,
                area.width - 40f, 155f);
            DrawGlassPanel(resultPanel, 0.82f);
            GUI.Label(new Rect(resultPanel.x + 24f, resultPanel.y + 14f,
                    resultPanel.width - 48f, 42f),
                $"{(_babyDoubleEyelid ? "유쌍" : "무쌍")} · {(_babyCurlyHair ? "곱슬" : "직모")} · {(_babyBigMouth ? "큰 입" : "작은 입")}",
                OverlayLabelStyle(_portrait ? 27 : 23, FontStyle.Bold,
                    Color.white, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(resultPanel.x + 24f, resultPanel.y + 62f,
                    resultPanel.width - 48f, 70f),
                $"눈매는 {eyeSource}, 머리결은 {hairSource}, 입은 {mouthSource}를 닮았어요",
                OverlayLabelStyle(_portrait ? 21 : 18, FontStyle.Normal,
                    new Color(0.86f, 0.9f, 0.94f), TextAnchor.MiddleCenter));
        }

        private void RollFamilyBaby()
        {
            _familyRollCount++;
            int seed = _familyRollCount * 1103515245 + 12345;
            _babyDoubleEyelid = (seed & 1) == 0 ? _dadDoubleEyelid : _momDoubleEyelid;
            _babyCurlyHair = (seed & 2) == 0 ? _dadCurlyHair : _momCurlyHair;
            _babyBigMouth = (seed & 4) == 0 ? _dadBigMouth : _momBigMouth;
            _babyVoiceVariant = Mathf.Abs(seed >> 3) % 3;
            _familyRolled = true;
            _audio?.SetBabyVoiceVariant(_babyVoiceVariant);
            _audio?.PlayUi();
        }

        private Texture2D GeneticBabyPortrait()
        {
            if (_babyDoubleEyelid && _babyCurlyHair)
                return Resources.Load<Texture2D>("Art/Baby/awake_calm");
            if (_babyDoubleEyelid) return _geneticDoubleStraight;
            return _babyCurlyHair ? _geneticMonolidCurly : _geneticMonolidStraight;
        }

        private static string TraitSource(bool babyTrait, bool dadTrait, bool momTrait)
        {
            if (dadTrait == momTrait)
                return "아빠와 엄마";

            return babyTrait == dadTrait ? "아빠" : "엄마";
        }

        private void DrawSetup()
        {
            var vm = _flow.BuildV2Setup();
            if (_portrait) { DrawPortraitSetup(vm); return; }
            Fill(new Rect(0, 0, LandscapeWidth, LandscapeHeight), new Color(0.01f, 0.02f, 0.035f, 0.34f));
            GUI.Label(new Rect(90, 64, 900, 56), $"{vm.NightLabel}  ·  밤 준비", _display);
            GUI.Label(new Rect(1360, 64, 460, 64), $"가져갈 물건  {vm.SelectedCount} / {vm.Slots}",
                OverlayLabelStyle(34, FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f),
                    TextAnchor.MiddleRight));
            if (vm.IsFirstNight)
                DrawCarePairSetup(vm, false);
            else
            {
                GUI.Label(new Rect(92, 126, 1500, 82), $"{vm.NightRoleTitle} · {vm.NightRoleSummary}", _headline);
                for (int i = 0; i < vm.RhythmCards.Count && i < 2; i++)
                {
                    var rhythm = vm.RhythmCards[i];
                    GUI.Label(new Rect(92, 220 + i * 62, 1700, 58),
                        $"{rhythm.PreviousChoice} · 도움: {rhythm.Help} · 부담: {rhythm.Burden}", _caption);
                }
            }

            const float displayWidth = 400f;
            const float gap = 48f;
            for (int i = 0; i < vm.Cards.Count; i++)
            {
                var card = vm.Cards[i];
                var rect = new Rect(88 + i * (displayWidth + gap), 350, displayWidth, 390);
                DrawCollectibleItem(rect, card, false);
            }

            var focused = FocusedSetupCard(vm);
            if (focused != null)
                DrawSetupItemDetail(new Rect(130, 758, 1120, 184), focused, false);
            GUI.Label(new Rect(130, 952, 940, 42), "소품을 눌러 오늘 밤의 진열대에 올리세요.",
                OverlayLabelStyle(20, FontStyle.Bold, new Color(0.74f, 0.79f, 0.84f)));

            string next = vm.CanStart ? "이 준비로 밤 시작하기  →" : $"물건을 {vm.Slots}개 골라주세요";
            if (DrawPrimaryButton(new Rect(1300, 900, 520, 82), next, vm.CanStart))
            {
                _audio?.PlayUi();
                _flow.ConfirmV2Setup();
                _lastMove = null;
            }
        }

        private void DrawCollectibleItem(Rect area, ItemCardViewModel card, bool portrait)
        {
            bool hovered = area.Contains(Event.current.mousePosition);
            if (hovered) _setupFocus = card.Id;
            float artSize = portrait ? 270f : 276f;
            float lift = card.Selected
                ? 15f + Mathf.Sin(Time.unscaledTime * 4.5f) * 3f
                : hovered && !card.Disabled ? 9f : 0f;
            float centerX = area.x + area.width * 0.5f;
            var artRect = new Rect(centerX - artSize * 0.5f, area.y + 30f - lift, artSize, artSize);
            var shadowRect = new Rect(centerX - artSize * 0.34f, area.y + 276f, artSize * 0.68f, portrait ? 34f : 28f);

            if (card.Selected)
                GUI.DrawTexture(new Rect(centerX - artSize * 0.55f, area.y + 5f - lift,
                    artSize * 1.1f, artSize * 1.1f), _itemGlow, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(shadowRect, _itemShadow, ScaleMode.StretchToFill, true);

            Color oldColor = GUI.color;
            if (card.Disabled) GUI.color = new Color(0.62f, 0.65f, 0.69f, 0.52f);
            else if (hovered || card.Selected) GUI.color = new Color(1.08f, 1.04f, 0.96f);
            DrawItemArt(card.Id, artRect);
            GUI.color = oldColor;

            if (card.Selected)
            {
                DrawItemSparkle(area.x + 48f, area.y + 58f, portrait ? 18f : 15f);
                DrawItemSparkle(area.xMax - 52f, area.y + 136f, portrait ? 14f : 12f);
            }

            var nameStyle = new GUIStyle(_headline)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = portrait ? 32 : 30,
                normal = { textColor = card.Selected
                    ? new Color(1f, 0.83f, 0.5f)
                    : new Color(0.96f, 0.93f, 0.86f) }
            };
            nameStyle.clipping = TextClipping.Overflow;
            GUI.Label(new Rect(area.x, area.y + 298, area.width, 64), card.Name, nameStyle);
            if (card.Selected)
            {
                var badge = new GUIStyle(_caption)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.78f, 0.36f) }
                };
                GUI.Label(new Rect(area.x, area.y + 354, area.width, 38), "✓ 오늘 밤에 챙김", badge);
            }
            else if (card.Disabled)
                GUI.Label(new Rect(area.x, area.y + 354, area.width, 38), "선택 칸이 가득 찼어요", Centered(_caption));
            else if (hovered)
                GUI.Label(new Rect(area.x, area.y + 354, area.width, 38), "눌러서 챙기기", Centered(_caption));

            var oldEnabled = GUI.enabled;
            GUI.enabled = !card.Disabled;
            if (GUI.Button(area, GUIContent.none, GUIStyle.none))
            {
                _setupFocus = card.Id;
                _audio?.PlayUi();
                _flow.ToggleV2Item(card.Id);
            }
            GUI.enabled = oldEnabled;
        }

        private static void DrawItemSparkle(float x, float y, float size)
        {
            Color color = new Color(1f, 0.76f, 0.28f, 0.92f);
            Fill(new Rect(x - size * 0.12f, y - size, size * 0.24f, size * 2f), color);
            Fill(new Rect(x - size, y - size * 0.12f, size * 2f, size * 0.24f), color);
            Fill(new Rect(x - size * 0.42f, y - size * 0.42f, size * 0.84f, size * 0.84f),
                new Color(1f, 0.9f, 0.58f, 0.82f));
        }

        private ItemCardViewModel FocusedSetupCard(SetupViewModel vm)
        {
            ItemCardViewModel first = null;
            ItemCardViewModel selected = null;
            foreach (var card in vm.Cards)
            {
                if (first == null) first = card;
                if (card.Selected && selected == null) selected = card;
                if (_setupFocus.HasValue && card.Id == _setupFocus.Value) return card;
            }
            return selected ?? first;
        }

        private void DrawSetupItemDetail(Rect rect, ItemCardViewModel card, bool portrait)
        {
            DrawGlassPanel(rect, 0.72f);
            Fill(new Rect(rect.x, rect.y, 6f, rect.height), card.Selected
                ? new Color(1f, 0.72f, 0.32f)
                : new Color(0.48f, 0.57f, 0.66f));
            float inset = portrait ? 34f : 42f;
            GUI.Label(new Rect(rect.x + inset, rect.y + 12, rect.width - inset * 2f, portrait ? 58 : 52),
                card.Selected ? $"{card.Name} · 오늘 밤에 챙겼어요" : card.Name,
                OverlayLabelStyle(portrait ? 42 : 34, FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f)));
            GUI.Label(new Rect(rect.x + inset, rect.y + (portrait ? 70 : 62), rect.width - inset * 2f,
                    portrait ? 66 : 58),
                card.Desc,
                OverlayLabelStyle(portrait ? 34 : 26, FontStyle.Normal, new Color(0.88f, 0.9f, 0.92f),
                    TextAnchor.MiddleLeft, true));
            var warning = new GUIStyle(_caption)
            {
                normal = { textColor = new Color(0.94f, 0.76f, 0.52f) }
            };
            warning.alignment = TextAnchor.MiddleLeft;
            warning.clipping = TextClipping.Overflow;
            GUI.Label(new Rect(rect.x + inset, rect.y + (portrait ? 140 : 120), rect.width - inset * 2f,
                    portrait ? 72 : 54),
                $"기억할 점 · {card.Side}", warning);
        }

        private void LoadItemArt(ItemId id, string resourceName)
        {
            var texture = Resources.Load<Texture2D>($"Art/Items/{resourceName}");
            if (texture != null) _itemArt[id] = texture;
        }

        private void DrawItemArt(ItemId id, Rect rect)
        {
            if (!_itemArt.TryGetValue(id, out var texture) || texture == null) return;
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        }

        private void DrawPreparedItems(Rect rect, bool showLabel, V2PlayViewModel vm = null)
        {
            if (showLabel)
                GUI.Label(new Rect(rect.x, rect.y - 28, rect.width, 26), "오늘 챙긴 물건", _caption);
            int count = _flow.SelectedItems.Count;
            if (count == 0) return;
            float size = Mathf.Min(rect.height, (rect.width - (count - 1) * 8f) / count);
            for (int i = 0; i < count; i++)
            {
                ItemId item = _flow.SelectedItems[i];
                var itemRect = new Rect(rect.x + i * (size + 8f), rect.y, size, size);
                bool active = vm != null && (item == ItemId.Carrier && vm.CarrierOn ||
                    item == ItemId.Noise && vm.NoiseOn);
                DrawGlassPanel(itemRect, active ? .88f : .48f, active);
                DrawItemArt(item, new Rect(itemRect.x + 4f, itemRect.y + 4f,
                    itemRect.width - 8f, itemRect.height - 8f));
                var badgeRect = new Rect(itemRect.x - 2f, itemRect.yMax - 17f,
                    itemRect.width + 4f, 19f);
                Fill(badgeRect, active
                    ? new Color(.31f, .72f, .48f, .96f)
                    : new Color(.12f, .17f, .23f, .92f));
                GUI.Label(badgeRect, active ? "작동 중" : "보유",
                    OverlayLabelStyle(17, FontStyle.Bold, Color.white,
                        TextAnchor.MiddleCenter));
            }
        }

        private bool DrawActionButton(Rect rect, V2ActionButtonViewModel action, V2PlayViewModel vm, bool portrait)
        {
            ItemId? item = ItemForAction(action.Action);
            bool active = action.Action == V2ActionId.ToggleCarrier && vm.CarrierOn ||
                          action.Action == V2ActionId.ToggleNoise && vm.NoiseOn;
            bool hovered = rect.Contains(Event.current.mousePosition);
            DrawGlassPanel(rect, hovered ? 0.82f : 0.66f, active);
            if (hovered)
                Fill(new Rect(rect.x + 8, rect.y + 5, rect.width - 16, 3),
                    new Color(1f, 0.76f, 0.38f, 0.72f));
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            float iconSize = rect.height - (portrait ? 18f : 14f);
            if (item.HasValue)
                DrawItemArt(item.Value, new Rect(rect.x + 8f, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize));
            else
            {
                var sigil = new Rect(rect.x + 12f, rect.y + (rect.height - iconSize * 0.72f) * 0.5f,
                    iconSize * 0.72f, iconSize * 0.72f);
                Fill(sigil, active
                    ? new Color(0.96f, 0.68f, 0.3f, 0.98f)
                    : new Color(0.62f, 0.34f, 0.12f, 0.94f));
                GUI.Label(sigil, ActionSigil(action.Action),
                    LabelStyle(portrait ? 25 : 19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter));
                iconSize = sigil.width + 10f;
            }
            float textInset = iconSize + 20f;
            var labelStyle = LabelStyle(portrait ? 35 : 20, FontStyle.Bold,
                active ? Color.white : new Color(0.94f, 0.91f, 0.84f), TextAnchor.MiddleLeft);
            GUI.Label(new Rect(rect.x + textInset, rect.y, rect.width - textInset - 12f, rect.height),
                action.Label, labelStyle);
            return clicked;
        }

        private static string ActionSigil(V2ActionId action) => action switch
        {
            V2ActionId.CheckDiaper => "기",
            V2ActionId.CheckHungerSignals => "배",
            V2ActionId.CheckEnvironment => "온",
            V2ActionId.CheckLimbRelaxation => "힘",
            V2ActionId.Hesitate => "쉼",
            V2ActionId.CatchBreath => "숨",
            V2ActionId.Hold => "안",
            V2ActionId.Pat => "토",
            V2ActionId.Laydown => "눕",
            V2ActionId.ChangeDiaper => "갈",
            V2ActionId.AdjustTemperature => "온",
            V2ActionId.AdjustHumidity => "습",
            V2ActionId.SterilizeBottle => "소",
            V2ActionId.PrepareWater => "물",
            V2ActionId.CoolBottle => "식",
            V2ActionId.FeedPreparedBottle => "먹",
            _ => "·"
        };

        private static ItemId? ItemForAction(V2ActionId action) => action switch
        {
            V2ActionId.ToggleCarrier => ItemId.Carrier,
            V2ActionId.Pacifier => ItemId.Pacifier,
            V2ActionId.ToggleNoise => ItemId.Noise,
            V2ActionId.CheckMonitor => ItemId.Monitor,
            _ => null
        };

        private void DrawLockedCandidate(Rect rect, string name, string description)
        {
            Fill(rect, new Color(0.055f, 0.075f, 0.1f, 0.82f));
            Fill(new Rect(rect.x, rect.y, 5, rect.height), new Color(0.35f, 0.39f, 0.44f));
            float icon = Mathf.Min(rect.height - 34f, _portrait ? 78f : 74f);
            var iconRect = new Rect(rect.x + 22, rect.y + (rect.height - icon) * 0.5f, icon, icon);
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.72f);
            GUI.DrawTexture(iconRect, LockIcon(), ScaleMode.ScaleToFit, true);
            GUI.color = oldColor;
            float tx = iconRect.xMax + 20f;
            float tw = rect.xMax - tx - 20f;
            if (_portrait)
            {
                var compactTitle = new GUIStyle(_body) { fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(tx, rect.y + 14, tw, 46), $"잠김 · {name}", compactTitle);
                GUI.Label(new Rect(tx, rect.y + 64, tw, rect.height - 70), description, _caption);
                return;
            }
            GUI.Label(new Rect(tx, rect.y + 18, tw, 52), $"잠김 · {name}", _headline);
            GUI.Label(new Rect(tx, rect.y + 76, tw, Mathf.Max(38f, rect.height - 122)), description, _body);
            GUI.Label(new Rect(tx, rect.y + rect.height - 42, tw, 38), "후속 해금 후보 · 현재 선택 불가", _caption);
        }

        private void DrawPlay()
        {
            var vm = _flow.BuildV2Play();
            HandleRoomMovementKeys(vm);
            int encounterSequence = _flow.Session.Night.V2.Diagnosis.EncounterSequence;
            if (!vm.CauseResolved && _actionEncounterSequence != encounterSequence)
            {
                _actionEncounterSequence = encounterSequence;
                _actionGroup = ActionGroup.Diagnose;
            }
            // 탈진 중에는 회복 행동만 유효하므로 살펴보기 탭에 고정한다(가로·세로 공통).
            if (vm.ParentStamina <= 0) _actionGroup = ActionGroup.Diagnose;
            UpdateBabyAmbient(vm);
            _audio.SetBabyState(vm);

            if (_portrait)
            {
                DrawPortraitPlay(vm);
                if (_flow.PendingOverlay != null) DrawPortraitOverlay(_flow.PendingOverlay);
                return;
            }

            DrawPlayScene(vm, new Rect(0, 0, LandscapeWidth, LandscapeHeight), false);
            DrawTopBar(vm);
            DrawLandscapeStatusOrnaments(vm);
            DrawEchoSource(vm, new Rect(390, 548, 1140, 126), false);
            DrawSceneFeedback(vm, new Rect(390, 686, 1140, 76), false);

            if (_flow.PendingOverlay != null) DrawOverlay(_flow.PendingOverlay);
        }

        private void DrawTopBar(V2PlayViewModel vm)
        {
            GUI.Label(new Rect(48, 26, 250, 76), vm.Clock, OverlayLabelStyle(46, FontStyle.Bold,
                new Color(0.98f, 0.91f, 0.76f)));
            Fill(new Rect(48, 96, 170, 3), new Color(0.96f, 0.67f, 0.28f, 0.84f));
            GUI.Label(new Rect(650, 28, 620, 58), $"{_flow.BabyName} · {vm.NightRoleTitle}",
                OverlayLabelStyle(24, FontStyle.Bold, new Color(.94f, .88f, .76f),
                    TextAnchor.MiddleCenter));
            GUI.Label(new Rect(1448, 27, 420, 68), $"새벽까지 {FormatDuration(vm.RemainingMinutes)}",
                OverlayLabelStyle(24, FontStyle.Bold, new Color(0.94f, 0.9f, 0.82f),
                    TextAnchor.MiddleRight));
            DrawProgress(new Rect(1606, 91, 262, 4),
                1f - vm.RemainingMinutes / 540f, new Color(0.94f, 0.67f, 0.3f));
        }

        private void DrawPortraitPlay(V2PlayViewModel vm)
        {
            DrawPlayScene(vm, new Rect(0, 0, PortraitWidth, 1120), true);
            GUI.Label(new Rect(54, 65, 250, 88), vm.Clock, OverlayLabelStyle(51, FontStyle.Bold,
                new Color(0.98f, 0.91f, 0.76f)));
            Fill(new Rect(54, 148, 172, 4), new Color(0.96f, 0.67f, 0.28f, 0.84f));
            GUI.Label(new Rect(610, 72, 414, 72), $"새벽까지 {FormatDuration(vm.RemainingMinutes)}",
                OverlayLabelStyle(29, FontStyle.Bold, new Color(0.94f, 0.9f, 0.82f),
                    TextAnchor.MiddleRight));
            GUI.Label(new Rect(300, 76, 360, 58), vm.NightRoleTitle,
                OverlayLabelStyle(23, FontStyle.Bold, new Color(.94f, .88f, .76f),
                    TextAnchor.MiddleCenter));
            DrawProgress(new Rect(735, 145, 289, 5), 1f - vm.RemainingMinutes / 540f,
                new Color(0.94f, 0.67f, 0.3f));
            if (vm.EchoSources.Count > 0)
                DrawEchoSource(vm, new Rect(58, 1390, 964, 132), true);
            DrawPortraitStatusOrnaments(vm);
            DrawSceneFeedback(vm, new Rect(58, 1688, 964, 142), true);
        }

        private void DrawEchoSource(V2PlayViewModel vm, Rect rect, bool portrait)
        {
            if (vm.EchoSources.Count == 0) return;
            var echo = vm.EchoSources[vm.EchoSources.Count - 1];
            DrawGlassPanel(rect, .86f);
            Fill(new Rect(rect.x, rect.y + 12, 5, rect.height - 24),
                new Color(.96f, .66f, .3f));
            GUI.Label(new Rect(rect.x + 24, rect.y + 8, rect.width - 48, rect.height - 16),
                $"지난 밤의 리듬 · {echo.Cause}\n지금 · {echo.Change}\n대응 · {echo.ResponseHint}",
                LabelStyle(portrait ? 21 : 18, FontStyle.Bold,
                    new Color(.96f, .92f, .84f), TextAnchor.MiddleLeft));
        }

        private void DrawPlayScene(V2PlayViewModel vm, Rect rect, bool portrait)
        {
            var backdropRect = portrait
                ? new Rect(0, 0, PortraitWidth, PortraitHeight)
                : rect;
            DrawRoomFocusBackdrop(vm.CaregiverLocation, backdropRect);
            Fill(backdropRect, RoomFocusTint(vm.CaregiverLocation));

            bool babyVisible = vm.BabyLocation == vm.CaregiverLocation && !RoomTransitionActive();
            if (babyVisible)
            {
                bool heldOutsideNursery = vm.BabyHeld &&
                    vm.CaregiverLocation != HomeLocation.Nursery;
                float babySize = heldOutsideNursery
                    ? (portrait ? 540f : 460f)
                    : (portrait ? 720f : 600f);
                float babyCenterX = heldOutsideNursery
                    ? (portrait ? 810f : 1510f)
                    : rect.center.x;
                var babyRect = new Rect(
                    babyCenterX - babySize * 0.5f,
                    !vm.BabyHeld && IsSleeping(vm)
                        ? (portrait ? 390f : 150f)
                        : (portrait ? 270f : 86f),
                    babySize, babySize);
                bool compositeAction = HasCompositeAction(vm);
                if (!compositeAction) babyRect = AnimatedBabyActionRect(babyRect);
                GUI.DrawTexture(new Rect(babyRect.center.x - babySize * 0.24f,
                    babyRect.yMax - babySize * 0.12f, babySize * 0.48f, babySize * 0.09f),
                    _itemShadow, ScaleMode.StretchToFill, true);
                bool compositeDrawn = DrawCompositeBaby(vm, babyRect, portrait);
                if (!compositeDrawn)
                {
                    DrawAnimatedBaby(vm, babyRect);
                    DrawBabyActionAnimation(babyRect, portrait);
                }
                DrawSignalMotionCue(vm, babyRect, portrait);
                DrawBabbleBubble(vm, babyRect, portrait);
                DrawBabyDirectInteraction(vm, babyRect, portrait);
            }
            else
            {
                // 방 안의 실제 오브젝트가 목표와 어포던스를 설명한다.
                // 중앙 안내 문구를 겹치지 않아 직접조작 장면을 가리지 않는다.
            }

            DrawDirectRoomObjects(vm, portrait, babyVisible);
            DrawSignalRibbon(vm, portrait);
            DrawHomeJourneyMap(vm, portrait);
            DrawRoomTravelMoment(portrait);
        }

        private bool HasCompositeAction(V2PlayViewModel vm)
            => _animatedAction.HasValue && _interactionFrames.ContainsKey(_animatedAction.Value) &&
               (_animatedAction.Value != V2ActionId.CheckLimbRelaxation || IsSleeping(vm));

        private bool DrawCompositeBaby(V2PlayViewModel vm, Rect babyRect, bool portrait)
        {
            Texture2D frame = null;
            string caption = null;
            if (_animatedAction.HasValue &&
                _interactionFrames.TryGetValue(_animatedAction.Value, out var frames))
            {
                V2ActionId action = _animatedAction.Value;
                // 수면 이완 원화는 실제 수면 상태에서만 사용한다. 각성 중에는 현재 아기 위 손 연출로 폴백한다.
                if (action == V2ActionId.CheckLimbRelaxation && !IsSleeping(vm))
                    return false;
                float progress = BabyActionProgress();
                if (!_animatedAction.HasValue) return false;
                int index;
                if (action == V2ActionId.Pat)
                    index = Mathf.FloorToInt(progress * 16f) % frames.Length;
                else
                    index = Mathf.Clamp(Mathf.FloorToInt(progress * frames.Length), 0, frames.Length - 1);
                frame = frames[index];
                caption = CompositeActionCaption(action, progress);
            }
            else if (vm.CarrierOn && _carrierBabyFrames != null)
            {
                int index = IsSleeping(vm) ? 3 : vm.CryIntensity > 45 ? 2 :
                    vm.CryIntensity > 14 ? 1 : 0;
                frame = _carrierBabyFrames[index];
            }
            else if (!vm.BabyHeld && IsSleeping(vm))
                frame = _lyingSleepArt;

            if (frame == null) return false;
            GUI.DrawTexture(babyRect, frame, ScaleMode.ScaleToFit, true);
            if (!string.IsNullOrEmpty(caption))
            {
                var captionRect = new Rect(babyRect.center.x - (portrait ? 250f : 220f),
                    babyRect.yMax - (portrait ? 48f : 42f), portrait ? 500f : 440f,
                    portrait ? 54f : 46f);
                DrawGlassPanel(captionRect, 0.76f);
                GUI.Label(captionRect, caption,
                    OverlayLabelStyle(portrait ? 25 : 20, FontStyle.Bold,
                        Color.white, TextAnchor.MiddleCenter));
            }
            return true;
        }

        private static string CompositeActionCaption(V2ActionId action, float progress) => action switch
        {
            V2ActionId.Pat => progress < .25f ? "손을 포근히 가져가요" :
                progress < .8f ? "토닥 · 토닥 · 천천히" : "같은 리듬으로 한 번 더",
            V2ActionId.Hold => progress < .35f ? "목과 등을 함께 받쳐요" :
                "아빠 품에 꼭 · 살랑살랑",
            V2ActionId.ToggleCarrier => "몸을 포근히 감싸고 얼굴은 편안하게",
            V2ActionId.FeedPreparedBottle => progress < .3f ? "목을 받치고 젖병을 가까이" :
                "꿀꺽 · 꿀꺽 · 아기 속도에 맞춰요",
            V2ActionId.Pacifier => progress < .45f ? "쪽쪽이를 입가에 살며시" :
                "편안하게 쪽쪽 · 숨을 살펴봐요",
            V2ActionId.CheckDiaper => "두 다리를 함께 받치고 기저귀를 살펴봐요",
            V2ActionId.ChangeDiaper => "깨끗한 기저귀를 포근히 채워요",
            V2ActionId.CheckLimbRelaxation => "팔다리의 힘이 풀렸는지 가볍게 받쳐봐요",
            V2ActionId.CheckBodyTemperature => "목을 받치고 이마에 손을 가만히 대봐요",
            _ => null
        };

        private void DrawBabyDirectInteraction(V2PlayViewModel vm, Rect babyRect, bool portrait)
        {
            if (_flow.InputLocked || RoomTransitionActive() || _animatedAction.HasValue)
            {
                if (_animatedAction.HasValue)
                {
                    var busy = new Rect(babyRect.center.x - (portrait ? 170f : 145f),
                        babyRect.yMax - (portrait ? 12f : 8f), portrait ? 340f : 290f,
                        portrait ? 52f : 44f);
                    DrawGlassPanel(busy, .62f);
                    GUI.Label(busy, "돌보는 중 · 잠시만 기다려요",
                        OverlayLabelStyle(portrait ? 20 : 17, FontStyle.Bold,
                            new Color(1f, .88f, .68f), TextAnchor.MiddleCenter));
                }
                return;
            }
            float pulse = (Mathf.Sin(Time.unscaledTime * 4.2f) + 1f) * .5f;
            var mouth = new Rect(babyRect.x + babyRect.width * .38f,
                babyRect.y + babyRect.height * .17f, babyRect.width * .24f, babyRect.height * .2f);
            var back = new Rect(babyRect.x + babyRect.width * .58f,
                babyRect.y + babyRect.height * .3f, babyRect.width * .25f, babyRect.height * .34f);
            var chest = new Rect(babyRect.x + babyRect.width * .27f,
                babyRect.y + babyRect.height * .32f, babyRect.width * .3f, babyRect.height * .31f);
            var diaper = new Rect(babyRect.x + babyRect.width * .34f,
                babyRect.y + babyRect.height * .57f, babyRect.width * .33f, babyRect.height * .12f);
            var limbs = new Rect(babyRect.x + babyRect.width * .16f,
                babyRect.y + babyRect.height * .72f, babyRect.width * .68f, babyRect.height * .16f);
            var mattress = new Rect(babyRect.x + babyRect.width * .08f,
                babyRect.yMax - babyRect.height * .08f, babyRect.width * .84f, babyRect.height * .17f);

            V2ActionId mouthAction = vm.FeedingReady
                ? V2ActionId.FeedPreparedBottle
                : !vm.CauseResolved ? V2ActionId.CheckHungerSignals : V2ActionId.Pacifier;
            V2ActionId diaperAction = vm.RevealedCause == WakeCause.Diaper
                ? V2ActionId.ChangeDiaper : V2ActionId.CheckDiaper;

            Rect recommendedRect = back;
            V2ActionId recommendedAction = V2ActionId.Pat;
            string recommendedLabel = "등을 천천히 토닥여요";
            if (!vm.CauseResolved)
            {
                recommendedRect = mouth;
                recommendedAction = mouthAction;
                recommendedLabel = "입과 손의 신호를 만져 살펴봐요";
            }
            if (vm.RevealedCause == WakeCause.Diaper)
            {
                recommendedRect = diaper;
                recommendedAction = diaperAction;
                recommendedLabel = "기저귀 부분을 만져 확인해요";
            }
            if (vm.FeedingReady)
            {
                recommendedRect = mouth;
                recommendedAction = V2ActionId.FeedPreparedBottle;
                recommendedLabel = "입가를 눌러 준비한 젖병을 먹여요";
            }
            if (Time.unscaledTime >= _directCueHiddenUntil)
                DrawRecommendedTouchCue(vm, recommendedRect, recommendedAction,
                    recommendedLabel, pulse, portrait);

            DrawBodyHotspot(vm, back, V2ActionId.Pat, "등을 토닥", pulse, portrait);
            V2ActionId chestAction = vm.CarrierOn ? V2ActionId.ToggleCarrier : V2ActionId.Hold;
            DrawBodyHotspot(vm, chest, chestAction,
                vm.CarrierOn ? "아기띠 풀어주기" : "품에 안기", 1f - pulse, portrait);
            DrawBodyHotspot(vm, mouth, mouthAction,
                mouthAction == V2ActionId.FeedPreparedBottle ? "준비한 분유 수유" :
                mouthAction == V2ActionId.Pacifier ? "쪽쪽이 건네기" : "입과 손 신호 보기",
                pulse, portrait);
            DrawBodyHotspot(vm, diaper, diaperAction,
                diaperAction == V2ActionId.ChangeDiaper ? "기저귀 갈기" : "기저귀 살피기",
                1f - pulse, portrait);
            DrawBodyHotspot(vm, limbs, V2ActionId.CheckLimbRelaxation,
                "팔다리 힘 살피기", pulse * .8f, portrait);
            if (IsSleeping(vm))
                DrawBodyHotspot(vm, mattress, V2ActionId.Laydown,
                    "침대로 천천히", pulse, portrait);
        }

        private void DrawBodyHotspot(V2PlayViewModel vm, Rect rect, V2ActionId id,
            string label, float pulse, bool portrait)
        {
            var action = DirectAction(vm, id);
            if (action == null) return;
            bool hovered = rect.Contains(Event.current.mousePosition);
            var old = GUI.color;
            float idleGlow = .12f + pulse * .2f;
            GUI.color = new Color(1f, .72f, .3f, hovered ? .62f : idleGlow);
            GUI.DrawTexture(rect, _itemGlow, ScaleMode.StretchToFill, true);
            GUI.color = old;
            // 모바일에는 hover가 없으므로 활성 부위와 기능을 항상 짧게 표시한다.
            var idleCaption = new Rect(rect.center.x - (portrait ? 86f : 74f),
                rect.yMax - (portrait ? 34f : 28f), portrait ? 172f : 148f,
                portrait ? 34f : 28f);
            DrawGlassPanel(idleCaption, hovered ? .84f : .58f);
            GUI.Label(idleCaption, label, OverlayLabelStyle(portrait ? 16 : 13,
                FontStyle.Bold, hovered ? Color.white : new Color(1f, .9f, .7f),
                TextAnchor.MiddleCenter));
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                _directHintSeen = true;
                PerformV2Action(id);
            }
        }

        private void DrawRecommendedTouchCue(V2PlayViewModel vm, Rect rect, V2ActionId id,
            string label, float pulse, bool portrait)
        {
            if (DirectAction(vm, id) == null) return;
            float size = Mathf.Min(rect.width, rect.height) * (.46f + pulse * .08f);
            var cue = new Rect(rect.center.x - size * .5f, rect.center.y - size * .5f, size, size);
            var old = GUI.color;
            GUI.color = new Color(1f, .74f, .34f, .32f + pulse * .34f);
            GUI.DrawTexture(cue, _itemGlow, ScaleMode.StretchToFill, true);
            GUI.color = old;
            DrawCareSparkles(cue.center, .45f + pulse * .4f, 2);
            var labelRect = new Rect(rect.center.x - (portrait ? 190f : 175f),
                rect.yMax + 4f, portrait ? 380f : 350f, portrait ? 46f : 40f);
            DrawGlassPanel(labelRect, 0.82f);
            GUI.Label(labelRect, label, OverlayLabelStyle(portrait ? 21 : 17,
                FontStyle.Bold, Color.white, TextAnchor.MiddleCenter));
        }

        private V2ActionButtonViewModel DirectAction(V2PlayViewModel vm, V2ActionId id)
            => vm.Actions.Find(action => action.Action == id && action.Enabled);

        private void DrawDirectRoomObjects(V2PlayViewModel vm, bool portrait, bool babyVisible)
        {
            if (_flow.InputLocked || RoomTransitionActive()) return;
            switch (vm.CaregiverLocation)
            {
                case HomeLocation.Kitchen:
                    DrawKitchenPreparation(vm, portrait, babyVisible);
                    break;
                case HomeLocation.Bathroom:
                    DrawSceneActionHotspot(vm,
                        portrait ? new Rect(610, 430, 190, 250) : new Rect(1040, 215, 190, 245),
                        V2ActionId.CheckBodyTemperature, "탕온계로 아기 체온 살피기", portrait);
                    DrawSceneActionHotspot(vm,
                        portrait ? new Rect(245, 470, 200, 230) : new Rect(650, 245, 200, 220),
                        V2ActionId.CheckEnvironment, "욕실 온도와 습기 살피기", portrait);
                    break;
                default:
                    DrawSceneActionHotspot(vm,
                        portrait ? new Rect(455, 175, 170, 150) : new Rect(1580, 430, 150, 145),
                        V2ActionId.CheckEnvironment, "온습도계 숫자 확인하기", portrait);
                    DrawSceneActionHotspot(vm,
                        portrait ? new Rect(250, 330, 190, 260) : new Rect(560, 170, 190, 250),
                        V2ActionId.AdjustTemperature, "창문을 조절해 방 온도 맞추기", portrait);
                    DrawSceneActionHotspot(vm,
                        portrait ? new Rect(620, 650, 170, 190) : new Rect(1110, 350, 170, 180),
                        V2ActionId.AdjustHumidity, "가습기를 조절해 습도 맞추기", portrait);
                    DrawRoomObject(vm, V2ActionId.ToggleNoise,
                        portrait ? new Rect(70, 500, 170, 210) : new Rect(340, 265, 170, 200),
                        vm.NoiseOn ? "백색소음 끄기" : "백색소음기", ItemId.Noise, portrait);
                    if (vm.HasPacifier)
                        DrawPacifierProp(vm,
                            portrait ? new Rect(835, 1015, 145, 145) : new Rect(1435, 470, 125, 125),
                            portrait);
                    if (!vm.CarrierOn)
                        DrawRoomObject(vm, V2ActionId.ToggleCarrier,
                            portrait ? new Rect(870, 560, 120, 150) : new Rect(1320, 305, 130, 150),
                            "아기띠", ItemId.Carrier, portrait);
                    DrawRoomObject(vm, V2ActionId.CheckMonitor,
                        portrait ? new Rect(70, 300, 145, 170) : new Rect(350, 230, 145, 155),
                        "베이비 모니터 살피기", ItemId.Monitor, portrait);
                    DrawSceneActionHotspot(vm,
                        portrait ? new Rect(825, 250, 150, 180) : new Rect(1510, 150, 150, 170),
                        V2ActionId.Grandma, "할머니에게 전화해 도움 청하기", portrait);
                    break;
            }
            if (IsSleeping(vm)) DrawSleepSceneChoices(portrait);
            DrawRoomPickupAnimation(vm, portrait);
        }

        private void DrawPacifierProp(V2PlayViewModel vm, Rect rect, bool portrait)
        {
            var action = DirectAction(vm, V2ActionId.Pacifier);
            float pulse = (Mathf.Sin(Time.unscaledTime * 3.2f) + 1f) * .5f;
            if (action != null)
            {
                var old = GUI.color;
                GUI.color = new Color(1f, .75f, .34f, .18f + pulse * .34f);
                GUI.DrawTexture(new Rect(rect.x - 12f, rect.y - 12f,
                    rect.width + 24f, rect.height + 24f), _itemGlow, ScaleMode.StretchToFill, true);
                GUI.color = old;
                DrawCareSparkles(rect.center, .35f + pulse * .35f, 2);
            }
            DrawItemArt(ItemId.Pacifier, rect);
            GUI.Label(new Rect(rect.x - 35f, rect.yMax - 6f, rect.width + 70f,
                    portrait ? 42f : 34f), "쪽쪽이",
                OverlayLabelStyle(portrait ? 20 : 16, FontStyle.Bold,
                    new Color(1f, .9f, .72f), TextAnchor.MiddleCenter));
            if (action != null && GUI.Button(rect, GUIContent.none, GUIStyle.none))
                PerformV2Action(V2ActionId.Pacifier);
        }

        private void DrawSleepSceneChoices(bool portrait)
        {
            Rect rest = portrait ? new Rect(110, 1230, 190, 150) : new Rect(520, 785, 180, 135);
            Rect environment = portrait ? new Rect(445, 1230, 190, 150) : new Rect(850, 785, 180, 135);
            Rect feed = portrait ? new Rect(780, 1230, 190, 150) : new Rect(1180, 785, 180, 135);
            DrawSleepProp(rest, "같이 눈 붙이기", "잠", SleepIntervalChoice.RestTogether, portrait);
            DrawSleepProp(environment, "방 상태 둘러보기", "방", SleepIntervalChoice.CheckEnvironment, portrait);
            DrawSleepProp(feed, "다음 수유 챙기기", "병", SleepIntervalChoice.PrepareNextFeed, portrait);
        }

        private void DrawSleepProp(Rect rect, string label, string symbol,
            SleepIntervalChoice choice, bool portrait)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * 2.4f + (int)choice) + 1f) * .5f;
            var old = GUI.color;
            GUI.color = new Color(.55f, .8f, .95f, .18f + pulse * .28f);
            GUI.DrawTexture(rect, _itemGlow, ScaleMode.StretchToFill, true);
            GUI.color = old;
            GUI.Label(new Rect(rect.x, rect.y, rect.width, rect.height * .62f), symbol,
                OverlayLabelStyle(portrait ? 62 : 52, FontStyle.Bold,
                    new Color(.72f, .9f, 1f), TextAnchor.MiddleCenter));
            GUI.Label(new Rect(rect.x - 30, rect.yMax - 42, rect.width + 60, 38), label,
                OverlayLabelStyle(portrait ? 19 : 16, FontStyle.Bold,
                    new Color(.9f, .94f, .96f), TextAnchor.MiddleCenter));
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none) &&
                _flow.ChooseV2SleepInterval(choice))
            {
                _lastResult = null;
                _lastMove = null;
                _audio.PlayUi();
                TriggerImpact(new Color(.36f, .62f, .78f, .22f), 1.5f, .22f);
            }
        }

        private void DrawKitchenPreparation(V2PlayViewModel vm, bool portrait, bool babyVisible)
        {
            bool splitPortrait = portrait && babyVisible;
            Rect powder = splitPortrait ? new Rect(92, 1050, 215, 265)
                : portrait ? new Rect(112, 500, 270, 330) : new Rect(480, 205, 255, 310);
            Rect bottle = splitPortrait ? new Rect(435, 1035, 185, 285)
                : portrait ? new Rect(414, 490, 240, 360) : new Rect(825, 185, 220, 330);
            Rect cooling = splitPortrait ? new Rect(735, 1100, 220, 195)
                : portrait ? new Rect(690, 570, 280, 250) : new Rect(1110, 270, 285, 235);

            DrawFormulaTin(powder, vm.FormulaMeasured, portrait);
            DrawFeedingBottleState(bottle, vm, portrait);
            DrawCoolingBasin(cooling, vm.BottleCooled, portrait);

            if (!vm.BottleSanitized)
                DrawSceneActionHotspot(vm, bottle, V2ActionId.SterilizeBottle,
                    "젖병을 씻고 소독하기", portrait);
            else if (!vm.BottleMixed)
                DrawSceneActionHotspot(vm, powder, V2ActionId.PrepareWater,
                    "분유가루를 떠서 물에 섞기", portrait);
            else if (!vm.BottleCooled)
                DrawSceneActionHotspot(vm, cooling, V2ActionId.CoolBottle,
                    "젖병을 물에 담가 식히기", portrait);

            string state = !vm.BottleSanitized ? "젖병을 먼저 소독해야 해요" :
                !vm.BottleMixed ? "빈 젖병 · 분유가루가 기다리고 있어요" :
                !vm.BottleCooled ? "분유가 채워졌어요 · 이제 식혀주세요" :
                "수유 준비 완료 · 아기에게 가져가세요";
            float stateY = splitPortrait ? 1350f : portrait ? 842f : 530f;
            GUI.Label(new Rect(portrait ? 120 : 500, stateY,
                    portrait ? 840 : 900, portrait ? 58 : 46), state,
                OverlayLabelStyle(portrait ? 25 : 21, FontStyle.Bold,
                    vm.FeedingReady ? new Color(.55f, .92f, .67f) : new Color(1f, .86f, .62f),
                    TextAnchor.MiddleCenter));
        }

        private void DrawFormulaTin(Rect rect, bool measured, bool portrait)
        {
            if (_formulaTinArt != null)
                GUI.DrawTexture(rect, _formulaTinArt, ScaleMode.ScaleToFit, true);
            if (measured)
                GUI.Label(new Rect(rect.x, rect.yMax - 48, rect.width, 42), "한 스푼 덜었어요",
                    OverlayLabelStyle(portrait ? 19 : 16, FontStyle.Bold,
                        new Color(.56f, .96f, .7f), TextAnchor.MiddleCenter));
        }

        private void DrawFeedingBottleState(Rect rect, V2PlayViewModel vm, bool portrait)
        {
            var bottle = new Rect(rect.x, rect.y, rect.width, rect.height - 34f);
            var body = new Rect(rect.x + rect.width * .405f, rect.y + rect.height * .46f,
                rect.width * .19f, rect.height * .3f);
            float fill = vm.BottleMixed ? .72f : vm.FeedingWaterReady ? .42f : .06f;
            Color liquid = vm.BottleCooled
                ? new Color(.88f, .71f, .39f, .82f)
                : new Color(.96f, .63f, .25f, .78f);
            if (_feedingBottleArt != null)
                GUI.DrawTexture(bottle, _feedingBottleArt, ScaleMode.ScaleToFit, true);
            if (fill > .08f)
            {
                var liquidRect = new Rect(body.x, body.yMax - body.height * fill,
                    body.width, body.height * fill);
                Fill(liquidRect, liquid);
                Fill(new Rect(liquidRect.x, liquidRect.y, liquidRect.width, 3f),
                    new Color(1f, .88f, .62f, .9f));
            }
            string label = !vm.BottleSanitized ? "미소독" :
                !vm.BottleMixed ? "비어 있음" :
                !vm.BottleCooled ? "분유 채움" : "먹일 준비 완료";
            GUI.Label(new Rect(rect.x, rect.yMax - 32, rect.width, 38), label,
                OverlayLabelStyle(portrait ? 20 : 17, FontStyle.Bold,
                    vm.FeedingReady ? new Color(.56f, .96f, .7f) : new Color(1f, .88f, .68f),
                    TextAnchor.MiddleCenter));
        }

        private void DrawCoolingBasin(Rect rect, bool cooled, bool portrait)
        {
            if (_coolingBasinArt != null)
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, rect.height - 28f),
                    _coolingBasinArt, ScaleMode.ScaleToFit, true);
            GUI.Label(new Rect(rect.x, rect.yMax - 38, rect.width, 38),
                cooled ? "알맞게 식었어요" : "식힘 물",
                OverlayLabelStyle(portrait ? 20 : 17, FontStyle.Bold,
                    Color.white, TextAnchor.MiddleCenter));
        }

        private void DrawSceneActionHotspot(V2PlayViewModel vm, Rect rect, V2ActionId id,
            string hoverLabel, bool portrait)
        {
            var action = DirectAction(vm, id);
            if (action == null) return;
            float pulse = (Mathf.Sin(Time.unscaledTime * 4.4f + (int)id) + 1f) * .5f;
            var old = GUI.color;
            GUI.color = new Color(1f, .76f, .32f, .22f + pulse * .5f);
            GUI.DrawTexture(new Rect(rect.x - 18, rect.y - 18, rect.width + 36, rect.height + 36),
                _itemGlow, ScaleMode.StretchToFill, true);
            GUI.color = old;
            DrawCareSparkles(rect.center, .55f + pulse * .45f, 4);
            if (rect.Contains(Event.current.mousePosition))
                GUI.Label(new Rect(rect.x - 50, rect.y - (portrait ? 48 : 40),
                        rect.width + 100, portrait ? 46 : 38), hoverLabel,
                    OverlayLabelStyle(portrait ? 21 : 18, FontStyle.Bold,
                        new Color(1f, .9f, .68f), TextAnchor.MiddleCenter));
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                _roomObjectAction = id;
                _roomObjectAnimationStarted = Time.unscaledTime;
                PerformV2Action(id);
            }
        }

        private void DrawRoomPickupAnimation(V2PlayViewModel vm, bool portrait)
        {
            if (!_roomObjectAction.HasValue) return;
            if (_roomObjectAction == V2ActionId.ToggleCarrier)
            {
                _roomObjectAction = null;
                return;
            }
            float progress = (Time.unscaledTime - _roomObjectAnimationStarted) / .8f;
            if (progress >= 1f) { _roomObjectAction = null; return; }
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            Vector2 from = portrait ? new Vector2(540, 550) : new Vector2(960, 300);
            Vector2 to = portrait ? new Vector2(870, 860) : new Vector2(1420, 650);
            Vector2 center = Vector2.Lerp(from, to, eased);
            float size = portrait ? 150f : 120f;
            var prop = new Rect(center.x - size * .5f, center.y - size * .5f, size, size * 1.3f);
            bool drewProp = false;
            if (vm.CaregiverLocation == HomeLocation.Kitchen)
            {
                DrawBottleProp(prop, _roomObjectAction.Value);
                drewProp = true;
            }
            else if (_roomObjectAction == V2ActionId.ToggleCarrier)
            {
                DrawItemArt(ItemId.Carrier, prop);
                drewProp = true;
            }
            else if (_roomObjectAction == V2ActionId.ToggleNoise)
            {
                DrawItemArt(ItemId.Noise, prop);
                drewProp = true;
            }
            else if (_roomObjectAction == V2ActionId.CheckMonitor)
            {
                DrawItemArt(ItemId.Monitor, prop);
                drewProp = true;
            }
            if (drewProp)
            {
                DrawCaregiverHand(new Rect(prop.xMax - size * .25f, prop.yMax - size * .3f,
                    size * .65f, size * .42f), true);
                DrawCareSparkles(prop.center, 1f - eased * .35f, 4);
            }
        }

        private void DrawRoomObject(V2PlayViewModel vm, V2ActionId actionId, Rect rect,
            string label, ItemId art, bool portrait)
        {
            var action = DirectAction(vm, actionId);
            if (action == null) return;
            float phase = (Mathf.Sin(Time.unscaledTime * 4f + (int)actionId) + 1f) * .5f;
            float lift = _roomObjectAction == actionId
                ? Mathf.Sin(Mathf.Clamp01((Time.unscaledTime - _roomObjectAnimationStarted) / .8f) * Mathf.PI) * 70f
                : phase * 7f;
            var objectRect = new Rect(rect.x, rect.y - lift, rect.width, rect.height);
            var old = GUI.color;
            GUI.color = new Color(1f, .82f, .48f, .22f + phase * .35f);
            GUI.DrawTexture(new Rect(objectRect.x - 20, objectRect.y - 20,
                objectRect.width + 40, objectRect.height + 40), _itemGlow, ScaleMode.StretchToFill, true);
            GUI.color = old;
            if (vm.CaregiverLocation == HomeLocation.Kitchen)
                DrawBottleProp(objectRect, actionId);
            else
                DrawItemArt(art, objectRect);
            DrawCareSparkles(objectRect.center, .45f + phase * .5f, 3);
            if (objectRect.Contains(Event.current.mousePosition))
                GUI.Label(new Rect(objectRect.x - 35, objectRect.y - (portrait ? 48f : 40f),
                        objectRect.width + 70, portrait ? 46f : 38f), label,
                    OverlayLabelStyle(portrait ? 21 : 18, FontStyle.Bold,
                        new Color(1f, .9f, .7f), TextAnchor.MiddleCenter));
            if (GUI.Button(objectRect, GUIContent.none, GUIStyle.none))
            {
                _roomObjectAction = actionId;
                _roomObjectAnimationStarted = Time.unscaledTime;
                PerformV2Action(actionId);
            }
        }

        private void DrawBottleProp(Rect rect, V2ActionId action)
        {
            float bottleWidth = rect.width * .42f;
            float bottleHeight = rect.height * .68f;
            var bottle = new Rect(rect.center.x - bottleWidth * .5f,
                rect.y + rect.height * .2f, bottleWidth, bottleHeight);
            GUI.DrawTexture(bottle, _diaperCloth, ScaleMode.StretchToFill, true);
            Color liquid = action == V2ActionId.CoolBottle
                ? new Color(.48f, .76f, .92f, .78f)
                : new Color(.96f, .78f, .42f, .82f);
            Fill(new Rect(bottle.x + 9f, bottle.y + bottle.height * .42f,
                bottle.width - 18f, bottle.height * .46f), liquid);
            GUI.DrawTexture(new Rect(bottle.center.x - bottle.width * .19f,
                bottle.y - bottle.height * .14f, bottle.width * .38f, bottle.height * .22f),
                _caregiverHand, ScaleMode.StretchToFill, true);
            if (action == V2ActionId.PrepareWater)
                Fill(new Rect(rect.x + rect.width * .12f, rect.y + rect.height * .62f,
                    rect.width * .18f, rect.height * .16f), new Color(.92f, .72f, .35f, .94f));
        }

        private void DrawSignalRibbon(V2PlayViewModel vm, bool portrait)
        {
            float width = portrait ? 760f : 700f;
            float x = portrait ? 46f : 52f;
            float y = portrait ? 176f : 118f;
            float height = portrait ? 104f : 84f;
            DrawGlassPanel(new Rect(x, y, width, height), 0.62f);
            Fill(new Rect(x, y + 12, 4, height - 24),
                vm.CauseResolved ? new Color(0.45f, 0.8f, 0.61f) : new Color(0.96f, 0.64f, 0.3f));
            GUI.Label(new Rect(x + 24, y + 4, width - 48, portrait ? 48 : 38),
                vm.CauseResolved ? BabyStateHeadline(vm) : "아기의 신호를 살펴보세요",
                OverlayLabelStyle(portrait ? 31 : 20, FontStyle.Bold,
                    new Color(0.98f, 0.87f, 0.68f)));
            GUI.Label(new Rect(x + 24, y + (portrait ? 47 : 39), width - 48, portrait ? 56 : 42),
                vm.CurrentSignal,
                OverlayLabelStyle(portrait ? 29 : 20, FontStyle.Normal,
                    new Color(0.92f, 0.93f, 0.93f), TextAnchor.MiddleLeft, true));
        }

        private void DrawHomeJourneyMap(V2PlayViewModel vm, bool portrait)
        {
            Rect map = portrait
                ? new Rect(816, 176, 232, 332)
                : new Rect(1510, 116, 360, 224);
            DrawGlassPanel(map, 0.66f);
            Fill(new Rect(map.x + 14, map.y + 14, 4, map.height - 28),
                new Color(0.95f, 0.69f, 0.33f, 0.8f));
            GUI.Label(new Rect(map.x + 28, map.y + 8, map.width - 42, portrait ? 40 : 32),
                "우리 집 새벽 동선",
                OverlayLabelStyle(portrait ? 19 : 16, FontStyle.Bold,
                    new Color(0.98f, 0.88f, 0.7f), TextAnchor.MiddleLeft));

            Vector2 nursery = portrait
                ? new Vector2(map.center.x, map.y + 100)
                : new Vector2(map.x + 82, map.y + 126);
            Vector2 kitchen = portrait
                ? new Vector2(map.x + 64, map.y + 252)
                : new Vector2(map.x + 190, map.y + 82);
            Vector2 bathroom = portrait
                ? new Vector2(map.x + 172, map.y + 252)
                : new Vector2(map.x + 282, map.y + 160);
            DrawDottedRoute(nursery, kitchen);
            DrawDottedRoute(nursery, bathroom);
            DrawDottedRoute(kitchen, bathroom);

            DrawMapRoomNode(vm, HomeLocation.Nursery, nursery, portrait);
            DrawMapRoomNode(vm, HomeLocation.Kitchen, kitchen, portrait);
            DrawMapRoomNode(vm, HomeLocation.Bathroom, bathroom, portrait);

            bool moving = RoomTransitionActive();
            if (!moving || !_roomTransitionBabyAccompanied)
                DrawMapToken(MapPoint(vm.BabyLocation, nursery, kitchen, bathroom) +
                    new Vector2(portrait ? 24f : 19f, portrait ? -27f : -22f),
                    "♥", new Color(0.95f, 0.65f, 0.72f), portrait);
            if (moving)
            {
                float eased = Mathf.SmoothStep(0f, 1f, RoomTransitionProgress());
                Vector2 from = MapPoint(_roomTransitionFrom, nursery, kitchen, bathroom);
                Vector2 to = MapPoint(_roomTransitionTo, nursery, kitchen, bathroom);
                Vector2 caregiver = Vector2.Lerp(from, to, eased) +
                    new Vector2(portrait ? -24f : -19f, portrait ? -27f : -22f);
                DrawMapToken(caregiver, "아", new Color(0.96f, 0.69f, 0.3f), portrait);
                if (_roomTransitionBabyAccompanied)
                    DrawMapToken(caregiver + new Vector2(portrait ? 38f : 31f, 0f),
                        "♥", new Color(0.95f, 0.65f, 0.72f), portrait);
            }
            else
            {
                DrawMapToken(MapPoint(vm.CaregiverLocation, nursery, kitchen, bathroom) +
                    new Vector2(portrait ? -24f : -19f, portrait ? -27f : -22f),
                    "아", new Color(0.96f, 0.69f, 0.3f), portrait);
            }
        }

        private void DrawMapRoomNode(V2PlayViewModel vm, HomeLocation location, Vector2 center, bool portrait)
        {
            float width = portrait ? 94f : 92f;
            float height = portrait ? 58f : 54f;
            var room = new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
            bool current = vm.CaregiverLocation == location;
            DrawGlassPanel(room, current ? 0.9f : 0.64f, current);
            GUI.Label(room, HomeLocationLabel(location),
                OverlayLabelStyle(portrait ? 17 : 14, FontStyle.Bold,
                    current ? Color.white : new Color(0.84f, 0.86f, 0.87f),
                    TextAnchor.MiddleCenter));
            var old = GUI.enabled;
            GUI.enabled = old && !current && !_flow.InputLocked && !RoomTransitionActive();
            if (GUI.Button(room, GUIContent.none, GUIStyle.none)) MoveToRoom(location);
            GUI.enabled = old;
        }

        private static Vector2 MapPoint(HomeLocation location, Vector2 nursery,
            Vector2 kitchen, Vector2 bathroom) => location switch
        {
            HomeLocation.Kitchen => kitchen,
            HomeLocation.Bathroom => bathroom,
            _ => nursery
        };

        private static void DrawDottedRoute(Vector2 from, Vector2 to)
        {
            for (int i = 1; i < 8; i++)
            {
                float t = i / 8f;
                Vector2 point = Vector2.Lerp(from, to, t);
                float pulse = 4f + Mathf.Sin((Time.unscaledTime + t) * 4f) * 1.2f;
                Fill(new Rect(point.x - pulse * 0.5f, point.y - pulse * 0.5f, pulse, pulse),
                    new Color(0.88f, 0.7f, 0.43f, 0.46f));
            }
        }

        private void DrawMapToken(Vector2 center, string label, Color color, bool portrait)
        {
            float size = portrait ? 34f : 28f;
            var token = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
            GUI.DrawTexture(token, _itemGlow, ScaleMode.StretchToFill, true);
            Fill(new Rect(token.x + 4f, token.y + 4f, token.width - 8f, token.height - 8f),
                new Color(color.r, color.g, color.b, 0.96f));
            GUI.Label(token, label, OverlayLabelStyle(portrait ? 17 : 13, FontStyle.Bold,
                Color.white, TextAnchor.MiddleCenter));
        }

        private void DrawRoomTravelMoment(bool portrait)
        {
            if (!RoomTransitionActive()) return;
            float progress = RoomTransitionProgress();
            float presence = Mathf.Sin(progress * Mathf.PI);
            Rect card = portrait
                ? new Rect(294, 620, 492, 72)
                : new Rect(760, 650, 470, 60);
            DrawGlassPanel(card, 0.55f + presence * 0.18f);
            string destination = HomeLocationLabel(_roomTransitionTo);
            string copy = _roomTransitionBabyAccompanied
                ? $"목을 받치고 {destination}으로 살금살금"
                : _roomTransitionTo == HomeLocation.Nursery
                    ? "우는 아기에게 다시 돌아가요"
                : $"{destination}에 필요한 물건을 가지러 가요";
            GUI.Label(card, copy, OverlayLabelStyle(portrait ? 23 : 19, FontStyle.Bold,
                new Color(1f, 0.87f, 0.68f), TextAnchor.MiddleCenter));
            for (int i = 0; i < 4; i++)
            {
                float step = (progress * 1.5f + i * 0.22f) % 1f;
                float x = Mathf.Lerp(card.x + 56f, card.xMax - 56f, step);
                float y = card.yMax + 10f + Mathf.Sin(step * Mathf.PI * 2f) * 5f;
                Fill(new Rect(x, y, 7f, 11f),
                    new Color(0.98f, 0.72f, 0.38f, presence * (0.9f - i * 0.12f)));
            }
        }

        private void DrawLandscapeStatusOrnaments(V2PlayViewModel vm)
        {
            DrawStatusOrnament(new Rect(38, 220, 286, 82), "현재 / 오늘 최장",
                $"{FormatDuration(vm.CurrentSleepStretchMinutes)} / {FormatDuration(vm.LongestSleepStretchMinutes)}",
                Mathf.Clamp01(vm.CurrentSleepStretchMinutes / 300f),
                new Color(0.4f, 0.72f, 0.91f), false);
            DrawStatusOrnament(new Rect(38, 316, 286, 82), "보호자 체력",
                $"{vm.ParentStamina:0}",
                Mathf.Clamp01((float)vm.ParentStamina / 100f),
                vm.ParentStamina >= 30 ? new Color(0.49f, 0.84f, 0.61f) : new Color(0.94f, 0.39f, 0.34f), false);
            DrawStatusOrnament(new Rect(38, 412, 286, 82), "마음의 여유",
                $"{vm.CaregiverComposure:0}",
                Mathf.Clamp01((float)vm.CaregiverComposure / 100f),
                new Color(0.72f, 0.56f, 0.94f), false);
            DrawPreparedItems(new Rect(44, 516, 270, 58), true, vm);
            DrawCaregiverBreathHotspot(vm, new Rect(38, 316, 286, 82), false);
        }

        private void DrawPortraitStatusOrnaments(V2PlayViewModel vm)
        {
            float y = 1562f;
            DrawStatusOrnament(new Rect(46, y, 310, 94), "현재 / 오늘 최장",
                $"{FormatDuration(vm.CurrentSleepStretchMinutes)} / {FormatDuration(vm.LongestSleepStretchMinutes)}",
                Mathf.Clamp01(vm.CurrentSleepStretchMinutes / 300f),
                new Color(0.4f, 0.72f, 0.91f), true);
            DrawStatusOrnament(new Rect(385, y, 310, 94), "보호자 체력",
                $"{vm.ParentStamina:0}",
                Mathf.Clamp01((float)vm.ParentStamina / 100f),
                vm.ParentStamina >= 30 ? new Color(0.49f, 0.84f, 0.61f) : new Color(0.94f, 0.39f, 0.34f), true);
            DrawStatusOrnament(new Rect(724, y, 310, 94), "마음의 여유",
                $"{vm.CaregiverComposure:0}",
                Mathf.Clamp01((float)vm.CaregiverComposure / 100f),
                new Color(0.72f, 0.56f, 0.94f), true);
            DrawCaregiverBreathHotspot(vm, new Rect(385, y, 310, 94), true);
        }

        private void DrawCaregiverBreathHotspot(V2PlayViewModel vm, Rect rect, bool portrait)
        {
            var action = DirectAction(vm, V2ActionId.CatchBreath);
            if (action == null) return;
            bool urgent = vm.ParentStamina <= 20 || vm.CaregiverComposure < 50;
            float pulse = (Mathf.Sin(Time.unscaledTime * 2.8f) + 1f) * .5f;
            if (urgent || rect.Contains(Event.current.mousePosition))
            {
                var old = GUI.color;
                GUI.color = new Color(.55f, .82f, 1f,
                    rect.Contains(Event.current.mousePosition) ? .5f : .12f + pulse * .22f);
                GUI.DrawTexture(rect, _itemGlow, ScaleMode.StretchToFill, true);
                GUI.color = old;
                GUI.Label(new Rect(rect.x, rect.y - (portrait ? 38 : 30), rect.width, portrait ? 38 : 30),
                    "내 숨을 한 번 고르기",
                    OverlayLabelStyle(portrait ? 18 : 14, FontStyle.Bold,
                        new Color(.72f, .9f, 1f), TextAnchor.MiddleCenter));
            }
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                PerformV2Action(V2ActionId.CatchBreath);
        }

        private void DrawStatusOrnament(Rect rect, string label, string value, float progress, Color color, bool portrait)
        {
            DrawGlassPanel(rect, portrait ? 0.5f : 0.56f);
            Fill(new Rect(rect.x + 12, rect.y + 14, 5, rect.height - 28), color);
            GUI.Label(new Rect(rect.x + 30, rect.y + 2, rect.width - 112, portrait ? 62 : 48), label,
                OverlayLabelStyle(portrait ? 27 : 16, FontStyle.Bold,
                    new Color(0.76f, 0.81f, 0.85f)));
            GUI.Label(new Rect(rect.xMax - 106, rect.y + 1, 86, portrait ? 64 : 50), value,
                OverlayLabelStyle(portrait ? 35 : 23, FontStyle.Bold,
                    new Color(0.98f, 0.94f, 0.86f), TextAnchor.MiddleRight));
            DrawProgress(new Rect(rect.x + 30, rect.yMax - (portrait ? 25 : 21), rect.width - 52,
                portrait ? 7 : 5), progress, color);
        }

        private void DrawSceneFeedback(V2PlayViewModel vm, Rect rect, bool portrait)
        {
            DrawGlassPanel(rect, 0.64f);
            string title = vm.CauseResolved ? "작은 숨소리가 방 안에 이어진다." : "무엇이 불편한 걸까?";
            string detail = BabyStepHint(vm);
            var outcome = _lastResult?.Outcome;
            if (outcome != null)
            {
                title = ActionFeedbackHeading(outcome);
                detail = OutcomeDetail(vm, outcome, detail);
            }
            else if (_lastMove != null && _lastMove.Accepted)
            {
                title = _lastMove.BabyAccompanied ? "아기를 안고 방을 옮겼다." : "필요한 물건을 가지러 왔다.";
                detail = $"{HomeLocationLabel(_lastMove.From)} → {HomeLocationLabel(_lastMove.To)} · {_lastMove.TimeDeltaMinutes}분";
            }
            string timer = !vm.CauseResolved ? "  ·  " + DecisionTimerCopy(vm) : "";
            GUI.Label(new Rect(rect.x + 28, rect.y + (portrait ? 4 : 1), rect.width - 56,
                    portrait ? 58 : 42),
                title + timer,
                OverlayLabelStyle(portrait ? 33 : 22, FontStyle.Bold,
                    new Color(0.98f, 0.9f, 0.76f)));
            GUI.Label(new Rect(rect.x + 28, rect.y + (portrait ? 56 : 38), rect.width - 56,
                    portrait ? 58 : 38),
                detail,
                OverlayLabelStyle(portrait ? 27 : 18, FontStyle.Normal,
                    new Color(0.83f, 0.86f, 0.88f), TextAnchor.MiddleLeft, true));
        }

        private void DrawLandscapeCommandDeck(V2PlayViewModel vm)
        {
            Fill(new Rect(0, 780, LandscapeWidth, 300), new Color(0.012f, 0.025f, 0.045f, 0.46f));
            Fill(new Rect(0, 780, LandscapeWidth, 2), new Color(0.84f, 0.62f, 0.31f, 0.72f));
            GUI.Label(new Rect(48, 806, 720, 40),
                vm.ParentStamina <= 0 ? "보호자도 돌봄이 필요해요" :
                vm.CaregiverLocation == HomeLocation.Nursery
                    ? "아기를 직접 만져 돌봐주세요"
                    : "반짝이는 물건을 직접 집어보세요",
                LabelStyle(26, FontStyle.Bold, new Color(0.98f, 0.91f, 0.78f)));
            GUI.Label(new Rect(48, 850, 760, 32),
                "아기와 방 안 오브젝트가 주 행동 경로예요 · 아래는 관찰과 접근성 보조",
                _caption);

            if (IsSleeping(vm))
            {
                GUI.Label(new Rect(48, 900, 300, 28), "아기가 자는 동안", _caption);
                DrawSleepIntervalChoices(new Rect(48, 938, 560, 54), true);
            }
            DrawUtilityActions(vm, new Rect(850, 824, 1020, 170), false);
        }

        private void DrawCommandTab(Rect rect, string label, ActionGroup group, bool enabled)
        {
            bool selected = _actionGroup == group;
            DrawGlassPanel(rect, selected ? 0.82f : 0.58f, selected);
            GUI.Label(rect, label, LabelStyle(_portrait ? 28 : 18, FontStyle.Bold,
                selected ? Color.white : new Color(0.78f, 0.81f, 0.83f), TextAnchor.MiddleCenter));
            var old = GUI.enabled;
            GUI.enabled = old && enabled;
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) _actionGroup = group;
            GUI.enabled = old;
        }

        private void DrawCommandActions(V2PlayViewModel vm, Rect area, bool portrait)
        {
            var actions = ActionsFor(_actionGroup, vm.ParentStamina <= 0);
            int columns = portrait ? 2 : 4;
            float gapX = portrait ? 18f : 16f;
            float gapY = portrait ? 18f : 14f;
            float width = (area.width - gapX * (columns - 1)) / columns;
            float height = portrait ? 94f : 92f;
            int visibleIndex = 0;
            for (int i = 0; i < actions.Length; i++)
            {
                var action = vm.Actions.Find(a => a.Action == actions[i]);
                if (action == null || !action.Enabled) continue;
                int col = visibleIndex % columns;
                int row = visibleIndex / columns;
                var rect = new Rect(area.x + col * (width + gapX), area.y + row * (height + gapY), width, height);
                var old = GUI.enabled;
                GUI.enabled = old && action.Enabled && !_flow.InputLocked;
                if (DrawActionButton(rect, action, vm, portrait)) PerformV2Action(action.Action);
                GUI.enabled = old;
                visibleIndex++;
            }
        }

        private void DrawRoomFocusBackdrop(HomeLocation location, Rect rect)
        {
            Texture2D backdrop = RoomBackdrop(location);
            if (backdrop != null)
                GUI.DrawTexture(rect, backdrop, ScaleMode.ScaleAndCrop, true);
            if (!RoomTransitionActive()) return;

            Texture2D previous = RoomBackdrop(_roomTransitionFrom);
            if (previous == null) return;
            float fade = 1f - Mathf.SmoothStep(0f, 1f, RoomTransitionProgress());
            var old = GUI.color;
            GUI.color = new Color(old.r, old.g, old.b, old.a * fade);
            GUI.DrawTexture(rect, previous, ScaleMode.ScaleAndCrop, true);
            GUI.color = old;
        }

        private Texture2D RoomBackdrop(HomeLocation location) => location switch
        {
            HomeLocation.Kitchen => _kitchenRoom,
            HomeLocation.Bathroom => _bathroomRoom,
            _ => _room
        };

        private void HandleRoomMovementKeys(V2PlayViewModel vm)
        {
            var currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.KeyDown ||
                _flow.InputLocked || RoomTransitionActive() ||
                Time.unscaledTime < _nextKeyboardMoveAt) return;

            HomeLocation? destination = null;
            switch (currentEvent.keyCode)
            {
                case KeyCode.W:
                    destination = vm.CaregiverLocation == HomeLocation.Bathroom
                        ? HomeLocation.Kitchen : (HomeLocation?)null;
                    break;
                case KeyCode.A:
                    destination = vm.CaregiverLocation == HomeLocation.Nursery
                        ? (HomeLocation?)null : HomeLocation.Nursery;
                    break;
                case KeyCode.S:
                    destination = vm.CaregiverLocation == HomeLocation.Nursery
                        ? HomeLocation.Bathroom :
                        vm.CaregiverLocation == HomeLocation.Kitchen ? HomeLocation.Bathroom : (HomeLocation?)null;
                    break;
                case KeyCode.D:
                    destination = vm.CaregiverLocation == HomeLocation.Nursery
                        ? HomeLocation.Kitchen : (HomeLocation?)null;
                    break;
            }
            if (!destination.HasValue) return;
            _nextKeyboardMoveAt = Time.unscaledTime + 0.25f;
            MoveToRoom(destination.Value);
            currentEvent.Use();
        }

        private void MoveToRoom(HomeLocation location)
        {
            _lastMove = _flow.MoveToHomeLocation(location);
            _lastResult = null;
            if (_lastMove.Accepted)
            {
                _roomTransitionFrom = _lastMove.From;
                _roomTransitionTo = _lastMove.To;
                _roomTransitionBabyAccompanied = _lastMove.BabyAccompanied;
                _roomTransitionStarted = Time.unscaledTime;
                _audio.PlayMove();
                TriggerImpact(new Color(0.45f, 0.68f, 0.8f, 0.3f), 2f, 0.2f);
            }
        }

        private bool RoomTransitionActive()
            => Time.unscaledTime - _roomTransitionStarted < RoomTransitionDuration;

        private float RoomTransitionProgress()
            => Mathf.Clamp01((Time.unscaledTime - _roomTransitionStarted) / RoomTransitionDuration);

        private static Color RoomFocusTint(HomeLocation location) => location switch
        {
            HomeLocation.Kitchen => new Color(0.12f, 0.085f, 0.045f, 0.42f),
            HomeLocation.Bathroom => new Color(0.035f, 0.09f, 0.11f, 0.48f),
            _ => new Color(0.02f, 0.055f, 0.09f, 0.36f)
        };

        private static string RoomFocusObjective(V2PlayViewModel vm) => vm.CaregiverLocation switch
        {
            HomeLocation.Kitchen => "분유와 물을 준비할 수 있어요",
            HomeLocation.Bathroom => vm.BathThermometerRetrieved
                ? "탕온계를 챙겼어요"
                : "세면대 위 탕온계를 챙길 수 있어요",
            _ => "아기에게 돌아왔어요"
        };

        private void DrawSignalMotionCue(V2PlayViewModel vm, Rect babyRect, bool portrait)
        {
            if (_animatedAction.HasValue) return;
            string cue = null;
            if (vm.CurrentSignal.Contains("입맛")) cue = "쩝… 쩝…";
            else if (vm.CurrentSignal.Contains("손을 입")) cue = "손을 입으로";
            else if (vm.CurrentSignal.Contains("하품")) cue = "하—암";
            else if (vm.CurrentSignal.Contains("눈을 비비")) cue = "눈을 슥슥";
            else if (vm.CurrentSignal.Contains("숨")) cue = "후…  후…";
            else if (vm.CurrentSignal.Contains("꼼지락")) cue = "꼼지락";
            if (string.IsNullOrEmpty(cue)) return;

            float pulse = (Mathf.Sin(Time.unscaledTime * 3f) + 1f) * 0.5f;
            var cueRect = new Rect(babyRect.center.x - (portrait ? 116f : 126f),
                babyRect.yMax - (portrait ? 88f : 96f), portrait ? 232f : 252f, portrait ? 54f : 58f);
            DrawGlassPanel(cueRect, 0.58f);
            var style = OverlayLabelStyle(portrait ? 22 : 25, FontStyle.Bold,
                Color.Lerp(new Color(0.95f, 0.72f, 0.45f), new Color(0.55f, 0.9f, 0.72f), pulse),
                TextAnchor.MiddleCenter);
            GUI.Label(cueRect, cue, style);

            V2ActionId cueAction = vm.FeedingReady
                ? V2ActionId.FeedPreparedBottle
                : !vm.CauseResolved ? V2ActionId.CheckHungerSignals : V2ActionId.Pacifier;
            if (DirectAction(vm, cueAction) == null) return;

            var old = GUI.color;
            GUI.color = new Color(1f, 0.76f, 0.38f, 0.08f + pulse * 0.1f);
            GUI.DrawTexture(cueRect, _itemGlow, ScaleMode.StretchToFill, true);
            GUI.color = old;
            if (GUI.Button(cueRect, GUIContent.none, GUIStyle.none))
            {
                _directHintSeen = true;
                PerformV2Action(cueAction);
            }
        }

        private float BabyActionProgress()
        {
            if (!_animatedAction.HasValue) return 1f;
            float progress = (Time.unscaledTime - _actionAnimationStarted) /
                ActionAnimationDuration(_animatedAction.Value);
            if (progress < 1f) return Mathf.Clamp01(progress);
            _animatedAction = null;
            return 1f;
        }

        private static float ActionAnimationDuration(V2ActionId action) => action switch
        {
            V2ActionId.Pat => 1.8f,
            V2ActionId.Hold => 1.7f,
            V2ActionId.ToggleCarrier => 1.5f,
            V2ActionId.FeedPreparedBottle => 1.8f,
            V2ActionId.Pacifier => 1.5f,
            _ => DefaultActionAnimationDuration
        };

        private Rect AnimatedBabyActionRect(Rect rect)
        {
            float progress = BabyActionProgress();
            if (!_animatedAction.HasValue || progress >= 1f) return rect;
            float lift = Mathf.Sin(progress * Mathf.PI);
            switch (_animatedAction.Value)
            {
                case V2ActionId.Hold:
                case V2ActionId.ToggleCarrier:
                    rect.y -= lift * 54f;
                    rect.x += Mathf.Sin(progress * Mathf.PI * 4f) * 9f * lift;
                    break;
                case V2ActionId.Pat:
                    rect.x += Mathf.Sin(progress * Mathf.PI * 6f) * 4f * lift;
                    break;
                case V2ActionId.Laydown:
                    rect.y -= (1f - progress) * 34f;
                    break;
                case V2ActionId.CheckDiaper:
                case V2ActionId.ChangeDiaper:
                    rect.y -= lift * 15f;
                    rect.x += Mathf.Sin(progress * Mathf.PI * 2f) * 3f * lift;
                    break;
            }
            return rect;
        }

        private void DrawBabyActionAnimation(Rect babyRect, bool portrait)
        {
            float progress = BabyActionProgress();
            if (!_animatedAction.HasValue || progress >= 1f) return;
            V2ActionId action = _animatedAction.Value;
            float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 4f));
            float leave = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((progress - 0.72f) / 0.28f));
            float presence = Mathf.Min(enter, leave);
            float beat = (Mathf.Sin(progress * Mathf.PI * 6f) + 1f) * 0.5f;
            float handWidth = portrait ? 72f : 82f;
            float handHeight = portrait ? 46f : 52f;

            switch (action)
            {
                case V2ActionId.CheckDiaper:
                case V2ActionId.ChangeDiaper:
                {
                    float targetY = babyRect.y + babyRect.height * 0.7f;
                    DrawCaregiverHand(new Rect(
                        Mathf.Lerp(babyRect.x - handWidth, babyRect.center.x - handWidth * 1.05f, presence),
                        targetY, handWidth, handHeight));
                    DrawCaregiverHand(new Rect(
                        Mathf.Lerp(babyRect.xMax, babyRect.center.x + handWidth * 0.05f, presence),
                        targetY, handWidth, handHeight), true);
                    if (action == V2ActionId.ChangeDiaper)
                    {
                        float clothWidth = portrait ? 170f : 205f;
                        float clothHeight = portrait ? 80f : 94f;
                        var cloth = new Rect(babyRect.center.x - clothWidth * 0.5f,
                            Mathf.Lerp(babyRect.yMax + 70f, babyRect.y + babyRect.height * 0.72f, presence),
                            clothWidth, clothHeight);
                        GUI.DrawTexture(cloth, _diaperCloth, ScaleMode.StretchToFill, true);
                        Fill(new Rect(cloth.x + 20f, cloth.y + cloth.height * 0.46f,
                            cloth.width - 40f, 3f), new Color(0.86f, 0.68f, 0.4f, 0.75f));
                        DrawCareSparkles(new Vector2(cloth.center.x, cloth.y + 8f), presence, 3);
                    }
                    DrawActionMotionLabel(babyRect, action == V2ActionId.ChangeDiaper
                        ? MotionStage(progress, "두 다리를 살짝 들어요", "깨끗하게 갈아입혀요", "보송하게, 옷매무새까지")
                        : MotionStage(progress, "두 다리를 살짝 들어요", "기저귀를 조심히 살펴봐요", "다시 포근하게 덮어줘요"),
                        portrait);
                    break;
                }
                case V2ActionId.CheckHungerSignals:
                {
                    var hand = new Rect(
                        Mathf.Lerp(babyRect.xMax + 30f, babyRect.center.x + babyRect.width * 0.22f, presence),
                        babyRect.y + babyRect.height * 0.36f, handWidth * 0.82f, handHeight * 0.82f);
                    DrawCaregiverHand(hand, true);
                    for (int i = 0; i < 3; i++)
                    {
                        float dot = (progress * 1.8f + i * 0.24f) % 1f;
                        float size = 10f + (1f - dot) * 7f;
                        GUI.DrawTexture(new Rect(babyRect.center.x + 42f + dot * 48f,
                            babyRect.y + babyRect.height * 0.29f - dot * 12f, size, size),
                            _itemGlow, ScaleMode.StretchToFill, true);
                    }
                    DrawActionMotionLabel(babyRect,
                        MotionStage(progress, "손을 입가 가까이 가져가요", "고개와 입의 방향을 기다려요", "아기의 대답을 기억해요"),
                        portrait);
                    break;
                }
                case V2ActionId.CheckBodyTemperature:
                {
                    var hand = new Rect(
                        Mathf.Lerp(babyRect.xMax + 30f, babyRect.center.x + babyRect.width * 0.14f, presence),
                        babyRect.y + babyRect.height * 0.13f, handWidth * 0.82f, handHeight * 0.82f);
                    DrawCaregiverHand(hand, true);
                    DrawActionMotionLabel(babyRect, "이마에 손을 가만히 대봐요", portrait);
                    break;
                }
                case V2ActionId.CheckLimbRelaxation:
                {
                    float handY = babyRect.y + babyRect.height * (0.69f + beat * 0.03f);
                    DrawCaregiverHand(new Rect(
                        Mathf.Lerp(babyRect.x - handWidth, babyRect.center.x - handWidth * 1.2f, presence),
                        handY, handWidth, handHeight));
                    DrawCaregiverHand(new Rect(
                        Mathf.Lerp(babyRect.xMax, babyRect.center.x + handWidth * 0.2f, presence),
                        handY, handWidth, handHeight), true);
                    DrawActionMotionLabel(babyRect,
                        MotionStage(progress, "발끝부터 가볍게 받쳐요", "팔다리의 힘을 천천히 확인", "깊어진 숨을 방해하지 않아요"),
                        portrait);
                    break;
                }
                case V2ActionId.Hold:
                case V2ActionId.ToggleCarrier:
                {
                    float handY = babyRect.y + babyRect.height * 0.62f;
                    DrawCaregiverHand(new Rect(babyRect.center.x - handWidth * 1.35f,
                        handY, handWidth, handHeight * 1.35f));
                    DrawCaregiverHand(new Rect(babyRect.center.x + handWidth * 0.35f,
                        handY, handWidth, handHeight * 1.35f), true);
                    DrawCareSparkles(new Vector2(babyRect.center.x, babyRect.y + 55f), presence, 4);
                    DrawActionMotionLabel(babyRect,
                        action == V2ActionId.ToggleCarrier
                            ? MotionStage(progress, "아기띠를 넓게 펼쳐요", "등과 목을 포근하게 감싸요", "아빠 심장 가까이에 꼭")
                            : MotionStage(progress, "한 손으로 목을 받쳐요", "가슴 가까이 천천히 안아요", "같은 리듬으로 살랑살랑"),
                        portrait);
                    break;
                }
                case V2ActionId.Pat:
                {
                    float patY = babyRect.y + babyRect.height * (0.36f + beat * 0.16f);
                    DrawCaregiverHand(new Rect(babyRect.xMax - handWidth * 0.78f, patY,
                        handWidth, handHeight * 1.2f), true);
                    for (int i = 0; i < 3; i++)
                        Fill(new Rect(babyRect.xMax + 12f + i * 14f,
                            patY + handHeight * 0.35f, 7f, 3f),
                            new Color(1f, 0.73f, 0.34f, presence * (0.9f - i * 0.2f)));
                    DrawCareSparkles(new Vector2(babyRect.xMax - 10f, patY - 8f), presence, 2);
                    DrawActionMotionLabel(babyRect,
                        MotionStage(progress, "등에 손바닥을 포근히", "토닥 · 토닥 · 천천히", "숨이 맞춰지면 손을 쉬어요"),
                        portrait);
                    break;
                }
                case V2ActionId.Pacifier:
                {
                    float size = portrait ? 72f : 86f;
                    var itemRect = new Rect(
                        Mathf.Lerp(babyRect.xMax + size, babyRect.center.x + babyRect.width * 0.04f, presence),
                        babyRect.y + babyRect.height * 0.3f, size, size);
                    DrawItemArt(ItemId.Pacifier, itemRect);
                    DrawActionMotionLabel(babyRect,
                        MotionStage(progress, "입가 가까이 보여줘요", "아기가 물 때까지 기다려요", "입술이 편안한지 살펴봐요"),
                        portrait);
                    break;
                }
                case V2ActionId.FeedPreparedBottle:
                {
                    float bottleWidth = portrait ? 72f : 82f;
                    float bottleHeight = portrait ? 150f : 172f;
                    var bottle = new Rect(
                        Mathf.Lerp(babyRect.xMax + bottleWidth, babyRect.center.x + babyRect.width * 0.12f, presence),
                        babyRect.y + babyRect.height * 0.27f, bottleWidth, bottleHeight);
                    if (_feedingBottleArt != null)
                        GUI.DrawTexture(bottle, _feedingBottleArt, ScaleMode.ScaleToFit, true);
                    DrawCareSparkles(new Vector2(bottle.x, bottle.y + bottle.height * 0.42f), presence, 2);
                    DrawActionMotionLabel(babyRect,
                        MotionStage(progress, "목과 등을 안정적으로 받쳐요", "삼키는 리듬에 맞춰 수유", "입가를 닦고 숨을 기다려요"),
                        portrait);
                    break;
                }
                case V2ActionId.Laydown:
                {
                    DrawCaregiverHand(new Rect(babyRect.center.x - handWidth * 1.3f,
                        babyRect.y + babyRect.height * 0.68f, handWidth, handHeight));
                    DrawCaregiverHand(new Rect(babyRect.center.x + handWidth * 0.3f,
                        babyRect.y + babyRect.height * 0.68f, handWidth, handHeight), true);
                    DrawActionMotionLabel(babyRect,
                        MotionStage(progress, "머리와 등을 함께 받쳐요", "숨의 리듬을 지키며 내려가요", "손을 천천히 빼고 기다려요"),
                        portrait);
                    break;
                }
            }
        }

        private static string MotionStage(float progress, string enter, string contact, string settle)
            => progress < 0.32f ? enter : progress < 0.72f ? contact : settle;

        private void DrawCareSparkles(Vector2 center, float presence, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float phase = Time.unscaledTime * 2.2f + i * 1.7f;
                float radius = 24f + i * 17f;
                float x = center.x + Mathf.Cos(phase) * radius;
                float y = center.y + Mathf.Sin(phase * 0.83f) * radius * 0.62f;
                float size = 5f + (i % 2) * 3f;
                Color color = new Color(1f, 0.75f + i * 0.035f, 0.46f,
                    presence * (0.78f - i * 0.08f));
                Fill(new Rect(x - size * 0.5f, y - 1f, size, 2f), color);
                Fill(new Rect(x - 1f, y - size * 0.5f, 2f, size), color);
            }
        }

        private void DrawCaregiverHand(Rect rect, bool mirror = false)
        {
            if (_caregiverHand == null) return;

            // 원화는 손가락과 소매가 한 실루엣으로 완결돼 있다. 조각 텍스처를 겹치지 않고
            // 좌우 반전만 사용해 양손의 해부학적 형태와 화풍을 보존한다.
            Matrix4x4 previous = GUI.matrix;
            if (!mirror)
                GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), rect.center);
            GUI.DrawTexture(rect, _caregiverHand, ScaleMode.ScaleToFit, true);
            GUI.matrix = previous;
        }

        private void DrawActionMotionLabel(Rect babyRect, string label, bool portrait)
        {
            var rect = new Rect(babyRect.center.x - (portrait ? 215f : 245f),
                babyRect.yMax - (portrait ? 72f : 70f),
                portrait ? 430f : 490f, portrait ? 54f : 58f);
            GUI.Label(rect, label, OverlayLabelStyle(portrait ? 24 : 23, FontStyle.Bold,
                new Color(1f, 0.87f, 0.64f), TextAnchor.MiddleCenter));
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
            if (_familyRolled && outcome == null &&
                _babyVisual.Resolve(vm, null) == BabyVisualPresenter.VisualState.AwakeCalm)
            {
                current = GeneticBabyPortrait();
                previous = current;
            }
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
            if (_animatedAction.HasValue || IsSleeping(vm) ||
                Time.unscaledTime >= _babbleUntil || string.IsNullOrEmpty(_currentBabble)) return;

            var bubble = new Rect(babyRect.xMax - (portrait ? 88f : 108f),
                babyRect.y + (portrait ? 38f : 42f), portrait ? 180f : 200f,
                portrait ? 58f : 64f);
            GUI.Label(bubble, _currentBabble,
                OverlayLabelStyle(portrait ? 27 : 28, FontStyle.Bold,
                    new Color(1f, .9f, .72f), TextAnchor.MiddleCenter));
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
            Panel(new Rect(48, 920, 984, 250), 0.72f);
            GUI.Label(new Rect(82, 950, 900, 42), !vm.CauseResolved ? DecisionTimerCopy(vm, "결정까지 ") : "방금 일어난 일", _caption);
            string title = vm.CauseResolved ? "호흡과 몸의 힘을 살핀다." : "무엇이 불편한 걸까?";
            string detail = vm.CurrentSignal;
            var outcome = _lastResult?.Outcome;
            if (outcome != null)
            {
                title = ActionFeedbackHeading(outcome);
                detail = OutcomeDetail(vm, outcome, detail);
            }
            else if (_lastMove != null && _lastMove.Accepted)
            {
                title = _lastMove.BabyAccompanied ? "아기를 안고 방을 옮겼다." : "필요한 물건을 가지러 왔다.";
                detail = $"{HomeLocationLabel(_lastMove.From)} → {HomeLocationLabel(_lastMove.To)} · {_lastMove.TimeDeltaMinutes}분 경과" +
                    (_lastMove.RetrievedBathThermometer ? " · 탕온계를 챙겼어요." : "");
            }
            GUI.Label(new Rect(82, 1002, 900, 52), title, _headline);
            GUI.Label(new Rect(82, 1062, 900, 76), detail, _body);
        }

        private void DrawPortraitActions(V2PlayViewModel vm)
        {
            Fill(new Rect(0, 1060, PortraitWidth, 860), new Color(0.012f, 0.025f, 0.045f, 0.42f));
            Fill(new Rect(0, 1060, PortraitWidth, 3), new Color(0.84f, 0.62f, 0.31f, 0.72f));
            GUI.Label(new Rect(48, 1090, 984, 52),
                vm.ParentStamina <= 0 ? "보호자도 돌봄이 필요해요" :
                vm.CaregiverLocation == HomeLocation.Nursery
                    ? "아기를 직접 만져 돌봐주세요"
                    : "반짝이는 물건을 직접 집어보세요",
                LabelStyle(33, FontStyle.Bold, new Color(0.98f, 0.91f, 0.78f)));
            GUI.Label(new Rect(48, 1144, 984, 58),
                "아기 탭 → 가까이 뜨는 행동 선택 · 아래는 관찰과 접근성 보조",
                _caption);
            if (IsSleeping(vm))
                DrawSleepIntervalChoices(new Rect(48, 1210, 984, 66), true);
            DrawUtilityActions(vm, new Rect(48, IsSleeping(vm) ? 1300 : 1225, 984, 330), true);
        }

        private void DrawUtilityActions(V2PlayViewModel vm, Rect area, bool portrait)
        {
            var ids = vm.ParentStamina <= 0
                ? new[] { V2ActionId.CatchBreath }
                : new[] { V2ActionId.CheckMonitor, V2ActionId.CatchBreath, V2ActionId.Hesitate };
            int visible = 0;
            foreach (var id in ids)
            {
                var action = DirectAction(vm, id);
                if (action == null) continue;
                int columns = portrait ? 2 : 3;
                float gap = portrait ? 18f : 16f;
                float width = (area.width - gap * (columns - 1)) / columns;
                int col = visible % columns;
                int row = visible / columns;
                var rect = new Rect(area.x + col * (width + gap),
                    area.y + row * (portrait ? 112f : 92f), width, portrait ? 94f : 76f);
                if (DrawActionButton(rect, action, vm, portrait)) PerformV2Action(id);
                visible++;
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
            string overlayAction = _flow.Session.Night.Over ? "밤의 기록 보기" : "계속 돌보기";
            if (GUI.Button(new Rect(190, 1080, 700, 100), overlayAction, _button))
            {
                _audio.PlayUi();
                _flow.DismissOverlay();
            }
        }

        private void DrawStatusPanel(V2PlayViewModel vm)
        {
            var panel = new Rect(48, 132, 360, 858);
            Panel(panel, 0.72f);
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
            GUI.Label(new Rect(74, 680, 280, 28), "마음의 여유", _caption);
            GUI.Label(new Rect(74, 712, 280, 48), $"{vm.CaregiverComposure:0}", _headline);
            DrawProgress(new Rect(74, 768, 280, 10), Mathf.Clamp01((float)vm.CaregiverComposure / 100f), new Color(0.66f, 0.53f, 0.92f));

            if (vm.TemperatureChecked || vm.HumidityChecked)
                GUI.Label(new Rect(74, 830, 280, 70),
                    $"방  {vm.TemperatureCelsius:0.#}°C\n습도 {vm.HumidityPercent:0.#}%", _body);
            DrawPreparedItems(new Rect(74, 920, 280, 54), true, vm);
        }

        private void DrawActionPanel(V2PlayViewModel vm)
        {
            var panel = new Rect(1430, 132, 442, 858);
            Panel(panel, 0.72f);
            GUI.Label(new Rect(1460, 162, 380, 36),
                vm.ParentStamina <= 0 ? "먼저 숨을 고르세요" : "어떻게 할까요?", _headline);

            bool exhausted = vm.ParentStamina <= 0;
            DrawTab(new Rect(1460, 220, 120, 48), "살펴보기", ActionGroup.Diagnose);
            DrawTab(new Rect(1589, 220, 120, 48), "돌보기", ActionGroup.Care, !exhausted);
            DrawTab(new Rect(1718, 220, 120, 48), "수유 준비", ActionGroup.Feed, !exhausted);

            var actions = ActionsFor(_actionGroup, vm.ParentStamina <= 0);
            float y = 292;
            for (int i = 0; i < actions.Length; i++)
            {
                var id = actions[i];
                var action = vm.Actions.Find(a => a.Action == id);
                if (action == null || !action.Enabled) continue;
                var oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && action.Enabled && !_flow.InputLocked;
                if (DrawActionButton(new Rect(1460, y, 378, 64), action, vm, false))
                    PerformV2Action(id);
                GUI.enabled = oldEnabled;
                y += 74;
            }

        }

        private void DrawSleepIntervalChoices(Rect rect, bool horizontal)
        {
            var oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && !_flow.InputLocked;
            var choices = new[]
            {
                (SleepIntervalChoice.RestTogether, "같이 쉬기"),
                (SleepIntervalChoice.CheckEnvironment, "환경 점검"),
                (SleepIntervalChoice.PrepareNextFeed, "다음 수유 준비")
            };
            for (int i = 0; i < choices.Length; i++)
            {
                Rect button = horizontal
                    ? new Rect(rect.x + i * (rect.width / 3f), rect.y, rect.width / 3f - 10, rect.height)
                    : new Rect(rect.x, rect.y + i * 58, rect.width, 50);
                if (GUI.Button(button, choices[i].Item2, _buttonSmall) &&
                    _flow.ChooseV2SleepInterval(choices[i].Item1))
                {
                    _lastResult = null;
                    _audio.PlayUi();
                    TriggerImpact(new Color(0.36f, 0.62f, 0.78f, 0.22f), 1.5f, 0.22f);
                }
            }
            GUI.enabled = oldEnabled;
        }

        private void DrawEventPanel(V2PlayViewModel vm)
        {
            var rect = new Rect(448, 790, 934, 200);
            Panel(rect, 0.72f);
            GUI.Label(new Rect(478, 816, 840, 28), !vm.CauseResolved ? DecisionTimerCopy(vm, "결정까지  ") : "방금 일어난 일", _caption);

            string title = "작은 숨소리가 방 안에 이어진다.";
            string detail = BabyStepHint(vm);
            var outcome = _lastResult?.Outcome;
            if (outcome != null)
            {
                title = ActionFeedbackHeading(outcome);
                detail = OutcomeDetail(vm, outcome, detail);
            }
            GUI.Label(new Rect(478, 856, 840, 42), title, _headline);
            GUI.Label(new Rect(478, 910, 840, 52), detail, _body);
        }

        private void DrawTab(Rect rect, string label, ActionGroup group, bool enabled = true)
        {
            GUIStyle normal = _portrait ? _buttonSmall : _tabButton;
            GUIStyle selected = _portrait ? _buttonSelected : _tabSelected;
            var oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && enabled;
            if (GUI.Button(rect, label, _actionGroup == group ? selected : normal)) _actionGroup = group;
            GUI.enabled = oldEnabled;
        }

        private static V2ActionId[] ActionsFor(ActionGroup group, bool caregiverExhausted)
        {
            // 탈진 중에는 현재 탭과 무관하게 유일한 회복 행동을 바로 보여준다.
            if (caregiverExhausted)
                return new[] { V2ActionId.CatchBreath };
            switch (group)
            {
                case ActionGroup.Diagnose:
                    return new[] { V2ActionId.CheckDiaper, V2ActionId.CheckHungerSignals, V2ActionId.CheckEnvironment, V2ActionId.CheckMonitor, V2ActionId.CheckLimbRelaxation, V2ActionId.Hesitate, V2ActionId.CatchBreath };
                case ActionGroup.Care:
                    return new[] { V2ActionId.Hold, V2ActionId.ToggleCarrier, V2ActionId.Pat, V2ActionId.Pacifier, V2ActionId.ToggleNoise, V2ActionId.Laydown, V2ActionId.ChangeDiaper, V2ActionId.AdjustTemperature, V2ActionId.AdjustHumidity };
                default:
                    return new[] { V2ActionId.SterilizeBottle, V2ActionId.PrepareWater, V2ActionId.CoolBottle, V2ActionId.FeedPreparedBottle };
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
            if (_flow.InputLocked)
            {
                _decisionDeadline = Time.unscaledTime + vm.DecisionSecondsRemaining;
                return Mathf.Max(0, vm.DecisionSecondsRemaining);
            }
            int remaining = Mathf.Max(0, Mathf.CeilToInt(_decisionDeadline - Time.unscaledTime));
            if (remaining == 0 && !_timeoutSent && !_flow.InputLocked)
            {
                _timeoutSent = true;
                PerformV2Action(V2ActionId.Hesitate);
            }
            return remaining;
        }

        private string DecisionTimerCopy(V2PlayViewModel vm, string prefix = "")
        {
            int remaining = UpdateDecisionTimer(vm);
            return _timeoutSent && remaining <= 0 ? prefix + "시간 초과" : $"{prefix}{remaining}초";
        }

        private void PerformV2Action(V2ActionId action)
        {
            _lastResult = _flow.ActV2(action);
            var outcome = _lastResult?.Outcome;
            if (outcome == null) return;
            _directHintSeen = true;
            _directCueHiddenUntil = Time.unscaledTime + 1.8f;
            if (outcome.Accepted && IsBabyInteractionAction(action))
            {
                _animatedAction = action;
                _actionAnimationStarted = Time.unscaledTime;
            }
            _audio.PlayAction(outcome);
            bool success = outcome.EventIds.Contains(GameEventId.LaydownSucceeded);
            bool failure = !outcome.Accepted ||
                outcome.EventIds.Contains(GameEventId.LaydownFailed) ||
                outcome.EventIds.Contains(GameEventId.BabyFullyWoke);
            TriggerImpact(
                success ? new Color(0.45f, 0.88f, 0.62f, 0.5f) :
                failure ? new Color(0.95f, 0.36f, 0.28f, 0.5f) :
                new Color(0.95f, 0.72f, 0.38f, 0.3f),
                success ? 3f : failure ? 9f : 2f,
                failure ? 0.34f : 0.24f);
        }

        private static bool IsBabyInteractionAction(V2ActionId action) => action switch
        {
            V2ActionId.CheckDiaper => true,
            V2ActionId.CheckHungerSignals => true,
            V2ActionId.CheckLimbRelaxation => true,
            V2ActionId.CheckBodyTemperature => true,
            V2ActionId.Hold => true,
            V2ActionId.ToggleCarrier => true,
            V2ActionId.Pat => true,
            V2ActionId.Pacifier => true,
            V2ActionId.Laydown => true,
            V2ActionId.ChangeDiaper => true,
            V2ActionId.FeedPreparedBottle => true,
            _ => false
        };

        private void TriggerImpact(Color color, float strength, float duration)
        {
            _impactStarted = Time.unscaledTime;
            _impactColor = color;
            _impactStrength = strength;
            _impactDuration = duration;
        }

        private static string ActionFeedbackTitle(V2ActionOutcome outcome)
        {
            if (outcome.WasMisdiagnosis && outcome.Action == V2ActionId.FeedPreparedBottle)
                return "삼키는 리듬이 급하지 않다. 배고픔이 먼저인 밤은 아니었나 보다.";
            if (outcome.WasMisdiagnosis)
                return "이 돌봄보다 먼저 살펴야 할 다른 불편이 남아 있다.";
            if (outcome.Action == V2ActionId.CheckHungerSignals &&
                !outcome.HungerSignalsMatchCause)
                return "입은 움직이지만 몸의 불편은 다른 곳에서 먼저 오는 듯하다.";
            if (outcome.EventIds.Contains(GameEventId.LaydownSucceeded))
                return "숨이 그대로 이어진다.";
            if (outcome.EventIds.Contains(GameEventId.LaydownFailed) ||
                outcome.EventIds.Contains(GameEventId.BabyFullyWoke))
                return "등이 닿자 몸이 움찔했다.";
            return outcome.Action switch
            {
                V2ActionId.Hold => "품 안에서 어깨의 힘이 조금 풀린다.",
                V2ActionId.Pat => "토닥임에 호흡이 천천히 맞춰진다.",
                V2ActionId.CatchBreath => "한 번 길게 숨을 내쉰다.",
                V2ActionId.Grandma => "도움을 건네받자 아기의 몸이 포근한 품으로 기대온다.",
                V2ActionId.CheckHungerSignals => "입과 손의 움직임을 살폈다.",
                V2ActionId.CheckDiaper => "불편한 곳부터 차례로 확인했다.",
                V2ActionId.CheckEnvironment => "방 안의 공기를 살폈다.",
                V2ActionId.PrepareWater => "젖병에 따뜻한 물을 준비했다.",
                V2ActionId.CoolBottle => "손목에 닿는 온도를 확인했다.",
                V2ActionId.FeedPreparedBottle => "아기의 삼키는 리듬을 기다렸다.",
                _ => "아기가 작은 움직임으로 답한다."
            };
        }

        private static string ActionFeedbackHeading(V2ActionOutcome outcome)
        {
            if (outcome.Accepted) return ActionFeedbackTitle(outcome);
            if (outcome.BlockReason != V2ActionBlockReason.None) return "먼저 필요한 돌봄이 있어요.";
            if (outcome.EventIds.Contains(GameEventId.LaydownFailed) ||
                outcome.EventIds.Contains(GameEventId.BabyFullyWoke))
                return "아기가 몸으로 ‘조금 더 기다려줘’라고 답했다.";
            return "이 방법은 지금 아기의 답과 달랐다.";
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
            string overlayAction = _flow.Session.Night.Over ? "밤의 기록 보기" : "계속 돌보기";
            if (GUI.Button(new Rect(760, 650, 400, 66), overlayAction, _button))
            {
                _audio.PlayUi();
                _flow.DismissOverlay();
            }
        }

        private void DrawDiary()
        {
            var vm = _flow.BuildV2Diary();
            if (_portrait) { DrawPortraitDiary(vm); return; }
            Fill(new Rect(0, 0, LandscapeWidth, LandscapeHeight), new Color(0.015f, 0.035f, 0.065f, 0.84f));
            GUI.Label(new Rect(110, 76, 1100, 58), $"{_flow.BabyName} · {vm.NightLabel} 밤의 기록", _display);
            Panel(new Rect(110, 200, 560, 680));
            GUI.Label(new Rect(155, 245, 470, 34), "밤이 남긴 것", _caption);
            GUI.Label(new Rect(155, 292, 470, 126), vm.CaregiverGrowth, _headline);
            GUI.Label(new Rect(155, 440, 470, 92), vm.Encouragement, _body);
            Fill(new Rect(155, 552, 420, 2), new Color(.84f, .62f, .31f, .5f));
            GUI.Label(new Rect(155, 574, 470, 34), $"밤의 기록 · {PresentationCopyMapper.NightGradeLabel(vm.Grade)}", _caption);
            GUI.Label(new Rect(155, 620, 470, 46), $"최장 연속 수면  {FormatDuration(vm.LongestSleepStretchMinutes)}", _headline);
            GUI.Label(new Rect(155, 680, 470, 40), $"총 수면 {FormatDuration(vm.TotalSleepMinutes)} · 깨어남 {vm.WakeCount}회", _body);
            GUI.Label(new Rect(155, 730, 470, 40), $"남은 체력 {vm.ParentStaminaAtDawn:0}", _body);

            Panel(new Rect(720, 200, 1090, 680));
            GUI.Label(new Rect(770, 245, 980, 36), "아기가 먼저 건넨 말", _caption);
            GUI.Label(new Rect(770, 288, 940, 68), vm.BabyResponseReflection, _headline);
            GUI.Label(new Rect(770, 370, 980, 32), "내가 건넨 답", _caption);
            GUI.Label(new Rect(770, 408, 940, 58), vm.ActionLearning, _body);
            GUI.Label(new Rect(770, 482, 980, 32), "함께 배운 것", _caption);
            GUI.Label(new Rect(770, 520, 940, 62), vm.FamilyUnderstanding, _body);
            DrawLandscapeHabitNotes(vm, 770, 598);
            GUI.Label(new Rect(770, 716, 940, 52), vm.CompanionMessage, _caption);
            GUI.Label(new Rect(770, 782, 940, 48), vm.ShareCardText, _caption);
            string nextLabel = vm.HasNextNight ? NextNightButtonLabel(vm.NightId) : "엔딩 보기 →";
            if (GUI.Button(new Rect(1290, 920, 520, 76), nextLabel, _button))
            {
                if (vm.HasNextNight) _flow.AdvanceFromV2Diary();
                else _flow.AdvanceToEnding();
                _lastMove = null;
                _lastResult = null;
                _actionGroup = ActionGroup.Diagnose;
                _timedEncounterSequence = -1;
            }
        }

        private void DrawPortraitTitle()
        {
            Fill(new Rect(0, 0, PortraitWidth, PortraitHeight), new Color(0.01f, 0.02f, 0.035f, 0.3f));
            GUI.Label(new Rect(90, 270, 900, 130), "NOT A NAP", new GUIStyle(_title) { fontSize = 92 });
            GUI.Label(new Rect(190, 414, 700, 80), "백일의 밤", Centered(_headline));
            Fill(new Rect(410, 520, 260, 4), new Color(0.93f, 0.66f, 0.29f, 0.84f));
            GUI.Label(new Rect(140, 595, 800, 160),
                "오늘 밤은 아빠 차례다.\n울음보다 먼저 오는 신호를 읽어보자.", Centered(_body));
            if (DrawPrimaryButton(new Rect(140, 1030, 800, 120), "우리 아기 만들기  →"))
            {
                _introBeat = 0;
                _flow.BeginFamilySetup();
            }
            GUI.Label(new Rect(140, 1190, 800, 60),
                "약 5분 · 정답보다 서로의 리듬을 알아가는 밤", Centered(_caption));
        }

        private void DrawCarePairSetup(SetupViewModel vm, bool portrait)
        {
            if (portrait)
            {
                GUI.Label(new Rect(48, 145, 984, 42), "나는 보통 · 정답 없는 보호자 성향", _caption);
                DrawCareStyleButton(new Rect(48, 190, 305, 56), "바로 반응", CaregiverStyle.Responsive, vm.CaregiverStyle);
                DrawCareStyleButton(new Rect(388, 190, 305, 56), "잠시 관찰", CaregiverStyle.Observant, vm.CaregiverStyle);
                DrawCareStyleButton(new Rect(727, 190, 305, 56), "차례로 확인", CaregiverStyle.Methodical, vm.CaregiverStyle);
                GUI.Label(new Rect(48, 252, 984, 42), "아기 반응 경향 · 의학적 진단이 아닙니다", _caption);
                DrawTemperamentButton(new Rect(48, 298, 305, 50), "반응이 잔잔함", Temperament.Soft, vm);
                DrawTemperamentButton(new Rect(388, 298, 305, 50), "자극에 민감", Temperament.Sensitive, vm);
                DrawTemperamentButton(new Rect(727, 298, 305, 50), "배고픔 신호 빠름", Temperament.Hungry, vm);
                return;
            }

            GUI.Label(new Rect(90, 132, 430, 32), "보호자 성향 · 정답은 없어요", _caption);
            DrawCareStyleButton(new Rect(90, 170, 230, 54), "바로 반응", CaregiverStyle.Responsive, vm.CaregiverStyle);
            DrawCareStyleButton(new Rect(335, 170, 230, 54), "잠시 관찰", CaregiverStyle.Observant, vm.CaregiverStyle);
            DrawCareStyleButton(new Rect(580, 170, 230, 54), "차례로 확인", CaregiverStyle.Methodical, vm.CaregiverStyle);
            GUI.Label(new Rect(860, 132, 500, 32), "아기의 반응 경향 · 의학적 진단이 아닙니다", _caption);
            DrawTemperamentButton(new Rect(860, 170, 250, 54), "반응이 잔잔함", Temperament.Soft, vm);
            DrawTemperamentButton(new Rect(1125, 170, 250, 54), "자극에 민감", Temperament.Sensitive, vm);
            DrawTemperamentButton(new Rect(1390, 170, 300, 54), "배고픔 신호 빠름", Temperament.Hungry, vm);
            GUI.Label(new Rect(90, 238, 1600, 90), $"{vm.CaregiverStyleDescription}\n{vm.PairGuidance}", _body);
        }

        private void DrawCareStyleButton(Rect rect, string label, CaregiverStyle value, CaregiverStyle selected)
        {
            if (DrawChoiceButton(rect, label, value == selected))
                _flow.SelectCaregiverStyle(value);
        }

        private void DrawTemperamentButton(Rect rect, string label, Temperament value, SetupViewModel vm)
        {
            if (DrawChoiceButton(rect, label, ReferenceEquals(value, _flow.Session.Run.Temperament)))
                _flow.SelectBabyTemperament(value);
        }

        private void DrawPortraitSetup(SetupViewModel vm)
        {
            Fill(new Rect(0, 0, PortraitWidth, PortraitHeight), new Color(0.01f, 0.02f, 0.035f, 0.36f));
            GUI.Label(new Rect(48, 55, 750, 74), $"{vm.NightLabel} · 밤 준비", _display);
            if (vm.IsFirstNight) DrawCarePairSetup(vm, true);
            else
            {
                GUI.Label(new Rect(48, 135, 984, 45), vm.NightRoleTitle, _headline);
                GUI.Label(new Rect(48, 182, 984, 72), vm.NightRoleSummary, _body);
                if (vm.RhythmCards.Count > 0)
                {
                    var rhythm = vm.RhythmCards[0];
                    GUI.Label(new Rect(48, 254, 984, 88),
                        $"{rhythm.PreviousChoice}\n도움 · {rhythm.Help}\n부담 · {rhythm.Burden}", _caption);
                }
            }
            GUI.Label(new Rect(48, 354, 984, 72), $"가져갈 물건  {vm.SelectedCount} / {vm.Slots}",
                OverlayLabelStyle(42, FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f)));
            for (int i = 0; i < vm.Cards.Count; i++)
            {
                var card = vm.Cards[i];
                int col = i % 2;
                int row = i / 2;
                var rect = new Rect(48 + col * 510, 425 + row * 390, 474, 390);
                DrawCollectibleItem(rect, card, true);
            }

            var focused = FocusedSetupCard(vm);
            if (focused != null)
                DrawSetupItemDetail(new Rect(48, 1250, 984, 240), focused, true);
            GUI.Label(new Rect(48, 1505, 984, 66), "소품을 눌러 오늘 밤의 진열대에 올리세요.",
                OverlayLabelStyle(26, FontStyle.Bold, new Color(0.74f, 0.79f, 0.84f)));
            if (DrawPrimaryButton(new Rect(100, 1695, 880, 120),
                vm.CanStart ? "밤 시작하기  →" : $"물건을 {vm.Slots}개 골라주세요", vm.CanStart))
            {
                _audio?.PlayUi();
                _flow.ConfirmV2Setup();
                _lastMove = null;
            }
        }

        private void DrawPortraitDiary(V2DiaryViewModel vm)
        {
            Fill(new Rect(0, 0, PortraitWidth, PortraitHeight), new Color(0.015f, 0.035f, 0.065f, 0.9f));
            GUI.Label(new Rect(60, 70, 960, 80), $"{_flow.BabyName} · {vm.NightLabel} 밤의 기록", _display);
            Panel(new Rect(60, 210, 960, 620));
            GUI.Label(new Rect(110, 260, 860, 48), "밤이 남긴 것", _caption);
            GUI.Label(new Rect(110, 320, 860, 128), vm.CaregiverGrowth, _headline);
            GUI.Label(new Rect(110, 470, 860, 82), vm.Encouragement, _body);
            Fill(new Rect(110, 566, 760, 2), new Color(.84f, .62f, .31f, .5f));
            GUI.Label(new Rect(110, 592, 860, 42), $"밤의 기록 · {PresentationCopyMapper.NightGradeLabel(vm.Grade)}", _caption);
            GUI.Label(new Rect(110, 646, 860, 56), $"최장 연속 수면  {FormatDuration(vm.LongestSleepStretchMinutes)}", _headline);
            GUI.Label(new Rect(110, 718, 860, 72), $"총 수면 {FormatDuration(vm.TotalSleepMinutes)} · 깨어남 {vm.WakeCount}회 · 남은 체력 {vm.ParentStaminaAtDawn:0}", _body);
            Panel(new Rect(60, 880, 960, 820));
            GUI.Label(new Rect(110, 930, 860, 42), "아기가 먼저 건넨 말", _caption);
            GUI.Label(new Rect(110, 982, 860, 102), vm.BabyResponseReflection, _headline);
            GUI.Label(new Rect(110, 1098, 860, 42), "내가 건넨 답", _caption);
            GUI.Label(new Rect(110, 1148, 860, 82), vm.ActionLearning, _body);
            GUI.Label(new Rect(110, 1240, 860, 42), "함께 배운 것", _caption);
            GUI.Label(new Rect(110, 1290, 860, 88), vm.FamilyUnderstanding, _body);
            DrawPortraitHabitNotes(vm, 110, 1385);
            GUI.Label(new Rect(110, 1540, 860, 70), vm.CompanionMessage, _caption);
            GUI.Label(new Rect(110, 1620, 860, 58), vm.ShareCardText, _caption);
            string nextLabel = vm.HasNextNight ? NextNightButtonLabel(vm.NightId) : "엔딩 보기 →";
            if (GUI.Button(new Rect(100, 1730, 880, 110), nextLabel, _button))
            {
                if (vm.HasNextNight) _flow.AdvanceFromV2Diary();
                else _flow.AdvanceToEnding();
                _lastMove = null;
                _lastResult = null;
                _actionGroup = ActionGroup.Diagnose;
                _timedEncounterSequence = -1;
                _actionEncounterSequence = -1;
            }
        }

        private static string NextNightButtonLabel(NightId night)
            => night == NightId.FirstNight ? "둘째 밤 준비하기 →" : "백일째 밤 준비하기 →";

        private void DrawLandscapeHabitNotes(V2DiaryViewModel vm, float x, float y)
        {
            GUI.Label(new Rect(x, y, 940, 28), "오늘 형성된 습관", _caption);
            for (int i = 0; i < vm.HabitNotes.Count && i < 2; i++)
                GUI.Label(new Rect(x, y + 35 + i * 48, 940, 45),
                    $"• {vm.HabitNotes[i]}  {vm.HabitEffects[i]}", _body);
        }

        private void DrawPortraitHabitNotes(V2DiaryViewModel vm, float x, float y)
        {
            GUI.Label(new Rect(x, y, 860, 35), "오늘 형성된 습관", _caption);
            for (int i = 0; i < vm.HabitNotes.Count && i < 2; i++)
                GUI.Label(new Rect(x, y + 38 + i * 54, 860, 52),
                    $"• {vm.HabitNotes[i]} · {vm.HabitEffects[i]}", _body);
        }

        private void DrawEnding()
        {
            var vm = _flow.BuildEnding();
            float width = _portrait ? PortraitWidth : LandscapeWidth;
            float height = _portrait ? PortraitHeight : LandscapeHeight;
            Fill(new Rect(0, 0, width, height), new Color(0.01f, 0.025f, 0.05f, 0.88f));
            float panelWidth = _portrait ? 920 : 1180;
            float panelHeight = _portrait ? 1180 : 720;
            float x = (width - panelWidth) * 0.5f;
            float y = (height - panelHeight) * 0.5f;
            Color accent = vm.IsSuccess
                ? new Color(0.53f, 0.75f, 0.63f)
                : new Color(0.86f, 0.58f, 0.36f);
            Panel(new Rect(x, y, panelWidth, panelHeight));
            Fill(new Rect(x, y, panelWidth, 8), accent);
            var statusStyle = Centered(_caption);
            statusStyle.normal.textColor = accent;
            var symbolStyle = Centered(new GUIStyle(_title) { fontSize = _portrait ? 110 : 84 });
            symbolStyle.normal.textColor = accent;
            GUI.Label(new Rect(x + 60, y + 55, panelWidth - 120, 80),
                $"{_flow.BabyName}의 백일 · {PresentationCopyMapper.EndingStatusLabel(vm.IsSuccess)}", statusStyle);
            GUI.Label(new Rect(x + 110, y + 130, panelWidth - 220, 55),
                $"지켜 낸 조건  {vm.MetConditionCount} / {vm.RequiredConditionCount}",
                Centered(_headline));
            GUI.Label(new Rect(x + 60, y + 190, panelWidth - 120, 95), vm.Symbol, symbolStyle);
            GUI.Label(new Rect(x + 60, y + 290, panelWidth - 120, 90), vm.Title, Centered(_display));
            GUI.Label(new Rect(x + 110, y + 390, panelWidth - 220, 100), vm.Subtitle, Centered(_body));
            GUI.Label(new Rect(x + 110, y + 490, panelWidth - 220, 72),
                "지켜 낸 조건 · " +
                (vm.MetConditions.Count > 0 ? string.Join(" · ", vm.MetConditions) : "없음"),
                Centered(_caption));
            float unmetY = _portrait ? y + 558 : y + 530;
            float retryY = _portrait ? y + 645 : y + 580;
            float buttonY = _portrait ? y + panelHeight - 135 : y + panelHeight - 88;
            GUI.Label(new Rect(x + 110, unmetY, panelWidth - 220, _portrait ? 88 : 52),
                "다음에 이어갈 조건 · " +
                (vm.UnmetConditions.Count > 0 ? string.Join(" · ", vm.UnmetConditions) : "세 가지 신호를 모두 지켰어요"),
                Centered(_caption));
            GUI.Label(new Rect(x + 110, retryY, panelWidth - 220, 48), vm.RetrySuggestion, Centered(_caption));
            if (GUI.Button(new Rect(x + panelWidth * 0.2f, buttonY, panelWidth * 0.6f, _portrait ? 82 : 64),
                "처음부터 다시 보기", _button))
            {
                _flow = new GameFlowController(new SystemRandomSource(Environment.TickCount));
                _lastResult = null;
                _actionGroup = ActionGroup.Diagnose;
                _timedEncounterSequence = -1;
                _actionEncounterSequence = -1;
            }
        }

        private static string HomeLocationLabel(HomeLocation location) => location switch
        {
            HomeLocation.Kitchen => "주방",
            HomeLocation.Bathroom => "욕실",
            _ => "아기방"
        };

        private static string OutcomeDetail(V2PlayViewModel vm, V2ActionOutcome outcome, string fallback)
        {
            if (outcome.EventIds.Contains(GameEventId.LaydownFailed) ||
                outcome.EventIds.Contains(GameEventId.BabyFullyWoke))
                return "실패가 아니에요. 눈꺼풀·호흡·팔다리 힘이 더 편안해진 뒤 다시 시도해보세요.";
            if (outcome.Action == V2ActionId.Pacifier && !outcome.Accepted &&
                outcome.BlockReason == V2ActionBlockReason.None)
                return "쪽쪽이를 밀어냈어요. 억지로 반복하지 말고 입·손·몸의 방향을 다시 살펴보세요.";
            if (outcome.BlockReason == V2ActionBlockReason.BabyNotHeld)
                return "아기는 이미 침대에 있어요. 먼저 품에 안아주세요.";
            if (outcome.BlockReason == V2ActionBlockReason.BabyNotAsleep)
                return "아직 잠들지 않았어요. 먼저 충분히 달래주세요.";
            if (outcome.BlockReason == V2ActionBlockReason.ItemUnavailable)
                return "이 물건을 가져오지 않아 사용할 수 없어요.";
            if (outcome.BlockReason == V2ActionBlockReason.CarrierAlreadyWorn)
                return "아기띠를 먼저 벗으면 맨손으로 안을 수 있어요.";
            if (outcome.BlockReason == V2ActionBlockReason.WrongLocation)
                return "집 지도를 보고 이 행동에 필요한 방으로 이동해주세요.";
            if (outcome.BlockReason == V2ActionBlockReason.ToolRequired)
                return "욕실에서 탕온계를 먼저 챙겨주세요.";
            if (outcome.BlockReason == V2ActionBlockReason.CaregiverExhausted)
                return "보호자 체력이 바닥났어요. 먼저 숨을 고르고 다시 돌봐주세요.";
            if (outcome.BlockReason == V2ActionBlockReason.ActionLimitReached)
                return "숨을 고르는 시간도 충분히 가졌어요. 이제 확인한 신호에 맞는 돌봄으로 돌아가세요.";
            if (outcome.ActivityLocation == "주방")
                return $"주방에서 준비하는 동안 {outcome.TimeDeltaMinutes}분이 흘렀다.";
            if (outcome.HeadSupported)
                return "목과 머리를 받치자 몸이 품 안으로 기대온다.";
            if (outcome.Action == V2ActionId.CatchBreath)
                return outcome.ObservedSignals.Count > 0
                    ? $"숨을 고르자 보이지 않던 신호가 들어온다. {PresentationCopyMapper.ObservationSignal(outcome.ObservedSignals[0])}"
                    : "숨을 길게 내쉬고 아기의 다음 움직임을 기다린다.";
            if (outcome.DiaperCheckResult == DiaperCheckResult.Wet)
                return "기저귀가 젖어 있어요. 기저귀를 갈아주세요.";
            if (outcome.DiaperCheckResult == DiaperCheckResult.Clean)
                return "기저귀는 깨끗해요. 다른 불편 신호를 살펴보세요.";
            if (outcome.Action == V2ActionId.CheckHungerSignals)
            {
                switch (outcome.HungerSignalStage)
                {
                    case HungerSignalStage.Late: return "입을 찾고 빠르게 숨 쉬며 배고픈 울음을 내요. 수유가 필요해요.";
                    case HungerSignalStage.Active: return "입가를 건드린 쪽으로 고개를 돌리고 입을 벌려요. 배고픔 신호예요.";
                    case HungerSignalStage.Early: return "입맛을 다시고 손을 빨아요. 초기 배고픔 신호예요.";
                    default: return "지금은 배고픔 신호가 보이지 않아요.";
                }
            }
            if (outcome.ObservedSignals.Count > 0)
                return PresentationCopyMapper.ObservationSignal(outcome.ObservedSignals[0]);
            if (outcome.Action == V2ActionId.CheckEnvironment)
                return $"온도 {vm.TemperatureCelsius:0.#}°C (권장 20~22) · 습도 {vm.HumidityPercent:0.#}% (권장 40~60)";
            if (outcome.Action == V2ActionId.CheckBodyTemperature)
                return $"아기 체온 {vm.BabyTemperatureCelsius:0.0}°C";
            if (outcome.Action == V2ActionId.AdjustTemperature)
                return $"온도를 {vm.TemperatureCelsius:0.#}°C로 조절했어요.";
            if (outcome.Action == V2ActionId.AdjustHumidity)
                return $"습도를 {vm.HumidityPercent:0.#}%로 조절했어요.";
            if (outcome.Action == V2ActionId.SterilizeBottle)
                return "젖병 소독을 마쳤어요. 이제 평소 수유 준비를 이어가세요.";
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
                return vm.BabyHeld
                    ? "팔다리 힘이 빠졌어요.\n이제 눕히기를 시도할 수 있어요."
                    : "침대에서 깊이 잠들었어요.\n그대로 지켜봐주세요.";
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
            if (vm.SleepStage == V2SleepStage.NremDeepSleep)
                return vm.BabyHeld ? "깊은 수면 확인 · 이제 눕혀도 좋아요" : "침대에서 깊이 잠듦 · 그대로 지켜봐요";
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
                    return "울지 않고 아빠를 빤히 바라본다";
            }
        }

        private static string FormatDuration(int minutes) => minutes >= 60 ? $"{minutes / 60}시간 {minutes % 60:00}분" : $"{minutes}분";

        private void Panel(Rect rect, float alpha = 0.94f) => Fill(rect, new Color(0.035f, 0.065f, 0.105f, alpha));

        private static void DrawGlassPanel(Rect rect, float alpha = 0.7f, bool selected = false)
        {
            Fill(rect, selected
                ? new Color(0.35f, 0.19f, 0.07f, Mathf.Max(alpha, 0.78f))
                : new Color(0.015f, 0.025f, 0.04f, alpha));
            Color edge = selected
                ? new Color(0.96f, 0.67f, 0.28f, 0.94f)
                : new Color(0.78f, 0.69f, 0.55f, 0.28f);
            Fill(new Rect(rect.x, rect.y, rect.width, 2f), edge);
            Fill(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), edge);
            Fill(new Rect(rect.x, rect.y, 2f, rect.height), edge);
            Fill(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), edge);
        }

        private bool DrawPrimaryButton(Rect rect, string label, bool enabled = true)
        {
            var old = GUI.enabled;
            GUI.enabled = old && enabled;
            Fill(rect, enabled
                ? new Color(0.72f, 0.4f, 0.12f, 0.96f)
                : new Color(0.05f, 0.07f, 0.1f, 0.72f));
            Fill(new Rect(rect.x, rect.y, rect.width, 3f),
                enabled ? new Color(1f, 0.78f, 0.42f) : new Color(0.35f, 0.38f, 0.42f));
            Fill(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f),
                enabled ? new Color(0.42f, 0.2f, 0.06f) : new Color(0.2f, 0.22f, 0.25f));
            GUI.Label(rect, label, LabelStyle(_portrait ? 34 : 27, FontStyle.Bold,
                enabled ? Color.white : new Color(0.55f, 0.57f, 0.6f), TextAnchor.MiddleCenter));
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.enabled = old;
            return clicked;
        }

        private bool DrawChoiceButton(Rect rect, string label, bool selected, bool enabled = true)
        {
            DrawGlassPanel(rect, selected ? 0.86f : 0.58f, selected);
            GUI.Label(rect, label, LabelStyle(_portrait ? 27 : 19, FontStyle.Bold,
                selected ? Color.white : new Color(0.82f, 0.84f, 0.85f), TextAnchor.MiddleCenter));
            var old = GUI.enabled;
            GUI.enabled = old && enabled;
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.enabled = old;
            return clicked;
        }

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
