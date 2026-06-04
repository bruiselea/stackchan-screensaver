// スタックちゃん顔（プロシージャル）を Canvas に描画する。
// 本家 meganetaaan/m5stack-avatar (MIT) の挙動を参考に Web へ移植:
//   - 目/口を図形で描く
//   - ランダムな瞬き (blink)
//   - 呼吸 (breath) による上下のゆらぎ
//   - 時々の視線移動 (saccade)
// 顔の見た目パラメータは FACE で調整できる。

const FACE = {
  bg: '#000000',
  color: '#e8e8e8',   // 目・口の色
  eyeR: 56,           // 目の半径(px) ※基準1080pで自動スケール
  eyeGap: 320,        // 左右の目の中心間隔(px)
  eyeY: -40,          // 顔中心からの目の縦オフセット
  mouthY: 140,        // 顔中心からの口の縦オフセット
  mouthW: 220,        // 口の幅
}

const canvas = document.getElementById('face')
const ctx = canvas.getContext('2d')

let W = 0, H = 0, scale = 1
function resize() {
  const dpr = window.devicePixelRatio || 1
  W = window.innerWidth
  H = window.innerHeight
  canvas.width = W * dpr
  canvas.height = H * dpr
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0)
  scale = Math.min(W, H * (16 / 9)) / 1920 // 基準幅に対するスケール
}
window.addEventListener('resize', resize)
resize()

// ---- アニメーション状態 ----
let blink = 0            // 0=開, 1=閉
let nextBlinkAt = 0
let blinkPhase = 'idle'  // idle | closing | opening
let blinkT = 0
let gaze = { x: 0, y: 0, tx: 0, ty: 0 }
let nextSaccadeAt = 0

function scheduleBlink(now) {
  // 2〜6 秒に一度まばたき
  nextBlinkAt = now + 2000 + Math.floor((Math.sin(now * 0.013) * 0.5 + 0.5) * 4000)
}
function scheduleSaccade(now) {
  nextSaccadeAt = now + 3000 + (Math.sin(now * 0.007) * 0.5 + 0.5) * 5000
}

function updateBlink(now, dt) {
  if (blinkPhase === 'idle' && now >= nextBlinkAt) { blinkPhase = 'closing'; blinkT = 0 }
  const SPEED = 7 // 大きいほど速い
  if (blinkPhase === 'closing') {
    blink = Math.min(1, blink + dt * SPEED)
    if (blink >= 1) blinkPhase = 'opening'
  } else if (blinkPhase === 'opening') {
    blink = Math.max(0, blink - dt * SPEED)
    if (blink <= 0) { blinkPhase = 'idle'; scheduleBlink(now) }
  }
}

function updateGaze(now, dt) {
  if (now >= nextSaccadeAt) {
    // 軽く視線を振る（ときどき中央へ戻す）
    const back = (Math.sin(now * 0.011) > 0.4)
    gaze.tx = back ? 0 : (Math.sin(now * 0.31) * 26)
    gaze.ty = back ? 0 : (Math.cos(now * 0.27) * 14)
    scheduleSaccade(now)
  }
  gaze.x += (gaze.tx - gaze.x) * Math.min(1, dt * 6)
  gaze.y += (gaze.ty - gaze.y) * Math.min(1, dt * 6)
}

function drawEye(cx, cy, openRatio) {
  const r = FACE.eyeR * scale
  ctx.save()
  ctx.translate(cx, cy)
  ctx.fillStyle = FACE.color
  ctx.beginPath()
  // 瞬き = 縦をつぶす。完全に閉じると細い線
  const ry = Math.max(r * 0.06, r * openRatio)
  ctx.ellipse(0, 0, r, ry, 0, 0, Math.PI * 2)
  ctx.fill()
  ctx.restore()
}

function drawMouth(cx, cy, open) {
  ctx.save()
  ctx.strokeStyle = FACE.color
  ctx.fillStyle = FACE.color
  ctx.lineWidth = 10 * scale
  ctx.lineCap = 'round'
  const w = FACE.mouthW * scale
  if (open < 0.04) {
    // 閉じ口 = 横線
    ctx.beginPath()
    ctx.moveTo(cx - w / 2, cy)
    ctx.lineTo(cx + w / 2, cy)
    ctx.stroke()
  } else {
    // 開き口 = 角丸の塗り
    const h = open * 90 * scale
    ctx.beginPath()
    ctx.ellipse(cx, cy + h / 4, w / 2, h, 0, 0, Math.PI * 2)
    ctx.fill()
  }
  ctx.restore()
}

let last = 0
function frame(now) {
  if (!last) { last = now; scheduleBlink(now); scheduleSaccade(now) }
  const dt = Math.min(0.05, (now - last) / 1000)
  last = now

  updateBlink(now, dt)
  updateGaze(now, dt)

  // 呼吸: 顔全体をゆっくり上下
  const breath = Math.sin(now * 0.0016) * 10 * scale
  // 待機時の口: 呼吸に合わせてほんの少しだけ動く
  const mouthOpen = (Math.sin(now * 0.0016) * 0.5 + 0.5) * 0.08

  ctx.fillStyle = FACE.bg
  ctx.fillRect(0, 0, W, H)

  const cx = W / 2
  const cy = H / 2 + breath
  const gap = FACE.eyeGap * scale
  const eyeY = cy + FACE.eyeY * scale + gaze.y * scale
  const openRatio = 1 - blink

  drawEye(cx - gap / 2 + gaze.x * scale, eyeY, openRatio)
  drawEye(cx + gap / 2 + gaze.x * scale, eyeY, openRatio)
  drawMouth(cx, cy + FACE.mouthY * scale, mouthOpen)

  requestAnimationFrame(frame)
}
requestAnimationFrame(frame)

// メインプロセスからの show/hide（今は描画は常時。将来ここで一時停止等を制御）
if (window.screensaver) {
  window.screensaver.onShow(() => { last = 0 })
  window.screensaver.onHide(() => {})
}
