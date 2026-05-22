const REQUIRED_LANDMARK_COUNT = 21;
const STORAGE_KEY = 'spellguard.custom-gesture-library.v1';
const DEFAULT_DURATION_MS = 12000;
const DEFAULT_REQUIRED_SAMPLES = 5;
const RECORD_STABLE_ENTER_FRAMES = 8;
const RECORD_STABLE_EXIT_FRAMES = 8;
const RECORD_MIN_FRAME_COUNT = 12;
const RECORD_MAX_IDLE_GAP_MS = 220;
const FEATURE_COUNT = 21 * 2 + 4;
const DTW_RESAMPLE_POINTS = 32;

const intentLabels = {
  CastFire: '火焰',
  CastIce: '冰霜',
  CastShield: '护盾',
  CustomGesture: '仅验证'
};

const dom = {
  video: document.getElementById('camera-feed'),
  overlay: document.getElementById('tracking-overlay'),
  overlayContext: document.getElementById('tracking-overlay').getContext('2d'),
  trajectoryCanvas: document.getElementById('trajectory-canvas'),
  trajectoryContext: document.getElementById('trajectory-canvas').getContext('2d'),
  startCamera: document.getElementById('start-camera'),
  recordSample: document.getElementById('record-sample'),
  saveTemplate: document.getElementById('save-template'),
  clearSamples: document.getElementById('clear-samples'),
  importJson: document.getElementById('import-json'),
  replayFiles: document.getElementById('replay-files'),
  startReplay: document.getElementById('start-replay'),
  stopReplay: document.getElementById('stop-replay'),
  exportSelected: document.getElementById('export-selected'),
  exportLibrary: document.getElementById('export-library'),
  startValidation: document.getElementById('start-validation'),
  stopValidation: document.getElementById('stop-validation'),
  gestureName: document.getElementById('gesture-name'),
  gestureIntent: document.getElementById('gesture-intent'),
  recordDuration: document.getElementById('record-duration'),
  requiredSamples: document.getElementById('required-samples'),
  cameraStatus: document.getElementById('camera-status'),
  handStatus: document.getElementById('hand-status'),
  cameraHint: document.getElementById('camera-hint'),
  captureStatus: document.getElementById('capture-status'),
  sampleCount: document.getElementById('sample-count'),
  frameCount: document.getElementById('frame-count'),
  matchName: document.getElementById('match-name'),
  matchScore: document.getElementById('match-score'),
  templateList: document.getElementById('template-list'),
  validationLog: document.getElementById('validation-log'),
  replayLog: document.getElementById('replay-log'),
};

const state = {
  cameraReady: false,
  cameraPermissionGranted: false,
  replaying: false,
  replayFiles: [],
  replayIndex: 0,
  replayVideo: null,
  capturing: false,
  validating: false,
  activeTemplateId: null,
  latestHand: null,
  gestureFrames: [],
  currentSampleFrames: [],
  sampleSessionFrames: [],
  sampleStableEnterCount: 0,
  sampleStableExitCount: 0,
  sampleActive: false,
  recordingStartedAt: 0,
  recordingDurationMs: DEFAULT_DURATION_MS,
  validationStartedAt: 0,
  validationBuffer: [],
  templates: loadTemplates(),
  lastMatchName: '无',
  lastMatchScore: '--',
  latestGestureKey: 'none',
  latestGestureLabel: '未检测到手',
  handsReady: false,
  handsError: '',
  lastResultsAt: 0,
  sampleCompleteAt: 0,
};

async function init() {
  bindEvents();
  renderAll();
  await loadCamera();
  startLoop();
}

function bindEvents() {
  dom.startCamera.addEventListener('click', loadCamera);
  dom.recordSample.addEventListener('click', toggleRecording);
  dom.saveTemplate.addEventListener('click', saveTemplateFromSamples);
  dom.clearSamples.addEventListener('click', clearSamples);
  dom.importJson.addEventListener('change', importJsonFiles);
  dom.replayFiles.addEventListener('change', handleReplayFiles);
  dom.startReplay.addEventListener('click', startReplay);
  dom.stopReplay.addEventListener('click', stopReplay);
  dom.exportSelected.addEventListener('click', exportSelectedTemplate);
  dom.exportLibrary.addEventListener('click', exportLibrary);
  dom.startValidation.addEventListener('click', startValidation);
  dom.stopValidation.addEventListener('click', stopValidation);
  dom.gestureIntent.addEventListener('change', updateCaptureStatus);
  dom.requiredSamples.addEventListener('change', updateCaptureStatus);
  dom.recordDuration.addEventListener('change', updateCaptureStatus);
}

