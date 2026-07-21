# NOT A NAP — 코드 기준 개발·기획 정합화 계획

> 상태: `CODE-VERIFIED / IMPLEMENTATION GAPS IDENTIFIED`
> 감사 기준: `main` / `f4c10fa` / 2026-07-21
> 목적: 피그마나 과거 문구를 구현 원본으로 삼지 않고, 현재 C# 코드와 승인된 통잠 기획의 차이를 개발 가능한 작업 단위로 고정한다.

## 1. 원본 우선순위

충돌 시 다음 순서로 판단한다.

1. 사용자가 승인한 제품 규칙: 통잠 루프, 반복 각성, 기저귀 우선, 신호 관찰, REM/NREM, 단계형 수유, 세 밤의 기억 변화
2. 이 문서의 코드 정합화 결정
3. `Assets/Scripts/Core`의 실제 타입과 테스트
4. `docs/storyboard-dev-spec.md`와 Figma — 화면 표현 참고
5. `Reference/prototype.html`과 V1 API — 호환용 LEGACY

현재 코드가 승인된 제품 규칙과 다르면 기획을 약화해 코드에 맞추지 않는다. 차이를 아래 P0 작업으로 고친다.

## 2. 현재 실제 플레이 가능 범위

현재 WebGL 셸은 다음 한 밤을 실행할 수 있다.

```text
TITLE
 → SETUP
 → PLAY (21:00~06:00, 분 단위, 반복 각성)
 → DIARY
 → 처음부터 다시
```

실제 진입점은 다음과 같다.

| 책임 | 실제 코드 |
|---|---|
| 런 시작 | `GameFlowController.StartGame()` → `GameSessionPresenter.StartRun()` |
| V2 밤 시작 | `GameFlowController.ConfirmV2Setup()` → `GameSessionPresenter.StartV2Night()` |
| V2 행동 | `GameFlowController.ActV2(V2ActionId)` → `ActionResolver.ApplyV2(...)` |
| 분 단위 진행 | `TurnResolver.AdvanceMinutes(...)` → `V2TimeResolver.Advance(...)` |
| 자는 시간 넘기기 | `GameFlowController.FastForwardV2Sleep()` |
| 밤 평가 | `NightEvaluationResolver.Evaluate(...)` |
| V2 일지 화면 데이터 | `GameSessionPresenter.BuildV2Diary()` |
| 실제 화면 셸 | `GameBootstrap`의 1920×1080 IMGUI |

## 3. 구현 상태 판정

### 3.1 구현되어 사용 가능한 것

| 기능 | 코드 근거 | 판정 |
|---|---|---|
| 21:00~06:00 분 단위 밤 | `V2NightState.ElapsedMinutes`, `V2TimeResolver.Advance` | 사용 가능 |
| 반복 각성 | `WakeScheduler`, `ScheduledWake`, `TriggerWake` | 기본 루프 사용 가능 |
| 연속/최장/총 수면 | `NightMetrics` | 사용 가능 |
| REM/NREM과 팔 이완 | `SleepCycleState`, `CheckLimbRelaxation` | 사용 가능 |
| 결정 제한시간 입력 | Presentation 타이머 → `V2ActionId.Hesitate` | 사용 가능, 기본 20초 |
| 기저귀·허기·환경 진단 타입 | `DiagnosisState`, `WakeCause`, 진단 행동 | 부분 사용 가능 |
| 단계형 분유 준비 | `FeedingPreparationState`, 6개 준비 단계 | 사용 가능, 소비/초기화 보완 필요 |
| 쪽쪽이 성향 | `BabyProfile.PacifierAffinity` | Core 판정 사용 가능 |
| 예방접종 배율 | `NightModifierId.Vaccination`, `NightModifierState` | Core 배율 존재, 화면 진입 연결 없음 |
| Bouncer 신규 선택 제외 | `V2NightFactory.IsSelectableItem` | 완료 |
| S~D 밤 등급 | `NightEvaluationResolver` | 사용 가능 |

### 3.2 타입은 있으나 게임에서 완성되지 않은 것

