# NOT A NAP — 모바일 Figma 전면 개편 핸드오프

> 기준: 2026-07-21 로컬 diff
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
| 탭 클릭 | `GameBootstrap.DrawTab()` | 같은 각성 안에서는 사용자가 선택한 탭을 계속 유지 |
| 아기 비주얼 | `GameBootstrap.DrawBabyStateVisual()` | `V2PlayViewModel` 값만 읽고 화면에서 판정하지 않음 |
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
아기 그림과 큰 상태 문장
연속 수면 / 보호자 체력
현재 사건·관찰 결과
살펴보기 / 돌보기 / 수유 준비
현재 탭의 행동 버튼
```

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
| 살펴보기 | `BTN_CHECK_RELAXATION` | `CheckLimbRelaxation` |
| 살펴보기 | `BTN_HESITATE` | `Hesitate` |
| 돌보기 | `BTN_HOLD` | `Hold` |
| 돌보기 | `BTN_PAT` | `Pat` |
| 돌보기 | `BTN_PACIFIER` | `Pacifier` |
| 돌보기 | `BTN_LAYDOWN` | `Laydown` |
| 돌보기 | `BTN_CHANGE_DIAPER` | `ChangeDiaper` |
| 돌보기 | `BTN_ADJUST_TEMPERATURE` | `AdjustTemperature` |
| 돌보기 | `BTN_ADJUST_HUMIDITY` | `AdjustHumidity` |
| 수유 준비 | `BTN_SANITIZE_BOTTLE` | `SterilizeBottle` |
| 수유 준비 | `BTN_PREPARE_WATER` | `PrepareWater` |
| 수유 준비 | `BTN_MEASURE_FORMULA` | `MeasureFormula` |
| 수유 준비 | `BTN_MIX_FORMULA` | `MixFormula` |
| 수유 준비 | `BTN_COOL_BOTTLE` | `CoolBottle` |
| 수유 준비 | `BTN_CHECK_BOTTLE_TEMP` | `CheckBottleTemperature` |
| 수유 준비 | `BTN_HOLD_WHILE_PREPARING` | `HoldWhilePreparing` |
| 수유 준비 | `BTN_FEED_PREPARED` | `FeedPreparedBottle` |

버튼 활성 조건의 최종 원본은 P0-4 이후 `GameSessionPresenter.BuildV2Play()`이다. 현재 모든 V2 버튼이 밤 종료 전 활성화되는 구현을 Figma의 최종 계약으로 복제하지 않는다.

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

- SETUP은 고정 2×2 종결 화면이 아니라 세로 스크롤 카드 목록 또는 가로 스냅 카드 구조로 확장한다.
- 카드에는 이름, 핵심 효과 한 문장, 주의점, 선택 상태, 잠금 상태를 큰 글자로 표시한다.
- 실제 선택 가능 여부는 `V2NightFactory.IsSelectableItem()`과 P0-4 아이템 계약을 따른다.
- 효과가 연결되지 않은 Carrier/Noise/Monitor를 최종 카드로 확정하지 않는다. P0-4에서 효과 연결 또는 임시 제거를 먼저 결정한다.
- `ItemId.Bouncer`는 V1 LEGACY이며 신규 카드에서 숨긴다.
- 옆잠베개·수면 포지셔너·토닥이인형은 `UNLOCK_CANDIDATE / NOT PLAYABLE` 그룹으로 분리한다.
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
| 34 | `M_ITEM_SCROLL` | 확장 카드·스크롤·선택 상태 |
| 35 | `M_UNLOCK_CANDIDATES` | 안전·월령 검토 전 후속 후보, 선택 불가 |

## 8. 현재 diff와 Figma의 차이

### 이미 코드에 반영됨

- 1080×1920 세로 PLAY/TITLE/SETUP/DIARY 배치.
- 글자와 버튼 확대.
- 새 각성에서만 살펴보기 탭 초기화.
- 같은 각성에서 돌보기·수유 준비 탭 유지.
- Awake/수면/큰 울음의 기본 얼굴 구분.

### Figma에는 그리되 `P0 연결 전` 표기 필요

- 약한 보챔 전용 그림.
- 배고픔 초기/후기 지속 비주얼.
- REM/NREM/이완 애니메이션.
- 모로반사 결과 애니메이션.
- 쪽쪽이 수용/거부 결과 애니메이션.
- 실제 선행조건에 따른 버튼 활성/비활성.
- 안전·월령 조건이 있는 후속 해금 아이템.

위 항목은 화면이 Core보다 앞서 판정을 만들어내지 않도록 Figma annotation에 연결 예정 Core 필드를 반드시 적는다.