async function loadCamera() {
  if (!navigator.mediaDevices?.getUserMedia) {
    setStatus('浏览器不支持摄像头', 'error');
    return;
  }

  try {
    setStatus('正在请求摄像头权限', 'pending');
    const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user' }, audio: false });
    dom.video.srcObject = stream;
    dom.video.autoplay = true;
    dom.video.playsInline = true;
    dom.video.muted = true;
    await dom.video.play();
    state.cameraReady = true;
    state.cameraPermissionGranted = true;
    setStatus('摄像头已启动', 'live');
    dom.cameraHint.textContent = '把手放入画面，先录制样本，再保存模板。';
  } catch (error) {
    console.error(error);
    setStatus('摄像头启动失败', 'error');
    dom.cameraHint.textContent = '请允许浏览器访问摄像头，或检查设备占用情况。';
  }
}

function toggleRecording() {
  if (state.capturing) {
    finishRecordingSession('已手动停止，本组样本可继续保存到模板。');
    return;
  }

  if (!state.cameraReady) {
    updateCaptureStatus('请先启动摄像头。');
    return;
  }

  state.capturing = true;
  state.currentSampleFrames = [];
  state.sampleSessionFrames = [];
  state.sampleStableEnterCount = 0;
  state.sampleStableExitCount = 0;
  state.sampleActive = false;
  state.recordingStartedAt = performance.now();
  state.recordingDurationMs = Number(dom.recordDuration.value) || DEFAULT_DURATION_MS;
  dom.recordSample.textContent = '停止录制';
  updateCaptureStatus('正在等待动作稳定进入...');
}

function saveTemplateFromSamples() {
  if (state.capturing) {
    finishRecordingSession('当前录制已保存到样本列表。');
  }

  if (state.recordedSamples.length === 0) {
    updateCaptureStatus('没有可保存的样本。');
    return;
  }

  const requiredSamples = Number(dom.requiredSamples.value) || DEFAULT_REQUIRED_SAMPLES;
  const gestureId = buildGestureId(dom.gestureName.value);
  const template = buildTemplate({
    gestureId,
    displayName: dom.gestureName.value.trim() || '未命名手势',
    targetIntent: dom.gestureIntent.value,
    requiredSamples,
    samples: state.recordedSamples,
  });

  upsertTemplate(template);
  state.activeTemplateId = template.GestureId;
  state.recordedSamples = [];
  state.currentSampleFrames = [];
  saveTemplates();
  renderAll();
  updateCaptureStatus(`已保存模板：${template.DisplayName}`);
}

function clearSamples() {
  state.currentSampleFrames = [];
  state.recordedSamples = [];
  state.gestureFrames = [];
  state.latestHand = null;
  state.lastMatchName = '无';
  state.lastMatchScore = '--';
  state.capturing = false;
  dom.recordSample.textContent = '录制样本';
  updateCaptureStatus('已清空当前采样。');
  renderAll();
}

function startValidation() {
  if (state.templates.length === 0) {
    updateCaptureStatus('没有模板可验证。');
    return;
  }

  state.validating = true;
  state.validationStartedAt = performance.now();
  dom.validationLog.textContent = '验证中，做出目标动作即可。';
  updateCaptureStatus('验证模式已开启。');
}

function stopValidation() {
  state.validating = false;
  dom.validationLog.textContent = '验证已停止。';
  updateCaptureStatus('验证模式已停止。');
}

function importJsonFiles(event) {
  const files = [...event.target.files];
  if (files.length === 0) {
    return;
  }

  Promise.all(files.map((file) => file.text())).then((texts) => {
    let imported = 0;
    for (const text of texts) {
      try {
        const parsed = JSON.parse(text);
        const templates = normalizeImportedTemplates(parsed);
        for (const template of templates) {
          upsertTemplate(template);
          imported += 1;
        }
      } catch (error) {
        console.warn('Import failed', error);
      }
    }

    saveTemplates();
    renderAll();
    updateCaptureStatus(`已导入 ${imported} 个模板。`);
  });
}

function handleReplayFiles(event) {
  state.replayFiles = [...event.target.files];
  state.replayIndex = 0;
  dom.replayLog.textContent = state.replayFiles.length
    ? `已选择 ${state.replayFiles.length} 个视频，点击开始回放。`
    : '等待选择视频文件。';
}

async function startReplay() {
  if (!state.replayFiles.length) {
    dom.replayLog.textContent = '请先选择至少一个视频文件。';
    return;
  }

  stopCameraTrack();
  state.replaying = true;
  dom.replayLog.textContent = '正在回放中...';
  for (let index = 0; index < state.replayFiles.length && state.replaying; index += 1) {
    state.replayIndex = index;
    const file = state.replayFiles[index];
    dom.replayLog.textContent = `播放 ${index + 1}/${state.replayFiles.length}: ${file.name}`;
    await playVideoFile(file);
  }

  if (state.replaying) {
    dom.replayLog.textContent = '回放完成。';
  }

  state.replaying = false;
}

function stopReplay() {
  state.replaying = false;
  if (state.replayVideo) {
    state.replayVideo.pause();
    state.replayVideo.src = '';
    state.replayVideo = null;
  }
  dom.replayLog.textContent = '回放已停止。';
}

function stopCameraTrack() {
  state.cameraReady = false;
  if (dom.video.srcObject) {
    const tracks = dom.video.srcObject.getTracks?.() || [];
    for (const track of tracks) {
      track.stop();
    }
    dom.video.srcObject = null;
  }
}

