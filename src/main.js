const GAME_WIDTH = 960;
const GAME_HEIGHT = 540;
const LANE_COUNT = 3;
const MAX_MISSES = 6;
const BASELINE_Y = GAME_HEIGHT - 88;
const MENU_DWELL_MS = 820;
const MENU_BACK_HOLD_MS = 650;
const GESTURE_STABILITY_FRAMES = 3;
const CURSOR_EDGE_PADDING = 14;
const SANDBOX_MODE = true;

const SETTINGS_CONFIG = {
  confirm: [420, 560, 720],
  difficulty: ['轻松', '标准', '紧张']
};

const SPELLS = {
  fire: {
    key: 'fire',
    label: '火焰印',
    gesture: '握拳',
    color: '#ff8f6b',
    short: '爆裂',
    hint: '握拳稳定保持，直接击破最前方威胁。'
  },
  ice: {
    key: 'ice',
    label: '冰霜印',
    gesture: 'V 手势',
    color: '#8be9ff',
    short: '冻结',
    hint: '做出 V 手势稳定保持，优先冻结高速目标。'
  },
  shield: {
    key: 'shield',
    label: '护盾印',
    gesture: '张掌',
    color: '#ffd76a',
    short: '护盾',
    hint: '张开手掌稳定保持，展开一次结界护盾。'
  }
};

const SPELL_ORDER = [SPELLS.fire, SPELLS.ice, SPELLS.shield];

const ENEMY_TYPES = {
  orb: { key: 'orb', label: '普通魔球', color: '#7ad8ff', radius: 18, speed: 154, damage: 1, hp: 1 },
  swift: { key: 'swift', label: '疾行碎影', color: '#9dffcf', radius: 15, speed: 244, damage: 1, hp: 1 },
  heavy: { key: 'heavy', label: '重压核心', color: '#ffb36b', radius: 24, speed: 124, damage: 2, hp: 2 }
};

const SCREEN_COPY = {
  menu: '菜单指令：食指指向移动焦点，停留确认；进入子页面后，张掌可快速返回。',
  settings: '设置指令：食指指向条目并停留切换；张掌或“返回主菜单”都能退出。',
  tutorial: '教程指令：先学指向与停留确认；战斗中可直接从一个结印切到下一个结印。',
  training: '训练指令：可直接切换握拳 / V / 张掌，不必先收势；张掌停留可返回主菜单。',
  playing: SANDBOX_MODE
    ? '当前为测试模式：敌人穿线不会结束本局。可直接切换手势连续施法，张掌仍可护盾。'
    : '根据敌情直接切换结印：握拳=火焰，V 手势=冰霜，张掌=护盾，无需先收势。',
  results: '结果页指令：食指指向按钮并停留确认；张掌也可快速回到主菜单。'
};

const dom = {
  video: document.getElementById('camera-feed'),
  overlay: document.getElementById('tracking-overlay'),
  overlayContext: document.getElementById('tracking-overlay').getContext('2d'),
  gameCanvas: document.getElementById('game-canvas'),
  gameContext: document.getElementById('game-canvas').getContext('2d'),
  cameraStatus: document.getElementById('camera-status'),
  handStatus: document.getElementById('hand-status'),
  cameraHint: document.getElementById('camera-hint'),
  score: document.getElementById('score-value'),
  miss: document.getElementById('miss-value'),
  control: document.getElementById('control-value'),
  fps: document.getElementById('fps-value')
};

const LANE_WIDTH = GAME_WIDTH / LANE_COUNT;
const LANE_CENTERS = [LANE_WIDTH * 0.5, LANE_WIDTH * 1.5, LANE_WIDTH * 2.5];

const state = {
  handPresent: false,
  handX: 0.5,
  handY: 0.5,
  cameraReady: false,
  fps: 0,
  previousFrameTime: 0,
  screen: 'menu',
  controlLabel: '待命',
  recognizedGesture: 'none',
  gestureLabel: '未检测到手',
  gestureFilter: {
    candidateKey: 'none',
    candidateLabel: '未检测到手',
    stableKey: 'none',
    stableLabel: '未检测到手',
    frames: 0
  },
  cursor: {
    active: false,
    x: GAME_WIDTH / 2,
    y: GAME_HEIGHT / 2
  },
  menu: {
    focusedKey: null,
    dwellKey: null,
    backStartedAt: 0,
    startedAt: 0,
    progress: 0,
    regions: []
  },
  settings: {
    confirmMs: SETTINGS_CONFIG.confirm[1],
    difficulty: SETTINGS_CONFIG.difficulty[1]
  },
  training: createTrainingState(),
  combat: createCombatState()
};

function createCombatState() {
  return {
    score: 0,
    misses: 0,
    spawnTimer: 0.8,
    gameOver: false,
    enemies: [],
    flashes: [],
    runeBursts: [],
    shield: {
      activeUntil: 0,
      charges: 0
    },
    combo: 0,
    maxCombo: 0,
    casts: 0,
    hits: 0,
    elapsed: 0,
    lastCastSpellKey: null,
    pendingSpell: createPendingSpell()
  };
}

function createTrainingState() {
  return {
    casts: 0,
    streak: 0,
    pointerChecks: 0,
    totals: {
      fire: 0,
      ice: 0,
      shield: 0
    },
    lastSpellKey: null,
    lastSpellAt: 0,
    runeBursts: [],
    flashes: [],
    lastCastSpellKey: null,
    pendingSpell: createPendingSpell()
  };
}

function createPendingSpell() {
  return {
    key: null,
    startedAt: 0,
    progress: 0
  };
}

function resetCombat() {
  state.combat = createCombatState();
  state.controlLabel = '待命';
  syncStats();
}

function resetTraining() {
  state.training = createTrainingState();
  state.controlLabel = '训练场';
  syncStats();
}

function startRun() {
  resetCombat();
  state.screen = 'playing';
  setBadge(dom.handStatus, '等待结印', 'pending');
  dom.cameraHint.textContent = SCREEN_COPY.playing;
}

function startTraining() {
  resetTraining();
  state.screen = 'training';
  setBadge(dom.handStatus, '训练场待命', 'pending');
  dom.cameraHint.textContent = SCREEN_COPY.training;
}

function returnToMenu() {
  resetCombat();
  state.screen = 'menu';
  state.menu.focusedKey = null;
  state.menu.dwellKey = null;
  state.menu.progress = 0;
  state.controlLabel = '主菜单';
  setBadge(dom.handStatus, state.handPresent ? '指向以选择' : '等待玩家入镜', state.handPresent ? 'live' : 'pending');
  dom.cameraHint.textContent = SCREEN_COPY.menu;
}

function setBadge(element, text, type) {
  element.textContent = text;
  element.className = `pill ${type}`;
}

function syncStats() {
  dom.score.textContent = String(state.combat.score);
  dom.miss.textContent = `${state.combat.misses} / ${MAX_MISSES}`;
  dom.control.textContent = state.controlLabel;
  dom.fps.textContent = `${Math.round(state.fps)} FPS`;
}

function clearPendingSpell() {
  state.combat.pendingSpell = createPendingSpell();
}

