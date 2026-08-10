import Anthropic from "@anthropic-ai/sdk";

/**
 * NOT A NAP 육아일지 서술 프록시.
 *
 * 게임(WebGL)은 판정이 끝난 밤의 사실(ID·수치)만 보내고, 이 워커가 프롬프트와
 * API 키를 소유한 채 Claude를 1회 호출해 서술 문자열 5개를 돌려준다.
 * 실패하면 게임이 규칙 기반 폴백 서술을 그대로 쓰므로, 오류에 본문을 담지 않는다.
 *
 * 계약 원본: docs/narrative-proxy.md
 */

export interface Env {
  /** wrangler secret put ANTHROPIC_API_KEY — 저장소에 키를 두지 않는다. */
  ANTHROPIC_API_KEY: string;
  /** CORS 허용 오리진. 미설정 시 GitHub Pages 배포 주소만 허용. */
  ALLOWED_ORIGIN?: string;
}

const DEFAULT_ORIGIN = "https://seowoo-choi.github.io";
const CONTRACT = "diary.v2";
/** NarrativeBoundary와 같은 상한. 넘으면 클라이언트가 통째로 버린다. */
const FIELD_LIMIT = 180;

const FIELDS = [
  "noticedSignal",
  "caregiverGrowth",
  "habitReflection",
  "familyUnderstanding",
  "shareCard",
] as const;

const SYSTEM_PROMPT = `너는 턴제 육아 게임 "NOT A NAP : 백일의 밤"의 육아일지를 쓰는 서술자다.
플레이어는 21:00~06:00 동안 아기의 반복 각성을 돌본 보호자다.
밤이 끝나면 그 밤의 기록을 받아 다섯 조각의 짧은 한국어 문장을 쓴다.

## 규칙
- 각 필드는 한국어 1~2문장, 공백 포함 ${FIELD_LIMIT}자 이내. 넘으면 버려진다.
- 받은 사실만 쓴다. 일어나지 않은 일을 지어내지 않는다.
- 승패·점수·등급을 선언하지 않는다. 너는 판정하지 않고 그 밤을 서술만 한다.
- 의료 표현(진단·치료·처방·투약) 금지. 제품 추천·광고·구매 유도 금지.
- 훈수 두지 말고, 보호자가 실제로 한 선택을 담담하고 따뜻하게 비춘다.
- 아기 이름은 모른다. "아기"로 부른다.
- 이모지·해시태그·마크다운 없이 평문으로 쓴다.

## 필드
- noticedSignal: 아기가 울기 전에 보낸 신호와 보호자가 그걸 알아챈 순간.
- caregiverGrowth: 이 밤에 보호자가 달라진 점, 또는 버텨낸 방식.
- habitReflection: 가장 자주 고른 행동이 습관으로 남는다는 회고.
- familyUnderstanding: 다음 밤에 달라질 것 한 가지.
- shareCard: 한 줄 요약. 최장 수면 분과 깨어남 횟수를 자연스럽게 담는다.

## 사실 사전
밤: FirstNight 첫째 밤 / SecondNight 둘째 밤 / HundredthNight 백일째 밤
신호: Rooting 젖 찾기 · LipSmacking 입 오물거림 · HandSucking 손 빨기 ·
  Squirming 몸 뒤척임 · RapidBreathing 가쁜 숨 · Yawning 하품 · RubbingEyes 눈 비비기 ·
  ArchedBack 등 젖히기 · ClenchedFist 주먹 쥐기 · Fussing 보챔 · HungerCry 배고픔 울음 ·
  EyelidFlutter 눈꺼풀 떨림 · LimbMovement 팔다리 움직임
행동: Hold 안기 · Pat 토닥이기 · Laydown 눕히기 · Pacifier 쪽쪽이 · CheckDiaper 기저귀 확인 ·
  ChangeDiaper 기저귀 갈기 · FeedPreparedBottle 수유 · CheckEnvironment 방 상태 확인 ·
  AdjustTemperature 온도 조절 · AdjustHumidity 습도 조절 · CatchBreath 숨 고르기 ·
  ToggleCarrier 아기띠 · ToggleNoise 백색소음기 · CheckMonitor 베이비 모니터 · Grandma 할머니 찬스
장소: Nursery 아기방 · Kitchen 주방 · Bathroom 욕실
리듬: Carrier 아기띠 의존 · HeldSleep 안아 재우기 · Noise 백색소음 · SelfSoothe 스스로 진정 ·
  Neutral 아직 굳어진 습관 없음
수면 간 선택: RestTogether 같이 쉬기 · CheckEnvironment 환경 점검 · PrepareNextFeed 다음 수유 준비
값이 null이면 그 일은 일어나지 않았다는 뜻이다. 없는 일을 채우지 마라.`;

