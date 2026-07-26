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

        private Font _font;
        private Texture2D _room;
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
        private ItemId? _setupFocus;

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
            LoadItemArt(ItemId.Carrier, "carrier");
            LoadItemArt(ItemId.Pacifier, "pacifier");
            LoadItemArt(ItemId.Noise, "noise");
            LoadItemArt(ItemId.Monitor, "monitor");
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
            Fill(new Rect(0, 0, LandscapeWidth, LandscapeHeight), new Color(0.01f, 0.025f, 0.05f, 0.34f));
            GUI.Label(new Rect(470, 220, 980, 110), "NOT A NAP", _title);
            GUI.Label(new Rect(650, 328, 620, 56), "백일의 밤", Centered(_headline));
            GUI.Label(new Rect(610, 445, 700, 100), "오늘 밤은 아빠 차례다.\n울음보다 먼저 오는 신호를 읽어보자.", Centered(_body));
            if (GUI.Button(new Rect(760, 690, 400, 78), "첫째 밤 시작하기", _button))
                _flow.StartGame();
            GUI.Label(new Rect(700, 790, 520, 32), "약 5분 · 정답보다 서로의 리듬을 알아가는 밤", Centered(_caption));
        }

        private void DrawSetup()
        {
            var vm = _flow.BuildV2Setup();
            if (_portrait) { DrawPortraitSetup(vm); return; }
            Fill(new Rect(0, 0, LandscapeWidth, LandscapeHeight), new Color(0.015f, 0.035f, 0.065f, 0.46f));
            GUI.Label(new Rect(90, 64, 900, 56), $"{vm.NightLabel}  ·  밤 준비", _display);
            GUI.Label(new Rect(1450, 75, 360, 44), $"가져갈 물건  {vm.SelectedCount} / {vm.Slots}", Right(_headline));
            if (vm.IsFirstNight)
                DrawCarePairSetup(vm, false);
            else
                GUI.Label(new Rect(92, 130, 1500, 40), $"“{vm.TemperamentHint}” · {vm.CaregiverStyleName}", _body);

            const float displayWidth = 400f;
            const float gap = 48f;
            for (int i = 0; i < vm.Cards.Count; i++)
            {
                var card = vm.Cards[i];
                var rect = new Rect(88 + i * (displayWidth + gap), 330, displayWidth, 390);
                DrawCollectibleItem(rect, card, false);
            }

            var focused = FocusedSetupCard(vm);
            if (focused != null)
                DrawSetupItemDetail(new Rect(130, 748, 1120, 174), focused, false);
            GUI.Label(new Rect(130, 930, 940, 34), "소품을 눌러 오늘 밤의 진열대에 올리세요.", _caption);

            var oldEnabled = GUI.enabled;
            GUI.enabled = vm.CanStart;
            string next = vm.CanStart ? "이 준비로 밤 시작하기  →" : $"물건을 {vm.Slots}개 골라주세요";
            if (GUI.Button(new Rect(1300, 900, 520, 82), next, _button))
            {
                _audio?.PlayUi();
                _flow.ConfirmV2Setup();
            }
            GUI.enabled = oldEnabled;
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
            GUI.Label(new Rect(area.x, area.y + 306, area.width, 48), card.Name, nameStyle);
            if (card.Selected)
            {
                var badge = new GUIStyle(_caption)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.78f, 0.36f) }
                };
                GUI.Label(new Rect(area.x, area.y + 352, area.width, 30), "✓ 오늘 밤에 챙김", badge);
            }
            else if (card.Disabled)
                GUI.Label(new Rect(area.x, area.y + 352, area.width, 30), "선택 칸이 가득 찼어요", Centered(_caption));
            else if (hovered)
                GUI.Label(new Rect(area.x, area.y + 352, area.width, 30), "눌러서 챙기기", Centered(_caption));

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
            Fill(rect, new Color(0.025f, 0.05f, 0.08f, 0.78f));
            Fill(new Rect(rect.x, rect.y, 6f, rect.height), card.Selected
                ? new Color(1f, 0.72f, 0.32f)
                : new Color(0.48f, 0.57f, 0.66f));
            float inset = portrait ? 34f : 42f;
            GUI.Label(new Rect(rect.x + inset, rect.y + 18, rect.width - inset * 2f, 42),
                card.Selected ? $"{card.Name} · 오늘 밤에 챙겼어요" : card.Name, _headline);
            GUI.Label(new Rect(rect.x + inset, rect.y + 68, rect.width - inset * 2f, 50),
                card.Desc, _body);
            var warning = new GUIStyle(_caption)
            {
                normal = { textColor = new Color(0.94f, 0.76f, 0.52f) }
            };
            GUI.Label(new Rect(rect.x + inset, rect.y + 124, rect.width - inset * 2f, rect.height - 128),
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

        private void DrawPreparedItems(Rect rect, bool showLabel)
        {
            if (showLabel)
                GUI.Label(new Rect(rect.x, rect.y - 28, rect.width, 26), "오늘 챙긴 물건", _caption);
            int count = _flow.SelectedItems.Count;
            if (count == 0) return;
            float size = Mathf.Min(rect.height, (rect.width - (count - 1) * 8f) / count);
            for (int i = 0; i < count; i++)
                DrawItemArt(_flow.SelectedItems[i], new Rect(rect.x + i * (size + 8f), rect.y, size, size));
        }

        private bool DrawActionButton(Rect rect, V2ActionButtonViewModel action, V2PlayViewModel vm, bool portrait)
        {
            ItemId? item = ItemForAction(action.Action);
            bool active = action.Action == V2ActionId.ToggleCarrier && vm.CarrierOn ||
                          action.Action == V2ActionId.ToggleNoise && vm.NoiseOn;
            bool clicked = GUI.Button(rect, GUIContent.none, active ? _buttonSelected : _buttonSmall);
            float iconSize = item.HasValue ? rect.height - (portrait ? 12f : 10f) : 0f;
            if (item.HasValue)
                DrawItemArt(item.Value, new Rect(rect.x + 8f, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize));
            float textInset = item.HasValue ? iconSize + 18f : 12f;
            var labelStyle = new GUIStyle(active ? _buttonSelected : _buttonSmall)
            {
                alignment = item.HasValue ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter,
                normal = { background = null },
                hover = { background = null },
                active = { background = null }
            };
            GUI.Label(new Rect(rect.x + textInset, rect.y, rect.width - textInset - 10f, rect.height), action.Label, labelStyle);
            return clicked;
        }

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

            DrawTopBar(vm);
            DrawLandscapeBaby(vm);
            DrawStatusPanel(vm);
            DrawActionPanel(vm);
            DrawEventPanel(vm);
            if (IsSleeping(vm))
            {
                Panel(new Rect(448, 700, 934, 72), 0.72f);
                GUI.Label(new Rect(470, 718, 210, 36), "아기가 자는 동안", _caption);
                DrawSleepIntervalChoices(new Rect(680, 708, 680, 54), true);
            }

            if (_flow.PendingOverlay != null) DrawOverlay(_flow.PendingOverlay);
        }

        private void DrawTopBar(V2PlayViewModel vm)
        {
            Fill(new Rect(0, 0, LandscapeWidth, 94), new Color(0.02f, 0.045f, 0.08f, 0.76f));
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

            Panel(new Rect(48, 640, 984, 250), 0.72f);
            GUI.Label(new Rect(82, 675, 270, 44), "연속 수면", _caption);
            GUI.Label(new Rect(82, 720, 270, 62), FormatDuration(vm.CurrentSleepStretchMinutes), _headline);
            DrawProgress(new Rect(82, 802, 260, 18), Mathf.Clamp01(vm.CurrentSleepStretchMinutes / 300f), new Color(0.38f, 0.68f, 0.86f));
            GUI.Label(new Rect(400, 675, 250, 44), "보호자 체력", _caption);
            GUI.Label(new Rect(400, 720, 250, 62), $"{vm.ParentStamina:0}", _headline);
            DrawProgress(new Rect(400, 802, 240, 18), Mathf.Clamp01((float)vm.ParentStamina / 100f), vm.ParentStamina >= 30 ? new Color(0.49f, 0.82f, 0.6f) : new Color(0.9f, 0.38f, 0.34f));
            GUI.Label(new Rect(700, 675, 260, 44), "마음의 여유", _caption);
            GUI.Label(new Rect(700, 720, 260, 62), $"{vm.CaregiverComposure:0}", _headline);
            DrawProgress(new Rect(700, 802, 250, 18), Mathf.Clamp01((float)vm.CaregiverComposure / 100f), new Color(0.66f, 0.53f, 0.92f));
            DrawPreparedItems(new Rect(700, 836, 250, 46), false);

            DrawPortraitEvent(vm);
            DrawPortraitActions(vm);
        }

        private void DrawBabyStateVisual(V2PlayViewModel vm, Rect rect)
        {
            DrawHomeMap(vm, rect, true);
        }

        private void DrawLandscapeBaby(V2PlayViewModel vm)
        {
            DrawHomeMap(vm, new Rect(448, 132, 934, 620), false);
        }

        private void DrawHomeMap(V2PlayViewModel vm, Rect rect, bool portrait)
        {
            var roomTitleStyle = LabelStyle(portrait ? 30 : 34, FontStyle.Bold,
                new Color(0.96f, 0.93f, 0.86f));
            var roomItemStyle = LabelStyle(portrait ? 22 : 20, FontStyle.Bold,
                new Color(0.78f, 0.83f, 0.87f));
            var stateTitleStyle = LabelStyle(portrait ? 22 : 20, FontStyle.Bold,
                new Color(0.82f, 0.87f, 0.91f));
            var stateSignalStyle = LabelStyle(portrait ? 24 : 24, FontStyle.Normal,
                new Color(0.9f, 0.92f, 0.94f));

            // 플레이어가 서 있는 방을 크게 보여주고, 집 구조는 우측 상단 미니맵으로만 제공한다.
            DrawRoomFocusBackdrop(vm.CaregiverLocation, rect);
            Fill(rect, RoomFocusTint(vm.CaregiverLocation));
            Fill(new Rect(rect.x, rect.yMax - (portrait ? 82 : 96), rect.width, portrait ? 82 : 96),
                new Color(0.015f, 0.035f, 0.06f, 0.62f));
            GUI.Label(new Rect(rect.x + 24, rect.yMax - (portrait ? 72 : 86), rect.width * 0.54f, 40),
                $"현재 위치 · {HomeLocationLabel(vm.CaregiverLocation)}", roomTitleStyle);
            GUI.Label(new Rect(rect.x + 24, rect.yMax - 42, rect.width * 0.6f, 30),
                RoomFocusItems(vm), roomItemStyle);

            bool babyVisible = vm.BabyLocation == vm.CaregiverLocation;
            if (babyVisible)
            {
                float babySize = portrait ? 245f : 360f;
                var babyRect = new Rect(
                    rect.center.x - babySize * 0.5f,
                    rect.center.y - babySize * 0.42f,
                    babySize, babySize);
                DrawAnimatedBaby(vm, babyRect);
                DrawSignalMotionCue(vm, babyRect, portrait);
                DrawBabbleBubble(vm, babyRect, portrait);
            }
            else
            {
                GUI.Label(new Rect(rect.x + rect.width * 0.2f, rect.y + rect.height * 0.38f,
                    rect.width * 0.6f, 90), "아기는 아기방에 있어요.\n이동 중에도 아기의 상태는 계속 변합니다.",
                    Centered(_body));
            }

            var stateOverlay = new Rect(rect.x + 24, rect.y + 20,
                rect.width * (portrait ? 0.56f : 0.55f), portrait ? 116 : 105);
            Fill(stateOverlay, new Color(0.025f, 0.06f, 0.105f, 0.72f));
            GUI.Label(new Rect(stateOverlay.x + 18, stateOverlay.y + 8, stateOverlay.width - 36, 38),
                $"아기의 지금 · {BabyStateHeadline(vm)}", stateTitleStyle);
            GUI.Label(new Rect(stateOverlay.x + 18, stateOverlay.y + 45, stateOverlay.width - 36, stateOverlay.height - 50),
                vm.CurrentSignal, stateSignalStyle);

            DrawMiniMap(vm, new Rect(
                rect.xMax - (portrait ? 350 : 360),
                rect.y + 18,
                portrait ? 330 : 340,
                portrait ? 240 : 210), portrait);
        }

        private void DrawMiniMap(V2PlayViewModel vm, Rect rect, bool portrait)
        {
            Fill(rect, new Color(0.015f, 0.035f, 0.06f, 0.72f));
            float gap = 6f;
            var nursery = new Rect(rect.x + 8, rect.y + 8, rect.width * 0.55f - 10, rect.height - 34);
            var kitchen = new Rect(nursery.xMax + gap, rect.y + 8,
                rect.xMax - nursery.xMax - gap - 8, (rect.height - 38) * 0.52f);
            var bathroom = new Rect(kitchen.x, kitchen.yMax + gap, kitchen.width,
                rect.yMax - kitchen.yMax - gap - 26);
            DrawMiniMapRoom(nursery, "아기방", HomeLocation.Nursery, vm, portrait);
            DrawMiniMapRoom(kitchen, "주방", HomeLocation.Kitchen, vm, portrait);
            DrawMiniMapRoom(bathroom, "욕실", HomeLocation.Bathroom, vm, portrait);
            var helpStyle = LabelStyle(portrait ? 18 : 18, FontStyle.Bold,
                new Color(0.83f, 0.71f, 0.48f), TextAnchor.MiddleLeft);
            GUI.Label(new Rect(rect.x + 8, rect.yMax - 24, rect.width - 16, 22),
                portrait ? "WASD · 방 이동" : "WASD로 이동 · 2–3분 경과", helpStyle);
        }

        private void DrawRoomFocusBackdrop(HomeLocation location, Rect rect)
        {
            if (_room != null)
            {
                Rect crop = location switch
                {
                    HomeLocation.Kitchen => new Rect(0.42f, 0.02f, 0.5f, 0.88f),
                    HomeLocation.Bathroom => new Rect(0.68f, 0.02f, 0.3f, 0.88f),
                    _ => new Rect(0.05f, 0.02f, 0.64f, 0.9f)
                };
                GUI.DrawTextureWithTexCoords(rect, _room, crop, true);
            }
            if (location == HomeLocation.Kitchen)
            {
                Fill(new Rect(rect.x, rect.yMax - rect.height * 0.25f, rect.width, rect.height * 0.25f),
                    new Color(0.13f, 0.075f, 0.035f, 0.74f));
                Fill(new Rect(rect.x + rect.width * 0.08f, rect.yMax - rect.height * 0.38f,
                    rect.width * 0.34f, rect.height * 0.12f), new Color(0.34f, 0.22f, 0.12f, 0.72f));
            }
            else if (location == HomeLocation.Bathroom)
            {
                for (int i = 1; i < 6; i++)
                    Fill(new Rect(rect.x, rect.y + rect.height * i / 6f, rect.width, 2f),
                        new Color(0.55f, 0.72f, 0.75f, 0.15f));
                Fill(new Rect(rect.x, rect.yMax - rect.height * 0.2f, rect.width, rect.height * 0.2f),
                    new Color(0.04f, 0.11f, 0.13f, 0.7f));
            }
        }

        private void DrawMiniMapRoom(Rect room, string name, HomeLocation location, V2PlayViewModel vm, bool portrait)
        {
            bool current = vm.CaregiverLocation == location;
            bool babyHere = vm.BabyLocation == location;
            var roomStyle = LabelStyle(portrait ? 21 : 18, FontStyle.Bold,
                current ? new Color(0.88f, 0.97f, 0.91f) : new Color(0.78f, 0.83f, 0.87f));
            var minuteStyle = LabelStyle(portrait ? 18 : 16, FontStyle.Bold,
                new Color(0.83f, 0.71f, 0.48f));
            Fill(room, current ? new Color(0.18f, 0.34f, 0.31f, 0.9f) :
                new Color(0.055f, 0.09f, 0.14f, 0.78f));
            GUI.Label(new Rect(room.x + 6, room.y + 4, room.width - 12, 28),
                (current ? "● " : "") + name + (babyHere ? "  ◉" : ""), roomStyle);
            int minutes = HomeMovementResolver.TravelMinutes(vm.CaregiverLocation, location);
            var old = GUI.enabled;
            GUI.enabled = old && !current && !_flow.InputLocked;
            if (GUI.Button(room, GUIContent.none, GUIStyle.none))
                MoveToRoom(location);
            GUI.enabled = old;
            if (!current)
                GUI.Label(new Rect(room.x + 6, room.yMax - 24, room.width - 12, 20), $"{minutes}분", minuteStyle);
        }

        private void HandleRoomMovementKeys(V2PlayViewModel vm)
        {
            var currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.KeyDown ||
                _flow.InputLocked || Time.unscaledTime < _nextKeyboardMoveAt) return;

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
                _audio.PlayMove();
                TriggerImpact(new Color(0.45f, 0.68f, 0.8f, 0.3f), 2f, 0.2f);
            }
        }

        private static Color RoomFocusTint(HomeLocation location) => location switch
        {
            HomeLocation.Kitchen => new Color(0.12f, 0.085f, 0.045f, 0.42f),
            HomeLocation.Bathroom => new Color(0.035f, 0.09f, 0.11f, 0.48f),
            _ => new Color(0.02f, 0.055f, 0.09f, 0.36f)
        };

        private static string RoomFocusItems(V2PlayViewModel vm) => vm.CaregiverLocation switch
        {
            HomeLocation.Kitchen => "젖병 소독기 · 분유포트 · 준비한 분유",
            HomeLocation.Bathroom => vm.BathThermometerRetrieved
                ? "탕온계를 챙겼어요 · 목욕용품"
                : "탕온계 · 목욕용품",
            _ => "침대 · 베이비 모니터 · 아기의 숨소리"
        };

        private void DrawSignalMotionCue(V2PlayViewModel vm, Rect babyRect, bool portrait)
        {
            string cue = null;
            if (vm.CurrentSignal.Contains("입맛")) cue = "쩝… 쩝…";
            else if (vm.CurrentSignal.Contains("손을 입")) cue = "손을 입으로";
            else if (vm.CurrentSignal.Contains("하품")) cue = "하—암";
            else if (vm.CurrentSignal.Contains("눈을 비비")) cue = "눈을 슥슥";
            else if (vm.CurrentSignal.Contains("숨")) cue = "후…  후…";
            else if (vm.CurrentSignal.Contains("꼼지락")) cue = "꼼지락";
            if (string.IsNullOrEmpty(cue)) return;

            float pulse = (Mathf.Sin(Time.unscaledTime * 3f) + 1f) * 0.5f;
            var style = LabelStyle(portrait ? 22 : 25, FontStyle.Bold,
                Color.Lerp(new Color(0.95f, 0.72f, 0.45f), new Color(0.55f, 0.9f, 0.72f), pulse),
                TextAnchor.MiddleCenter);
            GUI.Label(new Rect(babyRect.center.x - 100, babyRect.yMax - 68, 200, 50), cue, style);
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
            Panel(new Rect(48, 920, 984, 250), 0.72f);
            GUI.Label(new Rect(82, 950, 900, 42), !vm.CauseResolved ? $"결정까지 {UpdateDecisionTimer(vm)}초" : "방금 일어난 일", _caption);
            string title = vm.CauseResolved ? "호흡과 몸의 힘을 살핀다." : "무엇이 불편한 걸까?";
            string detail = vm.CurrentSignal;
            var outcome = _lastResult?.Outcome;
            if (outcome != null)
            {
                title = outcome.Accepted ? ActionFeedbackTitle(outcome) : "지금은 할 수 없어요.";
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
            Panel(new Rect(0, 1180, PortraitWidth, 740), 0.74f);
            GUI.Label(new Rect(48, 1200, 700, 52),
                vm.ParentStamina <= 0 ? "먼저 숨을 고르세요" :
                IsSleeping(vm) ? "아기가 자는 동안" : "어떻게 할까요?", _headline);
            // 세로 화면에도 가로와 같은 수면 중 시간 보내기 입력을 제공한다(Figma M_SLEEP_FAST_FORWARD).
            if (IsSleeping(vm))
            {
                DrawSleepIntervalChoices(new Rect(48, 1260, 984, 70), true);
            }
            float tabY = IsSleeping(vm) ? 1350 : 1280;
            bool exhausted = vm.ParentStamina <= 0;
            DrawTab(new Rect(48, tabY, 305, 70), "살펴보기", ActionGroup.Diagnose);
            DrawTab(new Rect(388, tabY, 305, 70), "돌보기", ActionGroup.Care, !exhausted);
            DrawTab(new Rect(727, tabY, 305, 70), "수유 준비", ActionGroup.Feed, !exhausted);

            var actions = ActionsFor(_actionGroup, vm.ParentStamina <= 0);
            int visibleIndex = 0;
            for (int i = 0; i < actions.Length; i++)
            {
                var action = vm.Actions.Find(a => a.Action == actions[i]);
                if (action == null || !action.Enabled) continue;
                int col = visibleIndex % 2;
                int row = visibleIndex / 2;
                var rect = new Rect(48 + col * 510, tabY + 105 + row * 108, 474, 84);
                var oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && action.Enabled && !_flow.InputLocked;
                if (DrawActionButton(rect, action, vm, true)) PerformV2Action(action.Action);
                GUI.enabled = oldEnabled;
                visibleIndex++;
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
            if (GUI.Button(new Rect(190, 1080, 700, 100), "밤을 이어가기", _button))
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
            DrawPreparedItems(new Rect(74, 920, 280, 54), true);
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
            GUI.Label(new Rect(478, 816, 840, 28), !vm.CauseResolved ? $"결정까지  {UpdateDecisionTimer(vm)}초" : "방금 일어난 일", _caption);

            string title = "작은 숨소리가 방 안에 이어진다.";
            string detail = BabyStepHint(vm);
            var outcome = _lastResult?.Outcome;
            if (outcome != null)
            {
                title = outcome.Accepted ? ActionFeedbackTitle(outcome) : "지금은 할 수 없어요.";
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
            int remaining = Mathf.Max(0, Mathf.CeilToInt(_decisionDeadline - Time.unscaledTime));
            if (remaining == 0 && !_timeoutSent && !_flow.InputLocked)
            {
                _timeoutSent = true;
                PerformV2Action(V2ActionId.Hesitate);
            }
            return remaining;
        }

        private void PerformV2Action(V2ActionId action)
        {
            _lastResult = _flow.ActV2(action);
            var outcome = _lastResult?.Outcome;
            if (outcome == null) return;
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

        private void TriggerImpact(Color color, float strength, float duration)
        {
            _impactStarted = Time.unscaledTime;
            _impactColor = color;
            _impactStrength = strength;
            _impactDuration = duration;
        }

        private static string ActionFeedbackTitle(V2ActionOutcome outcome)
        {
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
                V2ActionId.CheckHungerSignals => "입과 손의 움직임을 살폈다.",
                V2ActionId.CheckDiaper => "불편한 곳부터 차례로 확인했다.",
                V2ActionId.CheckEnvironment => "방 안의 공기를 살폈다.",
                V2ActionId.PrepareWater => "젖병에 따뜻한 물을 준비했다.",
                V2ActionId.CoolBottle => "손목에 닿는 온도를 확인했다.",
                V2ActionId.FeedPreparedBottle => "아기의 삼키는 리듬을 기다렸다.",
                _ => "아기가 작은 움직임으로 답한다."
            };
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
            if (GUI.Button(new Rect(760, 650, 400, 66), "밤을 이어가기", _button))
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
            GUI.Label(new Rect(770, 300, 940, 85), vm.LearnedSignal, _headline);
            GUI.Label(new Rect(770, 405, 940, 70), vm.CaregiverGrowth, _body);
            DrawLandscapeHabitNotes(vm, 770, 490);
            GUI.Label(new Rect(770, 625, 940, 62), vm.NextNightNote, _body);
            GUI.Label(new Rect(770, 700, 940, 62), vm.ShareCardText, _caption);
            string nextLabel = vm.HasNextNight ? NextNightButtonLabel(vm.NightId) : "엔딩 보기 →";
            if (GUI.Button(new Rect(1290, 920, 520, 76), nextLabel, _button))
            {
                if (vm.HasNextNight) _flow.AdvanceFromV2Diary();
                else _flow.AdvanceToEnding();
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
            GUI.Label(new Rect(140, 690, 800, 160), "오늘 밤은 아빠 차례다.\n울음보다 먼저 오는 신호를 읽어보자.", Centered(_body));
            if (GUI.Button(new Rect(140, 1110, 800, 120), "첫째 밤 시작하기", _button)) _flow.StartGame();
            GUI.Label(new Rect(140, 1260, 800, 60), "약 5분 · 정답보다 서로의 리듬을 알아가는 밤", Centered(_caption));
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
            Color old = GUI.backgroundColor;
            if (value == selected) GUI.backgroundColor = new Color(0.53f, 0.75f, 0.63f);
            if (GUI.Button(rect, label, _buttonSmall)) _flow.SelectCaregiverStyle(value);
            GUI.backgroundColor = old;
        }

        private void DrawTemperamentButton(Rect rect, string label, Temperament value, SetupViewModel vm)
        {
            Color old = GUI.backgroundColor;
            if (ReferenceEquals(value, _flow.Session.Run.Temperament))
                GUI.backgroundColor = new Color(0.91f, 0.7f, 0.36f);
            if (GUI.Button(rect, label, _buttonSmall)) _flow.SelectBabyTemperament(value);
            GUI.backgroundColor = old;
        }

        private void DrawPortraitSetup(SetupViewModel vm)
        {
            Fill(new Rect(0, 0, PortraitWidth, PortraitHeight), new Color(0.015f, 0.035f, 0.065f, 0.52f));
            GUI.Label(new Rect(48, 55, 750, 74), $"{vm.NightLabel} · 밤 준비", _display);
            if (vm.IsFirstNight) DrawCarePairSetup(vm, true);
            else GUI.Label(new Rect(48, 145, 984, 100), $"“{vm.TemperamentHint}” · {vm.CaregiverStyleName}", _body);
            GUI.Label(new Rect(48, 365, 984, 50), $"가져갈 물건  {vm.SelectedCount} / {vm.Slots}", _headline);
            for (int i = 0; i < vm.Cards.Count; i++)
            {
                var card = vm.Cards[i];
                int col = i % 2;
                int row = i / 2;
                var rect = new Rect(48 + col * 510, 425 + row * 390, 474, 370);
                DrawCollectibleItem(rect, card, true);
            }

            var focused = FocusedSetupCard(vm);
            if (focused != null)
                DrawSetupItemDetail(new Rect(48, 1218, 984, 230), focused, true);
            GUI.Label(new Rect(48, 1470, 984, 55), "소품을 눌러 오늘 밤의 진열대에 올리세요.", _caption);
            var previous = GUI.enabled;
            GUI.enabled = vm.CanStart;
            if (GUI.Button(new Rect(100, 1710, 880, 120), vm.CanStart ? "밤 시작하기 →" : $"물건을 {vm.Slots}개 골라주세요", _button))
            {
                _audio?.PlayUi();
                _flow.ConfirmV2Setup();
            }
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
            Panel(new Rect(60, 880, 960, 700));
            GUI.Label(new Rect(110, 930, 860, 48), "육아일지", _caption);
            GUI.Label(new Rect(110, 1010, 860, 130), vm.LearnedSignal, _headline);
            DrawPortraitHabitNotes(vm, 110, 1140);
            GUI.Label(new Rect(110, 1375, 860, 90), vm.NextNightNote, _body);
            GUI.Label(new Rect(110, 1470, 860, 90), vm.ShareCardText, _caption);
            string nextLabel = vm.HasNextNight ? NextNightButtonLabel(vm.NightId) : "엔딩 보기 →";
            if (GUI.Button(new Rect(100, 1730, 880, 110), nextLabel, _button))
            {
                if (vm.HasNextNight) _flow.AdvanceFromV2Diary();
                else _flow.AdvanceToEnding();
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
                GUI.Label(new Rect(x, y + 45 + i * 86, 860, 82),
                    $"• {vm.HabitNotes[i]}\n  {vm.HabitEffects[i]}", _body);
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
                $"백일의 밤 · {PresentationCopyMapper.EndingStatusLabel(vm.IsSuccess)}", statusStyle);
            GUI.Label(new Rect(x + 60, y + 140, panelWidth - 120, 130), vm.Symbol, symbolStyle);
            GUI.Label(new Rect(x + 60, y + 275, panelWidth - 120, 90), vm.Title, Centered(_display));
            GUI.Label(new Rect(x + 110, y + 380, panelWidth - 220, 120), vm.Subtitle, Centered(_body));
            GUI.Label(new Rect(x + 110, y + 515, panelWidth - 220, 55),
                $"지켜 낸 조건  {vm.MetConditionCount} / {vm.RequiredConditionCount}",
                Centered(_headline));
            GUI.Label(new Rect(x + 110, y + 580, panelWidth - 220, 130),
                vm.MetConditions.Count > 0 ? string.Join("  ·  ", vm.MetConditions) : "다음 밤에 다시 이어갈 신호를 남겼어요.",
                Centered(_caption));
            if (GUI.Button(new Rect(x + panelWidth * 0.2f, y + panelHeight - 135, panelWidth * 0.6f, 82),
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
            if (outcome.ActivityLocation == "주방")
                return $"주방에 다녀오는 동안 {outcome.TimeDeltaMinutes}분이 흘렀다.";
            if (outcome.HeadSupported)
                return "목과 머리를 받치자 몸이 품 안으로 기대온다.";
            if (outcome.Action == V2ActionId.CatchBreath)
                return outcome.ObservedSignals.Count > 0
                    ? $"숨을 고르자 보이지 않던 신호가 들어온다. {PresentationCopyMapper.ObservationSignal(outcome.ObservedSignals[0])}"
                    : "숨을 길게 내쉬고 아기의 다음 움직임을 기다린다.";
            if (outcome.ObservedSignals.Count > 0)
                return PresentationCopyMapper.ObservationSignal(outcome.ObservedSignals[0]);
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
            if (outcome.Action == V2ActionId.CheckEnvironment)
                return $"온도 {vm.TemperatureCelsius:0.#}°C · 습도 {vm.HumidityPercent:0.#}%";
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
