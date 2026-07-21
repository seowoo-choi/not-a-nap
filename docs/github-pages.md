# GitHub Actions WebGL 배포

`.github/workflows/deploy-webgl.yml`은 `main`에 변경이 들어오거나 Actions 화면에서 수동 실행할 때
Unity WebGL을 빌드하고 GitHub Pages에 배포한다.

## 최초 1회 설정

### 1. Unity Personal 라이선스 Secret 등록

저장소의 `Settings → Secrets and variables → Actions → New repository secret`에서 다음 세 값을 만든다.

- `UNITY_LICENSE`: 로컬 Unity Personal `.ulf` 파일의 전체 내용
- `UNITY_EMAIL`: Unity 계정 이메일
- `UNITY_PASSWORD`: Unity 계정 비밀번호

macOS의 일반적인 라이선스 파일 위치는 `/Library/Application Support/Unity/Unity_lic.ulf`다.
라이선스와 계정 정보는 파일이나 커밋에 넣지 않는다.

Unity Pro를 사용할 경우 GameCI 문서에 따라 `UNITY_SERIAL` 기반 설정으로 워크플로를 조정해야 한다.

### 2. Pages 배포 소스 선택

저장소의 `Settings → Pages → Build and deployment → Source`를 **GitHub Actions**로 선택한다.

### 3. 첫 실행

`main`에 워크플로를 병합하거나 `Actions → Build and deploy WebGL → Run workflow`를 누른다.
성공하면 deploy 작업의 `github-pages` Environment URL에서 게임을 실행할 수 있다.

## 워크플로 동작

1. Unity `6000.3.20f1` 프로젝트 복원
2. `Library` 캐시 복원
3. `NotANap.Editor.WebBuildCommand.Build`로 비압축 WebGL 생성
4. 산출물이 50MiB를 넘으면 실패
5. Pages artifact 업로드 및 배포

비압축 빌드를 사용하는 이유는 GitHub Pages에서 `.br` 파일의 `Content-Encoding: br` 헤더가
누락돼 압축 바이너리를 JavaScript로 읽는 문제를 피하기 위해서다.

## 공식 참고 자료

- GameCI Unity Builder: https://game.ci/docs/github/builder/
- GameCI Unity 라이선스 활성화: https://game.ci/docs/github/activation/
- GitHub Pages custom workflow: https://docs.github.com/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages
