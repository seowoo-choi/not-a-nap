# 인게임 LLM 서술 프록시

밤이 끝나면 게임은 육아일지 서술을 받기 위해 서버리스 프록시를 **정확히 1회** 호출한다.
이 문서는 그 경계와 계약을 정의한다. 제품 규칙의 원본은
[`vertical-slice-spec.md`](vertical-slice-spec.md)이고, 여기서는 호출 구조만 다룬다.

## 절대 규칙

- LLM은 **판정하지 않는다.** 응답은 화면 문구만 바꾸며 수치·확률·기억·이벤트·승패·엔딩에 닿지 않는다.
- **API 키는 클라이언트에 없다.** 빌드와 저장소가 아는 값은 프록시 URL 하나뿐이다.
- **프롬프트도 클라이언트에 없다.** 프록시가 시스템 프롬프트를 소유한다. 게임은 사실만 보낸다.
- 호출 실패·타임아웃·검증 실패는 전부 **규칙 기반 폴백 서술**로 떨어진다. 게임 진행은 AI 가용성에 의존하지 않는다.

## 구성 요소

| 계층 | 파일 | 책임 |
|---|---|---|
| Core | `Core/Models/NarrativeFacts.cs` (`NarrativeFacts`) | 판정이 끝난 밤에서 허용된 사실만 투영 |
| Core | `Core/Rules/NarrativeRequest.cs` | 사실을 요청 JSON으로 직렬화 (`NarrativeRequest`), 밤당 1회 제한 (`NarrativeCallGate`) |
| Core | `Core/Rules/NarrativeBoundary.cs` | 응답 문자열 길이·금칙어 검증. 실패 시 폴백 ID |
| App | `App/NarrativeProxyClient.cs` | 전송(UnityWebRequest)·JSON 파싱·설정 로드 |
| Presentation | `GameSessionPresenter.ApplyNarrative` | 검증을 통과한 문자열로 **화면 문구만** 교체 |

Core와 Presentation은 `noEngineReferences`라 네트워크에 접근할 수 없다.
전송은 App 계층에만 존재하고, 그 응답이 Core로 되돌아가는 경로는 `ApplyNarrative` 하나뿐이며
그 안에서 반드시 `NarrativeBoundary`를 통과한다.

## 요청

`POST <프록시 URL>` / `Content-Type: application/json`

```json
{
  "contract": "diary.v2",
  "night": "SecondNight",
  "grade": "B",
  "metrics": {
    "longestSleepMinutes": 95, "wakeCount": 3, "parentStamina": 42.5,
    "bareHandsLaydownAttempts": 2, "bareHandsLaydownSucceeded": true,
    "usedCatchBreath": true, "feedingPreparationIncident": false,
    "longestMovementMinutes": 6
  },
  "signals": { "firstNoticed": "Rooting" },
  "actions": {
    "mostRepeated": "Pat", "mostRepeatedCount": 4,
    "rejected": "Pacifier", "followup": "Hold",
    "longestPreparationStep": null, "sleepIntervalChoice": null,
    "longestMovementDestination": "Kitchen"
  },
  "rhythms": [ { "id": "Carrier", "strength": 0.25, "sourceCount": 3 } ]
}
```

본문에는 **열거형 ID와 수치만** 담긴다. 화면 문구, 프롬프트 문장, 플레이어가 입력한 아기 이름은
보내지 않는다. `NarrativeRequestTests.PayloadCarriesNoFreeTextOrPlayerInput`가 본문에 한글이
한 글자라도 섞이면 실패시켜 이 계약을 회귀 검증한다.

## 응답

```json
{
  "noticedSignal": "…",
  "caregiverGrowth": "…",
  "habitReflection": "…",
  "familyUnderstanding": "…",
  "shareCard": "…"
}
```

다섯 문자열 외의 필드는 읽지 않는다. 각 문자열은 `NarrativeBoundary`에서 검증한다.

- 180자 초과 → 거부
- 금칙어(진단·치료·처방 등 의료 표현, 구매·광고 등 상업 표현, 승패·상태 변경 지시) 포함 → 거부
- 하나라도 비었거나 거부되면 응답 전체를 버리고 폴백 서술 유지

프록시는 CORS 허용 헤더(`Access-Control-Allow-Origin`)를 GitHub Pages 도메인에 대해 내려야 한다.

## 설정

`Assets/Resources/narrative-proxy.json`:

```json
{ "url": "", "timeoutSeconds": 8 }
```

- `url`이 비어 있으면 **호출하지 않는다** (기본값). 게임은 폴백 서술로 완전히 동작한다.
- `https://`만 허용한다. GitHub Pages는 https라 http 프록시는 혼합 콘텐츠로 차단된다.
  로컬 개발용 `http://localhost`·`http://127.0.0.1`만 예외다.
- 에디터·스탠드얼론에서는 환경 변수 `NOTANAP_NARRATIVE_PROXY_URL`이 파일보다 우선한다.
- 배포는 저장소 변수 `NARRATIVE_PROXY_URL`을 쓴다. `deploy-webgl.yml`의 `Configure narrative proxy`
  단계가 빌드 직전에 위 파일을 덮어쓴다. 변수를 설정하지 않으면 폴백 빌드가 나간다.
  이 값은 Secret이 아니라 변수다 — URL 자체는 비밀이 아니며, 비밀은 프록시 안에만 있다.

## 호출 횟수

`NarrativeCallGate`가 밤당 1회를 보장한다. 실패해도 재시도하지 않는다.
한 런(첫째 밤·둘째 밤·백일째 밤)의 상한은 3회이며 `GateCapsWholeRunAtThreeCalls`가 이를 검증한다.
