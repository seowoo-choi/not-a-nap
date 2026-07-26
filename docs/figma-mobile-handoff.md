# NOT A NAP — 모바일 Figma 전면 개편 핸드오프

> 기준: 2026-07-26 배포 코드 `799d17b`
> 목적: 기존 1920×1080 스토리보드의 PLAY 35장을 모바일 세로 구조와 실제 C# 계약에 맞게 재작성한다.
> 판정 원본: `docs/code-first-development-plan.md` + `Assets/Scripts/Core`
> Figma 역할: Core/ViewModel 상태를 시각화하고 클릭 흐름을 표현한다. Figma가 성공·실패·숨은 원인을 판정하지 않는다.

## 1. 코드 기준 변경점

| 영역 | 실제 클래스/멤버 | 변경 계약 |
|---|---|---|
| 화면 방향 | `GameBootstrap.OnGUI()` | `Screen.height > Screen.width * 1.15`이면 1080×1920 세로 레이아웃 |
| 세로 플레이 | `GameBootstrap.DrawPortraitPlay()` | 아기 상태 → 밤 지표 → 큰 상태 문장 → 행동 탭 순서 |
| 탭 상태 | `GameBootstrap.ActionGroup` | `Diagnose / Care / Feed` 중 하나를 유지 |
| 새 각성 식별 | `DiagnosisState.EncounterSequence` | 값이 바뀐 새 각성에서만 `Diagnose`로 최초 1회 초기화 |
| 탭 클릭 | `GameBootstrap.DrawCommandTab()` | 같은 각성 안에서는 사용자가 선택한 탭을 계속 유지 |
| 아기 비주얼 | `GameBootstrap.DrawAnimatedBaby()` + `DrawBabyActionAnimation()` | ViewModel 상태와 직전 action outcome만 시각화하며 화면에서 판정하지 않음 |
| 방 이동 | `GameBootstrap.DrawRoomRibbon()` | 미니맵 없이 `아기방 / 주방 / 욕실` 알약 버튼으로 이동 |
| 입력 잠금 | `GameSessionPresenter.InputLocked` | 결과 오버레이가 열렸을 때만 행동 버튼 잠금 |
| 제한시간 | `GameBootstrap.UpdateDecisionTimer()` | `EncounterSequence`당 타이머 1회 시작, 만료 시 `Hesitate` 1회 전달 |

## 2. 모바일 공통 레이아웃

- 기준 프레임: **1080×1920**, 세로형.
- Safe area: 좌우 48px, 상단 30px, 하단 48px 이상.
- 최소 본문: 26px 기준. 핵심 상태 문장 34px 이상. 시계 52px. 주요 버튼 28px.
- 최소 터치 높이: 70px. 주요 CTA 100~120px.
- 한 화면의 작은 설명문은 최대 2줄. 나머지는 상태 그림, 진행 바, 큰 상태 문장으로 전달한다.
- PLAY 정보 순서:

```text
시계 / 새벽까지 남은 시간
신호 리본과 아기 그림
아기방 / 주방 / 욕실 이동
연속 수면 / 보호자 체력 / 마음의 여유
현재 행동 결과
살펴보기 / 돌보기 / 수유 준비
현재 탭의 행동 버튼
```

Unity 기준 좌표는 신호 리본 `46,176,760,104`, 방 이동 `y=720`, 상태 카드 `y=772`,
피드백 `58,912,964,118`, 행동 덱 `y=1060`이다. Figma 플러그인은 이 좌표를
`CODE_SYNC_UNITY_PRESENTATION_V8` 레이어로 재생성한다.

## 3. 탭 클릭 계약

### Figma 컴포넌트 ID

| Figma ID | 표시 | 코드 값 |
|---|---|---|
| `TAB_DIAGNOSE` | 살펴보기 | `ActionGroup.Diagnose` |
| `TAB_CARE` | 돌보기 | `ActionGroup.Care` |
| `TAB_FEED` | 수유 준비 | `ActionGroup.Feed` |

### 정확한 상태 흐름

