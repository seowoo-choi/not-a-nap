# Codex 현재 상태 감사

감사일: 2026-07-18 (Asia/Seoul)

## 작업 시작 상태

- 브랜치: `main` (`origin/main` 추적)
- 최근 커밋: `0715ff7 chore: upgrade project to Unity 6000.3.20f1`
- 기존 미커밋 변경: `.gitignore`, `docs/final-night-spec.md`
- 기존 미추적 작업: `Assets/Scripts/`, `Assets/Tests/` 및 상위 meta
- 위 변경은 모두 보존했다. README와 제출용 PDF는 수정하지 않았다.

## 환경과 설정

- `ProjectVersion.txt`: Unity 6000.3.20f1
- 설치 Editor: `/Applications/Unity/Hub/Editor/6000.3.20f1/Unity.app`
- WebGLSupport 모듈 및 ProjectSettings의 WebGL 빌드 대상 확인
- `com.unity.inputsystem` 1.19.0, `activeInputHandler: 1`; 레거시 전용 모드가 아님
- Test Framework 1.6.0
- `GameCore.asmdef`는 `noEngineReferences: true`; EditMode 테스트 asmdef가 GameCore를 참조

## 실제 구현 범위

### 구현됨

- 세 밤 상태 진행, 21:00~06:00의 9개 소비 턴
- 5개 기본 행동과 5개 아이템, 3개 기질의 규칙 기반 효과
- `IRandomSource`, 시드 기반 `SystemRandomSource`, 호출 순서 제어용 `SequenceRandomSource`
- 기억 형성과 실제 눕히기·자기진정·아이템 효과 반영
- 백일째 밤 표적 이벤트, 3개 중 2개 승리 판정, 6개 EndingId 우선순위
- 순수 설정 모델, `VictoryRuleDefinition`, `IVictoryRule`, `FeatureFlags`, 의미 기반 `GameEventId`
- AI 서술 경계와 AI 없는 폴백 ID
- 가치중립적 `TraceState`와 결정론적 Delayed Echo Event 예약·맥락 판정 기반

### 일부만 구현됨

- 의미 이벤트 목록은 새 코드와 주요 결과에 연결했지만 기존 행동 로그는 한국어 문자열 호환 어댑터를 유지한다.
- `GameBalanceConfig`는 초기 상태·아이템 슬롯·기억·승리 규칙부터 연결했다. 기존 행동별 세부 수치는 아직 레거시 Resolver 상수다.
- `HandoffData`/`HandoffCueId`, 활성수면·파트너 힌트·최종 도전 이벤트와 FeatureFlag는 확장 지점만 있다.
- Delayed Echo 계층은 generic ID와 테스트 fixture까지만 구현했다. 예방접종, 배앓이, 이앓이 등 실제 콘텐츠는 없다.

## Memory와 Trace의 역할

- `MemoryState`는 Carrier/HeldDep/NoiseHab/SelfSoothe처럼 현재 규칙에 직접 적용되는 연속 수치다. 기존 플레이 호환을 위해 유지한다.
- `TraceState`는 행동·이벤트의 과거 사실과 강도, 태그, 생성 시점, 발동 이력을 저장한다. Trace 자체에는 긍정·부정 속성이 없다.
- `FutureEventScheduler`가 seed, Trace 조건, eligible night/turn window, weight, run별 발동 제한으로 `EventSeed`를 예약한다.
- `IContextualEventRule`이 예약 이벤트와 현재 `EventContext`를 조합해 `EventResolution`의 규칙 결과 분류를 결정한다.

## Feedback과 Feedforward

- Feedback은 행동 직후의 실제 결과다. 기존 `LaydownSucceeded`, `LaydownFailed` 등의 의미 이벤트가 담당한다.
- Feedforward는 미래 영향 가능성만 암시하는 `FeedforwardCueId`다. Cue 조회는 상태나 Trace를 변경하거나 이벤트를 자동 해결하지 않는다.
- 실제 화면 문장은 Presentation/로컬라이징 계층의 책임이다.

## Bouncer 상태

- `ItemId.Bouncer`와 기존 Resolver는 호환을 위해 LEGACY로 남아 있다.
- 기질별 바운서 불이익을 장기 계약으로 검증하던 테스트는 일반적인 기질 modifier 테스트로 변경했다.
- 수면 성공 아이템에서는 제거 예정이며 대체 아이템은 안전성 검토와 최종 기획 이후 확정한다.

### 문서/프로토타입에만 있음

- 인수인계 UI, 엄마 깨우기 UI, 활성수면 튜토리얼, 배고픔 단서 애니메이션, 저자극 수유 UI
- 05:58 구체 연출, 성장 앨범, 백일잔치 에필로그, 공유 카드