const OUTPUT_SCHEMA = {
  type: "object",
  properties: Object.fromEntries(FIELDS.map((f) => [f, { type: "string" }])),
  required: [...FIELDS],
  additionalProperties: false,
};

function corsHeaders(env: Env): Record<string, string> {
  return {
    "Access-Control-Allow-Origin": env.ALLOWED_ORIGIN ?? DEFAULT_ORIGIN,
    "Access-Control-Allow-Methods": "POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type",
    "Access-Control-Max-Age": "86400",
  };
}

/** 게임이 폴백으로 떨어지도록 본문 없이 상태 코드만 돌려준다. */
function fail(status: number, env: Env): Response {
  return new Response(null, { status, headers: corsHeaders(env) });
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    if (request.method === "OPTIONS") {
      return new Response(null, { status: 204, headers: corsHeaders(env) });
    }
    if (request.method !== "POST") return fail(405, env);
    if (!env.ANTHROPIC_API_KEY) return fail(500, env);

    let facts: unknown;
    try {
      facts = await request.json();
    } catch {
      return fail(400, env);
    }
    // 이 워커는 게임의 밤 기록만 받는다. 다른 계약은 처리하지 않는다.
    if (
      typeof facts !== "object" ||
      facts === null ||
      (facts as { contract?: unknown }).contract !== CONTRACT
    ) {
      return fail(400, env);
    }

    const client = new Anthropic({ apiKey: env.ANTHROPIC_API_KEY });

    let response: Anthropic.Message;
    try {
      response = await client.messages.create({
        model: "claude-opus-5",
        max_tokens: 2048,
        // 짧은 서술 다섯 줄이라 낮은 effort로도 충분하다. 지연과 비용을 줄인다.
        output_config: {
          effort: "low",
          format: { type: "json_schema", schema: OUTPUT_SCHEMA },
        },
        system: [
          {
            type: "text",
            text: SYSTEM_PROMPT,
            // 프롬프트는 밤마다 동일하므로 캐시해 둔다.
            cache_control: { type: "ephemeral" },
          },
        ],
        messages: [
          {
            role: "user",
            content: `오늘 밤의 기록이다. 이 사실만으로 육아일지를 써라.\n\n${JSON.stringify(facts)}`,
          },
        ],
      });
    } catch {
      return fail(502, env);
    }

    // 안전 분류기가 거절하면 content가 비거나 부분적이다. 폴백으로 넘긴다.
    if (response.stop_reason === "refusal") return fail(502, env);

    const text = response.content.find((b) => b.type === "text")?.text;
    if (!text) return fail(502, env);

    let parsed: Record<string, unknown>;
    try {
      parsed = JSON.parse(text);
    } catch {
      return fail(502, env);
    }

    // 클라이언트의 NarrativeBoundary가 한 번 더 검증하지만, 여기서 먼저 거른다.
    const narrative: Record<string, string> = {};
    for (const field of FIELDS) {
      const value = parsed[field];
      if (typeof value !== "string") return fail(502, env);
      const trimmed = value.trim();
      if (trimmed.length === 0 || trimmed.length > FIELD_LIMIT) return fail(502, env);
      narrative[field] = trimmed;
    }

    return new Response(JSON.stringify(narrative), {
      status: 200,
      headers: { ...corsHeaders(env), "Content-Type": "application/json" },
    });
  },
};