```text
DiagnosisState.EncounterSequence 증가 + CauseResolved=false
 → TAB_DIAGNOSE 최초 1회 selected
 → TAB_CARE 클릭
 → 같은 EncounterSequence 동안 TAB_CARE selected 유지
 → TAB_FEED 클릭
 → 같은 EncounterSequence 동안 TAB_FEED selected 유지
 → 다음 EncounterSequence가 시작될 때만 TAB_DIAGNOSE로 초기화
```

- `CauseResolved=false`라는 이유만으로 매 프레임 `TAB_DIAGNOSE`를 다시 선택하면 안 된다.
- 탭을 바꾸는 행위는 Core 시간을 소비하지 않는다.
- `InputLocked=true`면 탭 아래 행동 입력을 잠그고 결과 오버레이를 먼저 닫게 한다.
- Figma prototype 연결도 위 순서대로 같은 프레임의 탭 variant를 유지해야 한다.

## 4. 행동 버튼 ID

| 탭 | Figma ID | `V2ActionId` |
|---|---|---|
| 살펴보기 | `BTN_CHECK_DIAPER` | `CheckDiaper` |
| 살펴보기 | `BTN_CHECK_HUNGER` | `CheckHungerSignals` |
| 살펴보기 | `BTN_CHECK_ENVIRONMENT` | `CheckEnvironment` |
| 살펴보기 | `BTN_CHECK_MONITOR` | `CheckMonitor` |
| 살펴보기 | `BTN_CHECK_RELAXATION` | `CheckLimbRelaxation` |
| 살펴보기 | `BTN_HESITATE` | `Hesitate` |
| 살펴보기 | `BTN_CATCH_BREATH` | `CatchBreath` |
| 돌보기 | `BTN_HOLD` | `Hold` |
| 돌보기 | `BTN_TOGGLE_CARRIER` | `ToggleCarrier` |
| 돌보기 | `BTN_PAT` | `Pat` |
| 돌보기 | `BTN_PACIFIER` | `Pacifier` |
| 돌보기 | `BTN_TOGGLE_NOISE` | `ToggleNoise` |
| 돌보기 | `BTN_LAYDOWN` | `Laydown` |
| 돌보기 | `BTN_CHANGE_DIAPER` | `ChangeDiaper` |
| 돌보기 | `BTN_ADJUST_TEMPERATURE` | `AdjustTemperature` |
| 돌보기 | `BTN_ADJUST_HUMIDITY` | `AdjustHumidity` |
| 수유 준비 | `BTN_SANITIZE_BOTTLE` | `SterilizeBottle` |
| 수유 준비 | `BTN_PREPARE_WATER` | `PrepareWater` |
| 수유 준비 | `BTN_COOL_BOTTLE` | `CoolBottle` |
| 수유 준비 | `BTN_FEED_PREPARED` | `FeedPreparedBottle` |

별도 `CheckBodyTemperature` 버튼은 현재 PLAY UI에서 노출하지 않는다. 체온 관련 Core 계약은
남아 있지만 Figma 화면 계약에는 복제하지 않는다. 버튼 활성 조건의 원본은
`GameSessionPresenter.BuildV2Play()`이며, 화면에 노출할 행동 목록의 원본은
`GameBootstrap.ActionsFor()`이다.

## 5. 아기 비주얼 상태 계약

### 상태 소스 우선순위

1. 실행 직후의 `V2ActionOutcome.EventIds` / `TraceIds`
2. 관찰 후 `ObservedSignals`
3. 지속 상태 `V2PlayViewModel.SleepStage`, `CryIntensity`, 이완·호흡 값

숨은 `DiagnosisState.ActiveCause`를 직접 그림으로 누설하지 않는다. 원인은 확인 행동이나 관찰 결과가 나온 뒤에만 명시한다.

