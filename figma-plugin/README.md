# NOT A NAP — V6 Code Sync

`MOBILE_QA_STORYBOARD_V6`의 화면 디자인은 유지하고, 현재 Unity 코드가 더 최신인 개발 계약만 동기화하는 Figma 개발 플러그인입니다.

## 실행

1. Figma Desktop에서 V6 보드가 있는 파일을 엽니다.
2. `Plugins → Development → Import plugin from manifest...`를 선택합니다.
3. 이 폴더의 `manifest.json`을 선택합니다.
4. `NOT A NAP — V6 Code Sync`를 실행합니다.

## 동작

- 원본 V6 보드를 직접 수정하지 않고 같은 페이지 오른쪽에 복제합니다.
- 복제 이름은 `MOBILE_QA_STORYBOARD_V6_CODE_SYNC_*`입니다.
- `Presenter.TryExecuteV2Action`을 실제 진입점인 `GameFlowController.ActV2`로 교체합니다.
- `M_ITEM_SCROLL`의 가상 `SelectItem` 계약을 실제 `ToggleV2Item(ItemId)` 계약으로 교체합니다.
- 현재 코드에 연결된 아기 상태 비주얼 계약을 `IMPLEMENTED`로 갱신합니다.
- `M_TIMEOUT`, `M_SLEEP_FAST_FORWARD`, `M_UNLOCK_CANDIDATES`를 현재 구현 상태로 갱신합니다.
- 백색소음기·베이비 모니터의 실제 PLAY 행동 계약을 추가합니다.
- 조기 눕히기 거부, 배고픔 단계 안내, 온·습도 숫자 표시, 다음 밤 진행 계약을 동기화합니다.
- `M_DIAPER_CHECK_WET/CLEAN`을 실제 젖음·깨끗함 결과 및 무해한 우선 검사 계약에 연결합니다.
- 수유 준비는 `BottleSanitized=true`를 기본으로 표시하고, 젖병 소독은 `EXCEPTION ONLY`로 분리합니다.
- 아이템 구성 및 PLAY/SETUP 화면 구조처럼 제품 결정이 필요한 항목은 화면을 바꾸지 않고 `REVIEW REQUIRED` 메모를 추가합니다.

플러그인은 게임 수치나 판정 계약을 변경하지 않습니다.