async function playVideoFile(file) {
  return new Promise((resolve) => {
    const video = document.createElement('video');
    state.replayVideo = video;
    video.playsInline = true;
    video.muted = true;
    video.src = URL.createObjectURL(file);
    video.onloadedmetadata = async () => {
      await video.play();
      const step = async () => {
        if (!state.replaying) {
          URL.revokeObjectURL(video.src);
          resolve();
          return;
        }

        if (video.ended) {
          URL.revokeObjectURL(video.src);
          resolve();
          return;
        }

        if (!state._hands && window.Hands) {
          await initHands();
        }

        if (state._hands && video.readyState >= 2) {
          state._hands.send({ image: video }).catch((error) => {
            state.handsError = error?.message || String(error);
            dom.replayLog.textContent = `回放识别失败：${state.handsError}`;
          });
        }

        requestAnimationFrame(step);
      };

      requestAnimationFrame(step);
    };
    video.onerror = () => {
      URL.revokeObjectURL(video.src);
      dom.replayLog.textContent = `无法播放视频：${file.name}`;
      resolve();
    };
  });
}

function exportSelectedTemplate() {
  const template = state.templates.find((item) => item.GestureId === state.activeTemplateId) || state.templates[0];
  if (!template) {
    updateCaptureStatus('没有可导出的模板。');
    return;
  }

  downloadJson(templateToUnityFile(template), `${template.GestureId}.json`);
}

function exportLibrary() {
  if (state.templates.length === 0) {
    updateCaptureStatus('没有模板可导出。');
    return;
  }

  const blob = {
    DatasetName: 'custom_gesture_library',
    ExportedAt: new Date().toISOString(),
    Templates: state.templates.map(templateToUnityFile)
  };
  downloadJson(blob, 'custom_gesture_library.json');
}

function updateCaptureStatus(message) {
  if (message) {
    dom.captureStatus.textContent = message;
  }

  const requiredSamples = Number(dom.requiredSamples.value) || DEFAULT_REQUIRED_SAMPLES;
  dom.sampleCount.textContent = `${state.recordedSamples.length} / ${requiredSamples}`;
  dom.frameCount.textContent = String(state.gestureFrames.length);
  dom.matchName.textContent = state.lastMatchName;
  dom.matchScore.textContent = state.lastMatchScore;
  dom.cameraHint.textContent = state.capturing
      ? `录制中，已收集 ${state.recordedSamples.length} 组，保持动作。`
      : state.validating
        ? '验证中，直接做目标手势。'
        : state.cameraReady
          ? `录入、管理、验证、导出都已就位。当前已收集 ${state.recordedSamples.length} 组样本。`
          : '先启动摄像头，再开始录入。';
  refreshHandsIndicator();
}

function renderAll() {
  renderTemplateList();
  renderTrajectoryPreview();
  updateCaptureStatus();
  refreshHandsIndicator();
}

function renderTemplateList() {
  if (state.templates.length === 0) {
    dom.templateList.innerHTML = '<div class="empty-state">还没有模板。先录一个动态手势吧。</div>';
    return;
  }

  dom.templateList.innerHTML = state.templates.map((template) => {
    const selected = template.GestureId === state.activeTemplateId;
    return `
      <button class="template-item ${selected ? 'selected' : ''}" data-template-id="${template.GestureId}">
        <div class="template-topline">
          <strong>${escapeHtml(template.DisplayName)}</strong>
          <span>${escapeHtml(intentLabels[template.TargetIntent] || template.TargetIntent)}</span>
        </div>
        <div class="template-meta">
          <span>${template.Kind === 'StaticPose' ? '静态' : '动态'}</span>
          <span>${template.RequiredHandedness || 'Unknown'}</span>
          <span>${template.Samples.length} samples</span>
        </div>
      </button>`;
  }).join('');

  dom.templateList.querySelectorAll('.template-item').forEach((element) => {
    element.addEventListener('click', () => {
      state.activeTemplateId = element.dataset.templateId;
      const template = state.templates.find((item) => item.GestureId === state.activeTemplateId);
      if (template) {
        dom.gestureName.value = template.DisplayName;
        dom.gestureIntent.value = template.TargetIntent;
      }
      renderAll();
    });
  });
}

function renderTrajectoryPreview() {
  const context = dom.trajectoryContext;
  const { width, height } = dom.trajectoryCanvas;
  context.clearRect(0, 0, width, height);
  context.fillStyle = '#07111f';
  context.fillRect(0, 0, width, height);
  context.strokeStyle = 'rgba(139, 233, 255, 0.3)';
  context.strokeRect(10, 10, width - 20, height - 20);

  const previewFrames = state.currentSampleFrames.length >= 2
    ? state.currentSampleFrames
    : sampleToPreviewFrames(state.recordedSamples.at(-1));

  if (previewFrames.length < 2) {
    context.fillStyle = '#8da4c5';
    context.font = '14px "Noto Sans SC", sans-serif';
    context.fillText('录制一组样本后，这里会显示轨迹预览。', 22, 40);
    return;
  }

  const trajectory = resampleTrajectory(previewFrames.map((frame) => frame.palm));
  const points = normalizePoints(trajectory, width, height);
  context.strokeStyle = '#8be9ff';
  context.lineWidth = 3;
  context.beginPath();
  points.forEach((point, index) => {
    if (index === 0) {
      context.moveTo(point.x, point.y);
    } else {
      context.lineTo(point.x, point.y);
    }
  });
  context.stroke();
}