| 문제 | 현재 코드 상태 | 필요한 수정 |
|---|---|---|
| 피로 신호 | `ObservationSignalId`에 하품·눈비빔 등이 있으나 Resolver와 행동이 없음 | `FatigueSignalStage`와 관찰 결과 연결 |
| 온습도 | 기본값·조절량이 모두 0, 초기화·드리프트 없음 | 설정값 공급, 밤 시작 초기화, 시간 경과 갱신, 양방향 조절 |
| 원인 기반 각성 | 원인을 7종 균등 무작위 선택 | Hunger·환경·수면 단계·modifier를 반영한 가중치 판정 |
| 기저귀 우선 습관 | 실제 원인이 기저귀가 아니면 첫 기저귀 확인도 오판 처리 | 기저귀 확인을 무해한 1차 배제 검사로 변경 |
| 아이템 선택 | Carrier/Noise/Monitor는 V2 규칙에 효과가 없고 Pacifier도 소지 검사를 안 함 | 보유 아이템 기반 행동·정보·패시브 게이트 |
| 분유 준비 | 한 번 수유 후 준비 상태가 소비되지 않음, 안고 준비 상태가 계속 유지 | 수유 완료 시 병 상태와 `HoldWhilePreparing` 초기화 |
| 자기 재입면 | `TrySelfResettle`가 존재하지만 호출되지 않음 | 자연 각성 시 예약/판정 흐름에 연결 |
| 안전 지표 | `UnsafeChoiceCount`를 증가시키는 규칙이 없음 | 안전 선택을 넣지 않으면 지표 제거, 넣으면 명시적 규칙 추가 |
| 예방접종 밤 | 배율은 있으나 `GameBootstrap`에서 선택되지 않음 | 밤 데이터 또는 시나리오에서 modifier 주입 |

### 3.3 아직 연결되지 않은 제품 핵심

| 기능 | 현재 상태 |
|---|---|
| 첫째 밤 → 둘째 밤 → 백일째 밤 | `RunState.AdvanceNight()`은 있으나 V2 화면 흐름에서 호출되지 않음 |
| V2 행동 → Memory 변화 | `BuildV2Diary()`는 `MemoryConsolidator`를 호출하지 않음 |
| 다음 밤에 전날 습관이 규칙을 바꿈 | V2 Resolver가 일부 legacy memory를 읽지만 V2 밤 결과가 memory를 만들지 않음 |
| 백일밤 FINAL_INTRO | 화면 없음 |
| V2 결과 → 6개 Ending | `EndingResolver`는 있으나 V2 흐름에서 호출되지 않음 |
| 실제 다음 밤 버튼 | DIARY에는 “처음부터 다시 보기”만 있음 |

## 4. 기획 정정 결정

### 4.1 화면 수

35개의 Figma 프레임을 35개의 Unity Scene으로 만들지 않는다. 실제 화면 상태는 다음 6개다.

```text
TITLE / SETUP / PLAY / DIARY / FINAL_INTRO / ENDING
```

기저귀 확인, 수유 준비, 팔 이완 관찰, 눕히기 결과 등은 `PLAY` 내부 패널·모달·상태 변형이다. Figma 프레임 ID는 캡처와 QA 시나리오 ID로만 사용한다.

### 4.2 등급

내부 등급은 이미 구현·테스트된 `NightGrade.S/A/B/C/D`를 유지한다.

| 등급 | 최장 연속 수면 | 표시 의미 |
|---|---:|---|
| S | 300분 이상 + 안전 위반 0 | 통잠에 가까운 밤 |
| A | 240분 이상 | 통잠 성공 |
| B | 180분 이상 | 긴 한숨 |
| C | 120분 이상 | 조각잠 |
| D | 120분 미만 | 백야 |

과거 문서의 `ThroughTheNight/LongStretch/Fragmented/WhiteNight`는 별도 enum으로 만들지 않고 표시 문구로만 매핑한다.

### 4.3 제한시간

