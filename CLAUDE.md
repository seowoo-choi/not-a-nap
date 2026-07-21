# NOT A NAP : 백일의 밤
NHN NAN 2026 해커톤 사전 과제. Unity 2D → WebGL 빌드 → GitHub Pages 배포.
아기가 플레이어의 재우는 방식을 기억하고 습관을 형성해 다음 밤의 규칙이 실제로 바뀌고,
백일째 밤에 그 습관들이 전부 돌아와 최종 시험이 되는 턴제 육아 로그라이크.
구성: 첫째 밤 → 둘째 밤 → 백일째 밤(보스전). 플레이 5분 내외.

## 절대 규칙
- 게임 판정(수치·확률·승패)은 전부 결정론적 C# 엔진이 담당한다. LLM은 판정하지 않는다.
- AI(LLM)는 밤 종료 시 1회만 호출해 육아일지 등 서술만 생성한다. 게임 상태와 판정은 변경하지 않으며 실패 시 규칙 기반 폴백을 사용한다.
- API 키를 유니티 빌드/저장소에 절대 넣지 않는다. 서버리스 프록시 URL만 설정한다.
- 코어 로직(GameCore)은 MonoBehaviour 없는 순수 C#으로 작성해 Unity Test Framework로 테스트한다.
- V2 통잠 루프의 제품 규칙은 docs/vertical-slice-spec.md, 실제 구현 순서는
  docs/code-first-development-plan.md가 원본이다. Reference/prototype.html과 V1 GameAction은
  저장·회귀 테스트 호환용 LEGACY이며 새 화면의 판정 원본으로 사용하지 않는다.
- 게임의 목표는 "아기를 한 번 재우기"가 아니라 21:00~06:00의 반복 각성을 돌보며
  가장 긴 연속 수면과 다음 밤에도 유지 가능한 가족 루틴을 만드는 것이다.
- WebGL 빌드 목표 50MB 이하. 외부 에셋 추가 시 반드시 출처·라이선스를 docs/assets.md에 기록.
- 제품 스코프 상한: 아기 1명, 밤 3회(1일/2일/백일), 기질 3종, 엔딩 6종.
  V2의 진단·수면 관찰·수유 준비 행동은 승인된 통잠 루프에 포함되므로 기존 "행동 5개" 제한의 예외다.
  쌍둥이·다중 보호자·실제 브랜드/광고 SDK·백일 이후 챕터는 P0보다 먼저 구현하지 않는다.

## 현재 개발 게이트

- Figma 35개 프레임을 35개 Unity Scene으로 만들지 않는다. 실제 화면은 TITLE/SETUP/PLAY/DIARY/FINAL_INTRO/ENDING이다.
- 현재 한 밤 Presentation은 존재하지만 세 밤 Memory/Ending 연결, 정상 온습도, 상태 기반 각성,
  기저귀 우선 규칙, 피로 신호, V2 아이템 게이트가 미완성이다.
- 새 Presentation 확장 전 docs/code-first-development-plan.md의 P0-1~P0-5를 순서대로 완료한다.
- Bouncer는 LEGACY 역직렬화만 유지하고 V2 신규 선택 UI에는 노출하지 않는다.