function startLoop() {
  const tick = async () => {
    if (state.cameraReady && window.Hands && !state._hands) {
      await initHands();
    }

    if (state._hands && dom.video.readyState >= 2) {
      try {
        await state._hands.send({ image: dom.video });
      } catch (error) {
        state.handsError = error?.message || String(error);
        setStatus('手势引擎异常', 'error');
        dom.captureStatus.textContent = `MediaPipe 发送帧失败：${state.handsError}`;
      }
    }

    requestAnimationFrame(tick);
  };

  requestAnimationFrame(tick);
}

async function initHands() {
  if (!window.Hands) {
    state.handsError = 'MediaPipe Hands 未加载';
    setStatus('手势引擎未加载', 'error');
    dom.captureStatus.textContent = 'MediaPipe Hands 资源未加载成功，请检查网络或脚本地址。';
    return;
  }

  const hands = new window.Hands({
    locateFile: (file) => `https://cdn.jsdelivr.net/npm/@mediapipe/hands/${file}`,
  });

  hands.setOptions({
    maxNumHands: 1,
    modelComplexity: 1,
    minDetectionConfidence: 0.5,
    minTrackingConfidence: 0.5,
  });

  hands.onResults(handleResults);
  state._hands = hands;
  state.handsReady = true;
  state.handsError = '';
  setStatus('手势引擎已就绪', 'live');
}

function handleResults(results) {
  state.lastResultsAt = performance.now();
  const context = dom.overlayContext;
  const width = Math.max(1, dom.overlay.clientWidth);
  const height = Math.max(1, dom.overlay.clientHeight);
  dom.overlay.width = width;
  dom.overlay.height = height;
  context.clearRect(0, 0, width, height);

  if (!results.multiHandLandmarks?.length) {
    state.latestHand = null;
    state.latestGestureKey = 'none';
    state.latestGestureLabel = '未检测到手';
    dom.handStatus.textContent = '未检测到手';
    dom.handStatus.className = 'pill pending';
    if (state.capturing) {
      captureFrame(null);
      maybeFinishCapture();
    }
    return;
  }

  const landmarks = results.multiHandLandmarks[0];
  const gesture = classifyGesture(landmarks);
  const palm = buildPalm(landmarks);

  state.latestHand = { landmarks, palm };
  state.latestGestureKey = gesture.key;
  state.latestGestureLabel = gesture.label;
  dom.handStatus.textContent = `当前：${gesture.label}`;
  dom.handStatus.className = 'pill live';

  drawLandmarks(context, landmarks, width, height);
  drawGestureHud(context, gesture.label, width, height);

  captureFrame({ landmarks, palm, gesture });
  updateValidation(gesture);
  maybeFinishCapture();
}

function captureFrame(hand) {
  if (!state.capturing || !hand) {
    return;
  }

  const now = performance.now();
  const time = (now - state.recordingStartedAt) / 1000;
  const frame = {
    time,
    palm: { x: hand.palm.x, y: hand.palm.y },
    landmarks: hand.landmarks.map((point) => ({ x: point.x, y: point.y })),
    staticGesture: hand.gesture.key === 'fist' ? 'Fist' : hand.gesture.key === 'v' ? 'VSign' : hand.gesture.key === 'openPalm' ? 'OpenPalm' : 'OpenPalm'
  };
  state.gestureFrames.push({
    time,
    confidence: 1,
    staticGesture: frame.staticGesture,
    palm: { ...frame.palm },
    landmarks: frame.landmarks.map((point) => ({ ...point }))
  });
  state.sampleSessionFrames.push(frame);
  updateRecordingBoundary(frame, now);
}

function maybeFinishCapture() {
  if (!state.capturing) {
    return;
  }

  const durationMs = Number(dom.recordDuration.value) || DEFAULT_DURATION_MS;
  if (performance.now() - state.recordingStartedAt >= durationMs) {
    finishRecordingSession('本组样本已完成，可继续录下一组。');
  }
}

