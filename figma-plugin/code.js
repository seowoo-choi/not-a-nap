/* NOT A NAP — MOBILE_QA_STORYBOARD_V6 code contract synchronizer.
 * The source board is preserved. A duplicated, editable board is updated.
 */

(async () => {
  const fonts = await figma.listAvailableFontsAsync();
  const preferredFamilies = ["Noto Sans KR", "Pretendard", "Apple SD Gothic Neo", "Inter"];
  const fallback = preferredFamilies
    .map(family => fonts.find(f => f.fontName.family === family &&
      ["Regular", "Medium"].includes(f.fontName.style)))
    .find(Boolean) || fonts[0];
  const fallbackBold = preferredFamilies
    .map(family => fonts.find(f => f.fontName.family === family &&
      ["Bold", "Semi Bold", "Semibold"].includes(f.fontName.style)))
    .find(Boolean) || fallback;
  if (fallback) await figma.loadFontAsync(fallback.fontName);
  if (fallbackBold) await figma.loadFontAsync(fallbackBold.fontName);

  const allFrames = figma.currentPage.findAll(n => n.type === "FRAME");
  let source = allFrames.find(n => n.name === "MOBILE_QA_STORYBOARD_V6");
  if (!source) {
    source = allFrames.find(n => {
      if (n.name.indexOf("MOBILE_QA_STORYBOARD_V6_CODE_SYNC_") === 0) return false;
      return n.findOne && n.findOne(c => c.type === "TEXT" && c.characters.indexOf("스토리보드 V6") >= 0);
    });
  }
  if (!source) {
    figma.closePlugin("MOBILE_QA_STORYBOARD_V6 보드를 찾지 못했습니다.");
    return;
  }

  // 두 번째 실행부터는 보드를 계속 복제하지 않고 가장 최신 CODE_SYNC 보드를 갱신한다.
  const existingSyncBoards = allFrames
    .filter(n => n.name.indexOf("MOBILE_QA_STORYBOARD_V6_CODE_SYNC_") === 0)
    .sort((a, b) => b.name.localeCompare(a.name));
  const board = existingSyncBoards[0] || source.clone();
  const created = existingSyncBoards.length === 0;
  if (created) {
    const stamp = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
    board.name = "MOBILE_QA_STORYBOARD_V6_CODE_SYNC_" + stamp;
    board.x = source.x + source.width + 480;
    board.y = source.y;
  }

  async function loadTextFonts(node) {
    if (!node || node.type !== "TEXT") return;
    const seen = {};
    const segments = node.getStyledTextSegments(["fontName"]);
    for (const segment of segments) {
      const font = segment.fontName;
      const key = font.family + "__" + font.style;
      if (!seen[key]) {
        await figma.loadFontAsync(font);
        seen[key] = true;
      }
    }
  }

  async function setText(node, value) {
    await loadTextFonts(node);
    node.characters = String(value);
  }

  function textNodes(root) {
    return root.findAll(n => n.type === "TEXT");
  }

  function contractFor(screenId) {
    const named = board.findOne(n => n.type === "FRAME" &&
      (n.name === screenId + "__DEV_CONTRACT" || n.name === screenId + "_DEV_CONTRACT"));
    if (named) return named;
    const idText = board.findOne(n => n.type === "TEXT" && n.characters.trim() === screenId);
    if (!idText) return null;
    let parent = idText.parent;
    while (parent && parent !== board) {
      if (parent.type === "FRAME" && parent.width < board.width * 0.35) return parent;
      parent = parent.parent;
    }
    return idText.parent && idText.parent.type === "FRAME" ? idText.parent : null;
  }

  function findText(root, exact) {
    return textNodes(root).find(n => n.characters.trim() === exact);
  }

  function contractContaining(terms) {
    const wanted = Array.isArray(terms) ? terms : [terms];
    const hits = textNodes(board).filter(n => wanted.some(term => n.characters.indexOf(term) >= 0));
    for (const hit of hits) {
      let parent = hit.parent;
      while (parent && parent !== board) {
        if (parent.type === "FRAME") {
          const texts = textNodes(parent).map(n => n.characters.trim());
          if (texts.some(value => ["P0 CONNECT", "IMPLEMENTED", "NOT PLAYABLE", "REVIEW REQUIRED", "EXCEPTION ONLY"].includes(value)) ||
              parent.name.indexOf("CONTRACT") >= 0) return parent;
        }
        parent = parent.parent;
      }
    }
    return null;
  }

  async function replaceAll(root, from, to) {
    let count = 0;
    for (const node of textNodes(root)) {
      if (node.characters.indexOf(from) < 0) continue;
      await setText(node, node.characters.split(from).join(to));
      count += 1;
    }
    return count;
  }

  async function replaceValue(contract, oldValues, nextValue) {
    if (!contract) return false;
    for (const oldValue of oldValues) {
      const node = findText(contract, oldValue);
      if (node) {
        await setText(node, nextValue);
        return true;
      }
    }
    return false;
  }

  async function setBadge(contract, value, fill) {
    if (!contract) return false;
    const badgeText = textNodes(contract).find(n =>
      ["P0 CONNECT", "IMPLEMENTED", "NOT PLAYABLE", "REVIEW REQUIRED", "EXCEPTION ONLY"].includes(n.characters.trim()));
    if (!badgeText) return false;
    await setText(badgeText, value);
    const cx = badgeText.x + badgeText.width / 2;
    const cy = badgeText.y + badgeText.height / 2;
    const background = contract.findAll(n =>
      (n.type === "RECTANGLE" || n.type === "FRAME") && n !== contract &&
      n.x <= cx && n.x + n.width >= cx && n.y <= cy && n.y + n.height >= cy &&
      n.height <= 100 && n.width <= 360
    ).sort((a, b) => a.width * a.height - b.width * b.height)[0];
    if (background) background.fills = [{ type: "SOLID", color: fill }];
    return true;
  }

  async function appendReviewNote(contract, message, key) {
    if (!contract) return false;
    const noteName = "CODE_SYNC_REVIEW_NOTE" + (key ? "__" + key : "");
    const existing = textNodes(contract).find(n => n.name === noteName);
    if (existing) {
      await setText(existing, message);
      return true;
    }
    const note = figma.createText();
    note.name = noteName;
    note.fontName = fallbackBold ? fallbackBold.fontName : fallback.fontName;
    note.fontSize = 16;
    note.characters = message;
    note.fills = [{ type: "SOLID", color: { r: 0.65, g: 0.18, b: 0.2 } }];
    note.textAutoResize = "HEIGHT";
    note.resize(Math.max(240, contract.width - 64), 60);
    note.x = 32;
    const reviewNoteCount = textNodes(contract).filter(n =>
      n.name.indexOf("CODE_SYNC_REVIEW_NOTE") === 0).length;
    note.y = contract.height - 88 - reviewNoteCount * 72;
    contract.appendChild(note);
    return true;
  }

  function screenFor(screenId) {
    const named = board.findAll(n => n.type === "FRAME" &&
      n.name.indexOf(screenId) >= 0 &&
      n.name.indexOf("CONTRACT") < 0 &&
      n.name !== board.name);
    const namedScreen = named
      .filter(n => n.width >= 600 && n.height >= 1000 && n.width / n.height < 0.8)
      .sort((a, b) => Math.abs(a.width / a.height - 1080 / 1920) -
        Math.abs(b.width / b.height - 1080 / 1920))[0];
    if (namedScreen) return namedScreen;

    const labels = textNodes(board).filter(n =>
      n.characters.indexOf(screenId) >= 0 &&
      (!n.parent || n.parent.name.indexOf("CONTRACT") < 0));
    const candidates = [];
    for (const label of labels) {
      let parent = label.parent;
      while (parent && parent !== board) {
        if (parent.type === "FRAME" &&
            parent.width >= 600 && parent.height >= 1000 &&
            parent.width / parent.height < 0.8) {
          candidates.push(parent);
        }
        parent = parent.parent;
      }
    }
    return candidates.sort((a, b) => a.width * a.height - b.width * b.height)[0] || null;
  }

  function addMapText(parent, name, value, x, y, width, size, bold, color) {
    const node = figma.createText();
    node.name = name;
    node.fontName = bold && fallbackBold ? fallbackBold.fontName : fallback.fontName;
    node.fontSize = size;
    node.characters = value;
    node.fills = [{ type: "SOLID", color }];
    node.textAutoResize = "HEIGHT";
    node.resize(width, Math.max(size * 1.5, 28));
    parent.appendChild(node);
    node.x = x;
    node.y = y;
    return node;
  }

  function addMapRoom(parent, name, items, x, y, width, height, active) {
    const room = figma.createFrame();
    room.name = "HOME_MAP_ROOM__" + name;
    room.resize(width, height);
    room.fills = [{
      type: "SOLID",
      color: active ? { r: 0.13, g: 0.22, b: 0.26 } : { r: 0.045, g: 0.075, b: 0.11 }
    }];
    room.strokes = [{
      type: "SOLID",
      color: active ? { r: 0.49, g: 0.82, b: 0.6 } : { r: 0.22, g: 0.3, b: 0.38 }
    }];
    room.strokeWeight = active ? 4 : 2;
    room.cornerRadius = 16;
    parent.appendChild(room);
    room.x = x;
    room.y = y;

    addMapText(room, "ROOM_NAME", active ? "● " + name : name,
      10, 8, width - 20, 16, true, { r: 0.94, g: 0.96, b: 0.98 });
    addMapText(room, "ROOM_ITEMS", items,
      10, Math.max(32, height * 0.42), width - 20, 13, false, { r: 0.63, g: 0.7, b: 0.78 });
    addMapText(room, "ROOM_MOVE",
      active ? "현재 위치" : (name === "아기방" ? "이동 · 2분" : "이동 · 2–3분"),
      10, height - 26, width - 20, 12, true,
      active ? { r: 0.49, g: 0.82, b: 0.6 } : { r: 0.91, g: 0.7, b: 0.36 });
    return room;
  }

  function upsertHomeMap(screenId) {
    const screen = screenFor(screenId);
    if (!screen) return false;

    const previous = screen.findOne(n => n.name === "CODE_SYNC_HOME_MAP");
    if (previous) previous.remove();

    const map = figma.createFrame();
    map.name = "CODE_SYNC_HOME_MAP";
    map.resize(screen.width * 0.87, screen.height * 0.23);
    map.fills = [{ type: "SOLID", color: { r: 0.02, g: 0.055, b: 0.09 } }];
    map.strokes = [{ type: "SOLID", color: { r: 0.17, g: 0.28, b: 0.38 } }];
    map.strokeWeight = 2;
    map.cornerRadius = 20;
    map.clipsContent = true;
    screen.appendChild(map);
    map.x = screen.width * 0.065;
    map.y = screen.height * 0.088;

    const inset = 12;
    const stateWidth = map.width * 0.52;
    const miniWidth = map.width * 0.34;
    const miniHeight = map.height * 0.46;

    const focus = figma.createFrame();
    focus.name = "FIRST_PERSON_ROOM_FOCUS";
    focus.resize(map.width - inset * 2, map.height - inset * 2);
    focus.fills = [{ type: "SOLID", color: { r: 0.035, g: 0.08, b: 0.12 }, opacity: 0.82 }];
    focus.cornerRadius = 14;
    map.appendChild(focus);
    focus.x = inset;
    focus.y = inset;

    const state = figma.createFrame();
    state.name = "BABY_STATE_OVERLAY";
    state.resize(stateWidth, 72);
    state.fills = [{ type: "SOLID", color: { r: 0.025, g: 0.06, b: 0.105 }, opacity: 0.72 }];
    state.cornerRadius = 12;
    map.appendChild(state);
    state.x = inset * 2;
    state.y = inset * 2;
    addMapText(state, "BABY_STATE_TITLE", "아기의 지금 · 표정과 몸짓을 살핀다",
      16, 10, state.width - 32, 19, true, { r: 0.94, g: 0.96, b: 0.98 });
    addMapText(state, "BABY_STATE_SIGNAL", "아기는 아기방 · 보호자만 이동",
      16, 38, state.width - 32, 15, false, { r: 0.63, g: 0.7, b: 0.78 });

    const baby = figma.createEllipse();
    baby.name = "BABY_LOCATION_MARKER";
    baby.resize(Math.max(92, map.width * 0.16), Math.max(92, map.width * 0.16));
    baby.fills = [{ type: "SOLID", color: { r: 0.93, g: 0.68, b: 0.53 } }];
    baby.strokes = [{ type: "SOLID", color: { r: 0.98, g: 0.84, b: 0.71 } }];
    baby.strokeWeight = 4;
    map.appendChild(baby);
    baby.x = map.width * 0.46 - baby.width / 2;
    baby.y = map.height * 0.48 - baby.height / 2;
    addMapText(map, "BABY_MARKER_LABEL", "아기",
      baby.x - 2, baby.y + baby.height + 6, baby.width + 4, 15, true,
      { r: 0.94, g: 0.96, b: 0.98 });

    addMapText(map, "ROOM_FOCUS_TITLE", "현재 위치 · 아기방",
      28, map.height - 70, map.width * 0.55, 24, true,
      { r: 0.94, g: 0.96, b: 0.98 });
    addMapText(map, "ROOM_FOCUS_ITEMS", "침대 · 베이비 모니터 · 아기의 숨소리",
      28, map.height - 38, map.width * 0.58, 15, false,
      { r: 0.63, g: 0.7, b: 0.78 });

    const mini = figma.createFrame();
    mini.name = "HOME_MINIMAP__TOP_RIGHT";
    mini.resize(miniWidth, miniHeight);
    mini.fills = [{ type: "SOLID", color: { r: 0.015, g: 0.035, b: 0.06 }, opacity: 0.72 }];
    mini.cornerRadius = 12;
    map.appendChild(mini);
    mini.x = map.width - miniWidth - inset * 2;
    mini.y = inset * 2;
    const gap = 6;
    const nurseryWidth = mini.width * 0.54;
    const sideWidth = mini.width - nurseryWidth - gap;
    addMapRoom(mini, "아기방", "WASD", 0, 0, nurseryWidth, mini.height - 24, true);
    addMapRoom(mini, "주방", "D", nurseryWidth + gap, 0, sideWidth,
      (mini.height - gap - 24) / 2, false);
    addMapRoom(mini, "욕실", "S", nurseryWidth + gap,
      (mini.height - gap - 24) / 2 + gap, sideWidth,
      (mini.height - gap - 24) / 2, false);
    addMapText(mini, "MINIMAP_HELP", "WASD 방 이동 · 2–3분 경과",
      6, mini.height - 22, mini.width - 12, 13, true,
      { r: 0.91, g: 0.7, b: 0.36 });

    const hud = figma.createFrame();
    hud.name = "TRANSPARENT_ACTION_HUD";
    hud.resize(map.width * 0.42, 54);
    hud.fills = [{ type: "SOLID", color: { r: 0.025, g: 0.055, b: 0.09 }, opacity: 0.62 }];
    hud.cornerRadius = 12;
    map.appendChild(hud);
    hud.x = map.width - hud.width - inset * 2;
    hud.y = map.height - hud.height - inset * 2;
    addMapText(hud, "HUD_TABS", "살펴보기     돌보기     수유 준비",
      14, 15, hud.width - 28, 16, true, { r: 0.86, g: 0.88, b: 0.91 });

    return true;
  }

  async function upsertActionSummary() {
    const panelWidth = 2200;
    const panelHeight = 1580;
    let panel = board.findOne(n => n.type === "FRAME" && n.name === "_REVIEW_ACTIONS_SUMMARY");
    if (!panel) {
      panel = figma.createFrame();
      panel.name = "_REVIEW_ACTIONS_SUMMARY";
      panel.resize(panelWidth, panelHeight);
      panel.x = 80;
      panel.y = board.height + 80;
      panel.fills = [{ type: "SOLID", color: { r: 0.055, g: 0.09, b: 0.13 } }];
      panel.cornerRadius = 32;
      board.appendChild(panel);
    }
    panel.resize(panelWidth, panelHeight);
    const requiredBoardHeight = panel.y + panelHeight + 80;
    if (board.height < requiredBoardHeight) board.resize(board.width, requiredBoardHeight);

    let title = panel.findOne(n => n.type === "TEXT" && n.name === "ACTION_SUMMARY_TITLE");
    if (!title) {
      title = figma.createText();
      title.name = "ACTION_SUMMARY_TITLE";
      title.fontName = fallbackBold ? fallbackBold.fontName : fallback.fontName;
      title.fontSize = 34;
      title.fills = [{ type: "SOLID", color: { r: 0.95, g: 0.96, b: 0.98 } }];
      title.x = 56;
      title.y = 48;
      panel.appendChild(title);
    }
    await setText(title, "REVIEW ACTIONS · 다음 구현");

    let body = panel.findOne(n => n.type === "TEXT" && n.name === "ACTION_SUMMARY_BODY");
    if (!body) {
      body = figma.createText();
      body.name = "ACTION_SUMMARY_BODY";
      body.fontName = fallback ? fallback.fontName : fallbackBold.fontName;
      body.fontSize = 24;
      body.lineHeight = { value: 38, unit: "PIXELS" };
      body.fills = [{ type: "SOLID", color: { r: 0.82, g: 0.86, b: 0.91 } }];
      body.textAutoResize = "HEIGHT";
      body.resize(2080, 1340);
      body.x = 56;
      body.y = 120;
      panel.appendChild(body);
    }
    await setText(body,
      "이번 반영 · 보호자 성향 3종 × 아기 기질 3종 설정(궁합 점수 없음)\n" +
      "이번 반영 · 입·손·호흡·몸짓 신호 + 숨 고르기 + 마음의 여유\n" +
      "이번 반영 · 목 받치고 안기 + 주방 이동/분유 준비 경과 시간\n" +
      "이번 반영 · 아기방/주방/욕실 지도 + 직접 이동 2–3분 + 지도 위 아기 상태\n" +
      "이번 반영 · 신호·보호자 성장·엄마 이해 중심 육아일지\n" +
      "이번 반영 · 서버 없는 검수 동행 문장과 마미톡 공유 카드 문안\n\n" +
      "완료 · #18  부적절한 수면 보조 장비를 암막 커튼으로 교체\n" +
      "P1 · #20-2  원인별 관찰 신호를 결정론적 시드로 변주\n" +
      "P1 · #20-3  관찰 → 근거 → 권장 행동을 단계적으로 안내\n" +
      "P2 · #20-4  30초 광고 → 옆잠베개 활성화·깊은 수면 +5 제안 검토\n" +
      "             안전·수면 판정 버프 제외. 심사·광고 표기·SDK·제품 안전 검토 후 결정\n" +
      "제품 확장 · 실제 마미톡 로그인/공유, 익명 집계/응원, 브랜드 계약은 서버·운영 정책 후\n\n" +
      "완료 · #4 #5 #6 #8–#17 #19 #20-1 #21 #23\n" +
      "유지 · #7 #22 #24\n\n" +
      "전체 댓글별 처리표\n" +
      "https://github.com/seowoo-choi/not-a-nap/blob/codex/fair-sleep-guidance/docs/figma-review-actions.md\n\n" +
      "완료 조건 · 코드 반영 + Unity 테스트 통과 + Figma 계약 동기화");
    return true;
  }

  let changes = 0;
  if (await upsertActionSummary()) changes += 1;
  changes += await replaceAll(board, "Presenter.TryExecuteV2Action 호출", "GameFlowController.ActV2 호출");
  changes += await replaceAll(board, "Presenter.TryExecuteV2Action", "GameFlowController.ActV2");
  // 기존 보드에 남은 삭제 대상 명칭만 찾아 암막 커튼으로 치환한다.
  changes += await replaceAll(board, "\uC218\uBA74 \uD3EC\uC9C0\uC154\uB108", "암막 커튼");
  changes += await replaceAll(board, "젖을 찾는 듯 고개를 움직인다", "입가를 건드린 쪽으로 고개를 돌리고 입을 벌린다");
  changes += await replaceAll(board, "준비해 둔 작은 일이 새벽에는 큰 도움이 된다.", "미리 소독해뒀다면 덜 기다렸을 텐데. 다음 밤에는 먼저 준비해두자.");
  changes += await replaceAll(board, "울지 않고 조용히 주변을 본다", "울지 않고 아빠를 빤히 바라본다");

  const visualImplemented = [
    "M_PLAY_AWAKE_CALM", "M_FUSS_SOFT", "M_CRY_HARD", "M_HUNGER_EARLY",
    "M_HUNGER_LATE", "M_DROWSY", "M_REM_ACTIVE", "M_NREM_DEEP",
    "M_LIMBS_RELAXED", "M_MORO_STARTLE", "M_PACIFIER_ACCEPT", "M_PACIFIER_REJECT"
  ];
  const green = { r: 0.82, g: 0.95, b: 0.87 };
  for (const id of visualImplemented) {
    if (await setBadge(contractFor(id), "IMPLEMENTED", green)) changes += 1;
  }

  // 코드의 PLAY 화면처럼 아기 단독 비주얼 영역을 실제 3칸 집 지도 UI로 갱신한다.
  // 같은 화면에서 다시 실행하면 CODE_SYNC_HOME_MAP을 교체하므로 중복 레이어가 생기지 않는다.
  const mapScreens = visualImplemented.concat([
    "M_DIAPER_CHECK_WET", "M_DIAPER_CHECK_CLEAN", "M_ENVIRONMENT_CHECK",
    "M_TAB_CARE_PERSIST", "M_TAB_FEED_PERSIST", "M_SLEEP_FAST_FORWARD"
  ]);
  for (const id of mapScreens) {
    if (upsertHomeMap(id)) changes += 1;
  }

  const timeout = contractFor("M_TIMEOUT");
  if (await setBadge(timeout, "IMPLEMENTED", green)) changes += 1;
  if (await replaceValue(timeout, ["GameBootstrap.UpdateDecisionTimer"], "GameBootstrap.UpdateDecisionTimer + _timeoutSent")) changes += 1;

  const fastForward = contractFor("M_SLEEP_FAST_FORWARD");
  if (await setBadge(fastForward, "IMPLEMENTED", green)) changes += 1;
  if (await replaceValue(fastForward, ["Presenter.FastForwardV2Sleep", "FastForwardV2Sleep"], "GameFlowController.FastForwardV2Sleep")) changes += 1;
  if (await appendReviewNote(fastForward,
    "IMPLEMENTED: GameFlowController.ChooseV2SleepInterval. 아기가 자는 동안 ① 같이 쉬기(체력 +15) ② 환경 점검(온·습도 확인) ③ 다음 수유 준비(분유 혼합 완료·체력 -3) 중 하나를 선택하고 다음 각성까지 진행.",
    "SLEEP_INTERVAL_CHOICE")) changes += 1;

  const item = contractFor("M_ITEM_SCROLL");
  if (await replaceValue(item, ["SelectItem"], "— (ItemId 사용)")) changes += 1;
  if (await replaceValue(item, ["V2NightFactory.IsSelectableItem 확인"], "GameFlowController.ToggleV2Item(ItemId) → IsSelectableItem 확인")) changes += 1;
  if (await setBadge(item, "REVIEW REQUIRED", { r: 1, g: 0.9, b: 0.68 })) changes += 1;
  if (await appendReviewNote(item, "코드 선택 목록: 아기띠 / 쪽쪽이 / 백색소음기 / 베이비 모니터. PLAY 연결: 백색소음기→돌보기/ToggleNoise, 모니터→살펴보기/CheckMonitor. 분유제조기는 제품 결정 필요.")) changes += 1;

  const unlock = contractFor("M_UNLOCK_CANDIDATES");
  if (await setBadge(unlock, "IMPLEMENTED", green)) changes += 1;
  if (await appendReviewNote(unlock,
    "사용자가 이해하기 어려운 장비는 제거하고 암막 커튼으로 교체. 세 후보는 안전·제품 검토 전까지 선택 불가.",
    "UNLOCK")) changes += 1;

  // 2026-07-22 플레이테스트 피드백을 개발 계약에 동기화한다.
  const laydown = contractFor("M_LIMBS_RELAXED");
  if (await appendReviewNote(laydown,
    "선행 조건: Held=true + REM/NREM. 품에서 잠든 경우만 눕히기 제안. 침대에서 토닥여 잠들면 ‘그대로 지켜보기’ 안내.",
    "LAYDOWN")) changes += 1;

  const hold = contractFor("M_TAB_CARE_PERSIST");
  if (await setBadge(hold, "IMPLEMENTED", green)) changes += 1;
  if (await appendReviewNote(hold,
    "IMPLEMENTED: 품에 안기=맨손 안기. 아기띠 선택 시 ToggleCarrier 착용/벗기 행동 노출. 벗긴 직후 Held=true / Wearing.Carrier=false로 독립 상태 유지.",
    "HOLD")) changes += 1;

  const awakeCopy = contractFor("M_PLAY_AWAKE_CALM");
  if (await appendReviewNote(awakeCopy,
    "IMPLEMENTED: BabyStateHeadline은 전지적 설명 대신 아빠가 보고 들을 수 있는 관찰을 사용. 기본 문구: ‘울지 않고 아빠를 빤히 바라본다.’",
    "FATHER_PERSPECTIVE")) changes += 1;
  if (await appendReviewNote(awakeCopy,
    "RELATIONAL PLAY: 표정 외 입맛 다시기·손 빨기·하품·호흡·몸의 방향을 관찰. CatchBreath는 ‘숨 고르고 신호 기다리기’이며 마음의 여유를 회복.",
    "RELATIONAL_SIGNAL")) changes += 1;

  const pat = contractFor("M_TAB_CARE_PERSIST");
  if (await setBadge(pat, "REVIEW REQUIRED", { r: 1, g: 0.9, b: 0.68 })) changes += 1;
  if (await appendReviewNote(pat,
    "침대 토닥임과 품 안 토닥임을 Held로 구분. 침대에서 잠들면 다시 눕히기 안내 금지.",
    "PAT")) changes += 1;

  const diaperWet = contractFor("M_DIAPER_CHECK_WET");
  if (await setBadge(diaperWet, "IMPLEMENTED", green)) changes += 1;
  if (await appendReviewNote(diaperWet,
    "CheckDiaper 결과 DiaperCheckResult.Wet → ‘기저귀가 젖어 있어요. 기저귀를 갈아주세요.’ 표시.")) changes += 1;

  const diaperClean = contractFor("M_DIAPER_CHECK_CLEAN");
  if (await setBadge(diaperClean, "IMPLEMENTED", green)) changes += 1;
  if (await appendReviewNote(diaperClean,
    "CheckDiaper 결과 DiaperCheckResult.Clean → 다른 신호 확인 안내. 안전한 배제 검사라 오판 0.")) changes += 1;

  const hunger = contractFor("M_HUNGER_LATE");
  if (await appendReviewNote(hunger,
    "배고픔 결과는 없음/초기/활성/후기. Active에 ‘입가를 건드린 쪽으로 고개를 돌리고 입을 벌림’ 루팅 반사를 명시.",
    "ROOTING")) changes += 1;

  const environment = contractFor("M_ENVIRONMENT_CHECK");
  if (await appendReviewNote(environment,
    "IMPLEMENTED: 방 온도·습도를 실제 숫자로 표시. 첫째 밤 여름 23°C, 둘째·백일째 밤 겨울 26°C. 아기 체온 확인은 별도 Diagnose 행동.",
    "BODY_TEMPERATURE")) changes += 1;

  const stamina = contractFor("M_PLAY_AWAKE_CALM");
  if (await appendReviewNote(stamina,
    "체력 0 도달 시 ParentExhausted 오버레이. 물 한 잔 마시며 숨 고르기(CatchBreath)로 15분 소모·체력 +9·울음 +3.",
    "STAMINA")) changes += 1;

  const diary = contractFor("M_DAWN_OVERLAY");
  if (await appendReviewNote(diary,
    "일지 중심: 알아차린 신호 / 보호자 성향의 성장 / 엄마의 밤 이해 / 다른 보호자의 검수 응원 / 마미톡 공유 카드 문안. 경쟁·정답률·궁합 점수 없음.",
    "DIARY")) changes += 1;

  // 평소 젖병은 이미 소독되어 있다. 소독 화면/행동은 예외 상태에서만 살아난다.
  const feeding = contractFor("M_TAB_FEED_PERSIST");
  if (await setBadge(feeding, "REVIEW REQUIRED", { r: 1, g: 0.9, b: 0.68 })) changes += 1;
  if (await appendReviewNote(feeding,
    "수유를 3단계로 축소: 분유 준비(물+계량+혼합) → 식히고 온도 확인 → 수유. 준비는 ‘주방’ 압축 이동이며 경과 분 동안 아기 상태도 계속 진행.",
    "FEEDING_FLOW")) changes += 1;
  if (await appendReviewNote(feeding,
    "HOME MAP: 아기방↔주방/욕실 2분, 주방↔욕실 3분. Held=false면 아기는 아기방에 남고 Held=true면 함께 이동. 분유 준비·소독은 주방에서만 가능.",
    "HOME_MAP")) changes += 1;

  const sterilize = contractFor("M_FEED_SANITIZED");
  if (sterilize) {
    if (await setBadge(sterilize, "EXCEPTION ONLY", { r: 1, g: 0.9, b: 0.68 })) changes += 1;
    if (await appendReviewNote(sterilize,
      "둘째 밤 시작 시 BottleFoundUnsanitized 돌발로 BottleSanitized=false. 평상시 숨김; 소독 완료 후 PrepareWater로 이동.",
      "STERILIZE_EXCEPTION")) changes += 1;
  }

  const boardTitle = textNodes(board).find(n => n.name === "BOARD_TITLE" || n.characters.indexOf("스토리보드 V6") >= 0);
  if (boardTitle && boardTitle.characters.indexOf("CODE SYNC") < 0) {
    await setText(boardTitle, boardTitle.characters.replace("스토리보드 V6", "스토리보드 V6 · CODE SYNC"));
    changes += 1;
  }

  figma.currentPage.selection = [board];
  figma.viewport.scrollAndZoomIntoView([board]);
  figma.closePlugin("V6 코드 계약 동기화 완료 · " +
    (created ? "싱크 보드 최초 생성" : "기존 최신 싱크 보드 갱신") +
    " · " + changes + "개 항목 갱신");
})();