## 문서와 코드 충돌

- `CLAUDE.md`와 `Reference/prototype.html`은 AI bias로 memory를 변경했으나 확정 원칙과 충돌해 bias 파싱·적용을 제거하고 서술 전용으로 정정했다.
- `docs/final-night-spec.md`는 자신을 수치 원본으로 선언하지만 현재 피날레·승리 조건은 변경 가능 항목이다. 코드는 기존 동작을 기본값으로 유지하면서 설정 가능하게 했다.
- `docs/evidence-based-parenting-design.md`, `docs/real-parenting-expansion.md`는 현재 저장소에 없다. 따라서 구현 근거로 사용하지 않았다.
- `Reference/prototype.html`은 실행 가능한 독립 프로토타입이며 Unity Scene/Presentation과 연결돼 있지 않다.

## 컴파일과 테스트

- 변경 전 batchmode 실행에서 기존 테스트 asmdef가 NUnit을 노출하지 않아 모든 테스트의 `Test`/`TestCase` 타입을 찾지 못하는 컴파일 오류를 확인했다. Unity 표준 `TestAssemblies` 참조로 수정했다.
- 변경 후 batchmode 재실행은 Unity Licensing Client 연결이 반복해서 끊기고 package entitlement를 거부해 `com.unity.test-framework`와 `com.unity.ext.nunit`를 포함한 패키지를 0개 등록했다. 그 결과 테스트 assembly에서 NUnit 타입을 찾지 못해 프로젝트 컴파일이 실패했으며 결과 XML은 생성되지 않았다. Core source 자체의 별도 C# 오류는 로그에 나타나지 않았지만, 프로젝트 전체 컴파일 성공을 주장할 수 없다.
- Delayed Echo 작업 후에도 같은 명령을 재실행했고 동일한 라이선스/패키지 등록 실패와 NUnit 누락이 재현됐다. 발견된 테스트 실행 케이스는 54개이며 실제 실행된 테스트는 0개다.
- 코드 정적 감사에서 Core의 `UnityEngine`, `MonoBehaviour`, `ScriptableObject`, `UnityEngine.Random` 의존은 발견되지 않았다.

### 검증 완료 (2026-07-18)

위 라이선스/패키지 등록 실패의 실제 원인은 Core 코드가 아니라 batchmode 실행 환경이었고, 다음을 정리한 뒤 테스트가 정상 실행됐다.

- 이전 실행을 강제 종료하며 남은 orphan `UnityLicensingClient` 프로세스와 `Temp/UnityLockfile`을 제거하니 라이선스 초기화 hang이 사라지고 `com.unity.test-framework`/`com.unity.ext.nunit` 패키지가 정상 로드됐다. 로컬 라이선스 파일(`Unity_lic.ulf`, 2026-08-17까지 유효)은 문제가 없었다.
- cold Library 상태의 첫 `-runTests` 실행은 asset import·스크립트 컴파일만 수행하고 종료된다(정상 동작). 이때 `GameCore.Tests.EditMode.dll`이 C# 오류 없이 컴파일됨을 확인했다.
- `-runTests`와 `-quit`를 함께 넘기면 `-quit`가 테스트 러너 시작 전에 에디터를 종료시켜 결과 XML이 생성되지 않는다. `-quit`를 제거하자 러너가 정상 실행됐다.
- 최초 실행 결과 54개 중 53개 통과, 1개 실패(`RequiredVictoryCountComesFromDefinition`). 이는 production 버그가 아니라 테스트가 `new NightState()` 기본값(체력 ≥ 30)을 통제하지 않아 승리 조건이 2개 충족된 것이었다. 테스트에서 체력을 임계값 미만으로 고정해 조건을 정확히 1개로 만든 뒤 재실행했다.
- 최종: EditMode 54개 전체 실행, 54개 통과, 실패 0, skipped/inconclusive 0. 결과 XML `TestResults/EditMode-results.xml` 생성(gitignore 처리됨).

## 변경 위험이 높은 영역

- `ActionResolver`, `TurnResolver`, `ScheduledEventResolver`의 레거시 하드코딩 수치와 한국어 로그
- 05:58 연출 및 최종 밤 규칙의 문서상 확정 표현
- Scene/Presentation 계층 부재: 현재 Unity에서 실제 플레이 가능한 UI 흐름은 없다.
- Unity 라이선스/배치 실행 환경이 재현 가능한 CI로 정리되지 않음
- 기존 Assets 코드와 테스트 전체가 작업 시작 당시 Git 미추적 상태였음
