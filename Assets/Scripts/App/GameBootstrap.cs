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
        /// <summary>AI 서술 요청을 밤당 1회로 묶는 게이트. 런을 다시 시작하면 새로 만든다.</summary>
        private NarrativeCallGate _narrativeGate = new NarrativeCallGate();
        private BabyVisualPresenter _babyVisual;
        private GameFeelAudio _audio;
        private V2PresentationActionResult _lastResult;
        private HomeMoveOutcome _lastMove;
        private int _timedEncounterSequence = -1;
        /// <summary>원인을 못 찾은 채 이어 붙일 수 있는 자동 토닥임 횟수.</summary>
        private const int UnresolvedPatRepeatLimit = 3;
        private int _continuousPatRepeats;
        /// <summary>방 안 물건 위에 얹어 그릴 신체 행동 콜아웃. 프레임마다 다시 채운다.</summary>
        private BodyActionLink? _deferredCallout;
        /// <summary>가로 화면에서 신체 행동 콜아웃이 내려갈 수 있는 하단 한계선.</summary>
        private float _landscapeCalloutBottom = 674f;
        private float _decisionDeadline;
        private bool _timeoutSent;
        private ActionGroup _actionGroup = ActionGroup.Diagnose;
        private int _actionEncounterSequence = -1;
        private bool _observationSheetOpen;
        private bool _portrait;
        private float _nextKeyboardMoveAt;
        private bool _directHintSeen;
        private float _directCueHiddenUntil = -10f;
        /// <summary>이번 프레임에 표시할 결정 잔여 초. 각성이 해소됐으면 -1.</summary>
        private int _decisionSecondsShown = -1;
        private V2ActionId? _roomObjectAction;
        private float _roomObjectAnimationStarted = -10f;

        /// <summary>
        /// 게임 시계는 행동이 분을 쓸 때만 움직이는 턴제 시계라, 값만 바꿔 그리면
        /// 22:23이 22:49로 순간이동해 아무도 시간이 지난 걸 눈치채지 못한다.
        /// 굴러가는 위치를 따로 들고 있다가 상단 바 두 곳에 함께 먹인다.
        /// </summary>
        private int _clockTargetMinutes = -1;
        private float _clockRollFrom;
        private float _clockRollStart = -10f;
        private int _clockDeltaMinutes;
        private float _clockBadgeUntil = -10f;
        private const float ClockRollSeconds = .55f;
        private const float ClockBadgeSeconds = 2.2f;

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
        private Texture2D _geneticDoubleStraight;
        private Texture2D _geneticMonolidCurly;
        private Texture2D _geneticMonolidStraight;
        private Material _mouthWarpMaterial;
        private readonly Dictionary<string, MouthWarpProfile> _mouthWarpProfiles =
            new Dictionary<string, MouthWarpProfile>();
        private readonly Dictionary<string, Texture2D> _babyVariantTextures =
            new Dictionary<string, Texture2D>();
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
        /// <summary>인트로 1단계에서 이미 눌러 확인한 부위 비트마스크.</summary>
        private int _introProbeMask;
        /// <summary>마지막으로 확인한 부위의 결과 한 줄.</summary>
        private string _introProbeNote;
        /// <summary>인트로 모니터 장면에서 실제로 화면을 봤는지.</summary>
        private bool _introMonitorRead;
        private bool _dadMonolid;
        private bool _momMonolid = true;
        private bool _dadStraightHair;
        private bool _momStraightHair = true;
        private bool _dadBigMouth;
        private bool _momBigMouth = true;
        private bool _dadBigNose;
        private bool _momBigNose = true;
        private bool _dadHighVoice;
        private bool _momHighVoice = true;
        private bool _dadDeepSkin;
        private bool _momDeepSkin = true;
        private bool _babyMonolid;
        private bool _babyStraightHair;
        private bool _babyBigMouth;
        private bool _babyBigNose;
        private bool _babyHighVoice;
        private bool _babyDeepSkin;
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

        private static readonly HashSet<string> BabyStateTextureNames = new HashSet<string>
        {
            "awake_calm", "fuss_soft", "cry_hard", "hunger_early", "hunger_late",
            "drowsy", "rem_active", "nrem_deep", "relaxed", "moro_startle",
            "pacifier_accept", "pacifier_reject"
        };

        private static readonly HashSet<string> BabyAnimatedTextureNames = new HashSet<string>
        {
            "awake_0", "awake_1", "awake_2", "awake_3",
            "fuss_0", "fuss_1", "fuss_2", "fuss_3",
            "sleep_0", "sleep_1", "sleep_2", "sleep_3"
        };

        private static readonly HashSet<string> BabyInteractionTextureNames = new HashSet<string>
        {
            "pat_0", "pat_1", "pat_2", "pat_3",
            "hold_0", "hold_1", "hold_2", "hold_3",
            "feed_0", "feed_1", "feed_2", "feed_3",
            "carrier_0", "carrier_1", "carrier_2", "carrier_3",
            "pacifier_0", "pacifier_1", "pacifier_2", "pacifier_3",
            "diaper_check", "diaper_change", "limb_check", "temperature_check", "lying_sleep"
        };

        private enum ActionGroup { Diagnose, Care, Feed }
        private enum ContinuousCareMode { None, Pat, Diaper }

        private readonly struct BodyActionLink
        {
            public readonly Rect Hotspot;
            public readonly V2ActionId Action;
            public readonly string Label;
            public readonly string Prompt;

            public BodyActionLink(Rect hotspot, V2ActionId action, string label, string prompt)
            {
                Hotspot = hotspot;
                Action = action;
                Label = label;
                Prompt = prompt;
            }
        }

        private const float RecommendedGlowBaseAlpha = .30f;
        private const float RecommendedGlowPulseAlpha = .12f;

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
            _geneticDoubleStraight = Resources.Load<Texture2D>("Art/Baby/Genetics/double_straight");
            _geneticMonolidCurly = Resources.Load<Texture2D>("Art/Baby/Genetics/monolid_curly");
            _geneticMonolidStraight = Resources.Load<Texture2D>("Art/Baby/Genetics/monolid_straight");
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
            // 아기 울음은 밤 화면에서만 난다. 여기서 비워 주지 않으면 마지막 울음 세기가
            // 남아 육아일지·엔딩·다음 밤 세팅에서까지 1.4초마다 계속 재생된다.
            if (_flow.Screen != ScreenState.Play) _audio.ClearBabyState();
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
            DrawGameFeelOverlay(impactProgress);
            GUI.matrix = oldMatrix;
        }

        private void DrawGameFeelOverlay(float impactProgress)
        {
            if (impactProgress < 1f && _impactColor.a > 0f)
            {
                var color = _impactColor;
                color.a *= (1f - impactProgress) * 0.38f;
                FillViewport(color);
            }
            float transition = Mathf.Clamp01((Time.unscaledTime - _screenTransitionStarted) / 0.48f);
            if (transition < 1f)
                FillViewport(new Color(0.005f, 0.012f, 0.024f,
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
            // OnGUI 시작 시 실제 화면 전체에 이미 배경을 그렸다. 기준 캔버스에
            // 배경을 다시 그리면 9:16보다 넓은 세로 화면에서 중앙만 어두워져
            // 좌우에 밝은 하드 엣지 밴드가 생긴다.
            FillViewport(new Color(0.01f, 0.025f, 0.05f, 0.25f));
        }

        private static void FillViewport(Color color)
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), color);
            GUI.matrix = previousMatrix;
        }

        private void DrawTitle()
        {
            if (_portrait) { DrawPortraitTitle(); return; }
            FillViewport(new Color(0.01f, 0.02f, 0.035f, 0.24f));
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
                GUI.Label(new Rect(165, 700, 540, 62), "이제 내려놓기만 하면 된다. 그 한 번이 제일 어렵다.", _body);
                if (DrawPrimaryButton(new Rect(1220, 760, 560, 104), "숨죽이고 내려놓기"))
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
            GUI.Label(new Rect(120, 235, 820, 70), "등이 닿으면 또 눈이 번쩍", _display);
            GUI.Label(new Rect(120, 340, 820, 70), "그래도 다시 안아 올리는 밤",
                OverlayLabelStyle(40, FontStyle.Bold, new Color(1f, .68f, .42f)));
            DrawGlassPanel(new Rect(120, 455, 790, 210), .8f);
            GUI.Label(new Rect(160, 480, 710, 56), "오늘 밤 할 일", _headline);
            GUI.Label(new Rect(160, 545, 710, 88),
                "아침 6시까지, 같이 버틴다.\n깊은 잠 · 내 체력 · 맨손 눕히기 — 셋 중 둘만 지키면 된다.", _body);
            if (DrawPrimaryButton(new Rect(120, 735, 790, 110), "오늘 밤도 버텨보기  →"))
            {
                ResetIntro();
                _flow.BeginFamilySetup();
            }
        }

        // ── 인트로(튜토리얼) ────────────────────────────────────────
        // 이 화면은 정답 맞히기가 아니다. 예전 인트로는 "입과 손"만 정답이고
        // 등·기저귀를 누르면 울음이 커지는 오답이었다. 그래서 플레이어는 배제
        // 절차 대신 찍기를 배웠고, 등 토닥이기는 한 번도 배우지 못했으며,
        // 주방 칩을 한 번 누르면 곧장 "먹였다"로 건너뛰어 분유 준비 순서가
        // 통째로 사라졌다.
        //
        // 지금은 본편과 같은 문법을 순서대로 한 번씩 밟는다.
        //   ① 세 곳(입과 손·등·기저귀)을 모두 눌러 배제하고 원인을 남긴다
        //   ② 아기띠로 두 손을 비우고 주방으로 옮긴다
        //   ③ 분유를 순서대로(섞기 → 식히고 온도 확인) 준비한다
        //   ④ 아기방으로 돌아와 먹이고, 등을 토닥여 재운다
        //   ⑤ 얕은 잠에서 실패해 보고, 깊은 잠을 확인한 뒤 내려놓는다
        //   ⑥ 백색소음기와 베이비 모니터의 쓰임을 각각 한 번씩 확인한다
        private const int IntroProbe = 0;
        private const int IntroCause = 1;
        private const int IntroCarrier = 2;
        private const int IntroToKitchen = 3;
        private const int IntroPrepMix = 4;
        private const int IntroPrepCool = 5;
        private const int IntroToNursery = 6;
        private const int IntroFeed = 7;
        private const int IntroSoothe = 8;
        private const int IntroFellAsleep = 9;
        private const int IntroLaydownFailed = 10;
        private const int IntroDeepSleep = 11;
        private const int IntroNoise = 12;
        private const int IntroMonitor = 13;
        private const int IntroDone = 14;

        // 1단계에서 눌러 본 곳을 기억한다. 세 곳을 다 확인해야 다음으로 간다.
        private const int ProbeMouth = 1;
        private const int ProbeBack = 2;
        private const int ProbeDiaper = 4;
        private const int ProbeAll = ProbeMouth | ProbeBack | ProbeDiaper;

        private static readonly Vector4 IntroMouthRatio = new Vector4(.38f, .17f, .24f, .2f);
        private static readonly Vector4 IntroChestRatio = new Vector4(.27f, .32f, .3f, .28f);
        private static readonly Vector4 IntroBackRatio = new Vector4(.58f, .3f, .25f, .34f);
        private static readonly Vector4 IntroDiaperRatio = new Vector4(.34f, .58f, .33f, .23f);
        private static readonly Vector4 IntroLimbRatio = new Vector4(.16f, .68f, .68f, .2f);
        private static readonly Vector4 IntroCribRatio = new Vector4(.08f, .92f, .84f, .17f);

        private void ResetIntro()
        {
            _introBeat = IntroProbe;
            _introProbeMask = 0;
            _introProbeNote = null;
            _introMonitorRead = false;
        }

        private bool IntroInKitchen => _introBeat == IntroPrepMix || _introBeat == IntroPrepCool ||
                                       _introBeat == IntroToNursery;

        private Rect IntroBabyRect()
        {
            float size = _portrait ? 650f : 620f;
            return _portrait
                ? new Rect(215f, 210f, size, size)
                : new Rect(1050f, 110f, size, size);
        }

        /// <summary>다음 버튼·방 이동 칩·소품 칩이 함께 쓰는 하단 띠.</summary>
        private Rect IntroStripRect() => _portrait
            ? new Rect(90f, 1250f, 900f, 132f)
            : new Rect(110f, 655f, 720f, 104f);

        private void DrawIntro()
        {
            FillViewport(new Color(0.01f, 0.015f, 0.025f, 0.48f));

            Rect babyRect = IntroBabyRect();
            if (_introBeat == IntroMonitor) DrawIntroMonitorScene(babyRect);
            else if (IntroInKitchen) DrawIntroKitchenProps();
            else DrawIntroBaby(babyRect);

            var copyPanel = _portrait
                ? new Rect(58f, 820f, 964f, 402f)
                : new Rect(110f, 150f, 780f, 470f);
            DrawGlassPanel(copyPanel, 0.82f);
            GUI.Label(new Rect(copyPanel.x + 38f, copyPanel.y + 32f,
                copyPanel.width - 76f, 54f), IntroStepLabel(),
                OverlayLabelStyle(_portrait ? 40 : 24, FontStyle.Bold,
                    new Color(0.96f, 0.69f, 0.31f)));
            GUI.Label(new Rect(copyPanel.x + 38f, copyPanel.y + 92f,
                copyPanel.width - 76f, _portrait ? 120f : 110f),
                IntroHeadline(),
                OverlayLabelStyle(_portrait ? 50 : 40, FontStyle.Bold,
                    Color.white, TextAnchor.UpperLeft));
            GUI.Label(new Rect(copyPanel.x + 38f, copyPanel.y + (_portrait ? 200f : 220f),
                copyPanel.width - 76f, _portrait ? 190f : 190f), IntroBody(),
                OverlayLabelStyle(_portrait ? 34 : 26, FontStyle.Normal,
                    new Color(0.9f, 0.92f, 0.94f), TextAnchor.UpperLeft, true));

            // 한 단계에 배울 것은 하나. 눌러야 할 대상만 화면에 살아 있게 둔다.
            switch (_introBeat)
            {
                case IntroProbe:
                    DrawIntroBodyProbe(babyRect, IntroMouthRatio,
                        IntroProbeDone(ProbeMouth) ? "입과 손 · 확인함" : "입과 손 · 눌러서 확인",
                        ProbeMouth);
                    DrawIntroBodyProbe(babyRect, IntroBackRatio,
                        IntroProbeDone(ProbeBack) ? "등 · 확인함" : "등 · 눌러서 확인",
                        ProbeBack);
                    DrawIntroBodyProbe(babyRect, IntroDiaperRatio,
                        IntroProbeDone(ProbeDiaper) ? "기저귀 · 확인함" : "기저귀 · 눌러서 확인",
                        ProbeDiaper);
                    if (_introProbeMask != ProbeAll) return;
                    break;
                case IntroPrepMix:
                case IntroPrepCool:
                    // 주방 소품 자체가 다음 단계 버튼이다. 별도의 진행 버튼을 같이
                    // 띄우면 순서를 밟지 않고도 넘어갈 수 있어 배우는 게 사라진다.
                    return;
                case IntroCarrier:
                    DrawIntroBodyProbe(babyRect, IntroChestRatio,
                        "가슴 · 아기띠로 안기", 0, IntroToKitchen);
                    return;
                case IntroToKitchen:
                    DrawIntroRoomProbe(0, "아기방", "지금 있는 방", false, 0);
                    DrawIntroRoomProbe(1, "주방", "분유를 준비하는 곳", true, IntroPrepMix);
                    DrawIntroRoomProbe(2, "욕실", "손을 씻는 곳", false, 0);
                    return;
                case IntroToNursery:
                    DrawIntroRoomProbe(0, "아기방", "아기가 기다리는 곳", true, IntroFeed);
                    DrawIntroRoomProbe(1, "주방", "지금 있는 방", false, 0);
                    DrawIntroRoomProbe(2, "욕실", "손을 씻는 곳", false, 0);
                    return;
                case IntroFeed:
                    DrawIntroBodyProbe(babyRect, IntroMouthRatio,
                        "입가 · 준비한 분유 먹이기", 0, IntroSoothe);
                    return;
                case IntroSoothe:
                    DrawIntroBodyProbe(babyRect, IntroBackRatio,
                        "등 · 같은 리듬으로 토닥이기", 0, IntroFellAsleep);
                    DrawIntroBodyProbe(babyRect, IntroMouthRatio,
                        "입가 · 쪽쪽이 물리기 (밤 3회)", 0, IntroFellAsleep);
                    return;
                case IntroFellAsleep:
                    DrawIntroBodyProbe(babyRect, IntroLimbRatio,
                        "팔다리 · 힘이 풀렸는지 살피기", 0, IntroDeepSleep);
                    DrawIntroBodyProbe(babyRect, IntroCribRatio,
                        "침대 · 지금 내려놓기", 0, IntroLaydownFailed);
                    return;
                case IntroDeepSleep:
                    DrawIntroBodyProbe(babyRect, IntroCribRatio,
                        "침대 · 천천히 내려놓기", 0, IntroNoise);
                    return;
                case IntroNoise:
                    DrawIntroPropChip("🔊  백색소음기 켜기",
                        "진정 효과 0 · 각성 간격 +25분 · 외부 소음 차단",
                        () => _introBeat = IntroMonitor);
                    return;
                case IntroMonitor:
                    // 먼저 막힌 화면을 보여주고, 모니터를 누른 뒤에야 열리게 한다.
                    // 설명만 읽히면 이 물건을 왜 챙기는지 끝내 모른다.
                    if (_introMonitorRead) break;
                    DrawIntroPropChip("📟  베이비 모니터 보기",
                        "아기방 밖에서만 · 1회 2분 · 체력 -1",
                        () => _introMonitorRead = true);
                    return;
            }

            var nextRect = _portrait
                ? new Rect(90f, 1420f, 900f, 128f)
                : new Rect(110f, 700f, 720f, 92f);
            string next = _introBeat switch
            {
                IntroProbe => "세 곳을 다 확인했다  →",
                IntroCause => "아기띠를 매러 가기  →",
                IntroMonitor => "이제 알겠다  →",
                IntroLaydownFailed => "다시 해보기",
                _ => "오늘 밤 준비하기  →"
            };
            if (!DrawPrimaryButton(nextRect, next)) return;
            _audio?.PlayUi();
            switch (_introBeat)
            {
                case IntroProbe: _introBeat = IntroCause; break;
                case IntroCause: _introBeat = IntroCarrier; break;
                case IntroMonitor: _introBeat = IntroDone; break;
                case IntroLaydownFailed: _introBeat = IntroFellAsleep; break;
                default: _flow.CompleteIntro(); break;
            }
        }

        private void DrawIntroBaby(Rect babyRect)
        {
            Texture2D portrait = _familyRolled ? GeneticBabyPortrait() : _introBabyArt;
            if (portrait == null) return;
            GUI.DrawTexture(new Rect(babyRect.center.x - babyRect.width * 0.28f,
                babyRect.yMax - babyRect.height * 0.12f, babyRect.width * 0.56f,
                babyRect.height * 0.09f), _itemShadow, ScaleMode.StretchToFill, true);
            DrawGeneticPortrait(babyRect, portrait);
        }

        /// <summary>
        /// 주방 장면. 본편의 소품 세 개를 같은 순서로 놓아 "분유는 주방에서
        /// 순서대로 만든다"를 눈으로 먼저 익히게 한다.
        /// </summary>
        private void DrawIntroKitchenProps()
        {
            bool mixed = _introBeat > IntroPrepMix;
            bool cooled = _introBeat > IntroPrepCool;
            Rect powder = _portrait ? new Rect(230f, 250f, 210f, 250f) : new Rect(1060f, 170f, 220f, 265f);
            Rect bottle = _portrait ? new Rect(470f, 230f, 185f, 285f) : new Rect(1305f, 150f, 195f, 300f);
            Rect cooling = _portrait ? new Rect(320f, 550f, 250f, 200f) : new Rect(1500f, 250f, 240f, 210f);

            if (_formulaTinArt != null)
                GUI.DrawTexture(powder, _formulaTinArt, ScaleMode.ScaleToFit, true);
            if (_feedingBottleArt != null)
                GUI.DrawTexture(bottle, _feedingBottleArt, ScaleMode.ScaleToFit, true);
            if (_coolingBasinArt != null)
                GUI.DrawTexture(cooling, _coolingBasinArt, ScaleMode.ScaleToFit, true);

            // 젖병 안의 분유. 섞기 전에는 비어 있고, 식힌 뒤에는 색이 가라앉는다.
            if (mixed)
            {
                var body = new Rect(bottle.x + bottle.width * .405f,
                    bottle.y + bottle.height * .46f, bottle.width * .19f, bottle.height * .3f);
                Fill(new Rect(body.x, body.yMax - body.height * .72f, body.width, body.height * .72f),
                    cooled ? new Color(.88f, .71f, .39f, .82f) : new Color(.96f, .63f, .25f, .78f));
            }

            if (_introBeat == IntroPrepMix)
            {
                DrawIntroPropHotspot(powder, "분유가루를 떠서 물에 섞기", IntroPrepCool);
                DrawIntroPropBlocked(cooling, "아직 섞지 않았어요");
            }
            else if (_introBeat == IntroPrepCool)
                DrawIntroPropHotspot(cooling, "젖병을 물에 담가 식히고 온도 확인", IntroToNursery);
            else
                DrawIntroPropDone(bottle, "먹일 준비 완료");
        }

        private void DrawIntroPropHotspot(Rect rect, string label, int nextBeat)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * 3.1f) + 1f) * .5f;
            bool hovered = rect.Contains(Event.current.mousePosition);
            Color previous = GUI.color;
            GUI.color = new Color(1f, .74f, .34f, hovered ? .5f : .24f + pulse * .16f);
            GUI.DrawTexture(rect, _itemGlow, ScaleMode.StretchToFill, true);
            GUI.color = previous;

            var labelRect = new Rect(rect.center.x - (_portrait ? 230f : 190f), rect.yMax + 6f,
                _portrait ? 460f : 380f, _portrait ? 48f : 38f);
            Fill(labelRect, new Color(0.02f, 0.03f, 0.05f, .58f));
            GUI.Label(labelRect, label, OverlayLabelStyle(_portrait ? 24 : 18, FontStyle.Bold,
                hovered ? new Color(.45f, .9f, .86f) : new Color(1f, .92f, .76f),
                TextAnchor.MiddleCenter));
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none) ||
                GUI.Button(labelRect, GUIContent.none, GUIStyle.none))
            {
                _audio?.PlayUi();
                _introBeat = nextBeat;
                TriggerImpact(new Color(.45f, .88f, .62f, .5f), 2f, .3f);
            }
        }

        /// <summary>순서를 어긴 소품. 누를 수 없고 왜 아직인지만 말한다.</summary>
        private void DrawIntroPropBlocked(Rect rect, string reason)
        {
            var labelRect = new Rect(rect.center.x - (_portrait ? 200f : 165f), rect.yMax + 6f,
                _portrait ? 400f : 330f, _portrait ? 48f : 38f);
            Fill(labelRect, new Color(0.02f, 0.03f, 0.05f, .5f));
            GUI.Label(labelRect, reason, OverlayLabelStyle(_portrait ? 22 : 17, FontStyle.Bold,
                new Color(.62f, .64f, .66f), TextAnchor.MiddleCenter));
        }

        private void DrawIntroPropDone(Rect rect, string label)
        {
            var labelRect = new Rect(rect.center.x - (_portrait ? 200f : 165f), rect.yMax + 6f,
                _portrait ? 400f : 330f, _portrait ? 48f : 38f);
            Fill(labelRect, new Color(0.02f, 0.03f, 0.05f, .58f));
            GUI.Label(labelRect, label, OverlayLabelStyle(_portrait ? 24 : 18, FontStyle.Bold,
                new Color(.55f, .92f, .67f), TextAnchor.MiddleCenter));
        }

        /// <summary>인트로용 방 이동 칩. 본편의 방 이동 스트립과 같은 문법으로 가르친다.</summary>
        private void DrawIntroRoomProbe(int index, string label, string detail, bool target, int nextBeat)
        {
            Rect strip = IntroStripRect();
            float gap = _portrait ? 14f : 12f;
            float width = (strip.width - gap * 2f) / 3f;
            var chip = new Rect(strip.x + index * (width + gap), strip.y, width, strip.height);

            bool hovered = target && chip.Contains(Event.current.mousePosition);
            float pulse = (Mathf.Sin(Time.unscaledTime * 3.1f + index) + 1f) * .5f;
            if (target)
            {
                Color previous = GUI.color;
                GUI.color = new Color(1f, .74f, .34f, hovered ? .5f : .22f + pulse * .16f);
                GUI.DrawTexture(chip, _itemGlow, ScaleMode.StretchToFill, true);
                GUI.color = previous;
            }
            DrawGlassPanel(chip, target ? (hovered ? .9f : .72f) : .34f, target);
            GUI.Label(new Rect(chip.x, chip.y + (_portrait ? 22f : 16f), chip.width,
                    _portrait ? 48f : 38f), label,
                OverlayLabelStyle(_portrait ? 36 : 26, FontStyle.Bold,
                    target ? new Color(1f, .88f, .68f) : new Color(.55f, .57f, .6f),
                    TextAnchor.MiddleCenter));
            GUI.Label(new Rect(chip.x, chip.y + (_portrait ? 74f : 56f), chip.width,
                    _portrait ? 44f : 34f), detail,
                OverlayLabelStyle(_portrait ? 26 : 18, FontStyle.Normal,
                    target ? new Color(.92f, .86f, .74f) : new Color(.45f, .47f, .5f),
                    TextAnchor.MiddleCenter));

            if (!target) return;
            if (GUI.Button(chip, GUIContent.none, GUIStyle.none))
            {
                _audio?.PlayUi();
                _introBeat = nextBeat;
            }
        }

        /// <summary>방 안 물건(백색소음기·베이비 모니터)을 누르는 한 칸짜리 칩.</summary>
        private void DrawIntroPropChip(string label, string detail, System.Action onClick)
        {
            Rect chip = IntroStripRect();
            bool hovered = chip.Contains(Event.current.mousePosition);
            float pulse = (Mathf.Sin(Time.unscaledTime * 3.1f) + 1f) * .5f;
            Color previous = GUI.color;
            GUI.color = new Color(1f, .74f, .34f, hovered ? .5f : .22f + pulse * .16f);
            GUI.DrawTexture(chip, _itemGlow, ScaleMode.StretchToFill, true);
            GUI.color = previous;
            DrawGlassPanel(chip, hovered ? .9f : .72f, true);
            GUI.Label(new Rect(chip.x, chip.y + (_portrait ? 22f : 16f), chip.width,
                    _portrait ? 48f : 38f), label,
                OverlayLabelStyle(_portrait ? 36 : 26, FontStyle.Bold,
                    new Color(1f, .88f, .68f), TextAnchor.MiddleCenter));
            GUI.Label(new Rect(chip.x, chip.y + (_portrait ? 74f : 56f), chip.width,
                    _portrait ? 44f : 34f), detail,
                OverlayLabelStyle(_portrait ? 26 : 18, FontStyle.Normal,
                    new Color(.92f, .86f, .74f), TextAnchor.MiddleCenter));
            if (GUI.Button(chip, GUIContent.none, GUIStyle.none))
            {
                _audio?.PlayUi();
                onClick();
            }
        }

        /// <summary>
        /// 인트로 모니터 장면. 아기를 눕히고 방을 나선 상태이므로 아기가 화면에 없다.
        /// 모니터를 보기 전에는 "확인 불가"만 서 있고, 본 뒤에야 상태가 열린다.
        /// 본편의 BabyStateVisible 규칙을 그대로 축소해 보여주는 자리다.
        /// </summary>
        private void DrawIntroMonitorScene(Rect babyRect)
        {
            var card = new Rect(babyRect.x + babyRect.width * .1f,
                babyRect.y + babyRect.height * .28f,
                babyRect.width * .8f, babyRect.height * .44f);
            DrawGlassPanel(card, .78f);
            GUI.Label(new Rect(card.x, card.y + (_portrait ? 26f : 20f), card.width, _portrait ? 44f : 34f),
                _introMonitorRead ? "아기방 · 📟 모니터" : "아기방",
                OverlayLabelStyle(_portrait ? 28 : 21, FontStyle.Bold,
                    new Color(.74f, .79f, .84f), TextAnchor.MiddleCenter));
            GUI.Label(new Rect(card.x, card.y + (_portrait ? 82f : 62f), card.width, _portrait ? 62f : 50f),
                _introMonitorRead ? "기분 74  좋음" : "확인 불가",
                OverlayLabelStyle(_portrait ? 46 : 36, FontStyle.Bold,
                    _introMonitorRead ? new Color(.49f, .84f, .61f) : new Color(.55f, .57f, .6f),
                    TextAnchor.MiddleCenter));
            GUI.Label(new Rect(card.x + 24f, card.y + (_portrait ? 158f : 124f),
                    card.width - 48f, _portrait ? 120f : 96f),
                _introMonitorRead
                    ? "화면 속 아기가 미동도 없다. 숨소리만 고르게 들린다.\n앞으로 30분은 이대로 상태가 보인다."
                    : "여기서는 아기가 보이지 않는다.\n진정도도 울음도 알 수 없다.",
                OverlayLabelStyle(_portrait ? 26 : 20, FontStyle.Normal,
                    _introMonitorRead ? new Color(.9f, .92f, .94f) : new Color(.94f, .76f, .52f),
                    TextAnchor.UpperCenter, true));
        }

        private bool IntroProbeDone(int probe) => (_introProbeMask & probe) != 0;

        private string IntroStepLabel() => _introBeat switch
        {
            IntroProbe or IntroCause => "21:00 · 신호 읽기",
            IntroCarrier => "21:05 · 두 손 비우기",
            IntroToKitchen => "21:06 · 방을 옮겨서",
            IntroPrepMix or IntroPrepCool => "21:10 · 주방에서 순서대로",
            IntroToNursery => "21:25 · 아기에게 돌아가기",
            IntroFeed => "21:28 · 수유",
            IntroSoothe => "21:45 · 달래기",
            IntroFellAsleep or IntroLaydownFailed => "22:00 · 잠들었다",
            IntroDeepSleep => "22:30 · 깊은 잠",
            IntroNoise => "22:35 · 잠을 이어 주기",
            IntroMonitor => "22:40 · 방을 비우기",
            _ => "22:45 · 오늘 밤의 첫 성공"
        };

        private string IntroHeadline() => _introBeat switch
        {
            IntroProbe => "왜 이렇게 보채는 걸까.",
            IntroCause => "남은 건 배고픔 하나.",
            IntroCarrier => "분유는 주방에 있다.",
            IntroToKitchen => "안은 채로 간다.",
            IntroPrepMix => "분유에도 순서가 있다.",
            IntroPrepCool => "뜨거운 걸 먹일 순 없다.",
            IntroToNursery => "젖병이 준비됐다.",
            IntroFeed => "이제 먹일 수 있다.",
            IntroSoothe => "먹였다고 바로 자지는 않는다.",
            IntroFellAsleep => "잠들었다. 아직 얕은 잠이지만.",
            IntroLaydownFailed => "등이 닿자마자 눈이 번쩍.",
            IntroDeepSleep => "팔다리 힘이 풀렸다. 지금이다.",
            IntroNoise => "이제 이 잠을 지켜야 한다.",
            IntroMonitor => _introMonitorRead ? "이제 보인다." : "여기선 아무것도 안 보인다.",
            _ => "숨이 그대로 이어진다."
        };

        private string IntroBody() => _introBeat switch
        {
            IntroProbe =>
                "정답을 맞히는 게 아니다. 아닌 것부터 하나씩 지워 나가면 된다.\n" +
                "세 곳을 다 눌러 보자.\n" +
                IntroProbeChecklist(),
            IntroCause =>
                "기저귀는 보송했고, 토닥이니 잠깐 조용했다가 다시 보챈다.\n" +
                "남은 건 입과 손. 이 녀석, 배가 고프다.\n" +
                "이유를 모른 채 달래면 진정 효과가 절반으로 줄어든다.",
            IntroCarrier =>
                "분유는 아기방에 없다. 주방까지 가서 타 와야 한다.\n" +
                "그렇다고 우는 애를 눕혀 두고 가면 울음만 더 커진다.\n" +
                "가슴을 눌러 아기띠를 매자. 안은 채로 두 손이 빈다.",
            IntroToKitchen =>
                "아기띠 덕에 안고 준비해도 체력이 깎이지 않고,\n" +
                "옮기는 동안 울음도 덜 오른다.\n" +
                "아래에서 주방을 눌러 보자.",
            IntroPrepMix =>
                "분유가루를 물에 섞는 게 먼저다. 식힘대야는 아직 소용없다.\n" +
                "순서를 건너뛰면 본편에서도 그대로 거절당한다.\n" +
                "분유통을 눌러 섞어 보자.",
            IntroPrepCool =>
                "갓 탄 분유는 뜨겁다. 식혀서 온도를 확인해야 먹일 수 있다.\n" +
                "식힘대야에 담가 두자.",
            IntroToNursery =>
                "준비 끝. 준비물은 주방에 두고 갈 수 없다.\n" +
                "아기방을 눌러 돌아가자.",
            IntroFeed =>
                "입가를 눌러 준비한 분유를 먹인다.\n" +
                "이번에 깬 이유가 여기서 사라진다.",
            IntroSoothe =>
                "배는 채웠는데 아직 진정도가 낮다.\n" +
                "등을 토닥이거나 쪽쪽이를 물려 마저 재우자.\n" +
                "쪽쪽이는 즉시 진정 +12. 대신 밤에 세 번뿐이다.",
            IntroFellAsleep =>
                "눈꺼풀과 손끝이 아직 움직인다. 여기서 내려놓으면 십중팔구 깬다.\n" +
                "팔다리 힘이 빠졌는지 먼저 살펴보자.",
            IntroLaydownFailed =>
                "얕은 잠에서는 등이 닿는 순간 깬다.\n" +
                "깊은 잠이 될 때까지 기다렸다가 내려놓아야 한다.",
            IntroDeepSleep =>
                "숨이 고르고 팔다리가 축 늘어졌다. 깊은 잠이다.\n" +
                "이제 침대를 눌러 천천히 내려놓자.",
            IntroNoise =>
                "백색소음기는 달래는 물건이 아니다. 켜 둬도 진정도는 안 오른다.\n" +
                "대신 초인종 같은 바깥 소리를 덮어서, 들어간 잠을 이어 준다.\n" +
                "매일 켜면 익숙해져 효과가 줄어든다.",
            IntroMonitor => _introMonitorRead
                ? "이게 베이비 모니터의 전부다. 곁을 떠나 있는 동안 아기 상태를 여는\n" +
                  "유일한 창이고, 한 번 보면 30분 동안 열려 있다.\n" +
                  "안 챙겨 오면 주방·욕실에서는 아무것도 알 수 없다."
                : "젖병을 씻으러 주방에 나왔다. 여기서는 아기가 보이지 않는다.\n" +
                  "기분도 울음도 '확인 불가'다.\n" +
                  "베이비 모니터를 눌러 보자. 아기 곁에서는 켜지지 않는 물건이다.",
            _ =>
                "아닌 것부터 지우고, 순서대로 준비하고,\n" +
                "깊이 잠든 걸 확인한 뒤에 내려놓는 것.\n" +
                "오늘 밤 아홉 시간을 이 리듬으로 건너면 된다."
        };

        private string IntroProbeChecklist()
        {
            string list = $"  {(IntroProbeDone(ProbeMouth) ? "✔" : "□")} 입과 손" +
                          $"   {(IntroProbeDone(ProbeBack) ? "✔" : "□")} 등" +
                          $"   {(IntroProbeDone(ProbeDiaper) ? "✔" : "□")} 기저귀";
            return string.IsNullOrEmpty(_introProbeNote) ? list : list + "\n" + _introProbeNote;
        }

        /// <summary>
        /// 인트로용 신체 히트존. ratio는 아기 rect 대비 (x, y, width, height) 비율이며
        /// 플레이 화면의 DrawLinkedBodyActions와 같은 값을 쓴다.
        /// probe가 0이 아니면 1단계 배제 검사이고, 아니면 nextBeat로 넘어가는 행동이다.
        /// </summary>
        private void DrawIntroBodyProbe(Rect babyRect, Vector4 ratio, string label,
            int probe, int nextBeat = -1)
        {
            bool done = probe != 0 && IntroProbeDone(probe);
            var hotspot = new Rect(
                babyRect.x + babyRect.width * ratio.x,
                babyRect.y + babyRect.height * ratio.y,
                babyRect.width * ratio.z,
                babyRect.height * ratio.w);
            bool hovered = hotspot.Contains(Event.current.mousePosition);
            float pulse = (Mathf.Sin(Time.unscaledTime * 3.1f + probe + nextBeat) + 1f) * .5f;
            Color accent = done
                ? new Color(.55f, .92f, .67f)
                : hovered ? new Color(.45f, .9f, .86f) : new Color(1f, .72f, .3f);

            Color previous = GUI.color;
            GUI.color = new Color(accent.r, accent.g, accent.b,
                done ? .18f : hovered ? .5f : .26f + pulse * .14f);
            GUI.DrawTexture(hotspot, _itemGlow, ScaleMode.StretchToFill, true);
            GUI.color = previous;
            if (hovered && !done) DrawCareSparkles(hotspot.center, .42f, 2);

            float labelWidth = _portrait ? 420f : 340f;
            float labelHeight = _portrait ? 46f : 36f;
            var labelRect = new Rect(hotspot.center.x - labelWidth * .5f,
                hotspot.yMax + 6f, labelWidth, labelHeight);
            // 인트로 라벨은 아기 위에 겹친다. 배경을 불투명하게 깔면 정작 눌러야 할
            // 몸이 가려지므로 옅은 판만 깔고 글자 대비로 읽히게 한다.
            Fill(labelRect, new Color(0.02f, 0.03f, 0.05f, hovered ? .62f : .46f));
            GUI.Label(labelRect, label, OverlayLabelStyle(_portrait ? 21 : 17,
                FontStyle.Bold, done ? accent : new Color(.96f, .95f, .92f),
                TextAnchor.MiddleCenter));

            if (done) return;
            if (!GUI.Button(hotspot, GUIContent.none, GUIStyle.none) &&
                !GUI.Button(labelRect, GUIContent.none, GUIStyle.none)) return;
            _audio?.PlayUi();
            if (probe != 0)
            {
                // 배제 검사에는 오답이 없다. 눌러 본 곳은 결과만 남기고 체크된다.
                _introProbeMask |= probe;
                _introProbeNote = probe switch
                {
                    ProbeDiaper => "기저귀는 보송하다. 이건 아니다.",
                    ProbeBack => "안고 토닥이니 잠깐 조용, 그리고 다시 보챈다. 이것도 아니다.",
                    _ => "입을 오물거리고 주먹을 빤다. 배가 고픈 거였다."
                };
                TriggerImpact(new Color(.45f, .88f, .62f, .4f), 2f, .24f);
                return;
            }
            _introBeat = nextBeat;
            TriggerImpact(nextBeat == IntroLaydownFailed
                    ? new Color(.95f, .36f, .28f, .5f)
                    : new Color(.45f, .88f, .62f, .5f),
                nextBeat == IntroLaydownFailed ? 7f : 2f, .3f);
        }

        private void DrawFamilySetup()
        {
            FillViewport(new Color(0.01f, 0.02f, 0.035f, 0.52f));
            GUI.Label(_portrait
                    ? new Rect(60f, 55f, 960f, 78f)
                    : new Rect(90f, 50f, 1000f, 64f),
                "우리 아기는 누구를 닮았을까?", _display);
            GUI.Label(_portrait
                    ? new Rect(60f, 130f, 960f, 60f)
                    : new Rect(90f, 112f, 1250f, 44f),
                "엄마와 아빠의 눈매·머리결·입·코·피부 톤과 목소리를 골라 주세요",
                _caption);

            if (_portrait)
            {
                DrawParentTraitPanel(new Rect(48f, 190f, 474f, 540f), "아빠",
                    ref _dadMonolid, ref _dadStraightHair, ref _dadBigMouth, ref _dadBigNose,
                    ref _dadDeepSkin, ref _dadHighVoice);
                DrawParentTraitPanel(new Rect(558f, 190f, 474f, 540f), "엄마",
                    ref _momMonolid, ref _momStraightHair, ref _momBigMouth, ref _momBigNose,
                    ref _momDeepSkin, ref _momHighVoice);
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
                    ResetIntro();
                    _flow.BeginIntro();
                }
                if (DrawPrimaryButton(new Rect(90f, 1780f, 900f, 124f), "바로 시작"))
                {
                    if (!_familyRolled) RollFamilyBaby();
                    _flow.SetBabyName(HasBabyName() ? _babyNameInput : "아용이");
                    ResetIntro();
                    _flow.BeginIntro();
                }
            }
            else
            {
                DrawParentTraitPanel(new Rect(90f, 200f, 480f, 470f), "아빠",
                    ref _dadMonolid, ref _dadStraightHair, ref _dadBigMouth, ref _dadBigNose,
                    ref _dadDeepSkin, ref _dadHighVoice);
                DrawParentTraitPanel(new Rect(600f, 200f, 480f, 470f), "엄마",
                    ref _momMonolid, ref _momStraightHair, ref _momBigMouth, ref _momBigNose,
                    ref _momDeepSkin, ref _momHighVoice);
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
                    ResetIntro();
                    _flow.BeginIntro();
                }
                if (DrawPrimaryButton(new Rect(610f, 820f, 470f, 82f), "바로 시작"))
                {
                    if (!_familyRolled) RollFamilyBaby();
                    _flow.SetBabyName(HasBabyName() ? _babyNameInput : "아용이");
                    ResetIntro();
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
            ref bool monolid, ref bool straightHair, ref bool bigMouth, ref bool bigNose,
            ref bool deepSkin, ref bool highVoice)
        {
            DrawGlassPanel(rect, 0.76f);
            GUI.Label(new Rect(rect.x + 28f, rect.y + 22f, rect.width - 56f, 48f),
                title, OverlayLabelStyle(_portrait ? 31 : 27, FontStyle.Bold,
                    new Color(1f, 0.85f, 0.62f)));
            float firstLabelY = rect.y + 72f;
            // 여섯 형질은 라벨과 선택지를 한 행에 둬서 작은 화면에서도 서로 겹치지 않는다.
            float rowStep = (rect.yMax - 16f - firstLabelY) / 6f;
            DrawBinaryTraitRow(firstLabelY, "눈매", "쌍꺼풀", "무쌍", rect, rowStep, ref monolid);
            DrawBinaryTraitRow(firstLabelY + rowStep, "머리결", "곱슬", "직모",
                rect, rowStep, ref straightHair);
            DrawBinaryTraitRow(firstLabelY + rowStep * 2f, "입 모양", "작은 입", "큰 입",
                rect, rowStep, ref bigMouth);
            DrawBinaryTraitRow(firstLabelY + rowStep * 3f, "코 모양", "작은 코", "큰 코",
                rect, rowStep, ref bigNose);
            DrawBinaryTraitRow(firstLabelY + rowStep * 4f, "피부 톤", "밝은 톤", "진한 톤",
                rect, rowStep, ref deepSkin);
            DrawBinaryTraitRow(firstLabelY + rowStep * 5f, "목소리", "낮고 차분", "높고 씩씩",
                rect, rowStep, ref highVoice);
        }

        private void DrawBinaryTraitRow(float labelY, string label, string left, string right,
            Rect panel, float rowStep, ref bool rightSelected)
        {
            float inset = 22f;
            float labelWidth = _portrait ? 76f : 70f;
            float gap = 9f;
            float choiceWidth = (panel.width - inset * 2f - labelWidth - gap * 2f) * .5f;
            float buttonHeight = rowStep - 8f;
            float buttonY = labelY + 2f;
            GUI.Label(new Rect(panel.x + inset, labelY, labelWidth, rowStep), label,
                OverlayLabelStyle(_portrait ? 19 : 17, FontStyle.Bold,
                    new Color(.78f, .82f, .86f), TextAnchor.MiddleLeft));
            float firstX = panel.x + inset + labelWidth + gap;
            if (DrawChoiceButton(new Rect(firstX, buttonY, choiceWidth, buttonHeight),
                    left, !rightSelected))
            {
                rightSelected = false;
                _familyRolled = false;
            }
            if (DrawChoiceButton(new Rect(firstX + choiceWidth + gap, buttonY,
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
            string noseSource = TraitSource(_babyBigNose, _dadBigNose, _momBigNose);
            string skinSource = TraitSource(_babyDeepSkin, _dadDeepSkin, _momDeepSkin);
            string voiceSource = TraitSource(_babyHighVoice, _dadHighVoice, _momHighVoice);
            string eyeSource = TraitSource(_babyMonolid, _dadMonolid, _momMonolid);
            string hairSource = TraitSource(_babyStraightHair, _dadStraightHair, _momStraightHair);
            var resultPanel = new Rect(area.x + 20f, area.y + artSize - 5f,
                area.width - 40f, _portrait ? 155f : 125f);
            DrawGlassPanel(resultPanel, 0.82f);
            GUI.Label(new Rect(resultPanel.x + 24f, resultPanel.y + 12f,
                    resultPanel.width - 48f, _portrait ? 58f : 48f),
                $"{(_babyMonolid ? "무쌍" : "쌍꺼풀")} · {(_babyStraightHair ? "직모" : "곱슬")} · " +
                $"{(_babyBigMouth ? "큰 입" : "작은 입")}\n{(_babyBigNose ? "큰 코" : "작은 코")} · " +
                $"{(_babyDeepSkin ? "진한 톤" : "밝은 톤")} · {(_babyHighVoice ? "높은 목소리" : "낮은 목소리")}",
                OverlayLabelStyle(_portrait ? 21 : 17, FontStyle.Bold,
                    Color.white, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(resultPanel.x + 24f, resultPanel.y + (_portrait ? 70f : 60f),
                    resultPanel.width - 48f, _portrait ? 72f : 56f),
                $"눈매·머리결은 {eyeSource}·{hairSource}, 입·코는 {mouthSource}·{noseSource},\n" +
                $"피부·목소리는 {skinSource}·{voiceSource}를 닮았어요",
                OverlayLabelStyle(_portrait ? 17 : 14, FontStyle.Normal,
                    new Color(0.86f, 0.9f, 0.94f), TextAnchor.MiddleCenter));
        }

        private void RollFamilyBaby()
        {
            _familyRollCount++;
            int seed = _familyRollCount * 1103515245 + 12345;
            _babyMonolid = (seed & 1) == 0 ? _dadMonolid : _momMonolid;
            _babyStraightHair = (seed & 2) == 0 ? _dadStraightHair : _momStraightHair;
            _babyBigMouth = (seed & 4) == 0 ? _dadBigMouth : _momBigMouth;
            _babyHighVoice = (seed & 8) == 0 ? _dadHighVoice : _momHighVoice;
            _babyBigNose = (seed & 16) == 0 ? _dadBigNose : _momBigNose;
            _babyDeepSkin = (seed & 32) == 0 ? _dadDeepSkin : _momDeepSkin;
            _babyVoiceVariant = _babyHighVoice ? 0 : 2;
            _familyRolled = true;
            _audio?.SetBabyVoiceVariant(_babyVoiceVariant);
            _audio?.PlayUi();
        }

        private Texture2D GeneticBabyPortrait()
        {
            if (_babyMonolid)
                return _babyStraightHair ? _geneticMonolidStraight : _geneticMonolidCurly;
            return _babyStraightHair ? _geneticDoubleStraight : _geneticDoubleCurly;
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

        /// <summary>
        /// 코 위치는 입 프로파일에서 유도한다. 프레임마다 좌표를 다시 재면
        /// 입과 코가 어긋날 수 있고, 얼굴이 기운 프레임에서 특히 티가 난다.
        /// 입 중심에서 얼굴 위쪽으로 입 반경에 비례해 올라간 지점을 쓰고,
        /// 그 방향을 입과 같은 각도로 회전시켜 누운 프레임까지 함께 따라가게 한다.
        /// </summary>
        private static Vector2 NoseCenterFromTop(MouthWarpProfile profile)
        {
            float distance = profile.Radius.x * .62f;
            float radians = profile.Angle * Mathf.Deg2Rad;
            return new Vector2(
                profile.CenterFromTop.x - distance * Mathf.Sin(radians),
                profile.CenterFromTop.y - distance * Mathf.Cos(radians));
        }

        // 진한 피부는 곱셈 틴트라 원화의 명암을 유지한 채 톤만 내려간다.
        private static readonly Color DeepSkinTint = new Color(.74f, .58f, .47f);

        private void DrawBabyTexture(Rect rect, Texture2D texture)
        {
            if (texture == null) return;
            texture = ResolveBabyVariantTexture(texture);
            bool hasProfile = _mouthWarpProfiles.TryGetValue(texture.name, out var profile);
            bool warpsFace = (_babyBigMouth || _babyBigNose) && hasProfile;
            // 피부 톤은 얼굴 좌표가 필요 없으므로 프로파일이 없는 프레임에도 적용한다.
            if (_mouthWarpMaterial == null || (!warpsFace && !_babyDeepSkin) ||
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
            bool warpsMouth = warpsFace && _babyBigMouth;
            bool warpsNose = warpsFace && _babyBigNose;
            _mouthWarpMaterial.SetVector("_MouthCenter", warpsMouth
                ? new Vector4(profile.CenterFromTop.x, 1f - profile.CenterFromTop.y, 0f, 0f)
                : Vector4.zero);
            _mouthWarpMaterial.SetVector("_MouthRadius",
                new Vector4(profile.Radius.x, profile.Radius.y, 0f, 0f));
            _mouthWarpMaterial.SetFloat("_MouthAngle", profile.Angle * Mathf.Deg2Rad);
            _mouthWarpMaterial.SetFloat("_MouthStrength", warpsMouth ? .28f : 0f);
            if (warpsNose)
            {
                Vector2 nose = NoseCenterFromTop(profile);
                _mouthWarpMaterial.SetVector("_NoseCenter",
                    new Vector4(nose.x, 1f - nose.y, 0f, 0f));
                float noseRadius = profile.Radius.x * .52f;
                _mouthWarpMaterial.SetVector("_NoseRadius",
                    new Vector4(noseRadius, noseRadius, 0f, 0f));
                _mouthWarpMaterial.SetFloat("_NoseAngle", profile.Angle * Mathf.Deg2Rad);
            }
            _mouthWarpMaterial.SetFloat("_NoseStrength", warpsNose ? .22f : 0f);
            _mouthWarpMaterial.SetColor("_SkinTint", DeepSkinTint);
            _mouthWarpMaterial.SetFloat("_SkinStrength", _babyDeepSkin ? .85f : 0f);
            _mouthWarpMaterial.SetColor("_Color", GUI.color);
            // screenRect는 이미 GUI 행렬이 적용된 좌표다. 그대로 그리면 WebGL에서
            // 반응형 스케일이 두 번 적용되어 큰 입 아기가 화면 밖으로 밀려난다.
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            Graphics.DrawTexture(screenRect, texture, _mouthWarpMaterial);
            GUI.matrix = previousMatrix;
        }

        private Texture2D ResolveBabyVariantTexture(Texture2D baseTexture)
        {
            if (!_familyRolled || baseTexture == null) return baseTexture;

            string variant = BabyArtVariantKey();
            if (string.IsNullOrEmpty(variant)) return baseTexture;

            string relativePath;
            if (BabyStateTextureNames.Contains(baseTexture.name))
                relativePath = baseTexture.name;
            else if (BabyAnimatedTextureNames.Contains(baseTexture.name))
                relativePath = $"Animated/{baseTexture.name}";
            else if (BabyInteractionTextureNames.Contains(baseTexture.name))
                relativePath = $"Interaction/{baseTexture.name}";
            else
                return baseTexture;

            string cacheKey = $"{variant}/{relativePath}";
            if (_babyVariantTextures.TryGetValue(cacheKey, out var cached))
                return cached != null ? cached : baseTexture;

            Texture2D resolved = Resources.Load<Texture2D>($"Art/Baby/Variants/{cacheKey}");
            _babyVariantTextures[cacheKey] = resolved;
            return resolved != null ? resolved : baseTexture;
        }

        private string BabyArtVariantKey()
        {
            if (_babyMonolid)
                return _babyStraightHair ? "monolid_straight" : "monolid_curly";
            return _babyStraightHair ? "double_straight" : null;
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
            => ReferenceEquals(portrait, _geneticDoubleCurly) ||
               ReferenceEquals(portrait, _geneticDoubleStraight) ||
               ReferenceEquals(portrait, _geneticMonolidCurly) ||
               ReferenceEquals(portrait, _geneticMonolidStraight);

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
            FillViewport(new Color(0.01f, 0.02f, 0.035f, 0.34f));
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
                DrawSetupItemDetail(new Rect(130, 752, 1120, 236), focused, false);

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
            float textWidth = rect.width - inset * 2f;
            // 아이템 설명은 "더 빨리 진정시킵니다" 같은 형용사가 아니라 엔진이 실제로
            // 적용하는 수치를 그대로 보여준다. 정체성 한 줄 → 수치 효과 → 비용 → 대가.
            GUI.Label(new Rect(rect.x + inset, rect.y + 10, textWidth, portrait ? 54 : 46),
                card.Selected ? $"{card.Name} · 선택 완료" : card.Name,
                OverlayLabelStyle(portrait ? 42 : 34, FontStyle.Bold, new Color(0.96f, 0.93f, 0.86f)));
            GUI.Label(new Rect(rect.x + inset, rect.y + (portrait ? 64 : 54), textWidth,
                    portrait ? 44 : 34),
                card.Role,
                OverlayLabelStyle(portrait ? 30 : 23, FontStyle.Normal, new Color(0.74f, 0.79f, 0.84f),
                    TextAnchor.MiddleLeft));

            float effectY = rect.y + (portrait ? 112 : 92);
            string[] effects = card.Effects ?? System.Array.Empty<string>();
            // 효과 줄 수는 아이템마다 다르다(1~3줄). 남은 높이에 비용·대가 두 줄을
            // 더 넣을 자리를 남기고 나눠 어떤 아이템에서도 패널 밖으로 새지 않게 한다.
            float available = rect.yMax - effectY - (portrait ? 108f : 82f);
            float effectStep = Mathf.Clamp(available / Mathf.Max(1, effects.Length),
                portrait ? 32f : 25f, portrait ? 44f : 34f);
            for (int i = 0; i < effects.Length; i++)
                GUI.Label(new Rect(rect.x + inset, effectY + i * effectStep, textWidth, effectStep),
                    "▸ " + effects[i],
                    OverlayLabelStyle(portrait ? 32 : 25, FontStyle.Bold, new Color(0.62f, 0.9f, 0.78f),
                        TextAnchor.MiddleLeft));

            float tailY = effectY + effects.Length * effectStep + (portrait ? 6f : 4f);
            if (!string.IsNullOrEmpty(card.Cost))
                GUI.Label(new Rect(rect.x + inset, tailY, textWidth, effectStep),
                    "비용 · " + card.Cost,
                    OverlayLabelStyle(portrait ? 28 : 22, FontStyle.Normal, new Color(0.78f, 0.82f, 0.86f),
                        TextAnchor.MiddleLeft));

            var warning = new GUIStyle(_caption)
            {
                normal = { textColor = new Color(0.94f, 0.76f, 0.52f) }
            };
            warning.alignment = TextAnchor.UpperLeft;
            warning.wordWrap = true;
            warning.clipping = TextClipping.Overflow;
            GUI.Label(new Rect(rect.x + inset, tailY + effectStep, textWidth, portrait ? 72 : 54),
                $"대가 · {card.Side}", warning);
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

        /// <summary>지금 고를 수 없는 행동을 이유와 함께 흐리게 남긴다. 입력은 받지 않는다.</summary>
        private void DrawBlockedActionRow(Rect rect, V2ActionButtonViewModel action)
        {
            DrawGlassPanel(rect, .34f);
            float iconSize = rect.height - 18f;
            var sigil = new Rect(rect.x + 12f, rect.y + (rect.height - iconSize * .72f) * .5f,
                iconSize * .72f, iconSize * .72f);
            Fill(sigil, new Color(.26f, .28f, .31f, .9f));
            GUI.Label(sigil, ActionSigil(action.Action),
                LabelStyle(25, FontStyle.Bold, new Color(.62f, .64f, .66f), TextAnchor.MiddleCenter));
            float textInset = sigil.width + 30f;
            GUI.Label(new Rect(rect.x + textInset, rect.y + 4f, rect.width - textInset - 12f,
                    rect.height * .5f), action.Label,
                LabelStyle(36, FontStyle.Bold, new Color(.64f, .66f, .68f), TextAnchor.MiddleLeft));
            GUI.Label(new Rect(rect.x + textInset, rect.y + rect.height * .52f,
                    rect.width - textInset - 12f, rect.height * .4f),
                action.DisabledReason,
                OverlayLabelStyle(28, FontStyle.Bold, new Color(.88f, .7f, .42f),
                    TextAnchor.MiddleLeft, true));
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
            // 결정 타이머는 화면 어디에 그려지든 매 프레임 흘러야 한다. 예전에는 '방금 한
            // 행동' 패널이 있을 때만 갱신돼, 방을 옮긴 직후에는 멈춘 것처럼 보이면서도
            // 만료되면 주저함이 조용히 발동했다.
            _decisionSecondsShown = vm.CauseResolved ? -1 : UpdateDecisionTimer(vm);
            vm = _flow.BuildV2Play();
            // 결정 타이머가 주저함을 발동시켰을 수도 있으니 갱신된 vm으로 시계를 읽는다.
            UpdateClockRoll(vm);
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

            // 되돌아온 습관 패널이 있는 밤에는 콜아웃이 더 위에서 멈춰야 한다.
            _landscapeCalloutBottom = vm.EchoSources.Count > 0 ? 536f : 674f;
            DrawPlayScene(vm, new Rect(0, 0, LandscapeWidth, LandscapeHeight), false);
            DrawTopBar(vm);
            DrawLandscapeStatusOrnaments(vm);
            DrawEchoSource(vm, new Rect(390, 548, 1140, 126), false);
            DrawSceneFeedback(vm, new Rect(390, 686, 1140, 76), false);
            DrawContinuousCareControl(vm, new Rect(790, 118, 390, 84), false);

            if (_flow.PendingOverlay != null) DrawOverlay(_flow.PendingOverlay);
        }

        /// <summary>
        /// 행동이 시간을 쓰면 그 자리에서 값을 갈아끼우지 않고 목표까지 굴린다.
        /// 되감김(새 밤 시작)은 굴릴 대상이 아니므로 즉시 맞춘다.
        /// </summary>
        private void UpdateClockRoll(V2PlayViewModel vm)
        {
            if (_clockTargetMinutes == vm.ElapsedMinutes) return;
            bool advanced = _clockTargetMinutes >= 0 && vm.ElapsedMinutes > _clockTargetMinutes;
            _clockRollFrom = advanced ? ShownElapsedMinutes() : vm.ElapsedMinutes;
            _clockRollStart = Time.unscaledTime;
            if (advanced)
            {
                _clockDeltaMinutes = vm.ElapsedMinutes - _clockTargetMinutes;
                _clockBadgeUntil = Time.unscaledTime + ClockBadgeSeconds;
            }
            _clockTargetMinutes = vm.ElapsedMinutes;
        }

        private float ShownElapsedMinutes()
        {
            if (_clockTargetMinutes < 0) return 0f;
            float t = Mathf.Clamp01((Time.unscaledTime - _clockRollStart) / ClockRollSeconds);
            return Mathf.Lerp(_clockRollFrom, _clockTargetMinutes, Mathf.SmoothStep(0f, 1f, t));
        }

        /// <summary>목표 시각까지 아직 굴러가지 못한 분. 두 시계를 같은 순간에 묶어 둔다.</summary>
        private int ClockMinutesBehind()
            => _clockTargetMinutes < 0 ? 0 : Mathf.Max(0, _clockTargetMinutes - Mathf.FloorToInt(ShownElapsedMinutes()));

        /// <summary>
        /// 벽시계 문자열의 원본은 프리젠터다(시작 시각도 거기 있다). 뷰는 그 값을
        /// 아직 도달하지 못한 분만큼 되돌려 그리기만 한다.
        /// </summary>
        private static string ShiftClockBack(string clock, int minutesBack)
        {
            if (minutesBack <= 0 || string.IsNullOrEmpty(clock)) return clock;
            int colon = clock.IndexOf(':');
            if (colon <= 0 ||
                !int.TryParse(clock.Substring(0, colon), out int hour) ||
                !int.TryParse(clock.Substring(colon + 1), out int minute)) return clock;
            int total = ((hour * 60 + minute - minutesBack) % 1440 + 1440) % 1440;
            return $"{total / 60:00}:{total % 60:00}";
        }

        /// <summary>
        /// 남은 시간 옆에 방금 쓴 분을 붙인다. 플레이어가 예산으로 읽는 쪽은 올라가는
        /// 벽시계가 아니라 줄어드는 "새벽까지"라 부호는 마이너스다.
        /// </summary>
        private string RemainingClockText(int remainingMinutes)
            => Time.unscaledTime < _clockBadgeUntil && _clockDeltaMinutes > 0
                ? $"새벽까지 {FormatDuration(remainingMinutes)}   -{_clockDeltaMinutes}분"
                : $"새벽까지 {FormatDuration(remainingMinutes)}";

        private Color RemainingClockColor()
            => Time.unscaledTime < _clockBadgeUntil && _clockDeltaMinutes > 0
                ? new Color(0.98f, 0.73f, 0.36f) : new Color(0.94f, 0.9f, 0.82f);

        private void DrawTopBar(V2PlayViewModel vm)
        {
            int behind = ClockMinutesBehind();
            int remaining = vm.RemainingMinutes + behind;
            GUI.Label(new Rect(48, 26, 250, 76), ShiftClockBack(vm.Clock, behind),
                OverlayLabelStyle(46, FontStyle.Bold, new Color(0.98f, 0.91f, 0.76f)));
            Fill(new Rect(48, 96, 170, 3), new Color(0.96f, 0.67f, 0.28f, 0.84f));
            GUI.Label(new Rect(650, 28, 620, 58), $"{_flow.BabyName} · {vm.NightRoleTitle}",
                OverlayLabelStyle(24, FontStyle.Bold, new Color(.94f, .88f, .76f),
                    TextAnchor.MiddleCenter));
            GUI.Label(new Rect(1448, 27, 420, 68), RemainingClockText(remaining),
                OverlayLabelStyle(24, FontStyle.Bold, RemainingClockColor(),
                    TextAnchor.MiddleRight));
            DrawProgress(new Rect(1606, 91, 262, 4),
                1f - remaining / 540f, new Color(0.94f, 0.67f, 0.3f));
            DrawDecisionTimer(new Rect(1348, 104, 520, 38), false);
        }

        /// <summary>
        /// 실제로 줄어드는 유일한 실시간 타이머를 항상 보이는 자리에 그린다. "새벽까지 N분"은
        /// 행동이 시간을 쓸 때만 줄어드는 게임 내 시계라, 둘을 구분해 주지 않으면
        /// 플레이어는 무엇이 줄고 있는지 알 수 없다.
        /// </summary>
        private void DrawDecisionTimer(Rect rect, bool portrait)
        {
            if (_decisionSecondsShown < 0) return;
            bool expired = _decisionSecondsShown == 0;
            bool urgent = _decisionSecondsShown <= 6;
            GUI.Label(rect, expired
                    ? "결정 시간 초과 · 주저하는 사이 울음이 커졌어요"
                    : $"결정까지 {_decisionSecondsShown}초",
                OverlayLabelStyle(portrait ? 24 : 19, FontStyle.Bold,
                    expired ? new Color(.96f, .45f, .38f)
                    : urgent ? new Color(1f, .7f, .34f)
                    : new Color(.82f, .85f, .88f),
                    TextAnchor.MiddleRight));
        }

        private void DrawPortraitPlay(V2PlayViewModel vm)
        {
            // The observation sheet is modal. IMGUI registers scene controls
            // before it is drawn, so disable those controls while the sheet is
            // open or a tap on the sheet can also move rooms/use an item below.
            bool previousEnabled = GUI.enabled;
            if (_observationSheetOpen) GUI.enabled = false;
            DrawPlayScene(vm, new Rect(0, 0, PortraitWidth, PortraitPrimaryActionY), true);
            int behind = ClockMinutesBehind();
            int remaining = vm.RemainingMinutes + behind;
            GUI.Label(new Rect(54, 65, 250, 88), ShiftClockBack(vm.Clock, behind),
                OverlayLabelStyle(51, FontStyle.Bold, new Color(0.98f, 0.91f, 0.76f)));
            Fill(new Rect(54, 148, 172, 4), new Color(0.96f, 0.67f, 0.28f, 0.84f));
            GUI.Label(new Rect(610, 72, 414, 72), RemainingClockText(remaining),
                OverlayLabelStyle(29, FontStyle.Bold, RemainingClockColor(),
                    TextAnchor.MiddleRight));
            GUI.Label(new Rect(300, 76, 360, 58), vm.NightRoleTitle,
                OverlayLabelStyle(23, FontStyle.Bold, new Color(.94f, .88f, .76f),
                    TextAnchor.MiddleCenter));
            DrawProgress(new Rect(735, 145, 289, 5), 1f - remaining / 540f,
                new Color(0.94f, 0.67f, 0.3f));
            DrawDecisionTimer(new Rect(300, 18, 724, 46), true);
            DrawPortraitItemDock(vm);
            DrawPortraitStatusOrnaments(vm);
            if (vm.EchoSources.Count > 0)
            {
                DrawEchoSource(vm, new Rect(58, PortraitContextY, 964, 120), true);
                DrawSceneFeedback(vm, new Rect(58, PortraitContextY + 134f, 964, 154), true);
            }
            else
                DrawSceneFeedback(vm, new Rect(58, PortraitContextY, 964, 154), true);
            DrawContinuousCareControl(vm, PortraitPrimaryActionRect(), true);
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
            FillViewport(new Color(0, 0, 0, .62f));
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
            if (!vm.BabyStateVisible)
            {
                // 아기 곁을 떠나 있으면 이 시트도 아기 상태를 말해서는 안 된다.
                GUI.Label(new Rect(104f, 830f, 872f, 44f), "아기 상태 · 확인 불가",
                    OverlayLabelStyle(34, FontStyle.Bold, new Color(.94f, .76f, .52f)));
                GUI.Label(new Rect(104f, 876f, 872f, 82f), vm.BabyStateBlockedReason,
                    OverlayLabelStyle(29, FontStyle.Normal, new Color(.9f, .92f, .94f),
                        TextAnchor.MiddleLeft, true));
            }
            else
            {
                GUI.Label(new Rect(104f, 828f, 872f, 42f),
                    vm.BabyStateViaMonitor ? BabyStateHeadline(vm) + "  · 📟" : BabyStateHeadline(vm),
                    OverlayLabelStyle(34, FontStyle.Bold, new Color(1f, .87f, .66f)));
                GUI.Label(new Rect(104f, 872f, 872f, 36f), vm.CurrentSignal,
                    OverlayLabelStyle(27, FontStyle.Normal, new Color(.9f, .92f, .94f),
                        TextAnchor.MiddleLeft));
                // 밤 돌봄의 판단 근거는 절대 수치보다 "얼마나 지났는지"다.
                // 졸림은 경과 시간이라 채울 최대치가 없어 글자로 두고, 0~100 척도인
                // 배고픔·울음만 단계 이름 옆에 막대를 붙인다. 행동 목록을 아래로
                // 밀지 않도록 막대는 같은 줄 안에 넣는다.
                var sheetTone = vm.FatigueStage == FatigueSignalStage.Overtired ||
                    vm.HungerStage == HungerSignalStage.Late
                        ? new Color(.96f, .72f, .5f) : new Color(.74f, .82f, .88f);
                GUI.Label(new Rect(104f, 908f, 330f, 30f), FatigueSheetLine(vm),
                    OverlayLabelStyle(23, FontStyle.Bold, sheetTone, TextAnchor.MiddleLeft));
                GUI.Label(new Rect(448f, 908f, 160f, 30f), $"배고픔 {vm.HungerLabel}",
                    OverlayLabelStyle(23, FontStyle.Bold, HungerRowColor(vm), TextAnchor.MiddleLeft));
                DrawInlineGauge(new Rect(612f, 920f, 84f, 6f), vm.Hunger, HungerRowColor(vm),
                    vm.HungerActiveThreshold, vm.HungerLateThreshold);
                GUI.Label(new Rect(712f, 908f, 140f, 30f),
                    $"울음 {PresentationCopyMapper.CryStageLabel(vm.CryIntensity)}",
                    OverlayLabelStyle(23, FontStyle.Bold, CryRowColor(vm), TextAnchor.MiddleLeft));
                DrawInlineGauge(new Rect(856f, 920f, 84f, 6f), vm.CryIntensity, CryRowColor(vm),
                    vm.CryWarningThreshold, 0);
                GUI.Label(new Rect(104f, 938f, 872f, 30f),
                    $"마지막 수유 {FormatDuration(vm.MinutesSinceFeed)} 전" +
                    $"    마지막 기저귀 {FormatDuration(vm.MinutesSinceDiaperChange)} 전",
                    OverlayLabelStyle(23, FontStyle.Normal, new Color(.74f, .82f, .88f),
                        TextAnchor.MiddleLeft));
            }

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

            // 고를 수 없는 항목을 목록에서 지우면 "분유 먹이기가 없어졌다"로 읽히고
            // 어디로 가야 하는지도 사라진다. 아래쪽에 이유와 함께 흐리게 남긴다.
            var actions = ActionsFor(_actionGroup, exhausted);
            var ready = new List<V2ActionButtonViewModel>();
            var blocked = new List<V2ActionButtonViewModel>();
            for (int i = 0; i < actions.Length; i++)
            {
                var action = vm.Actions.Find(candidate => candidate.Action == actions[i]);
                if (action == null) continue;
                (action.Enabled ? ready : blocked).Add(action);
            }
            int visible = 0;
            for (int i = 0; i < ready.Count; i++)
            {
                var rect = new Rect(72f + visible % 2 * 476f, 1150f + visible / 2 * 146f, 448f, 126f);
                if (DrawActionButton(rect, ready[i], vm, true))
                {
                    _observationSheetOpen = false;
                    PerformV2Action(ready[i].Action);
                    return;
                }
                visible++;
            }
            for (int i = 0; i < blocked.Count; i++)
            {
                var rect = new Rect(72f + visible % 2 * 476f, 1150f + visible / 2 * 146f, 448f, 126f);
                if (rect.yMax > PortraitHeight - 40f) break;
                DrawBlockedActionRow(rect, blocked[i]);
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
            // 콜아웃은 방 안 물건(백색소음기·온습도계 등)보다 나중에 그려야 한다.
            // 먼저 그리면 물건 아트와 패널이 그 위를 덮어 글자를 읽을 수 없다.
            _deferredCallout = null;
            DrawViewportRoomFocusBackdrop(vm.CaregiverLocation);

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
            if (_deferredCallout.HasValue)
                DrawBodyActionCallout(_deferredCallout.Value, portrait);
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
                // 원화 높이는 행동마다 달라서 가로에서도 자막이 장면 피드백·수면 선택
                // 패널까지 내려간다. 세로와 같은 하단 클램프를 가로에도 건다.
                captionY = portrait
                    ? Mathf.Min(captionY, PortraitSceneContentBottom - 58f)
                    : Mathf.Min(captionY, _landscapeCalloutBottom - 46f);
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
                mouth = new Rect(babyRect.x + babyRect.width * .105f,
                    babyRect.y + 382f, babyRect.width * .17f, 72f);
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
                // 표정과 눈을 가리지 않도록 입 주변만 감싸는 작은 접점으로 제한한다.
                mouth = new Rect(babyRect.x + babyRect.width * .40f,
                    babyRect.y + babyRect.height * .36f,
                    babyRect.width * .20f, babyRect.height * .13f);
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

            // 배고픔 신호를 이미 살펴본 각성에서는 같은 관찰을 다시 걸어 두지 않는다.
            // 그대로 두면 입가 링크가 영원히 "살피기"에 묶여 다음 행동이 사라진다.
            V2ActionId mouthAction = vm.FeedingReady
                ? V2ActionId.FeedPreparedBottle
                : !vm.CauseResolved && !vm.HungerChecked
                    ? V2ActionId.CheckHungerSignals
                    : V2ActionId.Pacifier;
            V2ActionId diaperAction = vm.DiaperChangedPendingDisposal
                ? V2ActionId.DisposeDiaper
                : vm.DiaperWetConfirmed
                    ? V2ActionId.ChangeDiaper
                    : V2ActionId.CheckDiaper;

            V2ActionId chestAction = vm.CarrierOn ? V2ActionId.ToggleCarrier : V2ActionId.Hold;
            V2ActionId? recommendedAction = RecommendedBodyAction(vm, mouthAction, diaperAction);
            var diaperButton = DirectAction(vm, diaperAction);
            string diaperCost = diaperButton != null && !string.IsNullOrEmpty(diaperButton.CostLabel)
                ? " · " + diaperButton.CostLabel : string.Empty;

            var links = new List<BodyActionLink>(6)
            {
                new BodyActionLink(mouth, mouthAction,
                    mouthAction == V2ActionId.FeedPreparedBottle ? "입가 · 준비한 분유 수유" :
                    mouthAction == V2ActionId.Pacifier ? "입가 · 쪽쪽이 건네기" :
                    "입과 손 · 배고픔 신호 살피기",
                    mouthAction == V2ActionId.FeedPreparedBottle ? "준비한 분유를 먹여보세요" :
                    mouthAction == V2ActionId.Pacifier ? "쪽쪽이를 건네보세요" :
                    "입과 손을 살펴보세요"),
                new BodyActionLink(back, V2ActionId.Pat, "등 · 같은 리듬으로 토닥이기",
                    "등을 토닥여 보세요"),
                // 이미 품에 안고 있는데 "품에 안기"라고 쓰면 같은 행동이 두 번 필요한
                // 것처럼 읽힌다. 안고 있을 때의 이 자리는 달래기다.
                new BodyActionLink(chest, chestAction,
                    vm.CarrierOn ? "가슴 · 아기띠 풀어주기"
                        : vm.BabyHeld ? "가슴 · 품에 안고 달래기" : "가슴 · 목을 받쳐 품에 안기",
                    vm.CarrierOn ? "아기띠를 풀어주세요"
                        : vm.BabyHeld ? "품에 안고 달래보세요" : "목을 받쳐 안아보세요"),
                new BodyActionLink(diaper, diaperAction,
                    diaperAction == V2ActionId.DisposeDiaper ? "기저귀 · 싸서 버리기" + diaperCost :
                    diaperAction == V2ActionId.ChangeDiaper
                        ? $"기저귀 · {(vm.DiaperStoolConfirmed ? "대변 처리" : "소변 기저귀 갈기")}{diaperCost}"
                        : "기저귀 · 상태 확인" + diaperCost,
                    diaperAction == V2ActionId.DisposeDiaper ? "사용한 기저귀를 버려주세요" :
                    diaperAction == V2ActionId.ChangeDiaper ? "기저귀를 갈아주세요" :
                    "기저귀를 확인해 보세요"),
                new BodyActionLink(limbs, V2ActionId.CheckLimbRelaxation,
                    "팔다리 · 힘이 풀렸는지 살피기", "팔다리의 힘을 살펴보세요")
            };
            if (IsSleeping(vm) && !portrait)
                links.Add(new BodyActionLink(mattress, V2ActionId.Laydown,
                    "침대 · 천천히 내려놓기", "천천히 내려놓아 보세요"));
            // 챙긴 물건도 같은 목록에 올린다. 방 안에서 눈으로 찾아야만 쓸 수 있으면
            // 아이템을 골라 온 의미가 플레이 중에 사라진다.
            links.Add(new BodyActionLink(NoiseObjectRect(portrait), V2ActionId.ToggleNoise,
                vm.NoiseOn ? "백색소음기 · 끄기" : "백색소음기 · 켜기",
                vm.NoiseOn ? "백색소음을 꺼보세요" : "백색소음을 켜보세요"));

            DrawLinkedBodyActions(vm, links, recommendedAction, pulse, portrait);
        }

        private static Rect NoiseObjectRect(bool portrait)
            => portrait ? new Rect(40, 620, 175, 165) : new Rect(75, 610, 160, 155);

        /// <summary>
        /// 모니터는 아기방 밖에서만 켜진다. 이전 좌표 (255,625)는 아기방 밖에서 세워지는
        /// 행동 목록 패널(40,596~380,842) 아래에 그대로 깔려, 물건을 눌러도 패널의 다른
        /// 버튼이 먼저 먹었다. 아기방 전용인 온습도계 자리(우하단)는 모니터가 뜨는
        /// 주방·욕실에서 항상 비어 있고, 안고 있는 아기(x 1420~1880, y 86~546)와도 겹치지 않는다.
        /// </summary>
        private static Rect MonitorObjectRect() => new Rect(1660, 790, 150, 150);

        private static V2ActionId? RecommendedBodyAction(V2PlayViewModel vm,
            V2ActionId mouthAction, V2ActionId diaperAction)
        {
            // 상단 신호 리본과 같은 상태 우선순위를 사용한다.
            // 방 이동·기다리기처럼 신체 접점이 아닌 행동은 억지로 몸 위에 추천하지 않는다.
            if (vm.HandsNeedWashing) return null;
            if (vm.DiaperChangedPendingDisposal || vm.DiaperStoolConfirmed ||
                vm.DiaperWetConfirmed)
                return diaperAction;
            if (!vm.CauseResolved)
            {
                if (vm.FeedingReady && vm.RevealedCause == WakeCause.Hunger)
                    return V2ActionId.FeedPreparedBottle;
                if (vm.DiaperRecommendationVisible) return diaperAction;
                if (!vm.HungerChecked) return mouthAction;
                // 살펴볼 신호를 다 본 뒤의 다음 손길은 달래기다. 수유가 필요한 밤이면
                // 주방 준비를 안내하는 패널이 따로 다음 단계를 가리킨다.
                return vm.BabyHeld ? V2ActionId.Pat : V2ActionId.Hold;
            }
            if (vm.SleepStage == V2SleepStage.RemActiveSleep) return null;
            if (vm.SleepStage == V2SleepStage.NremDeepSleep && !vm.DeepSleepObserved)
                return V2ActionId.CheckLimbRelaxation;
            if (vm.SleepStage == V2SleepStage.NremDeepSleep)
                return vm.BabyHeld ? V2ActionId.Laydown : null;
            return vm.BabyHeld ? V2ActionId.Pat : V2ActionId.Hold;
        }

        private void DrawLinkedBodyActions(V2PlayViewModel vm, List<BodyActionLink> links,
            V2ActionId? recommendedAction, float pulse, bool portrait)
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
                int recommendedIndex = active.FindIndex(link =>
                    recommendedAction.HasValue && link.Action == recommendedAction.Value);
                if (recommendedIndex >= 0 && Time.unscaledTime >= _directCueHiddenUntil)
                {
                    BodyActionLink recommendedLink = active[recommendedIndex];
                    Color old = GUI.color;
                    GUI.color = new Color(1f, .74f, .34f,
                        RecommendedGlowBaseAlpha + pulse * RecommendedGlowPulseAlpha);
                    GUI.DrawTexture(recommendedLink.Hotspot, _itemGlow, ScaleMode.StretchToFill, true);
                    GUI.color = old;
                    _deferredCallout = recommendedLink;
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
            // 주방·욕실은 화면 가운데를 준비 소품이 채운다. 아기 반대편 열에
            // 패널을 세우면 눌러야 할 소품을 그대로 덮으므로 왼쪽 아래로 내린다.
            Rect panel = vm.CaregiverLocation != HomeLocation.Nursery
                ? new Rect(40f, 596f, 340f, 54f + active.Count * 48f)
                : new Rect(babyOnRight ? 1040f : 1510f, 365f, 360f,
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
                bool recommended = recommendedAction.HasValue &&
                    link.Action == recommendedAction.Value &&
                    Time.unscaledTime >= _directCueHiddenUntil;
                Color accent = bodyHovered
                    ? new Color(.45f, .9f, .86f)
                    : new Color(1f, .72f, .3f);

                float glowAlpha = bodyHovered || labelHovered
                    ? .5f
                    : recommended
                        ? RecommendedGlowBaseAlpha + pulse * RecommendedGlowPulseAlpha
                        : 0f;
                if (glowAlpha > 0f)
                {
                    Color previousColor = GUI.color;
                    GUI.color = new Color(accent.r, accent.g, accent.b, glowAlpha);
                    GUI.DrawTexture(link.Hotspot, _itemGlow, ScaleMode.StretchToFill, true);
                    GUI.color = previousColor;
                }

                if (bodyHovered || labelHovered)
                {
                    DrawCareSparkles(link.Hotspot.center, .42f, 2);
                }

                DrawGlassPanel(labelRect, bodyHovered || labelHovered ? .9f : .54f);
                Fill(new Rect(labelRect.x, labelRect.y + 6f, bodyHovered ? 6f : 4f,
                    labelRect.height - 12f), accent);
                string prefix = recommended ? "★ " : string.Empty;
                GUI.Label(new Rect(labelRect.x + 14f, labelRect.y,
                        labelRect.width - 24f, labelRect.height), prefix + link.Label,
                    OverlayLabelStyle(portrait ? 17 : 14, FontStyle.Bold,
                        bodyHovered || labelHovered ? accent : recommended
                            ? new Color(1f, .84f, .58f)
                            : new Color(.91f, .91f, .89f),
                        TextAnchor.MiddleLeft));

                if (recommended) _deferredCallout = link;

                bool clickedBody = GUI.Button(link.Hotspot, GUIContent.none, GUIStyle.none);
                bool clickedLabel = GUI.Button(labelRect, GUIContent.none, GUIStyle.none);
                if (!clickedBody && !clickedLabel) continue;
                _directHintSeen = true;
                PerformV2Action(link.Action);
                return;
            }
        }

        private void DrawBodyActionCallout(BodyActionLink link, bool portrait)
        {
            float width = portrait ? 360f : 300f;
            float height = portrait ? 58f : 44f;
            float canvasWidth = portrait ? PortraitWidth : LandscapeWidth;
            bool placeRight = link.Hotspot.center.x < canvasWidth * .5f;
            float x = placeRight
                ? link.Hotspot.xMax + (portrait ? 18f : 14f)
                : link.Hotspot.x - width - (portrait ? 18f : 14f);
            // 아기 몸 위로 파고들지 않도록 히트존 바깥으로 한 번 더 밀어낸다.
            x = placeRight
                ? Mathf.Max(x, link.Hotspot.xMax + (portrait ? 18f : 14f))
                : Mathf.Min(x, link.Hotspot.x - width - (portrait ? 18f : 14f));
            x = Mathf.Clamp(x, portrait ? 32f : 24f, canvasWidth - width - (portrait ? 32f : 24f));
            float minY = portrait ? PortraitSceneContentY + 8f : 80f;
            // 가로 화면 하단은 되돌아온 습관(y 548)·장면 피드백(y 686)·수면 선택(y 785)
            // 패널이 차지한다. 콜아웃을 화면 끝까지 내리면 그 글자들과 겹쳐 읽을 수 없다.
            float maxY = portrait
                ? PortraitPrimaryActionY - height - 8f
                : _landscapeCalloutBottom - height;
            float y = Mathf.Clamp(link.Hotspot.center.y - height * .5f, minY, maxY);
            var callout = new Rect(x, y, width, height);
            // 추천 문구는 읽히기만 하면 된다. 예전처럼 90% 불투명 패널에 사면 테두리를
            // 두르면 아기 그림을 그대로 덮어 정작 눌러야 할 대상이 보이지 않는다.
            // 배경은 옅게, 강조는 아기 쪽을 가리키는 세로 막대 하나로만 한다.
            Fill(callout, new Color(0.02f, 0.03f, 0.05f, 0.5f));
            Fill(new Rect(placeRight ? callout.x : callout.xMax - 4f,
                callout.y + 6f, 4f, callout.height - 12f), new Color(1f, .72f, .3f, .9f));
            GUI.Label(new Rect(callout.x + 14f, callout.y, callout.width - 28f, callout.height),
                link.Prompt,
                OverlayLabelStyle(portrait ? 21 : 16, FontStyle.Bold,
                    new Color(1f, .92f, .76f), TextAnchor.MiddleCenter));
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

        private V2ActionButtonViewModel DirectAction(V2PlayViewModel vm, V2ActionId id)
            => vm.Actions.Find(action => action.Action == id && action.Enabled);

        private void DrawDirectRoomObjects(V2PlayViewModel vm, bool portrait, bool babyVisible)
        {
            if (_flow.InputLocked || RoomTransitionActive()) return;
            // 아기방을 비운 동안 아기를 살피는 물건이므로, 아기 히트존이 없는
            // 주방·욕실에서도 독립된 진입점이 있어야 한다.
            // 세로 화면은 주방·욕실 소품과 안고 있는 아기가 장면을 꽉 채워 모니터를
            // 놓을 빈 자리가 없다. 세로에서는 항상 열리는 관찰 시트의 보조 행동 목록
            // (DrawUtilityActions)이 같은 진입점을 제공하므로 소품을 그리지 않는다.
            if (!portrait && vm.CaregiverLocation != HomeLocation.Nursery)
                DrawRoomObject(vm, V2ActionId.CheckMonitor, MonitorObjectRect(),
                    "베이비 모니터로 아기 살피기", ItemId.Monitor, false);
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
                        portrait ? new Rect(28, 790, 250, 148) : new Rect(1640, 785, 210, 128),
                        portrait);
                    DrawSceneActionHotspot(vm,
                        portrait ? new Rect(28, 340, 190, 260) : new Rect(420, 170, 160, 250),
                        V2ActionId.AdjustTemperature, "창문을 조절해 방 온도 맞추기", portrait);
                    DrawSceneActionHotspot(vm,
                        portrait ? new Rect(858, 650, 170, 190) : new Rect(1300, 350, 160, 180),
                        V2ActionId.AdjustHumidity, "가습기를 조절해 습도 맞추기", portrait);
                    // 백색소음기는 세로에서도 방 안에 둔다. 가로에만 두면 모바일에서는
                    // 챙긴 아이템을 쓸 방법이 아예 없어진다.
                    DrawRoomObject(vm, V2ActionId.ToggleNoise, NoiseObjectRect(portrait),
                        vm.NoiseOn ? "백색소음 끄기" : "백색소음기", ItemId.Noise, portrait);
                    if (!portrait)
                    {
                        if (vm.HasPacifier)
                            DrawPacifierProp(vm, new Rect(1480, 790, 78, 78), false);
                        if (!vm.CarrierOn)
                            DrawRoomObject(vm, V2ActionId.ToggleCarrier,
                                new Rect(1280, 515, 105, 175), "아기띠", ItemId.Carrier, false);
                    }
                    // 이득 문구가 카드 아래로 붙으므로 세로에서는 시작점을 올려 둔다.
                    // 그대로 두면 패널 하단이 y=650의 가습기 핫스팟을 덮어 클릭을 가로챈다.
                    DrawGrandmaCall(vm,
                        portrait ? new Rect(768, 310, 282, 176) : new Rect(1452, 150, 300, 180),
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

            // 런당 한 번뿐인 카드다. 무엇이 얼마나 바뀌는지 눌러 보기 전에 알아야 한다.
            // 예전에는 이 설명을 마우스 호버에만 띄웠는데, WebGL 터치 환경에는 호버가
            // 없어 이득이 아예 보이지 않았다. 그래서 카드에 붙박이로 적는다.
            string benefit = GrandmaBenefitText(action.CostLabel);
            float lineHeight = portrait ? 30f : 24f;
            float benefitHeight = string.IsNullOrEmpty(benefit)
                ? 0f
                : lineHeight * (CountLines(benefit) + 0.4f);
            var panel = new Rect(rect.x, rect.y, rect.width, rect.height + benefitHeight);

            DrawGlassPanel(panel, .86f, true);
            Fill(new Rect(panel.x, panel.y, portrait ? 7f : 5f, panel.height),
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
            if (benefitHeight > 0f)
            {
                var strip = new Rect(panel.x + (portrait ? 9f : 7f), rect.yMax,
                    panel.width - (portrait ? 21f : 17f), benefitHeight);
                Fill(strip, new Color(1f, .72f, .3f, .12f));
                GUI.Label(new Rect(strip.x + 10f, strip.y + lineHeight * .2f,
                        strip.width - 20f, strip.height - lineHeight * .3f),
                    benefit,
                    OverlayLabelStyle(portrait ? 21 : 17, FontStyle.Bold,
                        new Color(1f, .92f, .76f), TextAnchor.UpperCenter, true));
            }
            if (GUI.Button(panel, GUIContent.none, GUIStyle.none))
            {
                PerformV2Action(V2ActionId.Grandma);
                _audio.PlayUi();
            }
        }

        /// <summary>
        /// 행동 비용 라벨(원본은 프레젠터)을 카드 폭에 맞게 두 항목씩 접는다.
        /// 문구를 여기서 다시 쓰지 않아야 판정과 표시가 어긋나지 않는다.
        /// </summary>
        private static string GrandmaBenefitText(string costLabel)
        {
            if (string.IsNullOrEmpty(costLabel)) return null;
            var parts = costLabel.Split(new[] { " · " }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i += 2)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(parts[i].Trim());
                if (i + 1 < parts.Length) sb.Append(" · ").Append(parts[i + 1].Trim());
            }
            return sb.ToString();
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int lines = 1;
            foreach (char c in text) if (c == '\n') lines++;
            return lines;
        }

        private void DrawBathroomGuidance(V2PlayViewModel vm, bool portrait)
        {
            bool babyTogether = vm.BabyLocation == HomeLocation.Bathroom;
            // 가로에서 x 38~324 · y 220~494는 수면/체력/집중력 상태 오너먼트가 쓴다.
            // 이 패널을 거기에 겹쳐 두면 두 벌의 글자가 그대로 포개져 읽을 수 없다.
            Rect panel = portrait
                ? new Rect(58f, 340f, 448f, 286f)
                : new Rect(390f, 222f, 430f, 252f);
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
            // 가로에서는 세 소품의 캡션이 모두 yMax 근처에 붙는다. 장면 피드백
            // 패널(y 686)과 겹치지 않도록 소품을 30px 올려 캡션이 686 위에서
            // 끝나게 한다.
            Rect powder = splitPortrait ? new Rect(66, 530, 210, 260)
                : portrait ? new Rect(90, 520, 250, 305) : new Rect(480, 365, 255, 310);
            Rect bottle = splitPortrait ? new Rect(290, 520, 180, 275)
                : portrait ? new Rect(405, 500, 225, 330) : new Rect(825, 345, 220, 330);
            Rect cooling = splitPortrait ? new Rect(118, 690, 250, 195)
                : portrait ? new Rect(705, 600, 260, 225) : new Rect(1110, 440, 285, 235);

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
            // 소품 캡션 세 개가 나란히 놓이는 띠 위에 요약 문장을 겹쳐 두면
            // 넷이 한 줄에서 뒤엉킨다. 가로에서는 소품 위쪽으로 올린다.
            float stateY = portrait ? 878f : 288f;
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
                checkedBoth ? "확인 완료 · 온습도계" : "미확인 · 온습도계",
                OverlayLabelStyle(portrait ? 23 : 18, FontStyle.Bold,
                    new Color(.94f, .91f, .84f), TextAnchor.MiddleLeft));
            var valuesRect = new Rect(rect.x + 18f, rect.y + (portrait ? 48f : 40f),
                rect.width - 36f, portrait ? 58f : 48f);
            float halfWidth = valuesRect.width * .5f;
            DrawEnvironmentMeterValue(new Rect(valuesRect.x, valuesRect.y, halfWidth, valuesRect.height),
                $"{vm.TemperatureCelsius:0.#}°", vm.TemperatureChecked,
                vm.TemperatureCelsius >= vm.RecommendedTemperatureMin &&
                vm.TemperatureCelsius <= vm.RecommendedTemperatureMax, portrait);
            DrawEnvironmentMeterValue(new Rect(valuesRect.x + halfWidth, valuesRect.y,
                    halfWidth, valuesRect.height),
                $"{vm.HumidityPercent:0.#}%", vm.HumidityChecked,
                vm.HumidityPercent >= vm.RecommendedHumidityMin &&
                vm.HumidityPercent <= vm.RecommendedHumidityMax, portrait);
            GUI.Label(new Rect(rect.x + 18f, rect.yMax - (portrait ? 37f : 31f), rect.width - 36f,
                    portrait ? 31f : 26f), checkedBoth ? "권장 범위와 비교했어요" : "눈으로 확인하기",
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

        private void DrawEnvironmentMeterValue(Rect rect, string value, bool observed,
            bool inRecommendedRange, bool portrait)
        {
            Color valueColor = !observed
                ? new Color(.86f, .9f, .94f, .28f)
                : inRecommendedRange
                    ? new Color(.62f, .94f, .72f)
                    : new Color(1f, .72f, .38f);
            GUIStyle style = OverlayLabelStyle(portrait ? 34 : 27, FontStyle.Bold,
                valueColor, TextAnchor.MiddleCenter);
            if (!observed)
            {
                // 값은 이미 방에 존재하지만 아직 눈으로 읽지 않았다는 표현이다.
                // 낮은 알파의 한 픽셀 잔상을 더해 고장 표시가 아닌 흐릿한 판독으로 보이게 한다.
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), value,
                    OverlayLabelStyle(portrait ? 34 : 27, FontStyle.Bold,
                        new Color(.86f, .9f, .94f, .10f), TextAnchor.MiddleCenter));
            }
            GUI.Label(rect, value, style);
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
            // 방으로 분기하면 안 된다. 주방에서 그려지는 방 물건은 베이비 모니터뿐인데
            // 이 분기가 그걸 젖병 소품으로 바꿔 그려 "모니터처럼 안 생긴" 물건이 됐다.
            // 젖병 소품은 수유 준비 히트존(DrawSceneActionHotspot)이 따로 그린다.
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
                BabyStateHeadline(vm),
                OverlayLabelStyle(portrait ? 34 : 20, FontStyle.Bold,
                    new Color(0.98f, 0.87f, 0.68f)));
            GUI.Label(new Rect(x + 24, y + (portrait ? 50 : 39), width - 48, portrait ? 96 : 42),
                vm.CurrentSignal,
                OverlayLabelStyle(portrait ? 32 : 20, FontStyle.Normal,
                    new Color(0.92f, 0.93f, 0.93f), TextAnchor.MiddleLeft, true));
        }

        /// <summary>다음 목적지 칸에 쓰는 맥동 색. 현재 위치의 고정 주황과 구분된다.</summary>
        private static Color NeedsRoomPulse()
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * 3.4f) + 1f) * .5f;
            return new Color(1f, .74f, .34f, .45f + pulse * .5f);
        }

        private void DrawHomeJourneyMap(V2PlayViewModel vm, bool portrait)
        {
            // 가로에서는 폭 330 안에 "이동" 라벨과 방 세 칸을 밀어 넣어 칸당 78px밖에
            // 남지 않았고, 그 안에 "아기방 · 나와 아기"를 12pt 한 줄로 그려 글자가
            // 뭉갰다. 라벨을 빼고 폭을 넓혀 방 이름과 누가 있는지를 두 줄로 나눈다.
            Rect map = portrait
                ? new Rect(58f, PortraitRoomMapY, 964f, PortraitRoomMapHeight)
                : new Rect(1540f, 682f, 356f, 74f);
            DrawGlassPanel(map, 0.7f);
            if (portrait)
                GUI.Label(new Rect(map.x + 22f, map.y + 7f, map.width - 44f, 34f), "방 이동",
                    OverlayLabelStyle(23, FontStyle.Bold, new Color(.96f, .78f, .5f),
                        TextAnchor.MiddleLeft));

            HomeLocation[] rooms =
                { HomeLocation.Nursery, HomeLocation.Kitchen, HomeLocation.Bathroom };
            float startX = map.x + (portrait ? 14f : 10f);
            float gap = portrait ? 12f : 8f;
            float roomWidth = (map.xMax - startX - (portrait ? 12f : 10f) - gap * 2f) / 3f;
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
                // 수유가 필요한데 젖병이 준비되지 않았으면 주방 칸이 다음 목적지다.
                // 별도 안내 패널을 띄우는 대신 이미 자리가 잡힌 이 칸을 쓴다.
                bool needsThisRoom = vm.NeedsKitchenForFeed &&
                    room == HomeLocation.Kitchen && !current;
                DrawGlassPanel(roomRect, current ? .92f : needsThisRoom ? .82f : .5f,
                    current || needsThisRoom);
                if (current || needsThisRoom)
                    Fill(new Rect(roomRect.x, roomRect.y, 4f, roomRect.height),
                        current ? new Color(.96f, .68f, .3f) : NeedsRoomPulse());
                // 칸 폭이 가로에서 107px밖에 안 된다. 문구가 길면 그대로 뭉친다.
                string occupants = needsThisRoom ? "수유 준비" :
                    current && babyHere ? "나와 아기" :
                    current ? "나" : babyHere ? "아기" : "비어 있음";
                if (portrait)
                    GUI.Label(roomRect, HomeLocationLabel(room) + " · " + occupants,
                        OverlayLabelStyle(25, FontStyle.Bold,
                            current ? Color.white
                            : needsThisRoom ? new Color(1f, .84f, .55f)
                            : new Color(.78f, .81f, .83f),
                            TextAnchor.MiddleCenter));
                else
                {
                    GUI.Label(new Rect(roomRect.x, roomRect.y + 6f, roomRect.width, 26f),
                        HomeLocationLabel(room),
                        OverlayLabelStyle(17, FontStyle.Bold,
                            current ? Color.white : new Color(.82f, .85f, .87f),
                            TextAnchor.MiddleCenter));
                    GUI.Label(new Rect(roomRect.x, roomRect.y + 30f, roomRect.width, 22f),
                        occupants,
                        OverlayLabelStyle(13, needsThisRoom ? FontStyle.Bold : FontStyle.Normal,
                            current ? new Color(1f, .84f, .58f)
                            : needsThisRoom ? new Color(1f, .82f, .5f)
                            : new Color(.6f, .63f, .66f),
                            TextAnchor.MiddleCenter));
                }
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
            DrawMoodOrnament(new Rect(38, 412, 286, 82), vm, false);
            // 배고픔·울음이 게이지 행(50px)이 되면서 236으로는 마지막 기저귀 행이 잘린다.
            // 아래 준비물 패널이 y=780에서 시작하므로 770까지만 늘린다.
            DrawBabyStackPanel(vm, new Rect(38, 508, 286, 262), false);
            DrawPreparedItems(new Rect(44, 780, 270, 58), true, vm);
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
            DrawMoodOrnament(new Rect(558, y + 112, 476, 96), vm, true);
            DrawCaregiverBreathHotspot(vm, new Rect(46, y + 112, 476, 96), true);
        }

        /// <summary>
        /// 아기 상태 스택. 밤 돌봄의 판단은 진정도 같은 절대 수치가 아니라
        /// "마지막으로 먹인 지 얼마나 됐나 / 얼마나 깨어 있었나"로 이루어진다.
        /// 아기를 볼 수 없는 동안에는 이 값들도 함께 닫힌다.
        /// </summary>
        private void DrawBabyStackPanel(V2PlayViewModel vm, Rect rect, bool portrait)
        {
            DrawGlassPanel(rect, .62f);
            float inset = portrait ? 22f : 16f;
            GUI.Label(new Rect(rect.x + inset, rect.y + 8f, rect.width - inset * 2f, portrait ? 34f : 26f),
                "아기 상태",
                OverlayLabelStyle(portrait ? 24 : 17, FontStyle.Bold,
                    new Color(.74f, .79f, .84f)));

            if (!vm.BabyStateVisible)
            {
                GUI.Label(new Rect(rect.x + inset, rect.y + (portrait ? 52f : 42f),
                        rect.width - inset * 2f, rect.height - (portrait ? 64f : 52f)),
                    vm.BabyStateBlockedReason,
                    OverlayLabelStyle(portrait ? 22 : 16, FontStyle.Bold,
                        new Color(.94f, .76f, .52f), TextAnchor.UpperLeft, true));
                return;
            }

            // 졸림·마지막 수유·기저귀는 "얼마나 지났나"라서 채울 최대치가 없다.
            // 게이지는 0~100 척도인 배고픔·울음에만 붙인다.
            float rowHeight = portrait ? 46f : 36f;
            float gaugeRowHeight = portrait ? 62f : 50f;
            float y = rect.y + (portrait ? 50f : 40f);
            DrawStackRow(rect, y, inset, portrait, FatigueRowLabel(vm),
                FatigueRowValue(vm),
                vm.FatigueStage == FatigueSignalStage.Overtired
                    ? new Color(.94f, .39f, .34f)
                    : vm.FatigueStage >= FatigueSignalStage.Active
                        ? new Color(.95f, .79f, .39f) : new Color(.62f, .9f, .78f));
            y += rowHeight;
            DrawStackGaugeRow(rect, y, inset, portrait, "배고픔", vm.HungerLabel,
                vm.Hunger, HungerRowColor(vm),
                vm.HungerActiveThreshold, vm.HungerLateThreshold);
            y += gaugeRowHeight;
            DrawStackGaugeRow(rect, y, inset, portrait, "울음",
                PresentationCopyMapper.CryStageLabel(vm.CryIntensity),
                vm.CryIntensity, CryRowColor(vm), vm.CryWarningThreshold, 0);
            y += gaugeRowHeight;
            DrawStackRow(rect, y, inset, portrait, "마지막 수유",
                $"{FormatDuration(vm.MinutesSinceFeed)} 전", new Color(.78f, .82f, .86f));
            DrawStackRow(rect, y + rowHeight, inset, portrait, "마지막 기저귀",
                $"{FormatDuration(vm.MinutesSinceDiaperChange)} 전", new Color(.78f, .82f, .86f));
        }

        private static Color HungerRowColor(V2PlayViewModel vm)
            => vm.HungerStage == HungerSignalStage.Late
                ? new Color(.94f, .39f, .34f)
                : vm.HungerStage == HungerSignalStage.Active
                    ? new Color(.95f, .79f, .39f) : new Color(.62f, .9f, .78f);

        private static Color CryRowColor(V2PlayViewModel vm)
            => vm.CryIntensity > vm.CryWarningThreshold
                ? new Color(.94f, .39f, .34f)
                : vm.CryIntensity > 0 ? new Color(.95f, .79f, .39f) : new Color(.62f, .9f, .78f);

        /// <summary>왼쪽 항목명, 오른쪽 값. 한 줄짜리 상태 행.</summary>
        private void DrawStackRow(Rect panel, float y, float inset, bool portrait,
            string label, string value, Color valueColor)
        {
            float width = panel.width - inset * 2f;
            float height = portrait ? 40f : 32f;
            GUI.Label(new Rect(panel.x + inset, y, width * .44f, height), label,
                OverlayLabelStyle(portrait ? 22 : 16, FontStyle.Normal,
                    new Color(.68f, .72f, .76f), TextAnchor.MiddleLeft));
            GUI.Label(new Rect(panel.x + inset + width * .44f, y, width * .56f, height), value,
                OverlayLabelStyle(portrait ? 24 : 17, FontStyle.Bold, valueColor,
                    TextAnchor.MiddleRight));
        }

        /// <summary>
        /// 0~100 척도 상태 행. 생 숫자는 "42가 큰 값인가"를 답해 주지 못하므로
        /// 단계 이름 + 막대로 바꾸고, 판정이 쓰는 경계를 눈금으로 찍는다.
        /// 눈금이 있어야 "울면 이미 늦었다"가 수치를 외우지 않고도 읽힌다.
        /// </summary>
        private void DrawStackGaugeRow(Rect panel, float y, float inset, bool portrait,
            string label, string stageLabel, double value, Color color,
            double warnThreshold, double lateThreshold)
        {
            DrawStackRow(panel, y, inset, portrait, label, stageLabel, color);
            float width = panel.width - inset * 2f;
            float barHeight = portrait ? 8f : 6f;
            DrawInlineGauge(new Rect(panel.x + inset, y + (portrait ? 42f : 34f), width, barHeight),
                value, color, warnThreshold, lateThreshold);
        }

        /// <summary>
        /// 0~100 막대 하나. 경계 눈금은 판정이 쓰는 값을 그대로 받아 찍는다.
        /// 0이나 100을 넘기면 눈금을 생략한다(해당 단계가 없는 지표).
        /// </summary>
        private static void DrawInlineGauge(Rect bar, double value, Color color,
            double warnThreshold, double lateThreshold)
        {
            DrawProgress(bar, Mathf.Clamp01((float)value / 100f), color);
            DrawGaugeTick(bar, warnThreshold, new Color(.95f, .79f, .39f, .85f));
            DrawGaugeTick(bar, lateThreshold, new Color(.94f, .39f, .34f, .9f));
        }

        private static void DrawGaugeTick(Rect bar, double threshold, Color color)
        {
            if (threshold <= 0 || threshold >= 100) return;
            float x = bar.x + bar.width * (float)(threshold / 100.0);
            Fill(new Rect(x - 1f, bar.y - 2f, 2f, bar.height + 4f), color);
        }

        private void DrawCaregiverBreathHotspot(V2PlayViewModel vm, Rect rect, bool portrait)
        {
            var action = DirectAction(vm, V2ActionId.CatchBreath);
            if (action == null) return;
            bool urgent = vm.ParentStamina <= 20;
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

        private void DrawViewportRoomFocusBackdrop(HomeLocation location)
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            var viewport = new Rect(0f, 0f, Screen.width, Screen.height);
            DrawRoomFocusBackdrop(location, viewport);
            Fill(viewport, RoomFocusTint(location));
            GUI.matrix = previousMatrix;
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

            // 큐 히트존은 아기 발치(가로 기준 y 669~727)에 있어 "아기가 깼다" 오버레이의
            // 확인 버튼(760,650,400,66)과 겹친다. IMGUI는 먼저 등록된 컨트롤이 클릭을
            // 가져가므로, 가드가 없으면 오버레이 버튼이 영원히 눌리지 않는다.
            if (_flow.InputLocked || RoomTransitionActive() || _animatedAction.HasValue) return;

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
            FillViewport(new Color(0, 0, 0, 0.72f));
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
            // DrawPlay가 프레임마다 한 번 갱신해 둔 값을 그대로 쓴다.
            int remaining = _decisionSecondsShown >= 0
                ? _decisionSecondsShown : UpdateDecisionTimer(vm);
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
                // 수면 진입 자체가 원인 해소를 요구하므로, 원인을 못 찾은 채로는
                // 아무리 토닥여도 아기가 잠들지 않는다. 그대로 두면 체력 30까지
                // 밤을 통째로 태우는 무한 루프가 되니 몇 번 뒤 진단으로 돌려보낸다.
                if (!vm.CauseResolved && _continuousPatRepeats >= UnresolvedPatRepeatLimit)
                {
                    StopContinuousCare("달래도 잠들지 않아요 · 먼저 원인을 찾아보세요");
                    return;
                }
                if (DirectAction(vm, V2ActionId.Pat) == null)
                {
                    StopContinuousCare("지금은 토닥일 수 없어 멈췄습니다");
                    return;
                }
                if (!vm.CauseResolved) _continuousPatRepeats++;
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

        private void DrawContinuousCareControl(V2PlayViewModel vm, Rect rect, bool portrait)
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
            // 원인을 못 찾은 채 토닥이면 Core가 진정 폭을 절반으로 준다.
            // 왜 잘 안 진정되는지 화면에서 바로 읽히게 배지로 알린다.
            bool halfEffect = _continuousCare == ContinuousCareMode.Pat && !vm.CauseResolved;
            var labelRect = halfEffect
                ? new Rect(rect.x, rect.y, rect.width, rect.height * .56f)
                : rect;
            GUI.Label(labelRect, label,
                OverlayLabelStyle(portrait ? 34 : 20, FontStyle.Bold,
                    new Color(1f, .88f, .68f), TextAnchor.MiddleCenter));
            if (halfEffect)
            {
                var badge = new Rect(rect.x + 10f, rect.y + rect.height * .54f,
                    rect.width - 20f, rect.height * .36f);
                DrawGlassPanel(badge, .5f);
                GUI.Label(badge, "원인을 아직 못 찾았어요  ·  진정 효과 절반",
                    OverlayLabelStyle(portrait ? 24 : 14, FontStyle.Bold,
                        new Color(1f, .74f, .38f), TextAnchor.MiddleCenter));
            }
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                StopContinuousCare("직접 멈췄습니다");
        }

        private void StopContinuousCare(string notice = null)
        {
            _continuousCare = ContinuousCareMode.None;
            _continuousPatRepeats = 0;
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
                // 원인을 못 찾았어도 "일단 달래보기"는 실제 육아 행동이므로
                // 연속 토닥이기를 막지 않는다. 대신 Core가 진정 폭을 절반으로
                // 낮추고 화면에는 그 사실을 배지로 알린다.
                if (!automatic && action == V2ActionId.Pat && !outcome.WasMisdiagnosis)
                {
                    _continuousCare = ContinuousCareMode.Pat;
                    _continuousPatRepeats = 0;
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
                return "삼키는 게 급하지 않다. 배가 고픈 밤은 아니었나 보다.";
            if (outcome.WasMisdiagnosis)
                return "이것 말고, 먼저 봐줘야 할 게 남아 있다.";
            // 확인 계열은 "무엇을 확인했고 결론이 무엇인지"를 헤드라인에 담는다.
            // 묘사만 남기면 아래 상세 줄을 읽기 전까지 무슨 검사였는지 알 수 없다.
            if (outcome.Action == V2ActionId.CheckHungerSignals)
                return !outcome.HungerSignalsMatchCause ||
                       outcome.HungerSignalStage == HungerSignalStage.None
                    ? "배고픔 확인 · 지금은 아니다"
                    : outcome.HungerSignalStage == HungerSignalStage.Late
                        ? "배고픔 확인 · 지금 당장 먹여야 한다"
                        : outcome.HungerSignalStage == HungerSignalStage.Active
                            ? "배고픔 확인 · 배가 고픈 게 맞다"
                            : "배고픔 확인 · 슬슬 배가 고파진다";
            if (outcome.Action == V2ActionId.CheckDiaper)
                return outcome.DiaperCheckResult switch
                {
                    DiaperCheckResult.Stool => "기저귀 확인 · 대변이다",
                    DiaperCheckResult.Wet => "기저귀 확인 · 젖었다",
                    _ => "기저귀 확인 · 보송하다"
                };
            if (outcome.EventIds.Contains(GameEventId.LaydownSucceeded))
                return "손을 뗐는데도 숨이 그대로 이어진다.";
            if (outcome.EventIds.Contains(GameEventId.LaydownFailed) ||
                outcome.EventIds.Contains(GameEventId.BabyFullyWoke))
                return "등이 닿자 몸이 움찔했다.";
            return outcome.Action switch
            {
                V2ActionId.Hold => "품에 들자 어깨의 힘이 조금 풀린다.",
                V2ActionId.Pat => "토닥이는 박자에 숨이 천천히 맞춰진다.",
                V2ActionId.CatchBreath => "나도 한 번, 길게 숨을 내쉰다.",
                V2ActionId.Grandma => "손이 바뀌자 몸이 그 품으로 기대 온다.",
                V2ActionId.ChangeDiaper => "깨끗한 기저귀로 갈아 줬다.",
                V2ActionId.DisposeDiaper => "쓴 기저귀를 싸서 버렸다.",
                V2ActionId.WashHands => "비누로 손을 씻었다.",
                V2ActionId.CheckEnvironment => "방 안 공기를 살폈다.",
                V2ActionId.PrepareWater => "젖병에 따뜻한 물을 받았다.",
                V2ActionId.CoolBottle => "손목에 한 방울 떨어뜨려 봤다.",
                V2ActionId.FeedPreparedBottle => "삼키는 박자를 기다려 준다.",
                _ => "작은 움직임으로 대답이 돌아온다."
            };
        }

        private static string ActionFeedbackHeading(V2ActionOutcome outcome)
        {
            if (outcome.Accepted) return ActionFeedbackTitle(outcome);
            if (outcome.BlockReason != V2ActionBlockReason.None) return "그 전에 해줘야 할 게 있다.";
            if (outcome.EventIds.Contains(GameEventId.LaydownFailed) ||
                outcome.EventIds.Contains(GameEventId.BabyFullyWoke))
                return "‘조금만 더 기다려줘.’ 몸으로 그렇게 말했다.";
            return "지금 이 아이가 원한 건 이게 아니었다.";
        }

        private void DrawOverlay(OverlayViewModel overlay)
        {
            FillViewport(new Color(0, 0, 0, 0.62f));
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
            EnsureNarrativeRequested(vm);
            if (_portrait) { DrawPortraitDiary(vm); return; }
            FillViewport(new Color(0.015f, 0.035f, 0.065f, 0.84f));
            GUI.Label(new Rect(110, 76, 1100, 58), $"{_flow.BabyName} · {vm.NightLabel} 밤의 기록", _display);
            if (vm.NarrativeFromAi)
                GUI.Label(new Rect(1230, 92, 580, 34), "AI 육아일지 · 판정에는 관여하지 않음", _caption);
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
            FillViewport(new Color(0.01f, 0.02f, 0.035f, 0.3f));
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
                if (DrawPrimaryButton(new Rect(90, 1420, 900, 132), "숨죽이고 내려놓기"))
                {
                    _titleDropAttempted = true;
                    _audio?.PlayAction(new V2ActionOutcome { Action = V2ActionId.Laydown, Accepted = true });
                    TriggerImpact(new Color(.95f, .32f, .24f, .62f), 10f, .38f);
                }
                return;
            }

            GUI.Label(new Rect(70, 120, 940, 120), "NOT A NAP", new GUIStyle(_title) { fontSize = 92 });
            GUI.Label(new Rect(70, 245, 940, 84), "등이 닿으면 또 눈이 번쩍", Centered(_display));
            if (_introBabyArt != null)
                DrawBabyTexture(new Rect(190, 315, 700, 700), _introBabyArt);
            GUI.Label(new Rect(90, 935, 900, 80), "그래도 다시 안아 올리는 밤",
                OverlayLabelStyle(46, FontStyle.Bold, new Color(1f, .68f, .42f),
                    TextAnchor.MiddleCenter));
            DrawGlassPanel(new Rect(58, 1040, 964, 310), .86f);
            GUI.Label(new Rect(104, 1070, 872, 64), "오늘 밤 할 일",
                OverlayLabelStyle(46, FontStyle.Bold, Color.white));
            GUI.Label(new Rect(104, 1150, 872, 145),
                "아침 6시까지, 같이 버틴다.\n깊은 잠 · 내 체력 · 맨손 눕히기 — 셋 중 둘만 지키면 된다.",
                OverlayLabelStyle(40, FontStyle.Normal, new Color(.9f, .92f, .94f),
                    TextAnchor.UpperLeft, true));
            if (DrawPrimaryButton(new Rect(90, 1420, 900, 132), "오늘 밤도 버텨보기  →"))
            {
                ResetIntro();
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
            FillViewport(new Color(0.01f, 0.02f, 0.035f, 0.36f));
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
            FillViewport(new Color(0.015f, 0.035f, 0.065f, 0.9f));
            GUI.Label(new Rect(60, 70, 960, 80), $"{_flow.BabyName} · {vm.NightLabel} 밤의 기록", _display);
            if (vm.NarrativeFromAi)
                GUI.Label(new Rect(60, 152, 960, 44), "AI 육아일지 · 판정에는 관여하지 않음", _caption);
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

        /// <summary>
        /// 밤 종료 화면에 들어온 뒤 서술 요청을 정확히 한 번 보낸다.
        /// 프록시 URL이 없거나 호출이 실패하면 이미 그려져 있는 규칙 기반 폴백 서술이 그대로 남는다.
        /// 실패해도 재시도하지 않는다 — 밤당 1회 호출 원칙을 코드로 지킨다.
        /// </summary>
        private void EnsureNarrativeRequested(V2DiaryViewModel vm)
        {
            if (!NarrativeProxySettings.Enabled) return;
            if (!_narrativeGate.TryBegin(vm.NightId)) return;
            string payload = _flow.Session.BuildNarrativePayload();
            if (string.IsNullOrEmpty(payload)) return;
            var session = _flow.Session;
            var night = vm.NightId;
            StartCoroutine(NarrativeProxyClient.Request(payload, response =>
            {
                if (response != null) session.ApplyNarrative(night, response);
            }));
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
            FillViewport(new Color(0.01f, 0.025f, 0.05f, 0.88f));
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
                _narrativeGate = new NarrativeCallGate();
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
                return "실패가 아니다. 눈꺼풀·숨·팔다리 힘이 더 편안해진 뒤에 다시 해보자.";
            if (outcome.Action == V2ActionId.Pacifier && !outcome.Accepted &&
                outcome.BlockReason == V2ActionBlockReason.None)
                return "쪽쪽이를 밀어냈다. 억지로 물리지 말고 입·손·몸이 향한 쪽을 다시 보자.";
            if (outcome.BlockReason == V2ActionBlockReason.BabyNotHeld)
                return "이미 침대에 있다. 먼저 품에 안아 올려야 한다.";
            if (outcome.BlockReason == V2ActionBlockReason.BabyNotAsleep)
                return "아직 잠들지 않았다. 조금 더 달래 주자.";
            if (outcome.BlockReason == V2ActionBlockReason.ItemUnavailable)
                return "오늘 밤 안 챙겨 온 물건이다.";
            if (outcome.BlockReason == V2ActionBlockReason.CarrierAlreadyWorn)
                return "아기띠를 먼저 벗어야 맨손으로 안을 수 있다.";
            if (outcome.BlockReason == V2ActionBlockReason.WrongLocation)
                return "이건 여기서 할 수 없다. 집 지도를 보고 방을 옮기자.";
            if (outcome.BlockReason == V2ActionBlockReason.HandsDirty)
                return "대변 기저귀를 만진 손이다. 먹이기 전에 욕실에서 비누로 씻자.";
            if (outcome.BlockReason == V2ActionBlockReason.ToolRequired)
            {
                if (outcome.Action == V2ActionId.ChangeDiaper)
                    return "기저귀 상태부터 확인해야 한다.";
                if (outcome.Action == V2ActionId.DisposeDiaper)
                    return "젖은 기저귀를 새것으로 갈아 준 다음이다.";
                if (outcome.Action == V2ActionId.WashHands)
                    return "지금은 손을 씻지 않아도 된다.";
                return "욕실에서 탕온계부터 챙겨 와야 한다.";
            }
            if (outcome.BlockReason == V2ActionBlockReason.CaregiverExhausted)
                return "내 체력이 바닥났다. 여기서 무리하면 둘 다 힘들어진다. 먼저 숨을 고르자.";
            if (outcome.BlockReason == V2ActionBlockReason.ActionLimitReached)
                return "숨은 충분히 골랐다. 이제 확인한 신호에 맞는 손으로 돌아갈 차례다.";
            if (outcome.ActivityLocation == "주방")
                return $"주방에서 준비하는 동안 {outcome.TimeDeltaMinutes}분이 흘렀다.";
            if (outcome.HeadSupported)
                return "목과 머리를 받치자 몸이 품 안으로 기대 온다.";
            if (outcome.Action == V2ActionId.CatchBreath)
                return outcome.ObservedSignals.Count > 0
                    ? $"숨을 고르자 안 보이던 게 보인다. {PresentationCopyMapper.ObservationSignal(outcome.ObservedSignals[0])}"
                    : "길게 한 번 내쉬고, 다음 움직임을 기다린다.";
            if (outcome.Action == V2ActionId.CheckDiaper &&
                outcome.DiaperCheckResult == DiaperCheckResult.Stool)
                return "대변이다. 처리에 시간이 더 들고, 그동안 울음도 조금 커진다.";
            if (outcome.Action == V2ActionId.CheckDiaper &&
                outcome.DiaperCheckResult == DiaperCheckResult.Wet)
                return "젖었다. 갈아 주면 된다.";
            if (outcome.Action == V2ActionId.CheckDiaper &&
                outcome.DiaperCheckResult == DiaperCheckResult.Clean)
                return "보송하다. 75분 동안은 다시 안 봐도 된다.";
            if (outcome.Action == V2ActionId.ChangeDiaper)
                return "새 기저귀를 채웠다. 쓴 기저귀는 싸서 버리자.";
            if (outcome.Action == V2ActionId.DisposeDiaper)
                return outcome.DiaperCheckResult == DiaperCheckResult.Stool
                    ? "기저귀는 끝. 먹이기 전에 욕실에서 손부터 씻자."
                    : "싸서 버렸다. 불편했던 건 이걸로 해결됐다.";
            if (outcome.Action == V2ActionId.WashHands)
                return "손 씻기 끝. 이제 아기에게 돌아가도 된다.";
            if (outcome.Action == V2ActionId.CheckHungerSignals)
            {
                switch (outcome.HungerSignalStage)
                {
                    case HungerSignalStage.Late: return "입을 찾고 숨이 가빠지며 운다. 지금 먹여야 한다.";
                    case HungerSignalStage.Active: return "손이 닿는 쪽으로 고개를 돌리고 입을 벌린다. 배가 고픈 게 맞다.";
                    case HungerSignalStage.Early: return "입맛을 다시고 손을 빤다. 아직 초기 신호다.";
                    default: return "지금은 배고픈 기색이 없다.";
                }
            }
            // 안기·토닥임·달래는 물건은 원인을 해소하진 않지만 울음을 실제로 낮춘다.
            // 그 사실을 말해주지 않으면 플레이어에게는 아무 일도 없었던 것으로 보인다.
            if (outcome.CryRelief > 0)
                return outcome.Action == V2ActionId.ToggleNoise
                    ? "소리가 방을 채우자 울음이 한풀 꺾였다."
                    : vm.RevealedCause == WakeCause.Hunger
                        ? "울음은 잦아들었다. 그래도 배고픔은 먹여야만 해결된다."
                        : "울음은 잦아들었다. 깬 이유는 아직 그대로다.";
            if (outcome.ObservedSignals.Count > 0)
                return PresentationCopyMapper.ObservationSignal(outcome.ObservedSignals[0]);
            if (outcome.Action == V2ActionId.CheckEnvironment)
                return $"온도 {vm.TemperatureCelsius:0.#}°C (권장 20~22) · 습도 {vm.HumidityPercent:0.#}% (권장 40~60)";
            if (outcome.Action == V2ActionId.CheckBodyTemperature)
                return $"아기 체온 {vm.BabyTemperatureCelsius:0.0}°C";
            if (outcome.Action == V2ActionId.AdjustTemperature)
                return $"창을 조절해 온도를 {vm.TemperatureCelsius:0.#}°C로 맞췄다.";
            if (outcome.Action == V2ActionId.AdjustHumidity)
                return $"가습기를 만져 습도를 {vm.HumidityPercent:0.#}%로 맞췄다.";
            if (outcome.Action == V2ActionId.SterilizeBottle)
                return "젖병 소독 완료. 이제 평소 순서대로 준비하면 된다.";
            // 다른 관찰과 마찬가지로 몸의 묘사로 돌려준다. 내부 수치를 그대로 노출하면
            // 세계 안의 물건이 아니라 디버그 창처럼 읽힌다.
            if (outcome.MonitorRead)
                return vm.SleepStage == V2SleepStage.NremDeepSleep
                    ? "화면 속에서 미동도 없다. 고른 숨소리만 넘어온다."
                    : vm.SleepStage == V2SleepStage.RemActiveSleep
                        ? "손끝이 이따금 움찔한다. 아직 얕은 잠이다."
                        : vm.CryIntensity > 55
                            ? "스피커 너머로 울음이 점점 커진다. 서둘러야 한다."
                            : vm.CryIntensity > 20
                                ? "뒤척이는 소리가 들린다. 곧 울 것 같다."
                                : "조용히 누워 있다. 아직은 괜찮다.";
            if (outcome.Action == V2ActionId.ToggleNoise)
                return vm.NoiseOn ? "백색소음기를 켰다. 쏴아—" : "백색소음기를 껐다.";
            if (outcome.ObservedSignals.Count > 0)
                return "관찰 · " + PresentationCopyMapper.ObservationLabel(outcome.ObservedSignals[0]);
            if (outcome.MissingPreparationSteps.Count > 0)
                return "먼저 필요함 · " + PresentationCopyMapper.FeedingStepLabel(outcome.MissingPreparationSteps[0]);
            if (outcome.ConsumedTime)
                return $"{outcome.TimeDeltaMinutes}분이 흘렀고, 내 체력이 {outcome.StaminaDelta:+0;-0;0} 변했다.";
            return fallback;
        }

        private static string CauseSignal(V2PlayViewModel vm)
            => vm.RevealedCause.HasValue ? $"확인된 원인\n{PresentationCopyMapper.WakeCauseLabel(vm.RevealedCause.Value)}" : "왜 깼는지 아직 모른다.\n작은 신호부터 하나씩 짚어 보자.";

        private static string SleepSignal(V2PlayViewModel vm)
        {
            if (!vm.CauseResolved) return CauseSignal(vm);
            if (vm.SleepStage == V2SleepStage.RemActiveSleep)
                return "활동 수면이다.\n내려놓기엔 이르다. 조금 더 기다리자.";
            if (vm.SleepStage == V2SleepStage.NremDeepSleep && !vm.DeepSleepObserved)
                return "깊은 수면이다.\n팔다리가 늘어졌는지 확인해 보자.";
            if (vm.SleepStage == V2SleepStage.NremDeepSleep)
                return vm.BabyHeld
                    ? "팔다리 힘이 빠졌다.\n지금이라면 내려놓을 수 있다."
                    : "침대에서 깊이 잠들었다.\n건드리지 말고 그대로 두자.";
            if (vm.CryIntensity > 45) return "울음이 커지고 있다.\n자극을 줄이고 천천히 움직이자.";
            if (vm.Calm < vm.DrowsyCalmThreshold)
                return $"진정도 {vm.Calm:0} / {vm.SleepStartCalmThreshold:0}\n안거나 토닥여서 가라앉히자.";
            return $"진정도 {vm.Calm:0} / {vm.SleepStartCalmThreshold:0}\n한 번만 더 차분히 달래면 된다.";
        }

        private static string BabyStepHint(V2PlayViewModel vm)
        {
            if (!vm.CauseResolved) return "깬 이유부터 찾을 것";
            if (vm.SleepStage == V2SleepStage.RemActiveSleep) return "활동 수면 · 내려놓기엔 이르다";
            if (vm.SleepStage == V2SleepStage.NremDeepSleep && !vm.DeepSleepObserved) return "깊은 수면 · 팔다리가 늘어졌는지 확인";
            if (vm.SleepStage == V2SleepStage.NremDeepSleep)
                return vm.BabyHeld ? "깊은 수면 확인 · 지금이면 내려놔도 된다" : "침대에서 깊이 잠듦 · 그대로 두자";
            if (vm.Calm < vm.DrowsyCalmThreshold)
                return $"진정도 {vm.Calm:0} / {vm.SleepStartCalmThreshold:0} · 안거나 토닥이기";
            return $"진정도 {vm.Calm:0} / {vm.SleepStartCalmThreshold:0} · 한 번만 더";
        }

        /// <summary>
        /// 신호 리본은 아기 상태만 말한다. 무엇을 할지는 행동 목록의 ★ 추천이,
        /// 방금 무슨 일이 있었는지는 하단 피드백이 맡는다. 같은 지시를 네 곳에서
        /// 반복하면 관찰 게임이 지시 수행 게임으로 읽힌다.
        /// </summary>
        private static string BabyStateHeadline(V2PlayViewModel vm)
        {
            switch (vm.SleepStage)
            {
                case V2SleepStage.Drowsy:
                    return "눈이 반쯤 감기고 움직임이 잦아든다";
                case V2SleepStage.RemActiveSleep:
                    return "눈꺼풀이 떨리고 손끝이 가끔 움직인다";
                case V2SleepStage.NremDeepSleep:
                    return vm.DeepSleepObserved
                        ? "팔다리가 축 늘어지고 깊이 잠들었다"
                        : "숨이 고르고 몸에서 힘이 빠진다";
                default:
                    if (vm.CryIntensity > 35) return "얼굴이 새빨개지고 울음이 터졌다";
                    if (vm.CryIntensity > 0) return "어딘가 불편한 듯 몸을 꼼지락거린다";
                    return "울지도 않고 아빠를 빤히 쳐다본다";
            }
        }

        private static bool IsAsleepStage(V2SleepStage stage)
            => stage == V2SleepStage.RemActiveSleep || stage == V2SleepStage.NremDeepSleep;

        /// <summary>
        /// 졸림 행은 "깨어 있던 시간"으로 계산되므로 아기가 잠들면 0분·None으로
        /// 돌아가고, 그대로 그리면 잠든 아기 옆에 "말똥말똥"이 뜬다. 자는 동안은
        /// 피로 대신 수면 단계와 이어진 시간을 말한다.
        /// </summary>
        private static string FatigueRowLabel(V2PlayViewModel vm)
            => IsAsleepStage(vm.SleepStage) ? "수면" : "졸림";

        private static string FatigueRowValue(V2PlayViewModel vm)
            => IsAsleepStage(vm.SleepStage)
                ? $"{PresentationCopyMapper.V2StageLabel(vm.SleepStage)} · {FormatDuration(vm.CurrentSleepStretchMinutes)}"
                : $"{vm.FatigueLabel} · {FormatDuration(vm.AwakeMinutes)}";

        private static string FatigueSheetLine(V2PlayViewModel vm)
            => IsAsleepStage(vm.SleepStage)
                ? $"수면 {PresentationCopyMapper.V2StageLabel(vm.SleepStage)} · {FormatDuration(vm.CurrentSleepStretchMinutes)} 이어짐"
                : $"졸림 {vm.FatigueLabel} · {FormatDuration(vm.AwakeMinutes)} 깨어 있음";

        private static string FormatDuration(int minutes) => minutes >= 60 ? $"{minutes / 60}시간 {minutes % 60:00}분" : $"{minutes}분";

        /// <summary>
        /// 아기 기분 게이지. 아기 곁을 떠나 있고 모니터로도 보지 않았다면 수치를
        /// 그리지 않는다. 이 공백이 베이비 모니터를 챙길 이유다.
        /// </summary>
        private void DrawMoodOrnament(Rect rect, V2PlayViewModel vm, bool portrait)
        {
            if (!vm.BabyStateVisible)
            {
                DrawStatusOrnament(rect, "아기 기분", "확인 불가", 0f,
                    new Color(0.55f, 0.57f, 0.6f), portrait);
                return;
            }
            DrawStatusOrnament(rect, vm.BabyStateViaMonitor ? "아기 기분 · 📟" : "아기 기분",
                $"{vm.BabyMood:0}  {vm.BabyMoodLabel}",
                Mathf.Clamp01((float)vm.BabyMood / 100f),
                MoodColor(vm.BabyMood), portrait);
        }

        /// <summary>아기 기분 게이지 색. 등급 경계는 BabyMoodResolver.Label과 같다.</summary>
        private static Color MoodColor(double mood)
        {
            if (mood >= 60) return new Color(0.49f, 0.84f, 0.61f);
            if (mood >= 40) return new Color(0.95f, 0.79f, 0.39f);
            return new Color(0.94f, 0.39f, 0.34f);
        }

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
