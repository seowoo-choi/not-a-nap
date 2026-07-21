# NOT A NAP — 수직 슬라이스 재설계 스펙 (통잠 루프)

> 문서 위상: **게임 코어 메커니즘 재설계.** 사용자 지시(2026-07-18)로 승인됨.
> 목표: 해커톤 제출용 첫 완성 구간이되, **버리는 프로토타입이 아니라 정식 제품 구조 위**에 만든다.
> 이 문서는 `Reference/prototype.html`의 "재우면 성공" 모델을 **통잠 루프**로 대체한다.
> 수치는 결정론(시드 기반)으로 구현하며, 아래 값은 초기 튜닝값(조정 가능)이다. 판정은 전부 C# Core.
> 이 문서가 코어 메커니즘 원본이고, `docs/storyboard-dev-spec.md`(화면)는 이 문서 확정 후 갱신한다.

관련: [feedback-log.md](feedback-log.md)(출처), [final-night-spec.md](final-night-spec.md)(백일밤은 이후 챕터로 보류).

---

## 0. 이번 빌드 범위 (사용자 확정 10 + 확장)

**반드시 포함:**
1. 통잠 루프 — 21:00~06:00 재각성 반복 (한 번 재움 ≠ 성공)
2. 최장 연속 수면 시간으로 밤 결과 평가
3. 울음 직후 **제한시간** 있는 원인 판단
4. 기저귀 우선 확인 + 오판 페널티
5. 배고픔·피로 **신호 관찰** (책 표1/표3)
6. REM/NREM + **팔 이완 확인** 후 눕히기
7. 단계형 **새벽 수유 준비**
8. 아기별 **쪽쪽이 수용/거부**
9. **온도·습도** 확인
10. Bouncer 신규 UI 제거 + LEGACY 호환 유지

**구조만 만들고 콘텐츠 1종:** `NightModifier` 확장 구조 + **예방접종 1종** 실구현. (원더윅스·이앓이·수면퇴행은 구조만)

**후속 제품 범위(확장점만):** 쌍둥이, 캐릭터 선택 UI, 실제 브랜드/광고 SDK, 백일 이후 챕터.
→ 확장점 타입: `BabyProfile`, `CaregiverProfile`, `ProductCapability`, `FeatureAvailability`, `NightModifier`.

> CLAUDE.md 스코프락(행동 5개·아이템 5개·밤 3회 등)은 이 지시로 **부분 상향**된다. CLAUDE.md 갱신은 별도 확인 후.

---

## 1. 핵심 루프 재정의 — 통잠

### 1.1 시간 모델
- 밤 = 21:00(=0분) → 06:00(=**540분**). 내부는 **분 단위** 정수 시계 `NightClockMinutes`.
- 아기는 **수면 사이클**을 돈다. 영아 사이클 ≈ **45분**(`SleepCycleMinutes`, 튜닝).
- 아기가 **자는 동안 시간은 블록으로 자동 진행**(빨리감기)되며 연속 수면이 쌓인다.
- **각성 이벤트**가 뜨면 자동 진행이 멈추고 **인카운터(Encounter)**가 열린다 → 플레이어가 개입.

### 1.2 연속 수면(streak) — 결과 지표
- `CurrentSleepStreakMin`: 지금 끊기지 않고 이어진 수면 분. **완전 각성(울음/Full Wake)** 시 0으로 리셋.
- `LongestSleepStreakMin`: 밤 전체 최댓값. **이게 밤 결과 평가의 1순위 지표.**
- 선잠(REM) 표면화로 잠깐 뒤척이는 건 리셋 아님. **Full Wake만 리셋.**

### 1.3 밤 결과 등급 (`NightSleepGrade`)
| 등급 | LongestSleepStreakMin | 의미 |
|---|---|---|
| `ThroughTheNight` | ≥ 240 (4h) | 통잠 성공 |
| `LongStretch` | 150–239 | 긴 한숨 |
| `Fragmented` | 60–149 | 조각잠 |
| `WhiteNight` | < 60 | 백야(거의 못 잼) |

### 1.4 승리 조건 재정의 (백일밤 3중2 대체 예정, 이번엔 일반 밤 평가)
아침 6시 기준 아래로 밤을 평가(엔딩 연동):
1. `NightSleepGrade ≥ LongStretch` (통잠 지향)
2. 보호자 체력 ≥ 30
3. **진단 정확도**: 그 밤 각성 인카운터에서 **오판 0~1회** (관찰 육아 보상)
→ 3개 중 2개 이상 = 그 밤 "좋은 밤". 백일밤 최종 승리는 후속.

