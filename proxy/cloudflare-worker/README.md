# 육아일지 서술 프록시 (Cloudflare Workers)

게임은 판정이 끝난 밤의 사실(ID·수치)만 보내고, 이 워커가 프롬프트와 API 키를 소유한 채
Claude를 1회 호출해 서술 문자열 5개를 돌려준다. 요청·응답 계약은
[`docs/narrative-proxy.md`](../../docs/narrative-proxy.md)가 원본이다.

**API 키는 이 저장소 어디에도 두지 않는다.** Cloudflare의 Secret에만 존재하며,
게임 빌드가 아는 값은 이 워커의 URL 하나뿐이다.

## 배포 (5분)

```bash
cd proxy/cloudflare-worker
npm install
npx wrangler login
npx wrangler secret put ANTHROPIC_API_KEY   # 키를 붙여넣는다. 저장소에 남지 않는다.
npx wrangler deploy
```

배포가 끝나면 `https://notanap-narrative.<계정>.workers.dev` 주소가 나온다.

## 게임에 연결

GitHub 저장소 → Settings → Secrets and variables → Actions → **Variables** 탭 →
`NARRATIVE_PROXY_URL`에 위 주소를 넣는다. 다음 배포부터 실제 호출이 켜지고,
육아일지 화면에 "AI 육아일지" 표기가 뜬다.

변수를 설정하지 않으면 규칙 기반 폴백 서술로 동작한다 — 이것이 기본값이다.

## 로컬에서 확인

```bash
ANTHROPIC_API_KEY=... npx wrangler dev
```

에디터에서 게임을 실행할 때 `NOTANAP_NARRATIVE_PROXY_URL=http://localhost:8787`을
환경 변수로 주면 로컬 워커를 쓴다.

## 동작 규칙

- `contract`가 `diary.v2`가 아니면 400. 이 워커는 게임의 밤 기록만 처리한다.
- 모델은 `claude-opus-5`, effort는 `low` (짧은 서술 다섯 줄이라 충분하다).
- 응답은 structured outputs로 5개 문자열 스키마에 고정한다.
- 필드가 비었거나 180자를 넘으면 워커가 502로 떨어뜨린다 —
  게임은 규칙 기반 폴백 서술을 유지한다.
- 오류 응답에는 본문을 담지 않는다. 게임이 알아야 할 것은 실패했다는 사실뿐이다.
- 시스템 프롬프트는 밤마다 동일하므로 prompt caching을 켜 둔다.

## 다른 플랫폼으로 옮기려면

핸들러 하나에 표준 `fetch(Request) → Response`뿐이라 Vercel Edge Function이나
AWS Lambda(Function URL)로 그대로 옮겨진다. 바뀌는 것은 시크릿 주입 방식과
CORS 헤더를 붙이는 위치뿐이다.
