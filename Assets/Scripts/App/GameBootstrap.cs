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
        // Portrait play is a vertical stack of owned layout slots. Keep these in
        // one place so scene content, controls and HUD can never silently share
        // the same y-range again.
        private const float PortraitSignalY = 176f;
        private const float PortraitSignalHeight = 154f;
        private const float PortraitSceneContentY = 330f;
        private const float PortraitSceneContentBottom = 930f;
        private const float PortraitPrimaryActionY = 940f;
        private const float PortraitPrimaryActionHeight = 112f;
        private const float PortraitItemDockY = 1070f;
        private const float PortraitItemDockHeight = 104f;
        private const float PortraitRoomMapY = 1192f;
        private const float PortraitRoomMapHeight = 150f;
        private const float PortraitStatusY = 1360f;
        private const float PortraitContextY = 1592f;

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
        private bool _observationSheetOpen;
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
        private Texture2D _geneticDoubleCurly;
        private Material _mouthWarpMaterial;
        private readonly Dictionary<string, MouthWarpProfile> _mouthWarpProfiles =
            new Dictionary<string, MouthWarpProfile>();
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
        private bool _titleDropAttempted;
        private int _introBeat;
        private bool _dadBigMouth;
        private bool _momBigMouth = true;
        private bool _dadHighVoice;
        private bool _momHighVoice = true;
        private bool _babyBigMouth;
        private bool _babyHighVoice;
        private int _babyVoiceVariant;
        private int _familyRollCount;
        private bool _familyRolled;
        private string _babyNameInput = "아용이";

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
        private ContinuousCareMode _continuousCare;
        private int _continuousEncounterSequence = -1;
        private float _nextContinuousActionAt = -10f;
        private string _continuousStopNotice;
        private float _continuousStopNoticeUntil = -10f;
        private float _roomTransitionStarted = -10f;
        private HomeLocation _roomTransitionFrom;
        private HomeLocation _roomTransitionTo;
        private bool _roomTransitionBabyAccompanied;
        private const float RoomTransitionDuration = 0.5f;

        private static readonly string[] AwakeBabble = { "아우…", "으응?", "응아", "에…", "아으" };
        private static readonly string[] FussBabble = { "으응…", "에에…", "아으…" };
        private static readonly string[] CryBabble = { "으아앙!", "에앵!", "아앙…" };

        private enum ActionGroup { Diagnose, Care, Feed }
        private enum ContinuousCareMode { None, Pat, Diaper }

        private readonly struct BodyActionLink
        {
            public readonly Rect Hotspot;
            public readonly V2ActionId Action;
            public readonly string Label;

            public BodyActionLink(Rect hotspot, V2ActionId action, string label)
            {
                Hotspot = hotspot;
                Action = action;
                Label = label;
            }
        }

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
            ValidatePortraitLayout();
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
            _geneticDoubleCurly = Resources.Load<Texture2D>("Art/Baby/awake_calm");
            BuildMouthWarpProfiles();
            Shader mouthWarpShader = Resources.Load<Shader>("Shaders/BabyMouthWarp");
            if (mouthWarpShader != null) _mouthWarpMaterial = new Material(mouthWarpShader);
            _carrierBabyFrames = LoadFrameSet("carrier", 4);
            _interactionFrames[V2ActionId.Pat] = LoadFrameSet("pat", 4);
            _interactionFrames[V2ActionId.Hold] = LoadFrameSet("hold", 4);
            _interactionFrames[V2ActionId.ToggleCarrier] = _carrierBabyFrames;
            _interactionFrames[V2ActionId.FeedPreparedBottle] = LoadFrameSet("feed", 4);
            _interactionFrames[V2ActionId.Pacifier] = LoadFrameSet("pacifier", 4);
            _interactionFrames[V2ActionId.CheckDiaper] = LoadSingleFrame("diaper_check");
            _interactionFrames[V2ActionId.ChangeDiaper] = LoadSingleFrame("diaper_change");
            _interactionFrames[V2ActionId.DisposeDiaper] = LoadSingleFrame("diaper_change");
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

        private void OnDestroy()
        {
            if (_mouthWarpMaterial != null) Destroy(_mouthWarpMaterial);
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
            if (!_titleDropAttempted)
            {
                GUI.Label(new Rect(110, 90, 420, 78), "02:47", _display);
                if (_lyingSleepArt != null)
                {
                    var sleepingRect = new Rect(610, 180, 700, 700);
                    DrawAnchoringShadow(new Rect(610, 180, 700, 500), .64f, .09f);
                    DrawBabyTexture(sleepingRect, _lyingSleepArt);
                }
                DrawGlassPanel(new Rect(125, 600, 620, 230), .86f);
                GUI.Label(new Rect(165, 625, 540, 66), "드디어 잠들었다.", _headline);
                GUI.Label(new Rect(165, 700, 540, 62), "이제 침대에 내려놓기만 하면 된다.", _body);
                if (DrawPrimaryButton(new Rect(1220, 760, 560, 104), "조심히 내려놓기"))
                {
                    _titleDropAttempted = true;
                    _audio?.PlayAction(new V2ActionOutcome { Action = V2ActionId.Laydown, Accepted = true });
                    TriggerImpact(new Color(.95f, .32f, .24f, .62f), 10f, .38f);
                }
                return;
            }

            if (_introBabyArt != null)
                DrawBabyTexture(new Rect(1050, 120, 680, 680), _introBabyArt);
            GUI.Label(new Rect(120, 120, 820, 105), "NOT A NAP", new GUIStyle(_title) { fontSize = 88 });
            GUI.Label(new Rect(120, 235, 820, 70), "눕히면 깬다", _display);
            GUI.Label(new Rect(120, 340, 820, 70), "등이 닿았다. 눈이 번쩍.",
                OverlayLabelStyle(40, FontStyle.Bold, new Color(1f, .68f, .42f)));
            DrawGlassPanel(new Rect(120, 455, 790, 210), .8f);
            GUI.Label(new Rect(160, 480, 710, 56), "오늘 밤 목표", _headline);
            GUI.Label(new Rect(160, 545, 710, 88),
                "아침 6시까지 함께 버텨라.\n깊은 잠 · 체력 · 맨손 눕히기 중 둘을 지켜라.", _body);
            if (DrawPrimaryButton(new Rect(120, 735, 790, 110), "오늘 밤 버티기  →"))
            {
                _introBeat = 0;
                _flow.BeginFamilySetup();
            }
        }

        private void DrawIntro()
        {
            float width = _portrait ? PortraitWidth : LandscapeWidth;
            float height = _portrait ? PortraitHeight : LandscapeHeight;
            Fill(new Rect(0, 0, width, height), new Color(0.01f, 0.015f, 0.025f, 0.48f));

            float babySize = _portrait ? 650f : 620f;
            var babyRect = _portrait
                ? new Rect(215f, 210f, babySize, babySize)
                : new Rect(1050f, 110f, babySize, babySize);
            Texture2D introPortrait = _introBeat > 1
                ? _introBabyArt
                : (_familyRolled ? GeneticBabyPortrait() : _introBabyArt);
            if (introPortrait != null)
            {
                GUI.DrawTexture(new Rect(babyRect.center.x - babySize * 0.28f,
                    babyRect.yMax - babySize * 0.12f, babySize * 0.56f, babySize * 0.09f),
                    _itemShadow, ScaleMode.StretchToFill, true);
                DrawGeneticPortrait(babyRect, introPortrait);
            }

            var copyPanel = _portrait
                ? new Rect(58f, 820f, 964f, 390f)
                : new Rect(110f, 150f, 780f, 470f);
            DrawGlassPanel(copyPanel, 0.82f);
            GUI.Label(new Rect(copyPanel.x + 38f, copyPanel.y + 32f,
                copyPanel.width - 76f, 54f), "21:00 · 첫 번째 판단",
                OverlayLabelStyle(_portrait ? 40 : 24, FontStyle.Bold,
                    new Color(0.96f, 0.69f, 0.31f)));
            GUI.Label(new Rect(copyPanel.x + 38f, copyPanel.y + 92f,
                copyPanel.width - 76f, _portrait ? 120f : 110f),
                _introBeat == 0 ? "아기는 왜 보채는 걸까?" :
                _introBeat == 1 ? "맞았다. 배가 고팠다." : "울음이 한 단계 커졌다.",
                OverlayLabelStyle(_portrait ? 50 : 40, FontStyle.Bold,
                    Color.white, TextAnchor.UpperLeft));
            string body = _introBeat == 0
                ? "아직 울지는 않는다. 입을 오물거리고 손을 입으로 가져간다."
                : _introBeat == 1
                    ? "울기 전에 신호를 알아차리자 몸의 힘이 조금 풀렸다."
                    : "첫 신호는 배고픔이었다. 다음에는 입과 손을 먼저 보자.";
            GUI.Label(new Rect(copyPanel.x + 38f, copyPanel.y + 220f,
                copyPanel.width - 76f, 130f), body,
                OverlayLabelStyle(_portrait ? 40 : 27, FontStyle.Normal,
                    new Color(0.9f, 0.92f, 0.94f), TextAnchor.UpperLeft));

            if (_introBeat == 0)
            {
                if (_portrait)
                {
                    DrawIntroCauseButton(new Rect(90f, 1260f, 900f, 122f), "배가 고픈가?", 1);
                    DrawIntroCauseButton(new Rect(90f, 1408f, 900f, 122f), "기저귀가 불편한가?", 2);
                    DrawIntroCauseButton(new Rect(90f, 1556f, 900f, 122f), "졸린가?", 3);
                }
                else
                {
                    DrawIntroCauseButton(new Rect(110f, 675f, 500f, 92f), "배가 고픈가?", 1);
                    DrawIntroCauseButton(new Rect(630f, 675f, 500f, 92f), "기저귀가 불편한가?", 2);
                    DrawIntroCauseButton(new Rect(1150f, 675f, 500f, 92f), "졸린가?", 3);
                }
                return;
            }

            var nextRect = _portrait
                ? new Rect(90f, 1420f, 900f, 128f)
                : new Rect(110f, 700f, 720f, 92f);
            if (DrawPrimaryButton(nextRect, "오늘 밤 준비하기  →"))
                _flow.CompleteIntro();
        }

        private void DrawIntroCauseButton(Rect rect, string label, int choice)
        {
            if (!DrawPrimaryButton(rect, label)) return;
            _introBeat = choice;
            _audio?.PlayUi();
            TriggerImpact(choice == 1
                    ? new Color(.45f, .88f, .62f, .5f)
                    : new Color(.95f, .36f, .28f, .5f),
                choice == 1 ? 2f : 7f, .3f);
        }

        private void DrawFamilySetup()
        {
            float width = _portrait ? PortraitWidth : LandscapeWidth;
            float height = _portrait ? PortraitHeight : LandscapeHeight;
            Fill(new Rect(0, 0, width, height), new Color(0.01f, 0.02f, 0.035f, 0.52f));
            GUI.Label(_portrait
                    ? new Rect(60f, 55f, 960f, 78f)
                    : new Rect(90f, 50f, 1000f, 64f),
                "우리 아기는 누구를 닮았을까?", _display);
            GUI.Label(_portrait
                    ? new Rect(60f, 130f, 960f, 60f)
                    : new Rect(90f, 112f, 1250f, 44f),
                "엄마와 아빠의 입 모양과 목소리를 골라 주세요",
                _caption);

            if (_portrait)
            {
                DrawParentTraitPanel(new Rect(48f, 190f, 474f, 540f), "아빠",
                    ref _dadBigMouth, ref _dadHighVoice);
                DrawParentTraitPanel(new Rect(558f, 190f, 474f, 540f), "엄마",
                    ref _momBigMouth, ref _momHighVoice);
                DrawBabyGachaResult(new Rect(240f, 735f, 600f, 500f));
                DrawBabyNameInput(new Rect(190f, 1370f, 700f, 112f));
                if (DrawPrimaryButton(new Rect(90f, 1500f, 900f, 124f),
                    _familyRolled ? "다시 섞기" : "특징 섞기"))
                    RollFamilyBaby();
                if (DrawPrimaryButton(new Rect(90f, 1640f, 900f, 124f),
                    !_familyRolled ? "먼저 특징을 섞어 주세요" :
                    !HasBabyName() ? "아기 이름을 입력해 주세요" : "이 아기로 시작하기  →",
                    _familyRolled && HasBabyName()))
                {
                    _flow.SetBabyName(_babyNameInput);
                    _introBeat = 0;
                    _flow.BeginIntro();
                }
                if (DrawPrimaryButton(new Rect(90f, 1780f, 900f, 124f), "바로 시작"))
                {
                    if (!_familyRolled) RollFamilyBaby();
                    _flow.SetBabyName(HasBabyName() ? _babyNameInput : "아용이");
                    _introBeat = 0;
                    _flow.BeginIntro();
                }
            }
            else
            {
                DrawParentTraitPanel(new Rect(90f, 200f, 480f, 420f), "아빠",
                    ref _dadBigMouth, ref _dadHighVoice);
                DrawParentTraitPanel(new Rect(600f, 200f, 480f, 420f), "엄마",
                    ref _momBigMouth, ref _momHighVoice);
                DrawBabyGachaResult(new Rect(1120f, 165f, 610f, 650f));
                DrawBabyNameInput(new Rect(1120f, 720f, 610f, 64f));
                if (DrawPrimaryButton(new Rect(160f, 720f, 420f, 82f),
                    _familyRolled ? "다시 섞기" : "특징 섞기"))
                    RollFamilyBaby();
                if (DrawPrimaryButton(new Rect(610f, 720f, 470f, 82f),
                    !_familyRolled ? "먼저 특징을 섞어 주세요" :
                    !HasBabyName() ? "아기 이름을 입력해 주세요" : "이 아기로 시작하기  →",
                    _familyRolled && HasBabyName()))
                {
                    _flow.SetBabyName(_babyNameInput);
                    _introBeat = 0;
                    _flow.BeginIntro();
                }
                if (DrawPrimaryButton(new Rect(610f, 820f, 470f, 82f), "바로 시작"))
                {
                    if (!_familyRolled) RollFamilyBaby();
                    _flow.SetBabyName(HasBabyName() ? _babyNameInput : "아용이");
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
                font = _font,
                fontSize = _portrait ? 25 : 21,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 14, 4, 4)
            };
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.focused.textColor = Color.white;
            style.active.textColor = Color.white;
            _babyNameInput = GUI.TextField(input, _babyNameInput ?? "", 8, style);
            if (!HasBabyName() && Event.current.type == EventType.Repaint)
                GUI.Label(input, "이름을 입력해 주세요", OverlayLabelStyle(_portrait ? 20 : 17,
                    FontStyle.Normal, new Color(.7f, .74f, .78f), TextAnchor.MiddleLeft));
        }

        private void DrawParentTraitPanel(Rect rect, string title,
            ref bool bigMouth, ref bool highVoice)
        {
            DrawGlassPanel(rect, 0.76f);
            GUI.Label(new Rect(rect.x + 28f, rect.y + 22f, rect.width - 56f, 48f),
                title, OverlayLabelStyle(_portrait ? 31 : 27, FontStyle.Bold,
                    new Color(1f, 0.85f, 0.62f)));
            float gap = 14f;
            float choiceWidth = (rect.width - 56f - gap) * 0.5f;
            float firstLabelY = rect.y + 72f;
            float rowStep = _portrait ? 145f : 106f;
            float buttonHeight = _portrait ? 122f : 60f;
            DrawBinaryTraitRow(firstLabelY, "입 모양", "작은 입", "큰 입",
                rect, choiceWidth, gap, buttonHeight, ref bigMouth);
            DrawBinaryTraitRow(firstLabelY + rowStep, "목소리", "낮고 차분하게", "높고 씩씩하게",
                rect, choiceWidth, gap, buttonHeight, ref highVoice);
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
                    "엄마와 아빠 중\n누구를 닮았을까?", Centered(_headline));
                return;
            }

            Texture2D portrait = GeneticBabyPortrait();
            float artSize = Mathf.Min(area.width, _portrait ? 480f : 430f);
            var artRect = new Rect(area.center.x - artSize * 0.5f, area.y, artSize, artSize);
            GUI.DrawTexture(new Rect(artRect.center.x - artSize * 0.3f,
                artRect.yMax - 26f, artSize * 0.6f, 28f),
                _itemShadow, ScaleMode.StretchToFill, true);
            if (portrait != null) DrawGeneticPortrait(artRect, portrait);
            string mouthSource = TraitSource(_babyBigMouth, _dadBigMouth, _momBigMouth);
            string voiceSource = TraitSource(_babyHighVoice, _dadHighVoice, _momHighVoice);
            var resultPanel = new Rect(area.x + 20f, area.y + artSize - 5f,
                area.width - 40f, 155f);
            DrawGlassPanel(resultPanel, 0.82f);
            GUI.Label(new Rect(resultPanel.x + 24f, resultPanel.y + 14f,
                    resultPanel.width - 48f, 42f),
                $"{(_babyBigMouth ? "큰 입" : "작은 입")} · {(_babyHighVoice ? "높은 목소리" : "낮은 목소리")}",
                OverlayLabelStyle(_portrait ? 27 : 23, FontStyle.Bold,
                    Color.white, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(resultPanel.x + 24f, resultPanel.y + 62f,
                    resultPanel.width - 48f, 70f),
                $"입은 {mouthSource}, 목소리는 {voiceSource}를 닮았어요",
                OverlayLabelStyle(_portrait ? 21 : 18, FontStyle.Normal,
                    new Color(0.86f, 0.9f, 0.94f), TextAnchor.MiddleCenter));
        }

        private void RollFamilyBaby()
        {
            _familyRollCount++;
            int seed = _familyRollCount * 1103515245 + 12345;
            _babyBigMouth = (seed & 4) == 0 ? _dadBigMouth : _momBigMouth;
            _babyHighVoice = (seed & 8) == 0 ? _dadHighVoice : _momHighVoice;
            _babyVoiceVariant = _babyHighVoice ? 0 : 2;
            _familyRolled = true;
            _audio?.SetBabyVoiceVariant(_babyVoiceVariant);
            _audio?.PlayUi();
        }

        private Texture2D GeneticBabyPortrait()
        {
            return _geneticDoubleCurly;
        }

        private void DrawGeneticPortrait(Rect rect, Texture2D portrait)
        {
            if (portrait == null) return;
            DrawBabyTexture(rect, portrait);
        }

        private readonly struct MouthWarpProfile
        {
            public readonly Vector2 CenterFromTop;
            public readonly Vector2 Radius;
            public readonly float Angle;

            public MouthWarpProfile(float x, float y, float radiusX = .064f,
                float radiusY = .044f, float angle = 0f)
            {
                CenterFromTop = new Vector2(x, y);
                Radius = new Vector2(radiusX, radiusY);
                Angle = angle;
            }
        }

        private void BuildMouthWarpProfiles()
        {
            // 유전 초상
            AddMouthProfile("double_straight", .621f, .410f);
            AddMouthProfile("monolid_curly", .621f, .441f);
            AddMouthProfile("monolid_straight", .598f, .445f);
            AddMouthProfile("awake_calm", .575f, .414f);

            // 상태 표정. 열린 울음은 세로 형태를 보존하고 가로 폭만 넓힌다.
            AddMouthProfile("fuss_soft", .503f, .438f, .058f, .035f);
            AddMouthProfile("cry_hard", .500f, .432f, .082f, .070f);
            AddMouthProfile("hunger_late", .506f, .405f, .080f, .068f);
            AddMouthProfile("drowsy", .512f, .480f, .054f, .035f);
            AddMouthProfile("rem_active", .501f, .473f, .052f, .034f);
            AddMouthProfile("nrem_deep", .493f, .472f, .050f, .032f);
            AddMouthProfile("relaxed", .514f, .431f, .054f, .035f);
            AddMouthProfile("moro_startle", .500f, .409f, .062f, .055f);
            AddMouthProfile("pacifier_reject", .550f, .413f, .050f, .032f);
            // hunger_early/pacifier_accept는 손·쪽쪽이가 입을 가리므로 변형하지 않는다.

            AddFrameProfiles("awake", .506f, .462f, .055f, .036f);
            AddFrameProfiles("fuss", .506f, .455f, .056f, .036f);
            AddFrameProfiles("sleep", .505f, .455f, .052f, .033f);
            AddFrameProfiles("pat", .527f, .415f, .052f, .033f);
            AddFrameProfiles("hold", .612f, .309f, .046f, .030f);
            AddFrameProfiles("carrier", .553f, .404f, .050f, .032f);
            AddFrameProfiles("feed", .553f, .385f, .047f, .030f);
            AddMouthProfile("pacifier_0", .648f, .400f, .047f, .030f);
            // pacifier_1~3은 쪽쪽이가 입 전체를 덮어 원본을 유지한다.

            AddMouthProfile("diaper_check", .363f, .379f, .042f, .028f, -18f);
            AddMouthProfile("diaper_change", .519f, .307f, .041f, .027f);
            AddMouthProfile("limb_check", .470f, .360f, .040f, .027f, 10f);
            AddMouthProfile("temperature_check", .506f, .309f, .039f, .026f);
            AddMouthProfile("lying_sleep", .359f, .518f, .038f, .025f, 23f);
        }

        private static void ValidatePortraitLayout()
        {
            // This is an executable layout contract, not a visual convention. Any
            // future edit that makes two owned blocks share vertical space fails
            // immediately instead of reaching the browser as another overlap bug.
            var slots = new[]
            {
                new Rect(0f, PortraitSignalY, PortraitWidth, PortraitSignalHeight),
                new Rect(0f, PortraitSceneContentY, PortraitWidth,
                    PortraitSceneContentBottom - PortraitSceneContentY),
                new Rect(0f, PortraitPrimaryActionY, PortraitWidth,
                    PortraitPrimaryActionHeight),
                new Rect(0f, PortraitItemDockY, PortraitWidth, PortraitItemDockHeight),
                new Rect(0f, PortraitRoomMapY, PortraitWidth, PortraitRoomMapHeight),
                new Rect(0f, PortraitStatusY, PortraitWidth, 208f),
                new Rect(0f, PortraitContextY, PortraitWidth,
                    PortraitHeight - PortraitContextY)
            };
            for (int i = 1; i < slots.Length; i++)
            {
                if (slots[i - 1].yMax > slots[i].y)
                    throw new InvalidOperationException(
                        $"Portrait layout slots overlap at index {i - 1}/{i}.");
            }
        }

        private void AddFrameProfiles(string prefix, float x, float y, float radiusX, float radiusY)
        {
            for (int i = 0; i < 4; i++)
                AddMouthProfile($"{prefix}_{i}", x, y, radiusX, radiusY);
        }

        private void AddMouthProfile(string textureName, float x, float y,
            float radiusX = .064f, float radiusY = .044f, float angle = 0f)
        {
            _mouthWarpProfiles[textureName] = new MouthWarpProfile(x, y, radiusX, radiusY, angle);
        }

        private void DrawBabyTexture(Rect rect, Texture2D texture)
        {
            if (texture == null) return;
            if (!_babyBigMouth || _mouthWarpMaterial == null ||
                !_mouthWarpProfiles.TryGetValue(texture.name, out var profile) ||
                Event.current.type != EventType.Repaint)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
                return;
            }

            float textureAspect = (float)texture.width / texture.height;
            float rectAspect = rect.width / rect.height;
            Rect fitted = rect;
            if (textureAspect > rectAspect)
            {
                fitted.height = rect.width / textureAspect;
                fitted.y += (rect.height - fitted.height) * .5f;
            }
            else
            {
                fitted.width = rect.height * textureAspect;
                fitted.x += (rect.width - fitted.width) * .5f;
            }

            Vector3 topLeft = GUI.matrix.MultiplyPoint3x4(new Vector3(fitted.x, fitted.y));
            Vector3 bottomRight = GUI.matrix.MultiplyPoint3x4(new Vector3(fitted.xMax, fitted.yMax));
            var screenRect = new Rect(topLeft.x, topLeft.y,
                bottomRight.x - topLeft.x, bottomRight.y - topLeft.y);
            _mouthWarpMaterial.SetVector("_MouthCenter",
                new Vector4(profile.CenterFromTop.x, 1f - profile.CenterFromTop.y, 0f, 0f));
            _mouthWarpMaterial.SetVector("_MouthRadius",
                new Vector4(profile.Radius.x, profile.Radius.y, 0f, 0f));
            _mouthWarpMaterial.SetFloat("_MouthAngle", profile.Angle * Mathf.Deg2Rad);
            _mouthWarpMaterial.SetFloat("_MouthStrength", .28f);
            _mouthWarpMaterial.SetColor("_Color", GUI.color);
            // screenRect는 이미 GUI 행렬이 적용된 좌표다. 그대로 그리면 WebGL에서
            // 반응형 스케일이 두 번 적용되어 큰 입 아기가 화면 밖으로 밀려난다.
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            Graphics.DrawTexture(screenRect, texture, _mouthWarpMaterial);
            GUI.matrix = previousMatrix;
        }

        private void DrawAnchoringShadow(Rect subjectRect, float widthRatio = .56f,
            float heightRatio = .075f)
        {
            if (_itemShadow == null) return;
            var shadow = new Rect(
                subjectRect.center.x - subjectRect.width * widthRatio * .5f,
                subjectRect.yMax - subjectRect.height * heightRatio * .72f,
                subjectRect.width * widthRatio,
                subjectRect.height * heightRatio);
            Color previous = GUI.color;
            GUI.color = new Color(.08f, .045f, .025f, .92f);
            GUI.DrawTexture(shadow, _itemShadow, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(shadow, _itemShadow, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private bool IsGeneticPortrait(Texture2D portrait)
            => ReferenceEquals(portrait, _geneticDoubleCurly);

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
            GUI.Label(new Rect(130, 952, 940, 42), $"오늘 밤 쓸 물건 {vm.Slots}개를 고르세요.",
                OverlayLabelStyle(20, FontStyle.Bold, new Color(0.74f, 0.79f, 0.84f)));

            string next = vm.CanStart ? "이 물건으로 밤 시작  →" : $"물건 {vm.Slots}개 선택";
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
                GUI.Label(new Rect(area.x, area.y + 354, area.width, 38), "✓ 선택 완료", badge);
            }
            else if (card.Disabled)
                GUI.Label(new Rect(area.x, area.y + 354, area.width, 38),
                    "선택 칸이 가득 찼어요", Centered(_caption));
            else if (hovered)
                GUI.Label(new Rect(area.x, area.y + 354, area.width, 38), "선택하기", Centered(_caption));

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
                card.Selected ? $"{card.Name} · 선택 완료" : card.Name,
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
                $"주의 · {card.Side}", warning);
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
                GUI.Label(new Rect(rect.x, rect.y - 28, rect.width, 26), "챙긴 물건", _caption);
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
                V2ActionId itemAction = item switch
                {
                    ItemId.Carrier => V2ActionId.ToggleCarrier,
                    ItemId.Pacifier => V2ActionId.Pacifier,
                    ItemId.Noise => V2ActionId.ToggleNoise,
                    ItemId.Monitor => V2ActionId.CheckMonitor,
                    _ => V2ActionId.Hesitate
                };
                if (vm != null && !_flow.InputLocked && itemAction != V2ActionId.Hesitate &&
                    DirectAction(vm, itemAction) != null &&
                    GUI.Button(itemRect, GUIContent.none, GUIStyle.none))
                    PerformV2Action(itemAction);
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
            var labelStyle = LabelStyle(portrait ? 40 : 20, FontStyle.Bold,
                active ? Color.white : new Color(0.94f, 0.91f, 0.84f), TextAnchor.MiddleLeft);
            float labelHeight = string.IsNullOrEmpty(action.CostLabel) ? rect.height : rect.height * .62f;
            GUI.Label(new Rect(rect.x + textInset, rect.y + (string.IsNullOrEmpty(action.CostLabel) ? 0f : 4f),
                    rect.width - textInset - 12f, labelHeight), action.Label, labelStyle);
            if (!string.IsNullOrEmpty(action.CostLabel))
                GUI.Label(new Rect(rect.x + textInset, rect.y + rect.height * .56f,
                        rect.width - textInset - 12f, rect.height * .36f), action.CostLabel,
                    OverlayLabelStyle(portrait ? 31 : 16, FontStyle.Bold,
                        new Color(.72f, .86f, .94f), TextAnchor.MiddleLeft));
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
            V2ActionId.DisposeDiaper => "버",
            V2ActionId.WashHands => "씻",
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
            GUI.Label(new Rect(tx, rect.y + rect.height - 42, tw, 38), "아직 사용할 수 없습니다", _caption);
        }

        private void DrawPlay()
        {
            var vm = _flow.BuildV2Play();
            UpdateContinuousCare(vm);
            vm = _flow.BuildV2Play();
            HandleRoomMovementKeys(vm);
            int encounterSequence = _flow.Session.Night.V2.Diagnosis.EncounterSequence;
            if (!vm.CauseResolved && _actionEncounterSequence != encounterSequence)
            {
                _actionEncounterSequence = encounterSequence;
                _actionGroup = ActionGroup.Diagnose;
                _observationSheetOpen = false;
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
            DrawContinuousCareControl(new Rect(790, 118, 390, 84), false);

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
            // The observation sheet is modal. IMGUI registers scene controls
            // before it is drawn, so disable those controls while the sheet is
            // open or a tap on the sheet can also move rooms/use an item below.
            bool previousEnabled = GUI.enabled;
            if (_observationSheetOpen) GUI.enabled = false;
            DrawPlayScene(vm, new Rect(0, 0, PortraitWidth, PortraitPrimaryActionY), true);
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
            DrawPortraitItemDock(vm);
            DrawPortraitStatusOrnaments(vm);
            if (vm.EchoSources.Count > 0)
            {
                DrawEchoSource(vm, new Rect(58, PortraitContextY, 964, 120), true);
                DrawSceneFeedback(vm, new Rect(58, PortraitContextY + 134f, 964, 154), true);
            }
            else
                DrawSceneFeedback(vm, new Rect(58, PortraitContextY, 964, 154), true);
            DrawContinuousCareControl(PortraitPrimaryActionRect(), true);
            GUI.enabled = previousEnabled;
            if (_observationSheetOpen) DrawPortraitObservationSheet(vm);
        }

        private static Rect PortraitPrimaryActionRect()
            => new Rect(58f, PortraitPrimaryActionY, 964f, PortraitPrimaryActionHeight);

        private void DrawPortraitItemDock(V2PlayViewModel vm)
        {
            var dock = new Rect(58f, PortraitItemDockY, 964f, PortraitItemDockHeight);
            DrawGlassPanel(dock, .7f);
            Fill(new Rect(dock.x, dock.y + 12f, 5f, dock.height - 24f),
                new Color(.78f, .68f, .96f));
            GUI.Label(new Rect(dock.x + 24f, dock.y + 8f, 180f, dock.height - 16f),
                "챙긴 물건",
                OverlayLabelStyle(25, FontStyle.Bold, new Color(.9f, .88f, .96f),
                    TextAnchor.MiddleLeft));
            if (_flow.SelectedItems.Count == 0)
            {
                GUI.Label(new Rect(dock.x + 220f, dock.y + 8f, dock.width - 246f,
                        dock.height - 16f), "이번 밤에 챙긴 물건이 없습니다",
                    OverlayLabelStyle(23, FontStyle.Normal, new Color(.72f, .75f, .78f),
                        TextAnchor.MiddleLeft));
                return;
            }
            DrawPreparedItems(new Rect(dock.x + 220f, dock.y + 13f,
                dock.width - 244f, dock.height - 26f), false, vm);
        }

        private void DrawPortraitObservationSheet(V2PlayViewModel vm)
        {
            Fill(new Rect(0, 0, PortraitWidth, PortraitHeight), new Color(0, 0, 0, .62f));
            var sheet = new Rect(28f, 650f, 1024f, 1270f);
            DrawGlassPanel(sheet, .98f);
            Fill(new Rect(sheet.x, sheet.y, sheet.width, 6f), new Color(1f, .7f, .28f));
            Fill(new Rect(440f, 675f, 200f, 10f), new Color(.56f, .6f, .64f));

            GUI.Label(new Rect(72f, 710f, 760f, 70f),
                CanChooseSleepInterval(vm) ? "아기가 잠든 사이" : "아기 살펴보기",
                OverlayLabelStyle(52, FontStyle.Bold, Color.white));
            var close = new Rect(850f, 695f, 150f, 110f);
            DrawGlassPanel(close, .82f);
            GUI.Label(close, "닫기 ×", OverlayLabelStyle(38, FontStyle.Bold,
                new Color(.9f, .91f, .92f), TextAnchor.MiddleCenter));
            if (GUI.Button(close, GUIContent.none, GUIStyle.none))
            {
                _observationSheetOpen = false;
                return;
            }

            DrawGlassPanel(new Rect(72f, 820f, 936f, 150f), .78f);
            GUI.Label(new Rect(104f, 830f, 872f, 44f),
                NextActionHeadline(vm),
                OverlayLabelStyle(34, FontStyle.Bold, new Color(1f, .87f, .66f)));
            GUI.Label(new Rect(104f, 876f, 872f, 82f), vm.CurrentSignal,
                OverlayLabelStyle(31, FontStyle.Normal, new Color(.9f, .92f, .94f),
                    TextAnchor.MiddleLeft, true));

            if (CanChooseSleepInterval(vm))
            {
                GUI.Label(new Rect(72f, 1000f, 936f, 60f),
                    "다음 깨어남까지 무엇을 할까?",
                    OverlayLabelStyle(38, FontStyle.Bold, Color.white,
                        TextAnchor.MiddleCenter));
                DrawSleepSceneChoices(true);
                GUI.Label(new Rect(90f, 1290f, 900f, 74f),
                    "한 번 선택하면 다음 신호가 올 때까지 시간이 흐릅니다.",
                    OverlayLabelStyle(28, FontStyle.Normal,
                        new Color(.78f, .82f, .86f), TextAnchor.MiddleCenter));
                return;
            }

            bool exhausted = vm.ParentStamina <= 0;
            DrawCommandTab(new Rect(72f, 1000f, 292f, 122f), "살펴보기", ActionGroup.Diagnose, true);
            DrawCommandTab(new Rect(394f, 1000f, 292f, 122f), "돌보기", ActionGroup.Care, !exhausted);
            DrawCommandTab(new Rect(716f, 1000f, 292f, 122f), "수유 준비", ActionGroup.Feed, !exhausted);

            var actions = ActionsFor(_actionGroup, exhausted);
            int visible = 0;
            for (int i = 0; i < actions.Length; i++)
            {
                var action = vm.Actions.Find(candidate => candidate.Action == actions[i]);
                if (action == null || !action.Enabled) continue;
                int col = visible % 2;
                int row = visible / 2;
                var rect = new Rect(72f + col * 476f, 1150f + row * 146f, 448f, 126f);
                if (DrawActionButton(rect, action, vm, true))
                {
                    _observationSheetOpen = false;
                    PerformV2Action(action.Action);
                    return;
                }
                visible++;
            }
        }

        private void DrawEchoSource(V2PlayViewModel vm, Rect rect, bool portrait)
        {
            if (vm.EchoSources.Count == 0) return;
            var echo = vm.EchoSources[vm.EchoSources.Count - 1];
            DrawGlassPanel(rect, .86f);
            Fill(new Rect(rect.x, rect.y + 12, 5, rect.height - 24),
                new Color(.96f, .66f, .3f));
            GUI.Label(new Rect(rect.x + 24, rect.y + 8, rect.width - 48, rect.height - 16),
                $"되돌아온 습관 · {echo.Cause}\n돌발 상황 · {echo.Change}\n공략 · {echo.ResponseHint}",
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
                    ? (portrait ? 440f : 460f)
                    : (portrait ? 600f : 600f);
                float babyCenterX = heldOutsideNursery
                    ? (portrait ? 810f : 1650f)
                    : rect.center.x;
                var babyRect = new Rect(
                    babyCenterX - babySize * 0.5f,
                    !vm.BabyHeld && IsSleeping(vm)
                        ? (portrait ? PortraitSceneContentY : 220f)
                        : !vm.BabyHeld
                            ? (portrait ? PortraitSceneContentY : 165f)
                            : (portrait ? 350f : 86f),
                    babySize, babySize);
                bool compositeAction = HasCompositeAction(vm);
                if (!compositeAction) babyRect = AnimatedBabyActionRect(babyRect);
                DrawAnchoringShadow(babyRect, heldOutsideNursery ? .46f : .58f,
                    heldOutsideNursery ? .065f : .085f);
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
            // The lying illustrations contain generous transparent padding.
            // Lower only those frames inside the owned scene slot so the
            // visible body lands on the mattress instead of floating above it.
            Rect artRect = babyRect;
            bool lyingFrame = ReferenceEquals(frame, _lyingSleepArt) ||
                (_animatedAction.HasValue &&
                 _animatedAction.Value == V2ActionId.CheckLimbRelaxation);
            if (portrait && lyingFrame) artRect.y += 190f;
            DrawBabyTexture(artRect, frame);
            if (!string.IsNullOrEmpty(caption))
            {
                float captionY = artRect.yMax - (portrait ? 48f : 42f);
                if (portrait)
                    captionY = Mathf.Min(captionY, PortraitSceneContentBottom - 58f);
                var captionRect = new Rect(artRect.center.x - (portrait ? 250f : 220f),
                    captionY, portrait ? 500f : 440f,
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
            V2ActionId.DisposeDiaper => "사용한 기저귀를 싸서 버려요",
            V2ActionId.WashHands => "비누로 손을 깨끗이 씻어요",
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
            bool lyingPortrait = portrait && !vm.BabyHeld && IsSleeping(vm);
            Rect mouth;
            Rect back;
            Rect chest;
            Rect diaper;
            Rect limbs;
            if (lyingPortrait)
            {
                // Horizontal sleeping art needs its own non-overlapping body
                // map. These five targets partition the visible body and end
                // before the primary-action block begins.
                mouth = new Rect(babyRect.x + babyRect.width * .07f,
                    babyRect.y + 360f, babyRect.width * .24f, 120f);
                chest = new Rect(babyRect.x + babyRect.width * .32f,
                    babyRect.y + 370f, babyRect.width * .18f, 120f);
                back = new Rect(babyRect.x + babyRect.width * .52f,
                    babyRect.y + 370f, babyRect.width * .18f, 120f);
                diaper = new Rect(babyRect.x + babyRect.width * .42f,
                    babyRect.y + 490f, babyRect.width * .20f, 80f);
                limbs = new Rect(babyRect.x + babyRect.width * .64f,
                    babyRect.y + 490f, babyRect.width * .29f, 95f);
            }
            else
            {
                mouth = new Rect(babyRect.x + babyRect.width * .38f,
                    babyRect.y + babyRect.height * .17f, babyRect.width * .24f, babyRect.height * .2f);
                back = new Rect(babyRect.x + babyRect.width * .58f,
                    babyRect.y + babyRect.height * .3f, babyRect.width * .25f, babyRect.height * .34f);
                chest = new Rect(babyRect.x + babyRect.width * .27f,
                    babyRect.y + babyRect.height * .32f, babyRect.width * .3f, babyRect.height * .31f);
                diaper = new Rect(babyRect.x + babyRect.width * .34f,
                    babyRect.y + babyRect.height * .57f, babyRect.width * .33f, babyRect.height * .12f);
                limbs = new Rect(babyRect.x + babyRect.width * .16f,
                    babyRect.y + babyRect.height * .72f, babyRect.width * .68f, babyRect.height * .16f);
            }
            var mattress = new Rect(babyRect.x + babyRect.width * .08f,
                babyRect.yMax - babyRect.height * .08f, babyRect.width * .84f, babyRect.height * .17f);

            V2ActionId mouthAction = vm.FeedingReady
                ? V2ActionId.FeedPreparedBottle
                : !vm.CauseResolved ? V2ActionId.CheckHungerSignals : V2ActionId.Pacifier;
            V2ActionId diaperAction = vm.DiaperChangedPendingDisposal
                ? V2ActionId.DisposeDiaper
                : vm.DiaperWetConfirmed
                    ? V2ActionId.ChangeDiaper
                    : V2ActionId.CheckDiaper;

            V2ActionId recommendedAction = V2ActionId.Pat;
            if (!vm.CauseResolved)
                recommendedAction = vm.DiaperChangedPendingDisposal || vm.DiaperWetConfirmed ||
                    vm.DiaperRecommendationVisible ? diaperAction : mouthAction;
            if (vm.FeedingReady)
            {
                recommendedAction = V2ActionId.FeedPreparedBottle;
            }
            V2ActionId chestAction = vm.CarrierOn ? V2ActionId.ToggleCarrier : V2ActionId.Hold;
            var diaperButton = DirectAction(vm, diaperAction);
            string diaperCost = diaperButton != null && !string.IsNullOrEmpty(diaperButton.CostLabel)
                ? " · " + diaperButton.CostLabel : string.Empty;

            var links = new List<BodyActionLink>(6)
            {
                new BodyActionLink(mouth, mouthAction,
                    mouthAction == V2ActionId.FeedPreparedBottle ? "입가 · 준비한 분유 수유" :
                    mouthAction == V2ActionId.Pacifier ? "입가 · 쪽쪽이 건네기" :
                    "입과 손 · 배고픔 신호 살피기"),
                new BodyActionLink(back, V2ActionId.Pat, "등 · 같은 리듬으로 토닥이기"),
                new BodyActionLink(chest, chestAction,
                    vm.CarrierOn ? "가슴 · 아기띠 풀어주기" : "가슴 · 목을 받쳐 품에 안기"),
                new BodyActionLink(diaper, diaperAction,
                    diaperAction == V2ActionId.DisposeDiaper ? "기저귀 · 싸서 버리기" + diaperCost :
                    diaperAction == V2ActionId.ChangeDiaper
                        ? $"기저귀 · {(vm.DiaperStoolConfirmed ? "대변 처리" : "소변 기저귀 갈기")}{diaperCost}"
                        : "기저귀 · 상태 확인" + diaperCost),
                new BodyActionLink(limbs, V2ActionId.CheckLimbRelaxation,
                    "팔다리 · 힘이 풀렸는지 살피기")
            };
            if (IsSleeping(vm) && !portrait)
                links.Add(new BodyActionLink(mattress, V2ActionId.Laydown,
                    "침대 · 천천히 내려놓기"));

            DrawLinkedBodyActions(vm, links, recommendedAction, pulse, portrait);
        }

        private void DrawLinkedBodyActions(V2PlayViewModel vm, List<BodyActionLink> links,
            V2ActionId recommendedAction, float pulse, bool portrait)
        {
            var active = new List<BodyActionLink>(links.Count);
            for (int i = 0; i < links.Count; i++)
                if (DirectAction(vm, links[i].Action) != null) active.Add(links[i]);
            // CatchBreath deliberately has no body hotspot. The portrait command
            // launcher must still remain reachable when exhaustion makes it the
            // only enabled action, otherwise the night dead-locks before dawn.
            if (active.Count == 0)
            {
                if (portrait) DrawPortraitActionLauncher(vm);
                return;
            }

            if (portrait)
            {
                for (int i = 0; i < active.Count; i++)
                    if (active[i].Hotspot.y < PortraitSceneContentY ||
                        active[i].Hotspot.yMax > PortraitPrimaryActionY)
                        throw new InvalidOperationException(
                            $"Portrait body hit target {active[i].Action} escaped its block: " +
                            active[i].Hotspot);
                int recommendedIndex = active.FindIndex(link => link.Action == recommendedAction);
                if (recommendedIndex >= 0)
                {
                    BodyActionLink recommendedLink = active[recommendedIndex];
                    Color old = GUI.color;
                    GUI.color = new Color(1f, .74f, .34f, .10f + pulse * .18f);
                    GUI.DrawTexture(recommendedLink.Hotspot, _itemGlow, ScaleMode.StretchToFill, true);
                    GUI.color = old;
                    DrawRectOutline(recommendedLink.Hotspot, new Color(1f, .72f, .3f), 5f);
                    if (GUI.Button(recommendedLink.Hotspot, GUIContent.none, GUIStyle.none))
                    {
                        PerformV2Action(recommendedLink.Action);
                        return;
                    }
                }

                DrawPortraitActionLauncher(vm);
                return;
            }

            bool babyOnRight = !portrait && active[0].Hotspot.center.x > 1300f;
            Rect panel = new Rect(babyOnRight ? 1040f : 1510f, 365f, 360f,
                54f + active.Count * 48f);
            DrawGlassPanel(panel, .7f);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 8f, panel.width - 36f, 32f),
                "아기를 눌러 행동 선택",
                OverlayLabelStyle(portrait ? 18 : 15, FontStyle.Bold,
                    new Color(.94f, .88f, .76f), TextAnchor.MiddleLeft));

            Vector2 mouse = Event.current.mousePosition;
            for (int i = 0; i < active.Count; i++)
            {
                BodyActionLink link = active[i];
                Rect labelRect;
                labelRect = new Rect(panel.x + 14f, panel.y + 44f + i * 48f,
                    panel.width - 28f, 40f);

                bool bodyHovered = link.Hotspot.Contains(mouse);
                bool labelHovered = labelRect.Contains(mouse);
                bool recommended = link.Action == recommendedAction &&
                    Time.unscaledTime >= _directCueHiddenUntil;
                Color accent = bodyHovered
                    ? new Color(.45f, .9f, .86f)
                    : new Color(1f, .72f, .3f);

                float glowAlpha = bodyHovered || labelHovered
                    ? .5f
                    : recommended ? .07f + pulse * .09f : .018f;
                Color previousColor = GUI.color;
                GUI.color = new Color(accent.r, accent.g, accent.b, glowAlpha);
                GUI.DrawTexture(link.Hotspot, _itemGlow, ScaleMode.StretchToFill, true);
                GUI.color = previousColor;

                if (bodyHovered || labelHovered)
                {
                    DrawRectOutline(link.Hotspot, accent, portrait ? 4f : 3f);
                    DrawCareSparkles(link.Hotspot.center, .42f, 2);
                }

                DrawGlassPanel(labelRect, bodyHovered || labelHovered ? .9f : .54f);
                Fill(new Rect(labelRect.x, labelRect.y + 6f, bodyHovered ? 6f : 4f,
                    labelRect.height - 12f), accent);
                string prefix = string.Empty;
                GUI.Label(new Rect(labelRect.x + 14f, labelRect.y,
                        labelRect.width - 24f, labelRect.height), prefix + link.Label,
                    OverlayLabelStyle(portrait ? 17 : 14, FontStyle.Bold,
                        bodyHovered || labelHovered ? accent : new Color(.91f, .91f, .89f),
                        TextAnchor.MiddleLeft));

                bool clickedBody = GUI.Button(link.Hotspot, GUIContent.none, GUIStyle.none);
                bool clickedLabel = GUI.Button(labelRect, GUIContent.none, GUIStyle.none);
                if (!clickedBody && !clickedLabel) continue;
                _directHintSeen = true;
                PerformV2Action(link.Action);
                return;
            }
        }

        private void DrawPortraitActionLauncher(V2PlayViewModel vm)
        {
            var launcher = PortraitPrimaryActionRect();
            DrawGlassPanel(launcher, .92f, true);
            Fill(new Rect(launcher.x, launcher.y, 8f, launcher.height),
                new Color(1f, .7f, .28f));
            GUI.Label(launcher, CanChooseSleepInterval(vm)
                    ? "아기가 잠든 사이  ↑"
                    : vm.ParentStamina <= 0
                        ? "숨 고르고 다시 돌보기  ↑"
                        : "아기 살펴보기  ↑",
                OverlayLabelStyle(38, FontStyle.Bold, Color.white,
                    TextAnchor.MiddleCenter));
            if (GUI.Button(launcher, GUIContent.none, GUIStyle.none))
                _observationSheetOpen = true;
        }

        private static void DrawRectOutline(Rect rect, Color color, float thickness)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
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
                    DrawBathroomGuidance(vm, portrait);
                    break;
                default:
                    DrawNurseryThermometer(vm,
                        portrait ? new Rect(28, 790, 250, 148) : new Rect(1400, 630, 180, 128),
                        portrait);
                    DrawSceneActionHotspot(vm,
                        portrait ? new Rect(28, 340, 190, 260) : new Rect(420, 170, 160, 250),
                        V2ActionId.AdjustTemperature, "창문을 조절해 방 온도 맞추기", portrait);
                    DrawSceneActionHotspot(vm,
                        portrait ? new Rect(858, 650, 170, 190) : new Rect(1300, 350, 160, 180),
                        V2ActionId.AdjustHumidity, "가습기를 조절해 습도 맞추기", portrait);
                    if (!portrait)
                    {
                        DrawRoomObject(vm, V2ActionId.ToggleNoise,
                            new Rect(75, 610, 160, 155),
                            vm.NoiseOn ? "백색소음 끄기" : "백색소음기", ItemId.Noise, false);
                        if (vm.HasPacifier)
                            DrawPacifierProp(vm, new Rect(1535, 672, 78, 78), false);
                        if (!vm.CarrierOn)
                            DrawRoomObject(vm, V2ActionId.ToggleCarrier,
                                new Rect(1280, 515, 105, 175), "아기띠", ItemId.Carrier, false);
                        DrawRoomObject(vm, V2ActionId.CheckMonitor,
                            new Rect(255, 625, 140, 140),
                            "베이비 모니터 살피기", ItemId.Monitor, false);
                    }
                    DrawGrandmaCall(vm,
                        portrait ? new Rect(825, 350, 180, 190) : new Rect(1510, 150, 180, 180),
                        portrait);
                    break;
            }
            if (IsSleeping(vm) && !portrait) DrawSleepSceneChoices(false);
            DrawRoomPickupAnimation(vm, portrait);
        }

        private void DrawGrandmaCall(V2PlayViewModel vm, Rect rect, bool portrait)
        {
            var action = DirectAction(vm, V2ActionId.Grandma);
            if (action == null) return;
            DrawGlassPanel(rect, .86f, true);
            Fill(new Rect(rect.x, rect.y, portrait ? 7f : 5f, rect.height),
                new Color(1f, .7f, .3f));
            GUI.Label(new Rect(rect.x + 12f, rect.y + (portrait ? 12f : 8f),
                    rect.width - 24f, portrait ? 78f : 70f), "☎",
                OverlayLabelStyle(portrait ? 54 : 44, FontStyle.Bold,
                    new Color(1f, .84f, .55f), TextAnchor.MiddleCenter));
            GUI.Label(new Rect(rect.x + 12f, rect.y + (portrait ? 90f : 74f),
                    rect.width - 24f, rect.height - (portrait ? 102f : 84f)),
                "할머니에게\n전화하기",
                OverlayLabelStyle(portrait ? 22 : 18, FontStyle.Bold,
                    Color.white, TextAnchor.MiddleCenter, true));
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                PerformV2Action(V2ActionId.Grandma);
                _audio.PlayUi();
            }
        }

        private void DrawBathroomGuidance(V2PlayViewModel vm, bool portrait)
        {
            bool babyTogether = vm.BabyLocation == HomeLocation.Bathroom;
            Rect panel = portrait
                ? new Rect(58f, 310f, 448f, 286f)
                : new Rect(52f, 226f, 410f, 252f);
            DrawGlassPanel(panel, .84f);
            Fill(new Rect(panel.x, panel.y + 14f, 5f, panel.height - 28f),
                new Color(.42f, .82f, .9f));
            GUI.Label(new Rect(panel.x + 22f, panel.y + 12f, panel.width - 44f, 36f),
                vm.HandsNeedWashing ? "대변 처리 후" : "욕실에서 할 일",
                OverlayLabelStyle(portrait ? 24 : 19, FontStyle.Bold,
                    new Color(.78f, .94f, 1f), TextAnchor.MiddleLeft));
            GUI.Label(new Rect(panel.x + 22f, panel.y + 48f, panel.width - 44f, 40f),
                vm.HandsNeedWashing ? "수유 전에 비누로 손을 씻으세요" :
                !babyTogether ? "아기를 안고 와야 체온을 살필 수 있어요" :
                !vm.BabyTemperatureChecked ? "탕온계로 체온을 확인해 주세요" :
                !vm.TemperatureChecked || !vm.HumidityChecked
                    ? "욕실의 온도와 습기도 확인해 주세요"
                    : "확인이 끝났어요. 아기방으로 돌아가세요",
                OverlayLabelStyle(portrait ? 18 : 15, FontStyle.Normal,
                    new Color(.9f, .91f, .9f), TextAnchor.MiddleLeft));

            float rowY = panel.y + 96f;
            if (vm.HandsNeedWashing)
            {
                DrawBathroomTask(vm, new Rect(panel.x + 20f, rowY, panel.width - 40f, 58f),
                    V2ActionId.WashHands, "비누로 손 씻기 · 2분 · 체력 -1",
                    false, true, portrait);
                return;
            }
            DrawBathroomTask(vm, new Rect(panel.x + 20f, rowY, panel.width - 40f, 48f),
                V2ActionId.CheckBodyTemperature, "1  탕온계로 아기 체온 살피기",
                vm.BabyTemperatureChecked, babyTogether, portrait);
            DrawBathroomTask(vm, new Rect(panel.x + 20f, rowY + 58f, panel.width - 40f, 48f),
                V2ActionId.CheckEnvironment, "2  욕실 온도·습도 숫자 확인하기",
                vm.TemperatureChecked && vm.HumidityChecked, true, portrait);

            var returnRect = new Rect(panel.x + 20f, rowY + 116f, panel.width - 40f, 48f);
            bool readyToReturn = vm.BabyTemperatureChecked &&
                vm.TemperatureChecked && vm.HumidityChecked;
            DrawGlassPanel(returnRect, readyToReturn ? .92f : .46f, readyToReturn);
            GUI.Label(returnRect, readyToReturn ? "3  확인 완료 · 아기방으로 돌아가기  →" :
                    "3  확인을 마치면 아기방으로 돌아가요",
                OverlayLabelStyle(portrait ? 17 : 14, FontStyle.Bold,
                    readyToReturn ? new Color(.62f, .96f, .74f) : new Color(.65f, .68f, .7f),
                    TextAnchor.MiddleCenter));
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && readyToReturn && !_flow.InputLocked;
            if (GUI.Button(returnRect, GUIContent.none, GUIStyle.none))
                MoveToRoom(HomeLocation.Nursery);
            GUI.enabled = previousEnabled;
        }

        private void DrawBathroomTask(V2PlayViewModel vm, Rect rect, V2ActionId actionId,
            string label, bool completed, bool prerequisiteMet, bool portrait)
        {
            bool available = !completed && prerequisiteMet && DirectAction(vm, actionId) != null;
            bool hovered = available && rect.Contains(Event.current.mousePosition);
            DrawGlassPanel(rect, completed ? .7f : hovered ? .92f : .58f, hovered);
            Color accent = completed ? new Color(.56f, .92f, .68f) :
                available ? new Color(1f, .76f, .38f) : new Color(.58f, .61f, .64f);
            Fill(new Rect(rect.x, rect.y + 6f, hovered ? 6f : 4f, rect.height - 12f), accent);
            string state = completed ? "  ✓ 완료" : !prerequisiteMet ? "  · 아기 필요" : "";
            GUI.Label(new Rect(rect.x + 14f, rect.y, rect.width - 24f, rect.height), label + state,
                OverlayLabelStyle(portrait ? 17 : 14, FontStyle.Bold, accent,
                    TextAnchor.MiddleLeft));
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && available && !_flow.InputLocked;
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                _roomObjectAction = actionId;
                _roomObjectAnimationStarted = Time.unscaledTime;
                PerformV2Action(actionId);
            }
            GUI.enabled = previousEnabled;
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
            Rect rest = portrait ? new Rect(72, 1075, 292, 200) : new Rect(520, 785, 180, 135);
            Rect environment = portrait ? new Rect(394, 1075, 292, 200) : new Rect(850, 785, 180, 135);
            Rect feed = portrait ? new Rect(716, 1075, 292, 200) : new Rect(1180, 785, 180, 135);
            DrawSleepProp(rest, "같이 쉬기", "체력 회복", SleepIntervalChoice.RestTogether, portrait);
            DrawSleepProp(environment, "방 살피기", "다음 각성 대비", SleepIntervalChoice.CheckEnvironment, portrait);
            DrawSleepProp(feed, "수유 준비", "젖병 미리 챙기기", SleepIntervalChoice.PrepareNextFeed, portrait);
        }

        private void DrawSleepProp(Rect rect, string label, string detail,
            SleepIntervalChoice choice, bool portrait)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * 2.4f + (int)choice) + 1f) * .5f;
            var old = GUI.color;
            GUI.color = new Color(.55f, .8f, .95f, .18f + pulse * .28f);
            GUI.DrawTexture(rect, _itemGlow, ScaleMode.StretchToFill, true);
            GUI.color = old;
            DrawGlassPanel(rect, .58f);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 20f, rect.width - 24f, portrait ? 70f : 42f), label,
                OverlayLabelStyle(portrait ? 34 : 20, FontStyle.Bold,
                    new Color(.82f, .94f, 1f), TextAnchor.MiddleCenter));
            GUI.Label(new Rect(rect.x + 12f, rect.y + (portrait ? 96f : 61f), rect.width - 24f,
                    portrait ? 76f : 45f), detail,
                OverlayLabelStyle(portrait ? 25 : 14, FontStyle.Normal,
                    new Color(.88f, .91f, .94f), TextAnchor.MiddleCenter, true));
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none) &&
                _flow.ChooseV2SleepInterval(choice))
            {
                _lastResult = null;
                _lastMove = null;
                if (portrait) _observationSheetOpen = false;
                _audio.PlayUi();
                TriggerImpact(new Color(.36f, .62f, .78f, .22f), 1.5f, .22f);
            }
        }

        private void DrawKitchenPreparation(V2PlayViewModel vm, bool portrait, bool babyVisible)
        {
            bool splitPortrait = portrait && babyVisible;
            // Kitchen props live inside the scene slot (y < primary action).
            // Previously they occupied y=1035~1350 and collided with the item
            // dock, room navigation and status HUD on portrait screens.
            Rect powder = splitPortrait ? new Rect(66, 530, 210, 260)
                : portrait ? new Rect(90, 520, 250, 305) : new Rect(480, 395, 255, 310);
            Rect bottle = splitPortrait ? new Rect(290, 520, 180, 275)
                : portrait ? new Rect(405, 500, 225, 330) : new Rect(825, 375, 220, 330);
            Rect cooling = splitPortrait ? new Rect(118, 690, 250, 195)
                : portrait ? new Rect(705, 600, 260, 225) : new Rect(1110, 470, 285, 235);

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
            float stateY = portrait ? 878f : 670f;
            GUI.Label(new Rect(portrait ? 120 : 500, stateY,
                    portrait ? 840 : 900, portrait ? 48 : 46), state,
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
            if (portrait && (rect.y < PortraitSceneContentY ||
                             rect.yMax > PortraitPrimaryActionY))
                throw new InvalidOperationException(
                    $"Portrait scene hit target {id} escaped its block: {rect}.");
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

        private void DrawNurseryThermometer(V2PlayViewModel vm, Rect rect, bool portrait)
        {
            var action = DirectAction(vm, V2ActionId.CheckEnvironment);
            bool checkedBoth = vm.TemperatureChecked && vm.HumidityChecked;
            bool hovered = rect.Contains(Event.current.mousePosition);
            DrawGlassPanel(rect, hovered ? .92f : .78f, checkedBoth);
            Fill(new Rect(rect.x, rect.y, portrait ? 6f : 5f, rect.height),
                checkedBoth ? new Color(.44f, .84f, .62f) : new Color(1f, .7f, .3f));
            GUI.Label(new Rect(rect.x + 18f, rect.y + 7f, rect.width - 36f, portrait ? 38f : 30f),
                "온습도계", OverlayLabelStyle(portrait ? 23 : 18, FontStyle.Bold,
                    new Color(.94f, .91f, .84f), TextAnchor.MiddleLeft));
            string temperature = vm.TemperatureChecked ? $"{vm.TemperatureCelsius:0.#}°" : "--°";
            string humidity = vm.HumidityChecked ? $"{vm.HumidityPercent:0.#}%" : "--%";
            GUI.Label(new Rect(rect.x + 18f, rect.y + (portrait ? 48f : 40f), rect.width - 36f,
                    portrait ? 58f : 48f), $"{temperature}   {humidity}",
                OverlayLabelStyle(portrait ? 34 : 27, FontStyle.Bold, Color.white,
                    TextAnchor.MiddleCenter));
            GUI.Label(new Rect(rect.x + 18f, rect.yMax - (portrait ? 37f : 31f), rect.width - 36f,
                    portrait ? 31f : 26f), checkedBoth ? "확인 완료" : "눌러서 확인",
                OverlayLabelStyle(portrait ? 17 : 14, FontStyle.Bold,
                    checkedBoth ? new Color(.62f, .94f, .72f) : new Color(1f, .84f, .58f),
                    TextAnchor.MiddleCenter));
            if (action != null && GUI.Button(rect, GUIContent.none, GUIStyle.none))
            {
                _roomObjectAction = V2ActionId.CheckEnvironment;
                _roomObjectAnimationStarted = Time.unscaledTime;
                PerformV2Action(V2ActionId.CheckEnvironment);
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
                : 0f;
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
            float width = portrait ? 988f : 700f;
            float x = portrait ? 46f : 52f;
            float y = portrait ? PortraitSignalY : 118f;
            float height = portrait ? PortraitSignalHeight : 84f;
            DrawGlassPanel(new Rect(x, y, width, height), 0.62f);
            Fill(new Rect(x, y + 12, 4, height - 24),
                vm.CauseResolved ? new Color(0.45f, 0.8f, 0.61f) : new Color(0.96f, 0.64f, 0.3f));
            GUI.Label(new Rect(x + 24, y + 4, width - 48, portrait ? 48 : 38),
                NextActionHeadline(vm),
                OverlayLabelStyle(portrait ? 34 : 20, FontStyle.Bold,
                    new Color(0.98f, 0.87f, 0.68f)));
            GUI.Label(new Rect(x + 24, y + (portrait ? 50 : 39), width - 48, portrait ? 96 : 42),
                vm.CurrentSignal,
                OverlayLabelStyle(portrait ? 32 : 20, FontStyle.Normal,
                    new Color(0.92f, 0.93f, 0.93f), TextAnchor.MiddleLeft, true));
        }

        private void DrawHomeJourneyMap(V2PlayViewModel vm, bool portrait)
        {
            Rect map = portrait
                ? new Rect(58f, PortraitRoomMapY, 964f, PortraitRoomMapHeight)
                : new Rect(1540f, 690f, 330f, 62f);
            DrawGlassPanel(map, 0.7f);
            Rect titleRect = portrait
                ? new Rect(map.x + 22f, map.y + 7f, map.width - 44f, 34f)
                : new Rect(map.x + 8f, map.y, 62f, map.height);
            GUI.Label(titleRect, portrait ? "방 이동" : "이동",
                OverlayLabelStyle(portrait ? 23 : 14, FontStyle.Bold,
                    new Color(.96f, .78f, .5f),
                    portrait ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter));

            HomeLocation[] rooms =
                { HomeLocation.Nursery, HomeLocation.Kitchen, HomeLocation.Bathroom };
            float startX = map.x + (portrait ? 14f : 70f);
            float gap = portrait ? 12f : 7f;
            float roomWidth = (map.xMax - startX - 12f - gap * 2f) / 3f;
            for (int i = 0; i < rooms.Length; i++)
            {
                HomeLocation room = rooms[i];
                var roomRect = portrait
                    ? new Rect(startX + i * (roomWidth + gap), map.y + 46f,
                        roomWidth, map.height - 58f)
                    : new Rect(startX + i * (roomWidth + gap), map.y + 8f,
                        roomWidth, map.height - 16f);
                bool current = vm.CaregiverLocation == room;
                bool babyHere = vm.BabyLocation == room;
                DrawGlassPanel(roomRect, current ? .92f : .5f, current);
                if (current)
                    Fill(new Rect(roomRect.x, roomRect.y, 4f, roomRect.height),
                        new Color(.96f, .68f, .3f));
                string occupants = current && babyHere ? " · 나와 아기" :
                    current ? " · 나" : babyHere ? " · 아기" : "";
                GUI.Label(roomRect, HomeLocationLabel(room) + occupants,
                        OverlayLabelStyle(portrait ? 25 : 12, FontStyle.Bold,
                        current ? Color.white : new Color(.78f, .81f, .83f),
                        TextAnchor.MiddleCenter));
                bool enabled = !current && !_flow.InputLocked && !RoomTransitionActive();
                bool previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && enabled;
                if (GUI.Button(roomRect, GUIContent.none, GUIStyle.none)) MoveToRoom(room);
                GUI.enabled = previousEnabled;
            }
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
            DrawStatusOrnament(new Rect(38, 412, 286, 82), "집중력",
                $"{vm.CaregiverComposure:0}",
                Mathf.Clamp01((float)vm.CaregiverComposure / 100f),
                new Color(0.72f, 0.56f, 0.94f), false);
            DrawPreparedItems(new Rect(44, 516, 270, 58), true, vm);
            DrawCaregiverBreathHotspot(vm, new Rect(38, 316, 286, 82), false);
        }

        private void DrawPortraitStatusOrnaments(V2PlayViewModel vm)
        {
            float y = PortraitStatusY;
            DrawStatusOrnament(new Rect(46, y, 988, 96), "연속 수면 / 오늘 최장",
                $"{FormatDuration(vm.CurrentSleepStretchMinutes)} / {FormatDuration(vm.LongestSleepStretchMinutes)}",
                Mathf.Clamp01(vm.CurrentSleepStretchMinutes / 300f),
                new Color(0.4f, 0.72f, 0.91f), true);
            DrawStatusOrnament(new Rect(46, y + 112, 476, 96), "보호자 체력",
                $"{vm.ParentStamina:0}",
                Mathf.Clamp01((float)vm.ParentStamina / 100f),
                vm.ParentStamina >= 30 ? new Color(0.49f, 0.84f, 0.61f) : new Color(0.94f, 0.39f, 0.34f), true);
            DrawStatusOrnament(new Rect(558, y + 112, 476, 96), "집중력",
                $"{vm.CaregiverComposure:0}",
                Mathf.Clamp01((float)vm.CaregiverComposure / 100f),
                new Color(0.72f, 0.56f, 0.94f), true);
            DrawCaregiverBreathHotspot(vm, new Rect(46, y + 112, 476, 96), true);
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
            if (!portrait && value.Contains("/"))
            {
                GUI.Label(new Rect(rect.x + 30, rect.y + 2, rect.width - 52, 30), label,
                    OverlayLabelStyle(15, FontStyle.Bold,
                        new Color(0.76f, 0.81f, 0.85f)));
                GUI.Label(new Rect(rect.x + 30, rect.y + 27, rect.width - 52, 32), value,
                    OverlayLabelStyle(21, FontStyle.Bold,
                        new Color(0.98f, 0.94f, 0.86f), TextAnchor.MiddleRight));
                DrawProgress(new Rect(rect.x + 30, rect.yMax - 16, rect.width - 52, 5), progress, color);
                return;
            }
            float valueWidth = portrait ? Mathf.Min(340f, rect.width * .42f) : 86f;
            GUI.Label(new Rect(rect.x + 30, rect.y + 2, rect.width - valueWidth - 54, portrait ? 62 : 48), label,
                OverlayLabelStyle(portrait ? 42 : 16, FontStyle.Bold,
                    new Color(0.76f, 0.81f, 0.85f)));
            GUI.Label(new Rect(rect.xMax - valueWidth - 20, rect.y + 1, valueWidth, portrait ? 64 : 50), value,
                OverlayLabelStyle(portrait ? 46 : 23, FontStyle.Bold,
                    new Color(0.98f, 0.94f, 0.86f), TextAnchor.MiddleRight));
            DrawProgress(new Rect(rect.x + 30, rect.yMax - (portrait ? 25 : 21), rect.width - 52,
                portrait ? 7 : 5), progress, color);
        }

        private void DrawSceneFeedback(V2PlayViewModel vm, Rect rect, bool portrait)
        {
            var outcome = _lastResult?.Outcome;
            if (outcome == null && (_lastMove == null || !_lastMove.Accepted)) return;
            DrawGlassPanel(rect, 0.64f);
            string title = "방금 한 행동";
            string detail = string.Empty;
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
            string timer = outcome != null && !vm.CauseResolved
                ? "  ·  " + DecisionTimerCopy(vm) : "";
            GUI.Label(new Rect(rect.x + 28, rect.y + (portrait ? 4 : 1), rect.width - 56,
                    portrait ? 66 : 42),
                title + timer,
                OverlayLabelStyle(portrait ? 42 : 22, FontStyle.Bold,
                    new Color(0.98f, 0.9f, 0.76f)));
            GUI.Label(new Rect(rect.x + 28, rect.y + (portrait ? 66 : 38), rect.width - 56,
                    portrait ? 76 : 38),
                detail,
                OverlayLabelStyle(portrait ? 36 : 18, FontStyle.Normal,
                    new Color(0.83f, 0.86f, 0.88f), TextAnchor.MiddleLeft, true));
        }

        private void DrawLandscapeCommandDeck(V2PlayViewModel vm)
        {
            Fill(new Rect(0, 780, LandscapeWidth, 300), new Color(0.012f, 0.025f, 0.045f, 0.46f));
            Fill(new Rect(0, 780, LandscapeWidth, 2), new Color(0.84f, 0.62f, 0.31f, 0.72f));
            GUI.Label(new Rect(48, 806, 720, 40),
                vm.ParentStamina <= 0 ? "체력이 바닥났다 · 먼저 숨 고르기" :
                vm.CaregiverLocation == HomeLocation.Nursery
                    ? "아기를 눌러 행동을 고르세요"
                    : "필요한 물건을 눌러 챙기세요",
                LabelStyle(26, FontStyle.Bold, new Color(0.98f, 0.91f, 0.78f)));
            GUI.Label(new Rect(48, 850, 760, 32),
                "신호를 확인하고, 필요한 행동을 고르세요",
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
            StopContinuousCare();
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
                if (current != null)
                {
                    if (IsGeneticPortrait(current)) DrawGeneticPortrait(animated, current);
                    else DrawBabyTexture(animated, current);
                }
                return;
            }
            if (previous != null && blend < 1f)
            {
                GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * (1f - blend));
                if (IsGeneticPortrait(previous)) DrawGeneticPortrait(animated, previous);
                else DrawBabyTexture(animated, previous);
            }
            if (current != null)
            {
                GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * blend);
                if (IsGeneticPortrait(current)) DrawGeneticPortrait(animated, current);
                else DrawBabyTexture(animated, current);
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

        private static bool CanChooseSleepInterval(V2PlayViewModel vm)
            => vm.SleepStage == V2SleepStage.NremDeepSleep &&
               vm.DeepSleepObserved && !vm.BabyHeld;

        private void DrawPortraitEvent(V2PlayViewModel vm)
        {
            Panel(new Rect(48, 920, 984, 250), 0.72f);
            GUI.Label(new Rect(82, 950, 900, 42), !vm.CauseResolved ? DecisionTimerCopy(vm, "결정까지 ") : "행동 결과", _caption);
            string title = vm.CauseResolved ? "호흡과 몸의 힘을 확인했다." : "왜 보채는 걸까?";
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
                vm.ParentStamina <= 0 ? "체력이 바닥났다 · 먼저 숨 고르기" :
                vm.CaregiverLocation == HomeLocation.Nursery
                    ? "아기를 눌러 행동을 고르세요"
                    : "필요한 물건을 눌러 챙기세요",
                LabelStyle(33, FontStyle.Bold, new Color(0.98f, 0.91f, 0.78f)));
            GUI.Label(new Rect(48, 1144, 984, 58),
                "신호를 확인하고, 필요한 행동을 고르세요",
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
            GUI.Label(new Rect(74, 680, 280, 28), "집중력", _caption);
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
                vm.ParentStamina <= 0 ? "체력이 바닥났다" : "행동 목록", _headline);

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
            GUI.Label(new Rect(478, 816, 840, 28), !vm.CauseResolved ? DecisionTimerCopy(vm, "결정까지  ") : "행동 결과", _caption);

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
                    return new[] { V2ActionId.Hold, V2ActionId.ToggleCarrier, V2ActionId.Pat, V2ActionId.Pacifier, V2ActionId.ToggleNoise, V2ActionId.Laydown, V2ActionId.ChangeDiaper, V2ActionId.DisposeDiaper, V2ActionId.WashHands, V2ActionId.AdjustTemperature, V2ActionId.AdjustHumidity };
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

        private void UpdateContinuousCare(V2PlayViewModel vm)
        {
            if (_continuousCare == ContinuousCareMode.None) return;
            if (_flow.PendingOverlay != null || _flow.Session.Night.Over)
            {
                StopContinuousCare("밤의 흐름이 바뀌어 자동으로 멈췄습니다");
                return;
            }
            if (_continuousEncounterSequence != _flow.Session.Night.V2.Diagnosis.EncounterSequence)
            {
                StopContinuousCare("새 신호가 보여 자동으로 멈췄습니다");
                return;
            }
            if (vm.ParentStamina <= 30)
            {
                StopContinuousCare("체력 30을 지키기 위해 멈췄습니다");
                return;
            }
            if (_animatedAction.HasValue || Time.unscaledTime < _nextContinuousActionAt) return;

            if (_continuousCare == ContinuousCareMode.Pat)
            {
                if (vm.SleepStage == V2SleepStage.RemActiveSleep ||
                    vm.SleepStage == V2SleepStage.NremDeepSleep)
                {
                    StopContinuousCare("아기가 잠들어 토닥이기를 멈췄습니다");
                    return;
                }
                if (!vm.CauseResolved || DirectAction(vm, V2ActionId.Pat) == null)
                {
                    StopContinuousCare("확인할 신호가 생겨 토닥이기를 멈췄습니다");
                    return;
                }
                PerformV2Action(V2ActionId.Pat, true);
                return;
            }

            V2ActionId next = vm.DiaperChangedPendingDisposal
                ? V2ActionId.DisposeDiaper
                : vm.DiaperWetConfirmed ? V2ActionId.ChangeDiaper : V2ActionId.CheckDiaper;
            if (next == V2ActionId.CheckDiaper || DirectAction(vm, next) == null)
            {
                StopContinuousCare(vm.HandsNeedWashing
                    ? "대변 처리가 끝났습니다 · 수유 전 손을 씻으세요"
                    : "기저귀 처리가 끝났습니다");
                return;
            }
            PerformV2Action(next, true);
        }

        private void DrawContinuousCareControl(Rect rect, bool portrait)
        {
            if (_continuousCare == ContinuousCareMode.None)
            {
                if (Time.unscaledTime >= _continuousStopNoticeUntil ||
                    string.IsNullOrEmpty(_continuousStopNotice)) return;
                DrawGlassPanel(rect, .86f);
                GUI.Label(rect, _continuousStopNotice,
                    OverlayLabelStyle(portrait ? 28 : 18, FontStyle.Bold,
                        new Color(.72f, .94f, .8f), TextAnchor.MiddleCenter));
                return;
            }

            DrawGlassPanel(rect, .94f, true);
            string label = _continuousCare == ContinuousCareMode.Pat
                ? "토닥이는 중  ·  멈추기"
                : "기저귀 처리 중  ·  멈추기";
            GUI.Label(rect, label,
                OverlayLabelStyle(portrait ? 34 : 20, FontStyle.Bold,
                    new Color(1f, .88f, .68f), TextAnchor.MiddleCenter));
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                StopContinuousCare("직접 멈췄습니다");
        }

        private void StopContinuousCare(string notice = null)
        {
            _continuousCare = ContinuousCareMode.None;
            _continuousEncounterSequence = -1;
            _nextContinuousActionAt = -10f;
            _continuousStopNotice = notice;
            _continuousStopNoticeUntil = string.IsNullOrEmpty(notice)
                ? -10f : Time.unscaledTime + 2.4f;
        }

        private void PerformV2Action(V2ActionId action, bool automatic = false)
        {
            if (!automatic && action != V2ActionId.Pat && action != V2ActionId.CheckDiaper)
                StopContinuousCare();
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
            if (outcome.Accepted)
            {
                if (!automatic && action == V2ActionId.Pat && !outcome.WasMisdiagnosis &&
                    _flow.Session.Night.V2.Diagnosis.CauseResolved)
                {
                    _continuousCare = ContinuousCareMode.Pat;
                    _continuousEncounterSequence =
                        _flow.Session.Night.V2.Diagnosis.EncounterSequence;
                }
                else if (!automatic && action == V2ActionId.CheckDiaper &&
                    (outcome.DiaperCheckResult == DiaperCheckResult.Wet ||
                     outcome.DiaperCheckResult == DiaperCheckResult.Stool))
                {
                    _continuousCare = ContinuousCareMode.Diaper;
                    _continuousEncounterSequence =
                        _flow.Session.Night.V2.Diagnosis.EncounterSequence;
                }
                if (_continuousCare != ContinuousCareMode.None)
                    _nextContinuousActionAt = Time.unscaledTime +
                        ActionAnimationDuration(action) + .18f;
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
            V2ActionId.DisposeDiaper => true,
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
                V2ActionId.ChangeDiaper => "깨끗한 기저귀로 갈아주었다.",
                V2ActionId.DisposeDiaper => "사용한 기저귀를 싸서 버렸다.",
                V2ActionId.WashHands => "비누로 손을 씻었다.",
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
            GUI.Label(new Rect(155, 245, 470, 34), "밤의 결과", _caption);
            GUI.Label(new Rect(155, 292, 470, 126), vm.CaregiverGrowth, _headline);
            GUI.Label(new Rect(155, 440, 470, 92), vm.Encouragement, _body);
            Fill(new Rect(155, 552, 420, 2), new Color(.84f, .62f, .31f, .5f));
            GUI.Label(new Rect(155, 574, 470, 34), $"밤의 기록 · {PresentationCopyMapper.NightGradeLabel(vm.Grade)}", _caption);
            GUI.Label(new Rect(155, 620, 470, 46), $"최장 연속 수면  {FormatDuration(vm.LongestSleepStretchMinutes)}", _headline);
            GUI.Label(new Rect(155, 680, 470, 40), $"총 수면 {FormatDuration(vm.TotalSleepMinutes)} · 깨어남 {vm.WakeCount}회", _body);
            GUI.Label(new Rect(155, 730, 470, 40), $"남은 체력 {vm.ParentStaminaAtDawn:0}", _body);

            Panel(new Rect(720, 200, 1090, 700));
            GUI.Label(new Rect(770, 245, 980, 36), "처음 발견한 신호", _caption);
            GUI.Label(new Rect(770, 288, 940, 68), vm.BabyResponseReflection, _headline);
            GUI.Label(new Rect(770, 370, 980, 32), "내가 선택한 행동", _caption);
            GUI.Label(new Rect(770, 408, 940, 58), vm.ActionLearning, _body);
            GUI.Label(new Rect(770, 482, 980, 32), "다음 밤에 바뀌는 것", _caption);
            GUI.Label(new Rect(770, 520, 940, 62), vm.FamilyUnderstanding, _body);
            float landscapeHabitBottom = DrawLandscapeHabitNotes(vm, 770, 598);
            float landscapeCompanionY = landscapeHabitBottom + 8f;
            GUI.Label(new Rect(770, landscapeCompanionY, 940, 52), vm.CompanionMessage, _caption);
            GUI.Label(new Rect(770, landscapeCompanionY + 60f, 940, 48), vm.ShareCardText, _caption);
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
            if (!_titleDropAttempted)
            {
                GUI.Label(new Rect(58, 80, 400, 90), "02:47", _display);
                if (_lyingSleepArt != null)
                {
                    var sleepingRect = new Rect(130, 430, 820, 820);
                    DrawAnchoringShadow(new Rect(130, 430, 820, 570), .68f, .09f);
                    DrawBabyTexture(sleepingRect, _lyingSleepArt);
                }
                DrawGlassPanel(new Rect(58, 180, 964, 250), .86f);
                GUI.Label(new Rect(104, 210, 872, 76), "드디어 잠들었다.",
                    OverlayLabelStyle(52, FontStyle.Bold, Color.white));
                GUI.Label(new Rect(104, 302, 872, 96),
                    "이제 침대에 내려놓기만 하면 된다.",
                    OverlayLabelStyle(40, FontStyle.Normal, new Color(.9f, .92f, .94f)));
                if (DrawPrimaryButton(new Rect(90, 1420, 900, 132), "조심히 내려놓기"))
                {
                    _titleDropAttempted = true;
                    _audio?.PlayAction(new V2ActionOutcome { Action = V2ActionId.Laydown, Accepted = true });
                    TriggerImpact(new Color(.95f, .32f, .24f, .62f), 10f, .38f);
                }
                return;
            }

            GUI.Label(new Rect(70, 120, 940, 120), "NOT A NAP", new GUIStyle(_title) { fontSize = 92 });
            GUI.Label(new Rect(70, 245, 940, 84), "눕히면 깬다", Centered(_display));
            if (_introBabyArt != null)
                DrawBabyTexture(new Rect(190, 315, 700, 700), _introBabyArt);
            GUI.Label(new Rect(90, 935, 900, 80), "등이 닿았다. 눈이 번쩍.",
                OverlayLabelStyle(46, FontStyle.Bold, new Color(1f, .68f, .42f),
                    TextAnchor.MiddleCenter));
            DrawGlassPanel(new Rect(58, 1040, 964, 310), .86f);
            GUI.Label(new Rect(104, 1070, 872, 64), "오늘 밤 목표",
                OverlayLabelStyle(46, FontStyle.Bold, Color.white));
            GUI.Label(new Rect(104, 1150, 872, 145),
                "아침 6시까지 함께 버텨라.\n깊은 잠 · 체력 · 맨손 눕히기 중 둘을 지켜라.",
                OverlayLabelStyle(40, FontStyle.Normal, new Color(.9f, .92f, .94f),
                    TextAnchor.UpperLeft, true));
            if (DrawPrimaryButton(new Rect(90, 1420, 900, 132), "오늘 밤 버티기  →"))
            {
                _introBeat = 0;
                _flow.BeginFamilySetup();
            }
        }

        private void DrawCarePairSetup(SetupViewModel vm, bool portrait)
        {
            if (portrait)
            {
                GUI.Label(new Rect(48, 145, 984, 42), "아빠의 돌봄 방식", _caption);
                DrawCareStyleButton(new Rect(48, 190, 305, 56), "바로 반응", CaregiverStyle.Responsive, vm.CaregiverStyle);
                DrawCareStyleButton(new Rect(388, 190, 305, 56), "잠시 관찰", CaregiverStyle.Observant, vm.CaregiverStyle);
                DrawCareStyleButton(new Rect(727, 190, 305, 56), "차례로 확인", CaregiverStyle.Methodical, vm.CaregiverStyle);
                GUI.Label(new Rect(48, 252, 984, 42), "오늘 밤 아기의 반응", _caption);
                DrawTemperamentButton(new Rect(48, 298, 305, 50), "반응이 잔잔함", Temperament.Soft, vm);
                DrawTemperamentButton(new Rect(388, 298, 305, 50), "자극에 민감", Temperament.Sensitive, vm);
                DrawTemperamentButton(new Rect(727, 298, 305, 50), "배고픔 신호 빠름", Temperament.Hungry, vm);
                return;
            }

            GUI.Label(new Rect(90, 132, 430, 32), "아빠의 돌봄 방식", _caption);
            DrawCareStyleButton(new Rect(90, 170, 230, 54), "바로 반응", CaregiverStyle.Responsive, vm.CaregiverStyle);
            DrawCareStyleButton(new Rect(335, 170, 230, 54), "잠시 관찰", CaregiverStyle.Observant, vm.CaregiverStyle);
            DrawCareStyleButton(new Rect(580, 170, 230, 54), "차례로 확인", CaregiverStyle.Methodical, vm.CaregiverStyle);
            GUI.Label(new Rect(860, 132, 500, 32), "오늘 밤 아기의 반응", _caption);
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
            GUI.Label(new Rect(48, 1505, 984, 66), $"오늘 밤 쓸 물건 {vm.Slots}개를 고르세요.",
                OverlayLabelStyle(26, FontStyle.Bold, new Color(0.74f, 0.79f, 0.84f)));
            if (DrawPrimaryButton(new Rect(100, 1695, 880, 120),
                vm.CanStart ? "이 물건으로 밤 시작  →" : $"물건 {vm.Slots}개 선택", vm.CanStart))
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
            GUI.Label(new Rect(110, 260, 860, 48), "밤의 결과", _caption);
            GUI.Label(new Rect(110, 320, 860, 128), vm.CaregiverGrowth, _headline);
            GUI.Label(new Rect(110, 470, 860, 82), vm.Encouragement, _body);
            Fill(new Rect(110, 566, 760, 2), new Color(.84f, .62f, .31f, .5f));
            GUI.Label(new Rect(110, 592, 860, 42), $"밤의 기록 · {PresentationCopyMapper.NightGradeLabel(vm.Grade)}", _caption);
            GUI.Label(new Rect(110, 646, 860, 56), $"최장 연속 수면  {FormatDuration(vm.LongestSleepStretchMinutes)}", _headline);
            GUI.Label(new Rect(110, 718, 860, 72), $"총 수면 {FormatDuration(vm.TotalSleepMinutes)} · 깨어남 {vm.WakeCount}회 · 남은 체력 {vm.ParentStaminaAtDawn:0}", _body);
            Panel(new Rect(60, 880, 960, 850));
            GUI.Label(new Rect(110, 930, 860, 42), "처음 발견한 신호", _caption);
            GUI.Label(new Rect(110, 982, 860, 102), vm.BabyResponseReflection, _headline);
            GUI.Label(new Rect(110, 1098, 860, 42), "내가 선택한 행동", _caption);
            GUI.Label(new Rect(110, 1148, 860, 82), vm.ActionLearning, _body);
            GUI.Label(new Rect(110, 1240, 860, 42), "다음 밤에 바뀌는 것", _caption);
            GUI.Label(new Rect(110, 1290, 860, 88), vm.FamilyUnderstanding, _body);
            float portraitHabitBottom = DrawPortraitHabitNotes(vm, 110, 1385);
            float portraitCompanionY = portraitHabitBottom + 4f;
            GUI.Label(new Rect(110, portraitCompanionY, 860, 64), vm.CompanionMessage, _caption);
            GUI.Label(new Rect(110, portraitCompanionY + 72f, 860, 58), vm.ShareCardText, _caption);
            string nextLabel = vm.HasNextNight ? NextNightButtonLabel(vm.NightId) : "엔딩 보기 →";
            if (GUI.Button(new Rect(100, 1745, 880, 110), nextLabel, _button))
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

        private float DrawLandscapeHabitNotes(V2DiaryViewModel vm, float x, float y)
        {
            GUI.Label(new Rect(x, y, 940, 28), "오늘 형성된 습관", _caption);
            float cursor = y + 35f;
            for (int i = 0; i < vm.HabitNotes.Count && i < 2; i++)
            {
                string copy = $"• {vm.HabitNotes[i]}  {vm.HabitEffects[i]}";
                float height = Mathf.Clamp(_body.CalcHeight(new GUIContent(copy), 940), 45f, 60f);
                GUI.Label(new Rect(x, cursor, 940, height), copy, _body);
                cursor += height + 8f;
            }
            return cursor;
        }

        private float DrawPortraitHabitNotes(V2DiaryViewModel vm, float x, float y)
        {
            GUI.Label(new Rect(x, y, 860, 35), "오늘 형성된 습관", _caption);
            float cursor = y + 42f;
            for (int i = 0; i < vm.HabitNotes.Count && i < 2; i++)
            {
                string copy = $"• {vm.HabitNotes[i]} · {vm.HabitEffects[i]}";
                float height = Mathf.Clamp(_body.CalcHeight(new GUIContent(copy), 860), 52f, 72f);
                GUI.Label(new Rect(x, cursor, 860, height), copy, _body);
                cursor += height + 8f;
            }
            return cursor;
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
            GUI.Label(new Rect(x + 60, y + 55, panelWidth - 120, 80),
                $"{_flow.BabyName}의 백일 · {PresentationCopyMapper.EndingStatusLabel(vm.IsSuccess)}", statusStyle);
            GUI.Label(new Rect(x + 110, y + 130, panelWidth - 220, 55),
                $"지켜 낸 조건  {vm.MetConditionCount} / {vm.TotalConditionCount}",
                Centered(_headline));
            Texture2D endingPortrait = _familyRolled ? GeneticBabyPortrait() : null;
            var endingArtRect = new Rect(x + panelWidth * .5f - 52f,
                y + 185f, 104f, 104f);
            if (endingPortrait != null)
            {
                GUI.DrawTexture(new Rect(endingArtRect.center.x - endingArtRect.width * .34f,
                    endingArtRect.yMax - 10f, endingArtRect.width * .68f, 14f),
                    _itemShadow, ScaleMode.StretchToFill, true);
                DrawGeneticPortrait(endingArtRect, endingPortrait);
            }
            else
            {
                var symbolStyle = Centered(new GUIStyle(_title) { fontSize = _portrait ? 110 : 84 });
                symbolStyle.normal.textColor = accent;
                GUI.Label(new Rect(x + 60, y + 190, panelWidth - 120, 95), vm.Symbol, symbolStyle);
            }
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
                "놓친 조건 · " +
                (vm.UnmetConditions.Count > 0 ? string.Join(" · ", vm.UnmetConditions) : "세 가지 신호를 모두 지켰어요"),
                Centered(_caption));
            GUI.Label(new Rect(x + 110, retryY, panelWidth - 220, 48), vm.RetrySuggestion, Centered(_caption));
            if (GUI.Button(new Rect(x + panelWidth * 0.2f, buttonY, panelWidth * 0.6f, _portrait ? 82 : 64),
                "첫째 밤부터 다시", _button))
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
            if (outcome.BlockReason == V2ActionBlockReason.HandsDirty)
                return "대변 기저귀를 처리했어요. 수유 전에 욕실에서 비누로 손을 씻어주세요.";
            if (outcome.BlockReason == V2ActionBlockReason.ToolRequired)
            {
                if (outcome.Action == V2ActionId.ChangeDiaper)
                    return "먼저 기저귀 상태를 확인해주세요.";
                if (outcome.Action == V2ActionId.DisposeDiaper)
                    return "먼저 젖은 기저귀를 새것으로 갈아주세요.";
                if (outcome.Action == V2ActionId.WashHands)
                    return "지금은 손 씻기가 필요한 상태가 아니에요.";
                return "욕실에서 탕온계를 먼저 챙겨주세요.";
            }
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
            if (outcome.Action == V2ActionId.CheckDiaper &&
                outcome.DiaperCheckResult == DiaperCheckResult.Stool)
                return "대변 기저귀예요. 처리 시간이 더 들고 울음이 조금 커집니다.";
            if (outcome.Action == V2ActionId.CheckDiaper &&
                outcome.DiaperCheckResult == DiaperCheckResult.Wet)
                return "기저귀가 젖어 있어요. 기저귀를 갈아주세요.";
            if (outcome.Action == V2ActionId.CheckDiaper &&
                outcome.DiaperCheckResult == DiaperCheckResult.Clean)
                return "기저귀는 깨끗해요. 75분 동안 다시 확인하지 않아도 돼요.";
            if (outcome.Action == V2ActionId.ChangeDiaper)
                return "새 기저귀를 채웠어요. 이제 사용한 기저귀를 싸서 버려주세요.";
            if (outcome.Action == V2ActionId.DisposeDiaper)
                return outcome.DiaperCheckResult == DiaperCheckResult.Stool
                    ? "기저귀 처리는 끝났어요. 수유 전에 욕실에서 비누로 손을 씻어주세요."
                    : "기저귀를 싸서 버렸어요. 불편함이 해결됐어요.";
            if (outcome.Action == V2ActionId.WashHands)
                return "손 씻기 완료. 이제 다시 아기에게 돌아가도 좋아요.";
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

        private static string NextActionHeadline(V2PlayViewModel vm)
        {
            if (vm.HandsNeedWashing)
                return vm.CaregiverLocation == HomeLocation.Bathroom
                    ? "다음 행동 · 비누로 손 씻기"
                    : "다음 행동 · 욕실로 이동해 손 씻기";
            if (vm.DiaperChangedPendingDisposal)
                return "다음 행동 · 사용한 기저귀 싸서 버리기";
            if (vm.DiaperStoolConfirmed)
                return "다음 행동 · 대변 기저귀 처리하기";
            if (vm.DiaperWetConfirmed)
                return "다음 행동 · 소변 기저귀 갈기";
            if (!vm.CauseResolved)
            {
                if (vm.FeedingReady && vm.RevealedCause == WakeCause.Hunger)
                    return "다음 행동 · 준비한 분유 수유하기";
                if (vm.DiaperRecommendationVisible)
                    return "다음 행동 · 기저귀 상태 확인하기";
                return "다음 행동 · 아기의 입·손·호흡 살펴보기";
            }
            if (vm.SleepStage == V2SleepStage.RemActiveSleep)
                return "다음 행동 · 잠이 깊어질 때까지 기다리기";
            if (vm.SleepStage == V2SleepStage.NremDeepSleep && !vm.DeepSleepObserved)
                return "다음 행동 · 팔다리 이완 확인하기";
            if (vm.SleepStage == V2SleepStage.NremDeepSleep && vm.BabyHeld)
                return "다음 행동 · 조심히 침대에 눕히기";
            if (vm.SleepStage == V2SleepStage.NremDeepSleep)
                return "다음 행동 · 깨우지 말고 지켜보기";
            return vm.BabyHeld
                ? "다음 행동 · 토닥이기 시작"
                : "다음 행동 · 목을 받치고 안기";
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