function clearTrainingPendingSpell() {
  state.training.pendingSpell = createPendingSpell();
}

function stabilizeGesture(rawGesture) {
  const filter = state.gestureFilter;

  if (filter.candidateKey !== rawGesture.key) {
    filter.candidateKey = rawGesture.key;
    filter.candidateLabel = rawGesture.label;
    filter.frames = 1;
  } else {
    filter.frames += 1;
  }

  if (filter.frames >= GESTURE_STABILITY_FRAMES) {
    filter.stableKey = rawGesture.key;
    filter.stableLabel = rawGesture.label;
  }

  return {
    key: filter.stableKey,
    label: filter.stableLabel,
    pointing: filter.stableKey === 'point'
  };
}

function classifyGesture(landmarks) {
  if (!landmarks) {
    return { key: 'none', label: '未检测到手', pointing: false };
  }

  const indexExtended = isFingerExtended(landmarks, 8, 6, 5);
  const middleExtended = isFingerExtended(landmarks, 12, 10, 9);
  const ringExtended = isFingerExtended(landmarks, 16, 14, 13);
  const pinkyExtended = isFingerExtended(landmarks, 20, 18, 17);
  const spread = distance(landmarks[8], landmarks[20]);
  const thumbSpread = distance(landmarks[4], landmarks[5]);
  const fingersUp = [indexExtended, middleExtended, ringExtended, pinkyExtended].filter(Boolean).length;

  if (!indexExtended && !middleExtended && !ringExtended && !pinkyExtended) {
    return { key: 'fist', label: '握拳', pointing: false };
  }

  if (indexExtended && middleExtended && !ringExtended && !pinkyExtended && spread > 0.12) {
    return { key: 'v', label: 'V 手势', pointing: false };
  }

  if (indexExtended && !middleExtended && !ringExtended && !pinkyExtended) {
    return { key: 'point', label: '指向', pointing: true };
  }

  if (fingersUp >= 4 && thumbSpread > 0.09) {
    return { key: 'openPalm', label: '张掌', pointing: false };
  }

  return { key: 'unknown', label: '识别中', pointing: false };
}

function isFingerExtended(landmarks, tipIndex, pipIndex, mcpIndex) {
  const tip = landmarks[tipIndex];
  const pip = landmarks[pipIndex];
  const mcp = landmarks[mcpIndex];
  return tip.y < pip.y - 0.015 && pip.y < mcp.y - 0.005;
}

function updateHandFromResults(results) {
  const { width, height } = dom.overlay;
  const context = dom.overlayContext;
  context.save();
  context.clearRect(0, 0, width, height);

  if (results.multiHandLandmarks?.length) {
    const landmarks = results.multiHandLandmarks[0];
    const rawGesture = classifyGesture(landmarks);
    const gesture = stabilizeGesture(rawGesture);
    const pointerTip = landmarks[8];
    const targetX = clamp((1 - pointerTip.x) * GAME_WIDTH, CURSOR_EDGE_PADDING, GAME_WIDTH - CURSOR_EDGE_PADDING);
    const targetY = clamp(pointerTip.y * GAME_HEIGHT, CURSOR_EDGE_PADDING, GAME_HEIGHT - CURSOR_EDGE_PADDING);

    state.handPresent = true;
    state.handX = 1 - pointerTip.x;
    state.handY = pointerTip.y;
    state.recognizedGesture = gesture.key;
    state.gestureLabel = gesture.label;
    state.cursor.active = gesture.pointing;
    state.cursor.x = lerp(state.cursor.x, targetX, 0.34);
    state.cursor.y = lerp(state.cursor.y, targetY, 0.34);

    for (const handLandmarks of results.multiHandLandmarks) {
      drawConnectors(context, handLandmarks, HAND_CONNECTIONS, {
        color: '#75d6ff',
        lineWidth: 4
      });
      drawLandmarks(context, handLandmarks, {
        color: '#4de0b7',
        lineWidth: 1,
        radius: 3
      });
    }

    drawTrackingHud(context);

    if (state.screen === 'playing') {
      updateCombatInput(performance.now());
    } else if (state.screen === 'training') {
      if (gesture.pointing) {
        state.training.lastCastSpellKey = null;
        clearTrainingPendingSpell();
        updateMenuInput(performance.now());
      } else {
        updateTrainingInput(performance.now());
      }
    } else {
      updateMenuInput(performance.now());
    }
  } else {
    handleHandLost();
  }

  context.restore();
}

function drawTrackingHud(context) {
  if (state.cursor.active) {
    context.beginPath();
    context.strokeStyle = 'rgba(255, 215, 106, 0.92)';
    context.lineWidth = 4;
    context.arc(
      (1 - state.handX) * dom.overlay.width,
      state.handY * dom.overlay.height,
      20,
      0,
      Math.PI * 2
    );
    context.stroke();
  }

  context.fillStyle = 'rgba(4, 10, 19, 0.76)';
  context.fillRect(16, 16, 164, 42);
  context.fillStyle = '#edf4ff';
  context.font = '600 16px "Noto Sans SC", sans-serif';
  context.fillText(`当前手势：${state.gestureLabel}`, 26, 42);
}

function handleHandLost() {
  const wasPlaying = state.screen === 'playing';
  state.handPresent = false;
  state.recognizedGesture = 'none';
  state.gestureLabel = '未检测到手';
  state.gestureFilter.candidateKey = 'none';
  state.gestureFilter.candidateLabel = '未检测到手';
  state.gestureFilter.stableKey = 'none';
  state.gestureFilter.stableLabel = '未检测到手';
  state.gestureFilter.frames = 0;
  state.cursor.active = false;
  clearPendingSpell();
  state.combat.lastCastSpellKey = null;
  clearTrainingPendingSpell();
  state.training.lastCastSpellKey = null;
  state.menu.dwellKey = null;
  state.menu.focusedKey = null;
  state.menu.backStartedAt = 0;
  state.menu.progress = 0;

  if (!state.cameraReady) {
    return;
  }

  if (state.screen === 'results') {
    setBadge(dom.handStatus, '结界失守', 'error');
    dom.cameraHint.textContent = SCREEN_COPY.results;
  } else if (state.screen === 'training') {
    setBadge(dom.handStatus, '训练场待命', 'pending');
    state.controlLabel = '训练场';
    dom.cameraHint.textContent = SCREEN_COPY.training;
  } else if (wasPlaying) {
    setBadge(dom.handStatus, '等待下一次结印', 'pending');
    state.controlLabel = '待命';
    dom.cameraHint.textContent = SCREEN_COPY.playing;
  } else {
    setBadge(dom.handStatus, '等待玩家入镜', 'pending');
    state.controlLabel = state.screen === 'settings' ? '设置中' : '主菜单';
    dom.cameraHint.textContent = SCREEN_COPY[state.screen];
  }
}