---

## 2. 상태 모델 변경

### 2.1 `BabyState` 확장
기존 `Calm/Sleep/Hunger/Held/Crying` 유지 + 추가:
```
SleepPhase Phase           // Awake / Drowsy(기면) / REM(활동수면) / NREM(비활동수면)
bool ArmRelaxed            // NREM 관찰 신호 (단독으로 성공 확정 아님)
bool BreathingRegular      // NREM 관찰 신호 (규칙적 호흡)
int CurrentSleepStreakMin
int LongestSleepStreakMin
WakeCause PendingCause      // 인카운터가 열렸을 때 숨은 원인 (플레이어에겐 미공개)
SignalStage HungerSignal    // None/Early/Mid/Late (책 표1)
SignalStage FatigueSignal   // None/Early/Mid/Late (책 표3)
```
- `SleepPhase`: `Sleep` 수치 + 사이클 진행으로 파생. Drowsy(잠들기 직전)→REM→NREM→REM→(각성 또는 NREM 유지).
- `ArmRelaxed`·`BreathingRegular`: NREM에서 참이 되는 **관찰 신호**. 눕히기 성공은 이 신호들을 관찰로 확인했을 때 보너스를 주되, 단독 신호로 성공을 확정하지 않는다(§4).

### 2.2 `EnvironmentState` (신규)
```
int TempC        // 방 온도, 최적 20~22
int HumidityPct  // 습도, 최적 40~60
```
밤 동안 서서히 드리프트. 최적 밴드 이탈이 커지면 각성 원인 후보가 됨.

### 2.3 `WakeCause` (신규 enum)
`SleepCycleStir`(원인 없는 선잠 각성) / `Diaper` / `Hunger` / `Overtired`(과각성) / `Environment` / `Moro`(놀람반사).

### 2.4 `SignalStage` (신규 enum)
`None / Early / Mid / Late`. 책 신호 표의 단계. Late일수록 이미 늦어 대응 난이도↑.

---

## 3. 각성 인카운터 & 원인 진단 (요구 3·4·5)

### 3.1 각성 트리거 (자동 진행 중 결정론적 판정)
사이클 경계(≈45분)마다, 그리고 임계 초과 시 각성 판정. 우선순위·확률:
1. **Hunger** — `Hunger ≥ 78` → 각성. (배고픔 신호는 각성 전부터 Early→Mid로 미리 노출)
2. **Environment** — 온·습도 최적 이탈폭 큼 → 확률 각성.
3. **Moro** — 초저녁/얕은 잠 + 무자극 낙하(눕히기 직후 등)에서 확률. Moro는 안기·낮은 자극·일정한 토닥임 등 **현재 구현된 안전한 행동**으로 완화한다. 새로운 수면 제품의 효과는 안전성 검토 전 확정하지 않는다.
4. **Diaper** — 시간 경과 누적 → 확률(밤 1~2회).
5. **Overtired** — 피로 신호 Late 방치 시 과각성 → 잘 안 잠.
6. **SleepCycleStir** — 위 원인 없을 때 REM 표면화. 개입 안 해도 재입면 가능(기질·습관 selfSoothe).

각 트리거는 `PendingCause`를 세팅하고 인카운터를 연다. **원인은 숨김.** 플레이어는 신호로 추론.

### 3.2 제한시간 (요구 3)
- 인카운터가 열리면 UI에 **초읽기**(예: `EncounterTimerSec = 8`) 시작. 실시간.
- 시간 내 미해결 → Core에 `EncounterAction.Timeout` 전달 → **각성 심화**(Calm 큰 폭 하락, Crying=대성통곡, streak는 이미 리셋, 해결 난이도↑).
- **결정론 보장**: 타이머는 Presentation. Core는 벽시계를 읽지 않고, UI가 만료를 `Timeout`이라는 **이산 입력**으로 넘긴다. 같은 입력 시퀀스 → 같은 결과.

### 3.3 기저귀 우선 + 오판 (요구 4)
인카운터에서 첫 개입 규칙(언니 도메인 지식):
- **정석**: 첫 행동으로 `CheckDiaper`(짧은 시간, 무해). 결과:
  - 젖음 → 원인 공개(Diaper) → `ChangeDiaper`로 해결.
  - 안 젖음 → 기저귀 배제, **신호 관찰**로 다음 원인 좁히기.
