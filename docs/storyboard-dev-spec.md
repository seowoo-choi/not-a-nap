# NOT A NAP : 백일의 밤 — 상세 스토리보드 및 화면 개발 명세

> 문서 상태: `V1 LEGACY / DO NOT IMPLEMENT AS CURRENT V2`
> 적용 대상: Unity 6000.3.20f1 / PC WebGL / 16:9
> 기준 해상도: 1920×1080, Canvas Scaler `Scale With Screen Size`
> 현재 구현 원본: [`code-first-development-plan.md`](code-first-development-plan.md) + 실제 `Assets/Scripts/Core` 코드
> Figma: 분위기와 배치 참고용이며 판정 규칙의 원본이 아니다.

> [!WARNING]
> 이 문서의 본문은 V1 `GameAction`·1시간 턴·자동 기저귀 사건을 기준으로 작성되어 현재 V2 통잠 루프와 충돌한다.
> `READY FOR PRESENTATION IMPLEMENTATION` 상태를 취소한다. 아래 화면 배치·색상·효과 ID만 참고하고,
> 행동 계약과 화면 흐름은 코드 기준 개발 계획의 P0-1~P0-5가 끝난 뒤 다시 생성한다.

## 0. 2026-07-21 코드 감사 결과

- 현재 실제 V2 화면은 `TITLE → SETUP → PLAY → DIARY → 처음부터 다시` 한 밤까지만 연결되어 있다.
- V2 행동은 `GameAction`이 아니라 `V2ActionId`, 결과는 `V2ActionOutcome`을 사용한다.
- V2 행동은 스스로 분 시간을 진행하므로 아래 V1 공통 시퀀스의 `Apply → EndTurn`을 그대로 사용하면 안 된다.
- 기저귀 확인·분유 준비·REM/NREM은 코드에 존재하지만 피로 신호, 정상 온습도, 상태 기반 각성, 세 밤 Memory/Ending 연결은 미완성이다.
- 35개 Figma 프레임은 Scene 목록이 아니라 `PLAY`의 상태와 QA 캡처 시나리오로 취급한다.

현재 개발 순서와 정확한 파일·테스트 계약은 [`code-first-development-plan.md`](code-first-development-plan.md)를 따른다.

---

## 1. 문서의 목적

이 문서는 개발자가 별도 설명 없이 다음을 구현할 수 있도록 작성한다.

- 화면에 무엇이 보여야 하는가
- 사용자가 무엇을 누를 수 있는가
- 클릭하면 어떤 연출이 어떤 순서로 실행되는가
- 어떤 기존 Core API를 호출하는가
- 상태를 언제 다시 읽어 화면을 갱신하는가
- 어떤 화면 또는 오버레이로 이동하는가
- 완료 여부를 무엇으로 검증하는가

화면은 사건마다 새 Scene을 만드는 구조가 아니다. 실제 화면은 아래 7개이며, 밤의 사건과 행동 결과는 `PLAY` 화면의 상태·로그·오버레이로 표현한다.

```text
TITLE
 → SETUP_DAY1 → PLAY_DAY1 → DIARY_DAY1
 → SETUP_DAY2 → PLAY_DAY2 → DIARY_DAY2
 → FINAL_INTRO → SETUP_FINAL → PLAY_FINAL → DIARY_FINAL
 → ENDING
```

---

## 2. 클래스와 책임

### 2.1 기존 Core — 새로 만들거나 이름을 바꾸지 않는다

| 클래스/타입 | 용도 |
|---|---|
| `RunState` | 세 밤 전체 상태, 기질, 기억, 누적 기록 |
| `NightState` | 현재 밤의 시간, 아기, 부모, 로그, 이벤트 |
| `NightFactory` | 선택 아이템을 받아 밤 상태 생성 |
| `ActionResolver` | 행동 판정과 상태 변화 |
| `TurnResolver` | 시간 경과, 패시브, 자동 사건, 밤 종료 |
| `MemoryConsolidator` | 밤 종료 후 습관 형성 |
| `EndingResolver` | 최종 엔딩 판정 |
| `GameAction` | 실제 행동 ID |
| `ActionOutcome` | 행동 수락 여부, 턴 소모 여부, 결과 |
| `GameEventId` | Presentation 연출 트리거 |

