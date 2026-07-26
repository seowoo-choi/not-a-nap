/* NOT A NAP — Unity V8 presentation contract synchronizer.
 * MOBILE_QA_STORYBOARD_V6 is preserved. The latest editable CODE_SYNC board is updated.
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

  const SYNC_VERSION = "V8 · Unity 799d17b · 2026-07-26";
  const THEME = {
    ink: { r: 0.012, g: 0.025, b: 0.045 },
    glass: { r: 0.025, g: 0.055, b: 0.09 },
    line: { r: 0.34, g: 0.4, b: 0.47 },
    cream: { r: 0.97, g: 0.94, b: 0.87 },
    muted: { r: 0.74, g: 0.79, b: 0.84 },
    gold: { r: 0.96, g: 0.68, b: 0.3 },
    green: { r: 0.49, g: 0.84, b: 0.61 },
    blue: { r: 0.4, g: 0.72, b: 0.91 },
    violet: { r: 0.72, g: 0.56, b: 0.94 },
    skin: { r: 0.92, g: 0.7, b: 0.56 }
  };

  function addSyncText(parent, name, value, x, y, width, size, bold, color, alignment) {
    const node = figma.createText();
    node.name = name;
    node.fontName = bold && fallbackBold ? fallbackBold.fontName : fallback.fontName;
    node.fontSize = size;
    node.characters = value;
    node.fills = [{ type: "SOLID", color }];
    node.textAutoResize = "HEIGHT";
    node.textAlignHorizontal = alignment || "LEFT";
    node.lineHeight = { value: Math.max(size * 1.45, size + 8), unit: "PIXELS" };
    node.resize(width, Math.max(size * 1.5, 28));
    parent.appendChild(node);
    node.x = x;
    node.y = y;
    return node;
  }

  function addSyncFrame(parent, name, x, y, width, height, color, opacity, stroke, radius) {
    const frame = figma.createFrame();
    frame.name = name;
    frame.resize(width, height);
    frame.x = x;
    frame.y = y;
    frame.fills = color ? [{ type: "SOLID", color, opacity: opacity == null ? 1 : opacity }] : [];
    frame.strokes = stroke ? [{ type: "SOLID", color: stroke }] : [];
    frame.strokeWeight = stroke ? 2 : 0;
    frame.cornerRadius = radius || 0;
    frame.clipsContent = false;
    parent.appendChild(frame);
    return frame;
  }

  function addRoomPill(parent, name, x, y, active, babyHere) {
    const room = addSyncFrame(parent, "ROOM_PILL__" + name, x, y, 270, 62,
      THEME.glass, active ? 0.82 : 0.58, active ? THEME.gold : THEME.line, 31);
    addSyncText(room, "ROOM_LABEL", name + (babyHere ? "  · 아기" : ""),
      16, 10, 238, 27, true, active ? THEME.cream : THEME.muted, "CENTER");
    return room;
  }

  function addStatusCard(parent, name, value, x, color) {
    const card = addSyncFrame(parent, "STATUS__" + name, x, 772, 310, 108,
      THEME.glass, 0.5, THEME.line, 0);
    const accent = addSyncFrame(card, "ACCENT", 12, 14, 5, 80, color, 1, null, 0);
    accent.name = "ACCENT__" + name;
    addSyncText(card, "LABEL", name, 30, 5, 175, 27, true, THEME.muted, "LEFT");
    addSyncText(card, "VALUE", value, 208, 3, 82, 35, true, THEME.cream, "RIGHT");
    addSyncFrame(card, "PROGRESS_TRACK", 30, 83, 258, 7, { r: 0.08, g: 0.13, b: 0.18 }, 1, null, 0);
    addSyncFrame(card, "PROGRESS_VALUE", 30, 83, name === "보호자 체력" ? 210 : 132, 7,
      color, 1, null, 0);
  }

  function screenCopy(screenId) {
    if (screenId.indexOf("HUNGER") >= 0)
      return { title: "입과 손의 신호를 살펴봐요", signal: "입가를 건드린 쪽으로 고개를 돌리고 입을 벌린다" };
    if (screenId.indexOf("NREM") >= 0 || screenId.indexOf("LIMBS_RELAXED") >= 0)
      return { title: "눈꺼풀이 편안하고 숨이 고르다", signal: "팔다리의 힘이 풀리고 호흡이 일정해졌어요" };
    if (screenId.indexOf("CRY") >= 0)
      return { title: "울음이 커지고 몸에 힘이 들어간다", signal: "표정만 보지 말고 입·손·호흡·몸의 방향을 함께 살펴보세요" };
    return { title: "울지 않고 아빠를 빤히 바라본다", signal: "표정만 보지 말고 입·손·호흡·몸의 방향을 함께 살펴보세요" };
  }

  function actionMotionFor(screenId) {
    if (screenId.indexOf("DIAPER_CHECK") >= 0) return "기저귀를 살짝 확인";
    if (screenId.indexOf("LIMBS_RELAXED") >= 0) return "팔다리의 힘을 천천히 확인";
    if (screenId.indexOf("PACIFIER") >= 0) return "입가에 쪽쪽이를 조심히 건네요";
    if (screenId.indexOf("FEED_COMPLETE") >= 0) return "삼키는 리듬에 맞춰 수유";
    if (screenId.indexOf("LAYDOWN") >= 0 || screenId.indexOf("MORO") >= 0)
      return "숨의 리듬을 지키며 눕혀요";
    return null;
  }

  function addActionMotion(parent, label) {
    const motion = addSyncFrame(parent, "ACTION_MOTION__" + label, 325, 630, 430, 70,
      THEME.glass, 0.7, THEME.gold, 0);
    addSyncText(motion, "ACTION_MOTION_LABEL", label, 18, 12, 394, 24, true,
      { r: 1, g: 0.87, b: 0.64 }, "CENTER");
    for (let i = 0; i < 3; i++) {
      const hand = figma.createEllipse();
      hand.name = "HAND_KEYFRAME__" + (i + 1);
      hand.resize(42, 28);
      hand.x = 92 + i * 116;
      hand.y = -52 + (i === 1 ? 12 : 0);
      hand.fills = [{ type: "SOLID", color: THEME.skin, opacity: i === 1 ? 1 : 0.38 }];
      motion.appendChild(hand);
    }
    return motion;
  }

  function upsertUnityPresentation(screenId) {
    const screen = screenFor(screenId);
    if (!screen) return false;

    const staleLayers = screen.findAll(n =>
      n.name === "CODE_SYNC_HOME_MAP" ||
      n.name === "CODE_SYNC_UNITY_PRESENTATION_V8");
    for (const stale of staleLayers) stale.remove();

    const sx = screen.width / 1080;
    const sy = screen.height / 1920;
    const overlay = figma.createFrame();
    overlay.name = "CODE_SYNC_UNITY_PRESENTATION_V8";
    overlay.resize(1080, 1920);
    overlay.x = 0;
    overlay.y = 0;
    overlay.fills = [];
    overlay.clipsContent = false;
    screen.appendChild(overlay);

    addSyncText(overlay, "SYNC_VERSION", SYNC_VERSION, 48, 16, 984, 18, true,
      THEME.gold, "RIGHT");
    addSyncText(overlay, "CLOCK", "21:00", 54, 65, 250, 51, true, THEME.cream, "LEFT");
    addSyncText(overlay, "TIME_REMAINING", "새벽까지 9시간 00분", 610, 72, 414, 29,
      true, THEME.cream, "RIGHT");
    addSyncFrame(overlay, "TIME_PROGRESS", 735, 145, 289, 5, THEME.gold, 1, null, 0);

    const copy = screenCopy(screenId);
    const signal = addSyncFrame(overlay, "SIGNAL_RIBBON", 46, 176, 760, 104,
      THEME.glass, 0.62, THEME.line, 0);
    addSyncFrame(signal, "SIGNAL_ACCENT", 0, 12, 4, 80, THEME.gold, 1, null, 0);
    addSyncText(signal, "SIGNAL_HEADLINE", copy.title, 24, 4, 712, 31, true,
      { r: 0.98, g: 0.87, b: 0.68 }, "LEFT");
    addSyncText(signal, "SIGNAL_BODY", copy.signal, 24, 47, 712, 29, false,
      { r: 0.92, g: 0.93, b: 0.93 }, "LEFT");

    addRoomPill(overlay, "아기방", 119, 720, true, true);
    addRoomPill(overlay, "주방", 405, 720, false, false);
    addRoomPill(overlay, "욕실", 691, 720, false, false);

    addStatusCard(overlay, "연속 수면", "0분", 46, THEME.blue);
    addStatusCard(overlay, "보호자 체력", "100", 385, THEME.green);
    addStatusCard(overlay, "마음의 여유", "50", 724, THEME.violet);

    const feedback = addSyncFrame(overlay, "SCENE_FEEDBACK", 58, 912, 964, 118,
      THEME.glass, 0.64, THEME.line, 0);
    addSyncText(feedback, "FEEDBACK_TITLE", "작은 숨소리가 방 안에 이어진다.",
      28, 5, 908, 33, true, { r: 0.98, g: 0.9, b: 0.76 }, "LEFT");
    addSyncText(feedback, "FEEDBACK_BODY", "표정과 입·손·호흡의 방향을 함께 살펴보세요.",
      28, 58, 908, 27, false, THEME.muted, "LEFT");

    const deck = addSyncFrame(overlay, "COMMAND_DECK", 0, 1060, 1080, 860,
      THEME.ink, 0.46, null, 0);
    addSyncFrame(deck, "COMMAND_TOP_LINE", 0, 0, 1080, 3, THEME.gold, 0.72, null, 0);
    addSyncText(deck, "COMMAND_TITLE", "어떻게 돌볼까요?", 48, 30, 700, 33,
      true, THEME.cream, "LEFT");
    const tabs = ["살펴보기", "돌보기", "수유 준비"];
    for (let i = 0; i < tabs.length; i++) {
      const tab = addSyncFrame(deck, "TAB__" + tabs[i], 48 + i * 339, 100, 305, 68,
        THEME.glass, i === 0 ? 0.82 : 0.58, i === 0 ? THEME.gold : THEME.line, 0);
      addSyncText(tab, "TAB_LABEL", tabs[i], 12, 11, 281, 30, true,
        i === 0 ? THEME.cream : THEME.muted, "CENTER");
    }
    const actions = ["기저귀 확인", "배고픔 신호 확인", "온도·습도", "팔다리 이완 확인",
      "잠시 망설임", "숨 고르고 신호 기다리기"];
    for (let i = 0; i < actions.length; i++) {
      const col = i % 2;
      const row = Math.floor(i / 2);
      const action = addSyncFrame(deck, "ACTION__" + actions[i],
        48 + col * 501, 194 + row * 112, 483, 94, THEME.glass, 0.66, THEME.line, 0);
      const sigil = addSyncFrame(action, "ACTION_SIGIL", 12, 13, 68, 68,
        { r: 0.62, g: 0.34, b: 0.12 }, 0.94, null, 0);
      addSyncText(sigil, "SIGIL_TEXT", actions[i].slice(0, 1), 4, 11, 60, 25,
        true, THEME.cream, "CENTER");
      addSyncText(action, "ACTION_LABEL", actions[i], 98, 17, 365, 30,
        true, THEME.cream, "LEFT");
    }

    const motionLabel = actionMotionFor(screenId);
    if (motionLabel) addActionMotion(overlay, motionLabel);

    overlay.rescale(Math.min(sx, sy));
    overlay.x = (screen.width - overlay.width) / 2;
    overlay.y = (screen.height - overlay.height) / 2;

    return true;
  }

  function upsertSetupPresentation(screenId) {
    const screen = screenFor(screenId);
    if (!screen) return false;
    const staleLayers = screen.findAll(n => n.name === "CODE_SYNC_SETUP_PRESENTATION_V8");
    for (const stale of staleLayers) stale.remove();

    const overlay = figma.createFrame();
    overlay.name = "CODE_SYNC_SETUP_PRESENTATION_V8";
    overlay.resize(1080, 1920);
    overlay.x = 0;
    overlay.y = 0;
    overlay.fills = [{ type: "SOLID", color: THEME.ink, opacity: 0.38 }];
    overlay.clipsContent = false;
    screen.appendChild(overlay);

    addSyncText(overlay, "SYNC_VERSION", SYNC_VERSION, 48, 16, 984, 18, true,
      THEME.gold, "RIGHT");
    addSyncText(overlay, "SETUP_TITLE", "첫째 밤 · 밤 준비", 48, 55, 750, 64,
      true, THEME.cream, "LEFT");
    addSyncText(overlay, "SETUP_COUNT", "가져갈 물건  0 / 3", 48, 354, 984, 42,
      true, THEME.cream, "LEFT");

    const items = [
      { name: "아기띠", color: { r: 0.91, g: 0.74, b: 0.58 } },
      { name: "쪽쪽이", color: { r: 0.96, g: 0.66, b: 0.28 } },
      { name: "백색소음기", color: { r: 0.94, g: 0.84, b: 0.58 } },
      { name: "베이비 모니터", color: { r: 0.88, g: 0.72, b: 0.42 } }
    ];
    for (let i = 0; i < items.length; i++) {
      const col = i % 2;
      const row = Math.floor(i / 2);
      const x = 48 + col * 510;
      const y = 425 + row * 390;
      const item = addSyncFrame(overlay, "ITEM_SHOWCASE__" + items[i].name,
        x, y, 474, 390, null, 0, null, 0);
      const glow = figma.createEllipse();
      glow.name = "ITEM_GLOW";
      glow.resize(300, 300);
      glow.x = 87;
      glow.y = 8;
      glow.fills = [{ type: "SOLID", color: THEME.gold, opacity: 0.12 }];
      item.appendChild(glow);
      const art = figma.createEllipse();
      art.name = "ITEM_ART_SLOT__REPLACE_WITH_PNG";
      art.resize(270, 270);
      art.x = 102;
      art.y = 30;
      art.fills = [{ type: "SOLID", color: items[i].color }];
      art.effects = [{ type: "DROP_SHADOW", color: { r: 0, g: 0, b: 0, a: 0.42 },
        offset: { x: 0, y: 18 }, radius: 18, spread: 0, visible: true, blendMode: "NORMAL" }];
      item.appendChild(art);
      addSyncText(item, "ITEM_NAME", items[i].name, 0, 298, 474, 32,
        true, THEME.cream, "CENTER");
    }

    const detail = addSyncFrame(overlay, "ITEM_DETAIL_PANEL", 48, 1250, 984, 240,
      THEME.glass, 0.72, THEME.line, 0);
    addSyncFrame(detail, "DETAIL_ACCENT", 0, 0, 6, 240, THEME.gold, 1, null, 0);
    addSyncText(detail, "DETAIL_TITLE", "아기띠", 34, 12, 916, 42, true,
      THEME.cream, "LEFT");
    addSyncText(detail, "DETAIL_BODY", "착용하면 계속 안은 상태가 됩니다. 진정 효과가 크고 잠들기 쉬워집니다.",
      34, 70, 916, 34, false, THEME.cream, "LEFT");
    addSyncText(detail, "DETAIL_MEMORY", "기억할 점 · 반복하면 아기가 습관으로 학습합니다.",
      34, 140, 916, 26, true, { r: 0.94, g: 0.76, b: 0.52 }, "LEFT");
    addSyncText(overlay, "SETUP_HELP", "소품을 눌러 오늘 밤의 진열대에 올리세요.",
      48, 1505, 984, 26, true, THEME.muted, "LEFT");
    const cta = addSyncFrame(overlay, "SETUP_CTA", 100, 1695, 880, 120,
      THEME.glass, 0.9, THEME.gold, 0);
    addSyncText(cta, "CTA_LABEL", "물건을 3개 골라주세요", 24, 30, 832, 34,
      true, THEME.muted, "CENTER");

    const scale = Math.min(screen.width / 1080, screen.height / 1920);
    overlay.rescale(scale);
    overlay.x = (screen.width - overlay.width) / 2;
    overlay.y = (screen.height - overlay.height) / 2;
    return true;
  }

  async function upsertMotionSpec() {
    let panel = board.findOne(n => n.type === "FRAME" && n.name === "_ACTION_MOTION_SPEC_V8");
    const summary = board.findOne(n => n.type === "FRAME" && n.name === "_REVIEW_ACTIONS_SUMMARY");
    if (!panel) {
      panel = figma.createFrame();
      panel.name = "_ACTION_MOTION_SPEC_V8";
      panel.resize(2200, 1700);
      panel.x = 80;
      panel.y = summary ? summary.y + summary.height + 80 : board.height + 80;
      panel.fills = [{ type: "SOLID", color: { r: 0.035, g: 0.06, b: 0.09 } }];
      panel.cornerRadius = 32;
      board.appendChild(panel);
    }
    for (const child of [...panel.children]) child.remove();
    panel.resize(2200, 1700);
    addSyncText(panel, "MOTION_SPEC_TITLE", "ACTION MOTION · 1.05초 Presentation 계약",
      56, 48, 2088, 36, true, THEME.cream, "LEFT");
    addSyncText(panel, "MOTION_SPEC_SUBTITLE",
      "Core 판정이 Accepted=true일 때만 재생 · 0% 진입 → 50% 접촉/동작 → 100% 퇴장",
      56, 104, 2088, 24, false, THEME.muted, "LEFT");

    const actions = [
      ["CHECK_DIAPER", "기저귀 확인", "양손이 허리 아래로 들어감"],
      ["CHANGE_DIAPER", "기저귀 갈기", "새 기저귀가 아래에서 올라옴"],
      ["CHECK_HUNGER", "배고픔 신호", "손이 입가 바깥까지 접근"],
      ["CHECK_RELAXATION", "팔다리 이완", "양손이 팔다리를 천천히 확인"],
      ["HOLD", "품에 안기", "아기가 54px 들리고 좌우로 흔들림"],
      ["PAT", "토닥이기", "손이 3회 왕복하고 아기가 작게 흔들림"],
      ["PACIFIER", "쪽쪽이", "소품이 화면 밖에서 입가로 이동"],
      ["LAYDOWN", "눕히기", "양손이 받친 채 아기가 34px 내려감"],
      ["FEED", "수유", "젖병이 입가로 이동하고 내용물이 표시됨"]
    ];
    for (let i = 0; i < actions.length; i++) {
      const col = i % 3;
      const row = Math.floor(i / 3);
      const card = addSyncFrame(panel, "MOTION__" + actions[i][0],
        56 + col * 700, 180 + row * 470, 650, 410, THEME.glass, 0.72, THEME.line, 0);
      addSyncText(card, "ACTION_NAME", actions[i][1], 28, 24, 594, 30, true,
        THEME.cream, "LEFT");
      addSyncText(card, "ACTION_DETAIL", actions[i][2], 28, 70, 594, 21, false,
        THEME.muted, "LEFT");
      for (let frameIndex = 0; frameIndex < 3; frameIndex++) {
        const keyframe = addSyncFrame(card, "KEYFRAME__" + frameIndex,
          28 + frameIndex * 202, 132, 180, 210, THEME.ink, 0.72, THEME.line, 0);
        const baby = figma.createEllipse();
        baby.name = "BABY";
        baby.resize(72, 92);
        baby.x = 54;
        baby.y = 54 - (actions[i][0] === "HOLD" && frameIndex === 1 ? 22 : 0);
        baby.fills = [{ type: "SOLID", color: { r: 0.95, g: 0.76, b: 0.6 } }];
        keyframe.appendChild(baby);
        const hand = figma.createEllipse();
        hand.name = "CAREGIVER_HAND";
        hand.resize(42, 24);
        hand.x = frameIndex === 0 ? 132 : frameIndex === 1 ? 96 : 142;
        hand.y = actions[i][0] === "PAT" ? 64 + frameIndex * 18 : 108;
        hand.fills = [{ type: "SOLID", color: THEME.skin,
          opacity: frameIndex === 1 ? 1 : 0.42 }];
        keyframe.appendChild(hand);
        addSyncText(keyframe, "TIME", frameIndex === 0 ? "0%" : frameIndex === 1 ? "50%" : "100%",
          12, 168, 156, 16, true, THEME.gold, "CENTER");
      }
    }
    const requiredBoardHeight = panel.y + panel.height + 80;
    if (board.height < requiredBoardHeight) board.resize(board.width, requiredBoardHeight);
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
      "동기화 기준 · " + SYNC_VERSION + "\n" +
      "이번 반영 · 공주풍 아기방/주방/욕실 독립 배경 + 아기 중심 구도\n" +
      "이번 반영 · 미니맵 제거 + 아기방/주방/욕실 방 이동 알약 3개\n" +
      "이번 반영 · 직선형 반투명 HUD + 금색 선택선 + 충분한 한글 line-height\n" +
      "이번 반영 · 진열형 아이템 2×2 + 독립 설명 패널 + 선택 광택\n" +
      "이번 반영 · 기저귀/배고픔/이완/안기/토닥임/눕히기/수유 1.05초 행동 모션\n" +
      "이번 반영 · 빌드 용량 상한 제거. 필수 WebGL 산출물 정합성만 검사\n\n" +
      "완료 · #18  부적절한 수면 보조 장비를 암막 커튼으로 교체\n" +
      "P1 · #20-2  원인별 관찰 신호를 결정론적 시드로 변주\n" +
      "P1 · #20-3  관찰 → 근거 → 권장 행동을 단계적으로 안내\n" +
      "P2 · #20-4  30초 광고 → 옆잠베개 활성화·깊은 수면 +5 제안 검토\n" +
      "             안전·수면 판정 버프 제외. 심사·광고 표기·SDK·제품 안전 검토 후 결정\n" +
      "제품 확장 · 실제 마미톡 로그인/공유, 익명 집계/응원, 브랜드 계약은 서버·운영 정책 후\n\n" +
      "완료 · #4 #5 #6 #8–#17 #19 #20-1 #21 #23\n" +
      "유지 · #7 #22 #24\n\n" +
      "전체 댓글별 처리표\n" +
      "https://github.com/seowoo-choi/not-a-nap/blob/main/docs/figma-review-actions.md\n\n" +
      "완료 조건 · 코드 반영 + Unity 테스트 통과 + Figma 계약 동기화");
    return true;
  }

  let changes = 0;
  if (await upsertActionSummary()) changes += 1;
  if (await upsertMotionSpec()) changes += 1;
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

  // 현재 Unity 세로 PLAY 좌표를 복제한다. 구형 CODE_SYNC_HOME_MAP은 실행 시 제거한다.
  // 같은 화면에서 다시 실행하면 V8 동기화 레이어를 교체하므로 중복되지 않는다.
  const presentationScreens = visualImplemented.concat([
    "M_DIAPER_CHECK_WET", "M_DIAPER_CHECK_CLEAN", "M_ENVIRONMENT_CHECK",
    "M_TAB_CARE_PERSIST", "M_TAB_FEED_PERSIST", "M_SLEEP_FAST_FORWARD",
    "M_LAYDOWN_SUCCESS", "M_FEED_COMPLETE"
  ]);
  for (const id of [...new Set(presentationScreens)]) {
    if (upsertUnityPresentation(id)) changes += 1;
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
  if (await setBadge(item, "IMPLEMENTED", green)) changes += 1;
  if (await appendReviewNote(item,
    "CURRENT UNITY: 스크롤 카드가 아니라 진열형 2×2. 아기띠 / 쪽쪽이 / 백색소음기 / 베이비 모니터를 카드 박스 없이 크게 보여주고, 이름·선택 배지 아래에 독립 설명 패널을 둔다.",
    "ITEM_SHOWCASE")) changes += 1;
  if (upsertSetupPresentation("M_ITEM_SCROLL")) changes += 1;

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
  if (await setBadge(pat, "IMPLEMENTED", green)) changes += 1;
  if (await appendReviewNote(pat,
    "IMPLEMENTED: 침대 토닥임과 품 안 토닥임을 Held로 구분. 수락된 Pat 직후 보호자 손이 1.05초 왕복하고 아기 자세가 흔들린다. 침대에서 잠들면 다시 눕히기 안내 금지.",
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
    "IMPLEMENTED: 방 온도·습도를 실제 숫자로 표시. 첫째 밤 여름 23°C, 둘째·백일째 밤 겨울 26°C. 별도 ‘아기 체온 확인’ 버튼은 현재 UI에서 제거.",
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
  if (await setBadge(feeding, "IMPLEMENTED", green)) changes += 1;
  if (await appendReviewNote(feeding,
    "CURRENT UNITY: 평상시 수유 UI는 분유 준비(PrepareWater) → 식히기(CoolBottle) → 수유(FeedPreparedBottle) 3단계. 젖병 소독은 둘째 밤 돌발에서만 예외 노출. 각 행동은 위치·선행조건을 Core에서 검증한다.",
    "FEEDING_FLOW")) changes += 1;
  if (await appendReviewNote(feeding,
    "ROOM RIBBON: 미니맵 없음. 화면 위 아기방/주방/욕실 알약 버튼으로 이동. 아기방↔주방/욕실 2분, 주방↔욕실 3분. Held=false면 아기는 아기방에 남고 Held=true면 함께 이동.",
    "ROOM_RIBBON")) changes += 1;

  const sterilize = contractFor("M_FEED_SANITIZED");
  if (sterilize) {
    if (await setBadge(sterilize, "EXCEPTION ONLY", { r: 1, g: 0.9, b: 0.68 })) changes += 1;
    if (await appendReviewNote(sterilize,
      "둘째 밤 시작 시 BottleFoundUnsanitized 돌발로 BottleSanitized=false. 평상시 숨김; 소독 완료 후 PrepareWater로 이동.",
      "STERILIZE_EXCEPTION")) changes += 1;
  }

  const boardTitle = textNodes(board).find(n => n.name === "BOARD_TITLE" || n.characters.indexOf("스토리보드 V6") >= 0);
  if (boardTitle) {
    const baseTitle = boardTitle.characters
      .replace(/\s*·\s*CODE SYNC(?:\s*·\s*V8)?/g, "")
      .replace(/\s*·\s*Unity [0-9a-f]+/g, "");
    await setText(boardTitle, baseTitle + " · CODE SYNC · V8");
    changes += 1;
  }

  figma.currentPage.selection = [board];
  figma.viewport.scrollAndZoomIntoView([board]);
  figma.closePlugin("V8 Unity 화면 계약 동기화 완료 · " +
    (created ? "싱크 보드 최초 생성" : "기존 최신 싱크 보드 갱신") +
    " · " + changes + "개 항목 갱신");
})();