function updateMenuInput(now) {
  if (state.recognizedGesture === 'openPalm' && state.screen !== 'menu') {
    if (!state.menu.backStartedAt) {
      state.menu.backStartedAt = now;
    }

    const backProgress = clamp((now - state.menu.backStartedAt) / MENU_BACK_HOLD_MS, 0, 1);
    state.controlLabel = `返回主菜单 ${Math.round(backProgress * 100)}%`;
    setBadge(dom.handStatus, '张掌返回中', 'pending');
    dom.cameraHint.textContent = '继续保持张掌即可返回主菜单。';

    if (backProgress >= 1) {
      returnToMenu();
    }
    return;
  }

  state.menu.backStartedAt = 0;

  if (!state.cursor.active) {
    state.menu.focusedKey = null;
    state.menu.dwellKey = null;
    state.menu.progress = 0;
    state.controlLabel = state.screen === 'settings' ? '设置中' : state.screen === 'tutorial' ? '教程页' : '主菜单';
    setBadge(dom.handStatus, `当前：${state.gestureLabel}`, 'live');
    dom.cameraHint.textContent = SCREEN_COPY[state.screen];
    return;
  }

  const focused = getFocusedRegion();

  if (!focused) {
    state.menu.focusedKey = null;
    state.menu.dwellKey = null;
    state.menu.progress = 0;
    state.controlLabel = '指向可交互项';
    setBadge(dom.handStatus, '移动焦点中', 'live');
    dom.cameraHint.textContent = SCREEN_COPY[state.screen];
    return;
  }

  state.menu.focusedKey = focused.key;

  if (state.menu.dwellKey !== focused.key) {
    state.menu.dwellKey = focused.key;
    state.menu.startedAt = now;
    state.menu.progress = 0;
  } else {
    state.menu.progress = clamp((now - state.menu.startedAt) / MENU_DWELL_MS, 0, 1);
  }

  const progressPercent = Math.round(state.menu.progress * 100);
  state.controlLabel = `${focused.label} ${progressPercent}%`;
  setBadge(dom.handStatus, `指向：${focused.label}`, 'pending');
  dom.cameraHint.textContent = `保持指向即可确认“${focused.label}”。`;

  if (state.menu.progress >= 1) {
    activateRegion(focused.key);
    state.menu.dwellKey = null;
    state.menu.progress = 0;
  }
}

function getFocusedRegion() {
  return state.menu.regions.find((region) => (
    state.cursor.x >= region.x
    && state.cursor.x <= region.x + region.width
    && state.cursor.y >= region.y
    && state.cursor.y <= region.y + region.height
  ));
}

function activateRegion(key) {
  if (state.screen === 'menu') {
    if (key === 'start') {
      startRun();
      return;
    }

    if (key === 'training') {
      startTraining();
      return;
    }

    if (key === 'settings') {
      state.screen = 'settings';
      state.controlLabel = '设置中';
      dom.cameraHint.textContent = SCREEN_COPY.settings;
      return;
    }

    if (key === 'tutorial') {
      state.screen = 'tutorial';
      state.controlLabel = '教程页';
      dom.cameraHint.textContent = SCREEN_COPY.tutorial;
      return;
    }
  }

  if (state.screen === 'settings') {
    if (key === 'confirm') {
      cycleSetting('confirmMs', SETTINGS_CONFIG.confirm);
      return;
    }

    if (key === 'difficulty') {
      cycleSetting('difficulty', SETTINGS_CONFIG.difficulty);
      return;
    }

    if (key === 'back') {
      returnToMenu();
    }
    return;
  }

  if (state.screen === 'tutorial') {
    if (key === 'play') {
      startRun();
      return;
    }

    if (key === 'training') {
      startTraining();
      return;
    }

    if (key === 'back') {
      returnToMenu();
    }
    return;
  }

  if (state.screen === 'results') {
    if (key === 'restart') {
      startRun();
      return;
    }

    if (key === 'menu') {
      returnToMenu();
    }
  }

  if (state.screen === 'training') {
    if (key === 'pointer-check') {
      state.training.pointerChecks += 1;
      state.controlLabel = `指向确认 ${state.training.pointerChecks}`;
      setBadge(dom.handStatus, '指向练习完成', 'live');
      dom.cameraHint.textContent = '很好，继续练习指向、停留确认，或切换到结印训练。';
      return;
    }

    if (key === 'reset-training') {
      resetTraining();
      return;
    }

    if (key === 'menu') {
      returnToMenu();
    }
  }
}

function cycleSetting(field, values) {
  const current = state.settings[field];
  const currentIndex = values.indexOf(current);
  const nextIndex = (currentIndex + 1) % values.length;
  state.settings[field] = values[nextIndex];
  state.controlLabel = `${field === 'confirmMs' ? '确认时长' : '难度'}：${values[nextIndex]}`;
}

function updateCombatInput(now) {
  const combat = state.combat;
  const spellKey = mapGestureToSpell(state.recognizedGesture);

  if (!spellKey) {
    combat.lastCastSpellKey = null;
    clearPendingSpell();
    state.controlLabel = `当前手势：${state.gestureLabel}`;
    setBadge(dom.handStatus, `识别：${state.gestureLabel}`, 'live');
    dom.cameraHint.textContent = SCREEN_COPY.playing;
    return;
  }

  if (combat.lastCastSpellKey === spellKey) {
    clearPendingSpell();
    setBadge(dom.handStatus, `${SPELLS[spellKey].label}已就绪`, 'live');
    dom.cameraHint.textContent = '保持当前手势不会重复释放；直接切换到另一种手势即可继续施法。';
    state.controlLabel = `${SPELLS[spellKey].label}保持中`;
    return;
  }

  if (combat.pendingSpell.key !== spellKey) {
    combat.pendingSpell = {
      key: spellKey,
      startedAt: now,
      progress: 0
    };
  } else {
    combat.pendingSpell.progress = clamp((now - combat.pendingSpell.startedAt) / state.settings.confirmMs, 0, 1);
  }

  const spell = SPELLS[spellKey];
  const progressPercent = Math.round(combat.pendingSpell.progress * 100);
  state.controlLabel = `${spell.label} ${progressPercent}%`;
  setBadge(dom.handStatus, `结印中：${spell.label}`, 'pending');
  dom.cameraHint.textContent = spell.hint;

  if (combat.pendingSpell.progress >= 1) {
    castSpell(spellKey);
    combat.lastCastSpellKey = spellKey;
    clearPendingSpell();
  }
}

function updateTrainingInput(now) {
  const training = state.training;
  const spellKey = mapGestureToSpell(state.recognizedGesture);

  if (!spellKey) {
    training.lastCastSpellKey = null;
    clearTrainingPendingSpell();
    state.controlLabel = `训练识别：${state.gestureLabel}`;
    setBadge(dom.handStatus, `训练中：${state.gestureLabel}`, 'live');
    dom.cameraHint.textContent = SCREEN_COPY.training;
    return;
  }

  if (training.lastCastSpellKey === spellKey) {
    clearTrainingPendingSpell();
    setBadge(dom.handStatus, `${SPELLS[spellKey].label}已记录`, 'live');
    dom.cameraHint.textContent = '同一手势会被视为持续保持；直接切到另一种手势即可继续训练。';
    state.controlLabel = `${SPELLS[spellKey].label}保持中`;
    return;
  }

  if (training.pendingSpell.key !== spellKey) {
    state.training.pendingSpell = {
      key: spellKey,
      startedAt: now,
      progress: 0
    };
  } else {
    training.pendingSpell.progress = clamp((now - training.pendingSpell.startedAt) / state.settings.confirmMs, 0, 1);
  }

  const spell = SPELLS[spellKey];
  const progressPercent = Math.round(training.pendingSpell.progress * 100);
  state.controlLabel = `训练${spell.label} ${progressPercent}%`;
  setBadge(dom.handStatus, `训练结印：${spell.label}`, 'pending');
  dom.cameraHint.textContent = `保持${spell.gesture}，完成一次稳定结印训练。`;

  if (training.pendingSpell.progress >= 1) {
    triggerTrainingSpell(spellKey, now);
    training.lastCastSpellKey = spellKey;
    clearTrainingPendingSpell();
  }
}