현재 기본값 `GameBalanceConfig.V2.DecisionSeconds = 20`을 첫 플레이테스트 기준으로 사용한다. 과거 문서의 8초·12초는 폐기한다. Presentation은 벽시계 만료 시 `V2ActionId.Hesitate`를 한 번만 전달한다.

### 4.4 기저귀 우선 규칙

승인된 규칙을 유지하고 Core를 다음처럼 수정한다.

```text
각성 인카운터의 첫 행동이 CheckDiaper
  원인=Diaper → 젖음 공개, ChangeDiaper 활성
  원인≠Diaper → Diaper 배제, 오판 없음

CheckDiaper 전에 돌봄/수유/눕히기
  SkippedDiaperFirst 기록
  원인=Diaper면 강한 페널티
  원인≠Diaper면 약한 페널티
```

따라서 `CorrectFirstChecks`는 “숨은 원인을 첫 번에 맞힘”이 아니라 “안전한 첫 확인을 수행함”으로 의미를 바꾸거나, 이름을 `SafeFirstCheckCount`로 마이그레이션한다.

### 4.5 아이템과 제품 기능

- `ItemId.Bouncer`는 LEGACY 역직렬화용으로만 유지하고 V2 신규 UI에서 계속 숨긴다.
- 안전성 검토 전 옆잠베개·수면 포지셔너·가중 이불을 성공률 버프로 추가하지 않는다.
- 분유제조기 효과는 우선 `ProductCapability.AutoFormulaPrep`로 구현한다. 브랜드는 Presentation 데이터이며 Core는 브랜드명을 모른다.
- Setup에서 고른 아이템은 반드시 실제 규칙 또는 정보 게이트 하나 이상과 연결되어야 한다. 효과 없는 선택 카드는 노출하지 않는다.

## 5. 다음 개발 순서

### P0-1 — Core 상태 정상화

대상:

- `GameBalanceConfig.V2`
- `V2NightFactory.Create`
- `V2TimeResolver.AdvanceContinuous`

작업:

1. 환경 권장 범위·초기값·조절 단위를 설정 데이터로 공급한다.
2. 시간 경과에 따라 Hunger와 환경이 결정론적으로 변한다.
3. `SleepMinuteGain` 등 현재 미사용 설정값을 연결하거나 제거한다.

완료 테스트:

- 새 밤의 온습도가 0/0이 아니다.
- 같은 seed·입력에서 환경과 Hunger 변화가 같다.
- 온도/습도 조절이 실제 값을 바꾸고 원인 해결 조건을 만족한다.

### P0-2 — 진단 규칙 수정

대상:

- `DiagnosisState`
- `V2ActionResolver.RegisterCheck`
- `NightMetrics`

작업:

1. 기저귀 첫 확인을 무해한 배제 검사로 만든다.
2. 기저귀를 건너뛴 첫 개입을 별도 오판으로 기록한다.
3. 피로 단계와 책의 피로 신호를 구조화된 결과로 반환한다.
4. 원인이 해결되기 전 부적절한 돌봄은 원인을 자동 해결하지 않는다.

완료 테스트:

- Hunger 원인에서도 첫 `CheckDiaper`는 오판 0.
- 기저귀 원인에서 첫 행동이 Hold면 강한 페널티.
- 하품·눈비빔·등 젖힘 단계가 코드 결과로 반환된다.

### P0-3 — 각성 스케줄러를 상태 기반으로 교체

대상:

- `WakeScheduler.Schedule`
- 필요 시 `WakeCauseResolver` 신규 추가

작업:

1. Hunger 임계, 환경 이탈, REM/Moro, 자연 수면 주기, modifier를 후보 가중치로 계산한다.
2. 후보 0개일 때만 `NaturalCycle`을 기본값으로 쓴다.
3. `TrySelfResettle`을 자연 각성 흐름에 연결한다.
4. 동일 seed·동일 상태·동일 입력 순서의 결정론을 유지한다.

### P0-4 — 수유·쪽쪽이·장비 계약 완성

대상:

- `V2ActionResolver`
- `V2NightFactory.SelectableItems`
- `GameSessionPresenter.BuildV2Play`