- **오판**: `CheckDiaper` 없이 다른 개입(안기·수유·눕히기 등)을 첫 행동으로 하면 → `MisjudgeFlag` 기록 + 페널티(원인이 실제로 기저귀였다면 강한 페널티, 아니면 약한 페널티).
  - 페널티: Calm −(8~15), 시간 +(5~8분), 각성 심화 가능, `NightStats.Misjudgments++`.
- 이 규칙으로 "울면 기저귀부터"가 **학습되는 습관**이 된다(오판이 반복되면 힌트 강조).

### 3.4 신호 관찰 (요구 5, 책 표1·표3)
- `Observe`(관찰하기) 행동: 시간 소량, 현재 `HungerSignal`/`FatigueSignal` 단계를 **텍스트 신호로 공개**.
- **배고픔 신호(표1)**: Early(입맛 다심·손 빪) → Mid(가슴 파고듦·수유자세·꼼지락) → Late(머리 좌우·울음). Late면 이미 늦음.
- **피로 신호(표3)**: Early(눈썹 붉음·눈 피함·멍) → Mid(하품·눈비빔·귀당김·안아달라) → Late(심하게 움·등 활·주먹) → 극도.
- 신호 단계는 원인·경과에 따라 결정론적으로 산출. **관찰 잘하면 울기 전에 대응**(과각성/배고픔 각성 예방).

### 3.5 원인별 올바른 해결 (요약 표)
| WakeCause | 신호 힌트 | 올바른 행동 | 오답 예 |
|---|---|---|---|
| Diaper | (관찰로 안 보임 → CheckDiaper로만) | CheckDiaper→ChangeDiaper | 안기/수유 |
| Hunger | HungerSignal Mid/Late | (새벽수유 준비→)Feed | 토닥/눕히기 |
| Overtired | FatigueSignal Late | 안기+토닥로 진정, 자극 차단 | 놀아주기/수유 |
| Environment | 온습도 표시 이탈 | 온도/습도 조절 | 안기만 |
| Moro | 갑작스런 움찔 | 안기·낮은 자극·일정한 토닥임(현재 구현된 안전 행동) | 큰 개입 |
| SleepCycleStir | 약한 뒤척임 | Watch(개입 최소) | 안아서 깨우기 |

---

## 4. 눕히기 재설계 — REM/NREM + 팔 이완 (요구 6)

- **깊은 수면 관찰 신호**: `DeepSleepObserved = Phase == NREM && ArmRelaxed && BreathingRegular`.
  `Observe`로 이 세 신호(NREM·팔 이완·규칙적 호흡)를 확인하면 "팔에 힘이 빠지고 숨이 고르다" 노출(언니 + 책 도메인).
- 눕히기 성공률:
  - `DeepSleepObserved`(세 신호 모두) → 높은 성공(기본 0.85). **`ArmRelaxed` 단독으로 90% 성공을 확정하지 않는다.**
  - `Phase == NREM`이지만 일부 신호만 → 중간(기본 0.55~0.6).
  - `Phase == REM` → 낮음(기본 0.25) + Moro 위험 (등 닿는 순간 놀람반사 = 실패·각성).
  - `Drowsy/Awake` → 안고 있지 않으면 눕히기 무의미(거부).
  - 기존 습관 페널티(HeldDep·Carrier) 유지: `− HeldDep*0.45 − Carrier*0.20`.
- 실패 = Moro/센서 발동 → Full Wake → streak 리셋. **선잠에 성급히 눕히면 통잠 깨짐**을 규칙으로 체감.

---

## 5. 새벽 수유 준비 — 단계형 (요구 7)

새벽(예: 02:00~05:00) `Hunger` 각성 시 Feed는 **즉시가 아니라 준비 시퀀스**:
```
1) 소독 상태 확인   — 사전 소독 되어 있으면 skip, 아니면 +시간 & 아기 각성 심화
2) 물 끓이기        — +시간
3) 식히기          — BottleTemperatureState가 허용 범위에 들 때까지 진행
4) 분유 타기        — 계량
5) 수유            — Feed 판정 (기존 Hunger≥45 성공 로직 재사용)
```
> 온도 규칙: 허용 범위는 `FeedingSafetyConfig` 또는 제품 데이터에서 읽는다. **Core·화면 문구에 40℃ 같은 고정값을 넣지 않는다.** 게임이 임의로 하나의 의료 기준을 정하지 않고, 제품 지침·아기 조건을 설정 데이터로 받는다.
- 각 단계 시간 비용. **준비 안 된 상태로 시작하면** 아기가 우는 동안 시간이 흐르며 각성 심화(현실 반영).
- 아이템 **분유제조기(`FormulaMaker`)** 보유 시 1~3단계를 1스텝으로 단축.
- 결정론: 각 단계는 이산 액션. 온도는 정수 상태값.