### 2.2 새 Presentation 클래스 — 권장 명칭

프로젝트에 동일 책임의 기존 클래스가 있으면 기존 명칭과 구조를 우선한다.

| 클래스 | 책임 |
|---|---|
| `GameFlowController` | 화면 전환과 중복 전환 차단 |
| `GameSessionPresenter` | `RunState`, `NightState`, 주입 RNG 보관 및 Core 호출 |
| `TitleScreenView` | 타이틀 표시와 시작 입력 전달 |
| `SetupScreenView` | 기질 힌트, 아이템 카드, 슬롯, 밤 시작 입력 |
| `PlayScreenView` | 밤 상태 렌더링과 행동 입력 전달 |
| `ActionButtonView` | 버튼 ID, 잠금, 시간 소모 배지, 보유 조건 표현 |
| `BabyVisualPresenter` | SleepStage·Held·Crying에 따른 아기 애니메이션 |
| `NightLogView` | 최근 로그 3줄 표시 |
| `ResultOverlayView` | 큰 사건 결과 모달과 입력 차단 |
| `DiaryScreenView` | 밤 결과, 일지, 습관 카드 |
| `FinalIntroScreenView` | 백일밤 습관 요약과 제약 표시 |
| `EndingScreenView` | 엔딩 6종 표시 |
| `PresentationCopyMapper` | `GameEventId`와 결과를 화면 문구로 변환 |
| `PresentationEffectPlayer` | 아래 `FX_*`, `SFX_*` 실행 |

### 2.3 절대 경계

- View와 Presenter는 `Calm`, `Sleep`, `Hunger`, `Stamina`를 직접 변경하지 않는다.
- Core 결과를 오버레이에서 다시 적용하지 않는다.
- UI 전용 RNG를 새로 생성하지 않는다.
- Core에 화면 문장, 색상, 애니메이션 이름을 넣지 않는다.

---

## 3. 공통 화면 규격

### 3.1 Safe Area

```text
전체: 1920×1080
안전 영역: X 80~1840 / Y 50~1030
상단 상태 바: 높이 96
하단 행동 영역: 높이 270
중앙 플레이 영역: 나머지 공간
```

### 3.2 공통 색상

| 토큰 | 값 | 사용 |
|---|---:|---|
| `Color/Night/Background` | `#0B1119` | 밤 배경 |
| `Color/Night/Panel` | `#152130` | 패널 |
| `Color/Text/Primary` | `#F4F0E7` | 주요 텍스트 |
| `Color/Text/Secondary` | `#AAB5C1` | 설명 |
| `Color/Accent/Warm` | `#E6B566` | 선택·시간 |
| `Color/Result/Good` | `#79D89A` | 성공 로그 |
| `Color/Result/Warn` | `#F0B35E` | 오판·거부 |
| `Color/Result/Bad` | `#E7766F` | 깨움·실패 |
| `Color/Baby` | `#78B7DB` | 아기 시점 로그 |

### 3.3 공통 효과 ID

아래 명칭은 새 Presentation 상수 또는 Addressable ID로 사용한다.