function updateRecordingBoundary(frame, now) {
  if (!frame) {
    return;
  }

  const previous = state.sampleSessionFrames.at(-2);
  const movement = previous ? distance(frame.palm, previous.palm) : 0;
  const stable = movement < 0.012;
  const activeHasFrames = state.currentSampleFrames.length > 0;

  if (!state.sampleActive) {
    state.sampleStableEnterCount = stable ? state.sampleStableEnterCount + 1 : 0;
    if (state.sampleStableEnterCount >= RECORD_STABLE_ENTER_FRAMES) {
      state.sampleActive = true;
      state.currentSampleFrames = state.sampleSessionFrames.slice(-RECORD_STABLE_ENTER_FRAMES);
      updateSampleProgress();
      dom.captureStatus.textContent = '动作已进入稳定录制区，继续完成手势。';
    }
    return;
  }

  if (activeHasFrames) {
    state.currentSampleFrames.push(frame);
  }

  const gapMs = previous ? now - state.lastResultsAt : 0;
  const endingCandidate = stable || gapMs > RECORD_MAX_IDLE_GAP_MS;
  state.sampleStableExitCount = endingCandidate ? state.sampleStableExitCount + 1 : 0;

  if (state.sampleStableExitCount >= RECORD_STABLE_EXIT_FRAMES && state.currentSampleFrames.length >= RECORD_MIN_FRAME_COUNT) {
    finishRecordingSession('本组样本已完成，可继续录下一组。');
  }
}

function updateValidation(gesture) {
  if (!state.validating || state.templates.length === 0) {
    return;
  }

  const frame = buildRuntimeFrame();
  const templates = state.templates.map(buildRuntimeTemplate);
  const match = resolveBestTemplate(frame, templates);
  state.lastMatchName = match ? match.DisplayName : '无';
  state.lastMatchScore = match ? match.score.toFixed(4) : '--';
  dom.validationLog.textContent = match
    ? `命中：${match.DisplayName} / ${intentLabels[match.TargetIntent] || match.TargetIntent} / 分数 ${match.score.toFixed(4)}`
    : `未命中。当前识别：${gesture.label}`;
}

function buildRuntimeFrame() {
  const frames = state.currentSampleFrames.length > 0
    ? state.currentSampleFrames
    : sampleToPreviewFrames(state.recordedSamples.at(-1));
  const latest = frames[frames.length - 1];
  if (!latest) {
    return null;
  }

  return {
    Time: latest.time,
    Confidence: 1,
    StaticGesture: latest.staticGesture,
    PalmCenter: latest.palm,
    Landmarks: latest.landmarks.map((point) => ({ x: point.x, y: point.y }))
  };
}

function buildRuntimeTemplate(template) {
  return {
    ...template,
    trajectoryTemplates: template.TrajectoryTemplates || [],
    samples: template.Samples || []
  };
}

function resolveBestTemplate(frame, templates) {
  if (!frame || templates.length === 0) {
    return null;
  }

  let best = null;
  for (const template of templates) {
    const score = scoreTemplate(frame, template);
    if (!best || score < best.score) {
      best = { ...template, score };
    }
  }

  return best && Number.isFinite(best.score) ? best : null;
}

function scoreTemplate(frame, template) {
  const currentTrajectory = resampleTrajectory(state.currentSampleFrames.map((item) => item.palm));
  if (template.Kind === 'StaticPose') {
    return scoreStaticTemplate(frame, template);
  }

  const templateTrajectory = (template.TrajectoryTemplates?.[0]?.Points || []).map((point) => normalizePoint(point));
  if (templateTrajectory.length >= 2 && currentTrajectory.length >= 2) {
    return dtwScore(normalizeTrajectory(currentTrajectory), normalizeTrajectory(templateTrajectory));
  }

  return scoreDynamicRule(template, currentTrajectory);
}

function scoreStaticTemplate(frame, template) {
  const sample = template.Samples?.[0];
  if (!sample?.Frames?.length) {
    return Number.POSITIVE_INFINITY;
  }

  const sampleFrame = sample.Frames[0];
  return pointDistance(frame.PalmCenter, sampleFrame.PalmCenter);
}

function scoreDynamicRule(template, trajectory) {
  const sample = template.Samples?.[0];
  if (!sample?.Frames?.length || trajectory.length < 2) {
    return Number.POSITIVE_INFINITY;
  }

  const sampleTrajectory = resampleTrajectory(sample.Frames.map((item) => normalizePalmCenter(item)));
  return dtwScore(normalizeTrajectory(trajectory), normalizeTrajectory(sampleTrajectory));
}

function buildSampleFromFrames(frames) {
  return {
    SampleId: crypto.randomUUID(),
    Handedness: 'Right',
    DurationSeconds: Math.max(0.01, frames.at(-1).time - frames[0].time),
    Frames: frames.map((frame) => ({
      Time: frame.time,
      Confidence: 1,
      StaticGesture: frame.staticGesture,
      PalmCenter: { ...frame.palm },
      Landmarks: frame.landmarks.map((point) => ({ X: point.x, Y: point.y }))
    }))
  };
}

function sampleToPreviewFrames(sample) {
  if (!sample?.Frames?.length) {
    return [];
  }

  return sample.Frames.map((frame) => ({
    time: Number(frame.Time) || 0,
    palm: normalizePalmCenter(frame),
    landmarks: normalizeLandmarks(frame.Landmarks)
  }));
}