| Figma variant | 시각 표현 | 코드 조건/근거 | 연결 상태 |
|---|---|---|---|
| `BABY_AWAKE_CALM` | 눈을 뜨고 조용히 주변을 봄 | `SleepStage=Awake && CryIntensity<=35` | 세로 UI 구현됨 |
| `BABY_FUSS_SOFT` | 찡그림, 몸 꼼지락, 작은 파형 | `SleepStage=Awake && 0<CryIntensity<=35` | 전용 텍스처·상태 문장 연결됨 |
| `BABY_CRY_HARD` | 입 크게 벌림, 눈물, 큰 파형 | `SleepStage=Awake && CryIntensity>35` | 전용 텍스처·상태 문장 연결됨 |
| `BABY_HUNGER_EARLY` | 입맛 다심, 입 벌림, 손 빨기 | `HungerSignalStage.Early` + 대응 `ObservedSignals` | 관찰 outcome 기반 전용 텍스처 연결됨 |
| `BABY_HUNGER_LATE` | 빠른 호흡, 머리 돌림, 배고픈 울음 | `HungerSignalStage.Late` + `RapidBreathing/HungerCry` | 관찰 outcome 기반 전용 텍스처 연결됨 |
| `BABY_DROWSY` | 눈이 반쯤 감기고 움직임 감소 | `SleepStage=Drowsy` | 전용 텍스처·상태 문장 연결됨 |
| `BABY_REM` | 눈꺼풀 떨림, 불규칙 호흡, 팔다리 움직임 | `SleepStage=RemActiveSleep`; 관찰 시 REM signals | 전용 텍스처·4프레임 앰비언트 연결됨 |
| `BABY_NREM` | 편안한 얼굴, 규칙적인 호흡 | `SleepStage=NremDeepSleep` | 전용 텍스처·4프레임 앰비언트 연결됨 |
| `BABY_RELAXED` | 팔·다리가 축 늘어짐 | `IsLimbRelaxed && IsBreathingRegular`; 확인 후 `DeepSleepObserved` | 관찰 전후 텍스처·상태 문장 연결됨 |
| `BABY_MORO` | 양팔이 순간 벌어지고 몸이 움찔 | `LaydownFailed` 후 `MoroReflex` 각성 등 의미 이벤트 | 결과 연출 텍스처 연결됨; 숨은 원인 사전 누설 없음 |
| `BABY_PACIFIER_ACCEPT` | 쪽쪽이를 물고 표정 완화 | outcome `TraceIds`에 `PacifierAccepted` | 결과 연출 텍스처 연결됨 |
| `BABY_PACIFIER_REJECT` | 쪽쪽이를 뱉고 얼굴을 돌림 | outcome `Accepted=false` + `PacifierRejected` trace | 결과 연출 텍스처 연결됨 |

REM/NREM은 정지 그림만 바꾸지 않고 최소 2프레임 또는 Smart Animate variant로 표현한다. REM은 작은 불규칙 움직임, NREM은 느리고 규칙적인 호흡을 사용한다.

## 6. 아이템 화면 계약

- SETUP은 현재 Unity와 동일한 2×2 진열형 구조를 사용한다.
- 아기띠·쪽쪽이·백색소음기·베이비 모니터를 카드 박스보다 소품 자체가 크게 보이게 배치한다.
- 이름과 선택 배지는 소품 아래, 효과 설명은 네 슬롯과 겹치지 않는 별도 패널에 표시한다.
- 실제 선택 가능 여부는 `V2NightFactory.IsSelectableItem()`과 `ToggleV2Item(ItemId)`를 따른다.
- `ItemId.Bouncer`는 V1 LEGACY이며 신규 화면에서 숨긴다.
- 암막 커튼 등 후속 후보는 `UNLOCK_CANDIDATE / NOT PLAYABLE` 그룹으로 분리한다.
- 안전·월령·제품 지침이 확정되기 전에는 수면 성공률, 모로반사 감소, 재입면 보너스를 표시하지 않는다.

## 7. PLAY 35장 재작성 목록

기존 프레임 번호를 유지하되 아래 QA 상태에 1:1로 다시 매핑한다. 실제 Figma node ID는 업데이트 후 별도 표에 기록한다.

