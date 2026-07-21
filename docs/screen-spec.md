# NOT A NAP — 화면 개발 명세 (Screen Development Spec)

> 상태: `V1 LEGACY / CURRENT V2 IMPLEMENTATION SOURCE 아님`
>
> 이 문서는 1시간 턴과 V1 `GameAction`을 설명한다. 2026-07-21 기준 V2에는 `CheckDiaper`와 분 단위 통잠 루프가 실제로 존재하므로,
> 본문의 “그런 행동 없음” 같은 문장을 현재 사실로 사용하지 않는다. 현재 개발 순서는
> [`code-first-development-plan.md`](code-first-development-plan.md), 제품 의도는
> [`vertical-slice-spec.md`](vertical-slice-spec.md)를 따른다.

이 문서는 **개발자가 보고 바로 구현할 수 있는** 화면 명세다. Figma 스토리보드(와이어프레임)를 대체한다.
Figma는 "그림 참고"로만 쓰고, **구현의 원본은 이 문서 + 실제 Core 코드**다.

- 게임 판정·수치·확률은 전부 `Assets/Scripts/Core`의 결정론적 C# 엔진이 담당한다. 이 문서는 그 엔진을 **화면에 어떻게 연결하는지**만 정의한다.
- 수치 원본: `Reference/prototype.html`(1~2일차) + `docs/final-night-spec.md`(백일째 밤). **이 문서는 그 수치를 재서술할 뿐, 새 수치를 만들지 않는다.** 충돌 시 코드가 우선.
- 여기 나오는 CMD/함수 이름은 전부 **실제 존재하는 Core API**다. 가짜 명령(CMD_* 등)은 쓰지 않는다.

---

## 0. 이 문서 읽는 법

각 화면은 세 층으로 적는다. 위에서부터 읽으면 기획자도 이해되고, 접힌 부분을 펴면 개발자가 그대로 구현한다.

1. **보이는 것** — 화면에 뭐가 나오나
2. **누르면 무슨 일이 일어나나** — 버튼별로 *쉬운 말*로. 기술 용어 금지.
3. **▸ 개발 계약(접힘)** — 실제 Core API 호출, 정확한 수치 변화, 활성/비활성 조건, 완료 조건

> 중요한 개념 하나: **화면은 42장이 아니다.** 실제로 필요한 건 아래 "전체 화면 지도"의 몇 장뿐이고,
> 나머지는 전부 **하나의 게임 화면 안에서 상태·문구·오버레이만 바뀌는 것**이다.
> "안기 실패", "수유 거부" 같은 건 별도 화면이 아니라 **게임 화면 위에 뜨는 결과 오버레이**다.

---

## 1. 전체 화면 지도 (실제 최소 세트)

| ID | 화면 | 종류 | 다음 |
|----|------|------|------|
| `TITLE` | 타이틀 | 정적 | → `SETUP` |
| `SETUP` | 밤 시작: 기질 힌트 + 아이템 고르기 | 정적 | → `PLAY` |
| `PLAY` | **밤 플레이 (핵심 화면)** | 상태형 | 밤 종료 시 → `DIARY` |
| `PLAY` 위 오버레이 | 결과/돌발 이벤트 팝업 | 오버레이 | 닫으면 `PLAY`로 복귀 |
| `DIARY` | 육아일지 + 습관 카드 | 정적 | 다음 밤 있으면 → `SETUP`, 없으면(백일밤 후) → `ENDING` |
| `FINAL_INTRO` | 백일째 밤 인트로(습관 요약) | 정적 | → `SETUP`(슬롯 2개 버전) |
| `ENDING` | 엔딩 6종 | 정적 | → `TITLE` |

**전체 흐름:**

```
TITLE
 → SETUP(1일차)  → PLAY(1일차) → DIARY(1일차)
 → SETUP(2일차)  → PLAY(2일차) → DIARY(2일차)
 → FINAL_INTRO   → SETUP(백일밤, 슬롯2·할머니금지) → PLAY(백일밤) → DIARY(백일밤)
 → ENDING
```

`PLAY` 화면은 세 밤 모두 **같은 화면**이다. 밤마다 바뀌는 건 아이템 슬롯 수, 돌발 이벤트, 승리 판정뿐이며 이건 전부 Core가 처리한다.

---

## 2. Core API 계약 (Presentation ↔ GameCore)

