/* NOT A NAP — MOBILE_QA_STORYBOARD_V6 code contract synchronizer.
 * The source board is preserved. A duplicated, editable board is updated.
 */

(async () => {
  const fonts = await figma.listAvailableFontsAsync();
  const fallback = fonts.find(f => f.fontName.family === "Inter" && f.fontName.style === "Regular");
  const fallbackBold = fonts.find(f => f.fontName.family === "Inter" && f.fontName.style === "Bold");
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

  async function upsertActionSummary() {
    let panel = board.findOne(n => n.type === "FRAME" && n.name === "_REVIEW_ACTIONS_SUMMARY");
    if (!panel) {
      panel = figma.createFrame();
      panel.name = "_REVIEW_ACTIONS_SUMMARY";
      panel.resize(2200, 720);
      panel.x = 80;
      panel.y = board.height + 80;
      panel.fills = [{ type: "SOLID", color: { r: 0.055, g: 0.09, b: 0.13 } }];
      panel.cornerRadius = 32;
      board.appendChild(panel);
      board.resize(board.width, board.height + 880);
    }

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
      body.resize(2080, 500);
      body.x = 56;
      body.y = 120;
      panel.appendChild(body);
    }
    await setText(body,
      "P0 · #13  맨손 안기와 아기띠 착용/해제 행동 분리\n" +
      "P0 · #15  여름 23°C / 겨울 26°C 계절 시나리오\n" +
      "P1 · #18  코드의 수면 포지셔너 명칭을 암막 커튼으로 교체\n" +
      "P1 · #20-3  관찰 뒤에 근거·권장 범위를 단계적으로 안내\n\n" +
      "단일 기준 · docs/figma-review-actions.md\n" +
      "완료 조건 · 코드 반영 + Unity 테스트 통과 + Figma 계약 동기화");
    return true;
  }

  let changes = 0;
  if (await upsertActionSummary()) changes += 1;
  changes += await replaceAll(board, "Presenter.TryExecuteV2Action 호출", "GameFlowController.ActV2 호출");
  changes += await replaceAll(board, "Presenter.TryExecuteV2Action", "GameFlowController.ActV2");
  changes += await replaceAll(board, "수면 포지셔너", "암막 커튼");
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
  if (await setBadge(hold, "REVIEW REQUIRED", { r: 1, g: 0.9, b: 0.68 })) changes += 1;
  if (await appendReviewNote(hold,
    "품에 안기=맨손 안기(Baby.Held=true). 아기띠 선택 시 별도 착용/해제 행동. 수유 중 안기는 HoldContext.Feeding으로 분리.",
    "HOLD")) changes += 1;

  const awakeCopy = contractFor("M_PLAY_AWAKE_CALM");
  if (await appendReviewNote(awakeCopy,
    "IMPLEMENTED: BabyStateHeadline은 전지적 설명 대신 아빠가 보고 들을 수 있는 관찰을 사용. 기본 문구: ‘울지 않고 아빠를 빤히 바라본다.’",
    "FATHER_PERSPECTIVE")) changes += 1;

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
    "방 온도·습도는 실제 숫자로 표시해 사용자가 판단. 아기 체온 확인은 별도 Diagnose 행동·상태로 분리 필요.",
    "BODY_TEMPERATURE")) changes += 1;

  const stamina = contractFor("M_PLAY_AWAKE_CALM");
  if (await appendReviewNote(stamina,
    "체력 0 도달 시 ParentExhausted 오버레이. 물 한 잔 마시며 숨 고르기(CatchBreath)로 15분 소모·체력 +9·울음 +3.",
    "STAMINA")) changes += 1;

  const diary = contractFor("M_DAWN_OVERLAY");
  if (await appendReviewNote(diary,
    "일지 중심: 오늘 알아차린 신호 / 통했던 반응 / 다음 밤에 기억할 한 가지 / 부모 성장 응원. 등급은 보조 정보. 다음에는 젖병을 미리 소독해두자처럼 구체적으로 기록.",
    "DIARY")) changes += 1;

  // 평소 젖병은 이미 소독되어 있다. 소독 화면/행동은 예외 상태에서만 살아난다.
  const feeding = contractFor("M_TAB_FEED_PERSIST");
  if (await setBadge(feeding, "REVIEW REQUIRED", { r: 1, g: 0.9, b: 0.68 })) changes += 1;
  if (await appendReviewNote(feeding,
    "수유를 3단계로 축소: 분유 준비(물+계량+혼합) → 식히고 온도 확인 → 수유. 안고 기다리기는 체력↔울음 트레이드오프.",
    "FEEDING_FLOW")) changes += 1;

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