function normalizePalmCenter(frame) {
  const palm = frame?.PalmCenter || frame?.palm || frame?.Palm || frame?.palmCenter || frame?.palmcenter;
  if (palm && Number.isFinite(palm.x) && Number.isFinite(palm.y)) {
    return { x: palm.x, y: palm.y };
  }

  return { x: 0.5, y: 0.5 };
}

function normalizeLandmarks(landmarks) {
  if (!Array.isArray(landmarks)) {
    return [];
  }

  return landmarks.map((point) => ({
    x: point?.X ?? point?.x ?? 0.5,
    y: point?.Y ?? point?.y ?? 0.5
  }));
}

function normalizeSampleList(samples) {
  if (!Array.isArray(samples)) {
    return [];
  }

  return samples.map((sample) => {
    const frames = normalizeFrameList(sample?.Frames || sample?.frames || []);
    return {
      SampleId: sample?.SampleId || sample?.sampleId || crypto.randomUUID(),
      Handedness: sample?.Handedness || sample?.handedness || 'Unknown',
      DurationSeconds: Number(sample?.DurationSeconds ?? sample?.durationSeconds) || 0,
      Frames: frames
    };
  }).filter((sample) => sample.Frames.length > 0);
}

function normalizeFrameList(frames) {
  if (!Array.isArray(frames)) {
    return [];
  }

  return frames.map((frame) => ({
    Time: Number(frame?.Time ?? frame?.time) || 0,
    Confidence: Number(frame?.Confidence ?? frame?.confidence) || 1,
    StaticGesture: frame?.StaticGesture || frame?.staticGesture || 'None',
    PalmCenter: normalizePalmCenter(frame),
    Landmarks: normalizeLandmarks(frame?.Landmarks || frame?.landmarks)
      .map((point) => ({ X: point.x, Y: point.y }))
  }));
}

function normalizeTrajectoryTemplateList(templates) {
  if (!Array.isArray(templates)) {
    return [];
  }

  return templates.map((template) => ({
    SampleId: template?.SampleId || template?.sampleId || crypto.randomUUID(),
    DurationSeconds: Number(template?.DurationSeconds ?? template?.durationSeconds) || 0,
    Points: normalizeLandmarks(template?.Points || template?.points)
  }));
}

function buildTemplate({ gestureId, displayName, targetIntent, requiredSamples, samples }) {
  const normalizedSamples = samples.slice(0, Math.max(1, requiredSamples));
  return {
    GestureId: gestureId,
    DisplayName: displayName,
    Kind: 'DynamicMotion',
    RequiredHandedness: 'Right',
    TargetIntent: targetIntent,
    MatchThreshold: 0.18,
    DynamicRule: {
      Pattern: 'Directional',
      Direction: 'Any',
      RequireOpenPalm: true,
      MinimumOpenPalmRatio: 0.65,
      MinimumDistance: 0.08,
      MaximumDrift: 0.18,
      MinimumDuration: 0.08,
      MaximumDuration: 2.0,
      RepeatCount: 2,
      MinimumPathRatio: 1.6,
      MaximumClosureDistance: 0.12
    },
    Samples: normalizedSamples,
    TrajectoryTemplates: normalizedSamples.map((sample) => ({
      SampleId: sample.SampleId,
      DurationSeconds: sample.DurationSeconds,
      Points: resampleTrajectory(sample.Frames.map((item) => normalizePalmCenter(item))).map((point) => ({ x: point.x, y: point.y }))
    }))
  };
}

function normalizeImportedTemplates(parsed) {
  if (parsed?.Template) {
    return [normalizeTemplate(parsed.Template)];
  }

  if (Array.isArray(parsed?.Templates)) {
    return parsed.Templates.map(normalizeTemplate).filter(Boolean);
  }

  if (parsed?.GestureId) {
    return [normalizeTemplate(parsed)];
  }

  return [];
}

function normalizeTemplate(template) {
  if (!template?.GestureId) {
    return null;
  }

  const samples = normalizeSampleList(template.Samples || template.samples || []);
  const trajectoryTemplates = normalizeTrajectoryTemplateList(template.TrajectoryTemplates || template.trajectoryTemplates || []);
  return {
    GestureId: template.GestureId,
    DisplayName: template.DisplayName || template.displayName || template.GestureId,
    Kind: template.Kind || template.kind || 'DynamicMotion',
    RequiredHandedness: template.RequiredHandedness || template.requiredHandedness || 'Unknown',
    TargetIntent: template.TargetIntent || template.targetIntent || 'CustomGesture',
    MatchThreshold: template.MatchThreshold ?? template.matchThreshold ?? 0.18,
    DynamicRule: template.DynamicRule || template.dynamicRule || null,
    Samples: samples,
    TrajectoryTemplates: trajectoryTemplates
  };
}

function templateToUnityFile(template) {
  return {
    Template: template
  };
}

function upsertTemplate(template) {
  const index = state.templates.findIndex((item) => item.GestureId === template.GestureId);
  if (index >= 0) {
    state.templates[index] = template;
  } else {
    state.templates.unshift(template);
  }
}