function triggerTrainingSpell(spellKey, now) {
  const training = state.training;
  const spell = SPELLS[spellKey];
  training.casts += 1;
  training.streak += 1;
  training.totals[spellKey] += 1;
  training.lastSpellKey = spellKey;
  training.lastSpellAt = now;
  training.runeBursts.push({ spellKey, life: 0.72 });

  const centerX = GAME_WIDTH / 2;
  const centerY = BASELINE_Y - 110;
  createTrainingImpact(centerX, centerY, spell.color, spellKey === 'shield' ? 140 : 98, 0.42);
  createTrainingImpact(centerX, centerY, '#ffffff', spellKey === 'shield' ? 46 : 36, 0.22);

  state.controlLabel = `${spell.label}训练成功`;
  setBadge(dom.handStatus, `${spell.label}训练完成`, 'live');
  dom.cameraHint.textContent = SCREEN_COPY.training;
}

function mapGestureToSpell(gestureKey) {
  if (gestureKey === 'fist') {
    return 'fire';
  }

  if (gestureKey === 'v') {
    return 'ice';
  }

  if (gestureKey === 'openPalm') {
    return 'shield';
  }

  return null;
}

function castSpell(spellKey) {
  const combat = state.combat;
  const spell = SPELLS[spellKey];
  const now = performance.now();
  let hitCount = 0;

  combat.casts += 1;
  combat.runeBursts.push({
    spellKey,
    life: 0.6
  });

  if (spellKey === 'fire') {
    const target = getFrontMostEnemy();
    if (target) {
      hitCount += damageEnemy(target, 1, spell.color, target.radius * 1.35);
    }
  }

  if (spellKey === 'ice') {
    const target = getFastestThreat() || getFrontMostEnemy();
    if (target) {
      target.frozenUntil = now + 2400;
      createImpact(target.x, target.y, '#8be9ff', target.radius * 1.1, 0.28);
      if (target.type === 'swift') {
        hitCount += damageEnemy(target, 1, '#8be9ff', target.radius * 1.42);
      }
    }
  }

  if (spellKey === 'shield') {
    combat.shield.activeUntil = now + 3200;
    combat.shield.charges = 1;
    createImpact(GAME_WIDTH / 2, BASELINE_Y - 26, spell.color, 120, 0.32);
  }

  if (hitCount > 0) {
    combat.hits += hitCount;
    combat.score += hitCount;
    combat.combo += hitCount;
    combat.maxCombo = Math.max(combat.maxCombo, combat.combo);
  } else if (spellKey !== 'shield') {
    combat.combo = 0;
  }

  state.controlLabel = `${spell.label}已释放`;
  setBadge(dom.handStatus, `${spell.label}生效`, 'live');
  dom.cameraHint.textContent = SCREEN_COPY.playing;
}

function getFrontMostEnemy() {
  const enemies = [...state.combat.enemies];
  enemies.sort((left, right) => right.y - left.y);
  return enemies[0] ?? null;
}

function getFastestThreat() {
  const threats = state.combat.enemies.filter((enemy) => enemy.type === 'swift');
  threats.sort((left, right) => right.y - left.y);
  return threats[0] ?? null;
}

function damageEnemy(enemy, amount, color, radius) {
  enemy.hp -= amount;
  createImpact(enemy.x, enemy.y, color, radius);

  if (enemy.hp <= 0) {
    const index = state.combat.enemies.indexOf(enemy);
    if (index >= 0) {
      state.combat.enemies.splice(index, 1);
    }
    return 1;
  }

  return 0;
}

function createImpact(x, y, color, radius, life = 0.38) {
  state.combat.flashes.push({ x, y, color, radius, life });
}

function createTrainingImpact(x, y, color, radius, life = 0.38) {
  state.training.flashes.push({ x, y, color, radius, life });
}

function updateGame(deltaSeconds) {
  updateTransientEffects(deltaSeconds);

  if (state.screen !== 'playing') {
    return;
  }

  const combat = state.combat;
  const now = performance.now();
  combat.elapsed += deltaSeconds;
  combat.spawnTimer -= deltaSeconds;

  if (combat.spawnTimer <= 0) {
    spawnEnemy();
    combat.spawnTimer = getSpawnInterval();
  }

  for (let index = combat.enemies.length - 1; index >= 0; index -= 1) {
    const enemy = combat.enemies[index];
    const frozen = enemy.frozenUntil > now;
    enemy.y += enemy.speed * deltaSeconds * (frozen ? 0.28 : 1);
    enemy.pulse += deltaSeconds * (frozen ? 1.2 : 4.2);

    if (enemy.y + enemy.radius >= BASELINE_Y) {
      if (combat.shield.activeUntil > now && combat.shield.charges > 0) {
        combat.shield.charges -= 1;
        combat.shield.activeUntil = now + 0.18;
        createImpact(enemy.x, BASELINE_Y - 12, '#ffd76a', enemy.radius * 1.6);
        combat.enemies.splice(index, 1);
        continue;
      }

      combat.enemies.splice(index, 1);
      combat.combo = 0;
      combat.misses += enemy.damage;
      createImpact(enemy.x, BASELINE_Y - 8, '#ff6b7d', enemy.radius * 1.52, 0.46);

      if (SANDBOX_MODE) {
        setBadge(dom.handStatus, '测试模式运行中', 'live');
        state.controlLabel = `已穿线 ${combat.misses}`;
        dom.cameraHint.textContent = SCREEN_COPY.playing;
        continue;
      }

      if (combat.misses >= MAX_MISSES) {
        combat.gameOver = true;
        state.screen = 'results';
        setBadge(dom.handStatus, '结界失守', 'error');
        dom.cameraHint.textContent = SCREEN_COPY.results;
        state.controlLabel = '战斗结束';
        break;
      }
    }
  }
}

function getSpawnInterval() {
  const difficultyOffset = state.settings.difficulty === '轻松'
    ? 0.16
    : state.settings.difficulty === '紧张'
      ? -0.14
      : 0;
  return Math.max(0.36, 1.02 - state.combat.score * 0.015 + difficultyOffset);
}

