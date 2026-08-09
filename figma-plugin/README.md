# NOT A NAP — V13 In-Place Comment Sync

현재 Unity의 큰 아기, 신체 직접 터치, 방 소품 직접조작, 완성 합성 돌봄 이미지를 기존
Figma V6 코드 싱크 보드에 반영하는 개발 플러그인입니다.

## 실행

1. Figma Desktop에서 `MOBILE_QA_STORYBOARD_V6`가 있는 파일을 엽니다.
2. `Plugins → Development → Import plugin from manifest...`에서 `manifest.json`을 고릅니다.
3. `NOT A NAP — V13 In-Place Comment Sync`를 실행합니다.

원본 `MOBILE_QA_STORYBOARD_V6`의 위치와 노드 ID를 유지한 채 제자리에서 현행화합니다.
따라서 원본 화면에 달린 Figma 댓글은 그대로 유지됩니다. 과거 플러그인이 양옆에 만든
`MOBILE_QA_STORYBOARD_V6_CODE_SYNC_*` 복제본은 삭제하지 않고 숨김 처리합니다.

## V13에서 교체되는 것

- PLAY의 하단 버튼 덱과 행동 탭을 제거하고 큰 아기 직접 터치 무대로 교체
- 입·등·가슴·기저귀·팔다리·매트리스의 투명 대형 히트 영역 표시
- 추천 영역 하나만 반짝이는 규칙 명시
- 토닥 3.4초, 안기 3.2초, 아기띠 2.8초, 수유 3.4초, 쪽쪽이 2.8초 계약
- 제품 PNG 오버레이 금지와 완성 합성 스프라이트 25종 계약
- 눕혀 재우기·기저귀 확인·기저귀 교체·팔다리 이완·체온 확인 신규 합성 아트 계약
- 욕실 체온 확인과 아기방 온·습도 숫자 확인/조정의 분리
- 체력 0 회복 선택, 밤 종료 중복 방지, 첫째→둘째→백일째→엔딩 전환 계약
- 기존 V6 HUD를 불투명 V13 화면으로 교체해 문구·상태 카드 중복 노출 방지
- 원본 보드를 복제하지 않는 인플레이스 동기화로 기존 댓글과 화면 좌표 유지
- 계약 카드의 여러 검수 메모를 실제 높이 기준으로 재배치해 글자 겹침 방지
- 주방의 분유통·젖병·식힘 물 직접조작과 젖병 내용물 상태
- 쪽쪽이를 소지했을 때 아기방 소품으로 항상 표시
- V10의 결정론적 일지·엔딩·기억 계약 유지

생성·교체 레이어:

- `CODE_SYNC_UNITY_PRESENTATION_V13`
- `CODE_SYNC_SETUP_PRESENTATION_V13`
- `_ACTION_MOTION_SPEC_V13`
- `_REVIEW_ACTIONS_SUMMARY`

게임 수치, 확률, 판정, 승패는 변경하지 않습니다.