function loadTemplates() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return [];
    }

    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function saveTemplates() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(state.templates));
}

function buildGestureId(name) {
  return `custom_${String(name || 'gesture').trim().toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '') || 'gesture'}`;
}

function downloadJson(value, filename) {
  const blob = new Blob([JSON.stringify(value, null, 2)], { type: 'application/json;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}

function classifyGesture(landmarks) {
  if (!landmarks) {
    return { key: 'none', label: '未检测到手' };
  }

  const indexExtended = isFingerExtended(landmarks, 8, 6, 5);
  const middleExtended = isFingerExtended(landmarks, 12, 10, 9);
  const ringExtended = isFingerExtended(landmarks, 16, 14, 13);
  const pinkyExtended = isFingerExtended(landmarks, 20, 18, 17);
  const spread = distance(landmarks[8], landmarks[20]);
  const fingersUp = [indexExtended, middleExtended, ringExtended, pinkyExtended].filter(Boolean).length;

  if (!indexExtended && !middleExtended && !ringExtended && !pinkyExtended) {
    return { key: 'fist', label: '握拳' };
  }

  if (indexExtended && middleExtended && !ringExtended && !pinkyExtended && spread > 0.12) {
    return { key: 'v', label: 'V 手势' };
  }

  if (indexExtended && !middleExtended && !ringExtended && !pinkyExtended) {
    return { key: 'point', label: '指向' };
  }

  if (fingersUp >= 4) {
    return { key: 'openPalm', label: '张掌' };
  }

  return { key: 'unknown', label: '未知手势' };
}

function isFingerExtended(landmarks, tipIndex, pipIndex, mcpIndex) {
  const tip = landmarks[tipIndex];
  const pip = landmarks[pipIndex];
  const mcp = landmarks[mcpIndex];
  return tip.y < pip.y - 0.015 && pip.y < mcp.y - 0.005;
}

function buildPalm(landmarks) {
  return {
    x: (landmarks[0].x + landmarks[5].x + landmarks[17].x) / 3,
    y: (landmarks[0].y + landmarks[5].y + landmarks[17].y) / 3
  };
}

function distance(a, b) {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

function pointDistance(a, b) {
  return distance(a, b);
}

function resampleTrajectory(points) {
  if (!points || points.length < 2) {
    return [];
  }

  const filtered = [points[0]];
  for (let index = 1; index < points.length; index += 1) {
    if (distance(filtered[filtered.length - 1], points[index]) > 0.0001) {
      filtered.push(points[index]);
    }
  }

  if (filtered.length < 2) {
    return [];
  }

  const cumulative = [0];
  let total = 0;
  for (let index = 1; index < filtered.length; index += 1) {
    total += distance(filtered[index - 1], filtered[index]);
    cumulative.push(total);
  }

  if (total <= 0.0001) {
    return [];
  }

  const resampled = [];
  for (let index = 0; index < DTW_RESAMPLE_POINTS; index += 1) {
    const target = (total * index) / (DTW_RESAMPLE_POINTS - 1);
    let segment = 1;
    while (segment < cumulative.length && cumulative[segment] < target) {
      segment += 1;
    }

    const prev = Math.max(0, segment - 1);
    const next = Math.min(filtered.length - 1, segment);
    const start = cumulative[prev];
    const end = cumulative[next];
    const t = end <= start ? 0 : (target - start) / (end - start);
    resampled.push({
      x: lerp(filtered[prev].x, filtered[next].x, t),
      y: lerp(filtered[prev].y, filtered[next].y, t)
    });
  }

  return resampled;
}

function normalizeTrajectory(points) {
  if (!points || points.length === 0) {
    return [];
  }

  const origin = points[0];
  const translated = points.map((point) => ({
    x: point.x - origin.x,
    y: point.y - origin.y
  }));
  const maxDistance = translated.reduce((max, point) => Math.max(max, Math.hypot(point.x, point.y)), 0) || 1;
  return translated.map((point) => ({
    x: point.x / maxDistance,
    y: point.y / maxDistance
  }));
}

function normalizePoint(point) {
  if (!point) {
    return { x: 0.5, y: 0.5 };
  }

  if (Number.isFinite(point.x) && Number.isFinite(point.y)) {
    return { x: point.x, y: point.y };
  }

  if (Number.isFinite(point.X) && Number.isFinite(point.Y)) {
    return { x: point.X, y: point.Y };
  }

  return { x: 0.5, y: 0.5 };
}

function normalizePoints(points, width, height) {
  if (!points.length) {
    return [];
  }

  const xs = points.map((point) => point.x);
  const ys = points.map((point) => point.y);
  const minX = Math.min(...xs);
  const maxX = Math.max(...xs);
  const minY = Math.min(...ys);
  const maxY = Math.max(...ys);
  const spanX = Math.max(0.001, maxX - minX);
  const spanY = Math.max(0.001, maxY - minY);
  return points.map((point) => ({
    x: 24 + ((point.x - minX) / spanX) * (width - 48),
    y: 24 + ((point.y - minY) / spanY) * (height - 48)
  }));
}

function dtwScore(a, b) {
  if (!a.length || !b.length) {
    return Number.POSITIVE_INFINITY;
  }

  const rows = a.length;
  const columns = b.length;
  const costs = Array.from({ length: rows + 1 }, () => Array(columns + 1).fill(Number.POSITIVE_INFINITY));
  costs[0][0] = 0;

  for (let row = 1; row <= rows; row += 1) {
    for (let column = 1; column <= columns; column += 1) {
      const local = Math.hypot(a[row - 1].x - b[column - 1].x, a[row - 1].y - b[column - 1].y);
      costs[row][column] = local + Math.min(costs[row - 1][column], costs[row][column - 1], costs[row - 1][column - 1]);
    }
  }

  return costs[rows][columns] / (rows + columns);
}

function drawLandmarks(context, landmarks, width, height) {
  context.strokeStyle = 'rgba(139, 233, 255, 0.8)';
  context.lineWidth = 2;
  const pairs = [
    [0, 1], [1, 2], [2, 3], [3, 4],
    [0, 5], [5, 6], [6, 7], [7, 8],
    [5, 9], [9, 10], [10, 11], [11, 12],
    [9, 13], [13, 14], [14, 15], [15, 16],
    [13, 17], [17, 18], [18, 19], [19, 20],
    [0, 17]
  ];

  context.clearRect(0, 0, width, height);
  for (const [from, to] of pairs) {
    const a = landmarks[from];
    const b = landmarks[to];
    context.beginPath();
    context.moveTo(a.x * width, a.y * height);
    context.lineTo(b.x * width, b.y * height);
    context.stroke();
  }

  for (const point of landmarks) {
    context.beginPath();
    context.fillStyle = '#8be9ff';
    context.arc(point.x * width, point.y * height, 4, 0, Math.PI * 2);
    context.fill();
  }
}

function drawGestureHud(context, label, width, height) {
  context.fillStyle = 'rgba(4, 10, 19, 0.74)';
  context.fillRect(16, 16, 180, 42);
  context.fillStyle = '#edf4ff';
  context.font = '600 16px "Noto Sans SC", sans-serif';
  context.fillText(`当前手势：${label}`, 26, 42);
}

function setStatus(text, type = 'pending') {
  dom.cameraStatus.textContent = text;
  dom.cameraStatus.className = `pill ${type}`;
}

function refreshHandsIndicator() {
  if (state.handsError) {
    dom.handStatus.textContent = `引擎异常：${state.handsError}`;
    dom.handStatus.className = 'pill error';
    return;
  }

  if (!state.cameraReady) {
    dom.handStatus.textContent = '未启动摄像头';
    dom.handStatus.className = 'pill pending';
    return;
  }

  if (!state.handsReady) {
    dom.handStatus.textContent = '等待手势引擎';
    dom.handStatus.className = 'pill pending';
    return;
  }

  if (performance.now() - state.lastResultsAt > 1500) {
    dom.handStatus.textContent = '画面正常，等待手进入识别区';
    dom.handStatus.className = 'pill pending';
    return;
  }

  if (state.latestGestureLabel && state.latestGestureLabel !== '未检测到手') {
    dom.handStatus.textContent = `当前：${state.latestGestureLabel}`;
    dom.handStatus.className = 'pill live';
    return;
  }

  dom.handStatus.textContent = '未检测到手';
  dom.handStatus.className = 'pill pending';
}

function updateSampleProgress() {
  const requiredSamples = Number(dom.requiredSamples.value) || DEFAULT_REQUIRED_SAMPLES;
  dom.sampleCount.textContent = `${state.recordedSamples.length} / ${requiredSamples}`;
}

function finishRecordingSession(statusMessage) {
  if (!state.capturing) {
    return;
  }

  state.capturing = false;
  dom.recordSample.textContent = '录制样本';

  const finalizedFrames = state.currentSampleFrames.length > 0 ? state.currentSampleFrames : state.sampleSessionFrames;
  if (finalizedFrames.length > 0) {
    const sample = buildSampleFromFrames(finalizedFrames);
    state.recordedSamples.push(sample);
    state.sampleCompleteAt = performance.now();
  }

  state.currentSampleFrames = [];
  state.sampleSessionFrames = [];
  state.sampleStableEnterCount = 0;
  state.sampleStableExitCount = 0;
  state.sampleActive = false;
  updateCaptureStatus(statusMessage || '本组样本已保存。');

  const requiredSamples = Number(dom.requiredSamples.value) || DEFAULT_REQUIRED_SAMPLES;
  if (state.recordedSamples.length >= requiredSamples) {
    dom.captureStatus.textContent = `已收集 ${state.recordedSamples.length} 组样本，可以保存模板了。`;
    dom.cameraHint.textContent = '样本已足够，直接保存模板，或者继续补录更多样本。';
  }
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}

function lerp(start, end, amount) {
  return start + (end - start) * amount;
}

init();