| ID | 표현 |
|---|---|
| `FX_SCREEN_FADE_IN` | 0.35초 검정→화면 |
| `FX_SCREEN_FADE_OUT` | 0.25초 화면→검정 |
| `FX_BUTTON_PRESS` | 0.08초 0.96배 축소 후 복귀 |
| `FX_INPUT_LOCK_DIM` | 입력 잠금 중 행동 영역 60% 명도 |
| `FX_STATE_VALUE_PULSE_GOOD` | 증가 수치 초록 펄스 |
| `FX_STATE_VALUE_PULSE_BAD` | 감소 수치 붉은 펄스 |
| `FX_LOG_LINE_ENTER` | 로그 한 줄이 아래에서 12px 상승 |
| `FX_OVERLAY_OPEN` | 배경 딤 + 모달 0.92→1.0 |
| `FX_OVERLAY_CLOSE` | 모달 1.0→0.96 페이드 |
| `FX_BABY_WAKE_STARTLE` | 아기 몸 움찔 + 눈 뜸 |
| `FX_BABY_CRY_SOFT` | 약한 울음 루프 |
| `FX_BABY_CRY_HARD` | 큰 울음 루프 |
| `FX_BABY_CALM_DOWN` | 울음 루프 종료 + 호흡 완화 |
| `FX_BABY_DEEP_SLEEP` | 호흡 느려짐 + 팔 이완 |
| `FX_LAYDOWN_SUCCESS` | 침대 안착 + 방 조명 완화 |
| `FX_LAYDOWN_FAILED` | 등 접촉 순간 움찔 + 파형 상승 |
| `FX_TIME_ADVANCE` | 시계 숫자 플립 0.4초 |
| `FX_NIGHT_TO_DAWN` | 배경 남색→새벽 회색 1.2초 |

### 3.4 공통 사운드 ID

| ID | 사용 시점 |
|---|---|
| `SFX_UI_CONFIRM` | 정상 버튼 클릭 |
| `SFX_UI_REJECT` | 행동 거부 |
| `SFX_UI_TOGGLE_ON` / `OFF` | 장비 토글 |
| `SFX_CLOCK_ADVANCE` | EndTurn 후 시간 변경 |
| `SFX_BABY_FUSS` | 약한 보챔 |
| `SFX_BABY_CRY` | 울음 |
| `SFX_BABY_SETTLE` | 울음 해제 |
| `SFX_BOTTLE_ACCEPT` | 수유 성공 |
| `SFX_BOTTLE_REJECT` | 수유 거부 |
| `SFX_LAYDOWN_SUCCESS` / `FAILED` | 눕히기 결과 |
| `SFX_EVENT_ALERT` | 자동 돌발 이벤트 |
| `BGM_NIGHT_ROOM` | PLAY 기본 루프 |
| `BGM_DAWN_RESULT` | 밤 종료·DIARY |

---

## 4. Core 호출 공통 시퀀스

모든 행동 버튼은 `GameSessionPresenter.TryExecuteAction(GameAction action)` 하나로 들어간다.

```csharp
if (InputLocked || CurrentNight == null || CurrentNight.Over)
    return;

InputLocked = true;
PlayView.SetActionButtonsInteractable(false);

var beforeEventCount = CurrentNight.Events.Count;
var beforeLogCount = CurrentNight.Log.Count;
var outcome = ActionResolver.Apply(CurrentRun, CurrentNight, action, Rng);

if (!outcome.Accepted)
{
    PlayView.AppendRejectedReason(outcome);
    PlayView.Render(CurrentRun, CurrentNight);
    InputLocked = false;
    PlayView.SetActionButtonsInteractable(true);
    return;
}

PlayView.RenderActionResult(outcome);

if (outcome.ConsumedTurn)
    TurnResolver.EndTurn(CurrentRun, CurrentNight, Rng);

PlayView.Render(CurrentRun, CurrentNight);
PresentNewLogs(beforeLogCount);
PresentNewEvents(beforeEventCount);

if (CurrentNight.Over)
    BeginNightCompletion();
else if (!ResultOverlay.IsOpen)
{
    InputLocked = false;
    PlayView.SetActionButtonsInteractable(true);
}
```

### 완료 조건

- 한 번의 클릭이 `Apply`를 두 번 호출하지 않는다.
- `ConsumedTurn=true`일 때만 `EndTurn`을 정확히 한 번 호출한다.
- `Accepted=false`이면 `EndTurn`을 호출하지 않는다.
- 오버레이가 열린 경우 닫기 전까지 입력 잠금을 해제하지 않는다.

---

# 5. 화면별 상세 스토리보드

## 5.1 `TITLE` — 타이틀

### 캡처에서 보여야 하는 화면

어두운 침실 문이 화면 오른쪽에 있다. 문틈의 따뜻한 복도 불빛이 점점 좁아진다. 화면 중앙보다 약간 위에 게임 제목이 보인다.