Presentation(Unity UI/MonoBehaviour)이 호출할 **실제 API**. 이 목록에 없는 명령은 만들지 마라.

### 상태를 만들고 진행하는 함수

| 목적 | 실제 호출 | 반환/효과 |
|------|-----------|-----------|
| 런 시작(3밤 전체) | `RunState` 생성 (`NightFactory` 참조) | 기질·기억·시드 초기화 |
| 한 밤 시작 | `NightFactory`로 `NightState` 생성 (아이템 목록 전달) | 21시, baby/parent 초기값 세팅 |
| **행동 실행** | `ActionResolver.Apply(run, night, GameAction, rng)` | `ActionOutcome` 반환 |
| **턴 넘기기(시간 경과)** | `TurnResolver.EndTurn(run, night, rng)` | 패시브·수면진행·이벤트·밤종료 처리 |
| 밤 종료 후 기억 형성 | `MemoryConsolidator` | 습관(memory) 갱신 + 습관 카드 노트 |
| 엔딩 판정 | `EndingResolver` | `EndingId` 하나 |
| 눕히기 성공 확률 미리보기 | `ActionResolver.CalculateLaydownSuccessProbability(run, night)` | `double`(정보 표시용, 상태 안 바꿈) |

### 행동 실행의 정확한 순서 (★ 반드시 이대로)

```
var outcome = ActionResolver.Apply(run, night, action, rng);

if (!outcome.Accepted) {
    // 행동 거부됨. outcome.Log의 사유를 로그창에 표시. 시간·턴 소모 없음. 버튼 그대로 유지.
    return;
}

// outcome.Log를 로그창에 그린다 (아래 "결과 표현 모델" 참고)

if (outcome.ConsumedTurn) {
    TurnResolver.EndTurn(run, night, rng);   // ← 시간이 흐르고 이때 돌발 이벤트가 터질 수 있음
}

// night 상태를 다시 읽어 화면 갱신 (시계, 아기 상태, 체력, 로그)
if (night.Over) { /* → DIARY 로 전환 */ }
```

### Presentation이 화면에 그릴 때 읽는 값 (읽기 전용)

| 화면 요소 | 읽는 값 |
|-----------|---------|
| 시계 | `night.Hour` (21~6). 라벨은 `"21:00"` 형식 |
| 남은 시간 | 21시→6시까지 남은 턴 수 (백일밤 클리어 연출 전까지 "05:58" 같은 정확 시각은 숨김) |
| 아기 상태(단어) | `night.Baby.GetStage()` → `SleepStage` (아래 3절 표) |
| 아기 상태(수치) | `night.Baby.Calm/Sleep/Hunger` — **Monitor 아이템 있을 때만 숫자 노출** |
| 보호자 체력 | `night.Parent.Stamina` |
| 로그 | `night.Log` (각 항목 `hour/text/cls`) |
| 의미 이벤트 | `night.Events` (`GameEventId`) — 연출·문구 트리거용 |

> **Core는 화면 문장을 만들지 않는다.** `outcome.Log`의 한국어 문자열은 프로토타입 호환용 임시 텍스트다.
> 최종 카피/연출은 Presentation이 `GameEventId`·상태값을 보고 정한다.

---

## 3. 공용 데이터 (전 화면 공통)

**밤 구조**: 21:00 시작 → 06:00 종료. **시간 소모 행동 9번**이면 아침. (`GameConfig.TurnsPerNight = 9`)

**행동이 시간을 쓰는지 여부** — UI가 "이거 누르면 시간 가나?"를 표시할 때 씀:

| 시간 소모 (누르면 1시간 경과) | 시간 무소모 (즉시, 준비 행동) |
|---|---|
| 안기 Hold, 토닥 Pat, 수유 Feed, 눕히기 Laydown, 지켜보기 Watch, 할머니 Grandma | 쪽쪽이 Pacifier, 아기띠 토글, 소음기 토글, 바운서 토글 |

**아기 상태 수치** (0~100, 초기값): Calm 55 / Sleep 0 / Hunger 30 / Held false / Crying false.
**보호자 체력**: 100 시작.

**아기 상태 단어** (`BabyState.GetStage()`, 위에서부터 우선):

