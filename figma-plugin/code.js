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

  const board = source.clone();
  const stamp = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
  board.name = "MOBILE_QA_STORYBOARD_V6_CODE_SYNC_" + stamp;
  board.x = source.x + source.width + 480;
  board.y = source.y;

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

  async function appendReviewNote(contract, message) {
    if (!contract) return false;
    const existing = textNodes(contract).find(n => n.name === "CODE_SYNC_REVIEW_NOTE");
    if (existing) {
      await setText(existing, message);
      return true;
    }
    const note = figma.createText();
    note.name = "CODE_SYNC_REVIEW_NOTE";
    note.fontName = fallbackBold ? fallbackBold.fontName : fallback.fontName;
    note.fontSize = 16;
    note.characters = message;
    note.fills = [{ type: "SOLID", color: { r: 0.65, g: 0.18, b: 0.2 } }];
    note.textAutoResize = "HEIGHT";
    note.resize(Math.max(240, contract.width - 64), 60);
    note.x = 32;
    note.y = contract.height - 88;
    contract.appendChild(note);
    return true;
  }

  let changes = 0;
  changes += await replaceAll(board, "Presenter.TryExecuteV2Action 호출", "GameFlowController.ActV2 호출");
  changes += await replaceAll(board, "Presenter.TryExecuteV2Action", "GameFlowController.ActV2");

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

  const item = contractFor("M_ITEM_SCROLL");
  if (await replaceValue(item, ["SelectItem"], "— (ItemId 사용)")) changes += 1;
  if (await replaceValue(item, ["V2NightFactory.IsSelectableItem 확인"], "GameFlowController.ToggleV2Item(ItemId) → IsSelectableItem 확인")) changes += 1;
  if (await setBadge(item, "REVIEW REQUIRED", { r: 1, g: 0.9, b: 0.68 })) changes += 1;
  if (await appendReviewNote(item, "코드 선택 목록: 아기띠 / 쪽쪽이 / 백색소음기 / 베이비 모니터. PLAY 연결: 백색소음기→돌보기/ToggleNoise, 모니터→살펴보기/CheckMonitor. 분유제조기는 제품 결정 필요.")) changes += 1;

  const unlock = contractFor("M_UNLOCK_CANDIDATES");
  if (await setBadge(unlock, "IMPLEMENTED", green)) changes += 1;
  if (await appendReviewNote(unlock, "세 후보 모두 현재 코드에서 선택 불가 카드로 표시됨: 옆잠베개 / 수면 포지셔너 / 토닥이인형.")) changes += 1;

  // 2026-07-22 플레이테스트 피드백을 개발 계약에 동기화한다.
  const laydown = contractContaining(["V2ActionId.Laydown", "Laydown"]);
  if (await appendReviewNote(laydown,
    "선행 조건: Held=true + REM/NREM 수면. 아직 잠들지 않았으면 시간·각성 이벤트 없이 거부(BabyNotAsleep).")) changes += 1;

  const diaperWet = contractFor("M_DIAPER_CHECK_WET");
  if (await setBadge(diaperWet, "IMPLEMENTED", green)) changes += 1;
  if (await appendReviewNote(diaperWet,
    "CheckDiaper 결과 DiaperCheckResult.Wet → ‘기저귀가 젖어 있어요. 기저귀를 갈아주세요.’ 표시.")) changes += 1;

  const diaperClean = contractFor("M_DIAPER_CHECK_CLEAN");
  if (await setBadge(diaperClean, "IMPLEMENTED", green)) changes += 1;
  if (await appendReviewNote(diaperClean,
    "CheckDiaper 결과 DiaperCheckResult.Clean → 다른 신호 확인 안내. 안전한 배제 검사라 오판 0.")) changes += 1;

  const hunger = contractContaining(["CheckHungerSignals", "HungerSignalStage"]);
  if (await appendReviewNote(hunger,
    "배고픔 각성은 HungerLateThreshold 이상. 확인 결과는 없음/초기/활성/후기 신호와 수유 필요 여부를 명시.")) changes += 1;

  const environment = contractContaining(["CheckEnvironment", "AdjustHumidity", "AdjustTemperature"]);
  if (await appendReviewNote(environment,
    "확인 결과에 실제 온도·습도 숫자 표시. 권장 범위 20–22°C / 40–60%; 조절 시 범위 안으로 보정.")) changes += 1;

  const stamina = contractContaining(["보호자 체력", "ParentStamina"]);
  if (await appendReviewNote(stamina,
    "체력 0 도달 시 ParentExhausted 오버레이. 물 한 잔 마시며 숨 고르기(CatchBreath)로 15분 소모·체력 +9·울음 +3.")) changes += 1;

  const diary = contractContaining(["AdvanceNight", "BuildV2Diary", "처음부터 다시"]);
  if (await appendReviewNote(diary,
    "첫째 밤 → 둘째 밤 → 백일째 밤은 같은 RunState로 진행. 마지막 밤 이후에만 처음부터 다시 시작.")) changes += 1;

  // 평소 젖병은 이미 소독되어 있다. 소독 화면/행동은 예외 상태에서만 살아난다.
  const feeding = contractContaining(["PrepareWater", "MeasureFormula", "FeedPreparedBottle"]);
  if (await appendReviewNote(feeding,
    "기본값 BottleSanitized=true. 일반 수유 준비는 물 준비부터 시작하며 젖병 소독 버튼은 숨김.")) changes += 1;

  const sterilize = contractContaining(["SterilizeBottle", "젖병 소독"]);
  if (sterilize) {
    if (await setBadge(sterilize, "EXCEPTION ONLY", { r: 1, g: 0.9, b: 0.68 })) changes += 1;
    if (await appendReviewNote(sterilize,
      "둘째 밤 시작 시 BottleFoundUnsanitized 돌발로 BottleSanitized=false. 평상시 숨김; 소독 완료 후 PrepareWater로 이동.")) changes += 1;
  }

  const boardTitle = textNodes(board).find(n => n.name === "BOARD_TITLE" || n.characters.indexOf("스토리보드 V6") >= 0);
  if (boardTitle) {
    await setText(boardTitle, boardTitle.characters.replace("스토리보드 V6", "스토리보드 V6 · CODE SYNC"));
    changes += 1;
  }

  figma.currentPage.selection = [board];
  figma.viewport.scrollAndZoomIntoView([board]);
  figma.closePlugin("V6 코드 계약 동기화 완료 · 원본 보존 · " + changes + "개 항목 갱신");
})();