function spawnEnemy() {
  const laneIndex = Math.floor(Math.random() * LANE_COUNT);
  const roll = Math.random();
  const typeKey = laneIndex === 0
    ? (roll < 0.56 ? 'swift' : roll < 0.86 ? 'orb' : 'heavy')
    : laneIndex === 1
      ? (roll < 0.34 ? 'heavy' : roll < 0.86 ? 'orb' : 'swift')
      : (roll < 0.62 ? 'orb' : roll < 0.85 ? 'swift' : 'heavy');
  const profile = ENEMY_TYPES[typeKey];

  state.combat.enemies.push({
    id: `${typeKey}-${Math.random().toString(36).slice(2, 9)}`,
    laneIndex,
    type: typeKey,
    x: LANE_CENTERS[laneIndex],
    y: -profile.radius - Math.random() * 40,
    radius: profile.radius,
    speed: profile.speed + state.combat.score * 1.4,
    damage: profile.damage,
    hp: profile.hp,
    pulse: Math.random() * Math.PI * 2,
    frozenUntil: 0
  });
}

function updateTransientEffects(deltaSeconds) {
  for (let index = state.combat.flashes.length - 1; index >= 0; index -= 1) {
    const flash = state.combat.flashes[index];
    flash.life -= deltaSeconds;
    flash.radius += deltaSeconds * 120;
    if (flash.life <= 0) {
      state.combat.flashes.splice(index, 1);
    }
  }

  for (let index = state.combat.runeBursts.length - 1; index >= 0; index -= 1) {
    const burst = state.combat.runeBursts[index];
    burst.life -= deltaSeconds;
    if (burst.life <= 0) {
      state.combat.runeBursts.splice(index, 1);
    }
  }

  for (let index = state.training.flashes.length - 1; index >= 0; index -= 1) {
    const flash = state.training.flashes[index];
    flash.life -= deltaSeconds;
    flash.radius += deltaSeconds * 90;
    if (flash.life <= 0) {
      state.training.flashes.splice(index, 1);
    }
  }

  for (let index = state.training.runeBursts.length - 1; index >= 0; index -= 1) {
    const burst = state.training.runeBursts[index];
    burst.life -= deltaSeconds;
    if (burst.life <= 0) {
      state.training.runeBursts.splice(index, 1);
    }
  }
}

function drawBackground(context) {
  const gradient = context.createLinearGradient(0, 0, 0, GAME_HEIGHT);
  gradient.addColorStop(0, '#050b18');
  gradient.addColorStop(0.55, '#0a1730');
  gradient.addColorStop(1, '#120d22');
  context.fillStyle = gradient;
  context.fillRect(0, 0, GAME_WIDTH, GAME_HEIGHT);

  for (let laneIndex = 0; laneIndex < LANE_COUNT; laneIndex += 1) {
    const x = laneIndex * LANE_WIDTH;
    context.fillStyle = laneIndex % 2 === 0 ? 'rgba(255,255,255,0.016)' : 'rgba(255,255,255,0.03)';
    context.fillRect(x, 0, LANE_WIDTH, GAME_HEIGHT);

    context.strokeStyle = 'rgba(141, 164, 197, 0.18)';
    context.lineWidth = 2;
    context.beginPath();
    context.moveTo(x, 0);
    context.lineTo(x, GAME_HEIGHT);
    context.stroke();
  }

  context.strokeStyle = 'rgba(141, 164, 197, 0.18)';
  context.beginPath();
  context.moveTo(GAME_WIDTH, 0);
  context.lineTo(GAME_WIDTH, GAME_HEIGHT);
  context.stroke();

  context.fillStyle = 'rgba(255, 255, 255, 0.08)';
  context.fillRect(0, BASELINE_Y, GAME_WIDTH, 8);
  context.fillStyle = 'rgba(117, 214, 255, 0.18)';
  context.fillRect(0, BASELINE_Y + 8, GAME_WIDTH, GAME_HEIGHT - BASELINE_Y - 8);
}

function drawRunes(context) {
  for (const burst of state.combat.runeBursts) {
    const spell = SPELLS[burst.spellKey];
    const alpha = burst.life * 0.35;
    context.fillStyle = hexToRgba(spell.color, alpha);
    context.beginPath();
    context.arc(GAME_WIDTH / 2, BASELINE_Y - 38, 120 + (1 - burst.life) * 28, 0, Math.PI * 2);
    context.fill();
  }
}

function drawTrainingRunes(context) {
  for (const burst of state.training.runeBursts) {
    const spell = SPELLS[burst.spellKey];
    const alpha = burst.life * 0.38;
    context.fillStyle = hexToRgba(spell.color, alpha);
    context.beginPath();
    context.arc(GAME_WIDTH / 2, BASELINE_Y - 120, 150 + (1 - burst.life) * 30, 0, Math.PI * 2);
    context.fill();
  }
}

function drawShield(context) {
  const active = state.combat.shield.activeUntil > performance.now() && state.combat.shield.charges > 0;
  const alpha = active ? 0.42 + Math.sin(performance.now() / 160) * 0.08 : 0.07;
  context.fillStyle = `rgba(255, 215, 106, ${alpha})`;
  context.fillRect(24, BASELINE_Y - 52, GAME_WIDTH - 48, 42);
  context.strokeStyle = `rgba(255, 231, 158, ${Math.max(alpha, 0.16)})`;
  context.lineWidth = 3;
  context.strokeRect(24, BASELINE_Y - 52, GAME_WIDTH - 48, 42);
}

function drawEnemies(context) {
  for (const enemy of state.combat.enemies) {
    const profile = ENEMY_TYPES[enemy.type];
    const radius = enemy.radius + Math.sin(enemy.pulse) * 2.2;
    const frozen = enemy.frozenUntil > performance.now();
    const coreColor = frozen ? '#d8f8ff' : profile.color;
    const enemyGradient = context.createRadialGradient(enemy.x - 4, enemy.y - 6, 3, enemy.x, enemy.y, radius);
    enemyGradient.addColorStop(0, '#ffffff');
    enemyGradient.addColorStop(0.55, coreColor);
    enemyGradient.addColorStop(1, frozen ? '#6ed9ff' : '#1e3762');

    context.beginPath();
    context.fillStyle = enemyGradient;
    context.arc(enemy.x, enemy.y, radius, 0, Math.PI * 2);
    context.fill();

    if (enemy.type === 'heavy') {
      context.strokeStyle = 'rgba(255, 205, 148, 0.76)';
      context.lineWidth = 3;
      context.beginPath();
      context.arc(enemy.x, enemy.y, radius + 5, 0, Math.PI * 2);
      context.stroke();
    }

    if (enemy.type === 'swift') {
      context.strokeStyle = 'rgba(157, 255, 207, 0.68)';
      context.lineWidth = 2;
      context.beginPath();
      context.moveTo(enemy.x - radius - 12, enemy.y);
      context.lineTo(enemy.x - radius + 2, enemy.y);
      context.stroke();
    }

    if (enemy.hp > 1) {
      context.fillStyle = 'rgba(255,255,255,0.7)';
      context.font = '700 14px "Noto Sans SC", sans-serif';
      context.textAlign = 'center';
      context.fillText(String(enemy.hp), enemy.x, enemy.y + 5);
      context.textAlign = 'start';
    }
  }
}