| 조건 | SleepStage | 화면 단어(예시) |
|------|-----------|----------------|
| 울고 있음 | `Cry` | 대성통곡 |
| Sleep ≥ 85 | `Deep` | 깊은 잠 |
| Sleep ≥ 50 | `Shallow` | 선잠 |
| Calm ≥ 70 | `Drowsy` | 꾸벅꾸벅 |
| Calm ≤ 30 | `Fussy` | 짜증 폭발 직전 |
| 그 외 | `Awake` | 말똥말똥 |

**기질 3종** (`TemperamentId`) — **id는 화면에 절대 노출 안 함. 힌트 문장만 보여줌.**

| 기질 | 힌트(화면 노출) | 특징(개발 참고) |
|------|----------------|-----------------|
| Soft 순둥이 | "작은 소리에는 크게 반응하지 않는 것 같다." | 둔감·자기진정 높음 |
| Sensitive 예민보스 | "멀리서 나는 소리에도 몸을 움찔거린다." | 예민·안아달라↑·자기진정 낮음 |
| Hungry 먹보 | "입을 오물거리며 자꾸 무언가를 찾는다." | 허기 빠름·수유 보너스 |

**아이템 5종** (`ItemId`) — 일반 밤 3개 선택, 백일밤 2개 선택:

| 아이템 | 효과(쉬운 말) | 부작용 |
|--------|--------------|--------|
| 🎒 아기띠 Carrier | 착용하면 계속 안은 상태. 진정 크고 잘 잠듦 | 매 시간 체력 소모, 반복하면 습관 학습 |
| 🍭 쪽쪽이 Pacifier | 즉시 진정, 밤당 3회, 시간 안 씀 | 선잠 중 빠지면 깰 수 있음 |
| 🔊 백색소음기 Noise | 켜두면 매 시간 진정 + 소음 이벤트 방어 | 매일 쓰면 익숙해져 효과↓ |
| 🪑 바운서 Bouncer | 내려놓은 아기를 체력 없이 달램 | 예민한 아기에겐 역효과 |
| 📟 베이비 모니터 Monitor | 아기 상태 **수치**를 정확히 보여줌 | 진정 효과 없음(정보가 무기) |

---

## 4. 화면별 명세

### 4.1 `TITLE` 타이틀

**보이는 것**: 게임 제목 "NOT A NAP : 백일의 밤", 시작 버튼 1개, (선택) 짧은 소개 한 줄.

**누르면**:
- **시작** → 첫째 밤 준비 화면(`SETUP`)으로 간다.

<details><summary>▸ 개발 계약</summary>

- 시작 시 `RunState` 새로 생성(시드 고정 시 결정론). 기질은 이때 무작위 결정되지만 화면엔 아직 안 보임.
- 완료 조건: 버튼 1회 클릭으로 `SETUP`(1일차) 진입, 중복 진입 없음.
</details>

---

### 4.2 `SETUP` 밤 시작 — 기질 힌트 + 아이템 고르기

**보이는 것**:
- 오늘이 몇째 밤인지 (1일째 / 2일째 / 백일째)
- 아기 기질 **힌트 문장 한 줄** (id 아님)
- 아이템 5개 카드. 각 카드에 이름·효과·부작용.
- 남은 슬롯 표시: **일반 밤 3칸 / 백일밤 2칸**
- "밤 시작하기" 버튼

**누르면**:
- **아이템 카드** → 고르면 선택됨(테두리 강조), 다시 누르면 해제. 슬롯이 다 차면 나머지 카드는 흐리게(비활성).
- **밤 시작하기** → 고른 아이템으로 밤이 시작되고 `PLAY` 화면으로 간다. (슬롯을 다 안 채워도 시작 가능하게 할지 여부는 아래 계약 참고)

<details><summary>▸ 개발 계약</summary>

- 슬롯 수: 일반 밤 `GameConfig.NormalNightItemSlots = 3`, 백일밤 `GameConfig.FinalNightItemSlots = 2`.
- 선택 아이템 리스트를 `NightFactory`에 넘겨 `NightState` 생성.
- 백일밤은 이 화면 앞에 `FINAL_INTRO`가 먼저 온다(4.6).
- **엔딩 "장비의 지배자" 집계**: 세 밤 동안 선택한 아이템 **종류의 누적 집합**이 4종 이상이면 조건 충족(`final-night-spec.md`). → 매 밤 선택을 런 누적 집합에 기록.
- 완료 조건: 슬롯 초과 선택 불가, 선택/해제 즉시 반영, "밤 시작하기"는 밤을 정확히 1회 생성.
- 미정(기획 확정 필요): 슬롯을 덜 채우고 시작 허용 여부. 프로토타입은 강제하지 않음 → 기본 허용 권장.
</details>