```text
NOT A NAP
백일의 밤

오늘 밤은 아빠 차례다.

[ 시작하기 ]
```

- 제목은 화면 중앙 X=960, Y=340.
- 시작 버튼은 X=760, Y=690, W=400, H=72.
- 버튼 이외의 HUD는 없다.
- 첫 진입 시 `FX_SCREEN_FADE_IN`, 문 닫힘 애니메이션 `ANIM_DOOR_CLOSE_INTRO`를 재생한다.

### 클릭 이벤트

| UI ID | 클릭 | 처리 | 다음 |
|---|---|---|---|
| `BTN_GAME_START` | 시작하기 | 입력 잠금 → 새 `RunState` 생성 → `FX_SCREEN_FADE_OUT` | `SETUP` |

### 개발 완료 조건

- 연속 클릭해도 RunState는 한 번만 생성된다.
- 뒤로 돌아왔다가 다시 시작하면 이전 RunState를 재사용하지 않는다.

---

## 5.2 `SETUP` — 밤 준비

### 캡처에서 보여야 하는 화면

화면 왼쪽 35%는 오늘 밤 정보, 오른쪽 65%는 아이템 선택 카드다.

```text
왼쪽
1일째 밤
21:00 — 06:00

“멀리서 나는 소리에도 몸을 움찔거린다.”

선택한 장비 2 / 3

오른쪽
[아기띠] [쪽쪽이] [백색소음기]
[바운서 LEGACY] [베이비 모니터]

                         [밤 시작하기]
```

카드에는 반드시 다음이 보인다.

- 아이템 이름
- 한 줄 효과
- 한 줄 부작용
- 선택 체크 표시
- 백일밤 사용 불가라면 잠금과 이유

### 카드 클릭 이벤트

| 상태 | 클릭 결과 |
|---|---|
| 미선택, 슬롯 여유 | 선택 테두리 표시, 선택 수 +1 |
| 선택됨 | 선택 해제, 선택 수 -1 |
| 미선택, 슬롯 가득 참 | `SFX_UI_REJECT`, 카드 좌우 3px 흔들림, “선택 슬롯이 가득 찼습니다.” |

아이템 선택은 Core 상태를 즉시 만들지 않는다. `SetupScreenView`의 임시 선택 목록에만 보관하고 `밤 시작하기`에서 한 번만 `NightFactory`에 전달한다.

### 밤 시작 클릭

1. `BTN_START_NIGHT` 비활성화.
2. 현재 선택 목록을 복사한다.
3. `NightFactory`의 실제 생성 API로 `NightState`를 한 번 생성한다.
4. `GameSessionPresenter.CurrentNight`에 저장한다.
5. `PLAY`로 전환한다.

### 밤별 차이

| 밤 | 슬롯 | 할머니 | 추가 표시 |
|---|---:|---|---|
| 1일차 | 3 | 사용 가능 | 첫 기질 힌트 |
| 2일차 | 3 | 미사용이면 가능 | 지난 밤 습관 카드 요약 |
| 백일밤 | 2 | 금지 | 습관 1.5배·장비 표적 사건 안내 |

---

## 5.3 `PLAY` — 밤 플레이 공통 화면

### 캡처에서 보여야 하는 화면

```text
┌────────────────────────────────────────────────────┐
│ 1일째 밤   21:00        남은 시간 9       체력 100 │
├────────────────────────────────────────────────────┤
│                                                    │
│                 [아기 일러스트]                    │
│                    말똥말똥                        │
│       Monitor 보유 시 Calm 55 / Sleep 0 / Hunger 30│
│                                                    │
├────────────────────────────────────────────────────┤
│ 21:00  오늘 밤이 시작됐다.                         │
│ 최근 로그 최대 3줄                                 │
├────────────────────────────────────────────────────┤
│ [안기][토닥][수유][눕히기][지켜보기][할머니]       │
│ [쪽쪽이 3][아기띠 OFF][소음기 OFF][바운서 OFF]     │
└────────────────────────────────────────────────────┘
```

### 영역별 구현