---

## 6. 아이템 변경 (요구 8·10)

### 6.1 제거/교체 (안전성 우선)
- Bouncer는 신규 UI에서 제거하고 LEGACY 호환만 유지한다. (`ItemId.Bouncer` + 기존 Resolver 유지, 선택 목록 비노출)
- FormulaMaker는 새벽 수유 준비 단축 장비로 구현한다.
- Carrier, Pacifier, Noise, Monitor는 유지한다.
- **SideSleepPillow, MilletBlanket, PattingDoll은 수면 성공 아이템으로 구현하지 않는다.**
- 해당 제품은 안전성 검토 전 **선택 목록·광고 보상·수면 확률 계산에서 제외한다.**
- 추후 검토된 제품은 ProductCapability 기반으로 별도 추가한다.

> 근거: 영아 수면 공간에서는 베개·부드럽거나 느슨한 물건을 피하도록 권고된다(미국소아과학회 SIDS/수면환경 지침).
> 따라서 옆잠베개·좁쌀/가중 이불·침대 안 토닥인형에 Moro 감소나 재입면 보너스를 부여하지 않는다.

### 6.2 쪽쪽이 아기별 수용 (요구 8)
- `BabyProfile.PacifierResponse ∈ { Accepts, Refuses, Neutral }`.
  - `Accepts`(예: 이솜이): 쪽쪽이 진정 효과 신뢰도↑, 대신 후속 "쪽쪽이 떼기" 부채 플래그.
  - `Refuses`(예: 아수): 쪽쪽이 대개 뱉음(효과 거의 없음) → 다른 전략 필요.
- 기존 Pacifier 로직에 프로필 게이트 추가(15% 뱉음 → 프로필별로 조정).

---

## 7. 온도·습도 (요구 9)

- `EnvironmentState`(§2.2) 밤 동안 드리프트(±). 최적 밴드: 온도 20~22℃, 습도 40~60%.
- 이탈폭 누적 → `Environment` 각성 확률↑. 관찰 시 화면에 수치/게이지 노출(Monitor 없이도 기본 표시할지, 아이템 게이팅할지는 화면 스펙에서).
- 조절 행동: `AdjustTemperature`(±), `AdjustHumidity`(±). 시간 소량.

---

## 8. NightModifier 구조 + 예방접종 1종 (요구 확장)

### 8.1 `NightModifier` (확장점)
한 밤에 적용되어 파라미터를 보정하는 인터페이스:
```
interface INightModifier {
  string Id { get; }
  void OnNightStart(RunState run, NightState night);      // 초기 상태 보정
  void OnCycleTick(RunState run, NightState night);       // 사이클마다 각성 확률/허기율 등 보정
}
```
- 밤은 `IReadOnlyList<INightModifier> Modifiers`를 가짐. 없으면 평범한 밤.
- 후속(원더윅스·이앓이·수면퇴행)은 이 인터페이스 구현만 추가하면 됨. **이번엔 구조 + 1종만.**

### 8.2 예방접종 모디파이어 `VaccinationNightModifier` (실구현)
- 그날 낮 예방접종 → 밤:
  - `OnNightStart`: 미열 → 최적 온도 민감↑, Calm 시작치 하락.
  - `OnCycleTick`: 각성 확률 +, 재입면 난이도 +(보챔↑), Overtired 도달 빠름.
- 확률·수치는 튜닝값. 결정론 유지.

---

## 9. 확장 지점 아키텍처 (Core 재작성 방지)

이번에 **껍데기 타입 + 연결점**만 만들어 후속 제품이 Core를 안 뜯게 한다.

| 타입 | 이번 빌드 | 후속 확장 |
|---|---|---|
| `BabyProfile` | Temperament + PacifierResponse (+ 성별 필드 존재만) | 성별/유형/개별 아기(이솜·아수) |
| `CaregiverProfile` | 기본 "아빠(남)" 1종 | 여/조부모/형제, 다중 보호자 |
| `ProductCapability` | 실제 장비 능력만(아래 목록) | 검토된 신규 제품 추가 |
| `FeatureAvailability` | 출시 플래그(아래 목록), 이번엔 전부 off | 기능 켜기 |
| `INightModifier` | 예방접종 1종 | 원더윅스/이앓이/수면퇴행 |