---

### 4.3 `PLAY` 밤 플레이 — **핵심 화면 (하나의 화면, 상태로 변함)**

이 화면이 게임의 90%다. **밤 내내 이 한 화면**이고, 행동 결과는 로그·상태 갱신·가끔 오버레이로 표현한다. 새 화면으로 넘어가지 않는다.

#### 레이아웃 (영역)

```
┌─────────────────────────────────────────┐
│  [시계 21:00]        [남은 시간: 9턴]     │  ← 상단 바
├─────────────────────────────────────────┤
│                                          │
│         아기 상태 표시                     │  ← 중앙: 아기 일러스트 + 상태 단어
│      (Monitor 있으면 수치도)              │     Held/Crying/Wearing에 따라 연출
│                                          │
│  보호자 체력 ▓▓▓▓▓▓░░░░ 100              │  ← 체력 바
├─────────────────────────────────────────┤
│  최근 로그 2~3줄 (색: 회/초/노/파랑)       │  ← 이벤트 로그
├─────────────────────────────────────────┤
│ [안기][토닥][수유][눕히기][지켜보기]        │  ← 시간 쓰는 행동
│ [쪽쪽이][아기띠][소음기][바운서][할머니]    │  ← 시간 안 쓰는 준비/토글 + 할머니
└─────────────────────────────────────────┘
```

- 아이템 토글 버튼은 **가진 아이템만** 표시. 착용 중이면 눌린 상태(ON) 표시.
- 로그 색: `sys`=회색, `good`=초록, `warn`=노랑/주황, `baby`=파랑(아기 시점).

#### 행동 버튼별 명세

아래는 전부 **같은 화면에서 상태만 바뀐다.** 각 항목: 쉬운 말 → 접힌 개발 계약(정확 수치).

##### ① 안기 (Hold) — 시간 1시간 소모

**누르면**: 아빠가 아기를 안는다. 아기가 진정된다(울고 있었으면 울음이 잦아든다). 대신 체력이 준다.

<details><summary>▸ 개발 계약</summary>

- 호출: `ActionResolver.Apply(..., GameAction.Hold, ...)` → 성공 시 `EndTurn`.
- **거부 조건**: 이미 아기띠 착용 중(`Wearing.Carrier`) → `Accepted=false`, 로그 "이미 아기띠로 안고 있다.", 시간 소모 없음.
- 효과: `Held=true`, `Calm += (16 + 기질.HoldNeed*12) * weak`, `Stamina -= 10`.
  - `weak = Stamina < 20 ? 0.6 : 1` (체력 고갈 시 진정 효과 감소).
  - 울고 있고 진정 결과 Calm>45면 울음 해제.
- 완료 조건: 안기 연출 후 상태 갱신, 체력 감소 1회만 적용.
</details>

##### ② 토닥 (Pat) — 시간 1시간 소모

**누르면**: 등을 토닥인다. 조금 진정된다. 이미 좀 자고 있었다면 더 깊이 잠든다. 체력은 조금만 준다.

<details><summary>▸ 개발 계약</summary>

- `Calm += 10*weak`; `Sleep>0 && !Crying`이면 `Sleep += 8`; `Stamina -= 5`.
- 울고 있고 Calm>50이면 울음 해제(로그 good).
</details>

##### ③ 수유 (Feed) — 시간 1시간 소모

**누르면**: 젖병을 준다.
- 아기가 실제로 배고팠으면 → 잘 먹고 크게 진정한다.
- 배고픈 게 아니었으면 → **고개를 돌리며 거부**한다. 살짝 짜증만 늘고 시간만 버린다. (별도 "수유 거부 화면" 없음 — 로그 + 상태로 표현)

<details><summary>▸ 개발 계약</summary>

- `Stamina -= 8` (성공·거부 공통).
- `Hunger >= 45`: `Hunger=5`, `Calm += (20 + 기질.FeedBonus)*weak`, 울음 해제, feeds++. (로그 good)
- `Hunger < 45`(거부): `Calm -= 6`, refusals++. (로그 warn "고개를 홱 돌린다…")
- 아기 배고픔은 **Monitor 없으면 숫자로 안 보임** → 오판 가능성이 곧 게임성. 거부는 실패가 아니라 정보다.
</details>