function drawFlashes(context) {
  for (const flash of state.combat.flashes) {
    context.beginPath();
    context.fillStyle = hexToRgba(flash.color, flash.life * 1.8);
    context.arc(flash.x, flash.y, flash.radius, 0, Math.PI * 2);
    context.fill();
  }
}

function drawTrainingFlashes(context) {
  for (const flash of state.training.flashes) {
    context.beginPath();
    context.fillStyle = hexToRgba(flash.color, flash.life * 1.8);
    context.arc(flash.x, flash.y, flash.radius, 0, Math.PI * 2);
    context.fill();
  }
}

function drawHud(context) {
  context.fillStyle = '#edf4ff';
  context.font = '700 24px "Noto Sans SC", sans-serif';
  context.fillText(`Score ${state.combat.score}`, 22, 38);
  context.fillText(
    SANDBOX_MODE ? `Breach ${state.combat.misses}` : `Ward ${Math.max(MAX_MISSES - state.combat.misses, 0)}`,
    22,
    72
  );
  context.fillText(`Combo ${state.combat.combo}`, 22, 106);

  context.textAlign = 'right';
  context.fillStyle = '#8da4c5';
  context.font = '600 16px "Noto Sans SC", sans-serif';
  context.fillText(`当前手势 ${state.gestureLabel}`, GAME_WIDTH - 22, 34);
  context.fillText('握拳=火焰  V=冰霜  张掌=护盾', GAME_WIDTH - 22, 62);
  context.fillText(`命中 ${state.combat.hits} / 施法 ${state.combat.casts}`, GAME_WIDTH - 22, 90);
  context.textAlign = 'start';
}

function drawCastingIndicator(context) {
  if (state.screen !== 'playing' || !state.combat.pendingSpell.key) {
    return;
  }

  const spell = SPELLS[state.combat.pendingSpell.key];
  const ringRadius = 56 + state.combat.pendingSpell.progress * 18;
  context.beginPath();
  context.strokeStyle = hexToRgba(spell.color, 0.9);
  context.lineWidth = 8;
  context.arc(GAME_WIDTH / 2, BASELINE_Y - 132, ringRadius, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * state.combat.pendingSpell.progress);
  context.stroke();

  context.fillStyle = '#edf4ff';
  context.textAlign = 'center';
  context.font = '700 20px "Noto Sans SC", sans-serif';
  context.fillText(spell.label, GAME_WIDTH / 2, BASELINE_Y - 138);
  context.font = '400 15px "Noto Sans SC", sans-serif';
  context.fillStyle = '#8da4c5';
  context.fillText(`保持${spell.gesture}完成结印，切到其他手势可直接续接`, GAME_WIDTH / 2, BASELINE_Y - 112);
  context.textAlign = 'start';
}

function drawTrainingIndicator(context) {
  if (state.screen !== 'training') {
    return;
  }

  const pending = state.training.pendingSpell;
  const currentKey = pending.key || state.training.lastSpellKey;

  if (!currentKey) {
    context.fillStyle = 'rgba(4, 10, 19, 0.58)';
    roundRect(context, 246, 188, 468, 180, 26);
    context.fill();
    context.fillStyle = '#edf4ff';
    context.textAlign = 'center';
    context.font = '700 28px "Noto Sans SC", sans-serif';
    context.fillText('纯手势训练场', GAME_WIDTH / 2, 246);
    context.font = '400 18px "Noto Sans SC", sans-serif';
    context.fillStyle = '#8da4c5';
    context.fillText('依次尝试：食指指向、握拳、V 手势、张掌。', GAME_WIDTH / 2, 286);
    context.fillText('同一手势不会连发，直接切到其他手势即可继续训练。', GAME_WIDTH / 2, 318);
    context.textAlign = 'start';
    return;
  }

  const spell = SPELLS[currentKey];
  const progress = pending.key ? pending.progress : 1;
  const ringRadius = 72 + progress * 20;
  context.beginPath();
  context.strokeStyle = hexToRgba(spell.color, 0.94);
  context.lineWidth = 10;
  context.arc(GAME_WIDTH / 2, BASELINE_Y - 120, ringRadius, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * progress);
  context.stroke();

  context.fillStyle = '#edf4ff';
  context.textAlign = 'center';
  context.font = '700 24px "Noto Sans SC", sans-serif';
  context.fillText(spell.label, GAME_WIDTH / 2, BASELINE_Y - 128);
  context.font = '400 16px "Noto Sans SC", sans-serif';
  context.fillStyle = '#8da4c5';
  context.fillText(
    pending.key ? `保持${spell.gesture}完成训练，可直接切下一个手势` : `最近一次完成：${spell.gesture}`,
    GAME_WIDTH / 2,
    BASELINE_Y - 96
  );
  context.textAlign = 'start';
}

function drawInteractiveCard(context, x, y, width, height, title, subtitle, focused, progress = 0) {
  context.save();
  context.fillStyle = focused ? 'rgba(139, 233, 255, 0.18)' : 'rgba(10, 19, 35, 0.82)';
  context.strokeStyle = focused ? 'rgba(255, 215, 106, 0.92)' : 'rgba(141, 164, 197, 0.28)';
  context.lineWidth = focused ? 3 : 2;
  roundRect(context, x, y, width, height, 22);
  context.fill();
  context.stroke();

  context.fillStyle = '#edf4ff';
  context.font = '700 28px "Noto Sans SC", sans-serif';
  context.fillText(title, x + 24, y + 40);
  context.fillStyle = '#8da4c5';
  context.font = '400 16px "Noto Sans SC", sans-serif';
  context.fillText(subtitle, x + 24, y + 72);

  if (focused) {
    context.fillStyle = 'rgba(255, 215, 106, 0.9)';
    context.fillRect(x + 24, y + height - 18, (width - 48) * progress, 6);
    context.strokeStyle = 'rgba(255, 215, 106, 0.92)';
    context.strokeRect(x + 24, y + height - 18, width - 48, 6);
  }

  context.restore();
}

function drawMenuOverlay(context) {
  state.menu.regions = [];
  context.fillStyle = 'rgba(4, 10, 19, 0.65)';
  context.fillRect(0, 0, GAME_WIDTH, GAME_HEIGHT);
  context.fillStyle = '#edf4ff';
  context.textAlign = 'center';
  context.font = '800 40px "Noto Sans SC", sans-serif';
  context.fillText('符印守卫', GAME_WIDTH / 2, 102);
  context.font = '400 18px "Noto Sans SC", sans-serif';
  context.fillStyle = '#8da4c5';
  context.fillText('食指指向移动焦点，停留确认；进入子页面后，张掌可快速返回。', GAME_WIDTH / 2, 138);
  context.fillText('战斗中可直接从握拳切到 V 或张掌，不必先收势。', GAME_WIDTH / 2, 166);
  context.textAlign = 'start';

  const items = [
    { key: 'start', label: '开始守卫', subtitle: '直接进入当前难度的结界战场' },
    { key: 'training', label: '手势训练场', subtitle: '无失败、无压力，持续调试结印输入' },
    { key: 'settings', label: '调整设置', subtitle: `确认 ${state.settings.confirmMs}ms · 难度 ${state.settings.difficulty}` },
    { key: 'tutorial', label: '查看教程', subtitle: '先熟悉指向、停留确认与三种结印' }
  ];

  items.forEach((item, index) => {
    const region = { key: item.key, label: item.label, x: 170, y: 196 + index * 80, width: 620, height: 68 };
    state.menu.regions.push(region);
    drawInteractiveCard(
      context,
      region.x,
      region.y,
      region.width,
      region.height,
      item.label,
      item.subtitle,
      state.menu.focusedKey === item.key,
      state.menu.dwellKey === item.key ? state.menu.progress : 0
    );
  });
}

