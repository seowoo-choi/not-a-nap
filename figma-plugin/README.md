# NOT A NAP — V11 Direct Care Code Sync

현재 Unity의 큰 아기, 신체 직접 터치, 방 소품 직접조작, 완성 합성 돌봄 이미지를 기존
Figma V6 코드 싱크 보드에 반영하는 개발 플러그인입니다.

## 실행

1. Figma Desktop에서 `MOBILE_QA_STORYBOARD_V6`가 있는 파일을 엽니다.
2. `Plugins → Development → Import plugin from manifest...`에서 `manifest.json`을 고릅니다.
3. `NOT A NAP — V11 Direct Care Code Sync`를 실행합니다.

원본 V6는 보존합니다. 첫 실행만 코드 싱크 보드를 복제하고 이후에는 가장 최신
`MOBILE_QA_STORYBOARD_V6_CODE_SYNC_*`를 제자리 갱신합니다.

## V11에서 교체되는 것

- PLAY의 하단 버튼 덱과 행동 탭을 제거하고 큰 아기 직접 터치 무대로 교체
- 입·등·가슴·기저귀·팔다리·매트리스의 투명 대형 히트 영역 표시
- 추천 영역 하나만 반짝이는 규칙 명시
- 토닥 3.4초, 안기 3.2초, 아기띠 2.8초, 수유 3.4초, 쪽쪽이 2.8초 계약
- 제품 PNG 오버레이 금지와 완성 합성 스프라이트 20종 계약
- 주방의 분유통·젖병·식힘 물 직접조작과 젖병 내용물 상태
- 쪽쪽이를 소지했을 때 아기방 소품으로 항상 표시
- V10의 결정론적 일지·엔딩·기억 계약 유지

생성·교체 레이어:

- `CODE_SYNC_UNITY_PRESENTATION_V11`
- `CODE_SYNC_SETUP_PRESENTATION_V11`
- `_ACTION_MOTION_SPEC_V11`
- `_REVIEW_ACTIONS_SUMMARY`

게임 수치, 확률, 판정, 승패는 변경하지 않습니다.