##### ④ 눕히기 (Laydown) — 시간 1시간 소모

**누르면**: 안고 있던 아기를 침대에 내려놓는다.
- 성공 → 아기가 침대에서 계속 잔다. (승리 조건과 직결)
- 실패 → 등이 닿는 순간 **센서 발동**, 아기가 깬다. 안겨 자던 습관이 셀수록 더 잘 깬다.

<details><summary>▸ 개발 계약</summary>

- **거부 조건**: 안고 있지도(`!Held`) 아기띠도 아님(`!Carrier`) → "아기는 이미 침대에 있다." 시간 소모 없음.
- 성공 확률 = `ActionResolver.CalculateLaydownSuccessProbability(run, night)`:
  - 기본: `Sleep≥85 → 0.9`, `Sleep≥50 → 0.6`, 그 외 `0.15`.
  - `- 기질.CribSens - memory.HeldDep*0.45 - memory.Carrier*0.20 - night.LaydownExtraPenalty`, 최종 `[0.05, 0.95]` 클램프.
  - 백일밤은 memory 효과 **1.5배**, 새벽각성 발동 시 `+0.1` 추가 페널티.
- `Stamina -= 4`. 아기띠 착용 중이었으면 벗겨짐(`Wearing.Carrier=false`).
- 성공: `Held=false`, `LaydownSucceeded=true`, `GameEventId.LaydownSucceeded`.
  - **맨손 눕히기**(`Held && !Carrier && !Bouncer` — 행동 시작 시점 기준) 성공이면 `BareHandsLaydown=true` → 백일밤 승리조건 3 충족.
- 실패: `GameEventId.LaydownFailed`, `TurnResolver.WakeBaby(...)` 호출로 아기 깸.
- UI 힌트: 눌러 시도하기 전에 위 확률을 **미리보기로 보여줄지**는 Monitor 소지 여부로 게이팅 권장(순수 함수라 상태 안 바꿈).
- 완료 조건: 성공/실패 결과 오버레이 표시, 결과 확정 후에만 다음 입력 허용(중복 클릭 방지).
</details>

##### ⑤ 지켜보기 (Watch) — 시간 1시간 소모

**누르면**: 손대지 않고 지켜본다. **체력이 회복된다.** 아기가 안정적이면 스스로 진정하기도 한다(자기진정 습관을 키우는 열쇠). 울고 있으면 지켜보는 건 역효과 — 울음이 커진다.

<details><summary>▸ 개발 계약</summary>

- `Stamina += 9` (유일한 체력 회복 행동).
- `!Crying && Calm≥45 && rng < (기질.SelfSoothe + memory.SelfSoothe) + 0.15`: `Calm += 7`, watchOk++ (good). watchOk 2회↑ → 기억 형성에서 SelfSoothe 습관 성장.
- 울고 있음: `Calm -= 9` (warn). 그 외: `Calm -= 5`.
</details>

##### ⑥ 할머니 찬스 (Grandma) — 시간 1시간 소모, 런당 1회, 백일밤 금지

**누르면**: 할머니가 와서 아기를 순식간에 재우고 내 체력도 크게 채워준다. 단, **아기는 이 품도 기억한다**(안겨 자는 습관↑). 그리고 **백일째 밤엔 못 쓴다.**

<details><summary>▸ 개발 계약</summary>

- **거부 조건**: 백일밤(`run.IsFinalNight`) → "가족 없이 버텨야 한다…" / 이미 사용(`run.GrandmaUsed`) → "이미 사용했다."
- 효과: `Calm=95`, `Sleep=max(Sleep,60)`, 울음 해제, `Held=true`, `Stamina += 35`. `GrandmaUsed=true`.
- 부작용: 기억 형성에서 HeldDep 습관 강화. 엔딩 "할머니가 최고야" 조건 플래그.
- UI: 백일밤에는 버튼을 아예 숨기거나 비활성 + 사유 툴팁 권장.
</details>

##### ⑦ 쪽쪽이 (Pacifier) — 시간 안 씀, 밤당 3회

**누르면**: 쪽쪽이를 물린다. 대개 즉시 조용해진다. 가끔 뱉는다. 시간은 안 쓴다.

<details><summary>▸ 개발 계약</summary>