| # | 권장 프레임명 | 핵심 상태/클릭 |
|---:|---|---|
| 01 | `M_PLAY_AWAKE_CALM` | 조용히 깨어 있음 |
| 02 | `M_WAKE_NEW_DIAGNOSE` | 새 `EncounterSequence`, 살펴보기 최초 선택 |
| 03 | `M_TAB_CARE_PERSIST` | 돌보기 클릭 후 같은 각성에서 유지 |
| 04 | `M_TAB_FEED_PERSIST` | 수유 준비 클릭 후 같은 각성에서 유지 |
| 05 | `M_DIAPER_CHECK_CLEAN` | 기저귀 배제, 오판 없음(P0-2) |
| 06 | `M_DIAPER_CHECK_WET` | 기저귀 원인 확인 |
| 07 | `M_HUNGER_EARLY` | 초기 배고픔 그림과 큰 상태 문장 |
| 08 | `M_HUNGER_LATE` | 후기 배고픔 그림과 큰 상태 문장 |
| 09 | `M_FUSS_SOFT` | 약하게 보챔 |
| 10 | `M_CRY_HARD` | 크게 울음 + 제한시간 |
| 11 | `M_DROWSY` | 졸림 단계 |
| 12 | `M_REM_ACTIVE` | 활동수면 애니메이션 |
| 13 | `M_NREM_DEEP` | 깊은 수면 애니메이션 |
| 14 | `M_LIMBS_RELAXED` | 팔다리 이완 확인 완료 |
| 15 | `M_LAYDOWN_SUCCESS` | 눕히기 성공 결과 |
| 16 | `M_MORO_STARTLE` | 모로반사 움찔 + 눕히기 실패 |
| 17 | `M_PACIFIER_ACCEPT` | 쪽쪽이 수용 |
| 18 | `M_PACIFIER_REJECT` | 쪽쪽이 거부 |
| 19 | `M_ENVIRONMENT_CHECK` | 온도·습도 확인 |
| 20 | `M_TEMPERATURE_ADJUST` | 온도 조절 결과(P0-1) |
| 21 | `M_HUMIDITY_ADJUST` | 습도 조절 결과(P0-1) |
| 22 | `M_NATURAL_CYCLE_STIR` | 자연 각성, 최소 개입 |
| 23 | `M_TIMEOUT` | 20초 만료 → Hesitate 1회 |
| 24 | `M_FEED_EMPTY` | 준비 전 수유 탭 |
| 25 | `M_FEED_SANITIZED` | 젖병 소독 완료 |
| 26 | `M_FEED_WATER_FORMULA` | 물·분유 준비 진행 |
| 27 | `M_FEED_MIXED` | 분유 혼합 완료 |
| 28 | `M_FEED_COOLED` | 식힘 완료 |
| 29 | `M_FEED_READY` | 온도 확인 및 수유 가능 |
| 30 | `M_FEED_COMPLETE` | 수유 완료, 준비 상태 소비(P0-4) |
| 31 | `M_SLEEP_FAST_FORWARD` | 수면 중 시간 보내기 |
| 32 | `M_WAKE_OVERLAY` | 새 각성 오버레이, 입력 잠금 |
| 33 | `M_DAWN_OVERLAY` | 밤 종료 오버레이 |
| 34 | `M_ITEM_SCROLL` | 2×2 진열형 소품·독립 설명 패널·선택 상태 |
| 35 | `M_UNLOCK_CANDIDATES` | 안전·월령 검토 전 후속 후보, 선택 불가 |

## 8. 현재 코드와 Figma 동기화 범위

- `CODE_SYNC_UNITY_PRESENTATION_V8`: 현재 세로 PLAY 좌표, 방 이동 알약, 상태·피드백·행동 덱.
- `CODE_SYNC_SETUP_PRESENTATION_V8`: 2×2 아이템 진열과 독립 설명 패널.
- `_ACTION_MOTION_SPEC_V8`: 기저귀 확인·갈기, 배고픔·이완 확인, 품에 안기,
  토닥이기, 쪽쪽이, 눕히기, 수유의 0%/50%/100% QA 키프레임.
- 안전·월령 조건이 있는 후속 해금 아이템만 `NOT PLAYABLE`로 유지한다.

Figma는 Core보다 앞서 성공·실패를 판정하지 않는다. 각 모션 프레임은 실행된 action outcome을
표현하는 QA 계약이며, 게임 수치나 조건을 새로 만들지 않는다.