| View ID | 클래스 | 읽는 값 | 갱신 시점 |
|---|---|---|---|
| `TXT_CLOCK` | `PlayScreenView` | `night.Hour` | 진입, 행동 후, EndTurn 후 |
| `TXT_TURNS_LEFT` | `PlayScreenView` | 남은 시간 소모 턴 | 동일 |
| `TXT_SLEEP_STAGE` | `BabyVisualPresenter` | `night.Baby.GetStage()` | 모든 상태 갱신 |
| `GROUP_MONITOR_VALUES` | `PlayScreenView` | Calm/Sleep/Hunger | Monitor 보유 시만 |
| `BAR_STAMINA` | `PlayScreenView` | `night.Parent.Stamina` | 모든 상태 갱신 |
| `LIST_NIGHT_LOG` | `NightLogView` | `night.Log` 최근 3개 | 로그 추가 후 |
| `GROUP_ACTIONS` | `ActionButtonView[]` | 보유 아이템·밤 제약 | 진입, 토글, 이벤트 후 |

### 아기 상태 연출

| SleepStage | 애니메이션 | 보조 표현 |
|---|---|---|
| `Awake` | `ANIM_BABY_AWAKE_IDLE` | 눈 뜸, 손 움직임 |
| `Fussy` | `ANIM_BABY_FUSSY` | 얼굴 찌푸림 |
| `Drowsy` | `ANIM_BABY_DROWSY` | 눈꺼풀 무거움 |
| `Shallow` | `ANIM_BABY_ACTIVE_SLEEP` | 눈꺼풀 떨림·짧은 움직임 |
| `Deep` | `ANIM_BABY_DEEP_SLEEP` | 규칙적 호흡·팔 이완 |
| `Cry` | Calm 값에 따라 `FX_BABY_CRY_SOFT/HARD` | 울음 파형 |

---

## 5.3.1 `BTN_HOLD` — 안기

### 클릭 후 화면에서 일어나는 순서

1. 모든 행동 버튼 잠금.
2. `FX_BUTTON_PRESS`와 `ANIM_PARENT_PICK_UP` 재생.
3. `ActionResolver.Apply(..., GameAction.Hold, ...)` 호출.
4. 거부되면 `SFX_UI_REJECT`, 사유 로그 표시 후 잠금 해제.
5. 수락되면 아기 위치를 침대에서 부모 품 위치로 이동한다.
6. `outcome.ConsumedTurn`이면 `EndTurn` 호출.
7. 시계, 상태, 체력, 로그를 최신 Core 값으로 다시 그린다.
8. 울음이 해제됐으면 `FX_BABY_CALM_DOWN`.

### 화면 문구

- 성공 로그: “품에 안자 몸의 긴장이 조금 풀렸다.”
- 아기띠로 이미 안은 상태의 거부: “이미 아기띠로 안고 있다.”

### 완료 조건

- 체력 감소를 UI가 직접 계산하지 않는다.
- 애니메이션 중 재클릭해도 두 번 실행되지 않는다.

---

## 5.3.2 `BTN_PAT` — 토닥이기

1. 입력 잠금.
2. `ANIM_PARENT_PAT_LOOP`를 1.2초 재생.
3. `GameAction.Pat` 실행.
4. 수락 시 턴 종료 후 전체 상태 갱신.
5. Sleep이 증가해 Stage가 바뀌었다면 상태 라벨을 0.2초 크로스페이드한다.

화면에는 “토닥임이 일정한 리듬을 만들었다.”를 기본 결과 문구로 사용한다.

---

## 5.3.3 `BTN_FEED` — 수유

### 배고픔 조건 충족

1. `ANIM_PARENT_OFFER_BOTTLE`.
2. `SFX_BOTTLE_ACCEPT`.
3. `ANIM_BABY_FEED_ACCEPT`.
4. 최신 Hunger와 Calm을 다시 렌더링한다.
5. 로그: “젖병을 받아들였다. 숨이 천천히 고르게 돌아왔다.”

### 배고픔 조건 미달