- `ConsumedTurn=false`. **거부**: 미소지 / `PacifierLeft<=0`.
- 15% 뱉음: `Calm -= 8` (warn). 아니면 `Calm += 18`, `PacifierInUse=true`, Calm>45면 울음 해제.
- 잔여 횟수(`PacifierLeft`, 시작 3) UI 표시. 선잠 중 자연 이탈은 `EndTurn`에서 처리(4.4 참고).
</details>

##### ⑧ 아기띠 / 소음기 / 바운서 토글 — 시간 안 씀

**누르면**: 해당 장비를 켜거나 끈다(ON/OFF). 즉시 반영, 시간 안 씀. 효과는 **다음 시간부터** 매 턴 적용된다.

<details><summary>▸ 개발 계약</summary>

- 전부 `ConsumedTurn=false`. 미소지면 거부.
- **아기띠**: 켜면 `Held=true`. 백일밤 버클고장 중(`CarrierDisabledTurns>0`)이면 켜기 거부.
- **소음기**: 백일밤 배터리 방전(`NoiseDisabled`) 시 켜기 거부.
- **바운서**: 안고 있거나 아기띠 착용 중이면 거부("먼저 눕히세요"). ⚠️ Bouncer는 LEGACY, 수면 성공 아이템에서 제거 예정(`docs/decision-register.md`) — 대체 확정 전까지 현 동작 유지.
- 착용 상태(`Wearing.*`)를 버튼 눌림/해제로 표시.
</details>

#### 결과 표현 모델 (별도 화면 대신 이것)

이전 스토리보드의 "FAIL-MISREAD-PICKUP" 같은 **전체 화면은 만들지 않는다.** 대신:

1. **로그 라인** (기본) — 대부분의 결과. `outcome.Log`를 색상으로 로그창에 추가. 안기·토닥·수유거부·토글 등 전부 여기.
2. **결과 오버레이** (큰 사건만) — 게임 화면 **위에 뜨는 모달**, 닫으면 `PLAY` 복귀:
   - 눕히기 성공/실패
   - 아기가 깸 (`GameEventId.BabyFullyWoke`)
   - 돌발 이벤트 발동 (기저귀/초인종/백일밤 표적 이벤트)
   - 밤 종료 전환
3. **상태 강조** (유도) — 실패 후 복귀 시 다음에 눌러볼 만한 버튼을 은은하게 강조 가능(선택 연출). 강조는 **연출일 뿐 판정에 개입하지 않는다.**

<details><summary>▸ 개발 계약 (오버레이 공통 규칙)</summary>

- 오버레이가 떠 있는 동안 **다른 행동 버튼 입력 차단**.
- 오버레이는 이미 적용된 상태 변화를 **다시 적용하지 않는다**(중복 방지). 상태는 Apply/EndTurn에서 이미 확정됨.
- 오버레이 트리거는 `outcome`의 플래그(`LaydownSucceeded` 등)와 `night.Events`(`GameEventId`)로 판단.
- 돌발 이벤트는 `EndTurn` 내부에서 자동 발생 → EndTurn 직후 `night.Events`/`night.Log` 새 항목을 확인해 오버레이 표시.
</details>

---

### 4.4 시간 경과(EndTurn)로 자동 일어나는 일 — 화면 반영용

시간 쓰는 행동 뒤 `EndTurn`이 돌면 아래가 자동 처리된다. **플레이어 입력 없이** 상태가 변하니 화면이 반드시 다시 읽어야 한다.

| 자동 처리 | 화면 반영 |
|-----------|-----------|
| 아이템 패시브 (아기띠 진정+체력소모 / 소음기 진정 / 바운서) | 상태·체력 갱신 |
| 배고픔 증가, 78 초과 & 안 울면 **배꼽시계로 깸** | 깸 오버레이 |
| 진정 자연 감소, Calm≤20이면 **울음 터짐** | 상태 단어 변경 |
| 수면 진행 (조건: 안 울고 Calm≥68 & Hunger<70) | Sleep 증가, 단어 변경 |
| 쪽쪽이 자연 이탈(선잠 중 30%) → 50%로 깸 | 로그/깸 오버레이 |
| 시각 +1시간 | 시계 갱신 |
| **돌발/표적 이벤트** (4.5) | 이벤트 오버레이 |
| 6시 도달 → 밤 종료 | `DIARY`로 전환 |

<details><summary>▸ 개발 계약 (밤 종료 판정)</summary>