**ProductCapability (실제 장비 능력 — Core 판정이 읽는 값):**
- `AutoFormulaPrep` / `PreSanitizedBottle` / `TemperatureControl` / `WhiteNoise` / `EnvironmentMonitoring`

**FeatureAvailability (기능 출시 플래그 — 제품 출시 여부 제어):**
- `AdsEnabled` / `BrandPartnershipEnabled` / `TwinsModeEnabled` / `PostHundredChapterEnabled` / `CaregiverSelectionEnabled`

- **Core 판정은 브랜드명이 아니라 `ProductCapability`를 읽고, 제품 출시 여부는 `FeatureAvailability`가 제어한다.** (`Twins`·`PostHundredChapter`는 장비 능력이 아니라 기능 플래그다.)
- `RunState`에 `BabyProfile`, `CaregiverProfile`, `ProductCapability`, `FeatureAvailability` 참조 추가.
- Core는 해당 `ProductCapability`가 없으면 관련 경로를 타지 않음(게이트).

---

## 10. 결과 평가 & 엔딩 연동 (요구 2)

- 밤 종료 시 `NightSleepGrade`(§1.3) + 체력 + 진단정확도로 그 밤 평가.
- 기존 `EndingId` 6종은 **의미 유지하되 판정 입력을 streak 기반으로 교체**:
  - 실패(아침이 이겼다) ↔ `WhiteNight`.
  - 통잠+저의존 ↔ 우리 집의 루틴. 등.
- `VictoryResolver`/`EndingResolver`의 입력만 교체(§1.4), 구조 유지.

---

## 11. 화면(스토리보드) 영향 요약

`PLAY` 화면이 **관리형 → 통잠 진단 루프형**으로 바뀐다. 갱신 필요 포인트:
- 상단: 시계(분 단위) + **현재 연속수면 / 최장 연속수면** 게이지 + 체력 + 온습도.
- 자는 동안: **빨리감기 연출**(시간 흐름 + streak 증가).
- 각성 시: **인카운터 오버레이** — 초읽기 타이머 + [기저귀 확인][관찰][안기][토닥][수유(준비)][온습도][눕히기][쪽쪽이]...
- 신호 표시: 관찰 시 배고픔/피로 신호 단계 텍스트.
- 눕히기: "팔 힘 빠짐(NREM)" 확인 UI.
- 새벽수유: 단계형 서브 플로우 화면.
→ `docs/storyboard-dev-spec.md`는 이 스펙 확정 후 갱신.

---

## 12. 빌드 순서 (Core 먼저, 테스트 가능)

Core는 UnityEngine 의존성을 최소화한 순수 C# 구조로 유지한다. Unity EditMode 테스트는 **Unity Test Runner로 실행**한다. 별도 .NET 테스트 프로젝트가 존재하거나 새로 구성된 경우에만 Unity 없이 테스트할 수 있다. **정적 검토를 실제 테스트 통과로 표현하지 않는다.** 권장 순서:

- **P1 상태·타임모델**: 분 단위 시계, SleepPhase, streak, EnvironmentState, 프로필/Capability/Modifier 껍데기. + 결정론 테스트.
- **P2 통잠 루프**: 사이클 자동진행 + 각성 트리거 + streak 리셋/기록 + 등급. + 테스트.
- **P3 진단 인카운터**: WakeCause, CheckDiaper 우선/오판, Observe/신호, 원인별 해결, Timeout. + 테스트.
- **P4 눕히기 재설계** REM/NREM/ArmRelaxed. + 테스트.
- **P5 새벽수유 단계** + FormulaMaker. + 테스트.
- **P6 아이템 교체**(Bouncer LEGACY, 신규 4종), 쪽쪽이 프로필. + 테스트.
- **P7 예방접종 NightModifier**. + 테스트.
- **P8 결과/엔딩 입력 교체**. + 테스트.
- **P9 Presentation**: PLAY 인카운터 UI, 스토리보드 갱신.

각 P는 기존 결정론 테스트(현재 54개) 유지 + 신규 테스트 추가.

---

## 13. 미해결·확인 필요 (구현 전 정할 것)

- 수면 사이클 45분/각성 확률/streak 등급 경계 = 초기 튜닝값. 플레이테스트로 조정.
- 제한시간(8초) 값과 Timeout 페널티 강도.
- 온습도 게이지를 기본 노출 vs Monitor 게이팅.
- CLAUDE.md 스코프락 문구 갱신 여부(행동/아이템 수 상향 반영).