1. `ANIM_PARENT_OFFER_BOTTLE`.
2. 아기가 고개를 돌리는 `ANIM_BABY_FEED_REJECT`.
3. `SFX_BOTTLE_REJECT`.
4. 전체 화면 전환 없이 경고 로그만 추가한다.
5. 로그: “고개를 돌리며 젖병을 밀어낸다. 배고픔이 원인은 아닌 것 같다.”
6. 턴은 Core 결과대로 소비된다.

별도의 `FAIL-MISREAD-FEED` 화면을 만들지 않는다.

---

## 5.3.4 `BTN_LAYDOWN` — 눕히기

### 클릭 전

- 이미 침대에 있으면 버튼 비활성 또는 Core 거부 사유 표시.
- 확률 미리보기는 Monitor 보유 시에만 `CalculateLaydownSuccessProbability` 결과를 `%`로 표시하는 방식을 권장한다.

### 클릭 후

1. 입력 잠금.
2. `ANIM_PARENT_LAYDOWN_BEGIN`.
3. `GameAction.Laydown` 실행.
4. 턴 소비 시 `EndTurn`.
5. `GameEventId.LaydownSucceeded`면 성공 오버레이.
6. 실패면 실패 오버레이.

#### 성공 오버레이 `OVL_LAYDOWN_SUCCESS`

```text
팔과 손에서 힘이 빠졌다.
아기는 침대에서도 잠을 이어간다.

[계속]
```

- `FX_LAYDOWN_SUCCESS`, `SFX_LAYDOWN_SUCCESS`.
- 계속 클릭 시 상태를 다시 적용하지 않고 오버레이만 닫는다.

#### 실패 오버레이 `OVL_LAYDOWN_FAILED`

```text
등이 침대에 닿는 순간 몸이 움찔했다.
눈이 다시 떠졌다.

[다시 살펴보기]
```

- `FX_LAYDOWN_FAILED`, `FX_BABY_WAKE_STARTLE`, `SFX_LAYDOWN_FAILED`.
- 닫으면 같은 `PLAY`로 복귀한다.

---

## 5.3.5 `BTN_WATCH` — 지켜보기

1. 입력 잠금.
2. 부모 캐릭터 움직임을 멈추고 `ANIM_PARENT_WATCH`.
3. `GameAction.Watch` 실행.
4. 수락 시 `EndTurn`.
5. 최신 상태를 렌더링한다.

결과 문구는 Core 결과에 따라 나눈다.

- 자기진정 성공: “손대지 않아도 호흡이 다시 잔잔해졌다.”
- 울음 중 악화: “기다리는 사이 울음이 더 커졌다.”
- 변화 미미: “조금 더 지켜봤지만 아직 잠들 준비는 아닌 듯하다.”

---

## 5.3.6 `BTN_GRANDMA` — 할머니 찬스

- 런 전체 1회.
- 백일밤에는 숨김 또는 잠금 처리한다.
- 잠금 상태에는 “백일째 밤은 아빠 혼자 버텨야 한다.” 툴팁.

수락 시 `OVL_GRANDMA`:

```text
“애는 이렇게 안는 거야.”

할머니 품에 안기자 방 안이 거짓말처럼 조용해졌다.
하지만 아기는 이 품도 기억한다.

[계속]
```

---

## 5.3.7 `BTN_PACIFIER` — 쪽쪽이

- 버튼 우측 상단에 잔여 횟수 `3`, `2`, `1`, `0`.
- `ConsumedTurn=false`이므로 시계 플립을 재생하지 않는다.
- 수락 시 `ANIM_BABY_PACIFIER_ACCEPT`.
- 뱉으면 `ANIM_BABY_PACIFIER_SPIT`와 경고 로그.
- 횟수 0이면 비활성 및 “오늘 밤은 모두 사용했다.”

---

## 5.3.8 장비 토글

### 아기띠 `BTN_CARRIER`

- OFF 클릭 → `GameAction`의 실제 Carrier 토글 값 사용 → ON 시 버튼 눌림 상태.
- ON 중에는 아기 위치를 부모 몸 앞에 고정한다.
- 백일밤 버클 고장 중에는 잠금 아이콘과 남은 턴 표시.

### 백색소음기 `BTN_NOISE`