- `night.Hour == 6`이면 `Over=true`. 종료 시 아기 상태로 결과 확정:
  - `Deep/Shallow && !Held` → `NightOutcome.Crib`(침대에서 아침)
  - `Deep/Shallow && Held` → `NightOutcome.Arms`(품에서 아침)
  - 그 외 → `NightOutcome.Awake`(끝내 못 잠)
- `GameEventId.NightCompleted` 발생. 이후 `DIARY`로.
</details>

---

### 4.5 돌발 이벤트 — 오버레이로 표시

플레이어가 만드는 게 아니라 **정해진 시각에 자동으로** 터진다. 전부 `EndTurn` 안에서 처리되므로 별도 명령 없음. 화면은 오버레이만 띄우면 된다.

**일반 밤** (`ScheduledEventResolver`):
- **1일차 00시 — 기저귀 사태**: Calm −20, 자고 있었으면 80%(소음기 있으면 감소)로 깸.
- **2일차 23시 — 초인종**: 자고 있으면 기질·소음기에 따라 깰 수 있음.

**백일째 밤 표적 이벤트** (`FinalNightResolver`, 형성된 습관을 노림, **최대 2개**):
- 00시 **아기띠 버클 고장** (`memory.carrier ≥ 0.3`): 2턴간 아기띠 사용 불가.
- 01시 **백색소음기 배터리 방전** (`noiseHab ≥ 0.3` 또는 1~2일차 noiseTurns 합 ≥ 6): 그 밤 내내 소음기 꺼짐.
- 03시 **새벽 각성** (`memory.heldDep ≥ 0.3`): Sleep −30, 눕히기 페널티 +0.1.
- (보상) **자기진정 재입면** (`memory.selfSoothe ≥ 0.3`): 깰 때마다 50%로 스스로 다시 잠듦. 2개 제한과 무관하게 항상 활성.

<details><summary>▸ 개발 계약</summary>

- 선택 규칙·수치는 `docs/final-night-spec.md`가 원본. 화면은 판정하지 않고 **결과만** 보여준다.
- 표적 이벤트는 "그동안 이렇게 재웠으니 오늘 이게 돌아온다"는 서사가 핵심 — 오버레이 카피에 **어떤 습관 때문인지**를 연결하면 게임 주제("습관이 다음 밤 규칙을 바꾼다")가 산다.
- 미구현(기획 확정 필요): 예방접종·배앓이·이앓이 등 추가 콘텐츠는 아직 없음. 현재 이벤트는 위가 전부.
</details>

---

### 4.6 `FINAL_INTRO` 백일째 밤 인트로

**보이는 것**: "시간이 흘렀다. 오늘은 백일째 밤." + **지금까지 형성된 습관 요약 카드** + 제약 안내(아이템 2개 / 할머니 없음 / 습관 효과 1.5배).

**누르면**: 확인 → 백일밤 `SETUP`(슬롯 2개)로.

<details><summary>▸ 개발 계약</summary>

- 습관 요약은 `run.Memory`(carrier/heldDep/noiseHab/selfSoothe) + 누적 memoryNotes에서 생성.
- 제약은 전부 Core가 강제(슬롯2·할머니금지·1.5배) — 화면은 안내만.
</details>

---

### 4.7 `DIARY` 육아일지 + 습관 카드

**보이는 것**:
- 밤 결과(침대에서 아침 / 품에서 아침 / 끝내 못 잠)
- **육아일지** 서술문 (AI 생성, 실패 시 규칙 기반 폴백)
- 오늘 밤 형성된 **습관 카드**들 (좋음/나쁨) — "아기띠에서 잠드는 습관이 생겼습니다" 등

**누르면**: 다음 → 다음 밤 `SETUP`, 백일밤 뒤면 `ENDING`.

<details><summary>▸ 개발 계약</summary>

- 습관 형성: `MemoryConsolidator`가 그 밤 stats로 memory 갱신 + 노트 생성.
  - carrierTurns≥3 → carrier↑, heldSleepTurns≥3 또는 할머니 → heldDep↑, noiseTurns≥4 → noiseHab↑, watchOk≥2 → selfSoothe↑(좋은 습관).
- **AI 호출은 밤 종료 시 1회, 서술만.** 상태·판정 절대 변경 금지. 실패 시 규칙 기반 폴백 일지 사용(`fallbackDiary` 대응). API 키는 클라이언트/저장소에 없음 — 프록시 URL만.
- `NarrativeBoundary` 참고: AI 출력은 검증된 필드(diary 문자열, memory_notes 배열)만 통과.
</details>

