# 결정 등록부

## [확정]

- Unity 6000.3.20f1
- Input System Package만 사용 (`activeInputHandler: 1`)
- Web 플랫폼 빌드
- 결정론적 순수 C# Core와 주입식 `IRandomSource`
- AI는 서술 전용이며 수치·확률·기억·이벤트·승패·엔딩 판정에 관여하지 않음
- AI가 없거나 응답이 잘못되어도 규칙 기반 폴백 사용

## [확정된 설계 원칙]

- 과거 행동과 규칙 결과는 가치중립적인 Trace를 남길 수 있음
- Trace 자체에는 Good/Bad 속성을 두지 않음
- 같은 Trace도 Trigger Event와 현재 Context 조합에 따라 Positive, Negative, Neutral 결과가 될 수 있음
- 동일 seed + 동일 입력·Trace·Context는 동일한 미래 이벤트와 발생 시점을 만듦
- AI는 Trace 생성, Future Event 선택, Context 판정과 결과에 개입하지 않음

## [현재 유지하지만 변경 가능]

- 3밤 구조, 밤당 9개 소비 턴
- 기질 3종, 아이템 5종, 엔딩 6종
- 백일째 밤의 3개 중 2개 승리 조건
- 인수인계, 활성수면, 엄마 깨우기, 05:58 최종 도전
- 기존 `MemoryState` 수치형 습관 구조
- V1의 9개 1시간 턴 API는 저장·Presentation 호환 계층으로 유지

## [V2 Core 규칙]

- 밤은 21:00~06:00의 540분 동안 계속되며 눕히기 성공은 밤 종료가 아니라 수면 구간의 일부다.
- 새벽 평가는 최장 연속 수면, 총 수면, 각성, 진단, 안전 지표를 설정 임계값으로 판정한다.
- 잠든 뒤에도 주입된 RNG로 각성 원인과 시점을 예약할 수 있다.
- 수면 단계, 진단, 환경 확인, 단계형 수유 준비는 Core의 구조화된 상태와 결과로 제공한다.
- 의료 권장 범위와 수유량은 Core Resolver에 상수로 확정하지 않고 설정 공급자가 제공한다.
- 외형 ID 및 성별 외형은 기질·능력 modifier와 독립이다.
- Vaccination modifier는 콘텐츠 문구나 진단 없이 규칙 배율만 제공하는 첫 수직 슬라이스다.

현재 수치와 목록은 호환 기본값일 뿐 최종 기획 확정이 아니다. 승리 임계값과 `requiredCount`는 `VictoryRuleDefinition`, 기억 임계값과 초기 상태는 `GameBalanceConfig`, 미확정 기능은 `FeatureFlags`에서 교체한다.

## [변경 예정]

- Bouncer는 수면 성공 아이템에서 제거 예정
- 대체 아이템은 안전성 검토 후 확정

현재 `ItemId.Bouncer`와 관련 Resolver는 기존 동작 호환을 위해 LEGACY 상태로 유지한다. 새 설계의 장기 계약이나 기질 필수 테스트로 간주하지 않는다.

## [아이디어 저장소]

- 쌍둥이
- 수면퇴행
- 이앓이
- 발달 변화 주간
- 속싸개
- 온습도
- 신규 아이템 대량 추가

위 항목은 구현 기준이 아니다. `docs/real-parenting-expansion.md`는 현재 저장소에 존재하지 않는다.
