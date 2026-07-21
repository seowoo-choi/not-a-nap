/* NOT A NAP — Developer Storyboard V4.2
 * 기존 보드를 삭제하지 않는다. 새 페이지를 만들지 않는다.
 * 현재 페이지 오른쪽에 실제 1920×1080 화면 프레임과 개발 계약을 생성한다.
 */

(async () => {
  const fonts = await figma.listAvailableFontsAsync();
  const hasFont = (family, style) => fonts.some(f => f.fontName.family === family && f.fontName.style === style);
  let family = "Inter";
  for (const candidate of ["Pretendard", "Noto Sans KR", "Apple SD Gothic Neo", "Inter"]) {
    if (hasFont(candidate, "Regular") && hasFont(candidate, "Bold")) { family = candidate; break; }
  }
  const REG = { family, style: "Regular" };
  const BOLD = { family, style: "Bold" };
  await figma.loadFontAsync(REG);
  await figma.loadFontAsync(BOLD);

  // Existing V4 boards are updated in place; user frames and screen specs are preserved.
  const existingBoards = figma.currentPage.children.filter(
    n => n.type === "FRAME" && typeof n.name === "string" && n.name.indexOf("V4_DEVELOPER_STORYBOARD_") === 0
  );
  let removedFlowLineCount = 0;
  if (existingBoards.length) {
    for (const existingBoard of existingBoards) {
      const flowLines = existingBoard.findAll(
        n => n.type === "LINE" && typeof n.name === "string" && n.name.indexOf("FLOW__") === 0
      );
      for (const line of flowLines) { line.remove(); removedFlowLineCount += 1; }
    }
  }

  const C = {
    board: "#E9E7E2", ink: "#17191E", muted: "#686E78", line: "#C9CDD3",
    night: "#081019", panel: "#132131", panel2: "#1B2D40", text: "#F6F1E8",
    sub: "#AAB7C5", warm: "#E7B763", blue: "#72B7E6", green: "#65CF92",
    red: "#E87370", orange: "#F0A954", purple: "#9A86E8", white: "#FFFFFF",
    spec: "#F7F7F8", safe: "#DFF3E8", darkGreen: "#276C4D"
  };
  const rgb = hex => {
    const n = parseInt(hex.slice(1), 16);
    return { r: ((n >> 16) & 255) / 255, g: ((n >> 8) & 255) / 255, b: (n & 255) / 255 };
  };
  const paint = hex => [{ type: "SOLID", color: rgb(hex) }];

  function rect(parent, x, y, w, h, fill, radius, name) {
    const n = figma.createRectangle();
    n.name = name || "Rectangle";
    n.resize(w, h); n.x = x; n.y = y;
    n.fills = paint(fill); n.cornerRadius = radius || 0;
    parent.appendChild(n); return n;
  }

  function label(parent, str, x, y, w, size, color, bold, name, align) {
    const t = figma.createText();
    t.name = name || "Text";
    t.fontName = bold ? BOLD : REG;
    t.characters = String(str);
    t.fontSize = size;
    t.fills = paint(color);
    t.textAutoResize = "HEIGHT";
    t.resize(w, Math.max(size * 1.5, 10));
    t.x = x; t.y = y;
    t.lineHeight = { value: 138, unit: "PERCENT" };
    if (align) t.textAlignHorizontal = align;
    parent.appendChild(t); return t;
  }

  function button(parent, textValue, x, y, w, h, tone, selected) {
    const bg = selected ? tone : C.panel2;
    const r = rect(parent, x, y, w, h, bg, 16, "BTN_" + textValue);
    r.strokes = paint(tone); r.strokeWeight = selected ? 3 : 1.5;
    label(parent, textValue, x + 18, y + 19, w - 36, 26, selected ? C.night : C.text, true, "TXT_" + textValue, "CENTER");
    return r;
  }

  function chip(parent, value, x, y, color) {
    const width = Math.max(126, value.length * 16 + 38);
    rect(parent, x, y, width, 42, C.panel2, 21, "CHIP_" + value);
    label(parent, value, x + 14, y + 9, width - 28, 18, color, true, "TXT_CHIP");
    return width;
  }

  function baby(parent, state) {
    const cx = 720, cy = 405;
    rect(parent, 390, 230, 660, 410, C.panel, 28, "ROOM_STAGE");
    rect(parent, 480, 550, 480, 34, "#D9D1C2", 15, "CRIB");
    const body = figma.createEllipse(); body.resize(190, 130); body.x = cx - 95; body.y = cy + 45; body.fills = paint("#8FA8BE"); body.name = "BABY_BODY"; parent.appendChild(body);
    const head = figma.createEllipse(); head.resize(126, 126); head.x = cx - 63; head.y = cy - 35; head.fills = paint("#DDA789"); head.name = "BABY_HEAD"; parent.appendChild(head);
    const eyeY = cy + 11;
    if (state === "cry" || state === "wake" || state === "moro") {
      rect(parent, cx - 38, eyeY, 22, 8, C.night, 4, "EYE_L");
      rect(parent, cx + 16, eyeY, 22, 8, C.night, 4, "EYE_R");
    } else if (state === "rem") {
      rect(parent, cx - 38, eyeY, 23, 4, C.night, 2, "EYE_FLUTTER_L");
      rect(parent, cx + 15, eyeY + 4, 23, 4, C.night, 2, "EYE_FLUTTER_R");
    } else {
      rect(parent, cx - 38, eyeY + 4, 23, 3, C.night, 2, "EYE_CLOSED_L");
      rect(parent, cx + 15, eyeY + 4, 23, 3, C.night, 2, "EYE_CLOSED_R");
    }
    const mouth = figma.createEllipse(); mouth.resize(state === "cry" ? 30 : 18, state === "cry" ? 30 : 8); mouth.x = cx - mouth.width / 2; mouth.y = cy + 45; mouth.fills = paint(state === "cry" ? C.red : "#9A5F57"); mouth.name = "BABY_MOUTH"; parent.appendChild(mouth);
    if (state === "nrem") {
      rect(parent, cx - 152, cy + 74, 100, 18, "#8FA8BE", 9, "ARM_RELAXED_L");
      rect(parent, cx + 52, cy + 74, 100, 18, "#8FA8BE", 9, "ARM_RELAXED_R");
    } else if (state === "moro") {
      const left = rect(parent, cx - 168, cy + 24, 120, 18, "#8FA8BE", 9, "ARM_STARTLE_L"); left.rotation = -25;
      const right = rect(parent, cx + 48, cy + 24, 120, 18, "#8FA8BE", 9, "ARM_STARTLE_R"); right.rotation = 25;
    } else {
      rect(parent, cx - 132, cy + 60, 82, 18, "#8FA8BE", 9, "ARM_L");
      rect(parent, cx + 50, cy + 60, 82, 18, "#8FA8BE", 9, "ARM_R");
    }
  }

  function makeScreen(def) {
    const f = figma.createFrame();
    f.name = def.id; f.resize(1920, 1080); f.fills = paint(C.night); f.cornerRadius = 28; f.clipsContent = true;
    rect(f, 0, 0, 1920, 102, "#0D1824", 0, "HUD_BG");
    label(f, def.hud || "첫째 밤", 44, 25, 430, 28, C.text, true, "SCREEN_ID");
    label(f, def.time || "02:14", 565, 24, 160, 30, C.warm, true, "CLOCK");
    chip(f, "현재 " + (def.streak || "38분"), 760, 28, C.blue);
    chip(f, "최장 " + (def.longest || "152분"), 950, 28, C.green);
    chip(f, "체력 " + (def.stamina || "64"), 1150, 28, C.orange);
    chip(f, def.environment || "21°C · 52%", 1328, 28, C.sub);

    baby(f, def.baby || "wake");
    label(f, def.stage || "깨어남", 420, 660, 600, 34, def.tone || C.text, true, "BABY_STATE", "CENTER");
    label(f, def.signal || "아기의 신호를 살펴보세요.", 420, 712, 600, 24, C.sub, false, "SIGNAL", "CENTER");

    rect(f, 1090, 166, 770, 608, C.panel, 24, "DECISION_PANEL");
    label(f, def.title, 1130, 200, 690, 38, C.text, true, "EVENT_TITLE");
    if (def.timer) {
      rect(f, 1660, 194, 150, 66, C.red, 33, "TIMER");
      label(f, def.timer, 1678, 209, 114, 28, C.white, true, "TXT_TIMER", "CENTER");
    }
    label(f, def.body || "현재 상태를 확인하고 다음 행동을 선택하세요.", 1130, 278, 680, 25, C.sub, false, "EVENT_BODY");

    const actions = def.actions || ["기저귀 확인", "관찰", "안기", "토닥이기", "수유 준비", "온·습도", "눕히기", "쪽쪽이"];
    actions.slice(0, 8).forEach((a, i) => {
      const col = i % 2, row = Math.floor(i / 2);
      button(f, a, 1130 + col * 340, 410 + row * 82, 314, 64, i === (def.primaryIndex || 0) ? (def.tone || C.warm) : C.blue, i === (def.primaryIndex || 0));
    });
    rect(f, 54, 820, 1812, 210, "#0D1824", 22, "LOG_PANEL");
    label(f, "아빠의 밤 기록", 86, 846, 240, 20, C.blue, true, "LOG_TITLE");
    label(f, def.log || "02:14  아기가 움직였다. 원인은 아직 알 수 없다.", 86, 890, 1720, 25, C.text, false, "LOG_TEXT");
    label(f, def.feedback || "관찰과 선택 결과가 이 영역에 누적됩니다.", 86, 946, 1720, 21, def.tone || C.sub, false, "LOG_FEEDBACK");
    return f;
  }

  function makeContract(def) {
    const f = figma.createFrame(); f.name = def.id + "__DEV_CONTRACT"; f.resize(720, 1080); f.fills = paint(C.spec); f.cornerRadius = 28; f.clipsContent = true;
    rect(f, 0, 0, 720, 116, def.tone || C.blue, 0, "CONTRACT_HEADER");
    label(f, "개발 계약", 34, 26, 260, 26, C.night, true, "CONTRACT_TITLE");
    label(f, def.id, 34, 64, 640, 22, C.night, true, "CONTRACT_ID");
    const rows = [
      ["EntryCondition", def.entry], ["ClickTarget", def.click], ["CoreAction", def.core],
      ["Resolver", def.resolver], ["StateDelta", def.delta], ["TraceId", def.trace],
      ["GameEventId", def.event], ["AnimationId", def.anim], ["SfxId", def.sfx],
      ["NextFrameId", (def.routes || []).map(r => r.to).join(" / ")]
    ];
    let y = 146;
    rows.forEach(([k, v]) => {
      label(f, k, 34, y, 188, 18, C.muted, true, "KEY_" + k);
      label(f, v || "—", 224, y, 458, 19, C.ink, false, "VALUE_" + k);
      y += 82;
    });
    rect(f, 28, 970, 664, 76, C.safe, 14, "SAFETY_NOTE");
    label(f, "판정은 C# Core. View는 수치를 직접 바꾸지 않는다.", 48, 992, 624, 18, C.darkGreen, true, "SAFETY_TEXT");
    return f;
  }

  const S = (id, title, extra) => Object.assign({
    id, title, body: "화면 신호를 확인하고 행동을 선택한다.", baby: "wake", stage: "깨어남",
    entry: "GameFlowController가 화면을 활성화", click: "표시된 기본 버튼",
    core: "GameSessionPresenter.TryExecuteAction(...)" , resolver: "ActionResolver / TurnResolver",
    delta: "Core 결과에서 읽음", trace: "해당 없음", event: "해당 없음",
    anim: "ANIM_BABY_STATE", sfx: "SFX_UI_CONFIRM", tone: C.blue, routes: []
  }, extra || {});

  const defs = [
    S("TITLE", "오늘 밤은 아빠 차례다", {stage:"게임 시작", baby:"nrem", actions:["시작하기"], entry:"앱 실행", click:"BTN_GAME_START", core:"CreateRunState", resolver:"RunFactory", delta:"새 RunState", anim:"ANIM_DOOR_CLOSE_INTRO", sfx:"BGM_TITLE", routes:[{to:"SETUP",tone:"blue"}]}),
    S("SETUP", "밤 준비", {stage:"21:00 · 준비", baby:"wake", actions:["아기띠","쪽쪽이","백색소음기","모니터","분유제조기","밤 시작"], entry:"RunState 생성 완료", click:"BTN_START_NIGHT", core:"NightFactory.Create", resolver:"NightFactory", delta:"NightState 생성", trace:"선택 장비 목록", routes:[{to:"PLAY_SLEEPING",tone:"blue"}]}),
    S("PLAY_SLEEPING", "자는 동안 시간이 흐른다", {baby:"nrem",stage:"NREM · 안정 수면",signal:"규칙적인 호흡 · 팔 이완",body:"수면 블록을 빠르게 진행하며 연속 수면을 누적한다.",actions:["빨리감기","상태 관찰"],entry:"LaydownSucceeded 또는 재입면",click:"BTN_FAST_FORWARD",core:"AdvanceSleepBlock",resolver:"WakeScheduler",delta:"Clock+ / Streak+ / Hunger+",anim:"ANIM_CLOCK_FAST_FORWARD",sfx:"SFX_CLOCK_TICK",routes:[{to:"PLAY_REM",tone:"observe"},{to:"PLAY_WAKE_ENCOUNTER",tone:"bad"}]}),
    S("PLAY_REM", "활동수면이 표면화됐다", {baby:"rem",stage:"REM · 활동수면",signal:"눈꺼풀 떨림 · 불규칙 호흡 · 팔다리 움직임",actions:["지켜보기","안기","팔 이완 확인"],entry:"SleepCycle REM 구간",click:"BTN_WATCH",core:"GameAction.Watch",resolver:"SleepCycleResolver",delta:"개입 최소 시 streak 유지",anim:"ANIM_BABY_REM",sfx:"SFX_BABY_STIR",routes:[{to:"PLAY_NREM",tone:"good"},{to:"PLAY_WAKE_ENCOUNTER",tone:"bad"}]}),
    S("PLAY_NREM", "깊은 잠의 신호", {baby:"nrem",stage:"NREM · 비활동수면",signal:"평온한 얼굴 · 규칙적인 호흡 · 축 늘어진 팔",actions:["팔 이완 확인","눕히기","지켜보기"],entry:"SleepCycle NREM 진입",click:"BTN_CHECK_LIMB",core:"CheckLimbRelaxation",resolver:"ObservationResolver",delta:"관찰 결과만 반환",trace:"trace.sleep.deep_signals_observed",anim:"ANIM_BABY_NREM",sfx:"SFX_BREATH_SLOW",routes:[{to:"PLAY_ARM_CHECK_NREM",tone:"observe"}]}),
    S("PLAY_WAKE_ENCOUNTER", "왜 깼을까?", {baby:"cry",stage:"완전 각성 · 원인 미확정",signal:"울음이 시작됐다. 8초 안에 판단하세요.",timer:"00:08",body:"원인은 숨겨져 있다. 기저귀부터 확인하는 것이 안전한 첫 단계다.",actions:["기저귀 확인","관찰","바로 안기","수유 준비","온·습도","쪽쪽이","지켜보기","시간초과"],entry:"WakeScheduler FullWake",click:"DiagnosisActionPanel",core:"EncounterAction",resolver:"DiagnosisResolver",delta:"선택에 따라 원인 공개/오판",event:"GameEventId.WakeEncounterStarted",anim:"ANIM_BABY_CRY",sfx:"SFX_BABY_CRY",tone:C.red,routes:[{to:"PLAY_DIAPER_CHECK",tone:"good"},{to:"PLAY_SIGNAL_OBSERVED",tone:"observe"},{to:"PLAY_MISJUDGE_HOLD",tone:"bad"},{to:"PLAY_FEED_PREP_STERILIZE",tone:"blue"},{to:"PLAY_ENVIRONMENT_CHECK",tone:"observe"},{to:"PLAY_TIMEOUT",tone:"bad"}]}),
    S("PLAY_DIAPER_CHECK", "기저귀를 확인한다", {baby:"cry",stage:"진단 중",actions:["기저귀 열어보기"],entry:"CheckDiaper 선택",click:"BTN_CHECK_DIAPER",core:"GameAction.CheckDiaper",resolver:"DiagnosisResolver",delta:"Diaper 검사 결과 공개",trace:"trace.check.diaper_first",anim:"ANIM_CHECK_DIAPER",sfx:"SFX_CLOTH",tone:C.green,routes:[{to:"PLAY_DIAPER_WET",tone:"good"},{to:"PLAY_DIAPER_DRY",tone:"observe"}]}),
    S("PLAY_DIAPER_WET", "기저귀가 축축하다", {baby:"cry",stage:"원인 발견 · Diaper",signal:"기저귀를 갈아주면 해결할 수 있다.",actions:["기저귀 갈아주기"],entry:"CheckDiaper 결과 Wet",click:"BTN_CHANGE_DIAPER",core:"GameAction.ChangeDiaper",resolver:"DiagnosisResolver",delta:"CauseResolved / Calm+",trace:"trace.cause.diaper_resolved",event:"GameEventId.CauseRevealed",anim:"ANIM_CHANGE_DIAPER",sfx:"SFX_DIAPER",tone:C.green,routes:[{to:"PLAY_CALMED",tone:"good"}]}),
    S("PLAY_DIAPER_DRY", "기저귀는 보송하다", {baby:"cry",stage:"Diaper 배제",signal:"배고픔·피로·환경 신호를 계속 관찰해야 한다.",actions:["관찰","온·습도","안기"],entry:"CheckDiaper 결과 Dry",click:"BTN_OBSERVE",core:"GameAction.Observe",resolver:"ObservationResolver",delta:"CheckedCauses += Diaper",anim:"ANIM_DIAPER_CLOSE",sfx:"SFX_UI_INFO",routes:[{to:"PLAY_SIGNAL_OBSERVED",tone:"observe"}]}),
    S("PLAY_CALMED", "원인이 해결됐다", {baby:"wake",stage:"진정됨",signal:"호흡이 차츰 고르게 돌아온다.",actions:["안기","토닥이기","졸림 신호 관찰"],entry:"CauseResolved=true",click:"BTN_HOLD",core:"GameAction.Hold",resolver:"ActionResolver",delta:"Calm+ / Crying=false",event:"GameEventId.CauseResolved",anim:"ANIM_BABY_CALM",sfx:"SFX_BABY_SETTLE",tone:C.green,routes:[{to:"PLAY_FATIGUE_EARLY",tone:"observe"}]}),
    S("PLAY_MISJUDGE_HOLD", "품에 안아도 울음이 멎지 않는다", {baby:"cry",stage:"오판 · 원인 미해결",signal:"축축한 감촉이 그대로다.",actions:["기저귀 확인","다시 관찰"],entry:"기저귀 미확인 상태에서 Hold",click:"BTN_CHECK_DIAPER",core:"MisdiagnosisResult",resolver:"DiagnosisResolver",delta:"Time+ / Stamina- / CryIntensity+",trace:"trace.misread.hold_before_check",event:"GameEventId.Misdiagnosed",anim:"ANIM_HOLD_NO_RELIEF",sfx:"SFX_UI_WARN",tone:C.red,routes:[{to:"PLAY_DIAPER_CHECK",tone:"observe"}]}),
    S("PLAY_MISJUDGE_FEED", "젖병을 밀어낸다", {baby:"cry",stage:"오판 · 배고픔 아님",signal:"준비에 쓴 시간은 되돌아오지 않는다.",actions:["기저귀 확인","관찰"],entry:"Hunger 아닌데 FeedPreparedBottle",click:"BTN_OBSERVE",core:"FeedPreparedBottle",resolver:"FeedingResolver",delta:"Time 유지 / Misjudgment+",trace:"trace.feed.not_hunger",event:"GameEventId.FeedRejected",anim:"ANIM_BABY_FEED_REJECT",sfx:"SFX_BOTTLE_REJECT",tone:C.red,routes:[{to:"PLAY_DIAPER_CHECK",tone:"observe"}]}),
    S("PLAY_SIGNAL_OBSERVED", "관찰한 신호", {baby:"wake",stage:"관찰 결과",signal:"입맛을 다시고 손을 입으로 가져간다.",actions:["배고픔 대응","피로 대응","온·습도 확인"],entry:"Observe 완료",click:"관찰 결과 CTA",core:"ObservationResult.Signals",resolver:"ObservationResolver",delta:"상태 변화 없음",trace:"trace.signals.observed",anim:"ANIM_SIGNAL_HIGHLIGHT",sfx:"SFX_UI_INFO",routes:[{to:"PLAY_HUNGER_EARLY",tone:"observe"},{to:"PLAY_FATIGUE_EARLY",tone:"observe"},{to:"PLAY_ENVIRONMENT_CHECK",tone:"observe"}]}),
    S("PLAY_HUNGER_EARLY", "배고픔 초기 신호", {baby:"wake",stage:"Hunger · Early",signal:"입맛 다시기 · 입 열고 닫기 · 손 빨기",actions:["수유 준비","계속 관찰"],entry:"HungerSignal=Early",click:"BTN_PREPARE_FEED",core:"StartFeedingPreparation",resolver:"HungerSignalResolver",delta:"선제 대응 보너스 가능",anim:"ANIM_HAND_SUCK",sfx:"SFX_BABY_COOT",routes:[{to:"PLAY_FEED_PREP_STERILIZE",tone:"blue"},{to:"PLAY_HUNGER_MID",tone:"bad"}]}),
    S("PLAY_HUNGER_MID", "배고픔 진행 신호", {baby:"wake",stage:"Hunger · Mid",signal:"몸 기대기 · 꼼지락 · 팔 두드림 · 빠른 호흡",actions:["수유 준비","안고 진정"],entry:"HungerSignal=Mid",click:"BTN_PREPARE_FEED",core:"StartFeedingPreparation",resolver:"HungerSignalResolver",delta:"Cry 상승 전 준비",anim:"ANIM_ROOTING",sfx:"SFX_BABY_FUSS",tone:C.orange,routes:[{to:"PLAY_FEED_PREP_STERILIZE",tone:"blue"},{to:"PLAY_HUNGER_LATE",tone:"bad"}]}),
    S("PLAY_HUNGER_LATE", "배고픔 후기 신호", {baby:"cry",stage:"Hunger · Late",signal:"머리를 좌우로 돌리며 크게 운다.",actions:["안고 준비하기","수유 준비"],entry:"HungerSignal=Late",click:"BTN_HOLD_WHILE_PREPARING",core:"HoldWhilePreparing",resolver:"FeedingResolver",delta:"Cry 악화 지연 / Stamina-",anim:"ANIM_HUNGER_CRY",sfx:"SFX_BABY_CRY",tone:C.red,routes:[{to:"PLAY_FEED_PREP_STERILIZE",tone:"blue"}]}),
    S("PLAY_FATIGUE_EARLY", "피로 초기 신호", {baby:"wake",stage:"Fatigue · Early",signal:"눈썹이 붉고 시선을 피하며 멍하게 본다.",actions:["자극 줄이기","안기","토닥이기"],entry:"FatigueSignal=Early",click:"BTN_REDUCE_STIMULUS",core:"GameAction.ReduceStimulus",resolver:"FatigueResolver",delta:"Overtired 예방",anim:"ANIM_AVOID_GAZE",sfx:"SFX_ROOM_QUIET",routes:[{to:"PLAY_FATIGUE_MID",tone:"observe"},{to:"PLAY_REM",tone:"good"}]}),
    S("PLAY_FATIGUE_MID", "피로 진행 신호", {baby:"wake",stage:"Fatigue · Mid",signal:"하품 · 눈 비빔 · 귀/머리카락 당김",actions:["안기","토닥이기","자극 줄이기"],entry:"FatigueSignal=Mid",click:"BTN_HOLD",core:"GameAction.Hold",resolver:"FatigueResolver",delta:"Calm+ / Sleep+",anim:"ANIM_YAWN_RUB_EYES",sfx:"SFX_YAWN",tone:C.orange,routes:[{to:"PLAY_NREM",tone:"good"},{to:"PLAY_FATIGUE_LATE",tone:"bad"}]}),
    S("PLAY_FATIGUE_LATE", "과피로 신호", {baby:"cry",stage:"Fatigue · Late",signal:"등을 활처럼 휘고 주먹을 꽉 쥐며 심하게 운다.",actions:["안기","일정한 토닥임","자극 차단"],entry:"FatigueSignal=Late",click:"BTN_HOLD",core:"GameAction.Hold + Pat",resolver:"OvertiredResolver",delta:"진정 난이도 증가",anim:"ANIM_ARCH_BACK_CRY",sfx:"SFX_BABY_CRY_HARD",tone:C.red,routes:[{to:"PLAY_CALMED",tone:"good"}]}),
    S("PLAY_ENVIRONMENT_CHECK", "온도와 습도를 확인한다", {baby:"cry",stage:"환경 진단",signal:"현재 환경값이 공개된다.",environment:"24°C · 34%",actions:["온도 낮추기","습도 올리기","뒤로"],entry:"CheckEnvironment",click:"BTN_CHECK_ENVIRONMENT",core:"GameAction.CheckEnvironment",resolver:"EnvironmentResolver",delta:"IsChecked=true",trace:"trace.environment.checked",anim:"ANIM_GAUGE_REVEAL",sfx:"SFX_UI_INFO",routes:[{to:"PLAY_ENVIRONMENT_ADJUST",tone:"observe"}]}),
    S("PLAY_ENVIRONMENT_ADJUST", "환경을 조절한다", {baby:"wake",stage:"조절 중",signal:"목표 범위까지 시간이 흐른다.",environment:"22°C · 48%",actions:["조절 완료","다시 확인"],entry:"환경 범위 이탈 확인",click:"BTN_ADJUST_ENVIRONMENT",core:"AdjustTemperature / AdjustHumidity",resolver:"EnvironmentResolver",delta:"Clock+ / Stamina- / Environment 보정",event:"GameEventId.EnvironmentAdjusted",anim:"ANIM_GAUGE_MOVE",sfx:"SFX_DEVICE",tone:C.green,routes:[{to:"PLAY_CALMED",tone:"good"}]}),
    S("PLAY_FEED_PREP_STERILIZE", "1/5 젖병 소독 상태", {baby:"cry",stage:"새벽 수유 준비",signal:"미리 소독했으면 이 단계를 건너뛴다.",actions:["소독하기","사전 소독 확인","안고 진정"],entry:"StartFeedingPreparation",click:"BTN_STERILIZE_BOTTLE",core:"SterilizeBottle",resolver:"FeedingPreparationResolver",delta:"BottleSanitized=true / Clock+",trace:"trace.feed.bottle_sanitized",anim:"ANIM_STERILIZER",sfx:"SFX_STERILIZER",routes:[{to:"PLAY_FEED_PREP_WATER",tone:"blue"}]}),
    S("PLAY_FEED_PREP_WATER", "2/5 물 준비", {baby:"cry",stage:"새벽 수유 준비",signal:"기기가 없으면 준비와 대기 시간이 든다.",actions:["물 준비","자동 준비 사용","안고 진정"],entry:"BottleSanitized",click:"BTN_PREPARE_WATER",core:"PrepareWater",resolver:"FeedingPreparationResolver",delta:"WaterReady=true / Clock+",anim:"ANIM_WATER_PREP",sfx:"SFX_WATER",routes:[{to:"PLAY_FEED_PREP_MEASURE",tone:"blue"}]}),
    S("PLAY_FEED_PREP_MEASURE", "3/5 분유 계량", {baby:"cry",stage:"새벽 수유 준비",signal:"제품 설정에서 정확한 양을 읽는다.",actions:["분유 계량","안고 진정"],entry:"WaterReady",click:"BTN_MEASURE_FORMULA",core:"MeasureFormula",resolver:"FeedingPreparationResolver",delta:"FormulaMeasured=true",anim:"ANIM_FORMULA_MEASURE",sfx:"SFX_SCOOP",routes:[{to:"PLAY_FEED_PREP_COOL",tone:"blue"}]}),
    S("PLAY_FEED_PREP_COOL", "4/5 섞고 식히기", {baby:"cry",stage:"새벽 수유 준비",signal:"허용 범위는 FeedingSafetyConfig에서 읽는다.",actions:["섞기","식히기","온도 확인"],entry:"WaterReady && FormulaMeasured",click:"BTN_COOL_BOTTLE",core:"MixFormula / CoolBottle",resolver:"FeedingPreparationResolver",delta:"BottleMixed/Cooled=true",anim:"ANIM_BOTTLE_COOL",sfx:"SFX_BOTTLE_SHAKE",routes:[{to:"PLAY_FEED_PREP_TEMP_CHECK",tone:"blue"}]}),
    S("PLAY_FEED_PREP_TEMP_CHECK", "5/5 온도 확인", {baby:"cry",stage:"새벽 수유 준비",signal:"뜨거우면 수유 버튼이 잠긴다.",actions:["온도 확인","더 식히기","수유하기"],entry:"BottleMixed && BottleCooled",click:"BTN_CHECK_BOTTLE_TEMP",core:"CheckBottleTemperature",resolver:"FeedingPreparationResolver",delta:"TemperatureChecked=true",anim:"ANIM_TEMP_CHECK",sfx:"SFX_UI_CONFIRM",routes:[{to:"PLAY_FEEDING",tone:"good"},{to:"PLAY_FEED_PREP_COOL",tone:"observe"}]}),
    S("PLAY_FEEDING", "수유한다", {baby:"wake",stage:"수유 중",signal:"배고픔 원인이면 호흡과 울음이 잦아든다.",actions:["수유 계속","트림시키기"],entry:"IsReadyToFeed=true",click:"BTN_FEED_PREPARED",core:"FeedPreparedBottle",resolver:"FeedingResolver",delta:"Hunger- / Calm+ / CauseResolved",trace:"trace.feed.preparation_completed",event:"GameEventId.FeedAccepted / GameEventId.DawnSmileReward",anim:"ANIM_BABY_FEED / ANIM_BABY_EYE_CONTACT_SMILE",sfx:"SFX_BOTTLE_ACCEPT",tone:C.green,routes:[{to:"PLAY_CALMED",tone:"good"},{to:"PLAY_MISJUDGE_FEED",tone:"bad"}]}),
    S("PLAY_ARM_CHECK_REM", "아직 팔에 힘이 남아 있다", {baby:"rem",stage:"REM · 이동 위험",signal:"눈꺼풀이 떨리고 호흡도 불규칙하다.",actions:["더 기다리기","지켜보기"],entry:"CheckLimbRelaxation in REM",click:"BTN_WATCH",core:"CheckLimbRelaxation",resolver:"ObservationResolver",delta:"관찰만 / 상태 변경 없음",anim:"ANIM_ARM_TENSION",sfx:"SFX_BABY_STIR",tone:C.orange,routes:[{to:"PLAY_NREM",tone:"observe"}]}),
    S("PLAY_ARM_CHECK_NREM", "팔과 손에서 힘이 빠졌다", {baby:"nrem",stage:"NREM · 이동 적기",signal:"규칙적인 호흡과 팔 이완을 함께 확인했다.",actions:["눕히기","조금 더 기다리기"],entry:"CheckLimbRelaxation in NREM",click:"BTN_LAYDOWN",core:"GameAction.Laydown",resolver:"LaydownResolver",delta:"DeepSleepObserved=true",trace:"trace.sleep.deep_signals_observed",anim:"ANIM_ARM_RELAXED",sfx:"SFX_BREATH_SLOW",tone:C.green,routes:[{to:"PLAY_LAYDOWN_SUCCESS",tone:"good"},{to:"PLAY_LAYDOWN_FAIL_MORO",tone:"bad"}]}),
    S("PLAY_LAYDOWN_SUCCESS", "침대에서도 잠을 이어간다", {baby:"nrem",stage:"눕히기 성공 · 밤 계속",signal:"성공은 밤 종료가 아니라 연속 수면 시작이다.",actions:["계속"],entry:"LaydownSucceeded",click:"BTN_CONTINUE",core:"ScheduleNextWake",resolver:"WakeScheduler",delta:"Streak 시작 / Night.Over=false",event:"GameEventId.LaydownSucceeded",anim:"ANIM_LAYDOWN_SUCCESS",sfx:"SFX_LAYDOWN_SUCCESS",tone:C.green,routes:[{to:"PLAY_SLEEPING",tone:"good"}]}),
    S("PLAY_LAYDOWN_FAIL_MORO", "등이 닿는 순간 몸이 움찔했다", {baby:"moro",stage:"모로반사 · 완전 각성",signal:"REM에 성급히 옮겨 연속 수면이 끊겼다.",actions:["다시 안기","낮은 자극","토닥이기"],entry:"LaydownFailed by Moro",click:"BTN_HOLD",core:"GameAction.Hold",resolver:"LaydownResolver",delta:"FullWake / Streak=0 / Calm-",trace:"trace.laydown.moro_wake",event:"GameEventId.LaydownFailed",anim:"ANIM_MORO_STARTLE",sfx:"SFX_LAYDOWN_FAILED",tone:C.red,routes:[{to:"PLAY_CALMED",tone:"observe"}]}),
    S("PLAY_SLEEP_FAST_FORWARD", "연속 수면이 쌓인다", {baby:"nrem",stage:"자동 시간 진행",signal:"현재 2시간 38분 · 최장 2시간 38분",streak:"158분",longest:"158분",actions:["다음 수면 블록"],entry:"Sleeping && no encounter",click:"BTN_FAST_FORWARD",core:"AdvanceSleepBlock",resolver:"SleepCycleResolver / WakeScheduler",delta:"Clock+ / Streak+",anim:"ANIM_NIGHT_FAST_FORWARD",sfx:"SFX_CLOCK_ADVANCE",tone:C.blue,routes:[{to:"PLAY_WAKE_ENCOUNTER",tone:"bad"},{to:"DIARY",tone:"good"}]}),
    S("PLAY_TIMEOUT", "판단이 늦었다", {baby:"cry",stage:"대성통곡 · 해결 난이도 상승",signal:"제한시간이 끝나 각성이 심해졌다.",timer:"00:00",actions:["기저귀 확인","관찰"],entry:"Encounter timer expired",click:"Presentation → Timeout",core:"EncounterAction.Timeout",resolver:"DiagnosisResolver",delta:"Clock+ / Calm- / CryIntensity+",trace:"trace.encounter.timeout",event:"GameEventId.EncounterTimedOut",anim:"ANIM_CRY_ESCALATE",sfx:"SFX_TIMEOUT",tone:C.red,routes:[{to:"PLAY_DIAPER_CHECK",tone:"observe"}]}),
    S("DIARY", "밤 결과", {baby:"nrem",stage:"아침 06:00",signal:"최장 연속 수면 152분 · LongStretch",time:"06:00",streak:"0분",longest:"152분",actions:["다음 밤","기록 보기"],entry:"NightClockMinutes >= 540",click:"BTN_NEXT_NIGHT",core:"EvaluateNight / ConsolidateMemory",resolver:"NightEvaluationResolver",delta:"NightSleepGrade / Memory",trace:"그 밤의 Trace 요약",event:"GameEventId.NightCompleted",anim:"ANIM_DAWN",sfx:"BGM_DAWN_RESULT",tone:C.green,routes:[{to:"ENDING",tone:"blue"},{to:"SETUP",tone:"blue"}]}),
    S("ENDING", "이제 당신도 이 아이의 밤을 안다", {baby:"nrem",stage:"엔딩",signal:"연속 수면·체력·진단 정확도로 결과가 정해진다.",time:"06:00",actions:["다시 시작"],entry:"EndingResolver completed",click:"BTN_RESTART",core:"ResetRun",resolver:"EndingResolver",delta:"새 런 준비",event:"GameEventId.EndingResolved",anim:"ANIM_ENDING",sfx:"BGM_ENDING",tone:C.purple,routes:[{to:"TITLE",tone:"blue"}]})
  ];

  // Player-facing copy: cinematic but restrained. Technical terms stay in the dev contract only.
  const UI = {
    TITLE:{hud:"NOT A NAP",title:"오늘 밤은 내가 재울게",stage:"작은 숨소리가 들린다",signal:"아직은 낯설고, 그래서 더 오래 바라보게 된다.",body:"서툴러도 괜찮아. 오늘 밤은 내가 네 곁에 있을게.",actions:["아기 곁으로"],log:"문이 닫히자 방 안에는 우리 둘만 남았다.",feedback:"네가 나를 알아보기 전에, 내가 먼저 너를 알아가 보기로 했다."},
    SETUP:{hud:"첫째 밤 준비",title:"불이 꺼지기 전에",stage:"밤 9시 · 아직은 말똥말똥",signal:"멀리서 난 소리에도 작은 어깨가 움찔한다.",body:"오늘 밤 필요한 것만 챙기자. 결국 가장 오래 곁에 있을 건 내 두 팔이다.",actions:["아기띠","쪽쪽이","백색소음","모니터","수유 준비기","불 끄기"],log:"가방은 가볍게, 마음은 단단하게.",feedback:"준비가 끝나면 오늘 밤의 첫 숨을 함께 센다."},
    PLAY_SLEEPING:{hud:"첫째 밤",title:"드디어 숨이 고르게 이어진다",stage:"깊이 잠든 듯하다",signal:"배가 천천히 오르내리고, 손끝이 느슨해졌다.",body:"괜히 건드리지 말자. 지금은 곁에 있는 것만으로 충분하다.",actions:["조용히 기다리기","숨소리 살피기"],log:"02:14  작은 숨이 하나, 또 하나 이어진다.",feedback:"아무것도 하지 않는 것도 돌봄이라는 걸 조금 알 것 같다."},
    PLAY_REM:{hud:"첫째 밤",title:"잠결에 몸을 꼼지락거린다",stage:"선잠을 지나는 중",signal:"눈꺼풀이 떨리고 입꼬리가 잠깐 움직였다.",body:"깬 걸까? 아니면 꿈을 꾸는 걸까. 조금만 더 지켜보자.",actions:["조금 더 보기","가만히 손 얹기","팔에 힘 보기"],log:"02:14  작은 움직임에 나도 모르게 숨을 멈췄다.",feedback:"모든 움직임이 울음의 시작은 아니다."},
    PLAY_NREM:{hud:"첫째 밤",title:"몸의 긴장이 천천히 풀린다",stage:"깊은 잠의 신호",signal:"호흡은 일정하고, 두 팔이 침대 쪽으로 툭 떨어진다.",body:"이제 옮겨도 될까. 서두르지 말고 한 번 더 확인하자.",actions:["팔 힘 확인하기","조심히 눕히기","조금 더 기다리기"],log:"02:18  품 안의 무게가 조금 더 포근하게 내려앉았다.",feedback:"네 몸이 보내는 신호를 하나씩 외워 간다."},
    PLAY_WAKE_ENCOUNTER:{hud:"첫째 밤",title:"아기가 울기 시작했다",stage:"무언가 불편한 모양이다",signal:"울음이 커지기 전에 이유를 찾아야 한다.",body:"당황하지 말자. 가장 먼저 기저귀부터 확인해 보자.",actions:["기저귀부터 보기","표정과 몸짓 보기","일단 안아보기","수유 준비하기","방 상태 보기","쪽쪽이 건네기","잠시 지켜보기","망설임"],log:"02:14  갑자기 두 눈을 뜨고 울음을 터뜨렸다.",feedback:"네가 말을 못 해도, 아빠가 하나씩 물어볼게."},
    PLAY_DIAPER_CHECK:{hud:"첫째 밤",title:"일단 기저귀부터",stage:"가장 기본적인 확인",signal:"울음 속에서도 작은 다리가 계속 꼼지락거린다.",body:"젖었는지만 보면 된다. 틀려도 아기를 더 힘들게 하지는 않는다.",actions:["살짝 열어보기"],log:"02:15  서두르지 않고 기저귀부터 확인했다.",feedback:"작은 습관 하나가 네 불편을 빨리 알아채게 해준다."},
    PLAY_DIAPER_WET:{hud:"첫째 밤",title:"아, 이게 불편했구나",stage:"울음의 이유를 찾았다",signal:"축축한 기저귀가 피부에 닿아 있었다.",body:"깨끗하게 갈아주면 다시 편안해질 거야.",actions:["보송하게 갈아주기"],log:"02:16  이유를 알고 나니 울음이 다르게 들린다.",feedback:"몰라서 미안해. 이제는 알았으니까 금방 편하게 해줄게."},
    PLAY_DIAPER_DRY:{hud:"첫째 밤",title:"기저귀는 괜찮다",stage:"한 가지는 확인했다",signal:"보송하지만 울음은 아직 이어진다.",body:"그럼 다른 신호를 보자. 입과 손, 호흡, 방 안의 공기.",actions:["몸짓 더 보기","방 상태 보기","품에 안기"],log:"02:16  기저귀는 아니었다. 그래도 헛된 확인은 아니었다.",feedback:"아닌 것을 하나씩 지우다 보면 네가 원하는 데 가까워진다."},
    PLAY_CALMED:{hud:"첫째 밤",title:"울음이 조금씩 잦아든다",stage:"다시 안정을 찾는 중",signal:"내 가슴에 귀를 대고 숨을 고르기 시작했다.",body:"이대로 조금만 더. 네가 괜찮아질 때까지 서두르지 않을게.",actions:["꼭 안아주기","천천히 토닥이기","졸린 신호 보기"],log:"02:23  꽉 쥐었던 손가락이 하나씩 펴졌다.",feedback:"내 품을 알아본 걸까. 그 생각만으로도 피곤함이 조금 가신다."},
    PLAY_MISJUDGE_HOLD:{hud:"첫째 밤",title:"안아도 울음이 더 커진다",stage:"뭔가 놓친 것 같다",signal:"품을 바꿔도 몸을 계속 뒤튼다.",body:"달래기 전에 불편한 곳부터 봤어야 했다. 다시 처음부터 확인하자.",actions:["기저귀 확인하기","몸짓 다시 보기"],log:"02:20  내 마음만 급해서 네 이유를 먼저 묻지 못했다.",feedback:"틀려도 괜찮다. 다음 선택은 조금 더 너를 보고 하자."},
    PLAY_MISJUDGE_FEED:{hud:"첫째 밤",title:"아기는 젖병을 밀어냈다",stage:"배고픈 울음은 아니었다",signal:"고개를 돌리고 입을 굳게 다문다.",body:"준비한 시간이 아깝더라도 억지로 먹이지 말자. 다른 이유가 있다.",actions:["기저귀 확인하기","몸짓 다시 보기"],log:"02:31  젖병보다 먼저 봐야 할 신호가 있었다.",feedback:"내가 원하는 답이 아니라, 네가 보내는 답을 들어야 한다."},
    PLAY_SIGNAL_OBSERVED:{hud:"첫째 밤",title:"입을 오물거리며 손을 빤다",stage:"몸짓이 먼저 말해 준다",signal:"입맛을 다시고 내 쪽으로 고개를 돌린다.",body:"이제 조금 알겠다. 배가 고파지기 시작한 것 같다.",actions:["수유 준비하기","피곤한지 더 보기","방 상태도 확인"],log:"02:25  울음보다 먼저 오는 신호가 있다는 걸 알아챘다.",feedback:"네 말은 소리가 아니라 몸짓에서 먼저 시작되는구나."},
    PLAY_HUNGER_EARLY:{hud:"첫째 밤",title:"배가 고프기 시작한 걸까",stage:"아직 울기 전",signal:"입맛을 다시고 손가락을 입으로 가져간다.",body:"지금 준비하면 울음을 키우지 않고 먹일 수 있다.",actions:["미리 수유 준비","조금 더 살펴보기"],log:"02:26  손을 빠는 작은 소리가 들렸다.",feedback:"네가 크게 울기 전에 알아챈 첫 번째 밤이다."},
    PLAY_HUNGER_MID:{hud:"첫째 밤",title:"내 쪽으로 몸을 파고든다",stage:"배고픔이 커지는 중",signal:"꼼지락거리며 가슴 쪽으로 고개를 돌린다.",body:"이제는 분명하다. 기다리게 하지 말고 준비하자.",actions:["바로 수유 준비","안고 기다려주기"],log:"02:28  내 옷자락을 향해 입을 벌린다.",feedback:"네가 원하는 걸 조금씩 알아보는 내가 된다."},
    PLAY_HUNGER_LATE:{hud:"첫째 밤",title:"배고픈 울음이 커졌다",stage:"많이 기다린 모양이다",signal:"머리를 좌우로 흔들고 숨을 빠르게 몰아쉰다.",body:"준비하는 동안이라도 혼자 울게 두지 말자.",actions:["안고 함께 준비","수유 준비 서두르기"],log:"02:32  울음이 커질수록 내 손도 바빠졌다.",feedback:"다음에는 이 울음까지 오기 전에 알아채고 싶다."},
    PLAY_FATIGUE_EARLY:{hud:"첫째 밤",title:"눈을 피하고 멍하니 본다",stage:"졸음이 찾아오는 중",signal:"눈썹이 살짝 붉어지고 고개를 옆으로 돌린다.",body:"지금부터 방을 조용히 하면 편하게 잠들 수 있다.",actions:["불빛 낮추기","품에 안기","천천히 토닥이기"],log:"21:42  장난감을 보던 눈이 자꾸 먼 곳에 머문다.",feedback:"졸리다는 말도 이렇게 조용히 하는구나."},
    PLAY_FATIGUE_MID:{hud:"첫째 밤",title:"하품을 하고 눈을 비빈다",stage:"잠들 준비가 됐다",signal:"귀를 만지고 내 품을 찾듯 몸을 기댄다.",body:"재우려고 애쓰기보다, 잠들 수 있게 곁을 만들어 주자.",actions:["부드럽게 안기","같은 박자로 토닥이기","불빛 더 낮추기"],log:"21:48  작은 하품 하나에 방 안의 속도도 느려졌다.",feedback:"네가 잠들 준비를 하면 나도 함께 조용해진다."},
    PLAY_FATIGUE_LATE:{hud:"첫째 밤",title:"너무 피곤해 잠들지 못한다",stage:"과하게 지친 상태",signal:"등을 활처럼 휘고 주먹을 꼭 쥔다.",body:"혼내거나 재촉할 일이 아니다. 자극을 줄이고 품에서부터 다시 시작하자.",actions:["단단히 안아주기","한 박자로 토닥이기","빛과 소리 줄이기"],log:"22:03  피곤한데도 잠들지 못해 온몸으로 울고 있다.",feedback:"괜찮아. 잠들 때까지 아빠가 여기 있을게."},
    PLAY_ENVIRONMENT_CHECK:{hud:"첫째 밤",title:"방 안이 조금 덥고 건조하다",stage:"공기도 불편할 수 있다",signal:"목덜미가 따뜻하고 코끝이 조금 마른 듯하다.",body:"안아주는 것만으로 해결되지 않는 불편도 있다.",actions:["조금 시원하게","습도 올리기","아기에게 돌아가기"],log:"02:34  아기만 보느라 방 안의 공기를 놓치고 있었다.",feedback:"네가 편히 숨 쉴 수 있는 방도 내가 돌볼 몫이다."},
    PLAY_ENVIRONMENT_ADJUST:{hud:"첫째 밤",title:"공기가 한결 편안해졌다",stage:"방이 천천히 안정되는 중",signal:"목덜미의 열감이 줄고 호흡도 조금 잔잔해졌다.",body:"수치가 아니라 아기의 반응까지 보고 마무리하자.",actions:["조절 마치기","한 번 더 확인"],log:"02:40  방 안의 공기가 부드러워지자 울음도 낮아졌다.",feedback:"눈에 보이지 않는 불편까지 챙기는 게 돌봄이구나."},
    PLAY_FEED_PREP_STERILIZE:{hud:"첫째 밤 · 새벽 수유",title:"아, 젖병을 미리 소독했나?",stage:"수유 준비 1단계",signal:"아기는 울고 있고, 내 손은 아직 잠이 덜 깼다.",body:"미리 해뒀다면 다행이다. 아니라면 지금부터 차근차근.",actions:["지금 소독하기","소독한 병 찾기","한 손으로 안아주기"],log:"02:41  준비해 둔 작은 일이 새벽에는 큰 도움이 된다.",feedback:"기다리는 네 시간이 길지 않게, 다음 밤의 나도 기억해 두자."},
    PLAY_FEED_PREP_WATER:{hud:"첫째 밤 · 새벽 수유",title:"물이 준비될 때까지",stage:"수유 준비 2단계",signal:"울음은 이어지지만 내 목소리에 잠깐 귀를 기울인다.",body:"서두르되 순서를 건너뛰지 말자. 한 손은 계속 네 곁에.",actions:["물 준비하기","준비기 사용하기","안고 기다려주기"],log:"02:44  기계 소리 사이로 네 울음을 계속 듣고 있다.",feedback:"혼자 기다리게 두지 않는 것, 지금 내가 할 수 있는 돌봄."},
    PLAY_FEED_PREP_MEASURE:{hud:"첫째 밤 · 새벽 수유",title:"서두를수록 정확하게",stage:"수유 준비 3단계",signal:"작은 손이 내 옷을 꼭 쥐었다.",body:"마음은 급해도 계량은 정확히. 네 입에 들어갈 것이니까.",actions:["분유 계량하기","잠깐 안아주기"],log:"02:46  졸린 눈을 비비고 수저의 양을 다시 확인했다.",feedback:"네가 믿고 먹을 한 병을 내가 만든다."},
    PLAY_FEED_PREP_COOL:{hud:"첫째 밤 · 새벽 수유",title:"아직 조금 뜨겁다",stage:"수유 준비 4단계",signal:"젖병을 기다리는 동안 울음이 다시 높아진다.",body:"급하다고 뜨거운 채로 줄 수는 없다. 안전하게 식을 때까지 곁을 지키자.",actions:["골고루 섞기","조심히 식히기","온도 보기"],log:"02:48  몇 분이 이렇게 길게 느껴진 적이 있었나.",feedback:"기다림도 너를 지키는 방법이라고 스스로를 다독인다."},
    PLAY_FEED_PREP_TEMP_CHECK:{hud:"첫째 밤 · 새벽 수유",title:"손목에 한 방울",stage:"수유 준비 마지막 단계",signal:"이제야 먹일 수 있을 만큼 준비가 됐다.",body:"한 번 더 확인하고, 편한 자세로 안아 먹이자.",actions:["온도 확인하기","조금 더 식히기","품에 안아 먹이기"],log:"02:50  마지막 확인을 마치고 너를 다시 품에 안았다.",feedback:"이제 됐어. 오래 기다렸지."},
    PLAY_FEEDING:{hud:"첫째 밤 · 새벽 수유",title:"이제야 제대로 먹기 시작한다",stage:"배고픔이 잦아드는 중",signal:"급하던 숨이 느려지고 손끝의 힘도 풀린다.",body:"젖병 너머로 눈이 마주쳤다. 아주 잠깐, 웃은 것 같기도 하다.",actions:["천천히 먹이기","어깨에 기대 트림"],log:"02:53  작게 삼키는 소리가 방 안을 채운다.",feedback:"이 시간이 힘든데도, 언젠가 그리워질 것 같다는 생각이 들었다."},
    PLAY_ARM_CHECK_REM:{hud:"첫째 밤",title:"팔에 아직 힘이 남아 있다",stage:"조금 더 기다려야 한다",signal:"눈꺼풀이 떨리고 손가락이 가끔 움찔한다.",body:"지금 옮기면 놀라 깰 수 있다. 품에서 한 호흡만 더.",actions:["조금 더 기다리기","숨소리 지켜보기"],log:"03:08  팔을 살짝 들어 보니 아직 힘이 남아 있다.",feedback:"재우는 것보다 기다리는 일이 더 어려울 때도 있다."},
    PLAY_ARM_CHECK_NREM:{hud:"첫째 밤",title:"팔이 툭, 아래로 떨어진다",stage:"옮겨도 될 만큼 깊이 잠들었다",signal:"호흡은 일정하고 얼굴의 힘도 완전히 풀렸다.",body:"천천히, 아주 천천히. 내 품의 온기가 갑자기 사라지지 않게.",actions:["조심히 눕히기","한 호흡 더 기다리기"],log:"03:12  작은 팔의 무게가 온전히 내 손에 맡겨졌다.",feedback:"나를 믿고 잠든 것 같아서, 움직이는 일조차 조심스러워진다."},
    PLAY_LAYDOWN_SUCCESS:{hud:"첫째 밤",title:"손을 빼도 잠이 이어진다",stage:"침대에서 편안히 자는 중",signal:"등이 닿았지만 호흡은 흐트러지지 않았다.",body:"성공했다. 그래도 밤은 아직 길다. 가까운 곳에서 계속 지켜보자.",actions:["곁에서 지켜보기"],log:"03:14  마지막 손가락을 빼는 데 한참이 걸렸다.",feedback:"품에서 내려놓았는데도 마음은 아직 네 곁에 남아 있다."},
    PLAY_LAYDOWN_FAIL_MORO:{hud:"첫째 밤",title:"등이 닿자 두 팔이 번쩍 들렸다",stage:"놀라서 다시 깨어남",signal:"눈을 크게 뜨고 금세 울음이 터졌다.",body:"조금 일렀나 보다. 실패가 아니라 다시 안아야 할 순간이다.",actions:["다시 품에 안기","빛과 소리 줄이기","같은 박자로 토닥이기"],log:"03:10  내려놓는 순간 작은 몸이 화들짝 놀랐다.",feedback:"괜찮아. 아빠가 아직 여기 있어."},
    PLAY_SLEEP_FAST_FORWARD:{hud:"첫째 밤",title:"잠든 시간이 차곡차곡 쌓인다",stage:"새벽을 건너는 중",signal:"현재 2시간 38분째, 한 번도 깨지 않았다.",body:"시계는 빨리 가고 방 안은 고요하다. 이 평온이 조금 더 이어지기를.",actions:["다음 숨까지 지켜보기"],log:"04:52  어느새 창밖의 어둠이 조금 옅어졌다.",feedback:"네가 자는 동안 나도 네 아빠가 되어 가는 중이다."},
    PLAY_TIMEOUT:{hud:"첫째 밤",title:"망설이는 사이 울음이 커졌다",stage:"많이 불편해진 상태",signal:"얼굴이 붉어지고 숨이 가빠졌다.",body:"늦었어도 지금부터 하면 된다. 기본부터 다시 확인하자.",actions:["기저귀부터 확인","몸짓 자세히 보기"],log:"02:15  무엇부터 해야 할지 몰라 잠깐 굳어 버렸다.",feedback:"완벽한 아빠는 아니어도, 포기하지 않는 아빠는 될 수 있다."},
    DIARY:{hud:"아침 여섯 시",title:"우리 둘이 건넌 첫 번째 밤",stage:"가장 길게 잔 시간 · 2시간 32분",signal:"네 번 깼고, 세 번은 이유를 제대로 찾았다.",body:"처음보다 네 울음이 조금 다르게 들린다. 내일은 오늘보다 빨리 알아챌 수 있을 것 같다.",actions:["다음 밤으로","오늘 밤 다시 보기"],log:"06:00  해가 뜨자 작게 기지개를 켠다.",feedback:"밤새 한숨도 못 잔 것 같은데, 네 얼굴을 보니 이상하게 웃음이 난다."},
    ENDING:{hud:"아침",title:"이제는 네 밤을 조금 안다",stage:"통잠보다 먼저 배운 것",signal:"배고픈 몸짓, 졸린 눈, 놀란 팔, 그리고 편안해지는 숨.",body:"처음엔 울음이 무서웠다. 이제는 그 울음 속에서 네가 하는 말을 찾게 된다.",actions:["우리의 첫밤 다시 시작"],log:"백일의 밤이 끝났다. 하지만 아빠의 밤은 이제 시작이다.",feedback:"네가 나를 아빠로 만든 모든 새벽을 오래 기억할 것 같다."}
  };
  defs.forEach(def => Object.assign(def, UI[def.id] || {}));

  async function replaceCharacters(node, value) {
    if (!node || node.type !== "TEXT" || value == null) return false;
    const segments = node.getStyledTextSegments(["fontName"]);
    const loaded = {};
    for (const segment of segments) {
      const fontName = segment.fontName;
      const key = fontName.family + "__" + fontName.style;
      if (!loaded[key]) { await figma.loadFontAsync(fontName); loaded[key] = true; }
    }
    node.characters = String(value);
    return true;
  }

  if (existingBoards.length) {
    let changed = 0;
    for (const existingBoard of existingBoards) {
      const boardTitle = existingBoard.findOne(n => n.type === "TEXT" && n.name === "BOARD_TITLE");
      if (await replaceCharacters(boardTitle, "NOT A NAP — 아빠가 되어 가는 밤 · 상세 스토리보드 V4.2")) changed += 1;
      for (const def of defs) {
        const screen = existingBoard.findOne(n => n.type === "FRAME" && n.name === def.id);
        if (!screen) continue;
        const updates = [
          ["SCREEN_ID", def.hud], ["EVENT_TITLE", def.title], ["EVENT_BODY", def.body],
          ["BABY_STATE", def.stage], ["SIGNAL", def.signal], ["LOG_TITLE", "아빠의 밤 기록"],
          ["LOG_TEXT", def.log], ["LOG_FEEDBACK", def.feedback]
        ];
        for (const [name, value] of updates) {
          const node = screen.findOne(n => n.type === "TEXT" && n.name === name);
          if (await replaceCharacters(node, value)) changed += 1;
        }
        const routeChip = screen.findOne(n => n.type === "TEXT" && n.name === "NEXT_ROUTE_CHIP");
        if (routeChip) routeChip.visible = false;
        const buttonTexts = screen.findAll(n => n.type === "TEXT" && n.x >= 1130 && n.y >= 400 && n.y < 730 && n.name.indexOf("TXT_") === 0)
          .sort((a, b) => a.y === b.y ? a.x - b.x : a.y - b.y);
        for (let i = 0; i < Math.min(buttonTexts.length, (def.actions || []).length); i++) {
          if (await replaceCharacters(buttonTexts[i], def.actions[i])) changed += 1;
          buttonTexts[i].name = "TXT_ACTION_" + (i + 1);
        }
      }
    }
    const newestBoard = existingBoards[existingBoards.length - 1];
    const firstScreen = newestBoard.findOne(n => n.type === "FRAME" && n.name === "TITLE");
    figma.currentPage.selection = firstScreen ? [firstScreen] : [newestBoard];
    figma.viewport.scrollAndZoomIntoView(firstScreen ? [firstScreen] : [newestBoard]);
    figma.closePlugin("V4.2 문구 교체 완료 · " + changed + "개 텍스트 수정 · 전환선 " + removedFlowLineCount + "개 제거");
    return;
  }

  const COLS = 3, SCREEN_W = 1920, NOTE_W = 720, UNIT_GAP = 42;
  const UNIT_W = SCREEN_W + UNIT_GAP + NOTE_W;
  const COL_GAP = 260, ROW_GAP = 260, TOP = 360, MARGIN = 120;
  const rows = Math.ceil(defs.length / COLS);
  const boardW = MARGIN * 2 + COLS * UNIT_W + (COLS - 1) * COL_GAP;
  const boardH = TOP + rows * 1080 + (rows - 1) * ROW_GAP + 180;
  const board = figma.createFrame();
  const stamp = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
  board.name = "V4_DEVELOPER_STORYBOARD_" + stamp;
  board.resize(boardW, boardH); board.fills = paint(C.board); board.cornerRadius = 40; board.clipsContent = false;
  label(board, "NOT A NAP — 아빠가 되어 가는 밤 · 상세 스토리보드 V4.2", MARGIN, 70, boardW - MARGIN * 2, 58, C.ink, true, "BOARD_TITLE");
  label(board, "각 프레임은 1920×1080 실제 화면. 오른쪽 계약표를 그대로 Unity Presenter·View·Core 연결 기준으로 사용합니다.", MARGIN, 150, boardW - MARGIN * 2, 26, C.muted, false, "BOARD_SUBTITLE");
  label(board, "초록=성공  ·  빨강=오판/실패  ·  파랑=관찰/이동  |  기존 보드는 삭제하지 않음", MARGIN, 210, boardW - MARGIN * 2, 24, C.darkGreen, true, "BOARD_LEGEND");

  const positions = {};
  defs.forEach((def, i) => {
    const col = i % COLS, row = Math.floor(i / COLS);
    const x = MARGIN + col * (UNIT_W + COL_GAP);
    const y = TOP + row * (1080 + ROW_GAP);
    const screen = makeScreen(def); screen.x = x; screen.y = y; board.appendChild(screen);
    const note = makeContract(def); note.x = x + SCREEN_W + UNIT_GAP; note.y = y; board.appendChild(note);
    positions[def.id] = { x, y, w: SCREEN_W, h: 1080 };
  });

  const others = figma.currentPage.children.filter(n => n !== board && typeof n.x === "number" && typeof n.width === "number");
  let placeX = 0, placeY = 0;
  if (others.length) {
    placeX = Math.max(...others.map(n => n.x + n.width)) + 600;
    placeY = Math.min(...others.map(n => n.y));
  }
  board.x = placeX; board.y = placeY;
  const firstScreen = board.findOne(n => n.type === "FRAME" && n.name === "TITLE");
  figma.currentPage.selection = firstScreen ? [firstScreen] : [board];
  figma.viewport.scrollAndZoomIntoView(firstScreen ? [firstScreen] : [board]);
  figma.closePlugin("V4 상세 스토리보드 33개 화면 생성 완료 · 기존 보드 보존 · 폰트 " + family);
})().catch(error => figma.closePlugin("V4 생성 오류: " + (error && error.message ? error.message : String(error))));