---

### 4.8 `ENDING` 엔딩 6종

**보이는 것**: 결정된 엔딩 하나 (제목 + 이모지 + 부제), 다시하기 버튼.

**누르면**: 다시하기 → `TITLE`.

<details><summary>▸ 개발 계약 (판정 우선순위, 위부터 — <code>EndingResolver</code>)</summary>

1. **아침이 이겼다** (실패, `MorningWon`): 승리 조건 0~1개.
2. **우리 집의 루틴** (`FamilyRoutine`): 승리 + carrier·heldDep·noiseHab 전부 < 0.4.
3. **품 안의 우주** (`UniverseInArms`): 승리 + carrier 또는 heldDep ≥ 0.5.
4. **할머니가 최고야** (`GrandmaBest`): 승리 + 1~2일차 할머니 사용.
5. **장비의 지배자** (`GearMaster`): 승리 + 세 밤 누적 서로 다른 아이템 4종 이상.
6. **새벽의 생존자** (`DawnSurvivor`): 그 외 승리.

- 백일밤 승리 = 3개 중 2개 이상: 깊은잠(Sleep≥85 & 안 울음) / 체력≥30 / 맨손눕히기 1회↑.
- 판정용 memory는 백일밤 기억 형성까지 반영된 저장값(1.5배 미적용).
</details>

---

## 5. 개발 완료 조건 (공통 QA 체크)

모든 화면 공통으로 아래를 만족해야 "완료"다.

- [ ] 행동 → `Apply` → (`ConsumedTurn`이면) `EndTurn` 순서를 지킨다. 순서 뒤바뀜 없음.
- [ ] `Accepted=false`면 시간·상태 변화 없이 사유 로그만 뜬다.
- [ ] 상태 변화는 Core에서 **한 번만** 일어나고, 오버레이/연출은 그 결과를 **다시 적용하지 않는다.**
- [ ] 오버레이가 떠 있는 동안 다른 버튼 입력이 막힌다. 복귀 후 중복 적용 없음.
- [ ] Monitor 미소지 시 숫자(Calm/Sleep/Hunger) 비노출, 단어만.
- [ ] 백일밤: 슬롯 2, 할머니 버튼 비활성/숨김, 습관 효과·표적 이벤트가 실제로 반영됨.
- [ ] Core는 화면 문구를 만들지 않는다. 화면 카피는 `GameEventId`·상태 기반으로 Presentation이 정한다.
- [ ] 같은 시드에서 같은 입력이면 화면 결과도 동일(결정론) — Core가 보장, UI가 rng를 따로 만들지 않는다.

---

## 부록 A. 이전 스토리보드에서 버려야 할 것 (혼란 원인)

| 스토리보드에 있던 것 | 실제 | 처리 |
|---|---|---|
| "기저귀 확인" 행동, CheckDiaper, CauseRevealed, WetDiaper | **그런 행동 없음.** 기저귀는 1일차 00시 자동 이벤트 | 삭제. 4.5의 자동 이벤트로 대체 |
| FAIL-MISREAD-PICKUP / FAIL-MISREAD-FEED 전체 화면 | 별도 화면 아님 | `PLAY` 위 결과 오버레이/로그로 대체(4.3) |
| N1-T1-CHECK 등 상태별 개별 화면 42장 | 하나의 `PLAY` 화면 상태 변화 | 화면 지도(1절)의 최소 세트로 대체 |
| CMD_* / EVT_STATE_UPDATED 가짜 명령 | 실제 API는 `Apply`/`EndTurn` | 2절 계약으로 대체 |
| trace.basic_check.diaper 등 Trace 콘텐츠 | Delayed Echo는 **뼈대만**, 실제 콘텐츠 미확정 | 기획 확정 전까지 화면에 노출 안 함 |

## 부록 B. 아직 기획 확정이 필요한 지점 (개발 전 결정)

- 슬롯 미충족 상태로 밤 시작 허용 여부 (권장: 허용).
- 눕히기 성공 확률 미리보기를 Monitor로 게이팅할지 여부.
- Bouncer 대체 아이템 (LEGACY 제거 후).
- 추가 돌발 이벤트 콘텐츠(예방접종·배앓이 등) 채택 여부 — 현재 미구현.
- Trace/FutureEvent/Cue 실제 콘텐츠 — 확정 전까지 화면 비노출.