작업:

1. Pacifier 미소지 시 행동을 거부한다.
2. Carrier/Noise/Monitor는 V2 효과를 연결하거나 선택 목록에서 임시 제거한다.
3. 수유 완료 후 사용한 준비 상태를 초기화한다.
4. `ProductCapability`를 Setup/시나리오 데이터에서 전달한다.
5. Presentation의 버튼 `Enabled`를 선행조건에 맞춰 계산한다.

### P0-5 — 세 밤과 기억 연결

대상:

- `GameSessionPresenter.BuildV2Diary`
- `GameFlowController`
- `GameBootstrap.DrawDiary`
- `MemoryConsolidator` 또는 V2 전용 adapter

작업:

1. V2 밤 결과를 `NightResult`와 Memory/Trace로 정확히 한 번 확정한다.
2. 첫째·둘째 밤 일지의 버튼을 다음 `SETUP`으로 연결한다.
3. `RunState.AdvanceNight()`를 한 번만 호출한다.
4. 백일째 밤 전 `FINAL_INTRO`, 종료 후 `EndingResolver`를 연결한다.
5. 전날 행동이 다음 밤의 실제 확률 또는 행동 효과를 바꾸는 before/after를 UI에 표시한다.

완료 시 실제 흐름:

```text
TITLE
 → SETUP_1 → PLAY_1 → DIARY_1
 → SETUP_2 → PLAY_2 → DIARY_2
 → FINAL_INTRO → SETUP_100 → PLAY_100 → DIARY_100
 → ENDING
```

### P1 — Presentation 품질과 정서 카피

Core 계약이 고정된 뒤 진행한다.

1. `GameBootstrap` IMGUI를 유지하더라도 화면 상태를 컴포넌트 단위로 분리한다.
2. 아기 신호 애니메이션은 `ObservationSignalId`에서만 파생한다.
3. 아빠의 독백은 짧고 구체적으로 쓴다. 기술 용어와 판정 수치는 플레이어 대사에 넣지 않는다.
4. 큰 오버레이는 각성·눕히기·아침처럼 입력 흐름이 바뀌는 사건에만 사용한다.
5. Figma 35장은 이 흐름의 QA 캡처 목록으로 다시 매핑한다.

## 6. 지금 만들면 안 되는 것

- 피그마 35장을 각각 Unity Scene으로 제작
- V1 `GameAction`을 기준으로 새 V2 화면 확장
- Core 결과 없이 화면에서 성공·실패·수치를 직접 계산
- 실제 브랜드명이나 광고 SDK를 Core에 추가
- 옆잠베개·가중 이불 등 안전성 미확정 제품을 수면 성공 버프로 추가
- 쌍둥이·보호자 다중 선택·백일 이후 챕터를 P0보다 먼저 구현

## 7. Definition of Done

수직 슬라이스가 완료됐다고 말하려면 다음을 모두 만족해야 한다.

- 한 번 눕힌 뒤에도 아기가 다시 깬다.
- 21:00~06:00 사이에 최소 두 번 이상의 상태 기반 각성이 재현 가능하다.
- 기저귀 첫 확인을 건너뛰면 실제 페널티가 발생한다.
- 배고픔과 피로 신호가 화면 문구가 아니라 Core 결과에서 나온다.
- REM에서 성급히 눕힐 때와 NREM·이완 확인 후 눕힐 때 결과 차이가 있다.
- 준비하지 않은 분유는 수유할 수 없고, 자동 준비 capability가 단계를 줄인다.
- 쪽쪽이 수용/거부와 소지 여부가 모두 판정에 반영된다.
- 첫째 밤의 반복 행동이 둘째 밤 규칙을 실제로 바꾼다.
- 백일째 밤 뒤 `EndingResolver`가 반환한 6개 엔딩 중 하나가 표시된다.
- 동일 seed와 동일 입력 시퀀스가 동일 결과를 낸다.
- Unity EditMode 전체 테스트와 WebGL 실제 플레이를 모두 검증한다.