- ON 시 방 배경에 낮은 파형을 표시하고 `SFX_WHITE_NOISE_LOOP` 재생.
- 배터리 방전 사건 후 버튼 비활성, 루프 즉시 종료.

### 바운서 `BTN_BOUNCER`

- `LEGACY` 배지 표시.
- 현재 Core 호환 동작만 연결한다.
- 새 성공 아이템처럼 강조하지 않는다.
- 대체 아이템 확정 전 추가 연출과 튜토리얼을 만들지 않는다.

---

## 5.4 자동 사건 오버레이

자동 사건은 `TurnResolver.EndTurn` 후 새로 추가된 `night.Events`를 비교하여 표시한다.

### `OVL_DIAPER_EVENT` — 1일차 00시

> 현재 Core에는 기저귀 확인 행동이 없다. 이 오버레이는 자동 사건 결과를 전달할 뿐 선택 분기를 만들지 않는다.

화면:

```text
00:00

갑자기 몸을 뒤틀며 보채기 시작했다.
기저귀를 갈아주고 나서야 표정이 조금 풀렸다.

[계속]
```

- `SFX_EVENT_ALERT`.
- Core가 이미 Calm·깨움 판정을 적용했으므로 View는 수치를 변경하지 않는다.

### `OVL_DOORBELL_EVENT` — 2일차 23시

```text
23:00

현관에서 초인종이 울렸다.
[깨어남 여부에 따른 결과 문장]

[계속]
```

소음기 방어 여부와 기질 결과는 Core 이벤트를 읽어 문구만 다르게 한다.

### 백일밤 표적 사건

| 오버레이 ID | 조건/결과 | 화면 표현 |
|---|---|---|
| `OVL_CARRIER_BUCKLE_BROKEN` | 아기띠 2턴 비활성 | 끊어진 버클 클로즈업 |
| `OVL_NOISE_BATTERY_EMPTY` | 소음기 그 밤 비활성 | 파형과 루프 사운드가 동시에 꺼짐 |
| `OVL_DAWN_WAKE` | Sleep 감소·눕히기 페널티 | 03:00 시계와 눈 뜨는 아기 |
| `OVL_SELF_SOOTHE` | 자기진정 재입면 | 아빠가 손을 뻗다 멈추고 아기가 스스로 진정 |

---

## 5.5 `DIARY` — 밤 결과와 습관

### 캡처에서 보여야 하는 화면

왼쪽은 오늘 밤 결과, 오른쪽은 습관 카드다.

```text
1일째 밤이 끝났다.

[침대에서 아침 / 품에서 아침 / 끝내 못 잠]

육아일지
“오늘 밤 …”

형성된 습관
[품에서 잠드는 습관 +0.2]
[스스로 진정하는 경험 +0.1]

[다음 밤]
```

### 진입 순서

1. `GameEventId.NightCompleted` 확인.
2. 입력 차단.
3. `MemoryConsolidator`의 실제 API를 정확히 한 번 호출.
4. NightOutcome과 memory note 저장.
5. `FX_NIGHT_TO_DAWN` 후 DIARY 표시.

### AI 경계

- 이번 Presentation 수직 슬라이스에서는 규칙 기반 문구를 사용한다.
- 추후 AI는 일지 문자열만 반환한다.
- AI 실패 시 동일 필드의 폴백 문구를 사용한다.
- AI 결과로 Memory·Ending·수치를 변경하지 않는다.

---

## 5.6 `FINAL_INTRO` — 백일째 밤

```text
시간이 흘렀다.
오늘은 백일째 밤.

이 아이가 기억한 것
[아기띠 습관]
[품에서 잠드는 습관]
[백색소음 습관]
[스스로 진정하는 경험]

오늘 밤
장비 슬롯 2개
할머니 찬스 없음
습관의 영향 1.5배

[마지막 밤 준비]
```

- 카드 수치는 실제 `run.Memory`에서 읽는다.
- 존재하지 않는 습관 카드는 숨긴다.

---

## 5.7 `ENDING` — 엔딩

화면 중앙에 엔딩 제목, 하단에 부제와 다시하기 버튼을 표시한다.