function drawSettingsOverlay(context) {
  state.menu.regions = [];
  context.fillStyle = 'rgba(4, 10, 19, 0.74)';
  context.fillRect(0, 0, GAME_WIDTH, GAME_HEIGHT);
  context.fillStyle = '#edf4ff';
  context.textAlign = 'center';
  context.font = '800 34px "Noto Sans SC", sans-serif';
  context.fillText('设置', GAME_WIDTH / 2, 104);
  context.font = '400 18px "Noto Sans SC", sans-serif';
  context.fillStyle = '#8da4c5';
  context.fillText('指向条目并停留即可循环切换；张掌或“返回主菜单”都能退出。', GAME_WIDTH / 2, 138);
  context.textAlign = 'start';

  const items = [
    { key: 'confirm', label: '结印确认时长', subtitle: `${state.settings.confirmMs} ms` },
    { key: 'difficulty', label: '敌人节奏', subtitle: state.settings.difficulty },
    { key: 'back', label: '返回主菜单', subtitle: '保存当前设置并回到主菜单' }
  ];

  items.forEach((item, index) => {
    const region = { key: item.key, label: item.label, x: 174, y: 186 + index * 102, width: 612, height: 80 };
    state.menu.regions.push(region);
    drawInteractiveCard(
      context,
      region.x,
      region.y,
      region.width,
      region.height,
      item.label,
      item.subtitle,
      state.menu.focusedKey === item.key,
      state.menu.dwellKey === item.key ? state.menu.progress : 0
    );
  });
}

function drawTutorialOverlay(context) {
  state.menu.regions = [];
  context.fillStyle = 'rgba(4, 10, 19, 0.76)';
  context.fillRect(0, 0, GAME_WIDTH, GAME_HEIGHT);
  context.fillStyle = '#edf4ff';
  context.textAlign = 'center';
  context.font = '800 34px "Noto Sans SC", sans-serif';
  context.fillText('手势教程', GAME_WIDTH / 2, 98);
  context.textAlign = 'start';

  const cards = [
    '食指指向：移动界面焦点，稳定停留即可确认。',
    '握拳：火焰印，击碎最前方威胁。',
    'V 手势：冰霜印，优先冻结疾行碎影。',
    '张掌：护盾印，为结界提供一次保底防御；在非主菜单页长按可返回。'
  ];

  cards.forEach((text, index) => {
    drawInteractiveCard(context, 138, 132 + index * 72, 684, 56, text, '保持动作稳定，比大幅挥动更重要。', false, 0);
  });

  const actions = [
    { key: 'play', label: '开始守卫', subtitle: '进入战斗，尝试三种结印' },
    { key: 'training', label: '进入训练场', subtitle: '先调通输入，再回到正式战斗' },
    { key: 'back', label: '返回主菜单', subtitle: '继续浏览或调整设置' }
  ];

  actions.forEach((action, index) => {
    const region = { key: action.key, label: action.label, x: 96 + index * 258, y: 430, width: 248, height: 64 };
    state.menu.regions.push(region);
    drawInteractiveCard(
      context,
      region.x,
      region.y,
      region.width,
      region.height,
      action.label,
      action.subtitle,
      state.menu.focusedKey === action.key,
      state.menu.dwellKey === action.key ? state.menu.progress : 0
    );
  });
}

function drawTrainingOverlay(context) {
  state.menu.regions = [];
  context.fillStyle = 'rgba(4, 10, 19, 0.3)';
  context.fillRect(0, 0, GAME_WIDTH, GAME_HEIGHT);

  context.fillStyle = '#edf4ff';
  context.font = '800 34px "Noto Sans SC", sans-serif';
  context.fillText('纯手势训练场', 28, 52);
  context.fillStyle = '#8da4c5';
  context.font = '400 16px "Noto Sans SC", sans-serif';
  context.fillText('没有敌人、没有失败。专注确认：识别是否稳定、停留是否触发、反馈是否清晰。', 28, 80);

  const summaryCards = [
    { x: 28, y: 110, title: '总训练次数', value: String(state.training.casts) },
    { x: 258, y: 110, title: '指向确认', value: String(state.training.pointerChecks) },
    { x: 488, y: 110, title: '火焰 / 冰霜', value: `${state.training.totals.fire} / ${state.training.totals.ice}` },
    { x: 718, y: 110, title: '护盾印', value: String(state.training.totals.shield) }
  ];

  summaryCards.forEach((card) => {
    drawInteractiveCard(context, card.x, card.y, 214, 88, card.title, card.value, false, 0);
  });

  drawTrainingIndicator(context);
  drawTrainingFlashes(context);

  const spellCards = [
    { spell: SPELLS.fire, x: 120, y: 392 },
    { spell: SPELLS.ice, x: 368, y: 392 },
    { spell: SPELLS.shield, x: 616, y: 392 }
  ];

  spellCards.forEach(({ spell, x, y }) => {
    context.save();
    context.fillStyle = hexToRgba(spell.color, 0.16);
    context.strokeStyle = hexToRgba(spell.color, 0.84);
    context.lineWidth = state.training.lastSpellKey === spell.key ? 3 : 2;
    roundRect(context, x, y, 224, 90, 22);
    context.fill();
    context.stroke();
    context.fillStyle = '#edf4ff';
    context.font = '700 22px "Noto Sans SC", sans-serif';
    context.fillText(spell.label, x + 18, y + 34);
    context.fillStyle = '#8da4c5';
    context.font = '400 15px "Noto Sans SC", sans-serif';
    context.fillText(`手势：${spell.gesture}`, x + 18, y + 60);
    context.fillText(`累计：${state.training.totals[spell.key]}`, x + 18, y + 80);
    context.restore();
  });

  const actions = [
    { key: 'pointer-check', label: '指向确认练习', subtitle: '对着这里停留，用来单独练习 point + dwell' },
    { key: 'reset-training', label: '重置训练计数', subtitle: '清空训练统计，继续测试' },
    { key: 'menu', label: '返回主菜单', subtitle: '退出训练场' }
  ];

  actions.forEach((action, index) => {
    const region = { key: action.key, label: action.label, x: 34 + index * 298, y: 28, width: 264, height: 62 };
    state.menu.regions.push(region);
    drawInteractiveCard(
      context,
      region.x,
      region.y,
      region.width,
      region.height,
      action.label,
      action.subtitle,
      state.menu.focusedKey === action.key,
      state.menu.dwellKey === action.key ? state.menu.progress : 0
    );
  });
}

