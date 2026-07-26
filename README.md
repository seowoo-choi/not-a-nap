# NOT A NAP : 백일의 밤

> 아기는 플레이어의 재우는 방식을 기억합니다.  
> 첫째 밤과 둘째 밤에 만든 습관이 백일째 밤의 규칙으로 돌아오는, 약 5분 분량의 턴제 육아 로그라이크입니다.

[![Build and deploy WebGL](https://github.com/seowoo-choi/not-a-nap/actions/workflows/deploy-webgl.yml/badge.svg)](https://github.com/seowoo-choi/not-a-nap/actions/workflows/deploy-webgl.yml)

NHN NAN 2026 해커톤 사전 과제로 제작한 Unity 2D WebGL 게임입니다. 목표는 아기를 가장 빨리 재우는 것이 아니라, 아기의 신호를 관찰하고 가족이 유지할 수 있는 밤 루틴을 만드는 것입니다.

## 게임 흐름

1. **첫째 밤** — 신호를 관찰하고 기본 루틴을 만듭니다.
2. **둘째 밤** — 돌발 상황 속에서 첫날의 선택을 다시 시험합니다.
3. **백일째 밤** — 쌓인 습관이 규칙과 사건으로 돌아오는 최종 밤입니다.

승리는 아래 세 조건 중 두 가지를 만족하면 됩니다.

- 아기가 깊은 잠에 든다.
- 보호자 체력을 30 이상 남긴다.
- 아기띠 없이 맨손 눕히기에 성공한다.

## 주요 특징

- 온도·습도, 기저귀, 배고픔 신호를 살펴보는 교육형 상호작용
- 맨손 안기와 아기띠 착용, 백색소음기, 베이비 모니터 등 선택형 도구
- 수면 중 `같이 쉬기 / 환경 점검 / 다음 수유 준비` 선택
- 행동과 습관이 다음 밤 및 백일째 밤의 규칙에 반영
- 모든 판정이 결정론적 순수 C# Core에서 실행
- LLM은 밤 종료 시 육아일지 서술에만 사용하며 판정에는 관여하지 않음

## 기술 구성

- Unity `6000.3.20f1`
- C# / Unity 2D
- WebGL
- Unity Test Framework
- GitHub Actions + GitHub Pages

## 로컬 실행

1. 저장소를 복제합니다.

   ```bash
   git clone https://github.com/seowoo-choi/not-a-nap.git
   cd not-a-nap
   ```

2. Unity Hub에서 프로젝트를 Unity `6000.3.20f1`로 엽니다.
3. Play 버튼을 눌러 실행합니다.

## 테스트

Unity Editor에서 `Window → General → Test Runner → EditMode`를 실행합니다.

명령행에서는 다음처럼 실행할 수 있습니다.

```bash
/Applications/Unity/Hub/Editor/6000.3.20f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics \
  -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults /tmp/not-a-nap-editmode.xml \
  -logFile /tmp/not-a-nap-editmode.log
```

## WebGL 배포

`main` 브랜치에 변경이 병합되면 GitHub Actions가 WebGL을 빌드하고 GitHub Pages에 배포합니다. Unity 라이선스 Secret과 Pages 설정이 먼저 필요합니다.

자세한 설정은 [GitHub Pages 배포 안내](docs/github-pages.md)를 참고하세요.

## 프로젝트 구조

```text
Assets/Scripts/Core/          MonoBehaviour 없는 결정론적 게임 규칙
Assets/Scripts/Presentation/  Core와 화면을 연결하는 Presenter·ViewModel
Assets/Scripts/App/           Unity 런타임 UI와 부트스트랩
Assets/Tests/EditMode/        Core·Presentation EditMode 테스트
docs/                         게임 규칙, 화면 명세, 의사결정 기록
figma-plugin/                 Figma 개발 계약 동기화 플러그인
```

## 주요 문서

- [게임 코어 설계](docs/vertical-slice-spec.md)
- [백일째 밤 규칙](docs/final-night-spec.md)
- [화면·상호작용 명세](docs/screen-spec.md)
- [Figma 모바일 개발 계약](docs/figma-mobile-handoff.md)
- [에셋 출처와 라이선스](docs/assets.md)

## 프로젝트 원칙

- 판정·수치·확률·승패는 C# Core만 결정합니다.
- API 키는 Unity 빌드와 저장소에 포함하지 않습니다.
- WebGL 빌드는 GitHub Pages에서 안정적으로 로드되도록 산출물 정합성을 검사합니다.
- 게임 범위는 아기 1명, 세 번의 밤, 5분 내외 플레이로 제한합니다.