`EndingResolver`가 반환한 `EndingId`를 `PresentationCopyMapper`로 매핑한다. UI가 우선순위를 다시 판정하지 않는다.

| EndingId | 제목 |
|---|---|
| `MorningWon` | 아침이 이겼다 |
| `FamilyRoutine` | 우리 집의 루틴 |
| `UniverseInArms` | 품 안의 우주 |
| `GrandmaBest` | 할머니가 최고야 |
| `GearMaster` | 장비의 지배자 |
| `DawnSurvivor` | 새벽의 생존자 |

성공 엔딩의 마지막 고정 문장:

```text
이제 당신도 이 아이의 밤을 안다.
```

---

# 6. 밤별 캡처 체크리스트

## 6.1 1일차 필수 캡처

1. `CAP-01` TITLE 문 닫힘과 시작 버튼
2. `CAP-02` SETUP 아이템 3개 선택
3. `CAP-03` PLAY 최초 상태
4. `CAP-04` 수유 거부 로그
5. `CAP-05` 활동수면 애니메이션
6. `CAP-06` 눕히기 실패 오버레이
7. `CAP-07` 00시 기저귀 자동 사건
8. `CAP-08` DIARY 습관 카드

## 6.2 2일차 필수 캡처

1. `CAP-09` 지난 밤 습관이 표시된 SETUP
2. `CAP-10` 같은 장비의 효과 변화
3. `CAP-11` 23시 초인종 사건
4. `CAP-12` 둘째 밤 DIARY 비교

## 6.3 백일밤 필수 캡처

1. `CAP-13` FINAL_INTRO 습관 요약
2. `CAP-14` 슬롯 2개·할머니 금지 SETUP
3. `CAP-15` 표적 장비 고장
4. `CAP-16` 03시 새벽 각성 또는 자기진정
5. `CAP-17` 맨손 눕히기 결과
6. `CAP-18` ENDING

---

# 7. Presentation 테스트 계약

최소 테스트 이름 예시:

```text
GameSessionPresenterTests.AcceptedFalse_DoesNotEndTurn
GameSessionPresenterTests.ConsumedTurnFalse_DoesNotEndTurn
GameSessionPresenterTests.ConsumedTurnTrue_EndsTurnExactlyOnce
GameSessionPresenterTests.RapidDoubleClick_AppliesActionOnce
GameSessionPresenterTests.NewEvents_ArePresentedOnce
GameSessionPresenterTests.NightCompleted_TransitionsToDiaryOnce
PlayScreenViewTests.WithoutMonitor_HidesNumericBabyState
PlayScreenViewTests.WithMonitor_ShowsNumericBabyState
SetupScreenTests.FullSlots_DisablesUnselectedCards
SetupScreenTests.FinalNight_UsesTwoSlotsAndDisablesGrandma
```

---

# 8. 개발 단계

## Phase 1 — 첫째 밤 수직 슬라이스

- TITLE
- SETUP
- PLAY 전체 행동
- 공통 결과 오버레이
- 1일차 자동 기저귀 사건
- DIARY 규칙 기반 문구

## Phase 2 — 둘째 밤

- DIARY → SETUP 반복
- 습관 요약
- 초인종 사건

## Phase 3 — 백일밤과 엔딩

- FINAL_INTRO
- 슬롯 2개, 할머니 금지
- 표적 사건 UI
- 엔딩 6종

## Phase 4 — 서사 AI

- 서버 프록시
- 일지 1회 생성
- 폴백과 출력 검증

---

# 9. 이번 문서에서 의도적으로 제외한 것

- 존재하지 않는 `CheckDiaper` 행동
- `FAIL-MISREAD-*` 전체 화면
- 42개의 개별 Gameplay Scene
- 미확정 Trace/FutureEvent/Cue 실제 콘텐츠
- 예방접종·배앓이·이앓이 이벤트
- Bouncer 대체 아이템 추측
- 실제 브랜드 로고와 광고 서버

이 항목들은 Core와 기획이 확정되기 전까지 Presentation에 추가하지 않는다.