function drawResultsOverlay(context) {
  state.menu.regions = [];
  context.fillStyle = 'rgba(4, 10, 19, 0.82)';
  context.fillRect(0, 0, GAME_WIDTH, GAME_HEIGHT);
  context.textAlign = 'center';
  context.fillStyle = '#edf4ff';
  context.font = '800 40px "Noto Sans SC", sans-serif';
  context.fillText('结界失守', GAME_WIDTH / 2, 118);
  context.font = '500 22px "Noto Sans SC", sans-serif';
  context.fillStyle = '#75d6ff';
  context.fillText(`最终得分 ${state.combat.score}`, GAME_WIDTH / 2, 162);
  context.fillText(`最大连击 ${state.combat.maxCombo}`, GAME_WIDTH / 2, 194);
  context.fillText(`命中率 ${getHitRate()}%`, GAME_WIDTH / 2, 226);
  context.textAlign = 'start';

  const actions = [
    { key: 'restart', label: '再来一局', subtitle: '保留当前设置，立即重新守卫' },
    { key: 'menu', label: '返回主菜单', subtitle: '回到入口继续调整手势设置' }
  ];

  actions.forEach((action, index) => {
    const region = { key: action.key, label: action.label, x: 216 + index * 278, y: 340, width: 248, height: 84 };
    state.menu.regions.push(region);
    drawInteractiveCard(
      context,
      region.x,
      region.y,
      region.width,
      region.height,
      action.label,
      action.subtitle,
      state.menu.focusedKey === action.key,
      state.menu.dwellKey === action.key ? state.menu.progress : 0
    );
  });
}

function drawCursor(context) {
  if (!state.cursor.active || state.screen === 'playing') {
    return;
  }

  context.save();
  context.beginPath();
  context.fillStyle = 'rgba(255, 215, 106, 0.95)';
  context.arc(state.cursor.x, state.cursor.y, 11, 0, Math.PI * 2);
  context.fill();
  context.restore();
}

function drawGame() {
  const context = dom.gameContext;
  context.clearRect(0, 0, GAME_WIDTH, GAME_HEIGHT);
  drawBackground(context);

  if (state.screen === 'playing' || state.screen === 'results') {
    drawRunes(context);
    drawShield(context);
    drawEnemies(context);
    drawFlashes(context);
    drawHud(context);
    drawCastingIndicator(context);
  }

  if (state.screen === 'training') {
    drawTrainingRunes(context);
    drawTrainingOverlay(context);
  }

  if (state.screen === 'menu') {
    drawMenuOverlay(context);
  }

  if (state.screen === 'settings') {
    drawSettingsOverlay(context);
  }

  if (state.screen === 'tutorial') {
    drawTutorialOverlay(context);
  }

  if (state.screen === 'results') {
    drawResultsOverlay(context);
  }

  drawCursor(context);
}

function getHitRate() {
  if (!state.combat.casts) {
    return 0;
  }

  return Math.round((state.combat.hits / state.combat.casts) * 100);
}

function roundRect(context, x, y, width, height, radius) {
  context.beginPath();
  context.moveTo(x + radius, y);
  context.lineTo(x + width - radius, y);
  context.quadraticCurveTo(x + width, y, x + width, y + radius);
  context.lineTo(x + width, y + height - radius);
  context.quadraticCurveTo(x + width, y + height, x + width - radius, y + height);
  context.lineTo(x + radius, y + height);
  context.quadraticCurveTo(x, y + height, x, y + height - radius);
  context.lineTo(x, y + radius);
  context.quadraticCurveTo(x, y, x + radius, y);
  context.closePath();
}

function average(...values) {
  return values.reduce((sum, value) => sum + value, 0) / values.length;
}

function distance(left, right) {
  return Math.hypot(left.x - right.x, left.y - right.y);
}

function lerp(start, end, amount) {
  return start + (end - start) * amount;
}

function clamp(value, min, max) {
  return Math.min(Math.max(value, min), max);
}

function hexToRgba(hex, alpha) {
  const normalized = hex.replace('#', '');
  const bigint = Number.parseInt(normalized, 16);
  const r = (bigint >> 16) & 255;
  const g = (bigint >> 8) & 255;
  const b = bigint & 255;
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

function waitForVideoReady(video) {
  if (video.readyState >= 2 && video.videoWidth && video.videoHeight) {
    return Promise.resolve();
  }

  return new Promise((resolve) => {
    const handleReady = () => {
      if (!video.videoWidth || !video.videoHeight) {
        return;
      }

      video.removeEventListener('loadedmetadata', handleReady);
      video.removeEventListener('canplay', handleReady);
      resolve();
    };

    video.addEventListener('loadedmetadata', handleReady);
    video.addEventListener('canplay', handleReady);
  });
}

async function initializeCamera() {
  if (typeof Hands === 'undefined' || typeof Camera === 'undefined') {
    throw new Error('MediaPipe 资源加载失败。');
  }

  const hands = new Hands({
    locateFile: (file) => `https://cdn.jsdelivr.net/npm/@mediapipe/hands/${file}`
  });

  hands.setOptions({
    maxNumHands: 1,
    modelComplexity: 1,
    minDetectionConfidence: 0.65,
    minTrackingConfidence: 0.55
  });

  hands.onResults(updateHandFromResults);

  const camera = new Camera(dom.video, {
    onFrame: async () => {
      if (dom.video.readyState < 2) {
        return;
      }

      if (dom.video.videoWidth && dom.video.videoHeight) {
        dom.overlay.width = dom.video.videoWidth;
        dom.overlay.height = dom.video.videoHeight;
      }

      await hands.send({ image: dom.video });
    },
    width: 1280,
    height: 720
  });

  await camera.start();
  await waitForVideoReady(dom.video);

  dom.overlay.width = dom.video.videoWidth || 1280;
  dom.overlay.height = dom.video.videoHeight || 720;

  state.cameraReady = true;
  setBadge(dom.cameraStatus, '摄像头已连接', 'live');
  setBadge(dom.handStatus, '等待玩家入镜', 'pending');
  dom.cameraHint.textContent = SCREEN_COPY.menu;
}

function loop(timestamp) {
  if (!state.previousFrameTime) {
    state.previousFrameTime = timestamp;
  }

  const deltaSeconds = Math.min((timestamp - state.previousFrameTime) / 1000, 0.033);
  state.previousFrameTime = timestamp;
  state.fps += ((1 / Math.max(deltaSeconds, 0.001)) - state.fps) * 0.1;

  updateGame(deltaSeconds);
  drawGame();
  syncStats();
  requestAnimationFrame(loop);
}

async function start() {
  returnToMenu();
  requestAnimationFrame(loop);

  try {
    await initializeCamera();
  } catch (error) {
    console.error(error);
    setBadge(dom.cameraStatus, '摄像头失败', 'error');
    setBadge(dom.handStatus, '无法启动施法跟踪', 'error');
    dom.cameraHint.textContent = `启动失败：${error.message}`;
  }
}

start();
