# NOT A NAP — Developer Storyboard V4

개발자가 화면과 클릭 분기를 바로 구현할 수 있도록 실제 1920×1080 화면 프레임 33개와 개발 계약을 생성하는 Figma 플러그인입니다.

## 실행

1. Figma Desktop → Plugins → Development → Import plugin from manifest
2. 이 폴더의 `manifest.json` 선택
3. 작업할 기존 페이지를 연 상태에서 플러그인 실행

## 보존 정책

- 새 Page를 만들지 않습니다. Starter 플랜 3페이지 제한과 충돌하지 않습니다.
- 기존 노드를 삭제하거나 덮어쓰지 않습니다.
- 실행할 때마다 현재 페이지 오른쪽 빈 공간에 새 `V4_DEVELOPER_STORYBOARD_*` 보드를 만듭니다.
- 이전 결과는 사용자가 직접 정리하기 전까지 보존됩니다.

## 생성 내용

- TITLE / SETUP
- 수면 빨리감기 / REM / NREM
- 각성 인카운터 / 기저귀 진단 / 오판 / 시간초과
- 배고픔·피로 신호 단계
- 온·습도 확인 및 조절
- 단계형 새벽 수유
- 팔 이완 확인 / 눕히기 성공·실패
- DIARY / ENDING
- 각 화면별 EntryCondition, ClickTarget, CoreAction, Resolver, StateDelta, TraceId, GameEventId, AnimationId, SfxId, NextFrameId
- 화면 내부 `NEXT` 경로 칩과 개발 계약의 `NextFrameId`

## V4.2 감정선·문구 개정

- 플레이 화면에서 NREM, CauseResolved 같은 개발 용어를 제거했습니다.
- 화면 ID는 레이어와 우측 개발 계약에만 남깁니다.
- 아빠의 서툰 거리감 → 아기의 신호 발견 → 지켜주겠다는 마음 → 새벽의 애착으로 감정선을 구성했습니다.
- 새벽 수유 뒤 눈맞춤, 팔 이완, 첫 오판, 눕히기 성공 등 작은 순간마다 아빠의 독백을 추가했습니다.
- 기존 V4 보드가 있으면 화면을 새로 만들지 않고 텍스트만 제자리에서 교체합니다.

## V4.1 화살표 정리

이전 V4가 만든 `FLOW__*` 직선은 큰 보드에서 교차와 화면 밖 돌출이 발생해 제거했습니다.
V4 보드가 이미 있는 페이지에서 플러그인을 다시 실행하면 새 보드를 만들지 않고 해당 선만 정리합니다.
화면 이동 정보는 각 화면의 `NEXT` 표시와 오른쪽 계약표의 `NextFrameId`로 확인합니다.
